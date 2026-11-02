// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// #1755: Slate.Light.xaml and Slate.Dark.xaml come from the design system
// (design/windows/xaml). These facts render through the REAL dictionaries in
// ThemeManager's merge order, because the generated files build almost every
// brush with DynamicResource: a brush that doesn't resolve paints nothing, and
// no XML check can see that.

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace SlateWindows.Tests;

public sealed class ThemeDesignSystemTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>The roles in design/windows/xaml/README.md. The dark values
    /// are the owner's call of 2026-11-02: logo gold measures |Lc| 59-62 as
    /// text on the dark surfaces, under the project's floor.</summary>
    [Theory]
    [InlineData("Slate.Light.xaml", "#FF7D5A00", "#FF6B4D00", "#FF563D04", "#FF6B4D00", "#FFA57A00", "#FFFFFFFF")]
    [InlineData("Slate.Dark.xaml", "#FFEECF7E", "#FFF0D590", "#FFEDCB75", "#FFEECF7E", "#FFEECF7E", "#FF171717")]
    public void TheAccentRolesAreSlatesGold(
        string fileName,
        string accent,
        string hover,
        string pressed,
        string text,
        string indicator,
        string textOnAccent)
    {
        IReadOnlyDictionary<string, string> colors = ColorsIn(fileName);

        Assert.Equal(accent, colors["Slate.AccentColor"]);
        Assert.Equal(hover, colors["Slate.AccentHoverColor"]);
        Assert.Equal(pressed, colors["Slate.AccentPressedColor"]);
        Assert.Equal(text, colors["Slate.AccentTextColor"]);
        Assert.Equal(indicator, colors["Slate.AccentIndicatorColor"]);
        Assert.Equal(textOnAccent, colors["Slate.TextOnAccentColor"]);
    }

    [Theory]
    [InlineData(false, "Slate.Light.xaml")]
    [InlineData(true, "Slate.Dark.xaml")]
    public void EveryBrushDrawsTheColorItNames(bool dark, string fileName)
    {
        IReadOnlyDictionary<string, string> colors = ColorsIn(fileName);
        (string Key, string ColorKey)[] brushes = BrushesNamingAColor(fileName);
        // A theme file that stops building brushes this way would pass with
        // nothing checked. The generated files have hundreds.
        Assert.True(brushes.Length > 300, $"{fileName} names a colour in only {brushes.Length} brushes.");

        string[] wrong = InRealWindow(dark, highContrast: false, window =>
            brushes
                .Where(brush => colors.ContainsKey(brush.ColorKey))
                .Where(brush =>
                    window.FindResource(brush.Key) is not SolidColorBrush found
                    || found.Color.ToString() != colors[brush.ColorKey])
                .Select(brush => $"{brush.Key} -> {brush.ColorKey}")
                .ToArray());

        Assert.True(wrong.Length == 0, $"{fileName}: {string.Join(", ", wrong)}");
    }

    [Theory]
    [InlineData("Slate.Light.xaml")]
    [InlineData("Slate.Dark.xaml")]
    public void EveryColorABrushNamesIsDefined(string fileName)
    {
        IReadOnlyDictionary<string, string> colors = ColorsIn(fileName);
        string[] dangling = [.. BrushesNamingAColor(fileName)
            .Where(brush => !colors.ContainsKey(brush.ColorKey))
            .Select(brush => $"{brush.Key} -> {brush.ColorKey}")];

        Assert.True(dangling.Length == 0, $"{fileName}: {string.Join(", ", dangling)}");
    }

    /// <summary>Fluent's own controls pick up the gold, which is the point of
    /// overriding its brush keys: redefining a colour key alone changes
    /// nothing, because Fluent builds its brushes when its dictionary loads.</summary>
    [Theory]
    [InlineData(false, "Slate.Light.xaml")]
    [InlineData(true, "Slate.Dark.xaml")]
    public void FluentControlsDrawTheGold(bool dark, string fileName)
    {
        IReadOnlyDictionary<string, string> colors = ColorsIn(fileName);

        (string? button, string? label, string? progress) = InRealWindow(dark, highContrast: false, window =>
        {
            var accentButton = new Button { Style = (Style)window.FindResource("AccentButtonStyle") };
            var bar = new ProgressBar { Value = 50 };
            window.Content = new StackPanel { Children = { accentButton, bar } };
            window.UpdateLayout();
            Pump();
            return (
                (accentButton.Background as SolidColorBrush)?.Color.ToString(),
                (accentButton.Foreground as SolidColorBrush)?.Color.ToString(),
                (bar.Foreground as SolidColorBrush)?.Color.ToString());
        });

        Assert.Equal(colors["Slate.AccentColor"], button);
        Assert.Equal(colors["Slate.TextOnAccentColor"], label);
        Assert.Equal(colors["Slate.AccentColor"], progress);
    }

    /// <summary>The solid pair, in every appearance. WPF's defaults are a 40%
    /// overlay, which leaves the pale gold almost invisible on white, and a
    /// white selection text brush.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ATextBoxSelectsInTheThemesSolidPair(bool dark, bool highContrast)
    {
        TextBoxSelection.Install();
        Assert.True(
            AppContext.TryGetSwitch(TextBoxSelection.AdornerSwitch, out bool useAdorner) && !useAdorner,
            "the adorner switch is not off.");

        (Color? background, Color? text, Color? wantBackground, Color? wantText, double opacity) =
            InRealWindow(dark, highContrast, window =>
            {
                var box = new TextBox { Text = "selected" };
                window.Content = box;
                window.UpdateLayout();
                Pump();
                return (
                    (box.SelectionBrush as SolidColorBrush)?.Color,
                    (box.SelectionTextBrush as SolidColorBrush)?.Color,
                    (window.FindResource(TextBoxSelection.BackgroundBrushKey) as SolidColorBrush)?.Color,
                    (window.FindResource(TextBoxSelection.TextBrushKey) as SolidColorBrush)?.Color,
                    box.SelectionOpacity);
            });

        Assert.NotNull(background);
        Assert.Equal(wantBackground, background);
        Assert.Equal(wantText, text);
        Assert.Equal(1, opacity);
    }

    /// <summary>The handler only reaches the app if startup installs it, and
    /// before any window exists: WPF reads the switch once.</summary>
    [Fact]
    public void StartupInstallsTextBoxSelectionBeforeAnyWindow()
    {
        string startup = File.ReadAllText(Path.Combine(SourceText.ShellSourceRoot(), "App.xaml.cs"));
        int install = startup.IndexOf("TextBoxSelection.Install();", StringComparison.Ordinal);

        Assert.True(install >= 0, "App.xaml.cs doesn't install TextBoxSelection.");
        Assert.True(
            install < startup.IndexOf("new ThemeManager(", StringComparison.Ordinal),
            "TextBoxSelection installs after the theme and the first windows.");
    }

    /// <summary>The type ramp's two styles Fluent doesn't ship. They live
    /// outside the generated files, so regenerating can't drop them, and High
    /// Contrast gets them too.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TheBodyLargeStylesAreTheRampsEighteenOnTwentyFour(bool dark, bool highContrast)
    {
        (TextFacts regular, TextFacts strong) = InRealWindow(dark, highContrast, window =>
        {
            var plain = new TextBlock { Style = (Style)window.FindResource("Slate.BodyLargeTextBlockStyle") };
            var bold = new TextBlock { Style = (Style)window.FindResource("Slate.BodyLargeStrongTextBlockStyle") };
            window.Content = new StackPanel { Children = { plain, bold } };
            window.UpdateLayout();
            return (TextFacts.Of(plain), TextFacts.Of(bold));
        });

        foreach (TextFacts text in new[] { regular, strong })
        {
            Assert.Equal(18, text.FontSize);
            Assert.Equal(24, text.LineHeight);
            Assert.Equal(LineStackingStrategy.BlockLineHeight, text.Stacking);
            Assert.StartsWith("Segoe UI Variable Text", text.Family, StringComparison.Ordinal);
        }

        Assert.Equal(FontWeights.Normal, regular.Weight);
        Assert.Equal(FontWeights.SemiBold, strong.Weight);
    }

    private readonly record struct TextFacts(
        double FontSize,
        double LineHeight,
        LineStackingStrategy Stacking,
        FontWeight Weight,
        string Family)
    {
        public static TextFacts Of(TextBlock text) =>
            new(text.FontSize, text.LineHeight, text.LineStackingStrategy, text.FontWeight, text.FontFamily.Source);
    }

    private static (string Key, string ColorKey)[] BrushesNamingAColor(string fileName) =>
        [.. Document(fileName)
            .Root!
            .Elements(Presentation + "SolidColorBrush")
            .Select(brush => (
                Key: brush.Attribute(Xaml + "Key")!.Value,
                Reference: Regex.Match(
                    (string?)brush.Attribute("Color") ?? "",
                    @"^\{(?:Dynamic|Static)Resource\s+([^}\s]+)\s*\}$")))
            .Where(brush => brush.Reference.Success)
            .Select(brush => (brush.Key, brush.Reference.Groups[1].Value))];

    private static IReadOnlyDictionary<string, string> ColorsIn(string fileName) =>
        Document(fileName)
            .Root!
            .Elements(Presentation + "Color")
            .ToDictionary(
                color => color.Attribute(Xaml + "Key")!.Value,
                color => color.Value.Trim().ToUpperInvariant());

    private static XDocument Document(string fileName) =>
        XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), "Themes", fileName));

    private static T InRealWindow<T>(bool dark, bool highContrast, Func<Window, T> body) =>
        StaThread.Run(
            () =>
            {
                // Registers the pack: scheme without constructing an
                // Application (see TextBoxAccessibilityTests).
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
                    typeof(Application).TypeHandle);
                var window = new Window
                {
                    Width = 400,
                    Height = 300,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Left = -10_000,
                    Top = -10_000,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                };
                foreach (ResourceDictionary layer in ThemeManager.DictionariesFor(dark ? SlateTheme.Dark : SlateTheme.Light, highContrast))
                {
                    window.Resources.MergedDictionaries.Add(layer);
                }

                try
                {
                    window.Show();
                    Pump();
                    return body(window);
                }
                finally
                {
                    window.Close();
                    Pump();
                }
            },
            TimeSpan.FromSeconds(60),
            "the theme window timed out.");

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
