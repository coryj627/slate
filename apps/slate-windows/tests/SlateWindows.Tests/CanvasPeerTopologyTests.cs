// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// §D task TD-3, the topology half: the three-cell table's data side —
/// window materialization, ID-4's path-bounds edge rule, discriminated
/// identity, and tombstones only for the retained.
/// </summary>
public sealed class CanvasPeerTopologyTests
{
    private static readonly ImmutableHashSet<CanvasPeerKey> NoneRetained = [];

    private static CanvasPopulation Population(
        CanvasSceneNode[] nodes, CanvasSceneEdge[]? edges = null) =>
        new(null, null, null, null, new CanvasScene(nodes, edges ?? []));

    private static CanvasSceneNode Node(
        string id, double x, double y, double w = 100, double h = 50) =>
        new(id, "text", id, id, x, y, w, h, null, null, null);

    private static CanvasViewportState View(double w = 400, double h = 300) =>
        CanvasViewportState.Seed().WithViewSize(w, h);

    /// <summary>A card inside the window materializes with its
    /// document rectangle; one beyond window-plus-margin does not, and
    /// carries no entry at all — UNREALIZED is the index's cell, not a
    /// stored one.</summary>
    [Fact]
    public void CardsMaterializeByTheWindowPlusOneMargin()
    {
        CanvasPopulation population = Population(
            [Node("in", 10, 10), Node("margin", 500, 10), Node("far", 5_000, 10)]);
        CanvasPeerTopology topology = CanvasPeerTopology.Derive(
            population, View(), NoneRetained);
        Assert.True(
            topology.Placements[CanvasPeerKey.Card("in")].Cell
                == CanvasPeerCell.Materialized
            && topology.Placements[CanvasPeerKey.Card("margin")].Cell
                == CanvasPeerCell.Materialized,
            "a card in the window, or one margin beyond it, must materialize.");
        Assert.True(
            !topology.Placements.ContainsKey(CanvasPeerKey.Card("far")),
            "a card past the margin carried an entry: the unrealized cell "
            + "is the descriptor index's to answer, not the topology's.");
    }

    /// <summary>Obligation ID-4's arrangement: both endpoints outside
    /// the window, the path crossing it — the labelled edge
    /// materializes by its RENDERED bounds, not endpoint
    /// membership.</summary>
    [Fact]
    public void ALongEdgeCrossingTheWindowMaterializesWithoutItsEndpoints()
    {
        CanvasPopulation population = Population(
            [Node("west", -10_000, 100), Node("east", 10_000, 100)],
            [new("cross", "west", null, "east", null, false, true, "spans", null)]);
        CanvasPeerTopology topology = CanvasPeerTopology.Derive(
            population, View(), NoneRetained);
        Assert.True(
            !topology.Placements.ContainsKey(CanvasPeerKey.Card("west"))
                && !topology.Placements.ContainsKey(CanvasPeerKey.Card("east")),
            "premise: both endpoints sit far outside the window.");
        Assert.True(
            topology.Placements.TryGetValue(
                CanvasPeerKey.Edge("cross"), out CanvasPeerPlacement? edge)
            && edge.Cell == CanvasPeerCell.Materialized,
            "the crossing edge did not materialize: 'either endpoint in the "
            + "window' is the narrowing ID-4 forbids — the rendered bounds "
            + "intersect the window, so the peer exists.");
    }

    /// <summary>DD-2: an unlabelled edge is not a peer, wherever it
    /// crosses.</summary>
    [Fact]
    public void AnUnlabelledEdgeIsNeverAPeer()
    {
        CanvasPopulation population = Population(
            [Node("a", 0, 0), Node("b", 200, 0)],
            [new("mute", "a", null, "b", null, false, true, null, null)]);
        CanvasPeerTopology topology = CanvasPeerTopology.Derive(
            population, View(), NoneRetained);
        Assert.True(
            !topology.Placements.ContainsKey(CanvasPeerKey.Edge("mute")),
            "an unlabelled edge materialized: it has no accessible handle "
            + "by DD-2, and its existence is readable from its endpoints.");
    }

    /// <summary>Tombstones exist ONLY for retained keys, and a card
    /// and an edge sharing an id string are two identities.</summary>
    [Fact]
    public void TombstonesAreRetainedOnlyAndIdentityIsDiscriminated()
    {
        CanvasPopulation population = Population(
            [Node("shared", 5_000, 5_000)],
            [new("shared", "shared", null, "shared", null, false, true, "loop", null)]);
        ImmutableHashSet<CanvasPeerKey> retained =
            [CanvasPeerKey.Card("shared")];
        CanvasPeerTopology topology = CanvasPeerTopology.Derive(
            population, View(), retained);
        Assert.True(
            topology.Placements.TryGetValue(
                CanvasPeerKey.Card("shared"), out CanvasPeerPlacement? card)
            && card.Cell == CanvasPeerCell.Placeholder,
            "the retained off-window card must carry a tombstone.");
        Assert.True(
            !topology.Placements.ContainsKey(CanvasPeerKey.Edge("shared")),
            "the unretained off-window edge carried an entry — or the card "
            + "and edge identities collapsed into one key (D3's "
            + "discriminated identity).");
    }
}
