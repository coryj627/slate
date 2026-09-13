// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use super::*;
use std::os::windows::ffi::OsStrExt;

fn junction(path: &Path, target: &Path) {
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

#[test]
fn staged_trash_handle_walk_never_follows_attribute_only_junction_conversion() {
    let vault = tempfile::tempdir().unwrap();
    let outside = tempfile::tempdir().unwrap();
    fs::write(outside.path().join("private.md"), b"outside").unwrap();
    fs::create_dir(vault.path().join("folder")).unwrap();
    let root = fs::OpenOptions::new()
        .access_mode(FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT)
        .open(vault.path())
        .unwrap();
    let folder = open_child(&root, OsStr::new("folder")).unwrap();
    check_directory(&folder).unwrap();
    let before = stamp(&folder).unwrap();
    junction(&vault.path().join("folder"), outside.path());
    assert_ne!(before, stamp(&folder).unwrap());
    // Enumeration stays on the opened empty directory. Relative child lookup
    // must fail; it must never discover/open the outside target's private file.
    assert!(children(&folder, &mut 1).unwrap().is_empty());
    assert!(open_child(&folder, OsStr::new("private.md")).is_err());
    assert!(snapshot(vault.path(), &vault.path().join("folder")).is_err());
    drop(folder);
    drop(root);
    fs::remove_dir(vault.path().join("folder")).unwrap();
    assert_eq!(
        fs::read(outside.path().join("private.md")).unwrap(),
        b"outside"
    );
}

#[test]
fn staged_trash_handle_enumeration_spans_buffers_and_counts_links_once() {
    let vault = tempfile::tempdir().unwrap();
    let outside = tempfile::tempdir().unwrap();
    let folder = vault.path().join("folder");
    fs::create_dir(&folder).unwrap();
    for index in 0..1200 {
        fs::write(
            folder.join(format!("{index:04}-long-enough-for-multiple-buffers.md")),
            b"",
        )
        .unwrap();
    }
    fs::create_dir(folder.join("link")).unwrap();
    fs::write(outside.path().join("private.md"), b"outside").unwrap();
    junction(&folder.join("link"), outside.path());
    let inventory = snapshot(vault.path(), &folder).unwrap();
    assert_eq!(inventory.item_count, 1201);
    assert!(snapshot(vault.path(), &folder.join("link/private.md")).is_err());
    fs::remove_dir(folder.join("link")).unwrap();
}
