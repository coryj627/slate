// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-26 (#1277), contract 34 D3–D5: a labelled edge is a live peer on the
/// visual board — named by core's traversal phrase with both endpoints,
/// windowed like a card through the same cells, realized at the current
/// zoom (its union when that fits the view, else its designated endpoint),
/// and invokable to the endpoint D5's end-style table names. Hosted STA
/// facts over a real <see cref="VaultSession"/> and real canvas bytes.
/// </summary>
public sealed class CanvasRendererPeerTests : IDisposable
{
    private const string Supports =
        "Text card \"Alpha\" connects to Text card \"Beta\", labelled \"supports\"";

    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;
    private readonly List<RenderedAnnouncement> _announced = [];

    public CanvasRendererPeerTests()
    {
        _fixture = FixtureVault.Create(0, "canvas-renderer-peers");
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    /// <summary>D5 and DD-2: the board's children are its windowed cards,
    /// then one Custom "connection" peer per windowed labelled edge, named
    /// by core's phrase; an unlabelled edge has no peer.</summary>
    [Fact]
    public void ALabelledEdgeInTheWindowIsAChildOfTheBoard() => RunSta(() =>
    {
        WriteCanvas(
            "near.canvas",
            [Card("a", "Alpha", 40, 40), Card("b", "Beta", 300, 40)],
            [Edge("e1", "a", "b", "supports"), Edge("e2", "a", "b", null)]);
        using Board board = Open("near.canvas");
        board.PanTo(0, 0);

        List<AutomationPeer> children = board.Children();
        Assert.Equal(
            ["Alpha", "Beta", Supports],
            children.Select(peer => peer.GetName()));
        CanvasEdgeAutomationPeer edge = Assert.Single(children.OfType<CanvasEdgeAutomationPeer>());
        Assert.Equal(CanvasPeerKey.Edge("e1"), edge.Key);
        Assert.Equal(AutomationControlType.Custom, edge.GetAutomationControlType());
        Assert.Equal("connection", edge.GetLocalizedControlType());
        Assert.Equal("CanvasEdge", edge.GetClassName());
        Assert.Equal("CanvasEdge:e1", edge.GetAutomationId());
        Assert.False(edge.IsOffscreen());
        Assert.NotNull(edge.GetPattern(PatternInterface.Invoke));
        Assert.NotNull(edge.GetPattern(PatternInterface.VirtualizedItem));
        Assert.Null(edge.GetPattern(PatternInterface.SelectionItem));
        Assert.False(edge.GetBoundingRectangle().IsEmpty);
        Assert.Null(board.Renderer.PeerFor(CanvasPeerKey.Edge("e2")));
    });

    /// <summary>D3/D4: an edge whose two cards lie together beyond the
    /// window is a placeholder — the virtualized-item pattern, Invoke
    /// refused — and its realization pans, at the current zoom, until the
    /// whole union is in view, promoting the same peer.</summary>
    [Fact]
    public void AnEdgeIsAPlaceholderOffWindowAndPromotesOnRealize() => RunSta(() =>
    {
        WriteCanvas(
            "far.canvas",
            [Card("home", "Home", 40, 40), Card("a", "Alpha", 20_000, 40), Card("b", "Beta", 20_300, 40)],
            [Edge("e1", "a", "b", "supports")]);
        using Board board = Open("far.canvas");
        board.PanTo(0, 0);
        CanvasViewportState before = board.Renderer.Engine.Current!.Viewport;
        Assert.True(
            500 * before.Zoom <= before.ViewWidth && 100 * before.Zoom <= before.ViewHeight,
            $"premise: the union (500×100) fits the {before.ViewWidth}×{before.ViewHeight} view at zoom {before.Zoom}.");

        CanvasEdgeAutomationPeer edge = board.HeldEdge("e1");
        Assert.DoesNotContain(board.Children(), peer => ReferenceEquals(peer, edge));
        Assert.True(edge.IsOffscreen());
        Assert.NotNull(edge.GetPattern(PatternInterface.VirtualizedItem));
        Assert.Null(edge.GetPattern(PatternInterface.Invoke));

        ((IVirtualizedItemProvider)edge).Realize();
        board.SettleUntil(() => board.Cell(CanvasPeerKey.Edge("e1")) == CanvasPeerCell.Materialized);

        CanvasViewportState after = board.Renderer.Engine.Current!.Viewport;
        Assert.Equal(before.Zoom, after.Zoom);
        AssertInView(after, 20_000, 40, 500, 100);
        Assert.Same(edge, board.Renderer.PeerFor(CanvasPeerKey.Edge("e1")));
        Assert.Contains(board.Children(), peer => ReferenceEquals(peer, edge));
        Assert.False(edge.IsOffscreen());
        var invoke = Assert.IsAssignableFrom<IInvokeProvider>(edge.GetPattern(PatternInterface.Invoke));
        invoke.Invoke();
        Assert.Equal("b", board.Document.Selection.Selected);
    });

    /// <summary>D3/D4 with a union wider than the view: no pan at the
    /// current zoom contains it, so the realization brings the designated
    /// endpoint — the card Invoke selects — wholly into view, never zooms,
    /// and Invoke then selects that card with one announcement and no
    /// further pan.</summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AnEdgeLongerThanTheViewportRealizesOnItsDesignatedEndpoint(bool fromArrow, bool toArrow) => RunSta(() =>
    {
        // The designated endpoint is the LEFT card in both rows: the
        // to-node for (false,true), the from-node for (true,false).
        (string from, string to) = toArrow ? ("right", "left") : ("left", "right");
        WriteCanvas(
            "long.canvas",
            [Card("home", "Home", 40, 40), Card("left", "Left", 20_000, 40), Card("right", "Right", 120_000, 40)],
            [Edge("e1", from, to, "spans", fromArrow, toArrow)]);
        using Board board = Open("long.canvas");
        board.PanTo(0, 0);
        CanvasViewportState before = board.Renderer.Engine.Current!.Viewport;
        Assert.True(
            100_200 * before.Zoom > before.ViewWidth,
            $"premise: the union (100,200 wide) exceeds the {before.ViewWidth} view at zoom {before.Zoom}.");

        CanvasEdgeAutomationPeer edge = board.HeldEdge("e1");
        Assert.True(edge.IsOffscreen());
        ((IVirtualizedItemProvider)edge).Realize();
        board.SettleUntil(() => board.Cell(CanvasPeerKey.Edge("e1")) == CanvasPeerCell.Materialized);

        CanvasViewportState after = board.Renderer.Engine.Current!.Viewport;
        Assert.Equal(before.Zoom, after.Zoom);
        AssertInView(after, 20_000, 40, 200, 100);
        Assert.Same(edge, board.Renderer.PeerFor(CanvasPeerKey.Edge("e1")));
        Assert.False(edge.IsOffscreen());

        board.Document.SeatSelectionSilently("home");
        _announced.Clear();
        ((IInvokeProvider)edge.GetPattern(PatternInterface.Invoke)!).Invoke();
        board.Settle();
        board.Announcer.FlushForTests();
        Assert.Equal("left", board.Document.Selection.Selected);
        Assert.Single(_announced);
        CanvasViewportState invoked = board.Renderer.Engine.Current!.Viewport;
        Assert.Equal((after.PanX, after.PanY, after.Zoom), (invoked.PanX, invoked.PanY, invoked.Zoom));
    });

    /// <summary>D5's end-style table, every cell: Invoke selects the
    /// arrow's head when exactly one end has an arrow, else the
    /// to-node, with one announcement.</summary>
    [Theory]
    [InlineData(false, true, "b")]
    [InlineData(true, false, "a")]
    [InlineData(false, false, "b")]
    [InlineData(true, true, "b")]
    public void InvokeSelectsTheEndpointTheEndStyleTableNames(bool fromArrow, bool toArrow, string selected) => RunSta(() =>
    {
        WriteCanvas(
            "table.canvas",
            [Card("a", "Alpha", 40, 40), Card("b", "Beta", 300, 40), Card("c", "Gamma", 40, 200)],
            [Edge("e1", "a", "b", "relates", fromArrow, toArrow)]);
        using Board board = Open("table.canvas");
        board.PanTo(0, 0);
        CanvasEdgeAutomationPeer edge = Assert.Single(board.Children().OfType<CanvasEdgeAutomationPeer>());

        board.Document.SeatSelectionSilently("c");
        _announced.Clear();
        ((IInvokeProvider)edge.GetPattern(PatternInterface.Invoke)!).Invoke();
        board.Announcer.FlushForTests();

        Assert.Equal(selected, board.Document.Selection.Selected);
        Assert.Single(_announced);
    });

    /// <summary>D3: an edge peer is the same object for as long as anything
    /// holds it, across installs, within its renderer; a second pane's
    /// renderer holds its own.</summary>
    [Fact]
    public void TheSameEdgePeerIsReturnedWhileAnythingHoldsIt() => RunSta(() =>
    {
        WriteCanvas(
            "same.canvas",
            [Card("a", "Alpha", 40, 40), Card("b", "Beta", 300, 40)],
            [Edge("e1", "a", "b", "supports")]);
        using Board board = Open("same.canvas");
        board.PanTo(0, 0);
        CanvasEdgeAutomationPeer edge = Assert.Single(board.Children().OfType<CanvasEdgeAutomationPeer>());

        board.PanTo(-10, 0);
        Assert.Same(edge, board.Renderer.PeerFor(CanvasPeerKey.Edge("e1")));
        Assert.Same(edge, Assert.Single(board.Children().OfType<CanvasEdgeAutomationPeer>()));

        using Board second = Open(board);
        second.PanTo(0, 0);
        CanvasEdgeAutomationPeer other = Assert.Single(second.Children().OfType<CanvasEdgeAutomationPeer>());
        Assert.NotSame(edge, other);
        Assert.Equal(edge.GetName(), other.GetName());
    });

    /// <summary>D3's first-touch cell: the descriptor index finds an edge by
    /// its name without minting or windowing anything; the container's name
    /// search — the cards' door, which realizes what it finds — hands back
    /// that edge's peer and pans the edge into view at the current zoom; a
    /// search after an edge continues in document order.</summary>
    [Fact]
    public void FindItemByNameResolvesAnEdge() => RunSta(() =>
    {
        const string Cites =
            "Text card \"Beta\" connects to Text card \"Alpha\", labelled \"cites\"";
        WriteCanvas(
            "find.canvas",
            [Card("home", "Home", 40, 40), Card("a", "Alpha", 20_000, 40), Card("b", "Beta", 20_300, 40)],
            [Edge("e1", "a", "b", "supports"), Edge("e2", "b", "a", "cites")]);
        using Board board = Open("find.canvas");
        board.PanTo(0, 0);
        CanvasPresentationState installed = board.Renderer.Engine.Current!;

        Assert.Equal(CanvasPeerKey.Edge("e1"), board.Renderer.FindByName(null, Supports));
        Assert.Equal(CanvasPeerKey.Edge("e2"), board.Renderer.FindByName(null, Cites));
        Assert.Same(installed, board.Renderer.Engine.Current);
        Assert.False(installed.Topology.Placements.ContainsKey(CanvasPeerKey.Edge("e1")));

        // The search mints the edge's peer and realizes it; with no UIA
        // client listening the provider it hands back may be null, so the
        // fact holds the peer the search minted, as the cards' fact does.
        _ = ((IItemContainerProvider)board.Peer).FindItemByProperty(
            null, AutomationElementIdentifiers.NameProperty.Id, Supports);
        CanvasEdgeAutomationPeer edge = board.HeldEdge("e1");
        Assert.Equal(CanvasPeerKey.Edge("e2"), board.Renderer.FindByName(edge, null));

        board.SettleUntil(() => board.Cell(CanvasPeerKey.Edge("e1")) == CanvasPeerCell.Materialized);
        Assert.Contains(CanvasPeerKey.Edge("e1"), board.Renderer.Engine.Current!.Retained);
        Assert.Same(edge, board.Renderer.PeerFor(CanvasPeerKey.Edge("e1")));
        CanvasViewportState after = board.Renderer.Engine.Current!.Viewport;
        Assert.Equal(installed.Viewport.Zoom, after.Zoom);
        AssertInView(after, 20_000, 40, 500, 100);
    });

    /// <summary>A labelled edge's rectangle is its endpoints' union, so it
    /// covers them: a point on an endpoint card is the CARD (a reader's
    /// mouse or touch hears the card), and only a point no card covers is
    /// the edge's.</summary>
    [Fact]
    public void APointOnAnEndpointCardIsTheCardNotTheEdge() => RunSta(() =>
    {
        WriteCanvas(
            "point.canvas",
            [Card("a", "Alpha", 40, 40), Card("b", "Beta", 400, 240)],
            [Edge("e1", "a", "b", "supports")]);
        using Board board = Open("point.canvas");
        board.PanTo(0, 0);
        List<AutomationPeer> children = board.Children();
        CanvasCardAutomationPeer alpha = children.OfType<CanvasCardAutomationPeer>().Single(card => card.Key.Id == "a");
        CanvasCardAutomationPeer beta = children.OfType<CanvasCardAutomationPeer>().Single(card => card.Key.Id == "b");
        CanvasEdgeAutomationPeer edge = Assert.Single(children.OfType<CanvasEdgeAutomationPeer>());

        Rect a = alpha.GetBoundingRectangle();
        Rect b = beta.GetBoundingRectangle();
        Rect union = edge.GetBoundingRectangle();
        Assert.True(union.Contains(a) && union.Contains(b), "premise: the edge's rectangle covers both cards.");
        Assert.Same(alpha, board.Peer.GetPeerFromPoint(new Point(a.X + (a.Width / 2), a.Y + (a.Height / 2))));
        Assert.Same(beta, board.Peer.GetPeerFromPoint(new Point(b.X + (b.Width / 2), b.Y + (b.Height / 2))));
        // Between the cards: the top-right corner of the union, on neither.
        Assert.Same(edge, board.Peer.GetPeerFromPoint(new Point(union.Right - 2, union.Top + 2)));
    });

    /// <summary>D3: a structure change raises the container's one
    /// children-invalidated event — adding a labelled edge between two
    /// windowed cards adds one child and raises exactly one.</summary>
    [Fact]
    public void AStructureChangeRaisesChildrenInvalidatedOncePerInstall() => RunSta(() =>
    {
        WriteCanvas(
            "grow.canvas",
            [Card("a", "Alpha", 40, 40), Card("b", "Beta", 300, 40)],
            []);
        using Board board = Open("grow.canvas");
        board.PanTo(0, 0);
        Assert.Equal(2, board.Children().Count);
        int announced = CanvasRendererAutomationPeer.ChildrenInvalidatedForTests;

        board.Document.CanvasConnect("a", "b", "supports");
        board.SettleUntil(() => board.Renderer.Engine.Current!.Topology.Placements.Keys.Any(key => key.IsEdge));

        Assert.Equal(announced + 1, CanvasRendererAutomationPeer.ChildrenInvalidatedForTests);
        Assert.Equal(Supports, Assert.Single(board.Children().OfType<CanvasEdgeAutomationPeer>()).GetName());
    });

    // --- Fixture -----------------------------------------------------------

    private static string Card(string id, string text, int x, int y) =>
        $"{{\"id\":\"{id}\",\"type\":\"text\",\"text\":\"{text}\",\"x\":{x},\"y\":{y},\"width\":200,\"height\":100}}";

    private static string Edge(
        string id, string from, string to, string? label, bool fromArrow = false, bool toArrow = true) =>
        $"{{\"id\":\"{id}\",\"fromNode\":\"{from}\",\"toNode\":\"{to}\","
        + $"\"fromEnd\":\"{(fromArrow ? "arrow" : "none")}\",\"toEnd\":\"{(toArrow ? "arrow" : "none")}\""
        + (label is null ? string.Empty : $",\"label\":\"{label}\"")
        + "}";

    private void WriteCanvas(string name, string[] nodes, string[] edges) => File.WriteAllText(
        Path.Combine(_fixture.Root, name),
        "{\"nodes\":[" + string.Join(",", nodes) + "],\"edges\":[" + string.Join(",", edges) + "]}");

    private static void AssertInView(CanvasViewportState viewport, double x, double y, double width, double height)
    {
        double left = (x * viewport.Zoom) + viewport.PanX;
        double top = (y * viewport.Zoom) + viewport.PanY;
        Assert.True(
            left >= 0 && top >= 0
                && left + (width * viewport.Zoom) <= viewport.ViewWidth
                && top + (height * viewport.Zoom) <= viewport.ViewHeight,
            $"the rect ({x}, {y}, {width}×{height}) sits at view ({left}, {top}) in the "
            + $"{viewport.ViewWidth}×{viewport.ViewHeight} view at zoom {viewport.Zoom}.");
    }

    private Board Open(string path)
    {
        var announcer = new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1));
        var document = new CanvasDocumentViewModel(_session, path, announcer, synchronousForTests: true);
        document.Load();
        return new Board(document, announcer, ownsDocument: true);
    }

    private static Board Open(Board shown) => new(shown.Document, shown.Announcer, ownsDocument: false);

    /// <summary>One shown visual board over a document: the surface in a
    /// window, its renderer, and a client's board peer.</summary>
    private sealed class Board : IDisposable
    {
        private readonly Window _window;
        private readonly bool _ownsDocument;

        internal Board(CanvasDocumentViewModel document, CanvasAnnouncer announcer, bool ownsDocument)
        {
            Document = document;
            Announcer = announcer;
            _ownsDocument = ownsDocument;
            var surface = new CanvasSurfaceView { Model = document };
            Renderer = surface.VisualForTests;
            _window = new Window
            {
                Content = surface,
                Width = 900,
                Height = 700,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ShowActivated = false,
            };
            _window.Show();
            document.ShowSurface(CanvasSurfaceKind.Visual);
            _window.UpdateLayout();
            Peer = Assert.IsType<CanvasRendererAutomationPeer>(
                UIElementAutomationPeer.CreatePeerForElement(Renderer));
            SettleUntil(() => Renderer.Engine.Current is { Source.Loaded: not null } state
                && state.Viewport.ViewWidth > 0);
        }

        internal CanvasDocumentViewModel Document { get; }

        /// <summary>The document's announcer: a navigation line waits out
        /// its coalescing window, so the facts flush it before
        /// counting.</summary>
        internal CanvasAnnouncer Announcer { get; }

        internal CanvasRendererView Renderer { get; }

        internal CanvasRendererAutomationPeer Peer { get; }

        internal List<AutomationPeer> Children()
        {
            Peer.ResetChildrenCache();
            return Peer.GetChildren() ?? [];
        }

        /// <summary>A client's hold on an edge's peer (minted on first
        /// use), as a reader keeps what it found.</summary>
        internal CanvasEdgeAutomationPeer HeldEdge(string edgeId) =>
            Assert.IsType<CanvasEdgeAutomationPeer>(Renderer.PeerFor(CanvasPeerKey.Edge(edgeId)));

        internal CanvasPeerCell? Cell(CanvasPeerKey key) =>
            Renderer.Engine.Current?.Topology.Placements.TryGetValue(key, out CanvasPeerPlacement? placement) == true
                ? placement.Cell
                : null;

        internal void PanTo(double panX, double panY)
        {
            Renderer.Engine.CommitViewport(view => view.PannedTo(panX, panY));
            Settle();
        }

        internal void Settle() => SettleUntil(() => true);

        internal void SettleUntil(Func<bool> condition) => PumpUntil(() =>
            Renderer.Engine.Current is { } state
            && state.Viewport.SameGeometry(Renderer.Engine.CommittedViewport)
            && condition());

        public void Dispose()
        {
            Renderer.Shutdown();
            _window.Close();
            if (_ownsDocument)
            {
                Document.Shutdown();
            }
        }
    }

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(120), "STA test body timed out.");

    private static void PumpUntil(Func<bool> condition)
    {
        var budget = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && budget.Elapsed < TimeSpan.FromSeconds(10))
        {
            PumpDispatcher();
            Thread.Yield();
        }
        Assert.True(condition(), "premise: the pumped condition never held.");
    }

    private static void PumpDispatcher()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
}
