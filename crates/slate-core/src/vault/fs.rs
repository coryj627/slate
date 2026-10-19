// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Desktop `VaultProvider` implementation backed by `std::fs`.
//!
//! Used on Mac and Windows. iOS and Android supply their own host-side
//! providers that route through their security-scoped resource APIs.
//!
//! See `super` (the `vault` module's `mod.rs`) for the cross-cutting
//! decisions: path safety rules, symlink handling, atomic-write
//! semantics, delete-via-trash policy.

use std::io::Write;
use std::path::{Component, Path, PathBuf};
use std::sync::{Arc, Mutex, PoisonError};
use std::time::{Instant, UNIX_EPOCH};
use std::{fs, io};

use crate::VaultError;

use super::echo_ledger::{EchoLedger, EchoState, ancestor_keys, ledger_key};
use super::provider::{DirEntry, EntryKind, FileEventSink, FileStat, VaultProvider, WatchHandle};
use super::trash_handoff::SystemTrash;

#[cfg(unix)]
mod pinned_root;

/// A vault provider backed by an on-disk directory.
#[derive(Debug)]
pub struct FsVaultProvider {
    root: PathBuf,
    /// Plan 43 PR 22a (#940): the root a filesystem session holds open for
    /// its lifetime. With it, every mutation resolves beneath the held root
    /// descriptor (renames, saves, creations, Move to Trash, `#1449`'s pin),
    /// saves and creates stage in the held `.slate/tmp`, `canonical_path`
    /// walks beneath it, and the watch is anchored to it. Without it (a
    /// provider built by path), each operation opens the root by path, as
    /// before. Reads still resolve by path either way.
    held: Option<Arc<super::fs_root::FsRoot>>,
    /// The system Trash call `delete` hands the pinned path to (#1450).
    trash: Arc<dyn SystemTrash>,
    /// What this provider's own mutations left behind, for the vault
    /// watcher's echo test (plan 43 PR 20a, #1267; `super::echo_ledger`).
    echo: Mutex<EchoLedger>,
}

/// A mutation destination whose containing directory is held open for the
/// lifetime of the operation. Unix callers publish relative to `parent`, so a
/// pathname ancestor replaced after this value is created cannot redirect the
/// final kernel operation through a symlink. Windows callers publish relative
/// to the pinned folder's handle the same way (#1451). Other platforms retain
/// the existing path-based behavior until they gain an equivalent API.
#[cfg(unix)]
#[derive(Debug)]
struct PinnedMutationTarget {
    parent: fs::File,
    leaf: std::ffi::CString,
}

/// Windows: the folder that will hold the leaf, pinned by the handle walk
/// beneath the vault root (`windows_entry`, #1451), with every folder above
/// it held open until the operation ends.
#[cfg(windows)]
#[derive(Debug)]
struct PinnedMutationTarget {
    parent: super::windows_entry::PinnedFolder,
    leaf: std::ffi::OsString,
}

#[cfg(not(any(unix, windows)))]
#[derive(Debug)]
struct PinnedMutationTarget {
    path: PathBuf,
}

/// The call a refused path stopped, for the end of its `InvalidPath` reason
/// (`pinned_root` and `windows_entry` word each one). On Unix a folder on the
/// path that is a symbolic link or not a folder refuses every mutation
/// (#1449, #1452); on Windows a junction, a symbolic link or a non-folder
/// does (#1451); elsewhere the value only travels with the path.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
#[cfg_attr(not(any(unix, windows)), allow(dead_code))]
pub(super) enum Mutation {
    /// A structural rename or move, and Move to Trash's inventory: "so
    /// nothing was moved".
    Move,
    /// A save, or any other overwrite of a file (`write_file`): "so nothing
    /// was saved".
    Save,
    /// A new file (`write_file_if_absent`): "so nothing was created".
    Create,
    /// A new folder (`create_dir`, `create_dir_if_absent`): "so no folder
    /// was created".
    CreateFolder,
}

/// Core's words for a refusal (#1452): the hosts show and speak them after
/// the path (`vault_error_detail`). The Unix walk (`pinned_root`) and the
/// Windows one (`windows_entry`, #1451) end every refusal with them.
#[cfg_attr(not(any(unix, windows)), allow(dead_code))]
impl Mutation {
    /// What did not happen: the end of every refusal's reason.
    pub(super) fn nothing_done(self) -> &'static str {
        match self {
            Self::Move => "so nothing was moved",
            Self::Save => "so nothing was saved",
            Self::Create => "so nothing was created",
            Self::CreateFolder => "so no folder was created",
        }
    }

    /// When a folder that had passed the walk changed under it (Unix: the
    /// Windows walk names the folder that changed instead).
    #[cfg_attr(not(unix), allow(dead_code))]
    pub(super) fn during(self) -> &'static str {
        match self {
            Self::Move => "during the move",
            Self::Save => "during the save",
            Self::Create => "while the file was being created",
            Self::CreateFolder => "while the folder was being created",
        }
    }
}

impl FsVaultProvider {
    /// Wrap an on-disk directory as a vault provider.
    ///
    /// The directory does not need to exist yet; subsequent operations
    /// will fail with `VaultError::Io` for non-existent paths. This is
    /// intentional — the provider doesn't enforce vault-existence
    /// semantics, the calling `VaultSession::open` does.
    ///
    /// `delete` hands entries to the system Trash. Tests are the exception
    /// (#1455): in core's own test build each provider gets a scratch bin of
    /// its own, and a host's test process routes every provider into its
    /// scratch bin (`route_trash_to_scratch_bin_for_tests`), so no test moves
    /// a fixture into the Trash of whoever runs it.
    pub fn new(root: PathBuf) -> Self {
        #[cfg(windows)]
        let root = windows_extended_path(&root);
        Self {
            root,
            held: None,
            trash: super::trash_handoff::default_trash(),
            echo: Mutex::new(EchoLedger::default()),
        }
    }

    /// The provider of a filesystem session (plan 43 PR 22a, #940): `root`
    /// is the spelling the session opened, and `held` the root it holds
    /// open, which every mutation, the staging folder, `canonical_path` and
    /// the watch resolve beneath.
    pub(crate) fn held(root: PathBuf, held: Arc<super::fs_root::FsRoot>) -> Self {
        Self {
            held: Some(held),
            ..Self::new(root)
        }
    }

    /// The vault root for one rename, save, creation or Trash inventory
    /// (#1449, #1452): the held root descriptor when the session holds one
    /// (`FsRoot` adopts `pinned_root`, design §7's sharing with option H),
    /// else the root opened by path for this operation.
    #[cfg(unix)]
    fn pinned_root(&self) -> Result<pinned_root::PinnedRoot, VaultError> {
        #[cfg(all(
            any(target_os = "macos", target_os = "linux"),
            target_pointer_width = "64"
        ))]
        if let Some(held) = &self.held {
            return pinned_root::PinnedRoot::adopt(held.descriptor());
        }
        pinned_root::PinnedRoot::open(&self.root)
    }

    /// Where saves and creates stage their bytes (#940): the held `.slate`'s
    /// `tmp`, made with `mkdirat` and opened without following a link, when
    /// the session holds the root; else `.slate/tmp` by path, made with
    /// `create_dir_all`, as before.
    fn staging(&self) -> io::Result<Staging> {
        #[cfg(all(
            any(target_os = "macos", target_os = "linux"),
            target_pointer_width = "64"
        ))]
        if let Some(held) = &self.held {
            let slate = held.cache_dir().descriptor();
            super::fs_root::sys::ensure_dir_at(slate, c"tmp")?;
            return Ok(Staging::Held(open_directory_at(slate, c"tmp")?));
        }
        let tmp_dir = self.tmp_dir();
        fs::create_dir_all(&tmp_dir)?;
        Ok(Staging::Path(tmp_dir))
    }

    /// A provider whose Trash handoff goes to `trash`: a `ScratchTrash` a
    /// fact inspects, or `PlatformTrash` for the gated real-Trash facts
    /// alone (`real_system_trash_provider`, #1455).
    #[cfg(test)]
    pub(crate) fn with_system_trash(root: PathBuf, trash: Arc<dyn SystemTrash>) -> Self {
        Self {
            trash,
            ..Self::new(root)
        }
    }

    /// Read the file at `relative` and return its canonical content
    /// hash (see [`content_hash`] for the contract). Convenience for
    /// the indexer; not part of the `VaultProvider` trait so it can
    /// stay desktop-specific.
    pub fn content_hash_for(&self, relative: &str) -> Result<String, VaultError> {
        let bytes = self.read_file(relative)?;
        Ok(content_hash(&bytes))
    }

    fn resolve(&self, relative: &str) -> Result<PathBuf, VaultError> {
        resolve_relative(&self.root, relative)
    }

    /// Plan 43 PR 20a (#1267; design §4.2): a completed mutation's post-state
    /// goes in the echo ledger, so the watcher's coalescer drops the events it
    /// caused. Every Slate write into the vault reaches one of the seven
    /// mutators that call this — saves, link rewrites, creates (imports and
    /// attachments too), folders, moves, undo and redo, Trash — so the ledger
    /// is complete by construction; a failed mutation records nothing.
    fn recorded(
        &self,
        relatives: &[&str],
        result: Result<(), VaultError>,
    ) -> Result<(), VaultError> {
        if result.is_ok() {
            self.record_echoes(relatives);
        }
        result
    }

    /// Observe and record `relatives` and every folder above them, right
    /// after the mutation. The folders are recorded because a mutation
    /// changes the folder that holds its entry (ReadDirectoryChangesW reports
    /// that folder's last-write change) and a create or a move can make
    /// missing folders. The stats run outside the ledger's lock.
    fn record_echoes(&self, relatives: &[&str]) {
        let mut keys: Vec<String> = Vec::new();
        for relative in relatives {
            let Some(key) = ledger_key(relative) else {
                continue;
            };
            for folder in ancestor_keys(&key) {
                if !keys.iter().any(|known| known == folder) {
                    keys.push(folder.to_string());
                }
            }
            if !keys.contains(&key) {
                keys.push(key);
            }
        }
        let observed: Vec<(String, Option<EchoState>)> = keys
            .into_iter()
            .map(|key| {
                let state = self.echo_state(&key);
                (key, state)
            })
            .collect();
        let mut ledger = self.echo.lock().unwrap_or_else(PoisonError::into_inner);
        let now = Instant::now();
        for (key, state) in observed {
            ledger.record(now, key, state);
        }
    }

    /// A path's state as the echo test compares it, through the same `stat`
    /// at record time and at test time. `None` when it cannot be observed
    /// (permission denied, an invalid spelling): such a path is never an
    /// echo.
    fn echo_state(&self, key: &str) -> Option<EchoState> {
        match self.stat(key) {
            Ok(stat) => Some(EchoState::Present {
                size_bytes: stat.size_bytes,
                mtime_ms: stat.mtime_ms,
                ctime_ms: stat.ctime_ms,
                kind: stat.kind,
            }),
            Err(VaultError::Io(error)) if error.kind() == io::ErrorKind::NotFound => {
                Some(EchoState::Absent)
            }
            Err(_) => None,
        }
    }

    /// Like `resolve`, but rejects paths that resolve to the vault root.
    ///
    /// Mutator entry points (`write_file`, `delete`, `rename`) use this
    /// so that `""` and `"."` cannot trash, overwrite, or move the
    /// vault directory itself. Read-side operations keep using `resolve`
    /// because listing/stating the root is well-defined.
    fn resolve_for_mutation(&self, relative: &str) -> Result<PathBuf, VaultError> {
        let path = self.resolve(relative)?;
        if path == self.root {
            return Err(VaultError::InvalidPath {
                path: relative.to_string(),
                reason: "operation would target the vault root".into(),
            });
        }
        Ok(path)
    }

    /// The folder that holds `relative`'s final component, held open, for a
    /// save, a creation or the Trash inventory. On Unix a folder on the way
    /// that is a link or a file is `mutation`'s typed refusal (#1452); on
    /// Windows a junction, a symbolic link or a file is (#1451).
    fn pin_mutation_target(
        &self,
        relative: &str,
        create_missing_parents: bool,
        mutation: Mutation,
    ) -> Result<PinnedMutationTarget, VaultError> {
        let path = self.resolve_for_mutation(relative)?;

        #[cfg(any(unix, windows))]
        {
            let vault_relative =
                path.strip_prefix(&self.root)
                    .map_err(|_| VaultError::InvalidPath {
                        path: relative.to_string(),
                        reason: "mutation destination is not beneath the vault root".into(),
                    })?;
            #[cfg(unix)]
            let root = self.pinned_root()?;
            #[cfg(windows)]
            let root = self.root.as_path();
            PinnedMutationTarget::open(
                &root,
                vault_relative,
                relative,
                create_missing_parents,
                mutation,
            )
        }

        #[cfg(not(any(unix, windows)))]
        {
            let _ = mutation;
            if create_missing_parents && let Some(parent) = path.parent() {
                fs::create_dir_all(parent).map_err(VaultError::Io)?;
            }
            Ok(PinnedMutationTarget { path })
        }
    }

    /// `path` (from `resolve_for_mutation`) relative to the vault root.
    #[cfg(any(unix, windows))]
    fn vault_relative<'p>(&self, path: &'p Path, original: &str) -> Result<&'p Path, VaultError> {
        path.strip_prefix(&self.root)
            .map_err(|_| VaultError::InvalidPath {
                path: original.to_string(),
                reason: "mutation path is not beneath the vault root".into(),
            })
    }

    /// #1449: the structural rename beneath one pinned root descriptor. Every
    /// caller that moves an existing entry (single and batch moves, undo and
    /// redo, compound and recovery replays) reaches it through
    /// [`VaultProvider::rename`]. No ancestor of either end is followed; a
    /// link or a non-folder among them refuses with `InvalidPath` and
    /// changes nothing (see `pinned_root`). `before_kernel_rename` is a test
    /// seam that runs after every check, just before the kernel call.
    #[cfg(unix)]
    fn rename_beneath_root(
        &self,
        from: &str,
        from_path: &Path,
        to: &str,
        to_path: &Path,
        before_kernel_rename: impl FnOnce() -> io::Result<()>,
    ) -> Result<(), VaultError> {
        let root = self.pinned_root()?;
        let missing_source = || {
            VaultError::Io(io::Error::new(
                io::ErrorKind::NotFound,
                format!("rename source does not exist: {from}"),
            ))
        };
        // Refuse early if the source is missing so we don't leave behind
        // freshly-minted destination parent directories on a failed move.
        // The probe inspects the entry itself inside its pinned parent, so a
        // broken symlink (a real entry `rename(2)` will happily move) is not
        // mistaken for a missing one, and other io errors propagate.
        let source = match root.end(self.vault_relative(from_path, from)?, from, false) {
            Ok(source) => source,
            Err(VaultError::Io(error)) if error.kind() == io::ErrorKind::NotFound => {
                return Err(missing_source());
            }
            Err(error) => return Err(error),
        };
        if !source.entry_exists().map_err(VaultError::Io)? {
            return Err(missing_source());
        }
        // Missing destination folders are made inside their pinned parents,
        // never through a link (the old `create_dir_all` followed one).
        let destination = root.end(self.vault_relative(to_path, to)?, to, true)?;
        // Structural moves are NO-CLOBBER (#871 Codex round 3): refuse if the
        // destination entry already exists, including a dangling symlink or a
        // file the index doesn't know yet. This is the fast early-out; the
        // atomic no-replace primitive below closes the check-to-rename race.
        // #1077 (contract I4): on an aliasing volume the destination may BE
        // the source under another spelling (the case-only rename
        // `A.md → a.md`); only then is the rename admissible.
        let destination_is_source = if destination.entry_exists().map_err(VaultError::Io)? {
            if !same_entry(from_path, to_path) {
                return Err(VaultError::DestinationExists {
                    path: to.to_string(),
                });
            }
            true
        } else {
            false
        };
        before_kernel_rename().map_err(VaultError::Io)?;
        match root.rename_no_replace(&source, &destination) {
            Ok(()) => Ok(()),
            // The no-replace primitive may report the source's own alias as
            // occupied (macOS `RENAME_EXCL`); with the same-entry proof above
            // this is the case change the filesystem applies to ONE entry,
            // still beneath the root (the residual race is IR-5, #1077).
            Err(error) if error.kind() == io::ErrorKind::AlreadyExists && destination_is_source => {
                root.rename_same_entry(&source, &destination)
                    .map_err(|error| root.rename_error(error, &source, &destination))
            }
            Err(error) if error.kind() == io::ErrorKind::AlreadyExists => {
                Err(VaultError::DestinationExists {
                    path: to.to_string(),
                })
            }
            Err(error) => Err(root.rename_error(error, &source, &destination)),
        }
    }

    /// #1451: the structural rename beneath the pinned vault root, on
    /// Windows, in the shape the identity inverse (`rename_if_identity`)
    /// already had, without its identity comparison. Every caller that moves
    /// an existing entry forward (single and batch moves and renames, the
    /// folder-note rename, batch rollback, the replay of a version-1 batch
    /// record) reaches it through [`VaultProvider::rename`].
    ///
    /// The root handle follows the root's own reparse point; every folder of
    /// either end beneath it is opened relative to the one before with
    /// `FILE_OPEN_REPARSE_POINT` (`windows_entry::PinnedRoot`), so a
    /// junction, a symbolic link or a file among them refuses with
    /// `InvalidPath` and changes nothing. The source is opened as itself (a
    /// link or a junction is moved, never followed) in its pinned folder; a
    /// missing destination folder is made in its pinned parent; and the
    /// rename is `FileRenameInformation` on the source handle, with the
    /// pinned destination folder as `RootDirectory` and `ReplaceIfExists`
    /// false, so neither end is ever resolved by path. `before_kernel_rename`
    /// is a test seam that runs after every check, just before that call.
    #[cfg(windows)]
    fn rename_beneath_root(
        &self,
        from: &str,
        from_path: &Path,
        to: &str,
        to_path: &Path,
        before_kernel_rename: impl FnOnce() -> io::Result<()>,
    ) -> Result<(), VaultError> {
        use super::windows_entry::{self, Folders, PinnedRoot};

        let root = PinnedRoot::open(&self.root)?;
        let from_relative = self.vault_relative(from_path, from)?;
        let to_relative = self.vault_relative(to_path, to)?;
        let missing_source = || {
            VaultError::Io(io::Error::new(
                io::ErrorKind::NotFound,
                format!("rename source does not exist: {from}"),
            ))
        };
        let from_leaf = final_component(from_relative, from)?;
        let to_leaf = final_component(to_relative, to)?;
        // Refuse early if the source is missing so we don't leave behind
        // freshly-minted destination parent directories on a failed move.
        // The source is opened as itself inside its pinned folder, so a
        // broken link (a real entry the rename will happily move) is not
        // mistaken for a missing one, and other io errors propagate.
        let source_parent = match root.folder(
            parent_of(from_relative),
            from,
            false,
            Mutation::Move,
            Folders::NotLinks,
        ) {
            Ok(parent) => parent,
            Err(VaultError::Io(error)) if error.kind() == io::ErrorKind::NotFound => {
                return Err(missing_source());
            }
            Err(error) => return Err(error),
        };
        let source = match windows_entry::open_for_rename(source_parent.handle(), from_leaf) {
            Ok(source) => source,
            Err(error) if error.kind() == io::ErrorKind::NotFound => {
                return Err(match source_parent.failed(error) {
                    refusal @ VaultError::InvalidPath { .. } => refusal,
                    _ => missing_source(),
                });
            }
            Err(error) => return Err(source_parent.failed(error)),
        };
        // Missing destination folders are made inside their pinned parents,
        // never through a link (the old `create_dir_all` followed one).
        let destination_parent = root.folder(
            parent_of(to_relative),
            to,
            true,
            Mutation::Move,
            Folders::NotLinks,
        )?;
        // Structural moves are NO-CLOBBER (#871 Codex round 3): refuse if the
        // destination entry already exists, including a dangling link or a
        // file the index doesn't know yet. This is the fast early-out; the
        // kernel's own no-replace rename below closes the check-to-rename
        // race. #1077 (contract I4): on an aliasing volume the destination
        // may BE the source under another spelling (the case-only rename
        // `A.md → a.md`); only then is the rename admissible.
        let destination_is_source =
            if windows_entry::entry_exists(destination_parent.handle(), to_leaf)
                .map_err(|error| destination_parent.failed(error))?
            {
                if !same_entry(from_path, to_path) {
                    return Err(VaultError::DestinationExists {
                        path: to.to_string(),
                    });
                }
                true
            } else {
                false
            };
        before_kernel_rename().map_err(VaultError::Io)?;
        let changed = |error: io::Error| match source_parent.failed(error) {
            refusal @ VaultError::InvalidPath { .. } => refusal,
            VaultError::Io(error) => destination_parent.failed(error),
            other => other,
        };
        match windows_entry::rename_into(&source, destination_parent.handle(), to_leaf, false) {
            Ok(()) => Ok(()),
            // NTFS renames a case-only alias of the source without
            // replacement (contract IR-5: Windows never takes this arm). A
            // volume that reports the source's own alias as occupied gets
            // the case change the filesystem applies to ONE entry, still on
            // the source handle and into the pinned folder.
            Err(error) if error.kind() == io::ErrorKind::AlreadyExists && destination_is_source => {
                windows_entry::rename_into(&source, destination_parent.handle(), to_leaf, true)
                    .map_err(changed)
            }
            Err(error) if error.kind() == io::ErrorKind::AlreadyExists => {
                Err(VaultError::DestinationExists {
                    path: to.to_string(),
                })
            }
            Err(error) => Err(changed(error)),
        }
    }

    /// The path-addressed rename for targets without a root-relative one.
    /// Unix renames beneath the pinned root descriptor (#1449) and Windows
    /// beneath the pinned root handle (#1451); a target that reaches this has
    /// no atomic no-replace primitive wired, so it fails closed.
    #[cfg(not(any(unix, windows)))]
    fn rename_by_path(
        &self,
        from: &str,
        from_path: &Path,
        to: &str,
        to_path: &Path,
    ) -> Result<(), VaultError> {
        // Refuse early if the source is missing so we don't leave behind
        // freshly-minted destination parent directories on a failed move.
        // `symlink_metadata` (lstat) checks the directory entry itself,
        // so a broken symlink — still a real entry that `rename(2)` will
        // happily move — is not mistakenly classified as NotFound, and
        // other io errors (permission denied, etc.) propagate untouched.
        match from_path.symlink_metadata() {
            Ok(_) => {}
            Err(e) if e.kind() == io::ErrorKind::NotFound => {
                return Err(VaultError::Io(io::Error::new(
                    io::ErrorKind::NotFound,
                    format!("rename source does not exist: {from}"),
                )));
            }
            Err(e) => return Err(VaultError::Io(e)),
        }
        // Structural moves are NO-CLOBBER (#871 Codex round 3): refuse if the
        // DESTINATION entry already exists. `std::fs::rename` REPLACES its
        // target, so without this a move/rename (or its undo replay) onto an
        // occupied path silently destroys the file there. `symlink_metadata`
        // (lstat) inspects the entry ITSELF — `exists()` follows symlinks and
        // reports a DANGLING symlink as absent, letting `rename` obliterate it;
        // it also catches a real file the SQLite index doesn't yet know about
        // (an external write between scans), which the index-level collision
        // check in `structural_move_file` misses. This is the fast early-out
        // (and the only guard on platforms without an atomic no-replace rename);
        // the atomic primitive below closes the check-to-rename TOCTOU race.
        let destination_is_source = match to_path.symlink_metadata() {
            // #1077 (contract I4): on an aliasing volume the destination
            // may BE the source under another spelling — the case-only
            // rename `A.md → a.md`. The same real entry is nothing to
            // clobber, so only then is the rename admissible.
            Ok(_) if same_entry(from_path, to_path) => true,
            Ok(_) => {
                return Err(VaultError::DestinationExists {
                    path: to.to_string(),
                });
            }
            Err(e) if e.kind() == io::ErrorKind::NotFound => false,
            Err(e) => return Err(VaultError::Io(e)),
        };
        if let Some(parent) = to_path.parent() {
            fs::create_dir_all(parent)?;
        }
        // Atomic no-replace (#871 Codex round 4): closes the window between the
        // lstat check above and the rename, where an external writer could have
        // created the destination — `fs::rename` would then destroy it. The
        // kernel primitive fails with `EEXIST` if the destination exists,
        // atomically, matching `atomic_create`'s `hard_link` no-replace publish.
        match rename_no_replace(from_path, to_path) {
            Ok(()) => Ok(()),
            // `AlreadyExists` is the portable mapping of the atomic primitive's
            // EEXIST (std maps it there) — referenced instead of `libc::EEXIST`
            // so this arm compiles on Windows, where `libc` is not a dependency
            // (#871 Codex round 5).
            // The no-replace primitive may report the source's own alias
            // as occupied; with the same-entry proof above, a plain rename is
            // the case change the filesystem applies to ONE entry — there is
            // nothing to replace (the residual race is contracts IR-5, #1077).
            Err(e) if e.kind() == io::ErrorKind::AlreadyExists && destination_is_source => {
                fs::rename(from_path, to_path).map_err(VaultError::Io)
            }
            Err(e) if e.kind() == io::ErrorKind::AlreadyExists => {
                Err(VaultError::DestinationExists {
                    path: to.to_string(),
                })
            }
            Err(e) => Err(VaultError::Io(e)),
        }
    }

    /// Directory where saves and creates stage their bytes (`stage_contents`,
    /// `write_file_if_absent_with_staging`). Lives under the vault's hidden
    /// `.slate/` subtree so a crash-leak doesn't surface as a visible entry
    /// and concurrent `list_dir` callers can't see half-written files
    /// alongside their targets. This is its pathname, for a provider that
    /// holds no root; a session's provider stages beneath the held `.slate`
    /// instead (`staging`, plan 43 PR 22a, #940).
    fn tmp_dir(&self) -> PathBuf {
        self.root.join(".slate").join("tmp")
    }

    fn require_mutation_parent_access(&self, path: &Path) -> Result<(), VaultError> {
        let parent = path.parent().ok_or_else(|| VaultError::InvalidPath {
            path: path.to_string_lossy().into_owned(),
            reason: "mutation path has no parent directory".into(),
        })?;
        let metadata = parent.metadata().map_err(VaultError::Io)?;
        if !metadata.is_dir() {
            return Err(VaultError::InvalidPath {
                path: parent.to_string_lossy().into_owned(),
                reason: "mutation parent is not a directory".into(),
            });
        }

        #[cfg(unix)]
        {
            use std::ffi::CString;
            let encoded = CString::new(parent.as_os_str().as_encoded_bytes()).map_err(|_| {
                VaultError::InvalidPath {
                    path: parent.to_string_lossy().into_owned(),
                    reason: "mutation parent contains an embedded NUL".into(),
                }
            })?;
            // Rename/delete require search plus write permission on the
            // containing directory. This is a current-process access check;
            // the eventual syscall remains authoritative against ACL/TOCTOU
            // changes.
            // SAFETY: `encoded` is a live NUL-terminated filesystem path.
            let allowed = unsafe { libc::access(encoded.as_ptr(), libc::W_OK | libc::X_OK) } == 0;
            if !allowed {
                return Err(VaultError::Io(io::Error::last_os_error()));
            }
        }

        #[cfg(windows)]
        if metadata.permissions().readonly() {
            return Err(VaultError::Io(io::Error::new(
                io::ErrorKind::PermissionDenied,
                "mutation parent is read-only",
            )));
        }

        Ok(())
    }

    fn write_file_if_absent_with_staging<WriteContents, SyncContents, Publish>(
        &self,
        relative: &str,
        contents: &[u8],
        write_contents: WriteContents,
        sync_contents: SyncContents,
        publish: Publish,
    ) -> Result<(), VaultError>
    where
        WriteContents: FnOnce(&mut fs::File, &[u8]) -> io::Result<()>,
        SyncContents: FnOnce(&fs::File) -> io::Result<()>,
        Publish: FnOnce(&Staged, &PinnedMutationTarget) -> io::Result<()>,
    {
        // Pin first, so a refused path stages nothing (#1452).
        let destination = self.pin_mutation_target(relative, true, Mutation::Create)?;
        let staging = self.staging().map_err(VaultError::Io)?;

        // A managed temp path is the failure guard: every exit before publish,
        // including partial writes and sync failures, still unlinks the staged
        // bytes. Keep the `create-` prefix so leaked-file diagnostics remain
        // recognizable if the filesystem itself refuses cleanup.
        let mut temp = staging.create_temp("create-").map_err(VaultError::Io)?;

        let staged =
            write_contents(temp.file_mut(), contents).and_then(|()| sync_contents(temp.file()));
        if let Err(stage_error) = staged {
            return match normalize_create_temp_cleanup(temp.close()) {
                Ok(()) => Err(VaultError::Io(stage_error)),
                Err(cleanup_error) => Err(VaultError::Io(io_error_with_cleanup(
                    stage_error,
                    cleanup_error,
                ))),
            };
        }

        // The shipping no-replace rename is the sole publish point. Success
        // consumes the staged directory entry, so there is no second unlink
        // whose failure could turn a committed create into a reported error.
        // Disarm `TempPath` cleanup explicitly: after the move, a new external
        // file could race into the old random name and must not be deleted by
        // the guard's destructor.
        match publish(&temp, &destination) {
            Ok(()) => {
                temp.published();
                Ok(())
            }
            Err(publish_error) => {
                let cleanup = normalize_create_temp_cleanup(temp.close());
                if publish_error.kind() == io::ErrorKind::AlreadyExists {
                    return match cleanup {
                        Ok(()) => Err(VaultError::DestinationExists {
                            path: relative.to_string(),
                        }),
                        Err(cleanup_error) => Err(VaultError::Io(io_error_with_cleanup(
                            publish_error,
                            cleanup_error,
                        ))),
                    };
                }
                match cleanup {
                    // On Windows a pinned folder converted into a junction
                    // after the walk is the typed refusal (#1451).
                    Ok(()) => Err(destination.failure(publish_error)),
                    Err(cleanup_error) => Err(VaultError::Io(io_error_with_cleanup(
                        publish_error,
                        cleanup_error,
                    ))),
                }
            }
        }
    }

    /// #1452: a save beneath the pinned vault root. Every overwrite core
    /// performs (saves, link rewrites, task toggles, property edits) reaches
    /// it through [`VaultProvider::write_file`]. The folder that will hold the
    /// file is pinned first with the no-follow walk, a missing one made with
    /// `mkdirat` inside its pinned parent; a link or a non-folder on the way
    /// refuses with `InvalidPath` before anything is staged, so the refusal is
    /// the provable no-write `save_text_locked` clears its intent for. The
    /// staged bytes are then renamed into that folder by descriptor, so the
    /// target's path is never resolved again (the staged file's own, in
    /// `.slate`, still is: #940). `before_publish` is a test seam that runs
    /// after the bytes are staged, just before the publish.
    #[cfg(unix)]
    fn write_file_beneath_root(
        &self,
        relative: &str,
        contents: &[u8],
        before_publish: impl FnOnce() -> io::Result<()>,
    ) -> Result<(), VaultError> {
        let target = self.pin_mutation_target(relative, true, Mutation::Save)?;
        let staged = self
            .staging()?
            .stage(contents, target.existing_permissions())?;
        before_publish()?;
        // A failed publish drops `staged`, which removes the staged file.
        target.publish_staged_replacing(&staged)?;
        // The staged name is consumed: disarm its cleanup, so a new file that
        // races into the old random name is never removed by the guard.
        staged.published();
        // Best effort, as before: the published bytes stand either way.
        let _ = target.sync_parent();
        Ok(())
    }

    /// #1452: a new folder beneath the pinned vault root, `create_folder`'s
    /// only route to disk ([`VaultProvider::create_dir`]). The folder that
    /// will hold it is pinned with the no-follow walk, and it is made with
    /// `mkdirat` in that folder's descriptor (`PinnedRoot::create_folder`).
    /// `before_create` is a test seam that runs after the pin, just before
    /// the `mkdirat`.
    #[cfg(unix)]
    fn create_dir_beneath_root(
        &self,
        relative: &str,
        before_create: impl FnOnce() -> io::Result<()>,
    ) -> Result<(), VaultError> {
        let path = self.resolve_for_mutation(relative)?;
        self.pinned_root()?.create_folder(
            self.vault_relative(&path, relative)?,
            relative,
            before_create,
        )
    }

    /// #1451: a save beneath the pinned vault root, on Windows. Every
    /// overwrite core performs (saves, link rewrites, task toggles, property
    /// edits) reaches it through [`VaultProvider::write_file`]. The folder
    /// that will hold the file is pinned first with the handle walk
    /// (`windows_entry`), a missing one made in its pinned parent; a
    /// junction, a symbolic link or a file on the way refuses with
    /// `InvalidPath` before anything is staged, so the refusal is the
    /// provable no-write `save_text_locked` clears its intent for. The staged
    /// bytes are then renamed into that folder by handle, replacing the
    /// target, so the target's path is never resolved again (the staged
    /// file's own, in `.slate`, still is: #940). `before_publish` is a test
    /// seam that runs after the bytes are staged, just before the publish.
    #[cfg(windows)]
    fn write_file_beneath_root(
        &self,
        relative: &str,
        contents: &[u8],
        before_publish: impl FnOnce() -> io::Result<()>,
    ) -> Result<(), VaultError> {
        let target = self.pin_mutation_target(relative, true, Mutation::Save)?;
        // No permissions are carried over: `persist` reset the attributes of
        // every Windows save to FILE_ATTRIBUTE_NORMAL, so the replaced file's
        // read-only bit never survived, and the publish does the same.
        let staged = self.staging()?.stage(contents, None)?;
        before_publish()?;
        // A failed publish drops `staged`, which removes the staged file.
        target
            .publish_staged_replacing(&staged)
            .map_err(|error| target.failure(error))?;
        // The staged name is consumed: disarm its cleanup, so a new file that
        // races into the old random name is never removed by the guard.
        staged.published();
        // Windows has no directory handle sync through std; NTFS metadata
        // journaling orders the rename with the synced file data, as before
        // (recorded in the protocol's accepted-risk register).
        Ok(())
    }

    /// #1451: a new folder beneath the pinned vault root, on Windows,
    /// `create_folder`'s only route to disk ([`VaultProvider::create_dir`]).
    /// The folder that will hold it is pinned with the handle walk, every
    /// missing one made in its pinned parent, and it is made in that
    /// folder's handle (`PinnedMutationTarget::create_folder`).
    /// `before_create` is a test seam that runs after the pin, just before
    /// the create.
    #[cfg(windows)]
    fn create_dir_beneath_root(
        &self,
        relative: &str,
        before_create: impl FnOnce() -> io::Result<()>,
    ) -> Result<(), VaultError> {
        let path = self.resolve_for_mutation(relative)?;
        let vault_relative = self.vault_relative(&path, relative)?;
        let target = PinnedMutationTarget::open(
            &self.root,
            vault_relative,
            relative,
            true,
            Mutation::CreateFolder,
        )?;
        before_create().map_err(VaultError::Io)?;
        target.create_folder(vault_relative, relative)
    }
}

#[cfg(unix)]
impl PinnedMutationTarget {
    /// Pin the folder that holds `vault_relative`'s final component with
    /// `pinned_root`'s no-follow walk, the one the structural rename takes
    /// (#1449): the vault root itself is followed, so a symlinked vault keeps
    /// working, and every folder beneath it is opened relative to the one
    /// before with `O_NOFOLLOW`. Since #1452 a link or a non-folder on the way
    /// is `mutation`'s typed refusal rather than the kernel's untyped
    /// `ENOTDIR`.
    fn open(
        root: &pinned_root::PinnedRoot,
        vault_relative: &Path,
        original: &str,
        create_missing_parents: bool,
        mutation: Mutation,
    ) -> Result<Self, VaultError> {
        let (parent, leaf) =
            root.parent_and_leaf(vault_relative, original, create_missing_parents, mutation)?;
        Ok(Self { parent, leaf })
    }

    fn create_dir_if_absent(&self) -> io::Result<()> {
        create_directory_at(&self.parent, &self.leaf)
    }

    /// A failed call in the pinned folder keeps the kernel's own error: a
    /// folder swapped after the pin is never followed (the call acts on the
    /// descriptor), so there is nothing to refuse after the fact.
    fn failure(&self, error: io::Error) -> VaultError {
        VaultError::Io(error)
    }

    fn publish_staged_no_replace(&self, staged: &Staged) -> io::Result<()> {
        use std::os::fd::AsRawFd as _;

        let (from_dir, from_name) = staged.source()?;

        // SAFETY: `from_dir` is `AT_FDCWD` or an open directory, both names
        // are NUL-terminated and outlive the call, and `parent` is an open
        // directory descriptor.
        #[cfg(target_os = "macos")]
        let result = unsafe {
            libc::renameatx_np(
                from_dir,
                from_name.as_ptr(),
                self.parent.as_raw_fd(),
                self.leaf.as_ptr(),
                libc::RENAME_EXCL,
            )
        };

        // SAFETY: as above.
        #[cfg(all(target_os = "linux", target_env = "gnu"))]
        let result = unsafe {
            libc::renameat2(
                from_dir,
                from_name.as_ptr(),
                self.parent.as_raw_fd(),
                self.leaf.as_ptr(),
                libc::RENAME_NOREPLACE,
            )
        };

        #[cfg(not(any(target_os = "macos", all(target_os = "linux", target_env = "gnu"))))]
        let result = unsafe {
            // These targets retain the previous fallback's replace semantics,
            // but the destination is still descriptor-relative and therefore
            // cannot be redirected by a swapped pathname ancestor.
            libc::renameat(
                from_dir,
                from_name.as_ptr(),
                self.parent.as_raw_fd(),
                self.leaf.as_ptr(),
            )
        };

        if result != 0 {
            return Err(io::Error::last_os_error());
        }
        Ok(())
    }

    /// A save's publish (#1452): the staged file renamed onto the leaf inside
    /// the pinned folder, replacing what is there, as `rename(2)` always has
    /// for a save. A leaf that is a symbolic link is replaced, never followed.
    ///
    /// For a session's provider the source is named beneath the held
    /// `.slate/tmp` descriptor (plan 43 PR 22a, #940), so neither end is
    /// resolved by path; a provider that holds no root names it by its path
    /// (`AT_FDCWD`), under the random name it was staged with a moment
    /// before. Either way the destination is the pinned folder.
    fn publish_staged_replacing(&self, staged: &Staged) -> io::Result<()> {
        use std::os::fd::AsRawFd as _;

        let (from_dir, from_name) = staged.source()?;
        // SAFETY: `from_dir` is `AT_FDCWD` or an open directory, both names
        // are NUL-terminated and outlive the call, and `parent` is an open
        // directory descriptor.
        let result = unsafe {
            libc::renameat(
                from_dir,
                from_name.as_ptr(),
                self.parent.as_raw_fd(),
                self.leaf.as_ptr(),
            )
        };
        if result != 0 {
            return Err(io::Error::last_os_error());
        }
        Ok(())
    }

    /// The permissions an overwrite keeps: the mode of what the save replaces,
    /// looked up in the pinned folder and following a final link, as
    /// `fs::metadata` did. `None` for a new file, or when the mode cannot be
    /// read: a best effort, as before.
    fn existing_permissions(&self) -> Option<fs::Permissions> {
        use std::os::fd::AsRawFd as _;
        use std::os::unix::fs::PermissionsExt as _;

        let mut stat = std::mem::MaybeUninit::<libc::stat>::uninit();
        // SAFETY: `parent` is an open directory, the leaf is NUL-terminated,
        // and `stat` has the size and alignment fstatat writes.
        let result = unsafe {
            libc::fstatat(
                self.parent.as_raw_fd(),
                self.leaf.as_ptr(),
                stat.as_mut_ptr(),
                0,
            )
        };
        if result != 0 {
            return None;
        }
        // SAFETY: a successful fstatat initialized `stat`.
        let mode = unsafe { stat.assume_init() }.st_mode;
        // `mode_t` is `u16` on macOS and `u32` on Linux.
        #[allow(clippy::useless_conversion)]
        let mode = u32::from(mode);
        Some(fs::Permissions::from_mode(mode))
    }

    /// Durable publication: sync the pinned folder's entries after the rename,
    /// so the marker-clearing index commit can never outlive the publication
    /// it vouches for (W4-3; contract 21). The pinned descriptor is opened for
    /// search only (`O_SEARCH`, or `O_PATH`, which cannot be synced), so the
    /// folder is reopened through it for reading. The save treats it as a
    /// best effort, as before: the published bytes stand either way.
    fn sync_parent(&self) -> io::Result<()> {
        use std::os::fd::{AsRawFd as _, FromRawFd as _};

        // SAFETY: `parent` is an open directory and "." is NUL-terminated.
        let descriptor = unsafe {
            libc::openat(
                self.parent.as_raw_fd(),
                c".".as_ptr(),
                libc::O_RDONLY | libc::O_DIRECTORY | libc::O_CLOEXEC,
            )
        };
        if descriptor < 0 {
            return Err(io::Error::last_os_error());
        }
        // SAFETY: a successful openat returned a new descriptor we now own.
        unsafe { fs::File::from_raw_fd(descriptor) }.sync_all()
    }
}

#[cfg(windows)]
impl PinnedMutationTarget {
    /// Pin the folder that holds `vault_relative`'s final component with the
    /// Windows handle walk (#1451): the vault root is followed, so a vault
    /// whose root is a junction keeps working, and every folder beneath it is
    /// opened relative to the one before with `FILE_OPEN_REPARSE_POINT`, a
    /// missing one made in its pinned parent. A junction, a symbolic link or
    /// a file on the way is `mutation`'s typed refusal.
    fn open(
        root: &Path,
        vault_relative: &Path,
        original: &str,
        create_missing_parents: bool,
        mutation: Mutation,
    ) -> Result<Self, VaultError> {
        use super::windows_entry::{Folders, PinnedRoot};

        let leaf = final_component(vault_relative, original)?.to_os_string();
        let parent = PinnedRoot::open(root)?.folder(
            parent_of(vault_relative),
            original,
            create_missing_parents,
            mutation,
            Folders::NotLinks,
        )?;
        Ok(Self { parent, leaf })
    }

    /// An exclusive new folder (`create_folder_exclusive`): `FILE_CREATE` in
    /// the pinned folder, so an entry of any kind already there is
    /// `AlreadyExists` and is never opened or followed.
    fn create_dir_if_absent(&self) -> io::Result<()> {
        super::windows_entry::create_folder_in(self.parent.handle(), &self.leaf).map(drop)
    }

    /// `create_dir` (#1451): the folder made in the pinned folder. A folder
    /// already there is `Ok`, looked at without being entered; a junction, a
    /// symbolic link or anything but a folder there is the typed refusal,
    /// naming it, and nothing is made through it.
    fn create_folder(&self, vault_relative: &Path, original: &str) -> Result<(), VaultError> {
        use super::windows_entry::{self, Folders};

        let Err(error) = windows_entry::create_folder_in(self.parent.handle(), &self.leaf) else {
            return Ok(());
        };
        // The leaf is already there: `FILE_CREATE` meets it as an
        // `AlreadyExists` collision for a plain name, but a leaf that is a
        // reparse point (a live junction to a folder, or a dangling one) can
        // instead surface as the kernel's reparse or not-found error, since
        // `FILE_CREATE` does not open the reparse point. So on any create
        // error, look at the leaf itself (no-follow); only a leaf that is
        // genuinely absent keeps the create's own error.
        match windows_entry::open_relative(
            self.parent.handle(),
            &self.leaf,
            windows_sys::Win32::Storage::FileSystem::FILE_READ_ATTRIBUTES,
        )
        .and_then(|entry| windows_entry::found(&entry))
        {
            // A folder already there is idempotent `Ok`; a junction, a
            // symbolic link or a non-folder is the typed refusal.
            Ok(found) => windows_entry::check(
                found,
                Folders::NotLinks,
                vault_relative,
                original,
                Mutation::CreateFolder,
            ),
            // Nothing to look at (gone, or unreadable): a pinned folder
            // converted after the walk is the typed refusal (`failure`
            // checks the chain), else the create's own error.
            Err(_) => Err(self.failure(error)),
        }
    }

    fn publish_staged_no_replace(&self, staged: &Staged) -> io::Result<()> {
        self.publish_staged(staged.path(), false)
    }

    /// A save's publish (#1451): the staged file renamed onto the leaf inside
    /// the pinned folder, replacing what is there, as `tempfile`'s `persist`
    /// (`MoveFileExW` with `MOVEFILE_REPLACE_EXISTING`) did before. A leaf
    /// that is a link is replaced, never followed.
    ///
    /// Windows can transiently refuse the rename with `ACCESS_DENIED` (os
    /// error 5) or `ERROR_SHARING_VIOLATION` (32) while an antivirus or
    /// indexer briefly holds the target or the fresh staged file — observed
    /// as a rare CI task-toggle write failure (W4-3 round-21 diagnostics:
    /// "Access is denied. (os error 5)" from an otherwise healthy toggle). So
    /// retry briefly with backoff; a persistent refusal is a real permissions
    /// problem and still surfaces.
    fn publish_staged_replacing(&self, staged: &Staged) -> io::Result<()> {
        let staged = staged.path();
        let mut delay = std::time::Duration::from_millis(5);
        let mut attempts = 0u32;
        loop {
            match self.publish_staged(staged, true) {
                Ok(()) => return Ok(()),
                Err(error) => {
                    let transient = matches!(error.raw_os_error(), Some(5) | Some(32));
                    attempts += 1;
                    if !transient || attempts >= 10 {
                        return Err(error);
                    }
                    std::thread::sleep(delay);
                    delay = (delay * 2).min(std::time::Duration::from_millis(100));
                }
            }
        }
    }

    /// Rename the staged file into the pinned folder by handle: the
    /// destination is `FileRenameInformation`'s `RootDirectory`, never a
    /// path. The staged file itself is still named by its path in
    /// `.slate/tmp`, under the random name `tempfile` gave it a moment
    /// before, and opened as itself (`FILE_OPEN_REPARSE_POINT`): a swap there
    /// can change which bytes arrive or fail the rename, never where they
    /// land. `.slate` is reached by path throughout, which is #940's
    /// descriptor-relative interior (PR 22's `FsRoot`), as on Unix.
    ///
    /// The staged file's attributes are reset to `FILE_ATTRIBUTE_NORMAL`
    /// first, as `persist` did: `tempfile` creates it
    /// `FILE_ATTRIBUTE_TEMPORARY`, which must not stay on a note.
    fn publish_staged(&self, staged: &Path, replace: bool) -> io::Result<()> {
        use std::os::windows::{fs::OpenOptionsExt as _, io::AsRawHandle as _};
        use windows_sys::Win32::Storage::FileSystem::{
            DELETE, FILE_ATTRIBUTE_NORMAL, FILE_BASIC_INFO, FILE_FLAG_OPEN_REPARSE_POINT,
            FILE_SHARE_DELETE, FILE_SHARE_READ, FILE_SHARE_WRITE, FILE_WRITE_ATTRIBUTES,
            FileBasicInfo, SetFileInformationByHandle,
        };

        let source = fs::OpenOptions::new()
            .access_mode(DELETE | FILE_WRITE_ATTRIBUTES)
            .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE)
            .custom_flags(FILE_FLAG_OPEN_REPARSE_POINT)
            .open(staged)?;
        // Zero times leave the file's times unchanged.
        let basic = FILE_BASIC_INFO {
            FileAttributes: FILE_ATTRIBUTE_NORMAL,
            ..Default::default()
        };
        // SAFETY: a live handle opened for FILE_WRITE_ATTRIBUTES and an
        // initialized FILE_BASIC_INFO of the size passed.
        if unsafe {
            SetFileInformationByHandle(
                source.as_raw_handle(),
                FileBasicInfo,
                (&basic as *const FILE_BASIC_INFO).cast(),
                std::mem::size_of::<FILE_BASIC_INFO>() as u32,
            )
        } == 0
        {
            return Err(io::Error::last_os_error());
        }
        super::windows_entry::rename_into(&source, self.parent.handle(), &self.leaf, replace)
    }

    /// A failed call in the pinned folder: the typed refusal when one of its
    /// folders changed into a junction or a link after the walk, else the
    /// IO error itself.
    fn failure(&self, error: io::Error) -> VaultError {
        self.parent.failed(error)
    }
}

#[cfg(not(any(unix, windows)))]
impl PinnedMutationTarget {
    fn create_dir_if_absent(&self) -> io::Result<()> {
        fs::create_dir(&self.path)
    }

    fn publish_staged_no_replace(&self, staged: &Staged) -> io::Result<()> {
        rename_no_replace(staged.path(), &self.path)
    }

    fn failure(&self, error: io::Error) -> VaultError {
        VaultError::Io(error)
    }
}

/// The name the volume stores for `name` in `dir`, without following a
/// link (`getattrlistat(ATTR_CMN_NAME, FSOPT_NOFOLLOW)`): on a case- or
/// normalization-insensitive volume, the spelling it keeps.
#[cfg(target_os = "macos")]
fn stored_name_at(dir: &fs::File, name: &std::ffi::CStr) -> io::Result<Vec<u8>> {
    use std::os::fd::AsRawFd as _;

    /// `getattrlist`'s reply for `ATTR_CMN_NAME`: its length, then the name's
    /// reference, then the bytes it points into.
    #[repr(C)]
    struct NameReply {
        length: u32,
        name: libc::attrreference_t,
        bytes: [u8; 1024],
    }

    // SAFETY: `attrlist` is plain integers; zero is its empty request.
    let mut request: libc::attrlist = unsafe { std::mem::zeroed() };
    request.bitmapcount = libc::ATTR_BIT_MAP_COUNT;
    request.commonattr = libc::ATTR_CMN_NAME;
    let mut reply = std::mem::MaybeUninit::<NameReply>::zeroed();
    // SAFETY: `dir` is an open directory, `name` is NUL-terminated, and
    // `request` and `reply` are live, writable and of the sizes passed.
    let result = unsafe {
        libc::getattrlistat(
            dir.as_raw_fd(),
            name.as_ptr(),
            (&mut request as *mut libc::attrlist).cast(),
            reply.as_mut_ptr().cast(),
            std::mem::size_of::<NameReply>(),
            libc::FSOPT_NOFOLLOW as libc::c_ulong,
        )
    };
    if result != 0 {
        return Err(io::Error::last_os_error());
    }
    // SAFETY: zero-initialized, then filled by a successful call.
    let reply = unsafe { reply.assume_init() };
    // The name's offset is relative to its `attrreference_t`; its length
    // counts the terminating NUL.
    let reference = std::mem::offset_of!(NameReply, name);
    let bytes_start = std::mem::offset_of!(NameReply, bytes);
    let start = (reference as i64 + i64::from(reply.name.attr_dataoffset)) - bytes_start as i64;
    let length = reply.name.attr_length as usize;
    let malformed = || io::Error::other("getattrlistat returned a malformed name");
    let start = usize::try_from(start).map_err(|_| malformed())?;
    let end = start
        .checked_add(length.checked_sub(1).ok_or_else(malformed)?)
        .filter(|end| *end <= reply.bytes.len())
        .ok_or_else(malformed)?;
    Ok(reply.bytes[start..end].to_vec())
}

#[cfg(unix)]
pub(super) fn open_directory_at(parent: &fs::File, name: &std::ffi::CStr) -> io::Result<fs::File> {
    use std::os::fd::{AsRawFd as _, FromRawFd as _};

    // SAFETY: `name` is NUL-terminated and `parent` remains open throughout
    // the call. A successful descriptor is uniquely owned by the returned
    // `File`.
    let descriptor = unsafe {
        libc::openat(
            parent.as_raw_fd(),
            name.as_ptr(),
            directory_open_flags(true),
        )
    };
    if descriptor == -1 {
        return Err(io::Error::last_os_error());
    }
    Ok(unsafe { fs::File::from_raw_fd(descriptor) })
}

#[cfg(target_os = "macos")]
pub(super) fn directory_open_flags(no_follow: bool) -> libc::c_int {
    libc::O_SEARCH | libc::O_CLOEXEC | if no_follow { libc::O_NOFOLLOW } else { 0 }
}

#[cfg(target_os = "linux")]
pub(super) fn directory_open_flags(no_follow: bool) -> libc::c_int {
    libc::O_PATH
        | libc::O_DIRECTORY
        | libc::O_CLOEXEC
        | if no_follow { libc::O_NOFOLLOW } else { 0 }
}

#[cfg(all(unix, not(any(target_os = "macos", target_os = "linux"))))]
pub(super) fn directory_open_flags(no_follow: bool) -> libc::c_int {
    libc::O_RDONLY
        | libc::O_DIRECTORY
        | libc::O_CLOEXEC
        | if no_follow { libc::O_NOFOLLOW } else { 0 }
}

#[cfg(unix)]
pub(super) fn create_directory_at(parent: &fs::File, name: &std::ffi::CStr) -> io::Result<()> {
    use std::os::fd::AsRawFd as _;

    // SAFETY: `name` is NUL-terminated and `parent` remains open throughout
    // the call. The process umask applies to the requested portable 0777 mode.
    let result = unsafe { libc::mkdirat(parent.as_raw_fd(), name.as_ptr(), 0o777) };
    if result != 0 {
        return Err(io::Error::last_os_error());
    }
    Ok(())
}

fn publish_staged_no_replace(
    staged: &Staged,
    destination: &PinnedMutationTarget,
) -> io::Result<()> {
    destination.publish_staged_no_replace(staged)
}

/// Where a save or a create stages its bytes (`FsVaultProvider::staging`).
enum Staging {
    /// `.slate/tmp` by path, staged with `tempfile`.
    Path(PathBuf),
    /// The held `.slate/tmp` (plan 43 PR 22a, #940).
    #[cfg(all(
        any(target_os = "macos", target_os = "linux"),
        target_pointer_width = "64"
    ))]
    Held(fs::File),
}

impl Staging {
    /// A new, empty staged file whose name starts with `prefix`.
    fn create_temp(&self, prefix: &str) -> io::Result<Staged> {
        match self {
            Self::Path(dir) => tempfile::Builder::new()
                .prefix(prefix)
                .tempfile_in(dir)
                .map(Staged::Path),
            #[cfg(all(
                any(target_os = "macos", target_os = "linux"),
                target_pointer_width = "64"
            ))]
            Self::Held(dir) => HeldStaged::create(dir, prefix).map(Staged::Held),
        }
    }

    /// A save's staged bytes, synced, carrying the permissions of what it
    /// replaces where there was one (see [`stage_contents`]).
    fn stage(
        &self,
        contents: &[u8],
        existing_perms: Option<fs::Permissions>,
    ) -> io::Result<Staged> {
        let mut staged = self.create_temp(".tmp")?;
        staged.file_mut().write_all(contents)?;
        staged.file().sync_data()?;
        if let Some(perms) = existing_perms {
            // Ignore failure: a successful overwrite with the temp's mode
            // is still better than aborting the write on a perms snag.
            let _ = staged.file().set_permissions(perms);
        }
        Ok(staged)
    }
}

/// A save's or a create's staged bytes, removed when dropped unless
/// [`Staged::published`] says the publish consumed the name.
enum Staged {
    Path(tempfile::NamedTempFile),
    #[cfg(all(
        any(target_os = "macos", target_os = "linux"),
        target_pointer_width = "64"
    ))]
    Held(HeldStaged),
}

impl Staged {
    fn file(&self) -> &fs::File {
        match self {
            Self::Path(temp) => temp.as_file(),
            #[cfg(all(
                any(target_os = "macos", target_os = "linux"),
                target_pointer_width = "64"
            ))]
            Self::Held(held) => &held.file,
        }
    }

    fn file_mut(&mut self) -> &mut fs::File {
        match self {
            Self::Path(temp) => temp.as_file_mut(),
            #[cfg(all(
                any(target_os = "macos", target_os = "linux"),
                target_pointer_width = "64"
            ))]
            Self::Held(held) => &mut held.file,
        }
    }

    /// A path-staged file's path (only Unix stages beneath a descriptor).
    #[cfg(not(unix))]
    fn path(&self) -> &Path {
        match self {
            Self::Path(temp) => temp.path(),
        }
    }

    /// A path-staged file's path, for the facts that reach its folder.
    #[cfg(all(unix, test))]
    fn staged_path(&self) -> Option<&Path> {
        match self {
            Self::Path(temp) => Some(temp.path()),
            #[cfg(all(
                any(target_os = "macos", target_os = "linux"),
                target_pointer_width = "64"
            ))]
            Self::Held(_) => None,
        }
    }

    /// The publish rename's source: the folder it is named in (`AT_FDCWD`
    /// for a path) and its name there.
    #[cfg(unix)]
    fn source(&self) -> io::Result<(libc::c_int, std::borrow::Cow<'_, std::ffi::CStr>)> {
        match self {
            Self::Path(temp) => {
                use std::os::unix::ffi::OsStrExt as _;
                let path = std::ffi::CString::new(temp.path().as_os_str().as_bytes())
                    .map_err(|error| io::Error::new(io::ErrorKind::InvalidInput, error))?;
                Ok((libc::AT_FDCWD, std::borrow::Cow::Owned(path)))
            }
            #[cfg(all(
                any(target_os = "macos", target_os = "linux"),
                target_pointer_width = "64"
            ))]
            Self::Held(held) => {
                use std::os::fd::AsRawFd as _;
                Ok((
                    held.dir.as_raw_fd(),
                    std::borrow::Cow::Borrowed(held.name.as_c_str()),
                ))
            }
        }
    }

    /// Remove the staged file now, reporting a failure (a create's cleanup).
    fn close(self) -> io::Result<()> {
        match self {
            Self::Path(temp) => temp.close(),
            #[cfg(all(
                any(target_os = "macos", target_os = "linux"),
                target_pointer_width = "64"
            ))]
            Self::Held(held) => held.close(),
        }
    }

    /// The publish consumed the staged name: never remove it, so a new file
    /// that races into the old random name is never removed by the guard.
    fn published(self) {
        match self {
            Self::Path(temp) => {
                let (file, mut path_guard) = temp.into_parts();
                path_guard.disable_cleanup(true);
                drop(path_guard);
                drop(file);
            }
            #[cfg(all(
                any(target_os = "macos", target_os = "linux"),
                target_pointer_width = "64"
            ))]
            Self::Held(mut held) => held.armed = false,
        }
    }
}

/// A file staged in the held `.slate/tmp` (plan 43 PR 22a, #940): made with
/// `openat(O_CREAT | O_EXCL | O_NOFOLLOW)` under a random name, as
/// `tempfile` names its files, and unlinked beneath the same descriptor.
#[cfg(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
))]
struct HeldStaged {
    dir: fs::File,
    name: std::ffi::CString,
    file: fs::File,
    armed: bool,
}

#[cfg(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
))]
impl HeldStaged {
    fn create(dir: &fs::File, prefix: &str) -> io::Result<Self> {
        static SEQUENCE: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(0);
        let dir = dir.try_clone()?;
        for _ in 0..64 {
            let sequence = SEQUENCE.fetch_add(1, std::sync::atomic::Ordering::Relaxed);
            let nanos = std::time::SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .map(|elapsed| elapsed.as_nanos())
                .unwrap_or_default();
            let seed = format!("{}:{nanos}:{sequence}", std::process::id());
            let suffix = &blake3::hash(seed.as_bytes()).to_hex()[..12];
            let name = std::ffi::CString::new(format!("{prefix}{suffix}"))
                .map_err(|error| io::Error::new(io::ErrorKind::InvalidInput, error))?;
            match super::fs_root::sys::open_at(
                &dir,
                &name,
                libc::O_WRONLY
                    | libc::O_CREAT
                    | libc::O_EXCL
                    | super::fs_root::sys::NO_FOLLOW
                    | libc::O_CLOEXEC,
                0o600,
            ) {
                Ok(file) => {
                    return Ok(Self {
                        dir,
                        name,
                        file,
                        armed: true,
                    });
                }
                Err(error) if error.kind() == io::ErrorKind::AlreadyExists => continue,
                Err(error) => return Err(error),
            }
        }
        Err(io::Error::new(
            io::ErrorKind::AlreadyExists,
            "could not name a staging file in .slate/tmp",
        ))
    }

    fn close(mut self) -> io::Result<()> {
        self.armed = false;
        super::fs_root::sys::unlink_at(&self.dir, &self.name)
    }
}

#[cfg(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
))]
impl Drop for HeldStaged {
    fn drop(&mut self) {
        if self.armed {
            let _ = super::fs_root::sys::unlink_at(&self.dir, &self.name);
        }
    }
}

fn normalize_create_temp_cleanup(result: io::Result<()>) -> io::Result<()> {
    match result {
        // If an external cleanup service removed the hidden temp first, the
        // desired postcondition already holds.
        Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(()),
        other => other,
    }
}

fn io_error_with_cleanup(primary: io::Error, cleanup: io::Error) -> io::Error {
    io::Error::new(
        primary.kind(),
        format!("{primary}; additionally failed to remove create staging file: {cleanup}"),
    )
}

/// Compute the canonical content hash of a byte slice.
///
/// **Contract** (relied on across modules — change this and audit
/// every call site):
/// - Algorithm: blake3.
/// - Output: lowercase hex, exactly 64 characters (256 bits).
/// - Deterministic and length-preserving across runs and platforms.
///
/// Used for the `files.content_hash` column, save-flow conflict
/// detection, and the op-log [`crate::oplog::body_checksum`] which
/// truncates the leading 8 hex chars to a `u32`. The hex-string
/// contract is what lets `body_checksum` skip a re-hash and just
/// parse nibbles directly.
pub fn content_hash(bytes: &[u8]) -> String {
    blake3::hash(bytes).to_hex().to_string()
}

/// Reject path spellings Win32 aliases or rewrites before they reach the
/// filesystem. Without this guard, names such as `CON.md` can address a device
/// and trailing dots/spaces can alias a different on-disk entry.
#[cfg(any(windows, test))]
fn validate_windows_relative_path(relative: &str) -> Result<(), &'static str> {
    for component in relative.split(['/', '\\']) {
        if component.is_empty() || component == "." || component == ".." {
            continue;
        }
        if component.ends_with(['.', ' ']) {
            return Err("Windows path components may not end with a dot or space");
        }
        if component.chars().any(|character| {
            character <= '\u{1f}' || matches!(character, '<' | '>' | ':' | '"' | '|' | '?' | '*')
        }) {
            return Err("Windows path component contains a reserved character");
        }

        let stem = component
            .split('.')
            .next()
            .unwrap_or_default()
            .trim_end_matches(['.', ' ']);
        let upper = stem.to_ascii_uppercase();
        let numbered_device = upper
            .strip_prefix("COM")
            .or_else(|| upper.strip_prefix("LPT"))
            .is_some_and(|number| {
                matches!(number, "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9")
            });
        if matches!(
            upper.as_str(),
            "CON" | "PRN" | "AUX" | "NUL" | "CONIN$" | "CONOUT$"
        ) || numbered_device
        {
            return Err("Windows reserved device names are not valid vault paths");
        }
    }

    Ok(())
}

/// Put an absolute Windows vault root into the verbatim namespace so every
/// descendant operation can cross the legacy `MAX_PATH` boundary. Vault paths
/// are validated before joining, which is important because verbatim paths
/// otherwise permit Win32 spellings such as trailing dots and spaces. A
/// SQLite database path long enough to need it takes the same form
/// (`db::sqlite_open_path`).
#[cfg(windows)]
pub(crate) fn windows_extended_path(path: &Path) -> PathBuf {
    use std::ffi::OsString;
    use std::os::windows::ffi::{OsStrExt, OsStringExt};

    if !path.is_absolute() {
        return path.to_owned();
    }

    let mut path_wide = path.as_os_str().encode_wide().collect::<Vec<_>>();
    const BACKSLASH: u16 = b'\\' as u16;
    const SLASH: u16 = b'/' as u16;
    const QUESTION: u16 = b'?' as u16;
    const DOT: u16 = b'.' as u16;

    if path_wide.starts_with(&[BACKSLASH, BACKSLASH, QUESTION, BACKSLASH])
        || path_wide.starts_with(&[BACKSLASH, BACKSLASH, DOT, BACKSLASH])
    {
        return path.to_owned();
    }

    for separator in &mut path_wide {
        if *separator == SLASH {
            *separator = BACKSLASH;
        }
    }

    let mut extended = "\\\\?\\".encode_utf16().collect::<Vec<_>>();
    if path_wide.starts_with(&[BACKSLASH, BACKSLASH]) {
        extended.extend("UNC\\".encode_utf16());
        extended.extend_from_slice(&path_wide[2..]);
    } else {
        extended.extend(path_wide);
    }

    PathBuf::from(OsString::from_wide(&extended))
}

/// Normalize a vault-relative path string against a root directory.
///
/// Returns `VaultError::InvalidPath` if the input is absolute, contains
/// any `..` component, or contains a Windows path prefix. The `.`
/// component is allowed and stripped (so `""`, `"."`, and `"./foo"` all
/// resolve cleanly).
fn resolve_relative(root: &Path, relative: &str) -> Result<PathBuf, VaultError> {
    let rel = Path::new(relative);
    if rel.is_absolute() {
        return Err(VaultError::InvalidPath {
            path: relative.to_string(),
            reason: "absolute paths are not allowed; vault-relative only".into(),
        });
    }

    let mut normalized = PathBuf::new();
    for component in rel.components() {
        match component {
            Component::Normal(s) => normalized.push(s),
            Component::CurDir => {}
            Component::ParentDir => {
                return Err(VaultError::InvalidPath {
                    path: relative.to_string(),
                    reason: "parent-directory references (..) are not allowed".into(),
                });
            }
            Component::RootDir | Component::Prefix(_) => {
                return Err(VaultError::InvalidPath {
                    path: relative.to_string(),
                    reason: "absolute paths and platform prefixes are not allowed".into(),
                });
            }
        }
    }

    #[cfg(windows)]
    validate_windows_relative_path(relative).map_err(|reason| VaultError::InvalidPath {
        path: relative.to_string(),
        reason: reason.into(),
    })?;

    Ok(root.join(normalized))
}

impl VaultProvider for FsVaultProvider {
    fn trash_snapshot(&self, relative: &str) -> Result<super::TrashSnapshot, VaultError> {
        self.trash_snapshot_cancellable(relative, &crate::CancelToken::new())
    }

    fn trash_snapshot_cancellable(
        &self,
        relative: &str,
        cancel: &crate::CancelToken,
    ) -> Result<super::TrashSnapshot, VaultError> {
        cancel.check()?;
        #[cfg(unix)]
        {
            let target = self.pin_mutation_target(relative, false, Mutation::Move)?;
            super::trash_snapshot::snapshot_at(&target.parent, &target.leaf, cancel)
        }
        #[cfg(not(unix))]
        {
            super::trash_snapshot::snapshot_cancellable(
                &self.root,
                &self.resolve_for_mutation(relative)?,
                cancel,
            )
        }
    }

    fn list_dir(&self, relative: &str) -> Result<Vec<DirEntry>, VaultError> {
        let path = self.resolve(relative)?;
        let read = fs::read_dir(&path)?;

        let mut entries: Vec<DirEntry> = Vec::new();
        for result in read {
            let entry = result?;
            // `file_type` is cheap on most platforms (uses the readdir
            // d_type field) and does NOT follow symlinks — exactly what
            // we want for reporting EntryKind::Symlink as itself.
            let file_type = entry.file_type()?;
            let kind = if file_type.is_dir() {
                EntryKind::Directory
            } else if file_type.is_symlink() {
                EntryKind::Symlink
            } else {
                EntryKind::File
            };

            let name = entry.file_name().to_string_lossy().into_owned();
            entries.push(DirEntry { name, kind });
        }

        // Stable, deterministic ordering for tests and UI predictability.
        entries.sort_by(|a, b| a.name.cmp(&b.name));
        Ok(entries)
    }

    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        let path = self.resolve(relative)?;
        Ok(fs::read(&path)?)
    }

    fn read_file_with_cap(&self, relative: &str, max_bytes: u64) -> Result<Vec<u8>, VaultError> {
        use std::io::Read;
        let path = self.resolve(relative)?;
        let file = fs::File::open(&path)?;
        // Cap allocation at `max_bytes + 1`. The +1 is the over-cap
        // sentinel: if `buf.len() > max_bytes` after the read, the
        // caller knows the file exceeded the threshold without us
        // ever materializing more than (max_bytes + 1) bytes in
        // memory, regardless of how large the file actually is.
        let cap = max_bytes.saturating_add(1);
        // `take` works in `u64` so cap fits even on 32-bit hosts.
        let mut handle = file.take(cap);
        let mut buf: Vec<u8> = Vec::with_capacity(cap.min(64 * 1024) as usize);
        handle.read_to_end(&mut buf)?;
        Ok(buf)
    }

    fn read_file_range_with_cap(
        &self,
        relative: &str,
        offset: u64,
        max_bytes: u64,
    ) -> Result<Vec<u8>, VaultError> {
        use std::io::{Read, Seek, SeekFrom};
        let path = self.resolve(relative)?;
        let mut file = fs::File::open(&path)?;
        file.seek(SeekFrom::Start(offset))?;
        let cap = max_bytes.saturating_add(1);
        let mut handle = file.take(cap);
        let mut buf: Vec<u8> = Vec::with_capacity(cap.min(64 * 1024) as usize);
        handle.read_to_end(&mut buf)?;
        Ok(buf)
    }

    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        // Beneath the pinned root, so a folder swapped for a link (#1452) or
        // a junction (#1451) refuses instead of redirecting the save.
        #[cfg(any(unix, windows))]
        let written = self.write_file_beneath_root(relative, contents, || Ok(()));
        #[cfg(not(any(unix, windows)))]
        let written = self.resolve_for_mutation(relative).and_then(|path| {
            atomic_write(&path, &self.tmp_dir(), contents).map_err(VaultError::from)
        });
        self.recorded(&[relative], written)
    }

    fn write_file_if_absent(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        let created = self.write_file_if_absent_with_staging(
            relative,
            contents,
            |file, contents| file.write_all(contents),
            fs::File::sync_data,
            publish_staged_no_replace,
        );
        self.recorded(&[relative], created)
    }

    /// Move an entry to the system Trash. The path handed to the Trash is
    /// derived from the pinned parent of a no-follow walk, never rebuilt
    /// lexically (#1450): a folder on the path that is a link or a file, or
    /// that moved out of the vault since the last check, refuses here with
    /// `InvalidPath` instead of redirecting the Trash to an object outside the
    /// vault. Nothing is mutated by a refusal. `trash_handoff` records what
    /// remains.
    fn delete(&self, relative: &str) -> Result<(), VaultError> {
        let trashed = self.trash_entry(relative);
        self.recorded(&[relative], trashed)
    }

    fn create_dir(&self, relative: &str) -> Result<(), VaultError> {
        // #1452: on Unix every missing folder is made with `mkdirat` inside
        // its pinned parent, and a link or a non-folder on the path, the
        // folder's own name included, is the typed refusal (`pinned_root`).
        // #1451: Windows makes each in its pinned parent's handle, and a
        // junction, a symbolic link or a non-folder is (`windows_entry`).
        #[cfg(any(unix, windows))]
        let created = self.create_dir_beneath_root(relative, || Ok(()));
        #[cfg(not(any(unix, windows)))]
        let created = self
            .resolve_for_mutation(relative)
            .and_then(|path| std::fs::create_dir_all(&path).map_err(VaultError::from));
        self.recorded(&[relative], created)
    }

    fn create_dir_if_absent(&self, relative: &str) -> Result<(), VaultError> {
        let created = self
            .pin_mutation_target(relative, false, Mutation::CreateFolder)
            .and_then(|destination| match destination.create_dir_if_absent() {
                Ok(()) => Ok(()),
                Err(error) if error.kind() == io::ErrorKind::AlreadyExists => {
                    Err(VaultError::DestinationExists {
                        path: relative.to_string(),
                    })
                }
                Err(error) => Err(destination.failure(error)),
            });
        self.recorded(&[relative], created)
    }

    #[cfg(windows)]
    fn mutation_identity(&self, relative: &str) -> Result<String, VaultError> {
        let path = self.resolve_for_mutation(relative)?;
        let relative = path
            .strip_prefix(&self.root)
            .map_err(|_| VaultError::InvalidArgument {
                message: "Mutation path is outside the vault".into(),
            })?;
        super::windows_entry::capture(&self.root, relative)
    }

    #[cfg(windows)]
    fn rename_if_identity(&self, from: &str, to: &str, expected: &str) -> Result<(), VaultError> {
        let renamed = self.rename_if_identity_unrecorded(from, to, expected);
        self.recorded(&[from, to], renamed)
    }

    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        let renamed = self.rename_unrecorded(from, to);
        self.recorded(&[from, to], renamed)
    }

    fn preflight_rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        let from_path = self.resolve_for_mutation(from)?;
        let to_path = self.resolve_for_mutation(to)?;
        // #1449: the same pinned walk the rename uses refuses a link or a
        // non-folder among either end's ancestors, so a batch is rejected
        // before its first item moves. The path-based probes below then
        // resolve through folders the walk has just seen as real.
        #[cfg(unix)]
        {
            let root = self.pinned_root()?;
            root.end(self.vault_relative(&from_path, from)?, from, false)?;
            root.end(self.vault_relative(&to_path, to)?, to, false)?;
        }
        // #1451: Windows walks its handles the same way, so a junction among
        // either end's folders rejects the batch before its first item moves.
        #[cfg(windows)]
        {
            use super::windows_entry::{Folders, PinnedRoot};
            let root = PinnedRoot::open(&self.root)?;
            for (path, original) in [(&from_path, from), (&to_path, to)] {
                let relative = self.vault_relative(path, original)?;
                root.folder(
                    parent_of(relative),
                    original,
                    false,
                    Mutation::Move,
                    Folders::NotLinks,
                )?;
            }
        }
        from_path.symlink_metadata().map_err(VaultError::Io)?;
        self.require_mutation_parent_access(&from_path)?;
        self.require_mutation_parent_access(&to_path)?;
        match to_path.symlink_metadata() {
            // #1077 (I4): the source's own alias is not an occupant.
            Ok(_) if same_entry(&from_path, &to_path) => Ok(()),
            Ok(_) => Err(VaultError::DestinationExists {
                path: to.to_string(),
            }),
            Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(()),
            Err(error) => Err(VaultError::Io(error)),
        }
    }

    fn preflight_delete(&self, relative: &str) -> Result<(), VaultError> {
        let path = self.resolve_for_mutation(relative)?;
        path.symlink_metadata().map_err(VaultError::Io)?;
        self.require_mutation_parent_access(&path)
    }

    fn mutation_path_kind(&self, relative: &str) -> Result<Option<EntryKind>, VaultError> {
        let path = self.resolve_for_mutation(relative)?;
        match path.symlink_metadata() {
            Ok(metadata) => Ok(Some(if metadata.file_type().is_symlink() {
                EntryKind::Symlink
            } else if metadata.is_dir() {
                EntryKind::Directory
            } else {
                EntryKind::File
            })),
            Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(None),
            Err(error) => Err(VaultError::Io(error)),
        }
    }

    fn mutation_path_exists(&self, relative: &str) -> Result<bool, VaultError> {
        self.mutation_path_kind(relative).map(|kind| kind.is_some())
    }

    /// #1077 contract I1: the stored spelling, from the filesystem.
    ///
    /// Windows and macOS are the aliasing platforms (case-insensitive
    /// NTFS/APFS; APFS also normalization-insensitive); everything else
    /// keeps the trait's identity default (divergence ID-1). macOS walks
    /// beneath the root descriptor (plan 43 PR 22a, below); on Windows, until
    /// 22W, the shape
    /// is the `read_in_vault_with_cap` precedent — `fs::canonicalize`
    /// on BOTH the entry and the root, so a symlinked vault root and an
    /// 8.3 short-name temp directory (the CI runner's `RUNNER~1`) strip
    /// cleanly — with two identity-specific rules:
    ///
    /// - **A link keeps its own spelling.** Identity never follows a
    ///   symlink or reparse point (the W1 anchored-vault discipline);
    ///   the link's own entry is the thing being mutated.
    /// - **Outside the root fails closed (I7).** An ancestor reparse
    ///   point that leads outside the vault leaves the entry with no
    ///   in-vault identity to bind to; refusing beats binding to a
    ///   spelling that is not this vault's.
    #[cfg(windows)]
    fn canonical_path(&self, relative: &str) -> Result<Option<String>, VaultError> {
        let path = self.resolve(relative)?;
        match path.symlink_metadata() {
            Ok(meta) if meta.file_type().is_symlink() => return Ok(Some(relative.to_string())),
            Ok(_) => {}
            Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(None),
            Err(error) => return Err(VaultError::Io(error)),
        }
        let canonical = match fs::canonicalize(&path) {
            Ok(canonical) => canonical,
            Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(None),
            Err(error) => return Err(VaultError::Io(error)),
        };
        let canonical_root = fs::canonicalize(&self.root)?;
        let Ok(rest) = canonical.strip_prefix(&canonical_root) else {
            return Err(VaultError::InvalidPath {
                path: relative.to_string(),
                reason: format!(
                    "canonical location {canonical:?} is not beneath the vault root \
                     {canonical_root:?} (an ancestor reparse point leads outside the \
                     vault); refusing to bind an identity"
                ),
            });
        };
        let mut spelling = String::new();
        for component in rest.components() {
            let part = component
                .as_os_str()
                .to_str()
                .ok_or_else(|| VaultError::InvalidPath {
                    path: relative.to_string(),
                    reason: "canonical path is not valid UTF-8".into(),
                })?;
            if !spelling.is_empty() {
                spelling.push('/');
            }
            spelling.push_str(part);
        }
        Ok(Some(spelling))
    }

    /// #1077 contract I1 on macOS, computed under the root descriptor (plan
    /// 43 PR 22a, #940; design §4.2): the stored spelling, one component at a
    /// time.
    ///
    /// The walk starts at the root the session holds open (a provider built
    /// by path opens the root once for the call) and descends one folder at
    /// a time with `O_NOFOLLOW`; each component's stored spelling is
    /// `getattrlistat(ATTR_CMN_NAME)` in its parent, which answers with the
    /// name the volume keeps (`notes` → `Notes`, an NFD spelling → the stored
    /// NFC one). "Beneath the vault" is the walk staying beneath the held
    /// descriptor, not a `strip_prefix` of two canonicalized pathnames that a
    /// swap of the root could move:
    ///
    /// - **A link keeps its own spelling.** Identity never follows a link
    ///   (the W1 anchored-vault discipline): a link at the end answers with
    ///   its own stored name.
    /// - **A folder on the way that is a link is not this vault's (I7).** The
    ///   walk never follows it, so nothing beneath the held root is at that
    ///   spelling: `None`, and the mutation that follows refuses the link in
    ///   its own words (#1449, #1452), as it does on Linux. Pathname
    ///   canonicalization followed such a link, binding an in-vault target's
    ///   spelling and refusing an outside one; the scanner never indexes
    ///   through a link either way.
    /// - A missing entry is `None`; a file where a folder should be is the
    ///   `ENOTDIR` it always was.
    #[cfg(target_os = "macos")]
    fn canonical_path(&self, relative: &str) -> Result<Option<String>, VaultError> {
        use std::os::unix::ffi::OsStrExt as _;

        let path = self.resolve(relative)?;
        let rest = path
            .strip_prefix(&self.root)
            .map_err(|_| VaultError::InvalidPath {
                path: relative.to_string(),
                reason: "the path is not beneath the vault root".into(),
            })?;
        let names: Vec<&std::ffi::OsStr> = rest
            .components()
            .filter_map(|component| match component {
                Component::Normal(name) => Some(name),
                _ => None,
            })
            .collect();
        let root = self.pinned_root()?;
        let mut dir = root.descriptor().try_clone().map_err(VaultError::Io)?;
        let mut spelling = String::new();
        for (index, name) in names.iter().enumerate() {
            let c_name =
                std::ffi::CString::new(name.as_bytes()).map_err(|_| VaultError::InvalidPath {
                    path: relative.to_string(),
                    reason: "the path contains an embedded NUL".into(),
                })?;
            let mode = match super::fs_root::sys::stat_at(&dir, &c_name, libc::AT_SYMLINK_NOFOLLOW)
            {
                Ok(stat) => stat.st_mode & libc::S_IFMT,
                Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(None),
                Err(error) => return Err(VaultError::Io(error)),
            };
            let stored = match stored_name_at(&dir, &c_name) {
                Ok(stored) => stored,
                Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(None),
                Err(error) => return Err(VaultError::Io(error)),
            };
            let stored = String::from_utf8(stored).map_err(|_| VaultError::InvalidPath {
                path: relative.to_string(),
                reason: "canonical path is not valid UTF-8".into(),
            })?;
            if !spelling.is_empty() {
                spelling.push('/');
            }
            spelling.push_str(&stored);
            if index + 1 == names.len() {
                break;
            }
            match mode {
                libc::S_IFDIR => {
                    dir = open_directory_at(&dir, &c_name).map_err(VaultError::Io)?;
                }
                libc::S_IFLNK => return Ok(None),
                _ => return Err(VaultError::Io(io::Error::from_raw_os_error(libc::ENOTDIR))),
            }
        }
        Ok(Some(spelling))
    }

    fn stat(&self, relative: &str) -> Result<FileStat, VaultError> {
        let path = self.resolve(relative)?;
        // `metadata()` follows symlinks; the indexer wants the target's
        // mtime/size when stating a symlink, so this matches the
        // documented semantics in `super::mod`.
        let meta = fs::metadata(&path)?;
        let kind = if meta.is_dir() {
            EntryKind::Directory
        } else if meta.file_type().is_symlink() {
            EntryKind::Symlink
        } else {
            EntryKind::File
        };
        let mtime_ms = meta
            .modified()
            .ok()
            .and_then(|t| t.duration_since(UNIX_EPOCH).ok())
            .map(|d| d.as_millis() as i64)
            .unwrap_or(0);
        Ok(FileStat {
            size_bytes: meta.len(),
            mtime_ms,
            ctime_ms: ctime_ms_of(&meta),
            birthtime_ms: birthtime_ms_of(&meta),
            kind,
        })
    }

    /// Plan 43 PR 20a (#1267): one recursive watch on the exact root this
    /// provider resolves against (on Windows its extended-length form),
    /// filtered by the scanner's hidden-path rule and anchored to the root's
    /// identity (`super::watch`).
    fn watch(&self, sink: Arc<dyn FileEventSink>) -> Result<Option<WatchHandle>, VaultError> {
        super::watch::watch_root(&self.root, self.held.as_ref(), sink)
    }

    /// Plan 43 PR 20a (design §4.2): `relative` is an echo when its current
    /// state equals the newest post-state this provider recorded for it
    /// within the ledger's 60 s.
    fn is_own_echo(&self, relative: &str) -> bool {
        let Some(key) = ledger_key(relative) else {
            return false;
        };
        let expected = {
            let ledger = self.echo.lock().unwrap_or_else(PoisonError::into_inner);
            match ledger.expected(Instant::now(), &key) {
                Some(state) => state.clone(),
                None => return false,
            }
        };
        self.echo_state(&key)
            .is_some_and(|current| current == expected)
    }

    fn read_in_vault_with_cap(
        &self,
        relative: &str,
        max_bytes: u64,
    ) -> Result<Vec<u8>, VaultError> {
        use std::io::Read;

        // The textual `resolve` already rejected `..` and absolute
        // paths. What remains is symlink escape: an entry under the
        // vault that points OUT (e.g. `Templates/Pwn.md` →
        // `/etc/passwd`). `fs::canonicalize` resolves the symlink
        // chain and returns the real on-disk location; we check
        // that location is under a canonicalized vault root.
        //
        // Both sides are canonicalized so a symlinked vault root
        // (rare but legal: `~/vaults/current → ~/vaults/2026-q2/`)
        // doesn't false-flag every entry inside as escaping.
        let resolved = self.resolve(relative)?;
        let canonical_target = fs::canonicalize(&resolved)?;
        let canonical_root = fs::canonicalize(&self.root)?;
        if !canonical_target.starts_with(&canonical_root) {
            return Err(VaultError::InvalidPath {
                path: relative.to_string(),
                reason: format!(
                    "canonical target {canonical_target:?} escapes the vault root {canonical_root:?} \
                     (likely a symlink pointing outside the vault); refusing to read for safety"
                ),
            });
        }

        // **TOCTOU defence — `O_NOFOLLOW` on the open.** Canonicalize
        // returns a string snapshot of where the symlinks pointed at
        // that instant. The subsequent `open` re-resolves that path
        // string through the kernel; if an attacker replaces the
        // canonical target's final component with a symlink between
        // the canonicalize and the open, a bare `File::open` would
        // happily follow the new symlink OUT of the vault (Codoki
        // PR #153 Critical).
        //
        // `O_NOFOLLOW` makes the open refuse symlinks on the final
        // path component — if anyone hot-swaps it in the race
        // window, we get `ELOOP` instead of an escape. The
        // canonical path's final component is by construction a
        // regular file (canonicalize resolved any symlinks), so
        // the flag has no effect on the legitimate path; it only
        // bites the racing attacker.
        //
        // An attacker who also hot-swaps a parent *directory* in
        // the canonical path can still race the open, but that
        // requires write access to a parent directory inside the
        // vault — the same privilege as just dropping a malicious
        // file directly. No meaningful escalation.
        //
        // Non-Unix builds fall back to the bare `File::open`; the
        // platforms slate ships to today (macOS, Linux) all
        // support `O_NOFOLLOW`. Windows would need an
        // `openat2`-style alternative when that target lands.
        let file = open_nofollow(&canonical_target)?;
        let cap = max_bytes.saturating_add(1);
        let mut handle = file.take(cap);
        let mut buf: Vec<u8> = Vec::with_capacity(cap.min(64 * 1024) as usize);
        handle.read_to_end(&mut buf)?;
        Ok(buf)
    }
}

impl FsVaultProvider {
    /// [`VaultProvider::delete`]'s body; the trait method records its echo.
    fn trash_entry(&self, relative: &str) -> Result<(), VaultError> {
        #[cfg(unix)]
        {
            let path = self.resolve_for_mutation(relative)?;
            // The structural rename's pinned walk (#1449): a link or a
            // non-folder among the item's ancestors is its typed refusal.
            let root = self.pinned_root()?;
            let item = root.end(self.vault_relative(&path, relative)?, relative, false)?;

            // Every entry goes to the Trash, and a symbolic link goes as the
            // link itself whether or not its target resolves, so Move to Trash
            // never deletes for good (#1495; NSFileManager and trash-rs both
            // move a dangling link as the link, and Put Back restores it). The
            // look does not follow the leaf, so a missing entry is NotFound
            // and never reaches the Trash, a broken symlink still counts as
            // present, and its io::Error keeps permission-denied (etc.) from
            // being masked as NotFound.
            item.require_present().map_err(VaultError::Io)?;

            let handed = super::trash_handoff::pinned_path(
                root.descriptor(),
                item.parent(),
                item.leaf(),
                relative,
            )?;
            self.trash.trash(&handed)
        }

        #[cfg(windows)]
        {
            let path = self.resolve_for_mutation(relative)?;
            let vault_relative =
                path.strip_prefix(&self.root)
                    .map_err(|_| VaultError::InvalidPath {
                        path: relative.to_string(),
                        reason: "mutation destination is not beneath the vault root".into(),
                    })?;
            let handed = super::trash_handoff::pinned_path(&self.root, vault_relative, relative)?;
            // `symlink_metadata` does not follow the leaf, so a broken symlink
            // still passes; the raw io::Error keeps its kind.
            let meta = handed.symlink_metadata().map_err(VaultError::Io)?;
            // Windows alone still unlinks a link whose target the system
            // reports absent: the Recycle Bin may reject one, and nobody has
            // checked (#1499; macOS and Linux trash it, #1495). A link whose
            // target Slate cannot open, a loop, or any other failure goes to
            // the Recycle Bin as the link (#1477).
            if meta.file_type().is_symlink()
                && fs::metadata(&handed)
                    .is_err_and(|error| super::trash_handoff::link_target_is_absent(&error))
            {
                return unlink_dangling_symlink(&handed, &meta).map_err(VaultError::Io);
            }
            self.trash.trash(&handed)
        }

        #[cfg(not(any(unix, windows)))]
        {
            self.resolve_for_mutation(relative)?;
            Err(VaultError::Trash {
                message: "Moving items to the Trash is unavailable on this platform".into(),
            })
        }
    }

    #[cfg(windows)]
    fn rename_if_identity_unrecorded(
        &self,
        from: &str,
        to: &str,
        expected: &str,
    ) -> Result<(), VaultError> {
        let from = self.resolve_for_mutation(from)?;
        let to = self.resolve_for_mutation(to)?;
        let relative = |path: &std::path::Path| {
            path.strip_prefix(&self.root)
                .map(std::path::Path::to_path_buf)
                .map_err(|_| VaultError::InvalidArgument {
                    message: "Mutation path is outside the vault".into(),
                })
        };
        super::windows_entry::rename(&self.root, &relative(&from)?, &relative(&to)?, expected)
    }

    /// [`VaultProvider::rename`]'s body; the trait method records its echo.
    fn rename_unrecorded(&self, from: &str, to: &str) -> Result<(), VaultError> {
        let from_path = self.resolve_for_mutation(from)?;
        let to_path = self.resolve_for_mutation(to)?;
        #[cfg(any(unix, windows))]
        {
            self.rename_beneath_root(from, &from_path, to, &to_path, || Ok(()))
        }
        #[cfg(not(any(unix, windows)))]
        {
            self.rename_by_path(from, &from_path, to, &to_path)
        }
    }
}

/// #1077 (contract I4): are two mutation paths ONE real filesystem entry?
/// True only when neither leaf is a symlink (a link onto its own target is
/// never "the same entry" — replacing the target with the link would lose
/// the file) and both resolve to the same canonical location: the
/// case-only or normalization-only alias an NTFS/APFS volume keeps for one
/// entry. A path that cannot be inspected is never the same entry.
fn same_entry(a: &Path, b: &Path) -> bool {
    let leaf_is_link = |path: &Path| {
        path.symlink_metadata()
            .map(|meta| meta.file_type().is_symlink())
            .unwrap_or(true)
    };
    if leaf_is_link(a) || leaf_is_link(b) {
        return false;
    }
    matches!(
        (fs::canonicalize(a), fs::canonicalize(b)),
        (Ok(x), Ok(y)) if x == y
    )
}

/// Read-only `File::open` that refuses to follow a symlink on the
/// final path component. On Unix this is `O_NOFOLLOW` via the std
/// `custom_flags` extension; on other targets it's a plain open
/// (the host platforms slate ships to are Unix-likes, so this is
/// the operative path).
#[cfg(unix)]
fn open_nofollow(path: &Path) -> io::Result<fs::File> {
    use std::os::unix::fs::OpenOptionsExt;
    fs::OpenOptions::new()
        .read(true)
        .custom_flags(libc::O_NOFOLLOW)
        .open(path)
}

#[cfg(not(unix))]
fn open_nofollow(path: &Path) -> io::Result<fs::File> {
    fs::File::open(path)
}

/// Remove a dangling symlink entry on Windows. (Unix unlinks it relative to
/// its pinned parent: `pinned_root::RenameEnd::unlink`.)
///
/// Symlinks carry a type flag at creation time. A symlink declared as a
/// directory symlink must be removed with `RemoveDirectory`
/// (`fs::remove_dir`); calling `DeleteFile` (`fs::remove_file`) on it
/// fails with `ERROR_ACCESS_DENIED`. The flag is exposed via
/// `FileTypeExt::is_symlink_dir`.
#[cfg(windows)]
fn unlink_dangling_symlink(path: &Path, meta: &fs::Metadata) -> io::Result<()> {
    use std::os::windows::fs::FileTypeExt;
    if meta.file_type().is_symlink_dir() {
        fs::remove_dir(path)
    } else {
        fs::remove_file(path)
    }
}

/// The vault-relative folder that holds `relative`'s final component (`""`
/// for the root).
#[cfg(windows)]
fn parent_of(relative: &Path) -> &Path {
    relative.parent().unwrap_or_else(|| Path::new(""))
}

/// `relative`'s final component, which every mutation names.
#[cfg(windows)]
fn final_component<'p>(
    relative: &'p Path,
    original: &str,
) -> Result<&'p std::ffi::OsStr, VaultError> {
    relative.file_name().ok_or_else(|| VaultError::InvalidPath {
        path: original.to_string(),
        reason: "the path has no final component".into(),
    })
}

/// Atomically rename `from` → `to` by path, failing with `EEXIST` if `to`
/// already exists (#871 Codex round 4), on targets without a root-relative
/// rename. None is wired, so it fails closed instead of silently falling back
/// to a replacing `fs::rename`, which could replace a destination created
/// after the provider's lstat pre-check.
///
/// Nothing renames by path any more: macOS and Linux rename beneath the
/// pinned vault root descriptor (`pinned_root`, #1449), and Windows beneath
/// the pinned root handle (`windows_entry`, #1451), because a path-addressed
/// rename (`MoveFileExW` on Windows) follows an ancestor swapped for a link
/// or a junction across the vault boundary.
#[cfg(not(any(unix, windows)))]
fn rename_no_replace(_from: &Path, _to: &Path) -> io::Result<()> {
    Err(io::Error::new(
        io::ErrorKind::Unsupported,
        "atomic no-clobber rename is unsupported on this target",
    ))
}

/// Extract inode change time (ctime) as Unix epoch milliseconds.
///
/// Unix exposes ctime via `MetadataExt`. On other platforms (Windows,
/// WASI) `std::fs::Metadata` has no portable ctime accessor, so we
/// return `0` and the scanner's fast-path falls back to mtime+size
/// only. ctime catches mtime-preserving writes (`cp -p`, `rsync -a`,
/// snapshot restore) that mtime alone would miss.
#[cfg(unix)]
fn ctime_ms_of(meta: &fs::Metadata) -> i64 {
    use std::os::unix::fs::MetadataExt;
    meta.ctime()
        .saturating_mul(1_000)
        .saturating_add(meta.ctime_nsec() / 1_000_000)
}

#[cfg(not(unix))]
fn ctime_ms_of(_meta: &fs::Metadata) -> i64 {
    0
}

/// File birth time in epoch ms, `0` where unavailable (the ctime
/// convention). `std::fs::Metadata::created()` maps to `st_birthtime`
/// on macOS/APFS and creation time on Windows; filesystems without a
/// birth time surface an error → `0`.
fn birthtime_ms_of(meta: &fs::Metadata) -> i64 {
    meta.created()
        .ok()
        .and_then(|t| t.duration_since(std::time::UNIX_EPOCH).ok())
        .map(|d| d.as_millis() as i64)
        .unwrap_or(0)
}

/// Stage `contents` for an atomic replace: a new temp file under `tmp_dir`,
/// written, flushed and synced, carrying `existing_perms` when the save
/// replaces a file. The caller renames it into place.
///
/// `tmp_dir` must live on the same filesystem as the target so the final
/// rename is atomic — the caller passes `<vault_root>/.slate/tmp/` for
/// that reason. Parking temps under `.slate/` (a) keeps the partial
/// state invisible to `list_dir` callers reading the target's directory
/// during the write, and (b) means a crash-leak doesn't surface as a
/// visible vault entry.
///
/// If the target already exists, its filesystem permissions are copied to
/// the temp before rename so a 0644 file isn't silently downgraded to
/// the temp's stricter default. New files keep the temp's permissions.
#[cfg(not(any(unix, windows)))]
fn stage_contents(
    tmp_dir: &Path,
    contents: &[u8],
    existing_perms: Option<fs::Permissions>,
) -> io::Result<tempfile::NamedTempFile> {
    let mut temp = tempfile::NamedTempFile::new_in(tmp_dir)?;
    temp.write_all(contents)?;
    temp.flush()?;
    temp.as_file().sync_data()?;
    if let Some(perms) = existing_perms {
        // Ignore failure: a successful overwrite with the temp's mode
        // is still better than aborting the write on a perms snag.
        let _ = temp.as_file().set_permissions(perms);
    }
    Ok(temp)
}

/// Atomically replace the contents of `target` by path: the save where the
/// provider has no descriptor- or handle-relative one (Unix saves beneath the
/// pinned root descriptor, #1452, and Windows beneath the pinned root handle,
/// #1451: `write_file_beneath_root`). The parent is made with
/// `create_dir_all` and the staged file renamed onto the path, so a folder
/// on it swapped for a link would redirect the save.
///
/// Note on symlinks: if `target` is a symlink, the rename replaces the
/// symlink directory entry with the regular temp file. `write_file`
/// does not transparently follow symlinks to write through to their
/// target — see the vault module docs.
#[cfg(not(any(unix, windows)))]
fn atomic_write(target: &Path, tmp_dir: &Path, contents: &[u8]) -> io::Result<()> {
    let parent = target.parent().ok_or_else(|| {
        io::Error::new(
            io::ErrorKind::InvalidInput,
            "target path has no parent directory",
        )
    })?;
    fs::create_dir_all(parent)?;
    fs::create_dir_all(tmp_dir)?;

    // Best-effort permission preservation. We don't fail the write if
    // we can't read the existing mode — the rename still produces a
    // valid file, just with the temp's default permissions.
    let existing_perms = fs::metadata(target).ok().map(|m| m.permissions());

    let temp = stage_contents(tmp_dir, contents, existing_perms)?;
    temp.persist(target).map_err(|error| error.error)?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    fn vault() -> (tempfile::TempDir, FsVaultProvider) {
        let tmp = tempfile::tempdir().expect("tempdir");
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        (tmp, provider)
    }

    /// Direct test that `open_nofollow` refuses a symlink at the
    /// final path component on Unix. This is what closes the
    /// post-canonicalize TOCTOU window in `read_in_vault_with_cap`:
    /// if an attacker hot-swaps the canonical target's final
    /// component to a symlink between our `canonicalize` and the
    /// `File::open`, the `O_NOFOLLOW` flag turns the open into
    /// `ELOOP` instead of an escape (Codoki PR #153 Critical).
    ///
    /// The race itself isn't reproducible without injecting a
    /// delay; what this test locks in is the kernel-level
    /// contract: open_nofollow on a symlink path fails with
    /// ELOOP. The provider's read path uses this helper, so the
    /// defence is in place wherever the helper is invoked.
    #[cfg(unix)]
    #[test]
    fn open_nofollow_refuses_a_symlinked_final_component_with_eloop() {
        let tmp = tempfile::tempdir().unwrap();
        let real = tmp.path().join("real.md");
        std::fs::write(&real, b"real contents").unwrap();
        let link = tmp.path().join("sym.md");
        std::os::unix::fs::symlink(&real, &link).unwrap();

        // Sanity check: a regular `File::open` on the symlink
        // successfully follows to `real.md`.
        let bare = std::fs::read(&link).unwrap();
        assert_eq!(bare, b"real contents");

        // `open_nofollow` should refuse to follow that final-
        // component symlink. Map the io error to its raw_os_error
        // so the assertion is portable across libc versions.
        let err = open_nofollow(&link).expect_err("expected ELOOP");
        assert_eq!(
            err.raw_os_error(),
            Some(libc::ELOOP),
            "expected ELOOP (libc::ELOOP = {}), got io error: {err:?}",
            libc::ELOOP
        );

        // And opening the REAL path directly still works — the
        // flag only bites symlinks on the final component, not
        // regular files.
        let direct = open_nofollow(&real).expect("regular file should open");
        let mut buf = String::new();
        use std::io::Read;
        let mut r = direct;
        r.read_to_string(&mut buf).unwrap();
        assert_eq!(buf, "real contents");
    }

    #[test]
    fn content_hash_is_deterministic() {
        let h1 = content_hash(b"hello world");
        let h2 = content_hash(b"hello world");
        assert_eq!(h1, h2);
        assert_eq!(h1.len(), 64); // blake3 hex = 64 chars
    }

    #[test]
    fn content_hash_distinguishes_inputs() {
        assert_ne!(content_hash(b"a"), content_hash(b"b"));
        assert_ne!(content_hash(b""), content_hash(b" "));
    }

    #[test]
    fn write_then_read_round_trip() {
        let (_tmp, p) = vault();
        p.write_file("notes/foo.md", b"# hello").unwrap();
        let bytes = p.read_file("notes/foo.md").unwrap();
        assert_eq!(bytes, b"# hello");
    }

    #[test]
    fn write_overwrites_existing_file() {
        let (_tmp, p) = vault();
        p.write_file("notes/foo.md", b"first").unwrap();
        p.write_file("notes/foo.md", b"second").unwrap();
        let bytes = p.read_file("notes/foo.md").unwrap();
        assert_eq!(bytes, b"second");
    }

    #[test]
    fn write_creates_intermediate_directories() {
        let (_tmp, p) = vault();
        p.write_file("a/b/c/d.md", b"deep").unwrap();
        let bytes = p.read_file("a/b/c/d.md").unwrap();
        assert_eq!(bytes, b"deep");
    }

    #[test]
    fn create_dir_if_absent_creates_only_an_unoccupied_final_component() {
        let (tmp, p) = vault();
        std::fs::create_dir(tmp.path().join("parent")).unwrap();

        p.create_dir_if_absent("parent/fresh").unwrap();

        assert!(tmp.path().join("parent/fresh").is_dir());
    }

    #[test]
    fn create_dir_if_absent_maps_an_existing_directory_to_destination_exists() {
        let (tmp, p) = vault();
        std::fs::create_dir(tmp.path().join("occupied")).unwrap();

        let error = p.create_dir_if_absent("occupied").unwrap_err();

        assert!(
            matches!(error, VaultError::DestinationExists { ref path } if path == "occupied"),
            "{error:?}"
        );
    }

    #[test]
    fn create_dir_if_absent_maps_an_existing_file_to_destination_exists() {
        let (tmp, p) = vault();
        std::fs::write(tmp.path().join("occupied"), b"external bytes").unwrap();

        let error = p.create_dir_if_absent("occupied").unwrap_err();

        assert!(
            matches!(error, VaultError::DestinationExists { ref path } if path == "occupied"),
            "{error:?}"
        );
        assert_eq!(
            std::fs::read(tmp.path().join("occupied")).unwrap(),
            b"external bytes"
        );
    }

    #[cfg(unix)]
    #[test]
    fn create_dir_if_absent_maps_a_dangling_symlink_to_destination_exists() {
        let (tmp, p) = vault();
        std::os::unix::fs::symlink(
            tmp.path().join("missing-target"),
            tmp.path().join("occupied"),
        )
        .unwrap();

        let error = p.create_dir_if_absent("occupied").unwrap_err();

        assert!(
            matches!(error, VaultError::DestinationExists { ref path } if path == "occupied"),
            "{error:?}"
        );
        assert!(tmp.path().join("occupied").symlink_metadata().is_ok());
    }

    #[test]
    fn create_dir_if_absent_does_not_create_missing_parents() {
        let (tmp, p) = vault();

        let error = p.create_dir_if_absent("missing/fresh").unwrap_err();

        assert!(
            matches!(error, VaultError::Io(ref error) if error.kind() == io::ErrorKind::NotFound),
            "{error:?}"
        );
        assert!(!tmp.path().join("missing").exists());
    }

    #[test]
    fn create_dir_if_absent_has_exactly_one_winner_between_racing_creators() {
        let (tmp, provider) = vault();
        let provider = Arc::new(provider);
        let barrier = Arc::new(std::sync::Barrier::new(3));
        let mut creators = Vec::new();
        for _ in 0..2 {
            let provider = Arc::clone(&provider);
            let barrier = Arc::clone(&barrier);
            creators.push(std::thread::spawn(move || {
                barrier.wait();
                provider.create_dir_if_absent("winner")
            }));
        }
        barrier.wait();

        let results: Vec<_> = creators
            .into_iter()
            .map(|creator| creator.join().expect("creator thread"))
            .collect();

        assert_eq!(results.iter().filter(|result| result.is_ok()).count(), 1);
        assert_eq!(
            results
                .iter()
                .filter(|result| matches!(result, Err(VaultError::DestinationExists { .. })))
                .count(),
            1
        );
        assert!(tmp.path().join("winner").is_dir());
    }

    #[cfg(unix)]
    #[test]
    fn create_dir_if_absent_cannot_be_redirected_when_an_ancestor_is_replaced() {
        let (tmp, provider) = vault();
        let outside = tempfile::tempdir().unwrap();
        let parent = tmp.path().join("parent");
        let pinned_parent = tmp.path().join("parent-pinned");
        std::fs::create_dir(&parent).unwrap();

        // Pin the vault-relative destination, then replace an ancestor before
        // the final create. The descriptor-relative mkdir must stay with the
        // directory we opened rather than following the replacement symlink.
        let destination = provider
            .pin_mutation_target("parent/fresh", false, Mutation::CreateFolder)
            .unwrap();
        std::fs::rename(&parent, &pinned_parent).unwrap();
        std::os::unix::fs::symlink(outside.path(), &parent).unwrap();

        destination.create_dir_if_absent().unwrap();

        assert!(
            !outside.path().join("fresh").exists(),
            "directory creation escaped through the replacement symlink"
        );
        assert!(pinned_parent.join("fresh").is_dir());
    }

    #[cfg(unix)]
    #[test]
    fn create_dir_if_absent_allows_a_write_search_only_parent() {
        use std::os::unix::fs::PermissionsExt as _;

        let (tmp, provider) = vault();
        let parent = tmp.path().join("parent");
        std::fs::create_dir(&parent).unwrap();
        std::fs::set_permissions(&parent, fs::Permissions::from_mode(0o300)).unwrap();

        let outcome = provider.create_dir_if_absent("parent/fresh");

        // Restore owner access before asserting so TempDir cleanup remains
        // reliable even when the regression fails.
        std::fs::set_permissions(&parent, fs::Permissions::from_mode(0o700)).unwrap();
        outcome.expect("mkdir only requires write and search permission on its parent");
        assert!(parent.join("fresh").is_dir());
    }

    #[test]
    fn list_dir_returns_alphabetical_entries() {
        let (_tmp, p) = vault();
        p.write_file("zebra.md", b"").unwrap();
        p.write_file("apple.md", b"").unwrap();
        p.write_file("mango.md", b"").unwrap();

        // Filter dotfiles: write_file auto-creates `.slate/tmp/` for its
        // temp files, which is internal scaffolding rather than user
        // content. Callers iterating user-visible vault entries should
        // skip dot-prefixed names (same rule the scanner applies).
        let entries = p.list_dir("").unwrap();
        let visible: Vec<&DirEntry> = entries
            .iter()
            .filter(|e| !e.name.starts_with('.'))
            .collect();
        let names: Vec<&str> = visible.iter().map(|e| e.name.as_str()).collect();
        assert_eq!(names, vec!["apple.md", "mango.md", "zebra.md"]);
        for e in &visible {
            assert_eq!(e.kind, EntryKind::File);
        }
    }

    #[test]
    fn list_dir_distinguishes_files_and_directories() {
        let (_tmp, p) = vault();
        p.write_file("note.md", b"").unwrap();
        p.write_file("subfolder/inner.md", b"").unwrap();

        let entries = p.list_dir("").unwrap();
        let map: std::collections::HashMap<_, _> =
            entries.iter().map(|e| (e.name.as_str(), e.kind)).collect();
        assert_eq!(map.get("note.md"), Some(&EntryKind::File));
        assert_eq!(map.get("subfolder"), Some(&EntryKind::Directory));
    }

    #[test]
    fn stat_reports_size_and_kind() {
        let (_tmp, p) = vault();
        p.write_file("foo.md", b"hello world").unwrap();
        let stat = p.stat("foo.md").unwrap();
        assert_eq!(stat.size_bytes, 11);
        assert_eq!(stat.kind, EntryKind::File);
        assert!(stat.mtime_ms > 0);
    }

    #[test]
    fn stat_missing_file_returns_io_not_found() {
        let (_tmp, p) = vault();
        let err = p.stat("never.md").unwrap_err();
        match err {
            VaultError::Io(io) => assert_eq!(io.kind(), io::ErrorKind::NotFound),
            other => panic!("expected Io NotFound, got {other:?}"),
        }
    }

    #[test]
    fn read_missing_file_returns_io_not_found() {
        let (_tmp, p) = vault();
        let err = p.read_file("missing.md").unwrap_err();
        match err {
            VaultError::Io(io) => assert_eq!(io.kind(), io::ErrorKind::NotFound),
            other => panic!("expected Io NotFound, got {other:?}"),
        }
    }

    #[test]
    fn rename_moves_file() {
        let (_tmp, p) = vault();
        p.write_file("old.md", b"content").unwrap();
        p.rename("old.md", "new.md").unwrap();
        assert!(p.read_file("old.md").is_err());
        let bytes = p.read_file("new.md").unwrap();
        assert_eq!(bytes, b"content");
    }

    #[test]
    fn rename_into_new_directory_creates_path() {
        let (_tmp, p) = vault();
        p.write_file("old.md", b"content").unwrap();
        p.rename("old.md", "archive/2026/old.md").unwrap();
        let bytes = p.read_file("archive/2026/old.md").unwrap();
        assert_eq!(bytes, b"content");
    }

    #[test]
    fn delete_removes_the_file() {
        let (_tmp, p) = vault();
        p.write_file("doomed.md", b"").unwrap();
        p.delete("doomed.md").unwrap();
        let err = p.read_file("doomed.md").unwrap_err();
        match err {
            VaultError::Io(io) => assert_eq!(io.kind(), io::ErrorKind::NotFound),
            other => panic!("expected Io NotFound, got {other:?}"),
        }
    }

    #[test]
    fn delete_missing_path_errors() {
        let (_tmp, p) = vault();
        let err = p.delete("never.md").unwrap_err();
        match err {
            VaultError::Io(io) => assert_eq!(io.kind(), io::ErrorKind::NotFound),
            other => panic!("expected Io NotFound, got {other:?}"),
        }
    }

    #[test]
    fn absolute_path_is_rejected() {
        let (_tmp, p) = vault();
        let err = p.read_file("/etc/passwd").unwrap_err();
        assert!(matches!(err, VaultError::InvalidPath { .. }));
    }

    #[test]
    fn parent_directory_traversal_is_rejected() {
        let (_tmp, p) = vault();
        let err = p.read_file("../../escape.md").unwrap_err();
        assert!(matches!(err, VaultError::InvalidPath { .. }));
    }

    #[test]
    fn windows_path_validator_rejects_device_aliases_and_rewritten_names() {
        for path in [
            "CON",
            "con.md",
            "folder/NUL.txt",
            "AUX.notes.md",
            "COM1",
            "com9.log",
            "LPT1.md",
            "lpt9",
            "CONIN$",
            "conout$.md",
            "trailing-dot.",
            "trailing-space ",
            "alternate:stream.md",
            "question?.md",
            "wild*.md",
            "control\u{001f}.md",
        ] {
            assert!(
                validate_windows_relative_path(path).is_err(),
                "Windows-reserved path unexpectedly accepted: {path:?}"
            );
        }
    }

    #[test]
    fn windows_path_validator_accepts_device_name_near_misses() {
        for path in [
            "console.md",
            "null.md",
            "com0.md",
            "com10.md",
            "lpt0.md",
            "lpt10.md",
            "notes/ordinary file.md",
        ] {
            validate_windows_relative_path(path)
                .unwrap_or_else(|reason| panic!("valid Windows path {path:?} rejected: {reason}"));
        }
    }

    #[cfg(windows)]
    #[test]
    fn windows_provider_surfaces_reserved_names_as_invalid_paths() {
        let (_tmp, provider) = vault();
        for path in ["CON.md", "notes/NUL", "LPT9.txt", "trailing-dot."] {
            let error = provider
                .write_file(path, b"must not be written")
                .unwrap_err();
            assert!(
                matches!(error, VaultError::InvalidPath { path: ref invalid, .. } if invalid == path),
                "expected InvalidPath for {path:?}, got {error:?}"
            );
        }
    }

    #[cfg(windows)]
    #[test]
    fn windows_provider_is_case_insensitive_and_case_preserving() {
        let (_tmp, provider) = vault();
        provider.write_file("MixedCase.md", b"original").unwrap();

        assert_eq!(provider.read_file("mixedcase.md").unwrap(), b"original");
        let entries = provider.list_dir("").unwrap();
        assert!(entries.iter().any(|entry| entry.name == "MixedCase.md"));

        let error = provider
            .write_file_if_absent("MIXEDCASE.md", b"replacement")
            .unwrap_err();
        assert!(matches!(error, VaultError::DestinationExists { .. }));
        assert_eq!(provider.read_file("MixedCase.md").unwrap(), b"original");
    }

    #[cfg(windows)]
    #[test]
    fn windows_provider_round_trips_paths_longer_than_max_path() {
        let (tmp, provider) = vault();
        let directory = (0..8)
            .map(|index| format!("segment-{index}-{}", "x".repeat(28)))
            .collect::<Vec<_>>()
            .join("/");
        let relative = format!("{directory}/long-note.md");
        assert!(
            tmp.path().join(&relative).as_os_str().len() > 260,
            "fixture must cross the legacy MAX_PATH boundary"
        );

        provider
            .write_file(&relative, b"long path contents")
            .unwrap();

        assert_eq!(
            provider.read_file(&relative).unwrap(),
            b"long path contents"
        );
    }

    #[test]
    fn current_directory_component_is_stripped() {
        let (_tmp, p) = vault();
        p.write_file("./inside.md", b"ok").unwrap();
        let bytes = p.read_file("inside.md").unwrap();
        assert_eq!(bytes, b"ok");
    }

    #[test]
    fn empty_relative_lists_root() {
        let (_tmp, p) = vault();
        p.write_file("a.md", b"").unwrap();
        p.write_file("b.md", b"").unwrap();
        let entries = p.list_dir("").unwrap();
        let visible: Vec<&DirEntry> = entries
            .iter()
            .filter(|e| !e.name.starts_with('.'))
            .collect();
        assert_eq!(visible.len(), 2);
    }

    #[test]
    fn dot_relative_lists_root() {
        let (_tmp, p) = vault();
        p.write_file("a.md", b"").unwrap();
        let entries = p.list_dir(".").unwrap();
        let visible: Vec<&DirEntry> = entries
            .iter()
            .filter(|e| !e.name.starts_with('.'))
            .collect();
        assert_eq!(visible.len(), 1);
        assert_eq!(visible[0].name, "a.md");
    }

    #[test]
    fn content_hash_for_reads_file() {
        let (_tmp, p) = vault();
        p.write_file("hash-me.md", b"abc").unwrap();
        let h = p.content_hash_for("hash-me.md").unwrap();
        assert_eq!(h, content_hash(b"abc"));
    }

    /// Plan 43 PR 20a (#1267) replaces `watch_returns_none_in_milestone_a`
    /// on the owner's recorded call (2026-10-17, design §11: the watcher
    /// lands in core behind `watch`; the TGC-17 rule rewrites the frozen fact
    /// before the code changes): a local vault's watch is a live handle,
    /// anchored to its root, and an external write reaches its sink.
    #[test]
    fn watch_returns_a_live_handle_on_a_local_vault() {
        use super::super::provider::FileEvent;
        use std::sync::{Condvar, Mutex};
        use std::time::{Duration, Instant};

        #[derive(Default)]
        struct Recording(Mutex<Vec<FileEvent>>, Condvar);
        impl FileEventSink for Recording {
            fn on_event(&self, event: FileEvent) {
                self.0.lock().unwrap().push(event);
                self.1.notify_all();
            }
        }

        let (tmp, p) = vault();
        let sink = Arc::new(Recording::default());
        let handle = p
            .watch(sink.clone())
            .unwrap()
            .expect("a live watch on a local vault");
        assert!(handle.arm_identity().is_some(), "anchored at arm time");
        assert!(handle.root_still_anchored());

        std::fs::write(tmp.path().join("external.md"), b"x").unwrap();
        let deadline = Instant::now() + Duration::from_secs(10);
        let mut events = sink.0.lock().unwrap();
        while !events.iter().any(|event| {
            matches!(event,
                FileEvent::Created { relative } | FileEvent::Modified { relative }
                    if relative == "external.md")
        }) {
            let now = Instant::now();
            assert!(
                now < deadline,
                "the external write never arrived: {events:?}"
            );
            events = sink.1.wait_timeout(events, deadline - now).unwrap().0;
        }
        drop(events);
        drop(handle);
    }

    /// Plan 43 PR 20a (design §4.2): every mutation the provider completes
    /// records its post-state, and the folders above it, so the watcher's echo
    /// test claims the events it caused; an external change after it, or a
    /// path the provider never touched, is no echo.
    #[test]
    fn every_provider_mutation_records_its_post_state_as_an_echo() {
        let (tmp, p) = vault();
        let disk = |relative: &str| tmp.path().join(relative);

        // A save into new folders, then an overwrite.
        p.write_file("a/b/note.md", b"one").unwrap();
        for echo in ["a/b/note.md", "a/b", "a"] {
            assert!(p.is_own_echo(echo), "{echo}");
        }
        p.write_file("a/b/note.md", b"two!").unwrap();
        assert!(p.is_own_echo("a/b/note.md"));
        std::fs::write(disk("a/b/note.md"), b"an external edit").unwrap();
        assert!(
            !p.is_own_echo("a/b/note.md"),
            "an external write is no echo"
        );

        // An exclusive create and two folder creates.
        p.write_file_if_absent("c/new.md", b"new").unwrap();
        assert!(p.is_own_echo("c/new.md") && p.is_own_echo("c"));
        p.create_dir("d/e").unwrap();
        assert!(p.is_own_echo("d/e") && p.is_own_echo("d"));
        p.create_dir_if_absent("f").unwrap();
        assert!(p.is_own_echo("f"));

        // A move: the old path's absence and the new path are both echoes.
        p.rename("c/new.md", "g/moved.md").unwrap();
        for echo in ["c/new.md", "c", "g/moved.md", "g"] {
            assert!(p.is_own_echo(echo), "{echo}");
        }
        std::fs::write(disk("c/new.md"), b"back").unwrap();
        assert!(!p.is_own_echo("c/new.md"), "a file recreated outside Slate");

        // Trash: the entry's absence is the echo.
        p.delete("g/moved.md").unwrap();
        assert!(p.is_own_echo("g/moved.md") && p.is_own_echo("g"));

        // Paths the provider never wrote, and refusals, record nothing.
        std::fs::write(disk("untouched.md"), b"x").unwrap();
        assert!(!p.is_own_echo("untouched.md"));
        assert!(!p.is_own_echo("never-existed.md"));
        assert!(p.write_file_if_absent("untouched.md", b"y").is_err());
        assert!(
            !p.is_own_echo("untouched.md"),
            "a refused create is no echo"
        );
        assert!(!p.is_own_echo(""), "the root is never an echo");
    }

    /// Plan 43 PR 20a: a backslash is part of a name on Unix, so Slate's
    /// write of `a\b.md` is that file's echo and never claims an external
    /// write to the nested `a/b.md`.
    #[cfg(unix)]
    #[test]
    fn a_backslash_in_a_unix_name_never_claims_another_path() {
        let (tmp, p) = vault();
        std::fs::create_dir(tmp.path().join("a")).unwrap();
        std::fs::write(tmp.path().join("a").join("b.md"), b"written outside").unwrap();
        p.write_file(r"a\b.md", b"written by slate").unwrap();
        assert!(p.is_own_echo(r"a\b.md"));
        assert!(!p.is_own_echo("a/b.md"), "the nested file is not Slate's");
    }

    #[cfg(unix)]
    #[test]
    fn list_dir_reports_symlink_as_symlink() {
        let (tmp, p) = vault();
        p.write_file("target.md", b"target").unwrap();
        std::os::unix::fs::symlink(tmp.path().join("target.md"), tmp.path().join("link.md"))
            .unwrap();

        let entries = p.list_dir("").unwrap();
        let map: std::collections::HashMap<_, _> =
            entries.iter().map(|e| (e.name.as_str(), e.kind)).collect();
        assert_eq!(map.get("target.md"), Some(&EntryKind::File));
        assert_eq!(map.get("link.md"), Some(&EntryKind::Symlink));
    }

    #[test]
    fn mutators_refuse_vault_root() {
        let (tmp, p) = vault();
        p.write_file("survivor.md", b"hi").unwrap();

        for relative in ["", "."] {
            let err = p.delete(relative).unwrap_err();
            assert!(
                matches!(err, VaultError::InvalidPath { .. }),
                "delete({relative:?}) should reject vault root, got {err:?}"
            );
            let err = p.write_file(relative, b"x").unwrap_err();
            assert!(
                matches!(err, VaultError::InvalidPath { .. }),
                "write_file({relative:?}) should reject vault root, got {err:?}"
            );
            let err = p.rename(relative, "elsewhere.md").unwrap_err();
            assert!(
                matches!(err, VaultError::InvalidPath { .. }),
                "rename(from={relative:?}) should reject vault root, got {err:?}"
            );
            let err = p.rename("survivor.md", relative).unwrap_err();
            assert!(
                matches!(err, VaultError::InvalidPath { .. }),
                "rename(to={relative:?}) should reject vault root, got {err:?}"
            );
        }

        // The vault directory and its sole file are untouched.
        assert!(tmp.path().is_dir());
        assert_eq!(p.read_file("survivor.md").unwrap(), b"hi");
    }

    #[test]
    fn rename_missing_source_does_not_create_destination_parents() {
        let (_tmp, p) = vault();
        let err = p
            .rename("nope.md", "archive/2026/old.md")
            .expect_err("missing source should error");
        match err {
            VaultError::Io(io) => assert_eq!(io.kind(), io::ErrorKind::NotFound),
            other => panic!("expected Io NotFound, got {other:?}"),
        }
        // The dest tree must not exist — that's the regression we're guarding.
        let listing = p.list_dir("").unwrap();
        assert!(
            listing.is_empty(),
            "vault root should be empty, found {listing:?}"
        );
    }

    #[test]
    fn write_places_temp_under_dot_slate_tmp_not_in_target_dir() {
        let (tmp, p) = vault();
        p.write_file("notes/foo.md", b"hi").unwrap();

        // No .tmpXXXX siblings of the target after a successful write.
        let notes_entries = p.list_dir("notes").unwrap();
        for entry in &notes_entries {
            assert!(
                !entry.name.starts_with(".tmp"),
                "found leftover temp {entry:?} alongside the target"
            );
        }
        let names: Vec<&str> = notes_entries.iter().map(|e| e.name.as_str()).collect();
        assert_eq!(names, vec!["foo.md"]);

        // The hidden tmp dir was created.
        assert!(
            tmp.path().join(".slate").join("tmp").is_dir(),
            ".slate/tmp/ should exist after the first write"
        );
    }

    fn create_staging_entries(tmp: &tempfile::TempDir) -> Vec<String> {
        let staging_dir = tmp.path().join(".slate").join("tmp");
        match std::fs::read_dir(staging_dir) {
            Ok(entries) => entries
                .map(|entry| entry.unwrap().file_name().to_string_lossy().into_owned())
                .filter(|name| name.starts_with("create-"))
                .collect(),
            Err(error) if error.kind() == io::ErrorKind::NotFound => Vec::new(),
            Err(error) => panic!("failed to inspect create staging directory: {error}"),
        }
    }

    #[test]
    fn create_exclusive_cleans_staged_temp_after_write_failure() {
        let (tmp, provider) = vault();

        let error = provider
            .write_file_if_absent_with_staging(
                "fresh.md",
                b"candidate bytes",
                |_file, _contents| {
                    Err(io::Error::new(
                        io::ErrorKind::WriteZero,
                        "injected staged write failure",
                    ))
                },
                |_file| Ok(()),
                publish_staged_no_replace,
            )
            .expect_err("the injected write failure must escape");

        assert!(
            matches!(error, VaultError::Io(ref error) if error.kind() == io::ErrorKind::WriteZero)
        );
        assert!(!tmp.path().join("fresh.md").exists());
        assert_eq!(create_staging_entries(&tmp), Vec::<String>::new());
    }

    #[test]
    fn create_exclusive_cleans_staged_temp_after_sync_failure() {
        let (tmp, provider) = vault();

        let error = provider
            .write_file_if_absent_with_staging(
                "fresh.md",
                b"candidate bytes",
                |file, contents| file.write_all(contents),
                |_file| Err(io::Error::other("injected staged sync failure")),
                publish_staged_no_replace,
            )
            .expect_err("the injected sync failure must escape");

        assert!(matches!(error, VaultError::Io(ref error) if error.kind() == io::ErrorKind::Other));
        assert!(!tmp.path().join("fresh.md").exists());
        assert_eq!(create_staging_entries(&tmp), Vec::<String>::new());
    }

    #[cfg(unix)]
    #[test]
    fn create_exclusive_publish_success_does_not_depend_on_unlinking_old_staging_name() {
        use std::os::unix::fs::PermissionsExt as _;

        let (tmp, provider) = vault();
        let staging_dir = tmp.path().join(".slate").join("tmp");

        let outcome = provider.write_file_if_absent_with_staging(
            "fresh.md",
            b"candidate bytes",
            |file, contents| file.write_all(contents),
            fs::File::sync_data,
            |staged, destination| {
                destination.publish_staged_no_replace(staged)?;
                // Reproduce the reviewer's post-publish edge: any attempt to
                // unlink the old staging name now fails. A successful atomic
                // move must nevertheless remain a successful create.
                fs::set_permissions(
                    staged
                        .staged_path()
                        .and_then(Path::parent)
                        .expect("staging parent"),
                    fs::Permissions::from_mode(0o000),
                )?;
                Ok(())
            },
        );
        fs::set_permissions(&staging_dir, fs::Permissions::from_mode(0o700)).unwrap();

        outcome.expect("the destination was published and must not report a false failure");
        assert_eq!(
            std::fs::read(tmp.path().join("fresh.md")).unwrap(),
            b"candidate bytes"
        );
        assert_eq!(create_staging_entries(&tmp), Vec::<String>::new());
    }

    #[cfg(unix)]
    #[test]
    fn create_exclusive_collision_cleanup_failure_is_reported_not_discarded() {
        use std::os::unix::fs::PermissionsExt as _;

        let (tmp, provider) = vault();
        let staging_dir = tmp.path().join(".slate").join("tmp");
        std::fs::write(tmp.path().join("occupied.md"), b"external winner").unwrap();

        let error = provider
            .write_file_if_absent_with_staging(
                "occupied.md",
                b"Slate candidate",
                |file, contents| file.write_all(contents),
                fs::File::sync_data,
                |staged, _destination| {
                    fs::set_permissions(
                        staged
                            .staged_path()
                            .and_then(Path::parent)
                            .expect("staging parent"),
                        fs::Permissions::from_mode(0o000),
                    )?;
                    Err(io::Error::new(
                        io::ErrorKind::AlreadyExists,
                        "injected no-replace collision",
                    ))
                },
            )
            .expect_err("cleanup failure after a collision must not be silent");
        fs::set_permissions(&staging_dir, fs::Permissions::from_mode(0o700)).unwrap();

        let message = match error {
            VaultError::Io(error) => error.to_string(),
            other => panic!("cleanup failure must surface as IO context, got {other:?}"),
        };
        assert!(
            message.contains("injected no-replace collision"),
            "{message}"
        );
        assert!(
            message.contains("failed to remove create staging file"),
            "{message}"
        );
        assert_eq!(
            std::fs::read(tmp.path().join("occupied.md")).unwrap(),
            b"external winner"
        );

        // The filesystem refused deletion, so residue is unavoidable; the
        // contract is that it is explicitly reported rather than discarded.
        let staged = create_staging_entries(&tmp);
        assert_eq!(
            staged.len(),
            1,
            "the injected cleanup failure must be observable"
        );
        std::fs::remove_file(staging_dir.join(&staged[0])).unwrap();
    }

    #[test]
    fn create_exclusive_collision_preserves_winner_bytes_and_cleans_staged_temp() {
        let (tmp, provider) = vault();
        std::fs::write(tmp.path().join("occupied.md"), b"external winner").unwrap();

        let error = provider
            .write_file_if_absent("occupied.md", b"Slate candidate")
            .expect_err("an occupied destination must win");

        assert!(matches!(
            error,
            VaultError::DestinationExists { ref path } if path == "occupied.md"
        ));
        assert_eq!(
            std::fs::read(tmp.path().join("occupied.md")).unwrap(),
            b"external winner"
        );
        assert_eq!(create_staging_entries(&tmp), Vec::<String>::new());
    }

    #[cfg(unix)]
    #[test]
    fn create_exclusive_cannot_be_redirected_when_an_ancestor_is_replaced() {
        let (tmp, provider) = vault();
        let outside = tempfile::tempdir().unwrap();
        let parent = tmp.path().join("parent");
        let pinned_parent = tmp.path().join("parent-pinned");
        std::fs::create_dir(&parent).unwrap();

        let outcome = provider.write_file_if_absent_with_staging(
            "parent/fresh.md",
            b"candidate bytes",
            |file, contents| file.write_all(contents),
            |file| {
                file.sync_data()?;
                // Staging is complete and the destination has already been
                // resolved. Swap the ancestor immediately before publish.
                std::fs::rename(&parent, &pinned_parent)?;
                std::os::unix::fs::symlink(outside.path(), &parent)?;
                Ok(())
            },
            publish_staged_no_replace,
        );

        outcome.expect("publishing into the pinned destination should succeed");
        assert!(
            !outside.path().join("fresh.md").exists(),
            "file creation escaped through the replacement symlink"
        );
        assert_eq!(
            std::fs::read(pinned_parent.join("fresh.md")).unwrap(),
            b"candidate bytes"
        );
        assert_eq!(create_staging_entries(&tmp), Vec::<String>::new());
    }

    #[cfg(unix)]
    #[test]
    fn rename_moves_broken_symlink() {
        let (tmp, p) = vault();
        std::os::unix::fs::symlink(
            tmp.path().join("does-not-exist.md"),
            tmp.path().join("broken.md"),
        )
        .unwrap();

        p.rename("broken.md", "still-broken.md")
            .expect("rename should move the dangling entry");

        assert!(tmp.path().join("broken.md").symlink_metadata().is_err());
        let moved = tmp
            .path()
            .join("still-broken.md")
            .symlink_metadata()
            .expect("renamed symlink should be a real entry at the destination");
        assert!(moved.file_type().is_symlink());
    }

    #[test]
    fn rename_refuses_to_clobber_an_existing_destination() {
        // #871 Codex round 3: structural rename/move is NO-CLOBBER — replacing
        // the destination via `fs::rename` would be silent data loss.
        let (tmp, p) = vault();
        p.write_file("src.md", b"source").unwrap();
        p.write_file("dst.md", b"victim").unwrap();

        let err = p.rename("src.md", "dst.md").unwrap_err();
        assert!(matches!(err, VaultError::DestinationExists { .. }));

        // The victim is untouched and the source stays put.
        assert_eq!(p.read_file("dst.md").unwrap(), b"victim");
        assert!(tmp.path().join("src.md").exists());
    }

    #[cfg(unix)]
    #[test]
    fn rename_refuses_to_clobber_a_dangling_symlink_destination() {
        // A DANGLING symlink at the destination: `exists()` reports it absent,
        // but the lstat-based guard must still refuse — else `fs::rename`
        // destroys it (Codex round 3).
        let (tmp, p) = vault();
        p.write_file("src.md", b"source").unwrap();
        std::os::unix::fs::symlink(tmp.path().join("nowhere.md"), tmp.path().join("dst.md"))
            .unwrap();

        let err = p.rename("src.md", "dst.md").unwrap_err();
        assert!(matches!(err, VaultError::DestinationExists { .. }));

        // The dangling symlink survives; the source stays put.
        assert!(tmp.path().join("dst.md").symlink_metadata().is_ok());
        assert!(tmp.path().join("src.md").exists());
    }

    /// The atomic no-replace primitive each platform's structural rename uses,
    /// called directly with absolute paths inside `root`: beneath the pinned
    /// root descriptor on Unix (#1449), and on Windows `FileRenameInformation`
    /// on the source handle into the pinned destination folder, without
    /// replacement (#1451).
    #[cfg(any(target_os = "macos", target_os = "linux", windows))]
    fn rename_no_replace_at(root: &Path, from: &Path, to: &Path) -> io::Result<()> {
        let unpinned = |error: VaultError| match error {
            VaultError::Io(error) => error,
            other => io::Error::other(other.to_string()),
        };
        let from_relative = from.strip_prefix(root).expect("source inside the root");
        let to_relative = to.strip_prefix(root).expect("destination inside the root");
        #[cfg(unix)]
        {
            let pinned = pinned_root::PinnedRoot::open(root).map_err(unpinned)?;
            let source = pinned
                .end(from_relative, "source", false)
                .map_err(unpinned)?;
            let destination = pinned
                .end(to_relative, "destination", false)
                .map_err(unpinned)?;
            pinned.rename_no_replace(&source, &destination)
        }
        #[cfg(windows)]
        {
            use crate::vault::windows_entry::{self, Folders, PinnedRoot};
            let pinned = PinnedRoot::open(root).map_err(unpinned)?;
            let folder = |relative: &Path, original: &str| {
                pinned
                    .folder(
                        parent_of(relative),
                        original,
                        false,
                        Mutation::Move,
                        Folders::NotLinks,
                    )
                    .map_err(unpinned)
            };
            let source_parent = folder(from_relative, "source")?;
            let destination_parent = folder(to_relative, "destination")?;
            let source = windows_entry::open_for_rename(
                source_parent.handle(),
                from_relative.file_name().expect("a source name"),
            )?;
            windows_entry::rename_into(
                &source,
                destination_parent.handle(),
                to_relative.file_name().expect("a destination name"),
                false,
            )
        }
    }

    /// #871 Codex round 4: the ATOMIC primitive itself must refuse an occupied
    /// destination with `EEXIST` — this is the layer that closes the
    /// check-to-rename TOCTOU window (an external writer winning the race after
    /// `provider.rename`'s lstat pre-check). Tested directly, bypassing that
    /// pre-check, since the window itself is inherently non-deterministic.
    #[cfg(any(target_os = "macos", target_os = "linux", windows))]
    #[test]
    fn rename_no_replace_atomic_refuses_occupied_destination() {
        let (tmp, _p) = vault();
        std::fs::write(tmp.path().join("src.md"), b"source").unwrap();
        std::fs::write(tmp.path().join("dst.md"), b"victim").unwrap();

        let err = rename_no_replace_at(
            tmp.path(),
            &tmp.path().join("src.md"),
            &tmp.path().join("dst.md"),
        )
        .expect_err("atomic no-replace must refuse an occupied destination");

        // EEXIST maps to `AlreadyExists` — the same portable check `rename` uses.
        assert_eq!(err.kind(), io::ErrorKind::AlreadyExists);
        assert_eq!(std::fs::read(tmp.path().join("dst.md")).unwrap(), b"victim");
        assert!(tmp.path().join("src.md").exists());
    }

    #[cfg(any(target_os = "macos", target_os = "linux", windows))]
    #[test]
    fn rename_no_replace_atomic_moves_files_and_directories() {
        let (tmp, _p) = vault();
        let source_file = tmp.path().join("source.md");
        let moved_file = tmp.path().join("moved.md");
        std::fs::write(&source_file, b"source").unwrap();

        rename_no_replace_at(tmp.path(), &source_file, &moved_file)
            .expect("file rename should succeed");
        assert!(!source_file.exists());
        assert_eq!(std::fs::read(&moved_file).unwrap(), b"source");

        let source_dir = tmp.path().join("source-dir");
        let moved_dir = tmp.path().join("moved-dir");
        std::fs::create_dir(&source_dir).unwrap();
        std::fs::write(source_dir.join("child.md"), b"child").unwrap();

        rename_no_replace_at(tmp.path(), &source_dir, &moved_dir)
            .expect("directory rename should succeed");
        assert!(!source_dir.exists());
        assert_eq!(std::fs::read(moved_dir.join("child.md")).unwrap(), b"child");
    }

    #[cfg(any(target_os = "macos", target_os = "linux", windows))]
    #[test]
    fn rename_no_replace_destination_creation_race_never_clobbers() {
        use std::io::Write;
        use std::sync::{Arc, Barrier};

        let (tmp, _p) = vault();
        for attempt in 0..128 {
            let source = tmp.path().join(format!("race-source-{attempt}.md"));
            let destination = tmp.path().join(format!("race-destination-{attempt}.md"));
            std::fs::write(&source, b"source").unwrap();

            let barrier = Arc::new(Barrier::new(2));
            let writer_barrier = Arc::clone(&barrier);
            let writer_destination = destination.clone();
            let writer = std::thread::spawn(move || {
                writer_barrier.wait();
                let mut file = match std::fs::OpenOptions::new()
                    .write(true)
                    .create_new(true)
                    .open(&writer_destination)
                {
                    Ok(file) => file,
                    Err(error) if error.kind() == io::ErrorKind::AlreadyExists => {
                        return Ok(false);
                    }
                    Err(error) => return Err(error),
                };
                file.write_all(b"victim")?;
                file.sync_all()?;
                Ok(true)
            });

            barrier.wait();
            let rename_result = rename_no_replace_at(tmp.path(), &source, &destination);
            let writer_created = writer.join().expect("destination writer panicked").unwrap();

            if writer_created {
                let error = rename_result.expect_err("occupied destination must reject rename");
                assert_eq!(error.kind(), io::ErrorKind::AlreadyExists);
                assert_eq!(std::fs::read(&destination).unwrap(), b"victim");
                assert_eq!(std::fs::read(&source).unwrap(), b"source");
            } else {
                rename_result.expect("rename winner should complete");
                assert_eq!(std::fs::read(&destination).unwrap(), b"source");
                assert!(!source.exists());
            }
        }
    }

    #[cfg(unix)]
    #[test]
    fn delete_removes_broken_symlink() {
        let (tmp, p) = vault();
        std::os::unix::fs::symlink(
            tmp.path().join("does-not-exist.md"),
            tmp.path().join("broken.md"),
        )
        .unwrap();
        // Sanity: the broken link is visible to list_dir but `exists()`
        // would have hidden it.
        assert!(!tmp.path().join("broken.md").exists());
        assert!(tmp.path().join("broken.md").symlink_metadata().is_ok());

        p.delete("broken.md").expect("delete should succeed");
        assert!(tmp.path().join("broken.md").symlink_metadata().is_err());
    }

    #[cfg(unix)]
    #[test]
    fn write_preserves_existing_file_permissions_on_overwrite() {
        use std::os::unix::fs::PermissionsExt;

        let (_tmp, p) = vault();
        p.write_file("notes/foo.md", b"first").unwrap();

        // Loosen the file to 0644 (NamedTempFile's default is 0600).
        let path = p.resolve("notes/foo.md").unwrap();
        fs::set_permissions(&path, fs::Permissions::from_mode(0o644)).unwrap();
        assert_eq!(
            fs::metadata(&path).unwrap().permissions().mode() & 0o777,
            0o644
        );

        p.write_file("notes/foo.md", b"second").unwrap();

        let after = fs::metadata(&path).unwrap().permissions().mode() & 0o777;
        assert_eq!(
            after, 0o644,
            "overwrite must not downgrade existing-file permissions"
        );
    }

    /// #1077 Phase 0 (contract I1): nothing there means `None` — the
    /// caller keeps the requested spelling, which is how a CREATE
    /// stores the name the user chose (I2).
    #[test]
    fn canonical_path_of_a_missing_entry_is_none() {
        let (_tmp, p) = vault();
        assert_eq!(p.canonical_path("nope.md").unwrap(), None);
        assert_eq!(p.canonical_path("no/where.md").unwrap(), None);
    }

    /// #1077 Phase 0 (contract I1): the canonical spelling comes from
    /// the filesystem. Volume-dependent BY DESIGN — the aliasing
    /// assertions run only where the volume actually aliases (probed,
    /// never assumed), so this fact is true on NTFS/APFS and vacuous on
    /// ext4, and it never invents a normalization on either.
    #[test]
    fn canonical_path_returns_the_stored_spelling_on_an_aliasing_volume() {
        let (_tmp, p) = vault();
        p.write_file("Notes/Alpha.md", b"a").unwrap();
        p.write_file("\u{00c4}rger.md", b"b").unwrap(); // Ärger
        p.write_file("\u{65e5}\u{672c}.md", b"c").unwrap(); // 日本 — no case to fold

        // Exact spellings resolve to themselves everywhere.
        assert_eq!(
            p.canonical_path("Notes/Alpha.md").unwrap().as_deref(),
            Some("Notes/Alpha.md")
        );
        assert_eq!(
            p.canonical_path("\u{65e5}\u{672c}.md").unwrap().as_deref(),
            Some("\u{65e5}\u{672c}.md")
        );

        if p.stat("notes/alpha.md").is_err() {
            // Case-sensitive volume: the alias does not exist, and saying
            // so IS the per-volume truth — never invent (I1).
            assert_eq!(p.canonical_path("notes/alpha.md").unwrap(), None);
            assert_eq!(p.canonical_path("\u{00e4}rger.md").unwrap(), None);
            return;
        }

        // Case-insensitive volume: every alias resolves to the ONE stored
        // spelling — directory components included, and beyond ASCII
        // (the pair the old `lower()` gate could not see, finding 1).
        assert_eq!(
            p.canonical_path("notes/alpha.md").unwrap().as_deref(),
            Some("Notes/Alpha.md")
        );
        assert_eq!(
            p.canonical_path("NOTES/ALPHA.MD").unwrap().as_deref(),
            Some("Notes/Alpha.md")
        );
        assert_eq!(
            p.canonical_path("\u{00e4}rger.md").unwrap().as_deref(),
            Some("\u{00c4}rger.md")
        );

        // NFC vs NFD is a separate axis: NTFS keeps them distinct, APFS
        // unifies them. Either way the answer is the volume's, not ours.
        let nfc = "caf\u{00e9}.md";
        let nfd = "cafe\u{0301}.md";
        p.write_file(nfc, b"d").unwrap();
        let expected = if p.stat(nfd).is_ok() {
            Some(nfc.to_string())
        } else {
            None
        };
        assert_eq!(p.canonical_path(nfd).unwrap(), expected);
    }

    /// #1077 Phase 0: a link is never followed for identity — it keeps
    /// its own spelling (the W1 anchored-vault discipline; the link's
    /// entry is the thing being mutated).
    #[cfg(unix)]
    #[test]
    fn canonical_path_keeps_a_symlinks_own_spelling() {
        let (tmp, p) = vault();
        p.write_file("Target.md", b"t").unwrap();
        std::os::unix::fs::symlink(tmp.path().join("Target.md"), tmp.path().join("Link.md"))
            .unwrap();
        assert_eq!(
            p.canonical_path("Link.md").unwrap().as_deref(),
            Some("Link.md")
        );
    }

    /// #1077 (contract I4): the case-only rename — the destination IS the
    /// source under the volume's other spelling — is admissible for files
    /// and directories, and the directory entry carries the new spelling;
    /// a destination that is a DIFFERENT entry stays refused. The refusal
    /// is volume-probed (on a case-sensitive volume `b.md` beside `B.md`
    /// is simply a new name).
    #[test]
    fn rename_admits_the_sources_own_alias_and_refuses_any_other_occupant() {
        let (tmp, p) = vault();
        p.write_file("A.md", b"a").unwrap();
        p.write_file("B.md", b"b").unwrap();
        p.create_dir("Docs").unwrap();
        p.write_file("Docs/Note.md", b"n").unwrap();
        let aliasing = p.stat("a.md").is_ok();

        if aliasing {
            assert!(matches!(
                p.rename("A.md", "b.md"),
                Err(VaultError::DestinationExists { .. })
            ));
            assert!(matches!(
                p.preflight_rename("A.md", "b.md"),
                Err(VaultError::DestinationExists { .. })
            ));
        }
        p.preflight_rename("A.md", "a.md").unwrap();
        p.rename("A.md", "a.md").unwrap();
        p.rename("Docs", "docs").unwrap();

        let names: Vec<String> = std::fs::read_dir(tmp.path())
            .unwrap()
            .map(|entry| entry.unwrap().file_name().to_string_lossy().into_owned())
            .collect();
        assert!(names.contains(&"a.md".to_string()), "{names:?}");
        assert!(names.contains(&"docs".to_string()), "{names:?}");
        assert!(!names.contains(&"A.md".to_string()), "{names:?}");
        assert!(!names.contains(&"Docs".to_string()), "{names:?}");
        assert_eq!(p.read_file("docs/Note.md").unwrap(), b"n");
    }

    /// The kind of link these facts substitute for a vault folder, as the
    /// refusal names it: a symbolic link on Unix (#1449, #1452), a junction on
    /// Windows, which needs no privilege to make (#1451).
    #[cfg(unix)]
    const LINKED: &str = "a symbolic link";
    #[cfg(windows)]
    const LINKED: &str = "a junction";

    /// Put a link to the folder `target` at `at`, where nothing is: a
    /// symbolic link on Unix, a junction on Windows. `target` need not exist.
    #[cfg(any(unix, windows))]
    fn link_folder(at: &Path, target: &Path) {
        #[cfg(unix)]
        std::os::unix::fs::symlink(target, at).unwrap();
        #[cfg(windows)]
        {
            std::fs::create_dir(at).unwrap();
            crate::vault::test_support::junction(at, target);
        }
    }

    /// `"<folder>" is a junction` (Windows) or `"<folder>" is a symbolic
    /// link` (Unix): what a refusal of a linked folder contains.
    #[cfg(any(unix, windows))]
    fn linked(folder: &str) -> String {
        format!("\"{folder}\" is {LINKED}")
    }

    /// Every entry beneath `dir`, never following a link: the kind, and the
    /// bytes or the link target. Two equal snapshots mean nothing under `dir`
    /// was created, removed, moved or rewritten.
    #[cfg(any(unix, windows))]
    fn tree(dir: &Path) -> std::collections::BTreeMap<String, String> {
        fn visit(base: &Path, dir: &Path, out: &mut std::collections::BTreeMap<String, String>) {
            for entry in std::fs::read_dir(dir).unwrap() {
                let path = entry.unwrap().path();
                let name = path
                    .strip_prefix(base)
                    .unwrap()
                    .to_string_lossy()
                    .into_owned();
                let meta = path.symlink_metadata().unwrap();
                if meta.file_type().is_symlink() {
                    let target = std::fs::read_link(&path).unwrap();
                    out.insert(name, format!("link -> {}", target.display()));
                } else if meta.is_dir() {
                    out.insert(name, "folder".into());
                    visit(base, &path, out);
                } else {
                    let bytes = std::fs::read(&path).unwrap();
                    out.insert(name, format!("file {}", content_hash(&bytes)));
                }
            }
        }
        let mut out = std::collections::BTreeMap::new();
        visit(dir, dir, &mut out);
        out
    }

    /// #1449, probe P1b, and its Windows twin #1451: a vault folder replaced
    /// by a link to a directory outside the vault (a symbolic link on Unix, a
    /// junction on Windows), after Slate saw it, must not redirect a
    /// structural rename in either direction. Before the fix the
    /// absolute-path rename (`renamex_np`, `renameat2` or `MoveFileExW`)
    /// followed the link: the first rename pulled the outside file into the
    /// vault and the second pushed a vault file out of it.
    #[cfg(any(unix, windows))]
    #[test]
    fn rename_refuses_both_directions_through_a_folder_swapped_for_a_link() {
        let (tmp, p) = vault();
        let outside = tempfile::tempdir().unwrap();
        std::fs::write(outside.path().join("a.md"), b"outside a").unwrap();
        p.write_file("sub/a.md", b"vault a").unwrap();
        p.write_file("c.md", b"vault c").unwrap();
        swap_sub_for_a_link(tmp.path(), outside.path());
        let outside_before = tree(outside.path());
        let vault_before = tree(tmp.path());

        for (from, to, refused) in [
            ("sub/a.md", "b.md", "sub/a.md"),
            ("c.md", "sub/c.md", "sub/c.md"),
        ] {
            let error = p.rename(from, to).unwrap_err();
            assert!(
                matches!(
                    &error,
                    VaultError::InvalidPath { path, reason }
                        if path == refused
                            && reason.contains(&linked("sub"))
                            && reason.ends_with("so nothing was moved")
                ),
                "{from} -> {to}: {error:?}"
            );
        }

        assert_eq!(tree(outside.path()), outside_before, "outside changed");
        assert_eq!(tree(tmp.path()), vault_before, "vault changed");
    }

    /// #1449 (#1451 on Windows): a missing destination folder is created one
    /// component at a time beneath the vault root, never through a link.
    /// Before the fix `create_dir_all` followed `sub` and made `new/deeper`
    /// outside the vault.
    #[cfg(any(unix, windows))]
    #[test]
    fn rename_never_creates_destination_folders_through_a_linked_folder() {
        let (tmp, p) = vault();
        let outside = tempfile::tempdir().unwrap();
        p.write_file("c.md", b"vault c").unwrap();
        link_folder(&tmp.path().join("sub"), outside.path());
        let outside_before = tree(outside.path());
        let vault_before = tree(tmp.path());

        let error = p.rename("c.md", "sub/new/deeper/c.md").unwrap_err();

        assert!(
            matches!(&error, VaultError::InvalidPath { path, reason }
                if path == "sub/new/deeper/c.md" && reason.contains(&linked("sub"))),
            "{error:?}"
        );
        assert_eq!(tree(outside.path()), outside_before, "outside changed");
        assert_eq!(tree(tmp.path()), vault_before, "vault changed");
    }

    /// #1449 (#1451 on Windows) keeps `rename`'s creation of missing
    /// destination folders: each missing component is made inside the last
    /// pinned folder (`mkdirat`, or `NtCreateFile` with `FILE_CREATE` on
    /// Windows) and then held, so a move into folders that do not exist yet
    /// still works and they are real folders in the vault. An EXISTING
    /// ancestor on either side that is a link or a file still refuses, and a
    /// refused move creates no folder at all, even on the destination side of
    /// a refused source.
    #[cfg(any(unix, windows))]
    #[test]
    fn rename_into_missing_nested_folders_creates_them_beneath_the_root() {
        let (tmp, p) = vault();
        let outside = tempfile::tempdir().unwrap();
        std::fs::write(outside.path().join("x.md"), b"outside x").unwrap();
        p.write_file("a.md", b"vault a").unwrap();
        p.write_file("notes/plain.md", b"a file, not a folder")
            .unwrap();
        link_folder(&tmp.path().join("link"), outside.path());

        p.rename("a.md", "new/deeper/a.md").unwrap();

        for folder in ["new", "new/deeper"] {
            let meta = tmp.path().join(folder).symlink_metadata().unwrap();
            assert!(meta.is_dir() && !meta.file_type().is_symlink(), "{folder}");
        }
        assert_eq!(p.read_file("new/deeper/a.md").unwrap(), b"vault a");

        let outside_before = tree(outside.path());
        let vault_before = tree(tmp.path());
        let link = linked("link");
        for (from, to, refused, reason) in [
            (
                "new/deeper/a.md",
                "link/fresh/a.md",
                "link/fresh/a.md",
                link.as_str(),
            ),
            (
                "new/deeper/a.md",
                "notes/plain.md/fresh/a.md",
                "notes/plain.md/fresh/a.md",
                "\"notes/plain.md\" is not a folder",
            ),
            ("link/x.md", "fresh/x.md", "link/x.md", link.as_str()),
            (
                "notes/plain.md/x.md",
                "fresh/x.md",
                "notes/plain.md/x.md",
                "\"notes/plain.md\" is not a folder",
            ),
        ] {
            let error = p.rename(from, to).unwrap_err();
            assert!(
                matches!(&error, VaultError::InvalidPath { path, reason: why }
                    if path == refused && why.contains(reason)),
                "{from} -> {to}: {error:?}"
            );
        }
        assert_eq!(tree(outside.path()), outside_before, "outside changed");
        assert_eq!(
            tree(tmp.path()),
            vault_before,
            "a refused move created or moved something"
        );
    }

    /// `preflight_rename` still requires the destination folder to exist, as
    /// it did before #1449 (`require_mutation_parent_access` stats it). The
    /// batch planners only target indexed folders, and `rename` itself still
    /// creates a missing one (the fact above).
    #[test]
    fn preflight_rename_still_requires_an_existing_destination_folder() {
        let (tmp, p) = vault();
        p.write_file("a.md", b"vault a").unwrap();

        let error = p.preflight_rename("a.md", "new/deeper/a.md").unwrap_err();

        assert!(
            matches!(&error, VaultError::Io(error) if error.kind() == io::ErrorKind::NotFound),
            "{error:?}"
        );
        assert!(
            !tmp.path().join("new").exists(),
            "preflight creates nothing"
        );
    }

    /// #1449: the swap lands after every check, just before the kernel call.
    /// macOS renames from the root with `RENAME_NOFOLLOW_ANY`, so the call
    /// itself refuses (`ELOOP`) and the refusal names the folder. Linux renames
    /// inside the parents it pinned, so the vault's own folder (now parked) is
    /// used, as P1b's pinned-parent probe recorded. Neither reaches outside.
    #[cfg(any(target_os = "macos", target_os = "linux"))]
    #[test]
    fn a_folder_swapped_after_the_checks_cannot_redirect_the_kernel_rename() {
        for (from, to) in [("sub/a.md", "b.md"), ("c.md", "sub/c.md")] {
            let (tmp, p) = vault();
            let outside = tempfile::tempdir().unwrap();
            std::fs::write(outside.path().join("a.md"), b"outside a").unwrap();
            p.write_file("sub/a.md", b"vault a").unwrap();
            p.write_file("c.md", b"vault c").unwrap();
            let outside_before = tree(outside.path());
            let swap = || {
                std::fs::rename(tmp.path().join("sub"), tmp.path().join("parked"))?;
                std::os::unix::fs::symlink(outside.path(), tmp.path().join("sub"))
            };
            let from_path = p.resolve_for_mutation(from).unwrap();
            let to_path = p.resolve_for_mutation(to).unwrap();

            let outcome = p.rename_beneath_root(from, &from_path, to, &to_path, swap);

            assert_eq!(
                tree(outside.path()),
                outside_before,
                "{from} -> {to}: outside changed"
            );
            #[cfg(target_os = "macos")]
            {
                let error = outcome.unwrap_err();
                assert!(
                    matches!(&error, VaultError::InvalidPath { path, reason }
                        if (path == from || path == to)
                            && reason.contains("\"sub\" is a symbolic link")),
                    "{from} -> {to}: {error:?}"
                );
                assert_eq!(
                    std::fs::read(tmp.path().join("parked/a.md")).unwrap(),
                    b"vault a"
                );
                assert_eq!(std::fs::read(tmp.path().join("c.md")).unwrap(), b"vault c");
            }
            #[cfg(target_os = "linux")]
            {
                outcome.unwrap();
                if from == "sub/a.md" {
                    assert_eq!(std::fs::read(tmp.path().join("b.md")).unwrap(), b"vault a");
                } else {
                    assert_eq!(
                        std::fs::read(tmp.path().join("parked/c.md")).unwrap(),
                        b"vault c"
                    );
                }
            }
        }
    }

    /// #1451: on Windows every folder of both ends stays open, shared for
    /// reading and writing but not for deletion, from the walk until the
    /// rename, so an external writer cannot rename the pinned folder away
    /// and put a junction in its place in that moment (macOS refuses such a
    /// swap at the kernel call; Linux follows the parked folder): the swap
    /// itself fails, and the move lands in the vault's own folder.
    #[cfg(windows)]
    #[test]
    fn a_pinned_folder_cannot_be_swapped_for_a_junction_before_the_kernel_rename() {
        for (from, to, landed) in [
            ("sub/a.md", "b.md", "b.md"),
            ("c.md", "sub/c.md", "sub/c.md"),
        ] {
            let (tmp, p) = vault();
            let outside = tempfile::tempdir().unwrap();
            std::fs::write(outside.path().join("a.md"), b"outside a").unwrap();
            p.write_file("sub/a.md", b"vault a").unwrap();
            p.write_file("c.md", b"vault c").unwrap();
            let outside_before = tree(outside.path());
            let swapped = std::cell::RefCell::new(None);
            let from_path = p.resolve_for_mutation(from).unwrap();
            let to_path = p.resolve_for_mutation(to).unwrap();

            p.rename_beneath_root(from, &from_path, to, &to_path, || {
                *swapped.borrow_mut() = Some(std::fs::rename(
                    tmp.path().join("sub"),
                    tmp.path().join("parked"),
                ));
                Ok(())
            })
            .unwrap_or_else(|error| panic!("{from} -> {to}: {error:?}"));

            let swapped = swapped.into_inner().expect("the seam ran");
            assert!(
                swapped.is_err(),
                "{from} -> {to}: the pinned folder was renamed away"
            );
            assert!(!tmp.path().join("parked").exists(), "{from} -> {to}");
            assert_eq!(
                tree(outside.path()),
                outside_before,
                "{from} -> {to}: outside changed"
            );
            let bytes: &[u8] = if from == "c.md" {
                b"vault c"
            } else {
                b"vault a"
            };
            assert_eq!(std::fs::read(tmp.path().join(landed)).unwrap(), bytes);
        }
    }

    /// #1451: sharing does not stop an EMPTY pinned folder from being
    /// converted into a junction in place, through a handle that holds only
    /// attribute access. The rename into it then fails instead of following
    /// the junction, and the failure is the typed refusal naming the folder:
    /// nothing moves and nothing outside changes.
    #[cfg(windows)]
    #[test]
    fn a_pinned_folder_converted_into_a_junction_before_the_kernel_rename_refuses() {
        let (tmp, p) = vault();
        let outside = tempfile::tempdir().unwrap();
        p.write_file("c.md", b"vault c").unwrap();
        p.create_dir("empty").unwrap();
        let outside_before = tree(outside.path());
        let from_path = p.resolve_for_mutation("c.md").unwrap();
        let to_path = p.resolve_for_mutation("empty/c.md").unwrap();

        let error = p
            .rename_beneath_root("c.md", &from_path, "empty/c.md", &to_path, || {
                crate::vault::test_support::junction(&tmp.path().join("empty"), outside.path());
                Ok(())
            })
            .unwrap_err();

        let refusal = "\"empty\" is a junction, not a folder in this vault, so nothing was moved";
        assert!(
            matches!(&error, VaultError::InvalidPath { path, reason }
                if path == "empty/c.md" && reason == refusal),
            "{error:?}"
        );
        assert_eq!(tree(outside.path()), outside_before, "outside changed");
        assert_eq!(std::fs::read(tmp.path().join("c.md")).unwrap(), b"vault c");
        std::fs::remove_dir(tmp.path().join("empty")).unwrap();
    }

    /// #1451's sharing decision: a forward move opens its source shared for
    /// deletion, as `MoveFileExW` did, so another process holding the entry
    /// open for deletion (and sharing deletion) does not make the move fail.
    /// The identity inverse keeps refusing that handle
    /// (`windows_entry`'s `conditional_rename_source_handle_cannot_target_path_replacement`).
    #[cfg(windows)]
    #[test]
    fn a_forward_move_shares_deletion_with_another_holder_as_move_file_ex_did() {
        use std::os::windows::fs::OpenOptionsExt as _;
        use windows_sys::Win32::Storage::FileSystem::{
            DELETE, FILE_READ_ATTRIBUTES, FILE_SHARE_DELETE, FILE_SHARE_READ, FILE_SHARE_WRITE,
        };

        let (tmp, p) = vault();
        p.write_file("a.md", b"held").unwrap();
        let holder = std::fs::OpenOptions::new()
            .access_mode(FILE_READ_ATTRIBUTES | DELETE)
            .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE)
            .open(tmp.path().join("a.md"))
            .unwrap();

        p.rename("a.md", "moved/a.md").unwrap();

        drop(holder);
        assert_eq!(
            std::fs::read(tmp.path().join("moved/a.md")).unwrap(),
            b"held"
        );
        assert!(!tmp.path().join("a.md").exists());
    }

    /// #1449 (#1451 on Windows): the batch preflight refuses the same
    /// substituted ancestors, so a batch is rejected before its first item
    /// moves.
    #[cfg(any(unix, windows))]
    #[test]
    fn preflight_rename_refuses_a_linked_folder_on_either_side() {
        let (tmp, p) = vault();
        let outside = tempfile::tempdir().unwrap();
        std::fs::write(outside.path().join("a.md"), b"outside a").unwrap();
        p.write_file("c.md", b"vault c").unwrap();
        link_folder(&tmp.path().join("sub"), outside.path());

        for (from, to, refused) in [
            ("sub/a.md", "b.md", "sub/a.md"),
            ("c.md", "sub/c.md", "sub/c.md"),
        ] {
            let error = p.preflight_rename(from, to).unwrap_err();
            assert!(
                matches!(&error, VaultError::InvalidPath { path, reason }
                    if path == refused && reason.contains(&linked("sub"))),
                "{from} -> {to}: {error:?}"
            );
        }
    }

    /// #1449: a folder deeper in the path that is not a folder at all refuses
    /// with its own reason, naming the vault-relative prefix.
    #[test]
    fn rename_names_a_nested_ancestor_that_is_not_a_folder() {
        let (_tmp, p) = vault();
        p.write_file("c.md", b"vault c").unwrap();
        p.write_file("notes/plain.md", b"a file, not a folder")
            .unwrap();

        let error = p.rename("c.md", "notes/plain.md/c.md").unwrap_err();

        assert!(
            matches!(&error, VaultError::InvalidPath { path, reason }
                if path == "notes/plain.md/c.md"
                    && reason.contains("\"notes/plain.md\" is not a folder")),
            "{error:?}"
        );
        assert_eq!(p.read_file("c.md").unwrap(), b"vault c");
    }

    /// #1449: the root is the descriptor, not a path component, so a vault
    /// opened through a symlinked root keeps renaming files and folders.
    /// #1451: on Windows the root handle follows the root's own junction the
    /// same way, and a case-only rename and a folder move still work.
    #[cfg(any(unix, windows))]
    #[test]
    fn rename_through_a_linked_vault_root_still_moves() {
        let tmp = tempfile::tempdir().unwrap();
        let real = tmp.path().join("real-vault");
        std::fs::create_dir(&real).unwrap();
        let link = tmp.path().join("vault-link");
        link_folder(&link, &real);
        let p = FsVaultProvider::new(link);
        p.write_file("a.md", b"a").unwrap();
        p.create_dir("sub").unwrap();

        p.rename("a.md", "sub/a.md").unwrap();
        p.rename("sub/a.md", "b.md").unwrap();
        p.rename("sub", "moved/sub").unwrap();
        p.preflight_rename("b.md", "moved/sub/b.md").unwrap();

        assert_eq!(std::fs::read(real.join("b.md")).unwrap(), b"a");
        assert!(real.join("moved/sub").is_dir());
        assert!(!real.join("sub").exists());

        // A case-only rename of a folder beneath the linked root: the stored
        // spelling changes (a plain new name on a case-sensitive volume).
        p.rename("moved", "Moved").unwrap();
        let names: Vec<String> = std::fs::read_dir(&real)
            .unwrap()
            .map(|entry| entry.unwrap().file_name().to_string_lossy().into_owned())
            .collect();
        assert!(names.contains(&"Moved".to_string()), "{names:?}");
        assert!(!names.contains(&"moved".to_string()), "{names:?}");
        assert!(real.join("Moved/sub").is_dir());
    }

    /// #1449 must keep matrix §6 (#1430): a normalization-only rename
    /// (`café` NFC → NFD) stores NFD where the volume accepts it as the
    /// same entry or as a new name, and stays a no-op on a volume that
    /// aliases normalization but reports itself case-sensitive.
    #[test]
    fn rename_keeps_normalization_only_renames_as_matrix_section_6_records() {
        let (tmp, p) = vault();
        let nfc = "caf\u{00e9}.md";
        let nfd = "cafe\u{0301}.md";
        p.write_file(nfc, b"cafe").unwrap();
        p.write_file("Probe.md", b"case probe").unwrap();
        let aliases_normalization = p.stat(nfd).is_ok();
        let case_insensitive = p.stat("probe.md").is_ok();

        p.rename(nfc, nfd).unwrap();

        let names: Vec<String> = std::fs::read_dir(tmp.path())
            .unwrap()
            .map(|entry| entry.unwrap().file_name().to_string_lossy().into_owned())
            .filter(|name| name.starts_with("caf"))
            .collect();
        let expected = if aliases_normalization && !case_insensitive {
            nfc
        } else {
            nfd
        };
        assert_eq!(names, vec![expected.to_string()]);
        assert_eq!(p.read_file(nfd).unwrap(), b"cafe");
    }

    /// #1452: the typed refusal a save or a creation gives for a folder on its
    /// path that is a symbolic link (a junction on Windows: #1451) or not a
    /// folder: `InvalidPath` for the caller's path, naming the folder, ending
    /// with what did not happen.
    #[cfg(any(unix, windows))]
    fn assert_refused(error: &VaultError, refused: &str, names: &str, outcome: &str) {
        assert!(
            matches!(error, VaultError::InvalidPath { path, reason }
                if path == refused && reason.contains(names) && reason.ends_with(outcome)),
            "expected the refusal of {refused:?} naming {names:?} ({outcome:?}), got {error:?}"
        );
    }

    /// `"sub" is a junction, not a folder in this vault` on Windows, `"sub"
    /// is a symbolic link, …` on Unix: the start of `sub`'s refusal.
    #[cfg(any(unix, windows))]
    fn linked_sub() -> String {
        format!("{}, not a folder in this vault", linked("sub"))
    }

    /// The vault's own `sub` folder, parked inside the vault, and a link to
    /// `outside` in its place (a symbolic link on Unix, a junction on
    /// Windows): the external writer of #1449, #1452 and #1451.
    #[cfg(any(unix, windows))]
    fn swap_sub_for_a_link(vault: &Path, outside: &Path) {
        std::fs::rename(vault.join("sub"), vault.join("parked")).unwrap();
        link_folder(&vault.join("sub"), outside);
    }

    /// #1452 (#1451 on Windows): a vault folder replaced by a link to a
    /// directory outside the vault, after Slate saw it, must not redirect a
    /// save. Before the fix `write_file` made the parent with
    /// `create_dir_all` and published with a path-addressed rename (on
    /// Windows `tempfile`'s `persist`, `MoveFileExW`), and both followed the
    /// link: the save replaced a same-named file outside the vault, or made a
    /// new one there, and a missing folder was made outside too, while the
    /// save reported success.
    #[cfg(any(unix, windows))]
    #[test]
    fn write_file_refuses_a_save_through_a_folder_swapped_for_a_link() {
        let (tmp, p) = vault();
        let outside = tempfile::tempdir().unwrap();
        std::fs::write(outside.path().join("note.md"), b"outside note").unwrap();
        p.write_file("sub/note.md", b"vault note").unwrap();
        swap_sub_for_a_link(tmp.path(), outside.path());
        let outside_before = tree(outside.path());
        let vault_before = tree(tmp.path());

        // An overwrite of a name the link's target holds, a new name, and a
        // new name in a folder that does not exist yet.
        for relative in ["sub/note.md", "sub/new-note.md", "sub/new/deeper.md"] {
            let error = p.write_file(relative, b"saved bytes").unwrap_err();
            assert_refused(&error, relative, &linked_sub(), "so nothing was saved");
        }

        assert_eq!(tree(outside.path()), outside_before, "outside changed");
        assert_eq!(tree(tmp.path()), vault_before, "vault changed");
    }

    /// #1452 (#1451 on Windows): `create_dir` (`create_folder`'s only route to
    /// disk) made the folder with `create_dir_all` from the absolute path, so
    /// a folder swapped for a link put the new folder outside the vault.
    #[cfg(any(unix, windows))]
    #[test]
    fn create_dir_refuses_a_new_folder_through_a_folder_swapped_for_a_link() {
        let (tmp, p) = vault();
        let outside = tempfile::tempdir().unwrap();
        p.create_dir("sub").unwrap();
        swap_sub_for_a_link(tmp.path(), outside.path());
        let outside_before = tree(outside.path());
        let vault_before = tree(tmp.path());

        for relative in ["sub/new-folder", "sub/new/deeper"] {
            let error = p.create_dir(relative).unwrap_err();
            assert_refused(&error, relative, &linked_sub(), "so no folder was created");
        }

        assert_eq!(tree(outside.path()), outside_before, "outside changed");
        assert_eq!(tree(tmp.path()), vault_before, "vault changed");
    }

    /// #1452: the exclusive creates already refused a symlinked ancestor,
    /// because they pin the parent with the no-follow walk, but reported the
    /// kernel's `ENOTDIR` as an untyped IO error ("Not a directory (os error
    /// 20)"). They now give the typed refusal, and a refused create changes
    /// nothing, not even the staging folder. #1451: on Windows the publish
    /// (`MoveFileExW(staged, path, 0)`) and `fs::create_dir` followed a
    /// junction; they refuse the same way.
    #[cfg(any(unix, windows))]
    #[test]
    fn exclusive_creates_through_a_linked_folder_give_the_typed_refusal() {
        let (tmp, p) = vault();
        let outside = tempfile::tempdir().unwrap();
        link_folder(&tmp.path().join("sub"), outside.path());
        let vault_before = tree(tmp.path());

        let create = p.write_file_if_absent("sub/new.md", b"new").unwrap_err();
        assert_refused(
            &create,
            "sub/new.md",
            &linked_sub(),
            "so nothing was created",
        );
        let folder = p.create_dir_if_absent("sub/new").unwrap_err();
        assert_refused(
            &folder,
            "sub/new",
            &linked_sub(),
            "so no folder was created",
        );

        assert!(tree(outside.path()).is_empty(), "outside changed");
        assert_eq!(tree(tmp.path()), vault_before, "vault changed");
    }

    /// #1452: a folder on the path that is a file refuses every save and
    /// creation with its own reason, naming the vault-relative prefix, where
    /// each used to fail with an untyped IO error.
    #[cfg(any(unix, windows))]
    #[test]
    fn saves_and_creations_name_an_ancestor_that_is_not_a_folder() {
        let (tmp, p) = vault();
        p.write_file("notes/plain.md", b"a file, not a folder")
            .unwrap();
        let vault_before = tree(tmp.path());
        let names = "\"notes/plain.md\" is not a folder";

        let save = p.write_file("notes/plain.md/x.md", b"x").unwrap_err();
        assert_refused(&save, "notes/plain.md/x.md", names, "so nothing was saved");
        let folder = p.create_dir("notes/plain.md/x/y").unwrap_err();
        assert_refused(
            &folder,
            "notes/plain.md/x/y",
            names,
            "so no folder was created",
        );
        let create = p
            .write_file_if_absent("notes/plain.md/x.md", b"x")
            .unwrap_err();
        assert_refused(
            &create,
            "notes/plain.md/x.md",
            names,
            "so nothing was created",
        );
        let exclusive = p.create_dir_if_absent("notes/plain.md/x").unwrap_err();
        assert_refused(
            &exclusive,
            "notes/plain.md/x",
            names,
            "so no folder was created",
        );

        assert_eq!(tree(tmp.path()), vault_before, "vault changed");
    }

    /// #1452: a folder's own name that is a symbolic link (to a folder or to
    /// nothing), or a file, is not a folder in this vault. Before the fix
    /// `create_dir` followed a link to a folder and reported success, so
    /// `create_folder` recorded a folder (whose undo would trash the link),
    /// and a file was an untyped `AlreadyExists`. Each is now the typed
    /// refusal, naming the entry, and nothing is followed or created. #1451:
    /// on Windows `create_dir_all` followed a junction the same way; a
    /// junction there, live or dangling, refuses too.
    #[cfg(any(unix, windows))]
    #[test]
    fn create_dir_refuses_a_final_component_that_is_a_link_or_a_file() {
        let (tmp, p) = vault();
        let outside = tempfile::tempdir().unwrap();
        link_folder(&tmp.path().join("link"), outside.path());
        link_folder(&tmp.path().join("dangling"), &tmp.path().join("missing"));
        p.write_file("plain.md", b"a file").unwrap();
        let vault_before = tree(tmp.path());

        for (relative, names) in [
            ("link", linked("link")),
            ("dangling", linked("dangling")),
            ("plain.md", "\"plain.md\" is not a folder".to_string()),
        ] {
            let error = p.create_dir(relative).unwrap_err();
            assert_refused(&error, relative, &names, "so no folder was created");
        }

        assert!(tree(outside.path()).is_empty(), "outside changed");
        assert_eq!(tree(tmp.path()), vault_before, "vault changed");
    }

    /// #1452 keeps what a save and a new folder do in real folders: missing
    /// folders are made, each inside its pinned parent (`mkdirat`, or
    /// `NtCreateFile` with `FILE_CREATE` on Windows: #1451), and are real
    /// folders in the vault; an overwrite replaces the bytes; and
    /// `create_dir` stays idempotent, an existing folder being `Ok` (on Unix
    /// even when it cannot be searched: the look at it does not open it).
    #[cfg(any(unix, windows))]
    #[test]
    fn saves_and_new_folders_still_make_missing_folders_beneath_the_root() {
        let (tmp, p) = vault();
        p.write_file("a/b/c.md", b"deep").unwrap();
        p.write_file("a/b/c.md", b"deeper").unwrap();
        p.create_dir("x/y/z").unwrap();
        p.create_dir("x/y/z").unwrap();
        p.create_dir("a/b").unwrap();
        p.write_file_if_absent("x/y/fresh.md", b"fresh").unwrap();
        p.create_dir_if_absent("x/y/fresh").unwrap();
        #[cfg(unix)]
        {
            use std::os::unix::fs::PermissionsExt as _;
            let locked = tmp.path().join("locked");
            std::fs::create_dir(&locked).unwrap();
            std::fs::set_permissions(&locked, fs::Permissions::from_mode(0o000)).unwrap();
            let idempotent = p.create_dir("locked");
            std::fs::set_permissions(&locked, fs::Permissions::from_mode(0o700)).unwrap();
            idempotent.expect("an existing folder is Ok, whatever its permissions");
        }

        for folder in ["a", "a/b", "x", "x/y", "x/y/z", "x/y/fresh"] {
            let meta = tmp.path().join(folder).symlink_metadata().unwrap();
            assert!(meta.is_dir() && !meta.file_type().is_symlink(), "{folder}");
        }
        assert_eq!(p.read_file("a/b/c.md").unwrap(), b"deeper");
        assert_eq!(p.read_file("x/y/fresh.md").unwrap(), b"fresh");
        assert_eq!(
            std::fs::read_dir(tmp.path().join(".slate/tmp"))
                .unwrap()
                .count(),
            0,
            "no staged file is left behind"
        );
    }

    /// `.slate/tmp` holds no staged file: every save either published its
    /// staged file or removed it.
    #[cfg(any(unix, windows))]
    fn assert_no_staged_file(vault: &Path) {
        let staged: Vec<_> = std::fs::read_dir(vault.join(".slate/tmp"))
            .unwrap()
            .map(|entry| entry.unwrap().file_name())
            .collect();
        assert!(staged.is_empty(), "staged files left behind: {staged:?}");
    }

    /// #1452's race, for a save: the folder is swapped for a link to a
    /// directory outside the vault after it is pinned and the bytes are
    /// staged, immediately before the publish's `renameat`. The publish is
    /// relative to the pinned descriptor, so the save lands in the vault's
    /// own folder (now parked), never through the link, for an overwrite of
    /// a name the link's target also holds, a new name, and a name in a
    /// folder the walk has just made. Nothing outside changes and no staged
    /// file is left. (#1449 records this pinned-parent moment for Linux
    /// renames; for saves it holds on macOS and Linux alike.)
    #[cfg(unix)]
    #[test]
    fn a_folder_swapped_just_before_the_publish_cannot_redirect_a_save() {
        for (relative, parked) in [
            ("sub/note.md", "parked/note.md"),
            ("sub/new-note.md", "parked/new-note.md"),
            ("sub/new/deeper.md", "parked/new/deeper.md"),
        ] {
            let (tmp, p) = vault();
            let outside = tempfile::tempdir().unwrap();
            std::fs::write(outside.path().join("note.md"), b"outside note").unwrap();
            p.write_file("sub/note.md", b"vault note").unwrap();
            let outside_before = tree(outside.path());

            p.write_file_beneath_root(relative, b"saved bytes", || {
                swap_sub_for_a_link(tmp.path(), outside.path());
                Ok(())
            })
            .unwrap_or_else(|error| panic!("{relative}: {error:?}"));

            assert_eq!(
                tree(outside.path()),
                outside_before,
                "{relative}: outside changed"
            );
            assert_eq!(
                std::fs::read(tmp.path().join(parked)).unwrap(),
                b"saved bytes",
                "{relative}"
            );
            assert_no_staged_file(tmp.path());
        }
    }

    /// #1452's race, for a new folder: the same swap, immediately before the
    /// `mkdirat`. The folder is made in the pinned descriptor, so it lands in
    /// the vault's own folder (now parked), whether its parent was there or
    /// the walk has just made it, and nothing appears outside.
    #[cfg(unix)]
    #[test]
    fn a_folder_swapped_just_before_the_mkdirat_cannot_redirect_a_new_folder() {
        for (relative, parked) in [
            ("sub/new-folder", "parked/new-folder"),
            ("sub/new/deeper", "parked/new/deeper"),
        ] {
            let (tmp, p) = vault();
            let outside = tempfile::tempdir().unwrap();
            p.create_dir("sub").unwrap();

            p.create_dir_beneath_root(relative, || {
                swap_sub_for_a_link(tmp.path(), outside.path());
                Ok(())
            })
            .unwrap_or_else(|error| panic!("{relative}: {error:?}"));

            assert!(
                tree(outside.path()).is_empty(),
                "{relative}: outside changed"
            );
            let meta = tmp.path().join(parked).symlink_metadata().unwrap();
            assert!(
                meta.is_dir() && !meta.file_type().is_symlink(),
                "{relative}"
            );
        }
    }

    /// #1452's race, where the pinned folder itself goes: the folder the walk
    /// has just made is removed, and a link to a directory outside the vault
    /// put in its place, immediately before the publish or the `mkdirat`. The
    /// pinned descriptor then names a removed folder, so the call fails with
    /// the kernel's own error, `ENOENT` as `Io(NotFound)`, not flattened into
    /// a refusal, and nothing is written anywhere: not through the link, and
    /// no staged file is left.
    #[cfg(unix)]
    #[test]
    fn a_pinned_folder_removed_just_before_the_publish_fails_with_its_errno() {
        fn swap_fresh(vault: &Path, outside: &Path) -> io::Result<()> {
            std::fs::remove_dir(vault.join("fresh"))?;
            std::os::unix::fs::symlink(outside, vault.join("fresh"))
        }
        fn assert_enoent(relative: &str, error: &VaultError) {
            assert!(
                matches!(error, VaultError::Io(io)
                    if io.kind() == io::ErrorKind::NotFound
                        && io.raw_os_error() == Some(libc::ENOENT)),
                "{relative}: {error:?}"
            );
        }

        let (tmp, p) = vault();
        let outside = tempfile::tempdir().unwrap();
        let save = p
            .write_file_beneath_root("fresh/x.md", b"saved bytes", || {
                swap_fresh(tmp.path(), outside.path())
            })
            .unwrap_err();
        assert_enoent("fresh/x.md", &save);
        assert!(tree(outside.path()).is_empty(), "the save wrote outside");
        assert_no_staged_file(tmp.path());

        let (tmp, p) = vault();
        let outside = tempfile::tempdir().unwrap();
        let folder = p
            .create_dir_beneath_root("fresh/new", || swap_fresh(tmp.path(), outside.path()))
            .unwrap_err();
        assert_enoent("fresh/new", &folder);
        assert!(
            tree(outside.path()).is_empty(),
            "the folder was made outside"
        );
    }

    /// The other failures keep the kernel's error (#1452): only a folder on
    /// the path that is a link or not a folder becomes the typed refusal. A
    /// save and a new folder in a folder Slate may not write keep `EACCES` as
    /// `Io(PermissionDenied)`, and the detail the hosts show still carries its
    /// `os error` number. The refused save leaves no staged file.
    #[cfg(unix)]
    #[test]
    fn failures_other_than_the_boundary_keep_their_io_error() {
        use std::os::unix::fs::PermissionsExt as _;

        let (tmp, p) = vault();
        p.create_dir("locked").unwrap();
        let locked = tmp.path().join("locked");
        std::fs::set_permissions(&locked, fs::Permissions::from_mode(0o500)).unwrap();
        let save = p.write_file("locked/x.md", b"x");
        let folder = p.create_dir("locked/new");
        // Restore owner access before asserting, so TempDir cleanup stays
        // reliable even when the fact fails.
        std::fs::set_permissions(&locked, fs::Permissions::from_mode(0o700)).unwrap();

        for (call, outcome) in [("write_file", save), ("create_dir", folder)] {
            let error = outcome.unwrap_err();
            assert!(
                matches!(&error, VaultError::Io(io)
                    if io.kind() == io::ErrorKind::PermissionDenied
                        && io.raw_os_error() == Some(libc::EACCES)),
                "{call}: {error:?}"
            );
            let detail = crate::a11y::vault_error_detail(&error);
            assert!(
                detail.contains(&format!("(os error {})", libc::EACCES)),
                "{call}: {detail}"
            );
        }
        assert!(
            std::fs::read_dir(&locked).unwrap().next().is_none(),
            "something was written in the locked folder"
        );
        assert_no_staged_file(tmp.path());
    }

    /// #1452: the root is the descriptor, not a path component, so a vault
    /// opened through a symlinked root keeps saving and creating folders.
    /// #1451: on Windows the root handle follows the root's junction.
    #[cfg(any(unix, windows))]
    #[test]
    fn saves_and_new_folders_through_a_linked_vault_root_still_land() {
        let tmp = tempfile::tempdir().unwrap();
        let real = tmp.path().join("real-vault");
        std::fs::create_dir(&real).unwrap();
        let link = tmp.path().join("vault-link");
        link_folder(&link, &real);
        let p = FsVaultProvider::new(link);

        p.write_file("a.md", b"a").unwrap();
        p.write_file("a.md", b"a again").unwrap();
        p.write_file("n/b.md", b"b").unwrap();
        p.create_dir("f/g").unwrap();
        p.write_file_if_absent("c.md", b"c").unwrap();
        p.create_dir_if_absent("h").unwrap();

        assert_eq!(std::fs::read(real.join("a.md")).unwrap(), b"a again");
        assert_eq!(std::fs::read(real.join("n/b.md")).unwrap(), b"b");
        assert_eq!(std::fs::read(real.join("c.md")).unwrap(), b"c");
        assert!(real.join("f/g").is_dir() && real.join("h").is_dir());
    }

    /// #1451: on Windows a pinned folder stays open, unshared for deletion,
    /// from the walk until the publish or the create, so an external writer
    /// cannot rename it away and put a junction in its place in that moment:
    /// the swap fails, and the save or the new folder lands in the vault's
    /// own folder, for an overwrite, a new name, and a folder the walk has
    /// just made. (On Unix the same swap succeeds and the call lands in the
    /// parked folder: `a_folder_swapped_just_before_the_publish_…`.)
    #[cfg(windows)]
    #[test]
    fn a_pinned_folder_cannot_be_swapped_for_a_junction_before_a_save_or_a_create() {
        for relative in ["sub/note.md", "sub/new-note.md", "sub/new/deeper.md"] {
            let (tmp, p) = vault();
            let outside = tempfile::tempdir().unwrap();
            std::fs::write(outside.path().join("note.md"), b"outside note").unwrap();
            p.write_file("sub/note.md", b"vault note").unwrap();
            let outside_before = tree(outside.path());
            let mut swapped = None;

            p.write_file_beneath_root(relative, b"saved bytes", || {
                swapped = Some(std::fs::rename(
                    tmp.path().join("sub"),
                    tmp.path().join("parked"),
                ));
                Ok(())
            })
            .unwrap_or_else(|error| panic!("{relative}: {error:?}"));

            assert!(
                swapped.expect("the seam ran").is_err(),
                "{relative}: the pinned folder was renamed away"
            );
            assert_eq!(
                tree(outside.path()),
                outside_before,
                "{relative}: outside changed"
            );
            assert_eq!(
                std::fs::read(tmp.path().join(relative)).unwrap(),
                b"saved bytes",
                "{relative}"
            );
            assert_no_staged_file(tmp.path());
        }

        for relative in ["sub/new-folder", "sub/new/deeper"] {
            let (tmp, p) = vault();
            let outside = tempfile::tempdir().unwrap();
            p.create_dir("sub").unwrap();
            let mut swapped = None;

            p.create_dir_beneath_root(relative, || {
                swapped = Some(std::fs::rename(
                    tmp.path().join("sub"),
                    tmp.path().join("parked"),
                ));
                Ok(())
            })
            .unwrap_or_else(|error| panic!("{relative}: {error:?}"));

            assert!(swapped.expect("the seam ran").is_err(), "{relative}");
            assert!(
                tree(outside.path()).is_empty(),
                "{relative}: outside changed"
            );
            let meta = tmp.path().join(relative).symlink_metadata().unwrap();
            assert!(
                meta.is_dir() && !meta.file_type().is_symlink(),
                "{relative}"
            );
        }
    }

    /// #1451: an EMPTY pinned folder (here one the walk has just made) can
    /// still be converted into a junction in place after the walk, because
    /// attribute-only access passes every sharing check. The publish into it
    /// is the same handle rename the identity inverse uses, which then fails
    /// instead of following the junction
    /// (`conditional_rename_destination_junction_conversion_refuses_without_escape`),
    /// and the failure is the typed refusal naming the folder: nothing is
    /// written anywhere, and no staged file is left.
    #[cfg(windows)]
    #[test]
    fn a_pinned_folder_converted_into_a_junction_before_the_publish_refuses() {
        let (tmp, p) = vault();
        let outside = tempfile::tempdir().unwrap();

        let save = p
            .write_file_beneath_root("fresh/x.md", b"saved bytes", || {
                crate::vault::test_support::junction(&tmp.path().join("fresh"), outside.path());
                Ok(())
            })
            .unwrap_err();

        let refusal = "\"fresh\" is a junction, not a folder in this vault, so nothing was saved";
        assert!(
            matches!(&save, VaultError::InvalidPath { path, reason }
                if path == "fresh/x.md" && reason == refusal),
            "{save:?}"
        );
        assert!(tree(outside.path()).is_empty(), "the save wrote outside");
        assert_no_staged_file(tmp.path());
        std::fs::remove_dir(tmp.path().join("fresh")).unwrap();
    }

    /// #1451: a Windows save or new note publishes its staged file with its
    /// attributes reset to `FILE_ATTRIBUTE_NORMAL`, as `tempfile`'s `persist`
    /// did for a save: `tempfile` stages it `FILE_ATTRIBUTE_TEMPORARY`, which
    /// must not stay on a note.
    #[cfg(windows)]
    #[test]
    fn saved_and_created_notes_are_not_left_temporary() {
        use std::os::windows::fs::MetadataExt as _;
        use windows_sys::Win32::Storage::FileSystem::FILE_ATTRIBUTE_TEMPORARY;

        let (tmp, p) = vault();
        p.write_file("saved.md", b"saved").unwrap();
        p.write_file("saved.md", b"saved again").unwrap();
        p.write_file_if_absent("created.md", b"created").unwrap();

        for name in ["saved.md", "created.md"] {
            let attributes = std::fs::metadata(tmp.path().join(name))
                .unwrap()
                .file_attributes();
            assert_eq!(
                attributes & FILE_ATTRIBUTE_TEMPORARY,
                0,
                "{name}: {attributes:#x}"
            );
        }
        assert_eq!(p.read_file("saved.md").unwrap(), b"saved again");
        assert_no_staged_file(tmp.path());
    }

    /// The final component is still a name (#1449, #1452): a save to a path
    /// that is itself a symbolic link replaces the link with the saved file,
    /// as `write_file` always has (the vault module's symlink decisions), and
    /// never writes through it. The file keeps the permissions of what the
    /// link pointed at, as an overwrite keeps a regular file's own.
    #[cfg(unix)]
    #[test]
    fn a_save_to_a_link_replaces_the_link_and_never_writes_through_it() {
        use std::os::unix::fs::PermissionsExt as _;

        let (tmp, p) = vault();
        let outside = tempfile::tempdir().unwrap();
        let target = outside.path().join("target.md");
        std::fs::write(&target, b"outside target").unwrap();
        std::fs::set_permissions(&target, fs::Permissions::from_mode(0o640)).unwrap();
        std::os::unix::fs::symlink(&target, tmp.path().join("link.md")).unwrap();

        p.write_file("link.md", b"saved").unwrap();

        let meta = tmp.path().join("link.md").symlink_metadata().unwrap();
        assert!(meta.is_file(), "the link was not replaced: {meta:?}");
        assert_eq!(meta.permissions().mode() & 0o777, 0o640);
        assert_eq!(std::fs::read(tmp.path().join("link.md")).unwrap(), b"saved");
        assert_eq!(std::fs::read(&target).unwrap(), b"outside target");
    }

    /// W4-3's durable publication (contract 21) survives #1452: the folder a
    /// save publishes into is synced after the rename. The pinned descriptor
    /// is search-only (`O_SEARCH` on macOS, `O_PATH` on Linux, which cannot be
    /// synced), so the folder is reopened through it; a sync that failed every
    /// time would lose the guarantee without a visible error, since the save
    /// treats the sync as a best effort.
    #[cfg(unix)]
    #[test]
    fn the_folder_a_save_publishes_into_can_be_synced() {
        let (_tmp, p) = vault();
        p.write_file("notes/a.md", b"a").unwrap();

        for relative in ["notes/a.md", "top.md", "notes/new.md"] {
            p.pin_mutation_target(relative, false, Mutation::Save)
                .unwrap()
                .sync_parent()
                .unwrap_or_else(|error| panic!("{relative}: {error}"));
        }
    }
}
