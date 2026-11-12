// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// #1901: <see cref="WallClockBudget"/> discards one untimed first reading and
/// returns the median of the warm ones, so a slow first use or a few
/// stretched readings do not decide a budget, and a cost in every reading
/// does.
/// </summary>
public sealed class WallClockBudgetTests
{
    private static TimeSpan Ms(double value) => TimeSpan.FromMilliseconds(value);

    [Fact]
    public void TheFirstReadingIsDiscardedAndTheWarmMedianReturned()
    {
        var readings = new Queue<TimeSpan>([Ms(598), Ms(3), Ms(9), Ms(2), Ms(250), Ms(4)]);
        int calls = 0;
        TimeSpan median = WallClockBudget.WarmMedian(() =>
        {
            calls++;
            return readings.Dequeue();
        });

        Assert.Equal(1 + WallClockBudget.Samples, calls);
        Assert.Equal(Ms(4), median);
    }

    [Fact]
    public void ACostInEveryWarmReadingIsTheMedian()
    {
        var readings = new Queue<TimeSpan>([Ms(900), Ms(700), Ms(640), Ms(820), Ms(610), Ms(760)]);
        TimeSpan median = WallClockBudget.WarmMedian(() => readings.Dequeue());

        Assert.Equal(Ms(700), median);
    }

    [Fact]
    public async Task TheAsynchronousFormDiscardsTheFirstReadingToo()
    {
        var readings = new Queue<TimeSpan>([Ms(650), Ms(40), Ms(48), Ms(46), Ms(300), Ms(47)]);
        int calls = 0;
        TimeSpan median = await WallClockBudget.WarmMedianAsync(async () =>
        {
            calls++;
            await Task.Yield();
            return readings.Dequeue();
        });

        Assert.Equal(1 + WallClockBudget.Samples, calls);
        Assert.Equal(Ms(47), median);
    }
}
