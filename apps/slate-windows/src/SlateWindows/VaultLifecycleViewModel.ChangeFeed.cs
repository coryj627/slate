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
    /// list, a tree showing a level it could not list, and every dependent
    /// showing a failure state, even with an empty window. Then at most
    /// <c>change_feed_max_passes()</c> windows: an exact one through its
    /// records, a gap or an oversized one through R-9's full re-sync, each
    /// followed by the journal's re-read and the feed's head. Then the last
    /// window's acknowledgment, unless a feed read failed, and the journal
    /// once more. Null when the run was cancelled or its vault closed:
    /// nothing is acknowledged, the retry set is as it was, and the next run
    /// reads the same window again.
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
            // first dependents pass only.
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
                    window = await RunRescanCoreAsync("window", () => session.ChangeFeedWindow(from, cancel), cancellation);
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
                    outcome.Failed += await ReSyncFromIndexAsync(generation, reason, session, cancel, cancellation, journal, failedSurfaces);
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
                    // index. Its dependents pass is ungated, which subsumes
                    // the retries and the recovery set.
                    outcome.Failed += await ReSyncFromIndexAsync(generation, reason, session, cancel, cancellation, journal, failedSurfaces);
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
                    head = await RunRescanCoreAsync("head", () => session.ChangeFeedHead(cancel), cancellation);
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

            // The last window's acknowledgment moves the cursor to its target,
            // after a failed re-sync operation too: the retry set owns that
            // failure, not the feed (design §4.3 :671-673). A failed
            // acknowledgment is one error, and the next run reads the window
            // again, so its counts are spoken once, by the run that
            // acknowledges them.
            if (answered && last is not null)
            {
                string token = last.Token;
                try
                {
                    _ = await RunRescanCoreAsync(
                        "acknowledge",
                        () =>
                        {
                            session.AcknowledgeChangeFeed(token, cancel);
                            return true;
                        },
                        cancellation);
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
                }

                if (!Current())
                {
                    return null;
                }
            }

            ulong? finalReread = await RereadJournalAsync(generation, session, cancel, cancellation, journal, failedSurfaces);
            if (finalReread is not ulong finalFailed)
            {
                return null;
            }

            outcome.Failed += finalFailed;
            if (answered && last is { Status: ChangeFeedStatus.Exact, Counted: true })
            {
                outcome.Counted = true;
                outcome.Changed = last.Changed;
                outcome.Removed = last.Removed;
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

    /// <summary>
    /// The record path (design §4.3, §6): an exact window's records, read in
    /// bounded pages under the run's token. Phase A, every page: Quick Open
    /// drops the removed paths and upserts the openable present ones in one
    /// pass; renames are buffered, and the removed and touched paths gather
    /// over the whole window. Phase B, after the last page, because
    /// renames precede removals in page order: each buffered rename moves the
    /// tabs that held its file (<see cref="WorkspaceViewModel.RetargetPathForFeedRename"/>,
    /// the owner's decision Q3), with whether its origin is gone decided over
    /// the complete window. Then, whatever the window holds, every open tab
    /// is compared with the index and the dependents follow, narrowed to the
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
        var touched = new HashSet<string>(StringComparer.Ordinal);
        bool anyCreated = false;
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
                    page = await RunRescanCoreAsync("page", () => session.ChangeFeedPage(token, from, limit, cancel), cancellation);
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
                    application.Failed += await ReSyncFromIndexAsync(generation, reason, session, cancel, cancellation, journal, failedSurfaces);
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
                            anyCreated = true;
                            break;
                        case ChangeEffect.Removed:
                            _ = removed.Add(record.Path);
                            break;
                        case ChangeEffect.Present when record.Counted is ChangeCount.Created or ChangeCount.Uncounted:
                            anyCreated = true;
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

            WorkspaceViewModel? workspace = Workspace;
            foreach (ChangeRecord rename in renames)
            {
                if (rename.RenamedFrom is string from)
                {
                    workspace?.RetargetPathForFeedRename(from, rename.Path, rename.FromHash, oldIsGone: removed.Contains(from));
                }
            }
        }

        var touch = new WorkspaceViewModel.RescanTouch(touched, anyRecord, anyCreated, retry, recover);
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
    /// The Slate-write journal's re-read (design §4.3 :674-690): every path a
    /// Slate-owned event named while the run was open — a rename's both — is
    /// re-read from the index for Quick Open's presence and summary, never
    /// its event replayed. An own write that committed after the window
    /// opened is newer than the window's records and wins over them; one
    /// that committed before it is already in them. The reads run on the
    /// worker under the run's token. Returns the failed operations (one, and
    /// Quick Open enters the retry set), or null when cancelled.
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

        string[] paths = [.. journal.Paths().Order(StringComparer.Ordinal)];
        if (paths.Length == 0)
        {
            return 0;
        }

        try
        {
            (string Path, FileSummary? Summary)[] reads = await RunRescanCoreAsync<(string Path, FileSummary? Summary)[]>(
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
            return generation == _generation && !cancellation.IsCancellationRequested ? 0UL : null;
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
    }
}
