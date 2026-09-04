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
    private sealed class Probe(bool synchronousForTests) : PanelWorkScheduler(synchronousForTests)
    {
        public int? ComputeThread { get; private set; }
        public int? ApplyThread { get; private set; }
        public int Applies { get; private set; }
        public bool HasContext => HasUiContext;

        public void Run(Action? afterApply = null)
        {
            StartWorkAlwaysAsync(
                () =>
                {
                    ComputeThread = Environment.CurrentManagedThreadId;
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
        probe.Run(afterApply: () =>
        {
            if (!chained)
            {
                chained = true;
                probe.Run();
            }
        });
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
}
