// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows;

/// <summary>One of the theme setting's choices: what's stored, and what it's called.</summary>
internal sealed record ThemeOption(string Value, string Label);

/// <summary>
/// DS-10 (#1811): the theme setting. Pure, because <see cref="ThemeManager"/> can't be built
/// under test, and a device preference, so a vault's settings don't touch it.
/// </summary>
internal static class ThemeChoice
{
    internal const string Light = "light";
    internal const string Dark = "dark";
    internal const string UseWindowsSetting = "system";

    /// <summary>The design's three, in its order. The only place their labels are written:
    /// Settings and the palette's Switch Theme… both list these.</summary>
    internal static readonly IReadOnlyList<ThemeOption> Options = new[]
    {
        new ThemeOption(Light, "Light"),
        new ThemeOption(Dark, "Dark"),
        new ThemeOption(UseWindowsSetting, "Use Windows setting"),
    };

    /// <summary>A stored value this build knows, else the default.</summary>
    internal static string Normalize(string? stored) =>
        stored is Light or Dark ? stored : UseWindowsSetting;

    /// <summary>Which theme a preference means. Windows' value only counts under
    /// <see cref="UseWindowsSetting"/>.</summary>
    internal static SlateTheme Resolve(string preference, bool systemIsDark) =>
        Normalize(preference) switch
        {
            Light => SlateTheme.Light,
            Dark => SlateTheme.Dark,
            _ => systemIsDark ? SlateTheme.Dark : SlateTheme.Light,
        };
}
