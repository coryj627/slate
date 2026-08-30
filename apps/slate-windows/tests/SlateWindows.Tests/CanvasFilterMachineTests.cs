// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 PR C-unit, task T6: U9's total state machine, driven — the
/// transition table's cells as facts, the burst rule, the lock-time
/// revalidation and the failed-answer bit, against an in-memory matcher
/// that parks and faults on demand and a runner the fact releases by
/// hand.
/// </summary>
public sealed class CanvasFilterMachineTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(30);

    // ---------------------------------------------------------------
    // The keystroke column
    // ---------------------------------------------------------------

    /// <summary>Idle + K: the request starts at once, the PENDING unit
    /// publishes in the same swap — the rows stay — and the completion
    /// publishes the answer and goes idle.</summary>
    [Fact]
    public void TypedWhenIdleStartsAtOnceAndTheCompletionAnswers()
    {
        var matcher = new Matcher { Answer = _ => ["a"] };
        var runner = new ManualRunner();
        (CanvasPublicationSlot slot, _) = LoadedSlot("root", "a");
        var machine = new CanvasFilterMachine(slot, matcher, runner.Enqueue);

        machine.Typed("a", active: true);

        CanvasPublication pending = slot.Current;
        Assert.True(
            pending.Filters.Running is not null && pending.Filters.Queued is null,
            "the keystroke on an idle machine starts its request.");
        Assert.True(
            pending.Unit!.Answer == CanvasAnswerState.Pending
                && pending.Unit.Needle == "a"
                && pending.Unit.VisibleCount == 2
                && pending.NeedleIntent == "a",
            "the pending unit publishes immediately, rows kept, intent recorded.");
        Assert.True(runner.Pending == 1, "and exactly one job was handed to the runner.");

        runner.RunAll();

        CanvasPublication answered = slot.Current;
        Assert.True(
            answered.Unit!.Answer == CanvasAnswerState.Answered
                && answered.Unit.Needle == "a"
                && answered.Unit.FilteredOrder.SequenceEqual(["a"])
                && answered.Filters.Running is null,
            "the running completion publishes its answer and the machine idles.");
        Assert.True(matcher.Matches == 1, $"the matcher ran {matcher.Matches} times.");
    }

    /// <summary>Running + K: queued, not started, the unit untouched;
    /// and a third keystroke REPLACES the queued request, which is
    /// dropped before it ever started.</summary>
    [Fact]
    public void TypedWhileRunningQueuesAndAThirdKeystrokeReplacesTheQueue()
    {
        var matcher = new Matcher();
        var runner = new ManualRunner();
        (CanvasPublicationSlot slot, _) = LoadedSlot("root");
        var machine = new CanvasFilterMachine(slot, matcher, runner.Enqueue);

        machine.Typed("a", active: true);
        CanvasRequestIdentity first = slot.Current.Filters.Running!;
        machine.Typed("ab", active: true);

        Assert.True(
            ReferenceEquals(slot.Current.Filters.Running, first)
                && slot.Current.Filters.Queued is not null
                && slot.Current.Unit!.Needle == "a"
                && runner.Pending == 1,
            "a keystroke while a job runs queues, starts nothing, and leaves the "
            + "pending unit where it was.");
        CanvasRequestIdentity queued = slot.Current.Filters.Queued!;

        machine.Typed("abc", active: true);

        Assert.True(
            ReferenceEquals(slot.Current.Filters.Running, first)
                && slot.Current.Filters.Queued is not null
                && !ReferenceEquals(slot.Current.Filters.Queued, queued)
                && runner.Pending == 1
                && slot.Current.NeedleIntent == "abc",
            "the third keystroke replaces the queued request — dropped before it "
            + "ever started — and still nothing new runs.");
    }

    /// <summary>U9's burst rule: ten keystrokes pay for at most two
    /// matches — the one already in flight and the last one standing —
    /// and the queued completion DISCARDS its answer and promotes.</summary>
    [Fact]
    public void ABurstOfTenKeystrokesPaysForAtMostTwoMatches()
    {
        var matcher = new Matcher { Answer = needle => needle == "n10" ? ["root"] : [] };
        var runner = new ManualRunner();
        (CanvasPublicationSlot slot, _) = LoadedSlot("root");
        var machine = new CanvasFilterMachine(slot, matcher, runner.Enqueue);

        for (var i = 1; i <= 10; i++)
        {
            machine.Typed($"n{i}", active: true);
        }

        runner.RunAll();

        Assert.True(
            matcher.Matches == 2 && matcher.Needles[0] == "n1" && matcher.Needles[1] == "n10",
            $"the burst paid for {matcher.Matches} matches ({string.Join(",", matcher.Needles)}); "
            + "U9 prices it at the one in flight plus the last one standing.");
        Assert.True(
            slot.Current.Unit!.Answer == CanvasAnswerState.Answered
                && slot.Current.Unit.Needle == "n10"
                && slot.Current.Filters.Running is null,
            "the surviving answer is the last needle's, and the machine idles — "
            + "n1's rows never rested under n10's needle.");
    }

    /// <summary>The clear: an inactive needle retires both entries and
    /// widens the unit to the whole population, with the selection
    /// re-resolved from the durable intent.</summary>
    [Fact]
    public void AnInactiveNeedleClearsTheMachineAndWidens()
    {
        var matcher = new Matcher { Answer = _ => ["a"] };
        var runner = new ManualRunner();
        (CanvasPublicationSlot slot, _) = LoadedSlot("root", "a");
        _ = slot.Publish(s => s.WithSelectedIntent("a"));
        var machine = new CanvasFilterMachine(slot, matcher, runner.Enqueue);
        machine.Typed("a", active: true);
        runner.RunAll();
        Assert.True(
            slot.Current.Unit!.Answer == CanvasAnswerState.Answered,
            "premise: an answer is on the books before the clear.");

        machine.Typed(" ", active: false);

        Assert.True(
            slot.Current.Filters.Running is null && slot.Current.Filters.Queued is null,
            "both entries retired.");
        Assert.True(
            slot.Current.Unit!.Answer == CanvasAnswerState.Unfiltered
                && slot.Current.Unit.VisibleCount == 2
                && slot.Current.Unit.ResolvedSelection == "a"
                && slot.Current.NeedleIntent == " ",
            "the unit widens to the whole population with the selection resolved "
            + "from the durable intent, and the intent records the raw needle.");
        Assert.True(runner.Pending == 0, "and no job runs for an inactive needle.");
    }

    /// <summary>A keystroke before any load records the intent only —
    /// U9's load column owns the rest, through the reseed.</summary>
    [Fact]
    public void AKeystrokeBeforeTheLoadRecordsTheIntentOnly()
    {
        var matcher = new Matcher();
        var runner = new ManualRunner();
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var machine = new CanvasFilterMachine(slot, matcher, runner.Enqueue);

        machine.Typed("q", active: true);

        Assert.True(
            slot.Current.NeedleIntent == "q"
                && slot.Current.Filters.Running is null
                && runner.Pending == 0,
            "no population, no request, no job — the intent alone, for the "
            + "acceptance's rebase to reseed from.");
    }

    // ---------------------------------------------------------------
    // The job: revalidation, teardown, the failed answer
    // ---------------------------------------------------------------

    /// <summary>U9's burst rule's other half: a job parked before its
    /// lock is abandoned BEFORE its FFI call once superseded — here by
    /// the clear — and publishes nothing.</summary>
    [Fact]
    public void AJobParkedBeforeItsLockIsAbandonedOnceSuperseded()
    {
        var matcher = new Matcher();
        using var parked = new ManualResetEventSlim(false);
        using var go = new ManualResetEventSlim(false);
        var probe = new CanvasFilterProbeForTests
        {
            OnPoint = point =>
            {
                if (point == CanvasFilterPoint.BeforeLock)
                {
                    parked.Set();
                    _ = go.Wait(Budget);
                }
            },
        };
        (CanvasPublicationSlot slot, _) = LoadedSlot("root");
        var workers = new List<CanvasWorker>();
        var machine = new CanvasFilterMachine(
            slot,
            matcher,
            body =>
            {
                var worker = new CanvasWorker(body);
                workers.Add(worker);
                worker.Start();
            },
            probe);

        machine.Typed("a", active: true);
        Assert.True(parked.Wait(Budget), "premise: the job parked before its lock.");

        machine.Typed(" ", active: false);
        go.Set();
        Assert.True(workers.TrueForAll(worker => worker.Join(Budget)), "premise: the job finished.");

        Assert.True(
            matcher.Matches == 0,
            $"the superseded job reached the FFI {matcher.Matches} times; the "
            + "lock-time revalidation abandons it first.");
        Assert.True(
            slot.Current.Unit!.Answer == CanvasAnswerState.Unfiltered,
            "and it published nothing over the clear.");
    }

    /// <summary>A completion arriving after teardown is dropped: the
    /// decision read refuses before the lock, and nothing publishes over
    /// the terminal record.</summary>
    [Fact]
    public void ACompletionAfterTeardownIsDropped()
    {
        var matcher = new Matcher { Answer = _ => ["root"] };
        var runner = new ManualRunner();
        var observer = new CanvasPublicationInstallObserver();
        (CanvasPublicationSlot slot, CanvasHandleLease lease) =
            LoadedSlot(observer, "root");
        var machine = new CanvasFilterMachine(slot, matcher, runner.Enqueue);
        machine.Typed("a", active: true);

        _ = slot.Publish(s => s.WithRetired());
        Assert.True(
            ReferenceEquals(CanvasLeaseTransfer.Terminalize(slot), lease) && lease.Close(),
            "premise: torn down, the lease closed by the terminal caller.");
        int installs = observer.Installs;

        runner.RunAll();

        Assert.True(
            matcher.Matches == 0 && observer.Installs == installs,
            $"the job matched {matcher.Matches} times and installed "
            + $"{observer.Installs - installs} publications after teardown; U9 "
            + "drops it before the FFI and publishes nothing.");
    }

    /// <summary>The failed-answer bit: a match that throws — panic-class
    /// included — becomes the unit's FAILED state, the rows it was
    /// showing kept, the machine idle again.</summary>
    [Fact]
    public void AFailedMatchKeepsTheRowsAndSaysFailed()
    {
        var matcher = new Matcher { Answer = _ => ["a"] };
        var runner = new ManualRunner();
        (CanvasPublicationSlot slot, _) = LoadedSlot("root", "a");
        var machine = new CanvasFilterMachine(slot, matcher, runner.Enqueue);
        machine.Typed("a", active: true);
        runner.RunAll();
        Assert.True(
            slot.Current.Unit!.VisibleCount == 1,
            "premise: a narrowed answer is on screen.");

        matcher.Fault = new InvalidOperationException("panic");
        machine.Typed("ab", active: true);
        runner.RunAll();

        CanvasProjectionUnit unit = slot.Current.Unit!;
        Assert.True(
            unit.Answer == CanvasAnswerState.Failed && unit.Needle == "ab",
            $"the unit reads {unit.Answer} for the needle that could not run.");
        Assert.True(
            unit.VisibleCount == 1 && unit.Matched.Contains("a"),
            "the rows it was showing linger — no silent widen on a fault.");
        Assert.True(
            slot.Current.Filters.Running is null,
            "and the machine is idle for the next keystroke.");
    }

    // ---------------------------------------------------------------
    // Completions racing keystrokes, and the reload window
    // ---------------------------------------------------------------

    /// <summary>A completion racing a keystroke, both orders: keystroke
    /// before the swap, the completion discards and promotes; keystroke
    /// after, a fresh start. Either way the surviving answer is the
    /// newest needle's.</summary>
    [Fact]
    public void ACompletionRacingAKeystrokeAnswersTheNewestNeedleInBothOrders()
    {
        var matcher = new Matcher { Answer = needle => needle == "b" ? ["root"] : [] };
        var runner = new ManualRunner();
        CanvasFilterMachine? machine = null;
        var typedInWindow = false;
        var probe = new CanvasFilterProbeForTests
        {
            OnPoint = point =>
            {
                if (point == CanvasFilterPoint.BeforeSwap && !typedInWindow)
                {
                    typedInWindow = true;
                    machine!.Typed("b", active: true);
                }
            },
        };
        (CanvasPublicationSlot slot, _) = LoadedSlot("root");
        machine = new CanvasFilterMachine(slot, matcher, runner.Enqueue, probe);

        machine.Typed("a", active: true);
        runner.RunAll();

        Assert.True(
            slot.Current.Unit!.Answer == CanvasAnswerState.Answered
                && slot.Current.Unit.Needle == "b"
                && slot.Current.Unit.FilteredOrder.SequenceEqual(["root"])
                && slot.Current.Filters.Running is null,
            "keystroke-before-swap: a's answer discarded, b promoted, matched and "
            + "answered — a's rows never rested under b's needle.");
        Assert.True(
            matcher.Needles.SequenceEqual(["a", "b"]),
            $"both jobs ran, in order ({string.Join(",", matcher.Needles)}).");

        // Completion first, keystroke after: the ordinary restart.
        machine.Typed("c", active: true);
        runner.RunAll();
        Assert.True(
            slot.Current.Unit!.Answer == CanvasAnswerState.Answered
                && slot.Current.Unit.Needle == "c",
            "completion-then-keystroke answers the new needle as a fresh start.");
    }

    /// <summary>The reload window: a completion whose document was
    /// un-named mid-flight publishes nothing — the acceptance's reseed
    /// owns the next machine.</summary>
    [Fact]
    public void ACompletionDuringTheReloadWindowPublishesNothing()
    {
        var matcher = new Matcher { Answer = _ => ["root"] };
        var runner = new ManualRunner();
        var observer = new CanvasPublicationInstallObserver();
        (CanvasPublicationSlot slot, _) = LoadedSlot(observer, "root");
        var machine = new CanvasFilterMachine(slot, matcher, runner.Enqueue);
        machine.Typed("a", active: true);

        // The reload worker's first publication, mid-flight.
        _ = slot.Publish(s => s.WithUnloaded());
        int installs = observer.Installs;

        runner.RunAll();

        Assert.True(
            matcher.Matches == 0 && observer.Installs == installs,
            $"the job matched {matcher.Matches} times and installed "
            + $"{observer.Installs - installs} publications into the reload "
            + "window; there is no lease to ask and nothing to publish over.");
    }

    // ---------------------------------------------------------------
    // U9's load column: the reseed's job
    // ---------------------------------------------------------------

    /// <summary>The needle typed before the load is answered after it:
    /// acceptance reseeds from the carried intent, the pipeline hands
    /// the minted request to the machine, and its job answers against
    /// the new population.</summary>
    [Fact]
    public void TheReseededRequestGetsItsJobAndAnswers()
    {
        var matcher = new Matcher { Answer = _ => ["kept"] };
        var runner = new ManualRunner();
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var machine = new CanvasFilterMachine(slot, matcher, runner.Enqueue);
        var pipeline = new CanvasLoadPipeline(
            slot,
            new ReloadSource(),
            onReseeded: machine.StartReseeded);

        machine.Typed("keep", active: true);
        Assert.True(
            pipeline.Deliver(pipeline.Request()!) == CanvasLoadOutcome.Accepted,
            "premise: the load lands.");

        Assert.True(
            slot.Current.Filters.Running is not null
                && slot.Current.Unit!.Answer == CanvasAnswerState.Pending
                && slot.Current.Unit.Needle == "keep"
                && runner.Pending == 1,
            "acceptance reseeded from the carried needle and the machine got its job.");

        runner.RunAll();

        Assert.True(
            slot.Current.Unit!.Answer == CanvasAnswerState.Answered
                && slot.Current.Unit.FilteredOrder.SequenceEqual(["kept"])
                && slot.Current.Filters.Running is null
                && matcher.Needles.SequenceEqual(["keep"]),
            "and the reseeded job answered against the new population.");
    }

    // ---------------------------------------------------------------
    // The mutations
    // ---------------------------------------------------------------

    /// <summary>U9's MUTATION: a completion that publishes its answer
    /// with a request queued — the replica omits the one cell — returns
    /// the named arrangement: R's rows resting under Q's needle.</summary>
    [Fact]
    public void MutationU9_PublishingOverTheQueueRestsOldRowsUnderTheNewNeedle()
    {
        var matcher = new Matcher { Answer = needle => needle == "a" ? ["root"] : [] };
        var runner = new ManualRunner();
        (CanvasPublicationSlot slot, _) = LoadedSlot("root", "b");
        var machine = new CanvasFilterMachine(slot, matcher, runner.Enqueue);
        machine.Typed("a", active: true);
        machine.Typed("bee", active: true);
        CanvasRequestIdentity running = slot.Current.Filters.Running!;

        // The replica: match, then publish with no queue check.
        string[] matched = matcher.Match(1, "a");
        CanvasPublication before = slot.Current;
        CanvasPublicationOutcome outcome = slot.Publish(s =>
            !ReferenceEquals(s.Filters.Running, running)
                ? null
                : s.WithFilters(s.Filters.Finished())
                    .WithUnit(s.Unit!.Answered(before.Population!, matched)));

        Assert.True(outcome.Installed, "premise: the replica published.");
        Assert.True(
            slot.Current.Unit!.Answer == CanvasAnswerState.Answered
                && slot.Current.Unit.Needle == "a"
                && slot.Current.NeedleIntent == "bee",
            "the named arrangement: a's rows and a's needle on screen while the "
            + "field says 'bee'. Production's completion DISCARDS and promotes, "
            + "and the burst fact pins that the machine cannot spell this.");
    }

    /// <summary>The revalidation MUTATION: a job that skips the
    /// lock-time re-check reaches the FFI for a request already
    /// superseded — the wasted match the burst rule prices out.</summary>
    [Fact]
    public void MutationU9_SkippingTheLockTimeRevalidationMatchesForADeadRequest()
    {
        var matcher = new Matcher();
        (CanvasPublicationSlot slot, _) = LoadedSlot("root");
        var machine = new CanvasFilterMachine(slot, matcher, _ => { });
        machine.Typed("a", active: true);
        machine.Typed(" ", active: false);
        CanvasHandleLease lease = slot.Current.Lease!;

        // The replica: admission answered without reading anything.
        _ = lease.Invoke(
            () => true,
            handle =>
            {
                _ = matcher.Match(handle, "a");
            });

        Assert.True(
            matcher.Matches == 1,
            "premise: the replica reached the FFI for a superseded request — the "
            + "arrangement the parked-job fact proves production cannot reach, "
            + "because its revalidation runs INSIDE the lock.");
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static (CanvasPublicationSlot Slot, CanvasHandleLease Lease) LoadedSlot(
        params string[] ids) => LoadedSlot(null, ids);

    private static (CanvasPublicationSlot Slot, CanvasHandleLease Lease) LoadedSlot(
        CanvasPublicationInstallObserver? observer, params string[] ids)
    {
        CanvasPublicationSlot slot = observer is null
            ? new CanvasPublicationSlot(CanvasPublication.Seed())
            : new CanvasPublicationSlot(CanvasPublication.Seed(), observer);
        var request = new CanvasRequestIdentity("L");
        _ = slot.Publish(s => s.WithLoads(s.Loads.Requested(request)));
        var lease = new CanvasHandleLease(1, _ => { });
        Assert.True(
            CanvasLeaseTransfer.TryAccept(slot, request, lease, Population(ids)).Accepted,
            "premise: the load lands.");
        return (slot, lease);
    }

    private static CanvasPopulation Population(params string[] nodeIds) => new(
        nodeIds.Select(id => new CanvasOutlineRow(
            id, 0, "text", id, id, [], 1, (uint)nodeIds.Length, 0, null)),
        null,
        null,
        0,
        null);

    /// <summary>An in-memory matcher: records the needles it answered,
    /// faults on demand.</summary>
    private sealed class Matcher : ICanvasFilterSource
    {
        private readonly object _gate = new();
        private readonly List<string> _needles = [];

        internal Func<string, string[]> Answer { get; set; } = _ => [];

        internal Exception? Fault { get; set; }

        internal int Matches
        {
            get
            {
                lock (_gate)
                {
                    return _needles.Count;
                }
            }
        }

        internal IReadOnlyList<string> Needles
        {
            get
            {
                lock (_gate)
                {
                    return [.. _needles];
                }
            }
        }

        public string[] Match(ulong handle, string needle)
        {
            if (Fault is { } fault)
            {
                throw fault;
            }

            lock (_gate)
            {
                _needles.Add(needle);
            }

            return Answer(needle);
        }
    }

    /// <summary>Jobs by hand: the machine enqueues, the fact decides
    /// when they run — which is what makes a queue observable.</summary>
    private sealed class ManualRunner
    {
        private readonly Queue<Action> _jobs = new();

        internal int Pending => _jobs.Count;

        internal void Enqueue(Action job) => _jobs.Enqueue(job);

        internal void RunAll()
        {
            while (_jobs.Count > 0)
            {
                _jobs.Dequeue()();
            }
        }
    }

    /// <summary>The smallest load source the reseed fact needs.</summary>
    private sealed class ReloadSource : ICanvasLoadSource
    {
        public CanvasOpenInfo Open() => new(1, 2, 0, false, []);

        public void Close(ulong handle)
        {
        }

        public CanvasOutlineRow[] Outline(ulong handle) =>
        [
            new("kept", 0, "text", "kept", "kept", [], 1, 2, 0, null),
            new("other", 0, "text", "other", "other", [], 2, 2, 0, null),
        ];

        public CanvasTableRow[] TableRows(ulong handle) => [];

        public CanvasScene Scene(ulong handle) => new([], []);

        public CanvasLoadFailure FailureFor(Exception exception) =>
            new(CanvasLoadState.Failed, exception.Message);

        public CanvasLoadFailure ParseError(IReadOnlyList<CanvasLoadWarning> warnings) =>
            new(CanvasLoadState.ParseError, null);
    }
}
