// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 PR C-unit, task T6: the one FFI call the filter machine makes,
/// as the machine sees it.
/// </summary>
/// <remarks>
/// An interface for the pipeline's reason: the machine names no FFI
/// surface, and its battery drives an in-memory matcher that blocks and
/// faults on demand — the barrier facts need a match that parks, and a
/// real session cannot be asked to.
/// </remarks>
internal interface ICanvasFilterSource
{
    /// <summary>Core's <c>canvas_filter</c>: the matched node ids for a
    /// needle, through the given handle. Core trims the needle; an empty
    /// one matches everything.</summary>
    string[] Match(ulong handle, string needle);
}

/// <summary>The points of the filter job a fact can fault or barrier
/// at. None is inside the gate: the match itself is the injectable call
/// and the source owns it.</summary>
internal enum CanvasFilterPoint
{
    /// <summary>After the job's decision read, before the FFI lock.
    /// Where a barrier parks a job so a keystroke or a teardown can
    /// land between its decision and its lock.</summary>
    BeforeLock,

    /// <summary>After the answer is built, before the completion's
    /// swap. Where a barrier lands a keystroke between the match and
    /// its publication.</summary>
    BeforeSwap,

    /// <summary>After the completion's swap, installed or declined.</summary>
    AfterSwap,
}

/// <summary>
/// W6-1 PR C-unit, task T6: the filter battery's instrument — one
/// callback at each point of the job, with the test-seam suffix this
/// codebase's guard is built on. Not attached in production, and the
/// machine behaves identically with and without it.
/// </summary>
internal sealed class CanvasFilterProbeForTests
{
    internal Action<CanvasFilterPoint>? OnPoint { get; set; }

    internal void Reached(CanvasFilterPoint point) => OnPoint?.Invoke(point);
}

/// <summary>
/// W6-1 PR C-unit, task T6: THE FILTER MACHINE — U9's total state
/// machine, driven. The schedule and the unit ARE the state; this class
/// holds none of its own, because a machine with a private copy of the
/// schedule would be a second authority for the one question the
/// publication answers.
/// </summary>
/// <remarks>
/// <para>
/// Every event is one transform under the gate, decided from its
/// snapshot: the KEYSTROKE publishes the needle intent, the schedule
/// transition and — when the request starts at once — the pending unit,
/// together; the COMPLETION publishes the answer with the running entry
/// retired, or discards the answer and promotes the queued request,
/// because publishing R's rows under Q's needle is the arrangement U9
/// exists to prevent; a NON-RUNNING completion publishes nothing at
/// all. Load acceptance's column belongs to the pipeline — the reseed —
/// and it hands the minted request here to be started.
/// </para>
/// <para>
/// STARTING A JOB IS AN EFFECT and follows a won swap, never an
/// attempt: the machine reads which request became running from the
/// outcome — predecessor against successor — and never from a captured
/// local. The job itself runs wherever the host's runner puts it (the
/// tracked worker in production, inline in synchronous tests), makes
/// ONE decision read, and revalidates INSIDE the FFI lock: a job parked
/// behind a long call is abandoned before its own FFI call once
/// superseded, which is what bounds a burst of ten keystrokes to at
/// most two matches — the one already in flight, and the last one
/// standing.
/// </para>
/// <para>
/// A match that THROWS — panic-class included — becomes the unit's
/// FAILED answer state rather than a faulted task or a silent widen:
/// the failed-answer bit is §C's travelling row, and it lands here as
/// data a surface can be honest about.
/// </para>
/// </remarks>
internal sealed class CanvasFilterMachine
{
    /// <summary>The one owner of "does this needle filter" (task T6,
    /// after its review): the keystroke, the acceptance's reseed and
    /// the surface's UI state all read this predicate, so a whitespace
    /// needle cannot be inactive at the keystroke and active at the
    /// reseed. The NEWLINE carve-out is deliberate — mac's
    /// <c>.whitespaces</c> does not include newlines, so a needle of
    /// nothing but a newline is ACTIVE and (core trimming it) matches
    /// everything; the wider .NET whitespace classes stay inactive, a
    /// ratified divergence pinned by its own fact.</summary>
    internal static bool IsActiveNeedle(string needle) =>
        needle.Any(character => !char.IsWhiteSpace(character) || character is '\n' or '\r');

    private readonly CanvasPublicationSlot _slot;
    private readonly ICanvasFilterSource _source;
    private readonly Action<Action> _run;
    private readonly CanvasFilterProbeForTests? _probe;

    /// <param name="run">Where a job runs — the host's tracked worker
    /// in production, so a job mid-FFI is part of the teardown drain;
    /// inline in synchronous tests. SOURCE-FREE: it schedules, and the
    /// job it is handed depends on nothing the runner owns.</param>
    internal CanvasFilterMachine(
        CanvasPublicationSlot slot,
        ICanvasFilterSource source,
        Action<Action> run,
        CanvasFilterProbeForTests? probeForTests = null)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(run);
        _slot = slot;
        _source = source;
        _run = run;
        _probe = probeForTests;
    }

    /// <summary>
    /// The keystroke, U9's first column. An ACTIVE needle mints a
    /// request: started at once with the pending unit published in the
    /// same swap when the machine is idle, queued — replacing whatever
    /// was queued, unstarted — when a job runs. An INACTIVE needle
    /// retires both entries and widens the unit to the whole
    /// population; the running job, superseded, abandons itself at its
    /// next revalidation. Either way the needle INTENT publishes, so a
    /// keystroke mid-load still reseeds at acceptance.
    /// </summary>
    internal void Typed(string needle, bool active)
    {
        ArgumentNullException.ThrowIfNull(needle);
        CanvasPublicationOutcome outcome = _slot.Publish(snapshot =>
        {
            if (snapshot.Retired)
            {
                return null;
            }

            CanvasPublication next = snapshot.WithNeedleIntent(needle);
            if (snapshot.Loaded is not { } loaded)
            {
                // Nothing to match against. The intent is recorded, and
                // acceptance's reseed seeds the machine — U9's load
                // column, owned by the pipeline.
                return next;
            }

            if (!active)
            {
                return next
                    .WithFilters(snapshot.Filters.Reseeded(null))
                    .WithUnit(CanvasProjectionUnit.Unfiltered(
                        loaded.Population,
                        loaded.Population.Resolve(snapshot.SelectedIntent)));
            }

            var request = new CanvasRequestIdentity($"filter \"{needle}\"");
            CanvasFilterSchedule filters = snapshot.Filters.Typed(request);
            next = next.WithFilters(filters);
            if (ReferenceEquals(filters.Running, request))
            {
                // Idle machine: the request starts now, and the PENDING
                // unit publishes immediately — the surface keeps the
                // rows it has instead of blanking while the match runs.
                next = next.WithUnit(loaded.Unit.Pending(request, needle));
            }

            return next;
        });
        StartIfRunningChanged(outcome);
    }

    /// <summary>U9's load-acceptance column, second half: the reseeded
    /// request the acceptance minted, handed here by the pipeline to be
    /// started — the effect after that won swap.</summary>
    internal void StartReseeded(CanvasRequestIdentity request, string needle)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(needle);
        _run(() => Job(request, needle));
    }

    /// <summary>The one start rule: a job starts exactly when a swap
    /// CHANGED which request is running — read from the outcome, never
    /// from a captured local.</summary>
    private void StartIfRunningChanged(CanvasPublicationOutcome outcome)
    {
        if (!outcome.Installed)
        {
            return;
        }

        CanvasRequestIdentity? running = outcome.Successor.Filters.Running;
        if (running is null
            || ReferenceEquals(running, outcome.Predecessor.Filters.Running))
        {
            return;
        }

        string needle = outcome.Successor.Unit!.Needle;
        _run(() => Job(running, needle));
    }

    /// <summary>One job: one decision read, revalidation inside the FFI
    /// lock, and a completion that is itself a transform.</summary>
    private void Job(CanvasRequestIdentity request, string needle)
    {
        CanvasPublication decision = _slot.Current;
        if (decision.Retired
            || decision.Lease is not { } lease
            || !ReferenceEquals(decision.Filters.Running, request))
        {
            // Superseded, torn down, or unloaded before the job began:
            // U9's non-running column, reached before the FFI was.
            return;
        }

        _probe?.Reached(CanvasFilterPoint.BeforeLock);
        string[]? matched = null;
        var failed = false;
        try
        {
            bool admitted = lease.Invoke(
                () =>
                {
                    // Revalidated AFTER acquiring the lock: a job parked
                    // behind a long call is abandoned before its own FFI
                    // call once superseded — U9's burst rule — and a
                    // waiter that acquires after lease death is refused
                    // before FFI.
                    CanvasPublication now = _slot.Current;
                    return !now.Retired
                        && now.Names(lease)
                        && ReferenceEquals(now.Filters.Running, request);
                },
                handle => matched = _source.Match(handle, needle));
            if (!admitted)
            {
                return;
            }
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException
                and not CanvasLeaseViolationException)
        {
            // The failed-answer bit, §C's travelling row: panic-class
            // included, the fault becomes an ANSWER STATE rather than a
            // faulted task or a silent widen. The lease's own tripwire
            // is EXCLUDED by name — an invariant breach is not a failed
            // match, and quieting it here was the T6 review's finding.
            failed = true;
        }

        Complete(request, matched, failed);
    }

    /// <summary>U9's completion column, both cells: the running
    /// completion publishes its answer — or, with a queue, DISCARDS it
    /// and promotes — and the non-running completion publishes
    /// nothing.</summary>
    private void Complete(CanvasRequestIdentity request, string[]? matched, bool failed)
    {
        // Built OUTSIDE the gate (I2's ledger note): the answered
        // projection walks the outline. One read suffices: while this
        // request is RUNNING, only acceptance replaces the population
        // or the unit, and acceptance retires the request — which the
        // transform's own check reads inside the gate. NOT built at all
        // when a queued request already guarantees the discard: the
        // queue can only be consumed by this completion, so a pre-read
        // queue is a gate-time queue, and the walk would be thrown
        // away (the T6 review's efficiency note).
        CanvasPublication before = _slot.Current;
        if (before.Loaded is not { } loaded
            || !ReferenceEquals(before.Filters.Running, request))
        {
            return;
        }

        CanvasProjectionUnit? answered = before.Filters.Queued is not null
            ? null
            : failed
                ? loaded.Unit.Failed()
                : loaded.Unit.Answered(loaded.Population, matched);

        _probe?.Reached(CanvasFilterPoint.BeforeSwap);
        CanvasPublicationOutcome outcome = _slot.Publish(snapshot =>
        {
            if (snapshot.Retired
                || snapshot.Loaded is not { } now
                || !ReferenceEquals(snapshot.Filters.Running, request)
                || !ReferenceEquals(now.Unit, loaded.Unit))
            {
                // The non-running completion: refuse the delivery,
                // publish nothing. The unit-reference arm is a detector
                // for the unreachable case — a unit moved while its
                // request still ran — kept because declining is free
                // and installing over it would not be.
                return null;
            }

            if (snapshot.Filters.Queued is { } promoted)
            {
                // DISCARD this answer and promote: publishing it would
                // rest these rows under the queued needle, which is the
                // arrangement U9 exists to prevent. The promoted
                // request's needle is the intent — the queued entry is
                // always the latest keystroke.
                return snapshot
                    .WithFilters(snapshot.Filters.Finished())
                    .WithUnit(now.Unit.Pending(promoted, snapshot.NeedleIntent));
            }

            if (answered is null)
            {
                // The queue vanished between the pre-read and the gate,
                // which nothing can do — only this completion consumes
                // it. A detector arm: decline rather than install a
                // projection that was never built.
                return null;
            }

            return snapshot
                .WithFilters(snapshot.Filters.Finished())
                .WithUnit(answered);
        });
        _probe?.Reached(CanvasFilterPoint.AfterSwap);

        // The promotion's start — the effect after the won swap.
        StartIfRunningChanged(outcome);
    }
}
