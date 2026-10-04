// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Criterion benchmarks for the deterministic force-layout kernel
//! (Milestone P, P2-1 #557).
//!
//! Budgets (p2_spec §P2-1 / program locked decision 10):
//! - `layout_cold/10000`: full cold solve (300 iters) ≤ 3 s
//!   single-threaded release on Apple silicon (Barnes–Hut tier).
//! - `layout_warm_tick/300`: a single settled-graph force pass < 2 ms.
//! - `layout_cold/{300, 1500}`: recorded baselines in `BENCHMARKS.md`.
//! - `layout_warm_frame/1500`: `step(20)` after a fresh 300-iteration
//!   settle, over the Windows fixture's ring-and-hub tier-A topology.
//!
//! Run directly: `cargo bench -p slate-core --bench layout_bench`.

use std::hint::black_box;

use criterion::{BatchSize, BenchmarkId, Criterion, criterion_group, criterion_main};
use slate_core::graph::{GraphFilter, GraphIndex};
use slate_core::graph_layout::{LayoutConfig, LayoutEngine, LayoutForces};

fn splitmix64(x: u64) -> u64 {
    let mut z = x.wrapping_add(0x9E37_79B9_7F4A_7C15);
    z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
    z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
    z ^ (z >> 31)
}

/// A deterministic synthetic graph: `n` notes with ~`2n` seeded-random
/// links — the same shape the layout unit tests use, sized for the
/// cold/warm budgets.
fn synthetic(n: usize) -> GraphIndex {
    let paths: Vec<String> = (0..n).map(|i| format!("notes/g{i:06}.md")).collect();
    let refs: Vec<&str> = paths.iter().map(|s| s.as_str()).collect();
    let mut state = 0xBEEF_u64 ^ n as u64;
    let mut next = || {
        state = splitmix64(state);
        state
    };
    let mut links = Vec::with_capacity(n * 2);
    for _ in 0..(n * 2) {
        let a = (next() % n as u64) as usize;
        let b = (next() % n as u64) as usize;
        if a != b {
            links.push((a, b));
        }
    }
    GraphIndex::from_test_links(&refs, &links)
}

fn bench_layout_cold(c: &mut Criterion) {
    let mut group = c.benchmark_group("layout_cold");
    group.sample_size(10);
    for &n in &[300usize, 1_500, 10_000] {
        let g = synthetic(n);
        group.bench_with_input(BenchmarkId::from_parameter(n), &n, |b, _| {
            b.iter(|| {
                let mut e = LayoutEngine::new(
                    &g,
                    &GraphFilter::default(),
                    LayoutForces::default(),
                    LayoutConfig::default(),
                );
                black_box(e.step(300));
            })
        });
    }
    group.finish();
}

fn bench_layout_warm_tick(c: &mut Criterion) {
    let mut group = c.benchmark_group("layout_warm_tick");
    let n = 300usize;
    let g = synthetic(n);
    group.bench_with_input(BenchmarkId::from_parameter(n), &n, |b, _| {
        // Fresh, fully-settled engine per batch; time a single tick.
        b.iter_batched(
            || {
                let mut e = LayoutEngine::new(
                    &g,
                    &GraphFilter::default(),
                    LayoutForces::default(),
                    LayoutConfig::default(),
                );
                e.step(300);
                e
            },
            |mut e| {
                black_box(e.step(1));
            },
            BatchSize::SmallInput,
        )
    });
    group.finish();
}

fn bench_layout_warm_frame(c: &mut Criterion) {
    let mut group = c.benchmark_group("layout_warm_frame");
    group.sample_size(10);
    let n = 1_500usize;
    let paths: Vec<String> = (0..n).map(|i| format!("note{i}.md")).collect();
    let refs: Vec<&str> = paths.iter().map(String::as_str).collect();
    let links: Vec<(usize, usize)> = (0..n).flat_map(|i| [(i, (i + 1) % n), (i, 0)]).collect();
    let graph = GraphIndex::from_test_links(&refs, &links);
    group.bench_with_input(BenchmarkId::from_parameter(n), &n, |b, _| {
        b.iter_batched_ref(
            || {
                let mut engine = LayoutEngine::new(
                    &graph,
                    &GraphFilter::default(),
                    LayoutForces::default(),
                    LayoutConfig::default(),
                );
                engine.step(300);
                engine
            },
            |engine| {
                let before = engine.iteration();
                let report = engine.step(20);
                // The production convergence predicate may return early.
                // Retain its report and actual iteration delta with the
                // frame workload; no continued mutation across samples.
                black_box((report, engine.iteration() - before));
            },
            BatchSize::PerIteration,
        );
    });
    group.finish();
}

criterion_group!(
    layout_benches,
    bench_layout_cold,
    bench_layout_warm_tick,
    bench_layout_warm_frame
);
criterion_main!(layout_benches);
