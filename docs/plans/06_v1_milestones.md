# 06 — V1 Milestone Decomposition (read+edit Mac alpha)

**Status (2026-05-23):** ✅ All six milestones (A–F) shipped. The alpha build covers the four primary read-and-write workflows — find a note, read its content, follow its links, and edit it — and is ready for the AT-user tester cohort. Per-milestone close dates are in the status table below. Issue tracking continues per the conventions section at the bottom.

**Strategic goal of this phase:** the 4 committed AT-user testers have a Mac app they can use against their own existing Obsidian vaults, with their own screen readers, to do all four primary read-and-write workflows. The phase shipped six progressively-richer builds, not one big drop at the end.

**Pacing note for future phases:** the original plan budgeted 12–16 weeks; A–F closed in 6 calendar days (2026-05-17 → 2026-05-23). The 2-week-per-milestone cadence remains the right *commitment* shape for tester comms — don't promise an end date, promise the next build — even when execution runs faster.

## Where to find the live plan

Per-milestone goals, scope, schema, tests, and acceptance criteria live on **GitHub Milestones**, not in this file:

| Milestone | GitHub | Status |
|---|---|---|
| A — Vault + file list | [milestone 1](https://github.com/coryj627/YANA/milestone/1) | ✅ Shipped (2026-05-17) |
| B — Read + heading nav | [milestone 2](https://github.com/coryj627/YANA/milestone/2) | ✅ Shipped (2026-05-18) |
| C — Backlinks + outgoing links | [milestone 3](https://github.com/coryj627/YANA/milestone/3) | ✅ Shipped (2026-05-18) |
| D — Frontmatter properties | [milestone 4](https://github.com/coryj627/YANA/milestone/4) | ✅ Shipped (2026-05-19) |
| E — Full-text search | [milestone 5](https://github.com/coryj627/YANA/milestone/5) | ✅ Shipped (2026-05-19) |
| F — Editing | [milestone 6](https://github.com/coryj627/YANA/milestone/6) | ✅ Shipped (2026-05-23) — backend [#105](https://github.com/coryj627/YANA/pull/105), UI [#106](https://github.com/coryj627/YANA/pull/106) |

Each GitHub Milestone carries the full Rust + Swift work breakdown, schema migrations, tests, accessibility checkpoints, tester-feedback questions, and definition of done. Individual issues link back to their milestone.

## At a glance

| Milestone | Weeks | Tester build can… | New code surfaces |
|---|---|---|---|
| **A** | 1–2 | Open vault folder; see all `.md` files in a sidebar; navigate with VoiceOver. | `FsVaultProvider`, SQLite `files` table + migrations, vault picker, sidebar, recent-vaults persistence |
| **B** | 3–4 | Select a file; read content with VoiceOver; jump heading-to-heading. | Headings persisted to SQLite, content view, outline panel, heading rotor |
| **C** | 5–6 | See what links to this note; see what this links to; jump between. | Wikilink + Markdown link parsing, link resolution, `links` table, backlinks/outgoing panels |
| **D** | 7–8 | See this note's YAML frontmatter as a structured properties panel. | YAML frontmatter parsing, type inference, `properties` table, properties panel |
| **E** | 9–10 | Search across the vault by content; navigate results accessibly. | FTS5 setup, search API, search UI, results list |
| **F** | 11–12 | Edit a note, save, see changes persist; conflict detection on save. | `NSTextView` wrapper, write path, op log v0, reindex-on-save |

## Cross-cutting concerns

- **Op log infrastructure** starts in Milestone F at coarse granularity (one entry per save). Fine-grained per-edit operations come in V1.x. Compaction policy from `05_locked_architecture_decisions.md` §7.5 is implemented but rarely triggered at this scale.
- **Performance benchmarks** start in Milestone A and run at the close of every milestone. The harness lives in `crates/yana-core/benches/`; baselines are recorded in `BENCHMARKS.md` at the workspace root. By end of F, the V1-release-gate targets from `05` §9.5 must be measurable on the benchmark suite (not the actual release gate yet — that's months 3+).
- **Mobile-friendly API discipline.** Even though only Mac ships in this phase, the Rust API stays paged + opaque-handle-based + cooperatively-cancellable per the locked decisions. No "load whole vault" shortcuts.
- **No sync writer.** All milestones produce an app that's read+edit on local files only. Sync detection (warn if `.obsidian/plugins/obsidian-livesync/` is present, or iCloud Drive markers) ships separately and not in this phase.
- **Tester compensation.** If testers spend real time on builds, they get paid per project principle (`feedback_oss_a11y_contribution.md`). Build it into the budget thinking now, not at first invoice.
- **Vault file safety.** Every file write goes through the atomic temp+rename pattern. Never overwrite directly. Conflict detection in Milestone F is the first safety net; full snapshot history (the "history" workstream from `docs/plans/03`) is V1.x.

## Explicitly NOT in months 0–3

- Math, Mermaid, code-block visual rendering (V1.x — see `05` §6.2, §6.3, §6.4).
- Citations and bibliography (V1.x — see §6.5).
- Bases / query builder / saved queries (V1.x — see §8).
- Tier 1 config-based plugins beyond saved-vault paths (V1.x — see §10).
- Sync writer (V2).
- Accessible structured conflict resolution UI beyond file-level (V2 — see §7.3).
- iOS, Windows, Android UI (later platforms — see §3).
- Themes, dark mode polish, command palette as a first-class surface.

## Risk register

| Risk | Likely impact | Mitigation |
|---|---|---|
| Tester feedback invalidates a UI choice mid-phase | Refactor cost on a 2-week slice | 2-week build cadence catches it; each milestone is a feedback opportunity, not a blocking review |
| `NSTextView` accessibility surprises in Milestone F | Could blow up F's timeline | Prototype the `NSViewRepresentable` wrapper in week 10 alongside Milestone E, not from scratch in week 11 |
| File watcher reliability on macOS | Stale UI when external tools modify the vault | Tested in Milestone A; refresh-on-foreground fallback per `05` §9.3 |
| Tree-sitter parse-tree memory at vault scale | OOM on large vaults | Tree-sitter doesn't enter the picture until V1.x (semantic spans). Until then, the parse trees are pulldown-cmark which is lighter |
| SQLite write performance on large initial vault open | Slow first open | Benchmark Milestone A; `parse_workers` in `SessionConfig` already lets us parallelize |
| Tester goes silent | Hard to validate without feedback | Ask why; don't assume; the cohort is small enough to actually check in |
| Op log file format choice in F locks us in too early | Hard to migrate later | Start with a versioned header byte and a length-prefixed record format; explicit version field makes future migration possible |
| Solo developer burnout over 16 weeks | Phase slips, momentum dies | Ship at end of each milestone, no exceptions. Don't compound milestones |

## Issue tracking conventions

- One GitHub Milestone per A–F (already created; links above).
- One issue per concrete task, with labels: `backend`, `swift-ui`, `schema`, `a11y`, `test`, `benchmark`, `tester-feedback`. Cross-cutting: `blocked`, `for-tester-review`.
- Tester-feedback issues are filed by the developer based on tester responses, with the tester credited. Public unless the tester wants otherwise.

## References

- `05_locked_architecture_decisions.md` — locked stack, API surfaces, schema. This phase implements §4 (API), §6 (content pipelines, partially), §7 (data model, partially), §9 (performance constraints in full).
- `01_detailed_roadmap.md` — phase model. This document is the concrete decomposition of "Phase 1: Accessible vault MVP" in `01` §3.
- `03_phase_1_plan.md` — earlier Phase 1 plan from the Flutter era. Most of this is superseded by `05` and this doc; some workstream sequencing ideas still apply.
- `.claude/projects/-Users-coryj-Dev-yana/memory/project_testers.md` — context on the tester cohort (4 committed AT users, compensated per project principle).
- `BENCHMARKS.md` — V1 baseline + how-to-run for the criterion harness.
