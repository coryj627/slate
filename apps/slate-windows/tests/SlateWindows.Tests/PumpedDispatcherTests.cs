// SPDX-License-Identifier: MIT
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
        var thread = new Thread(() => PumpedDispatcher.Run(() =>
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            answer = PumpedDispatcher.PumpUntil(() => false, TimeSpan.FromMilliseconds(150));
            elapsedMilliseconds = clock.ElapsedMilliseconds;
        }));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "the pump did not return");
        Assert.False(answer);
        Assert.True(elapsedMilliseconds >= 140, $"the pump returned after {elapsedMilliseconds} ms, before its 150 ms budget");
    }

    [Fact]
    public void PumpUntilAnswersTrueAsSoonAsTheConditionHolds()
    {
        bool answer = false;
        var thread = new Thread(() => PumpedDispatcher.Run(() =>
        {
            int pumps = 0;
            answer = PumpedDispatcher.PumpUntil(() => ++pumps >= 3, TimeSpan.FromSeconds(5));
        }));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "the pump did not return");
        Assert.True(answer);
    }
}
