# WB-13 — Deleting the focused canvas card seats the next outline row instead of dropping the keys on the window

Issues: Closes #1260. Size S–M. Lanes: windows (+ one FlaUI journey extended). AT: NVDA spot. Wave: W5 (slot 2, then WB-14). Depends on: nothing. Parallel with: WB-10, WB-15. Decision: none. Interlock: plan 43 PR 32 (#1370) edits `Canvas/CanvasOutlineView.cs` too — whichever lands second rebases (§6).

## Goal

In a canvas outline, deleting the focused card — from the card menu or the
palette — republishes the outline with a cleared seat; `Rebuild` recreates
every row container, the focused one is gone, nothing takes focus, and WPF
drops the keys to the window: NVDA says "Slate, window" and the canvas
chord Ctrl+Z does nothing until F6 brings the reader back. After this
chunk focus lands on the next row in reading order (else the previous,
else the outline tree itself), silently, with the unchanged "Deleted … —
Ctrl+Z to undo" sentence, and Ctrl+Z works from there. The completion's
`TryMarkFocusReturned`, which nothing calls today, marks that one return.

## Current state on main

Verified in the worktree; the plan's and `report_a11y.md`'s lines hold,
with one correction: the #1370 interlock line is the `CanvasPhrase.ActivationHint(row.Kind)`
call at `CanvasOutlineView.cs:53`, not `:30` (`:30` is the row view-model
constructor's `hint` parameter).

- `Canvas/CanvasOutlineView.cs:699` `OnOutlinePublished` → `Rebuild()`
  (`:738-856`): remembers group expansion (`:749-755`), then clears
  `_roots`, `_byNode`, `_parentOf`, `_selectedRow`, `_connectionHost`
  (`:756-760`), rebuilds every row from `model.FilteredOutline` in
  reading order (`:817-854`) and ends with `ApplySelection()` (`:855`).
  `ApplySelectionCore` (`:893-942`) returns at `:907-911` when
  `model.Selection.Selected` is null — so a cleared seat seats no row, and
  no code path focuses anything after a rebuild.
- `SetSelectedRow` (`:955-964`); `DeliverFocus`/`SeatFocus`
  (`:463-500`): seats the shared selection silently inside the
  `_syncingSelection` guard (`model.SeatSelectionSilently`, `:492`;
  contract C12 / CD-40), realizes the container and focuses it;
  `FocusedRow()` (`:562-565`, from `Keyboard.FocusedElement`); `FocusTree`
  (`:586-600`): the seated row, else the first root.
- The delete: `CanvasDocumentViewModel.cs:2624-2636` builds a
  `CanvasMutationOperation("delete card", …, CanvasMutationEffect.ClearSelection)`
  whose confirm is `CanvasA11yEvent.CanvasDeleted` (`:2635`); ungroup
  (`:2747-2756`) and the other deletes (`:3020`, `:3069`, `:4798`) are the
  same shape. `CanvasOperationEffects.cs:32-33`: `ClearSelection` →
  `CanvasEffectResolution.Seat(null)`; applied by
  `CanvasMutationFunnel.RefreshAndPublish` (`:572-593`). The outline is
  republished through `OutlinePublished` (`CanvasDocumentViewModel.cs:517`,
  raised at `:1093`, `:1716`, `:1729`, `:1873`).
- `CanvasOperationCompletion` (`CanvasOperationEffects.cs:107-123`):
  `TryMarkFocusReturned` (`:122`) has no production caller (only
  `CanvasOperationEffectsTests.cs:82`); the operation's receipt is
  `CanvasMutationOperation.Receipt` (`CanvasMutationOperation.cs:138, 200`).
- Reading order: `CanvasPopulation.Outline` (`CanvasPopulation.cs:139`,
  `ImmutableArray<CanvasOutlineRow>`); `Resolve(intent)` (`:220-221`).
- The sentence: core `a11y.rs:2915-2929` renders `CanvasDeleted` as
  "Deleted … — {undo_chord} to undo" at standard verbosity (goldens
  `:7415-7418`). Unchanged here.
- Precedent for "the selected row vanished": the Files tree's
  `ReconcileSelectionAfterPublication` / `NearestSurvivor`
  (`FilesSidebarViewModel.FileManagement.cs:644-673`): focus the nearest
  survivor without selecting it (contract 40 R-5, OD-11(a)).
- Facts today: `CanvasNavigatorTests.OutlineLanding.cs:61`
  `TheOutlineLandsOnTheSeatedRow`, `:80`
  `WithNothingSeatedTheOutlineLandsOnItsFirstRow`;
  `CanvasDocumentTests.cs:1065` `DeleteUnmarksTheActiveRowKeepingTheSheet`.
  FlaUI: `ShellAccessibilityTests.cs:7577`
  `CanvasMarks_ToggleListJumpDeleteAndUndo_AreReachable`, `:8611`
  `CanvasSurfaces_OutlineTreeSelectionAndActivation_AreClean`.

## Reference

Windows-only surface; the issue's "Expected" paragraph is the behavioural
spec: "focus lands on the next card in reading order (else the previous,
else the surface) with the existing 'Deleted … — Ctrl+Z to undo'
announcement, and Ctrl+Z works from there." Contract 34 C12 / CD-40: a
delivered landing is silent. Contract 40 R-5 (OD-11(a)): a tree never
holds the keys on nothing while it has rows.

## Design

- `CanvasOutlineView.Rebuild()` records, before tearing the rows down,
  `string? focusedId = HasKeyboardFocus ? FocusedRow()?.Id : null` and
  the previous reading order (`_lastOrder`, the ids of the previous
  `population.Outline`, kept as a field from the last rebuild). After
  `ApplySelection()`, if `focusedId` is non-null, `!_byNode.ContainsKey(focusedId)`
  and `_selectedRow is null`: choose the successor — the first id after
  `focusedId` in `_lastOrder` present in `_byNode` (a group counts; a
  connection row never, it has no node), else the last id before it, else
  none — and `DeliverFocus(successor)`; with no successor,
  `SelectorFocus.FocusSelectedOrFirstRow(_tree)` (`:589`), i.e. the tree
  itself, which is the surface root the issue names. The delivery runs
  inside the sync guard (`SeatFocus`), so it seats the shared selection
  silently and announces nothing; the operation's `CanvasDeleted` is the
  only utterance.
- Marking the return: `OutlinePublished` carries the operation's receipt
  when the publication is a mutation's (new `CanvasOutlinePublishedEventArgs
  { CanvasOperationCompletion? Receipt }` from `CanvasDocumentViewModel`,
  set at the funnel's republish, null for a load or a filter); the outline
  calls `Receipt?.TryMarkFocusReturned()` when it delivered the successor,
  so a retry cannot deliver twice (the receipt's "true exactly once" rule,
  `CanvasOperationEffects.cs:116-122`).
- The visual board and the table are not changed: the board keeps its
  focus already (its seat is cleared; the renderer is the one stop, D15),
  the table's `AccessibleDataGrid` has its own keepers.
- Not changed: `CanvasMutationEffect.ClearSelection` semantics (the seat
  is still null — the delivered row is a focus landing, not a new
  intent; `SeatSelectionSilently` keeps T3's rule), the delete sentence,
  the context menu (W7-7 PR 10 fixed it).
- No owner decision consumed.

## Tests

Facts fail first; xUnit, STA via `RunSta`, in the OutlineLanding partial.

- `CanvasNavigatorTests.OutlineLanding.DeletingTheFocusedRowSeatsTheNextRowInReadingOrder`
  — rows A B C, focus B, delete: focus and seat are C; `Keyboard.FocusedElement`
  is a `CanvasOutlineItem` (never the window). Mutation: drop the
  successor delivery → focus is not in the tree → fails.
- `…DeletingTheLastRowSeatsThePreviousOne` — focus C, delete: B.
  Mutation: successor-only → fails.
- `…DeletingTheOnlyRowLandsOnTheTree` — one card: the tree has focus,
  no row seated, no exception.
- `…ADeleteFromThePaletteSeatsTheSuccessorToo` — the palette's Delete
  route (no menu) lands the same way.
- `…TheDeleteSpeaksOnceAndTheLandingIsSilent` — exactly one
  `CanvasA11yEvent` (`CanvasDeleted`, text ends "— Ctrl+Z to undo"); no
  `CanvasMovedTo`. Mutation: deliver through `SelectNode` instead of
  `DeliverFocus` → a second utterance → fails.
- `…UndoFromTheSeatedRowRestoresTheCard` — Ctrl+Z after the delete acts
  (the card returns) without an F6.
- `CanvasOperationEffectsTests.ADeleteMarksItsFocusReturnOnce` — the
  receipt's `TryMarkFocusReturned` is consumed by the outline's delivery
  and false afterwards.
- FlaUI: extend `CanvasMarks_ToggleListJumpDeleteAndUndo_AreReachable`
  (`:7577`): after Delete, `FocusedElement` is a TreeItem of the outline,
  then Ctrl+Z restores. (No new journey; the shell gate's budget.)
- `CanvasScenarioCensus` / `CanvasEnablementCensus` stay green (they read
  the outline's event wiring).

## Docs and registers

- `w_c_matrix.md:57` (Canvas context menus and row actions) gains the
  sentence "deleting the focused row lands the keys on the next row in
  reading order, silently (#1260)".
- Contract 34: append a note under §E (the delete's effect row) — "focus
  return after a delete: next row in reading order, else previous, else
  the tree; `TryMarkFocusReturned` marks it" — appended, never rewriting
  the frozen D-rows.
- `18_windows_port/specs/w6_1_spec.md` §E if it has a delete row: a
  cross-reference only.
- No parity-matrix row (mac's flat outline has its own focus rule).

## Verification

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb13'; ./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --filter "FullyQualifiedName~CanvasNavigatorTests|FullyQualifiedName~CanvasOperationEffectsTests|FullyQualifiedName~CanvasDocumentTests|FullyQualifiedName~CanvasScenarioCensus|FullyQualifiedName~CanvasEnablementCensus"
dotnet test apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj --filter "FullyQualifiedName~CanvasMarks_ToggleListJumpDeleteAndUndo_AreReachable"   # NVDA closed
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/src/SlateWindows/Canvas/CanvasOutlineView.cs apps/slate-windows/src/SlateWindows/Canvas/CanvasDocumentViewModel.cs <tests>
```

`CanvasOutlineView.cs` has 0 CRs. CI: Windows lane with the shell gate.
AT: NVDA spot — outline, focus a middle card, Applications key → Delete:
"Deleted Text card "…" — Ctrl+Z to undo" then the next row's name on the
next arrow (no "Slate, window"); Ctrl+Z restores; repeat from the palette.
Record in the PR body.

## Acceptance

- [ ] Seven facts pass, seen failing first; mutations run; the extended journey green.
- [ ] `w_c_matrix.md` sentence and the contract 34 §E note appended.
- [ ] Windows lane green; format gate clean.
- [ ] NVDA spot recorded.
- [ ] PR body: `Closes #1260`. If plan 43 PR 32 landed first, the rebase of `CanvasOutlineView.cs:53` is named. Not delivered: nothing (the visual board and table need no change).

## Brief

Read `Canvas/CanvasOutlineView.cs:440-500, 555-600, 699, 738-856, 893-964`,
`CanvasDocumentViewModel.cs:517, 1093, 2624-2636`,
`CanvasOperationEffects.cs:20-40, 107-123`, `CanvasMutationFunnel.cs:572-593`,
`CanvasPopulation.cs:139, 220`, the precedent at
`FilesSidebarViewModel.FileManagement.cs:644-673`, and the facts at
`CanvasNavigatorTests.OutlineLanding.cs:61, 80`. Write the six OutlineLanding
facts and the effects fact first; see the focus land on the window. In
`Rebuild`, capture the focused row and the previous reading order before
the clear, and after `ApplySelection` deliver focus to the next survivor
(else previous, else the tree) through `DeliverFocus`; carry the mutation's
receipt on `OutlinePublished` and mark `TryMarkFocusReturned` once. Extend
the FlaUI marks journey. Append the `w_c_matrix.md` sentence and the
contract 34 note. Done when the lane is green and the NVDA spot is in the
PR body. Commit: `fix(windows): deleting the focused canvas card seats the
next outline row instead of dropping focus to the window`.
