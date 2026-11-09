// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Media;

namespace SlateWindows;

/// <summary>
/// DS-11 (#1812): WCAG 2's contrast ratio and the blends the accent's roles need. The tint
/// a selected row draws is a blend, so the app uses these; the facts measure with them. The
/// project's floor for gold, APCA, stays with gold's facts.
/// </summary>
internal static class SlateContrast
{
    internal static double WcagRatio(Color first, Color second)
    {
        static double Channel(byte value)
        {
            double c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        static double Luminance(Color c) =>
            (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));

        double a = Luminance(first);
        double b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary><paramref name="share"/> of <paramref name="top"/> over an opaque colour,
    /// channel by channel.</summary>
    internal static Color Mix(Color top, Color under, double share)
    {
        byte Channel(byte over, byte below) => (byte)Math.Round((share * over) + ((1 - share) * below));

        return Color.FromRgb(Channel(top.R, under.R), Channel(top.G, under.G), Channel(top.B, under.B));
    }

    /// <summary>A translucent colour over an opaque one.</summary>
    internal static Color Over(Color top, Color under) => Mix(top, under, top.A / 255.0);
}
