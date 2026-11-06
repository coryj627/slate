// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>DS-14 (#1815): a narrow spell suspends the right pane, it doesn't hide it. The
/// reader's place in Connections survives, nothing is armed or spoken, and coming back
/// reconciles silently. A reader's own hide and show keep Term 2 and Term 3(a).</summary>
public sealed partial class ConnectionsLeafTests
{
    private const double Wide = 1200;
    private const double Narrow = 500;

    private static string FirstRow(ConnectionsLeafViewModel leaf) =>
        leaf.Publication.Tree!.Outgoing.Concat(leaf.Publication.Tree.Incoming).First().Id;

    [Fact]
    public void ANarrowSpellKeepsTheConnectionsSelectionAndExpansion()
    {
        using GraphVault vault = GraphVault.Copy("narrow-keeps");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.WorkspaceRowWidth = Wide;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            string row = FirstRow(host.Leaf);
            host.Leaf.SelectedOccurrence = row;
            int epoch = host.Leaf.ViewStateEpoch;

            host.Workspace.WorkspaceRowWidth = Narrow;
            Assert.False(host.Workspace.IsRightPaneVisible);
            Assert.False(host.Workspace.ConnectionsLeafIsMounted());
            host.Workspace.WorkspaceRowWidth = Wide;
            host.Settle();

            Assert.True(host.Workspace.IsRightPaneVisible);
            Assert.Equal(row, host.Leaf.SelectedOccurrence);
            Assert.Equal(epoch, host.Leaf.ViewStateEpoch);
            Assert.False(host.Workspace.ConnectionsMountPendingForTests);
        });
    }

    [Fact]
    public void ComingBackFromANarrowSpellSpeaksNothing()
    {
        using GraphVault vault = GraphVault.Copy("narrow-silent");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.WorkspaceRowWidth = Wide;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            int loads = host.Loads;
            host.Clear();

            host.Workspace.WorkspaceRowWidth = Narrow;
            host.Settle();
            host.Workspace.WorkspaceRowWidth = Wide;
            host.Settle();

            Assert.Empty(host.Timeline);
            Assert.Equal(loads, host.Loads);
            Assert.True(host.Leaf.IsCurrent);
        });
    }

    /// <summary>A publication during the spell, with its load in flight across the return:
    /// after widening the presentation is the new tree, and nothing was spoken.</summary>
    [Fact]
    public void APublicationDuringANarrowSpellIsReconciledSilently()
    {
        using GraphVault vault = GraphVault.Copy("narrow-publication");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.Workspace.WorkspaceRowWidth = Wide;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            string before = Summary(leaf);
            host.Clear();

            host.Workspace.WorkspaceRowWidth = Narrow;
            (ManualResetEventSlim gate, ManualResetEventSlim reached) = Park(leaf);
            File.WriteAllText(Path.Combine(vault.Root, "narrow-new.md"), "[[hub]]\n");
            using (var cancel = new CancelToken())
            {
                host.Session.ScanInitial(cancel);
            }
            host.Workspace.NotifyGraphOfVaultChange();
            Assert.True(
                SpinWait.SpinUntil(() =>
                {
                    PumpedDispatcher.Drain();
                    return reached.IsSet;
                }, TimeSpan.FromSeconds(10)),
                "no load was issued for the publication");

            host.Workspace.WorkspaceRowWidth = Wide;
            gate.Set();
            host.Settle();

            Assert.True(leaf.IsCurrent);
            Assert.Equal(ConnectionsLoadState.Ready, leaf.Publication.State);
            Assert.NotEqual(before, Summary(leaf));
            Assert.Empty(host.Timeline);
        });
    }

    /// <summary>A note change during the spell is a root change, so the selection clears as
    /// it always does; the presentation waits, stale, and coming back loads the new note's
    /// tree with no summary.</summary>
    [Fact]
    public void ANoteChangedDuringANarrowSpellLoadsSilently()
    {
        using GraphVault vault = GraphVault.Copy("narrow-note");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.WorkspaceRowWidth = Wide;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            host.Leaf.SelectedOccurrence = FirstRow(host.Leaf);
            host.Workspace.WorkspaceRowWidth = Narrow;
            int loads = host.Loads;

            host.OpenNote(Two);
            Assert.Null(host.Leaf.SelectedOccurrence);
            Assert.True(host.Leaf.IsStale);
            Assert.Equal(loads, host.Loads);
            host.Clear();

            host.Workspace.WorkspaceRowWidth = Wide;
            host.Settle();

            Assert.True(host.Leaf.IsCurrent);
            Assert.Equal(loads + 1, host.Loads);
            Assert.Empty(host.Timeline);
        });
    }

    /// <summary>Narrow or not, a reader's hide still clears the leaf's view-local state and
    /// a reader's show still speaks the pane and the summary (Term 2, Term 3(a)).</summary>
    [Fact]
    public void AnExplicitHideStillClearsAndAnExplicitShowStillSpeaks()
    {
        using GraphVault vault = GraphVault.Copy("narrow-explicit");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.WorkspaceRowWidth = Wide;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            host.Workspace.WorkspaceRowWidth = Narrow;
            host.Clear();

            host.Workspace.RightPaneToggle = true;
            host.Settle();
            Assert.Equal(["RightPaneShown", Summary(host.Leaf)], host.Timeline);
            Assert.False(host.Workspace.IsFilesSidebarVisible);
            host.Leaf.SelectedOccurrence = FirstRow(host.Leaf);
            int epoch = host.Leaf.ViewStateEpoch;
            host.Clear();

            host.Workspace.ToggleRightPaneCommand.Execute(null);
            Assert.Null(host.Leaf.SelectedOccurrence);
            Assert.Equal(epoch + 1, host.Leaf.ViewStateEpoch);
            Assert.Equal(["RightPaneHidden"], host.Timeline);
            Assert.True(host.Workspace.IsFilesSidebarVisible);
            host.Clear();

            host.Workspace.ToggleRightPaneCommand.Execute(null);
            host.Settle();
            Assert.Equal(["RightPaneShown", Summary(host.Leaf)], host.Timeline);
        });
    }
}
