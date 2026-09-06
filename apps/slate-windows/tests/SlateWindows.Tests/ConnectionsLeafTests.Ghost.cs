// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
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

    /// <summary>Codex post-implementation pass 2 (IPB-6; B-6, B-9, B-11): a
    /// row acts only while the tree that rendered it is the one the document
    /// holds — a ghost row and a note row of Hub's tree, neither listed by
    /// Two's, are inert once the root moved to Two (no create, no open, no
    /// line), while the rows of an earlier tree of the SAME root are the
    /// same rows after a refresh (the occurrence id survives, B-6).</summary>
    [Fact]
    public void ARowOfATreeTheDocumentNoLongerHoldsIsInertAndARefreshedRootsRowIsNot()
    {
        using GraphVault vault = GraphVault.Copy("stale-row");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            var creator = new RecordingCreator(host.Session);
            host.Workspace.GraphNoteCreator = creator;
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            GraphConnectionsTree twos = host.Session.GraphConnectionsTree(Two, 1, leaf.Filter);
            HashSet<string> twosIds = [.. twos.Incoming.Concat(twos.Outgoing).Select(row => row.Id)];
            GraphConnectionRow[] hubs = [.. leaf.Publication.Tree!.Incoming, .. leaf.Publication.Tree!.Outgoing];
            GraphConnectionRow hubGhost = hubs.First(row => row.Kind == GraphNodeKind.Ghost && !twosIds.Contains(row.Id));
            GraphConnectionRow hubNote = hubs.First(row => row.Kind == GraphNodeKind.Note && row.Path is not null && row.Path != Two && !twosIds.Contains(row.Id));

            // The same root refreshed at another depth: the same rows.
            host.Workspace.ConnectionsDeeperCommand.Execute(null);
            host.Settle();
            Assert.True(leaf.IsRowCurrent(hubGhost));
            Assert.True(leaf.IsRowCurrent(hubNote));

            host.OpenNote(Two);
            host.Settle();
            host.Clear();
            Assert.False(leaf.IsRowCurrent(hubGhost));
            Assert.False(leaf.IsRowCurrent(hubNote));
            leaf.Activate(hubGhost, newTab: false);
            DrainCreate(host);
            leaf.Activate(hubNote, newTab: false);
            host.Settle();

            Assert.Empty(creator.Created);
            Assert.Equal(Two, host.Workspace.ActiveGroup.ActiveTab!.Path);
            Assert.Equal(Two, leaf.Root);
            Assert.Empty(host.Timeline);
        });
    }

    /// <summary>Codex post-implementation pass 4 (IPB-19; B-9, Term 1): the
    /// leaf's Reveal in file tree reaches the sidebar's select-path seam
    /// WITHOUT a graph tab — the graph tab's addressed reveal returned early
    /// without one, and the standalone leaf's enabled action did nothing.</summary>
    [Fact]
    public void RevealFromTheLeafReachesTheSidebarWithNoGraphTab()
    {
        using GraphVault vault = GraphVault.Copy("reveal-no-graph");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            ConnectionsLeafViewModel leaf = host.Leaf;
            var revealed = new List<string>();
            host.Workspace.GraphRevealInSidebar = path => revealed.Add(path);
            host.ActivateLeaf();
            host.OpenNote(Hub);
            host.Settle();
            Assert.Null(host.Workspace.GraphDocument);
            GraphConnectionRow note = leaf.Publication.Tree!.Outgoing.First(row => row.Kind == GraphNodeKind.Note && row.Path is not null);
            Assert.True(leaf.IsActionEnabled(GraphRowAction.Reveal, note));

            leaf.Execute(GraphRowAction.Reveal, note);

            Assert.Equal([note.Path], revealed);
            Assert.Null(host.Workspace.GraphDocument);
            Assert.Equal(Hub, host.Workspace.ActiveGroup.ActiveTab!.Path);
        });
    }

    /// <summary>Codex post-implementation pass 4 (IPB-20; A-2, B-1, B-11): the
    /// create worker names its owner DISPATCHER beside its context, so a
    /// completion enqueued while the dispatcher is busy and then ABORTED by
    /// its shutdown withdraws its promise — the create's drain completes,
    /// nothing applies (the generic scheduler's own fact, on this worker).</summary>
    [Fact]
    public async Task ACreateCompletionAbortedByTheDispatchersShutdownWithdrawsItsPromise()
    {
        GraphNoteCreationWorker? worker = null;
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim(false);
        using var insideOperation = new ManualResetEventSlim(false);
        using var releaseOperation = new ManualResetEventSlim(false);
        using var queued = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            worker = new GraphNoteCreationWorker();
            ready.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(10)), "the dispatcher thread never started");
        Assert.NotNull(worker);
        Assert.NotNull(dispatcher);
        worker.ApplyQueuedForTests = queued.Set;

        _ = dispatcher.BeginInvoke(
            DispatcherPriority.Normal,
            new Action(() =>
            {
                insideOperation.Set();
                _ = releaseOperation.Wait(TimeSpan.FromSeconds(10));
                dispatcher.InvokeShutdown();
            }));
        Assert.True(insideOperation.Wait(TimeSpan.FromSeconds(10)), "the dispatcher never entered the blocking operation");

        int completions = 0;
        worker.Run(
            () => new FileManagement.NoteCreateResult.Failed("never applied"),
            _ => Interlocked.Increment(ref completions));
        Assert.True(queued.Wait(TimeSpan.FromSeconds(10)), "the completion was never enqueued on the busy dispatcher");
        releaseOperation.Set();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "the dispatcher never shut down");
        Assert.True(dispatcher.HasShutdownFinished);

        Task drain = worker.WhenAllWorkDrained();
        Assert.True(await Task.WhenAny(drain, Task.Delay(TimeSpan.FromSeconds(10))) == drain, "the create's drain waited on an aborted operation");
        await drain;
        Assert.Equal(0, Volatile.Read(ref completions));
    }

    /// <summary>Codex post-implementation pass 4 (IPB-27; A-10 as amended,
    /// B-19 i): the runtime witness beside the source census — the leaf and
    /// the graph document speak through the SAME relay instance, the
    /// workspace's one.</summary>
    [Fact]
    public void TheLeafAndTheGraphDocumentShareTheWorkspacesOneRelayAtRuntime()
    {
        using GraphVault vault = GraphVault.Copy("one-relay");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphAnnouncer relay = host.Workspace.GraphRelayForTests;
            Assert.Same(relay, host.Leaf.AnnouncerForTests);
            host.Workspace.OpenGraph();
            host.Settle();
            Assert.Same(relay, host.Workspace.GraphDocument!.AnnouncerForTests);
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
