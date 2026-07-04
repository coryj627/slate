# 06 — V1 Milestone Decomposition (read+edit Mac alpha)

**Status (2026-06-10):** ✅ Alpha milestones A–F shipped (read+edit Mac alpha). ✅ Post-alpha tester-driven milestones G–J shipped (tasks, templates, property editing, embeds). ✅ K (content pipelines) and L (citations) closed on GitHub — K's user-facing surfaces landed with #410 (sidebar panels routing MathCAT speech / code preambles / diagram descriptions); L's config contract was fixed by #411 (vault-shipped `slate.json` honored). 🟡 V1.x milestones M–P open: sync diagnostics + CLI (M), Bases (N), local history + change tracking (O), Graph view (P). Per-milestone close dates are in the tables below; live scope lives on the GitHub Milestones (links inline).

**Strategic goal of the alpha phase:** the 4 committed AT-user testers have a Mac app they can use against their own existing Obsidian vaults, with their own screen readers, to do all four primary read-and-write workflows. The phase shipped six progressively-richer builds, not one big drop at the end.

**Pacing note for future phases:** the original plan budgeted 12–16 weeks; A–F closed in 6 calendar days (2026-05-17 → 2026-05-23), and G–J closed in the following 2 days. The 2-week-per-milestone cadence remains the right *commitment* shape for tester comms — don't promise an end date, promise the next build — even when execution runs faster.

## Where to find the live plan

Per-milestone goals, scope, schema, tests, and acceptance criteria live on **GitHub Milestones**, not in this file:

### Alpha (read+edit Mac alpha, A–F)

| Milestone | GitHub | Status |
|---|---|---|
| A — Vault + file list | [milestone 1](https://github.com/coryj627/slate/milestone/1) | ✅ Shipped (2026-05-17) |
| B — Read + heading nav | [milestone 2](https://github.com/coryj627/slate/milestone/2) | ✅ Shipped (2026-05-18) |
| C — Backlinks + outgoing links | [milestone 3](https://github.com/coryj627/slate/milestone/3) | ✅ Shipped (2026-05-18) |
| D — Frontmatter properties | [milestone 4](https://github.com/coryj627/slate/milestone/4) | ✅ Shipped (2026-05-19) |
| E — Full-text search | [milestone 5](https://github.com/coryj627/slate/milestone/5) | ✅ Shipped (2026-05-19) |
| F — Editing | [milestone 6](https://github.com/coryj627/slate/milestone/6) | ✅ Shipped (2026-05-23) — backend [#105](https://github.com/coryj627/slate/pull/105), UI [#106](https://github.com/coryj627/slate/pull/106) |

### Post-alpha tester-driven (G–J)

Added after A–F based on tester pull during the alpha. Not in the original plan; tracked here so the doc reflects what actually shipped.

| Milestone | GitHub | Status |
|---|---|---|
| G — Tasks | [milestone 7](https://github.com/coryj627/slate/milestone/7) | ✅ Shipped (2026-05-24) |
| H — Templates | [milestone 8](https://github.com/coryj627/slate/milestone/8) | ✅ Shipped (2026-05-23) |
| I — Property editing | [milestone 9](https://github.com/coryj627/slate/milestone/9) | ✅ Shipped (2026-05-25) |
| J — Embeds in the content view | [milestone 10](https://github.com/coryj627/slate/milestone/10) | ✅ Shipped (2026-05-25) |

### V1.x (K–P)

The full V1.x scope per `05_locked_architecture_decisions.md`. K and L are closed; ordering between M–P is not fixed; tester feedback during M will sharpen N's defaults, and O/P can ship in either order.

| Milestone | GitHub | Notes |
|---|---|---|
| K — Content pipelines (math, code, Mermaid) | [milestone 11](https://github.com/coryj627/slate/milestone/11) | ✅ Closed. Data path #257; user-facing sidebar surfaces #410. Implements `05` §6.2–6.4 (inline NSTextAttachment rendering stays deferred to V1.x). |
| L — Citations + bibliography | [milestone 12](https://github.com/coryj627/slate/milestone/12) | ✅ Closed. Implements `05` §6.5; vault-shipped `slate.json` config contract fixed by #411. |
| M — Sync detection + diagnostics + CLI v1 | [milestone 13](https://github.com/coryj627/slate/milestone/13) | Implements `05` §7.2 phase 1 + §10 Tier 2 (CLI). Local HTTP API ships separately later. **Plan + executable spec: [`09_sync_cli/`](09_sync_cli/00_plan.md)** (2026-07-03; supersedes the GH milestone description where they differ — leaf not sidebar, read-only CLI). |
| N — Bases v1 | [milestone 14](https://github.com/coryj627/slate/milestone/14) | Implements `05` §8. Biggest scope item in V1.x; recommended after K–M. |
| O — Local history + change tracking | [milestone 15](https://github.com/coryj627/slate/milestone/15) | Implements `05` §7.5, §8.9, and `03` §11. Consumes F's op-log infra; temporal-query slice (O-6 only) consumes N's Bases AST. **Plan + executable spec: [`10_local_history/`](10_local_history/00_plan.md)** (2026-07-03; supersedes the GH milestone description where they differ — save-point versions, two derived tables, leaf UI). |
| P — Graph view | [milestone 16](https://github.com/coryj627/slate/milestone/16) | Implements `05` §5.2 + `01` §7, **reordered: accessible relational model first, visual diagram as a projection** (owner decision 2026-07-04). **Program + executable specs: [`11_graph/`](11_graph/00_program.md)** (supersedes the GH milestone description and `01` §7's phase ordering where they differ). Research base: [`11_graph/01_research_brief.md`](11_graph/01_research_brief.md). |

Each GitHub Milestone carries the full Rust + Swift work breakdown, schema migrations, tests, accessibility checkpoints, tester-feedback questions, and definition of done — **except M, O, and P, whose authoritative contracts live in-repo** ([`09_sync_cli/`](09_sync_cli/00_plan.md), [`10_local_history/`](10_local_history/00_plan.md), [`11_graph/`](11_graph/00_program.md)); their GH milestone descriptions are the older sketch and yield to the specs where they differ. Individual issues link back to their milestone.

The UI-parity program (Milestone U, U0–U5 — tabs/splits, file tree, reading/editing modes, right-pane leaves, presentation polish) runs alongside V1.x and is tracked separately in [`08_ui_parity/00_program.md`](08_ui_parity/00_program.md) (GH milestones 23–28). M and O's UI surfaces build on U's leaf architecture.

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
- **Performance benchmarks** start in Milestone A and run at the close of every milestone. The harness lives in `crates/slate-core/benches/`; baselines are recorded in `BENCHMARKS.md` at the workspace root. By end of F, the V1-release-gate targets from `05` §9.5 must be measurable on the benchmark suite (not the actual release gate yet — that's months 3+).
- **Mobile-friendly API discipline.** Even though only Mac ships in this phase, the Rust API stays paged + opaque-handle-based + cooperatively-cancellable per the locked decisions. No "load whole vault" shortcuts.
- **No sync writer.** All milestones produce an app that's read+edit on local files only. Sync detection (warn if `.obsidian/plugins/obsidian-livesync/` is present, or iCloud Drive markers) ships separately and not in this phase.
- **Tester compensation.** If testers spend real time on builds, they get paid per project principle (`feedback_oss_a11y_contribution.md`). Build it into the budget thinking now, not at first invoice.
- **Vault file safety.** Every file write goes through the atomic temp+rename pattern. Never overwrite directly. Conflict detection in Milestone F is the first safety net; full snapshot history (the "history" workstream from `docs/plans/03`) is V1.x.

## Explicitly NOT in months 0–3 (alpha phase)

Original alpha-scope exclusions. Most of the V1.x items below now have GitHub milestones (K–P); the others remain unscheduled.

- Math, Mermaid, code-block visual rendering — Milestone K.
- Citations and bibliography — Milestone L.
- Bases / query builder / saved queries — Milestone N.
- Local history + change tracking — Milestone O.
- Graph view — Milestone P.
- Tier 1 config-based plugins beyond saved-vault paths (V1.x — see `05` §10; no milestone yet).
- Local HTTP API (V1.x — see `05` §10; ships separately after CLI in M).
- WASM tree-sitter grammar loading at runtime (V1.x — see `05` §6.4; no milestone yet).
- "Explain this function" semantic AT command + per-language semantic refinements (V1.x — see `05` §6.4; no milestone yet).
- `not-portable.md` migration-docs deliverable (V1.x — see `05` §10; no milestone yet).
- Sync writer (V2).
- Accessible structured conflict resolution UI beyond file-level (V2 — see `05` §7.3; data plumbing in O).
- iOS, Windows, Android UI (later platforms — see `05` §3).
- Themes, dark mode polish — Milestone R (themes/dark-mode polish).
- Command palette as a first-class surface — **shipped as Milestone Q** (`⌘⇧P`, fuzzy filter, recents, menu-mirroring registry). Was originally "no V1.x commitment; natural home here if testers ask" — testers asked, so it landed early.

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
- `.claude/projects/-Users-coryj-Dev-slate/memory/project_testers.md` — context on the tester cohort (4 committed AT users, compensated per project principle).
- `BENCHMARKS.md` — V1 baseline + how-to-run for the criterion harness.
