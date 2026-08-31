// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// A peer's discriminated identity (§D D3): CARD or EDGE plus the
/// core id, so a card and an edge sharing an id string are two peers.
/// The renderer and document halves of the identity are structural —
/// one topology per engine, one engine per renderer view — so the
/// value carries only what varies inside one renderer.
/// </summary>
internal readonly record struct CanvasPeerKey(bool IsEdge, string Id)
{
    internal static CanvasPeerKey Card(string nodeId) => new(false, nodeId);

    internal static CanvasPeerKey Edge(string edgeId) => new(true, edgeId);
}

/// <summary>
/// One peer's cell in the installed state (§D D3's three-cell table).
/// UNREALIZED is the absence of an entry — the descriptor index
/// answers a first-touch lookup and no per-item value exists until a
/// peer is minted.
/// </summary>
internal enum CanvasPeerCell
{
    /// <summary>Off the window but externally retained: the
    /// virtualized-item pattern always, identifying properties from
    /// the descriptor, action patterns refused until
    /// realization.</summary>
    Placeholder,

    /// <summary>In the window: the full peer, its rectangle
    /// recomputed with every installed state.</summary>
    Materialized,
}

/// <summary>
/// One materialized or retained peer's placement: its cell and — when
/// materialized — its DOCUMENT-space rectangle. Screen conversion is
/// the view's multiply at read time; the topology stays view-agnostic
/// so the derivation is pure over (population, viewport,
/// retained).</summary>
internal sealed class CanvasPeerPlacement
{
    private CanvasPeerPlacement(
        CanvasPeerCell cell, double x, double y, double width, double height)
    {
        Cell = cell;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    internal static CanvasPeerPlacement Materialized(
        double x, double y, double width, double height) =>
        new(CanvasPeerCell.Materialized, x, y, width, height);

    internal static CanvasPeerPlacement Tombstone() =>
        new(CanvasPeerCell.Placeholder, 0, 0, 0, 0);

    internal CanvasPeerCell Cell { get; }

    internal double X { get; }

    internal double Y { get; }

    internal double Width { get; }

    internal double Height { get; }
}

/// <summary>
/// The peer topology inside one installed presentation state (§D D3,
/// obligation ID-4): which peers are materialized with what
/// document-space rectangles, and which retained keys carry
/// tombstones. Derived pure over (population, viewport, retained
/// keys); a discarded build's topology vanishes with it, which is
/// half of the retirement rule — identities commit only with a
/// winning install, and the other half is the registry holding weak
/// identity only.
/// </summary>
internal sealed class CanvasPeerTopology
{
    private CanvasPeerTopology(
        ImmutableDictionary<CanvasPeerKey, CanvasPeerPlacement> placements)
    {
        Placements = placements;
    }

    internal static CanvasPeerTopology Empty() =>
        new(ImmutableDictionary<CanvasPeerKey, CanvasPeerPlacement>.Empty);

    internal ImmutableDictionary<CanvasPeerKey, CanvasPeerPlacement> Placements { get; }

    /// <summary>
    /// The window derivation. Cards materialize when their rectangle
    /// intersects the viewport plus one viewport's margin on every
    /// side (D4). EDGES materialize by their RENDERED bounds — the
    /// bounding box of both endpoints' rectangles, which contains the
    /// straight path and its label chip — never by endpoint
    /// membership, so a long edge crossing the window materializes
    /// while both its endpoints sit outside (obligation ID-4's
    /// arrangement). A retained key that did not materialize carries a
    /// tombstone; an unretained, unmaterialized key has no entry at
    /// all — the UNREALIZED cell is the descriptor index's to answer.
    /// </summary>
    internal static CanvasPeerTopology Derive(
        CanvasPopulation population,
        CanvasViewportState viewport,
        IReadOnlySet<CanvasPeerKey> retained)
    {
        ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(retained);
        // The window in DOCUMENT space: the view rect un-transformed,
        // widened by one view on every side. A zero-sized view (no
        // layout yet) materializes nothing and tombstones the
        // retained, which is the honest answer before first measure.
        double viewW = viewport.ViewWidth / Math.Max(viewport.Zoom, double.Epsilon);
        double viewH = viewport.ViewHeight / Math.Max(viewport.Zoom, double.Epsilon);
        double left = (0 - viewport.PanX) / Math.Max(viewport.Zoom, double.Epsilon) - viewW;
        double top = (0 - viewport.PanY) / Math.Max(viewport.Zoom, double.Epsilon) - viewH;
        double right = left + (viewW * 3);
        double bottom = top + (viewH * 3);

        ImmutableDictionary<CanvasPeerKey, CanvasPeerPlacement>.Builder placements =
            ImmutableDictionary.CreateBuilder<CanvasPeerKey, CanvasPeerPlacement>();
        foreach (CanvasSceneNode node in population.SceneNodes)
        {
            if (Intersects(node.X, node.Y, node.Width, node.Height))
            {
                placements[CanvasPeerKey.Card(node.NodeId)] =
                    CanvasPeerPlacement.Materialized(
                        node.X, node.Y, node.Width, node.Height);
            }
        }
        foreach (CanvasSceneEdge edge in population.SceneEdges)
        {
            if (edge.Label is not { Length: > 0 })
            {
                // DD-2: an unlabelled edge is not a peer.
                continue;
            }
            if (population.SceneByNode.TryGetValue(edge.FromNode, out CanvasSceneNode? from)
                && population.SceneByNode.TryGetValue(edge.ToNode, out CanvasSceneNode? to))
            {
                double x = Math.Min(from.X, to.X);
                double y = Math.Min(from.Y, to.Y);
                double w = Math.Max(from.X + from.Width, to.X + to.Width) - x;
                double h = Math.Max(from.Y + from.Height, to.Y + to.Height) - y;
                if (Intersects(x, y, w, h))
                {
                    placements[CanvasPeerKey.Edge(edge.EdgeId)] =
                        CanvasPeerPlacement.Materialized(x, y, w, h);
                }
            }
        }
        foreach (CanvasPeerKey key in retained)
        {
            if (!placements.ContainsKey(key))
            {
                placements[key] = CanvasPeerPlacement.Tombstone();
            }
        }
        return new(placements.ToImmutable());

        bool Intersects(double x, double y, double w, double h) =>
            x < right && x + w > left && y < bottom && y + h > top;
    }
}
