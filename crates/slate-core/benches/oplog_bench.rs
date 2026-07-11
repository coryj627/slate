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

use slate_core::oplog::{OpLogEntry, append_entry, read_oplog, try_create_log};
use slate_core::oplog_compaction::{CompactionLimits, CompactionOutcome, compact_log};
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

/// §9.3.3 gate: a 50k-op log compacts in under a second (release,
/// background thread). The 50k-entry log is assembled as raw frames in
/// memory (the wire format is pinned by checked-in fixtures) over a
/// constant-size document, so hashes are real and the fold's
/// reconstruction is cheap to verify; each iteration writes a fresh
/// copy and compacts it.
fn bench_compact(c: &mut Criterion) {
    let mut group = c.benchmark_group("oplog_compact");
    group.sample_size(10);

    // --- assemble 1 snapshot + 50k replace-batches as raw frames ---
    fn frame(entry_body: &[u8]) -> Vec<u8> {
        let checksum_hex = blake3::hash(entry_body).to_hex();
        let nibble = |c: u8| -> u8 {
            match c {
                b'0'..=b'9' => c - b'0',
                b'a'..=b'f' => c - b'a' + 10,
                _ => 0,
            }
        };
        let head = &checksum_hex.as_bytes()[..8];
        let mut sum = [0u8; 4];
        for (i, pair) in head.chunks_exact(2).enumerate() {
            sum[i] = (nibble(pair[0]) << 4) | nibble(pair[1]);
        }
        let mut out = Vec::with_capacity(4 + entry_body.len() + 4);
        out.extend_from_slice(&(entry_body.len() as u32).to_le_bytes());
        out.extend_from_slice(entry_body);
        out.extend_from_slice(&sum);
        out
    }
    fn body(ts: i64, kind: u8, hb: &str, ha: &str, payload: &[u8]) -> Vec<u8> {
        let actor = b"bench";
        let mut out = Vec::new();
        out.extend_from_slice(&ts.to_le_bytes());
        out.push(kind);
        out.extend_from_slice(&(actor.len() as u16).to_le_bytes());
        out.extend_from_slice(actor);
        out.extend_from_slice(&(hb.len() as u16).to_le_bytes());
        out.extend_from_slice(hb.as_bytes());
        out.extend_from_slice(&(ha.len() as u16).to_le_bytes());
        out.extend_from_slice(ha.as_bytes());
        out.extend_from_slice(&(payload.len() as u32).to_le_bytes());
        out.extend_from_slice(payload);
        out
    }
    fn replace_batch_payload(text: &str) -> Vec<u8> {
        // op_count=1 | tag=3 (Replace) | start=0 | end=8 | len | text
        let mut out = Vec::new();
        out.extend_from_slice(&1u32.to_le_bytes());
        out.push(3);
        out.extend_from_slice(&0u64.to_le_bytes());
        out.extend_from_slice(&8u64.to_le_bytes());
        out.extend_from_slice(&(text.len() as u32).to_le_bytes());
        out.extend_from_slice(text.as_bytes());
        out
    }

    let tmp = tempfile::tempdir().unwrap();
    try_create_log(tmp.path(), "big", "bench/big.md").unwrap();
    let big_path = tmp.path().join("oplog/big.oplog");
    let mut big_bytes = std::fs::read(&big_path).unwrap(); // the v2 header
    let doc = String::from("00000000");
    let mut hash = blake3::hash(doc.as_bytes()).to_hex().to_string();
    big_bytes.extend_from_slice(&frame(&body(0, 1, "", &hash, doc.as_bytes())));
    for i in 1..=50_000i64 {
        let next = format!("{i:08}");
        let next_hash = blake3::hash(next.as_bytes()).to_hex().to_string();
        big_bytes.extend_from_slice(&frame(&body(
            i,
            2,
            &hash,
            &next_hash,
            &replace_batch_payload(&next),
        )));
        hash = next_hash;
    }
    std::fs::write(&big_path, &big_bytes).unwrap();
    assert_eq!(read_oplog(tmp.path(), "big").unwrap().len(), 50_001);

    let limits = CompactionLimits {
        threshold_bytes: 16 * 1024, // forces a deep fold
        threshold_entries: 1000,
        retention_days: u32::MAX,
    };
    group.bench_function("compact_50k_ops", |b| {
        b.iter_batched(
            || {
                std::fs::write(&big_path, &big_bytes).unwrap();
            },
            |()| {
                let outcome =
                    compact_log(tmp.path(), "big", "bench/big.md", &limits, i64::MAX - 1).unwrap();
                assert!(matches!(outcome, CompactionOutcome::Rewritten { .. }));
                black_box(outcome);
            },
            criterion::BatchSize::PerIteration,
        )
    });
    group.finish();
}

/// The save path against a pre-built 5 MiB LOG on a small file: the
/// O-2 trigger check must be arithmetic-only, so this row should match
/// `save_text_hot` within noise (o_spec §O-2 g2 — a big log must not
/// make saves slower). The log is bulked at the frame layer; the
/// compaction threshold is parked high so the worker never interferes
/// with the measurement (the trigger CHECK still runs every save).
fn bench_save_with_big_log(c: &mut Criterion) {
    use slate_core::{FsVaultProvider, SessionConfig};
    use std::sync::Arc;

    let mut group = c.benchmark_group("oplog_save_path");
    let vault = tempfile::tempdir().unwrap();
    std::fs::write(vault.path().join("trig.md"), "seed\n").unwrap();
    let cache_dir = vault.path().join(".slate");
    let mut config = SessionConfig::new(cache_dir.clone());
    config.oplog_compaction_threshold_bytes = u32::MAX;
    let provider = Arc::new(FsVaultProvider::new(vault.path().to_path_buf()));
    let session = slate_core::VaultSession::open(provider, config).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    let mut content = String::from("seed\n");
    let report = session.save_text("trig.md", &content, None).unwrap();
    let _ = report;

    // Bulk the BOUND log to ~5 MiB with valid snapshot frames appended
    // at the oplog layer (the save path never reads the log, but keep
    // the frames well-formed anyway).
    let stem = {
        let dir = cache_dir.join("oplog");
        std::fs::read_dir(&dir)
            .unwrap()
            .filter_map(|e| e.ok())
            .filter_map(|e| {
                e.file_name()
                    .to_str()
                    .and_then(|n| n.strip_suffix(".oplog").map(str::to_string))
            })
            .next()
            .unwrap()
    };
    let filler_payload = vec![0x61u8; 64 * 1024];
    let mut filler = OpLogEntry {
        timestamp_ms: 1,
        user_actor_id: "bench".into(),
        op_kind: OpKind::WholeFileReplace,
        content_hash_before: hash_of(&filler_payload),
        content_hash_after: hash_of(&filler_payload),
        payload_bytes: filler_payload,
    };
    let mut log_len = 0;
    while log_len < 5 * 1024 * 1024 {
        filler.timestamp_ms += 1;
        log_len = append_entry(&cache_dir, &stem, "trig.md", &filler).unwrap();
    }
    assert!(read_oplog(&cache_dir, &stem).unwrap().len() > 60);

    // Re-anchor: put the file at the filler tail so the next save
    // chains as a small batch (deterministic warm-path shape).
    content = String::from_utf8(filler.payload_bytes.clone()).unwrap();
    std::fs::write(vault.path().join("trig.md"), &content).unwrap();
    let mut report = session.save_text("trig.md", &content, None).unwrap();

    let mut i = 0u64;
    group.bench_function("save_text_with_trigger_check_5mib_log", |b| {
        b.iter(|| {
            i += 1;
            content.push_str(&format!("line {i}\n"));
            report = session
                .save_text("trig.md", &content, Some(&report.new_content_hash))
                .unwrap();
            black_box(&report);
        })
    });
    group.finish();
}

/// O-4 gate: a 500 KB / 2k-block pair diffs in < 50 ms (release).
fn bench_structured_diff(c: &mut Criterion) {
    let mut group = c.benchmark_group("structured_diff");
    // ~2k blocks, ~500 KB total: alternating headings/paragraphs/lists
    // with a scattering of edits between the two sides.
    let mut from = String::with_capacity(600 * 1024);
    let mut to = String::with_capacity(600 * 1024);
    for i in 0..2000 {
        let block = match i % 4 {
            0 => format!("# Section {i}\n\n"),
            1 => format!(
                "paragraph {i} with some longer filler text to bulk the block {}\n\n",
                "x".repeat(1024)
            ),
            2 => format!("- [ ] task number {i}\n"),
            _ => format!("- bullet {i}\n"),
        };
        from.push_str(&block);
        if i % 97 == 0 {
            // Edit ~1 in 97 blocks on the to-side.
            to.push_str(&block.replace("filler", "FILLER").replace("task", "TASK"));
        } else {
            to.push_str(&block);
        }
    }
    assert!(from.len() > 500 * 1024);
    group.bench_function("diff_500kb_2k_blocks", |b| {
        b.iter(|| {
            let d = slate_core::structured_diff("bench.md", "a", "b", &from, &to);
            black_box(d);
        })
    });
    group.finish();
}

criterion_group!(
    benches,
    bench_structured_diff,
    bench_append,
    bench_compact,
    bench_save_with_big_log
);
criterion_main!(benches);
