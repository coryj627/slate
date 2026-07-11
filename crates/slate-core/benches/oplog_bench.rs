// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Criterion benchmarks for the op-log append path (O-1 #539).
//!
//! - `oplog_append/plain_batch`: one save-shaped `EditBatch` append —
//!   the pre-O-1 cost shape and the baseline.
//! - `oplog_append/annotated_batch`: the same batch wrapped in a
//!   kind-4 `Annotated` entry carrying three annotations — the O-1
//!   DoD gate is < 10% overhead over `plain_batch` (recorded in the
//!   PR alongside absolute numbers at the #404 baseline resolution).
//! - `oplog_append/save_text_hot`: the full session save path against
//!   a warm file (diff + encode + append + fsync), anchoring the
//!   absolute cost the two micro rows sit inside.
//!
//! Run: `cargo bench -p slate-core --bench oplog_bench`
//! (not part of `make bench`, which pins scan_bench only).

use std::hint::black_box;

use criterion::{Criterion, criterion_group, criterion_main};

use slate_core::oplog::{OpLogEntry, append_entry, try_create_log};
use slate_core::{
    CancelToken, EditOp, OpAnnotation, OpKind, VaultSession, encode_annotated, encode_edit_batch,
};

fn hash_of(bytes: &[u8]) -> String {
    // blake3 hex via the same helper the session uses.
    blake3::hash(bytes).to_hex().to_string()
}

fn sample_batch() -> Vec<u8> {
    encode_edit_batch(&[
        EditOp::Insert {
            pos: 100,
            text: "an inserted sentence of realistic length\n".into(),
        },
        EditOp::Delete {
            start: 400,
            end: 450,
        },
        EditOp::Replace {
            start: 900,
            end: 910,
            text: "replacement".into(),
        },
    ])
}

fn sample_annotations() -> Vec<OpAnnotation> {
    vec![
        OpAnnotation::SetProperty {
            key: "status".into(),
            value_json: "\"final\"".into(),
        },
        OpAnnotation::ToggleTask {
            ordinal: 4,
            new_status: 'x',
        },
        OpAnnotation::FrontmatterReplace,
    ]
}

fn entry(kind: OpKind, payload: Vec<u8>, ts: i64) -> OpLogEntry {
    OpLogEntry {
        timestamp_ms: ts,
        user_actor_id: "bench".into(),
        op_kind: kind,
        content_hash_before: hash_of(b"before"),
        content_hash_after: hash_of(b"after"),
        payload_bytes: payload,
    }
}

fn bench_append(c: &mut Criterion) {
    let mut group = c.benchmark_group("oplog_append");

    let tmp = tempfile::tempdir().unwrap();
    try_create_log(tmp.path(), "plain", "bench/plain.md").unwrap();
    let mut ts = 0i64;
    group.bench_function("plain_batch", |b| {
        b.iter(|| {
            ts += 1;
            let e = entry(OpKind::EditBatch, sample_batch(), ts);
            black_box(append_entry(tmp.path(), "plain", "bench/plain.md", &e).unwrap());
        })
    });

    try_create_log(tmp.path(), "annotated", "bench/annotated.md").unwrap();
    group.bench_function("annotated_batch", |b| {
        b.iter(|| {
            ts += 1;
            let payload =
                encode_annotated(OpKind::EditBatch, &sample_batch(), &sample_annotations());
            let e = entry(OpKind::Annotated, payload, ts);
            black_box(append_entry(tmp.path(), "annotated", "bench/annotated.md", &e).unwrap());
        })
    });
    group.finish();

    // Full save-path anchor: warm file, small edit each iteration.
    let mut group = c.benchmark_group("oplog_save_path");
    let vault = tempfile::tempdir().unwrap();
    std::fs::write(vault.path().join("hot.md"), "seed\n").unwrap();
    let session = VaultSession::from_filesystem(vault.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    let mut content = String::from("seed\n");
    let mut report = session.save_text("hot.md", &content, None).unwrap();
    let mut i = 0u64;
    group.bench_function("save_text_hot", |b| {
        b.iter(|| {
            i += 1;
            content.push_str(&format!("line {i}\n"));
            report = session
                .save_text("hot.md", &content, Some(&report.new_content_hash))
                .unwrap();
            black_box(&report);
        })
    });
    group.finish();
}

criterion_group!(benches, bench_append);
criterion_main!(benches);
