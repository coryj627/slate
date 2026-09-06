// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR B, slice B1 (#746): SPEAKING at apply (Term 2), the view-local
/// state (B-6, Term 2), focus entry (Term 9), the row copy and actions
/// (B-8, B-9), the three commands (B-14) and the label inventory (B-16).
/// </summary>
public sealed partial class ConnectionsLeafTests
{
    // --- SPEAKING is decided at apply (Term 2; IGH-14) ------------------------------

    [Fact]
    public void ASwitchAwayThenBackBeforeTheCompletionSpeaksAndAHideThenAwayDoesNot()
    {
        using GraphVault vault = GraphVault.Copy("speaking-at-apply");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            host.Clear();

            // Away then back before the completion lands: SPEAKING at apply.
            (ManualResetEventSlim gate, ManualResetEventSlim reached) = Park(leaf);
            using (gate)
            using (reached)
            {
                host.Workspace.ShowConnections();
                Assert.True(reached.Wait(TimeSpan.FromSeconds(10)), "the load never reached the gate");
                host.Workspace.ActiveLeaf = Host.OutlineLeaf;
                host.ActivateLeaf();
                gate.Set();
                host.Settle();
                Assert.Equal([PanelLine(), Summary(leaf)], host.RelayLines);
                Assert.Equal(["LeafPanelShown", "LeafPanelShown"], host.ShellEvents.Select(e => e.GetType().Name));
            }

            // Hide then away: not ACTIVE at apply — silent, whatever MOUNTED.
            host.Clear();
            (ManualResetEventSlim gate2, ManualResetEventSlim reached2) = Park(leaf);
            using (gate2)
            using (reached2)
            {
                host.Workspace.ShowConnections();
                Assert.True(reached2.Wait(TimeSpan.FromSeconds(10)), "the load never reached the gate");
                host.Workspace.IsRightPaneVisible = false;
                host.Workspace.ActiveLeaf = Host.OutlineLeaf;
                gate2.Set();
                host.Settle();
                Assert.Equal([PanelLine()], host.RelayLines);
                Assert.Equal(ConnectionsLoadState.Ready, leaf.Publication.State);
            }
        });
    }

    // --- The view-local state (B-6, Term 2) -----------------------------------------

    [Fact]
    public void TheSelectionIsScopedByTheRootAndTheMountAndPrunedByARefresh()
    {
        using GraphVault vault = GraphVault.Copy("selection");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            leaf.SetDepth(2);
            host.Settle();
            GraphConnectionRow nested = leaf.Publication.Tree!.Outgoing.First(row => row.Level == 2);
            leaf.SelectedOccurrence = nested.Id;

            // A depth change prunes: the level-2 occurrence vanishes at depth 1.
            leaf.SetDepth(1);
            host.Settle();
            Assert.Null(leaf.SelectedOccurrence);

            // A surviving occurrence stays through a same-root refresh.
            GraphConnectionRow first = leaf.Publication.Tree!.Outgoing.First();
            leaf.SelectedOccurrence = first.Id;
            host.Workspace.ShowConnections();
            host.Settle();
            Assert.Equal(first.Id, leaf.SelectedOccurrence);

            // A root change clears, even when the new root shares the neighbour's id.
            host.OpenNote(Two);
            Assert.Null(leaf.SelectedOccurrence);
            host.Settle();
            leaf.SelectedOccurrence = leaf.Publication.Tree!.Outgoing.First().Id;

            // The pane collapse clears (the mac's destruction); the document survives.
            ConnectionsPublication held = leaf.Publication;
            host.Workspace.IsRightPaneVisible = false;
            Assert.Null(leaf.SelectedOccurrence);
            Assert.Same(held, leaf.Publication);
            Assert.Equal(Two, leaf.Root);
        });
    }

    [Fact]
    public void TwoRootsSharingANeighbourReuseTheOccurrenceIdAcrossTheClear()
    {
        using GraphVault vault = GraphVault.Copy("shared-neighbour");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Two);
            host.Settle();
            // 2 → hub is an outgoing occurrence "out/<key of hub>"; hub → 2 is
            // "out/<key of 2>" — the id names the path from the first hop,
            // not the root (B-6), so the SAME string can appear under two
            // roots; the view keys by (root, id) and clears on the change.
            string fromTwo = leaf.Publication.Tree!.Outgoing.Single(row => row.Path == Hub).Id;
            leaf.SelectedOccurrence = fromTwo;
            host.OpenNote(Hub);
            Assert.Null(leaf.SelectedOccurrence);
            host.Settle();
            Assert.Contains(leaf.Publication.Tree!.Incoming, row => row.Id == fromTwo || row.Path == Two);
        });
    }

    // --- Focus entry (Term 9) ----------------------------------------------------------

    [Fact]
    public void FocusEntrySpeaksThePresentationOnceAndIsSilentOnErrorNoNoteAndRows()
    {
        using GraphVault vault = GraphVault.Copy("focus-entry");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();

            // NoNote: nothing.
            leaf.FocusEntered();
            Assert.Empty(host.RelayLines);

            // Loading: the loading status.
            host.OpenNote(Hub);
            leaf.FocusEntered();
            Assert.Equal([LoadingLine()], host.RelayLines);
            host.Settle();
            host.Clear();

            // Ready with rows: nothing — the row reads.
            leaf.FocusEntered();
            Assert.Empty(host.RelayLines);

            // STALE: the loading status.
            host.Workspace.ActiveLeaf = Host.OutlineLeaf;
            host.OpenNote(Two);
            host.Clear();
            leaf.FocusEntered();
            Assert.Equal([LoadingLine()], host.RelayLines);
            host.ActivateLeaf();
            host.Settle();

            // Error: nothing — the static label reads.
            host.Workspace.OpenPath("missing-note.md", WorkspaceOpenTarget.NewTab);
            host.Settle();
            Assert.Equal(ConnectionsLoadState.Error, leaf.Publication.State);
            host.Clear();
            leaf.FocusEntered();
            Assert.Empty(host.RelayLines);
        });
    }

    [Fact]
    public void FocusEntryOnTheEmptyNeighbourhoodSpeaksNoConnections()
    {
        using GraphVault vault = GraphVault.Copy("focus-empty");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            File.WriteAllText(Path.Combine(vault.Root, "lonely.md"), "# Lonely\n");
            using (var cancel = new CancelToken())
            {
                host.Session.ScanInitial(cancel);
            }
            host.ActivateLeaf();
            host.OpenNote("lonely.md");
            host.Settle();
            Assert.True(leaf.Publication.HoldsTree);
            Assert.False(leaf.Publication.HasRows);
            host.Clear();

            leaf.FocusEntered();

            Assert.Equal([NoConnectionsLine()], host.RelayLines);
        });
    }

    // --- The row copy and the actions (B-8, B-9) -----------------------------------

    [Fact]
    public void TheRowsNameIsCoresCopyAndItsHintIsItsActivations()
    {
        using GraphVault vault = GraphVault.Copy("row-copy");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            GraphConnectionRow note = leaf.Publication.Tree!.Outgoing.First(row => row.Kind == GraphNodeKind.Note);
            GraphConnectionRow ghost = leaf.Publication.Tree!.Outgoing.First(row => row.Kind == GraphNodeKind.Ghost);

            Assert.Equal(
                Render(new GraphA11yEvent.GraphRow(GraphVerbosity.Standard, ConnectionsLeafViewModel.RowCopy(note))),
                leaf.RowName(note));
            Assert.Equal(ConnectionsPhrase.NoteHint, leaf.RowHint(note));
            Assert.Equal(ConnectionsPhrase.GhostHint, leaf.RowHint(ghost));
            Assert.Equal(ghost.References, ConnectionsLeafViewModel.RowCopy(ghost).References);

            // B-9: Show connections is listed, disabled, with the one B1
            // reason on the ACTION alone — never the row's hint (IGG-15).
            Assert.Contains(leaf.ActionSpecs(GraphNodeKind.Note), spec => spec.Action == GraphRowAction.ShowConnections);
            Assert.False(leaf.IsActionEnabled(GraphRowAction.ShowConnections, note));
            Assert.Equal(ConnectionsPhrase.ShowConnectionsUnavailable, leaf.ActionDisabledReason(GraphRowAction.ShowConnections));
            Assert.DoesNotContain(ConnectionsPhrase.ShowConnectionsUnavailable, leaf.RowHint(note), StringComparison.Ordinal);
            Assert.True(leaf.IsActionEnabled(GraphRowAction.Open, note));
            Assert.True(leaf.IsActionEnabled(GraphRowAction.Reveal, note));
            Assert.False(leaf.IsActionEnabled(GraphRowAction.Open, ghost));
            Assert.True(leaf.IsActionEnabled(GraphRowAction.CreateNote, ghost));
            Assert.Equal(3, leaf.CrossingsForTests["graph_row_actions"]);
        });
    }

    [Fact]
    public void OpenFromTheLeafOpensInTheActivePaneAndPostsTheShellsLine()
    {
        using GraphVault vault = GraphVault.Copy("open-row");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            GraphConnectionRow note = leaf.Publication.Tree!.Outgoing.First(row => row.Path == Two);
            host.Clear();

            leaf.Activate(note, newTab: false);

            Assert.Equal(Two, host.Workspace.ActiveGroup.ActiveTab!.Path);
            Assert.Contains(host.ShellEvents, e => e is A11yEvent.OpenedFile);
            // The root followed the open (Term 3(d)) and loaded audibly.
            Assert.Equal(Two, leaf.Root);
            host.Settle();
            Assert.Contains(Summary(leaf), host.RelayLines);
        });
    }

    // --- The commands (B-14) and the labels (B-16) -------------------------------------

    [Fact]
    public void TheThreeCommandsAreRegisteredChordlessAndAlwaysEnabled()
    {
        using GraphVault vault = GraphVault.Copy("commands");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            Assert.True(host.Workspace.ShowConnectionsCommand.CanExecute(null));
            Assert.True(host.Workspace.ConnectionsDeeperCommand.CanExecute(null));
            Assert.True(host.Workspace.ConnectionsShallowerCommand.CanExecute(null));
            // Without a root: no load, no line (Term 3(e)).
            host.Workspace.ConnectionsDeeperCommand.Execute(null);
            host.Workspace.ConnectionsShallowerCommand.Execute(null);
            Assert.Equal(0, host.Loads);
            Assert.Empty(host.RelayLines);
            Assert.Equal(1u, host.Leaf.Depth);
        });
    }

    [Fact]
    public void TheLabelInventoryIsTheMacsByteForByte()
    {
        Assert.Equal("Select a note to see its connections.", ConnectionsPhrase.NoNote);
        Assert.Equal("Loading connections…", ConnectionsPhrase.LoadingVisible);
        Assert.Equal("Loading connections.", ConnectionsPhrase.LoadingAccessible);
        Assert.Equal("This note has no connections.", ConnectionsPhrase.Empty);
        Assert.Equal("Connections error: boom", ConnectionsPhrase.Error("boom"));
        Assert.Equal("Linked from, 1 note", ConnectionsPhrase.GroupHeader(ConnectionsPhrase.IncomingTitle, 1));
        Assert.Equal("Links to, 0 notes", ConnectionsPhrase.GroupHeader(ConnectionsPhrase.OutgoingTitle, 0));
        Assert.Equal("Links to, 12 notes", ConnectionsPhrase.GroupHeader(ConnectionsPhrase.OutgoingTitle, 12));
        Assert.Equal("Nothing links here.", ConnectionsPhrase.IncomingEmpty);
        Assert.Equal("This note links to nothing.", ConnectionsPhrase.OutgoingEmpty);
        Assert.Equal(["Unresolved", "Embed", "Attachment"], [ConnectionsPhrase.BadgeUnresolved, ConnectionsPhrase.BadgeEmbed, ConnectionsPhrase.BadgeAttachment]);
        Assert.Equal("Unresolved. Choose Create note to add it.", ConnectionsPhrase.GhostHint);
        Assert.Equal("Opens the note.", ConnectionsPhrase.NoteHint);
        Assert.Equal("Local graph depth", ConnectionsPhrase.DepthName);
        Assert.Equal("How many links away from this note to include.", ConnectionsPhrase.DepthHint);
        Assert.Equal(["Links", "2 links away", "3 links away"], ConnectionsPhrase.DepthTags);
        // The static loading label and the leaf's LoadingConnections status
        // render the same sentence (0a-D3: the status is Windows's).
        Assert.Equal(ConnectionsPhrase.LoadingAccessible, LoadingLine());
        Assert.Equal(ConnectionsPhrase.Empty, NoConnectionsLine());
    }
}
