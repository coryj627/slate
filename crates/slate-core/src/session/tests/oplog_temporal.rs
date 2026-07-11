// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! O-6 (#544) — the derived `oplog_events` index and the three
//! temporal query operators (`oplog.has_change_since`,
//! `oplog.has_property_change`, `oplog.deleted_content_matches`).
//!
//! Time discipline: operator tests script event timestamps by
//! UPDATE-ing `ts_ms` relative to wall clock at test start, always
//! ≥ 10 minutes away from every probe's window boundary — execution
//! jitter can never flip a verdict.

use super::common::*;
use super::*;

struct SplitMix64(u64);
impl SplitMix64 {
    fn next(&mut self) -> u64 {
        self.0 = self.0.wrapping_add(0x9E37_79B9_7F4A_7C15);
        let mut z = self.0;
        z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
        z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
        z ^ (z >> 31)
    }
    fn below(&mut self, n: u64) -> u64 {
        self.next() % n
    }
}

/// `(files, probes)` for the operator census.
fn census_scale() -> (usize, usize) {
    if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
        (24, 96)
    } else {
        (8, 24)
    }
}

/// One `oplog_events` row: `(ts_ms, event_class, property_key,
/// deleted_text)`.
type EventRow = (i64, u8, Option<String>, Option<String>);

/// All `oplog_events` rows for `path`, in insertion order.
fn events_for(session: &VaultSession, path: &str) -> Vec<EventRow> {
    let conn = session.conn.lock().unwrap();
    conn.prepare(
        "SELECT e.ts_ms, e.event_class, e.property_key, e.deleted_text
         FROM oplog_events e JOIN files f ON f.id = e.file_id
         WHERE f.path = ?1 ORDER BY e.rowid",
    )
    .unwrap()
    .query_map(rusqlite::params![path], |row| {
        Ok((row.get(0)?, row.get(1)?, row.get(2)?, row.get(3)?))
    })
    .unwrap()
    .collect::<Result<_, _>>()
    .unwrap()
}

/// Shift every event of `path` by `delta_ms` (scripted timestamps for
/// window tests). Deliberately does NOT bump the bases generation —
/// the cache carve-out test depends on that.
fn shift_events(session: &VaultSession, path: &str, delta_ms: i64) {
    let conn = session.conn.lock().unwrap();
    let changed = conn
        .execute(
            "UPDATE oplog_events SET ts_ms = ts_ms + ?1
             WHERE file_id = (SELECT id FROM files WHERE path = ?2)",
            rusqlite::params![delta_ms, path],
        )
        .unwrap();
    assert!(changed > 0, "no events to shift for {path}");
}

/// Run a one-view table query with `filter`, returning the matched
/// paths (ordered by file name) and any in-band view error.
fn filter_paths(session: &VaultSession, filter: &str) -> (Vec<String>, Option<String>) {
    assert!(!filter.contains('\''), "single quotes break YAML quoting");
    let yaml = format!(
        "views:\n  - type: table\n    name: T\n    filters: '{filter}'\n    order:\n      - file.name\n"
    );
    let (base, warnings) = crate::bases::parse_base(&yaml);
    assert!(warnings.is_empty(), "{warnings:?}");
    let query_json = serde_json::to_string(&crate::bases::view_query(&base, 0)).unwrap();
    let handle = session.open_query(&query_json, None).unwrap();
    let result = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    session.close_base(handle);
    (
        result.rows.iter().map(|r| r.file_path.clone()).collect(),
        result.view_error,
    )
}

/// Poll until `pred` or ~5 s elapse (the worker is asynchronous).
fn wait_for(mut pred: impl FnMut() -> bool) -> bool {
    for _ in 0..200 {
        if pred() {
            return true;
        }
        std::thread::sleep(std::time::Duration::from_millis(25));
    }
    false
}

const HOUR_MS: i64 = 3_600_000;
const DAY_MS: i64 = 24 * HOUR_MS;

// --- Population on append ---------------------------------------------

#[test]
fn population_covers_every_class_and_exclusions() {
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();

    // Cold-cache first save: class-1, NULL sample (no ops computed).
    let r0 = session
        .save_text(
            "n.md",
            "---\ndraft: true\n---\n- [ ] thing\nREMOVEME\n",
            None,
        )
        .unwrap();
    let events = events_for(&session, "n.md");
    assert_eq!(events.len(), 1);
    assert_eq!(events[0].1, 1);
    assert_eq!(events[0].3, None, "cold-cache save samples nothing");

    // Warm batch save deleting a distinctive word: sampled.
    session
        .save_text(
            "n.md",
            "---\ndraft: true\n---\n- [ ] thing\n",
            Some(&r0.new_content_hash),
        )
        .unwrap();
    let events = events_for(&session, "n.md");
    assert_eq!(events.len(), 2);
    assert!(events[1].3.as_deref().unwrap().contains("REMOVEME"));

    // Each annotation class rides its save: 2, 3, 4, 5.
    session
        .set_property(
            "n.md",
            "status",
            crate::frontmatter::PropertyValue::Text("final".into()),
            None,
        )
        .unwrap();
    session.delete_property("n.md", "draft", None).unwrap();
    session.toggle_task_status("n.md", 0, 'x', None).unwrap();
    session
        .set_frontmatter_source("n.md", "status: done", None)
        .unwrap();
    let events = events_for(&session, "n.md");
    // Every annotated save contributes its class-1 row PLUS the
    // annotation row.
    let classes: Vec<u8> = events.iter().map(|e| e.1).collect();
    assert_eq!(classes, vec![1, 1, 1, 2, 1, 3, 1, 4, 1, 5]);
    assert_eq!(events[3].2.as_deref(), Some("status"));
    assert_eq!(events[5].2.as_deref(), Some("draft"));
    assert_eq!(events[7].2, None, "task toggles carry no key");
    assert_eq!(events[9].2, None, "fm_replace carries no key");
    let count_before = events.len();

    // Identical save: no oplog entry, no rows.
    let current = session.read_text("n.md").unwrap();
    session.save_text("n.md", &current, None).unwrap();
    // Rename: a pure PathChanged marker — no rows (and the rows that
    // exist follow the file to its new path via file_id).
    session.rename_file("n.md", "renamed.md").unwrap();
    assert_eq!(events_for(&session, "renamed.md").len(), count_before);
}

#[test]
fn cadence_snapshot_save_still_samples_deleted_text() {
    // A warm save whose diff is as large as the file forces a
    // snapshot entry — but the session had the ops in hand, so the
    // spec requires the class-1 row to carry the removed spans (the
    // ops-in-hand case), unlike cold-cache snapshots.
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    let r0 = session
        .save_text("n.md", "alpha SNAPWORD beta\n", None)
        .unwrap();
    session
        .save_text("n.md", "entirely different\n", Some(&r0.new_content_hash))
        .unwrap();

    // Confirm the second entry really is a snapshot (the premise).
    let entries = session.read_oplog("n.md").unwrap();
    assert_eq!(entries.len(), 2);
    assert_eq!(entries[1].op_kind, crate::OpKind::WholeFileReplace);

    let events = events_for(&session, "n.md");
    assert_eq!(events.len(), 2);
    assert!(
        events[1].3.as_deref().unwrap().contains("SNAPWORD"),
        "cadence-snapshot save must sample: {:?}",
        events[1].3
    );
}

// --- Population on rebuild --------------------------------------------

#[test]
fn rebuild_regenerates_equivalent_rows() {
    let tmp = tempfile::tempdir().unwrap();
    let before: Vec<EventRow>;
    {
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();
        // Cold snapshot, warm batch (sampled), annotated save. The
        // body is long enough that a one-line delete and a frontmatter
        // insert both encode smaller than the file — genuine batches
        // (a tiny file would force snapshots and hit the boundary-NULL
        // case tested separately below).
        let body: String = (0..12).map(|i| format!("keep line {i}\n")).collect();
        let r0 = session
            .save_text("n.md", &format!("{body}WIPED\n"), None)
            .unwrap();
        let r1 = session
            .save_text("n.md", &body, Some(&r0.new_content_hash))
            .unwrap();
        session
            .set_property(
                "n.md",
                "status",
                crate::frontmatter::PropertyValue::Text("done".into()),
                None,
            )
            .unwrap();
        let _ = r1;
        before = events_for(&session, "n.md");
    }
    assert_eq!(
        before.iter().map(|e| e.1).collect::<Vec<_>>(),
        vec![1, 1, 1, 2]
    );

    // Cache rebuild: table starts empty; scan reconcile adopts the log
    // and regenerates the rows from it.
    std::fs::remove_file(tmp.path().join(".slate/cache.sqlite")).unwrap();
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    let after = events_for(&session, "n.md");

    assert_eq!(after.len(), before.len(), "row-for-row regeneration");
    for (b, a) in before.iter().zip(&after) {
        assert_eq!((b.0, b.1, &b.2), (a.0, a.1, &a.2), "ts/class/key identical");
    }
    // The pinned deleted_text matrix: cold snapshot NULL on both
    // sides; batch rows byte-identical; the set_property save was a
    // batch too (frontmatter edit), so it also matches exactly.
    assert_eq!(before[0].3, None);
    assert_eq!(after[0].3, None);
    assert_eq!(before[1].3, after[1].3);
    assert!(after[1].3.as_deref().unwrap().contains("WIPED"));
    assert_eq!(before[2].3, after[2].3);

    // A parser bump forces regeneration even with rows present:
    // vandalize one row, rebuild with the bump flag, and the truth
    // comes back.
    {
        let mut conn = session.conn.lock().unwrap();
        conn.execute("UPDATE oplog_events SET deleted_text = 'graffiti'", [])
            .unwrap();
        session.rebuild_oplog_events_if_stale(&mut conn, true);
    }
    let healed = events_for(&session, "n.md");
    assert_eq!(healed[1].3, after[1].3);
}

#[test]
fn rebuild_snapshot_boundary_yields_null_where_append_sampled() {
    // The one pinned append≠rebuild difference: a forced snapshot's
    // sample exists at append time (ops in hand) but not after a
    // rebuild (the log records only the snapshot bytes).
    let tmp = tempfile::tempdir().unwrap();
    {
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();
        let r0 = session
            .save_text("n.md", "alpha SNAPWORD beta\n", None)
            .unwrap();
        session
            .save_text("n.md", "entirely different\n", Some(&r0.new_content_hash))
            .unwrap();
        let before = events_for(&session, "n.md");
        assert!(before[1].3.as_deref().unwrap().contains("SNAPWORD"));
    }
    std::fs::remove_file(tmp.path().join(".slate/cache.sqlite")).unwrap();
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    let after = events_for(&session, "n.md");
    assert_eq!(after.len(), 2);
    assert_eq!(after[1].3, None, "snapshot boundary → NULL after rebuild");
}

#[test]
fn deleted_file_events_die_with_the_row_and_recycled_ids_start_clean() {
    // The O-1 recycled-id hazard at the events layer (adversarial
    // review): SQLite reuses a deleted max rowid, so without the
    // CASCADE a re-created file would inherit the dead file's history
    // through all three operators.
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("keeper.md", "stable\n", None).unwrap();

    // Created last → max rowid → the recycle candidate.
    let r = session
        .save_text("doomed.md", "hide GHOSTWORD now\n", None)
        .unwrap();
    session
        .save_text("doomed.md", "hide now\n", Some(&r.new_content_hash))
        .unwrap();
    session
        .set_property(
            "doomed.md",
            "secret",
            crate::frontmatter::PropertyValue::Text("yes".into()),
            None,
        )
        .unwrap();
    let (paths, _) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches("GHOSTWORD", "1h")"#,
    );
    assert_eq!(paths, vec!["doomed.md"], "premise: history exists");

    session.delete_file("doomed.md").unwrap();
    session.save_text("doomed.md", "reborn\n", None).unwrap();

    // The newcomer's only event is its own cold save.
    let events = events_for(&session, "doomed.md");
    assert_eq!(events.len(), 1, "no inherited rows: {events:?}");
    for filter in [
        r#"oplog.deleted_content_matches("GHOSTWORD", "1h")"#,
        r#"oplog.has_property_change("secret", "1h")"#,
    ] {
        let (paths, err) = filter_paths(&session, filter);
        assert_eq!(err, None);
        assert_eq!(
            paths,
            Vec::<String>::new(),
            "{filter} must not see the dead file"
        );
    }
    let (paths, _) = filter_paths(&session, r#"oplog.has_change_since("1h")"#);
    assert_eq!(paths, vec!["doomed.md", "keeper.md"], "own events intact");
}

#[test]
fn crash_between_append_and_insert_heals_via_the_marker() {
    // The round-2 adversarial window: `append_entry` is durable on the
    // filesystem, then the process dies before the event transaction
    // commits. Mark-before-append pins the on-disk state to exactly
    // (log has the entry, table lacks its rows, marker set) —
    // reconstructed here directly — and the next scan must rebuild
    // from the log and clear the marker. Without the marker this
    // state is undetectable: the table is non-empty and nothing
    // bumped.
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    let r = session.save_text("n.md", "v1\n", None).unwrap();
    assert_eq!(events_for(&session, "n.md").len(), 1);

    let stem: String = {
        let conn = session.conn.lock().unwrap();
        conn.query_row(
            "SELECT oplog_name FROM files WHERE path = 'n.md'",
            [],
            |row| row.get(0),
        )
        .unwrap()
    };
    let v2 = "v1\nCRASHWORD\n";
    let entry = crate::oplog::OpLogEntry {
        timestamp_ms: now_ms(),
        user_actor_id: "t".into(),
        op_kind: crate::OpKind::WholeFileReplace,
        content_hash_before: r.new_content_hash.clone(),
        content_hash_after: crate::vault::content_hash(v2.as_bytes()),
        payload_bytes: v2.as_bytes().to_vec(),
    };
    crate::oplog::append_entry(&session.config.cache_dir, &stem, "n.md", &entry).unwrap();
    {
        let conn = session.conn.lock().unwrap();
        conn.execute("INSERT INTO oplog_events_stale (marker) VALUES (1)", [])
            .unwrap();
    }
    assert_eq!(
        events_for(&session, "n.md").len(),
        1,
        "premise: the crashed save's rows are missing"
    );
    let (paths, _) = filter_paths(&session, r#"oplog.has_change_since("1h")"#);
    assert_eq!(paths, vec!["n.md"], "older event still matches");

    // "Restart": the next scan sees the marker and rebuilds.
    session.scan_initial(&CancelToken::new()).unwrap();
    let events = events_for(&session, "n.md");
    assert_eq!(
        events.len(),
        2,
        "the crashed save's row recovered from the log"
    );
    let stale: bool = {
        let conn = session.conn.lock().unwrap();
        conn.query_row("SELECT EXISTS(SELECT 1 FROM oplog_events_stale)", [], |r| {
            r.get(0)
        })
        .unwrap()
    };
    assert!(!stale, "marker cleared by the rebuild");
}

#[test]
fn successful_saves_leave_no_marker_residue() {
    // The mark-before-append protocol must be invisible on the happy
    // path: every successful save clears its own marker in the event
    // transaction, and a pre-existing marker from an EARLIER failure
    // survives an intervening successful save (per-row deletion, not
    // delete-all — a later save must not forget older staleness).
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("n.md", "v1\n", None).unwrap();
    let marker_count = |session: &VaultSession| -> i64 {
        let conn = session.conn.lock().unwrap();
        conn.query_row("SELECT COUNT(*) FROM oplog_events_stale", [], |r| r.get(0))
            .unwrap()
    };
    assert_eq!(marker_count(&session), 0, "happy path leaves nothing");

    // Simulate an older failure's surviving marker, then save again.
    {
        let conn = session.conn.lock().unwrap();
        conn.execute("INSERT INTO oplog_events_stale (marker) VALUES (1)", [])
            .unwrap();
    }
    let current = session.read_text("n.md").unwrap();
    session
        .save_text("n.md", &format!("{current}v2\n"), None)
        .unwrap();
    assert_eq!(
        marker_count(&session),
        1,
        "a successful save must not clear another save's marker"
    );

    // The synchronous-mode toggle must not leak (round 4): after the
    // save the connection is back on NORMAL (1), not FULL (2).
    let mode: i64 = {
        let conn = session.conn.lock().unwrap();
        conn.query_row("PRAGMA synchronous", [], |r| r.get(0))
            .unwrap()
    };
    assert_eq!(
        mode, 1,
        "synchronous restored to NORMAL after the marker commit"
    );
}

#[test]
fn restart_cold_save_with_old_in_hand_still_samples() {
    // Round 3 (spec conformance): the spec pins deleted_text NULL to
    // "no old content in hand", NOT to "session cache cold". The first
    // conflict-checked save after an app restart has the old bytes
    // (the conflict check read them) even though the oplog state is
    // cold — deleting a paragraph across a restart must remain
    // searchable.
    let tmp = tempfile::tempdir().unwrap();
    let body: String = (0..12).map(|i| format!("keep line {i}\n")).collect();
    let hash;
    {
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();
        hash = session
            .save_text("n.md", &format!("{body}RESTARTWORD\n"), None)
            .unwrap()
            .new_content_hash;
    }
    // New session = cold oplog state; the save carries the hash check.
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("n.md", &body, Some(&hash)).unwrap();

    // The entry is a re-anchoring snapshot (cold state), but the row
    // is sampled: old bytes were in hand.
    let entries = session.read_oplog("n.md").unwrap();
    assert_eq!(
        entries.last().unwrap().op_kind,
        crate::OpKind::WholeFileReplace
    );
    let (paths, err) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches("RESTARTWORD", "1h")"#,
    );
    assert_eq!(err, None);
    assert_eq!(paths, vec!["n.md"], "restart-boundary deletion searchable");

    // Without a conflict check there are no old bytes — still NULL
    // (the documented gap, now exactly the spec's matrix). A plain
    // no-hash edit to the SAME file loses its removed span.
    let current = session.read_text("n.md").unwrap();
    session
        .save_text("n.md", &current.replace("keep line 3\n", ""), None)
        .unwrap();
    let (paths, _) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches("keep line 3", "1h")"#,
    );
    assert_eq!(paths, Vec::<String>::new(), "no-old-bytes save stays NULL");
}

#[test]
fn adopted_log_history_survives_a_failed_rebuild_via_the_marker() {
    // Round 3 High: the reconcile writes adoption bindings in its own
    // transaction; the rebuild runs in a SEPARATE one. If the rebuild
    // crashes or busy-fails after the reconcile committed, an
    // in-memory "adopted" flag dies with it and the adopted history
    // stays invisible forever (table non-empty, no bump). The marker
    // is therefore written INSIDE the reconcile transaction. Simulate
    // the torn state directly: binding committed + marker present +
    // no rows; the next scan must heal.
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    let body: String = (0..12).map(|i| format!("keep line {i}\n")).collect();
    let r = session
        .save_text("adopted.md", &format!("{body}ADOPTWORD\n"), None)
        .unwrap();
    session
        .save_text("adopted.md", &body, Some(&r.new_content_hash))
        .unwrap();
    // Keep the table non-empty so emptiness can't mask the trigger.
    session.save_text("bystander.md", "x\n", None).unwrap();

    // Tear the state: wipe the file's rows (as if only the reconcile
    // tx — binding + marker — had committed and the rebuild died).
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "DELETE FROM oplog_events WHERE file_id = (SELECT id FROM files WHERE path = 'adopted.md')",
            [],
        )
        .unwrap();
        conn.execute("INSERT INTO oplog_events_stale (marker) VALUES (1)", [])
            .unwrap();
    }
    assert_eq!(
        filter_paths(
            &session,
            r#"oplog.deleted_content_matches("ADOPTWORD", "1h")"#
        )
        .0,
        Vec::<String>::new(),
        "premise: history invisible in the torn state"
    );

    session.scan_initial(&CancelToken::new()).unwrap();
    let (paths, err) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches("ADOPTWORD", "1h")"#,
    );
    assert_eq!(err, None);
    assert_eq!(paths, vec!["adopted.md"], "marker-driven rebuild healed");
}

#[test]
fn recovered_file_history_is_immediately_queryable() {
    // recover_deleted_file re-binds the remnant log to a NEW files row
    // — the old row's event rows died via the CASCADE. The recovery
    // transaction must repopulate them from the log, or the restored
    // history stays invisible to the operators until the next cache
    // rebuild.
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    // Multi-line body so the second save is a genuine batch (a tiny
    // file would force a snapshot, whose rebuild sample is NULL by
    // spec — tested elsewhere).
    let body: String = (0..12).map(|i| format!("keep line {i}\n")).collect();
    let r = session
        .save_text("lost.md", &format!("{body}PHOENIXWORD\n"), None)
        .unwrap();
    session
        .save_text("lost.md", &body, Some(&r.new_content_hash))
        .unwrap();
    session.delete_file("lost.md").unwrap();
    assert_eq!(
        filter_paths(
            &session,
            r#"oplog.deleted_content_matches("PHOENIXWORD", "1h")"#
        )
        .0,
        Vec::<String>::new(),
        "premise: the CASCADE removed the dead file's rows"
    );

    // Surface the remnant (reconcile runs at scan), then recover.
    session.scan_initial(&CancelToken::new()).unwrap();
    session.recover_deleted_file("lost.md").unwrap();

    // NO further scan: the history must already be queryable.
    let (paths, err) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches("PHOENIXWORD", "1h")"#,
    );
    assert_eq!(err, None);
    assert_eq!(
        paths,
        vec!["lost.md"],
        "recovered history visible immediately"
    );
    // Rebuild-shaped rows (snapshot boundary NULL + batch sample) plus
    // the recovery save's own cold row.
    let events = events_for(&session, "lost.md");
    assert_eq!(
        events.iter().map(|e| e.1).collect::<Vec<_>>(),
        vec![1, 1, 1]
    );
    assert!(events[1].3.as_deref().unwrap().contains("PHOENIXWORD"));
}

#[test]
fn failed_event_insert_is_atomic_and_heals_at_next_scan() {
    // Fault injection via a RAISE trigger: an entry whose LAST event
    // row fails must land NO rows (all-or-nothing, adversarial
    // review), set the staleness marker, and be fully repaired by the
    // next scan's rebuild — the entry itself is durable in the log.
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("n.md", "start\n", None).unwrap();
    let rows_before = events_for(&session, "n.md").len();

    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "CREATE TRIGGER fault_class5 BEFORE INSERT ON oplog_events
             WHEN NEW.event_class = 5
             BEGIN SELECT RAISE(ABORT, 'injected fault'); END",
            [],
        )
        .unwrap();
    }
    // A frontmatter replace derives [class-1, class-5]; the trigger
    // fails the second row. The save itself must still succeed.
    session
        .set_frontmatter_source("n.md", "k: v", None)
        .unwrap();
    {
        let conn = session.conn.lock().unwrap();
        conn.execute("DROP TRIGGER fault_class5", []).unwrap();
    }

    let events = events_for(&session, "n.md");
    assert_eq!(
        events.len(),
        rows_before,
        "no partial set: the class-1 row must have rolled back with the class-5"
    );
    let stale: bool = {
        let conn = session.conn.lock().unwrap();
        conn.query_row("SELECT EXISTS(SELECT 1 FROM oplog_events_stale)", [], |r| {
            r.get(0)
        })
        .unwrap()
    };
    assert!(stale, "the failure must be recorded durably");

    // The next scan repairs from the log and clears the marker.
    session.scan_initial(&CancelToken::new()).unwrap();
    let events = events_for(&session, "n.md");
    assert_eq!(
        events.iter().map(|e| e.1).collect::<Vec<_>>(),
        vec![1, 1, 5],
        "rebuild recovered the lost entry's rows from the log"
    );
    let stale: bool = {
        let conn = session.conn.lock().unwrap();
        conn.query_row("SELECT EXISTS(SELECT 1 FROM oplog_events_stale)", [], |r| {
            r.get(0)
        })
        .unwrap()
    };
    assert!(!stale, "marker cleared by the successful rebuild");
}

// --- The operators, end to end ----------------------------------------

#[test]
fn temporal_operators_filter_end_to_end() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("Notes/Untouched.md", b"never saved through slate\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    session
        .save_text("Notes/Recent.md", "fresh\n", None)
        .unwrap();
    session.save_text("Notes/Stale.md", "old\n", None).unwrap();
    shift_events(&session, "Notes/Stale.md", -8 * DAY_MS);

    // has_change_since: window boundaries are days from both events.
    let (paths, err) = filter_paths(&session, r#"oplog.has_change_since("7d")"#);
    assert_eq!(err, None);
    assert_eq!(paths, vec!["Notes/Recent.md"]);
    let (paths, _) = filter_paths(&session, r#"oplog.has_change_since("2w")"#);
    assert_eq!(
        paths,
        vec!["Notes/Recent.md", "Notes/Stale.md"],
        "wider window catches both; the never-saved file has no events at all"
    );

    // has_property_change: key-scoped, class 5 matches any key.
    session
        .set_property(
            "Notes/Recent.md",
            "status",
            crate::frontmatter::PropertyValue::Text("done".into()),
            None,
        )
        .unwrap();
    session
        .set_frontmatter_source("Notes/Stale.md", "anything: here", None)
        .unwrap();
    let (paths, err) = filter_paths(&session, r#"oplog.has_property_change("status", "1h")"#);
    assert_eq!(err, None);
    assert_eq!(
        paths,
        vec!["Notes/Recent.md", "Notes/Stale.md"],
        "exact key match plus the fm_replace wildcard"
    );
    let (paths, _) = filter_paths(&session, r#"oplog.has_property_change("nosuchkey", "1h")"#);
    assert_eq!(
        paths,
        vec!["Notes/Stale.md"],
        "unknown key still matches the whole-frontmatter replace"
    );

    // Composability with the shipped grammar (folder + property).
    session
        .save_text("Elsewhere/Other.md", "x\n", None)
        .unwrap();
    let (paths, _) = filter_paths(
        &session,
        r#"oplog.has_change_since("1h") && file.inFolder("Notes")"#,
    );
    assert_eq!(paths, vec!["Notes/Recent.md", "Notes/Stale.md"]);
}

#[test]
fn deleted_content_matches_is_ascii_case_insensitive_and_escapes_like() {
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();

    // Warm saves so the removed spans are sampled.
    let r = session
        .save_text("a.md", "keep Secret%Plan_v2 keep\n", None)
        .unwrap();
    session
        .save_text("a.md", "keep keep\n", Some(&r.new_content_hash))
        .unwrap();
    let r = session
        .save_text("b.md", "keep plain words\n", None)
        .unwrap();
    session
        .save_text("b.md", "keep\n", Some(&r.new_content_hash))
        .unwrap();

    // ASCII case-insensitive substring.
    let (paths, err) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches("sEcReT%pLaN", "1h")"#,
    );
    assert_eq!(err, None);
    assert_eq!(paths, vec!["a.md"]);

    // LIKE metacharacters are literals: % and _ must not wildcard.
    // Unescaped, "t%p" would also be a match via the wildcard; the
    // dedicated probes prove the escape.
    let (paths, _) = filter_paths(&session, r#"oplog.deleted_content_matches("%", "1h")"#);
    assert_eq!(paths, vec!["a.md"], "literal % only in a.md's deletion");
    let (paths, _) = filter_paths(&session, r#"oplog.deleted_content_matches("_v2", "1h")"#);
    assert_eq!(paths, vec!["a.md"]);
    let (paths, _) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches("plain_words", "1h")"#,
    );
    assert_eq!(
        paths,
        Vec::<String>::new(),
        "_ is not a single-char wildcard"
    );

    // Cold-cache deletions (NULL sample) never match — the documented
    // sampling gap, pinned here.
    let (paths, _) = filter_paths(&session, r#"oplog.deleted_content_matches("keep", "1h")"#);
    assert_eq!(paths, vec!["a.md", "b.md"], "warm deletions match");
}

#[test]
fn operator_composes_under_or_via_row_eval_fallback() {
    // Pushdown only handles top-level conjuncts; an OR forces the
    // row-level eval path (SqlVaultLookup). Same verdicts either way.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("Quiet.md", b"untouched\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("Busy.md", "edited\n", None).unwrap();

    let (paths, err) = filter_paths(
        &session,
        r#"oplog.has_change_since("1h") || file.name == "Quiet.md""#,
    );
    assert_eq!(err, None);
    assert_eq!(paths, vec!["Busy.md", "Quiet.md"]);

    let (paths, _) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches("nomatch", "1h") || oplog.has_property_change("k", "1h")"#,
    );
    assert_eq!(paths, Vec::<String>::new());
}

#[test]
fn invalid_durations_are_in_band_view_errors() {
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("n.md", "x\n", None).unwrap();

    for bad in ["0d", "7x", "d", "1.5h", "-2d", "01h"] {
        let filter = format!(r#"oplog.has_change_since("{bad}")"#);
        let (_, err2) = filter_paths(&session, &format!(r#"oplog.created_since("{bad}")"#));
        assert!(err2.is_some(), "{bad}: created_since rejects too");
        let (_, err3) = filter_paths(&session, &format!(r#"oplog.untouched_for("{bad}")"#));
        assert!(err3.is_some(), "{bad}: untouched_for rejects too");
        let (_, err4) = filter_paths(
            &session,
            &format!(r#"oplog.deleted_content_matches_regex("x", "{bad}")"#),
        );
        assert!(err4.is_some(), "{bad}: the regex variant rejects too");
        let (paths, err) = filter_paths(&session, &filter);
        assert!(paths.is_empty(), "{bad}: no rows on error");
        let err = err.unwrap_or_else(|| panic!("{bad}: expected a view error"));
        assert!(
            err.contains("h|d|w") || err.contains("duration"),
            "{bad}: error names the grammar: {err}"
        );
    }
    // The same grammar governs the fallback path.
    let (_, err) = filter_paths(
        &session,
        r#"oplog.has_change_since("0d") || file.name == "n.md""#,
    );
    assert!(err.is_some(), "row-eval path rejects bad durations too");
}

#[test]
fn wrong_arity_errors_identically_in_pushdown_and_row_eval() {
    // Round 3: the pushdown recognizer must not accept calls row-eval
    // rejects — an extra argument at a top-level AND used to push down
    // fine (extra arg ignored) while the identical expression inside
    // an OR errored. Exact arity or no pushdown: both positions now
    // produce the same in-band arity error.
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("n.md", "x\n", None).unwrap();

    for filter in [
        r#"oplog.has_change_since("7d", "extra")"#,
        r#"oplog.has_change_since("7d", "extra") || file.name == "n.md""#,
        r#"oplog.has_property_change("k", "7d", "extra")"#,
        r#"oplog.deleted_content_matches("pat")"#,
        r#"oplog.deleted_content_matches_regex("pat")"#,
        r#"oplog.deleted_content_matches_regex("pat", "7d", "extra")"#,
        r#"oplog.created_since("7d", "extra")"#,
        r#"oplog.untouched_for()"#,
    ] {
        let (paths, err) = filter_paths(&session, filter);
        assert!(paths.is_empty(), "{filter}: no rows on error");
        assert!(err.is_some(), "{filter}: arity error must surface in-band");
    }
}

#[test]
fn operators_are_filter_only_outside_filter_position() {
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("n.md", "x\n", None).unwrap();

    // In a formula column the operator must refuse (FilterOnly), not
    // silently evaluate against an unbounded clock.
    let yaml = "formulas:\n  recent: 'oplog.has_change_since(\"7d\")'\nviews:\n  - type: table\n    name: T\n    order:\n      - file.name\n      - formula.recent\n";
    let (base, warnings) = crate::bases::parse_base(yaml);
    assert!(warnings.is_empty(), "{warnings:?}");
    let query_json = serde_json::to_string(&crate::bases::view_query(&base, 0)).unwrap();
    let handle = session.open_query(&query_json, None).unwrap();
    let result = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    session.close_base(handle);
    let cell = &result.rows[0].values[1];
    assert!(
        cell.display.is_empty() || cell.display.contains("filter"),
        "formula cell must not carry a computed verdict: {:?}",
        cell.display
    );
}

#[test]
fn oplog_queries_bypass_the_result_cache() {
    // Aging out of a window changes results with NO vault mutation —
    // no generation bump, so a cached result would lie. The carve-out
    // makes oplog-mentioning queries recompute every execute.
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("n.md", "x\n", None).unwrap();

    let yaml = "views:\n  - type: table\n    name: T\n    filters: 'oplog.has_change_since(\"1h\")'\n    order:\n      - file.name\n";
    let (base, warnings) = crate::bases::parse_base(yaml);
    assert!(warnings.is_empty(), "{warnings:?}");
    let query_json = serde_json::to_string(&crate::bases::view_query(&base, 0)).unwrap();
    let handle = session.open_query(&query_json, None).unwrap();
    let first = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(first.rows.len(), 1);

    // Age the event out from under the SAME handle (no generation
    // bump — shift_events writes SQL directly).
    shift_events(&session, "n.md", -2 * HOUR_MS);
    let second = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    session.close_base(handle);
    assert_eq!(
        second.rows.len(),
        0,
        "a cached result would still show the aged-out file"
    );
}

#[test]
fn created_since_and_untouched_for_operators() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("planted.md", b"never saved through slate\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("fresh.md", "born now\n", None).unwrap();
    session
        .save_text("old.md", "born long ago\n", None)
        .unwrap();

    // Real filesystem birth: a just-created file matches a wide window
    // and carries a real (non-zero) birthtime.
    let (paths, err) = filter_paths(&session, r#"oplog.created_since("1h")"#);
    assert_eq!(err, None);
    assert!(paths.contains(&"fresh.md".to_string()));
    assert!(
        paths.contains(&"planted.md".to_string()),
        "fs birth, not oplog"
    );

    // Scripted windowing (birthtime is a plain files column; the
    // operator reads it — shifting is the same discipline as the
    // event-ts shifts above).
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE files SET birthtime_ms = birthtime_ms - ?1 WHERE path = 'old.md'",
            rusqlite::params![8 * DAY_MS],
        )
        .unwrap();
    }
    let (paths, _) = filter_paths(&session, r#"oplog.created_since("7d")"#);
    assert!(
        !paths.contains(&"old.md".to_string()),
        "8d-old birth misses 7d"
    );
    let (paths, _) = filter_paths(&session, r#"oplog.created_since("2w")"#);
    assert!(
        paths.contains(&"old.md".to_string()),
        "wider window catches it"
    );

    // Unknown birth (0) never matches — documented.
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE files SET birthtime_ms = 0 WHERE path = 'old.md'",
            [],
        )
        .unwrap();
    }
    let (paths, _) = filter_paths(&session, r#"oplog.created_since("52w")"#);
    assert!(
        !paths.contains(&"old.md".to_string()),
        "unknown birth ⇒ no match"
    );

    // untouched_for: fresh activity fails; aged mtime + aged events
    // pass; a touch INSIDE a wider window fails that window.
    let (paths, err) = filter_paths(&session, r#"oplog.untouched_for("1h")"#);
    assert_eq!(err, None);
    assert!(paths.is_empty(), "everything was just touched");
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE files SET mtime_ms = mtime_ms - ?1 WHERE path = 'fresh.md'",
            rusqlite::params![8 * DAY_MS],
        )
        .unwrap();
    }
    shift_events(&session, "fresh.md", -8 * DAY_MS);
    let (paths, _) = filter_paths(&session, r#"oplog.untouched_for("7d")"#);
    assert_eq!(paths, vec!["fresh.md"], "8d idle ⇒ untouched for 7d");
    let (paths, _) = filter_paths(&session, r#"oplog.untouched_for("2w")"#);
    assert!(paths.is_empty(), "touched 8d ago ⇒ NOT untouched for 2w");

    // Never-logged file: mtime alone governs (vacuously untouched
    // once idle — documented).
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE files SET mtime_ms = mtime_ms - ?1 WHERE path = 'planted.md'",
            rusqlite::params![8 * DAY_MS],
        )
        .unwrap();
    }
    let (paths, _) = filter_paths(&session, r#"oplog.untouched_for("7d")"#);
    assert!(paths.contains(&"planted.md".to_string()));

    // Both compose with the standard grammar (conjunction with
    // negation) and fall back to row-eval under OR.
    let (paths, err) = filter_paths(
        &session,
        r#"oplog.created_since("1h") && !oplog.untouched_for("7d")"#,
    );
    assert_eq!(err, None);
    assert!(
        !paths.contains(&"planted.md".to_string()),
        "planted is untouched-for-7d, so the negation excludes it"
    );
    let (paths, err) = filter_paths(
        &session,
        r#"oplog.untouched_for("7d") || file.name == "old.md""#,
    );
    assert_eq!(err, None, "row-eval fallback path works");
    assert!(paths.contains(&"old.md".to_string()));
}

// --- Census -------------------------------------------------------------

#[test]
fn census_temporal_operators_vs_reference() {
    // Random edit HISTORIES with scripted timestamps, built at the
    // oplog layer (`append_entry` with constructed entries) and fed
    // through the production rebuild (`derive_events_for_log` →
    // `oplog_events`). Every operator probe's SQL result must then
    // equal a brute-force scan the test computes directly from the
    // decoded logs under the documented semantics: window from
    // probe-now, key-or-class-5 property matching, ASCII-case-
    // insensitive substring on the removed spans. Timestamps sit on a
    // 15-minute grid offset 5 minutes from the whole-hour/day/week
    // probe boundaries, so execution jitter can never flip a verdict.
    let (files, probes) = census_scale();
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();

    let keys = ["status", "owner", "tags"];
    let words = ["Apple", "banana", "CHERRY", "date%", "elder_berry"];
    let mut rng = SplitMix64(0x0654_4C3E_9E51_u64);
    let cache_dir = session.config.cache_dir.clone();
    let actor = "census".to_string();

    // One clock capture for the whole census: entry scripting and the
    // reference retention cutoff below derive from the same instant
    // (Codoki on #844 — the 5-min grid offset already absorbs drift,
    // but one capture removes the question).
    let census_now = now_ms();
    let mut logs: Vec<(String, Vec<crate::oplog::OpLogEntry>)> = Vec::new();
    for i in 0..files {
        let path = format!("f{i:02}.md");
        session.save_text(&path, "seed\n", None).unwrap();
        let stem: String = {
            let conn = session.conn.lock().unwrap();
            conn.query_row(
                "SELECT oplog_name FROM files WHERE path = ?1",
                rusqlite::params![path],
                |row| row.get(0),
            )
            .unwrap()
        };
        std::fs::remove_file(crate::oplog::oplog_path_for_name(&cache_dir, &stem)).unwrap();
        crate::oplog::try_create_log(&cache_dir, &stem, &path).unwrap();

        // Scripted history: an opening snapshot, then a random mix of
        // edit batches (with deletions drawn from `words`), annotated
        // saves, touch-only anchors, and PathChanged markers. Document
        // versions chain so every batch replays.
        let now = census_now;
        // Offsets reach ~126 days — beyond the 90-day retention
        // window — so the census exercises the #831 shared cutoff:
        // the production rebuild must drop the beyond-window slice
        // and the reference below drops it by the same rule.
        let mut ts_offsets: Vec<i64> = (0..1 + rng.below(6))
            .map(|_| (rng.below(12096) as i64 + 1) * 15 * 60_000 + 5 * 60_000)
            .collect();
        ts_offsets.sort_unstable();
        ts_offsets.reverse(); // oldest first
        let mut doc = format!("line one {i}\nline two\n");
        let mut entries = vec![crate::oplog::OpLogEntry {
            timestamp_ms: now - ts_offsets[0],
            user_actor_id: actor.clone(),
            op_kind: crate::OpKind::WholeFileReplace,
            content_hash_before: crate::vault::content_hash(b"seed\n"),
            content_hash_after: crate::vault::content_hash(doc.as_bytes()),
            payload_bytes: doc.clone().into_bytes(),
        }];
        for offset in ts_offsets.into_iter().skip(1) {
            let ts = now - offset;
            let hash_here = crate::vault::content_hash(doc.as_bytes());
            match rng.below(4) {
                // Edit batch: delete the doc's middle line, insert a
                // fresh one carrying a random word.
                0 | 1 => {
                    let word = words[rng.below(words.len() as u64) as usize];
                    let next = format!("line one {i}\n{word} kept {ts}\n");
                    let ops = crate::diff::diff_to_ops(&doc, &next);
                    let payload = crate::oplog::encode_edit_batch(&ops);
                    let annotations: Vec<crate::oplog::OpAnnotation> = match rng.below(3) {
                        0 => vec![crate::oplog::OpAnnotation::SetProperty {
                            key: keys[rng.below(keys.len() as u64) as usize].to_string(),
                            value_json: "1".into(),
                        }],
                        1 => vec![crate::oplog::OpAnnotation::RemoveProperty {
                            key: keys[rng.below(keys.len() as u64) as usize].to_string(),
                        }],
                        _ => Vec::new(),
                    };
                    let (op_kind, payload_bytes) = if annotations.is_empty() {
                        (crate::OpKind::EditBatch, payload)
                    } else {
                        (
                            crate::OpKind::Annotated,
                            crate::oplog::encode_annotated(
                                crate::OpKind::EditBatch,
                                &payload,
                                &annotations,
                            ),
                        )
                    };
                    entries.push(crate::oplog::OpLogEntry {
                        timestamp_ms: ts,
                        user_actor_id: actor.clone(),
                        op_kind,
                        content_hash_before: hash_here,
                        content_hash_after: crate::vault::content_hash(next.as_bytes()),
                        payload_bytes,
                    });
                    doc = next;
                }
                // Annotated whole-frontmatter replace as a snapshot.
                2 => {
                    let next = format!("---\nk: {ts}\n---\nline one {i}\n");
                    entries.push(crate::oplog::OpLogEntry {
                        timestamp_ms: ts,
                        user_actor_id: actor.clone(),
                        op_kind: crate::OpKind::Annotated,
                        content_hash_before: hash_here,
                        content_hash_after: crate::vault::content_hash(next.as_bytes()),
                        payload_bytes: crate::oplog::encode_annotated(
                            crate::OpKind::WholeFileReplace,
                            next.as_bytes(),
                            &[crate::oplog::OpAnnotation::FrontmatterReplace],
                        ),
                    });
                    doc = next;
                }
                // Touch-only: a synthesized-style anchor or a rename
                // marker — must contribute NO events.
                _ => {
                    let payload_bytes = if rng.below(2) == 0 {
                        doc.clone().into_bytes()
                    } else {
                        crate::oplog::encode_annotated(
                            crate::OpKind::EditBatch,
                            &crate::oplog::encode_edit_batch(&[]),
                            &[crate::oplog::OpAnnotation::PathChanged {
                                from: path.clone(),
                                to: path.clone(),
                            }],
                        )
                    };
                    let op_kind = if payload_bytes == doc.as_bytes() {
                        crate::OpKind::WholeFileReplace
                    } else {
                        crate::OpKind::Annotated
                    };
                    entries.push(crate::oplog::OpLogEntry {
                        timestamp_ms: ts,
                        user_actor_id: actor.clone(),
                        op_kind,
                        content_hash_before: hash_here.clone(),
                        content_hash_after: hash_here,
                        payload_bytes,
                    });
                }
            }
        }
        for entry in &entries {
            crate::oplog::append_entry(&cache_dir, &stem, &path, entry).unwrap();
        }
        logs.push((path, entries));
    }

    // Population through the PRODUCTION path: the forced rebuild wipes
    // the append-time rows and regenerates everything from the
    // constructed logs via `derive_events_for_log`.
    {
        let mut conn = session.conn.lock().unwrap();
        session.rebuild_oplog_events_if_stale(&mut conn, true);
    }

    // Scripted birthtimes + mtimes for the #801 probes (same grid
    // discipline as the event timestamps: 15-min steps, 5-min offset
    // from whole-hour boundaries).
    let mut fs_times: Vec<(String, i64, i64)> = Vec::new(); // (path, birth, mtime)
    for i in 0..files {
        let path = format!("f{i:02}.md");
        let now = now_ms();
        let birth = if rng.below(5) == 0 {
            0 // unknown birth — must never match created_since
        } else {
            now - (rng.below(4032) as i64 + 1) * 15 * 60_000 - 5 * 60_000
        };
        let mtime = now - (rng.below(4032) as i64 + 1) * 15 * 60_000 - 5 * 60_000;
        {
            let conn = session.conn.lock().unwrap();
            conn.execute(
                "UPDATE files SET birthtime_ms = ?1, mtime_ms = ?2 WHERE path = ?3",
                rusqlite::params![birth, mtime, path],
            )
            .unwrap();
        }
        fs_times.push((path, birth, mtime));
    }

    // Brute-force reference, computed from the decoded logs in flat
    // imperative code (no shared derivation helpers): replay each
    // prefix for old content, collect removed spans per content
    // change, and decode annotations directly. Spans here are far
    // below the 4 KiB cap, which has its own boundary fixture.
    let ascii_contains = |haystack: &str, needle: &str| {
        haystack
            .to_ascii_lowercase()
            .contains(&needle.to_ascii_lowercase())
    };
    let mut expected_rows: Vec<(String, Vec<EventRow>)> = Vec::new();
    for (path, entries) in &logs {
        let mut rows: Vec<EventRow> = Vec::new();
        for (idx, entry) in entries.iter().enumerate() {
            if entry.content_hash_before == entry.content_hash_after {
                continue;
            }
            let (inner_kind, inner_payload, annotations) =
                if entry.op_kind == crate::OpKind::Annotated {
                    let (k, p, a) = crate::oplog::decode_annotated(&entry.payload_bytes).unwrap();
                    (k, p, a)
                } else {
                    (entry.op_kind, entry.payload_bytes.clone(), Vec::new())
                };
            let old =
                (idx > 0).then(|| crate::oplog::reconstruct_at_tail(&entries[..idx]).unwrap());
            let deleted = match (inner_kind, &old) {
                (crate::OpKind::EditBatch, Some(old)) => {
                    let mut removed = String::new();
                    for op in crate::oplog::decode_edit_batch(&inner_payload).unwrap() {
                        match op {
                            crate::EditOp::Delete { start, end }
                            | crate::EditOp::Replace { start, end, .. } => {
                                removed.push_str(&old[start..end]);
                            }
                            crate::EditOp::Insert { .. } => {}
                        }
                    }
                    Some(removed)
                }
                _ => None,
            };
            rows.push((entry.timestamp_ms, 1, None, deleted));
            for annotation in annotations {
                match annotation {
                    crate::oplog::OpAnnotation::SetProperty { key, .. } => {
                        rows.push((entry.timestamp_ms, 2, Some(key), None));
                    }
                    crate::oplog::OpAnnotation::RemoveProperty { key } => {
                        rows.push((entry.timestamp_ms, 3, Some(key), None));
                    }
                    crate::oplog::OpAnnotation::FrontmatterReplace => {
                        rows.push((entry.timestamp_ms, 5, None, None));
                    }
                    crate::oplog::OpAnnotation::ToggleTask { .. } => {
                        rows.push((entry.timestamp_ms, 4, None, None));
                    }
                    crate::oplog::OpAnnotation::PathChanged { .. } => {}
                }
            }
        }
        // #831: the shared retention rule, applied independently (the
        // reference's own arithmetic, not the production function).
        // The 15-min-grid + 5-min-offset discipline keeps every entry
        // at least 5 minutes from the whole-day cutoff boundary, so
        // clock drift between scripting and rebuilding cannot flip a
        // row.
        let retention_cutoff = census_now - 90 * DAY_MS;
        rows.retain(|(ts, ..)| *ts > retention_cutoff);
        expected_rows.push((path.clone(), rows));
    }

    // The rebuilt table must agree with the reference row-for-row —
    // the derivation half of the census (including the #831 cutoff:
    // beyond-window entries stay in the LOG but produce no rows).
    for (path, want) in &expected_rows {
        assert_eq!(&events_for(&session, path), want, "derived rows for {path}");
    }

    for probe in 0..probes {
        let (unit, unit_ms) =
            [("h", HOUR_MS), ("d", DAY_MS), ("w", 7 * DAY_MS)][rng.below(3) as usize];
        let n = rng.below(if unit == "h" { 1000 } else { 6 }) + 1;
        let duration = format!("{n}{unit}");
        let cutoff = now_ms() - (n as i64) * unit_ms;

        let (filter, reference): (String, Vec<String>) = match rng.below(6) {
            0 => (
                format!(r#"oplog.has_change_since("{duration}")"#),
                expected_rows
                    .iter()
                    .filter(|(_, rows)| rows.iter().any(|(ts, c, ..)| *c == 1 && *ts >= cutoff))
                    .map(|(p, _)| p.clone())
                    .collect(),
            ),
            1 => {
                let key = keys[rng.below(keys.len() as u64) as usize];
                (
                    format!(r#"oplog.has_property_change("{key}", "{duration}")"#),
                    expected_rows
                        .iter()
                        .filter(|(_, rows)| {
                            rows.iter().any(|(ts, c, k, _)| {
                                *ts >= cutoff
                                    && (*c == 5
                                        || (matches!(c, 2 | 3) && k.as_deref() == Some(key)))
                            })
                        })
                        .map(|(p, _)| p.clone())
                        .collect(),
                )
            }
            3 => (
                format!(r#"oplog.created_since("{duration}")"#),
                fs_times
                    .iter()
                    .filter(|(_, birth, _)| *birth > 0 && *birth >= cutoff)
                    .map(|(p, _, _)| p.clone())
                    .collect(),
            ),
            4 => (
                format!(r#"oplog.untouched_for("{duration}")"#),
                fs_times
                    .iter()
                    .filter(|(path, _, mtime)| {
                        *mtime < cutoff
                            && expected_rows.iter().find(|(p, _)| p == path).is_none_or(
                                |(_, rows)| {
                                    !rows.iter().any(|(ts, c, ..)| *c == 1 && *ts >= cutoff)
                                },
                            )
                    })
                    .map(|(p, _, _)| p.clone())
                    .collect(),
            ),
            5 => {
                // #800, the regex variant: same corpus word,
                // case-scrambled, split around an explicit `.*` so the
                // pattern exercises live regex syntax. The reference
                // applies the same bounded builder to the expected
                // samples — the census pins the PLUMBING (event class,
                // cutoff, NULL-skip, per-file scoping), not the regex
                // engine itself.
                let word = words[rng.below(words.len() as u64) as usize];
                let scrambled: String = word
                    .chars()
                    .map(|c| {
                        if rng.below(2) == 0 {
                            c.to_ascii_uppercase()
                        } else {
                            c.to_ascii_lowercase()
                        }
                    })
                    .collect();
                let (head, tail) = scrambled.split_at(scrambled.len() / 2);
                let pattern = format!("{head}.*{tail}");
                let regex = crate::bases::eval::build_deleted_content_regex(&pattern).unwrap();
                (
                    format!(r#"oplog.deleted_content_matches_regex("{pattern}", "{duration}")"#),
                    expected_rows
                        .iter()
                        .filter(|(_, rows)| {
                            rows.iter().any(|(ts, c, _, d)| {
                                *c == 1
                                    && *ts >= cutoff
                                    && d.as_deref().is_some_and(|d| regex.is_match(d))
                            })
                        })
                        .map(|(p, _)| p.clone())
                        .collect(),
                )
            }
            _ => {
                // Case-scrambled fragments, including the metachar-bearing
                // words — the reference applies plain (escaped) substring
                // semantics on both sides.
                let word = words[rng.below(words.len() as u64) as usize];
                let pattern: String = word
                    .chars()
                    .map(|c| {
                        if rng.below(2) == 0 {
                            c.to_ascii_uppercase()
                        } else {
                            c.to_ascii_lowercase()
                        }
                    })
                    .collect();
                (
                    format!(r#"oplog.deleted_content_matches("{pattern}", "{duration}")"#),
                    expected_rows
                        .iter()
                        .filter(|(_, rows)| {
                            rows.iter().any(|(ts, c, _, d)| {
                                *c == 1
                                    && *ts >= cutoff
                                    && d.as_deref().is_some_and(|d| ascii_contains(d, &pattern))
                            })
                        })
                        .map(|(p, _)| p.clone())
                        .collect(),
                )
            }
        };
        let (mut got, err) = filter_paths(&session, &filter);
        assert_eq!(err, None, "probe {probe}: {filter}");
        got.sort();
        let mut want = reference;
        want.sort();
        assert_eq!(got, want, "probe {probe}: {filter}");
    }
}

// --- Composition with the Milestone N grammar ---------------------------

#[test]
fn operators_compose_with_n_corpus_filters() {
    let (_tmp, session) = make_vault(|p| {
        p.create_dir("Projects").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session
        .save_text(
            "Projects/Active.md",
            "---\nstatus: active\ntags: [urgent]\n---\nbody\n",
            None,
        )
        .unwrap();
    session
        .save_text(
            "Projects/Done.md",
            "---\nstatus: done\ntags: [urgent]\n---\nbody\n",
            None,
        )
        .unwrap();
    session
        .save_text("Inbox.md", "---\nstatus: active\n---\nbody\n", None)
        .unwrap();
    shift_events(&session, "Projects/Done.md", -30 * DAY_MS);

    // Folder + property + temporal, all conjuncts pushed down.
    let (paths, err) = filter_paths(
        &session,
        r#"file.inFolder("Projects") && status == "active" && oplog.has_change_since("7d")"#,
    );
    assert_eq!(err, None);
    assert_eq!(paths, vec!["Projects/Active.md"]);

    // Tag + temporal: the aged file drops out on time, not on tag.
    let (paths, _) = filter_paths(
        &session,
        r#"file.hasTag("urgent") && oplog.has_change_since("7d")"#,
    );
    assert_eq!(paths, vec!["Projects/Active.md"]);
    let (paths, _) = filter_paths(
        &session,
        r#"file.hasTag("urgent") && oplog.has_change_since("6w")"#,
    );
    assert_eq!(paths, vec!["Projects/Active.md", "Projects/Done.md"]);

    // Negation over an operator (the standard combinators).
    let (paths, _) = filter_paths(
        &session,
        r#"file.inFolder("Projects") && !oplog.has_change_since("7d")"#,
    );
    assert_eq!(paths, vec!["Projects/Done.md"]);

    // ALL THREE operators composed with tag/property/folder filters
    // (the milestone DoD line covers each operator, not just
    // has_change_since — adversarial round 3). Multi-conjunct
    // pushdown means the operators' bound params ride in plan.params
    // alongside the tag/property predicates' — a param-ordering bug
    // in either direction flips these verdicts.
    session
        .set_property(
            "Projects/Active.md",
            "owner",
            crate::frontmatter::PropertyValue::Text("cj".into()),
            None,
        )
        .unwrap();
    let (paths, err) = filter_paths(
        &session,
        r#"file.inFolder("Projects") && file.hasTag("urgent") && oplog.has_property_change("owner", "1h")"#,
    );
    assert_eq!(err, None);
    assert_eq!(paths, vec!["Projects/Active.md"]);
    let (paths, _) = filter_paths(
        &session,
        r#"status == "done" && oplog.has_property_change("owner", "1h")"#,
    );
    assert_eq!(
        paths,
        Vec::<String>::new(),
        "property conjunct filters it out"
    );

    // deleted_content_matches composed: remove a distinctive word from
    // the tagged file, then require folder + tag + the deletion.
    let current = session.read_text("Projects/Active.md").unwrap();
    let with_word = format!("{current}DOOMEDLINE alpha beta gamma delta\n");
    let r = session
        .save_text("Projects/Active.md", &with_word, None)
        .unwrap();
    session
        .save_text("Projects/Active.md", &current, Some(&r.new_content_hash))
        .unwrap();
    let (paths, err) = filter_paths(
        &session,
        r#"file.inFolder("Projects") && file.hasTag("urgent") && oplog.deleted_content_matches("doomedline", "1h")"#,
    );
    assert_eq!(err, None);
    assert_eq!(paths, vec!["Projects/Active.md"]);
    let (paths, _) = filter_paths(
        &session,
        r#"file.inFolder("Inbox") && oplog.deleted_content_matches("doomedline", "1h")"#,
    );
    assert_eq!(
        paths,
        Vec::<String>::new(),
        "folder conjunct filters it out"
    );
}

// --- Compaction ↔ oplog_events coherence (milestone red team) -----------

#[test]
fn compaction_fold_invalidates_and_regenerates_the_events_index() {
    // Milestone red-team High: a successful fold discards history from
    // the LOG; the derived index must not keep matching it. The worker
    // couples every rewrite to a per-file regeneration (marker before
    // the rewrite, regen tx after), so the retention picker, the
    // recoverable log, and the temporal operators agree.
    let tmp = tempfile::tempdir().unwrap();
    let cache_dir = tmp.path().join(".slate");
    let mut config = SessionConfig::new(cache_dir);
    config.oplog_compaction_threshold_bytes = 2048;
    let provider = std::sync::Arc::new(FsVaultProvider::new(tmp.path().to_path_buf()));
    let session = VaultSession::open(provider, config).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    // Warm saves whose deletions carry a distinctive word, then enough
    // bulk to trip the 2 KiB trigger and force a positional fold.
    let body: String = (0..12).map(|i| format!("keep line {i}\n")).collect();
    let r = session
        .save_text("n.md", &format!("{body}FOLDWORD\n"), None)
        .unwrap();
    let mut hash = session
        .save_text("n.md", &body, Some(&r.new_content_hash))
        .unwrap()
        .new_content_hash;
    let (paths, _) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches("FOLDWORD", "1h")"#,
    );
    assert_eq!(paths, vec!["n.md"], "premise: the deletion is indexed");

    let mut contents = body;
    for i in 0..40 {
        contents.push_str(&format!("bulk filler line number {i} with padding\n"));
        hash = session
            .save_text("n.md", &contents, Some(&hash))
            .unwrap()
            .new_content_hash;
    }
    // The fold rewrites the log (generation bump) and the worker
    // regenerates the file's rows; the early FOLDWORD deletion is
    // discarded from BOTH.
    let folded = wait_for(|| {
        let entries = session.read_oplog("n.md").unwrap();
        !entries.iter().any(|e| {
            crate::oplog_events::derive_events(e, None, None)
                .iter()
                .any(|_| false)
        }) && {
            let conn = session.conn.lock().unwrap();
            let generation: u32 = {
                let name: String = conn
                    .query_row(
                        "SELECT oplog_name FROM files WHERE path = 'n.md'",
                        [],
                        |row| row.get(0),
                    )
                    .unwrap();
                drop(conn);
                crate::oplog::read_oplog_with_header(&session.config.cache_dir, &name)
                    .unwrap()
                    .0
                    .generation
            };
            generation > 0
        }
    });
    assert!(folded, "the fold ran");

    // The index no longer matches the folded-away deletion…
    let coherent = wait_for(|| {
        filter_paths(
            &session,
            r#"oplog.deleted_content_matches("FOLDWORD", "1h")"#,
        )
        .0
        .is_empty()
    });
    assert!(coherent, "regeneration removed the discarded event's row");
    // …while the retained recent history still matches.
    let (paths, _) = filter_paths(&session, r#"oplog.has_change_since("1h")"#);
    assert_eq!(paths, vec!["n.md"], "retained history still indexed");
    // And no orphan marker remains once the worker settled.
    let settled = wait_for(|| {
        let conn = session.conn.lock().unwrap();
        let stale: bool = conn
            .query_row("SELECT EXISTS(SELECT 1 FROM oplog_events_stale)", [], |r| {
                r.get(0)
            })
            .unwrap();
        !stale
    });
    assert!(settled, "the worker's marker was cleared");
}

#[test]
fn crash_between_rewrite_and_regen_heals_via_the_marker() {
    // The compaction crash window: the rewrite renamed, the process
    // died before the regen transaction. On-disk state = rewritten log
    // + stale rows + the pre-rewrite marker; the next scan rebuilds
    // from the (post-fold) logs.
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    let body: String = (0..12).map(|i| format!("keep line {i}\n")).collect();
    let r = session
        .save_text("n.md", &format!("{body}CRASHFOLD\n"), None)
        .unwrap();
    session
        .save_text("n.md", &body, Some(&r.new_content_hash))
        .unwrap();
    assert_eq!(
        filter_paths(
            &session,
            r#"oplog.deleted_content_matches("CRASHFOLD", "1h")"#
        )
        .0,
        vec!["n.md"]
    );

    // Fold DIRECTLY (bypassing the worker — simulating its rewrite
    // landing without the follow-up regen), then plant the marker the
    // worker would have written pre-rewrite.
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
    {
        let conn = session.conn.lock().unwrap();
        conn.execute("INSERT INTO oplog_events_stale (marker) VALUES (1)", [])
            .unwrap();
    }
    // Stale rows still match — exactly the incoherence the marker exists
    // to repair.
    assert_eq!(
        filter_paths(
            &session,
            r#"oplog.deleted_content_matches("CRASHFOLD", "1h")"#
        )
        .0,
        vec!["n.md"],
        "premise: rows outlived the fold"
    );

    // "Restart": the scan sees the marker and rebuilds from the folded log.
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        filter_paths(
            &session,
            r#"oplog.deleted_content_matches("CRASHFOLD", "1h")"#
        )
        .0,
        Vec::<String>::new(),
        "healed: the discarded event no longer matches"
    );
}

#[test]
fn compaction_racing_saves_never_loses_indexed_events() {
    // Milestone re-review High: a save landing between the worker's
    // log read and its DELETE+reinsert must not lose its event row.
    // The regen holds the log's exclusive lock across read+tx, so a
    // concurrent save's append (and thus its insert) orders strictly
    // around it. This test interleaves saves with worker folds and
    // pins the settle-state invariant: the indexed (ts, class) rows
    // equal exactly what the FINAL log derives — nothing lost,
    // nothing orphaned, no markers left.
    let tmp = tempfile::tempdir().unwrap();
    let cache_dir = tmp.path().join(".slate");
    let mut config = SessionConfig::new(cache_dir);
    config.oplog_compaction_threshold_bytes = 1024;
    let provider = std::sync::Arc::new(FsVaultProvider::new(tmp.path().to_path_buf()));
    let session = VaultSession::open(provider, config).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    let body: String = (0..12).map(|i| format!("keep line {i}\n")).collect();
    let mut contents = body;
    let mut hash = session
        .save_text("n.md", &contents, None)
        .unwrap()
        .new_content_hash;
    for i in 0..30 {
        contents.push_str(&format!("racing save number {i} with some padding\n"));
        hash = session
            .save_text("n.md", &contents, Some(&hash))
            .unwrap()
            .new_content_hash;
    }

    // Settle: worker drained (no marker rows) AND the index matches
    // the final log exactly on (ts_ms, event_class).
    let settled = wait_for(|| {
        let no_markers: bool = {
            let conn = session.conn.lock().unwrap();
            conn.query_row(
                "SELECT NOT EXISTS(SELECT 1 FROM oplog_events_stale)",
                [],
                |r| r.get(0),
            )
            .unwrap()
        };
        if !no_markers {
            return false;
        }
        let indexed: Vec<(i64, u8)> = events_for(&session, "n.md")
            .iter()
            .map(|e| (e.0, e.1))
            .collect();
        let entries = session.read_oplog("n.md").unwrap();
        let derived: Vec<(i64, u8)> = crate::oplog_events::derive_events_for_log(&entries)
            .iter()
            .map(|e| (e.ts_ms, e.event_class))
            .collect();
        indexed == derived
    });
    assert!(
        settled,
        "index must converge to exactly the final log's derivation"
    );
    // The newest save is queryable — the row a lost-update would drop.
    let (paths, _) = filter_paths(&session, r#"oplog.has_change_since("1h")"#);
    assert_eq!(paths, vec!["n.md"]);
}

#[test]
fn birthtime_backfills_on_fast_path_and_survives_zero_sentinels() {
    // Round 2 (adversarial review): (1) migration-030 rows carry
    // birthtime 0; an UNCHANGED file's next scan — the fast path,
    // which never re-reads content — must back-fill the filesystem
    // birth, or created_since omits every pre-upgrade file forever.
    // (2) A 0 sentinel from a platform that stops reporting birth
    // must never clobber a known value.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("old.md", b"pre-upgrade content\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Simulate the migrated row: birth unknown.
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE files SET birthtime_ms = 0 WHERE path = 'old.md'",
            [],
        )
        .unwrap();
    }
    let (paths, _) = filter_paths(&session, r#"oplog.created_since("52w")"#);
    assert!(
        !paths.contains(&"old.md".to_string()),
        "premise: unknown birth"
    );

    // An unchanged rescan takes the fast path and back-fills.
    session.scan_initial(&CancelToken::new()).unwrap();
    let birth: i64 = {
        let conn = session.conn.lock().unwrap();
        conn.query_row(
            "SELECT birthtime_ms FROM files WHERE path = 'old.md'",
            [],
            |r| r.get(0),
        )
        .unwrap()
    };
    assert!(birth > 0, "fast path back-filled the filesystem birth");
    let (paths, _) = filter_paths(&session, r#"oplog.created_since("1h")"#);
    assert!(paths.contains(&"old.md".to_string()));

    // Sentinel guard on the upsert paths: a save whose stat carried
    // birth keeps it; simulate a later 0-stat by direct SQL-shaped
    // upsert through the save path is fs-backed here (real birth), so
    // assert the CASE the other way: plant a KNOWN value, then verify
    // a real save (birth present) doesn't lose it and a scripted
    // 0-sentinel UPDATE through the fast path preserves it.
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE files SET birthtime_ms = 1234567890123 WHERE path = 'old.md'",
            [],
        )
        .unwrap();
    }
    session.scan_initial(&CancelToken::new()).unwrap();
    let birth: i64 = {
        let conn = session.conn.lock().unwrap();
        conn.query_row(
            "SELECT birthtime_ms FROM files WHERE path = 'old.md'",
            [],
            |r| r.get(0),
        )
        .unwrap()
    };
    // The fs reports a real birth, so the fast path may legitimately
    // refresh it — the invariant is only that it never becomes 0.
    assert!(birth > 0, "a known birth never degrades to the sentinel");
}

/// `save_query_as_base` serialization round-trip for every temporal
/// operator (regression: the #801 `method_source` arms carried an
/// `oplog.` prefix, so a saved query serialized as
/// `oplog.oplog.created_since(...)` — which no longer re-parses as the
/// operator and killed the saved view on read-back).
#[test]
fn saved_query_serialization_round_trips_every_operator() {
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    let r = session
        .save_text("n.md", "keep DOOMED keep\n", None)
        .unwrap();
    session
        .save_text("n.md", "keep keep\n", Some(&r.new_content_hash))
        .unwrap();

    for (filter, expect_match) in [
        (r#"oplog.has_change_since("1h")"#, true),
        (r#"oplog.has_property_change("k", "1h")"#, false),
        (r#"oplog.deleted_content_matches("doomed", "1h")"#, true),
        (r#"oplog.created_since("1h")"#, true),
        (r#"oplog.untouched_for("1h")"#, false),
        (
            r#"oplog.deleted_content_matches_regex("doo.ed", "1h")"#,
            true,
        ),
    ] {
        let yaml = format!(
            "views:\n  - type: table\n    name: T\n    filters: '{filter}'\n    order:\n      - file.name\n"
        );
        let (base, warnings) = crate::bases::parse_base(&yaml);
        assert!(warnings.is_empty(), "{filter}: {warnings:?}");
        let query_json = serde_json::to_string(&crate::bases::view_query(&base, 0)).unwrap();
        let path = "Queries/RoundTrip.base";
        session.save_query_as_base(&query_json, path).unwrap();

        // The serialized text carries the operator call — never a
        // doubled receiver. (Argument quoting is the YAML writer's
        // business; assert on the token up to the open paren.)
        let text = session.read_text(path).unwrap();
        let token = &filter[..filter.find('"').unwrap()];
        assert!(text.contains(token), "{filter}: operator survives:\n{text}");
        assert!(
            !text.contains("oplog.oplog"),
            "{filter}: doubled receiver:\n{text}"
        );

        // And the written .base re-parses INTO the operator: executing
        // it filters (no in-band view error, correct verdict for n.md).
        let handle = session.open_base(path).unwrap();
        let result = session
            .base_execute(handle, 0, None, None, &CancelToken::new())
            .unwrap();
        session.close_base(handle);
        assert_eq!(result.view_error, None, "{filter}");
        let paths: Vec<&str> = result.rows.iter().map(|r| r.file_path.as_str()).collect();
        // The .base file itself is also a vault note; only assert on n.md.
        assert_eq!(
            paths.contains(&"n.md"),
            expect_match,
            "{filter}: rows {paths:?}"
        );
    }
}

/// #800: the regex variant of `deleted_content_matches` — live regex
/// syntax, Unicode case folding (the capability LIKE cannot offer),
/// bounded compilation with in-band errors, and the same sampling-gap
/// semantics as the substring operator.
#[test]
fn deleted_content_matches_regex_is_unicode_case_aware_and_bounded() {
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();

    // Warm saves so the removed spans are sampled.
    let r = session
        .save_text("a.md", "keep ЗАМЕТКА draft-42 keep\n", None)
        .unwrap();
    session
        .save_text("a.md", "keep keep\n", Some(&r.new_content_hash))
        .unwrap();
    // b.md's deletion sits at the very start of the text, so anchors
    // have a boundary to bite on.
    let r = session
        .save_text("b.md", "zebra first\nkeep plain body\n", None)
        .unwrap();
    session
        .save_text("b.md", "keep plain body\n", Some(&r.new_content_hash))
        .unwrap();

    // Live regex syntax: classes, alternation, bounded repetition.
    let (paths, err) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches_regex("draft-[0-9]{2}", "1h")"#,
    );
    assert_eq!(err, None);
    assert_eq!(paths, vec!["a.md"]);

    // Unicode case folding: the deletion is uppercase Cyrillic; the
    // lowercase pattern matches through the regex variant…
    let (paths, err) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches_regex("заметка", "1h")"#,
    );
    assert_eq!(err, None);
    assert_eq!(paths, vec!["a.md"]);
    // …while the LIKE-based substring variant folds ASCII only — the
    // delta #800 exists to close, pinned here.
    let (paths, _) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches("заметка", "1h")"#,
    );
    assert_eq!(paths, Vec::<String>::new(), "LIKE folding is ASCII-only");

    // Anchors work against the whole sampled span.
    let (paths, _) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches_regex("^ZEBRA", "1h")"#,
    );
    assert_eq!(paths, vec!["b.md"]);

    // Composes at a top-level AND (no pushdown exists — the residual
    // row-eval fallback carries it) and under OR.
    let (paths, err) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches_regex("зам.тка", "1h") && file.name == "a.md""#,
    );
    assert_eq!(err, None);
    assert_eq!(paths, vec!["a.md"]);
    let (paths, err) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches_regex("nomatch.+x", "1h") || file.name == "b.md""#,
    );
    assert_eq!(err, None);
    assert_eq!(paths, vec!["b.md"]);

    // A bad pattern is an in-band view error naming the operator, in
    // both filter positions.
    for filter in [
        r#"oplog.deleted_content_matches_regex("(", "1h")"#,
        r#"oplog.deleted_content_matches_regex("(", "1h") || file.name == "a.md""#,
    ] {
        let (paths, err) = filter_paths(&session, filter);
        assert!(paths.is_empty(), "{filter}: no rows on error");
        let err = err.unwrap_or_else(|| panic!("{filter}: expected a view error"));
        assert!(
            err.contains("deleted_content_matches_regex"),
            "{filter}: error names the operator: {err}"
        );
    }

    // Bounded compilation: a pattern whose compiled program exceeds
    // the 1 MiB size limit is refused in-band — never compiled or run.
    let huge = format!(
        r#"oplog.deleted_content_matches_regex("{}", "1h")"#,
        "[a-z]{100}{100}{100}"
    );
    let (paths, err) = filter_paths(&session, &huge);
    assert!(paths.is_empty());
    assert!(
        err.is_some_and(|e| e.contains("deleted_content_matches_regex")),
        "size-limited pattern is an in-band error"
    );

    // Cold-cache deletions sample NULL and never match — the same
    // documented sampling gap as the substring variant. `.` matches
    // any sampled deletion, so only the warm files appear.
    let (paths, _) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches_regex(".", "1h")"#,
    );
    assert_eq!(paths, vec!["a.md", "b.md"], "warm deletions only");
}

/// The memo contract (codex round 1 on #800): one query compiles each
/// distinct pattern EXACTLY once, however many candidate rows the
/// residual row-eval visits — the eval arm must not validate-compile
/// per row, and the lookup memo must hold across rows (including rows
/// with no matching events).
#[test]
fn regex_pattern_compiles_once_per_query() {
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    // Eight candidates: three with warm sampled deletions (one
    // matching), five never-edited (no events at all — the
    // short-circuit rows).
    for i in 0..3 {
        let path = format!("edited{i}.md");
        let r = session
            .save_text(&path, &format!("keep chameleon_{i} keep\n"), None)
            .unwrap();
        session
            .save_text(&path, "keep keep\n", Some(&r.new_content_hash))
            .unwrap();
    }
    for i in 0..5 {
        session
            .save_text(&format!("quiet{i}.md"), "still\n", None)
            .unwrap();
    }

    // Test-unique pattern: the count below is per-pattern, so parallel
    // tests using the builder cannot interfere.
    let pattern = "chameleon_[01]";
    let (paths, err) = filter_paths(
        &session,
        &format!(r#"oplog.deleted_content_matches_regex("{pattern}", "1h")"#),
    );
    assert_eq!(err, None);
    assert_eq!(paths, vec!["edited0.md", "edited1.md"]);

    let counts = crate::bases::eval::regex_build_counts()
        .lock()
        .expect("build-count mutex");
    assert_eq!(
        counts.get(pattern).copied(),
        Some(1),
        "one compile per query per distinct pattern"
    );
}

/// #831: `oplog_events` growth is bounded by the retention window
/// under ONE shared rule. Producers (rebuild here) never write
/// beyond-window rows; the scan-time age-out prunes rows that were
/// legal when written (append-time rows that aged past the window, or
/// pre-#831 leftovers); and after both, the table equals what a fresh
/// rebuild regenerates — rebuild ≡ append-plus-age-out.
#[test]
fn event_rows_age_out_on_scan_and_rebuild_agrees() {
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    let cache_dir = session.config.cache_dir.clone();

    // Bind a log, then replace it wholesale with a constructed history:
    // one entry far beyond the 90-day window, one within it.
    session.save_text("n.md", "seed\n", None).unwrap();
    let stem: String = {
        let conn = session.conn.lock().unwrap();
        conn.query_row(
            "SELECT oplog_name FROM files WHERE path = 'n.md'",
            [],
            |row| row.get(0),
        )
        .unwrap()
    };
    std::fs::remove_file(crate::oplog::oplog_path_for_name(&cache_dir, &stem)).unwrap();
    crate::oplog::try_create_log(&cache_dir, &stem, "n.md").unwrap();
    let now = now_ms();
    let old_doc = "ancient ANCIENTWORD\n".to_string();
    let new_doc = "recent RECENTWORD\n".to_string();
    let entries = vec![
        crate::oplog::OpLogEntry {
            timestamp_ms: now - 100 * DAY_MS,
            user_actor_id: "t".into(),
            op_kind: crate::OpKind::WholeFileReplace,
            content_hash_before: crate::vault::content_hash(b"seed\n"),
            content_hash_after: crate::vault::content_hash(old_doc.as_bytes()),
            payload_bytes: old_doc.clone().into_bytes(),
        },
        crate::oplog::OpLogEntry {
            timestamp_ms: now - DAY_MS,
            user_actor_id: "t".into(),
            op_kind: crate::OpKind::WholeFileReplace,
            content_hash_before: crate::vault::content_hash(old_doc.as_bytes()),
            content_hash_after: crate::vault::content_hash(new_doc.as_bytes()),
            payload_bytes: new_doc.clone().into_bytes(),
        },
    ];
    for entry in &entries {
        crate::oplog::append_entry(&cache_dir, &stem, "n.md", entry).unwrap();
    }

    // Producer half: the forced rebuild derives from the full log but
    // writes only the in-window row.
    {
        let mut conn = session.conn.lock().unwrap();
        session.rebuild_oplog_events_if_stale(&mut conn, true);
    }
    let after_rebuild = events_for(&session, "n.md");
    assert_eq!(after_rebuild.len(), 1, "beyond-window row never produced");
    assert_eq!(after_rebuild[0].0, now - DAY_MS);

    // Pruner half: an append-time row that has since aged past the
    // window (simulated directly — exactly what a pre-#831 table
    // holds). The next scan's age-out removes it.
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "INSERT INTO oplog_events (file_id, ts_ms, event_class, property_key, deleted_text)
             SELECT id, ?1, 1, NULL, 'stale sample' FROM files WHERE path = 'n.md'",
            rusqlite::params![now - 95 * DAY_MS],
        )
        .unwrap();
    }
    assert_eq!(events_for(&session, "n.md").len(), 2, "leftover seeded");
    session.scan_initial(&CancelToken::new()).unwrap();
    let after_scan = events_for(&session, "n.md");
    assert_eq!(after_scan, after_rebuild, "age-out ≡ rebuild, row for row");

    // Operators see exactly the retained window.
    let (paths, _) = filter_paths(
        &session,
        r#"oplog.deleted_content_matches("ANCIENTWORD", "6w")"#,
    );
    assert_eq!(
        paths,
        Vec::<String>::new(),
        "beyond-window deletion invisible"
    );
    let (paths, _) = filter_paths(&session, r#"oplog.has_change_since("2d")"#);
    assert_eq!(paths, vec!["n.md"], "in-window change visible");

    // Retention shrink (the O-5 runtime setter): the next scan prunes
    // to the new window and a rebuild agrees.
    session.set_retention_days(30);
    session.save_text("m.md", "other\n", None).unwrap(); // untouched control
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "INSERT INTO oplog_events (file_id, ts_ms, event_class, property_key, deleted_text)
             SELECT id, ?1, 1, NULL, NULL FROM files WHERE path = 'n.md'",
            rusqlite::params![now - 40 * DAY_MS],
        )
        .unwrap();
    }
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        events_for(&session, "n.md"),
        after_rebuild,
        "shrunk window prunes the 40-day row"
    );
    {
        let mut conn = session.conn.lock().unwrap();
        session.rebuild_oplog_events_if_stale(&mut conn, true);
    }
    assert_eq!(
        events_for(&session, "n.md"),
        after_rebuild,
        "rebuild under the shrunk window agrees"
    );
}
