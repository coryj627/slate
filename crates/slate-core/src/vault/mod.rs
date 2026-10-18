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
//!   so a broken symlink can still be cleaned up or moved. On Unix
//!   `rename` resolves both paths beneath the vault root without
//!   following a symlink among their ancestors, and refuses one (#1449);
//!   so do `write_file`, `write_file_if_absent`, `create_dir` and
//!   `create_dir_if_absent` for their one path, and `create_dir` also
//!   refuses a final component that is a link (#1452). The root itself
//!   may be a symlink. Windows does the same with a walk of handles
//!   (`windows_entry`, #1451): a junction, a symbolic link or another
//!   name-surrogate reparse point among the ancestors refuses, a folder
//!   that is a reparse point redirecting nothing (a Cloud Files
//!   placeholder, as every OneDrive Files On-Demand folder is) is a
//!   folder, and the root may be a junction. Recursive enumeration —
//!   handled at the `VaultSession` layer, not here — will need cycle
//!   detection, but `FsVaultProvider`'s per-call API is symlink-safe by
//!   virtue of not recursing.
//! - **Atomic writes.** `write_file` writes a temp file under the
//!   vault's hidden `.slate/tmp/` directory, then renames it onto the
//!   target, into its folder pinned beneath the root (Unix: #1452;
//!   Windows: #1451). Same filesystem → atomic rename. Parking the temp inside
//!   `.slate/` keeps half-written files out of `list_dir` results for
//!   the target's directory and means a crash-leak doesn't surface as
//!   a visible vault entry. Existing-file permissions are preserved
//!   across overwrite; new files take the tempfile crate's default
//!   (0600 on POSIX).
//! - **Delete.** `delete` moves the entry to the system Trash:
//!   `NSFileManager` on macOS, the `trash` crate elsewhere (the Recycle
//!   Bin on Windows, the freedesktop Trash on Linux). The path it hands
//!   over is derived from the pinned parent of a no-follow walk and must
//!   lie beneath the vault root (`trash_handoff`, #1450). A symbolic
//!   link goes to the Trash as the link itself, whether or not its target
//!   resolves (#1477, #1495), except that Windows still unlinks a link whose
//!   target is truly absent until the Recycle Bin is checked with one
//!   (#1499). On platforms
//!   without trash support the call returns `VaultError::Trash`; the
//!   engine does not silently fall back to permanent deletion. Tests
//!   trash into scratch bins, never the Trash of whoever runs them
//!   (`trash_handoff`, #1455).

mod fs;
mod provider;
mod trash_handoff;
mod trash_snapshot;

#[cfg(test)]
pub(crate) use trash_handoff::ScratchTrash;
pub use trash_handoff::{route_trash_to_scratch_bin_for_tests, scratch_trash_bin_for_tests};

pub use trash_snapshot::TrashSnapshot;

#[cfg(windows)]
pub(crate) use fs::windows_extended_path;
pub use fs::{FsVaultProvider, content_hash};
pub use provider::{
    DirEntry, EntryKind, FileEvent, FileEventSink, FileStat, VaultProvider, WatchHandle,
};

#[cfg(windows)]
mod windows_entry;

#[cfg(all(test, windows))]
pub(crate) mod test_support;

#[cfg(test)]
mod test_trash_census;
