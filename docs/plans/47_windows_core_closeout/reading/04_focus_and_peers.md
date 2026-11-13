# Reading 04 — Windows shell focus, UI Automation peers, and two class sweeps

Baseline: worktree `<worktree>`, `main` at 1ec9bf31.
All paths below are under `apps/slate-windows/` unless they start with `docs/`.
`src/` means `src/SlateWindows/`, `tests/` means `tests/SlateWindows.Tests/`, `gate/` means
`tests/SlateWindows.AccessibilityTests/`.

Nothing was built or run. "Verified" means read in the code at the baseline. Anything about
what NVDA says, what WPF does inside its own assemblies, or what axe reports is marked
**unverified** unless an issue or a report in the repo records it.

No commit on `main` names #1671, #1783, #1835, #1836, #1846, #1849, #1860 or #1866 as closed
(`git log --grep`). #1866 is named only by #1867's own message, as left open.

---

## Summary of verdicts

| Issue | Verdict | Size |
|---|---|---|
| #1671 | stands; cause unknown, and it is **not** the #1867 mechanism | M |
| #1866 | stands | L |
| #1783 | stands; its named control is gone, the defect is every combo box's | S–M (with #1836) |
| #1836 | duplicate of #1783 | — |
| #1846 | misdescribed (most likely one focus move, and NVDA reading the editor's ancestors) | S to confirm |
| #1849 | stands; cause not knowable by reading | S–M after a diagnosis |
| #1860 | stands | S |
| #1835 | already fixed on main (3035525d, #1856 removed the button) | none |

---

## #1671 — deleting the last note of a date group makes NVDA say "unavailable"

- **Verdict:** stands. Not fixed by WB-6 (the issue was measured on WB-6's branch) and nothing
  since touches the path. It is not an instance of the mechanism #1867 fixed: see Cause.
- **Current state (verified):**
  - The delete: `src/FilesSidebarViewModel.cs:1972-2039` (`DeleteSelectedAsync`). After the
    trash it sets `SelectedNode = null`, calls `RequestSelectionAt(null, …)` (2018-2022), then
    `Refresh()` (2023), then reports "Moved … to the Recycle Bin." (2024). In the app the
    refresh is asynchronous (`src/FilesSidebarViewModel.TreeOperations.cs:220-…`: the
    `_treeUiContext is null` branch at 274-328 publishes in place, and otherwise
    `RefreshTreeAsync` is started at 341), so the sentence is spoken before the tree
    changes. That matches the issue's order.
  - The publication: `ApplyTreeRefresh`, `src/FilesSidebarViewModel.TreeOperations.cs:620-650`.
    It snapshots the tree's own selected row (629-632), publishes
    (`PublishKeepingUnchangedRows`, `src/FilesSidebarViewModel.RescanReconcile.cs:86-115`),
    and reconciles the selection last (650).
  - Before any row leaves, `DeselectBeforeRemoval` (`RescanReconcile.cs:128-141`) sets
    `IsSelected = false` on the row that is about to go. That row still holds the keys.
  - With the group's only note gone, the group is not kept, so the **group's** root row is
    removed from `RootNodes`; the note's row leaves inside it, and its own level is never
    reconciled. With a neighbour, the note's row is removed from the kept group's `Children`.
    That is the only structural difference between the two cases the issue compares.
  - The landing is already in the same dispatcher turn as the removal:
    `ReconcileSelectionAfterPublication` (`src/FilesSidebarViewModel.FileManagement.cs:687-735`,
    the `treeRow` arm at 728-734) records the nearest survivor (`NearestSurvivor`, 923-952;
    the surviving group header qualifies) and raises `TreeSelectionRestored`; the window's
    handler (`src/MainWindow.xaml.cs:619-641`) calls `FilesTree.UpdateLayout()` and
    `LandNearVanishedSelection` (`src/MainWindow.ShellRegions.cs:1183-1187`) at once when
    `KeysOnARemovedFilesRow()` (1192-1195) is true. The queued restore at 630-640 is only the
    fallback.
  - No XAML or code sets `IsEnabled` on a Files tree row: `MainWindow.xaml:1428-1456` (the
    tree and its `ItemContainerStyle`) binds `IsExpanded`, `IsSelected`, the name and the item
    status only.
- **Cause:** unknown — needs a diagnosis step first. What reading rules out: a control being
  disabled by a trigger, and a queued landing (the two halves of #1867's mechanism). So the
  issue's "the detached focused element's state, which is disabled" can only be true at the
  UI Automation level, not in WPF's own `IsEnabled`. Two candidates, both **unverified**:
  1. `DeselectBeforeRemoval` raises a selection change on the focused row a moment before it
     leaves; NVDA handles it after the row is gone and reads a dead element's `IsEnabled` as
     false. This would explain why only the group case speaks: there the row's item peer
     still has a container (its group's generator was never asked to drop it), so the element
     answers instead of failing outright.
  2. WPF's end-of-layout automation pass (inside the handler's own `UpdateLayout()`) reports
     the detached row before the focus change is delivered.
- **Fix shape:** whichever candidate holds, the remedy with no dependence on it is #1867's
  rule applied to a publication: **move the keys first, then let the row go.** In
  `PublishKeepingUnchangedRows`, after `PlanLevel` has decided what is kept and before
  `DeselectBeforeRemoval` and `ReconcileLevel`, when the row holding the keys (the selected
  row or the tree's own selected row) is not kept, raise a "row is leaving" event with the
  survivor computed from the plan; the window focuses that row unselected
  (`SelectorFocus.FocusRowUnselected`) while the leaving row is still in the tree. The
  survivor is a kept row, so it exists before the publication. Where nothing survives, the
  existing after-the-fact landing stays. Files: `src/FilesSidebarViewModel.RescanReconcile.cs`,
  `src/FilesSidebarViewModel.FileManagement.cs`, `src/FilesSidebarViewModel.TreeOperations.cs`,
  `src/MainWindow.xaml.cs` (the `TreeSelectionRestored` handler), `src/MainWindow.ShellRegions.cs`.
- **Failing test first:**
  - Hosted, `tests/FilesRegionLandingTests.Publication.cs`:
    `DeletingAGroupsLastNoteMovesTheKeysBeforeItsRowLeaves` — group by date, two groups, keys
    on the lone note; delete; assert the leaving row's container was not `IsKeyboardFocused`
    at the moment its `IsSelected` went false or its group left `RootNodes`, and that the keys
    moved once, to the surviving group's header. Fails today: the row holds the keys through
    both.
  - Shell gate, `gate/ShellAccessibilityTests.FilesTree.cs`:
    `FilesTree_DeletingAGroupsLastNoteMovesFocusOnce` — registers UIA focus-changed and
    `IsEnabled` property-changed handlers, deletes with the Delete key, asserts one focus
    change and no `IsEnabled=false` event on the element that had focus. This is the issue's
    "records the focus events between the delete and the landing". Whether it fails today is
    **unverified** (it depends on which candidate is true), so the diagnosis runs it first.
- **Size:** M.
- **Lanes:** windows; FlaUI shell gate involved.
- **Screen-reader work:** NVDA spot (the issue's Done-when asks for the two utterances, once).
- **Owner decision needed:** none.
- **Shares files with:** the Files tree chunk (#1668 #1669 #1670: `FilesSidebarViewModel.RescanReconcile.cs`,
  `FileManagement.cs`, `FilesRegionLandingTests.*`); #1866 through `MainWindow.xaml.cs` and the
  focus manifest. Large shared file: `MainWindow.xaml.cs`.

---

## #1866 — a sheet opened over the focused menu or an empty pane's action

- **Verdict:** stands. Every claim checks out in the code. Not reproduced under NVDA (the
  issue says so too).
- **Current state (verified):**
  - The menu: `src/MainWindow.xaml:167-250`. Fourteen sheet triggers set `IsEnabled=False` the
    moment a sheet's property is non-null: 180 AddProperty, 183 BulkRename, 186 CitationDetails,
    189 CitationSummary, 192 FilesCiting, 195 DashboardEditor, 198 BaseQueryBuilder,
    201 TemplatePicker, 204 TemplateFlow, 207 MoveTo, 210 CanvasCardEditor, 213 CanvasCardPicker,
    216 CanvasPrompt, 219 Settings. The "stay on while it holds the keys" triggers #1867 added
    (227-247) cover Quick Open, search and the palette only.
  - The launcher action: `src/WorkspaceTemplates.xaml:215` (`Slate.LauncherRowStyle`), triggers
    at 263-274; the sheet trigger at 271-273 has no keys-within condition. The window flag is
    `IsModalSheetOpen`, `src/MainWindow.ShellRegions.cs:276-303`.
  - **Main path, not an edge:** the launcher's second action is New from Template
    (`WorkspaceTemplates.xaml:1251-1258`, `Workspace.NewFromTemplateCommand`), which opens the
    template picker sheet. Activating it from the keyboard disables the row under its own keys.
  - Every sheet lands its first stop in a queued callback at `DispatcherPriority.Input`:
    `src/MainWindow.Properties.cs:200-202` (Add property), `216-218` (Bulk rename);
    `src/MainWindow.Citations.cs:367, 516, 646, 696` (the citation sheets; found by search,
    not each matched to its sheet); `src/MainWindow.Bases.cs:224` (dashboard editor, query
    builder; the same); `src/MainWindow.Templates.cs:130, 144, 186, 218`;
    `src/MainWindow.MoveTo.cs:85`; `src/MainWindow.xaml.cs:734, 739-740, 748` (the three
    canvas sheets; the card editor's through `QueueCardEditorLanding`, 767-768);
    `src/MainWindow.Settings.cs:48-56`.
  - The same-turn landing exists for the three overlays only: `LandInOpeningOverlay`,
    `src/MainWindow.Palette.cs:70-120` (its callers: `Palette.cs:151`, `MainWindow.Search.cs:53`,
    `MainWindow.xaml.cs:917`). Its `field` parameter is a `TextBox`; several sheets land on a
    list row or a button (Settings: `SettingsCategoriesList`, else `SettingsCloseButton`).
  - A sheet does **not** disable the workspace or the welcome view: their styles
    (`MainWindow.xaml:1031-1057`, `1136-1162`) have only the three overlay triggers. So the
    menu and the launcher are the whole of #1866; an editor or a button under a sheet stays
    enabled, fenced by `SheetKeyboardFence` and the modal key route.
  - A fact pins today's behaviour and must be rewritten, not just added to:
    `tests/EmptyPaneLauncherTests.cs:188` `UnderASheetEveryActionGoesOffInTheTurnItOpens`.
  - The focus manifest's line for the menu is no longer true:
    `docs/plans/18_windows_port/focus_integrity_manifest.json:325` says the menu is
    "disabled only while a modal overlay (Quick Open, Search, the palette) opens and takes the
    keys itself". The sheets disable it too, and take the keys a turn later.
  - **Unverified:** that a sheet's chord reaches the window while the keys are in the menu
    (#1867's round 2 showed it for the overlays' chords).
- **Cause:** as the issue says: the trigger runs in the turn the sheet opens, the landing runs
  a turn later, and the focused control is disabled in between.
- **Fix shape:** the issue's own, in this order.
  1. Generalize `LandInOpeningOverlay` to take a landing (`Func<bool>`) instead of a `TextBox`,
     and call it from each sheet's opening, keeping each queued landing as the fallback only
     when the same-turn one fails (#1867 round 6: a second landing re-selects typed text).
  2. Then let the menu and the launcher rows stay on while they hold the keys under a sheet:
     the menu's keep-on triggers widened from three overlays to `IsModalSurfaceOpen`, and the
     launcher's unconditional sheet trigger removed.
  3. Correct the manifest's three "takes the keys itself" exemptions to name the facts.
  Files: `src/MainWindow.xaml`, `src/WorkspaceTemplates.xaml`, `src/MainWindow.Palette.cs`,
  `src/MainWindow.Properties.cs`, `MainWindow.Citations.cs`, `MainWindow.Bases.cs`,
  `MainWindow.Templates.cs`, `MainWindow.MoveTo.cs`, `MainWindow.Settings.cs`,
  `src/MainWindow.xaml.cs`, `src/MainWindow.ShellRegions.cs`, the manifest.
- **Failing test first:** a new `tests/SheetOpeningTests.cs`, a theory over every
  `ModalSurface` that is a sheet × {the menu bar, an item of an open menu, a launcher action}:
  `NothingHoldingTheKeysGoesOffAsASheetOpens`, using the recorder #1867 wrote
  (`DisabledWithTheKeysInside`, `tests/CommandPaletteTests.OverlayFocus.cs:396`), plus
  `ASheetTakesTheKeysInTheTurnItOpens` per sheet. Both fail today for all fourteen. The
  menu cases need #1867's skip where a desktop refuses a menu the keys
  (`SkipWhereTheDesktopRefusesTheMenu`, same file, 359).
- **Size:** L. Fourteen sheets, and the same change on three overlays took eight Codex rounds
  (keys, text composition, clicks and Invoke reaching what stays enabled under the surface).
- **Lanes:** windows; FlaUI shell gate involved (every sheet journey runs through the changed
  landing).
- **Screen-reader work:** NVDA spot on three sheets: New from Template from the launcher,
  Settings by its chord with the keys in the menu, Add Property from the note header. Each
  should say the sheet's first stop and no "unavailable".
- **Owner decision needed:** none for the fix. One for the sweep; see the cluster section.
- **Shares files with:** #1671 (`MainWindow.xaml.cs`, the manifest); #1849 and #1835 do not
  touch its files. Large shared files: `MainWindow.xaml`, `MainWindow.xaml.cs`,
  `WorkspaceTemplates.xaml`.

---

## #1783 — an open dropdown's rows report their position in the set

- **Verdict:** stands, with a correction. The control it was reproduced on, the sort combo
  box `SidebarSortOrder`, no longer exists: #1856 (3035525d, 2026-11-07) replaced it with the
  Sort and group menu (`src/MainWindow.FilesToolbar.cs:37-89`). The defect is every combo
  box's, so it stands for the eleven that remain (Sweep B).
- **Current state (verified):**
  - No code in the app sets or overrides set information for a combo box row. The only
    `GetPositionInSetCore`/`GetSizeOfSetCore` overrides are the reading view's
    (`src/Reading/ReadingSurface.cs:2090-2093`).
  - No shell gate scans with a combo box's list open. The Settings journey opens the Theme
    list by Alt+Down and closes it without a scan (`gate/ShellAccessibilityTests.Settings.cs:306-316`,
    in `SettingsSheet_NamesFocusAndAxe_AreClean`, 264).
  - The scan itself: `AssertAxeClean`, `gate/ShellAccessibilityTests.cs:1003-1038`; its one
    waiver is the collapsed scroll bar part (1307-1337, `gate/AxeWaiverTests.cs`).
- **Cause:** not confirmed by reading; it is inside WPF. The evidence in the two issues points
  one way: NVDA hears "3 of 3" and "1 of 6" on the row it is on (#1836's comment), and axe
  fails rows of class `ListBoxItem`. WPF's item peer (`ItemAutomationPeer`) works its position
  out from the items control, which is what NVDA's focus event reaches. The open list is also
  a window of its own, whose tree is walked visual by visual, and there the rows are the
  containers' own peers, which report only `AutomationProperties.PositionInSet`/`SizeOfSet`,
  unset here. So a row is likely published twice, once with set information and once without.
  **Unverified**; a ten-minute hosted probe settles it (open a combo box, walk the popup
  root's peers and the combo box's peers, read `GetPositionInSet()` on each).
- **Fix shape:** one place, for every combo box whatever its row style: a class handler on
  `ComboBox` (registered once at start-up) that, as a list opens and when its items change
  while open, stamps each realized container with `AutomationProperties.PositionInSet` and
  `AutomationProperties.SizeOfSet`. Both peers read those first. A style setter cannot do it
  (a style has no index), and two combo boxes have no row style to carry one (Sweep B). A
  `ComboBox` subclass is the alternative and is worse: eleven call sites and the implicit
  style trap of #1767. If the probe shows a different cause, the fix is a peer override
  instead. Files: one new class (for example `src/ComboSetInfo.cs`), `src/App.xaml.cs`.
- **Failing test first:**
  - Hosted, new `tests/ComboBoxSetInfoTests.cs`:
    `EveryRowOfAnOpenComboBoxSaysItsPlaceInBothTrees` — for a combo box of each row-style
    family, open it, and assert every list-item peer under the combo box and under the popup's
    root has `0 < position <= size`. Expected to fail on the popup's tree (**unverified**).
  - Shell gate: `AssertAxeClean(process, "settings-theme-list-open")` after the list is
    expanded in `SettingsSheet_NamesFocusAndAxe_AreClean`, plus a read of each row's
    `PositionInSet`/`SizeOfSet` (the pattern is in `gate/ShellAccessibilityTests.ReadingList.cs:341-396`).
    Fails today by both issues' record.
- **Size:** S–M (S if the probe confirms the cause).
- **Lanes:** windows; FlaUI shell gate involved.
- **Screen-reader work:** NVDA spot: arrow through one open list and record each row (the
  issue's Done-when).
- **Owner decision needed:** none.
- **Shares files with:** #1836 (the same PR). `gate/ShellAccessibilityTests.Settings.cs`.
  No large shared source file.

## #1836 — an open combo box's items report no place in their set

- **Verdict:** duplicate of #1783. Same rule (`ControlShouldSupportSetInfoWPF`), same element
  (`ListItem`, class `ListBoxItem`), same missing scan. #1836 adds the Settings Theme site and
  the NVDA comment that narrows it; #1783 is older, labelled, and has the Done-when. One PR
  closes both.
- **Current state:** its site is `src/MainWindow.xaml:5153` (`SettingsThemeComboBox`), rows on
  `Slate.ComboRowBase` (5165). One correction: "which every combo box in `MainWindow.xaml`
  shares" is not so. Two have inline items and no row style (4171, 4269), and four more are
  built in code.
- **Cause:** as #1783 (not confirmed by reading).
- **Fix shape:** as #1783; no change of its own.
- **Failing test first:** #1783's two; the gate step is on this issue's own site.
- **Size:** none of its own (rides #1783's S–M).
- **Lanes:** windows; FlaUI shell gate involved.
- **Screen-reader work:** NVDA spot, the same one as #1783's.
- **Owner decision needed:** none.
- **Shares files with:** #1783.

---

## #1846 — a hidden pane's keys stop on the tab strip before the editor

- **Verdict:** misdescribed, most likely. The code moves the keys once, into the editor; the
  three utterances are what NVDA says for one focus change that enters the editor from
  outside the tab control. Confirm with one NVDA observation before closing.
- **Current state (verified):**
  - Hiding the right pane under the keys collapses `RightPaneBorder` (`src/MainWindow.xaml:1552`);
    the focus guard lands them through `WorkspaceColumns`' landing
    (`src/MainWindow.ShellRegions.cs:904`), `EditorRegionLanding` (974-1004), which calls
    `FocusEditorPane` (`src/MainWindow.xaml.cs:2455-2530`). For a Markdown tab in source mode
    that is `editor.FocusInputOwner()` (2525-2529): synchronous, answering Landed. The park on
    the tab's own item (`EditorRegionLanding`, 1001-1003) runs only when that is refused or
    pending, and a pending landing is a reading, canvas or graph tab's, none of which is
    "Alpha.md editor, document".
  - A hosted fact asserts exactly one focus change for this hide, from the rail and from the
    leaf: `tests/RightPaneHideLandingTests.cs:30-49`
    `HidingTheRightPaneUnderTheKeysLandsThemInTheEditorOnce` (`Assert.Single(changes)`).
  - The workspace tabs are a plain `TabControl` (`src/WorkspaceTemplates.xaml:1020`) with no
    custom peer. In WPF the selected tab's content sits under its tab item in the automation
    tree (**unverified** here; it is WPF's own peer), so the tab control and the tab are the
    editor's ancestors and NVDA speaks them when focus enters from outside.
  - The repo's own records show the same three utterances on other entries into the editor:
    `docs/plans/18_windows_port/reports/w7_5_nvda_field_pass_2026-09-20.md:26` (selecting a
    file) and `w7_6_nvda_production_run_2026-09-20.md:22, 26`.
- **Cause:** not a second focus stop, on the evidence above. **Unverified** under NVDA at this
  baseline.
- **Fix shape:** none recommended. The confirmation is one agent-driven NVDA run: hide the
  pane with Ctrl+Alt+I from a Connections row, and separately press F6 into the editor from
  Files; if both say the same three lines and a UIA focus log shows one event, close the
  issue with that evidence. Removing the two lines would mean taking the editor out from
  under its tab item in the automation tree (a custom tab peer), which changes object
  navigation everywhere and is not worth two utterances.
- **Failing test first:** none if it closes as described. If the run shows two focus events
  after all: `gate/ShellAccessibilityTests.ShellRegions.cs`,
  `RightPane_HidingItUnderTheKeysMovesFocusOnce`, with a UIA focus-changed recorder.
- **Size:** S.
- **Lanes:** windows (no code expected).
- **Screen-reader work:** NVDA spot.
- **Owner decision needed:** yes. "NVDA names the tab control and the tab whenever the keys
  enter the editor from another region. Is that acceptable?" Recommended: yes; close #1846 on
  the confirming run.
- **Shares files with:** none if closed. Otherwise `MainWindow.ShellRegions.cs`.

---

## #1849 — the note header's ⋯ menu moves from its first row to its last under NVDA

- **Verdict:** stands. No change since #1850 (6064922f) touches `NoteHeaderView.cs`.
- **Current state (verified):**
  - `OpensItsMenu`, `src/NoteHeaderView.cs:530-553`: Click opens the context menu under the
    button; when the open was not by pointer, a callback at `DispatcherPriority.Loaded`
    focuses the first enabled row (550).
  - The menu's `Opened` makes the header's own pane active (`NoteHeaderView.cs:135`,
    `ActivateOwnPane` 504-510, `WorkspaceViewModel.SelectGroupFromKeyboardFocus`,
    `src/WorkspaceViewModel.Layout.cs:91-101`): `ActiveGroup` changes, command states are
    re-raised (`Layout.cs:48`), the pane line is announced. Nothing in that path asks for
    focus.
  - The rows: Split Right, a separator, Add Property…, Bulk Rename Properties… (125-132).
  - The hosted fact covers the other pane's ⋯ (`tests/NoteHeaderTests.cs:740-801`, the split
    case at 789-800) and passes by the issue's record. Note it returns early, passing, when
    the first landing fails on its desktop (753-756).
- **Cause:** unknown — needs a diagnosis step first. Reading finds no code that moves the
  keys a second time. Three things could, all **unverified**, and one trace tells them apart:
  1. **The pointer.** A context menu's top-level row takes the keys when the pointer is over
     it, and a menu that opens under a parked pointer gets a synthesized mouse move at Input
     priority, after the Loaded-priority first-row focus. That gives exactly "first row, then
     another row, no key". It would also explain why the left pane's menu moves and the right
     pane's does not (it depends on where the pointer was left), and why it has only been
     seen in agent-driven runs.
  2. **An Up or Shift+Tab reaching the menu** (the issue's own guess: a "previous" step that
     wraps, since a menu cycles).
  3. **A UIA SetFocus from NVDA** on a row.

  The diagnosis the chunk begins with: behind a `SLATE_TEST_*` seam, log every
  `GotKeyboardFocus` inside the menu with its stack, every key and mouse event at
  `InputManager.PreProcessInput`, and each row's `IsMouseOver`. Run the issue's three steps
  under NVDA twice: pointer parked outside the window, and pointer parked where the left
  menu's last row opens. The second focus change's stack names the mover
  (`MenuItem.OnMouseEnter`, the menu's key handling, or
  `UIElementAutomationPeer.SetFocusCore`).
- **Fix shape:** depends on the trace. For the pointer: a menu opened without a pointer
  ignores mouse-enter until the mouse really moves (WPF does this for submenus). For a key:
  find the sender. For SetFocus: a row peer that refuses it while the menu's own landing is
  pending. File: `src/NoteHeaderView.cs` in every case; the two other code-built context
  menus that land a first row the same way, if any, take the same change (**unverified**
  whether `MainWindow.FilesToolbar.cs` and `MainWindow.RailMore.cs` share the shape).
- **Failing test first:** written after the trace. For the pointer cause it is hosted:
  `tests/NoteHeaderTests.cs`, `MoreOpenedWithoutTheMouseKeepsItsFirstRowUnderAParkedPointer`.
  For the others it is a shell-gate step in `gate/ShellAccessibilityTests.NoteHeader.cs` that
  reads UIA focus for three seconds after the Invoke.
- **Size:** S–M once the cause is known.
- **Lanes:** windows; FlaUI shell gate involved.
- **Screen-reader work:** NVDA full on these steps (the Done-when is a recorded NVDA run).
- **Owner decision needed:** none.
- **Shares files with:** none in this cluster. `NoteHeaderView.cs` is also #1848's (the
  compact width rule, a decision row).

---

## #1860 — a LayoutItemsControl keeps a removed item's raw-view peer

- **Verdict:** stands. #1861 (08ac3086) changed only `src/AutomationLandmark.cs` and two test
  files, and added no hook for a removal.
- **Current state (verified):** `LayoutItemsControl`, `src/AutomationLandmark.cs:272-312`. Its
  host peer's children are reset only when the generator reports `ContainersGenerated`
  (293-302), queued at `DispatcherPriority.Loaded`. A direct removal generates nothing, so
  that never runs. There is no `OnItemsChanged` override. The item peer's own re-read
  (`ReadAgainIfRewrapped`, 387-405) runs only from the host's `GetChildrenCore` (345-357), so
  it cannot drop a peer the host still lists. The hosts in use: the recent vaults
  (`MainWindow.xaml:1095`), the Embeds cards (1860), a third at 2305, the split host and the
  property list editor in `WorkspaceTemplates.xaml`, the search overlay, the builder's
  conditions, and the Bases warning banners (`src/Bases/BaseSurfaceView.cs:181`).
- **Cause:** as the issue says. That the control view stays correct is the issue's statement
  and **unverified** here.
- **Fix shape:** override `OnItemsChanged` in `LayoutItemsControl`; on Remove, Replace and
  Reset, queue the same Loaded-priority `ResetChildrenCache()` on the host's peer. Queued,
  never inline: #1861's shell gate showed an inline reset inside a peer read ends the app
  ("Recursive call to Automation Peer API is not valid"). File: `src/AutomationLandmark.cs`.
- **Failing test first:** `tests/LayoutItemsControlTests.cs`,
  `ARemovedItemLeavesTheHostsRawView` — build a host, read its peer's children once, remove an
  item from the source with no client listening, pump, and assert the host peer's
  `GetChildren()` no longer holds the removed item's peer.
- **Size:** S.
- **Lanes:** windows; run the FlaUI shell gate too (#1861's first push failed it after Split
  Right, and closing a pane removes an item from the split host).
- **Screen-reader work:** none (raw view only).
- **Owner decision needed:** none.
- **Shares files with:** none in this cluster.

---

## #1835 — the checked Dual pane button's text doesn't take the on-accent colour

- **Verdict:** already fixed on main, by removal: 3035525d, #1856 (2026-11-07), two days after
  the issue was filed. The `ToggleButton` with a `TextBlock` child at the old
  `MainWindow.xaml:1153` is gone, and so is the Tags toggle beside it. `SidebarDualPaneToggle`
  is now a checkable menu row, "Enable dual-pane files sidebar"
  (`src/MainWindow.FilesToolbar.cs:89`). The issue's proposed fact ("a checked toggle's text
  meets contrast in both themes") was never written, and there is now no such toggle to
  measure.
- **Current state (verified), the small sweep:**
  - `ToggleButton`s left in XAML: the two status-bar pane toggles (`MainWindow.xaml:932, 996`,
    `Slate.SubtleIconToggleButtonStyle`, a glyph string as content, checked colours at
    `src/Themes/Slate.Text.xaml:252-260`) and a templated chevron (`WorkspaceTemplates.xaml:134`).
    None has a `TextBlock` child. None is built in code (`new ToggleButton`: no match).
  - Radio buttons: all have string content (`MainWindow.xaml:1997-2018, 2232-2239`;
    `src/Canvas/CanvasSurfaceView.cs:1105`, `src/Graph/GraphSurfaceView.cs:639`,
    `src/Panels/HistorySurfaceView.cs:58, 68`), except the note header's Source and Reading
    segments (`src/NoteHeaderView.cs:555-574`), whose label carries the keyed
    `Slate.BodyTextBlockStyle` (no `Foreground` setter, `Slate.Text.xaml:24-30`) and whose
    glyph is a `TextBlock` subclass, which an implicit style does not reach. Both inherit the
    segment's colour. Not the defect.
  - Button-like controls with a `TextBlock` child, whole XAML: two, both plain buttons with
    no checked state: a recent search row (`MainWindow.xaml:3268-3277`) and "Rename property
    across the vault" (`WorkspaceTemplates.xaml:740-748`). The second is command-bound, so
    when its command is off its label may keep the enabled text colour. **Unverified**
    visually; cosmetic.
  - Selected list rows set `Slate.SelectedTextBrush` (`Slate.Text.xaml:466-470`), which in
    light and dark is the same colour as the text an implicit style would give
    (`Slate.Light.xaml:181, 222`; `Slate.Dark.xaml:185, 226`), so no mismatch can show there.
    In a contrast theme it is highlight text on the highlight (`Slate.Contrast.xaml:45-48`);
    whether every row template's text follows it is **unverified** and is the one place this
    class could still bite.
  - No use of an accent-filled button style in the app's own XAML or code.
- **Cause:** as the issue read it (**unverified**: the implicit `TextBlock` style is Fluent's,
  merged at application level by `src/ThemeManager.cs`, and its XAML is not in the repo).
- **Fix shape:** none. Close with a comment naming #1856.
- **Failing test first:** none.
- **Size:** none (a close-out comment).
- **Lanes:** none.
- **Screen-reader work:** none.
- **Owner decision needed:** none. Optional, not recommended as its own chunk: a one-look
  check of a selected row in a contrast theme during any field pass.
- **Shares files with:** none.

---

# Cluster section

## Sweep A — a control disabled or removed while it holds the keys

### What the class is, after reading

Two different things are filed under one symptom.

- **D: disabled under the keys.** `IsEnabled` goes false on the focused control or an ancestor,
  and the keys leave a turn later. This is what #1867 fixed and what #1866 is.
- **R: removed or collapsed under the keys.** The R-5 class. `RegionFocusGuard`
  (`src/RegionFocusGuard.cs`) and the publication keepers land the keys afterwards, and
  `FocusIntegrityManifestCensus` already lists every such site with a witness. NVDA has no
  "unavailable" for a hidden element; the pane-hide transcript in #1846 shows none. #1671 is
  the one R site where it has been heard, by a mechanism not yet known.

### How it was fixed before

- **#1867 (6cbacf4a):** two halves that only work together.
  1. *Land in the same turn:* `LandInOpeningOverlay` (`src/MainWindow.Palette.cs:70-120`)
     brings the visibility bindings up to date, lays out, and focuses the field in the turn
     `IsOpen` changes; a queued landing only as the fallback; the overlay itself holds the
     keys if the field can't.
  2. *Disable only once the keys have left:* each `IsEnabled=False` trigger gained a second
     condition, `IsKeyboardFocusWithin == False` (`MainWindow.xaml:1034-1055, 1139-1160`), or,
     for the menu, a later trigger that keeps it on while it holds the keys (227-247).
  Half 2 without half 1 lets queued keys, text composition, clicks and a reader's Invoke
  reach what stays enabled (#1867's rounds 3 to 5). That is why sheets were left out.
- **#1861 (08ac3086):** a different class (a stale peer), but the same lesson for peers: a
  reset is queued, never done inside a read.
- **The small precedent:** `SidebarFilterClear_Click` (`src/MainWindow.xaml.cs:1916-1917`)
  moves the keys to the filter field in the button's Click, which runs before its command
  turns the button off.

**The general remedy in this codebase: move the keys first, in the same turn; then disable or
remove.** Where the keys can't move first, the control stays on until they have left.

### The instances

"Keys first?" is whether focus is moved before the state changes. "Heard" is whether an NVDA
record exists. Line numbers are at the baseline.

| # | Where | What disables or removes it | Keys first? | Class | Instance? |
|---|---|---|---|---|---|
| 1 | Menu, `MainWindow.xaml:180-221` | 14 sheet triggers | No; sheet landings queued at Input | D | **Yes: #1866.** Not heard |
| 2 | Launcher rows, `WorkspaceTemplates.xaml:271-273` | `IsModalSheetOpen` trigger | No | D | **Yes: #1866.** New from Template (1251-1258) is the main path. Not heard |
| 3 | Menu under the 3 overlays, `MainWindow.xaml:171-179, 227-247` | overlay triggers | Yes (#1867) | D | Fixed |
| 4 | Workspace view, `MainWindow.xaml:1136-1162` | 3 overlay triggers | Yes (#1867) | D | Fixed |
| 5 | Welcome view, `MainWindow.xaml:1031-1057` | 3 overlay triggers | Yes (#1867) | D | Fixed |
| 6 | Launcher rows under overlays, `WorkspaceTemplates.xaml:264-270` | `IsModalSurfaceOpen` | Yes (#1867) | D | Fixed |
| 7 | Status-bar pane toggles, `MainWindow.xaml:932-940, 996-1008` | `IsModalSurfaceOpen` binding | n/a: `Focusable="False"` | D | No |
| 8 | Title bar buttons, `Themes/Slate.Text.xaml:152` | modal trigger | n/a: never focusable (manifest line 9) | D | No |
| 9 | Trash Cancel, `MainWindow.xaml:950-959` | `CancelTrashCommand` off in the Stopping phase (`FilesSidebarViewModel.Trash.cs:22, 37`), then its bar item collapses | No; restore queued at Input after the change (`MainWindow.Trash.cs:46-64`) | D then R | Yes by mechanism. Not heard |
| 10 | Clear filter, `MainWindow.xaml:1269-1276` | `ClearFilterCommand` off once cleared | **Yes** (Click moves focus) | D | No: the precedent |
| 11 | File actions: Delete (1347), Import… (1371), Cancel import (1372), Folder note / Delete folder note (1356-1357); Batch: Move checked to Recycle Bin (1329); Remove shortcut (1395-1398); toolbar Expand loaded (1220-1224) | each button's own command goes off when it runs (`FilesSidebarViewModel.cs:630-678`; an `AsyncRelayCommand` is off while it runs, `VaultLifecycleViewModel.cs:2710`) | No; the guard lands in the Files region after WPF's re-evaluation | D | Yes by mechanism (8 buttons). Not heard |
| 12 | Retry sidebar settings, `MainWindow.xaml:1235-1249` | its panel collapses with the notice | No; guard after | R | R-5, guarded |
| 13 | Bulk rename Preview / Apply, `MainWindow.xaml:3750, 3756` | `IsEnabled` bound to `CanPreview` / `CanApply` | No; guard: `BulkRenameOverlay` | D | Yes by mechanism. Not heard |
| 14 | Citation summary Walk through, `MainWindow.xaml:3958` | `IsEnabled` bound to `CanWalkThrough` | No; guard | D | Yes by mechanism. Not heard |
| 15 | Queries leaf action groups, `MainWindow.xaml:2439, 2511, 2569` | `IsEnabled` triggers | One has a keeps-the-keys fact (`RightPaneLeafRegionTests.PinFromItsButtonKeepsTheKeysOnTheButton`); two are guard-only | D | Two by mechanism. Not heard |
| 16 | Property row buttons, `WorkspaceTemplates.xaml` `PropertyRowTemplate` #0 and #2 | `IsEnabled` triggers | No; guard: `ContentPaneBorder` | D | Yes by mechanism. Not heard |
| 17 | Code-built: Bases quick filter and view picker (`BaseSurfaceView.RenderChrome`), Connections depth (`ConnectionsLeafView.RenderCore`), graph inspector sections (`GraphInspectorView.RenderGate`) | a render writes `IsEnabled` | No; guard | D | Yes by mechanism (4 sites). Not heard |
| 18 | Review's Load more, `PanelReviewLoadMore` | collapses on the last page | No; own stranded landing at Loaded | R | R-5, witnessed |
| 19 | Files tree row removed by a publication, with its date group | `ReconcileLevel` removes the group | No; landed in the same turn, after | R | **Yes: #1671.** Heard |
| 20 | Files tree row removed, neighbour survives | `ReconcileLevel` removes the row | No; same turn, after | R | Same code; not heard (the issue's control case) |
| 21 | Right pane hidden under the keys, `MainWindow.xaml:1552` | `Visibility` binding | No; guard, one focus change | R | R-5, witnessed; no "unavailable" in #1846's transcript |
| 22 | Files pane and splitters hidden; leaf switched; vault opened or closed | `Visibility` bindings and triggers | No; guard or gone-landing | R | R-5, witnessed (manifest 202-204, 220, 240-241, 307-342) |
| 23 | Sheet and overlay close | overlay `Visibility` | No; each restore queued at Input | R | R-5; exempt in the manifest (168-238) |
| 24 | Every other row, list and panel a publication rebuilds | collection changes, `ItemsSource` | No; keepers at Loaded | R | R-5: the manifest's ~170 remaining lines |

**Count.** D class: 2 open sites that are the named issue (#1866), 4 fixed by #1867, 2 not
focusable, 1 already keys-first, and **6 further groups (about 20 controls) that are instances
by mechanism and have never been heard** (rows 9, 11, 13, 14, 15, 16, 17). R class: #1671,
plus the guarded sites the manifest already owns.

**A gap in the existing census.** `FocusIntegrityManifestCensus` scrapes writes, bindings and
triggers on `IsEnabled`, `Visibility` and `Focusable`. It does not scrape `Command=`. A
command-bound button goes disabled through `CanExecute`, and there are 249 `Command="{Binding`
in the shell's XAML (205 in `MainWindow.xaml`, 39 in `WorkspaceTemplates.xaml`, 5 in
`FindInNoteBar.xaml`), many of them menu rows. Row 11 was found by hand in the Files pane
only; the rest are **not enumerated**.

### The census and facts that keep it closed

Model: `tests/Censuses/FocusIntegrityManifestCensus.cs` with
`docs/plans/18_windows_port/focus_integrity_manifest.json` (site scrape, a named witness per
site, `SLATE_FOCUS_MANIFEST_UPDATE=1` to regenerate, planted-source facts that prove the
scrape discriminates). The smaller model for the per-surface half is
`ModalSurfaceTests.TheMenuDisablesUnderEveryModalSurface` (`tests/ModalSurfaceTests.cs:1752`,
its map `MenuDisableBindings` at 675).

1. **A hosted theory over the enum, for #1866.** `SheetOpeningTests.NothingHoldingTheKeysGoesOffAsASheetOpens`
   over every `ModalSurface` × {menu bar, open menu item, launcher action}, with a
   sheet-opener map that has no wildcard arm, so a fifteenth sheet without a row fails the
   build or the fact. The recorder is `DisabledWithTheKeysInside`, moved out of
   `CommandPaletteTests.OverlayFocus.cs:396` into a shared helper.
2. **A new witness kind in the manifest census, for D sites.** Today any D site can be
   witnessed by `guard: <scope>`, which proves where the keys land afterwards and nothing
   about the turn in between. Add `keys-first: Class.Method`: a fact that uses the shared
   recorder and names the site. Every `IsEnabled` site on or above a focusable element must be
   `keys-first:`, `exempt: not focusable …`, or `accepted: <the owner's dated decision>`. The
   three "takes the keys itself" exemptions (manifest 325, 344, 345) become `keys-first:`
   lines naming #1867's facts.
3. **A new site kind, `xaml-command`,** for a `Command=` on a focusable button-like element
   outside a menu popup, held to the same three witnesses. This is what turns row 11 from a
   hand count into a list.
4. **For #1671, the shell-gate journey** in its section: UIA focus and `IsEnabled` events
   recorded across the delete. It is the only automated check that sees what a screen reader
   sees.

## Sweep B — an open dropdown's rows report no place in their set

### Every combo box, dropdown and popup list

| # | Control | Where | Rows' style | In a gate journey? |
|---|---|---|---|---|
| 1 | Add property type (`AddPropertyType`) | `MainWindow.xaml:3596` | `Slate.ComboRowBase` (3602) | not opened |
| 2 | Bulk rename key type (`BulkRenameOldKeyType`) | `MainWindow.xaml:3694` | `Slate.ComboRowBase` (3704) | not opened |
| 3 | `DashboardEditorQueryPicker` | `MainWindow.xaml:4092` | `Slate.ComboRowBase` (4103) | opened, not scanned open (`gate/ShellAccessibilityTests.cs:4534`) |
| 4 | Dashboard section "View override for …" | `MainWindow.xaml:4171` | **none**: three inline `ComboBoxItem`s, Fluent's implicit style | not opened |
| 5 | `BuilderCombinatorBox` | `MainWindow.xaml:4269` | **none**: two inline `ComboBoxItem`s | not opened |
| 6 | `SettingsThemeComboBox` | `MainWindow.xaml:5153` | `Slate.ComboRowBase` (5165) | opened by Alt+Down, not scanned open (`gate/ShellAccessibilityTests.Settings.cs:306-316`) |
| 7 | `SettingsAccentComboBox` | `MainWindow.xaml:5176` | `Slate.ComboRowBase` (5188) | not opened |
| 8 | Bases view picker (`BaseViewPicker`) | `src/Bases/BaseSurfaceView.cs:91` | built in code, `ClassicRows.Apply` (1328-1334) | selected through UIA (`gate/…cs:10704`) |
| 9 | Connections depth (`ConnectionsDepth`) | `src/Graph/ConnectionsLeafView.cs:306` | built in code, `ItemContainerNames.BySelf` → `ClassicRows.Apply` (`AutomationLandmark.cs:24`) | selected through UIA (11489-11494) |
| 10 | Graph inspector ring style, one per group | `src/Graph/GraphInspectorView.cs:503` | built in code, `PickerItemStyle` (555-560) | **unverified** |
| 11 | Graph inspector colour, one per group | `src/Graph/GraphInspectorView.cs:515` | the same | **unverified** |

Templates: `Slate.ComboRowBase` is `src/WorkspaceTemplates.xaml:58-64`; its template
`Slate.ComboRowTemplate` is `src/Themes/Slate.Text.xaml:475-507`; code-built rows take the
same template by key (`src/ClassicRows.cs:17-22`). #1783 already showed the row template is
not the cause (it failed on classic rows and on themed ones).

Other popups, for completeness: one `DatePicker` (`WorkspaceTemplates.xaml:444`; its calendar
is not a list); nine context menus (`MainWindow.xaml:1677, 1733`; `WorkspaceTemplates.xaml:827, 1031`;
`MainWindow.FilesToolbar.cs:37`; `MainWindow.RailMore.cs:39`; `NoteHeaderView.cs:128, 375`;
`Canvas/CanvasOutlineView.cs:269`; `Graph/ConnectionsLeafView.cs:1056`;
`Grids/AccessibleDataGrid.cs:1745`). Menu rows report their place: NVDA says "Split Right …
1 of 3" in #1849. Quick Open's, the palette's and search's result lists are lists inside
their overlays and are scanned open already. So the class is the combo boxes: **11 sites,
three row-style families, one defect.**

### Are #1783 and #1836 one defect?

Yes. Same axe rule, same element type and class, and the same missing gate step. #1783's
control was removed by #1856; #1836's is still there. Close both in one PR.

### Why, and the fix

See #1783's Cause and Fix shape. In one line: the rows are most likely published a second
time under the open list's own window, as container peers that read only the attached
`AutomationProperties.PositionInSet`/`SizeOfSet`, which nothing sets. **Unverified**; the
chunk's first step is the hosted probe. The fix is a class handler on `ComboBox` that stamps
those two attached properties on each realized row as the list opens and when its items
change, in one place for all eleven.

### Where the gate runs axe, and how the open-dropdown scan is added

- The gate is the FlaUI project `tests/SlateWindows.AccessibilityTests`. Every journey calls
  `ShellAccessibilityTests.AssertAxeClean(process, surface)`
  (`gate/ShellAccessibilityTests.cs:1003`), which waits for realized rows' rectangles, checks
  item names, scans every window of the process with axe-windows, writes the evidence file
  and fails on any error but the one waived shape.
- Add, in `SettingsSheet_NamesFocusAndAxe_AreClean` (`gate/ShellAccessibilityTests.Settings.cs:264`),
  between the `Expanded` assertion (311) and the Escape (312):
  `AssertAxeClean(process, "settings-theme-list-open")`, and a loop over the list's
  `ListItem`s asserting `PositionInSet` and `SizeOfSet`. That covers family 1.
- For the other two families, one open-and-scan each in journeys that already reach the
  control: Connections depth (`gate/ShellAccessibilityTests.cs:11486-11494`, code-built rows)
  and the query builder's combinator (inline rows; the builder journey reaches it at 4441).
- A hosted census, `tests/Censuses/ComboBoxCensus.cs`: scrape `<ComboBox` in the shell's XAML
  and `new ComboBox` in its code (Roslyn, as the manifest census does) and hold the list to
  the eleven above, each named with the family it belongs to and the gate journey or hosted
  fact that opens it. A twelfth combo box then fails until someone says how it is covered.

## Recommended cut into PRs

| PR | Issues | Why together | Order |
|---|---|---|---|
| 0 (no PR) | #1835 | Close with a comment naming #1856. Goes with the plan's first-step close-outs | first |
| 1 | #1860 | One file and one fact; shares nothing | any time; parallel-safe |
| 2 | #1783 + #1836 | One defect, one fix, one gate step, one census | parallel-safe with 1 and 3 (touches no `MainWindow*` source) |
| 3 | #1846 + #1849, as one NVDA session | Both begin with an agent-driven NVDA observation; one session answers both. #1846 then closes on evidence or gets a row; #1849's fix is its own small PR in `NoteHeaderView.cs` | the session early; #1849's PR parallel-safe with 1 and 2 |
| 4 | #1866, with the Sweep A census | The fourteen sheets, the menu, the launcher, the theory over the enum, and the manifest's new witness and site kinds | after 2 and before 5: it owns `MainWindow.xaml`, `MainWindow.xaml.cs`, `WorkspaceTemplates.xaml` and the manifest while it is open. Codex review, as #1867 had |
| 5 | #1671 | A different mechanism and different files from #1866. Its files are the Files tree chunk's (#1668 #1669 #1670), so it should ride with or directly follow that chunk, not #1866 | after 4 (shared `MainWindow.xaml.cs` and manifest), and sequenced with the Files tree chunk |

The brief's sketch puts #1671 and #1866 in one sweep. They share a census, not a PR: put the
census in PR 4 and have PR 5 add its line.

## Owner decisions this cluster adds

1. **#1846:** "NVDA names the tab control and the tab whenever the keys enter the editor from
   another region. Acceptable?" Recommended: yes; close on the confirming run.
2. **Sweep A's scope:** "Does a button that goes unavailable because the reader just
   activated it (Delete, Apply, Cancel, Remove shortcut; rows 9, 11 and 13 to 17) count as
   this defect?" None has been heard. Recommended: the sweep's NVDA session presses each
   once. Any that says "unavailable" and then moves the reader somewhere else is fixed in PR 4
   by the Clear filter pattern (move the keys in Click); the rest are entered in the census
   as `accepted`, dated, with this decision's number. Without this answer PR 4 cannot be
   sized past the two named sites.

## Risks

- **#1866 is the #1867 problem fourteen times.** Keeping the menu or a launcher row enabled
  under a sheet is only safe once the sheet has the keys in the same turn. Sheets whose first
  stop is not a text box, or is not realized in the opening turn (the template picker's list
  while it loads, the canvas card editor that waits beneath another surface,
  `MainWindow.xaml.cs:757-791`), need the "overlay holds the keys itself" fallback, and each is
  a place for a regression in Escape, typing or Invoke under the sheet.
- **The menu facts skip on the hosted runners** (no foreground window, #1680). A third of
  PR 4's theory will report skipped in CI and must be run locally and said so in the PR.
- **Sweep B's cause is a reading of WPF from memory.** If the probe shows the rows are not
  published twice, the fix changes shape (a peer override) and the size goes to M.
- **Stamped set information can go stale** if a list's items change while it is open; the
  handler must restamp on change, and the hosted fact should include an item added while open.
- **#1671's fix moves the keys before a publication.** It must not move them when a modal
  surface is open or when the keys are not in the tree, and it must not select the survivor
  (a selection opens a note). The existing after-the-fact landing has all three guards
  (`MainWindow.xaml.cs:621-623`); the new one needs them too.
- **#1849 may turn out to be the harness's pointer.** If so, it is still a real defect (a
  sighted keyboard user with a parked mouse gets the same jump), but the NVDA run that closes
  it must control where the pointer is, or it proves nothing.
- **#1860's reset has ended the app once** when done inline (#1861). Keep it queued and run
  the shell gate.
- **Shared files.** PRs 4 and 5 both edit `MainWindow.xaml.cs` and the focus manifest; the
  manifest is one JSON object that every focus PR in the plan appends to, so it conflicts on
  nearly every rebase. Keep those PRs in sequence.
