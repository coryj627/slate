// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;

namespace SlateWindows.Canvas;

/// <summary>One picker row: the node and the palette-style label the
/// sheet renders and the filter searches.</summary>
internal sealed record CanvasCardPickerRow(string NodeId, string Label, string? Status = null);

/// <summary>
/// W6-1 §E TE-6 (IE-21 consumed): the card picker's MODEL — core owns
/// the proximity order (`canvas_proximity_order`: distance from the
/// anchor's centre, READING-ORDER ties, groups included), and this
/// type only renders and text-filters. The rows arrive ORDERED from
/// the factory; filtering preserves that order and never re-sorts —
/// a host-side comparator would be R-D's forbidden second algorithm,
/// and the battery pins its absence by construction.
/// </summary>
internal sealed class CanvasCardPickerModel
{
    private readonly ImmutableArray<CanvasCardPickerRow> _rows;

    internal CanvasCardPickerModel(IEnumerable<CanvasCardPickerRow> orderedRows)
    {
        ArgumentNullException.ThrowIfNull(orderedRows);
        _rows = [.. orderedRows];
    }

    /// <summary>Core's order, whole.</summary>
    internal ImmutableArray<CanvasCardPickerRow> Rows => _rows;

    /// <summary>The palette interaction model's filter: case-insensitive
    /// contains over the LABEL, order preserved.</summary>
    internal ImmutableArray<CanvasCardPickerRow> Visible(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.Length == 0
            ? _rows
            : [.. _rows.Where(row =>
                row.Label.Contains(query, StringComparison.CurrentCultureIgnoreCase))];
    }
}
