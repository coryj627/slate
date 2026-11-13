# Reading 08 — canvas, the palette seal, the Files tree, two design follow-ups

Baseline: worktree `<worktree>` at 1ec9bf31. Read-only; nothing was built or run. All paths are under `apps/slate-windows/` unless they start with `docs/` or `crates/`. `src/` means `src/SlateWindows/`, `tests/` means `tests/SlateWindows.Tests/`.

"Unverified" marks every statement not checked in this repo's code. Statements about WPF's own internals are from memory of the WPF source and are all marked.

`git log --grep "#N"` for the twelve issues finds only the PRs that filed them (#1723 for #1721/#1724, #1696 for #1694, #1667 for #1665, #1672 for #1670/#1669). No merge on `main` has fixed or changed any of the twelve.

---

## #1838 — the visual canvas paints in hash order

- **Verdict:** stands.
- **Current state:**
  - `src/Canvas/CanvasRendererView.cs:282-317` `DrawCards`. The loop at `:296-297` enumerates `state.Topology.Placements`.
  - `src/Canvas/CanvasPeerTopology.cs:99` `Placements` is an `ImmutableDictionary<CanvasPeerKey, CanvasPeerPlacement>`. The key is a `record struct (bool IsEdge, string Id)` (`:16`), so its hash includes a string hash, which .NET randomizes per process. Enumeration order is therefore not the document's and differs run to run.
  - `HitTest` (`CanvasRendererView.cs:460-485`) walks `population.SceneNodes` backwards (`:470`). `MaterializedPeers` (`:711-733`) walks `SceneNodes` forwards. `DrawEdges` (`:326-356`) walks `SceneEdges`. `DrawCards` is the only consumer that enumerates the dictionary.
  - `SceneNodes` is core's order copied as-is (`CanvasPopulation.cs:72`, `CanvasModelCopy.cs:90-91`).
  - The contract fixes the order: `docs/plans/34_canvas_contracts.md:6578-6581`, D9: "Hit-testing and z-order are core's order, consumed. Topmost by DOCUMENT order ... groups behind their members (#960's insertion rule). The renderer derives z from core's order and never re-sorts." Core's rule is `crates/slate-core/src/canvas/apply.rs:252-262` and `:405-412` (array order is z-order, first node is lowest; a new group is inserted before the first node it contains).
  - Layer order is already right: `_edges`, then `_cards`, then `_ring` (`CanvasRendererView.cs:54-56`).
- **Cause:** verified by reading. `DrawCards` draws in dictionary order.
- **Fix shape:** in `DrawCards`, iterate `population.SceneNodes` in order, look each node's placement up with `Placements.TryGetValue(CanvasPeerKey.Card(node.NodeId), ...)`, skip a missing or non-materialized one. One file.
- **Mac and #1365:** the mac renderer stamps each card layer with its document index (`apps/slate-mac/Sources/SlateMac/Canvas/CanvasRendererView.swift:338-340`, `cardLayer.zPosition = CGFloat(documentIndex)`). #1365 is not about card order. It asks whether coloured connections and label chips paint above or below the cards, and the owner answered it on 2026-10-19 (RD3: coloured connections beneath the cards, chips above; delivery is plan 43's PR 33). Windows already draws every connection beneath the cards. The Windows fix does not depend on #1365. #1365's answer matters only to #1721's chip option (a chip layer would go above `_cards`).
- **Failing test first:** `tests/CanvasRendererPeerTests.cs` (it has the `Board` harness), new fact `TheBoardPaintsCardsInSceneOrderSoAGroupIsUnderItsMembers`. It needs a way to read the order: either extract a pure `PaintOrder(state)` the loop uses, or walk `_cards.Drawing` and read the rectangles in order. Use at least eight overlapping nodes and assert the whole sequence equals `SceneNodes` order. With two or three nodes the fact passes by luck on some runs, so it could not be "seen failing".
- **Size:** S.
- **Lanes:** windows. Hosted fact only; no FlaUI shell gate.
- **Screen-reader work:** none (pixels only; peers and hit-testing already use scene order).
- **Owner decision needed:** none.
- **Shares files with:** #1722 and #1721 (`CanvasRendererView.cs`).

---

## #1722 — a UIA client's first search for an off-window board item returns null

- **Verdict:** stands. Not reproduced here (nothing was run); the code the issue names is unchanged.
- **Current state:**
  - `src/Canvas/CanvasRendererPeers.cs:142-160` `IItemContainerProvider.FindItemByProperty`: `_view.RealizePeer(found) is { } peer ? ProviderFromPeer(peer) : null` (`:157-159`).
  - `src/Canvas/CanvasRendererView.cs:842-854` `RealizePeer` mints the peer with `PeerFor` and calls `Realize`. `Realize` (`:895-918`) commits the retained set and the pan to the engine and returns.
  - The engine builds off the dispatcher and installs later (`CanvasPresentationEngine.cs:245-261`, `Install` `:301-328`). Only the install runs `RefreshAutomationChildren` (`CanvasRendererView.cs:222`, `:252-266`), which is what puts the new peer in the board's child list.
  - The board peers derive from `AutomationPeer` directly (`CanvasRendererPeers.cs:173-175`), so nothing connects them to a parent except being listed by the board.
  - The unit facts already know the provider can be null: `tests/CanvasRendererPeerTests.cs:236-239` ("with no UIA client listening the provider it hands back may be null, so the fact holds the peer the search minted") and `tests/CanvasNavigatorTests.cs:5422` discards the return value. No fact asserts the returned provider.
  - The FlaUI journey `CanvasSurfaces_VisualBoardPeersAndZoom_AreClean` (`tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.cs:9312`) never calls ItemContainer.
- **Cause:** knowable by reading, with one WPF fact I could not check in this repo. `AutomationPeer.ProviderFromPeer` returns null for a peer WPF cannot find connected under the automation root: it walks the root's cached child lists, and the new peer is not in the board's list until the install (unverified: WPF `ElementProxy.StaticWrap` / `ValidateConnected`). So the null is made by WPF inside Slate's process, before UIA core sees anything; the issue's guess ("UIA core seems to drop a provider") is one step too late. The repo already works around the same rule elsewhere: `src/EditorHyperlinks.cs:248-249` calls `WpfEditorPeerConnection.Connect(this, Parent)` and then `ProviderFromPeer(this)`; `Connect` is reflection over WPF's `TrySetParentInfo` (`EditorHyperlinks.cs:335-358`). Stock WPF item containers do the same for a virtualized placeholder (unverified).
  - No separate diagnosis chunk is needed. The failing FlaUI fact is the confirmation: if connecting the peer makes the first call return the element, the cause is proven.
- **Fix shape:** in `FindItemByProperty`, connect the minted peer to the board (`WpfEditorPeerConnection.Connect(peer, this)`) before `ProviderFromPeer`. Files: `CanvasRendererPeers.cs`; possibly move `WpfEditorPeerConnection` out of `EditorHyperlinks.cs` since two subsystems would use it. A census pins the reflection contract already (`WpfInternalsResolved`, `EditorHyperlinks.cs:345`).
  - **Sibling of the same shape (unverified, read only):** `ISelectionProvider.GetSelection` (`CanvasRendererPeers.cs:131-138`) returns `[ProviderFromPeer(peer)]` for the selected card. `PeerFor` mints a peer for any card in the population, windowed or not (`CanvasRendererView.cs:756-797`), so a selected card outside the window would give an array holding null. Check it in the same PR.
- **Failing test first:** FlaUI, in `tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.cs` beside the board journey: new fact `CanvasSurfaces_TheFirstNameSearchReturnsAnOffWindowItem`. Fixture: a canvas with a card at x = 20000 and a labelled edge between two far cards. From `CanvasVisualBoard`, one `ItemContainer.FindItemByProperty(null, Name, "Far One")` call returns a non-null element with that name; the same for the edge's phrase. A hosted fact cannot see this: without a client the board peer has no window handle and `ProviderFromPeer` is null either way.
- **Size:** S (the fix is a few lines; the FlaUI fact is most of the work).
- **Lanes:** windows, and the FlaUI shell gate is involved (the new fact lives there).
- **Screen-reader work:** none required. NVDA does not use ItemContainer. A Narrator spot check is optional.
- **Owner decision needed:** none.
- **Shares files with:** #1721 (`CanvasRendererPeers.cs`), #1838 and #1724 (`CanvasRendererView.cs`, the peer tests).

---

## #1721 — a labelled edge's UIA bounds

- **Verdict:** stands as a latent risk, not a present failure; the remedy the issue proposes is a visible feature (decision). One detail is misdescribed.
- **Current state:**
  - An edge's placement is its endpoints' union: `src/Canvas/CanvasPeerTopology.cs:172-176`, `EdgeUnion` `:205-215`.
  - Every board peer reports its placement as its bounds: `src/Canvas/CanvasRendererPeers.cs:238-253`.
  - The board draws no labels: `DrawEdges` (`CanvasRendererView.cs:326-356`) draws lines only. Contract 34 D11 already names "edge chips (base 10)" as a text run (`docs/plans/34_canvas_contracts.md:6596-6598`), so the contract expects chips one day.
  - The hit test tests cards before edges and clips to the board: `CanvasRendererPeers.cs:72-91`.
  - The risk is a recorded accepted-risk row: `docs/plans/34_canvas_contracts.md:6514` ("Accepted risk: #1721 ... The follow-up reports a drawn label chip's rect instead"). Closing #1721 closes that row.
  - **Correction to the issue:** it says no axe scan of the board runs after a zoom-in, and proposes adding one after Ctrl+=. One already runs: the journey zooms at `ShellAccessibilityTests.cs:9387` and scans at `:9438`, with the edge peers present (WB-26 added the edge to that scan). What does not exist is a scan in the covering state: a long diagonal edge zoomed in until its union contains the board. `sample.canvas` after one zoom step is not that state.
- **Cause:** by design of WB-26 (the union is the one rect, used for windowing and for bounds).
- **Options:**
  - **A. Draw label chips, bounds = the chip (the issue's proposal).** A fourth drawing layer above `_cards` (RD3 puts chips above cards), chip fill and text tokens with their contrast rows, the text-scale census row D11 asks for, a chip rect on the placement or computed in the peer, `GetBoundingRectangleCore` for edges, the covering-state axe scan, the contract row. Files: `CanvasRendererView.cs`, `CanvasRendererPeers.cs`, `CanvasPeerTopology.cs`, theme XAML, `ThemeTokenContrastTests`, the FlaUI journey. Size M. It changes what a sighted user sees on every board with a labelled edge, with no design in `design/windows` checked here (unverified whether one exists).
  - **B. Bounds only, no drawing.** The edge peer reports a small rect centred on the path's midpoint (where a chip would sit); the topology keeps the union for windowing and realization. Files: `CanvasRendererPeers.cs`, one FlaUI scan with a covering fixture. Size S. A magnifier or Narrator's highlight then boxes the middle of the line. Touch or mouse exploration finds the edge only near its midpoint, where today any uncovered point inside the union answers it.
  - **C. Leave it as the recorded risk** and move the chip to the canvas milestone.
- **Failing test first:** FlaUI, `ShellAccessibilityTests.cs`: new fact or a new stage of the board journey, on a fixture canvas with one long diagonal labelled edge, zoomed in until the union contains the board, then `AssertAxeClean`. It should fail on `BoundingRectangleCompletelyObscuresContainer` today (unverified: nothing was run, and whether axe flags an offscreen-clipped union exactly as the issue expects is the first thing this fact tells us). Plus a hosted fact `ALabelledEdgesBoundsAreItsMidpointNotItsUnion` in `tests/CanvasRendererPeerTests.cs`.
- **Size:** S for B, M for A.
- **Lanes:** windows, and the FlaUI shell gate is involved.
- **Screen-reader work:** none for B (no name or speech changes). NVDA spot for A (object navigation over a chip).
- **Owner decision needed:** yes. "A labelled edge's bounds: draw label chips now (visible, M), report the midpoint without drawing (S), or leave the recorded risk for the canvas milestone?" Recommended: B here, and note in the contract row that the drawn chip stays with the canvas milestone. B closes the register row without a feature arriving under a defect's name.
- **Shares files with:** #1722 (`CanvasRendererPeers.cs`), #1838 (`CanvasRendererView.cs` if A).

---

## #1724 — a deselect made while an edit is installing is undone

- **Verdict:** stands. Not reproduced here, but the tree itself records it as live: `tests/CanvasRendererPeerTests.cs:430-435` turns Follow Selection off in one theory row because "installing the pending edit brings the seat back (#1724)".
- **Current state (the parts the cause must be among):**
  - A deselect: `SelectNode(null)` sets `Selection.Selected = null` (`src/Canvas/CanvasDocumentViewModel.cs:1952-1963`), the change publishes the intent (`PublishIntent`, `:1738-1782`, `WithSelectedIntent(null)` at `:1753`) and applies it at once (`:1781`).
  - Selection is written back from a publication in one place only: `PublishReady` (`:1840-1855`). It runs only when the applied population object changed (`Apply`, `:1672-1681`). Its first arm seats `unit.ResolvedSelection` when the publication's intent differs from the previous one. Its second arm seats `unit.ResolvedSelection`, else the first outline row, whenever nothing is selected.
  - A mutation's refresh resolves its seat outside the publish gate and then stamps it: `CanvasMutationFunnel.cs:600-606` reads `slot.Current.SelectedIntent`, `CanvasLoadPipeline.cs:219-225` calls it and then republishes, and `CanvasLeaseTransfer.cs:281-293` writes `.WithResolvedSelection(seat)` and `.WithSelectedIntent(seat)` over whatever the snapshot holds by then. With the funnel on a worker (production), a deselect that lands between the read and the stamp is overwritten. This fits the issue's "likely area". In the issue's repro the document is synchronous (`tests/CanvasRendererPeerTests.cs:945`), so this window cannot be what that repro hits.
  - The board's install path writes no selection: `OnStateInstalled` (`CanvasRendererView.cs:215-235`).
  - The outline does not clear its own selected row on a deselect: `ApplySelectionCore` (`src/Canvas/CanvasOutlineView.cs:1120-1138`) returns at a null selection and leaves `_selectedRow.IsSelected` true. A later container realization could echo that row back through `OnTreeSelectionChanged` → `model.SelectNode(line.Id)` (`:1193-1218`). Unverified that this happens with the Visual surface active.
- **Cause:** unknown — needs a diagnosis step first. I could not find, by reading, the write that restores `a` in the synchronous repro. Two candidates are above. The diagnosis is small: write the issue's five steps as a hosted fact and capture the stack at the `CanvasSelection.Selected` setter (`src/Canvas/CanvasSelection.cs:33-43`) when the value returns.
- **A second question the fix will meet:** `PublishReady`'s fallback arm (`:1851-1855`) seats the first outline row whenever nothing is selected and a new population applies. The issue's control ("deselecting before the edit: the edit seats `near` ... looks intended") is this arm: `near` is the first row of that fixture, not only the connection's card. So today no deselect survives any edit. "The deselect stands" needs a rule for when the first-row fallback applies (a load whose seat was lost) and when it does not (a mutation that declares `KeepSelection` with nothing selected). That is contract A12's wording and may want a decision row.
- **Fix shape:** depends on the diagnosis. Likely one of: resolve the mutation's seat inside the publish transform, from the snapshot it stamps (`CanvasLeaseTransfer.Republish`, `CanvasLoadPipeline.RefreshAfterMutation`); clear the outline's selected row on a null selection (`CanvasOutlineView.ApplySelectionCore`); and narrow `PublishReady`'s fallback. Files: `CanvasDocumentViewModel.cs`, `CanvasLeaseTransfer.cs`, `CanvasLoadPipeline.cs`, `CanvasOutlineView.cs`.
- **Failing test first:** `tests/CanvasRendererPeerTests.cs`, new fact `ADeselectMadeWhileAnEditInstallsStands`: the issue's steps 1-5, asserting `Selection.Selected` is null after the board installs the applied population and after a further settle. Then remove the Follow Selection workaround at `:430-436`. If the worker-funnel window is real, a second fact in `tests/CanvasMutationFunnelTests.cs` holds the refresh between its seat read and its publish and deselects there.
- **Size:** M (S if the diagnosis finds one writer).
- **Lanes:** windows. Hosted facts; no FlaUI shell gate.
- **Screen-reader work:** none (the UIA `RemoveFromSelection` door is not one NVDA or Narrator uses in normal reading; unverified).
- **Owner decision needed:** possibly one, after the diagnosis: "With nothing selected, does an edit seat the first row (today) or leave nothing selected?" Recommended: leave nothing selected after a mutation; keep the first-row seat for a load.
- **Shares files with:** #1722 and #1838 (`CanvasRendererPeerTests.cs`); nothing else in this cluster touches the document or the funnel.

---

## #1694 — a UI Automation pattern call acts under the seal

- **Verdict:** stands exactly as recorded. It is an accepted risk the owner recorded on 2026-10-29, not an unknown defect: `docs/plans/28_palette_contracts.md:379-393` (PR-6), pinned by the four facts in `tests/UiaPatternProbeTests.cs:28-73`.
- **Current state:**
  - The seal is read in one place: `MainWindow.SealTakes` (`src/MainWindow.Seal.cs:97-113`), and once more for the external-activation hold (`src/MainWindow.xaml.cs:292-296`). `ShellSealAdmissionCensus.TheSealIsReadOnlyByTheAdmission` pins that (`tests/Censuses/ShellSealAdmissionCensus.cs:134`).
  - No `ShellSeal.IsHolding` or `SealHolds` exists (the spec's planned reader was never built).
  - Slate's own peers with action patterns: canvas card and edge (`src/Canvas/CanvasRendererPeers.cs:308-473`), the board's ItemContainer and each peer's Realize (`:142-160`, `:255-259`, both pan the board), graph node and tier peers (`src/Graph/GraphDiagramPeers.cs:78`, `:190`), connections rows (`src/Graph/ConnectionsLeafView.cs:171`, `:231`), editor links (`src/EditorHyperlinks.cs:207`), `CheckMenuItem`'s toggle (`src/CheckMenuItem.cs:41`). Three of them answer `IsEnabledCore() => true` unconditionally (`CanvasRendererPeers.cs:265`, `GraphDiagramPeers.cs:140`, `:217`).
  - Stock controls in the shell's XAML, by tag count: Button 118, MenuItem 134, TextBox 31, ListBox 22, ComboBox 7, CheckBox 6, RadioButton 6, Expander 5, ToggleButton 3, TabControl 1, Hyperlink 1, plus the code-built ones (not counted).
- **What contract 28 I3 literally requires:** `docs/plans/28_palette_contracts.md:899`: the window's one admission check "takes every key, key release, text input, pointer press, wheel turn, mnemonic and the double-click WPF rebuilds from a taken press before any route in the shell sees it". Every item is an input the window routes. A pattern call is not one, and I3 does not require a guard for it. PR-6 says so and names #1694 as what closes the row. Contract 40 AR-48 (`docs/plans/40_nvda_matrix_remediation_contracts.md:74`) lists UIA pattern calls first among the four paths the seal "deliberately does not govern".
- **How far the exposure reaches:** the seal holds while a palette command runs or a modal loop is over the shell (`src/CommandPaletteViewModel.cs:496`). A pattern call is delivered on the UI thread, so it arrives only while that thread pumps: under a modal prompt, or in a command's own nested frame, which AR-48 says no shipped command pushes (unverified here). So the real case is: a prompt is up, the reader leaves the prompt with object navigation or a voice command, and activates a shell control beneath it. The four probe facts model that with `ComponentDispatcher.PushModal()`.
- **Remedies in WPF, and what each costs:**
  1. **A guard in every peer (the issue's "done").** Stock WPF gives no seam between the call and the action, so every interactive control type needs a Slate subclass whose peer reads the seal, and item rows need the containers' item peers replaced (a list or tree row's peer is made by its parent's peer). About 330 XAML sites, a dozen control types, AvalonEdit's own peers, a census forbidding the stock types, four flipped probe facts and more for the other patterns (Toggle, Scroll, ScrollItem, RangeValue, Window, VirtualizedItem, ItemContainer, and `SetFocus`, which is not a pattern at all but selects a tree row). Size L, a week or more. It replaces the peer of nearly every control a screen reader touches, so it owes a full NVDA pass and a Narrator pass over the whole shell.
  2. **Guard only Slate's own peers.** Size M. The owner's own record already rejects this as inconsistent (buttons, menu items and text fields stay open).
  3. **`IsEnabled = false` on the shell's content root while sealed.** Stock peers refuse on their own when their element is disabled (unverified: WPF's `ButtonAutomationPeer.Invoke`, `SelectorItem`, `TreeViewItem` and `TextBox` peers throw `ElementNotEnabledException`). Cost: every control reads "unavailable" and greys out for the length of every palette command and every prompt; a control that holds the keys when it is disabled loses them, which is the class plan 47 sweeps (#1671, #1866); Escape on an open sealed palette must keep working inside the disabled tree; Slate's peers that always answer enabled must be guarded anyway. Limiting it to the modal-loop cause avoids the focus loss (the prompt has the keys) but still greys the shell under every prompt and changes what a reader hears there. Size S-M in code, with a real risk of new "unavailable" defects.
  4. **Refuse in each command's `CanExecute`.** The design already rejects a gate on execution (`src/MainWindow.Seal.cs:57-65`: the command the palette runs executes while sealed, so a gate there refuses the palette's own work). It also disables the buttons (the same "unavailable" cost) and does nothing for Select, Expand or SetValue.
  5. **Leave PR-6 as a standing exclusion.** No code. The contract row loses its "the guard is #1694, which closes this row" promise by an appended owner note; the probe facts keep pinning the behaviour. Size S (docs).
- **Is it a plain fix?** No. It is fully a design question for the owner: how far the seal reaches for assistive technology, and whether an L chunk that re-peers the whole shell is worth closing a path that needs a reader to walk out of a prompt.
- **Failing test first (if option 1 is taken):** flip the four facts in `tests/UiaPatternProbeTests.cs` to assert refusal, each with the mutation the issue names, and add one per further pattern; a new census fact in `tests/Censuses/ShellSealAdmissionCensus.cs` that no stock interactive type appears in the shell's XAML or code.
- **Size:** L for option 1; S for option 5.
- **Lanes:** windows. Option 1 touches the FlaUI shell gate (axe and every journey read the new peers).
- **Screen-reader work:** NVDA full and Narrator for option 1 or 3. None for option 5.
- **Owner decision needed:** "Does the seal reach UI Automation pattern calls? Build the guard for every control (L, re-peers the shell), or make PR-6 a standing exclusion and close #1694 as won't-do?" Recommended: close as won't-do and keep PR-6, with its closing sentence amended by the owner's recorded call. If the owner wants prompts hardened, ask for a one-day probe of option 3 limited to modal loops before committing to option 1.
- **Shares files with:** #1695 (`MainWindow.Seal.cs`, `ShellSeal.cs`, `ShellSealAdmissionCensus.cs`, contract 28); option 1 would touch every XAML file other chunks edit.

---

## #1695 — touch and stylus input is not taken by the seal

- **Verdict:** stands as an unproven gap. The missing subscriptions are verified; whether anything acts through them is not.
- **Current state:**
  - The window's gated routes are the six preview attributes at `src/MainWindow.xaml:20-25`; the handlers are `src/MainWindow.xaml.cs:1101` (key down) and `src/MainWindow.Seal.cs:115-123` (the other five).
  - `ShellSeal.Admit` subscribes the same six and the access-key gate: `src/ShellSeal.cs:52-62`. Its one caller is `src/Reading/ReadingTableGrid.cs:49`.
  - The census's list is the same six: `tests/Censuses/ShellSealAdmissionCensus.cs:46-47` (`RequiredGates`).
  - A search of the whole shell source and XAML for `TouchDown`, `TouchMove`, `TouchUp`, `StylusDown`, `StylusMove`, `StylusUp`, `Manipulation*`, `IsManipulationEnabled`, `PanningMode` and `Stylus.` finds nothing. So the shell declares no touch handler of its own, and nothing sets `PanningMode`. Anything that acts on touch is a stock control doing so through its default style or class handling.
- **Cause:** by reading: the admission predates any touch consideration. What remains unknown is the effect. WPF promotes an unhandled touch to mouse events, which the seal takes; a `ScrollViewer` whose `PanningMode` is not `None` pans from manipulation events without that promotion (unverified WPF behaviour; and which shell lists inherit a theme style that sets `PanningMode` is unverified).
- **Fix shape:** the issue's own two steps.
  1. A hosted probe: a test `TouchDevice` subclass reports a down and a move on a sealed shell list and on a sealed reading table, and records what moves (scroll offset, selection, focus).
  2. If anything acts: add `PreviewTouchDown`, `PreviewTouchMove`, `PreviewTouchUp`, `PreviewStylusDown`, `PreviewStylusMove`, `PreviewStylusUp` (and the stylus system gesture, if the probe shows it matters) to `MainWindow.xaml:20-25` with one-line handlers in `MainWindow.Seal.cs`, the same in `ShellSeal.Admit`, and extend `RequiredGates`; `TheOwnedWindowGuardHoldsEveryRouteTheShellsWindowHolds` (`ShellSealAdmissionCensus.cs:524`) then forces the owned window to match. Append the routes to I3's sentence (`28_palette_contracts.md:899`). If nothing acts, the probe stays as a pinning fact and I3 gains "touch is promoted to the pointer routes".
- **Failing test first:** `tests/ShellOwnedWindowProbeTests.cs` gains `touch` and `stylus` rows in `AnOpenReadingTableUnderTheSealTakesEveryShellRoute`, and a new fact `ASealedTouchOnAShellListPansNothing` in `tests/CommandPaletteTests.ShellKeys.cs` beside the sealed-press witnesses. Each asserts the sealed input leaves the observable alone and the unsealed one changes it.
- **Size:** S-M (the probe harness is the work).
- **Lanes:** windows. Hosted facts and a census; no FlaUI shell gate.
- **Screen-reader work:** none.
- **Owner decision needed:** none if the routes are taken, which I recommend: the wheel is already taken so "nothing pans or zooms", and touch panning is the same effect. Recording touch as outside the seal beside AR-49 instead would need the owner.
- **Shares files with:** #1694 (the seal files, the census, contract 28).

---

## #1665 — DECISION: does the editor popover join ModalSurface?

- **Verdict:** not a defect (decision). The defect it came from, F6 leaking past the popover (#1281), is delivered.
- **Current state:**
  - The ring asks about the popover and nothing else does: `src/MainWindow.ShellRegions.cs:19-20` and `RingIsModal` `:35-36`, with the focus walk at `:42-54`. The remark at `:25-34` records that the popover is deliberately not a `ModalSurface`.
  - `ModalSurface` is the enum at `src/ModalSurfaces.cs:38`; `OpenModalSurface` is `src/MainWindow.Palette.cs:171`.
  - **The issue's inventory has grown since it was filed.** There are eight admission tables now, not seven: `DecideSettingsOpen` was added (`ModalSurfaces.cs:429`; the others at `:213`, `:284`, `:326`, `:358`, `:381`, `:409`, `:456`). `OpenModalSurface` has about 35 read sites, not the dozen listed. Corrected lines for the ones the issue cites: focus restore `MainWindow.Citations.cs:342`, `MainWindow.MoveTo.cs:109`, `MainWindow.Palette.cs:403` (was 314), `MainWindow.Search.cs:217` (was 223), `MainWindow.Templates.cs:323`; trash progress `MainWindow.Trash.cs:23`, `:59`; the canvas overlay probe `MainWindow.xaml.cs:157` (was 136); the rescan deferral and watcher hold `MainWindow.xaml.cs:181` and `:187` (was 160), feeding `VaultLifecycleViewModel.Rescan.cs:1154` (was 1093) and `VaultLifecycleViewModel.cs:314`; the presentation observer `MainWindow.ShellRegions.cs:261` (was 220) and `:977` (was 883). Sites the issue does not list: `MainWindow.AppMenu.cs:42`, `MainWindow.Settings.cs:20`, `:72`, and `MainWindow.xaml.cs:109`, `:589`, `:622`, `:633`, `:653`, `:783`, `:1013`, `:1119`, `:1341`, `:1462`, `:1842`, `:1888`, `:2436`, `:2458`.
- **The decision, in one sentence:** should an open editor popover count as a modal surface for everything that asks (admission of the palette, Quick Open, search and the sheets; the menu; an activation rescan; the watcher's sentence), and if so, only while focus is inside it?
- **Options:**
  - **No.** Close #1665. Append one line to contract 40's WB-5 section that the popover is not a modal surface by the owner's call. Size S (docs only).
  - **Interruptions only.** Extend the one function that feeds the rescan deferral and the watcher's hold (`MainWindow.xaml.cs:181`) to also answer true while focus is inside an open popover. A reader in the popover is then not talked over by a watcher sentence or republished under. Admission, the menu and focus restore are untouched. Size S-M: one function, two hosted facts, an NVDA spot check.
  - **Yes, in full.** A new enum member, an explicit arm in each of the eight tables, a reviewed answer at each of the ~35 sites, the four `ModalSurfaceTests` censuses and the XAML-order census extended to a second XAML file. Size M-L. NVDA spot checks for the menu and each admission.
- **Failing test first:** only for the second and third options: `tests/ModalSurfaceTests.cs` rows per table, and a hosted fact that a watcher run with focus inside the popover is held (`tests/FilesRegionLandingTests.Rescan.cs` has the watcher harness).
- **Size:** S / S-M / M-L as above.
- **Lanes:** windows. The full option touches the FlaUI shell gate (the popover journey `EditorPopover_F6AndShiftF6StayInsideIt`).
- **Screen-reader work:** none for "no"; NVDA spot otherwise.
- **Owner decision needed:** the sentence above. Recommended: no, close as won't-do. No symptom is reported, the inventory has only grown, and the open question in the issue's own comment (does an open popover count while focus is elsewhere?) has no good answer for the admission tables. If the owner has heard a watcher talk over the popover, take "interruptions only".
- **Shares files with:** nothing else in this cluster. The full option would collide with any chunk editing `MainWindow.xaml.cs` or `ModalSurfaces.cs`.

---

## #1670 — a Reveal in File Tree into an unlisted folder

- **Verdict:** stands. A defect a plain fix closes; no owner decision.
- **Current state:**
  - `src/FilesSidebarViewModel.SurfaceNotes.cs:67-82` `SelectPathFromSurface` stores `_pendingSurfaceSelectPath`, counts the request, expands the ancestor folders that are already rows, and tries to seat (`:81`).
  - `ConsumePendingSurfaceSelection` (`:85-100`) has exactly two callers: that line, and `ApplyTreeRefresh` (`src/FilesSidebarViewModel.TreeOperations.cs:649`).
  - Neither folder load seats it: `LoadChildrenSynchronously` publishes at `src/FilesSidebarViewModel.ChildExpansion.cs:213` and the asynchronous load at `:307`; neither calls the consume.
  - A mutation that names a row drops a waiting Reveal: `DropWaitingReveal` (`src/FilesSidebarViewModel.FileManagement.cs:598-604`), called from `RequestInlineRenameAt` (`:582-586`) and `RequestSelectionAt` (`:622-627`). The two trash flows read the Reveal count at their gesture (`src/FilesSidebarViewModel.cs:1980`, `:2364`).
  - Nothing drops a waiting Reveal when the reader selects another row, and a mutation that refreshes without naming a row (`NoteLanded`, `SurfaceNotes.cs:50-55`, calls `Refresh()` only) lets its publication seat the Reveal. Both are as the issue and its comment say.
  - The one caller of `SelectPathFromSurface` is the graph's Reveal (`src/VaultLifecycleViewModel.cs:1658`).
- **One more case, found by reading:** the ancestor loop at `SurfaceNotes.cs:72-80` runs once, over the rows that exist at the request. For a note two unlisted levels down, the first folder's load lists the second folder collapsed, and nothing expands it. Seating from the folder's own load must also expand the next ancestor, or a deep Reveal still waits.
- **Cause:** verified by reading: the consume is wired to the whole-tree publication only, and the pending Reveal has no ordering against newer intents except the three mutation paths WB-6 added.
- **Fix shape:**
  1. Both folder-load publish points call a seat step that expands the next unlisted ancestor of the pending path or, when the row is there, seats it (`ChildExpansion.cs:213`, `:307`).
  2. One ordering for the three intents (a Reveal, a mutation's named row, the reader's own selection): a counter or a single "newest intent" slot, so the reader's selection and a row-less mutation's refresh drop an older Reveal, and a Reveal asked while a mutation's publication is pending is not overridden by the older mutation. Files: `SurfaceNotes.cs`, `FileManagement.cs`, the `SelectedNode` setter in `FilesSidebarViewModel.cs`, `TreeOperations.cs:645-650` (the order of the three consumes).
- **Does it share the reconcile code #1668 and #1669 would change?** Not the reconcile itself: #1670 touches nothing in `FilesSidebarViewModel.RescanReconcile.cs` (`ReconcileLevel`, `RowsThatStay`) and nothing in `ResortMaterializedTree`. It does share `ApplyTreeRefresh` (`TreeOperations.cs:620-650`) and the selection plumbing (`SelectedNode`'s setter, `SelectOnBothHalves`, the pending-intent fields) with #1669, which changes how a publication treats the selection. So #1670 and #1669 must not run in parallel, and #1670 goes first: its "newest intent wins" ordering is what #1669's list-selection provenance would build on. #1668 is independent of #1670 in code but edits `FilesSidebarViewModel.cs` too, so it cannot share a wave with either.
- **Failing test first:** `tests/FilesRegionLandingTests.Publication.cs` (WB-6's partial; `ADuplicateAfterAWaitingRevealSeatsTheCopyAndDropsTheReveal` and `ARevealAskedDuringATrashSeatsWhenTheTrashPublishes` are the neighbours). New facts, each asserting both halves of the selection, exactly one focus change, and what opens and is announced: `ARevealIntoAnUnlistedFolderSeatsWhenTheFolderLists` (asynchronous load), the same for the synchronous load, `ARevealTwoUnlistedFoldersDownSeats`, `TheReadersOwnSelectionDropsAWaitingReveal`, `ARowlessMutationsPublicationDoesNotSeatAnOlderReveal`, `ARevealAskedWhileAMutationPublishesWins`.
- **Size:** M.
- **Lanes:** windows. Hosted facts; one FlaUI journey for the Reveal through a collapsed folder is worth adding in the shell gate (`tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.FilesTree.cs`).
- **Screen-reader work:** NVDA spot (the Reveal lands on the note, one announcement, no second move).
- **Owner decision needed:** none.
- **Shares files with:** #1669 (`TreeOperations.cs`, `FileManagement.cs`, `FilesSidebarViewModel.cs`), #1668 (`FilesSidebarViewModel.cs`).

---

## #1668 — ENHANCEMENT: keep a row's container across a sort move and a pin

- **Verdict:** not a defect (decision). Both paths were accepted in WB-6's review (the spec's "Amended at implementation" section: "Accepted, and filed as #1668").
- **Current state:**
  - **Sort moves.** `ReconcileLevel` (`src/FilesSidebarViewModel.RescanReconcile.cs:216-295`) moves a kept row by `RemoveAt` (`:272-278`) and `Insert` (`:280-294`); the comment at `:269-270` says WPF then gives it a new container. `RowsThatStay` (`:303-333`) keeps the longest run already in order, anchored on the reader's rows; `ReaderRows` (`:394-408`) is the selected row, the tree's own selected row, and every folder they are in. Those never move.
  - **Pins.** `PinSelected` and `UnpinSelected` (`src/FilesSidebarViewModel.cs:2142-2170`) call `ResortMaterializedTree` (`:1430-1443`). With grouping off it calls `Resort` on the root and on every listed folder, and `Resort` (`:1445-1453`) clears the collection and adds every row back. With Group by date on, it calls `Refresh()` instead (`:1432-1436`), which is a publication and keeps rows.
  - The keys after a pin are pinned by `PinNoteRebuildsTheTreeUnderTheKeysAndTheyStayOnTheRow` (`tests/FilesRegionLandingTests.cs:429`).
- **What a screen-reader user loses today:**
  - **After a sort change:** nothing in ordinary use. The selected row and its folders keep their containers and elements. What is lost is a second position the reader left on some other row that the sort moved: NVDA's object-navigation cursor, Narrator's scan cursor, a braille display's review position. It falls back to focus or points at a row that is gone. WB-6's spec notes that a UIA peer, keyed by its item, can outlive the container WPF regenerates, so the element may survive even here (unverified which).
  - **After Pin or Unpin with grouping off:** every row in the tree is rebuilt, the row holding the keys included. The keys are then put back on the same row by the restore the fact above pins. The reader loses the keys for a moment and hears the row land again around "Pinned X." (unverified by ear: the exact speech, and whether the landing cuts the status off, needs an NVDA pass). This is on a common gesture, with the keys on the row.
- **The decision, in one sentence:** is rebuilding rows on a sort move and on a pin worth fixing now, in whole or only for pins?
- **Options:**
  - **Pins only, in memory.** Replace `Resort`'s clear-and-add with the reconcile's own move rule (the rows that stay are the longest run in order through the reader's rows), so the pinned row and everything not displaced keep their containers. Files: `FilesSidebarViewModel.cs`, reusing `RowsThatStay` from `RescanReconcile.cs`. Size S-M.
  - **Pins through the publication.** Make the ungrouped path call `Refresh()` as the grouped path does. Size S, but it lists from disk on the tree worker for a change that is only an ordering.
  - **Moves that keep the container.** Use `ObservableCollection.Move` in `ReconcileLevel` and `Resort`. Whether WPF's recycling tree keeps a realized container on a Move is unverified and needs a one-hour hosted probe before anything is planned on it. If it does, both halves close. Size M.
  - **Won't-do.** Record both in contract 40 R-5 (h) as accepted for elements too.
- **Failing test first:** `tests/FilesRegionLandingTests.Publication.cs`: `APinKeepsEveryRowsContainerAndPeer`, `AnUnpinKeepsEveryRowsContainerAndPeer`, `AReverseSortKeepsEveryExpandedSubtreesContainersAndPeers`, each holding containers and peers of every expanded subtree across the change. `PinNoteRebuildsTheTreeUnderTheKeysAndTheyStayOnTheRow` then changes its name and asserts no focus change at all.
- **Size:** S-M for pins; M for both.
- **Lanes:** windows. Hosted facts; the FlaUI shell gate only if a journey is added.
- **Screen-reader work:** NVDA spot (Pin and Unpin with the keys on the row: one sentence, no re-landing).
- **Owner decision needed:** the sentence above. Recommended: do the pin half here by the in-memory option, and run the Move probe inside the same chunk; take the sort half only if the probe shows Move keeps containers, otherwise close it as won't-do with a line in R-5 (h).
- **Shares files with:** #1669 (`RescanReconcile.cs`), #1670 and #1669 (`FilesSidebarViewModel.cs`).

---

## #1669 — ENHANCEMENT: keep the tree's rows while a list's row holds the selection; track the reader on an unselected row

- **Verdict:** not a defect by WB-6's scope (decision), but case 1 is the repository's own accessibility gate not holding in a reachable state.
- **Current state:**
  - **Case 1.** `PublishKeepingUnchangedRows` replaces the whole tree when the sidebar's selection is not a tree row: `src/FilesSidebarViewModel.RescanReconcile.cs:97-101` (`SelectedNode is { } selected && !InTree(selected)` → `RootNodes = fresh`). `InTree` is `src/FilesSidebarViewModel.FileManagement.cs:737-738`. A list's row is one of `FilterResults` or `DualPaneFiles` (`src/FilesSidebarViewModel.cs:824`). The comment at `RescanReconcile.cs:92-96` gives WB-6's reason (S2).
  - **Case 2.** `ApplyTreeRefresh` snapshots the sidebar's selection and the tree's own selected row and nothing else: `src/FilesSidebarViewModel.TreeOperations.cs:628-632`. The focused row is never read.
- **What a screen-reader user loses today:**
  - **Case 1.** While a filter result or a dual-pane row is selected, every publication rebuilds the whole tree: any rescan after a change outside Slate (a sync client, another editor) and any of Slate's own mutations. If the keys are in the tree at that moment (in dual-pane mode the tree is where the reader walks folders, with the selection sitting in the file list), they are moved to the re-bound selected row and NVDA reads that row. The reader did nothing; a file changed somewhere. Any second position held in the tree (object navigation, a scan cursor, braille review) is lost on every such publication. `AGENTS.md`'s gate says a surface a watcher can republish keeps the reader's element and focus; in this state it does not.
  - **Case 2.** After a delete lands the keys on the nearest surviving row without selecting it, a later publication that removes or rebuilds that row drops the keys to the tree's own landing (the selected row, else the first row), not to the next survivor. The reader hears a jump to an unrelated row. It needs two removals around the reader in a row, so it is rarer.
- **The decision, in one sentence:** should a publication keep the Files tree's rows while a filter result or a dual-pane row is selected, and follow a reader who is on an unselected row?
- **Options:**
  - **Both, here.** Keep a list selection's provenance across publications so the keep path can run; let the tree's own selected row resolve its keys by itself; have the window supply the focused Files-tree row through a probe so it is snapshotted and resolved like the selection. Files: `RescanReconcile.cs`, `TreeOperations.cs`, `FileManagement.cs`, `FilesSidebarViewModel.cs` (the setter, `SelectOnBothHalves`, `SelectSilently`), `MainWindow.ShellRegions.cs` (the probe and the landing). Size M-L. This is the subsystem where WB-6 stopped after three review rounds, with round 3's high finding created by round 2's fix; it needs a short design first and a Codex review.
  - **Case 2 only.** The focused-row snapshot and its landing. Size S-M. It leaves the larger loss.
  - **Case 1 only.** Size M.
  - **Won't-do**, or move to Milestone W.
- **Failing test first:** `tests/FilesRegionLandingTests.Publication.cs`, each over two consecutive publications, asserting container identity, both selection halves, exactly one focus change, and that nothing opens or is announced: `APublicationKeepsTheTreesRowsWhileAFilterResultIsSelected`, `APublicationKeepsTheTreesRowsWhileADualPaneRowIsSelected`, `TheKeysInTheTreeStayPutWhenAListsRowIsSelectedAndAFileChangesOutside`, `AReaderOnAnUnselectedRowLandsOnItsNearestSurvivor`. One FlaUI journey in `ShellAccessibilityTests.Rescan.cs` through `AssertAWatcherRunKeepsTheReadersRow` with dual pane on.
- **Size:** M-L.
- **Lanes:** windows, and the FlaUI shell gate is involved (the watcher journey).
- **Screen-reader work:** NVDA full on the Files tree (filter and dual pane, watcher runs, deletes); the tree is the busiest surface a reader has.
- **Owner decision needed:** the sentence above. Recommended: do both here, after #1670, with a design step first. If capacity forces a cut, keep case 1 and close case 2 as won't-do.
- **Shares files with:** #1670 (`TreeOperations.cs`, `FileManagement.cs`, `FilesSidebarViewModel.cs`), #1668 (`RescanReconcile.cs`, `FilesSidebarViewModel.cs`).

---

## #1842 — DESIGN FOLLOW-UP: a user-chosen accent from colours that clear contrast

- **Verdict:** not a defect (decision). A feature, recorded as the follow-up to the owner's DSD16 call.
- **Current state:**
  - The accent has three choices: `src/SlateAccent.cs:60-65` (Automatic, Slate gold, Windows accent). `RolesFor` is at `:81`; `TryApplyWindowsAccent` at `:102`.
  - The contrast gate the issue points at is not on `main`. `TextPairs`, `MarkPairs`, `FailedChecks`, `EveryCheckCanFail` and `AnAccentThatFailsAnyCheckFallsBackToGold` appear nowhere in the worktree. They exist only at commit 9edf4b6b, which is reachable from the remote branch `origin/claude/ds11-accent` and nothing else. If that branch is pruned, the starting point the issue names is gone.
  - Plan 46 records the calls: `docs/plans/46_windows_design_system_plan.md:176` (DSD14) and `:178` (DSD16).
- **The decision, in one sentence:** does plan 47 build an accent picker, or does it go to Milestone W's settings and theming wave?
- **Options:**
  - **Do here.** A picker design from the Claude Design project (the issue says `design/windows` has none), a set of colours each with its own light and dark steps, colour names a reader hears (which belong in core by the architecture rule), a fact over every offered colour in both themes, the Appearance row. Size L, and it needs the owner and a design before any code.
  - **Move to Milestone W** (Wave 8: settings and theming, #751-#756).
  - **Won't-do.**
- **Failing test first:** not applicable until a design exists; the issue names the fact (every offered colour passes every pair in light and dark).
- **Size:** L.
- **Lanes:** windows (and rust if the names are core's).
- **Screen-reader work:** NVDA full on the new control, when built.
- **Owner decision needed:** the sentence above. Recommended: move to Milestone W. Before the move, keep 9edf4b6b reachable (a tag, or the gate's code pasted into the issue), since the issue's starting point lives only on an unmerged branch.
- **Shares files with:** none in this cluster.

---

## #1848 — DESIGN FOLLOW-UP: the note header's compact width rule

- **Verdict:** not a defect (decision). The owner's DSD18 call was "keep this for now, and revisit it as a refinement later" (`docs/plans/46_windows_design_system_plan.md:180`).
- **Current state:**
  - `src/NoteHeaderView.cs:34` `CompactBelow = 520`; the test is `:183` in `OnRenderSizeChanged`.
  - The default window is 1120 wide: `src/MainWindow.xaml:12`. The default pane widths of 270 and 320 were not found by a quick search and are unverified; plan 46's DSD18 states the result (the editor column is about 520 at the default size).
  - The bar already has a measured rule for the folders: `ArrangeOverride` (`:205-226`) collapses them into "…" when they do not fit beside the name, and brings them back when their room is as wide as they needed (`_overflowing`, `_foldersNeed`, `:71-73`). So a measured threshold for the folders exists; the fixed 520 sits on top of it and also drives the switch's labels (`:444-448`).
- **The decision, in one sentence:** change the rule now (a measured threshold, or different defaults), or leave it for Milestone W's design work?
- **Options:**
  - **Measured threshold, here.** Let the existing overflow rule alone decide the folders, and keep a threshold only for the switch's labels, measured from the labels' width so larger text sizes get more room. Files: `NoteHeaderView.cs` and its tests. Size S-M. It changes which stops a reader meets at the default size (one stop per folder where today there is one "Folders" button), so it needs an NVDA spot check and the design's agreement.
  - **Change the default window or pane widths.** Size S, but it moves every user's first layout.
  - **Move to Milestone W's design work.**
- **Failing test first:** `tests/NoteHeaderViewTests.cs` (unverified name; the header's hosted facts): `AtTheDefaultWindowSizeTheHeaderShowsItsFolders`.
- **Size:** S-M.
- **Lanes:** windows.
- **Screen-reader work:** NVDA spot.
- **Owner decision needed:** the sentence above. Recommended: move to Milestone W. It is a design choice among three levers, the owner already deferred it once, and nothing is broken.
- **Shares files with:** none in this cluster.

---

# The cluster

## The seal's route table

"Taken" means `MainWindow.SealTakes` (`src/MainWindow.Seal.cs:97-113`) marks it handled before any route acts. The owned window is the reading table grid, admitted at `src/Reading/ReadingTableGrid.cs:49`.

| Route into the shell while sealed | Taken? | Where |
|---|---|---|
| Key down (chords, input bindings, menu mode, Alt, F10) | Yes | `MainWindow.xaml:20` → `MainWindow.xaml.cs:1101` (the check is its first statement). Owned window: `ShellSeal.cs:55` |
| Key up (the Apps key opens a context menu on release) | Yes | `MainWindow.xaml:21` → `MainWindow.Seal.cs:115`; `ShellSeal.cs:56` |
| Text input | Yes | `MainWindow.xaml:22` → `MainWindow.Seal.cs:117`; `ShellSeal.cs:57` |
| Pointer press and release (buttons, menu clicks, context menus) | Yes | `MainWindow.xaml:23-24` → `MainWindow.Seal.cs:119-121`; `ShellSeal.cs:58-59` |
| Wheel | Yes | `MainWindow.xaml:25` → `MainWindow.Seal.cs:123`; `ShellSeal.cs:60` |
| The double-click WPF rebuilds from a taken press | Yes | class handlers `MainWindow.Seal.cs:75-87`, `:127-133`, resolved through `ShellSeal.ShellOf` (`ShellSeal.cs:32-37`) |
| Mnemonics | Yes | `MainWindow.xaml.cs:170-171` → `MainWindow.Seal.cs:139-145`; `ShellSeal.cs:61`, `:67-73` |
| Commands routed from menus | Yes, through the routes above | a menu opens only by a key or a press. A menu already open when the seal begins is the popup row below |
| Escape on an open palette | Deliberately acts | `MainWindow.Seal.cs:104-110` (T9) |
| A second launch, a jump-list vault | Parked, not taken | `MainWindow.xaml.cs:292-296` (`ExternalActivationMustWait`); AR-48's addendum, WB-9 |
| Pointer motion (hover popups, a drag that began before the seal) | No, recorded | contract 40 AR-49. Handlers: `Graph/GraphDiagramView.cs:1235`, `Canvas/CanvasRendererView.cs:1463` |
| Input inside an open popup or context menu | No, recorded | AR-49 (a popup is its own window source) |
| Touch, stylus, manipulation (touch panning) | No; no subscription anywhere | #1695. Zero matches in the shell for any touch, stylus or manipulation API. Effect unverified |
| Drag and drop from another app | No; not listed in any contract | The shell declares no `AllowDrop` and no drop handler (zero matches; `Reading/ReadingSurface.cs:1546` only reads `IsDragDrop` on a paste event). The editor and stock text boxes accept dropped text by default (unverified). Under a prompt the shell's window is disabled and gets no drop (unverified); in a command's own nested frame it could. A new instance of #1695's class |
| UIA pattern calls: Invoke, Toggle, Value.SetValue, ExpandCollapse, SelectionItem, Scroll, ScrollItem, RangeValue, Window, and the canvas's VirtualizedItem.Realize and ItemContainer search (both pan the board) | No, recorded | contract 28 PR-6, contract 40 AR-48, `tests/UiaPatternProbeTests.cs`; #1694 |
| UIA `SetFocus` (not a pattern; a tree row selects itself when it takes focus) | No; not listed in any contract | part of #1694's class; unverified that it acts |
| Text a TSF service writes into a focused field (voice typing, handwriting) | No, recorded | AR-48; `MainWindow.Seal.cs:44-47` |
| Closing the window | No, deliberately | AR-48; `MainWindow.Seal.cs:62-63` |
| The maximize button's non-client clicks (Snap Layouts) | No, reviewed | `MainWindow.TitleBar.cs:87-131`, admitted by the census's hook inventory (`ShellSealAdmissionCensus.cs:488-497`) |
| Timers and watchers (rescans, reloads, debounces, announcements) | Not input; not gated | the seal is read only by the admission and the activation hold. Rescans and the watcher's sentence are held by `OpenModalSurface`/thread-modal instead (`MainWindow.xaml.cs:181-187`), not by the seal |

Two rows are in no register today: drag and drop, and UIA `SetFocus`. Whatever the owner decides for #1694 and #1695 should name them, so they are not found again one at a time.

## Recommended cut into PRs

1. **Canvas board (one PR): #1838, #1722, and #1721 if the owner picks the bounds-only option.** All three are in `CanvasRendererView.cs` and `CanvasRendererPeers.cs`, with facts in `CanvasRendererPeerTests.cs` and one FlaUI journey. Size S-M together. If the owner picks drawn chips for #1721, it becomes its own M PR after this one.
2. **Canvas selection (one PR): #1724.** Diagnosis first, inside the chunk. It lives in the document and the funnel, not the renderer, but it edits `CanvasRendererPeerTests.cs` (removing the workaround), so it follows PR 1 rather than running beside it.
3. **The seal's routes (one PR): #1695, plus the drag-and-drop row.** Probe, then gate or record. If the owner closes #1694 as won't-do, its contract note rides in this PR, with UIA `SetFocus` named in the same sentence. Codex review per the brief.
4. **#1694 as its own chunk only if the owner asks for the full guard.** L, last, alone in its wave: it touches nearly every XAML file.
5. **Files tree, three PRs in series: #1670, then #1669, then #1668.** #1670 first because its intent ordering is what #1669 builds on and both edit `ApplyTreeRefresh`. #1668 last because it changes the reconcile's move step, which #1669's facts hold containers through. None of the three can share a wave with another. Codex review on each per the brief.
6. **#1665, #1842, #1848:** decision rows only. With the recommended answers they produce one docs line for #1665 and two milestone moves.

## Risks

- **#1722's cause rests on a WPF internal I could not read here.** If connecting the peer does not make the first call succeed, the remaining suspect is UIA core, and the fallback is to answer the search only after the install (the provider returned from a continuation is not possible in a synchronous provider call, so that would mean realizing synchronously). The FlaUI fact settles it on the first run.
- **#1838's fact can pass by luck** if it uses few nodes. Eight or more, full-sequence equality.
- **#1721's axe expectation is unproven.** No scan has ever run in the covering state; the rule may or may not fire for a rect that extends past the board. Write the scan first.
- **#1724 has two questions inside it** (who restores the seat; whether a deselect should survive an edit at all). The second may need a decision row after the diagnosis.
- **#1694 option 3 would create the defect class the plan sweeps** (a control disabled while it holds the keys). Do not take it without the probe.
- **The Files tree's selection subsystem** stopped WB-6 after three review rounds. #1669 is the same code. Plan a design step and expect review rounds; do not size it from the diff.
- **#1668's Move option depends on unverified WPF behaviour.** Probe before planning on it.
- **#1842's starting code is on an unmerged remote branch only.** Pin 9edf4b6b before any branch clean-up.
- **#1665's inventory in the issue is stale** (eight tables, about 35 sites). A "yes" must be sized from the code, not from the issue.
