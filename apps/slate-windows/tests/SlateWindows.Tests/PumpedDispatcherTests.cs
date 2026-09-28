// SPDX-License-Identifier: MIT
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
    /// its bound, instead of blocking the thread for good — the shape of
    /// the post-exhaustion hang, where a dispatcher without its message
    /// window held the test host until the CI job's limit. Here a
    /// Normal-priority operation that re-posts itself starves the drain's
    /// Background-priority frame.</summary>
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
}
