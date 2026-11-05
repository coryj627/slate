# DS-7 — The status bar is 24 high, and either pane can be hidden, giving its room back

Issues: filed by DS-0. Size M. Lanes: windows, rust, swift (compile). AT: NVDA spot. Wave 2. Depends on: DS-2, DS-9 (the toggle style). Decision: none. Interlock: #1792 already gave the right pane its computed column, floor and splitter, so this chunk adds the Files sidebar's beside them.

## Goal

The status bar is the design's 24 row with a pane toggle in each corner. The Files sidebar can be hidden, which it can't be today, and hiding either pane gives the editor its room.

## Current state on main

- **The bar:** `MainWindow.xaml:753-810`. Fluent's style makes it about 48 tall (plan 46 §2.2). `:786` is a fixed 180 for the progress item. "The bar is its own stop - its items take no focus" (`:748-752`), and `:768` is a focusable Cancel button only while a trash runs.
- **No Files toggle:** `chords.json` has only `slate.workspace.widenFilesSidebar` and `narrowFilesSidebar`. `FilesPaneBorder` (`:943`) has no visibility binding.
- **The right pane's toggle:** `WorkspaceViewModel.cs:2558-2565`, the property at `:2819-2837`, which announces through core (`A11yEvent.RightPaneShown`, `RightPaneHidden`; text at `crates/slate-core/src/a11y.rs:2025`, FFI at `crates/slate-uniffi/src/lib.rs:9611`). Visibility isn't persisted.
- **A reveal isn't finished when the property flips.** `IsRightPaneVisible`'s setter only arms a mount (`WorkspaceViewModel.Connections.cs:116-127`). The route that revealed the pane consumes it at its end (`WorkspaceViewModel.cs:2558-2565`, `WorkspaceViewModel.Connections.cs:134-145`), and while it's armed a leaf switch loads nothing (`WorkspaceViewModel.Connections.cs:165-168`). So a write that stops at the setter leaves Connections stale. `ConnectionsLeafCensus.cs:514` holds every C# writer to the consume, and it can't see a XAML binding.
- **A `ToggleButton` has two input paths, and they differ.** Measured in a throwaway WPF app on .NET 10.0.14: a click flips `IsChecked` and runs `Command`. UIA's `IToggleProvider.Toggle` flips `IsChecked` and doesn't run `Command`, so a `Command` with a one-way binding leaves the button unchecked and the state unchanged. A two-way binding, `IsChecked`'s default, writes back on both paths.
- **A hidden right pane gives back its room (#1792).** Its column, floor and splitter follow `RightPaneColumnWidth`, `RightPaneColumnMinimumWidth` and `RightPaneSplitterWidth` (`WorkspaceViewModel.SidebarWidths.cs`), and the splitter collapses with the pane. The Files sidebar has none of this.
- **Other routes to Files:** Ctrl+Alt+Left at the window's edge (`WorkspaceFocusBoundary.Files`, `WorkspaceViewModel.cs:29-30`, handled at `MainWindow.xaml.cs:947`), and the editor's fallback (`MainWindow.xaml.cs:2409`).
- **A core event has company.** `A11yCorpusCensus.cs:77` lists each event, `tests/fixtures/a11y/corpus.json:93` holds its text, `apps/slate-mac/Tests/SlateMacTests/A11yCorpusCensusTests.swift:70` mirrors the list by hand (`swift-tests.yml:187` runs `swift test`), `crates/slate-uniffi/src/lib.rs:15251-15258` trips locally if the mirror drifts, and `A11yTriggerParityCensus.cs:56-85` needs a row in `38_notification_dispatcher_contracts.md` with a site or a designation per platform.
- **Room arithmetic:** `WidestSidebarWidth` counts a hidden right pane as 0 since #1792, and still counts the Files sidebar's width whether it's visible or not.
- **The ring:** `ShellRegionLayout` (`ShellRegionRing.cs:36-39`) has `HasTabs`, `RightPaneVisible` and `RightPaneHasContentStop`, built at `WorkspaceViewModel.Layout.cs:1037`. `Files` is always present. `MainWindow.ShellRegions.cs:198`, `:220` refuse a hidden right pane.
- **The parity script** extends the Mac catalogue with Windows-only rows at `scripts/generate-parity-matrix.py:420-427`.

## Reference

`design/windows/readme.md:118`: toggles are 24 × 24 flat buttons, pressed while their pane is visible, glyphs `E90C` and `E90D`, Files in the far-left corner, right pane in the far-right, reachable as **Toggle Files Sidebar** and **Toggle Right Pane** in the menus and palette. `ui_kits/slate-windows/Chrome.jsx:125-141`: 12/16 secondary text.

## Design

1. **The bar.** Local values on `ShellStatusBar`, not a XAML style based on Fluent's (plan 46 §4 rule 9): `MinHeight` 24, zero padding, `StatusBarItem` padding `8,0` through `ItemContainerStyle`, Caption's size and family replacing Body's from DS-2. `MainWindow.xaml:786` becomes a `MinWidth` with `MaxWidth`.
2. **The toggles.** A `ToggleButton` in DS-9's toggle style at each end, 24 × 24, glyph `E90C` or `E90D` in an `AutomationPresentationTextBlock`, `Focusable="False"`. `IsChecked` binds both ways to `RightPaneToggle` and `FilesSidebarToggle`, not to the visibility properties, because a reveal has to finish. `RightPaneToggle`'s getter is `IsRightPaneVisible`, and its setter, when the value differs, runs the same `ToggleRightPane()` that `ToggleRightPaneCommand` now calls, so the mount is consumed exactly once on either input path. `FilesSidebarToggle` does the same with item 3's command, so its route moves focus and handles the rest. Both raise when their visibility property changes, so a chord or a route updates the button. `Command` isn't the way, since UIA's Toggle wouldn't run it. Names "Files sidebar" and "Right pane"; the Toggle pattern says whether it's on. Tooltip with the chord for the right pane. They're pointer and UIA paths: the keyboard path is the command, which keeps the rule at `MainWindow.xaml:748-752` and `TheF6OnlyLandingsAreNotTabStops`.
3. **The command.** `ChordTable`: `Ids.ToggleFilesSidebar = "slate.view.toggleFilesSidebar"`, label "Toggle Files Sidebar", section View, no chord. Registrar entry beside `SlateCommandRegistrar.cs:457`. A `MenuItem` beside `MainWindow.xaml:342`. `WINDOWS_ONLY_COMMANDS` row. Rerun `scripts/generate-parity-matrix.py`.
4. **The announcement.** Core gains `FilesSidebarShown` and `FilesSidebarHidden` in `A11yEvent`, beside `RightPaneShown`, with text "Files sidebar shown." and "Files sidebar hidden.", and the FFI arms. Then all the company above: `corpus()`, `corpus.json`, `A11yCorpusCensus.cs`, the Swift mirror (which can't be run here: `swift-tests.yml` proves it), and the plan 38 row, with the Windows site and a Mac designation saying the Mac has no such toggle (`AppState.swift:9640` is only its right pane).
5. **The state.** `IsFilesSidebarVisible`, default true, not persisted, like the right pane. `FilesPaneBorder` and its splitter collapse with it, because a zero-width pane that's still there stays tabbable. A hide moves focus to the editor if it was inside the pane. Commands and routes that need the pane show it first, as `WorkspaceViewModel.Connections.cs:187-189` does for the right pane: Focus Sidebar Filter, New Note from the tree, `SelectPathFromSurface`, Ctrl+Alt+Left at the edge. The editor's fallback at `MainWindow.xaml.cs:2409` returns `EditorFallback.None` when hidden, since a fallback shouldn't open a pane.
6. **The columns.** #1792 did this for the right pane. `GridSplitter` writes `ColumnDefinition.Width` with `SetValue`, and `SidebarResizeTests` relies on a local two-way binding carrying that to `HoldWidth`, so the `Width` binding stays local and points at a computed width: 0 while the pane is hidden, the held width otherwise, ignoring write-backs while hidden, so a width survives a hide and show. `MinWidth` is a binding too, 0 while hidden, because a local 200 would hold the column open. `FilesColumnWidth`, its floor and the splitter's column (`MainWindow.xaml:938`) follow that shape, and `WidestSidebarWidth` counts a hidden Files sidebar as 0 and no splitter.
7. **The ring.** `ShellRegionLayout` gains `FilesVisible`; `IsPresent` removes `Files`; `MainWindow.ShellRegions.cs:153` refuses when hidden.
8. **The census sees XAML.** `ConnectionsLeafCensus` gains `NoBindingWritesAPanesVisibility`: a XAML binding to `IsRightPaneVisible` or `IsFilesSidebarVisible` that can write, whether by `Mode=TwoWay` or on a property that binds two-way by default such as `IsChecked`, fails it, so the next toggle can't skip the route either.

## Tests

Failing first:

- `HidingTheFilesSidebarGivesTheEditorItsRoom`, the twin of #1792's `HidingTheRightPaneGivesTheEditorItsRoom`: hosted; `ContentPaneBorder.ActualWidth` grows by the pane's width plus the splitter.
- `AHiddenFilesSidebarKeepsItsWidth`: hide, show, same width.
- `TheStatusBarIsTwentyFour`: hosted at 14; `ActualHeight` is 24, and grows under DS-5's factor if that has merged.
- `ToggleFilesSidebarIsAPaletteCommandAndAMenuItem`, `CommandDriftTests` holding.
- `ARevealThroughTheToggleLoadsTheCurrentNote`: a theory over a click and `IToggleProvider.Toggle`. With Connections active, hide, change notes, show: the leaf holds the new note's tree, `ConnectionsMountPendingForTests` is false, there's one audible load, and a later leaf switch isn't suppressed.
- `TheToggleAndThePaneAgreeOnEveryPath`: a click, UIA, the chord and a route that reveals the pane each leave `IsChecked` equal to the visibility.
- `NoBindingWritesAPanesVisibility`, with a planted two-way `IsChecked` it must catch.
- `HidingFilesMovesFocusToTheEditor`, on both the toggle's input paths, `AHiddenFilesPaneIsNotATabStop`, `CtrlAltLeftAtTheEdgeShowsFiles`.
- Holding: `SidebarResizeTests`, whose binding assertions now see the computed path.
- `ShellRegionRingTests`: a layout with Files hidden; every existing shape stays.
- Rust: `files_sidebar_shown_and_hidden_speak` beside the right pane's in `a11y.rs`, the corpus list at `a11y.rs:4195`, and the uniffi tripwire. Swift: the mirror, on the lane.

Launched app: both panes hidden, each alone, both shown.

## Docs and registers

`parity_matrix.md` regenerated. `chords.json` projection. `38_notification_dispatcher_contracts.md`'s new row. `design/windows/wpf-feasibility.md`: nothing rated changes.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~ShellRegion|FullyQualifiedName~ModalSurfaceTests|FullyQualifiedName~CommandDriftTests|FullyQualifiedName~SidebarWidth|FullyQualifiedName~ConnectionsLeafCensus|FullyQualifiedName~ConnectionsLeafViewTests"`. For the core change, in PowerShell: `cargo fmt --check`, `cargo clippy --workspace --all-targets --locked -- -D warnings`, `cargo test -p slate-core a11y::`, per-worktree `CARGO_TARGET_DIR`. `python scripts/generate-parity-matrix.py --check`. `swift-tests.yml` proves the Mac compiles.

AT: NVDA, F6 round the ring with Files hidden; the palette's Toggle Files Sidebar speaks core's sentence; the status bar landing still reads its text; a toggle reads as a toggle in object navigation, and toggling the right pane that way with Connections showing loads the current note.

## Acceptance

- [ ] The discovery pass's width measurement shows the editor growing.
- [ ] The bar is 24 in the launched app at 100%.
- [ ] `--check` passes on the matrix.
- [ ] A reveal through either toggle finishes, by pointer and by UIA.

## Brief

Read `MainWindow.xaml:744-810` and `:935-945`, `WorkspaceViewModel.cs:2558-2565`, `WorkspaceViewModel.cs:2819-2837`, `WorkspaceViewModel.Connections.cs:111-170`, `WorkspaceViewModel.SidebarWidths.cs`, `ShellRegionRing.cs`, `WorkspaceViewModel.Layout.cs:1037`, `crates/slate-core/src/a11y.rs:669`, `:2025`. Add the core events first with all their company and regenerate bindings. Then the state, columns (the binding stays local), ring, routes, command, bar and toggles. Facts first. Commit: `feat(windows): the status bar is 24 high with a toggle for each pane, and a hidden pane gives the editor its room`.

## Notes from the build

**The bar's border is the design's hairline, inside the 24.** Fluent's status bar carries a 1 border all round, which made the bar 26 and took the toggles out of the corners. It's zero now, with a 1-high line over the row's top edge, which covers only what's transparent at rest and takes no pointer.

**The toggles disable under a modal surface,** which the spec doesn't list. The menu disables under every one because its commands would run behind a sheet, and the bar's scrim takes no pointer, so a toggle would too.

**Cancel keeps the row at 24 while a trash runs.** Fluent's button is taller, so it has a 24 floor and no vertical padding.

**The progress item's limits are on its column,** at 60 to 180 with a star width, because an `Auto` column would hold it at its floor, and a star column without a limit would centre it in half the bar.

**Showing the Files sidebar holds the right pane's width first.** It's the one that spread into the room the sidebar comes back to, so the sidebar returns at the width it left at. The right pane's own reveal holds Files first, for the same reason.

**The Mac designations cite plan 38,** which holds their rows and is on `windows.yml`'s path list, rather than this spec.

**New Note reveals at the rename hand-off** (`ArmSidebarRename`), so every create that hands off to the name field gets it from one place.
