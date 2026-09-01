// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 PR C-unit, task T3: the FFI surface one load walks, as the
/// pipeline sees it.
/// </summary>
/// <remarks>
/// An interface rather than the session type, so the pipeline names no
/// FFI surface and its battery can drive it with an in-memory source
/// that faults and blocks on demand — the frozen verification plan's
/// fault injection needs a source that throws at a chosen call, and a
/// real session cannot be asked to. The two non-FFI members are the
/// host's PROSE: which failure state an exception maps to, and what a
/// parse error's message is. They are here because the pipeline
/// publishes a failure in the same swap that releases its request, so
/// the words have to be in hand at that moment — but the words are the
/// document's (contract A3), never the pipeline's.
/// </remarks>
internal interface ICanvasLoadSource
{
    CanvasOpenInfo Open();

    void Close(ulong handle);

    CanvasOutlineRow[] Outline(ulong handle);

    CanvasTableRow[] TableRows(ulong handle);

    CanvasScene Scene(ulong handle);

    CanvasLoadFailure FailureFor(Exception exception);

    CanvasLoadFailure ParseError(IReadOnlyList<CanvasLoadWarning> warnings);
}

/// <summary>
/// The points of U4's ownership window a fact can fault or barrier at.
/// Four are INSIDE the acceptance transform, so a throw there leaves
/// the swap unmade; the last is after it.
/// </summary>
internal enum CanvasLoadPoint
{
    /// <summary>After the reads: the population is built and the lease
    /// is still the delivery's own. Where a barrier holds deliveries
    /// before either publishes.</summary>
    Built,

    /// <summary>Inside the transform, immediately after the decision
    /// snapshot is read.</summary>
    SnapshotRead,

    /// <summary>Inside the transform, during the rebase.</summary>
    Rebase,

    /// <summary>Inside the transform, during the filter reseed.</summary>
    Reseed,

    /// <summary>Inside the transform, immediately before the swap.</summary>
    BeforeSwap,

    /// <summary>After the swap installed, before the displaced close.</summary>
    AfterSwap,
}

/// <summary>
/// W6-1 PR C-unit, task T3: the instrument the fault-injection and
/// barrier facts drive — one callback at each point of the window.
/// </summary>
/// <remarks>
/// Named with the suffix this codebase's test-seam guard is built on
/// (task T1's observer went without one, and the record calls that a
/// divergence): a reviewer reading it in a shipping file knows at once
/// that production never constructs one. Not attached in production,
/// and the pipeline behaves identically with and without it. A
/// callback that THROWS is the fault; one that BLOCKS is the barrier.
/// Four of its points run inside the transform — an open callout under
/// the gate, obligation I4's forbidden shape — which is exactly why it
/// is an instrument and not a parameter production could reach for.
/// </remarks>
internal sealed class CanvasLoadProbeForTests
{
    internal Action<CanvasLoadPoint>? OnPoint { get; set; }

    internal void Reached(CanvasLoadPoint point) => OnPoint?.Invoke(point);
}

/// <summary>One load REQUEST as the pipeline hands it to its worker:
/// the identity the schedule now names. The request deliberately does
/// NOT un-name the old lease — the worker's first publication does —
/// because a worker the scheduler refuses to run would leave an
/// un-named lease with no owner at all, while a NAMED one is
/// teardown's to close. The T3 review found exactly that leak in a
/// request-side un-name.</summary>
internal sealed record CanvasLoadRequest(CanvasRequestIdentity Identity);

/// <summary>What one delivery did.</summary>
internal enum CanvasLoadOutcome
{
    /// <summary>Installed: the publication names the lease and the
    /// population, under a Ready load state.</summary>
    Accepted,

    /// <summary>Refused: superseded, already delivered, or the document
    /// retired. This delivery published nothing, and whatever it opened
    /// is closed.</summary>
    Refused,

    /// <summary>The open was degraded: ParseError published, the handle
    /// closed at once.</summary>
    ParseError,

    /// <summary>Something threw before the swap: the mapped failure
    /// published while the request was still latest, and whatever was
    /// opened is closed.</summary>
    Faulted,

    /// <summary>The swap installed and something AFTER it threw — an
    /// effect fault, not the load's. The publication is Ready and names
    /// the lease.</summary>
    FaultedAfterAccept,
}

/// <summary>
/// W6-1 PR C-unit, task T3: THE LOAD PIPELINE — U4's operation, from
/// the request that un-names the old lease to the finally that releases
/// whatever the delivery still owns.
/// </summary>
/// <remarks>
/// <para>
/// The frozen transition, in its own order: publish the terminal state
/// for the old lease and population; close the old handle under its
/// lock; construct the new lease and population off-thread; rebase,
/// carry forward, and install in ONE SWAP. <see cref="Request"/> is the
/// first step and <see cref="Deliver"/> is the rest. Nothing here
/// marshals: the request runs on its caller's thread, the delivery on
/// whatever thread the host gives it, and every publication is the
/// slot's gate. What the host does AFTER a delivery — project the new
/// publication onto its bindable surface — is the host's effect, on
/// the host's thread.
/// </para>
/// <para>
/// UN-NAME FIRST, the choice task T2 left open — and un-named by the
/// WORKER, not the request. The other order — displace the old lease
/// at acceptance — leaks nothing either, since T2's acceptance closes
/// what it displaces; this one keeps one native handle open at a time,
/// as the inherited load did, and the surface keeps its rows as a
/// coherent past because presentation is outside the chain. The
/// worker's own un-name publication hands it the displaced lease from
/// that decision snapshot, so the close needs no second read and a
/// dispatcher never waits under a lease's lock — and a worker the
/// scheduler refuses to run leaves the old lease NAMED, owned by the
/// terminal publication, rather than un-named and unreachable, which
/// is the leak the T3 review found in a request-side un-name.
/// </para>
/// <para>
/// THE RELEASE OBLIGATION is discharged in a finally wrapping the whole
/// delivery, and that finally is the one place a close fault is let go:
/// a session that died first makes the close throw, and that exception
/// must not replace the delivery's own. So the release is guarded, and
/// what it swallows is exactly the pair the inherited teardown
/// swallowed for the same reason.
/// </para>
/// </remarks>
internal sealed class CanvasLoadPipeline
{
    private readonly CanvasPublicationSlot _slot;
    private readonly ICanvasLoadSource _source;
    private readonly Action<CanvasRequestIdentity, string>? _onReseeded;

    /// <summary>W6-1 §E TE-5a: the funnel's refresh, hosted here so
    /// the population mint stays inside its wall and the reseed
    /// callback fires exactly as the acceptance path's does. Reads the
    /// trio under the lease (currency re-checked inside the FFI lock),
    /// mints the successor population, asks the caller for the seat
    /// against it, and republishes through the transfer's wall.</summary>
    internal CanvasRefreshOutcome RefreshAfterMutation(
        CanvasHandleLease lease,
        string basis,
        Func<CanvasPopulation, CanvasEffectResolution> resolveSeat)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(resolveSeat);

        CanvasOutlineRow[]? outline = null;
        CanvasTableRow[]? tableRows = null;
        CanvasScene? scene = null;
        bool read = lease.Invoke(
            () =>
            {
                CanvasPublication now = _slot.Current;
                return !now.Retired && now.Names(lease);
            },
            handle =>
            {
                outline = _source.Outline(handle);
                tableRows = _source.TableRows(handle);
                scene = _source.Scene(handle);
            });
        if (!read)
        {
            return CanvasRefreshOutcome.Refused;
        }
        CanvasPublication current = _slot.Current;
        var population = new CanvasPopulation(
            outline,
            tableRows,
            current.Population?.Warnings,
            current.Population?.LastActivatedNode,
            scene,
            contentHash: basis);
        CanvasEffectResolution seat = resolveSeat(population);
        if (seat.IsRequiredTargetMissing)
        {
            return CanvasRefreshOutcome.RequiredTargetMissing;
        }
        CanvasRepublishOutcome outcome = CanvasLeaseTransfer.Republish(
            _slot, lease, population, seat.SeatValue);
        if (!outcome.Installed)
        {
            return CanvasRefreshOutcome.Refused;
        }
        if (outcome.Reseeded is { } reseeded)
        {
            _onReseeded?.Invoke(reseeded, outcome.Needle!);
        }
        return CanvasRefreshOutcome.Installed;
    }
    private readonly CanvasLoadProbeForTests? _probe;

    /// <param name="onReseeded">Where the reseeded filter request goes —
    /// task T6's machine, which owes it a job. An effect after the won
    /// acceptance swap (U9's load column), handed the request and its
    /// needle from the outcome rather than from a second read.</param>
    internal CanvasLoadPipeline(
        CanvasPublicationSlot slot,
        ICanvasLoadSource source,
        Action<CanvasRequestIdentity, string>? onReseeded = null,
        CanvasLoadProbeForTests? probeForTests = null)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(source);
        _slot = slot;
        _source = source;
        _onReseeded = onReseeded;
        _probe = probeForTests;
    }

    /// <summary>
    /// Step one: publish the request — the schedule's new latest and
    /// the Loading state, the chain deliberately KEPT — in one swap.
    /// </summary>
    /// <returns>What the worker needs, or null when the document is
    /// retired: a retired document loads nothing, ever, and the refusal
    /// publishes nothing.</returns>
    internal CanvasLoadRequest? Request()
    {
        var identity = new CanvasRequestIdentity("load");
        CanvasPublicationOutcome outcome = _slot.Publish(snapshot =>
            snapshot.Retired
                ? null
                : snapshot
                    .WithLoads(snapshot.Loads.Requested(identity))
                    .WithLoadState(CanvasLoadState.Loading, null));
        return outcome.Installed ? new CanvasLoadRequest(identity) : null;
    }

    /// <summary>
    /// The rest: close what the request un-named, open, read, build,
    /// and deliver — accepting or releasing, in a finally, whatever this
    /// delivery still owns.
    /// </summary>
    internal CanvasLoadOutcome Deliver(CanvasLoadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CanvasRequestIdentity identity = request.Identity;

        CanvasHandleLease? lease = null;
        CanvasLoadFailure? failure = null;
        CanvasLoadOutcome outcome = CanvasLoadOutcome.Refused;
        try
        {
            outcome = Attempt();
        }
        finally
        {
            // The release obligation, whatever happened above — and the
            // one place the outcome is corrected: a fault AFTER a swap
            // that installed leaves the lease named, which the release
            // reports as a transfer.
            if (lease is not null)
            {
                CanvasLeaseRelease? released = ReleaseGuarded(identity, lease, failure);
                if (released == CanvasLeaseRelease.Transferred
                    && outcome == CanvasLoadOutcome.Faulted)
                {
                    outcome = CanvasLoadOutcome.FaultedAfterAccept;
                }
            }
            else if (failure is { } unopened)
            {
                _ = CanvasLeaseTransfer.Refuse(_slot, identity, unopened);
            }
        }

        return outcome;

        CanvasLoadOutcome Attempt()
        {
            try
            {
                // The worker's first act, and its own early refusal:
                // UN-NAME the old lease — a publication, declined when
                // the request is already superseded or the document
                // retired, in which case nothing opens. Un-naming here
                // rather than at the request keeps the old lease OWNED
                // at every instant: it stays named until this worker
                // actually runs, so a worker the scheduler never runs
                // leaves it for teardown's terminal publication.
                CanvasPublicationOutcome unnamed = _slot.Publish(snapshot =>
                    snapshot.Retired || !snapshot.Loads.Admits(identity)
                        ? null
                        : snapshot.WithUnloaded());
                if (!unnamed.Installed)
                {
                    return CanvasLoadOutcome.Refused;
                }

                // Then the old handle, under its own lock, so the last
                // in-flight call on it has returned before the new
                // open. INSIDE the try: a close that throws — even
                // panic-class — maps to the failure state instead of
                // faulting the tracked body.
                // The old unit's matched set, carried to the acceptance
                // so a reload under an active filter lingers the rows
                // it was showing instead of widening (T6 review) — from
                // the un-name outcome, the decision snapshot, never a
                // second read.
                string[]? lingering =
                    unnamed.Predecessor.Unit is { } previous
                        && previous.Answer != CanvasAnswerState.Unfiltered
                        ? [.. previous.Matched]
                        : null;

                if (unnamed.Predecessor.Lease is { } displaced)
                {
                    _ = displaced.Close();
                }

                CanvasOpenInfo info = _source.Open();
                lease = new CanvasHandleLease(info.Handle, _source.Close);
                if (info.Degraded)
                {
                    // Read-only by construction (contract A3): nothing
                    // will use the handle, so the finally releases it.
                    failure = _source.ParseError(info.Warnings);
                    return CanvasLoadOutcome.ParseError;
                }

                CanvasOutlineRow[]? outline = null;
                CanvasTableRow[]? tableRows = null;
                CanvasScene? scene = null;
                bool admitted = lease.Invoke(
                    () =>
                    {
                        CanvasPublication now = _slot.Current;
                        return !now.Retired && now.Loads.Admits(identity);
                    },
                    handle =>
                    {
                        outline = _source.Outline(handle);
                        tableRows = _source.TableRows(handle);
                        scene = _source.Scene(handle);
                    });
                if (!admitted)
                {
                    return CanvasLoadOutcome.Refused;
                }

                var population = new CanvasPopulation(
                    outline,
                    tableRows,
                    info.Warnings,
                    lastActivatedNode: null,
                    scene: scene,
                    contentHash: info.ContentHash);
                _probe?.Reached(CanvasLoadPoint.Built);

                CanvasLoadAcceptance acceptance = CanvasLeaseTransfer.TryAccept(
                    _slot, identity, lease, population, lingering, _probe);
                if (acceptance is { Accepted: true, Reseeded: { } reseeded })
                {
                    _onReseeded?.Invoke(reseeded, acceptance.ReseededNeedle!);
                }

                return acceptance.Accepted
                    ? CanvasLoadOutcome.Accepted
                    : CanvasLoadOutcome.Refused;
            }
            catch (Exception exception) when (CanvasFaults.Survivable(exception))
            {
                failure = _source.FailureFor(exception);
                return CanvasLoadOutcome.Faulted;
            }
        }
    }

    /// <summary>The release, with its close fault contained: the
    /// publication half cannot throw, and a close that does means the
    /// session died first — the handle died with it.</summary>
    private CanvasLeaseRelease? ReleaseGuarded(
        CanvasRequestIdentity identity, CanvasHandleLease lease, CanvasLoadFailure? failure)
    {
        try
        {
            return CanvasLeaseTransfer.Release(_slot, identity, lease, failure);
        }
        catch (Exception exception) when (CanvasFaults.Survivable(exception))
        {
            // The close's fault, not the delivery's: a session that
            // died first, or a panic-class exception out of the FFI —
            // either way the attempt is on the record, and the tracked
            // body must not fault over it.
            return null;
        }
    }
}
