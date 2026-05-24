# Slate Benchmarks

Slate's performance harness lives in `crates/slate-core/benches/scan_bench.rs` and uses [`criterion`](https://crates.io/crates/criterion). The suite covers the Milestone A hot paths against synthetic vaults at three scales (1k / 10k / 50k Markdown files).

## How to run

```sh
make bench
```

Or directly:

```sh
cargo bench -p slate-core --bench scan_bench
```

To run a subset (criterion filters by group/benchmark name as a regex):

```sh
make bench BENCH_ARGS='first_open_and_scan/1000'
make bench BENCH_ARGS='reopen_with_cache'
```

Full-suite walltime is roughly **10–15 minutes** on a modern Apple Silicon laptop. The 50k cold-scan case dominates because generating 50k files plus hashing them sequentially is the workload's worst point.

## What's measured

| Benchmark group | What each iteration does | Sample size |
|---|---|---|
| `first_open_and_scan` | `fs::remove_dir_all(.slate)` (setup, excluded) → `VaultSession::from_filesystem` → `scan_initial`. Each measurement is a true cold start. | 10 |
| `reopen_with_cache` | Cache primed once outside the loop. Each iteration re-opens + re-scans (scanner upserts on path, so this is the steady-state warm re-open). | 20 |
| `list_files_paged` | Cache primed once. Each iteration pages through `list_files` 1 000 rows at a time until exhausted. | 20 |
| `tasks_cold_scan` | Cold scan of a 1 000-file vault carrying 10 000 task lines (10 per file). Same setup discipline as `first_open_and_scan`; measures the scanner + tasks-pipeline cost. | 10 |
| `tasks_in_vault_first_page` | Cache primed; each iteration runs `tasks_in_vault(All, first(200))` against the 10 000-task fixture. Drives the Mac TasksReviewView's initial render. | 20 |

The first three groups run for three vault sizes: **1 000**, **10 000**, **50 000** Markdown files. The Tasks groups run a single shape (1 000 files × 10 tasks).

## V1 release-gate targets

From `docs/plans/05_locked_architecture_decisions.md` §9.5. **Regressions below these thresholds are V1 release blockers.**

| Operation | 10k-note vault | 50k-note vault |
|---|---|---|
| First-open indexing | <15 s | <60 s |
| Re-open with cache | <2 s | <5 s |
| Open a note (cached) | <50 ms | <50 ms |
| Save a note + reindex | <100 ms | <200 ms |
| Structured Bases query (indexed columns) | <50 ms | <200 ms |
| Full-text search (FTS5) | <100 ms | <300 ms |
| Tree-sitter incremental reparse on keystroke | <5 ms | <5 ms |
| Memory steady state (no edit) | <100 MB desktop / <40 MB mobile | <200 MB desktop / <60 MB mobile |

The bench harness today covers **first-open indexing** and **re-open with cache** directly. The other rows land as later milestones bring up the corresponding code paths (note reader, indexed bases, FTS5, tree-sitter, etc.).

## Reading criterion output

Each line looks like:

```
first_open_and_scan/10000
                        time:   [287.28 ms 288.48 ms 293.27 ms]
```

The three numbers are the **lower bound**, **estimate**, and **upper bound** of the 95 % confidence interval. Use the middle value as the headline number; if the bounds are far apart relative to the estimate, the measurement is noisy and either the system has background load or the sample size should be bumped.

Criterion writes HTML reports (with violin plots, distribution histograms, and regression-versus-baseline comparisons) to `target/criterion/`. Open `target/criterion/report/index.html` to browse.

To **save** a run as a named baseline you can compare against later:

```sh
cargo bench -p slate-core --bench scan_bench -- --save-baseline v1-pre-tester
```

To **compare** a current run against a saved baseline:

```sh
cargo bench -p slate-core --bench scan_bench -- --baseline v1-pre-tester
```

## V1 baseline — 2026-05-17

Recorded against the synthetic fixture in `crates/slate-core/benches/common/mod.rs`. File-size distribution: ~60 % small (0.5–2 KB), ~30 % medium (5–20 KB), ~10 % large (50–200 KB), with frontmatter on ~20 % of files and occasional code blocks. Files are spread across up to 50 subdirectories.

**Machine.** Apple M5 Pro (18 cores), 48 GB RAM, Apple Fabric SSD, macOS 26.5 (25F71). **Toolchain.** rustc 1.95.0 (59807616e 2026-04-14), release profile.

| Benchmark | 1 000 files | 10 000 files | 50 000 files | V1 gate (10k / 50k) |
|---|---|---|---|---|
| `first_open_and_scan` | 24.6 ms | 295.5 ms | 1.557 s | <15 s / <60 s |
| `reopen_with_cache` | 5.5 ms | 53.0 ms | 269 ms | <2 s / <5 s |
| `list_files_paged` | 131 µs | 1.66 ms | 15.86 ms | n/a |

_Numbers are the 95 % confidence-interval midpoint. Raw output lives in `target/criterion/`; rerun `make bench` to refresh._

**Headroom against V1 gates.** First-open beats the 10k target by ~50× and the 50k target by ~38×. Re-open with cache beats by ~38× and ~19× respectively after the mtime/size/ctime skip optimization landed (initial baseline was ~8× / ~3.4×).

**Re-open is now decoupled from vault size.** The scanner's fast path skips blake3 hashing entirely for files whose on-disk `(mtime_ms, size_bytes, ctime_ms)` already matches the cached row. At 50 k files, re-open dropped from 1.47 s to 0.27 s (-82 %), and that's now dominated by directory traversal + the per-file `SELECT` against SQLite, not file IO. Bench iterations also report `bytes_processed = 0` on a no-change rescan, confirming we never re-read content.

The fast path checks ctime in addition to mtime+size, which catches mtime-preserving writers like `cp -p` and `rsync -a` that the original optimization (PR #40) would have wrongly skipped. ctime adds one extra `i64` to the per-file `SELECT`, costing about 10 % vs the mtime/size-only version on 10k+ — still ~19× under the V1 50 k gate. On platforms without portable ctime access (Windows) the column stays at 0 and the fast path keeps its mtime+size semantics.

## V1 baseline — 2026-05-23 (Milestone G Tasks)

Same machine + toolchain as the 2026-05-17 row. The fixture is `generate_tasks_vault(1_000, 10)` from `crates/slate-core/benches/common/mod.rs`: 1 000 markdown notes spread across ~10 subdirectories, each note carrying a `## Tasks` block with 10 task lines that mix open / done / in-progress statuses, due dates across a 60-day window, priorities, and recurrences.

| Benchmark | 1 k files × 10 tasks (10 k tasks) |
|---|---|
| `tasks_cold_scan` | 29.5 ms |
| `tasks_in_vault_first_page` (200 rows) | 1.23 ms |

_Numbers are the 95 % confidence-interval midpoint. Raw output lives in `target/criterion/`._

**Cold scan cost.** Adding the tasks pipeline to the scanner adds roughly the same per-file overhead as the headings / links / properties pipelines — well within the scanner's existing headroom against the V1 first-open gate. The fast-path rescan invariant (no churn on unchanged files) carries over to the tasks table; see the `fast_path_rescan_does_not_touch_tasks_table` integration test.

**Vault-wide query cost.** A first-page query (200 rows from 10 000) returns in 1.23 ms — about 5 µs per returned row. The hot path is the `(due_ms ASC NULLS LAST, priority DESC NULLS LAST, file path, ordinal)` sort, which exercises `idx_tasks_completed` and `idx_tasks_due` for filtered variants and a sequential scan + sort for the unfiltered case. Both stay well below interactive-render budget for the TasksReviewView.

## When to rerun

- After any change to `VaultSession::from_filesystem`, `scan_initial`, or `list_files`.
- After any schema migration that touches the `files` table or its indexes.
- After any non-trivial change to the `FsVaultProvider` IO surface (`list_dir`, `stat`, `read_file`).
- Before each milestone build that ships to testers — record the numbers in this file as a new dated baseline so regressions are visible.
