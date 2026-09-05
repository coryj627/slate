// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Search;

namespace SlateWindows.Tests;

/// <summary>
/// W5-2 (#742) contract S3 facts: the snippet splitter is a toggle
/// state machine that cannot throw. Core never validates what SQLite's
/// <c>snippet()</c> emits, so imbalance and nesting are inputs, not
/// errors. Inputs are built from <see cref="SnippetRuns.HitStart"/> /
/// <see cref="SnippetRuns.HitEnd"/> so no invisible control character
/// hides in a literal.
/// </summary>
public sealed class SnippetRunsTests
{
    private static readonly string Stx = SnippetRuns.HitStart.ToString();
    private static readonly string Etx = SnippetRuns.HitEnd.ToString();

    [Fact]
    public void SingleSpanSplitsIntoThreeRuns()
    {
        string snippet = $"before {Stx}match{Etx} after";

        IReadOnlyList<SnippetRun> runs = SnippetRuns.Split(snippet);

        Assert.Equal(
            [
                new SnippetRun("before ", false),
                new SnippetRun("match", true),
                new SnippetRun(" after", false),
            ],
            runs);
        Assert.Equal("before match after", SnippetRuns.StripMarkers(snippet));
    }

    [Fact]
    public void NoSpanYieldsOneUnemphasizedRun()
    {
        IReadOnlyList<SnippetRun> runs = SnippetRuns.Split("plain text");

        Assert.Equal([new SnippetRun("plain text", false)], runs);
        Assert.Equal("plain text", SnippetRuns.StripMarkers("plain text"));
    }

    [Fact]
    public void MultipleSpansAlternateCorrectly()
    {
        string snippet = $"{Stx}one{Etx} and {Stx}two{Etx}";

        Assert.Equal(
            [
                new SnippetRun("one", true),
                new SnippetRun(" and ", false),
                new SnippetRun("two", true),
            ],
            SnippetRuns.Split(snippet));
    }

    [Fact]
    public void StrayEtxClearsEmphasisWithoutThrowing()
    {
        string snippet = $"a{Etx}b";

        Assert.Equal(
            [new SnippetRun("a", false), new SnippetRun("b", false)],
            SnippetRuns.Split(snippet));
        Assert.Equal("ab", SnippetRuns.StripMarkers(snippet));
    }

    [Fact]
    public void DanglingStxLeavesTheTailEmphasized()
    {
        string snippet = $"a{Stx}tail";

        Assert.Equal(
            [new SnippetRun("a", false), new SnippetRun("tail", true)],
            SnippetRuns.Split(snippet));
    }

    [Fact]
    public void NestedPairIsANoOpNotAnInversion()
    {
        // Set/clear, not XOR: the inner pair must not un-emphasize "c"
        // or emphasize "e".
        string snippet = $"a{Stx}b{Stx}c{Etx}d{Etx}e";

        Assert.Equal(
            [
                new SnippetRun("a", false),
                new SnippetRun("b", true),
                new SnippetRun("c", true),
                new SnippetRun("d", false),
                new SnippetRun("e", false),
            ],
            SnippetRuns.Split(snippet));
    }

    [Fact]
    public void EmptySnippetIsLegalAndYieldsNoRuns()
    {
        // Every empty-query tag-scope row carries one (search_db.rs:377).
        Assert.Empty(SnippetRuns.Split(string.Empty));
        Assert.Equal(string.Empty, SnippetRuns.StripMarkers(string.Empty));
    }

    [Fact]
    public void MarkerOnlySnippetYieldsNoRuns()
    {
        string snippet = $"{Stx}{Etx}{Etx}{Stx}";

        Assert.Empty(SnippetRuns.Split(snippet));
        Assert.Equal(string.Empty, SnippetRuns.StripMarkers(snippet));
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("")]
    [InlineData("|S|match|E|")]
    [InlineData("|S|dangling")]
    [InlineData("stray|E|etx")]
    [InlineData("a|S|b|S|nested|E|c|E|tail")]
    [InlineData("|E||S||E||S|churn|E||E||S|")]
    [InlineData("unicode 🗂 |S|Ünïcode|E| tail")]
    public void RunsConcatenateToTheStrippedTextAndNeverContainAMarker(string template)
    {
        // Templates spell the markers as |S| / |E| so the fixture stays
        // visible in the source; expand before splitting.
        string snippet = template
            .Replace("|S|", Stx, StringComparison.Ordinal)
            .Replace("|E|", Etx, StringComparison.Ordinal);

        IReadOnlyList<SnippetRun> runs = SnippetRuns.Split(snippet);
        string stripped = SnippetRuns.StripMarkers(snippet);

        Assert.Equal(stripped, string.Concat(runs.Select(run => run.Text)));
        Assert.All(runs, run =>
        {
            Assert.DoesNotContain(SnippetRuns.HitStart, run.Text);
            Assert.DoesNotContain(SnippetRuns.HitEnd, run.Text);
            Assert.NotEmpty(run.Text);
        });
        Assert.DoesNotContain(SnippetRuns.HitStart, stripped);
        Assert.DoesNotContain(SnippetRuns.HitEnd, stripped);
    }
}
