# DS-8 — The tab strip is the design's TabView

Issues: filed by DS-0. Size M. Lanes: windows. AT: NVDA and Narrator spot. Wave 2. Depends on: DS-2, DS-9 (the close glyph's style). Decision: none. Interlock: the `TabItem` template holds names and states W7-7 settled; they stay.

## Goal

Open notes sit in a 36-high strip of 32-high tabs, 100 to 240 wide, with a kind icon, a close glyph and the open tab joined to the note below. Everything a screen reader hears about a tab stays exactly as it is.

## Current state on main

- `WorkspaceTemplates.xaml:842-926`: a `TabControl` with a local `TabItem` style and template (#1758), `Padding="8,4"`, radius `6,6,0,0`, a 3px bottom border in `Slate.AccentBrush` when selected. About 30 tall in a capture.
- `WorkspaceTemplates.xaml:260-275`: the header: title, dirty marker, and a `Button` with `Content="×"`, named `Close {Title}` (`:271`).
- **Contracts to keep:** `:850` (`AutomationId`, `SiblingNames`), `:871-872` (name and status), `:906-923` (status triggers), `Focusable` and `IsTabStop` following `HasItems` (`:846-847`), `MainWindow.ShellRegions.cs:95` (a focus inside `TabItem` or `TabPanel` is the tab bar).
- **Chords:** `slate.workspace.closeTab` is Ctrl+W (`Commands/ChordTable.cs:737`). Ctrl+F4 and Delete are unbound (`chords.json`).

## Reference

`design/windows/components/shell/TabView.prompt.md`: 32 px tabs, 100 to 240 wide, 8 px top radius, 16 px icon at 8 px, label at 32 px, 40 × 32 close button with a 12 px `E711`; open tab on the note's surface with flared corners, others in secondary text with 1px dividers; Left, Right, Home, End move and open; Delete or Ctrl+F4 closes; only the open tab is a tab stop. `readme.md:99`: strip 36 minimum.

## Design

1. **Template.** A `Slate.TabItemTemplate` in the same place as today's, replacing `WorkspaceTemplates.xaml:881-903`: `MinHeight` 32, `MinWidth` 100, `MaxWidth` 240, radius `8,8,0,0` (plan 46 §2.3), background `Slate.SurfaceBrush` when selected. Two small `Path`s at the bottom corners draw the flare. A 1px `Slate.SubtleBorderBrush` divider between unselected tabs. Hover `Slate.HoverOverlayBrush`.
2. **Selection stays a shape.** The design joins the open tab to the note; `readme.md:110` says selected tabs get a 2 to 3px gold mark. Keep a 2px `Slate.AccentIndicatorBrush` line along the top, so selection isn't colour and tone alone.
3. **Header.** `WorkspaceTemplates.xaml:262-274`: icon `E8A5` (note), `E8A9` (Base), a canvas and a graph glyph by tab kind, at 16, in an `AutomationPresentationTextBlock` (plan 46 §4 rule 10), then the title with `CharacterEllipsis`, then the dirty marker, then the close button in DS-9's subtle style, 40 × 32, glyph `E711` at 12, name unchanged. Fixed-size glyphs per DS-5's census.
4. **The strip.** `MinHeight` 36 on the header panel through the `TabControl`'s template. Tabs that don't fit wrap to a second row, which is WPF's `TabPanel` behaviour today. No overflow menu.
5. **Keys.** Left, Right, Home and End are already `TabControl`'s. Add Ctrl+F4 as a second chord for `slate.workspace.closeTab`, and Delete on a focused `TabItem` only, through the same command, so an unsaved tab's prompt runs the same way. Both in `chords.json`, the parity matrix and `SpokenChords`.
6. **Don't change** the close button's tab behaviour without a pass: it stays as it is today, and the "only the open tab is a tab stop" line is met by the tabs themselves.

## Tests

Failing first:

- `ATabIsThirtyTwoAndTheStripThirtySix`.
- `ATabsWidthStaysBetweenOneHundredAndTwoHundredForty`: short title, long title (with ellipsis).
- `TheCloseButtonDrawsAnIconAndKeepsItsName`: no private-use character in the name, and none in any peer under the tab (axe's `NameExcludesPrivateUnicodeCharacters` is a shell gate failure).
- `AFocusedTabClosesOnDeleteAndOnCtrlF4`: and a dirty tab prompts.
- `TheTabKindPicksItsIcon`: theory.
- Holding: `LayoutItemsControlTests`, `SiblingNames` facts, `ThemedShell_TheMenuBarAndTheOpenTabAreReadable`, `SpokenChords_MenusPaletteAndOverlays_MatchTheTable`.

Launched app: one tab, many tabs wrapping, a dirty tab, a split, each kind.

## Docs and registers

`parity_matrix.md`, `chords.json`.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~Tab|FullyQualifiedName~SiblingNames|FullyQualifiedName~CommandDriftTests"`. `python scripts/generate-parity-matrix.py --check`.

AT: NVDA and Narrator, arrow across tabs (names and "Unsaved changes" unchanged), Delete on one, Ctrl+F4. Record the phrases.

## Acceptance

- [ ] The strip and tabs measure as specified in the launched app.
- [ ] Spoken names and states match the previous recording.
- [ ] Selection is visible with colour removed (the line stays).

## Brief

Read `WorkspaceTemplates.xaml:260-275`, `:842-926`, `MainWindow.ShellRegions.cs:95`, `TabView.prompt.md`, `Commands/ChordTable.cs:737`. Replace the template and header, keep every `AutomationProperties` line. Add the two keys. Facts first. Commit: `feat(windows): the tab strip is the design's tab view, and a focused tab closes on delete or ctrl+f4`.

## Notes from the build

**The strip's offset is the panel's.** `TabPanel` lays out rows without their tabs' margins, so a top margin on a tab overflowed its row. The panel's margin is `8,4,8,-1`, and each row is 32: the first row is 36 from the top with the rule, and a second adds 32.

**An empty pane has no strip.** A `MinHeight` would have left a blank band above the hint, so the panel collapses with no tabs. DS-19 draws that pane.

**The border is beside the header, not around it,** because its top edge would make the tab 33 with the close button's 32.

**The divider is on a tab's left edge, overlapping its neighbour by one.** The open tab is drawn above its neighbours, so it covers the ones beside it without any tab knowing which is open, and `AlternationIndex` 0 hides the first. In a wrapped row, the first tab still shows its divider: `TabPanel` doesn't say where a row starts.

**The canvas icon is ours.** The design has none, so it's `ECA5` (Tiles). A graph is `E9E9` and a Base `E8A9`, as the design's right pane has them. A saved query is `E71C`, and a dashboard is `E9D2`, by the same list.

**Delete has a scope of its own,** `ChordScope.TabStrip`, because two surface rows on one chord need different scopes, and its route is `TabStrip_PreviewKeyDown`. Ctrl+F4 is a row of its own, because a command row carries one chord.
