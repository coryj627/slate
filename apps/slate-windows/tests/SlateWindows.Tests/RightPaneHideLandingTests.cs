// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Canvas;
using SlateWindows.Graph;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4b round 2 (codex r1 F1; R-5 (h), "never … the window"): hiding the
/// right pane — Ctrl+Alt+I, the View menu, the palette — with the keys in it
/// collapses every scope they were in, and WPF's re-evaluation found no
/// focusable ancestor short of the window: the keys went to the MainWindow.
/// The three workspace columns now land them in the editor region, inside
/// that re-evaluation — one focus change. The shipped window, shown, over a
/// real workspace with a markdown note open.
/// </summary>
public sealed class RightPaneHideLandingTests
{
    [Theory]
    [InlineData("RightPaneRail")]
    [InlineData("RightPaneContent")]
    public void HidingTheRightPaneUnderTheKeysLandsThemInTheEditorOnce(string origin) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        host.Workspace.OpenPath("a.md");
        host.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "outline");
        host.Settle();
        var shell = (IShellRegionHost)host.Shell;
        Assert.True(host.Land(Enum.Parse<ShellRegionKind>(origin)), $"premise: {origin} took no keys");
        Assert.True(host.Shell.RightPaneBorder.IsKeyboardFocusWithin, "premise: the keys are not in the right pane");
        List<IInputElement> changes = host.RecordFocusChanges();

        host.Workspace.ToggleRightPaneCommand.Execute(null);
        PumpedDispatcher.Drain();

        Assert.False(host.Workspace.IsRightPaneVisible);
        Assert.False(host.Shell.RightPaneBorder.IsVisible);
        Assert.IsNotType<MainWindow>(Keyboard.FocusedElement);
        Assert.Equal(ShellRegionKind.Editor, shell.FocusedRegion());
        Assert.Single(changes);
    });

    /// <summary>The review's "Load more" owns a landing of its own — the row
    /// its page appended — which the hidden pane cannot take; the keys still
    /// land in the editor, once.</summary>
    [Fact]
    public void HidingTheRightPaneUnderLoadMoreLandsTheKeysInTheEditorOnce() => RunSta(() =>
    {
        string tasks = string.Concat(Enumerable.Range(0, (int)TasksReviewViewModel.PageSize + 5).Select(index => $"- [ ] task {index:D3}\n"));
        using var host = new ShownShell(("a.md", "Just a line of text.\n"), ("todo.md", tasks));
        host.Workspace.OpenPath("a.md");
        host.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "tasksReview");
        TasksReviewViewModel review = host.Workspace.TasksReview;
        review.EnsureLoaded();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => review.HasMore && !review.IsLoading, TimeSpan.FromSeconds(30)),
            "premise: the review never published a page with more to load.");
        host.Settle();
        Button loadMore = host.Shell.PanelReviewLoadMore;
        Assert.True(loadMore.IsVisible && loadMore.Focus(), "premise: Load more refused the keys.");
        List<IInputElement> changes = host.RecordFocusChanges();

        host.Workspace.ToggleRightPaneCommand.Execute(null);
        PumpedDispatcher.Drain();

        Assert.False(host.Shell.RightPaneBorder.IsVisible);
        Assert.IsNotType<MainWindow>(Keyboard.FocusedElement);
        Assert.Equal(ShellRegionKind.Editor, ((IShellRegionHost)host.Shell).FocusedRegion());
        Assert.Single(changes);
    });

    /// <summary>
    /// Codex PR 4b r1 F8's widened census: a leaf switched under the keys — by
    /// a command, a reveal, the model — collapses the leaf body they were in,
    /// and with it every scope they had. The leaves' host lands them in the
    /// leaf now shown (its first stop, else the rail's row): never the
    /// window, never out of the right pane.
    /// </summary>
    [Theory]
    [InlineData("outline", "OutlineLeafBody")]
    [InlineData("backlinks", "BacklinksLeafBody")]
    [InlineData("outgoingLinks", "OutgoingLinksLeafBody")]
    [InlineData("embeds", "EmbedsLeafBody")]
    [InlineData("tasks", "TasksLeafBody")]
    [InlineData("tasksReview", "TasksReviewLeafBody")]
    [InlineData("citations", "CitationsLeafBody")]
    [InlineData("bibliography", "BibliographyLeafBody")]
    [InlineData("queries", "QueriesLeafBody")]
    [InlineData("connections", "ConnectionsLeafBody")]
    [InlineData("history", "HistoryLeafBody")]
    [InlineData("syncDiagnostics", "SyncDiagnosticsLeafBody")]
    public void ALeafSwitchedUnderTheKeysLandsThemInTheShownLeaf(string leaf, string body) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        host.Workspace.OpenPath("a.md");
        host.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(option => option.Id == leaf);
        host.Settle();
        FrameworkElement leafBody = host.Body(body);
        Assert.True(host.Land(ShellRegionKind.RightPaneContent), $"premise: {leaf} took no keys");
        Assert.True(leafBody.IsKeyboardFocusWithin, $"premise: the keys are not in {body}, but on {Keyboard.FocusedElement}");

        host.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(option => option.Id == (leaf == "outline" ? "backlinks" : "outline"));
        PumpedDispatcher.Drain();

        Assert.False(leafBody.IsVisible);
        Assert.IsNotType<MainWindow>(Keyboard.FocusedElement);
        Assert.True(host.Shell.RightPaneBorder.IsKeyboardFocusWithin, $"the keys left the right pane, for {Keyboard.FocusedElement}");
    });

    /// <summary>Codex PR 4b r1 F8's widened census: closing the vault with
    /// the keys in the workspace collapses WorkspaceRoot, and every scope in
    /// it; the keys land on the welcome view, never the window.</summary>
    [Fact]
    public void ClosingTheVaultUnderTheKeysLandsThemOnTheWelcomeView() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        Assert.True(host.Land(ShellRegionKind.Editor), "premise: the editor took no keys");

        host.SetVaultOpen(false);

        Assert.False(host.Shell.WorkspaceRoot.IsVisible);
        Assert.IsNotType<MainWindow>(Keyboard.FocusedElement);
        Assert.True(host.Shell.WelcomeRoot.IsKeyboardFocusWithin, $"the keys are not on the welcome view, but on {Keyboard.FocusedElement}");
    });

    /// <summary>Opening the vault from the welcome view collapses WelcomeRoot
    /// under the keys; they land in the workspace, never the window.</summary>
    [Fact]
    public void OpeningTheVaultUnderTheKeysLandsThemInTheWorkspace() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        host.SetVaultOpen(false);
        Assert.True(host.Shell.WelcomeRoot.IsKeyboardFocusWithin || host.Shell.OpenVaultButton.Focus(), "premise: the welcome view took no keys");

        host.SetVaultOpen(true);

        Assert.False(host.Shell.WelcomeRoot.IsVisible);
        Assert.IsNotType<MainWindow>(Keyboard.FocusedElement);
        Assert.True(host.Shell.WorkspaceRoot.IsKeyboardFocusWithin, $"the keys are not in the workspace, but on {Keyboard.FocusedElement}");
    });

    /// <summary>
    /// W7-7 PR 4b on PR 8 (OD-12): the welcome view gone under the keys lands
    /// them in the workspace WITHOUT withdrawing the editor landing the window
    /// holds — the launch landing, held while a canvas or graph loads, seats and
    /// speaks its own line afterwards. The keys wait on the active tab's item
    /// meanwhile. (A landing of the fact's own stands in for the launch
    /// landing: the fixture raises no WorkspaceReady.)
    /// </summary>
    [Fact]
    public void OpeningTheVaultUnderTheKeysLeavesAHeldEditorLandingToSeat() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.SetVaultOpen(false);
        Assert.True(host.Shell.OpenVaultButton.Focus(), "premise: the welcome view took no keys");
        int withdrawals = 0;
        var launch = new HeldEditorLanding(
            target: () => null,
            isLive: () => true,
            withdraw: () => ++withdrawals > 0,
            stillWhereAsked: () => true,
            scope: [],
            ringRegion: null);
        host.Shell.EditorLandings.Hold(launch);
        try
        {
            host.SetVaultOpen(true);

            Assert.False(host.Shell.WelcomeRoot.IsVisible);
            Assert.Equal(0, withdrawals);
            Assert.Same(launch, host.Shell.EditorLandings.Held);
            Assert.IsType<TabItem>(Keyboard.FocusedElement);
            Assert.True(host.Shell.WorkspaceRoot.IsKeyboardFocusWithin, $"the keys are not in the workspace, but on {Keyboard.FocusedElement}");
        }
        finally
        {
            _ = host.Shell.EditorLandings.Withdraw();
        }
    });

    /// <summary>
    /// #1318's merge check (R-5 (h) on R-10): hiding the right pane under the
    /// keys while the graph's load or refresh is in flight lands them through
    /// the one entry, and the graph seats that landing PROVISIONALLY — its
    /// state host over a load with nothing held, the grid it still shows over
    /// a load or a refresh of its rows. That seat is the guard's landing: the
    /// keys stay on it, in one focus change, and the request stays held — the
    /// guard never moves them on to the tab's item, which read as the reader
    /// leaving and withdrew it. The terminal publication then seats the grid,
    /// and nothing is spoken for it.
    /// </summary>
    [Theory]
    [InlineData("a load with nothing held")]
    [InlineData("a load over its rows")]
    [InlineData("a refresh of its rows")]
    public void HidingTheRightPaneOverAGraphInFlightKeepsItsProvisionalSeat(string flight) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Links to [[b]].\n"), ("b.md", "Links to [[a]].\n"));
        host.Workspace.OpenGraph();
        GraphDocumentViewModel graph = host.Workspace.GraphDocument!;
        PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
        host.Settle();
        GraphSurfaceView view = ShownShell.Descendants(host.Shell).OfType<GraphSurfaceView>().Single(candidate => candidate.IsVisible);
        Assert.True(host.Land(ShellRegionKind.RightPaneContent), "premise: the right pane took no keys");
        Assert.True(host.Shell.RightPaneBorder.IsKeyboardFocusWithin, "premise: the keys are not in the right pane");
        using var gate = new ManualResetEventSlim(false);
        using var reached = new ManualResetEventSlim(false);
        try
        {
            if (flight == "a load with nothing held")
            {
                // A failed pair leaves ERROR with nothing held, and the next
                // request loads from scratch: LOADING, whose provisional seat
                // is the state host.
                graph.FetchGateForTests = () => throw new InvalidOperationException("The fixture's pair fails.");
                Assert.True(graph.Request(new GraphRequest.Preset(GraphPreset.Orphans)));
                PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
                PumpedDispatcher.Drain();
                Assert.Equal(GraphLoadState.Error, graph.Publication.State);
                graph.FetchGateForTests = null;
            }

            if (flight == "a refresh of its rows")
            {
                graph.FetchGateForTests = () =>
                {
                    reached.Set();
                    _ = gate.Wait(TimeSpan.FromSeconds(30));
                };
                Assert.True(graph.Request(new GraphRequest.Sort(new GraphTableSort(GraphTableColumn.Note, true))));
                Assert.True(reached.Wait(TimeSpan.FromSeconds(30)), "premise: the refresh never started");
            }
            else
            {
                graph.BeforeComputeForTests = () => gate.Wait(TimeSpan.FromSeconds(30));
                Assert.True(graph.Request(new GraphRequest.Needle()));
            }

            Assert.True(graph.IsRequestInFlight, $"premise: nothing is in flight ({flight})");
            Assert.Equal(flight != "a load with nothing held", graph.Publication.HoldsSnapshot);
            Assert.True(host.Shell.RightPaneBorder.IsKeyboardFocusWithin, "premise: the request moved the keys");
            List<IInputElement> changes = host.RecordFocusChanges();
            host.Announced.Clear();

            host.Workspace.ToggleRightPaneCommand.Execute(null);
            PumpedDispatcher.Drain();

            Assert.False(host.Shell.RightPaneBorder.IsVisible);
            IInputElement provisional = Assert.Single(changes);
            Assert.Same(provisional, Keyboard.FocusedElement);
            if (flight == "a load with nothing held")
            {
                Assert.Same(view.StateHostForTests, provisional);
            }
            else
            {
                Assert.True(view.TableForTests.IsKeyboardFocusWithin, $"the provisional seat is not the grid it still shows, but {provisional} ({flight})");
            }

            Assert.NotNull(graph.FocusRequest);
            Assert.True(((IShellRegionHost)host.Shell).HoldsLanding, $"the guard withdrew its own landing ({flight})");
            Assert.Null(((IShellRegionHost)host.Shell).HeldRingRegion);
        }
        finally
        {
            gate.Set();
        }

        PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
        PumpedDispatcher.Drain();
        graph.BeforeComputeForTests = null;
        graph.FetchGateForTests = null;

        Assert.False(graph.IsRequestInFlight);
        Assert.Null(graph.FocusRequest);
        Assert.False(((IShellRegionHost)host.Shell).HoldsLanding);
        Assert.True(view.TableForTests.IsKeyboardFocusWithin, $"the terminal publication seated nothing; the keys are on {Keyboard.FocusedElement} ({flight})");
        Assert.DoesNotContain(host.Announced, line => line is A11yEvent.EditorPaneFocused);
    });

    /// <summary>
    /// #1318's merge check (R-1's launch landing; R-5 (h)): an open that
    /// completes in ONE dispatcher turn — the welcome view collapsing under
    /// the keys, the workspace attached and the real <c>WorkspaceReady</c>
    /// raised, in the lifecycle's own order — moves the keys ONCE. The launch
    /// landing runs at Loaded, ahead of WPF's re-evaluation of the collapsed
    /// welcome view at Input, so the guard's gone landing never lands them
    /// first: with nothing open the launch landing is the Files region, the
    /// empty pane's line spoken once, and no editor or tab landing comes
    /// between.
    /// </summary>
    [Fact]
    public void AnOpenInOneTurnMovesTheKeysOnceToTheLaunchLanding() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        host.ShowWelcomeWithoutAWorkspace();
        Assert.True(host.Shell.OpenVaultButton.Focus(), "premise: the welcome view took no keys");
        PumpedDispatcher.Drain();
        Assert.Same(host.Shell.OpenVaultButton, Keyboard.FocusedElement);
        List<IInputElement> changes = host.RecordFocusChanges();
        host.Announced.Clear();

        host.OpenVaultInOneTurn();
        PumpedDispatcher.Drain();

        Assert.False(host.Shell.WelcomeRoot.IsVisible);
        Assert.True(
            changes.Count == 1,
            $"the open moved the keys {changes.Count} time(s): {string.Join(" -> ", changes)}");
        Assert.Same(changes[0], Keyboard.FocusedElement);
        Assert.True(host.Shell.FilesPaneBorder.IsKeyboardFocusWithin, $"the launch landed on {Keyboard.FocusedElement}");
        Assert.Equal([new A11yEvent.EditorPaneFocused(1, 1, "Empty pane", string.Empty)], host.Announced);
    });

    private static void RunSta(Action body)
    {
        Func<bool> priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        try
        {
            StaThread.RunPumped(body, TimeSpan.FromSeconds(90), "STA test body timed out.");
        }
        finally
        {
            CanvasSurfaceView.ShellOverlayIsOpen = priorOverlayProbe;
        }
    }
}
