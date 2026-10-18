// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #1452: a vault folder swapped for a symlink after Slate indexed it can no
//! longer redirect a save or a new folder, through the session APIs the
//! hosts call: `save_text` and every edit that saves through it (a task
//! toggle, a property edit, a link rewrite), `create_folder`,
//! `create_folder_exclusive` and `create_exclusive`. Before the fix
//! `FsVaultProvider::write_file` and `create_dir` resolved the absolute path
//! and followed the link, so a note's bytes or a new folder landed outside
//! the vault while the save or the create reported success.

use super::structural_ancestor_links::{indexed_vault, swap_for_link, tree, write};
use super::*;

/// #1452's refusal: `InvalidPath` for the caller's path, naming the folder
/// that is a link, ending with what did not happen.
fn assert_refused(error: &VaultError, refused: &str, outcome: &str) {
    assert!(
        matches!(error, VaultError::InvalidPath { path, reason }
            if path == refused
                && reason.starts_with("\"sub\" is a symbolic link, not a folder in this vault")
                && reason.ends_with(outcome)),
        "expected the #1452 refusal of {refused:?} ({outcome:?}), got {error:?}"
    );
}

fn count(session: &VaultSession, table: &str) -> i64 {
    let conn = session.conn.lock().unwrap();
    conn.query_row(&format!("SELECT COUNT(*) FROM {table}"), [], |row| {
        row.get(0)
    })
    .unwrap()
}

/// The issue's probe, through the session: a save of an indexed note, of a
/// new name, and of a new name in a folder that does not exist yet, each
/// under the swapped folder. On main every one returned `Ok` and wrote the
/// bytes outside the vault. Each now refuses before anything is staged, so
/// the save's write-intent marker clears with it (contract 21, register
/// entry 13: `write_file`'s `InvalidPath` is a provable no-write).
#[test]
fn a_save_under_a_folder_swapped_for_a_link_refuses_and_writes_nothing() {
    let (tmp, session) = indexed_vault(&[("sub/note.md", "vault note"), ("c.md", "vault c")]);
    let outside = tempfile::tempdir().unwrap();
    swap_for_link(tmp.path(), "sub", outside.path());
    let outside_before = tree(outside.path());
    let vault_before = tree(tmp.path());

    for path in ["sub/note.md", "sub/new-note.md", "sub/new/deeper.md"] {
        let error = session.save_text(path, "saved text", None).unwrap_err();
        assert_refused(&error, path, "so nothing was saved");
    }

    assert_eq!(tree(outside.path()), outside_before, "outside changed");
    assert_eq!(tree(tmp.path()), vault_before, "vault changed");
    assert_eq!(count(&session, "text_write_intents"), 0, "a marker lingers");
}

/// The edits that rewrite a note read it through the swapped folder, here
/// from a copy of it outside the vault, and save it through the same write:
/// a save with the host's content hash, a task toggle and a property edit.
/// Each refuses, typed, and the copy outside is untouched. macOS refuses one
/// step earlier, when binding the note to its stored spelling finds it
/// outside the vault (#1077 I7); elsewhere the provider's write is the
/// refusal, where on main each of them overwrote the copy outside.
#[test]
fn edits_under_a_swapped_folder_refuse_and_leave_the_outside_copy_alone() {
    let text = "---\nstatus: draft\n---\n- [ ] a task\n";
    let (tmp, session) = indexed_vault(&[("sub/note.md", text)]);
    let outside = tempfile::tempdir().unwrap();
    write(outside.path(), "note.md", text);
    swap_for_link(tmp.path(), "sub", outside.path());
    let hash = crate::vault::content_hash(text.as_bytes());
    let outside_before = tree(outside.path());
    let vault_before = tree(tmp.path());

    for error in [
        session
            .save_text("sub/note.md", "replaced", Some(&hash))
            .unwrap_err(),
        session
            .toggle_task_status("sub/note.md", 0, 'x', Some(&hash))
            .unwrap_err(),
        session
            .set_property(
                "sub/note.md",
                "status",
                crate::PropertyValue::Text("done".into()),
                Some(&hash),
            )
            .unwrap_err(),
    ] {
        assert!(matches!(error, VaultError::InvalidPath { .. }), "{error:?}");
        #[cfg(not(target_os = "macos"))]
        assert_refused(&error, "sub/note.md", "so nothing was saved");
    }

    assert_eq!(tree(outside.path()), outside_before, "outside changed");
    assert_eq!(tree(tmp.path()), vault_before, "vault changed");
    assert_eq!(count(&session, "text_write_intents"), 0, "a marker lingers");
}

/// A rename's link rewrite is a save too. The rename itself stands; the
/// rewrite of the note under the swapped folder is reported failed and
/// writes nothing, inside the vault or out (on main, off macOS, it rewrote
/// the copy outside the vault).
#[test]
fn a_link_rewrite_under_a_swapped_folder_is_reported_failed_and_writes_nothing() {
    let text = "see [[c]]\n";
    let (tmp, session) = indexed_vault(&[("c.md", "target"), ("sub/note.md", text)]);
    let outside = tempfile::tempdir().unwrap();
    write(outside.path(), "note.md", text);
    swap_for_link(tmp.path(), "sub", outside.path());
    let outside_before = tree(outside.path());

    let report = session.rename_file("c.md", "d.md").unwrap();

    assert!(
        report
            .failed
            .iter()
            .any(|failure| failure.path == "sub/note.md"),
        "{report:?}"
    );
    assert!(
        report
            .rewritten
            .iter()
            .all(|outcome| outcome.path != "sub/note.md"),
        "{report:?}"
    );
    assert_eq!(tree(outside.path()), outside_before, "outside changed");
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("sub-parked/note.md")).unwrap(),
        text
    );
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("d.md")).unwrap(),
        "target"
    );
}

/// The issue's other probe, through the session: `create_folder` under the
/// swapped folder made the folder outside the vault and journaled it. It now
/// refuses and journals nothing. The exclusive creates (Finder import's
/// folder, and a new note) already refused, as an untyped `ENOTDIR`; they
/// now give the same typed refusal.
#[test]
fn creates_under_a_folder_swapped_for_a_link_refuse_and_create_nothing() {
    let (tmp, session) = indexed_vault(&[("sub/keep.md", "keep")]);
    let outside = tempfile::tempdir().unwrap();
    swap_for_link(tmp.path(), "sub", outside.path());
    let outside_before = tree(outside.path());
    let vault_before = tree(tmp.path());
    let journal_before = count(&session, "structural_ops");

    let folder = session.create_folder("sub/new-folder").unwrap_err();
    assert_refused(&folder, "sub/new-folder", "so no folder was created");
    let nested = session.create_folder("sub/new/deeper").unwrap_err();
    assert_refused(&nested, "sub/new/deeper", "so no folder was created");
    let imported = session.create_folder_exclusive("sub/imported").unwrap_err();
    assert_refused(&imported, "sub/imported", "so no folder was created");
    let note = session
        .create_exclusive("sub/new-note.md", "new")
        .unwrap_err();
    assert_refused(&note, "sub/new-note.md", "so nothing was created");

    assert_eq!(tree(outside.path()), outside_before, "outside changed");
    assert_eq!(tree(tmp.path()), vault_before, "vault changed");
    assert_eq!(
        count(&session, "structural_ops"),
        journal_before,
        "journaled"
    );
}

/// #1452 keeps saves and creates in real folders as they were: an edit of an
/// indexed note, a save into folders that do not exist yet, nested new
/// folders, and the exclusive creates.
#[test]
fn saves_and_creates_in_real_folders_still_land() {
    let (tmp, session) = indexed_vault(&[("sub/note.md", "vault note")]);
    let hash = crate::vault::content_hash(b"vault note");

    session
        .save_text("sub/note.md", "edited", Some(&hash))
        .unwrap();
    session
        .save_text("new/deeper/fresh.md", "fresh", None)
        .unwrap();
    session.create_folder("folders/nested").unwrap();
    session.create_folder_exclusive("sub/imported").unwrap();
    session
        .create_exclusive("sub/created.md", "created")
        .unwrap();

    for (path, contents) in [
        ("sub/note.md", "edited"),
        ("new/deeper/fresh.md", "fresh"),
        ("sub/created.md", "created"),
    ] {
        assert_eq!(
            std::fs::read_to_string(tmp.path().join(path)).unwrap(),
            contents
        );
    }
    for folder in ["new/deeper", "folders/nested", "sub/imported"] {
        let meta = tmp.path().join(folder).symlink_metadata().unwrap();
        assert!(meta.is_dir() && !meta.file_type().is_symlink(), "{folder}");
    }
}

/// A vault opened through a symlinked root still saves and creates: the
/// root is followed once, as the descriptor, and only what lies beneath it
/// is resolved without following a link.
#[test]
fn a_vault_opened_through_a_symlinked_root_still_saves_and_creates() {
    let tmp = tempfile::tempdir().unwrap();
    let real = tmp.path().join("real-vault");
    write(&real, "note.md", "vault note");
    let link = tmp.path().join("vault-link");
    std::os::unix::fs::symlink(&real, &link).unwrap();
    let session = VaultSession::from_filesystem(link).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    let hash = crate::vault::content_hash(b"vault note");

    session.save_text("note.md", "edited", Some(&hash)).unwrap();
    session.save_text("sub/fresh.md", "fresh", None).unwrap();
    session.create_folder("folder/nested").unwrap();
    session.create_exclusive("created.md", "created").unwrap();

    assert_eq!(
        std::fs::read_to_string(real.join("note.md")).unwrap(),
        "edited"
    );
    assert_eq!(
        std::fs::read_to_string(real.join("sub/fresh.md")).unwrap(),
        "fresh"
    );
    assert_eq!(
        std::fs::read_to_string(real.join("created.md")).unwrap(),
        "created"
    );
    assert!(real.join("folder/nested").is_dir());
}
