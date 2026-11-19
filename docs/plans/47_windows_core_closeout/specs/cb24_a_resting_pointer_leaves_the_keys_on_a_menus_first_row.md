# CB-24: A resting pointer leaves the keys on a menu's first row

Issues: #1849 (closes); #1846 (ends by comment on the confirming run, CD16). Size S to M. Lanes: windows (the app job and the shell gate; new gate facts, which run in CI on `windows-latest`). AT: NVDA full on the issue's steps, and on the other menus. Wave: C7, slot 3. Depends on: nothing. Parallel with: CB-15a (`FilesSidebarViewModel*.cs`, one line of `WorkspaceViewModel.Properties.cs`, `Bases/BaseDocumentViewModel.cs`, the census and `ShellCompilation.cs`) and CB-28 (`EditorInteractions.cs`, `diagram.rs`, `Reading/`, one `lib.rs` doc comment). Decision: CD16 (answered as recommended, 2026-11-16). Interlock: the gate's partial files (`ShellAccessibilityTests*.cs`): this chunk edits `ShellAccessibilityTests.NoteHeader.cs`, `ShellAccessibilityTests.SortAndGroup.cs` and `ShellAccessibilityTests.RailPanels.cs`, and no other C7 chunk may edit those three. Review: a red team, then Codex adversarial-review rounds until it approves (plan §4 rule 9; product code changes).

*2026-11-18, the plan lead's rulings on this spec's calls:* (1) yes, one shared `RestingPointerHold` with the app menu moved onto it; its regression fact runs 5 times and only misses that match #1976's placement signature are rerun, with the counts in the PR; (2) yes, the Sort and group and More panels menus are taken here, red first, and CB-24 is added to §6's `MainWindow*.cs` row in the PR's records; (3) yes, the editor's Shift+F10 is probed during implementation, and if it reproduces one issue is filed for the class (High) and named in the PR; (4) yes, #1846 closes by comment after the merge, citing step 0 and the head run. The header buttons' Shift+F10 and Apps key fix is accepted as Small, red first.

Written 2026-11-18 from the plan's rows (§2.2, §2.6's #1941/#1944 row, §5, §6, §11), `reading/04_focus_and_peers.md:256-353`, the two issues and #1944's PR body. Every file and line below was read at `main` 26d353ef. Step 0 ran on a Release build of that commit. A fresh red team read the first draft (2 blockers, 5 medium, 14 low); every finding is folded in, and "Red team record" says how. Each statement that is still by reading says so.

`S/` is `apps/slate-windows/src/SlateWindows/`; `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`; `G/` is `apps/slate-windows/tests/SlateWindows.AccessibilityTests/`.

## Goal

A menu the code opens without the pointer lands its first row (or its checked row) and keeps it while the mouse pointer rests where the menu appears. Hovering works again once the pointer moves, and a click with the pointer still resting works the row under it. That holds for the note header's ⋯ menu and its Folders menu (one code path), opened by Click, by its Invoke pattern, or by Shift+F10 or the Apps key on the button; for the Files toolbar's Sort and group menu; and for the rail's More panels menu; as #1944 made it hold for the app menu, whose remedy becomes the shared one. Under NVDA the reader hears the landing row and nothing after it.

#1846 is answered, not changed: hiding the right pane under the keys moves them once, into the editor, and NVDA names the tab control and the tab because they are the editor's ancestors in UI Automation.

## Current state on main

**The note header's menus.** `S/NoteHeaderView.cs`. ⋯ (`NoteHeaderMore`) is built at `:124-152`: Split Right, a separator, Add Property…, Bulk Rename Properties… (`:125-132`); `menu.Opened` makes the header's pane active (`:135`); the button carries the menu as its own `ContextMenu` (`:145`). The collapsed path's Folders menu (`NoteHeaderFolders`) is built at `:373-399` and set as that button's `ContextMenu` (`:395`). Both open through `OpensItsMenu` (`:530-553`, called at `:150` and `:396`): Click sets `IsOpen = true` under the button (`PlacementMode.Bottom`), and when the open was not by pointer (`byPointer`, set by the button's own mouse-up and cleared at Input priority, `:532-537`) a Loaded-priority callback focuses the first enabled row (`:550`). Because each button owns its menu, WPF's `ContextMenuService` also opens it from Shift+F10 or the Apps key on the button. That route skips `OpensItsMenu`, so it lands no row: the method's own summary says such an open "leaves the keys on the menu itself, and a reader hears only 'menu'" (`:524-527`). Nothing holds the rows from a resting pointer on any route.

**The app menu's remedy (#1944).** `S/MainWindow.AppMenu.cs:81-177`. `HoldTheRestingPointer` (`:94-115`), called before the popup opens (`:62`): records `GetCursorPos`, hooks three releases once (only when nothing was held, `:101-108`), and sets `IsHitTestVisible = false` on every row at any depth (`RowsOf`, `:167-177`). A second call while held (Alt and another letter, or F10, while the popup is open, `:41-45`) only replaces the recorded point and holds the rows again. The releases, on the bar's own route: `PreviewMouseMove` when the pointer's place changed (`:117-125`); `PreviewMouseDown`, which releases and then calls `Mouse.Synchronize()` so the release works the row under the pointer (`:127-138`); and the popup's `SubmenuClosed` when the popup itself closed (`:140-147`). `ReleaseThePointer` unhooks and clears the local value from every row it enumerates again (`:149-164`). `MainWindow.AppMenu.cs` is a CRLF file.

**The two other code-built menus.** `S/MainWindow.FilesToolbar.cs:28-110`, `OpenSidebarSortMenu`: a fresh `ContextMenu` below `SidebarSortButton` (UIA `SidebarSortAndGroup`), whose `Opened` focuses the checked order row or the first (`:91-97`); `IsOpen = true` at `:109`. `S/MainWindow.RailMore.cs:27-110`, `OpenMorePanels`: a fresh `ContextMenu` to the left of `RightPaneMorePanelsButton` (UIA `RightPaneMorePanels`), whose `Opened` focuses the checked panel row or the first (`:90-95`); `IsOpen = true` at `:109`. Both land their row on every open, from a pointer's Click or the keys'. Neither holds its rows. `Slate.ContextMenuStyle` (`S/Themes/Slate.Menus.xaml:357-421`) leaves `StaysOpen` false, turns off WPF's popup animation and slides the menu in with its own 167 ms transform while `ShellMotion.Current.Animates` (`S/ShellMotion.cs:18`, `SystemParameters.ClientAreaAnimation`); its `VerticalOffset` of -16 puts the popup's window 16 DIP over the button's lower edge (`:366`), a band that holds only the shadow margin at rest.

**Other context menus** open through WPF's `ContextMenuService` (right-click, Shift+F10, the Apps key): the Markdown editor's (`S/WorkspaceTemplates.xaml:826-843`: Activate at Cursor, Preview Embed, Check Spelling While Typing), the workspace tab strip's (`:1030-1039`), the canvas renderer's and outline's, the data grid's, the Connections tree's, the graph diagram's and the right pane's link lists'. By reading, a keyboard open of one of those under a resting pointer is the same class; step 0 did not measure one (Call 3).

**Facts today.** `T/NoteHeaderTests.cs:740-801`, `MoreOpenedWithoutTheMouseLandsOnItsFirstRow` (hosted; returns early, passing, when the first landing fails on its desktop, `:753-756`; a pointer's open by mouse-up then Click at `:780-781`). `G/ShellAccessibilityTests.AppMenu.cs:59-140`, `AppMenu_APointerRestingWhereItOpensLeavesTheKeysOnTheFirstRow` (FlaUI, real keys and pointer; it reopens File until a middle row is under the pointer because the popup lands a little differently each open, #1944's body; #1976 records it missing locally 1 in 3 to 1 in 5 runs for that reason), with `SettledBoundsOf` (`:144-160`) and `RowsOf` (`:162-164`). `G/ShellAccessibilityTests.NoteHeader.cs:23`, `NoteHeader_NamesKeysAndAxe_AreClean`, sizes its window for a CI screen about 1044 wide (`:49-61`) and opens no menu under a parked pointer. `G/ShellAccessibilityTests.SortAndGroup.cs` and `.RailPanels.cs` hold helpers whose callers are in `G/ShellAccessibilityTests.cs`, `G/ShellAccessibilityTests.FilesTree.cs:227`, `:246` and `G/ShellAccessibilityTests.RegionStops.cs:653`; none parks the pointer. `T/RightPaneHideLandingTests.cs:30`, `HidingTheRightPaneUnderTheKeysLandsThemInTheEditorOnce`, asserts one focus change for #1846's hide. The shell gate runs on GitHub-hosted `windows-latest` in every runner mode but `home-only` (`.github/workflows/windows.yml:1387`) on an interactive desktop (`G/ShellAccessibilityTests.cs:12303-12322`), where #1944's fact passes today. The unit project's app job runs on Namespace (`windows.yml:719`; `WINDOWS_RUNNER_MODE` is `namespace`), whose session has no input desktop (#1680, comment of 2026-10-27), so by the Win32 contract `GetCursorPos` fails there.

### Step 0, as run (2026-11-18, main 26d353ef, Release)

One monitor, 5120 by 2880, 200 %; the window at 1800 by 1200 device-independent pixels; points below in physical pixels. The driver, each run's report and the NVDA session log were kept in the principal's working folder, outside the repository. The pointer never moved during a measured step (`GetCursorPos` the same at every poll). A UIA focus-changed recorder ran from the trigger to 3 s after it. Windows animations were on (the menus slid in).

The issue's setup (#1849's steps 1 and 2): Lecture split right from ⋯, Alpha opened in the right pane, keys in the right pane's editor; then the left pane's ⋯ opened by UIA Invoke (step 3), or by Enter on the button.

| Run | NVDA | Opened by | Pointer | Outcome |
|---|---|---|---|---|
| N1 ×3 | off | Invoke | off the window | stayed on Split Right, 3 of 3 |
| N2 ×3 | off | Invoke | last row's centre | moved to Bulk Rename Properties…, 3 of 3, 130 to 151 ms after Split Right took the keys |
| N3 ×2 | off | Invoke | third row's centre | moved to Add Property…, 2 of 2 |
| X3 ×2 | off | Enter | last row | moved to Bulk Rename Properties…, 2 of 2 |
| V1 ×3 | on | Invoke | off | stayed on Split Right, 3 of 3 |
| V2 ×3 | on | Invoke | last row | moved, 3 of 3 |
| V3 ×2 | on | Enter | last row | moved, 2 of 2 |
| X6 ×2, V6 ×2 | off, on | Enter on Sort and group | its last row, "Enable dual-pane files sidebar" | moved there from "Name (A to Z)", 4 of 4 |
| X7 ×2, V7 ×2 | off, on | Enter on More panels | its last row, "Sync" | moved there from "Embeds", 4 of 4 |
| V4 ×2 | on | Ctrl+Alt+I from a Connections row | off | one focus-changed event, on the editor, 2 of 2 |
| V5 ×2 | on | F6 from a Files row, twice | off | the first F6 to the Alpha tab, the second into the editor; one event each |
| X5 ×2 | on | Enter on a Files row | off | one focus-changed event, on the editor, 2 of 2 |

What NVDA said (log "Speaking" lines, reduced to their text):

- V1 (pointer off): "Editor pane 1 of 2, Lecture." "Outline, 1 heading." "Split Right, Ctrl+backslash, 1 of 3".
- V2 (pointer on the last row): the same three, then "Bulk Rename Properties…, Ctrl+Shift+R, 3 of 3", 52 to 81 ms after Split Right. This is the issue's first record, with the Outline line between.
- V3 (Enter): "enter", "Split Right, Ctrl+backslash, 1 of 3", "Bulk Rename Properties…, Ctrl+Shift+R, 3 of 3".
- V6: "enter", "Name (A to Z), checked, 1 of 9", "Enable dual-pane files sidebar, not checked, 9 of 9".
- V7: "enter", "Embeds, not checked, 1 of 12", "Sync, not checked, 12 of 12".
- V4 (#1846): "ctrl+alt+i", "Right pane hidden.", "Workspace tabs, tab control", "Alpha, tab, selected, 1 of 1", "Alpha.md editor, document, heading level 1, # Alpha": the issue's four lines.
- V5, the F6 that enters the editor from the tab bar: "f6", "Alpha.md editor, document, heading level 1, # Alpha", "Editor pane 1 of 1, Alpha." (the tab control and the tab are already in NVDA's focus ancestry, so they are not named).
- X5 (Enter on a Files row): "enter", "Workspace tabs, tab control", "Alpha, tab, selected, 1 of 1", "Alpha.md editor, document, …": the same two ancestor lines on an ordinary entry into the editor from another region.

The editor's UIA ancestors, nearest first: TabItem "Alpha"; Tab "Workspace tabs" (`WorkspaceTabs`); an unnamed landmark pane; "Editor workspace"; "Workspace"; the window.

What step 0 settles:

1. **#1849's cause is the resting pointer, not NVDA, a key or a UIA SetFocus.** With the pointer off the window the keys stay on the first row every time, with and without NVDA; with it over a row they move to that row every time. The reading's three candidates are reduced to its first.
2. **On this desktop it is not a race.** All 20 opens with the pointer over a row other than the landing one moved the keys (12 on ⋯, 4 on each other menu). So a gate fact that parks the pointer is red on main here.
3. **The popup slides in, and a row can take the keys mid-slide.** Split Right takes them while its rectangle is still 21 to 100 pixels above where it settles. In N3.1 Bulk Rename passed under the pointer during the slide, held the keys for 125 ms, then Add Property took them: WPF hovered on moves that did not change the pointer's place, so the place check in the release is load-bearing. A hold begun before the popup opens covers the slide.
4. **The two other code-built menus have the defect,** 4 of 4 each.
5. **#1846 is what the reading said:** one focus change; NVDA names the editor's two ancestors on any entry from another region (the pane hide and Enter on a Files row alike). CD16 accepts that.

Not settled: in N2.1 the menu closed by itself about 0.45 s after the keys moved to Bulk Rename, and focus then alternated four times between a vanished element and ⋯, about 160 ms apart (run A's report); it was not seen in the eleven later opens and is not explained. NVDA said nothing when the pointer was parked by `SetCursorPos`; a real mouse move may add NVDA's mouse-tracking speech. Only one monitor, one scale, row-centre pointer points and animations on were tried. The Folders menu and the `ContextMenuService` route on ⋯ were not opened.

## Design

### 1. One hold per route (`S/RestingPointerHold.cs`, new)

#1944's remedy moves into one internal type. It keeps at most one hold per route element, so a second begin on a held route never hooks a second set of handlers:

```csharp
internal static class RestingPointerHold
{
    // Where the pointer is now, or null when it cannot be read (no input
    // desktop). A hosted fact replaces it and restores it in a finally.
    internal static Func<(int X, int Y)?> PointerForTests { get; set; }

    // Holds `rows` from the pointer while it rests where it is. On a route
    // with no hold: reads the pointer (nothing is held when it cannot be
    // read), hooks route.PreviewMouseMove and route.PreviewMouseDown once,
    // records the place, and sets IsHitTestVisible = false on each row. On a
    // held route: records the new place and holds the given rows too; no
    // second set of handlers.
    internal static void Begin(UIElement route, IEnumerable<MenuItem> rows);

    // Gives every row this route's hold set back (ClearValue, never true),
    // unhooks once, and forgets the route. Harmless on a route with no hold.
    internal static void Release(UIElement route);
}
```

- The held rows are a snapshot: `Begin` adds the rows it is given to the route's set, and `Release` clears exactly that set, so no row keeps a local false and a row added after the last `Begin` is neither held nor cleared. The app menu's rows change only on a workspace wiring (`S/MainWindow.Graph.cs:14-15`), never while it is open, so its behaviour does not change.
- The handlers release on a `PreviewMouseMove` whose pointer place differs from the recorded one (the place check is what ignores the moves WPF raises while the popup appears and slides under a still pointer, step 0 item 3), and on a `PreviewMouseDown`, which releases and then calls `Mouse.Synchronize()` so the release works the row under the pointer, as #1944 does.
- The table of holds is keyed by the route element (a `ConditionalWeakTable`), so a dropped menu does not keep its rows alive.
- `NativePointer` moves into the type behind `PointerForTests`.

The four menus use it:

- **The app menu** (`S/MainWindow.AppMenu.cs`): `HoldTheRestingPointer` becomes `RestingPointerHold.Begin(MainMenu, RowsOf(AppMenuItem))`; `ReleaseThePointerWhenTheMenuCloses` keeps its guard (`:142-146`) and calls `Release(MainMenu)`; the move and press handlers and `_restingPointer` go. Behaviour unchanged (Call 1).
- **The note header** (`S/NoteHeaderView.cs`, `OpensItsMenu`): when the open is not by pointer (the existing `byPointer`), `Begin(menu, rows)` before `IsOpen = true`; `menu.Closed` calls `Release(menu)`. Both header menus go through this method, so the Folders menu is held too.
- **Shift+F10 and the Apps key on a header button** (found on the way, Small: the same file): `OpensItsMenu` handles the button's `ContextMenuOpening`. When the open came from the keys (`ContextMenuEventArgs.CursorLeft` and `CursorTop` are -1), it marks the event handled and opens through the same non-pointer path (under the button, first row, held); a right-click still opens as WPF opens it, with no landing and no hold, as a pointer's Click does today.
- **Sort and group and More panels** (`S/MainWindow.FilesToolbar.cs`, `S/MainWindow.RailMore.cs`): `Begin(menu, rows)` before `IsOpen = true`, and `Release(menu)` in its `Closed`. These two land their checked row on every open, pointer or not, so the hold covers every open. A pointer's open leaves the pointer on the button: the popup's window overlaps only the button's lower 16 DIP, which at rest holds the shadow margin (rows slide through it while the menu animates in, and the hold covers that), and the first move releases the hold. A touch or pen open is held the same way, until the first move or press.

Why the menu is the route: the move and press handlers have to see input over the popup. A move over the popup reaches the menu's route because the menu is the rows' ancestor; with `StaysOpen` false a `ContextMenu` also takes mouse capture while open, so a move outside it reaches it too (by reading of WPF, not load-bearing: the move that matters is over the popup). Fact 2 shows the stationary click working a row in one `ContextMenu`.

### 2. #1846

No code. The PR records step 0's run on main and the same steps on the head build (V4, V5 and X5 in the NVDA pass), and names #1846 with no closing word before its number. When this PR has merged and the lead says so, the principal posts a comment on #1846 with both runs' four lines and the one focus event, and closes it, as CD16 and the owner's comment of 2026-11-16 say ("this issue ends on that run, by comment"). If the head run does not confirm it (two focus events, or other speech between the pane line and the editor's), #1846 stays open, the question goes back to the owner as CD16 says, and CB-46's table carries it (plan §11).

### 3. What this chunk does not change

The `ContextMenuService` menus other than the two header buttons (the editor's, the tab strip's, the canvas, outline, grid, Connections, graph diagram and link lists; Call 3). The first-row landing itself (`OpensItsMenu`'s Loaded callback, the two `Opened` handlers). #1976 (the File menu resting above its own row, filed by CB-7) is the app menu's placement, not this hold.

## Tests

Gate facts (FlaUI, real pointer and keys). They run in CI on `windows-latest`, whose screen is about 1044 wide at 96 DPI, so each sizes its window as `NoteHeader_NamesKeysAndAxe_AreClean` does (`G/ShellAccessibilityTests.NoteHeader.cs:49-61`), parks the pointer at a point it has read as inside no popup row, and asserts the row it parks on is on screen (`IsOffscreen` false and inside the screen's bounds). Each "stays for 1.5 s" is a poll (a `while` over the focused element until the time is up), not a fixed delay, so `TestWaitCensus` gains no site. Each ends with the pointer at (0, 0) in a `finally`, as #1944's does. Each records whether Windows animations were on.

1. `G/ShellAccessibilityTests.NoteHeader.cs`, `NoteHeader_APointerRestingWhereItsMenuOpensLeavesTheKeysOnTheFirstRow`: #1849's steps (split, the other note in the right pane, keys in the right editor). Opens the left ⋯ once by Invoke with the pointer parked away to read its rows' settled bounds; closes it; parks the pointer on the last row's centre; then (a) Invoke, (b) Enter on the button, (c) Shift+F10 on the button. Each time: the keys are on Split Right and stay there for 1.5 s, then Down reaches Add Property…. Red on main for (a) and (b) (step 0, 5 of 5: the keys go to Bulk Rename Properties…) and for (c) (no row lands; by reading).
2. `NoteHeader_AMovedPointerHoversAndAStillClickWorksTheRow` (its own fact, so each part can run on main): with ⋯ open by Invoke and the pointer resting on the last row, a move of a few pixels within that row gives it the keys; reopened, a click without a move works the row under the pointer (Bulk Rename's sheet opens; Escape). It pins the releases on head; on main it passes (nothing is held), which the PR says.
3. `G/ShellAccessibilityTests.SortAndGroup.cs`, `SortAndGroup_APointerRestingWhereItOpensLeavesTheKeysOnTheCheckedRow`: the pointer parked on an on-screen row that is not the checked one (read from a first open), keys on `SidebarSortAndGroup`, Enter: the keys stay on the checked order row for 1.5 s; a move within the parked row gives it the keys; Escape; every toggle as it was (read before and after). Red on main (step 0, 4 of 4).
4. `G/ShellAccessibilityTests.RailPanels.cs`, `MorePanels_APointerRestingWhereItOpensLeavesTheKeysOnTheLandingRow`: the same with `RightPaneMorePanels`, parked on an on-screen row that is not the landing one. Red on main (4 of 4).

Hosted facts (run in CI; each sets `RestingPointerHold.PointerForTests` and restores it in a `finally`, so the CI session's missing input desktop does not matter):

5. `T/NoteHeaderTests.cs`, `MoreOpenedWithoutTheMouseHoldsItsRowsFromARestingPointer`: (a) open ⋯ by its Invoke pattern: while open, every row's `IsHitTestVisible` is false; close it with no press: every row has no local `IsHitTestVisible` value (pins the release on `Closed`). (b) Reopen; a `PreviewMouseDown` raised on the menu gives every row back. (c) A `PreviewMouseMove` raised with the seam at the recorded place keeps the rows held; with the seam moved, gives them back. (d) A pointer's open (the existing mouse-up then Click shape, `:780-781`) holds nothing. (e) A keyboard `ContextMenuOpening` on ⋯ (`CursorLeft` -1) opens the menu on its first row, held. Red on main: nothing is held, and (e) lands no row.
6. The same (a) and (e) for the Folders menu at a compact width (the existing compact fact's host).
7. `T/RestingPointerHoldTests.cs` (new; unit tests of the new type, proved by their mutations, not red on main): `Begin` at P, `Begin` at P, `Release`, `Begin` at Q, then a move raised with the seam at Q: the rows are still held (fails if any handler kept P); `Release` twice is harmless; a row added after the last `Begin` is not held and not cleared; a row given to a second `Begin` is cleared by the one `Release`; `Begin` with the seam returning null holds nothing.

Regression, not red: the `AppMenu_` facts in `G/ShellAccessibilityTests.AppMenu.cs` (`AppMenu_APointerRestingWhereItOpensLeavesTheKeysOnTheFirstRow` five times; a miss whose message is #1976's, "Eight opens never put a middle row under the pointer", is #1976 and is rerun, up to three times, and the PR says how many), `MoreOpenedWithoutTheMouseLandsOnItsFirstRow`, the Sort and group and More panels journeys (callers of `ToggleSortAndGroupRow` and `OpenRailPanel` in `G/ShellAccessibilityTests.cs`, `G/ShellAccessibilityTests.FilesTree.cs:227`, `:246` and `G/ShellAccessibilityTests.RegionStops.cs:653`), `NoteHeader_NamesKeysAndAxe_AreClean`, and the censuses (`--filter "FullyQualifiedName~Census"`). `ItemContainerNameCensus` keys the two `MainWindow` menus and the header menus by method and local name (`T/Censuses/ItemContainerNameCensus.cs:582-595`), so those names stay. `ShellSealAdmissionCensus` reads only the window's own subscriptions, past-handled `AddHandler` calls and class handlers (`:600-613`, `:739-758`), so `route.PreviewMouseMove +=` is not seen, as #1944's were not.

Mutations (each must turn the named fact red; the PR lists each with its result):

- no hold in `OpensItsMenu`: 1(a), 1(b), 5(a);
- the hold begun at Loaded priority instead of before `IsOpen = true`: 1 with animations on (recorded as not applicable with them off);
- the release on move without the place check: 1 (the slide's still-pointer moves release the hold, step 0 item 3);
- no `Mouse.Synchronize()` after the press release: 2's click;
- no release on `Closed`: 5(a);
- a hold on a pointer's open of ⋯: 5(d);
- a second `Begin` that hooks a second set of handlers: 7's first case;
- no `ContextMenuOpening` route: 1(c), 5(e);
- no hold in each `MainWindow` menu: 3, 4.

## The NVDA pass (full, on the steps)

Under the desktop lock, NVDA 2026.3.1 with debug logging, the step 0 driver, base and head builds, the same window size, animations as found (recorded). Runs on base and head: V1 to V3 (the issue's steps, pointer off and on the last row, Invoke and Enter), Shift+F10 on ⋯ with the pointer on the last row, V6, V7, V4, V5 and X5. On head only: with ⋯ open and the pointer resting on the last row, a real pointer move (a SendInput mouse move) within that row; Alt+F with the pointer resting where File's rows open (the app menu after the refactor); and the Folders menu with the pointer on its last row, if the worker can reach a compact header (a split pane at a narrow window). The worker records what is said; it does not expect a count where NVDA's mouse tracking may also speak.

Expected on head (the PR body records the phrases verbatim, base and head):

- V2, V3 and Shift+F10: "Split Right, Ctrl+backslash, 1 of 3" and no row after it; UIA focus on Split Right for 3 s. Base for Shift+F10: by reading "menu" and then the row under the pointer.
- V6: "Name (A to Z), checked, 1 of 9" and no row after it. V7: "Embeds, not checked, 1 of 12" (or the checked panel's row) and no row after it.
- V4: unchanged from base: "Right pane hidden.", "Workspace tabs, tab control", "Alpha, tab, selected, 1 of 1", "Alpha.md editor, document, heading level 1, # Alpha"; one focus-changed event.
- The move: the row under the pointer (recorded as heard).
- Alt+F: "File" then its first row, and no row after it.

#1849's "Done when" is the V2 and V3 head record.

## Files

Product: new `S/RestingPointerHold.cs`; `S/NoteHeaderView.cs` (`OpensItsMenu`, and the `ContextMenuOpening` route); `S/MainWindow.AppMenu.cs` (onto the shared type, Call 1; CRLF, checked before and after); `S/MainWindow.FilesToolbar.cs` and `S/MainWindow.RailMore.cs` (finds on the way, Call 2). Tests: `G/ShellAccessibilityTests.NoteHeader.cs`, `G/ShellAccessibilityTests.SortAndGroup.cs`, `G/ShellAccessibilityTests.RailPanels.cs`, `T/NoteHeaderTests.cs`, new `T/RestingPointerHoldTests.cs`. New `.cs` files: `git add`, then `python3 scripts/apply-license-header.py`. `dotnet format --verify-no-changes --include` on every edited C# file.

No XAML, no `crates/` file, no binding change. No `A11yEvent` is constructed or moved (contract 38's ledger does not move); no chord, command or help line (the parity matrix does not move). The focus manifest tracks writes to `Visibility`, `IsEnabled` and `Focusable` and collection changes (`T/Censuses/FocusIntegrityManifestCensus.cs:18-27`), not `IsHitTestVisible`, so it does not move. No sentence changes. No Mac source is read, so no census trait is owed.

**Overlap.** Plan §6's `MainWindow.xaml, MainWindow*.cs` row does not name CB-24; this spec adds it for `MainWindow.AppMenu.cs`, `MainWindow.FilesToolbar.cs` and `MainWindow.RailMore.cs` (the wave's docs PR corrects the row). No other C7 chunk edits a `MainWindow*.cs` file, `NoteHeaderView.cs` or the three gate partial files. CB-28's spec edits none of these files, and CB-15a's lists no `MainWindow` site.

## Docs and registers

- The W-C matrix rows for these menus (`docs/plans/18_windows_port/w_c_matrix.md:14`, `:20`, `:41`) describe their structure, names and keys, and claim nothing about a pointer; they are left as they are, as #1944 left the app menu's row.
- No contract register row names these menus' pointer behaviour (a search of `docs/plans` for #1939, #1944 and "resting pointer" finds only plan 47 and a Mac palette row, #1413, in plan 43's reassessment).

## Ledgers

- #1954 (Mac work owed): none. No core change; the Mac's menus are AppKit's.
- #1955 (deferred finds): N2.1's self-closing menu, if it is seen again on base or head during the pass (one line with the run). Call 3's result, if the lead rules to defer rather than file.

## Verification

```
# hosted, red on the tests-only commit and green on the fix
dotnet test apps/slate-windows/SlateWindows.slnx --configuration Release --filter "FullyQualifiedName~NoteHeaderTests|FullyQualifiedName~RestingPointerHoldTests"
# censuses
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --filter "FullyQualifiedName~Census"
# the gate, under the desktop lock: the new facts, then the regressions
dotnet test apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj --configuration Release --filter "FullyQualifiedName~NoteHeader_|FullyQualifiedName~SortAndGroup_|FullyQualifiedName~MorePanels_|FullyQualifiedName~AppMenu_"
# the whole unit run under the lock before the push (c7-process step 10)
```

The four new gate facts run once locally with the window at the CI size (about 1024 by 768) and the screen at 100 % before the push.

## Acceptance

- Facts 1, 3, 4 and 5 red on main's product code and green on head; 2, 6 and 7 green on head with their mutations red.
- The regressions above green (with #1976's misses counted and rerun as stated).
- The NVDA phrases recorded for base and head.
- `gh pr view N --json closingIssuesReferences` lists #1849 only. No commit message or PR text puts a close, fix or resolve word before #1846, #1890 or #1955.
- PR title: `fix(windows): a resting pointer leaves the keys on a menu's first row`. Predicted count: found 3 (the Sort and group menu, the More panels menu, the header's Shift+F10 route), fixed here 3, deferred 0 or 1, filed 0 or 1 (Call 3).

## Calls for the lead

1. **One hold, with the app menu moved onto it.** Recommended: yes. One implementation of the rule for four menus; #1944's code becomes a caller. Cost: `MainWindow.AppMenu.cs` (a CRLF file) is edited; #1944's gate fact, the regression, misses locally 1 in 3 to 1 in 5 runs for #1976's placement reason, so it is run five times and a #1976 miss is rerun; the Codex review reads one more file. The cheaper alternative keeps #1944's code byte for byte here and moves it onto the shared type in a later PR, leaving two copies of the rule meanwhile.
2. **Take the Sort and group and More panels menus in this PR,** red first. Recommended: yes. Each fails 4 of 4 on main; the fix is a few lines each in two files of the `MainWindow` partial class (Medium by AGENTS.md: two other non-test files, one subsystem; within rule 2's cap: three base non-test files, five with them). Cost: two more gate facts in two more partial files, which no other C7 chunk may then edit, and two more NVDA steps.
3. **The other `ContextMenuService` menus** (the editor's, the tab strip's, the canvas, outline, grid, Connections, graph diagram and link lists), opened by Shift+F10 or the Apps key under a resting pointer. Recommended: probe the editor's Shift+F10 (the most used and most exposed: a typing reader's pointer often rests over the text) under the lock during implementation; if it moves the keys, file one issue for the class (High: the fix crosses the editor, canvas, graph, grid and panel subsystems), naming `ContextMenuOpening` as where a hold begins and this chunk's type as the remedy, and leave it out of this PR. Cost: one probe run; the class stays on `main` until its own PR. The alternative, a class handler on `ContextMenu.ContextMenuOpening` here, changes every context menu, right-click opens included.
4. **When #1846 ends.** Recommended: by comment when this PR merges, citing step 0's run on main and the head run (the owner's words would also allow ending it on step 0 alone, which already confirms CD16). Cost: #1846 stays open a few days longer.

Taken without a call, as AGENTS.md's Small rule says: the header's Shift+F10 and Apps-key route (Design 1), in a file the PR already edits, red first (1(c), 5(e)) and named under "Found and fixed here".

## Red team record

A fresh Opus red team read the first draft on 2026-11-18 (its report and the first draft were kept outside the repository): 2 blockers, 5 medium, 14 low. All taken, two in part.

- **B1, hosted facts need a real pointer (taken).** The Namespace app job has no input desktop, so `GetCursorPos` fails there. The type gains `PointerForTests`; facts 5 to 7 use it.
- **B2, a second begin could leak handlers, and fact 7 could not fail (taken in part).** Design 1 defines one hold per route with a snapshot of held rows; fact 7 is rewritten (`Begin` P, `Begin` P, `Release`, `Begin` Q, a move at Q) with a mutation. The red team's extra gate step in `ShellAccessibilityTests.AppMenu.cs` is not taken: the leak is the type's, fact 7 pins it for any caller, and a resting-pointer step on File would inherit #1976's placement misses; the AppMenu partial stays unreserved.
- **M3, the gate facts run in CI (taken):** window sized for the CI screen, parking points read, on-screen assertions, a local run at the CI size.
- **M4, mutation expectations (taken):** the place-check mutation is expected red (step 0's N3.1); the order mutation is "at Loaded priority" with animations recorded; a no-press close arm pins the `Closed` release; the pointer-open exemption has a mutation; fact 2 is its own fact and is said to pass on main.
- **M5, Shift+F10 and the Apps key on the header buttons (taken):** fixed here as a Small find, with facts 1(c) and 5(e).
- **M6, Call 3's list and probe (taken):** the editor's and the tab strip's menus listed; the editor's Shift+F10 is the probe; the issue names `ContextMenuOpening`.
- **M7, Call 1's cost (taken):** #1976's miss rate, the CRLF file, and the byte-for-byte alternative are in the call.
- **Lows 8 to 21 (taken, 16 in part):** the holds are polls (`TestWaitCensus`); the snapshot rule for rows; step 0's counts (20 of 20, 130 to 151 ms, 52 to 81 ms); why #1944's fact reopens; "with the Outline line between"; the capture claim reworded; the 16 DIP overlap and touch and pen; the reason the two `MainWindow` menus hold on every open; the Folders menu covered by a state fact and an NVDA step if reachable, not by a gate fact (in part); NVDA's mouse tracking; #1846's fallback and who comments; closing words in commits; Verification, Acceptance and Docs sections, the regression callers and the W-C matrix rows; the focus manifest's real reason; N2.1's detail.

## What delivery changed (2026-11-18, PR #2014, merged as 5b890dd9)

Merged at 414f8bd6 with main 9999cb68 in it. #1849 closed on the merge. #1846 was closed by comment after the merge, citing step 0's run and the head run (CD16). #2013 was filed for the context menus WPF's ContextMenuService opens (Call 3).

**What the code does that the spec did not say.**

- **Every ContextMenu here fades on close.** The spec said `Slate.ContextMenuStyle` turns WPF's popup animation off. It does not. The style's `Popup.PopupAnimation` setter lands on the ContextMenu itself, and WPF sets the popup's own animation locally from `SystemParameters.MenuPopupAnimation`. A probe saw a styled More panels menu raise `Closed` 185 ms after `IsOpen` went false.
- **Holds left by a fading close.** Three guards came out of that fade:
  - The `Closed` handler ignores a `Closed` from a menu that is open again, so a late `Closed` never ends the next open's hold.
  - A pointer's open of a header menu gives back a hold still standing from a keyboard open whose close is fading (the diff red team).
  - A pointer's `ContextMenuOpening` raised by the header button gives back that hold too and is left unhandled for WPF (Codex round 1: Escape, then a right-click before the fade ends).
- **`RestingPointerHold.PointerForTests` is thread-static,** so a hosted fact's pointer applies only on its own thread and cannot leak into a test class running in parallel.
- **No source check on the keyboard `ContextMenuOpening` route.** The diff red team suggested one. A probe pressed the Apps key on a row of the open ⋯ menu: no listener on the button, the menu or the row saw any request, and WPF closed the menu. A check could not be seen red, so none was added. Shift+F10 on a row was not probed.

**How the facts changed.**

- Fact 1 became a theory over four ways in: Invoke, Enter, Shift+F10 and the real Apps key. Each is red on main on its own. With Invoke and Enter the keys went to Bulk Rename Properties…; with Shift+F10 and the Apps key no row landed.
- Every gate fact asserts, in each open it measures, that the parked row's settled bounds hold the still pointer. This came from the diff red team for fact 1 and from Codex round 1 for the Sort and group, More panels and moved-pointer facts. The rows are read by automation id across the process's windows, sorted by Y, because a read through the first row's UIA parent dropped each menu's first row.
- The hold begun at Loaded priority did not turn the gate fact red on this desktop. A hosted arm pins the order instead: the rows must already be held when the popup raises `Opened`, and the arm fails if `Opened` is never raised.
- Hosted facts pin the hold on a pointer's open of Sort and group and of More panels.
- Hosted facts cover a pointer's open and a right-click during a keyboard open's close fade. The merge judge noted, as an accepted minor, that these two pass vacuously where menus do not fade.
- `AnnouncementNameCensus` requires every shell journey to run the item-name census, so each new gate journey does, with its menu open.
- The gate grew. Its CI run took 25 min 25 s of its 35-minute limit (the judge's other accepted minor).

**What was measured that the spec expected otherwise.**

- On base, Shift+F10 on ⋯ with the pointer on the last row said "menu" twice, and the keys stayed on the menu. The spec expected "menu" and then the row under the pointer. WPF's service places a keyboard open elsewhere, so no row was under the pointer.
- On head, Alt+F with a resting pointer said the first row ("Open Vault… …, 1 of 8"), not "File" first.
- The Folders menu was reached at a compact header (window 1000 DIP wide) and held: "learning, 1 of 2", nothing after.
- The editor's Shift+F10 probe (Call 3) reproduced 4 of 4: the row under a resting pointer took the keys 62 to 191 ms after the menu opened. That is #2013.
- #1944's regression fact on head: 10 runs, 5 misses, every miss with #1976's placement message. In an interleaved control, base missed the same way 2 of 5 runs. In review rounds 2 and 3 it passed first time.
- The run at the CI size used a 1024 by 768 DIP window on this 200 % desktop; the window came out 1024 by 960. The gate process is not DPI aware here, so the journeys' `1500 * scale` sizing fills the screen above 100 %. It is right at CI's 96 DPI. No run at 100 % was made, and CI's animation setting is not known.

**Reviews.**

- Spec red team: 2 blockers, 5 medium, 14 low.
- Diff red team: 0 blockers, 1 major, 12 minors. The major and five minors were taken, and the source check above was declined.
- Codex: round 1 needs-attention, with two medium findings, both fixed red first; round 2 approve.
- The merge judge said Merge, with two accepted minors (above).

Count: found 4, fixed here 3, deferred 0, filed 1 (#2013).
