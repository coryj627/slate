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

    /// <summary>The window of the facts that feed the reads across a key:
    /// they never wait on it.</summary>
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(50);

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

        string? why = FocusSettleWatch.Watch(() => ++reads < 3, Window);

        Assert.NotNull(why);
        Assert.Contains("left the row", why, StringComparison.Ordinal);
        // The third read saw focus leave, and the watch made no fourth: it
        // stopped at once, whatever the clock read.
        Assert.Equal(3, reads);
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

    /// <summary>Plan 47 CB-7: a key that moved focus away and let it come
    /// back did move it. In place before the key; after it the reads go
    /// gone, gone, back, back: the first read after the key fails the check,
    /// and no later read is made. A check that waited after the key for a
    /// read in place, and only then watched, passed this.</summary>
    [Fact]
    public void AKeyThatMovedFocusAwayAndBackFailsOnTheFirstReadAfterIt()
    {
        bool pressed = false;
        int after = 0;

        string? why = FocusSettleWatch.WatchAcrossAKey(
            () => !pressed || ++after > 2, () => pressed = true, Short, poll: TimeSpan.Zero);

        Assert.NotNull(why);
        Assert.Contains("left the row", why, StringComparison.Ordinal);
        Assert.Equal(1, after);
    }

    /// <summary>A read after the key that cannot see the state is no
    /// observation: unreadable once, then in place for the whole window,
    /// passes.</summary>
    [Fact]
    public void AnUnreadableReadAfterTheKeyIsNoObservation()
    {
        bool pressed = false;
        int after = 0;

        string? why = FocusSettleWatch.WatchAcrossAKey(
            () => !pressed ? true : ++after == 1 ? (bool?)null : true, () => pressed = true, Short, poll: TimeSpan.Zero);

        Assert.Null(why);
        Assert.True(after > FocusSettleWatch.RequiredReads, $"only {after} reads were made after the key");
    }

    /// <summary>In place on the first read after the key and gone on the
    /// second: the second read fails the check.</summary>
    [Fact]
    public void AKeyThatMovesFocusAfterOneReadInPlaceFails()
    {
        bool pressed = false;
        int after = 0;

        string? why = FocusSettleWatch.WatchAcrossAKey(
            () => !pressed || ++after < 2, () => pressed = true, Short, poll: TimeSpan.Zero);

        Assert.NotNull(why);
        Assert.Contains("left the row", why, StringComparison.Ordinal);
        Assert.Equal(2, after);
    }

    /// <summary>The premise is read once, before the key. A state that is
    /// not in place then fails in the premise's own words, and the key is
    /// never pressed.</summary>
    [Fact]
    public void AStateNotInPlaceBeforeTheKeyFailsThePremiseAndPressesNothing()
    {
        bool pressed = false;
        int reads = 0;

        string? why = FocusSettleWatch.WatchAcrossAKey(
            () =>
            {
                reads++;
                return false;
            },
            () => pressed = true,
            Short,
            poll: TimeSpan.Zero);

        Assert.NotNull(why);
        Assert.StartsWith("Premise:", why, StringComparison.Ordinal);
        Assert.Contains("not in place before the key", why, StringComparison.Ordinal);
        Assert.False(pressed, "the key was pressed though the premise failed");
        Assert.Equal(1, reads);
    }

    /// <summary>A premise that cannot be read fails too, and says that it
    /// could not be read, not that focus was elsewhere.</summary>
    [Fact]
    public void AStateThatCannotBeReadBeforeTheKeyFailsThePremiseInItsOwnWords()
    {
        bool pressed = false;

        string? why = FocusSettleWatch.WatchAcrossAKey(() => null, () => pressed = true, Short, poll: TimeSpan.Zero);

        Assert.NotNull(why);
        Assert.StartsWith("Premise:", why, StringComparison.Ordinal);
        Assert.Contains("could not be read before the key", why, StringComparison.Ordinal);
        Assert.False(pressed, "the key was pressed though the premise could not be read");
    }
}
