// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Privacy tests for the oplog-degradation diagnostics (#507).
//!
//! slate-core routes its non-fatal oplog-degradation warnings through the
//! `log` facade, deliberately keeping vault-relative paths / note names off
//! `warn`-level records and emitting them only on a separate `debug!` line.
//! These tests plant a sentinel note title, force the oplog-append failure
//! path, and prove:
//!
//! * exactly one `warn` record is emitted, carrying the `file_id` and error
//!   kind but **not** the sentinel; and
//! * the sentinel path rides a `debug` record that is visible only when the
//!   captured max level is `Debug`.
//!
//! ## Why the machinery here is heavier than a normal unit test
//!
//! `log`'s logger is process-global and set-once (`log::set_logger` succeeds
//! only the first time). So we install one capture logger for the whole test
//! binary, funnel every record into a shared buffer, and serialise these
//! logging tests behind a mutex so their buffer snapshots don't interleave
//! with each other (or with any incidental `log` call from another test).
//! Each test raises the max level itself and clears the buffer on entry.

use super::*;
use std::sync::{Mutex, OnceLock};
use std::thread::ThreadId;

/// A captured record: the emitting thread, its level, and the formatted
/// message. The thread id lets each test see only the records its own
/// `save_text` produced — the capture logger is process-global, so a parallel
/// test's oplog warning would otherwise pollute the buffer (records are logged
/// synchronously on the calling thread, so thread id is a reliable filter).
#[derive(Clone)]
struct Captured {
    thread: ThreadId,
    level: log::Level,
    message: String,
}

/// Buffer every record the capture logger sees. `None` until the logger is
/// installed; a `Mutex<Vec<_>>` afterward.
static CAPTURED: OnceLock<Mutex<Vec<Captured>>> = OnceLock::new();

/// Serialises the logging tests so they don't fight over the process-global
/// `log::max_level` while asserting (one test wants Debug, another Warn).
static TEST_LOCK: Mutex<()> = Mutex::new(());

struct CaptureLogger;

impl log::Log for CaptureLogger {
    fn enabled(&self, metadata: &log::Metadata<'_>) -> bool {
        metadata.level() <= log::max_level()
    }
    fn log(&self, record: &log::Record<'_>) {
        // Record everything the level gate admits; individual tests set the
        // max level to decide whether debug lines are in scope.
        if !self.enabled(record.metadata()) {
            return;
        }
        if let Some(buf) = CAPTURED.get() {
            buf.lock().unwrap().push(Captured {
                thread: std::thread::current().id(),
                level: record.level(),
                message: format!("{}", record.args()),
            });
        }
    }
    fn flush(&self) {}
}

static CAPTURE_LOGGER: CaptureLogger = CaptureLogger;

/// Install the capture logger once for this test binary. Idempotent: a
/// second `set_logger` fails and is ignored. Returns the shared buffer.
///
/// If another test in this binary had already installed a *different* logger,
/// our records would never land — so this asserts our logger is the active
/// one (no other slate-core test installs a logger; this pins that).
fn capture_buffer() -> &'static Mutex<Vec<Captured>> {
    let buf = CAPTURED.get_or_init(|| Mutex::new(Vec::new()));
    let _ = log::set_logger(&CAPTURE_LOGGER);
    // Prove our sink is live: emit a debug probe on this thread and confirm it
    // was captured. Guards against a foreign logger silently swallowing records
    // (which would make the privacy assertions vacuously pass).
    log::set_max_level(log::LevelFilter::Debug);
    let me = std::thread::current().id();
    let probe = "__capture_probe__";
    let before = buf.lock().unwrap().len();
    log::debug!("{probe}");
    let captured_probe = buf
        .lock()
        .unwrap()
        .iter()
        .skip(before)
        .any(|r| r.thread == me && r.message.contains(probe));
    assert!(
        captured_probe,
        "capture logger is not the active `log` sink — another logger won \
         set_logger; the privacy assertions can't be trusted"
    );
    buf
}

/// Snapshot the records emitted on the current thread, in order. Filtering by
/// thread id isolates this test from any parallel test's log output.
fn drain_this_thread(buf: &Mutex<Vec<Captured>>) -> Vec<Captured> {
    let me = std::thread::current().id();
    buf.lock()
        .unwrap()
        .iter()
        .filter(|r| r.thread == me)
        .cloned()
        .collect()
}

/// Remove this thread's records from the shared buffer so a later snapshot in
/// the same test starts clean.
fn clear_this_thread(buf: &Mutex<Vec<Captured>>) {
    let me = std::thread::current().id();
    buf.lock().unwrap().retain(|r| r.thread != me);
}

/// Build a session over a fresh temp vault, returning the temp dir (kept
/// alive for the caller), the session, and the cache dir (`<vault>/.slate`).
fn session_over_temp_vault() -> (tempfile::TempDir, VaultSession, std::path::PathBuf) {
    let tmp = tempfile::tempdir().unwrap();
    let cache_dir = tmp.path().join(".slate");
    let config = SessionConfig::new(cache_dir.clone());
    let provider = Arc::new(FsVaultProvider::new(tmp.path().to_path_buf()));
    let session = VaultSession::open(provider, config).unwrap();
    (tmp, session, cache_dir)
}

/// Build a session, then sabotage its op-log directory so the next
/// oplog-append fails: replace `<cache_dir>/oplog` with a regular file, so
/// `create_dir_all(<cache_dir>/oplog)` inside `append_entry` errors. The
/// file save itself still succeeds (it writes vault content, not the cache),
/// so only the supplementary oplog diagnostic fires.
fn session_with_broken_oplog_dir() -> (tempfile::TempDir, VaultSession) {
    let (tmp, session, cache_dir) = session_over_temp_vault();

    // Plant a regular file where the oplog directory would go. `open`
    // already created `.slate` for the DB; `oplog/` doesn't exist yet.
    let oplog_dir = cache_dir.join("oplog");
    std::fs::write(&oplog_dir, b"not a directory").unwrap();
    assert!(
        oplog_dir.is_file(),
        "sabotage precondition: <cache_dir>/oplog must be a file, not a dir"
    );

    (tmp, session)
}

/// The sentinel note title. If it ever appears in a `warn` record, the
/// privacy rule is broken.
const SENTINEL: &str = "SECRET-NOTE-TITLE";

#[test]
fn oplog_append_failure_warns_without_path_and_debugs_with_it() {
    let _guard = TEST_LOCK.lock().unwrap();
    let buf = capture_buffer();

    // Debug-level capture so we can see BOTH the warn and the debug line and
    // assert on the split. (The default host install is warn-only, which is
    // the release-safe behaviour; here we opt into debug to inspect it.)
    log::set_max_level(log::LevelFilter::Debug);
    clear_this_thread(buf); // drop the capture_buffer() probe record

    let (_tmp, session) = session_with_broken_oplog_dir();

    // Save a note whose vault-relative path IS the sentinel. The write
    // succeeds; the oplog append fails, exercising session.rs's
    // append-failure diagnostic.
    let sentinel_path = format!("{SENTINEL}.md");
    session
        .save_text(&sentinel_path, "hello world\n", None)
        .expect("the file save itself must succeed; only the oplog degrades");

    let records = drain_this_thread(buf);

    let warns: Vec<&Captured> = records
        .iter()
        .filter(|r| r.level == log::Level::Warn)
        .collect();
    assert_eq!(
        warns.len(),
        1,
        "expected exactly one warn from the oplog-append failure, got {}: {:?}",
        warns.len(),
        warns.iter().map(|r| &r.message).collect::<Vec<_>>()
    );

    // (a) The warn must NOT carry the sentinel note title.
    let warn = &warns[0];
    assert!(
        !warn.message.contains(SENTINEL),
        "warn leaked the note title: {:?}",
        warn.message
    );
    // …and it should carry the non-identifying facts we routed instead.
    assert!(
        warn.message.contains("file_id="),
        "warn should identify the file by id, not path: {:?}",
        warn.message
    );

    // (b) The path rides a debug record — present here because we captured at
    // Debug level.
    let debug_with_path = records
        .iter()
        .filter(|r| r.level == log::Level::Debug)
        .find(|r| r.message.contains(SENTINEL));
    assert!(
        debug_with_path.is_some(),
        "expected a debug line carrying the sentinel path at Debug level; debug records: {:?}",
        records
            .iter()
            .filter(|r| r.level == log::Level::Debug)
            .map(|r| &r.message)
            .collect::<Vec<_>>()
    );
}

#[test]
fn oplog_append_failure_debug_line_is_suppressed_at_warn_level() {
    let _guard = TEST_LOCK.lock().unwrap();
    let buf = capture_buffer();

    // Warn-level capture — the release-default posture. The debug line
    // carrying the path must NOT be recorded, so the sentinel never appears
    // anywhere in the captured stream. Clear the probe (emitted at Debug by
    // capture_buffer) *before* lowering to Warn.
    clear_this_thread(buf);
    log::set_max_level(log::LevelFilter::Warn);

    let (_tmp, session) = session_with_broken_oplog_dir();

    let sentinel_path = format!("{SENTINEL}.md");
    session
        .save_text(&sentinel_path, "hello world\n", None)
        .expect("the file save itself must succeed; only the oplog degrades");

    let records = drain_this_thread(buf);

    // Exactly one warn, and it's clean.
    let warns: Vec<&Captured> = records
        .iter()
        .filter(|r| r.level == log::Level::Warn)
        .collect();
    assert_eq!(warns.len(), 1, "expected exactly one warn at Warn level");
    assert!(
        !warns[0].message.contains(SENTINEL),
        "warn leaked the note title: {:?}",
        warns[0].message
    );

    // No debug records at all at Warn level, so the sentinel is nowhere in
    // the captured stream — the shipped-log guarantee.
    assert!(
        records.iter().all(|r| !r.message.contains(SENTINEL)),
        "sentinel path escaped into a captured record at Warn level: {:?}",
        records
            .iter()
            .filter(|r| r.message.contains(SENTINEL))
            .map(|r| (r.level, &r.message))
            .collect::<Vec<_>>()
    );
}

/// Regression for the adversarial-review finding: `append_entry` returns an
/// `io::Error` whose *Display* embeds the on-disk cache path when it meets a
/// short/torn existing op-log (`oplog {path:?}: torn header …`). The warn
/// must not interpolate that Display — it carries only the error *kind* — so
/// the cache path (`…/.slate/oplog/<id>.oplog`) never reaches a warn record.
///
/// Unlike the sabotage above (which yields a generic `NotADirectory`), this
/// drives the specific path-bearing error branch in `oplog::append_entry`.
#[test]
fn oplog_append_torn_header_warn_carries_no_cache_path() {
    let _guard = TEST_LOCK.lock().unwrap();
    let buf = capture_buffer();

    // Warn-level capture: the release-default posture we must keep clean.
    log::set_max_level(log::LevelFilter::Warn);

    let (_tmp, session, cache_dir) = session_over_temp_vault();
    let sentinel_path = format!("{SENTINEL}.md");

    // First save succeeds: assigns a file_id and writes a valid op-log
    // (8-byte header + entry). No warn expected here.
    session
        .save_text(&sentinel_path, "v1\n", None)
        .expect("first save should succeed");

    // Look up the file_id so we can find and corrupt its op-log file.
    let (file_id, log_name): (i64, String) = {
        let conn = session.conn.lock().unwrap();
        conn.query_row(
            "SELECT id, oplog_name FROM files WHERE path = ?1",
            rusqlite::params![sentinel_path],
            |row| Ok((row.get(0)?, row.get(1)?)),
        )
        .expect("file row should exist after a successful save")
    };
    let oplog_file = crate::oplog::oplog_path_for_name(&cache_dir, &log_name);
    assert!(oplog_file.is_file(), "op-log file should exist after save");

    // Truncate to fewer than HEADER_LEN (8) bytes. The next append opens a
    // non-empty file, sees `len < HEADER_LEN`, and returns the torn-header
    // error whose Display embeds `oplog_file`.
    std::fs::write(&oplog_file, b"XYZW").unwrap();

    // Clear this thread's records *after* the first (successful) save and the
    // capture_buffer() probe, so we only capture the failing save's records.
    clear_this_thread(buf);

    // Second save: the file write succeeds, but the op-log append hits the
    // torn header → path-bearing error → warn.
    session
        .save_text(&sentinel_path, "v2 body\n", None)
        .expect("the file save itself must succeed; only the oplog degrades");

    let records = drain_this_thread(buf);

    let warns: Vec<&Captured> = records
        .iter()
        .filter(|r| r.level == log::Level::Warn)
        .collect();
    assert_eq!(
        warns.len(),
        1,
        "expected exactly one warn from the torn-header append failure, got {:?}",
        warns.iter().map(|r| &r.message).collect::<Vec<_>>()
    );
    let warn = &warns[0];

    // The load-bearing assertion: the warn must not carry the cache path in
    // any form — not the full cache dir, not the note title, not the
    // ".oplog" suffix that only the path-bearing Display contains.
    let cache_str = cache_dir.to_string_lossy();
    assert!(
        !warn.message.contains(cache_str.as_ref()),
        "warn leaked the cache path: {:?}",
        warn.message
    );
    assert!(
        !warn.message.contains(".oplog"),
        "warn leaked the op-log filename (path-bearing Display escaped): {:?}",
        warn.message
    );
    assert!(
        !warn.message.contains(SENTINEL),
        "warn leaked the note title: {:?}",
        warn.message
    );
    // It should still identify the file by id.
    assert!(
        warn.message.contains(&format!("file_id={file_id}")),
        "warn should identify the file by id: {:?}",
        warn.message
    );
}

/// Regression for the adversarial-review finding: `canvas_apply`'s semantic
/// journal append used to swallow its error with `let _ = …`, giving the host
/// no diagnostic when the cache op-log was unwritable/torn. It now routes a
/// facade warn like the save/anchor sites. This test forces the failure and
/// asserts (a) the canvas write still succeeds, (b) a canvas-journal warn
/// fires, and (c) no warn carries a cache path.
///
/// (The torn op-log also trips the save-path append inside `canvas_apply`, so
/// more than one warn is expected; we assert the canvas-journal warn is among
/// them and that *every* warn is path-clean.)
#[test]
fn canvas_apply_journal_failure_warns_without_cache_path() {
    use crate::canvas::apply::{CanvasAction, CanvasNodeContent, CanvasOp};

    let _guard = TEST_LOCK.lock().unwrap();
    let buf = capture_buffer();
    log::set_max_level(log::LevelFilter::Warn);

    const SAMPLE: &str = include_str!("../../../tests/fixtures/canvas/sample.canvas");

    let (_tmp, session, cache_dir) = session_over_temp_vault();
    // Write the canvas fixture, scan, and open it.
    session
        .save_text("board.canvas", SAMPLE, None)
        .expect("seed canvas file");
    session.scan_initial(&CancelToken::new()).unwrap();
    let info = session.open_canvas("board.canvas").unwrap();

    // file_id of the canvas file, so we can corrupt its op-log.
    let log_name: String = {
        let conn = session.conn.lock().unwrap();
        conn.query_row(
            "SELECT oplog_name FROM files WHERE path = ?1",
            rusqlite::params!["board.canvas"],
            |row| row.get(0),
        )
        .expect("canvas file row should exist")
    };
    let oplog_file = crate::oplog::oplog_path_for_name(&cache_dir, &log_name);
    // The save above may or may not have created the op-log yet; force a
    // short/torn file so the next append hits the path-bearing error.
    std::fs::create_dir_all(oplog_file.parent().unwrap()).unwrap();
    std::fs::write(&oplog_file, b"XYZW").unwrap();

    clear_this_thread(buf);

    // Apply a canvas action. The committed canvas write succeeds; the op-log
    // appends (save-path and the semantic journal) both hit the torn header.
    let action = CanvasAction {
        name: "create card".into(),
        ops: vec![CanvasOp::CreateNode {
            id: "regress-1".into(),
            content: CanvasNodeContent::Text {
                text: "regression".into(),
            },
            x: 0.0,
            y: 900.0,
            width: 200.0,
            height: 120.0,
            color: None,
        }],
    };
    session
        .canvas_apply(info.handle, action)
        .expect("canvas write must succeed even when the oplog journal degrades");

    let records = drain_this_thread(buf);
    let warns: Vec<&Captured> = records
        .iter()
        .filter(|r| r.level == log::Level::Warn)
        .collect();

    // The canvas-journal warn is now present (it was previously swallowed).
    assert!(
        warns
            .iter()
            .any(|r| r.message.contains("canvas oplog journal append failed")),
        "expected a canvas-journal append-failure warn; warns: {:?}",
        warns.iter().map(|r| &r.message).collect::<Vec<_>>()
    );
    // And every warn is path-clean (no cache path, no ".oplog" suffix).
    let cache_str = cache_dir.to_string_lossy();
    for w in &warns {
        assert!(
            !w.message.contains(cache_str.as_ref()) && !w.message.contains(".oplog"),
            "a warn leaked the cache path: {:?}",
            w.message
        );
    }
}
