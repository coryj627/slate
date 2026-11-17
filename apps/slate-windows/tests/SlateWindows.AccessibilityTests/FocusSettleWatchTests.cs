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

    /// <summary>The faults the gate's reads take for no observation, here
    /// the one kind.</summary>
    private static bool Unreadable(Exception exception) => exception is System.Runtime.InteropServices.COMException;

    /// <summary>A fault a record can throw: of the kind the reads take for
    /// no observation, or any other.</summary>
    private static Exception RecordFault(bool ofTheUnreadableKind) =>
        ofTheUnreadableKind
            ? new System.Runtime.InteropServices.COMException("the element is gone")
            : new InvalidOperationException("the element is gone");

    /// <summary>The first read after the key sees the state elsewhere, the
    /// record of what it found cannot be read, and every later read sees the
    /// state back in place. The check fails on that first read and still
    /// says the state was seen elsewhere. Read under the verdict's own
    /// catch, a record that threw a UI Automation fault turned the read
    /// into no observation, and the watch passed on the reads after it; one
    /// that threw anything else lost the message.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AReadThatSawTheStateElsewhereStaysAFailureWhenItsRecordCannotBeRead(bool ofTheUnreadableKind)
    {
        Exception fault = RecordFault(ofTheUnreadableKind);
        int presses = 0;
        int after = 0;
        string recorded = string.Empty;

        string? why = FocusSettleWatch.WatchAcrossAKey(
            () => FocusSettleWatch.Decide(
                () => (bool?)(presses == 0 || ++after > 1),
                () => throw fault,
                found => recorded = found,
                Unreadable),
            () => presses++,
            Short,
            poll: TimeSpan.Zero);

        Assert.NotNull(why);
        Assert.Contains("Focus left the row", why, StringComparison.Ordinal);
        Assert.Equal(1, after);
        Assert.Contains("could not be read", recorded, StringComparison.Ordinal);
        Assert.Contains(fault.GetType().Name, recorded, StringComparison.Ordinal);
        Assert.Contains("the element is gone", recorded, StringComparison.Ordinal);
    }

    /// <summary>A read that saw the state elsewhere hands over what it
    /// found, and its verdict is false.</summary>
    [Fact]
    public void AReadThatSawTheStateElsewhereRecordsWhatItFound()
    {
        string recorded = string.Empty;

        bool? seen = FocusSettleWatch.Decide(() => false, () => " found the tab bar", found => recorded = found, Unreadable);

        Assert.False(seen);
        Assert.Equal(" found the tab bar", recorded);
    }

    /// <summary>A verdict of true is final too: nothing more is read after
    /// it, so nothing can fault after it.</summary>
    [Fact]
    public void AReadThatSawTheStateInPlaceAsksForNoRecord()
    {
        int described = 0;
        int recorded = 0;

        bool? seen = FocusSettleWatch.Decide(
            () => true,
            () =>
            {
                described++;
                throw new InvalidOperationException("never asked");
            },
            _ => recorded++,
            Unreadable);

        Assert.True(seen);
        Assert.Equal(0, described);
        Assert.Equal(0, recorded);
    }

    /// <summary>Only the verdict's own read can be no observation: a fault
    /// of the named kind there answers null and asks for no record, and any
    /// other exception is the journey's to see.</summary>
    [Fact]
    public void OnlyTheVerdictsOwnFaultIsNoObservation()
    {
        int described = 0;
        string Describe()
        {
            described++;
            return string.Empty;
        }

        bool? seen = FocusSettleWatch.Decide(
            () => throw new System.Runtime.InteropServices.COMException("busy"), Describe, _ => described++, Unreadable);

        Assert.Null(seen);
        Assert.Equal(0, described);
        Assert.Throws<InvalidOperationException>(
            () => FocusSettleWatch.Decide(() => throw new InvalidOperationException("a defect"), Describe, null, Unreadable));
    }

    /// <summary>A watch that passed asks its message for nothing: a read
    /// made only to word a failure cannot fail a check that passed.</summary>
    [Fact]
    public void AWatchThatPassedAsksItsMessageForNothing()
    {
        int asked = 0;

        string? failure = FocusSettleWatch.FailureOf(
            null,
            "The key moved focus.",
            () =>
            {
                asked++;
                throw new InvalidOperationException("never asked");
            });

        Assert.Null(failure);
        Assert.Equal(0, asked);
    }

    /// <summary>A watch that failed keeps its reason when the rest of its
    /// message cannot be read.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AWatchThatFailedKeepsItsReasonWhenItsRecordCannotBeRead(bool ofTheUnreadableKind)
    {
        Exception fault = RecordFault(ofTheUnreadableKind);
        string? failure = FocusSettleWatch.FailureOf("Focus left the row after 12 ms.", "The key moved focus.", () => throw fault);

        Assert.NotNull(failure);
        Assert.StartsWith("The key moved focus. Focus left the row after 12 ms.", failure, StringComparison.Ordinal);
        Assert.Contains("could not be read", failure, StringComparison.Ordinal);
        Assert.Contains(fault.GetType().Name, failure, StringComparison.Ordinal);
    }

    /// <summary>A watch that failed and could read its record says
    /// both.</summary>
    [Fact]
    public void AWatchThatFailedSaysItsReasonAndItsRecord()
    {
        string? failure = FocusSettleWatch.FailureOf("Focus left the row after 12 ms.", "The key moved focus.", () => " Focused: the tab bar.");

        Assert.Equal("The key moved focus. Focus left the row after 12 ms. Focused: the tab bar.", failure);
    }
}
