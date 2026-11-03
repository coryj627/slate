# DS-15 — A note has a header with its place and its mode

Issues: filed by DS-0. Size L. Lanes: windows. AT: NVDA and Narrator pass. Wave 5. Depends on: DS-2, DS-9, DS-14. Decision: DSD11, DSD15. Interlock: the Properties header (`WorkspaceTemplates.xaml:561`) sits right below; plan 44's WB-29 edits tab state.

## Goal

A 40 high bar above a note holds the folder path with the note's name, a Source and Reading switch, and a ⋯ menu. A sighted reader sees where they are and which mode they're in. Each control acts on its own note, whichever pane is active. Nothing a screen reader relies on moves. Back and Forward wait for a real note history (DSD15).

## Current state on main

- **No bar.** `WorkspaceTemplates.xaml:547-554`: the tab's content grid has four rows, starting with the Properties expander at `:561`. The editor is `:626`.
- **The verbs exist:** `ToggleReadingModeCommand` (`MainWindow.xaml:48`, Ctrl+Shift+E), `SplitRightCommand`, `OpenAddPropertySheetCommand`, `OpenBulkRenameSheetCommand`. Reveal: `SelectPathFromSurface` reached through `GraphRevealInSidebar` (`Graph/WorkspaceViewModel.Graph.cs:147`).
- **Those verbs act on the active pane.** `ToggleActiveViewMode` (`WorkspaceViewModel.cs:2892-2901`), `SplitRightCommand` (`WorkspaceViewModel.cs:2536-2538`) and `OpenAddPropertySheet` (`WorkspaceViewModel.Properties.cs:120-134`) all read `ActiveGroup`. A pane becomes active when keyboard focus enters it (`MainWindow.xaml.cs:2100-2113`, `WorkspaceViewModel.Layout.cs:56-66`), and a UIA Invoke raises the click without moving focus (`ButtonAutomationPeer`, `dotnet/wpf` `release/10.0`). So in a split, an Invoke on the inactive pane's control would act on the other note. The Properties header has this exposure today (#1793).
- **The only history is the sidebar's.** `FileSidebar.HistoryBackCommand` and `HistoryForwardCommand` (`FilesSidebarViewModel.cs:1061-1062`, `MainWindow.xaml:77-78`) are "Back in Sidebar History" (`Commands/ChordTable.cs:1509`), on Windows and on the Mac. Only `RequestOpen` feeds it (`FilesSidebarViewModel.cs:2472-2499`). Quick Open goes straight to `OpenPath`, so after two notes opened that way Back is still disabled, and after mixed opens it walks sidebar selections rather than this note's past.
- **The mode's path moves focus.** `ToggleActiveViewMode` ends in `RequestActiveEditorFocus` (`WorkspaceViewModel.cs:2900`), on purpose (R-10, `WorkspaceViewModel.cs:2885-2890`). It speaks no sentence of its own: what a reader hears is the landing. `WorkspaceTabViewModel.ToggleViewMode` (`WorkspaceViewModel.cs:371-407`) flips the mode and leaves focus alone.
- **A radio switch exists to copy.** The canvas's surface switcher: `GroupName` (`Canvas/CanvasSurfaceView.cs:1097`), one Tab stop (`Canvas/CanvasSurfaceView.cs:132`), `RadioGroupArrows` (`Canvas/CanvasSurfaceView.cs:144`, `RadioGroupArrows.cs:73`), and a guard against re-entry (`Canvas/CanvasSurfaceView.cs:1406`).
- **F6:** `ShellRegionKind.Editor` lands on the editor or reading surface directly (`MainWindow.ShellRegions.cs:95-97`), so a bar above doesn't change that.
- **Contained arrows:** `WorkspaceTemplates.xaml:547-548`, for the reason at `WorkspaceTemplates.xaml:543-546`.
- **Other kinds:** Base, Canvas and Graph have their own title rows (`Bases/BaseSurfaceView.cs:78`, `Canvas/CanvasSurfaceView.cs:92`, `Graph/GraphSurfaceView.cs:86`).

## Reference

`design/windows/components/patterns/NoteHeader.prompt.md`: the behaviour, including the compact state below 520, except that its Back and Forward (`NoteHeader.prompt.md:15`, `NoteHeader.prompt.md:27`) assume a note history this app doesn't have. `components/patterns/note-header.card.html`. `ui_kits/slate-windows/index.html:66`.

## Design

1. **A `NoteHeaderView`,** row 0 of the tab's grid for Markdown tabs only. The existing rows shift down. `MinHeight` 40, `Slate.SurfaceBrush`. `AutomationLandmark`-free: a `Grid` with no peer, so it adds stops but no group to announce.
2. **The path.** Folder segments are text `Button`s in secondary text, with `E76C` at 12 between them (not peers). A segment reveals that folder in Files, showing the pane first (DS-7). The note's name is semibold primary text, not a stop, because the tab's name already says it; it carries `AutomationProperties.ItemStatus` "current" only if the pass shows it's reachable and useful. Truncate folders first, then the name. Any composed path sentence comes from core.
3. **Source and Reading.** Two `RadioButton`s with a segmented template, `E943` and `E736`, each in an `AutomationPresentationTextBlock`, built as the canvas's switcher is: `GroupName` per tab, one Tab stop and `RadioGroupArrows`, so arrow keys switch and only the checked one is a tab stop, as `NoteHeader.prompt.md` says. A new `WorkspaceViewModel.SetViewMode(tab, reading, landFocus)` is the one path. Inside `RunWorkspaceMutation`, so the mode persists, it flips that tab and lands focus only when asked. `ToggleActiveViewMode` becomes a call to it with the active tab and `landFocus: true`, so Ctrl+Shift+E, the Editor menu and the palette keep R-10's landing. The radios call it for their own tab with `false`, so focus stays on the radio for the next arrow. There's no new sentence either way: the radio's own focus speech names the mode. If a UIA Select flips a tab with the keys in its editor, the editor collapses under them and `ContentPaneBorder`'s guard lands them (`MainWindow.ShellRegions.cs:858`).
4. **⋯.** `E712`, a `ContextMenu` opened by a `Button`: Split Right, Add Property…, Bulk Rename Properties… with their chords, each bound to the same command as the menus. Named "More note actions".
5. **Every control acts for its own pane.** Before it acts, a control in the bar runs `SelectGroupFromKeyboardFocus` for its own group, which is what focus entering the pane would have done: nothing when the pane is already active, otherwise the one pane announcement and a persist. ⋯ does this when it opens too, so its rows' enabled states read this note. The commands themselves don't change, so the menus, the palette and the chords still follow the active pane.
6. **Compact.** On `SizeChanged` below 520: the switch drops its labels and shows tooltips, and folders collapse into a "…" button with a Folders menu.
7. **Tab order (DSD11).** The bar's stops come before Properties and the editor: one per folder segment, the mode switch and ⋯. As recommended, they all stay, and F6 and Ctrl+Tab remain the fast routes. The pass can change this, and the PR records the owner's call.
8. **Reading mode.** The bar stays in both modes. The reading surface's `FocusManager` paths don't include it.
9. **Census.** `FocusIntegrityManifestCensus` gains the new sites. No literal sizes (DS-2), `MinHeight` (DS-5).
10. **No Back or Forward (DSD15).** Bound to the sidebar's history, Back would sit disabled after Quick Open or walk sidebar selections, and a button that misleads is worse than none (plan 46 §1). The sidebar's commands and their names don't change. A note history is a chunk of its own if the owner wants it, and it would need: one history per tab; an entry admitted when a navigation has finished, after the dirty-note gate; every route that replaces a tab's note feeding it (Quick Open, links, Files, Connections); renames and deletes carried through, as `FilesSidebarViewModel.cs:2963-3000` does for the sidebar's; chords and a parity row; the Mac's call, since it has no such history; and facts for Quick Open, links, splits and a cancelled dirty navigation.

## Tests

Failing first:

- `TheHeaderIsFortyAndOnlyMarkdownTabsHaveIt`.
- `TheHeaderHasNoHistoryButtons`.
- `AFolderSegmentRevealsItsFolder`, `ARevealShowsAHiddenSidebar`.
- `AHeaderActionActsOnItsOwnNote`: a theory over the ⋯ rows and the switch. Two panes with different notes, the first active, and the second pane's control reached through its peer (`IInvokeProvider.Invoke`, `ISelectionItemProvider.Select`), not a click. Add Property's `PropertyAddIntent` names the second note, Split Right splits the second pane, and the switch flips the second tab only.
- `TheMenusAndChordsStillFollowTheActivePane`.
- `AnArrowKeepsFocusInTheSwitch`: arrows both ways, repeated. Focus stays on the radios, Tab goes on to the next stop, the mode persists, and no `A11yEvent` is posted.
- `CtrlShiftEStillLandsInTheNote`: both directions, the radios follow, and it's one flip.
- `TheSwitchHasOneStop`: Tab reaches the checked option only.
- `AFlipUnderTheKeysLandsThemInTheNote`: a UIA Select with focus in the editor.
- `TheMoreMenuIsTheSameCommands`.
- `BelowFiveHundredTwentyTheHeaderCompacts`.
- `F6StillLandsInTheEditor`: `ShellRegionCycleTests`.
- Gate: `NoteHeader_NamesKeysAndAxe_AreClean`.

Launched app: nested and root notes, long names, reading and source, split panes, compact.

## Docs and registers

`w_c_matrix.md` rows. `parity_matrix.md` if a verb's label changes. `wpf-feasibility.md:146` done. `NoteHeader.prompt.md:15` and `NoteHeader.prompt.md:27`: Back and Forward wait on DSD15.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~NoteHeader|FullyQualifiedName~ShellRegion|FullyQualifiedName~FocusIntegrity"`, plus the gate fact.

AT: NVDA and Narrator pass: Tab through the bar from the tab strip; hear each stop; switch modes by arrow and by chord; ⋯ menu; reveal a folder; in a split, activate the inactive pane's ⋯ by object navigation and check which note it acts on; confirm that a note's first line isn't further away by F6. Record the phrases, and the owner's read of the added stops.

## Acceptance

- [ ] The bar matches the card at 100% and compact, less Back and Forward.
- [ ] An arrow leaves focus in the switch, Ctrl+Shift+E lands in the note, and neither adds a sentence.
- [ ] In a split, every control acts on its own note, however it's invoked.
- [ ] The pass is recorded and the Tab-order question answered.

## Brief

Read `NoteHeader.prompt.md`, `WorkspaceTemplates.xaml:541-650`, `MainWindow.ShellRegions.cs:64-113`, `WorkspaceViewModel.cs:2885-2901`, `RadioGroupArrows.cs`, `Canvas/CanvasSurfaceView.cs:1404-1412`, the commands above. Build the view and wire it to the existing commands. The one new operation is `SetViewMode`. Facts first. Commit: `feat(windows): a note has a header with its place and its mode`.
