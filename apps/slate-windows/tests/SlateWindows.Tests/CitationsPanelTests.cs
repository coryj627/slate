// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-5 (#737): the citations leaf over REAL core output, pinning
/// feature contracts 1 (speech is never re-composed), 2 (placeholder
/// honesty), 3 (staleness), 4 (announcement identity) and 7
/// (unresolved fidelity).
/// </summary>
public sealed class CitationsPanelTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;

    public CitationsPanelTests()
    {
        _fixture = FixtureVault.Create(0, "citations-panel");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "library.bib"),
            "@article{knuth1984,\n  title = {Literate Programming},\n"
                + "  author = {Knuth, Donald E.},\n  year = {1984},\n"
                + "  journal = {The Computer Journal}\n}\n");
        File.Copy(
            Path.Combine(RepoRoot, "demo-vault", "csl", "ieee.csl"),
            Path.Combine(_fixture.Root, "ieee.csl"));
        File.WriteAllText(
            Path.Combine(_fixture.Root, "slate.json"),
            "{\"citations\":{\"bibliography\":\"library.bib\",\"cite_style\":\"ieee\"}}");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "cited.md"),
            "# Cited\n\nResolved [@knuth1984] and unresolved [@nosuchkey].\n");
        File.WriteAllText(Path.Combine(_fixture.Root, "plain.md"), "# Plain\n\nNo citations.\n");
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
        _ = _session.SetBibliographySources(_session.CitationsPrefs().Sources);
    }

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

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    private CitationsPanelViewModel MakePanel(List<A11yEvent>? announced = null) =>
        new(_session, (announced ?? []).Add, synchronousForTests: true);

    [Fact]
    public void RowsSpeakCoreSpeechVerbatimAndFlagUnresolvedFromTheArtifact()
    {
        var panel = MakePanel();
        panel.NoteChanged("cited.md");

        Assert.Null(panel.LoadError);
        Assert.Equal(2, panel.Rows.Count);

        // Contract 1: the resolved row's spoken name IS core's
        // SpeechText — byte for byte, no host composition.
        CitationRowViewModel resolved = panel.Rows[0];
        Assert.NotNull(resolved.Rendered);
        Assert.Equal(resolved.Rendered!.SpeechText, resolved.AutomationName);
        Assert.False(resolved.IsUnresolved);
        Assert.Equal(resolved.Rendered.VisualText, resolved.DisplayText);

        // Contract 7: the unresolved predicate is core-derived, and
        // the ONLY modification of core speech is the badge prefix.
        CitationRowViewModel unresolved = panel.Rows[1];
        Assert.True(unresolved.IsUnresolved);
        Assert.Null(unresolved.Rendered!.BibEntry);
        Assert.NotEmpty(unresolved.Rendered.StyleId);
        Assert.Equal(
            $"Unresolved citation key. {unresolved.Rendered.SpeechText}",
            unresolved.AutomationName);
        Assert.EndsWith(unresolved.Rendered.SpeechText, unresolved.AutomationName);
    }

    [Fact]
    public void AMixedSiteIsClassifiedUnresolvedBecauseCoreReturnsNoEntry()
    {
        // Measured against real core: [@knuth1984; @nosuchkey] comes
        // back with BibEntry == null even though one key resolved, and
        // core's own speech says "… and Unresolved citation: …". The
        // predicate follows core rather than second-guessing it.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "mixed.md"),
            "# Mixed\n\nA mixed site [@knuth1984; @nosuchkey].\n");
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);

        var panel = MakePanel();
        panel.NoteChanged("mixed.md");
        CitationRowViewModel row = Assert.Single(panel.Rows);
        Assert.True(row.IsUnresolved);
        Assert.Contains("knuth", row.Rendered!.SpeechText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nosuchkey", row.Rendered.SpeechText, StringComparison.Ordinal);
    }

    [Fact]
    public void NoConfiguredStyleYieldsPlaceholdersWithoutInventingAStyle()
    {
        // Contract 2: no default style ⇒ every row is a placeholder,
        // no render is attempted, and no Unresolved badge appears —
        // the badge means "core looked and found nothing".
        File.WriteAllText(Path.Combine(_fixture.Root, "slate.json"), "{}");
        using var styleless = VaultSession.OpenFilesystem(_fixture.Root);
        using (var cancel = new CancelToken())
        {
            styleless.ScanInitial(cancel);
        }
        Assert.Null(styleless.CitationsPrefs().DefaultStyle);

        var panel = new CitationsPanelViewModel(
            styleless, _ => { }, synchronousForTests: true);
        panel.NoteChanged("cited.md");

        Assert.Null(panel.LoadError);
        Assert.Equal(2, panel.Rows.Count);
        Assert.All(panel.Rows, row =>
        {
            Assert.Null(row.Rendered);
            Assert.False(row.IsUnresolved);
        });
        Assert.Equal("Citation: knuth1984", panel.Rows[0].AutomationName);
        Assert.Equal("[@knuth1984]", panel.Rows[0].DisplayText);
    }

    [Fact]
    public void TheCountIsAnnouncedOncePerPathAndNeverForAnEmptyNote()
    {
        var announced = new List<A11yEvent>();
        var panel = MakePanel(announced);

        panel.NoteChanged("cited.md");
        Assert.Single(announced.OfType<A11yEvent.CitationsCount>());
        Assert.Equal(
            2u, announced.OfType<A11yEvent.CitationsCount>().Single().Count);

        // A refresh of the SAME path does not re-announce.
        panel.Refresh();
        Assert.Single(announced.OfType<A11yEvent.CitationsCount>());

        // An empty note announces nothing at all…
        panel.NoteChanged("plain.md");
        Assert.Empty(panel.Rows);
        Assert.Single(announced.OfType<A11yEvent.CitationsCount>());
        Assert.True(panel.ShowEmptyState);

        // …and returning to the cited note re-arms the announcement.
        panel.NoteChanged("cited.md");
        Assert.Equal(2, announced.OfType<A11yEvent.CitationsCount>().Count());
    }

    [Fact]
    public void StalePublishesAreDiscardedSilentlyAndCannotAnnounce()
    {
        var announced = new List<A11yEvent>();
        var panel = MakePanel(announced);
        panel.NoteChanged("cited.md");
        int baseline = announced.OfType<A11yEvent.CitationsCount>().Count();
        int rows = panel.Rows.Count;

        // Contract 3: a stale generation and a stale requestId are
        // both discarded — no rows, no error, no announcement.
        var fabricated = new CitationReference("[@x]", [], 0, 1);
        panel.Publish(long.MaxValue, int.MaxValue, "cited.md", [fabricated], null, null);
        panel.Publish(0, -1, "cited.md", [], null, "should not surface");

        Assert.Equal(rows, panel.Rows.Count);
        Assert.Null(panel.LoadError);
        Assert.Equal(baseline, announced.OfType<A11yEvent.CitationsCount>().Count());
    }

    [Fact]
    public void APathChangeClearsRowsSynchronouslyBeforeTheNewReadLands()
    {
        var panel = MakePanel();
        panel.NoteChanged("cited.md");
        Assert.NotEmpty(panel.Rows);

        // The old note's rows must not remain visible/actionable while
        // the new path loads (contract 3). With no file selected there
        // is no read at all, so the clear is observable directly.
        panel.NoteChanged(null);
        Assert.Empty(panel.Rows);
        Assert.Empty(panel.References);
        Assert.True(panel.ShowNoFileState);
        Assert.False(panel.IsLoading);
    }

    [Fact]
    public void AMissingFileIsEmptyNotAnError()
    {
        // Measured, not assumed: core's ListCitationsInFile returns an
        // EMPTY list for a path it doesn't know rather than throwing.
        // The honest surface is therefore the empty state — showing an
        // error would misreport a file that simply has no citations.
        var panel = MakePanel();
        panel.NoteChanged("cited.md");
        Assert.NotEmpty(panel.Rows);

        panel.NoteChanged("missing-note.md");
        Assert.Null(panel.LoadError);
        Assert.Empty(panel.Rows);
        Assert.True(panel.ShowEmptyState);
    }

    [Fact]
    public void ALoadFailureClearsRowsAndSurfacesTheSpokenError()
    {
        var panel = MakePanel();
        panel.NoteChanged("cited.md");
        Assert.NotEmpty(panel.Rows);

        // Drive the failure through the publish seam: the worker's
        // catch filter funnels every VaultException here, and this is
        // the deterministic way to assert the surface without faking a
        // broken session.
        panel.Publish(
            panel.GenerationForTests,
            panel.RequestIdForTests,
            "cited.md",
            [],
            null,
            "bibliography on non-filesystem vault");

        Assert.Equal("bibliography on non-filesystem vault", panel.LoadError);
        Assert.Empty(panel.Rows);
        Assert.Empty(panel.References);
        Assert.Equal(
            "Citations couldn't be loaded. bibliography on non-filesystem vault",
            panel.ErrorSpoken);
        // An error is NOT an empty note — the states are distinct.
        Assert.False(panel.ShowEmptyState);
    }

    [Fact]
    public void ReferencesStayOneToOneWithRowsForTheSummaryCount()
    {
        // Contract 12 depends on this: the summary counts unique keys
        // from References, so they must be published together with the
        // rows they produced.
        var panel = MakePanel();
        panel.NoteChanged("cited.md");
        Assert.Equal(panel.Rows.Count, panel.References.Count);
        for (int i = 0; i < panel.Rows.Count; i++)
        {
            Assert.Same(panel.References[i], panel.Rows[i].Reference);
        }
    }
}
