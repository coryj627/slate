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

    /// <summary>The premise is read before the key. A read that sees the
    /// state elsewhere fails at once in the premise's own words: it is not
    /// asked again, and the key is never pressed.</summary>
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

    /// <summary>A premise whose first read cannot be read is asked again,
    /// as the watch after the key treats the same fault as no observation:
    /// unreadable once and then in place passes, and the key is pressed
    /// once. Failing on that one read was a flake with focus correct
    /// throughout.</summary>
    [Fact]
    public void APremiseThatCannotBeReadAtFirstIsAskedAgainAndTheKeyIsPressedOnce()
    {
        int presses = 0;
        int before = 0;

        string? why = FocusSettleWatch.WatchAcrossAKey(
            () => presses > 0 ? true : ++before == 1 ? (bool?)null : true,
            () => presses++,
            Short,
            poll: TimeSpan.Zero,
            premisePatience: TimeSpan.FromSeconds(5));

        Assert.Null(why);
        Assert.Equal(1, presses);
        Assert.Equal(2, before);
    }

    /// <summary>A premise that can never be read fails at its cap, says that
    /// it could not be read (not that focus was elsewhere) and how many
    /// reads it tried, and the key is never pressed.</summary>
    [Fact]
    public void APremiseThatCanNeverBeReadFailsAtItsCapAndSaysHowManyReadsItTried()
    {
        bool pressed = false;
        int reads = 0;

        string? why = FocusSettleWatch.WatchAcrossAKey(
            () =>
            {
                reads++;
                return null;
            },
            () => pressed = true,
            Short,
            poll: TimeSpan.Zero,
            premisePatience: Short);

        Assert.NotNull(why);
        Assert.StartsWith("Premise:", why, StringComparison.Ordinal);
        Assert.Contains("could not be read before the key", why, StringComparison.Ordinal);
        Assert.Contains($"{reads} reads", why, StringComparison.Ordinal);
        Assert.True(reads > 1, "the premise was read once only");
        Assert.False(pressed, "the key was pressed though the premise could not be read");
    }

    /// <summary>Asking again is for a read that cannot be read, never for
    /// one that sees the state elsewhere: unreadable and then elsewhere
    /// fails on the second read, with the key not pressed.</summary>
    [Fact]
    public void APremiseReadElsewhereAfterAnUnreadableReadFailsAtOnce()
    {
        bool pressed = false;
        int reads = 0;

        string? why = FocusSettleWatch.WatchAcrossAKey(
            () => ++reads == 1 ? (bool?)null : false,
            () => pressed = true,
            Short,
            poll: TimeSpan.Zero,
            premisePatience: TimeSpan.FromSeconds(5));

        Assert.NotNull(why);
        Assert.StartsWith("Premise:", why, StringComparison.Ordinal);
        Assert.Contains("not in place before the key", why, StringComparison.Ordinal);
        Assert.False(pressed, "the key was pressed though the premise failed");
        Assert.Equal(2, reads);
    }
}
