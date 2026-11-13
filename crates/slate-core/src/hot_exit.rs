// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Plan 44 WB-29a: the hot-exit store — unsaved editor buffers kept
//! durably outside the vault, so closing, quitting or a crash keeps them
//! and they come back unsaved when the vault reopens.
//!
//! The design is `docs/plans/44_windows_backlog/specs/
//! wb29a_hot_exit_shared_design_and_core_store.md`; its numbered sections
//! are cited below as "§n".
//!
//! One [`HotExitStore`] per process, over the host's device-local root.
//! It never touches a vault and never borrows a session: every call takes
//! the vault's [`HotExitVaultKey`] (plain data the session hands out) and,
//! for a writer, its [`HotExitClaim`].
//!
//! **Claims (§2, amended by WB-29 §7).** A tab owns its entry through a
//! claim carrying a random incarnation the host makes first. A claim
//! replaces only the current claim it names, so a stale claim never
//! displaces a newer one; a newer claim, a retirement, or a vault discard
//! (which advances the vault's epoch) invalidates every older writer, in
//! any thread or process. A write publishes only under its own live
//! claim; a retirement removes the claims and payloads of the
//! incarnations it lists and nothing else. A host owns a vault's store
//! across processes through [`HotExitStore::hold_vault`].
//!
//! **Locking (§8).** Every check and publish for a vault runs under an
//! exclusive lock on the vault directory's `lock` file, held only for the
//! check, the rename and their syncs. Each call opens its own handle, and
//! the lock belongs to the handle (`LockFileEx` on Windows, `flock` on an
//! open file description elsewhere), so it serialises two threads of one
//! process exactly as it serialises two processes. A payload
//! write stages its file outside both, so a stalled payload never delays a
//! discard.
//!
//! **Durability (§2).** A file is synced before its rename and the rename
//! is made durable after; every sync failure is an error, never a
//! best-effort step. All I/O goes through [`HotExitFs`], so facts can see
//! the order of calls and fail any of them.
//!
//! **Nothing unread is deleted (§1).** No retention, no pruning: an entry
//! leaves through its tab's release, a vault discard, or a restore the
//! next write supersedes. Only temp files over an hour old and files a
//! cut-short release or discard left behind are cleaned.

use std::fs::File;
use std::io::{self, Write};
use std::path::{Path, PathBuf};
use std::sync::{Arc, Mutex};
use std::time::{Duration, SystemTime};

use serde::{Deserialize, Serialize};

#[cfg(test)]
mod tests;

/// Largest entry text the store keeps (§1).
pub const MAX_ENTRY_TEXT_BYTES: usize = 8 * 1024 * 1024;
/// Most entries one vault keeps (§1).
pub const MAX_ENTRIES_PER_VAULT: usize = 256;
/// Most bytes of entry files one vault keeps (§1).
pub const MAX_BYTES_PER_VAULT: u64 = 64 * 1024 * 1024;
/// Most vault directories one store keeps (§1).
pub const MAX_VAULTS: usize = 32;
/// Temp files younger than this may belong to a live write (§3).
pub const ORPHAN_TEMP_AGE: Duration = Duration::from_secs(60 * 60);
/// Longest entry id: the host's tab id (§1).
pub const MAX_ENTRY_ID_LEN: usize = 64;

const STORE_DIR: &str = "hot-exit";
const VERSION_DIR: &str = "v1";
const VAULTS_DIR: &str = "vaults";
const EPOCH_FILE: &str = "epoch";
const CLAIMS_DIR: &str = "claims";
const ENTRIES_DIR: &str = "entries";
const LOCK_FILE: &str = "lock";
const STORE_LOCK_FILE: &str = "store.lock";
const HOLDS_DIR: &str = "holds";
const HOLD_EXT: &str = ".lock";
const ENTRY_EXT: &str = ".json";
const TEMP_MARK: &str = ".tmp-";
const SCHEMA: u32 = 1;

/// The vault's store key: opaque text core makes from a lasting, local
/// root identity (§1). The host passes it back unread.
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct HotExitVaultKey {
    pub text: String,
}

/// A tab's ownership of its entry (§2).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct HotExitClaim {
    pub entry_id: String,
    pub incarnation: String,
    pub epoch: u64,
    /// Every incarnation this claim displaced — the claim it expected and
    /// the stored payload's, when an older claim wrote it — so its release
    /// retires that text too (§6; WB-29 §7).
    pub adopted: Vec<String>,
}

/// A claim current at a snapshot's epoch, with or without a payload
/// (WB-29 §7): an adopter that has not written yet, or a claim whose first
/// write never became durable.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct HotExitCurrentClaim {
    pub entry_id: String,
    pub incarnation: String,
}

/// What a write carries besides its claim (§1's entry fields).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct HotExitEntryWrite {
    pub path: String,
    pub text: String,
    pub baseline_hash: Option<String>,
    pub baseline_file_id: Option<String>,
}

/// An entry as stored and as `read_buffers` returns it.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct HotExitEntry {
    pub schema: u32,
    pub entry_id: String,
    pub incarnation: String,
    pub generation: u64,
    pub path: String,
    pub text: String,
    pub baseline_hash: Option<String>,
    pub baseline_file_id: Option<String>,
    pub written_at: String,
}

/// The vault's entries at one moment, with the epoch a writer claims in.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct HotExitSnapshot {
    pub epoch: u64,
    pub entries: Vec<HotExitEntry>,
    /// Every claim at `epoch`. An entry may sit under a claim of another
    /// incarnation (an adopter that has not written), and a claim may have
    /// no entry at all; a host needs both to adopt and to retire.
    pub claims: Vec<HotExitCurrentClaim>,
}

/// Why a claim, write or release was not applied (§2).
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum HotExitSupersession {
    /// The vault was discarded after this epoch.
    Epoch { current: u64 },
    /// A newer claim, or a release, owns the entry now.
    Claim,
    /// The stored entry, under this same claim, is at least this
    /// generation and is durable.
    Newer { stored_generation: u64 },
}

/// A cap the store will not exceed (§1).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum HotExitRefusal {
    EntryTooLarge,
    VaultFull,
    TooManyVaults,
}

impl HotExitRestore {
    /// The vault-relative path the decision opens.
    #[must_use]
    pub fn path(&self) -> &str {
        match self {
            Self::Restore { path }
            | Self::RestoreStale { path }
            | Self::ExistsNow { path }
            | Self::Gone { path }
            | Self::Recovered { path } => path,
        }
    }
}

impl HotExitRefusal {
    /// Core's reason sentence fragment (§4), for `UnsavedNotesNotKept`.
    pub fn reason(self) -> &'static str {
        match self {
            HotExitRefusal::EntryTooLarge => crate::a11y::HOT_EXIT_REASON_ENTRY_TOO_LARGE,
            HotExitRefusal::VaultFull => crate::a11y::HOT_EXIT_REASON_VAULT_FULL,
            HotExitRefusal::TooManyVaults => crate::a11y::HOT_EXIT_REASON_TOO_MANY_VAULTS,
        }
    }
}

/// What `VaultSession::hot_exit_key` answers (§1, §7).
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum HotExitKeyResult {
    Key(HotExitVaultKey),
    /// No lasting, local identity: no hot exit for this vault. `reason`
    /// is one of `a11y::HOT_EXIT_REASON_*`.
    Unavailable {
        reason: &'static str,
    },
}

/// How one kept entry comes back into the open vault (§3).
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum HotExitRestore {
    /// The note is as the buffer was loaded: reopen it, dirty.
    Restore { path: String },
    /// The note changed on disk: reopen it dirty and externally stale.
    RestoreStale { path: String },
    /// Another file sits at the path (or the buffer was a new note): a
    /// new note whose first save is create-exclusive.
    ExistsNow { path: String },
    /// Nothing at the path: a new note there.
    Gone { path: String },
    /// The path no longer validates: a new note at `path`, a free
    /// `Recovered note.md` name at the vault root.
    Recovered { path: String },
}

/// One tab a restore installs a kept buffer into (plan 43 PR 29 §7).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct HotExitPlannedTab {
    /// For [`HotExitRestorePlan::layout`] and [`HotExitRestorePlan::displaced`]
    /// the layout tab's own id; for [`HotExitRestorePlan::new_tabs`] the
    /// entry's id, which the host keeps as the tab's recovery id.
    pub entry_id: String,
    /// The kept entry the tab is restored from (for a displaced tab, the
    /// entry whose text it shows).
    pub entry: HotExitEntry,
    pub decision: HotExitRestore,
    /// Where the tab opens: the decision's path.
    pub path: String,
    /// The buffer the tab holds.
    pub text: String,
    /// The snapshot's current claim on `entry_id`, when one names it: the
    /// incarnation the tab's first claim expects (WB-29 §2, §5).
    pub observed_claim: Option<String>,
}

/// A peer's entry whose text another peer's recovered note already
/// carries: retired once the workspace is built, never a second note.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct HotExitRedundantEntry {
    pub entry_id: String,
    /// The entry's payload incarnation, and its observed claim's when that
    /// differs.
    pub incarnations: Vec<String>,
}

/// How a snapshot's entries come back into a workspace (plan 43 PR 29
/// §7; the rules of WB-29 §5, which both hosts share).
#[derive(Debug, Clone, PartialEq, Eq, Default)]
pub struct HotExitRestorePlan {
    /// Entries restored into the layout tab that holds their id.
    pub layout: Vec<HotExitPlannedTab>,
    /// Layout tabs whose id a recovered peer took: the host gives each a
    /// fresh id; it shows the latest text of its note.
    pub displaced: Vec<HotExitPlannedTab>,
    /// Entries no layout tab holds, in `written_at` order.
    pub new_tabs: Vec<HotExitPlannedTab>,
    /// Current claims with no payload.
    pub orphan_claims: Vec<HotExitCurrentClaim>,
    pub redundant: Vec<HotExitRedundantEntry>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum HotExitClaimOutcome {
    Claimed(HotExitClaim),
    Superseded(HotExitSupersession),
    Refused(HotExitRefusal),
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum HotExitWriteOutcome {
    Durable { generation: u64 },
    Superseded(HotExitSupersession),
    Refused(HotExitRefusal),
}

#[derive(Debug, thiserror::Error)]
pub enum HotExitError {
    #[error("hot-exit store I/O failed: {detail}")]
    Io { detail: String },
    #[error("hot-exit entry id is not a tab id")]
    InvalidEntryId,
    #[error("hot-exit incarnation is not 32 lower-case hex digits")]
    InvalidIncarnation,
    #[error("hot-exit records for this vault are damaged")]
    Damaged,
    #[error("another process holds this vault's hot-exit store")]
    HeldElsewhere,
}

impl HotExitError {
    /// Core's reason sentence fragment (§4).
    pub fn reason(&self) -> &'static str {
        match self {
            HotExitError::Io { .. }
            | HotExitError::InvalidEntryId
            | HotExitError::InvalidIncarnation => crate::a11y::HOT_EXIT_REASON_STORE_UNWRITABLE,
            HotExitError::Damaged => crate::a11y::HOT_EXIT_REASON_DAMAGED,
            HotExitError::HeldElsewhere => crate::a11y::HOT_EXIT_REASON_HELD_ELSEWHERE,
        }
    }
}

/// A host's exclusive hold on one vault's store across processes (WB-29
/// §7): an OS file lock on the vault's hold file, released by `release`,
/// by drop, or by the OS when the process dies, so a crash never leaves a
/// vault held.
#[derive(Debug)]
pub struct HotExitVaultHold {
    _file: File,
}

impl HotExitVaultHold {
    /// Releases the hold now (drop does the same).
    pub fn release(self) {}
}

/// A new claim incarnation: 32 lower-case hex digits from the store's
/// random source. A host makes one before it claims, so a claim that
/// errors after reaching the disk is still known (WB-29 §0 O2).
pub fn new_hot_exit_incarnation() -> Result<String, HotExitError> {
    fresh_incarnation()
}

impl From<io::Error> for HotExitError {
    fn from(error: io::Error) -> Self {
        // The kind only: a path would carry a vault's note name (lib.rs's
        // privacy rule).
        HotExitError::Io {
            detail: format!("{:?}", error.kind()),
        }
    }
}

/// One step of the store's I/O, named so a fact can fail it (§Tests).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum HotExitStep {
    WriteFile,
    SyncFile,
    Rename,
    SyncDir,
    Remove,
    CreateDir,
}

/// Every file operation the store makes. The real implementation is
/// [`RealHotExitFs`]; facts wrap it to record and fail steps.
pub trait HotExitFs: Send + Sync {
    /// Creates `path` (it must not exist) with `bytes`; not yet durable.
    fn write_new(&self, path: &Path, bytes: &[u8]) -> io::Result<()>;
    /// Makes `path`'s contents durable (F_FULLFSYNC on macOS).
    fn sync_file(&self, path: &Path) -> io::Result<()>;
    /// Replaces `to` with `from` (write-through on Windows).
    fn rename(&self, from: &Path, to: &Path) -> io::Result<()>;
    /// Makes `dir`'s entries durable (a no-op on Windows, whose renames
    /// are write-through).
    fn sync_dir(&self, dir: &Path) -> io::Result<()>;
    fn remove(&self, path: &Path) -> io::Result<()>;
    fn create_dir(&self, path: &Path) -> io::Result<()>;
    fn read(&self, path: &Path) -> io::Result<Vec<u8>>;
    fn list(&self, dir: &Path) -> io::Result<Vec<String>>;
    fn modified(&self, path: &Path) -> io::Result<SystemTime>;
    fn size(&self, path: &Path) -> io::Result<u64>;
}

/// The store's real I/O.
pub struct RealHotExitFs;

impl HotExitFs for RealHotExitFs {
    fn write_new(&self, path: &Path, bytes: &[u8]) -> io::Result<()> {
        let mut file = std::fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(path)?;
        file.write_all(bytes)
    }

    fn sync_file(&self, path: &Path) -> io::Result<()> {
        let file = std::fs::OpenOptions::new().write(true).open(path)?;
        full_sync(&file)
    }

    fn rename(&self, from: &Path, to: &Path) -> io::Result<()> {
        durable_rename(from, to)
    }

    fn sync_dir(&self, dir: &Path) -> io::Result<()> {
        sync_directory(dir)
    }

    fn remove(&self, path: &Path) -> io::Result<()> {
        std::fs::remove_file(path)
    }

    fn create_dir(&self, path: &Path) -> io::Result<()> {
        std::fs::create_dir(path)
    }

    fn read(&self, path: &Path) -> io::Result<Vec<u8>> {
        std::fs::read(path)
    }

    fn list(&self, dir: &Path) -> io::Result<Vec<String>> {
        let mut names = Vec::new();
        for entry in std::fs::read_dir(dir)? {
            let entry = entry?;
            if let Some(name) = entry.file_name().to_str() {
                names.push(name.to_string());
            }
        }
        names.sort();
        Ok(names)
    }

    fn modified(&self, path: &Path) -> io::Result<SystemTime> {
        std::fs::metadata(path)?.modified()
    }

    fn size(&self, path: &Path) -> io::Result<u64> {
        Ok(std::fs::metadata(path)?.len())
    }
}

#[cfg(target_os = "macos")]
fn full_sync(file: &File) -> io::Result<()> {
    use std::os::fd::AsRawFd;
    // macOS `fsync` does not flush the drive's cache; F_FULLFSYNC does.
    // SAFETY: the descriptor is open for the call's whole duration.
    if unsafe { libc::fcntl(file.as_raw_fd(), libc::F_FULLFSYNC) } == -1 {
        return Err(io::Error::last_os_error());
    }
    Ok(())
}

#[cfg(not(target_os = "macos"))]
fn full_sync(file: &File) -> io::Result<()> {
    file.sync_all()
}

#[cfg(windows)]
fn durable_rename(from: &Path, to: &Path) -> io::Result<()> {
    use std::os::windows::ffi::OsStrExt;
    use windows_sys::Win32::Storage::FileSystem::{
        MOVEFILE_REPLACE_EXISTING, MOVEFILE_WRITE_THROUGH, MoveFileExW,
    };
    // A raw Win32 call gets none of std's long-path handling, so a store
    // under a deep profile would pass MAX_PATH: hand it the verbatim form.
    let wide = |path: &Path| -> io::Result<Vec<u16>> {
        let absolute: Vec<u16> = std::path::absolute(path)?
            .as_os_str()
            .encode_wide()
            .collect();
        let utf16 = |text: &str| text.encode_utf16().collect::<Vec<u16>>();
        let mut verbatim =
            if absolute.starts_with(&utf16(r"\\?\")) || absolute.starts_with(&utf16(r"\\.\")) {
                absolute
            } else if absolute.starts_with(&utf16(r"\\")) {
                [utf16(r"\\?\UNC"), absolute[1..].to_vec()].concat()
            } else {
                [utf16(r"\\?\"), absolute].concat()
            };
        verbatim.push(0);
        Ok(verbatim)
    };
    let (from, to) = (wide(from)?, wide(to)?);
    // MOVEFILE_WRITE_THROUGH: the call returns only once the rename is on
    // disk, the documented way to make a rename durable on Windows (§2).
    // SAFETY: both buffers are NUL-terminated and outlive the call.
    let moved = unsafe {
        MoveFileExW(
            from.as_ptr(),
            to.as_ptr(),
            MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH,
        )
    };
    if moved == 0 {
        return Err(io::Error::last_os_error());
    }
    Ok(())
}

#[cfg(not(windows))]
fn durable_rename(from: &Path, to: &Path) -> io::Result<()> {
    std::fs::rename(from, to)
}

#[cfg(windows)]
fn sync_directory(dir: &Path) -> io::Result<()> {
    use std::os::windows::fs::OpenOptionsExt as _;
    use windows_sys::Win32::Storage::FileSystem::FILE_FLAG_BACKUP_SEMANTICS;
    // FlushFileBuffers on a directory opened for writing commits its
    // entries, as fsync on a directory does on POSIX: a delete (a release,
    // a discard) is durable once this returns, not only a write-through
    // rename (§2, §6).
    std::fs::OpenOptions::new()
        .write(true)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS)
        .open(dir)?
        .sync_all()
}

#[cfg(not(windows))]
fn sync_directory(dir: &Path) -> io::Result<()> {
    full_sync(&File::open(dir)?)
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
struct ClaimRecord {
    incarnation: String,
    epoch: u64,
}

/// The process-lifetime store (§1, §8).
pub struct HotExitStore {
    root: PathBuf,
    fs: Arc<dyn HotExitFs>,
    now: Arc<dyn Fn() -> SystemTime + Send + Sync>,
    store_lock: Mutex<()>,
}

impl std::fmt::Debug for HotExitStore {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("HotExitStore").finish_non_exhaustive()
    }
}

/// The vault lock held for one call: the directory's exclusive file lock,
/// released when the handle closes.
struct VaultGuard {
    _file: File,
}

impl HotExitStore {
    /// Opens (creating) the store under `<device_root>/hot-exit/v1/`.
    pub fn open(device_root: &Path) -> Result<Self, HotExitError> {
        Self::open_with(
            device_root,
            Arc::new(RealHotExitFs),
            Arc::new(SystemTime::now),
        )
    }

    /// The store over an I/O seam and a clock (facts).
    pub fn open_with(
        device_root: &Path,
        fs: Arc<dyn HotExitFs>,
        now: Arc<dyn Fn() -> SystemTime + Send + Sync>,
    ) -> Result<Self, HotExitError> {
        // The device root is the host's own app-data folder: absolute and
        // already there, its name the host's to have made durable. Existence
        // alone proves nothing about a directory this store would create
        // (another opener may have just made it, unsynced), so the store
        // creates nothing above its own `hot-exit` directory.
        if !device_root.is_absolute() || !device_root.is_dir() {
            return Err(HotExitError::Io {
                detail: format!(
                    "the hot-exit device root must be an existing absolute folder: {}",
                    device_root.display()
                ),
            });
        }
        let root = device_root.join(STORE_DIR).join(VERSION_DIR);
        let vaults = root.join(VAULTS_DIR);
        std::fs::create_dir_all(&vaults)?;
        // Each directory's entry is durable only once its parent is synced:
        // sync every directory this store owns and the device root (whose
        // entry names `hot-exit`) on every open, whoever created them, so
        // nothing acknowledged later can vanish with an unsynced ancestor
        // (§2). Above the device root nothing is the store's: its parent may
        // not even be writable.
        for dir in [&vaults, &root, &device_root.join(STORE_DIR), device_root] {
            fs.sync_dir(dir)?;
        }
        Ok(Self {
            root,
            fs,
            now,
            store_lock: Mutex::new(()),
        })
    }

    /// The vault's current epoch: 0 before its first claim (§6).
    pub fn current_epoch(&self, key: &HotExitVaultKey) -> Result<u64, HotExitError> {
        let dir = self.vault_dir(key);
        if !dir.exists() {
            return Ok(0);
        }
        let _guard = self.lock_vault(key)?;
        self.read_epoch(&dir)
    }

    /// Claims `entry_id` for a new writer as `incarnation`, replacing only
    /// the current claim it names (§2 as amended by WB-29 §7): `expected`
    /// is the incarnation of the entry's current claim at `epoch`, or
    /// `None` for an entry with no current claim. Anything else is
    /// `Superseded { Claim }` and changes nothing. The same call retried
    /// after it reached the disk answers `Claimed` again.
    pub fn claim_entry(
        &self,
        key: &HotExitVaultKey,
        epoch: u64,
        entry_id: &str,
        expected: Option<&str>,
        incarnation: &str,
    ) -> Result<HotExitClaimOutcome, HotExitError> {
        validate_entry_id(entry_id)?;
        validate_incarnation(incarnation)?;
        // A vault with no directory is at epoch 0 with no current claim: a
        // claim that could only be refused there is refused before creating
        // it, so a refusal changes nothing and spends no vault cap. A vault
        // created meanwhile answers the next call under its lock.
        if !self.vault_dir(key).exists() {
            if epoch != 0 {
                return Ok(HotExitClaimOutcome::Superseded(
                    HotExitSupersession::Epoch { current: 0 },
                ));
            }
            if expected.is_some() {
                return Ok(HotExitClaimOutcome::Superseded(HotExitSupersession::Claim));
            }
        }
        let dir = match self.ensure_vault(key)? {
            Ok(dir) => dir,
            Err(refusal) => return Ok(HotExitClaimOutcome::Refused(refusal)),
        };
        let _guard = self.lock_vault(key)?;
        let current = self.read_epoch(&dir)?;
        if epoch != current {
            return Ok(HotExitClaimOutcome::Superseded(
                HotExitSupersession::Epoch { current },
            ));
        }
        let name = stem(entry_id);
        let claims_dir = dir.join(CLAIMS_DIR);
        // An older epoch's claim is no claim; a later epoch's is damage, as
        // in `read_buffers`, never treated as absent.
        let held = match self.read_claim(&dir, &name)? {
            Some(record) if record.epoch > current => return Err(HotExitError::Damaged),
            Some(record) if record.epoch == current => Some(record.incarnation),
            _ => None,
        };
        let entries_dir = dir.join(ENTRIES_DIR);
        let target = entries_dir.join(format!("{name}{ENTRY_EXT}"));
        let stored = self.read_entry(&target)?.map(|entry| entry.incarnation);
        let adopted = |stored: Option<String>| -> Vec<String> {
            let mut adopted: Vec<String> = expected.map(str::to_string).into_iter().collect();
            if let Some(stored) = stored
                && stored != incarnation
                && Some(stored.as_str()) != expected
            {
                adopted.push(stored);
            }
            adopted
        };
        let claimed = |adopted: Vec<String>| {
            HotExitClaimOutcome::Claimed(HotExitClaim {
                entry_id: entry_id.to_string(),
                incarnation: incarnation.to_string(),
                epoch,
                adopted,
            })
        };
        if held.as_deref() == Some(incarnation) {
            // A retry of a claim that reached the disk: vouch for it only
            // once it is durable, as `Newer` does for a payload (§2).
            self.fs.sync_file(&claims_dir.join(&name))?;
            self.fs.sync_dir(&claims_dir)?;
            return Ok(claimed(adopted(stored)));
        }
        if held.as_deref() != expected {
            return Ok(HotExitClaimOutcome::Superseded(HotExitSupersession::Claim));
        }
        if held.is_none() {
            // Only an older epoch's or a released claim can have left a
            // payload with no current claim: `read_buffers` would delete it,
            // and under this claim a crash before its first write would
            // restore it. Synced either way (the release's retry rule).
            if stored.is_some() {
                self.fs.remove(&target)?;
            }
            self.fs.sync_dir(&entries_dir)?;
        }
        let record = ClaimRecord {
            incarnation: incarnation.to_string(),
            epoch,
        };
        self.publish(
            &claims_dir,
            &name,
            &serde_json::to_vec(&record).map_err(json_error)?,
        )?;
        Ok(claimed(if held.is_some() {
            adopted(stored)
        } else {
            Vec::new()
        }))
    }

    /// Holds `key`'s store for this host across processes (WB-29 §7): an
    /// exclusive, non-blocking lock on its hold file. Another holder is
    /// `HeldElsewhere`. The store's other calls do not check it.
    pub fn hold_vault(&self, key: &HotExitVaultKey) -> Result<HotExitVaultHold, HotExitError> {
        let holds = self.root.join(HOLDS_DIR);
        std::fs::create_dir_all(&holds)?;
        let file = self.open_lock(&holds.join(format!(
            "{}{HOLD_EXT}",
            blake3::hash(key.text.as_bytes()).to_hex()
        )))?;
        match file.try_lock() {
            Ok(()) => Ok(HotExitVaultHold { _file: file }),
            Err(std::fs::TryLockError::WouldBlock) => Err(HotExitError::HeldElsewhere),
            Err(std::fs::TryLockError::Error(error)) => Err(error.into()),
        }
    }

    /// Publishes `entry` under `claim` at `generation` (§2).
    pub fn write_buffer(
        &self,
        key: &HotExitVaultKey,
        claim: &HotExitClaim,
        generation: u64,
        entry: &HotExitEntryWrite,
    ) -> Result<HotExitWriteOutcome, HotExitError> {
        validate_entry_id(&claim.entry_id)?;
        if entry.text.len() > MAX_ENTRY_TEXT_BYTES {
            return Ok(HotExitWriteOutcome::Refused(HotExitRefusal::EntryTooLarge));
        }
        let dir = self.vault_dir(key);
        if !dir.exists() {
            return Ok(HotExitWriteOutcome::Superseded(HotExitSupersession::Claim));
        }
        let entries_dir = dir.join(ENTRIES_DIR);
        let record = HotExitEntry {
            schema: SCHEMA,
            entry_id: claim.entry_id.clone(),
            incarnation: claim.incarnation.clone(),
            generation,
            path: entry.path.clone(),
            text: entry.text.clone(),
            baseline_hash: entry.baseline_hash.clone(),
            baseline_file_id: entry.baseline_file_id.clone(),
            written_at: timestamp((self.now)()),
        };
        let bytes = serde_json::to_vec(&record).map_err(json_error)?;

        // 1. Outside the lock: stage and sync the payload.
        let temp = entries_dir.join(format!(
            "{}{ENTRY_EXT}{TEMP_MARK}{}",
            stem(&claim.entry_id),
            fresh_incarnation()?
        ));
        let staged = self
            .fs
            .write_new(&temp, &bytes)
            .and_then(|()| self.fs.sync_file(&temp));
        if let Err(error) = staged {
            let _ = self.fs.remove(&temp);
            return Err(error.into());
        }

        // 2. Under the lock: check, then publish.
        let outcome = self.publish_staged(key, &dir, claim, generation, &temp, bytes.len() as u64);
        if !matches!(outcome, Ok(HotExitWriteOutcome::Durable { .. })) {
            let _ = self.fs.remove(&temp);
        }
        outcome
    }

    fn publish_staged(
        &self,
        key: &HotExitVaultKey,
        dir: &Path,
        claim: &HotExitClaim,
        generation: u64,
        temp: &Path,
        new_size: u64,
    ) -> Result<HotExitWriteOutcome, HotExitError> {
        let _guard = self.lock_vault(key)?;
        let current = self.read_epoch(dir)?;
        if claim.epoch != current {
            return Ok(HotExitWriteOutcome::Superseded(
                HotExitSupersession::Epoch { current },
            ));
        }
        let name = stem(&claim.entry_id);
        match self.read_claim(dir, &name)? {
            Some(record) if record.incarnation == claim.incarnation => {}
            _ => return Ok(HotExitWriteOutcome::Superseded(HotExitSupersession::Claim)),
        }
        let entries_dir = dir.join(ENTRIES_DIR);
        let target = entries_dir.join(format!("{name}{ENTRY_EXT}"));
        if let Some(stored) = self.read_entry(&target)?
            && stored.incarnation == claim.incarnation
            && stored.generation >= generation
        {
            // Vouch for the stored entry only once it is durable: a newer
            // write whose own durability step failed must not count (§2).
            self.fs.sync_file(&target)?;
            self.fs.sync_dir(&entries_dir)?;
            return Ok(HotExitWriteOutcome::Superseded(
                HotExitSupersession::Newer {
                    stored_generation: stored.generation,
                },
            ));
        }
        let (count, bytes) = self.vault_usage(&entries_dir, &name)?;
        if count + 1 > MAX_ENTRIES_PER_VAULT || bytes + new_size > MAX_BYTES_PER_VAULT {
            return Ok(HotExitWriteOutcome::Refused(HotExitRefusal::VaultFull));
        }
        self.fs.rename(temp, &target)?;
        self.fs.sync_dir(&entries_dir)?;
        Ok(HotExitWriteOutcome::Durable { generation })
    }

    /// Retires `claim`'s own claim and payload, and every incarnation it
    /// adopted, whatever has happened since; touches no other incarnation
    /// (§6).
    pub fn release_entry(
        &self,
        key: &HotExitVaultKey,
        claim: &HotExitClaim,
    ) -> Result<(), HotExitError> {
        let mut incarnations = vec![claim.incarnation.clone()];
        incarnations.extend(claim.adopted.iter().cloned());
        self.retire_entry(key, &claim.entry_id, &incarnations)
    }

    /// Removes `entry_id`'s claim and its payload where their incarnation
    /// is listed (WB-29 §7). It needs no claim and checks no epoch, so a
    /// host can retire a restored tab's inherited text whether or not its
    /// own claim ever landed; nothing of an unlisted incarnation is
    /// touched.
    pub fn retire_entry(
        &self,
        key: &HotExitVaultKey,
        entry_id: &str,
        incarnations: &[String],
    ) -> Result<(), HotExitError> {
        validate_entry_id(entry_id)?;
        let dir = self.vault_dir(key);
        if !dir.exists() {
            return Ok(());
        }
        let _guard = self.lock_vault(key)?;
        let listed = |incarnation: &str| incarnations.iter().any(|i| i == incarnation);
        let claims_dir = dir.join(CLAIMS_DIR);
        let name = stem(entry_id);
        if let Some(record) = self.read_claim(&dir, &name)?
            && listed(&record.incarnation)
        {
            self.fs.remove(&claims_dir.join(&name))?;
        }
        // Synced whether or not this call removed anything: a retry after a
        // failed sync finds the records already gone, and must still make
        // that removal durable before it reports the retirement done (§6).
        self.fs.sync_dir(&claims_dir)?;
        let entries_dir = dir.join(ENTRIES_DIR);
        let target = entries_dir.join(format!("{name}{ENTRY_EXT}"));
        if let Some(stored) = self.read_entry(&target)?
            && listed(&stored.incarnation)
        {
            self.fs.remove(&target)?;
        }
        self.fs.sync_dir(&entries_dir)?;
        Ok(())
    }

    /// Discards every entry of the vault: the epoch advances durably
    /// first, then every claim and entry goes (§6). Returns the new epoch.
    pub fn discard_vault(&self, key: &HotExitVaultKey) -> Result<u64, HotExitError> {
        // A vault with no directory yet still gets its fence: a first claim
        // issued at epoch 0 before this discard may be creating it now, and
        // must meet epoch 1 under the lock (§6). A vault the cap refuses has
        // nothing to discard, and no claim can create it either.
        let dir = match self.ensure_vault(key)? {
            Ok(dir) => dir,
            Err(_) => return Ok(0),
        };
        let _guard = self.lock_vault(key)?;
        let next = match self.read_epoch(&dir) {
            // Above every epoch a claim names too, should the counter have
            // gone backwards.
            // No successor past u64::MAX: damage, refused before any change.
            Ok(epoch) => epoch
                .max(self.highest_claim_epoch(&dir)?)
                .checked_add(1)
                .ok_or(HotExitError::Damaged)?,
            // A lost epoch is re-anchored above any epoch it can have held:
            // the highest a claim names, or the clock's milliseconds, which
            // exceed every counted epoch (§3, §6).
            Err(HotExitError::Damaged) => self.reanchored_epoch(&dir)?,
            Err(error) => return Err(error),
        };
        self.write_epoch(&dir, next)?;
        self.inject(HotExitStep::Remove)?;
        for sub in [CLAIMS_DIR, ENTRIES_DIR] {
            let sub_dir = dir.join(sub);
            // A temp file may be a live write's staging file; that write
            // meets the new epoch under the lock and removes it (§6), and a
            // dead one's is cleaned by a read once it is an hour old (§3).
            let names = match self.fs.list(&sub_dir) {
                Ok(names) => names,
                // A damaged vault missing a sub-directory gets it back.
                Err(error) if error.kind() == io::ErrorKind::NotFound => {
                    self.fs.create_dir(&sub_dir)?;
                    self.fs.sync_dir(&dir)?;
                    Vec::new()
                }
                Err(error) => return Err(error.into()),
            };
            for name in names {
                if !name.contains(TEMP_MARK) {
                    self.fs.remove(&sub_dir.join(name))?;
                }
            }
            self.fs.sync_dir(&sub_dir)?;
        }
        Ok(next)
    }

    /// The vault's live entries (§3): only entries whose claim is current
    /// in this epoch. Under the lock it deletes temp files over an hour old
    /// and entries a cut-short release or discard left without a current
    /// claim, and nothing else.
    pub fn read_buffers(&self, key: &HotExitVaultKey) -> Result<HotExitSnapshot, HotExitError> {
        let dir = self.vault_dir(key);
        if !dir.exists() {
            return Ok(HotExitSnapshot {
                epoch: 0,
                entries: Vec::new(),
                claims: Vec::new(),
            });
        }
        let _guard = self.lock_vault(key)?;
        let epoch = self.read_epoch(&dir)?;
        let claims_dir = dir.join(CLAIMS_DIR);
        let entries_dir = dir.join(ENTRIES_DIR);
        // Without the claims directory no entry can be judged current, and
        // nothing unread is deleted on a guess: the vault is damaged (§3).
        let claim_names = match self.fs.list(&claims_dir) {
            Ok(names) => names,
            Err(error) if error.kind() == io::ErrorKind::NotFound => {
                return Err(HotExitError::Damaged);
            }
            Err(error) => return Err(error.into()),
        };
        // A claim is only ever written at the vault's current epoch, so one
        // naming a later epoch proves the epoch itself damaged (a counter
        // gone backwards), not its entry stale: refuse before deleting
        // anything on that evidence (§3).
        for name in &claim_names {
            if let Some(record) = self.read_claim(&dir, name)?
                && record.epoch > epoch
            {
                return Err(HotExitError::Damaged);
            }
        }
        // Claims and the epoch are published under this lock, so a temp
        // file beside them is a dead publisher's, whatever its age.
        for (temps_dir, names) in [
            (&claims_dir, claim_names.clone()),
            (&dir, self.fs.list(&dir)?),
        ] {
            let mut removed = false;
            for name in names.iter().filter(|name| name.contains(TEMP_MARK)) {
                self.fs.remove(&temps_dir.join(name))?;
                removed = true;
            }
            if removed {
                self.fs.sync_dir(temps_dir)?;
            }
        }
        let now = (self.now)();
        let mut entries = Vec::new();
        let mut changed = false;
        for name in self.fs.list(&entries_dir)? {
            let path = entries_dir.join(&name);
            if name.contains(TEMP_MARK) {
                let old = self
                    .fs
                    .modified(&path)
                    .ok()
                    .and_then(|at| now.duration_since(at).ok())
                    .is_some_and(|age| age >= ORPHAN_TEMP_AGE);
                if old {
                    self.fs.remove(&path)?;
                    changed = true;
                }
                continue;
            }
            let Some(id) = name.strip_suffix(ENTRY_EXT) else {
                continue;
            };
            let Some(entry) = self.read_entry(&path)? else {
                continue;
            };
            match self.read_claim(&dir, id)? {
                Some(record) if record.epoch == epoch => entries.push(entry),
                // No claim (a release or discard cut short) or an older
                // epoch's: proven superseded. A newer epoch than the vault's
                // was refused above as damage.
                _ => {
                    self.fs.remove(&path)?;
                    changed = true;
                }
            }
        }
        if changed {
            self.fs.sync_dir(&entries_dir)?;
        }
        let mut stale_claims = false;
        let mut claims = Vec::new();
        for name in claim_names {
            match self.read_claim(&dir, &name)? {
                Some(record) if record.epoch < epoch => {
                    self.fs.remove(&claims_dir.join(&name))?;
                    stale_claims = true;
                }
                Some(record) => {
                    // A name this store never wrote names no entry id.
                    if let Some(entry_id) = unstem(&name) {
                        claims.push(HotExitCurrentClaim {
                            entry_id,
                            incarnation: record.incarnation,
                        });
                    }
                }
                None => {}
            }
        }
        if stale_claims {
            self.fs.sync_dir(&claims_dir)?;
        }
        claims.sort_by(|a, b| a.entry_id.cmp(&b.entry_id));
        entries.sort_by(|a, b| {
            a.written_at
                .cmp(&b.written_at)
                .then_with(|| a.entry_id.cmp(&b.entry_id))
        });
        Ok(HotExitSnapshot {
            epoch,
            entries,
            claims,
        })
    }

    // ---- internals ------------------------------------------------------

    fn vault_dir(&self, key: &HotExitVaultKey) -> PathBuf {
        self.root
            .join(VAULTS_DIR)
            .join(blake3::hash(key.text.as_bytes()).to_hex().as_str())
    }

    /// The vault's directory, created (with epoch 0, its sub-directories and
    /// lock file, each durable) on its first claim under the store lock,
    /// where the vault cap is checked (§1). A directory found already in
    /// place is made durable by this caller too: its creator may not yet
    /// have synced the parent after the rename, or that sync may have
    /// failed, and nothing may be acknowledged inside an unsynced name (§2).
    fn ensure_vault(
        &self,
        key: &HotExitVaultKey,
    ) -> Result<Result<PathBuf, HotExitRefusal>, HotExitError> {
        let dir = self.vault_dir(key);
        let vaults = self.root.join(VAULTS_DIR);
        if dir.exists() {
            self.fs.sync_dir(&vaults)?;
            return Ok(Ok(dir));
        }
        let _store = self.store_lock.lock().expect("hot-exit store mutex");
        let lock_file = self.open_lock(&self.root.join(STORE_LOCK_FILE))?;
        lock_file.lock()?;
        if dir.exists() {
            self.fs.sync_dir(&vaults)?;
            return Ok(Ok(dir));
        }
        let names = self.fs.list(&vaults)?;
        // Vault directories are only ever built under this lock, so a
        // staging directory here is a dead creator's (best effort).
        for name in names.iter().filter(|n| n.contains(TEMP_MARK)) {
            let _ = std::fs::remove_dir_all(vaults.join(name));
        }
        let count = names.iter().filter(|n| !n.contains(TEMP_MARK)).count();
        if count >= MAX_VAULTS {
            return Ok(Err(HotExitRefusal::TooManyVaults));
        }
        // Built under a temp name and renamed into place, so a vault
        // directory never exists without its epoch.
        let staging = vaults.join(format!(
            "{}{TEMP_MARK}{}",
            dir.file_name().and_then(|n| n.to_str()).unwrap_or_default(),
            fresh_incarnation()?
        ));
        self.inject(HotExitStep::CreateDir)?;
        self.fs.create_dir(&staging)?;
        for sub in [CLAIMS_DIR, ENTRIES_DIR] {
            self.fs.create_dir(&staging.join(sub))?;
        }
        self.fs.write_new(&staging.join(LOCK_FILE), b"")?;
        self.fs.write_new(&staging.join(EPOCH_FILE), b"0")?;
        self.fs.sync_file(&staging.join(EPOCH_FILE))?;
        self.fs.sync_dir(&staging)?;
        self.fs.rename(&staging, &dir)?;
        self.fs.sync_dir(&vaults)?;
        Ok(Ok(dir))
    }

    fn lock_vault(&self, key: &HotExitVaultKey) -> Result<VaultGuard, HotExitError> {
        let file = self.open_lock(&self.vault_dir(key).join(LOCK_FILE))?;
        file.lock()?;
        Ok(VaultGuard { _file: file })
    }

    fn open_lock(&self, path: &Path) -> Result<File, HotExitError> {
        Ok(std::fs::OpenOptions::new()
            .read(true)
            .write(true)
            .create(true)
            .truncate(false)
            .open(path)?)
    }

    fn read_epoch(&self, dir: &Path) -> Result<u64, HotExitError> {
        match self.fs.read(&dir.join(EPOCH_FILE)) {
            Ok(bytes) => std::str::from_utf8(&bytes)
                .ok()
                .and_then(|text| text.trim().parse().ok())
                .ok_or(HotExitError::Damaged),
            Err(error) if error.kind() == io::ErrorKind::NotFound => Err(HotExitError::Damaged),
            Err(error) => Err(error.into()),
        }
    }

    fn write_epoch(&self, dir: &Path, epoch: u64) -> Result<(), HotExitError> {
        self.publish(dir, EPOCH_FILE, epoch.to_string().as_bytes())
    }

    fn reanchored_epoch(&self, dir: &Path) -> Result<u64, HotExitError> {
        let highest = self.highest_claim_epoch(dir)?;
        let millis = (self.now)()
            .duration_since(SystemTime::UNIX_EPOCH)
            .map(|d| d.as_millis() as u64)
            .unwrap_or(0);
        Ok(highest
            .checked_add(1)
            .ok_or(HotExitError::Damaged)?
            .max(millis))
    }

    /// The highest epoch any readable claim names (0 with none).
    fn highest_claim_epoch(&self, dir: &Path) -> Result<u64, HotExitError> {
        let mut highest = 0u64;
        let names = match self.fs.list(&dir.join(CLAIMS_DIR)) {
            Ok(names) => names,
            Err(error) if error.kind() == io::ErrorKind::NotFound => Vec::new(),
            Err(error) => return Err(error.into()),
        };
        for name in names {
            // An unreadable claim names no epoch (a re-anchor still has the
            // clock to bound it).
            match self.read_claim(dir, &name) {
                Ok(Some(record)) => highest = highest.max(record.epoch),
                Ok(None) | Err(HotExitError::Damaged) => {}
                Err(error) => return Err(error),
            }
        }
        Ok(highest)
    }

    /// The claim file `name` (an entry's [`stem`], as listed).
    fn read_claim(&self, dir: &Path, name: &str) -> Result<Option<ClaimRecord>, HotExitError> {
        if name.contains(TEMP_MARK) {
            return Ok(None);
        }
        match self.fs.read(&dir.join(CLAIMS_DIR).join(name)) {
            // A claim that exists but does not parse fails closed, like a
            // lost epoch: reading it as absent would delete its entry unread.
            Ok(bytes) => serde_json::from_slice(&bytes)
                .map(Some)
                .map_err(|_| HotExitError::Damaged),
            Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(None),
            Err(error) => Err(error.into()),
        }
    }

    fn read_entry(&self, path: &Path) -> Result<Option<HotExitEntry>, HotExitError> {
        match self.fs.read(path) {
            Ok(bytes) => Ok(serde_json::from_slice(&bytes).ok()),
            Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(None),
            Err(error) => Err(error.into()),
        }
    }

    /// Entries and bytes the vault holds, not counting the writing entry's
    /// own file `own_stem` (a write replaces it).
    fn vault_usage(
        &self,
        entries_dir: &Path,
        own_stem: &str,
    ) -> Result<(usize, u64), HotExitError> {
        let own = format!("{own_stem}{ENTRY_EXT}");
        let mut count = 0;
        let mut bytes = 0;
        for name in self.fs.list(entries_dir)? {
            if name.contains(TEMP_MARK) || name == own {
                continue;
            }
            count += 1;
            bytes += self.fs.size(&entries_dir.join(&name))?;
        }
        Ok((count, bytes))
    }

    /// Writes `name` in `dir` durably: temp, sync, rename, directory sync.
    fn publish(&self, dir: &Path, name: &str, bytes: &[u8]) -> Result<(), HotExitError> {
        let temp = dir.join(format!("{name}{TEMP_MARK}{}", fresh_incarnation()?));
        let result = self
            .fs
            .write_new(&temp, bytes)
            .and_then(|()| self.fs.sync_file(&temp))
            .and_then(|()| self.fs.rename(&temp, &dir.join(name)))
            .and_then(|()| self.fs.sync_dir(dir));
        if result.is_err() {
            let _ = self.fs.remove(&temp);
        }
        Ok(result?)
    }

    /// A no-op outside facts: the seam fails a named step through
    /// [`HotExitFs`]; this hook lets a fact stop between two steps the
    /// store takes without I/O between them.
    fn inject(&self, step: HotExitStep) -> Result<(), HotExitError> {
        let _ = step;
        #[cfg(test)]
        if let Some(fail) = tests::INJECTED_STOP.with(|cell| *cell.borrow())
            && fail == step
        {
            return Err(HotExitError::Io {
                detail: format!("injected stop before {step:?}"),
            });
        }
        Ok(())
    }
}

fn validate_entry_id(entry_id: &str) -> Result<(), HotExitError> {
    let valid = !entry_id.is_empty()
        && entry_id.len() <= MAX_ENTRY_ID_LEN
        && entry_id
            .bytes()
            .all(|b| b.is_ascii_alphanumeric() || b == b'-' || b == b'_');
    if valid {
        Ok(())
    } else {
        Err(HotExitError::InvalidEntryId)
    }
}

/// The name an entry's claim and payload files carry: the id's bytes in
/// lower-case hex. Two valid ids may differ only by case (`TabA`, `taba`),
/// and NTFS and APFS compare names without case, so the id itself as a
/// name would let one tab's write replace another's unread text.
fn stem(entry_id: &str) -> String {
    entry_id.bytes().map(|b| format!("{b:02x}")).collect()
}

/// The entry id a [`stem`] names, or `None` for a name no valid id makes.
fn unstem(name: &str) -> Option<String> {
    if !name.len().is_multiple_of(2) {
        return None;
    }
    let bytes: Option<Vec<u8>> = (0..name.len())
        .step_by(2)
        .map(|i| u8::from_str_radix(name.get(i..i + 2)?, 16).ok())
        .collect();
    let id = String::from_utf8(bytes?).ok()?;
    validate_entry_id(&id).ok()?;
    (stem(&id) == name).then_some(id)
}

fn validate_incarnation(incarnation: &str) -> Result<(), HotExitError> {
    if incarnation.len() == 32
        && incarnation
            .bytes()
            .all(|b| b.is_ascii_digit() || (b'a'..=b'f').contains(&b))
    {
        Ok(())
    } else {
        Err(HotExitError::InvalidIncarnation)
    }
}

fn fresh_incarnation() -> Result<String, HotExitError> {
    let mut bytes = [0u8; 16];
    getrandom::fill(&mut bytes).map_err(|_| HotExitError::Io {
        detail: "no randomness".into(),
    })?;
    Ok(bytes.iter().map(|b| format!("{b:02x}")).collect())
}

fn timestamp(at: SystemTime) -> String {
    chrono::DateTime::<chrono::Utc>::from(at).to_rfc3339_opts(chrono::SecondsFormat::Millis, true)
}

fn json_error(error: serde_json::Error) -> HotExitError {
    HotExitError::Io {
        detail: format!("json: {}", error.classify() as u8),
    }
}
