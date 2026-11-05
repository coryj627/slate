# Slate WPF theme

`Slate.Light.xaml` and `Slate.Dark.xaml` replace `apps/slate-windows/src/SlateWindows/Themes/Slate.Light.xaml` and `Slate.Dark.xaml` in [coryj627/slate](https://github.com/coryj627/slate). They are generated from this design system's tokens (`tokens/colors.css`, `brand.css`, `fluent-slate.css`, `spacing.css`) and the brush keys of the WPF Fluent theme (`dotnet/wpf`, `PresentationFramework.Fluent/Resources/Theme/Light.xaml` and `Dark.xaml`).

`ThemeManager` already merges these files after `Fluent.Light.xaml` / `Fluent.Dark.xaml`, so no loading code changes. Every existing `Slate.*` key is kept, so `ValidateResourceDictionaries` still passes. Leave `Slate.Contrast.xaml` and `Fluent.HC.xaml` as they are: High Contrast uses system colours and never gold.

## What each file contains

1. **Accent roles.** Nine colours that every accent brush reads with `DynamicResource` (see Accent rules).
2. **Slate keys the app already has.** Values are unchanged except: accent blue becomes Slate gold, text selection becomes pale gold (light) or dark gold (dark), `Slate.FocusBrush` becomes the text colour (the focus ring is 2px in the text colour), and the editor heading, wikilink and tag brushes use `AccentText`.
3. **New Slate keys:** subtle border, control stroke hover/pressed, control fill rest/hover/pressed, disabled text/border/surface, success, hover and pressed overlays, scrim. Each has a `Color` and a `Brush`.
4. **Shadows:** `Slate.Shadow2` … `Slate.Shadow64` (`DropShadowEffect`).
5. **Fluent colour keys with Slate values,** for templates that read a `Color` directly.
6. **Fluent brushes re-pointed to Slate:** 81 accent brushes and about 267 neutral brushes (text, control fills, strokes, flyout and card backgrounds, disabled states, status colours). Fluent's own brushes are built with `StaticResource` when its dictionary loads, so redefining a colour key alone changes nothing; each brush key is overridden.
7. **Control borders:** Fluent's gradient "elevation" strokes become Slate's flat 1px control stroke (`Slate.BorderColor`, 3:1 on the surface). Focused text fields keep a 2px accent underline.

If you add one of the new section-3 brushes to Slate-drawn UI, add the same key to `Slate.Contrast.xaml`, mapped to a `SystemColors` brush.

The app also merges `Themes/Slate.SystemColors.xaml` in light and dark, not in High Contrast. A control with a local style, which about 25 of the shell's lists and trees have, loses Fluent's implicit style and falls back to WPF's classic one, which paints from `SystemColors`. That file points those keys at Slate's tokens. Without it, those rows draw black text in the dark theme and select in Windows blue.

## Accent rules

Settings › Appearance › **Accent color**: `Automatic` (default) · `Slate gold` · `Windows accent`.

- **Slate gold:** always gold.
- **Windows accent:** the colour chosen in Windows Settings › Personalization › Colors.
- **Automatic:** gold while Windows uses its default accent (`#0078D4`). Once the user picks another colour, or lets Windows pick one from the wallpaper, Slate follows it.

Selected panel rows use `SelectedSurface` with an `AccentIndicator` pill, so selection is a shape as well as a colour, and all their text clears the floor: primary, secondary and `AccentText`.

Text draws in `AccentText`, never in `Accent`: the fill measures Lc 71.61 on the raised surface in light, and the text role clears all three surfaces (#1051). A test enforces this.

Everything that is gold follows the setting. That is the nine roles below, which drive primary buttons, checked boxes, radios, switches and sliders, progress, focused-field underlines, links, editor headings, wikilinks and tags, the selected-row pill, the tab underline, selected rows and text selection.

**No contrast gate.** A Windows accent is used as Windows gives it, in both themes, whatever its contrast (plan 46, DSD16). The gate DS-11 first shipped, text at 4.5:1 and fills and marks at 3:1, sent every hue the owner tried back to gold in dark. Choosing an accent from colours that clear contrast is #1842.

Slate gold and the Windows default blue clear 4.5:1 for every text the accent draws and 3:1 for its fills and marks, in both themes (`SlateAccentTests`): `TextOnAccent` on the three fills, `AccentText` on the surfaces and `SelectedSurface`, links hovered and pressed on the surface, `SelectionText` on `SelectionBackground`, and primary, secondary and tertiary text on `SelectedSurface`. Gold stays at the project's floor for text, |Lc| above 75 (`ThemeTokenContrastTests`), and a Windows accent may sit well below it. A selected row's text draws in `Slate.SelectedTextBrush`, which is primary text and doesn't follow the accent. `SelectionText` is text selection's alone: white on the 12% tint would be 1.17:1.

| Role (`Slate.<Role>Color`) | Gold, light | Gold, dark | Windows accent, light | Windows accent, dark |
| --- | --- | --- | --- | --- |
| `Accent` | `#7D5A00` | `#EECF7E` | `AccentColorDark1` | `AccentColorLight2` |
| `AccentHover` | `#6B4D00` | `#F0D590` | `AccentColorDark2` | `AccentColorLight3` |
| `AccentPressed` | `#563D04` | `#EDCB75` | `AccentColorDark3` | `AccentColorLight1` |
| `AccentText` | `#6B4D00` | `#EECF7E` | `AccentColorDark2` | `AccentColorLight3` |
| `AccentIndicator` | `#A57A00` | `#EECF7E` | `AccentColorDark1` | `AccentColorLight2` |
| `TextOnAccent` | `#FFFFFF` | `#171717` | `#FFFFFF` | `#000000` |
| `SelectedSurface` | `#FDF5E1` | `#33291A` | `AccentColor` 12% over the surface | `AccentColor` 11% over the surface |
| `SelectionBackground` | `#F9E7B8` | `#6B4D00` | `AccentColor` | `AccentColor` |
| `SelectionText` | `#171717` | `#F5F5F5` | `#FFFFFF` | `#FFFFFF` |
| `ReadingSelection` | `#A57A00` at 20% | `#E2AD24` at 24% | `AccentColor` at 20% | `AccentColor` at 24% |

The Windows columns are `SystemColors` properties (.NET 9 and later) and follow Fluent's own mapping. On the default blue, `AccentColor` is `#0078D4`, Light 1 to 3 are `#0091F8`, `#4CC2FF` and `#99EBFF`, and Dark 1 to 3 are `#0067C0`, `#003E92` and `#001A68` (measured 3 November 2026, Windows 11 build 26300). So `Accent` draws as `#0067C0` in light and `#4CC2FF` in dark. WinUI's brush constants for the same blue are `#005FB8` and `#60CDFF`, and those are the values the design system uses whenever it needs to show a Windows accent.

### Applying it

`SlateAccent.TryApplyWindowsAccent` runs on the freshly loaded Slate dictionary in `ThemeManager.DictionariesFor`, and writes these roles. It returns false, with the dictionary left gold, when gold applies: High Contrast, the Slate gold setting, or Automatic with the default accent. `ThemeManager` applies again when the accent changes: WPF holds the colours until a message of its own clears them, and that message can come after `SystemEvents.UserPreferenceChanged`, so `SystemAccentWatch` keeps a resource reference to `SystemColors.AccentColorKey` on the main window, which WPF reads again right after the clearing. A fact in `SlateAccentTests` reads this table.

### Selection in the reading view

A `RichTextBox`, which the reading view is, always draws its selection as an overlay on top of the text. It ignores `UseAdornerForTextboxSelectionRendering` and `SelectionTextBrush`, so the solid text box pair would paint over the words. `ReadingSurface` sets `Slate.ReadingSelectionBrush` at `SelectionOpacity="1"` in its constructor. It has no Fluent style, so without that it stays system blue, and a `Loaded` class handler can't reach it because WPF only raises `Loaded` on an element whose style or instance asks for it:

| Theme | Overlay | Highlight on the surface | Stands off the surface | Selected primary text, surface / raised |
| --- | --- | --- | --- | --- |
| Light | `#A57A00` at 20% | `#EDE4CC` | 1.27:1 | Lc 85.0 / 77.9 |
| Dark | `#E2AD24` at 24% | `#4D401F` | 1.64:1 | Lc 80.4 / 77.5 |

An overlay tints the text as well as the background, so no overlay that shows at all keeps every colour above Lc 75. Selected secondary text measures Lc 75.6 in light and 65.1 in dark. Selected links use `AccentText`, and they measure below the floor too. Primary text under the blue overlay this replaced measured Lc 57-61. In High Contrast the brush is `SystemColors.Highlight` at 40%, the opacity Fluent's default gives. 
Under a Windows accent the overlay is `AccentColor` at gold's alpha, and nothing checks it against the hue. On the owner's `#EA005E` in dark the highlight doesn't stand off the surface by the 1.2:1 gold is held to. Measured on the default blue:

| Theme | Overlay | Selected primary text, surface / raised | Stands off, surface / raised |
| --- | --- | --- | --- |
| Light | `#0078D4` at 20% | 11.23:1 / 9.92:1 | 1.31:1 / 1.29:1 |
| Dark | `#0078D4` at 24% | 8.63:1 / 7.68:1 | 1.29:1 / 1.27:1 |

These ratios are WCAG 2's (`SlateContrast.WcagRatio`).

## Theme setting

Settings › Appearance › **Theme**: `Light` · `Dark` · `Use Windows setting` (default). The command palette has **Switch Theme…**, which opens a list of the same three choices with the current one checked. The title bar has no theme button.

`ThemeManager` follows the Windows setting only when the preference is `Use Windows setting`. For `Light` or `Dark`, it ignores `AppsUseLightTheme` changes. High Contrast still wins over all three. The preference lives in `preferences.json` on this device, under `theme`, and it's read before the first paint.

## Type ramp

The UI uses the WinUI type ramp, and these are the styles to use. WPF's Fluent theme ships most of them as `TextBlock` styles, but those take their sizes by `StaticResource`, so they can't follow Windows Text size. The app's are in `Themes/Slate.Text.xaml`, and read keys from `TypeRamp.cs`, which the app merges first in every appearance:

| Token | Size / line height | Slate style | Keys |
| --- | --- | --- | --- |
| `--type-caption` | 12/16, Segoe UI Variable Small | `Slate.CaptionTextBlockStyle` | `Slate.FontSizeCaption`, `Slate.LineHeightCaption` |
| `--type-body` | 14/20, Text | `Slate.BodyTextBlockStyle` | `Slate.FontSizeBody`, `Slate.LineHeightBody` |
| `--type-body-strong` | 14/20 semibold, Text | `Slate.BodyStrongTextBlockStyle` | `Slate.FontSizeBodyStrong`, `Slate.LineHeightBodyStrong` |
| `--type-body-large` | 18/24, Text | `Slate.BodyLargeTextBlockStyle` | `Slate.FontSizeBodyLarge`, `Slate.LineHeightBodyLarge` |
| `--type-body-large-strong` | 18/24 semibold, Text | `Slate.BodyLargeStrongTextBlockStyle` | `Slate.FontSizeBodyLargeStrong`, `Slate.LineHeightBodyLargeStrong` |
| `--type-subtitle` | 20/28 semibold, Display | `Slate.SubtitleTextBlockStyle` | `Slate.FontSizeSubtitle`, `Slate.LineHeightSubtitle` |
| `--type-title` | 28/36 semibold, Display | `Slate.TitleTextBlockStyle` | `Slate.FontSizeTitle`, `Slate.LineHeightTitle` |

The families are `Slate.FontFamilySmall`, `Slate.FontFamilyText` and `Slate.FontFamilyDisplay`. `--type-title-large` and `--type-display` have no style, because no Slate surface uses them.

The reading view has its own scale, the tokens' `--reader-*`: `Slate.FontSizeReaderBody` and `Slate.LineHeightReaderBody`, and the same pair for `H1` to `H4` and `Small`. They're keys in `TypeRamp` so text size scales them too, but they aren't ramp steps, because the ramp is for chrome. Its 72ch measure is margins (`PagePadding`) rather than `ColumnWidth`, because a column would be a reading-order hazard, and margins keep the scroll bar at the pane's edge and can't move the caret. The body's line height is a floor, so a math or diagram object grows its line instead of overlapping what follows. A formula wider than the measure keeps its size and scrolls sideways, because shrinking it to fit left it too small to read.

Text with no size of its own is Body: the window sets the family and size, so it inherits them. A control whose default style sets a size doesn't inherit, so `MainMenu` and `ShellStatusBar` set both, and `ContextMenu` and `ToolTip`, which aren't in the window's tree, get theirs from styles built on Fluent's (`TypeRamp.StyleOn`). The window sets no line height, because `TextBlock.LineHeight` inherits and a fixed value would follow into every size.

Set a size through a style or a key, never a number. `TypeRampCensus` fails on a literal, apart from the few it names with a reason. A control's own value, such as a `TextBox`'s, takes the key directly:

```xml
<TextBlock Text="Add property" Style="{DynamicResource Slate.SubtitleTextBlockStyle}" />
<TextBox FontSize="{DynamicResource Slate.FontSizeBodyLarge}" />
```

## Scrim

Modal scrims are 30% black in both themes. That is WinUI's smoke and the value of WPF's `SmokeFillColorDefault`. `Slate.ScrimColor` and `SmokeFillColorDefault` are `#4D000000`.

## Shadows

WPF takes one `DropShadowEffect` per element, so each level is a single shadow. `Direction="270"` makes the shadow fall straight down; the default (315) pushes it down and to the right. `BlurRadius` is 1.5 × the CSS blur, which gives the same Gaussian spread. Check each level by eye against the Elevation card in the running app.

| Level | Used for | CSS | ShadowDepth | BlurRadius | Opacity light / dark |
| --- | --- | --- | --- | --- | --- |
| `Slate.Shadow2` | Card media | `0 1px 2px` | 1 | 3 | 0.18 / 0.36 |
| `Slate.Shadow4` | Card | `0 2px 4px` | 2 | 6 | 0.18 / 0.36 |
| `Slate.Shadow8` | Card, tooltip, floating toolbar | `0 4px 8px` | 4 | 12 | 0.18 / 0.36 |
| `Slate.Shadow16` | Menu, popover, dropdown, toast | `0 8px 16px` | 8 | 24 | 0.18 / 0.36 |
| `Slate.Shadow28` | Command palette | `0 14px 28px` | 14 | 42 | 0.18 / 0.36 |
| `Slate.Shadow64` | Dialog, overlay drawer | `0 32px 64px` | 32 | 96 | 0.18 / 0.36 |

An effect turns off ClearType for the text inside it. Put the effect on a background-only `Border` behind the content, not on the content's parent. On opaque popups, set `RenderOptions.ClearTypeHint="Enabled"`.

## Text size

WPF doesn't apply the Windows "Text size" setting (Settings › Accessibility › Text size, 100% to 225%) to fixed font sizes, though text with no explicit `FontSize` grows through `SystemFonts`. Read `HKCU\Software\Microsoft\Accessibility\TextScaleFactor`, a percentage from 100 to 225, and divide it by 100 to get the factor (1 when the value is missing, unreadable or under 100, and never above 2.25). Multiply every UI font size by the factor, and update on `SystemEvents.UserPreferenceChanged`. Don't scale icon glyphs. The registry is plan 46's choice (DSD3), because `UISettings.TextScaleFactor` is WinRT and the standard route to it ships about 25 MB of Windows SDK projection for one number. The owner's live check on 2026-11-04 confirmed it: a change made in Windows Settings reaches the running app.

Heights that hold text are minimums, so they grow with the text instead of clipping it at 225%. Use `MinHeight`, never `Height`: title bar 48, tab strip 36, status bar 24, and every list, tree, menu and setting row. The CSS tokens are `--titlebar-min-height`, `--tabstrip-min-height` and `--statusbar-min-height`.

Layout pixels don't scale, so whatever holds text has to make room for it. In the app, one `TextScaleService` owned by `App` reads the setting, and `TypeRamp.Apply` writes the new sizes into the ramp's dictionary, so every `DynamicResource` follows with no re-merge. A sheet keeps its width and its text wraps, and a list in a sheet doesn't scroll sideways, because that measures its rows at any width and nothing in them wraps. A width or height reserved for text is a figure at the body size, multiplied by the text size. The right pane's rail goes under its leaf when the leaf would have under eight ems beside it, and scrolls in 40% of the pane's height.

A sheet's whole body scrolls, and so does the welcome view, because at the window's 480 minimum a long subtitle alone can take all the room a field has. A list inside that scroller has a `MaxHeight`, since a star row under an infinite height realizes every row, and `WheelChain` on the scroller passes the wheel down only to an inner scroller that can move that way. Without it, a short list stops the sheet under the pointer.

## Regenerating

The files are generated, not hand-edited. When a token in `tokens/` changes, regenerate both files and copy them into the app. The Fluent brush list comes from the `dotnet/wpf` theme files, so regenerate after a .NET update too.
