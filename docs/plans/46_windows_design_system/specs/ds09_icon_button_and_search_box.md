# DS-9 — Icon-only buttons draw an icon and have a name, and search fields look like search fields

Issues: filed by DS-0. Size S–M. Lanes: windows. AT: NVDA spot. Wave 2. Depends on: DS-2. Decision: DSD6. Interlock: DS-8, DS-12, DS-15, DS-17 and DS-18 use these styles.

## Goal

Two small styles the later chunks need, used first on the buttons and fields that already want them. A census makes "an icon-only button has a name" a rule rather than something each surface remembers.

## Current state on main

- **Text glyphs on buttons:** `MainWindow.xaml:959` (`↻`), `MainWindow.xaml:2792` (`✕`), `WorkspaceTemplates.xaml:270` (`×`), `Graph/GraphInspectorView.cs:492` (`✕`), and two `RepeatButton`s at `WorkspaceTemplates.xaml:338` and `:343` (`▲`, `▼`). Each has a name today.
- **No icon glyphs anywhere in the shell:** `SymbolThemeFontFamily` appears in no XAML under `src`, apart from a comment at `TextBoxAccessibility.cs:21`.
- **Search and filter fields:** `MainWindow.xaml:993` (Files), `:2005` (Bibliography), `:2623` (Quick Open), `:2744` (Search), `:3036` (palette); the graph's and canvas's filters in code (`Canvas/CanvasSurfaceView.cs:155`). All plain `TextBox`es with `FieldPrompt`. `MainWindow.xaml:1005-1009` is the Files filter's separate Clear button (R-3).
- **The clear button** inside a Fluent `TextBox` already has its name fixed (`TextBoxAccessibility`, #1106).
- **Nothing pins names on icon buttons,** only `TextBoxAccessibilityTests.AssertNoPrivateUseName` for that one button.

## Reference

`design/windows/wpf-feasibility.md:69`: "Every icon-only button needs `AutomationProperties.Name`. Otherwise Narrator reads the private-use glyph as the name." `readme.md:131-137`: Regular at rest, 16 in dense rows, 20 in toolbars; icon buttons have a name and a tooltip showing the chord. `winui-review.md:46`, `:77`, DSD6. `components/forms/SearchBox.prompt.md`. `FieldPrompt.cs`.

## Design

1. **`Slate.SubtleIconButtonStyle`,** in `Themes/Slate.Text.xaml`, `TargetType="ButtonBase"` so it serves `Button` and `RepeatButton` alike, built whole because a based-on-nothing style falls back to classic (#1767): transparent at rest, hover `Slate.HoverOverlayBrush`, pressed `Slate.PressedOverlayBrush`, radius 4, a 16 glyph in `SymbolThemeFontFamily`, drawn by an `AutomationPresentationTextBlock` so no peer is named by it, `Width` and `Height` 32, ring from DS-6 if merged. `Slate.SubtleIconButton40Style` based on it: 40 × 40 with the fill inset to 30, for the title bar, per DSD6. A `ToggleButton` twin for the pressed state.
2. **`SearchGlyph`,** an attached behaviour, not a style: a based-on style can't add a template part, and a style in window XAML can't reach Fluent's (plan 46 §4 rule 9). Like `FieldPrompt` (`FieldPrompt.cs:55-70`), it finds `PART_ContentHost`'s panel and injects glyph `E721` at the leading edge as an `AutomationPresentationTextBlock`, `IsHitTestVisible="False"`, reinstalling on `ThemeManager.ResourcesChanged` because a theme change swaps the template (`FieldPrompt.cs:37-39`). It shifts the prompt and text by the glyph's width. It must not change `TextBox`'s peer, its name or selection (`TextBoxSelection`).
3. **Use them:** `MainWindow.xaml:959` takes `E72C`; `MainWindow.xaml:2792` and `GraphInspectorView.cs:492` take `E711`; `WorkspaceTemplates.xaml:270` is DS-8's; `WorkspaceTemplates.xaml:338` and `:343` take `E70E` and `E70D`. Files, Bibliography and the graph and canvas filters take `SearchGlyph`. Quick Open, Search and the palette keep their larger field, with the glyph added.
4. **The census.** `GlyphsNameNothing`, scanning XAML and hosted realization: any `ButtonBase` whose content is a private-use character or a glyph style has a non-empty name without a private-use character, and no plain `TextBlock` anywhere holds a private-use character outside a control's template. Later chunks that add glyphs (DS-7, DS-8, DS-10, DS-16) fall under it.
5. **Tooltips.** Each of these buttons gets a tooltip with its name and chord (`cmd:ChordText` where there's a chord), and `ToolTipService.ShowsToolTipOnKeyboardFocus` stays on outside a rail (`wpf-feasibility.md:107`).

## Tests

Failing first:

- `GlyphsNameNothing` (census). Red when a named glyph button loses its name, or a plain `TextBlock` takes a glyph, in planted test cases; don't treat a clean first pass as proof.
- `TheSubtleIconButtonIsThirtyTwoAndItsLargeTwinFortyWithAThirtyFill`.
- `ASearchBoxKeepsItsPeerAndItsPrompt`: same `AutomationPeer` type and name as a plain `TextBox`, prompt shown when empty, gone with text.
- `TheSearchGlyphIsNotAStop`: Tab goes from the field to the next control.

Launched app: each changed button and field, dark, and hosted light.

## Docs and registers

`wpf-feasibility.md:69`, `:130`: a note that the styles exist.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~GlyphsNameNothing|FullyQualifiedName~TextBoxAccessibility|FullyQualifiedName~FieldPrompt|FullyQualifiedName~ThemeDesignSystemTests"`.

AT: NVDA, Tab onto Refresh and the arrow buttons: names unchanged, no glyph read; a search field reads its name and prompt as before.

## Acceptance

- [ ] No text glyph left on a button.
- [ ] The census passes with a planted failure shown in the PR.
- [ ] Every field's automation name is unchanged.

## Brief

Read `Themes/Slate.Text.xaml`, `WorkspaceTemplates.xaml:65-120` (`Slate.ButtonBase` as a whole-template example), `FieldPrompt.cs`, `TextBoxAccessibility.cs`, `SearchBox.prompt.md`. Add the styles, change the listed sites, write the census. Commit: `feat(windows): icon-only buttons draw an icon and have a name, and search fields show they search`.
