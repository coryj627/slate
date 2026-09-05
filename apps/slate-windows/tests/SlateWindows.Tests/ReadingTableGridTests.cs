// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows.Documents;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-1 reading-table → grid substrate: the in-range table stays for
/// linear reading (G23), Enter at the caret opens the SAME source on
/// the accessible grid — cells re-derived by core, never a host
/// re-parse. Layered like math and diagrams, recorded as a G-row.
/// </summary>
public sealed class ReadingTableGridTests
{
    private const string TableSource =
        "| Name | Status |\n"
        + "| --- | --- |\n"
        + "| beta | Open |\n"
        + "| alpha | Done |\n";

    [Fact]
    public void BuilderStampsTheTableSourceForGridActivation()
    {
        RunSta(() =>
        {
            FlowDocument document = BuildSource("# T\n\n" + TableSource);
            Table table = document.Blocks.OfType<Table>().Single();
            string? source = ReadingSemantics.TableSourceOf(table);
            Assert.NotNull(source);
            Assert.Contains("| Name |", source);
        });
    }

    [Fact]
    public void GridModelCarriesHeadersRowsAndSummaryFromCore()
    {
        var model = ReadingTableGrid.BuildModel(TableSource);
        Assert.NotNull(model);
        var (columns, rows, summary, label) = model.Value;

        Assert.Equal(new[] { "Name", "Status" }, columns.Select(c => c.Header));
        Assert.Equal(2, rows.Count);
        Assert.Equal("2 rows, 2 columns.", summary);
        Assert.Equal("Table, 2 rows, 2 columns", label);

        Assert.Equal("beta", columns[0].Cell(rows[0]));
        Assert.Equal("Done", columns[1].Cell(rows[1]));

        // Ragged rows are legal markdown; a short row reads empty in
        // the missing columns, never throws.
        Assert.Equal(string.Empty, columns[1].Cell(new[] { "only" }));

        // Every column sorts (ordinal over untyped text cells).
        Assert.All(columns, column => Assert.NotNull(column.Sort));
        Assert.True(columns[0].Sort!.Compare(rows[1], rows[0]) < 0, "alpha < beta");
    }

    [Fact]
    public void DegenerateSourceYieldsNoGrid()
    {
        Assert.Null(ReadingTableGrid.BuildModel("not a table"));
        RunSta(() => Assert.Null(ReadingTableGrid.Build("not a table")));
    }

    [Fact]
    public void SortRunsThroughTheCoreEventPath()
    {
        RunSta(() =>
        {
            var grid = ReadingTableGrid.Build(TableSource);
            Assert.NotNull(grid);
            string? text = grid.ApplySort(1, ascending: true);
            Assert.Equal(
                SlateUniffiMethods.A11yRender(
                    new A11yEvent.GridSorted("Status", true)).Text,
                text);
        });
    }

    [Fact]
    public void EnterAtCaretInsideTheTableOpensTheGrid()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-table-grid");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "# T\n\n" + TableSource);
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();

            var surface = new ReadingSurface { Model = tab.Reading };
            surface.Measure(new System.Windows.Size(900, 4000));
            surface.Arrange(new System.Windows.Rect(0, 0, 900, 4000));
            surface.UpdateLayout();

            string? opened = null;
            surface.TableGridOpener = source =>
            {
                opened = source;
                return true;
            };

            // Caret on the heading: no table, no activation.
            surface.CaretPosition = surface.Document.ContentStart;
            Assert.False(surface.TryActivateAtCaret());
            Assert.Null(opened);

            // Land on the table exactly as a user does — the chord —
            // then Enter.
            var navigator = new ReadingNavigator(surface, _ => { });
            navigator.SetLandmarks(surface.LandmarksForTests);
            navigator.Move(ReadingLandmarkKind.Table, forward: true);

            Assert.True(surface.TryActivateAtCaret());
            Assert.NotNull(opened);
            Assert.Contains("| Name |", opened);
        });
    }

    private static FlowDocument BuildSource(string source)
    {
        ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(source);
        ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
            source, Array.Empty<RenderedCitation>(), Array.Empty<OutgoingLink>());
        var model = new List<(ReadingBlock, ReadingBlockInlines)>();
        for (int i = 0; i < blocks.Length && i < inlines.Length; i++)
        {
            model.Add((blocks[i], inlines[i]));
        }
        return ReadingDocumentBuilder.Build(model).Document;
    }

    /// <summary>WPF text objects require STA; xunit runs MTA.</summary>
    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
