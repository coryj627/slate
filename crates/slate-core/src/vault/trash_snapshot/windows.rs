// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Handle-relative inventory: denying delete sharing alone does not prevent
//! FILE_WRITE_ATTRIBUTES callers from converting a pinned directory to a junction.

use super::{MAX_DEPTH, MAX_ENTRIES, TrashSnapshot, field, refused};
use crate::VaultError;
use std::os::windows::{
    ffi::{OsStrExt, OsStringExt},
    fs::{MetadataExt, OpenOptionsExt},
    io::{AsRawHandle, FromRawHandle},
};
use std::{
    ffi::{OsStr, OsString},
    fs, io,
    path::{Component, Path},
};
use windows_sys::{
    Wdk::{
        Foundation::OBJECT_ATTRIBUTES,
        Storage::FileSystem::{
            FILE_OPEN, FILE_OPEN_REPARSE_POINT, FILE_SYNCHRONOUS_IO_NONALERT, NtCreateFile,
        },
    },
    Win32::{
        Foundation::{
            ERROR_NO_MORE_FILES, OBJ_CASE_INSENSITIVE, RtlNtStatusToDosError, UNICODE_STRING,
        },
        Storage::FileSystem::*,
        System::IO::IO_STATUS_BLOCK,
    },
};

pub(in crate::vault) fn snapshot(root: &Path, path: &Path) -> Result<TrashSnapshot, VaultError> {
    let relative = path
        .strip_prefix(root)
        .map_err(|_| refused("Trash path is outside the vault"))?;
    let root = fs::OpenOptions::new()
        .access_mode(FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT)
        .open(root)?;
    check_directory(&root)?;
    let mut guards = vec![root];
    let components = relative.components().collect::<Vec<_>>();
    if components.is_empty() {
        return Err(refused("Cannot stage Trash for the vault root"));
    }
    for (index, component) in components.iter().enumerate() {
        let Component::Normal(name) = component else {
            return Err(refused("Invalid Trash path component"));
        };
        let child = open_child(guards.last().unwrap(), name)?;
        if index + 1 < components.len() {
            check_directory(&child)?;
        }
        guards.push(child);
    }
    let file = guards.last().unwrap();
    let metadata = file.metadata()?;
    if is_link(&metadata) {
        return Err(refused("Cannot stage Trash for a symbolic link"));
    }
    let mut hasher = blake3::Hasher::new();
    let mut entries = 1;
    visit(file, &mut hasher, &mut entries, 0)?;
    Ok(TrashSnapshot {
        fingerprint: hasher.finalize().to_hex().to_string(),
        is_directory: metadata.is_dir(),
        item_count: entries - 1,
    })
}

fn is_link(metadata: &fs::Metadata) -> bool {
    metadata.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT != 0
}

fn check_directory(file: &fs::File) -> Result<(), VaultError> {
    let metadata = file.metadata()?;
    if !metadata.is_dir() || is_link(&metadata) {
        return Err(refused("Cannot stage Trash through a symbolic link"));
    }
    Ok(())
}

fn open_child(parent: &fs::File, name: &OsStr) -> io::Result<fs::File> {
    let mut name: Vec<u16> = name.encode_wide().collect();
    if name.is_empty()
        || name == [46]
        || name == [46, 46]
        || name.iter().any(|c| matches!(*c, 0 | 47 | 58 | 92))
    {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Expected a simple entry name",
        ));
    }
    let length = u16::try_from(name.len() * 2)
        .map_err(|_| io::Error::new(io::ErrorKind::InvalidInput, "Entry name is too long"))?;
    let mut unicode = UNICODE_STRING {
        Length: length,
        MaximumLength: length,
        Buffer: name.as_mut_ptr(),
    };
    let attributes = OBJECT_ATTRIBUTES {
        Length: std::mem::size_of::<OBJECT_ATTRIBUTES>() as u32,
        RootDirectory: parent.as_raw_handle(),
        ObjectName: &mut unicode,
        Attributes: OBJ_CASE_INSENSITIVE,
        ..Default::default()
    };
    let mut handle = std::ptr::null_mut();
    let mut status_block: IO_STATUS_BLOCK = unsafe { std::mem::zeroed() };
    // SAFETY: the synchronous call borrows live name/attribute/status buffers and
    // the parent handle. A single component plus OPEN_REPARSE_POINT never follows
    // a child link; converted parent reparses fail instead of re-resolving a path.
    let status = unsafe {
        NtCreateFile(
            &mut handle,
            FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY | SYNCHRONIZE,
            &attributes,
            &mut status_block,
            std::ptr::null(),
            0,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            FILE_OPEN,
            FILE_OPEN_REPARSE_POINT | FILE_SYNCHRONOUS_IO_NONALERT,
            std::ptr::null(),
            0,
        )
    };
    if status < 0 {
        return Err(io::Error::from_raw_os_error(
            unsafe { RtlNtStatusToDosError(status) } as i32,
        ));
    }
    // SAFETY: a successful NtCreateFile transfers a newly owned file handle.
    Ok(unsafe { fs::File::from_raw_handle(handle) })
}

fn visit(
    file: &fs::File,
    hasher: &mut blake3::Hasher,
    entries: &mut u64,
    depth: usize,
) -> Result<(), VaultError> {
    if depth > MAX_DEPTH {
        return Err(refused(
            "Folder is too large to verify for Trash; select a smaller group of items",
        ));
    }
    let before = stamp(file)?;
    let metadata = file.metadata()?;
    hasher.update(&[0]);
    field(hasher, &before);
    if is_link(&metadata) && !known_link(file)? {
        return Err(refused(
            "Cannot verify Trash contents for this reparse entry",
        ));
    }
    if metadata.is_dir() && !is_link(&metadata) {
        for name in children(file, entries)? {
            hasher.update(&[1]);
            field(hasher, name.as_encoded_bytes());
            let child = open_child(file, &name)?;
            visit(&child, hasher, entries, depth + 1)?;
        }
    }
    if before != stamp(file)? {
        return Err(refused(
            "Files changed while preparing Trash. Request deletion again",
        ));
    }
    hasher.update(&[2]);
    Ok(())
}

fn children(directory: &fs::File, entries: &mut u64) -> Result<Vec<OsString>, VaultError> {
    // Eight-byte alignment, fixed memory irrespective of directory size. Charge
    // names when discovered, so nested pending child lists share one budget.
    let mut buffer = vec![0u64; 8192];
    let size = (buffer.len() * 8) as u32;
    let mut class = FileFullDirectoryRestartInfo;
    let mut names = Vec::new();
    loop {
        buffer.fill(0);
        // SAFETY: live directory handle and writable, aligned, correctly sized buffer.
        if unsafe {
            GetFileInformationByHandleEx(
                directory.as_raw_handle(),
                class,
                buffer.as_mut_ptr().cast(),
                size,
            )
        } == 0
        {
            let error = io::Error::last_os_error();
            if error.raw_os_error() == Some(ERROR_NO_MORE_FILES as i32) {
                break;
            }
            return Err(error.into());
        }
        class = FileFullDirectoryInfo;
        // SAFETY: all bytes belong to the initialized buffer. Validate every
        // variable-length record before reading its name or advancing.
        let bytes =
            unsafe { std::slice::from_raw_parts(buffer.as_ptr().cast::<u8>(), size as usize) };
        let mut offset = 0;
        loop {
            let name_offset = std::mem::offset_of!(FILE_FULL_DIR_INFO, FileName);
            if offset + std::mem::size_of::<FILE_FULL_DIR_INFO>() > bytes.len() {
                return Err(refused("Invalid directory inventory"));
            }
            let info = unsafe {
                std::ptr::read_unaligned(bytes.as_ptr().add(offset).cast::<FILE_FULL_DIR_INFO>())
            };
            let length = info.FileNameLength as usize;
            let end = offset + name_offset + length;
            if length == 0 || !length.is_multiple_of(2) || end > bytes.len() {
                return Err(refused("Invalid directory inventory"));
            }
            let wide = bytes[offset + name_offset..end]
                .chunks_exact(2)
                .map(|b| u16::from_le_bytes([b[0], b[1]]))
                .collect::<Vec<_>>();
            if wide != [46] && wide != [46, 46] {
                if *entries >= MAX_ENTRIES {
                    return Err(refused(
                        "Folder is too large to verify for Trash; select a smaller group of items",
                    ));
                }
                *entries += 1;
                names.push(OsString::from_wide(&wide));
            }
            if info.NextEntryOffset == 0 {
                break;
            }
            let next = info.NextEntryOffset as usize;
            if next < name_offset + length || offset + next >= bytes.len() {
                return Err(refused("Invalid directory inventory"));
            }
            offset += next;
        }
    }
    names.sort();
    Ok(names)
}

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

fn stamp(file: &fs::File) -> io::Result<Vec<u8>> {
    use std::os::windows::io::AsRawHandle;
    use windows_sys::Win32::Storage::FileSystem::*;
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

#[cfg(test)]
#[path = "windows_tests.rs"]
mod tests;
