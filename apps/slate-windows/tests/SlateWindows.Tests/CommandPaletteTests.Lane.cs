// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Windows.Threading;
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
    /// Enter while a newer query's rows are on their way runs the selection
    /// those rows bring, once they land — never the previous keystroke's
    /// row. Moving the selection meanwhile withdraws it.
    /// </summary>
    [Fact]
    public void EnterWhileARankIsPendingRunsTheRowsItWasTypedFor() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;

        host.Ranker.Park("q");
        palette.Query = "q";
        host.Ranker.WaitUntilEntered("q");
        palette.InvokeSelected();
        Assert.Empty(host.Harness.Source.Invoked);
        Assert.True(palette.IsOpen);

        host.Ranker.Release("q");
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsOpen, TimeSpan.FromSeconds(10)),
            "Enter never ran once the rows landed");
        Assert.Equal(["slate.nav.quickOpen"], host.Harness.Source.Invoked);

        // Withdrawn: the user moved after pressing Enter.
        palette.Open();
        host.PumpUntilPublished();
        host.Ranker.Park("o");
        palette.Query = "o";
        host.Ranker.WaitUntilEntered("o");
        palette.InvokeSelected();
        palette.MoveSelection(1);
        host.Ranker.Release("o");
        host.PumpUntilPublished();
        Assert.True(palette.IsOpen);
        Assert.Equal(["slate.nav.quickOpen"], host.Harness.Source.Invoked);
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
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !host.Palette.IsOpen, TimeSpan.FromSeconds(10)),
            "the palette never dismissed after the invocation");
        Assert.NotNull(host.Harness.Source.RecordThread);
        Assert.NotEqual(host.Owner, host.Harness.Source.RecordThread);
    });

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
    /// An Enter pressed while query A was ranking belongs to A: a later query
    /// B withdraws it, so when A is discarded and B publishes, nothing runs
    /// and the palette stays open on B's rows. (The positive case — A lands
    /// and runs its selection — is <c>EnterWhileARankIsPendingRunsTheRowsItWasTypedFor</c>.)
    /// </summary>
    [Fact]
    public void AnEnterPressedForASupersededQueryRunsNothing() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;

        host.Ranker.Park("q");
        palette.Query = "q";
        host.Ranker.WaitUntilEntered("q");
        palette.InvokeSelected();
        host.Ranker.Park("o");
        palette.Query = "o";

        host.Ranker.Release("q");
        host.Ranker.WaitUntilEntered("o");
        PumpedDispatcher.Drain();
        Assert.Empty(host.Harness.Source.Invoked);

        host.Ranker.Release("o");
        host.PumpUntilPublished();
        Assert.Empty(host.Harness.Source.Invoked);
        Assert.True(palette.IsOpen);
        Assert.Equal(
            ["slate.file.newNote", "slate.nav.quickOpen", "slate.editor.bold"],
            host.Harness.RowIds);
    });

    /// <summary>
    /// Contract P9 step 4 on the palette's own lane: the recents write lands
    /// first and the palette dismisses after it — while the write is parked
    /// the palette stays up and a second Enter runs nothing — and a write
    /// that fails still dismisses.
    /// </summary>
    [Fact]
    public void ARecordLandsBeforeThePaletteDismisses() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        using var gate = new ManualResetEventSlim(false);
        host.Harness.Source.RecordGate = gate;
        lock (host.Harness.Log)
        {
            host.Harness.Log.Clear();
        }

        palette.InvokeSelected();
        Assert.Equal(["slate.file.newNote"], host.Harness.Source.Invoked);
        Assert.True(
            SpinWait.SpinUntil(
                () => host.Harness.Source.RecordThread is not null,
                TimeSpan.FromSeconds(10)),
            "the recents write never started");
        PumpedDispatcher.Drain();
        Assert.True(palette.IsOpen, "the palette dismissed before its recents write landed");
        palette.InvokeSelected();
        Assert.Single(host.Harness.Source.Invoked);

        gate.Set();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsOpen, TimeSpan.FromSeconds(10)),
            "the palette never dismissed after its recents write");
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
            ["invoke:slate.file.newNote", "record:slate.file.newNote", "dismiss"],
            order);

        // A failed write is logged on the lane and still dismisses.
        host.Harness.Source.RecordGate = null;
        host.Harness.Source.RecordFailure = new IOException("recents write refused");
        palette.Open();
        host.PumpUntilPublished();
        palette.InvokeSelected();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsOpen, TimeSpan.FromSeconds(10)),
            "a failed recents write left the palette open");
    });

    /// <summary>
    /// Teardown with work parked on the lane: the palette shuts down, the
    /// command source is disposed only once the lane is quiet, and nothing
    /// that finishes afterwards publishes, announces or re-opens the palette
    /// — first with the open's command load parked, then with a rank parked.
    /// </summary>
    [Fact]
    public void TeardownWaitsForTheLaneAndNothingLandsAfterIt() => RunSta(() =>
    {
        LaneHost loading = LaneHost.Create();
        CommandLoadGate gate = loading.ParkTheCommandLoad();
        loading.Palette.Open();
        gate.WaitUntilEntered();
        var loadProbe = new DisposalProbe(() => loading.Harness.Source.ListCommandsReturned);
        ReleaseSoon(gate.Event);
        VaultLifecycleViewModel.ShutDownPalette(loading.Palette, loadProbe);
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
        ReleaseSoon(ranking.Ranker.GateOf("o"));
        VaultLifecycleViewModel.ShutDownPalette(ranking.Palette, rankProbe);
        Assert.True(
            rankProbe.LaneWasQuietAtDisposal,
            "the source was disposed while a rank still ran");
        ranking.AssertNothingLandsAfterTeardown();
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

    /// <summary>Releases a parked lane item shortly after the calling thread
    /// has started its teardown, from a thread that is not blocked by it.</summary>
    private static void ReleaseSoon(ManualResetEventSlim gate) =>
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            gate.Set();
        });

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
            Harness = new PaletteHarness(
                SynchronizationContext.Current,
                StandardCommands(),
                productionLane: true,
                rank: Ranker.Rank);
        }

        public int Owner { get; }

        public ParkedRanker Ranker { get; }

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

        /// <summary>Every query actually ranked, in order.</summary>
        public ConcurrentQueue<string> Ranked { get; } = new();

        public void Park(string query) => _gates[query] = new ManualResetEventSlim(false);

        public void Release(string query) => _gates[query].Set();

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

            PaletteSection[] ranked = SlateUniffiMethods.PaletteSections(commands, query, recents, pinned);
            _returned[query] = true;
            return ranked;
        }

        private ManualResetEventSlim Entered(string query) =>
            _entered.GetOrAdd(query, _ => new ManualResetEventSlim(false));
    }
}
