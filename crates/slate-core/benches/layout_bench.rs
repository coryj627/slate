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

criterion_group!(layout_benches, bench_layout_cold, bench_layout_warm_tick);
criterion_main!(layout_benches);
