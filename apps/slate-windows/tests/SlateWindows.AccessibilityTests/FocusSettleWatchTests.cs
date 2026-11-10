// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;

namespace SlateWindows.AccessibilityTests;

/// <summary>
/// #1611: the Files tree journey's check that a key left focus on its row.
/// These run without a window: the watch is a function of its reads.
/// </summary>
public sealed class FocusSettleWatchTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(1500);

    /// <summary>The shell gate's failure: on a loaded VM each UIA read took
    /// about 300 ms, so only 4 fitted in the window, though every one saw
    /// focus on the row. Slow reads that all see the row pass.</summary>
    [Fact]
    public void SlowReadsThatAllSeeTheRowPass()
    {
        string? why = FocusSettleWatch.Watch(
            () =>
            {
                Thread.Sleep(300);
                return true;
            },
            Window);

        Assert.Null(why);
    }

    [Fact]
    public void FastReadsPassOnceTheWindowHasPassed()
    {
        var clock = Stopwatch.StartNew();

        string? why = FocusSettleWatch.Watch(() => true, Window);

        Assert.Null(why);
        Assert.True(clock.Elapsed >= Window, $"the watch ended after {clock.ElapsedMilliseconds} ms, inside its window");
    }

    [Fact]
    public void AReadThatSeesFocusLeaveFailsAtOnce()
    {
        int reads = 0;
        var clock = Stopwatch.StartNew();

        string? why = FocusSettleWatch.Watch(() => ++reads < 3, Window);

        Assert.NotNull(why);
        Assert.Contains("left the row", why, StringComparison.Ordinal);
        Assert.True(clock.Elapsed < Window, $"the watch read on for {clock.ElapsedMilliseconds} ms after focus left");
    }

    /// <summary>Reads that never see anything fail at the cap, and say how
    /// many were lost and how slow the slowest was, so a failure tells a slow
    /// UIA from a faulting one.</summary>
    [Fact]
    public void ReadsThatAllFaultFailAtTheCapAndSayWhy()
    {
        string? why = FocusSettleWatch.Watch(() => null, Window, cap: TimeSpan.FromSeconds(2));

        Assert.NotNull(why);
        Assert.Contains("read only 0 times", why, StringComparison.Ordinal);
        Assert.Contains("lost", why, StringComparison.Ordinal);
        Assert.Contains("slowest read", why, StringComparison.Ordinal);
    }
}
