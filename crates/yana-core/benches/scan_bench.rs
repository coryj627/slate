//! Criterion benchmarks covering Milestone A's hot paths.
//!
//! - `first_open_and_scan`: cold open + initial scan. The `.yana`
//!   cache is wiped between iterations so each measurement is a true
//!   "first scan."
//! - `reopen_with_cache`: cache primed once; each iteration re-opens
//!   the vault and re-runs scan_initial (idempotent — the scanner
//!   upserts on path).
//! - `list_files_paged`: cache primed; each iteration pages through
//!   the full file list 1k rows at a time.
//!
//! Sizes (`1_000 / 10_000 / 50_000` files) track the per-milestone
//! benchmark targets in `docs/plans/05` §9.5. The 50k case is slow;
//! sample sizes are reduced accordingly so a full run stays under
//! ~20 minutes on a modern laptop.

use std::fs;

use criterion::{criterion_group, criterion_main, BatchSize, BenchmarkId, Criterion};

use yana_core::{CancelToken, FileFilter, Paging, VaultSession};

mod common;
use common::generate_vault;

const SIZES: &[usize] = &[1_000, 10_000, 50_000];

fn bench_first_open_and_scan(c: &mut Criterion) {
    let mut group = c.benchmark_group("first_open_and_scan");
    // Cold scan on 50k files can take ~30–60 s per sample; cap
    // measurement_time and shrink sample size so total walltime
    // stays sane.
    group.sample_size(10);

    for &size in SIZES {
        let vault = generate_vault(size);
        let vault_path = vault.path().to_path_buf();
        group.bench_with_input(BenchmarkId::from_parameter(size), &size, |b, _| {
            b.iter_batched(
                || {
                    // Drop the cache so the next iteration is a true
                    // first-open. fs::remove_dir_all is excluded from
                    // the timed routine via iter_batched semantics.
                    let _ = fs::remove_dir_all(vault_path.join(".yana"));
                    vault_path.clone()
                },
                |path| {
                    let session = VaultSession::from_filesystem(path).expect("open vault");
                    session.scan_initial(&CancelToken::new()).expect("scan")
                },
                BatchSize::SmallInput,
            );
        });
        drop(vault);
    }

    group.finish();
}

fn bench_reopen_with_cache(c: &mut Criterion) {
    let mut group = c.benchmark_group("reopen_with_cache");
    group.sample_size(20);

    for &size in SIZES {
        let vault = generate_vault(size);
        // Prime once outside the bench loop so every timed iteration
        // is a re-open against a populated index.
        {
            let session =
                VaultSession::from_filesystem(vault.path().to_path_buf()).expect("prime open");
            session
                .scan_initial(&CancelToken::new())
                .expect("prime scan");
        }
        group.bench_with_input(BenchmarkId::from_parameter(size), &size, |b, _| {
            b.iter(|| {
                let session =
                    VaultSession::from_filesystem(vault.path().to_path_buf()).expect("reopen");
                session.scan_initial(&CancelToken::new()).expect("rescan")
            });
        });
        drop(vault);
    }

    group.finish();
}

fn bench_list_files_paged(c: &mut Criterion) {
    let mut group = c.benchmark_group("list_files_paged");
    group.sample_size(20);

    for &size in SIZES {
        let vault = generate_vault(size);
        let session = VaultSession::from_filesystem(vault.path().to_path_buf()).expect("open");
        session.scan_initial(&CancelToken::new()).expect("scan");

        group.bench_with_input(BenchmarkId::from_parameter(size), &size, |b, _| {
            b.iter(|| {
                let mut cursor: Option<String> = None;
                let mut total = 0usize;
                loop {
                    let page = session
                        .list_files(
                            FileFilter::All,
                            Paging {
                                cursor: cursor.clone(),
                                limit: 1_000,
                            },
                        )
                        .expect("list_files");
                    total += page.items.len();
                    match page.next_cursor {
                        Some(c) => cursor = Some(c),
                        None => break,
                    }
                }
                total
            });
        });

        drop(session);
        drop(vault);
    }

    group.finish();
}

criterion_group!(
    benches,
    bench_first_open_and_scan,
    bench_reopen_with_cache,
    bench_list_files_paged
);
criterion_main!(benches);
