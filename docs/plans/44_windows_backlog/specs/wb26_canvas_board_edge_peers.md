# WB-26 — Labelled canvas edges are UIA peers on the visual board: named by core's traversal phrase, windowed like cards, Invoke total over the end-style table

Issues: Closes #1277. Size L (design first). Lanes: windows + FlaUI (shell gate); rust + swift (compile) only if design choice D-A below adds a core field. AT: NVDA (full pass). Wave: W9. Depends on: WB-13 (last on `Canvas/CanvasRendererView.cs` and `CanvasOutlineView.cs`, after plan 43 PR 32, §6). Parallel with: WB-25, WB-27. Decision: none in the plan; the design section below carries two choices for the reviewer.

## Goal

Locked contract 34 D5 says a labelled edge is a peer in the board's name
namespace, named by core's rendered traversal phrase, with an Invoke whose
target is total over the end-style pair; D3 says edges window like cards
through the same three cells. On `main` the peer topology materializes
edge placements, but `MaterializedPeers` enumerates card keys only and
`PeerFor` returns null for any edge key, so a screen-reader user
enumerating `CanvasVisualBoard` finds cards and never a connection. After
this chunk a labelled edge is a live peer: discoverable by name,
placeholder off-window, realizable, and invokable to the endpoint D5's
table names — with live UIA-tree facts rather than topology alone.

## Current state on main

Verified in the worktree. The issue's lines have drifted: `MaterializedPeers`
is `Canvas/CanvasRendererView.cs:541-561` (issue: 461–479), `PeerFor`
`:568-595` with the `key.IsEdge` rejection at `:576` (issue: 485–494).

- `CanvasRendererView.cs:525-527` `_peers`: `Dictionary<CanvasPeerKey,
  WeakReference<CanvasCardAutomationPeer>>` (typed to the card peer);
  `:535-536` `OnCreateAutomationPeer` → `CanvasRendererAutomationPeer`;
  `:541-561` `MaterializedPeers` iterates `population.SceneNodes` and
  `CanvasPeerKey.Card(node.NodeId)` only, adding a peer when the placement
  cell is `Materialized`; `:568-595` `PeerFor` mints `CanvasCardAutomationPeer`
  and returns null for `key.IsEdge` or an unknown node.
- `Canvas/CanvasRendererPeers.cs:20-24` `CanvasRendererAutomationPeer :
  IValueProvider, ISelectionProvider, IItemContainerProvider`; `:39`
  name `CanvasPhrase.VisualBoardName`; `:55-57` `GetChildrenCore` →
  `_view.MaterializedPeers()`. `:141-145` `CanvasCardAutomationPeer :
  IInvokeProvider, IVirtualizedItemProvider, …`; `:168` name =
  `Node?.SpeakableName`; `:192-205` `GetPattern` (Invoke, SelectionItem);
  `:210` `Invoke() => _view.SelectAnnounced(Key.Id)`; `:247` `Realize()
  => _view.Realize(Key)`.
- `Canvas/CanvasPeerTopology.cs:16-20` `CanvasPeerKey(bool IsEdge, string
  Id)`, `Card`, `Edge`; `:160-182` edge placements: the union of both
  endpoints' rects, `Materialized` when a transient drag follows or the
  rect intersects the window (`:176-180`); retained keys become
  placeholders (`:183-`).
- `CanvasPeerTopologyTests.cs:60-77` a crossing labelled edge's placement
  materializes ("the peer exists" — a placement, not a peer); `:80`
  `AnUnlabelledEdgeIsNeverAPeer` (DD-2).
- Edge data: binding `CanvasSceneEdge { edge_id, from_node, from_side,
  to_node, to_side, from_arrow, to_arrow, label, color }`
  (`lib.rs:13272-13282`); `CanvasDocumentViewModel.SceneEdgeFor(edgeId)`
  (`:4379-4380`); the direction rule `direction_from(is_from, from_end,
  to_end)` (`canvas/model.rs:623-624`).
- Core's traversal phrase: `CanvasA11yEvent::CanvasConnectionTraversed {
  direction, kind_label, title, label }` (`a11y.rs:1548-1553`), rendered
  `:2717-2731` ("Connects to" / "Connected from" / "Linked with" + the
  card reference, `, labelled "…"`). Windows already renders it without
  posting for outline connection rows:
  `CanvasDocumentViewModel.ConnectionRowName(neighbor)` (`:4371-4377`,
  `CanvasAnnouncer.RenderLabel`).
- FlaUI `ShellAccessibilityTests.cs:9316`
  `CanvasSurfaces_VisualBoardPeersAndZoom_AreClean` counts `Button`
  children of the board only (`:9375-9380`).
- Registers: `w_c_matrix.md:53` (Canvas visual row: "one Button peer per
  MATERIALIZED card"); censuses `CanvasAutomationPropertyCensus`,
  `CanvasLabelClassCensus`, `CanvasTriggerParityCensus`.

## Reference

Contract 34 (`34_canvas_contracts.md`): D3 `:6390-6426` (three cells,
discriminated identity "(renderer instance, document, EDGE id) for
labelled edges", descriptor index, placeholder = virtualized-item pattern
always, "EDGES window like cards … a labelled edge materializes when
either endpoint is in the window, is a placeholder when retained
off-window"); D4 `:6427-6446` (never-dead-end: realization pans to
contain the target); D5 `:6447-6470` ("Name uniqueness is GLOBAL across
the container … An EDGE'S name is core's rendered traversal phrase — the
same sentence the connection-walk announces, INCLUDING the other endpoint
… resolved by the SKIP-OCCUPIED loop core's own naming uses … The edge
peer's INVOKE target is total over the shipped edge algebra", the table
at `:6465-6470`: (false,true) → to-node; (true,false) → from-node;
(false,false) → to-node; (true,true) → to-node). DD-2: an unlabelled edge
is never a peer.

## Design section to review before code

Two choices need the reviewer's answer before implementation starts; the
rest of the design follows from them.

**D-A — Where the edge's name is composed and made unique.** D5 wants
core's traversal phrase "including the other endpoint", in one global
namespace resolved by core's skip-occupied loop. On the board there is no
"current card", so the phrase needs both endpoints, and the only core
renderer today (`CanvasConnectionTraversed`) names one card relative to
another.
- *Recommended:* core owns it. `CanvasSceneEdge` gains `speakable_name:
  String` (new record field), allocated by the population's naming pass
  in the same skip-occupied loop as cards (`core/canvas` where
  `speakable_name` is assigned), rendered as "{from ref} connects to {to
  ref}, labelled "{label}"" / "connected from" / "linked with" by the
  `EdgeDirection` rule — one grammar, one namespace, no host composition.
  Cost: an FFI record change (both bindings regenerated), plan §4 rule 6 —
  every Swift test literal of `CanvasSceneEdge` gains the field (grep
  `CanvasSceneEdge(` under `apps/slate-mac/Tests` before coding and name
  the files in the PR body); the Swift lane compiles it; rust lane runs.
- *Alternative:* a core free function `canvas_edge_name(from_kind,
  from_title, direction, to_kind, to_title, label) -> String` (new, no
  record change, no Swift edit), with the host running the skip-occupied
  ordinal loop over cards' `SpeakableName`s plus edge names at install.
  Cheaper, but the uniqueness loop lives in the host — D5 says "core's
  own naming uses", so this needs the reviewer to accept a host loop that
  mirrors it, pinned by a fact against core's card names.

**D-B — The edge peer's control type and patterns.** Cards are `Button`
with Invoke + SelectionItem. Recommended for edges:
`AutomationControlType.Custom` with `LocalizedControlType` "connection",
Invoke (D5's table), VirtualizedItem (D3's placeholder cell), no
SelectionItem (an edge is not selectable on the board today — the
`CanvasSelection` is a node seat). Alternative: `Hyperlink` (a reader
hears "link", which misnames it). The reviewer picks; the FlaUI journey
and `w_c_matrix.md` row follow.

The rest, given D-A recommended and D-B recommended:

- **Registry.** `_peers` retypes to an abstract
  `CanvasBoardAutomationPeer` (base of `CanvasCardAutomationPeer` and the
  new `CanvasEdgeAutomationPeer`); `PeerFor` mints an edge peer when
  `key.IsEdge` and `population.SceneEdges` holds the id and the edge has a
  label (DD-2); the sweep (`:584-594`) is unchanged.
- **Enumeration.** `MaterializedPeers` adds, after the cards, one edge
  peer per labelled `SceneEdges` entry whose placement cell is
  `Materialized` — document order (cards, then edges), stable per
  install, so `IItemContainerProvider.FindItemByProperty(Name)` resolves
  edges as it does cards.
- **`CanvasEdgeAutomationPeer`** (`CanvasRendererPeers.cs`, beside the
  card peer): `GetNameCore` = the edge's `SpeakableName`; bounding
  rectangle = the placement rect transformed to screen on every pan,
  zoom and resize (as the card peer does); `IVirtualizedItemProvider.Realize`
  → `_view.Realize(Key)`, which for an edge pans to contain the union rect
  (D4) and installs the next window; `IInvokeProvider.Invoke` →
  `_view.SelectAnnounced(target)` where `target` is the D5 table over
  `(FromArrow, ToArrow)`: `(false,true)` → `ToNode`, `(true,false)` →
  `FromNode`, else `ToNode`. Placeholder cell: identifying properties from
  the descriptor index, action patterns refused until realization
  (`ElementNotAvailableException`, the card's rule).
- **Descriptor index.** The installed state's descriptor index (D3) gains
  edge descriptors (id, name, endpoints, rect) so a first-touch
  find-by-name answers without mutation.
- **Children-invalidated.** One `StructureChanged` per install, as today
  (`CanvasRendererView.RefreshAutomationChildren`, `:181-196`).
- Not changed: `CanvasPeerTopology`'s placement rules (already right),
  the outline's connection rows, the navigator's edge traversal, the
  card peer, unlabelled edges (never peers).

## Tests

Facts fail first; hosted STA facts in a new `CanvasRendererPeerTests`
(beside `CanvasPeerTopologyTests`), plus one Rust fact under D-A.

- Rust `canvas::tests::edge_speakable_names_join_the_card_namespace`
  (D-A recommended): two same-label edges from one card to two
  destinations differ by name; an edge whose phrase collides with a card
  title takes the next free ordinal. Mutation: skip edges in the loop →
  fails.
- `CanvasRendererPeerTests.ALabelledEdgeInTheWindowIsAChildOfTheBoard` —
  a canvas with a visible "supports" edge: the renderer peer's children
  include an edge peer named by core's phrase; an unlabelled edge yields
  none (DD-2). Mutation: restore the `IsEdge` null at `:576` → fails.
- `…AnEdgeIsAPlaceholderOffWindowAndPromotesOnRealize` — far endpoints:
  the retained edge peer reports VirtualizedItem, refuses Invoke, and
  after `Realize()` the viewport contains the union rect and Invoke works
  (D3/D4).
- `…InvokeSelectsTheEndpointTheEndStyleTableNames` — a `[Theory]` over
  the four `(fromArrow, toArrow)` cells asserting the selected node and
  one announcement. Mutation: swap a cell → that row fails.
- `…TheSameEdgePeerIsReturnedWhileAnythingHoldsIt` — identity-stable
  per key within a renderer; a second pane's renderer holds its own.
- `…FindItemByNameResolvesAnEdge` — `IItemContainerProvider` finds the
  edge by its name from the descriptor index without materializing it.
- `…AStructureChangeRaisesChildrenInvalidatedOncePerInstall` — adding an
  edge: one `StructureChanged`.
- `CanvasPeerTopologyTests.cs:60-77`: its comment "the peer exists"
  becomes true — reference the new fact.
- FlaUI: extend `CanvasSurfaces_VisualBoardPeersAndZoom_AreClean`
  (`:9316`): the board's children include a custom element whose name is
  the edge phrase; Invoke on it selects and announces the to-node.
- Censuses: `CanvasAutomationPropertyCensus` (the new element's
  properties), `CanvasLabelClassCensus` (the name is core's, not a host
  literal), `CanvasTriggerParityCensus` (no new event under D-A
  recommended), `CanvasAnnouncerCensus`.

## Docs and registers

- Contract 34: append a delivery record under D5 ("edge peers delivered:
  #1277, design choices D-A/D-B as answered"), never rewriting D3–D5.
- `w_c_matrix.md:53`: the Canvas visual row gains "one Custom
  'connection' peer per materialized labelled edge, named by core's
  traversal phrase, Invoke per D5's table".
- `parity_matrix.md`: a row "canvas board edge elements" — Windows
  implemented; mac "unverified on mac; not shipped here" (no mac change
  in this chunk).
- `python -I scripts/canvas_reconciliation.py --check` must stay clean
  (WB-17 adds it to the lane).
- Under D-A recommended: `tests/fixtures` goldens for the edge names if
  the canvas corpus pins population names.

## Verification

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb26'
cargo test -p slate-core canvas::            # D-A recommended
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --filter "FullyQualifiedName~CanvasRendererPeerTests|FullyQualifiedName~CanvasPeerTopologyTests|FullyQualifiedName~CanvasAutomationPropertyCensus|FullyQualifiedName~CanvasLabelClassCensus|FullyQualifiedName~CanvasTriggerParityCensus|FullyQualifiedName~CanvasDocumentTests"
dotnet test apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj --filter "FullyQualifiedName~CanvasSurfaces_VisualBoardPeersAndZoom_AreClean"   # NVDA closed
python -I scripts/canvas_reconciliation.py --check
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/src/SlateWindows/Canvas/CanvasRendererView.cs apps/slate-windows/src/SlateWindows/Canvas/CanvasRendererPeers.cs apps/slate-windows/src/SlateWindows/Canvas/CanvasPeerTopology.cs <tests>
make ci                                       # D-A recommended
```

CI: Windows lane with the shell gate; `rust.yml` and `swift-tests.yml`
under D-A recommended (the Swift literal edits are blind — the PR waits
for that lane). Codex adversarial review (size L). AT: NVDA full pass:
on the board, object navigation reaches "connection, <phrase>" between
cards; Enter (Invoke) on an arrow-headed edge lands on the head card with
its `CanvasMovedTo` sentence; an undirected edge lands on the to-node; an
off-window edge is reachable by NVDA's find-by-name and pans into view;
an unlabelled edge is not announced anywhere.

## Acceptance

- [ ] D-A and D-B answered by the reviewer and recorded in the spec before code.
- [ ] Seven hosted facts (and the Rust fact under D-A) pass, seen failing first; mutations run; the extended journey green.
- [ ] Contract 34 record appended; `w_c_matrix.md:53` and the parity row updated; `canvas_reconciliation.py --check` clean.
- [ ] All lanes green; codex review done; format gate clean.
- [ ] NVDA pass recorded in the PR body.
- [ ] PR body: `Closes #1277`. Not delivered: edge SELECTION on the board (no SelectionItem; a seat is a node) and the mac board's edge elements.

## Brief

Read `Canvas/CanvasRendererView.cs:520-600`, `Canvas/CanvasRendererPeers.cs:20-60,
141-250`, `Canvas/CanvasPeerTopology.cs:16-20, 160-190`,
`CanvasDocumentViewModel.cs:4371-4380`, `CanvasPeerTopologyTests.cs:60-100`,
core `a11y.rs:1548-1553, 2717-2731`, `canvas/model.rs:620-640`,
`lib.rs:13272-13295`, and contract 34 `:6390-6470`. Get D-A and D-B answered
first. Then write the hosted facts and the Rust fact and see them fail;
add the edge name to the population (D-A), the abstract board peer and
`CanvasEdgeAutomationPeer` with the D5 Invoke table and D3's placeholder
behaviour, enumerate materialized labelled edges after the cards, extend
the descriptor index, extend the FlaUI board journey, append the contract
record and the matrix rows, run the NVDA pass. Done when every lane is
green and the pass is recorded. Commit: `feat(windows): labelled canvas
edges are board peers named by core's traversal phrase`.
