// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #1078 (`docs/plans/33`): the upgrade fence — no coordinated actor writes
//! the cache under a schema version other than the one it was opened at.
//! The facts simulate a NEWER process migrating (or replacing) the shared
//! cache under a live session by editing `schema_version` through a raw
//! connection, which is exactly what another process's `migrate` does to
//! this session's view of the world.

use super::common::*;
use super::*;
use std::sync::mpsc;
use std::time::Duration;

fn cache_path(root: &std::path::Path) -> std::path::PathBuf {
    root.join(".slate").join("cache.sqlite")
}

/// Another process migrated further: the cache is now at `version`.
fn move_cache_ahead(root: &std::path::Path, version: u32) {
    let conn = rusqlite::Connection::open(cache_path(root)).unwrap();
    conn.execute(
        "INSERT INTO schema_version(version, applied_at_ms, description) \
         VALUES (?1, 0, 'test: a newer process migrated')",
        [version],
    )
    .unwrap();
}

fn move_cache_back(root: &std::path::Path, version: u32) {
    let conn = rusqlite::Connection::open(cache_path(root)).unwrap();
    conn.execute("DELETE FROM schema_version WHERE version = ?1", [version])
        .unwrap();
}

fn intent_rows(session: &VaultSession) -> i64 {
    let conn = session.conn.lock().unwrap();
    conn.query_row("SELECT COUNT(*) FROM text_write_intents", [], |row| {
        row.get(0)
    })
    .unwrap()
}

fn is_skew(err: &VaultError) -> bool {
    matches!(
        err,
        VaultError::Db(crate::db::DbError::SchemaVersionSkew { .. })
    )
}

/// U1/U2/U3: once the cache moves ahead, a save is refused BEFORE the file
/// write and leaves no marker; structural ops and creates refuse the same
/// way; the error is the host-ready `Db` message.
#[test]
fn a_live_session_refuses_to_write_once_the_cache_moves_ahead() {
    let (tmp, session) = make_vault(|provider| {
        provider.write_file("note.md", b"before\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let build = crate::db::build_schema_version();
    move_cache_ahead(tmp.path(), build + 1);

    let err = session.save_text("note.md", "after\n", None).unwrap_err();
    assert!(is_skew(&err), "{err:?}");
    assert!(err.to_string().contains("restart Slate"), "{err}");
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("note.md")).unwrap(),
        "before\n",
        "refused BEFORE the file write"
    );
    assert_eq!(
        intent_rows(&session),
        0,
        "no marker is registered under a skewed schema"
    );

    let err = session.rename_file("note.md", "moved.md").unwrap_err();
    assert!(is_skew(&err), "{err:?}");
    assert!(
        tmp.path().join("note.md").is_file(),
        "the structural op did not touch disk"
    );
    let err = session.create_exclusive("new.md", "x").unwrap_err();
    assert!(is_skew(&err), "{err:?}");
    assert!(!tmp.path().join("new.md").exists());

    // U5: reads still serve.
    assert_eq!(session.read_text("note.md").unwrap(), "before\n");

    // U6: stateless — the version returning makes the same session write
    // again, with nothing to reset.
    move_cache_back(tmp.path(), build + 1);
    session.save_text("note.md", "after\n", None).unwrap();
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("note.md")).unwrap(),
        "after\n"
    );
}

/// U4: a cache BEHIND this build (a backup restored under a live session)
/// is skew too.
#[test]
fn a_cache_behind_this_build_is_skew_too() {
    let (tmp, session) = make_vault(|provider| {
        provider.write_file("note.md", b"before\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let build = crate::db::build_schema_version();
    move_cache_back(tmp.path(), build);
    let err = session.save_text("note.md", "after\n", None).unwrap_err();
    assert!(is_skew(&err), "{err:?}");
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("note.md")).unwrap(),
        "before\n"
    );
}

/// U7: `VaultSession::open` holds the structural lock across `migrate`, so
/// a migration never overlaps an in-flight structural operation in another
/// process — pinned so a refactor cannot drop it silently. The negative
/// probe is deliberately short; the liveness budget generous (#1146).
#[test]
fn open_waits_for_an_in_flight_structural_operation() {
    let (tmp, session) = make_vault(|provider| {
        provider.write_file("note.md", b"n\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let cache_dir = tmp.path().join(".slate");
    let held = VaultStructuralLock::acquire(&cache_dir).expect("hold the structural lock");

    let root = tmp.path().to_path_buf();
    let (opened_tx, opened_rx) = mpsc::channel();
    std::thread::spawn(move || {
        let result = VaultSession::from_filesystem(root).map(|_| ());
        let _ = opened_tx.send(result);
    });
    assert!(
        opened_rx.recv_timeout(Duration::from_millis(300)).is_err(),
        "open must wait behind the structural lock (it migrates under it)"
    );
    drop(held);
    opened_rx
        .recv_timeout(Duration::from_secs(30))
        .expect("open completes once the structural lock is released")
        .expect("open succeeds");
}

/// U1's pin: every cache-writer transaction in the session and the
/// bibliography writer is begun through `db::begin_fenced`; no raw
/// constructor remains (comments excepted). `db::migrate` is the deliberate
/// exception and lives in `db.rs`, outside this pin.
#[test]
fn every_cache_writer_transaction_is_fenced() {
    for (name, source) in [
        ("session.rs", include_str!("../../session.rs")),
        ("citations_db.rs", include_str!("../../citations_db.rs")),
    ] {
        let mut offenders = Vec::new();
        let mut in_tests = false;
        for (index, line) in source.lines().enumerate() {
            if line.starts_with("#[cfg(test)]") {
                in_tests = true;
            }
            if in_tests {
                continue;
            }
            let code = line.trim_start();
            if code.starts_with("//") {
                continue;
            }
            if code.contains(".transaction()")
                || code.contains("transaction_with_behavior(")
                || code.contains("unchecked_transaction(")
                || code.contains("Transaction::new_unchecked(")
            {
                offenders.push(format!("{name}:{}: {}", index + 1, code));
            }
        }
        assert!(
            offenders.is_empty(),
            "raw transaction constructors outside db::begin_fenced (#1078 U1):\n{}",
            offenders.join("\n")
        );
    }
}
