// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use super::*;
use crate::vault::test_support::junction;

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
    assert!(
        children(&folder, &mut 1, &crate::CancelToken::new())
            .unwrap()
            .is_empty()
    );
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
