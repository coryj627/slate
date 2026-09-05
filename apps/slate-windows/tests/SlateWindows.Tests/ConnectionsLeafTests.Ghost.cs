// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR B, slice B1 (#746), contract B-11: the ghost create addressed
/// by the LEAF's root and epoch — no graph tab needed; the open suppressed
/// when the root or the epoch moved, and NOT when only the leaf went
/// inactive (B-D10); ONE <c>NoteCreated</c> after the attempt; the summary
/// only when the open's root change loaded audibly.
/// </summary>
public sealed partial class ConnectionsLeafTests
{
    private sealed class RecordingCreator(VaultSession session, Action? beforeCreate = null) : FileManagement.ISurfaceNoteCreator
    {
        public List<string> Created { get; } = [];
        public List<string> Landed { get; } = [];
        public List<string> Caveats { get; } = [];

        public FileManagement.NoteCreateResult TryCreateNote(string path, string content)
        {
            beforeCreate?.Invoke();
            try
            {
                string? caveat = CreateOutcomes.CreateReporting(session, path, content, Path.GetFileName(path));
                Created.Add(path);
                return new FileManagement.NoteCreateResult.Landed(caveat);
            }
            catch (VaultException.DestinationExists exception)
            {
                return new FileManagement.NoteCreateResult.Exists(exception.Message);
            }
            catch (VaultException exception)
            {
                return new FileManagement.NoteCreateResult.Failed(exception.Message);
            }
        }

        public void NoteLanded(string path) => Landed.Add(path);

        public void SpeakCaveat(string caveat) => Caveats.Add(caveat);
    }

    /// <summary>A creator whose every attempt FAILS with a fixed message —
    /// the typed <c>Failed</c> arm, deterministic (IPB-1's regression).</summary>
    private sealed class FailingCreator(string message) : FileManagement.ISurfaceNoteCreator
    {
        public FileManagement.NoteCreateResult TryCreateNote(string path, string content) =>
            new FileManagement.NoteCreateResult.Failed(message);

        public void NoteLanded(string path) =>
            throw new InvalidOperationException("a failing create never lands");

        public void SpeakCaveat(string caveat) =>
            throw new InvalidOperationException("a failing create has no caveat");
    }

    private static bool IsNoteCreated(A11yEvent @event) =>
        @event is A11yEvent.Graph { Event: GraphA11yEvent.GraphStatus { Note: GraphStatusNote.NoteCreated } };

    private static (GraphConnectionRow Ghost, string Path) FirstGhost(ConnectionsLeafViewModel leaf)
    {
        GraphConnectionRow ghost = leaf.Publication.Tree!.Outgoing.First(row => row.Kind == GraphNodeKind.Ghost);
        return (ghost, SlateUniffiMethods.GraphGhostNotePath(ghost.TargetRaw));
    }

    private static void DrainCreate(Host host)
    {
        PumpedDispatcher.PumpUntilDrained(host.Workspace.DrainGraphNoteCreationForTests());
        PumpedDispatcher.Drain();
        host.Settle();
    }

    [Fact]
    public void AGhostsCreateNeedsNoGraphTabLandsOpensAndSpeaksOneNoteCreatedThenTheNewRootsSummary()
    {
        using GraphVault vault = GraphVault.Copy("ghost-create");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            var creator = new RecordingCreator(host.Session);
            host.Workspace.GraphNoteCreator = creator;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            (GraphConnectionRow ghost, string expectedPath) = FirstGhost(leaf);
            Assert.Null(host.Workspace.GraphDocument);
            host.Clear();

            leaf.Activate(ghost, newTab: false);
            DrainCreate(host);

            Assert.Equal([expectedPath], creator.Created);
            Assert.Equal([expectedPath], creator.Landed);
            // The open ran (the source current): the new note is in view,
            // the root followed it, and its summary spoke after the ONE
            // NoteCreated line (the open is silent).
            Assert.Equal(expectedPath, host.Workspace.ActiveGroup.ActiveTab!.Path);
            Assert.Equal(expectedPath, leaf.Root);
            Assert.Single(host.ShellEvents, IsNoteCreated);
            Assert.DoesNotContain(host.ShellEvents, e => e is A11yEvent.OpenedFile);
            Assert.Equal(ConnectionsLoadState.Ready, leaf.Publication.State);
            Assert.Equal([Summary(leaf)], host.RelayLines);
        });
    }

    /// <summary>A-10 as amended, codex post-implementation pass 1 (IPB-1):
    /// the create's failure line is a HIGH graph event through the
    /// workspace's ONE relay, so a class pending on that relay — the graph
    /// table's filter count rides the same relay — is DROPPED by its flush
    /// and never spoken after the failure; nothing lands, no NoteCreated,
    /// the root unmoved.</summary>
    [Fact]
    public void AFailedCreateRidesTheRelayAndItsHighFlushDropsAPendingClass()
    {
        using GraphVault vault = GraphVault.Copy("ghost-create-fails");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            host.Workspace.GraphNoteCreator = new FailingCreator("injected failure");
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            (GraphConnectionRow ghost, _) = FirstGhost(leaf);
            host.Clear();
            GraphAnnouncer relay = host.Workspace.GraphRelayForTests;
            relay.Announce(new GraphA11yEvent.GraphFilterCount(3, 9));
            Assert.Equal(1, relay.PendingForTests);

            leaf.Activate(ghost, newTab: false);
            DrainCreate(host);

            string failure = Render(new GraphA11yEvent.GraphBlocked(new GraphBlockedReason.NoteCreateFailed("injected failure")));
            Assert.Equal([failure], host.RelayLines);
            Assert.Equal(0, relay.PendingForTests);
            relay.FlushForTests();
            Assert.Equal([failure], host.RelayLines);
            Assert.DoesNotContain(host.ShellEvents, IsNoteCreated);
            Assert.Equal(Hub, leaf.Root);
        });
    }

    [Fact]
    public void AGhostsCreateParkedAcrossARootMoveLandsAndSpeaksButOpensNothing()
    {
        using GraphVault vault = GraphVault.Copy("ghost-root-moved");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            using var gate = new ManualResetEventSlim(false);
            using var reached = new ManualResetEventSlim(false);
            var creator = new RecordingCreator(host.Session, () =>
            {
                reached.Set();
                gate.Wait(TimeSpan.FromSeconds(10));
            });
            host.Workspace.GraphNoteCreator = creator;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            (GraphConnectionRow ghost, string expectedPath) = FirstGhost(leaf);
            host.Clear();

            leaf.Activate(ghost, newTab: false);
            Assert.True(reached.Wait(TimeSpan.FromSeconds(10)), "the create never reached the gate");
            host.OpenNote(Two);
            host.Settle();
            gate.Set();
            DrainCreate(host);

            Assert.Equal([expectedPath], creator.Landed);
            Assert.Single(host.ShellEvents, IsNoteCreated);
            Assert.Equal(Two, host.Workspace.ActiveGroup.ActiveTab!.Path);
            Assert.Equal(Two, leaf.Root);
        });
    }

    [Fact]
    public void AGhostsCreateParkedAcrossAnExcursionAndBackOpensNothingTheEpochMoved()
    {
        using GraphVault vault = GraphVault.Copy("ghost-epoch");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            using var gate = new ManualResetEventSlim(false);
            using var reached = new ManualResetEventSlim(false);
            var creator = new RecordingCreator(host.Session, () =>
            {
                reached.Set();
                gate.Wait(TimeSpan.FromSeconds(10));
            });
            host.Workspace.GraphNoteCreator = creator;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            (GraphConnectionRow ghost, string expectedPath) = FirstGhost(leaf);
            int epoch = leaf.RootEpoch;
            host.Clear();

            leaf.Activate(ghost, newTab: false);
            Assert.True(reached.Wait(TimeSpan.FromSeconds(10)), "the create never reached the gate");
            // A → B → A: the root is restored, the epoch is not.
            host.OpenNote(Two);
            host.OpenNote(Hub);
            host.Settle();
            Assert.Equal(epoch + 2, leaf.RootEpoch);
            gate.Set();
            DrainCreate(host);

            Assert.Equal([expectedPath], creator.Landed);
            Assert.Single(host.ShellEvents, IsNoteCreated);
            Assert.Equal(Hub, host.Workspace.ActiveGroup.ActiveTab!.Path);
            Assert.DoesNotContain(host.Workspace.ActiveGroup.Tabs, tab => tab.Path == expectedPath);
        });
    }

    [Fact]
    public void AGhostsCreateParkedWhileTheLeafWentInactiveStillOpensAndLoadsSilently()
    {
        using GraphVault vault = GraphVault.Copy("ghost-inactive");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            using var gate = new ManualResetEventSlim(false);
            using var reached = new ManualResetEventSlim(false);
            var creator = new RecordingCreator(host.Session, () =>
            {
                reached.Set();
                gate.Wait(TimeSpan.FromSeconds(10));
            });
            host.Workspace.GraphNoteCreator = creator;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            (GraphConnectionRow ghost, string expectedPath) = FirstGhost(leaf);
            host.Clear();

            leaf.Activate(ghost, newTab: false);
            Assert.True(reached.Wait(TimeSpan.FromSeconds(10)), "the create never reached the gate");
            host.Workspace.ActiveLeaf = Host.OutlineLeaf;
            int loads = host.Loads;
            gate.Set();
            DrainCreate(host);

            // B-D10: the leaf's ACTIVE state is not consulted — the open
            // runs; the root change then loads NOTHING audibly (inactive),
            // and the presentation is STALE until the next mount or switch.
            Assert.Equal(expectedPath, host.Workspace.ActiveGroup.ActiveTab!.Path);
            Assert.Equal(expectedPath, leaf.Root);
            Assert.Equal(loads, host.Loads);
            Assert.Single(host.ShellEvents, IsNoteCreated);
            Assert.Empty(host.RelayLines);
            Assert.True(leaf.IsStale);
        });
    }
}
