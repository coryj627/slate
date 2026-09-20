// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows;

/// <summary>What the window answers for an F6 press (W7-6, #1240): where
/// focus is, whether a modal surface owns the keys, and whether a region
/// can take focus. The view model owns the ring and the announcement;
/// the window owns elements. <c>ShellRegionCycleTests</c> drives this
/// through a fake.</summary>
internal interface IShellRegionHost
{
    /// <summary>A sheet, overlay or palette is up: F6 is a no-op (spec §4).</summary>
    bool ModalSurfaceOpen { get; }

    /// <summary>The shown right-pane leaf has a focusable first stop.</summary>
    bool RightPaneHasContentStop { get; }

    /// <summary>The status bar's current text, spoken on that landing.</summary>
    string StatusText { get; }

    /// <summary>The region containing keyboard focus, or null when focus
    /// is on the window root, an overlay, or nowhere.</summary>
    ShellRegionKind? FocusedRegion();

    /// <summary>Put keyboard focus in the region; false when it cannot
    /// (the view model then tries the next region).</summary>
    bool TryLand(ShellRegionKind region);
}
