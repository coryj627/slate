// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR D (#746), rule N (contracts D-11, D-12, D-14; D-13's Term V6
/// fact; D-5's stable-key fact): the derived selection, the guarded select,
/// core's spatial and structural steps, the type-ahead, Enter, the row line
/// on a keyboard move, the silent outside write, the click and the
/// double-click, the wheel, the Escape ladder's rung 3, the tooltip; the
/// actions through the document (the table's plus Pin), the ghost's
/// admission, the stale node; the diagram's Where-am-I readback with the
/// zoom clause; the value and the clause reading one number.
/// </summary>
public sealed partial class GraphDiagramTests
{
    private static KeyEventArgs Press(GraphDiagramView diagram, Key key)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(diagram)!, 0, key)
        {
            RoutedEvent = Keyboard.KeyDownEvent,
        };
        diagram.RaiseEvent(args);
        return args;
    }

    private static string WhereAmILine(GraphA11yEvent.GraphWhereAmI @event) => Render(@event);

    private static string PinnedLine(bool pinned) => Render(new GraphA11yEvent.GraphPinned(pinned));

    // --- D-11: the selection and the keyboard (rule N) ---------------------------------

    [Fact]
    public void TheSelectionIsTheSharedKeysVisibleNode()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-selection-derived");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            ulong id = diagram.VisibleIds[1];
            string key = diagram.Entries[id].StableKey;
            // Present: its id.
            Assert.True(document.SelectRow(key));
            Assert.Equal(id, diagram.SelectedId);
            Assert.Equal(id, document.DiagramSelectedEntry()!.Id);
            // Hidden by the needle: none, the key kept (A-7).
            host.Workspace.GraphNavigator.SetNameQuery("note0");
            host.Settle(document);
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.VisibleIds.Count == 1, TimeSpan.FromSeconds(5)));
            Assert.Equal(key, document.ViewState.SelectedKey);
            Assert.Null(diagram.SelectedId);
            Assert.Null(document.DiagramSelectedEntry());
            host.Workspace.GraphNavigator.ClearNameQuery();
            host.Settle(document);
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.VisibleIds.Count == 4, TimeSpan.FromSeconds(5)));
            Assert.Equal(id, diagram.SelectedId);
            // Absent: none.
            document.ViewState.SelectedKey = "p:nowhere.md";
            Assert.Null(diagram.SelectedId);
        });
    }

    [Fact]
    public void ASelectWritesTheSharedKeyThroughTheDocumentsGuard()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-select-guard");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            ulong id = diagram.VisibleIds[0];
            Assert.True(diagram.SelectNode(id, announce: false));
            Assert.Equal(diagram.Entries[id].StableKey, document.ViewState.SelectedKey);
            // A node the table's snapshot does not know is refused (DR-4): a
            // synthetic entry seeded through the model's seams.
            InstallSyntheticTopology(document, model, 2);
            Assert.False(diagram.SelectNode(diagram.VisibleIds[0], announce: false));
            Assert.DoesNotContain(diagram.Entries.Values, e => e.StableKey == document.ViewState.SelectedKey);
            // A retired document refuses.
            host.Workspace.CloseActiveTabCommand.Execute(null);
            Assert.True(document.IsRetired);
            Assert.False(diagram.SelectNode(id, announce: false));
        });
    }

    [Fact]
    public void ArrowsStepSpatiallyNeighboursFirstThenFallBack()
    {
        RunSta(() =>
        {
            using var host = new Host(5, "diagram-arrows");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            Assert.True(diagram.Focus());
            Assert.Null(diagram.SelectedId);
            // No selection: the first visible node, no crossing.
            Assert.True(Press(diagram, Key.Right).Handled);
            Assert.Equal(diagram.VisibleIds[0], diagram.SelectedId);
            Assert.Equal(0, document.CrossingsForTests.GetValueOrDefault("graph_spatial_step"));
            // Injected positions: the selection at the origin, a neighbour to the
            // right, a non-neighbour further right — the neighbour wins; a step
            // with no candidate leaves the ring where it is.
            // Every note links back to note0, so note0 neighbours everything;
            // the origin is a node with a NON-neighbour among the visible set.
            ulong from = diagram.VisibleIds.First(id => diagram.VisibleIds.Any(o => o != id && diagram.Entries[id].Neighbors.All(n => n.Id != o)));
            GraphTopologyNode entry = diagram.Entries[from];
            // The neighbour with the FEWEST neighbours of its own (not the hub note0
            // every note links back to), so a non-neighbour of both exists.
            ulong neighbour = entry.Neighbors.Where(n => n.Id != from).OrderBy(n => diagram.Entries[n.Id].Neighbors.Length).First().Id;
            ulong other = diagram.VisibleIds.First(id => id != from && entry.Neighbors.All(n => n.Id != id));
            // A NEARER non-neighbour of both, in the Right direction: the
            // neighbours-first pass must beat it.
            ulong near = diagram.VisibleIds.First(id => id != from && id != other && entry.Neighbors.All(n => n.Id != id) && diagram.Entries[neighbour].Neighbors.All(n => n.Id != id));
            Assert.True(diagram.SelectNode(from, announce: false));
            var positions = new float[model.NodeIds.Length * 2];
            for (int i = 0; i < model.NodeIds.Length; i++)
            {
                ulong id = model.NodeIds[i];
                (float x, float y) = id == from ? (0f, 0f) : id == neighbour ? (100f, 0f) : id == other ? (-60f, 0f) : id == near ? (50f, 0f) : (0f, -300f - (10f * i));
                positions[2 * i] = x;
                positions[(2 * i) + 1] = y;
            }
            Assert.True(model.Driver.ApplyForTests(new LayoutFrame(positions, 1, true, model.Generation)));
            Assert.True(Press(diagram, Key.Right).Handled);
            // Right: the neighbour at 100 wins over the nearer non-neighbour at 50
            // (neighbours first).
            Assert.Equal(1, document.CrossingsForTests["graph_spatial_step"]);
            Assert.Equal(neighbour, diagram.SelectedId);
            // From the neighbour, Left: back to the origin through core.
            Assert.True(Press(diagram, Key.Left).Handled);
            Assert.Equal(2, document.CrossingsForTests["graph_spatial_step"]);
            Assert.Equal(from, diagram.SelectedId);
            // Left again: no neighbour that way — the FALLBACK over every visible
            // node finds the non-neighbour at -60.
            Assert.True(Press(diagram, Key.Left).Handled);
            Assert.Equal(3, document.CrossingsForTests["graph_spatial_step"]);
            Assert.Equal(other, diagram.SelectedId);
            // A direction with nothing there: one crossing, no move.
            Assert.True(Press(diagram, Key.Down).Handled);
            Assert.Equal(4, document.CrossingsForTests["graph_spatial_step"]);
            Assert.Equal(other, diagram.SelectedId);
        });
    }

    [Fact]
    public void TabWrapsStructurallyAndIsConsumedOnlyWithAVisibleSet()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-tab");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            Assert.True(diagram.Focus());
            ulong[] order = [.. diagram.VisibleIds];
            Assert.True(Press(diagram, Key.Tab).Handled);
            Assert.Equal(order[0], diagram.SelectedId);
            Assert.True(Press(diagram, Key.Tab).Handled);
            Assert.Equal(order[1], diagram.SelectedId);
            Assert.True(Press(diagram, Key.Tab).Handled);
            Assert.Equal(order[2], diagram.SelectedId);
            // Wraps.
            Assert.True(Press(diagram, Key.Tab).Handled);
            Assert.Equal(order[0], diagram.SelectedId);
            Assert.True(diagram.StructuralMove(forward: false));
            Assert.Equal(order[2], diagram.SelectedId);
            Assert.Equal(5, document.CrossingsForTests["graph_structural_step"]);
            // An EMPTY visible set: Tab is not consumed — the reader leaves.
            host.Workspace.GraphNavigator.SetNameQuery("nothing-matches");
            host.Settle(document);
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.VisibleIds.Count == 0, TimeSpan.FromSeconds(5)));
            Assert.False(Press(diagram, Key.Tab).Handled);
            Assert.Equal(5, document.CrossingsForTests["graph_structural_step"]);
        });
    }

    [Fact]
    public void TypeAheadJumpsByPrefixWithinASecond()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-type-ahead");
            _ = host.Session.CreateExclusive("solo.md", "# Solo\n\nAlone.\n");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            var now = new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);
            ulong ByLabel(string label) => diagram.Entries.Values.Single(e => e.Label == label).Id;
            Assert.True(diagram.TypeAhead("N", now));
            Assert.Equal(ByLabel("note0"), diagram.SelectedId);
            // Within the second the buffer grows: "n" + "ote2".
            Assert.True(diagram.TypeAhead("ote2", now.AddMilliseconds(500)));
            Assert.Equal(ByLabel("note2"), diagram.SelectedId);
            // After the second the buffer resets: "s" → solo.
            Assert.True(diagram.TypeAhead("s", now.AddSeconds(2)));
            Assert.Equal(ByLabel("solo"), diagram.SelectedId);
            // No match: consumed, the ring stays.
            Assert.True(diagram.TypeAhead("zzz", now.AddSeconds(4)));
            Assert.Equal(ByLabel("solo"), diagram.SelectedId);
        });
    }

    [Fact]
    public void EnterActivatesTheSelection()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-enter");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            var opened = new List<(string Path, WorkspaceOpenTarget Target)>();
            document.OpenRowFromSurface = (row, target) => opened.Add((row.Path!, target));
            Assert.True(diagram.Focus());
            // Nothing selected: Enter is not consumed.
            Assert.False(Press(diagram, Key.Enter).Handled);
            Assert.Empty(opened);
            ulong id = diagram.VisibleIds[1];
            Assert.True(diagram.SelectNode(id, announce: false));
            Assert.True(Press(diagram, Key.Enter).Handled);
            Assert.Equal([(diagram.Entries[id].Path!, WorkspaceOpenTarget.CurrentTab)], opened);
        });
    }

    [Fact]
    public void AKeyboardMoveSpeaksOneRowLineAndTheLandingSpeaksNothing()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-move-line");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, HostedWindow window, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            // The landing (Term M4 / F5): the renderer takes the keys silently.
            host.GraphLines.Clear();
            Assert.True(diagram.Focus());
            window.UpdateLayout();
            host.Settle(document);
            Assert.Empty(host.GraphLines);
            // One keyboard move: ONE GraphRow line at the live verbosity.
            Assert.True(Press(diagram, Key.Tab).Handled);
            Assert.True(PumpedDispatcher.PumpUntil(() => host.GraphLines.Count > 0, TimeSpan.FromSeconds(5)), "the row line never fired");
            host.Settle(document);
            Assert.Equal([RowLine(document, diagram.Entries[diagram.SelectedId!.Value])], host.GraphLines);
            Assert.True(surface.ProjectionHasFocus);
        });
    }

    [Fact]
    public void AnOutsideKeyWriteMovesTheRingSilently()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-outside-write");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            host.GraphLines.Clear();
            ulong id = diagram.VisibleIds[2];
            int redraws = diagram.RedrawsForTests;
            // The table in the other pane, a re-root: the shared key written
            // from OUTSIDE the diagram.
            Assert.True(document.SelectRow(diagram.Entries[id].StableKey));
            Assert.Equal(id, diagram.SelectedId);
            Assert.True(diagram.RedrawsForTests >= redraws);
            Assert.True(((ISelectionItemProvider)diagram.PeerFor(id)!).IsSelected);
            host.Settle(document);
            PumpedDispatcher.PumpUntil(() => false, TimeSpan.FromMilliseconds(300));
            Assert.Empty(host.GraphLines);
        });
    }

    [Fact]
    public void ClickSelectsAndDoubleClickActivates()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-click");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, HostedWindow window, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.ActualSize));
            window.UpdateLayout();
            var opened = new List<string>();
            document.OpenRowFromSurface = (row, _) => opened.Add(row.Path!);
            host.GraphLines.Clear();
            ulong id = diagram.VisibleIds[0];
            diagram.PointerPressed(ViewCentre(diagram, model, id), clickCount: 1);
            Assert.Equal(id, diagram.SelectedId);
            Assert.True(diagram.IsKeyboardFocused);
            Assert.True(PumpedDispatcher.PumpUntil(() => host.GraphLines.Count > 0, TimeSpan.FromSeconds(5)));
            Assert.Equal([RowLine(document, diagram.Entries[id])], host.GraphLines);
            Assert.Empty(opened);
            // A double-click on another node: a SILENT select, then the activation.
            host.GraphLines.Clear();
            ulong other = diagram.VisibleIds[1];
            diagram.PointerPressed(ViewCentre(diagram, model, other), clickCount: 2);
            Assert.Equal(other, diagram.SelectedId);
            Assert.Equal([diagram.Entries[other].Path!], opened);
            host.Settle(document);
            PumpedDispatcher.PumpUntil(() => false, TimeSpan.FromMilliseconds(300));
            Assert.Empty(host.GraphLines);
            // Empty space: nothing selected differently, no line.
            diagram.PointerPressed(new Point(-5000, -5000), clickCount: 1);
            Assert.Equal(other, diagram.SelectedId);
        });
    }

    [Fact]
    public void TheWheelPansAndCtrlWheelZooms()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-wheel");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.ActualSize));
            double panY = diagram.Viewport.PanY;
            double panX = diagram.Viewport.PanX;
            diagram.Wheel(new Point(100, 100), 120, ModifierKeys.None);
            Assert.Equal(panY + 120, diagram.Viewport.PanY, 6);
            Assert.Equal(panX, diagram.Viewport.PanX, 6);
            Assert.Equal(100u, diagram.Viewport.ZoomPercent);
            diagram.Wheel(new Point(100, 100), -120, ModifierKeys.Shift);
            Assert.Equal(panX - 120, diagram.Viewport.PanX, 6);
            // Ctrl+wheel: one step, centre-preserving on the pointer.
            var pointer = new Point(100, 100);
            double layoutX = (pointer.X - diagram.Viewport.PanX) / diagram.Viewport.Zoom;
            diagram.Wheel(pointer, 120, ModifierKeys.Control);
            Assert.Equal(125u, diagram.Viewport.ZoomPercent);
            Assert.Equal(layoutX, (pointer.X - diagram.Viewport.PanX) / diagram.Viewport.Zoom, 6);
            diagram.Wheel(pointer, -120, ModifierKeys.Control);
            Assert.Equal(100u, diagram.Viewport.ZoomPercent);
        });
    }

    [Fact]
    public void TheEscapeLadderBubblesFromTheRenderer()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-escape");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            Assert.True(diagram.Focus());
            Assert.True(surface.IsKeyboardFocusWithin);
            // Rungs 0–2 have nothing: the press is unconsumed and bubbles (rung 3).
            KeyEventArgs escape = Press(diagram, Key.Escape);
            Assert.False(escape.Handled);
            Assert.False(host.Workspace.GraphNavigator.HandleKey(Key.Escape, ModifierKeys.None, surface));
            // An open tooltip is the rung's most transient region: dismissed.
            diagram.PointerEnteredForTests(diagram.VisibleIds[0]);
            Assert.True(diagram.TooltipIsOpenForTests);
            Assert.True(host.Workspace.GraphNavigator.HandleKey(Key.Escape, ModifierKeys.None, surface));
            Assert.False(diagram.TooltipIsOpenForTests);
            Assert.True(diagram.IsKeyboardFocused);
        });
    }

    [Fact]
    public void TheTooltipIsTheInventorysComposedLabel()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-tooltip");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            ulong id = diagram.VisibleIds[0];
            GraphTopologyNode entry = diagram.Entries[id];
            Assert.False(diagram.TooltipIsOpenForTests);
            diagram.PointerEnteredForTests(id);
            Assert.True(diagram.TooltipIsOpenForTests);
            Assert.Equal(
                entry.Label + GraphPhrase.TooltipSeparator + entry.InLinks + GraphPhrase.TooltipInSuffix + entry.OutLinks + GraphPhrase.TooltipOutSuffix,
                diagram.TooltipTextForTests);
            // HOVERABLE: the pointer travelling from the node ONTO the tooltip
            // keeps it open; leaving the tooltip closes it.
            diagram.PointerOnTooltipForTests(true);
            diagram.PointerLeftNodeForTests();
            Assert.True(diagram.TooltipIsOpenForTests);
            diagram.PointerOnTooltipForTests(false);
            Assert.False(diagram.TooltipIsOpenForTests);
            diagram.PointerEnteredForTests(id);
            Assert.True(diagram.TooltipIsOpenForTests);
            // DISMISSABLE: Escape's answer closes it; leaving closes it.
            Assert.True(diagram.DismissTooltip());
            Assert.False(diagram.TooltipIsOpenForTests);
            Assert.False(diagram.DismissTooltip());
            diagram.PointerEnteredForTests(id);
            Assert.True(diagram.TooltipIsOpenForTests);
            diagram.PointerLeftForTests();
            Assert.False(diagram.TooltipIsOpenForTests);
            // Never announced.
            host.Settle(document);
            Assert.DoesNotContain(host.GraphLines, line => line.Contains(GraphPhrase.TooltipInSuffix, StringComparison.Ordinal));
        });
    }

    // --- D-12: the actions and the menu (Term N5); Term N7's pin -------------------------

    /// <summary>Term N5's target rule (IPH-1-3): a pointer request opens the
    /// menu on the node HIT at the view point — not the selection — none
    /// over empty space; a keyboard request (the Menu key, Shift+F10: -1, -1)
    /// opens on the selection. The journey proves the event's coordinate
    /// space on a real right-click; this fact pins the rule the handler
    /// applies to it.</summary>
    [Fact]
    public void APointerMenuRequestTargetsTheHitNodeAndAKeyboardRequestTheSelection()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-menu-target");
            GraphDocumentViewModel document = host.Open();
            (_, HostedWindow window, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            ulong selected = diagram.VisibleIds[0];
            ulong other = diagram.VisibleIds[1];
            Assert.True(diagram.SelectNode(selected, announce: false));
            window.UpdateLayout();
            Assert.Equal(selected, diagram.SelectedId);
            Point centre = ViewCentre(diagram, model, other);
            Assert.Equal(other, diagram.MenuTargetFor(true, centre.X, centre.Y));
            Assert.Equal(selected, diagram.MenuTargetFor(false, -1, -1));
            // Empty space: a corner the hit grid answers nothing for.
            Point[] corners = [new(1, 1), new(diagram.ActualWidth - 2, 1), new(1, diagram.ActualHeight - 2), new(diagram.ActualWidth - 2, diagram.ActualHeight - 2)];
            Point empty = corners.First(p => diagram.HitTest(p) is null);
            Assert.Null(diagram.MenuTargetFor(true, empty.X, empty.Y));
        });
    }

    /// <summary>Term T2 / D-8 (IPH-1-2): a node peer a client holds across
    /// the tier edge reports NO selection once the container exposes the
    /// summary alone — the selected id stands in the visible set, the peer
    /// is no longer the renderer's peer for it.</summary>
    [Fact]
    public void AStalePeerHeldAcrossTheTierEdgeReportsNoSelection()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-stale-peer");
            GraphDocumentViewModel document = host.Open();
            (_, HostedWindow window, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            InstallSyntheticTopology(document, model, 5);
            window.UpdateLayout();
            const ulong id = 100_002;
            document.ViewState.SelectedKey = "p:synthetic2.md";
            GraphNodeAutomationPeer held = Assert.IsType<GraphNodeAutomationPeer>(diagram.PeerFor(id));
            Assert.Equal(id, diagram.SelectedId);
            Assert.True(((ISelectionItemProvider)held).IsSelected);
            InstallSyntheticTopology(document, model, 1501);
            window.UpdateLayout();
            Assert.True(diagram.IsTierB);
            Assert.Equal(id, diagram.SelectedId);
            Assert.Null(diagram.PeerFor(id));
            Assert.False(((ISelectionItemProvider)held).IsSelected, "a peer the container no longer exposes reported the selection");
        });
    }

    [Fact]
    public void TheDiagramsActionsEqualTheTablesPlusPin()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-actions-drift");
            _ = host.Session.CreateExclusive("dangling.md", "# Dangling\n\n[[missing]]\n");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, HostedWindow window, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            var diagramTitles = new Dictionary<GraphNodeKind, IReadOnlyList<string>>();
            var keys = new Dictionary<GraphNodeKind, string>();
            foreach (GraphNodeKind kind in new[] { GraphNodeKind.Note, GraphNodeKind.Ghost })
            {
                GraphTopologyNode entry = diagram.Entries.Values.First(e => e.Kind == kind);
                diagramTitles[kind] = diagram.MenuTitlesForTests(entry.Id);
                keys[kind] = entry.StableKey;
            }
            // The table's row-actions menu for the same nodes.
            Assert.True(document.SetMode(GraphSurfaceMode.Table));
            window.UpdateLayout();
            foreach ((GraphNodeKind kind, string key) in keys)
            {
                Assert.True(document.SelectRow(key));
                window.UpdateLayout();
                System.Windows.Controls.ContextMenu? tableMenu = surface.TableForTests.GridForTests.BuildRowActionsMenu();
                Assert.NotNull(tableMenu);
                string[] tableTitles = [.. tableMenu.Items.OfType<System.Windows.Controls.MenuItem>().Select(item => (string)item.Header)];
                Assert.Equal([.. tableTitles, GraphPhrase.PinLabel], diagramTitles[kind]);
                // Core's per-kind vector in core's order.
                Assert.Equal([.. document.ActionSpecs(kind).Select(s => s.Title)], tableTitles);
            }
        });
    }

    [Fact]
    public void AGhostWithoutCreateAdmissionShowsTheReasonAndInvokeDoesNothing()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-ghost-admission");
            _ = host.Session.CreateExclusive("dangling.md", "# Dangling\n\n[[missing]]\n");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            var created = new List<string>();
            document.CreateNoteFromSurface = path => created.Add(path);
            document.CreateAdmissionReason = () => "Busy indexing";
            GraphTopologyNode ghost = diagram.Entries.Values.First(e => e.Kind == GraphNodeKind.Ghost);
            Assert.True(diagram.RebuildMenu(ghost.Id));
            System.Windows.Controls.MenuItem create = diagram.MenuForTests.Items.OfType<System.Windows.Controls.MenuItem>()
                .Single(item => (string)item.Header == document.ActionSpecs(GraphNodeKind.Ghost).Single(s => s.Action == GraphRowAction.CreateNote).Title);
            Assert.False(create.IsEnabled);
            Assert.Equal("Busy indexing", System.Windows.Automation.AutomationProperties.GetHelpText(create));
            // Invoke is inert; the pin remains.
            ((IInvokeProvider)diagram.PeerFor(ghost.Id)!).Invoke();
            Assert.Empty(created);
            Assert.Contains(GraphPhrase.PinLabel, diagram.MenuTitlesForTests(ghost.Id));
            // Admitted: Invoke creates through the seam, by the label.
            document.CreateAdmissionReason = () => null;
            ((IInvokeProvider)diagram.PeerFor(ghost.Id)!).Invoke();
            Assert.Equal([SlateUniffiMethods.GraphGhostNotePath(ghost.Label)], created);
            // A note's Invoke opens.
            var opened = new List<string>();
            document.OpenRowFromSurface = (row, _) => opened.Add(row.Path!);
            GraphTopologyNode note = diagram.Entries.Values.First(e => e.Kind == GraphNodeKind.Note);
            ((IInvokeProvider)diagram.PeerFor(note.Id)!).Invoke();
            Assert.Equal([note.Path!], opened);
        });
    }

    [Fact]
    public void AGhostOmitsShowConnections()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-ghost-menu");
            _ = host.Session.CreateExclusive("dangling.md", "# Dangling\n\n[[missing]]\n");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            string showConnections = document.ActionSpecs(GraphNodeKind.Note).Single(s => s.Action == GraphRowAction.ShowConnections).Title;
            GraphTopologyNode ghost = diagram.Entries.Values.First(e => e.Kind == GraphNodeKind.Ghost);
            GraphTopologyNode note = diagram.Entries.Values.First(e => e.Kind == GraphNodeKind.Note);
            Assert.DoesNotContain(showConnections, diagram.MenuTitlesForTests(ghost.Id));
            Assert.Contains(showConnections, diagram.MenuTitlesForTests(note.Id));
            Assert.False(document.ExecuteFromDiagram(GraphRowAction.ShowConnections, ghost));
        });
    }

    [Fact]
    public void EachActionReachesTheTablesSeam()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-actions-seams");
            _ = host.Session.CreateExclusive("dangling.md", "# Dangling\n\n[[missing]]\n");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            var opened = new List<(string, WorkspaceOpenTarget)>();
            var revealed = new List<string>();
            var shown = new List<string>();
            var created = new List<string>();
            document.OpenRowFromSurface = (row, target) => opened.Add((row.Path!, target));
            document.RevealRowFromSurface = path => revealed.Add(path);
            document.ShowConnectionsFromSurface = row => shown.Add(row.Path!);
            document.CreateNoteFromSurface = path => created.Add(path);
            GraphTopologyNode note = diagram.Entries.Values.First(e => e.Kind == GraphNodeKind.Note);
            GraphTopologyNode ghost = diagram.Entries.Values.First(e => e.Kind == GraphNodeKind.Ghost);
            Assert.True(document.ExecuteFromDiagram(GraphRowAction.Open, note));
            Assert.True(document.ExecuteFromDiagram(GraphRowAction.OpenInNewTab, note));
            Assert.True(document.ExecuteFromDiagram(GraphRowAction.Reveal, note));
            Assert.True(document.ExecuteFromDiagram(GraphRowAction.ShowConnections, note));
            Assert.True(document.ExecuteFromDiagram(GraphRowAction.CreateNote, ghost));
            Assert.Equal([(note.Path!, WorkspaceOpenTarget.CurrentTab), (note.Path!, WorkspaceOpenTarget.NewTab)], opened);
            Assert.Equal([note.Path!], revealed);
            Assert.Equal([note.Path!], shown);
            Assert.Equal([SlateUniffiMethods.GraphGhostNotePath(ghost.Label)], created);
            // The menu's items reach the same seams.
            Assert.True(diagram.RebuildMenu(note.Id));
            string reveal = document.ActionSpecs(GraphNodeKind.Note).Single(s => s.Action == GraphRowAction.Reveal).Title;
            System.Windows.Controls.MenuItem item = diagram.MenuForTests.Items.OfType<System.Windows.Controls.MenuItem>().Single(i => (string)i.Header == reveal);
            item.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
            Assert.Equal([note.Path!, note.Path!], revealed);
        });
    }

    [Fact]
    public void AStaleNodeIsRefused()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-stale-node");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            var opened = new List<string>();
            document.OpenRowFromSurface = (row, _) => opened.Add(row.Path!);
            GraphTopologyNode note3 = diagram.Entries.Values.Single(e => e.Path == "note3.md");
            Assert.True(document.IsNodeCurrent(note3.Id));
            // The needle hides it: gone from the visible set, refused.
            host.Workspace.GraphNavigator.SetNameQuery("note0");
            host.Settle(document);
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.VisibleIds.Count == 1, TimeSpan.FromSeconds(5)));
            Assert.False(document.IsNodeCurrent(note3.Id));
            Assert.False(document.ExecuteFromDiagram(GraphRowAction.Open, note3));
            Assert.False(document.ActivateFromDiagram(note3));
            Assert.False(diagram.ActivateNode(note3.Id));
            Assert.Empty(opened);
        });
    }

    [Fact]
    public void ThePinTogglesThroughTheGateAndSpeaksPinned()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-pin");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            ulong id = diagram.VisibleIds[0];
            host.GraphLines.Clear();
            Assert.Equal(GraphPhrase.PinLabel, diagram.MenuTitlesForTests(id)[^1]);
            Assert.True(diagram.TogglePin(id));
            Assert.Contains(id, model.Pinned);
            Assert.Equal(1, document.CrossingsForTests["layout_pin_node"]);
            Assert.True(PumpedDispatcher.PumpUntil(() => host.GraphLines.Count > 0, TimeSpan.FromSeconds(5)));
            Assert.Equal([PinnedLine(true)], host.GraphLines);
            Assert.Equal(GraphPhrase.PinnedStatus, diagram.PeerFor(id)!.GetItemStatus());
            Assert.Equal(GraphPhrase.UnpinLabel, diagram.MenuTitlesForTests(id)[^1]);
            host.GraphLines.Clear();
            Assert.True(diagram.TogglePin(id));
            Assert.DoesNotContain(id, model.Pinned);
            Assert.True(PumpedDispatcher.PumpUntil(() => host.GraphLines.Count > 0, TimeSpan.FromSeconds(5)));
            Assert.Equal([PinnedLine(false)], host.GraphLines);
            // A retired model: refused, nothing spoken (IGS-2).
            host.GraphLines.Clear();
            Assert.True(document.SetMode(GraphSurfaceMode.Table));
            Assert.False(diagram.TogglePin(id));
            host.Settle(document);
            Assert.Equal([ModeLine(GraphSurfaceMode.Table)], host.GraphLines);
        });
    }

    // --- D-14: Where-am-I on the diagram (Term N6) -------------------------------------------

    [Fact]
    public void TheDiagramReadbackNamesTheSelectionsTopologyEntryWithTheZoomClause()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-readback-node");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(GraphViewportVerb.ActualSize));
            ulong id = diagram.VisibleIds.First(i => diagram.Entries[i].InLinks > 0);
            GraphTopologyNode entry = diagram.Entries[id];
            Assert.True(diagram.SelectNode(id, announce: false));
            GraphA11yEvent.GraphWhereAmI? @event = document.DiagramWhereAmI();
            Assert.NotNull(@event);
            Assert.Equal(new GraphWhereAmISelection.Node(GraphDocumentViewModel.RowCopyOf(entry), entry.Component), @event.Selection);
            Assert.Equal(100u, @event.ZoomPercent);
            Assert.Equal(new GraphWhereAmIFilter.Normal(false, false, true), @event.Filter);
            Assert.Equal(string.Empty, @event.NameFilter);
            string line = WhereAmILine(@event);
            Assert.Contains("zoom", line, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("component", line, StringComparison.Ordinal);
            // The verb: the panel's text and ONE post render the one event
            // (TheChordOpensThePanelWithTheSameTextItSpoke).
            host.GraphLines.Clear();
            Assert.True(host.Workspace.GraphNavigator.WhereAmI());
            Assert.Equal(line, host.Workspace.GraphNavigator.WhereAmIText);
            Assert.True(PumpedDispatcher.PumpUntil(() => host.GraphLines.Count > 0, TimeSpan.FromSeconds(5)));
            Assert.Equal([line], host.GraphLines);
        });
    }

    [Fact]
    public void TheDiagramReadbackReadsNoSelectionWithoutAVisibleKey()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-readback-none");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            document.ViewState.SelectedKey = null;
            Assert.Equal(new GraphWhereAmISelection.NoSelection(), document.DiagramWhereAmI()!.Selection);
            document.ViewState.SelectedKey = "p:nowhere.md";
            Assert.Equal(new GraphWhereAmISelection.NoSelection(), document.DiagramWhereAmI()!.Selection);
            // A key the needle hides: no selection, the key kept.
            ulong id = diagram.VisibleIds[1];
            Assert.True(document.SelectRow(diagram.Entries[id].StableKey));
            host.Workspace.GraphNavigator.SetNameQuery("note0");
            host.Settle(document);
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.VisibleIds.Count == 1, TimeSpan.FromSeconds(5)));
            Assert.Equal(new GraphWhereAmISelection.NoSelection(), document.DiagramWhereAmI()!.Selection);
            Assert.NotNull(document.ViewState.SelectedKey);
            Assert.True(host.Workspace.GraphNavigator.WhereAmI());
            Assert.StartsWith("No node selected", host.Workspace.GraphNavigator.WhereAmIText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void TheDiagramReadbackCarriesTheNeedleAndTheOverlay()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-readback-filters");
            _ = host.Session.CreateExclusive("dangling.md", "# Dangling\n\n[[missing]]\n");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            host.Workspace.GraphNavigator.SetNameQuery("  note  ");
            host.Settle(document);
            Assert.Equal("  note  ", document.DiagramWhereAmI()!.NameFilter);
            Assert.True(host.Workspace.GraphNavigator.WhereAmI());
            Assert.Contains("note", host.Workspace.GraphNavigator.WhereAmIText, StringComparison.Ordinal);
            // The Unresolved preset: the overlay reads UnresolvedOnly; the rebuild
            // under the preset's filter keeps the diagram live.
            host.Workspace.GraphNavigator.RunPreset(GraphPreset.Unresolved);
            _ = SettledModel(host, document);
            Assert.Equal(new GraphWhereAmIFilter.UnresolvedOnly(), document.DiagramWhereAmI()!.Filter);
            Assert.Equal(GraphSurfaceMode.Diagram, document.ViewState.Mode);
            _ = diagram;
        });
    }

    [Fact]
    public void TheReadbackIsRefusedWhileTheDiagramBuildsAndAnswersAtInstall()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-readback-build");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView surface = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(surface);
            Assert.True(host.Workspace.GraphNavigator.CanWhereAmI);
            using var park = new Park();
            document.FetchGateForTests = park.Hit;
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            park.WaitReached();
            // Diagram mode with no model: the diagram's seam is uninstalled —
            // refused, the row disabled (TheRowEnablesWhenTheDiagramSeamInstalls).
            Assert.False(host.Workspace.GraphNavigator.CanWhereAmI);
            Assert.Null(document.DiagramWhereAmI());
            Assert.False(host.Workspace.GraphNavigator.WhereAmI());
            document.FetchGateForTests = null;
            park.Release();
            _ = SettledModel(host, document);
            Assert.True(host.Workspace.GraphNavigator.CanWhereAmI);
            Assert.NotNull(document.DiagramWhereAmI());
            Assert.True(host.Workspace.GraphNavigator.WhereAmI());
            // Torn down: refused again.
            Assert.True(document.SetMode(GraphSurfaceMode.Table));
            Assert.True(host.Workspace.GraphNavigator.CanWhereAmI);
            Assert.Null(document.DiagramWhereAmI());
        });
    }

    // --- Term V6: the value and the clause; D-5: the stable key across a churn --------------

    [Fact]
    public void TheValueAndTheClauseReadOneNumber()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-one-number");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            var value = (IValueProvider)ContainerPeer(diagram);
            foreach (GraphViewportVerb verb in new[] { GraphViewportVerb.ActualSize, GraphViewportVerb.ZoomIn, GraphViewportVerb.ZoomIn, GraphViewportVerb.FitGraph, GraphViewportVerb.ZoomOut })
            {
                var zoomed = Assert.IsType<GraphViewportOutcome.Zoomed>(surface.ViewportCommand(verb));
                Assert.Equal(zoomed.Percent, document.DiagramWhereAmI()!.ZoomPercent);
                Assert.Equal($"Zoom {zoomed.Percent} percent", value.Value);
            }
        });
    }

    [Fact]
    public void AGenerationChurnKeepsTheSelectionByStableKey()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-churn-key");
            GraphDocumentViewModel document = host.Open();
            (_, _, GraphDiagramView diagram, GraphDiagramModel model) = LiveDiagram(host, document);
            ulong id = diagram.Entries.Values.Single(e => e.Path == "note2.md").Id;
            Assert.True(diagram.SelectNode(id, announce: false));
            string key = document.ViewState.SelectedKey!;
            ulong before = model.Generation;
            // Two notes added: the generation moves, ids may be reassigned.
            _ = host.Session.CreateExclusive("alpha.md", "# Alpha\n\n[[note2]]\n");
            _ = host.Session.CreateExclusive("beta.md", "# Beta\n\n[[note0]]\n");
            document.Probe();
            host.Settle(document);
            WaitForTheSettle(host, document, model);
            Assert.True(PumpedDispatcher.PumpUntil(() => model.Generation > before && diagram.Entries.Count == model.NodeIds.Length, TimeSpan.FromSeconds(10)));
            Assert.Equal(key, document.ViewState.SelectedKey);
            ulong after = diagram.Entries.Values.Single(e => e.Path == "note2.md").Id;
            Assert.Equal(after, diagram.SelectedId);
            Assert.True(((ISelectionItemProvider)diagram.PeerFor(after)!).IsSelected);
        });
    }
}
