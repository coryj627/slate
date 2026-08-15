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
