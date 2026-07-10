# Milestone N gap analysis — deliberate deltas, recorded so they never read as omissions

Cross-references: [00_program.md](../00_program.md) decisions · [01_research_brief.md](../01_research_brief.md) · `../../05_locked_architecture_decisions.md` §8 · the [milestone 14 description](https://github.com/coryj627/slate/milestone/14).

## Deltas from the 05 §8 sketch (all additive-compatible, none re-litigate the architecture)

| # | 05 §8 says | N ships | Why |
|---|---|---|---|
| G1 | §8.3 `bases_files` has `path TEXT PRIMARY KEY` + `raw_yaml` column; §4.5 sketches a `specialized_blocks` table | `file_id` FK, **no raw_yaml**; fence discovery gets a **new `bases_blocks` table** (n0 §N0-4) | §9.2 (SQLite = index, not source of truth) wins over the sketch; every shipped index table joins on `file_id`. `specialized_blocks` was never implemented (migrations 001–020 verified; reading-pipeline kinds derive at render time), so N brings its own table. |
| G2 | §8.4 names the rich type `QueryResultSet`; the milestone says "reuse `QueryResultSet` from Milestone E — **no new result shape**" | `BasesResultSet` (n2 §N2-1) — a new shape | Milestone E shipped `QueryResultSet` as the FTS shape `{rows, summary}` (search_db.rs:100), which **cannot carry columns/groups/summaries** — the milestone's "no new result shape" premise is unimplementable as written. "No retrofitting" is honored by not touching the FTS type; the rich tabular type gets the `Bases` prefix. |
| G3 | §8.1 lists Dataview DQL as parsed | **In N scope** (owner decision 2026-07-06, reversing this program's own draft deferral): block-query parser N0-5 #713, read-only ` ```dataview ` rendering, convert-to-`.base` command | Owner: "necessary to support this work correctly." The v1 boundary moved to the DQL *remainder* (N-E1): inline `= expr`, FLATTEN, `rows`-aggregation GROUP BY, unmapped functions — see O11. |
| G4 | §8.2 `QuerySource::Custom(String)` | Parses to `Unsupported` | V2+ plugin surface per the sketch's own comment. |
| G5 | §8.11 `parse_dql` | Ships in N2-1 as handle-shaped `open_dql` + one-shot `dql_as_base` | Follows G3; the handle form matches the shipped canvas/bases FFI idiom rather than the sketch's bare-function signature. |
| G6 | §8.6 free-order builder prose | Source compiles into filters on `.base` save (n4 §N4-1 rule 1); Linked-from serializes as `link("<note>").linksTo(file.file)` (also N0-5 rule 3's `outgoing()` form) | Obsidian's format has no source clause; round-trip requires it. Builder recognizes its canonical shape on re-open (§N-G tested). |
| G7 | §8.2 `SlateQuery` sketch: flat `Vec<FilterCondition>` with per-row combinators, `formulas: HashMap` | Pinned in n0 §N0-2: recursive `FilterNode` (`Stmt(Expr)`/And/Or/Not) over arbitrary expressions; declaration-ordered `Vec<(String, Expr)>` formulas; added `row_source` | The sketch cannot represent `not`, nested groups, or expression statements like `formula.ppu > 5` (the brief-§1 example); HashMap iteration violates §N-B determinism. Same AST role, richer shape. |
| G8 | §8.11 API names: `execute_query`, `parse_base_file`, `save_as_base_file`, `parse_dql`, `create_query_builder` | Handle-idiom mapping pinned in n2 §N2-1: `open_query`+`base_execute`, `open_base`, `save_query_as_base`, `open_dql`/`dql_as_base`; `create_query_builder` dropped (the builder is UI over the AST, not a session object) | Matches the shipped canvas/XD handle FFI family; capability-equivalent. |
| G9 | §8.3: cache "invalidated when any file **in the query's source set** changes" | One session-global vault generation; any change invalidates all cached queries (n1 §N1-2 rule 5, n0 §N0-4 rule 4) | Correct-by-construction v1 (§N-C census provable); per-source-set partial invalidation is a perf follow-up, not a v1 gamble. |
| G10 | Milestone: "Standalone `BasesView` (**sidebar surface**)" | `.base` opens as a workspace **tab** (n3 §N3-1); the sidebar gets the `queries` list leaf (n4 §N4-3) + the `basesDock` follow-active grid leaf (n4 §N4-4) | Grids need tab real estate for cell navigation; the sidebar keeps list + docked-grid roles. Capability preserved, placement changed. |

## Deltas from Obsidian behavior (each is a documented stance, not an accident)

| # | Obsidian | Slate v1 | Authority |
|---|---|---|---|
| O1 | `random()` regenerates per view load | Excluded — fail-loud | Determinism DoD §N-B; help documents it. |
| O2 | Undocumented per-view UI state keys (sort, widths, card size…) | Unknown keys remain preserved opaque, ignored for execution, and named by the notice banner. Native `sort` is the read-only execution exception: interpret property IDs/expressions while preserving authored bytes; a present `slate.sort` (including `[]`) wins. Slate writes only the namespaced child. | Brief §6.1; decision 3; n0 §N0-2 rule 4; n1 §N1-3 rule 1. Reverse-engineering the remaining keys is N-E-scale future work, not v1. |
| O3 | Cards + Map render | Table-fallback + notice | 05 §8.5 (cards V1.x = N-E3; map V2+ with the AT-caveat stance). |
| O4 | `html()`/`image()`/`icon()` render rich | Parse, render as text | 05 §1.3 (no webview); help documents. |
| O5 | Summary superset (`mean`, Median, Stddev, Range) | v1 = the ten milestone-14 defaults (+ checked/unchecked mapping); others preserved, fail-loud if executed | Brief §6.2; N-E6. |
| O6 | Property edits undo/redo in-grid | Re-edit only | No property undo stack exists yet; follow-up tracked outside N (n3 §N3-4 rule 3). |
| O7 | Summaries computed over shown rows (behavior unverified in docs) | Post-filter **pre-limit**, both counts reported | Docs silent; accessible-honest stance pinned (n1 §N1-3 rule 6). |
| O8 | Tasks not queryable (team-stated cache limit) | `source: tasks` + `file.tasks.*` | The differentiator (brief §5.2, decision 8). |
| O9 | Bases search shipped 1.12 filename-first | Quick filter across displayed columns, transient | Brief §5.1; decision 12. |
| O10 | No full-text in Bases ("Bases ≠ search" moderation stance) | `file.matches()` extension | 05 §8.8 locked it; decision 9. |
| O11 | Dataview DQL semantics that don't map | CALENDAR, **all** GROUP BY (even "simple" grouping changes row membership — one row per key vs. labeled sections), FLATTEN, order-dependent command pipelines, multi-arg lambdas, and ~20 unmapped functions convert to **named fail-loud errors**, never approximations. DQL's `null <= date` truthiness needs no special case under the N1-1 totality rules (Null ordering ⇒ false both sides of the conversion; the residual `<=`-on-missing edge is in the migration help) | N0-5 rules 2, 4–7; brief §8. The mapping tables are the traceable backlog for future function additions. |
| O12 | Dataview task-field semantics | `created`/`completion`/`start`/`fullyCompleted` ⇒ `Unsupported` (tasks table has due/scheduled/priority/recurrence only — deliberate migration-008 shape). Per the current executable Dataview source, DQL `completed` accepts both `"x"` and `"X"`, while `checked` requires a status that is neither empty nor the single unchecked space; Slate pins those predicates directly. Conversely Slate indexes `priority`, which Dataview explicitly does not support. |
| O13 | Obsidian "follows JavaScript behavior" — full JS coercion in comparisons/arithmetic | Slate evaluates documented coercions + numeric-string coercion, and makes everything else **total over data** (Null/cross-type ordering ⇒ false + view warning; incompatible arithmetic ⇒ Null + warning; structural problems alone are fatal) | n1 §N1-1 rules 2–3; program decisions 5–6. More defined and less magical than JS; membership deltas vs. Obsidian are edge-case (exotic coercions), surfaced as warnings, and documented in help. |
| O14 | 05 §8.4 names the post-limit count `filtered_count` | `shown_count` | It carries the shown-row count, not a filter count (n1 §N1-3 rule 6, n2 §N2-1) — renamed for honesty; `total_count` unchanged. |
| O15 | 05 §8.7 row action "show local graph" | Deferred until Milestone P ships the local-graph leaf; context-menu slot reserved | n3 §N3-4 rule 1. N cannot depend on P (program sequencing note); the action lands as a P-or-later follow-up, not silently dropped. |

## Known risks the specs already fence

- **YAML comment preservation** is the round-trip crux — n0 §N0-3 mandates splice-not-reemit precisely because yaml-rust2 drops comments; the adversarial census (§N-A) is the guard. If splicing proves brittle on exotic YAML, the fallback is scoping byte-preservation to untouched-files + canonical-rewrite-on-edit **with owner sign-off first** (it weakens decision 3's minimal-diff promise).
- **Grid component growth** (cell mode, groups, summary row, editing) lands inside the shared `AccessibleDataGrid` — n3 §N3-1 requires existing callers' regression suites stay green untouched; if the additions destabilize v2, fork-to-v3 is the escape hatch (owner decision at that point).
- **Builder scope creep** is the milestone's schedule risk; n4's one-level-grouping + advanced-chips boundary is the fence. Anything beyond it is the raw editor's job in v1.
- **`this` in cached results**: cache keys include `this` identity (n1 §N1-2 rule 5) — the follow-active sidebar (n4 §N4-4) is why; a missed key would serve one note's results to another.

## Owner amendments after initial draft

- **2026-07-06 — DQL in scope** (PR #712 review): the draft deferred the DQL parser to V1.x; the owner pulled it into N ("necessary to support this work correctly"). Program decision 2 amended; issue #713 (N0-5) added; N0-4/N2-1/N3-5/N4-5 extended (`dataview` fence discovery, `open_dql`/`dql_as_base` FFI, read-only fence rendering + convert action, migration help page). DataviewJS remains never-supported.
- **2026-07-06 — adversarial readiness pass** (owner-requested pre-handoff review; three independent audits): fixed the `specialized_blocks` fiction (G1 — new `bases_blocks` table), pinned `SlateQuery` (G7) and the §8.11 API mapping (G8), redrew the fail-loud boundary so data-shaped Null/type mismatches are warnings rather than view-killers (decision 6, n1 rules 2–3, O13 — `price > 2.1` must run on a whole-vault dataset), shipped `file.inDegree`/`file.outDegree` as extension fields (honoring the milestone's "ship the basics"), moved quick-filter matching/counting engine-side (one source of truth for summaries + audio strings), made embeds read-only in v1, made ` ```slate-query ` dual-mode (reference *or* inline YAML), made all DQL GROUP BY fail-loud (O11), pinned the DQL task-status predicates (O12; corrected on 2026-07-10 to current-source `x|X` completion and nonempty/non-space checked semantics), excluded `now()`-queries from the cache (§N-C coherence), vendored the milestone's normative test/checkpoint lists into [`../02_milestone_brief.md`](../02_milestone_brief.md), and recorded deltas G9/G10/O14/O15 plus assorted anchor/citation corrections.
- **2026-07-08 — N4-5 close-out evidence** (#711; completed 2026-07-09): close-out owns the user help page, command/help drift coverage, CLI/session E2E fixtures, manual AT smoke checklist, and the final Bases benchmark table. The checked-in corpus now includes raw `.base` captures written by Obsidian 1.12.7, with app/OS/timestamp/UI-step provenance and SHA-256 verification in `crates/slate-core/tests/fixtures/bases/obsidian/PROVENANCE.md`; they are included in parse, no-edit byte-equality, fixed corpus-edit, and round-trip gates and must never be normalized. The milestone can merge with automated gates green, but it remains operationally open until the manual AT checklist records PASS or follow-up issues for each failed item.
- **2026-07-09 — adversarial audit remediation:** executable counterexamples
  closed lossless serializer, DQL/evaluator, freshness/typing, grid, live-refresh,
  builder, dashboard, and closure-gate gaps. Generated default/full censuses now
  enforce the N-specific DoD, and Criterion p50 evidence includes a matched
  pre/post scan comparison (+3.403% at 10k, +2.390% at 50k; both inside the 5%
  gate). This changes no deliberate product delta above. Manual VoiceOver AT
  remains open and is not inferred from static accessibility checks.

## Feature-request traceability (the owner's two pinned threads)

- forum 100964 (transient quick search) → decision 12, issue [#704](https://github.com/coryj627/slate/issues/704), reserved variants N-E2.
- forum 103074 (tasks) → decision 8, issue [#697](https://github.com/coryj627/slate/issues/697) (+ grid/task actions n3 §N3-4 rule 1, help caveat n4 §N4-5), reserved kanban projection N-E7.
