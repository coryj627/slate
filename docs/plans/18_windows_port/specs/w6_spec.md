# W6 executable spec — Structural surfaces: canvas & graph

Issues: W6-1 ([#745](https://github.com/coryj627/slate/issues/745)) · W6-2* ([#746](https://github.com/coryj627/slate/issues/746)). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue — **these are the two largest issues in the program**; each is delivered as a stacked PR series against a single issue (the issue stays the unit of acceptance; the PR is the unit of review). *(\* W6-2 iff Milestone P shipped — an entry criterion, so effectively unconditional; the marker exists only for matrix mechanics.)*
Program: [00_program.md](../00_program.md) (decision 14; DoD §W-A/§W-C/§W-D). **Depends on (wave-6 gate): W4-1** (canvas table/grid substrate) **and W5-1** (navigator command surface + finalized chord table). Behavioral reference: the T program (`../../09_canvas/00_program.md`, interaction contract `t0_interaction_contract.md`, AT checklist) and the P program (`../../11_graph/00_program.md`).

**Execution order: W6-1 → W6-2** (the W6-1 §W-G finding below applies to the graph announcer too — `a11y.rs:14–28` names both engines as residue — so W6-2 reuses W6-1's phase-0 pattern rather than discovering it again).

**W5 execution baseline (2026-08-22 refresh — facts the original spec predates):**

- **Both gates are satisfied.** W4-1 (#733) merged and is battle-tested through W4-2…W4-7; W5-1 (#741) merged 2026-08-13 — the chord table (`Commands/ChordTable.cs` → `chords.json` schemaVersion 3), registrar/resolvers, palette, and the three command-drift tests exist. W5-2/W5-3/W5-4 are also merged (main `a75d0e84`). **W6 is unblocked.**
- **The canvas read/write FFI is bound** (12 `VaultSession` entry points, `crates/slate-uniffi/src/lib.rs:9320–9459`) and the Windows shell already routes `.canvas` to a placeholder tab body (`WorkspaceViewModel.cs:285–297`) with `ActiveCanvasSurface` persisted. No new FFI is needed for the projections.
- **§W-D reality — the canvas announcer is NOT core-rendered (the decision-14 gap).** `crates/slate-core/src/a11y.rs:14–28` names "the canvas/graph announcers' verbosity machinery" as `HostComposed` residue; `tests/fixtures/a11y/corpus.json` contains zero canvas events; the whole t0 §1 grammar (verbosity matrix, group/connection phrases, confirmations, the undo hint, Where-am-I, coalescing) lives in `apps/slate-mac/Sources/SlateMac/Canvas/CanvasAnnouncer.swift` + ~20 prose sites. It moves to core first (W6-1 PR 0a), with mac consuming it — never into C#. A further dozen Swift-derived structural rules (containment queries, trace-path, relative description, auto-sides, bounds, constants, id minting, the filter predicate, speakable-name uniqueness) move in W6-1 PR 0b. The full audit table is [w6_1_canvas_spec.md §2](w6_1_canvas_spec.md#2-the-w-g-canonical-consumption-audit--seed-register).
- **Windows has no app-level undo manager** (editor `UndoStack`; structural `StructuralUndoJournal` with focus-scoped imperative Ctrl+Z/Ctrl+Y); canvas undo is a third focus-routed domain built on the same precedent (WGA-3).
- **Windows text scaling is greenfield** (only per-monitor-v2 DPI exists); W6-1 item 5 introduces it.
- **`chords.json` has zero `slate.canvas.*` rows** — the 56 + `slate.file.newCanvas` rows exist only as pending matrix rows; they land per PR with delivery evidence. Two chord collisions need adjudication (Ctrl+Alt+I is `toggleRightPane`; Ctrl+Alt+C is a Reading-scoped chord) — proposed mapping in the detailed spec §7.
- **Fluent theme (program decision 2 addendum):** canvas chrome, sheets, and pickers are Fluent-styled; the renderer is Slate-owned (token-backed fills, two-layer Contrast behavior, Mica policy applies to the surface background).

## W6-1 · Canvas (Milestone T parity) — PR series 1

**Detailed executable spec: [w6_1_canvas_spec.md](w6_1_canvas_spec.md)** — per-PR goal/consumes/builds/behavior/tests/evidence, the §W-G audit register, the owner decisions, and the chord mapping. The series (each PR runs the full contracts → red-team → codex → CI → codoki loop; contracts in `docs/plans/33_canvas_contracts.md`, one document, one section per PR):

| PR | Slice | Gate |
|---|---|---|
| 0a | **Core:** canvas announcer vocabulary → `a11y.rs` (verbosity as a parameter; first chord-bearing templates; four-place rule; mac consumes; residue count drops) | — |
| 0b | **Core:** structural queries, constants, id minting, filter, speakable names → `VaultSession` FFI (mac consumes; `canvas_queries` §W-A rows) | ∥ 0a |
| A | Canvas document VM + tab wiring (the one attach funnel) + outline tree peer + load states + `canvas_read` §W-A rows + open-path §K bench | 0a |
| B | Table projection on the W4-1 substrate | A |
| C | Navigator command layer, mode-controller infra (M1–M7 against a test mode), Esc ladder, Where-am-I + panel, filter, verbosity pref | A, 0b |
| D | Visual renderer (per-card peers, windowing), viewport commands, Windows text scaling, color/Contrast contract + APCA rows, renderer §K benches | C |
| E | Mutation funnel, canvas undo domain, core authoring verbs, pickers, card editor (real AvalonEdit on a scratch buffer, Esc commits), New Canvas, mutation-harness scenarios | C, 0b |
| F | Move/resize modes, structural placement commands, connect picker + connect mode | D, E |
| G | Mark-then-act, bulk verbs, create-connected-card, duplicate, convert-to-note | F |
| H | Close-out: E2E journey, §K/§W-A/§W-C/§W-D/§W-G evidence, matrix + `chords.json`, JAWS/NVDA checklist, issue reconciliation | G |

The original scope items, retained as the acceptance checklist (each maps to the PRs named):

1. Consumes the canonical layer T built: parser/model/derivation (reading order, containment, adjacency, summaries), `canvas_apply` FFI, placement engine, scene/outline/table projections, op-log undo. **None of that is re-derived** (§W-G); every Swift-derived need moves to core first (decision 14) — **PR 0a/0b**, closed by the §W-G register in PR H.
2. The T interaction contract is the behavioral spec: mode stack (move/resize/connect/…), Esc-commits ladder, navigator command layer, mark-then-act multi-select, announcer grammar + verbosity + "Where am I?" — re-hosted on WPF with the canonical announcement events (§W-D rows for the whole announcer corpus) — **PR 0a, C, F, G**.
3. Projections: outline (tree peer), table (W4-1 substrate), visual renderer with per-card UIA elements + windowing — the renderer's AT model mirrors the mac per-card AX element design, with SelectionItem/Selection patterns — **PR A, B, D**.
4. Authoring parity: full T verb set (create/delete/color/group/connect/edit/duplicate/convert/locate…), card editor, pickers, and nearest-preset naming (core-owned). Slate-owned dark/light canvas fills use the shared W1-1 token set and meet the APCA acceptance inherited from T. Under Windows Contrast themes, semantic roles collapse onto compatible dynamic `SystemColors` pairs and preserve meaning with text/icons/borders rather than color alone; user-customized system colors are not APCA-gated. This issue records the canvas-specific checks, while W8-2 locks the shared dark/light pairs and Contrast-theme behavior behind CI/UI automation — **PR D, E, F, G**.
5. Dynamic Type equivalent: renderer labels respect Windows text scaling — **PR D**.
6. §W-A rows: scene/outline/table/apply round-trips byte-identical; §K-scale budgets re-verified through the binding (canvas benches' fixture sizes) — **PR A, 0b, E; recorded H**.
7. JAWS/NVDA canvas checklist (T's `at_smoke_checklist.md` re-expressed for UIA) executed and recorded (W7-4 owns the format) — **PR H**.

## W6-2 · Graph view (Milestone P parity) — PR series 2

1. Consumes P's canonical model and its **accessible textual representation** (the entry-criterion artifact) + metrics substrate; accessible-first order per P's locked decisions (one model, two projections).
2. The P interaction model is the behavioral spec once shipped; same §W-A/§W-C/§W-D discipline; determinism guarantees (P rejected nondeterministic layout) hold identically through the binding.
3. **Phase 0 first (from W6-1):** the graph announcer is the other named residue engine (`a11y.rs:14–28`); run the same §W-G audit (announcer → core, Swift-derived structural rules → core, mac consumes) before the first Windows PR, and write its detailed spec in the W6-1 form.

- [ ] (each) matrix rows green; canonical-consumption audit (§W-G) recorded; AT checklists executed
