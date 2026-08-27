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
/// W6-1 PR C-unit, task T1: the load schedule — the latest load
/// request and whether its delivery has been consumed.
/// </summary>
/// <remarks>
/// The delivery state is what makes the publication itself the
/// one-shot latch, so that no second mutable field is needed to stop
/// a repeat delivery. Task T3 lands the pipeline that drives it;
/// T1 lands the shape so the publication record and its transform
/// algebra are whole.
/// </remarks>
internal sealed class CanvasLoadSchedule
{
    private CanvasLoadSchedule(CanvasRequestIdentity? latest, bool consumed)
    {
        Latest = latest;
        Consumed = consumed;
    }

    /// <summary>No load has ever been requested.</summary>
    internal static CanvasLoadSchedule Idle { get; } = new(null, consumed: false);

    internal CanvasRequestIdentity? Latest { get; }

    /// <summary>Whether <see cref="Latest"/> has already been accepted.
    /// A delivery that reads this as true refuses — that REFUSAL is
    /// task T3's behaviour and T3's fact; what T1 owns and asserts is
    /// the value arithmetic underneath it.</summary>
    internal bool Consumed { get; }

    internal CanvasLoadSchedule Requested(CanvasRequestIdentity request) =>
        new(request, consumed: false);

    internal CanvasLoadSchedule ConsumedBy(CanvasRequestIdentity request) =>
        new(request, consumed: true);
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
/// T1 carries the DOCUMENT-class state and the two schedules. The
/// lease, population and unit references — the finer classes of the
/// nesting chain — arrive with their types in task T2, and the record
/// grows to hold them. Nothing here stubs them, because a stub is a
/// fictitious owner and this branch has paid for those before.
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
        CanvasFilterSchedule filters)
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
        filters: CanvasFilterSchedule.Idle);

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

    internal CanvasPublication WithRetired() => Copy(retired: true);

    internal CanvasPublication WithLoadState(CanvasLoadState state, string? message) =>
        Copy(loadState: state, loadMessage: message, loadMessageSet: true);

    internal CanvasPublication WithActiveSurface(CanvasSurfaceKind surface) =>
        Copy(activeSurface: surface);

    internal CanvasPublication WithSelectedIntent(string? nodeId) =>
        Copy(selectedIntent: nodeId, selectedIntentSet: true);

    internal CanvasPublication WithMarkedIntent(IEnumerable<string>? nodeIds) =>
        Copy(markedIntent: CanvasModelCopy.Ids(nodeIds));

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
        CanvasFilterSchedule? filters = null) => new(
            retired ?? Retired,
            loadState ?? LoadState,
            loadMessageSet ? loadMessage : LoadMessage,
            activeSurface ?? ActiveSurface,
            selectedIntentSet ? selectedIntent : SelectedIntent,
            markedIntent ?? MarkedIntent,
            needleIntent ?? NeedleIntent,
            loads ?? Loads,
            filters ?? Filters);
}
