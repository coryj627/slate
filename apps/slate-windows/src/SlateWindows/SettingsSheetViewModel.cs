// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows;

/// <summary>A category in the Settings sheet's list.</summary>
internal sealed record SettingsCategory(string Label, string Glyph);

/// <summary>
/// DS-10 (#1811): the Settings sheet. It opens on its first category, and a change to a
/// setting applies at once, so there's nothing to save or cancel. W8-1 (#751) adds its tabs
/// as categories.
/// </summary>
internal sealed class SettingsSheetViewModel : BindableBase
{
    internal static readonly SettingsCategory Appearance = new("Appearance", "\uE790");

    private readonly ThemePreference _theme;
    private SettingsCategory _selectedCategory;

    internal SettingsSheetViewModel(ThemePreference theme, IReadOnlyList<SettingsCategory>? categories = null)
    {
        _theme = theme;
        Categories = categories ?? new[] { Appearance };
        _selectedCategory = Categories[0];
    }

    public IReadOnlyList<SettingsCategory> Categories { get; }

    public SettingsCategory SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (value is not null)
            {
                SetField(ref _selectedCategory, value);
            }
        }
    }

    public IReadOnlyList<ThemeOption> ThemeOptions => ThemeChoice.Options;

    public ThemeOption SelectedTheme
    {
        get => ThemeChoice.Options.First(option => option.Value == _theme.Current);
        set
        {
            if (value is not null)
            {
                _theme.Choose(value.Value);
                OnPropertyChanged();
            }
        }
    }
}
