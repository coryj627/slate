// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;

namespace SlateWindows.Tests;

/// <summary>
/// #1843: one thread for the whole test process that does nothing but note
/// the time, ten times a second. A fact that times out without giving up
/// its stack (<see cref="StaThread"/>) reads the longest stretch in which
/// this thread did not run while the fact's body did, and says it in the
/// timeout's message.
/// </summary>
/// <remarks>
/// <para>
/// Why. On 2026-11-06 a fact on the home pool ran past its 60 s bound in
/// synchronous code while the runner host committed a cache disk and
/// started another VM. Its message could not say whether the fact's thread
/// alone was held or the whole process had stopped, and the dump that
/// might have said was kept for 7 days. The job log is kept for 90, so the
/// reading goes into the message.
/// </para>
/// <para>
/// What a reading can say. The thread runs at the highest priority, so a
/// busy machine starves it after the process's other threads. A long gap
/// therefore means the process as a whole did not run: a stalled machine
/// or VM, a suspended process, or a garbage collection, which stops this
/// thread with every other managed one. The timeout prints the collector's
/// pause beside the gap for that reason. A short gap rules those out. A
/// reading cannot tell a starved fact thread from a healthy one, since
/// this thread runs ahead of both, and it does not say how many other
/// threads were held.
/// </para>
/// <para>
/// The thread sets no apartment state and makes no dispatcher and no
/// window, so it registers no window class and <c>StaThreadCensus</c> has
/// nothing of it to count. It is a background thread and never holds the
/// process's exit.
/// </para>
/// </remarks>
internal static class Heartbeat
{
    /// <summary>How long the thread sleeps between two beats.</summary>
    internal static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);

    /// <summary>About how far back the ring reaches: it keeps as many beats
    /// as this many intervals hold. The longest bound a fact names today is
    /// 5 minutes, so a body's window lies well inside it. A window that
    /// reached further back would be read from the ring's oldest beat, and
    /// the timeout's sentence would then speak for the end of the body only.</summary>
    internal static readonly TimeSpan Kept = TimeSpan.FromMinutes(10);

    /// <summary>The thread's name, as a dump lists it. It does not begin
    /// with <see cref="StaThread.ThreadNamePrefix"/>: the thread is no
    /// fact's.</summary>
    internal const string ThreadName = "fact-heartbeat";

    private static readonly Lock s_gate = new();

    private static Ring? s_ring;

    private static Thread? s_thread;

    /// <summary>The thread, once a window has been opened.</summary>
    internal static Thread? ThreadForTests => Volatile.Read(ref s_thread);

    /// <summary>How many beats have been recorded.</summary>
    internal static long Beats => Volatile.Read(ref s_ring)?.Total ?? 0;

    /// <summary>Open a window that starts now, starting the thread if this
    /// is the first one. A reading is taken through the window alone, so
    /// none is ever taken of a time before the thread existed.</summary>
    /// <remarks>The thread's start can throw only as any thread's can:
    /// <see cref="OutOfMemoryException"/>, when the system gives the process
    /// no further thread. It is not caught, as the fact's own thread, started
    /// next, would meet the same. Nothing is kept then, and the next window
    /// tries again.</remarks>
    internal static Window OpenWindow()
    {
        Ring ring = Volatile.Read(ref s_ring) ?? Start();
        return new Window(ring, Stopwatch.GetTimestamp());
    }

    private static Ring Start()
    {
        lock (s_gate)
        {
            if (s_ring is { } started)
            {
                return started;
            }
            // The first beat is this thread's, taken before the heartbeat's
            // thread starts: the stretch until that thread first runs is
            // then a gap like any other, and the ring is never empty.
            var ring = new Ring((int)(Kept / Interval), Stopwatch.GetTimestamp());
            var thread = new Thread(() => Beat(ring))
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = ThreadName,
            };
            thread.Start();
            Volatile.Write(ref s_thread, thread);
            Volatile.Write(ref s_ring, ring);
            return ring;
        }
    }

    /// <summary>The thread's whole body. The time is read before the ring is
    /// entered, so a beat carries the moment the thread ran, not the moment
    /// a reader let it in.</summary>
    private static void Beat(Ring ring)
    {
        while (true)
        {
            ring.Beat(Stopwatch.GetTimestamp());
            Thread.Sleep(Interval);
        }
    }

    /// <summary>A stretch of time that began when it was opened and ends at
    /// each reading.</summary>
    internal readonly struct Window
    {
        private readonly Ring _ring;
        private readonly long _from;

        internal Window(Ring ring, long from)
        {
            _ring = ring;
            _from = from;
        }

        /// <summary>The longest stretch since the window opened in which the
        /// thread recorded no beat. The stretch still open now counts.</summary>
        internal TimeSpan LongestGap() =>
            Stopwatch.GetElapsedTime(0, _ring.LongestGap(_from, Stopwatch.GetTimestamp()));
    }

    /// <summary>The newest beats, as timestamps in the order they came, and
    /// the arithmetic over them. It reads no clock: its one writer hands it
    /// each beat's time and a reader hands it the window, so its facts run
    /// on timestamps they make up.</summary>
    internal sealed class Ring
    {
        private readonly Lock _gate = new();
        private readonly long[] _beats;
        private long _total;

        /// <summary>A ring that keeps <paramref name="capacity"/> beats and
        /// is born with its first, so there is always an oldest one to
        /// measure from.</summary>
        internal Ring(int capacity, long first)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
            _beats = new long[capacity];
            Beat(first);
        }

        /// <summary>How many beats were ever recorded, the forgotten ones
        /// included.</summary>
        internal long Total
        {
            get
            {
                lock (_gate)
                {
                    return _total;
                }
            }
        }

        /// <summary>Record a beat at <paramref name="at"/>, which is never
        /// earlier than the beat before it. The oldest kept beat is
        /// forgotten once the ring is full.</summary>
        internal void Beat(long at)
        {
            lock (_gate)
            {
                _beats[_total % _beats.Length] = at;
                _total++;
            }
        }

        /// <summary>The longest stretch between <paramref name="from"/> and
        /// <paramref name="to"/> with no beat in it, in the timestamps' own
        /// unit.</summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item>A gap that ended before <paramref name="from"/> is not
        /// counted.</item>
        /// <item>A gap that straddles <paramref name="from"/> counts from
        /// there.</item>
        /// <item>The gap still open at <paramref name="to"/> counts up to
        /// it.</item>
        /// <item>The ring knows nothing of the time before its oldest kept
        /// beat. A window that starts earlier is measured from that beat.</item>
        /// </list>
        /// </remarks>
        internal long LongestGap(long from, long to)
        {
            lock (_gate)
            {
                long kept = Math.Min(_total, _beats.Length);
                long longest = 0;
                // Walked from the newest beat back: `end` is where the
                // stretch being measured ends.
                long end = to;
                for (long back = 1; back <= kept; back++)
                {
                    long beat = _beats[(_total - back) % _beats.Length];
                    if (beat > to)
                    {
                        // Recorded after the reader took its time.
                        continue;
                    }
                    if (beat <= from)
                    {
                        return Math.Max(longest, end - from);
                    }
                    longest = Math.Max(longest, end - beat);
                    end = beat;
                }
                return longest;
            }
        }
    }
}
