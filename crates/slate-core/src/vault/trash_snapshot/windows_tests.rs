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

/// WB-24 (#1492): the Trash inventory takes a Cloud Files placeholder as the
/// folder or note it stands for, wherever it is: on the way to the selection,
/// as the selection, or inside a selected folder. A junction still refuses on
/// the way and as the selection, and nested in a selected folder it is a leaf,
/// counted once and never followed. `opaque_reparse_point` stands in for a
/// placeholder; with no filter behind its tag nothing can be made inside it,
/// so the rule on the way is checked on its handle, and a real placeholder
/// ancestor is the gated OneDrive probe's.
#[test]
fn the_inventory_admits_a_placeholder_anywhere_refuses_a_junction_on_the_path_and_counts_a_nested_junction_once()
 {
    use crate::vault::test_support::opaque_reparse_point;

    let vault = tempfile::tempdir().unwrap();
    let outside = tempfile::tempdir().unwrap();
    fs::write(outside.path().join("private.md"), b"outside").unwrap();
    let root = fs::OpenOptions::new()
        .access_mode(FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT)
        .open(vault.path())
        .unwrap();

    // On the way: a placeholder folder passes, a junction does not.
    fs::create_dir(vault.path().join("cloud")).unwrap();
    opaque_reparse_point(&vault.path().join("cloud"));
    check_directory(&open_child(&root, OsStr::new("cloud")).unwrap())
        .expect("a placeholder folder is a folder on the way");
    fs::create_dir(vault.path().join("j")).unwrap();
    junction(&vault.path().join("j"), outside.path());
    let through = snapshot(vault.path(), &vault.path().join("j").join("private.md")).unwrap_err();
    assert!(
        through
            .to_string()
            .contains("Cannot stage Trash through a symbolic link"),
        "{through}"
    );

    // As the selection: a placeholder folder and a placeholder note are what
    // they stand for; a junction is refused.
    let folder = snapshot(vault.path(), &vault.path().join("cloud")).unwrap();
    assert!(folder.is_directory);
    assert_eq!(folder.item_count, 0);
    fs::write(vault.path().join("note.md"), b"a placeholder note").unwrap();
    opaque_reparse_point(&vault.path().join("note.md"));
    let note = snapshot(vault.path(), &vault.path().join("note.md")).unwrap();
    assert!(!note.is_directory);
    let selected = snapshot(vault.path(), &vault.path().join("j")).unwrap_err();
    assert!(
        selected
            .to_string()
            .contains("Cannot stage Trash for a symbolic link"),
        "{selected}"
    );

    // Inside a selected folder: a placeholder is counted, and a junction
    // beside it is one leaf whose outside target is neither read nor counted.
    let f = vault.path().join("f");
    fs::create_dir(&f).unwrap();
    fs::write(f.join("x.md"), b"x").unwrap();
    fs::create_dir(f.join("nested-cloud")).unwrap();
    opaque_reparse_point(&f.join("nested-cloud"));
    fs::create_dir(f.join("nested-junction")).unwrap();
    junction(&f.join("nested-junction"), outside.path());
    let inventory = snapshot(vault.path(), &f).unwrap();
    assert_eq!(
        inventory.item_count, 3,
        "x.md, the placeholder and the junction, each once; private.md is never counted"
    );

    drop(root);
    fs::remove_dir(vault.path().join("j")).unwrap();
    fs::remove_dir(f.join("nested-junction")).unwrap();
    assert_eq!(
        fs::read(outside.path().join("private.md")).unwrap(),
        b"outside"
    );
}
