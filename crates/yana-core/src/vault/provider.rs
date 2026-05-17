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
