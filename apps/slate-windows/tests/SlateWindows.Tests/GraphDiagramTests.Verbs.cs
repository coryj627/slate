// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;
using SlateWindows.Commands;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR D (#746), rule V's verbs, rows and chords (contract D-13's
/// navigator half; Terms V2–V4; C-11's scrape at six): the four rows with
/// the mac's labels, hints and chords in the Graph scope; the three shared
/// chords recorded beside the canvas pairs and Ctrl+Alt+0 free; a verb in
/// Table mode refused and its chord falling through; a verb from the filter
/// field in Diagram mode acting; the menu items following the availability;
/// the zoom line through the document's seam; the binding record the one
/// authority.
/// </summary>
public sealed partial class GraphDiagramTests
{
    private static string ZoomLine(bool fit, uint percent) => Render(new GraphA11yEvent.GraphZoom(fit, percent));

    private static KeyEventArgs PreviewPress(System.Windows.UIElement target, Key key)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, System.Windows.PresentationSource.FromVisual(target)!, 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };
        target.RaiseEvent(args);
        return args;
    }

    [Fact]
    public void TheFourRowsTheirScopeLabelsAndChords()
    {
        foreach ((string id, string label, string hint, string mac, string win) in new[]
        {
            (ChordTable.Ids.GraphZoomIn, "Graph: Zoom In", "Zoom the visual diagram in. The zoom level is announced.", "⌘=", "Ctrl+="),
            (ChordTable.Ids.GraphZoomOut, "Graph: Zoom Out", "Zoom the visual diagram out.", "⌘-", "Ctrl+-"),
            (ChordTable.Ids.GraphActualSize, "Graph: Actual Size", "Reset the visual diagram zoom to 100 percent.", "⌘0", "Ctrl+0"),
            (ChordTable.Ids.GraphFitGraph, "Graph: Fit Graph", "Zoom so every node is visible. Option-Command-0 on the diagram.", "⌥⌘0", "Ctrl+Alt+0"),
        })
        {
            ChordTableEntry row = ChordTable.Entries.Single(r => r.Id == id);
            Assert.Equal(label, row.Label);
            Assert.Equal(hint, row.Hint);
            Assert.Equal(mac, row.MacChord);
            Assert.Equal(win, row.WindowsChord);
            Assert.Equal(ChordScope.Graph, row.Scope);
            Assert.Equal(CommandSection.Graph, row.Section);
            Assert.True(row.IsRegistered);
            Assert.Null(row.Divergence);
        }
        // The mac's ids, byte for byte.
        Assert.Equal(["slate.graph.zoomIn", "slate.graph.zoomOut", "slate.graph.actualSize", "slate.graph.fitGraph"],
            [ChordTable.Ids.GraphZoomIn, ChordTable.Ids.GraphZoomOut, ChordTable.Ids.GraphActualSize, ChordTable.Ids.GraphFitGraph]);
    }

    [Fact]
    public void TheScrapeInBothDirectionsHoldsSixChords()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-six-chords");
            (Key Key, ModifierKeys Modifiers)[] chords = [.. host.Workspace.GraphNavigator.ChordsForTests];
            Assert.Equal(6, chords.Length);
            // The table's Graph-scoped chorded rows: Escape, Where-am-I, the four.
            ChordTableEntry[] rows = [.. ChordTable.Entries.Where(r => r.Scope == ChordScope.Graph && r.WindowsChord is not null)];
            Assert.Equal(6, rows.Length);
            Assert.Contains(rows, r => r.WindowsChord == "Ctrl+=");
            Assert.Contains(rows, r => r.WindowsChord == "Ctrl+-");
            Assert.Contains(rows, r => r.WindowsChord == "Ctrl+0");
            Assert.Contains(rows, r => r.WindowsChord == "Ctrl+Alt+0");
        });
    }

    [Fact]
    public void TheSharedChordDispositionsNameTheThreeCanvasPairsAndCtrlAltZeroIsFree()
    {
        foreach ((string graph, string canvas) in new[]
        {
            (ChordTable.Ids.GraphZoomIn, ChordTable.Ids.CanvasZoomIn),
            (ChordTable.Ids.GraphZoomOut, ChordTable.Ids.CanvasZoomOut),
            (ChordTable.Ids.GraphActualSize, ChordTable.Ids.CanvasActualSize),
        })
        {
            ChordTableEntry graphRow = ChordTable.Entries.Single(r => r.Id == graph);
            ChordTableEntry canvasRow = ChordTable.Entries.Single(r => r.Id == canvas);
            Assert.Equal(canvasRow.WindowsChord, graphRow.WindowsChord);
            Assert.NotEqual(canvasRow.Scope, graphRow.Scope);
        }
        // CtrlAltZeroIsFreeInEveryOtherScope: the fit's chord is the graph row's alone.
        Assert.Single(ChordTable.Entries, r => string.Equals(r.WindowsChord, "Ctrl+Alt+0", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AVerbInTableModeIsRefusedAndTheChordFallsThrough()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-verb-table-refused");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView surface = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(surface);
            GraphNavigator navigator = host.Workspace.GraphNavigator;
            host.GraphLines.Clear();
            Assert.False(navigator.CanZoom);
            Assert.False(navigator.ZoomIn());
            Assert.False(navigator.FitGraph());
            // The chord falls through unconsumed (the press bubbles); the
            // commands are listed and disabled.
            Assert.False(navigator.HandleKey(Key.OemPlus, ModifierKeys.Control, surface));
            Assert.False(navigator.HandleKey(Key.D0, ModifierKeys.Control | ModifierKeys.Alt, surface));
            Assert.False(host.Workspace.GraphZoomInCommand.CanExecute(null));
            Assert.False(host.Workspace.GraphFitGraphCommand.CanExecute(null));
            host.Settle(document);
            Assert.Empty(host.GraphLines);
            // Diagram effective on the SAME surface: the chord is consumed and
            // the line spoken.
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            GraphDiagramView diagram = surface.DiagramForTests;
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.Entries.Count == model.NodeIds.Length, TimeSpan.FromSeconds(10)));
            window.UpdateLayout();
            host.GraphLines.Clear();
            Assert.True(navigator.CanZoom);
            Assert.True(navigator.HandleKey(Key.OemPlus, ModifierKeys.Control, surface));
            Assert.True(PumpedDispatcher.PumpUntil(() => host.GraphLines.Count > 0, TimeSpan.FromSeconds(5)));
            Assert.Equal([ZoomLine(false, diagram.Viewport.ZoomPercent)], host.GraphLines);
        });
    }

    [Fact]
    public void AVerbFromTheFilterFieldInDiagramModeActs()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-verb-filter-field");
            GraphDocumentViewModel document = host.Open();
            (GraphSurfaceView surface, _, GraphDiagramView diagram, _) = LiveDiagram(host, document);
            Assert.True(surface.FilterFieldForTests.Focus());
            Assert.True(surface.FilterRegionHasKeys);
            uint before = diagram.Viewport.ZoomPercent;
            host.GraphLines.Clear();
            // The chord carries Control: the surface's tunnelling handler acts
            // and no typed character is eaten.
            KeyEventArgs press = PreviewPress(surface.FilterFieldForTests, Key.OemPlus);
            _ = press;
            Assert.True(host.Workspace.GraphNavigator.HandleKey(Key.OemPlus, ModifierKeys.Control, surface));
            Assert.NotEqual(before, diagram.Viewport.ZoomPercent);
            Assert.True(surface.FilterFieldForTests.IsKeyboardFocused);
            Assert.True(PumpedDispatcher.PumpUntil(() => host.GraphLines.Count > 0, TimeSpan.FromSeconds(5)));
            Assert.Contains(ZoomLine(false, diagram.Viewport.ZoomPercent), host.GraphLines);
        });
    }

    [Fact]
    public void TheMenuItemsFollowTheAvailability()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-menu-availability");
            GraphDocumentViewModel document = host.Open();
            ICommand[] commands =
            [
                host.Workspace.GraphZoomInCommand, host.Workspace.GraphZoomOutCommand,
                host.Workspace.GraphActualSizeCommand, host.Workspace.GraphFitGraphCommand,
            ];
            int changed = 0;
            foreach (ICommand command in commands)
            {
                command.CanExecuteChanged += (_, _) => changed++;
            }
            Assert.All(commands, c => Assert.False(c.CanExecute(null)));
            GraphSurfaceView surface = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(surface);
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            // Building: not yet effective.
            Assert.All(commands, c => Assert.False(c.CanExecute(null)));
            GraphDiagramModel model = SettledModel(host, document);
            GraphDiagramView diagram = surface.DiagramForTests;
            Assert.True(PumpedDispatcher.PumpUntil(() => diagram.Entries.Count == model.NodeIds.Length, TimeSpan.FromSeconds(10)));
            window.UpdateLayout();
            Assert.All(commands, c => Assert.True(c.CanExecute(null)));
            Assert.True(changed >= commands.Length, "the install raised the availability");
            // The reader in the surface (the presenter attached on the keys'
            // edge): the verbs through the commands speak the lines.
            Assert.True(diagram.Focus());
            host.GraphLines.Clear();
            host.Workspace.GraphActualSizeCommand.Execute(null);
            host.Workspace.GraphFitGraphCommand.Execute(null);
            Assert.True(PumpedDispatcher.PumpUntil(() => host.GraphLines.Count >= 2, TimeSpan.FromSeconds(5)));
            Assert.Equal(ZoomLine(false, 100), host.GraphLines[0]);
            Assert.StartsWith(ZoomLine(true, 0).Split('0')[0], host.GraphLines[1], StringComparison.Ordinal);
            // Torn down: disabled again.
            int before = changed;
            Assert.True(document.SetMode(GraphSurfaceMode.Table));
            Assert.All(commands, c => Assert.False(c.CanExecute(null)));
            Assert.True(changed > before);
        });
    }

    [Fact]
    public void TheGraphViewportBindingRecordIsTheOneAuthority()
    {
        (string Id, string NavigatorMember, Func<ISlateCommandHost, ICommand?> Resolve)[] bindings = SlateCommandRegistrar.GraphViewportBindings;
        Assert.Equal(
            [ChordTable.Ids.GraphActualSize, ChordTable.Ids.GraphFitGraph, ChordTable.Ids.GraphZoomIn, ChordTable.Ids.GraphZoomOut],
            bindings.Select(b => b.Id).OrderBy(id => id, StringComparer.Ordinal));
        foreach ((string id, string member, _) in bindings)
        {
            ChordTableEntry row = Assert.Single(ChordTable.Entries, entry => entry.Id == id);
            Assert.True(row.IsRegistered);
            Assert.NotNull(typeof(GraphNavigator).GetMethod(member, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance));
            Assert.Contains(id, SlateCommandRegistrar.ResolvableIds);
        }
    }
}
