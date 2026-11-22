// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Bases;
using SlateWindows.Canvas;
using SlateWindows.Graph;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-17b (#1866, CD11): three renders that turn controls off in code. A base's
/// reload turns its quick filter and view picker off (BaseSurfaceView.RenderChrome), the
/// Connections leaf's depth control goes off as its anchor note goes
/// (ConnectionsLeafView.RenderCore), and the graph inspector's four sections go off as the
/// graph stops being effective (GraphInspectorView.RenderGate). Each render now makes every
/// other write first, then moves the keys out of the whole set it is about to turn off, then
/// turns it off: the keys go where the guard put them after the off (the base's grid cell,
/// the rail), and no landing picks a control the same render turns off. The real window with
/// the sidebar attached; the loads run on their workers and publish through the dispatcher.
/// </summary>
public sealed class RenderKeysFirstTests
{
    private const string TwoViewBase =
        "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n    order:\n      - file.name\n  - type: table\n    name: Second\n    order:\n      - file.name\n";

    /// <summary>The keys in a base's quick filter through a reload: they leave it before it
    /// goes off, for the base's grid, and never onto the view picker the same render turns
    /// off. The keys-first witness of BaseSurfaceView's _quickFilter.</summary>
    [Fact]
    public void TheQuickFilterLetsTheKeysGoFirstThroughAReload() => RunSta(() =>
    {
        using var host = new ShownShell(("Notes.base", TwoViewBase), ("a.md", "# A\n"), ("b.md", "# B\n"));
        using var sidebar = new ShownShellSidebar(host);
        (BaseSurfaceView surface, BaseDocumentViewModel document) = OpenTheBase(host);
        TextBox quickFilter = surface.QuickFilterForTests;
        ComboBox viewPicker = surface.ViewPickerForTests;
        Assert.True(viewPicker.IsVisible && viewPicker.IsEnabled, "premise: the base shows its view picker");
        Assert.True(
            quickFilter.Focus() && ReferenceEquals(quickFilter, Keyboard.FocusedElement),
            $"keyboard premise: the quick filter could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");
        List<string> offUnderThePicker = [];

        KeysFirstRecorder.AssertTheKeysLeaveFirst("the quick filter", quickFilter, () =>
        {
            offUnderThePicker = KeysFirstRecorder.DisabledWithTheKeysInside(viewPicker, () =>
            {
                document.Load();
                Assert.True(PumpedDispatcher.PumpUntil(() => document.State == BaseLoadState.Ready, TimeSpan.FromSeconds(30)), "the reload never ended");
                host.Settle();
            });
        });

        Assert.True(offUnderThePicker.Count == 0, $"the view picker the same reload turns off took the keys and went off with them on {string.Join(", ", offUnderThePicker)}");
        Assert.True(
            surface.GridForTests.IsKeyboardFocusWithin,
            $"after the reload the keys were on {Describe(Keyboard.FocusedElement)}, not in the base's grid");
    });

    /// <summary>The keys on a base's view picker through a reload: they leave it before it goes
    /// off, for the base's grid, and never into the quick filter the same render turns off. The
    /// keys-first witness of BaseSurfaceView's _viewPicker.</summary>
    [Fact]
    public void TheViewPickerLetsTheKeysGoFirstThroughAReload() => RunSta(() =>
    {
        using var host = new ShownShell(("Notes.base", TwoViewBase), ("a.md", "# A\n"), ("b.md", "# B\n"));
        using var sidebar = new ShownShellSidebar(host);
        (BaseSurfaceView surface, BaseDocumentViewModel document) = OpenTheBase(host);
        TextBox quickFilter = surface.QuickFilterForTests;
        ComboBox viewPicker = surface.ViewPickerForTests;
        Assert.True(
            viewPicker.IsVisible && viewPicker.Focus() && ReferenceEquals(viewPicker, Keyboard.FocusedElement),
            $"keyboard premise: the view picker could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");
        List<string> offUnderTheFilter = [];

        KeysFirstRecorder.AssertTheKeysLeaveFirst("the view picker", viewPicker, () =>
        {
            offUnderTheFilter = KeysFirstRecorder.DisabledWithTheKeysInside(quickFilter, () =>
            {
                document.Load();
                Assert.True(PumpedDispatcher.PumpUntil(() => document.State == BaseLoadState.Ready, TimeSpan.FromSeconds(30)), "the reload never ended");
                host.Settle();
            });
        });

        Assert.True(offUnderTheFilter.Count == 0, $"the quick filter the same reload turns off took the keys and went off with them on {string.Join(", ", offUnderTheFilter)}");
        Assert.True(
            surface.GridForTests.IsKeyboardFocusWithin,
            $"after the reload the keys were on {Describe(Keyboard.FocusedElement)}, not in the base's grid");
    });

    /// <summary>The keys in the quick filter of a base with no rows, through a reload: with no
    /// grid or list to land on, the surface's landing passes over the quick filter the render
    /// is moving them off, for Refresh.</summary>
    [Fact]
    public void TheQuickFilterOfABaseWithNoRowsLetsTheKeysGoToRefreshFirst() => RunSta(() =>
    {
        using var host = new ShownShell(
            ("Notes.base", "filters: 'file.ext == \"zzz\"'\nviews:\n  - type: table\n    name: Main\n    order:\n      - file.name\n"),
            ("a.md", "# A\n"));
        using var sidebar = new ShownShellSidebar(host);
        host.Workspace.OpenPath("Notes.base");
        BaseDocumentViewModel document = host.Workspace.ActiveGroup.ActiveTab?.Base
            ?? throw new Xunit.Sdk.XunitException("premise: the base's tab has no document");
        Assert.True(PumpedDispatcher.PumpUntil(() => document.State == BaseLoadState.Ready, TimeSpan.FromSeconds(30)), "premise: the base never loaded");
        host.Settle();
        BaseSurfaceView surface = ShownShell.Descendants(host.Shell).OfType<BaseSurfaceView>().Single(view => view.IsVisible);
        Assert.False(surface.GridForTests.IsVisible || surface.ListForTests.IsVisible, "premise: the base shows rows");
        TextBox quickFilter = surface.QuickFilterForTests;
        Assert.True(
            quickFilter.Focus() && ReferenceEquals(quickFilter, Keyboard.FocusedElement),
            $"keyboard premise: the quick filter could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");

        KeysFirstRecorder.AssertTheKeysLeaveFirst("the quick filter of a base with no rows", quickFilter, () =>
        {
            document.Load();
            Assert.True(PumpedDispatcher.PumpUntil(() => document.State == BaseLoadState.Ready, TimeSpan.FromSeconds(30)), "the reload never ended");
            host.Settle();
        });

        Assert.True(
            ReferenceEquals(surface.RefreshForTests, Keyboard.FocusedElement),
            $"after the reload the keys were on {Describe(Keyboard.FocusedElement)}, not on Refresh");
    });

    /// <summary>The keys on the Connections leaf's depth control as its anchor note's tab
    /// closes: they leave it before it goes off, and stay out of the leaf's controls the
    /// render turns off. The keys-first witness of ConnectionsLeafView's _depth.</summary>
    [Fact]
    public void TheDepthControlLetsTheKeysGoFirstAsItsAnchorGoes() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n\nLinks to [[b]].\n"), ("b.md", "# B\n\nLinks to [[a]].\n"));
        using var sidebar = new ShownShellSidebar(host);
        ConnectionsLeafViewModel leaf = host.Workspace.Connections;
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(option => option.Id == "connections");
        host.Workspace.IsRightPaneVisible = true;
        SettleConnections(host, leaf);
        ConnectionsLeafView surface = host.Shell.ConnectionsLeafSurface;
        Assert.True(surface.IsVisible, "premise: the Connections leaf is not shown");
        Assert.Equal("a.md", leaf.Root);
        ComboBox depth = surface.DepthForTests;
        Assert.True(
            depth.IsEnabled && depth.Focus() && ReferenceEquals(depth, Keyboard.FocusedElement),
            $"keyboard premise: the depth control could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");
        List<IInputElement> changes = host.RecordFocusChanges();

        KeysFirstRecorder.AssertTheKeysLeaveFirst("the depth control", depth, () =>
        {
            host.Workspace.CloseActiveTabCommand.Execute(null);
            SettleConnections(host, leaf);
        });

        Assert.Null(leaf.Root);
        Assert.True(
            Keyboard.FocusedElement is UIElement { IsVisible: true, IsEnabled: true } and not Window,
            $"after the anchor went the keys were on {Describe(Keyboard.FocusedElement)}");
        Assert.DoesNotContain(changes, change => ReferenceEquals(change, depth));
    });

    /// <summary>The keys in each section of the graph inspector as the graph stops being
    /// effective (another tab in its pane): they leave the section before it goes off, and on
    /// the way never rest on a control of another section the same render turns off. The
    /// keys-first witness of GraphInspectorView's section.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EachInspectorSectionLetsTheKeysGoFirstAsTheGraphStopsBeingEffective(int index) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n\nLinks to [[b]].\n"), ("b.md", "# B\n\nLinks to [[a]].\n"));
        using var sidebar = new ShownShellSidebar(host);
        host.Workspace.OpenGraph();
        GraphDocumentViewModel graph = host.Workspace.GraphDocument!;
        PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
        host.Settle();
        host.Workspace.ToggleGraphInspector();
        host.Settle();
        GraphInspectorView inspector = host.Shell.GraphInspectorSurface;
        Assert.True(inspector.IsVisible, "premise: the inspector is not shown");
        IReadOnlyList<FrameworkElement> sections = inspector.SectionsForTests;
        FrameworkElement section = sections[index];
        Control stop = index switch
        {
            0 => inspector.NameQueryForTests,
            1 => inspector.AddGroupForTests,
            2 => inspector.ArrowsForTests,
            _ => inspector.SlidersForTests[3],
        };
        Assert.True(section.IsAncestorOf(stop), $"premise: the stop is not in section {AutomationProperties.GetName(section)}");
        Assert.True(
            section.IsEnabled && stop.Focus() && ReferenceEquals(stop, Keyboard.FocusedElement),
            $"keyboard premise: {AutomationProperties.GetName(section)} could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");
        List<IInputElement> changes = host.RecordFocusChanges();

        KeysFirstRecorder.AssertTheKeysLeaveFirst(AutomationProperties.GetName(section), section, () =>
        {
            host.Workspace.OpenPath("a.md");
            host.Settle();
        });

        Assert.All(sections, other => Assert.False(other.IsEnabled, $"premise: {AutomationProperties.GetName(other)} stayed on"));
        Assert.All(changes, change => Assert.False(
            change is DependencyObject element && sections.Any(other => other.IsAncestorOf(element)),
            $"the keys rested on a control of a section the graph's gate turns off: {string.Join(" -> ", changes.Select(Describe))}"));
    });

    /// <summary>The two-view base open in the real window with its first load published.</summary>
    private static (BaseSurfaceView Surface, BaseDocumentViewModel Document) OpenTheBase(ShownShell host)
    {
        host.Workspace.OpenPath("Notes.base");
        BaseDocumentViewModel document = host.Workspace.ActiveGroup.ActiveTab?.Base
            ?? throw new Xunit.Sdk.XunitException("premise: the base's tab has no document");
        Assert.True(PumpedDispatcher.PumpUntil(() => document.State == BaseLoadState.Ready, TimeSpan.FromSeconds(30)), "premise: the base never loaded");
        host.Settle();
        BaseSurfaceView surface = ShownShell.Descendants(host.Shell).OfType<BaseSurfaceView>().Single(view => view.IsVisible);
        Assert.True(surface.GridForTests.IsVisible, "premise: the base shows its grid");
        return (surface, document);
    }

    private static void SettleConnections(ShownShell host, ConnectionsLeafViewModel leaf)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            PumpedDispatcher.PumpUntilDrained(leaf.WhenAllWorkDrained());
            host.Settle();
        }
    }

    private static string Describe(object? element) => element switch
    {
        null => "nothing",
        ListBoxItem row when row.DataContext is WorkspaceLeafOption option => $"the rail's {option.Title} row",
        FrameworkElement shown => $"{shown.GetType().Name} {(shown.Name.Length > 0 ? shown.Name : AutomationProperties.GetAutomationId(shown))} \"{AutomationProperties.GetName(shown)}\"",
        _ => element.GetType().Name,
    };

    private static void RunSta(Action body)
    {
        Func<bool> priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        try
        {
            StaThread.RunPumped(body, TimeSpan.FromSeconds(120), "STA test body timed out.");
        }
        finally
        {
            CanvasSurfaceView.ShellOverlayIsOpen = priorOverlayProbe;
        }
    }
}
