// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-5 (#737): the three citation overlays — details, summary, and
/// files-citing. Contracts 10 (no invented data), 12 (summary counts
/// are reference-derived) and 4 (overlays announce nothing on open).
/// </summary>
public class CitationOverlayTests
{
    private static BibEntry FullEntry() =>
        new(
            "knuth1984", "article", "Literate Programming",
            [new Author("Knuth", "Donald E.")], 1984, "The Computer Journal",
            "10.1093/comjnl/27.2.97", "https://example.org/lp", "ACM",
            "A method of programming.");

    private static BibEntry SparseEntry() =>
        new("ghost", "misc", "", [], null, null, null, null, null, null);

    private static CitationReference Reference(params string[] keys) =>
        new(
            "[@" + string.Join("; @", keys) + "]",
            [.. keys.Select(k => new CitedItem(k, null, null, null, CitationMode.Bracketed))],
            0, 1);

    private static RenderedCitation Rendered(BibEntry? entry) =>
        new("[@knuth1984]", "[1]", "Citation: Knuth 1984", entry, "ieee");

    [Fact]
    public void DetailsFromAnEntryFollowTheMacFieldOrder()
    {
        var details = CitationDetailsViewModel.FromEntry(FullEntry());

        Assert.Equal(
            ["Title", "Authors", "Year", "Journal", "Publisher", "DOI", "URL"],
            details.Fields.Select(f => f.Label));
        Assert.Equal("Knuth, Donald E.", details.Fields[1].Value);
        Assert.Equal("1984", details.Fields[2].Value);
        Assert.Equal("Year: 1984", details.Fields[2].AutomationName);
        Assert.Equal(
            "https://doi.org/10.1093/comjnl/27.2.97", details.Fields[5].LinkTarget);
        Assert.Equal("https://example.org/lp", details.Fields[6].LinkTarget);
        Assert.Equal(
            "Citation expanded. Title: Literate Programming.", details.AutomationName);
        Assert.False(details.IsUnresolved);
    }

    [Fact]
    public void AbsentFieldsProduceNoRowAtAll()
    {
        // Contract 10: never "Year: 0.", never an empty Journal row,
        // never a hollow abstract disclosure.
        var details = CitationDetailsViewModel.FromEntry(SparseEntry());

        Assert.Empty(details.Fields);
        Assert.False(details.HasAbstract);
        Assert.Null(details.AbstractText);
        // No title still yields the bare summary, never "Title: ."
        Assert.Equal("Citation expanded.", details.AutomationName);
    }

    [Fact]
    public void AnAbsentAbstractIsTheNormalCaseAndRendersAsAbsence()
    {
        // The DB read path hardcodes AbstractText absent, so this is
        // the ordinary state — not an error, not an empty expander.
        BibEntry noAbstract = FullEntry() with { AbstractText = null };
        var details = CitationDetailsViewModel.FromEntry(noAbstract);
        Assert.False(details.HasAbstract);

        var withAbstract = CitationDetailsViewModel.FromEntry(FullEntry());
        Assert.True(withAbstract.HasAbstract);
        Assert.Equal(
            "Abstract, collapsed.", withAbstract.AbstractAutomationName);
        withAbstract.AbstractExpanded = true;
        Assert.Equal(
            "Abstract: A method of programming.", withAbstract.AbstractAutomationName);
    }

    [Fact]
    public void UnresolvedDetailsUseCoresParsedKeyNotARawTextScrape()
    {
        var details = CitationDetailsViewModel.FromRendered(
            Rendered(entry: null), Reference("nosuchkey"));

        Assert.True(details.IsUnresolved);
        Assert.Equal("nosuchkey", details.UnresolvedKey);
        Assert.Empty(details.Fields);
        Assert.Equal("Citation key not found", details.UnresolvedHeading);
        // Visible and spoken forms differ deliberately (mac parity).
        Assert.Equal(
            "'nosuchkey' isn't in any bibliography source.", details.UnresolvedBody);
        Assert.Equal(
            "Unresolved citation: nosuchkey. This key isn't in any bibliography source.",
            details.AutomationName);
    }

    [Fact]
    public void DetailsCarryTheReturnFocusTokenForEscape()
    {
        // Contract 11: the overlay knows which row opened it.
        object token = new();
        var details = CitationDetailsViewModel.FromEntry(FullEntry(), token);
        Assert.Same(token, details.ReturnFocusToken);
    }

    [Fact]
    public void SummaryCountsUniqueKeysAcrossMultiKeySites()
    {
        // Contract 12: [@a; @b] contributes TWO unique keys from ONE
        // rendered row — the rendered list cannot express that, which
        // is exactly why the count reads references.
        var references = new[] { Reference("a", "b"), Reference("c") };
        var summary = new CitationSummaryViewModel(
            total: 2, references, _ => { }, () => { });

        Assert.Equal(2, summary.Total);
        Assert.Equal(3, summary.Unique);
        Assert.Equal(
            "This note has 2 citations referencing 3 unique sources.", summary.Body);
    }

    [Fact]
    public void RepeatedKeysWithDifferentLocatorsCountOnce()
    {
        // [@a, p. 1] and [@a, p. 9] are two sites, one source.
        var references = new[]
        {
            new CitationReference(
                "[@a, p. 1]",
                [new CitedItem("a", new Locator("p.", "1"), null, null, CitationMode.Bracketed)],
                0, 1),
            new CitationReference(
                "[@a, p. 9]",
                [new CitedItem("a", new Locator("p.", "9"), null, null, CitationMode.Bracketed)],
                0, 2),
        };
        var summary = new CitationSummaryViewModel(
            total: 2, references, _ => { }, () => { });

        Assert.Equal(2, summary.Total);
        Assert.Equal(1, summary.Unique);
        Assert.Equal(
            "This note has 2 citations referencing 1 unique source.", summary.Body);
    }

    [Fact]
    public void AnEmptySummaryUsesTheEmptyCopyAndCannotWalk()
    {
        var summary = new CitationSummaryViewModel(0, [], _ => { }, () => { });

        Assert.True(summary.IsEmpty);
        Assert.Equal("This note has no citations.", summary.Body);
        Assert.Equal(
            "Citation Summary. This note has no citations.", summary.AutomationName);
        Assert.Equal("OK", summary.DismissText);
        Assert.False(summary.CanWalkThrough);
    }

    [Fact]
    public void WalkThroughClosesFirstThenAnnouncesExactlyOnce()
    {
        var announced = new List<A11yEvent>();
        int closes = 0;
        var summary = new CitationSummaryViewModel(
            total: 1, [Reference("a")], announced.Add, () => closes++);

        Assert.Equal("Done", summary.DismissText);
        summary.WalkThrough();

        Assert.Equal(1, closes);
        Assert.Single(announced.OfType<A11yEvent.CitationWalkThrough>());

        // An empty summary's walk action is inert — no close, no
        // announcement (contract 4).
        var empty = new CitationSummaryViewModel(0, [], announced.Add, () => closes++);
        empty.WalkThrough();
        Assert.Equal(1, closes);
        Assert.Single(announced.OfType<A11yEvent.CitationWalkThrough>());
    }

    [Fact]
    public void ConstructingOverlaysAnnouncesNothing()
    {
        // §2.6: opening any overlay is silent — the container's
        // AutomationName is the speech surface.
        var announced = new List<A11yEvent>();
        _ = CitationDetailsViewModel.FromEntry(FullEntry());
        _ = CitationDetailsViewModel.FromRendered(
            Rendered(entry: null), Reference("k"));
        _ = new CitationSummaryViewModel(1, [Reference("a")], announced.Add, () => { });
        Assert.Empty(announced);
    }
}
