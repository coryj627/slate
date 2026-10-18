// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Security.AccessControl;
using SlateWindows.Tests.Support;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 43 PR 20b (#1267; design
/// <c>docs/plans/43_mac_backlog_delivery/20_vault_watcher_design.md</c>
/// §4.7): the Windows host of core's vault watcher. A burst of changes made
/// outside Slate reaches the open vault's listener as one
/// <c>OnExternalChange</c>, and the lifecycle answers it with a Watcher
/// rescan — the run Refresh makes, on the foreground rescan's sentence
/// policy, behind the watcher's own sentence gate (at most one sentence a
/// minute, none under a modal surface).
/// </summary>
/// <remarks>
/// Most facts make the watcher's call themselves, through the real listener
/// and from a thread of its own (<see cref="Harness.SignalExternalChange"/>),
/// so its timing is theirs. Three lift the test switch
/// (<see cref="VaultWatcherSwitch"/>) and run the real watcher end to end:
/// ReadDirectoryChangesW, core's coalescer and its "slate-watch" thread. The
/// Watcher reason renders the shared "Files refreshed. …" sentence (owner
/// decision Q4), so <see cref="Explicit1"/> is its one-file text too.
/// </remarks>
public sealed partial class RescanTests
{
    // ---------------------------------------------------------------------
    // The watcher's run and its sentence
    // ---------------------------------------------------------------------

    /// <summary>The real watcher, end to end on ReadDirectoryChangesW: the
    /// open armed the watch (no "unavailable" diagnostic), a file made outside
    /// Slate reached core's coalescer, the listener was called once, and the
    /// lifecycle's Watcher run put the file in the tree and Quick Open — with
    /// no Refresh — and said so in the one shared sentence.</summary>
    [Fact]
    public void AWatcherDrivenChangeReachesTheTreeWithoutARefresh() => RunSta(() =>
    {
        using var log = new HostLogLines();
        using IDisposable watcher = VaultWatcherSwitch.Lift();
        using var h = new Harness("watcher-live", ("alpha.md", "# Alpha\n"));
        Assert.DoesNotContain(log.Lines, line => line.StartsWith("SlateWindows.VaultWatchUnavailable", StringComparison.Ordinal));
        int scans = h.ScanCalls;

        h.Write("late.md", "# Late\n");
        h.Context.RunUntil(
            () => h.Sidebar.RootNodes.Any(node => node.Path == "late.md") && !h.Lifecycle.IsRescanActive,
            "the watcher's run listing late.md");
        h.Context.Await(h.Lifecycle.RescanCompletion);

        A11yEvent.VaultRescanFinished finished = OneRescanSentence(h);
        Assert.Equal((RescanReason.Watcher, 1UL, 0UL), (finished.Reason, finished.Changed, finished.Removed));
        Assert.Equal([Explicit1], h.Spoken);
        Assert.Contains("late.md", h.QuickOpen.FilePathsForTests);
        Assert.True(h.ScanCalls > scans, "no rescan ran");
    });

    /// <summary>§4.5: a Watcher run that found nothing posts nothing — the
    /// whole sequence is empty and the status line is unchanged — exactly
    /// like an unchanged foreground rescan.</summary>
    [Fact]
    public void AWatcherRunIsSilentWhenNothingChanged() => RunSta(() =>
    {
        using var h = new Harness("watcher-quiet", ("alpha.md", "# Alpha\n"));
        string status = h.Lifecycle.StatusText;
        int scans = h.ScanCalls;

        SignalAndAwaitTheWatcherRun(h);

        Assert.Equal(scans + 1, h.ScanCalls);
        Assert.Empty(h.Events);
        Assert.Equal(status, h.Lifecycle.StatusText);
    });

    /// <summary>§4.5: a Watcher run that found changes says so in ONE
    /// sentence, with its own counts and the Watcher reason, on the status
    /// line too; the tree and Quick Open show the new file and drop the
    /// deleted one.</summary>
    [Fact]
    public void AWatcherRunSpeaksWhenSomethingChanged() => RunSta(() =>
    {
        using var h = new Harness("watcher-speaks", ("alpha.md", "# Alpha\n"), ("gone.md", "# Gone\n"));
        h.Write("late.md", "# Late\n");
        h.Delete("gone.md");

        SignalAndAwaitTheWatcherRun(h);

        A11yEvent.VaultRescanFinished finished = OneRescanSentence(h);
        Assert.Equal((RescanReason.Watcher, 1UL, 1UL), (finished.Reason, finished.Changed, finished.Removed));
        Assert.Equal(["Files refreshed. 1 new or changed, 1 removed."], h.Spoken);
        Assert.Equal(h.Spoken[0], h.Lifecycle.StatusText);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "late.md");
        Assert.DoesNotContain(h.Sidebar.RootNodes, node => node.Path == "gone.md");
        Assert.Contains("late.md", h.QuickOpen.FilePathsForTests);
        Assert.DoesNotContain("gone.md", h.QuickOpen.FilePathsForTests);
    });

    /// <summary>§4.5: a partial Watcher run speaks the incomplete sentence,
    /// never "No changes" and never silence — and the sentence window covers
    /// it too: the next incomplete Watcher run inside the window posts
    /// nothing, so a file that keeps failing during a sync is not repeated
    /// every few seconds.</summary>
    [Fact]
    public void AnIncompleteWatcherRunSpeaksTheIncompleteSentence() => RunSta(() =>
    {
        using var h = new Harness("watcher-incomplete", ("alpha.md", "# Alpha\n"));
        h.Write("locked.md", "# Locked\n");
        using var deny = DenyAccess.To(Path.Combine(h.Root, "locked.md"), FileSystemRights.ReadData);

        SignalAndAwaitTheWatcherRun(h);
        Assert.Equal(1UL, Assert.IsType<A11yEvent.VaultRescanIncomplete>(Assert.Single(h.Events)).Errors);
        Assert.Equal([OneError], h.Spoken);

        h.Events.Clear();
        h.Now += TimeSpan.FromSeconds(10);
        SignalAndAwaitTheWatcherRun(h);
        Assert.Empty(h.Events);
    });

    /// <summary>§4.5 item 2 (owner decision Q2): at most one watcher sentence
    /// per <c>watcher_sentence_spacing_ms()</c> — core's one number, 60 s,
    /// read through the FFI. A Watcher run inside the window reconciles and
    /// posts nothing, the status line included; the first one past it speaks
    /// its own run's counts, never the silent run's carried over.</summary>
    [Fact]
    public void ASecondWatcherSentenceInsideTheWindowIsSilent() => RunSta(() =>
    {
        Assert.Equal(60_000UL, SlateUniffiMethods.WatcherSentenceSpacingMs());
        Assert.Equal(TimeSpan.FromMinutes(1), VaultLifecycleViewModel.WatcherSentenceSpacing);
        using var h = new Harness("watcher-window", ("alpha.md", "# Alpha\n"));

        h.Write("one.md", "# One\n");
        SignalAndAwaitTheWatcherRun(h);
        Assert.Equal([Explicit1], h.Spoken);
        string status = h.Lifecycle.StatusText;

        h.Events.Clear();
        h.Now += VaultLifecycleViewModel.WatcherSentenceSpacing - TimeSpan.FromSeconds(1);
        h.Write("two.md", "# Two\n");
        h.Write("three.md", "# Three\n");
        SignalAndAwaitTheWatcherRun(h);
        Assert.Empty(h.Events);
        Assert.Equal(status, h.Lifecycle.StatusText);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "two.md");
        Assert.Contains("three.md", h.QuickOpen.FilePathsForTests);

        h.Now += TimeSpan.FromSeconds(1);
        h.Write("four.md", "# Four\n");
        SignalAndAwaitTheWatcherRun(h);
        A11yEvent.VaultRescanFinished finished = OneRescanSentence(h);
        Assert.Equal((RescanReason.Watcher, 1UL, 0UL), (finished.Reason, finished.Changed, finished.Removed));
        Assert.Equal([Explicit1], h.Spoken);
    });

    /// <summary>§4.5 item 3: no watcher sentence while a modal surface is open
    /// (the foreground route's predicate) — the run still reconciles — and a
    /// sentence held back that way starts no window: once the surface closes,
    /// the next Watcher run speaks at once, with its own counts.</summary>
    [Fact]
    public void AWatcherSentenceIsNotPostedWhileAModalSurfaceIsOpen() => RunSta(() =>
    {
        using var h = new Harness("watcher-modal", ("alpha.md", "# Alpha\n"));
        h.ModalOpen = true;
        h.Write("one.md", "# One\n");

        SignalAndAwaitTheWatcherRun(h);
        Assert.Empty(h.Events);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "one.md");

        h.ModalOpen = false;
        h.Write("two.md", "# Two\n");
        SignalAndAwaitTheWatcherRun(h);
        Assert.Equal(RescanReason.Watcher, OneRescanSentence(h).Reason);
        Assert.Equal([Explicit1], h.Spoken);
    });

    // ---------------------------------------------------------------------
    // The lattice, and a request that is never dropped
    // ---------------------------------------------------------------------

    /// <summary>§4.2 item 2: the pending-reason lattice's join is Explicit &gt;
    /// Foreground &gt; Watcher, over every pending reason and every arrival;
    /// and through the coalescer, requests joined to a running rescan make
    /// ONE follow-up of the joined reason — Watcher ⊔ Watcher a Watcher run,
    /// Foreground ⊔ Watcher a Foreground run (it speaks where a Watcher run
    /// inside the window would not), Explicit ⊔ Watcher an Explicit run
    /// ("No changes" is still said).</summary>
    [Fact]
    public void WatcherJoinsTheLattice() => RunSta(() =>
    {
        (RescanReason? Pending, RescanReason Arriving, RescanReason Joined)[] joins =
        [
            (null, RescanReason.Explicit, RescanReason.Explicit),
            (null, RescanReason.Foreground, RescanReason.Foreground),
            (null, RescanReason.Watcher, RescanReason.Watcher),
            (RescanReason.Explicit, RescanReason.Explicit, RescanReason.Explicit),
            (RescanReason.Explicit, RescanReason.Foreground, RescanReason.Explicit),
            (RescanReason.Explicit, RescanReason.Watcher, RescanReason.Explicit),
            (RescanReason.Foreground, RescanReason.Explicit, RescanReason.Explicit),
            (RescanReason.Foreground, RescanReason.Foreground, RescanReason.Foreground),
            (RescanReason.Foreground, RescanReason.Watcher, RescanReason.Foreground),
            (RescanReason.Watcher, RescanReason.Explicit, RescanReason.Explicit),
            (RescanReason.Watcher, RescanReason.Foreground, RescanReason.Foreground),
            (RescanReason.Watcher, RescanReason.Watcher, RescanReason.Watcher),
        ];
        foreach ((RescanReason? pending, RescanReason arriving, RescanReason joined) in joins)
        {
            Assert.True(
                VaultLifecycleViewModel.JoinRescanReasons(pending, arriving) == joined,
                $"{pending?.ToString() ?? "nothing"} joined by {arriving} must be {joined}, "
                + $"not {VaultLifecycleViewModel.JoinRescanReasons(pending, arriving)}");
        }

        // Watcher ⊔ Watcher: two signals during a parked foreground run make
        // one Watcher follow-up.
        using (var h = new Harness("lattice-watcher-twice", ("alpha.md", "# Alpha\n")))
        {
            int scans = h.ScanCalls;
            h.ParkAfterCall = scans + 1;
            Task run = h.Lifecycle.RescanAsync(RescanReason.Foreground);
            h.Context.RunUntil(() => h.Parked.IsSet, "the foreground scan parking");
            h.Write("late.md", "# Late\n");
            h.SignalExternalChange();
            h.SignalExternalChange();
            h.Context.Drain();
            h.Release.Set();
            h.Context.Await(run);

            Assert.Equal(scans + 2, h.ScanCalls);
            Assert.Equal(RescanReason.Watcher, OneRescanSentence(h).Reason);
            Assert.Equal([Explicit1], h.Spoken);
        }

        // Foreground ⊔ Watcher: with a watcher sentence just spoken, a Watcher
        // follow-up would be silent; the joined one speaks, so it is
        // Foreground.
        using (var h = new Harness("lattice-foreground-over-watcher", ("alpha.md", "# Alpha\n")))
        {
            h.Write("first.md", "# First\n");
            SignalAndAwaitTheWatcherRun(h);
            h.Events.Clear();
            int scans = h.ScanCalls;
            h.ParkAfterCall = scans + 1;
            h.SignalExternalChange();
            h.Context.RunUntil(() => h.Parked.IsSet, "the watcher scan parking");
            h.Write("late.md", "# Late\n");
            h.SignalExternalChange();
            h.Context.Drain();
            _ = h.Lifecycle.RescanAsync(RescanReason.Foreground);
            h.Release.Set();
            h.Context.RunUntil(() => h.ScanCalls == scans + 2 && !h.Lifecycle.IsRescanActive, "the follow-up");
            h.Context.Await(h.Lifecycle.RescanCompletion);

            Assert.Equal(RescanReason.Foreground, OneRescanSentence(h).Reason);
            Assert.Equal([Explicit1], h.Spoken);
        }

        // Explicit ⊔ Watcher: a Refresh during a parked Watcher run makes an
        // Explicit follow-up, which always speaks.
        using (var h = new Harness("lattice-explicit-over-watcher", ("alpha.md", "# Alpha\n")))
        {
            int scans = h.ScanCalls;
            h.ParkAfterCall = scans + 1;
            h.SignalExternalChange();
            h.Context.RunUntil(() => h.Parked.IsSet, "the watcher scan parking");
            h.SignalExternalChange();
            h.Context.Drain();
            _ = h.Lifecycle.RescanAsync(RescanReason.Explicit);
            h.Release.Set();
            h.Context.RunUntil(() => h.ScanCalls == scans + 2 && !h.Lifecycle.IsRescanActive, "the follow-up");
            h.Context.Await(h.Lifecycle.RescanCompletion);

            Assert.Equal(RescanReason.Explicit, OneRescanSentence(h).Reason);
            Assert.Equal([NoChanges], h.Spoken);
        }
    });

    /// <summary>§4.2 item 3: a watcher request made while the initial open
    /// scan, an import or a trash operation runs is recorded, not refused,
    /// and runs once they settle. The open's request is served by the end of
    /// the open — the open parked before its walk here, after it in
    /// <see cref="AWatcherRequestBeforeTheWorkspaceRunsAfterTheOpen"/> — and
    /// the import's and the trash's, as a Watcher run, when they end.</summary>
    [Fact]
    public void AWatcherRequestWhileBlockedRunsWhenTheBlockersSettle() => RunSta(() =>
    {
        int scansWhileOpening = -1;
        bool busyWhileOpening = false;
        using (var h = Harness.WhileOpeningBeforeItsScan(
            "watcher-open-scan",
            opening =>
            {
                opening.SignalExternalChange();
                opening.Context.Drain();
                scansWhileOpening = opening.ScanCalls;
                busyWhileOpening = opening.Lifecycle.IsBusy;
                opening.ParkAfterCall = 2;
            },
            ("alpha.md", "# Alpha\n")))
        {
            h.Context.RunUntil(() => h.Parked.IsSet, "the recorded watcher run's scan");
            Assert.Equal(1, scansWhileOpening);
            Assert.True(busyWhileOpening, "the open was not busy before its scan");
            Assert.Equal(2, h.ScanCalls);
            h.Release.Set();
            h.Context.RunUntil(() => !h.Lifecycle.IsRescanActive, "the recorded watcher run");
            h.Context.Await(h.Lifecycle.RescanCompletion);
            Assert.Empty(h.Events);
        }

        var picker = new TaskCompletionSource<IReadOnlyList<string>>();
        using (var h = new Harness("watcher-import", pickImportSources: () => picker.Task, files: [("alpha.md", "# Alpha\n")]))
        {
            int scans = h.ScanCalls;
            h.Sidebar.ImportCommand.Execute(null);
            Assert.True(h.Sidebar.IsImporting);
            h.Write("late.md", "# Late\n");
            h.SignalExternalChange();
            h.Settle();
            Assert.Equal(scans, h.ScanCalls);

            picker.SetResult([]);
            h.Context.RunUntil(
                () => !h.Sidebar.IsImporting && h.ScanCalls == scans + 1 && !h.Lifecycle.IsRescanActive,
                "the recorded watcher run after the import");
            h.Context.Await(h.Lifecycle.RescanCompletion);
            A11yEvent.VaultRescanFinished finished = Assert.Single(h.Events.OfType<A11yEvent.VaultRescanFinished>());
            Assert.Equal((RescanReason.Watcher, 1UL), (finished.Reason, finished.Changed));
            Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "late.md");
        }

        using (var h = new Harness("watcher-trash", ("full/one.md", "1\n"), ("full/two.md", "2\n")))
        {
            using var parked = new ManualResetEventSlim(false);
            using var release = new ManualResetEventSlim(false);
            h.Sidebar.TrashWorkerStartingForTesting = token =>
            {
                parked.Set();
                _ = release.Wait(TimeSpan.FromSeconds(30));
            };
            h.Sidebar.ConfirmRecycle = _ => false;
            int scans = h.ScanCalls;
            h.Sidebar.SelectedNode = Assert.Single(h.Sidebar.RootNodes, node => node.Path == "full");
            h.Sidebar.DeleteCommand.Execute(null);
            h.Context.RunUntil(() => parked.IsSet, "the trash worker parking");
            Assert.True(h.Sidebar.IsTrashing);
            h.Write("late.md", "# Late\n");
            h.SignalExternalChange();
            h.Settle();
            Assert.Equal(scans, h.ScanCalls);

            release.Set();
            h.Context.RunUntil(
                () => !h.Sidebar.IsTrashing && h.ScanCalls == scans + 1 && !h.Lifecycle.IsRescanActive,
                "the recorded watcher run after the trash");
            h.Context.Await(h.Lifecycle.RescanCompletion);
            A11yEvent.VaultRescanFinished finished = Assert.Single(h.Events.OfType<A11yEvent.VaultRescanFinished>());
            Assert.Equal((RescanReason.Watcher, 1UL), (finished.Reason, finished.Changed));
            Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "late.md");
        }
    });

    /// <summary>§4.2 item 3: before the workspace exists — the open parked
    /// after its scan and its Quick Open listing, still busy — a watcher
    /// request is recorded, and the end of the open serves it: one Watcher
    /// run, which finds the file written after the open's walk and says
    /// so.</summary>
    [Fact]
    public void AWatcherRequestBeforeTheWorkspaceRunsAfterTheOpen() => RunSta(() =>
    {
        bool busyWhileParked = false;
        bool workspaceWhileParked = true;
        int scansWhileParked = -1;
        using var h = Harness.WhileOpening(
            "watcher-before-workspace",
            opening =>
            {
                opening.Write("late.md", "# Late\n");
                opening.SignalExternalChange();
                opening.Context.Drain();
                busyWhileParked = opening.Lifecycle.IsBusy;
                workspaceWhileParked = opening.Lifecycle.Workspace is not null;
                scansWhileParked = opening.ScanCalls;
                // The served run's scan parks, so its sentence lands after
                // the harness has set the open's lines aside.
                opening.ParkAfterCall = 2;
            },
            ("alpha.md", "# Alpha\n"));
        h.Context.RunUntil(() => h.Parked.IsSet, "the recorded watcher run's scan");
        Assert.Equal(2, h.ScanCalls);
        h.Release.Set();
        h.Context.RunUntil(() => !h.Lifecycle.IsRescanActive, "the recorded watcher run");
        h.Context.Await(h.Lifecycle.RescanCompletion);

        Assert.True(busyWhileParked, "the open was not busy while parked");
        Assert.False(workspaceWhileParked, "the workspace existed while the open was parked");
        Assert.Equal(1, scansWhileParked);
        Assert.Equal(RescanReason.Watcher, OneRescanSentence(h).Reason);
        Assert.Equal([Explicit1], h.Spoken);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "late.md");
    });

    /// <summary>§4.2 item 3: core already spaced a watcher request, so the
    /// foreground cooldown never applies to it — one second after a scan a
    /// foreground request does not even scan, and a watcher request
    /// does.</summary>
    [Fact]
    public void AWatcherRequestIsNotSubjectToTheForegroundCooldown() => RunSta(() =>
    {
        using var h = new Harness("watcher-cooldown", ("alpha.md", "# Alpha\n"));
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        h.Events.Clear();
        int scans = h.ScanCalls;
        h.Now += TimeSpan.FromSeconds(1);
        h.Write("late.md", "# Late\n");

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        Assert.Equal(scans, h.ScanCalls);

        SignalAndAwaitTheWatcherRun(h);
        Assert.Equal(scans + 1, h.ScanCalls);
        Assert.Equal(RescanReason.Watcher, OneRescanSentence(h).Reason);
        Assert.Equal([Explicit1], h.Spoken);
    });

    /// <summary>§4.6: an overflow — the platform dropped events — reaches the
    /// host as one catch-up call, like any burst, and makes ONE full rescan
    /// however many files it covers; more calls while that run is in flight
    /// make one follow-up, never one each.</summary>
    [Fact]
    public void AnOverflowSignalRunsOneRescan() => RunSta(() =>
    {
        using var h = new Harness("watcher-overflow", ("alpha.md", "# Alpha\n"));
        for (int n = 0; n < 50; n++)
        {
            h.Write($"burst/{n:00}.md", $"# Burst {n}\n");
        }

        int scans = h.ScanCalls;
        SignalAndAwaitTheWatcherRun(h);
        Assert.Equal(scans + 1, h.ScanCalls);
        Assert.Equal(["Files refreshed. 50 new or changed, 0 removed."], h.Spoken);

        h.Events.Clear();
        h.Now += VaultLifecycleViewModel.WatcherSentenceSpacing;
        scans = h.ScanCalls;
        h.ParkAfterCall = scans + 1;
        h.SignalExternalChange();
        h.Context.RunUntil(() => h.Parked.IsSet, "the watcher scan parking");
        h.Write("late.md", "# Late\n");
        h.SignalExternalChange();
        h.SignalExternalChange();
        h.SignalExternalChange();
        h.Context.Drain();
        h.Release.Set();
        h.Context.RunUntil(() => h.ScanCalls == scans + 2 && !h.Lifecycle.IsRescanActive, "the one follow-up");
        h.Context.Await(h.Lifecycle.RescanCompletion);
        h.Settle();

        Assert.Equal(scans + 2, h.ScanCalls);
        Assert.Equal([Explicit1], h.Spoken);
    });

    /// <summary>
    /// The close stops the watch FIRST (<c>CloseSession</c>, before the
    /// rescan admission closes). With the real watcher armed and a rescan
    /// parked, so the close's drain holds the teardown open for ten seconds,
    /// a file made outside Slate meanwhile reaches no listener — a second
    /// listener on the same session would hear core's call. And a signal
    /// already queued when the vault closes, or one through the closed
    /// vault's listener after it, starts no run and says nothing.
    /// </summary>
    [Fact]
    public void CloseStopsTheWatchAndDropsALateSignal() => RunSta(() =>
    {
        using (VaultWatcherSwitch.Lift())
        {
            using var h = new Harness("watcher-close", ("alpha.md", "# Alpha\n"));
            var outside = new EventRecorder();
            _ = h.Lifecycle.SessionForTests!.RegisterEventListener(outside);
            h.ParkAfterCall = h.ScanCalls + 1;
            Task parked = h.Lifecycle.RescanAsync(RescanReason.Explicit);
            h.Context.RunUntil(() => h.Parked.IsSet, "the explicit scan parking");
            Task writer = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                h.Write("outside.md", "# Made outside while Slate closes\n");
                // Past core's quiet window and its spacing after the parked
                // scan: a live watch would have called by now.
                await Task.Delay(TimeSpan.FromSeconds(10));
                h.Release.Set();
            });
            h.Lifecycle.CloseVault();
            h.Context.Await(writer, "the outside write");
            h.Context.Await(parked, "the closed rescan");

            Assert.Equal(0, outside.ExternalChanges);
            Assert.False(h.Lifecycle.IsVaultOpen);
        }

        using var late = new Harness("watcher-late-signal", ("alpha.md", "# Alpha\n"));
        VaultEventListener closed = late.Lifecycle.EventListenerForTests!;
        int scans = late.ScanCalls;
        late.HoldUiQueue = true;
        late.SignalExternalChange();
        Assert.True(late.HeldUiActions >= 1, "the watcher's signal was not queued for the UI");
        late.Lifecycle.CloseVault();
        late.ReleaseUiQueue();
        Task.Run(() => closed.OnExternalChange()).GetAwaiter().GetResult();
        late.Settle();

        Assert.Equal(scans, late.ScanCalls);
        Assert.False(late.Lifecycle.IsRescanActive);
        Assert.DoesNotContain(late.Events, e => e is A11yEvent.VaultRescanFinished or A11yEvent.VaultRescanIncomplete);
    });

    // ---------------------------------------------------------------------
    // The fallback
    // ---------------------------------------------------------------------

    /// <summary>§4.6: the baseline never turns off. With the real watcher
    /// live, Refresh still runs its explicit rescan and speaks its own count,
    /// the foreground route still rescans a return after being away and says
    /// what changed — and the watch was live throughout: a file made outside
    /// Slate at the end reaches the tree with no Refresh.</summary>
    [Fact]
    public void TheBaselineIsUnchangedWhileTheWatcherIsLive() => RunSta(() =>
    {
        using IDisposable watcher = VaultWatcherSwitch.Lift();
        using var h = new Harness("watcher-baseline", ("alpha.md", "# Alpha\n"));
        var route = new ForegroundRescanRoute(
            h.Lifecycle.RescanAsync,
            () => false,
            () => h.Now,
            h.Lifecycle.ServeWatcherRequestOnActivation);
        Assert.Null(route.OnActivated());

        h.Write("refreshed.md", "# Refreshed\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal(RescanReason.Explicit, OneRescanSentence(h).Reason);
        Assert.Equal([Explicit1], h.Spoken);

        h.Events.Clear();
        h.WindowActive = false;
        route.OnDeactivated();
        h.Now += TimeSpan.FromMinutes(1);
        h.Write("away.md", "# Away\n");
        h.WindowActive = true;
        Task? returned = route.OnActivated();
        Assert.NotNull(returned);
        h.Context.Await(returned!);
        Assert.Equal(RescanReason.Foreground, OneRescanSentence(h).Reason);
        Assert.Equal([Explicit1], h.Spoken);

        h.Events.Clear();
        h.Write("live.md", "# Live\n");
        h.Context.RunUntil(
            () => h.Sidebar.RootNodes.Any(node => node.Path == "live.md") && !h.Lifecycle.IsRescanActive,
            "the watcher's run listing live.md");
        h.Context.Await(h.Lifecycle.RescanCompletion);
        A11yEvent.VaultRescanFinished finished = OneRescanSentence(h);
        Assert.Equal((RescanReason.Watcher, 1UL), (finished.Reason, finished.Changed));
    });

    /// <summary>§4.6: a watcher that cannot start — here the test switch,
    /// which every unit fact runs under — is a diagnostic in the host log and
    /// nothing else: the open says exactly what it always said, and Refresh
    /// and the foreground rescan reconcile and speak as before.</summary>
    [Fact]
    public void AnUnavailableWatcherLeavesTheBaselineUnchanged() => RunSta(() =>
    {
        using var log = new HostLogLines();
        using var h = new Harness("watcher-unavailable", ("alpha.md", "# Alpha\n"));
        Assert.Contains("SlateWindows.VaultWatchUnavailable", log.Lines);
        Assert.All(h.OpenEvents, e => Assert.True(
            e is A11yEvent.VaultOpened or A11yEvent.VaultScanStarted or A11yEvent.VaultScanProgress or A11yEvent.VaultScanFinished,
            $"the open said {e.GetType().Name}"));
        Assert.Equal("Scan finished: 1 files, 1 new or changed.", h.OpenStatusText);

        h.Write("late.md", "# Late\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal([Explicit1], h.Spoken);

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Write("later.md", "# Later\n");
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        Assert.Equal(RescanReason.Foreground, OneRescanSentence(h).Reason);
        Assert.Equal([Explicit1], h.Spoken);
    });

    /// <summary>The call core's watcher makes, and the Watcher run it starts,
    /// to its end.</summary>
    private static void SignalAndAwaitTheWatcherRun(Harness h)
    {
        int before = h.ScanCalls;
        h.SignalExternalChange();
        h.Context.RunUntil(() => h.ScanCalls > before && !h.Lifecycle.IsRescanActive, "the watcher's run");
        h.Context.Await(h.Lifecycle.RescanCompletion);
    }

    private static A11yEvent.VaultRescanFinished OneRescanSentence(Harness h) =>
        Assert.IsType<A11yEvent.VaultRescanFinished>(Assert.Single(h.Events));

    /// <summary>The host log's lines while this is alive, from any thread,
    /// through HostLog's observer (chained to any other, and restored).</summary>
    private sealed class HostLogLines : IDisposable
    {
        private readonly Action<string>? _previous = HostLog.ObserverForTests;
        private readonly ConcurrentQueue<string> _lines = new();

        internal HostLogLines() =>
            HostLog.ObserverForTests = message =>
            {
                _previous?.Invoke(message);
                _lines.Enqueue(message);
            };

        internal IReadOnlyCollection<string> Lines => _lines;

        public void Dispose() => HostLog.ObserverForTests = _previous;
    }
}
