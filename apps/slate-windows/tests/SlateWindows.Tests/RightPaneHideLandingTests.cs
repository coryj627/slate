// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Canvas;
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
        Assert.True(shell.TryLand(Enum.Parse<ShellRegionKind>(origin)), $"premise: {origin} took no keys");
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
        Assert.True(((IShellRegionHost)host.Shell).TryLand(ShellRegionKind.RightPaneContent), $"premise: {leaf} took no keys");
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
        Assert.True(((IShellRegionHost)host.Shell).TryLand(ShellRegionKind.Editor), "premise: the editor took no keys");

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
