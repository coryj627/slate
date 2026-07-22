// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Quick Open ranking baselines for W1-RT-05.
//!
//! The paired 50,000-file measurements make the remediation directly
//! observable: both paths scan every file for an exact count, while
//! `top_50` retains and sorts only the host's visible page.
//!
//! Run with:
//! `cargo bench -p slate-core --bench switcher_bench`.

use std::hint::black_box;

use criterion::{Criterion, criterion_group, criterion_main};
use slate_core::switcher::{SwitcherFile, switcher_rank, switcher_rank_top};

const CORPUS_SIZE: usize = 50_000;
const DISPLAY_CAP: usize = 50;

fn corpus() -> Vec<SwitcherFile> {
    (0..CORPUS_SIZE)
        .map(|index| SwitcherFile {
            path: format!("projects/{:03}/meeting-note-{index:05}.md", index % 257),
            name: format!("meeting-note-{index:05}.md"),
        })
        .collect()
}

fn bench_switcher_rank(c: &mut Criterion) {
    let files = corpus();
    let recents = vec![
        "projects/001/meeting-note-00001.md".to_owned(),
        "projects/002/meeting-note-00002.md".to_owned(),
    ];
    let mut group = c.benchmark_group("switcher_rank_50000");
    group.sample_size(10);

    group.bench_function("full", |b| {
        b.iter(|| {
            black_box(switcher_rank(
                black_box(&files),
                black_box("note"),
                black_box(&recents),
            ))
        })
    });
    group.bench_function("top_50", |b| {
        b.iter(|| {
            black_box(switcher_rank_top(
                black_box(&files),
                black_box("note"),
                black_box(&recents),
                DISPLAY_CAP,
            ))
        })
    });
    group.finish();
}

criterion_group!(benches, bench_switcher_rank);
criterion_main!(benches);
