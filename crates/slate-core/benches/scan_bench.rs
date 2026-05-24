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

use slate_core::{
    extract_tasks, CancelToken, FileFilter, Paging, SearchScope, TaskFilter, VaultSession,
};

mod common;
use common::{generate_linked_vault, generate_tasks_vault, generate_vault};

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
    // Milestone G #115, refreshed #146: 1k-file vault with a
    // realistic-vault task distribution (~70% zero-task / ~25%
    // 1–3 tasks scattered / ~5% heavy 10–15-task blocks).
    //
    // The previous shape (every file got a uniform `## Tasks`
    // block with 10 tasks) was a parser-hot-path stress test;
    // the new shape measures what the cold scan actually does
    // on a typical user's vault and surfaces the M3 fast-path
    // benefit (~70% of files skip pulldown-cmark entirely).
    //
    //   - `tasks_cold_scan`: wipe `.slate` and run `scan_initial`.
    //     Realistic shape so the headline number tracks
    //     real-world cold-open latency, not a worst-case.
    //   - `tasks_in_vault_first_page`: cache primed; each iteration
    //     pulls the first page (200 rows) of the vault-wide tasks
    //     query with the All filter. Drives the TasksReviewView's
    //     initial render.
    const FILE_COUNT: usize = 1_000;

    let mut cold = c.benchmark_group("tasks_cold_scan");
    cold.sample_size(10);
    cold.bench_function("1k_files_realistic", |b| {
        let vault = generate_tasks_vault(FILE_COUNT);
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
    query.bench_function("1k_files_realistic", |b| {
        let vault = generate_tasks_vault(FILE_COUNT);
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

// =====================================================================
// D+E perf bench extensions (issue #104)
//
// Each PR landed in #92's perf pass reshaped a hot path on
// real-world reasoning, but no before/after measurements were
// captured. These three benches establish V1 baselines so
// regressions become visible as numbers move, not as testers
// complaining the app feels slow.
//
// Skipped here: PR #99's NoteContentView first-paint cost —
// that's a SwiftUI layout cost, not a Rust-side measurement.
// Documented as a follow-up in BENCHMARKS.md.
// =====================================================================

fn bench_full_text_search(c: &mut Criterion) {
    // PR #98 wins: FTS5 external-content table + STX/ETX marker
    // snippet wrapping at search time. We bench against a 10k-file
    // primed vault and search for a token that appears in most
    // files (`lorem` is in every paragraph generated by
    // `synthetic_markdown`). Single number per run; this is the
    // hot path for the Mac search overlay's "Enter to search."
    let mut group = c.benchmark_group("full_text_search");
    group.sample_size(20);

    let vault = generate_vault(10_000);
    let session = VaultSession::from_filesystem(vault.path().to_path_buf()).expect("open");
    session
        .scan_initial(&CancelToken::new())
        .expect("prime scan");

    group.bench_function("10k_files_common_token", |b| {
        b.iter(|| {
            let result = session
                .full_text_search("lorem", &SearchScope::Vault, &CancelToken::new())
                .expect("search");
            black_box(result.rows.len())
        });
    });
    drop(session);
    drop(vault);

    group.finish();
}

fn bench_files_with_property(c: &mut Criterion) {
    // PR #100 wins: `value_text_norm` partial composite index +
    // `properties_list_values` side table + CTE-backed COUNT.
    // Before the redesign, every `files_with_property` call did
    // `LEFT JOIN json_each(value_text)` per property row and
    // re-ran the join for the row count.
    //
    // Existing `generate_vault` puts `tags: [bench, file-N]`
    // frontmatter on ~20% of files (every 5th seed). At 10k
    // files that's ~2k matching files for the `bench` tag — a
    // broad-tag query, which is exactly the shape #100 was
    // optimised for.
    let mut group = c.benchmark_group("files_with_property");
    group.sample_size(20);

    let vault = generate_vault(10_000);
    let session = VaultSession::from_filesystem(vault.path().to_path_buf()).expect("open");
    session
        .scan_initial(&CancelToken::new())
        .expect("prime scan");

    group.bench_function("10k_files_broad_tag_first_100", |b| {
        b.iter(|| {
            let page = session
                .files_with_property("tags", "bench", Paging::first(100))
                .expect("files_with_property");
            black_box(page.items.len())
        });
    });
    drop(session);
    drop(vault);

    group.finish();
}

fn bench_note_load_bundle(c: &mut Criterion) {
    // PR #102 wins: single mutex acquisition for the
    // backlinks + outgoing + properties combo, vs three
    // sequential acquires under the old shape. This bench
    // captures the throughput baseline on a heavily-backlinked
    // hub note (worst case for the backlinks query).
    //
    // Contention simulation (a background thread holding the
    // mutex while the bench thread hammers note_load_bundle) is
    // deferred — the contention shape would need a tunable hold
    // primitive on `VaultSession`. The throughput number here
    // is the regression target; under contention the win is
    // strictly larger.
    let mut group = c.benchmark_group("note_load_bundle");
    group.sample_size(20);

    let vault = generate_linked_vault(10_000);
    let session = VaultSession::from_filesystem(vault.path().to_path_buf()).expect("open");
    session
        .scan_initial(&CancelToken::new())
        .expect("prime scan");

    // The hub note accumulates ~9999 backlinks; bundle query
    // pages the first 100 of them.
    group.bench_function("10k_files_hub_first_100_backlinks", |b| {
        b.iter(|| {
            let bundle = session
                .note_load_bundle("notes/000/note-00000000.md", Paging::first(100))
                .expect("note_load_bundle");
            black_box((
                bundle.backlinks.items.len(),
                bundle.outgoing_links.len(),
                bundle.properties.len(),
            ))
        });
    });
    drop(session);
    drop(vault);

    group.finish();
}

criterion_group!(
    benches,
    bench_first_open_and_scan,
    bench_reopen_with_cache,
    bench_list_files_paged,
    bench_tasks_scan_and_query,
    bench_parser_zero_task_overhead,
    bench_full_text_search,
    bench_files_with_property,
    bench_note_load_bundle,
);
criterion_main!(benches);
