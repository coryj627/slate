// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Media;

namespace SlateWindows.Canvas;

/// <summary>
/// Canvas color arithmetic (§D D13; mac's palette method, ported):
/// token LOOKUPS live in the theme dictionaries; this class owns the
/// one composite rule — a preset or hex tint at
/// <see cref="FillTintFraction"/> over the opaque surface — so the
/// static fill tokens, a raw hex card's runtime fill, and the contrast
/// rows that gate both all take the same arithmetic. Meaning never
/// rides color alone: the color NAME travels in text (core's
/// color_name), and this class only paints.
/// </summary>
internal static class CanvasPalette
{
    /// <summary>How much of the tint survives compositing into a card
    /// fill — low enough that the text token keeps Lc > 75 on every
    /// preset and any hex, high enough to read as the color it
    /// names (mac's pinned fraction).</summary>
    internal const double FillTintFraction = 0.18;

    /// <summary>Obsidian's six preset RGBs — the same reference points
    /// as core's nearest-preset naming.</summary>
    internal static Color? PresetTint(string? raw) => raw switch
    {
        "1" => Color.FromRgb(0xFB, 0x46, 0x4C),
        "2" => Color.FromRgb(0xE9, 0x97, 0x3F),
        "3" => Color.FromRgb(0xE0, 0xDE, 0x71),
        "4" => Color.FromRgb(0x44, 0xCF, 0x6E),
        "5" => Color.FromRgb(0x53, 0xDF, 0xDD),
        "6" => Color.FromRgb(0xA8, 0x82, 0xFF),
        _ => Hex(raw),
    };

    /// <summary>A `#RGB`/`#RRGGBB` author color, or null for anything
    /// else — the hostile half of the fill domain, which is why the
    /// contrast rows sample it (D13's hex row).</summary>
    internal static Color? Hex(string? raw)
    {
        if (raw is null || !raw.StartsWith('#'))
        {
            return null;
        }
        string body = raw[1..];
        if (body.Length == 3)
        {
            body = string.Concat(body[0], body[0], body[1], body[1], body[2], body[2]);
        }
        if (body.Length != 6
            || !uint.TryParse(
                body,
                // AllowHexSpecifier ALONE (the review round): the
                // HexNumber composite admits leading and trailing
                // whitespace, so "# FF00 " parsed as a colour.
                System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture,
                out uint value))
        {
            return null;
        }
        return Color.FromRgb(
            (byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));
    }

    /// <summary>The ONE composite: tint at fraction over an opaque
    /// base, opaque result — deterministic, so contrast is measurable
    /// and stacked layers cannot double up alpha.</summary>
    internal static Color Blend(Color tint, double fraction, Color over) => Color.FromRgb(
        (byte)((tint.R * fraction) + (over.R * (1 - fraction)) + 0.5),
        (byte)((tint.G * fraction) + (over.G * (1 - fraction)) + 0.5),
        (byte)((tint.B * fraction) + (over.B * (1 - fraction)) + 0.5));

    /// <summary>A card or group fill: the author's color composited at
    /// the pinned fraction over the surface; colorless keeps the plain
    /// surface. The static Fill1..6 tokens in the dictionaries are
    /// THIS arithmetic precomputed — the census row proves they have
    /// not drifted from it.</summary>
    internal static Color Fill(string? raw, Color surface) =>
        PresetTint(raw) is { } tint
            ? Blend(tint, FillTintFraction, surface)
            : surface;
}
