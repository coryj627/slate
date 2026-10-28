// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// WB-10 (#1293, plan 43 21b; design
/// <c>docs/plans/43_mac_backlog_delivery/21_change_feed_design.md</c> §4.3,
/// §4.6, §6): a surface the rescan re-syncs — what a failed re-sync operation
/// leaves in the lifecycle's retry set (design §4.3, "a dependent by its
/// reload"). Tabs have no entry: every run compares every open tab.
/// </summary>
internal enum RescanSurface
{
    Tree,
    QuickOpen,
    Reading,
    History,
    Bases,
    Graph,
}

/// <summary>
/// WB-10 (#1293): the rescan's consumption of core's change feed. The open
/// registers this session's cursor after its scan; each run, after its scan,
/// opens a window from the cursor, applies an exact window's records by path
/// (renames by baseline, Quick Open in one pass per page, the dependents
/// narrowed to the window's paths), takes R-9's full re-sync for a gap or an
/// oversized window, makes up to <c>change_feed_max_passes()</c> passes when
/// another session committed meanwhile (AR-31), and acknowledges. Only an
/// explicit Refresh speaks other sessions' changes (the owner's decision Q2).
/// </summary>
internal sealed partial class VaultLifecycleViewModel
{
    private static readonly RescanSurface[] DependentSurfaces =
        [RescanSurface.Reading, RescanSurface.History, RescanSurface.Bases, RescanSurface.Graph];

    // The surfaces whose last re-sync failed: the next run, whatever its
    // reason, retries each (design §4.3 :662-673). A surface leaves only when
    // its retry ran and did not fail; a cancelled run leaves the set as it
    // found it; a close or a vault switch clears it (ResetRescanState).
    private readonly HashSet<RescanSurface> _retrySet = [];

    // The feed renames this session applied since its cursor last moved,
    // origin to destination: a run that applied a window and then could not
    // acknowledge it (cancelled, a feed read or the acknowledgment failed)
    // leaves its tabs moved while the next run reads from the same cursor,
    // where core composes a later rename with the earlier one (A→B, then
    // B→C, is one record A→C) or cancels a rename that came home. Cleared by
    // an acknowledgment and by a close or a vault switch.
    private readonly Dictionary<string, (string To, string? FromHash)> _appliedFeedRenames =
        new(StringComparer.Ordinal);

    /// <summary>The retry set, for the facts.</summary>
    internal IReadOnlySet<RescanSurface> RetrySetForTests => _retrySet;

    /// <summary>Test seam: a smaller page than the host's 1,000 (within
    /// core's bound), so a fact proves the paging with a few records.</summary>
    internal uint? ChangeFeedPageLimitForTests { get; set; }

    /// <summary>Test seam: every window a run opens, as it crossed the FFI,
    /// on the dispatcher, before the run applies it.</summary>
    internal Action<ChangeFeedWindow>? ChangeFeedWindowObserverForTests { get; set; }

    /// <summary>Test seam: replaces the open's <c>RegisterChangeFeed()</c>
    /// (on the session load's worker), so a fact can make it fail or skip
    /// it.</summary>
    internal Func<VaultSession, ulong>? RegisterChangeFeedForTests { get; set; }

    /// <summary>Test seam: a feed call — <c>window</c>, <c>page</c>,
    /// <c>head</c>, <c>acknowledge</c>, <c>summary</c> — for which this
    /// answers true throws on the worker before core is asked, as a busy
    /// cache would.</summary>
    internal Func<string, bool>? ChangeFeedFaultForTests { get; set; }

    /// <summary>Test seam: cancel the running rescan in place — its managed
    /// twin, then its native token through the rescan core seam, as a close
    /// cancels it — with the lifecycle and the session kept.</summary>
    internal Task CancelRunningRescanForTests()
    {
        _rescanCancellation?.Cancel();
        CancelToken? cancel = _rescanCancel;
        return cancel is null
            ? Task.CompletedTask
            : StartRescanCoreCall(
                "cancel",
                () =>
                {
                    cancel.Cancel();
                    return true;
                });
    }

    /// <summary>
    /// The open: this session's cursor at the feed's head, right after the
    /// open scan and before Quick Open's listing (design §6), so the open
    /// scan's own files — which <c>VaultScanFinished</c> spoke — are not in
    /// the first window. A failure (a busy cache, a schema skew) is logged
    /// with no path (#507) and never spoken: the first run's window is then a
    /// gap, whose full re-sync re-creates the cursor.
    /// </summary>
    private void RegisterChangeFeed(VaultSession session, Func<VaultSession, ulong>? register)
    {
        try
        {
            _ = register is null ? session.RegisterChangeFeed() : register(session);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.Write(HostDiagnosticEvent.ChangeFeedRegisterFailed, exception);
        }
    }

    /// <summary>One feed call through the rescan core seam, under the run's
    /// token, with the fact's fault applied first.</summary>
    private Task<T> RunChangeFeedCallAsync<T>(string operation, Func<T> call, CancellationToken cancellation)
    {
        Func<string, bool>? fault = ChangeFeedFaultForTests;
        return RunRescanCoreAsync(
            operation,
            () => fault?.Invoke(operation) == true
                ? throw new VaultException.Io($"test fault: {operation}")
                : call(),
            cancellation);
    }

    /// <summary>What a run's consumption of the feed did, for its one
    /// sentence (design §4.7). <see cref="Failed"/> first: failed re-sync
    /// operations, feed reads and acknowledgments, one error each. Then
    /// <see cref="Counted"/>: the run's last window was exact and counted,
    /// every feed read answered and its acknowledgment landed, so
    /// <see cref="Changed"/> and <see cref="Removed"/> are exact from this
    /// session's cursor.</summary>
    private sealed class ChangeFeedOutcome
    {
        internal ulong Failed { get; set; }

        internal bool Counted { get; set; }

        internal ulong Changed { get; set; }

        internal ulong Removed { get; set; }
    }

    /// <summary>One exact window's application.</summary>
    private sealed class WindowApplication
    {
        internal ulong Failed { get; set; }

        /// <summary>Every page of the window was read.</summary>
        internal bool Answered { get; set; } = true;
    }

    /// <summary>
    /// The run after its scan (design §4.3, §4.6). The Slate-write journal
    /// opens first and stays open to the run's end. Then the retry set,
    /// whatever the reason — Quick Open and the tree by their full reloads;
    /// the dependents ride the run's first dependents pass — and an explicit
    /// Refresh's recovery set (the owner's decision Q8): Quick Open's whole
    /// list, a tree showing a failure, and every dependent showing one, even
    /// with an empty window. Then at most <c>change_feed_max_passes()</c>
    /// windows: an exact one through its records, a gap or an oversized one
    /// through R-9's full re-sync, each followed by the journal's re-read and
    /// the feed's head. Then the journal once more, and the last window's
    /// acknowledgment, unless a feed read failed. Null when the run was
    /// cancelled or its vault closed: nothing is acknowledged, the retry set
    /// is as it was, and the next run reads the same window again.
    /// </summary>
    private async Task<ChangeFeedOutcome?> ConsumeChangeFeedAsync(
        int generation,
        RescanReason reason,
        VaultSession session,
        CancelToken cancel,
        CancellationToken cancellation)
    {
        bool Current() => generation == _generation && !cancellation.IsCancellationRequested;

        var outcome = new ChangeFeedOutcome();
        var failedSurfaces = new HashSet<RescanSurface>();
        var retried = new HashSet<RescanSurface>();
        HashSet<RescanSurface> retrying = [.. _retrySet];
        QuickSwitcherViewModel? switcher = QuickSwitcher;
        QuickSwitcherViewModel.ReplacementJournal? journal = switcher?.BeginReplacement();
        try
        {
            if (retrying.Contains(RescanSurface.QuickOpen) || reason == RescanReason.Explicit)
            {
                ulong failed = await ContainedAsync(ReSyncQuickOpenAsync(session, cancel, cancellation, journal), cancellation);
                if (!Current())
                {
                    return null;
                }

                outcome.Failed += failed;
                _ = retried.Add(RescanSurface.QuickOpen);
                if (failed > 0)
                {
                    _ = failedSurfaces.Add(RescanSurface.QuickOpen);
                }
            }

            if (retrying.Contains(RescanSurface.Tree)
                || (reason == RescanReason.Explicit && FileSidebar?.ShowsFailedLevelForRescan == true))
            {
                ulong failed = await ContainedAsync(ReSyncTreeAsync(reason, cancellation), cancellation);
                if (!Current())
                {
                    return null;
                }

                outcome.Failed += failed;
                _ = retried.Add(RescanSurface.Tree);
                if (failed > 0)
                {
                    _ = failedSurfaces.Add(RescanSurface.Tree);
                }
            }

            // The dependents' retries and the recovery set ride the run's
            // first dependents pass only — the full re-sync's too.
            IReadOnlySet<RescanSurface> retry = new HashSet<RescanSurface>(retrying.Intersect(DependentSurfaces));
            bool recover = reason == RescanReason.Explicit;
            ulong? after = null;
            ChangeFeedWindow? last = null;
            bool answered = true;
            int passes = Math.Max(1, (int)SlateUniffiMethods.ChangeFeedMaxPasses());
            for (int pass = 1; pass <= passes; pass++)
            {
                ulong? from = after;
                ChangeFeedWindow window;
                try
                {
                    window = await RunChangeFeedCallAsync("window", () => session.ChangeFeedWindow(from, cancel), cancellation);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    if (!Current())
                    {
                        return null;
                    }

                    // The feed cannot answer: one error, nothing
                    // acknowledged, and R-9's full re-sync.
                    HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
                    outcome.Failed++;
                    answered = false;
                    outcome.Failed += await ReSyncFromIndexAsync(
                        generation, reason, session, cancel, cancellation, journal, failedSurfaces, RescanTouch(retry, recover));
                    retried.UnionWith(retry);
                    if (!Current())
                    {
                        return null;
                    }

                    break;
                }

                if (!Current())
                {
                    return null;
                }

                ChangeFeedWindowObserverForTests?.Invoke(window);
                last = window;
                if (window.Status is ChangeFeedStatus.Exact)
                {
                    WindowApplication? applied = await ApplyChangeFeedWindowAsync(
                        generation, reason, session, window, cancel, cancellation, journal, retry, recover, failedSurfaces);
                    if (applied is null)
                    {
                        return null;
                    }

                    outcome.Failed += applied.Failed;
                    if (!applied.Answered)
                    {
                        answered = false;
                    }
                }
                else
                {
                    // More than 10,000 rows, or rows the feed no longer
                    // holds: nothing was read, and the full re-sync shows the
                    // index. Its dependents pass is ungated, and the retries
                    // and the recovery set ride it.
                    outcome.Failed += await ReSyncFromIndexAsync(
                        generation, reason, session, cancel, cancellation, journal, failedSurfaces, RescanTouch(retry, recover));
                    if (!Current())
                    {
                        return null;
                    }
                }

                retried.UnionWith(retry);
                retry = new HashSet<RescanSurface>();
                recover = false;

                ulong? reread = await RereadJournalAsync(generation, session, cancel, cancellation, journal, failedSurfaces);
                if (reread is not ulong rereadFailed)
                {
                    return null;
                }

                outcome.Failed += rereadFailed;
                if (!answered || pass >= passes)
                {
                    break;
                }

                ulong head;
                try
                {
                    head = await RunChangeFeedCallAsync("head", () => session.ChangeFeedHead(cancel), cancellation);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    if (!Current())
                    {
                        return null;
                    }

                    HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
                    outcome.Failed++;
                    answered = false;
                    break;
                }

                if (!Current())
                {
                    return null;
                }

                // A head past the target: another session committed after the
                // window opened, and a further window applies the rest.
                if (head <= window.ToSeq)
                {
                    break;
                }

                after = window.ToSeq;
            }

            // The journal's last re-read comes before the acknowledgment, so
            // a run cancelled during it has moved no cursor.
            ulong? finalReread = await RereadJournalAsync(generation, session, cancel, cancellation, journal, failedSurfaces);
            if (finalReread is not ulong finalFailed)
            {
                return null;
            }

            outcome.Failed += finalFailed;

            // The last window's acknowledgment moves the cursor to its target,
            // after a failed re-sync operation too: the retry set owns that
            // failure, not the feed (design §4.3 :671-673). A failed
            // acknowledgment is one error, and the next run reads the window
            // again, so its counts are spoken once, by the run that
            // acknowledges them.
            bool acknowledged = false;
            if (answered && last is not null)
            {
                string token = last.Token;
                try
                {
                    _ = await RunChangeFeedCallAsync(
                        "acknowledge",
                        () =>
                        {
                            session.AcknowledgeChangeFeed(token, cancel);
                            return true;
                        },
                        cancellation);
                    acknowledged = true;
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    if (!Current())
                    {
                        return null;
                    }

                    HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
                    outcome.Failed++;
                }

                if (!Current())
                {
                    return null;
                }
            }

            if (acknowledged)
            {
                _appliedFeedRenames.Clear();
                if (last is { Status: ChangeFeedStatus.Exact, Counted: true })
                {
                    outcome.Counted = true;
                    outcome.Changed = last.Changed;
                    outcome.Removed = last.Removed;
                }
            }

            _retrySet.ExceptWith(retried);
            _retrySet.UnionWith(failedSurfaces);
            return outcome;
        }
        finally
        {
            if (journal is not null)
            {
                switcher!.EndReplacement(journal);
            }
        }
    }

    /// <summary>The full re-sync's dependents pass: ungated, with the run's
    /// retries and recovery set.</summary>
    private static WorkspaceViewModel.RescanTouch RescanTouch(IReadOnlySet<RescanSurface> retry, bool recover) =>
        WorkspaceViewModel.RescanTouch.Full(retry, recover);

    /// <summary>
    /// The record path (design §4.3, §6): an exact window's records, read in
    /// bounded pages under the run's token. Phase A, every page: Quick Open
    /// drops the removed paths and upserts the openable present ones in one
    /// pass; renames are buffered, and the removed, present and touched paths
    /// gather over the whole window. Phase B, after the last page, because
    /// renames precede removals in page order: the buffered renames move the
    /// tabs that held their files in one planned batch
    /// (<see cref="WorkspaceViewModel.RetargetPathsForFeedRenames"/>, the
    /// owner's decision Q3), each with whether its origin is gone decided over
    /// the complete window, and the renames an unacknowledged run applied are
    /// followed on. Then, whatever the window holds, every open tab is
    /// compared with the index and the dependents follow, narrowed to the
    /// window's paths (<see cref="WorkspaceViewModel.RescanTouch"/>), beside
    /// the tree's one silent refresh when the window has a record. Null when
    /// cancelled.
    /// </summary>
    private async Task<WindowApplication?> ApplyChangeFeedWindowAsync(
        int generation,
        RescanReason reason,
        VaultSession session,
        ChangeFeedWindow window,
        CancelToken cancel,
        CancellationToken cancellation,
        QuickSwitcherViewModel.ReplacementJournal? journal,
        IReadOnlySet<RescanSurface> retry,
        bool recover,
        ISet<RescanSurface> failedSurfaces)
    {
        bool Current() => generation == _generation && !cancellation.IsCancellationRequested;

        var application = new WindowApplication();
        var renames = new List<ChangeRecord>();
        var removed = new HashSet<string>(StringComparer.Ordinal);
        var present = new HashSet<string>(StringComparer.Ordinal);
        var touched = new HashSet<string>(StringComparer.Ordinal);
        bool anyRecord = window.RecordCount > 0;
        if (anyRecord)
        {
            uint limit = Math.Clamp(
                ChangeFeedPageLimitForTests ?? 1_000U,
                1U,
                SlateUniffiMethods.MaxChangeFeedPageLimit());
            string token = window.Token;
            string? cursor = null;
            do
            {
                string? from = cursor;
                ChangeFeedPage page;
                try
                {
                    page = await RunChangeFeedCallAsync("page", () => session.ChangeFeedPage(token, from, limit, cancel), cancellation);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    if (!Current())
                    {
                        return null;
                    }

                    // A page that cannot be read leaves the window unknown:
                    // one error, nothing acknowledged, and the full re-sync.
                    HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
                    application.Failed++;
                    application.Answered = false;
                    application.Failed += await ReSyncFromIndexAsync(
                        generation, reason, session, cancel, cancellation, journal, failedSurfaces, RescanTouch(retry, recover));
                    return Current() ? application : null;
                }

                if (!Current())
                {
                    return null;
                }

                foreach (ChangeRecord record in page.Records)
                {
                    _ = touched.Add(record.Path);
                    if (record.RenamedFrom is string renamedFrom)
                    {
                        _ = touched.Add(renamedFrom);
                    }

                    if (record.Entry == ChangeEntry.Directory)
                    {
                        // A folder created, removed or renamed: each file it
                        // moved or removed is a record of its own.
                        continue;
                    }

                    switch (record.Effect)
                    {
                        case ChangeEffect.Renamed:
                            renames.Add(record);
                            break;
                        case ChangeEffect.Removed:
                            _ = removed.Add(record.Path);
                            break;
                        case ChangeEffect.Present:
                            _ = present.Add(record.Path);
                            break;
                    }
                }

                if (QuickSwitcher is QuickSwitcherViewModel switcher)
                {
                    try
                    {
                        await switcher.ApplyRecordsAsync(page.Records, cancellation);
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    {
                        if (!Current())
                        {
                            return null;
                        }

                        HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
                        application.Failed++;
                        _ = failedSurfaces.Add(RescanSurface.QuickOpen);
                    }

                    if (!Current())
                    {
                        return null;
                    }
                }

                cursor = page.NextCursor;
            }
            while (cursor is not null);
        }

        if (Workspace is WorkspaceViewModel workspace
            && PlanFeedRenames(renames, removed, present) is { Count: > 0 } moves)
        {
            try
            {
                workspace.RetargetPathsForFeedRenames(moves);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // Contained: one error; the tab comparison below still runs.
                HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
                application.Failed++;
            }
        }

        var touch = new WorkspaceViewModel.RescanTouch(touched, anyRecord, retry, recover);
        Task<ulong> tree = anyRecord ? ReSyncTreeAsync(reason, cancellation) : Task.FromResult(0UL);
        Task<ulong> workspaceSync = Workspace is WorkspaceViewModel open
            ? ReSyncWorkspaceAsync(open, session, cancel, cancellation, touch, failedSurfaces)
            : Task.FromResult(0UL);

        ulong workspaceFailed = await ContainedAsync(workspaceSync, cancellation, () => failedSurfaces.UnionWith(DependentSurfaces));
        ulong treeFailed = await ContainedAsync(tree, cancellation);
        if (!Current())
        {
            return null;
        }

        if (treeFailed > 0)
        {
            _ = failedSurfaces.Add(RescanSurface.Tree);
        }

        application.Failed += workspaceFailed + treeFailed;
        return application;
    }

    /// <summary>
    /// The window's moves: each <c>Renamed</c> record's, and for a record whose
    /// origin an unacknowledged run already moved on, the move from wherever
    /// that run left its tabs (the chain's every stop) to the record's
    /// destination; and for a rename an unacknowledged run applied that came
    /// home since — its destination gone, its origin back, no record of its
    /// own — the move back. The applied renames are kept until the cursor
    /// moves (<see cref="_appliedFeedRenames"/>).
    /// </summary>
    private List<WorkspaceViewModel.FeedRename> PlanFeedRenames(
        IReadOnlyList<ChangeRecord> renames,
        IReadOnlySet<string> removed,
        IReadOnlySet<string> present)
    {
        var moves = new List<WorkspaceViewModel.FeedRename>();
        var renamedFrom = new HashSet<string>(StringComparer.Ordinal);
        foreach (ChangeRecord rename in renames)
        {
            if (rename.RenamedFrom is not string from)
            {
                continue;
            }

            _ = renamedFrom.Add(from);
            moves.Add(new(from, rename.Path, rename.FromHash, removed.Contains(from)));
            var visited = new HashSet<string>(StringComparer.Ordinal) { from };
            string at = from;
            while (_appliedFeedRenames.TryGetValue(at, out (string To, string? FromHash) applied)
                && visited.Add(applied.To))
            {
                if (!string.Equals(applied.To, rename.Path, StringComparison.Ordinal))
                {
                    moves.Add(new(applied.To, rename.Path, rename.FromHash, removed.Contains(applied.To)));
                }

                at = applied.To;
            }
        }

        foreach ((string from, (string to, string? fromHash)) in _appliedFeedRenames.ToList())
        {
            if (!renamedFrom.Contains(from) && removed.Contains(to) && present.Contains(from))
            {
                moves.Add(new(to, from, fromHash, OldIsGone: true));
                _ = _appliedFeedRenames.Remove(from);
            }
        }

        foreach (ChangeRecord rename in renames)
        {
            if (rename.RenamedFrom is string from)
            {
                _appliedFeedRenames[from] = (rename.Path, rename.FromHash);
            }
        }

        return moves;
    }

    /// <summary>
    /// The Slate-write journal's re-read (design §4.3 :674-690): every path a
    /// Slate-owned event named while the run was open — a rename's both — is
    /// re-read from the index for Quick Open's presence and summary, never
    /// its event replayed. An own write that committed after the window
    /// opened is newer than the window's records and wins over them; one
    /// that committed before it is already in them. The reads run on the
    /// worker under the run's token; an own event journaled while they ran
    /// (a rename the read predates) makes the re-read run again, at most three
    /// times. Returns the failed operations (one, and Quick Open enters the
    /// retry set), or null when cancelled.
    /// </summary>
    private async Task<ulong?> RereadJournalAsync(
        int generation,
        VaultSession session,
        CancelToken cancel,
        CancellationToken cancellation,
        QuickSwitcherViewModel.ReplacementJournal? journal,
        ISet<RescanSurface> failedSurfaces)
    {
        if (journal is null || QuickSwitcher is not QuickSwitcherViewModel switcher)
        {
            return 0;
        }

        for (int round = 0; round < 3; round++)
        {
            int journaled = journal.Changes.Count;
            string[] paths = [.. journal.Paths().Order(StringComparer.Ordinal)];
            if (paths.Length == 0)
            {
                return 0;
            }

            try
            {
                (string Path, FileSummary? Summary)[] reads = await RunChangeFeedCallAsync<(string Path, FileSummary? Summary)[]>(
                    "summary",
                    () =>
                    {
                        var found = new List<(string Path, FileSummary? Summary)>(paths.Length);
                        foreach (string path in paths)
                        {
                            if (cancel.IsCancelled())
                            {
                                throw new VaultException.Cancelled();
                            }

                            try
                            {
                                found.Add((path, session.GetFileSummary(path)));
                            }
                            catch (VaultException.InvalidPath)
                            {
                                // A path the index can never hold.
                                found.Add((path, null));
                            }
                        }

                        return [.. found];
                    },
                    cancellation);
                if (generation != _generation || cancellation.IsCancellationRequested)
                {
                    return null;
                }

                await switcher.ApplyJournaledPathsAsync(reads, cancellation);
                if (generation != _generation || cancellation.IsCancellationRequested)
                {
                    return null;
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                if (generation != _generation || cancellation.IsCancellationRequested)
                {
                    return null;
                }

                HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
                _ = failedSurfaces.Add(RescanSurface.QuickOpen);
                return 1;
            }

            if (journal.Changes.Count == journaled)
            {
                return 0;
            }
        }

        return 0;
    }
}
