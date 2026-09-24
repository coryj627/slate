// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using SlateWindows.Bases;
using SlateWindows.Canvas;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 3 (#1246, R-4; the spec review, round 23): the items hosts the
/// shell builds in C#, each taken from its own view as the view built it —
/// its container style, its template and the sibling rule it declares —
/// and given siblings that read alike. Every stop must still read apart:
/// by the distinguisher where the view declares one, else by place.
/// (The Graph inspector's pickers are hosted in GraphInspectorViewTests,
/// inside the inspector that builds them.)
/// </summary>
public sealed class CodeBuiltSiblingTests
{
    private static BasesRow Row(string path, string readback, ulong? task = null) =>
        new(path, task, [], readback);

    /// <summary>A dashboard section's "list" override: two notes that read
    /// alike add their files; two tasks of one note, their places; a row
    /// no sibling shares reads bare.</summary>
    [Fact]
    public void ADashboardSectionListTellsRowsThatReadAlikeApart() => RunSta(() =>
    {
        var result = new BasesResultSet(
            [],
            [
                Row("A/note.md", "note.md, status open"),
                Row("B/note.md", "note.md, status open"),
                Row("C/tasks.md", "Call back, due Friday", task: 1),
                Row("C/tasks.md", "Call back, due Friday", task: 2),
                Row("D/solo.md", "solo.md"),
            ],
            [], [], 5, 5, 5, 0, [], null, "5 rows");
        ListBox list = DashboardSurfaceView.BuildSectionList("Dashboard", 0, result);
        Hosted(list, () => Assert.Equal(
            [
                "note.md, status open, A/note.md",
                "note.md, status open, B/note.md",
                "Call back, due Friday, row 3",
                "Call back, due Friday, row 4",
                "solo.md",
            ],
            ItemContainerNameBindingTests.ItemNames(list)));
    });

    /// <summary>The Base tab's list renderer, through its own render path:
    /// rows that read alike add their files, else their places.</summary>
    [Fact]
    public void TheBaseListTellsRowsThatReadAlikeApart() => RunSta(() =>
    {
        var surface = new BaseSurfaceView();
        surface.RenderListForTests(new BasesResultSet(
            [],
            [
                Row("A/note.md", "note.md"),
                Row("B/note.md", "note.md"),
                Row("C/tasks.md", "Call back", task: 1),
                Row("C/tasks.md", "Call back", task: 2),
            ],
            [], [], 4, 4, 4, 0, [], null, "4 rows"));
        ListBox list = Detach(surface.ListForTests);
        Hosted(list, () => Assert.Equal(
            ["note.md, A/note.md", "note.md, B/note.md", "Call back, row 3", "Call back, row 4"],
            ItemContainerNameBindingTests.ItemNames(list)));
    });

    /// <summary>A base may name two views alike (core warns
    /// DuplicateViewName): the view picker reads them by place.</summary>
    [Fact]
    public void TheBaseViewPickerTellsViewsNamedAlikeApart() => RunSta(() =>
    {
        ComboBox picker = Detach(new BaseSurfaceView().ViewPickerForTests);
        picker.ItemsSource = new[]
        {
            new BaseViewSummary("Open tasks", "table", "tasks", BaseViewStatus.Executable, null),
            new BaseViewSummary("Open tasks", "list", "tasks", BaseViewStatus.Executable, null),
            new BaseViewSummary("Archive", "table", "archive", BaseViewStatus.Executable, null),
        };
        Hosted(picker, () => Assert.Equal(
            ["Open tasks, view 1", "Open tasks, view 2", "Archive"],
            ItemContainerNameBindingTests.ItemNames(picker)));
    });

    /// <summary>Two equal warnings are two focusable texts, each read apart
    /// — not one peer between them (SiblingText).</summary>
    [Fact]
    public void TheBaseWarningBannersTellEqualWarningsApart() => RunSta(() =>
    {
        LayoutItemsControl banners = Detach(new BaseSurfaceView().WarningBannersForTests);
        banners.ItemsSource = SiblingText.Wrap(["Unknown property: status", "Unknown property: status", "Empty filter"]);
        Hosted(banners, () => Assert.Equal(
            ["Unknown property: status, warning 1", "Unknown property: status, warning 2", "Empty filter"],
            WrappedTexts(banners)));
    });

    /// <summary>The canvas outline's warning rows: two equal warnings are
    /// two rows, read apart.</summary>
    [Fact]
    public void TheCanvasWarningRowsTellEqualWarningsApart() => RunSta(() =>
    {
        ListBox rows = Detach(new CanvasSurfaceView().WarningRowsForTests);
        rows.ItemsSource = SiblingText.Wrap(["Skipped a node of unknown type", "Skipped a node of unknown type", "A connection points nowhere"]);
        Hosted(rows, () => Assert.Equal(
            ["Skipped a node of unknown type, warning 1", "Skipped a node of unknown type, warning 2", "A connection points nowhere"],
            ItemContainerNameBindingTests.ItemNames(rows)));
    });

    /// <summary>The Connections tree: two rows that read alike, told apart
    /// by place at their level.</summary>
    [Fact]
    public void TheConnectionsTreeTellsRowsThatReadAlikeApart() => RunSta(() =>
    {
        TreeView tree = Detach(new ConnectionsLeafView().TreeForTests);
        tree.ItemsSource = new[]
        {
            ConnectionsRowViewModel.ForEmpty("empty-1", "No links yet"),
            ConnectionsRowViewModel.ForEmpty("empty-2", "No links yet"),
            ConnectionsRowViewModel.ForEmpty("empty-3", "Nothing embedded"),
        };
        Hosted(tree, () => Assert.Equal(
            ["No links yet, item 1", "No links yet, item 2", "Nothing embedded"],
            ItemContainerNameBindingTests.ItemNames(tree)));
    });

    /// <summary>The name of every control-view element a layout host's
    /// containers hold — its stops: the containers themselves are out of
    /// the control view (LayoutItemAutomationPeer).</summary>
    private static string[] WrappedTexts(ItemsControl host)
    {
        host.UpdateLayout();
        PumpedDispatcher.Drain();
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(host);
        peer.ResetChildrenCache();
        ItemAutomationPeer[] containers = [.. (peer.GetChildren() ?? []).OfType<ItemAutomationPeer>()];
        Assert.All(containers, container => Assert.False(container.IsControlElement()));
        return
        [
            .. containers
                .SelectMany(container => container.GetChildren() ?? [])
                .Where(child => child.IsControlElement())
                .Select(child => child.GetName()),
        ];
    }

    /// <summary>The host out of its view — its own configuration intact —
    /// and visible, to be hosted alone.</summary>
    private static T Detach<T>(T element)
        where T : FrameworkElement
    {
        switch (element.Parent)
        {
            case Panel panel:
                panel.Children.Remove(element);
                break;
            case Decorator decorator:
                decorator.Child = null;
                break;
            case ContentControl content:
                content.Content = null;
                break;
            case null:
                break;
            default:
                throw new InvalidOperationException($"cannot detach a {element.GetType().Name} from a {element.Parent.GetType().Name}");
        }
        element.Visibility = Visibility.Visible;
        return element;
    }

    private static void Hosted(FrameworkElement host, Action body) =>
        ItemContainerNameBindingTests.Hosted(host, body);

    private static void RunSta(Action body) => ItemContainerNameBindingTests.RunSta(body);
}
