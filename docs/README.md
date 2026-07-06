# Slate — Documentation

Planning, architecture, and end-user docs for Slate, an accessibility-first,
Obsidian-vault-compatible knowledge workspace. Project overview and build
instructions are in the [top-level README](../README.md).

Statuses below reflect each program's own `Status:` header and its GitHub
milestone state; they can lag reality by a few days — the milestone is the
source of truth.

## Start here

- [`plans/05_locked_architecture_decisions.md`](plans/05_locked_architecture_decisions.md) — **the canonical reference.** The locked decisions every milestone inherits (native-UI-per-platform, accessibility owned by the Rust core, content pipelines, editor model, queries/Bases, FFI tooling, platform order).
- [`plans/06_v1_milestones.md`](plans/06_v1_milestones.md) — the V1 milestone decomposition (A–R) and the rule that a program doc supersedes this file where they differ.
- [`plans/07_portability_review.md`](plans/07_portability_review.md) — the accessibility + Windows-reuse review that shapes the FFI boundary and the editor/spans work.
- [`plans/13_repo_structure.md`](plans/13_repo_structure.md) — ADR: single monorepo, one native app per `apps/slate-<platform>/`.
- [`plans/14_l10n.md`](plans/14_l10n.md) — localization strategy (umbrella, not a milestone).

## Origin documents

The earliest planning artifacts, kept for provenance. They predate the locked
architecture and the rename to Slate — treat `05` and later as authoritative
where they conflict.

- [`plans/01_detailed_roadmap.md`](plans/01_detailed_roadmap.md) · [`plans/02_phase_0_plan.md`](plans/02_phase_0_plan.md) · [`plans/03_phase_1_plan.md`](plans/03_phase_1_plan.md) · [`plans/04_draft_architecture_plan.md`](plans/04_draft_architecture_plan.md) — roadmap, discovery/spike plans, MVP plan, draft architecture.
- [`plans/initial_chat_and_research.md`](plans/initial_chat_and_research.md) — the research transcript the pack was generated from.

## Program & milestone specs

Each directory is a program: a `00_program.md` (or `00_plan.md`) with locked
decisions and a DoD, plus per-slice executable `specs/`. Grouped by state.

**Shipped / code-complete**

- [`plans/08_ui_parity/`](plans/08_ui_parity/00_program.md) — **Milestone U** (U0–U5): workspace shell (tabs + splits), file tree + management, reading/editing editor with inline properties, right-pane leaves, presentation polish. ✅ Complete.
- [`plans/09_canvas/`](plans/09_canvas/00_program.md) — **Milestone T**: accessible JSON-Canvas (model, authoring, navigator, outline/table/renderer projections, UIA-ready spans). 🏁 Code-complete; human AT smoke pass is the only residual.
- [`plans/09_sync_cli/`](plans/09_sync_cli/00_plan.md) — **Milestone M**: sync detection + diagnostics + the read/write `slate` CLI (v1). ✅ Shipped.

**Specs drafted — implementation not started**

- [`plans/17_bases/`](plans/17_bases/00_program.md) — **Milestone N**: Bases v1 — `.base` files, accessible query builder + data grid, tasks-as-queryable.
- [`plans/10_local_history/`](plans/10_local_history/00_plan.md) — **Milestone O**: local history + change tracking (accessible diff).
- [`plans/11_graph/`](plans/11_graph/00_program.md) — **Milestone P**: graph view — accessible model first, visual graph as a projection.
- [`plans/12_autocomplete/`](plans/12_autocomplete/00_program.md) — **Milestone V**: editor autocomplete (Editor Intelligence bucket).
- [`plans/12_latex_suite/`](plans/12_latex_suite/00_program.md) — **Milestone X**: optional LaTeX authoring aids over the shipped math pipeline.
- [`plans/15_files_sidebar/`](plans/15_files_sidebar/00_program.md) — **Milestone FL**: files sidebar navigator (Notebook Navigator parity).
- [`plans/16_excalidraw/`](plans/16_excalidraw/00_program.md) — **Milestone XD**: read-only Excalidraw viewer.
- [`plans/18_windows_port/`](plans/18_windows_port/00_program.md) — **Milestone W**: the WPF + AvalonEdit + UIA Windows port on the same `slate-core`. ⏸ Parked (see the program's entry criteria); specs are ready.

## Help, runbooks & reference

- [`help/`](help/) — end-user feature guides ([Canvas](help/canvas.md)).
- [`runbooks/`](runbooks/) — operational procedures ([VoiceOver feature-test](runbooks/voiceover-feature-test.md)).
- [`diagrams/`](diagrams/) — Mermaid architecture diagrams ([manifest](diagrams/MANIFEST.md)) and source `mmd/`.
- [`research/`](research/) — [competitive landscape and resources](research/competitive-landscape-and-resources.md).

## Notes

These are planning and design artifacts, not final engineering specifications
or legal advice. Where an executable spec and this prose disagree, the spec
and the code win.
