// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-5 (#737): the workspace seam — source seeding, the two chorded
/// commands, and the overlay lifecycle. Contracts 4 (announcement
/// identity), 5 (degradation honesty) and 6 (read-only safety).
/// </summary>
public sealed class CitationWorkspaceSeamTests : IDisposable
{
    private readonly FixtureVault _fixture;

    public CitationWorkspaceSeamTests()
    {
        _fixture = FixtureVault.Create(0, "citation-seam");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "library.bib"),
            "@article{knuth1984,\n  title = {Literate Programming},\n"
                + "  author = {Knuth, Donald E.},\n  year = {1984}\n}\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "cited.md"),
            "# Cited\n\nA citation [@knuth1984] and a ghost [@ghostkey].\n");
        File.Copy(
            Path.Combine(RepoRoot, "demo-vault", "csl", "ieee.csl"),
            Path.Combine(_fixture.Root, "ieee.csl"));
        WriteConfig(ConfigWithStyle);
    }

    /// <summary>A style is REQUIRED for rows to render; without one
    /// every row is a placeholder and nothing can be expanded.</summary>
    private const string ConfigWithStyle =
        "{\"citations\":{\"bibliography\":\"library.bib\",\"cite_style\":\"ieee\"}}";

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "demo-vault")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
        }
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_fixture.Root, "slate.json"), json);

    public void Dispose() => _fixture.Dispose();

    private VaultSession OpenScanned()
    {
        var session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    private WorkspaceViewModel MakeWorkspace(
        VaultSession session, List<A11yEvent> announced) =>
        new(session, _fixture.Root, () => [], announced.Add,
            startInteractionBackgroundWork: false);

    [Fact]
    public void SeedingLoadsTheBibliographyAndIsSilentOnSuccess()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeWorkspace(session, announced);

        // Contract 5 / §2.6: vault open never speaks bibliography copy.
        Assert.Empty(announced.OfType<A11yEvent.HostComposed>());
        Assert.Empty(workspace.Bibliography.LoadNotices);
        Assert.False(workspace.Bibliography.HasNoSources);

        workspace.Bibliography.EnsureLoaded();
        Assert.Equal("knuth1984", Assert.Single(workspace.Bibliography.Entries).Key);
    }

    [Fact]
    public void AnUnreadableSourceBecomesANoticeNotAnExceptionOrAnAnnouncement()
    {
        // Core's SetBibliographySources is ALL-OR-NOTHING: a missing
        // file aborts the whole call. That is a vault-health
        // condition, so the vault still opens and the leaf explains
        // itself (contract 5).
        WriteConfig("{\"citations\":{\"bibliography\":\"nowhere.bib\"}}");
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeWorkspace(session, announced);

        string notice = Assert.Single(workspace.Bibliography.LoadNotices);
        Assert.Contains("nowhere.bib", notice, StringComparison.OrdinalIgnoreCase);
        // Silent: the notice region is the surface, not speech.
        Assert.Empty(announced.OfType<A11yEvent.HostComposed>());
        // A failed load is NOT "no sources configured".
        Assert.False(workspace.Bibliography.HasNoSources);
        Assert.False(workspace.Bibliography.ShowNoSourcesState);
    }

    [Fact]
    public void AVaultWithNoCitationConfigReportsNoSourcesRatherThanAnError()
    {
        WriteConfig("{}");
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeWorkspace(session, announced);

        Assert.True(workspace.Bibliography.HasNoSources);
        Assert.Empty(workspace.Bibliography.LoadNotices);
        workspace.Bibliography.EnsureLoaded();
        Assert.True(workspace.Bibliography.ShowNoSourcesState);
    }

    [Fact]
    public void TheCitationsLeafFollowsTheActiveNote()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeWorkspace(session, announced);

        workspace.OpenPath("cited.md");
        Assert.Equal("cited.md", workspace.Citations.Path);
        Assert.Equal(2, workspace.Citations.Rows.Count);
        Assert.Single(announced.OfType<A11yEvent.CitationsCount>());
    }

    [Fact]
    public void RevealingTheBibliographyLeafLoadsItOnceAndAnnouncesTheLeafOnly()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeWorkspace(session, announced);

        workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(
            leaf => leaf.Id == "bibliography");
        Assert.NotEmpty(workspace.Bibliography.Entries);
        int queries = workspace.Bibliography.EntriesRequestIdForTests;

        // A4 only — the canonical leaf announcement, already shipped.
        Assert.Contains(
            announced.OfType<A11yEvent.LeafPanelShown>(),
            e => e.Title == "Bibliography");
        Assert.Empty(announced.OfType<A11yEvent.HostComposed>());

        // Re-revealing must not re-query the vault.
        workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "citations");
        workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(
            leaf => leaf.Id == "bibliography");
        Assert.Equal(queries, workspace.Bibliography.EntriesRequestIdForTests);
    }

    [Fact]
    public void SummaryCountsComeFromTheLeafWithoutReReading()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeWorkspace(session, announced);
        workspace.OpenPath("cited.md");

        workspace.OpenCitationSummary();
        CitationSummaryViewModel summary = workspace.CitationSummary!;
        Assert.Equal(2, summary.Total);
        Assert.Equal(2, summary.Unique);
        // Opening the sheet announces nothing (§2.6).
        Assert.Empty(announced.OfType<A11yEvent.CitationWalkThrough>());

        summary.WalkThrough();
        Assert.Null(workspace.CitationSummary);
        Assert.Single(announced.OfType<A11yEvent.CitationWalkThrough>());
    }

    [Fact]
    public void JumpToBibliographyAnnouncesWhichOutcomeHappened()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeWorkspace(session, announced);
        workspace.OpenPath("cited.md");
        workspace.Bibliography.EnsureLoaded();

        // Disabled until something is expanded (mac parity).
        Assert.False(workspace.JumpToBibliographyCommand.CanExecute(null));

        // Resolved key present in the loaded entries → "Jumped to".
        workspace.OpenCitationDetails(
            workspace.Citations.Rows.First(row => !row.IsUnresolved));
        Assert.True(workspace.JumpToBibliographyCommand.CanExecute(null));
        workspace.JumpToBibliography();
        Assert.Equal("bibliography", workspace.ActiveLeaf.Id);
        Assert.Contains(
            announced.OfType<A11yEvent.HostComposed>(),
            e => e.Text == "Jumped to bibliography entry: knuth1984.");
        Assert.Equal("knuth1984", workspace.Bibliography.ConsumeKeyFocusRequest());

        // Unresolved key absent from the entries → "Searching for".
        workspace.OpenCitationDetails(
            workspace.Citations.Rows.First(row => row.IsUnresolved));
        workspace.JumpToBibliography();
        Assert.Contains(
            announced.OfType<A11yEvent.HostComposed>(),
            e => e.Text == "Searching bibliography for: ghostkey.");
    }

    /// <summary>
    /// The announcement must not be able to outrun the focus move.
    ///
    /// "Jumped to bibliography entry: X." is answered from the LOADED
    /// set, but the view focuses a row from the BOUND set — filtered by
    /// the search box, scoped to the entries segment, and present only
    /// if the pane is visible at all. Each of those three could be out
    /// of step, and every one of them turned the announcement into a
    /// confident lie: the user hears they arrived somewhere they never
    /// went. mac avoids it by setting the search text and the segment
    /// and dismissing the popover before announcing
    /// (AppState.swift:12307-12322).
    /// </summary>
    [Fact]
    public void JumpToBibliographyMakesTheTargetReachableBeforeAnnouncingArrival()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeWorkspace(session, announced);
        workspace.OpenPath("cited.md");
        workspace.Bibliography.EnsureLoaded();

        // The three states that each broke it, all at once.
        workspace.IsRightPaneVisible = false;
        workspace.Bibliography.Segment = BibliographySegment.Unresolved;
        workspace.Bibliography.SearchText = "zzzz-matches-nothing";
        Assert.Empty(workspace.Bibliography.Entries);

        workspace.OpenCitationDetails(
            workspace.Citations.Rows.First(row => !row.IsUnresolved));
        workspace.JumpToBibliography();

        // The leaf is reachable, on the right segment...
        Assert.True(workspace.IsRightPaneVisible);
        Assert.Equal("bibliography", workspace.ActiveLeaf.Id);
        Assert.True(workspace.Bibliography.ShowEntries);
        // ...the focus-trapped sheet is out of the way (mac clears
        // expandedCitation)...
        Assert.Null(workspace.CitationDetails);
        // ...and the row the announcement names is actually in the
        // bound set the grid will search.
        Assert.Contains(
            workspace.Bibliography.Entries,
            row => row.Key == "knuth1984");
        Assert.Contains(
            announced.OfType<A11yEvent.HostComposed>(),
            e => e.Text == "Jumped to bibliography entry: knuth1984.");
    }

    /// <summary>
    /// A save must reach the citations leaf.
    ///
    /// Every save path called <c>Panels.NoteSaved</c> and none of them
    /// told this leaf, while <c>Citations.Refresh</c> — whose own doc
    /// called itself the post-save funnel — had no production caller at
    /// all. So adding a citation and pressing Ctrl+S left the panel
    /// showing the old rows indefinitely; only switching notes and
    /// coming back repaired it. All ten save sites now route through
    /// one funnel so the next surface cannot be forgotten the same way.
    /// </summary>
    [Fact]
    public void SavingANoteRefreshesTheCitationsLeaf()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeWorkspace(session, announced);
        workspace.OpenPath("cited.md");
        Assert.Equal(2, workspace.Citations.Rows.Count);

        var tab = Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        tab.Text = "# Cited\n\nA citation [@knuth1984], a ghost [@ghostkey], "
            + "and a third [@newkey].\n";
        workspace.SaveActiveCommand.Execute(null);

        Assert.Equal(3, workspace.Citations.Rows.Count);
    }

    [Fact]
    public void TheSuiteAddsExactlyTwoHostComposedTexts()
    {
        // Residue budget (§2.6): A5 and A6 at one site. Everything
        // else in the suite speaks canonical core events.
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeWorkspace(session, announced);
        workspace.OpenPath("cited.md");
        workspace.Bibliography.EnsureLoaded();
        workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(
            leaf => leaf.Id == "bibliography");
        workspace.OpenCitationSummary();
        workspace.CitationSummary!.WalkThrough();
        workspace.OpenEntryDetails(workspace.Bibliography.Entries[0].Entry);
        workspace.OpenFilesCiting("knuth1984");
        workspace.AnnounceInsertCitationUnavailable();

        var residue = announced.OfType<A11yEvent.HostComposed>()
            .Select(e => e.Text)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Assert.Empty(residue);

        // Only the jump produces residue, and only these two texts.
        workspace.JumpToBibliography();
        residue = announced.OfType<A11yEvent.HostComposed>()
            .Select(e => e.Text)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Assert.Single(residue);
        Assert.StartsWith("Jumped to bibliography entry:", residue[0], StringComparison.Ordinal);
    }

    [Fact]
    public void InsertCitationIsUnavailableButDiscoverable()
    {
        // O1: core exports no citation mutator, so the product answer
        // IS the announcement. The action stays enabled on purpose.
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeWorkspace(session, announced);

        workspace.AnnounceInsertCitationUnavailable();
        Assert.Single(announced.OfType<A11yEvent.CitationInsertUnavailable>());
    }

    [Fact]
    public void FilesCitingResolvesThroughTheWorkspace()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeWorkspace(session, announced);

        workspace.OpenFilesCiting("knuth1984");
        Assert.Equal("cited.md", Assert.Single(workspace.FilesCiting!.Paths));
        Assert.Equal(
            "Files citing this entry. 1 file.", workspace.FilesCiting.AutomationName);

        workspace.CloseFilesCitingCommand.Execute(null);
        Assert.Null(workspace.FilesCiting);
    }

    [Fact]
    public void APlaceholderRowHasNothingHonestToExpand()
    {
        // Contract 2: with NO cite_style configured core never looked
        // a citation up, so every row is a placeholder and there is
        // nothing honest to expand.
        WriteConfig("{\"citations\":{\"bibliography\":\"library.bib\"}}");
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeWorkspace(session, announced);
        workspace.OpenPath("cited.md");

        CitationRowViewModel row = workspace.Citations.Rows[0];
        Assert.Null(row.Rendered);
        Assert.False(row.IsUnresolved);

        workspace.OpenCitationDetails(row);
        Assert.Null(workspace.CitationDetails);
        // …and with nothing expanded, Jump is unavailable.
        Assert.False(workspace.JumpToBibliographyCommand.CanExecute(null));
    }

    [Fact]
    public void TheWholeSuiteNeverWritesANote()
    {
        // Contract 6: exercise both leaves, all three overlays and
        // both commands, then assert the note's bytes are unchanged.
        string notePath = Path.Combine(_fixture.Root, "cited.md");
        string before = File.ReadAllText(notePath);

        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeWorkspace(session, announced);
        workspace.OpenPath("cited.md");
        workspace.Bibliography.EnsureLoaded();
        workspace.Bibliography.SearchText = "knuth";
        workspace.Bibliography.Segment = BibliographySegment.Unresolved;
        workspace.Bibliography.Segment = BibliographySegment.Entries;
        workspace.OpenCitationSummary();
        workspace.CitationSummary!.WalkThrough();
        workspace.OpenCitationDetails(workspace.Citations.Rows[0]);
        workspace.JumpToBibliography();
        workspace.CloseCitationDetailsCommand.Execute(null);
        workspace.OpenFilesCiting("knuth1984");
        workspace.CloseFilesCitingCommand.Execute(null);
        workspace.AnnounceInsertCitationUnavailable();

        Assert.Equal(before, File.ReadAllText(notePath));
    }
}
