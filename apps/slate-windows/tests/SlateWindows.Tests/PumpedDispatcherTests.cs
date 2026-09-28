// SPDX-License-Identifier: MIT
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Xunit;

namespace SlateWindows.Tests;

/// <summary>The test-side pump's contract, pinned where a suite relies on
/// it: a false condition pumps for the whole budget and answers false —
/// no throw, no early return — which is how <c>GraphEndToEndTests.Host.Observed</c>
/// drains one relay window after a line's first sighting (IPJ-1-4;
/// codoki's third-round note).</summary>
public sealed class PumpedDispatcherTests
{
    [Fact]
    public void PumpUntilWithAFalseConditionPumpsForTheBudgetAndAnswersFalse()
    {
        bool answer = true;
        long elapsedMilliseconds = 0;
        StaThread.RunPumped(() =>
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            answer = PumpedDispatcher.PumpUntil(() => false, TimeSpan.FromMilliseconds(150));
            elapsedMilliseconds = clock.ElapsedMilliseconds;
        }, TimeSpan.FromSeconds(30), "the pump did not return");
        Assert.False(answer);
        Assert.True(elapsedMilliseconds >= 140, $"the pump returned after {elapsedMilliseconds} ms, before its 150 ms budget");
    }

    [Fact]
    public void PumpUntilAnswersTrueAsSoonAsTheConditionHolds()
    {
        bool answer = false;
        StaThread.RunPumped(() =>
        {
            int pumps = 0;
            answer = PumpedDispatcher.PumpUntil(() => ++pumps >= 3, TimeSpan.FromSeconds(5));
        }, TimeSpan.FromSeconds(30), "the pump did not return");
        Assert.True(answer);
    }

    /// <summary>A drain whose frame the dispatcher never runs FAILS, within
    /// its bound. Here a Normal-priority operation that re-posts itself
    /// starves the drain's Background-priority frame while the dispatcher
    /// stays healthy — its own wake-ups still reach the loop, so this is
    /// the watchdog's bound, not its wake (the next fact is the wake).</summary>
    [Fact]
    public void ADrainTheDispatcherNeverRunsFailsWithinItsBound() => StaThread.RunPumped(() =>
    {
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        bool starving = true;
        void Starve()
        {
            if (starving)
            {
                _ = dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(Starve));
            }
        }
        Starve();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Assert.Throws<TimeoutException>(() => PumpedDispatcher.Drain(TimeSpan.FromMilliseconds(300)));
        starving = false;
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"the bounded drain took {clock.Elapsed}");
    }, TimeSpan.FromSeconds(30), "the bounded drain blocked");

    /// <summary>The post-exhaustion hang, reproduced: a dispatcher whose
    /// message window could not be created carries none, and every wake-up
    /// it issues itself — the frame's <c>Continue</c> setter's among them —
    /// is a post to that window, so it reaches nothing and <c>PushFrame</c>
    /// blocks in <c>GetMessage</c> for good. Detaching the window from a
    /// live dispatcher puts it in that state. Only the watchdog's DIRECT
    /// wake, a thread message, ends the drain: it must be made, once, and
    /// succeed. Without it the drain blocks until this fact wakes the thread
    /// itself, and fails saying so.</summary>
    [Fact]
    public void ADrainOnADispatcherWithoutItsWindowEndsThroughTheDirectWake()
    {
        FieldInfo? window = typeof(Dispatcher).GetField("_window", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.True(window is not null, "WPF's Dispatcher._window moved; teach this witness the new shape");
        var wakes = new List<bool>();
        Func<uint, bool> directWake = PumpedDispatcher.WakeThread;
        PumpedDispatcher.WakeThread = threadId =>
        {
            bool posted = directWake(threadId);
            lock (wakes)
            {
                wakes.Add(posted);
            }
            return posted;
        };
        uint drainingThread = 0;
        bool timedOut = false;
        try
        {
            StaThread.Run(() =>
            {
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                object? detached = window.GetValue(dispatcher);
                Assert.NotNull(detached);
                Volatile.Write(ref drainingThread, PumpedDispatcher.GetCurrentThreadId());
                window.SetValue(dispatcher, null);
                try
                {
                    Assert.Throws<TimeoutException>(() => PumpedDispatcher.Drain(TimeSpan.FromMilliseconds(300)));
                }
                finally
                {
                    window.SetValue(dispatcher, detached);
                }
            }, TimeSpan.FromSeconds(10), "the drain on a windowless dispatcher never returned", unwindGrace: TimeSpan.FromSeconds(1));
        }
        catch (Xunit.Sdk.XunitException wedged) when (wedged.Message.Contains("has not unwound", StringComparison.Ordinal))
        {
            timedOut = true;
        }
        finally
        {
            PumpedDispatcher.WakeThread = directWake;
        }
        if (timedOut)
        {
            // Unwedge it for the facts after this one: the watchdog already
            // ended the frame; the loop only needs waking.
            _ = PostThreadMessageW(drainingThread, 0 /* WM_NULL */, IntPtr.Zero, IntPtr.Zero);
            Assert.True(
                SpinWait.SpinUntil(() => StaThread.Wedged() is null, TimeSpan.FromSeconds(10)),
                "the drained thread did not end even after this fact woke it");
            Assert.Fail("the drain on a windowless dispatcher blocked until this fact woke its thread: the watchdog's direct wake is missing or failed");
        }
        lock (wakes)
        {
            Assert.Equal([true], wakes);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessageW(uint threadId, uint message, IntPtr wParam, IntPtr lParam);
}
