// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 §E TE-4 (IE-11/IE-35): the MODEL-SIDE effect resolution — a
/// pure function the publish transform calls, so the refreshed rows
/// and the seat they imply install in ONE swap. The post-publication
/// half (announcements, editor-open, focus) cannot run here (§D's
/// no-callouts rule for publication transforms) and is tracked by
/// <see cref="CanvasOperationCompletion"/> instead.
/// </summary>
internal static class CanvasEffectPlan
{
    /// <summary>Resolve the operation's declared selection effect
    /// against the REFRESHED population. A REQUIRED target that the
    /// refresh cannot resolve — SelectCreated whose created id is
    /// missing — is IE-35's committed-but-unpresented signal, never
    /// the silent drop: the drop rule stays for the OPTIONAL current
    /// seat (KeepSelection over a deleted node resolves to null and
    /// that is the truth, not a failure).</summary>
    internal static CanvasEffectResolution ResolveSelection(
        CanvasMutationEffect effect,
        CanvasPopulation refreshed,
        string? currentIntent,
        string? createdId)
    {
        ArgumentNullException.ThrowIfNull(refreshed);
        switch (effect)
        {
            case CanvasMutationEffect.ClearSelection:
                return CanvasEffectResolution.Seat(null);
            case CanvasMutationEffect.KeepSelection:
                return CanvasEffectResolution.Seat(refreshed.Resolve(currentIntent));
            case CanvasMutationEffect.SelectCreated:
                if (createdId is null || refreshed.Resolve(createdId) is not { } created)
                {
                    return CanvasEffectResolution.RequiredTargetMissing;
                }
                return CanvasEffectResolution.Seat(created);
            default:
                throw new CanvasLeaseViolationException(
                    $"an effect arm nobody declared: {effect}");
        }
    }
}

/// <summary>The resolution: a seat to publish atomically, or IE-35's
/// typed failure — the commit is real, its presentation is not.</summary>
internal sealed class CanvasEffectResolution
{
    private CanvasEffectResolution(bool missing, string? seat)
    {
        IsRequiredTargetMissing = missing;
        SeatValue = seat;
    }

    internal static CanvasEffectResolution RequiredTargetMissing { get; } =
        new(missing: true, seat: null);

    internal static CanvasEffectResolution Seat(string? seat) => new(missing: false, seat);

    internal bool IsRequiredTargetMissing { get; }

    internal string? SeatValue { get; }
}

/// <summary>
/// W6-1 §E TE-4 (IE-11): the post-publication effects' COMPLETION
/// STATE, kept under the operation identity — each addressed effect
/// runs at most once, a retry after a failure re-runs only what never
/// ran, and a duplicate announcement is unspellable rather than
/// guarded by convention.
/// </summary>
internal sealed class CanvasOperationCompletion(CanvasOperationId operation)
{
    private int _announced;
    private int _editorOpened;
    private int _focusReturned;

    internal CanvasOperationId Operation { get; } =
        operation ?? throw new ArgumentNullException(nameof(operation));

    /// <summary>True exactly once — the caller that wins runs the
    /// effect; every retry sees false.</summary>
    internal bool TryMarkAnnounced() => Interlocked.Exchange(ref _announced, 1) == 0;

    internal bool TryMarkEditorOpened() => Interlocked.Exchange(ref _editorOpened, 1) == 0;

    internal bool TryMarkFocusReturned() => Interlocked.Exchange(ref _focusReturned, 1) == 0;
}

/// <summary>
/// W6-1 §E TE-4 (IE-34): one audible busy refusal per gate hold. The
/// held OPERATION is the epoch — key repeat during one slow apply
/// announces once, and a NEW hold announces again. No timer: two
/// distinct holds 50 ms apart are two true refusals, and merging them
/// would trade correctness for quiet.
/// </summary>
internal sealed class CanvasBusyGate
{
    private CanvasMutationOperation? _lastAnnouncedHold;

    /// <summary>Whether THIS refusal is the hold's first — the audible
    /// one. The surface stays enabled and keeps its state either way
    /// (IE-9; the funnel wires the keeping).</summary>
    internal bool ShouldAnnounce(CanvasMutationOperation heldOperation)
    {
        ArgumentNullException.ThrowIfNull(heldOperation);
        if (ReferenceEquals(_lastAnnouncedHold, heldOperation))
        {
            return false;
        }
        _lastAnnouncedHold = heldOperation;
        return true;
    }
}
