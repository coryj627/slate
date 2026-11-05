# DS-11 — The accent can follow Windows

Issues: filed by DS-0. Size M. Lanes: windows. AT: — (no spoken change; the fallback is silent by design). Wave 3. Depends on: DS-10. Decision: DSD14. Interlock: none.

## Goal

Appearance gets its second row: Accent color: Automatic, Slate gold or Windows accent. Everything gold follows it. Every text an accepted accent draws clears 4.5:1, and every fill and mark 3:1, selected rows included (DSD14). A Windows accent that can't falls back to gold without a message, as the design says.

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

Compiled as written, the sketch makes selected rows unreadable in light, so they get their own brush and the gate gains their pairs. The other three rows pass the owner's bar (DSD14), and they sit below the floor gold clears. That's accepted for an accent the reader chose, so the table stays.

## Design

Steps 4 and 9, the gate and its silent fallback, were built and then taken out on the owner's call (DSD16): see the notes.

1. **`SlateAccent.cs`,** from the README's sketch, with the colours passed in so facts can feed them: `AccentSource { Automatic, SlateGold, WindowsAccent }`, an `AccentPalette` record of the seven `SystemColors` values, and `TryApplyWindowsAccent(ResourceDictionary, AccentSource, AccentPalette, bool dark, bool highContrast)`, returning false when gold applies: High Contrast, the Slate gold setting, Automatic with `#0078D4`, or a failed check.
2. **A selected row's text gets its own brush.** `Slate.SelectedTextBrush`, beside `Slate.SelectedSurfaceBrush`: `Slate.TextColor` in light and dark, and `SystemColors.HighlightTextColorKey` in `Slate.Contrast.xaml`, so High Contrast keeps today's pair. The five setters above move to it, and it joins `RequiredSlateBrushKeys` (`ThemeManager.cs:37-69`). The accent doesn't write it, since primary text doesn't follow the accent. `SelectionText` is then only what its name says: `SelectionBackground`'s partner in a text box (`TextBoxSelection.cs:60`), in the editor (`SlateTextEditor.cs:73`) and in the classic `Highlight` pair (`Slate.SystemColors.xaml:18-19`). The classic inactive pair already draws primary text (`Slate.SystemColors.xaml:20-21`). Nothing changes under gold, because the two values are equal. For that reason `ThemeDesignSystemTests.cs:946` and `ThemeDesignSystemTests.cs:1001`, which compare a selected cell's and row's ink with the selection brush, would pass either way, so they move to the new key.
3. **The role table** is `xaml/README.md:44-54`'s, dark fills included, with the overlay added: `Slate.ReadingSelectionColor` is `AccentColor` at gold's alphas, 20% in light and 24% in dark.
4. **The gate (DSD14).** It runs on the computed dictionary, and every check must pass.
   - **Text, at 4.5:1:** `TextOnAccent` on `Accent`, `AccentHover` and `AccentPressed`. `AccentText` on surface, window, raised and `SelectedSurface`. `AccentHover` and `AccentPressed` on surface, for links. `SelectionText` on `SelectionBackground`. `Slate.TextColor`, `Slate.SecondaryTextColor` and `Slate.TertiaryTextColor` on `SelectedSurface`, the last for DS-18's counts.
   - **Fills and marks, at 3:1:** `Accent` and `AccentIndicator` on surface, and `AccentIndicator` on `SelectedSurface`.
   - **The overlay:** primary text under it at 4.5:1 on surface and raised, and the highlight at 1.2:1 off both, the stand-off gold is held to (`ThemeTokenContrastTests.cs:126`). There's no search for an alpha that fits. A hue that fails falls back.

   The text pairs are one list, `SlateAccent.TextPairs`. The gate runs it, and a fact runs it over the gold files, so a pair added for one reaches the other. `EveryTextBearingPairClearsProjectApcaFloor` keeps holding gold to |Lc| > 75. `WcagRatio` moves unchanged to `SlateContrast.cs` in the app, and the facts call it. `ApcaLc` stays in the test.

   Measured on the default blue, where everything passes:

   | | Light | Dark |
   |---|---|---|
   | Lowest text pair | Text selection: 4.53:1 | Text selection: 4.53:1 |
   | A selected row: primary, secondary, tertiary, `AccentText` | 15.28:1, 9.69:1, 6.35:1, 8.51:1 | 13.80:1, 10.15:1, 10.15:1, 11.23:1 |
   | Text on the fills: rest, hover, pressed | 5.67:1, 9.98:1, 15.58:1 | 10.47:1, 15.68:1, 6.41:1 |
   | Links: rest, hover, pressed, on the surface | 9.98:1, 9.98:1, 15.58:1 | 12.45:1, 12.45:1, 5.09:1 |
   | Fills and marks: `Accent` and `AccentIndicator` on surface, `AccentIndicator` on `SelectedSurface` | 5.67:1, 5.67:1, 4.84:1 | 8.31:1, 8.31:1, 7.50:1 |
   | The overlay: primary on surface and raised, then the highlight off each | 11.23:1, 9.92:1; 1.31:1, 1.29:1 | 8.63:1, 7.68:1; 1.29:1, 1.27:1 |

   Text selection clears by a hair, so a hue a little lighter than the default blue falls back on that pair alone. Nothing here measures a real palette for another hue. Reading one means changing a system setting, so how many of Windows' presets pass is unknown until the owner looks (plan 46 §8).
5. **Automatic's test** is exactly `SystemColors.AccentColor == #0078D4`, as the README says. Windows' own wallpaper-derived accents count as chosen.
6. **The preference.** `AppPreferencesState.AccentColor`, default `automatic`. A `ThemeManager.SetAccent`. `DictionariesFor` gains the source as a parameter, default `SlateGold`, so hosted facts stay gold unless they ask.
7. **Reapply.** On `UserPreferenceChanged` General or Color, re-run `ApplyResources` when the accent source isn't gold and the read colour changed. The test is a pure `SlateAccent.ChangeNeedsReapply(source, previous, current)`, since `ThemeManager` can't be built under test. `ResourcesChanged` already reaches the editor's highlighter (`EditorHighlighting.cs:219`), `FieldPrompt` and the graph.
8. **The row.** `SettingRow` with the label and description from `SettingsWindow.jsx:6`.
9. **No message on fallback,** by design. The log gets one `HostLog` line for diagnosis, with no colour values.

## Tests

Failing first, feeding colours through the palette, since a fact can't change the Windows accent:

- `ASelectedRowIsReadableUnderAnAcceptedAccent`: the regression for the white-on-tint pair. In a real window, in light and dark, under the default blue's computed dictionary, a selected grid cell, panel row, dropdown row and tree row: what each resolves for its foreground on what it resolves for its background, at 4.5:1. It reads the controls and not the keys, so a template that goes back to `SelectionText` fails it, at 1.17:1 in light.
- `TheDefaultBlueIsGoldUnderAutomatic`, `AChosenAccentIsFollowedUnderAutomatic`, `SlateGoldIgnoresWindows`, `WindowsAccentFollowsWhenItPasses`.
- `AnAccentThatFailsAnyCheckFallsBackToGold`: a theory with one palette per rule, including pale yellow in light and dark navy in dark.
- `TheDefaultBluePassesEveryCheckInBothThemes`: the palette measured above, every pair, the overlay included.
- `GoldClearsEveryPairTheGateChecks`: `SlateAccent.TextPairs` over both gold files.
- `ASelectedRowLooksTheSameUnderGold`: `Slate.SelectedTextBrush` draws the value `Slate.SelectionTextColor` holds in both gold themes, and `HighlightText` in the contrast dictionary.
- `TheRoleTableMatchesTheReadme`: role to `SystemColors` property, light and dark.
- `EverythingGoldFollowsTheAccent`: under a test accent, a primary button, a selected row's pill, a checked box, a focused field's underline and a pressed link resolve to their table entries.
- `AnAccentChangeNeedsOneReapply`: over `ChangeNeedsReapply`.

Launched app: can't be driven without changing a system setting, so a hosted render through the palette in both themes, with a selected row in each template, plus an owner look if they change their accent.

## Docs and registers

`xaml/README.md`: the gate (`xaml/README.md:35-42`) gains the selected row's and the links' pairs, `SelectionText` is named as text selection's alone (`xaml/README.md:54`), "sketch, not compiled" (`xaml/README.md:58`) becomes what shipped, and the overlay's rule goes in at `xaml/README.md:106`. `design/windows/xaml/Slate.Light.xaml` and `Slate.Dark.xaml` get `Slate.SelectedTextBrush`, and `design/windows/CLAUDE.md:22` gets a line on it, since regenerating the theme without that key fails `ThemeManager`'s census. The Claude Design project picks this up at the owner's next `/design-sync`. `readme.md:107` holds: it says what a design shows, not what the app reads.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~SlateAccent|FullyQualifiedName~ThemeTokenContrastTests|FullyQualifiedName~ThemeDesignSystemTests|FullyQualifiedName~AppPreferences"`.

## Acceptance

- [ ] The README's every claim has a fact.
- [ ] A selected row's text clears 4.5:1 under every accepted accent, in every template, by a fact that reads the controls.
- [ ] The gate and gold's fact run one list of text pairs.
- [ ] The overlay's rule is written down with measurements.
- [ ] Gold stays the default on a default Windows, and a selected row under gold draws the colours it drew before.

## Notes from the build

- **The gate is gone (DSD16).** The owner's look at real accents on 2026-11-05 (plan 46 §8) found every hue they tried falling back to gold in dark, so Windows accent looked like it did nothing. Their `#EA005E` passes every check in light; in dark a pressed link (`AccentColorLight1` `#FF0A6F` on the surface) measures 4.39:1, and the reading overlay doesn't stand off the surface or the raised surface by 1.2:1. Their call: drop the requirement, and let the user choose from colours that clear contrast later, which is #1842. The facts for the gate and the fallback went with it; `AnAccentBelowTheBarIsStillFollowed` holds their palette, and `TheDefaultBlueMeasuresWhatTheSpecRecords` keeps the table above as a record.
- **A closed combo box's change is mostly silent under NVDA,** as DS-10 found, so the row posts `AccentChosen`, as the theme's posts `ThemeChosen`. This is a spoken line the spec didn't plan for, and it's why this chunk touches `slate-core` and both hosts' corpus censuses. It says what was chosen, so a Windows accent that falls back to gold says "Accent color: Windows accent." and the fallback stays silent, as the design says.
- **The launched app can show this without changing a system setting.** Windows accent is a choice in the app, and it draws whatever Windows has. `AccentColor_WindowsAccentDrawsAtOnceAndAcrossARestart` is the only fact that reaches `ThemeManager.SetAccent`, since the manager can't be built under test. What still needs a person is another hue, and a live change in Windows (plan 46 §8).
- **WPF holds the accent's colours.** `SystemColors` caches them until `WM_DWMCOLORIZATIONCOLORCHANGED`, `WM_SYSCOLORCHANGE` or `WM_THEMECHANGED` reaches its own window (`SystemResources.cs`, `SystemColors.cs` in dotnet/wpf), which can be after `UserPreferenceChanged` reaches ours, so no read at a chosen moment is sure of the new colour. The manager watches instead: `SystemAccentWatch` holds a resource reference to `SystemColors.AccentColorKey` on the main window, and WPF reads that again right after it clears its cache. `WpfClearingItsColoursReachesTheWatch` runs WPF's own path with a colour planted in that cache, and Windows' real change is the owner's look.
- **A sixth setter.** DS-9's checked icon toggle drew `SelectionText` on `SelectedSurface` too, so it moved with the spec's five.
- **`DevicePreference`** is `ThemePreference` with its one line of difference taken out, because the accent's preference is otherwise the same class.
- **Some checks have the room never to fail on a colour from Windows:** primary, secondary and tertiary text on `SelectedSurface`, and primary text under the overlay. `EveryCheckCanFail` proves they're checked, by feeding one grey for every role, and the rest have a real palette each.

## Brief

Read `design/windows/xaml/README.md:21-108`, `ThemeManager.cs:204-259`, `ThemeTokenContrastTests.cs`, `Slate.Text.xaml:51-157`, `AppPreferencesStore.cs`, DS-10's row. The target is the README's table, with this spec's gate and brush. Facts first, through the palette, starting with the selected row. Then `Slate.SelectedTextBrush`, `SlateContrast`, `SlateAccent`, the overlay, the preference, the row and the reapply. Commit: `feat(windows): the accent can follow windows, and an accent that fails contrast falls back to gold`.
