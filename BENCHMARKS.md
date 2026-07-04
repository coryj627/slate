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

## V1 baseline — 2026-05-31 (#375: per-keystroke editor highlight, Swift end-to-end)

The editor's per-keystroke highlight cost, measured **from Swift** through the real entry points the debounced `scheduleHighlight` runs: the Rust `editorHighlightSpans` FFI (the canonical syntax spans, #376/#377 — replacing the retired Swift `findEditorSyntaxSpans`) plus the Swift `findEditorEmbedSpans` overlay. This is the Swift-side complement to the Rust criterion `editor_highlight_ranged` group — it includes the **FFI marshalling** of the span array back into Swift, which the Rust bench doesn't see.

Committed as the `SLATE_BENCH`-gated `HighlightBenchmarkTests` (`XCTSkip` unless `SLATE_BENCH=1`, so normal `swift test` / CI skips it). Same machine as the other rows. **Methodology:** release dylib + `swift test -c release` (representative `-O`); because `Package.swift` hard-links `-L ../../target/debug`, the numbers were taken with release-built `libslate_uniffi` content at that path. Fixture is `representativeMarkdown` — the same mixed shape as the Rust `scan_bench` note.

| size | syntax (`editorHighlightSpans`) | embed (`findEditorEmbedSpans`) | total / keystroke | prior Swift `findEditorSyntaxSpans` (#375 issue) |
|---|---|---|---|---|
| 100 KB | 3.6 ms | 0.2 ms | **3.8 ms** | 6.5 ms |
| 1 MB | 42.7 ms | 2.0 ms | **44.7 ms** | 70.6 ms |
| 2 MB | 100.8 ms | 4.0 ms | **104.7 ms** | 182 ms |
| 8 MB | 629.8 ms | 16.2 ms | **646.0 ms** | 1 683 ms |

The Rust migration (#376/#377/#388) made the syntax pass **~1.6–2.6× faster** than the old Swift regex highlighter, and the gap widens with size (the old Swift cost was super-linear). The **FFI marshalling** of the span array is visible but modest — the 8 MB syntax figure (629.8 ms) is the Rust `highlight_spans` (~477 ms by criterion) plus ~150 ms to serialise the span `Vec` across the boundary into Swift. The embed scan (still Swift) is a small constant fraction. Smooth-typing (sub-frame) holds to ~100 KB synchronously; past that the debounced off-main pass + #379's ranged recompute are what keep the keystroke responsive — this baseline is the regression target they must beat.

> Run: `SLATE_BENCH=1 swift test -c release --filter HighlightBenchmarkTests` (debug is ~10× slower and not representative).

## V1 baseline — 2026-05-31 (#387: `extract_code_blocks` made linear)

`code::extract_code_blocks` called `line_of_offset(source, start)` once per block, and `line_of_offset` counted newlines from byte 0 — so extraction was **O(n × blocks)** (~660 ms on a 2 MB note with ~5.7k blocks; the same anti-pattern was in `diagram`/`math`). The fix is an incremental `LineTracker` that counts each newline once over the source as the in-order extractor advances (O(n)). The `extract_code_blocks` criterion group benches the issue's 500/1000/2000/4000-block counts.

| blocks | before (O(n×blocks)) | after (linear) |
|---|---|---|
| 500 | 2.7 ms | **0.090 ms** |
| 1 000 | 8.8 ms | **0.178 ms** |
| 2 000 | 25.8 ms | **0.358 ms** |
| 4 000 | 92 ms | **0.709 ms** |

After: doubling the block count ~doubles the time (the "before" column quadrupled). At 4 000 blocks it's **~130× faster**; the 2 MB / ~5.7k-block case drops from ~660 ms to ~1 ms. The "before" column is the #387 issue's measurement on the same machine class. The same `LineTracker` replaced the duplicate quadratic line-numbering in `diagram::extract_diagram_blocks` and `math::extract_math_blocks`.

## V1 baseline — 2026-05-31 (#404 Slice A: stateful `DocumentBuffer`, per-keystroke baseline)

Same machine + toolchain as the 2026-05-17 row. `bench_doc_buffer_keystroke` measures **one keystroke through the stateful buffer** — `DocBufferState::apply_edit` (a single-char insert) **plus** `highlight_in_range` (the windowed pass) — against `synthetic_note(N)` at realistic note sizes. A pre-built buffer is cloned per iteration (O(1) — the rope shares chunks via `Arc`), so each sample is one keystroke on a fixed-size document.

| scenario | time | notes |
|---|---|---|
| `doc_buffer_keystroke/mid/10kb` | 63.5 µs | — |
| `doc_buffer_keystroke/mid/100kb` | 530 µs | — |
| `doc_buffer_keystroke/mid/1mb` | 6.27 ms | — |
| `doc_buffer_keystroke/mid/8mb` | 62.6 ms | apples-to-apples with `ranged_mid_edit/8mb` (50.4 ms) |
| `doc_buffer_keystroke/tail_8mb` | 64.2 ms | ≈ mid: the structural scan is whole-doc regardless of edit position |

**This row is the Slice B regression target, and it is deliberately still ~O(n).** Slice A delivers the *foundation* (the stateful rope-backed buffer) + the editor wiring, and it removes the per-keystroke costs the **Swift** side used to pay — the whole-document string marshalled across FFI every pass and the four `TextBuffer::from_str` rope rebuilds for the dirty/applied conversions (now O(log n) on the live rope; see the #375 Swift end-to-end row for what those cost). But the *structural decision* inside `highlight_in_range` is unchanged: it materialises the rope once and calls today's `highlight_spans_in_range`, which parses block structure over the whole document (the `StructureSnapshot` oracle). So the mid rows still scale ~linearly (10 KB → 8 MB is ~63 µs → ~63 ms) and the tail row matches the mid row — exactly the O(prefix) floor #379 documented. Slice B replaces the whole-doc parse with an incrementally maintained `StructureSnapshot`, which is what flattens the mid/tail curve to O(window); this table is what proves it when it lands.

## V1 — 2026-05-31 (#404 Slice B: incremental structure + cached frontmatter body — the flatten)

Slice B lands the two structural optimizations the Slice A row above set up as its regression target:

- **Task A — reconvergence-stopped `StructureSnapshot::updated`.** `apply_edit` no longer re-parses the whole document suffix from the clean break. It re-parses only a *bounded chunk* `[r, P)` from the clean break `r ≤ edit_start` to the first **reconvergence point** `P` past the edit (a blank line where the new parse and the shifted old parse provably agree), then splices the untouched prefix (`< r`) and the untouched, byte-shifted old tail (`≥ P`) around it. O(suffix) → O(edit).
- **Task B — cached frontmatter-body framing.** The ranged highlight runs a second `window_diverges` on the *frontmatter-stripped body* (CRITICAL #2) that previously called `StructureSnapshot::from_source(body)` — an O(document) parse — on every keystroke. `DocBufferState` now caches that body snapshot and maintains it incrementally on `apply_edit` (the same reconvergence machinery, body-local), so a body keystroke never re-parses the body. Frontmatter-touching edits (rare) rebuild it from scratch.

Same `bench_doc_buffer_keystroke` as the Slice A row, re-measured before/after on one machine (an Apple-silicon laptop faster than the 2026-05-17 reference box, so the absolute Slice A "before" numbers here run lower than the canonical row above; the **ratios** are what matter):

| scenario | before (Slice A) | after (Slice B) | speedup |
|---|---|---|---|
| `doc_buffer_keystroke/mid/10kb` | 49.4 µs | **39.4 µs** | 1.3× |
| `doc_buffer_keystroke/mid/100kb` | 404 µs | **86.7 µs** | 4.7× |
| `doc_buffer_keystroke/mid/1mb` | 5.84 ms | **393 µs** | 14.9× |
| `doc_buffer_keystroke/mid/8mb` | 48.4 ms | **4.03 ms** | 12.0× |
| `doc_buffer_keystroke/tail_8mb` | 36.4 ms | **4.05 ms** | 9.0× |

The mid curve is now near-flat: an 8 MB note's keystroke dropped from ~48 ms (canonical box: ~63 ms) to ~4 ms, and `tail_8mb` matches `mid/8mb` (the structural scan is no longer whole-doc, so edit position is irrelevant). The residual ~O(n) — `mid/8mb` is still ~10× `mid/1mb` — is **not** the structure parse any more; it's the two `Rope::to_string()` full materializations (`apply_edit` reads the post-edit text once, `highlight_in_range` once more) plus the whole-document `scan_comments` / `%%` sweep in the ranged comment fallback. Those are inherent to the current `&str`-in / whole-document-coordinates contract of `highlight_spans_in_range`, not the two structural costs this slice removed; flattening them further is a separate change (a rope-native windowed highlight) outside #404. The arbiter is the `incremental_structure_*` differential census (raw + frontmatter) at `PROPTEST_CASES=100000`, plus an exhaustive single-edit reconvergence census and a buffer-level body-cache census — all green.

## V1 — 2026-05-31 (#407: rope-native windowed highlight — the flatten, finished)

#404 Slice B made the *structural decision* incremental but left the residual ~O(n) the row above names. #407 removes it: the keystroke path now materializes only what it touches.

- **Window-native highlight.** `window_diverges` + a new `highlight_window` take the **window text** (not the whole source); `highlight_in_range` rope-walks the window bounds and materializes only `[win_start..win_end]`. Whole-doc fallback (frontmatter-touch / `---`-head / straddle / `%%`) is the rare path.
- **Rope-native `apply_edit`.** `updated` is generic over a `DocText` trait, so the incremental re-lex materializes only its bounded chunk — no `Rope::to_string()` on the **main thread** (where `apply_edit` runs in the editor).
- **Incremental comment index.** A cached `%%…%%` range set, maintained per-edit (re-scan only when a `%` is in the inserted/deleted text or the 1-char edit halo, else shift), replaces the per-keystroke whole-document `scan_comments`.

| scenario | before (Slice B) | after (#407) | speedup |
|---|---|---|---|
| `doc_buffer_keystroke/mid/1mb` | 392.9 µs | 80.7 µs | 4.9× |
| `doc_buffer_keystroke/mid/8mb` | 4.38 ms | **244.7 µs** | **~18×** |
| `doc_buffer_keystroke/tail_8mb` | 4.26 ms | **252.3 µs** | **~17×** |

The mid curve is now **flat**: `mid/8mb` (245 µs) is ~3× `mid/1mb` (was ~11×), and the only remaining sub-linear growth is the `updated` splice rebuilding the (short) structure `Vec`s — O(block-count), a tiny constant, not a per-byte cost. Cumulatively from Slice A, an 8 MB note's keystroke went **62 ms → 245 µs (~250×)**. Output is unchanged — the gate is the `buffer_matches_stateless` census (window-native `highlight_in_range` == the stateless `highlight_spans_in_range`, byte-identical) + a `comment_index == scan_comments` census, both at `PROPTEST_CASES=100000`/200k, plus the red-team's `%`-dense + awkward-frontmatter + `rope_fm_end`-boundary suites.

## When to rerun

- After any change to `VaultSession::from_filesystem`, `scan_initial`, or `list_files`.
- After any schema migration that touches the `files` table or its indexes.
- After any non-trivial change to the `FsVaultProvider` IO surface (`list_dir`, `stat`, `read_file`).
- After any change to `editor_spans::highlight_spans` / `highlight_spans_in_range`, the scanners they compose, or the ranged safe-window / fallback logic. Refresh the Swift end-to-end row too via `SLATE_BENCH=1 swift test -c release --filter HighlightBenchmarkTests` (it also catches FFI-marshalling regressions the Rust bench can't).
- Before each milestone build that ships to testers — record the numbers in this file as a new dated baseline so regressions are visible.


## Milestone U verification (2026-07-04, U5-4 #477)

### Full-scale census run — release, `SLATE_CENSUS_FULL=1`

Every census in the program, run once at full spec scale in release mode
(Apple Silicon laptop). All clean. These are the program's correctness
invariants: workspace-model geometry (Swift, in the regular suite),
structural path integrity + journaled undo, link-graph referential
stability + byte-exact move undo, the split/compose round-trip law, the
widget/body edit interleave, reading-block body coverage, and dir-tree
id stability.

| Census (slate-core, release) | Scale | Result | Wall time |
|---|---|---|---|
| `census_structural_mutations_path_integrity` | 500 vaults × 200 ops | ok | ~14.8 min (chunk: 890s for the pair) |
| `census_structural_undo_round_trip` | 500 × 200 | ok | ″ |
| `census_link_graph_referential_stability_session` | 120 seeds | ok | 65s (chunk of 3) |
| `census_move_undo_restores_bytes` | full | ok | ″ |
| `census_referential_stability_over_random_moves` (pure planner) | 2 000 seeds | ok | ″ |
| `census_split_compose_round_trip` | 100k documents | ok | 606s (chunk of 5) |
| `census_widget_body_edit_interleave` | full | ok | ″ |
| `census_reading_blocks_cover_body_exactly` | full | ok | ″ |
| `census_dir_ids_stable_across_rescans` | full | ok | ″ |
| `census_dir_tree_matches_filesystem` | full | ok | ″ |

Swift-side censuses (workspace model 800-seed geometry/focus, U4-4
terminal-region routing 800-seed) run in the regular `swift test` suite
on every CI push — green at program close.

### Milestone U interaction budgets

State-layer latencies (the synchronous funnels the views bind), measured
by `InteractionBudgetTests` — each has a hard `ContinuousClock` ceiling
that fails the suite on an order-of-magnitude regression, plus an XCTest
`measure` baseline recorded here.

| Interaction | Ceiling (per op) | Measured (2026-07 baseline, XCTest `measure` avg) |
|---|---|---|
| Tab switch (snapshot ⊕ restore funnel, parked path) | < 50 ms | **~69 µs** (0.138 ms per switch-pair) |
| Mode toggle (editing ⇄ reading, incl. caret park + workspace.json persist) | < 50 ms | **~0.65 ms** (1.3 ms per toggle-pair; dominated by the layout write) |
| Leaf switch (rail activation) | < 10 ms | **< 1 µs** (6 µs per 10-leaf sweep) |
| Tree expand + flatten + collapse, 10k-file folder (cached level) | < 500 ms | **~2.2 ms** |

### The #404 keystroke guarantee, post-U3-5 (body-only buffer)

The program's hardest performance promise: keystroke cost stays FLAT in
document size, and the U3-5 body-only flip must not regress it (the
buffer now receives the body; `fm_end == 0` path).

| Bench (criterion, release) | 2026-07 median | Meaning |
|---|---|---|
| `doc_buffer_keystroke/tail_8mb` | **261.5 µs** | One keystroke at the tail of an 8 MB document through the stateful DocumentBuffer — matches the pre-flip #404 baseline (245 µs, within run noise). **The flip held the budget.** |
| `editor_highlight_ranged/ranged_tail_edit_8mb` | 65.0 ms | The stateless ranged fallback (window re-parse) — unchanged class. |
| `editor_highlight_ranged/whole_document_8mb` | 463.0 ms | The stateless whole-document pass — the "why #404 exists" number; never on the keystroke path. |

Legacy baselines spot-confirmed unchanged this run (release, same machine):
`first_open_and_scan` 1k = 78 ms · 10k = 1.54 s; `reopen_with_cache`
1k = 14.7 ms · 10k = 153 ms — the historical baseline classes hold.

### U2 baselines (new benches added this PR — the u2_spec rows)

| Bench | 2026-07 median | Meaning |
|---|---|---|
| `dir_and_rewrite/list_dir_children_10k_root` | **5.3 ms** | One lazy tree-level fetch against a 10k-file vault root (the sidebar's expand hot path). |
| `dir_and_rewrite/plan_rewrites_500_sources` | **172.5 ms** (~345 µs/source) | The U2-3 planner over 500 link-bearing sources for one moved file — every link re-resolved against the pre/post indexes (the censused correctness path). |


## Milestone T Wave 1 — 2026-07-04 (canvas backend, #359/#360/#517/#361/#366)

New bench file `crates/slate-core/benches/canvas_bench.rs` against the
committed 2,000-node fixture (`large_2000.canvas`, the §K scale
budget). Gate: the full open path (parse + derive) stays interactive
and nothing is quadratic — 2,000 nodes ≈ 5.6 ms end-to-end, so canvas
open cost is dominated by I/O, not derivation. Apple Silicon laptop,
release.

| Bench | 2026-07 median | Meaning |
|---|---|---|
| `canvas_parse_2000` | **3.20 ms** | Tolerant lossless parse (serde_json + typed extraction + raw retention). |
| `canvas_derive_2000` | **2.25 ms** | Containment tree, reading order, adjacency, summaries, spatial index. |
| `canvas_parse_derive_2000` | **5.62 ms** | The full open path (what `open_canvas` pays before its index write). |
| `canvas_serialize_2000` | **1.90 ms** | Canonical re-emission (per-field reconciliation, skipped-entry interleave). |
