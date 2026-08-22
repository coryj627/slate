// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #1077 contracts I2/I3/I4 (Phases 1–2): every mutation target binds to
//! the filesystem's stored spelling, so one physical file has ONE index
//! row, ONE marker key, and ONE epoch row — and a create keeps the leaf
//! the user chose while taking its parent's stored spelling.
//!
//! Volume-probed, never assumed: the aliasing facts run only where the
//! volume actually aliases `notes/alpha.md` onto `Notes/Alpha.md` (NTFS,
//! APFS) and are vacuous on ext4. The probe is the filesystem itself,
//! not a platform cfg.

use super::common::*;
use super::*;

/// A scanned vault holding `Notes/Alpha.md`, or `None` on a volume that
/// does not alias `notes/alpha.md` onto it.
fn aliasing_vault() -> Option<(tempfile::TempDir, VaultSession)> {
    let (tmp, session) = make_vault(|provider| {
        provider.write_file("Notes/Alpha.md", b"# alpha\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    if !tmp.path().join("notes").join("alpha.md").is_file() {
        return None;
    }
    Some((tmp, session))
}

fn index_paths(session: &VaultSession, table: &str) -> Vec<String> {
    let conn = session.conn.lock().unwrap();
    let mut stmt = conn
        .prepare(&format!("SELECT path FROM {table} ORDER BY path"))
        .unwrap();
    let rows = stmt.query_map([], |row| row.get::<_, String>(0)).unwrap();
    rows.map(|row| row.unwrap()).collect()
}

fn strings(items: &[&str]) -> Vec<String> {
    items.iter().map(|item| item.to_string()).collect()
}

#[test]
fn a_save_through_an_alias_updates_the_stored_row_not_a_second_one() {
    let Some((tmp, session)) = aliasing_vault() else {
        return;
    };
    session
        .save_text("notes/alpha.md", "# alpha, edited\n", None)
        .unwrap();
    // ONE row, the stored spelling's — not a second one under the alias.
    assert_eq!(index_paths(&session, "files"), strings(&["Notes/Alpha.md"]));
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("Notes").join("Alpha.md")).unwrap(),
        "# alpha, edited\n"
    );
    assert_eq!(
        session.read_text("Notes/Alpha.md").unwrap(),
        "# alpha, edited\n"
    );
}

#[test]
fn a_new_file_keeps_its_leaf_and_takes_its_parents_stored_spelling() {
    let Some((tmp, session)) = aliasing_vault() else {
        return;
    };
    session
        .save_text("notes/Fresh.md", "fresh\n", None)
        .unwrap();
    assert_eq!(
        index_paths(&session, "files"),
        strings(&["Notes/Alpha.md", "Notes/Fresh.md"])
    );
    assert!(tmp.path().join("Notes").join("Fresh.md").is_file());

    // The create primitive agrees (I4): the leaf the user chose — beyond
    // ASCII, untouched — under the parent's stored spelling.
    session
        .create_exclusive("notes/\u{00c4}rger.md", "\u{00e4}\n")
        .unwrap();
    assert!(index_paths(&session, "files").contains(&"Notes/\u{00c4}rger.md".to_string()));
    // A create through an alias of an EXISTING file is still refused.
    assert!(matches!(
        session.create_exclusive("notes/alpha.md", "x"),
        Err(VaultError::DestinationExists { .. })
    ));
}

#[test]
fn a_folder_created_through_an_alias_parent_binds_the_parents_stored_spelling() {
    let Some((tmp, session)) = aliasing_vault() else {
        return;
    };
    session.create_folder("notes/Sub").unwrap();
    assert_eq!(
        index_paths(&session, "dirs"),
        strings(&["Notes", "Notes/Sub"])
    );
    assert!(tmp.path().join("Notes").join("Sub").is_dir());
}

#[test]
fn a_delete_through_an_alias_removes_the_stored_row() {
    let Some((tmp, session)) = aliasing_vault() else {
        return;
    };
    session.delete_file("notes/alpha.md").unwrap();
    assert!(index_paths(&session, "files").is_empty());
    assert!(!tmp.path().join("Notes").join("Alpha.md").exists());
}

#[test]
fn a_rename_through_an_alias_source_reports_and_indexes_the_stored_spelling() {
    let Some((tmp, session)) = aliasing_vault() else {
        return;
    };
    let report = session.rename_file("notes/alpha.md", "Beta.md").unwrap();
    // The report carries the truth: the stored source spelling, and the
    // destination under the parent's stored spelling with its leaf
    // verbatim (I4).
    assert_eq!(
        report.moved,
        vec![("Notes/Alpha.md".to_string(), "Notes/Beta.md".to_string())]
    );
    assert_eq!(index_paths(&session, "files"), strings(&["Notes/Beta.md"]));
    assert!(tmp.path().join("Notes").join("Beta.md").is_file());
}

#[test]
fn a_folder_rename_through_an_alias_source_binds_the_stored_spelling() {
    let Some((tmp, session)) = aliasing_vault() else {
        return;
    };
    let report = session.rename_folder("notes", "Archive").unwrap();
    assert_eq!(
        report.moved,
        vec![("Notes/Alpha.md".to_string(), "Archive/Alpha.md".to_string())]
    );
    assert_eq!(index_paths(&session, "dirs"), strings(&["Archive"]));
    assert_eq!(
        index_paths(&session, "files"),
        strings(&["Archive/Alpha.md"])
    );
    assert!(tmp.path().join("Archive").join("Alpha.md").is_file());
}

#[test]
fn a_delete_of_a_folder_through_an_alias_removes_the_stored_subtree() {
    let Some((tmp, session)) = aliasing_vault() else {
        return;
    };
    session.delete_folder("notes").unwrap();
    assert!(index_paths(&session, "dirs").is_empty());
    assert!(index_paths(&session, "files").is_empty());
    assert!(!tmp.path().join("Notes").exists());
}

/// Phase 2 (I3): the durable write-intent marker is keyed by the stored
/// spelling, so a marker planted through one spelling is found — and
/// cleared — through any alias. Stranded deliberately with the
/// pre-write + intent-clear seams (the tasks-module precedent) so the
/// key is observable.
#[test]
fn a_marker_planted_through_an_alias_is_keyed_by_the_stored_spelling() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    let Some((_tmp, session)) = aliasing_vault() else {
        return;
    };
    // The pre-write seam sits inside the compare-and-swap read, so the
    // faulted save must carry an expected hash (the tasks precedent);
    // take it from a plain save through the stored spelling.
    let current_hash = session
        .save_text(
            "Notes/Alpha.md",
            "# alpha
",
            None,
        )
        .unwrap()
        .new_content_hash;
    // The trigger matches BOTH spellings, so it cannot itself decide the
    // outcome.
    unsafe { std::env::set_var("SLATE_TEST_FAULT_PRE_WRITE", "lpha.md") };
    unsafe { std::env::set_var("SLATE_TEST_FAULT_INTENT_CLEAR", "lpha.md") };
    let result = session.save_text(
        "notes/alpha.md",
        "# edited
",
        Some(&current_hash),
    );
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_INTENT_CLEAR") };
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_PRE_WRITE") };
    assert!(result.is_err(), "the pre-write fault refuses the save");
    assert_eq!(
        index_paths(&session, "text_write_intents"),
        strings(&["Notes/Alpha.md"]),
        "the stranded marker is keyed by the stored spelling, not the alias the save came through"
    );
}
