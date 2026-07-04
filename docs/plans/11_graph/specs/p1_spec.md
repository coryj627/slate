# P1 executable spec — Accessible navigator: Connections leaf, graph table, commands & presets

Issues: P1-1 ([#554](https://github.com/coryj627/slate/issues/554)) · P1-2 ([#555](https://github.com/coryj627/slate/issues/555)) · P1-3 ([#556](https://github.com/coryj627/slate/issues/556)).
Milestone: [GH 16](https://github.com/coryj627/slate/milestone/16). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 6–7; DoD §P-A/§P-B). The full U-program Presentation-Ready DoD (`../../08_ui_parity/00_program.md` §A–§G) applies to every P1 issue — empty/loading/error/populated states, APCA Lc ≥ 75 measured both appearances, a11y-check 100/100.

**Execution order: P1-1 → P1-2 → P1-3.** Gate: P0 wave complete (P0-4 censuses clean). P1 is the milestone's accessibility gate: **no P2 issue merges until P1 is feature-complete** (DoD §P-A).

Baseline facts (verified 2026-07-04, this worktree):

- Right-pane leaf registry: `Leaf` enum + `Leaf.registered` + `leafContent(_:)` switch in `apps/slate-mac/Sources/SlateMac/Workspace/RightPaneView.swift` (cases ~:29, registered list ~:80, content switch ~:276); panel identity is preserved across leaf switches (do not re-create panels per switch — see the `BibliographyPanel.segment` regression note at RightPaneView.swift:205). M-3's `Leaf.syncDiagnostics` is the newest-leaf precedent; follow its PR shape.
- Panel state discipline: `LeafEmptyState` / `LeafSection` (ContentBlockPanels.swift).
- FFI call pattern: AppState dispatches via `Task.detached` + `@MainActor` publish (AppState.swift:2867 pattern); announcements via `postAccessibilityAnnouncement(_:priority:)`.
- Workspace tab seam: `EditorItem` serialization reserves the `"graph"` discriminator (WorkspaceModel.swift:38, U1-6 schema); `WorkspaceStore.swift:15` notes the N/T/P discriminators. Tabs open via `WorkspaceModel` operations (U1); command registration in `SlateCommands.swift` + `CommandRegistryTests`.
- Grid: `AccessibleDataGrid.swift` (v1) exists; **v2** (sortable, virtualized, keyboard row model) is Canvas issue [#519](https://github.com/coryj627/slate/issues/519), shared with Milestone N. P1-2 builds on v2 when landed; otherwise implements on v1 and files the swap as a follow-up noted in the PR.
- Icon layer: `SlateSymbol.swift` — semantic cases with SF Symbols v7 + macOS 15-floor fallbacks + mandatory labels (`SlateSymbolTests.testEveryFallbackIsFloorSafe`). `.backlinks` (:74) and `.outgoingLinks` (:76) exist; **no `.graph` or `.connections` case yet.**
- Backend surface consumed (P0-3): `graph_snapshot(GraphFilter)`, `graph_neighborhood(path, depth, filter)`, `graph_generation()`; plus existing `note_load_bundle` (session.rs:1437) and `list_unresolved_links` (session.rs:1459).
- Canvas t0 interaction contract (`../../09_canvas/specs/t0_interaction_contract.md`) is the normative announcement/verbosity/command-rules reference; P1 adopts its rules rather than restating them.

---

## P1-1 · Connections leaf — the local graph, accessibly (#554) — PR 1

The Roam-derived in/out split (research brief §5): the focused note's relational neighborhood as structured, keyboard-navigable lists — this *is* the local graph, projected accessibly.

### Surface

New `Leaf.connections` (icon: new `SlateSymbol.connections`; label "Connections") + `ConnectionsPanel` in the leaf registry. Content, top to bottom:

1. **Header:** note display name + summary line = P0-3 neighborhood `audio_summary`.
2. **Depth control:** stepper/segmented 1–3 ("Links", "2 links away", "3 links away"), default 1, persisted per-vault in `.slate/graph.json` (schema owned by P2-4; until it lands, `@AppStorage`-equivalent vault-scoped default with a TODO referencing P2-4).
3. **"Linked from" section** (incoming) and **"Links to" section** (outgoing), each a `LeafSection`: rows show target label, kind badges (embed / ghost / attachment — icon + text per §B, never color alone), and snippet (from existing `Backlink`/`OutgoingLink` data at depth 1). At depth ≥ 2 rows gain a disclosure; expanding shows that node's neighbors as nested rows (data: `graph_neighborhood(path, depth)`, expansion is presentation of the already-fetched neighborhood, not a re-query per disclosure).
4. **Ghost rows** ("unresolved") carry a "Create note" action (routes to the existing new-note flow pre-filled with the authored target; U2-2 file creation, landed #502).

### Interaction (normative)

- Row focus order: header → depth control → incoming rows → outgoing rows. Arrow keys move within lists; ⌥↓/⌥↑ jump between sections (registered commands, T rule R2: plain-arrow behavior has palette equivalents).
- Return opens the note in the active tab; ⌘Return in a new tab; "Show connections" row action re-roots the panel on that node (breadcrumb back-stack, ⌘[ returns — matching app back conventions).
- Data loads via `note_load_bundle` at depth 1 (one mutex acquisition, existing path) and `graph_neighborhood` at depth ≥ 2; refresh on selection change and on `graph_generation()` change after AppState-observed mutations (P0-3 refresh contract).

### VoiceOver copy (normative)

- Row label: `"{label}, {n} links in, {m} links out"`; ghosts: `"{label}, unresolved, {n} references"`; embeds append `", embed"`.
- On depth change: announce the refreshed neighborhood `audio_summary`.
- On re-root: `"Connections: {label}"` then summary. All announcements route through the T announcement coordinator (verbosity setting honored).

### Tests

Leaf registration + panel-identity (no re-create on switch); row copy strings verbatim; depth clamp; ghost create-note routing; keyboard path E2E (XCUITest keyboard-only walk: focus leaf → depth 2 → expand → open in new tab); a11y-check on the panel; empty (orphan note) / loading / error states.

## P1-2 · Graph tab, Table mode — the global graph as a grid (#555) — PR 2

### Surface

`EditorItem` gains the `.graph` case (discriminator already reserved — this PR realizes it; workspace census I1–I7 must stay green, serialization round-trip test extended). Opens via P1-3's "Open graph" command; tab label "Graph", icon `SlateSymbol.graph` (new case; SF Symbols v7 pick with macOS 15 fallback, e.g. `point.3.connected.trianglepath.dotted` family — implementer verifies floor-safety in `SlateSymbolTests`).

Tab content = **Table mode** (sole mode until P2-3 adds Diagram and the U3-style mode toggle; leave the toggle seam: a `GraphTabMode` enum with one live case and the switch container in place).

Columns (all sortable): Note (label), Links in, Links out, Embeds in, Embeds out, Component, Modified, Folder, Kind. Default sort: Links in, descending (hubs first — research brief §2 "maturity/hub" workflows). Filter bar above the grid: text filter (label substring, case/diacritic-insensitive), toggles for Attachments / Unresolved / Orphans only — exactly `GraphFilter` semantics, live row-count announced on change (`"{k} of {n} shown"`).

Data: `graph_snapshot(filter)` once per generation, sorted/filtered client-side (10k nodes of small records is fine in-memory; virtualized rendering via the grid). Grid: `AccessibleDataGrid` v2 (#519) — sortable headers, keyboard row model, VoiceOver row/column readout; if #519 hasn't landed, v1 + manual sort headers, and the PR files the v2 swap follow-up.

Row actions (menu + keyboard, drift-tested against Diagram actions when P2-5 lands, DoD §P-B): Open (Return), Open in new tab (⌘Return), Show connections (focuses `Leaf.connections` re-rooted on the row), Reveal in file tree. Ghost rows: Create note.

### Tests

Workspace censuses green with `.graph` tabs (open/close/split/serialize round-trip); sort determinism (ties break by key); filter↔announcement copy verbatim; 10k-row synthetic snapshot renders virtualized (no full materialization — instrument row instantiation); keyboard-only sort + filter + act E2E; a11y-check 100/100.

## P1-3 · Graph commands, presets & announcements (#556) — PR 3

New `CommandSection.graph` (cross-language enum change — backend + Swift in one PR per T's #369 precedent). Commands (registry + palette + menu; no new chords — T rule R1, palette/menu are the paths):

| Command | Behavior |
|---|---|
| Open graph | Opens/activates the `.graph` tab (Table mode) |
| Show connections | Reveals + focuses `Leaf.connections` for the active note |
| Graph: orphaned notes | Graph tab, filter = orphans-only, announce `"{k} orphaned notes."` |
| Graph: unresolved links | Graph tab, filter = ghosts visible + sorted by Links in desc, kind-filtered to ghosts; announce `"{k} unresolved targets."` |
| Graph: most linked notes | Graph tab, default filter, sort Links in desc; announce top row |
| Connections: deeper / shallower | Depth ±1 in the leaf (keyboard path to the stepper) |

Presets are *parameterizations of the table*, not new surfaces — one model, projections stay thin. Command names, announcement strings, and palette keywords ("orphans", "broken links", "hubs") are normative copy. `CommandRegistryTests` extended; the chord↔surface drift test asserts no new chords.

### Tests

Registry entries + palette reachability; preset filter/sort assertions; announcement strings verbatim; VoiceOver E2E: invoke each preset from the palette, hear count, land in grid with focus on first row.

---

**Wave-2 exit (= DoD §P-A gate for P2):** all three PRs merged; a keyboard-only and VoiceOver walkthrough completes: find orphans → open one → inspect its connections at depth 2 → follow a link → create a note from a ghost — with zero pointer use. Record the walkthrough script in the P1-3 PR description (it becomes the docs/help/graph.md §keyboard source, P-D).
