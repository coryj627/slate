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
/// DS-11 (#1812): the accent can follow Windows, as Windows gives it, whatever its contrast
/// (DSD16). Choosing an accent from colours that clear contrast is #1842. The rules are
/// <c>design/windows/xaml/README.md</c>'s.
/// </summary>
internal static class SlateAccent
{
    internal const string Automatic = "automatic";
    internal const string SlateGold = "gold";
    internal const string WindowsAccent = "windows";

    private const string Surface = "Slate.SurfaceColor";
    private const string Selected = "Slate.SelectedSurfaceColor";
    private const string ReadingSelection = "Slate.ReadingSelectionColor";

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

    /// <summary>Writes the Windows accent into a freshly loaded Slate dictionary's roles.</summary>
    /// <returns><see langword="false"/>, with the dictionary left gold, when gold applies: High
    /// Contrast, the Slate gold setting, or Automatic with Windows' default accent.</returns>
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

        foreach ((string key, Color color) in RolesFor(palette, (Color)slate[Surface], ((Color)slate[ReadingSelection]).A, dark))
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

/// <summary>
/// WPF holds the accent's colours until a message of its own clears them, and that message can
/// come after any event of Windows' that reaches the app, so no read at a chosen moment is sure
/// of the new colour. A resource reference on a live window is read again right after the
/// clearing, so a change in it is the moment the new colours can be read.
/// </summary>
internal static class SystemAccentWatch
{
    private static readonly DependencyProperty AccentProperty = DependencyProperty.RegisterAttached(
        "Accent",
        typeof(Color),
        typeof(SystemAccentWatch),
        new PropertyMetadata(default(Color), (root, _) => ((Action?)root.GetValue(ChangedProperty))?.Invoke()));

    private static readonly DependencyProperty ChangedProperty = DependencyProperty.RegisterAttached(
        "Changed",
        typeof(Action),
        typeof(SystemAccentWatch));

    /// <summary>Calls <paramref name="changed"/> once now, with the first read, and again each
    /// time the colour is a different one. <paramref name="root"/> has to be a window that
    /// outlives the watching: WPF only walks what's shown.</summary>
    internal static void Attach(FrameworkElement root, Action changed)
    {
        root.SetValue(ChangedProperty, changed);
        root.SetResourceReference(AccentProperty, SystemColors.AccentColorKey);
    }
}
