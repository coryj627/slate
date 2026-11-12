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

/// A vault of `notes` notes opened with `idle_cache_release_ms` set (0 keeps
/// it out of the registry, as the test build's default does), scanned.
pub(super) fn opted_in_vault(
    notes: usize,
    idle_ms: u32,
    cache_pages: u32,
) -> (tempfile::TempDir, VaultSession) {
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
    config.idle_cache_release_ms = idle_ms;
    config.max_db_cache_pages = cache_pages;
    let session = VaultSession::open(Arc::new(FsVaultProvider::new(root)), config).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    (tmp, session)
}

/// `release_idle_cache` frees this session's cache and records the release.
/// SQLite's shrink empties the whole shared pool, so B's unused pages go
/// too (measured here first: B fell from 527 KiB to 5 KiB): the registry
/// therefore releases a slot once per idle period, never at every pass. Run
/// alone, where nothing else moves A's reading.
#[test]
fn release_idle_cache_drops_this_connections_cache() {
    run_alone(
        "session::tests::two_sessions::release_idle_cache_drops_this_connections_cache_child",
    );
}

#[test]
fn release_idle_cache_drops_this_connections_cache_child() {
    if !is_alone_child(
        "session::tests::two_sessions::release_idle_cache_drops_this_connections_cache_child",
    ) {
        return;
    }
    let (_a_tmp, a) = scanned_vault(200);
    let (_b_tmp, _b) = scanned_vault(200);
    let a0 = a.db_cache_status().main;
    assert!(a0.used > 0, "{a0:?}");

    a.release_idle_cache();

    let a1 = a.db_cache_status().main;
    assert!(
        a1.used * 10 <= a0.used,
        "A's cache {} -> {}",
        a0.used,
        a1.used
    );
    assert_eq!(
        a.conn.releases_for_tests(),
        1,
        "the release was not recorded"
    );
}
/// A released session rescans exactly as before: the release frees pages,
/// never the connection's functions or state.
#[test]
fn an_idle_session_releases_its_cache_and_rescans_correctly() {
    let (_tmp, session) = scanned_vault(40);
    let count = |session: &VaultSession| -> i64 {
        let conn = session.conn.lock().unwrap();
        conn.query_row("SELECT count(*) FROM files", [], |row| row.get(0))
            .unwrap()
    };
    let before_count = count(&session);
    let before = session
        .rescan_with_progress(&CancelToken::new(), None)
        .unwrap();

    session.release_idle_cache();

    let after = session
        .rescan_with_progress(&CancelToken::new(), None)
        .unwrap();
    assert_eq!(count(&session), before_count);
    let summary = |report: &ScanReport| {
        (
            report.files_seen,
            report.files_indexed,
            report.files_skipped,
            report.error_count,
        )
    };
    assert_eq!(summary(&after), summary(&before), "{before:?} -> {after:?}");
}

/// The remedy's timing guard: the older session's rescan with a newer one
/// open and idle (and so released) stays within 1.25x of its rescan alone,
/// and its cache holds between rescans. Runs only alone (`--release`, by
/// name): every bulk run skips `perf_guard_`. WB-31's bench measured 1.51x
/// before the remedy and 1.00x with the idle session released.
#[test]
fn perf_guard_two_sessions_do_not_evict_each_others_cache() {
    // A cache one session's working set fills (A alone holds about 1 MiB in
    // 256 pages), as the bench's 10k notes fill 4,096: with B open and the
    // release off, A rescanned 1.44x slower; with it, 0.94x. At 512 pages A
    // fits beside B and the guard could not fail.
    const NOTES: usize = 2_500;
    const PAGES: u32 = 256;
    const RESCANS: usize = 6;
    const IDLE_MS: u32 = 200;
    let median = |session: &VaultSession| -> f64 {
        let mut times: Vec<f64> = (0..RESCANS)
            .map(|_| {
                let started = std::time::Instant::now();
                session
                    .rescan_with_progress(&CancelToken::new(), None)
                    .unwrap();
                started.elapsed().as_secs_f64()
            })
            .collect();
        times.sort_by(|x, y| x.partial_cmp(y).unwrap());
        (times[RESCANS / 2 - 1] + times[RESCANS / 2]) / 2.0
    };
    let (_a_tmp, a) = opted_in_vault(NOTES, IDLE_MS, PAGES);
    let alone = median(&a);

    let (_b_tmp, b) = opted_in_vault(NOTES, IDLE_MS, PAGES);
    // B goes idle; the release thread frees its cache a full interval after.
    std::thread::sleep(std::time::Duration::from_millis(u64::from(IDLE_MS) * 4));
    let warm_up = a.rescan_with_progress(&CancelToken::new(), None).unwrap();
    let used_before = a.db_cache_status().main.used;
    let with_b = median(&a);
    let used_after = a.db_cache_status().main.used;
    drop(b);
    drop(warm_up);

    let ratio = with_b / alone;
    assert!(
        ratio <= 1.25,
        "A rescanned {ratio:.2}x slower with B open ({with_b:.3}s / {alone:.3}s)"
    );
    assert!(
        used_after * 10 >= used_before * 9,
        "A's cache fell between its rescans: {used_before} -> {used_after}"
    );
}
