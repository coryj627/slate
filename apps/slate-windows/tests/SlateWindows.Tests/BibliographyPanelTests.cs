// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-5 (#737): the bibliography leaf over REAL core output —
/// contracts 3 (staleness), 5 (degradation honesty), 6 (read-only
/// safety) and 9 (bounded presentation without silent loss).
/// </summary>
public sealed class BibliographyPanelTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;

    public BibliographyPanelTests()
    {
        _fixture = FixtureVault.Create(0, "bibliography-panel");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "library.bib"),
            "@article{knuth1984,\n  title = {Literate Programming},\n"
                + "  author = {Knuth, Donald E.},\n  year = {1984},\n"
                + "  journal = {The Computer Journal}\n}\n\n"
                + "@book{lamport1994,\n  title = {LaTeX: A Document Preparation System},\n"
                + "  author = {Lamport, Leslie and Bibby, Duane},\n  year = {1994},\n"
                + "  publisher = {Addison-Wesley}\n}\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "slate.json"),
            "{\"citations\":{\"bibliography\":\"library.bib\"}}");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "cited.md"),
            "# Cited\n\nKnown [@knuth1984] and unknown [@ghostkey].\n");
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
        _ = _session.SetBibliographySources(_session.CitationsPrefs().Sources);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    private BibliographyViewModel MakeLeaf(List<A11yEvent>? announced = null) =>
        new(_session, (announced ?? []).Add, synchronousForTests: true);

    private static BibEntry Entry(
        string key, string title, params Author[] authors) =>
        new(key, "article", title, authors, 2000, null, null, null, null, null, "{}");

    [Fact]
    public void EntriesLoadLazilyAndOnlyOnceUntilForced()
    {
        var leaf = MakeLeaf();
        Assert.Empty(leaf.Entries);

        leaf.EnsureLoaded();
        Assert.Equal(2, leaf.Entries.Count);
        int afterFirst = leaf.EntriesRequestIdForTests;

        // Idempotent: the rail reveal must not re-query the vault on
        // every leaf switch.
        leaf.EnsureLoaded();
        Assert.Equal(afterFirst, leaf.EntriesRequestIdForTests);

        // Only an explicit reload re-queries.
        leaf.ForceReload();
        Assert.NotEqual(afterFirst, leaf.EntriesRequestIdForTests);
        Assert.Equal(2, leaf.Entries.Count);
    }

    [Fact]
    public void RowsCarryTheMacLabelsFromCoreFields()
    {
        var leaf = MakeLeaf();
        leaf.EnsureLoaded();

        BibliographyRowViewModel knuth =
            leaf.Entries.Single(row => row.Key == "knuth1984");
        Assert.Equal("Literate Programming (1984)", knuth.TitleLine);
        Assert.Equal("Knuth — The Computer Journal", knuth.Subtitle);
        Assert.Equal("1984", knuth.YearText);
        Assert.Equal(
            "Title: Literate Programming. Authors: Knuth, Donald E.. Year: 1984. "
                + "Journal: The Computer Journal. Key: knuth1984.",
            knuth.RowDescription);

        BibliographyRowViewModel lamport =
            leaf.Entries.Single(row => row.Key == "lamport1994");
        Assert.Equal("Lamport, Bibby", lamport.Subtitle);
    }

    [Fact]
    public void SearchUsesTheMacPredicateOverTheLoadedSet()
    {
        var leaf = MakeLeaf();
        leaf.EnsureLoaded();
        int queries = leaf.EntriesRequestIdForTests;

        // Title substring…
        leaf.SearchText = "literate";
        Assert.Equal("knuth1984", Assert.Single(leaf.Entries).Key);
        // …KEY, which core's SearchBibliography does not match (D-4)…
        leaf.SearchText = "lamport1994";
        Assert.Equal("lamport1994", Assert.Single(leaf.Entries).Key);
        // …author family…
        leaf.SearchText = "knuth";
        Assert.Equal("knuth1984", Assert.Single(leaf.Entries).Key);
        // …and GIVEN name, which core also does not match.
        leaf.SearchText = "leslie";
        Assert.Equal("lamport1994", Assert.Single(leaf.Entries).Key);

        // No hits gets its own sentence, distinct from "no entries".
        leaf.SearchText = "zzz";
        Assert.Empty(leaf.Entries);
        Assert.True(leaf.ShowNoFilterHitsState);
        Assert.Equal("No entries match 'zzz'.", leaf.NoFilterHitsText);

        // Clearing restores everything.
        leaf.SearchText = "";
        Assert.Equal(2, leaf.Entries.Count);

        // Filtering NEVER touched core.
        Assert.Equal(queries, leaf.EntriesRequestIdForTests);
    }

    [Fact]
    public void FilteringIsCaseAndWhitespaceInsensitive()
    {
        Assert.Single(
            BibliographyViewModel.Matching(
                [Entry("k", "Literate Programming", new Author("Knuth", "Donald"))],
                "  LITERATE  "));
        Assert.Empty(
            BibliographyViewModel.Matching(
                [Entry("k", "Literate", new Author("Knuth", "Donald"))], "xyz"));
        // An empty query returns everything untouched.
        var all = new[] { Entry("a", "A"), Entry("b", "B") };
        Assert.Equal(2, BibliographyViewModel.Matching(all, "   ").Count());
    }

    [Fact]
    public void UnresolvedSegmentLoadsOnDemandAndCarriesTheMacRowLabel()
    {
        var leaf = MakeLeaf();
        leaf.EnsureLoaded();
        Assert.Empty(leaf.Unresolved);

        leaf.Segment = BibliographySegment.Unresolved;
        UnresolvedRowViewModel row = Assert.Single(leaf.Unresolved);
        Assert.Equal("ghostkey", row.Key);
        Assert.Equal("cited.md", row.Path);
        Assert.Equal(
            "Unresolved key: ghostkey in cited.md.", row.RowDescription);
        Assert.False(leaf.ShowUnresolvedEmptyState);
    }

    [Fact]
    public void SegmentSwitchingNeverAnnouncesAndNeverReQueries()
    {
        var announced = new List<A11yEvent>();
        var leaf = MakeLeaf(announced);
        leaf.EnsureLoaded();
        leaf.Segment = BibliographySegment.Unresolved;
        int entryQueries = leaf.EntriesRequestIdForTests;

        leaf.Segment = BibliographySegment.Entries;
        leaf.Segment = BibliographySegment.Unresolved;
        leaf.Segment = BibliographySegment.Entries;

        Assert.Empty(announced);
        Assert.Equal(entryQueries, leaf.EntriesRequestIdForTests);
    }

    [Fact]
    public void StalePublishesAreDiscardedForBothSegments()
    {
        var leaf = MakeLeaf();
        leaf.EnsureLoaded();
        int rows = leaf.Entries.Count;

        leaf.PublishEntries(long.MaxValue, int.MaxValue, [Entry("x", "Ghost")], null);
        leaf.PublishEntries(0, -1, [], "should not surface");
        leaf.PublishUnresolved(long.MaxValue, int.MaxValue, [], "nor this");

        Assert.Equal(rows, leaf.Entries.Count);
        Assert.Null(leaf.EntriesError);
        Assert.Null(leaf.UnresolvedError);
    }

    [Fact]
    public void ALoadFailureClearsRowsAndSpeaksTheBibliographyError()
    {
        var leaf = MakeLeaf();
        leaf.EnsureLoaded();
        Assert.NotEmpty(leaf.Entries);

        leaf.PublishEntries(
            leaf.GenerationForTests,
            leaf.EntriesRequestIdForTests,
            [],
            "bibliography on non-filesystem vault");

        Assert.Empty(leaf.Entries);
        Assert.Equal(
            "Bibliography couldn't be loaded. bibliography on non-filesystem vault",
            leaf.EntriesErrorSpoken);
        // An error is not the same state as "no sources configured".
        Assert.False(leaf.ShowNoSourcesState);
        Assert.False(leaf.ShowNoFilterHitsState);
    }

    [Fact]
    public void NoConfiguredSourcesIsItsOwnStateWithTheWindowsCopy()
    {
        var leaf = MakeLeaf();
        // Contract 5: sources absent is distinct from sources present
        // but empty, and from a load error.
        leaf.ApplySeedOutcome([], hasSources: false);
        leaf.EnsureLoaded();

        Assert.True(leaf.ShowNoSourcesState);
        Assert.Equal(
            "No bibliography sources configured. "
                + "Add a \"citations\" section to the vault's slate.json.",
            CitationPhrase.BibliographyNoSources);
    }

    [Fact]
    public void SeedWarningsAreSurfacedVerbatim()
    {
        var leaf = MakeLeaf();
        leaf.ApplySeedOutcome(
            ["missing.bib: no such file or directory"], hasSources: true);
        leaf.EnsureLoaded();

        // Contract 5: never swallowed — the notice names the source.
        string notice = Assert.Single(leaf.LoadNotices);
        Assert.Equal("missing.bib: no such file or directory", notice);
        Assert.False(leaf.ShowNoSourcesState);
    }

    [Fact]
    public void TheEntryCapIsAnnouncedInTheSummaryRatherThanSilent()
    {
        var leaf = MakeLeaf();
        var many = Enumerable.Range(0, BibliographyViewModel.MaxEntryRows + 25)
            .Select(i => Entry($"k{i}", $"Title {i}"))
            .ToArray();

        leaf.EnsureLoaded();
        leaf.PublishEntries(
            leaf.GenerationForTests, leaf.EntriesRequestIdForTests, many, null);

        // Contract 9: bounded, but the bound is SPOKEN.
        Assert.Equal(BibliographyViewModel.MaxEntryRows, leaf.Entries.Count);
        Assert.Contains(
            $"Showing the first {BibliographyViewModel.MaxEntryRows} of {many.Length} entries.",
            leaf.EntriesSummary,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnUncappedSetSummarisesWithoutATruncationNotice()
    {
        var leaf = MakeLeaf();
        leaf.EnsureLoaded();
        Assert.Equal("2 entries", leaf.EntriesSummary);
        Assert.DoesNotContain("Showing the first", leaf.EntriesSummary);
    }

    [Fact]
    public void TheLeafNeverWritesToTheVault()
    {
        // Contract 6: exercise every read path and assert the note's
        // bytes are untouched. The leaf must never call
        // SetBibliographySources — that is the workspace's seam.
        string notePath = Path.Combine(_fixture.Root, "cited.md");
        string before = File.ReadAllText(notePath);
        string bibBefore = File.ReadAllText(Path.Combine(_fixture.Root, "library.bib"));

        var leaf = MakeLeaf();
        leaf.EnsureLoaded();
        leaf.SearchText = "knuth";
        leaf.Segment = BibliographySegment.Unresolved;
        leaf.Segment = BibliographySegment.Entries;
        leaf.ForceReload();

        Assert.Equal(before, File.ReadAllText(notePath));
        Assert.Equal(bibBefore, File.ReadAllText(Path.Combine(_fixture.Root, "library.bib")));
    }
}
