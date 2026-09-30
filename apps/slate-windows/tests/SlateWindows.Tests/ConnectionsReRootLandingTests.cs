// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Canvas;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1318's shell gate (<c>GraphConnections_LeafWalkDepthAndReRoot_AreClean</c>;
/// contract 35 B2-3, IGI-4 and Term 9, with contract 40 R-5 (h)): the graph
/// table's Show connections, with the keys on a table cell, re-roots the
/// Connections leaf on the row's note, and the window's right-pane boundary
/// lands the keys in the leaf — its tree — and they stay there through the
/// re-root's load: never on the rail's row on the way.
/// </summary>
/// <remarks>
/// The re-root replaces the graph tab with the note, so the cell holding the
/// keys leaves the tree before the boundary's landing runs. The focus guard
/// read that landing's direct request — to a row of the leaf's tree, which has
/// rows of its own (a TreeViewItem is an ItemsControl) — as a removed row
/// handing its keys to its own bare list, declined it, and the boundary fell
/// back to the rail.
/// </remarks>
public sealed class ConnectionsReRootLandingTests
{
    [Fact]
    public void TheTablesShowConnectionsLandsTheKeysInTheReRootedLeaf() => RunSta(() =>
    {
        using var host = new ShownShell(
            ("Alpha.md", "# Alpha\n\nLinks to [[Beta]] and [[Gamma]] and [[Missing Note]].\n"),
            ("Beta.md", "# Beta\n\nLinks to [[Alpha]] and [[Delta]].\n"),
            ("Gamma.md", "# Gamma\n\nLinks to [[Alpha]].\n"),
            ("Delta.md", "# Delta\n\nLinks to [[Beta]].\n"));
        host.RelayFocusBoundaries();
        ConnectionsLeafViewModel leaf = host.Workspace.Connections;
        host.Workspace.OpenPath("Alpha.md");
        host.Settle();
        // The leaf pinned and showing its rows, as the journey leaves it
        // before it opens the graph.
        Assert.True(host.Workspace.ReRootConnectionsOn("Alpha.md"));
        Settle(host, leaf);
        ConnectionsLeafView surface = host.Shell.ConnectionsLeafSurface;
        Assert.Equal("Alpha.md", leaf.Pin);
        Assert.True(surface.IsVisible, "premise: the Connections leaf is not shown");
        Assert.NotEmpty(surface.RootsForTests);

        host.Workspace.OpenGraph();
        GraphDocumentViewModel graph = host.Workspace.GraphDocument!;
        PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
        host.Settle();
        GraphSurfaceView view = ShownShell.Descendants(host.Shell).OfType<GraphSurfaceView>().Single(candidate => candidate.IsVisible);
        GraphTableRow beta = graph.Publication.Rows.Single(row => string.Equals(row.Path, "Beta.md", StringComparison.Ordinal));
        Assert.Equal(LandingSeat.Seated, view.TableForTests.GridForTests.SeatRow(row => ReferenceEquals(row, beta)));
        Assert.True(view.TableForTests.IsKeyboardFocusWithin, $"premise: Beta's row did not take the keys; they are on {Describe(Keyboard.FocusedElement)}");
        List<IInputElement> changes = host.RecordFocusChanges();

        Assert.True(graph.IsActionEnabled(GraphRowAction.ShowConnections, beta));
        graph.Execute(GraphRowAction.ShowConnections, beta);
        Settle(host, leaf);

        Assert.Equal("Beta.md", leaf.Pin);
        string journey = string.Join(" -> ", changes.Select(Describe));
        Assert.True(
            surface.TreeForTests.IsKeyboardFocusWithin,
            $"the re-rooted leaf does not hold the keys; they are on {Describe(Keyboard.FocusedElement)} ({journey})");
        Assert.NotEmpty(changes);
        Assert.All(
            changes,
            change => Assert.True(
                change is DependencyObject element && surface.IsAncestorOf(element),
                $"the keys stopped outside the leaf on the way: {journey}"));
    });

    private static void Settle(ShownShell host, ConnectionsLeafViewModel leaf)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            PumpedDispatcher.PumpUntilDrained(leaf.WhenAllWorkDrained());
            host.Settle();
        }
    }

    private static string Describe(IInputElement? element) => element switch
    {
        null => "nothing",
        ListBoxItem row when row.DataContext is WorkspaceLeafOption option => $"the rail's {option.Title} row",
        FrameworkElement { Name: { Length: > 0 } name } framed => $"{framed.GetType().Name} '{name}'",
        FrameworkElement framed => $"{framed.GetType().Name} ({System.Windows.Automation.AutomationProperties.GetAutomationId(framed)})",
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
