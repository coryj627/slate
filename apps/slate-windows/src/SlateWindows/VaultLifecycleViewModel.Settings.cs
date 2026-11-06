// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// DS-10 (#1811): the Settings sheet and the theme setting. Here rather than on the
/// workspace because a theme isn't a vault's: they work with no vault open, and outlive one.
/// </summary>
internal sealed partial class VaultLifecycleViewModel
{
    private SettingsSheetViewModel? _settingsSheet;
    private ThemePreference? _themePreference;
    private AccentPreference? _accentPreference;
    private RelayCommand? _openSettingsCommand;
    private RelayCommand? _switchThemeCommand;

    /// <summary>The open sheet, or <see langword="null"/>.</summary>
    public SettingsSheetViewModel? SettingsSheet
    {
        get => _settingsSheet;
        private set => SetField(ref _settingsSheet, value);
    }

    /// <summary>A fact sets its own, with a store of its own and no manager to apply to.</summary>
    internal ThemePreference ThemePreference
    {
        get => _themePreference ??= new ThemePreference(
            new AppPreferencesStore(),
            preference => ThemeManager.Current?.SetPreference(preference));
        set => _themePreference = value;
    }

    /// <summary>A fact sets its own, as for the theme.</summary>
    internal AccentPreference AccentPreference
    {
        get => _accentPreference ??= new AccentPreference(
            new AppPreferencesStore(),
            accent => ThemeManager.Current?.SetAccent(accent));
        set => _accentPreference = value;
    }

    /// <summary>The window's say on whether the sheet may open, and its clearing of what it
    /// supersedes. The command is unguarded otherwise, because the palette invokes before it
    /// dismisses (P9).</summary>
    internal Func<bool>? SettingsOpenAdmission { get; set; }

    public ICommand OpenSettingsCommand => _openSettingsCommand ??= new RelayCommand(_ => OpenSettings(null), _ => true);

    /// <summary>Palette and menu alike: the palette, showing the three themes.</summary>
    public ICommand SwitchThemeCommand => _switchThemeCommand ??= new RelayCommand(_ => Palette.OpenChoices(ThemeChoices()), _ => true);

    public void CloseSettings() => SettingsSheet = null;

    /// <param name="categories">The sheet's list, for a fact: one category gives the
    /// arrows nowhere to go.</param>
    internal void OpenSettings(IReadOnlyList<SettingsCategory>? categories)
    {
        if (SettingsOpenAdmission?.Invoke() != false)
        {
            SettingsSheet = new SettingsSheetViewModel(ThemePreference, AccentPreference, _announce, categories);
            _announce(new A11yEvent.SettingsSheetShown());
        }
    }

    private PaletteChoices ThemeChoices() => new(
        Prompt: "Select a theme",
        CurrentStatus: "Current theme",
        Choices: ThemeChoice.Options.Select(option => new PaletteChoice(option.Value, option.Label)).ToArray(),
        CurrentId: ThemePreference.Current,
        Choose: choice =>
        {
            ThemePreference.Choose(choice.Id);
            _announce(new A11yEvent.ThemeChosen(choice.Label));
        });
}
