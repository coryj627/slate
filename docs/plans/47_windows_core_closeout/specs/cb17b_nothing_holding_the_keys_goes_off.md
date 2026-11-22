# CB-17b: Nothing holding the keys goes off: the sheets, the launcher and every self-disabling button

Issue: #1866 (closes, with the second PR). Size L, proposed as two PRs (Call 1). Lanes: windows (the app job and the gate). AT: NVDA spot, per the plan row: three sheets, and one button of each group. Wave: C10. Depends on: CB-17a (#2037 = 3c0dbe55, merged). Parallel with: CB-33 (core only, `db.rs`; no overlap). Decision: CD11 as the owner answered it on 2026-11-16. Interlocks: `focus_integrity_manifest.json` goes CB-17a, CB-17b, CB-16, CB-18 (plan §6 :765); `FilesSidebarViewModel*.cs` (:762); `VaultLifecycleViewModel.cs`, `Panels/BulkRenameViewModel.cs`, `Bases/BaseSurfaceView.cs`, `Graph/ConnectionsLeafView.cs`, `Graph/GraphInspectorView.cs` (:763); `MainWindow.xaml`, `MainWindow*.cs`, `WorkspaceTemplates.xaml` (:764). Review: an Opus xhigh red team on each diff, then Codex until it approves.

**CD11 (§3.1).** "Every self-disabling button moves the keys first, whatever the NVDA session hears, not only the ones heard to fail." CB-17b ends every dated row of this class, and no dated accepted exception is left.

Written 2026-11-21 at `origin/main` 3c0dbe55 from: the plan's rows (§2.3 :183, §3.1 CD11, §5 :574, §6 :755-765, §7 :779, §11 :1040); `reading/04_focus_and_peers.md:110-182` and `:435-540`; #1866 with its four comments; #1955's comments 7067701218 and 7069935142; CB-17a's spec with its delivery section and its NVDA record (`docs/plans/18_windows_port/reports/cb17a_nvda_session_2026-11-21.md`); two workers' traces of 32 rows (`w1_files_pane_rows.md`, `w2_isenabled_and_close_rows.md`, in the principal's working notes, not committed; by reading, with file and line for each claim); and this spec's own probe runs (step 0). A statement that rests on reading alone says so.

`S/` is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`; `M` is `docs/plans/18_windows_port/focus_integrity_manifest.json`. "The record" is CB-17a's NVDA record.

## Goal

1. No control that holds the keys is turned off before they have left it: under each of the fourteen sheets (the menu, the launcher's rows), and for each of the 32 other dated rows.
2. The worst three things a keyboard user meets in this class end first: the dead keyboard after Import…'s box is cancelled; the keys behind the still-open Bulk Rename sheet; Clear All Marks stuck off.
3. `M` has no `dated:` row. The dated kind, its baseline and its ceiling are deleted. The 47 keys that were dated can never take a free-text exemption.

## Step 0, as run (2026-11-21, main 3c0dbe55, Release, on this desktop under the lock)

Probe facts on the real window (`ShownShell`), never committed; the source and the results (`Cb17bStep0ProbeTests.cs`, `step0_results.log`) are in the principal's working notes, not committed. 27 probe cases ran; none asserts anything about the product.

| # | Probe | Result |
|---|---|---|
| P1 | A launcher row holding the keys as a sheet opens (Settings from Quick Open…; the template picker from Quick Open… and from its own row) | 3 of 3: the row went off with the keys on it, in the turn the sheet opened |
| P1 | The menu holding the keys as a sheet opens (Settings, the template picker, Add Property, Bulk Rename) | 4 of 4: the menu went off with the keys on its item |
| P2 | Whether a sheet's first stop takes the keys in the opening turn, once the sheet's `Visibility` and `DataContext` bindings are brought up to date and it is laid out (the same four sheets) | 4 of 4 took them: Appearance (a list row), the template picker's Cancel (its Loading state), Property key, Old property key |
| P8 | The menu with one more trigger, last: `IsKeyboardFocusWithin` True sets `IsEnabled` True. A sheet opens over it, its landing still queued | 4 of 4: never off with the keys inside, and off once they left |
| P3 | Settings opened from the launcher, then closed | the keys come back to Quick Open… (as the record heard) |
| P3b | The template picker, with one template, opened by Enter on its launcher row, closed by Escape | the keys rest on `Workspace editor panes`, the empty pane's host, not on the row. At the open the row went off under the keys and WPF moved them to the pane's host, which became the window's logical focus; at the close WPF hands the keys there, and `RestoreFocusAfterTemplates` reads that as a claim and stands down (`S/MainWindow.Templates.cs:333-336`) |
| P4 | Bulk Rename's Preview and Apply turned off with the keys on them, no press (`MarkWorkInFlightForTests`) | off with the keys on it, 2 of 2; the keys then land on Old property key, inside the sheet. So the self-disable alone does not put the keys behind the sheet |
| P5 | A key press on a button inside the Bulk Rename sheet: a planted button that does nothing, Close, and Preview | **Space on the planted button: the keys leave the open sheet for the editor's text behind it, before the Click. Enter: they stay.** Preview by Space: the keys were already on the editor when Preview went off. Preview by Enter: Preview went off with the keys on it |
| P6 | The same Space presses with a candidate rule in place (a request aimed at the window from a live element inside an open sheet is declined) | the keys stay on the pressed button, its Click runs once, and the sheet's own close still returns the keys to the editor |

The record is the measurement for the 32 buttons at the level NVDA hears: "unavailable" was logged for nine of them, four went off without a word, and three were not reached.

**What P5 changes.** Every sheet and the three overlays are focus scopes (`FocusManager.IsFocusScope="True"`, 17 in `S/MainWindow.xaml`). WPF's `ButtonBase` takes the mouse capture on Space down and on a click, and when it loses the capture it gives the keys back to the main focus scope if the button is in another one. That is written for toolbars. Here it means: **Space, or a click, on any button in any sheet that stays open puts the keys behind the sheet.** #1866's two Bulk Rename comments, and the record's C1 and C2, pressed Space. This is the cause of "Preview and Apply drop the keys behind the still-open sheet", and it is wider than those two buttons: the template picker's Try Again, the query builder's and the dashboard editor's buttons, and every check box are the same by mechanism (by reading; only Bulk Rename was pressed). The self-disable is a second, smaller defect on the same two buttons (P4, and Preview by Enter in P5).

## What the drift check changes

1. **Plan §2.3 :183 and the reading's row 13** put Bulk Rename's lost keys down to its buttons going off. P4 and P5 show the keys leave on Space before the Click, by WPF's return to the main focus scope.
2. **The reading's row 11** ("the guard lands in the Files region") is wrong for the buttons inside the three Expanders: WPF's own hand-up stops on the Expander, which the guard accepts as a stop (`S/RegionFocusGuard.cs:465-468`). The record agrees ("File actions, grouping, expanded").
3. **Six dated rows do not go off in their own press** (worker 1, by reading): `Refresh()` is asynchronous in the app (`S/FilesSidebarViewModel.TreeOperations.cs:331-352`), so Rename, Folder note, Delete folder note and both Move to… buttons go off in a later posted publication, and Cancel import when the import settles. A Click handler cannot move the keys first for these.
4. **Three manifest mechanisms are not what main does.**
   - `CitationSummaryWalkButton`: `CanWalkThrough` is constant for the sheet's life (`S/Panels/CitationSummaryViewModel.cs:45-80`); the press closes the sheet. By reading the button never goes off.
   - `CanvasPromptClearMarksButton`: the text describes what happens once the stuck state is fixed.
   - File actions Move to…: its own press never turns it off; the committed move does, and it then stays off when the moved file's new place is not shown (the record's B13, run 1).
5. **The reading's fix shape for the menu** (widen the three keep-on triggers) is replaced by one trigger (P8).
6. Line numbers: the menu's style is `S/MainWindow.xaml:172-255`; the launcher style `S/WorkspaceTemplates.xaml:215-275`; the sheets' queued landings are as the reading lists them, moved by a few lines (`S/MainWindow.Properties.cs:200-202`, `:216-218`; `S/MainWindow.Citations.cs:616-618`, `:631`, `:644-646`; `S/MainWindow.Bases.cs:171`, `:183`; `S/MainWindow.Templates.cs:129-130`, `:143-144`; `S/MainWindow.MoveTo.cs:76-85`; `S/MainWindow.xaml.cs:738`, `:743-744`, `:771-772`; `S/MainWindow.Settings.cs:48-56`).
7. `IsModalSheetOpen` is read only by the launcher's sheet trigger (`S/WorkspaceTemplates.xaml:271`); a search finds no other reader.

## Design

Two PRs (Call 1). PR 1 is the buttons; PR 2 is the sheets, and closes #1866. *Revised 2026-11-21 after the red team's report; "Red team record" says what changed.*

### PR 1: the 32 buttons (refs #1866)

**D1. A sheet keeps the keys.** `RegionFocusGuard.Guarded` (`S/RegionFocusGuard.cs:216`) gains one rule ahead of its stranded test: a request to move the keys onto a `Window` is declined when the element holding them is live (in the tree, shown, enabled, focusable) and its focus scope is a live element other than the window that owns a guard landing. Those scopes are the fourteen sheets and the three overlays (`GuardRegions`, `S/MainWindow.ShellRegions.cs:923-926`; `RegionGuardCensus` holds every focus scope to a landing). The menu is a focus scope with no landing, so it still gives the keys back when it closes. A sheet that is closing is not live, so its close is untouched (P6 shows the restore still runs). The decline clears the guard's record of the last direct request (`:208-214`, `:233-236`). This is P6's rule, moved into the guard; the guard is a class handler on `Window` and runs before WPF's own hand-on to the window's logical focus. The red team looked for a request the rule would wrongly decline (Alt and F10 from a sheet, stacked sheets, Alt+Tab, a box closing, `Keyboard.ClearFocus`, an external activation) and found none.

**D2. Keys first for a command.** One seam, `S/Commands/CommandKeysFirst.cs`: `RelayCommand`, `AsyncRelayCommand` and `ReasonedCommand` raise `CanExecuteChanged` through it. Before the event is raised, on a thread that has a dispatcher (`Dispatcher.FromThread`, checked before any `Keyboard` member is touched, since `Keyboard` creates an input manager on the thread that asks), it asks: is the element holding the keys an `ICommandSource` bound to this command, enabled, whose parameter the command now refuses? A `MenuItem` inside a `Menu` or a `ContextMenu` is left alone: menu rows are not sites, and the menu owns its own keys. Otherwise the keys move first, through `RegionFocusGuard.LandBeforeLeaving(leaving)`:

1. the element's own stranded landing, if it has one;
2. else the one nearest focusable, shown, enabled ancestor, which is what WPF's hand-up proposes today, taken only if it is a stop by the guard's own rule (`IsAStop`): the Expander, for the Files pane's actions. If it is not a stop (a tab control), no further ancestor is tried;
3. else the landings of the scopes around it, innermost first, with the element as the guard's `Holder`.

`LandBeforeLeaving` takes one element or a set: every landing skips the set and everything inside it (`FirstFocusable`, `S/MainWindow.ShellRegions.cs:847-874`; `LandInLeaf`; `PropertiesLanding`). It succeeds only when the keys are then on another live element; if nothing takes them, the control goes off as today and the guard lands after, so nothing gets worse.

The seam sees a command button whenever it goes off while its window holds the keys, by its own press or not: in the press (the busy flag), in a later publication (Folder note, Delete folder note, Move to…), when work settles (Cancel import), and by another driver (a rescan's deselect, an import started elsewhere). It covers the 74 command sites and every later one. It also holds raises: `CommandKeysFirst.Hold()` opens a scope in which raises are deferred and each command is raised once when the scope ends (D4).

**D2b. Keys that came back to nothing are landed.** D2 cannot see a button that goes off while the window is not active: the window holds no keys then. When the reader comes back, or a box closes, WPF tries to return the keys to the element that had them, and an off element refuses (recalled from WPF's behaviour, and what the record's B4 shows). So on `Activated` (hooked at `S/MainWindow.xaml.cs:95`, `:192`) the window queues one step at Input: if no modal loop is running and the keys are on nothing or on the window, and the window's logical focus is an element that can no longer hold them, they land through `RegionFocusGuard.LandInScopesOf` on that element's scopes. This is the backstop for every button that is off when a box closes or the window returns: Import… on main, Cancel import after an import that ended while the reader was elsewhere, the trash's Cancel.

**D3. Import… is on while its own boxes are up.** Today the busy flag and `IsImporting` are both set before the delegate shows the box (`S/VaultLifecycleViewModel.cs:2768-2772`, `S/FilesSidebarViewModel.Import.cs:97-103`), all in the press; when the box closes, the keys cannot return to the off button, no element holds them, and no routed chord has a target (worker 1, T1).

- `IsImporting` splits in two. **The admission** is set at the press and cleared in `finally`. `RescanBlockedReason()` reads it (`S/VaultLifecycleViewModel.Rescan.cs:300-305`), so a rescan is refused through the pick exactly as today, and a second `ImportAsync` returns at once while it is set. **The running flag** is set only once the pick has returned sources; it gates `ImportCommand`, Cancel import, the status line and the other verbs, as `IsImporting` does today.
- `ImportCommand` is built with `admitWhileExecuting: true`.
- On Cancel the keys return to Import…, which is on (WPF's restore, recalled; D2b holds the outcome if it is refused). After a pick they return to it too, and D2 moves them before Import… goes off for the import. The folder picker's close (the record's item 2) is the same path.

**D4. A control that should not go off at all, does not.**

- **A Files publication raises each command once.** `ApplyTreeRefresh` (`S/FilesSidebarViewModel.TreeOperations.cs:620-700`, the batch-check projection included) runs inside `CommandKeysFirst.Hold()`, in `try` and `finally`. Rename and the File actions Move to… no longer go off and on inside one publication, and neither does any other selection verb, whoever raises (the sidebar, the registrar, the structural-mutation notice). A selection that ends null turns its verbs off at the scope's end, and D2 moves the keys first.
- **Expand loaded folders** (Call 4, the owner's) admits while executing, and a press while a run is going is ignored: today a second run would cancel the first and start again (`:779-782`). Its predicate is already constant true (`S/FilesSidebarViewModel.cs:688-694`).
- **Bulk Rename's Preview** (Call 4, the owner's) stays on while a run is in flight: its `IsEnabled` follows only whether both keys are filled. `RunPreview` already refuses a second run (`S/Panels/BulkRenameViewModel.cs:273-280`), as Enter in the sheet's fields does today.
- **The Queries leaf's three groups** do not go off while the registry republishes. A window flag is raised in `Queries_Republishing` (`S/MainWindow.Bases.cs:76-80`) and is a second condition of each of the three triggers (`S/MainWindow.xaml:2442-2444`, `:2514-2516`, `:2572-2574`): off only when the list has no selection and no republish is running. `Queries_Republished` (`:96-115`) re-seats the selection, then, if a group is about to go off with the keys on one of its buttons, lands them (the list's row, else the leaf's first stop, as today, where it runs after the off), and only then lowers the flag. The three manifest keys change with their condition; D12 maps old to new.

**D5. A close button leaves with its surface.**

- `DismissCommand` (Quick Open's Close), `CloseCanvasInspectionCommand` and `CanvasPromptClearMarksCommand` are always on. Each is read only by its own button, each button shows only while its surface does, and each delegate guards itself (worker 2, rows 13, 16, 17; the red team read the same). Clear All Marks' stuck state ends with it. The kept red test becomes its fact. Clear All Marks is never the marks list's opening landing: when no row can take the keys, the sheet holds them (`FocusCanvasPromptSheet`, `S/MainWindow.xaml.cs:810-818`, falls back to that button today, which only works because it is stuck off).
- **Move To's Cancel:** the `MoveToSheet` setter raises the property before it retires the picker (`S/FilesSidebarViewModel.FileManagement.cs:1018-1028`), so the button has left the screen, and lost its binding, before `_retired` turns the command off.
- **The editor popover's Close and Open source** cannot be always on: the window's Escape binding and the trash's Escape fallback read `ClosePopoverCommand.CanExecute` (`S/MainWindow.xaml:105`, `S/MainWindow.Trash.cs:24`). D2 moves the keys to the editor region's landing before `IsPopoverOpen` falls. If its fact shows the editor refusing while the popover is still open, `ClosePopover` makes its own focus request first, as the find bar's Close does (`S/FindInNoteViewModel.cs:360-372`).

**D6. Two moves made by the window.**

- **Bulk Rename's Apply:** the keys go to Old property key before the apply starts, through `LandBeforeLeaving` on a `WorkStarting` notice from `BulkRenameViewModel.Apply`, raised only once the apply is admitted (`:201-215`). That is where the guard lands them today, after the off (P4). Close was this spec's first choice and is not taken: Close during an apply cancels it (`RequestClose`, `:260-269`), and a held Enter or a second press would land on it. A refusal at the apply's turn (`:223-228`) leaves the keys on Old property key after its spoken reason.
- **The property row's Save:** the keys go to the row's field before `WriteInFlight` is set (`DispatchRowWrite`, `S/WorkspaceViewModel.Properties.cs:432-441`), through `PropertiesLanding`, and only when the keys are on that row's Save: not for Enter in the field, and not for a delete, whose row goes away. That is where they land today, after "unavailable". The style-default off, when the write lands the draft clean, then falls with the keys already on the field.

**D7. The four code writes.** In `RenderChrome` (`S/Bases/BaseSurfaceView.cs:751-753`), `RenderCore` (`S/Graph/ConnectionsLeafView.cs:700-712`) and `RenderGate` (`S/Graph/GraphInspectorView.cs:366-377`) the order becomes: every other write of the render first (`ShowState`, the inactive notice); then `LandBeforeLeaving` with the whole set the render is about to turn off (all four inspector sections, both Bases controls); then the `IsEnabled` writes. The keys land where the guard lands them today: the base's grid cell, the rail, the rail.

**D8. Walk through citations.** No product change if its fact confirms it never goes off (drift 4). If it does go off, its Click moves the keys to the restore's token before `WalkThrough()` closes the sheet.

**Where the keys go (Call 3).** The place WPF or the guard puts them today, reached before the control goes off: "File actions, grouping, expanded" for Delete, Folder note, Delete folder note, Cancel import and Import…; "Batch actions" and "Shortcuts" the same; the Files tree for Retry sidebar settings; Old property key for Apply. The trash's Cancel registers a stranded landing that lands the element the keys came from (`_focusBeforeTrashCancel`, `S/MainWindow.Trash.cs:31-36`), since moving the keys first ends the restore that reads it today (`:38-64`). No new stop and no new sentence.

**The rows after PR 1** (32 dated rows end; the ceiling falls from 47 to 15):

| Witness | Rows |
|---|---|
| `keys-first:` (24) | `CancelTrash`; Move checked to Recycle Bin; Cancel import; Folder note; Delete; Delete folder note; Import…; File actions Move to… and the batch Move to… (each by a move to a place not shown); `SidebarSettingsRetry`; Remove shortcut; `MoveToRetry`; `EditorPopoverClose`; `EditorPopoverOpenSource`; `BulkRenameApplyButton`; the three Queries groups; the property row's Save, both triggers, by one fact; `_quickFilter`; `_viewPicker`; `_depth`; the inspector's section |
| `stays-on:` (4, a new kind) | `BulkRenamePreviewButton`; `CitationSummaryWalkButton`; `MoveToCancelButton`; Rename |
| `exempt: always on` (4, checked by Roslyn as today) | `SidebarExpandLoaded`; `QuickSwitcherClose`; `CanvasInspectionClose`; `CanvasPromptClearMarksButton` |

If the owner answers Call 4 the other way, Preview and Expand loaded move to the first row (Preview to Old property key, Expand loaded to the Files tree).

**The census in PR 1.**

- **`stays-on: Class.Method`:** a fact whose own code names the site and calls `KeysFirstRecorder.AssertItStaysOn(what, root, act, reached)`. It fails unless the root holds the keys as the act starts, never goes off in it, and `reached()` is true after it: the predicate says the driver ran (the publication applied, the run completed, the sheet closed), so a fact that never pumped to the off cannot pass. The census requires the fourth argument and judges the call by the same syntax rules as `keys-first:`. A `stays-on` fact covers every outcome of its press.
- `DatedCeiling` 47 to 15. `DatedBaseline` stays until PR 2.

### PR 2: the fourteen sheets and the launcher (closes #1866)

**D9. A sheet takes the keys in the turn it opens.** `LandInOpeningOverlay` (`S/MainWindow.Palette.cs:70-120`) becomes `LandInOpeningSurface(ModalSurface which, FrameworkElement surface, Func<bool> land, Func<bool> stillOpen)`.

- It acts only when `which` is the topmost open surface (`OpenModalSurface`). A sheet that opens beneath another (the card editor today, `S/MainWindow.xaml.cs:787-790`; a query builder or a citation summary presented from a continuation, `S/MainWindow.Bases.cs:166-171`, `S/MainWindow.Citations.cs:607-615`) leaves its queued landing alone.
- It brings the surface's `Visibility` and `DataContext` bindings up to date, lays it out, and runs the landing. Each of the fourteen openings calls it with the landing it queues today (`AddPropertyKeyTextBox.Focus()`, `LandCardEditor`, Move To's with its three checks, and so on). The overlays' landing keeps its refresh of every binding on the field (#1867's round 8, `:77-84`), and a sheet's first stop gets the same.
- **When the landing does not take the keys in that turn, the sheet itself takes them,** whenever they are in the workspace, the welcome view or the menu, as an overlay does today (`:93-109`). This is unconditional: #1867's key fence is gone (its round 5 removed it), so this take-out is the whole guarantee that no queued key, text, click or reader's Invoke reaches a launcher row or a menu item under an open sheet.
- The queued landing stays as the fallback, and runs only while the sheet is still open and the keys are not on a stop inside it (#1867's round 6: a second landing re-selects typed text).
- **The template picker while it is loading** does not land on Cancel in the opening turn: the sheet holds the keys, and the queued landing and the state observer land as today (`S/MainWindow.Templates.cs:172-188`, `:222-258`). On main the queued landing usually finds the list already filled (P3b); landing on Cancel a turn earlier would put "Cancel, button" in front of the list on the launcher's main path.

**D10. The menu and the launcher stay on while they hold the keys.**

- The menu: one trigger, last in its style, replaces #1867's three: `IsKeyboardFocusWithin` True sets `IsEnabled` True (P8). Its condition does not read the sheet's property, so no order of notifications can turn the menu off and on. The seventeen per-surface triggers, and the census that reads them, do not change.
- The launcher: the `IsModalSheetOpen` trigger goes (`S/WorkspaceTemplates.xaml:271-273`); the first trigger already turns a row off under any modal surface once the keys have left it. `IsModalSheetOpen` and its property go with it (drift 7).

**D11. The launcher's restore (the record's item 2, P3b).** With D9 and D10 the row never goes off under the keys, so WPF never moves them to the pane's host, the window's logical focus stays the row, and the close hands the keys back to it. A fact pins it.

**D12. The census in PR 2.**

- The fifteen sheet rows take `keys-first:` witnesses from one theory per holder (below). The launcher's sheet key is deleted with its trigger.
- `DatedBaseline`, `DatedCeiling` and the `dated:` kind are deleted. In their place a frozen table, `EndedByCb17b`, holds the 47 keys, each with its key today (three Queries keys changed in PR 1) or the reason it is gone (the launcher's). The census fails if one of them is witnessed by anything but a measured or checked kind: `keys-first:`, `stays-on:`, `exempt: always on`, `exempt: not focusable`. Neither free-text exemption (`its own press never turns it off`, `never fires`) is taken for these keys. That closes the merge judge's m3 for this class.
- **m1.** A witness whose fact can skip at run time is refused unless its row says so. "Can skip" is read by following calls inside the test assembly, by name, to `Assert.Skip` (the recorder's two helpers, and the private ones at `T/EditorSurfaceCommandTests.cs:1124`, `T/RightPaneStripTests.cs:402`, `T/CommandPaletteTests.ShellKeys.cs:667-670`). The row is written `keys-first, where a desktop gives a menu the keys (#1680): Class.Method`, and the set so marked is pinned to the menu's seventeen. Every other row's witness then runs on every lane; for that reason no witness presses Space or clicks (those need the mouse capture, which a job VM can refuse, #1701): witnesses press by Enter or the peer's Invoke, and the Space and click facts stand beside them with the capture as a premise. The menu's rows cannot be made to run on the hosted runners: WPF's menu mode needs the capture too (recalled).
- The nits: the two rows with no press are reworded; the welcome view's reason is rewritten; `NeverRuns` gains `SkipTestWithoutData`; `FocuslessNames` matches the receiver's declared type as well as its name. m2 is not taken (Call 5).

## Tests, each seen red first

Every keys-first and stays-on fact is on the real window (`ShownShell`, with the sidebar attached as `T/SheetOpenListKeyTests.cs:334-338` does, and the dispatcher's context installed, so a publication is posted as in the app), holds the keys on the real control, and calls the recorder in its own code. Fourteen of the Files pane's fifteen buttons have no `x:Name`; a fact finds one by its command inside its named holder and names the holder and the command, as the census asks of an unnamed button.

**PR 1.**

1. `T/SheetKeepsTheKeysTests.cs`: a theory over every focus scope the window has (found as `GuardRegions` finds them, so an eighteenth is included): a planted button takes Space, then a click; the keys stay inside the surface and the Click runs once. The capture is a premise: a desktop that refuses it skips, naming #1701. Red on main (P5). Then Preview and Close by Space on the real Bulk Rename sheet, and the sheet's close still returns the keys. None of these is a manifest witness.
2. `T/CommandKeysFirstTests.cs`: the seam on a small window: a button in an Expander goes to the Expander first; a button whose nearest focusable ancestor is a tab control lands through its scope's landing, not on a farther ancestor; a button with no focusable ancestor goes to its scope's landing; a landing never picks the leaving set or anything inside it; a command that still admits moves nothing; a raise on a thread with no dispatcher, and on one with a dispatcher and no window, touches nothing; a row of an open menu is left alone; a held scope raises each command once, at its end, and releases on a throw.
3. `T/FilesPaneKeysFirstTests.cs`: one fact per row: Delete; Move checked to Recycle Bin; Cancel import and Import… (through `pickImportSources` and `importWorker`); Folder note; Delete folder note (`confirmDestructive`); Remove shortcut; Retry sidebar settings; the trash's Cancel (`TrashWorkerStartingForTesting`), which lands on the element the keys came from; File actions Move to… and the batch Move to…, each to a place not shown; `MoveToRetry` (`BeforeMoveToPageForTesting`); and, as `stays-on`, Rename (reached: the publication applied and the renamed row is selected) and Move To's Cancel (reached: the sheet closed). Expand loaded has a fact that it stays on through a held run and that a second press starts no second run.
4. **Import….** `ImportStaysOnWhileItsBoxIsUp`: inside the pick seam the button is enabled; after an empty pick the keys are on Import…. `ARescanIsRefusedWhileAnImportIsBeingPicked`: inside the pick seam an explicit rescan and a foreground one are refused, and no import starts while a rescan runs. `ASecondImportWhileOneIsPickedDoesNothing`. `AfterAPickTheKeysLeaveImportFirst`. The same shape for the two buttons confirmed through a box and then turned off: Delete folder note, and the Queries leaf's two Delete… (`BasesDeleteConfirmation`): the button is on inside the seam, and the keys leave it before it goes off.
5. `KeysThatCameBackToNothingAreLanded` (D2b): the keys on Cancel import, the window deactivated, the import settled, the window activated: the keys are on a live element in the Files pane. The same with a button turned off while a stand-in owned window is up. The real box's close is WPF's and Win32's, and no hosted fact reaches it; the NVDA spot presses it (Call 6).
6. `T/BulkRenameKeysTests.cs`: Preview by Enter with a held worker: on throughout, the keys on it (`stays-on`; reached: the preview published); a press while a run is in flight starts no second run. Apply by Enter: the keys go to Old property key before it goes off, and stay in the sheet through the publish; a held Enter does not reach Close.
7. The kept Clear All Marks test, and its press: the sheet closes and the keys land on the canvas. The marks list never opens with the keys on Clear All Marks.
8. `RightPaneLeafRegionTests`: Delete… for a saved query and for a dashboard, and a base file lost in a refresh (`QueriesRefreshReadForTests`): keys first. `PinFromItsButtonKeepsTheKeysOnTheButton` gains the recorder's list form and asserts no off.
9. `PropertiesLandingTests.ASaveClickLandsOnTheSamePropertysEditor` gains the recorder: the keys reach the field before Save goes off. Enter in the field and a row's delete move nothing.
10. Four facts for the code writes, each with the whole leaf body or surface as its root: the keys in the quick filter and on the view picker through a reload; on the depth control as the anchor goes; in each inspector section as the graph stops being effective (the keys never rest on a control of another section on the way).
11. Quick Open's Close, the canvas inspection's Close, and the popover's two.
12. `CitationSummaryWalkNeverGoesOff` (`stays-on`; reached: the sheet closed).
13. Census: `AStaysOnWitnessNamesTheSiteCallsTheRecorderAndSaysTheDriverRan` (planted facts, as CB-17a's fact 3; a call with three arguments is refused); `KeysFirstRecorderTests` for `AssertItStaysOn` (it fails when `reached` is false).

**PR 2.**

14. `T/SheetOpeningTests.cs`: `NothingHoldingTheKeysGoesOffAsASheetOpens`, over every `ModalSurface` that is a sheet, with the keys on the menu bar, on an item of an open menu, and on a launcher row where the launcher shows. The opener map has no wildcard arm, so a fifteenth sheet without an opener fails the build. Each fact calls `AssertTheKeysLeaveFirst` in its own code and names `MainMenu`, or the launcher style, and its sheet's token.
15. `ASheetTakesTheKeysInTheTurnItOpens`, per sheet, from the editor; and `ASheetThatOpensBeneathAnotherLeavesTheKeysAlone`.
16. `ALateLandingTakesTheKeysOutOfWhatIsUnderTheSheet`, per sheet, with the first stop refusing the keys: the sheet holds them; a queued key, `TextInput`, a click and a UIA Invoke each reach neither the launcher row nor a menu item; Escape still closes the sheet; the queued landing runs once. It follows #1867's `ALateLandingTakesTheKeysOutOfTheViewUnderThePalette`.
17. `TheTemplatePickerNeverLandsOnCancelWhileItFills`, and `ClosingAPickerOpenedFromTheLauncherReturnsTheKeysToItsRow` (P3b), by Enter and by the peer's Invoke.
18. `EmptyPaneLauncherTests.UnderASheetEveryActionGoesOffInTheTurnItOpens` is rewritten: the row holding the keys goes off only after the sheet has them, and nothing reaches it meanwhile.
19. Census: the frozen table (a free-text exemption on one of its keys, and a missing key with no reason, are each named), the skip rule through one and two levels of helper, the marker and its pinned set; the dated kind's facts are deleted.

**Mutations** (each must turn its fact red; the PR lists each): D1's rule removed; D1 without the live test (a closing sheet then keeps the keys: the restore facts go red); D2's check dropped; step 2 allowed to climb; a landing allowed to pick the leaving set; D2b removed; the import's admission not read by the rescan gate; the publication's hold removed; `admitWhileExecuting` removed from Import… and from Expand loaded; each always-on command given back its gate; the `MoveToSheet` setter's order restored; each move in D6 removed; D7's order restored in each render; the Queries flag lowered before the landing; the keep-on trigger removed; the launcher's sheet trigger restored; the same-turn landing queued again; the take-out made conditional; the topmost check removed.

Regression: every `CommandPaletteTests`, `EmptyPaneLauncherTests`, `ModalSurfaceTests`, `SheetKeyboardFenceTests`, `RegionFocusGuardTests`, `FilesRegionLandingTests`, `RightPaneLeafRegionTests`, rescan and import fact and every census; the whole unit run under the lock before each push, on this interactive desktop, where the Space, click and menu facts run without a skip (plan §7 :779); the gate on each PR.

## Files

- **PR 1, product:** `S/RegionFocusGuard.cs`; new `S/Commands/CommandKeysFirst.cs`; `S/VaultLifecycleViewModel.cs` (the two command classes) and `S/VaultLifecycleViewModel.Rescan.cs` (the rescan gate reads the admission); `S/Commands/ReasonedCommand.cs`; `S/FilesSidebarViewModel.cs`, `.Import.cs`, `.FileManagement.cs`, `.TreeOperations.cs`; `S/MainWindow.xaml.cs` (the activation step); `S/MainWindow.Trash.cs`; `S/MainWindow.ShellRegions.cs` (`FirstFocusable`, `LandInLeaf`); `S/PropertiesLanding.cs`; `S/Panels/BulkRenameViewModel.cs`; `S/MainWindow.Properties.cs`; `S/MainWindow.xaml` (Preview's binding, the Queries triggers); `S/MainWindow.Bases.cs`; `S/WorkspaceViewModel.Properties.cs`; `S/QuickSwitcherViewModel.cs`; `S/Canvas/WorkspaceViewModel.Canvas.cs`; `S/EditorInteractions.cs` (`ClosePopover`, only if D2 does not carry it); `S/Bases/BaseSurfaceView.cs`; `S/Graph/ConnectionsLeafView.cs`; `S/Graph/GraphInspectorView.cs`.
- **PR 2, product:** `S/MainWindow.Palette.cs`, `.Properties.cs`, `.Citations.cs`, `.Bases.cs`, `.Templates.cs`, `.MoveTo.cs`, `.Settings.cs`, `.Search.cs`, `S/MainWindow.xaml.cs`, `S/MainWindow.ShellRegions.cs`, `S/MainWindow.xaml` (the menu's style), `S/WorkspaceTemplates.xaml` (the launcher's style).
- **Tests and data, both:** the classes above; `T/Support/KeysFirstRecorder.cs`; `T/Censuses/FocusIntegrityManifestCensus.KeysFirst.cs`; `M`.
- New `.cs` files: `git add`, then the license header script. `dotnet format --verify-no-changes --include` after every C# edit. No `A11yEvent` construction moves (contract 38's ledger does not move). No chord, command or help line (the parity matrix does not move). No new failure text (`ShellFailureDetailCensus` sees nothing new).

## Docs and registers

`docs/plans/40_nvda_matrix_remediation_contracts.md` holds R-5 (the keys never fall out of a region because their holder went away). D1, D2 and D2b add to that behaviour. PR 1 adds an addendum after R-5's rows naming the three, in the form CB-10's and CB-9a's addenda took; if that record takes only the owner's recorded calls, the addendum is the owner's answer to Call 4 and the rest goes in the PR body. `reading/04_focus_and_peers.md` is not edited; its corrections (the drift check) go to the wave's docs PR.

## Overlap

- **CB-33** (Principal X): core only. None.
- **CB-22b** (later): the menu's shared-key rows. PR 2 edits the menu's style, not its rows.
- **CB-16** (after CB-17b): adds a status bar Cancel for an import, and moves the import's sentences to core. Its Cancel is command-bound, so D2 covers it from birth; its landing is CB-16's to choose. It meets D3's two flags in `FilesSidebarViewModel.Import.cs`.
- **CB-18, CB-40, CB-41:** the Files publication code. D4's hold is one scope around `ApplyTreeRefresh`; CB-18's spec reads it.
- **`RegionFocusGuard.cs`** and `S/VaultLifecycleViewModel.Rescan.cs` are not in plan §6's rows for this chunk; the wave's docs PR adds them.

## NVDA spot check (base is main, head is each PR)

Base phrases are the record's. Expected on head; the order of a focus line and an announcement made in the same turn is the run's to show.

| Step | Base (the record) | Head, expected |
|---|---|---|
| Delete, `n001.md` (B2) | "Preparing…" / "unavailable" / "Moving…" / "File actions, grouping, expanded" / "Moved n001.md to the Recycle Bin." | no "unavailable"; "File actions, grouping, expanded" now comes at the press, before or beside "Preparing…" |
| Remove shortcut (B10) | "Removed a.md…" / "unavailable" / "Shortcuts, grouping, expanded" | the same without "unavailable" |
| The trash's Cancel (B6, not reached on base) | not heard | a folder large enough to outlast the trip; "Stopping…" and the line of the element the keys came from; no "unavailable" |
| Import…, box cancelled (B4) | "escape", then nothing; F6 and Ctrl+O dead | "Import…, button"; F6 moves on |
| Import…, a folder picked (the record's item 2) | the keys on the window | "Import…, button" as each box closes, then "File actions, grouping, expanded" as the import starts |
| Cancel import (B5) | "unavailable" / "File actions, grouping, expanded" | the same without "unavailable" |
| Bulk Rename Preview by Space (C1) | the editor's line; the keys behind the sheet | no focus line; "201 files will be renamed, 0 skipped, 0 errors."; Tab moves on inside the sheet |
| Bulk Rename Apply by Space (C2) | the editor's line; the keys behind the sheet | "Old property key, edit", then "201 renamed, 0 skipped, 0 failed." |
| Space on a button of another sheet that stays open (the template picker's Try Again, with no template) | not in the record | the keys stay in the sheet |
| Queries Delete… (C4) | the box / "Deleted saved query." / "New dashboard, button" | the same, with no "unavailable" and no focus event on the off button |
| The property row's Save (C7) | "unavailable" / "Property status updated." / the field | the field / "Property status updated." |
| A base's quick filter through a reload (C8) | "unavailable" / the grid's first cell | the grid's first cell |
| Walk through citations (C3) | the editor's line / the walk's sentence | the same |
| Quick Open's Close (D1) | the editor | the same |
| PR 2: New Note from Template… by Enter on its launcher row (A1) | "Template picker opened. 1 template available." / "unavailable" / "Templates, list" / the row | no "unavailable" and no "Cancel, button"; whether the sheet's own line is spoken while it holds the keys is the run's to show; Escape then says "New Note from Template…, button" |
| PR 2: Ctrl+, from Quick Open… (A3) | "Settings" / "unavailable" / "Settings categories, list" / "Appearance, 1 of 1" | the same without "unavailable" |
| PR 2: Ctrl+, with the keys in the open Slate menu (A4) | "Settings" / "Settings categories, list" / "Appearance, 1 of 1" | the same; this is the one hearing of D10's menu half on a real desktop |
| PR 2: Add Property from the note header | not in the record | "Add property" / "Property key, edit" |

Clear All Marks is pressed once on head (it cannot be pressed on base).

## Ledgers

- **#1954 (Mac work owed):** none. No core change, no sentence.
- **#1955:** CB-17a's "other drivers" line shrinks: D2 covers every command-bound control whatever turns it off, D2b covers the window's return, and D4's hold ends the publication's off and on; three `IsEnabled` rows of that line stay. New lines: the Connections leaf's own stop is passed over by `FirstFocusable`, which skips borders (worker 2, row 11); `MenuRowCommandTests` still needs a desktop that gives a menu the keys; m2. CB-17a's three other finds stay as they are.

## Acceptance

- PR 1: facts 1 to 13 red on the tests-first commit and green on head; every mutation red; `M` has 15 dated rows and `DatedCeiling` is 15; the record's B4 is gone in the NVDA spot.
- PR 2: facts 14 to 19 the same; `M` has no dated row and the census has no dated kind; `gh pr view N --json closingIssuesReferences` names #1866 and nothing else.
- Each PR body records the local whole run that ran the Space, click and menu facts without a skip.
- Titles: `fix(windows): a button that goes off moves the keys first, and a sheet keeps the keys a press would send behind it`; `fix(windows): nothing holding the keys goes off as a sheet opens`.

## Calls for the lead

1. **Two PRs.** PR 1: the 32 buttons, with Import…'s box, Bulk Rename and Clear All Marks (refs #1866). PR 2: the fourteen sheets, the launcher and its restore, and the end of the dated kind (closes #1866). Recommended. The buttons hold the worst finds and can merge while the sheets, which are #1867's eight Codex rounds over again, are reviewed. PR 1 leaves main consistent: a sheet keeps the keys, and the menu and launcher go off under a sheet as they do today. Cost: two red teams, two Codex loops, two whole runs, two NVDA spots. The record's item 2 (the launcher's restore) lands with PR 2, because its cause is the row going off as the sheet opens (P3b). The alternative is one PR of about 50 files.
2. **D1, a sheet keeps the keys, and D2b, keys that came back to nothing are landed, in PR 1.** Recommended. D1 is the measured cause of #1866's two Bulk Rename comments, it cannot be fixed in a Click handler (the keys leave on Space up, before the Click), and it is the PR's own feature, so it is fixed on the branch. D2b is the only fix that reaches a button turned off while the window is away, and it backs Import…'s fix. Cost: one rule in the guard and one step on activation; Space and a click on a stay-open button change in all fourteen sheets and three overlays (the keys now stay on the pressed button); one theory over the seventeen scopes.
3. **The keys go where they go today, earlier.** Recommended: no new stop, no new sentence, and the spot check compares like with like. Cost: the Expander's group ("File actions, grouping, expanded") stays the landing for the Files pane's actions; it is focusable but not a Tab stop. The alternative is a designed landing per button (Delete to the next tree row, Import… to Cancel import), which needs the owner's answers and a fuller NVDA pass.
4. **For the owner: two buttons stay available while they run, instead of moving the keys.** CD11 says every self-disabling button moves the keys first. For Preview and Expand loaded folders this spec proposes that the button does not go off at all: the keys stay on it, and a press while it is still running does nothing and says nothing (as Enter in the Bulk Rename fields does today while a preview runs). Recommended, because the keys' place is kept: with the other reading, every Preview press sends the reader to Old property key and every Expand loaded press to the Files tree. Cost: the two buttons no longer grey while they run, and the palette's Expand loaded row has no unavailable reason during a run. A third form says a sentence on the ignored press; that is a new announcement site (contract 38's ledger moves) and needs a sentence core already has, which was not looked for. **This departs from CD11's words, so it is the owner's to answer, before PR 1's tests-first commit.** The other six rows that end without a keys-first witness are not this question: Rename goes off and on inside one publication for no reason and now does not; three close buttons (Quick Open's, the canvas inspection's and Clear All Marks) leave the screen with their surface, as Settings' Close always has; Walk through citations and Move To's Cancel never go off once read or reordered.
5. **The census:** the `stays-on:` kind with its `reached` argument; the frozen table of the 47 keys, held to measured or checked kinds, in place of the ratchet (m3); the skip rule with the menu's seventeen marked and pinned (m1). Recommended. m1 is closed for every row but the menu's seventeen, which cannot run without a foreground window. m2 (a witness is not tied to its recorder's root) is not taken: in every fact the root is a local variable, so its expression carries no name the census could match; all rows are read by the red team, and it stays on #1955. Cost: about 200 lines of census code and four planted facts.
6. **Import…'s real box has no automated check.** Hosted facts hold what the restore needs (the button is on while the box is up, a rescan is refused through the pick, and keys that come back to nothing are landed); the NVDA spot presses the real box. Recommended, with a gate journey only if the gate already drives a system box (to be read before PR 1's first fact). Cost of the journey: one more gate fact in a partial file of its own.
7. **Workers.** After D1, D2 and D2b are on the branch, two workers (Fable, high) write PR 1's facts and fixes in worktrees cut from it: the Files pane and Import… (facts 3 to 5), and the rest (6 to 12). Recommended, for wall time.

## Red team record

A fresh Opus agent at xhigh read the first draft (`cb17b_spec_draft1.md`, in the principal's working notes, not committed) against `origin/main` 3c0dbe55, by reading: 1 blocker, 10 majors, 12 minors, 7 lows (`redteam_spec_report.md`). Every finding is taken but one in part. It also checked that D1 runs before WPF's hand-on and declines no legitimate request it could find, that no fact or gate journey contradicts P5, that the 47 rows are counted right, and that the drift claims hold.

- **B1, a rescan could start during an import's pick (taken).** D3 as first drafted cleared the way for a foreground or deferred rescan between the box and the picker, and the import then started inside it (R-9, `S/VaultLifecycleViewModel.Rescan.cs:307-312`). `IsImporting` now splits into an admission, which the rescan gate reads, and the running flag; fact 4 holds it.
- **M1, the key fence is gone (taken).** #1867's round 5 removed it. D9's take-out is unconditional and is the whole guarantee; fact 16 runs per sheet with a key, text, a click and an Invoke.
- **M2, step 2 climbed to "Workspace editor panes" (taken).** Only the one nearest focusable ancestor is tried.
- **M3, a button that goes off while the window is away (taken).** D2b, with fact 5.
- **M4, a render's landing could pick what the same render turns off (taken).** D7's order, the leaving set, and the facts' wider root.
- **M5, Close cancels the apply (taken).** Apply lands on Old property key.
- **M6, `stays-on` could pass without measuring, and the batch Move to… was wrong (taken).** The `reached` argument; the batch Move to… is keys-first.
- **M7, Space and click need the capture (taken).** No witness presses Space or clicks; those facts take the capture as a premise.
- **M8, Call 4 was the owner's (taken).** It is narrowed to the two buttons whose press changes meaning and marked for the owner.
- **M9, the Queries hold had no mechanism (taken).** D4 names the flag, the order, and the three keys that change.
- **M10, "Cancel, button" on the launcher's main path (taken).** The template picker does not land while it is loading.
- **Minors (taken, eleven; one in part).** The admission flag is the second press's guard. Expand loaded ignores a press during a run instead of restarting. The hold is at the seam, around the whole `ApplyTreeRefresh`, in `try` and `finally`. The topmost check is in `LandInOpeningSurface`. Clear All Marks is never an opening landing. The trash's Cancel registers its own landing. The frozen table refuses both free-text kinds and maps changed or deleted keys. The skip rule follows helpers by name. Save's move is scoped to its own press. The NVDA table's order, labels and missing steps are corrected. The restore at a box's close is marked as recalled. **m2 in part is not taken:** the red team proposed matching the recorder's root expression to the site's names; the root is a local variable in every fact, so there is nothing to match (Call 5).
- **Lows (taken, seven).** D1 clears the direct request; D1 needs no issue of its own; contract 40's R-5 addendum; the seam checks for a dispatcher before it touches `Keyboard`; a menu row is left alone; a refusal at the apply's turn is described; the overlays keep their binding refresh.
- **Not verified by the red team or this spec:** WPF's internals, all recalled (`ButtonBase` giving the keys back on a lost capture, which P5 and P6 measured in effect; the restore at activation; the order of class handlers, which P6 measured); whether the hosted runners refuse the capture; the concrete harm of an import inside a rescan beyond the written invariant.

## Lead rulings (2026-11-21)

1. Two PRs, as recommended. PR 2 starts from main once PR 1 merges. Each has its own red team, Codex loop, whole run, NVDA spot and merge judge.
2. D1 and D2b go in PR 1. PR 1's body records that Space and a click on a stay-open button now keep the keys on that button in all fourteen sheets and three overlays.
3. The keys go where they go today, only earlier. No new stop and no new sentence, except Call 4's.
4. **The owner answered Call 4: "Stay, and speak on a press".** Preview and Expand loaded folders stay available while they run, and the keys stay on them. A press made while either is still running speaks a sentence and does nothing else. The sentence comes from core (below).
5. The census as recommended. m2 stays on #1955 with its reason.
6. The gate journey for Import…'s box is added only if the gate already drives a system box. It does: `HistorySurfaces_LeafDiffAndRestore_AreClean` finds an owned modal window through `window.ModalWindows` and presses its button (`tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.cs:4980-5020`). So PR 1 adds one journey in a partial file of its own, `ShellAccessibilityTests.ImportBox.cs`: the keys on Import…, its box cancelled, the keys back on Import…, and F6 moving on.
7. Two workers (Fable, high) once D1, D2 and D2b are on the branch.

### The sentence a press speaks while the work still runs (the owner's answer to Call 4)

No sentence in core fits. Core's nearest are `PaletteCommandUnavailable { reason }`, which only carries a reason the host words, and `HostComposed`, the host's own text; the host's own busy sentences ("Wait for the current save to finish.", "Wait for the current file operation to finish.") are host-composed residue. So core gains one event, the smallest addition that keeps the words in core:

- `A11yEvent::WorkStillRunning { work: RunningWork }`, with `RunningWork` one of `BulkRenamePreview`, `BulkRenameApply`, `ExpandLoadedFolders`.
- It says:
  - Preview pressed while a preview runs: **"The preview is still running."**
  - Preview pressed while an apply runs: **"The rename is still running."**
  - Expand loaded folders pressed while it runs: **"Folders are still expanding."**
- Its priority is the palette's unavailable sentence's (`PaletteCommandUnavailable`'s arm, `crates/slate-core/src/a11y.rs:1963`), so a press is answered at once and is not queued behind progress.
- Core: the variant, its sentence, its priority arm, its corpus sample and the variant count in `crates/slate-core/src/a11y.rs`; the mirror in `crates/slate-uniffi/src/lib.rs`. Both bindings are regenerated. The Swift side has no exhaustive switch over the event (a search of `apps/slate-mac/Sources` finds none), so the Mac compiles unchanged.
- Windows: `BulkRenameViewModel.RunPreview`'s in-flight refusal (`:273-280`) and Expand loaded's ignored press post the event. Two new `A11yEvent` construction sites: contract 38's trigger ledger moves (`python scripts/a11y_trigger_ledger.py --write`).
- The palette's Expand loaded row and its menu row run the same command, so a press there during a run says the same sentence.
- **#1954:** the Mac has both buttons (`BulkRenameSheet.swift`; `SlateCommandID.sidebarExpandLoaded`). Whether each goes unavailable while it runs there, and the Mac's adoption of the sentence with a VoiceOver pass, are owed; `not delivered: VoiceOver pass`.
- Plan §6: the `a11y.rs` row and the `crates/slate-uniffi/src/lib.rs` row gain CB-17b ahead of CB-16; no other C10 chunk edits either (CB-33 was `db.rs` and `session.rs`, merged as #2036). PR 1's lanes become windows, rust and swift.
- Facts: a core test for the three sentences and the corpus; `BulkRenameKeysTests` and the Expand loaded fact assert the event on the ignored press and that nothing else happens. The NVDA spot hears each on head.

### What the PR 2 red facts already show (worker 3, branch `claude/plan-47-cb17b-sheet-facts`, ed7783bb, tests only, not pushed)

`T/SheetOpeningTests.cs` opens every one of the fourteen sheets by its product route on the real window. On main, under the lock: 56 cases, 55 red and the map's pin green. Every sheet turns the menu off with the keys on it (bar and open menu), every sheet that can open over an empty pane turns the launcher row off (eleven; the three canvas sheets need a canvas tab), no sheet takes the keys in the turn it opens, and the launcher's restore is red by Enter and by Invoke. Its notes for D9: the template picker's `Load()` is synchronous after the property is set, so the list is filled when the opening call returns; Files Citing, a parked citation summary and Edit View Filters' builder are shown in a later posted turn; the template flow opens over the open picker. The branch waits for PR 2.

## What delivery changed (PR 1, 2026-11-22, Principal Y2)

Y stopped at the Fable limit with the core event, D1, D2, D2b and the stays-on recorder on the branch. Y2 took PR 1 over on Opus; two Opus workers wrote the Files pane and Import… half and the panels half.

- **The frozen table came forward.** PR 1 holds its 32 ended rows in `EndedByCb17b` (by their key now; the three Queries keys changed with their condition) and refuses a free-text exemption, a date or a bare fact for any of them, so m3 is closed for these rows already. `DatedBaseline` keeps only the fifteen rows PR 2 ends, and `DatedCeiling` is 15. PR 2 adds the fifteen and deletes the dated kind, as D12 says.
- **Import…'s flags.** `IsImporting` kept its name and is the admission; the new `IsImportRunning` is the running flag. Every existing reader of the admission needed no change.
- **The batch Move to… is witnessed by an import starting,** not by a move to a place the tree does not show: a collapsed folder proves nothing absent, so the check is kept (the red team's M6 was wrong on this point).
- **Quick Open's Close was not carried by D2:** its overlay collapses before the command's raise. Always on fixed it, as D5 planned.
- **`CloseCanvasInspectionCommand`'s delegate did not guard itself;** the always-on command now closes only an inspection.
- **Clear All Marks' fallback:** the sheet holds the keys (the palette's `SetHoldsTheKeys`), after forgetting the element its scope last held.
- **The Queries flag binds with `FallbackValue=False`,** so a host that lifts the leaf into a plain window behaves as before; the registry republish says it ended from a `finally` (red team F4).
- **Enter in Bulk Rename's fields now says the sentence too** while a run goes, since it reaches the same refusal.
- **The red team on the diff** (Opus xhigh, at 116e0f85): no blocker, no major; 2 minors and 6 lows. Fixed here: F1 (the trash's Cancel, hidden for its confirmation, lost its target), F2 (a held Enter repeated the sentence: one physical press, one step in the sheet and on Expand loaded folders), F3 (an `AsyncRelayCommand` whose work ends in its own press is one raise, inside `CommandKeysFirst.Hold`), F4, F7 (a constant `reached` is refused), F8 (doc drift). Declined with the bound that holds, and put on #1955: F5 (a wording window after Collapse All) and F6 (a re-targeted held property write; three coincidences).
- **Codex** approved at round 1 on ccc05803, with no material finding.
- **The merge judge** (Opus, on c1fa4d1f): MERGE with 1 minor and 5 nits. Import… now stays on while its box is up, and the box's default button is Cancel, so a held Enter closed the box and opened it again on every repeat; Import… takes the Files pane's one-press-per-hold fence. The Bulk Rename fence lets a repeat through in the preview grid, `CommandKeysFirst` reports every failure of a held scope, `AssertItStaysOn` refuses a `reached` already true, the census refuses any constant predicate, and the trash's Cancel lets go of its remembered element. Nit 3 is the Expand loaded line on #1955.
- **One physical press is the keyboard's, not WPF's guess** (Codex round 2 on cc6a2254). `KeyEventArgs.IsRepeat` is WPF's previous-key marker: any key-up ends it, and a key-up a native box took leaves it standing. The fences read the repeat bit of the WM_KEYDOWN being routed (`HeldKey`, through `ComponentDispatcher.CurrentKeyboardMessage`; the seal census forbids a message hook), and the held-key facts post the keyboard's messages to the window. The older fences in Move To, the template picker and flow and the reading view keep `IsRepeat` and are on #1955.
- **The Mac designation** of `WorkStillRunning` said the event is Windows only; it now says the Mac's adoption is owed on #1954 (the lead's ruling 4).
- **The gate journey** `ImportBox_CancelGivesTheKeysBackToImportAndF6MovesOn` drives the real box: Enter on Import…, Cancel invoked through the window's modal windows, the keys back on Import…, F6 to the empty pane. Red with `admitWhileExecuting` removed from Import….
- Merged as #2041 = ad40500d on 2026-11-22: found 21 / fixed 14 / deferred 6 (#1955 comment 7080803604) / filed 1 (#2040).

## PR 2 delta (2026-11-22, Principal Y2, at main ad40500d)

Branch `claude/plan-47-cb17b-sheet-facts`: worker 3's red facts (ed7783bb) with main merged in (fad172f1, no conflict). Bindings regenerated with this worktree's own cargo target.

**Step 0, run again on the merged branch** (Release, under the lock, `FullyQualifiedName~SheetOpeningTests`): 55 red, 1 green, 0 skipped, the same as on main 3c0dbe55. By theory: `NothingHoldingTheKeysGoesOffAsASheetOpens` 28 of 28 red (the menu goes off with the keys on the bar or on an open menu's item); `NoLauncherRowHoldingTheKeysGoesOffAsASheetOpens` 11 of 11 (for example "the launcher's row, as TemplatePickerSheet opened, went off with the keys on … Quick Open…"); `ASheetTakesTheKeysInTheTurnItOpens` 14 of 14 (for example "in the turn FilesCiting opened, the keys were on TextArea, not on its first stop"); `ClosingAPickerOpenedFromTheLauncherReturnsTheKeysToItsRow` 2 of 2 (the keys rest on "Workspace editor panes", by Enter and by Invoke). The map's pin is green. PR 1 changed none of them: D1 and D2b act after a sheet is open, and D2 leaves the menu and the launcher rows alone (a menu row is no command site; the launcher's rows go off by style triggers, not by a command).

**What PR 1 changes for D9 to D12.**

1. D1 protects D9's take-out. When D9 makes the sheet itself a stop and lands the keys on it, the sheet is its own focus scope (`FocusManager.GetFocusScope` returns a scope element itself) and owns a guard landing, so a request to move the keys onto the window while they wait there is declined.
2. D12 is smaller. `EndedByCb17b` already exists, frozen, with PR 1's 32 keys and the measured-or-checked rule. PR 2 adds the menu's fourteen sheet keys (each `keys-first: SheetOpeningTests.NothingHoldingTheKeysGoesOffAsASheetOpens`, under m1's marker) and the launcher's `IsModalSheetOpen` key as gone, with its reason (its trigger is deleted, D10). That is 47. `DatedBaseline`, `DatedCeiling`, the `dated:` kind, `DatedProblems` and their facts (`TheDatedCeilingIsTheBaselinesLength`, the dated planted cases) are deleted, and the manifest's `_doc` loses the dated kind.
3. m1, measured by reading: two witnesses reach `Assert.Skip`, both through `KeysFirstRecorder.SkipWhereTheDesktopRefusesTheMenu` (directly and through `TakeTheMenuKeys`): `CommandPaletteTests.TheMenuIsDisabledOnlyAfterTheKeysLeaveIt` (the three overlay rows) and `SheetOpeningTests.NothingHoldingTheKeysGoesOffAsASheetOpens` (the fourteen). That is exactly the menu's seventeen, so the pinned set holds. No other keys-first or stays-on witness, PR 1's included, reaches a skip. The other skips in the project (`EditorSurfaceCommandTests`, `RightPaneStripTests`, `FilesToolbarTests`, `ShellContainerLandingTests`, `WindowTabWrapTests`, `CommandPaletteTests.ShellKeys`, `LayoutItemsControlTests`, `SheetKeepsTheKeysTests`) witness no row.
4. The four nits are where CB-17a's judge put them (#1955 comment 7069935142): the two own-press rows with no press (`AccessibleDataGrid.BuildRowActionsMenu item IsEnabled`, `TextBox in BuilderConditions#0 IsEnabled`), the welcome view's "never fires" reason, `NeverRuns` without `SkipTestWithoutData`, and `FocuslessNames` matching by text.

**Drift.**

- `LandInOpeningOverlay` is still `S/MainWindow.Palette.cs:70-120`, with three callers (the palette `:151`, search `S/MainWindow.Search.cs:53`, Quick Open `S/MainWindow.xaml.cs:937`).
- The menu's seventeen per-surface triggers and #1867's three keep-on triggers are `S/MainWindow.xaml:175-247`; the launcher's two triggers are `S/WorkspaceTemplates.xaml:263-275`. `IsModalSheetOpen` is set at `S/MainWindow.ShellRegions.cs:276-281` and read by the launcher's trigger, `SheetOpeningTests.cs:135` (a premise) and the census's dated key.
- The queued landings moved: Add Property and Bulk Rename `S/MainWindow.Properties.cs:202`, `:218`; the query builder and the dashboard editor `S/MainWindow.Bases.cs:208`, `:220` (through `FocusWhenReady`, `S/MainWindow.Citations.cs:644-646`); citation details, summary and Files Citing `S/MainWindow.Citations.cs:595`, `:615-618`, `:631`; the template picker and flow `S/MainWindow.Templates.cs:129-130`, `:143-144`; Move To `S/MainWindow.MoveTo.cs:76-85`; the canvas prompt, card picker and card editor `S/MainWindow.xaml.cs:742`, `:747-748`, `:776`; Settings `S/MainWindow.Settings.cs:48-56`.
- `EmptyPaneLauncherTests.UnderASheetEveryActionGoesOffInTheTurnItOpens` (`T/EmptyPaneLauncherTests.cs:174`) pins today's off-in-the-turn and is rewritten (fact 18). `ModalSurfaceTests.TheMenuDisablesUnderEveryModalSurface` reads only the seventeen `DataTrigger`s and does not change. The census takes only a setter to `False` as a site, so D10's one keep-on trigger adds no row.

**PR 2's facts still to write (tests first):** 15b `ASheetThatOpensBeneathAnotherLeavesTheKeysAlone`; 16 `ALateLandingTakesTheKeysOutOfWhatIsUnderTheSheet` per sheet (a queued key, text, a click and an Invoke); 17a `TheTemplatePickerNeverLandsOnCancelWhileItFills`; 18 the launcher rewrite; 19 the census facts. Worker 3's 14, 15a and 17b exist and are red.

**NVDA spot, three sheets** (base main ad40500d, head PR 2): the template picker by Enter on its launcher row, then Escape back to the row (A1 and the record's item 2); Settings by Ctrl+, from Quick Open… and from the open Slate menu (A3, A4); Add Property from the note header.

### PR 2's red team on the design (Opus xhigh, at 1beb9c3c, by reading)

No blocker, 5 majors, 4 minors, 6 lows. Every finding is taken. It confirmed D10 and D11 by reading, D1 with the take-out, the delta's m1 measurement, and the counts.

- **F1 (major), the hold is forwarded to the scope's remembered element.** Focusing a sheet whose scope remembers a focusable element (the template picker left from Cancel) hands the keys to that element, so "Cancel, button" comes back on the launcher's main path, and Bulk Rename remembering Apply would take a typed-ahead Enter. Taken: the opening edge clears the surface's remembered element (`FocusManager.SetFocusedElement(surface, null)`, as `FocusCanvasPromptSheet` does). The picker's state observer lands at once when the picker is topmost, so the picker, filled before its open call returns, lands its row in the opening turn. Fact: `ThePickerLeftFromItsCancelDoesNotLandThereWhenItOpensAgain` (a pin, green on main).
- **F2 (major), the take-out's set misses holders.** The template route dismisses the palette, Quick Open or search before the picker opens, and the status bar sits outside the workspace. Taken: the take-out runs whenever the keys are in this window and not inside the opening surface, for the overlays too, since they share the routine. Fact: `ASheetTakesTheKeysFromWhereverTheyAreInTheWindow` (the palette's field, the status bar, the menu bar), red. Quick Open in `ShownShell` has no switcher, and search's field did not take the keys there, so the palette stands for the three overlays.
- **F3 (major), a sheet presented beneath another takes the keys from the topmost.** Files Citing and the query builder present from a continuation with no admission, and `FocusWhenReady` has no topmost check: measured, Files Citing presented under Settings moved the keys from Settings' Appearance row to Files Citing's Close. This is main's behaviour, and the spec's "leaves its queued landing alone" kept it. Taken: a sheet that opens beneath waits, and its landing runs when it becomes topmost (#1666's card-editor await, generalised to every surface, fired from `ModalSource_PropertyChanged`); the fallback runs only while its surface is topmost. That also gives search, opened beneath the palette by Search Vault, its landing at the palette's close (F13). Fact: `ASheetPresentedBeneathAnotherWaitsUntilItIsTopmost`, red.
- **F4 (major), a sheet and an inactive window.** Taken: D2b's activation step and the end of a modal loop land the topmost surface's opening landing when it does not hold the keys, and the sheet's hold ignores a loss to nothing. Facts: `ASheetHoldingTheKeysItselfHasThemWhenTheWindowComesBack` (red on its premise until a sheet holds the keys) and `ASheetPresentedWhileTheWindowIsAwayHasTheKeysWhenItComesBack`. The second is green on main: the red team's first route did not reproduce, and it stays as a pin.
- **F5 (major), the harness ran the fallback under the refusal.** Taken: the turn reader ends the refusal as it reads the keys.
- **F6 to F8 (minor).** `LandCardEditor` forgets its await only once it has landed. m1's skip read covers a skipped or explicit theory row, every `Assert.Skip*` member and `SkipException`, object creations, method groups and local functions, with planted cases. The second-landing reselection twin is `TextTypedAfterTheFlowsLandingIsNotSelectedByASecondOne` (red); the parked summary and the query builder from the edit-JSON continuation have no row (their route needs a base and a parked summary; F3's fact covers a later-turn sheet).
- **F9 to F14 (low).** The NVDA spot adds a second picker opening after Cancel and the picker from the palette. The frozen table pins its one gone key and refuses it if it reappears; m1's marked prefix is taken only for the menu's seventeen. The menu theory says what its template flow rows measure. `NoLauncherRowHoldingTheKeysGoesOffAsASheetOpens` reads `IsModalSheetOpen`, so the product commit replaces that premise. The routine takes the stop as well as the landing. Drift: the menu's triggers end at `S/MainWindow.xaml:252`; the card editor's beneath check is `S/MainWindow.xaml.cs:785-794`; a fifteenth sheet is a CS8509 warning plus the pin fact going red, not a build error.

## What delivery changed (PR 2, 2026-11-22, Principal Y2)

PR 2 started from main ad40500d on worker 3's red facts. One Opus worker wrote D12, the census half, in its own worktree; the principal wrote D9 to D11.

- **D9 is wider than the design.** The red team on the design widened it (F1 to F4, accepted by the lead as this PR's own feature): the take-out reaches any holder in the window outside the surface, each opening forgets its scope's remembered element, a surface that opens beneath another waits until it is the topmost, and the window's return lands the topmost surface. One routine lands all seventeen surfaces, in `MainWindow.OpeningLandings.cs`; #1666's card-editor wait is its general case.
- **The red team on the diff found a defect on main of the same class (F1, major):** the citation, Bases and property sheets' restores went straight to their token, so a sheet closed over another left the keys behind the one still open. A restore whose token is outside the topmost surface now gives way to that surface's landing, and a close that leaves another surface on top checks it once the restores have run. The window's return lands the stop the reader left in the sheet first (F4).
- **The template picker lands its row in its opening call,** not after a hold: its `Load` runs inside the open, so its state observer lands the row at once while it is the topmost. Settings lands its row in the opening turn and keeps Close for the fallback.
- **The hold always ends when its keys leave,** the window going away included: the window's return lands the surface again, so a hold kept through a loss to nothing was redundant, and could outlive its keys (diff F6).
- **The menu's keep-on trigger is equivalent in every order a fact can arrange,** since the surface now lands before the menu's triggers hear it open; its mutation stays green under every live fact, so a scrape pins it for the order #1690 measured.
- **The census:** following calls by name alone refused seven legitimate rows whose paths shared names with product members (`Dismiss`, `Current`, `Open`, `Read`); the reading applies C#'s member rules (static or instance, private reach, a qualifier read as a type only when it is no value). The welcome view's Quick Open reason had the order of `CloseSession` and `IsVaultOpen` wrong. Until D10, the launcher row carried an interim keys-first witness; the commit that removes it with its trigger is green only on top of D10 (accepted by the lead). The 47 dated keys are pinned by a hash of their text (diff F8).
- **The hold helper's parameter keeps its name** (`overlay`): its manifest row reads it, and the palette's witness names it.
- Codex approved at round 1 on e21ce5b0, with no material finding.
