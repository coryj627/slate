# W6-2 executable spec — Graph on Windows (Milestone P parity): the stacked PR series

Issue: [#746](https://github.com/coryj627/slate/issues/746) · Milestone W (GH 22) · wave spec: [`w6_spec.md`](w6_spec.md) §W6-2 · program decisions consumed: 4 ("C# may contain"), 11 (a11y tooling gate), 12 (chords are host-table-owned), 14 (structural surfaces consume the canonical representations; a gap goes into core first). **Issue = unit of acceptance; PR = unit of review** — the W6-1 convention, with the per-PR loop of §5.4.

**Behavioral source (normative, in this order):** the P program's locked decisions ([`11_graph/00_program.md`](../../11_graph/00_program.md) §Locked scope decisions 1–10 and the §P-A…§P-E gates) → the P specs ([`p0_spec.md`](../../11_graph/specs/p0_spec.md) §P0-2 metrics and §P0-3 the normative `audio_summary` formats; [`p1_spec.md`](../../11_graph/specs/p1_spec.md) §"VoiceOver copy (normative)" and the P1 command catalog; [`p2_spec.md`](../../11_graph/specs/p2_spec.md) §P2-3 accessibility, §P2-4 inspector, §P2-5 shared selection) → the mac test suites (`GraphAnnouncerTests`, `GraphActionParityTests`, `GraphCommandsTests`, `GraphConfigTests`, `GraphDiagramTests`, `GraphTabRoutingTests`, `GraphTableViewTests`, `ConnectionsPanelTests` — 2,294 lines, the behaviour census) → [`docs/help/graph.md`](../../../help/graph.md) → the mac implementation under `apps/slate-mac/Sources/SlateMac/Graph/` (13 files), **never as the parity target where it cheats** (the §2 register names where it does).

---

## 0. Read this first — facts established 2026-09-03 (supersede the issue's 2026-08-09 delivery note where they differ)

1. **Both wave gates are satisfied.** W4-1 (#733, the grid substrate) and W5-1 (#741, the navigator command surface + finalized chord table, merged 2026-08-13) are on `main`; Milestone P shipped (GH milestone 16 closed) with the graph's canonical model, metrics, deterministic layout and the accessible textual representation in Rust. The issue body's "W5-1 pending" is stale.
2. **The graph's read-side FFI is bound and needs no new entry point for the projections.** `VaultSession.graph_snapshot(GraphFilter) -> GraphSnapshot` (`crates/slate-uniffi/src/lib.rs:1301`), `graph_neighborhood(path, depth, filter) -> GraphNeighborhood` (`:1307`), `graph_generation() -> u64` (`:1323`, the cheap change probe), `start_graph_layout(filter, forces, config) -> LayoutSession` (`:1337`) with `node_ids`, `edges`, `node_metadata`, `generation`, `tick`, `run_to_convergence(cancel)`, `set_forces`, `pin_node`/`unpin_node`, `refresh` (`:3570–3700`). The records (`GraphNode` with `in_links/out_links/in_embeds/out_embeds/component/is_orphan/pagerank/modified_ms`, `GraphEdge{kind, count}`, `GraphFilter{include_attachments, include_ghosts, orphans_only}`) are at `:3227–3391`. Two Windows censuses already exercise the layout handle's lifetime (`BindingSurfaceCensus.cs:69`, `HandleLifetimeCensus.cs:232`). **There is no graph mutation FFI and none is wanted**: the graph is a read surface; its only writes are "create the note a ghost names" (through the shell's existing note-creation seam) and the vault-local `.slate/graph.json` (whose schema moves to core, §2 row F).
3. **The "accessible textual representation" is two things, and only one of them is in core.** In core: the pre-rendered `audio_summary` strings — the snapshot and neighbourhood summaries of `p0_spec.md` §P0-3 (`session.rs:9017` and `:1990–1997`; pinned by `neighborhood_summary_verbatim_and_unknown_path_errors`, `session/tests/graph.rs:907`; since 0a the rows `GraphSnapshotSummary` and `GraphNeighborhoodSummary` of `35_graph_contracts.md` 0a-2b). In Swift only: **everything else a reader hears** — the P1 row copy with its terse collapse (`GraphRow`), the re-root label (`GraphReRooted`), the preset headlines (`GraphPreset`), the filter count (`GraphFilterCount`), the Where-am-I composition (`GraphWhereAmI`), the forces phrases (`GraphForceValue`), the tier-B summary (`GraphTierSummary`) — `GraphAnnouncer.swift` (239 lines) and 26 posting sites across `AppState+Graph*.swift`, `GraphTableView.swift`, `GraphDiagramView.swift`, `ConnectionsPanel.swift`; one `// W0.5-3 residue:` marker (`GraphAnnouncer.swift:98`). `corpus.json` holds zero graph-announcer entries; `a11y.rs:14–38` names the graph announcer as "the remaining named engine, and W6-2 does for it what 0a did". **Phase 0 is therefore not optional and blocks PR A**, exactly as it did for canvas.
4. **What the Windows shell already has.** The `Graph` tab kind (`WorkspacePersistence.cs:9–17`), a hard singleton at `"graph:singleton"` (`:375–377`; `WorkspaceViewModel.Layout.cs:178–192` `TryFocusGlobalGraph`), split refused with core's `GraphOpensSinglePane`, reopen with `ReopenedGraph` (`WorkspaceViewModel.Layout.cs:229–233, :475`; both events in `corpus.json`), Duplicate excluded (`:424–428`), and a **placeholder body** — `IsPlaceholder` falls through for Graph and renders "Graph is docked in this workspace. Its full surface ships in its owning milestone." (`WorkspaceViewModel.cs:276–290`). The right-pane leaf catalogue already lists `connections` (`WorkspaceViewModel.cs:1682`) as a placeholder leaf. Facts pinned today: `W1WorkspaceRedTeamTests.cs:117–125, 355–377` (singleton, Duplicate/Split no-ops, restore collapses two persisted graph tabs to one). There is no `Graph/` directory, no graph XAML, **zero** `slate.graph.*` rows in `chords.json`, no `ChordScope.Graph`, no `w_c_matrix.md` graph row; the parity matrix carries the twelve graph command rows as `pending` (`parity_matrix.md:139–150`), the `connections` leaf row (`:225`) and the surface row (`:276`).
5. **Core's command section says what the chord table must do.** `CommandSection::Graph = 11` is doc-commented "Registry + palette + menu paths only; **P1 registers zero new chords**" (`crates/slate-core/src/commands.rs:55–61`). The mac catalog (`SlateCommands.swift:1514–1618`) carries twelve ids; four are palette mirrors of focus-routed chords (`zoomIn` ⌘=, `zoomOut` ⌘-, `actualSize` ⌘0, `whereAmI` ⌃⌘I — routed by `zoomRouteTarget`/`whereAmIRouteTarget` between the canvas and the graph) and one is graph-owned (`fitGraph` ⌥⌘0). Windows already routes the canvas's four at the same chords in `ChordScope.Canvas` (`chords.json:851–1004`: `Ctrl+Alt+Shift+I`, `Ctrl+=`, `Ctrl+-`, `Ctrl+0`); the graph takes the **same chords in `ChordScope.Graph`** — disjoint delivery sites, the Ctrl+F precedent W6-1 D-2 used — and `fitGraph` proposes `Ctrl+Alt+0` (§7). The seven chordless rows stay chordless.
6. **The contracts document is `docs/plans/35_graph_contracts.md`.** The issue body's `33_graph_view_contracts.md` is taken (`33_upgrade_fence_contracts.md` exists; `34_canvas_contracts.md` is the canvas's). Contract numbering per §5.1; the citation census (`ContractsCitationCensus`) is extended per section as it was for canvas; the reconciliation generator (`scripts/canvas_reconciliation.py`) is generalised or twinned for the new document — PR H decides, the contracts doc records.
7. **The graph verbosity is the graph's own setting, persisted in the vault.** Mac's `GraphVerbosity` mirrors `CanvasVerbosity` but is a separate value "persisted alongside the other graph settings in `.slate/graph.json`" (`GraphAnnouncer.swift:7–10`) — not the device-local canvas preference. Windows follows: the level is read and written through core's config API (§2 row F) so both hosts read one vault file; the menu items are `CheckMenuItem`s (the m1 class, fixed by #1177) bound to the three levels.
8. **Coalescing classes exist on mac and are copied, not redesigned.** `GraphAnnouncer.swift:185–238`: a 200 ms class-keyed latest-wins debounce (navigation, filter), errors assertive and flushing, summaries and re-roots immediate, a fire-time gate on filter counts, and the settle announcement debounced after the layout converges. PR 0a pins the class keys in `a11y.rs`'s ONE list (the canvas precedent) so `the_mac_coalescing_switch_matches_the_pinned_class_list` and its Windows twin cover the graph too. `A11yResidueCensusTests.pinnedResidueSites` drops **29 → 28** when the marker at `GraphAnnouncer.swift:98` goes.
9. **Mac-side gaps found while reading (file upstream at close-out; not W6-2 scope unless a §2 row moves them).** `GraphNodeKey.make` (`GraphViewState.swift:58–73`) re-folds ghost keys in Swift with an `en_US_POSIX` workaround although core owns `ghost_key` (`graph.rs:43`) — §2 row C moves it. The filter count (`GraphFilterCount`) is composed at two Swift sites (`AppState+GraphTable.swift:225–235`, `GraphTableView.swift:340–342`) — 0a collapses them. The name filter is diacritic-insensitive on mac (`graphNameMatches`, `AppState+GraphConfig.swift:16–20`), which no other host reproduces byte for byte — §2 row E moves it to core with the divergence recorded (the canvas's row K precedent). The tier-B threshold (1,500) and the node-diameter formula live only in Swift — §2 row H.

---

## 1. Architecture on Windows — the shape every PR shares

```
apps/slate-windows/src/SlateWindows/Graph/
  GraphDocumentViewModel.cs    PR A   the ONE graph document (singleton, on the workspace): owns the snapshot,
                                      the generation probe, LoadState, the published table rows, GraphViewState;
                                      PanelWorkScheduler conventions (FFI off-dispatcher, generation-guarded publishes)
  GraphViewState.cs            PR A   { SelectedKey: string?, Filter, NameQuery, Groups, Mode } — the ONLY view state
                                      (P2-5): Table, Diagram and the Connections leaf read/write it, never copy
  GraphSurfaceView.xaml(.cs)   PR A   tab body: header (Table/Diagram switcher, filter field PR C, presets), load/empty/
                                      error states, the two projections (visibility-gated: exactly one in the UIA tree),
                                      the Where-am-I panel (PR C), the inspector (PR E)
  GraphTableView.cs            PR A   the textual projection: AccessibleDataGrid over core's rows (0b), nine columns,
                                      row actions from core's action set (0b)
  ConnectionsLeafView.cs       PR B   the right-pane Connections leaf: the neighbourhood tree from core (0b),
                                      depth 1–3, re-root + back stack, ghost → create note
  GraphNavigator.cs            PR C   the command layer (where-am-I, presets, mode switching, filter, zoom routing)
  GraphDiagramView.cs          PR D   the visual projection (custom FrameworkElement + per-node peers, windowed;
                                      tier B summary above core's threshold), the layout session driver
  GraphInspectorView.xaml      PR E   filters / groups / display / forces (P2-4), sliders that announce once settled
  GraphAnnouncer.cs            PR A   thin relay: canonical A11yEvents (PR 0a vocabulary), the pinned coalescing classes,
                                      AccessibilityNotificationDispatcher — NO text
  GraphPreferencesViewModel.cs PR C   verbosity + config, read/written through core's graph config API (0b)
```

Rules that bind every PR (each is a contract row in `docs/plans/35_graph_contracts.md`, §5.1):

- **R-A One query surface.** Every datum the surfaces show comes from `graph_snapshot`, `graph_neighborhood`, the layout session, or the 0b queries; the generation probe decides staleness; no surface caches a derived structure of its own. The only writes are the ghost's "Create note" through the shell's note-creation seam and `.slate/graph.json` through core's config API.
- **R-B One view state.** `GraphViewState` lives on the document (P2-5): the selected node key, the backend filter, the name query, the groups and the mode; the Connections leaf re-roots through it; arrows in the diagram move `SelectedKey`; the table's current row IS the selection.
- **R-C No host prose.** Every audible string is a canonical `A11yEvent` rendered by `a11y_render` (PR 0a). `GraphAnnouncer.cs` is a relay + coalescer; the announcement-seam census asserts zero `HostComposed` posts under `Graph/`. Static labels (column titles, the inspector's labels, the tier-B summary, the empty and loading states) are the mac label inventory verbatim, recorded in the contracts doc's §W-C label class.
- **R-D No re-derivation.** The connections tree and its reading order, neighbours and their "Connects to" content, the stable node key, the row action set, the name-filter predicate, group matching, the column model and every cell's text, the preset headline's ordering, the constants (tier boundary, node diameter, label cap, depth clamp), the spatial step, the ghost's note path — all consumed from core (PR 0b closes the gaps). The only geometry C# computes is viewport and screen transforms, the hit-test grid, and driving the layout session (tick/converge off-dispatcher) — pinned in the audit.
- **R-E Commands are table rows.** Every verb is a `ChordTable` row (section `CommandSection.Graph`, `ChordScope.Graph` for the five chorded rows), resolved in `SlateCommandRegistrar.BuildResolvers()`, palette-reachable (drift test 1), menu-backed where a menu item exists (drift test 2) with the accelerator from the table (drift test 3); the four routed chords are delivered by the focused surface, as the canvas's are.
- **R-F Modal admission.** Any sheet (the ghost-create confirmation, if the shell's seam prompts) is a `ModalSurface` member passing the #1118 chord admission; the inspector is a pane, not a modal.
- **R-G Announcements route through the dispatcher** at the event's priority; the coalescing classes are mac's, pinned in `a11y.rs`'s one list; the settle debounce and the filter-count fire-time gate are host timers with the constants recorded.
- **R-H Tokens only.** Node fills come from the eight-slot APCA-gated group palette and ring styles as the non-colour channel (P2-4, `GraphConfigTests` :157/:166), added to the theme dictionaries as graph keys; Contrast themes bind to system pairs (decision 11).
- **R-I "C# may contain" (decision 4) for graph:** view models, UIA peers, the view-state machine, viewport math, the hit-test grid, the layout-session driver and its cancellation, WPF marshalling of the position buffer, coalescing timers, the config file's I/O (schema and merge policy core's). Nothing else.

---

## 2. The §W-G canonical-consumption audit — seed register

What the mac graph derives in Swift, sorted by tier. **Tier 1 and 2 move to core (PR 0a/0b) with the mac consuming the new API in the same PR and the Swift derivation deleted** — decision 5's pattern, as W6-1 ran it. **Tier 3 is host-by-designation**, recorded so the audit is explicit. This table seeds the named `§W-G register` in `35_graph_contracts.md`; the close-out re-greps `apps/slate-windows/src/SlateWindows/Graph/` against it.

| # | Swift-derived pocket (file:line) | Core today | Tier | Target (PR) |
|---|---|---|---|---|
| A | **The entire announcer grammar**: `GraphVerbosity`, `GraphEvent`, the row copy and its terse collapse (`GraphAnnouncer.swift:158–170` — `GraphRow`), the re-root label (`:172–181` — `GraphReRooted`), the preset headlines (`AppState+GraphTable.swift:336–354` — `GraphPreset`), the filter count (two sites — `GraphFilterCount`), the forces phrases (`AppState+GraphConfig.swift:138–150` — `GraphForceValue`), the Where-am-I composition (`AppState+GraphDiagram.swift:252–267` — `GraphWhereAmI`), the filter phrase (`:359–365` — its clause), the tier-B entry and summary strings (`GraphDiagramView.swift:496–498, 628–637` — `GraphTierEntered`, `GraphTierSummary`), the "Connects to" custom content with its first-ten cap and overflow count (`:851–870` — `GraphNeighborsContent`), and the status literals (mode, zoom, fit, pinned, unpinned, settled, loading, errors) — 26 posting sites | two workspace events only (`GraphOpensSinglePane`, `ReopenedGraph`); the two `audio_summary` strings | 1 | `A11yEvent::Graph { event: GraphA11yEvent }` + `GraphVerbosity` (0a) |
| B | The Connections tree — undirected adjacency from the neighbourhood's edges, in/out split by centre incidence, self-edges dropped, Link+Embed merged with an embed-only flag, recursion with an ancestor cycle guard, per-occurrence path ids, `localizedStandardCompare` per level (`ConnectionsPanel.swift:381–490`) | `graph_neighborhood` returns flat nodes/edges | 2 | `graph_connections_tree(path, depth, filter) -> GraphConnectionsTree` — flat pre-order rows with the neighbourhood's counts (0b; the surface is `35_graph_contracts.md` 0b-2, which supersedes the seed names in this column — 0bD-12) |
| C | Cross-projection node identity — ghost-key re-folding with an `en_US_POSIX` workaround and percent-encoding (`GraphViewState.swift:58–73`) | `ghost_key`/`NodeKey` (`graph.rs:29, 43`) own the truth, not exposed | 2 | `GraphNode.stable_key` (0b) |
| D | Diagram neighbours and the "Connects to" content — the first-ten cap and the overflow count (`GraphDiagramView.swift:851–870, 1216–1223`) | `neighborhood_ids` internal | 2 | the topology entry's `neighbors` (`graph_topology(query, config) -> GraphTopology`, one record per rebuild) and `graph_neighbors(query, id) -> GraphNeighbors` for one node + the content as a 0a event over the FULL label list, the cap core's `GRAPH_NEIGHBOR_LABEL_CAP` (0b/0a; 0bD-12) |
| E | The name-filter predicate — `.caseInsensitive, .diacriticInsensitive` (`AppState+GraphConfig.swift:16–20`), called by both projections | none | 2 | `graph_visibility(query) -> GraphVisibility` over the one `GraphVisibilityQuery` (filter, needle, kind overlay; core's fold — the divergence recorded as 0b-D3), with `graph_label_matches` the free predicate (0b; 0bD-12) |
| F | `.slate/graph.json` schema, defaults, clamping, unknown-key preservation, refuse-clobber and no-downgrade generation rules, group first-match-wins and ring cycling (`GraphConfig.swift:34–45, 61`, `GraphConfigStore.swift`, `GraphConfigTests`) | none — the file is mac-only today | 2 (schema, merge policy) / 3 (I/O) | `graph_config_decode(json)` / `graph_config_encode(config, existing_json)` with core's schema, `graph_config_default`, the group rules (0b; 0bD-12); the writer's I/O host-designated |
| G | The table column model — order, `directionalComparator`, the `byLabel` total-order tie-break, kind labels, every cell's text (`GraphTableView.swift:527–636`); the preset headline reuses the comparator (`AppState+GraphTable.swift:346–352`) | rows from core, unformatted | 2 | `graph_table_rows(query, sort) -> GraphTableRows` with nine core-formatted cells per row, and `graph_table_columns() -> Vec<GraphTableColumnSpec>` as the ordered column model (0b; 0bD-12) |
| H | Constants with accessible meaning — tier boundary 1,500 (`GraphDiagramModel.swift:58`), node diameter `8 + 6·ln(1+in)` clamped 8–28 (`GraphDiagramView.swift:461–465`), label cap 200 by in-links (`:640–648`), depth clamp 1–3 (`AppState+Connections.swift:31–33`) | none | 2 | `graph_constants()` (0b) |
| I | The canonical row action set and availability per node kind (`GraphViewState.swift:15–48`) — the §P-B parity contract, in Swift only | none | 2 | `graph_row_actions(kind) -> Vec<GraphRowAction>` with labels (0b) |
| J | Spatial navigation scoring — cosine threshold 0.1, `dist/cosθ`, id tie-break; structural wrap; type-ahead (`GraphDiagramView.swift:1225–1272`) | none | 2 | `graph_spatial_step(points, neighbors, from, dx, dy) -> Option<id>` and `graph_structural_step(visible, from, forward)` (0b; 0bD-12); type-ahead a host list search over core's labels |
| K | Ghost → note path minting (`AppState+Connections.swift:303+`) | none | 2 | `graph_ghost_note_path(target) -> String` (0b) |
| L | Viewport math — zoom clamp and step, fit, centre-preserving scale (`AppState+GraphDiagram.swift:339–347`, reusing the canvas viewport) | none | 3 | host; the constants pinned to the canvas's (one viewport policy for both structural surfaces) |
| M | The uniform hit-test grid and tile layers (`GraphDiagramView.swift:582+, 738–757`) | none | 3 | host rendering |
| N | The coalescing state machine — classes, 200 ms window, the fire-time gate, the settle debounce (`GraphAnnouncer.swift:185–238`) | none | 3 (timing) / 1 (class keys) | host timers; the class keys in `a11y.rs`'s list (0a) |
| O | The singleton tab lifecycle and restore collapse (`WorkspaceStore.swift:370–376`) | none | 3 | already on Windows (W1-3) |
| P | Driving the layout session — tick cadence, convergence, cancellation, Reduce Motion (`AppState+GraphDiagram.swift`, `GraphDiagramView.swift`) | the engine and its determinism | 3 | host driver over core's session; §P-C's determinism pinned by a §W-A golden over the position buffer |

**Consequence for the series:** PR 0a blocks PR A (the table announces from day one). PR 0b blocks PR B onward (the leaf needs B/C/K; the table's columns need G; the diagram needs D/H/J; the inspector needs F). A may proceed while 0b is in review, on the flat snapshot, and re-points its column text at G's rows when 0b lands.

---

## 3. The PR series

```
PR 0a  core: graph announcer vocabulary → a11y.rs (+mac consumes)      ─┐
PR 0b  core: connections tree, stable key, neighbours, filter, config, ─┼─▶ PR A  document + tab + table (textual projection)
       columns, constants, actions, spatial step, ghost path (+mac)    │        ├▶ PR B  Connections leaf
                                                                       │        └▶ PR C  navigator: where-am-I, presets, filter, verbosity, config
                                                                       │              ├▶ PR D  diagram: renderer, peers, tiers, layout driver, zoom
                                                                       │              └▶ PR E  inspector: filters, groups, display, forces
                                                                       └──────────────────────────────────────▶ PR F  close-out (E2E, §K, §W-A/§W-C/§W-D, matrix, AT checklist, reconciliation)
```

| PR | Title | Gate | Size (mac analogue) | P issues covered |
|---|---|---|---|---|
| 0a | Graph announcer vocabulary → core | none (pre-A) | core ~300 LoC + mac migration (26 sites, 239-line announcer) + 3 census mirrors | P1-1 (#5xx announcer) |
| 0b | Graph structural queries, config schema, constants → core | none (∥ 0a) | core ~500 LoC + FFI + mac migration (`ConnectionsModel`, `GraphNodeKey`, `GraphTableColumn`, `GraphConfig`, spatial nav) | P0/P1/P2 leaks |
| A | Document, tab, the table projection | 0a | ~900 LoC (`AppState+GraphTable` 435 + `GraphTableView` on the substrate + the document) | P1-2 |
| B | The Connections leaf | A, 0b | ~500 LoC (`ConnectionsPanel` 493 + `AppState+Connections`) | P1-3 |
| C | Navigator, presets, filter, verbosity, config | A, 0b | ~450 LoC (routers, presets, filter field, config load/save) | P1-2/P1-3 commands |
| D | Diagram: renderer, peers, tiers, layout driver, zoom | C | ~1,400 LoC (`GraphDiagramView` 1,290 + model 123 + `AppState+GraphDiagram` 366) | P2-1..P2-3 |
| E | Inspector: filters, groups, display, forces | D | ~400 LoC (`GraphInspectorView` + `AppState+GraphConfig`) | P2-4 |
| F | Close-out | E | evidence + docs | P2-5 (+ W7-4 checklist rows) |

---

## 4. Per-PR specs

Each PR section lists: **Goal · Consumes · Builds · Behavior pinned · Tests · Evidence/acceptance · Hand-off.** "Contracts" means the numbered rows the PR adds to `35_graph_contracts.md` *before* its round 1 (§5.1).

### PR 0a — Graph announcer vocabulary → core (Rust + uniffi + mac + censuses)

**Goal.** Every graph announcement becomes a typed `A11yEvent` with its template, priority and verbosity policy in `crates/slate-core/src/a11y.rs`; mac's `GraphAnnouncer` shrinks to a relay + coalescer; the residue census drops 29 → 28 and `a11y.rs:14–38` is rewritten to name only the structural-mutation builder; `corpus.json` gains the graph family; §W-D becomes provable for the graph. **Copy rule: shipped mac strings move verbatim** — the P1/P2 normative copy is the reference, not a redesign.

**Consumes.** `GraphAnnouncer.swift` (`GraphEvent` cases, `rowPhrase` `:158–170`, `phrase` `:172–181`), the 26 posting sites (§0 item 3), `GraphAnnouncerTests.swift` (the copy matrix — becomes the Rust golden), `GraphCommandsTests.testPresetAnnouncementStringsAreVerbatim` (`:76`), `GraphConfigTests.testForcesChangePhraseNamesTheOneChangedControl` (`:97`).

**Builds.**
1. `GraphVerbosity { Terse, Standard, Verbose }` (core enum + uniffi mirror), a parameter on the events that vary by it, not global state — the canvas's shape.
2. `A11yEvent::Graph { event: GraphA11yEvent }` — one nested family under one top-level variant (the uniffi budget, `a11y.rs:1245–1252`); **the schema is `35_graph_contracts.md` 0a-2b**, literally — seventeen variants over eight nested a11y enums plus the reused `GraphNodeKind`; **no free-text variant**. The list an earlier draft of this item carried (a `GraphSummary{text}`, a separate `GraphLinkDistance`, `GraphZoom{context?}`, a standalone `GraphFilterPhrase`, mode statuses inside `GraphStatus`) is superseded by that table on 2026-09-03: the summaries are typed over exported count records, the four force controls are one `GraphForceValue{control, percent}`, zoom is `GraphZoom{fit, percent}`, the filter phrase is a clause of `GraphWhereAmI`, and the two modes are `GraphMode{mode: GraphSurfaceMode}`.
3. **Priorities and classes**: errors High and flushing; navigation (row focus) and filter counts coalesced 200 ms latest-wins; summaries, re-roots, presets immediate — the class keys added to `a11y.rs`'s "Coalescing class keys" list so both hosts' switch-list tests cover the graph.
4. **Five-place rule** (the W0.5-3 lesson — the canvas's 0a-2 counts the golden table as a place): `a11y.rs` variants + `tests/fixtures/a11y/corpus.json` (regenerated through the artifact test's own path) + the uniffi mirror (and the coverage test's nested-family inventory, `CANVAS_NESTED_ENUMS`'s twin for the graph) + the two census mirrors (`A11yCorpusCensusTests.swift`, `A11yCorpusCensus.cs`).
5. **Mac consumes:** `GraphAnnouncer.phrase`/`rowPhrase` and the prose sites are replaced by event construction + `a11yRender`; the two filter-count sites collapse to one event (`GraphFilterCount`, through the one gated entry); `GraphAnnouncerTests` copy assertions move to Rust (table-driven over verbosity × event); the Swift tests keep coalescing, flush, the fire-time gate and `testNoDirectAnnouncementsUnderGraph`; `pinnedResidueSites` 29 → 28.

**Behavior pinned.** Strings byte-identical to the shipped mac strings (the copy authority where the P specs differ, recorded per phrase); verbosity per P1 (terse collapse), a parameter on `GraphRow` only; Where-am-I always the full row copy — a recorded correction of mac's terse coupling; the summaries typed over the exported count records and rendered by the one formatter that fills `audio_summary`, Medium; coalescing stays host, class keys pinned in core's list.

**Tests.** Rust: the golden rows; the artifact round-trip; every variant and arm represented; the priority tier; the coalescing class list. Mac: the graph suites green unchanged; the residue count pinned at 28. Windows: `A11yCorpusCensus` mirror extended.

**Evidence.** Corpus diff reviewed string by string against `GraphAnnouncerTests`; both census mirrors green in CI; the trigger ledger's graph keys (PR F) derive from this family.

**Hand-off.** The event list and the relay convention, cited by PR A's `GraphAnnouncer.cs`.

### PR 0b — Graph structural queries, config schema, constants → core (Rust + uniffi + mac)

**Goal.** §2 rows B–K become core queries over the existing `GraphIndex`/metrics/layout: the Connections tree in reading order, the stable node key, neighbours, the name filter, the config schema and merge policy, the table rows with formatted cells, the constants, the action set, the spatial step, the ghost's note path. Mac consumes each and deletes its Swift copy.

**Consumes.** `graph.rs` (`GraphIndex`, `neighborhood_ids` `:604`, `ghost_key` `:43`), `graph_metrics.rs`, `graph_layout.rs`, `session.rs`'s graph surface (`:1840–2067`), the Swift sites named in §2 B–K, `ConnectionsPanelTests` (the in/out split and nesting cases become Rust goldens), `GraphTableViewTests` (sort determinism, total order), `GraphConfigTests` (round-trip, clamping, refuse-clobber, no downgrade), `GraphDiagramTests` (spatial move, tier boundary, filter equivalence).

**Builds.** The FFI entry points named in §2's Target column, each with a §W-A scenario: `graph_connections_tree`, `GraphNode.stable_key`, `graph_neighbors`, `graph_filter_labels`, `graph_config_read/write` (+ the schema, defaults, clamps, unknown-key preservation, generation rules), `graph_table_rows` (nine columns, core-formatted cells, `byLabel` total order), `graph_constants`, `graph_row_actions`, `graph_spatial_step`, `graph_ghost_note_path`. The parity harness's `graph_queries` scenario directory with goldens, run by both twins; the Windows `ParityHarnessCensus` twin extended so a graph fixture cannot be silently skipped (the canvas census's shape).

**Behavior pinned.** Each query's output equals the Swift derivation on the mac fixtures (the deleted Swift code's tests become the Rust goldens); determinism (§P-C, §P-D) holds through the binding; the diacritic divergence of row E recorded.

**Tests.** Rust unit + census per query; `graph_queries` goldens byte-identical on both lanes; mac suites green with the Swift copies gone.

**Evidence.** The §W-G register rows B–K read "moved", each with the deleted Swift range and the consuming mac site.

**Hand-off.** The FFI names, cited by A (columns), B (tree, ghost path), C (filter, config), D (neighbours, constants, spatial step), E (config).

### PR A — Graph document, tab wiring, the table projection

**Goal.** The placeholder becomes the graph: one `GraphDocumentViewModel` on the workspace behind the existing singleton tab, loading the snapshot off-dispatcher with generation-guarded publishes, and the **textual projection first** — the table on the W4-1 substrate with the nine columns, the preset-free default sort, the row actions, the summary region carrying core's `audio_summary`. Default landing = Table (P locked decision 1; §P-A).

**Consumes.** `graph_snapshot`, `graph_generation`; 0a's `GraphRow`, `GraphSnapshotSummary`, `GraphStatus`, `GraphBlocked`, `GraphMode`; 0b's `graph_table_rows`, `graph_row_actions`, `stable_key` (A lands on the flat snapshot if 0b is still in review and re-points at G when it merges). Windows seams: the singleton and its events (`WorkspaceViewModel.Layout.cs:178–192, 229–233, 424–428, 475`), the canvas's attach funnel as the template (`WorkspaceViewModel.cs:289–300`, `WorkspaceViewModel.Canvas.cs:40–80`), `AccessibleDataGrid.Bind` (`Grids/AccessibleDataGrid.cs:429–437`) and its `Announce` swap seam (`:64`), `ExternalSortHandler` (`:663`), `PanelWorkScheduler`, `AccessibilityNotificationDispatcher`.

**Builds.** `Graph/GraphDocumentViewModel.cs`, `GraphViewState.cs`, `GraphSurfaceView.xaml(.cs)` (header with the mode switcher — Diagram disabled until PR D — and the summary region; load/empty/error states), `GraphTableView.cs` (the grid configuration: `GraphTableGrid` automation id, the mac column titles verbatim, row header = the label column, the kind labels Note/Attachment/Unresolved, row actions Open / Open in New Tab / Show connections / Reveal in File Tree / Create note per core's action set, activation = Open), `GraphAnnouncer.cs` (relay + coalescer over the pinned classes). `ChordTable` rows: `slate.graph.openTab` (chordless, section Graph) and `ChordScope.Graph`; the placeholder text retired for Graph.

**Behavior pinned.** 1. One document per workspace (the singleton), attached at every open/reopen/restore site, released when the tab closes; the generation probe refreshes on vault change (the W4-7 reveal discipline). 2. Load states: loading (status), ready, empty (the empty vault's summary is `GraphSnapshotSummary` over zero counts; a preset's empty case is PR C's), error (`GraphBlocked`). 3. The table: nine columns in mac's order with core's cell text; default sort by links in descending then label; sort announced by the substrate; the row's Name is the P1 row copy (`GraphRow` at the set verbosity), ItemStatus carries the kind badge. 4. Selection: the current row writes `GraphViewState.SelectedKey`; the summary region reads `audio_summary` verbatim. 5. Activation: Open lands the note in the current tab; ghost rows offer Create note through the shell's seam. 6. Persistence: the singleton restore collapse as today (`W1WorkspaceRedTeamTests`). 7. The announcement on open is `GraphStatus{Opened}` then `GraphSnapshotSummary`, mac's order.

**Tests.** `GraphDocumentTests` (registry, load states, generation refresh, selection, activation), `GraphTableTests` (columns, sort determinism through the substrate, row actions, ghost availability), the announcement-seam census over `Graph/`, `GraphTabRoutingTests`' Windows twins added to `W1WorkspaceRedTeamTests`; FlaUI `GraphSurfaces_TableSortSelectionAndActivation_AreClean` (open the graph tab, walk the grid by UIA, assert row names verbatim, sort, activate, axe `graph-table`). **§W-A:** the table rows and the summary over the shared corpus byte-identical (0b's `graph_queries`). **§K:** `GraphOpenBenchmarks` — snapshot marshalling on the 1k/10k fixtures against P's budgets.

**Evidence / acceptance.** A JAWS/NVDA user opens the graph, hears `GraphStatus{Opened}` and the vault's `GraphSnapshotSummary`, walks a sortable table whose rows read as P1's copy, and opens a note from it. Matrix rows: `slate.graph.openTab` ✓; the surface row stays pending until F; `w_c_matrix.md` gains "Graph table (W6-2 PR A)".

**Hand-off.** The document, the view state and the announcer, cited by B–E.

### PR B — The Connections leaf

**Goal.** The right pane's `connections` leaf (already in the catalogue, `WorkspaceViewModel.cs:1682`) becomes the local graph: core's connections tree at depth 1–3 for the active note, re-root on activation with a back stack, the neighbourhood summary relayed on depth change and re-root, ghost rows that create the note.

**Consumes.** 0b's `graph_connections_tree` (the tree with its summary counts — one record per load), `graph_ghost_note_path`, `graph_constants` (the depth clamp); 0a's `GraphReRooted`, `GraphNeighborhoodSummary`, `GraphRow`, `GraphStatus`, `GraphBlocked`; the leaf host (`RightPanePanelsViewModel.cs`) and the note-creation seam.

**Builds.** `Graph/ConnectionsLeafView.cs` (a tree peer: the in/out groups as headers, rows named by `GraphRow`, embed-only and unresolved badges as ItemStatus, depth control "Local graph depth" with mac's hint, the loading/no-connections/error copy verbatim), `ChordTable` rows `slate.graph.showConnections`, `connectionsDeeper`, `connectionsShallower` (chordless), the leaf's registration and focus route.

**Behavior pinned.** The tree's order is core's (row B); self-edges omitted, Link+Embed collapsed (the `ConnectionsPanelTests` cases as Windows facts over the same fixtures); depth clamped 1–3 by core's constant; re-root announces `GraphReRooted` then `GraphNeighborhoodSummary`; Back returns to the prior root; a ghost's Create note lands the note and re-roots; the leaf re-roots when the active note changes unless pinned by the user's re-root (mac's rule, pinned).

**Tests.** `ConnectionsLeafTests` (tree shape, depth, re-root/back, ghost create, active-note follow), FlaUI `GraphConnections_LeafWalkDepthAndReRoot_AreClean` (axe `graph-connections`). **§W-A:** the tree over the corpus byte-identical.

**Evidence / acceptance.** From a note, the user opens Connections, hears the neighbourhood summary, walks in/out links two hops deep, re-roots on a neighbour and comes back. Matrix rows: the three connections ids ✓; `w_c_matrix.md` "Graph connections leaf (W6-2 PR B)".

**Hand-off.** The leaf's re-root seam, read by D's diagram selection sync.

### PR C — Navigator, presets, filter, verbosity, config

**Goal.** The command layer: Where-am-I (panel + announcement), the three presets, the name filter field and its count, the mode switcher's commands, the verbosity menu (three `CheckMenuItem`s) and the config load/save through core.

**Consumes.** 0a's `GraphWhereAmI`, `GraphPreset`, `GraphFilterCount`, `GraphMode`; 0b's `graph_table_rows` over the one `GraphVisibilityQuery` (the count is the rows result's), `graph_visibility`, `graph_config_decode/encode` (0bD-12); the canvas navigator's shape (`Canvas/CanvasNavigator.cs`, the Where-am-I panel and filter field in `CanvasSurfaceView.cs`, `CanvasFilterMachine.cs`, `CanvasPreferencesViewModel.cs`).

**Builds.** `Graph/GraphNavigator.cs`, the filter field (`GraphFilterField`, its summary and Clear), the Where-am-I panel (`GraphWhereAmIPanel`), `GraphPreferencesViewModel.cs`, `ChordTable` rows `slate.graph.orphans`, `unresolved`, `mostLinked` (chordless) and `slate.graph.whereAmI` (`Ctrl+Alt+Shift+I`, `ChordScope.Graph`); the Verbosity submenu.

**Behavior pinned.** Presets are parameterisations of the table (p1 §90): each sets the backend filter and sort and speaks its headline (`GraphPreset`), which SUPERSEDES the generic summary for that load — mac's rule (`AppState+GraphTable.swift:195–199`), pinned; the filter count (`GraphFilterCount`) is coalesced with the fire-time gate; Where-am-I (`GraphWhereAmI`) reads the full row copy, the component, the zoom (when the diagram shows) and the active filters — always the full copy, the 0a-D1 correction — and renders in the panel; the verbosity level persists in `.slate/graph.json` through core and is read at every announce; the Escape ladder (panel → filter → surface) as the canvas's.

**Tests.** `GraphNavigatorTests` (presets, filter machine, Where-am-I readback equals the spoken string, verbosity persistence through the store with the m1 re-selection fact), `GraphConfigTests`' Windows twins; FlaUI `GraphSurfaces_NavigatorFilterAndWhereAmI_AreClean` (axe `graph-navigator`).

**Evidence / acceptance.** The user filters by name, hears the count, jumps to orphans, asks Where am I and reads the panel. Matrix rows: the four ids ✓; `w_c_matrix.md` "Graph navigator, filter and Where-am-I (W6-2 PR C)".

**Hand-off.** The navigator's routing seam for zoom and Where-am-I, taken by D.

### PR D — Diagram: renderer, per-node peers, tiers, the layout driver, zoom

**Goal.** The second projection over the same model: a custom FrameworkElement drawing core's positions, per-node automation peers (Button, Invoke selects, Name = the row copy, HelpText = the "Connects to" content) windowed to the viewport, tier B above core's threshold (one summary element routing to Table), the layout session driven off-dispatcher with the settle announcement, spatial and structural keyboard navigation from core's step, pin/unpin, zoom in/out/actual/fit with the canvas's viewport policy, Reduce Motion.

**Consumes.** `start_graph_layout` and the session API, 0b's `graph_topology` (one record per rebuild: the visible nodes with their neighbours, diameters, groups and label slots), `graph_constants`, `graph_spatial_step`, `graph_structural_step` (0bD-12); 0a's `GraphRow`, `GraphMode`, `GraphZoom`, `GraphPinned`, `GraphLayoutSettled`, `GraphTierEntered`, `GraphTierSummary`, `GraphNeighborsContent`; the canvas renderer's machinery (`CanvasRendererView.cs`, `CanvasRendererPeers.cs`, `CanvasPeerTopology.cs`, `CanvasViewportState.cs`, `CanvasTextScaleService.cs`).

**Builds.** `Graph/GraphDiagramView.cs`, `GraphDiagramPeers.cs`, the layout driver (`GraphLayoutDriver.cs`: tick cadence, run-to-convergence with cancellation, generation refresh, Reduce Motion = converge before first paint), `ChordTable` rows `zoomIn` (`Ctrl+=`), `zoomOut` (`Ctrl+-`), `actualSize` (`Ctrl+0`), `fitGraph` (`Ctrl+Alt+0`) in `ChordScope.Graph`; the theme keys for node fills and rings; text scaling as the canvas's.

**Behavior pinned.** One model, two projections: the diagram's node set, labels, selection and filter are the table's (the drift test enumerates diagram actions against the table's, §P-B); positions are core's (the §W-A golden over the quantised position buffer, §P-C); tier A per-node peers up to the threshold, tier B one summary element and the entry announcement; arrows = core's spatial step, Tab = structural order, type-ahead by label; zoom announced as `GraphZoom`; fit and actual size; pin/unpin announced; the settle announcement once, debounced; the diameter and label cap from core's constants.

**Tests.** `GraphDiagramTests` (the mac suite's 35 cases as Windows facts where they concern behaviour), `GraphLayoutDriverTests` (determinism through the driver, cancellation, refresh), FlaUI `GraphSurfaces_DiagramPeersTiersAndZoom_AreClean` (axe `graph-diagram`). **§W-A:** the position golden. **§K:** `GraphRendererBenchmarks` — warm tick, first windowed rebuild, per-pan hop, spatial step against P's budgets.

**Evidence / acceptance.** The user switches to Diagram, hears `GraphMode{Diagram}`, moves node to node with arrows hearing each row's copy, zooms and fits, and on a 2,000-node vault hears the tier-B summary and is routed to Table. Matrix rows: the four zoom ids ✓; `w_c_matrix.md` "Graph diagram (W6-2 PR D)".

**Hand-off.** The diagram's selection and viewport seams, read by E's display and forces.

### PR E — Inspector: filters, groups, display, forces

**Goal.** P2-4's inspector as a pane: the backend filters (attachments, ghosts, orphans only), groups by query with the eight-slot palette and ring styles, display toggles, and the force sliders that announce once settled; all persisted through core's config with mac's generation rules.

**Consumes.** 0b's `graph_config_decode/encode`, `graph_config_matching_group` and `graph_config_next_group_style` (0bD-12); 0a's `GraphForceValue` (all four controls, `LinkDistance` among them), `GraphLayoutSettled`; the session's `set_forces`; the canvas prompt/sheet machinery for any picker.

**Builds.** `Graph/GraphInspectorView.xaml(.cs)` (peered controls: check boxes, a groups list with add/remove, colour + ring pickers, four sliders with Value patterns), the config writer (debounced, single-writer, refuse-clobber), theme keys.

**Behavior pinned.** Filter equivalence: table and diagram read one predicate (core's); groups first-match-wins with the ring as the non-colour channel; sliders announce the resulting condition as TWO events in mac's order — `GraphForceValue` coalesced to the resting value, then `GraphLayoutSettled` once the layout converges (contracts doc 0a-16, 0a-D5; P2's single sentence is recorded, not adopted); the config file round-trips with unknown keys preserved and never downgrades; a superseded generation is refused.

**Tests.** `GraphInspectorTests`, `GraphConfigStoreTests` (round-trip, clobber, downgrade, superseded generation), FlaUI `GraphInspector_FiltersGroupsAndForces_AreClean` (axe `graph-inspector`).

**Evidence / acceptance.** The user hides attachments, adds a group "project" in blue with a dashed ring, drags the repel slider and hears the layout settle. Matrix: no new command rows (the inspector is a pane); `w_c_matrix.md` "Graph inspector (W6-2 PR E)".

**Hand-off.** None; F closes.

### PR F — Close-out (issue-level reconciliation)

**Goal.** The P analogue of #365 + the W evidence: prove the whole promise end to end on Windows and record every gate, in the W6-1 PR H form.

**Builds / records.**
1. **E2E suite** (`GraphEndToEndTests`, through the real `VaultSession` and the document, not mocks): open the graph on the shared corpus → table rows and summary verbatim → sort → preset → filter → Connections leaf walk and re-root → diagram: layout to convergence, spatial steps, zoom, Where-am-I → the config round-trip; plus the large fixture under P's budgets.
2. **§K** — `BENCHMARKS.md` "Milestone W6-2 — graph through the C# binding": snapshot marshalling (A), warm tick / first windowed rebuild / per-pan hop / spatial step (D) with budgets asserted.
3. **§W-A** — `graph_queries` scenarios and the position golden byte-identical on both twins; the parity-harness censuses extended.
4. **§W-D** — the corpus covers the graph family on both twins; the trigger-parity table (the canvas ledger's generator generalised or twinned) recorded; the residue count 29 → 28 recorded.
5. **§W-C** — `w_c_matrix.md` rows per projection + leaf + inspector; axe 0 failures across the graph journeys; the matrix census extended.
6. **§W-G** — the register closed by re-grep of `apps/slate-windows/src/SlateWindows/Graph/` against §2.
7. **Matrix** — `scripts/generate-parity-matrix.py` re-run; all twelve `slate.graph.*` rows implemented; the surface row derived from `#746`'s aggregate evidence (`expected_issues` + `issue_delivery_status` + the f-string row, as #745's); `chords.json` `graph*` groups and the `graph` aggregate (validation 14).
8. **AT checklist** — `reports/w6_2_graph_at_checklist.md` in the W7-4 form: table walk (names, sort), leaf walk, diagram traversal (no dead end, tier B), arrow-only, keyboard-only, Voice Access (D-7's twin), switch access, braille, text scaling, Contrast, Reduce Motion; every human cell Pending.
9. **Issue reconciliation** — the final section of `35_graph_contracts.md`: the PR ledger by ancestry, contract → evidence generated, the registers, the decisions, the upstream issues filed, the residuals; `w6_spec.md` §W6-2's items as checkbox rows; the help-doc chord column hand-off to W8-6 (#756).

**Acceptance (issue close).** PRs 0a–E merged; the E2E suite green on CI; all gates recorded; the checkbox rows ticked with dates; the human AT checklist executed or listed as the release residual with the owner's sign-off.

---

## 5. Cross-cutting process and gates

**Copy authority (0aD-8, the round-3 design pass).** This spec names EVENTS and never quotes an announcement TEMPLATE. Every template lives in exactly two places — the schema table of `35_graph_contracts.md` (0a-2b) and the Rust golden `corpus_renders_the_shipped_strings` — and this document refers to `GraphStatus{Opened}`, `GraphMode{Diagram}`, and so on, in §0's facts and §2's register as much as in §4. A control's NAME may be quoted where it names the control (the "Connects to" content, the "Local graph depth" control): that is the label inventory's business (§W-C), not a template. A quoted template anywhere in this document is a defect.

### 5.1 The contracts document (precondition 0 of the red-team protocol)

`docs/plans/35_graph_contracts.md` — **one** document for the issue (PR = section), landed as its own commit **before** each PR's round 1, per [`24_red_team_protocol.md`](../../24_red_team_protocol.md). Contract ids are prefixed by PR letter (`0a-1…`, `A1…`); decisions, divergences and risks per section as §H's (`HD-`, `HD-D`, `HR-`) so the reconciliation's key grammar carries over. Named registers: **§W-G canonical-consumption audit** (seeded from §2), **recorded divergences** (Windows vs mac), **accepted risks**, **mac details recorded while reading**, **owner decisions** (§6), **verified during implementation**. The citation census registers each section with a floor below its population; the reconciliation generator is pointed at the new document by PR F.

### 5.2 Definition of done per PR (in addition to the wave DoD)

- `cargo fmt --check` + clippy (0a/0b); `dotnet format apps/slate-windows/SlateWindows.slnx` after **every** edit batch under `apps/slate-windows`; a core change needs `apps/slate-windows/generate-bindings.ps1` before any Windows test sees it (the bindings are git-ignored); every test step on the build's own "Build succeeded" line — a `--no-build` run after a failed build reports the stale binary green.
- Unit suites + censuses local; the FlaUI journey suite is **CI's shell gate's** to arbitrate (local FlaUI as a serialized smoke, never concurrent with the unit suite, no screen reader running); a shell-wide behavioural fix re-runs the **whole** journey suite.
- A lane skipped behind a failing upstream lane hides its assertions — read every lane's log, match **failures**, not test names.
- Red-team rounds per the protocol (invariant-targeted prompts citing contract ids with the accepted-risk register inline; `xhigh`; stop rules 4/5; the standing freeze precedent applied and recorded "owner may overrule"); the codex post-implementation pass over the diff before the PR opens; the PR; CI green on both twins on the final head; codoki's approval with "auto-approved (no issues found)" on the exact head, findings addressed or refuted; merge.
- Pre-review self-QA: journeys assert **rendered content and real input paths**; mutation verification re-runs the byte-restored mutations; the process record per task states what ran.

### 5.3 Evidence ledger (what each gate means for graph)

| Gate | Graph artifact | Lands in |
|---|---|---|
| §W-A | `graph_queries` scenarios (0b), the table rows and summary (A), the connections tree (B), the position golden (D) — byte-identical cross-platform + goldens | 0b, A, B, D; closed F |
| §W-C | per-projection FlaUI journeys + axe; `w_c_matrix.md` rows | A, B, C, D, E; closed F |
| §W-D | graph family in `corpus.json` + both census mirrors; the trigger-parity table | 0a; closed F |
| §W-G | audit register seeded (§2), closed by re-grep | 0a/0b; closed F |
| §K | snapshot marshalling (A) + layout driver and renderer (D) with P's budgets asserted | A, D; recorded F |
| Matrix | 12 command rows + the `connections` leaf row + the surface row + `chords.json` delivery evidence | per PR; regenerated F |
| Human AT | `reports/w6_2_graph_at_checklist.md` | F (release residual) |

### 5.4 Branching and review

One branch per PR (`feat/w6-2-<slice>`), stacked on the previous slice's merged `main`; commit the contracts section first; push before round 1; record the lessons as they recur (the W6-1 close-out's process record is the checklist).

---

## 6. Owner decisions required (record the answer in the contracts doc; none blocks PR 0a's start)

| # | Decision | Recommendation |
|---|---|---|
| D-1 | Accept the §2 tiering (Tier 1+2 move to core with mac consumption; Tier 3 host-by-designation) | Accept as listed; any demotion of a Tier-2 row is a recorded divergence naming the duplicated rule |
| D-2 | The five chorded rows: `whereAmI`, `zoomIn`, `zoomOut`, `actualSize` at the canvas's Windows chords in `ChordScope.Graph` (disjoint delivery sites, the Ctrl+F precedent); `fitGraph` ⌥⌘0 → `Ctrl+Alt+0` | Accept; the table PR verifies `Ctrl+Alt+0` is free in every scope |
| D-3 | `.slate/graph.json` — schema, defaults, clamps, unknown-key preservation and the generation rules move to core so both hosts read one vault file; the writer's I/O stays host | Accept; mac's `GraphConfigStore` becomes a consumer in 0b |
| D-4 | The name filter's diacritic insensitivity (mac) versus Unicode simple case-fold (core) | Core's fold on both hosts; the divergence recorded (the canvas's row K precedent) |
| D-5 | The tier-B threshold (1,500), node diameter formula, label cap and depth clamp become core constants | Accept (`graph_constants`) |
| D-6 | The graph's verbosity: mac declares a `GraphVerbosity` but never sets or persists it (no switch, no config field — P2-4's intention never landed); the canvas's is device-local | Its own `GraphVerbosity` (0a); 0b adds the `verbosity` key to `.slate/graph.json`'s schema (mac's reader preserves unknown keys); PR C's menu is a separate submenu; the mac gap is filed |
| D-7 | Voice Control twin for the AT checklist | Windows Voice Access ("show numbers"); Narrator smoke only (as W6-1) |
| D-8 | The Connections leaf's follow rule — re-root on active-note change unless the user re-rooted (mac) | Follow mac, pinned by a fact |

**Upstream issues to file (not W6-2 scope):** none known beyond the §0 item 9 duplications, which 0a/0b delete.

---

## 7. Chord mapping — proposed Windows chords (the table PR per slice adjudicates; rule: ⌘→Ctrl, ⌥→Alt, ⌃⌘→Ctrl+Alt, disambiguate with Shift per G18)

| Command | mac | Windows (proposed) | Scope | Lands | Note |
|---|---|---|---|---|---|
| `slate.graph.openTab` | — | — | — | A | palette and menu only |
| `slate.graph.showConnections` | — | — | — | B | palette and menu only |
| `slate.graph.connectionsDeeper` | — | — | — | B | palette only |
| `slate.graph.connectionsShallower` | — | — | — | B | palette only |
| `slate.graph.orphans` | — | — | — | C | palette and menu |
| `slate.graph.unresolved` | — | — | — | C | palette and menu |
| `slate.graph.mostLinked` | — | — | — | C | palette and menu |
| `slate.graph.whereAmI` | ⌃⌘I | **Ctrl+Alt+Shift+I** | Graph | C | the canvas's chord in a disjoint scope (D-2) |
| `slate.graph.zoomIn` | ⌘= | Ctrl+= | Graph | D | as the canvas's |
| `slate.graph.zoomOut` | ⌘- | Ctrl+- | Graph | D | as the canvas's |
| `slate.graph.actualSize` | ⌘0 | Ctrl+0 | Graph | D | as the canvas's |
| `slate.graph.fitGraph` | ⌥⌘0 | **Ctrl+Alt+0** | Graph | D | the one graph-owned chord; verify free |
