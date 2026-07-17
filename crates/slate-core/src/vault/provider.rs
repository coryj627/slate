// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `VaultProvider` trait and its associated types.
//!
//! Hosts implement this trait to give the engine vault access without
//! coupling the engine to a specific filesystem story. The desktop
//! implementation `FsVaultProvider` lives in `super::fs`.

use std::sync::Arc;

use crate::VaultError;

/// A pluggable vault file-system backend.
///
/// All path arguments are vault-relative. The provider is responsible
/// for resolving them against its own root and enforcing path safety
/// (no absolute paths, no parent-directory traversal).
pub trait VaultProvider: Send + Sync {
    /// List the immediate entries of a vault-relative directory.
    ///
    /// An empty `relative` (or `"."`) lists the vault root.
    fn list_dir(&self, relative: &str) -> Result<Vec<DirEntry>, VaultError>;

    /// Read a vault file's bytes.
    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError>;

    /// Read up to `max_bytes + 1` of a vault file's bytes.
    ///
    /// Returning more than `max_bytes` is the contract that signals
    /// "the file is larger than this cap." Callers check
    /// `returned.len() > max_bytes` to detect over-cap and avoid
    /// allocating arbitrary amounts of memory for a refuse-threshold
    /// check.
    ///
    /// Default implementation falls back to `read_file`, which is
    /// **not** memory-bounded. Providers that can do a true
    /// streaming read (filesystem-backed ones especially) should
    /// override this so the `read_text` refuse path is safe under
    /// TOCTOU growth.
    fn read_file_with_cap(&self, relative: &str, max_bytes: u64) -> Result<Vec<u8>, VaultError> {
        let _ = max_bytes;
        self.read_file(relative)
    }

    /// Write a vault file. Implementations should be atomic where
    /// possible (write to a temp file, then rename).
    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError>;

    /// Create a vault file that must not already exist (O-3 #541 —
    /// `create_exclusive`/recovery must never clobber a concurrent
    /// writer, including one outside Slate). An occupied destination
    /// is `VaultError::DestinationExists`, nothing written.
    /// Implementations should use a filesystem no-replace primitive
    /// (the default is a weaker check-then-write for providers without
    /// one — documented TOCTOU against non-Slate writers only; Slate
    /// writers are serialized by the caller's cross-process SQLite
    /// lock).
    fn write_file_if_absent(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        if self.stat(relative).is_ok() {
            return Err(VaultError::DestinationExists {
                path: relative.to_string(),
            });
        }
        self.write_file(relative, contents)
    }

    /// Delete a file or directory. Implementations should move-to-trash
    /// where the platform supports it.
    fn delete(&self, relative: &str) -> Result<(), VaultError>;

    /// Rename or move a file within the vault.
    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError>;

    /// Best-effort capability probe for a later rename. Batch structural
    /// operations call every probe before their first mutation. This cannot
    /// eliminate ACL/TOCTOU failures; runtime reporting remains authoritative.
    /// The default keeps host providers and test doubles source-compatible.
    fn preflight_rename(&self, from: &str, _to: &str) -> Result<(), VaultError> {
        self.stat(from).map(|_| ())
    }

    /// Best-effort capability probe for a later system-Trash operation.
    fn preflight_delete(&self, path: &str) -> Result<(), VaultError> {
        self.stat(path).map(|_| ())
    }

    /// Inspect the mutation entry itself after a provider call. Unlike a
    /// content read, this must treat a dangling symlink as present and must
    /// preserve the entry kind so callers can distinguish an original item
    /// that survived from an opposite-kind replacement at the same path.
    /// Providers with an lstat-style primitive should override the default.
    fn mutation_path_kind(&self, path: &str) -> Result<Option<EntryKind>, VaultError> {
        match self.stat(path) {
            Ok(stat) => Ok(Some(stat.kind)),
            Err(VaultError::Io(error)) if error.kind() == std::io::ErrorKind::NotFound => Ok(None),
            Err(error) => Err(error),
        }
    }

    fn mutation_path_exists(&self, path: &str) -> Result<bool, VaultError> {
        self.mutation_path_kind(path).map(|kind| kind.is_some())
    }

    /// Create a directory (and any missing parents) at a vault-relative
    /// path. Idempotent: an already-existing directory is Ok — the caller
    /// (U2-2 `create_folder`) enforces its own collision policy against
    /// the index, where case-insensitivity is decided; the provider only
    /// guarantees the directory exists afterwards.
    fn create_dir(&self, relative: &str) -> Result<(), VaultError>;

    /// Create a directory only when its final path component is absent.
    ///
    /// The compatibility default is a weaker check-then-create for host
    /// providers without an atomic primitive. It can lose a race to an
    /// external creator and merge the directory created in that window, and
    /// it inherits `create_dir`'s parent-creation behavior. Filesystem-backed
    /// providers must override this with an atomic final-component create that
    /// never merges an existing directory.
    fn create_dir_if_absent(&self, relative: &str) -> Result<(), VaultError> {
        if self.mutation_path_exists(relative)? {
            return Err(VaultError::DestinationExists {
                path: relative.to_string(),
            });
        }
        self.create_dir(relative)
    }

    /// Cheap metadata: size, mtime, kind.
    fn stat(&self, relative: &str) -> Result<FileStat, VaultError>;

    /// Best-effort change subscription. Returns `Ok(None)` if the
    /// platform doesn't support filesystem events for this vault — the
    /// engine falls back to refresh-on-foreground in that case.
    fn watch(&self, sink: Arc<dyn FileEventSink>) -> Result<Option<WatchHandle>, VaultError>;

    /// Atomically check + read a vault file: canonicalize-and-
    /// verify-in-scope as one indivisible step with the open that
    /// produces the bytes.
    ///
    /// Why this is one method, not separate verify + read:
    ///
    /// A naïve `verify(path); read(path);` pair has a TOCTOU race —
    /// between the verify (which says "the canonical target sits
    /// inside the vault root") and the subsequent open of the same
    /// vault-relative path, an attacker with filesystem write
    /// access can swap a symlink so the open follows OUT of the
    /// vault even though verify said it was safe (Codoki PR #153
    /// Medium). Doing both in one method, and opening the
    /// **canonical resolved path** rather than re-resolving the
    /// relative path through the kernel, closes that window: by
    /// the time we have a canonical absolute path with no symlink
    /// components left, `File::open` opens that exact inode and
    /// can't be redirected.
    ///
    /// Returns:
    ///   - `Ok(bytes)` — bytes read from the verified canonical
    ///     target. As with `read_file_with_cap`, returning more
    ///     than `max_bytes` is the "exceeded cap" sentinel.
    ///   - `Err(VaultError::InvalidPath { reason })` — the
    ///     canonical target sits outside the vault scope (a
    ///     symlink under the vault pointing out).
    ///   - `Err(VaultError::Io(...))` — couldn't canonicalize or
    ///     open (broken symlink, permission denied, etc.).
    ///
    /// Default impl forwards to `read_file_with_cap` — providers
    /// that route through OS-level security-scoped APIs (iOS,
    /// Android) already enforce scope and don't need the
    /// canonical-path dance. The default also keeps existing test
    /// doubles working without forcing every mock to implement
    /// canonicalization.
    fn read_in_vault_with_cap(
        &self,
        relative: &str,
        max_bytes: u64,
    ) -> Result<Vec<u8>, VaultError> {
        self.read_file_with_cap(relative, max_bytes)
    }
}

/// A single entry in a directory listing.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DirEntry {
    /// File or directory name (the final path component). Vault-relative
    /// path is reconstructed by the caller from the directory and this
    /// name.
    pub name: String,
    pub kind: EntryKind,
}

/// Metadata about a single vault file.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FileStat {
    pub size_bytes: u64,
    /// Last-modified time as Unix epoch milliseconds.
    pub mtime_ms: i64,
    /// Inode change time as Unix epoch milliseconds. Unix only; `0` on
    /// platforms where `std::fs::Metadata` doesn't expose ctime (e.g.
    /// Windows). Used by the scanner's fast-path to catch
    /// mtime-preserving copies (`cp -p`, `rsync -a`) that mtime alone
    /// can't see.
    pub ctime_ms: i64,
    /// File birth time as Unix epoch milliseconds; `0` where the
    /// filesystem/platform doesn't expose it (the ctime convention).
    /// macOS/APFS `st_birthtime`. Compaction- and rebuild-stable —
    /// `oplog.created_since` (#801) lowers onto it precisely because
    /// event rows shift with retention folds while birth doesn't.
    pub birthtime_ms: i64,
    pub kind: EntryKind,
}

/// What kind of entry a vault path refers to.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum EntryKind {
    File,
    Directory,
    Symlink,
}

/// Sink for filesystem-watch events. The `watch` API hands the engine
/// a stream of file-change events; the sink is how those events are
/// delivered.
pub trait FileEventSink: Send + Sync {
    fn on_event(&self, event: FileEvent);
}

/// A single filesystem change event.
#[derive(Debug, Clone)]
pub enum FileEvent {
    Created { relative: String },
    Modified { relative: String },
    Deleted { relative: String },
    Renamed { from: String, to: String },
}

/// Opaque handle returned by `watch`. Dropping the handle unsubscribes.
/// Reserved for the real watcher implementation (V1.A ships with
/// `watch` returning `Ok(None)`).
#[derive(Debug)]
pub struct WatchHandle {
    _private: (),
}

impl WatchHandle {
    /// Reserved for the future watcher implementation. Not constructible
    /// from outside the crate to ensure handles always come from a
    /// provider's `watch` call.
    #[allow(dead_code)]
    pub(crate) fn new() -> Self {
        Self { _private: () }
    }
}
