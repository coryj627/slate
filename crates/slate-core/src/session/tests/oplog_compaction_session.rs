// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! O-2 (#540) — session-level compaction: the background worker, the
//! save-path trigger, the `VaultEventListener` error channel, the
//! on-open sweep, and remnant reclamation.

use super::*;

/// Recording listener: captures every `on_error` dispatch.
struct RecordingListener {
    events: Mutex<Vec<(EventErrorCode, String, String)>>,
}

impl RecordingListener {
    fn new() -> Arc<Self> {
        Arc::new(Self {
            events: Mutex::new(Vec::new()),
        })
    }
    fn events(&self) -> Vec<(EventErrorCode, String, String)> {
        self.events.lock().unwrap().clone()
    }
}

impl VaultEventListener for RecordingListener {
    fn on_error(&self, code: EventErrorCode, path: String, message: String) {
        self.events.lock().unwrap().push((code, path, message));
    }
}

/// Session whose byte threshold is tiny, so a couple of saves trip the
/// compaction trigger.
fn tiny_threshold_session() -> (tempfile::TempDir, VaultSession) {
    let tmp = tempfile::tempdir().unwrap();
    let cache_dir = tmp.path().join(".slate");
    let mut config = SessionConfig::new(cache_dir);
    config.oplog_compaction_threshold_bytes = 2048;
    let provider = Arc::new(FsVaultProvider::new(tmp.path().to_path_buf()));
    let session = VaultSession::open(provider, config).unwrap();
    (tmp, session)
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

fn log_size(session: &VaultSession, path: &str) -> u64 {
    let conn = session.conn.lock().unwrap();
    let name: String = conn
        .query_row(
            "SELECT oplog_name FROM files WHERE path = ?1",
            rusqlite::params![path],
            |row| row.get(0),
        )
        .unwrap();
    drop(conn);
    std::fs::metadata(crate::oplog::oplog_path_for_name(
        &session.config.cache_dir,
        &name,
    ))
    .unwrap()
    .len()
}

#[test]
fn oversized_log_is_compacted_in_the_background() {
    let (_tmp, session) = tiny_threshold_session();
    session.scan_initial(&CancelToken::new()).unwrap();

    // Grow the log well past the 2 KiB threshold with snapshot-heavy
    // saves (None-hash saves snapshot every time).
    let mut content = String::new();
    for i in 0..12 {
        content = format!("{content}line {i} with some padding text to bulk the log\n");
        session.save_text("hot.md", &content, None).unwrap();
    }
    assert!(
        wait_for(|| log_size(&session, "hot.md") <= 2048),
        "background compaction should shrink the log below the threshold; \
         size is {}",
        log_size(&session, "hot.md")
    );
    // History still reconstructs to the final content.
    let entries = session.read_oplog("hot.md").unwrap();
    assert_eq!(
        crate::oplog::reconstruct_at_tail(&entries).unwrap(),
        content
    );
}

#[test]
fn compaction_failure_dispatches_the_exact_listener_copy() {
    let (_tmp, session) = tiny_threshold_session();
    session.scan_initial(&CancelToken::new()).unwrap();
    let listener = RecordingListener::new();
    let token = session.register_event_listener(listener.clone());

    *crate::oplog_compaction::FAIL_NEXT_COMPACTION_FOR
        .lock()
        .unwrap() = Some(session.config.cache_dir.clone());
    let mut content = String::new();
    for i in 0..12 {
        content = format!("{content}line {i} with some padding text to bulk the log\n");
        session.save_text("doomed.md", &content, None).unwrap();
    }
    assert!(
        wait_for(|| !listener.events().is_empty()),
        "the injected compaction fault must reach the listener"
    );
    let events = listener.events();
    let (code, path, message) = &events[0];
    assert_eq!(*code, EventErrorCode::CompactionFailed);
    assert_eq!(path, "doomed.md");
    assert_eq!(
        message,
        "Slate couldn't compact the edit history for doomed.md: injected \
         compaction fault (test hook). History for this file may grow unbounded.",
        "the O-2/O-5 error copy is a contract — exact match required"
    );

    // Unregister: further failures don't reach it.
    session.unregister_event_listener(token);
    let before = listener.events().len();
    *crate::oplog_compaction::FAIL_NEXT_COMPACTION_FOR
        .lock()
        .unwrap() = Some(session.config.cache_dir.clone());
    content = format!("{content}more\n");
    session.save_text("doomed.md", &content, None).unwrap();
    std::thread::sleep(std::time::Duration::from_millis(300));
    assert_eq!(
        listener.events().len(),
        before,
        "an unregistered listener must not receive events"
    );
    *crate::oplog_compaction::FAIL_NEXT_COMPACTION_FOR
        .lock()
        .unwrap() = None;
}

#[test]
fn on_open_sweep_compacts_a_log_that_grew_while_closed() {
    let tmp = tempfile::tempdir().unwrap();
    // Session 1: huge threshold — no compaction ever triggers, the log
    // just grows.
    {
        let cache_dir = tmp.path().join(".slate");
        let provider = Arc::new(FsVaultProvider::new(tmp.path().to_path_buf()));
        let session = VaultSession::open(provider, SessionConfig::new(cache_dir)).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();
        let mut content = String::new();
        for i in 0..12 {
            content = format!("{content}line {i} with some padding text to bulk the log\n");
            session.save_text("grown.md", &content, None).unwrap();
        }
        assert!(log_size(&session, "grown.md") > 2048);
    }
    // Session 2: tiny threshold — the on-open sweep (after the scan
    // reconcile) must find the oversized log and queue it.
    let cache_dir = tmp.path().join(".slate");
    let mut config = SessionConfig::new(cache_dir);
    config.oplog_compaction_threshold_bytes = 2048;
    let provider = Arc::new(FsVaultProvider::new(tmp.path().to_path_buf()));
    let session = VaultSession::open(provider, config).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(
        wait_for(|| log_size(&session, "grown.md") <= 2048),
        "the on-open sweep should compact the oversized log"
    );
}

#[test]
fn out_of_retention_remnants_are_reclaimed_in_retention_kept() {
    let (_tmp, session) = tiny_threshold_session();
    session.scan_initial(&CancelToken::new()).unwrap();

    let cache_dir = &session.config.cache_dir;
    let old_ts = now_ms() - 200 * 24 * 60 * 60 * 1000; // 200 days ago
    let fresh_ts = now_ms() - 24 * 60 * 60 * 1000; // yesterday

    let make_remnant = |stem: &str, path: &str, ts: i64| {
        assert!(crate::oplog::try_create_log(cache_dir, stem, path).unwrap());
        crate::oplog::append_entry(
            cache_dir,
            stem,
            path,
            &crate::oplog::OpLogEntry {
                timestamp_ms: ts,
                user_actor_id: "t".into(),
                op_kind: crate::OpKind::WholeFileReplace,
                content_hash_before: String::new(),
                content_hash_after: crate::vault::content_hash(b"remnant body\n"),
                payload_bytes: b"remnant body\n".to_vec(),
            },
        )
        .unwrap();
    };
    make_remnant("stale", "gone/stale.md", old_ts);
    make_remnant("fresh", "gone/fresh.md", fresh_ts);

    session.scan_initial(&CancelToken::new()).unwrap();

    // Default retention is 90 days: the 200-day remnant is deleted
    // from disk and absent from the list; the fresh one survives with
    // full fidelity.
    assert!(
        !crate::oplog::oplog_path_for_name(cache_dir, "stale").exists(),
        "out-of-retention remnant must be reclaimed"
    );
    assert!(crate::oplog::oplog_path_for_name(cache_dir, "fresh").exists());
    let remnants = session.remnant_logs();
    assert!(remnants.iter().all(|r| r.stem != "stale"));
    assert!(
        remnants
            .iter()
            .any(|r| r.stem == "fresh" && r.effective_path == "gone/fresh.md")
    );

    // A live retention change applies to the NEXT sweep: shrink the
    // window below one day and the fresh remnant ages out too.
    session.set_retention_days(0);
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(
        !crate::oplog::oplog_path_for_name(cache_dir, "fresh").exists(),
        "runtime retention change must apply to the next sweep"
    );
    assert!(session.remnant_logs().iter().all(|r| r.stem != "fresh"));
}

#[test]
fn stale_quarantined_copy_is_reclaimed_on_the_same_rule() {
    let (_tmp, session) = tiny_threshold_session();
    session.scan_initial(&CancelToken::new()).unwrap();
    // A bound file whose log tail is ancient (timestamps are scripted
    // at the oplog layer), then a hand-copied duplicate: the copy is
    // quarantined (O-1) and — being out of retention — reclaimed.
    let cache_dir = &session.config.cache_dir;
    let old_ts = now_ms() - 200 * 24 * 60 * 60 * 1000;
    session.save_text("original.md", "body\n", None).unwrap();
    let conn = session.conn.lock().unwrap();
    let stem: String = conn
        .query_row(
            "SELECT oplog_name FROM files WHERE path = 'original.md'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    drop(conn);
    // Replace the real log with one whose tail is ancient, then copy it.
    std::fs::remove_file(crate::oplog::oplog_path_for_name(cache_dir, &stem)).unwrap();
    assert!(crate::oplog::try_create_log(cache_dir, &stem, "original.md").unwrap());
    crate::oplog::append_entry(
        cache_dir,
        &stem,
        "original.md",
        &crate::oplog::OpLogEntry {
            timestamp_ms: old_ts,
            user_actor_id: "t".into(),
            op_kind: crate::OpKind::WholeFileReplace,
            content_hash_before: String::new(),
            content_hash_after: crate::vault::content_hash(b"body\n"),
            payload_bytes: b"body\n".to_vec(),
        },
    )
    .unwrap();
    std::fs::copy(
        crate::oplog::oplog_path_for_name(cache_dir, &stem),
        crate::oplog::oplog_path_for_name(cache_dir, "copycat"),
    )
    .unwrap();

    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(
        !crate::oplog::oplog_path_for_name(cache_dir, "copycat").exists(),
        "a stale quarantined copy ages out on the remnant rule"
    );
    // The original binding is untouched.
    assert!(crate::oplog::oplog_path_for_name(cache_dir, &stem).exists());
}

#[test]
fn backward_clock_remnant_is_not_reclaimed() {
    // Adversarial-review High: a fresh deletion whose TAIL timestamp
    // looks ancient (the clock stepped backwards before the last save)
    // must NOT be reclaimed — any in-retention timestamp anywhere in
    // the log keeps it.
    let (_tmp, session) = tiny_threshold_session();
    session.scan_initial(&CancelToken::new()).unwrap();
    let cache_dir = &session.config.cache_dir;
    let fresh_ts = now_ms() - 24 * 60 * 60 * 1000; // yesterday
    let regressed_ts = now_ms() - 200 * 24 * 60 * 60 * 1000; // "200 days ago"

    assert!(crate::oplog::try_create_log(cache_dir, "clockstep", "gone/clockstep.md").unwrap());
    let snap = |ts: i64, body: &[u8]| crate::oplog::OpLogEntry {
        timestamp_ms: ts,
        user_actor_id: "t".into(),
        op_kind: crate::OpKind::WholeFileReplace,
        content_hash_before: String::new(),
        content_hash_after: crate::vault::content_hash(body),
        payload_bytes: body.to_vec(),
    };
    crate::oplog::append_entry(
        cache_dir,
        "clockstep",
        "gone/clockstep.md",
        &snap(fresh_ts, b"v1\n"),
    )
    .unwrap();
    // The clock regresses; the LAST save carries an ancient timestamp.
    crate::oplog::append_entry(
        cache_dir,
        "clockstep",
        "gone/clockstep.md",
        &snap(regressed_ts, b"v2\n"),
    )
    .unwrap();

    session.scan_initial(&CancelToken::new()).unwrap();

    assert!(
        crate::oplog::oplog_path_for_name(cache_dir, "clockstep").exists(),
        "a log with ANY in-retention timestamp must survive reclamation"
    );
    assert!(
        session
            .remnant_logs()
            .iter()
            .any(|r| r.stem == "clockstep" && r.effective_path == "gone/clockstep.md"),
        "the fresh-but-clock-stepped deletion must stay recoverable"
    );
}

#[test]
fn entry_count_threshold_triggers_without_byte_threshold() {
    // Adversarial-review Medium: many tiny entries below the byte
    // threshold must still trigger compaction via the session append
    // counter (a sound lower bound on the log's entry count).
    let tmp = tempfile::tempdir().unwrap();
    let cache_dir = tmp.path().join(".slate");
    let mut config = SessionConfig::new(cache_dir);
    config.oplog_compaction_threshold_bytes = u32::MAX; // byte arm off
    config.oplog_compaction_threshold_entries = 6;
    let provider = Arc::new(FsVaultProvider::new(tmp.path().to_path_buf()));
    let session = VaultSession::open(provider, config).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    let mut content = String::new();
    let mut report = session.save_text("tiny.md", "seed\n", None).unwrap();
    content.push_str("seed\n");
    for i in 0..10 {
        content.push_str(&format!("l{i}\n"));
        report = session
            .save_text("tiny.md", &content, Some(&report.new_content_hash))
            .unwrap();
    }
    // 11 appends > threshold 6 → the count trigger fired and the fold
    // (threshold_entries/2 = 3 retained) shrank the entry count.
    assert!(
        wait_for(|| session.read_oplog("tiny.md").unwrap().len() <= 6),
        "entry-count trigger must compact a tiny-entry log; count is {}",
        session.read_oplog("tiny.md").unwrap().len()
    );
    let entries = session.read_oplog("tiny.md").unwrap();
    assert_eq!(
        crate::oplog::reconstruct_at_tail(&entries).unwrap(),
        content
    );
}

#[test]
fn enqueue_during_active_compaction_runs_exactly_one_follow_up() {
    // Adversarial-review Medium: the single-flight claim holds through
    // completion; a trigger landing while a log is claimed flips the
    // dirty bit (never re-queues), and the worker runs exactly one
    // follow-up that clears both. Staged deterministically: the claim
    // is planted by hand and the job is driven through the worker's
    // real channel — no sleep-based racing against the worker.
    let (_tmp, session) = tiny_threshold_session();
    session.scan_initial(&CancelToken::new()).unwrap();

    // A real bound log to compact.
    let mut content = String::new();
    for i in 0..12 {
        content = format!("{content}line {i} with some padding text to bulk the log\n");
        session.save_text("busy.md", &content, None).unwrap();
    }
    let stem = {
        let conn = session.conn.lock().unwrap();
        conn.query_row(
            "SELECT oplog_name FROM files WHERE path = 'busy.md'",
            [],
            |row| row.get::<_, String>(0),
        )
        .unwrap()
    };
    let file_id: i64 = {
        let conn = session.conn.lock().unwrap();
        conn.query_row("SELECT id FROM files WHERE path = 'busy.md'", [], |row| {
            row.get(0)
        })
        .unwrap()
    };
    // Let any save-triggered runs drain first.
    assert!(wait_for(|| {
        session.compaction_queued.lock().unwrap().is_empty()
            && session.compaction_dirty.lock().unwrap().is_empty()
    }));

    // Plant an in-flight claim, then trigger: the enqueue must flip
    // the dirty bit instead of double-queueing.
    session
        .compaction_queued
        .lock()
        .unwrap()
        .insert(stem.clone());
    session.enqueue_compaction(file_id, &stem, "busy.md");
    assert!(
        session.compaction_dirty.lock().unwrap().contains(&stem),
        "a trigger on a claimed log must set the dirty bit"
    );

    // Drive the claimed job through the worker's real channel: it
    // compacts, sees the dirty bit, runs exactly one follow-up, and
    // releases both the claim and the bit.
    session
        .compaction_tx
        .send(CompactionJob::Compact {
            file_id,
            log_name: stem.clone(),
            path: "busy.md".to_string(),
        })
        .unwrap();
    assert!(
        wait_for(|| {
            session.compaction_queued.lock().unwrap().is_empty()
                && session.compaction_dirty.lock().unwrap().is_empty()
        }),
        "the follow-up run must clear the claim and the dirty bit"
    );
    let entries = session.read_oplog("busy.md").unwrap();
    assert_eq!(
        crate::oplog::reconstruct_at_tail(&entries).unwrap(),
        content
    );

    // Second staging (codex re-review): triggers landing DURING the
    // drain's follow-up passes and its release window must never be
    // erased. Every compaction pass stalls under the hold hook while
    // oversized saves keep landing; if any trigger were dropped, the
    // log would stay oversized after quiescence.
    *crate::oplog_compaction::HOLD_LOCK_FOR.lock().unwrap() =
        Some((session.config.cache_dir.clone(), 150));
    for i in 0..6 {
        content = format!("{content}{} filler {i}\n", "y".repeat(2500));
        session.save_text("busy.md", &content, None).unwrap();
        std::thread::sleep(std::time::Duration::from_millis(60));
    }
    *crate::oplog_compaction::HOLD_LOCK_FOR.lock().unwrap() = None;
    assert!(
        wait_for(|| {
            session.compaction_queued.lock().unwrap().is_empty()
                && session.compaction_dirty.lock().unwrap().is_empty()
        }),
        "drain must converge with no orphaned claim or dirty bit"
    );
    // Floor: the log cannot shrink below its own content (the anchor
    // IS the document) plus one retained snapshot — the budget of a
    // fully drained fold. An erased trigger leaves 3+ accumulated
    // snapshots (~3× content or more) and blows this budget.
    let drained_budget = 2 * content.len() as u64 + 4096;
    assert!(
        wait_for(|| log_size(&session, "busy.md") <= drained_budget),
        "an erased mid-drain trigger would leave the log oversized; size is {} (budget {})",
        log_size(&session, "busy.md"),
        drained_budget
    );
    let entries = session.read_oplog("busy.md").unwrap();
    assert_eq!(
        crate::oplog::reconstruct_at_tail(&entries).unwrap(),
        content
    );
}

#[test]
fn session_close_joins_the_worker() {
    // Drop with queued work must not hang: the shutdown flag is
    // checked between files and join completes.
    let (_tmp, session) = tiny_threshold_session();
    session.scan_initial(&CancelToken::new()).unwrap();
    let mut content = String::new();
    for i in 0..12 {
        content = format!("{content}line {i} with some padding text to bulk the log\n");
        session.save_text("hot.md", &content, None).unwrap();
    }
    let started = std::time::Instant::now();
    drop(session);
    assert!(
        started.elapsed() < std::time::Duration::from_secs(10),
        "session drop must join the worker promptly"
    );
}
