// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;

namespace SlateWindows;

/// <summary>Which way a step went past the end of the matches.</summary>
internal enum FindInNoteWrap
{
    None,
    ToTop,
    ToEnd,
}

/// <summary>
/// WB-22 (#1112): the find bar's host-composed lines — a §W-C golden
/// family, spoken through <c>HostComposed</c> at Medium. No mac twin:
/// AppKit's stock find bar announces nothing, so the family is designated
/// residue.
/// </summary>
internal static class FindInNotePhrase
{
    public const string MatchChanged = "Match changed; nothing replaced.";

    public const string WaitingForReadingView = "Waiting for the reading view.";

    /// <summary>"{N} of {M}"; "No matches" for none; "{M} matches" when
    /// matches exist but none is current (a recount the selection no
    /// longer covers).</summary>
    public static string Count(int current, int total) =>
        total == 0 ? "No matches"
        : current == 0 ? (total == 1 ? "1 match" : $"{total.ToString(CultureInfo.InvariantCulture)} matches")
        : $"{current.ToString(CultureInfo.InvariantCulture)} of {total.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>A step's one line: the count, with the wrap appended once.</summary>
    public static string Step(int current, int total, FindInNoteWrap wrap) => wrap switch
    {
        FindInNoteWrap.ToTop => $"{Count(current, total)}. Wrapped to the top",
        FindInNoteWrap.ToEnd => $"{Count(current, total)}. Wrapped to the end",
        _ => Count(current, total),
    };

    public static string ReplacedAll(int count) =>
        count == 1 ? "Replaced 1 match." : $"Replaced {count.ToString(CultureInfo.InvariantCulture)} matches.";
}
