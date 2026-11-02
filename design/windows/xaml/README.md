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

## Accent rules

Settings › Appearance › **Accent color**: `Automatic` (default) · `Slate gold` · `Windows accent`.

- **Slate gold:** always gold.
- **Windows accent:** the colour chosen in Windows Settings › Personalization › Colors.
- **Automatic:** gold while Windows uses its default accent (`#0078D4`). Once the user picks another colour, or lets Windows pick one from the wallpaper, Slate follows it.

Everything that is gold follows the setting. That is the nine roles below, which drive primary buttons, checked boxes, radios, switches and sliders, progress, focused-field underlines, links, editor headings, wikilinks and tags, the selected-row pill, the tab underline, selected rows and text selection.

**Contrast gate.** Before using a Windows accent, check every pair below in the current theme. If any pair fails, use gold without telling the user. Check again whenever the accent or the theme changes.

- `TextOnAccent` on `Accent`, `AccentHover` and `AccentPressed`: at least 4.5:1.
- `AccentText` on `Slate.SurfaceColor` and `Slate.WindowBackgroundColor`: at least 4.5:1.
- `Accent` and `AccentIndicator` on `Slate.SurfaceColor`, and `AccentIndicator` on `SelectedSurface`: at least 3:1.
- `SelectionText` on `SelectionBackground`: at least 4.5:1.

Slate gold and the Windows default blue pass every check in both themes.

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

The Windows columns are `SystemColors` properties (.NET 9 and later) and follow Fluent's own mapping. With the default blue they give `#005FB8` (light) and `#60CDFF` (dark), the values the design system uses whenever it needs to show a Windows accent.

### Applying it (sketch, not compiled)

Run this on the freshly loaded Slate dictionary in `ThemeManager.ApplyResources`, and call `ApplyResources` again when the accent changes (`SystemEvents.UserPreferenceChanged`, today it only reapplies when the light/dark theme changes).

```csharp
internal enum AccentSource { Automatic, SlateGold, WindowsAccent }

internal static class SlateAccent
{
    private static readonly Color WindowsDefault = Color.FromRgb(0x00, 0x78, 0xD4);

    /// Writes the Windows accent into the nine role keys. Returns false (dictionary stays gold) when
    /// gold applies: High Contrast, the Slate gold setting, Automatic with the default accent, or a failed check.
    public static bool TryApplyWindowsAccent(ResourceDictionary slate, AccentSource source, bool dark)
    {
        if (SystemParameters.HighContrast || source == AccentSource.SlateGold) return false;
        Color accent = SystemColors.AccentColor;
        if (source == AccentSource.Automatic && accent == WindowsDefault) return false;

        Color surface = (Color)slate["Slate.SurfaceColor"];
        var roles = new Dictionary<string, Color>
        {
            ["Slate.AccentColor"] = dark ? SystemColors.AccentColorLight2 : SystemColors.AccentColorDark1,
            ["Slate.AccentHoverColor"] = dark ? SystemColors.AccentColorLight3 : SystemColors.AccentColorDark2,
            ["Slate.AccentPressedColor"] = dark ? SystemColors.AccentColorLight1 : SystemColors.AccentColorDark3,
            ["Slate.AccentTextColor"] = dark ? SystemColors.AccentColorLight3 : SystemColors.AccentColorDark2,
            ["Slate.AccentIndicatorColor"] = dark ? SystemColors.AccentColorLight2 : SystemColors.AccentColorDark1,
            ["Slate.TextOnAccentColor"] = dark ? Colors.Black : Colors.White,
            ["Slate.SelectedSurfaceColor"] = Mix(accent, surface, dark ? 0.11 : 0.12),
            ["Slate.SelectionBackgroundColor"] = accent,
            ["Slate.SelectionTextColor"] = Colors.White,
        };
        if (!PassesContrastGate(roles, slate)) return false; // silent fallback to gold
        foreach (var (key, color) in roles) slate[key] = color;
        return true;
    }
}
```

### Selection in the reading view

A `RichTextBox`, which the reading view is, always draws its selection as an overlay on top of the text. It ignores `UseAdornerForTextboxSelectionRendering` and `SelectionTextBrush`, so the solid text box pair would paint over the words. `ReadingSurface` sets `Slate.ReadingSelectionBrush` at `SelectionOpacity="1"` in its constructor. It has no Fluent style, so without that it stays system blue, and a `Loaded` class handler can't reach it because WPF only raises `Loaded` on an element whose style or instance asks for it:

| Theme | Overlay | Highlight on the surface | Stands off the surface | Selected primary text, surface / raised |
| --- | --- | --- | --- | --- |
| Light | `#A57A00` at 20% | `#EDE4CC` | 1.27:1 | Lc 85.0 / 77.9 |
| Dark | `#E2AD24` at 24% | `#4D401F` | 1.64:1 | Lc 80.4 / 77.5 |

An overlay tints the text as well as the background, so no overlay that shows at all keeps every colour above Lc 75. Selected secondary text measures Lc 75.6 in light and 65.1 in dark, and accent text about 63-65. Primary text under the blue overlay this replaced measured Lc 57-61. In High Contrast the brush is `SystemColors.Highlight` at 40%, the opacity Fluent's default gives. If the Windows accent setting is added, this overlay needs its own rule in the contrast gate.

`PassesContrastGate` is the list above, using the WCAG 2 contrast ratio. `Mix` blends two opaque colours channel by channel.

## Theme setting

Settings › Appearance › **Theme**: `Light` · `Dark` · `Use Windows setting` (default). The command palette has **Switch Theme…**, which opens a list of the same three choices with the current one checked. The title bar has no theme button.

`ThemeManager` follows the Windows setting today. Only do that when the preference is `Use Windows setting`. For `Light` or `Dark`, ignore `AppsUseLightTheme` changes.

## Type ramp

The UI uses the WinUI type ramp. WPF's Fluent theme already ships most of the `TextBlock` styles. Body Large isn't one of them, so add Slate's own styles in the app (not in these generated files):

| Token | Size / line height | WPF style |
| --- | --- | --- |
| `--type-caption` | 12/16, Segoe UI Variable Small | `CaptionTextBlockStyle` |
| `--type-body` | 14/20, Text | `BodyTextBlockStyle` |
| `--type-body-strong` | 14/20 semibold, Text | `BodyStrongTextBlockStyle` |
| `--type-body-large` | 18/24, Text | `Slate.BodyLargeTextBlockStyle` (add) |
| `--type-body-large-strong` | 18/24 semibold, Text | `Slate.BodyLargeStrongTextBlockStyle` (add) |
| `--type-subtitle` | 20/28 semibold, Display | `SubtitleTextBlockStyle` |
| `--type-title` | 28/36 semibold, Display | `TitleTextBlockStyle` |
| `--type-title-large` | 40/52 semibold, Display | `TitleLargeTextBlockStyle` |
| `--type-display` | 68/92 semibold, Display | `DisplayTextBlockStyle` |

The app has them in `Themes/Slate.Text.xaml`, merged after the Slate dictionary in every appearance. They set the family themselves rather than basing on `BodyTextBlockStyle`, because a `StaticResource` can't reach a sibling merged dictionary:

```xml
<Style x:Key="Slate.BodyLargeTextBlockStyle" TargetType="TextBlock">
    <Setter Property="FontFamily" Value="Segoe UI Variable Text, Segoe UI" />
    <Setter Property="FontSize" Value="18" />
    <Setter Property="LineHeight" Value="24" />
    <Setter Property="LineStackingStrategy" Value="BlockLineHeight" />
</Style>
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

WPF doesn't apply the Windows "Text size" setting (Settings › Accessibility › Text size, 100% to 225%) to fixed font sizes, though text with no explicit `FontSize` grows through `SystemFonts`. Read `UISettings.TextScaleFactor`, multiply every UI font size by it and update when `TextScaleFactorChanged` fires. Don't scale icon glyphs.

Heights that hold text are minimums, so they grow with the text instead of clipping it at 225%. Use `MinHeight`, never `Height`: title bar 48, tab strip 36, status bar 24, and every list, tree, menu and setting row. The CSS tokens are `--titlebar-min-height`, `--tabstrip-min-height` and `--statusbar-min-height`.

## Regenerating

The files are generated, not hand-edited. When a token in `tokens/` changes, regenerate both files and copy them into the app. The Fluent brush list comes from the `dotnet/wpf` theme files, so regenerate after a .NET update too.
