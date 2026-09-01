// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Xml.Linq;

namespace SlateWindows.Tests;

/// <summary>
/// Provisional W1 APCA-W3 v0.1.9 check. W8-2 owns final token values and the
/// permanent CI matrix; these constants intentionally match APCAContrast.swift.
/// </summary>
public sealed class ThemeTokenContrastTests
{
    [Theory]
    [InlineData("Slate.Light.xaml")]
    [InlineData("Slate.Dark.xaml")]
    public void EveryTextBearingPairClearsProjectApcaFloor(string fileName)
    {
        IReadOnlyDictionary<string, Rgb> colors = ReadColors(fileName);
        var pairs = new[]
        {
            ("primary/window", "Slate.TextColor", "Slate.WindowBackgroundColor"),
            ("primary/surface", "Slate.TextColor", "Slate.SurfaceColor"),
            ("primary/raised", "Slate.TextColor", "Slate.RaisedSurfaceColor"),
            ("secondary/window", "Slate.SecondaryTextColor", "Slate.WindowBackgroundColor"),
            ("secondary/surface", "Slate.SecondaryTextColor", "Slate.SurfaceColor"),
            ("secondary/raised", "Slate.SecondaryTextColor", "Slate.RaisedSurfaceColor"),
            ("accent/window", "Slate.AccentColor", "Slate.WindowBackgroundColor"),
            // W3-1: reading text sits on Slate.SurfaceBrush, so the
            // accent pairing that was only gated against window must be
            // gated here too.
            ("accent/surface", "Slate.AccentColor", "Slate.SurfaceColor"),
            // NOT gated: accent/raised measures |Lc| 74.46 in the light
            // theme — a real near-miss against the >75 floor, found while
            // adding accent/surface. Deliberately left ungated rather
            // than silently passing: fixing it means retuning
            // Slate.AccentColor, which is shared with selection and focus
            // and whose final values W8-2 owns (the palette is marked
            // provisional). No surface renders accent text on the raised
            // background today. Tracked so it is a decision, not a gap.
            ("selection", "Slate.SelectionTextColor", "Slate.SelectionBackgroundColor"),
            ("error/surface", "Slate.ErrorColor", "Slate.SurfaceColor"),
            // W3-4 code-token palette: token text renders on the code
            // block's raised background inside the reading surface, so
            // every REAL color is gated against BOTH. (Comment aliases
            // the already-gated secondary text; identifier, operator,
            // and punctuation resolve to gated brushes in the palette
            // code and add no new colors.)
            ("code-keyword/raised", "Slate.CodeKeywordColor", "Slate.RaisedSurfaceColor"),
            ("code-keyword/surface", "Slate.CodeKeywordColor", "Slate.SurfaceColor"),
            ("code-string/raised", "Slate.CodeStringColor", "Slate.RaisedSurfaceColor"),
            ("code-string/surface", "Slate.CodeStringColor", "Slate.SurfaceColor"),
            ("code-number/raised", "Slate.CodeNumberColor", "Slate.RaisedSurfaceColor"),
            ("code-number/surface", "Slate.CodeNumberColor", "Slate.SurfaceColor"),
            ("code-type/raised", "Slate.CodeTypeColor", "Slate.RaisedSurfaceColor"),
            ("code-type/surface", "Slate.CodeTypeColor", "Slate.SurfaceColor"),
            ("code-function/raised", "Slate.CodeFunctionColor", "Slate.RaisedSurfaceColor"),
            ("code-function/surface", "Slate.CodeFunctionColor", "Slate.SurfaceColor"),
            // Unresolved wikilinks (#849). A distinct role from error on
            // purpose: a note you have not written yet is not a failure,
            // and mac draws the same distinction with warningText.
            ("warning/surface", "Slate.WarningColor", "Slate.SurfaceColor"),
            ("warning/raised", "Slate.WarningColor", "Slate.RaisedSurfaceColor"),
            // Field pass 3 caption token (adversarial round 1: the first
            // cut shipped #B0B0B0 at |Lc| 57.70 ungated). Gated against
            // raised too so a future in-card use can't regress silently.
            // Dark tertiary EQUALS secondary — no dimming headroom at
            // this floor — so the dark caption dims by size only.
            ("tertiary/surface", "Slate.TertiaryTextColor", "Slate.SurfaceColor"),
            ("tertiary/raised", "Slate.TertiaryTextColor", "Slate.RaisedSurfaceColor"),
            // Canvas (W6-1 §D D13): text on every preset fill and the
            // group fill; the selection ring's carrier against every
            // fill, the group fill and the window — mac's measured
            // pairing, gated here per appearance.
            ("canvas-text/fill1", "Slate.Canvas.TextColor", "Slate.Canvas.Fill1Color"),
            ("canvas-text/fill2", "Slate.Canvas.TextColor", "Slate.Canvas.Fill2Color"),
            ("canvas-text/fill3", "Slate.Canvas.TextColor", "Slate.Canvas.Fill3Color"),
            ("canvas-text/fill4", "Slate.Canvas.TextColor", "Slate.Canvas.Fill4Color"),
            ("canvas-text/fill5", "Slate.Canvas.TextColor", "Slate.Canvas.Fill5Color"),
            ("canvas-text/fill6", "Slate.Canvas.TextColor", "Slate.Canvas.Fill6Color"),
            ("canvas-text/groupfill", "Slate.Canvas.TextColor", "Slate.Canvas.GroupFillColor"),
            ("canvas-ring/fill1", "Slate.Canvas.SelectionRingColor", "Slate.Canvas.Fill1Color"),
            ("canvas-ring/fill2", "Slate.Canvas.SelectionRingColor", "Slate.Canvas.Fill2Color"),
            ("canvas-ring/fill3", "Slate.Canvas.SelectionRingColor", "Slate.Canvas.Fill3Color"),
            ("canvas-ring/fill4", "Slate.Canvas.SelectionRingColor", "Slate.Canvas.Fill4Color"),
            ("canvas-ring/fill5", "Slate.Canvas.SelectionRingColor", "Slate.Canvas.Fill5Color"),
            ("canvas-ring/fill6", "Slate.Canvas.SelectionRingColor", "Slate.Canvas.Fill6Color"),
            ("canvas-ring/groupfill", "Slate.Canvas.SelectionRingColor", "Slate.Canvas.GroupFillColor"),
            ("canvas-ring/window", "Slate.Canvas.SelectionRingColor", "Slate.WindowBackgroundColor"),
        };

        foreach ((string name, string textKey, string backgroundKey) in pairs)
        {
            double contrast = Math.Abs(ApcaLc(colors[textKey], colors[backgroundKey]));
            Assert.True(
                contrast > 75,
                $"{fileName} {name} measured |Lc| {contrast:F2}; expected > 75.");
        }
    }

    /// <summary>D13's hostile hex row: a raw author color takes the
    /// SAME composite the preset tokens precompute
    /// (<see cref="SlateWindows.Canvas.CanvasPalette"/>), and the text
    /// and ring tokens clear the floor on it in both appearances — the
    /// path a preset-only matrix never exercises.</summary>
    [Theory]
    [InlineData("Slate.Light.xaml")]
    [InlineData("Slate.Dark.xaml")]
    public void AHostileHexFillKeepsTextAndRingAboveTheFloor(string fileName)
    {
        IReadOnlyDictionary<string, Rgb> colors = ReadColors(fileName);
        Rgb surface = colors["Slate.SurfaceColor"];
        System.Windows.Media.Color fill = SlateWindows.Canvas.CanvasPalette.Fill(
            "#FF00FF",
            System.Windows.Media.Color.FromRgb(
                (byte)Math.Round(surface.Red * 255),
                (byte)Math.Round(surface.Green * 255),
                (byte)Math.Round(surface.Blue * 255)));
        Rgb composited = Rgb.Parse($"#FF{fill.R:X2}{fill.G:X2}{fill.B:X2}");
        foreach (string key in new[]
            { "Slate.Canvas.TextColor", "Slate.Canvas.SelectionRingColor" })
        {
            double contrast = Math.Abs(ApcaLc(colors[key], composited));
            Assert.True(
                contrast > 75,
                $"{fileName} {key} on the hex composite measured |Lc| "
                + $"{contrast:F2}; expected > 75.");
        }
    }


    /// <summary>§D TD-7's token-drift census (the gap TD-6's record
    /// kept owed): the six precomputed Fill tokens in each appearance
    /// ARE CanvasPalette's arithmetic — the preset tint at the pinned
    /// fraction over that appearance's surface — so a hand-edited
    /// token or a retuned fraction cannot drift apart silently while
    /// the contrast floor still happens to pass.</summary>
    [Theory]
    [InlineData("Slate.Light.xaml")]
    [InlineData("Slate.Dark.xaml")]
    public void ThePrecomputedFillTokensMatchThePaletteArithmetic(string fileName)
    {
        IReadOnlyDictionary<string, Rgb> colors = ReadColors(fileName);
        Rgb surface = colors["Slate.SurfaceColor"];
        var surfaceColor = System.Windows.Media.Color.FromRgb(
            (byte)Math.Round(surface.Red * 255),
            (byte)Math.Round(surface.Green * 255),
            (byte)Math.Round(surface.Blue * 255));
        for (var preset = 1; preset <= 6; preset++)
        {
            System.Windows.Media.Color expected =
                SlateWindows.Canvas.CanvasPalette.Fill(
                    preset.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    surfaceColor);
            Rgb token = colors[$"Slate.Canvas.Fill{preset}Color"];
            var actual = System.Windows.Media.Color.FromRgb(
                (byte)Math.Round(token.Red * 255),
                (byte)Math.Round(token.Green * 255),
                (byte)Math.Round(token.Blue * 255));
            Assert.True(
                expected == actual,
                $"{fileName} Fill{preset} is {actual} but the arithmetic "
                + $"says {expected}: the token drifted from the one "
                + "composite, and the contrast floor alone cannot see it.");
        }
    }

    private static IReadOnlyDictionary<string, Rgb> ReadColors(string fileName)
    {
        string filePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "SlateWindows", "Themes", fileName));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return XDocument.Load(filePath)
            .Root!
            .Elements(presentation + "Color")
            .ToDictionary(
                element => element.Attribute(x + "Key")!.Value,
                element => Rgb.Parse(element.Value));
    }

    private static double ApcaLc(Rgb text, Rgb background)
    {
        const double blackThreshold = 0.022;
        const double blackClamp = 1.414;
        const double minimumDelta = 0.0005;
        const double lowClip = 0.1;
        const double lowOffset = 0.027;
        const double scale = 1.14;

        static double SoftClamp(double value) => value > blackThreshold
            ? value
            : value + Math.Pow(blackThreshold - value, blackClamp);

        double textY = SoftClamp(text.ScreenLuminance);
        double backgroundY = SoftClamp(background.ScreenLuminance);
        if (Math.Abs(backgroundY - textY) < minimumDelta)
        {
            return 0;
        }

        double sapc;
        double output;
        if (backgroundY > textY)
        {
            sapc = (Math.Pow(backgroundY, 0.56) - Math.Pow(textY, 0.57)) * scale;
            output = sapc < lowClip ? 0 : sapc - lowOffset;
        }
        else
        {
            sapc = (Math.Pow(backgroundY, 0.65) - Math.Pow(textY, 0.62)) * scale;
            output = sapc > -lowClip ? 0 : sapc + lowOffset;
        }

        return output * 100;
    }

    private readonly record struct Rgb(double Red, double Green, double Blue)
    {
        public double ScreenLuminance =>
            (0.2126729 * Math.Pow(Red, 2.4))
            + (0.7151522 * Math.Pow(Green, 2.4))
            + (0.0721750 * Math.Pow(Blue, 2.4));

        public static Rgb Parse(string value)
        {
            string hex = value.Trim().TrimStart('#');
            if (hex.Length == 8)
            {
                hex = hex[2..];
            }

            if (hex.Length != 6)
            {
                throw new FormatException($"Expected RRGGBB or AARRGGBB, got {value}.");
            }

            return new Rgb(
                Byte(hex[0..2]),
                Byte(hex[2..4]),
                Byte(hex[4..6]));
        }

        private static double Byte(string hex) =>
            int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
    }
}
