// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7 (#1252), codex's final merge-delta check (on 66193dfd), on the
/// production-scheduled fixture of follow-up B's pumped saves: a real vault
/// opened through the vault lifecycle, its file events arriving on the
/// dispatcher as the production listener delivers them, every save writing
/// off the dispatcher. (1) A Created event never re-seats a tab under a save
/// admitted for its file (contract 38 D-10, R-9). (2) A rescan's same-item
/// reload keeps a faulted save's gates closed (D-10). (3) Close Vault waits
/// for a cache rerun a rescan's invalidation started (R-9's fixed point).
/// </summary>
public sealed class FinalMergeDeltaTests
{
    /// <summary>Finding 1 (critical): a missing note's first save is a create,
    /// off the dispatcher; core queues its Created event before the save
    /// publishes, and the reader takes the edit back before that event is
    /// applied. The event's re-seat keeps the buffer, its undo history and
    /// its document, and clears the missing state without retiring the save:
    /// the save publishes to the tab — "Saved" — and the undo stays an
    /// unsaved change behind the created bytes.</summary>
    [Fact]
    public void ACreatedEventNeverReseatsATabUnderItsAdmittedCreate()
    {
        using var host = new PumpedSaveReentrancyTests.Host();
        WorkspaceTabViewModel ghost = OpenMissing(host, "ghost.md", WorkspaceOpenTarget.NewTab);
        ghost.EditorDocument!.Insert(0, "draft\n");
        Assert.True(ghost.IsDirty);
        int document = ghost.ItemIdentity;

        Task<bool> save = SaveTakingTheEditBack(host, ghost);

        Assert.True(save.Result, "the create failed");
        Assert.Equal(string.Empty, ghost.Text);
        Assert.True(ghost.IsDirty, "the undo lost its place behind the created bytes (D-10)");
        Assert.True(ghost.EditorDocument!.UndoStack.CanRedo, "the re-seat destroyed the undo history");
        Assert.False(ghost.IsMissingFromDisk);
        Assert.Equal(document, ghost.ItemIdentity);
        Assert.Equal("draft\n", host.Disk("ghost.md"));
        Assert.Single(host.Announced, item => item.Event is A11yEvent.NoteSaved);
        Assert.Equal("Saved ghost.md.", ghost.Status);
    }

    /// <summary>Finding 1's everyday arm: the reader saves a missing note
    /// back, taking nothing back. The create's Created event is applied while
    /// the tab is still dirty, under the admitted save, and the note is back
    /// on disk: after the save the tab is clean, not missing, the same
    /// document, and "Saved" is spoken once. It used to stay "missing from
    /// disk" after the save.</summary>
    [Fact]
    public void SavingAMissingNoteBringsItBackOnDisk()
    {
        using var host = new PumpedSaveReentrancyTests.Host();
        WorkspaceTabViewModel ghost = OpenMissing(host, "ghost.md", WorkspaceOpenTarget.NewTab);
        ghost.EditorDocument!.Insert(0, "draft\n");
        int document = ghost.ItemIdentity;

        Task<bool> save = ghost.SaveAsync(announce: true);
        Assert.True(PumpedDispatcher.PumpUntil(() => save.IsCompleted, TimeSpan.FromSeconds(20)), "the save's publication");
        host.Settle();

        Assert.True(save.Result, "the create failed");
        Assert.False(ghost.IsDirty);
        Assert.False(ghost.IsMissingFromDisk, "the saved note still reads missing from disk");
        Assert.Equal(document, ghost.ItemIdentity);
        Assert.Equal("draft\n", host.Disk("ghost.md"));
        Assert.Single(host.Announced, item => item.Event is A11yEvent.NoteSaved);
        Assert.Equal("Saved ghost.md.", ghost.Status);
    }

    /// <summary>Finding 1, a note shown twice — in another pane, or a
    /// duplicate in the same one: both tabs of the path are missing, the
    /// reader types and saves in one and takes the edit back in it (the peer
    /// mirrors both). The Created event re-seats neither: both keep the
    /// reader's text and their documents, and neither is missing.</summary>
    [Theory]
    [InlineData("split")]
    [InlineData("duplicate")]
    public void ACreatedEventNeverReseatsAPeerUnderTheAdmittedCreate(string peerKind)
    {
        using var host = new PumpedSaveReentrancyTests.Host();
        WorkspaceTabViewModel ghost = OpenMissing(host, "ghost.md", WorkspaceOpenTarget.NewTab);
        if (peerKind == "split")
        {
            host.Workspace.OpenPath("ghost.md", WorkspaceOpenTarget.SplitRight);
        }
        else
        {
            host.Workspace.DuplicateTabCommand.Execute(null);
        }

        WorkspaceTabViewModel peer = host.Workspace.ActiveGroup.ActiveTab!;
        Assert.NotSame(ghost, peer);
        Assert.Equal("ghost.md", peer.Path);
        Assert.True(peer.IsMissingFromDisk);
        ghost.EditorDocument!.Insert(0, "draft\n");
        Assert.Equal("draft\n", peer.Text);
        (int Ghost, int Peer) documents = (ghost.ItemIdentity, peer.ItemIdentity);

        Task<bool> save = SaveTakingTheEditBack(host, ghost);

        Assert.True(save.Result, "the create failed");
        Assert.Equal(string.Empty, ghost.Text);
        Assert.Equal(string.Empty, peer.Text);
        Assert.True(ghost.IsDirty, "the undo lost its place behind the created bytes (D-10)");
        Assert.True(peer.IsDirty, "the peer lost the unsaved undo");
        Assert.False(ghost.IsMissingFromDisk);
        Assert.False(peer.IsMissingFromDisk);
        Assert.Equal(documents, (ghost.ItemIdentity, peer.ItemIdentity));
        Assert.Equal("draft\n", host.Disk("ghost.md"));
    }

    /// <summary>Finding 2: a save that faulted after its write was adopted
    /// leaves the tab clean but not saved in D-10's sense. The note changed
    /// outside Slate and a Refresh reloads the clean tab in place — the same
    /// item — and every gate still refuses: the fault is the item's.</summary>
    [Theory]
    [InlineData("close-tab")]
    [InlineData("close-pane")]
    [InlineData("replace")]
    [InlineData("teardown")]
    public void ARescansReloadKeepsAFaultedSavesGatesClosed(string site)
    {
        using var host = new PumpedSaveReentrancyTests.Host(VaultCloseDecision.Discard);
        FaultAfterAdoption(host);
        int document = host.S.ItemIdentity;

        File.WriteAllText(Path.Combine(host.Root, "note1.md"), "# Note 1\n\nChanged outside Slate.\n");
        Task run = host.Lifecycle.RescanAsync(RescanReason.Explicit);
        Assert.True(PumpedDispatcher.PumpUntil(() => run.IsCompleted, TimeSpan.FromSeconds(60)), "the rescan");
        host.Settle();
        Assert.Contains("Changed outside Slate.", host.S.Text, StringComparison.Ordinal);
        Assert.NotEqual(document, host.S.ItemIdentity);
        Assert.True(host.S.LastSaveFaulted, "the rescan's reload cleared the faulted save");
        host.Announced.Clear();

        RunGate(host, site);
        host.Settle();

        Assert.DoesNotContain(
            host.Announced,
            item => item.Event is A11yEvent.TabClosed
                or A11yEvent.VaultClosed
                or A11yEvent.VaultClosedAllSaved
                or A11yEvent.VaultClosedChangesDiscarded);
        switch (site)
        {
            case "close-tab": Assert.False(host.S.IsDisposed, "the tab closed over a faulted save"); break;
            case "close-pane": Assert.Contains(host.G1, host.Workspace.Groups); break;
            case "replace": Assert.Equal("note1.md", host.S.Path); break;
            case "teardown":
                Assert.NotNull(host.Lifecycle.Workspace);
                Assert.Equal(
                    "Vault remains open because one or more notes could not be saved.",
                    host.Lifecycle.StatusText);
                break;
        }
    }

    /// <summary>Finding 2's other side (D-10): the fault belongs to its item,
    /// so an ACTUAL item change still leaves it behind — the reader edits the
    /// note after the fault, opens another note into its tab and discards;
    /// the tab shows that note, clean and not faulted, and closes.</summary>
    [Fact]
    public void AnItemChangeStillLeavesAFaultedSaveBehind()
    {
        using var host = new PumpedSaveReentrancyTests.Host(VaultCloseDecision.Discard);
        FaultAfterAdoption(host);

        host.Type(host.S, "Marker-S2");
        host.TabPrompt = _ => WorkspaceDirtyNavigationDecision.Discard;
        host.Workspace.OpenPath("note3.md");
        host.Settle();
        Assert.Equal("note3.md", host.S.Path);
        Assert.False(host.S.IsDirty);
        Assert.False(host.S.LastSaveFaulted, "the fault followed the tab to another note");

        host.Workspace.CloseTabCommand.Execute(host.S);
        host.Settle();
        Assert.True(host.S.IsDisposed, "a clean note with no faulted save stayed open");
    }

    /// <summary>Finding 3: a tab's link-and-task cache load parked, a rescan
    /// invalidates the tab (the load owes a rerun), the load ends and its
    /// publication starts the rerun, which parks too. Close Vault waits for
    /// that rerun before the session goes — the fixed point includes a
    /// successor a worker's publication started.</summary>
    [Fact]
    public void TheCloseWaitsForARerunARescansInvalidationStarted()
    {
        using var host = new PumpedSaveReentrancyTests.Host();
        using var loads = new ParkedLoads(host);
        EditorInteractionCoordinator coordinator = loads.OpenParked("note3.md");
        int generation = coordinator.ArtifactCacheGenerationForTests;

        Task run = host.Lifecycle.RescanAsync(RescanReason.Explicit);
        Assert.True(PumpedDispatcher.PumpUntil(() => run.IsCompleted, TimeSpan.FromSeconds(60)), "the rescan");
        // The rescan's invalidation reached the load in flight — the one
        // coordinator, its first load still unpublished — so the load's
        // publication owes the rerun.
        Assert.True(coordinator.ArtifactCacheLoadingForTests, "premise: the first load is still in flight");
        Assert.True(
            coordinator.ArtifactCacheGenerationForTests > generation,
            "premise: the rescan's invalidation did not reach the load in flight");
        loads.ReleaseFirst.Set();
        Assert.True(PumpedDispatcher.PumpUntil(() => loads.Second.IsSet, TimeSpan.FromSeconds(10)), "the rerun parking");

        CloseWhileTheRerunIsParked(host, loads.ReleaseSecond);
    }

    /// <summary>Finding 3, the gap itself: the load's worker has ended but
    /// its publication — the one that starts the rerun the rescan's
    /// invalidation asked for — is still queued when the run completes and
    /// prunes the coordinators it touched (the dispatcher is busy with the
    /// run's tail). The coordinator is not idle, so it stays registered, and
    /// Close Vault waits for the rerun that publication starts.</summary>
    [Fact]
    public void ALoadWhosePublicationIsQueuedKeepsTheCloseWaiting()
    {
        using var host = new PumpedSaveReentrancyTests.Host();
        using var loads = new ParkedLoads(host);
        EditorInteractionCoordinator coordinator = loads.OpenParked("note3.md");

        // The run's last dependent: every publication the run awaits settles
        // first; then, with the dispatcher held in this turn, the load's
        // worker ends — its publication queues behind the turn, and the run
        // completes, pruning, inside it. Premises are read after the run (an
        // assertion thrown here would only count as a failed operation).
        var publications = new List<Task>();
        bool held = false;
        bool settled = false;
        bool inFlight = false;
        bool ended = false;
        bool queued = false;
        host.Workspace.RescanPublicationForTests = (kind, _, publication) =>
        {
            Task real = publication();
            publications.Add(real);
            if (kind != "graph" || held)
            {
                return real;
            }

            held = true;
            settled = PumpedDispatcher.PumpUntil(
                () => publications.All(task => task.IsCompleted),
                TimeSpan.FromSeconds(30));
            // Their awaiters' continuations too: nothing the run awaits after
            // this turn is left incomplete.
            PumpedDispatcher.Drain();
            inFlight = coordinator.ArtifactCacheLoadingForTests;
            loads.ReleaseFirst.Set();
            ended = SpinWait.SpinUntil(() => coordinator.LiveWorkersForTests == 0, TimeSpan.FromSeconds(10));
            queued = coordinator.ArtifactCacheLoadingForTests;
            return real;
        };

        Task run = host.Lifecycle.RescanAsync(RescanReason.Explicit);
        Assert.True(PumpedDispatcher.PumpUntil(() => run.IsCompleted, TimeSpan.FromSeconds(60)), "the rescan");
        host.Workspace.RescanPublicationForTests = null;
        Assert.True(held, "premise: the run's graph dependent never published");
        Assert.True(settled, "premise: the run's other publications never settled");
        Assert.True(inFlight, "premise: the first load was not in flight");
        Assert.True(ended, "premise: the load's worker never ended");
        Assert.True(queued, "premise: the load's publication ran inside the run's turn");
        Assert.True(PumpedDispatcher.PumpUntil(() => loads.Second.IsSet, TimeSpan.FromSeconds(10)), "the rerun parking");

        CloseWhileTheRerunIsParked(host, loads.ReleaseSecond);
    }

    /// <summary>A note that does not exist, opened (its load reads nothing,
    /// so it carries no content hash) and swept as missing — what a restore
    /// of a vanished note produces.</summary>
    private static WorkspaceTabViewModel OpenMissing(PumpedSaveReentrancyTests.Host host, string path, WorkspaceOpenTarget target)
    {
        host.Workspace.OpenPath(path, target);
        WorkspaceTabViewModel tab = host.Workspace.ActiveGroup.ActiveTab!;
        Assert.Equal(path, tab.Path);
        host.Workspace.InvalidatePath(path);
        Assert.True(tab.IsMissingFromDisk);
        PumpedDispatcher.Drain();
        host.Announced.Clear();
        return tab;
    }

    /// <summary>Save the tab — a create, off the dispatcher — parked on its
    /// worker before the write; take the edit back meanwhile (the tab is
    /// clean again: its baseline is still the empty note the create started
    /// from), then let the create land. Core queues the create's Created
    /// event from inside the write, so the event is applied — the tab clean,
    /// the save admitted — before the save publishes. Returns the settled
    /// save.</summary>
    private static Task<bool> SaveTakingTheEditBack(PumpedSaveReentrancyTests.Host host, WorkspaceTabViewModel tab)
    {
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        tab.SaveWriteHookForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
        };
        try
        {
            Task<bool> save = tab.SaveAsync(announce: true);
            Assert.True(PumpedDispatcher.PumpUntil(() => parked.IsSet, TimeSpan.FromSeconds(10)), "the create parking");
            tab.EditorDocument!.UndoStack.Undo();
            Assert.False(tab.IsDirty, "premise: the undo is back at the baseline the create started from");
            Assert.True(tab.HasPendingSaves, "premise: the create is admitted");
            release.Set();
            Assert.True(PumpedDispatcher.PumpUntil(() => save.IsCompleted, TimeSpan.FromSeconds(20)), "the save's publication");
            host.Settle();
            return save;
        }
        finally
        {
            release.Set();
            tab.SaveWriteHookForTests = null;
        }
    }

    /// <summary>The note under test (S) saved with a fault after its write
    /// was adopted: clean, and not saved in D-10's sense.</summary>
    private static void FaultAfterAdoption(PumpedSaveReentrancyTests.Host host)
    {
        host.Workspace.SaveActiveAndSettle();
        Assert.False(host.Workspace.HasDirtyTabs, "the arrangement left a dirty tab");
        host.G1.ActiveTab = host.S;
        host.Type(host.S, "Marker-S");
        host.S.SaveAdoptedHookForTests = () => throw new InjectedFault();
        host.Workspace.SaveActiveCommand.Execute(null);
        host.Settle();
        host.S.SaveAdoptedHookForTests = null;
        Assert.False(host.S.IsDirty);
        Assert.True(host.S.LastSaveFaulted, "premise: the save's publication did not fault after adoption");
    }

    /// <summary>The gates, on the note under test (S) in the first pane.</summary>
    private static void RunGate(PumpedSaveReentrancyTests.Host host, string site)
    {
        switch (site)
        {
            case "close-tab": host.Workspace.CloseTabCommand.Execute(host.S); break;
            case "close-pane": host.Workspace.ClosePaneCommand.Execute(null); break;
            case "replace": host.Workspace.OpenPath("note3.md"); break;
            case "teardown": host.Lifecycle.CloseVault(); break;
        }
    }

    /// <summary>Close Vault while the rerun is parked, releasing it 800 ms
    /// later from another thread: the close must not return before then —
    /// the rerun's core calls run before the session is disposed.</summary>
    private static void CloseWhileTheRerunIsParked(PumpedSaveReentrancyTests.Host host, ManualResetEventSlim rerun)
    {
        string before = CloseState(host);
        var clock = Stopwatch.StartNew();
        long releasedAt = 0;
        _ = Task.Run(async () =>
        {
            await Task.Delay(800);
            Volatile.Write(ref releasedAt, clock.ElapsedMilliseconds);
            try
            {
                rerun.Set();
            }
            catch (ObjectDisposedException)
            {
                // The fact already failed and tore its fixture down.
            }
        });
        host.Lifecycle.CloseVault();
        long closedAt = clock.ElapsedMilliseconds;

        long released = Volatile.Read(ref releasedAt);
        string diagnostics = $"CloseVault returned at {closedAt} ms; rerun released at {released} ms. "
            + $"Before: {before}. After: {CloseState(host)}.";
        Assert.True(host.Lifecycle.Workspace is null, $"The workspace remained open. {diagnostics}");
        Assert.True(
            released > 0 && closedAt >= released,
            $"The close returned before the rerun was released. {diagnostics}");
    }

    private static string CloseState(PumpedSaveReentrancyTests.Host host)
    {
        FilesSidebarViewModel? sidebar = host.Lifecycle.FileSidebar;
        return $"busy={host.Lifecycle.IsBusy}, rescanActive={host.Lifecycle.IsRescanActive}, "
            + $"status={host.Lifecycle.StatusText}, treeRefreshCompleted={sidebar?.TreeRefreshCompletion.IsCompleted}, "
            + $"loadingChildren={sidebar?.IsLoadingChildren}, expandingLoaded={sidebar?.IsExpandingLoaded}, "
            + $"importing={sidebar?.IsImporting}, trashing={sidebar?.IsTrashing}, filtering={sidebar?.IsFiltering}, "
            + $"dirtyTabs={host.Workspace.HasDirtyTabs}, faultedCleanSave={host.Workspace.HasCleanTabWithAFaultedSave}, "
            + $"savesIdle={host.Workspace.SavesIdle}, saveWorkers={host.Workspace.SavesForTests.LiveWorkersForTests}, "
            + $"savesClosed={host.Workspace.SavesForTests.IsClosed}";
    }

    /// <summary>The link-and-task cache loads of the tabs the workspace
    /// builds from now on, parked in the workers' fault seam: the first on
    /// <see cref="First"/> until <see cref="ReleaseFirst"/>, the second on
    /// <see cref="Second"/> until <see cref="ReleaseSecond"/>.</summary>
    private sealed class ParkedLoads : IDisposable
    {
        private readonly PumpedSaveReentrancyTests.Host _host;
        private int _loads;

        internal ParkedLoads(PumpedSaveReentrancyTests.Host host)
        {
            _host = host;
            host.Workspace.SaveActiveAndSettle();
            Assert.False(host.Workspace.HasDirtyTabs, "the arrangement left a dirty tab");
            host.Workspace.InteractionBackgroundFaultForTests = kind =>
            {
                if (kind == EditorInteractionWorkerKind.Artifact)
                {
                    switch (Interlocked.Increment(ref _loads))
                    {
                        case 1:
                            First.Set();
                            _ = ReleaseFirst.Wait(TimeSpan.FromSeconds(30));
                            break;
                        case 2:
                            Second.Set();
                            _ = ReleaseSecond.Wait(TimeSpan.FromSeconds(30));
                            break;
                    }
                }

                return null;
            };
        }

        internal ManualResetEventSlim First { get; } = new(false);
        internal ManualResetEventSlim ReleaseFirst { get; } = new(false);
        internal ManualResetEventSlim Second { get; } = new(false);
        internal ManualResetEventSlim ReleaseSecond { get; } = new(false);

        /// <summary>Open <paramref name="path"/> in a new tab and wait for its
        /// first cache load to park; the tab's coordinator.</summary>
        internal EditorInteractionCoordinator OpenParked(string path)
        {
            _host.Workspace.OpenPath(path, WorkspaceOpenTarget.NewTab);
            WorkspaceTabViewModel tab = _host.Workspace.ActiveGroup.ActiveTab!;
            Assert.Equal(path, tab.Path);
            Assert.True(PumpedDispatcher.PumpUntil(() => First.IsSet, TimeSpan.FromSeconds(10)), "the cache load parking");
            return tab.EditorInteractions!;
        }

        public void Dispose()
        {
            // Release first: a worker still parked wakes before its event goes.
            ReleaseFirst.Set();
            ReleaseSecond.Set();
            _host.Workspace.InteractionBackgroundFaultForTests = null;
            First.Dispose();
            ReleaseFirst.Dispose();
            Second.Dispose();
            ReleaseSecond.Dispose();
        }
    }

    private sealed class InjectedFault() : Exception("injected fault after adoption");
}
