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
/// is to it). Since WB-24 the Trash handoff's walk takes the same rule
/// (#1492).
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

/// WB-24 (#1492): undo and redo walk through a Cloud Files placeholder as the
/// folder or note it stands for, and still refuse a junction. In a OneDrive
/// Files On-Demand vault the root, every folder and every note is one.
/// `opaque_reparse_point` stands in for a placeholder, as for #1451's facts;
/// with no filter behind its tag nothing can be made inside it, so the
/// ancestor rule is checked on its handle, and a real placeholder ancestor
/// is the gated OneDrive probe's
/// (`what_undo_and_the_trash_do_in_a_onedrive_placeholder_vault`).
#[test]
fn undo_identity_is_captured_through_a_placeholder_folder_and_refused_through_a_junction() {
    use crate::vault::test_support::opaque_reparse_point;

    let root = tempfile::tempdir().unwrap();
    let outside = tempfile::tempdir().unwrap();
    fs::write(outside.path().join("x.md"), b"outside").unwrap();
    fs::create_dir(root.path().join("cloud")).unwrap();
    opaque_reparse_point(&root.path().join("cloud"));
    fs::write(root.path().join("placeholder.md"), b"a placeholder note").unwrap();
    opaque_reparse_point(&root.path().join("placeholder.md"));
    fs::create_dir(root.path().join("linked")).unwrap();
    junction(&root.path().join("linked"), outside.path());
    let held = unheld(root.path());

    // The folder rule every ancestor meets.
    let access = FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY;
    let cloud = open_relative(&held, OsStr::new("cloud"), access).unwrap();
    directory(&cloud).expect("a placeholder folder is a folder on the way");
    let linked = open_relative(&held, OsStr::new("linked"), access).unwrap();
    assert!(
        directory(&linked).is_err(),
        "a junction is not a folder on the way"
    );
    let note = open_relative(&held, OsStr::new("placeholder.md"), FILE_READ_ATTRIBUTES).unwrap();
    assert!(
        directory(&note).is_err(),
        "a note is not a folder on the way"
    );
    drop((cloud, linked, note));

    // A placeholder leaf is the note or folder it stands for.
    for item in ["cloud", "placeholder.md"] {
        capture(&held, Path::new(item)).unwrap_or_else(|error| {
            panic!("{item}: a placeholder is what it stands for: {error:?}")
        });
    }
    let id = capture(&held, Path::new("placeholder.md")).unwrap();
    rename(
        &held,
        Path::new("placeholder.md"),
        Path::new("renamed.md"),
        &id,
    )
    .unwrap();
    assert_eq!(
        capture(&held, Path::new("renamed.md")).unwrap(),
        id,
        "the same note, renamed"
    );

    // A junction refuses, above a note or as the item itself.
    for item in ["linked/x.md", "linked"] {
        let error = capture(&held, Path::new(item)).unwrap_err();
        assert!(
            matches!(&error, VaultError::InvalidArgument { message } if message.contains("Cannot undo or redo")),
            "{item}: {error:?}"
        );
    }
    drop(held);

    // A vault whose root is itself a placeholder opens for undo.
    let cloud_root = tempfile::tempdir().unwrap();
    let vault = cloud_root.path().join("vault");
    fs::create_dir(&vault).unwrap();
    opaque_reparse_point(&vault);
    open_unheld_root(&vault).expect("a placeholder vault root is a folder");

    assert_eq!(fs::read(outside.path().join("x.md")).unwrap(), b"outside");
    fs::remove_dir(root.path().join("linked")).unwrap();
}
