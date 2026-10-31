# WB-26 — Labelled canvas edges are UIA peers on the visual board: named by core's traversal phrase, windowed like cards, Invoke total over the end-style table

Issues: Closes #1277. Size L (design first). Lanes: windows + FlaUI (shell gate), rust, swift (compile) — all three under EITHER D-A answer, as plan §5 lists them: the recommended answer changes an FFI record (Swift literals edited), the alternative exports a new core function (both bindings regenerated, the Swift binding compiles unchanged). AT: NVDA (full pass). Wave: W9. Depends on: WB-13 (last on `Canvas/CanvasRendererView.cs` and `CanvasOutlineView.cs`, after plan 43 PR 32, §6). Parallel with: WB-25, WB-27. Decision: none in the plan; the design section below carries two choices for the reviewer.

**Owner's call of 2026-10-26** (plan §3.1, "Review calls") outranks this spec where they differ: WB-26 does not wait for plan 43 PR 32 (#1370) — they share no Windows file; only core's `a11y.rs` and `lib.rs` may need a rebase.

**Design answers, recorded 2026-10-31 before code.** These answers come from the run process (plan §3.1, "Run process"). The design section's recommendations were taken, and a fresh red team reviewed this record before the first fact was written.
- **D-A: recommended (core owns the name).** The answer comes with five refinements:
  - **Where names are allocated.** `canvas::model` allocates the names in the same skip-occupied loop as the cards' speakable names, right after the cards. It walks edges in document order and keeps them in a new `CanvasModel.edge_names: HashMap<EdgeId, String>`. An edge is named only if:
    - its label is non-empty, the host's own DD-2 test (`Label is { Length: > 0 }`), and
    - both of its endpoints exist (a dangling edge is never named, as adjacency already excludes it).
  - **Ordinals.** A colliding edge phrase takes `"{phrase} {n}"`. As with the card ordinals, `n` skips every spelling already used and every real card title.
  - **The phrase.**
    - It is rendered in `a11y.rs` by a new `canvas_edge_phrase(from_kind, from_name, direction, to_kind, to_name, label)`.
    - It shares its direction words with the `CanvasConnectionTraversed` renderer through one helper, so the two cannot drift.
    - Its form is `{from ref} {connects to | connected from | linked with} {to ref}, labelled "{label}"`.
    - `direction` is `direction_from(true, from_end, to_end)`, the from-node's own reading of the edge.
    - Each ref is `card_ref(kind, speakable name)`. The endpoints' speakable names, not their raw titles, so the phrase names each card the way the board names it.
  - **FFI.**
    - The record gains `speakable_name: Option<String>`, not the design section's `String`.
    - `None` means exactly "not a peer" (DD-2), so the host never re-derives labelled-ness from an empty string.
    - It is the record's last field, with `#[uniffi(default = None)]`.
  - **Swift.** No Swift source constructs a `CanvasSceneEdge`: `git grep CanvasSceneEdge apps/slate-mac` finds nothing outside the generated binding. So no Swift literal is edited, and the Swift lane compiles the regenerated binding.
- **D-B: recommended.**
  - The edge peer is `AutomationControlType.Custom` with `LocalizedControlType` "connection" and `ClassName` "CanvasEdge".
  - It exposes Invoke (D5's table) and VirtualizedItem (always, as the card does), and no SelectionItem.
  - `HelpText` and `ItemStatus` are empty.
  - `AutomationId` is `CanvasEdge:{edge id}` (the card peer's existing rule).
- **Base class.** The new `CanvasBoardAutomationPeer` base carries the shared code:
  - the key and the materialized test;
  - the screen rectangle from the placement;
  - the VirtualizedItem door and offscreen;
  - the AutomationId;
  - the empty abstract surface.
  The card and edge peers keep only their own name, control type and action patterns.
- **Find by name.** `FindByName` walks cards, then named edges, in document order. Its `after` argument may be a card or an edge peer.
- **Validity check.** `PeerFor` and `Realize` treat an edge key as valid only when the edge is in `SceneEdges`, has a speakable name, and has both endpoints in `SceneByNode`.

**Red-team amendments, 2026-10-31, before code.** Where these differ from the design section or the Tests below, they win.
1. **Hit-testing.** An edge's rectangle is the union of its endpoints' rectangles, so it covers them, and WPF's point walk checks children last-first. The board peer therefore overrides `GetPeerFromPointCore`:
   - a card wins;
   - an edge answers only a point no card covers.
   New fact: `CanvasRendererPeerTests.APointOnAnEndpointCardIsTheCardNotTheEdge`.
2. **The item-container door realizes, for edges as for cards.**
   - **Find by name.** `FindItemByProperty` goes through `RealizePeer`, as it does for cards (`TheBoardsOwnPeersRevealTheirCardWithFollowSelectionOff` pins that pan). So `FindItemByNameResolvesAnEdge` asserts that:
     - the descriptor lookup (`FindByName`) changes nothing;
     - the container's search hands back the edge's peer;
     - that peer is promoted with the bounded realization pan.
   - **Placeholder.** A placeholder refuses action patterns the card's way: `GetPattern(Invoke)` is null. It does not throw `ElementNotAvailableException`, which nothing in the canvas does.
3. **Censuses.**
   - **`WcMatrixCanvasEvidenceCensus` is the census that applies.** It scrapes each peer class's own source span, so `GetPattern` stays in each subclass; the base class carries no pattern code.
   - **Its manifest gains:**
     - `CanvasEdgeAutomationPeer` (`Custom`; Invoke, VirtualizedItem);
     - `Custom` in the Canvas visual row's control-type cell;
     - the new fact names.
   - **Dropped:** `CanvasAutomationPropertyCensus` sees no peer, so it is out of the filter.
4. **D5's four-way fixture.**
   - `EveryBoardPeerHasItsOwnNameAndTheContainersNameIsReserved` now waits for and asserts its two twin edges' peers: the phrase, and the phrase with " 2".
   - The Rust fact covers twin parallel edges, a card that squats on an edge's phrase, and a card pre-named with an edge ordinal.
5. **Parity matrix.** `parity_matrix.md` is generated, so any row goes through `scripts/generate-parity-matrix.py`, never by hand.
6. **One labelled test.**
   - Core names only an edge whose label is not blank (`!label.trim().is_empty()`, as group labels are trimmed).
   - The topology's DD-2 test reads `SpeakableName is not null` instead of the label, and the topology facts' edge literals pass a `SpeakableName`.
7. **Speakable names in the phrase.** The phrase references the endpoints by their speakable names, while the walk speaks `display_title`. This is a deliberate refinement of D5's "the same sentence": the board's references must match the cards' own board names. Contract 34's delivery record says so.
8. **Lookup and geometry.**
   - **Edge index.** The load-class descriptor index gains `SceneByEdge` (edge id to scene edge), built once with the population (ID-3), so a Name read is no linear scan.
   - **What the topology keeps.** Edge geometry stays the topology's (a drag moves it).
   - **Realization target.** `Realize` builds an edge's union from the node rectangles, because a placeholder's placement has no geometry.
   - **Find by name.** `FindByName` compares discriminated keys, never bare ids.
9. **Typed test sites.** Two facts typed `PeerFor`/`RealizePeer` as the card peer (`CanvasNavigatorTests`, `CanvasDocumentTests`). They now assert the type or hold the base class.
10. **Swift.** The "Swift literal edits" in the header, the design section and the Brief do not apply (see D-A above). `canvas::model` now calls `crate::a11y::canvas_edge_phrase`, its first use of `a11y`.

**Review of the code (codex round 1 and the code red team), 2026-10-31.**
- **D3's identity is (renderer, document, key).**
  - The registry is keyed by document, and every peer records its own.
  - A held peer exposes no pattern, reads offscreen and has an empty rect once its card or edge is gone, the pane closes, or the board shows another document.
  - `PeerFor` revalidates before it returns a held peer.
  - This applies to cards too, closing the same gap they had.
- **Realization reads the installed state's effective rects** (`NodeRect`) through one `CanvasPeerTopology.EdgeUnion`.
- **The board's hit test answers only inside the board,** each child clipped to it.
- **Facts:**
  - `APointBesideTheBoardIsNeverOneOfItsPeers`
  - `AHeldEdgePeerGoesUnavailableWhenItsEdgeStopsBeingAPeer`
  - `AnEdgePeerHeldOnOneCanvasNeverAnswersForAnother`
  - `AnEdgeRealizesOnTheRectanglesItsBoardDraws`
  The placeholder and find facts now target an edge whose designated endpoint is the LEFT card, so only the union branch passes them.
- **Follow-ups:** the mac board's edges are #1720; an edge's bounds as a label chip, and the full-cover axe risk, are #1721.

**Codex round 2, 2026-10-31.**
- **Retained providers recheck at call time.** A client keeps the pattern interfaces it was handed, so every action rechecks when it runs (`RequireAvailable`):
  - Realize refuses with `ElementNotAvailableException` once the peer's card or edge is gone, or its document is no longer shown.
  - Invoke and the SelectionItem actions refuse the same way, and also off the window.
- **The renderer records each publication's document** as it forwards it to the engine (`InstalledDocument`). Every peer-surface read requires the installed state to be the bound document's:
  - `PeerFor`;
  - `FindByName`;
  - `Realize`;
  - `MaterializedPeers`;
  - each peer's own reads.
  So nothing answers from the previous canvas's scene during a switch.
- **Facts:**
  - `ARetainedProviderRefusesOnceItsPeerIsUnavailable`
  - `AHeldPeerReadsNothingUntilItsCanvasIsInstalledAgain`

**Codex round 3, 2026-10-31.**
- **Actions also check the applied population.** An action checks the document's APPLIED population as well as the installed one (`ExistsIn`): the board installs a delete, an unlabelling or a reload a moment after the document applies it, and nothing may act on what the document already dropped.
- **Invoke reads the applied edge.** An edge's Invoke picks its endpoint from the document's applied edge, so an applied direction change is obeyed.
- **Invoke always says where it landed.** It does so even when that card is already the seat (`SelectNode`'s `announceWhenSeated`, used by the edge peer only), with one announcement each time.
- **A card's Invoke on the seated card stays silent,** as every other selection door is.
- **Facts:**
  - `ARetainedActionObeysWhatTheDocumentAppliedBeforeTheBoardInstallsIt`
  - `InvokingAnEdgeWhoseEndpointIsTheSeatStillSaysWhereItLanded`

**Codex round 4, 2026-10-31.**
- **A realization checks its key against the document's applied scene.**
- **It defers while the board lags.** When the installed scene is not yet the applied one, it commits the key as retained and owes the pan to the first install that is (`PayOwedRealization`, beside the owed reveal). The target is computed from that state's rects, so a reload or a direction change already applied is the one obeyed.
- **The item-container search refuses dropped keys.** `RealizePeer` refuses a key the applied scene has dropped.
- **Fact:** `ARealizationWaitsForTheSceneTheDocumentApplied`.

**Codex round 5, 2026-10-31.**
- **The board's hit test returns null outside the board, or while it is hidden.** Returning the board itself would end the window's walk there and mask the sibling control under the point.
- **The newest request for the view wins.**
  - A reveal and a viewport verb each drop an owed realization.
  - A realization that pans at once drops an older owed reveal.
  - An owed realization is paid after any owed reveal, so it lands last.
- **Facts:**
  - `APointBesideTheBoardIsNeverOneOfItsPeers` now also walks the window onto the switcher button with the board shown and hidden;
  - `TheNewestRequestForTheViewWins` covers both orders, a fit verb, and a hidden board.

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
- `CanvasRendererView.cs:653-664` `Realize(key)` looks the key up in
  `population.SceneByNode` only (an edge key returns at once), commits it
  retained and calls `PanToContain(v, node)`; `:666-691` `PanToContain`
  keeps `viewport.Zoom` and only pans, so it can contain a rect no wider
  and no taller than the view at the current zoom — for a larger rect it
  brings one edge into view (the left or top edge when the rect starts
  left of or above the view, else the right or bottom edge). `FindByName`
  (`:611-633`) walks `SceneNodes` only.
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
  from_title, direction, to_kind, to_title, label) -> String` (new in
  `a11y.rs` beside `resolved_embed_title`, exported with
  `#[uniffi::export]` in `lib.rs` — a core and FFI change: `rust.yml`
  runs, both bindings regenerate, the Swift binding compiles unchanged
  with no literal edits), with the host running the skip-occupied
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
  → `_view.Realize(Key)`, which for an edge pans to a **bounded
  realization target** (below) and installs the next window;
  `IInvokeProvider.Invoke` → `_view.SelectAnnounced(target)` where
  `target` is the edge's **designated endpoint**, the D5 table over
  `(FromArrow, ToArrow)`: `(false,true)` → `ToNode`, `(true,false)` →
  `FromNode`, else `ToNode` — one private helper,
  `CanvasRendererView.DesignatedEndpoint(CanvasSceneEdge edge) -> string`
  (new), serves both Invoke and realization. Placeholder cell:
  identifying properties from the descriptor index, action patterns
  refused until realization (`ElementNotAvailableException`, the card's
  rule).
- **The bounded realization target (D3/D4).** An edge's union rect can be
  wider or taller than the view — two cards far apart — and
  `PanToContain` (`:666-691`) only pans, at the current zoom, so no pan
  can contain such a union; a realization that zooms to fit would change
  the reader's zoom as a side effect of a UIA call. D3 only needs an
  endpoint in the window for the edge to materialize, and D4's "pans to
  contain it" is satisfiable for a card. So `Realize(key)` for an edge
  key looks the edge up in `population.SceneEdges` (a missing or
  unlabelled edge returns, as a missing card does) and pans to contain:
  the union rect when its width and height at the current zoom are each
  at most the view's (`width * zoom <= ViewWidth` and `height * zoom <=
  ViewHeight`) — the whole edge comes into view; otherwise the
  designated endpoint's card rect — the card Invoke will select comes
  into view, the edge's placement (whose union now intersects the
  window) is `Materialized`, and the same peer object is promoted.
  `PanToContain` gains a rect overload, `private static
  CanvasViewportState PanToContain(CanvasViewportState viewport, double
  x, double y, double width, double height)` (new), which the node
  overload calls with the node's rect; neither ever changes `Zoom`. A
  card's realization is unchanged.
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
- Under the D-A alternative instead: Rust
  `a11y::tests::canvas_edge_name_renders_both_endpoints_and_the_label` —
  for each `EdgeDirection` the phrase names both endpoints' kind and
  title in core's traversal grammar and ends `, labelled "…"`. Mutation:
  drop the other endpoint → fails.
- `CanvasRendererPeerTests.ALabelledEdgeInTheWindowIsAChildOfTheBoard` —
  a canvas with a visible "supports" edge: the renderer peer's children
  include an edge peer named by core's phrase; an unlabelled edge yields
  none (DD-2). Mutation: restore the `IsEdge` null at `:576` → fails.
- `…AnEdgeIsAPlaceholderOffWindowAndPromotesOnRealize` — an edge whose
  two cards lie together beyond the window, its union smaller than the
  view at the current zoom (asserted as a precondition): the retained
  edge peer reports VirtualizedItem and refuses Invoke; after `Realize()`
  the viewport contains the whole union rect, `Zoom` equals its value
  before, the edge's placement is `Materialized`, the peer returned is
  the same object and is no longer a placeholder, and Invoke works
  (D3/D4). Mutation: realize an edge key through the card path
  (`SceneByNode` misses, nothing pans) → the placement stays a
  placeholder.
- `…AnEdgeLongerThanTheViewportRealizesOnItsDesignatedEndpoint` (new;
  a `[Theory]` over `(false,true)` with the to-node as the left card and
  `(true,false)` with the from-node as the left card) — two cards far
  apart, both beyond the view's right edge, so the union's width at the
  current zoom exceeds `ViewWidth` (asserted as a precondition) and the
  designated endpoint is the left card: after `Realize()`, `Zoom` equals
  its value before, the designated endpoint's card rect lies wholly
  inside the viewport, the edge's placement is `Materialized` and the
  same peer object is promoted (no longer a placeholder); Invoke then
  selects that endpoint with one announcement and moves the viewport no
  further. Mutations: pan to contain the union (`PanToContain` brings
  the union's right edge, the other card, into view → the designated
  card is outside); fit the union by changing `Zoom` (→ the zoom
  assertion fails); pan to the endpoint the D5 table does not name (→
  the `(true,false)` row fails).
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
  #1277, design choices D-A/D-B as answered; an edge's realization pans,
  at the current zoom, to contain its union rect when that fits the view
  and otherwise its designated endpoint — the card D5's table names for
  Invoke — so D4's never-dead-end holds for an edge longer than the view
  and realization never changes the zoom"), never rewriting D3–D5.
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

PowerShell, repo root. Each filter is listed first and the block throws
unless every required fact is discovered under it; every command's
`$LASTEXITCODE` is checked (no output grepping for "Failed").

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb26'
function Assert-Ok([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step failed ($LASTEXITCODE)" } }
$daAnswer = 'recommended'   # the D-A answer recorded in this spec's header before code: 'recommended' or 'alternative'
$rustFilter, $rustFact = if ($daAnswer -eq 'recommended') { 'canvas::', 'edge_speakable_names_join_the_card_namespace' } else { 'canvas_edge_name', 'canvas_edge_name_renders_both_endpoints_and_the_label' }
$rust = cargo test -p slate-core --locked $rustFilter -- --list; Assert-Ok "listing $rustFilter"
if (-not ($rust -match "(^|::)$([regex]::Escape($rustFact)): test$")) { throw "$rustFilter does not select $rustFact" }
cargo test -p slate-core --locked $rustFilter; Assert-Ok "cargo test $rustFilter"
cargo test -p slate-uniffi --locked crosses_the_ffi; Assert-Ok 'uniffi crosses_the_ffi'
./apps/slate-windows/generate-bindings.ps1 -Locked; Assert-Ok 'generate-bindings'
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; Assert-Ok 'dotnet build'
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$filter = 'FullyQualifiedName~SlateWindows.Tests.CanvasRendererPeerTests|FullyQualifiedName~SlateWindows.Tests.CanvasPeerTopologyTests|FullyQualifiedName~SlateWindows.Tests.Censuses.CanvasAutomationPropertyCensus|FullyQualifiedName~SlateWindows.Tests.Censuses.CanvasLabelClassCensus|FullyQualifiedName~SlateWindows.Tests.Censuses.CanvasTriggerParityCensus|FullyQualifiedName~SlateWindows.Tests.Censuses.CanvasAnnouncerCensus|FullyQualifiedName~SlateWindows.Tests.CanvasDocumentTests'
$names = dotnet test $tests --no-build --configuration Release --list-tests --filter $filter; Assert-Ok 'test discovery'
$required = @('ALabelledEdgeInTheWindowIsAChildOfTheBoard', 'AnEdgeIsAPlaceholderOffWindowAndPromotesOnRealize', 'AnEdgeLongerThanTheViewportRealizesOnItsDesignatedEndpoint', 'InvokeSelectsTheEndpointTheEndStyleTableNames', 'TheSameEdgePeerIsReturnedWhileAnythingHoldsIt', 'FindItemByNameResolvesAnEdge', 'AStructureChangeRaisesChildrenInvalidatedOncePerInstall')
$missing = @($required | Where-Object { -not ($names -match "CanvasRendererPeerTests\.$([regex]::Escape($_))(\(|\s*$)") })   # a Theory lists its rows as Name(...)
if ($missing.Count -gt 0) { throw "the filter does not select: $($missing -join ', ')" }
dotnet test $tests --no-build --configuration Release --filter $filter; Assert-Ok 'the hosted facts and censuses'
# NVDA closed, desktop free (ShellAccessibilityTests is a partial class: select by method name)
$at = 'apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj'
$atFilter = 'FullyQualifiedName~ShellAccessibilityTests.CanvasSurfaces_VisualBoardPeersAndZoom_AreClean'
$atNames = @(dotnet test $at --no-build --configuration Release --list-tests --filter $atFilter | Select-String 'CanvasSurfaces_VisualBoardPeersAndZoom_AreClean'); Assert-Ok 'FlaUI discovery'
if ($atNames.Count -lt 1) { throw "the FlaUI filter selects nothing: $atFilter" }
dotnet test $at --no-build --configuration Release --filter $atFilter; Assert-Ok 'FlaUI journey'
python -I scripts/canvas_reconciliation.py --check; Assert-Ok 'canvas reconciliation'
$base = git merge-base origin/main HEAD; Assert-Ok 'merge-base'
$cs = @(git diff --name-only $base -- '*.cs') + @(git ls-files --others --exclude-standard -- '*.cs')   # Canvas/CanvasRendererView.cs, Canvas/CanvasRendererPeers.cs, Canvas/CanvasPeerTopology.cs, CanvasRendererPeerTests.cs (new), CanvasPeerTopologyTests.cs, ShellAccessibilityTests.cs
if ($cs.Count -eq 0) { throw 'no C# changes found' }
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs; Assert-Ok 'dotnet format'
# the Rust gates (plan §4 rule 3; CONTRIBUTING.md:76-98 — no make on this machine):
cargo fmt --all -- --check; Assert-Ok 'cargo fmt'
cargo clippy --all-targets --workspace -- -D warnings; Assert-Ok 'cargo clippy'
cargo test --workspace --locked; Assert-Ok 'cargo test --workspace'
cargo bench --workspace --no-run; Assert-Ok 'cargo bench --workspace --no-run'
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh; Assert-Ok 'license headers'   # Git Bash by full path: a bare `bash` in PowerShell here resolves to WSL
```

CI: Windows lane with the shell gate, `rust.yml` and `swift-tests.yml`
under either D-A answer (under the recommended answer the Swift literal
edits are blind — the PR waits for that lane; under the alternative the
lane proves the regenerated Swift binding compiles). Codex adversarial review (size L). AT: NVDA full pass:
on the board, object navigation reaches "connection, <phrase>" between
cards; Enter (Invoke) on an arrow-headed edge lands on the head card with
its `CanvasMovedTo` sentence; an undirected edge lands on the to-node; an
off-window edge is reachable by NVDA's find-by-name and its realization
pans the whole edge into view when it fits, else the card Invoke would
select, with the zoom unchanged; an unlabelled edge is not announced
anywhere.

## Acceptance

- [ ] D-A and D-B answered by the reviewer and recorded in the spec before code.
- [ ] Seven hosted facts (`ALabelledEdgeInTheWindowIsAChildOfTheBoard`, `AnEdgeIsAPlaceholderOffWindowAndPromotesOnRealize`, `AnEdgeLongerThanTheViewportRealizesOnItsDesignatedEndpoint`, `InvokeSelectsTheEndpointTheEndStyleTableNames`, `TheSameEdgePeerIsReturnedWhileAnythingHoldsIt`, `FindItemByNameResolvesAnEdge`, `AStructureChangeRaisesChildrenInvalidatedOncePerInstall`) and the Rust fact of the recorded D-A answer pass, seen failing first and discovered by name; mutations run; the extended journey green.
- [ ] An edge's realization never changes the zoom: it contains the union when that fits the view, otherwise the designated endpoint (D5's Invoke target), and the edge peer is promoted either way.
- [ ] The Verification block runs clean in PowerShell: every command's `$LASTEXITCODE` 0, the Rust gates of plan §4 rule 3 included (`cargo bench --workspace --no-run`, the license-header check under Git Bash).
- [ ] Contract 34 record appended; `w_c_matrix.md:53` and the parity row updated; `canvas_reconciliation.py --check` clean.
- [ ] All lanes green; codex review done; format gate clean.
- [ ] NVDA pass recorded in the PR body.
- [ ] PR body: `Closes #1277`. Not delivered: edge SELECTION on the board (no SelectionItem; a seat is a node) and the mac board's edge elements.

## Brief

Read `Canvas/CanvasRendererView.cs:520-700` (the registry, `PeerFor`,
`FindByName` `:611-633`, `Realize` `:653-664` — card keys only — and
`PanToContain` `:666-691`, which keeps the zoom and so cannot contain a
rect larger than the view), `Canvas/CanvasRendererPeers.cs:20-60,
141-250`, `Canvas/CanvasPeerTopology.cs:16-20, 160-190`,
`CanvasDocumentViewModel.cs:4371-4380`, `CanvasPeerTopologyTests.cs:60-100`,
core `a11y.rs:1548-1553, 2717-2731`, `canvas/model.rs:620-640`,
`lib.rs:13272-13295`, and `docs/plans/34_canvas_contracts.md:6390-6470`
(D3, D4, D5 and the end-style table at `:6465-6470`). Two design choices
are pending with the reviewer and must be recorded in this spec before
code: D-A (recommended: core allocates `speakable_name` on
`CanvasSceneEdge` in the population's skip-occupied naming loop — an FFI
record change, so `git grep CanvasSceneEdge apps/slate-mac` names every
Swift literal to edit in the same PR; alternative: a core free fn
`canvas_edge_name(...)` exported through uniffi with a host-side ordinal
loop pinned against card names) and D-B (recommended: `Custom` control
type, localized "connection", Invoke + VirtualizedItem, no SelectionItem;
alternative: `Hyperlink`). Build: an abstract `CanvasBoardAutomationPeer`
base for `CanvasCardAutomationPeer` and a new `CanvasEdgeAutomationPeer`
(name = the edge's speakable name; screen rect from the placement's union
rect on every pan/zoom/resize; `IVirtualizedItemProvider.Realize` →
`_view.Realize(Key)`; `IInvokeProvider.Invoke` →
`_view.SelectAnnounced(target)` with target = the designated endpoint
from the new private `CanvasRendererView.DesignatedEndpoint(CanvasSceneEdge
edge) -> string`: `ToNode` for `(false,true)`, `FromNode` for
`(true,false)`, `ToNode` for `(false,false)` and `(true,true)`;
placeholder cell refuses action patterns with
`ElementNotAvailableException`); `Realize(key)` for an edge key looks
the edge up in `SceneEdges` and pans, at the current zoom, to a bounded
target — the union rect when its width and height at the current zoom
fit the view, otherwise the designated endpoint's card rect — through a
new rect overload `private static CanvasViewportState
PanToContain(CanvasViewportState viewport, double x, double y, double
width, double height)` that the node overload calls; realization never
changes `Zoom` (an edge can span more than the view, and D3 needs only
an endpoint in the window to materialize it); `_peers` retyped to the base;
`PeerFor` mints an edge peer for a labelled `SceneEdges` id;
`MaterializedPeers` appends materialized labelled edges after the cards
in document order; the installed state's descriptor index gains edge
descriptors; unlabelled edges never become peers (DD-2). Facts, written
first and seen failing: Rust
`canvas::tests::edge_speakable_names_join_the_card_namespace` (two
same-label edges to two destinations differ; a collision with a card title
takes the next free ordinal) under D-A recommended;
`CanvasRendererPeerTests.ALabelledEdgeInTheWindowIsAChildOfTheBoard`
(an edge peer named by core's phrase among the board's children; an
unlabelled edge yields none);
`CanvasRendererPeerTests.AnEdgeIsAPlaceholderOffWindowAndPromotesOnRealize`
(an off-window edge whose union fits the view, asserted first:
VirtualizedItem reported, Invoke refused; after `Realize()` the viewport
contains the union, `Zoom` unchanged, the placement `Materialized`, the
same peer promoted and Invoke works — mutation: realize through the card
path, nothing pans);
`CanvasRendererPeerTests.AnEdgeLongerThanTheViewportRealizesOnItsDesignatedEndpoint`
(new; a `[Theory]` over `(false,true)` with the to-node as the left card
and `(true,false)` with the from-node as the left card, both cards
beyond the view's right edge and the union wider than the view, asserted
first: after `Realize()` `Zoom` is unchanged, the designated endpoint's
card lies wholly in the viewport, the placement is `Materialized`, the
same peer is promoted, and Invoke selects that card with one
announcement and no further pan — mutations: pan to contain the union
(the other card comes into view), fit the union by zooming, pan to the
endpoint the table does not name);
`CanvasRendererPeerTests.InvokeSelectsTheEndpointTheEndStyleTableNames`
(a `[Theory]` over the four cells, selected node and one announcement);
`CanvasRendererPeerTests.TheSameEdgePeerIsReturnedWhileAnythingHoldsIt`
(identity per key per renderer; a second pane holds its own);
`CanvasRendererPeerTests.FindItemByNameResolvesAnEdge` (descriptor index,
no materialization);
`CanvasRendererPeerTests.AStructureChangeRaisesChildrenInvalidatedOncePerInstall`
(one `StructureChanged` when an edge is added); FlaUI
`ShellAccessibilityTests.CanvasSurfaces_VisualBoardPeersAndZoom_AreClean`
(`ShellAccessibilityTests.cs:9316`) extended: a custom child named by the
edge phrase whose Invoke selects and announces the to-node. Mutations:
restore the `IsEdge` null at `CanvasRendererView.cs:576`; swap one
end-style cell; skip edges in the naming loop; the three realization
mutations above. Under the D-A alternative the Rust fact is
`a11y::tests::canvas_edge_name_renders_both_endpoints_and_the_label`
(each `EdgeDirection` names both endpoints and ends `, labelled "…"`;
mutation: drop the other endpoint). Registers: append a
delivery record under D5 in `docs/plans/34_canvas_contracts.md` (it
also records the bounded realization target: the union when it fits at
the current zoom, else the designated endpoint, never a zoom change);
amend `docs/plans/18_windows_port/w_c_matrix.md:53`; add a "canvas board
edge elements" row to `docs/plans/18_windows_port/parity_matrix.md`
(Windows implemented; mac unverified, not shipped here);
`python -I scripts/canvas_reconciliation.py --check` clean. Not
delivered: edge selection on the board (no SelectionItem) and the mac
board's edge elements. Done when `windows.yml` (with the shell gate),
`rust.yml` and `swift-tests.yml` are green under the recorded D-A answer,
the Verification block runs clean in PowerShell (the Rust fact of the
recorded D-A answer listed by `cargo test -- --list` and the seven hosted
facts and the FlaUI journey listed by `--list-tests`, each throwing when
missing; every command's `$LASTEXITCODE` 0 — the five Rust gates
`cargo fmt --all -- --check`, `cargo clippy --all-targets --workspace --
-D warnings`, `cargo test --workspace --locked`, `cargo bench --workspace
--no-run` and `& "$env:ProgramFiles\Git\bin\bash.exe"
scripts/check-license-headers.sh` (Git Bash by full path; a bare `bash`
here is WSL), and the format gate), the codex review
(size L) is done, the mutation run is recorded, and the NVDA pass
(object navigation reaches "connection, <phrase>"; Invoke lands on the
head card with its `CanvasMovedTo` sentence; an undirected edge lands on
the to-node; an off-window edge is found by name and its realization
pans the whole edge into view when it fits, else the card Invoke would
select, with the zoom unchanged; an unlabelled edge is never announced)
is in the PR body with `Closes #1277`.
Commit: `feat(windows): labelled canvas edges are board peers named by
core's traversal phrase`.
