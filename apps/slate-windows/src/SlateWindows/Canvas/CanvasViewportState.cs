// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Canvas;

/// <summary>
/// The viewport half of the visual projection's presentation state
/// (§D D1): zoom, pan and view size as one immutable value. A viewport
/// command commits a successor of this value on the commit thread
/// BEFORE any presentation build is queued, so no build ever owns a
/// viewport delta and a discarded build cannot swallow a zoom (ID-2's
/// sibling guarantee, ratified in D1). Every transform allocates (I5).
/// </summary>
internal sealed class CanvasViewportState
{
    /// <summary>The §2 I constants, pinned by fact: clamp bounds, the
    /// zoom step, and the two fit paddings (logical pixels).</summary>
    internal const double MinZoom = 0.1;
    internal const double MaxZoom = 4.0;
    internal const double ZoomStep = 1.25;
    internal const double FitPadding = 40.0;
    internal const double FitSelectionPadding = 120.0;

    private CanvasViewportState(
        double zoom, double panX, double panY,
        double viewWidth, double viewHeight, bool followSelection)
    {
        Zoom = zoom;
        PanX = panX;
        PanY = panY;
        ViewWidth = viewWidth;
        ViewHeight = viewHeight;
        FollowSelection = followSelection;
    }

    /// <summary>The seed: identity zoom, origin pan, no view yet,
    /// follow-selection ON (the ratified default).</summary>
    internal static CanvasViewportState Seed() => new(1.0, 0.0, 0.0, 0.0, 0.0, true);

    internal double Zoom { get; }

    internal double PanX { get; }

    internal double PanY { get; }

    internal double ViewWidth { get; }

    internal double ViewHeight { get; }

    /// <summary>Whether a selection arriving from ANOTHER surface pans
    /// the viewport (D4's origin-sensitive rule). A selection made on
    /// the visual surface scrolls into view regardless.</summary>
    internal bool FollowSelection { get; }

    /// <summary>Zoom by one step toward the ceiling, preserving the
    /// given centre point (document coordinates stay under it).</summary>
    internal CanvasViewportState ZoomedIn(double centreX, double centreY) =>
        WithZoom(Zoom * ZoomStep, centreX, centreY);

    /// <summary>Zoom by one step toward the floor, centre-preserving.</summary>
    internal CanvasViewportState ZoomedOut(double centreX, double centreY) =>
        WithZoom(Zoom / ZoomStep, centreX, centreY);

    /// <summary>Actual size: zoom 1.0, centre-preserving.</summary>
    internal CanvasViewportState AtActualSize(double centreX, double centreY) =>
        WithZoom(1.0, centreX, centreY);

    /// <summary>The one zoom arithmetic: clamp, then move the pan so
    /// the document point under the given view-space centre stays
    /// under it. Every zoom verb routes here so the clamp and the
    /// centre rule cannot drift apart.</summary>
    internal CanvasViewportState WithZoom(double zoom, double centreX, double centreY)
    {
        double clamped = Math.Clamp(zoom, MinZoom, MaxZoom);
        if (Zoom == 0)
        {
            return new(clamped, PanX, PanY, ViewWidth, ViewHeight, FollowSelection);
        }
        double scale = clamped / Zoom;
        double panX = centreX - ((centreX - PanX) * scale);
        double panY = centreY - ((centreY - PanY) * scale);
        return new(clamped, panX, panY, ViewWidth, ViewHeight, FollowSelection);
    }

    /// <summary>Pan to an absolute offset (view-space).</summary>
    internal CanvasViewportState PannedTo(double panX, double panY) =>
        new(Zoom, panX, panY, ViewWidth, ViewHeight, FollowSelection);

    /// <summary>The view's size changed (layout, DPI, window).</summary>
    internal CanvasViewportState WithViewSize(double width, double height) =>
        new(Zoom, PanX, PanY, width, height, FollowSelection);

    /// <summary>The follow toggle (D4). A transform, not a setter —
    /// the toggle is viewport state exactly like the zoom.</summary>
    internal CanvasViewportState WithFollowSelection(bool follow) =>
        new(Zoom, PanX, PanY, ViewWidth, ViewHeight, follow);

    /// <summary>Whether two values would produce the same transform —
    /// the build deduplication's viewport half (ID-2).</summary>
    internal bool SameGeometry(CanvasViewportState other) =>
        Zoom == other.Zoom
        && PanX == other.PanX
        && PanY == other.PanY
        && ViewWidth == other.ViewWidth
        && ViewHeight == other.ViewHeight
        && FollowSelection == other.FollowSelection;
}
