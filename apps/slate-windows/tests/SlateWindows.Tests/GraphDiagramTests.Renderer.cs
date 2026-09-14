// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using SlateWindows.Canvas;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR D (#746), rules T and V (contracts D-8, D-9, D-10, D-13's
/// renderer half; D-3's visible-set facts; D-4's fit fact): tier A's
/// complete peers named by core's renders, tier B's one summary over a
/// synthetic topology through the model's own seams, the labels, the tokens
/// and the ring styles, the hit grid, the four viewport verbs on the
/// surface's presenter seam, the fit, and the scroll into view. Every fact
/// hosts the surface in a hidden window over a fixture vault.
/// </summary>
public sealed partial class GraphDiagramTests
{
    /// <summary>The surface in Diagram mode with a live, settled model whose
    /// first epoch landed; the renderer laid out.</summary>
    private static (GraphSurfaceView Surface, HostedWindow Window, GraphDiagramView Diagram, GraphDiagramModel Model) LiveDiagram(Host host, GraphDocumentViewModel document)
    {
        GraphSurfaceView surface = SurfaceFor(host, document);
        HostedWindow window = HostInWindow(surface);
        Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
        GraphDiagramModel model = SettledModel(host, document);
        window.UpdateLayout();
        GraphDiagramView diagram = surface.DiagramForTests;
        Assert.True(PumpedDispatcher.PumpUntil(() => diagram.Entries.Count == model.NodeIds.Length, TimeSpan.FromSeconds(10)), "the first epoch never landed on the renderer");
        window.UpdateLayout();
        return (surface, window, diagram, model);
    }

    private static List<GraphNodeAutomationPeer> NodePeers(GraphDiagramView diagram) =>
        [.. diagram.PeersInOrder().Cast<GraphNodeAutomationPeer>()];

    private static GraphDiagramAutomationPeer ContainerPeer(GraphDiagramView diagram) =>
        (GraphDiagramAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(diagram);

    private static string RowLine(GraphDocumentViewModel document, GraphTopologyNode entry) =>
        Render(new GraphA11yEvent.GraphRow(document.Verbosity, GraphDocumentViewModel.RowCopyOf(entry)));

    private static string NeighboursHelp(GraphTopologyNode entry)
    {
        string rendered = Render(new GraphA11yEvent.GraphNeighborsContent([.. entry.Neighbors.Select(n => n.Label)]));
        return rendered.Length == 0 ? string.Empty : GraphPhrase.ConnectsToPrefix + rendered;
    }

    private static Point ViewCentre(GraphDiagramView diagram, GraphDiagramModel model, ulong id)
    {
        GraphPoint point = model.Positions[id];
        CanvasViewportState viewport = diagram.Viewport;
        return new Point((point.X * viewport.Zoom) + viewport.PanX, (point.Y * viewport.Zoom) + viewport.PanY);
    }

    /// <summary>A synthetic topology of <paramref name="count"/> notes on a
    /// grid, installed through the model's own seams (D-9: no vault of
    /// 1,501 notes): the read, the frame, the topology, the epoch's landing.</summary>
    private static void InstallSyntheticTopology(GraphDocumentViewModel document, GraphDiagramModel model, int count)
    {
        var ids = new ulong[count];
        var nodes = new GraphNode[count];
        var entries = new GraphTopologyNode[count];
        var positions = new float[count * 2];
        for (int i = 0; i < count; i++)
        {
            ulong id = (ulong)(100_000 + i);
            ids[i] = id;
            string label = $"synthetic{i}";
            nodes[i] = new GraphNode(id, "p:" + label + ".md", label + ".md", label, GraphNodeKind.Note, 0, 0, 0, 0, 0, true, 0, null);
            entries[i] = new GraphTopologyNode(id, "p:" + label + ".md", label, label + ".md", GraphNodeKind.Note, 0, 0, 0, 0, 0, true, 12, null, i < 10, []);
            positions[2 * i] = (i % 50) * 20f;
            positions[(2 * i) + 1] = (i / 50) * 20f;
        }
        model.Adopt(new GraphDiagramTopologyRead(ids, [], nodes, model.Generation));
        model.AdoptFrame(new LayoutFrame(positions, 1, true, model.Generation));
        model.Topology = new GraphTopology(model.Generation, (ulong)count, entries, []);
        document.RaiseDiagramTopologyChangedForTests();
    }

    private static string TierEnteredLine() => Render(new GraphA11yEvent.GraphTierEntered());

    // --- Term T6's theme arm (IPH-3-1) ------------------------------------------------

    /// <summary>Term T6 / D-10 (IPH-3-1): a hosted renderer is subscribed to
    /// the theme's change and repaints its visuals on it — no frame, epoch,
    /// size or viewport moved — and leaving the tree releases the
    /// subscription. The arm is driven on THIS renderer (TGD-12): the
    /// manager's static event is never raised in the test process, where
    /// its audience is every subscriber other facts left on their own
    /// threads.</summary>
    [Fact]
    public void AThemeChangeRedrawsTheDiagramAndUnloadReleasesIt()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-theme-redraw");
            GraphDocumentViewModel document = host.Open();
            (_, HostedWindow window, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            Assert.True(diagram.IsLoaded);
            Assert.True(diagram.IsSubscribedToThemeForTests, "a hosted renderer must be subscribed to the theme's change");
            int before = diagram.RedrawsForTests;
            int fits = diagram.FitsForTests;
            diagram.RaiseThemeChangedForTests();
            Assert.Equal(before + 1, diagram.RedrawsForTests);
            Assert.Equal(fits, diagram.FitsForTests);
            diagram.RaiseThemeChangedForTests();
            Assert.Equal(before + 2, diagram.RedrawsForTests);
            // The window's close unloads the surface; Unloaded is a broadcast
            // the dispatcher delivers after the close.
            window.Dispose();
            Assert.True(PumpedDispatcher.PumpUntil(() => !diagram.IsLoaded, TimeSpan.FromSeconds(5)), "the window's close never unloaded the renderer");
            Assert.False(diagram.IsSubscribedToThemeForTests, "an unloaded renderer must be released from the theme's change");
        });
    }

    // --- D-8: tier A's complete peers (Term T2) ---------------------------------------

    [Fact]
    public void EveryVisibleNodeHasAButtonPeerNamedByTheRowCopyWithItsNeighboursAsHelpText()
    {
        RunSta(() =>
        {
            using var host = new Host(5, "diagram-peers-complete");
            _ = host.Session.CreateExclusive("solo.md", "# Solo\n\nNo links here.\n");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            List<GraphNodeAutomationPeer> peers = NodePeers(diagram);
            Assert.Equal(diagram.VisibleIds.Count, peers.Count);
            Assert.Equal(model.NodeIds.Length, peers.Count);
            Assert.Equal(diagram.VisibleIds, peers.Select(p => p.Id));
            foreach (GraphNodeAutomationPeer peer in peers)
            {
                GraphTopologyNode entry = diagram.Entries[peer.Id];
                Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());
                Assert.Equal(RowLine(document, entry), peer.GetName());
                Assert.Equal(NeighboursHelp(entry), peer.GetHelpText());
                Assert.Equal("GraphNode:" + entry.StableKey, peer.GetAutomationId());
                Assert.False(peer.IsKeyboardFocusable());
                Assert.Equal(string.Empty, peer.GetItemStatus());
            }
            // Two names equal core's renders at the live verbosity; the isolated
            // note's HelpText is EMPTY (core renders nothing for no neighbours).
            GraphNodeAutomationPeer solo = peers.Single(p => diagram.Entries[p.Id].Path == "solo.md");
            Assert.Equal(string.Empty, solo.GetHelpText());
            GraphNodeAutomationPeer linked = peers.First(p => diagram.Entries[p.Id].Neighbors.Length > 0);
            Assert.StartsWith(GraphPhrase.ConnectsToPrefix, linked.GetHelpText(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void APanNeverDropsAPeer()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-peers-pan");
            GraphDocumentViewModel document = host.Open();
            (_, HostedWindow window, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            List<GraphNodeAutomationPeer> before = NodePeers(diagram);
            diagram.PanBy(50_000, 50_000);
            window.UpdateLayout();
            List<GraphNodeAutomationPeer> after = NodePeers(diagram);
            Assert.Equal(before.Count, after.Count);
            Assert.All(after, peer => Assert.True(peer.IsOffscreen()));
            Assert.Equal(before, after);
        });
    }

    [Fact]
    public void APeersRectangleFollowsTheViewportAtReadTime()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-peers-rect");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, HostedWindow window, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.ActualSize));
            GraphNodeAutomationPeer peer = NodePeers(diagram)[0];
            Rect before = peer.GetBoundingRectangle();
            Assert.False(before.IsEmpty);
            Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.ZoomIn));
            window.UpdateLayout();
            Rect after = peer.GetBoundingRectangle();
            Assert.InRange(after.Width, (before.Width * CanvasViewportState.ZoomStep) - 1, (before.Width * CanvasViewportState.ZoomStep) + 1);
            Assert.NotEqual(before.X, after.X);
        });
    }

    [Fact]
    public void AVerbosityChangeRenamesEveryPeerWithoutALoad()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-peers-verbosity");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            int loads = host.Workspace.GraphLoadsForTests;
            int layouts = document.CrossingsForTests["start_graph_layout"];
            List<GraphNodeAutomationPeer> peers = NodePeers(diagram);
            string[] before = [.. peers.Select(p => p.GetName())];
            GraphVerbositySpec other = host.Workspace.GraphPreferences.Choices.First(c => c.Spec.Verbosity != host.Workspace.GraphPreferences.Verbosity).Spec;
            host.Workspace.GraphPreferences.SetVerbosityCommand.Execute(other.Tag);
            host.Settle(document);
            string[] after = [.. peers.Select(p => p.GetName())];
            Assert.NotEqual(before, after);
            Assert.Equal([.. peers.Select(p => RowLine(document, diagram.Entries[p.Id]))], after);
            Assert.Equal(loads, host.Workspace.GraphLoadsForTests);
            Assert.Equal(layouts, document.CrossingsForTests["start_graph_layout"]);
        });
    }

    [Fact]
    public void ThePeerIsIdentityStableAcrossEpochsRefreshesAndViewportChangesWithinAModel()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-peers-identity");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, HostedWindow window, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            ulong id = diagram.VisibleIds[0];
            GraphNodeAutomationPeer peer = diagram.PeerFor(id)!;
            // A new epoch (the groups), a viewport change, a refresh.
            document.ViewState.Groups = [new GraphGroup("note", GraphColorToken.Blue, GraphRingStyle.Solid)];
            host.Settle(document);
            Assert.Same(peer, diagram.PeerFor(id));
            _ = surface.ViewportCommand(GraphViewportVerb.ZoomIn);
            window.UpdateLayout();
            Assert.Same(peer, diagram.PeerFor(id));
            _ = host.Session.CreateExclusive("iota.md", "# Iota\n\n[[note0]]\n");
            document.Probe();
            host.Settle(document);
            WaitForTheSettle(host, document, model);
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.Entries.Count == model.NodeIds.Length, TimeSpan.FromSeconds(10)));
            Assert.Same(model, document.DiagramModel);
            Assert.Same(peer, diagram.PeerFor(id));
            Assert.Equal(4, NodePeers(diagram).Count);
        });
    }

    [Fact]
    public void AModelReplacementRecreatesThePeers()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-peers-replaced");
            GraphDocumentViewModel document = host.Open();
            (_, HostedWindow window, GraphDiagramView diagram, GraphDiagramModel first) = LiveDiagram(host, document);
            List<GraphNodeAutomationPeer> before = NodePeers(diagram);
            // A backend-filter change rebuilds (Term G6): a new model, new peers.
            document.ViewState.Filter = new GraphFilter(IncludeAttachments: true, IncludeGhosts: true, OrphansOnly: false);
            GraphDiagramModel second = SettledModel(host, document);
            Assert.NotSame(first, second);
            Assert.True(PumpedDispatcher.PumpUntil(() => ReferenceEquals(diagram.Diagram, second) && diagram.Entries.Count == second.NodeIds.Length, TimeSpan.FromSeconds(10)));
            window.UpdateLayout();
            List<GraphNodeAutomationPeer> after = NodePeers(diagram);
            Assert.Equal(before.Count, after.Count);
            Assert.All(after, peer => Assert.DoesNotContain(before, old => ReferenceEquals(old, peer)));
        });
    }

    [Fact]
    public void ItemStatusReadsPinnedWhilePinned()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-peers-pinned");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            GraphNodeAutomationPeer peer = NodePeers(diagram)[0];
            Assert.Equal(string.Empty, peer.GetItemStatus());
            GraphPoint at = model.Positions[peer.Id];
            Assert.True(model.TogglePin(peer.Id, (float)at.X, (float)at.Y));
            Assert.Equal(GraphPhrase.PinnedStatus, peer.GetItemStatus());
            Assert.True(model.TogglePin(peer.Id, (float)at.X, (float)at.Y));
            Assert.Equal(string.Empty, peer.GetItemStatus());
        });
    }

    [Fact]
    public void TheContainerExposesSelectionAndTheZoomValue()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-container");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            GraphDiagramAutomationPeer container = ContainerPeer(diagram);
            Assert.Equal(AutomationControlType.Group, container.GetAutomationControlType());
            Assert.Equal(GraphPhrase.DiagramName, container.GetName());
            Assert.Equal("GraphDiagram", container.GetAutomationId());
            var selection = (ISelectionProvider)container;
            Assert.False(selection.CanSelectMultiple);
            Assert.False(selection.IsSelectionRequired);
            Assert.Empty(selection.GetSelection());
            var value = (IValueProvider)container;
            Assert.True(value.IsReadOnly);
            Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.ActualSize));
            Assert.Equal("Zoom 100 percent", value.Value);
            Assert.Equal(100u, diagram.Viewport.ZoomPercent);
            ulong id = diagram.VisibleIds[0];
            Assert.True(diagram.SelectNode(id, announce: false));
            Assert.Single(selection.GetSelection());
            Assert.Equal(id, diagram.SelectedId);
            Assert.Equal(diagram.PeersInOrder().Count, container.GetChildren().Count);
            // The collapsed board exposes no children — the canvas board's
            // rule: no offscreen, pattern-less Buttons in the tree.
            diagram.Visibility = Visibility.Collapsed;
            Assert.Empty(container.GetChildren() ?? []);
            diagram.Visibility = Visibility.Visible;
            Assert.NotEmpty(container.GetChildren());
        });
    }

    [Fact]
    public void SelectThroughThePatternSelectsAndAnnouncesAndRemoveClears()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-peers-select");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            host.GraphLines.Clear();
            GraphNodeAutomationPeer peer = NodePeers(diagram)[1];
            var item = (ISelectionItemProvider)peer;
            Assert.False(item.IsSelected);
            item.Select();
            Assert.Equal(diagram.Entries[peer.Id].StableKey, document.ViewState.SelectedKey);
            Assert.True(item.IsSelected);
            Assert.Equal(peer.Id, diagram.SelectedId);
            // The row line through the document's seam (the navigation class).
            Assert.True(PumpedDispatcher.PumpUntil(() => host.GraphLines.Count > 0, TimeSpan.FromSeconds(5)), "the row line never fired");
            Assert.Equal([RowLine(document, diagram.Entries[peer.Id])], host.GraphLines);
            item.RemoveFromSelection();
            Assert.Null(document.ViewState.SelectedKey);
            Assert.False(item.IsSelected);
            Assert.Null(diagram.SelectedId);
        });
    }

    [Fact]
    public void AddToSelectionWithAnotherSelectedThrows()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-peers-add");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            List<GraphNodeAutomationPeer> peers = NodePeers(diagram);
            var first = (ISelectionItemProvider)peers[0];
            var second = (ISelectionItemProvider)peers[1];
            first.AddToSelection();
            Assert.True(first.IsSelected);
            _ = Assert.Throws<InvalidOperationException>(second.AddToSelection);
            Assert.True(first.IsSelected);
            // Adding the selected one again is a no-op.
            first.AddToSelection();
            Assert.Equal(peers[0].Id, diagram.SelectedId);
        });
    }

    // --- D-9: tier B (Term T3) ---------------------------------------------------------

    [Fact]
    public void TheTierBoundaryIsInclusiveAt1500AndSwitchesAt1501()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-tier-boundary");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            int threshold = (int)GraphCoreConstants.Once.TierBThreshold;
            Assert.Equal(1500, threshold);
            InstallSyntheticTopology(document, model, threshold);
            Assert.False(diagram.IsTierB);
            Assert.Equal(threshold, NodePeers(diagram).Count);
            InstallSyntheticTopology(document, model, threshold + 1);
            Assert.True(diagram.IsTierB);
            Assert.Single(diagram.PeersInOrder());
            Assert.IsType<GraphTierSummaryAutomationPeer>(diagram.PeersInOrder()[0]);
        });
    }

    [Fact]
    public void TierBExposesOneSummaryPeerNamedByCoresRenderWhoseInvokeSwitchesToTable()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-tier-summary");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            InstallSyntheticTopology(document, model, 1501);
            var summary = Assert.IsType<GraphTierSummaryAutomationPeer>(Assert.Single(diagram.PeersInOrder()));
            Assert.Equal(AutomationControlType.Button, summary.GetAutomationControlType());
            Assert.Equal(Render(new GraphA11yEvent.GraphTierSummary(1501)), summary.GetName());
            Assert.Equal(GraphPhrase.SwitchToTable, summary.GetHelpText());
            Assert.False(summary.IsKeyboardFocusable());
            ((IInvokeProvider)summary).Invoke();
            Assert.Equal(GraphSurfaceMode.Table, document.ViewState.Mode);
            Assert.False(document.HasLiveDiagram);
        });
    }

    [Fact]
    public void TierEnteredSpeaksOnceOnTheEdgeAndNeverOnBToA()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-tier-entered");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            host.GraphLines.Clear();
            InstallSyntheticTopology(document, model, 1501);
            Assert.True(PumpedDispatcher.PumpUntil(() => host.GraphLines.Count > 0, TimeSpan.FromSeconds(5)), "the tier line never fired");
            Assert.Equal([TierEnteredLine()], host.GraphLines);
            // A rebuild that stays in B: nothing more.
            InstallSyntheticTopology(document, model, 1600);
            host.Settle(document);
            Assert.Equal([TierEnteredLine()], host.GraphLines);
            // B → A: nothing.
            InstallSyntheticTopology(document, model, 4);
            host.Settle(document);
            Assert.False(diagram.IsTierB);
            Assert.Equal([TierEnteredLine()], host.GraphLines);
        });
    }

    [Fact]
    public void ANameFilterCollapsesTierBToTheVisibleSet()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-tier-filter");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            InstallSyntheticTopology(document, model, 1501);
            Assert.True(diagram.IsTierB);
            // The VISIBLE set decides: a topology of three (the needle's) is
            // tier A — three peers, not a summary.
            InstallSyntheticTopology(document, model, 3);
            Assert.False(diagram.IsTierB);
            Assert.Equal(3, NodePeers(diagram).Count);
            Assert.Null(diagram.SummaryPeer);
            // The ids that left the visible set hold no peer (Term T2: dropped
            // when the id leaves).
            Assert.Null(diagram.PeerFor(100_500));
            Assert.NotNull(diagram.PeerFor(100_001));
        });
    }

    [Fact]
    public void TierBDrawsOneVisualAndKeepsTheRingAndTheHitTest()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-tier-hit");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, HostedWindow window, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            InstallSyntheticTopology(document, model, 1501);
            Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.ActualSize));
            window.UpdateLayout();
            Assert.True(diagram.IsTierB);
            Assert.Empty(diagram.LabelledForTests);
            Assert.Null(diagram.FillKeyForTests(diagram.VisibleIds[0]));
            // The hit test still answers at a node's centre; the ring still follows the key.
            ulong id = diagram.VisibleIds[7];
            Assert.Equal(id, diagram.HitTest(ViewCentre(diagram, model, id)));
            // A synthetic node is not in the table's snapshot, so SelectRow
            // refuses it (DR-4); the ring follows the shared KEY regardless.
            Assert.False(diagram.SelectNode(id, announce: false));
            document.ViewState.SelectedKey = diagram.Entries[id].StableKey;
            Assert.Equal(id, diagram.SelectedId);
        });
    }

    // --- D-10: labels, styling, edges, the hit test (Terms T4–T7) -----------------------

    [Fact]
    public void ALabelDrawsOnlyForACoreLabeledNodeAtOrAboveTheFadeZoom()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-labels");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, HostedWindow window, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.ActualSize));
            window.UpdateLayout();
            Assert.True(diagram.Viewport.Zoom >= document.DiagramDisplay.TextFadeZoom);
            Assert.True(diagram.LabelsShownForTests);
            Assert.Equal([.. diagram.VisibleIds.Where(id => diagram.Entries[id].Labeled)], [.. diagram.LabelledForTests.OrderBy(id => Array.IndexOf([.. diagram.VisibleIds], id))]);
            Assert.NotEmpty(diagram.LabelledForTests);
            // Below the fade zoom: no labels.
            while (diagram.Viewport.Zoom >= document.DiagramDisplay.TextFadeZoom && diagram.Viewport.Zoom > CanvasViewportState.MinZoom)
            {
                _ = surface.ViewportCommand(GraphViewportVerb.ZoomOut);
            }
            Assert.False(diagram.LabelsShownForTests);
            Assert.Empty(diagram.LabelledForTests);
        });
    }

    [Fact]
    public void TheNodeSizeMultiplierScalesTheDrawnDiameter()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-diameter-multiplier");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, HostedWindow window, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.ActualSize));
            window.UpdateLayout();
            double multiplier = document.DiagramDisplay.NodeSizeMultiplier;
            foreach (ulong id in diagram.VisibleIds)
            {
                GraphTopologyNode entry = diagram.Entries[id];
                Assert.Equal(entry.Diameter * multiplier, diagram.ScaledDiameter(id), 6);
                Assert.Equal(entry.Diameter * multiplier * diagram.Viewport.Zoom, diagram.NodeViewRect(id).Width, 6);
            }
        });
    }

    [Fact]
    public void AGroupedNodeTakesTheTokenBrushAndItsRingStyle()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-group-style");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            ulong id = diagram.VisibleIds[0];
            Assert.Equal(GraphDiagramView.NoteBrushKey, diagram.FillKeyForTests(id));
            document.ViewState.Groups = [new GraphGroup("note", GraphColorToken.Teal, GraphRingStyle.Dashed)];
            host.Settle(document);
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.Entries[id].Group is not null, TimeSpan.FromSeconds(5)), "the group's epoch never landed");
            Assert.Equal(GraphDiagramView.GroupBrushKeys[(int)GraphColorToken.Teal], diagram.FillKeyForTests(id));
            (double width, double[]? dash) = diagram.RingStyleForTests(id)!.Value;
            Assert.Equal(3, width);
            Assert.Equal([4, 2], dash);
            document.ViewState.Groups = [new GraphGroup("note", GraphColorToken.Purple, GraphRingStyle.Double)];
            host.Settle(document);
            Assert.Equal(GraphDiagramView.GroupBrushKeys[(int)GraphColorToken.Purple], diagram.FillKeyForTests(id));
            Assert.Equal(4, diagram.RingStyleForTests(id)!.Value.Width);
            Assert.Null(diagram.RingStyleForTests(id)!.Value.Dash);
        });
    }

    [Fact]
    public void AGroupedRingIsHeavierThanUngroupedEvenWhenSolid()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-group-solid");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            ulong id = diagram.VisibleIds[0];
            Assert.Equal(1.5, diagram.RingStyleForTests(id)!.Value.Width);
            document.ViewState.Groups = [new GraphGroup("note", GraphColorToken.Red, GraphRingStyle.Solid)];
            host.Settle(document);
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.Entries[id].Group is not null, TimeSpan.FromSeconds(5)));
            Assert.Equal(3, diagram.RingStyleForTests(id)!.Value.Width);
            Assert.Null(diagram.RingStyleForTests(id)!.Value.Dash);
            Assert.Equal(GraphDiagramView.GroupBrushKeys[0], diagram.FillKeyForTests(id));
        });
    }

    [Fact]
    public void UngroupingClearsTheDashAndTheWidth()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-ungroup");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            ulong id = diagram.VisibleIds[0];
            document.ViewState.Groups = [new GraphGroup("note", GraphColorToken.Green, GraphRingStyle.Dotted)];
            host.Settle(document);
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.Entries[id].Group is not null, TimeSpan.FromSeconds(5)));
            Assert.Equal([1, 2], diagram.RingStyleForTests(id)!.Value.Dash);
            document.ViewState.Groups = [];
            host.Settle(document);
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.Entries[id].Group is null, TimeSpan.FromSeconds(5)));
            Assert.Equal(1.5, diagram.RingStyleForTests(id)!.Value.Width);
            Assert.Null(diagram.RingStyleForTests(id)!.Value.Dash);
            Assert.Equal(GraphDiagramView.NoteBrushKey, diagram.FillKeyForTests(id));
        });
    }

    [Fact]
    public void TheDiameterIsTheTopologyEntrys()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-diameter");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            foreach (ulong id in diagram.VisibleIds)
            {
                GraphTopologyNode entry = diagram.Entries[id];
                // Core's curve through the record, the same curve the scalar query gives.
                Assert.Equal(SlateUniffiMethods.GraphNodeDiameter(entry.InLinks), entry.Diameter, 6);
                Assert.Equal(entry.Diameter * document.DiagramDisplay.NodeSizeMultiplier, diagram.ScaledDiameter(id), 6);
            }
        });
    }

    [Fact]
    public void TheGridHitTestFindsTheNodeUnderAPoint()
    {
        RunSta(() =>
        {
            using var host = new Host(6, "diagram-hit-test");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, HostedWindow window, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.ActualSize));
            window.UpdateLayout();
            Assert.True(diagram.GridCellCountForTests >= 1);
            foreach (ulong id in diagram.VisibleIds)
            {
                Assert.Equal(id, diagram.HitTest(ViewCentre(diagram, model, id)));
            }
            // Far from every node: nothing.
            Assert.Null(diagram.HitTest(new Point(-100_000, -100_000)));
        });
    }

    // --- D-3: the visible set is the table's (Term T1) --------------------------------------

    [Fact]
    public void TheNeedleAndTheKindOverlayNarrowTheVisibleSetToTheTables()
    {
        RunSta(() =>
        {
            using var host = new Host(5, "diagram-visible-set");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            Assert.Equal(5, diagram.VisibleIds.Count);
            ulong[] before = [.. diagram.VisibleIds];
            host.Workspace.GraphNavigator.SetNameQuery("note1");
            host.Settle(document);
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.VisibleIds.Count == 1, TimeSpan.FromSeconds(5)), "the needle's epoch never landed");
            // Term T2: the peers of the ids the needle hid are DROPPED; the
            // shown one keeps its peer.
            Assert.All(before.Where(id => id != diagram.VisibleIds[0]), id => Assert.Null(diagram.PeerFor(id)));
            Assert.NotNull(diagram.PeerFor(diagram.VisibleIds[0]));
            // The visible ids equal core's visibility and the table's rows' keys.
            var query = new GraphVisibilityQuery(document.ViewState.Filter, document.ViewState.NameQuery, document.ViewState.KindOnly);
            Assert.Equal(host.Session.GraphVisibility(query).Ids, diagram.VisibleIds);
            Assert.Equal([.. document.Publication.Rows.Select(r => r.StableKey)], [.. diagram.VisibleIds.Select(id => diagram.Entries[id].StableKey)]);
            Assert.Same(model, document.DiagramModel);
            // The overlay narrows the same way (the same predicate).
            host.Workspace.GraphNavigator.ClearNameQuery();
            host.Settle(document);
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.VisibleIds.Count == 5, TimeSpan.FromSeconds(5)));
        });
    }

    [Fact]
    public void NeighbourContentExcludesFilteredOutNodes()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-neighbours-filtered");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            GraphNodeAutomationPeer note1 = NodePeers(diagram).Single(p => diagram.Entries[p.Id].Path == "note1.md");
            Assert.NotEqual(string.Empty, note1.GetHelpText());
            host.Workspace.GraphNavigator.SetNameQuery("note1");
            host.Settle(document);
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.VisibleIds.Count == 1, TimeSpan.FromSeconds(5)));
            // Its neighbours are hidden: the entry lists none, the HelpText is empty.
            Assert.Empty(diagram.Entries[note1.Id].Neighbors);
            Assert.Equal(string.Empty, note1.GetHelpText());
        });
    }

    // --- D-4: the first non-empty frame fits once -------------------------------------------

    [Fact]
    public void TheFirstNonEmptyFrameFitsOnceAndLaterFramesDoNot()
    {
        RunSta(() =>
        {
            using var host = new Host(6, "diagram-first-fit");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            Assert.True(model.Driver.FramesAppliedForTests >= 1);
            Assert.Equal(1, diagram.FitsForTests);
            // A re-settle applies more frames: no second fit.
            model.Driver.StartSettle();
            WaitForTheSettle(host, document, model);
            Assert.Equal(1, diagram.FitsForTests);
        });
    }

    // --- D-13: the viewport verbs on the presenter seam (rule V) -----------------------------

    [Fact]
    public void ZoomInOutAndActualSizeAreCentrePreservingAndClampedAndSpeakTheZoom()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-zoom-verbs");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, HostedWindow window, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            var actual = Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.ActualSize));
            Assert.Equal((100u, false), (actual.Percent, actual.Fit));
            // The view centre maps to the same layout point across a zoom.
            CanvasViewportState before = diagram.Viewport;
            double centreX = diagram.ActualWidth / 2;
            double centreY = diagram.ActualHeight / 2;
            double layoutX = (centreX - before.PanX) / before.Zoom;
            double layoutY = (centreY - before.PanY) / before.Zoom;
            var zoomedIn = Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.ZoomIn));
            Assert.Equal(125u, zoomedIn.Percent);
            CanvasViewportState after = diagram.Viewport;
            Assert.Equal(layoutX, (centreX - after.PanX) / after.Zoom, 6);
            Assert.Equal(layoutY, (centreY - after.PanY) / after.Zoom, 6);
            var zoomedOut = Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.ZoomOut));
            Assert.Equal(100u, zoomedOut.Percent);
            // Clamped at the canvas's bounds.
            GraphViewportOutcome last = GraphViewportOutcome.Refused;
            for (int i = 0; i < 20; i++)
            {
                last = surface.ViewportCommand(GraphViewportVerb.ZoomIn);
            }
            Assert.Equal((uint)Math.Round(CanvasViewportState.MaxZoom * 100), Assert.IsType<GraphViewportOutcome.Zoomed>(last).Percent);
            for (int i = 0; i < 40; i++)
            {
                last = surface.ViewportCommand(GraphViewportVerb.ZoomOut);
            }
            Assert.Equal((uint)Math.Round(CanvasViewportState.MinZoom * 100), Assert.IsType<GraphViewportOutcome.Zoomed>(last).Percent);
            window.UpdateLayout();
            Assert.Equal("Zoom 10 percent", ((IValueProvider)ContainerPeer(diagram)).Value);
        });
    }

    [Fact]
    public void FitFramesTheVisibleNodesAndSpeaksTheFitLine()
    {
        RunSta(() =>
        {
            using var host = new Host(5, "diagram-fit");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, HostedWindow window, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            diagram.PanBy(3000, 3000);
            var fit = Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.FitGraph));
            Assert.True(fit.Fit);
            Assert.Equal(diagram.Viewport.ZoomPercent, fit.Percent);
            window.UpdateLayout();
            var view = new Rect(0, 0, diagram.ActualWidth, diagram.ActualHeight);
            foreach (ulong id in diagram.VisibleIds)
            {
                Assert.True(view.Contains(ViewCentre(diagram, model, id)), "a visible node's centre lies outside the fitted view");
            }
            // A hidden node is excluded from the bounds: the needle's single
            // node fits alone (its inflated bounds), at a larger zoom than the five.
            uint five = fit.Percent;
            host.Workspace.GraphNavigator.SetNameQuery("note2");
            host.Settle(document);
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.VisibleIds.Count == 1, TimeSpan.FromSeconds(5)));
            var single = Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.FitGraph));
            Assert.True(single.Fit);
            Assert.True(single.Percent >= five);
            Assert.True(view.Contains(ViewCentre(diagram, model, diagram.VisibleIds[0])));
        });
    }

    [Fact]
    public void FitOnAnEmptySetSpeaksTheUnchangedPercent()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-fit-empty");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            host.Workspace.GraphNavigator.SetNameQuery("nothing-matches-this");
            host.Settle(document);
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.VisibleIds.Count == 0, TimeSpan.FromSeconds(5)));
            uint before = diagram.Viewport.ZoomPercent;
            var fit = Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.FitGraph));
            Assert.True(fit.Fit);
            Assert.Equal(before, fit.Percent);
            Assert.Equal(before, diagram.Viewport.ZoomPercent);
        });
    }

    [Fact]
    public void AVerbInTableModeIsRefusedOnTheSurface()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-verb-refused");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView surface = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(surface);
            Assert.Equal(GraphSurfaceMode.Table, document.ViewState.Mode);
            Assert.Same(GraphViewportOutcome.Refused, surface.ViewportCommand(GraphViewportVerb.ZoomIn));
            Assert.Same(GraphViewportOutcome.Refused, surface.ViewportCommand(GraphViewportVerb.FitGraph));
            // Diagram mode with no live model yet (the build in flight): refused too.
            using var park = new Park();
            document.FetchGateForTests = park.Hit;
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            park.WaitReached();
            Assert.Same(GraphViewportOutcome.Refused, surface.ViewportCommand(GraphViewportVerb.ZoomIn));
            document.FetchGateForTests = null;
            park.Release();
            _ = SettledModel(host, document);
            Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.ZoomIn));
        });
    }

    [Fact]
    public void ScrollIntoViewKeepsTheSelectionInsideTheMargin()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-scroll-into-view");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, HostedWindow window, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.ActualSize));
            ulong id = diagram.VisibleIds[0];
            diagram.PanBy(-5000, -5000);
            window.UpdateLayout();
            Assert.True(diagram.PeerFor(id)!.IsOffscreen());
            int scrolls = diagram.ScrollsForTests;
            Assert.True(diagram.SelectNode(id, announce: false));
            Assert.Equal(scrolls + 1, diagram.ScrollsForTests);
            Point centre = ViewCentre(diagram, model, id);
            Assert.InRange(centre.X, GraphDiagramView.ScrollMargin - 0.5, diagram.ActualWidth - GraphDiagramView.ScrollMargin + 0.5);
            Assert.InRange(centre.Y, GraphDiagramView.ScrollMargin - 0.5, diagram.ActualHeight - GraphDiagramView.ScrollMargin + 0.5);
            // Already inside: no pan.
            Assert.True(diagram.SelectNode(id, announce: false));
            Assert.Equal(scrolls + 1, diagram.ScrollsForTests);
            // An OUTSIDE write of the key scrolls too, silently.
            diagram.PanBy(5000, 5000);
            host.GraphLines.Clear();
            ulong other = diagram.VisibleIds[1];
            Assert.True(document.SelectRow(diagram.Entries[other].StableKey));
            Assert.Equal(other, diagram.SelectedId);
            Assert.Equal(scrolls + 2, diagram.ScrollsForTests);
            host.Settle(document);
            Assert.Empty(host.GraphLines);
        });
    }
}
