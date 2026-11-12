// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Plan 44 WB-31 (#1484): two sessions in one process. The counter seams
//! that tell one session's page cache from the process's, read always.
//!
//! With `SQLITE_ENABLE_MEMORY_MANAGEMENT` (the bundled libsqlite3-sys sets
//! it) every connection in the process draws pages from one pool, so a
//! parallel test's connection can recycle a page another session holds. A
//! fact that compares two connections' `used` exactly therefore runs its
//! body alone in a child process ([`run_alone`]); the rest compare nothing
//! across connections.

use super::common::make_vault;
use super::*;

fn scanned_vault(notes: usize) -> (tempfile::TempDir, VaultSession) {
    let (tmp, session) = make_vault(|provider| {
        for index in 0..notes {
            provider
                .write_file(
                    &format!("note{index}.md"),
                    format!(
                        "# Note {index}\n\nBody of note {index} with #tag{}.\n",
                        index % 7
                    )
                    .as_bytes(),
                )
                .unwrap();
        }
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    (tmp, session)
}

#[test]
fn db_cache_status_reads_this_connections_counters() {
    let (_tmp, session) = scanned_vault(50);
    let after_scan = session.db_cache_status();
    assert!(after_scan.main.used > 0, "{after_scan:?}");
    assert!(after_scan.main.miss > 0, "{after_scan:?}");
    assert_eq!(
        after_scan.worker, None,
        "the compactor's connection is not open yet"
    );

    let count = || -> i64 {
        let conn = session.conn.lock().unwrap();
        conn.query_row("SELECT count(*) FROM files", [], |row| row.get(0))
            .unwrap()
    };
    count();
    let before = session.db_cache_status().main;
    count();
    let after = session.db_cache_status().main;
    assert!(
        after.hit > before.hit,
        "a repeated query hit no cached page: {before:?} -> {after:?}"
    );

    session.open_worker_connection_for_tests();
    let with_worker = session.db_cache_status();
    let worker = with_worker
        .worker
        .expect("the compactor's connection is open");
    assert!(worker.used >= 0, "{with_worker:?}");
}

/// The process reading is the process's: run alone, so nothing else moves it.
#[test]
fn process_cache_status_reads_the_process_counters() {
    run_alone("session::tests::two_sessions::process_cache_status_child");
}

/// Fills an in-memory temp table of at least 8 MiB on one connection while
/// the other is untouched. `temp_store` is MEMORY, so those pages stay in the
/// connection's cache, and no LRU evicts them.
fn pad(session: &VaultSession) {
    let conn = session.conn.lock().unwrap();
    conn.execute_batch(
        "CREATE TEMP TABLE wb31_pad(x BLOB);
         WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 1024)
         INSERT INTO wb31_pad SELECT zeroblob(8192) FROM n;",
    )
    .unwrap();
}

#[test]
fn process_cache_status_child() {
    if !is_alone_child("session::tests::two_sessions::process_cache_status_child") {
        return;
    }
    const EIGHT_MIB: i64 = 8 * 1024 * 1024;
    let (_a_tmp, a) = scanned_vault(50);
    let (_b_tmp, b) = scanned_vault(50);

    let a0 = a.db_cache_status().main;
    let b0 = b.db_cache_status().main;
    let p0 = db::process_cache_status();
    pad(&b);
    let a1 = a.db_cache_status().main;
    let b1 = b.db_cache_status().main;
    let p1 = db::process_cache_status();
    let b_growth = b1.used - b0.used;
    assert_eq!(a1.used, a0.used, "A's reading moved while only B allocated");
    assert!(b_growth >= EIGHT_MIB, "B grew {b_growth} bytes");
    assert!(
        p1.memory_used - p0.memory_used >= b_growth / 2,
        "the process memory moved {} for B's {b_growth}",
        p1.memory_used - p0.memory_used
    );
    assert!(
        p1.pagecache_overflow - p0.pagecache_overflow >= b_growth / 2,
        "the page-cache overflow moved {} for B's {b_growth}",
        p1.pagecache_overflow - p0.pagecache_overflow
    );

    pad(&a);
    let a2 = a.db_cache_status().main;
    let b2 = b.db_cache_status().main;
    let p2 = db::process_cache_status();
    let a_growth = a2.used - a1.used;
    assert_eq!(b2.used, b1.used, "B's reading moved while only A allocated");
    assert!(a_growth >= EIGHT_MIB, "A grew {a_growth} bytes");
    assert!(
        p2.memory_used - p1.memory_used >= a_growth / 2,
        "the process memory moved {} for A's {a_growth}",
        p2.memory_used - p1.memory_used
    );
    assert!(
        p2.pagecache_overflow - p1.pagecache_overflow >= a_growth / 2,
        "the page-cache overflow moved {} for A's {a_growth}",
        p2.pagecache_overflow - p1.pagecache_overflow
    );
}
