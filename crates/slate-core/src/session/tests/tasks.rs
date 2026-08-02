// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `VaultSession` tests — task scan, save_text rewrites, toggle_task_status, tasks_in_vault filters and paging.
//!
//! Extracted from `crates/slate-core/src/session.rs` as part of #272.

#![allow(clippy::too_many_lines)]

use super::common::*;
use super::*;

type TaskSnapshotRow = (u32, char, bool, String, Option<i64>, Option<i32>);

/// One row of `EXPLAIN QUERY PLAN` output, parsed into its column
/// shape rather than collapsed into one substring-matched blob.
/// Used by the two planner-shape tests below — audit #284 flagged
/// the original concatenate-then-substring pattern as brittle to
/// SQLite phrasing tweaks; this structural form keeps the index-
/// name check (which IS stable across SQLite versions) but gives
/// per-row failure messages.
#[derive(Debug, Clone)]
struct PlanRow {
    id: i64,
    parent: i64,
    detail: String,
}

/// Run `EXPLAIN QUERY PLAN <sql>` and collect the per-step rows.
/// The `notused` column (column 2 in SQLite ≥ 3.24) is skipped;
/// `detail` (column 3) is what carries "USING INDEX <name>" and
/// "SCAN <table>" markers the tests assert against.
fn explain_plan_rows(
    conn: &rusqlite::Connection,
    sql: &str,
    params: &[&dyn rusqlite::ToSql],
) -> Vec<PlanRow> {
    let explain = format!("EXPLAIN QUERY PLAN {sql}");
    conn.prepare(&explain)
        .unwrap()
        .query_map(params, |row| {
            Ok(PlanRow {
                id: row.get::<_, i64>(0)?,
                parent: row.get::<_, i64>(1)?,
                detail: row.get::<_, String>(3)?,
            })
        })
        .unwrap()
        .collect::<Result<Vec<_>, _>>()
        .unwrap()
}

/// Render a plan as a multi-line string for assertion failure
/// messages. Each row shows its tree id + parent so the optimizer's
/// step hierarchy is visible — easier to debug a regression than
/// the joined-detail blob the old tests printed.
fn format_plan(rows: &[PlanRow]) -> String {
    rows.iter()
        .map(|row| format!("  [id={}, parent={}] {}", row.id, row.parent, row.detail))
        .collect::<Vec<_>>()
        .join("\n")
}

fn tasks_snapshot(session: &VaultSession, path: &str) -> Vec<TaskSnapshotRow> {
    session
        .tasks_for_file(path)
        .unwrap()
        .into_iter()
        .map(|t| {
            (
                t.ordinal,
                t.status_char,
                t.completed,
                t.text,
                t.due_ms,
                t.priority,
            )
        })
        .collect()
}

#[test]
fn scan_populates_tasks_table_from_markdown_body() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "notes/todos.md",
            b"# To do\n\n- [ ] open\n- [x] done\n- [/] doing\n",
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let tasks = session.tasks_for_file("notes/todos.md").unwrap();
    assert_eq!(tasks.len(), 3);
    assert_eq!(tasks[0].status_char, ' ');
    assert_eq!(tasks[1].status_char, 'x');
    assert!(tasks[1].completed);
    assert_eq!(tasks[2].status_char, '/');
    assert!(!tasks[2].completed);
}

#[test]
fn note_tasks_bounds_rows_in_sql_and_reports_true_totals() {
    // W4-3: the panel's bounded twin of tasks_for_file — a
    // task-dense note must never materialize an unbounded Vec, and
    // the header's "N open of M tasks" needs true counts past the
    // display cap.
    let mut body = String::from("# Dense\n\n");
    for i in 0..30 {
        let status = if i % 3 == 0 { 'x' } else { ' ' };
        body.push_str(&format!("- [{status}] task {i}\n"));
    }
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/dense.md", body.as_bytes()).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let page = session.note_tasks("notes/dense.md", 12).unwrap();
    assert_eq!(page.tasks.len(), 12);
    assert_eq!(page.total, 30);
    assert_eq!(page.open_total, 20);
    // OPEN first (the display budget goes to actionable rows), each
    // group in document order: the first open task is "task 1"
    // (task 0 is completed).
    assert!(page.tasks.iter().all(|t| !t.completed));
    assert_eq!(page.tasks[0].text, "task 1");
    assert_eq!(page.tasks[1].text, "task 2");
    // The snapshot hash rides along (round 2) and matches the save
    // path's conflict-check hash, so panel rows can prove their
    // ordinals still name the same tasks before a toggle.
    assert!(!page.content_hash.is_empty());
    assert_eq!(
        page.content_hash,
        session
            .read_note_parts("notes/dense.md")
            .unwrap()
            .content_hash
    );

    // Unknown paths are an empty page, not an error.
    let missing = session.note_tasks("notes/none.md", 12).unwrap();
    assert!(missing.tasks.is_empty());
    assert_eq!(missing.total, 0);
    assert_eq!(missing.open_total, 0);
    assert!(missing.content_hash.is_empty());
}

#[test]
fn vault_pages_bound_task_text_before_ffi() {
    // Adversarial round 4: LIMIT bounds row COUNT, but a valid task
    // line can approach the per-file byte ceiling, and a vault-wide
    // page spans many files — exact rows would marshal the whole
    // text through SQLite → Rust → FFI → host. text/recurrence get
    // the links-panel snippet bound; identity fields stay exact.
    let huge = "x".repeat(64 * 1024);
    let body = format!("- [ ] {huge} \u{1F501} every {huge}\n");
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/huge.md", body.as_bytes()).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let page = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(10))
        .unwrap();
    let row = &page.items[0];
    let ceiling = 4096 + '…'.len_utf8();
    assert!(row.task.text.len() <= ceiling);
    // (No ellipsis assert: SQL clips CHARS first, so an ASCII text
    // arrives at the byte bound already inside it.)
    assert!(row.task.text.starts_with("xxxx"));
    if let Some(recurrence) = row.task.recurrence.as_ref() {
        assert!(recurrence.len() <= ceiling);
    }
    assert_eq!(row.task.ordinal, 0);
    assert!(!row.content_hash.is_empty());

    // The per-NOTE read keeps the panel's exact record: a single
    // note's tasks are substrings of one file, so its exposure is
    // the file ceiling, not a vault-wide multiplier.
    let note = session.note_tasks("notes/huge.md", 10).unwrap();
    assert!(note.tasks[0].text.len() >= 64 * 1024);
}

#[test]
fn post_write_failures_leave_disk_newer_and_reindex_path_repairs_it() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 12: the REAL partial-failure boundary —
    // save_text writes the file, then the index commit fails. The
    // fault seam trips only on a path containing the trigger, so
    // parallel tests' saves are unaffected.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/fault-target-a1b2.md", b"- [ ] flip me\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // SAFETY: env vars are process-global; the trigger string is
    // unique to this test's fixture path.
    unsafe { std::env::set_var("SLATE_TEST_FAULT_AFTER_WRITE", "fault-target-a1b2") };
    let result = session.toggle_task_status("notes/fault-target-a1b2.md", 0, 'x', None);
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_AFTER_WRITE") };
    assert!(result.is_err(), "the injected fault must surface");

    // Disk moved; the index did not.
    let disk = session
        .read_note_parts("notes/fault-target-a1b2.md")
        .unwrap();
    assert!(disk.body.contains("- [x] flip me"));
    let stale = session
        .note_tasks("notes/fault-target-a1b2.md", 10)
        .unwrap();
    assert!(
        !stale.tasks[0].completed,
        "the index must still be pre-write"
    );
    assert_ne!(stale.content_hash, disk.content_hash);

    // Pin the row's stat tuple to DISK's current values — the
    // coarse-mtime world (adversarial round 13): a checkbox toggle
    // preserves size, and a filesystem can preserve mtime/ctime, so
    // index_file's fast path would consider the row current while
    // its CONTENT columns hold the rolled-back state. The repair
    // must bypass the fast path and converge anyway.
    {
        let stat = session.provider.stat("notes/fault-target-a1b2.md").unwrap();
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE files SET mtime_ms = ?1, size_bytes = ?2, ctime_ms = ?3 WHERE path = ?4",
            rusqlite::params![
                stat.mtime_ms,
                i64::try_from(stat.size_bytes).unwrap(),
                stat.ctime_ms,
                "notes/fault-target-a1b2.md"
            ],
        )
        .unwrap();
    }

    // The repair forces the path back to disk truth.
    session.reindex_path("notes/fault-target-a1b2.md").unwrap();
    let repaired = session
        .note_tasks("notes/fault-target-a1b2.md", 10)
        .unwrap();
    assert!(repaired.tasks[0].completed);
    assert_eq!(repaired.content_hash, disk.content_hash);
}

#[test]
fn tasks_index_revision_sees_other_sessions_where_the_generation_cannot() {
    // Adversarial round 14: the session-local generation cannot see
    // another PROCESS committing to the shared cache DB — a paging
    // cursor could read a mutated index with both drift checks still
    // equal. A second session on the same vault stands in for the
    // second process (same shared DB, different connection).
    let (tmp, session_a) = make_vault(|p| {
        p.write_file("notes/shared.md", b"- [ ] cross-process\n")
            .unwrap();
    });
    session_a.scan_initial(&CancelToken::new()).unwrap();

    let generation_before = session_a.interaction_generation();
    let revision_before = session_a.tasks_index_revision();

    let session_b = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session_b
        .save_text("notes/shared.md", "- [x] cross-process\n", None)
        .unwrap();

    // The gap: A's generation never moved…
    assert_eq!(generation_before, session_a.interaction_generation());
    // …but the revision did, so drift checks built on it stay honest.
    assert_ne!(revision_before, session_a.tasks_index_revision());
}

#[test]
fn cross_process_post_write_rollbacks_self_heal_on_other_sessions() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 21: process A's post-write rollback moves no
    // committed state process B could observe (`data_version` only
    // advances on commits), so B's task queries would publish A's
    // ghost rows forever. The durable write intent — committed
    // BEFORE A's file write, surviving its rollback — marks the path
    // suspect for every process; abandoned intents self-heal via a
    // single-file reindex inside the query paths. A second session
    // on the same vault stands in for the second process.
    let (tmp, session_a) = make_vault(|p| {
        p.write_file("notes/xproc-intent.md", b"- [ ] flip me\n")
            .unwrap();
    });
    session_a.scan_initial(&CancelToken::new()).unwrap();
    let session_b = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();

    // A fails post-write: disk flipped, A's index tx rolled back,
    // the intent row (committed pre-write) survives.
    unsafe { std::env::set_var("SLATE_TEST_FAULT_AFTER_WRITE", "xproc-intent") };
    let result = session_a.toggle_task_status("notes/xproc-intent.md", 0, 'x', None);
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_AFTER_WRITE") };
    assert!(result.is_err());

    // Fresh intent (within the liveness threshold): B reads the old
    // but CONSISTENT snapshot — ordinary transient staleness, not an
    // error.
    let fresh = session_b.note_tasks("notes/xproc-intent.md", 10).unwrap();
    assert!(!fresh.tasks[0].completed);

    // Abandoned (threshold collapsed for this path only): BOTH of
    // B's query surfaces self-heal to disk truth.
    unsafe { std::env::set_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR", "xproc-intent") };
    let healed = session_b.note_tasks("notes/xproc-intent.md", 10).unwrap();
    let page = session_b
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .unwrap();
    unsafe { std::env::remove_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR") };
    assert!(
        healed.tasks[0].completed,
        "note_tasks must heal to disk truth"
    );
    let row = page
        .items
        .iter()
        .find(|r| r.path == "notes/xproc-intent.md")
        .expect("the healed task row");
    assert!(row.task.completed, "tasks_in_vault must heal to disk truth");

    // The heal cleared the intent: later queries take the fast path.
    let settled = session_b.note_tasks("notes/xproc-intent.md", 10).unwrap();
    assert!(settled.tasks[0].completed);
}

#[test]
fn stale_repairs_never_erase_a_replacement_writers_intent() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 22: a sweep selects an abandoned intent, a
    // NEW writer replaces the row before the repair transaction, and
    // the repair's clear must miss — deleting by path alone would
    // erase the only durable marker covering the new writer's own
    // post-write rollback. The stale sweep is simulated by calling
    // the repair with the OLD token after the replacement landed.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/token-race.md", b"- [ ] flip me\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Writer 1 fails post-write: intent T1 survives, index stale.
    unsafe { std::env::set_var("SLATE_TEST_FAULT_AFTER_WRITE", "token-race") };
    assert!(
        session
            .toggle_task_status("notes/token-race.md", 0, 'x', None)
            .is_err()
    );
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_AFTER_WRITE") };
    let old_token: i64 = {
        let conn = session.conn.lock().unwrap();
        conn.query_row(
            "SELECT token FROM text_write_intents WHERE path = ?1",
            rusqlite::params!["notes/token-race.md"],
            |row| row.get(0),
        )
        .unwrap()
    };

    // A NEW writer's registration replaces the row (fresh token).
    let new_token = old_token.wrapping_add(1);
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "INSERT OR REPLACE INTO text_write_intents(path, created_ms, token) VALUES (?1, ?2, ?3)",
            rusqlite::params!["notes/token-race.md", now_ms(), new_token],
        )
        .unwrap();
    }

    // The stale sweep repairs with the OLD token: the index heals,
    // and the replacement intent SURVIVES.
    {
        let mut conn = session.conn.lock().unwrap();
        session
            .reindex_path_locked(&mut conn, "notes/token-race.md", &[old_token])
            .unwrap();
        let surviving: i64 = conn
            .query_row(
                "SELECT token FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/token-race.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(surviving, new_token, "the fresh registration must survive");
    }

    // The host-facing repair route never clears intents at all.
    session.reindex_path("notes/token-race.md").unwrap();
    {
        let conn = session.conn.lock().unwrap();
        let count: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/token-race.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(count, 1, "host repairs must not clear intents");
    }

    // The replacement writer rolls back too (simulated: its intent
    // simply ages out with the index already healed): a third
    // session's query token-safely clears it and reads disk truth.
    let third = VaultSession::from_filesystem(_tmp.path().to_path_buf()).unwrap();
    unsafe { std::env::set_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR", "token-race") };
    let healed = third.note_tasks("notes/token-race.md", 10).unwrap();
    unsafe { std::env::remove_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR") };
    assert!(healed.tasks[0].completed, "third session reads disk truth");
    {
        let conn = session.conn.lock().unwrap();
        let count: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/token-race.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(count, 0, "the aged replacement clears token-safely");
    }
}

#[test]
fn deleted_file_intents_converge_instead_of_poisoning_queries() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 23: a conflict refusal clears its own intent
    // (provable no-write), and an intent stranded against a DELETED
    // file converges to deletion — the aged repair used to hit
    // NotFound forever and fail every vault-wide task query.
    let (tmp, session) = make_vault(|p| {
        p.write_file("notes/doomed-intent.md", b"- [ ] flip me\n")
            .unwrap();
        p.write_file("notes/bystander.md", b"- [ ] safe\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Phase 1: a stale-hash save against an externally deleted file
    // refuses with a conflict AND clears its own registration.
    std::fs::remove_file(tmp.path().join("notes/doomed-intent.md")).unwrap();
    let conflicted = session.save_text("notes/doomed-intent.md", "- [x] flip me\n", Some("stale"));
    assert!(matches!(conflicted, Err(VaultError::WriteConflict { .. })));
    {
        let conn = session.conn.lock().unwrap();
        let count: i64 = conn
            .query_row("SELECT COUNT(*) FROM text_write_intents", [], |row| {
                row.get(0)
            })
            .unwrap();
        assert_eq!(count, 0, "a provable no-write must clear its intent");
    }

    // Phase 2: an intent stranded by a post-write failure whose file
    // is THEN deleted — the aged sweep must converge to deletion
    // (index row gone, marker cleared) instead of erroring forever.
    session
        .save_text("notes/doomed-intent.md", "- [ ] back again\n", None)
        .unwrap();
    unsafe { std::env::set_var("SLATE_TEST_FAULT_AFTER_WRITE", "doomed-intent") };
    assert!(
        session
            .toggle_task_status("notes/doomed-intent.md", 0, 'x', None)
            .is_err()
    );
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_AFTER_WRITE") };
    std::fs::remove_file(tmp.path().join("notes/doomed-intent.md")).unwrap();

    // The convergence must announce as DELETED (adversarial round
    // 24): a Modified event leaves hosts presenting the note as
    // existing — an open tab never marked missing, a ghost Quick
    // Switcher entry — while only the task lists heal.
    struct KindRecorder(std::sync::Mutex<Vec<(FileChangeKind, String)>>);
    impl VaultEventListener for KindRecorder {
        fn on_error(&self, _c: EventErrorCode, _p: String, _m: String) {}
        fn on_file_change(&self, event: FileChangeEvent) {
            self.0.lock().unwrap().push((event.kind, event.path));
        }
    }
    let recorder = std::sync::Arc::new(KindRecorder(std::sync::Mutex::new(Vec::new())));
    let listener_token = session.register_event_listener(recorder.clone());

    unsafe { std::env::set_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR", "doomed-intent") };
    let page = session.tasks_in_vault(crate::TaskFilter::default(), Paging::first(50));
    unsafe { std::env::remove_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR") };
    session.unregister_event_listener(listener_token);
    assert!(
        recorder.0.lock().unwrap().iter().any(
            |(kind, path)| *kind == FileChangeKind::Deleted && path == "notes/doomed-intent.md"
        ),
        "convergence to deletion must announce as Deleted"
    );

    let page = page.expect("the vault-wide query must succeed after convergence");
    assert!(
        page.items
            .iter()
            .all(|r| r.path != "notes/doomed-intent.md"),
        "the deleted file's ghost rows are gone"
    );
    assert!(
        page.items.iter().any(|r| r.path == "notes/bystander.md"),
        "unrelated tasks still serve"
    );
    {
        let conn = session.conn.lock().unwrap();
        let intents: i64 = conn
            .query_row("SELECT COUNT(*) FROM text_write_intents", [], |row| {
                row.get(0)
            })
            .unwrap();
        assert_eq!(intents, 0, "the stranded marker converged away");
        let files: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM files WHERE path = 'notes/doomed-intent.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(files, 0, "the deleted file's index row converged away");
    }
}

#[test]
fn overlapping_writers_keep_their_own_durable_registrations() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 25: with `path` as the sole primary key, a
    // second writer's registration REPLACED the first's — the second
    // completed, cleared its own token, and the first writer's
    // post-write failure went unmarked forever. Writer A's paused
    // registration is staged directly (the gap between a writer's
    // autocommit insert and its transaction begin cannot be paused
    // from a test); writer B is a real full save on the same path.
    let (tmp, session_a) = make_vault(|p| {
        p.write_file("notes/overlap.md", b"- [ ] flip me\n")
            .unwrap();
    });
    session_a.scan_initial(&CancelToken::new()).unwrap();

    // Writer A: registered, paused before its transaction.
    let token_a: i64 = 0x5EED;
    {
        let conn = session_a.conn.lock().unwrap();
        conn.execute(
            "INSERT OR REPLACE INTO text_write_intents(path, created_ms, token) VALUES (?1, ?2, ?3)",
            rusqlite::params!["notes/overlap.md", now_ms(), token_a],
        )
        .unwrap();
    }

    // Writer B (a second session): a full successful save of the
    // same path. Its completion clears only ITS registration.
    let session_b = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session_b
        .save_text("notes/overlap.md", "- [x] flip me\n", None)
        .unwrap();

    // A's registration survives B's entire lifecycle.
    {
        let conn = session_a.conn.lock().unwrap();
        let count: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1 AND token = ?2",
                rusqlite::params!["notes/overlap.md", token_a],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(count, 1, "writer A's registration must survive writer B");
    }

    // A's write then fails post-write: with the marker intact, a
    // later query heals to disk truth and clears it token-safely.
    // (The simulated A resumes as a real fault-injected save.)
    unsafe { std::env::set_var("SLATE_TEST_FAULT_AFTER_WRITE", "overlap") };
    assert!(
        session_a
            .save_text("notes/overlap.md", "- [/] half flipped\n", None)
            .is_err()
    );
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_AFTER_WRITE") };

    unsafe { std::env::set_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR", "overlap") };
    let healed = session_b.note_tasks("notes/overlap.md", 10).unwrap();
    unsafe { std::env::remove_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR") };
    assert_eq!(healed.tasks[0].status_char, '/', "healed to A's disk bytes");
    {
        let conn = session_a.conn.lock().unwrap();
        let count: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/overlap.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(count, 0, "every abandoned registration cleared");
    }
}

#[test]
fn swept_registrations_are_refenced_before_the_write() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 26: a writer suspended past the liveness
    // threshold between its registration and its transaction can be
    // swept as abandoned — resuming to write with no durable marker
    // reopens the unmarked-failure window. The seam deletes the
    // registration exactly in that gap; the fence must re-register
    // before the write, so the subsequent post-write failure still
    // leaves a marker and another session heals to disk truth.
    let (tmp, session) = make_vault(|p| {
        p.write_file("notes/fence-probe.md", b"- [ ] flip me\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    unsafe { std::env::set_var("SLATE_TEST_SWEEP_AFTER_INTENT", "fence-probe") };
    unsafe { std::env::set_var("SLATE_TEST_FAULT_AFTER_WRITE", "fence-probe") };
    let result = session.save_text("notes/fence-probe.md", "- [x] flip me\n", None);
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_AFTER_WRITE") };
    unsafe { std::env::remove_var("SLATE_TEST_SWEEP_AFTER_INTENT") };
    assert!(result.is_err());

    // The fence's re-registration survived the rollback — without
    // it, the swept marker stayed gone and the failure was unmarked.
    {
        let conn = session.conn.lock().unwrap();
        let count: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/fence-probe.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(count, 1, "the re-fenced registration must survive");
    }

    // Another session heals to the written bytes.
    let other = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    unsafe { std::env::set_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR", "fence-probe") };
    let healed = other.note_tasks("notes/fence-probe.md", 10).unwrap();
    unsafe { std::env::remove_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR") };
    assert!(
        healed.tasks[0].completed,
        "healed to the fenced write's bytes"
    );
}

#[test]
fn pre_write_failures_clear_their_own_registration() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 27: any error between the durable
    // registration and the file write is a PROVABLE no-write, so it
    // must clear its own token. A stranded pre-write intent on a
    // persistently unreadable file would otherwise fail its aged
    // repair BEFORE token clearing on every vault-wide task query —
    // a permanent Tasks Review outage over a marker that never
    // marked anything.
    let (tmp, session) = make_vault(|p| {
        p.write_file("notes/prewrite-probe.md", b"- [ ] keep me\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let current_hash = session
        .note_tasks("notes/prewrite-probe.md", 1)
        .unwrap()
        .content_hash;

    unsafe { std::env::set_var("SLATE_TEST_FAULT_PRE_WRITE", "prewrite-probe") };
    let result = session.save_text(
        "notes/prewrite-probe.md",
        "- [x] keep me\n",
        Some(&current_hash),
    );
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_PRE_WRITE") };
    assert!(result.is_err());

    // The provable no-write cleared its own registration.
    {
        let conn = session.conn.lock().unwrap();
        let count: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/prewrite-probe.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(
            count, 0,
            "a provable pre-write failure must clear its own registration"
        );
    }

    // The outage vector itself: were an intent stranded here AND the
    // path's repair failing (the reindex fault stands in for a
    // persistently unreadable file), every vault-wide query would
    // error before token clearing — forever. With the cleanup there
    // is nothing to repair, so queries stay available even with the
    // repair path broken.
    let other = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    unsafe { std::env::set_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR", "prewrite-probe") };
    unsafe { std::env::set_var("SLATE_TEST_FAULT_REINDEX", "prewrite-probe") };
    let page = other.tasks_in_vault(crate::TaskFilter::default(), Paging::first(50));
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_REINDEX") };
    unsafe { std::env::remove_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR") };
    let page = page.expect("vault-wide task queries stay available");
    let row = page
        .items
        .iter()
        .find(|r| r.path == "notes/prewrite-probe.md")
        .expect("the untouched file's tasks still serve");
    assert!(
        !row.task.completed,
        "disk truth: the failed save never wrote"
    );
}

#[test]
fn unclearable_markers_quarantine_instead_of_poisoning_queries() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 28: the pre-write cleanup DELETE can itself
    // fail (BUSY from another process's writer lock, database I/O) —
    // the same stranded-marker posture as a crash. If the stranded
    // path also stays unreadable, its aged repair fails before token
    // clearing on EVERY vault-wide query. The sweep must quarantine
    // the path (suspect rows stop serving, marker stays, queries
    // stay available) and heal in full once the file reads again.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/stuck-marker.md", b"- [ ] trapped\n")
            .unwrap();
        p.write_file("notes/bystander28.md", b"- [ ] serving\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let current_hash = session
        .note_tasks("notes/stuck-marker.md", 1)
        .unwrap()
        .content_hash;

    let intent_count = |session: &VaultSession| -> i64 {
        let conn = session.conn.lock().unwrap();
        conn.query_row(
            "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
            rusqlite::params!["notes/stuck-marker.md"],
            |row| row.get(0),
        )
        .unwrap()
    };

    // A pre-write failure whose cleanup ALSO fails: the marker strands.
    unsafe { std::env::set_var("SLATE_TEST_FAULT_PRE_WRITE", "stuck-marker") };
    unsafe { std::env::set_var("SLATE_TEST_FAULT_INTENT_CLEAR", "stuck-marker") };
    let result = session.save_text(
        "notes/stuck-marker.md",
        "- [x] trapped\n",
        Some(&current_hash),
    );
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_INTENT_CLEAR") };
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_PRE_WRITE") };
    assert!(result.is_err());
    assert_eq!(intent_count(&session), 1, "the marker stranded");

    // Count Modified announcements: the quarantine must fire ONCE,
    // not once per query — repeated failing sweeps would otherwise
    // storm hosts with refresh events.
    struct KindRecorder(std::sync::Mutex<Vec<(FileChangeKind, String)>>);
    impl VaultEventListener for KindRecorder {
        fn on_error(&self, _c: EventErrorCode, _p: String, _m: String) {}
        fn on_file_change(&self, event: FileChangeEvent) {
            self.0.lock().unwrap().push((event.kind, event.path));
        }
    }
    let recorder = std::sync::Arc::new(KindRecorder(std::sync::Mutex::new(Vec::new())));
    let listener_token = session.register_event_listener(recorder.clone());

    // The marker ages while the path stays unreadable (the reindex
    // fault stands in for permissions / an AV lock).
    unsafe { std::env::set_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR", "stuck-marker") };
    unsafe { std::env::set_var("SLATE_TEST_FAULT_REINDEX", "stuck-marker") };
    for _ in 0..2 {
        let page = session
            .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
            .expect("vault-wide queries stay available under an un-repairable marker");
        assert!(
            page.items.iter().all(|r| r.path != "notes/stuck-marker.md"),
            "the quarantined path's suspect rows must not serve"
        );
        assert!(
            page.items.iter().any(|r| r.path == "notes/bystander28.md"),
            "unrelated tasks still serve"
        );
    }
    let honest_empty = session
        .note_tasks("notes/stuck-marker.md", 10)
        .expect("per-file queries stay available too");
    assert!(
        honest_empty.tasks.is_empty(),
        "the honest surface for an unreadable file is task-free"
    );
    assert_eq!(
        intent_count(&session),
        1,
        "suspicion persists until a repair actually lands"
    );
    assert_eq!(
        recorder
            .0
            .lock()
            .unwrap()
            .iter()
            .filter(
                |(kind, path)| *kind == FileChangeKind::Modified && path == "notes/stuck-marker.md"
            )
            .count(),
        1,
        "the quarantine announces once, not once per query"
    );
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_REINDEX") };

    // The file reads again: the next query's sweep repairs in full —
    // rows rebuilt to disk truth, marker cleared.
    let healed = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .expect("the healed query succeeds");
    unsafe { std::env::remove_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR") };
    session.unregister_event_listener(listener_token);
    let row = healed
        .items
        .iter()
        .find(|r| r.path == "notes/stuck-marker.md")
        .expect("the repaired path's tasks return");
    assert!(
        !row.task.completed,
        "disk truth: the failed save never wrote"
    );
    assert_eq!(
        intent_count(&session),
        0,
        "the successful repair clears the marker"
    );
}

#[test]
fn resolved_candidates_skip_the_quarantine() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 29: the sweep selects its candidates BEFORE
    // taking the writer lock. A live writer can commit its index
    // update and clear its own aged token in that gap — its fresh
    // rows are the consistent truth. A quarantine that doesn't
    // revalidate ownership would delete those fresh rows and leave
    // NO marker behind, so nothing would ever repair them: honest
    // truth silently replaced by permanent empty.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/resolved-race.md", b"- [ ] fresh truth\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "INSERT OR REPLACE INTO text_write_intents(path, created_ms, token) VALUES (?1, ?2, ?3)",
            rusqlite::params!["notes/resolved-race.md", now_ms(), 0xACEi64],
        )
        .unwrap();
    }

    // The marker ages, the repair fails (unreadable file), and the
    // 'writer' resolves — clears its token — just before the
    // quarantine takes the lock (the resolve seam).
    unsafe { std::env::set_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR", "resolved-race") };
    unsafe { std::env::set_var("SLATE_TEST_FAULT_REINDEX", "resolved-race") };
    unsafe { std::env::set_var("SLATE_TEST_RESOLVE_BEFORE_QUARANTINE", "resolved-race") };
    let page = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .expect("the query still succeeds");
    unsafe { std::env::remove_var("SLATE_TEST_RESOLVE_BEFORE_QUARANTINE") };
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_REINDEX") };
    unsafe { std::env::remove_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR") };
    assert!(
        page.items
            .iter()
            .any(|r| r.path == "notes/resolved-race.md"),
        "a resolved candidate's fresh rows keep serving — the quarantine must skip"
    );
}

#[test]
fn newer_writers_commits_survive_a_stale_quarantine() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial rounds 30-31: a NEWER writer commits fresh rows in
    // the repair→quarantine gap, clearing only its OWN token — the
    // aged selected token survives and would vouch for quarantining
    // rows that are now the consistent truth. Supersession is
    // durable: the writer's commit bumped the path's index_epoch, so
    // the quarantine — re-deriving under its writer lock — must
    // delete the obsolete marker WITHOUT containment and keep the
    // fresh rows serving. No sampled baseline is involved, so there
    // is no window (before selection, between retries) where the
    // commit can hide.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/second-writer.md", b"- [ ] fresh truth\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    {
        let conn = session.conn.lock().unwrap();
        // Staged at the CURRENT epoch — a live marker, not
        // pre-obsolete; only the seam's simulated newer-writer
        // commit supersedes it.
        conn.execute(
            "INSERT OR REPLACE INTO text_write_intents(path, created_ms, token, registered_epoch)
             VALUES (?1, ?2, ?3, COALESCE((SELECT index_epoch FROM files WHERE path = ?1), 0))",
            rusqlite::params!["notes/second-writer.md", now_ms(), 0xA6EDi64],
        )
        .unwrap();
    }

    let db_state = |session: &VaultSession| -> (i64, i64) {
        let conn = session.conn.lock().unwrap();
        let tasks: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM tasks t JOIN files f ON f.id = t.file_id WHERE f.path = ?1",
                rusqlite::params!["notes/second-writer.md"],
                |row| row.get(0),
            )
            .unwrap();
        let intents: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/second-writer.md"],
                |row| row.get(0),
            )
            .unwrap();
        (tasks, intents)
    };

    unsafe { std::env::set_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR", "second-writer") };
    unsafe { std::env::set_var("SLATE_TEST_FAULT_REINDEX", "second-writer") };
    unsafe {
        std::env::set_var(
            "SLATE_TEST_SECOND_WRITER_BEFORE_QUARANTINE",
            "second-writer",
        )
    };
    let page = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .expect("the query succeeds — the superseded marker resolves, nothing contains");
    unsafe { std::env::remove_var("SLATE_TEST_SECOND_WRITER_BEFORE_QUARANTINE") };
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_REINDEX") };
    assert!(
        page.items
            .iter()
            .any(|r| r.path == "notes/second-writer.md"),
        "the newer writer's rows keep serving through the vault surface"
    );
    assert_eq!(
        db_state(&session),
        (1, 0),
        "the fresh rows survive untouched and the obsolete marker is deleted"
    );

    // The per-file surface agrees, and everything stays healthy once
    // the faults are gone.
    let per_file = session
        .note_tasks("notes/second-writer.md", 10)
        .expect("the per-file surface serves too");
    unsafe { std::env::remove_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR") };
    assert!(
        !per_file.tasks.is_empty() && !per_file.tasks[0].completed,
        "the note surface serves the fresh rows"
    );
}

#[test]
fn database_failures_during_repair_still_fail_queries() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 29: honest-empty containment is for FILE
    // conditions. A DATABASE failure during repair — a corrupt FTS
    // trigger, a broken cache — must keep failing queries loud;
    // absorbing it would silently drop the path's tasks over an
    // error the file never caused.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/db-broken.md", b"- [ ] survive me\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "INSERT OR REPLACE INTO text_write_intents(path, created_ms, token) VALUES (?1, ?2, ?3)",
            rusqlite::params!["notes/db-broken.md", now_ms(), 0xD8i64],
        )
        .unwrap();
    }

    unsafe { std::env::set_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR", "db-broken") };
    unsafe { std::env::set_var("SLATE_TEST_FAULT_REINDEX_DB", "db-broken") };
    let failed = session.tasks_in_vault(crate::TaskFilter::default(), Paging::first(50));
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_REINDEX_DB") };
    assert!(
        matches!(failed, Err(VaultError::Db(_))),
        "database failures propagate instead of quarantining"
    );
    {
        let conn = session.conn.lock().unwrap();
        let tasks: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM tasks t JOIN files f ON f.id = t.file_id WHERE f.path = ?1",
                rusqlite::params!["notes/db-broken.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(tasks, 1, "no quarantine ran: the task rows survive");
        let intents: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/db-broken.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(intents, 1, "the marker survives too");
    }

    // The database recovers: the same sweep now repairs in full.
    let healed = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .expect("the healed query succeeds");
    unsafe { std::env::remove_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR") };
    assert!(
        healed.items.iter().any(|r| r.path == "notes/db-broken.md"),
        "the repaired path serves its tasks"
    );
    {
        let conn = session.conn.lock().unwrap();
        let intents: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/db-broken.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(intents, 0, "the successful repair clears the marker");
    }
}

#[test]
fn repair_or_contain_releases_a_permanently_unreadable_path() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 31 (host route): the Windows repair
    // coordinator bars EVERY task query while a pending path
    // exists, so a persistently unreadable file must resolve to an
    // outcome the host can release. Contained = suspect rows
    // dropped honest-empty + an already-aged self-healing marker
    // planted, so queries are immediately safe in every process and
    // the path heals in full the moment the file reads again.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/host-stuck.md", b"- [ ] locked away\n")
            .unwrap();
        p.write_file("notes/host-bystander.md", b"- [ ] serving\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Healthy path: plain repair.
    assert_eq!(
        session
            .repair_or_contain_path("notes/host-stuck.md")
            .unwrap(),
        TaskIndexRepairOutcome::Repaired,
    );

    // Persistent file condition: containment.
    unsafe { std::env::set_var("SLATE_TEST_FAULT_REINDEX", "host-stuck") };
    assert_eq!(
        session
            .repair_or_contain_path("notes/host-stuck.md")
            .unwrap(),
        TaskIndexRepairOutcome::Contained,
    );
    {
        let conn = session.conn.lock().unwrap();
        let (tasks, intents): (i64, i64) = (
            conn.query_row(
                "SELECT COUNT(*) FROM tasks t JOIN files f ON f.id = t.file_id WHERE f.path = ?1",
                rusqlite::params!["notes/host-stuck.md"],
                |row| row.get(0),
            )
            .unwrap(),
            conn.query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/host-stuck.md"],
                |row| row.get(0),
            )
            .unwrap(),
        );
        assert_eq!(tasks, 0, "suspect rows dropped honest-empty");
        assert_eq!(intents, 1, "a self-healing marker was planted");
    }

    // Queries are safe DURING the outage — the planted marker's
    // sweep re-fails the repair, re-contains quietly, and serves
    // everything else.
    let during = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .expect("queries stay available while the file is unreadable");
    assert!(
        during.items.iter().all(|r| r.path != "notes/host-stuck.md"),
        "the contained path serves nothing"
    );
    assert!(
        during
            .items
            .iter()
            .any(|r| r.path == "notes/host-bystander.md"),
        "unrelated tasks still serve"
    );

    // A DATABASE failure propagates instead of containing.
    unsafe { std::env::set_var("SLATE_TEST_FAULT_REINDEX_DB", "host-stuck") };
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_REINDEX") };
    assert!(
        matches!(
            session.repair_or_contain_path("notes/host-stuck.md"),
            Err(VaultError::Db(_))
        ),
        "database failures stay loud on the host route"
    );
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_REINDEX_DB") };

    // The file reads again: the planted marker heals everything on
    // the next query.
    let healed = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .expect("the healed query succeeds");
    assert!(
        healed.items.iter().any(|r| r.path == "notes/host-stuck.md"),
        "the contained path's tasks return"
    );
    {
        let conn = session.conn.lock().unwrap();
        let intents: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/host-stuck.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(intents, 0, "the planted marker cleared with the repair");
    }
}

#[test]
fn commits_in_the_registration_gap_never_orphan_the_marker() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 32: writer A registers (stamping epoch E),
    // writer B wins the lock and fully indexes the OLD bytes
    // (epoch E+1), then A writes the NEW bytes and fails before its
    // index commit. A's marker stamped E would read as
    // provably-superseded (E+1 > E) and be deleted — leaving B's
    // stale rows serving with no repair trigger. The fence must
    // re-stamp the marker to the current epoch under the writer
    // lock before the write, so the post-write failure's marker
    // stays CURRENT and containment (not silent staleness) wins.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/gap-commit.md", b"- [ ] old truth\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    unsafe { std::env::set_var("SLATE_TEST_COMMIT_AFTER_INTENT", "gap-commit") };
    unsafe { std::env::set_var("SLATE_TEST_FAULT_AFTER_WRITE", "gap-commit") };
    let result = session.save_text("notes/gap-commit.md", "- [x] old truth\n", None);
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_AFTER_WRITE") };
    unsafe { std::env::remove_var("SLATE_TEST_COMMIT_AFTER_INTENT") };
    assert!(result.is_err());

    // The surviving marker is stamped AT the file's current epoch —
    // the fence re-registered past the gap commit.
    {
        let conn = session.conn.lock().unwrap();
        let (stamped, current): (i64, i64) = conn
            .query_row(
                "SELECT i.registered_epoch, f.index_epoch
                 FROM text_write_intents i JOIN files f ON f.path = i.path
                 WHERE i.path = ?1",
                rusqlite::params!["notes/gap-commit.md"],
                |row| Ok((row.get(0)?, row.get(1)?)),
            )
            .unwrap();
        assert_eq!(
            stamped, current,
            "the fence must re-stamp the marker to the epoch at the write"
        );
    }

    // Un-repairable: the CURRENT marker contains (honest-empty) —
    // the stale pre-write rows must not serve.
    unsafe { std::env::set_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR", "gap-commit") };
    unsafe { std::env::set_var("SLATE_TEST_FAULT_REINDEX", "gap-commit") };
    let page = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .expect("queries stay available");
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_REINDEX") };
    assert!(
        page.items.iter().all(|r| r.path != "notes/gap-commit.md"),
        "the stale rows must not serve under a live current marker"
    );

    // Readable again: heals to the written bytes.
    let healed = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .expect("the healed query succeeds");
    unsafe { std::env::remove_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR") };
    let row = healed
        .items
        .iter()
        .find(|r| r.path == "notes/gap-commit.md")
        .expect("the repaired path serves");
    assert!(row.task.completed, "healed to the fenced write's bytes");
}

#[test]
fn recreated_files_supersede_prior_incarnation_markers() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 32: a per-file epoch counter resets on
    // delete/recreate, so a prior incarnation's marker could
    // compare at-or-above the recreation's epoch (ABA) and vouch
    // for quarantining the replacement's fresh rows. The GLOBAL
    // clock never repeats: the recreation's full index commit
    // stamps strictly greater than every earlier marker, so the old
    // marker resolves as obsolete and the fresh rows keep serving.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/reborn.md", b"- [ ] first life\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    {
        // A crashed writer's marker from the FIRST incarnation.
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "INSERT OR REPLACE INTO text_write_intents(path, created_ms, token, registered_epoch)
             VALUES (?1, ?2, ?3, COALESCE((SELECT index_epoch FROM files WHERE path = ?1), 0))",
            rusqlite::params!["notes/reborn.md", now_ms(), 0xABAi64],
        )
        .unwrap();
        // The incarnation dies: its files row goes away entirely
        // (cascade takes the tasks), the marker survives.
        conn.execute(
            "DELETE FROM files WHERE path = ?1",
            rusqlite::params!["notes/reborn.md"],
        )
        .unwrap();
    }

    // The recreation: a full save indexes the second incarnation.
    let _ = session
        .save_text("notes/reborn.md", "- [ ] second life\n", None)
        .unwrap();

    // Un-repairable moment: the old marker must resolve as
    // obsolete — the recreation's stamp is strictly greater — and
    // the second life's rows keep serving.
    unsafe { std::env::set_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR", "reborn") };
    unsafe { std::env::set_var("SLATE_TEST_FAULT_REINDEX", "reborn") };
    let page = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .expect("the query succeeds");
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_REINDEX") };
    unsafe { std::env::remove_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR") };
    assert!(
        page.items
            .iter()
            .any(|r| r.path == "notes/reborn.md" && r.task.text.contains("second life")),
        "the recreation's fresh rows keep serving"
    );
    {
        let conn = session.conn.lock().unwrap();
        let intents: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/reborn.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(intents, 0, "the prior incarnation's marker resolved");
    }
}

#[test]
fn containment_without_an_index_row_still_plants_the_healing_marker() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 32: the host containment's missing-row arm
    // released the coordinator gate WITHOUT a durable marker — if
    // the file later became readable with no filesystem event, its
    // tasks stayed honest-empty until a full scan. Contained must
    // always leave a healing trigger.
    let (tmp, session) = make_vault(|p| {
        p.write_file("notes/anchor.md", b"- [ ] anchor\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    // Created AFTER the scan: on disk, but no files row.
    std::fs::write(
        tmp.path().join("notes").join("ghostly.md"),
        b"- [ ] unseen\n",
    )
    .unwrap();

    unsafe { std::env::set_var("SLATE_TEST_FAULT_REINDEX", "ghostly") };
    assert_eq!(
        session.repair_or_contain_path("notes/ghostly.md").unwrap(),
        TaskIndexRepairOutcome::Contained,
    );
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_REINDEX") };
    {
        let conn = session.conn.lock().unwrap();
        let intents: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/ghostly.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(intents, 1, "Contained must leave a healing marker");
    }

    // The file reads fine now: the planted marker heals on the very
    // next query — no filesystem event, no full scan.
    unsafe { std::env::set_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR", "ghostly") };
    let healed = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .expect("the healed query succeeds");
    unsafe { std::env::remove_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR") };
    assert!(
        healed.items.iter().any(|r| r.path == "notes/ghostly.md"),
        "the never-indexed file's tasks appear via the planted marker"
    );
    {
        let conn = session.conn.lock().unwrap();
        let intents: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/ghostly.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(intents, 0, "the marker cleared with the heal");
    }
}

#[test]
fn moved_files_resolve_prior_destination_markers_by_reading() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial rounds 33-34: a marker surviving from the
    // destination path's prior incarnation must be resolved by a
    // REAL read, never by move bookkeeping — round 34 proved a
    // move-time clock stamp vouches for content the move never
    // read and can retire a racing save's post-write marker. Moved
    // rows carry epoch 0 (unknown incarnation): while the repair
    // cannot read, the marker holds and the path serves
    // honest-empty (never stale); the first successful read
    // converges to disk truth and clears it.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/mover.md", b"- [ ] travels with me\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    {
        // A marker from the destination path's PRIOR incarnation:
        // the incarnation's history advanced the clock well past the
        // mover's epoch, and the marker stamped that later value —
        // realistic, since markers always stamp at-or-below the
        // clock.
        let conn = session.conn.lock().unwrap();
        conn.execute("UPDATE index_epoch_clock SET clock = clock + 100", [])
            .unwrap();
        conn.execute(
            "INSERT OR REPLACE INTO text_write_intents(path, created_ms, token, registered_epoch)
             VALUES (?1, ?2, ?3, (SELECT clock FROM index_epoch_clock))",
            rusqlite::params!["notes/dest.md", now_ms(), 0x40Ei64],
        )
        .unwrap();
    }

    let _ = session.rename_file("notes/mover.md", "dest.md").unwrap();

    // Un-repairable moment: the marker holds — honest-empty, never
    // the moved rows served on a path a live marker still suspects.
    unsafe { std::env::set_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR", "dest") };
    unsafe { std::env::set_var("SLATE_TEST_FAULT_REINDEX", "dest") };
    let page = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .expect("the query succeeds");
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_REINDEX") };
    assert!(
        page.items.iter().all(|r| r.path != "notes/dest.md"),
        "a still-suspect path serves nothing, not unverified rows"
    );
    {
        let conn = session.conn.lock().unwrap();
        let intents: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/dest.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert!(intents >= 1, "the marker survives until a read resolves it");
    }

    // Readable: the repair's real read converges and clears.
    let healed = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .expect("the healed query succeeds");
    unsafe { std::env::remove_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR") };
    assert!(
        healed
            .items
            .iter()
            .any(|r| r.path == "notes/dest.md" && r.task.text.contains("travels with me")),
        "the moved file's rows serve after a real read"
    );
    {
        let conn = session.conn.lock().unwrap();
        let intents: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/dest.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(
            intents, 0,
            "the read resolved the prior incarnation's marker"
        );
    }
}

#[test]
fn moves_leave_markers_that_converge_interleaved_saves() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 35: a SUCCESSFUL save on the source can
    // interleave between the move's filesystem rename and its index
    // commit — the save recreates the source, indexes it, clears
    // its own marker, and the move then relocates that fresh row to
    // the destination: both paths silently wrong, no marker, no
    // trigger. Every move therefore durably marks BOTH sides of
    // each rename before touching the filesystem and never clears
    // those markers itself; the next query's sweep re-reads both
    // paths and converges whatever the interleaving produced. The
    // recreated-source residue below stands in for the schedule's
    // end state: a file on disk at the source with no index row.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/src35.md", b"- [ ] source truth\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let _ = session
        .rename_file("notes/src35.md", "relocated.md")
        .unwrap();
    {
        // The move's own markers stand on both sides.
        let conn = session.conn.lock().unwrap();
        let (src_markers, dst_markers): (i64, i64) = (
            conn.query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/src35.md"],
                |row| row.get(0),
            )
            .unwrap(),
            conn.query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/relocated.md"],
                |row| row.get(0),
            )
            .unwrap(),
        );
        assert!(
            src_markers >= 1 && dst_markers >= 1,
            "the move marks both sides before mutating the filesystem"
        );
    }
    // The interleaved save's residue: bytes at the source, no row.
    session
        .provider
        .write_file("notes/src35.md", b"- [x] recreated at source\n")
        .unwrap();

    // The next query's sweep re-reads BOTH sides and converges:
    // the destination serves its disk bytes, and the recreated
    // source is discovered and indexed — no scan, no restart.
    unsafe { std::env::set_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR", "35") };
    let page = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .expect("the query succeeds");
    unsafe { std::env::remove_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR") };
    let moved = page
        .items
        .iter()
        .find(|r| r.path == "notes/relocated.md")
        .expect("the moved file's rows serve");
    assert!(
        !moved.task.completed && moved.task.text.contains("source truth"),
        "the destination serves its actual disk bytes"
    );
    let recreated = page
        .items
        .iter()
        .find(|r| r.path == "notes/src35.md")
        .expect("the interleaved save's file is discovered via the move's marker");
    assert!(
        recreated.task.completed && recreated.task.text.contains("recreated"),
        "the recreated source serves its actual disk bytes"
    );
    {
        let conn = session.conn.lock().unwrap();
        let markers: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path IN (?1, ?2)",
                rusqlite::params!["notes/src35.md", "notes/relocated.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(markers, 0, "the reads resolved every move marker");
    }
}

#[test]
fn renamed_over_writes_never_serve_stale_rows() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 34, the exact schedule: process A's save on
    // the destination registers its marker while the path has NO
    // files row (stamp 0), process B's rename lands the source's
    // row on the path afterward, and A's file write (which the
    // rename does not order against) leaves DISK holding A's bytes
    // while the INDEX holds the source's rows. A move-time clock
    // stamp would out-rank A's marker and retire it — stale rows
    // served silently forever. With moved rows carrying epoch 0 the
    // marker holds, containment refuses the stale rows, and the
    // first real read converges to disk truth.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/source.md", b"- [ ] source truth\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // B's rename: the source's row lands on the destination path.
    let _ = session
        .rename_file("notes/source.md", "landing.md")
        .unwrap();
    {
        // A's racing save, staged at its true fence state: it read
        // the destination BEFORE B's index commit — no files row —
        // so its marker stamped 0. Its file write then landed after
        // B's rename.
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "INSERT OR REPLACE INTO text_write_intents(path, created_ms, token, registered_epoch)
             VALUES (?1, ?2, ?3, 0)",
            rusqlite::params!["notes/landing.md", now_ms(), 0xACEDi64],
        )
        .unwrap();
    }
    session
        .provider
        .write_file("notes/landing.md", b"- [x] A's bytes won the disk\n")
        .unwrap();

    // Un-repairable moment: A's marker must HOLD (the moved row's
    // epoch vouches for nothing), so the source-content rows are
    // refused, not served against A's bytes.
    unsafe { std::env::set_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR", "landing") };
    unsafe { std::env::set_var("SLATE_TEST_FAULT_REINDEX", "landing") };
    let page = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .expect("the query succeeds");
    unsafe { std::env::remove_var("SLATE_TEST_FAULT_REINDEX") };
    assert!(
        page.items.iter().all(|r| r.path != "notes/landing.md"),
        "index rows describing the source must not serve over A's bytes"
    );

    // Readable: the real read converges to DISK truth — A's bytes.
    let healed = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .expect("the healed query succeeds");
    unsafe { std::env::remove_var("SLATE_TEST_INTENT_ABANDON_ZERO_FOR") };
    let row = healed
        .items
        .iter()
        .find(|r| r.path == "notes/landing.md")
        .expect("the destination serves after the read");
    assert!(
        row.task.completed && row.task.text.contains("A's bytes"),
        "converged to the disk truth A actually wrote"
    );
    {
        let conn = session.conn.lock().unwrap();
        let intents: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params!["notes/landing.md"],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(intents, 0, "the read resolved A's marker");
    }
}

#[test]
fn note_task_pages_read_one_snapshot() {
    let _env_guard = ENV_FAULT_GUARD.lock().unwrap();
    // Adversarial round 33: hash, rows, and totals were separate
    // autocommit reads — another process committing between them
    // paired an old content_hash with new offsets, defeating the
    // panel's hash/offset identity premise. The whole page now
    // reads from ONE deferred-transaction snapshot; the seam
    // commits a hash rewrite + row deletion through a second
    // connection between the hash read and the row reads.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/torn-read.md", b"- [ ] one\n- [x] two\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let baseline = session.note_tasks("notes/torn-read.md", 10).unwrap();
    assert_eq!(baseline.tasks.len(), 2);

    unsafe { std::env::set_var("SLATE_TEST_TORN_NOTE_READ", "torn-read") };
    let page = session.note_tasks("notes/torn-read.md", 10).unwrap();
    unsafe { std::env::remove_var("SLATE_TEST_TORN_NOTE_READ") };
    assert_eq!(
        page.content_hash, baseline.content_hash,
        "the hash comes from the snapshot, not the racing commit"
    );
    assert_eq!(
        page.tasks.len(),
        2,
        "the rows come from the SAME snapshot as the hash"
    );
    assert_eq!(page.total, 2, "totals agree with the snapshot too");
    assert_eq!(page.open_total, 1);
}

#[test]
fn note_tasks_never_buries_open_tasks_behind_completed_ones() {
    // Adversarial round 1: a note whose first N tasks are completed
    // must not spend the entire bounded budget on finished work.
    let mut body = String::new();
    for i in 0..40 {
        body.push_str(&format!("- [x] done {i}\n"));
    }
    body.push_str("- [ ] the actionable one\n");
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/buried.md", body.as_bytes()).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let page = session.note_tasks("notes/buried.md", 10).unwrap();
    assert_eq!(page.tasks.len(), 10);
    assert_eq!(page.open_total, 1);
    // The lone open task leads; the remaining budget fills with done
    // rows in document order.
    assert_eq!(page.tasks[0].text, "the actionable one");
    assert!(!page.tasks[0].completed);
    assert!(page.tasks[1..].iter().all(|t| t.completed));
    assert_eq!(page.tasks[1].text, "done 0");
}

#[test]
fn save_text_rewrites_tasks_table_on_edit() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/n.md", b"- [ ] a\n- [ ] b\n- [ ] c\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(session.tasks_for_file("notes/n.md").unwrap().len(), 3);

    // Toggle b → done and drop c entirely.
    session
        .save_text("notes/n.md", "- [ ] a\n- [x] b\n", None)
        .unwrap();
    let after = session.tasks_for_file("notes/n.md").unwrap();
    assert_eq!(after.len(), 2);
    assert_eq!(after[0].status_char, ' ');
    assert_eq!(after[1].status_char, 'x');
    assert_eq!(after[1].text, "b");
}

#[test]
fn fast_path_rescan_does_not_touch_tasks_table() {
    // Mirror of `fast_path_does_not_rewrite_links` — an unchanged
    // file must not churn the tasks rows. We capture a snapshot,
    // rescan, snapshot again, and compare for byte-level identity.
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "notes/t.md",
            "- [ ] alpha 📅 2026-06-01 ⏫\n- [x] beta\n".as_bytes(),
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let before = tasks_snapshot(&session, "notes/t.md");
    assert_eq!(before.len(), 2);

    session.scan_initial(&CancelToken::new()).unwrap();
    let after = tasks_snapshot(&session, "notes/t.md");
    assert_eq!(after, before, "fast path must not touch tasks");
}

#[test]
fn scan_picks_up_added_and_removed_tasks_on_content_change() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("notes/n.md", b"- [ ] one\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(session.tasks_for_file("notes/n.md").unwrap().len(), 1);

    // Add two tasks; rescan picks them up.
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file("notes/n.md", b"- [ ] one\n- [ ] two\n- [ ] three\n")
        .unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(session.tasks_for_file("notes/n.md").unwrap().len(), 3);

    // Remove all tasks; rescan drops the rows.
    provider.write_file("notes/n.md", b"plain text\n").unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(session.tasks_for_file("notes/n.md").unwrap().is_empty());
}

#[test]
fn tasks_in_vault_empty_filter_returns_every_task_in_sort_order() {
    // Sort order is (due ASC NULLS LAST, priority DESC NULLS LAST,
    // path ASC, ordinal ASC). Build a vault that exercises every
    // axis so the ordering is unambiguous.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", "- [ ] no metadata\n- [ ] high pri 🔼\n".as_bytes())
            .unwrap();
        p.write_file(
            "b.md",
            "- [ ] due tomorrow 📅 2026-05-24 🔽\n- [ ] due tomorrow no pri 📅 2026-05-24\n"
                .as_bytes(),
        )
        .unwrap();
        p.write_file("c.md", "- [ ] due today 📅 2026-05-23\n".as_bytes())
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let page = session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(100))
        .unwrap();
    let order: Vec<&str> = page.items.iter().map(|r| r.task.text.as_str()).collect();
    assert_eq!(
        order,
        vec![
            // Due today first.
            "due today",
            // Both due tomorrow — priority DESC NULLS LAST means
            // a populated priority (-1 here) outranks NULL even
            // when -1 is the lowest "real" priority.
            "due tomorrow",
            "due tomorrow no pri",
            // No due date — sort last; high-pri before no-pri.
            "high pri",
            "no metadata",
        ]
    );
    assert_eq!(page.total_filtered, 5);
    assert!(page.next_cursor.is_none());
}

#[test]
fn tasks_in_vault_completed_filter_excludes_done_tasks() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"- [ ] open\n- [x] done\n- [/] doing\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let page = session
        .tasks_in_vault(
            crate::TaskFilter {
                completed: Some(false),
                ..crate::TaskFilter::default()
            },
            Paging::first(100),
        )
        .unwrap();
    let texts: Vec<&str> = page.items.iter().map(|r| r.task.text.as_str()).collect();
    // Only the unchecked + in-progress tasks survive (done is
    // status_char `x` → completed=true).
    assert_eq!(texts, vec!["open", "doing"]);
    // total_filtered must match the filtered row count — not the
    // global count. Regression for the COUNT(*) subquery alias
    // bug (Codoki PR 134, High): with `t2`/`f2` aliases the
    // subquery's `WHERE t.completed = ?` resolved to the outer
    // `t`, turning the count into a correlated boolean and
    // returning the wrong total under any filter.
    assert_eq!(page.total_filtered, 2);
}

#[test]
fn tasks_in_vault_total_filtered_under_filters_matches_actual_count() {
    // Direct regression for the COUNT(*) subquery alias bug.
    // Build a vault where the global count and the
    // per-filter count differ on every filter axis, so an alias
    // slip on any one of them shows up here.
    let (_tmp, session) = make_vault(|p| {
        // 5 total tasks: 3 open / 2 done, 2 due in window / 3 out,
        // 1 highest priority / 1 high / 3 with no priority.
        p.write_file(
            "a.md",
            "- [ ] in window high pri 📅 2026-06-01 🔼\n\
             - [x] in window done 📅 2026-06-02\n\
             - [ ] out of window 📅 2026-07-01\n\
             - [ ] no due\n\
             - [x] no due done ⏫\n"
                .as_bytes(),
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let only_open = session
        .tasks_in_vault(
            crate::TaskFilter {
                completed: Some(false),
                ..crate::TaskFilter::default()
            },
            Paging::first(100),
        )
        .unwrap();
    assert_eq!(only_open.items.len(), 3);
    assert_eq!(only_open.total_filtered, 3);

    let only_done = session
        .tasks_in_vault(
            crate::TaskFilter {
                completed: Some(true),
                ..crate::TaskFilter::default()
            },
            Paging::first(100),
        )
        .unwrap();
    assert_eq!(only_done.items.len(), 2);
    assert_eq!(only_done.total_filtered, 2);

    let from = NaiveDate::from_ymd_opt(2026, 6, 1)
        .unwrap()
        .and_hms_opt(0, 0, 0)
        .unwrap()
        .and_utc()
        .timestamp_millis();
    let to = NaiveDate::from_ymd_opt(2026, 6, 30)
        .unwrap()
        .and_hms_opt(0, 0, 0)
        .unwrap()
        .and_utc()
        .timestamp_millis();
    let june_due = session
        .tasks_in_vault(
            crate::TaskFilter {
                due_from_ms: Some(from),
                due_to_ms: Some(to),
                ..crate::TaskFilter::default()
            },
            Paging::first(100),
        )
        .unwrap();
    assert_eq!(june_due.items.len(), 2);
    assert_eq!(june_due.total_filtered, 2);

    let high_or_better = session
        .tasks_in_vault(
            crate::TaskFilter {
                priority_at_least: Some(1),
                ..crate::TaskFilter::default()
            },
            Paging::first(100),
        )
        .unwrap();
    assert_eq!(high_or_better.items.len(), 2);
    assert_eq!(high_or_better.total_filtered, 2);
}

#[test]
fn tasks_in_vault_due_window_inclusive_lower_exclusive_upper() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "a.md",
            "- [ ] just before 📅 2026-05-22\n- [ ] from 📅 2026-05-23\n- [ ] to 📅 2026-05-24\n- [ ] after 📅 2026-05-25\n"
                .as_bytes(),
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let from = NaiveDate::from_ymd_opt(2026, 5, 23)
        .unwrap()
        .and_hms_opt(0, 0, 0)
        .unwrap()
        .and_utc()
        .timestamp_millis();
    let to = NaiveDate::from_ymd_opt(2026, 5, 24)
        .unwrap()
        .and_hms_opt(0, 0, 0)
        .unwrap()
        .and_utc()
        .timestamp_millis();
    let page = session
        .tasks_in_vault(
            crate::TaskFilter {
                due_from_ms: Some(from),
                due_to_ms: Some(to),
                ..crate::TaskFilter::default()
            },
            Paging::first(100),
        )
        .unwrap();
    let texts: Vec<&str> = page.items.iter().map(|r| r.task.text.as_str()).collect();
    // [from, to) → from-date matches, to-date excluded.
    assert_eq!(texts, vec!["from"]);
}

#[test]
fn tasks_in_vault_paging_round_trips() {
    let (_tmp, session) = make_vault(|p| {
        let mut body = String::new();
        for i in 0..7 {
            body.push_str(&format!("- [ ] task {i:02}\n"));
        }
        p.write_file("a.md", body.as_bytes()).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let limit = 3;
    let mut cursor: Option<String> = None;
    let mut seen: Vec<String> = Vec::new();
    loop {
        let paging = match &cursor {
            Some(c) => Paging::after(c.clone(), limit),
            None => Paging::first(limit),
        };
        let page = session
            .tasks_in_vault(crate::TaskFilter::default(), paging)
            .unwrap();
        assert_eq!(page.total_filtered, 7);
        for row in &page.items {
            seen.push(row.task.text.clone());
        }
        if let Some(c) = page.next_cursor {
            cursor = Some(c);
        } else {
            break;
        }
    }
    let expected: Vec<String> = (0..7).map(|i| format!("task {i:02}")).collect();
    assert_eq!(seen, expected);
}

#[test]
fn priority_at_least_filter_uses_idx_tasks_priority_not_table_scan() {
    // Regression for #139 (red-team M1). Before migration 009,
    // `tasks_in_vault` with `priority_at_least = Some(_)`
    // produced an `EXPLAIN QUERY PLAN` of `SCAN tasks` —
    // sequential table scan on every page. After the partial
    // index lands, the planner switches to `SEARCH … USING
    // INDEX idx_tasks_priority`.
    //
    // Populating ~500 prioritised rows + ANALYZE so the planner
    // has real cardinality estimates instead of the empty-table
    // defaults that would mask the plan choice.
    let (_tmp, session) = make_vault(|p| {
        let mut body = String::with_capacity(20_000);
        for i in 0..500 {
            let marker = match i % 4 {
                0 => "⏫",
                1 => "🔼",
                2 => "🔽",
                _ => "⏬",
            };
            body.push_str(&format!("- [ ] task {i} {marker}\n"));
        }
        // Plus some no-priority tasks so the partial index
        // actually skips rows (proving its value).
        for i in 0..200 {
            body.push_str(&format!("- [ ] no-pri {i}\n"));
        }
        p.write_file("bulk.md", body.as_bytes()).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let conn = session.conn.lock().unwrap();
    conn.execute("ANALYZE", []).unwrap();

    // Replicates the WHERE + ORDER BY shape `tasks_db::tasks_in_vault`
    // produces for a `priority_at_least` filter. The planner
    // picks the index based on WHERE + cardinality; LIMIT is
    // included for parity with the real path.
    let plan = explain_plan_rows(
        &conn,
        "SELECT f.path, t.ordinal
         FROM tasks t
         JOIN files f ON f.id = t.file_id
         WHERE t.priority IS NOT NULL AND t.priority >= ?
         ORDER BY IFNULL(t.due_ms, ?) ASC,
                  IFNULL(t.priority, ?) DESC,
                  f.path COLLATE BINARY ASC,
                  t.ordinal ASC
         LIMIT ?",
        rusqlite::params![1i64, i64::MAX, i32::MIN as i64, 200i64],
    );

    // Audit #284: parse rows from `EXPLAIN QUERY PLAN` structurally
    // (one detail string per planner step) rather than concatenating
    // them all into one blob and substring-matching. The index name
    // is the most stable part of the planner's output across SQLite
    // versions — phrasing around it ("USING INDEX" → "USING COVERING
    // INDEX") can drift but the name itself doesn't. Companion
    // tests verify the index exists in `sqlite_master` independently
    // (`idx_tasks_priority_exists_after_migration_009`).
    assert!(
        plan.iter()
            .any(|row| row.detail.contains("idx_tasks_priority")),
        "priority filter should use idx_tasks_priority; plan:\n{}",
        format_plan(&plan),
    );
    // Defensive check: regardless of how SQLite phrases the
    // optimizer output in future versions, no line should
    // start with a bare full-table SCAN of `t`. The plan may
    // include "USE TEMP B-TREE FOR ORDER BY" — that's the
    // sort step, separately tracked as red-team M2.
    for row in &plan {
        let trimmed = row.detail.trim_start();
        assert!(
            !(trimmed.starts_with("SCAN t ") || trimmed == "SCAN t"),
            "expected no full SCAN of tasks; row {}: {:?}\nfull plan:\n{}",
            row.id,
            row.detail,
            format_plan(&plan),
        );
    }
}

#[test]
fn idx_tasks_priority_exists_after_migration_009() {
    // Belt-and-braces: even if a future planner change masks the
    // EXPLAIN-based test above, the index itself must remain in
    // `sqlite_master`. Migration 009 is append-only / forward-
    // only per the project's migration policy.
    let (_tmp, session) = make_vault(|_| {});
    let conn = session.conn.lock().unwrap();
    let exists: i64 = conn
        .query_row(
            "SELECT COUNT(*) FROM sqlite_master
             WHERE type = 'index' AND name = 'idx_tasks_priority'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(
        exists, 1,
        "idx_tasks_priority must exist after migration 009"
    );
}

#[test]
fn idx_tasks_sort_exists_after_migration_010() {
    // Companion to `idx_tasks_priority_exists_after_migration_009`:
    // the expression index must remain in sqlite_master so future
    // planner changes can't silently mask a missing index.
    let (_tmp, session) = make_vault(|_| {});
    let conn = session.conn.lock().unwrap();
    let exists: i64 = conn
        .query_row(
            "SELECT COUNT(*) FROM sqlite_master
             WHERE type = 'index' AND name = 'idx_tasks_sort'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(exists, 1, "idx_tasks_sort must exist after migration 010");
}

#[test]
fn unfiltered_tasks_in_vault_uses_idx_tasks_sort_not_full_temp_btree() {
    // Regression for #145 (red-team M2). Before migration 010 the
    // unfiltered `tasks_in_vault` plan ended in
    // `USE TEMP B-TREE FOR ORDER BY` — materialise every row,
    // sort in a temp btree, apply LIMIT. With the expression
    // index, the planner walks the index in (due, priority)
    // order and only needs `USE TEMP B-TREE FOR RIGHT PART OF
    // ORDER BY` to sort within (due, priority) tie-groups by
    // path. That residual sort is small and bounded by tie-group
    // size, not total matching rows.
    //
    // Populate ~500 prioritised tasks + ANALYZE so the planner
    // has real stats; otherwise the empty-table defaults can
    // mask the plan choice.
    let (_tmp, session) = make_vault(|p| {
        let mut body = String::with_capacity(20_000);
        for i in 0..500 {
            let marker = match i % 4 {
                0 => "⏫",
                1 => "🔼",
                2 => "🔽",
                _ => "⏬",
            };
            body.push_str(&format!(
                "- [ ] task {i} {marker} 📅 2026-06-{:02}\n",
                (i % 28) + 1
            ));
        }
        p.write_file("bulk.md", body.as_bytes()).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let conn = session.conn.lock().unwrap();
    conn.execute("ANALYZE", []).unwrap();

    // Replicates the ORDER BY shape `tasks_db::tasks_in_vault`
    // produces, including the literal IFNULL sentinels that
    // match the expression-index definition.
    let plan = explain_plan_rows(
        &conn,
        "SELECT f.path, t.ordinal
         FROM tasks t JOIN files f ON f.id = t.file_id
         ORDER BY IFNULL(t.due_ms, 9223372036854775807) ASC,
                  IFNULL(t.priority, -2147483648) DESC,
                  f.path COLLATE BINARY ASC,
                  t.ordinal ASC
         LIMIT ?",
        rusqlite::params![200i64],
    );

    // Audit #284: same structural-parse approach as
    // `priority_at_least_filter_uses_idx_tasks_priority_not_table_scan`.
    // Companion `idx_tasks_sort_exists_after_migration_010` test
    // verifies the index's existence independent of EXPLAIN output.
    assert!(
        plan.iter().any(|row| row.detail.contains("idx_tasks_sort")),
        "ORDER BY should use idx_tasks_sort; plan:\n{}",
        format_plan(&plan),
    );
    // The FULL temp-btree variant (no qualifier) means the
    // entire ORDER BY had to be sorted from scratch — that's
    // the bug. The "RIGHT PART OF ORDER BY" variant is
    // acceptable: the index satisfies the leading tiers and
    // only the trailing path tiebreak sorts in a temp btree
    // within tie-groups.
    for row in &plan {
        let trimmed = row.detail.trim_start();
        assert!(
            trimmed != "USE TEMP B-TREE FOR ORDER BY",
            "expected idx-driven sort, got full temp btree; plan:\n{}",
            format_plan(&plan),
        );
    }
}

#[test]
fn tasks_in_vault_priority_at_least_filter() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "a.md",
            "- [ ] no pri\n- [ ] low 🔽\n- [ ] high 🔼\n- [ ] highest ⏫\n".as_bytes(),
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let page = session
        .tasks_in_vault(
            crate::TaskFilter {
                priority_at_least: Some(1),
                ..crate::TaskFilter::default()
            },
            Paging::first(100),
        )
        .unwrap();
    let texts: Vec<&str> = page.items.iter().map(|r| r.task.text.as_str()).collect();
    assert_eq!(texts, vec!["highest", "high"]);
}

#[test]
fn toggle_task_status_changes_only_the_status_character() {
    let body = "- [ ] task one\n- [ ] task two 📅 2026-06-01 ⏫\n";
    let (tmp, session) = make_vault(|p| {
        p.write_file("n.md", body.as_bytes()).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    session
        .toggle_task_status("n.md", 1, 'x', None)
        .expect("toggle succeeds");

    // Re-read the file from disk to confirm everything outside
    // the bracket is preserved byte-for-byte.
    let on_disk = std::fs::read_to_string(tmp.path().join("n.md")).unwrap();
    assert_eq!(on_disk, "- [ ] task one\n- [x] task two 📅 2026-06-01 ⏫\n");

    // Index reflects the new state, including completed=true.
    let tasks = session.tasks_for_file("n.md").unwrap();
    assert_eq!(tasks[1].status_char, 'x');
    assert!(tasks[1].completed);
    // Metadata still on the task.
    assert!(tasks[1].due_ms.is_some());
    assert_eq!(tasks[1].priority, Some(2));
}

#[test]
fn toggle_task_status_preserves_indentation_for_nested_tasks() {
    let body = "- [ ] parent\n  - [ ] child\n    - [ ] grandchild\n";
    let (tmp, session) = make_vault(|p| {
        p.write_file("n.md", body.as_bytes()).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    session
        .toggle_task_status("n.md", 2, 'x', None)
        .expect("toggle grandchild");
    let on_disk = std::fs::read_to_string(tmp.path().join("n.md")).unwrap();
    assert_eq!(
        on_disk,
        "- [ ] parent\n  - [ ] child\n    - [x] grandchild\n"
    );
}

#[test]
fn toggle_task_status_supports_custom_status_chars() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"- [ ] thing\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session
        .toggle_task_status("n.md", 0, '/', None)
        .expect("toggle to in-progress");
    let on_disk = std::fs::read_to_string(tmp.path().join("n.md")).unwrap();
    assert_eq!(on_disk, "- [/] thing\n");
}

#[test]
fn toggle_task_status_returns_invalid_argument_for_out_of_range_ordinal() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"- [ ] only one task\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let result = session.toggle_task_status("n.md", 99, 'x', None);
    match result {
        Err(VaultError::InvalidArgument { message }) => {
            assert!(message.contains("ordinal 99"), "got: {message}");
        }
        other => panic!("expected InvalidArgument, got {other:?}"),
    }
    // File untouched.
    let on_disk = std::fs::read_to_string(tmp.path().join("n.md")).unwrap();
    assert_eq!(on_disk, "- [ ] only one task\n");
}

#[test]
fn toggle_task_status_returns_write_conflict_when_hash_stale() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"- [ ] thing\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    // Pretend the editor read at a state that no longer exists.
    let stale_hash = crate::content_hash(b"something else entirely\n");
    let result = session.toggle_task_status("n.md", 0, 'x', Some(&stale_hash));
    match result {
        Err(VaultError::WriteConflict { .. }) => {}
        other => panic!("expected WriteConflict, got {other:?}"),
    }
    // File untouched.
    let on_disk = std::fs::read_to_string(tmp.path().join("n.md")).unwrap();
    assert_eq!(on_disk, "- [ ] thing\n");
}

#[test]
fn toggle_task_status_appends_oplog_entry() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"- [ ] thing\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(session.read_oplog("n.md").unwrap().is_empty());

    session.toggle_task_status("n.md", 0, 'x', None).unwrap();

    let entries = session.read_oplog("n.md").unwrap();
    assert_eq!(
        entries.len(),
        1,
        "toggle should append exactly one op-log entry"
    );
    // O-1 (#539): the toggle's intent rides the entry as an annotation —
    // one atomic kind-4 entry wrapping the cold-cache snapshot.
    assert_eq!(entries[0].op_kind, crate::OpKind::Annotated);
    let (inner_kind, _inner_payload, anns) =
        crate::oplog::decode_annotated(&entries[0].payload_bytes).unwrap();
    assert_eq!(inner_kind, crate::OpKind::WholeFileReplace);
    assert_eq!(
        anns,
        vec![crate::OpAnnotation::ToggleTask {
            ordinal: 0,
            new_status: 'x',
        }]
    );
}

#[test]
fn toggle_task_status_serializes_with_concurrent_save() {
    // Concurrent toggle + editor save on the same file must
    // serialize through the session mutex so the op-log records
    // exactly two entries in well-defined order. The actual
    // outcome (which save lands first) is racy; we assert
    // invariants that hold either way.
    use std::sync::Arc as StdArc;
    use std::thread;

    let (_tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"- [ ] thing\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let session = StdArc::new(session);

    let s1 = StdArc::clone(&session);
    let s2 = StdArc::clone(&session);
    let t1 = thread::spawn(move || s1.toggle_task_status("n.md", 0, 'x', None));
    let t2 = thread::spawn(move || s2.save_text("n.md", "- [ ] thing edited\n", None));
    t1.join().unwrap().expect("toggle ok");
    t2.join().unwrap().expect("save ok");

    let entries = session.read_oplog("n.md").unwrap();
    assert_eq!(entries.len(), 2, "both saves should land an op-log entry");
    // Hashes should chain — second entry's content_hash_before
    // equals first's content_hash_after.
    assert_eq!(
        entries[1].content_hash_before, entries[0].content_hash_after,
        "op-log entries should chain through the mutex"
    );
}

#[test]
fn toggle_task_status_does_not_lose_concurrent_save() {
    // Regression for #135 (red-team finding C1). The previous
    // shape of `toggle_task_status` read the file outside the
    // session mutex, parsed, then called `save_text` which
    // acquired the mutex inside its own body. A concurrent
    // `save_text(..., None)` between toggle's read and toggle's
    // save would land first; toggle then wrote a rebuilt version
    // of the PRE-save contents, silently overwriting the editor's
    // edit.
    //
    // The earlier `toggle_task_status_serializes_with_concurrent_save`
    // test caught hash-chain ordering through the op-log but did
    // not assert the *on-disk content* of both ops survived —
    // so the bug passed CI for an entire PR cycle. This test
    // closes that gap: after both ops complete the file must
    // contain `appended` (the save's distinctive payload),
    // regardless of which op landed first.
    //
    // Run many trials with a barrier to maximize race likelihood —
    // the red-team probe saw 50/50 corruption under exactly this
    // shape.
    use std::sync::{Arc as StdArc, Barrier};
    use std::thread;

    for trial in 0..20 {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("n.md", b"- [ ] thing\n").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let session = StdArc::new(session);
        let barrier = StdArc::new(Barrier::new(2));

        let s1 = StdArc::clone(&session);
        let b1 = StdArc::clone(&barrier);
        let s2 = StdArc::clone(&session);
        let b2 = StdArc::clone(&barrier);

        let t1 = thread::spawn(move || {
            b1.wait();
            s1.toggle_task_status("n.md", 0, 'x', None)
        });
        let t2 = thread::spawn(move || {
            b2.wait();
            s2.save_text("n.md", "- [ ] thing\nappended\n", None)
        });
        t1.join().unwrap().expect("toggle ok");
        t2.join().unwrap().expect("save ok");

        let on_disk = session.read_text("n.md").unwrap();
        // Acceptable final states (both ops survived):
        //   - save-then-toggle: "- [x] thing\nappended\n"
        //   - toggle-then-save: "- [ ] thing\nappended\n"
        // Unacceptable (lost-update bug):
        //   - "- [x] thing\n"  (toggle's stale read clobbered save)
        assert!(
            on_disk.contains("appended"),
            "trial {trial}: save's `appended` line was lost — final on-disk: {on_disk:?}",
        );
    }
}

#[test]
fn tasks_table_purged_when_file_exceeds_large_file_threshold() {
    // A file that grows past the large-file refuse threshold gets
    // its derivative rows purged. The tasks table must follow the
    // same discipline as headings / links / properties, otherwise
    // stale task rows would keep showing up in the panel for a
    // file the scanner no longer indexes.
    let tmp = tempfile::tempdir().unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file("notes/n.md", b"- [ ] task\n- [ ] other\n")
        .unwrap();

    let mut config = SessionConfig::new(tmp.path().join(".slate"));
    // Tiny threshold so the second write trips it.
    config.large_file_refuse_bytes = 50;
    let session = VaultSession::open(
        Arc::new(FsVaultProvider::new(tmp.path().to_path_buf())),
        config,
    )
    .unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(!session.tasks_for_file("notes/n.md").unwrap().is_empty());

    // Grow the file past the refuse threshold.
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file("notes/n.md", vec![b'a'; 200].as_slice())
        .unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(
        session.tasks_for_file("notes/n.md").unwrap().is_empty(),
        "large-file purge must drop task rows"
    );
}
