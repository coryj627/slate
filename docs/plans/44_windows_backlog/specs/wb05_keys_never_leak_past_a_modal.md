# WB-5 — Keys never leak past a modal surface: F6 is a no-op over the editor popover, and a sheet refuses the editing commands it cannot answer

Issues: Closes #1281, Closes #1259. Size S–M. Lanes: windows. AT: NVDA spot (F6 on the popover says nothing; Ctrl+Z in a sheet field changes nothing behind it). Wave: W2. Depends on: nothing. Parallel with: WB-4, WB-6. Decision: none.

## Goal

Two routes still carry keys from a modal surface to the note behind it.
With the Ctrl+E embed or citation popover open, F6 and Shift+F6 move
focus into a shell region behind the still-visible dialog and announce
it (W7-6 §4 says a modal makes the press a no-op). Inside a sheet,
Ctrl+Z, Ctrl+Y, Ctrl+X, Ctrl+V and Delete that the sheet's field cannot
answer are re-targeted by `CommandManager` to AvalonEdit's `TextArea`
and edit the note silently. After this chunk the popover counts as a
modal surface for the ring, and the sheet fence refuses every editing
command that would otherwise leave the sheet, on the sixteen fenced
overlays — the palette, search and Quick Open boxes included.

## Current state on main

Verified against the worktree; the plan's "guards the palette, search
and Quick Open fields too" needs no XAML: those three overlays already
carry the fence (below), so extending the fence's command set covers
them. One correction to the plan: "the editor popover joins
`ModalSurfaceState`" costs four censuses, listed under Design.

- `MainWindow.ShellRegions.cs:19-20`:
  `IShellRegionHost.ModalSurfaceOpen => ModalSurfaces.TopmostOpen(CurrentModalSurfaceState) is not null`.
- `MainWindow.Palette.cs:94-117` `CurrentModalSurfaceState`: sixteen
  named flags, none for the popover. `:82-83` `OpenModalSurface` is the
  same walk; `MainWindow.xaml.cs:147-152` feeds it (OR
  `ComponentDispatcher.IsThreadModal`) to `ForegroundRescanRoute` and
  `_viewModel.ModalSurfaceOpen` (`VaultLifecycleViewModel.Rescan.cs:1093`
  holds the watcher sentence back while it is true).
- `ModalSurfaces.cs:38-82` `enum ModalSurface` (paint order, last open
  wins at `:179-190`); `:140-158` `ModalSurfaceState` record;
  `:246-265` `IsOpen` switch with no wildcard (CS8509 on a new member);
  `:275-297` `DecidePaletteOpen`, `:321-330` `DecideSearchOpen`,
  `:208-214` `DecideQuickOpenOpen` — each `_ => Refuse`.
- The popover: `EditorInteractions.cs:641` `IsPopoverOpen`; opened at
  `:3411-3420`, closed at `:3373-3395`; its view is
  `WorkspaceTemplates.xaml:488-497` (`AutomationLandmarkGrid`,
  `Panel.ZIndex=100`, `IsDialog=True`, AutomationId
  `EditorInteractionPopover`) inside the tab content, beneath every
  `MainWindow.xaml` overlay; `EditorInteractionPopoverHost.cs:96-105`
  focuses `EditorPopoverClose`; Escape closes (`:109-117`).
- The ring: `WorkspaceViewModel.Layout.cs:952-956` `CycleShellRegion`
  returns when `host.ModalSurfaceOpen`; `:1012-1016` and `:1040-1044`
  re-check it per landing.
- `SheetKeyboardFence.cs:81-85` the edge is two refusals,
  `TabForward`/`TabBackward`; `:117-122` `OnUnansweredCanExecute`
  (`CanExecute=false, ContinueRouting=true, Handled=true`); `:126-139`
  `OnPreviewExecuted` guards a direct `Execute` for Tab commands only.
- `MainWindow.xaml` fences all sixteen focus scopes (`IsFocusScope`
  at `:2544` Quick Open, `:2660` search, `:2954` palette, then the
  thirteen sheets; `SheetKeyboardFence.IsEnabled="True"` at `:2545`,
  `:2661`, `:2955`, …). `SheetFenceCensus.cs:46` pins fence-plus-Cycle on
  every focus scope.
- Censuses that bind the enum: `ModalSurfaceTests.cs:220`
  `EveryModalSurfaceHasADecision`, `:259` `EachSurfaceReadsItsOwnFlag`,
  `:301-376` `EveryLiveStateFieldReadsThePropertyNamedAfterIt` (argument
  count equals the enum's length; `OverlayStateExpressions` `:379-385`
  pins non-`Sheet` expressions), `:546-600`
  `ModalSurfaceOrderMatchesTheXamlDeclarationOrder` (reads
  `MainWindow.xaml` only), `:1144`
  `TheSheetPresentationObserverCoversEverySheetAndIsWired`, `:1651`
  `TheMenuDisablesUnderEveryModalSurface`.
- Existing facts: `ShellRegionCycleTests.cs:226`
  `AModalSurfaceMakesThePressANoOp` (fake host flag), `:518`;
  `SheetKeyboardFenceTests.cs:47-335` (ten facts; `:291`
  `TheSheetsOwnEditingChordsStillReachItsField`, `:322` the TR-7
  coexistence).

## Reference

- W7-6 F6 ring spec §4 (`18_windows_port/specs/w7_6_*`): "Modal surface
  up … F6 is a no-op … focus stays in the modal … no announcement."
- Contract 40 R-6 (`40_nvda_matrix_remediation_contracts.md:17`): the
  fence refuses `TabForward`/`TabBackward` "only when nothing inside the
  sheet answered them"; shell-command suppression stays TR-7's.
- Mac: none; the issues' text is the behavioural spec.

## Design

**#1281, the popover as a modal surface.** `ModalSurface` gains
`EditorPopover` declared FIRST (it paints inside the content pane,
beneath every shell overlay, so a violated exclusivity degrades to the
shell surface); `ModalSurfaceState` gains `EditorPopover: bool`, read in
`CurrentModalSurfaceState` as
`workspace?.ActiveGroup.ActiveTab?.EditorInteractions?.IsPopoverOpen == true`
and pinned in `OverlayStateExpressions`. `IsOpen` gains its arm. The
three `Decide*` tables gain an explicit `ModalSurface.EditorPopover`
arm that keeps today's admission (`Open` — Ctrl+K, Ctrl+Shift+F and
Ctrl+O open over the popover now because the walk returns null; the
issue asks only for the ring's no-op, and this spec does not widen it).
Consequences to accept in the PR body: `OpenModalSurface` becomes true
with the popover open, so `ForegroundRescanRoute` defers an activation
rescan and `AdmitWatcherSentence` holds the watcher's sentence until the
popover closes — both are the dialog posture contract 40 R-9/OD-1 already
gives sheets. Censuses: the ordering census gains a per-row XAML file
(`WorkspaceTemplates.xaml` for the popover row, `MainWindow.xaml` for the
rest) and keeps its exhaustiveness assertion; the decision, menu-disable
and presentation-observer censuses get the row they demand (the menu
disables under the popover like under a sheet; the presentation observer
treats `IsPopoverOpen` changes as the sheet-shown event it watches).
Fallback if the owner prefers no admission or census change: the
issue's narrower form, `IShellRegionHost.ModalSurfaceOpen` ORs the
active tab's `IsPopoverOpen` and nothing else moves; the ring fact below
is unchanged.

**#1259, the fence refuses the editing commands.** `SheetKeyboardFence.Edge`
grows from two refusals to the closed set: `ApplicationCommands.Undo`,
`Redo`, `Cut`, `Paste`, `Delete`, `SelectAll`, and every static
`RoutedUICommand` on `EditingCommands` except `Copy` (a read-only field's
Copy must still reach its own TextBox — it answers inside the sheet
anyway) — enumerated by reflection once, so a new `EditingCommands`
member is covered without an edit. `IsTabCommand` becomes `IsFencedCommand`
over the same set, so the direct-`Execute` guard covers them too. A
sheet's own field keeps answering first (the class handler consults the
field before the edge, R-6's mechanism), so Ctrl+Z inside a field with
undo history still undoes there; only an unanswered command is refused.
Not changed: TR-7's shell-chord swallow; `ModalSurfaceDescriptor.OwnsTextField`.

## Tests

- `ShellRegionCycleTests.ThePopoverIsAModalSurfaceForTheRing` — a hosted
  `MainWindow` (the `CommandPaletteTests.ShellKeys` shape) opens a note,
  Ctrl+E on an embed, waits for `EditorPopoverClose` focus, presses F6
  and Shift+F6: focus stays on Close, `Announced` is empty. Mutation:
  drop `EditorPopover` from `CurrentModalSurfaceState` → focus moves to
  Files and a region line is spoken.
- `ModalSurfaceTests` existing censuses extended (rows for
  `EditorPopover`); `ModalSurfaceOrderMatchesTheXamlDeclarationOrder`
  gains the two-file form and a new negative assertion that the popover's
  AutomationId is not in `MainWindow.xaml`.
- `SheetKeyboardFenceTests.UndoRedoCutPasteAndDeleteInsideASheetNeverReachTheEditor`
  — a Theory over the five chords from the template prompt's Topic field
  and from a read-only field: the note's text and undo stack are unchanged
  and the tab stays clean. Mutation: remove a command from the edge → that
  row edits the note.
- `SheetKeyboardFenceTests.ASheetFieldWithItsOwnUndoStillUndoesInside`
  — type then Ctrl+Z in the field: the field reverts, the note unchanged.
- `SheetKeyboardFenceTests.ThePaletteSearchAndQuickOpenBoxesFenceTheEditingCommandsToo`
  — Ctrl+Z / Ctrl+Y / Ctrl+X in each of the three boxes with an empty
  query: the note unchanged (pins the "already fenced" fact so a future
  XAML change that drops the fence from an overlay fails).
- `SheetKeyboardFenceTests.ExecutingAFencedEditingCommandDirectlyInsideASheetLeavesTheEditorAlone`
  — the `:144` fact's shape over `ApplicationCommands.Undo`.
- FlaUI: `ShellAccessibilityTests.SheetFence.Templates_PromptUndoLeavesTheNoteAlone`
  beside `Templates_PromptTabTraversalStaysInTheSheet` (`:30`).

## Docs and registers

- Contract 40: append an R-6 note ("R-6 widened to the editing commands
  the fence can refuse; Copy excepted; WB-5, #1259") and a W7-6 §4 note
  for the popover (#1281). Frozen rows untouched.
- `w_c_matrix.md`: the editor popover row's notification cell gains
  "F6 is a no-op while open (WB-5)".
- No `chords.json`, designation or census-JSON change.

## Verification

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb05'; ./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --filter "FullyQualifiedName~ShellRegionCycleTests|FullyQualifiedName~ModalSurfaceTests|FullyQualifiedName~SheetKeyboardFenceTests|FullyQualifiedName~SheetFenceCensus|FullyQualifiedName~CommandPaletteTests"
dotnet test apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj --filter "FullyQualifiedName~SheetFence"   # desktop free, NVDA closed
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/src/SlateWindows/ModalSurfaces.cs apps/slate-windows/src/SlateWindows/MainWindow.Palette.cs apps/slate-windows/src/SlateWindows/SheetKeyboardFence.cs <test files>
```

CI: Windows lane plus the FlaUI shell gate. AT: NVDA spot — Ctrl+E on an
embed, F6, Shift+F6: NVDA says nothing and focus stays on "Close"; in the
template prompt's Topic field, Ctrl+Z: NVDA says nothing, the note behind
is unchanged on close. Recorded in the PR body per §4 rule 4.

## Acceptance

- [ ] Ring fact and the five fence facts fail first, then pass; mutations run locally.
- [ ] The four modal censuses extended, not weakened; `SheetFenceCensus` still green.
- [ ] Contract 40 notes appended; `w_c_matrix.md` cell updated.
- [ ] NVDA spot check recorded.
- [ ] PR body: `Closes #1281`, `Closes #1259`; the admission-and-rescan consequences named.

## Brief

Read `ModalSurfaces.cs:38-158` and `:179-330`, `MainWindow.Palette.cs:82-117`,
`MainWindow.ShellRegions.cs:19-20`, `MainWindow.xaml.cs:140-152`,
`WorkspaceViewModel.Layout.cs:952-1050`, `SheetKeyboardFence.cs` whole,
`ModalSurfaceTests.cs:220-600`, `SheetKeyboardFenceTests.cs:47-335`,
`ShellRegionCycleTests.cs:226-243`. Add `EditorPopover` first in the
enum and the state, keep the three `Decide*` admissions at today's
behaviour with explicit arms, extend the four censuses; widen the fence's
edge to the editing-command set (Copy excepted) by reflection and make
the direct-`Execute` guard use the same set. Write the facts above
failing first; run the FlaUI journey with the desktop free. Append the
R-6 and §4 notes. Done when the lanes are green and the NVDA spot check
is in the PR body. Commit: `fix(windows): f6 is a no-op over the editor
popover and a sheet refuses the editing commands it cannot answer`.
