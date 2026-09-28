// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Windows.Threading;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1275: the palette's FFI work runs off the UI thread (locked decision 05
/// §4, principle 2). These facts run the palette's own lane against a real
/// dispatcher and a ranker a fact can park, so they see the hand-off to a
/// worker and the publication back — the synchronous harness the facts
/// above use cannot.
/// </summary>
public sealed partial class CommandPaletteTests
{
    /// <summary>
    /// A rank that has not finished leaves the owning dispatcher free — a
    /// queued operation runs while the ranker is parked on a worker — and
    /// changes nothing the user sees or hears until its rows publish; then
    /// the selection's move and the count arrive in that order.
    /// </summary>
    [Fact]
    public void ASlowRankLeavesTheDispatcherPumpingAndTheRowsUntouched() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        Assert.NotEqual(host.Owner, host.Harness.Source.ListCommandsThread);
        palette.SelectLast();
        Assert.Equal("slate.tasks.review", palette.SelectedId);
        host.Harness.Announcements.Clear();
        CommandPaletteRowViewModel[] before = [.. palette.Rows];

        host.Ranker.Park("o");
        palette.Query = "o";
        host.Ranker.WaitUntilEntered("o");
        Assert.NotEqual(host.Owner, host.Ranker.ThreadOf("o"));

        bool ran = false;
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, () => ran = true);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => ran, TimeSpan.FromSeconds(10)),
            "the dispatcher stopped pumping while the rank was in flight");
        Assert.True(palette.IsRankPending);
        Assert.Equal(before, palette.Rows);
        Assert.Equal("slate.tasks.review", palette.SelectedId);
        Assert.Empty(host.Harness.Announcements);

        host.Ranker.Release("o");
        host.PumpUntilPublished();
        Assert.Equal(
            ["slate.file.newNote", "slate.nav.quickOpen", "slate.editor.bold"],
            host.Harness.RowIds);
        Assert.Collection(
            host.Harness.Announcements,
            announced => Assert.Equal(
                "New Note",
                Assert.IsType<A11yEvent.PaletteCommandSelected>(announced).Label),
            announced => Assert.Equal(
                "o",
                Assert.IsType<A11yEvent.PaletteFilterCount>(announced).Query));
    });

    /// <summary>
    /// Three queries in quick succession: the one already ranking finishes
    /// and is discarded unseen, the one still waiting behind it never runs,
    /// and only the newest publishes — one selection sentence and one count,
    /// both for it.
    /// </summary>
    [Fact]
    public void OnlyTheNewestQueryPublishes() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        palette.SelectLast();
        host.Harness.Announcements.Clear();
        var published = new List<string[]>();
        palette.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(CommandPaletteViewModel.Rows))
            {
                published.Add([.. host.Harness.RowIds]);
            }
        };

        host.Ranker.Park("o");
        palette.Query = "o";
        host.Ranker.WaitUntilEntered("o");
        palette.Query = "t";
        palette.Query = "q";

        host.Ranker.Release("o");
        host.PumpUntilPublished();

        Assert.Equal([["slate.nav.quickOpen"]], published);
        Assert.DoesNotContain("t", host.Ranker.Ranked);
        Assert.Contains("o", host.Ranker.Ranked);
        Assert.Collection(
            host.Harness.Announcements,
            announced => Assert.Equal(
                "Quick Open",
                Assert.IsType<A11yEvent.PaletteCommandSelected>(announced).Label),
            announced => Assert.Equal(
                (1u, "q"),
                (Assert.IsType<A11yEvent.PaletteFilterCount>(announced).Count,
                    ((A11yEvent.PaletteFilterCount)announced).Query)));
    });

    /// <summary>
    /// The selection rule reads the row the user is on when the rows land,
    /// not when they were asked for: a move made while the rank runs is the
    /// selection the publication keeps, and keeping it says nothing (P7).
    /// </summary>
    [Fact]
    public void TheSelectionMadeWhileARankRunsIsTheOneThePublicationKeeps() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        Assert.Equal("slate.file.newNote", palette.SelectedId);

        host.Ranker.Park("o");
        palette.Query = "o";
        host.Ranker.WaitUntilEntered("o");
        palette.Select(Assert.Single(palette.Rows, row => row.Id == "slate.editor.bold"));
        Assert.Equal(["Toggle Bold"], host.SelectionAnnouncements);
        host.Harness.Announcements.Clear();

        host.Ranker.Release("o");
        host.PumpUntilPublished();

        Assert.Equal("slate.editor.bold", palette.SelectedId);
        A11yEvent.PaletteFilterCount count = Assert.IsType<A11yEvent.PaletteFilterCount>(
            Assert.Single(host.Harness.Announcements));
        Assert.Equal((3u, "o"), (count.Count, count.Query));
    });

    /// <summary>
    /// #1275 codex round 2, finding 1 (P7): Enter acts on the list on
    /// screen. With "New Note" published and a newer query still ranking,
    /// Enter runs New Note at once — never the Quick Open row the pending
    /// query would bring — and that rank's rows, landing later, run nothing.
    /// A move made while a rank is pending moves over the rows on screen,
    /// and Enter runs THAT row.
    /// </summary>
    [Fact]
    public void EnterRunsTheSelectionOnScreenWhileANewerRankIsPending() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        Assert.Equal("slate.file.newNote", palette.SelectedId);

        host.Ranker.Park("q");
        palette.Query = "q";
        host.Ranker.WaitUntilEntered("q");
        Assert.True(palette.IsRankPending);
        palette.InvokeSelected();
        Assert.Equal(["slate.file.newNote"], host.Harness.Source.Invoked);
        Assert.False(palette.IsOpen);

        host.Ranker.Release("q");
        PumpedDispatcher.PumpUntilDrained(palette.RankCompletion);
        PumpedDispatcher.Drain();
        Assert.Equal(["slate.file.newNote"], host.Harness.Source.Invoked);
        Assert.False(palette.IsOpen);

        palette.Open();
        host.PumpUntilPublished();
        host.Ranker.Park("o");
        palette.Query = "o";
        host.Ranker.WaitUntilEntered("o");
        palette.SelectLast();
        Assert.Equal("slate.tasks.review", palette.SelectedId);
        palette.InvokeSelected();
        Assert.Equal(["slate.file.newNote", "slate.tasks.review"], host.Harness.Source.Invoked);

        host.Ranker.Release("o");
        PumpedDispatcher.PumpUntilDrained(palette.RankCompletion);
        PumpedDispatcher.Drain();
        Assert.Equal(["slate.file.newNote", "slate.tasks.review"], host.Harness.Source.Invoked);
    });

    /// <summary>
    /// #1275 codex round 2, finding 1: with nothing published — the open's
    /// snapshot still loading — there is no selection, and Enter does
    /// nothing, then or later: the first rows landing run nothing and say
    /// nothing beyond what P7 and P10 say for an open (the first selection
    /// is silent).
    /// </summary>
    [Fact]
    public void EnterBeforeAnythingIsPublishedRunsNothing() => RunSta(() =>
    {
        LaneHost host = LaneHost.Create();
        CommandPaletteViewModel palette = host.Palette;
        CommandLoadGate gate = host.ParkTheCommandLoad();

        palette.Open();
        gate.WaitUntilEntered();
        Assert.Null(palette.SelectedRow);
        palette.InvokeSelected();
        Assert.Empty(host.Harness.Source.Invoked);
        Assert.True(palette.IsOpen);

        gate.Release();
        host.PumpUntilPublished();
        Assert.Empty(host.Harness.Source.Invoked);
        Assert.True(palette.IsOpen);
        Assert.Equal("slate.file.newNote", palette.SelectedId);
        Assert.Empty(host.Harness.Announcements);
    });

    /// <summary>
    /// #1275 codex round 2, finding 1: a rank that fails resolves — the
    /// palette is no longer pending — with the list, the selection and the
    /// copy left as last published, nothing said for the failed query, the
    /// failure logged, and the keys still working on the rows on screen.
    /// </summary>
    [Fact]
    public void AFailedRankResolvesAndTheKeysStillWorkTheRowsOnScreen() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        palette.SelectLast();
        host.Harness.Announcements.Clear();
        CommandPaletteRowViewModel[] before = [.. palette.Rows];

        host.Ranker.Fail("x", new InvalidOperationException("ranking refused"));
        palette.Query = "x";
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
            "a failed rank left the palette pending forever");
        PumpedDispatcher.PumpUntilDrained(palette.RankCompletion);
        PumpedDispatcher.PumpUntilDrained(palette.FilterCountCompletion);
        PumpedDispatcher.Drain();

        Assert.Equal(before, palette.Rows);
        Assert.Equal("slate.tasks.review", palette.SelectedId);
        Assert.Empty(host.Harness.Announcements);
        Assert.Contains(HostDiagnosticEvent.PaletteWorkFailed, host.Harness.Diagnostics);

        palette.MoveSelection(-1);
        Assert.Equal("slate.editor.bold", palette.SelectedId);
        palette.InvokeSelected();
        Assert.Equal(["slate.editor.bold"], host.Harness.Source.Invoked);
        Assert.False(palette.IsOpen);
    });

    /// <summary>
    /// #1275 codex round 2, finding 3 (P10): the count's trailing window
    /// opens at the KEYSTROKE. Here it runs out while the rank is still
    /// parked: nothing is said until the rows publish, and then the
    /// selection and the count arrive once each, the count at publication.
    /// </summary>
    [Fact]
    public void TheCountWindowOpensAtTheKeystrokeAndARankSlowerThanItSpeaksAtPublication() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        List<TaskCompletionSource> windows = host.HoldTheCountWindows();
        host.Harness.Announcements.Clear();

        host.Ranker.Park("q");
        palette.Query = "q";
        host.Ranker.WaitUntilEntered("q");
        TaskCompletionSource window = Assert.Single(windows);

        window.SetResult();
        PumpedDispatcher.Drain();
        Assert.Empty(host.Harness.Announcements);

        host.Ranker.Release("q");
        host.PumpUntilPublished();
        Assert.Single(windows);
        Assert.Collection(
            host.Harness.Announcements,
            announced => Assert.Equal(
                "Quick Open",
                Assert.IsType<A11yEvent.PaletteCommandSelected>(announced).Label),
            announced => Assert.Equal(
                (1u, "q"),
                (Assert.IsType<A11yEvent.PaletteFilterCount>(announced).Count,
                    ((A11yEvent.PaletteFilterCount)announced).Query)));
    });

    /// <summary>
    /// The other ordering: the rows publish while the keystroke's window is
    /// still open — the selection speaks at publication, the count only when
    /// the window runs out, once.
    /// </summary>
    [Fact]
    public void ARankFasterThanTheCountWindowSpeaksTheCountWhenTheWindowRunsOut() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        List<TaskCompletionSource> windows = host.HoldTheCountWindows();
        host.Harness.Announcements.Clear();

        palette.Query = "q";
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
            "the rank never published");
        PumpedDispatcher.Drain();
        Assert.Equal(["Quick Open"], host.SelectionAnnouncements);
        Assert.Empty(host.Harness.Announcements.OfType<A11yEvent.PaletteFilterCount>());

        Assert.Single(windows).SetResult();
        PumpedDispatcher.PumpUntilDrained(palette.FilterCountCompletion);
        PumpedDispatcher.Drain();
        Assert.Collection(
            host.Harness.Announcements,
            announced => Assert.Equal(
                "Quick Open",
                Assert.IsType<A11yEvent.PaletteCommandSelected>(announced).Label),
            announced => Assert.Equal(
                (1u, "q"),
                (Assert.IsType<A11yEvent.PaletteFilterCount>(announced).Count,
                    ((A11yEvent.PaletteFilterCount)announced).Query)));
    });

    /// <summary>
    /// The open's snapshot loads on the lane: no empty state claims the
    /// registry is empty while it loads, a query typed meanwhile is the one
    /// the first rows answer, and a palette closed before anything lands
    /// publishes nothing.
    /// </summary>
    [Fact]
    public void TheSnapshotLoadsOffTheUiThreadAndAClosedPaletteTakesNothing() => RunSta(() =>
    {
        LaneHost host = LaneHost.Create();
        CommandPaletteViewModel palette = host.Palette;
        CommandLoadGate gate = host.ParkTheCommandLoad();

        palette.Open();
        Assert.True(palette.IsOpen);
        gate.WaitUntilEntered();
        Assert.NotEqual(host.Owner, host.Harness.Source.ListCommandsThread);
        Assert.False(palette.ShowsEmptyState);
        Assert.Empty(palette.Rows);
        palette.Query = "o";
        gate.Release();
        host.PumpUntilPublished();
        Assert.Equal(
            ["slate.file.newNote", "slate.nav.quickOpen", "slate.editor.bold"],
            host.Harness.RowIds);
        A11yEvent.PaletteFilterCount count = Assert.IsType<A11yEvent.PaletteFilterCount>(
            Assert.Single(host.Harness.Announcements));
        Assert.Equal("o", count.Query);

        // Closed while its rank ran: nothing lands, nothing is said.
        host.Ranker.Park("q");
        palette.Query = "q";
        host.Ranker.WaitUntilEntered("q");
        palette.Dismiss();
        host.Harness.Announcements.Clear();
        host.Ranker.Release("q");
        PumpedDispatcher.PumpUntilDrained(palette.RankCompletion);
        PumpedDispatcher.Drain();
        Assert.False(palette.IsOpen);
        Assert.Empty(palette.Rows);
        Assert.Empty(host.Harness.Announcements);
    });

    /// <summary>
    /// A successful invocation's recents transition is written on the lane,
    /// not on the owning thread.
    /// </summary>
    [Fact]
    public void ARecordedInvocationIsWrittenOffTheUiThread() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        host.Palette.InvokeSelected();
        Assert.False(host.Palette.IsOpen);
        PumpedDispatcher.PumpUntilDrained(host.Palette.RecordCompletion);
        Assert.NotNull(host.Harness.Source.RecordThread);
        Assert.NotEqual(host.Owner, host.Harness.Source.RecordThread);
    });

    /// <summary>
    /// #1275 codex round 2, finding 2 (P9 step 4 across the lane): a
    /// successful invocation hands its recents transition to the lane and
    /// then retires the palette in the SAME dispatcher turn — it does not
    /// wait for the write. The write's place on the lane is what P9's
    /// "record, then dismiss" governs: with it parked, the next open's
    /// snapshot waits behind it, so the recent exists before anything reads
    /// recents again. A write that throws or does not persist is logged.
    /// </summary>
    [Fact]
    public void ASuccessfulInvocationRetiresThePaletteAtOnceAndItsRecordLeadsTheNextOpen() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        using var gate = new ManualResetEventSlim(false);
        host.Harness.Source.RecordGate = gate;
        host.Harness.Source.LaneOrder.Clear();
        lock (host.Harness.Log)
        {
            host.Harness.Log.Clear();
        }

        palette.InvokeSelected();
        Assert.Equal(["slate.file.newNote"], host.Harness.Source.Invoked);
        Assert.False(palette.IsOpen, "the palette stayed up over the command it ran");
        Assert.True(
            SpinWait.SpinUntil(
                () => host.Harness.Source.RecordThread is not null,
                TimeSpan.FromSeconds(10)),
            "the recents write never started");
        Assert.False(palette.RecordCompletion.IsCompleted);

        palette.Open();
        PumpedDispatcher.Drain();
        Assert.True(palette.IsRankPending);
        Assert.Empty(palette.Rows);
        Assert.Empty(host.Harness.Source.LaneOrder);

        gate.Set();
        host.PumpUntilPublished();
        Assert.True(palette.RecordCompletion.IsCompletedSuccessfully);
        Assert.Equal(
            ["record:slate.file.newNote", "list", "recents"],
            host.Harness.Source.LaneOrder.ToArray());
        string[] order;
        lock (host.Harness.Log)
        {
            order =
            [
                .. host.Harness.Log.Where(entry =>
                    entry.StartsWith("invoke:", StringComparison.Ordinal)
                    || entry.StartsWith("record:", StringComparison.Ordinal)
                    || entry == "dismiss"),
            ];
        }

        Assert.Equal(
            ["invoke:slate.file.newNote", "dismiss", "record:slate.file.newNote"],
            order);
        Assert.Empty(host.Harness.Diagnostics);

        // A write that throws, and one that returns without persisting, are
        // each logged on the lane; the palette had already retired.
        host.Harness.Source.RecordGate = null;
        host.Harness.Source.RecordFailure = new IOException("recents write refused");
        palette.InvokeSelected();
        Assert.False(palette.IsOpen);
        PumpedDispatcher.PumpUntilDrained(palette.RecordCompletion);
        Assert.Equal([HostDiagnosticEvent.PaletteWorkFailed], host.Harness.Diagnostics.ToArray());

        host.Harness.Source.RecordFailure = null;
        host.Harness.Source.RecordPersisted = false;
        palette.Open();
        host.PumpUntilPublished();
        palette.InvokeSelected();
        Assert.False(palette.IsOpen);
        PumpedDispatcher.PumpUntilDrained(palette.RecordCompletion);
        Assert.Equal(
            [HostDiagnosticEvent.PaletteWorkFailed, HostDiagnosticEvent.PaletteWorkFailed],
            host.Harness.Diagnostics.ToArray());
    });

    /// <summary>
    /// #1275 codex round 2, finding 4: the recents store's ORDINARY failure
    /// — an IO error <c>TrySave</c> catches, so <c>RecordInvocation</c>
    /// returns normally — is logged, through the real
    /// <see cref="PaletteCommandSource"/> and the real
    /// <see cref="CommandPaletteRecentsStore"/>. The invocation still
    /// completes and the in-memory list still moves (P11).
    /// </summary>
    [Fact]
    public void AnUnpersistedRecentsWriteIsLoggedThroughTheRealStore()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-1275-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            // A FILE where the store's directory belongs: CreateDirectory
            // throws IOException, which TrySave catches and reports as false.
            string blocker = Path.Combine(root, "blocker");
            File.WriteAllText(blocker, "not a directory");
            var store = new CommandPaletteRecentsStore(Path.Combine(blocker, "command-palette-recents.json"));
            var host = new CommandRegistrationTests.FakeCommandHost { IsVaultOpen = true };
            using var source = new PaletteCommandSource(host, Dispatcher.CurrentDispatcher, store);
            var diagnostics = new ConcurrentQueue<HostDiagnosticEvent>();
            var palette = new CommandPaletteViewModel(
                source,
                _ => { },
                _ => Task.CompletedTask,
                new InlineWorkLane(),
                rank: null,
                diagnostics: (diagnostic, _) => diagnostics.Enqueue(diagnostic));

            palette.Open();
            palette.Invoke(Assert.Single(palette.Rows, row => row.Id == ChordTable.Ids.VaultOpen));

            Assert.Equal(1, host.OpenVaultInvocations);
            Assert.False(palette.IsOpen);
            Assert.True(palette.RecordCompletion.IsCompletedSuccessfully);
            Assert.IsAssignableFrom<IOException>(source.LastRecentsSaveError);
            Assert.Equal([HostDiagnosticEvent.PaletteWorkFailed], diagnostics.ToArray());
            Assert.Equal([ChordTable.Ids.VaultOpen], source.LoadRecents());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// The lane runs work in hand-over order, off the calling thread; a
    /// waiter cancelled before its turn never runs, and the work after it
    /// still waits for the work already running.
    /// </summary>
    [Fact]
    public async Task TheLaneRunsWorkInHandOverOrderAndSkipsCancelledWaiters()
    {
        var lane = new CommandPaletteWorkLane();
        var order = new ConcurrentQueue<string>();
        int caller = Environment.CurrentManagedThreadId;
        using var first = new ManualResetEventSlim(false);
        using var skipped = new CancellationTokenSource();

        Task<int> running = lane.Run(
            () =>
            {
                Assert.True(first.Wait(TimeSpan.FromSeconds(30)));
                order.Enqueue("first");
                return Environment.CurrentManagedThreadId;
            },
            CancellationToken.None);
        Task<int> cancelled = lane.Run(
            () =>
            {
                order.Enqueue("cancelled");
                return 0;
            },
            skipped.Token);
        Task<int> last = lane.Run(
            () =>
            {
                order.Enqueue("last");
                return 0;
            },
            CancellationToken.None);

        skipped.Cancel();
        Assert.False(last.IsCompleted);
        first.Set();
        int worker = await running.WaitAsync(TimeSpan.FromSeconds(30));
        await last.WaitAsync(TimeSpan.FromSeconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);

        Assert.NotEqual(caller, worker);
        Assert.Equal(["first", "last"], order.ToArray());
    }

    /// <summary>
    /// A lane item that faults with no caller awaiting it, then the teardown
    /// wait: once the lane is dropped and collected, its faulted tail raises
    /// no unobserved task exception — <c>WhenIdle</c> observed it (Codoki on
    /// #1298) — and <c>WhenIdle</c> itself completed without faulting.
    /// </summary>
    [Fact]
    public void WhenIdleObservesAFaultNobodyAwaited()
    {
        string marker = $"palette-lane-fault-{Guid.NewGuid():N}";
        int unobserved = 0;
        EventHandler<UnobservedTaskExceptionEventArgs> watch = (_, e) =>
        {
            if (e.Exception.Flatten().InnerExceptions.Any(inner => inner.Message == marker))
            {
                _ = Interlocked.Increment(ref unobserved);
            }
        };
        TaskScheduler.UnobservedTaskException += watch;
        try
        {
            FaultTheLaneAndWaitForIdle(marker);
            for (int pass = 0; pass < 3; pass++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= watch;
        }

        Assert.Equal(0, Volatile.Read(ref unobserved));
    }

    /// <summary>The lane, its faulted item and the idle wait live only in
    /// this frame, so all of them are collectable once it returns.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void FaultTheLaneAndWaitForIdle(string marker)
    {
        var lane = new CommandPaletteWorkLane();
        _ = lane.Run<int>(() => throw new InvalidOperationException(marker), CancellationToken.None);
        Task idle = lane.WhenIdle();
        Assert.True(idle.Wait(TimeSpan.FromSeconds(10)), "the lane never went idle");
        Assert.Equal(TaskStatus.RanToCompletion, idle.Status);
    }

    /// <summary>
    /// Teardown with work parked on the lane: the palette shuts down, the
    /// command source is disposed only once the lane is quiet, and nothing
    /// that finishes afterwards publishes, announces or re-opens the palette
    /// — with the open's command load parked, with a rank parked, and with a
    /// successful invocation's recents write parked (the recent exists
    /// before the source goes). Each parked item is released only once
    /// teardown is waiting on the lane — a handshake, not a delay.
    /// </summary>
    [Fact]
    public void TeardownWaitsForTheLaneAndNothingLandsAfterIt() => RunSta(() =>
    {
        LaneHost loading = LaneHost.Create();
        CommandLoadGate gate = loading.ParkTheCommandLoad();
        loading.Palette.Open();
        gate.WaitUntilEntered();
        var loadProbe = new DisposalProbe(() => loading.Harness.Source.ListCommandsReturned);
        Task<bool> loadRelease = ReleaseOnceTeardownWaits(loading.Lane, gate.Event);
        VaultLifecycleViewModel.ShutDownPalette(loading.Palette, loadProbe);
        AssertReleasedByTheHandshake(loadRelease);
        Assert.True(loadProbe.Disposed);
        Assert.True(
            loadProbe.LaneWasQuietAtDisposal,
            "the source was disposed while its command load still ran");
        loading.AssertNothingLandsAfterTeardown();

        LaneHost ranking = LaneHost.Opened();
        ranking.Ranker.Park("o");
        ranking.Palette.Query = "o";
        ranking.Ranker.WaitUntilEntered("o");
        ranking.Harness.Announcements.Clear();
        var rankProbe = new DisposalProbe(() => ranking.Ranker.HasReturned("o"));
        Task<bool> rankRelease = ReleaseOnceTeardownWaits(ranking.Lane, ranking.Ranker.GateOf("o"));
        VaultLifecycleViewModel.ShutDownPalette(ranking.Palette, rankProbe);
        AssertReleasedByTheHandshake(rankRelease);
        Assert.True(
            rankProbe.LaneWasQuietAtDisposal,
            "the source was disposed while a rank still ran");
        ranking.AssertNothingLandsAfterTeardown();

        LaneHost recording = LaneHost.Opened();
        using var recordGate = new ManualResetEventSlim(false);
        recording.Harness.Source.RecordGate = recordGate;
        recording.Palette.InvokeSelected();
        Assert.False(recording.Palette.IsOpen);
        Assert.True(
            SpinWait.SpinUntil(
                () => recording.Harness.Source.RecordThread is not null,
                TimeSpan.FromSeconds(10)),
            "the recents write never started");
        recording.Harness.Announcements.Clear();
        var recordProbe = new DisposalProbe(() => recording.Harness.Source.LaneOrder.Contains("record:slate.file.newNote"));
        Task<bool> recordRelease = ReleaseOnceTeardownWaits(recording.Lane, recordGate);
        VaultLifecycleViewModel.ShutDownPalette(recording.Palette, recordProbe);
        AssertReleasedByTheHandshake(recordRelease);
        Assert.True(
            recordProbe.LaneWasQuietAtDisposal,
            "the source was disposed before the recents write landed");
        recording.AssertNothingLandsAfterTeardown();
    });

    /// <summary>
    /// A command whose own action tears the shell down — a window close
    /// runs the palette's shutdown synchronously, inside the invoke — gets
    /// no recents write: teardown has already waited for the lane and let
    /// the source go, so nothing may join the lane after it (I8).
    /// </summary>
    [Fact]
    public void ACommandThatTearsTheShellDownQueuesNoRecentsWrite() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        var probe = new DisposalProbe(() => true);
        host.Harness.Source.OnInvoke = _ => VaultLifecycleViewModel.ShutDownPalette(host.Palette, probe);
        host.Harness.Source.LaneOrder.Clear();
        Task before = host.Palette.RecordCompletion;

        host.Palette.InvokeSelected();

        Assert.Equal(["slate.file.newNote"], host.Harness.Source.Invoked);
        Assert.True(probe.Disposed);
        Assert.True(host.Palette.IsShutDown);
        Assert.Same(before, host.Palette.RecordCompletion);
        PumpedDispatcher.PumpUntilDrained(host.Lane.WhenIdle());
        PumpedDispatcher.Drain();
        Assert.Empty(host.Harness.Source.Recorded);
        Assert.Empty(host.Harness.Source.LaneOrder);
        Assert.False(host.Palette.IsOpen);
    });

    /// <summary>The shell's teardown runs the palette's: disposing the
    /// lifecycle shuts its palette down before the command source goes.</summary>
    [Fact]
    public void DisposingTheLifecycleShutsItsPaletteDown()
    {
        using var fixture = FixtureVault.Create(1);
        var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(fixture.Root, "device-state", "recent-vaults.json")));
        CommandPaletteViewModel palette = lifecycle.Palette;

        lifecycle.Dispose();

        Assert.True(palette.IsShutDown);
    }

    // --- helpers -------------------------------------------------------------

    /// <summary>
    /// Releases a parked lane item once teardown is WAITING on the lane —
    /// signalled by the lane itself when the palette asks it for idleness —
    /// from a thread that teardown does not block. The timeout only bounds a
    /// failure (teardown that never waits); it orders nothing.
    /// </summary>
    private static Task<bool> ReleaseOnceTeardownWaits(SignallingLane lane, ManualResetEventSlim gate) =>
        Task.Run(() =>
        {
            bool waited = lane.WhenIdleAsked.Wait(TimeSpan.FromSeconds(30));
            gate.Set();
            return waited;
        });

    private static void AssertReleasedByTheHandshake(Task<bool> release) => Assert.True(
        release.Wait(TimeSpan.FromSeconds(30)) && release.Result,
        "teardown never asked the lane to go quiet");

    /// <summary>A command source stand-in that records whether the lane had
    /// gone quiet when it was disposed.</summary>
    private sealed class DisposalProbe(Func<bool> laneQuiet) : IDisposable
    {
        public bool Disposed { get; private set; }

        public bool LaneWasQuietAtDisposal { get; private set; }

        public void Dispose()
        {
            LaneWasQuietAtDisposal = laneQuiet();
            Disposed = true;
        }
    }

    /// <summary>The palette's production lane, reporting when someone asks
    /// it for idleness — the teardown facts' handshake.</summary>
    private sealed class SignallingLane : ICommandPaletteWorkLane
    {
        private readonly CommandPaletteWorkLane _inner = new();

        /// <summary>Set once <see cref="WhenIdle"/> has captured everything
        /// handed over so far — after that, releasing parked work cannot
        /// race the capture.</summary>
        public ManualResetEventSlim WhenIdleAsked { get; } = new(false);

        public Task<T> Run<T>(Func<T> work, CancellationToken cancellationToken) =>
            _inner.Run(work, cancellationToken);

        public Task WhenIdle()
        {
            Task idle = _inner.WhenIdle();
            WhenIdleAsked.Set();
            return idle;
        }
    }

    /// <summary>
    /// The palette over the fake source with its production lane, owned by
    /// the dispatcher thread the fact runs on, ranked by a
    /// <see cref="ParkedRanker"/>.
    /// </summary>
    private sealed class LaneHost
    {
        private LaneHost()
        {
            Owner = Environment.CurrentManagedThreadId;
            Ranker = new ParkedRanker(Owner);
            Lane = new SignallingLane();
            Harness = new PaletteHarness(
                SynchronizationContext.Current,
                StandardCommands(),
                productionLane: true,
                rank: Ranker.Rank,
                lane: Lane);
        }

        public int Owner { get; }

        public ParkedRanker Ranker { get; }

        public SignallingLane Lane { get; }

        public PaletteHarness Harness { get; }

        public CommandPaletteViewModel Palette => Harness.Palette;

        public string[] SelectionAnnouncements =>
        [
            .. Harness.Announcements
                .OfType<A11yEvent.PaletteCommandSelected>()
                .Select(selected => selected.Label),
        ];

        public static LaneHost Create()
        {
            Assert.IsType<DispatcherSynchronizationContext>(SynchronizationContext.Current);
            return new LaneHost();
        }

        /// <summary>Open, and pump until the open's rows have published.</summary>
        public static LaneHost Opened()
        {
            LaneHost host = Create();
            host.Palette.Open();
            host.PumpUntilPublished();
            Assert.Equal(5, host.Palette.Rows.Count);
            return host;
        }

        /// <summary>Pump the owning dispatcher until no rank is pending and
        /// the posted count has had its turn.</summary>
        public void PumpUntilPublished()
        {
            Assert.True(
                PumpedDispatcher.PumpUntil(() => !Palette.IsRankPending, TimeSpan.FromSeconds(10)),
                "the rank never published");
            PumpedDispatcher.PumpUntilDrained(Palette.FilterCountCompletion);
            PumpedDispatcher.Drain();
        }

        /// <summary>From here on every keystroke's count window is held
        /// open until the fact completes it; the list is in keystroke
        /// order. A cancelled window reports cancelled.</summary>
        public List<TaskCompletionSource> HoldTheCountWindows()
        {
            var windows = new List<TaskCompletionSource>();
            Harness.Window = token =>
            {
                var window = new TaskCompletionSource();
                token.Register(() => window.TrySetCanceled(token));
                windows.Add(window);
                return window.Task;
            };
            return windows;
        }

        /// <summary>Pumps what the lane left behind, then asserts the palette
        /// is shut, empty and silent, and stays shut when asked to open.</summary>
        public void AssertNothingLandsAfterTeardown()
        {
            PumpedDispatcher.PumpUntilDrained(Palette.RankCompletion);
            PumpedDispatcher.Drain();
            Assert.True(Palette.IsShutDown);
            Assert.False(Palette.IsOpen);
            Assert.Empty(Palette.Rows);
            Assert.Empty(Harness.Announcements);
            Palette.Open();
            PumpedDispatcher.Drain();
            Assert.False(Palette.IsOpen);
            Assert.Empty(Palette.Rows);
        }

        public CommandLoadGate ParkTheCommandLoad()
        {
            var gate = new CommandLoadGate(Harness.Source);
            Harness.Source.ListCommandsGate = gate.Event;
            return gate;
        }
    }

    /// <summary>Holds the open's command load and says when it began.</summary>
    private sealed class CommandLoadGate(FakePaletteCommandSource source)
    {
        public ManualResetEventSlim Event { get; } = new(false);

        public void WaitUntilEntered() => Assert.True(
            SpinWait.SpinUntil(() => source.ListCommandsThread is not null, TimeSpan.FromSeconds(10)),
            "the command load never started");

        public void Release() => Event.Set();
    }

    /// <summary>
    /// Core's ranking behind per-query gates. On the owning thread it never
    /// waits — a palette that ranked there would fail its thread assertion
    /// instead of hanging the fact.
    /// </summary>
    private sealed class ParkedRanker(int ownerThread)
    {
        private readonly ConcurrentDictionary<string, ManualResetEventSlim> _gates = new();
        private readonly ConcurrentDictionary<string, ManualResetEventSlim> _entered = new();
        private readonly ConcurrentDictionary<string, int> _threads = new();
        private readonly ConcurrentDictionary<string, bool> _returned = new();
        private readonly ConcurrentDictionary<string, Exception> _failures = new();

        /// <summary>Every query actually ranked, in order.</summary>
        public ConcurrentQueue<string> Ranked { get; } = new();

        public void Park(string query) => _gates[query] = new ManualResetEventSlim(false);

        public void Release(string query) => _gates[query].Set();

        /// <summary>Ranking <paramref name="query"/> throws
        /// <paramref name="failure"/> — core's ranking is pure, so this is
        /// the unexpected failure the palette must still resolve.</summary>
        public void Fail(string query, Exception failure) => _failures[query] = failure;

        public ManualResetEventSlim GateOf(string query) => _gates[query];

        /// <summary>Whether core's ranking for <paramref name="query"/> has
        /// returned to the lane.</summary>
        public bool HasReturned(string query) => _returned.ContainsKey(query);

        public void WaitUntilEntered(string query) => Assert.True(
            Entered(query).Wait(TimeSpan.FromSeconds(10)),
            $"the rank for \"{query}\" never reached the ranker");

        public int? ThreadOf(string query) =>
            _threads.TryGetValue(query, out int thread) ? thread : null;

        public PaletteSection[] Rank(Command[] commands, string query, string[] recents, string[] pinned)
        {
            _threads[query] = Environment.CurrentManagedThreadId;
            Ranked.Enqueue(query);
            Entered(query).Set();
            if (Environment.CurrentManagedThreadId != ownerThread
                && _gates.TryGetValue(query, out ManualResetEventSlim? gate))
            {
                Assert.True(gate.Wait(TimeSpan.FromSeconds(30)), $"\"{query}\" was never released");
            }

            if (_failures.TryGetValue(query, out Exception? failure))
            {
                throw failure;
            }

            PaletteSection[] ranked = SlateUniffiMethods.PaletteSections(commands, query, recents, pinned);
            _returned[query] = true;
            return ranked;
        }

        private ManualResetEventSlim Entered(string query) =>
            _entered.GetOrAdd(query, _ => new ManualResetEventSlim(false));
    }
}
