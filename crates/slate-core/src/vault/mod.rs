// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

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
//!   `EntryKind::Symlink` variant without following. `read_file` and
//!   `stat` follow symlinks via the OS default. `write_file` does
//!   *not* — its rename-into-place semantics replace a symlink's
//!   directory entry with a regular file rather than writing through
//!   the link to its target. Mutators (`delete`, `rename`) treat the
//!   directory entry itself — they use lstat-style existence checks
//!   so a broken symlink can still be cleaned up or moved. Recursive
//!   enumeration — handled at the `VaultSession` layer, not here —
//!   will need cycle detection, but `FsVaultProvider`'s per-call API
//!   is symlink-safe by virtue of not recursing.
//! - **Atomic writes.** `write_file` writes a temp file under the
//!   vault's hidden `.slate/tmp/` directory, then renames it onto the
//!   target. Same filesystem → atomic rename. Parking the temp inside
//!   `.slate/` keeps half-written files out of `list_dir` results for
//!   the target's directory and means a crash-leak doesn't surface as
//!   a visible vault entry. Existing-file permissions are preserved
//!   across overwrite; new files take the tempfile crate's default
//!   (0600 on POSIX).
//! - **Delete.** `delete` uses the `trash` crate to move-to-trash on
//!   macOS and Windows (Slate's target platforms). On platforms without
//!   trash support the call returns `VaultError::Trash`; the engine does
//!   not silently fall back to permanent deletion.

mod fs;
mod provider;

pub use fs::{content_hash, FsVaultProvider};
pub use provider::{
    DirEntry, EntryKind, FileEvent, FileEventSink, FileStat, VaultProvider, WatchHandle,
};
