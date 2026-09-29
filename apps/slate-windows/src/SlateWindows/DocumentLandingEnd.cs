// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 8 (#1253, R-10): one attempt at a terminal seat. A document's
/// request stays pending only while a later edge can still realize its target;
/// a realized target that will not take focus ends it refused, so no request
/// is left live with nothing that will ever seat it.
/// </summary>
internal enum LandingSeat
{
    /// <summary>Focus is on the target.</summary>
    Seated,

    /// <summary>The target is not realized yet — not shown, its container not
    /// generated, the grid not bound to this publication: the edge that
    /// realizes it re-asks.</summary>
    NotYet,

    /// <summary>The target is realized and refused focus, or the projection
    /// cannot show it at all.</summary>
    Refused,
}

/// <summary>Terminal seats on a single element (R-10).</summary>
internal static class LandingSeats
{
    /// <summary>Not yet while <paramref name="target"/> is not shown (the edge
    /// that shows it re-asks); seated when the keys end up in it; refused when
    /// it is shown and will not take them. The seat goes through
    /// <see cref="SelectorFocus.LandOnStop"/>, the shell's one landing for an
    /// element-typed target (W7-7 PR 4, R-5: never a bare populated
    /// container; <c>SelectorLandingCensus</c>).</summary>
    internal static LandingSeat On(UIElement target) =>
        !target.IsVisible
            ? LandingSeat.NotYet
            : SelectorFocus.LandOnStop(target) && target.IsKeyboardFocusWithin
                ? LandingSeat.Seated
                : LandingSeat.Refused;
}

/// <summary>
/// W7-7 PR 8 (#1253, contract R-10): how a canvas or graph document's
/// addressed focus request ended, as the document itself reports it. The F6
/// ring reads it instead of inferring an outcome from where focus sits: a
/// graph seats a shell request PROVISIONALLY while its load is in flight, so
/// focus inside the surface says nothing about whether the landing was taken.
/// </summary>
internal enum DocumentLandingEnd
{
    /// <summary>A declared terminal seat took the landing and completed the
    /// request: the ring's Landed, spoken once.</summary>
    Seated,

    /// <summary>The document let go of the request without a terminal seat —
    /// a failure (a rows-only failure, a rejection, a load that failed) or
    /// the document torn down. The ring's Refused unless a move that cancels
    /// the landing (a rebind, an unload, the reader's leaving) comes first.</summary>
    Released,
}

/// <summary>The last addressed focus request a document ended, the owner it
/// was addressed to, and how (<see cref="DocumentLandingEnd"/>). Compared by
/// reference: a request is its own identity, and the record answers only for
/// that one.</summary>
internal sealed record DocumentLandingEnded(object Request, object Owner, DocumentLandingEnd End);
