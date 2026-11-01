// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use super::*;
use crate::vault::test_support::junction;

/// The root an undo resolves beneath when no session holds one.
fn unheld(root: &Path) -> fs::File {
    open_unheld_root(root).unwrap()
}

#[test]
fn conditional_rename_rejects_replacements_and_collisions() {
    let root = tempfile::tempdir().unwrap();
    fs::write(root.path().join("a.md"), b"original").unwrap();
    let expected = capture(&unheld(root.path()), Path::new("a.md")).unwrap();
    fs::rename(root.path().join("a.md"), root.path().join("original.md")).unwrap();
    fs::write(root.path().join("a.md"), b"replacement").unwrap();
    assert!(
        rename(
            &unheld(root.path()),
            Path::new("a.md"),
            Path::new("b.md"),
            &expected
        )
        .is_err()
    );
    assert_eq!(fs::read(root.path().join("a.md")).unwrap(), b"replacement");
    fs::write(root.path().join("b.md"), b"occupied").unwrap();
    let expected = capture(&unheld(root.path()), Path::new("a.md")).unwrap();
    assert!(
        rename(
            &unheld(root.path()),
            Path::new("a.md"),
            Path::new("b.md"),
            &expected
        )
        .is_err()
    );
    assert_eq!(fs::read(root.path().join("b.md")).unwrap(), b"occupied");
}

#[test]
fn conditional_rename_handles_case_only_inverse_and_directory_moves() {
    let root = tempfile::tempdir().unwrap();
    fs::create_dir(root.path().join("folder")).unwrap();
    fs::create_dir(root.path().join("dest")).unwrap();
    fs::write(root.path().join("folder/a.md"), b"original").unwrap();
    let id = capture(&unheld(root.path()), Path::new("folder/a.md")).unwrap();
    rename(
        &unheld(root.path()),
        Path::new("folder/a.md"),
        Path::new("folder/A.md"),
        &id,
    )
    .unwrap();
    rename(
        &unheld(root.path()),
        Path::new("folder/A.md"),
        Path::new("folder/a.md"),
        &id,
    )
    .unwrap();
    let folder_id = capture(&unheld(root.path()), Path::new("folder")).unwrap();
    rename(
        &unheld(root.path()),
        Path::new("folder"),
        Path::new("dest/folder"),
        &folder_id,
    )
    .unwrap();
    assert_eq!(
        capture(&unheld(root.path()), Path::new("dest/folder")).unwrap(),
        folder_id
    );
    assert_eq!(
        capture(&unheld(root.path()), Path::new("dest/folder/a.md")).unwrap(),
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
            &unheld(root.path()),
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
        &unheld(root.path()),
        Path::new("a.md"),
        FILE_READ_ATTRIBUTES | DELETE,
    )
    .unwrap();
    let parent = open(
        &unheld(root.path()),
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

/// #1451: the walk forward moves, saves and creations take refuses a
/// junction and a file where a folder should be, naming each in
/// `pinned_root`'s words, and lets through a folder that is a reparse point
/// whose tag is not a name surrogate (what a OneDrive Files On-Demand folder
/// is to it). The Trash handoff's walk keeps refusing that one too (#1450).
#[test]
fn the_walk_refuses_links_and_files_and_admits_a_reparse_point_that_redirects_nothing() {
    use crate::vault::fs::Mutation;
    use crate::vault::test_support::{OPAQUE_REPARSE_TAG, opaque_reparse_point};

    let root = tempfile::tempdir().unwrap();
    let outside = tempfile::tempdir().unwrap();
    fs::create_dir_all(root.path().join("real/deeper")).unwrap();
    fs::write(root.path().join("real/plain.md"), b"a file").unwrap();
    fs::create_dir(root.path().join("real/linked")).unwrap();
    junction(&root.path().join("real/linked"), outside.path());
    fs::create_dir(root.path().join("opaque")).unwrap();
    opaque_reparse_point(&root.path().join("opaque"));
    let pinned = PinnedRoot::open(root.path()).unwrap();
    let walk = |parent: &str, folders| {
        pinned.folder(
            Path::new(parent),
            "the/original.md",
            false,
            Mutation::Save,
            folders,
        )
    };

    walk("real/deeper", Folders::NotLinks).unwrap();
    for (parent, expected) in [
        (
            "real/linked/x",
            "\"real/linked\" is a junction, not a folder in this vault, so nothing was saved",
        ),
        (
            "real/plain.md/x",
            "\"real/plain.md\" is not a folder, so nothing was saved",
        ),
    ] {
        let error = walk(parent, Folders::NotLinks).unwrap_err();
        assert!(
            matches!(&error, VaultError::InvalidPath { path, reason }
                if path == "the/original.md" && reason == expected),
            "{parent}: {error:?}"
        );
    }

    let opaque =
        open_relative(pinned.handle(), OsStr::new("opaque"), FILE_READ_ATTRIBUTES).unwrap();
    assert_eq!(found(&opaque).unwrap(), Found::ReparseFolder);
    let mut tag: FILE_ATTRIBUTE_TAG_INFO = unsafe { std::mem::zeroed() };
    // SAFETY: the buffer matches FileAttributeTagInfo and the handle is live.
    let queried = unsafe {
        GetFileInformationByHandleEx(
            opaque.as_raw_handle(),
            FileAttributeTagInfo,
            (&mut tag as *mut FILE_ATTRIBUTE_TAG_INFO).cast(),
            std::mem::size_of::<FILE_ATTRIBUTE_TAG_INFO>() as u32,
        )
    };
    assert_ne!(queried, 0, "{}", io::Error::last_os_error());
    assert_eq!(tag.ReparseTag, OPAQUE_REPARSE_TAG);
    drop(opaque);
    walk("opaque", Folders::NotLinks).expect("a reparse point that redirects nothing is a folder");
    let trash = walk("opaque", Folders::PlainOnly).unwrap_err();
    assert!(
        matches!(&trash, VaultError::InvalidPath { reason, .. }
            if reason == "\"opaque\" is a reparse point, not a plain folder, so nothing was saved"),
        "{trash:?}"
    );

    assert!(fs::read_dir(outside.path()).unwrap().next().is_none());
    drop(pinned);
    fs::remove_dir(root.path().join("real/linked")).unwrap();
    fs::remove_dir(root.path().join("opaque")).unwrap();
}

/// #1451: every folder the walk pinned stays open, unshared for deletion,
/// until the pinned value drops, so none of them can be renamed away in
/// between; and one converted into a junction in place after the walk turns
/// a later failure into the typed refusal naming it.
#[test]
fn a_pinned_chain_holds_its_folders_and_names_one_converted_after_the_walk() {
    use crate::vault::fs::Mutation;

    let root = tempfile::tempdir().unwrap();
    let outside = tempfile::tempdir().unwrap();
    fs::create_dir_all(root.path().join("a/b")).unwrap();
    let pinned = PinnedRoot::open(root.path()).unwrap();
    let chain = pinned
        .folder(
            Path::new("a/b"),
            "a/b/c.md",
            false,
            Mutation::Move,
            Folders::NotLinks,
        )
        .unwrap();

    for (folder, parked) in [("a", "a-parked"), ("a/b", "a/b-parked")] {
        assert!(
            fs::rename(root.path().join(folder), root.path().join(parked)).is_err(),
            "{folder} was renamed while pinned"
        );
    }
    assert!(matches!(
        chain.failed(io::Error::from(io::ErrorKind::Other)),
        VaultError::Io(_)
    ));

    junction(&root.path().join("a/b"), outside.path());
    let refusal = chain.failed(io::Error::from(io::ErrorKind::Other));
    let expected = "\"a/b\" is a junction, not a folder in this vault, so nothing was moved";
    assert!(
        matches!(&refusal, VaultError::InvalidPath { path, reason }
            if path == "a/b/c.md" && reason == expected),
        "{refusal:?}"
    );
    drop(chain);
    drop(pinned);
    fs::remove_dir(root.path().join("a/b")).unwrap();
    fs::rename(root.path().join("a"), root.path().join("a-renamed"))
        .expect("released once the pinned value drops");
}
