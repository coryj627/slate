# DS-11 — The accent can follow Windows, and never fails contrast

Issues: filed by DS-0. Size M. Lanes: windows. AT: — (no spoken change; the fallback is silent by design). Wave 3. Depends on: DS-10. Decision: none. Interlock: none.

## Goal

Appearance gets its second row: Accent color: Automatic, Slate gold or Windows accent. Everything gold follows it. A Windows accent that would fail contrast falls back to gold without a message, as the design says.

## Current state on main

- **Gold only.** No `SystemColors.Accent*` read anywhere in `src`.
- **The roles are ready.** `Themes/Slate.Dark.xaml` and `Slate.Light.xaml` hold the nine `Slate.<Role>Color` values, and Fluent's accent brushes read them by `DynamicResource` (for example `Slate.Dark.xaml:286`).
- **The hook.** `ThemeManager.DictionariesFor` (`ThemeManager.cs:204-238`) loads a fresh Slate dictionary and already writes computed values into it (`:216-217`).
- **Reapplying.** `SystemEvents_UserPreferenceChanged` (`:144-174`) reapplies only when light or dark changed.
- **The gate that exists:** `ThemeTokenContrastTests` covers the gold pairs.
- **The reading overlay.** `xaml/README.md:106`: "If the Windows accent setting is added, this overlay needs its own rule in the contrast gate."

## Reference

`design/windows/xaml/README.md:21-95`: the rules, the table, the contrast gate and a sketch marked "not compiled". `:97-108`: the reading view's overlay measurements. `readme.md:107`.

## Design

1. **`SlateAccent.cs`,** from the README's sketch: `AccentSource { Automatic, SlateGold, WindowsAccent }`, and `TryApplyWindowsAccent(ResourceDictionary, AccentSource, bool dark)`, returning false when gold applies: High Contrast, the Slate gold setting, Automatic with `#0078D4`, or a failed check.
2. **The gate:** the README's list, by WCAG 2 contrast: `TextOnAccent` on the three fills at 4.5:1; `AccentText` on surface and window at 4.5:1; `Accent` and `AccentIndicator` on surface, and `AccentIndicator` on `SelectedSurface`, at 3:1; `SelectionText` on `SelectionBackground` at 4.5:1.
3. **The reading overlay's rule,** which the README leaves open. `Slate.ReadingSelectionColor` becomes the accent at an alpha chosen so primary text under it stays above Lc 75 on both surfaces, the same bar as gold's. Try the gold alpha (20% light, 24% dark). If no alpha clears the floor while still showing against the surface at 1.2:1, the whole accent falls back to gold. Add this to `PassesContrastGate`, and write the result into the README.
4. **Automatic's test** is exactly `SystemColors.AccentColor == #0078D4`, as the README says. Windows' own wallpaper-derived accents count as chosen.
5. **The preference.** `AppPreferencesState.AccentColor`, default `automatic`. A `ThemeManager.SetAccent`. `DictionariesFor` gains the source as a parameter, default `SlateGold`, so hosted facts stay gold unless they ask.
6. **Reapply.** On `UserPreferenceChanged` General or Color, re-run `ApplyResources` when the accent source isn't gold and the read colour changed. The test is a pure `SlateAccent.ChangeNeedsReapply(source, previous, current)`, since `ThemeManager` can't be built under test. `ResourcesChanged` already reaches the editor's highlighter (`EditorHighlighting.cs:219`), `FieldPrompt` and the graph.
7. **The row.** `SettingRow` with the label and description from `SettingsWindow.jsx:6`.
8. **No message on fallback,** by design. The log gets one `HostLog` line for diagnosis, with no colour values.

## Tests

Failing first, feeding colours through a seam, since a fact can't change the Windows accent:

- `TheDefaultBlueIsGoldUnderAutomatic`, `AChosenAccentIsFollowedUnderAutomatic`, `SlateGoldIgnoresWindows`, `WindowsAccentFollowsWhenItPasses`.
- `AnAccentThatFailsAnyCheckFallsBackToGold`: a theory with pale yellow in light, dark navy in dark, one per rule.
- `TheDefaultBluePassesEveryCheckInBothThemes`: the README's claim, including the overlay.
- `TheRoleTableMatchesTheReadme`: role to `SystemColors` property, light and dark.
- `EverythingGoldFollowsTheAccent`: under a test accent, a primary button, a selected row's pill, a checked box and a focused field's underline resolve to it.
- `AnAccentChangeNeedsOneReapply`: over `ChangeNeedsReapply`.

Launched app: can't be driven without changing a system setting, so a seam-driven hosted render in both themes, plus an owner look if they change their accent.

## Docs and registers

`xaml/README.md:58-108`: "sketch, not compiled" becomes what shipped, and the overlay rule. `readme.md:107` holds.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~SlateAccent|FullyQualifiedName~ThemeTokenContrastTests|FullyQualifiedName~ThemeDesignSystemTests|FullyQualifiedName~AppPreferences"`.

## Acceptance

- [ ] The README's every claim has a fact.
- [ ] The overlay's rule is written down with measurements.
- [ ] Gold stays the default on a default Windows.

## Brief

Read `design/windows/xaml/README.md:21-108`, `ThemeManager.cs:204-259`, `AppPreferencesStore.cs`, DS-10's row. Compile the sketch for real, add the overlay's rule, the preference, the row and the reapply. Facts first through a colour seam. Commit: `feat(windows): the accent can follow windows, and an accent that fails contrast falls back to gold`.
