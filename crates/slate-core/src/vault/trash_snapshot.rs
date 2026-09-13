// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Bounded, no-follow inventory for staged Trash confirmation. This is an
//! observation, not an OS filesystem transaction: a non-cooperating writer can
//! still change an entry after validation and before the system Trash call.

#[cfg(not(any(unix, windows)))]
use std::path::Path;
#[cfg(unix)]
use std::{fs, io};

use crate::VaultError;

/// Provider-owned inventory of one mutation entry, including hidden descendants.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TrashSnapshot {
    pub fingerprint: String,
    pub is_directory: bool,
    /// All descendants, including folders and symlinks; excludes the root entry.
    pub item_count: u64,
}

const MAX_ENTRIES: u64 = 100_000;
const MAX_DEPTH: usize = 256;

fn refused(message: &str) -> VaultError {
    VaultError::InvalidArgument {
        message: message.into(),
    }
}

#[cfg(windows)]
mod windows;
#[cfg(windows)]
pub(super) use windows::snapshot_cancellable;

#[cfg(not(any(unix, windows)))]
pub(super) fn snapshot_cancellable(
    _root: &Path,
    _path: &Path,
    cancel: &crate::CancelToken,
) -> Result<TrashSnapshot, VaultError> {
    cancel.check()?;
    Err(refused("Trash snapshots are unavailable on this platform"))
}

fn field(hasher: &mut blake3::Hasher, value: &[u8]) {
    hasher.update(&(value.len() as u64).to_le_bytes());
    hasher.update(value);
}

#[cfg(unix)]
pub(super) fn snapshot_at(
    parent: &fs::File,
    leaf: &std::ffi::CStr,
    cancel: &crate::CancelToken,
) -> Result<TrashSnapshot, VaultError> {
    cancel.check()?;
    use std::os::fd::AsRawFd;
    let mut hasher = blake3::Hasher::new();
    let mut entries = 1;
    let is_directory = unix_visit(
        parent.as_raw_fd(),
        leaf,
        &mut hasher,
        &mut entries,
        0,
        cancel,
    )?;
    cancel.check()?;
    Ok(TrashSnapshot {
        fingerprint: hasher.finalize().to_hex().to_string(),
        is_directory,
        item_count: entries - 1,
    })
}

#[cfg(unix)]
fn unix_stat(parent: std::os::fd::RawFd, leaf: &std::ffi::CStr) -> io::Result<libc::stat> {
    let mut stat = std::mem::MaybeUninit::uninit();
    // SAFETY: parent is held open by the caller; leaf is terminated and stat
    // has the syscall's required size/alignment. Never follow the leaf link.
    if unsafe {
        libc::fstatat(
            parent,
            leaf.as_ptr(),
            stat.as_mut_ptr(),
            libc::AT_SYMLINK_NOFOLLOW,
        )
    } != 0
    {
        return Err(io::Error::last_os_error());
    }
    // SAFETY: successful fstatat initialized stat.
    Ok(unsafe { stat.assume_init() })
}

#[cfg(unix)]
fn unix_stamp(m: &libc::stat) -> Vec<u8> {
    let mut bytes = Vec::new();
    macro_rules! append {
        ($($value:expr),*) => { $(bytes.extend_from_slice(&$value.to_le_bytes());)* };
    }
    append!(
        m.st_dev,
        m.st_ino,
        m.st_mode,
        m.st_size,
        m.st_mtime,
        m.st_mtime_nsec,
        m.st_ctime,
        m.st_ctime_nsec
    );
    bytes
}

#[cfg(unix)]
fn unix_visit(
    parent: std::os::fd::RawFd,
    leaf: &std::ffi::CStr,
    hasher: &mut blake3::Hasher,
    entries: &mut u64,
    depth: usize,
    cancel: &crate::CancelToken,
) -> Result<bool, VaultError> {
    cancel.check()?;
    use std::os::fd::{AsRawFd, FromRawFd, IntoRawFd};
    if *entries > MAX_ENTRIES || depth > MAX_DEPTH {
        return Err(refused(
            "Folder is too large to verify for Trash; select a smaller group of items",
        ));
    }
    let metadata = unix_stat(parent, leaf)?;
    cancel.check()?;
    let before = unix_stamp(&metadata);
    let kind = metadata.st_mode & libc::S_IFMT;
    if depth == 0 && kind == libc::S_IFLNK {
        return Err(refused("Cannot stage Trash for a symbolic link"));
    }
    hasher.update(&[0]); // begin entry
    field(hasher, &before);
    if kind == libc::S_IFDIR {
        cancel.check()?;
        // SAFETY: parent and leaf are live; openat returns an owned descriptor.
        // O_DIRECTORY|O_NOFOLLOW refuses a leaf replaced by a link or file.
        let fd = unsafe {
            libc::openat(
                parent,
                leaf.as_ptr(),
                libc::O_RDONLY | libc::O_DIRECTORY | libc::O_NOFOLLOW | libc::O_CLOEXEC,
            )
        };
        if fd < 0 {
            return Err(io::Error::last_os_error().into());
        }
        // SAFETY: fd is newly owned by us.
        let directory = unsafe { fs::File::from_raw_fd(fd) };
        if before != unix_stamp(&unix_stat(directory.as_raw_fd(), c".")?) {
            return Err(refused(
                "Files changed while preparing Trash. Request deletion again",
            ));
        }
        let duplicate = directory.try_clone()?.into_raw_fd();
        // SAFETY: duplicate is owned; fdopendir owns it only on success.
        let raw = unsafe { libc::fdopendir(duplicate) };
        if raw.is_null() {
            let error = io::Error::last_os_error();
            // SAFETY: fdopendir did not take ownership on failure.
            unsafe {
                libc::close(duplicate);
            }
            return Err(error.into());
        }
        struct DirectoryStream(*mut libc::DIR);
        impl Drop for DirectoryStream {
            fn drop(&mut self) {
                // SAFETY: this owns the live directory stream exactly once.
                unsafe {
                    libc::closedir(self.0);
                }
            }
        }
        let stream = DirectoryStream(raw);
        let mut children = Vec::new();
        loop {
            cancel.check()?;
            // SAFETY: errno is thread-local; stream is live and exclusively
            // used here. Copy each readdir name before the next call.
            let entry = unsafe {
                #[cfg(any(target_os = "linux", target_os = "android"))]
                {
                    *libc::__errno_location() = 0;
                }
                #[cfg(not(any(target_os = "linux", target_os = "android")))]
                {
                    *libc::__error() = 0;
                }
                libc::readdir(stream.0)
            };
            if entry.is_null() {
                let error = io::Error::last_os_error();
                if error.raw_os_error().unwrap_or(0) != 0 {
                    return Err(error.into());
                }
                break;
            }
            // SAFETY: readdir returned a live, terminated entry name.
            let name = unsafe { std::ffi::CStr::from_ptr((*entry).d_name.as_ptr()) };
            if name == c"." || name == c".." {
                continue;
            }
            if *entries >= MAX_ENTRIES {
                return Err(refused(
                    "Folder is too large to verify for Trash; select a smaller group of items",
                ));
            }
            children.push(name.to_owned());
            *entries += 1;
        }
        cancel.check()?;
        children.sort();
        cancel.check()?;
        for name in children {
            hasher.update(&[1]); // child name follows
            field(hasher, name.to_bytes());
            unix_visit(
                directory.as_raw_fd(),
                &name,
                hasher,
                entries,
                depth + 1,
                cancel,
            )?;
        }
    }
    cancel.check()?;
    if before != unix_stamp(&unix_stat(parent, leaf)?) {
        return Err(refused(
            "Files changed while preparing Trash. Request deletion again",
        ));
    }
    hasher.update(&[2]); // end entry: distinguish different tree nesting
    Ok(kind == libc::S_IFDIR)
}
