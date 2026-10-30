// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>WB-22 (#1112): the find bar's host-composed lines, pinned
/// verbatim — a §W-C golden family with no mac twin (AppKit's find bar
/// announces nothing).</summary>
public sealed class FindInNotePhraseTests
{
    [Fact]
    public void TheCountLinesReadNOfMOrNoMatches()
    {
        Assert.Equal("1 of 3", FindInNotePhrase.Count(1, 3));
        Assert.Equal("3 of 3", FindInNotePhrase.Count(3, 3));
        Assert.Equal("No matches", FindInNotePhrase.Count(0, 0));
        // A recount that leaves no match current still says how many.
        Assert.Equal("3 matches", FindInNotePhrase.Count(0, 3));
        Assert.Equal("1 match", FindInNotePhrase.Count(0, 1));
    }

    [Fact]
    public void AWrapIsAppendedOnceToTheStepLine()
    {
        Assert.Equal("1 of 2. Wrapped to the top", FindInNotePhrase.Step(1, 2, FindInNoteWrap.ToTop));
        Assert.Equal("2 of 2. Wrapped to the end", FindInNotePhrase.Step(2, 2, FindInNoteWrap.ToEnd));
        Assert.Equal("2 of 2", FindInNotePhrase.Step(2, 2, FindInNoteWrap.None));
    }

    [Fact]
    public void TheReplaceAndWaitingLinesAreFixed()
    {
        Assert.Equal("Replaced 3 matches.", FindInNotePhrase.ReplacedAll(3));
        Assert.Equal("Replaced 1 match.", FindInNotePhrase.ReplacedAll(1));
        Assert.Equal("Match changed; nothing replaced.", FindInNotePhrase.MatchChanged);
        Assert.Equal("Waiting for the reading view.", FindInNotePhrase.WaitingForReadingView);
    }
}
