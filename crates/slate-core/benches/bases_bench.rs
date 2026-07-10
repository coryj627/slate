// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Bases session benchmarks (Milestone N, #699).
//!
//! The benches exercise the public `VaultSession` API rather than the lower
//! engine directly, so the measured path includes handle lookup, query
//! execution, result mirroring, quick filtering, and export formatting.

use std::fs;
use std::hint::black_box;
use std::time::Duration;

use criterion::{BatchSize, BenchmarkId, Criterion, criterion_group, criterion_main};
use tempfile::TempDir;

use slate_core::{CancelToken, ExportFormat, VaultSession, bases};

const SIZES: &[usize] = &[1_000, 10_000, 50_000];
const FULL_BASE_PATH: &str = "Queries/BenchFull.base";
const GATE_BASE_PATH: &str = "Queries/BenchGate.base";
const FULL_BASE_TEXT: &str = r#"properties:
  status:
    displayName: Status
  priority:
    displayName: Priority
views:
  - type: table
    name: Full
    filters: "file.inFolder(\"Notes\")"
    order:
      - file.name
      - status
      - priority
    slate:
      sort:
        - expr: file.name
          direction: asc
"#;
const GATE_BASE_TEXT: &str = r#"properties:
  status:
    displayName: Status
  priority:
    displayName: Priority
views:
  - type: table
    name: Gate
    limit: 100
    filters: "file.inFolder(\"Notes/000\")"
    order:
      - file.name
      - status
      - priority
    summaries:
      status: count
    slate:
      sort:
        - expr: file.name
          direction: asc
"#;

struct BasesBenchVault {
    _tmp: TempDir,
    session: VaultSession,
}

fn bases_benches(c: &mut Criterion) {
    let mut group = c.benchmark_group("bases_session");
    group.warm_up_time(Duration::from_millis(500));
    group.measurement_time(Duration::from_secs(2));

    for &size in SIZES {
        let vault = prepare_bases_vault(size);

        group.bench_with_input(
            BenchmarkId::new("indexed_query_gate_uncached", size),
            &size,
            |b, _| {
                b.iter_batched(
                    || vault.session.open_base(GATE_BASE_PATH).expect("open base"),
                    |handle| {
                        let result = vault
                            .session
                            .base_execute(handle, 0, None, None, &CancelToken::new())
                            .expect("execute base");
                        vault.session.close_base(handle);
                        black_box(result)
                    },
                    BatchSize::SmallInput,
                )
            },
        );

        let cache_handle = vault.session.open_base(GATE_BASE_PATH).expect("open base");
        vault
            .session
            .base_execute(cache_handle, 0, None, None, &CancelToken::new())
            .expect("prime gate base cache");
        group.bench_with_input(
            BenchmarkId::new("cache_hit_reexecute", size),
            &size,
            |b, _| {
                b.iter(|| {
                    black_box(
                        vault
                            .session
                            .base_execute(cache_handle, 0, None, None, &CancelToken::new())
                            .expect("cache-hit base"),
                    )
                })
            },
        );
        vault.session.close_base(cache_handle);

        let quick_handle = vault.session.open_base(GATE_BASE_PATH).expect("open base");
        group.bench_with_input(
            BenchmarkId::new("quick_filter_display_values", size),
            &size,
            |b, _| {
                b.iter(|| {
                    black_box(
                        vault
                            .session
                            .base_execute(
                                quick_handle,
                                0,
                                None,
                                Some("active".to_string()),
                                &CancelToken::new(),
                            )
                            .expect("quick filter base"),
                    )
                })
            },
        );
        vault.session.close_base(quick_handle);

        group.bench_with_input(BenchmarkId::new("export_csv_gate", size), &size, |b, _| {
            b.iter_batched(
                || vault.session.open_base(GATE_BASE_PATH).expect("open base"),
                |handle| {
                    let csv = vault
                        .session
                        .base_export(handle, 0, ExportFormat::Csv, None)
                        .expect("export csv");
                    vault.session.close_base(handle);
                    black_box(csv)
                },
                BatchSize::SmallInput,
            )
        });

        group.bench_with_input(
            BenchmarkId::new("export_csv_full_diagnostic", size),
            &size,
            |b, _| {
                b.iter_batched(
                    || {
                        vault
                            .session
                            .open_base(FULL_BASE_PATH)
                            .expect("open full base")
                    },
                    |handle| {
                        let csv = vault
                            .session
                            .base_export(handle, 0, ExportFormat::Csv, None)
                            .expect("export full csv");
                        vault.session.close_base(handle);
                        black_box(csv)
                    },
                    BatchSize::SmallInput,
                )
            },
        );

        drop(vault);
    }

    group.finish();

    c.bench_function("bases_format/parse_serialize_roundtrip", |b| {
        b.iter(|| {
            let (base, warnings) = bases::parse_base(black_box(GATE_BASE_TEXT));
            assert!(warnings.is_empty());
            black_box(bases::serialize_base(&base, &[]).expect("serialize base"))
        })
    });
}

fn prepare_bases_vault(file_count: usize) -> BasesBenchVault {
    let tmp = generate_bases_vault(file_count);
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).expect("open vault");
    session
        .scan_initial(&CancelToken::new())
        .expect("scan vault");
    BasesBenchVault { _tmp: tmp, session }
}

fn generate_bases_vault(file_count: usize) -> TempDir {
    let tmp = tempfile::tempdir().expect("create tempdir for bases vault");
    fs::create_dir_all(tmp.path().join("Queries")).expect("create queries dir");
    fs::write(tmp.path().join(FULL_BASE_PATH), FULL_BASE_TEXT).expect("write full bench base");
    fs::write(tmp.path().join(GATE_BASE_PATH), GATE_BASE_TEXT).expect("write gate bench base");

    let subdir_count = (file_count / 100).clamp(1, 50);
    for i in 0..file_count {
        let dir = tmp.path().join(format!("Notes/{:03}", i % subdir_count));
        fs::create_dir_all(&dir).expect("create notes dir");
        let status = match i % 4 {
            0 => "active",
            1 => "waiting",
            2 => "done",
            _ => "archived",
        };
        let priority = (i % 5) + 1;
        fs::write(
            dir.join(format!("note-{i:08}.md")),
            format!(
                "---\nstatus: {status}\npriority: {priority}\n---\n# Note {i}\n\nSynthetic Bases benchmark note {i}.\n"
            ),
        )
        .expect("write note");
    }

    tmp
}

criterion_group! {
    name = benches;
    // Keep the normal 50k-vault run practical while allowing Criterion's
    // `--sample-size` argument to replace the runner default for every group.
    config = Criterion::default().sample_size(10);
    targets = bases_benches
}
criterion_main!(benches);
