// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using SlateWindows.Commands;
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
/// One run, in order (AR-18's fallback): rescan through the rescan core
/// seam; re-synchronize the host's surfaces from the index; then EXACTLY
/// ONE completion sentence — <c>VaultRescanFinished</c> from this scan's
/// counts, or <c>VaultRescanIncomplete</c> when the walk was partial, a
/// file or a re-sync operation failed, or the scan call threw. Nothing
/// is retained between runs. The
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

    private bool _rescanActive;
    private RescanReason? _pendingRescanReason;
    private DateTimeOffset? _lastScanEndedAt;
    private Task _rescanCompletion = Task.CompletedTask;

    // The running rescan's cancel token (CloseSession cancels it through
    // the seam and then owns it).
    private CancelToken? _rescanCancel;

    // Its managed twin (F5): CloseSession cancels it with the native one;
    // host-side checks read it, so none makes an FFI call on the UI thread.
    private CancellationTokenSource? _rescanCancellation;

    /// <summary>The running rescan (and its coalesced follow-ups), for the
    /// facts to await.</summary>
    internal Task RescanCompletion => _rescanCompletion;

    internal bool IsRescanActive => _rescanActive;

    /// <summary>The open session — for the facts that write through it.</summary>
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
    private bool RescanIsBlocked() => RescanBlockedReason() is not null;

    /// <summary>The blockers, as the reason a refused request gives: an
    /// import or a trash operation says what to wait for; the initial open
    /// scan (the sidebar exists only at its very end) the generic
    /// reason.</summary>
    private string? RescanBlockedReason() =>
        FileSidebar?.IsImporting == true || FileSidebar?.IsTrashing == true
            ? SlateCommandRegistrar.StructuralMutationBusyReason
            : IsBusy
                ? SlateCommandRegistrar.UnavailableReason
                : null;

    /// <summary>
    /// W7-7 PR 7 (#1252; codex PR 7 round 1, finding 11): why a rescan
    /// request would be refused right now, or null — the Refresh command's
    /// availability, so the palette never lists it as available while
    /// <see cref="RequestRescan"/> would silently refuse it. The same
    /// refusals, in the same order: no vault, then the blockers.
    /// </summary>
    internal string? RescanUnavailableReason() =>
        _session is null || !IsVaultOpen || Workspace is null
            ? SlateCommandRegistrar.NoVaultReason
            : RescanBlockedReason();

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
        // page read take it (locked decision 05). It is made, cancelled and
        // disposed through the rescan core seam, like every other rescan core
        // call. A close or a vault switch cancels it (CloseSession, which
        // then owns and disposes it); a cancelled run stops silently wherever
        // it is; nothing is retained, so the next rescan starts afresh.
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
        var cancellation = new CancellationTokenSource();
        _rescanCancellation = cancellation;
        try
        {
            await RunOneRescanAsync(generation, session, reason, cancel, cancellation.Token);
        }
        finally
        {
            if (ReferenceEquals(_rescanCancellation, cancellation))
            {
                _rescanCancellation = null;
            }

            cancellation.Dispose();
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
        CancelToken cancel,
        CancellationToken cancellation)
    {
        // (1) The scan, through the seam. The listener only moves the
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

        // (2) Re-synchronize the host from the index (AR-18's fallback):
        // every re-sync operation is awaited to its publication, and each
        // that fails counts one error in the sentence.
        ulong failedOperations = await ReSyncFromIndexAsync(generation, reason, cancellation);
        if (generation != _generation || cancellation.IsCancellationRequested)
        {
            return;
        }

        // (3) The one sentence. The error COUNT crosses the FFI in full; its
        // messages only as samples (rounds 25-26).
        if (!report.Complete || failedOperations > 0)
        {
            ulong errors = report.ErrorCount > ulong.MaxValue - failedOperations
                ? ulong.MaxValue
                : report.ErrorCount + failedOperations;
            PostRescanIncomplete(Math.Max(1UL, errors));
        }
        else if (reason == RescanReason.Explicit || report.FilesChanged + report.FilesRemoved > 0)
        {
            PostRescanOutcome(new A11yEvent.VaultRescanFinished(
                reason, report.FilesChanged, report.FilesRemoved));
        }
    }

    /// <summary>
    /// AR-18's fallback: re-synchronize the host's surfaces from the index
    /// after a scan, every one awaited to its publication, and return how
    /// many operations failed (each counts in the sentence). The files tree
    /// is wired; the rest of the re-sync follows contract R-9's design.
    /// </summary>
    private async Task<ulong> ReSyncFromIndexAsync(
        int generation,
        RescanReason reason,
        CancellationToken cancellation)
    {
        ulong failed = 0;
        try
        {
            // The run's managed token reaches the tree read and its tag tree
            // (F5): a cancel before or after the native TagTree call skips or
            // discards the result.
            await (FileSidebar?.RefreshAsync(
                    reportCount: reason == RescanReason.Explicit,
                    cancellation: cancellation)
                ?? Task.CompletedTask);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A close or a vault switch: nothing to count or say.
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (generation == _generation)
            {
                HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
                failed++;
            }
        }

        return failed;
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
    }
}
