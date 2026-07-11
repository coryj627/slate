// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! O-3 (#541) — version history + deleted-file recovery APIs.
//!
//! The load-bearing invariant: **no version operation ever serves
//! bytes whose hash doesn't match the requested version** — integrity
//! verification refuses with `HistoryUnavailable` instead.

use super::common::*;
use super::*;

fn hash(content: &str) -> String {
    crate::vault::content_hash(content.as_bytes())
}

/// A session over a fixture note with a rich history: saves, a
/// property edit, a rename, and returns the ordered content states.
fn history_fixture() -> (tempfile::TempDir, VaultSession, Vec<String>) {
    let (tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"v0\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let mut states = Vec::new();

    let r0 = session.save_text("note.md", "v0\n", None).unwrap();
    states.push("v0\n".to_string());
    let r1 = session
        .save_text("note.md", "v0\nv1\n", Some(&r0.new_content_hash))
        .unwrap();
    states.push("v0\nv1\n".to_string());
    let r2 = session
        .set_property(
            "note.md",
            "status",
            crate::frontmatter::PropertyValue::Text("final".into()),
            Some(&r1.new_content_hash),
        )
        .unwrap();
    let with_prop = session.read_text("note.md").unwrap();
    states.push(with_prop.clone());
    session
        .save_text(
            "note.md",
            &format!("{with_prop}v3\n"),
            Some(&r2.new_content_hash),
        )
        .unwrap();
    states.push(format!("{with_prop}v3\n"));
    // Rename appends a pure marker.
    session.rename_file("note.md", "renamed.md").unwrap();
    (tmp, session, states)
}

#[test]
fn list_versions_pins_order_positions_annotations_and_markers() {
    let (_tmp, session, states) = history_fixture();
    let page = session
        .list_versions("renamed.md", Paging::first(50))
        .unwrap();
    // 4 saves + 1 marker, newest first.
    assert_eq!(page.total_filtered, 5);
    assert_eq!(page.next_cursor, None);
    let rows = &page.items;
    assert_eq!(rows.len(), 5);
    for (i, row) in rows.iter().enumerate() {
        assert_eq!(row.position_from_tail, i as u32, "newest first");
    }
    // Row 0: the marker.
    assert!(rows[0].is_marker);
    assert_eq!(rows[0].op_count, 0);
    assert_eq!(rows[0].byte_delta, 0);
    assert_eq!(
        rows[0].annotations[0].display,
        "Renamed from note.md to renamed.md"
    );
    assert_eq!(rows[0].content_hash_after, hash(&states[3]));
    // Row 1: the v3 batch.
    assert!(!rows[1].is_marker);
    assert_eq!(rows[1].op_kind, crate::OpKind::EditBatch);
    assert!(rows[1].op_count >= 1);
    assert_eq!(rows[1].byte_delta, 3); // "v3\n"
    assert!(rows[1].audio_fragment.contains("3 bytes added"));
    // Row 2: the property edit — annotated.
    assert_eq!(rows[2].annotations[0].display, "Set property 'status'");
    assert_eq!(rows[2].content_hash_after, hash(&states[2]));
    // Row 4 (oldest): the cold-cache snapshot.
    assert_eq!(rows[4].op_kind, crate::OpKind::WholeFileReplace);
    assert_eq!(rows[4].content_hash_after, hash(&states[0]));
    assert_eq!(rows[4].byte_delta, states[0].len() as i64);
}

#[test]
fn list_versions_paging_drains_and_generation_bump_invalidates_cursor() {
    let (_tmp, session, _states) = history_fixture();
    // Drain with limit 2: 5 rows → 3 pages.
    let p1 = session
        .list_versions("renamed.md", Paging::first(2))
        .unwrap();
    assert_eq!(p1.items.len(), 2);
    let c1 = p1.next_cursor.clone().expect("more pages");
    let p2 = session
        .list_versions("renamed.md", Paging::after(c1.clone(), 2))
        .unwrap();
    assert_eq!(p2.items.len(), 2);
    let c2 = p2.next_cursor.clone().expect("one more");
    let p3 = session
        .list_versions("renamed.md", Paging::after(c2.clone(), 2))
        .unwrap();
    assert_eq!(p3.items.len(), 1);
    assert_eq!(p3.next_cursor, None);
    // Positions are continuous across the drain.
    let all: Vec<u32> = p1
        .items
        .iter()
        .chain(&p2.items)
        .chain(&p3.items)
        .map(|r| r.position_from_tail)
        .collect();
    assert_eq!(all, vec![0, 1, 2, 3, 4]);

    // Force a compaction (generation bump) mid-drain → typed error.
    let stem: String = {
        let conn = session.conn.lock().unwrap();
        conn.query_row(
            "SELECT oplog_name FROM files WHERE path = 'renamed.md'",
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
        "renamed.md",
        &limits,
        now_ms(),
    )
    .unwrap();
    assert!(matches!(
        outcome,
        crate::oplog_compaction::CompactionOutcome::Rewritten { .. }
    ));
    let err = session
        .list_versions("renamed.md", Paging::after(c1.clone(), 2))
        .unwrap_err();
    assert!(
        matches!(&err, VaultError::InvalidArgument { message }
            if message == "history changed, restart paging"),
        "got {err:?}"
    );
    // A fresh page one works.
    assert!(
        session
            .list_versions("renamed.md", Paging::first(50))
            .is_ok()
    );
}

#[test]
fn version_content_is_byte_exact_at_every_version() {
    let (_tmp, session, states) = history_fixture();
    for state in &states {
        assert_eq!(
            session.version_content("renamed.md", &hash(state)).unwrap(),
            *state,
            "every version must reconstruct byte-exactly"
        );
    }
    // Unknown hash → InvalidArgument, not HistoryUnavailable.
    let err = session
        .version_content("renamed.md", &hash("never existed"))
        .unwrap_err();
    assert!(
        matches!(&err, VaultError::InvalidArgument { message } if message == "no such version")
    );
}

#[test]
fn duplicate_hashes_resolve_to_identical_bytes() {
    // A→B→A: the same hash occurs twice; any occurrence serves the
    // same (verified) bytes.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"A\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let ra = session.save_text("n.md", "A\n", None).unwrap();
    let rb = session
        .save_text("n.md", "B\n", Some(&ra.new_content_hash))
        .unwrap();
    session
        .save_text("n.md", "A\n", Some(&rb.new_content_hash))
        .unwrap();
    assert_eq!(
        session.version_content("n.md", &hash("A\n")).unwrap(),
        "A\n"
    );
    assert_eq!(
        session.version_content("n.md", &hash("B\n")).unwrap(),
        "B\n"
    );
    let rows = session.list_versions("n.md", Paging::first(10)).unwrap();
    let a_rows: Vec<_> = rows
        .items
        .iter()
        .filter(|r| r.content_hash_after == hash("A\n"))
        .collect();
    assert_eq!(
        a_rows.len(),
        2,
        "duplicate-hash rows are distinct by position"
    );
}

#[test]
fn corrupted_chain_yields_history_unavailable_never_wrong_bytes() {
    // Forge a log whose entry claims a hash its reconstruction doesn't
    // produce: version_content must refuse with HistoryUnavailable.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"seed\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
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
    // Append a forged batch entry claiming a bogus hash_after.
    let bogus_hash = hash("bytes this log cannot produce");
    let forged = crate::oplog::OpLogEntry {
        timestamp_ms: now_ms(),
        user_actor_id: "forger".into(),
        op_kind: crate::OpKind::EditBatch,
        content_hash_before: hash("seed\n"),
        content_hash_after: bogus_hash.clone(),
        payload_bytes: crate::oplog::encode_edit_batch(&[crate::oplog::EditOp::Insert {
            pos: 0,
            text: "x".into(),
        }]),
    };
    crate::oplog::append_entry(&session.config.cache_dir, &stem, "n.md", &forged).unwrap();

    let err = session.version_content("n.md", &bogus_hash).unwrap_err();
    assert!(
        matches!(err, VaultError::HistoryUnavailable { .. }),
        "corrupt chain must refuse, got {err:?}"
    );
    // And restore of that version refuses too, writing nothing.
    let before = session.read_text("n.md").unwrap();
    let err = session
        .restore_version("n.md", &bogus_hash, Some(&hash(&before)))
        .unwrap_err();
    assert!(matches!(err, VaultError::HistoryUnavailable { .. }));
    assert_eq!(
        session.read_text("n.md").unwrap(),
        before,
        "nothing written"
    );
}

#[test]
fn restore_is_cross_session_byte_exact_conflict_guarded_and_append_only() {
    let tmp = tempfile::tempdir().unwrap();
    let mid_state;
    let final_hash;
    {
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();
        let r0 = session.save_text("doc.md", "one\n", None).unwrap();
        let r1 = session
            .save_text("doc.md", "one\ntwo\n", Some(&r0.new_content_hash))
            .unwrap();
        session
            .save_text("doc.md", "one\ntwo\nthree\n", Some(&r1.new_content_hash))
            .unwrap();
        mid_state = "one\ntwo\n".to_string();
        final_hash = hash("one\ntwo\nthree\n");
    }
    // Session 2 (the milestone DoD case: edit across sessions, restore
    // a mid-sequence version, read_text equals the bytes at that
    // point).
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    let versions_before = session
        .list_versions("doc.md", Paging::first(50))
        .unwrap()
        .total_filtered;

    // Stale expected hash → WriteConflict, nothing written.
    let err = session
        .restore_version("doc.md", &hash(&mid_state), Some(&hash("stale")))
        .unwrap_err();
    assert!(matches!(err, VaultError::WriteConflict { .. }));
    assert_eq!(session.read_text("doc.md").unwrap(), "one\ntwo\nthree\n");

    // Correct expected hash → restored byte-exactly.
    session
        .restore_version("doc.md", &hash(&mid_state), Some(&final_hash))
        .unwrap();
    assert_eq!(session.read_text("doc.md").unwrap(), mid_state);

    // Restore APPENDS: one new version; prior versions unchanged.
    let page = session.list_versions("doc.md", Paging::first(50)).unwrap();
    assert_eq!(page.total_filtered, versions_before + 1);
    assert_eq!(
        session.version_content("doc.md", &final_hash).unwrap(),
        "one\ntwo\nthree\n",
        "the pre-restore state remains available in history"
    );
}

#[test]
fn create_exclusive_creates_once_and_refuses_occupants() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("indexed.md", b"indexed\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Absent → created atomically (and indexed + op-logged).
    let report = session.create_exclusive("fresh.md", "born\n").unwrap();
    assert_eq!(report.new_content_hash, hash("born\n"));
    assert_eq!(session.read_text("fresh.md").unwrap(), "born\n");
    assert_eq!(session.read_oplog("fresh.md").unwrap().len(), 1);

    // Present in the index → DestinationExists.
    let err = session.create_exclusive("indexed.md", "x").unwrap_err();
    assert!(matches!(err, VaultError::DestinationExists { .. }));
    assert_eq!(session.read_text("indexed.md").unwrap(), "indexed\n");

    // Present on disk but NOT indexed → DestinationExists too.
    std::fs::write(tmp.path().join("unindexed.md"), b"already here\n").unwrap();
    let err = session.create_exclusive("unindexed.md", "x").unwrap_err();
    assert!(matches!(err, VaultError::DestinationExists { .. }));
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("unindexed.md")).unwrap(),
        "already here\n"
    );
}

#[test]
fn deleted_file_lifecycle_lists_recovers_and_keeps_history() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("keep.md", b"bystander\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let r0 = session.save_text("gone.md", "first\n", None).unwrap();
    session
        .save_text("gone.md", "first\nlast\n", Some(&r0.new_content_hash))
        .unwrap();
    session.delete_file("gone.md").unwrap();
    // The remnant surfaces on the next scan.
    session.scan_initial(&CancelToken::new()).unwrap();

    let page = session.list_deleted_files(Paging::first(10)).unwrap();
    assert_eq!(page.total_filtered, 1);
    let entry = &page.items[0];
    assert_eq!(entry.path, "gone.md");
    assert!(entry.recoverable);
    assert_eq!(entry.size_bytes, Some("first\nlast\n".len() as u64));
    assert!(
        entry.deleted_at_ms.is_some(),
        "journal timestamp joins onto the remnant"
    );

    // Recover: bytes equal the pre-delete tail; history CONTINUES
    // (pre-deletion versions still listed).
    session.recover_deleted_file("gone.md").unwrap();
    assert_eq!(session.read_text("gone.md").unwrap(), "first\nlast\n");
    let versions = session.list_versions("gone.md", Paging::first(50)).unwrap();
    assert!(
        versions.total_filtered >= 3,
        "pre-deletion history must survive recovery; got {}",
        versions.total_filtered
    );
    assert_eq!(
        session
            .version_content("gone.md", &hash("first\n"))
            .unwrap(),
        "first\n",
        "a pre-deletion version reconstructs after recovery"
    );
    // The deleted list no longer offers it.
    let page = session.list_deleted_files(Paging::first(10)).unwrap();
    assert_eq!(page.total_filtered, 0);

    // Never-saved deleted files are absent (the honesty rule).
    session.delete_file("keep.md").unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    let page = session.list_deleted_files(Paging::first(10)).unwrap();
    assert!(
        page.items.iter().all(|e| e.path != "keep.md"),
        "a file never saved through Slate has no log and no recovery row"
    );
}

#[test]
fn recover_onto_occupied_path_refuses() {
    let (tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("busy.md", "old life\n", None).unwrap();
    session.delete_file("busy.md").unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    // Someone re-creates the path out of band.
    std::fs::write(tmp.path().join("busy.md"), b"new occupant\n").unwrap();
    let err = session.recover_deleted_file("busy.md").unwrap_err();
    assert!(matches!(err, VaultError::DestinationExists { .. }));
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("busy.md")).unwrap(),
        "new occupant\n",
        "nothing overwritten"
    );
}

#[test]
fn delete_recreate_delete_lists_one_row_newest_wins() {
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("cycle.md", "life one\n", None).unwrap();
    session.delete_file("cycle.md").unwrap();
    session.save_text("cycle.md", "life two\n", None).unwrap();
    session.delete_file("cycle.md").unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    let page = session.list_deleted_files(Paging::first(10)).unwrap();
    let rows: Vec<_> = page.items.iter().filter(|e| e.path == "cycle.md").collect();
    assert_eq!(rows.len(), 1, "one row per path; newest remnant wins");
    assert_eq!(rows[0].size_bytes, Some("life two\n".len() as u64));

    session.recover_deleted_file("cycle.md").unwrap();
    assert_eq!(
        session.read_text("cycle.md").unwrap(),
        "life two\n",
        "recovery returns the NEWEST life's content"
    );
}

#[test]
fn write_file_if_absent_is_no_replace_under_racing_writers() {
    // The filesystem no-replace primitive (adversarial review):
    // racing writers on one path — exactly one wins, the loser gets
    // DestinationExists, and the winner's bytes survive intact.
    use std::sync::Barrier;
    let tmp = tempfile::tempdir().unwrap();
    let provider = Arc::new(FsVaultProvider::new(tmp.path().to_path_buf()));
    const THREADS: usize = 8;
    let barrier = Arc::new(Barrier::new(THREADS));
    let handles: Vec<_> = (0..THREADS)
        .map(|i| {
            let provider = Arc::clone(&provider);
            let barrier = Arc::clone(&barrier);
            std::thread::spawn(move || {
                barrier.wait();
                provider
                    .write_file_if_absent("raced.md", format!("writer {i}\n").as_bytes())
                    .map(|()| i)
            })
        })
        .collect();
    let mut winners = Vec::new();
    for handle in handles {
        if let Ok(i) = handle.join().unwrap() {
            winners.push(i);
        }
    }
    assert_eq!(winners.len(), 1, "exactly one writer must win");
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("raced.md")).unwrap(),
        format!("writer {}\n", winners[0]),
        "the winner's bytes survive byte-exactly"
    );
}

#[test]
fn failed_recovery_leaves_no_phantom_row_and_stays_retryable() {
    // Adversarial review: a recovery whose disk write fails must roll
    // the whole row (and its remnant binding) back — the remnant stays
    // listed and a later retry succeeds.
    let (tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("fragile.md", "precious\n", None).unwrap();
    session.delete_file("fragile.md").unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        session
            .list_deleted_files(Paging::first(10))
            .unwrap()
            .total_filtered,
        1
    );

    // Make the vault root read-only: the no-replace publish fails.
    let root = tmp.path();
    let mut perms = std::fs::metadata(root).unwrap().permissions();
    let original = perms.clone();
    use std::os::unix::fs::PermissionsExt;
    perms.set_mode(0o555);
    std::fs::set_permissions(root, perms).unwrap();
    let err = session.recover_deleted_file("fragile.md");
    std::fs::set_permissions(root, original).unwrap();
    assert!(err.is_err(), "the write must fail under a read-only root");

    // No phantom: the path is not indexed, the remnant is still
    // offered, and the retry succeeds.
    let conn = session.conn.lock().unwrap();
    let indexed: i64 = conn
        .query_row(
            "SELECT COUNT(*) FROM files WHERE path = 'fragile.md'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    drop(conn);
    assert_eq!(indexed, 0, "a failed recovery must not commit a row shell");
    assert_eq!(
        session
            .list_deleted_files(Paging::first(10))
            .unwrap()
            .total_filtered,
        1,
        "the remnant is still recoverable"
    );
    session.recover_deleted_file("fragile.md").unwrap();
    assert_eq!(session.read_text("fragile.md").unwrap(), "precious\n");
}

#[test]
fn deleted_files_cursor_is_invalidated_by_set_mutation() {
    // Adversarial review: paging across a recovery must fail typed —
    // never silently skip or duplicate rows against the mutated set.
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    for i in 0..3 {
        let path = format!("d{i}.md");
        session
            .save_text(&path, &format!("body {i}\n"), None)
            .unwrap();
        session.delete_file(&path).unwrap();
    }
    session.scan_initial(&CancelToken::new()).unwrap();

    let p1 = session.list_deleted_files(Paging::first(1)).unwrap();
    assert_eq!(p1.total_filtered, 3);
    let cursor = p1.next_cursor.clone().expect("more pages");

    // Mutate the set between pages.
    session.recover_deleted_file(&p1.items[0].path).unwrap();

    let err = session
        .list_deleted_files(Paging::after(cursor, 1))
        .unwrap_err();
    assert!(
        matches!(&err, VaultError::InvalidArgument { message }
            if message == "deleted files changed, restart paging"),
        "got {err:?}"
    );
    // Page one works and reflects the new set.
    let fresh = session.list_deleted_files(Paging::first(10)).unwrap();
    assert_eq!(fresh.total_filtered, 2);
}

#[test]
fn post_rebuild_deletion_lists_with_unknown_timestamp() {
    let tmp = tempfile::tempdir().unwrap();
    {
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();
        session.save_text("lost.md", "content\n", None).unwrap();
        session.delete_file("lost.md").unwrap();
    }
    // Rebuild: the journal (with the DeleteFile row) is gone; the log
    // survives.
    std::fs::remove_file(tmp.path().join(".slate/cache.sqlite")).unwrap();
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    let page = session.list_deleted_files(Paging::first(10)).unwrap();
    let entry = page
        .items
        .iter()
        .find(|e| e.path == "lost.md")
        .expect("remnant survives the rebuild");
    assert_eq!(entry.deleted_at_ms, None, "no journal → honest None");
    assert!(entry.recoverable);
}

// --- History prefs (O-5 #543) -------------------------------------------

#[test]
fn history_prefs_roundtrip_apply_live_and_survive_reopen() {
    let tmp = tempfile::tempdir().unwrap();
    {
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        assert_eq!(
            session.history_prefs().retention_days,
            90,
            "config default without a prefs file"
        );

        // Persist + live-apply in one call.
        session
            .set_history_prefs(crate::history_prefs::HistoryPrefs { retention_days: 30 })
            .unwrap();
        assert_eq!(
            session.retention_days(),
            30,
            "compaction window applied live"
        );

        // Rejected values change nothing anywhere.
        assert!(
            session
                .set_history_prefs(crate::history_prefs::HistoryPrefs { retention_days: 0 })
                .is_err()
        );
        assert_eq!(session.retention_days(), 30);

        // Unknown prefs.json keys survive the write (the writer
        // discipline the Swift store established).
        let raw = std::fs::read_to_string(tmp.path().join(".slate/prefs.json")).unwrap();
        let mut root: serde_json::Value = serde_json::from_str(&raw).unwrap();
        root["future_section"] = serde_json::json!({"keep": true});
        std::fs::write(
            tmp.path().join(".slate/prefs.json"),
            serde_json::to_string_pretty(&root).unwrap(),
        )
        .unwrap();
        session
            .set_history_prefs(crate::history_prefs::HistoryPrefs {
                retention_days: 180,
            })
            .unwrap();
        let root: serde_json::Value = serde_json::from_str(
            &std::fs::read_to_string(tmp.path().join(".slate/prefs.json")).unwrap(),
        )
        .unwrap();
        assert_eq!(root["future_section"]["keep"], true);
        assert_eq!(root["history"]["retention_days"], 180);
    }

    // A fresh session reads the persisted window at open.
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    assert_eq!(session.history_prefs().retention_days, 180);
    assert_eq!(
        session.retention_days(),
        180,
        "runtime config seeded from prefs"
    );
}

// --- Restore As… (#795) --------------------------------------------------

#[test]
fn recover_deleted_file_as_lands_at_the_chosen_path_with_history() {
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    let body: String = (0..12).map(|i| format!("keep line {i}\n")).collect();
    let r = session
        .save_text("lost.md", &format!("{body}PHOENIX\n"), None)
        .unwrap();
    session
        .save_text("lost.md", &body, Some(&r.new_content_hash))
        .unwrap();
    session.delete_file("lost.md").unwrap();
    // Surface the remnant, THEN a squatter appears at the original
    // path OUTSIDE the index (external write, no rescan) — the
    // reachable occupied-destination case. (An INDEXED squatter
    // quarantines the remnant at the next reconcile — never guess —
    // so recovery wouldn't offer it at all.)
    session.scan_initial(&CancelToken::new()).unwrap();
    let root = session.config.cache_dir.parent().unwrap().to_path_buf();
    std::fs::write(root.join("lost.md"), b"squatter\n").unwrap();

    // Original-path recovery refuses (no clobber)…
    assert!(matches!(
        session.recover_deleted_file("lost.md"),
        Err(VaultError::DestinationExists { .. })
    ));
    assert_eq!(
        std::fs::read_to_string(root.join("lost.md")).unwrap(),
        "squatter\n",
        "refusal wrote nothing"
    );

    // …and Restore As… lands at the chosen destination with the tail
    // content, the squatter untouched.
    session
        .recover_deleted_file_as("lost.md", "recovered/lost (restored).md")
        .unwrap();
    assert_eq!(
        session.read_text("recovered/lost (restored).md").unwrap(),
        body
    );
    assert_eq!(
        std::fs::read_to_string(root.join("lost.md")).unwrap(),
        "squatter\n"
    );

    // The remnant log re-bound to the NEW path: pre-delete history is
    // paged there, and the remnant left the Deleted list.
    let page = session
        .list_versions("recovered/lost (restored).md", Paging::first(10))
        .unwrap();
    assert!(
        page.items.len() >= 3,
        "pre-delete versions + the recovery save: {}",
        page.items.len()
    );
    let deleted = session.list_deleted_files(Paging::first(10)).unwrap();
    assert!(deleted.items.iter().all(|e| e.path != "lost.md"));

    // Unknown source path is a typed error.
    assert!(matches!(
        session.recover_deleted_file_as("nope.md", "anywhere.md"),
        Err(VaultError::InvalidArgument { .. })
    ));
}

/// #797: history coverage for non-markdown writers — both route
/// through the save seam, so versions, verified content, and restores
/// all work; a canvas action's two journal entries (byte + semantic)
/// render as ONE version row carrying the action name.
#[test]
fn canvas_and_base_writes_are_versioned_through_the_seam() {
    use crate::canvas::apply::{CanvasAction, CanvasNodeContent, CanvasOp};

    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "board.canvas",
            br#"{"nodes":[{"id":"seed","type":"text","text":"seed","x":0,"y":0,"width":100,"height":50}],"edges":[]}"#,
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Canvas: one action = TWO oplog entries (the seam's byte save +
    // the semantic record) = ONE version row, action name attached.
    let info = session.open_canvas("board.canvas").unwrap();
    session
        .canvas_apply(
            info.handle,
            CanvasAction {
                name: "create card".into(),
                ops: vec![CanvasOp::CreateNode {
                    id: "n1".into(),
                    content: CanvasNodeContent::Text {
                        text: "first card".into(),
                    },
                    x: 0.0,
                    y: 100.0,
                    width: 200.0,
                    height: 100.0,
                    color: None,
                }],
            },
        )
        .unwrap();
    let page = session
        .list_versions("board.canvas", Paging::first(50))
        .unwrap();
    assert_eq!(page.total_filtered, 1, "one action, one version row");
    let row = &page.items[0];
    assert!(
        row.annotations
            .iter()
            .any(|a| a.kind == "CanvasAction" && a.display == "Canvas: create card"),
        "the action name rides the byte row: {:?}",
        row.annotations
    );

    // Verified content + restore work on .canvas like any note.
    let v1_hash = row.content_hash_after.clone();
    let v1_content = session.version_content("board.canvas", &v1_hash).unwrap();
    assert!(v1_content.contains("first card"));
    session
        .canvas_apply(
            info.handle,
            CanvasAction {
                name: "nudge card".into(),
                ops: vec![CanvasOp::UpdateNodeGeometry {
                    id: "n1".into(),
                    x: 50.0,
                    y: 150.0,
                    width: 200.0,
                    height: 100.0,
                }],
            },
        )
        .unwrap();
    let current = session
        .get_file_metadata("board.canvas")
        .unwrap()
        .unwrap()
        .content_hash;
    session
        .restore_version("board.canvas", &v1_hash, Some(&current))
        .unwrap();
    assert_eq!(
        session.read_text("board.canvas").unwrap(),
        v1_content,
        "the restore landed the verified bytes"
    );
    let after = session
        .list_versions("board.canvas", Paging::first(50))
        .unwrap();
    assert_eq!(
        after.total_filtered, 3,
        "restore appended its own row — history never rewrites"
    );

    // Base: save_query_as_base routes through save_text — versioned.
    let yaml = "views:\n  - type: table\n    name: T\n    order:\n      - file.name\n";
    let (base, warnings) = crate::bases::parse_base(yaml);
    assert!(warnings.is_empty(), "{warnings:?}");
    let query_json = serde_json::to_string(&crate::bases::view_query(&base, 0)).unwrap();
    session
        .save_query_as_base(&query_json, "Queries/Q.base")
        .unwrap();
    let base_page = session
        .list_versions("Queries/Q.base", Paging::first(10))
        .unwrap();
    assert_eq!(base_page.total_filtered, 1, ".base writes are versioned");
    let base_hash = &base_page.items[0].content_hash_after;
    let round_trip = session
        .version_content("Queries/Q.base", base_hash)
        .unwrap();
    assert_eq!(
        round_trip,
        session.read_text("Queries/Q.base").unwrap(),
        "verified .base bytes round-trip"
    );
}

/// #797 fold under interleave (codex): in-process splits are closed by
/// holding the session lock across both canvas appends, but a
/// cross-process writer can still land between them. The record then
/// renders as a STANDALONE "canvas action" row — the pre-fold
/// behavior for that one action — and must never mis-fold into the
/// interloper (whose hash pair differs).
#[test]
fn interleaved_semantic_record_degrades_to_standalone_never_misfolds() {
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("n.canvas", "{}\n", None).unwrap();
    let stem: String = {
        let conn = session.conn.lock().unwrap();
        conn.query_row(
            "SELECT oplog_name FROM files WHERE path = 'n.canvas'",
            [],
            |row| row.get(0),
        )
        .unwrap()
    };
    let cache_dir = session.config.cache_dir.clone();
    let (a, b, c) = ("A\n", "B\n", "C\n");
    let hash = |s: &str| crate::vault::content_hash(s.as_bytes());
    let mk = |kind, before: &str, after: &str, payload: Vec<u8>| crate::oplog::OpLogEntry {
        timestamp_ms: now_ms(),
        user_actor_id: "t".into(),
        op_kind: kind,
        content_hash_before: hash(before),
        content_hash_after: hash(after),
        payload_bytes: payload,
    };
    // byte(A→B), foreign save (B→C), then the canvas record (A→B)
    // landing late — the cross-process interleave shape.
    for entry in [
        mk(crate::OpKind::WholeFileReplace, a, b, b.as_bytes().to_vec()),
        mk(crate::OpKind::WholeFileReplace, b, c, c.as_bytes().to_vec()),
        mk(
            crate::OpKind::CanvasApply,
            a,
            b,
            br#"{"name":"late action"}"#.to_vec(),
        ),
    ] {
        crate::oplog::append_entry(&cache_dir, &stem, "n.canvas", &entry).unwrap();
    }

    let page = session
        .list_versions("n.canvas", Paging::first(10))
        .unwrap();
    // Initial save + the three constructed entries, NONE folded: the
    // record's predecessor (B→C) has a different pair.
    assert_eq!(page.total_filtered, 4);
    let canvas_row = page
        .items
        .iter()
        .find(|r| r.op_kind == crate::OpKind::CanvasApply)
        .expect("standalone canvas row");
    assert_eq!(canvas_row.audio_fragment, "canvas action");
    assert!(
        page.items
            .iter()
            .all(|r| !r.annotations.iter().any(|a| a.kind == "CanvasAction")),
        "no row absorbed the interleaved record"
    );
}
