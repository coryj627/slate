// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;

namespace SlateWindows.Tests;

/// <summary>
/// #1843: the heartbeat a timed-out fact reads (<see cref="Heartbeat"/>).
/// First the ring's arithmetic, on injected timestamps; then a window's
/// reading over a ring of made-up stopwatch timestamps; then the one real
/// thread.
/// </summary>
public sealed class HeartbeatTests
{
    /// <summary>The plain case: the window holds several stretches between
    /// beats, and the longest of them is the answer.</summary>
    [Fact]
    public void TheLongestGapOfAWindowIsItsLongestStretchWithNoBeat()
    {
        Heartbeat.Ring ring = Beating(capacity: 16, 100, 200, 900, 1000, 1100);

        Assert.Equal(700, ring.LongestGap(from: 0, to: 1100));
    }

    /// <summary>A stall that was over before the body started is not the
    /// body's: the thread missed 5,000 here, and the window after it saw
    /// every beat.</summary>
    [Fact]
    public void AGapThatEndedBeforeTheWindowIsNotCounted()
    {
        Heartbeat.Ring ring = Beating(capacity: 16, 5000, 5100, 5200, 5300);

        Assert.Equal(100, ring.LongestGap(from: 5000, to: 5300));
        Assert.Equal(100, ring.LongestGap(from: 5050, to: 5300));
    }

    /// <summary>A stall that began before the body and ended inside it
    /// counts only for the part the body was there for. One that covers the
    /// whole window is the whole window.</summary>
    [Fact]
    public void AGapThatStraddlesTheWindowsStartIsClippedToIt()
    {
        Heartbeat.Ring ring = Beating(capacity: 16, 5000, 5100, 5200);

        Assert.Equal(2000, ring.LongestGap(from: 3000, to: 5200));
        Assert.Equal(700, ring.LongestGap(from: 1000, to: 1700));
    }

    /// <summary>The window's end closes the last stretch: a thread that
    /// stopped and had not run again by then missed everything up to it.</summary>
    [Fact]
    public void TheGapStillOpenAtTheWindowsEndIsCounted()
    {
        Heartbeat.Ring ring = Beating(capacity: 16, 100, 200);

        Assert.Equal(4000, ring.LongestGap(from: 0, to: 4200));
    }

    /// <summary>The ring keeps its newest beats only. Of the time before its
    /// oldest one it knows nothing, so that time is not read as a gap: here
    /// the beats at 0 and 1,000 are forgotten, and neither the stretch
    /// between them nor the one from the window's start to the oldest kept
    /// beat is counted.</summary>
    [Fact]
    public void AWindowOlderThanTheRingIsMeasuredFromTheRingsOldestBeat()
    {
        Heartbeat.Ring ring = Beating(capacity: 3, 1000, 1100, 1200, 1300);

        Assert.Equal(100, ring.LongestGap(from: 0, to: 1350));
    }

    /// <summary>A window's reading is its ring's, between its own opening
    /// and the end its reader names, as a time. Each window fact here has a
    /// longest gap of its own length, so no one answer serves two of them.
    /// Here the thread beat, missed seven seconds and beat again.</summary>
    [Fact]
    public void AWindowReadsAGapOfSecondsFromItsBeats()
    {
        Heartbeat.Window window = OpenedAt(0, beats: [100, 7100, 7200]);

        Assert.Equal(Milliseconds(7000), window.LongestGapUntil(At(7200)));
    }

    /// <summary>The same for an uneven fraction of a second: the longest of
    /// the stretches 120, 370, 120 and 40 ms.</summary>
    [Fact]
    public void AWindowReadsAGapOfAFractionOfASecondFromItsBeats()
    {
        Heartbeat.Window window = OpenedAt(0, beats: [120, 490, 610]);

        Assert.Equal(Milliseconds(370), window.LongestGapUntil(At(650)));
    }

    /// <summary>A window is read up to the end its reader names, and what
    /// happened after that end is not the window's. Here the thread beat at
    /// 300 ms, and the window ended at 750 ms with that stretch still open:
    /// 450 ms. The beat 10 ms after the end does not close the stretch
    /// later, and the eight seconds missed after it are not the window's. A
    /// timed-out body's window ends at its deadline, so a stall during the
    /// wait for its stack, or during the dump, is not read as a stall
    /// during the body.</summary>
    [Fact]
    public void AGapAfterTheWindowsEndIsNotCounted()
    {
        Heartbeat.Window window = OpenedAt(0, beats: [300, 760, 9000]);

        Assert.Equal(Milliseconds(450), window.LongestGapUntil(At(750)));
    }

    /// <summary>A window that opens three seconds into a five-second gap
    /// counts the two that came after its opening.</summary>
    [Fact]
    public void AWindowThatOpensInsideAGapCountsItFromItsOpening()
    {
        Heartbeat.Window window = OpenedAt(3000, beats: [5000, 5100]);

        Assert.Equal(Milliseconds(2000), window.LongestGapUntil(At(5100)));
    }

    /// <summary>A stopwatch timestamp a minute before these facts run. The
    /// made-up beats below are counted from it, so all of them lie in the
    /// past and a reading that ran on to now would show as a longer gap.</summary>
    private static readonly long AMinuteAgo = Stopwatch.GetTimestamp() - (60 * Stopwatch.Frequency);

    /// <summary>The stopwatch timestamp <paramref name="milliseconds"/>
    /// after <see cref="AMinuteAgo"/>.</summary>
    private static long At(long milliseconds) => AMinuteAgo + Ticks(milliseconds);

    private static long Ticks(long milliseconds) => milliseconds * Stopwatch.Frequency / 1000;

    /// <summary>That many milliseconds, converted from stopwatch ticks as a
    /// window converts its reading.</summary>
    private static TimeSpan Milliseconds(long milliseconds) => Stopwatch.GetElapsedTime(0, Ticks(milliseconds));

    /// <summary>A window that opened <paramref name="opened"/> ms after
    /// <see cref="AMinuteAgo"/>, over a ring whose first beat is at 0 ms and
    /// which then beat at each of <paramref name="beats"/>, in ms.</summary>
    private static Heartbeat.Window OpenedAt(long opened, long[] beats)
    {
        var ring = new Heartbeat.Ring(capacity: 16, first: At(0));
        foreach (long beat in beats)
        {
            ring.Beat(At(beat));
        }
        return new Heartbeat.Window(ring, At(opened));
    }

    /// <summary>The thread must never hold the test process's exit (xUnit
    /// fails a run whose foreground threads outlive it, #1826), and it sets
    /// no apartment state, so it is not one of the STA threads
    /// <c>StaThreadCensus</c> counts. There is one for the process.</summary>
    [Fact]
    public void TheHeartbeatsThreadIsOneBackgroundThreadAtTheHighestPriority()
    {
        _ = Heartbeat.OpenWindow();
        Thread? thread = Heartbeat.ThreadForTests;
        _ = Heartbeat.OpenWindow();

        Assert.NotNull(thread);
        Assert.Same(thread, Heartbeat.ThreadForTests);
        Assert.True(thread.IsAlive, "the heartbeat thread has ended");
        Assert.True(thread.IsBackground, "a foreground heartbeat thread would hold the test process's exit");
        Assert.Equal(ThreadPriority.Highest, thread.Priority);
        Assert.NotEqual(ApartmentState.STA, thread.GetApartmentState());
        Assert.Equal(Heartbeat.ThreadName, thread.Name);
        Assert.False(
            Heartbeat.ThreadName.StartsWith(StaThread.ThreadNamePrefix, StringComparison.Ordinal),
            "the heartbeat thread carries a fact thread's name, so a fact's window classes could be looked for under it");
    }

    /// <summary>The thread records beats, and a window's reading is of them:
    /// between two beats lies one whole interval, so a reading that spans
    /// three is never shorter than half of one (the half allows for the
    /// sleep's granularity).</summary>
    [Fact]
    public void TheHeartbeatBeats()
    {
        Heartbeat.Window window = Heartbeat.OpenWindow();
        long before = Heartbeat.Beats;

        Assert.True(
            SpinWait.SpinUntil(() => Heartbeat.Beats >= before + 3, TimeSpan.FromSeconds(30)),
            "the heartbeat thread recorded no beat in 30 s");
        TimeSpan gap = window.LongestGapUntil(Stopwatch.GetTimestamp());
        Assert.True(gap >= Heartbeat.Interval / 2, $"three beats came, but the window's longest gap reads {gap.TotalMilliseconds:0} ms");
    }

    /// <summary>A ring whose first beat is at 0 and which then beats at each
    /// of <paramref name="beats"/>.</summary>
    private static Heartbeat.Ring Beating(int capacity, params long[] beats)
    {
        var ring = new Heartbeat.Ring(capacity, first: 0);
        foreach (long beat in beats)
        {
            ring.Beat(beat);
        }
        return ring;
    }
}
