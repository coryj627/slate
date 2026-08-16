// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Search;

namespace SlateWindows.Tests;

/// <summary>
/// W5-2 (#742): the host-side line derivation for an activated search
/// result — the pure twin of mac's <c>firstTokenLineNumber</c>
/// (<c>AppState.swift:23773-23830</c>), pinned fact by fact so the
/// heuristic cannot drift between platforms.
/// </summary>
public sealed class SearchLineLocatorTests
{
    private const string Body =
        "alpha line one\n"
        + "second line with beta\n"
        + "third line with gamma\n"
        + "and a fourth line";

    [Fact]
    public void EarliestOccurrenceOfAnyTokenWins()
    {
        // "gamma" appears on line 3, "beta" on line 2: the query order
        // does not matter, the earliest BODY position does.
        Assert.Equal(2, SearchLineLocator.FirstTokenLine(Body, "gamma beta"));
    }

    [Fact]
    public void MatchIsCaseInsensitiveInBothDirections()
    {
        Assert.Equal(2, SearchLineLocator.FirstTokenLine(Body, "BETA"));
        Assert.Equal(
            1,
            SearchLineLocator.FirstTokenLine("ALPHA IN CAPITALS", "alpha"));
    }

    [Fact]
    public void Fts5KeywordsNeverAnchorTheScan()
    {
        // The scan is substring-based (mac range(of:)), so an undropped
        // "and" would anchor inside "sand" on line 1. As a bare query
        // word it is an FTS5 keyword and must be dropped (#93 item 5),
        // leaving "beta" on line 2 as the anchor.
        string body = "sand on line one\nbeta on line two";
        Assert.Equal(2, SearchLineLocator.FirstTokenLine(body, "beta AND something"));
        Assert.Equal(3, SearchLineLocator.FirstTokenLine(Body, "and or not near gamma"));
    }

    [Fact]
    public void PureNumericTokensAreDropped()
    {
        // Numbers inside composite FTS5 constructs (NEAR(a b, 5)) are
        // not meaningful to a body-line scan.
        string body = "5 things\nbeta on line two";
        Assert.Equal(2, SearchLineLocator.FirstTokenLine(body, "NEAR(beta gamma, 5)"));
    }

    [Fact]
    public void BodyTextColumnPrefixIsStrippedBeforeTokenizing()
    {
        // body_text:beta means "beta inside body_text" — the prefix must
        // not seed "body" or "text" as scan tokens.
        string body = "the body text preamble\nbeta lives here";
        Assert.Equal(2, SearchLineLocator.FirstTokenLine(body, "body_text:beta"));
    }

    [Fact]
    public void NoMatchIsLineOne()
    {
        Assert.Equal(1, SearchLineLocator.FirstTokenLine(Body, "unfindable"));
        Assert.Equal(1, SearchLineLocator.FirstTokenLine(Body, string.Empty));
        Assert.Equal(1, SearchLineLocator.FirstTokenLine(Body, "and or 42"));
        Assert.Equal(1, SearchLineLocator.FirstTokenLine(string.Empty, "alpha"));
    }

    [Fact]
    public void NewlinesAreCountedNotGuessed()
    {
        Assert.Equal(1, SearchLineLocator.FirstTokenLine(Body, "alpha"));
        Assert.Equal(4, SearchLineLocator.FirstTokenLine(Body, "fourth"));
    }

    // ---- SR-3: the accepted tokenizer nuances (contracts doc) -----------
    //
    // Three places the .NET and Swift standard libraries disagree at the
    // margins. The port deliberately does NOT chase mac byte-for-byte;
    // each divergence bottoms out in the benign no-match fallback —
    // line 1 — and these facts pin that recorded behaviour so a future
    // "fix" is a deliberate SR-3 revision, not drift.

    [Fact]
    public void Sr3AnAstralPlaneQueryDegradesToLineOne()
    {
        // (1) UTF-16 vs Character tokenization: 𝒜 (U+1D49C) is a letter
        // to Swift's grapheme-based isLetter, but each of its UTF-16
        // surrogate halves fails char.IsLetterOrDigit, so no token ever
        // forms and the scan falls back to line 1 — even when the body
        // contains the exact character. Mac would land on line 2.
        string body = "first line\n\U0001D49C sits on line two";
        Assert.Equal(1, SearchLineLocator.FirstTokenLine(body, "\U0001D49C"));
    }

    [Fact]
    public void Sr3ANonDecimalNumeralDegradesToLineOne()
    {
        // (2) numeric classification: Swift's tokenizer admits Ⅻ
        // (U+216B, category Nl — alphabetic to Character.isLetter) and
        // then drops the pure-numeral token via isNumber; here
        // char.IsLetterOrDigit excludes Nl entirely, so the numeral
        // never forms a token and splits any token it touches. A bare
        // numeral query bottoms out at line 1 on both platforms — by
        // different routes — and a mixed token anchors on its letter
        // fragments here.
        string body = "first line\nⅫ on line two";
        Assert.Equal(1, SearchLineLocator.FirstTokenLine(body, "Ⅻ"));
        Assert.Equal(
            2,
            SearchLineLocator.FirstTokenLine("x\nnote here", "noteⅫ"));
    }

    [Fact]
    public void Sr3CanonicalEquivalenceIsNotAppliedAndDegradesToLineOne()
    {
        // (3) ordinal IndexOf vs Swift's canonically-equivalent string
        // search: a precomposed query (é, U+00E9) does not match a
        // decomposed body (e + U+0301) here — mac would land on line 2;
        // Windows accepts the line-1 fallback.
        string decomposedBody = "first line\ncafe\u0301 on line two";
        Assert.Equal(
            1,
            SearchLineLocator.FirstTokenLine(decomposedBody, "caf\u00E9"));
    }

    // ---- LineStartOffset: where the caret parks -------------------------

    [Fact]
    public void LineOneStartsAtOffsetZero()
    {
        Assert.Equal(0, SearchLineLocator.LineStartOffset(Body, 1));
        Assert.Equal(0, SearchLineLocator.LineStartOffset(Body, 0));
        Assert.Equal(0, SearchLineLocator.LineStartOffset(string.Empty, 3));
    }

    [Fact]
    public void MiddleLinesStartAfterTheirNewline()
    {
        Assert.Equal(
            Body.IndexOf("second", StringComparison.Ordinal),
            SearchLineLocator.LineStartOffset(Body, 2));
        Assert.Equal(
            Body.IndexOf("and a fourth", StringComparison.Ordinal),
            SearchLineLocator.LineStartOffset(Body, 4));
    }

    [Fact]
    public void CrlfEndingsLandPastTheCarriageReturnPair()
    {
        string crlf = "first\r\nsecond\r\nthird";
        Assert.Equal(
            crlf.IndexOf("second", StringComparison.Ordinal),
            SearchLineLocator.LineStartOffset(crlf, 2));
        Assert.Equal(
            crlf.IndexOf("third", StringComparison.Ordinal),
            SearchLineLocator.LineStartOffset(crlf, 3));
    }

    [Fact]
    public void ALinePastTheEndParksOnTheLastLineStart()
    {
        Assert.Equal(
            Body.IndexOf("and a fourth", StringComparison.Ordinal),
            SearchLineLocator.LineStartOffset(Body, 99));
    }
}
