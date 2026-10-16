# PR 3 — Canvas: paint order, selection after mutation, never-silent adjacent moves

Issues: #961, #1166, #1167. Size M. Lanes: swift, a11y, windows. Depends
on nothing. Parallel with PRs 1, 2, 4 to 7.

## Goal

Three state bugs in the canvas host, each pinned by a fact: the renderer
paints in document order after any pan cycle; a mutation reload never
leaves a selection pointing at a node that no longer exists; Next and
Previous Card always say something, on every document state and on an
empty canvas, through the arms Windows uses.

## Current state on main

- `Canvas/CanvasRendererView.swift:295` `rebuildVisible()` materializes
  the window: `let cardLayer = cardLayers[node.nodeId] ?? makeCardLayer
  (for: node)` (`:323`), then `if cardLayer.superlayer == nil {
  contentLayer.addSublayer(cardLayer) }` (`:325`), which appends at the
  top regardless of document index. No `zPosition` is set anywhere in
  the file. Hit testing is last-in-document-order (`hitTestNode`, `:693`,
  `scene.nodes.last { … }` `:695`), so after a pan cycle an earlier card
  can paint above a later one that hit-testing selects (#961).
- `Canvas/CanvasDocument.swift:683` `reloadAfterMutation(session:)`
  refreshes `outline`, `tableRows`, `scene`, `targets` and the caches,
  never `selection.selected` (#1166). A verb whose guard tests the raw
  selection passes while the collection it announces, resolved against
  the outline, is empty: Duplicate says "Duplicated 0 cards" and writes
  an empty undo entry.
- `Canvas/AppState+CanvasNavigation.swift:179` `canvasSelectAdjacent
  (offset:)`: `guard let doc = activeCanvasDocument else { return }`
  (`:180`, `.ready` only, silent otherwise); on an empty filtered set it
  speaks `.noCardsMatchFilter` only when `doc.filterActive`, else returns
  silently (`:183-189`). The nothing-selected arm exists at `:174-176`
  (`canvasAnnounceSelectionUnresolvable`) (#1167, CD-46).

## Windows reference

- Paint order is not a Windows concern (no layer renderer); the contract
  is JSON Canvas z-order equals array order, and hit-testing already
  agrees.
- #1166: PR E's mutation funnel drops a selection its reload cannot
  resolve (contract 34 "Mac details … A stale selection survives undo",
  `:15667`).
- #1167: Windows routes Next/Previous Card through its read-state mapping
  (CD-46, `:19383`): loading, degraded, failed, retarget-failed and empty
  each speak the arm already in the corpus (the VA-1/VA-2 closure for
  the other read verbs).

## Design

1. **Paint order (#961).** In `rebuildVisible`, stamp
   `cardLayer.zPosition = CGFloat(documentIndex)` on every materialized
   or re-materialized layer (enumerate `document.scene.nodes` with its
   index). Core Animation composites by `zPosition`, so insertion order
   stops mattering. Keep the selection-always-materializes rule
   (`:315-319`).
2. **Selection reconciliation (#1166).** At the end of
   `reloadAfterMutation`, if `selection.selected` is not among the
   refreshed outline's node ids, clear it (and any secondary selection
   state the host keeps for the same node). Verbs then take their
   nothing-selected arm: Duplicate refuses with the existing
   `.canvasStatus(note: .nothingSelected)` sentence, writes no undo
   entry, and never speaks a zero count. Marks are a separate set and
   are not reconciled here unless the mutation funnel already does so.
3. **Never silent (#1167).** `canvasSelectAdjacent` resolves the canvas
   document through the same read-state mapping the other read verbs use
   (find it by the `switch document.state` that speaks the loading /
   degraded / failed / retarget-failed arms; reuse it rather than adding a
   second switch), and on an empty canvas with no filter speaks the
   empty-canvas arm from the corpus. The existing end/start-of-canvas and
   no-cards-match-filter arms stay.

## Changes

- `Canvas/CanvasRendererView.swift:295-330`: the index and `zPosition`.
- `Canvas/CanvasDocument.swift:683-692`: the reconciliation.
- `Canvas/AppState+CanvasNavigation.swift:179-205`: the mapping.

## Tests

- `CanvasRendererTests.testRematerializedCardsPaintInDocumentOrder`:
  card A (doc index 1, wide) overlaps card B (doc index 0, small);
  materialize a window covering only A, pan so B materializes, pan back;
  assert `zPosition` ordering (or the resolved sublayer order) paints A
  above B, and `hitTestNode` at the overlap returns A.
- `CanvasScenarioTests.testUndoingACreateDropsTheSelectionAndDuplicateRefuses`:
  create a card, undo, then Duplicate; the announcer speaks the
  nothing-selected sentence, the undo stack has no "duplicate 0 cards"
  entry, and no "Duplicated 0 cards" is ever rendered.
- `CanvasNavigatorTests.testNextCardSpeaksTheDocumentStateOutsideReady`
  for each of loading, degraded, failed, retarget-failed (parametrize
  over the states the mapping covers), and
  `testNextCardOnAnEmptyCanvasSpeaksTheEmptyArm`.
- `CanvasAnnouncerTests`: the rendered sentences equal the corpus rows.

## Accessibility

The new sentences are existing corpus arms (no new copy). No VoiceOver
field pass; the facts prove the events.

## Docs and registers

`docs/plans/34_canvas_contracts.md`: CD-46 (`:19383`) closed on mac; the
"Mac details" items "A stale selection survives undo" (`:15667`) and
"`canvasSelectAdjacent` is silent outside `.ready`" (`:15790`) marked
resolved. #961 has no register row; the PR body is its record.

## Acceptance

- [ ] The three facts fail before and pass after.
- [ ] No new host-composed copy (`A11yResidueCensusTests` unchanged).
- [ ] Registers updated; lanes green.

## Risks

Clearing the selection after a reload could surprise a verb that relies
on it surviving a mutation it caused (a rename keeps its node id; only
removed nodes are affected). Audit the mutation funnel's callers of
`reloadAfterMutation` for one that reselects afterwards and keep that
order.

## Brief

Fix three canvas host bugs with a fact each. (1) In
`CanvasRendererView.rebuildVisible` (`:295`) stamp
`zPosition = CGFloat(documentIndex)` on every materialized layer so a
pan cycle cannot change paint order; pin it with a materialize / pan
away / pan back fact that also checks `hitTestNode`. (2) At the end of
`CanvasDocument.reloadAfterMutation` (`:683`) drop a selection the
refreshed outline cannot resolve; pin "undo a create, then Duplicate
refuses with the nothing-selected arm and writes no undo entry". (3)
Route `canvasSelectAdjacent` (`AppState+CanvasNavigation.swift:179`)
through the read-state mapping the other read verbs use so loading,
degraded, failed, retarget-failed and empty canvases speak their corpus
arm; pin each state. Add no copy. Close CD-46 and the two "Mac details"
items in contract 34. Done when the facts pass and the lanes are green.
