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

    /// <summary>How long a premise goes on asking while its read cannot be
    /// read. A UI Automation fault, or a moment with no focused element, is
    /// transient (#1611 counted single reads lost to one), and the watch
    /// after the key already takes such a read for no observation: the
    /// premise must not be stricter about the same fault, or a journey fails
    /// with focus correct throughout. Three seconds is thirty reads at the
    /// poll: long past a transient fault, short beside a journey. It bounds
    /// only reads that cannot be read; a read that sees the state elsewhere
    /// is never asked again.</summary>
    internal static readonly TimeSpan PremisePatience = TimeSpan.FromSeconds(3);

    /// <summary>A key that must move nothing. The state is read in place
    /// before the key: that is the premise. A read that sees the state
    /// elsewhere fails there at once, in the premise's own words, with the
    /// key not pressed. A read that cannot be read is asked again every
    /// poll for <see cref="PremisePatience"/>, and passes on the first read
    /// in place; unreadable all that time fails as a premise that could not
    /// be read, saying how many reads were tried. Then the key is pressed
    /// and <see cref="Watch"/> runs from the press on, so the first read
    /// that sees the state gone fails, whatever a later read would see. A
    /// check that first waited after the key for a read in place would pass
    /// a key that moved focus away and let it come back: it would prove
    /// recovery, not that nothing moved.</summary>
    internal static string? WatchAcrossAKey(
        Func<bool?> inPlace,
        Action press,
        TimeSpan window,
        TimeSpan? cap = null,
        TimeSpan? poll = null,
        TimeSpan? premisePatience = null)
    {
        TimeSpan patience = premisePatience ?? PremisePatience;
        var clock = Stopwatch.StartNew();
        int unreadable = 0;
        while (true)
        {
            bool? before = inPlace();
            if (before is true)
            {
                break;
            }

            if (before is false)
            {
                return "Premise: the state was not in place before the key, so the key was not pressed.";
            }

            unreadable++;
            if (clock.Elapsed >= patience)
            {
                return $"Premise: the state could not be read before the key: {unreadable} reads in {patience.TotalSeconds:0.#} s "
                    + "all came back unreadable (UI Automation faulted or answered no element), so the key was not pressed.";
            }

            Thread.Sleep(poll ?? TimeSpan.FromMilliseconds(100));
        }

        press();
        return Watch(inPlace, window, cap, poll);
    }

    /// <summary>One read of a check that a key moved nothing, in two steps
    /// that cannot touch each other. The verdict is asked first and alone:
    /// true (in place), false (elsewhere), or no observation when the
    /// verdict's own read throws a fault <paramref name="unreadable"/> names
    /// (a UI Automation fault). Once the verdict is false it stays false:
    /// what that read found is asked for after it, best effort
    /// (<see cref="RecordOf"/>), and a fault there only shortens the
    /// message. Were the record read under the verdict's catch, a fault in
    /// it would turn a read that saw the state elsewhere into no
    /// observation, and the watch could pass once the state came back. A
    /// verdict of true, and one that could not be read, ask for no
    /// record.</summary>
    internal static bool? Decide(
        Func<bool?> verdict, Func<string> describe, Action<string>? record, Func<Exception, bool> unreadable)
    {
        bool? seen;
        try
        {
            seen = verdict();
        }
        catch (Exception exception) when (unreadable(exception))
        {
            return null;
        }

        if (seen is false)
        {
            record?.Invoke(RecordOf(describe));
        }

        return seen;
    }

    /// <summary>What a decided check adds to its message, read after the
    /// verdict and never able to change it: <paramref name="describe"/>'s
    /// words, or, when it throws anything at all, that the record could not
    /// be read and why.</summary>
    internal static string RecordOf(Func<string> describe)
    {
        try
        {
            return describe();
        }
        catch (Exception exception)
        {
            return $" The record of what the read found could not be read: {exception.GetType().Name}: {exception.Message}";
        }
    }

    /// <summary>The failure message of a watch that has decided, or null
    /// when it passed. <paramref name="why"/> is the verdict. A pass asks
    /// <paramref name="record"/> for nothing, so no read made for a message
    /// can fail a check that passed; a failure keeps <paramref name="why"/>
    /// whatever the record does (<see cref="RecordOf"/>).</summary>
    internal static string? FailureOf(string? why, string message, Func<string> record) =>
        why is null ? null : $"{message} {why}{RecordOf(record)}";
}
