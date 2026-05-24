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

    /// Delete a file or directory. Implementations should move-to-trash
    /// where the platform supports it.
    fn delete(&self, relative: &str) -> Result<(), VaultError>;

    /// Rename or move a file within the vault.
    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError>;

    /// Cheap metadata: size, mtime, kind.
    fn stat(&self, relative: &str) -> Result<FileStat, VaultError>;

    /// Best-effort change subscription. Returns `Ok(None)` if the
    /// platform doesn't support filesystem events for this vault — the
    /// engine falls back to refresh-on-foreground in that case.
    fn watch(&self, sink: Arc<dyn FileEventSink>) -> Result<Option<WatchHandle>, VaultError>;

    /// Verify that `relative` does not escape the vault scope once
    /// symlinks are followed.
    ///
    /// The textual checks in `resolve_relative` (rejecting `..` and
    /// absolute paths) only catch lexical escapes — they're blind to
    /// a symlink under the vault that *points* outside (e.g.
    /// `Templates/Pwn.md → /etc/passwd`). Callers that hand
    /// vault-relative paths to user-discoverable surfaces should
    /// invoke this before reading.
    ///
    /// Returns:
    ///   - `Ok(())` — the path stays in scope (or scope checks aren't
    ///     applicable to this provider).
    ///   - `Err(VaultError::InvalidPath { reason })` — the canonical
    ///     target escapes the vault.
    ///   - `Err(VaultError::Io(...))` — couldn't canonicalize (broken
    ///     symlink, permission denied, etc.). Callers should usually
    ///     surface this distinct from the InvalidPath case so users
    ///     can tell "missing file" from "refused for safety."
    ///
    /// Default impl returns `Ok(())` — providers that route through
    /// OS-level security-scoped APIs (iOS, Android) already enforce
    /// scope and don't need to repeat the check. The default also
    /// keeps existing test doubles working without forcing every
    /// mock to implement canonicalization.
    fn verify_in_vault(&self, relative: &str) -> Result<(), VaultError> {
        let _ = relative;
        Ok(())
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
