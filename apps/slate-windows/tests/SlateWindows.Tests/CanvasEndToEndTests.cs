// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using System.Diagnostics;
using System.Windows.Controls;
using SlateWindows.Canvas;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 §H (H1): the end-to-end suite in the mac shape
/// (<c>MilestoneTIntegrationTests</c>) — through the REAL
/// <see cref="VaultSession"/> and the document view-model, never a fake
/// source, never a direct FFI apply — over the committed
/// <c>sample.canvas</c> copied from the repository's fixtures.
/// </summary>
public sealed class CanvasEndToEndTests : IDisposable
{
    private const string Canvas = "sample.canvas";

    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;
    private readonly List<RenderedAnnouncement> _announced = [];
    private CanvasVerbosity _verbosity = CanvasVerbosity.Standard;

    public CanvasEndToEndTests()
    {
        _fixture = FixtureVault.Create(2, "canvas-e2e");
        // The committed fixture, byte for byte — never a test-authored
        // twin (H1, HD-1). The notes it references exist so the file
        // cards resolve the way the mac twin's do.
        File.Copy(CommittedFixture(Canvas), Path.Combine(_fixture.Root, Canvas));
        Directory.CreateDirectory(Path.Combine(_fixture.Root, "notes"));
        Directory.CreateDirectory(Path.Combine(_fixture.Root, "specs"));
        File.WriteAllText(
            Path.Combine(_fixture.Root, "notes", "canvas research.md"),
            "# Canvas research\n\nNotes.\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "specs", "interaction.md"),
            "# Interaction\n\n## Announcement grammar\n\nThe grammar.\n");
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    internal static string CommittedFixture(string name) =>
        Path.Combine(
            SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures", "canvas", name);

    private CanvasDocumentViewModel Open() => Open(Canvas);

    private CanvasDocumentViewModel Open(string path)
    {
        var document = new CanvasDocumentViewModel(
            _session,
            path,
            new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
            synchronousForTests: true,
            verbosity: () => _verbosity);
        document.Load();
        Assert.Equal(CanvasLoadState.Ready, document.State);
        return document;
    }

    /// <summary>The mac twin's first test, plus the sort leg the spec
    /// names: nine rows on every projection; the group first (reading
    /// order, groups before members); the preset and the custom colour
    /// names; the subpath separator in a file card's title; one
    /// navigator step seats a selection; Where-am-I renders a readback.
    /// Then the table sorted by the PRODUCTION command — the one
    /// Ctrl+Alt+S runs — against a PINNED oracle in both directions:
    /// the Type cells in core's kind order, and the ids each type block
    /// holds (the comparator is Type-only, so the order inside a block
    /// is not a contract). The whole fact runs on one STA thread —
    /// document, surface and grid created there — because the announcer
    /// captures its creation dispatcher (IH-33).</summary>
    [Fact]
    public void OpenSampleExposesOutlineTableAndScene() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open();

        Assert.Equal(9, document.Outline.Count);
        CanvasPublication applied = document.AppliedPublication!;
        Assert.Equal(9, applied.Loaded!.Population.SceneNodes.Count());
        Assert.Equal("group", document.Outline[0].Kind);
        string[] colours = document.Outline
            .Select(row => row.ColorName)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToArray();
        Assert.Contains("red", colours);
        Assert.Contains("purple (custom)", colours);
        Assert.Contains(document.Outline, row => row.Title.Contains(" › ", StringComparison.Ordinal));

        document.Navigator.NextCard();
        Assert.NotNull(document.Selection.Selected);
        document.Navigator.WhereAmI();
        Assert.False(string.IsNullOrWhiteSpace(document.WhereAmIText));

        // ---- The table: nine rows, then the production sort -------
        var surface = new CanvasSurfaceView { Model = document };
        document.ShowSurface(CanvasSurfaceKind.Table);
        AccessibleDataGrid grid = surface.TableForTests.GridForTests;
        Assert.Equal(9, grid.Grid.Items.Cast<CanvasTableRow>().Count());

        // Seat the reader on the Type column (the first) so the toggle
        // sorts by it, as the grid's own facts do.
        CanvasTableRow first = grid.Grid.Items.Cast<CanvasTableRow>().First();
        grid.Grid.CurrentCell = new DataGridCellInfo(first, grid.Grid.Columns[0]);

        AccessibleDataGrid.ToggleSortCommand.Execute(null, grid);
        AssertTypeOrder(grid, ascending: true);
        AccessibleDataGrid.ToggleSortCommand.Execute(null, grid);
        AssertTypeOrder(grid, ascending: false);

        document.Shutdown();
    });

    /// <summary>The oracle is written down, not derived: core's kind
    /// words sort ordinally — file, group, image, link, text — and the
    /// committed fixture has two files, two groups, one image, one link
    /// and three text cards.</summary>
    private static void AssertTypeOrder(AccessibleDataGrid grid, bool ascending)
    {
        string[] ascendingCells =
            ["File", "File", "Group", "Group", "Image", "Link", "Text", "Text", "Text"];
        string[] expectedCells = ascending ? ascendingCells : ascendingCells.Reverse().ToArray();
        CanvasTableRow[] rows = grid.Grid.Items.Cast<CanvasTableRow>().ToArray();
        Assert.Equal(expectedCells, rows.Select(row => CanvasPhrase.TypeCell(row.Kind)).ToArray());

        var blocks = new Dictionary<string, HashSet<string>>
        {
            ["file"] = ["card-notes", "card-spec"],
            ["group"] = ["grp-research", "grp-inspiration"],
            ["image"] = ["card-diagram"],
            ["link"] = ["card-jsoncanvas"],
            ["text"] = ["card-question", "card-evidence", "card-loose"],
        };
        foreach ((string kind, HashSet<string> ids) in blocks)
        {
            Assert.Equal(
                ids,
                rows.Where(row => row.Kind == kind).Select(row => row.NodeId).ToHashSet());
        }
    }

    /// <summary>The mac twin's grammar test: on a row with connections,
    /// the selection sentence at Terse has no " of ", at Standard has
    /// " of ", at Verbose has " of " and names a connection; a delete
    /// speaks the destructive arm with the platform's undo chord as core
    /// renders it; an undo follows and the row is back.</summary>
    [Fact]
    public void AnnouncementGrammarConformsPerVerbosity()
    {
        CanvasDocumentViewModel document = Open();
        CanvasOutlineRow connected = document.Outline.First(row => row.ConnectionCount > 0);

        (CanvasVerbosity Verbosity, Func<string, bool> Reads)[] table =
        [
            (CanvasVerbosity.Terse, text => !text.Contains(" of ", StringComparison.Ordinal)),
            (CanvasVerbosity.Standard, text => text.Contains(" of ", StringComparison.Ordinal)),
            (CanvasVerbosity.Verbose, text =>
                text.Contains(" of ", StringComparison.Ordinal)
                && text.Contains("connection", StringComparison.Ordinal)),
        ];
        foreach ((CanvasVerbosity verbosity, Func<string, bool> reads) in table)
        {
            _verbosity = verbosity;
            document.SelectNode(null, announce: false);
            _announced.Clear();
            document.SelectNode(connected.NodeId, announce: true);
            // The announcer coalesces inside its window; the flush is
            // the test seam the navigator battery uses.
            document.AnnouncerForTests.FlushForTests();
            RenderedAnnouncement line = Assert.Single(
                _announced,
                a => a.Text.Contains(connected.Title, StringComparison.Ordinal));
            Assert.True(reads(line.Text), $"{verbosity}: \"{line.Text}\"");
        }

        // §1.3's destructive arm, with the platform's undo chord.
        _verbosity = CanvasVerbosity.Standard;
        _announced.Clear();
        document.CanvasDeleteSelection();
        document.AnnouncerForTests.FlushForTests();
        // Review round 1 (IH-64): the sentence is core's destructive arm
        // with the platform's chord (D-6) — "Deleted ⟨card⟩ — Ctrl+Z to
        // undo" — not any host line that mentions undo.
        Assert.Contains(
            _announced,
            a => a.Text.StartsWith("Deleted ", StringComparison.Ordinal)
                && a.Text.EndsWith(" — Ctrl+Z to undo", StringComparison.Ordinal));
        Assert.Equal(8, document.Outline.Count);
        document.CanvasUndo();
        Assert.Equal(9, document.Outline.Count);
        document.Shutdown();
    }

    /// <summary>The mac twin's authoring test, through the production
    /// members and nothing else (IH-32): the baseline is the COMMITTED
    /// bytes — the copy equals them before the first verb (HD-1) — then
    /// create (the card lands selected), edit, connect to the anchor
    /// (mac's: the first non-group row that is not the new card), mark
    /// the new card and the anchor with the selection RESEATED between
    /// them, group the marked set, reseat the new card, attach a pane
    /// and move it by one large step, commit. Five history entries —
    /// the offered undo stands before each of five undos and is gone
    /// after the fifth — and the file is the committed bytes again, the
    /// outline its original ids.</summary>
    [Fact]
    public void AuthoringLoopThenUndoChainRestoresTheCommittedBytes()
    {
        CanvasDocumentViewModel document = Open();
        string path = Path.Combine(_fixture.Root, Canvas);
        byte[] committed = File.ReadAllBytes(CommittedFixture(Canvas));
        Assert.Equal(committed, File.ReadAllBytes(path));
        Assert.Null(document.UndoStack.OfferedUndo);
        string[] originalIds = document.Outline.Select(row => row.NodeId).ToArray();

        // 1. Create — the new card lands selected.
        document.CanvasNewCard();
        string newId = Assert.IsType<string>(document.Selection.Selected);
        Assert.DoesNotContain(newId, originalIds);
        string anchor = document.Outline
            .First(row => row.Kind != "group" && row.NodeId != newId)
            .NodeId;

        // 2. Edit the new card's text.
        document.CanvasCommitCardEdit(newId, "E2E card");
        Assert.Equal("E2E card", document.Outline.First(row => row.NodeId == newId).Title);

        // 3. Connect it to the anchor.
        document.CanvasConnect(newId, anchor, "e2e");
        Assert.Single(document.NeighborsOf(newId));

        // 4. Mark both — reseating between the toggles — and group the
        //    marked set.
        document.SelectNode(newId, announce: false);
        document.ToggleMark();
        document.SelectNode(anchor, announce: false);
        document.ToggleMark();
        Assert.Equal(2, document.AppliedPublication!.MarkedIntent.Count);
        Assert.NotNull(document.SubmitGroupMarked("E2E Zone"));
        Assert.Contains("E2E Zone", document.Outline.First(row => row.NodeId == newId).GroupPath);

        // 5. Move the new card: reseated, a pane attached, one large step,
        //    committed.
        document.SelectNode(newId, announce: false);
        document.Navigator.AttachPresenter(new EndToEndPane());
        Assert.True(document.Navigator.EnterMoveMode(), "move mode did not enter");
        Assert.True(document.Navigator.ModeStep(1, 0, large: true), "the move step was refused");
        Assert.True(document.Modes.Commit(), "the move did not commit");
        Assert.NotEqual(committed, File.ReadAllBytes(path));

        // Five entries, five undos, the committed bytes.
        for (int entry = 1; entry <= 5; entry++)
        {
            Assert.True(
                document.UndoStack.OfferedUndo is not null,
                $"no undo offered before undo {entry} — a verb did not land as one history entry");
            document.CanvasUndo();
        }
        Assert.Null(document.UndoStack.OfferedUndo);
        Assert.Equal(committed, File.ReadAllBytes(path));
        Assert.Equal(originalIds, document.Outline.Select(row => row.NodeId).ToArray());
        document.Shutdown();
    }

    /// <summary>The pane a mode needs attached — the mutation battery's
    /// shape; the E2E never drives a viewport through it.</summary>
    private sealed class EndToEndPane : ICanvasSurfacePresenter
    {
        public CanvasSurfaceKind Projection => CanvasSurfaceKind.Outline;

        public bool ProjectionHasFocus => true;

        public bool CanMoveWithinProjection(bool forward) => true;

        public bool DismissTransientRegion() => false;

        public object? Owner => null;

        public CanvasViewportOutcome ViewportCommand(CanvasViewportVerb verb) => CanvasViewportOutcome.Refused;

        public bool FocusRow(string nodeId) => false;

        public bool FocusProjection() => false;
    }

    /// <summary>The mac twin's §K test, on the committed 2,000-node
    /// fixture: the open within PR A's 500 ms in the synchronous arm;
    /// the peer topology's first windowed derivation over a 1600 × 1200
    /// viewport materialising more than zero and fewer than 600
    /// placements (windowing bounds the UIA tree) under 500 ms; a pan's
    /// window hop averaging under 100 ms over ten hops; and a NAVIGATOR
    /// step — `NextCard` on the real navigator over the real document,
    /// not the benchmark's descriptor lookup (IH-34) — averaging under
    /// 50 ms over fifty steps. `Stopwatch` on the derivation and the
    /// navigator the app runs; the draw and the UIA re-frame ride the app
    /// and the journeys (HD-D1). The measurements are printed as BENCH
    /// lines for the §K roll-up, pasted never typed.</summary>
    [Fact]
    public void LargeCanvasOpensNavigatesAndWindowsUnderBudget()
    {
        const string large = "large_2000.canvas";
        File.Copy(CommittedFixture(large), Path.Combine(_fixture.Root, large));

        var open = Stopwatch.StartNew();
        CanvasDocumentViewModel document = Open(large);
        open.Stop();
        Assert.Equal(2000, document.Outline.Count);
        Assert.True(
            open.ElapsedMilliseconds < 500,
            $"opening the 2,000-node canvas took {open.ElapsedMilliseconds} ms");

        CanvasPopulation population = document.AppliedPublication!.Loaded!.Population;
        CanvasViewportState viewport = CanvasViewportState.Seed().WithViewSize(1600, 1200);
        ImmutableHashSet<CanvasPeerKey> none = [];

        var first = Stopwatch.StartNew();
        int materialized = CanvasPeerTopology.Derive(population, viewport, none).Placements.Count;
        first.Stop();
        Assert.True(materialized > 0, "the first window materialised nothing");
        Assert.True(materialized < 600, $"windowing must bound the UIA tree; {materialized} placements");
        Assert.True(first.Elapsed.TotalMilliseconds < 500, $"first windowed derivation {first.Elapsed.TotalMilliseconds:F3} ms");

        var pans = Stopwatch.StartNew();
        for (int hop = 1; hop <= 10; hop++)
        {
            _ = CanvasPeerTopology.Derive(
                population, viewport.PannedTo(-400 * hop, -250 * hop), none).Placements.Count;
        }
        pans.Stop();
        double panMs = pans.Elapsed.TotalMilliseconds / 10;
        Assert.True(panMs < 100, $"pan window hop averaged {panMs:F3} ms");

        document.SelectNode(document.Outline[0].NodeId, announce: false);
        var steps = Stopwatch.StartNew();
        for (int step = 0; step < 50; step++)
        {
            document.Navigator.NextCard();
        }
        steps.Stop();
        double navMs = steps.Elapsed.TotalMilliseconds / 50;
        Assert.True(navMs < 50, $"navigator step averaged {navMs:F3} ms");
        Assert.NotEqual(document.Outline[0].NodeId, document.Selection.Selected);

        Console.WriteLine(
            $"BENCH canvas_e2e open_2000={open.Elapsed.TotalMilliseconds:F1}ms "
            + $"first_windowed_derivation_2000={first.Elapsed.TotalMilliseconds:F3}ms materialized={materialized} "
            + $"pan_window_hop_2000={panMs:F3}ms nav_step_2000={navMs:F3}ms");
        document.Shutdown();
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception e)
            {
                failure = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException(failure.ToString());
        }
    }
}
