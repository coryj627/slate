# DS-16 — The right-pane rail is an icon strip with a More menu

Issues: filed by DS-0. Size L. Lanes: windows. AT: NVDA and Narrator pass. Wave 5. Depends on: DS-4 (the menu), DS-9. Decision: DSD8, DSD19. Owner step: the icons' review, done 2026-11-07 (DSD19). Interlock: `MainWindow.ShellRegions.cs:977-1005` holds the rail's settled selection (#1698).

## Goal

The rail is a strip of icons with a gold pill and tooltips, which gives the pane's content back about a hundred pixels. Five panels stay in the strip, and the other 12 live in a More panels menu, as the design draws it. A reader can reach every panel by keyboard, and can always tell which one is open.

## Current state on main

- `MainWindow.xaml:2560-2579`: `ListBox` `RightPaneLeavesList`, `AutomationId="RightPaneLeaves"`, 17 rows of `DisplayMemberPath="Title"`, name from `Title` (`MainWindow.xaml:2576`), on `Slate.PanelRowBase`. `SelectedItem` binds two-way to `ActiveLeaf` (`MainWindow.xaml:2564`).
- `WorkspaceViewModel.cs:53`: `WorkspaceLeafOption(string Id, string Title)`; `WorkspaceViewModel.cs:2600` `LeafOptions`.
- **Keys and settling:** `MainWindow.ShellRegions.cs:977-1005`. The `RightPaneRail` region is in the ring (`ShellRegionRing.cs:54`).
- **Landings on the rail:** `MainWindow.ShellRegions.cs:228`, `MainWindow.Bases.cs:332`, `MainWindow.Citations.cs:364` and `MainWindow.Citations.cs:693` all call `FocusFirstOrSelectedItem(RightPaneLeavesList)`. The guard is at `MainWindow.ShellRegions.cs:853`. `MainWindow.ShellRegions.cs:102` decides the region from the list.
- **Facts reach panels by their rows.** The shell gate and hosted facts find a row under `RightPaneLeaves` by name (`CommandPaletteTests.ShellKeys.cs:480`, `ShellAccessibilityTests.RegionStops.cs:62`, and others).
- **Widths:** `WorkspaceViewModel.SidebarWidths.cs:40-41`: 270 and 320, against the design's 260 and 300. #1776 widened the right pane because the text rail took room.

## Reference

`design/windows/components/patterns/PaneSwitcher.prompt.md`: `PaneSwitcher.prompt.md:13-15` for the strip, the More slot and the keys. `components/patterns/PaneSwitcher.jsx:6-9`: six slots by default, the last becoming More. `design/windows/wpf-feasibility.md:144`. `ui_kits/slate-windows/Kit.jsx:54-56`, the icons: Outline `E8FD`, Backlinks `E71B`, Outgoing links `E8A7`, Connections `F003`, Graph inspector `E9E9`, Embeds `E8A1`, Math `E8EF`, Code `E943`, Diagrams `E9D2`, Tasks `E9D5`, Tasks Review `E73A`, History `E81C`, Citations `E82D`, Bibliography `E8F1`, Queries `E71C`, Base dock `E8A9`, Sync `E895`. `assessment.md:113`: these are "my picks and need your review". The owner's review (DSD19) changed four: Backlinks is a drawn chain with an arrow coming in, Outgoing links is `E71B` turned 45° to rise to the right, Graph inspector is `F22C`, and Diagrams is a drawn flowchart.

## Design

1. **The strip (DSD8).** `RightPaneLeavesList` stays a `ListBox`, keeping its `x:Name`, `AutomationId`, name, `DirectionalNavigation` and the handlers at `MainWindow.ShellRegions.cs:977-1005`. Its `ItemsSource` becomes `StripLeaves`, the first five of `Leaves`: Outline, Backlinks, Outgoing links, Connections and Graph inspector. The limit is fixed, so a reader hears the same count at any window size. `LeafOptions` keeps all 17 for everything else that reads it.
2. **Selection across the split.** `SelectedItem` binds to a new `StripLeaf`: `ActiveLeaf` when that's in the strip, otherwise null, with a setter that ignores null. Bound straight to `ActiveLeaf`, the list would be handed an item it doesn't hold, and its null could write back over the open panel. `ActiveLeaf` stays the one truth: commands, routes, the palette and the menu all set it.
3. **More panels.** A `Button` in the sixth slot, below the list, in DS-9's subtle icon style at 32 (DSD6): `E712` in an `AutomationPresentationTextBlock`, `AutomationId="RightPaneMorePanels"`, named "More panels", opening a `ContextMenu` in DS-4's template with the other 12, each with its icon and title. The open one shows a check in place of its icon. While one of them is open, the button takes the selected look and the pill, and its `AutomationProperties.ItemStatus` is that panel's title, so a reader can tell from the button what's open. The pass decides whether that's heard. If it isn't, the name gains the panel through core, since anything composed is core's.
4. **Keys.** Tab enters the strip, arrows and Home/End move within it, and More panels is the next Tab stop, as `PaneSwitcher.prompt.md:15` says. A pick from the menu is the same leaf switch as an arrow on the strip, without the hold-to-settle, and focus returns to the button, as any menu does.
5. **The rail's landing.** A new `LandOnRail()` replaces the four landing sites and the guard's body: the open panel's row, or More panels when the open panel is in its menu, or the first row. `MainWindow.ShellRegions.cs:102` counts the button as the rail. `RegionBoundaryCensus` and `FocusIntegrityManifestCensus` gain it.
6. **The icon.** `WorkspaceLeafOption` gains `Glyph`, a turn for it, and a drawn path for the two icons the font lacks (DSD19). `DisplayMemberPath` goes. A `LeafIcon` element draws either one, used by the strip at 20 and by the menu at 16: the glyph in `SymbolThemeFontFamily`, in an `AutomationPresentationTextBlock` (a plain `TextBlock` here would be a Text peer named by a private-use character, which axe fails, whatever the row's own name says), or the path filled with the row's foreground, which has no peer at all, in a 40 × 40 row. The paths are filled outlines on a 20 grid at the font's stroke of 1.1, cut where the links cross, so no gap colour is painted over a hover or selection fill. Selected uses the pill from `Slate.PanelRowTemplate` on the leading edge. The item's name stays `Title` (`MainWindow.xaml:2576`), which is what stops Narrator reading the glyph.
7. **Tooltips.** Title and chord (Tasks Review: Ctrl+R) on each row and on More panels, with `ToolTipService.ShowsToolTipOnKeyboardFocus="False"`, as the prompt allows, so arrowing isn't noisy.
8. **Height.** Vertical strip. Six slots at 40 is 240, so only a very short window scrolls it, through `VerticalScrollBarVisibility="Auto"`, with More panels inside the scroll area.
9. **Widths.** `_rightPaneWidth` becomes 300 and `_filesSidebarWidth` 260. `MainWindow.xaml:937`, `MainWindow.xaml:941` fallbacks follow. Check the leaves at 300 minus the strip: the WrapPanels from #1776 are why this can move.
10. **High Contrast.** The glyphs use the row's foreground, so nothing new.

## Tests

Failing first:

- `TheStripHasFiveAndMoreHoldsTheOtherTwelve`.
- `EveryLeafHasAnIconAndItsNameIsItsTitle`: theory over `Leaves`, strip and menu; no private-use character in a name or in any peer under the row. `FluentShell_...`'s axe run holds.
- `TheSelectedRowDrawsThePill`.
- `OpeningAMorePanelLeavesActiveLeafAlone`: the regression for the null write-back. The strip's selection goes empty and `ActiveLeaf` keeps the panel.
- `AMorePanelIsMarkedInTheMenuAndOnTheButton`: the check, the selected look and `ItemStatus`.
- `ACommandOpeningAMorePanelMarksTheButton`: Tasks Review's chord and the palette.
- `ArrowsStayInTheStripAndMoreIsTheNextTabStop`.
- `PickingFromMoreSwitchesThePanelOnceAndReturnsFocus`, `EscapeLeavesThePanelAlone`.
- `F6LandsOnTheOpenPanelsRowOrOnMore`: both shapes, and each of the other landing sites.
- `AShortWindowKeepsMoreReachable`.
- `TheDefaultWidthsAreTheDesigns`.
- Holding: `ShellRegionRingTests`, the rail's settle facts from #1698, `RightPanePanels_LeafBodiesCarryRowsAndBacklinkNavigates`, `FluentShell_...`. A helper that picks a panel by its row goes through the menu when the panel lives there, and its fact's meaning doesn't change.

Launched app: each leaf opened, from the strip and from the menu, dark; hosted light.

## Docs and registers

`PaneSwitcher.prompt.md`: settled in DS-0, and the icons as reviewed. `wpf-feasibility.md:144`: settled in DS-0. `w_c_matrix.md` rows for the rail's keys.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~RightPane|FullyQualifiedName~ShellRegion|FullyQualifiedName~SidebarWidth|FullyQualifiedName~FocusIntegrity|FullyQualifiedName~RegionBoundary"`.

AT: NVDA and Narrator pass: F6 to the rail and arrow through it ("Backlinks, 2 of 5"); Tab to More panels; open it, arrow, pick Tasks, and hear the panel change once; F6 away and back, landing on More panels; the settled selection speaks once. Record the phrases against the previous recording, which this changes on purpose, and the owner's read of reaching a panel in the menu.

## Acceptance

- [x] The owner has reviewed the icons (DSD19, 2026-11-07).
- [ ] All 17 panels are reachable by keyboard, and a reader can tell which is open.
- [ ] The pass is recorded, with the owner's read.
- [ ] The leaves fit at 300 in the discovery pass.

## Brief

Read `MainWindow.xaml:2548-2580`, `MainWindow.ShellRegions.cs:970-1005`, `MainWindow.ShellRegions.cs:95-110`, `WorkspaceViewModel.cs:53`, `Kit.jsx:54-56`, `PaneSwitcher.prompt.md`, `PaneSwitcher.jsx`, DS-4's menu template. Split the list, add `StripLeaf`, the button, its menu and `LandOnRail`, then the glyphs, item template and tooltips, and move the defaults. Leave the settle handlers alone. Commit: `feat(windows): the right-pane rail is an icon strip with a more panels menu`.
