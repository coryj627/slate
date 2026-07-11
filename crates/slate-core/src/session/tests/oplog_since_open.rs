// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! O-4 (#542) — changes-since-last-open: the `open_marks` baseline,
//! the four-state verdict matrix, and the pinned compute-then-mark
//! funnel order.

use super::common::*;
use super::*;

fn hash(content: &str) -> String {
    crate::vault::content_hash(content.as_bytes())
}

#[test]
fn changes_since_open_full_matrix() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"start\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let r0 = session.save_text("n.md", "start\n", None).unwrap();

    // 1. NoBaseline: no mark recorded yet.
    assert_eq!(
        session.changes_since_last_open("n.md").unwrap(),
        ChangesSinceOpen::NoBaseline
    );

    // 2. Unchanged: mark, no edits.
    session.mark_opened("n.md").unwrap();
    assert_eq!(
        session.changes_since_last_open("n.md").unwrap(),
        ChangesSinceOpen::Unchanged
    );

    // 3. Diff: mark, then edit.
    let r1 = session
        .save_text("n.md", "start\n\n# Goals\n", Some(&r0.new_content_hash))
        .unwrap();
    match session.changes_since_last_open("n.md").unwrap() {
        ChangesSinceOpen::Diff(diff) => {
            assert_eq!(diff.from_hash, hash("start\n"));
            assert_eq!(diff.to_hash, hash("start\n\n# Goals\n"));
            assert!(
                diff.operations
                    .iter()
                    .any(|op| op.semantic_description == "Added heading 'Goals' at line 3"),
                "got {:?}",
                diff.operations
            );
        }
        other => panic!("expected Diff, got {other:?}"),
    }

    // 4. BaselineCompacted: compact past the mark.
    let stem: String = {
        let conn = session.conn.lock().unwrap();
        conn.query_row(
            "SELECT oplog_name FROM files WHERE path = 'n.md'",
            [],
            |row| row.get(0),
        )
        .unwrap()
    };
    let limits = crate::oplog_compaction::CompactionLimits {
        threshold_bytes: 1,
        threshold_entries: 1,
        retention_days: u32::MAX,
    };
    let outcome = crate::oplog_compaction::compact_log(
        &session.config.cache_dir,
        &stem,
        "n.md",
        &limits,
        now_ms(),
    )
    .unwrap();
    assert!(matches!(
        outcome,
        crate::oplog_compaction::CompactionOutcome::Rewritten { .. }
    ));
    // The mark's version ("start\n") was folded away; only the tail
    // remains reconstructible.
    assert_eq!(
        session.changes_since_last_open("n.md").unwrap(),
        ChangesSinceOpen::BaselineCompacted
    );

    // Re-marking heals the baseline.
    session.mark_opened("n.md").unwrap();
    assert_eq!(
        session.changes_since_last_open("n.md").unwrap(),
        ChangesSinceOpen::Unchanged
    );
    let _ = r1;
}

#[test]
fn compute_then_mark_ordering_mark_first_would_lie() {
    // The pinned funnel order: compute FIRST, then mark. This test
    // proves the inverted order lies — after an edit, marking first
    // reports Unchanged and the change is never surfaced.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"v1\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let r0 = session.save_text("n.md", "v1\n", None).unwrap();
    session.mark_opened("n.md").unwrap();
    session
        .save_text("n.md", "v1\nv2\n", Some(&r0.new_content_hash))
        .unwrap();

    // WRONG order: mark first → the verdict lies (Unchanged).
    session.mark_opened("n.md").unwrap();
    assert_eq!(
        session.changes_since_last_open("n.md").unwrap(),
        ChangesSinceOpen::Unchanged,
        "this is exactly the lie the ordering contract forbids"
    );

    // RIGHT order on the next edit: compute first → Diff; then mark.
    let current = session.read_text("n.md").unwrap();
    session
        .save_text("n.md", &format!("{current}v3\n"), Some(&hash(&current)))
        .unwrap();
    assert!(matches!(
        session.changes_since_last_open("n.md").unwrap(),
        ChangesSinceOpen::Diff(_)
    ));
    session.mark_opened("n.md").unwrap();
    assert_eq!(
        session.changes_since_last_open("n.md").unwrap(),
        ChangesSinceOpen::Unchanged
    );
}

#[test]
fn marks_are_regenerable_and_die_with_their_file() {
    let tmp = tempfile::tempdir().unwrap();
    {
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();
        session.save_text("n.md", "content\n", None).unwrap();
        session.mark_opened("n.md").unwrap();
        assert_eq!(
            session.changes_since_last_open("n.md").unwrap(),
            ChangesSinceOpen::Unchanged
        );
    }
    // Cache rebuild: the mark is gone → honest NoBaseline (plan
    // decision #6 — lost marks degrade, never lie).
    std::fs::remove_file(tmp.path().join(".slate/cache.sqlite")).unwrap();
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        session.changes_since_last_open("n.md").unwrap(),
        ChangesSinceOpen::NoBaseline
    );

    // Delete → recreate: the CASCADE cleans the mark; the recycled id
    // starts markless (the O-1 hazard closed at the marks layer too).
    session.save_text("doomed.md", "x\n", None).unwrap();
    session.mark_opened("doomed.md").unwrap();
    session.delete_file("doomed.md").unwrap();
    session.save_text("doomed.md", "reborn\n", None).unwrap();
    assert_eq!(
        session.changes_since_last_open("doomed.md").unwrap(),
        ChangesSinceOpen::NoBaseline,
        "a recycled row must not inherit the dead file's mark"
    );
}

#[test]
fn diff_versions_resolves_both_sides_verified() {
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    let r0 = session.save_text("d.md", "alpha\n", None).unwrap();
    session
        .save_text("d.md", "alpha\n\nbeta\n", Some(&r0.new_content_hash))
        .unwrap();

    let diff = session
        .diff_versions("d.md", &hash("alpha\n"), &hash("alpha\n\nbeta\n"))
        .unwrap();
    assert_eq!(diff.operations.len(), 1);
    assert_eq!(
        diff.operations[0].semantic_description,
        "Added paragraph at line 3"
    );

    // Unknown hash on either side is typed, not a panic.
    assert!(
        session
            .diff_versions("d.md", &hash("alpha\n"), &hash("never"))
            .is_err()
    );
}
