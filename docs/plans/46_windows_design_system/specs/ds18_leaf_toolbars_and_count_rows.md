# DS-18 — Each leaf has the toolbar, and tags and backlinks use count rows

Issues: filed by DS-0. Size L, and it may split by leaf at the implementer's call. Lanes: windows. AT: NVDA spot. Wave 5. Depends on: DS-9, DS-16, DS-17 (`PanelToolbarView`). Decision: none. Interlock: row names come from core through `SiblingNames`; they stay.

## Goal

Each right-pane leaf opens with the same 40 row: its title, and its count where it has one. Tags and backlinks draw as count rows, label on the left and count on the right.

## Current state on main

- **Headings:** `PanelHeading` at `MainWindow.xaml:1324-1329`, used per leaf, for example `:1401-1403` (`Panels.BacklinksHeader`). The others: `:2131`, `:2341`, `:2465`, `:2490`, `:2552`, and `Graph/ConnectionsLeafView.cs:300`.
- **Backlinks:** `PanelBacklinksList` at `MainWindow.xaml:1412-1420` on `PanelRowContainer` (`MainWindow.xaml:1331-1334`), names through `SiblingNames`.
- **Tags:** `SidebarTagTemplate` (`WorkspaceTemplates.xaml:215`), `SidebarTagTree` (`MainWindow.xaml:1051-1058`) on `Slate.TreeRowBase`.
- **Row hover:** `Slate.PanelRowTemplate` (`Themes/Slate.Text.xaml:110-112`) uses `Slate.RaisedSurfaceBrush`. The design's `--surface-hover` is `Slate.HoverOverlayBrush`.

## Reference

`design/windows/components/patterns/PanelToolbar.prompt.md`, `CountListRow.prompt.md`. `ui_kits/slate-windows/Panes.jsx:22-28`: which leaves take a count, sort and expand; `:34`, `:39-44`. `wpf-feasibility.md:148`: "Fold the count into the name".

## Design

1. **Toolbars.** Every leaf's heading becomes `PanelToolbarView` with `HeadingLevel` 2 and the same text binding. A count where `Panes.jsx:22-28` shows one and the leaf has the number: Backlinks, Outgoing links, Citations. No leaf has sort, expand-all or search verbs today (searched), and a button that does nothing is worse than none, so none is drawn (plan 46 §1). The slots stay in `PanelToolbarView` for when a leaf has one. Bibliography (`MainWindow.xaml:2005`) keeps its own field, per "`search={false}` when the panel already has a filter field".
2. **The count's name.** It's in the heading's text only if core's header string already says it (`Panels.BacklinksHeader`). Otherwise it's a presentation-only caption, and a number a reader wants goes through core.
3. **`Slate.CountRowTemplate`.** 32 medium, 24 small; icon 16, label Body, count Caption with tabular figures in `Slate.TertiaryTextBrush`, right-aligned; selected uses the pill and semibold label; hover `Slate.HoverOverlayBrush`. On `Slate.TreeRowBase` for tags and `Slate.PanelRowBase` for backlinks, so neither falls to classic.
4. **Names.** A row's automation name already says what it should, from core. The count is folded in only if core's name lacks it. If it does lack it, that's a core change (plan 46 §4 rule 3), and it's done for both hosts.
5. **Backlinks stay rows.** Expanding them into tree nodes is out of this plan (plan 46 §1).
6. **The hover.** `Slate.PanelRowTemplate`'s hover moves to `Slate.HoverOverlayBrush`, and the `Slate.Contrast.xaml` key arrives from DS-4.

## Tests

Failing first:

- `EveryLeafOpensWithTheToolbar`: a theory over `Leaves`; heading level 2, its text unchanged, no empty button.
- `ACountRowDrawsLabelAndCount`, `ACountRowIsThirtyTwoAndItsSmallTwinTwentyFour`.
- `ASelectedCountRowHasThePillAndASemiboldLabel`.
- `ARowsNameIsUnchanged`: backlink and tag names before and after, from the same fixture.
- Holding: `RightPanePanels_LeafBodiesCarryRowsAndBacklinkNavigates`, `ALocallyStyledRowDrawsTheThemesColors`, `FocusIntegrityManifestCensus`.

Launched app: all 17 leaves, with content where the fixture has it.

## Docs and registers

`w_c_matrix.md` if a heading's level or a stop moved. `wpf-feasibility.md:145`, `:148`.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~RightPane|FullyQualifiedName~Panel|FullyQualifiedName~SiblingNames|FullyQualifiedName~FocusIntegrity"`; keep `ConnectionsLeafTests.TheModelOf` out if the filter widens to `Connections`.

AT: NVDA, heading navigation through two leaves; a backlink row and a tag row; the toolbar's buttons where they exist.

## Acceptance

- [ ] All 17 leaves share the row.
- [ ] Row names are unchanged.
- [ ] No button is drawn that does nothing.

## Brief

Read `MainWindow.xaml:1304-1376` and each leaf's heading, `Panes.jsx:22-44`, `PanelToolbar.prompt.md`, `CountListRow.prompt.md`, DS-17's `PanelToolbarView`. Convert the headings, add the template, change hover. Commit: `feat(windows): each right-pane leaf has the toolbar, and tags and backlinks draw as count rows`.
