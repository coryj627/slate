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

    private BibliographyViewModel MakeLeaf() =>
        new(_session, synchronousForTests: true);

    private static BibEntry Entry(
        string key, string title, params Author[] authors) =>
        new(key, "article", title, authors, 2000, null, null, null, null, null);

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

    /// <summary>
    /// A segment switch never re-queries.
    ///
    /// This used to assert "never announces" too — but the leaf held an
    /// announce callback it never invoked, so that half could not fail
    /// for any mutation short of adding a call that did not exist. The
    /// callback is gone; §2.6 silence is now a property of the type
    /// rather than something a test claims to check.
    /// </summary>
    [Fact]
    public void SegmentSwitchingNeverReQueries()
    {
        var leaf = MakeLeaf();
        leaf.EnsureLoaded();
        leaf.Segment = BibliographySegment.Unresolved;
        int entryQueries = leaf.EntriesRequestIdForTests;

        leaf.Segment = BibliographySegment.Entries;
        leaf.Segment = BibliographySegment.Unresolved;
        leaf.Segment = BibliographySegment.Entries;

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

    /// <summary>
    /// Contract 5: sources absent stays a DISTINCT state from sources
    /// present but empty, and from a load error — that distinction
    /// lives in <c>HasNoSources</c>.
    ///
    /// The visible sentence, though, follows mac exactly
    /// (BibliographyPanel.swift:104-108): it appears when the loaded
    /// entry set is EMPTY, and mac uses this one sentence for both
    /// cases. This test previously asserted the sentence showed while
    /// two entries were on screen, which is the contradiction it now
    /// pins shut.
    /// </summary>
    [Fact]
    public void NoConfiguredSourcesIsItsOwnStateWithTheWindowsCopy()
    {
        var leaf = MakeLeaf();
        leaf.ApplySeedOutcome(
            new BibliographySeedOutcome(BibliographySeedStatus.NoSources, []));
        leaf.EnsureLoaded();

        // The distinct state survives...
        Assert.True(leaf.HasNoSources);
        // ...but this fixture's session really does hold entries, and a
        // panel showing two rows must not also claim nothing is
        // configured.
        Assert.NotEmpty(leaf.Entries);
        Assert.False(leaf.ShowNoSourcesState);
        Assert.Equal(
            "No bibliography sources configured. "
                + "Add a \"citations\" section to the vault's slate.json.",
            CitationPhrase.BibliographyNoSources);
    }

    /// <summary>
    /// A FAILED seed must not be dressed up as any other state.
    ///
    /// The D-13 refusal publishes an empty set with a NULL error — that
    /// is the whole point, it did not ask core anything. But the empty
    /// set then satisfies two sentences that are both false:
    /// "No bibliography sources configured. Add a "citations" section
    /// to the vault's slate.json." (sources ARE configured; the path in
    /// them is broken, so the instruction sends the user to fix
    /// something already correct) and, on the other segment, "No
    /// unresolved citations. Every key in your notes has a bibliography
    /// entry." — a positive factual claim about a query that was
    /// refused.
    ///
    /// Both are asserted here because they are ONE defect: the refusal
    /// is not a distinct state, so every empty-set sentence adopts it.
    /// The notices region carries the real reason (contract 5).
    ///
    /// NOTE: both segments are exercised WITHOUT switching away, so
    /// neither assertion can pass merely because its segment is
    /// inactive — the way the two existing assertions elsewhere in this
    /// file do.
    /// </summary>
    [Fact]
    public void AFailedSeedIsItsOwnStateAndBorrowsNoOtherSentence()
    {
        var leaf = MakeLeaf();
        var seed = new BibliographySeed();
        seed.Complete(new BibliographySeedOutcome(
            BibliographySeedStatus.Failed, ["library.bib: no such file or directory"]));
        leaf.AttachSeed(seed);
        leaf.ApplySeedOutcome(seed.Outcome!);

        leaf.EnsureLoaded();

        // Entries segment is active here — the guard cannot mask this.
        Assert.True(leaf.ShowEntries);
        Assert.Empty(leaf.Entries);
        Assert.False(leaf.ShowNoSourcesState);

        leaf.Segment = BibliographySegment.Unresolved;

        Assert.True(leaf.ShowUnresolved);
        Assert.Empty(leaf.Unresolved);
        Assert.False(leaf.ShowUnresolvedEmptyState);

        // The reason is still carried, verbatim.
        Assert.Equal(
            "library.bib: no such file or directory",
            Assert.Single(leaf.LoadNotices));
    }

    /// <summary>The other half: an empty entry set DOES surface the
    /// sentence. Without this, a vault whose sources loaded to nothing
    /// showed a silent "0 entries" and no explanation at all.</summary>
    [Fact]
    public void AnEmptyEntrySetSurfacesTheNoSourcesSentence()
    {
        using var bare = FixtureVault.Create(0, "bibliography-empty");
        File.WriteAllText(Path.Combine(bare.Root, "note.md"), "# Note\n");
        using var session = VaultSession.OpenFilesystem(bare.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        var leaf = new BibliographyViewModel(session, synchronousForTests: true);
        leaf.ApplySeedOutcome(
            new BibliographySeedOutcome(BibliographySeedStatus.NoSources, []));

        leaf.EnsureLoaded();

        Assert.Empty(leaf.Entries);
        Assert.True(leaf.ShowNoSourcesState);
    }

    /// <summary>
    /// Every state line is segment-scoped. The unresolved segment loads
    /// LAZILY, so "No unresolved citations. Every key in your notes has
    /// a bibliography entry." used to render above the entries grid on
    /// first reveal — a factual claim about a query that had never run.
    /// </summary>
    /// <summary>
    /// The SEGMENT GUARD is what hides the other segment's lines.
    ///
    /// The first version of this test asserted every cross-segment flag
    /// was false in a state where each was ALREADY false for its own
    /// reasons — no error set, nothing loading, the unresolved query
    /// never run. Deleting every `ShowEntries &amp;&amp;` / `ShowUnresolved &amp;&amp;`
    /// from the view model left it green, so the behaviour it was
    /// written for could have been reverted wholesale without a single
    /// failure.
    ///
    /// Both segments now carry a LIVE error at once, so the guard is
    /// the only thing that can hide either line.
    /// </summary>
    [Fact]
    public void SegmentScopingIsWhatHidesTheOtherSegmentsLines()
    {
        var leaf = MakeLeaf();
        leaf.ApplySeedOutcome(
            new BibliographySeedOutcome(BibliographySeedStatus.Seeded, []));
        leaf.EnsureLoaded();
        leaf.Segment = BibliographySegment.Unresolved;

        leaf.PublishEntries(
            leaf.GenerationForTests, leaf.EntriesRequestIdForTests, [], "entries boom");
        leaf.PublishUnresolved(
            leaf.GenerationForTests, leaf.UnresolvedRequestIdForTests, [], "unresolved boom");

        // Both errors are live. On the unresolved segment only its own
        // shows; the entries error is hidden by the guard alone.
        Assert.NotNull(leaf.UnresolvedError);
        Assert.NotNull(leaf.EntriesError);
        Assert.True(leaf.ShowUnresolvedError);
        Assert.False(leaf.ShowEntriesError);

        leaf.Segment = BibliographySegment.Entries;

        Assert.True(leaf.ShowEntriesError);
        Assert.False(leaf.ShowUnresolvedError);
    }

    [Fact]
    public void SeedWarningsAreSurfacedVerbatim()
    {
        var leaf = MakeLeaf();
        leaf.ApplySeedOutcome(new BibliographySeedOutcome(
            BibliographySeedStatus.Seeded, ["missing.bib: no such file or directory"]));
        leaf.EnsureLoaded();

        // Contract 5: never swallowed — the notice names the source.
        string notice = Assert.Single(leaf.LoadNotices);
        Assert.Equal("missing.bib: no such file or directory", notice);
        Assert.False(leaf.ShowNoSourcesState);
    }

    /// <summary>
    /// The stale-on-failure defect, fixed (D-13). This fixture's
    /// session HAS seeded entries — exactly the condition that makes
    /// the bug reachable: core's index is live from an earlier
    /// successful seed, so a query would answer with two entries while
    /// the notice says loading failed. Presenting them would be stale
    /// bytes served as authoritative, which contract 5 forbids.
    ///
    /// Discrimination: with the refusal removed this publishes the two
    /// fixture entries and the assertion fails.
    /// </summary>
    [Fact]
    public void AFailedSeedPublishesNothingRatherThanLastSessionsEntries()
    {
        // Core really would answer — prove it before asserting refusal,
        // so a fixture that silently stopped loading cannot pass this.
        Assert.NotEmpty(_session.GetBibliographyEntries());

        var leaf = MakeLeaf();
        var seed = new BibliographySeed();
        seed.Complete(new BibliographySeedOutcome(
            BibliographySeedStatus.Failed, ["library.bib: permission denied"]));
        leaf.AttachSeed(seed);
        leaf.ApplySeedOutcome(seed.Outcome!);

        leaf.EnsureLoaded();
        leaf.Segment = BibliographySegment.Unresolved;

        Assert.Empty(leaf.Entries);
        Assert.Empty(leaf.Unresolved);
        // The notice region carries the reason, verbatim (contract 5).
        Assert.Equal("library.bib: permission denied", Assert.Single(leaf.LoadNotices));
        // NOT "no sources configured" — sources existed and failed.
        Assert.False(leaf.ShowNoSourcesState);
    }

    /// <summary>A successful seed still reads core, so the refusal
    /// above cannot be a blanket "never query" regression.</summary>
    [Fact]
    public void ASeededOutcomeStillReadsCore()
    {
        var leaf = MakeLeaf();
        var seed = new BibliographySeed();
        seed.Complete(new BibliographySeedOutcome(BibliographySeedStatus.Seeded, []));
        leaf.AttachSeed(seed);

        leaf.EnsureLoaded();

        Assert.NotEmpty(leaf.Entries);
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
