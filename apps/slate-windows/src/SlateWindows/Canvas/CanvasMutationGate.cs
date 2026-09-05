// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 §E TE-1: THE GATE — one per-document admission cell holding at
/// most ONE mutation operation through its whole transaction (prepare,
/// apply, record, refresh, effects). Acquisition is one atomic
/// compare-and-swap (IE-33: never check-then-enter — two callers that
/// both observed "free" would otherwise both proceed and the loser
/// would queue, which ED-5 forbids); a verb arriving while the gate is
/// held is REFUSED, not queued, because mac's main-actor serialization
/// is the twin's real semantic and a queue would invent interleavings
/// mac cannot produce.
/// </summary>
/// <remarks>
/// The held OPERATION is the gate's whole state: identity and epoch in
/// one reference, so "who holds" and "is this still the same hold" are
/// the same question the operation's id already answers. Release by
/// anything but the holder is the TRIPWIRE, not a refusal — the
/// model's invariants contradict each other if two parties think they
/// hold one cell — and it throws the exception every survivable-catch
/// filter excludes by name (`CanvasFaults`).
/// </remarks>
internal sealed class CanvasMutationGate
{
    private CanvasMutationOperation? _holder;

    /// <summary>The holding operation, or null when the gate is free.
    /// A volatile read for enablement readers; admission never reads —
    /// it swaps.</summary>
    internal CanvasMutationOperation? Held => Volatile.Read(ref _holder);

    /// <summary>One atomic acquisition (IE-33). True and the gate is
    /// held by <paramref name="operation"/>; false and it was already
    /// held — the caller refuses with the typed busy status, keeping
    /// its surface state (IE-9's rule lands with the funnel).</summary>
    internal bool TryAcquire(CanvasMutationOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return Interlocked.CompareExchange(ref _holder, operation, null) is null;
    }

    /// <summary>Release by the holder, and only the holder. A release
    /// by any other operation is an invariant breach, not a race to
    /// tolerate.</summary>
    internal void Release(CanvasMutationOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!ReferenceEquals(
            Interlocked.CompareExchange(ref _holder, null, operation), operation))
        {
            throw new CanvasLeaseViolationException(
                "a mutation gate was released by an operation that does not hold "
                + "it: one cell, one holder, and the holder releases exactly once "
                + "— two parties believing they hold one gate is the invariant "
                + "contradiction, not a refusal");
        }
    }
}
