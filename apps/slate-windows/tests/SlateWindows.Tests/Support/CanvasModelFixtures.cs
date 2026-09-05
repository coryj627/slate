// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// The model-shaped arrangements every canvas battery kept building by
/// hand — one flat population, one depth-shaped one (the cleanup pass:
/// the same eight lines lived in three batteries).
/// </summary>
internal static class CanvasModelFixtures
{
    /// <summary>A flat population over the given ids: depth zero,
    /// ordinal one, no groups — the shape the slot, machine and lease
    /// facts race and filter.</summary>
    internal static CanvasPopulation Population(params string[] nodeIds) => new(
        nodeIds.Select(id => new CanvasOutlineRow(
            id, 0, "text", id, id, [], 1, (uint)nodeIds.Length, 0, null)),
        null,
        null,
        null,
        null);

    /// <summary>A depth-shaped population — the ancestry facts' input,
    /// with everything but id and depth held constant.</summary>
    internal static CanvasPopulation Outline(params (string Id, uint Depth)[] rows) => new(
        rows.Select(row => new CanvasOutlineRow(
            row.Id, row.Depth, "text", row.Id, row.Id, [], 1, (uint)rows.Length, 0, null)),
        null,
        null,
        null,
        null);
}
