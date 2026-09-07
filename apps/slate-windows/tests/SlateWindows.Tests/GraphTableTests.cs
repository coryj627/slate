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
            : this(FixtureVault.Create(notes, label))
        {
        }

        /// <summary>Over a vault the fact shaped itself (the 10k fact's
        /// ghost-heavy one); the host owns and disposes it.</summary>
        public Host(FixtureVault vault)
        {
            Vault = vault;
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

    /// <summary>W6-2 PR B2, Term 15 (IGJ-6): the table VIEW holds no write of
    /// its own — its current row selects through the document's guarded
    /// method — so a view retained over a closed graph tab moves nothing in
    /// the WORKSPACE's view state, which outlives the document (B2-1).</summary>
    [Fact]
    public void ARetainedTableViewOverAClosedTabWritesNothing()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "graph-retained-view");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphTableView { Model = document };
            AccessibleDataGrid grid = view.GridForTests;
            GraphViewState state = host.Workspace.GraphViewStateForTests;
            Assert.Same(state, document.ViewState);

            // Live: the grid's current row writes the key through the document.
            GraphTableRow second = document.Publication.Rows[1];
            Assert.True(grid.SelectRow(row => ReferenceEquals(row, second)));
            Assert.Equal(second.StableKey, state.SelectedKey);

            // Retired: the retained view's current row moves nothing.
            host.Workspace.CloseActiveTabCommand.Execute(null);
            Assert.True(document.IsRetired);
            GraphTableRow first = document.Publication.Rows[0];
            _ = grid.SelectRow(row => ReferenceEquals(row, first));
            Assert.Equal(second.StableKey, state.SelectedKey);
            Assert.False(document.SelectRow(first.StableKey));
        });
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

    /// <summary>A null model (the tab replaced in place, the surface
    /// detached) leaves NOTHING bound: the rows and every delegate that
    /// captured the old document go with the model, so a retired document
    /// is not reachable through the grid (codoki on 1de19b4).</summary>
    [Fact]
    public void ANullModelUnbindsTheRowsAndTheDelegatesWithTheDocument()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "graph-null-model");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphTableView { Model = document };
            DataGrid grid = view.GridForTests.Grid;
            Assert.True(grid.Items.Count >= 3);
            Assert.NotEmpty(grid.Columns);

            view.Model = null;

            Assert.Empty(grid.Items);
            Assert.Empty(grid.Columns);
            Assert.Null(view.Model);
        });
    }

    /// <summary>The 10k-row vault is GHOST-heavy on purpose: a hundred
    /// notes each linking to a hundred unresolved targets give the table
    /// ten thousand ghost rows for a hundred files, so the fact measures
    /// the grid's virtualisation rather than the disk (the CI runner
    /// timed out at the harness's two-minute budget on ten thousand
    /// files; this box needed 110 seconds of it).</summary>
    [Fact]
    public void TenThousandRowsStayVirtualisedAndTheActionInventoryStaysThree()
    {
        RunSta(() =>
        {
            FixtureVault vault = FixtureVault.Create(100, "graph-10k");
            for (int note = 0; note < 100; note++)
            {
                var body = new System.Text.StringBuilder($"# Note {note}\n\n");
                for (int target = 0; target < 100; target++)
                {
                    body.Append($"[[Missing {note}-{target}]] ");
                }
                File.WriteAllText(Path.Combine(vault.Root, $"note{note}.md"), body.Append('\n').ToString());
            }
            using var host = new Host(vault);
            GraphDocumentViewModel document = host.Open();
            // The population exactly (IPC-5): a hundred notes AND ten
            // thousand ghosts — a projection that lost every note would
            // still clear a bare "at least ten thousand".
            Assert.Equal(100, document.Publication.Rows.Count(r => r.Kind == GraphNodeKind.Note));
            Assert.Equal(10_000, document.Publication.Rows.Count(r => r.Kind == GraphNodeKind.Ghost));
            Assert.Equal(10_100, document.Publication.Rows.Count);
            var view = new GraphTableView { Model = document };
            DataGrid grid = view.GridForTests.Grid;
            int loaded = 0;
            int unloaded = 0;
            var kindsRealized = new HashSet<GraphNodeKind>();
            grid.LoadingRow += (_, e) =>
            {
                loaded++;
                _ = kindsRealized.Add(((GraphTableRow)e.Row.Item).Kind);
            };
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
                // The capacity is the frozen text's (A-15): the VIEWPORT's row
                // capacity plus the PANEL's cache length, both READ from the
                // realised panel — never a literal, never a share of the
                // population. The substrate virtualises rows in STANDARD mode
                // and scrolls by item with a one-item cache each side; all
                // four are asserted, so a substrate that changed any of them
                // fails here rather than silently widening the bound.
                System.Windows.Controls.VirtualizingStackPanel? panel = FindPanel(grid);
                Assert.NotNull(panel);
                Assert.True(grid.EnableRowVirtualization, "the grid must virtualise its rows");
                Assert.Equal(
                    System.Windows.Controls.VirtualizationMode.Standard,
                    System.Windows.Controls.VirtualizingPanel.GetVirtualizationMode(grid));
                Assert.Equal(System.Windows.Controls.ScrollUnit.Item, System.Windows.Controls.VirtualizingPanel.GetScrollUnit(grid));
                int viewportRows = (int)Math.Ceiling(panel.ViewportHeight);
                System.Windows.Controls.VirtualizationCacheLength cache = System.Windows.Controls.VirtualizingPanel.GetCacheLength(grid);
                double cacheItems = System.Windows.Controls.VirtualizingPanel.GetCacheLengthUnit(grid) switch
                {
                    System.Windows.Controls.VirtualizationCacheLengthUnit.Item => cache.CacheBeforeViewport + cache.CacheAfterViewport,
                    System.Windows.Controls.VirtualizationCacheLengthUnit.Page => (cache.CacheBeforeViewport + cache.CacheAfterViewport) * viewportRows,
                    _ => throw new InvalidOperationException("a pixel cache length has no row capacity"),
                };
                int capacity = viewportRows + (int)Math.Ceiling(cacheItems);
                // AT REST the panel holds the capacity itself, with an
                // allowance of two for a partially visible row at each edge:
                // that is A-15's sentence, asserted for the first page and
                // again at the end, where nothing is in flight (IPF-2).
                int restingBound = capacity + 2;
                // WHILE PAGING, WPF's Standard virtualisation defers its
                // cleanup: a jump's new containers join the old ones until
                // the panel's own threshold trips, and no public API forces
                // that pass (pumping does not). Measured here it peaks near
                // four capacities before falling back to one. Five capacities
                // is the transient ceiling — a stated empirical allowance
                // over the contract's resting bound, not a reading of it —
                // and a panel that never unloads exceeds it within five
                // pages, which the sweep's `ipd3-never-unload` confirms.
                int pagingBound = capacity * 5;
                Assert.True(viewportRows > 0 && viewportRows < document.Publication.Rows.Count / 10, $"the viewport holds {viewportRows} rows");
                int live = loaded - unloaded;
                Assert.True(live > 0 && live <= restingBound, $"{live} live containers for the first page against a capacity of {capacity}");
                // Page through: the live count stays bounded, never the row count.
                for (int page = 0; page < 20; page++)
                {
                    grid.ScrollIntoView(document.Publication.Rows[Math.Min(document.Publication.Rows.Count - 1, (page + 1) * 500)]);
                    grid.UpdateLayout();
                    // A background frame for whatever cleanup the panel deferred.
                    PumpedDispatcher.Drain();
                    Assert.True(
                        loaded - unloaded <= pagingBound,
                        $"{loaded - unloaded} live containers after page {page} against a bound of {pagingBound} ({viewportRows} rows in the viewport, {cacheItems} cached)");
                }
                grid.ScrollIntoView(document.Publication.Rows[^1]);
                grid.UpdateLayout();
                PumpedDispatcher.Drain();
                Assert.True(
                    loaded - unloaded <= pagingBound,
                    $"{loaded - unloaded} live containers at the end against a bound of {pagingBound}");
                // Unloading HAPPENED — a panel that only realises would have
                // twenty pages live — and never every row.
                Assert.True(unloaded > 0, "no container was ever unloaded");
                Assert.True(loaded < document.Publication.Rows.Count, "every row was realized");
                // The DISTANT containers are gone: ten thousand rows from the
                // viewport, the first row holds no container at all (IPF-2).
                Assert.Null(grid.ItemContainerGenerator.ContainerFromItem(document.Publication.Rows[0]));
                // Both kinds were realised along the way (the ghosts lead
                // under links-in descending; the notes close the table).
                Assert.Contains(GraphNodeKind.Ghost, kindsRealized);
                Assert.Contains(GraphNodeKind.Note, kindsRealized);
            }
            finally
            {
                window.Close();
            }
            Assert.Equal(3, document.ActionInventoryCrossings);
            Assert.Equal(3, document.CrossingsForTests["graph_row_actions"]);
        });
    }

    /// <summary>The grid's items panel — the substrate's virtualising
    /// stack panel — once realised in a shown window.</summary>
    private static System.Windows.Controls.VirtualizingStackPanel? FindPanel(System.Windows.DependencyObject root)
    {
        if (root is System.Windows.Controls.VirtualizingStackPanel panel)
        {
            return panel;
        }
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            System.Windows.Controls.VirtualizingStackPanel? found = FindPanel(System.Windows.Media.VisualTreeHelper.GetChild(root, index));
            if (found is not null)
            {
                return found;
            }
        }
        return null;
    }
}
