// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 §E TE-1 (IE-1): one mutation invocation's opaque identity.
/// Reference identity only — the `CanvasRequestIdentity` discipline:
/// two operations built from equal inputs are DIFFERENT invocations,
/// so a committed-but-unpresented retry or a conflict reattempt can
/// tell the original from an equal later attempt.
/// </summary>
internal sealed class CanvasOperationId(string label)
{
    /// <summary>Diagnostic label — never an identity input.</summary>
    internal string Label { get; } = label;

    public override string ToString() => Label;
}

/// <summary>
/// W6-1 §E TE-1: the typed post-commit selection effect an operation
/// declares at mint (E4's table; TE-4 executes them against the
/// refreshed population, atomically with the publish).
/// </summary>
internal enum CanvasMutationEffect
{
    /// <summary>Keep the current selection (color, rename, edit).</summary>
    KeepSelection,

    /// <summary>Select the node the operation creates (New Card, New
    /// Group, the add verbs); the created id resolves against the
    /// REFRESHED population.</summary>
    SelectCreated,

    /// <summary>Clear the selection — mac's shipped behavior after a
    /// delete (IE-26: the "next-or-previous rule" does not exist in
    /// the twin, and this arm says so in the type).</summary>
    ClearSelection,
}

/// <summary>
/// W6-1 §E TE-1: THE OPERATION — one mutation invocation as a value:
/// identity (IE-1), the initiating owner and its optional source
/// anchor (IE-8 — several verbs have no row: New Card on an empty
/// canvas, the palette, a menu), the currency basis (IE-2), the
/// declared effect, and the mode token that lets a mode's OWN commit
/// pass the transient guard (C7's retry rule, IE-7's one-way half).
/// </summary>
/// <remarks>
/// <para>
/// CURRENCY IS THE `Basis` REFERENCE, never a scalar stamp (IE-2 —
/// the §C-unit frozen rule): the operation captures the
/// <see cref="CanvasLoaded"/> the verb was minted against, and
/// <see cref="IsCurrentAgainst"/> answers by reference comparison
/// against what the slot holds NOW. A reload, retarget or shutdown
/// installs a different reference — or none — and every boundary
/// (before the FFI call, after its return, before each dispatcher
/// effect) asks this one question.
/// </para>
/// <para>
/// The value is immutable; the GATE holds at most one at a time
/// (<see cref="CanvasMutationGate"/>), which is what makes the id
/// usable as the gate's epoch: "the same operation still holds" and
/// "a different operation holds now" are reference questions.
/// </para>
/// </remarks>
internal sealed class CanvasMutationOperation
{
    internal CanvasMutationOperation(
        CanvasOperationId id,
        object owner,
        string? anchor,
        CanvasLoaded basis,
        CanvasMutationEffect effect,
        object? modeToken = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(basis);
        Id = id;
        Owner = owner;
        Anchor = anchor;
        Basis = basis;
        Effect = effect;
        ModeToken = modeToken;
    }

    internal CanvasOperationId Id { get; }

    /// <summary>The initiating SURFACE — the pane that ran the verb,
    /// validated before every presentation or focus effect (IE-8's
    /// stable half; the row is the nullable <see cref="Anchor"/>).</summary>
    internal object Owner { get; }

    /// <summary>The initiating row's node id, when the verb had one.
    /// Nullable by design: New Card on an empty canvas, a menu or the
    /// palette have an owner and no anchor.</summary>
    internal string? Anchor { get; }

    /// <summary>The loaded triple this operation was minted against —
    /// the whole of its currency (IE-2).</summary>
    internal CanvasLoaded Basis { get; }

    internal CanvasMutationEffect Effect { get; }

    /// <summary>Non-null only for a mode's own commit: the funnel's
    /// transient guard admits exactly the operation carrying the live
    /// mode's token, so a refused commit leaves the mode and its
    /// transient standing (IE-7 / C7). PR F consumes this seam.</summary>
    internal object? ModeToken { get; }

    /// <summary>The one currency question, asked at every boundary:
    /// the publication is live and still holds the exact loaded triple
    /// this operation was minted against.</summary>
    internal bool IsCurrentAgainst(CanvasPublication now)
    {
        ArgumentNullException.ThrowIfNull(now);
        return !now.Retired && ReferenceEquals(now.Loaded, Basis);
    }
}
