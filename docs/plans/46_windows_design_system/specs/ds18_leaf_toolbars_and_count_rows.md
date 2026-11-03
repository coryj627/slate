# DS-18 — Each leaf has the toolbar, and tags use count rows

Issues: filed by DS-0. Size L, and it may split by leaf at the implementer's call. Lanes: windows. AT: NVDA spot. Wave 5. Depends on: DS-9, DS-16, DS-17 (`PanelToolbarView`). Decision: none. Interlock: row names come from core through `SiblingNames`; they stay.

## Goal

Each right-pane leaf opens with the same 40 row: its title, and its count where it has one. Tags draw as count rows, label on the left and count on the right. Backlinks keep their rows, each one occurrence with its context.

## Current state on main

- **Headings:** `PanelHeading` at `MainWindow.xaml:1324-1329`, used per leaf, for example `:1401-1403` (`Panels.BacklinksHeader`). The others: `:2131`, `:2341`, `:2465`, `:2490`, `:2552`, and `Graph/ConnectionsLeafView.cs:300`.
- **Backlinks are occurrences.** `PanelBacklinksList` at `MainWindow.xaml:1412-1420` on `PanelRowContainer` (`MainWindow.xaml:1331-1334`), names through `SiblingNames`. A row is one link (`crates/slate-uniffi/src/lib.rs:3682-3688`): `BacklinkRowViewModel` (`Panels/RightPanePanelsViewModel.cs:1141-1160`) holds the file name and snippet, has no count, and is named "Backlink from {file}, context: {snippet}". One page loads, at most 200 (`Panels/RightPanePanelsViewModel.cs:30`). The header's number is every occurrence, from core's total (`Panels/RightPanePanelsViewModel.cs:501`, `RightPanePanelsTests.cs:543`). The kit's rows count mentions per note (`ui_kits/slate-windows/Panes.jsx:39`), and that can't be derived from a truncated page.
- **Tags have real counts.** `SidebarTagTemplate` (`WorkspaceTemplates.xaml:215`), `SidebarTagTree` (`MainWindow.xaml:1051-1058`) on `Slate.TreeRowBase`. `SidebarTagViewModel` (`FilesSidebarViewModel.cs:414-438`) draws `DisplayLabel`, "{Segment} ({FileCount})", and is named "{Segment}, {FileCount} files".
- **Row hover:** `Slate.PanelRowTemplate` (`Themes/Slate.Text.xaml:110-112`) uses `Slate.RaisedSurfaceBrush`. The design's `--surface-hover` is `Slate.HoverOverlayBrush`.
- **Tertiary text on a selected row is new.** `Slate.TertiaryTextBrush` draws only in the reading document today (`Reading/ReadingDocumentBuilder.cs:524`). In High Contrast it's window text (`Themes/Slate.Contrast.xaml:24-25`), while a selected row is `Highlight`.

## Reference

`design/windows/components/patterns/PanelToolbar.prompt.md`, `CountListRow.prompt.md`. `ui_kits/slate-windows/Panes.jsx:22-28`: which leaves take a count, sort and expand; `Panes.jsx:34`, `Panes.jsx:39-44`. `wpf-feasibility.md:148`: "Fold the count into the name".

## Design

1. **Toolbars.** Every leaf's heading becomes `PanelToolbarView` with `HeadingLevel` 2 and the same text binding. A count where `Panes.jsx:22-28` shows one and the leaf has the number: Backlinks, Outgoing links, Citations. No leaf has sort, expand-all or search verbs today (searched), and a button that does nothing is worse than none, so none is drawn (plan 46 §1). The slots stay in `PanelToolbarView` for when a leaf has one. Bibliography (`MainWindow.xaml:2005`) keeps its own field, per "`search={false}` when the panel already has a filter field".
2. **The count's name.** It's in the heading's text only if core's header string already says it (`Panels.BacklinksHeader`). Otherwise it's a presentation-only caption, and a number a reader wants goes through core.
3. **`Slate.CountRowTemplate`,** on `Slate.TreeRowBase`, for tags. 32 medium, 24 small; icon 16, label Body from `Segment`, count Caption from `FileCount` with tabular figures in `Slate.TertiaryTextBrush`, right-aligned; selected uses the pill and semibold label; hover `Slate.HoverOverlayBrush`.
4. **A selected count.** `Slate.SelectedCaptionBrush`: `Slate.TertiaryTextColor` in light and dark, as the design draws it, and `SystemColors.HighlightTextColorKey` in `Slate.Contrast.xaml`, so High Contrast doesn't put window text on the highlight. It joins `RequiredSlateBrushKeys` (`ThemeManager.cs:37-69`). DS-11's gate holds tertiary to the floor on `SelectedSurface`, so an accepted accent can't break it.
5. **Names don't change.** A tag's name already says its count, so the row needs nothing folded in.
6. **Backlinks keep their rows.** Each is one occurrence, with the file name and the snippet a reader opened the leaf for. They get the toolbar and the new hover, and their content, names and list role stay. Counting mentions per note, or expanding rows into tree nodes, needs a grouped query that counts past the loaded page: core, the FFI and both hosts. That's out of this plan (plan 46 §1).
7. **The hover.** `Slate.PanelRowTemplate`'s hover moves to `Slate.HoverOverlayBrush`, and the `Slate.Contrast.xaml` key arrives from DS-4.

## Tests

Failing first:

- `EveryLeafOpensWithTheToolbar`: a theory over `Leaves`; heading level 2, its text unchanged, no empty button.
- `ACountRowDrawsLabelAndCount`, `ACountRowIsThirtyTwoAndItsSmallTwinTwentyFour`.
- `ASelectedCountRowHasThePillAndASemiboldLabel`.
- `ASelectedCountClearsTheFloor`: a selected tag row in a real window, in light and dark: the count's resolved foreground on the row's resolved background at |Lc| > 75, under gold and, once DS-11 has shipped, under the default blue's computed dictionary. In the contrast dictionary, `Slate.SelectedCaptionBrush` is `HighlightText`.
- `ARowsNameIsUnchanged`: backlink and tag names before and after, from the same fixture.
- `ABacklinkRowKeepsItsFileAndSnippet`.
- Holding: `RightPanePanels_LeafBodiesCarryRowsAndBacklinkNavigates`, `ALocallyStyledRowDrawsTheThemesColors`, `FocusIntegrityManifestCensus`.

Launched app: all 17 leaves, with content where the fixture has it.

## Docs and registers

`w_c_matrix.md` if a heading's level or a stop moved. `wpf-feasibility.md:145`, `wpf-feasibility.md:148`. `CountListRow.prompt.md:1` and `CountListRow.prompt.md:22`: Slate's backlinks are occurrence rows until a grouped query exists.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~RightPane|FullyQualifiedName~Panel|FullyQualifiedName~SiblingNames|FullyQualifiedName~FocusIntegrity|FullyQualifiedName~SidebarTag"`; keep `ConnectionsLeafTests.TheModelOf` out if the filter widens to `Connections`.

AT: NVDA, heading navigation through two leaves; a backlink row and a tag row; the toolbar's buttons where they exist.

## Acceptance

- [ ] All 17 leaves share the row.
- [ ] Row names are unchanged.
- [ ] Backlink rows still show their file and snippet.
- [ ] A selected count clears the floor, and High Contrast keeps its pair.
- [ ] No button is drawn that does nothing.

## Brief

Read `MainWindow.xaml:1304-1376` and each leaf's heading, `Panes.jsx:22-44`, `PanelToolbar.prompt.md`, `CountListRow.prompt.md`, DS-17's `PanelToolbarView`. Convert the headings, add the template for tags and its selected brush, change hover. Leave backlink rows' content alone. Commit: `feat(windows): each right-pane leaf has the toolbar, and tags draw as count rows`.
