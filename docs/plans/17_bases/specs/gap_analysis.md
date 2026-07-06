# Milestone N gap analysis — deliberate deltas, recorded so they never read as omissions

Cross-references: [00_program.md](../00_program.md) decisions · [01_research_brief.md](../01_research_brief.md) · `../../05_locked_architecture_decisions.md` §8 · the [milestone 14 description](https://github.com/coryj627/slate/milestone/14).

## Deltas from the 05 §8 sketch (all additive-compatible, none re-litigate the architecture)

| # | 05 §8 says | N ships | Why |
|---|---|---|---|
| G1 | §8.3 `bases_files` has `path TEXT PRIMARY KEY` + `raw_yaml` column | `file_id` FK, **no raw_yaml** (n0 §N0-4) | §9.2 (SQLite = index, not source of truth) wins over the sketch; every shipped index table joins on `file_id`. |
| G2 | §8.4 names the rich type `QueryResultSet` | `BasesResultSet` (n2 §N2-1) | Milestone E shipped `QueryResultSet` as the FTS shape (search_db.rs:100); the milestone-14 "reuse, no retrofitting" instruction is honored by **not** touching it — the rich tabular type gets the `Bases` prefix. |
| G3 | §8.1 lists Dataview DQL as parsed | Deferred to V1.x (decision 2, N-E1) | Not in the milestone-14 user-facing capability list; the AST is the shared target so nothing narrows. |
| G4 | §8.2 `QuerySource::Custom(String)` | Parses to `Unsupported` | V2+ plugin surface per the sketch's own comment. |
| G5 | §8.11 `parse_dql` | Absent until N-E1 | Follows G3. |
| G6 | §8.6 free-order builder prose | Source compiles into filters on `.base` save (n4 §N4-1 rule 1) | Obsidian's format has no source clause; round-trip requires it. Builder recognizes its canonical shape on re-open (§N-G tested). |

## Deltas from Obsidian behavior (each is a documented stance, not an accident)

| # | Obsidian | Slate v1 | Authority |
|---|---|---|---|
| O1 | `random()` regenerates per view load | Excluded — fail-loud | Determinism DoD §N-B; help documents it. |
| O2 | Undocumented per-view UI state keys (sort, widths, card size…) | Preserved opaque; **ignored for execution**, banner names them; Slate state under `slate` sub-key | Brief §6.1; decision 3. Reverse-engineering their names is N-E-scale future work, not v1. |
| O3 | Cards + Map render | Table-fallback + notice | 05 §8.5 (cards V1.x = N-E3; map V2+ with the AT-caveat stance). |
| O4 | `html()`/`image()`/`icon()` render rich | Parse, render as text | 05 §1.3 (no webview); help documents. |
| O5 | Summary superset (`mean`, Median, Stddev, Range) | v1 = the ten milestone-14 defaults (+ checked/unchecked mapping); others preserved, fail-loud if executed | Brief §6.2; N-E6. |
| O6 | Property edits undo/redo in-grid | Re-edit only | No property undo stack exists yet; follow-up tracked outside N (n3 §N3-4 rule 3). |
| O7 | Summaries computed over shown rows (behavior unverified in docs) | Post-filter **pre-limit**, both counts reported | Docs silent; accessible-honest stance pinned (n1 §N1-3 rule 6). |
| O8 | Tasks not queryable (team-stated cache limit) | `source: tasks` + `file.tasks.*` | The differentiator (brief §5.2, decision 8). |
| O9 | Bases search shipped 1.12 filename-first | Quick filter across displayed columns, transient | Brief §5.1; decision 12. |
| O10 | No full-text in Bases ("Bases ≠ search" moderation stance) | `file.matches()` extension | 05 §8.8 locked it; decision 9. |

## Known risks the specs already fence

- **YAML comment preservation** is the round-trip crux — n0 §N0-3 mandates splice-not-reemit precisely because yaml-rust2 drops comments; the adversarial census (§N-A) is the guard. If splicing proves brittle on exotic YAML, the fallback is scoping byte-preservation to untouched-files + canonical-rewrite-on-edit **with owner sign-off first** (it weakens decision 3's minimal-diff promise).
- **Grid component growth** (cell mode, groups, summary row, editing) lands inside the shared `AccessibleDataGrid` — n3 §N3-1 requires existing callers' regression suites stay green untouched; if the additions destabilize v2, fork-to-v3 is the escape hatch (owner decision at that point).
- **Builder scope creep** is the milestone's schedule risk; n4's one-level-grouping + advanced-chips boundary is the fence. Anything beyond it is the raw editor's job in v1.
- **`this` in cached results**: cache keys include `this` identity (n1 §N1-2 rule 5) — the follow-active sidebar (n4 §N4-4) is why; a missed key would serve one note's results to another.

## Feature-request traceability (the owner's two pinned threads)

- forum 100964 (transient quick search) → decision 12, issue [#704](https://github.com/coryj627/slate/issues/704), reserved variants N-E2.
- forum 103074 (tasks) → decision 8, issue [#697](https://github.com/coryj627/slate/issues/697) (+ grid/task actions n3 §N3-4 rule 1, help caveat n4 §N4-5), reserved kanban projection N-E7.
