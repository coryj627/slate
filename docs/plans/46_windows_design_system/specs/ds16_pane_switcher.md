# DS-16 — The right-pane rail is an icon strip that reads as it does today

Issues: filed by DS-0. Size M. Lanes: windows. AT: NVDA and Narrator spot. Wave 5. Depends on: DS-9. Decision: DSD8. Owner step: the icons' review. Interlock: `MainWindow.ShellRegions.cs:977-999` holds the rail's settled selection (#1698).

## Goal

The rail is a strip of icons with a gold pill and tooltips, which gives the pane's content back about a hundred pixels. A reader's experience doesn't change: the same names, one list, the same keys.

## Current state on main

- `MainWindow.xaml:2560-2579`: `ListBox` `RightPaneLeavesList`, `AutomationId="RightPaneLeaves"`, 17 rows of `DisplayMemberPath="Title"`, name from `Title` (`:2576`), on `Slate.PanelRowBase`.
- `WorkspaceViewModel.cs:53`: `WorkspaceLeafOption(string Id, string Title)`; `:2600` `LeafOptions`.
- **Keys and settling:** `MainWindow.ShellRegions.cs:977-999`, `:853`, `:228`. The `RightPaneRail` region is in the ring (`ShellRegionRing.cs:54`).
- **Widths:** `WorkspaceViewModel.SidebarWidths.cs:40-41`: 270 and 320, against the design's 260 and 300. #1776 widened the right pane because the text rail took room.

## Reference

`design/windows/components/patterns/PaneSwitcher.prompt.md`. `design/windows/wpf-feasibility.md:144`. `ui_kits/slate-windows/Kit.jsx:54-56`, the icons: Outline `E8FD`, Backlinks `E71B`, Outgoing links `E8A7`, Connections `F003`, Graph inspector `E9E9`, Embeds `E8A1`, Math `E8EF`, Code `E943`, Diagrams `E9D2`, Tasks `E9D5`, Tasks Review `E73A`, History `E81C`, Citations `E82D`, Bibliography `E8F1`, Queries `E71C`, Base dock `E8A9`, Sync `E895`. `assessment.md:113`: these are "my picks and need your review".

## Design

1. **Keep the list.** Per DSD8, one `ListBox` with all 17, keeping its `x:Name`, `AutomationId`, name, `ItemsSource`, selection binding, `DirectionalNavigation` and the handlers at `MainWindow.ShellRegions.cs:977-999`. No More menu. If the owner chooses the design's overflow instead, this chunk's scope grows by a `Button` and `ContextMenu` and a split selection model, and it needs its own NVDA check.
2. **The icon.** `WorkspaceLeafOption` gains `Glyph`. `DisplayMemberPath` goes. An `ItemTemplate` draws the glyph at 20 in `SymbolThemeFontFamily`, in an `AutomationPresentationTextBlock` (a plain `TextBlock` here would be a Text peer named by a private-use character, which axe fails, whatever the row's own name says), in a 40 × 40 row. Selected uses the pill from `Slate.PanelRowTemplate` on the leading edge. The item's name stays `Title` (`MainWindow.xaml:2576`), which is what stops Narrator reading the glyph.
3. **Tooltips.** Title and chord (Tasks Review: Ctrl+R) on each row, with `ToolTipService.ShowsToolTipOnKeyboardFocus="False"`, as the prompt allows, so arrowing isn't noisy.
4. **Height.** Vertical strip, `VerticalScrollBarVisibility="Auto"`. Seventeen rows at 40 is 680, so a short window scrolls. The selected row scrolls into view.
5. **Widths.** `_rightPaneWidth` becomes 300 and `_filesSidebarWidth` 260. `MainWindow.xaml:937`, `:941` fallbacks follow. Check the leaves at 300 minus the strip: the WrapPanels from #1776 are why this can move.
6. **High Contrast.** The glyph uses the row's foreground, so nothing new.

## Tests

Failing first:

- `EveryLeafHasAnIconAndItsNameIsItsTitle`: theory over `Leaves`; no private-use character in a name or in any peer under the row. `FluentShell_...`'s axe run holds.
- `TheRailIsOneListOfSeventeen`.
- `TheSelectedRowDrawsThePill`.
- `AShortWindowScrollsTheSelectedRowIntoView`.
- `TheDefaultWidthsAreTheDesigns`.
- Holding: `ShellRegionRingTests`, the rail's settle facts from #1698, `RightPanePanels_LeafBodiesCarryRowsAndBacklinkNavigates`, `FluentShell_...`.

Launched app: each leaf selected, dark; hosted light.

## Docs and registers

`PaneSwitcher.prompt.md`: settled in DS-0, and the icons as reviewed. `design/windows/CLAUDE.md`: why there's no More menu, if that's the owner's choice.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~RightPane|FullyQualifiedName~ShellRegion|FullyQualifiedName~SidebarWidth|FullyQualifiedName~FocusIntegrity"`.

AT: NVDA and Narrator, F6 to the rail; arrow through it: "Backlinks, 2 of 17" and the rest unchanged; the settled selection speaks once. Record the phrases against the previous recording.

## Acceptance

- [ ] The owner has reviewed the icons.
- [ ] Spoken names and positions match the previous recording.
- [ ] The leaves fit at 300 in the discovery pass.

## Brief

Read `MainWindow.xaml:2548-2580`, `MainWindow.ShellRegions.cs:970-1005`, `WorkspaceViewModel.cs:53`, `Kit.jsx:54-56`, `PaneSwitcher.prompt.md`. Add the glyphs, the item template and tooltips, move the defaults. Leave the handlers alone. Commit: `feat(windows): the right-pane rail is an icon strip that reads as it did`.
