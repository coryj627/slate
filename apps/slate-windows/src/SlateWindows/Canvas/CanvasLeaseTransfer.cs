// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 PR C-unit, task T2: what an acceptance did.
/// </summary>
/// <remarks>
/// Result-bearing for the reason every outcome in this design is: the
/// caller learns from the outcome, never from a second read of the
/// slot. An accepting delivery needs two things it cannot otherwise
/// know without that read — whether it landed, and which filter
/// request the acceptance seeded for the carried needle, because that
/// is the job task T6's machine now has to start.
/// </remarks>
/// <param name="Accepted">Whether the acceptance landed. False means
/// the request was superseded, already delivered, released by a
/// concurrent cleanup, or the document retired — and in every one of
/// those the caller still owns the lease it opened and must release
/// it.</param>
/// <param name="Reseeded">The filter request seeded from the carried
/// needle, or null when the needle was empty or the acceptance did not
/// land.</param>
/// <param name="ReseededNeedle">The carried needle the reseed was
/// minted under — what task T6's machine matches for. Null exactly when
/// <paramref name="Reseeded"/> is.</param>
internal readonly record struct CanvasLoadAcceptance(
    bool Accepted,
    CanvasRequestIdentity? Reseeded,
    string? ReseededNeedle);

/// <summary>
/// W6-1 PR C-unit, task T2: what a release did.
/// </summary>
/// <remarks>
/// Result-bearing for the same reason: a caller that has to re-read the
/// slot to find out whether it still owns a handle has reintroduced
/// the second read obligation I1 is about. And it reports what the
/// release DID, not what it decided — the T2 review showed a decision
/// re-derived from the snapshot could not tell "already released" from
/// "superseded since".
/// </remarks>
internal enum CanvasLeaseRelease
{
    /// <summary>This release closed the lease. If the request was still
    /// latest and pending, a terminal state was published for it FIRST,
    /// so no acceptance can follow; if it was already superseded or the
    /// document retired, acceptance was impossible before this release
    /// read anything, and there was nothing to publish.</summary>
    Closed,

    /// <summary>The live publication names this lease, so an
    /// acceptance won and the PUBLICATION owns it now. Nothing was
    /// closed and nothing was published.</summary>
    Transferred,

    /// <summary>The lease was already closed — by an earlier release,
    /// or by the reload that displaced it. Nothing was done and nothing
    /// was published.</summary>
    AlreadyReleased,
}

/// <summary>
/// W6-1 PR C-unit, task T3: the load state a refusing or faulting
/// delivery publishes WITH its terminal state — one swap, so a document
/// is never Failed under a request that could still be accepted, nor
/// released under a state that still says Loading.
/// </summary>
internal readonly record struct CanvasLoadFailure(CanvasLoadState State, string? Message);

/// <summary>
/// W6-1 PR C-unit, task T2: OBLIGATION I1's mechanism — the ownership
/// transfer of a lease, made atomic by being a publication rather than
/// an observation.
/// </summary>
/// <remarks>
/// <para>
/// Codex round 7's blocker 1: <i>"close unless a final slot read names
/// my lease" is an observation, not an atomic ownership transfer. It
/// was safe under serialized delivery, but not with free-threaded
/// concurrent deliveries.</i> The arrangement it names: deliveries A
/// and B share a result and its lease L; A faults and enters its
/// cleanup; A's final read sees no publication naming L; B then wins
/// the acceptance and publishes L; A closes L on the strength of its
/// earlier read; and the live publication now names a closed handle.
/// The close-once record does not help, because it is the FIRST close
/// that is wrong.
/// </para>
/// <para>
/// The remedy codex offered first — <i>"refusal or fault must first
/// make acceptance impossible through an atomic publication
/// transition"</i> — is what this type does, and task T1's gate is
/// what makes it cheap. Both operations below are TRANSFORMS. They
/// decide and they publish inside the same critical section, so the
/// window the arrangement needs does not exist:
/// </para>
/// <para>
/// If the release runs first, it publishes the request's terminal
/// state, and the acceptance that follows reads that state and
/// declines — so B never installs L and A's close is safe. If the
/// acceptance runs first, it publishes the lease, and the release that
/// follows reads a publication that NAMES its lease and declines to
/// close. There is no third interleaving, because there is one field
/// and one gate, and both decisions read it inside that gate. The
/// arrangement is unrepresentable rather than unlikely.
/// </para>
/// <para>
/// The decision never leaves a transform through a captured local.
/// Transforms are pure — that is the rule obligation I4 will make
/// structural — so what the caller learns, it learns from the OUTCOME
/// and from the effect it then performs: a predecessor naming the lease
/// means the publication owns it; anything else means the close is
/// safe, either because the terminal state is now published or because
/// acceptance was impossible before the release read the slot. The
/// close sits outside the transform where an effect belongs, and its
/// own answer — whether this was the call that closed — is what the
/// release reports.
/// </para>
/// </remarks>
internal static class CanvasLeaseTransfer
{
    /// <summary>
    /// Accept a load: install its lease, population and unit, mark its
    /// request consumed, rebase the carried intents and reseed the
    /// filter machine — all in one publication — then close whatever
    /// lease the swap displaced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE REBASE runs inside the gate, from the decision snapshot: the
    /// selected intent resolves against the new graph, and the needle
    /// intent reseeds the filter machine. A reload retires BOTH
    /// schedule entries and seeds the new machine from the carried
    /// needle, never from the dead one — the frozen "a reload cannot
    /// strand the filter machine" row, which the T2 review found this
    /// method leaving to a comment. The request the reseed mints is
    /// what task T6 starts a job for; it is minted inside the transform
    /// because the transform runs exactly once, and read back from the
    /// successor rather than smuggled out through a captured local.
    /// </para>
    /// <para>
    /// THE DISPLACED LEASE. A reload's swap un-names the lease the
    /// predecessor held, and after that nothing can reach it: no
    /// publication names it, no release owns it. The swap's effect step
    /// therefore owns its close, from the decision snapshot the outcome
    /// carries rather than from a second read. Close takes the old
    /// lease's FFI lock, so it waits for the last in-flight call on the
    /// old handle; a call arriving after that reads the moved slot and
    /// refuses. The frozen transition text also sanctions un-naming the
    /// old lease BEFORE the new load is built — task T3's choice — in
    /// which case there is nothing here to displace.
    /// </para>
    /// </remarks>
    internal static CanvasLoadAcceptance TryAccept(
        CanvasPublicationSlot slot,
        CanvasRequestIdentity request,
        CanvasHandleLease lease,
        CanvasPopulation population,
        IReadOnlyCollection<string>? lingeringMatched = null,
        CanvasLoadProbeForTests? probeForTests = null)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(population);

        // Built OUTSIDE the gate: the projection walks every row, and
        // under the gate that walk is every publisher's cost rather
        // than this one's — I2's ledger note. What stays inside is the
        // rebase, which is a dictionary probe and a few allocations.
        CanvasProjectionUnit unfiltered = CanvasProjectionUnit.Unfiltered(population);

        // The lingering base too (the cleanup pass): Answered walks the
        // outline, and both of its inputs are known before the gate —
        // the previous answer, carried across the reload and resolved
        // against the new graph so the pending unit lingers instead of
        // widening (the T6 review's flash).
        CanvasProjectionUnit lingeringBase = lingeringMatched is null
            ? unfiltered
            : unfiltered.Answered(population, lingeringMatched);

        CanvasPublicationOutcome outcome = slot.Publish(snapshot =>
        {
            probeForTests?.Reached(CanvasLoadPoint.SnapshotRead);
            if (snapshot.Retired || !snapshot.Loads.Admits(request))
            {
                return null;
            }

            string? resolved = population.Resolve(snapshot.SelectedIntent);
            probeForTests?.Reached(CanvasLoadPoint.Rebase);
            CanvasRequestIdentity? reseed = null;
            CanvasProjectionUnit unit;
            if (CanvasFilterMachine.IsActiveNeedle(snapshot.NeedleIntent))
            {
                // The ACTIVE predicate, the same one the keystroke
                // reads — a whitespace needle must not be inactive at
                // the keystroke and active at the reseed, minting a
                // phantom job on every reload (T6 review).
                reseed = new CanvasRequestIdentity($"reseed of {request.Label}");
                if (lingeringMatched is not null
                    && resolved is not null
                    && !lingeringBase.Matched.Contains(resolved))
                {
                    // The lingering answer filters the selection the
                    // way a landed one would have.
                    resolved = null;
                }

                unit = lingeringBase
                    .WithResolvedSelection(resolved)
                    .Pending(reseed, snapshot.NeedleIntent);
            }
            else
            {
                unit = unfiltered.WithResolvedSelection(resolved);
            }

            probeForTests?.Reached(CanvasLoadPoint.Reseed);
            CanvasPublication successor = snapshot
                .WithLoaded(lease, population, unit)
                .WithLoads(snapshot.Loads.ConsumedBy(request))
                .WithFilters(snapshot.Filters.Reseeded(reseed))
                // U4: "the load state the acceptance itself writes".
                .WithLoadState(CanvasLoadState.Ready, null);
            probeForTests?.Reached(CanvasLoadPoint.BeforeSwap);
            return successor;
        });

        if (!outcome.Installed)
        {
            return new CanvasLoadAcceptance(false, null, null);
        }

        probeForTests?.Reached(CanvasLoadPoint.AfterSwap);

        CanvasHandleLease? displaced = outcome.Predecessor.Lease;
        if (displaced is not null && !ReferenceEquals(displaced, lease))
        {
            _ = displaced.Close();
        }

        return new CanvasLoadAcceptance(
            true,
            outcome.Successor.Filters.Running,
            outcome.Successor.Filters.Running is null
                ? null
                : outcome.Successor.Unit!.Needle);
    }

    /// <summary>W6-1 §E TE-5a (the wall censuses' catch): the funnel's
    /// post-mutation republish, INSIDE the `WithLoaded` wall. Admits on
    /// the LEASE — a unit swapped by a filter answer mid-apply still
    /// takes the refresh, which replaces population and unit wholesale
    /// — and reuses the acceptance's reseed rule: an active needle
    /// leaves the unit pending a fresh filter request rather than
    /// silently unfiltering, which is exactly the logic the funnel's
    /// first cut lost by publishing around this wall.</summary>
    internal static CanvasRepublishOutcome Republish(
        CanvasPublicationSlot slot,
        CanvasHandleLease lease,
        CanvasPopulation population,
        string? seat,
        Func<CanvasPublication, System.Collections.Immutable.ImmutableHashSet<string>>? marks = null)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(population);

        CanvasRequestIdentity? reseed = null;
        string? needle = null;
        CanvasPublicationOutcome outcome = slot.Publish(snapshot =>
        {
            if (snapshot.Retired || !snapshot.Names(lease))
            {
                return null;
            }
            reseed = null;
            needle = null;
            CanvasProjectionUnit unit = CanvasProjectionUnit
                .Unfiltered(population)
                .WithResolvedSelection(seat);
            if (CanvasFilterMachine.IsActiveNeedle(snapshot.NeedleIntent))
            {
                reseed = new CanvasRequestIdentity("reseed of mutation refresh");
                needle = snapshot.NeedleIntent;
                unit = unit.Pending(reseed, snapshot.NeedleIntent);
            }
            CanvasPublication next = snapshot
                .WithLoaded(lease, population, unit)
                .WithFilters(snapshot.Filters.Reseeded(reseed))
                .WithSelectedIntent(seat);
            // §G TG-3 (GD-7): the mark effect lands in the SAME
            // publication as the refreshed rows — one install, never a
            // second write that a later local mark could race.
            return marks is null ? next : next.WithMarkedIntent(marks(snapshot));
        });
        return new CanvasRepublishOutcome(outcome.Installed, reseed, needle);
    }

    /// <summary>
    /// Release a lease this caller opened and no publication took —
    /// the refusal and the fault path, which are the same path.
    /// </summary>
    /// <remarks>
    /// Called from a finally, so it runs whether the delivery refused
    /// on its own terms or threw somewhere between opening the handle
    /// and installing it. That is why it must be safe to call when the
    /// acceptance actually succeeded: the <see
    /// cref="CanvasLeaseRelease.Transferred"/> branch is not an error
    /// path, it is the ordinary outcome of a delivery that worked. And
    /// it is why a close that throws is the caller's to sequence — see
    /// <see cref="CanvasHandleLease"/> — so that a faulting close in a
    /// finally does not replace the fault it was cleaning up after.
    /// </remarks>
    internal static CanvasLeaseRelease Release(
        CanvasPublicationSlot slot,
        CanvasRequestIdentity request,
        CanvasHandleLease lease,
        CanvasLoadFailure? failure = null)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(lease);

        CanvasPublicationOutcome outcome = slot.Publish(snapshot =>
        {
            if (snapshot.Names(lease))
            {
                // An acceptance won. Publishing anything here would be
                // this release deciding something about a lease it no
                // longer owns.
                return null;
            }

            return Terminal(snapshot, request, failure);
        });

        if (outcome.Predecessor.Names(lease))
        {
            return CanvasLeaseRelease.Transferred;
        }

        // Either the terminal state is now published, or acceptance was
        // impossible before this release read the slot. Both make the
        // close safe, and it is an effect, which is why it sits out here
        // rather than in the transform. What this call reports is what
        // it DID, answered by the close-once record's one question.
        return lease.Close()
            ? CanvasLeaseRelease.Closed
            : CanvasLeaseRelease.AlreadyReleased;
    }

    /// <summary>
    /// Task T3: refuse a request that never held a handle — the open
    /// itself threw — publishing its failure with its terminal state
    /// while it is still the latest, and nothing once a newer request
    /// owns the schedule.
    /// </summary>
    internal static bool Refuse(
        CanvasPublicationSlot slot, CanvasRequestIdentity request, CanvasLoadFailure failure)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(request);
        return slot.Publish(snapshot => Terminal(snapshot, request, failure)).Installed;
    }

    /// <summary>
    /// Task T3, U12 step 4: the terminal publication, with the lease it
    /// un-named handed back for the caller to close — on the caller's
    /// thread of choice, because production closes off the dispatcher
    /// and a test closes inline. The close follows the publication, so
    /// no live publication ever names a closed handle.
    /// </summary>
    internal static CanvasHandleLease? Terminalize(CanvasPublicationSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return slot.Publish(snapshot => snapshot.WithTerminal()).Predecessor.Lease;
    }

    /// <summary>The terminal transition for a request, or a decline
    /// when acceptance is already impossible — shared by the release
    /// and the lease-less refusal.</summary>
    private static CanvasPublication? Terminal(
        CanvasPublication snapshot, CanvasRequestIdentity request, CanvasLoadFailure? failure)
    {
        if (snapshot.Retired || !snapshot.Loads.Admits(request))
        {
            // Acceptance of this request is ALREADY impossible — the
            // document retired, the request superseded by a newer one,
            // or a terminal state already reached — so a publication
            // would add nothing, and would be wrong in two different
            // ways: after the terminal publication it would install a
            // later record over the one every holder treats as final,
            // and under a newer request it would overwrite that
            // request's schedule entry — and now its load state — with
            // an older request's.
            return null;
        }

        // The terminal state also retires BOTH filter entries (T6
        // review): a failed load has no population to match against,
        // and a running request left behind is U9's stranded machine —
        // every later keystroke queueing behind a request whose job can
        // never complete. The needle INTENT survives, so the next
        // successful load reseeds from it.
        CanvasPublication released = snapshot
            .WithLoads(snapshot.Loads.ReleasedBy(request))
            .WithFilters(snapshot.Filters.Reseeded(null));
        return failure is { } state
            ? released.WithLoadState(state.State, state.Message)
            : released;
    }
}

/// <summary>The republish's answer: whether it installed, and the
/// reseeded filter request the caller must start (the pipeline fires
/// its onReseeded exactly as the acceptance path does).</summary>
internal readonly record struct CanvasRepublishOutcome(
    bool Installed,
    CanvasRequestIdentity? Reseeded,
    string? Needle);
