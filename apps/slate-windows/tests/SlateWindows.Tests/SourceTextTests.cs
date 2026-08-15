// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// W5-1 (#741): the comment stripper every drift scrape now runs through.
/// </summary>
/// <remarks>
/// A stripper that quietly did nothing would restore the dead-source
/// defect it exists to close, and a stripper that ate too much would make
/// a live chord look deleted. Both directions are pinned here.
/// </remarks>
public sealed class SourceTextTests
{
    [Fact]
    public void CommentedOutCodeIsNotScrapable()
    {
        // Codex's exact mutation shape: the arm is gone from the product
        // but its text remains, so a regex still reads the chord.
        const string source = """
            double pixels = (Orientation, key) switch
            {
                // (Orientation.Vertical, Key.Up) => -Step,
                (Orientation.Vertical, Key.Down) => Step,
            };
            """;

        string stripped = SourceText.WithoutComments(source);
        Assert.DoesNotContain("Key.Up", stripped, System.StringComparison.Ordinal);
        Assert.Contains("Key.Down", stripped, System.StringComparison.Ordinal);
    }

    [Fact]
    public void BlockCommentsGoToo_AndTheCodeAroundThemSurvives()
    {
        const string source = "case Key.A: /* case Key.B: */ case Key.C:";

        string stripped = SourceText.WithoutComments(source);
        Assert.DoesNotContain("Key.B", stripped, System.StringComparison.Ordinal);
        Assert.Contains("Key.A", stripped, System.StringComparison.Ordinal);
        Assert.Contains("Key.C", stripped, System.StringComparison.Ordinal);
    }

    [Fact]
    public void SlashesInsideLiteralsDoNotSwallowTheLine()
    {
        // The over-eager failure: a URL, a path separator or an escaped
        // quote must not delete live code to the end of its line.
        const string source = """"
            var a = "https://example.invalid"; case Key.Up:
            var b = @"C:\a\b"; case Key.Down:
            var c = "he said \"//\""; case Key.Left:
            var d = '/'; case Key.Right:
            var e = @"a""//b"; case Key.Home:
            """";

        string stripped = SourceText.WithoutComments(source);
        foreach (string key in new[] { "Key.Up", "Key.Down", "Key.Left", "Key.Right", "Key.Home" })
        {
            Assert.Contains(key, stripped, System.StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Raw and <c>@$</c> literals do not end early and leak live code.
    /// </summary>
    /// <remarks>
    /// A stripper that ends a literal at the wrong quote treats the code
    /// after it as string content and stops stripping comments there —
    /// a silent under-scrape, which is the exact failure class this
    /// stripper exists to close. No scraped file uses either form today;
    /// both are ordinary C# and would arrive without warning.
    /// </remarks>
    [Fact]
    public void RawAndInterpolatedVerbatimLiteralsDoNotEndEarly()
    {
        // A raw literal containing quotes, a "//" and a "/*" — none of
        // which end it, and none of which are comments.
        string source = "var a = " + "\"\"\"" + " he said \"//\" /* x */ " + "\"\"\""
            + "; case Key.Up: // gone\ncase Key.Down:";

        string stripped = SourceText.WithoutComments(source);
        Assert.Contains("Key.Up", stripped, System.StringComparison.Ordinal);
        Assert.Contains("Key.Down", stripped, System.StringComparison.Ordinal);
        Assert.DoesNotContain("gone", stripped, System.StringComparison.Ordinal);

        // @$"…" and $@"…" are both legal; only one prefix order was
        // recognised before. A TRAILING BACKSLASH is what discriminates:
        // verbatim ends the literal at the quote, non-verbatim reads \"
        // as an escape and runs on to the end of the line — swallowing
        // the comment after it, which then never gets stripped. An input
        // without the trailing backslash parses identically either way
        // and proves nothing, which is how the first version of this test
        // passed under mutation.
        foreach (string prefix in new[] { "@$", "$@" })
        {
            string interpolated = $"var b = {prefix}\"C:\\\"; // case Key.Left:\ncase Key.End:";
            string strippedLiteral = SourceText.WithoutComments(interpolated);
            Assert.DoesNotContain("Key.Left", strippedLiteral, System.StringComparison.Ordinal);
            Assert.Contains("Key.End", strippedLiteral, System.StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LineStructureSurvives_SoAnchoredPatternsStillMatch()
    {
        // MethodBody bounds a scrape on "\n    private", so a stripper
        // that ate newlines would silently return the rest of the file.
        const string source = "    private void A() // trailing\n    private void B()";

        string stripped = SourceText.WithoutComments(source);
        Assert.Contains("\n    private void B()", stripped, System.StringComparison.Ordinal);
        Assert.DoesNotContain("trailing", stripped, System.StringComparison.Ordinal);
    }
}
