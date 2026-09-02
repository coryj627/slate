// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

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

    private CanvasDocumentViewModel Open()
    {
        var document = new CanvasDocumentViewModel(
            _session,
            Canvas,
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
        Assert.Contains(
            _announced,
            a => a.Text.Contains("Ctrl+Z", StringComparison.Ordinal)
                || a.Text.Contains("undo", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(8, document.Outline.Count);
        document.CanvasUndo();
        Assert.Equal(9, document.Outline.Count);
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
