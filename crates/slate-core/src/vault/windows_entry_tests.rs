// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use super::*;
use crate::vault::test_support::junction;

#[test]
fn conditional_rename_rejects_replacements_and_collisions() {
    let root = tempfile::tempdir().unwrap();
    fs::write(root.path().join("a.md"), b"original").unwrap();
    let expected = capture(root.path(), Path::new("a.md")).unwrap();
    fs::rename(root.path().join("a.md"), root.path().join("original.md")).unwrap();
    fs::write(root.path().join("a.md"), b"replacement").unwrap();
    assert!(rename(root.path(), Path::new("a.md"), Path::new("b.md"), &expected).is_err());
    assert_eq!(fs::read(root.path().join("a.md")).unwrap(), b"replacement");
    fs::write(root.path().join("b.md"), b"occupied").unwrap();
    let expected = capture(root.path(), Path::new("a.md")).unwrap();
    assert!(rename(root.path(), Path::new("a.md"), Path::new("b.md"), &expected).is_err());
    assert_eq!(fs::read(root.path().join("b.md")).unwrap(), b"occupied");
}

#[test]
fn conditional_rename_handles_case_only_inverse_and_directory_moves() {
    let root = tempfile::tempdir().unwrap();
    fs::create_dir(root.path().join("folder")).unwrap();
    fs::create_dir(root.path().join("dest")).unwrap();
    fs::write(root.path().join("folder/a.md"), b"original").unwrap();
    let id = capture(root.path(), Path::new("folder/a.md")).unwrap();
    rename(
        root.path(),
        Path::new("folder/a.md"),
        Path::new("folder/A.md"),
        &id,
    )
    .unwrap();
    rename(
        root.path(),
        Path::new("folder/A.md"),
        Path::new("folder/a.md"),
        &id,
    )
    .unwrap();
    let folder_id = capture(root.path(), Path::new("folder")).unwrap();
    rename(
        root.path(),
        Path::new("folder"),
        Path::new("dest/folder"),
        &folder_id,
    )
    .unwrap();
    assert_eq!(
        capture(root.path(), Path::new("dest/folder")).unwrap(),
        folder_id
    );
    assert_eq!(
        capture(root.path(), Path::new("dest/folder/a.md")).unwrap(),
        id
    );
}

#[test]
fn conditional_rename_source_handle_cannot_target_path_replacement() {
    let root = tempfile::tempdir().unwrap();
    fs::write(root.path().join("a.md"), b"original").unwrap();
    let source = fs::OpenOptions::new()
        .access_mode(FILE_READ_ATTRIBUTES | DELETE)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE)
        .open(root.path().join("a.md"))
        .unwrap();
    let id = identity(&source).unwrap();
    assert!(
        open(
            root.path(),
            Path::new("a.md"),
            FILE_READ_ATTRIBUTES | DELETE
        )
        .is_err(),
        "guarded opens refuse a pre-existing DELETE handle"
    );
    fs::rename(root.path().join("a.md"), root.path().join("elsewhere.md")).unwrap();
    fs::write(root.path().join("a.md"), b"replacement").unwrap();
    let parent = fs::OpenOptions::new()
        .access_mode(FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS)
        .open(root.path())
        .unwrap();
    rename_opened(&source, &parent, OsStr::new("b.md")).unwrap();
    assert_eq!(identity(&source).unwrap(), id);
    assert_eq!(fs::read(root.path().join("a.md")).unwrap(), b"replacement");
    assert_eq!(fs::read(root.path().join("b.md")).unwrap(), b"original");
}

#[test]
fn conditional_rename_destination_junction_conversion_refuses_without_escape() {
    let root = tempfile::tempdir().unwrap();
    let outside = tempfile::tempdir().unwrap();
    fs::write(root.path().join("a.md"), b"original").unwrap();
    fs::create_dir(root.path().join("dest")).unwrap();
    let source = open(
        root.path(),
        Path::new("a.md"),
        FILE_READ_ATTRIBUTES | DELETE,
    )
    .unwrap();
    let parent = open(
        root.path(),
        Path::new("dest"),
        FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY,
    )
    .unwrap();
    junction(&root.path().join("dest"), outside.path());
    assert!(rename_opened(&source.file, &parent.file, OsStr::new("b.md")).is_err());
    assert!(!outside.path().join("b.md").exists());
    assert_eq!(fs::read(root.path().join("a.md")).unwrap(), b"original");
    drop(parent);
    fs::remove_dir(root.path().join("dest")).unwrap();
}
