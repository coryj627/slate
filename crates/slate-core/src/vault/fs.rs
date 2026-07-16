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
use std::sync::Arc;
use std::time::UNIX_EPOCH;
use std::{fs, io};

use crate::VaultError;

use super::provider::{DirEntry, EntryKind, FileEventSink, FileStat, VaultProvider, WatchHandle};

/// A vault provider backed by an on-disk directory.
#[derive(Debug)]
pub struct FsVaultProvider {
    root: PathBuf,
}

impl FsVaultProvider {
    /// Wrap an on-disk directory as a vault provider.
    ///
    /// The directory does not need to exist yet; subsequent operations
    /// will fail with `VaultError::Io` for non-existent paths. This is
    /// intentional — the provider doesn't enforce vault-existence
    /// semantics, the calling `VaultSession::open` does.
    pub fn new(root: PathBuf) -> Self {
        Self { root }
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

    /// Directory where `atomic_write` parks tempfiles. Lives under the
    /// vault's hidden `.slate/` subtree so a crash-leak doesn't surface
    /// as a visible entry and concurrent `list_dir` callers can't see
    /// half-written files alongside their targets.
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
        Publish: FnOnce(&Path, &Path) -> io::Result<()>,
    {
        let path = self.resolve_for_mutation(relative)?;
        let tmp_dir = self.tmp_dir();
        fs::create_dir_all(&tmp_dir).map_err(VaultError::Io)?;
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent).map_err(VaultError::Io)?;
        }

        // A managed temp path is the failure guard: every exit before publish,
        // including partial writes and sync failures, still unlinks the staged
        // bytes. Keep the `create-` prefix so leaked-file diagnostics remain
        // recognizable if the filesystem itself refuses cleanup.
        let mut temp = tempfile::Builder::new()
            .prefix("create-")
            .tempfile_in(&tmp_dir)
            .map_err(VaultError::Io)?;

        let staged = write_contents(temp.as_file_mut(), contents)
            .and_then(|()| sync_contents(temp.as_file()));
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
        match publish(temp.path(), &path) {
            Ok(()) => {
                let (file, mut path_guard) = temp.into_parts();
                path_guard.disable_cleanup(true);
                drop(path_guard);
                drop(file);
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
                    Ok(()) => Err(VaultError::Io(publish_error)),
                    Err(cleanup_error) => Err(VaultError::Io(io_error_with_cleanup(
                        publish_error,
                        cleanup_error,
                    ))),
                }
            }
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

    Ok(root.join(normalized))
}

impl VaultProvider for FsVaultProvider {
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

    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        let path = self.resolve_for_mutation(relative)?;
        atomic_write(&path, &self.tmp_dir(), contents)?;
        Ok(())
    }

    fn write_file_if_absent(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.write_file_if_absent_with_staging(
            relative,
            contents,
            |file, contents| file.write_all(contents),
            fs::File::sync_data,
            rename_no_replace,
        )
    }

    fn delete(&self, relative: &str) -> Result<(), VaultError> {
        let path = self.resolve_for_mutation(relative)?;
        // `symlink_metadata` uses lstat and does not follow symlinks, so
        // a broken symlink (a real directory entry whose target is gone)
        // still passes this guard. `path.exists()` would have hidden it.
        // Propagate the raw io::Error so permission-denied (etc.)
        // doesn't get masked as NotFound.
        let meta = path.symlink_metadata().map_err(VaultError::Io)?;

        // Broken symlinks: some `trash` backends reject dangling links
        // (macOS routes through Finder, which refuses). The target file
        // is already gone so there's nothing recoverable to send to the
        // trash anyway — unlink the dangling entry directly.
        if meta.file_type().is_symlink() && !path.exists() {
            return unlink_dangling_symlink(&path, &meta).map_err(VaultError::Io);
        }

        match trash::delete(&path) {
            Ok(()) => Ok(()),
            Err(e) => Err(VaultError::Trash {
                message: e.to_string(),
            }),
        }
    }

    fn create_dir(&self, relative: &str) -> Result<(), VaultError> {
        let path = self.resolve_for_mutation(relative)?;
        std::fs::create_dir_all(&path).map_err(VaultError::from)
    }

    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        let from_path = self.resolve_for_mutation(from)?;
        let to_path = self.resolve_for_mutation(to)?;
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
        match to_path.symlink_metadata() {
            Ok(_) => {
                return Err(VaultError::DestinationExists {
                    path: to.to_string(),
                });
            }
            Err(e) if e.kind() == io::ErrorKind::NotFound => {}
            Err(e) => return Err(VaultError::Io(e)),
        }
        if let Some(parent) = to_path.parent() {
            fs::create_dir_all(parent)?;
        }
        // Atomic no-replace (#871 Codex round 4): closes the window between the
        // lstat check above and the rename, where an external writer could have
        // created the destination — `fs::rename` would then destroy it. The
        // kernel primitive fails with `EEXIST` if the destination exists,
        // atomically, matching `atomic_create`'s `hard_link` no-replace publish.
        match rename_no_replace(&from_path, &to_path) {
            Ok(()) => Ok(()),
            // `AlreadyExists` is the portable mapping of the atomic primitive's
            // EEXIST (std maps it there) — referenced instead of `libc::EEXIST`
            // so this arm compiles on Windows, where `libc` is not a dependency
            // (#871 Codex round 5).
            Err(e) if e.kind() == io::ErrorKind::AlreadyExists => {
                Err(VaultError::DestinationExists {
                    path: to.to_string(),
                })
            }
            Err(e) => Err(VaultError::Io(e)),
        }
    }

    fn preflight_rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        let from_path = self.resolve_for_mutation(from)?;
        let to_path = self.resolve_for_mutation(to)?;
        from_path.symlink_metadata().map_err(VaultError::Io)?;
        self.require_mutation_parent_access(&from_path)?;
        self.require_mutation_parent_access(&to_path)?;
        match to_path.symlink_metadata() {
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

    fn watch(&self, _sink: Arc<dyn FileEventSink>) -> Result<Option<WatchHandle>, VaultError> {
        // Real watcher is a follow-up issue. For Milestone A, returning
        // None tells the session to fall back to refresh-on-foreground
        // semantics, which works for the tester build.
        Ok(None)
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

/// Remove a dangling symlink entry. Pulled out as a helper so the
/// Windows-specific dir-vs-file branch lives in one place.
///
/// Unix: `unlink(2)` (which `fs::remove_file` calls) removes the
/// directory entry of any symlink regardless of what it pointed at.
///
/// Windows: symlinks carry a type flag at creation time. A symlink
/// declared as a directory symlink must be removed with `RemoveDirectory`
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

#[cfg(not(windows))]
fn unlink_dangling_symlink(path: &Path, _meta: &fs::Metadata) -> io::Result<()> {
    fs::remove_file(path)
}

/// Atomically rename `from` → `to`, FAILING with `EEXIST` if `to` already
/// exists (#871 Codex round 4). Unlike `fs::rename` — which silently REPLACES
/// its target — this closes the check-to-rename TOCTOU window: an external
/// process that creates the destination after the caller's lstat pre-check can
/// no longer be clobbered. macOS uses `renamex_np(RENAME_EXCL)`, Linux
/// `renameat2(RENAME_NOREPLACE)`. Other targets (Windows is parked) fall back
/// to a plain `fs::rename` guarded only by the caller's pre-check.
#[cfg(target_os = "macos")]
fn rename_no_replace(from: &Path, to: &Path) -> io::Result<()> {
    use std::os::unix::ffi::OsStrExt;
    let from_c = std::ffi::CString::new(from.as_os_str().as_bytes())
        .map_err(|e| io::Error::new(io::ErrorKind::InvalidInput, e))?;
    let to_c = std::ffi::CString::new(to.as_os_str().as_bytes())
        .map_err(|e| io::Error::new(io::ErrorKind::InvalidInput, e))?;
    // SAFETY: both CStrings outlive the call and are valid NUL-terminated
    // paths; RENAME_EXCL asks the kernel for atomic no-replace semantics.
    let rc = unsafe { libc::renamex_np(from_c.as_ptr(), to_c.as_ptr(), libc::RENAME_EXCL) };
    if rc != 0 {
        return Err(io::Error::last_os_error());
    }
    Ok(())
}

// GNU only — `libc 0.2` exposes the `renameat2` wrapper under Linux glibc but
// not musl (#871 Codex round 5), so musl falls to the portable branch below.
#[cfg(all(target_os = "linux", target_env = "gnu"))]
fn rename_no_replace(from: &Path, to: &Path) -> io::Result<()> {
    use std::os::unix::ffi::OsStrExt;
    let from_c = std::ffi::CString::new(from.as_os_str().as_bytes())
        .map_err(|e| io::Error::new(io::ErrorKind::InvalidInput, e))?;
    let to_c = std::ffi::CString::new(to.as_os_str().as_bytes())
        .map_err(|e| io::Error::new(io::ErrorKind::InvalidInput, e))?;
    // SAFETY: valid NUL-terminated paths; RENAME_NOREPLACE = atomic no-replace.
    let rc = unsafe {
        libc::renameat2(
            libc::AT_FDCWD,
            from_c.as_ptr(),
            libc::AT_FDCWD,
            to_c.as_ptr(),
            libc::RENAME_NOREPLACE,
        )
    };
    if rc != 0 {
        return Err(io::Error::last_os_error());
    }
    Ok(())
}

// Portable fallback: no atomic no-replace primitive is wired for this target
// (Windows — parked — and Linux musl). The caller's lstat pre-check is the
// best-effort guard, with the residual check-to-rename TOCTOU window `fs::rename`
// inherently has. None of slate's SHIPPING targets (macOS, Linux glibc) land
// here; they take the atomic branches above.
#[cfg(not(any(target_os = "macos", all(target_os = "linux", target_env = "gnu"))))]
fn rename_no_replace(from: &Path, to: &Path) -> io::Result<()> {
    fs::rename(from, to)
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

/// Atomically replace the contents of `target` by writing into a temp
/// file under `tmp_dir` and renaming it into place.
///
/// `tmp_dir` must live on the same filesystem as `target` so the final
/// rename is atomic — the caller passes `<vault_root>/.slate/tmp/` for
/// that reason. Parking temps under `.slate/` (a) keeps the partial
/// state invisible to `list_dir` callers reading the target's directory
/// during the write, and (b) means a crash-leak doesn't surface as a
/// visible vault entry.
///
/// If `target` already exists, its filesystem permissions are copied to
/// the temp before rename so a 0644 file isn't silently downgraded to
/// the temp's stricter default. New files keep the temp's permissions.
///
/// Note on symlinks: if `target` is a symlink, `rename(2)` replaces the
/// symlink directory entry with the regular temp file. `write_file`
/// does not transparently follow symlinks to write through to their
/// target — see the vault module docs.
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

    let mut temp = tempfile::NamedTempFile::new_in(tmp_dir)?;
    temp.write_all(contents)?;
    temp.flush()?;
    temp.as_file().sync_data()?;
    if let Some(perms) = existing_perms {
        // Ignore failure: a successful overwrite with the temp's mode
        // is still better than aborting the write on a perms snag.
        let _ = temp.as_file().set_permissions(perms);
    }
    temp.persist(target).map_err(|e| e.error)?;
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

    #[test]
    fn watch_returns_none_in_milestone_a() {
        let (_tmp, p) = vault();
        struct NullSink;
        impl FileEventSink for NullSink {
            fn on_event(&self, _event: super::super::provider::FileEvent) {}
        }
        let sink: Arc<dyn FileEventSink> = Arc::new(NullSink);
        let result = p.watch(sink).unwrap();
        assert!(result.is_none());
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
                rename_no_replace,
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
                rename_no_replace,
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
                rename_no_replace(staged, destination)?;
                // Reproduce the reviewer's post-publish edge: any attempt to
                // unlink the old staging name now fails. A successful atomic
                // move must nevertheless remain a successful create.
                fs::set_permissions(
                    staged.parent().expect("staging parent"),
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
                        staged.parent().expect("staging parent"),
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

    /// #871 Codex round 4: the ATOMIC primitive itself must refuse an occupied
    /// destination with `EEXIST` — this is the layer that closes the
    /// check-to-rename TOCTOU window (an external writer winning the race after
    /// `provider.rename`'s lstat pre-check). Tested directly, bypassing that
    /// pre-check, since the window itself is inherently non-deterministic.
    #[cfg(any(target_os = "macos", all(target_os = "linux", target_env = "gnu")))]
    #[test]
    fn rename_no_replace_atomic_refuses_occupied_destination() {
        let (tmp, _p) = vault();
        std::fs::write(tmp.path().join("src.md"), b"source").unwrap();
        std::fs::write(tmp.path().join("dst.md"), b"victim").unwrap();

        let err = rename_no_replace(&tmp.path().join("src.md"), &tmp.path().join("dst.md"))
            .expect_err("atomic no-replace must refuse an occupied destination");

        // EEXIST maps to `AlreadyExists` — the same portable check `rename` uses.
        assert_eq!(err.kind(), io::ErrorKind::AlreadyExists);
        assert_eq!(std::fs::read(tmp.path().join("dst.md")).unwrap(), b"victim");
        assert!(tmp.path().join("src.md").exists());
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
}
