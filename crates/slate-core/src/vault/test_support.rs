// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Test-only helpers the vault's Windows facts share: the entry walk
//! (`windows_entry`), the Trash inventory (`trash_snapshot::windows`), the
//! Trash handoff (`trash_handoff`), the provider's moves, saves and creations
//! (`fs`, #1451) and the session's (`session::tests`) all substitute a
//! junction for a folder.

use std::ffi::OsStr;
use std::os::windows::{ffi::OsStrExt as _, fs::OpenOptionsExt as _, io::AsRawHandle as _};
use std::path::Path;
use std::{fs, io};

use windows_sys::Win32::Storage::FileSystem::{
    FILE_FLAG_BACKUP_SEMANTICS, FILE_FLAG_OPEN_REPARSE_POINT, FILE_READ_ATTRIBUTES,
    FILE_SHARE_DELETE, FILE_SHARE_READ, FILE_SHARE_WRITE, FILE_WRITE_ATTRIBUTES,
};

/// `IO_REPARSE_TAG_MOUNT_POINT` (MS-FSCC 2.1.2.1): a junction. These two are
/// named here rather than by enabling more `windows-sys` features for test
/// constants.
const IO_REPARSE_TAG_MOUNT_POINT: u32 = 0xA000_0003;

/// `FSCTL_SET_REPARSE_POINT` (MS-FSCC 2.3.65), the control code that writes
/// the reparse buffer.
const FSCTL_SET_REPARSE_POINT: u32 = 0x0009_00A4;

/// Write `buffer`, a reparse buffer, onto the empty directory `path` through a
/// handle that holds only attribute access, as an unprivileged writer can.
fn set_reparse_point(path: &Path, buffer: &[u8]) {
    let file = fs::OpenOptions::new()
        // WRITE_ATTRIBUTES bypasses sharing arbitration: this is the concrete
        // race that defeated the original path-based walk's directory pins.
        .access_mode(FILE_READ_ATTRIBUTES | FILE_WRITE_ATTRIBUTES)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT)
        .open(path)
        .unwrap();
    let mut returned = 0;
    // SAFETY: live handle, initialized reparse buffer, synchronous call, no
    // output buffer.
    let result = unsafe {
        windows_sys::Win32::System::IO::DeviceIoControl(
            file.as_raw_handle(),
            FSCTL_SET_REPARSE_POINT,
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

/// Convert the empty directory `path` into a junction (a mount-point reparse
/// point) to `target`, through a handle that holds only attribute access, as
/// an unprivileged writer can. `target` need not exist: a junction to a
/// missing folder dangles.
pub(crate) fn junction(path: &Path, target: &Path) {
    let printable: Vec<u16> = target.as_os_str().encode_wide().collect();
    let substitute: Vec<u16> = OsStr::new(r"\??\")
        .encode_wide()
        .chain(printable.iter().copied())
        .collect();
    let sub_len = substitute.len() * 2;
    let print_len = printable.len() * 2;
    let mut buffer = vec![0u8; 16 + sub_len + 2 + print_len + 2];
    let data_len = (buffer.len() - 8) as u16;
    buffer[0..4].copy_from_slice(&IO_REPARSE_TAG_MOUNT_POINT.to_le_bytes());
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
    set_reparse_point(path, &buffer);
}

/// A third-party reparse tag (MS-FSCC 2.1.2.1: the Microsoft bit clear, the
/// name-surrogate bit clear, no reserved bit set) that no filter on a test
/// machine owns.
pub(crate) const OPAQUE_REPARSE_TAG: u32 = 0x0000_1451;

/// Make the empty directory `path` a reparse point that redirects nothing:
/// [`OPAQUE_REPARSE_TAG`], whose name-surrogate bit is clear, in a
/// `REPARSE_GUID_DATA_BUFFER` (a third-party tag carries its owner's GUID).
/// To the walk this is what a Cloud Files placeholder (a OneDrive Files
/// On-Demand folder) is: a folder, not a link (#1451).
pub(crate) fn opaque_reparse_point(path: &Path) {
    // An owner GUID of this test's own; any non-zero value serves.
    const OWNER: [u8; 16] = *b"slate-1451-tests";
    let data = *b"1451";
    let mut buffer = Vec::with_capacity(24 + data.len());
    buffer.extend_from_slice(&OPAQUE_REPARSE_TAG.to_le_bytes());
    buffer.extend_from_slice(&(data.len() as u16).to_le_bytes());
    buffer.extend_from_slice(&0u16.to_le_bytes());
    buffer.extend_from_slice(&OWNER);
    buffer.extend_from_slice(&data);
    set_reparse_point(path, &buffer);
}
