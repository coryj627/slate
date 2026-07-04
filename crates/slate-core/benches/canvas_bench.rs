// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Canvas benchmarks (Milestone T §K scale budget, t1 acceptance).
//!
//! - `canvas_parse_2000`: tolerant parse of the committed 2,000-node
//!   fixture.
//! - `canvas_derive_2000`: model derivation (containment, reading
//!   order, adjacency, summaries, spatial index) on the parsed canvas.
//! - `canvas_parse_derive_2000`: the full open path.
//! - `canvas_serialize_2000`: canonical re-emission.
//!
//! Gate: no quadratic blow-up — parse+derive must stay well under the
//! per-open interactive budget (§K keeps outline/table virtualized and
//! the renderer windowed on top of this). Numbers are recorded in
//! `BENCHMARKS.md` at each wave close.

use std::hint::black_box;

use criterion::{Criterion, criterion_group, criterion_main};

use slate_core::canvas;

const LARGE: &str = include_str!("../tests/fixtures/canvas/large_2000.canvas");

fn canvas_benches(c: &mut Criterion) {
    let (parsed, warnings) = canvas::parse(LARGE);
    assert!(warnings.is_empty());
    assert_eq!(parsed.nodes.len(), 2000);

    c.bench_function("canvas_parse_2000", |b| {
        b.iter(|| canvas::parse(black_box(LARGE)))
    });
    c.bench_function("canvas_derive_2000", |b| {
        b.iter(|| canvas::model::derive(black_box(&parsed)))
    });
    c.bench_function("canvas_parse_derive_2000", |b| {
        b.iter(|| {
            let (canvas, _) = canvas::parse(black_box(LARGE));
            canvas::model::derive(&canvas)
        })
    });
    c.bench_function("canvas_serialize_2000", |b| {
        b.iter(|| canvas::serialize::serialize(black_box(&parsed)))
    });
}

criterion_group!(benches, canvas_benches);
criterion_main!(benches);
