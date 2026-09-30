// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Bases;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7 (#1252, R-9) over PR 3 (#1246, R-4) and PR 4 (#1247, R-5): the
/// rescan's Bases re-run republishes an open base's grid under the reader
/// through the base's own publication — the surface's Bind with its row key —
/// so the key restore and the publication keeper leave the keys on the
/// reader's row, never on the row that took its place.
/// </summary>
public sealed partial class RescanTests
{
    private const string NamedColumnsBase =
        "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n    order:\n      - file.name\n";

    /// <summary>The keys on the grid's THIRD row, and the note shown first
    /// deleted outside Slate: the rescan's re-run republishes one row fewer,
    /// and the keys stay on the reader's note — by its key — not on the row
    /// now at their old place, nor on the first row.</summary>
    [Fact]
    public void ARescansBaseReRunKeepsTheKeysOnTheReadersRow() => RunSta(() =>
    {
        using var h = new Harness(
            "rescan-grid-row",
            ("Notes.base", NamedColumnsBase),
            ("b.md", "# B\n"),
            ("d.md", "# D\n"),
            ("f.md", "# F\n"),
            ("h.md", "# H\n"));
        WorkspaceTabViewModel tab = h.Open("Notes.base");
        h.PumpUntil(() => tab.Base?.State == BaseLoadState.Ready, "the base's first load");
        BaseDocumentViewModel document = Assert.IsType<BaseDocumentViewModel>(tab.Base);
        var surface = new BaseSurfaceView { Model = document };
        var window = new Window
        {
            Content = surface,
            Width = 700,
            Height = 500,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        try
        {
            AccessibleDataGrid grid = surface.GridForTests;
            string[] shown = [.. grid.Grid.Items.Cast<BaseGridRowViewModel>().Select(row => row.RowKey)];
            Assert.Equal(4, shown.Length);
            string above = shown[0];
            string reader = shown[2];
            Assert.True(grid.SelectRow(row => ((BaseGridRowViewModel)row).RowKey == reader, moveFocus: true));
            PumpedDispatcher.Drain();
            Assert.Equal(reader, RowKeyOfFocusedCell());

            h.Delete(above);
            h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
            h.PumpUntil(() => grid.Grid.Items.Count == 3, "the re-run's publication");
            window.UpdateLayout();
            PumpedDispatcher.Drain();

            Assert.Equal(reader, RowKeyOfFocusedCell());
            Assert.Equal(["Files refreshed. 0 new or changed, 1 removed."], h.Spoken);
        }
        finally
        {
            window.Close();
        }
    });

    private static string? RowKeyOfFocusedCell() =>
        (Keyboard.FocusedElement as DataGridCell)?.DataContext is BaseGridRowViewModel row ? row.RowKey : null;
}
