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

    // One save per file creates the binding; then the log is replaced
    // wholesale with a constructed history.
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
        let now = now_ms();
        let mut ts_offsets: Vec<i64> = (0..1 + rng.below(6))
            .map(|_| (rng.below(4032) as i64 + 1) * 15 * 60_000 + 5 * 60_000)
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
        expected_rows.push((path.clone(), rows));
    }

    // The rebuilt table must agree with the reference row-for-row —
    // the derivation half of the census.
    for (path, want) in &expected_rows {
        assert_eq!(&events_for(&session, path), want, "derived rows for {path}");
    }

    for probe in 0..probes {
        let (unit, unit_ms) =
            [("h", HOUR_MS), ("d", DAY_MS), ("w", 7 * DAY_MS)][rng.below(3) as usize];
        let n = rng.below(if unit == "h" { 1000 } else { 6 }) + 1;
        let duration = format!("{n}{unit}");
        let cutoff = now_ms() - (n as i64) * unit_ms;

        let (filter, reference): (String, Vec<String>) = match rng.below(3) {
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
}
