// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Criterion benchmarks for the Milestone P graph backend (#553).
//!
//! Budgets (p0_spec §P0-4 / program locked decision 10):
//! - `linkset_change_incremental/10000`: O(changed-file), < 1 ms —
//!   the hooks ride the save path and must stay free of vault-size
//!   terms when the index is built.
//! - `graph_build`, `graph_snapshot_default_filter`,
//!   `graph_neighborhood_d2`, `metrics_full`: recorded as baselines
//!   in `BENCHMARKS.md`; no fixed gate at P0 beyond "off the save
//!   path."
//!
//! `make bench` runs scan_bench only — invoke this file directly:
//! `cargo bench -p slate-core --bench graph_bench`.

use std::hint::black_box;

use criterion::{BenchmarkId, Criterion, criterion_group, criterion_main};

use slate_core::graph::GraphFilter;
use slate_core::{CancelToken, VaultSession};

// This bench uses only the linked-vault generator; the other fixture
// fns in common/ are for scan_bench.
#[allow(dead_code)]
mod common;
use common::generate_linked_vault;

const SIZES: &[usize] = &[1_000, 10_000, 50_000];

/// Open + scan + first graph query so every iteration below measures
/// warm-index behavior only.
fn primed_session(size: usize) -> (tempfile::TempDir, VaultSession) {
    let vault = generate_linked_vault(size);
    let session = VaultSession::from_filesystem(vault.path().to_path_buf()).expect("open");
    session.scan_initial(&CancelToken::new()).expect("scan");
    (vault, session)
}

fn bench_graph_build(c: &mut Criterion) {
    let mut group = c.benchmark_group("graph_build");
    group.sample_size(10);
    for &size in SIZES {
        let (_vault, session) = primed_session(size);
        group.bench_with_input(BenchmarkId::from_parameter(size), &size, |b, _| {
            // Cold build each iteration: drop the index (bench seam),
            // then the snapshot's first-query path rebuilds from
            // SQLite. Warm snapshot cost is measured separately in
            // `graph_snapshot_default_filter`; the difference is the
            // build itself.
            b.iter(|| {
                session.graph_drop_for_bench();
                black_box(
                    session
                        .graph_snapshot(GraphFilter::default())
                        .expect("snapshot"),
                )
            })
        });
    }
    group.finish();
}

fn bench_graph_snapshot_default_filter(c: &mut Criterion) {
    let mut group = c.benchmark_group("graph_snapshot_default_filter");
    {
        let size = 10_000usize;
        let (_vault, session) = primed_session(size);
        let _ = session
            .graph_snapshot(GraphFilter::default())
            .expect("warm");
        group.bench_with_input(BenchmarkId::from_parameter(size), &size, |b, _| {
            b.iter(|| {
                black_box(
                    session
                        .graph_snapshot(GraphFilter::default())
                        .expect("snapshot"),
                )
            })
        });
    }
    group.finish();
}

fn bench_graph_neighborhood_d2(c: &mut Criterion) {
    let mut group = c.benchmark_group("graph_neighborhood_d2");
    {
        let size = 10_000usize;
        let (_vault, session) = primed_session(size);
        let snap = session
            .graph_snapshot(GraphFilter::default())
            .expect("warm");
        // The hub note has ~everything linking at it — worst-case
        // neighborhood.
        let hub = snap
            .nodes
            .iter()
            .max_by_key(|n| n.in_links)
            .and_then(|n| n.path.clone())
            .expect("hub path");
        group.bench_with_input(BenchmarkId::from_parameter(size), &size, |b, _| {
            b.iter(|| {
                black_box(
                    session
                        .graph_neighborhood(&hub, 2, GraphFilter::default())
                        .expect("neighborhood"),
                )
            })
        });
    }
    group.finish();
}

fn bench_metrics_full(c: &mut Criterion) {
    let mut group = c.benchmark_group("metrics_full");
    group.sample_size(10);
    for &size in &[10_000usize, 50_000] {
        let (_vault, session) = primed_session(size);
        let _ = session
            .graph_snapshot(GraphFilter::default())
            .expect("warm");
        group.bench_with_input(BenchmarkId::from_parameter(size), &size, |b, _| {
            b.iter(|| {
                session.graph_metrics_drop_for_bench();
                black_box(session.graph_metrics_snapshot().expect("metrics"))
            })
        });
    }
    group.finish();
}

/// The shared save loop for the two linkset benches below: one file's
/// links replaced through the real save path, alternating bodies so
/// every iteration is a genuine content change (diff + op-log append
/// + full derivative reindex included).
fn linkset_save_loop(b: &mut criterion::Bencher<'_>, session: &VaultSession, flip: &mut bool) {
    b.iter(|| {
        *flip = !*flip;
        let body = if *flip {
            "[[note-00000005]] ![[note-00000006]] [[nowhere-at-all]]"
        } else {
            "[[note-00000007]] [[note-00000007]]"
        };
        black_box(
            session
                .save_text("notes/001/note-00000001.md", body, None)
                .expect("save"),
        )
    })
}

fn bench_linkset_change_incremental(c: &mut Criterion) {
    let mut group = c.benchmark_group("linkset_change_incremental");
    {
        let size = 10_000usize;
        let (_vault, session) = primed_session(size);
        let _ = session
            .graph_snapshot(GraphFilter::default())
            .expect("warm");
        let mut flip = false;
        group.bench_with_input(BenchmarkId::from_parameter(size), &size, |b, _| {
            // Index LIVE: the save carries the incremental graph
            // maintenance. The budget (< 1 ms of graph work at 10k,
            // O(changed-file)) is the DELTA against the unbuilt
            // control below — the save itself dominates both.
            linkset_save_loop(b, &session, &mut flip)
        });
    }
    group.finish();
}

fn bench_linkset_change_unbuilt(c: &mut Criterion) {
    let mut group = c.benchmark_group("linkset_change_unbuilt");
    {
        let size = 10_000usize;
        // Identical loop, but the graph index is never built — hooks
        // are no-ops (DoD §P-E: cold sessions pay zero). This is the
        // control for the incremental group's delta.
        let (_vault, session) = primed_session(size);
        let mut flip = false;
        group.bench_with_input(BenchmarkId::from_parameter(size), &size, |b, _| {
            linkset_save_loop(b, &session, &mut flip)
        });
    }
    group.finish();
}

criterion_group!(
    benches,
    bench_graph_build,
    bench_graph_snapshot_default_filter,
    bench_graph_neighborhood_d2,
    bench_metrics_full,
    bench_linkset_change_incremental,
    bench_linkset_change_unbuilt
);
criterion_main!(benches);
