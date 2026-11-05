// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Media;

namespace SlateWindows;

internal enum AccentSource
{
    Automatic,
    SlateGold,
    WindowsAccent,
}

/// <summary>One of the accent setting's choices: what's stored, and what it's called.</summary>
internal sealed record AccentOption(string Value, string Label);

/// <summary>What Windows reports for its accent. A value, so a fact can feed one in:
/// changing the real one means changing a system setting.</summary>
internal sealed record AccentPalette(
    Color Accent,
    Color Light1,
    Color Light2,
    Color Light3,
    Color Dark1,
    Color Dark2,
    Color Dark3)
{
    internal static AccentPalette ReadSystem() => new(
        SystemColors.AccentColor,
        SystemColors.AccentColorLight1,
        SystemColors.AccentColorLight2,
        SystemColors.AccentColorLight3,
        SystemColors.AccentColorDark1,
        SystemColors.AccentColorDark2,
        SystemColors.AccentColorDark3);
}

/// <summary>
/// DS-11 (#1812): the accent can follow Windows, and every text it draws still clears 4.5:1
/// and every fill and mark 3:1 (DSD14). A Windows accent that can't falls back to gold
/// without a message, as the design says. The rules are <c>design/windows/xaml/README.md</c>'s.
/// </summary>
internal static class SlateAccent
{
    internal const string Automatic = "automatic";
    internal const string SlateGold = "gold";
    internal const string WindowsAccent = "windows";

    private const string Surface = "Slate.SurfaceColor";
    private const string Raised = "Slate.RaisedSurfaceColor";
    private const string Selected = "Slate.SelectedSurfaceColor";
    private const string ReadingSelection = "Slate.ReadingSelectionColor";
    private const double TextMinimum = 4.5;
    private const double MarkMinimum = 3;

    /// <summary>What the overlay's highlight keeps off the surface: as far as the text box's
    /// solid gold selection stands off white.</summary>
    private const double OverlayStandOff = 1.2;

    /// <summary>What Automatic treats as no choice at all. Windows' wallpaper-derived accents
    /// count as chosen.</summary>
    private static readonly Color WindowsDefault = Color.FromRgb(0x00, 0x78, 0xD4);

    /// <summary>The design's three, in its order.</summary>
    internal static readonly IReadOnlyList<AccentOption> Options = new[]
    {
        new AccentOption(Automatic, "Automatic"),
        new AccentOption(SlateGold, "Slate gold"),
        new AccentOption(WindowsAccent, "Windows accent"),
    };

    /// <summary>Every text the accent can draw on, at 4.5:1. One list: the gate runs it on a
    /// computed accent, and a fact runs it over the gold themes, so a pair added for one
    /// reaches the other.</summary>
    internal static readonly IReadOnlyList<(string Text, string Background)> TextPairs = new[]
    {
        ("Slate.TextOnAccentColor", "Slate.AccentColor"),
        ("Slate.TextOnAccentColor", "Slate.AccentHoverColor"),
        ("Slate.TextOnAccentColor", "Slate.AccentPressedColor"),
        ("Slate.AccentTextColor", Surface),
        ("Slate.AccentTextColor", "Slate.WindowBackgroundColor"),
        ("Slate.AccentTextColor", Raised),
        ("Slate.AccentTextColor", Selected),

        // A link, hovered and pressed, draws in the fill roles.
        ("Slate.AccentHoverColor", Surface),
        ("Slate.AccentPressedColor", Surface),
        ("Slate.SelectionTextColor", "Slate.SelectionBackgroundColor"),

        // A selected row. Tertiary is for DS-18's counts.
        ("Slate.TextColor", Selected),
        ("Slate.SecondaryTextColor", Selected),
        ("Slate.TertiaryTextColor", Selected),
    };

    /// <summary>Fills and marks, at 3:1.</summary>
    internal static readonly IReadOnlyList<(string Mark, string Background)> MarkPairs = new[]
    {
        ("Slate.AccentColor", Surface),
        ("Slate.AccentIndicatorColor", Surface),
        ("Slate.AccentIndicatorColor", Selected),
    };

    /// <summary>A stored value this build knows, else the default.</summary>
    internal static string Normalize(string? stored) =>
        stored is SlateGold or WindowsAccent ? stored : Automatic;

    internal static AccentSource SourceOf(string? stored) =>
        Normalize(stored) switch
        {
            SlateGold => AccentSource.SlateGold,
            WindowsAccent => AccentSource.WindowsAccent,
            _ => AccentSource.Automatic,
        };

    /// <summary>The design's table. Selection is the accent itself, and the reading view's
    /// overlay is the accent at gold's alpha, which <paramref name="overlayAlpha"/> carries.</summary>
    internal static IReadOnlyDictionary<string, Color> RolesFor(
        AccentPalette palette,
        Color surface,
        byte overlayAlpha,
        bool dark) => new Dictionary<string, Color>
        {
            ["Slate.AccentColor"] = dark ? palette.Light2 : palette.Dark1,
            ["Slate.AccentHoverColor"] = dark ? palette.Light3 : palette.Dark2,
            ["Slate.AccentPressedColor"] = dark ? palette.Light1 : palette.Dark3,
            ["Slate.AccentTextColor"] = dark ? palette.Light3 : palette.Dark2,
            ["Slate.AccentIndicatorColor"] = dark ? palette.Light2 : palette.Dark1,
            ["Slate.TextOnAccentColor"] = dark ? Colors.Black : Colors.White,
            [Selected] = SlateContrast.Mix(palette.Accent, surface, dark ? 0.11 : 0.12),
            ["Slate.SelectionBackgroundColor"] = palette.Accent,
            ["Slate.SelectionTextColor"] = Colors.White,
            [ReadingSelection] = Color.FromArgb(overlayAlpha, palette.Accent.R, palette.Accent.G, palette.Accent.B),
        };

    /// <summary>The gate: the name of each check these colours fail, in order. A name is
    /// role keys and no colour values, so it can go in the log.</summary>
    internal static IReadOnlyList<string> FailedChecks(Func<string, Color> color)
    {
        var failed = new List<string>();
        foreach ((string text, string background) in TextPairs)
        {
            if (SlateContrast.WcagRatio(color(text), color(background)) < TextMinimum)
            {
                failed.Add($"{text} on {background}");
            }
        }

        foreach ((string mark, string background) in MarkPairs)
        {
            if (SlateContrast.WcagRatio(color(mark), color(background)) < MarkMinimum)
            {
                failed.Add($"{mark} on {background}");
            }
        }

        // The reading view's selection is an overlay on top of the text, so it tints both.
        // There's no search for an alpha that fits: a hue that fails falls back.
        Color overlay = color(ReadingSelection);
        foreach (string surfaceKey in new[] { Surface, Raised })
        {
            Color highlight = SlateContrast.Over(overlay, color(surfaceKey));
            if (SlateContrast.WcagRatio(SlateContrast.Over(overlay, color("Slate.TextColor")), highlight) < TextMinimum)
            {
                failed.Add($"Slate.TextColor under {ReadingSelection} on {surfaceKey}");
            }

            if (SlateContrast.WcagRatio(highlight, color(surfaceKey)) < OverlayStandOff)
            {
                failed.Add($"{ReadingSelection} off {surfaceKey}");
            }
        }

        return failed;
    }

    /// <summary>Writes the Windows accent into a freshly loaded Slate dictionary's roles.</summary>
    /// <returns><see langword="false"/>, with the dictionary left gold, when gold applies: High
    /// Contrast, the Slate gold setting, Automatic with Windows' default accent, or a failed
    /// check. A failed check tells the log and not the reader, by design.</returns>
    internal static bool TryApplyWindowsAccent(
        ResourceDictionary slate,
        AccentSource source,
        AccentPalette palette,
        bool dark,
        bool highContrast)
    {
        if (highContrast
            || source == AccentSource.SlateGold
            || (source == AccentSource.Automatic && palette.Accent == WindowsDefault))
        {
            return false;
        }

        IReadOnlyDictionary<string, Color> roles = RolesFor(
            palette,
            (Color)slate[Surface],
            ((Color)slate[ReadingSelection]).A,
            dark);
        IReadOnlyList<string> failed = FailedChecks(key => roles.TryGetValue(key, out Color role) ? role : (Color)slate[key]);
        if (failed.Count > 0)
        {
            HostLog.WriteDetail(HostDiagnosticEvent.AccentFellBackToGold, failed[0]);
            return false;
        }

        foreach ((string key, Color color) in roles)
        {
            slate[key] = color;
        }

        return true;
    }

    /// <summary>Whether what Windows reports now means the resources have to be built again.
    /// Pure, because <see cref="ThemeManager"/> can't be built under test.</summary>
    internal static bool ChangeNeedsReapply(AccentSource source, AccentPalette previous, AccentPalette current) =>
        source != AccentSource.SlateGold && previous != current;
}
