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
/// <remarks>The loop is a function of the frames it is handed, so it runs on
/// fed frames with no window (<c>SettledPixelsTests</c>).</remarks>
internal static class SettledPixels
{
    /// <summary>How many captures in a row must be equal.</summary>
    internal const int RequiredEqualFrames = 3;

    /// <summary>The pause between two captures.</summary>
    internal static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);

    /// <summary>The longest a wait goes on capturing.</summary>
    internal static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    /// <summary>Null once <see cref="RequiredEqualFrames"/> captures in a
    /// row were equal; otherwise why not, with the captures made.
    /// <paramref name="capture"/> answers a frame's pixels, or null when
    /// nothing is drawn yet or the surface could not be read: such a capture
    /// counts for nothing and the run starts again after it.</summary>
    internal static string? Wait(Func<int[]?> capture, TimeSpan? bound = null, TimeSpan? interval = null)
    {
        TimeSpan limit = bound ?? Bound;
        var clock = Stopwatch.StartNew();
        int[]? last = null;
        int equal = 0;
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
                last = null;
                equal = 0;
            }
            else if (last is not null && frame.AsSpan().SequenceEqual(last))
            {
                equal++;
            }
            else
            {
                last = frame;
                equal = 1;
            }

            longest = Math.Max(longest, equal);
            if (equal >= RequiredEqualFrames)
            {
                return null;
            }

            if (clock.Elapsed >= limit)
            {
                return $"no {RequiredEqualFrames} captures in a row were equal in {limit.TotalSeconds:0.#} s: {captures} captures, "
                    + $"{empty} of them with nothing drawn, and the longest run of equal ones was {longest}.";
            }

            Thread.Sleep(interval ?? Interval);
        }
    }
}
