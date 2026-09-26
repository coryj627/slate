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
/// removal-first — Quick Open's changes among them
/// (<see cref="ReconcileScanDeltaAsync"/>); release the ledger into core's
/// per-path net counts; refresh the sidebar, notify the graph; then
/// EXACTLY ONE completion sentence —
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

    // Rounds 25-26: the Slate-owned write journal (NoteSlateOwnedWrite) —
    // written by the session listener on the writer's thread and by the
    // funnel on this one, kept only while a rescan runs.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> _slateOwnedWriteEpochs =
        new(StringComparer.Ordinal);
    private long _slateOwnedWriteEpoch;
    private volatile bool _slateOwnedWriteJournalActive;

    // The running rescan's cancel token (CloseSession cancels it through
    // the seam and then owns it).
    private CancelToken? _rescanCancel;

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
        _slateOwnedWriteJournalActive = true;
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
                _slateOwnedWriteJournalActive = false;
                _slateOwnedWriteEpochs.Clear();
            }
        }
    }

    private async Task RunOneRescanAsync(int generation, VaultSession session, RescanReason reason)
    {
        // ONE cancel token for the whole run: the resume, the scan and every
        // page read take it (locked decision 05). It is made, cancelled and
        // disposed through the rescan core seam, like every other rescan core
        // call. A close or a vault switch cancels it (CloseSession, which
        // then owns and disposes it); a cancelled run stops silently wherever
        // it is, its generation Pending at its cursor for a later rescan of
        // the same session (a closed session's generation dies with its
        // connection).
        CancelToken cancel;
        try
        {
            cancel = await RunRescanCoreAsync("token", () => new CancelToken(), trackForClose: false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
            if (generation == _generation)
            {
                PostRescanIncomplete(1);
            }

            return;
        }

        if (generation != _generation)
        {
            await DisposeRescanTokenAsync(cancel);
            return;
        }

        _rescanCancel = cancel;
        try
        {
            await RunOneRescanAsync(generation, session, reason, _scanDeltaChannel(session), cancel);
        }
        finally
        {
            if (ReferenceEquals(_rescanCancel, cancel))
            {
                _rescanCancel = null;
                await DisposeRescanTokenAsync(cancel);
            }
        }
    }

    private async Task DisposeRescanTokenAsync(CancelToken cancel)
    {
        try
        {
            _ = await RunRescanCoreAsync(
                "dispose",
                () =>
                {
                    cancel.Dispose();
                    return true;
                },
                trackForClose: false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
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
        switch (await ReconcileScanDeltaAsync(generation, channel, cancel))
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

        // (2) The scan, through the seam. The listener only moves the
        // progress bar of an explicit refresh; nothing it hears speaks.
        UiProgressListener? progress = reason == RescanReason.Explicit
            ? new UiProgressListener(_enqueueUi, @event => HandleRescanProgress(generation, @event))
            : null;
        Task<ScanReport> scan = StartRescanCoreCall("scan", () => progress is null
            ? session.Rescan(cancel)
            : session.RescanWithProgress(cancel, progress));
        _sessionLoadCompletion = scan;
        ScanReport report;
        try
        {
            report = await scan;
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

        // (3) The new generation's effects — Quick Open's among them: the
        // delta is its only rescan path — then (4) the release: core reduces
        // the retained Applied generation (this run's, coalesced with any
        // recovered one) into the net per-path counts.
        ScanDeltaOutcome? outcome = null;
        switch (await ReconcileScanDeltaAsync(generation, channel, cancel))
        {
            case DeltaReconciliation.Cancelled:
                return;
            case DeltaReconciliation.Complete:
                try
                {
                    outcome = await RunRescanCoreAsync("release", channel.Release);
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

        // (5) The tree re-reads the index the scan committed, whatever the
        // pages did (the graph probed with each page's effects). A surface
        // that faults here is logged, never allowed to swallow the run's
        // one sentence below.
        try
        {
            FileSidebar?.Refresh(reportCount: reason == RescanReason.Explicit);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
        }

        // (6) The one sentence. The error COUNT crosses the FFI in full; its
        // messages only as samples (rounds 25-26).
        if (outcome is not ScanDeltaOutcome released)
        {
            PostRescanIncomplete(report.ErrorCount == ulong.MaxValue ? ulong.MaxValue : report.ErrorCount + 1);
        }
        else if (!report.Complete)
        {
            PostRescanIncomplete(Math.Max(1UL, report.ErrorCount));
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
    /// <para>
    /// Every ledger call runs through the rescan core seam, off the
    /// dispatcher, cancellable (locked decision 05 §4.1); only a page's
    /// effects run here, in one turn.
    /// </para>
    /// <para>
    /// The linearization with Slate-owned writes (rounds 25-26): the host
    /// journals a new epoch for a path whenever it learns a Slate-owned
    /// write to it committed (<see cref="NoteSlateOwnedWrite"/>) — at the
    /// commit itself, where the session's listener runs inside the write
    /// and so before a synchronous write like Save returns or an
    /// asynchronous one's completion resumes, and again when its event is
    /// handled — and captures the epoch as it issues each page read. A row
    /// whose path's epoch is newer than the capture is skipped: that write
    /// already reconciled the path. A write committed before the read
    /// arrives superseded (core's flag); one whose event is handled after
    /// the page's effects reconciles over them.
    /// </para>
    /// </remarks>
    private async Task<DeltaReconciliation> ReconcileScanDeltaAsync(
        int generation, IScanDeltaChannel channel, CancelToken cancel)
    {
        ScanDeltaPending? pending;
        try
        {
            pending = await RunRescanCoreAsync("pending", channel.Pending);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
            return DeltaReconciliation.Failed;
        }

        if (generation != _generation)
        {
            return DeltaReconciliation.Cancelled;
        }

        if (pending is null)
        {
            return DeltaReconciliation.Complete;
        }

        string? cursor = pending.Cursor;
        while (generation == _generation)
        {
            long capturedEpoch = Interlocked.Read(ref _slateOwnedWriteEpoch);
            string? pageCursor = cursor;
            ScanDeltaPage page;
            try
            {
                page = await RunRescanCoreAsync(
                    "page",
                    () => channel.ReadPage(pending.Generation, pageCursor, _scanDeltaPageLimit, cancel));
                if (generation != _generation)
                {
                    return DeltaReconciliation.Cancelled;
                }

                // Every row's reload is awaited — published, not merely
                // scheduled — before the page is reported applied.
                IDisposable? silence = Workspace?.BeginSilentReconciliation();
                try
                {
                    await ApplyScanDeltaPageAsync(page, capturedEpoch, cancel);
                }
                finally
                {
                    silence?.Dispose();
                }

                if (generation != _generation)
                {
                    return DeltaReconciliation.Cancelled;
                }

                _ = await RunRescanCoreAsync(
                    "applied",
                    () =>
                    {
                        channel.PageApplied(pending.Generation, page.NextCursor);
                        return true;
                    });
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
    /// Start one rescan core call through the seam — refused if the seam
    /// would run it on the UI thread (locked decision 05 §4.1).
    /// </summary>
    private Task<T> StartRescanCoreCall<T>(string operation, Func<T> call)
    {
        int uiThread = _uiThreadId;
        return _rescanWorker.Run(operation, () =>
            Environment.CurrentManagedThreadId == uiThread
                ? throw new InvalidOperationException(
                    $"The rescan core call '{operation}' was entered on the UI thread.")
                : call());
    }

    /// <summary>One rescan core call through the seam, awaited. A tracked
    /// call stands in the session-load completion, so a close waits for it
    /// before the session goes away.</summary>
    private async Task<T> RunRescanCoreAsync<T>(string operation, Func<T> call, bool trackForClose = true)
    {
        Task<T> task = StartRescanCoreCall(operation, call);
        if (!trackForClose)
        {
            return await task;
        }

        _sessionLoadCompletion = task;
        try
        {
            return await task;
        }
        finally
        {
            if (ReferenceEquals(_sessionLoadCompletion, task))
            {
                _sessionLoadCompletion = Task.CompletedTask;
            }
        }
    }

    /// <summary>
    /// Rounds 25-26: journal a Slate-owned write to <paramref name="path"/> —
    /// a new epoch — while a rescan runs. Called from any thread: by the
    /// session listener INSIDE the write (at its commit, before a synchronous
    /// write returns or an asynchronous one's completion resumes) and by
    /// <see cref="HandleFileChange"/> when its event is handled. A delta
    /// page read before the epoch skips that path's row.
    /// </summary>
    private void NoteSlateOwnedWrite(string path)
    {
        if (_slateOwnedWriteJournalActive)
        {
            _slateOwnedWriteEpochs[path] = Interlocked.Increment(ref _slateOwnedWriteEpoch);
        }
    }

    private void NoteSlateOwnedWrite(FileChangeEvent @event)
    {
        NoteSlateOwnedWrite(@event.Path);
        if (@event.PreviousPath is string movedFrom)
        {
            NoteSlateOwnedWrite(movedFrom);
        }
    }

    private bool ReconciledSince(string path, long capturedEpoch) =>
        _slateOwnedWriteEpochs.TryGetValue(path, out long epoch) && epoch > capturedEpoch;

    /// <summary>
    /// One page's effects, through the routine a Slate-owned event also
    /// goes through (<see cref="ApplyFileChangeEffectsAsync"/>), for every
    /// row no Slate-owned write has reconciled: not superseded in core,
    /// not journaled since <paramref name="capturedEpoch"/> — also
    /// re-checked when a reload's worker read comes back. The Task
    /// completes when every reload has published; idempotent, so a
    /// resumed page may be applied twice. Each row carries core's own
    /// openable classification (round 26), and Quick Open takes the page's
    /// changes as its only rescan path (round 25).
    /// </summary>
    private Task ApplyScanDeltaPageAsync(ScanDeltaPage page, long capturedEpoch, CancelToken cancel)
    {
        var changes = new List<(FileChangeEvent Change, bool Openable)>();
        foreach (ScanDeltaEntry entry in page.Entries)
        {
            if (entry.Superseded || ReconciledSince(entry.Path, capturedEpoch))
            {
                continue;
            }

            FileChangeKind kind = entry.Kind switch
            {
                ScanDeltaKind.Removed => FileChangeKind.Deleted,
                ScanDeltaKind.Created => FileChangeKind.Created,
                ScanDeltaKind.Modified => FileChangeKind.Modified,
                _ => throw new ArgumentOutOfRangeException(nameof(page), entry.Kind, "unknown scan delta kind"),
            };
            changes.Add((new FileChangeEvent(kind, entry.Path, null), entry.Openable));
        }

        return ApplyFileChangeEffectsAsync(
            changes,
            FileChangeOrigin.Rescan,
            path => ReconciledSince(path, capturedEpoch),
            cancel);
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

    private void PostRescanIncomplete(ulong errors) =>
        PostRescanOutcome(new A11yEvent.VaultRescanIncomplete(Math.Max(1UL, errors)));

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
        _slateOwnedWriteJournalActive = false;
        _slateOwnedWriteEpochs.Clear();
    }
}
