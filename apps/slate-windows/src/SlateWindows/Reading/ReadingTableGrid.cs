// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Reading;

/// <summary>
/// The reading table's grid destination (W4-1): Enter on an in-range
/// table opens the SAME source on the accessible grid substrate,
/// gaining the §8.7 powers — headers on entry, cell navigation,
/// keyboard sort, type-ahead. Cells come from core
/// <c>ReadingTableCells</c>, never a host re-parse (§10.8).
///
/// No export producer: reading tables have no core export surface,
/// and a host-composed one is exactly what the census forbids — the
/// export commands stay disabled (G-row, w_c_matrix W4-1).
/// </summary>
internal static class ReadingTableGrid
{
    internal static bool Show(string source, Window? owner)
    {
        AccessibleDataGrid? grid = Build(source);
        if (grid is null)
        {
            return false;
        }
        var window = new Window
        {
            Title = "Table",
            Width = 820,
            Height = 520,
            Owner = owner,
            Content = grid,
        };
        window.SetResourceReference(Window.BackgroundProperty, "Slate.SurfaceBrush");
        AutomationProperties.SetAutomationId(window, "ReadingTableGridWindow");
        // Escape returns to the reading caret — closing an owned
        // window reactivates the owner, and the RichTextBox keeps its
        // caret position.
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                window.Close();
            }
        };
        // First CELL, explicitly — MoveFocus(First) lands on the
        // summary, which precedes the grid in tree order (adversarial
        // round 1: entry must announce headers + cell).
        window.Loaded += (_, _) => grid.FocusFirstCell();
        window.Show();
        return true;
    }

    internal static AccessibleDataGrid? Build(string source)
    {
        if (BuildModel(source) is not { } model)
        {
            return null;
        }
        var grid = new AccessibleDataGrid();
        grid.Bind(model.Columns, model.Rows, model.Summary, model.Label);
        return grid;
    }

    /// <summary>Null when core cannot derive cells — the caller must
    /// let Enter fall through rather than open an empty window.</summary>
    internal static (
        IReadOnlyList<AccessibleGridColumn> Columns,
        IReadOnlyList<object> Rows,
        string Summary,
        string Label)? BuildModel(string source)
    {
        ReadingTableCells? cells = SlateUniffiMethods.ReadingTableCells(source);
        if (cells is null || (cells.Header.Length == 0 && cells.Rows.Length == 0))
        {
            return null;
        }
        int columnCount = Math.Max(
            cells.Header.Length,
            cells.Rows.Length == 0 ? 1 : cells.Rows.Max(row => row.Length));
        var columns = new List<AccessibleGridColumn>(columnCount);
        for (int i = 0; i < columnCount; i++)
        {
            int index = i;
            string header = index < cells.Header.Length
                && cells.Header[index].Length > 0
                    ? cells.Header[index]
                    : string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"Column {index + 1}");
            columns.Add(new AccessibleGridColumn
            {
                Header = header,
                Cell = row => CellText(row, index),
                // Markdown cells are untyped text; ordinal keeps the
                // order deterministic across cultures.
                Sort = Comparer<object>.Create((x, y) =>
                    string.CompareOrdinal(CellText(x, index), CellText(y, index))),
                // A markdown table's first column is its natural key —
                // the row identity the UIA Table pattern serves.
                IsRowHeader = index == 0,
            });
        }
        string summary = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{cells.Rows.Length} rows, {columnCount} columns.");
        string label = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"Table, {cells.Rows.Length} rows, {columnCount} columns");
        return (columns, cells.Rows.Cast<object>().ToArray(), summary, label);
    }

    /// <summary>Ragged markdown rows are legal; a short row reads
    /// empty in the missing columns, never throws.</summary>
    private static string CellText(object row, int index)
    {
        string[] cells = (string[])row;
        return index < cells.Length ? cells[index] : string.Empty;
    }
}
