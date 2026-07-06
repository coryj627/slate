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

/// A captured record: its level and the fully-formatted message.
#[derive(Clone)]
struct Captured {
    level: log::Level,
    message: String,
}

/// Buffer every record the capture logger sees. `None` until the logger is
/// installed; a `Mutex<Vec<_>>` afterward.
static CAPTURED: OnceLock<Mutex<Vec<Captured>>> = OnceLock::new();

/// Serialises the logging tests so one test's snapshot can't observe another
/// test's records (parallel `cargo test` would otherwise interleave them).
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
                level: record.level(),
                message: format!("{}", record.args()),
            });
        }
    }
    fn flush(&self) {}
}

static CAPTURE_LOGGER: CaptureLogger = CaptureLogger;

/// Install the capture logger once for this test binary. Idempotent: a
/// second call is a no-op (another test — or another binary sharing the
/// process — may have won `set_logger`; that's fine, we only need *a*
/// logger and *our* buffer). Returns a reference to the shared buffer.
fn capture_buffer() -> &'static Mutex<Vec<Captured>> {
    let buf = CAPTURED.get_or_init(|| Mutex::new(Vec::new()));
    // `set_logger` fails after the first success; ignore the error.
    let _ = log::set_logger(&CAPTURE_LOGGER);
    buf
}

/// Build a session, then sabotage its op-log directory so the next
/// oplog-append fails: replace `<cache_dir>/oplog` with a regular file, so
/// `create_dir_all(<cache_dir>/oplog)` inside `append_entry` errors. The
/// file save itself still succeeds (it writes vault content, not the cache),
/// so only the supplementary oplog diagnostic fires.
fn session_with_broken_oplog_dir() -> (tempfile::TempDir, VaultSession) {
    let tmp = tempfile::tempdir().unwrap();
    let cache_dir = tmp.path().join(".slate");
    let config = SessionConfig::new(cache_dir.clone());
    let provider = Arc::new(FsVaultProvider::new(tmp.path().to_path_buf()));
    let session = VaultSession::open(provider, config).unwrap();

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
    buf.lock().unwrap().clear();

    let (_tmp, session) = session_with_broken_oplog_dir();

    // Save a note whose vault-relative path IS the sentinel. The write
    // succeeds; the oplog append fails, exercising session.rs's
    // append-failure diagnostic.
    let sentinel_path = format!("{SENTINEL}.md");
    session
        .save_text(&sentinel_path, "hello world\n", None)
        .expect("the file save itself must succeed; only the oplog degrades");

    let records = buf.lock().unwrap().clone();

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
    // anywhere in the captured stream.
    log::set_max_level(log::LevelFilter::Warn);
    buf.lock().unwrap().clear();

    let (_tmp, session) = session_with_broken_oplog_dir();

    let sentinel_path = format!("{SENTINEL}.md");
    session
        .save_text(&sentinel_path, "hello world\n", None)
        .expect("the file save itself must succeed; only the oplog degrades");

    let records = buf.lock().unwrap().clone();

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
