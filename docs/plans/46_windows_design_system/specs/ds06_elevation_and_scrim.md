# DS-6 — Overlays keep ClearType and have the design's shadow, scrim and corners

Issues: filed by DS-0. Size M. Lanes: windows. AT: Narrator spot. Wave 2. Depends on: DS-2. Decision: none. Interlock: DS-14 relies on the width change here; DS-12 adds a scrim layer for its row.

## Goal

A sheet or the palette reads as raised: a shadow that falls straight down, corners of 8, the window dimmed by the 30% scrim. Text inside it has ClearType, because the shadow no longer sits on its parent. Dialog-sized sheets are the design's width. Nothing a screen reader reaches changes, open or closed.

## Current state on main

- **Shadows:** eleven identical `DropShadowEffect BlurRadius="18" ShadowDepth="4" Opacity="0.28"` at `MainWindow.xaml:2610, 2727, 3021, 3224, 3307, 3440, 3578, 3634, 4045, 4185, 4335`. Each is on the overlay itself, so it turns ClearType off for everything inside (spike 4, `wpf-feasibility.md:84`). The direction defaults to 315, down and to the right. Five overlays have none: `DashboardEditorOverlay` (`MainWindow.xaml:3669`), `BaseQueryBuilderOverlay` (`MainWindow.xaml:3786`), `CanvasCardEditorOverlay` (`MainWindow.xaml:4416`), `CanvasCardPickerOverlay` (`MainWindow.xaml:4462`), `CanvasPromptOverlay` (`MainWindow.xaml:4508`).
- **The tokens exist and are unused:** `Slate.Shadow2` to `Slate.Shadow64` at `Themes/Slate.Dark.xaml:214-219` and the same in light. `Slate.ScrimBrush` at `Themes/Slate.Dark.xaml:209`.
- **No scrim:** each overlay has a hit-test layer with `Background="Transparent"` (`MainWindow.xaml:2587, 2704, 2998, 3202, 3284, 3418, 3556, 3612, 3667, 3784, 4022, 4162, 4312`). The three canvas sheets have no layer. All of these are children of the `Grid` at `MainWindow.xaml:812`, between the menu and the status bar, so a layer there can't cover those two rows.
- **A landmark's visibility is read directly.** `AutomationLandmarkPeer.IsControlElementCore` is `Owner is UIElement { Visibility: Visibility.Visible }` (`AutomationLandmark.cs:314-315`), the element's own value, not `IsVisible`. So a landmark inside a collapsed parent stays in the control view. `AssertElementDisappears` (`ShellAccessibilityTests.cs:2641-2652`) and `CommandPaletteTests.Shell.cs:343-351` read the overlay's own `Visibility`.
- **No corners:** `AutomationLandmarkBorder` (`AutomationLandmark.cs:39`) sets no radius, and the overlays pass none.
- **Widths:** `MainWindow.xaml:2591` (600), `:2708` (640), `:3002` (640), `:3206` (460), `:3288` (760), `:3422` (460), `:3560` (420), `:3616` (460), `:3671` (560), `:3788` (640), `:4026` (520), `:4166` (520), `:4316` (520), `:4419` (`MinWidth` 420).
- **Tooltips:** Fluent's `ToolTip.xaml:35-49` puts its shadow on the border around the text, with `ClearTypeHint` on the control (`ToolTip.xaml:29`), which that defeats. Radius 4.

## Reference

`design/windows/xaml/README.md:143-160`; `wpf-feasibility.md:78-85`; spike 4's arrangement. `readme.md:100-103`: 8 for flyouts and dialogs; 28 for the palette; 64 for dialogs. `winui-review.md:75`: dialog width 448, from 320 to 520. `ui_kits/slate-windows/Panes.jsx:74`: the palette takes the scrim too. The focus ring stays as Fluent draws it: `tokens/base.css:7` and plan 46 §2.3.

## Design

1. **Sibling shadow.** Each overlay gets a wrapping `Grid`, which takes its `Width` (as below), `MaxHeight`, alignment and `Margin`. Its children are a background-only `Border` (`Style="{DynamicResource Slate.OverlayShadow28}"` or `...64`, surface background, radius 8) and the `AutomationLandmarkBorder`. **The landmark keeps its own `Visibility` binding,** with its `DataContext`, `FocusManager.IsFocusScope`, `SheetKeyboardFence`, navigation and every automation property, and takes `CornerRadius="8"`. The shadow `Border` binds its `Visibility` to the landmark's by `ElementName`. The wrapper is never collapsed: with no background and collapsed children, it paints nothing and takes no hits. A `Grid` has no peer, so the UIA tree doesn't change. Styles go in `Themes/Slate.Text.xaml`.
2. **Levels:** Quick Open, Search and the palette take `Slate.Shadow28`. Every other sheet takes `Slate.Shadow64`, including the five that have none today. `Slate.Contrast.xaml` has no shadow keys; a `DynamicResource` with none gives no effect, which is fine, so none is added.
3. **ClearType:** `RenderOptions.ClearTypeHint="Enabled"` on each landmark, since they're opaque.
4. **The scrim.** Each hit-test layer takes `Background="{DynamicResource Slate.ScrimBrush}"`. The three canvas sheets gain one. A Contrast mapping for `Slate.ScrimBrush` goes in as `xaml/README.md:17` requires. Two thin layers finish the window: a `Border` over `MainMenu`'s row and one over `ShellStatusBarBorder`'s, both `Slate.ScrimBrush`, `IsHitTestVisible="False"`, shown when any modal surface is open through a host property backed by `ModalSurfaces.TopmostOpen`. A flat 30% black blends across the three, so it reads as one scrim without moving 2000 lines of overlays. The menu is already disabled under every surface.
5. **Widths.** One-form sheets (`MainWindow.xaml:3206, 3422, 3560, 3616, 4026, 4166, 4316`) become `MaxWidth="448"`, `MinWidth="320"`. The wide ones (`MainWindow.xaml:3288` bulk rename, `MainWindow.xaml:3671` dashboard editor, `MainWindow.xaml:3788` query builder) keep their width as `MaxWidth`, because they hold tables and a side-by-side form. The palette family keeps its width as `MaxWidth`. All with a 24 margin, and `HorizontalAlignment="Stretch"` on the wrapper: stretched up to `MaxWidth` and centred above it, whereas `Center` would size a sheet to its content.
6. **Tooltips.** Extend DS-2's `ToolTip` style with a template: a sibling `Border` with `Slate.Shadow8`, then the opaque border with radius 4, `Slate.SurfaceBrush`, the hint, and Caption text.

## Tests

Failing first:

- `EveryOverlayCarriesItsShadowBesideItsContent` (XAML census, driven by `ModalSurface` so a new surface can't be forgotten): no overlay has `.Effect`; its sibling names a shadow style; the landmark still has its own `Visibility` binding.
- `EveryModalSurfaceDrawsTheScrim`: same driver; the layer's background is `Slate.ScrimBrush`.
- `TheMenuAndStatusRowsDimUnderAModalSurface`: hosted; both thin layers show and take no hits.
- `AnOverlaysCornersAreEight`.
- `AOneFormSheetIsTheDialogWidth`, hosted at a wide and a narrow window; a sheet with little content is still 448 wide.
- `AControlsRingMatchesTheDesign`: Fluent's rectangle has `Margin` −3 and thickness 2, so its inner edge is 1 outside the control. It pins what's right, so a later change can't get this wrong.
- Gate, `ThemedShell_APalettesTextKeepsItsClearType`: a physical-pixel capture of the open palette; colour-fringed pixels are at least half of its text pixels, the method of spike 4. If a job VM has ClearType off, the fact says so and skips with that reason, so a missing fringe isn't mistaken for a pass.

Existing to hold: `AssertElementDisappears`' users, `CommandPaletteTests.Shell.cs:343-351`, `ThemedShell_ADisabledGridStaysOnItsSurface`, `AGridCellsRingIsDrawnInsideTheCell`, `FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean`.

Launched app: each overlay, dark; the palette and Add Property in a hosted light render. Judge the shadow against the Elevation card (`xaml/README.md:149`: "Check each level by eye").

## Docs and registers

`wpf-feasibility.md:78-85`: done. `Dialog.prompt.md` and `wpf-feasibility.md:89` were settled by DS-0.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~ModalSurfaceTests|FullyQualifiedName~ThemeDesignSystemTests|FullyQualifiedName~FocusIntegrity|FullyQualifiedName~CommandPaletteTests"`, and the gate facts locally with the desktop free.

AT: Narrator, open Add Property and the palette: still announced as dialogs, focus lands as before, Esc returns it. With nothing open, object navigation meets no closed sheet. A panel with no peer shouldn't change anything, and this proves it.

## Acceptance

- [ ] No `DropShadowEffect` left in `MainWindow.xaml`.
- [ ] Every modal surface dims the whole window.
- [ ] The palette's text has colour fringes in a capture.
- [ ] A UIA tree dump with a sheet open, and one with everything closed, match the dumps from before this PR.

## Brief

Read `MainWindow.xaml:2584-2612` as the pattern, then each overlay in the lists above, `AutomationLandmark.cs:39` and `:302-318`, `Themes/Slate.Dark.xaml:205-219`, `design/windows/xaml/README.md:143-160`. Take the UIA dumps first. Add the shadow styles and the tooltip template to `Themes/Slate.Text.xaml`. Wrap each overlay, leaving `Visibility` on the landmark. Facts first. Commit: `feat(windows): overlays keep their cleartype, with the design's shadow, scrim and corners`.

## Notes from the build

**ClearType's bar is a quarter, not half.** With the effect, the palette's text had 0 of 21139 pixels fringed. Without it, 38% are, by the gate's measure, and plain window text on the same capture is 34 to 53%, at 200% display scale, where a stem's middle is solid. Half would fail ordinary text.

**The canvas sheets keep their size.** The width list leaves them, so their wrappers are `Center` with the `MinWidth` of 420 they had: they size to their content.

**Three headings wrap,** in the template sheets, because 448 is narrower than the 520 they had: `NoSheetTextIsCutAtTheLargestSize` caught "Fill in template details" at 225%.

**A wrapper is never collapsed,** so `WithNoVaultNoOverlayShowsAndThePointerReachesTheWelcomeView` counts a wrapper with nothing shown in it as not shown, which is what it is: no paint and no hits.
