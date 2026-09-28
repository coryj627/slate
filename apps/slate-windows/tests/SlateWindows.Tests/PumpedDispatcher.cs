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

    /// <summary>How long one <see cref="Drain()"/> may wait for its frame.</summary>
    internal static readonly TimeSpan DrainBound = TimeSpan.FromSeconds(30);

    /// <summary>One background-priority frame: everything queued on the
    /// current dispatcher before it runs.</summary>
    /// <remarks>Bounded: a dispatcher whose message window could not be
    /// created (the desktop heap exhausted — see <see cref="StaThread"/>)
    /// never runs the frame's closing operation, and an unbounded
    /// <c>PushFrame</c> then blocked its thread for good, holding the test
    /// host until the CI job's limit. A watchdog ends the frame and wakes
    /// the thread's message loop directly — the dispatcher's own wake-up
    /// goes through the window that is missing — and the drain fails
    /// fast.</remarks>
    public static void Drain() => Drain(DrainBound);

    /// <summary><see cref="Drain()"/> with an explicit bound.</summary>
    internal static void Drain(TimeSpan bound)
    {
        var frame = new DispatcherFrame();
        bool ran = false;
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () =>
            {
                Volatile.Write(ref ran, true);
                frame.Continue = false;
            });
        uint thread = GetCurrentThreadId();
        using (new Timer(
            _ =>
            {
                if (Volatile.Read(ref ran))
                {
                    return;
                }
                try
                {
                    frame.Continue = false;
                }
                catch (Exception)
                {
                    // A broken dispatcher; the thread message below still
                    // wakes the loop, which then sees the frame ended.
                }
                _ = PostThreadMessageW(thread, 0 /* WM_NULL */, IntPtr.Zero, IntPtr.Zero);
            },
            null,
            bound,
            Timeout.InfiniteTimeSpan))
        {
            Dispatcher.PushFrame(frame);
        }
        if (!Volatile.Read(ref ran))
        {
            throw new TimeoutException(
                $"The dispatcher did not run a background frame within {bound.TotalSeconds:0.#} s; "
                + "a dispatcher whose message window could not be created (desktop heap exhausted?) never will.");
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PostThreadMessageW(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

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
