# CB-36, PR 2: Each labelled edge has a drawn chip, and the edge's bounds are the chip's

Issue: #1721 (this PR closes it). Size M to L. Lanes: windows (the app job) and the FlaUI shell gate. AT: the CB-36 row's NVDA spot check, by mouse tracking (Call 9). Wave: C13. Decisions: CD24 (2026-11-16: drawn chips, not the midpoint rectangle) and the owner's three answers on the design (2026-11-25, all as recommended): (1) a 1-pixel outline in `Slate.Canvas.EdgeBrush`; (2) a later chip covers an earlier one; (3) a cut-off chip's full label on pointer hover, in the board's existing tooltip. Two premises of that design were wrong (Call 1). Interlock: §6's canvas renderer row (CB-36, then CB-37) and the gate partial `ShellAccessibilityTests.CanvasBoard.cs`. Review: a red team (done, folded in below), then Codex until it approves.

Written 2026-11-26. Branch `claude/cb36-edge-label-chips`: 591b1e79 (the covering-state scan, test only) and 61b8f99b (origin/main 01b293b3, CB-36's first PR, merged in; the first PR's gate partial and `BoardChildNames` won, and this branch keeps only its journey and one evidence helper). Every file and line below was re-read at 61b8f99b. Bindings: the generated folder copied from the first PR's worktree (the same core; neither PR touches `crates/`). Statements still by reading say so.

`S/` below is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`; `G/` is `apps/slate-windows/tests/SlateWindows.AccessibilityTests/`.

**Files this PR edits.** Product: `S/Canvas/CanvasRendererView.cs` (the chip layer and its per-install cache, the hover and the tooltip subject), `S/Canvas/CanvasRendererPeers.cs` (the edge peer's bounds; the point walk; comments), `S/Canvas/CanvasPeerTopology.cs` (one doc comment that becomes false). Tests: `T/CanvasRendererPeerTests.cs`, `T/CanvasDocumentTests.cs` (the chip tooltip facts beside the card tooltip's), `T/ThemeTokenContrastTests.cs` (the chip rows), `T/Censuses/WcMatrixCanvasEvidenceCensus.cs` (the manifest), `G/ShellAccessibilityTests.CanvasBoard.cs` (the covering-state scan, and one pointer journey). Docs: `docs/plans/34_canvas_contracts.md` (the owner's recorded call appended) and `docs/plans/18_windows_port/w_c_matrix.md` (the canvas visual row). No XAML, no theme token, no `crates/` file, no binding.

**Overlap.** CB-21 and CB-35 edit none of these files. CB-37 follows on the renderer files and their tests.

**No sentence is added or changed.** The edge's name stays core's phrase. No `A11yEvent`, no chord or help line, no focus call.

## Goal

On the Windows visual board, every labelled connection that is one of the board's elements shows its label in a drawn chip on its line. The connection's UI Automation element reports the chip's rectangle as its bounds, so a magnifier or a reader's highlight boxes the label and an axe scan in the covering state is clean. A point on a chip answers the connection; a cut-off chip shows its full label on hover.

## Step 0 (measured)

**The covering-state scan, written first and seen to fail on b19c94ae** (`CanvasBoard_ALongLabelledEdgeZoomedInLeavesTheBoardAxeClean`, 591b1e79): "Start" (0, 0) and "Finish" (3000, 2400) joined by "spans", "Middle" (1500, 300), "Beside" (1900, 300); seat "Start", Down to "Middle", Shift+2, check the premise, axe.

| Runs | Result |
|---|---|
| 1 on main, without "Beside" | fails: `BoundingRectangleCompletelyObscuresContainer` on the connection, the only error (the connection's element 4864 by 3801 px over a 544 by 532 board) |
| 2 on main, with "Beside" | the same single error; "Beside", a windowed card wholly off the board, raises nothing |
| 2 on a reverted prototype of the bounds (a 60 by 16 rectangle at the line's midpoint, no drawing) | **clean**. The connection's element sat at y 3660 to 3709 physical px, below a board ending at y 1480: off the board, and below this desktop's screen. axe accepts an element wholly off the board and off the screen that reads on screen |

(The design and the scan's commit message said three runs without "Beside" and five in all; the logs hold one and two. Corrected here.)

The same prototype run also kept `CanvasSurfaces_VisualBoardPeersAndZoom_AreClean` (its `canvas-visual` scan, with `sample.canvas`'s three labelled edges) and the first PR's `CanvasBoard_TheFirstNameSearchReturnsAnOffWindowItem` green, in both runs.

**Hoverable, measured with the real pointer on this desktop** (a probe journey, 3 runs, untracked and deleted): a truncated card (a 200 by 60 card with a long title) shows its tooltip under the pointer, flush below the card (a 1 px gap). Moving the pointer onto the tooltip in 3 px steps, and in one jump, kept it open each time, so the board's existing machinery is hoverable as WPF orders the leave and enter events here. The red team's worry (major 6) is answered for cards; the chip's tooltip uses the same popup and is placed flush below the chip. The probe also showed a gate trap: FlaUI's `Mouse` and the rectangles must share one coordinate space, so the pointer journey touches `Mouse` before reading any rectangle.

**Colour pairs** (the theme tests' APCA; the red team recomputed the first four and measured the last two):

| Pair | Light | Dark |
|---|---|---|
| `Slate.Canvas.TextColor` on `Slate.SurfaceColor` (chip text; ungated today) | Lc 104.7 | 99.5 |
| `Slate.Canvas.EdgeColor` on `Slate.WindowBackgroundColor` (outline against the board) | 66.8 | 55.7 |
| `Slate.Canvas.EdgeColor` on `Slate.SurfaceColor` (outline against the chip) | 71.6 | 54.4 |
| `Slate.SurfaceColor` against `Slate.WindowBackgroundColor` | 0 | 0 |
| `Slate.Canvas.SelectionRingColor` on `Slate.SurfaceColor` (the ring now draws over chips) | 104.7 | 99.5 |
| `Slate.Canvas.EdgeColor` against the six fills and the group fill (chips over coloured cards) | 46.5 to 67.2 | |

`Slate.Canvas.EdgeColor` equals `Slate.BorderColor` in all three themes (light #767676, dark #AAAAAA, contrast WindowText).

**Windows today, re-read.** Layers `_edges`, `_cards`, `_ring` (`S/Canvas/CanvasRendererView.cs:54-56`). The board sits on the tab grid's `Slate.WindowBackgroundBrush` (`S/WorkspaceTemplates.xaml:688`; the design cited `:817`, the Markdown editor's). `BrushOf` answers `Transparent` in an unthemed host (`:386-391`). The tooltip (`:1387-1507`) has one subject, a node id; its pointer-on-tooltip branch returns the old subject without checking it is still truncated (`TooltipSubject`), and `UpdateTooltip` checks only that the card exists. `OnMouseMove` hovers `HitTest`'s card at `e.GetPosition(this)`; hover is not revalidated at an install or reset on a rebind. `HitTest` and `MenuTargetFor` are cards only. Every board peer reports its placement as its bounds (`S/Canvas/CanvasRendererPeers.cs`); an edge's placement is its endpoints' union (`S/Canvas/CanvasPeerTopology.cs:172-176`), and `:104-107` says that union "contains the straight path and its label chip". The point walk tests cards, then edges. Realization pans to contain the union when it fits the view, else the designated endpoint (`CanvasRendererView.cs`, `RealizationTarget`).

**The Mac (#371), by reading** (`CanvasRendererView.swift:466-581`, `:145-148`): chips at the midpoint of the side-to-side path; font `max(4, 10 × body/13 × zoom)`; width estimated from the character count, capped at `200 × zoom`; height `font × 1.6`; uncoloured card fill and card text, no outline; a tooltip on every chip, cut off or not; a chip for a whitespace-only label; and the covering order between overlapping chips is a Swift dictionary's insertion order, not document order (filed as #2069).

**What the change touches that is pinned today.** `APointOnAnEndpointCardIsTheCardNotTheEdge` (`T/CanvasRendererPeerTests.cs:258-280`: the union premise and its corner point), `APointBesideTheBoardIsNeverOneOfItsPeers` (`:287-323`: its point found through the union), `AnEdgeLongerThanTheViewportRealizesOnItsDesignatedEndpoint` (`:120-159`, if Call 4 is taken), contract 34's D5 hit rule (`docs/plans/34_canvas_contracts.md:6510`), TD-3's union sentence (`:7260-7262`), the #1721 row (`:6514`), the W-C matrix's canvas visual row (`w_c_matrix.md:58`) and its census (`T/Censuses/WcMatrixCanvasEvidenceCensus.cs:77-131`).

## Design

1. **A chip layer.** `_chips`, a fourth `DrawingVisual` between `_cards` and `_ring`: chips over cards (the owner's 2026-10-19 answer on #1365), the ring over chips. `DrawChips(state)` runs in `OnStateInstalled` with the other layers, so theme, text size and viewport changes redraw it in the same pass. A seam, `ChipsDrawingForTests`, hands out its drawing as PR 1's `CardsDrawingForTests` does.
2. **Which chips.** `SceneEdges` in order; one chip per edge that is a board peer (`NamedEdge`: core named it) and whose placement is `Materialized`. A chip is drawn exactly while its element is a child; windowing (the union) is unchanged. A whitespace-only or dangling edge has no chip, as it has no element.
3. **One layout per install, shared (D1).** `DrawChips` lays out each chip once and keeps the results in an immutable map keyed by edge id, tagged with the installed state it was built from. The edge peer's bounds, the point walk, the hover hit test and the tooltip read only that map, and only while its state is `_engine.Current`; otherwise they answer nothing. So the text size factor, which moves before the install that redraws, cannot make the bounds part from the pixels, and no query lays text out.
   - Centre: the midpoint of the line `DrawEdges` draws (the effective rectangles' centres), so a move or resize preview carries its chips. A self-loop's chip sits on its card's centre.
   - Text: `edge.Label` with every line break (CR, LF, CRLF, U+0085, U+2028, U+2029) and tab turned into a space and the ends trimmed, in the card typeface at `max(4, 10 × factor × zoom)` (D11's base 10; the Mac's floor of 4, so a chip at the 0.1 minimum zoom stays above axe's 25 px² size floor: by arithmetic about 20 by 6.4), one line, `CharacterEllipsis`, at most `max(0, 200 × zoom − 12)` wide.
   - Size: the drawn text's width plus 6 each side, `1.6 ×` the font size tall, centred on the midpoint.
   - Cut off: the normalized label is wider than the line allows (judged on the drawn text, so trailing spaces and line breaks no longer count).
4. **The look (answer 1).** Fill `Slate.SurfaceBrush`, a 1-pixel `Slate.Canvas.EdgeBrush` outline snapped to device pixels (a guideline set), text `Slate.Canvas.TextBrush`, corner radius 3; no new token; not dimmed by a filter, like its line. High contrast: window, window text, window text (`Slate.Contrast.xaml:5-8`, `:107-108`).
5. **Overlap (answer 2).** Chips draw in `SceneEdges` order, a later chip over an earlier one, on every run. Twin connections share a centre, so the later chip covers the earlier where they meet; both elements stay, each with its own chip's rectangle.
6. **Bounds and offscreen.** `CanvasEdgeAutomationPeer` reports its chip's rectangle through `ViewToScreen`; empty while not materialized. `IsOffscreen` stays "not materialized", the cards' rule; a chip beyond the board reads on screen (measured: axe accepts it, even off the screen; Call 3).
7. **Realization (Call 4).** Recommended: a realization pans to contain the union together with the chip when that fits the view (a short edge's long label can overhang its union), and otherwise to contain the chip, so the element a reader realized is in view (D4: a realization always scrolls into view). Today's fallback, the designated endpoint, moves to Invoke, which already pans to the card it seats.
8. **Point queries.** `GetPeerFromPointCore` tests the edges first (their chips, clipped to the board, last-drawn first), then the cards. A point on a chip answers the connection even over a card; a point on a card under no chip answers the card; a point on the bare line answers the board.
9. **Pointer.** Clicks stay as they are: `HitTest` and `MenuTargetFor` are cards only, so a left or right click on a chip acts on the card under it (the owner's design; Call 5). Hover reads chips first: `OnMouseMove` calls `HoverAt(e.GetPosition(this))`, which asks the chip hit test before `HitTest` and keeps the last pointer point; a seam, `PointerMovedForTests(Point)`, calls the same method.
10. **The tooltip (answer 3).** The subject becomes a `CanvasPeerKey` (a card and an edge may share an id). The order: the selection on a truncated card (the keyboard trigger, unchanged); the hovered cut-off chip; the hovered truncated card; the pointer on the open tooltip. Every branch passes one validity check at every install and every hover change: the subject still exists, is windowed and is still truncated or cut off. The chip's tooltip shows the full raw label, flush below the chip; it keeps the card tooltip's rules (hoverable, Esc through the surface rung, the popup gate's hidden board, modal surface and inactive window). There is no keyboard trigger for a chip: a keyboard user cannot seat a connection, and the label is in the element's name and the outline's connection rows (ID-9's edge-by-focus cell, recorded). At each install the hover is checked again at the last pointer point, so a chip or card that moved away from a still pointer stops being hovered; a rebind clears the hover.
11. **Found and fixed here (same method, small).** The tooltip's pointer-on-tooltip branch ignored validity: with the pointer resting on a card's tooltip, a title edited until it fits left the tooltip open (contract 34 D11 and ID-9 say it closes). The hover was not checked again at an install, nor reset on a rebind. Both close with design 10, each with a red fact.
12. **Records.** Contract 34, appended: the owner's recorded call (CD24 and the three answers, worded without "as on the Mac"), which closes the #1721 row; it names what it supersedes (D5's "a point on an endpoint card resolves to the card … the board answers cards first", `:6510`) and corrects (TD-3's "contains … its label chip", `:7260-7262`), and records the realization rule (Call 4) and ID-9's edge-by-focus cell. `ContractsCitationCensus` runs over it. The W-C matrix's canvas visual row and its census manifest: the chips, the bounds, the point rule, the new evidence, and the axe labels `canvas-board-covering` and the first PR's `canvas-board-first-search`, in the row's "axe: `a`, `b`" form. Doc comments that become false: `CanvasRendererPeers.cs` (the point walk's), `CanvasRendererView.cs` (`HitTest`'s), `CanvasPeerTopology.cs:104-107`.

**Rejected.** Windowing an edge by its chip: a line crossing the window would leave the board's children while drawn. Reading a connection offscreen when its chip is off the board: a walk would skip a connection the board may show (Call 3). Laying a chip out on each query: the red team counted 300 labelled edges among 1,899 in `large_2000.canvas`, and mouse tracking queries points as the mouse moves.

## Tests (each red on b19c94ae's code first, then green; each named mutation turns its fact red)

The chip facts run in a host window with `Slate.Light.xaml` merged (the pack URI the palette and reading facts use), and the theme facts in light and dark, so brushes are real.

1. **The covering-state scan** (gate): red on main; green with the chips. Mutation: the edge's bounds back to the placement.
2. **`ALabelledEdgesBoundsAreItsDrawnChip`**: cards of different sizes (a sized card helper), so the line's midpoint differs from the union's centre; the edge's bounds equal the chip rectangle read from `ChipsDrawingForTests`, centred within a pixel on the midpoint computed from the fixture, smaller than the union; empty off the window. Rows: factor 1 and 1.5; a self-loop. Mutations: midpoint from the union's centre; bounds left as the placement; the factor dropped from the layout.
3. **`TheChipsLayerDrawsOneChipPerWindowedLabelledEdgeAboveTheCardsAndBelowTheRing`**: labelled edges in and out of the window, one in the margin (drawn, off the board), an unlabelled one, a whitespace-only one, a dangling one; exactly the windowed labelled ones are drawn, in `SceneEdges` order; the board's visual children are edges, cards, chips, ring. Brushes by identity with the host's resources: the fill is `Slate.SurfaceBrush`, the outline pen's brush is `Slate.Canvas.EdgeBrush` and 1 px wide, the text brush is `Slate.Canvas.TextBrush`. Mutations: the outline in `Slate.BorderBrush` (same colour, a different brush), the fill in `Slate.WindowBackgroundBrush`, the text in `Slate.TextBrush`.
4. **`AChipsTextFollowsTheTextSizeAndTheZoomWithAFloor`**: the glyph run's size is `10 × factor × zoom` at (1, 1), (1.5, 1), (1, 2), and 4 at zoom 0.1; the chip rectangle follows at 1.5. Mutation: the floor dropped.
5. **`ALongLabelIsCutOffAndAFittingOneIsNot`**: a long label caps the chip at `200 × zoom` and is cut off; a label with a line break, a leading break and trailing spaces draws on one line, normalized, and is cut off only if the drawn text is. Mutations: the cap; the normalization.
6. **`ALaterChipCoversAnEarlierOne`** (answer 2): twins with two different labels of equal drawn width; both chips drawn, the later's glyphs last, the rectangles equal; both elements present. Mutation: reverse order.
7. **`APointOnAChipIsTheConnectionAndOnTheBareLineIsTheBoard`**: a chip over a card answers the connection; the card away from the chip answers the card; the line away from the chip answers the board; a self-loop's chip over its own card answers the connection. Red on main. Plus the two pinned facts rewritten, names kept: `APointOnAnEndpointCardIsTheCardNotTheEdge` (a chip premise in place of the union) and `APointBesideTheBoardIsNeverOneOfItsPeers` (its point on a chip lying beside the board). Mutation: cards first.
8. **`AWindowedChipOffTheBoardReadsOnScreen`** (Call 3): its element is a child, reads on screen, and its rectangle lies outside the board.
9. **Realization** (Call 4): `AnEdgeLongerThanTheViewportRealizesOnItsChip` (replacing the endpoint fact) and `AShortEdgesLongLabelIsInViewAfterItsRealization` (union plus chip); with the other answer, the existing fact stays and a fact pins the chip's position after the endpoint pan.
10. **Tooltip facts** (`T/CanvasDocumentTests.cs`, beside the card's, through `PointerMovedForTests`): hover on a cut-off chip opens its full raw label below the chip, and a fitting chip opens nothing; Esc's rung dismisses it, and an install with the pointer still keeps it closed; the hidden board, a modal surface and an inactive window hide it and bring it back; the chip unlabelled, deleted, or panned out of the window closes it at the install; a chip over a truncated card wins the hover, and a fitting chip over a truncated card shows nothing; a card and an edge sharing an id never mix subjects; a republish (a watcher-style reload of the same canvas) with the pointer still keeps the chip's tooltip and its subject. Found and fixed here: `ATitleEditedToFitClosesItsTooltipUnderThePointer` (the card's pointer-on-tooltip branch) and `AnInstallThatMovesACardFromUnderAStillPointerEndsItsHover` (red on main).
11. **`APointerOnAChipOverACardShowsTheChipsLabel`** (gate, the real pointer; the probe's recipe with `Mouse` touched first): a cut-off chip over a truncated card; the pointer on the chip shows the chip's full label, not the card's title; the pointer stepped onto the tooltip keeps it open. Red on main (no chip; the card's title shows).
12. **Theme rows** (`T/ThemeTokenContrastTests.cs`, light and dark): `canvas-text/surface` and `canvas-ring/surface` in the text matrix (> 75); `canvas-chip-outline/window`, `/surface`, and against the six fills and the group fill at the mark floor (> 15). Green on main by step 0's measurement; they guard the tokens from now on.
13. **Censuses**: `WcMatrixCanvasEvidenceCensus` (the row's new evidence and axe labels), `ContractsCitationCensus` (the contract append), `CanvasLabelClass`, `TestWaitCensus`, `AnnouncementNameCensus` (the new gate journey scans or names its items).
14. **Cost**, measured on `large_2000.canvas` and recorded in the PR: an install with its labelled edges windowed, one point query, one hover move; before and after.

**Existing facts that must stay green:** every fact in `T/CanvasRendererPeerTests.cs`, `T/CanvasDocumentTests.cs`, `T/CanvasPeerTopologyTests.cs`, `T/CanvasContextMenuTests.cs` (the right-click rule, Call 5), `T/TextScaleTests.cs`, `T/ThemeTokenContrastTests.cs`; every census; the gate's canvas journeys. The whole unit project with CI's filter runs under the lock before the push.

## Screen-reader phrases (Call 9)

NVDA with mouse tracking on, in a copy of the owner's profile, base and head: the mouse on a chip over a card reads the connection on head and the card on base; on a card under no chip, the card on both; on the bare line, the board on head and the connection on base (inside the union). NVDA's highlight on the connection: the chip on head, the union on base. Object navigation does not reach a connection on either build (#2064).

## Ledger lines

- **#1954:** none; no core change.
- **Mac divergences, recorded in the PR, not owed:** the Mac shows a tooltip on every chip and Windows only on a cut-off one (the owner's answer); the Mac draws a chip for a whitespace-only label; the Mac estimates a chip's width; the Mac draws no outline. The Mac's arbitrary covering order is filed as #2069.
- **#1955:** three: a chip's and a card title's text flow is always left to right (`CanvasRendererView.cs`, the `FormattedText` calls); Esc reaches the board's tooltip only through the canvas navigator's ladder, so a pointer user whose keys are elsewhere cannot dismiss it (`CanvasNavigator.cs:1941`, the surface rung); every board peer's clickable point is (0, 0) (`CanvasRendererPeers.cs`, `GetClickablePointCore`).

## Calls for the lead (recommendation first, with its cost)

1. **Two premises in the design the owner read were wrong; the answers otherwise stand.** (a) "As on the Mac, the later chip covers the earlier": the Mac's covering order is arbitrary (#2069); Windows draws in document order. (b) "The outline uses the line's colour, so it reads as part of the connection rather than as a small card": the line's colour equals the card border's in every theme, so the outline separates the chip from the board (APCA 0 without it) but does not set it apart from a card by colour. Recommended: tell the owner both, keep the answers, word the contract's recorded call without "as on the Mac". Cost: none, unless the owner wants a distinct outline token (a design-system change only the owner starts).
2. **The point rule (design 8).** Recommended as designed. Cost: a reader's mouse or touch on the line away from its chip no longer finds the connection; a point on a card under a chip now reads the connection (a self-loop's chip sits on its own card; chips inside groups sit over member cards).
3. **Offscreen (design 6).** Recommended: a connection whose chip lies beyond the board reads on screen, as cards in the margin do; measured clean under axe. Cost: a highlight can point off the board's edge. Alternative: offscreen when the chip lies wholly off the board, which skips a connection the board may show.
4. **Realization (design 7).** Recommended: pan to contain the union with the chip when it fits, else the chip, so the element is in view after realization; Invoke still pans to its card. Cost: it changes contract 34 D5's realization sentence (WB-26's rule) and replaces `AnEdgeLongerThanTheViewportRealizesOnItsDesignatedEndpoint`; for a long edge, the reader sees the chip but neither card until Invoke. Alternative: keep today's pan; then a realized long edge's highlight lies off the board, and the PR records it.
5. **Clicks on a chip (design 9).** Recommended: as the design says, a left or right click acts on the card under the chip, pinned in `CanvasContextMenuTests`. Cost: a mouse-tracking reader who hears the connection over a chip and right-clicks gets the card's menu and seats that card. Alternative: a right-click on a chip opens nothing.
6. **The font floor (design 3).** Recommended: `max(4, 10 × factor × zoom)`, the Mac's floor. Cost: at the lowest zooms a chip is larger relative to the cards than a card title is.
7. **The per-install layout cache (design 3).** Recommended. Cost: one immutable map per install; nothing lays text out per query.
8. **Fix the tooltip's validity and hover revalidation here (design 11).** Recommended: yes, same method, two red facts. Cost: none beyond the facts.
9. **The NVDA spot check by mouse tracking**, base and head, in a copy of the owner's profile with tracking on. Recommended. Cost: about 30 minutes of the shared desktop.
10. **A real-pointer gate journey (test 11).** Recommended: the chip-over-card hover and the tooltip's hoverability, which no canvas fact shows today. Cost: one more journey with the real pointer under the gate's lock.
11. **Records (design 12).** Recommended: the contract append and the W-C row and census in this PR. Cost: none.

## Red team record (2026-11-26, a fresh Opus xhigh agent, read-only, on the first draft)

Two blockers, fourteen majors, sixteen minors; folded in:

- **Blockers.** (1) The brush claims were untestable in an unthemed host, and the outline's colour equals a card border's: the chip facts now run themed and compare brushes by identity, with three mutations. (2) A hosted fact cannot steer the pointer: the hover rule goes through `HoverAt(Point)` and a seam, and the physical half is a gate journey (test 11).
- **Majors.** (3, 4) The live text-size factor and per-query layout: the per-install cache (design 3), and a cost measurement (test 14). (5) The design's two wrong premises: Call 1, #2069 filed. (6) Hoverability by WPF's event order: measured with the real pointer (step 0): it holds. (7) The subject is a `CanvasPeerKey` with one validity check, and the card branch's defect is fixed here. (8) The tooltip facts cover the whole trigger matrix (test 10). (9) Labels with line breaks and edge whitespace are normalized (design 3, test 5). (10) Realization: Call 4. (11) The font floor: Call 6. (12) The contract append names what it supersedes (design 12). (13) The right-click rule: Call 5. (14) The off-board chip in the covering state: measured clean with a prototype (step 0). (15) The mutations that could not bite are reworked (sized cards, a long label, the factor on the rectangle, equal-width twins, the margin chip, an offscreen fact). (16) Every open choice is a call with its cost.
- **Minors.** The board's background citation (`:688`, not `:817`); the run counts; twins share a centre, not always a rectangle; Call 2's reverse case; a windowed union can lie wholly in the margin; the ring and outline contrast rows; the Mac divergences recorded; the comments that become false (design 12); D11's text-run census does not exist (tests 4 and 5 stand in); the three #1955 lines; PR 1's test helpers reused after the merge; the W-C row's form; a self-loop leg.

### Lead's rulings (2026-11-26)

1. The lead put both false premises to the owner (2026-11-26). The owner kept all three answers: a later chip covers an earlier one, in Windows document order; the 1-pixel `Slate.Canvas.EdgeBrush` outline, knowing it equals the card border; and a click on a chip acts on the card under it, the right-click menu included. The contract's recorded call is worded without "as on the Mac" and cites #2069.
2. Yes, the point rule; the two pinned facts are rewritten with their names kept.
3. Yes: a chip beyond the board reads on screen, by the cards' rule.
4. Yes: pan to the union plus the chip when that fits, else to the chip, so D4's promise holds. The contract append names the D5 sentence it supersedes; `AnEdgeLongerThanTheViewportRealizesOnItsDesignatedEndpoint` is replaced. The §12 owner's-eye items carry it.
5. Act on the card (the owner's answer), pinned in `CanvasContextMenuTests`.
6. Yes, the font floor `max(4, 10 × factor × zoom)`.
7. Yes, the per-install layout cache.
8. Yes, found and fixed here, red first.
9. Yes, the NVDA spot check by mouse tracking, base and head.
10. Yes, the one real-pointer gate journey, in the CanvasBoard partial.
11. Yes: the contract 34 append, the #1721 row closed, `ContractsCitationCensus` run, and the W-C row and its census in this PR.
The three #1955 lines and no #1954 line are approved.

## What delivery changed (2026-11-26)

- **Tests whose premises were wrong, fixed before green.**
  - A drawing visual hands out a new copy of its drawing at each read, so the layer-order fact compares the layers' bounds, not the drawings by identity.
  - The board's landing seats a card, and a seated truncated card's keyboard trigger outranks the pointer. The hosted pointer facts and the gate journey clear the seat first.
  - A pan that brings a box in is the smallest one, which leaves it flush with the view's edge. The in-view checks allow a hundredth of a pixel.
  - ARealizationWaitsForTheSceneTheDocumentApplied pinned the endpoint realization through a direction change. It is rewritten with its name kept, through a relabel that widens the chip.
  - The title fact reloaded the file. A reload hides the board while it loads, and the popup's own rules then close the tooltip, so the fact passed with the fix removed (mutation M13b). It now edits the card in place and is red without the fix.
- **TypeRampCensus** names the chip's two literal sizes (base 10, floor 4) as drawn labels scaled by text size and zoom, as it names the graph's.
- **Red team (Opus xhigh, on 29645ab6): no blocker, one major, ten minor.** All taken, each red first where behaviour changed:
  - the contract append's false sentence about a point on an endpoint card;
  - vertical tab and form feed now read as spaces;
  - the later chip pinned for the point walk and the hover;
  - the ring pinned as the top layer;
  - the right-click fact checks its chip is there;
  - the gate journey's premises (no seat, a truncated card, a retried last read);
  - a rebind closes the tooltip and drops the held subject;
  - three doc comments;
  - a realization during an uninstalled zoom lays the chip out at the committed zoom;
  - a production-shaped seam for the pointer leaving the board onto the tooltip;
  - the append moved after #1277's record.
- **Codex.** Round 1 found one medium: during a rebind a pointer move over the old canvas's chip reopened its label. Every tooltip subject is now valid only while the installed state is the bound document's. Round 2: approve.
- **The board takes no left click.** The owner's "a click on a chip acts on the card under it" binds the right-click now and any later click. The contract and the W-C row say so.
- **Cost.** Measured on large_2000.canvas: about 2.8 ms more per install with 70 chips windowed, and about 0.17 ms more per point query. See the PR.
- **NVDA by mouse tracking.** Every expectation held on base and head. Pre-existing on both builds: a card's title is spoken twice (name and help text), and the connection's role is "unknown".
- **Hoverable, measured again with the real pointer, a pixel at a time.** Step 0's probe measured a card's tooltip as hoverable, but the card was seated and its keyboard trigger held the tooltip. With no seat and 1-pixel steps, crossing from a card or a chip onto the tooltip closed it: the board's leave can arrive before the tooltip's enter. This was found and fixed here (1ca84996). The board's leave and the tooltip's leave read where the pointer is. The gate journey was red at the card's step before the change. After it, the event-position version passed 2 runs of 3, and the cursor-position version passed 5 of 5.
- **Codex round 3** reviewed the hoverable change.
- **CI caught one thing local runs did not.** python -I scripts/canvas_reconciliation.py --check, a step in the Windows app job, failed at e74c7336. The append moved the divergence table's line numbers. CanvasReconciliationCensus checks the contract to evidence table, not those line numbers, so the local census run passed. The fix was --write (2bd7fc5d), which changes only line numbers. After a contract 34 edit, run --check before pushing.
- **PR #2073**, head 2bd7fc5d (contains main b2ebcbb9). Every check passed or skipped.
- **Found after READY, filed:**
  - #2074: NVDA speaks a card's title twice, as its name and its help text.
  - #2075: the census does not check the divergence table's line numbers.
  - On #2064, a comment: NVDA's navigator gives a connection the role "unknown".
- **Merge judge on 2bd7fc5d: MERGE, with 0 blockers, 0 majors, 1 minor and 8 nits.**
  - Minor: a seated truncated card's tooltip outranks the pointer, so the append now says so.
  - Nit 8: ID-9's edge-by-focus cell was closed on my own reasoning. It is left open now, pointing at #2076, which covers the keyboard route and the precedence for the owner's design.
  - Nit 1: CRLF is now named as one break.
  - This docs-only delta is 72e8866a. Its contract 34 censuses passed 147 of 147.
- **Merged** on 2026-11-26 as 20f7a8ab, squashed at 72e8866a, closing #1721. Final count: 10 found, 3 fixed here, 3 deferred, 4 filed (#2069, #2074, #2075, #2076).
- **Cleanup:**
  - The Codex broker had no session recorded after the reboot.
  - Removed: the worktree, the local branch, and the target folder `target-cb36-board-order-first-search`. The remote branch was already gone.
  - The worktree's empty folder was held only by my own tool shells, which start in it. A detached process removed it after they exited; no process was stopped.
