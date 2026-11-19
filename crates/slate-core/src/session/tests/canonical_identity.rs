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
    let mut _env = fault_guard();
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
    _env.set("SLATE_TEST_FAULT_PRE_WRITE", "lpha.md");
    _env.set("SLATE_TEST_FAULT_INTENT_CLEAR", "lpha.md");
    let result = session.save_text(
        "notes/alpha.md",
        "# edited
",
        Some(&current_hash),
    );
    _env.clear("SLATE_TEST_FAULT_INTENT_CLEAR");
    _env.clear("SLATE_TEST_FAULT_PRE_WRITE");
    assert!(result.is_err(), "the pre-write fault refuses the save");
    assert_eq!(
        index_paths(&session, "text_write_intents"),
        strings(&["Notes/Alpha.md"]),
        "the stranded marker is keyed by the stored spelling, not the alias the save came through"
    );
}

// ---- Phase 3: the collision gate (contract I5) and the case-only rename (I4) ----

/// I5: the gate is a PORTABILITY guard — it runs on EVERY volume, so these
/// refusals hold on ext4 too (deliberately not volume-probed).
#[test]
fn the_gate_refuses_a_collision_beyond_ascii() {
    let (_tmp, session) = make_vault(|provider| {
        provider.write_file("\u{00c4}rger.md", b"a").unwrap(); // Ärger
        provider.create_dir("\u{00dc}bung").unwrap(); // Übung
        provider.write_file("caf\u{00e9}.md", b"c").unwrap(); // NFC
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    // Case beyond ASCII — the pair the old lower() gate could not see.
    assert!(matches!(
        session.create_exclusive("\u{00e4}rger.md", "x"),
        Err(VaultError::DestinationExists { path }) if path == "\u{00c4}rger.md"
    ));
    assert!(matches!(
        session.create_folder("\u{00fc}bung"),
        Err(VaultError::DestinationExists { path }) if path == "\u{00dc}bung"
    ));
    // Normalization: NFD beside NFC is one name on APFS.
    assert!(matches!(
        session.create_exclusive("cafe\u{0301}.md", "x"),
        Err(VaultError::DestinationExists { path }) if path == "caf\u{00e9}.md"
    ));
}

/// I5: kanji, kana, and width are NOT folded — the gate leaves them alone
/// (finding 1: no case to fold; NFC keeps full-width distinct, as the
/// volumes do).
#[test]
fn the_gate_leaves_cjk_and_width_alone() {
    let (_tmp, session) = make_vault(|provider| {
        provider.write_file("\u{65e5}\u{672c}.md", b"a").unwrap(); // 日本
        provider.write_file("ABC.md", b"b").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session
        .create_exclusive("\u{65e5}\u{672c}\u{8a9e}.md", "x") // 日本語
        .unwrap();
    session
        .create_exclusive("\u{ff21}\u{ff22}\u{ff23}.md", "x") // ＡＢＣ
        .unwrap();
}

/// I4: the case-only rename BECOMES expressible — file and folder — and
/// moves the row; a rename onto a DIFFERENT file's alias stays refused.
/// Holds on every volume (on a case-sensitive one it is simply a rename).
#[test]
fn a_case_only_rename_succeeds_and_moves_the_row() {
    let (tmp, session) = make_vault(|provider| {
        provider.write_file("A.md", b"a").unwrap();
        provider.write_file("B.md", b"b").unwrap();
        provider.create_dir("Docs").unwrap();
        provider.write_file("Docs/Note.md", b"n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let report = session.rename_file("A.md", "a.md").unwrap();
    assert_eq!(report.moved, vec![("A.md".to_string(), "a.md".to_string())]);
    let report = session.rename_folder("Docs", "docs").unwrap();
    assert!(
        report
            .moved
            .contains(&("Docs/Note.md".to_string(), "docs/Note.md".to_string())),
        "{:?}",
        report.moved
    );
    assert_eq!(
        index_paths(&session, "files"),
        strings(&["B.md", "a.md", "docs/Note.md"])
    );
    assert_eq!(index_paths(&session, "dirs"), strings(&["docs"]));

    // The directory entries carry the new spelling.
    let names: Vec<String> = std::fs::read_dir(tmp.path())
        .unwrap()
        .map(|entry| entry.unwrap().file_name().to_string_lossy().into_owned())
        .filter(|name| name != ".slate")
        .collect();
    assert!(names.contains(&"a.md".to_string()), "{names:?}");
    assert!(names.contains(&"docs".to_string()), "{names:?}");
    assert!(!names.contains(&"A.md".to_string()), "{names:?}");
    assert!(!names.contains(&"Docs".to_string()), "{names:?}");

    // Onto a DIFFERENT file's alias: refused, and nothing moved.
    assert!(matches!(
        session.rename_file("a.md", "b.md"),
        Err(VaultError::DestinationExists { path }) if path == "B.md"
    ));
    assert_eq!(
        index_paths(&session, "files"),
        strings(&["B.md", "a.md", "docs/Note.md"])
    );
}

/// I4 for the folder+note pair: `rename_folder_with_note` renames the
/// folder AND its same-named note, and a case-only rename of both is legal
/// (the collision probe finds the old note's own row).
#[test]
fn a_case_only_folder_note_rename_succeeds() {
    let (_tmp, session) = make_vault(|provider| {
        provider.create_dir("Docs").unwrap();
        provider.write_file("Docs/Docs.md", b"n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let report = session.rename_folder_with_note("Docs", "docs").unwrap();
    assert!(
        report
            .moved
            .contains(&("Docs/Docs.md".to_string(), "docs/docs.md".to_string())),
        "{:?}",
        report.moved
    );
    assert_eq!(index_paths(&session, "files"), strings(&["docs/docs.md"]));
    assert_eq!(index_paths(&session, "dirs"), strings(&["docs"]));
}

// ---- Plan 47 CB-13 (#1727, CD7): a case-only rename the volume ignored ----

/// The owner's sentence for a case-only rename the volume did not apply
/// (CD7), written out here and not read from the product, so this file is a
/// golden against core's copy.
#[cfg(windows)]
fn case_only_refusal(stored: &str, requested: &str) -> String {
    format!(
        "This drive keeps the capitalization a file was created with, so {stored} was not renamed to {requested}."
    )
}

/// The names in a folder of the vault, `.slate` aside, sorted. A listing,
/// because an existence check is case-blind on an aliasing volume.
#[cfg(windows)]
fn root_listing(root: &std::path::Path) -> Vec<String> {
    let mut names: Vec<String> = std::fs::read_dir(root)
        .unwrap()
        .map(|entry| entry.unwrap().file_name().to_string_lossy().into_owned())
        .filter(|name| name != ".slate")
        .collect();
    names.sort();
    names
}

/// #1727: FAT32 and exFAT have been seen to report a case-only rename as
/// done and keep the old spelling. The seam stands in for such a volume on
/// NTFS: the rename is refused in the owner's sentence, for a file and for a
/// folder with its note, and the index, the other note's link and the
/// volume's own names stay as they were. With the seam off both renames
/// succeed.
#[cfg(windows)]
#[test]
fn a_case_only_rename_the_volume_ignored_is_refused_and_nothing_else_changes() {
    let mut env = fault_guard();
    let (tmp, session) = make_vault(|provider| {
        provider.write_file("Note.md", b"- [ ] note\n").unwrap();
        provider.write_file("Other.md", b"[[Note]]\n").unwrap();
        provider.create_dir("Docs").unwrap();
        provider
            .write_file("Docs/Docs.md", b"- [ ] docs\n")
            .unwrap();
        provider
            .write_file("Docs/Inner.md", b"- [ ] inner\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let files_before = strings(&["Docs/Docs.md", "Docs/Inner.md", "Note.md", "Other.md"]);
    assert_eq!(index_paths(&session, "files"), files_before);
    assert_eq!(index_paths(&session, "dirs"), strings(&["Docs"]));

    // The seam matches a provider by a substring of its root path: this
    // vault's own temporary folder name, which no other vault has.
    let vault_name = tmp
        .path()
        .file_name()
        .unwrap()
        .to_string_lossy()
        .into_owned();
    env.set("SLATE_TEST_CASE_ONLY_RENAME_IGNORED", &vault_name);
    let renamed_names = ["Note.md", "note.md", "Docs", "docs"];
    for path in renamed_names {
        assert!(
            !session.provider.is_own_echo(path),
            "premise: nothing has recorded {path} as this provider's echo"
        );
    }

    let error = session
        .rename_file("Note.md", "note.md")
        .expect_err("a case-only rename the volume ignored is refused, not reported as done");
    assert!(
        matches!(
            &error,
            VaultError::InvalidArgument { message }
                if *message == case_only_refusal("Note.md", "note.md")
        ),
        "{error:?}"
    );
    let error = session
        .rename_folder_with_note("Docs", "docs")
        .expect_err("a folder's case-only rename the volume ignored is refused too");
    assert!(
        matches!(
            &error,
            VaultError::InvalidArgument { message }
                if *message == case_only_refusal("Docs", "docs")
        ),
        "{error:?}"
    );

    // A refused rename is a failed mutation and records no echo: neither
    // spelling is this provider's own afterwards. An echo recorded here
    // would take the entry's current state for Slate's own, and an outside
    // edit whose event was still pending would be dropped with it
    // (`watch::an_outside_edit_pending_at_a_refused_case_only_rename_still_calls_back`).
    for path in renamed_names {
        assert!(
            !session.provider.is_own_echo(path),
            "{path} was recorded as this provider's echo by a refusal"
        );
    }

    // Nothing else changed: the rows, the other note's link, the names.
    assert_eq!(index_paths(&session, "files"), files_before);
    assert_eq!(index_paths(&session, "dirs"), strings(&["Docs"]));
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("Other.md")).unwrap(),
        "[[Note]]\n"
    );
    assert_eq!(
        root_listing(tmp.path()),
        strings(&["Docs", "Note.md", "Other.md"])
    );
    assert_eq!(
        root_listing(&tmp.path().join("Docs")),
        strings(&["Docs.md", "Inner.md"])
    );

    // Each refusal left its markers under both spellings: for the note, and
    // for each file in the folder. The next vault-wide task query settles
    // them under the spelling the volume stores (#1975), so each task is
    // returned once, the rows are the old paths and nothing else, and no
    // marker is left.
    assert_eq!(
        vault_tasks(&session),
        strings(&[
            "Docs/Docs.md: docs",
            "Docs/Inner.md: inner",
            "Note.md: note"
        ])
    );
    let files = index_paths(&session, "files");
    assert_eq!(files, files_before);
    assert!(
        !files
            .iter()
            .any(|path| path == "note.md" || path.starts_with("docs/")),
        "a row stands under a spelling the volume does not store: {files:?}"
    );
    assert_eq!(index_paths(&session, "dirs"), strings(&["Docs"]));
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());

    // With the seam off the volume applies the rename and core reports it.
    env.clear("SLATE_TEST_CASE_ONLY_RENAME_IGNORED");
    let report = session.rename_file("Note.md", "note.md").unwrap();
    assert_eq!(
        report.moved,
        vec![("Note.md".to_string(), "note.md".to_string())]
    );
    let report = session.rename_folder_with_note("Docs", "docs").unwrap();
    assert!(
        report
            .moved
            .contains(&("Docs/Docs.md".to_string(), "docs/docs.md".to_string())),
        "{:?}",
        report.moved
    );
    assert_eq!(
        index_paths(&session, "files"),
        strings(&["Other.md", "docs/Inner.md", "docs/docs.md", "note.md"])
    );
    assert_eq!(index_paths(&session, "dirs"), strings(&["docs"]));
    assert_eq!(
        root_listing(tmp.path()),
        strings(&["Other.md", "docs", "note.md"])
    );
}

// ---- #1975: a marker under an alias spelling adds no second row (I3) ----

/// Contract 32 I3 after a case-only rename (found by plan 47 CB-13's step
/// 0, #1975): the rename plants a marker under each spelling, and the next
/// vault-wide task query sweeps them. One file keeps one row, under the
/// spelling the volume holds, and no marker is left.
#[test]
fn a_case_only_rename_leaves_one_row_after_the_next_task_query() {
    let (_tmp, session) = make_vault(|provider| {
        provider.write_file("A.md", b"- [ ] one\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session.rename_file("A.md", "a.md").unwrap();
    bind_history(&session, "a.md", "kept-1975");

    session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(10))
        .unwrap();

    assert_eq!(index_paths(&session, "files"), strings(&["a.md"]));
    assert_eq!(
        index_paths(&session, "text_write_intents"),
        Vec::<String>::new()
    );
    // The row is the note's own, read again in place: the sweep removes a
    // row only under a spelling the volume does not store.
    assert_eq!(
        history_binding(&session, "a.md").as_deref(),
        Some("kept-1975")
    );
}

/// Gives the `files` row under `path` a history binding. A read of the note
/// updates the row in place and keeps the binding; a row that was removed
/// and inserted again has none, whatever id it was given.
fn bind_history(session: &VaultSession, path: &str, name: &str) {
    let conn = session.conn.lock().unwrap();
    let changed = conn
        .execute(
            "UPDATE files SET oplog_name = ?2 WHERE path = ?1",
            rusqlite::params![path, name],
        )
        .unwrap();
    assert_eq!(changed, 1, "premise: a row stands under {path}");
}

/// The history binding of the `files` row under `path`.
fn history_binding(session: &VaultSession, path: &str) -> Option<String> {
    let conn = session.conn.lock().unwrap();
    conn.query_row(
        "SELECT oplog_name FROM files WHERE path = ?1",
        rusqlite::params![path],
        |row| row.get(0),
    )
    .unwrap()
}

/// The same after a case-only rename that failed (#1975): a holder that
/// shares no deletion refuses the rename's own open after both markers are
/// planted. The next vault-wide task query leaves the one row under the old
/// spelling and no marker.
#[cfg(windows)]
#[test]
fn a_failed_case_only_rename_leaves_one_row_after_the_next_task_query() {
    use std::os::windows::fs::OpenOptionsExt as _;
    use windows_sys::Win32::Storage::FileSystem::{FILE_SHARE_READ, FILE_SHARE_WRITE};

    let (tmp, session) = make_vault(|provider| {
        provider.write_file("A.md", b"- [ ] one\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let holder = std::fs::OpenOptions::new()
        .read(true)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE)
        .open(tmp.path().join("A.md"))
        .unwrap();
    let outcome = session.rename_file("A.md", "a.md");
    drop(holder);
    assert!(
        outcome.is_err(),
        "premise: the holder refuses the rename: {outcome:?}"
    );
    assert_eq!(root_listing(tmp.path()), strings(&["A.md"]));
    let recorder = ChangeRecorder::on(&session);

    session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(10))
        .unwrap();

    assert_eq!(index_paths(&session, "files"), strings(&["A.md"]));
    assert_eq!(
        index_paths(&session, "text_write_intents"),
        Vec::<String>::new()
    );
    // A host hears the note read again, once, under the spelling it kept,
    // and nothing for the spelling that was asked for.
    assert_eq!(recorder.take(), strings(&["Modified A.md"]));
}

/// The vault-wide task list as `path: text`, in the order the query gives.
fn vault_tasks(session: &VaultSession) -> Vec<String> {
    session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .unwrap()
        .items
        .into_iter()
        .map(|task| format!("{}: {}", task.path, task.task.text))
        .collect()
}

fn no_markers() -> Vec<String> {
    Vec::new()
}

/// A marker as a mover or a writer leaves one: held, as a move plants it
/// before the kernel call, or aged, as a move leaves it when it lands.
fn plant_marker(session: &VaultSession, path: &str, held: bool) {
    let created_ms = if held {
        VaultSession::HELD_MOVE_MARKER_MS
    } else {
        1
    };
    let conn = session.conn.lock().unwrap();
    conn.execute(
        "INSERT INTO text_write_intents(path, created_ms, token, registered_epoch)
         VALUES (?1, ?2, ?3, COALESCE((SELECT index_epoch FROM files WHERE path = ?1), 0))",
        rusqlite::params![path, created_ms, fresh_intent_token()],
    )
    .unwrap();
}

/// A marker with a token and a registration epoch the fact chooses.
fn plant_marker_as(session: &VaultSession, path: &str, token: i64, held: bool, epoch: i64) {
    let created_ms = if held {
        VaultSession::HELD_MOVE_MARKER_MS
    } else {
        1
    };
    let conn = session.conn.lock().unwrap();
    conn.execute(
        "INSERT INTO text_write_intents(path, created_ms, token, registered_epoch)
         VALUES (?1, ?2, ?3, ?4)",
        rusqlite::params![path, created_ms, token, epoch],
    )
    .unwrap();
}

/// Every marker as (path, token, held, registered epoch), by path and token.
fn marker_rows(session: &VaultSession) -> Vec<(String, i64, bool, i64)> {
    let conn = session.conn.lock().unwrap();
    let mut stmt = conn
        .prepare(
            "SELECT path, token, created_ms, registered_epoch FROM text_write_intents
             ORDER BY path, token",
        )
        .unwrap();
    let rows = stmt
        .query_map([], |row| {
            Ok((
                row.get::<_, String>(0)?,
                row.get::<_, i64>(1)?,
                row.get::<_, i64>(2)? == VaultSession::HELD_MOVE_MARKER_MS,
                row.get::<_, i64>(3)?,
            ))
        })
        .unwrap();
    rows.map(|row| row.unwrap()).collect()
}

/// Records every file-change notification, in the order it arrives.
#[derive(Default)]
struct ChangeRecorder {
    changes: std::sync::Mutex<Vec<FileChangeEvent>>,
}

impl VaultEventListener for ChangeRecorder {
    fn on_error(&self, _code: EventErrorCode, _path: String, _message: String) {}
    fn on_file_change(&self, event: FileChangeEvent) {
        self.changes.lock().unwrap().push(event);
    }
}

impl ChangeRecorder {
    fn on(session: &VaultSession) -> Arc<ChangeRecorder> {
        let recorder = Arc::new(ChangeRecorder::default());
        session.register_event_listener(recorder.clone());
        recorder
    }

    /// The notifications so far as `Kind path`, or `Kind path <- previous`
    /// for one that carries a previous path, taken out of the record.
    fn take(&self) -> Vec<String> {
        self.changes
            .lock()
            .unwrap()
            .drain(..)
            .map(|event| match event.previous_path {
                Some(previous) => format!("{:?} {} <- {previous}", event.kind, event.path),
                None => format!("{:?} {}", event.kind, event.path),
            })
            .collect()
    }
}

/// A second `files` row for `stored` under `alias`, with its task rows, as
/// the sweep wrote one before #1975 was fixed.
fn plant_alias_row(session: &VaultSession, stored: &str, alias: &str) {
    let conn = session.conn.lock().unwrap();
    let columns = |table: &str, skip: &[&str]| -> Vec<String> {
        let mut stmt = conn
            .prepare(&format!("SELECT name FROM pragma_table_info('{table}')"))
            .unwrap();
        let names = stmt.query_map([], |row| row.get::<_, String>(0)).unwrap();
        names
            .map(|name| name.unwrap())
            .filter(|name| !skip.contains(&name.as_str()))
            .collect()
    };
    let file_columns = columns("files", &["id", "path", "name"]).join(", ");
    conn.execute(
        &format!(
            "INSERT INTO files (path, name, {file_columns})
             SELECT ?1, ?2, {file_columns} FROM files WHERE path = ?3"
        ),
        rusqlite::params![alias, leaf_name(alias), stored],
    )
    .unwrap();
    let task_columns = columns("tasks", &["file_id"]).join(", ");
    conn.execute(
        &format!(
            "INSERT INTO tasks (file_id, {task_columns})
             SELECT (SELECT id FROM files WHERE path = ?1), {task_columns}
             FROM tasks WHERE file_id = (SELECT id FROM files WHERE path = ?2)"
        ),
        rusqlite::params![alias, stored],
    )
    .unwrap();
}

/// A folder's case-only rename plants one marker pair for each file in it.
/// After the next vault-wide task query each file keeps one row, each task
/// is listed once, and no marker is left. A host hears each note read
/// again, once, under the spelling the volume holds, and nothing for the
/// old names, on every volume. Where the volume keeps case the rename was a
/// plain one: the old names name no file and no row, and a repair that
/// finds neither announces nothing (#1683; before it, they were reported
/// deleted first).
#[test]
fn a_folders_case_only_rename_leaves_one_row_for_each_file_after_the_next_task_query() {
    let (_tmp, session) = make_vault(|provider| {
        provider.write_file("Docs/Note.md", b"- [ ] one\n").unwrap();
        provider
            .write_file("Docs/Sub/Inner.md", b"- [ ] two\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session.rename_folder("Docs", "docs").unwrap();
    let recorder = ChangeRecorder::on(&session);

    let mut tasks = vault_tasks(&session);
    assert_eq!(
        recorder.take(),
        strings(&["Modified docs/Note.md", "Modified docs/Sub/Inner.md"])
    );
    tasks.sort();
    assert_eq!(
        tasks,
        strings(&["docs/Note.md: one", "docs/Sub/Inner.md: two"])
    );
    assert_eq!(
        index_paths(&session, "files"),
        strings(&["docs/Note.md", "docs/Sub/Inner.md"])
    );
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
}

/// The same after a folder's case-only rename that failed: a holder on one
/// file inside refuses the folder's rename after every marker is planted.
#[cfg(windows)]
#[test]
fn a_folders_failed_case_only_rename_leaves_one_row_for_each_file_after_the_next_task_query() {
    use std::os::windows::fs::OpenOptionsExt as _;
    use windows_sys::Win32::Storage::FileSystem::{FILE_SHARE_READ, FILE_SHARE_WRITE};

    let (tmp, session) = make_vault(|provider| {
        provider.write_file("Docs/Note.md", b"- [ ] one\n").unwrap();
        provider.write_file("Docs/Two.md", b"- [ ] two\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let holder = std::fs::OpenOptions::new()
        .read(true)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE)
        .open(tmp.path().join("Docs").join("Note.md"))
        .unwrap();
    let outcome = session.rename_folder("Docs", "docs");
    drop(holder);
    assert!(
        outcome.is_err(),
        "premise: the holder refuses the folder's rename: {outcome:?}"
    );
    assert_eq!(root_listing(tmp.path()), strings(&["Docs"]));

    let mut tasks = vault_tasks(&session);
    tasks.sort();
    assert_eq!(tasks, strings(&["Docs/Note.md: one", "Docs/Two.md: two"]));
    assert_eq!(
        index_paths(&session, "files"),
        strings(&["Docs/Note.md", "Docs/Two.md"])
    );
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
}

/// A folder renamed with its note makes two hops, so the note has markers
/// under three spellings. It keeps one row.
#[test]
fn a_case_only_folder_note_rename_leaves_one_row_after_the_next_task_query() {
    let (_tmp, session) = make_vault(|provider| {
        provider.write_file("Docs/Docs.md", b"- [ ] one\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session.rename_folder_with_note("Docs", "docs").unwrap();

    assert_eq!(vault_tasks(&session), strings(&["docs/docs.md: one"]));
    assert_eq!(index_paths(&session, "files"), strings(&["docs/docs.md"]));
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
}

/// Undo is the same rename the other way, with its own marker pair.
#[test]
fn an_undone_case_only_rename_leaves_one_row_after_the_next_task_query() {
    let (_tmp, session) = make_vault(|provider| {
        provider.write_file("A.md", b"- [ ] one\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let report = session.rename_file("A.md", "a.md").unwrap();
    session.undo_structural(report.op_id).unwrap();

    assert_eq!(vault_tasks(&session), strings(&["A.md: one"]));
    assert_eq!(index_paths(&session, "files"), strings(&["A.md"]));
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
}

/// A task query between a case-only rename and its undo leaves the undo
/// working. Before #1975 was fixed the query's second row made the undo's
/// index update fail on the path's uniqueness, as a database error.
#[test]
fn a_case_only_rename_is_undone_after_a_task_query() {
    let (_tmp, session) = make_vault(|provider| {
        provider.write_file("A.md", b"- [ ] one\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let report = session.rename_file("A.md", "a.md").unwrap();
    vault_tasks(&session);

    let undone = session.undo_structural(report.op_id);
    assert!(undone.is_ok(), "the undo is refused: {undone:?}");

    assert_eq!(vault_tasks(&session), strings(&["A.md: one"]));
    assert_eq!(index_paths(&session, "files"), strings(&["A.md"]));
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
}

/// A mover that stopped after the volume applied its case-only rename and
/// before its index commit leaves the row under the old spelling and a held
/// marker under each. The next task query's sweep leaves one row, under the
/// spelling the volume holds.
#[test]
fn a_case_only_rename_the_volume_applied_and_the_index_never_recorded_leaves_one_row() {
    let (tmp, session) = make_vault(|provider| {
        provider.write_file("A.md", b"- [ ] one\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    plant_marker(&session, "A.md", true);
    plant_marker(&session, "a.md", true);
    std::fs::rename(tmp.path().join("A.md"), tmp.path().join("a.md")).unwrap();

    assert_eq!(vault_tasks(&session), strings(&["a.md: one"]));
    assert_eq!(index_paths(&session, "files"), strings(&["a.md"]));
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
}

/// One note's task list, asked for under the spelling the note had before
/// a case-only rename, sweeps that spelling's marker alone. It adds no row.
#[test]
fn a_notes_tasks_asked_for_under_its_old_spelling_add_no_row() {
    let (_tmp, session) = make_vault(|provider| {
        provider.write_file("A.md", b"- [ ] one\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session.rename_file("A.md", "a.md").unwrap();

    session.tasks_for_file("A.md").unwrap();

    assert_eq!(index_paths(&session, "files"), strings(&["a.md"]));
}

/// The sweep after a case-only rename tells a host that the note changed,
/// once, under the spelling the volume holds, and nothing else, on every
/// volume. On a volume that keeps case the rename was a plain one: the old
/// name names no file and no row, and a repair that finds neither announces
/// nothing (#1683; before it, the sweep reported the old name deleted
/// first).
#[test]
fn the_sweep_after_a_case_only_rename_reports_the_stored_spelling_once() {
    let (_tmp, session) = make_vault(|provider| {
        provider.write_file("A.md", b"- [ ] one\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session.rename_file("A.md", "a.md").unwrap();
    let recorder = ChangeRecorder::on(&session);

    vault_tasks(&session);

    assert_eq!(recorder.take(), strings(&["Modified a.md"]));
}

/// A provider that stands in for an aliasing volume on any platform: one
/// alias spelling reaches the file stored under another. It can also fail
/// the stored-spelling lookup for one path, as an unreachable volume does,
/// answer the alias's next lookups from a list a fact gives it, as a volume
/// another process is renaming on does, and it counts the lookups it is
/// asked for.
struct OneAliasProvider {
    inner: FsVaultProvider,
    alias: &'static str,
    stored: &'static str,
    lookup_fails_for: std::sync::Mutex<Option<&'static str>>,
    alias_answers: std::sync::Mutex<std::collections::VecDeque<&'static str>>,
    lookups: std::sync::atomic::AtomicUsize,
}

/// In `alias_answers`: this lookup fails, as a read of an unreachable
/// volume does.
const LOOKUP_FAILS: &str = "\0fails";

impl OneAliasProvider {
    fn through<'a>(&self, relative: &'a str) -> &'a str {
        if relative == self.alias {
            self.stored
        } else {
            relative
        }
    }
}

impl VaultProvider for OneAliasProvider {
    fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
        self.inner.list_dir(relative)
    }
    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        self.inner.read_file(self.through(relative))
    }
    fn read_file_with_cap(&self, relative: &str, max_bytes: u64) -> Result<Vec<u8>, VaultError> {
        self.inner
            .read_file_with_cap(self.through(relative), max_bytes)
    }
    fn read_in_vault_with_cap(
        &self,
        relative: &str,
        max_bytes: u64,
    ) -> Result<Vec<u8>, VaultError> {
        self.inner
            .read_in_vault_with_cap(self.through(relative), max_bytes)
    }
    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.inner.write_file(self.through(relative), contents)
    }
    fn delete(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.delete(self.through(relative))
    }
    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        self.inner.rename(from, to)
    }
    fn create_dir(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.create_dir(relative)
    }
    fn stat(&self, relative: &str) -> Result<crate::FileStat, VaultError> {
        self.inner.stat(self.through(relative))
    }
    fn mutation_path_kind(&self, path: &str) -> Result<Option<EntryKind>, VaultError> {
        self.inner.mutation_path_kind(self.through(path))
    }
    fn mutation_path_exists(&self, path: &str) -> Result<bool, VaultError> {
        self.inner.mutation_path_exists(self.through(path))
    }
    fn canonical_path(&self, path: &str) -> Result<Option<String>, VaultError> {
        self.lookups
            .fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        if *self.lookup_fails_for.lock().unwrap() == Some(path) {
            return Err(VaultError::Io(std::io::Error::other(
                "test: the stored spelling cannot be read",
            )));
        }
        if path == self.alias
            && let Some(answer) = self.alias_answers.lock().unwrap().pop_front()
        {
            if answer == LOOKUP_FAILS {
                return Err(VaultError::Io(std::io::Error::other(
                    "test: the stored spelling cannot be read",
                )));
            }
            return Ok(Some(answer.to_string()));
        }
        self.inner.canonical_path(self.through(path))
    }
    fn watch(
        &self,
        sink: Arc<dyn crate::FileEventSink>,
    ) -> Result<Option<crate::WatchHandle>, VaultError> {
        self.inner.watch(sink)
    }
}

/// A scanned vault that stores `stored.md`, on a provider that also reaches
/// it as `ALIAS.md`.
fn one_alias_vault() -> (tempfile::TempDir, Arc<OneAliasProvider>, VaultSession) {
    one_alias_vault_named("stored.md", "ALIAS.md")
}

/// The same vault under names of the fact's own. A fact that sets a fault
/// variable needs them: a fault matches a path by substring in every
/// session of the test process.
fn one_alias_vault_named(
    stored: &'static str,
    alias: &'static str,
) -> (tempfile::TempDir, Arc<OneAliasProvider>, VaultSession) {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join(stored), b"- [ ] one\n").unwrap();
    let provider = Arc::new(OneAliasProvider {
        inner: FsVaultProvider::new(tmp.path().to_path_buf()),
        alias,
        stored,
        lookup_fails_for: std::sync::Mutex::new(None),
        alias_answers: std::sync::Mutex::new(std::collections::VecDeque::new()),
        lookups: std::sync::atomic::AtomicUsize::new(0),
    });
    let session = VaultSession::open(
        Arc::clone(&provider) as Arc<dyn VaultProvider>,
        SessionConfig::new(tmp.path().join(".slate")),
    )
    .unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(index_paths(&session, "files"), strings(&[stored]));
    (tmp, provider, session)
}

/// The rule itself, on every platform: a marker under a spelling the volume
/// resolves to a file stored under another spelling is settled by reading
/// the stored spelling. No row is added and the marker is cleared.
#[test]
fn a_marker_under_an_alias_is_settled_by_reading_the_stored_spelling() {
    let (_tmp, _provider, session) = one_alias_vault();
    plant_marker(&session, "ALIAS.md", false);

    assert_eq!(vault_tasks(&session), strings(&["stored.md: one"]));
    assert_eq!(index_paths(&session, "files"), strings(&["stored.md"]));
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
}

/// Write-intent INV2 at the bind step: a marker under an alias is never
/// cleared while the write it guards could still be unindexed. The file
/// holds bytes the index has not read. While the stored spelling cannot be
/// read, the marker stands (moved onto the stored spelling's key) and the
/// note serves no tasks, never the stale ones (INV4). The first read that
/// lands indexes the bytes and clears the marker with them.
#[test]
fn a_marker_under_an_alias_is_cleared_only_with_the_files_bytes_indexed() {
    let mut env = fault_guard();
    let (tmp, _provider, session) = one_alias_vault_named("indexed1975.md", "INDEXED1975.md");
    plant_marker(&session, "INDEXED1975.md", false);
    std::fs::write(tmp.path().join("indexed1975.md"), b"- [ ] one\n- [ ] two\n").unwrap();

    // The fault names the stored spelling alone (it matches by case).
    env.set("SLATE_TEST_FAULT_REINDEX", "indexed1975");
    assert_eq!(vault_tasks(&session), no_markers());
    assert_eq!(index_paths(&session, "files"), strings(&["indexed1975.md"]));
    assert_eq!(
        index_paths(&session, "text_write_intents"),
        strings(&["indexed1975.md"])
    );

    env.clear("SLATE_TEST_FAULT_REINDEX");
    assert_eq!(
        vault_tasks(&session),
        strings(&["indexed1975.md: one", "indexed1975.md: two"])
    );
    assert_eq!(index_paths(&session, "files"), strings(&["indexed1975.md"]));
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
}

/// The move itself, with tokens on both keys. A held token and an aged
/// token under the alias, and under the stored spelling an aged token that
/// carries the held one's number. After the bind step every token stands
/// under the stored spelling: the shared number is one row and it is held
/// (the stronger of the two), and the aged token carries the stored row's
/// current epoch, so the quarantine cannot call it superseded by a read
/// that came before the move. The read is made to fail so the tokens can
/// be seen; once it lands they are all cleared.
#[test]
fn a_moved_marker_keeps_its_age_and_takes_the_stored_rows_epoch() {
    let mut env = fault_guard();
    let (_tmp, _provider, session) = one_alias_vault_named("moved1975.md", "MOVED1975.md");
    let stored_epoch: i64 = {
        let conn = session.conn.lock().unwrap();
        conn.query_row(
            "SELECT index_epoch FROM files WHERE path = 'moved1975.md'",
            [],
            |row| row.get(0),
        )
        .unwrap()
    };
    assert!(
        stored_epoch > 0,
        "premise: the scan stamped the stored row's epoch"
    );
    // An aged token registered before the stored row's last full read: on
    // its old epoch the quarantine would delete it without a read.
    plant_marker_as(&session, "MOVED1975.md", 11, false, 0);
    plant_marker_as(&session, "MOVED1975.md", 22, true, 0);
    plant_marker_as(&session, "moved1975.md", 22, false, 0);

    env.set("SLATE_TEST_FAULT_REINDEX", "moved1975");
    assert_eq!(vault_tasks(&session), no_markers());
    assert_eq!(index_paths(&session, "files"), strings(&["moved1975.md"]));
    assert_eq!(
        marker_rows(&session),
        vec![
            ("moved1975.md".to_string(), 11, false, stored_epoch),
            ("moved1975.md".to_string(), 22, true, stored_epoch),
        ]
    );

    env.clear("SLATE_TEST_FAULT_REINDEX");
    assert_eq!(vault_tasks(&session), strings(&["moved1975.md: one"]));
    assert_eq!(index_paths(&session, "files"), strings(&["moved1975.md"]));
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
}

/// A marker under a path that names nothing is settled as it always was:
/// the lookup answers nothing, the read finds nothing, the row goes, the
/// marker is cleared and the host hears the path deleted.
#[test]
fn a_marker_under_a_path_that_names_nothing_is_settled_as_before() {
    let (tmp, session) = make_vault(|provider| {
        provider.write_file("gone1975.md", b"- [ ] one\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    std::fs::remove_file(tmp.path().join("gone1975.md")).unwrap();
    plant_marker(&session, "gone1975.md", false);
    let recorder = ChangeRecorder::on(&session);

    assert_eq!(vault_tasks(&session), no_markers());
    assert_eq!(index_paths(&session, "files"), no_markers());
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
    assert_eq!(recorder.take(), strings(&["Deleted gone1975.md"]));
}

/// What the bind step costs: one stored-spelling lookup for each path the
/// sweep selected, one more for a path that turned out to be an alias (the
/// second look, inside the writer transaction), and none anywhere else. A
/// task query with no marker asks for none, a marker too fresh to be
/// selected asks for none, and a scan asks for none.
#[test]
fn the_stored_spelling_is_looked_up_for_selected_paths_only_and_twice_for_an_alias() {
    let (_tmp, provider, session) = one_alias_vault();
    let lookups = || provider.lookups.load(std::sync::atomic::Ordering::SeqCst);
    let after_open = lookups();

    session.scan_initial(&CancelToken::new()).unwrap();
    vault_tasks(&session);
    assert_eq!(lookups(), after_open, "no marker, no lookup");

    // A writer's marker, registered now: in flight, never selected.
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "INSERT INTO text_write_intents(path, created_ms, token, registered_epoch)
             VALUES ('ALIAS.md', ?1, 7, 0)",
            rusqlite::params![now_ms()],
        )
        .unwrap();
    }
    vault_tasks(&session);
    assert_eq!(lookups(), after_open, "a fresh marker is not looked up");
    {
        let conn = session.conn.lock().unwrap();
        conn.execute("DELETE FROM text_write_intents", []).unwrap();
    }

    // Two tokens under one path are one path: one lookup.
    plant_marker(&session, "stored.md", false);
    plant_marker(&session, "stored.md", false);
    vault_tasks(&session);
    assert_eq!(lookups(), after_open + 1, "one lookup for a selected path");
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());

    plant_marker(&session, "ALIAS.md", false);
    plant_marker(&session, "stored.md", false);
    vault_tasks(&session);
    assert_eq!(
        lookups(),
        after_open + 4,
        "two for the alias, one for the stored spelling"
    );
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());

    vault_tasks(&session);
    assert_eq!(lookups(), after_open + 4, "settled markers cost nothing");
}

/// The volume's answer can move between the sweep's lookup and its writer
/// transaction: another Slate process case-renames the note the other way
/// and commits, so the spelling the sweep took for the alias is now the
/// stored one and the index row stands under it. The sweep looks again
/// inside the transaction. An answer that moved changes nothing: the row,
/// its history binding and the marker are as they were, and nothing is
/// sent. Before this, the sweep removed that row and gave the note a new
/// one under the spelling that had become the alias. The next sweep, with a
/// steady answer, settles the marker.
#[test]
fn a_marker_whose_stored_spelling_moved_under_the_sweep_is_left_for_the_next_one() {
    let (_tmp, provider, session) = one_alias_vault();
    // The other process's rename has landed in the index: the row is under
    // the spelling this sweep is about to take for an alias.
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE files SET path = 'ALIAS.md', name = 'ALIAS.md' WHERE path = 'stored.md'",
            [],
        )
        .unwrap();
    }
    bind_history(&session, "ALIAS.md", "kept-1975");
    plant_marker_as(&session, "ALIAS.md", 31, false, 0);
    let recorder = ChangeRecorder::on(&session);
    // Before the transaction the volume still answered the old way; inside
    // it, the rename has landed and the path is its own stored spelling.
    provider
        .alias_answers
        .lock()
        .unwrap()
        .extend(["stored.md", "ALIAS.md"]);

    assert_eq!(vault_tasks(&session), strings(&["ALIAS.md: one"]));
    assert_eq!(index_paths(&session, "files"), strings(&["ALIAS.md"]));
    assert_eq!(
        history_binding(&session, "ALIAS.md").as_deref(),
        Some("kept-1975")
    );
    assert_eq!(
        marker_rows(&session),
        vec![("ALIAS.md".to_string(), 31, false, 0)]
    );
    assert_eq!(recorder.take(), no_markers());

    // A steady answer: the path is its own stored spelling, so the marker
    // is settled by reading it, in place.
    provider.alias_answers.lock().unwrap().push_back("ALIAS.md");
    assert_eq!(vault_tasks(&session), strings(&["ALIAS.md: one"]));
    assert_eq!(index_paths(&session, "files"), strings(&["ALIAS.md"]));
    assert_eq!(
        history_binding(&session, "ALIAS.md").as_deref(),
        Some("kept-1975")
    );
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
    assert_eq!(recorder.take(), strings(&["Modified ALIAS.md"]));
}

/// The second lookup can fail too. It is the marker's repair error, as the
/// first one's is: the row the marker's own key names is contained (it
/// serves no tasks while it cannot be vouched for), the row and its history
/// binding stay, the marker stays, and the query answers.
#[test]
fn a_second_lookup_that_fails_is_the_markers_repair_error() {
    let (_tmp, provider, session) = one_alias_vault();
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE files SET path = 'ALIAS.md', name = 'ALIAS.md' WHERE path = 'stored.md'",
            [],
        )
        .unwrap();
    }
    bind_history(&session, "ALIAS.md", "kept-1975");
    plant_marker(&session, "ALIAS.md", false);
    provider
        .alias_answers
        .lock()
        .unwrap()
        .extend(["stored.md", LOOKUP_FAILS]);

    assert_eq!(vault_tasks(&session), no_markers());
    assert_eq!(index_paths(&session, "files"), strings(&["ALIAS.md"]));
    assert_eq!(
        history_binding(&session, "ALIAS.md").as_deref(),
        Some("kept-1975")
    );
    assert_eq!(
        index_paths(&session, "text_write_intents"),
        strings(&["ALIAS.md"])
    );
}

/// Round 22's token discipline through the bind step: the repair clears
/// the tokens the sweep selected, the moved ones among them, and no other.
/// A writer whose save is in flight on the stored spelling has a fresh
/// token there, never selected. It still stands after the sweep moved an
/// alias marker onto that key and read the note.
#[test]
fn a_live_writers_token_under_the_stored_spelling_outlives_the_bind() {
    let (_tmp, _provider, session) = one_alias_vault();
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "INSERT INTO text_write_intents(path, created_ms, token, registered_epoch)
             VALUES ('stored.md', ?1, 41, 0)",
            rusqlite::params![now_ms()],
        )
        .unwrap();
    }
    plant_marker_as(&session, "ALIAS.md", 42, false, 0);

    assert_eq!(vault_tasks(&session), strings(&["stored.md: one"]));

    assert_eq!(index_paths(&session, "files"), strings(&["stored.md"]));
    let left: Vec<(String, i64)> = marker_rows(&session)
        .into_iter()
        .map(|(path, token, _, _)| (path, token))
        .collect();
    assert_eq!(left, vec![("stored.md".to_string(), 41)]);
}

/// Contract 32 I7 at the sweep: when the stored spelling cannot be looked
/// up, the marker stays, nothing is read through the spelling that did not
/// resolve, no row is added, and the task query still answers. The marker
/// is settled by the first sweep that can look the spelling up.
#[test]
fn a_marker_whose_stored_spelling_cannot_be_looked_up_stays_and_adds_no_row() {
    let (_tmp, provider, session) = one_alias_vault();
    plant_marker(&session, "ALIAS.md", false);
    *provider.lookup_fails_for.lock().unwrap() = Some("ALIAS.md");

    assert_eq!(vault_tasks(&session), strings(&["stored.md: one"]));
    assert_eq!(index_paths(&session, "files"), strings(&["stored.md"]));
    assert_eq!(
        index_paths(&session, "text_write_intents"),
        strings(&["ALIAS.md"])
    );

    *provider.lookup_fails_for.lock().unwrap() = None;
    assert_eq!(vault_tasks(&session), strings(&["stored.md: one"]));
    assert_eq!(index_paths(&session, "files"), strings(&["stored.md"]));
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
}

/// The write-intent protocol's per-path deferral follows the stored
/// spelling: while an active move holds the structural lock and a held
/// marker stands under the stored spelling, a marker under an alias of it
/// waits too. Both are settled once the move is over.
#[test]
fn a_marker_under_an_alias_waits_while_a_move_owns_the_stored_spelling() {
    let (_tmp, _provider, session) = one_alias_vault();
    plant_marker(&session, "stored.md", true);
    plant_marker(&session, "ALIAS.md", false);

    let active_move = VaultStructuralLock::acquire(&session.interior).unwrap();
    assert_eq!(vault_tasks(&session), strings(&["stored.md: one"]));
    assert_eq!(index_paths(&session, "files"), strings(&["stored.md"]));
    assert_eq!(
        index_paths(&session, "text_write_intents"),
        strings(&["ALIAS.md", "stored.md"])
    );
    drop(active_move);

    assert_eq!(vault_tasks(&session), strings(&["stored.md: one"]));
    assert_eq!(index_paths(&session, "files"), strings(&["stored.md"]));
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
}

/// The other half of that rule: a held marker under the stored spelling
/// whose mover is gone (the structural lock is free) holds nothing up. One
/// note's own sweep sees only the alias marker; it claims the lock, as the
/// sweep does for a held marker under its own key, and settles the alias
/// marker in that same sweep. The held marker is the vault-wide sweep's.
#[test]
fn a_notes_own_sweep_claims_the_lock_to_settle_a_marker_under_its_alias() {
    let (_tmp, _provider, session) = one_alias_vault();
    plant_marker(&session, "stored.md", true);
    plant_marker(&session, "ALIAS.md", false);

    session.tasks_for_file("ALIAS.md").unwrap();

    assert_eq!(index_paths(&session, "files"), strings(&["stored.md"]));
    assert_eq!(
        index_paths(&session, "text_write_intents"),
        strings(&["stored.md"])
    );
    assert_eq!(
        marker_rows(&session)
            .iter()
            .map(|(_, _, held, _)| *held)
            .collect::<Vec<_>>(),
        vec![true],
        "the held marker under the stored spelling is left for its own sweep"
    );

    assert_eq!(vault_tasks(&session), strings(&["stored.md: one"]));
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
}

/// A database an earlier build damaged: the second row is already there
/// and a marker under its spelling is swept again (a second rename, or an
/// undo). The row under the alias goes and the task is listed once. The
/// stored row is the one that was there, read again.
///
/// A host hears one thing and no more: the stored note was read again
/// (`Modified`, as after a repair of a row that stood). Nothing is sent
/// for the alias path. `Deleted` would tell a reader that a file on disk is
/// gone, and `Renamed` onto a spelling a host already holds (both rows were
/// listed) would put two open buffers on one path.
#[test]
fn a_row_an_earlier_build_left_under_an_alias_goes_when_its_marker_is_swept() {
    let (_tmp, _provider, session) = one_alias_vault();
    plant_alias_row(&session, "stored.md", "ALIAS.md");
    assert_eq!(
        index_paths(&session, "files"),
        strings(&["ALIAS.md", "stored.md"]),
        "premise: the damaged database holds both rows"
    );
    bind_history(&session, "stored.md", "kept-1975");
    plant_marker(&session, "ALIAS.md", false);
    let recorder = ChangeRecorder::on(&session);

    assert_eq!(vault_tasks(&session), strings(&["stored.md: one"]));
    assert_eq!(index_paths(&session, "files"), strings(&["stored.md"]));
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
    assert_eq!(
        history_binding(&session, "stored.md").as_deref(),
        Some("kept-1975")
    );
    assert_eq!(recorder.take(), strings(&["Modified stored.md"]));
}

/// The other way a row comes to stand under an alias: a mover stopped after
/// the volume applied its case-only rename, so the only row is under the
/// old spelling and nothing is under the stored one. The sweep removes that
/// row, reads the stored spelling and gives it its row. A host hears one
/// thing: the stored note's `Created`, because the row the sweep wrote is
/// the stored spelling's first (#1704: a repair announces a first row as a
/// save of a new path does, so a host re-seats a tab missing at it).
#[test]
fn a_row_a_stopped_move_left_under_an_alias_goes_and_the_stored_spelling_gets_its_row() {
    let (_tmp, _provider, session) = one_alias_vault();
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE files SET path = 'ALIAS.md', name = 'ALIAS.md' WHERE path = 'stored.md'",
            [],
        )
        .unwrap();
    }
    plant_marker(&session, "ALIAS.md", true);
    plant_marker(&session, "stored.md", true);
    let recorder = ChangeRecorder::on(&session);

    assert_eq!(vault_tasks(&session), strings(&["stored.md: one"]));
    assert_eq!(index_paths(&session, "files"), strings(&["stored.md"]));
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
    assert_eq!(recorder.take(), strings(&["Created stored.md"]));
}

/// What a host's change feed holds after the sweep removed a row under an
/// alias. The removal is not announced by an event, so it is not one of the
/// session's own announced writes: the next window carries it as one file
/// removed, exactly as it carries a scan's removal of that row. The feed
/// must not claim an announcement that was never made, or no host would
/// ever hear that the row went. The read of the stored note is announced
/// (`Modified`), so it is not in the window.
#[test]
fn a_row_removed_from_under_an_alias_is_in_the_hosts_next_change_feed_window() {
    let (_tmp, _provider, session) = one_alias_vault();
    plant_alias_row(&session, "stored.md", "ALIAS.md");
    plant_marker(&session, "ALIAS.md", false);
    session.register_change_feed().unwrap();

    vault_tasks(&session);

    let window = session
        .change_feed_window(None, &CancelToken::new())
        .unwrap();
    let page = session
        .change_feed_page(&window.token, None, 100, &CancelToken::new())
        .unwrap();
    let records: Vec<String> = page
        .records
        .iter()
        .map(|record| format!("{:?} {}", record.effect, record.path))
        .collect();
    assert_eq!(records, strings(&["Removed ALIAS.md"]));
    assert_eq!((window.changed, window.removed), (0, 1));
}
/// A marker under an alias with no row under it, which is every case-only
/// rename that ran to its end: nothing is sent for the alias. A host hears
/// the stored note read again, once.
#[test]
fn a_marker_under_an_alias_with_no_row_sends_nothing_for_the_alias() {
    let (_tmp, _provider, session) = one_alias_vault();
    plant_marker(&session, "ALIAS.md", false);
    plant_marker(&session, "stored.md", false);
    let recorder = ChangeRecorder::on(&session);

    vault_tasks(&session);

    assert_eq!(recorder.take(), strings(&["Modified stored.md"]));
}

/// The same database with no marker left: the next full scan removes the
/// row under the alias, and its task rows and its search row go with it.
/// This held before #1975 was fixed; it is pinned here because it is the
/// only repair a database gets once the markers are gone.
#[test]
fn a_scan_removes_a_row_an_earlier_build_left_under_an_alias() {
    let (_tmp, session) = make_vault(|provider| {
        provider
            .write_file("stored.md", b"- [ ] one zebrafish\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    plant_alias_row(&session, "stored.md", "STORED.md");
    assert_eq!(
        index_paths(&session, "files"),
        strings(&["STORED.md", "stored.md"]),
        "premise: the damaged database holds both rows"
    );
    assert_eq!(fts_match_count(&session, "zebrafish"), 2);

    session.scan_initial(&CancelToken::new()).unwrap();

    assert_eq!(index_paths(&session, "files"), strings(&["stored.md"]));
    assert_eq!(
        vault_tasks(&session),
        strings(&["stored.md: one zebrafish"])
    );
    assert_eq!(fts_match_count(&session, "zebrafish"), 1);
}

/// A vault whose root keeps `A.md` and `a.md` as two files, scanned, or
/// `None` where the volume cannot: a Linux volume as it is, and on Windows a
/// folder switched to case-sensitive names, which needs the Windows
/// Subsystem for Linux feature and no elevation.
fn two_files_by_case_vault() -> Option<(tempfile::TempDir, VaultSession)> {
    let tmp = tempfile::tempdir().unwrap();
    #[cfg(windows)]
    {
        let switched = std::process::Command::new("fsutil")
            .args(["file", "setCaseSensitiveInfo"])
            .arg(tmp.path())
            .arg("enable")
            .output()
            .is_ok_and(|output| output.status.success());
        if !switched {
            // This machine cannot make a case-sensitive folder.
            return None;
        }
    }
    std::fs::write(tmp.path().join("A.md"), b"- [ ] upper\n").unwrap();
    std::fs::write(tmp.path().join("a.md"), b"- [ ] lower\n").unwrap();
    let names = std::fs::read_dir(tmp.path()).unwrap().count();
    if names != 2 {
        // This volume keeps one file for `A.md` and `a.md`.
        return None;
    }
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    Some((tmp, session))
}

/// Two real files whose names differ only by case are two files: a marker
/// under each is settled by reading each, and both rows stay, each the row
/// it was. The volume says which spellings are aliases; no case rule does
/// (contract 32 I1).
///
/// Where the volume cannot hold both names the fact has nothing to run on.
/// It says which happened, so a run's output shows whether it ran.
// The line printed is the fact's own record and names no vault path; #507's
// no-stdout rule is for slate-core itself.
#[allow(clippy::disallowed_macros)]
#[test]
fn two_files_whose_names_differ_only_by_case_keep_two_rows() {
    let Some((_tmp, session)) = two_files_by_case_vault() else {
        println!(
            "two_files_whose_names_differ_only_by_case_keep_two_rows: SKIPPED, this volume cannot hold both names"
        );
        return;
    };
    println!(
        "two_files_whose_names_differ_only_by_case_keep_two_rows: RAN on a folder that holds both names"
    );
    assert_eq!(index_paths(&session, "files"), strings(&["A.md", "a.md"]));
    bind_history(&session, "A.md", "upper-1975");
    bind_history(&session, "a.md", "lower-1975");
    plant_marker(&session, "A.md", false);
    plant_marker(&session, "a.md", false);
    let recorder = ChangeRecorder::on(&session);

    let mut tasks = vault_tasks(&session);
    tasks.sort();
    assert_eq!(tasks, strings(&["A.md: upper", "a.md: lower"]));
    assert_eq!(index_paths(&session, "files"), strings(&["A.md", "a.md"]));
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
    assert_eq!(
        (
            history_binding(&session, "A.md"),
            history_binding(&session, "a.md"),
        ),
        (
            Some("upper-1975".to_string()),
            Some("lower-1975".to_string()),
        )
    );
    assert_eq!(
        recorder.take(),
        strings(&["Modified A.md", "Modified a.md"])
    );
}

/// The two repair routes a host calls with a path of its own bind it as a
/// save does (contract 32 I2 and I3): through an alias they read the stored
/// row again and add none. On every platform, through the stand-in
/// provider, and on a real volume where it aliases. It says whether the
/// real-volume half ran, so a run's output shows it.
// The line printed is the fact's own record and names no vault path; #507's
// no-stdout rule is for slate-core itself.
#[allow(clippy::disallowed_macros)]
#[test]
fn a_hosts_repair_through_an_alias_reads_the_stored_row_and_adds_none() {
    let (tmp, _provider, session) = one_alias_vault();
    std::fs::write(tmp.path().join("stored.md"), b"- [ ] one\n- [ ] two\n").unwrap();
    session.reindex_path("ALIAS.md").unwrap();
    assert_eq!(index_paths(&session, "files"), strings(&["stored.md"]));
    assert_eq!(
        vault_tasks(&session),
        strings(&["stored.md: one", "stored.md: two"])
    );

    std::fs::write(tmp.path().join("stored.md"), b"- [ ] three\n").unwrap();
    let outcome = session.repair_or_contain_path("ALIAS.md").unwrap();
    assert_eq!(outcome, TaskIndexRepairOutcome::Repaired);
    assert_eq!(index_paths(&session, "files"), strings(&["stored.md"]));
    assert_eq!(vault_tasks(&session), strings(&["stored.md: three"]));

    let Some((_tmp, session)) = aliasing_vault() else {
        println!(
            "a_hosts_repair_through_an_alias_reads_the_stored_row_and_adds_none: SKIPPED on a real volume, this one keeps case; the stand-in half ran"
        );
        return;
    };
    println!(
        "a_hosts_repair_through_an_alias_reads_the_stored_row_and_adds_none: RAN on a real volume that aliases, after the stand-in half"
    );
    session.reindex_path("notes/alpha.md").unwrap();
    assert_eq!(index_paths(&session, "files"), strings(&["Notes/Alpha.md"]));
    let outcome = session.repair_or_contain_path("notes/alpha.md").unwrap();
    assert_eq!(outcome, TaskIndexRepairOutcome::Repaired);
    assert_eq!(index_paths(&session, "files"), strings(&["Notes/Alpha.md"]));
}

/// Contract 32 I7 at the host's repair routes: a lookup that fails reads
/// nothing through the spelling that did not resolve and adds no row.
/// `reindex_path` hands the failure back. `repair_or_contain_path` answers
/// `Contained`, as for any file it cannot read, and leaves an aged marker
/// under the spelling the host gave, which the next sweep that can look the
/// spelling up settles under the stored one.
#[test]
fn a_hosts_repair_whose_lookup_fails_reads_nothing_and_adds_no_row() {
    let (_tmp, provider, session) = one_alias_vault();
    *provider.lookup_fails_for.lock().unwrap() = Some("ALIAS.md");

    assert!(matches!(
        session.reindex_path("ALIAS.md"),
        Err(VaultError::Io(_))
    ));
    assert_eq!(index_paths(&session, "files"), strings(&["stored.md"]));

    let outcome = session.repair_or_contain_path("ALIAS.md").unwrap();
    assert_eq!(outcome, TaskIndexRepairOutcome::Contained);
    assert_eq!(index_paths(&session, "files"), strings(&["stored.md"]));
    assert_eq!(
        index_paths(&session, "text_write_intents"),
        strings(&["ALIAS.md"])
    );

    *provider.lookup_fails_for.lock().unwrap() = None;
    assert_eq!(vault_tasks(&session), strings(&["stored.md: one"]));
    assert_eq!(index_paths(&session, "files"), strings(&["stored.md"]));
    assert_eq!(index_paths(&session, "text_write_intents"), no_markers());
}

/// Found on the way (#1975's design, item b): a folder's case-only rename
/// moves the rows of the folders inside it too. Before the fix the index
/// update asked the volume whether `Docs/Sub` was still there, the alias
/// answered yes, and the row stayed under the old spelling, so the Files
/// tree showed no folder inside `docs` until the next scan.
#[test]
fn a_folders_case_only_rename_moves_the_rows_of_the_folders_inside_it() {
    let (_tmp, session) = make_vault(|provider| {
        provider
            .write_file("Docs/Sub/Inner.md", b"# inner\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session.rename_folder("Docs", "docs").unwrap();

    assert_eq!(
        index_paths(&session, "dirs"),
        strings(&["docs", "docs/Sub"])
    );
    let listing = session
        .list_dir_children("docs", Paging::first(10))
        .unwrap();
    let folders: Vec<String> = listing.dirs.into_iter().map(|dir| dir.path).collect();
    assert_eq!(folders, strings(&["docs/Sub"]));
}

/// What the rule that D4 narrowed is for (write-intent protocol, the
/// final-confirmation review: "Directory rows apply the same source-Live
/// rule so file and directory trees cannot split"): a folder inside the
/// moved one that is there again under its own old path when the index is
/// updated, because a save recreated it after the rename, keeps its row.
/// Moving that row would leave the files saved there with no folder row.
#[test]
fn a_folder_recreated_at_its_old_path_keeps_its_row_through_a_folder_move() {
    let (tmp, session) = make_vault(|provider| {
        provider.write_file("f1975/sub/a.md", b"# a\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        index_paths(&session, "dirs"),
        strings(&["f1975", "f1975/sub"])
    );

    // The folder moved on disk, and a save then recreated the inner folder
    // at its old path.
    std::fs::rename(tmp.path().join("f1975"), tmp.path().join("g1975")).unwrap();
    std::fs::create_dir_all(tmp.path().join("f1975").join("sub")).unwrap();
    {
        let mut conn = session.conn.lock().unwrap();
        let tx = conn.transaction().unwrap();
        rename_prefix_in_index(
            &tx,
            session.provider.as_ref(),
            "f1975",
            "g1975",
            &[("f1975/sub/a.md".to_string(), "g1975/sub/a.md".to_string())],
        )
        .unwrap();
        tx.commit().unwrap();
    }

    assert_eq!(
        index_paths(&session, "dirs"),
        strings(&["f1975/sub", "g1975"])
    );
}
