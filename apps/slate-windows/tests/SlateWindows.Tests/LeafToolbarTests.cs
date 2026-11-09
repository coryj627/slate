// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SlateWindows.Tests;

/// <summary>
/// DS-18: every right-pane leaf opens with DS-17's toolbar, its title the level 2
/// heading, and tags draw as count rows, label on the left and count on the right.
/// Names don't change: a row's container names it, as before.
/// </summary>
public sealed class LeafToolbarTests
{
    public static TheoryData<string> LeafIds()
    {
        var data = new TheoryData<string>();
        foreach (WorkspaceLeafOption leaf in WorkspaceViewModel.Leaves)
        {
            data.Add(leaf.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(LeafIds))]
    public void EveryLeafOpensWithTheToolbar(string id) => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n\nA [[note]] line.\n"));
        host.Workspace.OpenPath("note.md");
        host.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.Single(leaf => leaf.Id == id);
        host.Settle();
        host.Settle();
        var leaves = (Grid)host.Shell.FindName("RightPaneLeafHost");

        PanelToolbarView toolbar = Assert.Single(
            ShownShell.Descendants(leaves).OfType<PanelToolbarView>(),
            candidate => candidate.IsVisible);
        Assert.Equal(AutomationHeadingLevel.Level2, AutomationProperties.GetHeadingLevel(toolbar.TitleBlock));
        Assert.Contains(toolbar.TitleBlock.Text, ExpectedTitles(host, id));
        Assert.True(toolbar.ActualHeight >= 40, $"{id}'s toolbar is {toolbar.ActualHeight:F0} tall.");
        Assert.All(toolbar.Buttons.OfType<ButtonBase>(), button =>
        {
            Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(button)), $"{id} has an unnamed button.");
            Assert.True(button.Command is not null || HasClick(button), $"{id} has a button that does nothing.");
        });

        // One heading per leaf: nothing else in it is a level 2 heading.
        Assert.DoesNotContain(
            ShownShell.Descendants(leaves).OfType<TextBlock>(),
            text => text.IsVisible && !ReferenceEquals(text, toolbar.TitleBlock)
                && AutomationProperties.GetHeadingLevel(text) == AutomationHeadingLevel.Level2);
    });

    /// <summary>Codex round 1: Citations shows its count, as Panes.jsx draws it, from the
    /// rows the leaf holds; Backlinks and Outgoing links say theirs in core's header.</summary>
    [Fact]
    public void TheCitationsToolbarCountsItsRows() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        host.Workspace.OpenPath("note.md");
        host.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.Single(leaf => leaf.Id == "citations");
        host.Settle();
        var leaves = (Grid)host.Shell.FindName("RightPaneLeafHost");
        PanelToolbarView toolbar = Assert.Single(ShownShell.Descendants(leaves).OfType<PanelToolbarView>(), candidate => candidate.IsVisible);

        Assert.Equal(host.Workspace.Citations.Rows.Count.ToString(System.Globalization.CultureInfo.CurrentCulture), toolbar.Count);
    });

    [Fact]
    public void ACountRowDrawsLabelAndCountAndKeepsItsName() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n\n#alpha and #beta\n"), ("other.md", "# Other\n\n#alpha\n"));
        using FilesToolbarTests.Attached attached = FilesToolbarTests.Attach(host);
        TreeViewItem alpha = TagRow(host, attached.Sidebar, "alpha");

        Assert.Equal("alpha, 2 files", AutomationProperties.GetName(alpha));
        TextBlock label = Part(alpha, "Label");
        TextBlock count = Part(alpha, "Count");
        Assert.Equal("alpha", label.Text);
        Assert.Equal("2", count.Text);
        Assert.True(label.TranslatePoint(default, alpha).X < count.TranslatePoint(default, alpha).X, "the count isn't right of the label.");
        Assert.Equal(FontNumeralAlignment.Tabular, System.Windows.Documents.Typography.GetNumeralAlignment(count));
        // The row as drawn, its padding in: 32 at normal text size.
        var header = (FrameworkElement)alpha.Template.FindName("PART_Header", alpha);
        var rowBox = (FrameworkElement)VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(header));
        Assert.Equal(32, rowBox.ActualHeight, 1);
        Assert.Equal(Color(host, "Slate.TertiaryTextBrush"), Assert.IsType<SolidColorBrush>(count.Foreground).Color);
    });

    /// <summary>Codex round 2: a long tag trims its label rather than push every count
    /// in the tree past the sidebar's edge.</summary>
    [Fact]
    public void ALongTagKeepsEveryCountInsideTheSidebar() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n\n#alpha and #project_documentation_references_for_the_whole_team\n"));
        using FilesToolbarTests.Attached attached = FilesToolbarTests.Attach(host);
        Assert.Equal(260, host.Workspace.FilesSidebarWidth);
        TreeViewItem alpha = TagRow(host, attached.Sidebar, "alpha");
        TreeViewItem longTag = TagRow(host, attached.Sidebar, "project_documentation_references_for_the_whole_team");
        host.Settle();
        var tree = (FrameworkElement)host.Shell.FindName("FilesPaneBorder");

        foreach (TreeViewItem row in new[] { alpha, longTag })
        {
            TextBlock count = Part(row, "Count");
            double right = count.TranslatePoint(new Point(count.ActualWidth, 0), tree).X;
            Assert.True(right <= tree.ActualWidth + 0.5, $"{((SidebarTagViewModel)row.DataContext).Segment}'s count ends at {right:F0} in a {tree.ActualWidth:F0} pane.");
        }
    });

    [Fact]
    public void ASelectedCountRowHasThePillASemiboldLabelAndItsCaptionBrush() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n\n#alpha\n"));
        using FilesToolbarTests.Attached attached = FilesToolbarTests.Attach(host);
        TreeViewItem alpha = TagRow(host, attached.Sidebar, "alpha");

        alpha.IsSelected = true;
        host.Settle();

        Assert.Equal(FontWeights.SemiBold, Part(alpha, "Label").FontWeight);
        Assert.Equal(Color(host, "Slate.SelectedCaptionBrush"), Assert.IsType<SolidColorBrush>(Part(alpha, "Count").Foreground).Color);
        Rectangle pill = Assert.Single(ShownShell.Descendants(alpha).OfType<Rectangle>(), shape => shape.Width == 3 && shape.Height == 16);
        Assert.True(pill.IsVisible, "the selection pill isn't shown");
        Assert.Equal(Color(host, "Slate.AccentIndicatorBrush"), Assert.IsType<SolidColorBrush>(pill.Fill).Color);
    });

    /// <summary>A selected tag row's count, as drawn, on the row's background as drawn:
    /// |Lc| above 75 under gold, and 4.5:1 under Windows' default blue (xaml/README.md's
    /// bar for an accent), in light and dark.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ASelectedCountClearsTheFloor(bool dark, bool windowsBlue) => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n\n#alpha\n"));
        AccentPalette blue = new(
            Hex("#0078D4"), Hex("#0091F8"), Hex("#4CC2FF"), Hex("#99EBFF"), Hex("#0067C0"), Hex("#003E92"), Hex("#001A68"));
        foreach (ResourceDictionary layer in ThemeManager.DictionariesFor(
            dark ? SlateTheme.Dark : SlateTheme.Light,
            highContrast: false,
            accent: windowsBlue ? AccentSource.WindowsAccent : AccentSource.SlateGold,
            palette: windowsBlue ? blue : null))
        {
            host.Shell.Resources.MergedDictionaries.Add(layer);
        }

        using FilesToolbarTests.Attached attached = FilesToolbarTests.Attach(host);
        TreeViewItem alpha = TagRow(host, attached.Sidebar, "alpha");
        alpha.IsSelected = true;
        host.Settle();

        System.Windows.Media.Color count = Assert.IsType<SolidColorBrush>(Part(alpha, "Count").Foreground).Color;
        System.Windows.Media.Color background = Assert.IsType<SolidColorBrush>(alpha.Background).Color;
        Assert.Equal(Color(host, "Slate.SelectedSurfaceBrush"), background);
        if (windowsBlue)
        {
            double ratio = Wcag(count, background);
            Assert.True(ratio >= 4.5, $"a selected count measures {ratio:F2}:1 on its row under the default blue.");
        }
        else
        {
            double lc = ThemeTokenContrastTests.Lc(count, background);
            Assert.True(lc > 75, $"a selected count measures |Lc| {lc:F1} on its row under gold.");
        }
    });

    private static System.Windows.Media.Color Hex(string hex) => (System.Windows.Media.Color)ColorConverter.ConvertFromString(hex);

    private static double Wcag(System.Windows.Media.Color a, System.Windows.Media.Color b)
    {
        static double Channel(byte value)
        {
            double c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        static double Luminance(System.Windows.Media.Color color) =>
            (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));

        double one = Luminance(a);
        double two = Luminance(b);
        return (Math.Max(one, two) + 0.05) / (Math.Min(one, two) + 0.05);
    }

    /// <summary>High Contrast puts a selected count in highlight text, as it does the
    /// row's label, never window text on the highlight.</summary>
    [Fact]
    public void HighContrastPutsHighlightTextOnASelectedCount() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"));
        foreach (ResourceDictionary layer in ThemeManager.DictionariesFor(SlateTheme.Dark, highContrast: true))
        {
            host.Shell.Resources.MergedDictionaries.Add(layer);
        }

        host.Settle();
        Assert.Equal(Color(host, "Slate.SelectedTextBrush"), Color(host, "Slate.SelectedCaptionBrush"));
        Assert.Equal(SystemColors.HighlightTextColor, Color(host, "Slate.SelectedCaptionBrush"));
    });

    /// <summary>What each leaf's heading said before DS-18, from the same sources.</summary>
    private static string[] ExpectedTitles(ShownShell host, string id)
    {
        WorkspaceViewModel workspace = host.Workspace;
        string leafTitle = WorkspaceViewModel.Leaves.Single(leaf => leaf.Id == id).Title;
        return id switch
        {
            "backlinks" => [workspace.Panels.BacklinksHeader],
            "outgoingLinks" => [workspace.Panels.OutgoingLinksHeader],
            "outline" => ["Outline"],
            "embeds" => [workspace.Panels.EmbedsHeader],
            "tasks" => [workspace.Panels.TasksHeader],
            "tasksReview" => [workspace.TasksReview.Header],
            "citations" => [Panels.CitationPhrase.CitationsHeading],
            "bibliography" => [Panels.CitationPhrase.BibliographyHeading],
            "queries" => ["Queries"],
            "basesDock" => ["Base dock"],
            "history" => ["History"],
            "syncDiagnostics" => ["Sync"],
            // The Connections leaf titles itself by its root note, or its own name with none.
            "connections" => [Graph.ConnectionsPhrase.Title, "note.md", "note"],
            _ => [leafTitle],
        };
    }

    private static bool HasClick(ButtonBase button) => HasClickHandler(button);
    private static bool HasClickHandler(ButtonBase button)
    {
        object? store = typeof(UIElement)
            .GetProperty("EventHandlersStore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(button);
        if (store is null)
        {
            return false;
        }

        var handlers = (Array?)store.GetType()
            .GetMethod("GetRoutedEventHandlers", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?
            .Invoke(store, [ButtonBase.ClickEvent]);
        return handlers is { Length: > 0 };
    }

    private static TreeViewItem TagRow(ShownShell host, FilesSidebarViewModel sidebar, string segment)
    {
        sidebar.ShowTags = true;
        TreeViewItem? row = null;
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () =>
                {
                    host.Settle();
                    row = ShownShell.Descendants(host.Shell).OfType<TreeViewItem>()
                        .FirstOrDefault(item => item.DataContext is SidebarTagViewModel tag && tag.Segment == segment);
                    return row is not null;
                },
                TimeSpan.FromSeconds(10)),
            $"no tag row for {segment}");
        return row!;
    }

    private static TextBlock Part(TreeViewItem row, string name) =>
        ShownShell.Descendants(row).OfType<TextBlock>().Single(text => text.Name == name);

    private static Color Color(ShownShell host, string key) =>
        Assert.IsType<SolidColorBrush>(host.Shell.FindResource(key)).Color;

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
