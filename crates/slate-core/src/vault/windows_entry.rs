// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Windows no-follow entry resolution and identity-conditional rename. File IDs
//! are filesystem-issued values, not permanent incarnation IDs: a filesystem may
//! eventually reuse one after deletion. Comparison and rename use one live handle.

use crate::VaultError;
use std::os::windows::{
    ffi::OsStrExt,
    fs::{MetadataExt, OpenOptionsExt},
    io::{AsRawHandle, FromRawHandle},
};
use std::{
    ffi::OsStr,
    fs, io,
    path::{Component, Path},
};
use windows_sys::{
    Wdk::{
        Foundation::OBJECT_ATTRIBUTES,
        Storage::FileSystem::{
            FILE_OPEN, FILE_OPEN_REPARSE_POINT, FILE_RENAME_INFORMATION,
            FILE_SYNCHRONOUS_IO_NONALERT, FileRenameInformation, NtCreateFile,
            NtSetInformationFile,
        },
    },
    Win32::{
        Foundation::{OBJ_CASE_INSENSITIVE, RtlNtStatusToDosError, UNICODE_STRING},
        Storage::FileSystem::*,
        System::IO::IO_STATUS_BLOCK,
    },
};

pub(super) fn open_relative(parent: &fs::File, name: &OsStr, access: u32) -> io::Result<fs::File> {
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
            access | SYNCHRONIZE,
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

struct PinnedEntry {
    file: fs::File,
    _ancestors: Vec<fs::File>,
}

fn refused() -> VaultError {
    VaultError::InvalidArgument {
        message:
            "Cannot undo or redo: the files have changed or their identities cannot be verified"
                .into(),
    }
}

fn directory(file: &fs::File) -> Result<(), VaultError> {
    let metadata = file.metadata()?;
    if !metadata.is_dir() || metadata.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT != 0 {
        return Err(refused());
    }
    Ok(())
}

fn open(root: &Path, relative: &Path, access: u32) -> Result<PinnedEntry, VaultError> {
    let root = fs::OpenOptions::new()
        .access_mode(FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT)
        .open(root)?;
    directory(&root)?;
    let mut ancestors = vec![root];
    let components = relative.components().collect::<Vec<_>>();
    if components.is_empty() {
        return Err(refused());
    }
    for (index, component) in components.iter().enumerate() {
        let Component::Normal(name) = component else {
            return Err(refused());
        };
        let last = index + 1 == components.len();
        let child = open_relative(
            ancestors.last().unwrap(),
            name,
            if last {
                access
            } else {
                FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY
            },
        )?;
        if last {
            if child.metadata()?.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT != 0 {
                return Err(refused());
            }
            return Ok(PinnedEntry {
                file: child,
                _ancestors: ancestors,
            });
        }
        directory(&child)?;
        ancestors.push(child);
    }
    Err(refused())
}

fn identity(file: &fs::File) -> Result<String, VaultError> {
    let mut info: FILE_ID_INFO = unsafe { std::mem::zeroed() };
    // SAFETY: live handle and the exact initialized output structure.
    if unsafe {
        GetFileInformationByHandleEx(
            file.as_raw_handle(),
            FileIdInfo,
            (&mut info as *mut FILE_ID_INFO).cast(),
            std::mem::size_of::<FILE_ID_INFO>() as u32,
        )
    } == 0
    {
        return Err(io::Error::last_os_error().into());
    }
    Ok(format!(
        "{:016x}-{:032x}",
        info.VolumeSerialNumber,
        u128::from_le_bytes(info.FileId.Identifier)
    ))
}

pub(super) fn capture(root: &Path, relative: &Path) -> Result<String, VaultError> {
    identity(&open(root, relative, FILE_READ_ATTRIBUTES)?.file)
}

pub(super) fn rename(
    root: &Path,
    from: &Path,
    to: &Path,
    expected: &str,
) -> Result<(), VaultError> {
    let source = open(root, from, FILE_READ_ATTRIBUTES | DELETE)?;
    if expected.is_empty() || identity(&source.file)? != expected {
        return Err(refused());
    }
    let destination_parent = to.parent().ok_or_else(refused)?;
    let destination = if destination_parent.as_os_str().is_empty() {
        let file = fs::OpenOptions::new()
            .access_mode(FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY)
            .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE)
            .custom_flags(FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT)
            .open(root)?;
        directory(&file)?;
        PinnedEntry {
            file,
            _ancestors: Vec::new(),
        }
    } else {
        open(
            root,
            destination_parent,
            FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY,
        )?
    };
    directory(&destination.file)?;
    rename_opened(
        &source.file,
        &destination.file,
        to.file_name().ok_or_else(refused)?,
    )
}

fn rename_opened(source: &fs::File, parent: &fs::File, name: &OsStr) -> Result<(), VaultError> {
    let name: Vec<u16> = name.encode_wide().collect();
    if name.is_empty()
        || name == [46]
        || name == [46, 46]
        || name.iter().any(|c| matches!(*c, 0 | 47 | 58 | 92))
    {
        return Err(refused());
    }
    let name_bytes = name.len().checked_mul(2).ok_or_else(refused)?;
    let name_offset = std::mem::offset_of!(FILE_RENAME_INFORMATION, FileName);
    let length = std::mem::size_of::<FILE_RENAME_INFORMATION>().max(name_offset + name_bytes);
    let mut buffer = vec![0u64; length.div_ceil(8)];
    // SAFETY: aligned initialized buffer holds the fixed header and full UTF-16
    // name. ReplaceIfExists stays false. Both handles outlive the synchronous call.
    let status = unsafe {
        let info = buffer.as_mut_ptr().cast::<FILE_RENAME_INFORMATION>();
        (*info).RootDirectory = parent.as_raw_handle();
        (*info).FileNameLength = u32::try_from(name_bytes).map_err(|_| refused())?;
        std::ptr::copy_nonoverlapping(
            name.as_ptr(),
            buffer
                .as_mut_ptr()
                .cast::<u8>()
                .add(name_offset)
                .cast::<u16>(),
            name.len(),
        );
        let mut status_block: IO_STATUS_BLOCK = std::mem::zeroed();
        NtSetInformationFile(
            source.as_raw_handle(),
            &mut status_block,
            info.cast(),
            length as u32,
            FileRenameInformation,
        )
    };
    if status < 0 {
        return Err(
            io::Error::from_raw_os_error(unsafe { RtlNtStatusToDosError(status) } as i32).into(),
        );
    }
    Ok(())
}

#[cfg(test)]
#[path = "windows_entry_tests.rs"]
mod tests;
