//! Vault file-system abstraction.
//!
//! `VaultProvider` is the trait through which the vault session reads
//! and writes vault files. The desktop implementation (`FsVaultProvider`)
//! uses `std::fs`; mobile platforms supply their own implementations
//! that honor security-scoped resource handles (`UIDocumentPicker`
//! bookmarks on iOS, SAF URIs on Android).
//!
//! Decisions documented in this module:
//!
//! - **Path safety.** All vault-relative paths are normalized and
//!   validated. Absolute paths and `..` traversal are rejected with
//!   `VaultError::InvalidPath`. `.` components are allowed and stripped.
//! - **Symlinks.** `list_dir` reports symlinks as their own
//!   `EntryKind::Symlink` variant without following. Per-call
//!   operations (`read_file`, `stat`, `write_file`) follow symlinks via
//!   the OS default. Recursive enumeration — handled at the
//!   `VaultSession` layer, not here — will need cycle detection, but
//!   `FsVaultProvider`'s per-call API is symlink-safe by virtue of not
//!   recursing.
//! - **Atomic writes.** `write_file` writes to a temporary file in the
//!   target's parent directory and renames it into place. The temp file
//!   shares a filesystem with the target so the rename is atomic on
//!   POSIX. The target is either at its old content or its new content,
//!   never partial.
//! - **Delete.** `delete` uses the `trash` crate to move-to-trash on
//!   macOS and Windows (YANA's target platforms). On platforms without
//!   trash support the call returns `VaultError::Trash`; the engine does
//!   not silently fall back to permanent deletion.

mod fs;
mod provider;

pub use fs::{content_hash, FsVaultProvider};
pub use provider::{
    DirEntry, EntryKind, FileEvent, FileEventSink, FileStat, VaultProvider, WatchHandle,
};
