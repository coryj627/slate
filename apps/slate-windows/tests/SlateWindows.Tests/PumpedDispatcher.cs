// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR A (contract A-2, AR-6): the pumped dispatcher harness the graph
/// facts run under — the canvas presentation engine's
/// <c>WithPumpedContext</c> shape. A <see cref="DispatcherSynchronizationContext"/>
/// is installed on the calling thread, so a document constructed inside
/// the body captures it as its owner context, and the body pumps
/// <see cref="DispatcherFrame"/>s until a condition holds.
/// </summary>
internal static class PumpedDispatcher
{
    public static void Run(Action body)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try
        {
            body();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    /// <summary>Pump until the condition holds or ten seconds pass; the
    /// return value is the condition's final answer.</summary>
    public static bool PumpUntil(Func<bool> condition, TimeSpan? budget = null)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan limit = budget ?? TimeSpan.FromSeconds(10);
        while (!condition() && clock.Elapsed < limit)
        {
            Drain();
            Thread.Yield();
        }
        return condition();
    }

    /// <summary>One background-priority frame: everything queued on the
    /// current dispatcher before it runs.</summary>
    public static void Drain()
    {
        var frame = new DispatcherFrame();
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    /// <summary>Pump until a scheduler's fixed-point drain completes — and
    /// OBSERVE it (IPA-8): a faulted or cancelled drain is complete too,
    /// and a fact that only waited would hide a scheduler or receiver
    /// failure behind assertions that need no publication.</summary>
    public static void PumpUntilDrained(Task drain)
    {
        Xunit.Assert.True(PumpUntil(() => drain.IsCompleted), "the drain never completed");
        drain.GetAwaiter().GetResult();
    }
}
