# DS-11 — The accent can follow Windows, and never fails contrast

Issues: filed by DS-0. Size M. Lanes: windows. AT: — (no spoken change; the fallback is silent by design). Wave 3. Depends on: DS-10. Decision: DSD14. Interlock: none.

## Goal

Appearance gets its second row: Accent color: Automatic, Slate gold or Windows accent. Everything gold follows it. Every text pair an accepted accent draws clears the floor gold clears, selected rows included. A Windows accent that can't falls back to gold without a message, as the design says.

## Current state on main

- **Gold only.** No `SystemColors.Accent*` read anywhere in `src`.
- **The roles are ready.** `Themes/Slate.Dark.xaml` and `Slate.Light.xaml` hold the nine `Slate.<Role>Color` values, and Fluent's accent brushes read them by `DynamicResource` (for example `Slate.Dark.xaml:286`).
- **The hook.** `ThemeManager.DictionariesFor` (`ThemeManager.cs:204-238`) loads a fresh Slate dictionary and already writes computed values into it (`ThemeManager.cs:216-217`).
- **Reapplying.** `SystemEvents_UserPreferenceChanged` (`ThemeManager.cs:144-174`) reapplies only when light or dark changed.
- **The gate that exists.** `ThemeTokenContrastTests.EveryTextBearingPairClearsProjectApcaFloor` (`ThemeTokenContrastTests.cs:18`) holds gold to |Lc| > 75, including a selected row's text, captions and links (`ThemeTokenContrastTests.cs:53-56`). It reads the theme files (`ThemeTokenContrastTests.cs:276`), so a computed dictionary never reaches it. `WcagRatio` (`ThemeTokenContrastTests.cs:146`) and `ApcaLc` (`ThemeTokenContrastTests.cs:292`) live only in that file, and this gate has to run in the app.
- **Selected rows draw in `SelectionText`.** `Slate.Text.xaml:73`, `Slate.Text.xaml:115`, `Slate.Text.xaml:146`, `Slate.Text.xaml:150` and `WorkspaceTemplates.xaml:189` pair it with `SelectedSurface`: a grid cell, a panel row, a dropdown row twice and a tree row. Gold gets away with it, because `SelectionText` equals `Slate.TextColor` in both themes (`Slate.Light.xaml:20`, `Slate.Light.xaml:29`, `Slate.Dark.xaml:20`, `Slate.Dark.xaml:28`). High Contrast needs that pair as it stands: a selected row there is `Highlight` with `HighlightText` (`Slate.Contrast.xaml:32-33`, `Slate.Contrast.xaml:38-39`).
- **Links draw in the fill roles when hovered and pressed.** `Slate.Light.xaml:290-291` and `Slate.Light.xaml:309`, with the same keys at `Slate.Dark.xaml:294-295` and `Slate.Dark.xaml:313`.
- **The reading overlay.** `xaml/README.md:106`: "If the Windows accent setting is added, this overlay needs its own rule in the contrast gate."

## Reference

`design/windows/xaml/README.md:21-95`: the rules, the table, the contrast gate and a sketch marked "not compiled". `xaml/README.md:29`: a selected row's text is primary, secondary and `AccentText`. `xaml/README.md:97-108`: the reading view's overlay measurements. `readme.md:107`.

## What the README's table does on a default Windows

Read from `SystemColors` on this desktop on 2026-11-03 (Windows 11 build 26300, .NET 10.0.14, the default accent): `AccentColor` `#0078D4`; Light1, 2 and 3 `#0091F8`, `#4CC2FF`, `#99EBFF`; Dark1, 2 and 3 `#0067C0`, `#003E92`, `#001A68`. `xaml/README.md:56` gives `#005FB8` and `#60CDFF`. Those are WinUI's brush constants, not what this API returns.

| Pair, on that blue | Where it draws | Measured | The README's gate |
|---|---|---|---|
| White on the 12% tint: `#FFFFFF` on `#E0EFFA` | Every selected row, light | Lc 9.8, 1.17:1 | Doesn't check it |
| Black on `AccentColorLight2`: `#4CC2FF` | A primary button, dark | Lc 65.4, 10.47:1 | Passes |
| Black on `AccentColorLight1`: `#0091F8` | A pressed primary button, dark | Lc 44.7, 6.41:1 | Passes |
| `AccentColorLight1` on the surface | A pressed link, dark | Lc 40.6, 5.09:1 | Doesn't check it |

Compiled as written, the sketch makes selected rows unreadable in light. Held to the floor as written, it sends `Windows accent` back to gold on every dark default Windows, against `xaml/README.md:42`. So the table changes as well as the gate.

## Design

1. **`SlateAccent.cs`,** from the README's sketch, with the colours passed in so facts can feed them: `AccentSource { Automatic, SlateGold, WindowsAccent }`, an `AccentPalette` record of the seven `SystemColors` values, and `TryApplyWindowsAccent(ResourceDictionary, AccentSource, AccentPalette, bool dark, bool highContrast)`, returning false when gold applies: High Contrast, the Slate gold setting, Automatic with `#0078D4`, or a failed check.
2. **A selected row's text gets its own brush.** `Slate.SelectedTextBrush`, beside `Slate.SelectedSurfaceBrush`: `Slate.TextColor` in light and dark, and `SystemColors.HighlightTextColorKey` in `Slate.Contrast.xaml`, so High Contrast keeps today's pair. The five setters above move to it, and it joins `RequiredSlateBrushKeys` (`ThemeManager.cs:37-69`). The accent doesn't write it, since primary text doesn't follow the accent. `SelectionText` is then only what its name says: `SelectionBackground`'s partner in a text box (`TextBoxSelection.cs:60`), in the editor (`SlateTextEditor.cs:73`) and in the classic `Highlight` pair (`Slate.SystemColors.xaml:18-19`). The classic inactive pair already draws primary text (`Slate.SystemColors.xaml:20-21`). Nothing changes under gold, because the two values are equal. For that reason `ThemeDesignSystemTests.cs:946` and `ThemeDesignSystemTests.cs:1001`, which compare a selected cell's and row's ink with the selection brush, would pass either way, so they move to the new key.
3. **The role table,** corrected from `xaml/README.md:44-54` (DSD14). The README's columns hold except for the dark fills and the overlay, which it left open.

   | Role | Windows accent, light | Windows accent, dark |
   |---|---|---|
   | `Accent` | `AccentColorDark1` | `AccentColorLight3` mixed 25% toward `AccentColorLight2` |
   | `AccentHover` | `AccentColorDark2` | `AccentColorLight3` |
   | `AccentPressed` | `AccentColorDark3` | `AccentColorLight3` mixed 40% toward `AccentColorLight2` |
   | `AccentText` | `AccentColorDark2` | `AccentColorLight3` |
   | `AccentIndicator` | `AccentColorDark1` | `AccentColorLight2` |
   | `TextOnAccent` | white | black |
   | `SelectedSurface` | `AccentColor` 12% over the surface | `AccentColor` 11% over the surface |
   | `SelectionBackground` | `AccentColor` | `AccentColor` |
   | `SelectionText` | white | white |
   | `ReadingSelection` | `AccentColor` at 20% | `AccentColorLight2` at 24% |

   The dark fills keep gold's shape: hover lighter than rest, pressed a little darker. `AccentColor` at gold's 24% is too strong for the overlay in dark, at Lc 74.3 on the surface and 72.1 on raised.
4. **The gate.** It runs on the computed dictionary, and every check must pass.
   - **Text, at |Lc| > 75 and 4.5:1:** `TextOnAccent` on `Accent`, `AccentHover` and `AccentPressed`. `AccentText` on surface, window, raised and `SelectedSurface`. `AccentHover` and `AccentPressed` on surface, for links. `SelectionText` on `SelectionBackground`. `Slate.TextColor`, `Slate.SecondaryTextColor` and `Slate.TertiaryTextColor` on `SelectedSurface`, the last for DS-18's counts.
   - **Marks, at 3:1:** `Accent` and `AccentIndicator` on surface, and `AccentIndicator` on `SelectedSurface`.
   - **The overlay:** primary text under it at |Lc| > 75 on surface and raised, and the highlight at 1.2:1 off both, the measure gold is held to (`ThemeTokenContrastTests.cs:126`). There's no search for an alpha that fits. A hue that fails falls back.

   The text pairs are one list, `SlateAccent.TextPairs`, and `EveryTextBearingPairClearsProjectApcaFloor` runs it over the gold files too, so the gate and gold's fact can't drift apart. Gold's `accent/window` and `accent/surface` stay in its own fact and out of this list: no text draws in the fill role (`ThemeDesignSystemTests.NoTextDrawsInTheAccentFillRole`), and `#0067C0` on the window measures Lc 72.9. `ApcaLc` and `WcagRatio` move unchanged to `SlateContrast.cs` in the app, and the facts call them. Their constants still match `APCAContrast.swift`.

   Measured on the default blue, where everything passes:

   | | Light | Dark |
   |---|---|---|
   | Lowest text pair | `Slate.TertiaryTextColor` on `SelectedSurface`: Lc 75.1, 6.35:1 | `AccentPressed` on surface: Lc 75.5, 10.64:1 |
   | Text selection | Lc 76.2, 4.53:1 | Lc 76.2, 4.53:1 |
   | A selected row: primary, secondary, tertiary, `AccentText` | Lc 93.9, 85.4, 75.1, 81.5 | Lc 98.1, 77.3, 77.3, 83.9 |
   | Text on the fills: rest, hover, pressed | Lc 82.9, 96.6, 104.5 | Lc 81.4, 87.1, 78.1 |
   | Marks: `Accent` and `AccentIndicator` on surface, `AccentIndicator` on `SelectedSurface` | 5.67:1, 5.67:1, 4.84:1 | 11.29:1, 8.31:1, 7.50:1 |
   | The overlay: primary on surface and raised, then the highlight off each | Lc 83.6, 76.2; 1.31:1, 1.29:1 | Lc 80.3, 77.4; 1.65:1, 1.65:1 |

   The margin is thin: `#0070D4`, a hair from the default blue, puts tertiary at Lc 74.7 on its tint and falls back. That's the bar working, and part of DSD14's cost. Nothing here measures a real palette for another hue. Reading one means changing a system setting, so how many of Windows' presets pass is unknown until the owner looks (DSD14).
5. **Automatic's test** is exactly `SystemColors.AccentColor == #0078D4`, as the README says. Windows' own wallpaper-derived accents count as chosen.
6. **The preference.** `AppPreferencesState.AccentColor`, default `automatic`. A `ThemeManager.SetAccent`. `DictionariesFor` gains the source as a parameter, default `SlateGold`, so hosted facts stay gold unless they ask.
7. **Reapply.** On `UserPreferenceChanged` General or Color, re-run `ApplyResources` when the accent source isn't gold and the read colour changed. The test is a pure `SlateAccent.ChangeNeedsReapply(source, previous, current)`, since `ThemeManager` can't be built under test. `ResourcesChanged` already reaches the editor's highlighter (`EditorHighlighting.cs:219`), `FieldPrompt` and the graph.
8. **The row.** `SettingRow` with the label and description from `SettingsWindow.jsx:6`.
9. **No message on fallback,** by design. The log gets one `HostLog` line for diagnosis, with no colour values.

## Tests

Failing first, feeding colours through the palette, since a fact can't change the Windows accent:

- `ASelectedRowClearsTheFloorUnderAnAcceptedAccent`: the regression for the white-on-tint pair. In a real window, in light and dark, under the default blue's computed dictionary, a selected grid cell, panel row, dropdown row and tree row: what each resolves for its foreground on what it resolves for its background, at |Lc| > 75. It reads the controls and not the keys, so a template that goes back to `SelectionText` fails it, at Lc 9.8 in light.
- `TheDefaultBlueIsGoldUnderAutomatic`, `AChosenAccentIsFollowedUnderAutomatic`, `SlateGoldIgnoresWindows`, `WindowsAccentFollowsWhenItPasses`.
- `AnAccentThatFailsAnyCheckFallsBackToGold`: a theory with one palette per rule, including pale yellow in light, dark navy in dark, one that passes the README's WCAG list and fails the floor, and `#0070D4` with the default blue's shades, which fails only tertiary on `SelectedSurface`.
- `TheDefaultBluePassesEveryCheckInBothThemes`: the palette measured above, every pair, the overlay included.
- `GoldClearsEveryPairTheGateChecks`: `SlateAccent.TextPairs` over both gold files.
- `ASelectedRowLooksTheSameUnderGold`: `Slate.SelectedTextBrush` draws the value `Slate.SelectionTextColor` holds in both gold themes, and `HighlightText` in the contrast dictionary.
- `TheRoleTableMatchesTheReadme`: role to `SystemColors` property or mix, light and dark.
- `EverythingGoldFollowsTheAccent`: under a test accent, a primary button, a selected row's pill, a checked box, a focused field's underline and a pressed link resolve to their table entries.
- `AnAccentChangeNeedsOneReapply`: over `ChangeNeedsReapply`.

Launched app: can't be driven without changing a system setting, so a hosted render through the palette in both themes, with a selected row in each template, plus an owner look if they change their accent.

## Docs and registers

`xaml/README.md`: the gate (`xaml/README.md:35-42`), the table (`xaml/README.md:44-54`) and the values (`xaml/README.md:56`) become what's above, "sketch, not compiled" (`xaml/README.md:58`) becomes what shipped, and the overlay's rule goes in at `xaml/README.md:106`. `design/windows/xaml/Slate.Light.xaml` and `Slate.Dark.xaml` get `Slate.SelectedTextBrush`, and `design/windows/CLAUDE.md:22` gets a line on it, since regenerating the theme without that key fails `ThemeManager`'s census. The Claude Design project picks this up at the owner's next `/design-sync`. `readme.md:107` holds: it says what a design shows, not what the app reads.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~SlateAccent|FullyQualifiedName~ThemeTokenContrastTests|FullyQualifiedName~ThemeDesignSystemTests|FullyQualifiedName~AppPreferences"`.

## Acceptance

- [ ] The README's every claim has a fact.
- [ ] A selected row clears the floor under every accepted accent, in every template, by a fact that reads the controls.
- [ ] The gate and gold's fact run one list of text pairs.
- [ ] The overlay's rule is written down with measurements.
- [ ] Gold stays the default on a default Windows, and a selected row under gold draws the colours it drew before.

## Brief

Read `design/windows/xaml/README.md:21-108`, `ThemeManager.cs:204-259`, `ThemeTokenContrastTests.cs`, `Slate.Text.xaml:51-157`, `AppPreferencesStore.cs`, DS-10's row. The README's sketch isn't the target: this spec's table and gate are. Facts first, through the palette, starting with the selected row. Then `Slate.SelectedTextBrush`, `SlateContrast`, `SlateAccent`, the overlay, the preference, the row and the reapply. Commit: `feat(windows): the accent can follow windows, and an accent that fails contrast falls back to gold`.
