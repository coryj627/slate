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

## V1 baseline — 2026-05-24 (Milestone G Tasks, realistic fixture)

Same machine + toolchain as the 2026-05-17 row. The fixture is `generate_tasks_vault(1_000)` from `crates/slate-core/benches/common/mod.rs`. **Distribution refresh from #146** — the prior shape (every file a uniform 10-task block) over-counted real-world parser cost. The current shape mirrors a casual-user Obsidian vault:

- **~70% zero-task files** — frontmatter + headings + paragraphs + occasional code fence; no task lines. Exercises the M3 fast-path return added in #144.
- **~25% light files** — 1–3 tasks scattered through body paragraphs (not bunched in a `## Tasks` section). Exercises the parser's mid-document line walk.
- **~5% heavy files** — 10–15 tasks in a dedicated `## Tasks` block. Exercises the bulk-insert path of `replace_tasks_for_file`.

| Benchmark | 1 k files (realistic distribution) |
|---|---|
| `tasks_cold_scan` | 65.5 ms |
| `tasks_in_vault_first_page` (200 rows) | 138 µs |

| Benchmark | 1 KB doc | 10 KB doc | 50 KB doc |
|---|---|---|---|
| `parser_zero_task_overhead` | 336 ns | 3.00 µs | 14.9 µs |

_Numbers are the 95 % confidence-interval midpoint. Raw output lives in `target/criterion/`._

### Reading the new task-bench numbers

The absolute figures differ from the 2026-05-23 row (29.5 ms / 1.23 ms) — that's **not a regression**, it's a fixture change:

- **`tasks_cold_scan` went up** because the realistic fixture has much larger file bodies (frontmatter, headings, multi-paragraph prose, occasional code fences) than the prior synthetic vault. Cold scan time is dominated by file-content reading + blake3 hashing, not parsing. The 65.5 ms figure tracks real-world cold-open latency on a typical user's notes.
- **`tasks_in_vault_first_page` went down** for two reasons. First, the realistic fixture carries roughly ~1100 tasks total (25% × ~2 + 5% × ~12 per file), against ~10 000 in the prior uniform shape. Second, the expression index from migration 010 (#145) lets SQLite walk the (due, priority) sort tiers directly via `idx_tasks_sort` instead of materialising every matching row into a temp btree. Combined, the first-page query dropped from a 1.23 ms full-temp-btree baseline to 138 µs (~9× faster); the residual `USE TEMP B-TREE FOR RIGHT PART OF ORDER BY` step only sorts within (due, priority) tie-groups by path, bounded by tie-group size rather than total rows.

`parser_zero_task_overhead` is unchanged — that's a focused micro-bench on the M3 fast path and doesn't depend on the vault fixture.

### Context on `parser_zero_task_overhead`

Measures `extract_tasks` on a zero-task document (frontmatter + headings + paragraphs + markdown links — same shape as a typical no-tasks note in a real vault). With the byte-scan prefilter added in #144, the parser short-circuits before pulldown-cmark, so the cost stays in the nanosecond-to-low-microsecond range regardless of doc size. Before the prefilter, the same 50 KB doc cost ~50 µs per call (red-team measurement on PR #134) — ~3.4× speedup on the worst case. The shared `TASK_BULLETS` / `TASK_BULLET_SEPARATORS` constants added in PR #148's polish round keep the prefilter and `parse_task_line` from drifting apart on what counts as a task-line opener.

**Cold scan cost.** Adding the tasks pipeline to the scanner adds roughly the same per-file overhead as the headings / links / properties pipelines — well within the scanner's existing headroom against the V1 first-open gate. The fast-path rescan invariant (no churn on unchanged files) carries over to the tasks table; see the `fast_path_rescan_does_not_touch_tasks_table` integration test.

**Vault-wide query cost.** A first-page query (200 rows from 10 000) returns in 1.23 ms — about 5 µs per returned row. The hot path is the `(due_ms ASC NULLS LAST, priority DESC NULLS LAST, file path, ordinal)` sort, which exercises `idx_tasks_completed` and `idx_tasks_due` for filtered variants and a sequential scan + sort for the unfiltered case. Both stay well below interactive-render budget for the TasksReviewView.

## V1 baseline — 2026-05-24 (D + E perf bench extensions, issue #104)

Three new criterion scenarios establishing baselines for hot paths reshaped in PRs #98 / #100 / #102. Each PR landed without before/after measurements; these benches lock in V1 numbers so regressions surface as moved-numbers rather than tester complaints.

| Benchmark | 10 k-file vault |
|---|---|
| `full_text_search` (`Vault` scope, common token, all hits) | 251 ms |
| `files_with_property` (broad tag, first 100) | 939 µs |
| `note_load_bundle` (hub note, first 100 backlinks) | 9.5 ms |

### What each scenario measures

- **`full_text_search`** queries the FTS5 external-content index added in PR #98. The fixture's `synthetic_markdown` produces paragraphs from a fixed word pool, so any common word hits every file in the vault — exercising the worst-case snippet-generation path (10 000 hits, STX/ETX-wrapped snippets, content-table joins). This is a pessimistic bound; a real-world selective query (small number of hits) would be much faster. The V1 release-gate target of `<100 ms` was scoped to selective queries; the 251 ms here is the full-vault-token shape that #98's redesign was specifically meant to make tractable.
- **`files_with_property`** queries the partial composite index + `properties_list_values` side table added in PR #100. The fixture's `tags: [bench, file-N]` frontmatter on ~20 % of files gives ~2 000 hits for the broad `bench` tag, paged at 100 rows. The CTE-backed COUNT (#92 item 3) means the row count and the page fetch share one materialised match set instead of two.
- **`note_load_bundle`** queries the one-acquire-per-bundle shape from PR #102 against a hub note that accumulates ~9 999 backlinks (one from every other file in the linked-graph fixture). The 9.5 ms figure is the throughput baseline; the win is larger under contention from a running scanner, which isn't modelled here yet (see "Deferred" below).

### Deferred

- **NoteContentView first-paint (PR #99)** — SwiftUI layout cost, not Rust-side. Manual Instruments measurement is the right tool when it lands; documenting the protocol in an XCTest is the natural follow-up.
- **Contention shape for `note_load_bundle`** — needs a tunable mutex-hold primitive on `VaultSession` to make criterion measurements deterministic. The throughput baseline above is the regression target; under contention the bundle's lock-amortization win is strictly larger.

## V1 baseline — 2026-05-31 (ranged editor highlight, #379)

Same machine + toolchain as the 2026-05-17 row (Apple M5 Pro, 18 cores, 48 GB, macOS 26.5 / 25F71, rustc 1.95.0). The fixture is `synthetic_note(N)` in `scan_bench.rs`: frontmatter then repeated mixed blocks (heading, a prose paragraph carrying a wikilink / inline code / bold / tag / citation / link, a blockquote, and a fenced Rust block every 4th block). `bench_editor_highlight_ranged` measures `editor_spans::highlight_spans_in_range` (the #379 ranged API) against the whole-document `highlight_spans` the editor runs today.

| Benchmark | time | vs whole-doc 8 MB |
|---|---|---|
| `editor_highlight_ranged/whole_document_8mb` | 501.0 ms | 1× (baseline) |
| `editor_highlight_ranged/ranged_tail_edit_8mb` | 63.66 ms | **7.9× faster** |
| `editor_highlight_ranged/ranged_mid_edit/1mb` | 8.00 ms | — |
| `editor_highlight_ranged/ranged_mid_edit/4mb` | 26.02 ms | — |
| `editor_highlight_ranged/ranged_mid_edit/8mb` | 50.36 ms | **9.9× faster** |

_Numbers are the 95 % confidence-interval midpoint. Raw output lives in `target/criterion/`. These are the **post-review** numbers: the substring-parse-equivalence guard (`window_diverges`) was hardened against six correctness breakers found by adversarial red-teams (frontmatter-stripped-body fence parity, HTML blocks, list-content invent/grow/setext/kind-flip), which is why the ranged figure is higher than an earlier draft's — see below._

### Reading the ranged-highlight numbers (and the honest cost model)

The headline is the **absolute drop**: a span recompute around an edit on an 8 MB document falls from **501 ms to ~50 ms (~10×)**. The expensive work — 15 tree-sitter grammars over every fence, the four slate scanners, and the O(n) overlap bitmap — is scoped to a blank-line-bounded window around the edit instead of the whole document.

But the ranged path is **not strictly O(edit)**, and the numbers show exactly why:

- **It scales with document size, not edit size.** `ranged_mid_edit` is 8.0 / 26.0 / 50.4 ms at 1 / 4 / 8 MB — very nearly linear in document length. To prove the window is parseable in isolation, `window_diverges` runs pulldown-cmark over the **whole source** to compare the window's opaque-block (code/HTML) structure and list/quote container nesting against the document's. On a doc with frontmatter (this fixture has it), it runs that light pass **twice** — once on the raw source (for `markdown_spans` + the tag/comment scanners) and once on the frontmatter-stripped body (for the wikilink/citation extractors, whose fence parity the strip can change). That doubled O(document) pulldown pass is the residual cost — cheap per byte because it skips tree-sitter and the scanners, but not free and not bounded by the edit. (An earlier draft measured ~34 ms with a single raw pass; closing the frontmatter-body equivalence bug, #2 of the review, added the second pass. Correctness over a few ms.)
- **Edit position barely matters.** `ranged_tail_edit_8mb` (63.7 ms) and `ranged_mid_edit/8mb` (50.4 ms) are the same order — the light structural scan covers the whole document either way, so a tail edit costs about what a mid edit does. The `ranged_tail_edit_8mb` row exists to keep this O(document) floor visible rather than hide it behind a best-case mid-document number.

So V1's win is "eliminate the heavy per-fence/per-scanner/tree-sitter pass, keep one (or two, with frontmatter) light whole-document pulldown scans." True O(edit) — where even the structural decision is incremental — needs a cached parse and live buffer state (the stateful `DocumentBuffer`), which is deferred. This bench is the regression target that will make that future win measurable.

This is a Rust-core measurement only. PR 1 adds the API + FFI; the Mac editor still calls the whole-document path until #379 PR 2 wires the ranged call into the `NSTextStorageDelegate` edit loop. The end-to-end keystroke-latency win lands with that PR.

## V1 baseline — 2026-05-31 (#388: fuse `highlight_spans`' pulldown passes)

`highlight_spans` was making **four** independent pulldown passes over the source — `markdown_spans` (raw), `extract_links` (body), `extract_citations` (body), `code_internal_spans` (raw). #388 fuses them to **two**: one raw pass yields the structure spans + the per-token code internals; one body pass feeds both the wikilink and citation scanners from a single `collect_code_ranges`. Pure refactor — output is byte-identical (pinned by the `consolidated_matches_reference` proptest, holding at 50k cases).

The win depends on what dominates the document:

| Fixture (2 MB) | reference (4-pass) | fused (2-pass) | speedup |
|---|---|---|---|
| **prose-dominant** (headings + paragraphs + wikilinks/cites/tags/bold, no fences) | 103 ms | 81 ms | **1.27×** |
| fence-heavy (`whole_document_8mb`, a fence every 4th block) | ~501 ms | ~477 ms | ~1.05× |

On a **prose-dominant note** — the common case — pulldown parsing is the bottleneck, so halving the passes cuts ~21%. On a **fence-heavy** doc the cost is dominated by tree-sitter over every fence (unchanged by #388), so the pass-reduction is a small fraction. Either way it's a structural win that most helps the #379 PR 2 keystroke path (which runs `highlight_spans` on the edit window). Prose number is a back-to-back `Instant` timing of the fused `highlight_spans` vs the retained four-pass `highlight_spans_reference` oracle on the same 2 MB fixture; the fence-heavy row is the criterion `whole_document_8mb` midpoint before vs after (within run-to-run noise, trending faster).

## When to rerun

- After any change to `VaultSession::from_filesystem`, `scan_initial`, or `list_files`.
- After any schema migration that touches the `files` table or its indexes.
- After any non-trivial change to the `FsVaultProvider` IO surface (`list_dir`, `stat`, `read_file`).
- After any change to `editor_spans::highlight_spans` / `highlight_spans_in_range`, the scanners they compose, or the ranged safe-window / fallback logic.
- Before each milestone build that ships to testers — record the numbers in this file as a new dated baseline so regressions are visible.
