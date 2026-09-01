// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>The admission answer a verb's surface acts on: admitted,
/// or the typed refusal it announces-or-not (the busy arm consults
/// the TE-4 gate; every refusal keeps the surface's state).</summary>
internal enum CanvasMutationAdmission
{
    Admitted,
    NotReady,
    RecoveryPending,
    ConflictPending,
    ModeHeld,
    Busy,
    BusyAlreadyAnnounced,
    Stale,
}

/// <summary>
/// W6-1 §E TE-5: THE FUNNEL — the one `canvas_apply` call site (R-A).
/// Admission is E2's table in order; the transaction is E3's order
/// with the gate held throughout: apply (through the lease, currency
/// re-checked inside the FFI lock) → the history entry RECORDED
/// before anything fallible → refresh rows → ONE publish installing
/// rows and the effect-plan seat → release. A WriteConflict builds
/// the retained record with the pre-conflict snapshot taken at
/// refusal; a landed-but-unindexed write records its entry and marks
/// the publication committed-unpresented; a lease displaced mid-apply
/// still records its entry (which self-quarantines on the successor's
/// basis — TE-1's IE-5 receipt ruling) and publishes nothing.
/// </summary>
internal sealed class CanvasMutationFunnel(
    CanvasPublicationSlot slot,
    CanvasMutationGate gate,
    CanvasUndoStack history,
    CanvasBusyGate busy,
    ICanvasMutationSource writes,
    ICanvasLoadSource reads,
    Action<Action> run,
    Action<CanvasA11yEvent> announce)
{
    private CanvasConflictRecord? _conflict;
    private object? _modeToken;

    /// <summary>The resolving door's pass-through (IE-14): an
    /// Overwrite's own apply carries it in the mode-token slot.</summary>
    internal static readonly object ConflictResolutionToken = new();

    /// <summary>The standing conflict record, if any — the document's
    /// conflict value (IE-18) and the recovery surface read it.</summary>
    internal CanvasConflictRecord? Conflict => _conflict;

    /// <summary>PR F's seam: the live mode's token; a foreign token
    /// refuses out-of-band writes (IE-7).</summary>
    internal void SetModeToken(object? token) => _modeToken = token;

    /// <summary>E2's admission table, in order, then E3's transaction
    /// scheduled on the run seam. The answer is the ADMISSION;
    /// completion reaches the world through the collaborators.</summary>
    internal CanvasMutationAdmission Apply(
        CanvasMutationOperation operation, CanvasAction action, string name)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(name);

        CanvasPublication now = slot.Current;
        if (now.Retired || now.LoadState != CanvasLoadState.Ready)
        {
            return CanvasMutationAdmission.NotReady;
        }
        if (now.CommittedUnpresented is not null)
        {
            return CanvasMutationAdmission.RecoveryPending;
        }
        if (_conflict is { Terminal: false }
            && !ReferenceEquals(operation.ModeToken, ConflictResolutionToken))
        {
            return CanvasMutationAdmission.ConflictPending;
        }
        if (_modeToken is not null && !ReferenceEquals(operation.ModeToken, _modeToken))
        {
            return CanvasMutationAdmission.ModeHeld;
        }
        if (!operation.IsCurrentAgainst(now))
        {
            return CanvasMutationAdmission.Stale;
        }
        if (!gate.TryAcquire(operation))
        {
            CanvasMutationOperation? holder = gate.Held;
            return holder is not null && busy.ShouldAnnounce(holder)
                ? CanvasMutationAdmission.Busy
                : CanvasMutationAdmission.BusyAlreadyAnnounced;
        }

        run(() => Transact(operation, action, name));
        return CanvasMutationAdmission.Admitted;
    }

    private void Transact(
        CanvasMutationOperation operation, CanvasAction action, string name)
    {
        try
        {
            CanvasApplyResult? result = null;
            VaultException? refusal = null;
            bool ran = operation.Basis.Lease.Invoke(
                () => operation.IsCurrentAgainst(slot.Current),
                handle =>
                {
                    try
                    {
                        result = writes.Apply(handle, action);
                    }
                    catch (VaultException e)
                    {
                        refusal = e;
                        if (e is VaultException.WriteConflict)
                        {
                            // The pre-conflict snapshot AT refusal,
                            // gate held (IE-17): the in-memory canvas
                            // did not move.
                            _conflict = new CanvasConflictRecord(
                                operation, action, name,
                                failedBasis: history.AttachedBasis ?? string.Empty,
                                CanvasConflictHistoryPolicy.PushAndClearRedo,
                                writes.CurrentText(handle));
                        }
                    }
                });
            if (!ran)
            {
                return; // displaced before the call: nothing happened.
            }
            if (refusal is VaultException.WriteConflict)
            {
                announce(new CanvasA11yEvent.CanvasSaveConflict());
                return;
            }
            if (refusal is VaultException.SavedButUnindexed unindexed)
            {
                // A landed write IS a commit (TE-0's boundary): record
                // it, then surface the refresh-only state. The inverse
                // is unknown on this arm — the entry carries the empty
                // action and quarantines the moment anything moves.
                history.PushAndClearRedo(
                    new CanvasHistoryEntry(
                        name, new CanvasAction(name, []), unindexed.newContentHash),
                    unindexed.newContentHash);
                MarkUnpresented(operation);
                return;
            }
            if (refusal is not null)
            {
                return; // rejected whole (InvalidArgument): no motion.
            }

            var entry = new CanvasHistoryEntry(
                name, result!.Inverse, result.NewContentHash);
            if (!operation.IsCurrentAgainst(slot.Current))
            {
                // Displaced mid-apply (IE-5's receipt arm): retained
                // WITHOUT rebasing — the live basis belongs to the
                // displacing reload, so the entry lands quarantined
                // rather than offered against a document it does not
                // describe. No publish, no effects.
                history.PushRetained(entry);
                return;
            }

            // E3: the entry is recorded before anything fallible.
            history.PushAndClearRedo(entry, result.NewContentHash);
            if (!result.Indexed)
            {
                MarkUnpresented(operation);
                return;
            }
            RefreshAndPublish(operation);
        }
        finally
        {
            gate.Release(operation);
        }
    }

    private void MarkUnpresented(CanvasMutationOperation operation) =>
        _ = slot.Publish(s =>
            s.Retired ? null : s.WithCommittedUnpresented(operation.Id));

    private void RefreshAndPublish(CanvasMutationOperation operation)
    {
        CanvasOutlineRow[]? outline = null;
        CanvasTableRow[]? tableRows = null;
        CanvasScene? scene = null;
        bool read = operation.Basis.Lease.Invoke(
            () => operation.IsCurrentAgainst(slot.Current),
            handle =>
            {
                outline = reads.Outline(handle);
                tableRows = reads.TableRows(handle);
                scene = reads.Scene(handle);
            });
        if (!read)
        {
            return;
        }
        var population = new CanvasPopulation(
            outline,
            tableRows,
            slot.Current.Population?.Warnings,
            slot.Current.Population?.LastActivatedNode,
            scene,
            contentHash: history.AttachedBasis ?? string.Empty);
        CanvasEffectResolution seat = CanvasEffectPlan.ResolveSelection(
            operation.Effect,
            population,
            slot.Current.SelectedIntent,
            createdId: null);
        if (seat.IsRequiredTargetMissing)
        {
            MarkUnpresented(operation);
            return;
        }
        _ = slot.Publish(s =>
            !s.Retired && ReferenceEquals(s.Loaded, operation.Basis)
                ? s.WithLoaded(
                        operation.Basis.Lease,
                        population,
                        CanvasProjectionUnit
                            .Unfiltered(population)
                            .WithResolvedSelection(seat.SeatValue))
                    .WithSelectedIntent(seat.SeatValue)
                : null);
    }
}
