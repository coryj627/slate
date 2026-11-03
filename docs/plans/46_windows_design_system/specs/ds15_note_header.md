# DS-15 — A note has a header with its place and its mode

Issues: filed by DS-0. Size L. Lanes: windows. AT: NVDA and Narrator pass. Wave 5. Depends on: DS-2, DS-9, DS-14. Decision: DSD11. Interlock: the Properties header (`WorkspaceTemplates.xaml:561`) sits right below; plan 44's WB-29 edits tab state.

## Goal

A 40 high bar above a note holds back and forward, the folder path with the note's name, a Source and Reading switch, and a ⋯ menu. A sighted reader sees where they are and which mode they're in. Nothing a screen reader relies on moves.

## Current state on main

- **No bar.** `WorkspaceTemplates.xaml:547-554`: the tab's content grid has four rows, starting with the Properties expander at `:561`. The editor is `:626`.
- **The verbs exist:** `ToggleReadingModeCommand` (`MainWindow.xaml:48`, Ctrl+Shift+E), `FileSidebar.HistoryBackCommand` and `HistoryForwardCommand` (`FilesSidebarViewModel.cs:1061-1062`, `MainWindow.xaml:77-78`), `SplitRightCommand`, `OpenAddPropertySheetCommand`, `OpenBulkRenameSheetCommand`. Reveal: `SelectPathFromSurface` reached through `GraphRevealInSidebar` (`Graph/WorkspaceViewModel.Graph.cs:147`).
- **F6:** `ShellRegionKind.Editor` lands on the editor or reading surface directly (`MainWindow.ShellRegions.cs:95-97`), so a bar above doesn't change that.
- **Contained arrows:** `WorkspaceTemplates.xaml:547-548`, for the reason at `WorkspaceTemplates.xaml:543-546`.
- **Other kinds:** Base, Canvas and Graph have their own title rows (`Bases/BaseSurfaceView.cs:78`, `Canvas/CanvasSurfaceView.cs:92`, `Graph/GraphSurfaceView.cs:86`).

## Reference

`design/windows/components/patterns/NoteHeader.prompt.md`: all of the behaviour, including the compact state below 520. `components/patterns/note-header.card.html`. `ui_kits/slate-windows/index.html:66`.

## Design

1. **A `NoteHeaderView`,** row 0 of the tab's grid for Markdown tabs only. The existing rows shift down. `MinHeight` 40, `Slate.SurfaceBrush`. `AutomationLandmark`-free: a `Grid` with no peer, so it adds stops but no group to announce.
2. **Back, Forward.** `Slate.SubtleIconButtonStyle`, `E72B` and `E72A`, the existing commands, disabled at the ends. Named "Back" and "Forward", with tooltips showing the chords.
3. **The path.** Folder segments are text `Button`s in secondary text, with `E76C` at 12 between them (not peers). A segment reveals that folder in Files, showing the pane first (DS-7). The note's name is semibold primary text, not a stop, because the tab's name already says it; it carries `AutomationProperties.ItemStatus` "current" only if the pass shows it's reachable and useful. Truncate folders first, then the name. Any composed path sentence comes from core.
4. **Source and Reading.** Two `RadioButton`s with a segmented template and `GroupName` per tab, `E943` and `E736`, each in an `AutomationPresentationTextBlock`: arrow keys switch and only the checked one is a tab stop, as `NoteHeader.prompt.md` says. Bound to the tab's reading state. The announcement stays what Ctrl+Shift+E speaks today, from the same path: no second sentence.
5. **⋯.** `E712`, a `ContextMenu` opened by a `Button`: Split Right, Add Property…, Bulk Rename Properties… with their chords, each bound to the same command as the menus. Named "More note actions".
6. **Compact.** On `SizeChanged` below 520: the switch drops its labels and shows tooltips, and folders collapse into a "…" button with a Folders menu.
7. **Tab order (DSD11).** The bar's stops come before Properties and the editor. With everything in, that's up to five Tab presses per note. As recommended, Back and Forward stay out of Tab order, since they have chords, leaving the path and the mode switch. F6 and Ctrl+Tab remain the fast routes. The pass can change this, and the PR records the owner's call.
8. **Reading mode.** The bar stays in both modes. The reading surface's `FocusManager` paths don't include it.
9. **Census.** `FocusIntegrityManifestCensus` gains the new sites. No literal sizes (DS-2), `MinHeight` (DS-5).

## Tests

Failing first:

- `TheHeaderIsFortyAndOnlyMarkdownTabsHaveIt`.
- `BackAndForwardFollowTheHistory`.
- `AFolderSegmentRevealsItsFolder`, `ARevealShowsAHiddenSidebar`.
- `TheSwitchFollowsCtrlShiftEAndViceVersa`: state in both directions, one announcement.
- `TheSwitchHasOneStop`: Tab reaches the checked option only, arrows change it.
- `TheMoreMenuIsTheSameCommands`.
- `BelowFiveHundredTwentyTheHeaderCompacts`.
- `F6StillLandsInTheEditor`: `ShellRegionCycleTests`.
- Gate: `NoteHeader_NamesKeysAndAxe_AreClean`.

Launched app: nested and root notes, long names, reading and source, split panes, compact.

## Docs and registers

`w_c_matrix.md` rows. `parity_matrix.md` if a verb's label changes. `wpf-feasibility.md:146` done.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~NoteHeader|FullyQualifiedName~ShellRegion|FullyQualifiedName~FocusIntegrity"`, plus the gate fact.

AT: NVDA and Narrator pass: Tab through the bar from the tab strip; hear each stop; switch modes by arrow and by chord; ⋯ menu; reveal a folder; confirm that a note's first line isn't further away by F6. Record the phrases, and the owner's read of the added stops.

## Acceptance

- [ ] The bar matches the card at 100% and compact.
- [ ] Ctrl+Shift+E and the switch say one thing, once.
- [ ] The pass is recorded and the Tab-order question answered.

## Brief

Read `NoteHeader.prompt.md`, `WorkspaceTemplates.xaml:541-650`, `MainWindow.ShellRegions.cs:64-113`, the commands above. Build the view, wire it to existing commands, no new verbs. Facts first. Commit: `feat(windows): a note has a header with its place and its mode`.
