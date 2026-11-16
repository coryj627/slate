// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;

namespace SlateWindows.AccessibilityTests;

/// <summary>
/// Watches focus stay where a key left it, for the journeys that check a key
/// moved nothing. The read is a UIA query, so it answers true (focus is
/// there), false (it left) or null (UIA faulted and nothing was seen).
/// </summary>
internal static class FocusSettleWatch
{
    /// <summary>How many reads must see focus in place before it counts as
    /// having stayed.</summary>
    internal const int RequiredReads = 5;

    /// <summary>The longest a watch reads before it gives up on seeing
    /// <see cref="RequiredReads"/>.</summary>
    internal static readonly TimeSpan Cap = TimeSpan.FromSeconds(10);

    /// <summary>Null when focus stayed for <paramref name="window"/> and was
    /// seen at least <see cref="RequiredReads"/> times; otherwise why not.
    /// It counts reads, not wall time (#1611): on a loaded VM a read can take
    /// 300 ms, so it keeps reading past the window until enough have seen
    /// focus, up to <paramref name="cap"/>. A read that sees focus gone fails
    /// at once.</summary>
    internal static string? Watch(Func<bool?> read, TimeSpan window, TimeSpan? cap = null, TimeSpan? poll = null)
    {
        TimeSpan limit = cap ?? Cap;
        var clock = Stopwatch.StartNew();
        int observed = 0;
        int lost = 0;
        TimeSpan slowest = TimeSpan.Zero;
        while (clock.Elapsed < window || observed < RequiredReads)
        {
            if (clock.Elapsed >= limit)
            {
                return $"Focus could be read only {observed} times in {limit.TotalSeconds:0.#} s: "
                    + $"{lost} reads were lost to UIA faults, and the slowest read took {slowest.TotalMilliseconds:0} ms.";
            }

            TimeSpan began = clock.Elapsed;
            bool? onRow = read();
            TimeSpan took = clock.Elapsed - began;
            if (took > slowest)
            {
                slowest = took;
            }

            if (onRow is false)
            {
                return $"Focus left the row after {clock.ElapsedMilliseconds} ms.";
            }

            if (onRow is true)
            {
                observed++;
            }
            else
            {
                lost++;
            }

            Thread.Sleep(poll ?? TimeSpan.FromMilliseconds(100));
        }

        return null;
    }

    /// <summary>A key that must move nothing. The state is read in place
    /// once before the key: that is the premise, and a read that sees the
    /// state elsewhere, or cannot see it, fails there in its own words, with
    /// the key not pressed. Then the key is pressed and <see cref="Watch"/>
    /// runs from the press on, so the first read that sees the state gone
    /// fails, whatever a later read would see. A check that first waited
    /// after the key for a read in place would pass a key that moved focus
    /// away and let it come back: it would prove recovery, not that nothing
    /// moved.</summary>
    internal static string? WatchAcrossAKey(
        Func<bool?> inPlace, Action press, TimeSpan window, TimeSpan? cap = null, TimeSpan? poll = null)
    {
        switch (inPlace())
        {
            case null:
                return "Premise: the state could not be read before the key (UI Automation faulted or answered no element), "
                    + "so the key was not pressed.";
            case false:
                return "Premise: the state was not in place before the key, so the key was not pressed.";
        }

        press();
        return Watch(inPlace, window, cap, poll);
    }
}
