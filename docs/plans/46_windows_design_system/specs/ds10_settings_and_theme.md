# DS-10 — There is a Settings surface, and the theme can be chosen

Issues: filed by DS-0; `Refs #751` (W8-1 keeps its tabs). Size L. Lanes: windows. AT: NVDA and Narrator pass. Wave 3. Depends on: DS-2, DS-6, DS-9. Decision: none. Interlock: W8-1 fills this surface; DS-11 adds a row; DS-12 adds a button that opens it.

## Goal

Settings… opens a Settings sheet with the design's layout. Its first category, Appearance, sets the theme: Light, Dark or Use Windows setting. The palette's Switch Theme… offers the same three, with the current one marked. A choice applies at once and survives a restart.

## Current state on main

- **No surface:** no settings view or command in `src` (searched). `slate.settings.open` is in the Mac catalogue, mapped to #751 at `scripts/generate-parity-matrix.py:104`, and its status is pending (`:1101-1107`). The Settings section of the matrix is written at `:1286-1292`.
- **The theme:** `App.xaml.cs:57` starts from `ThemeManager.ReadSystemTheme()`. `ThemeManager.cs:144-174` follows Windows on every preference change. `SetTheme` exists at `:127-136` with no caller.
- **Preferences:** `AppPreferencesStore.cs:16-42` is the device-local record: `SlateDeviceState.Root\preferences.json`, every field defaulting, unknown values normalized at the boundary (`:79-109`).
- **Modal surfaces:** `ModalSurfaces.cs:38` is the enum, CS8509 makes its switches exhaustive (`SlateWindows.csproj:13-20`), and `ModalSurfaceTests.TheMenuDisablesUnderEveryModalSurface` requires a trigger per member. `SheetKeyboardFence.cs:100` and `FocusIntegrityManifestCensus` cover focus.
- **The palette:** `CommandPaletteViewModel.cs:246` has no second-level list; rows at `:33` and `:292`.
- **Chords:** Ctrl+, is free (`chords.json`; Mac has ⌘,).

## Reference

`design/windows/components/patterns/SettingsLayout.prompt.md`, `SettingRow.prompt.md`. `design/windows/xaml/README.md:110-114`. `readme.md:107`. `ui_kits/slate-windows/SettingsWindow.jsx:5-6`, `:43` (an in-window overlay with the scrim), `Panes.jsx:78` ("Select a theme"). `w8_spec.md:10-11`: "additive keys, mac-ignorable".

## Design

1. **The sheet.** `ModalSurface.Settings`, declared last, with a `SettingsSheet` view model on the workspace host, available with or without a vault (theme isn't a vault setting), and `Slate.Shadow64`, scrim and radius from DS-6. `Width` 760 as `MaxWidth`, height to the window less margins.
2. **Layout.** A `Grid` with a `ListBox` of categories (`AutomationId="SettingsCategories"`, a 20 glyph and label, the gold pill from `Slate.PanelRowTemplate`, Up and Down open, only the open one a tab stop, the glyph an `AutomationPresentationTextBlock`, as `SettingsLayout.prompt.md` says) and a `ScrollViewer` for the page: Subtitle title, Body description up to 560 wide, rows 4 apart.
3. **`SettingRow`.** A small control or template: `MinHeight` 68, padding `12,16`, radius 4, `Slate.SurfaceBrush` with `Slate.SubtleBorderBrush`; label Body, description Caption in secondary text. The control's `AutomationProperties.LabeledBy` points at the label and `HelpText` carries the description, as the prompt says.
4. **Appearance,** icon `E790`: one row now, **Theme**, a `ComboBox` based on `Slate.ComboRowBase`, with the design's three labels. DS-11 adds Accent color. The search field appears when a second category does, since filtering one row helps nobody.
5. **The preference.** `AppPreferencesState` gains `Theme` (`"system"`, `"light"`, `"dark"`), defaulting to `system`, unknown values normalized. A pure `ThemeChoice.Resolve(preference, systemIsDark)` decides, so a fact can reach it (plan 46 §4 rule 12). `ThemeManager` takes the preference at construction, gains `SetPreference`, and applies what `Resolve` says. `ThemeManager.cs:156-164` runs only under `system`. `App.xaml.cs:57` reads the store first, so the first paint is right.
6. **Commands.** `slate.settings.open`, "Settings…", section as the Mac has it, Ctrl+,, in the File menu and the palette, with the registrar and `chords.json` entries. `slate.view.switchTheme`, "Switch Theme…", palette and Workspace menu, `WINDOWS_ONLY_COMMANDS`. Remap `generate-parity-matrix.py:104` to this chunk's issue with its evidence, and split the literal row at `generate-parity-matrix.py:1292` so Appearance can be delivered while the rest stays #751's.
7. **Switch Theme….** The palette gains one level: a command can replace its rows with choices and a placeholder. Selecting one applies and closes; Esc returns to commands or closes, as today. The current choice carries `AutomationProperties.ItemStatus` and a check glyph. Any composed string goes through core (plan 46 §4 rule 3); the three labels are the same static strings Settings uses, kept in one place.
8. **Focus.** Esc closes and returns focus to the trigger (`SheetKeyboardFence`). A theme change while the sheet is open keeps focus on the combo.
9. **Tests' independence.** `DictionariesFor` still takes an explicit theme, so no hosted fact reads the preference.

## Tests

Failing first:

- `TheSettingsSheetIsAModalSurface` (the existing mapping tests go red until the trigger and map row exist).
- `AThemeChoiceSurvivesARestart`: store round trip. `TheChoiceResolves`: a theory over `ThemeChoice.Resolve`; `Light` ignores the system's value, `Use Windows setting` follows it.
- `AnUnknownThemeIsTheDefault`, beside `AppPreferencesStoreTests`.
- `TheCategoryListMovesWithArrowsAndHasOneStop`.
- `ASettingRowLabelsItsControl`: `LabeledBy`, `HelpText`.
- `SwitchThemeListsTheThreeChoicesWithTheCurrentOneMarked`, `EscFromTheChoicesGoesBack`.
- Gate: `ThemePreference_ChoosingLightStaysLightAcrossARestart`: the launched app, with its device state under `SLATE_LOCAL_APP_DATA`. Its first capture after the restart is light, which is also what shows no flash at startup.
- Gate: `SettingsSheet_NamesFocusAndAxe_AreClean`: axe clean, Tab cycles, Esc returns focus.

Launched app: the sheet in dark, then in light by choosing Light. This is the first time light can be seen in the launched app here; capture every surface in it once, and file what it finds.

## Docs and registers

`parity_matrix.md` regenerated. `chords.json`. `w_c_matrix.md` gains the new surface's rows. `xaml/README.md:114`: done. Comment on #751: the surface exists, and its tabs go here.

## Verification

Plan 46 §7's loop with `--filter "FullyQualifiedName~ModalSurfaceTests|FullyQualifiedName~AppPreferences|FullyQualifiedName~CommandPaletteTests|FullyQualifiedName~Settings|FullyQualifiedName~CommandDriftTests"`; `python scripts/generate-parity-matrix.py --check`; the gate fact locally.

AT: NVDA and Narrator pass on the new surface: open by chord and palette; hear the dialog, category, Theme label, description and value; change it; Esc; focus back. Switch Theme…'s list, including which one is current. Record the phrases.

## Acceptance

- [ ] Choosing Light in a dark-mode Windows gives a light app that stays light after a restart.
- [ ] The Windows preference is followed only under Use Windows setting.
- [ ] The gate fact and the AT pass are recorded.

## Brief

Read `ModalSurfaces.cs`, `ModalSurfaceTests.cs:1660-1745`, `MainWindow.xaml:3202-3284` (a sheet to copy), `SheetKeyboardFence.cs`, `AppPreferencesStore.cs`, `ThemeManager.cs:127-174`, `CommandPaletteViewModel.cs`, `SettingsLayout.prompt.md`, `SettingRow.prompt.md`, `SettingsWindow.jsx`. Build the preference, then the surface, then the commands, then the palette's level. Facts first. Commit: `feat(windows): settings has a surface, and appearance sets the theme`.
