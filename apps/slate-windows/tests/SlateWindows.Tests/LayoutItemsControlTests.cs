// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 3 (#1246, contract R-4): a container that only WRAPS the
/// real stop is layout. WPF gives every item of a plain ItemsControl a
/// DataItem peer named by the item's <c>ToString()</c>, so NVDA spoke
/// "RecentVault { Path = …, LastOpenedMs = … }, data item" and
/// "SlateWindows.WorkspacePaneNodeViewModel, data item, 1 of 2" on the
/// way to the button or editor the reader actually landed on (record F3).
/// </summary>
public sealed class LayoutItemsControlTests
{
    private sealed record Row(string Label);

    /// <summary>The host is ONE group carrying its name; each container
    /// peer answers IsControlElement/IsContentElement false (NVDA treats
    /// such an element as layout and never speaks it) and carries no name,
    /// so not even a raw-view client can read the item's ToString; the
    /// wrapped control is the stop. The host itself is never a stop.</summary>
    [Fact]
    public void LayoutContainersLeaveTheControlViewAndTheHostIsANamedGroup() => RunSta(() =>
    {
        var button = new FrameworkElementFactory(typeof(Button));
        button.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(Row.Label)));
        var host = new LayoutItemsControl
        {
            ItemsSource = new[] { new Row("Alpha"), new Row("Beta") },
            ItemTemplate = new DataTemplate { VisualTree = button },
        };
        AutomationProperties.SetName(host, "Recent vaults");
        var window = new Window
        {
            Content = host,
            Width = 300,
            Height = 200,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            AutomationPeer hostPeer = Assert.IsType<LayoutItemsControlAutomationPeer>(
                UIElementAutomationPeer.CreatePeerForElement(host));
            Assert.Equal(AutomationControlType.Group, hostPeer.GetAutomationControlType());
            Assert.Equal("Recent vaults", hostPeer.GetName());
            Assert.True(hostPeer.IsControlElement());
            Assert.False(host.Focusable);

            List<AutomationPeer> containers = hostPeer.GetChildren();
            Assert.Equal(2, containers.Count);
            foreach (AutomationPeer container in containers)
            {
                Assert.IsType<LayoutItemAutomationPeer>(container);
                Assert.False(container.IsControlElement());
                Assert.False(container.IsContentElement());
                Assert.Equal(string.Empty, container.GetName());
                AutomationPeer stop = Assert.Single(container.GetChildren());
                Assert.Equal(AutomationControlType.Button, stop.GetAutomationControlType());
                Assert.True(stop.IsControlElement());
            }
            Assert.Equal(
                ["Alpha", "Beta"],
                containers.Select(container => container.GetChildren()[0].GetName()));

            host.Visibility = Visibility.Collapsed;
            Assert.False(hostPeer.IsControlElement());
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>Spec §4.2 item 2 (codex PR 3 round 4): a host that only
    /// wraps the real stops is never named and never a control element. The
    /// split host arranges the panes and nothing more, so a split adds no
    /// named object-navigation element: the host is unnamed and out of the
    /// control and content views, visible or not, and its containers are
    /// layout. The announcer already says "Editor pane 1 of 2, {title}." on
    /// pane moves (W7-6), so nothing positional is lost.</summary>
    [Fact]
    public void TheSplitHostIsUnnamedStructure() => RunSta(() =>
    {
        var resources = Assert.IsType<ResourceDictionary>(Application.LoadComponent(
            new Uri("/SlateWindows;component/WorkspaceTemplates.xaml", UriKind.Relative)));
        DataTemplate template = Assert.IsType<DataTemplate>(resources["WorkspaceNodeTemplate"]);
        Grid root = Assert.IsType<Grid>(template.LoadContent());
        LayoutItemsControl host = Assert.Single(root.Children.OfType<LayoutItemsControl>());
        Assert.Equal(string.Empty, AutomationProperties.GetName(host));
        Assert.True(host.IsStructural);
        host.ItemsSource = new[] { "left", "right" };
        var window = new Window
        {
            Content = root,
            Width = 480,
            Height = 320,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.True(host.IsVisible);
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(host);
            Assert.Equal(string.Empty, peer.GetName());
            Assert.False(peer.IsControlElement());
            Assert.False(peer.IsContentElement());
            Assert.All(peer.GetChildren() ?? [], container => Assert.False(container.IsControlElement()));
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>With the split containers out of the control view, each
    /// pane's content would surface directly under the split — two
    /// focusable "Workspace tabs" siblings, which axe fails
    /// (SiblingUniqueAndFocusable, measured on the shell journey's
    /// workspace scan). Each pane is therefore one structural Pane: axe's
    /// sibling rules exclude Panes and NVDA does not speak an unnamed one,
    /// so the pane stays as silent as the spec wants it (W7-6's announcer
    /// speaks "Editor pane 1 of 2, {title}."). It is UNNAMED because a
    /// Pane named "Editor pane" fails axe's NameExcludesControlType
    /// (measured); that name sat on a plain Border, which creates no peer,
    /// so it had never reached UIA (the W4-5 class).</summary>
    [Fact]
    public void EachPaneIsOneSilentStructuralPane() => RunSta(() =>
    {
        var resources = Assert.IsType<ResourceDictionary>(Application.LoadComponent(
            new Uri("/SlateWindows;component/WorkspaceTemplates.xaml", UriKind.Relative)));
        DataTemplate template = Assert.IsType<DataTemplate>(resources["WorkspaceGroupTemplate"]);
        AutomationLandmarkBorder pane = Assert.IsType<AutomationLandmarkBorder>(template.LoadContent());
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(pane);
        Assert.Equal(AutomationControlType.Pane, peer.GetAutomationControlType());
        Assert.Equal(string.Empty, peer.GetName());
        // #1774: the Grid holds the tab control and the empty workspace's message.
        // A Grid has no peer, so the pane is still the one stop above the tabs.
        Assert.Single(Assert.IsType<Grid>(pane.Child).Children.OfType<TabControl>());
    });

    /// <summary>Codex PR 3 round 1: the Bases warning banners are built in
    /// C#, and as a plain ItemsControl each warning's focusable text sat
    /// under a DataItem wrapper. The host is layout now: the generated
    /// container is out of the control view and the warning text is the
    /// one stop.</summary>
    [Fact]
    public void TheBasesWarningBannersAreALayoutHostOverTheirFocusableText() => RunSta(() =>
    {
        var surface = new Bases.BaseSurfaceView();
        LayoutItemsControl banners = surface.WarningBannersForTests;
        banners.ItemsSource = SiblingText.Wrap(["Unknown property: status"]);
        banners.Visibility = Visibility.Visible;
        ((FrameworkElement)banners.Parent).Visibility = Visibility.Visible;
        var window = new Window
        {
            Content = surface,
            Width = 480,
            Height = 320,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            AutomationPeer container = Assert.Single(
                UIElementAutomationPeer.CreatePeerForElement(banners).GetChildren());
            Assert.IsType<LayoutItemAutomationPeer>(container);
            Assert.False(container.IsControlElement());
            Assert.False(container.IsContentElement());
            AutomationPeer text = Assert.Single(container.GetChildren());
            Assert.True(text.IsControlElement());
            Assert.Equal("Unknown property: status", text.GetName());
            Assert.True(((FrameworkElementAutomationPeer)text).Owner.Focusable);
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>#1677 (Codex PR #1861 round 2): an equal item rebuilt keeps its item peer
    /// and gets a new container at once, before layout applies the container's template. A
    /// client reading the item then reads no children. After layout, the live button is in
    /// the tree: an early read isn't kept just because the container is the same.</summary>
    [Fact]
    public void AnItemReadBeforeLayoutShowsItsButtonAfterLayout() => RunSta(() =>
    {
        var button = new FrameworkElementFactory(typeof(Button));
        button.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(Row.Label)));
        var rows = new System.Collections.ObjectModel.ObservableCollection<Row> { new("Alpha") };
        var host = new LayoutItemsControl
        {
            ItemsSource = rows,
            ItemTemplate = new DataTemplate { VisualTree = button },
        };
        var window = new Window
        {
            Content = host,
            Width = 300,
            Height = 200,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        window.Show();
        try
        {
            Drain();
            AutomationPeer hostPeer = UIElementAutomationPeer.CreatePeerForElement(host);
            AutomationPeer item = Assert.Single(hostPeer.GetChildren());
            Assert.Single(item.GetChildren());

            rows.Clear();
            rows.Add(new Row("Alpha"));
            DependencyObject container = host.ItemContainerGenerator.ContainerFromItem(rows[0]);
            Assert.False(
                container is null || ((UIElement)container).IsArrangeValid,
                "premise: the new container isn't waiting for layout");
            hostPeer.ResetChildrenCache();
            AutomationPeer early = Assert.Single(hostPeer.GetChildren());
            Assert.Same(item, early);
            early.ResetChildrenCache();
            Assert.Empty(early.GetChildren() ?? []);

            Drain();
            Button live = Assert.IsType<Button>(
                System.Windows.Media.VisualTreeHelper.GetChild(container, 0));
            AutomationPeer livePeer = UIElementAutomationPeer.CreatePeerForElement(live);
            Assert.Contains(
                livePeer,
                (hostPeer.GetChildren() ?? []).SelectMany(child => child.GetChildren() ?? []));
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>#1860: a removal, a replace and a move generate no containers, so the
    /// host's reset on ContainersGenerated never ran for them, and nothing else rebuilt the
    /// host peer's children until a client had connected the window. A host read in process
    /// kept a removed item's peer (an empty element where the item was), the old item of a
    /// replace and the old order of a move. Once the reset queued for the change has run,
    /// the host lists exactly its items' peers, in order, and an item the change kept keeps
    /// its peer (in the refill and the swap, WPF hands the peer of an equal item to the new
    /// record). Clear, a refill, a swapped source and an Add reset through ContainersGenerated
    /// already; they stay as guards. The host is in no window, so no client can connect
    /// it.</summary>
    [Theory]
    [InlineData("remove")]
    [InlineData("replace")]
    [InlineData("move")]
    [InlineData("burst")]
    [InlineData("clear")]
    [InlineData("refill")]
    [InlineData("swap")]
    [InlineData("add")]
    [InlineData("collapsed remove")]
    public void AChangedSourceLeavesNoStalePeerOnTheHost(string change) => RunSta(() =>
    {
        ObservableCollection<Row> rows = change == "burst" ? HundredAndFiveRows() : FiveRows();
        LayoutItemsControl host = LaidOutAlone(rows);
        AutomationPeer hostPeer = UIElementAutomationPeer.CreatePeerForElement(host);
        Dictionary<string, AutomationPeer> before = ReadLikeAClient(hostPeer);

        switch (change)
        {
            case "remove":
                rows.Remove(rows[2]);
                break;
            case "replace":
                rows[2] = new Row("C2");
                break;
            case "move":
                rows.Move(0, 4);
                break;
            case "burst":
                RemoveAHundred(rows);
                break;
            case "clear":
                rows.Clear();
                break;
            case "refill":
                Refill(rows);
                break;
            case "swap":
                host.ItemsSource = new List<Row> { new("A"), new("B"), new("Z") };
                break;
            case "add":
                rows.Add(new Row("F"));
                break;
            case "collapsed remove":
                host.Visibility = Visibility.Collapsed;
                host.UpdateLayout();
                rows.Remove(rows[2]);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change), change, "no such change");
        }
        Drain();

        Row[] items = [.. host.Items.Cast<Row>()];
        List<AutomationPeer> children = hostPeer.GetChildren() ?? [];
        Assert.Equal(string.Join(" | ", items.Select(row => row.Label)), string.Join(" | ", children.Select(LabelOf)));
        for (int index = 0; index < items.Length; index++)
        {
            ItemAutomationPeer peer = Assert.IsAssignableFrom<ItemAutomationPeer>(children[index]);
            Assert.Same(items[index], peer.Item);
            if (before.TryGetValue(items[index].Label, out AutomationPeer? kept))
            {
                Assert.Same(kept, peer);
            }
        }
    });

    /// <summary>#1860, the issue's schedule: the host's peer is read in process while no
    /// UI Automation client has connected the window, a row is removed, and a client then
    /// connects. It saw A, B, an empty SlateLayoutItem with an empty rectangle and no
    /// children, D and E in the raw view (the control view was right). Premise: no client
    /// has connected the window before ours, since a connected window has WPF rebuild the
    /// host's children after each layout and would hide the stale peer without the reset.
    /// A desktop UI Automation client connects it by either of the two routes
    /// AccessibilityNotificationDispatcherTests records: handlers it advised into this process
    /// for an earlier window leave a listener in WPF's event map, and WPF then connects every
    /// new window's root itself as it shows; or it sends the new window WM_GETOBJECT as it is
    /// created. On either route this schedule cannot occur, on main or with the fix, so the
    /// fact skips and names the route: it reads the event map before the window shows, and
    /// counts WM_GETOBJECT with a thread hook installed before the window exists. A root
    /// connected by neither route can come only from code in this process, and fails the
    /// fact. Wherever this fact skips, facts 1 and 3 (AChangedSourceLeavesNoStalePeerOnTheHost
    /// and ABurstOfRemovalsQueuesOneReset), whose hosts are in no window, still carry the
    /// proof.</summary>
    [Fact]
    public void AClientThatConnectsAfterARemovalSeesNoExtraElement() => RunSta(() =>
    {
        string listening = ListeningEvents();
        if (listening.Length > 0)
        {
            Assert.Skip($"WPF's event map in this process already has a listener ({listening}), left by a UI Automation "
                + "client that advised an earlier window, so WPF connects every new window's root itself and #1860's "
                + "schedule, a client that connects only after the removal, cannot occur here; facts 1 and 3, hosts in no "
                + "window, carry the proof");
        }

        using var requests = new AccessibilityNotificationDispatcherTests.RootRequests();
        ObservableCollection<Row> rows = FiveRows();
        LayoutItemsControl host = ButtonRows(rows);
        AutomationProperties.SetAutomationId(host, ClientHostId);
        AutomationProperties.SetName(host, "Rows");
        var window = new Window
        {
            Content = host,
            Width = 320,
            Height = 400,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        try
        {
            nint handle = new WindowInteropHelper(window).Handle;
            Drain();
            _ = ReadLikeAClient(UIElementAutomationPeer.CreatePeerForElement(host));
            SkipWhereAClientConnected(requests, handle, "the removal");

            rows.Remove(rows[2]);
            Drain();
            SkipWhereAClientConnected(requests, handle, "our client");

            Task<ClientView> read = Task.Run(() => ReadAsAClient(handle));
            PumpedDispatcher.PumpUntilDrained(read, TimeSpan.FromSeconds(30));
            ClientView view = read.Result;
            Assert.Equal(
                "SlateLayoutItem[Button A] | SlateLayoutItem[Button B] | SlateLayoutItem[Button D] | SlateLayoutItem[Button E]",
                string.Join(" | ", view.Raw));
            Assert.Equal("Button A | Button B | Button D | Button E", string.Join(" | ", view.Control));
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>#1860: one reset serves a burst. A hundred removals queue one reset on a host
    /// whose peer a client read, not a hundred, and a list cleared and refilled (one
    /// ContainersGenerated per change) queues one, not six. A host no client has read has
    /// nothing cached and posts none, so the twin, whose peer was never made, counts every
    /// Loaded post the same change makes besides the host's own: none, for hosts in no window.
    /// The raw counts are asserted, host and twin, so a red run shows which defect it saw
    /// (main posts none for the removals and six for each refill, the twin's too).</summary>
    [Fact]
    public void ABurstOfRemovalsQueuesOneReset() => RunSta(() =>
    {
        ObservableCollection<Row> hostRows = HundredAndFiveRows();
        ObservableCollection<Row> twinRows = HundredAndFiveRows();
        LayoutItemsControl host = LaidOutAlone(hostRows);
        LayoutItemsControl twin = LaidOutAlone(twinRows);
        _ = ReadLikeAClient(UIElementAutomationPeer.CreatePeerForElement(host));
        Assert.True(UIElementAutomationPeer.FromElement(twin) is null, "premise: the twin's peer was made");

        int hostRemovals = LoadedPostsDuring(() => RemoveAHundred(hostRows));
        int twinRemovals = LoadedPostsDuring(() => RemoveAHundred(twinRows));
        Drain();
        int hostRefill = LoadedPostsDuring(() => Refill(hostRows));
        int twinRefill = LoadedPostsDuring(() => Refill(twinRows));

        Assert.Equal((1, 0, 1, 0), (hostRemovals, twinRemovals, hostRefill, twinRefill));
    });

    private const string ClientHostId = "Cb25ClientHost";

    /// <summary>What a UI Automation client saw under the host: each raw child as its class
    /// name and its raw children, and the control view's children.</summary>
    private sealed record ClientView(IReadOnlyList<string> Raw, IReadOnlyList<string> Control);

    /// <summary>A client on another thread, through UI Automation's core, as a reader
    /// connects. An element with an empty rectangle says so.</summary>
    private static ClientView ReadAsAClient(nint handle)
    {
        AutomationElement window = AutomationElement.FromHandle(handle);
        AutomationElement host = window.FindFirst(
            TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, ClientHostId))
            ?? throw new InvalidOperationException("the client found no host");
        static string Element(AutomationElement element) =>
            element.Current.ControlType.ProgrammaticName.Replace("ControlType.", string.Empty, StringComparison.Ordinal)
            + " " + element.Current.Name + EmptyRectangle(element);
        static string EmptyRectangle(AutomationElement element) =>
            element.Current.BoundingRectangle.IsEmpty ? " (empty rectangle)" : string.Empty;
        List<string> raw = [];
        foreach (AutomationElement item in Children(TreeWalker.RawViewWalker, host))
        {
            raw.Add(item.Current.ClassName + EmptyRectangle(item)
                + "[" + string.Join(", ", Children(TreeWalker.RawViewWalker, item).Select(Element)) + "]");
        }
        return new ClientView(raw, [.. Children(TreeWalker.ControlViewWalker, host).Select(Element)]);
    }

    private static IEnumerable<AutomationElement> Children(TreeWalker walker, AutomationElement parent)
    {
        for (AutomationElement? child = walker.GetFirstChild(parent); child is not null; child = walker.GetNextSibling(child))
        {
            yield return child;
        }
    }

    /// <summary>The windows on this thread a UI Automation client has connected (WPF's
    /// ContextLayoutManager.AutomationEvents). WPF rebuilds a connected window's peers after
    /// each layout, so a fact that needs a stale cache on main asserts this is empty.</summary>
    private static int ConnectedRoots()
    {
        const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        Type manager = typeof(UIElement).Assembly.GetType("System.Windows.ContextLayoutManager", throwOnError: true)!;
        object layout = manager.GetMethod("From", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [Dispatcher.CurrentDispatcher])!;
        object roots = manager.GetProperty("AutomationEvents", Any)!.GetValue(layout)!;
        return (int)roots.GetType().GetProperty("Count", Any)!.GetValue(roots)!;
    }

    /// <summary>The automation events WPF's process-wide event map has a listener for, joined
    /// (read through the public per-event probe); empty when none. A listener is a UI
    /// Automation client's handler advised into this process.</summary>
    private static string ListeningEvents() =>
        string.Join("+", Enum.GetValues<AutomationEvents>().Where(AutomationPeer.ListenerExists));

    /// <summary>Fact 2's premise: no client connected the window's automation root before
    /// <paramref name="what"/>. A desktop client connects it by one of two routes, and on
    /// either #1860's schedule cannot occur, so the fact skips and names the route: a
    /// WM_GETOBJECT sent to the window (counted from before the window existed), or a
    /// listener a client advised into WPF's event map while the window showed. A root
    /// connected with neither can come only from code in this process, and fails the
    /// fact.</summary>
    private static void SkipWhereAClientConnected(
        AccessibilityNotificationDispatcherTests.RootRequests requests, nint window, string what)
    {
        if (ConnectedRoots() == 0)
        {
            return;
        }

        int asked = requests.To(window);
        if (asked > 0)
        {
            Assert.Skip($"a desktop UI Automation client asked the window for its automation root ({asked} WM_GETOBJECT) "
                + $"before {what}, so WPF connected it and #1860's schedule, a client that connects only after the removal, "
                + "did not occur here; facts 1 and 3, hosts in no window, carry the proof");
        }

        if (ListeningEvents() is { Length: > 0 } listening)
        {
            Assert.Skip($"WPF's event map in this process gained a listener ({listening}) while the window showed, left by "
                + $"a UI Automation client, so WPF connected the window's root itself before {what} and #1860's schedule "
                + "did not occur here; facts 1 and 3, hosts in no window, carry the proof");
        }

        Assert.Fail($"the window's automation root was connected before {what} with no WM_GETOBJECT to it and no listener "
            + "in WPF's event map: neither client route explains it, so code in this process connected it");
    }

    /// <summary>The operations posted at Loaded priority while <paramref name="change"/>
    /// runs.</summary>
    private static int LoadedPostsDuring(Action change)
    {
        int posted = 0;
        DispatcherHooks hooks = Dispatcher.CurrentDispatcher.Hooks;
        void Posted(object? sender, DispatcherHookEventArgs args)
        {
            if (args.Operation.Priority == DispatcherPriority.Loaded)
            {
                posted++;
            }
        }
        hooks.OperationPosted += Posted;
        try
        {
            change();
        }
        finally
        {
            hooks.OperationPosted -= Posted;
        }
        return posted;
    }

    private static ObservableCollection<Row> FiveRows() => [new("A"), new("B"), new("C"), new("D"), new("E")];

    private static ObservableCollection<Row> HundredAndFiveRows() =>
        [.. Enumerable.Range(0, 105).Select(index => new Row($"R{index:000}"))];

    private static void RemoveAHundred(ObservableCollection<Row> rows)
    {
        for (int removed = 0; removed < 100; removed++)
        {
            rows.RemoveAt(0);
        }
    }

    /// <summary>Clear, then five Adds of records equal to <see cref="FiveRows"/>.</summary>
    private static void Refill(ObservableCollection<Row> rows)
    {
        rows.Clear();
        foreach (Row row in FiveRows())
        {
            rows.Add(row);
        }
    }

    /// <summary>A host of one Button per row, named by the row's label (fact 5's
    /// shape).</summary>
    private static LayoutItemsControl ButtonRows(ObservableCollection<Row> rows)
    {
        var button = new FrameworkElementFactory(typeof(Button));
        button.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(Row.Label)));
        return new LayoutItemsControl
        {
            ItemsSource = rows,
            ItemTemplate = new DataTemplate { VisualTree = button },
        };
    }

    /// <summary><see cref="ButtonRows"/> laid out in no window. A root built in code takes
    /// its theme template, and so its items panel, only once initialized.</summary>
    private static LayoutItemsControl LaidOutAlone(ObservableCollection<Row> rows)
    {
        LayoutItemsControl host = ButtonRows(rows);
        host.BeginInit();
        host.EndInit();
        host.Measure(new Size(320, 400));
        host.Arrange(new Rect(0, 0, 320, 400));
        host.UpdateLayout();
        Drain();
        return host;
    }

    /// <summary>Reads the host's children and each item's children once, as a client walk
    /// does; answers each item peer by its row's label.</summary>
    private static Dictionary<string, AutomationPeer> ReadLikeAClient(AutomationPeer hostPeer)
    {
        Dictionary<string, AutomationPeer> byLabel = [];
        foreach (AutomationPeer child in hostPeer.GetChildren() ?? [])
        {
            _ = child.GetChildren();
            byLabel.Add(LabelOf(child), child);
        }
        return byLabel;
    }

    /// <summary>An item peer's row label. An item peer holds its item weakly, so a stale
    /// peer's removed row may be gone.</summary>
    private static string LabelOf(AutomationPeer peer) => peer switch
    {
        ItemAutomationPeer { Item: Row row } => row.Label,
        ItemAutomationPeer => "(an item peer whose item was collected)",
        _ => peer.GetType().Name,
    };

    /// <summary>Runs layout and the Loaded-priority work queued behind it.</summary>
    private static void Drain()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ContextIdle,
            () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
