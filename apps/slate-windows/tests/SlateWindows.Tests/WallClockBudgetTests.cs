// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// #1901: a wall-clock budget measured inside the full suite reads the
/// runner's load as much as the code. <see cref="WallClockBudget"/> keeps the
/// budget but takes the fastest of a few attempts, so a transient spike
/// passes and a regression, which slows every attempt, still fails.
/// </summary>
public sealed class WallClockBudgetTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(500);

    [Fact]
    public void AFirstAttemptUnderTheBudgetIsTheOnlyAttempt()
    {
        int calls = 0;
        TimeSpan fastest = WallClockBudget.Fastest(Budget, () =>
        {
            calls++;
            return TimeSpan.FromMilliseconds(120);
        });

        Assert.Equal(1, calls);
        Assert.Equal(TimeSpan.FromMilliseconds(120), fastest);
    }

    [Fact]
    public void ASpikeOnTheFirstAttemptPassesOnTheNext()
    {
        var readings = new Queue<TimeSpan>(
            [TimeSpan.FromMilliseconds(598), TimeSpan.FromMilliseconds(140)]);
        int calls = 0;
        TimeSpan fastest = WallClockBudget.Fastest(Budget, () =>
        {
            calls++;
            return readings.Dequeue();
        });

        Assert.Equal(2, calls);
        Assert.Equal(TimeSpan.FromMilliseconds(140), fastest);
    }

    [Fact]
    public void ARegressionOverEveryAttemptReturnsItsFastestReading()
    {
        var readings = new Queue<TimeSpan>(
        [
            TimeSpan.FromMilliseconds(900),
            TimeSpan.FromMilliseconds(700),
            TimeSpan.FromMilliseconds(800),
        ]);
        int calls = 0;
        TimeSpan fastest = WallClockBudget.Fastest(Budget, () =>
        {
            calls++;
            return readings.Dequeue();
        });

        Assert.Equal(WallClockBudget.Attempts, calls);
        Assert.Equal(TimeSpan.FromMilliseconds(700), fastest);
        Assert.True(fastest >= Budget);
    }

    [Fact]
    public async Task TheAsynchronousFormStopsAtTheFirstAttemptUnderTheBudget()
    {
        var readings = new Queue<TimeSpan>(
        [
            TimeSpan.FromMilliseconds(650),
            TimeSpan.FromMilliseconds(90),
            TimeSpan.FromMilliseconds(10),
        ]);
        int calls = 0;
        TimeSpan fastest = await WallClockBudget.FastestAsync(Budget, async () =>
        {
            calls++;
            await Task.Yield();
            return readings.Dequeue();
        });

        Assert.Equal(2, calls);
        Assert.Equal(TimeSpan.FromMilliseconds(90), fastest);
    }
}
