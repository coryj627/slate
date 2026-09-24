// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 7 (#1252, contract R-9): the rescan — the only reconciliation a
/// file created, changed or deleted outside Slate gets on Windows (OD-1: no
/// live watcher). Files Sidebar → Refresh runs it <see
/// cref="RescanReason.Explicit"/>; the main window coming back to the
/// foreground runs it <see cref="RescanReason.Foreground"/>
/// (<see cref="ForegroundRescanRoute"/>).
/// </summary>
/// <remarks>
/// <para>
/// One run, in order: resume the Pending delta generation a failed run
/// left (its effects applied from its stored cursor, idempotently); rescan
/// on the session-load worker; reconcile the new generation's pages
/// removal-first (<see cref="ReconcileScanDelta"/>); release the ledger
/// into core's per-path net counts; refresh the sidebar, replace the Quick
/// Open list, notify the graph; then EXACTLY ONE completion sentence —
/// <c>VaultRescanFinished</c>, or <c>VaultRescanIncomplete</c> when the
/// walk was partial, a file or a page failed, or the scan call threw. The
/// progress listener's Started/Finished announcements never speak for a
/// rescan (the reason-aware progress policy).
/// </para>
/// <para>
/// One run at a time. A request that arrives while one runs joins the
/// pending-reason lattice (Explicit ⊔ Foreground = Explicit, Foreground ⊔
/// Foreground = Foreground) and is served by ONE follow-up run — never
/// dropped, and never subject to the foreground cooldown, which applies
/// only to STARTING a run from idle.
/// </para>
/// </remarks>
internal sealed partial class VaultLifecycleViewModel
{
    /// <summary>The foreground route's cooldown: a foreground request that
    /// would START a run within this long of the last scan's end is
    /// skipped. A request during a running rescan is never subject to it.</summary>
    internal static readonly TimeSpan ForegroundRescanCooldown = TimeSpan.FromSeconds(5);

    /// <summary>Delta entries per page: small enough to apply in one UI
    /// turn, large enough that an ordinary refresh is one page.</summary>
    internal const uint DefaultScanDeltaPageLimit = 256;

    private bool _rescanActive;
    private RescanReason? _pendingRescanReason;
    private DateTimeOffset? _lastScanEndedAt;
    private Task _rescanCompletion = Task.CompletedTask;

    /// <summary>The running rescan (and its coalesced follow-ups), for the
    /// facts to await.</summary>
    internal Task RescanCompletion => _rescanCompletion;

    internal bool IsRescanActive => _rescanActive;

    /// <summary>The open session — for the facts that read core's ledger.</summary>
    internal VaultSession? SessionForTests => _session;

    /// <summary>
    /// Run a rescan of the open vault for <paramref name="reason"/>.
    /// Refused — silently, nothing to reconcile yet — with no vault or a
    /// closing one, while the initial open scan runs, or while an import or
    /// a trash operation is in flight; joined to the running rescan's
    /// pending-reason lattice when one is running; skipped for a foreground
    /// request inside the cooldown. Never faults.
    /// </summary>
    internal Task RescanAsync(RescanReason reason) => RequestRescan(reason, honorCooldown: true);

    /// <summary>The coalescer. <paramref name="honorCooldown"/> is false only
    /// for a pending request that an import or trash deferred: it was
    /// recorded while a rescan ran, and the cooldown never applies to a
    /// recorded request.</summary>
    private Task RequestRescan(RescanReason reason, bool honorCooldown)
    {
        if (_session is not VaultSession session || !IsVaultOpen || Workspace is null)
        {
            return Task.CompletedTask;
        }

        if (_rescanActive)
        {
            // Joins the lattice BEFORE any refusal: a request during a
            // running rescan is never dropped (R-9), and the cooldown does
            // not apply to recording it.
            _pendingRescanReason = JoinRescanReasons(_pendingRescanReason, reason);
            return _rescanCompletion;
        }

        if (RescanIsBlocked())
        {
            return Task.CompletedTask;
        }

        if (honorCooldown
            && reason == RescanReason.Foreground
            && _lastScanEndedAt is DateTimeOffset ended
            && _scanClock() - ended < ForegroundRescanCooldown)
        {
            return Task.CompletedTask;
        }

        _rescanActive = true;
        _rescanCompletion = RunRescansAsync(_generation, session, reason);
        return _rescanCompletion;
    }

    /// <summary>The pending-reason lattice's join: Explicit dominates.</summary>
    internal static RescanReason JoinRescanReasons(RescanReason? pending, RescanReason arriving) =>
        pending == RescanReason.Explicit || arriving == RescanReason.Explicit
            ? RescanReason.Explicit
            : RescanReason.Foreground;

    /// <summary>The initial open scan, an import or a trash operation in
    /// flight — the only blockers (a running rescan is joined, not
    /// refused).</summary>
    private bool RescanIsBlocked() =>
        IsBusy
        || FileSidebar?.IsImporting == true
        || FileSidebar?.IsTrashing == true;

    private async Task RunRescansAsync(int generation, VaultSession session, RescanReason reason)
    {
        try
        {
            RescanReason current = reason;
            while (true)
            {
                await RunOneRescanAsync(generation, session, current);
                if (generation != _generation
                    || _pendingRescanReason is not RescanReason next
                    || RescanIsBlocked())
                {
                    // A follow-up blocked by an import or trash that began
                    // meanwhile stays pending and runs when they settle
                    // (FileSidebar_RescanBlockersChanged).
                    return;
                }

                _pendingRescanReason = null;
                current = next;
            }
        }
        catch (Exception exception)
        {
            HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
        }
        finally
        {
            if (generation == _generation)
            {
                _rescanActive = false;
                _lastScanEndedAt = _scanClock();
            }
        }
    }

    private async Task RunOneRescanAsync(int generation, VaultSession session, RescanReason reason)
    {
        // ONE cancel token for the whole run: the resume, the scan and every
        // page read take it (locked decision 05). A close or a vault switch
        // cancels it (CloseSession, which then owns and disposes it); a
        // cancelled run stops silently wherever it is, its generation Pending
        // at its cursor for a later rescan of the same session (a closed
        // session's generation dies with its connection).
        var cancel = new CancelToken();
        _scanCancel = cancel;
        // Quick Open's replacement is read on the worker after the scan; the
        // journal opened here re-bases it on every Slate-owned write whose
        // event is handled meanwhile (QuickSwitcherViewModel.ReplaceFiles).
        QuickSwitcherViewModel? quickOpen = QuickSwitcher;
        quickOpen?.BeginReplacementJournal();
        try
        {
            await RunOneRescanAsync(generation, session, reason, _scanDeltaChannel(session), cancel);
        }
        finally
        {
            quickOpen?.EndReplacementJournal();
            if (ReferenceEquals(_scanCancel, cancel))
            {
                _scanCancel = null;
                cancel.Dispose();
            }
        }
    }

    private async Task RunOneRescanAsync(
        int generation,
        VaultSession session,
        RescanReason reason,
        IScanDeltaChannel channel,
        CancelToken cancel)
    {
        // (1) A Pending generation a failed or cancelled run left: its
        // remaining pages' effects first, from its stored cursor — core
        // refuses a new scan over it, so an unreconciled tail is never
        // overwritten.
        switch (ReconcileScanDelta(generation, channel, cancel))
        {
            case DeltaReconciliation.Cancelled:
                return;
            case DeltaReconciliation.Failed:
                if (generation == _generation)
                {
                    PostRescanIncomplete(1);
                }

                return;
        }

        // (2) The scan, on the session-load worker. The listener only moves
        // the progress bar of an explicit refresh; nothing it hears speaks.
        UiProgressListener? progress = reason == RescanReason.Explicit
            ? new UiProgressListener(_enqueueUi, @event => HandleRescanProgress(generation, @event))
            : null;
        Task<(ScanReport Report, SwitcherFile[] SwitcherFiles)> scan = _runSessionLoad(() =>
        {
            ScanReport report = progress is null
                ? session.Rescan(cancel)
                : session.RescanWithProgress(cancel, progress);
            return (report, LoadSwitcherFiles(session));
        });
        _sessionLoadCompletion = scan;
        (ScanReport Report, SwitcherFile[] SwitcherFiles) loaded;
        try
        {
            loaded = await scan;
        }
        catch (VaultException.Cancelled)
        {
            // A close or a vault switch: nothing to say.
            return;
        }
        catch (Exception exception)
        {
            if (generation == _generation)
            {
                // The call threw before any report existed: an honest
                // "results may be incomplete", never silence and never
                // "No changes".
                HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
                PostRescanIncomplete(1);
            }

            return;
        }
        finally
        {
            if (generation == _generation)
            {
                if (_sessionLoadCompletion.IsCompleted)
                {
                    _sessionLoadCompletion = Task.CompletedTask;
                }

                IsProgressIndeterminate = false;
            }
        }

        if (generation != _generation)
        {
            return;
        }

        // (3) The new generation's effects, then (4) the release: core
        // reduces the retained Applied generation — this run's, coalesced
        // with any recovered one — into the net per-path counts.
        ScanDeltaOutcome? outcome = null;
        switch (ReconcileScanDelta(generation, channel, cancel))
        {
            case DeltaReconciliation.Cancelled:
                return;
            case DeltaReconciliation.Complete:
                try
                {
                    outcome = channel.Release();
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
                }

                break;
        }

        if (generation != _generation)
        {
            return;
        }

        // (5) The index truth the scan committed, whatever the pages did. A
        // surface that faults here is logged, never allowed to swallow the
        // run's one sentence below.
        try
        {
            FileSidebar?.Refresh(reportCount: reason == RescanReason.Explicit);
            QuickSwitcher?.ReplaceFiles(loaded.SwitcherFiles);
            Workspace?.NotifyGraphOfVaultChange();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
        }

        // (6) The one sentence.
        int errors = loaded.Report.Errors.Length;
        if (outcome is not ScanDeltaOutcome released)
        {
            PostRescanIncomplete(errors + 1);
        }
        else if (!loaded.Report.Complete)
        {
            PostRescanIncomplete(Math.Max(1, errors));
        }
        else if (reason == RescanReason.Explicit || released.Changed + released.Removed > 0)
        {
            PostRescanOutcome(new A11yEvent.VaultRescanFinished(
                reason, released.Changed, released.Removed));
        }
    }

    /// <summary>How a walk of the Pending generation ended.</summary>
    private enum DeltaReconciliation
    {
        /// <summary>Every page applied (or nothing was pending).</summary>
        Complete,

        /// <summary>A page failed: the generation keeps its cursor and the
        /// run says "results may be incomplete".</summary>
        Failed,

        /// <summary>The run's token was cancelled, or the vault closed or
        /// switched: the generation keeps its cursor, nothing is said.</summary>
        Cancelled,
    }

    /// <summary>
    /// Walk the Pending delta generation from its stored cursor in page
    /// order — core pages it removal-first, so a case-only rename's removal
    /// marks its tab missing before the creation re-seats it, in one bounded
    /// pass with no deferral — applying, path by path, the operations the
    /// file-change funnel (<see cref="HandleFileChange"/>) applies, silently
    /// (<see cref="WorkspaceViewModel.BeginSilentReconciliation"/>), and
    /// reporting each page applied only AFTER its effects. The last page's
    /// report marks the generation Applied. On failure or cancellation the
    /// generation keeps its cursor and the next rescan resumes it.
    /// </summary>
    /// <remarks>
    /// The linearization with Slate-owned writes (round 24): every page is
    /// RE-CHECKED against core's ledger in the very turn that applies it,
    /// with no await between the check and the effects. Slate-owned events
    /// are handled on this thread, so none can be handled in between: a
    /// write whose event the host already handled has superseded its rows
    /// by the check (its own transaction flagged them) and they are skipped;
    /// a write that commits after the check reaches the host after the
    /// page's effects, and its own event reconciles over them (a Created
    /// re-seats the tab a stale Deleted marked missing).
    /// </remarks>
    private DeltaReconciliation ReconcileScanDelta(
        int generation, IScanDeltaChannel channel, CancelToken cancel)
    {
        ScanDeltaPending? pending;
        try
        {
            pending = channel.Pending();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
            return DeltaReconciliation.Failed;
        }

        if (pending is null)
        {
            return DeltaReconciliation.Complete;
        }

        string? cursor = pending.Cursor;
        using IDisposable? silence = Workspace?.BeginSilentReconciliation();
        while (generation == _generation && !cancel.IsCancelled())
        {
            ScanDeltaPage page;
            try
            {
                ScanDeltaPage fetched = channel.ReadPage(
                    pending.Generation, cursor, _scanDeltaPageLimit, cancel);
                page = RecheckScanDeltaPage(channel, pending.Generation, cursor, fetched, cancel);
                ApplyScanDeltaPage(page);
                channel.PageApplied(pending.Generation, page.NextCursor);
            }
            catch (VaultException.Cancelled)
            {
                return DeltaReconciliation.Cancelled;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
                return DeltaReconciliation.Failed;
            }

            if (page.NextCursor is not string next)
            {
                return DeltaReconciliation.Complete;
            }

            cursor = next;
        }

        return DeltaReconciliation.Cancelled;
    }

    /// <summary>
    /// The round-24 re-check: the fetched page re-read, synchronously, in
    /// the turn that applies it — its rows' superseded flags as they stand
    /// NOW. A generation is immutable but for those flags, so any other
    /// difference is a broken ledger: the page fails closed.
    /// </summary>
    private ScanDeltaPage RecheckScanDeltaPage(
        IScanDeltaChannel channel,
        ulong generation,
        string? cursor,
        ScanDeltaPage fetched,
        CancelToken cancel)
    {
        ScanDeltaPage rechecked = channel.RecheckPage(generation, cursor, _scanDeltaPageLimit, cancel);
        bool same = rechecked.Entries.Length == fetched.Entries.Length
            && string.Equals(rechecked.NextCursor, fetched.NextCursor, StringComparison.Ordinal)
            && rechecked.Entries.Zip(fetched.Entries).All(pair =>
                pair.First.Kind == pair.Second.Kind
                && string.Equals(pair.First.Path, pair.Second.Path, StringComparison.Ordinal));
        return same
            ? rechecked
            : throw new InvalidOperationException(
                "A scan delta page changed between its read and its re-check.");
    }

    /// <summary>
    /// One page's effects: the operations <see cref="HandleFileChange"/>
    /// applies for the matching event kind (an external rename is Deleted +
    /// Created, AR-8), plus the clean-tab reload the funnel's Modified arm
    /// never had — for every entry no Slate-owned write has superseded (that
    /// write's own event reconciled the host). Idempotent, so a resumed page
    /// may be applied twice.
    /// </summary>
    /// <remarks>
    /// Batched per page, in the page's own removal-first order, so the cost
    /// is linear in the delta: the removals invalidate their tabs in one
    /// sweep with one workspace persist (the funnel's per-event
    /// <c>InvalidatePath</c> writes the workspace file once per call), and
    /// the missing-tab re-seat — a sweep over every missing tab — runs once
    /// per page, after that page's removals, when it created anything.
    /// Quick Open is not updated per entry: the run replaces its whole list
    /// from the index afterwards (<see cref="QuickSwitcherViewModel.ReplaceFiles"/>),
    /// which subsumes the funnel's per-event <c>ApplyFileChange</c> — an
    /// O(files) pass per entry that would make a large delta quadratic.
    /// </remarks>
    private void ApplyScanDeltaPage(ScanDeltaPage page)
    {
        WorkspaceViewModel? workspace = Workspace;
        ScanDeltaEntry[] live = [.. page.Entries.Where(entry => !entry.Superseded)];
        var removed = new List<string>();
        var modified = new List<string>();
        bool created = false;
        foreach (ScanDeltaEntry entry in live)
        {
            switch (entry.Kind)
            {
                case ScanDeltaKind.Removed:
                    removed.Add(entry.Path);
                    break;
                case ScanDeltaKind.Created:
                    created = true;
                    break;
                case ScanDeltaKind.Modified:
                    modified.Add(entry.Path);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(page), entry.Kind, "unknown scan delta kind");
            }
        }

        // Removals before creations: a missing tab's file back under another
        // spelling (`ghost.md` → `Ghost.md`) is re-seated only once its
        // removal has marked the tab missing.
        workspace?.InvalidatePaths(removed);
        if (created)
        {
            workspace?.ReseatMissingTabs();
        }

        foreach (string path in modified)
        {
            workspace?.ReloadCleanTab(path);
            workspace?.NotifyHistoryOfVaultChange(path);
        }

        foreach (ScanDeltaEntry entry in live)
        {
            FileChangeKind kind = entry.Kind switch
            {
                ScanDeltaKind.Removed => FileChangeKind.Deleted,
                ScanDeltaKind.Created => FileChangeKind.Created,
                _ => FileChangeKind.Modified,
            };
            workspace?.NotifyReadingOfVaultChange(kind, entry.Path);
            workspace?.NotifyBasesOfVaultChange(entry.Path);
        }

        if (live.Length > 0)
        {
            workspace?.InvalidateAllInteractionStates();
        }
    }

    /// <summary>The rescan's progress policy: an explicit refresh moves the
    /// progress bar; nothing it hears speaks or rewrites the status line —
    /// the run's one completion sentence does.</summary>
    private void HandleRescanProgress(int generation, ScanProgress @event)
    {
        if (generation != _generation)
        {
            return;
        }

        switch (@event)
        {
            case ScanProgress.Started started:
                ProgressMaximum = Math.Max(1, started.TotalFiles);
                ProgressValue = 0;
                IsProgressIndeterminate = started.TotalFiles == 0;
                break;
            case ScanProgress.FileIndexed indexed:
                ProgressMaximum = Math.Max(1, indexed.Total);
                ProgressValue = Math.Min(indexed.Indexed, ProgressMaximum);
                IsProgressIndeterminate = false;
                break;
            case ScanProgress.Finished finished:
                ProgressMaximum = Math.Max(1, finished.Report.FilesSeen);
                ProgressValue = ProgressMaximum;
                IsProgressIndeterminate = false;
                break;
            default:
                IsProgressIndeterminate = false;
                break;
        }
    }

    /// <summary>The open scan's status line (OD-6): both counts, the
    /// hash-authoritative "new or changed" one included — the read count is
    /// never shown.</summary>
    internal static string ScanFinishedStatus(ScanReport report) =>
        $"Scan finished: {report.FilesSeen} files, {report.FilesChanged} new or changed.";

    private void PostRescanIncomplete(int errors) =>
        PostRescanOutcome(new A11yEvent.VaultRescanIncomplete((ulong)Math.Max(1, errors)));

    /// <summary>The status line shows what was spoken — core's rendering,
    /// the <c>RemoveRecentVault</c> shape.</summary>
    private void PostRescanOutcome(A11yEvent outcome)
    {
        StatusText = SlateUniffiMethods.A11yRender(outcome).Text;
        _announce(outcome);
    }

    /// <summary>A follow-up that an import or trash blocked runs when they
    /// settle — the lattice never drops a request.</summary>
    private void FileSidebar_RescanBlockersChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (sender is not FilesSidebarViewModel sidebar
            || !ReferenceEquals(sidebar, FileSidebar)
            || eventArgs.PropertyName is not (nameof(FilesSidebarViewModel.IsImporting)
                or nameof(FilesSidebarViewModel.IsTrashing))
            || _rescanActive
            || _pendingRescanReason is not RescanReason pending
            || RescanIsBlocked())
        {
            return;
        }

        _pendingRescanReason = null;
        _ = RequestRescan(pending, honorCooldown: false);
    }

    /// <summary>Teardown: a closing vault forgets its rescans (their
    /// continuations are generation-guarded; the scan task is drained with
    /// the session load).</summary>
    private void ResetRescanState()
    {
        _rescanActive = false;
        _pendingRescanReason = null;
        _lastScanEndedAt = null;
        _rescanCompletion = Task.CompletedTask;
    }
}
