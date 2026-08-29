// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 PR C-unit, task T2: what a release decided.
/// </summary>
/// <remarks>
/// Result-bearing for the reason every outcome in this design is: a
/// caller that has to re-read the slot to find out whether it still
/// owns a handle has reintroduced the second read obligation I1 is
/// about.
/// </remarks>
internal enum CanvasLeaseRelease
{
    /// <summary>The release published a terminal state for its request
    /// and CLOSED the lease. Nobody else can accept that request
    /// afterwards, because the terminal state is published.</summary>
    Closed,

    /// <summary>The live publication names this lease, so an
    /// acceptance won first and the PUBLICATION owns it now. Nothing
    /// was closed and nothing was published.</summary>
    Transferred,

    /// <summary>This request had already reached a terminal state, so
    /// an earlier release closed the lease. Nothing to do.</summary>
    AlreadyReleased,
}

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
/// The decision never leaves the transform through a captured local.
/// Transforms are pure — that is the rule obligation I4 will make
/// structural — so what the caller learns, it learns from the
/// OUTCOME: an install means this release published the terminal
/// state and therefore owns the close; a decline means it does not.
/// One meaning per branch, which is what lets the close sit outside
/// the transform where an effect belongs.
/// </para>
/// </remarks>
internal static class CanvasLeaseTransfer
{
    /// <summary>
    /// Accept a load: install its lease, population and unit, and mark
    /// its request consumed, all in one publication.
    /// </summary>
    /// <returns>Whether the acceptance landed. False means the request
    /// was superseded, already delivered, released by a concurrent
    /// cleanup, or the document retired — and in every one of those the
    /// caller still owns the lease it opened and must release it.
    /// </returns>
    internal static bool TryAccept(
        CanvasPublicationSlot slot,
        CanvasRequestIdentity request,
        CanvasHandleLease lease,
        CanvasPopulation population)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(population);

        // No caller delegate runs inside the gate here. The projection
        // is computed from the snapshot and the incoming population,
        // both of which this method already has, so acceptance adds no
        // second open callout to the one the transform itself is.
        return slot.Publish(snapshot =>
        {
            if (snapshot.Retired || !snapshot.Loads.Admits(request))
            {
                return null;
            }

            return snapshot
                .WithLoaded(
                    lease,
                    population,
                    CanvasProjectionUnit.Unfiltered(
                        population, population.Resolve(snapshot.SelectedIntent)))
                .WithLoads(snapshot.Loads.ConsumedBy(request));
        }).Installed;
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
    /// path, it is the ordinary outcome of a delivery that worked.
    /// </remarks>
    internal static CanvasLeaseRelease Release(
        CanvasPublicationSlot slot,
        CanvasRequestIdentity request,
        CanvasHandleLease lease)
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

            if (!snapshot.Loads.Admits(request))
            {
                // Acceptance of this request is ALREADY impossible —
                // it was superseded by a newer request, or it has
                // already reached a terminal state. Publishing a
                // terminal state for it now would clobber a newer
                // request's schedule entry with an older request's
                // name, which is a different defect in the same family.
                return null;
            }

            return snapshot.WithLoads(snapshot.Loads.ReleasedBy(request));
        });

        CanvasPublication decidedFrom = outcome.Predecessor;

        if (decidedFrom.Names(lease))
        {
            return CanvasLeaseRelease.Transferred;
        }

        if (outcome.Installed)
        {
            // The terminal state is published, so no acceptance of this
            // request can follow. Only NOW is the close safe — and it
            // is an effect, which is why it is out here rather than in
            // the transform.
            lease.Close();
            return CanvasLeaseRelease.Closed;
        }

        // Declined without the publication naming the lease: acceptance
        // was impossible before this release ran, so the lease is still
        // this caller's to close. Whether an earlier release already
        // did it is read from the SNAPSHOT rather than from the close
        // record — the close record answers one question only, and
        // "who released this" is not it. Close is idempotent, so the
        // repeat is a no-op either way.
        bool alreadyTerminal =
            ReferenceEquals(decidedFrom.Loads.Latest, request)
            && decidedFrom.Loads.Delivery == CanvasLoadDelivery.Released;

        lease.Close();
        return alreadyTerminal
            ? CanvasLeaseRelease.AlreadyReleased
            : CanvasLeaseRelease.Closed;
    }
}
