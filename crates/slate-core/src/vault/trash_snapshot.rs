// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Bounded, no-follow inventory for staged Trash confirmation. This is an
//! observation, not an OS filesystem transaction: a non-cooperating writer can
//! still change an entry after validation and before the system Trash call.

#[cfg(not(unix))]
use std::path::Path;
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

#[cfg(not(unix))]
pub(super) fn snapshot(root: &Path, path: &Path) -> Result<TrashSnapshot, VaultError> {
    // Never walk through an already-present symlink/reparse ancestor. This does
    // not claim to exclude an external ancestor replacement during the walk.
    let relative = path
        .strip_prefix(root)
        .map_err(|_| refused("Trash path is outside the vault"))?;
    let mut ancestor = root.to_path_buf();
    let mut guards = vec![lock_entry(root)?];
    if let Some(parent) = relative.parent() {
        for component in parent.components() {
            ancestor.push(component);
            let guard = lock_entry(&ancestor)?;
            if is_link(&guard.metadata()?) {
                return Err(refused("Cannot stage Trash through a symbolic link"));
            }
            guards.push(guard);
        }
    }
    let metadata = fs::symlink_metadata(path)?;
    if is_link(&metadata) {
        return Err(refused("Cannot stage Trash for a symbolic link"));
    }
    let mut hasher = blake3::Hasher::new();
    let mut entries = 1;
    visit(path, &mut hasher, &mut entries, 0)?;
    Ok(TrashSnapshot {
        fingerprint: hasher.finalize().to_hex().to_string(),
        is_directory: metadata.is_dir(),
        item_count: entries - 1,
    })
}

fn field(hasher: &mut blake3::Hasher, value: &[u8]) {
    hasher.update(&(value.len() as u64).to_le_bytes());
    hasher.update(value);
}

#[cfg(not(unix))]
fn visit(
    path: &Path,
    hasher: &mut blake3::Hasher,
    entries: &mut u64,
    depth: usize,
) -> Result<(), VaultError> {
    if *entries > MAX_ENTRIES || depth > MAX_DEPTH {
        return Err(refused(
            "Folder is too large to verify for Trash; select a smaller group of items",
        ));
    }
    // Windows denies delete-sharing while this entry is observed. Each parent
    // remains pinned by its caller, so a path walk cannot follow a replacement.
    let guard = lock_entry(path)?;
    let before = stamp(path)?;
    hasher.update(&[0]); // begin entry
    field(hasher, &before);
    let metadata = guard.metadata()?;
    #[cfg(windows)]
    if is_link(&metadata) && !known_link(&guard)? {
        // Cloud/ProjFS and other non-name-surrogate reparses can own real
        // children. Treating them as opaque links would omit trashed data.
        return Err(refused(
            "Cannot verify Trash contents for this reparse entry",
        ));
    }
    if metadata.is_dir() && !is_link(&metadata) {
        let mut children = Vec::new();
        for child in fs::read_dir(path)? {
            if *entries >= MAX_ENTRIES {
                return Err(refused(
                    "Folder is too large to verify for Trash; select a smaller group of items",
                ));
            }
            children.push(child?);
            *entries += 1;
        }
        children.sort_by_key(|child| child.file_name());
        for child in children {
            hasher.update(&[1]); // child name follows
            field(hasher, child.file_name().as_encoded_bytes());
            visit(&child.path(), hasher, entries, depth + 1)?;
        }
    }
    if before != stamp(path)? {
        return Err(refused(
            "Files changed while preparing Trash. Request deletion again",
        ));
    }
    hasher.update(&[2]); // end entry: distinguish different tree nesting
    Ok(())
}

#[cfg(not(unix))]
fn is_link(metadata: &fs::Metadata) -> bool {
    #[cfg(windows)]
    {
        use std::os::windows::fs::MetadataExt;
        metadata.file_attributes()
            & windows_sys::Win32::Storage::FileSystem::FILE_ATTRIBUTE_REPARSE_POINT
            != 0
    }
    #[cfg(not(windows))]
    {
        metadata.file_type().is_symlink()
    }
}

#[cfg(windows)]
fn known_link(file: &fs::File) -> io::Result<bool> {
    use std::os::windows::io::AsRawHandle;
    use windows_sys::Win32::Storage::FileSystem::*;
    let mut info: FILE_ATTRIBUTE_TAG_INFO = unsafe { std::mem::zeroed() };
    // SAFETY: the buffer matches FileAttributeTagInfo and the handle is live.
    if unsafe {
        GetFileInformationByHandleEx(
            file.as_raw_handle(),
            FileAttributeTagInfo,
            (&mut info as *mut FILE_ATTRIBUTE_TAG_INFO).cast(),
            std::mem::size_of::<FILE_ATTRIBUTE_TAG_INFO>() as u32,
        )
    } == 0
    {
        return Err(io::Error::last_os_error());
    }
    // MS-FSCC reparse tags: mount point and symbolic link, respectively.
    // Unknown tags (including Cloud Files) deliberately fail closed.
    Ok(matches!(info.ReparseTag, 0xA000_0003 | 0xA000_000C))
}

#[cfg(windows)]
fn lock_entry(path: &Path) -> io::Result<fs::File> {
    use std::os::windows::fs::OpenOptionsExt;
    use windows_sys::Win32::Storage::FileSystem::*;
    fs::OpenOptions::new()
        .access_mode(FILE_READ_ATTRIBUTES)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT)
        .open(path)
}

#[cfg(windows)]
fn stamp(path: &Path) -> io::Result<Vec<u8>> {
    use std::os::windows::{fs::OpenOptionsExt, io::AsRawHandle};
    use windows_sys::Win32::Storage::FileSystem::*;
    let file = fs::OpenOptions::new()
        .access_mode(FILE_READ_ATTRIBUTES)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT)
        .open(path)?;
    let mut identity: FILE_ID_INFO = unsafe { std::mem::zeroed() };
    let mut basic: FILE_BASIC_INFO = unsafe { std::mem::zeroed() };
    let mut standard: FILE_STANDARD_INFO = unsafe { std::mem::zeroed() };
    // SAFETY: the handle stays open and each buffer has the declared class's
    // exact size and alignment. All observations use the same opened entry.
    unsafe {
        for (class, buffer, size) in [
            (
                FileIdInfo,
                (&mut identity as *mut FILE_ID_INFO).cast(),
                std::mem::size_of::<FILE_ID_INFO>(),
            ),
            (
                FileBasicInfo,
                (&mut basic as *mut FILE_BASIC_INFO).cast(),
                std::mem::size_of::<FILE_BASIC_INFO>(),
            ),
            (
                FileStandardInfo,
                (&mut standard as *mut FILE_STANDARD_INFO).cast(),
                std::mem::size_of::<FILE_STANDARD_INFO>(),
            ),
        ] {
            if GetFileInformationByHandleEx(file.as_raw_handle(), class, buffer, size as u32) == 0 {
                return Err(io::Error::last_os_error());
            }
        }
    }
    let mut bytes = identity.VolumeSerialNumber.to_le_bytes().to_vec();
    bytes.extend_from_slice(&identity.FileId.Identifier);
    for value in [
        basic.CreationTime,
        basic.LastWriteTime,
        basic.ChangeTime,
        standard.EndOfFile,
    ] {
        bytes.extend_from_slice(&value.to_le_bytes());
    }
    bytes.extend_from_slice(&basic.FileAttributes.to_le_bytes());
    Ok(bytes)
}

#[cfg(not(any(unix, windows)))]
fn stamp(_path: &Path) -> io::Result<Vec<u8>> {
    Err(io::Error::new(
        io::ErrorKind::Unsupported,
        "Trash snapshots are unavailable on this platform",
    ))
}

#[cfg(not(any(unix, windows)))]
fn lock_entry(_path: &Path) -> io::Result<fs::File> {
    Err(io::Error::new(
        io::ErrorKind::Unsupported,
        "Trash snapshots are unavailable on this platform",
    ))
}

#[cfg(unix)]
pub(super) fn snapshot_at(
    parent: &fs::File,
    leaf: &std::ffi::CStr,
) -> Result<TrashSnapshot, VaultError> {
    use std::os::fd::AsRawFd;
    let mut hasher = blake3::Hasher::new();
    let mut entries = 1;
    let is_directory = unix_visit(parent.as_raw_fd(), leaf, &mut hasher, &mut entries, 0)?;
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
) -> Result<bool, VaultError> {
    use std::os::fd::{AsRawFd, FromRawFd, IntoRawFd};
    if *entries > MAX_ENTRIES || depth > MAX_DEPTH {
        return Err(refused(
            "Folder is too large to verify for Trash; select a smaller group of items",
        ));
    }
    let metadata = unix_stat(parent, leaf)?;
    let before = unix_stamp(&metadata);
    let kind = metadata.st_mode & libc::S_IFMT;
    if depth == 0 && kind == libc::S_IFLNK {
        return Err(refused("Cannot stage Trash for a symbolic link"));
    }
    hasher.update(&[0]); // begin entry
    field(hasher, &before);
    if kind == libc::S_IFDIR {
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
        children.sort();
        for name in children {
            hasher.update(&[1]); // child name follows
            field(hasher, name.to_bytes());
            unix_visit(directory.as_raw_fd(), &name, hasher, entries, depth + 1)?;
        }
    }
    if before != unix_stamp(&unix_stat(parent, leaf)?) {
        return Err(refused(
            "Files changed while preparing Trash. Request deletion again",
        ));
    }
    hasher.update(&[2]); // end entry: distinguish different tree nesting
    Ok(kind == libc::S_IFDIR)
}
