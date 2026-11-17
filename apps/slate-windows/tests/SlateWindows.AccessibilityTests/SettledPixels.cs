// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;

namespace SlateWindows.AccessibilityTests;

/// <summary>
/// Waits for a surface to stop changing before a journey measures its pixels
/// (plan 47 CB-7). A journey first waits for the state that starts the change
/// (a focus, a selection, an element), then for a settled frame here, and
/// then measures once. It never polls the measurement itself: a fact that
/// guards an animation ending in a bad colour would pass on its first frame.
/// </summary>
/// <remarks>
/// <para>
/// A surface has settled when its last three captures are equal, or when it
/// is blinking: its recent captures hold two frames and no other, and one of
/// them has come back after the other (A, B, A). A text caret is why the
/// second rule is there. A focused field, the editor and the reading surface
/// each draw one, and it turns on and off about every 530 ms. Three equal
/// captures need three captures inside one of its phases, which a slow
/// capture never gets: a UI Automation read alone has taken 300 ms on a
/// loaded job VM (#1611), and at that pace the frames go A, A, B, B, A for
/// as long as the wait lasts.
/// </para>
/// <para>
/// A blink alternates between two frames and comes back. A repaint or an
/// animation moves on and does not return to a frame it has left, so a third
/// frame starts the wait over from the frame before it, and A, A, B, B, C, C
/// never settles.
/// </para>
/// <para>
/// The loop is a function of the frames it is handed, so it runs on fed
/// frames with no window (<c>SettledPixelsTests</c>).
/// </para>
/// </remarks>
internal static class SettledPixels
{
    /// <summary>How many captures in a row must be equal for a surface at
    /// rest.</summary>
    internal const int RequiredEqualFrames = 3;

    /// <summary>The pause between two captures.</summary>
    internal static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);

    /// <summary>The longest a wait goes on capturing.</summary>
    internal static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    /// <summary>How a wait ended. <see cref="Why"/> is null when the surface
    /// settled, and <see cref="How"/> then says by which rule; otherwise
    /// <see cref="Why"/> says why not. <see cref="Captures"/> and
    /// <see cref="Took"/> are the captures made and the time they took, so a
    /// run's log shows the capture period.</summary>
    internal readonly record struct Outcome(string? Why, string How, int Captures, TimeSpan Took)
    {
        internal bool Settled => Why is null;
    }

    /// <summary>The rule a wait settled by.</summary>
    internal const string AtRest = "at rest";

    /// <summary>The other rule: a surface that blinks between two
    /// frames.</summary>
    internal const string Blinking = "blinking";

    /// <summary>Captures until the surface has settled or
    /// <paramref name="bound"/> has passed. <paramref name="capture"/>
    /// answers a frame's pixels, or null when nothing is drawn yet or the
    /// surface could not be read: such a capture counts for nothing and the
    /// wait starts over after it.</summary>
    internal static Outcome Wait(Func<int[]?> capture, TimeSpan? bound = null, TimeSpan? interval = null)
    {
        TimeSpan limit = bound ?? Bound;
        var clock = Stopwatch.StartNew();
        // The captures since the wait last started over, as runs of equal
        // frames. They never hold more than two distinct frames.
        var runs = new List<(int[] Frame, int Count)>();
        int longest = 0;
        int captures = 0;
        int empty = 0;
        while (true)
        {
            int[]? frame = capture();
            captures++;
            if (frame is null)
            {
                empty++;
                runs.Clear();
            }
            else if (runs.Count > 0 && frame.AsSpan().SequenceEqual(runs[^1].Frame))
            {
                runs[^1] = (runs[^1].Frame, runs[^1].Count + 1);
            }
            else
            {
                runs.Add((frame, 1));
                // A third frame: the surface has moved on, so the wait starts
                // over from the frame before this one.
                if (runs.Count == 3 && !frame.AsSpan().SequenceEqual(runs[0].Frame))
                {
                    runs.RemoveAt(0);
                }
            }

            if (runs.Count > 0)
            {
                longest = Math.Max(longest, runs[^1].Count);
                if (runs[^1].Count >= RequiredEqualFrames)
                {
                    return new Outcome(null, AtRest, captures, clock.Elapsed);
                }

                // Three runs of two frames: the first frame has come back.
                if (runs.Count == 3)
                {
                    return new Outcome(null, Blinking, captures, clock.Elapsed);
                }
            }

            if (clock.Elapsed >= limit)
            {
                return new Outcome(
                    $"it neither came to rest ({RequiredEqualFrames} equal captures in a row) nor blinked between two frames in "
                    + $"{limit.TotalSeconds:0.#} s: {captures} captures in {clock.ElapsedMilliseconds} ms, {empty} of them with "
                    + $"nothing drawn, and the longest run of equal ones was {longest}.",
                    string.Empty,
                    captures,
                    clock.Elapsed);
            }

            Thread.Sleep(interval ?? Interval);
        }
    }

    /// <summary>A popup's surface is on the screen once this share of its
    /// rectangle, one pixel in this many, differs there from the window the
    /// popup opens over. Before the popup has painted the screen shows that
    /// window, and the two are equal to the pixel (0 of 21,680 and 0 of
    /// 20,178 differed for the app menu's row and a combo box's row with
    /// their popups shut). Once it has painted nearly every pixel differs
    /// where the popup's surface is another colour than what lies under it,
    /// and where the two colours happen to be one, the row's own text still
    /// differs: 489 of the app menu row's 21,680 pixels are text, one in
    /// 44. One in a hundred sits between nothing and that.</summary>
    internal const int CoveredOneIn = 100;

    /// <summary>The rule a popup's wait ended by.</summary>
    internal const string Covered = "covered";

    /// <summary>Captures until a popup's surface is on the screen: the
    /// screen in its rectangle differs from the pixels of the window under
    /// it (<see cref="CoveredOneIn"/>). UI Automation can hand out a popup's
    /// row before the popup has painted, and the screen there is then the
    /// window beneath, at rest: a journey that settled and measured it would
    /// measure the wrong surface. <paramref name="capture"/> answers the two
    /// frames of one rectangle, or a null for either when it could not be
    /// read: such a capture is no observation.</summary>
    internal static Outcome WaitUntilCovered(
        Func<(int[]? OnScreen, int[]? Under)> capture, TimeSpan? bound = null, TimeSpan? interval = null)
    {
        TimeSpan limit = bound ?? Bound;
        var clock = Stopwatch.StartNew();
        int captures = 0;
        int unreadable = 0;
        int differing = 0;
        int total = 0;
        while (true)
        {
            (int[]? onScreen, int[]? under) = capture();
            captures++;
            if (onScreen is null || under is null || onScreen.Length != under.Length || onScreen.Length == 0)
            {
                unreadable++;
            }
            else
            {
                total = onScreen.Length;
                differing = 0;
                for (var pixel = 0; pixel < total; pixel++)
                {
                    if ((onScreen[pixel] & 0xFFFFFF) != (under[pixel] & 0xFFFFFF))
                    {
                        differing++;
                    }
                }

                if ((long)differing * CoveredOneIn >= total)
                {
                    return new Outcome(null, Covered, captures, clock.Elapsed);
                }
            }

            if (clock.Elapsed >= limit)
            {
                return new Outcome(
                    $"the screen there still showed the window under it after {limit.TotalSeconds:0.#} s: at the last capture that "
                    + $"could be read {differing} of {total} pixels differed from that window's own, and one in {CoveredOneIn} "
                    + $"had to; {captures} captures in {clock.ElapsedMilliseconds} ms, {unreadable} of them unreadable.",
                    string.Empty,
                    captures,
                    clock.Elapsed);
            }

            Thread.Sleep(interval ?? Interval);
        }
    }
}
