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
    /// whole union is in view, promoting the same peer. The edge points at
    /// the LEFT card, so a pan to its designated endpoint alone would leave
    /// the right card out of view.</summary>
    [Fact]
    public void AnEdgeIsAPlaceholderOffWindowAndPromotesOnRealize() => RunSta(() =>
    {
        WriteCanvas(
            "far.canvas",
            [Card("home", "Home", 40, 40), Card("a", "Alpha", 20_000, 40), Card("b", "Beta", 20_300, 40)],
            [Edge("e1", "b", "a", "cites")]);
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
        Assert.Equal("a", board.Document.Selection.Selected);
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
        // "cites" points at the LEFT card, so only the union pan shows both.
        _ = ((IItemContainerProvider)board.Peer).FindItemByProperty(
            null, AutomationElementIdentifiers.NameProperty.Id, Cites);
        CanvasEdgeAutomationPeer edge = board.HeldEdge("e2");
        Assert.Equal(CanvasPeerKey.Edge("e2"), board.Renderer.FindByName(board.HeldEdge("e1"), null));
        Assert.Null(board.Renderer.FindByName(edge, null));

        board.SettleUntil(() => board.Cell(CanvasPeerKey.Edge("e2")) == CanvasPeerCell.Materialized);
        Assert.Contains(CanvasPeerKey.Edge("e2"), board.Renderer.Engine.Current!.Retained);
        Assert.Same(edge, board.Renderer.PeerFor(CanvasPeerKey.Edge("e2")));
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

    /// <summary>Peers materialize up to a view beyond the board, and an edge's
    /// union can reach further still: a point beside the board — over the
    /// sidebar or the editor — is never a card or an edge, nor the board
    /// itself, so the window's walk reaches the sibling control that is
    /// there, with the board shown or hidden (codex round 5).</summary>
    [Fact]
    public void APointBesideTheBoardIsNeverOneOfItsPeers() => RunSta(() =>
    {
        WriteCanvas(
            "wide.canvas",
            [Card("a", "Alpha", 40, 40), Card("b", "Beta", 6_000, 40)],
            [Edge("e1", "a", "b", "spans")]);
        using Board board = Open("wide.canvas");
        board.ZoomTo(1);
        CanvasEdgeAutomationPeer edge = Assert.Single(board.Children().OfType<CanvasEdgeAutomationPeer>());
        Rect bounds = board.Peer.GetBoundingRectangle();
        Rect union = edge.GetBoundingRectangle();
        var beside = new Point(bounds.Right + 20, union.Top + (union.Height / 2));
        Assert.True(
            union.Contains(beside),
            $"premise: the edge's union {union} reaches past the board {bounds} to {beside}.");

        Assert.Null(board.Peer.GetPeerFromPoint(beside));

        // Through the window, onto the sibling switcher button above the board.
        AutomationPeer window = UIElementAutomationPeer.CreatePeerForElement(board.Window);
        FrameworkElement outlineChoice = FindByAutomationId(board.Window, "CanvasShowOutline");
        AutomationPeer choice = UIElementAutomationPeer.CreatePeerForElement(outlineChoice);
        Rect choiceRect = choice.GetBoundingRectangle();
        var onChoice = new Point(choiceRect.X + (choiceRect.Width / 2), choiceRect.Y + (choiceRect.Height / 2));
        Assert.False(bounds.Contains(onChoice), "premise: the switcher button sits off the board.");
        foreach (bool hidden in new[] { false, true })
        {
            board.Renderer.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
            board.Window.UpdateLayout();
            AutomationPeer? hit = window.GetPeerFromPoint(onChoice);
            Assert.True(
                hit is not null && hit is not CanvasRendererAutomationPeer && hit is not CanvasBoardAutomationPeer,
                $"the window's walk answered {hit?.GetType().Name ?? "null"} for the switcher button (board hidden: {hidden}).");
        }
        board.Renderer.Visibility = Visibility.Visible;
    });

    /// <summary>D4: the newest request for the view wins (codex round 5). With
    /// the board still installing what the document applied, a realization
    /// owed to that install and a reveal of a card the user selects on the
    /// board — in either order — or a fit verb after the realization end with
    /// the view where the NEWER request put it.</summary>
    [Theory]
    [InlineData("realize, then select")]
    [InlineData("select, then realize")]
    [InlineData("realize, then fit")]
    [InlineData("hidden: select, then realize")]
    public void TheNewestRequestForTheViewWins(string order) => RunSta(() =>
    {
        WriteCanvas(
            "newest.canvas",
            [Card("near", "Near", 40, 40), Card("a", "Alpha", 20_000, 40), Card("b", "Beta", 20_300, 40)],
            [Edge("e1", "b", "a", "far"), Edge("e2", "near", "near", "loops")]);
        using Board board = Open("newest.canvas");
        board.ZoomTo(1);
        var far = Assert.IsAssignableFrom<IVirtualizedItemProvider>(board.HeldEdge("e1").GetPattern(PatternInterface.VirtualizedItem));
        board.Document.SeatSelectionSilently("a");

        if (order.StartsWith("hidden", StringComparison.Ordinal))
        {
            // A hidden board owes its reveal until it shows again; a
            // realization made meanwhile pans at once, and the reveal it
            // superseded must not undo it when the board shows.
            board.Renderer.Visibility = Visibility.Collapsed;
            board.Renderer.SelectAnnounced("near");
            far.Realize();
            board.Renderer.Visibility = Visibility.Visible;
            board.Window.UpdateLayout();
            board.Settle();
            AssertInView(board.Renderer.Engine.Current!.Viewport, 20_000, 40, 500, 100);
            return;
        }

        // The document applies an edit the board has not installed yet.
        _ = board.Document.CanvasEditConnection("e2", "circles", CanvasConnectionDirection.ToTarget);
        Assert.False(
            ReferenceEquals(board.Renderer.Engine.Current!.Source.Loaded!.Population, board.Document.AppliedPublication!.Loaded!.Population),
            "premise: the board has not installed the applied edit yet.");

        switch (order)
        {
            case "realize, then select":
                far.Realize();
                board.Renderer.SelectAnnounced("near");
                break;
            case "select, then realize":
                board.Renderer.SelectAnnounced("near");
                far.Realize();
                break;
            default:
                far.Realize();
                _ = board.Surface.ViewportCommand(CanvasViewportVerb.FitCanvas);
                break;
        }
        board.SettleUntil(() => ReferenceEquals(
            board.Renderer.Engine.Current!.Source.Loaded!.Population, board.Document.AppliedPublication!.Loaded!.Population));
        board.Settle();

        CanvasViewportState after = board.Renderer.Engine.Current!.Viewport;
        switch (order)
        {
            case "realize, then select":
                AssertInView(after, 40, 40, 200, 100);
                break;
            case "select, then realize":
                AssertInView(after, 20_000, 40, 500, 100);
                break;
            default:
                // Fit centres the canvas's bounds (40..20,500) at its clamped
                // zoom; the owed realization would have panned to the edge.
                Assert.True(after.Zoom < 0.5, $"the fit was undone: zoom {after.Zoom}.");
                Assert.InRange((10_270 * after.Zoom) + after.PanX, (after.ViewWidth / 2) - 2, (after.ViewWidth / 2) + 2);
                break;
        }
    });

    /// <summary>A held edge peer answers only while its edge is a peer on
    /// this board: once the edge loses its label or is deleted, or the pane
    /// closes, it exposes no pattern and reads offscreen, and the registry
    /// hands it out no more; an undo that brings the edge back brings the
    /// same peer back (codex).</summary>
    [Fact]
    public void AHeldEdgePeerGoesUnavailableWhenItsEdgeStopsBeingAPeer() => RunSta(() =>
    {
        WriteCanvas(
            "gone.canvas",
            [Card("a", "Alpha", 40, 40), Card("b", "Beta", 300, 40)],
            [Edge("e1", "a", "b", "supports"), Edge("e2", "a", "b", "cites")]);
        using Board board = Open("gone.canvas");
        board.PanTo(0, 0);
        CanvasEdgeAutomationPeer supports = board.HeldEdge("e1");
        CanvasEdgeAutomationPeer cites = board.HeldEdge("e2");

        // Unlabelled: no longer a peer (DD-2).
        _ = board.Document.CanvasEditConnection("e1", null, CanvasConnectionDirection.ToTarget);
        board.SettleUntil(() => board.Renderer.Engine.Current!.Source.Loaded!.Population.SceneByEdge["e1"].SpeakableName is null);
        AssertGone(supports);
        Assert.Null(board.Renderer.PeerFor(CanvasPeerKey.Edge("e1")));

        // Deleted.
        _ = board.Document.CanvasDeleteConnection("e2");
        board.SettleUntil(() => !board.Renderer.Engine.Current!.Source.Loaded!.Population.SceneByEdge.ContainsKey("e2"));
        AssertGone(cites);
        Assert.Null(board.Renderer.PeerFor(CanvasPeerKey.Edge("e2")));

        // Undone: the edge is back, and the peer the reader holds is it again.
        board.Document.CanvasUndo();
        board.SettleUntil(() => board.Cell(CanvasPeerKey.Edge("e2")) == CanvasPeerCell.Materialized);
        Assert.Same(cites, board.Renderer.PeerFor(CanvasPeerKey.Edge("e2")));
        Assert.NotNull(cites.GetPattern(PatternInterface.Invoke));

        // The pane closes.
        board.Renderer.Shutdown();
        AssertGone(cites);

        static void AssertGone(CanvasEdgeAutomationPeer peer)
        {
            Assert.Equal(string.Empty, peer.GetName());
            Assert.True(peer.IsOffscreen());
            Assert.True(peer.GetBoundingRectangle().IsEmpty);
            Assert.Null(peer.GetPattern(PatternInterface.VirtualizedItem));
            Assert.Null(peer.GetPattern(PatternInterface.Invoke));
        }
    });

    /// <summary>The board installs what the document applied a moment
    /// later: in that moment a kept action must not reach what the document
    /// already dropped, and an edge's Invoke obeys the direction the document
    /// already applied (codex round 3).</summary>
    [Fact]
    public void ARetainedActionObeysWhatTheDocumentAppliedBeforeTheBoardInstallsIt() => RunSta(() =>
    {
        WriteCanvas(
            "lag.canvas",
            [Card("a", "Alpha", 40, 40), Card("b", "Beta", 300, 40), Card("c", "Gamma", 40, 200)],
            [Edge("e1", "a", "b", "supports"), Edge("e2", "a", "b", "cites")]);
        using Board board = Open("lag.canvas");
        board.ZoomTo(1);
        var deletedInvoke = Assert.IsAssignableFrom<IInvokeProvider>(board.HeldEdge("e1").GetPattern(PatternInterface.Invoke));
        var deletedRealize = Assert.IsAssignableFrom<IVirtualizedItemProvider>(board.HeldEdge("e1").GetPattern(PatternInterface.VirtualizedItem));
        var turnedInvoke = Assert.IsAssignableFrom<IInvokeProvider>(board.HeldEdge("e2").GetPattern(PatternInterface.Invoke));
        board.Document.SeatSelectionSilently("c");

        _ = board.Document.CanvasDeleteConnection("e1");
        _ = board.Document.CanvasEditConnection("e2", "cites", CanvasConnectionDirection.FromTarget);
        CanvasPopulation applied = board.Document.AppliedPublication!.Loaded!.Population;
        CanvasPopulation installed = board.Renderer.Engine.Current!.Source.Loaded!.Population;
        Assert.True(
            !applied.SceneByEdge.ContainsKey("e1") && installed.SceneByEdge.ContainsKey("e1")
                && applied.SceneByEdge["e2"].FromArrow && !installed.SceneByEdge["e2"].FromArrow,
            "premise: the document applied both edits and the board has not installed them yet.");

        Assert.Throws<ElementNotAvailableException>(deletedInvoke.Invoke);
        Assert.Throws<ElementNotAvailableException>(deletedRealize.Realize);
        Assert.Equal("c", board.Document.Selection.Selected);

        turnedInvoke.Invoke();
        Assert.Equal("a", board.Document.Selection.Selected);
    });

    /// <summary>D3/D4 between the document's apply and the board's install: a
    /// realization asked for in that moment waits for the scene the document
    /// applied and pans by ITS geometry — here a direction change that moved
    /// the designated endpoint of an edge longer than the view — and a search
    /// for an edge the document already deleted hands nothing back and moves
    /// nothing (codex round 4).</summary>
    [Fact]
    public void ARealizationWaitsForTheSceneTheDocumentApplied() => RunSta(() =>
    {
        WriteCanvas(
            "realize-lag.canvas",
            [Card("home", "Home", 40, 40), Card("left", "Left", 20_000, 40), Card("right", "Right", 120_000, 40)],
            [Edge("e1", "right", "left", "spans"), Edge("e2", "home", "left", "gone")]);
        using Board board = Open("realize-lag.canvas");
        board.ZoomTo(1);
        CanvasEdgeAutomationPeer edge = board.HeldEdge("e1");
        var realize = Assert.IsAssignableFrom<IVirtualizedItemProvider>(edge.GetPattern(PatternInterface.VirtualizedItem));
        const string Gone = "Text card \"Home\" connects to Text card \"Left\", labelled \"gone\"";
        CanvasViewportState before = board.Renderer.Engine.CommittedViewport;

        // The designated endpoint was Left (the arrow's head); the direction
        // change makes it Right, the from-node.
        _ = board.Document.CanvasEditConnection("e1", "spans", CanvasConnectionDirection.FromTarget);
        _ = board.Document.CanvasDeleteConnection("e2");
        CanvasPopulation installed = board.Renderer.Engine.Current!.Source.Loaded!.Population;
        Assert.True(
            !installed.SceneByEdge["e1"].FromArrow && installed.SceneByEdge.ContainsKey("e2"),
            "premise: the board has not installed the document's edits yet.");

        Assert.Null(((IItemContainerProvider)board.Peer).FindItemByProperty(
            null, AutomationElementIdentifiers.NameProperty.Id, Gone));
        Assert.True(before.SameGeometry(board.Renderer.Engine.CommittedViewport), "the search for a deleted edge panned.");
        Assert.Null(board.Renderer.RealizePeer(CanvasPeerKey.Edge("e2")));
        realize.Realize();
        board.SettleUntil(() => board.Cell(CanvasPeerKey.Edge("e1")) == CanvasPeerCell.Materialized);

        CanvasViewportState after = board.Renderer.Engine.Current!.Viewport;
        Assert.Equal(before.Zoom, after.Zoom);
        AssertInView(after, 120_000, 40, 200, 100);
        Assert.False(edge.IsOffscreen());
    });

    /// <summary>D5: an edge's Invoke always says where it landed — when its
    /// endpoint is already the seat, and on a second Invoke of the same
    /// edge — with exactly one announcement each time (codex round
    /// 3).</summary>
    [Theory]
    [InlineData(false, true, "b")]
    [InlineData(true, false, "a")]
    [InlineData(false, false, "b")]
    [InlineData(true, true, "b")]
    public void InvokingAnEdgeWhoseEndpointIsTheSeatStillSaysWhereItLanded(bool fromArrow, bool toArrow, string selected) => RunSta(() =>
    {
        WriteCanvas(
            "seated.canvas",
            [Card("a", "Alpha", 40, 40), Card("b", "Beta", 300, 40)],
            [Edge("e1", "a", "b", "relates", fromArrow, toArrow)]);
        using Board board = Open("seated.canvas");
        board.PanTo(0, 0);
        var invoke = Assert.IsAssignableFrom<IInvokeProvider>(board.HeldEdge("e1").GetPattern(PatternInterface.Invoke));
        board.Document.SeatSelectionSilently(selected);

        for (int round = 1; round <= 2; round++)
        {
            _announced.Clear();
            invoke.Invoke();
            board.Announcer.FlushForTests();
            Assert.Equal(selected, board.Document.Selection.Selected);
            Assert.Single(_announced);
        }
    });

    /// <summary>D3's identity is (renderer, document, key): a peer held on
    /// one canvas never answers for another canvas the same board shows that
    /// reuses the edge's id, and the board showing the first canvas again
    /// hands back the peer the reader held (codex).</summary>
    [Fact]
    public void AnEdgePeerHeldOnOneCanvasNeverAnswersForAnother() => RunSta(() =>
    {
        WriteCanvas(
            "first.canvas",
            [Card("a", "Alpha", 40, 40), Card("b", "Beta", 300, 40)],
            [Edge("e1", "a", "b", "supports")]);
        WriteCanvas(
            "second.canvas",
            [Card("a", "Gamma", 40, 40), Card("b", "Delta", 300, 40)],
            [Edge("e1", "b", "a", "refutes")]);
        using Board board = Open("first.canvas");
        board.PanTo(0, 0);
        CanvasEdgeAutomationPeer first = board.HeldEdge("e1");
        Assert.Equal(Supports, first.GetName());

        CanvasDocumentViewModel second = OpenDocument("second.canvas");
        try
        {
            board.Show(second);
            board.SettleUntil(() => board.Renderer.Engine.Current!.Source.Loaded!.Population.SceneByNode["a"].Title == "Gamma");
            Assert.Equal(string.Empty, first.GetName());
            Assert.Null(first.GetPattern(PatternInterface.VirtualizedItem));
            CanvasEdgeAutomationPeer other = board.HeldEdge("e1");
            Assert.NotSame(first, other);
            Assert.Equal(
                "Text card \"Delta\" connects to Text card \"Gamma\", labelled \"refutes\"",
                other.GetName());

            board.Show(board.Document);
            board.SettleUntil(() => board.Renderer.Engine.Current!.Source.Loaded!.Population.SceneByNode["a"].Title == "Alpha");
            Assert.Same(first, board.Renderer.PeerFor(CanvasPeerKey.Edge("e1")));
            Assert.Equal(Supports, first.GetName());
        }
        finally
        {
            second.Shutdown();
        }
    });

    /// <summary>A client keeps the pattern interfaces it was handed and calls
    /// them later, so every action rechecks when it runs (codex round 2): a
    /// retained provider whose peer's canvas the board no longer shows, or
    /// whose edge left the window, refuses as unavailable and moves nothing
    /// on the canvas the board shows now.</summary>
    [Fact]
    public void ARetainedProviderRefusesOnceItsPeerIsUnavailable() => RunSta(() =>
    {
        WriteCanvas(
            "first.canvas",
            [Card("a", "Alpha", 40, 40), Card("b", "Beta", 300, 40), Card("far", "Far", 9_000, 40)],
            [Edge("e1", "a", "b", "supports")]);
        WriteCanvas(
            "second.canvas",
            [Card("a", "Gamma", 40, 40), Card("b", "Delta", 300, 40), Card("far", "Away", 9_000, 40)],
            [Edge("e1", "b", "a", "refutes")]);
        using Board board = Open("first.canvas");
        board.ZoomTo(1);
        CanvasEdgeAutomationPeer edge = board.HeldEdge("e1");
        var card = Assert.IsType<CanvasCardAutomationPeer>(board.Renderer.PeerFor(CanvasPeerKey.Card("a")));
        var edgeInvoke = Assert.IsAssignableFrom<IInvokeProvider>(edge.GetPattern(PatternInterface.Invoke));
        var edgeRealize = Assert.IsAssignableFrom<IVirtualizedItemProvider>(edge.GetPattern(PatternInterface.VirtualizedItem));
        var cardInvoke = Assert.IsAssignableFrom<IInvokeProvider>(card.GetPattern(PatternInterface.Invoke));
        var cardItem = Assert.IsAssignableFrom<ISelectionItemProvider>(card.GetPattern(PatternInterface.SelectionItem));

        // The board shows another canvas that reuses every id.
        CanvasDocumentViewModel second = OpenDocument("second.canvas");
        try
        {
            board.Show(second);
            board.SettleUntil(() => ReferenceEquals(board.Renderer.InstalledDocument, second));
            board.PanTo(-8_000, 0);
            second.SeatSelectionSilently("far");
            board.Settle();
            CanvasViewportState away = board.Renderer.Engine.CommittedViewport;

            Assert.Throws<ElementNotAvailableException>(edgeRealize.Realize);
            Assert.Throws<ElementNotAvailableException>(edgeInvoke.Invoke);
            Assert.Throws<ElementNotAvailableException>(cardInvoke.Invoke);
            Assert.Throws<ElementNotAvailableException>(cardItem.Select);
            Assert.False(cardItem.IsSelected);
            board.Settle();
            Assert.Equal("far", second.Selection.Selected);
            Assert.True(away.SameGeometry(board.Renderer.Engine.CommittedViewport), "a retained provider moved the other canvas's view.");
            Assert.DoesNotContain(CanvasPeerKey.Edge("e1"), board.Renderer.Engine.Current!.Retained);
        }
        finally
        {
            board.Show(board.Document);
            second.Shutdown();
        }

        // Back on its own canvas, panned so the edge is off the window: the
        // action pattern it kept refuses, the placeholder's rule.
        board.SettleUntil(() => ReferenceEquals(board.Renderer.InstalledDocument, board.Document));
        board.PanTo(-20_000, -20_000);
        Assert.NotEqual(CanvasPeerCell.Materialized, board.Cell(CanvasPeerKey.Edge("e1")));
        board.Document.SeatSelectionSilently("far");
        Assert.Throws<ElementNotAvailableException>(edgeInvoke.Invoke);
        Assert.Equal("far", board.Document.Selection.Selected);
    });

    /// <summary>The engine installs off the dispatcher, so just after the
    /// board goes back to a canvas its installed state is still the other
    /// canvas's: until its own state installs, a held peer reads nothing and
    /// nothing is minted from the other canvas's scene (codex round
    /// 2).</summary>
    [Fact]
    public void AHeldPeerReadsNothingUntilItsCanvasIsInstalledAgain() => RunSta(() =>
    {
        WriteCanvas(
            "first.canvas",
            [Card("a", "Alpha", 40, 40), Card("b", "Beta", 300, 40)],
            [Edge("e1", "a", "b", "supports")]);
        WriteCanvas(
            "second.canvas",
            [Card("a", "Gamma", 40, 40), Card("b", "Delta", 300, 40)],
            [Edge("e1", "b", "a", "refutes")]);
        using Board board = Open("first.canvas");
        board.PanTo(0, 0);
        CanvasEdgeAutomationPeer first = board.HeldEdge("e1");
        CanvasDocumentViewModel second = OpenDocument("second.canvas");
        try
        {
            board.Show(second);
            board.SettleUntil(() => ReferenceEquals(board.Renderer.InstalledDocument, second));

            board.Show(board.Document);
            Assert.True(
                ReferenceEquals(board.Renderer.InstalledDocument, second),
                "premise: the switch back has not installed yet.");
            Assert.Equal(string.Empty, first.GetName());
            Assert.Null(first.GetPattern(PatternInterface.VirtualizedItem));
            Assert.Null(board.Renderer.PeerFor(CanvasPeerKey.Edge("e1")));
            Assert.Null(board.Renderer.FindByName(null, null));
            Assert.Empty(board.Renderer.MaterializedPeers());

            board.SettleUntil(() => ReferenceEquals(board.Renderer.InstalledDocument, board.Document));
            Assert.Equal(Supports, first.GetName());
            Assert.Same(first, board.Renderer.PeerFor(CanvasPeerKey.Edge("e1")));
        }
        finally
        {
            second.Shutdown();
        }
    });

    /// <summary>D4 during a move preview: the realization targets the
    /// rectangles the board DRAWS. The committed union is wider than the
    /// view, the previewed one fits, so the whole previewed edge comes into
    /// view (codex).</summary>
    [Fact]
    public void AnEdgeRealizesOnTheRectanglesItsBoardDraws() => RunSta(() =>
    {
        WriteCanvas(
            "preview.canvas",
            [Card("home", "Home", 40, 40), Card("a", "Alpha", 20_000, 40), Card("b", "Beta", 23_000, 40)],
            [Edge("e1", "b", "a", "cites")]);
        using Board board = Open("preview.canvas");
        board.ZoomTo(1);
        CanvasViewportState before = board.Renderer.Engine.Current!.Viewport;
        Assert.True(3_200 * before.Zoom > before.ViewWidth, "premise: the committed union is wider than the view.");

        // Move Beta left, in the preview only, until its union with Alpha fits.
        board.Document.Navigator.AttachPresenter(board.Surface);
        board.Document.SeatSelectionSilently("b");
        Assert.True(board.Document.Navigator.EnterMoveMode(), "premise: move mode starts on the seated card.");
        CanvasRect Beta() => board.Document.Transient!.Rects["b"];
        for (int step = 0; step < 400 && (board.Document.Transient?.Rects.ContainsKey("b") != true
            || (Beta().X + Beta().Width - 20_000) * before.Zoom > before.ViewWidth * 0.8); step++)
        {
            Assert.True(board.Document.Navigator.ModeStep(-1, 0, large: true), "premise: the preview moves.");
        }
        board.SettleUntil(() => board.Renderer.Engine.Current!.TransientRects?.ContainsKey("b") == true);
        CanvasRect previewed = Beta();
        double right = previewed.X + previewed.Width;
        Assert.True((right - 20_000) * before.Zoom <= before.ViewWidth, "premise: the previewed union fits the view.");

        // Move mode follows the moved card into view (and keeps a moving
        // card's edges materialized); pan back home so the realization, not
        // the preview's own reveal, brings the edge into view.
        board.PanTo(0, 0);
        CanvasViewportState home = board.Renderer.Engine.Current!.Viewport;
        Assert.True(
            (20_000 * home.Zoom) + home.PanX > home.ViewWidth,
            "premise: panned home, the previewed edge is out of view.");
        CanvasEdgeAutomationPeer edge = board.HeldEdge("e1");
        ((IVirtualizedItemProvider)edge).Realize();
        board.SettleUntil(() => board.Cell(CanvasPeerKey.Edge("e1")) == CanvasPeerCell.Materialized);

        CanvasViewportState after = board.Renderer.Engine.Current!.Viewport;
        Assert.Equal(before.Zoom, after.Zoom);
        AssertInView(after, 20_000, 40, right - 20_000, 100);
        _ = board.Document.Navigator.CancelMode();
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

    private CanvasDocumentViewModel OpenDocument(string path)
    {
        var document = new CanvasDocumentViewModel(
            _session, path, new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)), synchronousForTests: true);
        document.Load();
        return document;
    }

    private static Board Open(Board shown) => new(shown.Document, shown.Announcer, ownsDocument: false);

    /// <summary>One shown visual board over a document: the surface in a
    /// window, its renderer, and a client's board peer.</summary>
    private sealed class Board : IDisposable
    {
        private readonly Window _window;
        private readonly CanvasSurfaceView _surface;
        private readonly bool _ownsDocument;

        internal Board(CanvasDocumentViewModel document, CanvasAnnouncer announcer, bool ownsDocument)
        {
            Document = document;
            Announcer = announcer;
            _ownsDocument = ownsDocument;
            var surface = new CanvasSurfaceView { Model = document };
            _surface = surface;
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

        internal Window Window => _window;

        /// <summary>The pane's surface — the navigator's presenter once it
        /// has focus, which a hosted fact attaches by hand.</summary>
        internal CanvasSurfaceView Surface => _surface;

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

        /// <summary>The pane shows another document (a tab switch rebinds
        /// the surface).</summary>
        internal void Show(CanvasDocumentViewModel document)
        {
            _surface.Model = document;
            document.ShowSurface(CanvasSurfaceKind.Visual);
            _window.UpdateLayout();
        }

        /// <summary>A fixed zoom with the origin at the view's corner: the
        /// board opens fitted to its content, which a fact whose premise is
        /// a length against the view must not depend on.</summary>
        internal void ZoomTo(double zoom)
        {
            Renderer.Engine.CommitViewport(view => view.WithZoom(zoom, 0, 0).PannedTo(0, 0));
            Settle();
        }

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

    private static FrameworkElement FindByAutomationId(DependencyObject root, string id)
    {
        var pending = new Stack<DependencyObject>([root]);
        while (pending.Count > 0)
        {
            DependencyObject next = pending.Pop();
            if (next is FrameworkElement element && AutomationProperties.GetAutomationId(element) == id)
            {
                return element;
            }
            for (int index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(next); index++)
            {
                pending.Push(System.Windows.Media.VisualTreeHelper.GetChild(next, index));
            }
        }
        throw new Xunit.Sdk.XunitException($"no element with AutomationId {id}.");
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
