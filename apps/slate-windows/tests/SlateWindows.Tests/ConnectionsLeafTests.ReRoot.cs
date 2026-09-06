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

    /// <summary>Term 12 through the leaf's own entrance (B2-3): the row's
    /// Show connections runs the pin mutation on Show's shape — the leaf
    /// already active and mounted, so no pane or leaf line — then the
    /// ordinary open in place (no `TabFocused`): the pin, the note in view,
    /// one load, the key, and the lines in Term 14's order.</summary>
    [Fact]
    public void TheLeafsShowConnectionsPinsThenOpensWithOneLoadAndTheLinesInOrder()
    {
        using GraphVault vault = GraphVault.Copy("leaf-entrance");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            GraphConnectionRow row = host.Leaf.Publication.Tree!.Outgoing.First(r => r.Kind == GraphNodeKind.Note);
            string target = row.Path!;
            int loadsBefore = host.Loads;
            host.Clear();

            Assert.True(host.Leaf.IsActionEnabled(GraphRowAction.ShowConnections, row));
            host.Leaf.Execute(GraphRowAction.ShowConnections, row);
            host.Settle();
            Assert.Equal(target, host.Leaf.Pin);
            Assert.Equal(target, host.Leaf.Root);
            Assert.Equal(target, host.Leaf.NoteInView);
            Assert.Equal(target, host.Workspace.ActiveGroup.ActiveTab!.Path);
            Assert.Equal([(null, Hub)], host.Leaf.BackStack);
            Assert.Equal(1, host.Loads - loadsBefore);
            Assert.Equal(StableKey(target), host.Workspace.GraphViewStateForTests.SelectedKey);
            Assert.Equal([ReRooted(target), LineFor(host, target, 1)], host.Timeline);
        });
    }

    /// <summary>Term 12 / B2D-7: a refused open (the dirty gate's Cancel)
    /// leaves the pin, the stack, the key and the line exactly as the pin
    /// mutation left them — the mac's outcome; the note in view unchanged.</summary>
    [Fact]
    public void AReRootsRefusedOpenLeavesThePinStanding()
    {
        using GraphVault vault = GraphVault.Copy("re-root-refused");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root, dirtyNavigationDecision: (_, _) => WorkspaceDirtyNavigationDecision.Cancel);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            host.Workspace.ActiveGroup.ActiveTab!.Text = "# hub, edited and unsaved\n";
            Assert.True(host.Workspace.ActiveGroup.ActiveTab!.IsDirty);
            int loadsBefore = host.Loads;
            host.Clear();

            Assert.True(host.Workspace.ReRootConnectionsOn(Two));
            host.Settle();
            Assert.Equal(Two, host.Leaf.Pin);
            Assert.Equal(Hub, host.Leaf.NoteInView);
            Assert.Equal(Hub, host.Workspace.ActiveGroup.ActiveTab!.Path);
            Assert.Equal([(null, Hub)], host.Leaf.BackStack);
            Assert.Equal(1, host.Loads - loadsBefore);
            Assert.Equal(StableKey(Two), host.Workspace.GraphViewStateForTests.SelectedKey);
            Assert.Equal([ReRooted(Two), LineFor(host, Two, 1)], host.RelayLines);
        });
    }

    /// <summary>Term 12 / B2D-6: already pinned on the path — the shared key
    /// repaired, the pane revealed and the leaf activated as B1's triggers
    /// say, no push, no load of the route's own, no re-root line.</summary>
    [Fact]
    public void ASameRootReRootRepairsTheKeyRevealsAndProposesNothing()
    {
        using GraphVault vault = GraphVault.Copy("same-root");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            Assert.True(host.Workspace.ReRootConnectionsOn(Two));
            host.Settle();
            host.Workspace.GraphViewStateForTests.SelectedKey = StableKey(Hub);
            host.Workspace.ActiveLeaf = Host.OutlineLeaf;
            host.Settle();
            int loadsBefore = host.Loads;
            host.Clear();

            Assert.False(host.Workspace.ReRootConnectionsOn(Two));
            host.Settle();
            Assert.Equal(Two, host.Leaf.Pin);
            Assert.Equal([(null, Hub)], host.Leaf.BackStack);
            Assert.Equal(StableKey(Two), host.Workspace.GraphViewStateForTests.SelectedKey);
            Assert.True(host.Workspace.ConnectionsLeafIsActive());
            // The mounted switch to a CURRENT leaf loads nothing (Term 3(b)).
            Assert.Equal(0, host.Loads - loadsBefore);
            Assert.DoesNotContain(ReRooted(Two), host.RelayLines);
            Assert.Contains("LeafPanelShown", host.Timeline);
        });
    }

    /// <summary>Term 13 / B2-D5: Back opens the top entry's note first, then
    /// pops with one load; a refused open pops nothing; FOLLOWING or an
    /// empty stack falls through.</summary>
    [Fact]
    public void BackOpensThenPopsAndARefusedOpenPopsNothing()
    {
        using GraphVault vault = GraphVault.Copy("back");
        PumpedDispatcher.Run(() =>
        {
            var decision = WorkspaceDirtyNavigationDecision.Discard;
            using var host = new Host(vault.Root, dirtyNavigationDecision: (_, _) => decision);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            Assert.False(host.Workspace.ConnectionsBack());
            Assert.True(host.Workspace.ReRootConnectionsOn(Two));
            host.Settle();
            Assert.Equal(Two, host.Workspace.ActiveGroup.ActiveTab!.Path);

            // A refused open: nothing pops — and nothing of the pop mutation
            // runs either: with the pane collapsed and another leaf active,
            // no pane line, no leaf line, the pane still collapsed.
            host.Workspace.ActiveGroup.ActiveTab!.Text = "# two, edited and unsaved\n";
            host.Workspace.IsRightPaneVisible = false;
            host.Workspace.ActiveLeaf = Host.OutlineLeaf;
            host.Settle();
            decision = WorkspaceDirtyNavigationDecision.Cancel;
            int loadsBefore = host.Loads;
            host.Clear();
            Assert.False(host.Workspace.ConnectionsBack());
            Assert.Equal(Two, host.Leaf.Pin);
            Assert.Equal([(null, Hub)], host.Leaf.BackStack);
            Assert.Equal(0, host.Loads - loadsBefore);
            Assert.False(host.Workspace.IsRightPaneVisible);
            Assert.False(host.Workspace.ConnectionsLeafIsActive());
            Assert.Empty(host.Timeline);

            // The open allowed: the note in view moves under the pin, then
            // the pop mutation reveals, activates and restores FOLLOWING on
            // it with one load.
            decision = WorkspaceDirtyNavigationDecision.Discard;
            host.Clear();
            Assert.True(host.Workspace.ConnectionsBack());
            host.Settle();
            Assert.Null(host.Leaf.Pin);
            Assert.Equal(Hub, host.Leaf.Root);
            Assert.Equal(Hub, host.Workspace.ActiveGroup.ActiveTab!.Path);
            Assert.Empty(host.Leaf.BackStack);
            Assert.Equal(1, host.Loads - loadsBefore);
            Assert.True(host.Workspace.IsRightPaneVisible);
            Assert.True(host.Workspace.ConnectionsLeafIsActive());
            Assert.Equal(StableKey(Hub), host.Workspace.GraphViewStateForTests.SelectedKey);
            // Term 14: the pane's line, the setter's, the re-root's, the summary.
            Assert.Equal(["RightPaneShown", "LeafPanelShown", ReRooted(Hub), LineFor(host, Hub, 1)], host.Timeline);
            Assert.False(host.Workspace.ConnectionsBack());
        });
    }

    /// <summary>B2-3 (IGI-4): the table's Show connections makes the graph's
    /// group and tab active first, then pins; from a split whose other
    /// group is active, the re-root's open lands in the graph's group.</summary>
    [Fact]
    public void TheTablesShowConnectionsFocusesTheGraphsGroupThenPins()
    {
        using GraphVault vault = GraphVault.Copy("table-entrance");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            host.Workspace.SplitRightCommand.Execute(null);
            host.Workspace.OpenGraph();
            SettleTheDocuments(host);
            GraphDocumentViewModel document = host.Workspace.GraphDocument!;
            WorkspaceGroupViewModel graphsGroup = host.Workspace.ActiveGroup;
            // The other group active: the entrance must address the graph's.
            Assert.True(host.Workspace.FocusDirectionalPane("horizontal", -1));
            Assert.NotSame(graphsGroup, host.Workspace.ActiveGroup);
            SettleTheDocuments(host);
            GraphTableRow row = document.Publication.Rows.First(r => string.Equals(r.Path, Two, StringComparison.Ordinal));
            host.Clear();

            Assert.True(document.IsActionEnabled(GraphRowAction.ShowConnections, row));
            document.Execute(GraphRowAction.ShowConnections, row);
            SettleTheDocuments(host);
            Assert.Same(graphsGroup, host.Workspace.ActiveGroup);
            Assert.Equal(Two, host.Leaf.Pin);
            Assert.Equal(Two, host.Workspace.ActiveGroup.ActiveTab!.Path);
            Assert.Equal(StableKey(Two), host.Workspace.GraphViewStateForTests.SelectedKey);
            // The address made the graph tab the note in view before the
            // pin: no effective root, nothing pushed (B2-D7), Back falls through.
            Assert.Empty(host.Leaf.BackStack);
            Assert.False(host.Workspace.ConnectionsBack());
            // A row a republish dropped acts on nothing (the current-row wall).
            var stale = row with { };
            Assert.False(document.IsRowCurrent(stale));
            document.Execute(GraphRowAction.ShowConnections, stale);
            SettleTheDocuments(host);
            Assert.Empty(host.Leaf.BackStack);
        });
    }

    /// <summary>IGL-3: the re-root's open withholds the editor's focus
    /// request, and the leaf's boundary request is the last one raised.</summary>
    [Fact]
    public void AReRootsOpenWithholdsTheEditorsFocusRequest()
    {
        using GraphVault vault = GraphVault.Copy("re-root-focus");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            var requests = new List<string>();
            host.Workspace.EditorPaneFocusRequested += (_, _) => requests.Add("editor");
            host.Workspace.FocusBoundaryRequested += (_, boundary) => requests.Add(boundary.ToString());

            Assert.True(host.Workspace.ReRootConnectionsOn(Two));
            host.Settle();
            Assert.Equal(["RightPane"], requests);
            requests.Clear();
            host.OpenNote(Deep);
            host.Settle();
            Assert.Equal(["editor"], requests);
        });
    }

    /// <summary>IGL-6: an open whose active group changed inside the dirty
    /// gate's pump installs nothing — the captured tab's group must still be
    /// the active one and still hold it.</summary>
    [Fact]
    public void AnOpenWhoseGroupChangedInsideTheGateInstallsNothing()
    {
        using GraphVault vault = GraphVault.Copy("open-group-changed");
        PumpedDispatcher.Run(() =>
        {
            Host? host = null;
            host = new Host(
                vault.Root,
                dirtyNavigationDecision: (_, _) =>
                {
                    // The pump inside the gate: the other group becomes active.
                    Assert.True(host!.Workspace.FocusDirectionalPane("horizontal", -1));
                    return WorkspaceDirtyNavigationDecision.Discard;
                });
            using (host)
            {
                host.ActivateLeaf();
                host.OpenNote(Hub);
                host.Settle();
                host.Workspace.SplitRightCommand.Execute(null);
                host.Settle();
                WorkspaceGroupViewModel origin = host.Workspace.ActiveGroup;
                origin.ActiveTab!.Text = "# hub, edited and unsaved\n";
                Assert.True(origin.ActiveTab!.IsDirty);

                host.OpenNote(Two);
                host.Settle();
                Assert.All(host.Workspace.Groups.SelectMany(group => group.Tabs), tab => Assert.NotEqual(Two, tab.Path));
                Assert.All(host.Workspace.Groups, group => Assert.True(group.ActiveTab is null || group.Tabs.Contains(group.ActiveTab)));
            }
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
