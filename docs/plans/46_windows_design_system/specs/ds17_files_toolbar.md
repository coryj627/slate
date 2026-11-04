# DS-17 — The Files pane sits under one toolbar

Issues: filed by DS-0. Size L. Lanes: windows, rust, swift (compile). AT: NVDA and Narrator pass. Wave 5. Depends on: DS-4 (the sort menu's check column), DS-9, DS-12 (the vault's name in the title). Decision: DSD9, DSD12. Interlock: the Files tree's name, selection and landing contracts stay untouched.

## Goal

The Files pane opens with the vault's name and a row of icon buttons: New note, Sort and group, Collapse and Expand. The stack of a combo box, a check box and two toggle buttons goes. So does the band above the panes.

## Current state on main

- **The band:** `MainWindow.xaml:925-930`: vault name at 18 with `HeadingLevel="Level1"`, and the path.
- **The header:** `:952-964`: "Files" heading, Refresh (`:957-963`).
- **Filter:** `:988-1010`, with Clear (R-3).
- **Sort:** `ComboBox` `SidebarSortOrder` at `:1012-1029` over `SortModes` (`FilesSidebarViewModel.cs:693`), and `CheckBox` `SidebarGroupDates` at `MainWindow.xaml:1030-1034`.
- **Toggles:** `SidebarShowTags` (`MainWindow.xaml:1037-1040`), `SidebarDualPaneToggle` (`MainWindow.xaml:1041-1045`).
- **Tags:** `SidebarTagTree` at `MainWindow.xaml:1051-1058`.
- **Sections:** expanders for Batch actions (`MainWindow.xaml:1082`), File actions (`:1099`), Shortcuts (`:1142`). Tree at `MainWindow.xaml:1205`.
- **Verbs:** `CollapseAllCommand`, `ExpandLoadedCommand` (`FilesSidebarViewModel.cs:667-668`), `Ids.NewNote` (`Commands/ChordTable.cs:1430`), the Files Sidebar menu at `MainWindow.xaml:239-296`.
- **Tests naming these ids:** `FilesTree_ArrowsKeepFocusEnterOpens` and others in `ShellAccessibilityTests.FilesTree.cs`, plus the sidebar's view-model facts.

## Reference

`design/windows/ui_kits/slate-windows/Sidebar.jsx:1`, `:50-59`: `PanelToolbar title="demo-vault" search={false} sortLabel="Sort and group"`, New note, Sort and group with six modes and a Group by date toggle, Collapse and Expand. `components/patterns/PanelToolbar.prompt.md`. `readme.md:99`.

## Design

1. **`PanelToolbarView`,** a reusable control DS-18 also uses: `MinHeight` 40, title in BodyStrong with an optional count in Caption, then slots for 32 icon buttons (DS-9). Heading level from a property.
2. **The Files toolbar.** Title: the vault's display name, `HeadingLevel="Level1"`, the path as its tooltip, per DSD9. Buttons: New note (`E710`), Sort and group (`E8CB`), Collapse all (`F166`) and Expand loaded (`F164`), Refresh (`E72C`).
3. **Sort and group.** A `Button` opening a `ContextMenu`: one checkable row per `SortModes` member with `SidebarSortModeLabelConverter`'s labels, a separator, and a checkable Group by date. Bound to the same `SortMode` and `GroupByDate`. It keeps every value and label. What a combo gives and a menu button doesn't is its value on focus (DSD12), so the button's name comes from core: a new `sidebar_sort_button_name(mode)` gives "Sort and group, Name (A to Z)", through the FFI, using the labels `SidebarSortModeLabelConverter` already has, and the Mac only compiles against it. Tests naming `SidebarSortOrder` and `SidebarGroupDates` move to the new rows' ids, in this PR.
4. **Where the toggles go.** Tags and Dual pane aren't in the kit's toolbar; the kit shows Tags as a section. Show tags becomes a checkable row in the same menu under a separator, and Dual pane too, both keeping their commands (`slate.sidebar.toggleLayout` at `Commands/ChordTable.cs:1541`). Their names stay "Show tag tree" and "Enable dual-pane files sidebar".
5. **The band goes.** `MainWindow.xaml:925-930` and its row. The `Grid`'s row definitions shift. `FilesPaneBorder` keeps its landmark name. The vault's name is now read at the Files toolbar, and in the window title from DS-12.
6. **Refresh, New note** bind existing commands. The filter stays as it is, in DS-9's search style. `Slate.TreeRowBase` rows don't change. Expanders stay; the kit's Shortcuts and Recents sections are out of this plan (plan 46 §1).
7. **Landing.** `LandOnFilesTree` and `MainWindow.ShellRegions.cs:153-161` don't change, so F6 still lands on the tree, not the toolbar. Toolbar buttons are Tab stops, like Refresh today.

## Tests

Failing first:

- `TheSortMenuListsEveryModeAndChecksTheCurrentOne`.
- `GroupByDateShowTagsAndDualPaneAreCheckableRows`: bound both ways, `FilesSidebarViewModel` state unchanged.
- `TheVaultsNameIsTheLevelOneHeadingInTheToolbar`, `TheBandIsGone`.
- `ThePanelToolbarIsFortyAndItsButtonsThirtyTwo`.
- `F6StillLandsOnTheTree`.
- Holding, with ids updated: the Files gate tests, `SidebarTagTree` facts, `FocusIntegrityManifestCensus`.

Launched app: with and without tags, dual pane on, a long vault name, at 260 wide.

## Docs and registers

`w_c_matrix.md` rows, because a combo became a menu. `parity_matrix.md` if a label moves. `wpf-feasibility.md:145`.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~FilesSidebar|FullyQualifiedName~FocusIntegrity|FullyQualifiedName~ShellRegion|FullyQualifiedName~SidebarSort"`, and the Files gate tests locally.

AT: NVDA and Narrator pass: Tab across the toolbar; open Sort and group; hear each mode and which is checked; change it; Esc returns to the button; heading navigation finds the vault. Record the phrases, and say plainly what a reader lost or gained compared with a combo box, since DSD12 keeps the combo if the button reads badly.

## Acceptance

- [ ] Every setting the stack offered is reachable from the toolbar, by the same names.
- [ ] The pass is recorded.
- [ ] Nothing clips at the pane's 260.

## Brief

Read `MainWindow.xaml:925-1060`, `FilesSidebarViewModel.cs:693-740`, `:926-945`, `Sidebar.jsx:50-70`, `PanelToolbar.prompt.md`, `MainWindow.ShellRegions.cs:153-161`. Build `PanelToolbarView`, move the settings to the menu, remove the band, update the ids in tests. Commit: `feat(windows): the files pane sits under one toolbar with the vault's name`.
