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
            ("selection", "Slate.SelectionTextColor", "Slate.SelectionBackgroundColor"),
            ("error/surface", "Slate.ErrorColor", "Slate.SurfaceColor"),
        };

        foreach ((string name, string textKey, string backgroundKey) in pairs)
        {
            double contrast = Math.Abs(ApcaLc(colors[textKey], colors[backgroundKey]));
            Assert.True(
                contrast > 75,
                $"{fileName} {name} measured |Lc| {contrast:F2}; expected > 75.");
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
