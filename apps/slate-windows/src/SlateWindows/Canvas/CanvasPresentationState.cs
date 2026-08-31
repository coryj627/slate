// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

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
        int textScaleRevision,
        int themeRevision)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(viewport);
        Source = source;
        Viewport = viewport;
        TextScaleRevision = textScaleRevision;
        ThemeRevision = themeRevision;
    }

    /// <summary>The applied publication this state renders — the
    /// identity the commit revalidates at install (ID-1), and the one
    /// carrier of population, unit and scene (D2): selection and
    /// filter state are DERIVED from it, never carried twice.</summary>
    internal CanvasPublication Source { get; }

    /// <summary>The viewport value the state was built against.</summary>
    internal CanvasViewportState Viewport { get; }

    /// <summary>The text-scale revision consumed from the owned
    /// service (D11); a bumped revision is a new state.</summary>
    internal int TextScaleRevision { get; }

    /// <summary>The theme revision (D13's re-render trigger).</summary>
    internal int ThemeRevision { get; }
}
