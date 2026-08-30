// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 PR C-unit, task T3: the load pipeline — U4's operation, driven
/// against an in-memory source that faults and blocks on demand.
/// </summary>
/// <remarks>
/// The frozen verification plan's delivery facts, each by the
/// instrument it names: the close OBSERVATION for native release,
/// injected barriers for interleavings, injected faults for the
/// ownership window. Every fact is deterministic — a barrier lands the
/// interleaving it names rather than hoping for it — including round
/// 6's arrangement, which runs on real threads but is barriered so both
/// workers hold an open handle before either reaches the gate.
/// </remarks>
public sealed class CanvasLoadPipelineTests
{
    private static readonly TimeSpan LivenessBudget = TimeSpan.FromSeconds(30);

    // ---------------------------------------------------------------
    // The request
    // ---------------------------------------------------------------

    /// <summary>The request publishes Loading and KEEPS the chain: the
    /// old lease stays named — and therefore owned — until the worker's
    /// own first publication un-names it. A worker the scheduler never
    /// runs then leaves the lease for teardown, which is the leak the
    /// T3 review found in a request-side un-name.</summary>
    [Fact]
    public void TheOldLeaseStaysOwnedBetweenTheRequestAndItsWorker()
    {
        var source = new FakeSource();
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var pipeline = new CanvasLoadPipeline(slot, source);
        Assert.True(
            pipeline.Deliver(pipeline.Request()!) == CanvasLoadOutcome.Accepted,
            "premise: the first load lands.");
        CanvasHandleLease live = slot.Current.Lease!;

        CanvasLoadRequest reload = pipeline.Request()!;

        Assert.True(
            slot.Current.Names(live)
                && !live.IsClosed
                && slot.Current.LoadState == CanvasLoadState.Loading
                && !slot.Current.Retired,
            "the request publishes Loading and keeps the old lease NAMED: un-named "
            + "here, a dropped worker would leave a handle nobody can reach.");

        // The worker never runs — a shutdown landed instead. The lease
        // is still named, so the terminal publication owns its close.
        _ = slot.Publish(s => s.WithRetired());
        CanvasHandleLease? unnamed = CanvasLeaseTransfer.Terminalize(slot);
        Assert.True(
            ReferenceEquals(unnamed, live) && unnamed!.Close() && source.TotalCloses == 1,
            $"teardown closed the requested-but-undelivered document's lease "
            + $"{source.TotalCloses} times; the request left it reachable.");

        // And the worker, arriving late, opens nothing.
        Assert.True(
            pipeline.Deliver(reload) == CanvasLoadOutcome.Refused && source.Opens == 1,
            "the late worker refused at its un-name and opened nothing.");
    }

    /// <summary>A displaced close that throws — panic-class included —
    /// maps to the failure state instead of faulting the tracked body:
    /// the attempt is on the record, nothing opens, and the request is
    /// terminal.</summary>
    [Fact]
    public void ADisplacedCloseThatThrowsPublishesTheFailure()
    {
        var source = new FakeSource();
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var pipeline = new CanvasLoadPipeline(slot, source);
        Assert.True(
            pipeline.Deliver(pipeline.Request()!) == CanvasLoadOutcome.Accepted,
            "premise: the first load lands.");
        source.CloseFault = new InvalidOperationException("panic");

        CanvasLoadOutcome outcome = pipeline.Deliver(pipeline.Request()!);

        Assert.True(outcome == CanvasLoadOutcome.Faulted, $"the reload reported {outcome}.");
        Assert.True(
            source.TotalCloses == 1 && source.Opens == 1,
            $"one close attempt is on the record ({source.TotalCloses}) and nothing "
            + $"new opened ({source.Opens}).");
        Assert.True(
            slot.Current.LoadState == CanvasLoadState.Failed
                && slot.Current.Loads.Delivery == CanvasLoadDelivery.Released
                && slot.Current.Loaded is null,
            "the failure and the terminal state published together.");
    }

    /// <summary>A retired document requests nothing, and publishes
    /// nothing for the refusal.</summary>
    [Fact]
    public void ARetiredDocumentRefusesTheRequest()
    {
        var source = new FakeSource();
        var observer = new CanvasPublicationInstallObserver();
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed(), observer);
        var pipeline = new CanvasLoadPipeline(slot, source);
        _ = slot.Publish(s => s.WithRetired());
        int installs = observer.Installs;

        Assert.True(pipeline.Request() is null, "a retired document requested a load.");
        Assert.True(observer.Installs == installs, "and the refusal published nothing.");
    }

    // ---------------------------------------------------------------
    // The delivery
    // ---------------------------------------------------------------

    /// <summary>The accepted branch: the swap installs lease, population
    /// and unit, marks the request consumed, and writes Ready — U4's
    /// "the load state the acceptance itself writes".</summary>
    [Fact]
    public void AFirstLoadInstallsTheLeaseThePopulationAndReady()
    {
        var source = new FakeSource { Rows = ["a", "b", "c"] };
        source.Subpaths["b"] = "#Heading";
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var pipeline = new CanvasLoadPipeline(slot, source);

        CanvasLoadOutcome outcome = pipeline.Deliver(pipeline.Request()!);

        CanvasPublication published = slot.Current;
        Assert.True(outcome == CanvasLoadOutcome.Accepted, $"the delivery reported {outcome}.");
        Assert.True(
            published.Lease is { IsClosed: false }
                && published.Population!.Count == 3
                && published.Population.Subpath("b") == "#Heading"
                && published.Unit!.VisibleCount == 3,
            "the publication names an open lease, the population — subpaths "
            + "included — and its unfiltered unit.");
        Assert.True(
            published.LoadState == CanvasLoadState.Ready && published.LoadMessage is null,
            $"the acceptance writes the load state: {published.LoadState}.");
        Assert.True(
            published.Loads.Consumed && source.TotalCloses == 0,
            "the request is consumed and nothing was closed.");
    }

    /// <summary>One native handle at a time: the worker closes the
    /// displaced lease BEFORE it opens the new one, and exactly once.</summary>
    [Fact]
    public void AReloadClosesTheOldHandleBeforeItOpensTheNew()
    {
        var source = new FakeSource();
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var pipeline = new CanvasLoadPipeline(slot, source);
        Assert.True(
            pipeline.Deliver(pipeline.Request()!) == CanvasLoadOutcome.Accepted,
            "premise: the first load lands.");
        CanvasHandleLease old = slot.Current.Lease!;
        source.Rows = ["x"];

        CanvasLoadOutcome outcome = pipeline.Deliver(pipeline.Request()!);

        Assert.True(outcome == CanvasLoadOutcome.Accepted, $"the reload reported {outcome}.");
        Assert.True(
            old.IsClosed && source.TotalCloses == 1,
            $"the displaced lease closed {source.TotalCloses} times.");
        Assert.True(
            source.IndexOf("close", 0) < source.IndexOf("open", 1),
            $"the old handle closed AFTER the new one opened ({source.Trace}); one "
            + "handle at a time is the inherited load's rule and the frozen "
            + "transition's order.");
        Assert.True(
            slot.Current.Lease is { IsClosed: false } fresh
                && !ReferenceEquals(fresh, old)
                && slot.Current.Population!.Outline[0].NodeId == "x",
            "the publication names the new lease and the new rows.");
    }

    /// <summary>A degraded open is read-only by construction: ParseError
    /// and its message publish with the request's terminal state, and
    /// the handle closes at once.</summary>
    [Fact]
    public void ADegradedOpenPublishesParseErrorAndClosesAtOnce()
    {
        var source = new FakeSource { Degraded = true };
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var pipeline = new CanvasLoadPipeline(slot, source);
        CanvasLoadRequest request = pipeline.Request()!;

        CanvasLoadOutcome outcome = pipeline.Deliver(request);

        Assert.True(outcome == CanvasLoadOutcome.ParseError, $"the delivery reported {outcome}.");
        Assert.True(
            slot.Current.LoadState == CanvasLoadState.ParseError
                && slot.Current.LoadMessage == FakeSource.ParseErrorMessage
                && slot.Current.Loaded is null,
            "ParseError and its message are published with nothing loaded.");
        Assert.True(
            source.Opens == 1 && source.TotalCloses == 1,
            $"the handle opened {source.Opens} times and closed {source.TotalCloses}.");
        Assert.True(
            ReferenceEquals(slot.Current.Loads.Latest, request.Identity)
                && slot.Current.Loads.Delivery == CanvasLoadDelivery.Released,
            "and the request reached its terminal state in the same publication.");
    }

    /// <summary>An open that throws held no handle: the mapped failure
    /// publishes with the terminal state, and there is nothing to
    /// close.</summary>
    [Fact]
    public void AnOpenThatThrowsPublishesTheMappedFailureWithNothingToClose()
    {
        var source = new FakeSource { OpenFault = new InvalidOperationException("no such file") };
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var pipeline = new CanvasLoadPipeline(slot, source);
        CanvasLoadRequest request = pipeline.Request()!;

        CanvasLoadOutcome outcome = pipeline.Deliver(request);

        Assert.True(outcome == CanvasLoadOutcome.Faulted, $"the delivery reported {outcome}.");
        Assert.True(
            slot.Current.LoadState == CanvasLoadState.Failed
                && slot.Current.LoadMessage == "failed: no such file"
                && slot.Current.Loaded is null
                && slot.Current.Loads.Delivery == CanvasLoadDelivery.Released,
            "the host's mapped failure is published with the terminal state.");
        Assert.True(source.TotalCloses == 0, "nothing was opened, so nothing closed.");
    }

    /// <summary>A read that throws mid-build: the handle closes exactly
    /// once and the failure publishes.</summary>
    [Fact]
    public void AReadThatThrowsClosesTheHandleOnceAndPublishesFailure()
    {
        var source = new FakeSource { ReadFault = new InvalidOperationException("torn") };
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var pipeline = new CanvasLoadPipeline(slot, source);

        CanvasLoadOutcome outcome = pipeline.Deliver(pipeline.Request()!);

        Assert.True(outcome == CanvasLoadOutcome.Faulted, $"the delivery reported {outcome}.");
        Assert.True(
            source.TotalCloses == 1
                && slot.Current.Loaded is null
                && slot.Current.LoadState == CanvasLoadState.Failed,
            $"the close observation reads {source.TotalCloses}; the lease was the "
            + "delivery's own and the finally released it.");
    }

    /// <summary>A delivery superseded after its reads: refused at the
    /// gate, its own handle closed, the newer request untouched.</summary>
    [Fact]
    public void ADeliverySupersededBeforeItsSwapIsRefusedAndClosesOnlyItsOwnHandle()
    {
        var source = new FakeSource();
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var probe = new CanvasLoadProbeForTests();
        var pipeline = new CanvasLoadPipeline(slot, source, probeForTests: probe);
        CanvasLoadRequest first = pipeline.Request()!;
        CanvasLoadRequest? second = null;
        probe.OnPoint = point =>
        {
            if (point == CanvasLoadPoint.Built && second is null)
            {
                second = pipeline.Request();
            }
        };

        CanvasLoadOutcome outcome = pipeline.Deliver(first);

        Assert.True(outcome == CanvasLoadOutcome.Refused, $"the stale delivery reported {outcome}.");
        Assert.True(
            source.TotalCloses == 1 && slot.Current.Loaded is null,
            "the superseded delivery closed the handle it opened and installed nothing.");
        Assert.True(
            second is not null
                && ReferenceEquals(slot.Current.Loads.Latest, second.Identity)
                && slot.Current.Loads.Admits(second.Identity)
                && slot.Current.LoadState == CanvasLoadState.Loading,
            "the newer request is untouched: still latest, still pending, still Loading.");
        probe.OnPoint = null;
        Assert.True(
            pipeline.Deliver(second!) == CanvasLoadOutcome.Accepted && source.TotalCloses == 1,
            "and the newer delivery lands without another close.");
    }

    // ---------------------------------------------------------------
    // The ownership window, under injected faults
    // ---------------------------------------------------------------

    /// <summary>The frozen plan's fault-injection facts, one per point
    /// inside the window: the close observation reads exactly one, the
    /// lease is unpublished, and the failure and the terminal state are
    /// published together — obligation I1 under a real fault.</summary>
    [Theory]
    [InlineData("SnapshotRead")]
    [InlineData("Rebase")]
    [InlineData("Reseed")]
    [InlineData("BeforeSwap")]
    public void AFaultInsideTheWindowClosesTheHandleOnceAndPublishesFailure(string pointName)
    {
        CanvasLoadPoint point = Enum.Parse<CanvasLoadPoint>(pointName);
        var source = new FakeSource();
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var probe = new CanvasLoadProbeForTests
        {
            OnPoint = reached =>
            {
                if (reached == point)
                {
                    throw new InvalidOperationException($"fault at {point}");
                }
            },
        };
        var pipeline = new CanvasLoadPipeline(slot, source, probeForTests: probe);
        // A needle, so the reseed point has work to do.
        _ = slot.Publish(s => s.WithNeedleIntent("q"));
        CanvasLoadRequest request = pipeline.Request()!;

        CanvasLoadOutcome outcome = pipeline.Deliver(request);

        Assert.True(outcome == CanvasLoadOutcome.Faulted, $"the delivery reported {outcome}.");
        Assert.True(
            source.TotalCloses == 1,
            $"the close observation reads {source.TotalCloses} after a fault at {point}; "
            + "the frozen plan says exactly one.");
        Assert.True(
            slot.Current.Loaded is null
                && slot.Current.LoadState == CanvasLoadState.Failed
                && slot.Current.LoadMessage == $"failed: fault at {point}",
            "the lease never reached a publication, and the failure did.");
        Assert.True(
            ReferenceEquals(slot.Current.Loads.Latest, request.Identity)
                && slot.Current.Loads.Delivery == CanvasLoadDelivery.Released,
            "the request is terminal, so no acceptance can follow the close.");
    }

    /// <summary>The fifth point: a fault AFTER the swap is an effect
    /// fault. The lease stays live and published, nothing closes.</summary>
    [Fact]
    public void AFaultAfterTheSwapLeavesTheLeaseLiveAndPublished()
    {
        var source = new FakeSource();
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var probe = new CanvasLoadProbeForTests
        {
            OnPoint = reached =>
            {
                if (reached == CanvasLoadPoint.AfterSwap)
                {
                    throw new InvalidOperationException("after");
                }
            },
        };
        var pipeline = new CanvasLoadPipeline(slot, source, probeForTests: probe);

        CanvasLoadOutcome outcome = pipeline.Deliver(pipeline.Request()!);

        Assert.True(
            outcome == CanvasLoadOutcome.FaultedAfterAccept,
            $"the delivery reported {outcome}.");
        Assert.True(
            source.TotalCloses == 0
                && slot.Current.Lease is { IsClosed: false }
                && slot.Current.LoadState == CanvasLoadState.Ready
                && slot.Current.Loads.Consumed,
            "the swap installed and the release found its lease named: the close "
            + "observation stays at zero and the publication is Ready.");
    }

    /// <summary>
    /// Round 6's breaking arrangement, as a fact: two workers each hold
    /// an open handle before either reaches the gate. Exactly one
    /// publication survives and exactly one lease closes, whichever
    /// worker publishes first.
    /// </summary>
    [Fact]
    public void TwoConcurrentDeliveriesLeaveOnePublicationAndOneClosedLease()
    {
        var source = new FakeSource();
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        using var firstBuilt = new ManualResetEventSlim(false);
        using var secondBuilt = new ManualResetEventSlim(false);
        using var go = new ManualResetEventSlim(false);
        var arrivals = 0;
        var probe = new CanvasLoadProbeForTests
        {
            OnPoint = point =>
            {
                if (point == CanvasLoadPoint.Built)
                {
                    (Interlocked.Increment(ref arrivals) == 1 ? firstBuilt : secondBuilt).Set();
                    _ = go.Wait(LivenessBudget);
                }
            },
        };
        var pipeline = new CanvasLoadPipeline(slot, source, probeForTests: probe);

        CanvasLoadRequest first = pipeline.Request()!;
        CanvasLoadOutcome firstOutcome = CanvasLoadOutcome.Refused;
        var firstWorker = new CanvasWorker(() => firstOutcome = pipeline.Deliver(first));
        firstWorker.Start();
        Assert.True(firstBuilt.Wait(LivenessBudget), "premise: the first worker built.");

        // Superseded while the first worker holds an open handle.
        CanvasLoadRequest second = pipeline.Request()!;
        CanvasLoadOutcome secondOutcome = CanvasLoadOutcome.Refused;
        var secondWorker = new CanvasWorker(() => secondOutcome = pipeline.Deliver(second));
        secondWorker.Start();
        Assert.True(secondBuilt.Wait(LivenessBudget), "premise: the second worker built.");
        Assert.True(
            source.Opens == 2 && source.TotalCloses == 0,
            "premise: two handles open at once, neither published.");

        go.Set();
        Assert.True(
            firstWorker.Join(LivenessBudget) && secondWorker.Join(LivenessBudget),
            "premise: a worker did not finish.");

        Assert.True(
            firstOutcome == CanvasLoadOutcome.Refused
                && secondOutcome == CanvasLoadOutcome.Accepted,
            $"the outcomes were {firstOutcome} and {secondOutcome}; only the latest "
            + "request may land, whichever worker reaches the gate first.");
        Assert.True(
            source.TotalCloses == 1 && slot.Current.Lease is { IsClosed: false },
            $"the close observation totals {source.TotalCloses}: exactly one lease "
            + "closed, and the published one is open.");
    }

    // ---------------------------------------------------------------
    // Teardown, intents, and the instrument
    // ---------------------------------------------------------------

    /// <summary>U12's walk: a delivery racing teardown is safe in both
    /// orders. Teardown first, the delivery reads retired and closes its
    /// own handle; delivery first, the terminal publication un-names
    /// the lease and its caller closes it — after publishing.</summary>
    [Fact]
    public void ADeliveryRacingTeardownIsSafeInBothOrders()
    {
        // Teardown lands between the build and the swap.
        var source = new FakeSource();
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var probe = new CanvasLoadProbeForTests
        {
            OnPoint = point =>
            {
                if (point == CanvasLoadPoint.Built)
                {
                    _ = slot.Publish(s => s.WithRetired());
                    Assert.True(
                        CanvasLeaseTransfer.Terminalize(slot) is null,
                        "premise: nothing was published to un-name.");
                }
            },
        };
        var pipeline = new CanvasLoadPipeline(slot, source, probeForTests: probe);

        CanvasLoadOutcome outcome = pipeline.Deliver(pipeline.Request()!);

        Assert.True(outcome == CanvasLoadOutcome.Refused, $"the delivery reported {outcome}.");
        Assert.True(
            source.TotalCloses == 1 && slot.Current.Retired && slot.Current.Loaded is null,
            "the delivery read retired at its decision, refused, and closed the handle "
            + "it opened; the terminal publication stands.");

        // The delivery lands first; teardown un-names, then closes.
        var source2 = new FakeSource();
        var slot2 = new CanvasPublicationSlot(CanvasPublication.Seed());
        var pipeline2 = new CanvasLoadPipeline(slot2, source2);
        Assert.True(
            pipeline2.Deliver(pipeline2.Request()!) == CanvasLoadOutcome.Accepted,
            "premise: the load lands.");
        CanvasHandleLease lease = slot2.Current.Lease!;

        _ = slot2.Publish(s => s.WithRetired());
        Assert.True(
            slot2.Current.Names(lease) && !lease.IsClosed,
            "the PRETERMINAL publication retains the lease: the retired interval is "
            + "a model state, and nothing has closed.");
        CanvasHandleLease? unnamed = CanvasLeaseTransfer.Terminalize(slot2);
        Assert.True(
            ReferenceEquals(unnamed, lease) && slot2.Current.Loaded is null && !lease.IsClosed,
            "the terminal publication hands back the lease it un-named, still open — "
            + "the close FOLLOWS the publication.");
        Assert.True(
            unnamed!.Close() && source2.TotalCloses == 1,
            "and the caller closes it exactly once.");
    }

    /// <summary>The rebase barriers: intents arriving mid-load survive
    /// acceptance — selection resolved against the new graph, marks
    /// carried, the surface carried, the needle seeding the new
    /// machine.</summary>
    [Fact]
    public void IntentsArrivingMidLoadSurviveAcceptance()
    {
        var source = new FakeSource { Rows = ["a", "b"] };
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var probe = new CanvasLoadProbeForTests
        {
            OnPoint = point =>
            {
                if (point == CanvasLoadPoint.Built)
                {
                    _ = slot.Publish(s => s.WithSelectedIntent("b"));
                    _ = slot.Publish(s => s.WithMarkedIntent(["a"]));
                    _ = slot.Publish(s => s.WithActiveSurface(CanvasSurfaceKind.Table));
                    _ = slot.Publish(s => s.WithNeedleIntent("q"));
                }
            },
        };
        var pipeline = new CanvasLoadPipeline(slot, source, probeForTests: probe);

        Assert.True(
            pipeline.Deliver(pipeline.Request()!) == CanvasLoadOutcome.Accepted,
            "premise: the load lands.");

        CanvasPublication published = slot.Current;
        Assert.True(
            published.Unit!.ResolvedSelection == "b",
            $"the selection intent arriving mid-load resolved to "
            + $"{published.Unit.ResolvedSelection}; a load carrying A must not roll a "
            + "later selection of B backwards.");
        Assert.True(
            published.MarkedIntent.Contains("a")
                && published.ActiveSurface == CanvasSurfaceKind.Table,
            "marks and the surface switch are carried, never defaulted back.");
        Assert.True(
            published.Filters.Running is not null
                && published.Unit.Answer == CanvasAnswerState.Pending
                && published.Unit.Needle == "q",
            "and the needle typed mid-load seeds the new machine.");
    }

    /// <summary>A close that throws inside the release — the session
    /// died first — is contained: the delivery reports its own outcome
    /// and the record shows the attempt.</summary>
    [Fact]
    public void ACloseFaultInTheReleaseDoesNotReplaceTheDeliveryOutcome()
    {
        var source = new FakeSource { CloseFault = new ObjectDisposedException("session") };
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var probe = new CanvasLoadProbeForTests();
        var pipeline = new CanvasLoadPipeline(slot, source, probeForTests: probe);
        CanvasLoadRequest first = pipeline.Request()!;
        probe.OnPoint = point =>
        {
            if (point == CanvasLoadPoint.Built)
            {
                _ = pipeline.Request();
            }
        };

        CanvasLoadOutcome outcome = pipeline.Deliver(first);

        Assert.True(
            outcome == CanvasLoadOutcome.Refused && source.TotalCloses == 1,
            $"the superseded delivery reported {outcome} with {source.TotalCloses} close "
            + "attempts; the close fault was the session's, not the delivery's.");
    }

    /// <summary>RETENTION's mutation, by the right instrument: a replica
    /// delivery that drops its lease on refusal instead of releasing in
    /// a finally leaks a native handle a weak reference would never see
    /// — and the close observation does.</summary>
    [Fact]
    public void MutationRetention_ALeaseDroppedWithoutReleaseIsCaughtByTheCloseObservation()
    {
        var source = new FakeSource();
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        var pipeline = new CanvasLoadPipeline(slot, source);
        CanvasLoadRequest first = pipeline.Request()!;
        _ = pipeline.Request();

        // The mutant: open, build, try, and simply return on refusal.
        CanvasOpenInfo info = source.Open();
        var dropped = new CanvasHandleLease(info.Handle, source.Close);
        var population = new CanvasPopulation(source.Outline(info.Handle), null, null, 0, null);
        Assert.False(
            CanvasLeaseTransfer.TryAccept(slot, first.Identity, dropped, population).Accepted,
            "premise: the mutant's request is superseded, so it is refused.");

        Assert.True(
            source.TotalCloses == 0 && !dropped.IsClosed,
            "the named arrangement: an unpublished handle nobody closed. Visible to "
            + "the close observation and to nothing else — the wrapper is "
            + "collectable while the native handle is not.");
    }

    // ---------------------------------------------------------------
    // The source
    // ---------------------------------------------------------------

    /// <summary>An in-memory source: handles are integers, the close
    /// observation is a counter, and each FFI call can be told to throw.
    /// The order of calls is logged so a fact can assert "closed before
    /// opened" rather than only "closed".</summary>
    private sealed class FakeSource : ICanvasLoadSource
    {
        internal const string ParseErrorMessage = "not a canvas";
        private readonly object _gate = new();
        private readonly List<string> _log = [];
        private ulong _next;
        private int _opens;
        private int _closes;

        internal string[] Rows { get; set; } = ["a", "b"];

        internal Dictionary<string, string> Subpaths { get; } = new(StringComparer.Ordinal);

        internal bool Degraded { get; init; }

        internal Exception? OpenFault { get; init; }

        internal Exception? ReadFault { get; init; }

        internal Exception? CloseFault { get; set; }

        internal int Opens => Volatile.Read(ref _opens);

        internal int TotalCloses => Volatile.Read(ref _closes);

        internal string Trace
        {
            get
            {
                lock (_gate)
                {
                    return string.Join(" ", _log);
                }
            }
        }

        internal int IndexOf(string op, int occurrence)
        {
            lock (_gate)
            {
                var seen = 0;
                for (var i = 0; i < _log.Count; i++)
                {
                    if (_log[i].StartsWith(op, StringComparison.Ordinal) && seen++ == occurrence)
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        public CanvasOpenInfo Open()
        {
            if (OpenFault is { } fault)
            {
                throw fault;
            }

            ulong handle = Interlocked.Increment(ref _next);
            _ = Interlocked.Increment(ref _opens);
            Record($"open:{handle}");
            return new CanvasOpenInfo(handle, (uint)Rows.Length, 0, Degraded, []);
        }

        public void Close(ulong handle)
        {
            _ = Interlocked.Increment(ref _closes);
            Record($"close:{handle}");
            if (CloseFault is { } fault)
            {
                throw fault;
            }
        }

        public CanvasOutlineRow[] Outline(ulong handle)
        {
            if (ReadFault is { } fault)
            {
                throw fault;
            }

            Record($"outline:{handle}");
            return [.. Rows.Select((id, i) => new CanvasOutlineRow(
                id, 0, "text", id, id, [], (uint)(i + 1), (uint)Rows.Length, 0, null))];
        }

        public CanvasTableRow[] TableRows(ulong handle) =>
            [.. Rows.Select(id => new CanvasTableRow(id, "text", id, id, [], string.Empty, 0, null))];

        public CanvasScene Scene(ulong handle) =>
            new(
                [.. Subpaths.Select(pair => new CanvasSceneNode(
                    pair.Key, "file", pair.Key, pair.Key, 0, 0, 0, 0, null, null, pair.Value))],
                []);

        public CanvasLoadFailure FailureFor(Exception exception) =>
            new(CanvasLoadState.Failed, $"failed: {exception.Message}");

        public CanvasLoadFailure ParseError(IReadOnlyList<CanvasLoadWarning> warnings) =>
            new(CanvasLoadState.ParseError, ParseErrorMessage);

        private void Record(string entry)
        {
            lock (_gate)
            {
                _log.Add(entry);
            }
        }
    }
}
