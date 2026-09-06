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

    /// <summary>W6-2 PR B's post-implementation pass 3 (IPB-13; A-2, B-1):
    /// the registration precedes the worker — when a compute starts, its
    /// task is already in the tracked set, so a drain observed from inside
    /// the compute is pending, never complete; the placeholder completes
    /// with the real task and the drain returns after the apply.</summary>
    [Fact]
    public void AComputeStartsOnlyAfterItsRegistrationAndADrainWaitsForIt()
    {
        WithPumpedContext(pump =>
        {
            var probe = new Probe(synchronousForTests: false);
            int pendingAtStart = -1;
            bool drainCompleteAtStart = true;
            // The caller is PARKED right after it scheduled the worker, until
            // the compute has read the tracked set: whatever the caller does
            // after the schedule cannot be what the compute saw.
            using var computeRead = new ManualResetEventSlim(false);
            probe.BeforeComputeForTests = () =>
            {
                pendingAtStart = probe.PendingWorkForTests;
                drainCompleteAtStart = probe.WhenAllWorkDrained().IsCompleted;
                computeRead.Set();
            };
            probe.AfterScheduleForTests = () => Assert.True(computeRead.Wait(TimeSpan.FromSeconds(10)), "the compute never started");
            probe.Run();
            Task drain = probe.DrainAll();
            Assert.True(pump(() => drain.IsCompleted), "the tracked task completed");
            drain.GetAwaiter().GetResult();
            Assert.Equal(1, pendingAtStart);
            Assert.False(drainCompleteAtStart);
            Assert.Equal(1, probe.Applies);
            Assert.Equal(0, probe.PendingWorkForTests);
        });
    }

    /// <summary>IPB-29 (pass 5's ledger): a shutdown that lands while a caller
    /// is inside the primitive and BEFORE its admission registers nothing —
    /// the check and the registration are one locked transition — so a drain
    /// after the flip has nothing to wait for and the compute never starts.
    /// The body would park on an uncompleted prerequisite, so a placeholder
    /// registered after the flip would stay tracked and be seen.</summary>
    [Fact]
    public void AShutdownBeforeTheAdmissionRegistersNothingAndTheComputeNeverStarts()
    {
        WithPumpedContext(pump =>
        {
            var probe = new Probe(synchronousForTests: false);
            var prerequisite = new TaskCompletionSource();
            probe.GateWorkOn(prerequisite.Task);
            using var parked = new ManualResetEventSlim(false);
            using var release = new ManualResetEventSlim(false);
            probe.BeforeRegisterForTests = () =>
            {
                parked.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)), "the caller was never released");
            };
            Task caller = Task.Run(() => probe.Run());
            Assert.True(parked.Wait(TimeSpan.FromSeconds(10)), "the caller never reached the seam");
            probe.Shutdown();
            release.Set();
            caller.GetAwaiter().GetResult();
            Assert.Equal(0, probe.PendingWorkForTests);
            Assert.True(probe.DrainAll().IsCompleted, "a drain after the flip waited for something");
            prerequisite.SetResult();
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

    /// <summary>A context whose Post BLOCKS until released (IPE-3): while it
    /// blocks, a shutdown on another thread must complete — which it cannot
    /// if the post is made under the work lock the shutdown needs.</summary>
    private sealed class BlockingContext : SynchronizationContext
    {
        public ManualResetEventSlim Posting { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Posting.Set();
            _ = Release.Wait(TimeSpan.FromSeconds(10));
            d(state);
        }
    }

    [Fact]
    public async Task AShutdownCompletesWhileAPostIsBlockedBecauseThePostIsOutsideTheLock()
    {
        var context = new BlockingContext();
        var probe = new Probe(context);
        probe.Run();
        Assert.True(context.Posting.Wait(TimeSpan.FromSeconds(10)), "the post never started");
        // The post is parked. A shutdown on a DEDICATED thread must reach
        // and finish its work — the lock it takes is not held across the
        // post (IPF-4: a dedicated thread, an entered barrier and a release
        // in `finally`, so a failure cannot leave the poster hanging and a
        // starved pool cannot be mistaken for lock inversion).
        using var entered = new ManualResetEventSlim(false);
        using var shutdownReturned = new ManualResetEventSlim(false);
        var shutdown = new Thread(() =>
        {
            entered.Set();
            probe.Shutdown();
            shutdownReturned.Set();
        });
        try
        {
            shutdown.Start();
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)), "the shutdown thread never started");
            Assert.True(shutdownReturned.Wait(TimeSpan.FromSeconds(5)), "the shutdown blocked behind the parked post");
        }
        finally
        {
            context.Release.Set();
            Assert.True(shutdown.Join(TimeSpan.FromSeconds(10)), "the shutdown thread never ended");
        }
        Task drain = probe.DrainAll();
        Assert.True(await Task.WhenAny(drain, Task.Delay(TimeSpan.FromSeconds(10))) == drain, "the drain never completed");
        await drain;
        // The released callback fails to claim the settled promise.
        Assert.Equal(0, probe.Applies);
        Assert.Equal(0, probe.FaultedWorkForTests);
    }

    /// <summary>A REAL dispatcher shut down with the apply ALREADY ENQUEUED
    /// (IPE-1, IPF-1): the operation is posted while the dispatcher thread is
    /// busy inside another operation, so it is still pending when that
    /// operation shuts the dispatcher down — WPF ABORTS it instead of
    /// throwing, and the promise must be withdrawn through the operation or
    /// the drain waits forever. Nothing applies, nothing faults.</summary>
    [Fact]
    public async Task AnEnqueuedApplyAbortedByTheDispatchersShutdownWithdrawsItsPromise()
    {
        Probe? probe = null;
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim(false);
        using var insideOperation = new ManualResetEventSlim(false);
        using var releaseOperation = new ManualResetEventSlim(false);
        using var queued = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            probe = new Probe(synchronousForTests: false);
            ready.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(10)), "the dispatcher thread never started");
        Assert.NotNull(probe);
        Assert.NotNull(dispatcher);
        Assert.True(probe.HasContext);
        probe.ApplyQueuedForTests = queued.Set;

        // Occupy the dispatcher thread: anything enqueued now waits behind
        // this operation, which shuts the dispatcher down when released.
        _ = dispatcher.BeginInvoke(
            DispatcherPriority.Normal,
            new Action(() =>
            {
                insideOperation.Set();
                _ = releaseOperation.Wait(TimeSpan.FromSeconds(10));
                dispatcher.InvokeShutdown();
            }));
        Assert.True(insideOperation.Wait(TimeSpan.FromSeconds(10)), "the dispatcher never entered the blocking operation");

        probe.Run();
        Assert.True(queued.Wait(TimeSpan.FromSeconds(10)), "the apply was never enqueued on the busy dispatcher");
        // The apply is a PENDING DispatcherOperation; the shutdown aborts it.
        releaseOperation.Set();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "the dispatcher never shut down");
        Assert.True(dispatcher.HasShutdownFinished);

        Task drain = probe.DrainAll();
        Assert.True(await Task.WhenAny(drain, Task.Delay(TimeSpan.FromSeconds(10))) == drain, "the drain waited on an aborted operation");
        await drain;
        Assert.NotNull(probe.ComputeThread);
        Assert.Equal(0, probe.Applies);
        await Task.Delay(50);
        Assert.Equal(0, probe.FaultedWorkForTests);
    }

    /// <summary>A dispatcher context handed in that targets ANOTHER thread's
    /// dispatcher (IPF-3): its dispatcher is not knowable here, so the apply
    /// posts through the CONTEXT — and lands on that other thread. A
    /// scheduler that inferred the constructing thread's dispatcher instead
    /// would post where nothing pumps and hang the drain.</summary>
    [Fact]
    public async Task AForeignDispatcherContextPostsThroughTheContextNotTheConstructingThread()
    {
        Dispatcher? owner = null;
        using var ready = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            owner = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(10)), "the owner thread never started");
        Assert.NotNull(owner);
        try
        {
            // Constructed HERE, over THAT thread's dispatcher.
            var probe = new Probe(new DispatcherSynchronizationContext(owner));
            probe.Run();
            Task drain = probe.DrainAll();
            Assert.True(
                await Task.WhenAny(drain, Task.Delay(TimeSpan.FromSeconds(10))) == drain,
                "the drain waited on an apply posted to a dispatcher nobody pumps");
            await drain;
            Assert.Equal(1, probe.Applies);
            Assert.Equal(owner.Thread.ManagedThreadId, probe.ApplyThread);
        }
        finally
        {
            owner.InvokeShutdown();
            Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "the owner dispatcher never shut down");
        }
    }

    /// <summary>A dispatcher already dead when the body posts (IPE-1): the
    /// refusal arm, distinct from the aborted-operation arm above.</summary>
    [Fact]
    public async Task AShutDownDispatcherAbortsThePostAndTheTrackedTaskStillCompletes()
    {
        Probe? probe = null;
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            probe = new Probe(synchronousForTests: false);
            ready.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(10)), "the dispatcher thread never started");
        Assert.NotNull(probe);
        Assert.NotNull(dispatcher);
        Assert.True(probe.HasContext);
        dispatcher.InvokeShutdown();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "the dispatcher never shut down");
        Assert.True(dispatcher.HasShutdownFinished);

        probe.Run();
        Task drain = probe.DrainAll();
        Assert.True(await Task.WhenAny(drain, Task.Delay(TimeSpan.FromSeconds(10))) == drain, "the drain waited on a post the dead dispatcher will never run");
        await drain;
        Assert.NotNull(probe.ComputeThread);
        Assert.Equal(0, probe.Applies);
        await Task.Delay(50);
        Assert.Equal(0, probe.FaultedWorkForTests);
    }

    /// <summary>A seam that throws is the test's defect (IPE-4): tracked
    /// work does not fault and the apply still lands.</summary>
    [Fact]
    public void AThrowingSeamFaultsNoTrackedWork()
    {
        WithPumpedContext(pump =>
        {
            var probe = new Probe(synchronousForTests: false);
            probe.ApplyQueuedForTests = () => throw new InvalidOperationException("a hostile seam");
            probe.Run();
            Assert.True(pump(() => probe.Applies == 1), "the apply landed");
            Task drain = probe.DrainAll();
            Assert.True(pump(() => drain.IsCompleted), "the drain completed");
            drain.GetAwaiter().GetResult();
            Assert.True(pump(() => probe.FaultedWorkForTests == 0));
            Assert.Equal(0, probe.FaultedWorkForTests);
        });
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
