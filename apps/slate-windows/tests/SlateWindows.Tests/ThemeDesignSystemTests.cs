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

    /// <summary>#1767: a local style replaces Fluent's and falls back to WPF's
    /// classic template, whose literal colours no resource key can change. Each of
    /// these control types has a Slate base, and a new local style without it
    /// fails here.</summary>
    [Theory]
    [InlineData("ListBoxItem", "Slate.PanelRowBase")]
    [InlineData("ListBox", "Slate.PanelListBase")]
    [InlineData("Button", "Slate.ButtonBase")]
    [InlineData("TreeViewItem", "Slate.TreeRowBase")]
    public void EveryLocalStyleOfAClassicControlIsBasedOnItsSlateBase(string target, string baseKey)
    {
        string[] stray = [.. Directory
            .EnumerateFiles(SourceText.ShellSourceRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => XDocument.Load(file, LoadOptions.SetLineInfo)
                .Descendants()
                .Where(element => element.Name.LocalName == "Style"
                    && (string?)element.Attribute("TargetType") == target
                    && (string?)element.Attribute(Xaml + "Key") != baseKey
                    && (string?)element.Attribute("BasedOn") != $"{{StaticResource {baseKey}}}")
                .Select(element => $"{Path.GetFileName(file)}:{((System.Xml.IXmlLineInfo)element).LineNumber}"))];

        Assert.True(stray.Length == 0, $"{target} styles not based on {baseKey}: {string.Join(", ", stray)}");
    }

    /// <summary>A subclass gets no implicit style, because those are keyed on
    /// the exact type. The command palette's list painted classic white under
    /// the dark theme's pale text.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TheSubclassedListAndTreeTakeFluentsStyles(bool dark, bool highContrast)
    {
        (bool list, bool tree) = InRealWindow(dark, highContrast, window =>
        {
            var box = new AutomationVisibilityListBox();
            var view = new LandingTreeView();
            window.Content = new StackPanel { Children = { box, view } };
            window.UpdateLayout();
            Pump();
            return (
                ReferenceEquals(box.Style, window.FindResource(typeof(ListBox))),
                ReferenceEquals(view.Style, window.FindResource(typeof(TreeView))));
        });

        Assert.True(list, "the palette's list has no Fluent style.");
        Assert.True(tree, "the tree has no Fluent style.");
    }

    /// <summary>Fluent's list has no border or padding, so an empty one measured
    /// nothing, and an on-screen element with no rectangle is an accessibility
    /// error: a query with no matches left the reader on a list with no bounds.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void AnEmptyListStillHasARectangle(bool dark, bool highContrast) => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        ApplyTheme(host, dark, highContrast);
        var overlay = new AutomationVisibilityListBox();
        var panel = new ListBox { Style = (Style)host.Shell.FindResource("Slate.PanelListBase") };
        host.Shell.Content = new StackPanel { Children = { overlay, panel } };
        host.Settle();

        Assert.True(overlay.ActualHeight > 0, "an empty overlay list has no rectangle.");
        Assert.True(panel.ActualHeight > 0, "an empty panel list has no rectangle.");
    });

    /// <summary>The selected cell drew pale text on bright gold.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheGridTakesTheThemesCell(bool dark)
    {
        bool cellStyle = InRealWindow(dark, highContrast: false, window =>
        {
            var grid = new SlateWindows.Grids.AccessibleDataGrid();
            window.Content = grid;
            window.UpdateLayout();
            Pump();
            DataGrid inner = ShownShell.Descendants(grid).OfType<DataGrid>().Single();
            return ReferenceEquals(inner.CellStyle, window.FindResource("Slate.GridCellStyle"));
        });

        Assert.True(cellStyle, "the grid doesn't use the theme's cell style.");
    }

    /// <summary>#1772: the canvas outline's rows are subclasses with a style built
    /// in code, so they kept WPF's classic row.</summary>
    [Fact]
    public void TheCanvasOutlineUsesTheShellsTreeRows() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        var row = new SlateWindows.Canvas.CanvasOutlineItem { Header = "A card" };
        host.Shell.Content = new TreeView { Items = { row } };
        host.Settle();

        Assert.Same(host.Shell.FindResource("Slate.TreeRowTemplate"), row.Template);
    });

    /// <summary>#1772: a task's text ran past the pane and was cut mid-word, because
    /// its row was a horizontal stack that gave the text unlimited width. The Tasks and
    /// Tasks Review leaves each have their own row.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ATaskRowWrapsItsTextInsideTheRow(bool review) => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        var list = new ListBox
        {
            Style = (Style)host.Shell.RightPaneBorder.FindResource("PanelListBox"),
            ItemTemplate = review
                ? host.Shell.PanelReviewList.ItemTemplate
                : (DataTemplate)host.Shell.RightPaneBorder.FindResource("PanelTaskRowTemplate"),
            ItemContainerStyle = (Style)host.Shell.FindResource("Slate.PanelRowBase"),
            ItemsSource = new[]
            {
                new
                {
                    FileName = "A note with quite a long file name that also has to fit.md",
                    DisplayText = "Reply to the accessibility review before the end of the week",
                    MetadataCaption = "Due 2026-06-01 and priority highest and a little more besides",
                    Completed = false,
                    HasMetadata = true,
                    CheckboxLabel = "Task",
                    CheckboxHelpText = "Help",
                },
            },
            Width = 160,
            Height = 200,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        host.Shell.Content = list;
        host.Settle();

        TextBlock[] texts = ShownShell.Descendants(list).OfType<TextBlock>().ToArray();
        Assert.NotEmpty(texts);
        double right = texts.Max(block => block.TranslatePoint(new Point(block.ActualWidth, 0), list).X);
        Assert.True(right <= 160, $"the row's text reaches {right:F0}px in a 160px list.");
    });

    /// <summary>#1772: the right pane can be 200px wide, and a horizontal stack cuts
    /// off whatever doesn't fit: Queries' buttons, Bibliography's second radio button.</summary>
    [Fact]
    public void TheRightPaneHasNoHorizontalStacks()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement leafHost = XDocument
            .Load(Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"), LoadOptions.SetLineInfo)
            .Descendants(presentation + "Grid")
            .Single(grid => (string?)grid.Attribute(xaml + "Name") == "RightPaneLeafHost");

        string[] stacks = [.. leafHost
            .Descendants(presentation + "StackPanel")
            .Where(stack => (string?)stack.Attribute("Orientation") == "Horizontal")
            .Select(stack => $"line {((System.Xml.IXmlLineInfo)stack).LineNumber}")];

        Assert.Empty(stacks);
    }

    /// <summary>#1773: the filter was a fixed 220px in whatever room was left, and
    /// the switcher beside it cut off its right edge.</summary>
    [Fact]
    public void TheGraphFilterStopsBeforeTheSwitcher() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        var view = new SlateWindows.Graph.GraphSurfaceView { Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
        host.Shell.Content = view;
        host.Settle();

        FrameworkElement Find(string id) => ShownShell.Descendants(view)
            .OfType<FrameworkElement>()
            .Single(element => System.Windows.Automation.AutomationProperties.GetAutomationId(element) == id);
        FrameworkElement field = Find("GraphFilterField");
        double right = field.TranslatePoint(new Point(field.ActualWidth, 0), view).X;
        double switcher = Find("GraphSurfaceSwitcher").TranslatePoint(new Point(0, 0), view).X;
        Assert.True(right <= switcher, $"the filter ends at {right:F0}px and the switcher starts at {switcher:F0}px.");
    });

    /// <summary>#1773: a list property's item was a horizontal stack with a 140px
    /// field, so its Remove button was cut off in a narrow editor.</summary>
    [Fact]
    public void AListPropertysRemoveButtonStaysInItsRow() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        var presenter = new ContentPresenter
        {
            ContentTemplate = (DataTemplate)host.Shell.FindResource("PropertyListEditorTemplate"),
            Content = new
            {
                AddItemVisibleText = "Add item",
                AddItemLabel = "Add item",
                Items = new[] { new ListItemRow() },
            },
        };
        var row = new Border { Width = 170, HorizontalAlignment = HorizontalAlignment.Left, Child = presenter };
        host.Shell.Content = row;
        host.Settle();

        Button remove = ShownShell.Descendants(presenter).OfType<Button>().Single(button => button.Content is "Remove");
        double right = remove.TranslatePoint(new Point(remove.ActualWidth, 0), row).X;
        Assert.True(right <= 170, $"Remove ends at {right:F0}px in a 170px row.");
    });

    /// <summary>#1773: the splitters were 7px bars of the border colour. They keep
    /// that width to grab and draw one line.</summary>
    /// <summary>Fluent's grid animates its Disabled state to fixed colours by these
    /// keys, and every open overlay disables the shell behind it: the table turned
    /// white under the theme's pale text. The pixels are checked in
    /// ThemedShell_ADisabledGridStaysOnItsSurface.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TheSplittersDrawAThinLine(bool dark, bool highContrast) => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        ApplyTheme(host, dark, highContrast);
        host.Settle();

        GridSplitter[] splitters = [.. ShownShell.Descendants(host.Shell).OfType<GridSplitter>()];
        Assert.NotEmpty(splitters);
        foreach (GridSplitter splitter in splitters)
        {
            System.Windows.Shapes.Rectangle line = Assert.Single(ShownShell.Descendants(splitter).OfType<System.Windows.Shapes.Rectangle>());
            Assert.Equal(1, line.ActualWidth);
        }
    });

    /// <summary>#1773: the bar stays in the UIA tree after a scan, because a reader
    /// can ask how the last one ended, but gold there says nothing and competes with
    /// real selection, so it only draws fully while work is running.</summary>
    [Theory]
    [InlineData(0, 1, false, false)]
    [InlineData(1, 2, false, true)]
    [InlineData(2, 2, false, false)]
    [InlineData(0, 1, true, true)]
    public void TheStatusBarProgressKnowsWhenWorkIsRunning(double value, double maximum, bool indeterminate, bool active) => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        var lifecycle = Assert.IsType<VaultLifecycleViewModel>(host.Shell.DataContext);
        void Set(string name, object content) => typeof(VaultLifecycleViewModel).GetProperty(name)!.SetValue(lifecycle, content);
        Set(nameof(VaultLifecycleViewModel.ProgressMaximum), maximum);
        Set(nameof(VaultLifecycleViewModel.ProgressValue), value);
        Set(nameof(VaultLifecycleViewModel.IsProgressIndeterminate), indeterminate);

        Assert.Equal(active, typeof(VaultLifecycleViewModel).GetProperty("IsProgressActive")?.GetValue(lifecycle));
    });

    /// <summary>Fluent's grid animates its Disabled state to fixed colours by these
    /// keys, and every open overlay disables the shell behind it: the table turned
    /// white under the theme's pale text. The pixels are checked in
    /// ThemedShell_ADisabledGridStaysOnItsSurface.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TheGridsDisabledStateUsesTheThemesColours(bool dark, bool highContrast)
    {
        (Color surface, Color text, Color disabledSurface, Color disabledText) = InRealWindow(dark, highContrast, window => (
            highContrast ? SystemColors.WindowColor : (Color)window.FindResource("Slate.SurfaceColor"),
            highContrast ? SystemColors.GrayTextColor : (Color)window.FindResource("Slate.DisabledTextColor"),
            (Color)window.FindResource("ControlLightColor"),
            (Color)window.FindResource("DisabledForegroundColor")));

        Assert.Equal(surface, disabledSurface);
        Assert.Equal(text, disabledText);
    }

    /// <summary>The row header showed the clipped first letters of each row left
    /// of the first column, which already shows them. It's still the row's
    /// identity for a screen reader, so it keeps its name and its rectangle.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TheRowHeaderDrawsNoTextAndKeepsItsName(bool dark, bool highContrast)
    {
        (string[] texts, int grips, double width, string name) = InRealWindow(dark, highContrast, window =>
        {
            var grid = new SlateWindows.Grids.AccessibleDataGrid();
            window.Content = grid;
            grid.Bind(
                [new SlateWindows.Grids.AccessibleGridColumn { Header = "Name", Cell = row => (string)row, IsRowHeader = true }],
                ["A row"],
                "1 row.",
                "Rows",
                rowAutomationName: row => (string)row,
                rowKey: row => (string)row);
            window.UpdateLayout();
            Pump();
            var header = ShownShell.Descendants(grid).OfType<System.Windows.Controls.Primitives.DataGridRowHeader>().Single();
            return (
                ShownShell.Descendants(header).OfType<TextBlock>().Select(block => block.Text).ToArray(),
                ShownShell.Descendants(header).OfType<System.Windows.Controls.Primitives.Thumb>().Count(),
                header.ActualWidth,
                System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(header).GetName());
        });

        Assert.Empty(texts);
        Assert.Equal(0, grips);
        Assert.True(width > 0, "the row header has no rectangle.");
        Assert.Equal("A row", name);
    }

    /// <summary>The classic list turns fixed white when disabled, which every
    /// open overlay does to the shell behind it.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TheBaseControlsDrawTheThemeInEveryState(bool dark, bool highContrast) => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        ApplyTheme(host, dark, highContrast);
        var list = new ListBox { Style = (Style)host.Shell.FindResource("Slate.PanelListBase"), IsEnabled = false, ItemsSource = new[] { "A row" } };
        var enabled = new Button { Style = (Style)host.Shell.FindResource("Slate.ButtonBase"), Content = "Save" };
        var disabled = new Button { Style = (Style)host.Shell.FindResource("Slate.ButtonBase"), Content = "Delete", IsEnabled = false };
        var row = new TreeViewItem { Style = (Style)host.Shell.FindResource("Slate.TreeRowBase"), Header = "A row", IsSelected = true, IsExpanded = true, Items = { new TreeViewItem { Style = (Style)host.Shell.FindResource("Slate.TreeRowBase"), Header = "A child" } } };
        var cell = new DataGridCell { Style = (Style)host.Shell.FindResource("Slate.GridCellStyle"), Content = "A cell", IsSelected = true };
        host.Shell.Content = new StackPanel { Children = { list, enabled, disabled, new TreeView { Items = { row } }, cell } };
        host.Settle();

        Color Theme(string key) => Assert.IsType<SolidColorBrush>(host.Shell.FindResource(key)).Color;
        Color Fill(DependencyObject element) => Assert.IsType<SolidColorBrush>(
            ShownShell.Descendants(element).OfType<Border>().First().Background).Color;

        Assert.Equal(Colors.Transparent, Fill(list));
        Assert.Equal(Theme("ButtonBackground"), Fill(enabled));
        Assert.Equal(Theme("ButtonForeground"), Assert.IsType<SolidColorBrush>(enabled.Foreground).Color);
        Assert.Equal(Theme("ButtonBackgroundDisabled"), Fill(disabled));
        Assert.Equal(Theme("ButtonForegroundDisabled"), Assert.IsType<SolidColorBrush>(disabled.Foreground).Color);
        Assert.Equal(Theme("Slate.SelectedSurfaceBrush"), ShownShell.Descendants(row).OfType<Border>()
            .Select(border => border.Background).OfType<SolidColorBrush>().First(brush => brush.Color.A > 0).Color);
        Assert.Equal(Theme("Slate.AccentIndicatorBrush"), Assert.IsType<SolidColorBrush>(
            ShownShell.Descendants(row).OfType<System.Windows.Shapes.Rectangle>().First().Fill).Color);
        Assert.Equal(Theme("Slate.SelectedSurfaceBrush"), Fill(cell));
        Assert.Equal(Theme(TextBoxSelection.TextBrushKey), Assert.IsType<SolidColorBrush>(cell.Foreground).Color);
        return 0;
    }, TimeSpan.FromSeconds(60), "the base control test timed out.");

    /// <summary>#1762: a list row with its own style would otherwise fall back to
    /// WPF's classic template, whose blue selection no resource key can change.
    /// A style added later without the base brings it back, so this reads every
    /// XAML file.</summary>
    [Fact]
    public void EveryListRowStyleIsBasedOnThePanelRow()
    {
        string[] stray = [.. Directory
            .EnumerateFiles(SourceText.ShellSourceRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => XDocument.Load(file, LoadOptions.SetLineInfo)
                .Descendants()
                .Where(element => element.Name.LocalName == "Style"
                    && (string?)element.Attribute("TargetType") is "ListBoxItem" or "{x:Type ListBoxItem}"
                    && (string?)element.Attribute(Xaml + "Key") != "Slate.PanelRowBase"
                    && (string?)element.Attribute("BasedOn") != "{StaticResource Slate.PanelRowBase}")
                .Select(element => $"{Path.GetFileName(file)}:{((System.Xml.IXmlLineInfo)element).LineNumber}"))];

        Assert.True(stray.Length == 0, $"not based on the panel row: {string.Join(", ", stray)}");
    }

    /// <summary>The panel row draws the design's selected state, the selected
    /// surface and a gold pill, with or without the keyboard focus.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ASelectedPanelRowDrawsTheThemesSelection(bool dark, bool highContrast) => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        ApplyTheme(host, dark, highContrast);
        var list = new ListBox
        {
            ItemContainerStyle = new Style(typeof(ListBoxItem), (Style)host.Shell.FindResource("Slate.PanelRowBase")),
            ItemTemplate = TextRowTemplate(),
            ItemsSource = new[] { "A row" },
            SelectedIndex = 0,
        };
        host.Shell.Content = list;
        host.Settle();

        var row = Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromIndex(0));
        Border chrome = ShownShell.Descendants(row).OfType<Border>().First();
        TextBlock text = ShownShell.Descendants(row).OfType<TextBlock>().Single();
        Color Theme(string key) => Assert.IsType<SolidColorBrush>(host.Shell.FindResource(key)).Color;
        Color fill = Assert.IsType<SolidColorBrush>(chrome.Background).Color;
        Color ink = Assert.IsType<SolidColorBrush>(text.Foreground).Color;

        System.Windows.Shapes.Rectangle pill = ShownShell.Descendants(row).OfType<System.Windows.Shapes.Rectangle>().Single();
        Assert.Equal(Theme("Slate.SelectedSurfaceBrush"), fill);
        Assert.Equal(Theme(TextBoxSelection.TextBrushKey), ink);
        Assert.Equal(Theme("Slate.AccentIndicatorBrush"), Assert.IsType<SolidColorBrush>(pill.Fill).Color);
        return 0;
    }, TimeSpan.FromSeconds(60), "the row test timed out.");

    /// <summary>#1763: the editor's selection is the solid pair, because
    /// AvalonEdit draws it under the text. The border keeps the current find
    /// match apart from the others, whose fill is close to it in both themes.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TheEditorSelectsInTheThemesColors(bool dark, bool highContrast)
    {
        (Color? fill, Color? ink, Color? border, Color? wantFill, Color? wantInk, Color? wantBorder) = InRealWindow(dark, highContrast, window =>
        {
            var editor = new SlateTextEditor();
            window.Content = editor;
            window.UpdateLayout();
            Pump();
            Color? Theme(string key) => (window.FindResource(key) as SolidColorBrush)?.Color;
            return (
                (editor.TextArea.SelectionBrush as SolidColorBrush)?.Color,
                (editor.TextArea.SelectionForeground as SolidColorBrush)?.Color,
                (editor.TextArea.SelectionBorder?.Brush as SolidColorBrush)?.Color,
                Theme(TextBoxSelection.BackgroundBrushKey),
                Theme(TextBoxSelection.TextBrushKey),
                Theme("Slate.AccentTextBrush"));
        });

        Assert.NotNull(fill);
        Assert.Equal(wantFill, fill);
        Assert.Equal(wantInk, ink);
        Assert.NotNull(border);
        Assert.Equal(wantBorder, border);
    }

    /// <summary>#1051: the accent fill is under the floor on the raised surface,
    /// so text takes the accent text role, which clears it. The one reading use
    /// left is a quote's border. A later `Foreground` on the fill role fails here.</summary>
    [Fact]
    public void NoTextDrawsInTheAccentFillRole()
    {
        string[] uses = [.. Directory
            .EnumerateFiles(SourceText.ShellSourceRoot(), "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".xaml", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}Themes{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.EndsWith("ThemeManager.cs", StringComparison.Ordinal))
            .SelectMany(file => File.ReadLines(file)
                .Where(line => line.Contains("Slate.AccentBrush", StringComparison.Ordinal)
                    && !line.Contains("BorderBrush", StringComparison.Ordinal))
                .Select(line => $"{Path.GetFileName(file)}: {line.Trim()}"))];

        Assert.True(uses.Length == 0, $"the accent fill role names something other than a border: {string.Join(" | ", uses)}");
    }

    private static void ApplyTheme(ShownShell host, bool dark, bool highContrast)
    {
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
        ApplyTheme(host, dark, highContrast);

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
        // The classic tab control's panel is fixed white. A disabled grid goes
        // transparent behind an overlay, so that white showed through it (#1767).
        TabControl tabs = ShownShell.Descendants(host.Shell).OfType<TabControl>().Single();
        Assert.Equal(Theme("Slate.SurfaceBrush"), Assert.IsType<SolidColorBrush>(tabs.Background).Color);
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

    private sealed class ListItemRow
    {
        public string Text { get; set; } = "- [ ] a value long enough to fill the field";

        public string ItemLabel => "Item";

        public string RemoveLabel => "Remove";
    }

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
