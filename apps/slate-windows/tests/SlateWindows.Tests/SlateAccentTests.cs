// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>
/// DS-11 (#1812): the accent can follow Windows, as Windows gives it (DSD16). Colours go in
/// through the palette: a fact can't change the Windows accent, because that's a system
/// setting.
/// </summary>
public sealed class SlateAccentTests
{
    private const int WmDwmColorizationColorChanged = 0x0320;

    /// <summary>Every text the accent can draw on. A link, hovered and pressed, draws in the
    /// fill roles, and tertiary text on a selected row is for DS-18's counts.</summary>
    private static readonly (string Text, string Background)[] AccentTextPairs =
    {
        ("Slate.TextOnAccentColor", "Slate.AccentColor"),
        ("Slate.TextOnAccentColor", "Slate.AccentHoverColor"),
        ("Slate.TextOnAccentColor", "Slate.AccentPressedColor"),
        ("Slate.AccentTextColor", "Slate.SurfaceColor"),
        ("Slate.AccentTextColor", "Slate.WindowBackgroundColor"),
        ("Slate.AccentTextColor", "Slate.RaisedSurfaceColor"),
        ("Slate.AccentTextColor", "Slate.SelectedSurfaceColor"),
        ("Slate.AccentHoverColor", "Slate.SurfaceColor"),
        ("Slate.AccentPressedColor", "Slate.SurfaceColor"),
        ("Slate.SelectionTextColor", "Slate.SelectionBackgroundColor"),
        ("Slate.TextColor", "Slate.SelectedSurfaceColor"),
        ("Slate.SecondaryTextColor", "Slate.SelectedSurfaceColor"),
        ("Slate.TertiaryTextColor", "Slate.SelectedSurfaceColor"),
    };

    private static readonly (string Mark, string Background)[] AccentMarkPairs =
    {
        ("Slate.AccentColor", "Slate.SurfaceColor"),
        ("Slate.AccentIndicatorColor", "Slate.SurfaceColor"),
        ("Slate.AccentIndicatorColor", "Slate.SelectedSurfaceColor"),
    };

    /// <summary>What <c>SystemColors</c> read on a default Windows 11 on 2026-11-03.</summary>
    private static readonly AccentPalette DefaultBlue = new(
        Hex("#0078D4"),
        Hex("#0091F8"),
        Hex("#4CC2FF"),
        Hex("#99EBFF"),
        Hex("#0067C0"),
        Hex("#003E92"),
        Hex("#001A68"));

    /// <summary>A live change's way in, through WPF's own code. A fact can't change the accent,
    /// so this puts a colour in the cache WPF keeps and sends this thread's windows the message
    /// Windows would. All of that is private to WPF, so the premises say if it moves.</summary>
    [Fact]
    public void WpfClearingItsColoursReachesTheWatch() => RunSta(() =>
    {
        using var host = new ShownShell();
        Color windows = SystemColors.AccentColor;
        PlantAccent(windows == Colors.Red ? Colors.Lime : Colors.Red);
        try
        {
            int changes = 0;
            SystemAccentWatch.Attach(host.Shell, () => changes++);
            Assert.True(changes == 1, "premise: the watch didn't take its first read.");
            changes = 0;

            SendToThisThreadsWindows(WmDwmColorizationColorChanged);

            Assert.True(PumpUntil(() => changes > 0, TimeSpan.FromSeconds(10)), "the watch never heard WPF clear its colours.");
            Assert.Equal(windows, SystemColors.AccentColor);
            Assert.Equal(1, changes);

            SendToThisThreadsWindows(WmDwmColorizationColorChanged);
            _ = PumpUntil(() => false, TimeSpan.FromMilliseconds(200));
            Assert.Equal(1, changes);
        }
        finally
        {
            PlantAccent(windows);
        }
    });

    /// <summary>The watch needs a window that lives as long as the app.</summary>
    [Fact]
    public void StartupWatchesTheAccentOnTheMainWindow()
    {
        string startup = File.ReadAllText(Path.Combine(SourceText.ShellSourceRoot(), "App.xaml.cs"));

        Assert.Contains("_themeManager?.WatchSystemAccent(_mainWindow);", startup, StringComparison.Ordinal);
    }

    private static void PlantAccent(Color color)
    {
        _ = SystemColors.AccentColor;
        var cache = (Color[]?)typeof(SystemColors)
            .GetField("_colorCache", BindingFlags.NonPublic | BindingFlags.Static)?
            .GetValue(null);
        Type? slots = typeof(SystemColors).GetNestedType("CacheSlot", BindingFlags.NonPublic);
        Assert.True(cache is not null && slots is not null, "premise: WPF's colour cache has moved.");
        cache![(int)Enum.Parse(slots!, "AccentColor")] = color;
        Assert.Equal(color, SystemColors.AccentColor);
    }

    private static void SendToThisThreadsWindows(int message) =>
        _ = EnumThreadWindows(
            GetCurrentThreadId(),
            (window, _) =>
            {
                _ = SendMessage(window, message, IntPtr.Zero, IntPtr.Zero);
                return true;
            },
            IntPtr.Zero);

    private delegate bool EnumThreadWindowsProc(IntPtr window, IntPtr state);

    [DllImport("user32.dll")]
    private static extern bool EnumThreadWindows(uint thread, EnumThreadWindowsProc callback, IntPtr state);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    /// <summary>WPF walks the window at Normal, so a Background turn comes after it.</summary>
    private static bool PumpUntil(Func<bool> condition, TimeSpan budget)
    {
        var clock = Stopwatch.StartNew();
        while (!condition() && clock.Elapsed < budget)
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
            Thread.Sleep(5);
        }

        return condition();
    }

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(120), "STA test body timed out.");

    /// <summary>A choice, close enough to the default to pass.</summary>
    private static readonly AccentPalette Chosen = DefaultBlue with { Accent = Hex("#0070C8") };

    /// <summary>What <c>SystemColors</c> read on the owner's desktop on 2026-11-05, during
    /// their look at real accents. DSD14's bar sent it back to gold in dark, which led to
    /// DSD16.</summary>
    private static readonly AccentPalette OwnersPink = new(
        Hex("#EA005E"),
        Hex("#FF0A6F"),
        Hex("#FF529F"),
        Hex("#FF95CA"),
        Hex("#D30050"),
        Hex("#9E002F"),
        Hex("#6D0011"));

    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static SlateTheme ThemeOf(bool dark) => dark ? SlateTheme.Dark : SlateTheme.Light;

    /// <summary>A pack URI needs WPF's application scheme, which this registers without making
    /// an application.</summary>
    private static void RegisterPackScheme() =>
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

    private static ResourceDictionary GoldThemeFile(bool dark)
    {
        RegisterPackScheme();
        return new()
        {
            Source = new Uri($"pack://application:,,,/SlateWindows;component/Themes/Slate.{(dark ? "Dark" : "Light")}.xaml", UriKind.Absolute),
        };
    }

    private static void AssertStillGold(ResourceDictionary slate, bool dark)
    {
        ResourceDictionary gold = GoldThemeFile(dark);
        foreach (string key in SlateAccent.RolesFor(DefaultBlue, Colors.White, 255, dark).Keys)
        {
            Assert.Equal((Color)gold[key], (Color)slate[key]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheDefaultBlueIsGoldUnderAutomatic(bool dark) => StaThread.Run(() =>
    {
        ResourceDictionary slate = GoldThemeFile(dark);

        Assert.False(SlateAccent.TryApplyWindowsAccent(slate, AccentSource.Automatic, DefaultBlue, dark, highContrast: false));

        AssertStillGold(slate, dark);
        return 0;
    }, TimeSpan.FromSeconds(30), "the theme file never loaded.");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AChosenAccentIsFollowedUnderAutomatic(bool dark) => StaThread.Run(() =>
    {
        ResourceDictionary slate = GoldThemeFile(dark);

        Assert.True(SlateAccent.TryApplyWindowsAccent(slate, AccentSource.Automatic, Chosen, dark, highContrast: false));

        Assert.Equal(Chosen.Accent, (Color)slate["Slate.SelectionBackgroundColor"]);
        Assert.Equal(dark ? Chosen.Light2 : Chosen.Dark1, (Color)slate["Slate.AccentColor"]);
        return 0;
    }, TimeSpan.FromSeconds(30), "the theme file never loaded.");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SlateGoldIgnoresWindows(bool dark) => StaThread.Run(() =>
    {
        ResourceDictionary slate = GoldThemeFile(dark);

        Assert.False(SlateAccent.TryApplyWindowsAccent(slate, AccentSource.SlateGold, Chosen, dark, highContrast: false));

        AssertStillGold(slate, dark);
        return 0;
    }, TimeSpan.FromSeconds(30), "the theme file never loaded.");

    /// <summary>Windows accent means it, even for the default that Automatic treats as no
    /// choice at all.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WindowsAccentFollowsWhenItPasses(bool dark) => StaThread.Run(() =>
    {
        ResourceDictionary slate = GoldThemeFile(dark);

        Assert.True(SlateAccent.TryApplyWindowsAccent(slate, AccentSource.WindowsAccent, DefaultBlue, dark, highContrast: false));

        Assert.Equal(DefaultBlue.Accent, (Color)slate["Slate.SelectionBackgroundColor"]);
        return 0;
    }, TimeSpan.FromSeconds(30), "the theme file never loaded.");

    /// <summary>Only the app asks for an accent, so a hosted fact draws gold whatever the
    /// desktop's accent is, even one that would pass.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AHostedDictionaryIsGoldUnlessItAsks(bool dark) => StaThread.Run(() =>
    {
        RegisterPackScheme();
        ResourceDictionary slate = ThemeManager.DictionariesFor(ThemeOf(dark), highContrast: false, palette: Chosen)
            .Single(layer => layer.Contains("Slate.AccentColor"));

        AssertStillGold(slate, dark);
        return 0;
    }, TimeSpan.FromSeconds(30), "the theme file never loaded.");

    /// <summary>DSD16: Windows' accent is followed whatever its contrast. In dark this palette
    /// draws a pressed link at 4.39:1, and its reading overlay doesn't stand off the surface.
    /// Choosing a colour that clears contrast is #1842.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnAccentBelowTheBarIsStillFollowed(bool dark) => StaThread.Run(() =>
    {
        ResourceDictionary slate = GoldThemeFile(dark);
        byte goldAlpha = ((Color)slate["Slate.ReadingSelectionColor"]).A;

        Assert.True(SlateAccent.TryApplyWindowsAccent(slate, AccentSource.WindowsAccent, OwnersPink, dark, highContrast: false));

        Assert.Equal(OwnersPink.Accent, (Color)slate["Slate.SelectionBackgroundColor"]);
        Assert.Equal(dark ? OwnersPink.Light1 : OwnersPink.Dark3, (Color)slate["Slate.AccentPressedColor"]);
        Assert.Equal(Color.FromArgb(goldAlpha, 0xEA, 0x00, 0x5E), (Color)slate["Slate.ReadingSelectionColor"]);
        return 0;
    }, TimeSpan.FromSeconds(30), "the theme file never loaded.");

    /// <summary>The user's own palette is the theme there. The dictionary has no accent roles.</summary>
    [Fact]
    public void HighContrastIsNeverTheAccent() =>
        Assert.False(SlateAccent.TryApplyWindowsAccent(new ResourceDictionary(), AccentSource.WindowsAccent, Chosen, dark: false, highContrast: true));

    /// <summary>The spec's table: the palette read on this Windows, every pair, the overlay
    /// included. Nothing gates on it since DSD16, so it's the record of what the commonest
    /// accent draws, and it fails if the role table moves.</summary>
    [Theory]
    [InlineData(false, 4.53, new[] { 15.28, 9.69, 6.35, 8.51 }, new[] { 5.67, 9.98, 15.58 }, new[] { 9.98, 9.98, 15.58 }, new[] { 5.67, 5.67, 4.84 }, new[] { 11.23, 9.92, 1.31, 1.29 })]
    [InlineData(true, 4.53, new[] { 13.80, 10.15, 10.15, 11.23 }, new[] { 10.47, 15.68, 6.41 }, new[] { 12.45, 12.45, 5.09 }, new[] { 8.31, 8.31, 7.50 }, new[] { 8.63, 7.68, 1.29, 1.27 })]
    public void TheDefaultBlueMeasuresWhatTheSpecRecords(
        bool dark,
        double selection,
        double[] selectedRow,
        double[] onFills,
        double[] links,
        double[] marks,
        double[] overlay) => StaThread.Run(() =>
    {
        ResourceDictionary slate = GoldThemeFile(dark);
        IReadOnlyDictionary<string, Color> roles = SlateAccent.RolesFor(
            DefaultBlue, (Color)slate["Slate.SurfaceColor"], ((Color)slate["Slate.ReadingSelectionColor"]).A, dark);
        Color Of(string key) => roles.TryGetValue(key, out Color role) ? role : (Color)slate[key];
        double Ratio(string first, string second) => SlateContrast.WcagRatio(Of($"Slate.{first}Color"), Of($"Slate.{second}Color"));

        Assert.Equal(selection, Ratio("SelectionText", "SelectionBackground"), 2);
        Assert.Equal(selectedRow, new[] { "Text", "SecondaryText", "TertiaryText", "AccentText" }.Select(text => Math.Round(Ratio(text, "SelectedSurface"), 2)));
        Assert.Equal(onFills, new[] { "Accent", "AccentHover", "AccentPressed" }.Select(fill => Math.Round(Ratio("TextOnAccent", fill), 2)));
        Assert.Equal(links, new[] { "AccentText", "AccentHover", "AccentPressed" }.Select(link => Math.Round(Ratio(link, "Surface"), 2)));
        Assert.Equal(marks, new[] { Ratio("Accent", "Surface"), Ratio("AccentIndicator", "Surface"), Ratio("AccentIndicator", "SelectedSurface") }.Select(ratio => Math.Round(ratio, 2)));

        Color tint = Of("Slate.ReadingSelectionColor");
        double[] measured = new[] { "Surface", "RaisedSurface" }
            .Select(key => SlateContrast.WcagRatio(SlateContrast.Over(tint, Of("Slate.TextColor")), SlateContrast.Over(tint, Of($"Slate.{key}Color"))))
            .Concat(new[] { "Surface", "RaisedSurface" }
                .Select(key => SlateContrast.WcagRatio(SlateContrast.Over(tint, Of($"Slate.{key}Color")), Of($"Slate.{key}Color"))))
            .Select(ratio => Math.Round(ratio, 2))
            .ToArray();
        Assert.Equal(overlay, measured);
        return 0;
    }, TimeSpan.FromSeconds(30), "the theme file never loaded.");

    /// <summary>Every text the accent draws, at 4.5:1, and every fill and mark, at 3:1, on gold.
    /// Gold is also held to its higher floor, in <see cref="ThemeTokenContrastTests"/>.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GoldClearsEveryPairTheAccentDraws(bool dark) => StaThread.Run(() =>
    {
        ResourceDictionary slate = GoldThemeFile(dark);
        Color Of(string key) => (Color)slate[key];

        foreach ((string text, string background) in AccentTextPairs)
        {
            double ratio = SlateContrast.WcagRatio(Of(text), Of(background));
            Assert.True(ratio >= 4.5, $"{text} on {background} is {ratio:F2}:1.");
        }

        foreach ((string mark, string background) in AccentMarkPairs)
        {
            double ratio = SlateContrast.WcagRatio(Of(mark), Of(background));
            Assert.True(ratio >= 3, $"{mark} on {background} is {ratio:F2}:1.");
        }

        return 0;
    }, TimeSpan.FromSeconds(30), "the theme file never loaded.");

    /// <summary>A selected row draws what it drew before: the brush is only a new name for the
    /// value under gold, and High Contrast keeps its pair.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ASelectedRowLooksTheSameUnderGold(bool dark, bool highContrast) => StaThread.Run(() =>
    {
        using var host = new ShownShell();
        Use(host, dark, highContrast, AccentSource.SlateGold, null);

        Color Brush(object key) => Assert.IsType<SolidColorBrush>(host.Shell.FindResource(key)).Color;
        Assert.Equal(
            highContrast ? Brush(SystemColors.HighlightTextBrushKey) : (Color)GoldThemeFile(dark)["Slate.SelectionTextColor"],
            Brush("Slate.SelectedTextBrush"));
        return 0;
    }, TimeSpan.FromSeconds(60), "the shell never settled.");

    /// <summary>The regression this brush exists for: white on the 12% tint, 1.17:1 in light.
    /// It reads the controls and not the keys, so a template that goes back to the selection's
    /// text fails here. Both palettes Windows has given on these desktops.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ASelectedRowIsReadableUnderAWindowsAccent(bool dark, bool pink) => StaThread.Run(() =>
    {
        using var host = new ShownShell();
        Use(host, dark, highContrast: false, AccentSource.WindowsAccent, pink ? OwnersPink : DefaultBlue);
        object Style(string key) => host.Shell.FindResource(key);
        var cell = new DataGridCell { Style = (Style)Style("Slate.GridCellStyle"), Content = "A cell", IsSelected = true };
        var list = new ListBox
        {
            ItemContainerStyle = new Style(typeof(ListBoxItem), (Style)Style("Slate.PanelRowBase")),
            ItemsSource = new[] { "A row" },
            SelectedIndex = 0,
        };
        var dropdown = new ComboBoxItem { Style = (Style)Style("Slate.ComboRowBase"), Content = "A choice", IsSelected = true };
        var highlighted = new HighlightedComboBoxItem { Style = (Style)Style("Slate.ComboRowBase"), Content = "Another choice" };
        var tree = new TreeViewItem { Style = (Style)Style("Slate.TreeRowBase"), Header = "A file", IsSelected = true };
        var toggle = new ToggleButton { Style = (Style)Style("Slate.SubtleIconToggleButtonStyle"), Content = "A", IsChecked = true };
        host.Shell.Content = new StackPanel { Children = { cell, list, dropdown, highlighted, new TreeView { Items = { tree } }, toggle } };
        host.Settle();

        Color selected = Assert.IsType<SolidColorBrush>(host.Shell.FindResource("Slate.SelectedSurfaceBrush")).Color;
        Assert.NotEqual((Color)GoldThemeFile(dark)["Slate.SelectedSurfaceColor"], selected);
        foreach ((string name, FrameworkElement owner) in new (string, FrameworkElement)[]
        {
            ("a grid cell", cell),
            ("a panel row", Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromIndex(0))),
            ("a dropdown row", dropdown),
            ("a highlighted dropdown row", highlighted),
            ("a tree row", tree),
            ("a checked icon toggle", toggle),
        })
        {
            Color fill = ShownShell.Descendants(owner).OfType<Border>()
                .Select(border => border.Background).OfType<SolidColorBrush>().First(brush => brush.Color.A == 255).Color;
            Color ink = Assert.IsType<SolidColorBrush>(ShownShell.Descendants(owner).OfType<TextBlock>().First().Foreground).Color;
            Assert.Equal(selected, fill);
            double ratio = SlateContrast.WcagRatio(ink, fill);
            Assert.True(ratio >= 4.5, $"{name} draws {ink} on {fill}, {ratio:F2}:1.");
        }

        return 0;
    }, TimeSpan.FromSeconds(60), "the shell never settled.");

    /// <summary>The design's table, read from the README, so the two can't drift: each role
    /// under a palette whose seven colours are all different.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheRoleTableMatchesTheReadme(bool dark) => StaThread.Run(() =>
    {
        var palette = new AccentPalette(Hex("#102030"), Hex("#111111"), Hex("#222222"), Hex("#333333"), Hex("#444444"), Hex("#555555"), Hex("#666666"));
        Color surface = (Color)GoldThemeFile(dark)["Slate.SurfaceColor"];
        byte goldAlpha = ((Color)GoldThemeFile(dark)["Slate.ReadingSelectionColor"]).A;
        IReadOnlyDictionary<string, Color> roles = SlateAccent.RolesFor(palette, surface, goldAlpha, dark);
        string[] lines = File.ReadAllLines(Path.Combine(SourceText.RepoRoot(), "design", "windows", "xaml", "README.md"));
        string[] rows = lines
            .Select(line => line.Split('|', StringSplitOptions.TrimEntries))
            .Where(cells => cells.Length == 7 && Regex.IsMatch(cells[1], "^`[A-Z][A-Za-z]+`$") && roles.ContainsKey($"Slate.{cells[1].Trim('`')}Color"))
            .Select(cells => cells[1])
            .ToArray();
        Assert.Equal(roles.Count, rows.Length);

        Color Expected(string cell)
        {
            Match percent = Regex.Match(cell, "^`AccentColor` (?:at )?(\\d+)%( over the surface)?$");
            if (percent.Success)
            {
                double share = double.Parse(percent.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) / 100;
                return percent.Groups[2].Success
                    ? SlateContrast.Mix(palette.Accent, surface, share)
                    : Color.FromArgb((byte)Math.Round(share * 255), palette.Accent.R, palette.Accent.G, palette.Accent.B);
            }

            string name = cell.Trim('`');
            return name switch
            {
                "AccentColor" => palette.Accent,
                "AccentColorLight1" => palette.Light1,
                "AccentColorLight2" => palette.Light2,
                "AccentColorLight3" => palette.Light3,
                "AccentColorDark1" => palette.Dark1,
                "AccentColorDark2" => palette.Dark2,
                "AccentColorDark3" => palette.Dark3,
                _ => Hex(name),
            };
        }

        foreach (string[] cells in lines
            .Select(line => line.Split('|', StringSplitOptions.TrimEntries))
            .Where(cells => cells.Length == 7 && rows.Contains(cells[1])))
        {
            Assert.Equal(Expected(cells[dark ? 5 : 4]), roles[$"Slate.{cells[1].Trim('`')}Color"]);
        }

        return 0;
    }, TimeSpan.FromSeconds(30), "the theme file never loaded.");

    /// <summary>Everything gold follows it, through the keys the controls read.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EverythingGoldFollowsTheAccent(bool dark) => StaThread.Run(() =>
    {
        using var host = new ShownShell();
        Use(host, dark, highContrast: false, AccentSource.WindowsAccent, Chosen);
        Color accent = dark ? Chosen.Light2 : Chosen.Dark1;
        Color hover = dark ? Chosen.Light3 : Chosen.Dark2;
        Color pressed = dark ? Chosen.Light1 : Chosen.Dark3;
        Color text = dark ? Chosen.Light3 : Chosen.Dark2;

        Color Brush(object key) => Assert.IsType<SolidColorBrush>(host.Shell.FindResource(key)).Color;

        // A primary button at rest, hovered and pressed, and what's on it.
        Assert.Equal(accent, Brush("AccentButtonBackground"));
        Assert.Equal(hover, Brush("AccentButtonBackgroundPointerOver"));
        Assert.Equal(pressed, Brush("AccentButtonBackgroundPressed"));
        Assert.Equal(dark ? Colors.Black : Colors.White, Brush("AccentButtonForeground"));

        // A selected row's pill, a checked box, a focused field's underline.
        Assert.Equal(accent, Brush("Slate.AccentIndicatorBrush"));
        Assert.Equal(accent, Brush("CheckBoxCheckBackgroundFillChecked"));
        Assert.Equal(accent, Brush("TextControlFocusedBorderBrush"));

        // A link, at rest and pressed, and the editor's accent text.
        Assert.Equal(text, Brush("HyperlinkForeground"));
        Assert.Equal(pressed, Brush("HyperlinkButtonForegroundPressed"));
        Assert.Equal(text, Brush(EditorSyntaxPalette.HeadingBrushKey));
        Assert.Equal(text, Brush(EditorSyntaxPalette.WikilinkBrushKey));
        Assert.Equal(text, Brush(EditorSyntaxPalette.TagBrushKey));

        // Text selection, in a field and in a classic list's rows, and the reading view's.
        Assert.Equal(Chosen.Accent, Brush(TextBoxSelection.BackgroundBrushKey));
        Assert.Equal(Colors.White, Brush(TextBoxSelection.TextBrushKey));
        Assert.Equal(Chosen.Accent, Brush(SystemColors.HighlightBrushKey));
        Color reading = Brush(TextBoxSelection.ReadingBrushKey);
        Assert.Equal(Chosen.Accent, Color.FromRgb(reading.R, reading.G, reading.B));
        Assert.Equal(((Color)GoldThemeFile(dark)["Slate.ReadingSelectionColor"]).A, reading.A);
        return 0;
    }, TimeSpan.FromSeconds(60), "the shell never settled.");

    /// <summary>A change is one rebuild, and under gold it's none.</summary>
    [Fact]
    public void AnAccentChangeNeedsOneReapply()
    {
        Assert.True(SlateAccent.ChangeNeedsReapply(AccentSource.Automatic, DefaultBlue, Chosen));
        Assert.True(SlateAccent.ChangeNeedsReapply(AccentSource.WindowsAccent, DefaultBlue, Chosen));
        Assert.False(SlateAccent.ChangeNeedsReapply(AccentSource.SlateGold, DefaultBlue, Chosen));
        Assert.False(SlateAccent.ChangeNeedsReapply(AccentSource.Automatic, Chosen, Chosen with { }));
        Assert.True(SlateAccent.ChangeNeedsReapply(AccentSource.WindowsAccent, DefaultBlue, DefaultBlue with { Dark3 = Hex("#000000") }));
    }

    /// <summary>A row the keys or the pointer are on, which only the list can say otherwise.</summary>
    private sealed class HighlightedComboBoxItem : ComboBoxItem
    {
        public HighlightedComboBoxItem() => IsHighlighted = true;
    }

    /// <summary>What the manager merges, into the shell's own resources.</summary>
    private static void Use(ShownShell host, bool dark, bool highContrast, AccentSource accent, AccentPalette? palette)
    {
        ResourceDictionary[] layers = ThemeManager.DictionariesFor(ThemeOf(dark), highContrast, 1, accent, palette);
        Collection<ResourceDictionary> merged = host.Shell.Resources.MergedDictionaries;
        for (int index = ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false).Length - 1; index >= 0; index--)
        {
            merged.RemoveAt(index);
        }

        for (int index = 0; index < layers.Length; index++)
        {
            merged.Insert(index, layers[index]);
        }

        host.Settle();
    }
}
