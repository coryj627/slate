// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Grids;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-16 (#1301, contract 40 AR-54): the unresolved-citations grid re-seats
/// the reader by each row's key after a republish. Its key was the citing
/// file's path, which two unresolved citations in one note share, so a
/// republish put the reader on the first of them, whichever they were
/// reading.
/// </summary>
public sealed class BibliographyTests
{
    [Fact]
    public void ASortThenARepublishKeepsTheReaderOnItsUnresolvedCitation() => RunSta(() =>
    {
        var grid = new AccessibleDataGrid { Announce = _ => { } };
        MainWindow.BindBibliographyUnresolved(grid, Fresh(), "2 unresolved citations.");
        Assert.NotNull(grid.ApplySort(columnIndex: 0, ascending: true));
        Assert.Equal(["jones2019", "smith2020"], grid.Grid.Items.Cast<UnresolvedRowViewModel>().Select(row => row.Key));
        var reading = (UnresolvedRowViewModel)grid.Grid.Items[1];
        grid.Grid.CurrentCell = new System.Windows.Controls.DataGridCellInfo(reading, grid.Grid.Columns[0]);

        // A republish: fresh rows for the same two citations.
        MainWindow.BindBibliographyUnresolved(grid, Fresh(), "2 unresolved citations.");

        Assert.Equal("smith2020", Assert.IsType<UnresolvedRowViewModel>(grid.Grid.CurrentCell.Item).Key);
    });

    /// <summary>Two unresolved citations in one note, in the order the
    /// panel publishes them.</summary>
    private static IReadOnlyList<object> Fresh() =>
    [
        new UnresolvedRowViewModel(new UnresolvedCitation(Path: "notes/a.md", Key: "smith2020")),
        new UnresolvedRowViewModel(new UnresolvedCitation(Path: "notes/a.md", Key: "jones2019")),
    ];

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
