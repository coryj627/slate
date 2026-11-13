// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Plan 44 WB-31 (#1484): two sessions in one process. The counter seams
//! that tell one session's page cache from the process's, and the guards
//! that the sessions keep their caches apart.
//!
//! libsqlite3-sys 0.38 built SQLite with `SQLITE_ENABLE_MEMORY_MANAGEMENT`,
//! under which every connection in the process drew pages from one pool:
//! WB-31's bench proved the older session's cache emptied while a newer one
//! scanned (1.51x on a 10k-note rescan). libsqlite3-sys 0.39 (#1911) builds
//! without it. A fact that reads an exact cache or process value runs its
//! body alone in a child process ([`run_alone`]), so no other test's
//! connection moves it, under either build.

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

/// A vault of `notes` notes with a `cache_pages` page cache, scanned. A
/// small cache that one session's working set fills is where a shared pool
/// shows: 2,500 of these notes in 256 pages, as the bench's 10k notes fill
/// 4,096.
fn vault_with_cache(notes: usize, cache_pages: u32) -> (tempfile::TempDir, VaultSession) {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path().to_path_buf();
    let writer = FsVaultProvider::new(root.clone());
    for index in 0..notes {
        writer
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
    let mut config = SessionConfig::new(root.join(".slate"));
    config.max_db_cache_pages = cache_pages;
    let session = VaultSession::open(Arc::new(FsVaultProvider::new(root)), config).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    (tmp, session)
}

/// #1484's mechanism, held shut: a newer session's scan leaves the older
/// session's cache exactly where it was. Under libsqlite3-sys 0.38's shared
/// pool B's scan evicted A's pages; a build that brings that pool back fails
/// here. Run alone, where no other test's connection is open.
#[test]
fn a_newer_sessions_scan_leaves_the_older_sessions_cache() {
    run_alone(
        "session::tests::two_sessions::a_newer_sessions_scan_leaves_the_older_sessions_cache_child",
    );
}

#[test]
fn a_newer_sessions_scan_leaves_the_older_sessions_cache_child() {
    if !is_alone_child(
        "session::tests::two_sessions::a_newer_sessions_scan_leaves_the_older_sessions_cache_child",
    ) {
        return;
    }
    let (_a_tmp, a) = vault_with_cache(2_500, 256);
    let before = a.db_cache_status().main;
    assert!(before.used > 0, "{before:?}");

    let (_b_tmp, _b) = vault_with_cache(2_500, 256);

    let after = a.db_cache_status().main;
    assert_eq!(
        after.used, before.used,
        "a newer session's scan evicted the older session's pages: the page pool is shared again"
    );
}

/// The timing half: the older session's no-change rescan with a newer one
/// open stays within 1.25x of its rescan alone, and its cache holds between
/// rescans. Runs only alone (`--release`, by name): every bulk run skips
/// `perf_guard_`.
///
/// Each phase reads its fastest of [`RESCANS`] rescans: noise on a shared
/// runner only adds time, while the shared pool slowed every rescan, the
/// fastest included. A reading over the limit is taken once more from fresh
/// sessions, and the guard fails only when both are over, so a runner slow
/// for one whole phase is not read as B's cost (#1923: the median of six
/// read 1.25 on a 46 ms rescan under 0.39).
///
/// Measured on CDESK (#1923), fastest-rescan ratios at this size: 1.54 to
/// 1.56 under libsqlite3-sys 0.38's shared pool, 0.98 to 1.01 under 0.39. A
/// larger vault narrows the gap (1.09 to 1.11 at 10k notes in 1,024 pages),
/// so the guard stays at 2,500 notes in 256 pages.
#[test]
fn perf_guard_two_sessions_do_not_evict_each_others_cache() {
    const LIMIT: f64 = 1.25;
    let first = two_sessions_reading();
    let reading = if first.ratio() <= LIMIT {
        first
    } else {
        let second = two_sessions_reading();
        assert!(
            second.ratio() <= LIMIT,
            "A rescanned {:.2}x then {:.2}x slower with B open ({:.4}s / {:.4}s, then {:.4}s / {:.4}s)",
            first.ratio(),
            second.ratio(),
            first.with_b,
            first.alone,
            second.with_b,
            second.alone,
        );
        second
    };
    assert!(
        reading.used_after * 10 >= reading.used_before * 9,
        "A's cache fell between its rescans: {} -> {}",
        reading.used_before,
        reading.used_after
    );
}

/// Rescans per phase of [`two_sessions_reading`].
const RESCANS: usize = 12;

/// One reading of [`perf_guard_two_sessions_do_not_evict_each_others_cache`],
/// from fresh sessions: A's fastest rescan alone, then with B open.
struct TwoSessionsReading {
    alone: f64,
    with_b: f64,
    used_before: i64,
    used_after: i64,
}

impl TwoSessionsReading {
    fn ratio(&self) -> f64 {
        self.with_b / self.alone
    }
}

fn two_sessions_reading() -> TwoSessionsReading {
    const NOTES: usize = 2_500;
    const PAGES: u32 = 256;
    let fastest = |session: &VaultSession| -> f64 {
        (0..RESCANS)
            .map(|_| {
                let started = std::time::Instant::now();
                session
                    .rescan_with_progress(&CancelToken::new(), None)
                    .unwrap();
                started.elapsed().as_secs_f64()
            })
            .fold(f64::INFINITY, f64::min)
    };
    let (_a_tmp, a) = vault_with_cache(NOTES, PAGES);
    let alone = fastest(&a);

    let (_b_tmp, b) = vault_with_cache(NOTES, PAGES);
    let used_before = a.db_cache_status().main.used;
    let with_b = fastest(&a);
    let used_after = a.db_cache_status().main.used;
    drop(b);
    TwoSessionsReading {
        alone,
        with_b,
        used_before,
        used_after,
    }
}
