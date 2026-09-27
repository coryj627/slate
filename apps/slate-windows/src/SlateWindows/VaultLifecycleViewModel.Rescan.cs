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
        ulong failedOperations = await ReSyncFromIndexAsync(generation, reason, session, cancel, cancellation);
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
    /// AR-18's fallback (contract R-9): re-synchronize every host surface
    /// from the index after the scan — whoever indexed a change (this scan,
    /// an earlier one, another Slate window or slate-cli), it is shown — and
    /// return how many operations failed (each counts one error in the
    /// sentence). Every operation is awaited to its publication, and none
    /// speaks: the files tree (silent, <see cref="FilesSidebarViewModel.RefreshForRescanAsync"/>),
    /// Quick Open (the index's openable documents, Slate writes replayed),
    /// every open document (hash-compared, hash-conditioned reloads), then
    /// the dependents once. Every core call runs through the rescan seam
    /// with the run's native token; host-side checks read its managed twin.
    /// A cancelled run (a close, a vault switch) stops wherever it is.
    /// </summary>
    private async Task<ulong> ReSyncFromIndexAsync(
        int generation,
        RescanReason reason,
        VaultSession session,
        CancelToken cancel,
        CancellationToken cancellation)
    {
        Task<ulong> tree = ReSyncTreeAsync(reason, cancellation);
        Task<ulong> quickOpen = ReSyncQuickOpenAsync(session, cancel, cancellation);
        ulong failed = 0;
        if (Workspace is WorkspaceViewModel workspace)
        {
            failed += await ReSyncWorkspaceAsync(workspace, session, cancel, cancellation);
        }

        failed += await tree;
        failed += await quickOpen;
        return generation == _generation ? failed : 0;
    }

    /// <summary>The files tree, silently: its root-list and tag-tree
    /// failures come back counted, never spoken.</summary>
    private async Task<ulong> ReSyncTreeAsync(RescanReason reason, CancellationToken cancellation)
    {
        if (FileSidebar is not FilesSidebarViewModel sidebar)
        {
            return 0;
        }

        try
        {
            return await sidebar.RefreshForRescanAsync(
                reportCount: reason == RescanReason.Explicit,
                cancellation);
        }
        catch (OperationCanceledException)
        {
            // Cancelled with nothing left to publish: a close, a vault
            // switch, a shutdown — the run says nothing.
            return 0;
        }
    }

    /// <summary>
    /// Quick Open (v2 §7): the index's openable documents, read in pages on
    /// the seam with the run's token (<c>ListFiles</c> honours it), replace
    /// its list — the Slate-owned changes applied meanwhile replayed onto
    /// the pages — and an open switcher re-ranks silently, awaited to that
    /// publication.
    /// </summary>
    private async Task<ulong> ReSyncQuickOpenAsync(
        VaultSession session,
        CancelToken cancel,
        CancellationToken cancellation)
    {
        if (QuickSwitcher is not QuickSwitcherViewModel switcher)
        {
            return 0;
        }

        QuickSwitcherViewModel.ReplacementJournal journal = switcher.BeginReplacement();
        try
        {
            SwitcherFile[] files = await RunRescanCoreAsync(
                "list",
                () => LoadSwitcherFiles(session, cancel, null));
            if (cancellation.IsCancellationRequested || !ReferenceEquals(switcher, QuickSwitcher))
            {
                return 0;
            }

            await switcher.ReplaceFilesAsync(files, journal, cancellation);
            return 0;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return 0;
        }
        catch (VaultException.Cancelled) when (cancellation.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
            return 1;
        }
        finally
        {
            switcher.EndReplacement(journal);
        }
    }

    /// <summary>
    /// The workspace: the index hashes of every document it shows, read in
    /// chunks of at most <c>MaxIndexedHashPaths()</c> under the one token
    /// (v2 §5), then the documents re-sync and the dependents follow.
    /// </summary>
    private async Task<ulong> ReSyncWorkspaceAsync(
        WorkspaceViewModel workspace,
        VaultSession session,
        CancelToken cancel,
        CancellationToken cancellation)
    {
        IReadOnlyList<string> paths = workspace.RescanPathsToHash();
        IReadOnlyDictionary<string, WorkspaceViewModel.IndexedPath> indexed;
        try
        {
            indexed = await RunRescanCoreAsync("hashes", () => ReadIndexedPaths(session, paths, cancel));
        }
        catch (VaultException.Cancelled) when (cancellation.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Without the hashes no document can be compared: one failure.
            HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
            return 1;
        }

        if (cancellation.IsCancellationRequested || !ReferenceEquals(workspace, Workspace))
        {
            return 0;
        }

        try
        {
            WorkspaceViewModel.RescanDocumentsOutcome documents = await workspace.ReSyncOpenDocumentsAsync(
                indexed,
                path => ReadForRescanAsync(session, path, cancel),
                cancellation);
            ulong dependents = await workspace.ReSyncDependentsAsync(documents, indexed, cancellation);
            return documents.Failed + dependents;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return 0;
        }
    }

    /// <summary>The index hashes of <paramref name="paths"/>, chunked to
    /// core's bound (v2 §5) — ON THE WORKER. A path with no index row is
    /// also checked on disk, so an unindexed file that exists is never
    /// marked missing.</summary>
    private IReadOnlyDictionary<string, WorkspaceViewModel.IndexedPath> ReadIndexedPaths(
        VaultSession session,
        IReadOnlyList<string> paths,
        CancelToken cancel)
    {
        var indexed = new Dictionary<string, WorkspaceViewModel.IndexedPath>(StringComparer.Ordinal);
        int chunk = (int)Math.Max(1U, IndexedHashChunkForTests ?? SlateUniffiMethods.MaxIndexedHashPaths());
        for (int start = 0; start < paths.Count; start += chunk)
        {
            string[] slice = [.. paths.Skip(start).Take(chunk)];
            IReadOnlyList<string?> hashes = session.IndexedContentHashes(slice, cancel);
            for (int index = 0; index < slice.Length; index++)
            {
                string? hash = hashes[index];
                bool onDisk = hash is not null || OnDiskUnderThisSpelling(session, slice[index]);
                indexed[slice[index]] = new WorkspaceViewModel.IndexedPath(hash, onDisk);
            }
        }

        return indexed;
    }

    /// <summary>Test seam (v2 §5): a smaller hash chunk than core's bound,
    /// so a fact proves the chunking without a thousand tabs as well.</summary>
    internal uint? IndexedHashChunkForTests { get; set; }

    /// <summary>Whether an entry exists at the vault-relative
    /// <paramref name="path"/> UNDER THIS SPELLING — asked on the worker,
    /// only for a path the index has no row for. Core's canonical path is
    /// the spelling the filesystem stores (#1077): a case-only rename
    /// outside Slate (<c>ghost.md</c> → <c>Ghost.md</c>) leaves the old
    /// spelling "not on disk" even on a case-insensitive volume, so its tab
    /// is marked missing and re-seated on the stored spelling.</summary>
    private static bool OnDiskUnderThisSpelling(VaultSession session, string path)
    {
        try
        {
            return string.Equals(session.CanonicalPath(path), path, StringComparison.Ordinal);
        }
        catch (VaultException exception) when (exception is not VaultException.Cancelled)
        {
            // Unknown: never mark missing on a guess (contract I7).
            return true;
        }
    }

    /// <summary>A Markdown path's re-read for the re-sync — on the seam, off
    /// the dispatcher, refused once the run is cancelled: its text and that
    /// text's content hash (the index's hash function), or null when the
    /// file cannot be read now (gone since the scan, locked).</summary>
    private Task<WorkspaceViewModel.RescanRead?> ReadForRescanAsync(
        VaultSession session,
        string path,
        CancelToken cancel) =>
        RunRescanCoreAsync<WorkspaceViewModel.RescanRead?>("read", () =>
        {
            if (cancel.IsCancelled())
            {
                throw new VaultException.Cancelled();
            }

            try
            {
                string text = session.ReadText(path);
                return new WorkspaceViewModel.RescanRead(text, SlateUniffiMethods.EditorTextContentHash(text));
            }
            catch (VaultException exception) when (exception is not VaultException.Cancelled)
            {
                return null;
            }
        });

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
