//! Criterion benchmarks covering Milestone A's hot paths.
//!
//! - `first_open_and_scan`: cold open + initial scan. The `.slate`
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

use criterion::{black_box, criterion_group, criterion_main, BatchSize, BenchmarkId, Criterion};

use slate_core::{extract_tasks, CancelToken, FileFilter, Paging, TaskFilter, VaultSession};

mod common;
use common::{generate_tasks_vault, generate_vault};

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
                    let _ = fs::remove_dir_all(vault_path.join(".slate"));
                    vault_path.clone()
                },
                |path| {
                    let session = VaultSession::from_filesystem(path).expect("open vault");
                    // black_box the result so the compiler can't elide
                    // the work (criterion's iter_batched already does
                    // this on the return value, but being explicit
                    // survives refactors).
                    black_box(session.scan_initial(&CancelToken::new()).expect("scan"))
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
                black_box(session.scan_initial(&CancelToken::new()).expect("rescan"))
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
                    // black_box the per-page count so the compiler
                    // can't elide rows it observes are unused before
                    // the outer total is consumed. Wrapping the count
                    // (rather than the Vec) avoids consuming
                    // page.items and keeps `page` available for the
                    // next_cursor read below.
                    total += black_box(page.items.len());
                    match page.next_cursor {
                        Some(c) => cursor = Some(c),
                        None => break,
                    }
                }
                black_box(total)
            });
        });

        drop(session);
        drop(vault);
    }

    group.finish();
}

fn bench_tasks_scan_and_query(c: &mut Criterion) {
    // Milestone G #115: 10k tasks spread across 1k files (10 per
    // file). The bench has two timed measurements:
    //
    //   - `cold_scan`: wipe `.slate` and run `scan_initial` on a
    //     vault whose every file carries a Tasks block. This is the
    //     same shape as `first_open_and_scan` but with tasks in the
    //     mix, so we can compare scanner cost with and without the
    //     tasks pipeline running.
    //   - `tasks_in_vault_first_page`: cache primed; each iteration
    //     pulls the first page (200 rows) of the vault-wide tasks
    //     query with the All filter. Drives the TasksReviewView's
    //     initial render.
    const FILE_COUNT: usize = 1_000;
    const TASKS_PER_FILE: usize = 10;

    let mut cold = c.benchmark_group("tasks_cold_scan");
    cold.sample_size(10);
    cold.bench_function("1k_files_10k_tasks", |b| {
        let vault = generate_tasks_vault(FILE_COUNT, TASKS_PER_FILE);
        let vault_path = vault.path().to_path_buf();
        b.iter_batched(
            || {
                let _ = fs::remove_dir_all(vault_path.join(".slate"));
                vault_path.clone()
            },
            |path| {
                let session = VaultSession::from_filesystem(path).expect("open vault");
                black_box(session.scan_initial(&CancelToken::new()).expect("scan"))
            },
            BatchSize::SmallInput,
        );
        drop(vault);
    });
    cold.finish();

    let mut query = c.benchmark_group("tasks_in_vault_first_page");
    query.sample_size(20);
    query.bench_function("1k_files_10k_tasks", |b| {
        let vault = generate_tasks_vault(FILE_COUNT, TASKS_PER_FILE);
        let session = VaultSession::from_filesystem(vault.path().to_path_buf()).expect("open");
        session.scan_initial(&CancelToken::new()).expect("prime");
        b.iter(|| {
            let page = session
                .tasks_in_vault(TaskFilter::default(), Paging::first(200))
                .expect("tasks_in_vault");
            black_box(page.items.len())
        });
        drop(session);
        drop(vault);
    });
    query.finish();
}

fn bench_parser_zero_task_overhead(c: &mut Criterion) {
    // #144 fast-path measurement: how much does `extract_tasks` cost
    // on a document with zero task lines? Real vaults are dominated
    // by these — most notes don't contain tasks at all. With the
    // byte-substring prefilter the cost should drop into the
    // nanosecond range; without it, every file pays the
    // pulldown-cmark walk + line scan (~50 µs on 50 KB per the
    // red-team measurement).
    let mut group = c.benchmark_group("parser_zero_task_overhead");
    group.sample_size(100);

    for &kb in &[1usize, 10, 50] {
        let mut body = String::with_capacity(kb * 1024 + 256);
        body.push_str("---\ntitle: Test\ntags: [a, b]\n---\n\n# Heading\n\n");
        while body.len() < kb * 1024 {
            body.push_str("Lorem ipsum [link](url) dolor sit amet, consectetur adipiscing elit.\n");
            body.push_str("Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.\n\n");
            body.push_str("## Subheading\n\n");
        }
        group.bench_with_input(
            BenchmarkId::from_parameter(format!("{kb}kb")),
            &kb,
            |b, _| {
                b.iter(|| black_box(extract_tasks(black_box(&body))));
            },
        );
    }
    group.finish();
}

criterion_group!(
    benches,
    bench_first_open_and_scan,
    bench_reopen_with_cache,
    bench_list_files_paged,
    bench_tasks_scan_and_query,
    bench_parser_zero_task_overhead,
);
criterion_main!(benches);
