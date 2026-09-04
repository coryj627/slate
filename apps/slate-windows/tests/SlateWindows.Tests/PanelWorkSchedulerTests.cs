// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using SlateWindows.Panels;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR A (contracts A-2, AD-4; the round-4 ledger's IGA-42, IGA-53,
/// IGA-60): the always-async scheduling primitive — compute on the pool
/// in EVERY mode, apply on the captured owner context, the tracked task
/// complete after the apply, and a fixed-point drain that follows work
/// an apply enqueued.
/// </summary>
public sealed class PanelWorkSchedulerTests
{
    /// <summary>A scheduler that exposes the primitive and records
    /// where each half ran.</summary>
    private sealed class Probe : PanelWorkScheduler
    {
        public Probe(bool synchronousForTests)
            : base(synchronousForTests)
        {
        }

        /// <summary>Over an explicit owner context — the hostile contexts
        /// of IPD-1 (one whose Post runs the callback inline, one whose
        /// Post refuses).</summary>
        public Probe(SynchronizationContext ownerContext)
            : base(synchronousForTests: false, ownerContext)
        {
        }

        public int? ComputeThread { get; private set; }
        public int? ApplyThread { get; private set; }
        public int Applies { get; private set; }
        public bool HasContext => HasUiContext;

        public void Run(Action? afterApply = null, TimeSpan? computeDelay = null, Action? beforeComputeReturns = null)
        {
            StartWorkAlwaysAsync(
                () =>
                {
                    if (computeDelay is { } delay)
                    {
                        Thread.Sleep(delay);
                    }
                    ComputeThread = Environment.CurrentManagedThreadId;
                    beforeComputeReturns?.Invoke();
                    return 42;
                },
                value =>
                {
                    ApplyThread = Environment.CurrentManagedThreadId;
                    Applies += value == 42 ? 1 : 0;
                    afterApply?.Invoke();
                });
        }

        public Task DrainAll() => WhenAllWorkDrained();

        public Task DrainOnce() => WhenWorkDrained();
    }

    /// <summary>The canvas's pumped harness (CanvasPresentationEngineTests.WithPumpedContext):
    /// a DispatcherSynchronizationContext on THIS thread, pumped by
    /// DispatcherFrames until the condition holds.</summary>
    private static void WithPumpedContext(Action<Func<Func<bool>, bool>> body)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try
        {
            body(PumpUntil);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static bool PumpUntil(Func<bool> condition)
    {
        var budget = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && budget.Elapsed < TimeSpan.FromSeconds(10))
        {
            var frame = new DispatcherFrame();
            _ = Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
            Thread.Yield();
        }
        return condition();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheComputeNeverRunsOnTheCallingThread(bool synchronousForTests)
    {
        WithPumpedContext(pump =>
        {
            var probe = new Probe(synchronousForTests);
            int caller = Environment.CurrentManagedThreadId;
            probe.Run();
            Assert.True(pump(() => probe.Applies == 1), "the apply landed");
            Assert.NotNull(probe.ComputeThread);
            Assert.NotEqual(caller, probe.ComputeThread);
        });
    }

    [Fact]
    public void TheApplyRunsOnTheCapturedContextsThread()
    {
        WithPumpedContext(pump =>
        {
            var probe = new Probe(synchronousForTests: false);
            Assert.True(probe.HasContext);
            int owner = Environment.CurrentManagedThreadId;
            probe.Run();
            Assert.True(pump(() => probe.Applies == 1), "the apply landed");
            Assert.Equal(owner, probe.ApplyThread);
        });
    }

    [Fact]
    public async Task TheTrackedTaskCompletesAfterTheApply()
    {
        // No context captured on a pool thread: the apply runs where the
        // compute finished, and the drain still returns only after it.
        Probe? probe = null;
        await Task.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(null);
            probe = new Probe(synchronousForTests: true);
        });
        Assert.NotNull(probe);
        Assert.False(probe.HasContext);
        probe.Run();
        await probe.DrainOnce();
        Assert.Equal(1, probe.Applies);
        Assert.NotNull(probe.ApplyThread);
    }

    [Fact]
    public async Task TheFixedPointDrainFollowsWorkAnApplyEnqueued()
    {
        Probe? probe = null;
        await Task.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(null);
            probe = new Probe(synchronousForTests: true);
        });
        Assert.NotNull(probe);
        // The first apply enqueues a second body — the silent replacement
        // pair of A-2 — after any single snapshot of the tracked set.
        bool chained = false;
        // Both bodies slow on purpose: the drain starts while the FIRST is
        // still computing, so a one-shot drain snapshots only that task and
        // returns while the chained body is still computing (the sweep's
        // `drain-not-fixed-point`).
        probe.Run(
            afterApply: () =>
            {
                if (!chained)
                {
                    chained = true;
                    probe.Run(computeDelay: TimeSpan.FromMilliseconds(400));
                }
            },
            computeDelay: TimeSpan.FromMilliseconds(300));
        await probe.DrainAll();
        Assert.Equal(2, probe.Applies);
    }

    [Fact]
    public async Task ShutdownStopsBothHalves()
    {
        Probe? probe = null;
        await Task.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(null);
            probe = new Probe(synchronousForTests: true);
        });
        Assert.NotNull(probe);
        probe.Shutdown();
        probe.Run();
        await probe.DrainAll();
        Assert.Equal(0, probe.Applies);
        Assert.Null(probe.ComputeThread);
    }

    /// <summary>IPA-5: a teardown that lands between queueing the body and
    /// the pool picking it up — the window StartWork's RunIfLive closes —
    /// refuses the COMPUTE where it would start, and applies nothing.</summary>
    [Fact]
    public void AShutdownBetweenQueueingAndThePoolPickupRefusesTheCompute()
    {
        WithPumpedContext(pump =>
        {
            var probe = new Probe(synchronousForTests: false);
            probe.BeforeComputeForTests = probe.Shutdown;
            probe.Run();
            // ONE drain, pumped to completion and OBSERVED (IPB-8): a
            // faulted drain is complete too.
            Task drain = probe.DrainAll();
            Assert.True(pump(() => drain.IsCompleted), "the tracked task completed");
            drain.GetAwaiter().GetResult();
            Assert.Null(probe.ComputeThread);
            Assert.Equal(0, probe.Applies);
        });
    }

    /// <summary>IPA-5: a teardown after the compute finished and before the
    /// apply was dispatched skips the apply — the tracked task still
    /// completes, so the drain returns.</summary>
    [Fact]
    public void AShutdownAfterTheComputeAndBeforeTheApplySkipsTheApply()
    {
        WithPumpedContext(pump =>
        {
            var probe = new Probe(synchronousForTests: false);
            probe.Run(beforeComputeReturns: probe.Shutdown);
            Task drain = probe.DrainAll();
            Assert.True(pump(() => drain.IsCompleted), "the tracked task completed");
            drain.GetAwaiter().GetResult();
            Assert.NotNull(probe.ComputeThread);
            Assert.Equal(0, probe.Applies);
        });
    }

    /// <summary>IPB-3: an apply posted to the owner context and not yet
    /// run is SETTLED by the shutdown, so a teardown that blocks the
    /// owner's thread while it drains sees the tracked task complete —
    /// the apply never runs.</summary>
    [Fact]
    public void AShutdownSettlesAPendingApplySoTheDrainCompletesWithoutTheOwnerPumping()
    {
        WithPumpedContext(pump =>
        {
            var probe = new Probe(synchronousForTests: false);
            using var queued = new ManualResetEventSlim(false);
            probe.ApplyQueuedForTests = queued.Set;
            probe.Run();
            // The apply is QUEUED on this thread — the seam fires under the
            // work lock the instant it is (IPC-2) — and this thread does not
            // pump: without the settle the drain below could never complete.
            Assert.True(queued.Wait(TimeSpan.FromSeconds(10)), "the apply was never queued");
            probe.Shutdown();
            Task drain = probe.DrainAll();
            Assert.True(drain.Wait(TimeSpan.FromSeconds(5)), "the drain waited on an apply the owner could not run");
            drain.GetAwaiter().GetResult();
            Assert.Equal(0, probe.Applies);
            // The queued callback RUNS now — a frame behind it — finds its
            // promise already settled, and applies nothing.
            bool frameRan = false;
            _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frameRan = true);
            Assert.True(pump(() => frameRan), "the late callback's frame never ran");
            Assert.Equal(0, probe.Applies);
            Assert.True(probe.DrainAll().IsCompleted);
        });
    }

    /// <summary>A context whose Post runs the callback INLINE on the posting
    /// thread (IPD-1): the apply is claimed and runs once, on the worker,
    /// outside the scheduler's lock — no deadlock, the drain completes.</summary>
    private sealed class InlineContext : SynchronizationContext
    {
        public int Posts { get; private set; }

        public override void Post(SendOrPostCallback d, object? state)
        {
            Posts++;
            d(state);
        }
    }

    /// <summary>A context whose Post REFUSES (IPD-1): a dispatcher already
    /// shut down — the promise is withdrawn and the tracked task completes
    /// without a fault.</summary>
    private sealed class RefusingContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) =>
            throw new InvalidOperationException("the owner context refused the post");
    }

    [Fact]
    public async Task AContextThatRunsThePostInlineAppliesOnceAndTheDrainCompletes()
    {
        var context = new InlineContext();
        var probe = new Probe(context);
        Assert.True(probe.HasContext);
        probe.Run();
        Task drain = probe.DrainAll();
        Assert.True(await Task.WhenAny(drain, Task.Delay(TimeSpan.FromSeconds(10))) == drain, "the drain never completed");
        await drain;
        Assert.Equal(1, context.Posts);
        Assert.Equal(1, probe.Applies);
        Assert.NotNull(probe.ApplyThread);
    }

    [Fact]
    public async Task AContextThatRefusesThePostCompletesTheTrackedTaskWithoutAFault()
    {
        var probe = new Probe(new RefusingContext());
        probe.Run();
        Task drain = probe.DrainAll();
        Assert.True(await Task.WhenAny(drain, Task.Delay(TimeSpan.FromSeconds(10))) == drain, "the drain never completed");
        await drain;
        Assert.Equal(0, probe.Applies);
        Assert.NotNull(probe.ComputeThread);
        // A completed task leaves the pending set, so the drain cannot see
        // a fault — the scheduler counts them, and the count stays zero
        // (the sweep's `ipd1-refused-post-faults`).
        await Task.Delay(50);
        Assert.Equal(0, probe.FaultedWorkForTests);
    }
}
