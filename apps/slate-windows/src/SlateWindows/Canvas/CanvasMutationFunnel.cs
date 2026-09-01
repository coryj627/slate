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
    CanvasLoadPipeline pipeline,
    Action<Action> run,
    Action<CanvasA11yEvent> announce)
{
    private CanvasConflictRecord? _conflict;
    private object? _modeToken;

    private object? _suspendedModeToken;

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
    /// completion reaches the world through the collaborators.
    /// PREPARATION runs inside the transaction, under the gate and the
    /// SAME lease hold as the apply (round 1's #5: a placement or
    /// lookup outside the write boundary goes stale between query and
    /// write) — `prepare` receives the handle and answers the action,
    /// or null to refuse after its own typed announcement.</summary>
    internal CanvasMutationAdmission Apply(
        CanvasMutationOperation operation,
        Func<ulong, CanvasAction?> prepare,
        string name,
        Func<CanvasA11yEvent>? confirm = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(prepare);
        ArgumentNullException.ThrowIfNull(name);

        CanvasMutationAdmission admission = AdmitAndAcquire(operation);
        if (admission != CanvasMutationAdmission.Admitted)
        {
            AnnounceAdmission(admission);
            return admission;
        }

        run(() => Transact(operation, prepare, name, confirm));
        return CanvasMutationAdmission.Admitted;
    }

    /// <summary>E2's ladder, shared by the verb and history
    /// entrypoints: Admitted means the GATE IS HELD and the caller
    /// must schedule a transaction that releases it.</summary>
    private CanvasMutationAdmission AdmitAndAcquire(CanvasMutationOperation operation)
    {
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
        // §F TF-1 (IF-7): the ladder read the token BEFORE acquiring,
        // and a mode can install between the read and the acquire — an
        // operation that admitted through that window would refresh
        // through a transient it never owned. Re-ask both stateful
        // questions UNDER the held gate; a mismatch releases and
        // refuses with the same arm the pre-read would have used.
        if (_modeToken is not null
            && !ReferenceEquals(operation.ModeToken, _modeToken))
        {
            gate.Release(operation);
            return CanvasMutationAdmission.ModeHeld;
        }
        if (_conflict is { Terminal: false }
            && !ReferenceEquals(operation.ModeToken, ConflictResolutionToken))
        {
            gate.Release(operation);
            return CanvasMutationAdmission.ConflictPending;
        }
        return CanvasMutationAdmission.Admitted;
    }

    /// <summary>§F TF-1 (IF-8): mode entry's preflight — the same
    /// ladder, and on Admitted the mode token installs UNDER the held
    /// gate before it releases, so no admitted operation can be in
    /// flight when the token lands and none can admit past it after.
    /// Every refusal speaks its §E sentence. The caller must check
    /// the C machine's own AdmitsEntry FIRST (M7's rejection arm owns
    /// the active-mode case), and must roll the token back via
    /// <see cref="ClearModeToken"/> if the machine then refuses.</summary>
    internal CanvasMutationAdmission AdmitModeEntry(CanvasMutationOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.ModeToken is null)
        {
            throw new CanvasLeaseViolationException(
                "a mode entry preflight requires the mode's token on the operation");
        }
        CanvasMutationAdmission admission = AdmitAndAcquire(operation);
        if (admission != CanvasMutationAdmission.Admitted)
        {
            AnnounceAdmission(admission);
            return admission;
        }
        _modeToken = operation.ModeToken;
        gate.Release(operation);
        return CanvasMutationAdmission.Admitted;
    }

    /// <summary>§F TF-1 (F4c): the identity-checked clear — only the
    /// token that holds may clear, so a stale clear from an ended mode
    /// cannot strip a successor's hold. Every row of the clear table
    /// routes here: installed success, no-effect Return, Esc, the F1a
    /// cancels, restoration failure, and the entry rollback.</summary>
    internal void ClearModeToken(object token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (ReferenceEquals(_modeToken, token))
        {
            _modeToken = null;
        }
    }

    /// <summary>§F TF-10 (IF-30): whether a mode's token is
    /// SUSPENDED — the conflict yielded it and the transient is
    /// frozen. Steps and presets gate on this; the ladder already
    /// refuses everything that submits.</summary>
    internal bool ModeSuspended => _suspendedModeToken is not null;

    /// <summary>§F TF-10 (IF-30): the suspended column's one voice —
    /// a gated step or preset speaks the ladder's own ConflictPending
    /// sentence, never a second phrasing.</summary>
    internal void AnnounceConflictPending() =>
        AnnounceAdmission(CanvasMutationAdmission.ConflictPending);

    /// <summary>§F TF-1 (FD-5, the token half): the conflict
    /// completion YIELDS the mode's token so the recovery's writes pass
    /// through <see cref="ConflictResolutionToken"/>; the suspended
    /// identity is remembered so resolution-continuation can reinstall
    /// it — and ONLY it: a reload's F1a cancel forgets the identity,
    /// so a late reinstall finds a mismatch and no-ops.</summary>
    internal void SuspendModeToken(object token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (ReferenceEquals(_modeToken, token))
        {
            _modeToken = null;
            _suspendedModeToken = token;
        }
    }

    /// <summary>The reinstall half of <see cref="SuspendModeToken"/>.</summary>
    internal bool ReinstallSuspendedModeToken(object token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (!ReferenceEquals(_suspendedModeToken, token))
        {
            return false;
        }
        _suspendedModeToken = null;
        _modeToken = token;
        return true;
    }

    /// <summary>Forget a suspended identity (the F1a cancel's row).</summary>
    internal void ForgetSuspendedModeToken(object token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (ReferenceEquals(_suspendedModeToken, token))
        {
            _suspendedModeToken = null;
        }
    }

    /// <summary>§E TE-11 (ED-1): the history entrypoint — the same
    /// admission ladder, then the checked-out inverse through the same
    /// transaction seam. The RECORD step is the checkout's commit
    /// (E3's order intact): the receipt crosses to the OPPOSITE stack
    /// and the redo pile survives, which is what the verb path's
    /// clear-redo recording must never do here.</summary>
    internal CanvasMutationAdmission ApplyHistory(
        CanvasMutationOperation operation,
        CanvasHistorySnapshot snapshot,
        bool redo)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(snapshot);
        CanvasMutationAdmission admission = AdmitAndAcquire(operation);
        if (admission != CanvasMutationAdmission.Admitted)
        {
            AnnounceAdmission(admission);
            return admission;
        }
        run(() => TransactHistory(operation, snapshot, redo));
        return CanvasMutationAdmission.Admitted;
    }

    private void TransactHistory(
        CanvasMutationOperation operation,
        CanvasHistorySnapshot snapshot,
        bool redo)
    {
        try
        {
            if (!history.TryCheckOut(snapshot))
            {
                // The stack moved since the snapshot (a raced
                // mutation, a rebase): nothing left its pile, and
                // the blocked arm is mac's own answer.
                AnnounceHistoryBlocked(redo);
                return;
            }
            CanvasApplyResult? result = null;
            VaultException? refusal = null;
            bool ran = operation.Basis.Lease.Invoke(
                () => operation.IsCurrentAgainst(slot.Current),
                handle =>
                {
                    try
                    {
                        result = writes.Apply(handle, snapshot.Entry.Inverse);
                    }
                    catch (VaultException e)
                    {
                        refusal = e;
                    }
                });
            if (!ran || refusal is VaultException.WriteConflict)
            {
                // Displaced, or the disk moved under the entry: the
                // entry returns EXACTLY where it was (IE-9) and stays
                // poppable after the reload rebases or quarantines it
                // — mac's t3 rule through the stricter machinery.
                history.RestoreCheckout();
                AnnounceHistoryBlocked(redo);
                return;
            }
            if (refusal is VaultException.SavedButUnindexed unindexed)
            {
                // A landed write IS a commit (TE-0's boundary); the
                // successor's inverse is unknown on this arm, so the
                // empty action self-quarantines the moment anything
                // moves — the verb path's arm, checkout-shaped.
                history.CommitCheckout(
                    new CanvasHistoryEntry(
                        snapshot.Entry.Name,
                        new CanvasAction(snapshot.Entry.Name, []),
                        unindexed.newContentHash),
                    unindexed.newContentHash);
                MarkUnpresented(operation);
                return;
            }
            if (refusal is not null)
            {
                history.RestoreCheckout();
                return;
            }
            history.CommitCheckout(
                new CanvasHistoryEntry(
                    snapshot.Entry.Name, result!.Inverse, result.NewContentHash),
                result.NewContentHash);
            if (!result.Indexed)
            {
                // The verb path's arm, checkout-shaped (codoki on
                // §E TE-11: a landed-but-unindexed apply must not
                // publish or speak against a stale index). The receipt
                // crossed; recovery is refresh-only.
                MarkUnpresented(operation);
                return;
            }
            if (RefreshAndPublish(operation) == CanvasRefreshOutcome.Installed)
            {
                announce(new CanvasA11yEvent.CanvasHistoryApplied(
                    redo ? CanvasHistoryVerb.Redo : CanvasHistoryVerb.Undo,
                    snapshot.Entry.Name));
            }
        }
        finally
        {
            gate.Release(operation);
        }
    }

    private void AnnounceHistoryBlocked(bool redo) =>
        announce(new CanvasA11yEvent.CanvasBlocked(
            redo
                ? new CanvasBlockedReason.RedoBlocked()
                : new CanvasBlockedReason.UndoBlocked()));

    /// <summary>§E TE-11c (E8a/E2): every refused admission SPEAKS its
    /// typed event — the never-silent table's funnel half. Stale is
    /// silent BY CONTRACT (E2: a displaced operation swallows — no
    /// announcement for a document that moved on), and a Busy repeat
    /// against the same hold stays quiet (the once-per-hold gate).</summary>
    private void AnnounceAdmission(CanvasMutationAdmission admission)
    {
        switch (admission)
        {
            case CanvasMutationAdmission.NotReady:
                announce(new CanvasA11yEvent.CanvasMutationRefused(
                    NotReadyReason(slot.Current)));
                break;
            case CanvasMutationAdmission.RecoveryPending:
                announce(new CanvasA11yEvent.CanvasMutationRefused(
                    CanvasMutationRefusal.RefreshPending));
                break;
            case CanvasMutationAdmission.ConflictPending:
                announce(new CanvasA11yEvent.CanvasSaveConflict());
                break;
            case CanvasMutationAdmission.ModeHeld:
            case CanvasMutationAdmission.Busy:
                announce(new CanvasA11yEvent.CanvasBlocked(
                    new CanvasBlockedReason.ModeBusy()));
                break;
            default:
                break;
        }
    }

    /// <summary>The one NotReady → refusal-reason derivation, shared
    /// with the verbs' guard short-circuits so the guard and the
    /// ladder can never say different things: mac's exact table
    /// (canvasMutationRefusal), publication-shaped.</summary>
    internal static CanvasMutationRefusal NotReadyReason(CanvasPublication now) =>
        now.LoadState switch
        {
            CanvasLoadState.Loading => CanvasMutationRefusal.Opening,
            CanvasLoadState.ParseError => CanvasMutationRefusal.ReadOnly,
            CanvasLoadState.RetargetAbsent => CanvasMutationRefusal.RetargetFailed,
            _ => CanvasMutationRefusal.Unavailable,
        };

    private void Transact(
        CanvasMutationOperation operation,
        Func<ulong, CanvasAction?> prepare,
        string name,
        Func<CanvasA11yEvent>? confirm)
    {
        // §F TF-0: the terminal outcome, delivered to the operation's
        // completion (if any) in the finally — from THIS thread; the
        // consumer marshals itself. Defaults to the refused arm so an
        // early return reports honestly.
        CanvasOperationOutcome outcome = CanvasOperationOutcome.RefusedPrepare;
        try
        {
            CanvasApplyResult? result = null;
            CanvasAction? action = null;
            VaultException? refusal = null;
            bool ran = operation.Basis.Lease.Invoke(
                () => operation.IsCurrentAgainst(slot.Current),
                handle =>
                {
                    // Preparation under the SAME hold as the apply:
                    // nothing can move between the placement's answer
                    // and the write it feeds.
                    action = prepare(handle);
                    if (action is null)
                    {
                        return;
                    }
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
            if (!ran || action is null)
            {
                return; // displaced, or preparation refused: no write.
            }
            if (refusal is VaultException.WriteConflict)
            {
                outcome = CanvasOperationOutcome.Conflict;
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
                outcome = CanvasOperationOutcome.Unindexed;
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
                outcome = CanvasOperationOutcome.Displaced;
                return;
            }

            // E3: the entry is recorded before anything fallible.
            history.PushAndClearRedo(entry, result.NewContentHash);
            if (!result.Indexed)
            {
                outcome = CanvasOperationOutcome.Unindexed;
                MarkUnpresented(operation);
                return;
            }
            outcome =
                RefreshAndPublish(operation) == CanvasRefreshOutcome.Installed
                    ? CanvasOperationOutcome.Installed
                    : CanvasOperationOutcome.RefreshRefused;
            if (outcome == CanvasOperationOutcome.Installed
                && confirm is not null)
            {
                // The verb's confirmation (t0 §1.3), spoken only for
                // a commit whose presentation INSTALLED — a refused
                // or displaced publish has nothing true to confirm.
                announce(confirm());
            }
        }
        finally
        {
            gate.Release(operation);
            operation.Completion?.Invoke(outcome);
        }
    }

    private void MarkUnpresented(CanvasMutationOperation operation) =>
        _ = slot.Publish(s =>
            s.Retired ? null : s.WithCommittedUnpresented(operation.Id));

    private CanvasRefreshOutcome RefreshAndPublish(CanvasMutationOperation operation)
    {
        // Through the WALLED machinery (the model census's catch): the
        // pipeline mints the population and the transfer republishes,
        // which is also where an active filter needle reseeds instead
        // of silently unfiltering — the logic the first cut lost by
        // publishing around the wall.
        CanvasRefreshOutcome outcome = pipeline.RefreshAfterMutation(
            operation.Basis.Lease,
            history.AttachedBasis ?? string.Empty,
            population => CanvasEffectPlan.ResolveSelection(
                operation.Effect,
                population,
                slot.Current.SelectedIntent,
                operation.CreatedId));
        if (outcome == CanvasRefreshOutcome.RequiredTargetMissing)
        {
            MarkUnpresented(operation);
        }
        return outcome;
    }
}

/// <summary>The mutation refresh's typed answer (§E TE-5a).</summary>
internal enum CanvasRefreshOutcome
{
    Installed,
    RequiredTargetMissing,
    Refused,
}
