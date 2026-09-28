// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
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
public sealed partial class GraphTableTests
{
    private sealed class Host : IDisposable
    {
        public FixtureVault Vault { get; }
        public VaultSession Session { get; }
        public WorkspaceViewModel Workspace { get; }
        public List<string> GraphLines { get; } = [];

        /// <summary>W6-2 PR E: the shell's announcements, by event.</summary>
        public List<A11yEvent> ShellEvents { get; } = [];

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
                @event => ShellEvents.Add(@event),
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

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(120), "STA test body timed out.");

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

    /// <summary>Codex PR 3 round 6, owner decision OD-9 — row identity: the
    /// graph table's sort is EXTERNAL (a rows-only token to core, which
    /// republishes the rows in the new order), and a row's name never comes
    /// from its position. Two notes named same.md read alike, so each adds its
    /// node — its path — and keeps it through a sort by modified time each
    /// way.</summary>
    [Fact]
    public void AnExternallySortedGraphTableKeepsEachRowsName()
    {
        RunSta(() =>
        {
            FixtureVault vault = FixtureVault.Create(0, "graph-external-sort");
            foreach (string folder in new[] { "A", "B" })
            {
                Directory.CreateDirectory(Path.Combine(vault.Root, folder));
                File.WriteAllText(Path.Combine(vault.Root, folder, "same.md"), $"# In {folder}\n");
            }
            // A/same.md older than B/same.md, so a sort by modified time
            // orders the pair — and core's row labels (which count links)
            // still read alike.
            File.SetLastWriteTimeUtc(Path.Combine(vault.Root, "A", "same.md"), new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(Path.Combine(vault.Root, "B", "same.md"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            using var host = new Host(vault);
            GraphDocumentViewModel document = host.Open();
            var view = new GraphTableView { Model = document };
            var window = new System.Windows.Window
            {
                Content = view,
                Width = 900,
                Height = 400,
                ShowInTaskbar = false,
                WindowStyle = System.Windows.WindowStyle.None,
            };
            window.Show();
            try
            {
                (string Key, string Name)[] Read()
                {
                    host.Settle(document);
                    window.UpdateLayout();
                    PumpedDispatcher.Drain();
                    window.UpdateLayout();
                    return [.. GridRowNames.Read(view.GridForTests)
                        .Where(row => ((GraphTableRow)row.Item).Path?.EndsWith("same.md", StringComparison.Ordinal) == true)
                        .Select(row => (((GraphTableRow)row.Item).StableKey, row.Name))];
                }
                string Show((string Key, string Name)[] rows) =>
                    string.Join(" | ", rows.Select(row => $"{row.Key} = \"{row.Name}\""));

                GraphTableRow[] pair = [.. document.Publication.Rows.Where(row => row.Path?.EndsWith("same.md", StringComparison.Ordinal) == true)];
                Assert.Equal(2, pair.Length);
                // The premise: the pair's own names read alike.
                Assert.True(
                    SiblingNames.ReadAlike.Equals(document.RowName(pair[0]), document.RowName(pair[1])),
                    $"the pair reads apart already: \"{document.RowName(pair[0])}\", \"{document.RowName(pair[1])}\"");
                (string Key, string Name)[] before = Read();
                Assert.All(before, row => Assert.EndsWith(", " + row.Key[2..], row.Name, StringComparison.Ordinal));
                int modified = document.CellIndexOf(GraphTableColumn.Modified);
                string[] orders = new string[2];
                foreach ((bool ascending, int index) in new[] { (false, 0), (true, 1) })
                {
                    Assert.Null(view.GridForTests.ApplySort(modified, ascending));
                    (string Key, string Name)[] after = Read();
                    orders[index] = after[0].Key;
                    Assert.True(
                        before.OrderBy(row => row.Key, StringComparer.Ordinal)
                            .SequenceEqual(after.OrderBy(row => row.Key, StringComparer.Ordinal)),
                        $"before the sort: {Show(before)}; after it: {Show(after)}");
                }
                // The premise: the two sorts showed the pair in two orders.
                Assert.NotEqual(orders[0], orders[1]);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Codex PR 3 round 7, OD-9: a row's identity is its node's
    /// STABLE key, never its label. A ghost's label is the smallest authored
    /// spelling of its target, which core recomputes as links come and go, so
    /// ordered by label two ghosts that read alike swapped their places when
    /// one was relabelled. Ghost "foo-bar" (spelled "/foo-bar" and "foo-bar")
    /// and ghost "foo bar" read alike under every label they take; dropping
    /// the "/foo-bar" spelling (and one "foo bar" link, so the link counts
    /// still match) relabels the first past the second, and each keeps its
    /// place.</summary>
    [Fact]
    public void ARelabelledGhostKeepsItsPlaceAmongGhostsThatReadAlike()
    {
        RunSta(() =>
        {
            FixtureVault vault = FixtureVault.Create(0, "graph-ghost-relabel");
            File.WriteAllText(Path.Combine(vault.Root, "a.md"), "[[/foo-bar]]\n");
            File.WriteAllText(Path.Combine(vault.Root, "b.md"), "[[foo-bar]]\n");
            File.WriteAllText(Path.Combine(vault.Root, "c.md"), "[[foo bar]]\n");
            File.WriteAllText(Path.Combine(vault.Root, "d.md"), "[[foo bar]]\n");
            using var host = new Host(vault);
            GraphDocumentViewModel document = host.Open();
            var view = new GraphTableView { Model = document };
            var window = new System.Windows.Window
            {
                Content = view,
                Width = 900,
                Height = 400,
                ShowInTaskbar = false,
                WindowStyle = System.Windows.WindowStyle.None,
            };
            window.Show();
            try
            {
                Dictionary<string, (string Label, string Place)> Read()
                {
                    host.Settle(document);
                    window.UpdateLayout();
                    PumpedDispatcher.Drain();
                    window.UpdateLayout();
                    GraphTableRow[] ghosts = [.. document.Publication.Rows.Where(row => row.Path is null)];
                    Assert.Equal(2, ghosts.Length);
                    // The premise: the ghosts read alike, by name and by label.
                    Assert.True(
                        SiblingNames.ReadAlike.Equals(document.RowName(ghosts[0]), document.RowName(ghosts[1])),
                        $"the ghosts read apart already: \"{document.RowName(ghosts[0])}\", \"{document.RowName(ghosts[1])}\"");
                    Assert.True(SiblingNames.ReadAlike.Equals(ghosts[0].Label, ghosts[1].Label));
                    (GraphTableRow Row, string Name)[] named =
                    [
                        .. GridRowNames.Read(view.GridForTests)
                            .Select(row => ((GraphTableRow)row.Item, row.Name))
                            .Where(row => row.Item1.Path is null),
                    ];
                    // Each reads its own name, then its label — what a reader
                    // hears of its key — then its place.
                    Assert.All(named, row => Assert.Matches(
                        "^" + Regex.Escape($"{document.RowName(row.Row)}, {row.Row.Label}, ") + @"row \d+$",
                        row.Name));
                    return named.ToDictionary(
                        row => row.Row.StableKey,
                        row => (row.Row.Label, Regex.Match(row.Name, @"row \d+$").Value));
                }

                Dictionary<string, (string Label, string Place)> before = Read();
                _ = host.Session.SaveText("a.md", "no link\n", null);
                _ = host.Session.SaveText("d.md", "no link\n", null);
                _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
                Dictionary<string, (string Label, string Place)> after = Read();

                string Show(Dictionary<string, (string Label, string Place)> rows) =>
                    string.Join(" | ", rows.OrderBy(row => row.Key, StringComparer.Ordinal)
                        .Select(row => $"{row.Key} = \"{row.Value.Label}\" at \"{row.Value.Place}\""));
                // The premise: the same two ghosts, one relabelled, and the
                // label order flipped — a label-ordered naming swaps them.
                Assert.Equal(
                    before.Keys.Order(StringComparer.Ordinal),
                    after.Keys.Order(StringComparer.Ordinal));
                string relabelled = Assert.Single(before.Keys, key => before[key].Label != after[key].Label);
                string other = Assert.Single(before.Keys, key => key != relabelled);
                Assert.NotEqual(
                    string.CompareOrdinal(before[relabelled].Label, before[other].Label) < 0,
                    string.CompareOrdinal(after[relabelled].Label, after[other].Label) < 0);
                Assert.All(before.Values.Concat(after.Values), row => Assert.NotEqual(string.Empty, row.Place));
                Assert.True(
                    before.All(row => after[row.Key].Place == row.Value.Place),
                    $"before the relabel: {Show(before)}; after it: {Show(after)}");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Two notes called same.md, in A and B, read alike by their
    /// row copy; A is the older.</summary>
    private static FixtureVault TwoSameNotes(string label)
    {
        FixtureVault vault = FixtureVault.Create(0, label);
        foreach (string folder in new[] { "A", "B" })
        {
            Directory.CreateDirectory(Path.Combine(vault.Root, folder));
            File.WriteAllText(Path.Combine(vault.Root, folder, "same.md"), $"# In {folder}\n");
        }
        File.SetLastWriteTimeUtc(Path.Combine(vault.Root, "A", "same.md"), new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(Path.Combine(vault.Root, "B", "same.md"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        return vault;
    }

    /// <summary>The graph table over <paramref name="vault"/>, shown; the
    /// body gets the host, the document, the view and a settle that lays
    /// the window out again.</summary>
    private static void WithShownTable(
        FixtureVault vault, Action<Host, GraphDocumentViewModel, GraphTableView, Action> body)
    {
        using var host = new Host(vault);
        GraphDocumentViewModel document = host.Open();
        var view = new GraphTableView { Model = document };
        var window = new System.Windows.Window
        {
            Content = view,
            Width = 900,
            Height = 400,
            ShowInTaskbar = false,
            WindowStyle = System.Windows.WindowStyle.None,
        };
        window.Show();
        try
        {
            void Settle()
            {
                host.Settle(document);
                window.UpdateLayout();
                PumpedDispatcher.Drain();
                window.UpdateLayout();
            }
            Settle();
            body(host, document, view, Settle);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Codex PR 3 round 8 (contract 35 A-6: the row's Name and its
    /// row move speak the SAME text): a row move speaks the grid's final
    /// composed name. The two same.md rows are named "…, A/same.md" and "…,
    /// B/same.md", and a move onto either — the currency an arrow moves —
    /// announced the bare row copy both share.</summary>
    [Fact]
    public void ARowMoveSpeaksTheRowsComposedName() => RunSta(() => WithShownTable(
        TwoSameNotes("graph-row-move-name"),
        (host, document, view, settle) =>
        {
            AccessibleDataGrid grid = view.GridForTests;
            Dictionary<object, string> names = GridRowNames.Read(grid)
                .ToDictionary(row => row.Item, row => row.Name, ReferenceEqualityComparer.Instance);
            GraphTableRow[] pair = [.. document.Publication.Rows.Where(row => row.Path?.EndsWith("same.md", StringComparison.Ordinal) == true)];
            Assert.Equal(2, pair.Length);
            // The premise: the pair's copy reads alike, its names apart.
            Assert.True(SiblingNames.ReadAlike.Equals(document.RowName(pair[0]), document.RowName(pair[1])));
            Assert.False(SiblingNames.ReadAlike.Equals(names[pair[0]], names[pair[1]]));
            grid.Grid.CurrentCell = new DataGridCellInfo(pair[0], grid.Grid.Columns[0]);
            settle();
            var moves = new List<A11yEvent.GridRowMoved>();
            Action<A11yEvent> relay = grid.Announce;
            grid.Announce = @event =>
            {
                if (@event is A11yEvent.GridRowMoved moved)
                {
                    moves.Add(moved);
                }
                relay(@event);
            };
            foreach (GraphTableRow row in new[] { pair[1], pair[0], pair[1] })
            {
                moves.Clear();
                host.GraphLines.Clear();
                grid.Grid.CurrentCell = new DataGridCellInfo(row, grid.Grid.Columns[0]);
                settle();
                // The description IS the row's UIA Name, and the relayed line
                // speaks it.
                A11yEvent.GridRowMoved move = Assert.Single(moves);
                Assert.Equal(names[row], move.Description);
                Assert.Contains(SlateUniffiMethods.A11yRender(move).Text, host.GraphLines);
            }
        }));

    /// <summary>Codex PR 3 round 8 (R-4, OD-9; contract 35 A-7): a seat the
    /// shared key does not hold — rule F's silent seat writes none — is
    /// restored by the row's stable key through a republish that relabels
    /// it. Ghost "/foo-bar" (seated first under a Note sort) is relabelled
    /// "foo-bar", which moves it past "foo bar": restored by its old label
    /// the seat was lost; and nothing writes the key.</summary>
    [Fact]
    public void ARelabelledGhostKeepsASeatTheKeyDoesNotHold() => RunSta(() =>
    {
        FixtureVault vault = FixtureVault.Create(0, "graph-ghost-seat");
        File.WriteAllText(Path.Combine(vault.Root, "a.md"), "[[/foo-bar]]\n");
        File.WriteAllText(Path.Combine(vault.Root, "b.md"), "[[foo-bar]]\n");
        File.WriteAllText(Path.Combine(vault.Root, "c.md"), "[[foo bar]]\n");
        File.WriteAllText(Path.Combine(vault.Root, "d.md"), "[[foo bar]]\n");
        WithShownTable(vault, (host, document, view, settle) =>
        {
            host.Workspace.GraphNavigator.SetNameQuery("foo");
            Assert.True(document.Request(new GraphRequest.Sort(new GraphTableSort(GraphTableColumn.Note, true))));
            settle();
            // The premise: the two ghosts, "/foo-bar" first.
            Assert.Equal(["/foo-bar", "foo bar"], document.Publication.Rows.Select(row => row.Label));
            Assert.True(view.FocusProjection());
            settle();
            Assert.Null(document.ViewState.SelectedKey);
            var seated = Assert.IsType<GraphTableRow>(view.GridForTests.Grid.CurrentCell.Item);
            Assert.Equal("/foo-bar", seated.Label);

            _ = host.Session.SaveText("a.md", "no link\n", null);
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            settle();
            // The premise: relabelled, and moved.
            Assert.Equal(["foo bar", "foo-bar"], document.Publication.Rows.Select(row => row.Label));
            var restored = Assert.IsType<GraphTableRow>(view.GridForTests.Grid.CurrentCell.Item);
            Assert.Equal(seated.StableKey, restored.StableKey);
            Assert.Null(document.ViewState.SelectedKey);
        });
    });

    /// <summary>...and through an external sort each way: the two same.md
    /// rows share their row-header text, so restored by that text and its
    /// old place the seat moved onto the OTHER note whenever the sort swapped
    /// them. The seat stays on its node, and nothing writes the key.</summary>
    [Fact]
    public void AnExternalSortKeepsASeatTheKeyDoesNotHoldOnItsNode() => RunSta(() => WithShownTable(
        TwoSameNotes("graph-sort-seat"),
        (host, document, view, settle) =>
        {
            host.Workspace.GraphNavigator.SetNameQuery("same");
            settle();
            Assert.Equal(2, document.Publication.Rows.Count);
            Assert.True(view.FocusProjection());
            settle();
            Assert.Null(document.ViewState.SelectedKey);
            string seated = Assert.IsType<GraphTableRow>(view.GridForTests.Grid.CurrentCell.Item).StableKey;
            int modified = document.CellIndexOf(GraphTableColumn.Modified);
            var places = new List<int>();
            foreach (bool ascending in new[] { true, false, true })
            {
                Assert.Null(view.GridForTests.ApplySort(modified, ascending));
                settle();
                var current = Assert.IsType<GraphTableRow>(view.GridForTests.Grid.CurrentCell.Item);
                Assert.Equal(seated, current.StableKey);
                Assert.Null(document.ViewState.SelectedKey);
                places.Add(document.Publication.Rows.ToList().FindIndex(row => row.StableKey == seated));
            }
            // The premise: the sorts moved the seated note between the places.
            Assert.Equal(2, places.Distinct().Count());
        }));

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
    public void TheModeSwitcherIsCoresVectorWithBothModesEnabled()
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
            // W6-2 PR D (rule M, Term M2): A-11's admission lifted — the
            // Diagram item is live.
            Assert.True(diagram.IsEnabled);
            Assert.Equal(GraphSurfaceMode.Table, document.ViewState.Mode);
        });
    }

    private static void Toggle(System.Windows.Controls.Primitives.ToggleButton toggle)
    {
        System.Windows.Automation.Peers.AutomationPeer peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(toggle);
        ((System.Windows.Automation.Provider.IToggleProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Toggle)!).Toggle();
    }

    private static string[] Names(IEnumerable<A11yEvent> events) => [.. events.Select(e => e.GetType().Name)];

    /// <summary>W6-2 PR E (Terms I1, I2; E-3; E-D5): the header's toggle carries
    /// the mac's names (T29, T30) and the automation id; a click shows the
    /// pane with the inspector leaf active, checks the toggle and asks for
    /// the pane boundary; a second click hides the pane, leaves the leaf,
    /// unchecks the toggle and asks the keys back to the projection; the
    /// shell's own moves re-check the toggle through the document.</summary>
    [Fact]
    public void TheHeaderToggleShowsAndHidesTheInspectorAndReturnsTheKeys()
    {
        RunSta(() =>
        {
            using var host = new Host(2, "graph-inspector-toggle");
            GraphDocumentViewModel document = host.Open();
            var surface = new GraphSurfaceView { Model = document };
            using HostedWindow window = HostInWindow(surface);
            System.Windows.Controls.Primitives.ToggleButton toggle = surface.InspectorToggleForTests;
            Assert.Equal(GraphPhrase.InspectorLabel, (string)toggle.Content);
            Assert.Equal("GraphInspectorToggle", System.Windows.Automation.AutomationProperties.GetAutomationId(toggle));
            Assert.Equal(GraphPhrase.InspectorToggleName, System.Windows.Automation.AutomationProperties.GetName(toggle));
            Assert.Equal(GraphPhrase.InspectorToggleHint, System.Windows.Automation.AutomationProperties.GetHelpText(toggle));
            Assert.False(toggle.IsChecked);
            Assert.False(host.Workspace.IsGraphInspectorShown);
            // In the header at its far right (docked right, first in the tree),
            // and the LAST Tab stop of the surface — after the projection — so
            // the grid's Shift+Tab reaches the switcher and the switcher's the
            // field (C's route; Term N2; E-13).
            DockPanel header = Assert.IsType<DockPanel>(toggle.Parent);
            System.Windows.UIElement switcher = header.Children.OfType<System.Windows.UIElement>().First(child => System.Windows.Automation.AutomationProperties.GetAutomationId(child) == "GraphSurfaceSwitcher");
            Assert.True(System.Windows.Input.KeyboardNavigation.GetTabIndex(toggle) > System.Windows.Input.KeyboardNavigation.GetTabIndex(switcher));
            Assert.True(System.Windows.Input.KeyboardNavigation.GetTabIndex(toggle) > System.Windows.Input.KeyboardNavigation.GetTabIndex(surface.FilterFieldForTests));
            Assert.True(header.Children.IndexOf(toggle) < header.Children.IndexOf(switcher));
            Assert.Equal(Dock.Right, DockPanel.GetDock(toggle));
            WorkspaceFocusBoundary? boundary = null;
            host.Workspace.FocusBoundaryRequested += (_, requested) => boundary = requested;
            // Show: the pane, the leaf, the boundary; the toggle checked.
            Toggle(toggle);
            Assert.True(host.Workspace.IsRightPaneVisible);
            Assert.Equal("inspector", host.Workspace.ActiveLeaf.Id);
            Assert.True(host.Workspace.IsGraphInspectorShown);
            Assert.True(toggle.IsChecked);
            Assert.Equal(WorkspaceFocusBoundary.RightPane, boundary);
            // Hide: the pane hidden, the leaf kept, no boundary; the keys, on
            // the toggle, asked back to the projection and delivered to the
            // live grid (rule F's request; E-D5).
            boundary = null;
            Assert.True(toggle.Focus());
            Assert.Same(toggle, System.Windows.Input.Keyboard.FocusedElement);
            Toggle(toggle);
            Assert.False(host.Workspace.IsRightPaneVisible);
            Assert.Equal("inspector", host.Workspace.ActiveLeaf.Id);
            Assert.False(host.Workspace.IsGraphInspectorShown);
            Assert.False(toggle.IsChecked);
            Assert.Null(boundary);
            host.Settle(document);
            Assert.True(surface.IsKeyboardFocusWithin);
            Assert.NotSame(toggle, System.Windows.Input.Keyboard.FocusedElement);
            // The shell's own moves: the pane shown with the leaf still the
            // inspector checks the toggle; another leaf unchecks it.
            host.Workspace.IsRightPaneVisible = true;
            Assert.True(toggle.IsChecked);
            host.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "outline");
            Assert.False(toggle.IsChecked);
            host.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "inspector");
            Assert.True(toggle.IsChecked);
            // The leaf's entry: after the Connections leaf, titled T37.
            int connections = WorkspaceViewModel.Leaves.ToList().FindIndex(leaf => leaf.Id == "connections");
            Assert.Equal("inspector", WorkspaceViewModel.Leaves[connections + 1].Id);
            Assert.Equal(GraphPhrase.InspectorName, WorkspaceViewModel.Leaves[connections + 1].Title);
        });
    }

    /// <summary>W6-2 PR E (Term I2; E-3, E-9; IGV-1): the toggle's four
    /// timelines are the shell's setters' lines and nothing of the graph's —
    /// (a) hidden pane, another leaf: RightPaneShown then LeafPanelShown;
    /// (d) a hide: RightPaneHidden alone; (b) hidden pane, the inspector
    /// already the leaf: RightPaneShown alone; (c) visible pane, another
    /// leaf: LeafPanelShown alone.</summary>
    [Fact]
    public void TheToggleSpeaksTheShellsFourTimelinesAndNothingOfItsOwn()
    {
        RunSta(() =>
        {
            using var host = new Host(2, "graph-inspector-timelines");
            GraphDocumentViewModel document = host.Open();
            WorkspaceViewModel workspace = host.Workspace;
            WorkspaceLeafOption outline = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "outline");
            // (a) hidden pane, another leaf active.
            workspace.IsRightPaneVisible = false;
            workspace.ActiveLeaf = outline;
            host.Settle(document);
            document.AnnouncerForTests.FlushForTests();
            host.ShellEvents.Clear();
            host.GraphLines.Clear();
            workspace.ToggleGraphInspector();
            Assert.Equal(["RightPaneShown", "LeafPanelShown"], Names(host.ShellEvents));
            Assert.Equal(GraphPhrase.InspectorName, ((A11yEvent.LeafPanelShown)host.ShellEvents[1]).Title);
            // (d) the hide.
            host.ShellEvents.Clear();
            workspace.ToggleGraphInspector();
            Assert.Equal(["RightPaneHidden"], Names(host.ShellEvents));
            Assert.Equal("inspector", workspace.ActiveLeaf.Id);
            // (b) hidden pane, the inspector already the leaf.
            host.ShellEvents.Clear();
            workspace.ToggleGraphInspector();
            Assert.Equal(["RightPaneShown"], Names(host.ShellEvents));
            // (c) visible pane, another leaf active.
            workspace.ActiveLeaf = outline;
            host.ShellEvents.Clear();
            workspace.ToggleGraphInspector();
            Assert.Equal(["LeafPanelShown"], Names(host.ShellEvents));
            Assert.True(workspace.IsGraphInspectorShown);
            // Nothing of the graph's on any timeline.
            host.Settle(document);
            document.AnnouncerForTests.FlushForTests();
            Assert.Empty(host.GraphLines);
        });
    }

    /// <summary>W6-2 PR E (Terms I4, I7; E-D8): the inspector leaf persists as
    /// every leaf does and restores silently, with no graph document — the
    /// pane inert (the gate false) until a graph opens.</summary>
    [Fact]
    public void TheInspectorLeafRestoresSilentlyAndStaysInertWithNoGraph()
    {
        RunSta(() =>
        {
            using FixtureVault vault = FixtureVault.Create(2, "graph-inspector-restore");
            var events = new List<A11yEvent>();
            using (var session = VaultSession.OpenFilesystem(vault.Root))
            {
                using var cancel = new CancelToken();
                session.ScanInitial(cancel);
                var first = new WorkspaceViewModel(session, vault.Root, () => [], events.Add, startInteractionBackgroundWork: false, announceRendered: _ => { });
                first.IsRightPaneVisible = true;
                first.ToggleGraphInspector();
                Assert.True(first.IsGraphInspectorShown);
                first.Dispose();
            }
            events.Clear();
            using (var session = VaultSession.OpenFilesystem(vault.Root))
            {
                using var cancel = new CancelToken();
                session.ScanInitial(cancel);
                var second = new WorkspaceViewModel(session, vault.Root, () => [], events.Add, startInteractionBackgroundWork: false, announceRendered: _ => { });
                Assert.Equal("inspector", second.ActiveLeaf.Id);
                Assert.DoesNotContain(events, @event => @event is A11yEvent.LeafPanelShown);
                Assert.Null(second.GraphDocument);
                Assert.False(second.Inspector.IsGraphEffective);
                second.OpenGraph();
                PumpedDispatcher.PumpUntilDrained(second.GraphDocument!.WhenAllWorkDrained());
                PumpedDispatcher.Drain();
                Assert.True(second.Inspector.IsGraphEffective);
                second.Dispose();
            }
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

    /// <summary>W6-2 PR C (C-9): a verbosity change re-binds the current
    /// publication under the syncing guard — a realised row's UIA Name
    /// moves from the corpus copy to the bare label — with no load, the
    /// same publication, and nothing posted.</summary>
    [Fact]
    public void ARealisedRowsNameChangesFromTheCopyToTheBareLabelWithNoLoadAndNoPost()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "graph-relabel");
            GraphDocumentViewModel document = host.Open();
            var view = new GraphTableView { Model = document };
            using HostedWindow window = HostInWindow(view);
            view.GridForTests.Grid.UpdateLayout();
            GraphTableRow first = document.Publication.Rows[0];
            var realized = (DataGridRow?)view.GridForTests.Grid.ItemContainerGenerator.ContainerFromItem(first);
            Assert.NotNull(realized);
            string copy = SlateUniffiMethods.A11yRender(
                new A11yEvent.Graph(new GraphA11yEvent.GraphRow(GraphVerbosity.Standard, document.RowCopy(first)))).Text;
            Assert.Equal(copy, AutomationProperties.GetName(realized));
            Assert.NotEqual(first.Label, copy);
            ulong seq = document.SeqForTests;
            int lines = host.GraphLines.Count;
            GraphPublication publication = document.Publication;

            host.Workspace.GraphPreferences.SetVerbosityCommand.Execute("terse");
            view.GridForTests.Grid.UpdateLayout();

            var relabelled = (DataGridRow?)view.GridForTests.Grid.ItemContainerGenerator.ContainerFromItem(first);
            Assert.NotNull(relabelled);
            Assert.Equal(first.Label, AutomationProperties.GetName(relabelled));
            Assert.Same(publication, document.Publication);
            Assert.Equal(seq, document.SeqForTests);
            Assert.Equal(lines, host.GraphLines.Count);
        });
    }

}
