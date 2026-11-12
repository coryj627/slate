// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Plan 44 WB-31 (#1484): two sessions in one process.
//!
//! Opens two identical 10k-note vaults and times the older session's
//! no-change rescan alone, with the newer one open, with it closed, and
//! with a third open, reading SQLite's per-connection page-cache counters
//! around every rescan. The mechanism #1484 suspects is one process-wide
//! page-cache LRU (`SQLITE_ENABLE_MEMORY_MANAGEMENT`, which the bundled
//! libsqlite3-sys build sets). It is proven when, after B opens and scans,
//! A's cache shrank between its rescans without A running and A's misses
//! per rescan rose; it is refuted when A's cache holds and its misses stay
//! flat while any slowdown persists.
//!
//! `cargo bench -p slate-core --bench two_sessions_bench` (release, the
//! issue's profile). Prints one table; the numbers go to BENCHMARKS.md.

// The bench prints its own measurement table to its own stdout: no vault path
// or note name, which is what slate-core's macro ban (clippy.toml, #507)
// keeps out of host logs.
#![allow(clippy::disallowed_macros)]

use std::time::Instant;

use slate_core::db::{DbCacheCounters, process_cache_status};
use slate_core::{CancelToken, VaultSession};

#[allow(dead_code)] // shared with the other benches; this one uses generate_vault
mod common;
use common::generate_vault;

const FILES: usize = 10_000;
const RESCANS: usize = 8;

struct Phase {
    name: &'static str,
    median_ms: f64,
    misses_per_rescan: i64,
    hits_per_rescan: i64,
    used_before_kib: i64,
    used_after_kib: i64,
    shrank_while_idle_kib: i64,
    process_memory_mib: f64,
    process_overflow_mib: f64,
}

/// Rescans `session` `RESCANS` times; A's counters are read before the
/// first and after the last, and `idle_baseline` is A's `used` at the end
/// of the previous phase, so a fall between the two is eviction by
/// another connection while A did not run.
fn measure(
    name: &'static str,
    session: &VaultSession,
    idle_baseline: Option<DbCacheCounters>,
) -> (Phase, DbCacheCounters) {
    let before = session.db_cache_status().main;
    let mut times = Vec::with_capacity(RESCANS);
    for _ in 0..RESCANS {
        let started = Instant::now();
        session
            .rescan_with_progress(&CancelToken::new(), None)
            .expect("rescan");
        times.push(started.elapsed().as_secs_f64() * 1000.0);
    }
    let after = session.db_cache_status().main;
    times.sort_by(|a, b| a.partial_cmp(b).expect("finite times"));
    let median_ms = (times[RESCANS / 2 - 1] + times[RESCANS / 2]) / 2.0;
    let process = process_cache_status();
    let phase = Phase {
        name,
        median_ms,
        misses_per_rescan: (after.miss - before.miss) / RESCANS as i64,
        hits_per_rescan: (after.hit - before.hit) / RESCANS as i64,
        used_before_kib: before.used / 1024,
        used_after_kib: after.used / 1024,
        shrank_while_idle_kib: idle_baseline.map_or(0, |idle| (idle.used - before.used) / 1024),
        process_memory_mib: process.memory_used as f64 / (1024.0 * 1024.0),
        process_overflow_mib: process.pagecache_overflow as f64 / (1024.0 * 1024.0),
    };
    (phase, after)
}

fn open_and_scan(path: std::path::PathBuf) -> VaultSession {
    let session = VaultSession::from_filesystem(path).expect("open vault");
    session
        .scan_initial(&CancelToken::new())
        .expect("first scan");
    session
}

fn main() {
    // Criterion passes its own flags (`--bench`); this bench takes none.
    let vault_a = generate_vault(FILES);
    let vault_b = generate_vault(FILES);
    let mut phases = Vec::new();

    let a = open_and_scan(vault_a.path().to_path_buf());
    let (alone, a_end) = measure("A alone", &a, None);
    phases.push(alone);

    let b = open_and_scan(vault_b.path().to_path_buf());
    let (with_b, a_end) = measure("A with B open", &a, Some(a_end));
    phases.push(with_b);
    let (b_self, _) = measure("B (newer)", &b, None);
    phases.push(b_self);
    // The remedy's test: B idle and released, A rescans.
    b.release_idle_cache();
    let (b_released, a_end) = measure("A with B open, B released", &a, Some(a_end));
    phases.push(b_released);

    drop(b);
    let (b_closed, a_end) = measure("A after B closed", &a, Some(a_end));
    phases.push(b_closed);

    let c = open_and_scan(vault_b.path().to_path_buf());
    let (with_c, _) = measure("A with C open", &a, Some(a_end));
    phases.push(with_c);
    drop(c);

    println!(
        "WB-31 two sessions, {FILES} notes each, median of {RESCANS} no-change rescans (Windows)"
    );
    println!(
        "| phase | median ms | misses/rescan | hits/rescan | A used before (KiB) | A used after (KiB) | A shrank while idle (KiB) | process memory (MiB) | page-cache overflow (MiB) |"
    );
    println!("|---|---|---|---|---|---|---|---|---|");
    for phase in &phases {
        println!(
            "| {} | {:.1} | {} | {} | {} | {} | {} | {:.1} | {:.1} |",
            phase.name,
            phase.median_ms,
            phase.misses_per_rescan,
            phase.hits_per_rescan,
            phase.used_before_kib,
            phase.used_after_kib,
            phase.shrank_while_idle_kib,
            phase.process_memory_mib,
            phase.process_overflow_mib,
        );
    }
    let alone_ms = phases[0].median_ms;
    let ratio = |name: &str| {
        phases
            .iter()
            .find(|phase| phase.name == name)
            .map_or(f64::NAN, |phase| phase.median_ms / alone_ms)
    };
    println!(
        "ratio to A alone: with B open {:.2}; with B open and released {:.2}; with C open {:.2}",
        ratio("A with B open"),
        ratio("A with B open, B released"),
        ratio("A with C open")
    );
}
