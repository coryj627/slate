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

    /// <summary>Put keyboard focus in the region: <see
    /// cref="ShellRegionLanding.Refused"/> when it cannot (the view model
    /// then tries the next region), <see cref="ShellRegionLanding.Pending"/>
    /// when the region holds the landing for content still arriving — the
    /// host then calls <paramref name="announceWhenLanded"/> once focus is
    /// actually in the region, or <paramref name="fallThroughWhenRefused"/>
    /// once the held landing turns out to be untakeable (the view model
    /// resumes the ring past it), and neither when it is withdrawn.</summary>
    ShellRegionLanding TryLand(
        ShellRegionKind region, Action announceWhenLanded, Action fallThroughWhenRefused);

    /// <summary>Let go of the landing the last <see
    /// cref="ShellRegionLanding.Pending"/> answer is holding (W7-7 PR 8, R-10:
    /// a newer press cancels it): no line, no fall-through, and focus is not
    /// moved for it. Answers whether it was still held with the reader
    /// exactly where the held press left them — false when the region already
    /// let go of it (it completed, the view changed) or the reader moved,
    /// even between two stops of one region.</summary>
    bool WithdrawHeldLanding();
}
