// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 PR C-unit, task T1: the identity of one request, carried by
/// reference and nothing else.
/// </summary>
/// <remarks>
/// A counter here would carry the ABA shape this branch retired in
/// C-lite round 1, and the handle a stamp would name is a reused
/// integer. So a request is identified by BEING itself: a fresh
/// allocation per request, compared with reference equality, with the
/// label carried only so a failure message can say which request it
/// was talking about. Two requests with the same label are two
/// requests.
/// </remarks>
internal sealed class CanvasRequestIdentity(string label)
{
    /// <summary>Diagnostic only. Never compared — see the remarks.</summary>
    internal string Label { get; } = label;

    public override string ToString() => $"request({Label})";
}

/// <summary>
/// W6-1 PR C-unit, task T2: how far one load request's delivery got.
/// </summary>
/// <remarks>
/// Task T1 shipped this as a bool, which could say "accepted" but not
/// "released". Obligation I1 needs the third state: a delivery that
/// refuses or faults publishes a TERMINAL state for its own request
/// BEFORE it closes anything, and that published state is what makes a
/// concurrent acceptance of the same request impossible rather than
/// merely unlikely. A bool had nowhere to put it.
/// </remarks>
internal enum CanvasLoadDelivery
{
    /// <summary>Requested and not yet delivered. The only state an
    /// acceptance may proceed from.</summary>
    Pending,

    /// <summary>Accepted: the publication took ownership of the lease
    /// this request opened.</summary>
    Consumed,

    /// <summary>Released: refused or faulted, its lease closed or about
    /// to be, and no acceptance of this request can follow.</summary>
    Released,
}

/// <summary>
/// W6-1 PR C-unit, task T1: the load schedule - the latest load
/// request and how far its delivery got.
/// </summary>
/// <remarks>
/// The delivery state is what makes the publication itself the
/// one-shot latch, so that no second mutable field is needed to stop
/// a repeat delivery. Task T3 lands the pipeline that drives it;
/// T1 landed the shape and T2 gave it the terminal state obligation
/// I1's mechanism publishes.
/// </remarks>
internal sealed class CanvasLoadSchedule
{
    private CanvasLoadSchedule(
        CanvasRequestIdentity? latest, CanvasLoadDelivery delivery)
    {
        Latest = latest;
        Delivery = delivery;
    }

    /// <summary>No load has ever been requested.</summary>
    internal static CanvasLoadSchedule Idle { get; } =
        new(null, CanvasLoadDelivery.Pending);

    internal CanvasRequestIdentity? Latest { get; }

    internal CanvasLoadDelivery Delivery { get; }

    /// <summary>Whether <see cref="Latest"/> has already been accepted.
    /// A delivery that reads this as true refuses - that REFUSAL is
    /// task T3's behaviour and T3's fact; what T1 owns and asserts is
    /// the value arithmetic underneath it.</summary>
    internal bool Consumed => Delivery == CanvasLoadDelivery.Consumed;

    /// <summary>Whether <paramref name="request"/> can still be
    /// accepted here: it must be the latest, and its delivery must not
    /// have reached either terminal state.</summary>
    internal bool Admits(CanvasRequestIdentity request) =>
        ReferenceEquals(Latest, request) && Delivery == CanvasLoadDelivery.Pending;

    internal CanvasLoadSchedule Requested(CanvasRequestIdentity request) =>
        new(request, CanvasLoadDelivery.Pending);

    internal CanvasLoadSchedule ConsumedBy(CanvasRequestIdentity request) =>
        new(request, CanvasLoadDelivery.Consumed);

    /// <summary>The terminal refusal obligation I1's cleanup publishes
    /// before it closes anything.</summary>
    internal CanvasLoadSchedule ReleasedBy(CanvasRequestIdentity request) =>
        new(request, CanvasLoadDelivery.Released);
}

/// <summary>
/// W6-1 PR C-unit, task T1: the filter schedule — the RUNNING request
/// and the QUEUED-LATEST request, together, because the design's
/// transition table is keyed by the pair and a schedule that could
/// only answer one of them was what left the third keystroke
/// undecided.
/// </summary>
/// <remarks>
/// T1 lands the value and its transitions as pure functions. The
/// machine that drives them, and the job that runs off-thread, are
/// task T6's.
/// </remarks>
internal sealed class CanvasFilterSchedule
{
    private CanvasFilterSchedule(
        CanvasRequestIdentity? running, CanvasRequestIdentity? queued)
    {
        Running = running;
        Queued = queued;
    }

    internal static CanvasFilterSchedule Idle { get; } = new(null, null);

    internal CanvasRequestIdentity? Running { get; }

    internal CanvasRequestIdentity? Queued { get; }

    /// <summary>A keystroke: start it when nothing runs, otherwise
    /// replace whatever was queued, which is dropped before it ever
    /// started.</summary>
    internal CanvasFilterSchedule Typed(CanvasRequestIdentity request) =>
        Running is null ? new(request, null) : new(Running, request);

    /// <summary>The running job finished. With a queue this DISCARDS
    /// the finishing answer and promotes; without one it goes
    /// idle.</summary>
    internal CanvasFilterSchedule Finished() =>
        Queued is null ? Idle : new(Queued, null);

    /// <summary>A reload retires both entries; the new machine is
    /// seeded from the rebased needle, never from the dead one.</summary>
    internal CanvasFilterSchedule Reseeded(CanvasRequestIdentity? request) =>
        request is null ? Idle : new(request, null);
}

/// <summary>
/// W6-1 PR C-unit, task T2: the three finer classes of the nesting
/// chain — LEASE, POPULATION and UNIT — as one value, present together
/// or absent together.
/// </summary>
/// <remarks>
/// <para>
/// The chain says a unit is current only while its population is, and
/// a population only while its lease is. Three nullable fields on the
/// publication could spell every combination the chain forbids — a
/// unit beside no population, a lease beside no unit — and the T2
/// review found the invariant being carried by a doc comment on one
/// transform. One nullable value holding all three makes those
/// combinations unspellable: the only way to have a unit is to have the
/// population it projects and the lease that population was loaded
/// through.
/// </para>
/// <para>
/// What this does NOT close is a unit projected from a DIFFERENT
/// population than the one beside it. The unit carries no population
/// reference — the record says why — so <see cref="WithUnit"/> cannot
/// check it structurally. Task T6's filter pipeline projects from the
/// population it reads out of the same snapshot it publishes against,
/// which is the discipline; a census that keeps it honest is task T4's.
/// </para>
/// </remarks>
internal sealed class CanvasLoaded
{
    internal CanvasLoaded(
        CanvasHandleLease lease, CanvasPopulation population, CanvasProjectionUnit unit)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(unit);
        Lease = lease;
        Population = population;
        Unit = unit;
    }

    internal CanvasHandleLease Lease { get; }

    internal CanvasPopulation Population { get; }

    internal CanvasProjectionUnit Unit { get; }

    /// <summary>The same lease and population under a successor unit -
    /// a filter request publishing, in the chain's terms.</summary>
    internal CanvasLoaded WithUnit(CanvasProjectionUnit unit) => new(Lease, Population, unit);
}

/// <summary>
/// W6-1 PR C-unit, task T1: THE PUBLICATION — the immutable value the
/// one slot holds, and the whole of the model's currency.
/// </summary>
/// <remarks>
/// <para>
/// This is a sealed CLASS and not a record, deliberately. Currency in
/// this design is derived by REFERENCE comparison against what the
/// slot holds, and the install is a compare-and-swap, which is
/// reference identity whatever the type says. A record would generate
/// value equality alongside that, so two publications carrying equal
/// fields would compare equal with one operator and differ with the
/// other — and every currency question in the model would then depend
/// on which one a caller reached for. Reference equality is the only
/// equality this type has.
/// </para>
/// <para>
/// The constructor is private and every successor comes from a
/// <c>With</c> method. Each of those ALLOCATES, including when the
/// value it sets is the value already there: a "return this when
/// unchanged" optimisation is precisely the identity-return path
/// codex round 7 named in obligation I5, and the slot refuses it at
/// the install as well. Freshness is not a performance question here;
/// it is what keeps a stale compare-and-swap from succeeding against
/// a value that has come round again.
/// </para>
/// <para>
/// T1 carried the DOCUMENT-class state and the two schedules; T2 adds
/// the three finer classes of the nesting chain as ONE nullable value,
/// <see cref="CanvasLoaded"/>, so that a unit beside no population or
/// a lease beside no unit is unspellable rather than forbidden by a
/// comment. It is null before the first load lands, which is a real
/// state rather than a stub: a document with no lease has not opened a
/// handle, and every boundary that would use one refuses on the
/// document's own currency long before it looks.
/// </para>
/// </remarks>
internal sealed class CanvasPublication
{
    private CanvasPublication(
        bool retired,
        CanvasLoadState loadState,
        string? loadMessage,
        CanvasSurfaceKind activeSurface,
        string? selectedIntent,
        ImmutableHashSet<string> markedIntent,
        string needleIntent,
        CanvasLoadSchedule loads,
        CanvasFilterSchedule filters,
        CanvasLoaded? loaded,
        CanvasOperationId? committedUnpresented)
    {
        Retired = retired;
        LoadState = loadState;
        LoadMessage = loadMessage;
        ActiveSurface = activeSurface;
        SelectedIntent = selectedIntent;
        MarkedIntent = markedIntent;
        NeedleIntent = needleIntent;
        Loads = loads;
        Filters = filters;
        Loaded = loaded;
        CommittedUnpresented = committedUnpresented;
    }

    /// <summary>The first publication for a document: nothing loaded,
    /// nothing intended, both schedules idle.</summary>
    internal static CanvasPublication Seed() => new(
        retired: false,
        loadState: CanvasLoadState.Loading,
        loadMessage: null,
        activeSurface: CanvasSurfaceKind.Outline,
        selectedIntent: null,
        markedIntent: CanvasModelCopy.Ids(null),
        needleIntent: string.Empty,
        loads: CanvasLoadSchedule.Idle,
        filters: CanvasFilterSchedule.Idle,
        loaded: null,
        committedUnpresented: null);

    /// <summary>DOCUMENT class, and the coarsest currency: every model
    /// boundary validates this first.</summary>
    internal bool Retired { get; }

    internal CanvasLoadState LoadState { get; }

    internal string? LoadMessage { get; }

    /// <summary>DOCUMENT class. Absorbed from the selection object, and
    /// carried unchanged across a load rather than rebased — it does
    /// not depend on the population, so there is nothing to
    /// resolve.</summary>
    internal CanvasSurfaceKind ActiveSurface { get; }

    /// <summary>The durable INTENT, not the resolved selection. The
    /// resolution against a population arrives with the population, in
    /// task T2.</summary>
    internal string? SelectedIntent { get; }

    internal ImmutableHashSet<string> MarkedIntent { get; }

    /// <summary>The typed needle as intent — DOCUMENT class, which is
    /// why it survives a reload and reseeds the filter machine.</summary>
    internal string NeedleIntent { get; }

    internal CanvasLoadSchedule Loads { get; }

    internal CanvasFilterSchedule Filters { get; }

    /// <summary>The finer classes, together. Null until a load lands.
    /// Naming a lease here is what makes it live - the lease itself
    /// carries no liveness flag and cannot answer the question.</summary>
    internal CanvasLoaded? Loaded { get; }

    /// <summary>W6-1 §E TE-2 (IE-10): the operation whose COMMIT is on
    /// disk while its refresh never presented — a real document state,
    /// not an error in flight. While non-null, admission refuses every
    /// write except the refresh-only recovery that clears it: a second
    /// write would prepare against invisible core state, and an undo
    /// would reverse a change the user never saw. The funnel (TE-5)
    /// enforces; this value only makes the state spellable.</summary>
    internal CanvasOperationId? CommittedUnpresented { get; }

    /// <summary>LEASE class, read through <see cref="Loaded"/>.</summary>
    internal CanvasHandleLease? Lease => Loaded?.Lease;

    /// <summary>POPULATION class. Replaced whole by a reload, together
    /// with the lease, in one swap.</summary>
    internal CanvasPopulation? Population => Loaded?.Population;

    /// <summary>UNIT class, the finest in the chain.</summary>
    internal CanvasProjectionUnit? Unit => Loaded?.Unit;

    /// <summary>Whether this publication names <paramref name="lease"/>
    /// - the one question "is this lease live" reduces to, asked by
    /// reference because a lease IS its reference.</summary>
    internal bool Names(CanvasHandleLease lease) => ReferenceEquals(Lease, lease);

    internal CanvasPublication WithRetired() => Copy(retired: true);

    internal CanvasPublication WithLoadState(CanvasLoadState state, string? message) =>
        Copy(loadState: state, loadMessage: message, loadMessageSet: true);

    internal CanvasPublication WithActiveSurface(CanvasSurfaceKind surface) =>
        Copy(activeSurface: surface);

    internal CanvasPublication WithSelectedIntent(string? nodeId) =>
        Copy(selectedIntent: nodeId, selectedIntentSet: true);

    internal CanvasPublication WithMarkedIntent(IEnumerable<string>? nodeIds) =>
        Copy(markedIntent: CanvasModelCopy.Ids(nodeIds));

    /// <summary>The already-copied form, for a caller that built the
    /// set OUTSIDE the gate — the copy is O(marks) and a transform is
    /// every publisher's cost (the cleanup pass).</summary>
    internal CanvasPublication WithMarkedIntent(ImmutableHashSet<string> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        return Copy(markedIntent: nodeIds);
    }

    /// <remarks>Guarded, because <c>Copy</c> reads null as "not
    /// supplied": without this, a null needle would silently produce an
    /// unchanged-valued copy rather than throwing, which is the second
    /// spelling of one value this design refuses everywhere
    /// else. Same for the two schedules below.</remarks>
    internal CanvasPublication WithNeedleIntent(string needle)
    {
        ArgumentNullException.ThrowIfNull(needle);
        return Copy(needleIntent: needle);
    }

    internal CanvasPublication WithLoads(CanvasLoadSchedule loads)
    {
        ArgumentNullException.ThrowIfNull(loads);
        return Copy(loads: loads);
    }

    internal CanvasPublication WithFilters(CanvasFilterSchedule filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        return Copy(filters: filters);
    }

    /// <summary>Install a lease, the population it loaded and the
    /// unit projected from it, together. One transform and one value,
    /// because a publication naming a new lease beside an old
    /// population is a state the chain forbids and nothing can
    /// spell.</summary>
    internal CanvasPublication WithLoaded(
        CanvasHandleLease lease,
        CanvasPopulation population,
        CanvasProjectionUnit unit) =>
        Copy(loaded: new CanvasLoaded(lease, population, unit), loadedSet: true);

    /// <summary>The reload WORKER's first act: lease, population and
    /// unit un-named together, the document NOT retired. Task T3's
    /// un-name-first order — the frozen transition's "publish the
    /// terminal state for the old lease and population" — so that one
    /// native handle is open at a time and the old lease's close, which
    /// follows under its own lock, closes nothing a publication names.
    /// Presentation is outside the chain, so a surface keeps the rows it
    /// has as a coherent past until the new ones land.</summary>
    internal CanvasPublication WithUnloaded() => Copy(loaded: null, loadedSet: true);

    /// <summary>A successor unit over the SAME lease and population.
    /// Refused outright on a publication with no population to project
    /// - a unit without one is the state the chain forbids, and a
    /// transform that could spell it would be carrying the invariant
    /// in a comment.</summary>
    internal CanvasPublication WithUnit(CanvasProjectionUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (Loaded is null)
        {
            throw new InvalidOperationException(
                "a unit was installed on a publication with no population to "
                + "project: the nesting chain has no such state, so the publisher "
                + "decided from a snapshot it did not read");
        }

        return Copy(loaded: Loaded.WithUnit(unit), loadedSet: true);
    }

    /// <summary>A commit landed but its refresh failed (IE-10): the
    /// state that blocks further writes until recovery.</summary>
    internal CanvasPublication WithCommittedUnpresented(CanvasOperationId operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return Copy(committedUnpresented: operation, committedUnpresentedSet: true);
    }

    /// <summary>The refresh-only recovery presented the commit.</summary>
    internal CanvasPublication WithPresented() =>
        Copy(committedUnpresented: null, committedUnpresentedSet: true);

    /// <summary>The terminal publication: every model currency cleared
    /// at once. Physical close follows, under the lease's own lock,
    /// after the last in-flight call returns - it is not part of
    /// this.</summary>
    internal CanvasPublication WithTerminal() => Copy(
        retired: true,
        loaded: null, loadedSet: true);

    /// <summary>
    /// The one copy path, so there is one place where freshness lives.
    /// </summary>
    /// <remarks>
    /// The two <c>*Set</c> flags exist because their fields are
    /// nullable and null is a MEANING for them — no message, no
    /// selection — so "the caller did not pass one" cannot be spelled
    /// as null without erasing the ability to clear them.
    /// </remarks>
    private CanvasPublication Copy(
        bool? retired = null,
        CanvasLoadState? loadState = null,
        string? loadMessage = null,
        bool loadMessageSet = false,
        CanvasSurfaceKind? activeSurface = null,
        string? selectedIntent = null,
        bool selectedIntentSet = false,
        ImmutableHashSet<string>? markedIntent = null,
        string? needleIntent = null,
        CanvasLoadSchedule? loads = null,
        CanvasFilterSchedule? filters = null,
        CanvasLoaded? loaded = null,
        bool loadedSet = false,
        CanvasOperationId? committedUnpresented = null,
        bool committedUnpresentedSet = false) => new(
            retired ?? Retired,
            loadState ?? LoadState,
            loadMessageSet ? loadMessage : LoadMessage,
            activeSurface ?? ActiveSurface,
            selectedIntentSet ? selectedIntent : SelectedIntent,
            markedIntent ?? MarkedIntent,
            needleIntent ?? NeedleIntent,
            loads ?? Loads,
            filters ?? Filters,
            loadedSet ? loaded : Loaded,
            committedUnpresentedSet ? committedUnpresented : CommittedUnpresented);
}
