# CB-36, PR 1: The board paints in scene order, and a client's first search returns the item it realizes

Issues: #1838 and #1722 (this PR closes both). #1721 is only referenced here; CB-36's second PR closes it with the drawn chips. Size S to M. Lanes: windows (the app job, for the hosted facts) and the FlaUI shell gate (one new journey). AT: Call 3. Wave: C13, with CB-21 then CB-35 (Principal O). Decision: CD24 belongs to the chips PR; nothing here gates on it. Interlock: plan §6's canvas renderer row (CB-36, then CB-37 in a later wave) and the gate row: this PR adds its own partial file, `ShellAccessibilityTests.CanvasBoard.cs`, which the lead gave to CB-36 on 2026-11-25 (O stays out of it). Review: a red team, then Codex adversarial-review rounds until it approves (plan §4 rule 9: the PR changes product code).

Written 2026-11-25 from `reading/08_canvas_seal_tree.md` (read at 1ec9bf31), the two issues and a step 0 run. Every file and line below was re-read at b19c94ae, `main` when this was written. Step 0 ran on b19c94ae in this chunk's worktree (`claude/cb36-board-order-first-search`), with its own `CARGO_TARGET_DIR` (`C:/dev/slate/target-cb36-board-order-first-search`), `generate-bindings.ps1 -Locked`, a Release build (0 errors), probe facts in an untracked file (`Cb36StepZeroProbes.cs`, deleted before any commit), the new gate journey, and a measurement prototype of the fix that was reverted afterwards (`git checkout`, the DLL rebuilt and checked free of it), all under the desktop lock. The raw logs are in the principal's working notes (`step0-probes-main.log`, `step0-probes2-main.log`, `step0-p3b-main.log`, `step0-gate-first-search-main.log`, `step0-gate2-main.log`, and the `-prototype` logs), not committed. Statements that are still by reading say so. The spec's red team is folded in (record at the end).

`S/` below is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`; `G/` is `apps/slate-windows/tests/SlateWindows.AccessibilityTests/`; `W/` is WPF 10's sources as CB-25's step 0 kept them (in that principal's working notes, not committed; dotnet/wpf release/10.0, a partial copy).

**Files this PR edits.** Product: `S/Canvas/CanvasRendererView.cs` (`DrawCards`, and one internal test seam that hands out the cards layer's drawing) and `S/Canvas/CanvasRendererPeers.cs` (the board peer's `FindItemByProperty` and `GetSelection`, and one private helper). Tests: `T/CanvasRendererPeerTests.cs` and the new gate partial `G/ShellAccessibilityTests.CanvasBoard.cs`. No XAML file, no `crates/` file, no binding, no other gate partial, no contract document (Call 5).

**Overlap.** CB-21 and CB-35 edit none of these files. CB-36's chips PR edits the same two product files and adds its journey to the same gate partial; this PR owns the partial and its `BoardChildNames` helper, and the chips branch drops its own copy of both when it merges `main` after this PR lands. CB-37 (a later wave) follows both CB-36 PRs: §6 orders "CB-36, then CB-37" on these files, and the chips PR edits them too.

**No sentence is added or changed.** No `A11yEvent` is constructed or moved (contract 38's ledger does not move), no chord or help line (the parity matrix does not move), no focus call. No register row in `docs/plans/*_contracts.md` names #1838 or #1722 (searched: only #1721 has a row, contract 34 `:6514`, which the chips PR closes).

## Goal

The visual board paints its cards in core's scene order on every run, so a group listed before its members sits under them, as hit testing, the outline and contract 34 D9 already say (JSON Canvas array order is z-order, `crates/slate-core/src/canvas/apply.rs:405-412`, so a file that lists a group after its members paints it over them, as `HitTest` already answers). A UI Automation client's first ItemContainer search for a card or labelled edge outside the board's window returns the element it realizes, which then becomes the board's child of that name. The board's Selection pattern reports the seated card even while it is outside the window. A hidden board's door answers nothing and moves nothing.

## Drift check (what moved since the reading)

| Claim (reading 08, issues) | On b19c94ae |
|---|---|
| `DrawCards` `CanvasRendererView.cs:282-317`, loop over `state.Topology.Placements` at `:296-297` | Holds |
| `Placements` is an `ImmutableDictionary<CanvasPeerKey, CanvasPeerPlacement>` (`CanvasPeerTopology.cs:99`), key a `record struct (bool IsEdge, string Id)` (`:16`) | Holds |
| `HitTest` `:460-485` walks `SceneNodes` backwards (`:470`); `MaterializedPeers` `:711-733` forwards; `DrawEdges` `:326-356` walks `SceneEdges`; `DrawCards` is the only consumer that enumerates the dictionary | Holds. `git grep Placements` in `S/` finds `DrawCards` as the only enumeration; every other read is a `TryGetValue` |
| `SceneNodes` is core's order as-is (`CanvasPopulation.cs:72`) | Holds (`CanvasModelCopy.Ordered(scene?.Nodes)` at `:72`; `canvas_scene` hands "geometry in document order", `crates/slate-uniffi/src/lib.rs:14681-14689`). Card ids are unique: core skips a duplicate id (`crates/slate-core/src/canvas/mod.rs:579-580`, `:655-656`), so a scene-order loop draws no card twice |
| Contract 34 D9 at `docs/plans/34_canvas_contracts.md:6578-6581`; core's rule at `crates/slate-core/src/canvas/apply.rs:252-262` and `:405-412` | Holds |
| Layer order `_edges`, `_cards`, `_ring` at `CanvasRendererView.cs:54-56` | Holds |
| Mac stamps each card layer with its document index (`CanvasRendererView.swift:338-340`) | Holds (`:340`) |
| `FindItemByProperty` `CanvasRendererPeers.cs:142-160`, `ProviderFromPeer` straight after `RealizePeer` at `:157-159` | Holds |
| `RealizePeer` `CanvasRendererView.cs:842-854`, `Realize` `:895-918` | Holds |
| The engine builds off the dispatcher and installs later (`CanvasPresentationEngine.cs:245-261`, `Install` `:301-328`) | Holds (`Task.Run` at `:245`, `Install` at `:301`, `StateInstalled` raised at `:327`). The engine is always asynchronous, so the door can never see its own install |
| Only the install runs `RefreshAutomationChildren` (`CanvasRendererView.cs:222`, `:252-266`) | Holds, and two more callers exist: `Model`'s setter (`:183`) and a visibility change (`:203`). Neither runs inside a search |
| Board peers derive from `AutomationPeer` (`CanvasRendererPeers.cs:173-175`) | Holds |
| `T/CanvasRendererPeerTests.cs:236-239` holds the minted peer because the provider may be null with no client; `T/CanvasNavigatorTests.cs:5422` discards the return | Holds |
| The FlaUI journey `CanvasSurfaces_VisualBoardPeersAndZoom_AreClean` at `G/ShellAccessibilityTests.cs:9312`, zoom at `:9387`, scan at `:9438` | **Moved**: `:9447`, zoom `:9520`, scan `:9571`. It still never calls ItemContainer |
| `EditorHyperlinks.cs:248-249` connects then asks; `Connect` is reflection over `TrySetParentInfo` (`:335-358`); the census at `:345` | Holds (`Connect` `:357-358`, `WpfInternalsResolved` `:345`, pinned by `T/EditorSemanticTextRangeTests.cs:78`) |
| `GetSelection` (`CanvasRendererPeers.cs:131-138`) "would give an array holding null" for a seat outside the window (unverified) | Holds in process: the board peer returns a one-element array holding null. A client reads an empty selection (step 0, P3 and P3b): UI Automation's core or the managed client drops the null |
| "A hosted fact cannot see this" (#1722) | **Wrong**: an in-process client on a pool thread sees it (step 0, P2) |
| The issue's guess, "UIA core seems to drop a provider" | **Wrong**: the board peer's own door, called directly once a client has connected the board, answers null on main (step 0, P5) |

## Step 0 (b19c94ae, measured)

Probes in an untracked file in `T/`, each in a borderless 900×700 window shown with `ShowActivated = false`, over a real `VaultSession` and real canvas bytes, the document synchronous as in `CanvasRendererPeerTests`. The client is `System.Windows.Automation` on a thread-pool thread in the test process while the STA pumps (CB-25's M3 recipe), which goes through UI Automation's core and connects the window's root by `WM_GETOBJECT`. P1 to P4 ran three times, each in its own process; P3b to P7 ran on main and on the prototype; P3b ran twice more on main after its premise was fixed.

**P1, the paint order (#1838).** A group listed first (`g`, 900×600) and ten overlapping cards inside it (`c0` to `c9`), zoom 0.5. The cards layer's drawing (a `DrawingVisual`) is one `DrawingGroup` with one child per card; each child holds the card's fill as a `GeometryDrawing` over a `RectangleGeometry` and its title in a nested group. Read rectangle by rectangle and mapped back to node ids:

| Process | Painted | Group's place |
|---|---|---|
| 164164 | c8 c0 c7 c5 c2 c9 c6 c3 c4 c1 g | 11th (over every card) |
| 77896 | c7 c2 c9 c4 c1 g c5 c3 c0 c6 c8 | 6th |
| 172200 | g c1 c5 c3 c8 c4 c9 c6 c7 c2 c0 | 1st |

Scene order was `g c0 … c9` in all three. The painted order equalled the dictionary's enumeration order in all three and the scene order in none. That is the issue's "on screen in one capture and gone in the next".

**P2, the first search (#1722).** Cards "Home" at (40, 40), "Far One" at (20000, 40), "Alpha" and "Beta" at (30000, 40) and (30300, 40) with a labelled edge "cites"; the board panned to the origin.

| Search (one `FindItemByProperty` call by Name) | Main, 4 runs | Prototype |
|---|---|---|
| "Far One", first call | null | the element, on screen |
| "Far One", second call | the element | the element |
| the "cites" edge's phrase, first call | null | the element, on screen |
| the edge, second call | the element | the element |

**P3 and P3b, the seat off the window.** P3 seated "Far One" silently with Follow Selection on (the default, `CanvasViewportState.cs:39`), which revealed it, then panned back to the origin. P3b turns Follow Selection off first, so the seat never materializes and its peer is first minted by `GetSelection` (the red team's correction: in P3 the peer was minted and listed during the reveal, so a run where WPF had already connected the root could pass on main by luck). The client's `SelectionPattern.Current.GetSelection()`:

| | Main | Prototype |
|---|---|---|
| P3 (revealed, then panned away) | 0 elements (3 runs) | 1 element, "Far One" |
| P3b (never materialized) | 0 elements (2 runs) | 1 element, "Far One", offscreen |

**P4, the fix simulated without a product edit.** The client first connects the root and the board. On the STA the probe mints "Far One"'s peer through `PeerFor`, holds it, and connects it with `WpfEditorPeerConnection.Connect`. The client's first search then returns "Far One" (3 runs).

**P5, whose null it is.** A client connects the board; the STA then calls the board peer's `IItemContainerProvider.FindItemByProperty` directly. Main: **null**. Prototype: an `ElementProxy`. The null is made in Slate's process by WPF's `ProviderFromPeer`, before UI Automation's core sees anything.

**P6, the next-item door (property 0) after "Home".** Main: **null**, and the board panned to (-19312, 0): the first off-window item was realized and the answer lost. Not "Home": `PeerFromProvider` unwraps the in-process client's element (the red team's worry). Prototype: "Far One", the same pan, and "far" retained beside "home".

**P7, a hidden board.** A client holds the board element; the board is then hidden (the Outline view chosen, `Visibility` Collapsed) and the client searches for "Far One". Main: null, **and the hidden board panned** from (0, 0) to (-19312, 0). Prototype with the visibility guard: null, no pan.

**G0, the gate journey, out of process (FlaUI UIA3, the Release app).** Main, two runs: the first search for "Far One" and for the edge each returned nothing, and a second identical search returned each (the issue's shape). Prototype: green, both rows, with the element's raw-view parent the board and the element becoming the board's on-screen child of that name.

**The prototype against the existing facts.** 197 facts (`CanvasRendererPeerTests`, `CanvasNavigatorTests`, and `CanvasDocumentTests`' `ANewlyMaterializedSelectedCardRemainsReachableThroughTheBoardProvider`, `ARealizationRetainsLivePeersNotEveryCardTheBoardEverWindowed` and `TheBoardAnnouncesAChangedChildListOnceAndAnUnchangedOneNever`): all passed.

**Cause, by reading W/ and confirmed by P4 and P5.** `ProviderFromPeer` (`W/AutomationPeer.cs:1795-1806`) wraps the peer through `ElementProxy.StaticWrap`, which calls `ValidateConnected` (by memory: `ElementProxy.cs` is not in the kept copy). `ValidateConnected` (`:458-493`) accepts a peer whose parent and window handle are set, or one it finds by walking each automation root's child lists (`isDescendantOf`, through `GetChildren`, which rebuilds only a list WPF marked invalid, `EnsureChildren` `:1479-1496`), and otherwise returns null. The peer `FindItemByProperty` mints for an off-window item is in no child list until the next install refreshes the board's children, so WPF answers null. `TrySetParentInfo` (`:504-518`) sets exactly the parent and the window handle, and declines when the parent has no window handle. WPF's own items controls call it on every item peer they create for this door (`W/ItemsControlAutomationPeer.cs:359-374`, `FindOrCreateItemAutomationPeer`, used by its `FindItemByProperty` at `:227-315`), which is why a stock virtualized list's first search answers. `GetSelection` hands back the same kind of peer for a seat outside the window and loses it the same way.

## Design

1. **Paint in scene order (#1838).** `DrawCards` walks `population.SceneNodes` in order. For each node it looks up `CanvasPeerKey.Card(node.NodeId)` in `state.Topology.Placements` and skips a node with no placement or a placement that is not `Materialized` (a tombstone included), then draws from the placement's rectangle exactly as today. Nothing else in the loop changes: the fill, the border, the dimming, the title and the truncation set (which feeds the tooltip; it is cleared and refilled in the same pass as today). Groups paint where core's order puts them (D9); the renderer never sorts. A card in a move or resize preview paints at its document depth, under any later card it overlaps; that matches `HitTest` and the Mac (`CanvasRendererView.swift:340`) and is not a change of rule. Cost by reading: one `ImmutableDictionary` lookup per scene node per install instead of one enumeration of the placements; measured on `large_2000.canvas` during delivery and recorded in the PR. One internal test seam, `CardsDrawingForTests` (the cards layer's `Drawing`), lets a fact read the painted order without relying on the layer's index among the board's visual children, which the chips PR changes.
2. **A connected provider (#1722).** The board peer gets one private helper, `ConnectedProvider(CanvasBoardAutomationPeer peer)`: it connects the peer to the board (`WpfEditorPeerConnection.Connect(peer, this)`) when `WpfEditorPeerConnection.WpfInternalsResolved`, then returns `ProviderFromPeer(peer)`. `FindItemByProperty` returns `ConnectedProvider(peer)` for the peer `RealizePeer` hands back. The connection sets only the peer's parent and window handle. It does not add the peer to the board's child list: the install still refreshes the children and raises the container's one children-invalidated event (contract 34 D3); `MaterializedPeers`, `GetPeerFromPointCore`, the weak registry and the tombstone retirement are unchanged (the prototype passed their facts). A connected peer that is not yet a child answers its parent (the board) and no sibling until the install lists it (a fresh peer's index is -1, `W/AutomationPeer.cs:2505`, `:1541-1566`).
   - **The residual.** `TrySetParentInfo` declines when the board peer itself has no window handle. That holds with no client, and can also hold while a client holds the board if WPF handed the board out from a parent list cached before the root connected (by reading `W :476-489`, `:1484-1495`; not observed). In that state the door keeps today's null. Nothing in the app is known to reach it; the gate journey and P2 both connect the board the ordinary way.
   - **The guard.** `WpfInternalsResolved` also requires the `ChildrenValid` member that the connection does not use, so it is stricter than the need. If a later WPF drops either member, the door keeps today's answer rather than throwing. That fallback has no fact; `ThePeerConnectionsWpfInternalsResolveOnThisFramework` pins both members (Call 4).
3. **The seat off the window (found on the way, same class).** `GetSelection` returns `[ConnectedProvider(peer)]` for the seat, and an empty array, never an array holding null, when the provider is null (no client). A seat outside the window then reads as a placeholder: offscreen, an empty rectangle, the VirtualizedItem door and no SelectionItem (D3's placeholder table: action patterns are refused until realization, pinned for this very state by `T/CanvasDocumentTests.cs:5908`). A client that wants `IsSelected` realizes the item first (Call 6).
4. **A hidden board answers nothing (found by the red team, the door's own behaviour).** `FindItemByProperty` returns null without searching or realizing, and `GetSelection` returns an empty array, unless the board's own `Visibility` is `Visible`: the rule `GetChildrenCore` already keeps (`CanvasRendererPeers.cs:56-59`). Today a client holding the board element while the Outline or Table view shows can search the hidden board, which realizes and pans it (P7) and, with the connection alone, would hand out a placeholder whose Invoke can come alive on the hidden board's degenerate window.

**Rejected.** Answering the search only after the install, by realizing synchronously: the engine builds off the dispatcher by design (TD-6), and a synchronous provider call cannot wait for it. Adding the minted peer to the board's cached children before the install: it would list an unmaterialized peer as a child, which the hidden-surface rule and D3's one-event rule both refuse. A next-item door that returns a placeholder without realizing, as WPF's stock door does: contract 34 D3 makes this door realize what it finds (`FindItemByNameResolvesAnEdge`), so that is a contract change (Call 7).

## Tests

Each red on b19c94ae's code first, then green; each mutation named turns its fact red.

1. **`TheBoardPaintsItsCardsInSceneOrderSoAGroupIsUnderItsMembers`** (`T/CanvasRendererPeerTests.cs`, hosted). P1's fixture: a group listed first and ten overlapping cards inside it, each at its own position. After the install, the cards layer's drawing (`CardsDrawingForTests`) is read rectangle by rectangle. The expected rectangles are computed from the fixture's own geometry and the installed zoom and pan, not from the placements `DrawCards` reads (24_red_team_protocol.md: build candidates independently). The painted sequence equals the scene's order exactly (eleven entries) and the group's rectangle is the first. Red on main (P1: never the scene's order; the chance of a lucky pass is 1 in 11! per process). Mutation: restore the dictionary loop.
2. **`AClientsFirstSearchReturnsTheOffWindowItemItRealizes`** (`T/CanvasRendererPeerTests.cs`, hosted, a theory over three doors). P2's fixture, the board panned to the origin; the in-process client makes ONE `FindItemByProperty` call: by Name for "Far One", by Name for the "cites" edge's phrase, and the next item after "Home" (property 0). Each returns an element with the expected name (main: null, P2 and P6; the next-item row asserts main's answer is null, not "Home"). The element the client holds is the board's item: its raw-view parent is the board, and once the install lands it is the board's child of that name (`Automation.Compare` with the child), reads on screen and has a non-empty rectangle inside the board. The next-item row also asserts what Call 7 chooses (today's rule: the board panned to "Far One" and "far" is retained). Mutations: drop the connect from `ConnectedProvider`; connect to the window's root peer instead of the board.
3. **`AClientReadsASeatOffTheWindowAsTheSelection`** (`T/CanvasRendererPeerTests.cs`, hosted). P3b's fixture: Follow Selection off, "Far One" seated silently, premise that "far" never had a placement. The client's `GetSelection()` returns one element, named "Far One", that reads offscreen and offers the VirtualizedItem pattern. Red on main (P3b: 0 elements). Mutation: `GetSelection` back to `ProviderFromPeer`.
4. **`AHiddenBoardsDoorAnswersNothingAndMovesNothing`** (`T/CanvasRendererPeerTests.cs`, hosted). P7's fixture: the client holds the board element, the Outline view is chosen, and the client searches for "Far One" and reads the selection. The search answers null, the committed viewport has not moved, "far" is not retained, and the selection is empty. Red on main (P7: the hidden board panned). Mutation: drop the visibility guard from `FindItemByProperty`.
5. **`CanvasBoard_TheFirstNameSearchReturnsAnOffWindowItem`** (`G/ShellAccessibilityTests.CanvasBoard.cs`, the gate). The issue's own repro, out of process through FlaUI UIA3 on the Release app: a vault with P2's canvas, the Visual view chosen, and from `CanvasVisualBoard` one `ItemContainer.FindItemByProperty(null, Name, …)` call each for "Far One" and the "cites" edge. Premise: neither is one of the board's children before its search. Each first call returns the element with that name; its raw-view parent is the board; it becomes the board's child of that name, on screen inside the board. Both rows are judged before the journey fails, and a null row says whether a second, identical call found it, the issue's control. Red on main (G0, both rows); green on the prototype.

**Existing facts that must stay green, unedited** (all green on the prototype, 197): every fact in `T/CanvasRendererPeerTests.cs` (`FindItemByNameResolvesAnEdge` among them), `T/CanvasNavigatorTests.cs`, `T/CanvasDocumentTests.cs`' `ANewlyMaterializedSelectedCardRemainsReachableThroughTheBoardProvider` (`:5820-5916`, which calls `GetSelection` after `NativeWindow.RequestUiaRoot` and pins the placeholder's missing SelectionItem at `:5908`), `ARealizationRetainsLivePeersNotEveryCardTheBoardEverWindowed` (`:5927`) and `TheBoardAnnouncesAChangedChildListOnceAndAnUnchangedOneNever` (`:6032`); `T/EditorSemanticTextRangeTests.ThePeerConnectionsWpfInternalsResolveOnThisFramework`; the gate's `CanvasSurfaces_VisualBoardPeersAndZoom_AreClean` (its `SeatedCard` reads `GetSelection`; its seat helpers reject an empty rectangle, so an offscreen seat still fails them as before) and `Canvas_ContextMenus_OpenTheCardMenuByKeyboard`; and the censuses that read the gate's files: `TestWaitCensus` (rules 1, 2 and 4 walk every file in the gate project; the new partial writes its canvas with `File.WriteAllText`, reads nothing directly and waits only on conditions) and `ComboBoxCensus` (`ShellAccessibilityTests*.cs` by glob; the new partial has no combo box). The `RequestUiaRoot` recipe connects only the root; the board peer gets its window handle when something walks to it, so the new hosted facts connect the board through the pool-thread client as P2 does. The whole Windows unit project runs under the lock before the push.

## Screen-reader phrases

Call 3. By reading only (unverified): NVDA does not use the ItemContainer pattern (reading 08); whether NVDA or Narrator reads the container's Selection when the keys land on the board, and whether Narrator's scan mode walks the board with the next-item door, is not known here. If the lead takes the NVDA spot check on this PR, it records base and head phrases for: the keys landing on the board while its seat is outside the window (the Selection change), and object navigation into the board's first card and edge (no change expected).

## Ledger lines

- **#1954 (Mac work owed):** none. No core change. The Mac already paints in document order (`CanvasRendererView.swift:340`), and its board has no ItemContainer door.
- **#1955 (deferred finds):** two (Call 8, and the diff's red team, minor 11):
  - "The visual board's ItemContainer door answers null, not E_INVALIDARG, for a property it does not support (WPF's own door throws, `ItemsControlAutomationPeer.cs:231-236`); a Name search with a non-string value walks to, and realizes, the next item; and a `startAfter` that is not one of the board's peers restarts the walk at the first item, where WPF's door answers null (`CanvasRendererPeers.cs`, the door after CB-36; `CanvasRendererView.FindByName`). No reader is known to send any of the three."
  - "A canvas card peer's `SelectionItem.SelectionContainer` answers null (`CanvasRendererPeers.cs`, `CanvasCardAutomationPeer`), where the graph's node answers its diagram. Deferred because it changes a UI Automation answer a reader may speak, which needs a screen-reader check."

## Calls for the lead

1. **Fix the off-window seat in this PR?** Recommended: yes, as "Found and fixed here". It is the same class as #1722 (a provider for a peer the board's child list does not hold yet), in the same file, through the same helper. Cost: one hosted fact and two lines of product code; no new non-test file.
2. **How #1722 is proved.** Recommended: the hosted facts (red first, in the app job) and the gate journey (the issue's out-of-process repro). Cost: one more journey in the shell gate, 1 to 2 seconds a run with its app launch (measured on this desktop, three runs).
3. **The screen-reader obligation.** Recommended: an NVDA spot check on this PR, base and head, of the two cases above (about 30 minutes of the shared desktop); Narrator is `not delivered: Narrator spot check (no speech log here; the gate fact proves the UI Automation answer Narrator would get)`. The CB-36 row's NVDA spot check of the chips stays with the chips PR. Alternative: no pass here, `not delivered` naming both readers. Cost of the alternative: the Selection change reaches readers unheard.
4. **`WpfEditorPeerConnection` stays where it is** (`S/EditorHyperlinks.cs`); the canvas already calls its `InvalidateChildren` (`CanvasRendererView.cs:261`). The door guards on `WpfInternalsResolved` and keeps today's answer if WPF drops a member. Recommended: no move and no new `TryConnect` (that would add an editor-subsystem file to this PR). Cost: the guard is stricter than the need, and its fallback has no fact.
5. **No contract edit.** Contract 34 D3 and D9 already state what this PR delivers, and no register row names #1838 or #1722. D6 is silent on what Selection answers for a seat outside the window; the PR body records it. Recommended: none. Alternative: a short "Delivered by CB-36" note under D3, as #1277 has. Cost: none either way.
6. **The off-window seat has no SelectionItem.** Recommended: as designed, per D3's placeholder table and the existing pin (`CanvasDocumentTests.cs:5908`); a client realizes the item to read `IsSelected`. Alternative: a read-only SelectionItem on a selected placeholder, which changes D3/D6 and needs the owner. Cost of the recommendation: a client that confirms a selection through `IsSelected` without realizing gets "pattern not supported".
7. **The next-item walk realizes each step.** With the fix a client that walks the board with the next-item door no longer stops at the first off-window item: each step realizes, pans the board to that item and retains its key while the client holds it (D3's externally live tombstone). Recommended: accept, as D3 and D4 say a realization pans (`FindItemByNameResolvesAnEdge` pins the realizing door), and say so in the PR. Alternative: property 0 returns a placeholder without realizing, as WPF's stock door does; a contract 34 D3 change. Cost of the recommendation: a scan-mode walk moves the board item by item.
8. **The door's unsupported-property answer.** Recommended: the #1955 line above, no change here. Cost: none known to a reader.

### Lead's rulings (2026-11-25)

1. Yes: the off-window seat is fixed here, under "Found and fixed here".
2. Yes: the hosted facts plus the gate journey.
3. Yes: an NVDA spot check on this PR, base and head, for the two cases, under the desktop lock. Narrator is `not delivered` with the reason above. The chips' NVDA spot check stays with the chips PR.
4. Yes: no move and no `TryConnect`; the guard is `WpfInternalsResolved`.
5. No contract edit. The PR body records D6's silence on an off-window seat.
6. As designed: the off-window seat is a D3 placeholder with no SelectionItem.
7. Accept, as D3 and D4 already say. The PR body states it, and the §12 line's owner's-eye items carry "a next-item walk realizes and pans the board item by item".
8. Yes: the #1955 line, with no change here.

## Verification (to run)

```
# per worktree, once
$env:CARGO_TARGET_DIR = 'C:/dev/slate/target-cb36-board-order-first-search'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
# red on the tests-only commit, green on the fix, under the desktop lock
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~CanvasRendererPeerTests|FullyQualifiedName~CanvasNavigatorTests|FullyQualifiedName~CanvasDocumentTests.ANewlyMaterializedSelectedCardRemainsReachableThroughTheBoardProvider|FullyQualifiedName~CanvasDocumentTests.ARealizationRetainsLivePeersNotEveryCardTheBoardEverWindowed|FullyQualifiedName~CanvasDocumentTests.TheBoardAnnouncesAChangedChildListOnceAndAnUnchangedOneNever|FullyQualifiedName~EditorSemanticTextRangeTests|FullyQualifiedName~TestWaitCensus|FullyQualifiedName~ComboBoxCensus"
dotnet test apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj -c Release --no-build --filter "FullyQualifiedName~CanvasBoard_|FullyQualifiedName~CanvasSurfaces_VisualBoardPeersAndZoom_AreClean|FullyQualifiedName~Canvas_ContextMenus"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <each edited file>
# the whole unit project, under the lock, before the push
```

## Acceptance

The five facts are seen red on b19c94ae's code and green on the fix; each named mutation turns its fact red; the existing facts and censuses above stay green; the whole Windows unit project passes under the lock; the PR body says `Closes #1838` and `Closes #1722`, mentions #1721 with no close, fix or resolve word before it, and `gh pr view N --json closingIssuesReferences` lists exactly #1838 and #1722.

## Red team record (2026-11-25, a fresh Opus xhigh agent, read-only, on the first draft)

No blocker; nine majors and fourteen minors, all folded in:

- **Majors.** (1) A hidden board's door would hand out a connected placeholder, and already pans the hidden board on main: design 4 and fact 4, measured as P7. (2) P3 could pass on main by luck where WPF had already connected the root: P3b and fact 3 keep the seat from ever materializing. (3) No fact tied the element the client holds to the peer the install promotes: facts 2 and 5 assert the raw-view parent and the promotion, with a wrong-parent mutation. (4) The next-item walk's pan was unrecorded: Call 7. (5) The off-window seat has no SelectionItem, so "parity with WPF's selectors" was wrong in substance: design 3 and Call 6. (6) The screen-reader claim was unproven and Narrator uncovered: Call 3 rewritten, the claims marked unverified. (7) G0 and the out-of-process sufficiency were unmeasured: both measured, red on main and green on the prototype. (8) The next-item row could go red for the wrong reason: P6 shows main's answer is null, not "Home". (9) The existing-facts list missed `CanvasDocumentTests`' three board-provider facts: added, and run green on the prototype.
- **Minors.** Main returns an array holding null, not an empty one (design 3). P5 was cited before it ran (now measured). Citations corrected (`ValidateConnected` `:458-493`, ItemsControl's door `:227-315`, `StaticWrap` marked by memory). The goal no longer says "every run" for a group listed after its members. The residual of `TrySetParentInfo` declining is stated. The guard's strictness and its untested fallback are stated (Call 4). Fact 1 reads a seam and computes its expected rectangles from the fixture. The gate journey judges both rows before failing. The unsupported-property answer is Call 8's #1955 line. The shared gate partial and `BoardChildNames` belong to this PR. CB-37 follows both CB-36 PRs. D6's silence is named in Call 5. A preview card's paint depth and the unique ids are stated.

## The diff's red team (2026-11-25, a fresh Opus xhigh agent, read-only, on 5ac54e32, 9d3ec823 and dfc0557b)

One blocker, seven majors, eight minors. Taken or answered:

- **Blocker 1, taken.** The gate journey reached `SlateWindowsExe` and neither census helper, which `AnnouncementNameCensus.EveryShellJourneyRunsTheItemNameCensus` refuses; the 647-fact run did not include that census. The journey now scans the board after both realizations (`AssertAxeClean(process, "canvas-board-first-search")`), and every census in `T/Censuses` runs before the push.
- **Major 2, declined with evidence; for the lead.** A client's explicit Realize on a placeholder it holds still pans a hidden board at once and drops the reveal the board owed. That is pinned behaviour, not this PR's: `TheNewestRequestForTheViewWins`' rows "hidden: select, then realize" and "hidden: select, edit, then realize" (codex round 5: a realization made while the board is hidden pans at once, and the reveal it superseded must not undo it). Changing it reverses a reviewed rule. The PR body narrows its claim: a hidden board's search no longer realizes or pans it; an explicit Realize keeps D4's newest-request rule.
- **Major 3, declined.** The door and the selection follow the board's children rule (its own `Visibility`, `GetChildrenCore`), while the reveal machinery reads `IsVisible`. No app path collapses an ancestor while the board stays bound (the red team found none; the two-pane fact collapses a surface by hand). The PR body states the rule.
- **Majors 4 and 5, taken.** The paint fact now has twelve painted nodes: a group listed first, ten cards, a retained off-window card in the middle of the file (a tombstone placement, not painted) and a group listed last (painted last). New mutations: drop the `Materialized` filter; sort groups first.
- **Major 6, taken.** The client facts read the parent after the install may have landed, which re-parents to the board either way; their comments say so, and the PR body says M3 (connect to the window's root) is caught by `TheItemTheDoorHandsOutIsUnderTheBoardBeforeItsInstall` only.
- **Major 7, taken.** `GetSelection`'s visibility guard mutated alone.
- **Major 8, filed** as #2064 (NVDA's object navigation skips a labelled connection, on main as on the PR). The PR body does not report object navigation into an edge as checked.
- **Minor 9, taken.** The far canvas stacks Beta under Alpha, so the edge's union is 200 by 260 and fits a small board.
- **Minor 10, filed** as #2065 (the graph diagram's Selection has the same shape; another subsystem).
- **Minors 11 and 12:** the two #1955 lines above.
- **Minor 13, taken:** the comments corrected (WPF's wrap rule, a hidden search's harm, the paint order's source, where the seat's peer is first minted, the gate's reader claim, "a second search"). The first commit's subject is left as it is (no rewrite).
- **Minors 14 to 16:** the PR body's Narrator reason names what the facts prove (ItemContainer out of process; Selection in process); the whole unit project, the format gate and Codex run before the push; the body carries every listed item.

## Codex rounds

- **Round 1 (b8adafec): needs-attention, two mediums, both test weaknesses; no schedule breaking I1 to I5.** (1) The paint fact's positions rose with the scene's order, so a sort by x passed it, and no node lacked a placement. Taken: positions scrambled against the order, an off-window card with no placement added beside the retained one, and a premise that this run's dictionary order differs from the scene's (a dictionary walk can no longer pass by luck; the premise fails instead). (2) The in-process "never holds null" check ran on a shown window an external client could already have connected. Taken: `ABoardNoClientCanReachNeverHandsBackASelectionHoldingNull`, a board with no presentation source.
- **Round 2 (da597438): approve.** No material findings; I1 to I5 hold by inspection. One below-bar candidate, accepted: a run whose dictionary order matched the scene's by chance (1 in 12!) fails the paint fact's premise with correct code; it cannot pass falsely.

## What delivery changed (2026-11-25, PR #2066)

- **Facts.** Seven hosted facts and one gate journey, not five: the diff's red team and Codex added `TheItemTheDoorHandsOutIsUnderTheBoardBeforeItsInstall` (the only fact that catches a connection to the wrong parent: the client facts read the parent after the install may have re-parented it) and `ABoardNoClientCanReachNeverHandsBackASelectionHoldingNull`, and reshaped the paint fact (scrambled positions, a group last, a tombstone card, a card with no placement, a dictionary-order premise). The gate journey scans the board (the item-names census every shell journey must run).
- **The far canvas** stacks Beta under Alpha (union 200 by 260), so the edge fits a small board.
- **Mutations** (eleven), each red: listed in the PR body.
- **NVDA** (base and head): no phrase differs; NVDA never reaches a labelled edge by object navigation, on main either (#2064).
- **Filed:** #2064 (NVDA object navigation and the edge's control type), #2065 (the graph diagram's Selection).
- **Declined with evidence:** a Realize on a hidden board keeps the pinned newest-request rule; the door follows the children's Visibility rule.
- **The whole unit project, CI's filter:** 9,066 facts, 9,064 passed, 2 skipped. A first attempt without CI's filter ran the `ConnectionsLeafTests.TheModelOf` facts and the benchmarks, which CI excludes; it was stopped after 49 minutes (6,461 passed, 0 failed) and rerun with the filter.
- **Spec claims that were wrong:** "a hosted fact cannot see #1722"; the issue's "UIA core drops the provider"; the gate journey's line numbers (moved).
- **Merged** 2026-11-26 as 01b293b3. The merge judge (standing in for Codoki): MERGE, 0 blockers, 0 majors, 1 minor, 4 nits. The minor is the pre-existing explicit Realize on a held placeholder that pans a hidden board, which the diff red team kept because `TheNewestRequestForTheViewWins` pins it; filed as #2068 on the lead's ruling, so the count is 7 / 2 / 2 / 3. The two deferred finds are on #1955 (issuecomment-7184650454). CI at da597438: `SlateWindows.Tests` 8,931 passed, the shell gate 240 passed, the new journey among them.
