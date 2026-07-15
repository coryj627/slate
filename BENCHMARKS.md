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
| `first_open_and_scan` | `fs::remove_dir_all(.slate)` (setup, excluded) → `VaultSession::from_filesystem` → `scan_initial`. Each measurement is a true cold start. | 10 default; CLI-overridable |
| `reopen_with_cache` | Cache primed once outside the loop. Each iteration re-opens + re-scans (scanner upserts on path, so this is the steady-state warm re-open). | 10 default; CLI-overridable |
| `list_files_paged` | Cache primed once. Each iteration pages through `list_files` 1 000 rows at a time until exhausted. | 10 default; CLI-overridable |
| `tasks_cold_scan` | Cold scan of the realistic 1 000-file Tasks fixture: ~70% zero-task, ~25% with 1–3 tasks, and ~5% with 10–15 tasks. Same setup discipline as `first_open_and_scan`; measures the scanner + tasks pipeline. | 10 default; CLI-overridable |
| `tasks_in_vault_first_page` | Cache primed; each iteration runs `tasks_in_vault(All, first(200))` against that realistic 1 000-file fixture. Drives the Mac TasksReviewView's initial render. | 10 default; CLI-overridable |

The first three groups run for three vault sizes: **1 000**, **10 000**, **50 000** Markdown files. The Tasks groups run the single realistic 1 000-file distribution described above.

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

The three console numbers are the **lower bound**, **estimate**, and **upper
bound** of Criterion's default timing estimate. They are not the p50. When a
gate is specified as p50, read `median.point_estimate` and
`median.confidence_interval` from that benchmark's `new/estimates.json`. Wide
bounds indicate noise and should trigger a quieter rerun or a larger CLI sample
size.

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

## Milestone T Wave 5 — 2026-07-04 (canvas UI at §K scale, #365)

UI-side timings on the same committed 2,000-node fixture, measured by
`MilestoneTIntegrationTests.testLargeCanvasOpensNavigatesAndWindowsResponsively`
(real AppState + FFI + `CanvasRendererNSView` at 800×600; the suite
asserts budgets of 500/100/50 ms so a regression fails CI, and these
recorded values show the headroom):

| Measure | Recorded | Notes |
|---|---|---|
| First windowed rebuild | **3.9 ms** | 13 of 2,000 cards materialized (AX windowing, viewport + 1-viewport margin). |
| Per-pan window hop | **2.8 ms** | Ten viewport jumps averaged — window churn, edge rebuild, AX re-frame. |
| Per-step navigator traversal | **0.18 ms** | Reading-order selection moves incl. announcement assembly. |

## Milestone N close-out — 2026-07-10 (final remediation source)

New bench file `crates/slate-core/benches/bases_bench.rs` drives the public
`VaultSession` Bases API against deterministic 1k / 10k / 50k Markdown vaults
with frontmatter properties. The measured path includes handle lookup,
SQLite-backed query execution, result mirroring for UniFFI/UI callers, quick
filter projection, cache-hit replay, parse/serialize, and CSV export
formatting. Criterion release run on the source committed as `dacb2b0` on a
MacBook Pro (Apple M5 Pro, 18 cores, 48 GB), macOS 26.5.1 (25F80), arm64, with rustc 1.95.0
(59807616e 2026-04-14). The release-gate query filters to one indexed folder
and limits the grid to 100 displayed rows; the full-export diagnostic row below
is the intentionally unbounded "dump everything" stress path. Every number is
the Criterion median point estimate (p50); parenthesized ranges are the median's
95% confidence interval from `new/estimates.json`.

| Bench (criterion, release) | 1k files | 10k files | 50k files | Meaning |
|---|---:|---:|---:|---|
| `bases_session/indexed_query_gate_uncached` | **742.704 µs** (740.806–750.474) | **2.040692 ms** (2.037500–2.045577) | **10.016226 ms** (9.966765–10.027050) | Fresh handle, indexed folder filter, result mirror, 100-row grid limit. Gate target: <50 ms @10k / <200 ms @50k. |
| `bases_session/cache_hit_reexecute` | **40.440 µs** (40.104–40.573) | **41.494 µs** (41.235–41.907) | **41.224 µs** (41.096–41.420) | Same handle/query/generation after priming the session cache. Gate target: <2 ms. |
| `bases_session/quick_filter_display_values` | **750.967 µs** (737.917–765.222) | **2.088535 ms** (2.084746–2.093973) | **10.119612 ms** (10.105942–10.142661) | Same gate query with accent-insensitive displayed-value quick filter before sort/group/summary/limit. |
| `bases_session/export_csv_gate` | **758.788 µs** (753.867–766.813) | **2.075906 ms** (2.072717–2.084532) | **10.009286 ms** (9.974605–10.128565) | Gate query through `base_export`, formatting exactly the displayed rows as CSV. |
| `bases_session/export_csv_full_diagnostic` | **6.710419 ms** (6.695509–6.726014) | **76.208459 ms** (76.109073–76.590656) | **466.565605 ms** (466.123084–466.702355) | Deliberately unbounded full-vault CSV export diagnostic; not the interactive grid gate. |

| Bench (criterion, release) | 2026-07 p50 (95% CI) | Meaning |
|---|---:|---|
| `bases_format/parse_serialize_roundtrip` | **22.663 µs** (22.648–22.676) | Parse the gate `.base` file and serialize it byte-equally. Gate target: <5 ms per file. |

Command:

```sh
CARGO_TARGET_DIR=target/milestone-n-bench-final cargo bench -p slate-core --bench bases_bench -- --sample-size 20
```

The release-gate rows pass with substantial headroom: 10k/50k indexed queries
are 2.041/10.016 ms against 50/200 ms, cache replay is about 0.041 ms against
2 ms, and format round-trip is 0.023 ms against 5 ms. Raw estimates and reports
are under `target/milestone-n-bench-final/criterion/`.

### Matched N scanner regression

The same `first_open_and_scan` measurement body and synthetic fixture ran
sequentially at pre-N `b05f86f` and the final source committed as `dacb2b0`,
with 20 true cold samples per size. The historical runner had a group-level
`sample_size(10)` that overrides Criterion's CLI; the temporary pre-N worktree
removed only that runner line so both sides honored `--sample-size 20`. No
measured function, fixture, or production source was changed for the historical
run. The historical worktree and final checkout resolved 333 packages afresh
under the same toolchain; the scan benchmark compiled matching dependency
versions (the two lock-only version differences, `bytes` and `rustversion`, were
not built by this target).

| Vault | Pre-N p50 (95% CI) | Final p50 (95% CI) | Delta | 5% gate |
|---|---:|---:|---:|---|
| 10k files | **1.795082 s** (1.778649–1.830046) | **1.709608 s** (1.704388–1.712786) | **−4.7616%** | PASS |
| 50k files | **10.720721 s** (10.635877–11.240251) | **10.367891 s** (10.325665–10.460516) | **−3.2911%** | PASS |

Commands (run one at a time from the corresponding detached worktree; target
paths below are rooted in the primary checkout):

```sh
CARGO_TARGET_DIR=/path/to/slate/target/milestone-n-scan-pre cargo bench --offline -p slate-core --bench scan_bench -- 'first_open_and_scan/(10000|50000)$' --sample-size 20
CARGO_TARGET_DIR=/path/to/slate/crates/slate-core/target/milestone-n-scan-final cargo bench -p slate-core --bench scan_bench -- 'first_open_and_scan/(10000|50000)' --sample-size 20
```

Raw median estimates and confidence intervals live at
`target/milestone-n-scan-pre/criterion/first_open_and_scan/` and
`crates/slate-core/target/milestone-n-scan-final/criterion/first_open_and_scan/`.
The p50 delta is `(post - pre) / pre * 100`; both sizes are faster than the
pre-N source and therefore pass decision 16's no-worse-than-5% regression
budget.

## Milestone O — O-1 op-log v2 baselines (2026-07-10)

First recorded baselines for the op-log append path (no earlier rows exist —
`oplog_bench.rs` is new with O-1 #539). Machine: same arm64 laptop as the
Milestone N close-out rows. Command:

```sh
CARGO_TARGET_DIR=target/o1-bench cargo bench -p slate-core --bench oplog_bench -- --sample-size 20
```

| Bench | p50 (95% CI) | Note |
|---|---:|---|
| `oplog_append/plain_batch` | **3.3002 ms** (3.1915–3.4350) | one `EditBatch` append incl. `sync_data` — the fsync dominates |
| `oplog_append/annotated_batch` | **3.6132 ms** (3.4792–3.7693) | same batch + 3 annotations in a kind-4 wrapper — **+9.5% vs plain, inside the <10% O-1 gate**; the delta is encode + ~200 extra bytes through the same fsync |
| `oplog_save_path/save_text_hot` | **9.3330 ms** (9.0947–9.5120) | full warm `save_text` (CAS re-hash + atomic write + index tx + op-log append) — the absolute anchor the micro rows sit inside |

The `#404` keystroke baselines (`doc_buffer_keystroke/*`) are untouched by
O-1: no editor-path code changed, and the save path's new work is one indexed
`oplog_name` lookup inside the existing save transaction plus arithmetic.

## Milestone O — O-2 compaction baselines (2026-07-10)

Same machine/command pattern as the O-1 rows (`--sample-size 10`).

| Bench | p50 (95% CI) | Gate |
|---|---:|---|
| `oplog_compact/compact_50k_ops` | **100.15 ms** (99.2–101.1) | §9.3.3 "< 1 s in the background" — **10× headroom** (50,001 entries: read + fold + verify + rewrite + rename) |
| `oplog_save_path/save_text_with_trigger_check_5mib_log` | **10.45 ms** (10.3–10.5) | o_spec §O-2 g2 — vs `save_text_hot` 9.33 ms on a tiny log; the ~1 ms delta tracks the bench's 64 KiB file content (hash + write + diff), not the log: the trigger check is returned-length arithmetic, no log walk on the save path |


## Milestone O — O-4 structured diff baseline (2026-07-11)

| Bench | p50 (95% CI) | Gate |
|---|---:|---|
| `structured_diff/diff_500kb_2k_blocks` | **23.65 ms** (23.5–23.8) | o_spec §O-4 "< 50 ms release" — ~550 KB, 2,000 blocks, ~21 scattered edits (LCS anchoring + pairing + copy generation) |

## Milestone O — O-6 temporal query baseline (2026-07-11)

| Bench | p50 (95% CI) | Gate |
|---|---:|---|
| `oplog_temporal/has_change_since_7d_10k_files` | **3.31 ms** (3.28–3.35) | o_spec §O-6 "< 50 ms warm" — full `base_execute` over a 10k-file vault, 589 files carrying events; the filter lowers to one indexed `id IN (SELECT … FROM oplog_events)` membership subquery, and the oplog cache carve-out means every iteration is a real execution |

Save-path note (O-6): `oplog_save_path/save_text_hot` 9.33 ms → 9.44 ms (+1.2%)
with the full O-6 population — event derivation, the mark-before-append
staleness protocol (the marker commit fsync'd under synchronous=FULL),
and the per-entry event transaction. No spec gate binds the save path
here; recorded for the close-out benchmark comparison.

## Milestone O — close-out summary vs the #404 baselines (2026-07-11)

Milestone O (local history + change tracking, #539–#544) shipped with every
spec gate cleared and **zero editor-path regression** — the #404 budget
(8 MB-document keystroke ~245 µs, flat) is untouched by construction: all O
work rides the SAVE path (op-log append, event derivation, marker protocol),
never the keystroke path.

| Gate | Spec budget | Shipped |
|---|---:|---:|
| O-1 annotated append overhead vs plain | < 10% | **+9.5%** (3.61 vs 3.30 ms) |
| O-2 compact 50k-op log (release) | < 1 s | **100.15 ms** |
| O-2 save-path trigger check (5 MiB log) | no log walk | **10.45 ms** (arithmetic only) |
| O-4 structured diff, ~550 KB / 2k blocks | < 50 ms | **23.65 ms** |
| O-6 `has_change_since(7d)`, 10k-file vault | < 50 ms warm | **3.31 ms** |
| Whole-milestone hot-save delta | no gate | 9.33 → **9.44 ms** (+1.2%: event derivation + fsync'd staleness marker + per-entry event transaction) |

Full-scale release censuses at close: op-log identity/compaction/history/
temporal batteries 137 tests green; Mac suite 87 suites; `a11y-check` 100.0.

## Milestone P — P0 graph backend baselines (2026-07-12)

First recorded baselines for the graph backend (#550–#553): `GraphIndex`
(petgraph `StableDiGraph` mirror of the links table), `MetricsSnapshot`
(degrees / components / orphans / hand-rolled 40-iteration PageRank), and
the P0-3 query surface. Fixture: `benches/common::generate_linked_vault`
(hub topology, ~3 outlinks/file). Run via
`cargo bench -p slate-core --bench graph_bench` (`make bench` remains
scan_bench-only). Apple silicon, release profile.

| Benchmark | Mean (95% CI) | Notes |
|---|---:|---|
| `graph_build/1000` | **1.98 ms** (1.82–2.17) | cold build + first snapshot |
| `graph_build/10000` | **35.35 ms** (33.78–37.48) | scales ~linearly |
| `graph_build/50000` | **175.39 ms** (173.18–177.46) | worst-case lazy rebuild cost |
| `graph_snapshot_default_filter/10000` | **8.00 ms** (7.95–8.05) | warm index; per-generation refresh cost |
| `graph_neighborhood_d2/10000` | **8.44 ms** (8.37–8.53) | depth-2 BFS rooted at the vault hub (worst case) |
| `metrics_full/10000` | **5.24 ms** (5.22–5.26) | full recompute incl. PageRank ×40 |
| `metrics_full/50000` | **28.72 ms** (28.65–28.80) | " |

**Save-path gate (DoD §P-E, O(changed-file)):** identical alternating-body
save loop on the same 10k vault, index live vs never built:

| Variant | Mean (95% CI) |
|---|---:|
| `linkset_change_incremental/10000` (index live) | **14.975 ms** (14.870–15.078) |
| `linkset_change_unbuilt/10000` (hooks no-op) | **14.772 ms** (14.630–14.912) |
| **Graph increment** | **+0.203 ms (+1.4%)** — inside the < 1 ms budget |

(The absolute save numbers sit above O's 9.44 ms `save_text_hot` because
this fixture's saves are genuine content changes on a 10k vault — diff,
op-log append, full derivative reindex, and the save path's documented
O(N) vault-index snapshot — none of which is graph work; the controlled
delta above is the graph's whole cost.)

**Scan path (hooks unbuilt = structurally free):**

| Benchmark | This branch | Standing baseline | Delta |
|---|---:|---:|---|
| `first_open_and_scan/10000` | **1.7518 s** (1.7442–1.7598) | 1.7096 s (N-final, 2026-07-10) | **+2.5%** — inside the 5% budget (decision 16); cross-day comparison, and the only new unbuilt-path work is two bounded allocations (written-rows vec on the slow path, re-resolve affected-sources vec) |

Note for future filtered runs: criterion name filters skip measurements
but not group setup — a filtered `graph_bench` run still pays every
group's vault generation + scan priming (both 50k primes included).
Split hot groups into their own bench binary if this becomes a habit.

---

## V1 — 2026-07-12 (#557: deterministic force-layout kernel, Milestone P P2-1)

`cargo bench -p slate-core --bench layout_bench` on Apple silicon (release,
single-threaded). Synthetic graph: `n` notes with ~`2n` seeded-random links
(`from_test_links`). The kernel is `graph_layout::LayoutEngine` — seeded
Fruchterman–Reingold + gravity, `f64`, exact repulsion below 1,500 nodes and
a Barnes–Hut quadtree tier (θ=0.9) at or above it.

| Benchmark | Time | Budget (locked decision 10) |
|---|---:|---|
| `layout_cold/300` (300 iters, exact) | **38.0 ms** | baseline |
| `layout_cold/1500` (300 iters, exact) | **808 ms** | baseline |
| `layout_cold/10000` (300 iters, Barnes–Hut) | **2.14 s** | ≤ 3 s ✓ |
| `layout_warm_tick/300` (one settled force pass) | **131 µs** | < 2 ms ✓ |

Both gated budgets pass. Determinism (DoD §P-C) is enforced separately by the
golden-digest unit tests (bit-identical positions at iters {60, 300} for the
10- and 100-node fixtures) and the `census_barnes_hut_matches_exact` oracle
(BH repulsion within 5% RMS of the exact solver on ≤500-node graphs).

---

## Milestone FL — FL-01 derived file metadata (2026-07-14)

FL-01 adds the regenerable `file_meta` projection, enriches file listings, and
derives metadata during real saves and cold scans. The adjacent merged base is
**A**, commit `f07d78eddf0215e510c2f9beaa0377097dd80f5c`; the implementation is
**B**, the complete FL-01 Tasks 1-3 tree committed alongside this record. Its
parent implementation state is Task-2 commit
`6e66a601bf98bf109f25eb818c1610a3df31e01a`; the containing commit adds the
benchmarked Task-3 changes and this evidence. Both worktrees used byte-identical
benchmark sources:

- `benches/common/mod.rs`: git blob
  `f185a0d0f7647258bbdfae9bf513870a4dbe20a8`, SHA-256
  `f1230265594b033dad0af265865b753ee7a0796850d7328edce52b2110e42d85`
- `benches/scan_bench.rs`: git blob
  `9cfd52d6fe332d2f7b88acc74878bdb29e1df1f9`, SHA-256
  `9937378cd6fef9c306bc0ab2ae71ca6c0f7a90f2b0b618533bbc86142deaece0`

Environment: MacBook Pro `Mac17,8`, Apple M5 Pro (18 cores: 6 Super + 12
Performance), 48 GB RAM; macOS 26.5.1 (25F80); `aarch64-apple-darwin`;
`rustc 1.95.0 (59807616e 2026-04-14)`, LLVM 22.1.2. Criterion ran release
builds with 10 samples, 3 s warm-up, and 5 s measurement time. Processes ran
serially with a 90 s quiet settle before each Criterion invocation; one
invocation then measured its 1k, 10k, and 50k groups consecutively. The power
snapshot reported the AC Power profile with `lowpowermode = 2`, while the
battery snapshot simultaneously reported discharging; no charging state is
claimed. Values below are the Criterion median point estimate (p50) and its 95%
confidence interval from `new/estimates.json`.

The final blocks use symmetric orders to counterbalance observed order bias;
they cannot eliminate every environmental effect. “Geo” is the geometric mean
of each source's two p50s. Relative delta is `(Geo B / Geo A) - 1`; additive
delta is `Geo B - Geo A`.

### Final cold-scan block — order B-A-A-B

| Vault | B1 p50 (95% CI) | A1 p50 (95% CI) | A2 p50 (95% CI) | B2 p50 (95% CI) |
|---|---:|---:|---:|---:|
| 1k | 143.533236 ms (143.026281–144.392806) | 139.762058 ms (136.863875–141.849807) | 137.141776 ms (136.705868–138.119571) | 141.165228 ms (140.522401–143.994208) |
| 10k | 1.720478042 s (1.713534542–1.726522208) | 1.675821271 s (1.664331250–1.691288646) | 1.677923709 s (1.674648375–1.690689500) | 1.663686084 s (1.657441917–1.674380521) |
| 50k | 10.205454563 s (10.011595500–10.441502729) | 9.910130604 s (9.820423042–10.006874500) | 10.038366896 s (9.996783125–10.091863584) | 10.077934771 s (10.032073042–10.215685791) |

| Vault | Geo A | Geo B | Relative delta | Additive delta | ≤5% gate |
|---|---:|---:|---:|---:|---:|
| 1k | 138.445718 ms | 142.344308 ms | +2.81597% | +3.898590 ms | PASS |
| 10k | 1.676872160 s | 1.691843780 s | +0.89283% | +14.971620 ms | PASS |
| 50k | 9.974042660 s | 10.141494238 s | +1.67887% | +167.451578 ms | PASS |

The two directional comparisons were +2.69828% / +2.93379% at 1k,
+2.66477% / −0.84853% at 10k, and +2.98002% / +0.39417% at 50k. Every
order-balanced geometric result clears the 5% scan budget.

An additional settled 1k confirmation used the reverse A-B-B-A order:

| A1 p50 (95% CI) | B1 p50 (95% CI) | B2 p50 (95% CI) | A2 p50 (95% CI) | Block geo delta |
|---:|---:|---:|---:|---:|
| 142.143521 ms (140.983109–143.800202) | 150.637183 ms (150.064958–166.130609) | 152.197771 ms (151.049966–154.478479) | 147.976545 ms (146.178833–149.076375) | +4.40235% |

Across both settled 1k blocks (eight p50s), base Geo is 141.699968 ms, tip
Geo is 146.809842 ms, and the combined delta is **+3.60612%**, still inside
the 5% gate.

### Final metadata-listing block — order A-B-B-A

| Benchmark | A1 p50 (95% CI) | B1 p50 (95% CI) | B2 p50 (95% CI) | A2 p50 (95% CI) |
|---|---:|---:|---:|---:|
| `list_dir_children_meta/10000` | 2.693962 ms (2.684512–2.701095) | 8.178805 ms (7.508822–8.675548) | 7.469075 ms (7.368726–7.569071) | 2.668797 ms (2.660317–2.704245) |

Geo A is 2.681350 ms and Geo B is 7.815888 ms: +191.49075%, or
+5.134538 ms additive. Both FL-01 p50s, and their geometric mean, remain below
the **10 ms local gate** (the automated noisy-runner guard remains 100 ms):
**PASS**. The increase is the intended single-query metadata projection, not an
N+1 query.

### Final real-save block — order B-A-A-B

Each iteration alternates same-sized metadata-rich bodies and supplies the
previous returned hash, so it is a real CAS save without content growth or
compaction noise.

| Vault | B1 p50 (95% CI) | A1 p50 (95% CI) | A2 p50 (95% CI) | B2 p50 (95% CI) |
|---|---:|---:|---:|---:|
| 1k | 9.469296 ms (9.090867–9.715815) | 9.037884 ms (8.909538–9.109259) | 9.121467 ms (9.022958–9.228014) | 9.294869 ms (9.143424–9.358572) |
| 10k | 10.404605 ms (10.207583–10.631232) | 10.402968 ms (10.258887–10.664069) | 10.360702 ms (10.112495–11.294675) | 10.999462 ms (10.394361–11.285402) |
| 50k | 19.398748 ms (19.178625–20.182662) | 19.486959 ms (19.356723–19.800885) | 20.024314 ms (19.652933–20.255849) | 20.332711 ms (20.199017–20.524298) |

| Vault | Geo A | Geo B | Relative delta | Additive delta | Amended ≤0.5 ms gate |
|---|---:|---:|---:|---:|---:|
| 1k | 9.079579 ms | 9.381677 ms | +3.32723% | **+0.302098 ms** | PASS |
| 10k | 10.381813 ms | 10.697900 ms | +3.04461% | **+0.316086 ms** | PASS |
| 50k | 19.753810 ms | 19.860240 ms | +0.53878% | **+0.106430 ms** | PASS |

Directional deltas were +4.77338% / +1.90103% at 1k, +0.01574% /
+6.16521% at 10k, and −0.45267% / +1.54011% at 50k. The order-balanced
additive result passes the owner-approved 2026-07-14 gate at every size. The
overhead does not grow with vault size (0.302, 0.316, then 0.106 ms), so FL-01
adds O(changed-file) metadata work without worsening the shipped save path's
existing O(N) vault-index curve. User-visible scope and all metadata derivation
rules are unchanged; retaining the reliable current index rebuild was favored
over a late, risky vault-index cache.

### Gate amendment and excluded exploratory runs

The owner-approved 2026-07-14 amendment replaces the original literal “total
save curve no worse than adjacent base” reading with the order-balanced
geometric-p50 additive budget above. It does not relax scan, listing,
correctness, durability, or metadata O(changed-file) requirements.

Earlier exploratory measurements that overlapped another Criterion process or
did not receive the 90 s quiet settle are **non-gating and excluded**. The
settled symmetric runs measured a material second-run/order bias; selecting one
direction would therefore misstate the change. Only the complete symmetric
blocks above are release evidence.

Before the reviewed optimizations, the first unoptimized pass failed every
performance area. Only point medians were retained for this exploratory pass;
its confidence intervals were overwritten and are not reconstructed here.

| Benchmark | Base p50 | Initial tip p50 | Delta / result |
|---|---:|---:|---:|
| scan 1k | 144.384121 ms | 168.903997 ms | +16.98%, FAIL |
| scan 10k | 1.707203167 s | 2.002056438 s | +17.27%, FAIL |
| scan 50k | 10.276509188 s | 12.113648167 s | +17.88%, FAIL |
| listing 10k | 2.706268 ms | 34.061683 ms | >10 ms, FAIL |
| save 1k | 8.870469 ms | 9.743574 ms | +9.84%, +0.873105 ms, FAIL |
| save 10k | 10.875062 ms | 10.899277 ms | +0.22%, +0.024215 ms |
| save 50k | 19.312632 ms | 20.189552 ms | +4.54%, +0.876920 ms, FAIL |

Final verdict: **scan PASS at 1k/10k/50k; listing PASS at 10k; amended save
gate PASS at 1k/10k/50k; scale-shape/O(changed-file) gate PASS.**
