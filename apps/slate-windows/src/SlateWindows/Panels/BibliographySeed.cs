// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Panels;

/// <summary>How the one-per-vault bibliography seed ended.</summary>
internal enum BibliographySeedStatus
{
    /// <summary>Sources were configured and core accepted them.</summary>
    Seeded,

    /// <summary>The vault configures no bibliography source at all — a
    /// DISTINCT state from "sources exist but yielded no entries"
    /// (contract 5).</summary>
    NoSources,

    /// <summary>Sources were configured and the load failed. Core's
    /// <c>set_bibliography_sources</c> is all-or-nothing, so NOTHING
    /// was replaced.</summary>
    Failed,

    /// <summary>Teardown landed before the seed did.</summary>
    Cancelled,
}

/// <summary>
/// The terminal result of seeding, plus whatever notices the attempt
/// produced. Notices are carried verbatim and never swallowed
/// (contract 5).
/// </summary>
internal sealed record BibliographySeedOutcome(
    BibliographySeedStatus Status,
    IReadOnlyList<string> Notices)
{
    /// <summary>
    /// Whether core's bibliography tables may be READ as authoritative.
    ///
    /// After a FAILED seed they may not. Core builds its initial
    /// BibIndex from whatever `bibliography_entries` already holds
    /// (session.rs:2085-2088), and a failed `load_source` returns early
    /// (session.rs:8491) BEFORE `replace_bibliography_entries` and
    /// before the index rebuild — so both survive untouched. Querying
    /// then answers from the PREVIOUS session's data while the notice
    /// says loading failed: stale bytes presented as authoritative,
    /// which is exactly what contract 5 forbids.
    /// </summary>
    internal bool MayReadEntries =>
        Status is BibliographySeedStatus.Seeded or BibliographySeedStatus.NoSources;

    internal bool HasSources => Status is not BibliographySeedStatus.NoSources;
}

/// <summary>
/// The workspace-level prerequisite both citation leaves wait on.
///
/// This replaces the bare <c>TaskCompletionSource</c> gate that the
/// first fix used. A gate encodes only WHEN seeding finished; the
/// panels need to know WHAT HAPPENED, because a failed seed must stop
/// them reading core rather than merely let them through late.
///
/// It ALWAYS completes, and never completes as faulted or cancelled.
/// A naive <c>TrySetCanceled</c> here would throw out of the awaiting
/// continuation into <see cref="PanelWorkScheduler"/>'s catch, which
/// swallows it and runs the body anyway — the cancellation would be
/// silently ineffective. Cancellation is therefore a STATUS.
/// </summary>
internal sealed class BibliographySeed
{
    private readonly TaskCompletionSource<BibliographySeedOutcome> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes with the terminal outcome. Never faults,
    /// never cancels.</summary>
    internal Task<BibliographySeedOutcome> Completion => _completion.Task;

    /// <summary>The settled outcome, or null while the seed is still in
    /// flight. Readers that have already awaited
    /// <see cref="Completion"/> can rely on this being non-null.</summary>
    internal BibliographySeedOutcome? Outcome { get; private set; }

    /// <summary>Settle the seed. The FIRST call wins: a teardown that
    /// races a landing seed must not overwrite a real outcome with
    /// Cancelled, and vice versa.</summary>
    private readonly object _settleLock = new();

    internal void Complete(BibliographySeedOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        BibliographySeedOutcome settled;
        lock (_settleLock)
        {
            // Publish the outcome BEFORE releasing waiters, so a body
            // that wakes on Completion always observes a non-null
            // Outcome. Locked because teardown and a landing seed can
            // call this concurrently — an unsynchronised `??=` lets
            // both read null and the loser's result reach waiters that
            // already saw the winner's Outcome.
            Outcome ??= outcome;
            settled = Outcome;
        }
        _ = _completion.TrySetResult(settled);
    }

    internal void Cancel() =>
        Complete(new BibliographySeedOutcome(BibliographySeedStatus.Cancelled, []));
}
