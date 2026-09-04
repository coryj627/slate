// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows.Automation;
using System.Windows.Controls;
using SlateWindows.Graph;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR A (#746), contracts A-5, A-6, A-11, A-15: the graph table as a
/// configuration of the substrate — core's columns in core's order, the
/// records as rows, the external sort as a rows-only token, GridSorted
/// once on adoption, the row's Name as P1's copy and the kind on
/// ItemStatus, the mode switcher from core's vector, and the 10k grid
/// virtualised on the real substrate with the action inventory constant.
/// </summary>
public sealed class GraphTableTests
{
    private sealed class Host : IDisposable
    {
        public FixtureVault Vault { get; }
        public VaultSession Session { get; }
        public WorkspaceViewModel Workspace { get; }
        public List<string> GraphLines { get; } = [];

        public Host(int notes, string label)
        {
            Vault = FixtureVault.Create(notes, label);
            Session = VaultSession.OpenFilesystem(Vault.Root);
            using var cancel = new CancelToken();
            Session.ScanInitial(cancel);
            Workspace = new WorkspaceViewModel(
                Session,
                Vault.Root,
                () => [],
                _ => { },
                startInteractionBackgroundWork: false,
                announceRendered: line => GraphLines.Add(line.Text));
        }

        public GraphDocumentViewModel Open()
        {
            Workspace.OpenGraph();
            GraphDocumentViewModel document = Workspace.GraphDocument!;
            Settle(document);
            return document;
        }

        public void Settle(GraphDocumentViewModel document)
        {
            PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
        }

        public void Dispose()
        {
            Workspace.Dispose();
            Session.Dispose();
            Vault.Dispose();
        }
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PumpedDispatcher.Run(body);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    [Fact]
    public void TheColumnsAreCoresVectorInOrderWithTheNoteColumnAsRowHeader()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-columns");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphTableView { Model = document };
            AccessibleDataGrid grid = view.GridForTests;

            string[] headers = grid.Grid.Columns.Select(c => (string)c.Header).ToArray();
            Assert.Equal(document.ColumnSpecs.Select(s => s.Header), headers);
            Assert.Equal(SlateUniffiMethods.GraphTableColumns().Select(s => s.Header), headers);
            Assert.Equal(DataGridHeadersVisibility.All, grid.Grid.HeadersVisibility);

            // The rows are the records themselves — no wrapper.
            Assert.All(grid.Grid.Items.Cast<object>(), row => Assert.IsType<GraphTableRow>(row));
            // The default sort is the fetched record, indicated on its column.
            Assert.Equal(SlateUniffiMethods.GraphTableDefaultSort(), document.Publication.AcceptedSort);
            Assert.Equal(
                (document.CellIndexOf(document.DefaultSort.Column), document.DefaultSort.Ascending),
                grid.ActiveSort);
        });
    }

    [Fact]
    public void AHeaderSortIssuesARowsOnlyTokenAndGridSortedSpeaksOnceOnAdoption()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-sort");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphTableView { Model = document };
            AccessibleDataGrid grid = view.GridForTests;
            host.GraphLines.Clear();
            int noteColumn = document.CellIndexOf(GraphTableColumn.Note);

            // The substrate delegates: nothing reorders, nothing speaks yet.
            Assert.Null(grid.ApplySort(noteColumn, true));
            Assert.NotNull(document.RequestedSortForTests);
            Assert.Empty(host.GraphLines);
            host.Settle(document);

            string sorted = SlateUniffiMethods.A11yRender(
                new A11yEvent.GridSorted(document.ColumnSpecs[noteColumn].Header, true)).Text;
            Assert.Contains(sorted, host.GraphLines);
            Assert.Equal(1, host.GraphLines.Count(line => line == sorted));
            Assert.Equal((noteColumn, true), grid.ActiveSort);
            Assert.Equal(new GraphTableSort(GraphTableColumn.Note, true), document.Publication.AcceptedSort);

            // The same sort again: a no-op, no second announcement.
            Assert.Null(grid.ApplySort(noteColumn, true));
            host.Settle(document);
            Assert.Equal(1, host.GraphLines.Count(line => line == sorted));

            // A reversal: another column requested, then the accepted sort
            // requested back while it is pending — the publication answers
            // a sort request whose accepted sort is UNCHANGED, so nothing
            // is spoken (the sweep's `grid-sorted-every-publish`).
            int links = document.CellIndexOf(GraphTableColumn.LinksIn);
            Assert.Null(grid.ApplySort(links, true));
            Assert.Null(grid.ApplySort(noteColumn, true));
            host.Settle(document);
            Assert.Equal(new GraphTableSort(GraphTableColumn.Note, true), document.Publication.AcceptedSort);
            Assert.Equal(1, host.GraphLines.Count(line => line == sorted));
            string linksSorted = SlateUniffiMethods.A11yRender(
                new A11yEvent.GridSorted(document.ColumnSpecs[links].Header, true)).Text;
            Assert.DoesNotContain(linksSorted, host.GraphLines);
        });
    }

    [Fact]
    public void TheRowNameIsTheCorpusCopyAndTheItemStatusIsTheKindCell()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "graph-row-name");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphTableView { Model = document };
            var window = new System.Windows.Window
            {
                Content = view,
                Width = 800,
                Height = 400,
                ShowInTaskbar = false,
                WindowStyle = System.Windows.WindowStyle.None,
            };
            window.Show();
            try
            {
                view.GridForTests.Grid.UpdateLayout();
                GraphTableRow first = document.Publication.Rows[0];
                var realized = (DataGridRow?)view.GridForTests.Grid.ItemContainerGenerator.ContainerFromItem(first);
                Assert.NotNull(realized);
                string expected = SlateUniffiMethods.A11yRender(
                    new A11yEvent.Graph(new GraphA11yEvent.GraphRow(GraphVerbosity.Standard, document.RowCopy(first)))).Text;
                Assert.Equal(expected, AutomationProperties.GetName(realized));
                Assert.Equal(document.CellOf(first, GraphTableColumn.Kind), AutomationProperties.GetItemStatus(realized));
                Assert.Equal("Note", AutomationProperties.GetItemStatus(realized));
                Assert.Equal(
                    "Summary: " + document.Publication.Summary,
                    AutomationProperties.GetName(view.GridForTests.SummaryRegion));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void TheCellLookupFindsTheKindByColumnUnderAReorderedVector()
    {
        // The document's ONE cell lookup keys by column: a vector in
        // another order still finds the Kind cell (contract A-6).
        RunSta(() =>
        {
            using var host = new Host(2, "graph-cell-lookup");
            GraphDocumentViewModel document = host.Open();
            GraphTableRow row = document.Publication.Rows[0];
            int kind = document.CellIndexOf(GraphTableColumn.Kind);
            Assert.Equal(row.Cells[kind], document.CellOf(row, GraphTableColumn.Kind));
            Assert.Equal(document.ColumnSpecs.Count - 1, kind);
            // A reordered vector HANDED TO THE DOCUMENT (IPA-11): the lookup
            // answers the moved position and returns the cell AT that
            // position — keyed by the vector, never by a typed index.
            IReadOnlyList<GraphTableColumnSpec> reversed = document.ColumnSpecs.Reverse().ToArray();
            document.ReplaceColumnInventoryForTests(reversed);
            Assert.Equal(0, document.CellIndexOf(GraphTableColumn.Kind));
            Assert.Equal(row.Cells[0], document.CellOf(row, GraphTableColumn.Kind));
            Assert.Equal(reversed.Count - 1, document.CellIndexOf(GraphTableColumn.Note));
            Assert.Equal(row.Cells[^1], document.CellOf(row, GraphTableColumn.Note));
            Assert.NotEqual(kind, document.CellIndexOf(GraphTableColumn.Kind));
        });
    }

    [Fact]
    public void TheModeSwitcherIsCoresVectorWithTheDiagramDisabled()
    {
        RunSta(() =>
        {
            using var host = new Host(2, "graph-modes");
            GraphDocumentViewModel document = host.Open();
            var surface = new GraphSurfaceView { Model = document };
            Assert.Equal(
                SlateUniffiMethods.GraphSurfaceModes().Select(s => s.Title),
                surface.ModeChoicesForTests.Select(c => (string)c.Content));
            RadioButton table = surface.ModeChoicesForTests.First(c => (GraphSurfaceMode)c.Tag == GraphSurfaceMode.Table);
            RadioButton diagram = surface.ModeChoicesForTests.First(c => (GraphSurfaceMode)c.Tag == GraphSurfaceMode.Diagram);
            Assert.True(table.IsChecked);
            Assert.True(table.IsEnabled);
            Assert.False(diagram.IsEnabled);
            Assert.Equal(GraphSurfaceMode.Table, document.ViewState.Mode);
        });
    }

    [Fact]
    public void TheStatesShowTheMacsLabels()
    {
        RunSta(() =>
        {
            using var host = new Host(2, "graph-states");
            host.Workspace.OpenGraph();
            GraphDocumentViewModel document = host.Workspace.GraphDocument!;
            var surface = new GraphSurfaceView { Model = document };
            Assert.Equal(GraphSurfaceView.LoadingText, surface.StateTextForTests.Text);
            Assert.Equal(GraphSurfaceView.LoadingAccessibleName, AutomationProperties.GetName(surface.StateTextForTests));
            host.Settle(document);
            Assert.Equal(System.Windows.Visibility.Collapsed, surface.StateTextForTests.Visibility);
            Assert.Equal(System.Windows.Visibility.Visible, surface.TableForTests.Visibility);
        });
    }

    [Fact]
    public void TenThousandRowsStayVirtualisedAndTheActionInventoryStaysThree()
    {
        RunSta(() =>
        {
            using var host = new Host(10_000, "graph-10k");
            GraphDocumentViewModel document = host.Open();
            Assert.True(document.Publication.Rows.Count >= 10_000);
            var view = new GraphTableView { Model = document };
            DataGrid grid = view.GridForTests.Grid;
            int loaded = 0;
            int unloaded = 0;
            grid.LoadingRow += (_, _) => loaded++;
            grid.UnloadingRow += (_, _) => unloaded++;
            var window = new System.Windows.Window
            {
                Content = view,
                Width = 800,
                Height = 400,
                ShowInTaskbar = false,
                WindowStyle = System.Windows.WindowStyle.None,
            };
            window.Show();
            try
            {
                grid.UpdateLayout();
                int live = loaded - unloaded;
                Assert.True(live > 0 && live < 200, $"{live} live containers for the first page");
                // Page through: the live count stays bounded, never the row count.
                for (int page = 0; page < 20; page++)
                {
                    grid.ScrollIntoView(document.Publication.Rows[Math.Min(document.Publication.Rows.Count - 1, (page + 1) * 500)]);
                    grid.UpdateLayout();
                    Assert.True(loaded - unloaded < 400, $"{loaded - unloaded} live containers after page {page}");
                }
                grid.ScrollIntoView(document.Publication.Rows[^1]);
                grid.UpdateLayout();
                Assert.True(loaded - unloaded < 400, $"{loaded - unloaded} live containers at the end");
                Assert.True(loaded < document.Publication.Rows.Count, "every row was realized");
            }
            finally
            {
                window.Close();
            }
            Assert.Equal(3, document.ActionInventoryCrossings);
        });
    }
}
