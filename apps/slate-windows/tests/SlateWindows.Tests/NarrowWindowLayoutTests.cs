// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SlateWindows.Graph;

namespace SlateWindows.Tests;

/// <summary>#1795: what a narrow or short window keeps on screen.</summary>
public sealed class NarrowWindowLayoutTests
{
    [Fact]
    public void ANarrowWindowKeepsTheRightPanesRailOnScreen() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));

        host.Shell.Width = 800;
        host.Settle();

        FrameworkElement rail = host.Body("RightPaneLeavesList");
        Assert.True(rail.IsVisible);
        double right = rail.TranslatePoint(new Point(rail.ActualWidth, 0), host.Shell).X;
        Assert.True(right <= host.Shell.ActualWidth, $"the rail ends at {right:F0}px in a {host.Shell.ActualWidth:F0}px window.");
    });

    /// <summary>The window's borders take 16px at 100% display scale and 13px at 200%,
    /// so a minimum that only just fits at one scale can miss at another.</summary>
    [Fact]
    public void TheWindowsMinimumHasRoomForEveryColumnsFloor() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));

        host.Shell.Width = host.Shell.MinWidth;
        host.Settle();

        double row = host.Workspace.WorkspaceRowWidth;
        Assert.True(row > 0 && row <= host.Shell.MinWidth, $"the row reports {row:F0}px in a {host.Shell.MinWidth:F0}px window.");
        Assert.True(
            row >= WorkspaceViewModel.MinimumSidebarWidth
                + WorkspaceViewModel.MinimumEditorWidth
                + WorkspaceViewModel.MinimumRightPaneWidth
                + (2 * WorkspaceViewModel.SplitterWidth),
            $"the row has only {row:F0}px.");
    });

    /// <summary>Half of a 1920x1080 screen at 125% is 768 wide, and about 782 with the
    /// window's invisible borders. A larger minimum would stop that Snap from fitting.
    /// Plan 46's DS-14 takes the minimum down further.</summary>
    [Fact]
    public void TheWindowsMinimumStillFitsHalfOfACommonScreen() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));

        Assert.InRange(host.Shell.MinWidth, 0, 782);
    });

    /// <summary>A wrap panel squeezes a button wider than itself, and the button cuts
    /// its label off ("Refresh querie").</summary>
    [Theory]
    [InlineData("QueriesNewDashboard")]
    [InlineData("QueriesRefresh")]
    public void ARightPaneAtItsFloorStillShowsQueriesButtonsWhole(string button) => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Workspace.WorkspaceRowWidth = 1600;
        host.Workspace.RightPaneWidth = WorkspaceViewModel.MinimumRightPaneWidth;
        host.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "queries");
        host.Settle();

        FrameworkElement shown = host.Body(button);
        double width = shown.ActualWidth;
        shown.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double whole = shown.DesiredSize.Width - shown.Margin.Left - shown.Margin.Right;
        Assert.True(width >= whole - 1, $"{button} is {width:F0}px wide and its label needs {whole:F0}px.");
    });

    [Fact]
    public void GroupDatesKeepsItsLabelInANarrowFilesSidebar() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));

        host.Workspace.FilesSidebarWidth = WorkspaceViewModel.MinimumSidebarWidth;
        host.Settle();

        FrameworkElement group = host.Body("SidebarGroupDates");
        FrameworkElement files = host.Body("FilesPane");
        double right = group.TranslatePoint(new Point(group.ActualWidth, 0), files).X;
        Assert.True(right <= files.ActualWidth, $"Group dates ends at {right:F0}px in a {files.ActualWidth:F0}px sidebar.");
    });

    /// <summary>A ComboBox sizes to its selected row, so a fixed width cut the longer
    /// labels off at 14 point. Choosing one doesn't move what's beside it either.</summary>
    [Theory]
    [InlineData("NameAscending")]
    [InlineData("NameDescending")]
    [InlineData("ModifiedNewest")]
    [InlineData("ModifiedOldest")]
    [InlineData("CreatedNewest")]
    [InlineData("CreatedOldest")]
    public void TheSortBoxHoldsEachLabelWithoutMoving(string name) => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        var sort = (ComboBox)host.Body("SidebarSortOrder");
        // The hosted shell has no sidebar model to bind them from.
        sort.ItemsSource = Enum.GetValues<SidebarSortMode>();
        sort.SelectedItem = SidebarSortMode.NameAscending;
        host.Settle();
        double before = sort.ActualWidth;

        SidebarSortMode mode = Enum.Parse<SidebarSortMode>(name);
        sort.SelectedItem = mode;
        host.Settle();

        var label = new TextBlock
        {
            Text = (string)new SidebarSortModeLabelConverter().Convert(mode, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture),
            FontFamily = sort.FontFamily,
            FontSize = sort.FontSize,
        };
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        ContentPresenter shown = ShownShell.Descendants(sort).OfType<ContentPresenter>().First(presenter => presenter.ActualWidth > 0);
        Assert.True(shown.ActualWidth >= label.DesiredSize.Width, $"{label.Text} needs {label.DesiredSize.Width:F0}px and has {shown.ActualWidth:F0}px.");
        Assert.Equal(before, sort.ActualWidth);
    });

    /// <summary>DS-2: at 14 point an embed's title ran under its chevron, cut off
    /// mid-word.</summary>
    [Fact]
    public void AnEmbedsTitleWrapsInsteadOfRunningUnderItsChevron() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        const string title = "Embedded note: a title long enough to need a second line";
        var view = new EditorEmbedPreviewView
        {
            Width = 160,
            Root = new EditorEmbedPreviewNode(title, [], null, null, IsDisclosure: true, InitiallyExpanded: false, IsWarning: false),
        };
        DockPanel.SetDock(view, Dock.Top);
        ((DockPanel)host.Shell.Content).Children.Insert(0, view);
        host.Settle();

        Expander expander = ShownShell.Descendants(view).OfType<Expander>().Single();
        TextBlock shown = ShownShell.Descendants(expander).OfType<TextBlock>().First(text => text.Text == title);
        double right = shown.TranslatePoint(new Point(shown.ActualWidth, 0), expander).X;
        Assert.True(right <= expander.ActualWidth, $"the title ends at {right:F0}px in a {expander.ActualWidth:F0}px expander.");
        Assert.True(shown.ActualHeight > shown.FontSize * 2, $"the title is {shown.ActualHeight:F0}px tall, so it didn't wrap.");
    });

    /// <summary>DS-2: a link's target is one long word, and it sits in a wrap panel, so
    /// the panel squeezed it and the row cut it off.</summary>
    [Fact]
    public void ALongLinkTargetWrapsInTheOutgoingList() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "[a](https://example.com/a-link-target-far-too-long-for-the-right-pane-to-hold-on-one-line)\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Title == "Outgoing links");
        host.Settle();

        FrameworkElement list = host.Body("PanelOutgoingLinksList");
        TextBlock? target = null;
        System.Threading.SpinWait.SpinUntil(
            () =>
            {
                host.Settle();
                target = ShownShell.Descendants(list).OfType<TextBlock>().FirstOrDefault(text => text.Name == "TargetText");
                return target is not null;
            },
            TimeSpan.FromSeconds(10));

        Assert.NotNull(target);
        double right = target.TranslatePoint(new Point(target.ActualWidth, 0), list).X;
        Assert.True(right <= list.ActualWidth, $"the target ends at {right:F0}px in a {list.ActualWidth:F0}px list.");
        Assert.True(target.ActualHeight > target.FontSize * 2, $"the target is {target.ActualHeight:F0}px tall, so it didn't wrap.");
    });

    /// <summary>A note with many properties took all of a short window's height from
    /// its own text.</summary>
    [Fact]
    public void ANoteWithManyPropertiesKeepsItsTextInAShortWindow() => StaThread.Run(() =>
    {
        string keys = string.Concat(Enumerable.Range(1, 12).Select(index => $"key{index}: value\n"));
        using var host = new ShownShell(("Lecture.md", $"---\n{keys}---\n# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();

        host.Shell.Height = 620;
        host.Settle();
        System.Threading.SpinWait.SpinUntil(
            () =>
            {
                PumpedDispatcher.Drain();
                return host.Workspace.ActiveGroup.ActiveTab is WorkspaceTabViewModel { Properties.Rows.Count: > 0 };
            },
            TimeSpan.FromSeconds(10));
        host.Settle();

        FrameworkElement editor = ShownShell.Descendants(host.Shell)
            .OfType<SlateTextEditor>()
            .Single(element => element.IsVisible);
        // Four of its 22px lines, inside the editor's own 24px of padding.
        Assert.True(editor.ActualHeight >= 112, $"the editor is {editor.ActualHeight:F0}px tall.");
    });

    /// <summary>A short window takes its room back from the rows, and only there: a
    /// pane of ordinary height keeps all of it, and the shortest keeps some. This asks
    /// the shipped converter, because a host's display can't always make a tall window.</summary>
    [Theory]
    [InlineData(900, 280)]
    [InlineData(600, 280)]
    [InlineData(416, 96)]
    [InlineData(200, 80)]
    public void ThePropertyRowsKeepTheirRoomUntilTheWindowIsShort(double pane, double rows) => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        var converter = (System.Windows.Data.IValueConverter)host.Shell.FindResource("PropertyRowsMaxHeightConverter");

        Assert.Equal(rows, converter.Convert(pane, typeof(double), null!, System.Globalization.CultureInfo.InvariantCulture));
    });

    /// <summary>The rows' room comes from the pane they're in, in the shell itself.</summary>
    [Fact]
    public void ThePropertyRowsReadTheirRoomFromThePane() => StaThread.Run(() =>
    {
        string keys = string.Concat(Enumerable.Range(1, 12).Select(index => $"key{index}: value\n"));
        using var host = new ShownShell(("Lecture.md", $"---\n{keys}---\n# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();

        ScrollViewer? scroller = null;
        TabControl? pane = null;
        System.Threading.SpinWait.SpinUntil(
            () =>
            {
                // Rows that arrive after the first pass still need laying out, and a drain
                // alone leaves that to the next render.
                host.Settle();
                FrameworkElement? shown = ShownShell.Descendants(host.Shell)
                    .OfType<FrameworkElement>()
                    .FirstOrDefault(element => AutomationProperties.GetAutomationId(element) == "PropertiesRows");
                for (DependencyObject? step = shown; step is not null; step = System.Windows.Media.VisualTreeHelper.GetParent(step))
                {
                    scroller ??= step as ScrollViewer;
                    pane ??= step as TabControl;
                }

                return scroller is not null && pane is not null;
            },
            // The rows load on the thread pool, and on CI this first shell fact in its class
            // has found the load still running at 10 s. What's under test is the binding.
            TimeSpan.FromSeconds(60));

        Assert.True(scroller is not null && pane is not null, WhatTheShellShows(host));
        var converter = (System.Windows.Data.IValueConverter)host.Shell.FindResource("PropertyRowsMaxHeightConverter");
        Assert.Equal(converter.Convert(pane.ActualHeight, typeof(double), null!, System.Globalization.CultureInfo.InvariantCulture), scroller.MaxHeight);
    });

    /// <summary>What a fact that couldn't find the rows needs to say about why.</summary>
    private static string WhatTheShellShows(ShownShell host)
    {
        string tab = host.Workspace.ActiveGroup.ActiveTab switch
        {
            WorkspaceTabViewModel { Properties: { } properties } open =>
                $"tab {open.Title}: {properties.Rows.Count} rows, loading {properties.IsLoading}, error '{properties.LoadError}', expanded {properties.IsExpanded}",
            WorkspaceTabViewModel open => $"tab {open.Title} has no properties",
            null => "no active tab",
        };
        DependencyObject[] tree = ShownShell.Descendants(host.Shell).ToArray();
        return $"{tab}; shell visible {host.Shell.IsVisible}, {host.Shell.ActualWidth:F0}x{host.Shell.ActualHeight:F0}, "
            + $"{tree.Length} elements, {tree.OfType<TabControl>().Count()} tab controls, "
            + $"{tree.OfType<Expander>().Count(expander => expander.IsVisible)} visible expanders, "
            + $"{tree.OfType<PropertyRowGrid>().Count()} rows drawn.";
    }

    /// <summary>The key's fixed column and the buttons took the whole row, which left
    /// the value no width at all and cut off Delete.</summary>
    [Fact]
    public void APropertyRowStacksInANarrowEditor() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "---\nkey: value\n---\n# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        host.Shell.Width = host.Shell.MinWidth;
        host.Settle();
        System.Threading.SpinWait.SpinUntil(
            () =>
            {
                PumpedDispatcher.Drain();
                return host.Workspace.ActiveGroup.ActiveTab is WorkspaceTabViewModel { Properties.Rows.Count: > 0 };
            },
            TimeSpan.FromSeconds(10));
        host.Settle();

        FrameworkElement rows = host.Body("PropertiesRows");
        System.Windows.Controls.TextBox value = ShownShell.Descendants(rows).OfType<System.Windows.Controls.TextBox>().Single();
        System.Windows.Controls.Button delete = ShownShell.Descendants(rows)
            .OfType<System.Windows.Controls.Button>()
            .Single(button => Equals(button.Content, "Delete"));
        Assert.True(value.ActualWidth >= 140, $"the value is {value.ActualWidth:F0}px wide.");
        double right = delete.TranslatePoint(new Point(delete.ActualWidth, 0), rows).X;
        Assert.True(right <= rows.ActualWidth, $"Delete ends at {right:F0}px in {rows.ActualWidth:F0}px of rows.");
    });

    /// <summary>A text field's 160px minimum lost its right edge, and the fixed stacks
    /// beside a link or a number cut off Pick… and the steppers.</summary>
    [Fact]
    public void APropertysEditorStopsBeforeTheRowsButtons() => StaThread.Run(() =>
    {
        using var host = new ShownShell(
            ("Lecture.md", "---\ntitle: words\nlink: \"[[Other]]\"\ncount: 3\n---\n# Lecture\n"),
            ("Other.md", "# Other\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        host.Shell.Width = 1120;
        host.Settle();
        System.Threading.SpinWait.SpinUntil(
            () =>
            {
                PumpedDispatcher.Drain();
                return host.Workspace.ActiveGroup.ActiveTab is WorkspaceTabViewModel { Properties.Rows.Count: 3 };
            },
            TimeSpan.FromSeconds(10));
        host.Settle();

        FrameworkElement rows = host.Body("PropertiesRows");
        Assert.True(rows.ActualWidth >= PropertyRowGrid.StackBelow, $"the rows are only {rows.ActualWidth:F0}px wide, so they've stacked.");
        foreach (PropertyRowGrid row in ShownShell.Descendants(rows).OfType<PropertyRowGrid>())
        {
            double buttons = row.Children[2].TranslatePoint(new Point(0, 0), row).X;
            foreach (FrameworkElement part in ShownShell.Descendants(row.Children[1])
                .OfType<FrameworkElement>()
                .Where(element => element is System.Windows.Controls.TextBox or System.Windows.Controls.Primitives.ButtonBase))
            {
                double right = part.TranslatePoint(new Point(part.ActualWidth, 0), row).X;
                Assert.True(right <= buttons, $"{part.GetType().Name} ends at {right:F0}px and the buttons start at {buttons:F0}px.");
            }
        }
    });

    /// <summary>A long note name ran past the panel's edge.</summary>
    [Fact]
    public void TheConnectionsHeadingWrapsInANarrowPane() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        var leaf = new ConnectionsLeafView { Width = 120, HorizontalAlignment = HorizontalAlignment.Left };
        host.Shell.Content = leaf;
        host.Settle();

        var heading = Assert.IsType<System.Windows.Controls.TextBlock>(Named(leaf, "ConnectionsHeading"));
        Assert.Equal(TextWrapping.Wrap, heading.TextWrapping);
    });

    [Fact]
    public void TheBasesQuickFilterStaysWholeInANarrowPane() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("a-base-with-a-fairly-long-name.base", "views:\n  - type: table\n    name: All\n"));
        host.Workspace.OpenPath("a-base-with-a-fairly-long-name.base");
        host.Settle();

        host.Shell.Width = 800;
        host.Settle();

        // The Base dock leaf holds a surface too.
        FrameworkElement surface = ShownShell.Descendants(host.Shell)
            .OfType<SlateWindows.Bases.BaseSurfaceView>()
            .Single(view => view.IsVisible);
        FrameworkElement filter = Named(surface, "BaseQuickFilter");
        FrameworkElement refresh = Named(surface, "BaseRefresh");
        Rect filterBox = BoxIn(filter, surface);
        Assert.True(filterBox.Right <= surface.ActualWidth, $"the filter ends at {filterBox.Right:F0}px in a {surface.ActualWidth:F0}px surface.");
        Assert.False(filterBox.IntersectsWith(BoxIn(refresh, surface)), $"the filter at {filterBox} is under Refresh at {BoxIn(refresh, surface)}.");
    });

    [Fact]
    public void TheGraphsHeaderKeepsItsTitleAndFilterInANarrowPane() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        var view = new GraphSurfaceView { Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
        host.Shell.Content = view;
        host.Settle();

        FrameworkElement field = Named(view, "GraphFilterField");
        Assert.True(field.ActualWidth >= 140, $"the filter is {field.ActualWidth:F0}px wide.");

        Rect fieldBox = BoxIn(field, view);
        Rect switcher = BoxIn(Named(view, "GraphSurfaceSwitcher"), view);
        Rect inspector = BoxIn(Named(view, "GraphInspectorToggle"), view);
        Assert.True(fieldBox.Right <= 300 && switcher.Right <= 300 && inspector.Right <= 300, $"{fieldBox} {switcher} {inspector}");
        Assert.False(fieldBox.IntersectsWith(switcher), $"the filter at {fieldBox} is under the switcher at {switcher}.");
        Assert.False(fieldBox.IntersectsWith(inspector), $"the filter at {fieldBox} is under the inspector at {inspector}.");

        // The heading, whose box includes its own margins.
        FrameworkElement title = ShownShell.Descendants(view)
            .OfType<System.Windows.Controls.TextBlock>()
            .First(text => text.Text == "Graph");
        Rect titleBox = BoxIn(title, view);
        Assert.False(titleBox.IntersectsWith(inspector), $"the title at {titleBox} is under the inspector at {inspector}.");
        Assert.False(titleBox.IntersectsWith(switcher), $"the title at {titleBox} is under the switcher at {switcher}.");
    });

    /// <summary>#1797: at the right pane's floor the dock's surface is about 150 wide.
    /// Refresh stayed docked beside the title, which left it about 50, and the filter's
    /// 160 minimum ran past the surface's edge.</summary>
    [Fact]
    public void ABaseInTheDockKeepsItsHeaderWhole() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        var view = new SlateWindows.Bases.BaseSurfaceView { Width = 150, HorizontalAlignment = HorizontalAlignment.Left };
        host.Shell.Content = view;
        host.Settle();

        Rect filter = BoxIn(Named(view, "BaseQuickFilter"), view);
        Rect refresh = BoxIn(Named(view, "BaseRefresh"), view);
        Assert.True(filter.Right <= 150, $"the filter ends at {filter.Right:F0}px in a 150px surface.");
        Assert.True(refresh.Right <= 150, $"Refresh ends at {refresh.Right:F0}px in a 150px surface.");
        Assert.False(filter.IntersectsWith(refresh), $"the filter at {filter} is under Refresh at {refresh}.");

        var title = ShownShell.Descendants(view)
            .OfType<System.Windows.Controls.TextBlock>()
            .First(text => AutomationProperties.GetHeadingLevel(text) == AutomationHeadingLevel.Level2);
        Assert.Equal(TextWrapping.Wrap, title.TextWrapping);
    });

    /// <summary>#1797: a list row was cut off mid-word, with no way to read the rest.</summary>
    [Fact]
    public void ABasesListRowWraps() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        var view = new SlateWindows.Bases.BaseSurfaceView { Width = 200, Height = 300, HorizontalAlignment = HorizontalAlignment.Left };
        view.ListForTests.Visibility = Visibility.Visible;
        view.ListForTests.ItemsSource = new[] { new SlateWindows.Bases.BaseListHeaderViewModel(string.Concat(Enumerable.Repeat("words that wrap ", 12))) };
        host.Shell.Content = view;
        host.Settle();

        var text = ShownShell.Descendants(view.ListForTests)
            .OfType<System.Windows.Controls.TextBlock>()
            .Single(block => block.Text.StartsWith("words", StringComparison.Ordinal));
        Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
    });

    /// <summary>The dual pane's list kept a fixed 150, and the tree above it got what
    /// was left, about one row in a default window.</summary>
    [Fact]
    public void TheDualPanesListLeavesTheFilesTreeRoom() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Height = 620;
        host.Settle();

        FrameworkElement list = host.Body("SidebarDualPane");
        var holder = Assert.IsType<System.Windows.Controls.Grid>(System.Windows.Media.VisualTreeHelper.GetParent(list));
        Assert.True(holder.ActualHeight > 0);
        Assert.True(list.MaxHeight <= holder.ActualHeight / 2, $"the list can take {list.MaxHeight:F0}px of {holder.ActualHeight:F0}px.");
    });

    /// <summary>The graph's summary ran past half of a split pane.</summary>
    [Fact]
    public void AGridsSummaryWrapsInANarrowPane() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        var grid = new SlateWindows.Grids.AccessibleDataGrid { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
        host.Shell.Content = grid;
        host.Settle();

        var summary = Assert.IsType<System.Windows.Controls.TextBlock>(Named(grid, "AccessibleDataGridSummary"));
        Assert.Equal(TextWrapping.Wrap, summary.TextWrapping);
    });

    [Fact]
    public void AShortWindowLeavesTheFilesTreeRoom() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));

        host.Shell.Height = host.Shell.MinHeight;
        host.Settle();

        // The tree's own chrome comes out of its half.
        double tree = host.Body("FilesTree").ActualHeight;
        DependencyObject actions = host.Body("SidebarBatchActions");
        while (actions is not System.Windows.Controls.ScrollViewer)
        {
            actions = System.Windows.Media.VisualTreeHelper.GetParent(actions);
        }

        double actionsHeight = ((FrameworkElement)actions).ActualHeight;
        Assert.True(tree > 0 && tree + 8 >= actionsHeight, $"the tree is {tree:F0}px tall over {actionsHeight:F0}px of actions.");
    });

    private static FrameworkElement Named(DependencyObject root, string id) =>
        ShownShell.Descendants(root)
            .OfType<FrameworkElement>()
            .Single(element => AutomationProperties.GetAutomationId(element) == id);

    private static Rect BoxIn(FrameworkElement element, UIElement ancestor) =>
        new(element.TranslatePoint(new Point(0, 0), ancestor), new Size(element.ActualWidth, element.ActualHeight));
}
