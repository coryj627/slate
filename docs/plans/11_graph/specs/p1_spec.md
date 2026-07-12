# P1 executable spec — Accessible navigator: Connections leaf, graph table, commands & presets

Issues: P1-1 ([#554](https://github.com/coryj627/slate/issues/554)) · P1-2 ([#555](https://github.com/coryj627/slate/issues/555)) · P1-3 ([#556](https://github.com/coryj627/slate/issues/556)).
Milestone: [GH 16](https://github.com/coryj627/slate/milestone/16). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 6–7; DoD §P-A/§P-B). The full U-program Presentation-Ready DoD (`../../08_ui_parity/00_program.md` §A–§G) applies to every P1 issue — empty/loading/error/populated states, APCA Lc ≥ 75 measured both appearances, a11y-check 100/100.

**Execution order: P1-1 → P1-2 → P1-3.** Gate: P0 wave complete (P0-4 censuses clean). P1 is the milestone's accessibility gate: **no P2 issue merges until P1 is feature-complete** (DoD §P-A).

Baseline facts (verified 2026-07-04; **re-verified 2026-07-11** — main 9ea8d21; symbol names outrank line numbers):

- Right-pane leaf registry: `Leaf` enum + `Leaf.registered` + `leafContent(_:)` switch in `apps/slate-mac/Sources/SlateMac/Workspace/RightPaneView.swift` (cases :22-41, registered list :99, content switch :279); panel identity is preserved across leaf switches (do not re-create panels per switch — the mounted-ZStack retention pattern documented at RightPaneView.swift:220-247, `BibliographyPanel.segment` regression note :227). **Newest-leaf precedent is O-5's `.history` (PR #835)** — copy its PR shape: leaf case + registry placement + v7-divergent `SlateSymbol` + `RightPaneViewTests`/`LeafPortTests` extension + `AppState+<Feature>.swift` file + `AnnouncementPosting`-injected announcement tests. Registry order is **usage-frequency, not append-last** (`.history` was inserted mid-list); `.connections` likely belongs near its semantic siblings `.backlinks`/`.outgoingLinks`.
- Panel state discipline: `LeafEmptyState` / `LeafSection` — now in `LeafChrome.swift` (:28/:52); `ContentBlockPanels.swift` is just a consumer.
- FFI call pattern: `Task.detached(priority: .userInitiated)` + post-await `@MainActor` publish; the exemplar to copy is O-5's compute-then-publish with post-await guards (`!Task.isCancelled`, `currentSession === session`, selection + seq recheck, injectable publish-gate test seam) at `AppState+History.swift:168-209`. Announcements post through the injectable `AnnouncementPosting` seam (`AnnouncementPosting.swift`) for test spyability — the bare global `postAccessibilityAnnouncement(_:priority:)` is un-spyable (NSApp-nil early return).
- Announcement funnel: **there is no app-generic "T announcement coordinator."** T landed `CanvasAnnouncer` (Canvas/CanvasAnnouncer.swift — typed `CanvasEvent` grammar, `@Published` verbosity terse/standard/verbose, ~200 ms same-class coalescing final-state-wins, `whereAmIText`, enforced by `testNoDirectAnnouncementsUnderCanvas`); Bases posts directly. **P1-1 builds a `GraphAnnouncer` on the CanvasAnnouncer pattern** — typed graph event enum, shared verbosity setting, coalescing, plus a `testNoDirectAnnouncementsUnderGraph` source-scan lint over the graph sources — rather than pretending a shared component exists. Wherever these specs say "the T announcement coordinator," read "GraphAnnouncer, built to t0's rules."
- Workspace tab seam: `EditorItem` serialization reserves the `"graph"` discriminator (WorkspaceModel.swift:39, U1-6 schema). **The case-addition precedent is now N's `.base` (PR #772)** (+ `.savedQuery`/`.dashboard` for non-path payloads): case + `WorkspaceStore` Item mapping + known-kinds list (`WorkspaceStore.swift:119` — add `"graph"`) + a `GraphTabRoutingTests` modeled on `BasesTabRoutingTests`/`CanvasTabRoutingTests`. **Mechanical to-do:** `testEditorItemCodableRoundTripAndUnknownKind` (WorkspaceModelTests.swift:730-743) uses `{"kind":"graph"}` as its unknown-kind forward-compat probe — realizing `.graph` must repurpose that probe to a still-unknown synthetic kind. Command registration in `SlateCommands.swift` + `CommandRegistryTests`.
- Grid: **`AccessibleDataGrid` v2 landed** (#519 closed 2026-07-04; NSTableView-backed, native AX row/column readout + `NSAccessibilitySortDirection` headers, per-column sort comparators, `DataGridSortState`, selection bindings, `sortsRowsLocally`, AX custom row actions, row-reuse virtualization, Home/End + type-ahead, injectable `announce:`). Consumers to copy: `Bases/BaseContainerView.swift` (fullest — sort + selection + filter announcements) and `Canvas/CanvasTableView.swift` (a tab-content table, closest to the graph tab). The grid's stated budget commentary is 2,000 rows — P1-2's 10k-row test deliberately exceeds it; verify row-reuse holds at 10k in the PR.
- Icon layer: `SlateSymbol.swift` — semantic cases with SF Symbols v7 + macOS 15-floor fallbacks + mandatory labels (`SlateSymbolTests.testEveryFallbackIsFloorSafe` against `knownMacOS15SafeSymbols`). `.backlinks` (:86) and `.outgoingLinks` (:88) exist; **no `.graph` or `.connections` case yet.** ⚠️ `point.3.connected.trianglepath.dotted` is TAKEN — it is `.diagram`'s glyph (Mermaid leaf, always-visible rail); pick distinct glyphs for `.graph`/`.connections` or document a deliberate share with rationale.
- Hit targets: every new glyph-only control gets `.frame(minWidth: 28, minHeight: 28)` with the citing comment ("HIG macOS DEFAULT click target 28×28pt; HIG minimum 20, WCAG 2.5.8 minimum 24 — 28 clears all three") per #866/#884 — there is no shared token yet, the convention is per-site.
- Backend surface consumed (P0-3): `graph_snapshot(GraphFilter)`, `graph_neighborhood(path, depth, filter)`, `graph_generation()`; plus existing `note_load_bundle` (session.rs:3784) and `list_unresolved_links` (session.rs:3805).
- Canvas t0 interaction contract (`../../09_canvas/specs/t0_interaction_contract.md`) is the normative announcement/verbosity reference (the R1/R2/R3 command rules live in the T program doc §Shortcut allocation); P1 adopts those rules rather than restating them.
- In-flight chord work to sequence against: HIG-audit #848 (unified focus-routed ⌘=/⌘−/⌘0 zoom) and #863 (⌘O/⇧⌘O/⌘T/⇧⌘T/⌘R reallocation — decided 2026-07-11, implementation its own PR; local branch `claude/chords-and-zoom` exists). P1 registers **zero new chords** so it is collision-free by construction, but P1-3's `SlateCommandsTests` additions may need a rebase if those PRs land in between; do not cite the old ⌘T/⌘O map in PR descriptions.

---

## P1-1 · Connections leaf — the local graph, accessibly (#554) — PR 1

The Roam-derived in/out split (research brief §5): the focused note's relational neighborhood as structured, keyboard-navigable lists — this *is* the local graph, projected accessibly.

### Surface

New `Leaf.connections` (icon: new `SlateSymbol.connections`; label "Connections") + `ConnectionsPanel` in the leaf registry. Content, top to bottom:

1. **Header:** note display name + summary line = P0-3 neighborhood `audio_summary`.
2. **Depth control:** stepper/segmented 1–3 ("Links", "2 links away", "3 links away"), default 1, persisted per-vault in `.slate/graph.json` (schema owned by P2-4; until it lands, `@AppStorage`-equivalent vault-scoped default with a TODO referencing P2-4).
3. **"Linked from" section** (incoming) and **"Links to" section** (outgoing), each a `LeafSection`: rows show target label, kind badges (embed / ghost / attachment — icon + text per §B, never color alone), and snippet (from existing `Backlink`/`OutgoingLink` data at depth 1). At depth ≥ 2 rows gain a disclosure; expanding shows that node's neighbors as nested rows (data: `graph_neighborhood(path, depth)`, expansion is presentation of the already-fetched neighborhood, not a re-query per disclosure).
4. **Ghost rows** ("unresolved") carry a "Create note" action (routes to the existing new-note flow pre-filled with the authored target; U2-2 file creation, landed #502).
5. **Bases handoff (gap O15, n3 §N3-4 rule 1):** Milestone N reserved a Bases row context-menu slot for "Show local graph," deferred until this leaf exists. P1-1 (or a same-wave follow-up filed in the PR) wires that action to `Leaf.connections` re-rooted on the row's note — the reservation must not be silently dropped.

### Interaction (normative)

- Row focus order: header → depth control → incoming rows → outgoing rows. Arrow keys move within lists; ⌥↓/⌥↑ jump between sections (registered commands, T rule R2: plain-arrow behavior has palette equivalents).
- Return opens the note in the active tab; ⌘Return in a new tab; "Show connections" row action re-roots the panel on that node (breadcrumb back-stack, ⌘[ returns — matching app back conventions).
- Data loads via `note_load_bundle` at depth 1 (one mutex acquisition, existing path) and `graph_neighborhood` at depth ≥ 2; refresh on selection change and on `graph_generation()` change after `VaultEventListener` file-change/scan-finished events (P0-3 refresh contract).

### VoiceOver copy (normative)

- Row label: `"{label}, {n} links in, {m} links out"`; ghosts: `"{label}, unresolved, {n} references"`; embeds append `", embed"`.
- On depth change: announce the refreshed neighborhood `audio_summary`.
- On re-root: `"Connections: {label}"` then summary. All announcements route through the `GraphAnnouncer` (verbosity honored; enforced by `GraphAnnouncerTests.testNoDirectAnnouncementsUnderGraph`).

**Implementation notes (P1-1 landed):** the "Show Connections" reveal is a single `slate.graph.showConnections` command in the `.view` menu (no chord — P1 registers none; the full `CommandSection.graph` + presets + depth commands land with P1-3). Data loads through `graph_neighborhood` for structure + metrics + the pre-rendered `audioSummary` at every depth, with `note_load_bundle` overlaid at depth 1 for per-row snippets. The "Reveal in File Tree" row action is deferred to P1-2, which builds the shared reveal-with-expand helper the graph table also needs (no AppState-level tree-reveal API exists today; faking it as "Open" would mislead). Depth persistence is session-state with a TODO for `.slate/graph.json` (owned by P2-4). The Bases O15 handoff is wired in both grid and list row actions (`basesShowConnections`).

### Tests

Leaf registration + panel-identity (no re-create on switch); row copy strings verbatim; depth clamp; ghost create-note routing; keyboard path E2E (XCUITest keyboard-only walk: focus leaf → depth 2 → expand → open in new tab); a11y-check on the panel; empty (orphan note) / loading / error states.

## P1-2 · Graph tab, Table mode — the global graph as a grid (#555) — PR 2

### Surface

`EditorItem` gains the `.graph` case (discriminator already reserved — this PR realizes it, following N's `.base` PR #772 shape; workspace census I1–I7 must stay green, serialization round-trip test extended, `"graph"` added to `WorkspaceStore`'s known-kinds list, and the unknown-kind probe in `testEditorItemCodableRoundTripAndUnknownKind` repurposed to a still-unknown synthetic kind). Opens via P1-3's "Open graph" command; tab label "Graph", icon `SlateSymbol.graph` (new case; SF Symbols v7 pick with macOS 15 fallback — **not** `point.3.connected.trianglepath.dotted`, which is `.diagram`'s glyph; implementer picks a distinct floor-safe glyph and verifies in `SlateSymbolTests`).

Tab content = **Table mode** (sole mode until P2-3 adds Diagram and the U3-style mode toggle; leave the toggle seam: a `GraphTabMode` enum with one live case and the switch container in place).

Columns (all sortable): Note (label), Links in, Links out, Embeds in, Embeds out, Component, Modified, Folder, Kind. Default sort: Links in, descending (hubs first — research brief §2 "maturity/hub" workflows). (Bases' shipped `file.inDegree`/`outDegree` fold links+embeds into one number — deliberate delta, recorded in p0_spec §P0-2; the split columns here are why no information is lost.) Filter bar above the grid: text filter (label substring, case/diacritic-insensitive), toggles for Attachments / Unresolved / Orphans only — exactly `GraphFilter` semantics, live row-count announced on change (`"{k} of {n} shown"`).

Data: `graph_snapshot(filter)` once per generation, sorted/filtered client-side (10k nodes of small records is fine in-memory; virtualized rendering via the grid). Grid: `AccessibleDataGrid` v2 (landed, #519) — sortable headers via `DataGridSortState`, keyboard row model, VoiceOver row/column readout; copy the `BaseContainerView`/`CanvasTableView` wiring (sortState + `sortsRowsLocally` + `announce:` hooked into the GraphAnnouncer funnel).

Row actions (menu + keyboard, drift-tested against Diagram actions when P2-5 lands, DoD §P-B): Open (Return), Open in new tab (⌘Return), Show connections (focuses `Leaf.connections` re-rooted on the row), Reveal in file tree. Ghost rows: Create note.

### Tests

Workspace censuses green with `.graph` tabs (open/close/split/serialize round-trip); sort determinism (ties break by key); filter↔announcement copy verbatim; 10k-row synthetic snapshot renders virtualized (no full materialization — instrument row instantiation); keyboard-only sort + filter + act E2E; a11y-check 100/100.

**Implementation notes (P1-2 landed):** `EditorItem.graph` is a **singleton** (no path payload — one graph tab per workspace; `openTab`'s identity dedup activates rather than duplicates); the `{"kind":"graph"}` unknown-kind probe in `WorkspaceModelTests`/`WorkspaceStoreTests` was repurposed to a still-unknown kind (`excalidraw`) and `"graph"` added to `WorkspaceStore`'s known-kinds list. Tab body dispatches in `WorkspaceView.paneContent` (a `.graph` branch BEFORE the note fallback) → `GraphContainerView`, which hosts the `GraphTabMode` seam (one live `.table` case) + the filter bar and renders `GraphTableView` on `AccessibleDataGrid` v2. Snapshot fetched once per generation via `graph_snapshot`, sorted by the grid (default: Links-in descending, label tie-break) and text-filtered client-side; the backend GraphFilter toggles (Attachments/Unresolved/Orphans) re-fetch. Generation-driven refresh extends the P1-1 `VaultEventListener` wiring (`refreshGraphTableIfGraphChanged`). "Open Graph" is a `.view` command (`slate.graph.openTab`, no chord; migrates to `CommandSection.graph` in P1-3). The shared `revealInFileTree` helper (deferred from P1-1) is built here — expands ancestors + opens + focuses the tree — and wired into both the graph table and the Connections leaf row actions.

## P1-3 · Graph commands, presets & announcements (#556) — PR 3

New `CommandSection.graph` (cross-language enum change — backend + Swift in one PR per T's #369 precedent, doubly proven by N's Bases addition). **Discriminant: `Graph = 11`** — the core enum is `#[repr(u8)]` with Canvas = 8, **9 explicitly reserved for Excalidraw (Milestone XD)**, Bases = 10 (commands.rs; do not reuse the reservation). Commands (registry + palette + menu; no new chords — T rule R1, palette/menu are the paths; copy Bases' `testBasesCommandsRegisterInBasesSectionWithoutGlobalChords` shape as `testGraphCommandsRegisterInGraphSectionWithoutGlobalChords`):

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
