// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows;

/// <summary>
/// The ONE scheme allowlist for every surface that hands a target to the
/// shell — the mac allowlist verbatim. Anything else (<c>file:</c>,
/// <c>javascript:</c>, a custom scheme) is refused, so a hostile
/// <c>.bib</c> entry, a note's link, or a canvas link card cannot smuggle
/// a local-execution target past a surface that forgot to check.
/// </summary>
/// <remarks>
/// <para>
/// W6-1 PR A (#745) extracted this. It had been copied three times —
/// the right-pane panels, the citation popover, and then the canvas
/// link card — and the third copy is what made it a drift surface
/// rather than a coincidence: three literals of
/// <c>"http" or "https" or "mailto"</c> that a future scheme addition
/// would have to find all of.
/// </para>
/// <para>
/// The PREDICATE is shared; the announcements deliberately are not.
/// The panels and the citation popover speak the
/// <c>ExternalLink*</c> vocabulary; the canvas speaks its own
/// (<c>CanvasOpened</c> / <c>CanvasBlocked</c>), because a canvas
/// surface that suddenly spoke a different family's sentences would be
/// the §W-D drift this programme exists to stop. One rule, three
/// voices.
/// </para>
/// </remarks>
internal static class ExternalLinkPolicy
{
    /// <summary>Whether the shell may be handed this target.</summary>
    internal static bool IsLaunchable(string? target) =>
        target is { Length: > 0 }
        && Uri.TryCreate(target, UriKind.Absolute, out Uri? uri)
        && uri.Scheme is "http" or "https" or "mailto";
}
