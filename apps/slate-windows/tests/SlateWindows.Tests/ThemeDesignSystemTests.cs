// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// #1755: Slate.Light.xaml and Slate.Dark.xaml come from the design system
// (design/windows/xaml). These facts render through the REAL dictionaries in
// ThemeManager's merge order, because the generated files build almost every
// brush with DynamicResource: a brush that doesn't resolve paints nothing, and
// no XML check can see that.

using System.Collections.ObjectModel;
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

    /// <summary>The reading view is a RichTextBox, which always draws its
    /// selection as an overlay on top of the text, so the text box's solid
    /// pair would paint over the words. With no style of its own it would stay
    /// system blue. The opacity lives in the brush, so High Contrast keeps its
    /// own. The pixels are ReadingView_ASelectionStandsOutAndKeepsItsText.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TheReadingViewSelectsInTheThemesOverlay(bool dark, bool highContrast)
    {
        (Color? brush, double brushOpacity, Color? want, double wantOpacity, double opacity) =
            InRealWindow(dark, highContrast, window =>
            {
                var surface = new SlateWindows.Reading.ReadingSurface();
                window.Content = surface;
                window.UpdateLayout();
                Pump();
                var selection = surface.SelectionBrush as SolidColorBrush;
                var theme = window.FindResource(TextBoxSelection.ReadingBrushKey) as SolidColorBrush;
                return (selection?.Color, selection?.Opacity ?? 0, theme?.Color, theme?.Opacity ?? 0, surface.SelectionOpacity);
            });

        Assert.NotNull(brush);
        Assert.Equal(want, brush);
        Assert.Equal(wantOpacity, brushOpacity);
        Assert.Equal(1, opacity);
    }

    /// <summary>#1758: a row with its own ItemContainerStyle loses Fluent's
    /// implicit style and paints from the system colours, which in the dark
    /// theme meant black text on a dark panel and a Windows blue selection.
    /// About 25 lists and trees in the shell have one.</summary>
    [Theory]
    [InlineData(false, "Slate.Light.xaml")]
    [InlineData(true, "Slate.Dark.xaml")]
    public void ALocallyStyledRowDrawsTheThemesColors(bool dark, string fileName)
    {
        IReadOnlyDictionary<string, string> colors = ColorsIn(fileName);

        // The selected tree row's state depends on the window having focus, which
        // a session 0 runner never gives it, so that row is checked through the
        // keys its classic template reads.
        (string? listText, string? selection, string? selectionText, string? inactive, string? inactiveText) =
            InRealWindow(dark, highContrast: false, window =>
            {
                var list = new ListBox
                {
                    ItemContainerStyle = new Style(typeof(ListBoxItem)),
                    ItemTemplate = TextRowTemplate(),
                    ItemsSource = new[] { "A row" },
                };
                window.Content = list;
                window.UpdateLayout();
                Pump();
                TextBlock row = ShownShell.Descendants(list).OfType<TextBlock>().Single();
                string? Key(object key) => (window.FindResource(key) as SolidColorBrush)?.Color.ToString();
                return (
                    (row.Foreground as SolidColorBrush)?.Color.ToString(),
                    Key(SystemColors.HighlightBrushKey),
                    Key(SystemColors.HighlightTextBrushKey),
                    Key(SystemColors.InactiveSelectionHighlightBrushKey),
                    Key(SystemColors.InactiveSelectionHighlightTextBrushKey));
            });

        Assert.Equal(colors["Slate.TextColor"], listText);
        Assert.Equal(colors["Slate.SelectionBackgroundColor"], selection);
        Assert.Equal(colors["Slate.SelectionTextColor"], selectionText);
        Assert.Equal(colors["Slate.SelectedSurfaceColor"], inactive);
        Assert.Equal(colors["Slate.TextColor"], inactiveText);
    }

    /// <summary>High Contrast keeps the real system colours: there they are
    /// the theme, and overriding them would take the user's own palette away.</summary>
    [Fact]
    public void HighContrastLeavesTheSystemColorsAlone()
    {
        (Color? text, Color? highlight) = InRealWindow(dark: false, highContrast: true, window => (
            (window.FindResource(SystemColors.ControlTextBrushKey) as SolidColorBrush)?.Color,
            (window.FindResource(SystemColors.HighlightBrushKey) as SolidColorBrush)?.Color));

        Assert.Equal(SystemColors.ControlTextColor, text);
        Assert.Equal(SystemColors.HighlightColor, highlight);
    }

    /// <summary>The menu bar has a local style for its modal block, so it paints
    /// WPF's classic fixed light bar unless it names its own background. The
    /// dark theme's light text on that bar was close to unreadable.</summary>
    [Theory]
    [InlineData(false, "Slate.Light.xaml")]
    [InlineData(true, "Slate.Dark.xaml")]
    public void TheMenuBarDrawsTheThemesTextOnTheThemesWindow(bool dark, string fileName)
    {
        IReadOnlyDictionary<string, string> colors = ColorsIn(fileName);
        XElement menu = XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"))
            .Descendants()
            .Single(element => element.Name.LocalName == "Menu"
                && (string?)element.Attribute(Xaml + "Name") == "MainMenu");

        string? foreground = InRealWindow(dark, highContrast: false, window =>
            (window.FindResource("MenuBarForeground") as SolidColorBrush)?.Color.ToString());

        Assert.Equal("{DynamicResource Slate.WindowBackgroundBrush}", (string?)menu.Attribute("Background"));
        Assert.Equal(colors["Slate.TextColor"], foreground);
    }

    /// <summary>The tab's local style brings its own template, because WPF's
    /// classic one hard-codes a white selected tab. Its focus visual has to
    /// resolve too, or the tab would take the keys with no ring.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TheOpenTabDrawsTheThemesColorsAndAFocusRing(bool dark, bool highContrast) => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        ResourceDictionary[] layers = ThemeManager.DictionariesFor(dark ? SlateTheme.Dark : SlateTheme.Light, highContrast);
        Collection<ResourceDictionary> merged = host.Shell.Resources.MergedDictionaries;
        for (int index = ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false).Length - 1; index >= 0; index--)
        {
            merged.RemoveAt(index);
        }

        for (int index = 0; index < layers.Length; index++)
        {
            merged.Insert(index, layers[index]);
        }

        PumpedDispatcher.PumpUntilDrained(host.Workspace.SeedWorkForTests);
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();

        TabItem tab = ShownShell.Descendants(host.Shell).OfType<TabItem>().Single();
        Border chrome = ShownShell.Descendants(tab).OfType<Border>().First();
        Color Theme(string key) => Assert.IsType<SolidColorBrush>(host.Shell.FindResource(key)).Color;

        Assert.True(tab.IsSelected);
        Assert.Equal(Theme("Slate.SurfaceBrush"), Assert.IsType<SolidColorBrush>(chrome.Background).Color);
        Assert.Equal(Theme("Slate.AccentBrush"), Assert.IsType<SolidColorBrush>(chrome.BorderBrush).Color);
        Assert.Equal(Theme("Slate.TextBrush"), Assert.IsType<SolidColorBrush>(tab.Foreground).Color);
        Assert.NotNull(tab.FocusVisualStyle);
        return 0;
    }, TimeSpan.FromSeconds(60), "the tab test timed out.");

    /// <summary>AvalonEdit's default link colour is pure blue, |Lc| 15.6 on the
    /// dark editor.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void AnEditorLinkDrawsTheThemesLinkColor(bool dark, bool highContrast)
    {
        (Color? link, Color? want) = InRealWindow(dark, highContrast, window =>
        {
            var editor = new SlateTextEditor();
            window.Content = editor;
            window.UpdateLayout();
            Pump();
            return (
                (editor.TextArea.TextView.LinkTextForegroundBrush as SolidColorBrush)?.Color,
                (window.FindResource(EditorSyntaxPalette.WikilinkBrushKey) as SolidColorBrush)?.Color);
        });

        Assert.NotNull(link);
        Assert.Equal(want, link);
    }

    private static DataTemplate TextRowTemplate()
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding());
        return new DataTemplate { VisualTree = text };
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
