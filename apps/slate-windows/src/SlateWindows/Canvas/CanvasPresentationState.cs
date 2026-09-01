// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// ONE installed presentation state (§D D1): everything the visual
/// projection's readers consume, immutable, installed by a single
/// reference swap on the commit thread. Task TD-1 carries the render
/// source and the viewport; TD-3 extends this value with the peer
/// topology — extended, not replaced, so the install discipline is
/// built once and every later field inherits it.
/// </summary>
internal sealed class CanvasPresentationState
{
    internal CanvasPresentationState(
        CanvasPublication source,
        CanvasViewportState viewport,
        CanvasPeerTopology topology,
        System.Collections.Immutable.ImmutableHashSet<CanvasPeerKey> retained,
        int textScaleRevision,
        int themeRevision,
        CanvasTransientHolder? transient = null,
        System.Collections.Immutable.ImmutableDictionary<string, CanvasRect>?
            transientRects = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(retained);
        Source = source;
        Viewport = viewport;
        Topology = topology;
        Retained = retained;
        TextScaleRevision = textScaleRevision;
        ThemeRevision = themeRevision;
        TransientAuthority = transient;
        TransientRects = transientRects;
    }

    /// <summary>§F TF-5 (F10): the transient authority this state was
    /// derived with — the install-time revalidation's reference, held
    /// whether or not the identity admitted it.</summary>
    internal CanvasTransientHolder? TransientAuthority { get; }

    /// <summary>§F TF-5 (F10): the ADMITTED hypothetical rects — null
    /// unless the transient's identity IS this state's loaded
    /// reference, so a sibling pane on another publication or a
    /// reloaded scene renders committed truth. The identity check ran
    /// once, in the derivation.</summary>
    internal System.Collections.Immutable.ImmutableDictionary<string, CanvasRect>?
        TransientRects { get; }

    /// <summary>§F TF-5 (F10): a node's EFFECTIVE rectangle — the
    /// transient hypothetical when admitted, the committed scene rect
    /// otherwise. Edges, the ring and hit-testing all answer from
    /// this; placements applied it at topology derivation.</summary>
    internal CanvasRect NodeRect(CanvasSceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return TransientRects is { } rects
            && rects.TryGetValue(node.NodeId, out CanvasRect? rect)
            ? rect
            : new CanvasRect(node.X, node.Y, node.Width, node.Height);
    }

    /// <summary>§F TF-5 (F10): the ONE identity check. Admitted rects
    /// come back only when the holder's identity is the publication's
    /// own loaded reference.</summary>
    internal static System.Collections.Immutable.ImmutableDictionary<string, CanvasRect>?
        EffectiveRects(CanvasPublication source, CanvasTransientHolder? transient) =>
        transient is not null
            && source.Loaded is { } loaded
            && ReferenceEquals(loaded, transient.Identity)
            ? transient.Rects
            : null;

    /// <summary>The peer topology this state was derived with (§D D3,
    /// task TD-3): materialized placements and the retained keys'
    /// tombstones — peers read THIS, and a discarded build's topology
    /// vanishes with the build.</summary>
    internal CanvasPeerTopology Topology { get; }

    /// <summary>The retained-key snapshot the topology was derived
    /// from — the third authority's half of the install-time
    /// revalidation.</summary>
    internal System.Collections.Immutable.ImmutableHashSet<CanvasPeerKey> Retained { get; }

    /// <summary>The applied publication this state renders — the
    /// identity the commit revalidates at install (ID-1), and the one
    /// carrier of population, unit and scene (D2): selection and
    /// filter state are DERIVED from it, never carried twice.</summary>
    internal CanvasPublication Source { get; }

    /// <summary>The viewport value the state was built against.</summary>
    internal CanvasViewportState Viewport { get; }

    /// <summary>The selection this state RENDERS (§D D6, obligation
    /// ID-5): the publication's durable selected intent, resolved
    /// against the population this state draws — never the unit's
    /// filtered resolution, which keeps a selection only when the
    /// filter matched it, while D4 keeps unmatched cards visible and
    /// selectable. A pure function of <see cref="Source"/>, so the
    /// mid-apply divergence the periphery's T5 row records cannot
    /// reach a reader: two states answer with their own selections
    /// and no live authority is consulted — obligation ID-6's settled
    /// direction, the machine gaining no event.</summary>
    internal string? Selection =>
        Source.Loaded?.Population.Resolve(Source.SelectedIntent);

    /// <summary>The text-scale revision consumed from the owned
    /// service (D11); a bumped revision is a new state.</summary>
    internal int TextScaleRevision { get; }

    /// <summary>The theme revision (D13's re-render trigger).</summary>
    internal int ThemeRevision { get; }
}
