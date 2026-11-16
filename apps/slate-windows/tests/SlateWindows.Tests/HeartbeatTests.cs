// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// #1843: the heartbeat a timed-out fact reads (<see cref="Heartbeat"/>).
/// First the ring's arithmetic, on injected timestamps; then the one real
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

    /// <summary>A window is read up to the end its reader names, and what
    /// happened after that end is not the window's. Here the thread beat
    /// for 200 ms, missed nearly nine seconds and beat again. A window that
    /// ended 50 ms after the second beat saw one interval as its longest
    /// gap: a timed-out body's window ends when its bound passes, so a stall
    /// during the wait for its stack, or during the dump, is not read as a
    /// stall during the body.</summary>
    [Fact]
    public void AGapAfterTheWindowsEndIsNotCounted()
    {
        long second = System.Diagnostics.Stopwatch.Frequency;
        long opened = System.Diagnostics.Stopwatch.GetTimestamp() - (10 * second);
        var ring = new Heartbeat.Ring(capacity: 16, first: opened);
        ring.Beat(opened + (second / 10));
        ring.Beat(opened + (2 * second / 10));
        ring.Beat(opened + (9 * second));
        var window = new Heartbeat.Window(ring, opened);

        TimeSpan gap = window.LongestGapUntil(opened + (25 * second / 100));

        Assert.Equal(System.Diagnostics.Stopwatch.GetElapsedTime(0, second / 10), gap);
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
        TimeSpan gap = window.LongestGapUntil(System.Diagnostics.Stopwatch.GetTimestamp());
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
