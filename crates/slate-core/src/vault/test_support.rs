// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Test-only helpers the vault's Windows facts share: the entry walk
//! (`windows_entry`), the Trash inventory (`trash_snapshot::windows`) and the
//! Trash handoff (`trash_handoff`) all substitute a junction for a folder.

use std::ffi::OsStr;
use std::os::windows::{ffi::OsStrExt as _, fs::OpenOptionsExt as _, io::AsRawHandle as _};
use std::path::Path;
use std::{fs, io};

use windows_sys::Win32::Storage::FileSystem::{
    FILE_FLAG_BACKUP_SEMANTICS, FILE_FLAG_OPEN_REPARSE_POINT, FILE_READ_ATTRIBUTES,
    FILE_SHARE_DELETE, FILE_SHARE_READ, FILE_SHARE_WRITE, FILE_WRITE_ATTRIBUTES,
};

/// Convert the empty directory `path` into a junction (a mount-point reparse
/// point) to `target`, through a handle that holds only attribute access, as
/// an unprivileged writer can.
pub(super) fn junction(path: &Path, target: &Path) {
    let file = fs::OpenOptions::new()
        // WRITE_ATTRIBUTES bypasses sharing arbitration: this is the concrete
        // race that defeated the original path-based walk's directory pins.
        .access_mode(FILE_READ_ATTRIBUTES | FILE_WRITE_ATTRIBUTES)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT)
        .open(path)
        .unwrap();
    let printable: Vec<u16> = target.as_os_str().encode_wide().collect();
    let substitute: Vec<u16> = OsStr::new(r"\??\")
        .encode_wide()
        .chain(printable.iter().copied())
        .collect();
    let sub_len = substitute.len() * 2;
    let print_len = printable.len() * 2;
    let mut buffer = vec![0u8; 16 + sub_len + 2 + print_len + 2];
    let data_len = (buffer.len() - 8) as u16;
    buffer[0..4].copy_from_slice(&0xa0000003u32.to_le_bytes());
    buffer[4..6].copy_from_slice(&data_len.to_le_bytes());
    buffer[10..12].copy_from_slice(&(sub_len as u16).to_le_bytes());
    buffer[12..14].copy_from_slice(&((sub_len + 2) as u16).to_le_bytes());
    buffer[14..16].copy_from_slice(&(print_len as u16).to_le_bytes());
    for (index, value) in substitute
        .iter()
        .chain([0].iter())
        .chain(printable.iter())
        .enumerate()
    {
        buffer[16 + index * 2..18 + index * 2].copy_from_slice(&value.to_le_bytes());
    }
    let mut returned = 0;
    // SAFETY: live handle, initialized mount-point reparse buffer, synchronous
    // call, no output buffer. FSCTL_SET_REPARSE_POINT from MS-FSCC.
    let result = unsafe {
        windows_sys::Win32::System::IO::DeviceIoControl(
            file.as_raw_handle(),
            0x000900a4,
            buffer.as_ptr().cast(),
            buffer.len() as u32,
            std::ptr::null_mut(),
            0,
            &mut returned,
            std::ptr::null_mut(),
        )
    };
    assert_ne!(result, 0, "{}", io::Error::last_os_error());
}
