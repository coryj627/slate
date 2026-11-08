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
        Assert.False(string.IsNullOrWhiteSpace(toolbar.TitleBlock.Text), $"{id}'s toolbar has no title.");
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
        Assert.True(((FrameworkElement)VisualTreeHelper.GetParent(label)).ActualHeight >= 32, "the row is under 32 tall.");
        Assert.Equal(Color(host, "Slate.TertiaryTextBrush"), Assert.IsType<SolidColorBrush>(count.Foreground).Color);
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
        Rectangle? pill = ShownShell.Descendants(alpha).OfType<Rectangle>().FirstOrDefault(shape => shape.Width == 3);
        if (pill is not null)
        {
            Assert.NotEqual(Colors.Transparent, Assert.IsType<SolidColorBrush>(pill.Fill).Color);
        }
    });

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
