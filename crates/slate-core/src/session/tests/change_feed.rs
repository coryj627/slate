// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Plan 43 PR 21a (#1293; contract 40 AR-30 and AR-31): the change feed's
//! core facts, under the names of
//! `docs/plans/43_mac_backlog_delivery/21_change_feed_design.md` §4.9.
//!
//! Two sessions on one vault stand for two Slate windows (or the app and
//! `slate-cli`): each holds its own connection to the shared
//! `.slate/cache.sqlite`, exactly as two processes do.

use std::sync::{Arc, Mutex};

use super::common::*;
use super::*;
use crate::change_feed::window_test_hook;
use crate::structural_batch::{BatchMoveRequest, BatchTrashRequest, StructuralBatchItem};
use crate::{
    ChangeCount, ChangeEffect, ChangeEntry, ChangeFeedGap, ChangeFeedStatus, ChangeFeedWindow,
    ChangeRecord,
};

use ChangeCount::{Created, Modified, Uncounted};
use ChangeEffect::{Present, Renamed};

fn open(tmp: &tempfile::TempDir) -> VaultSession {
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    session
}

/// A host that opened, scanned and registered, and a second session (another
/// window) that did the same.
fn two_hosts(
    setup: impl FnOnce(&FsVaultProvider),
) -> (tempfile::TempDir, VaultSession, VaultSession) {
    let (tmp, a) = make_vault(setup);
    a.scan_initial(&CancelToken::new()).unwrap();
    a.register_change_feed().unwrap();
    let b = open(&tmp);
    b.register_change_feed().unwrap();
    (tmp, a, b)
}

fn window(session: &VaultSession) -> ChangeFeedWindow {
    session
        .change_feed_window(None, &CancelToken::new())
        .unwrap()
}

fn window_after(session: &VaultSession, after: u64) -> ChangeFeedWindow {
    session
        .change_feed_window(Some(after), &CancelToken::new())
        .unwrap()
}

/// Every record of `window`, paged at the maximum.
fn records(session: &VaultSession, window: &ChangeFeedWindow) -> Vec<ChangeRecord> {
    let mut out = Vec::new();
    let mut cursor = None;
    loop {
        let page = session
            .change_feed_page(
                &window.token,
                cursor.take(),
                crate::MAX_CHANGE_FEED_PAGE_LIMIT,
                &CancelToken::new(),
            )
            .unwrap();
        out.extend(page.records);
        match page.next_cursor {
            Some(next) => cursor = Some(next),
            None => break,
        }
    }
    assert_eq!(out.len() as u64, window.record_count, "record_count");
    out
}

fn ack(session: &VaultSession, window: &ChangeFeedWindow) {
    session
        .acknowledge_change_feed(&window.token, &CancelToken::new())
        .unwrap();
}

/// (effect, path, count) of each file record, in page order.
fn files(records: &[ChangeRecord]) -> Vec<(ChangeEffect, &str, ChangeCount)> {
    records
        .iter()
        .filter(|record| record.entry == ChangeEntry::File)
        .map(|record| (record.effect, record.path.as_str(), record.counted))
        .collect()
}

fn renamed<'r>(records: &'r [ChangeRecord], to: &str) -> &'r ChangeRecord {
    records
        .iter()
        .find(|record| record.effect == Renamed && record.path == to)
        .unwrap_or_else(|| panic!("no Renamed record to {to}: {records:#?}"))
}

fn hash_of(session: &VaultSession, path: &str) -> String {
    session
        .get_file_metadata(path)
        .unwrap()
        .unwrap_or_else(|| panic!("{path} is indexed"))
        .content_hash
}

fn counts(window: &ChangeFeedWindow) -> (u64, u64) {
    (window.changed, window.removed)
}

fn feed_rows(session: &VaultSession) -> i64 {
    session
        .conn
        .lock()
        .unwrap()
        .query_row("SELECT COUNT(*) FROM change_feed", [], |row| row.get(0))
        .unwrap()
}

/// (seq, resync_pending) of `session`'s cursor row.
fn cursor_of(session: &VaultSession) -> Option<(i64, bool)> {
    session
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT seq, resync_pending FROM change_feed_cursors WHERE writer = ?1",
            [session.change_feed.writer()],
            |row| Ok((row.get(0)?, row.get(1)?)),
        )
        .optional()
        .unwrap()
}

fn head(session: &VaultSession) -> u64 {
    session.change_feed_head(&CancelToken::new()).unwrap()
}

/// `n` index rows another writer committed, straight through `session`'s
/// connection: plain `.txt` rows, so no derivative or FTS work, each firing
/// the feed's insert trigger with `session`'s writer and reconcile origin.
fn indexed_rows(session: &VaultSession, prefix: &str, n: usize) {
    session
        .conn
        .lock()
        .unwrap()
        .execute(
            "WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < ?1)
             INSERT INTO files (path, name, extension, size_bytes, mtime_ms, content_hash,
                                parser_version, indexed_at_ms, is_markdown)
             SELECT ?2 || i || '.txt', ?2 || i || '.txt', 'txt', 1, 1, 'h' || i, 1, 1, 0
               FROM n",
            rusqlite::params![n as i64, prefix],
        )
        .unwrap();
}

fn edit_on_disk(tmp: &tempfile::TempDir, path: &str, bytes: &[u8]) {
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    let before = provider.stat(path).unwrap().mtime_ms;
    rewrite_until_mtime_advances(&provider, path, bytes, before);
}

// --- Two sessions count and apply each other's changes (spec fact 1) ------------

/// Spec fact 1: session B creates, modifies and deletes; A's window says 2
/// new or changed and 1 removed and carries a Present/Created record with
/// its summary and hash, a Present/Modified record and a Removed record;
/// B's own window has no record and no count. (The AR-25 witness
/// `another_sessions_writes_are_in_the_shared_index_but_not_in_this_scans_counts`
/// stays: it pins the scan report, which is unchanged.)
#[test]
fn two_sessions_count_and_apply_each_others_create_modify_and_delete() {
    let (_tmp, a, b) = two_hosts(|p| {
        p.write_file("edit.md", b"before\n").unwrap();
        p.write_file("gone.md", b"gone\n").unwrap();
    });
    b.create_exclusive("new.md", "# New from B\n").unwrap();
    let before = hash_of(&b, "edit.md");
    b.save_text("edit.md", "after, from B\n", Some(&before))
        .unwrap();
    b.delete_file("gone.md").unwrap();

    let w = window(&a);
    assert_eq!(w.status, ChangeFeedStatus::Exact);
    assert!(w.counted);
    assert_eq!(counts(&w), (2, 1));
    let records = records(&a, &w);
    assert_eq!(
        files(&records),
        [
            (ChangeEffect::Removed, "gone.md", ChangeCount::Removed),
            (Present, "edit.md", Modified),
            (Present, "new.md", Created),
        ]
    );
    let new = records.iter().find(|r| r.path == "new.md").unwrap();
    assert_eq!(
        new.content_hash.as_deref(),
        Some(crate::content_hash(b"# New from B\n").as_str())
    );
    let summary = new
        .summary
        .as_ref()
        .expect("a present file carries its summary");
    assert_eq!(summary.path, "new.md");
    assert_eq!(summary.name, "new.md");
    assert!(summary.is_markdown);
    assert_eq!(summary.size_bytes, 13);
    assert!(new.openable);
    let edit = records.iter().find(|r| r.path == "edit.md").unwrap();
    assert_eq!(
        edit.content_hash.as_deref(),
        Some(crate::content_hash(b"after, from B\n").as_str())
    );
    let gone = records.iter().find(|r| r.path == "gone.md").unwrap();
    assert_eq!(
        (gone.content_hash.as_ref(), gone.summary.as_ref()),
        (None, None)
    );

    let own = window(&b);
    assert_eq!(own.status, ChangeFeedStatus::Exact);
    assert!(own.counted);
    assert_eq!((own.record_count, own.changed, own.removed), (0, 0, 0));
}

/// A change one session's scan reconciled is counted by both: a scan's rows
/// are never anyone's own announced write.
#[test]
fn a_change_one_session_scanned_is_counted_by_both() {
    let (tmp, a, b) = two_hosts(|p| {
        p.write_file("x.md", b"alpha\n").unwrap();
    });
    edit_on_disk(&tmp, "x.md", b"omega\n");
    let report = a.rescan_with_progress(&CancelToken::new(), None).unwrap();
    assert_eq!(report.files_changed, 1);

    for session in [&a, &b] {
        let w = window(session);
        assert_eq!(counts(&w), (1, 0));
        assert_eq!(files(&records(session, &w)), [(Present, "x.md", Modified)]);
    }
}

/// The census of design §3.5: every Slate-owned write is announced by its
/// session's file-change events, so it is excluded from that session's own
/// window and counted once by another session's.
#[test]
fn every_slate_owned_write_is_excluded_from_its_own_window() {
    let (_tmp, a, b) = two_hosts(|p| {
        p.write_file("save.md", b"one\n").unwrap();
        p.write_file("ren.md", b"ren\n").unwrap();
        p.write_file("mv.md", b"mv\n").unwrap();
        p.write_file("Folder/inside.md", b"inside\n").unwrap();
        p.write_file("MoveMe/inside2.md", b"inside2\n").unwrap();
        p.create_dir("Dest").unwrap();
        p.write_file("del.md", b"del\n").unwrap();
        p.write_file("DelDir/f.md", b"f\n").unwrap();
        p.write_file("bm.md", b"bm\n").unwrap();
        p.write_file("bt.md", b"bt\n").unwrap();
        p.write_file("undo.md", b"undo\n").unwrap();
        p.write_file("prop.md", b"---\nstatus: a\n---\nbody\n")
            .unwrap();
        p.write_file("task.md", b"- [ ] task\n").unwrap();
        p.write_file("board.canvas", br#"{"nodes":[],"edges":[]}"#)
            .unwrap();
    });
    let step = |name: &str, expected: (u64, u64)| {
        let own = window(&a);
        assert_eq!(own.status, ChangeFeedStatus::Exact, "{name}: own status");
        assert!(own.counted, "{name}: own counted");
        assert_eq!(
            (own.record_count, own.changed, own.removed),
            (0, 0, 0),
            "{name}: the session's own write reaches its own window: {:#?}",
            records(&a, &own)
        );
        ack(&a, &own);
        let other = window(&b);
        assert_eq!(
            other.status,
            ChangeFeedStatus::Exact,
            "{name}: other status"
        );
        assert!(other.counted, "{name}: other counted");
        assert_eq!(
            counts(&other),
            expected,
            "{name}: counted once by another session: {:#?}",
            records(&b, &other)
        );
        let seen = records(&b, &other);
        ack(&b, &other);
        seen
    };
    let file = |path: &str| StructuralBatchItem {
        path: path.into(),
        is_directory: false,
    };

    let one = hash_of(&a, "save.md");
    let two = a.save_text("save.md", "two\n", Some(&one)).unwrap();
    step("save", (1, 0));
    a.save_text("save.md", "three\n", Some(&two.new_content_hash))
        .unwrap();
    step("save again", (1, 0));
    a.create_exclusive("created.md", "created\n").unwrap();
    step("create", (1, 0));
    a.create_exclusive_bytes("bytes.bin", b"\x00\x01\x02")
        .unwrap();
    step("create bytes", (1, 0));
    a.rename_file("ren.md", "renamed.md").unwrap();
    let seen = step("rename a file", (1, 1));
    assert_eq!(
        renamed(&seen, "renamed.md").renamed_from.as_deref(),
        Some("ren.md")
    );
    a.move_file("mv.md", "Dest").unwrap();
    step("move a file", (1, 1));
    a.rename_folder("Folder", "Folder2").unwrap();
    let seen = step("rename a folder", (1, 1));
    assert!(
        seen.iter()
            .any(|r| r.entry == ChangeEntry::Directory && r.path == "Folder2"),
        "{seen:#?}"
    );
    a.move_folder("MoveMe", "Dest").unwrap();
    step("move a folder", (1, 1));
    a.create_folder("NewDir").unwrap();
    let seen = step("create a folder", (0, 0));
    assert_eq!(
        seen.iter()
            .map(|r| (r.entry, r.effect, r.path.as_str(), r.counted))
            .collect::<Vec<_>>(),
        [(ChangeEntry::Directory, Present, "NewDir", Uncounted)],
        "a folder is recorded, never counted"
    );
    a.delete_file("del.md").unwrap();
    step("delete a file", (0, 1));
    a.delete_folder("DelDir").unwrap();
    step("delete a folder", (0, 1));
    let moved = a
        .batch_move(BatchMoveRequest {
            items: vec![file("bm.md")],
            new_parent: "Dest".into(),
        })
        .unwrap();
    step("batch move", (1, 1));
    a.undo_batch_move(moved.op_id.expect("the batch move journals"))
        .unwrap();
    step("batch undo", (1, 1));
    let renamed_undo = a.rename_file("undo.md", "undone.md").unwrap();
    step("rename before the structural undo", (1, 1));
    a.undo_structural(renamed_undo.op_id).unwrap();
    step("structural undo", (1, 1));
    a.restore_version("save.md", &two.new_content_hash, None)
        .unwrap();
    step("version restore", (1, 0));
    a.save_text("rec.md", "recover me\n", None).unwrap();
    step("save a note to delete", (1, 0));
    a.delete_file("rec.md").unwrap();
    step("delete it", (0, 1));
    a.scan_initial(&CancelToken::new()).unwrap();
    step("the scan that finds its history", (0, 0));
    a.recover_deleted_file("rec.md").unwrap();
    step("deleted-file recovery", (1, 0));
    let board = a.open_canvas("board.canvas").unwrap();
    a.canvas_apply(
        board.handle,
        crate::canvas::apply::CanvasAction {
            name: "create card".into(),
            ops: vec![crate::canvas::apply::CanvasOp::CreateNode {
                id: "card-1".into(),
                content: crate::canvas::apply::CanvasNodeContent::Text {
                    text: "A card".into(),
                },
                x: 0.0,
                y: 0.0,
                width: 260.0,
                height: 140.0,
                color: None,
            }],
        },
    )
    .unwrap();
    step("canvas apply", (1, 0));
    a.set_property(
        "prop.md",
        "status",
        crate::PropertyValue::Text("b".into()),
        None,
    )
    .unwrap();
    step("property edit", (1, 0));
    a.toggle_task_status("task.md", 0, 'x', None).unwrap();
    step("task edit", (1, 0));
    a.batch_trash(BatchTrashRequest {
        items: vec![file("bt.md")],
    })
    .unwrap();
    step("batch trash", (0, 1));
}

// --- Design §4.3's table, one fact per row --------------------------------------

/// B edits X; then A's user renames X to Y: X Removed, not counted; Y
/// Present with B's hash, 1 modified — the moved tab reloads B's bytes.
#[test]
fn another_sessions_edit_then_an_own_rename_moves_the_change_to_the_destination() {
    let (_tmp, a, b) = two_hosts(|p| {
        p.write_file("x.md", b"from the start\n").unwrap();
    });
    let before = hash_of(&b, "x.md");
    let edited = b.save_text("x.md", "edited by B\n", Some(&before)).unwrap();
    a.rename_file("x.md", "y.md").unwrap();

    let w = window(&a);
    assert_eq!(counts(&w), (1, 0));
    let records = records(&a, &w);
    assert_eq!(
        files(&records),
        [
            (ChangeEffect::Removed, "x.md", Uncounted),
            (Present, "y.md", Modified)
        ]
    );
    assert_eq!(
        records[1].content_hash.as_deref(),
        Some(edited.new_content_hash.as_str())
    );
}

/// B edits X; then A's user deletes X: X Removed, and nothing is counted.
#[test]
fn another_sessions_edit_then_an_own_delete_counts_nothing() {
    let (_tmp, a, b) = two_hosts(|p| {
        p.write_file("x.md", b"x\n").unwrap();
    });
    let before = hash_of(&b, "x.md");
    b.save_text("x.md", "edited by B\n", Some(&before)).unwrap();
    a.delete_file("x.md").unwrap();

    let w = window(&a);
    assert_eq!(counts(&w), (0, 0));
    assert_eq!(
        files(&records(&a, &w)),
        [(ChangeEffect::Removed, "x.md", Uncounted)]
    );
}

/// A creates X; then B deletes it: X Removed, 1 removed.
#[test]
fn an_own_create_then_another_sessions_delete_counts_one_removed() {
    let (_tmp, a, b) = two_hosts(|_| {});
    a.create_exclusive("x.md", "mine\n").unwrap();
    b.delete_file("x.md").unwrap();

    let w = window(&a);
    assert_eq!(counts(&w), (0, 1));
    assert_eq!(
        files(&records(&a, &w)),
        [(ChangeEffect::Removed, "x.md", ChangeCount::Removed)]
    );
}

/// B renames X to Y: a Renamed record X → Y carrying the hash A's tabs at X
/// hold, X Removed and Y Present, counted 1 new and 1 removed (Q4).
#[test]
fn a_foreign_rename_is_a_renamed_record_counting_one_new_and_one_removed() {
    let (_tmp, a, b) = two_hosts(|p| {
        p.write_file("x.md", b"x body\n").unwrap();
    });
    let x_hash = hash_of(&a, "x.md");
    b.rename_file("x.md", "y.md").unwrap();

    let w = window(&a);
    assert_eq!(counts(&w), (1, 1));
    let records = records(&a, &w);
    assert_eq!(
        files(&records),
        [
            (Renamed, "y.md", Uncounted),
            (ChangeEffect::Removed, "x.md", ChangeCount::Removed),
            (Present, "y.md", Created)
        ]
    );
    let rename = renamed(&records, "y.md");
    assert_eq!(rename.renamed_from.as_deref(), Some("x.md"));
    assert_eq!(rename.from_hash.as_deref(), Some(x_hash.as_str()));
}

/// B renames X to Y; then A's user renames Y to Z: one Renamed record from
/// X, where A's tab is, to Z, where the file is.
#[test]
fn another_sessions_rename_then_an_own_rename_is_one_renamed_record() {
    let (_tmp, a, b) = two_hosts(|p| {
        p.write_file("x.md", b"x body\n").unwrap();
    });
    let x_hash = hash_of(&a, "x.md");
    b.rename_file("x.md", "y.md").unwrap();
    a.rename_file("y.md", "z.md").unwrap();

    let w = window(&a);
    assert_eq!(counts(&w), (1, 1));
    let records = records(&a, &w);
    assert_eq!(
        files(&records),
        [
            (Renamed, "z.md", Uncounted),
            (ChangeEffect::Removed, "x.md", ChangeCount::Removed),
            (ChangeEffect::Removed, "y.md", Uncounted),
            (Present, "z.md", Created)
        ]
    );
    let rename = renamed(&records, "z.md");
    assert_eq!(rename.renamed_from.as_deref(), Some("x.md"));
    assert_eq!(rename.from_hash.as_deref(), Some(x_hash.as_str()));
}

/// B renames `readme.md` to `README.md` on a case-insensitive volume, and
/// A's next save of its tab binds to `README.md` (#1077): the save is an own
/// write of a path A does not show, so B's rename stays counted.
#[test]
fn an_own_save_under_another_sessions_spelling_keeps_its_rename_counted() {
    let (tmp, a, b) = two_hosts(|p| {
        p.write_file("readme.md", b"read me\n").unwrap();
    });
    let probe = tmp.path().join("Probe-Case.md");
    std::fs::write(&probe, b"").unwrap();
    let case_insensitive = tmp.path().join("probe-case.md").exists();
    std::fs::remove_file(&probe).unwrap();
    if !case_insensitive {
        // A case-sensitive volume (the Linux lane) keeps both spellings
        // apart; the derivation's row is pinned by
        // `change_feed::tests::the_design_table_derives_row_by_row`.
        return;
    }
    let tab_hash = hash_of(&a, "readme.md");
    b.rename_file("readme.md", "README.md").unwrap();
    a.save_text("readme.md", "read me, edited\n", Some(&tab_hash))
        .unwrap();
    assert!(a.get_file_metadata("README.md").unwrap().is_some());

    let w = window(&a);
    assert_eq!(counts(&w), (1, 1));
    let records = records(&a, &w);
    assert_eq!(
        files(&records),
        [
            (Renamed, "README.md", Uncounted),
            (ChangeEffect::Removed, "readme.md", ChangeCount::Removed),
            (Present, "README.md", Created)
        ]
    );
    assert_eq!(
        renamed(&records, "README.md").renamed_from.as_deref(),
        Some("readme.md")
    );
}

/// B creates X; A's user opens and saves it before the run: X Present, 1
/// new — the save adds nothing to A's Quick Open.
#[test]
fn another_sessions_create_then_an_own_save_counts_one_new() {
    let (_tmp, a, b) = two_hosts(|_| {});
    let created = b.create_exclusive("x.md", "from B\n").unwrap();
    let saved = a
        .save_text("x.md", "from B, then A\n", Some(&created.new_content_hash))
        .unwrap();

    let w = window(&a);
    assert_eq!(counts(&w), (1, 0));
    let records = records(&a, &w);
    assert_eq!(files(&records), [(Present, "x.md", Created)]);
    assert_eq!(
        records[0].content_hash.as_deref(),
        Some(saved.new_content_hash.as_str())
    );
}

/// B renames X to Y; then A's user renames another note W to X: the Renamed
/// record X → Y carries X's old hash, which only the old note's tab holds,
/// so the tab A moved to X stays; X and Y Present, 1 new.
#[test]
fn a_renamed_record_carries_the_hash_only_the_old_tabs_hold() {
    let (_tmp, a, b) = two_hosts(|p| {
        p.write_file("x.md", b"the old x\n").unwrap();
        p.write_file("w.md", b"another note\n").unwrap();
    });
    let x_hash = hash_of(&a, "x.md");
    let w_hash = hash_of(&a, "w.md");
    b.rename_file("x.md", "y.md").unwrap();
    a.rename_file("w.md", "x.md").unwrap();

    let win = window(&a);
    assert_eq!(counts(&win), (1, 0));
    let records = records(&a, &win);
    assert_eq!(
        files(&records),
        [
            (Renamed, "y.md", Uncounted),
            (Present, "x.md", Uncounted),
            (Present, "y.md", Created)
        ]
    );
    let rename = renamed(&records, "y.md");
    assert_eq!(rename.renamed_from.as_deref(), Some("x.md"));
    assert_eq!(rename.from_hash.as_deref(), Some(x_hash.as_str()));
    assert_ne!(rename.from_hash.as_deref(), Some(w_hash.as_str()));
}

/// B edits X, then edits it back: X Present, and nothing is counted.
#[test]
fn a_path_changed_and_changed_back_is_an_uncounted_present_record() {
    let (_tmp, a, b) = two_hosts(|p| {
        p.write_file("x.md", b"original\n").unwrap();
    });
    let original = hash_of(&b, "x.md");
    let edited = b.save_text("x.md", "changed\n", Some(&original)).unwrap();
    b.save_text("x.md", "original\n", Some(&edited.new_content_hash))
        .unwrap();

    let w = window(&a);
    assert_eq!(counts(&w), (0, 0));
    assert_eq!(files(&records(&a, &w)), [(Present, "x.md", Uncounted)]);
}

/// A's scan finds X edited outside Slate: X Present, 1 modified, in A's own
/// window — a reconcile row is never an own write.
#[test]
fn an_own_scans_change_is_counted_by_its_own_window() {
    let (tmp, a) = make_vault(|p| {
        p.write_file("x.md", b"alpha\n").unwrap();
    });
    a.scan_initial(&CancelToken::new()).unwrap();
    a.register_change_feed().unwrap();
    edit_on_disk(&tmp, "x.md", b"omega\n");
    a.rescan_with_progress(&CancelToken::new(), None).unwrap();

    let w = window(&a);
    assert_eq!(counts(&w), (1, 0));
    assert_eq!(files(&records(&a, &w)), [(Present, "x.md", Modified)]);
}

/// A saves X: no record and no count.
#[test]
fn an_own_save_is_neither_a_record_nor_a_count() {
    let (_tmp, a, _b) = two_hosts(|p| {
        p.write_file("x.md", b"x\n").unwrap();
    });
    let before = hash_of(&a, "x.md");
    a.save_text("x.md", "saved by A\n", Some(&before)).unwrap();

    let w = window(&a);
    assert_eq!(w.status, ChangeFeedStatus::Exact);
    assert!(w.counted);
    assert_eq!((w.record_count, w.changed, w.removed), (0, 0, 0));
}

/// A range of this session's own writes alone is an empty exact window
/// however large: a folder move of 10,001 files already reached the host
/// as its own events. Another session's window over the same rows is too
/// large.
#[test]
fn an_own_folder_move_past_the_row_limit_is_an_empty_exact_window() {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::create_dir(tmp.path().join("Big")).unwrap();
    for index in 0..=crate::CHANGE_FEED_WINDOW_ROW_LIMIT {
        std::fs::write(tmp.path().join(format!("Big/{index}.txt")), b"t").unwrap();
    }
    let a = open(&tmp);
    a.register_change_feed().unwrap();
    let b = open(&tmp);
    b.register_change_feed().unwrap();
    let before = head(&a);
    a.rename_folder("Big", "Moved").unwrap();
    assert!(
        head(&a) - before > crate::CHANGE_FEED_WINDOW_ROW_LIMIT,
        "one row per moved file and folder"
    );

    let own = window(&a);
    assert_eq!(own.status, ChangeFeedStatus::Exact);
    assert!(own.counted);
    assert_eq!((own.record_count, own.changed, own.removed), (0, 0, 0));

    let other = window(&b);
    assert_eq!(other.status, ChangeFeedStatus::TooLarge);
    assert!(!other.counted);
    assert_eq!(other.record_count, 0);
}

// --- Cursors, registration and the gate ----------------------------------------

/// The triggers write nothing while no session holds a cursor: the feed
/// costs one gate check per mutation until a host registers.
#[test]
fn no_feed_rows_without_a_cursor() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"a\n").unwrap();
        p.write_file("b.md", b"b\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let before = hash_of(&session, "a.md");
    session
        .save_text("a.md", "a, saved\n", Some(&before))
        .unwrap();
    session.create_exclusive("c.md", "c\n").unwrap();
    session.rename_file("b.md", "bee.md").unwrap();
    session.create_folder("Dir").unwrap();
    session.delete_file("c.md").unwrap();
    edit_on_disk(&tmp, "bee.md", b"bee, outside\n");
    session
        .rescan_with_progress(&CancelToken::new(), None)
        .unwrap();

    assert_eq!(feed_rows(&session), 0);
    assert_eq!(head(&session), 0);
    assert_eq!(cursor_of(&session), None);
}

/// Registration sets the cursor to the head and returns it, after the open
/// scan: the open scan's own changes stay out of the first window, while
/// another registered session counts them.
#[test]
fn register_moves_the_cursor_to_the_head() {
    let (tmp, a) = make_vault(|p| {
        p.write_file("x.md", b"x\n").unwrap();
    });
    a.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(a.register_change_feed().unwrap(), 0);
    assert_eq!(cursor_of(&a), Some((0, false)));
    a.create_exclusive("own.md", "own\n").unwrap();
    assert_eq!(head(&a), 1);

    // B's open scan reconciles an outside edit with A's cursor holding the
    // gate open, then registers at the head after it.
    edit_on_disk(&tmp, "x.md", b"x, outside\n");
    let b = open(&tmp);
    let head_after_scan = head(&b);
    assert_eq!(head_after_scan, 2, "B's open scan wrote its reconcile row");
    assert_eq!(b.register_change_feed().unwrap(), head_after_scan);
    assert_eq!(cursor_of(&b), Some((head_after_scan as i64, false)));

    let first = window(&b);
    assert_eq!(first.from_seq, head_after_scan);
    assert_eq!(
        (first.record_count, first.changed, first.removed),
        (0, 0, 0),
        "the open scan already spoke its changes"
    );
    let a_window = window(&a);
    assert_eq!(counts(&a_window), (1, 0), "A counts B's scan of x.md");
    assert_eq!(
        files(&records(&a, &a_window)),
        [(Present, "x.md", Modified)]
    );
}

/// The head is AUTOINCREMENT's high-water mark: trimming every row never
/// moves it back, and the next row follows it.
#[test]
fn the_head_survives_trimming_every_row() {
    let (_tmp, a, b) = two_hosts(|_| {});
    b.create_exclusive("one.md", "1\n").unwrap();
    b.create_exclusive("two.md", "2\n").unwrap();
    let head_before = head(&a);
    assert_eq!(head_before, 2);
    for session in [&a, &b] {
        let w = window(session);
        ack(session, &w);
    }
    assert_eq!(feed_rows(&a), 0, "every row is below every live cursor");
    assert_eq!(head(&a), head_before);
    b.create_exclusive("three.md", "3\n").unwrap();
    let next: i64 = a
        .conn
        .lock()
        .unwrap()
        .query_row("SELECT MAX(seq) FROM change_feed", [], |row| row.get(0))
        .unwrap();
    assert_eq!(next as u64, head_before + 1);
    let w = window(&a);
    assert_eq!(w.from_seq, head_before);
    assert_eq!(counts(&w), (1, 0));
}

/// A lone host's acknowledgment trims every row it covered, and its next
/// window is exact (the trim floor equals its cursor, which is no gap).
#[test]
fn a_lone_hosts_next_window_after_its_acknowledgment_is_exact() {
    let (tmp, a) = make_vault(|p| {
        p.write_file("x.md", b"alpha\n").unwrap();
    });
    a.scan_initial(&CancelToken::new()).unwrap();
    a.register_change_feed().unwrap();
    edit_on_disk(&tmp, "x.md", b"omega\n");
    a.rescan_with_progress(&CancelToken::new(), None).unwrap();
    let first = window(&a);
    assert_eq!(counts(&first), (1, 0));
    ack(&a, &first);
    assert_eq!(feed_rows(&a), 0);

    let next = window(&a);
    assert_eq!(next.status, ChangeFeedStatus::Exact);
    assert!(next.counted);
    assert_eq!(next.from_seq, first.to_seq);
    assert_eq!((next.record_count, next.changed, next.removed), (0, 0, 0));
}

/// A cursor silent for 24 hours is reaped by another session's
/// registration, and the reaped session's next window is a gap. Its own old
/// heartbeat never makes its own window a gap: a laptop asleep for a weekend
/// with one Slate open gets an exact window when it wakes.
#[test]
fn a_reaped_cursor_is_a_gap() {
    let (tmp, a) = make_vault(|p| {
        p.write_file("x.md", b"x\n").unwrap();
    });
    a.scan_initial(&CancelToken::new()).unwrap();
    a.register_change_feed().unwrap();
    let age_cursor = |session: &VaultSession| {
        session
            .conn
            .lock()
            .unwrap()
            .execute(
                "UPDATE change_feed_cursors SET heartbeat_ms = heartbeat_ms - ?1",
                [crate::CHANGE_FEED_CURSOR_LIVENESS_MS + 60_000],
            )
            .unwrap();
    };
    age_cursor(&a);
    let asleep = window(&a);
    assert_eq!(
        asleep.status,
        ChangeFeedStatus::Exact,
        "its own old heartbeat"
    );
    ack(&a, &asleep);
    assert!(
        cursor_of(&a).is_some(),
        "its own acknowledgment never reaps it"
    );

    age_cursor(&a);
    let b = open(&tmp);
    b.register_change_feed().unwrap();
    assert_eq!(cursor_of(&a), None, "B's registration reaped A's cursor");
    let gap = window(&a);
    assert_eq!(gap.status, ChangeFeedStatus::Gap(ChangeFeedGap::NoCursor));
    assert!(!gap.counted);
    assert_eq!((gap.record_count, gap.changed, gap.removed), (0, 0, 0));
}

/// Spec fact 2: rows a lagging cursor needed are trimmed by the 100,000-row
/// cap; its window is a gap that re-creates the cursor at the head, marked
/// pending; the next window stays a gap until the full re-sync is
/// acknowledged, and then it is exact again.
#[test]
fn a_trimmed_window_is_a_gap_and_the_acknowledgment_heals_it() {
    let (_tmp, a, b) = two_hosts(|_| {});
    let cap = crate::CHANGE_FEED_MAX_ROWS as usize;
    indexed_rows(&b, "bulk-", cap + 5);
    let b_window = window(&b);
    ack(&b, &b_window);
    assert_eq!(feed_rows(&a), cap as i64, "the cap trimmed the oldest rows");

    let gap = window(&a);
    assert_eq!(gap.status, ChangeFeedStatus::Gap(ChangeFeedGap::Trimmed));
    assert!(!gap.counted);
    assert_eq!(gap.record_count, 0);
    assert_eq!(gap.to_seq, head(&a));
    assert_eq!(cursor_of(&a), Some((gap.to_seq as i64, true)));

    // The re-sync has not been acknowledged: the next run is a gap too.
    let still = window(&a);
    assert_eq!(
        still.status,
        ChangeFeedStatus::Gap(ChangeFeedGap::ResyncPending)
    );
    ack(&a, &still);
    assert_eq!(cursor_of(&a), Some((still.to_seq as i64, false)));

    b.create_exclusive("after.md", "after\n").unwrap();
    let healed = window(&a);
    assert_eq!(healed.status, ChangeFeedStatus::Exact);
    assert!(healed.counted);
    assert_eq!(counts(&healed), (1, 0));
}

/// A lone host whose cursor vanished (a cache rebuilt under it, say): its
/// gap re-creates the cursor, so the triggers stay on during the full
/// re-sync, and a write made meanwhile reaches the run's later pass.
#[test]
fn a_gap_recreates_the_cursor_so_a_write_during_the_resync_has_a_row() {
    let (tmp, a) = make_vault(|p| {
        p.write_file("x.md", b"x\n").unwrap();
    });
    a.scan_initial(&CancelToken::new()).unwrap();
    a.register_change_feed().unwrap();
    a.conn
        .lock()
        .unwrap()
        .execute("DELETE FROM change_feed_cursors", [])
        .unwrap();

    let gap = window(&a);
    assert_eq!(gap.status, ChangeFeedStatus::Gap(ChangeFeedGap::NoCursor));
    assert_eq!(cursor_of(&a), Some((gap.to_seq as i64, true)));

    // During the full re-sync, an unregistered session (the CLI) writes.
    let cli = VaultSession::from_filesystem_with_actor(tmp.path().to_path_buf(), "cli").unwrap();
    cli.create_exclusive("during.md", "written during the re-sync\n")
        .unwrap();
    assert_eq!(feed_rows(&a), 1, "the re-created cursor kept the gate on");

    let further = window_after(&a, gap.to_seq);
    assert_eq!(further.status, ChangeFeedStatus::Exact);
    assert!(!further.counted, "a pending re-sync's run is uncounted");
    assert_eq!(
        files(&records(&a, &further)),
        [(Present, "during.md", Uncounted)]
    );
    ack(&a, &further);
    assert_eq!(cursor_of(&a), Some((further.to_seq as i64, false)));
}

/// A run cancelled during its full re-sync acknowledges nothing, so the
/// next run's first window is still a gap: a cancelled re-sync is never
/// taken for a finished one.
#[test]
fn a_cancelled_resync_stays_a_gap() {
    let (_tmp, a) = make_vault(|_| {});
    a.scan_initial(&CancelToken::new()).unwrap();
    a.register_change_feed().unwrap();
    a.conn
        .lock()
        .unwrap()
        .execute("DELETE FROM change_feed_cursors", [])
        .unwrap();
    let gap = window(&a);
    assert_eq!(gap.status, ChangeFeedStatus::Gap(ChangeFeedGap::NoCursor));
    // The host's re-sync is cancelled: no acknowledgment.
    for _ in 0..2 {
        let next = window(&a);
        assert_eq!(
            next.status,
            ChangeFeedStatus::Gap(ChangeFeedGap::ResyncPending)
        );
        assert!(!next.counted);
    }
    let last = window(&a);
    ack(&a, &last);
    assert_eq!(window(&a).status, ChangeFeedStatus::Exact);
}

/// A window of more than 10,000 rows, not all this session's own, is too
/// large and reads nothing; a window of exactly 10,000 is exact.
#[test]
fn a_window_over_the_row_limit_is_too_large_and_reads_nothing() {
    let limit = crate::CHANGE_FEED_WINDOW_ROW_LIMIT as usize;
    let (_tmp, a, b) = two_hosts(|_| {});
    indexed_rows(&b, "at-limit-", limit);
    window_test_hook::take_rows_derived();
    let exact = window(&a);
    assert_eq!(exact.status, ChangeFeedStatus::Exact);
    assert!(exact.counted);
    assert_eq!(counts(&exact), (limit as u64, 0));
    assert_eq!(exact.record_count, limit as u64);
    assert_eq!(window_test_hook::take_rows_derived(), limit as u64);
    ack(&a, &exact);

    indexed_rows(&b, "over-limit-", limit + 1);
    let too_large = window(&a);
    assert_eq!(too_large.status, ChangeFeedStatus::TooLarge);
    assert!(!too_large.counted);
    assert_eq!(
        (too_large.record_count, too_large.changed, too_large.removed),
        (0, 0, 0)
    );
    assert_eq!(
        window_test_hook::take_rows_derived(),
        0,
        "a too-large window reads none of its rows"
    );
    assert!(records(&a, &too_large).is_empty());
}

/// A run's later pass reports the run's cumulative counts from the cursor.
/// A run whose range from the cursor passes the row limit is uncounted,
/// though its later window is still exact and carries records.
#[test]
fn a_later_pass_counts_from_the_cursor_and_past_the_limit_is_uncounted() {
    let (_tmp, a, b) = two_hosts(|_| {});
    b.create_exclusive("first.md", "1\n").unwrap();
    let first = window(&a);
    assert_eq!(counts(&first), (1, 0));
    b.create_exclusive("second.md", "2\n").unwrap();
    let later = window_after(&a, first.to_seq);
    assert_eq!(later.from_seq, first.to_seq);
    assert!(later.counted);
    assert_eq!(counts(&later), (2, 0), "the run's cumulative counts");
    assert_eq!(
        files(&records(&a, &later)),
        [(Present, "second.md", Created)]
    );
    ack(&a, &later);

    let limit = crate::CHANGE_FEED_WINDOW_ROW_LIMIT as usize;
    indexed_rows(&b, "pass-one-", limit - 10);
    let pass_one = window(&a);
    assert_eq!(pass_one.status, ChangeFeedStatus::Exact);
    assert!(pass_one.counted);
    indexed_rows(&b, "pass-two-", 20);
    let pass_two = window_after(&a, pass_one.to_seq);
    assert_eq!(pass_two.status, ChangeFeedStatus::Exact);
    assert!(
        !pass_two.counted,
        "the run's range from the cursor is past the limit"
    );
    assert_eq!((pass_two.changed, pass_two.removed), (0, 0));
    assert_eq!(pass_two.record_count, 20);
    assert!(
        records(&a, &pass_two)
            .iter()
            .all(|record| record.counted == Uncounted)
    );
}

/// A further window cannot start before the session's cursor.
#[test]
fn a_window_never_starts_before_the_cursor() {
    let (_tmp, a, b) = two_hosts(|_| {});
    b.create_exclusive("x.md", "x\n").unwrap();
    let w = window(&a);
    ack(&a, &w);
    match a.change_feed_window(Some(0), &CancelToken::new()) {
        Err(VaultError::InvalidArgument { message }) => assert_eq!(
            message,
            "a change feed window must start at or after the session's cursor"
        ),
        other => panic!("expected a refusal, got {other:?}"),
    }
}

// --- Acknowledgment ---------------------------------------------------------------

/// An acknowledgment that fails (here a schema skew: another process
/// migrated the cache) is one error for the run, and the next run reads the
/// same window again: its counts are spoken once, by the run that
/// acknowledges them.
#[test]
fn a_failed_acknowledgment_is_one_error_and_the_window_is_read_again() {
    let (tmp, a, b) = two_hosts(|_| {});
    b.create_exclusive("x.md", "x\n").unwrap();
    let first = window(&a);
    assert_eq!(counts(&first), (1, 0));
    let skew = |insert: bool| {
        let conn = rusqlite::Connection::open(tmp.path().join(".slate/cache.sqlite")).unwrap();
        if insert {
            conn.execute(
                "INSERT INTO schema_version (version, applied_at_ms, description)
                 VALUES (9999, 0, 'a later build')",
                [],
            )
            .unwrap();
        } else {
            conn.execute("DELETE FROM schema_version WHERE version = 9999", [])
                .unwrap();
        }
    };
    skew(true);
    let failed = a.acknowledge_change_feed(&first.token, &CancelToken::new());
    skew(false);
    assert!(
        matches!(
            failed,
            Err(VaultError::Db(crate::db::DbError::SchemaVersionSkew { .. }))
        ),
        "{failed:?}"
    );
    assert_eq!(
        cursor_of(&a).map(|(seq, _)| seq),
        Some(first.from_seq as i64)
    );

    let again = window(&a);
    assert_eq!(
        (again.from_seq, again.to_seq),
        (first.from_seq, first.to_seq)
    );
    assert_eq!(counts(&again), (1, 0));
    assert_eq!(files(&records(&a, &again)), [(Present, "x.md", Created)]);
    ack(&a, &again);
    assert_eq!(counts(&window(&a)), (0, 0));
}

/// A cancelled acknowledgment changes nothing: not the cursor, not the
/// heartbeat, not a row — whether it is cancelled before it starts or in
/// the middle of its trim.
#[test]
fn a_cancelled_acknowledgment_changes_nothing() {
    let (_tmp, a, b) = two_hosts(|_| {});
    indexed_rows(&b, "trim-", 20_000);
    let b_window = window(&b);
    // A's cursor moves to the head, so B's trim would delete every row.
    let a_window = window(&a);
    ack(&a, &a_window);
    let snapshot = |session: &VaultSession| -> (Option<(i64, bool)>, i64, i64) {
        let heartbeat: i64 = session
            .conn
            .lock()
            .unwrap()
            .query_row(
                "SELECT heartbeat_ms FROM change_feed_cursors WHERE writer = ?1",
                [session.change_feed.writer()],
                |row| row.get(0),
            )
            .unwrap();
        (cursor_of(session), feed_rows(session), heartbeat)
    };
    // Age B's heartbeat a little (never past the liveness window), so a
    // committed acknowledgment would visibly move it.
    b.conn
        .lock()
        .unwrap()
        .execute(
            "UPDATE change_feed_cursors SET heartbeat_ms = heartbeat_ms - 60000 WHERE writer = ?1",
            [b.change_feed.writer()],
        )
        .unwrap();
    let before = snapshot(&b);

    let cancelled = CancelToken::new();
    cancelled.cancel();
    assert!(matches!(
        b.acknowledge_change_feed(&b_window.token, &cancelled),
        Err(VaultError::Cancelled)
    ));
    assert_eq!(snapshot(&b), before);

    // B's trim of all 20,000 rows is cancelled from inside SQLite's
    // progress handler on the way.
    let mid_trim = CancelToken::new();
    let trip = mid_trim.clone();
    crate::session::directory_page::progress_test_hook::install(Box::new(move || {
        trip.cancel();
    }));
    let result = b.acknowledge_change_feed(&b_window.token, &mid_trim);
    crate::session::directory_page::progress_test_hook::clear();
    assert!(matches!(result, Err(VaultError::Cancelled)), "{result:?}");
    assert_eq!(snapshot(&b), before, "a cancelled trim rolled back");

    ack(&b, &b_window);
    assert_eq!(feed_rows(&b), 0);
}

// --- Pages (spec fact 4) ------------------------------------------------------------

/// Spec fact 4: pages are bounded (limits 0 and 10,001 refused), and five
/// records at limit 2 come in three pages, each record once: renames in
/// `seq` order, then removals by path, then the rest by path. A page
/// resumes by keyset.
#[test]
fn pages_are_bounded_removal_first_and_resume_by_keyset() {
    let (_tmp, a, b) = two_hosts(|p| {
        p.write_file("x.md", b"x\n").unwrap();
        p.write_file("d.md", b"d\n").unwrap();
    });
    b.rename_file("x.md", "y.md").unwrap();
    b.delete_file("d.md").unwrap();
    b.create_exclusive("c.md", "c\n").unwrap();
    let w = window(&a);
    assert_eq!(w.record_count, 5);

    for limit in [0, crate::MAX_CHANGE_FEED_PAGE_LIMIT + 1] {
        assert!(matches!(
            a.change_feed_page(&w.token, None, limit, &CancelToken::new()),
            Err(VaultError::InvalidArgument { .. })
        ));
    }

    let page = |cursor: Option<String>| {
        a.change_feed_page(&w.token, cursor, 2, &CancelToken::new())
            .unwrap()
    };
    let first = page(None);
    let second = page(first.next_cursor.clone());
    let third = page(second.next_cursor.clone());
    assert_eq!(
        (
            first.records.len(),
            second.records.len(),
            third.records.len()
        ),
        (2, 2, 1)
    );
    assert!(first.next_cursor.is_some() && second.next_cursor.is_some());
    assert_eq!(third.next_cursor, None);
    let all: Vec<ChangeRecord> = [first.records, second.records, third.records].concat();
    assert_eq!(
        files(&all),
        [
            (Renamed, "y.md", Uncounted),
            (ChangeEffect::Removed, "d.md", ChangeCount::Removed),
            (ChangeEffect::Removed, "x.md", ChangeCount::Removed),
            (Present, "c.md", Created),
            (Present, "y.md", Created),
        ]
    );
    assert_eq!(
        all,
        records(&a, &w),
        "the keyset pages read each record once"
    );
    assert_eq!(
        page(first.next_cursor).records,
        all[2..4].to_vec(),
        "a page resumes by keyset"
    );
    ack(&a, &w);

    // Renames page in `seq` order, whatever their paths' order.
    b.rename_file("y.md", "zz.md").unwrap();
    b.rename_file("c.md", "aa.md").unwrap();
    let w = window(&a);
    let renames: Vec<String> = records(&a, &w)
        .into_iter()
        .filter(|record| record.effect == Renamed)
        .map(|record| record.path)
        .collect();
    assert_eq!(renames, ["zz.md", "aa.md"]);
}

/// A page honours its token: cancelled before it starts, or from inside
/// SQLite's progress handler while it reads.
#[test]
fn a_page_honours_its_cancel_token() {
    let (_tmp, a, b) = two_hosts(|_| {});
    indexed_rows(&b, "page-", 3_000);
    let w = window(&a);
    assert_eq!(w.record_count, 3_000);

    let cancelled = CancelToken::new();
    cancelled.cancel();
    assert!(matches!(
        a.change_feed_page(&w.token, None, 3_000, &cancelled),
        Err(VaultError::Cancelled)
    ));

    let mid_page = CancelToken::new();
    let trip = mid_page.clone();
    crate::session::directory_page::progress_test_hook::install(Box::new(move || {
        trip.cancel();
    }));
    let result = a.change_feed_page(&w.token, None, 3_000, &mid_page);
    crate::session::directory_page::progress_test_hook::clear();
    assert!(matches!(result, Err(VaultError::Cancelled)), "{result:?}");

    assert_eq!(records(&a, &w).len(), 3_000, "the window is intact");
}

/// A window's token is bound to its session and replaced by the next
/// window: a page or an acknowledgment of a replaced window is refused.
#[test]
fn a_stale_window_token_is_refused() {
    let (_tmp, a, b) = two_hosts(|_| {});
    b.create_exclusive("x.md", "x\n").unwrap();
    let old = window(&a);
    let new = window(&a);
    let stale = |result: Result<(), VaultError>| match result {
        Err(VaultError::InvalidArgument { message }) => {
            assert_eq!(message, "change feed window is stale");
        }
        other => panic!("expected a stale-window refusal, got {other:?}"),
    };
    stale(
        a.change_feed_page(&old.token, None, 10, &CancelToken::new())
            .map(|_| ()),
    );
    stale(a.acknowledge_change_feed(&old.token, &CancelToken::new()));
    assert!(matches!(
        b.change_feed_page(&new.token, None, 10, &CancelToken::new()),
        Err(VaultError::InvalidArgument { .. })
    ));
    assert!(matches!(
        a.change_feed_page("not a token", None, 10, &CancelToken::new()),
        Err(VaultError::InvalidArgument { .. })
    ));
    assert_eq!(records(&a, &new).len(), 1);
}

// --- Crash consistency (spec fact 5) --------------------------------------------

/// Spec fact 5: the feed row and the index mutation commit together.
#[test]
fn the_feed_row_commits_with_the_index_mutation() {
    // A scan cancelled at its commit point leaves neither rows nor feed rows.
    let (tmp, a, b) = two_hosts(|p| {
        p.write_file("keep.md", b"keep\n").unwrap();
    });
    std::fs::write(tmp.path().join("late.md"), b"late\n").unwrap();
    let cancel = CancelToken::new();
    let trip = cancel.clone();
    crate::session::scan_point_test_hook::install(Box::new(move |point| {
        if point == "commit" {
            trip.cancel();
        }
    }));
    let cancelled = a.rescan_with_progress(&cancel, None);
    crate::session::scan_point_test_hook::clear();
    assert!(matches!(cancelled, Err(VaultError::Cancelled)));
    assert!(a.get_file_metadata("late.md").unwrap().is_none());
    assert_eq!(feed_rows(&a), 0);

    // A save whose index commit fails (the existing post-write fault seam)
    // leaves no feed row; the next scan by any session reconciles the file
    // and every session counts that row.
    std::fs::rename(
        tmp.path().join("keep.md"),
        tmp.path().join("feed-fault-keep.md"),
    )
    .unwrap();
    a.rescan_with_progress(&CancelToken::new(), None).unwrap();
    for session in [&a, &b] {
        let w = window(session);
        ack(session, &w);
    }
    let before = hash_of(&b, "feed-fault-keep.md");
    let faulted = {
        let mut _env = fault_guard();
        _env.set("SLATE_TEST_FAULT_AFTER_WRITE", "feed-fault-keep.md");
        let faulted = b.save_text(
            "feed-fault-keep.md",
            "saved, never indexed\n",
            Some(&before),
        );
        _env.clear("SLATE_TEST_FAULT_AFTER_WRITE");
        faulted
    };
    assert!(
        matches!(faulted, Err(VaultError::SavedButUnindexed { .. })),
        "{faulted:?}"
    );
    assert_eq!(
        feed_rows(&a),
        0,
        "the failed index commit wrote no feed row"
    );
    a.rescan_with_progress(&CancelToken::new(), None).unwrap();
    for session in [&a, &b] {
        let w = window(session);
        assert_eq!(counts(&w), (1, 0));
        ack(session, &w);
    }

    // Inside a committed scan, a file whose upsert statement fails leaves no
    // row for it, and a file whose upsert landed before a later statement
    // failed keeps a row that matches the committed index.
    for (path, body) in [
        ("bad.md", b"# Bad\n".as_slice()),
        ("half.md", b"# Half\n".as_slice()),
        ("good.md", b"# Good\n".as_slice()),
    ] {
        std::fs::write(tmp.path().join(path), body).unwrap();
    }
    a.rescan_with_progress(&CancelToken::new(), None).unwrap();
    for session in [&a, &b] {
        let w = window(session);
        ack(session, &w);
    }
    for (path, body) in [
        ("bad.md", b"# Bad, edited\n".as_slice()),
        ("half.md", b"# Half, edited\n".as_slice()),
        ("good.md", b"# Good, edited\n".as_slice()),
    ] {
        edit_on_disk(&tmp, path, body);
    }
    a.conn
        .lock()
        .unwrap()
        .execute_batch(
            "CREATE TEMP TRIGGER fail_bad_upsert AFTER UPDATE OF content_hash ON files
             WHEN NEW.path = 'bad.md'
             BEGIN SELECT RAISE(ABORT, 'injected upsert failure'); END;
             CREATE TEMP TRIGGER fail_half_headings BEFORE INSERT ON headings
             WHEN NEW.file_id = (SELECT id FROM files WHERE path = 'half.md')
             BEGIN SELECT RAISE(ABORT, 'injected heading failure'); END;",
        )
        .unwrap();
    let report = a.rescan_with_progress(&CancelToken::new(), None).unwrap();
    a.conn
        .lock()
        .unwrap()
        .execute_batch("DROP TRIGGER temp.fail_bad_upsert; DROP TRIGGER temp.fail_half_headings;")
        .unwrap();
    assert_eq!(report.error_count, 2, "{:?}", report.error_samples);
    let rows: Vec<(String, Option<String>)> = {
        let conn = a.conn.lock().unwrap();
        let mut stmt = conn
            .prepare("SELECT path, hash_after FROM change_feed ORDER BY seq")
            .unwrap();
        stmt.query_map([], |row| Ok((row.get(0)?, row.get(1)?)))
            .unwrap()
            .collect::<Result<_, _>>()
            .unwrap()
    };
    let paths: Vec<&str> = rows.iter().map(|(path, _)| path.as_str()).collect();
    assert!(
        !paths.contains(&"bad.md"),
        "the rolled-back statement's row: {rows:?}"
    );
    for path in ["half.md", "good.md"] {
        let (_, hash_after) = rows
            .iter()
            .find(|(row_path, _)| row_path == path)
            .unwrap_or_else(|| panic!("{path}'s landed upsert has its row: {rows:?}"));
        assert_eq!(
            hash_after.as_deref(),
            Some(hash_of(&a, path).as_str()),
            "{path}"
        );
    }
    assert_eq!(
        hash_of(&a, "bad.md"),
        crate::content_hash(b"# Bad\n"),
        "bad.md's index is unchanged"
    );

    // §3.4's child process: a writer that dies before its commit leaves
    // neither row, one that dies right after leaves both.
    a_child_process_that_aborts_commits_both_rows_or_neither();
}

/// The child half of [`a_child_process_that_aborts_commits_both_rows_or_neither`]:
/// with `SLATE_TEST_FEED_CHILD` set to `<cache path>|<before|after>`, open
/// the cache as another process would, upsert a file inside an IMMEDIATE
/// transaction, and `abort()` before or after its commit. Without the
/// variable it does nothing.
#[test]
fn change_feed_child_writer() {
    let Ok(spec) = std::env::var("SLATE_TEST_FEED_CHILD") else {
        return;
    };
    let (cache, mode) = spec.rsplit_once('|').expect("cache|mode");
    let conn = crate::db::open_database(std::path::Path::new(cache), 512).unwrap();
    let seat = crate::change_feed::ChangeFeedSeat::new("child", 0xc41d);
    seat.install(&conn).unwrap();
    let tx = crate::db::begin_fenced(&conn).unwrap();
    tx.execute(
        "INSERT INTO files (path, name, extension, size_bytes, mtime_ms, content_hash,
                            parser_version, indexed_at_ms, is_markdown)
         VALUES ('child.txt', 'child.txt', 'txt', 1, 1, 'child-hash', 1, 1, 0)",
        [],
    )
    .unwrap();
    if mode == "after" {
        tx.commit().unwrap();
    }
    std::process::abort();
}

/// §3.4's child-process abort, part of
/// [`the_feed_row_commits_with_the_index_mutation`]: a writer that dies
/// before its commit leaves neither the index row nor its feed row, and one
/// that dies right after leaves both, with its own writer id; the cache
/// stays intact.
fn a_child_process_that_aborts_commits_both_rows_or_neither() {
    let (tmp, a) = make_vault(|_| {});
    a.scan_initial(&CancelToken::new()).unwrap();
    a.register_change_feed().unwrap();
    let cache = tmp.path().join(".slate/cache.sqlite");
    let state = || -> (bool, Vec<String>, String) {
        let conn = a.conn.lock().unwrap();
        let indexed: bool = conn
            .query_row(
                "SELECT EXISTS(SELECT 1 FROM files WHERE path = 'child.txt')",
                [],
                |row| row.get(0),
            )
            .unwrap();
        let mut stmt = conn
            .prepare("SELECT writer FROM change_feed WHERE path = 'child.txt'")
            .unwrap();
        let writers = stmt
            .query_map([], |row| row.get(0))
            .unwrap()
            .collect::<Result<Vec<String>, _>>()
            .unwrap();
        let integrity: String = conn
            .query_row("PRAGMA integrity_check", [], |row| row.get(0))
            .unwrap();
        (indexed, writers, integrity)
    };
    for mode in ["before", "after"] {
        let status = std::process::Command::new(std::env::current_exe().unwrap())
            .args([
                "session::tests::change_feed::change_feed_child_writer",
                "--exact",
                "--nocapture",
            ])
            .env(
                "SLATE_TEST_FEED_CHILD",
                format!("{}|{mode}", cache.display()),
            )
            .status()
            .unwrap();
        assert!(!status.success(), "the child aborted ({mode})");
        let (indexed, writers, integrity) = state();
        assert_eq!(integrity, "ok", "{mode}");
        match mode {
            "before" => assert_eq!((indexed, writers.len()), (false, 0), "before commit"),
            _ => {
                assert!(indexed, "after commit");
                assert_eq!(writers.len(), 1);
                assert!(writers[0].starts_with("child:"), "{writers:?}");
            }
        }
    }
}

/// `seq` is dense and never reused: a statement that fails on its second row
/// inside a continuing transaction leaves no phantom and no gap, a
/// rolled-back transaction consumes no `seq`, and a row after a trim of
/// every row follows the old head.
#[test]
fn seq_is_dense_and_never_reused() {
    let (_tmp, a) = make_vault(|_| {});
    a.scan_initial(&CancelToken::new()).unwrap();
    a.register_change_feed().unwrap();
    let conn = a.conn.lock().unwrap();
    let insert = |path: &str| {
        format!(
            "INSERT INTO files (path, name, extension, size_bytes, mtime_ms, content_hash,
                                parser_version, indexed_at_ms, is_markdown)
             VALUES ('{path}', '{path}', 'txt', 1, 1, 'h', 1, 1, 0)"
        )
    };
    let seqs = |conn: &rusqlite::Connection| -> Vec<(i64, String)> {
        let mut stmt = conn
            .prepare("SELECT seq, path FROM change_feed ORDER BY seq")
            .unwrap();
        stmt.query_map([], |row| Ok((row.get(0)?, row.get(1)?)))
            .unwrap()
            .collect::<Result<_, _>>()
            .unwrap()
    };
    conn.execute_batch("BEGIN IMMEDIATE").unwrap();
    conn.execute(&insert("d1.txt"), []).unwrap();
    let failed = conn.execute(
        "INSERT INTO files (path, name, extension, size_bytes, mtime_ms, content_hash,
                            parser_version, indexed_at_ms, is_markdown)
         VALUES ('d2.txt', 'd2.txt', 'txt', 1, 1, 'h', 1, 1, 0),
                ('d1.txt', 'd1.txt', 'txt', 1, 1, 'h', 1, 1, 0)",
        [],
    );
    assert!(failed.is_err(), "the second row conflicts");
    conn.execute(&insert("d3.txt"), []).unwrap();
    conn.execute_batch("COMMIT").unwrap();
    assert_eq!(seqs(&conn), [(1, "d1.txt".into()), (2, "d3.txt".into())]);

    conn.execute_batch("BEGIN IMMEDIATE").unwrap();
    conn.execute(&insert("d4.txt"), []).unwrap();
    conn.execute_batch("ROLLBACK").unwrap();
    conn.execute(&insert("d5.txt"), []).unwrap();
    assert_eq!(seqs(&conn).last(), Some(&(3, "d5.txt".into())));

    conn.execute("DELETE FROM change_feed", []).unwrap();
    conn.execute(&insert("d6.txt"), []).unwrap();
    assert_eq!(seqs(&conn), [(4, "d6.txt".into())]);
    assert_eq!(crate::change_feed::head(&conn).unwrap(), 4);
}

/// The head moves exactly for index changes, whoever makes them; the
/// compaction worker's writes (`oplog_events_stale`) move `data_version` but
/// never the head.
#[test]
fn the_head_ignores_compaction_writes() {
    let (tmp, a, b) = two_hosts(|_| {});
    let data_version = |session: &VaultSession| -> i64 {
        session
            .conn
            .lock()
            .unwrap()
            .pragma_query_value(None, "data_version", |row| row.get(0))
            .unwrap()
    };
    let head_before = head(&a);
    let version_before = data_version(&a);
    let worker = crate::db::open_database(&tmp.path().join(".slate/cache.sqlite"), 64).unwrap();
    worker
        .execute("INSERT INTO oplog_events_stale (marker) VALUES (1)", [])
        .unwrap();
    assert_ne!(data_version(&a), version_before, "the worker's commit");
    assert_eq!(head(&a), head_before, "is no index change");
    b.create_exclusive("x.md", "x\n").unwrap();
    assert_eq!(head(&a), head_before + 1);
}

/// The window is one snapshot: another session's commit while it
/// materializes is not in it — its summary and hash are the target's — and
/// the next window has it.
#[test]
fn the_window_is_one_snapshot() {
    let (_tmp, a, b) = two_hosts(|_| {});
    let created = b.create_exclusive("x.md", "one\n").unwrap();
    let b = Arc::new(b);
    let racing = Arc::clone(&b);
    let raced = Arc::new(Mutex::new(None));
    let landed = Arc::clone(&raced);
    let committed = created.new_content_hash.clone();
    window_test_hook::install(Box::new(move || {
        if landed.lock().unwrap().is_none() {
            let saved = racing
                .save_text("x.md", "two, much longer\n", Some(&committed))
                .unwrap();
            *landed.lock().unwrap() = Some(saved.new_content_hash);
        }
    }));
    let w = window(&a);
    window_test_hook::clear();
    assert!(raced.lock().unwrap().is_some(), "the racing commit ran");
    let first = records(&a, &w);
    assert_eq!(files(&first), [(Present, "x.md", Created)]);
    assert_eq!(
        first[0].content_hash.as_deref(),
        Some(created.new_content_hash.as_str())
    );
    assert_eq!(first[0].summary.as_ref().unwrap().size_bytes, 4);
    ack(&a, &w);

    let next = window(&a);
    assert_eq!(counts(&next), (1, 0));
    let second = records(&a, &next);
    assert_eq!(files(&second), [(Present, "x.md", Modified)]);
    assert_eq!(
        second[0].content_hash,
        raced.lock().unwrap().clone(),
        "the next window has the racing commit"
    );
}

/// A raw SQLite connection without Slate's functions cannot change a
/// watched column of `files` — with a cursor present, and with none (the
/// trigger's body is compiled with the statement) — while an unwatched
/// column stays writable (§9 residual 4).
#[test]
fn a_connection_without_slate_functions_cannot_change_a_watched_column() {
    for registered in [true, false] {
        let (tmp, a) = make_vault(|p| {
            p.write_file("a.md", b"a\n").unwrap();
        });
        a.scan_initial(&CancelToken::new()).unwrap();
        if registered {
            a.register_change_feed().unwrap();
        }
        let raw = rusqlite::Connection::open(tmp.path().join(".slate/cache.sqlite")).unwrap();
        let refused = raw
            .execute(
                "UPDATE files SET content_hash = 'raw' WHERE path = 'a.md'",
                [],
            )
            .unwrap_err()
            .to_string();
        assert!(
            refused.contains("no such function: slate_feed_writer"),
            "registered {registered}: {refused}"
        );
        raw.execute("UPDATE files SET indexed_at_ms = 7 WHERE path = 'a.md'", [])
            .unwrap();
        assert_eq!(hash_of(&a, "a.md"), crate::content_hash(b"a\n"));
        assert_eq!(feed_rows(&a), 0);
    }
}

/// The constants the owner accepted (design §4.2, §4.3, §4.6; Q5).
#[test]
fn the_change_feed_constants_are_pinned() {
    assert_eq!(crate::MAX_CHANGE_FEED_PAGE_LIMIT, 10_000);
    assert_eq!(crate::CHANGE_FEED_WINDOW_ROW_LIMIT, 10_000);
    assert_eq!(crate::CHANGE_FEED_MAX_ROWS, 100_000);
    assert_eq!(crate::CHANGE_FEED_CURSOR_LIVENESS_MS, 86_400_000);
    assert_eq!(crate::CHANGE_FEED_MAX_PASSES, 3);
}

/// A closing session deletes its cursor, so it holds no rows from being
/// trimmed; a session that never registered deletes nothing.
#[test]
fn a_closing_session_deletes_its_cursor() {
    let (tmp, a, b) = two_hosts(|_| {});
    let cli = VaultSession::from_filesystem_with_actor(tmp.path().to_path_buf(), "cli").unwrap();
    drop(cli);
    let count = |session: &VaultSession| -> i64 {
        session
            .conn
            .lock()
            .unwrap()
            .query_row("SELECT COUNT(*) FROM change_feed_cursors", [], |row| {
                row.get(0)
            })
            .unwrap()
    };
    assert_eq!(count(&a), 2);
    drop(b);
    assert_eq!(count(&a), 1);
    assert!(cursor_of(&a).is_some());
}

// --- WB-10a (#1519): a window carrying another writer's change drops the link graph ---

/// The center's edges in this session's link graph (building it if absent).
fn edges_into(session: &VaultSession, center: &str) -> usize {
    session
        .graph_neighborhood(center, 1, crate::graph::GraphFilter::default())
        .unwrap()
        .edges
        .len()
}

/// WB-10a (#1519): B adds a link; A's window is exact and carries B's
/// record, so it drops A's in-memory graph: the generation moves (the
/// hosts' probes read "changed") and the next query rebuilds from the
/// shared index and finds the link.
#[test]
fn a_foreign_link_change_in_an_exact_window_drops_this_sessions_graph() {
    let (_tmp, a, b) = two_hosts(|p| {
        p.write_file("a.md", b"# A\n").unwrap();
        p.write_file("b.md", b"# B\n").unwrap();
    });
    assert_eq!(edges_into(&a, "b.md"), 0);
    let before = a.graph_generation();
    assert!(before > 0, "premise: A's graph is built");
    let hash = hash_of(&b, "a.md");
    b.save_text("a.md", "# A\n\n[[b]]\n", Some(&hash)).unwrap();

    let w = window(&a);
    assert_eq!(w.status, ChangeFeedStatus::Exact);
    assert!(
        w.record_count > 0,
        "premise: B's save is a record in A's window"
    );
    assert_ne!(
        a.graph_generation(),
        before,
        "the probes must read a change"
    );
    assert_eq!(edges_into(&a, "b.md"), 1, "the rebuilt graph has B's link");
}

/// WB-10a (#1519): a folder another session renamed moves every note's path
/// under it, and the graph is keyed by path — so a window of directory and
/// file renames drops it. A directory record alone counts too: it still
/// says another writer changed the tree this session's graph was built on.
#[test]
fn a_foreign_folder_rename_in_an_exact_window_drops_the_graph() {
    let (_tmp, a, b) = two_hosts(|p| {
        p.write_file("dir/linked.md", b"# Linked\n\n[[hub]]\n")
            .unwrap();
        p.write_file("hub.md", b"# Hub\n").unwrap();
    });
    assert_eq!(edges_into(&a, "hub.md"), 1);
    let before = a.graph_generation();
    b.rename_folder("dir", "moved").unwrap();

    let w = window(&a);
    assert_eq!(w.status, ChangeFeedStatus::Exact);
    let seen = records(&a, &w);
    assert!(
        seen.iter().any(|r| r.entry == ChangeEntry::Directory),
        "{seen:#?}"
    );
    assert!(
        seen.iter().any(|r| r.entry == ChangeEntry::File),
        "{seen:#?}"
    );
    assert_ne!(a.graph_generation(), before);
    ack(&a, &w);

    // A window whose only record is a directory.
    assert_eq!(edges_into(&a, "hub.md"), 1);
    let before_folder = a.graph_generation();
    b.create_folder("empty").unwrap();
    let w = window(&a);
    let seen = records(&a, &w);
    assert_eq!(
        seen.iter().map(|r| r.entry).collect::<Vec<_>>(),
        [ChangeEntry::Directory],
        "premise: only a directory record"
    );
    assert_ne!(a.graph_generation(), before_folder);
}

/// WB-10a (#1519): a window of this session's own writes has no record —
/// its graph already learned them through `graph_apply` — so it is left
/// alone: the generation is the one the save set, and the metrics cache
/// survives.
#[test]
fn an_exact_window_of_own_writes_only_leaves_the_graph_alone() {
    let (_tmp, a, _b) = two_hosts(|p| {
        p.write_file("a.md", b"# A\n").unwrap();
        p.write_file("b.md", b"# B\n").unwrap();
    });
    assert_eq!(edges_into(&a, "b.md"), 0);
    let hash = hash_of(&a, "a.md");
    a.save_text("a.md", "# A\n\n[[b]]\n", Some(&hash)).unwrap();
    assert_eq!(
        edges_into(&a, "b.md"),
        1,
        "premise: the save reached the graph"
    );
    let _ = a.graph_metrics_snapshot().unwrap();
    let after_save = a.graph_generation();

    let w = window(&a);
    assert_eq!(w.status, ChangeFeedStatus::Exact);
    assert_eq!(w.record_count, 0, "premise: own writes only");
    assert_eq!(a.graph_generation(), after_save);
    assert!(a.graph_is_built());
    assert!(
        a.graph_metrics.lock().unwrap().is_some(),
        "the metrics cache survives"
    );
}

/// WB-10a (red team): this session's own scan writes reconcile rows, which
/// are records in its window, but the scan already taught its graph the
/// change (`graph_apply`): a lone session's external edit does not force a
/// rebuild.
#[test]
fn a_window_of_this_sessions_own_scan_leaves_the_graph_alone() {
    let (tmp, a, _b) = two_hosts(|p| {
        p.write_file("a.md", b"# A\n").unwrap();
        p.write_file("b.md", b"# B\n").unwrap();
    });
    assert_eq!(edges_into(&a, "b.md"), 0);
    edit_on_disk(&tmp, "a.md", b"# A\n\n[[b]]\n");
    a.rescan_with_progress(&CancelToken::new(), None).unwrap();
    assert_eq!(
        edges_into(&a, "b.md"),
        1,
        "premise: the scan taught the graph"
    );
    let _ = a.graph_metrics_snapshot().unwrap();
    let after_scan = a.graph_generation();

    let w = window(&a);
    assert_eq!(w.status, ChangeFeedStatus::Exact);
    assert!(
        w.record_count > 0,
        "premise: the own scan's row is a record"
    );
    assert_eq!(a.graph_generation(), after_scan);
    assert!(a.graph_is_built());
    assert!(
        a.graph_metrics.lock().unwrap().is_some(),
        "the metrics cache survives"
    );
}

/// WB-10a (red team): an oversized window of this session's own rows reads
/// nothing, but none of its rows is another writer's: the graph stays.
#[test]
fn an_oversized_window_of_this_sessions_own_rows_leaves_the_graph_alone() {
    let (_tmp, a, _b) = two_hosts(|p| {
        p.write_file("a.md", b"# A\n").unwrap();
    });
    let _ = edges_into(&a, "a.md");
    let before = a.graph_generation();
    indexed_rows(
        &a,
        "own-over-limit-",
        crate::CHANGE_FEED_WINDOW_ROW_LIMIT as usize + 1,
    );

    let too_large = window(&a);
    assert_eq!(too_large.status, ChangeFeedStatus::TooLarge);
    assert_eq!(a.graph_generation(), before);
}

/// WB-10a (#1519): a trimmed cursor's window is a gap; the host's full
/// re-sync cannot tell the graph what another session indexed, so the gap
/// drops it.
#[test]
fn a_trimmed_cursor_window_drops_the_graph() {
    let (_tmp, a, b) = two_hosts(|p| {
        p.write_file("a.md", b"# A\n").unwrap();
    });
    let _ = edges_into(&a, "a.md");
    let before = a.graph_generation();
    let cap = crate::CHANGE_FEED_MAX_ROWS as usize;
    indexed_rows(&b, "bulk-", cap + 5);
    let b_window = window(&b);
    ack(&b, &b_window);

    let gap = window(&a);
    assert_eq!(gap.status, ChangeFeedStatus::Gap(ChangeFeedGap::Trimmed));
    assert_ne!(a.graph_generation(), before);
}

/// WB-10a (#1519): a session with no cursor gets a gap, which drops the
/// graph like every gap.
#[test]
fn a_window_with_no_cursor_drops_the_graph() {
    let (_tmp, a) = make_vault(|p| {
        p.write_file("a.md", b"# A\n").unwrap();
    });
    a.scan_initial(&CancelToken::new()).unwrap();
    let _ = edges_into(&a, "a.md");
    let before = a.graph_generation();
    assert!(before > 0, "premise: A's graph is built");

    let gap = window(&a);
    assert_eq!(gap.status, ChangeFeedStatus::Gap(ChangeFeedGap::NoCursor));
    assert_ne!(a.graph_generation(), before);
}

/// WB-10a (#1519): an oversized window reads nothing, so it cannot say
/// whether another session changed a link: it drops the graph.
#[test]
fn an_oversized_window_drops_the_graph() {
    let (_tmp, a, b) = two_hosts(|p| {
        p.write_file("a.md", b"# A\n").unwrap();
    });
    let _ = edges_into(&a, "a.md");
    let before = a.graph_generation();
    indexed_rows(
        &b,
        "over-limit-",
        crate::CHANGE_FEED_WINDOW_ROW_LIMIT as usize + 1,
    );

    let too_large = window(&a);
    assert_eq!(too_large.status, ChangeFeedStatus::TooLarge);
    assert_ne!(a.graph_generation(), before);
}

/// WB-10a (#1519): a session that never built its graph reports the floor,
/// so the floor itself must move on each foreign change — two in a row,
/// with no query between, read two different generations.
#[test]
fn a_drop_with_no_built_index_still_moves_the_generation() {
    let (_tmp, a, b) = two_hosts(|_| {});
    assert_eq!(a.graph_generation(), 0);
    assert!(!a.graph_is_built());

    b.create_exclusive("one.md", "# One\n").unwrap();
    let first = window(&a);
    assert!(first.record_count > 0, "premise: B's create is a record");
    assert_eq!(a.graph_generation(), 1);
    ack(&a, &first);

    b.create_exclusive("two.md", "# Two\n").unwrap();
    let second = window(&a);
    assert!(
        second.record_count > 0,
        "premise: B's second create is a record"
    );
    assert_eq!(a.graph_generation(), 2);
    assert!(!a.graph_is_built(), "nothing queried the graph");
}
