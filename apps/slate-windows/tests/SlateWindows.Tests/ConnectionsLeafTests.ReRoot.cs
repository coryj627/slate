// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR B2 (#746), rule D — the root mode on the leaf's document
/// (B2-2, Terms 11, 15, 16): the pin, the pop, the rename and delete
/// hooks, the note in view recorded under a pin, the shared key through
/// core's stable key, the re-root's line, and the boundary reconciling
/// once with the last candidate (IGL-2).
/// </summary>
public sealed partial class ConnectionsLeafTests
{
    private static string StableKey(string path) => SlateUniffiMethods.GraphStableKeyForPath(path);

    private static string ReRooted(string path) =>
        Render(new GraphA11yEvent.GraphReRooted(System.IO.Path.GetFileName(path)));

    /// <summary>Term 12's leaf half: the effective root pushed, the pin set,
    /// the epoch advanced, ONE audible load, the key core's, the line posted.</summary>
    [Fact]
    public void PinToPushesTheOriginPinsIssuesOneLoadWritesTheKeyAndSpeaksTheLine()
    {
        using GraphVault vault = GraphVault.Copy("pin-to");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            int epoch = host.Leaf.RootEpoch;
            int crossings = host.Leaf.CrossingsForTests["graph_stable_key_for_path"];
            int loadsBefore = host.Loads;
            host.Clear();

            Assert.True(host.Leaf.PinTo(Two));
            host.Settle();
            Assert.Equal(Two, host.Leaf.Pin);
            Assert.Equal(Two, host.Leaf.Root);
            Assert.Equal(Hub, host.Leaf.NoteInView);
            Assert.Equal([(null, Hub)], host.Leaf.BackStack);
            Assert.Equal(epoch + 1, host.Leaf.RootEpoch);
            Assert.Equal(1, host.Loads - loadsBefore);
            Assert.Equal(StableKey(Two), host.Workspace.GraphViewStateForTests.SelectedKey);
            // Two crossings: the key's own (Term 15) and the receiver's
            // centre-key check at the load's apply (rule C, Term 8).
            Assert.Equal(crossings + 2, host.Leaf.CrossingsForTests["graph_stable_key_for_path"]);
            Assert.Equal([ReRooted(Two), LineFor(host, Two, 1)], host.RelayLines);
        });
    }

    /// <summary>Term 11: while PINNED a note change is recorded and is not a
    /// root change — no epoch, no load, the presentation kept.</summary>
    [Fact]
    public void ANoteChangeUnderAPinIsRecordedAndLoadsNothing()
    {
        using GraphVault vault = GraphVault.Copy("pinned-note-change");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            Assert.True(host.Leaf.PinTo(Two));
            host.Settle();
            int epoch = host.Leaf.RootEpoch;
            int loadsBefore = host.Loads;
            host.Clear();

            host.Workspace.OpenPath(Deep, WorkspaceOpenTarget.NewTab);
            host.Settle();
            Assert.Equal(Deep, host.Leaf.NoteInView);
            Assert.Equal(Two, host.Leaf.Root);
            Assert.Equal(Two, host.Leaf.Pin);
            Assert.Equal(epoch, host.Leaf.RootEpoch);
            Assert.Equal(0, host.Loads - loadsBefore);
            Assert.True(host.Leaf.IsCurrent);
            Assert.Empty(host.RelayLines);
        });
    }

    /// <summary>Term 13's leaf half: after the ordinary open of the top
    /// entry's note, the pop restores FOLLOWING on that note with ONE audible
    /// load, the key the restored node's, the line its file name.</summary>
    [Fact]
    public void PopToRestoresFollowingOnTheOpenedNoteWithOneLoad()
    {
        using GraphVault vault = GraphVault.Copy("pop-to-following");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            Assert.True(host.Leaf.PinTo(Two));
            host.Settle();
            // The ordinary open of the top entry's note, under the pin.
            host.OpenNote(Hub);
            host.Settle();
            Assert.Equal(Hub, host.Leaf.NoteInView);
            Assert.Equal(Two, host.Leaf.Root);
            int loadsBefore = host.Loads;
            host.Clear();

            Assert.True(host.Leaf.PopTo(Hub, Hub));
            host.Settle();
            Assert.Null(host.Leaf.Pin);
            Assert.Equal(Hub, host.Leaf.Root);
            Assert.Empty(host.Leaf.BackStack);
            Assert.Equal(1, host.Loads - loadsBefore);
            Assert.Equal(StableKey(Hub), host.Workspace.GraphViewStateForTests.SelectedKey);
            Assert.Equal([ReRooted(Hub), LineFor(host, Hub, 1)], host.RelayLines);
        });
    }

    /// <summary>Term 13: a pop restores a PRIOR PIN when the entry holds one.</summary>
    [Fact]
    public void PopToRestoresAPriorPin()
    {
        using GraphVault vault = GraphVault.Copy("pop-to-prior-pin");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            Assert.True(host.Leaf.PinTo(Two));
            host.Settle();
            Assert.True(host.Leaf.PinTo(Deep));
            host.Settle();
            Assert.Equal([(null, Hub), (Two, Two)], host.Leaf.BackStack);
            host.OpenNote(Two);
            host.Settle();
            int loadsBefore = host.Loads;
            host.Clear();

            Assert.True(host.Leaf.PopTo(Two, Two));
            host.Settle();
            Assert.Equal(Two, host.Leaf.Pin);
            Assert.Equal(Two, host.Leaf.Root);
            Assert.Equal([(null, Hub)], host.Leaf.BackStack);
            Assert.Equal(1, host.Loads - loadsBefore);
            Assert.Equal(StableKey(Two), host.Workspace.GraphViewStateForTests.SelectedKey);
        });
    }

    /// <summary>Term 13 / B2-D5: the top entry must name the note the open
    /// INSTALLED — a rename inside the open's dialog rewrote the entry while
    /// the open installed the old path — else nothing pops.</summary>
    [Fact]
    public void PopToRefusesWhenTheTopDoesNotNameTheInstalledNote()
    {
        using GraphVault vault = GraphVault.Copy("pop-to-refused");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            Assert.True(host.Leaf.PinTo(Two));
            host.Settle();
            int loadsBefore = host.Loads;
            host.Clear();

            Assert.False(host.Leaf.PopTo(Deep, Deep));
            Assert.Equal(Two, host.Leaf.Pin);
            Assert.Equal([(null, Hub)], host.Leaf.BackStack);
            Assert.Equal(0, host.Loads - loadsBefore);
            Assert.Empty(host.RelayLines);
            // FOLLOWING, or an empty stack: nothing to pop.
            Assert.True(host.Leaf.PopTo(Hub, Hub));
            host.Settle();
            Assert.False(host.Leaf.PopTo(Hub, Hub));
        });
    }

    /// <summary>Term 16's rename hook: the pin, the note in view and every
    /// entry move by the same-or-descendant rule — a folder rename moves what
    /// it contains — a moved PIN is Term 3(d)'s root move with one audible
    /// load, and the shared key follows the pin only while it was the pin's.</summary>
    [Fact]
    public void RetargetMovesThePinTheEntriesAndTheKeyByTheDescendantRule()
    {
        using GraphVault vault = GraphVault.Copy("retarget");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            Assert.True(host.Leaf.PinTo("notes/nested/other.md"));
            host.Settle();
            Assert.True(host.Leaf.PinTo(Deep));
            host.Settle();
            Assert.Equal([(null, Hub), ("notes/nested/other.md", "notes/nested/other.md")], host.Leaf.BackStack);
            int epoch = host.Leaf.RootEpoch;
            int loadsBefore = host.Loads;
            host.Clear();

            host.Leaf.Retarget("notes/nested", "moved", activeAndMounted: true);
            host.Settle();
            Assert.Equal("moved/deep.md", host.Leaf.Pin);
            Assert.Equal("moved/deep.md", host.Leaf.Root);
            Assert.Equal(epoch + 1, host.Leaf.RootEpoch);
            Assert.Equal(1, host.Loads - loadsBefore);
            Assert.Equal([(null, Hub), ("moved/other.md", "moved/other.md")], host.Leaf.BackStack);
            Assert.Equal(StableKey("moved/deep.md"), host.Workspace.GraphViewStateForTests.SelectedKey);

            // A key that drifted to another selection is left alone.
            host.Workspace.GraphViewStateForTests.SelectedKey = StableKey(Hub);
            host.Leaf.Retarget("moved", "elsewhere", activeAndMounted: false);
            Assert.Equal("elsewhere/deep.md", host.Leaf.Pin);
            Assert.Equal(StableKey(Hub), host.Workspace.GraphViewStateForTests.SelectedKey);
            Assert.True(host.Leaf.IsStale);
            Assert.Equal(1, host.Loads - loadsBefore);

            // The note in view moves too; a path outside the source does not.
            host.Leaf.Retarget(Hub, "hub-renamed.md", activeAndMounted: true);
            Assert.Equal("hub-renamed.md", host.Leaf.NoteInView);
            Assert.Equal([(null, "hub-renamed.md"), ("elsewhere/other.md", "elsewhere/other.md")], host.Leaf.BackStack);
        });
    }

    /// <summary>Term 16's delete hook: the entries under the deleted path
    /// are pruned; the pin and the note in view are kept.</summary>
    [Fact]
    public void PruneDropsTheEntriesUnderTheSourceAndKeepsThePin()
    {
        using GraphVault vault = GraphVault.Copy("prune");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            Assert.True(host.Leaf.PinTo(Deep));
            host.Settle();
            Assert.True(host.Leaf.PinTo(Two));
            host.Settle();
            Assert.Equal([(null, Hub), (Deep, Deep)], host.Leaf.BackStack);

            host.Leaf.Prune("notes");
            Assert.Equal([(null, Hub)], host.Leaf.BackStack);
            Assert.Equal(Two, host.Leaf.Pin);
            host.Leaf.Prune(Two);
            Assert.Equal(Two, host.Leaf.Pin);
            Assert.Equal(Hub, host.Leaf.NoteInView);
            Assert.Equal([(null, Hub)], host.Leaf.BackStack);
        });
    }

    /// <summary>Term 16: after retirement every entry refuses without mutation.</summary>
    [Fact]
    public void EveryRootModeEntryRefusesOnceRetired()
    {
        using GraphVault vault = GraphVault.Copy("retired-entries");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            Assert.True(host.Leaf.PinTo(Two));
            host.Settle();
            string? key = host.Workspace.GraphViewStateForTests.SelectedKey;
            host.Leaf.Retire();
            int loadsBefore = host.Loads;
            host.Clear();

            Assert.False(host.Leaf.PinTo(Deep));
            Assert.False(host.Leaf.PopTo(Hub, Hub));
            host.Leaf.Retarget(Two, "moved.md", activeAndMounted: true);
            host.Leaf.Prune(Hub);
            Assert.Equal(Two, host.Leaf.Pin);
            Assert.Equal([(null, Hub)], host.Leaf.BackStack);
            Assert.Equal(key, host.Workspace.GraphViewStateForTests.SelectedKey);
            Assert.Empty(host.RelayLines);
        });
    }

    /// <summary>Term 14: the re-root's line is posted through the relay
    /// unconditionally — the pin mutation constructs the leaf active, so no
    /// gate is consulted at the leaf.</summary>
    [Fact]
    public void TheReRootLineIsPostedWhateverTheActiveLeaf()
    {
        using GraphVault vault = GraphVault.Copy("re-root-line");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.Workspace.ActiveLeaf = Host.OutlineLeaf;
            host.OpenNote(Hub);
            host.Settle();
            int loadsBefore = host.Loads;
            host.Clear();
            Assert.True(host.Leaf.PinTo(Two));
            host.Settle();
            Assert.Contains(ReRooted(Two), host.RelayLines);
        });
    }

    /// <summary>IGL-2: the outermost boundary reconciles the leaf's root ONCE
    /// with the LAST candidate — a candidate a nested sync recorded earlier
    /// inside the open's dirty dialog (a rename's) is never replayed over the
    /// note the open installed.</summary>
    [Fact]
    public void TheBoundaryReconcilesOnceWithTheLastCandidateNotAnOlderRecordedOne()
    {
        using GraphVault vault = GraphVault.Copy("boundary-once");
        PumpedDispatcher.Run(() =>
        {
            Host? host = null;
            host = new Host(
                vault.Root,
                dirtyNavigationDecision: (_, _) =>
                {
                    // The pump inside the gate: a rename lands, whose
                    // RetargetPath ends in a SyncPanels at nested depth.
                    host!.Workspace.RetargetPath(Hub, "hub-renamed.md");
                    return WorkspaceDirtyNavigationDecision.Discard;
                });
            using (host)
            {
                host.ActivateLeaf();
                host.OpenNote(Hub);
                host.Settle();
                host.Workspace.ActiveGroup.ActiveTab!.Text = "# hub, edited and unsaved\n";
                Assert.True(host.Workspace.ActiveGroup.ActiveTab!.IsDirty, "the tab did not become dirty");
                int loadsBefore = host.Loads;
                host.Clear();

                host.OpenNote(Two);
                host.Settle();
                Assert.Equal(Two, host.Leaf.Root);
                Assert.Equal(Two, host.Leaf.NoteInView);
                Assert.Equal(1, host.Loads - loadsBefore);
                Assert.Equal([LineFor(host, Two, 1)], host.RelayLines);
            }
        });
    }
}
