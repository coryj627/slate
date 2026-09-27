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
                "Call back, due Friday, C/tasks.md, row 3",
                "Call back, due Friday, C/tasks.md, row 4",
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
            ["note.md, A/note.md", "note.md, B/note.md", "Call back, C/tasks.md, row 3", "Call back, C/tasks.md, row 4"],
            ItemContainerNameBindingTests.ItemNames(list)));
    });

    /// <summary>A base may name two views alike (core warns
    /// DuplicateViewName): the view picker reads them by place.</summary>
    [Fact]
    public void TheBaseViewPickerTellsViewsNamedAlikeApart() => RunSta(() =>
    {
        ComboBox picker = Detach(new BaseSurfaceView().ViewPickerForTests);
        picker.ItemsSource = ItemOccurrence.Of(
            [
                new BaseViewSummary("Open tasks", "table", "tasks", BaseViewStatus.Executable, null),
                new BaseViewSummary("Open tasks", "list", "tasks", BaseViewStatus.Executable, null),
                new BaseViewSummary("Archive", "table", "archive", BaseViewStatus.Executable, null),
            ],
            view => view.Name);
        Hosted(picker, () => Assert.Equal(
            ["Open tasks, view 1", "Open tasks, view 2", "Archive"],
            ItemContainerNameBindingTests.ItemNames(picker)));
    });

    /// <summary>Codex PR 3 rounds 4 and 6, owner decision OD-9 — occurrence
    /// identity, through the production render: a base keeps duplicate view
    /// definitions (core warns DuplicateViewName), so two views are fully
    /// value-EQUAL records, and WPF keys an items host's peers by equality —
    /// the picker showed two views while UIA held ONE item peer, named as the
    /// second. Each view is its own occurrence: the open picker's own peer
    /// holds two items, "Open tasks, view 1" and "Open tasks, view 2", and the
    /// closed picker names its selection — the second — as the second. Codex
    /// PR 3 round 7: so does the switch's announcement, and Where Am I — both
    /// spoke the bare "Open tasks" for either.</summary>
    [Fact]
    public void TwoEqualBaseViewsAreTwoUiaItemsReadApart() => RunSta(() => WithBaseViews(
        """
          - type: table
            name: Open tasks
            order:
              - file.name
          - type: table
            name: Open tasks
            order:
              - file.name
          - type: table
            name: Archive
            order:
              - file.name
        """,
        (document, picker, announced) =>
        {
            // The premise: two distinct records, equal by value.
            Assert.Equal(document.Views[0], document.Views[1]);
            Assert.NotSame(document.Views[0], document.Views[1]);
            ItemAutomationPeer[] items = OpenPickerItems(picker);
            Assert.True(
                items.Length == 3,
                $"{items.Length} item peers for three views: {string.Join(" | ", items.Select(item => item.GetName()))}");
            Assert.Equal(
                ["Open tasks, view 1", "Open tasks, view 2", "Archive"],
                items.Select(item => item.GetName()));

            picker.IsDropDownOpen = false;
            announced.Clear();
            picker.SelectedIndex = 1;
            PumpedDispatcher.Drain();
            Assert.Equal(1, document.ActiveViewIndex);
            Assert.Equal("Open tasks, view 2", ClosedSelectionName(picker));
            Assert.Equal(
                "Open tasks, view 2",
                Assert.Single(announced.OfType<A11yEvent.BasesViewSelected>()).Name);
            Assert.Equal(
                "Open tasks, view 2",
                Assert.IsType<A11yEvent.BaseWhereAmI>(document.WhereAmIEvent()).View);
        }));

    /// <summary>...and the view commands' switch speaks it too (codex PR 3
    /// round 7): Next View from the first "Open tasks" names the second as
    /// the picker does.</summary>
    [Fact]
    public void TheViewCommandsSpeakADuplicateViewAsThePickerNamesIt()
    {
        using FixtureVault vault = FixtureVault.Create(1, "base-view-commands");
        File.WriteAllText(
            Path.Combine(vault.Root, "Views.base"),
            "filters: 'file.ext == \"md\"'\nviews:\n"
            + "  - type: table\n    name: Open tasks\n"
            + "  - type: table\n    name: Open tasks\n"
            + "  - type: table\n    name: Archive\n");
        using VaultSession session = VaultSession.OpenFilesystem(vault.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            session, vault.Root, () => [], announced.Add, startInteractionBackgroundWork: false);
        workspace.OpenPath("Views.base");
        BaseDocumentViewModel document = Assert.IsType<BaseDocumentViewModel>(workspace.ActiveBaseDocument);
        Assert.Equal(0, document.ActiveViewIndex);

        workspace.BasesNextViewCommand.Execute(null);
        workspace.BasesNextViewCommand.Execute(null);
        workspace.BasesPreviousViewCommand.Execute(null);
        Assert.Equal(
            ["Open tasks, view 2", "Archive", "Open tasks, view 2"],
            announced.OfType<A11yEvent.BasesViewSelected>().Select(item => item.Name));
    }

    /// <summary>Codex PR 3 round 6, OD-9 — speech identity, through the
    /// production render: core keeps a quoted view name verbatim, so views
    /// named "Open tasks", "Open tasks ", " Open  tasks", "Open tasks." and
    /// "Open-tasks" all reach the picker, and a reader hears them alike —
    /// each takes its place, and each keeps its own spelling.</summary>
    [Fact]
    public void BaseViewsThatReadAlikeAreToldApartVerbatim() => RunSta(() => WithBaseViews(
        """
          - type: table
            name: "Open tasks"
          - type: table
            name: "Open tasks "
          - type: table
            name: " Open  tasks"
          - type: table
            name: "Open tasks."
          - type: table
            name: "Open-tasks"
          - type: table
            name: "Archive"
        """,
        (document, picker, _) =>
        {
            Assert.Equal(
                ["Open tasks", "Open tasks ", " Open  tasks", "Open tasks.", "Open-tasks", "Archive"],
                document.Views.Select(view => view.Name));
            Assert.Equal(
                [
                    "Open tasks, view 1", "Open tasks , view 2", " Open  tasks, view 3", "Open tasks., view 4",
                    "Open-tasks, view 5", "Archive",
                ],
                OpenPickerItems(picker).Select(item => item.GetName()));
        }));

    /// <summary>A base over one note whose views are <paramref name="views"/>
    /// (YAML list items), loaded, rendered by a real BaseSurfaceView and
    /// shown; <paramref name="body"/> reads its model, its view picker and
    /// what it announced.</summary>
    private static void WithBaseViews(
        string views, Action<BaseDocumentViewModel, ComboBox, List<A11yEvent>> body)
    {
        using FixtureVault vault = FixtureVault.Create(1, "base-view-picker");
        File.WriteAllText(
            Path.Combine(vault.Root, "Views.base"),
            "filters: 'file.ext == \"md\"'\nviews:\n" + views.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
        using VaultSession session = VaultSession.OpenFilesystem(vault.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        var announced = new List<A11yEvent>();
        var document = new BaseDocumentViewModel(session, "Views.base", announced.Add, synchronousForTests: true);
        document.Load();
        var surface = new BaseSurfaceView { Model = document };
        var window = new Window
        {
            Content = surface,
            Width = 800,
            Height = 480,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            PumpedDispatcher.Drain();
            body(document, surface.ViewPickerForTests, announced);
        }
        finally
        {
            window.Close();
            document.Shutdown();
        }
    }

    /// <summary>The item peers the open picker's own peer holds — WPF's
    /// item-peer cache included, never peers built by hand.</summary>
    private static ItemAutomationPeer[] OpenPickerItems(ComboBox picker)
    {
        picker.IsDropDownOpen = true;
        picker.UpdateLayout();
        PumpedDispatcher.Drain();
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(picker);
        peer.ResetChildrenCache();
        return [.. (peer.GetChildren() ?? []).OfType<ItemAutomationPeer>()];
    }

    /// <summary>The name UIA gives a closed combo's selection: an item peer
    /// the combo's own peer makes for it, through its throwaway
    /// wrapper.</summary>
    private static string ClosedSelectionName(ComboBox picker)
    {
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(picker);
        System.Reflection.MethodInfo create = typeof(ItemsControlAutomationPeer).GetMethod(
            "CreateItemAutomationPeer",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return ((ItemAutomationPeer)create.Invoke(peer, [picker.SelectedItem])!).GetName();
    }

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
