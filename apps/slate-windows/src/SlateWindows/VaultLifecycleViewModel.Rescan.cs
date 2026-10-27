// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 7 (#1252, contract R-9): the rescan — the reconciliation a file
/// created, changed or deleted outside Slate gets on Windows. Files Sidebar →
/// Refresh runs it <see cref="RescanReason.Explicit"/>; the main window coming
/// back to the foreground runs it <see cref="RescanReason.Foreground"/>
/// (<see cref="ForegroundRescanRoute"/>); and since plan 43 PR 20b (#1267)
/// core's vault watcher asks for it <see cref="RescanReason.Watcher"/>, once
/// per coalesced burst of changes made outside this session
/// (<see cref="HandleExternalChange"/>).
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
/// pending-reason lattice, Explicit &gt; Foreground &gt; Watcher (Explicit ⊔
/// anything = Explicit, Foreground ⊔ Foreground = Foreground ⊔ Watcher =
/// Foreground, Watcher ⊔ Watcher = Watcher), and is served by ONE follow-up
/// run — never dropped, and never subject to the foreground cooldown, which
/// applies only to STARTING a foreground run from idle.
/// </para>
/// <para>
/// Plan 43 PR 20b (design §4.2 item 3, §4.5): a Watcher request is never
/// dropped. Before the workspace exists (the open's own scan and listing) and
/// while an import or a trash operation runs it is recorded in the lattice,
/// and the end of the open and the blockers settling serve it; while the
/// window is away it waits for the next activation, where it joins the
/// foreground route's request (one run, Foreground reason, so the return
/// still says what changed while the user was away) or, when the route
/// forwards none, runs on its own. Core already spaced it, so the cooldown
/// never applies. Its sentence is the foreground policy's, gated: at most one
/// per <c>watcher_sentence_spacing_ms()</c> per open vault, and none while a
/// modal surface is open — a run inside the window posts nothing, and its
/// counts are not carried to a later one.
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

    // Plan 43 PR 20b (#1267, design §4.5): when this vault's last watcher
    // sentence was posted, and a watcher request the window's absence
    // deferred until its next activation.
    private DateTimeOffset? _lastWatcherSentenceAt;
    private bool _watcherRequestWhileAway;

    // The running rescan's cancel token (CloseSession cancels it through
    // the seam and then owns it).
    private CancelToken? _rescanCancel;

    // Its managed twin (F5): CloseSession cancels it with the native one;
    // host-side checks read it, so none makes an FFI call on the UI thread.
    private CancellationTokenSource? _rescanCancellation;

    // W7-7 PR 7 (codex AR-18 review round 2, finding 3): every rescan core
    // call that touches the session is admitted into ONE synchronized set —
    // the re-sync runs several at once (Quick Open's listing beside the
    // workspace's hash read, say). CloseSession closes the admission,
    // cancels the run and drains the set to EMPTY before any native state
    // is disposed.
    private readonly object _rescanCoreGate = new();
    private readonly HashSet<Task> _rescanCoreCalls = [];
    private bool _rescanCoreAdmissionClosed;

    /// <summary>The running rescan (and its coalesced follow-ups), for the
    /// facts to await.</summary>
    internal Task RescanCompletion => _rescanCompletion;

    internal bool IsRescanActive => _rescanActive;

    /// <summary>Plan 43 PR 20b (design §4.5 item 2): at most one watcher
    /// sentence per this long per open vault — core's one number
    /// (<c>watcher_sentence_spacing_ms</c>), so both hosts agree.</summary>
    internal static TimeSpan WatcherSentenceSpacing { get; } =
        TimeSpan.FromMilliseconds(SlateUniffiMethods.WatcherSentenceSpacingMs());

    /// <summary>Plan 43 PR 20b (§4.5 item 4): whether the main window is
    /// active. <c>MainWindow</c> installs <c>Window.IsActive</c>; null (a
    /// headless fact, no window) reads active.</summary>
    internal Func<bool>? WindowIsActive { get; set; }

    /// <summary>Plan 43 PR 20b (§4.5 item 3): whether a modal surface is
    /// open. <c>MainWindow</c> installs the foreground route's predicate;
    /// null reads none. No watcher sentence is posted while one is.</summary>
    internal Func<bool>? ModalSurfaceOpen { get; set; }

    private bool IsWindowActive => WindowIsActive?.Invoke() ?? true;

    /// <summary>
    /// Run a rescan of the open vault for <paramref name="reason"/>.
    /// Refused — silently, nothing to reconcile yet — with no vault or a
    /// closing one, while the initial open scan runs, or while an import or
    /// a trash operation is in flight; joined to the running rescan's
    /// pending-reason lattice when one is running; skipped for a foreground
    /// request inside the cooldown. A Watcher request is recorded where the
    /// others are refused (plan 43 PR 20b). Never faults.
    /// </summary>
    internal Task RescanAsync(RescanReason reason) => RequestRescan(reason, honorCooldown: true);

    /// <summary>The coalescer. <paramref name="honorCooldown"/> is false for
    /// a recorded request — one an import or trash deferred, one the end of
    /// the open or an activation serves — and for every Watcher request: the
    /// cooldown never applies to a recorded request, and core already spaced
    /// a watcher's (design §4.2 item 3).</summary>
    private Task RequestRescan(RescanReason reason, bool honorCooldown)
    {
        // WB-9 (#1319): the open's scan runs before IsVaultOpen turns true,
        // and a watcher burst during it is still recorded and served.
        if (_session is not VaultSession session || (!IsVaultOpen && !IsOpening))
        {
            return Task.CompletedTask;
        }

        // Plan 43 PR 20b (§4.5 item 4): a watcher request the window's
        // absence deferred joins the foreground route's request on
        // activation — one run, Foreground reason — and carries the watcher
        // request's guarantees: never dropped, never subject to the cooldown.
        bool carriesWatcher = reason == RescanReason.Watcher;
        if (reason == RescanReason.Foreground && _watcherRequestWhileAway)
        {
            _watcherRequestWhileAway = false;
            carriesWatcher = true;
            honorCooldown = false;
        }

        if (DeferWatcherWhileAway(reason))
        {
            return Task.CompletedTask;
        }

        if (Workspace is null)
        {
            // Before the workspace exists — the open's own scan and listing
            // — a watcher request is recorded, and the end of the open
            // serves it (ServeRecordedRescan).
            RecordWatcherRequest(reason, carriesWatcher);
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
            // An import or a trash operation in flight: a watcher request
            // waits for it in the lattice (FileSidebar_RescanBlockersChanged
            // serves it); Explicit and Foreground are refused, as before.
            RecordWatcherRequest(reason, carriesWatcher);
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
        FileSidebar?.RaiseStructuralMutationAvailabilityChanged();
        _rescanCompletion = RunRescansAsync(_generation, session, reason);
        return _rescanCompletion;
    }

    /// <summary>The pending-reason lattice's join, Explicit &gt; Foreground
    /// &gt; Watcher (plan 43 PR 20b, design §4.2 item 2): Explicit dominates,
    /// Foreground with Watcher is Foreground, Watcher with Watcher stays
    /// Watcher.</summary>
    internal static RescanReason JoinRescanReasons(RescanReason? pending, RescanReason arriving) =>
        pending == RescanReason.Explicit || arriving == RescanReason.Explicit
            ? RescanReason.Explicit
            : pending == RescanReason.Foreground || arriving == RescanReason.Foreground
                ? RescanReason.Foreground
                : RescanReason.Watcher;

    /// <summary>Plan 43 PR 20b (§4.5 item 4): no Watcher run starts while the
    /// window is away. The request waits for the next activation
    /// (<see cref="ServeWatcherRequestOnActivation"/>), where it joins the
    /// foreground route's request. True when it was deferred.</summary>
    private bool DeferWatcherWhileAway(RescanReason reason)
    {
        if (reason != RescanReason.Watcher || IsWindowActive)
        {
            return false;
        }

        _watcherRequestWhileAway = true;
        return true;
    }

    /// <summary>Plan 43 PR 20b (design §4.2 item 3): a request that carries
    /// a watcher request is recorded in the lattice, never refused.</summary>
    private void RecordWatcherRequest(RescanReason reason, bool carriesWatcher)
    {
        if (carriesWatcher)
        {
            _pendingRescanReason = JoinRescanReasons(_pendingRescanReason, reason);
        }
    }

    /// <summary>
    /// Plan 43 PR 20b (#1267): core's vault watcher saw a burst of changes
    /// made outside this session — one <c>OnExternalChange</c> per coalesced
    /// burst, marshalled here by the listener. It asks for a Watcher rescan:
    /// run now when the window is active; recorded while it is away (the
    /// next activation serves it, joined into the foreground route's
    /// request), before the workspace exists or while an import or a trash
    /// operation runs; joined to a running rescan's lattice — never dropped.
    /// A signal from a closed vault's session is ignored.
    /// </summary>
    private void HandleExternalChange(int generation)
    {
        if (generation != _generation)
        {
            return;
        }

        _ = RequestRescan(RescanReason.Watcher, honorCooldown: false);
    }

    /// <summary>
    /// Plan 43 PR 20b (§4.5 item 4): the main window was activated
    /// (<see cref="ForegroundRescanRoute.OnActivated"/>, after its own
    /// decision). A watcher request the window's absence deferred runs now —
    /// unless the foreground request the route forwarded first already took
    /// it (one run, Foreground reason). Returns the run, or null when nothing
    /// was deferred.
    /// </summary>
    internal Task? ServeWatcherRequestOnActivation()
    {
        if (!_watcherRequestWhileAway)
        {
            return null;
        }

        _watcherRequestWhileAway = false;
        return RequestRescan(RescanReason.Watcher, honorCooldown: false);
    }

    /// <summary>Plan 43 PR 20b: the end of the open serves a request the
    /// lattice recorded before the workspace existed — no blocker settles
    /// then, so nothing else would.</summary>
    private void ServeRecordedRescan()
    {
        if (_rescanActive || _pendingRescanReason is not RescanReason pending || RescanIsBlocked())
        {
            return;
        }

        _pendingRescanReason = null;
        _ = RequestRescan(pending, honorCooldown: false);
    }

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

    /// <summary>W7-7 PR 7 (codex PR 7 round 4, finding 2): why an import or a
    /// trash operation cannot START now — a running rescan — or null. R-9's
    /// concurrency rule holds in both orders: a rescan never starts during
    /// them, and they never start during a rescan.</summary>
    internal string? StructuralMutationBlockedReason() =>
        _rescanActive ? SlateCommandRegistrar.StructuralMutationBusyReason : null;

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
                if (DeferWatcherWhileAway(next))
                {
                    // Plan 43 PR 20b (§4.5 item 4): the window went away
                    // meanwhile; the Watcher follow-up waits for its return.
                    return;
                }

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
                FileSidebar?.RaiseStructuralMutationAvailabilityChanged();
            }
        }
    }

    private async Task RunOneRescanAsync(int generation, VaultSession session, RescanReason reason)
    {
        // Plan 43 PR 20b: which route ran, for the shell gate's journeys
        // (logged only under SLATE_UIA_DIAGNOSTICS=1; the reason's name is
        // the whole detail).
        HostLog.WriteUiAutomationDiagnostic(HostDiagnosticEvent.VaultRescanStarted, reason.ToString());

        // ONE cancel token for the whole run: the resume, the scan and every
        // page read take it (locked decision 05). It is made, cancelled and
        // disposed through the rescan core seam, like every other rescan core
        // call. A close or a vault switch cancels it (CloseSession, which
        // then owns and disposes it); a cancelled run stops silently wherever
        // it is; nothing is retained, so the next rescan starts afresh.
        CancelToken cancel;
        try
        {
            cancel = await StartRescanCoreCall("token", () => new CancelToken());
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
            if (generation == _generation)
            {
                PostRescanIncomplete(reason, 1);
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
            _ = await StartRescanCoreCall(
                "dispose",
                () =>
                {
                    cancel.Dispose();
                    return true;
                });
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
        Task<ScanReport> scan = RunRescanCoreAsync(
            "scan",
            () => progress is null
                ? session.Rescan(cancel)
                : session.RescanWithProgress(cancel, progress),
            cancellation);
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
                PostRescanIncomplete(reason, 1);
            }

            return;
        }
        finally
        {
            if (generation == _generation)
            {
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
        // messages only as samples (rounds 25-26). A Watcher run speaks on
        // the foreground policy, through the watcher's gate (plan 43 PR 20b,
        // PostRescanOutcome).
        if (!report.Complete || failedOperations > 0)
        {
            ulong errors = report.ErrorCount > ulong.MaxValue - failedOperations
                ? ulong.MaxValue
                : report.ErrorCount + failedOperations;
            PostRescanIncomplete(reason, Math.Max(1UL, errors));
        }
        else if (reason == RescanReason.Explicit || report.FilesChanged + report.FilesRemoved > 0)
        {
            PostRescanOutcome(reason, new A11yEvent.VaultRescanFinished(
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
        Task<ulong> workspace = Workspace is WorkspaceViewModel open
            ? ReSyncWorkspaceAsync(open, session, cancel, cancellation)
            : Task.FromResult(0UL);

        // Codex PR 7 round 3, finding 4: every branch is awaited — one that
        // fails unexpectedly counts one error and never leaves a sibling
        // unawaited or the run without its sentence.
        ulong failed = await ContainedAsync(workspace, cancellation);
        failed += await ContainedAsync(tree, cancellation);
        failed += await ContainedAsync(quickOpen, cancellation);
        return generation == _generation ? failed : 0;
    }

    /// <summary>One re-sync branch's failures, counted: its own count, or —
    /// when it throws anything but the run's cancellation — one error,
    /// logged by type.</summary>
    private static async Task<ulong> ContainedAsync(Task<ulong> branch, CancellationToken cancellation)
    {
        try
        {
            return await branch;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (cancellation.IsCancellationRequested)
            {
                return 0;
            }

            HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
            return 1;
        }
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
                () => LoadSwitcherFiles(session, cancel, SwitcherPageLoadingForTests),
                cancellation);
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
            indexed = await RunRescanCoreAsync(
                "hashes",
                () => ReadIndexedPaths(session, paths, cancel),
                cancellation);
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
            // Codex PR 7 round 3, finding 4: each phase is contained — a
            // document re-sync that fails unexpectedly counts one error and
            // the dependents still run (over an empty outcome).
            WorkspaceViewModel.RescanDocumentsOutcome documents;
            ulong failed = 0;
            try
            {
                documents = await workspace.ReSyncOpenDocumentsAsync(
                    indexed,
                    path => ReadForRescanAsync(session, path, cancel, cancellation),
                    missing => ProbeReseatAsync(session, missing, cancel, cancellation),
                    cancellation);
                failed += documents.Failed;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                and not OperationCanceledException
                && !cancellation.IsCancellationRequested)
            {
                HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
                documents = WorkspaceViewModel.RescanDocumentsOutcome.Empty;
                failed++;
            }

            try
            {
                failed += await workspace.ReSyncDependentsAsync(documents, indexed, cancellation);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                and not OperationCanceledException
                && !cancellation.IsCancellationRequested)
            {
                HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
                failed++;
            }

            return failed;
        }
        catch (Exception) when (cancellation.IsCancellationRequested)
        {
            return 0;
        }
        finally
        {
            // Codex PR 7 round 3, finding 6: an uncancelled run's work is
            // kept at its fixed point until it ends; a cancelled run's is
            // drained by the workspace teardown.
            if (!cancellation.IsCancellationRequested)
            {
                workspace.CompleteRescanWork();
            }
        }
    }

    /// <summary>The index hashes of <paramref name="paths"/>, chunked to
    /// core's bound (v2 §5) — ON THE WORKER. A path with no index row is
    /// also checked on disk, so an unindexed file that exists is never
    /// marked missing; a check that FAILS keeps the path present and is
    /// counted (codex AR-18 review round 2, finding 7).</summary>
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
                if (hash is not null)
                {
                    indexed[slice[index]] = new WorkspaceViewModel.IndexedPath(hash, OnDisk: true);
                    continue;
                }

                SpellingOnDisk probe = ProbeSpellingOnDisk(session, slice[index]);
                indexed[slice[index]] = new WorkspaceViewModel.IndexedPath(
                    null,
                    OnDisk: probe != SpellingOnDisk.Absent,
                    ProbeFailed: probe == SpellingOnDisk.Failed);
            }
        }

        return indexed;
    }

    /// <summary>Test seam (W7-7 PR 7 round 3): replaces core's canonical-path
    /// probe for the re-sync, so a fact can make it fail.</summary>
    internal Func<string, string?>? CanonicalPathForTests { get; set; }

    /// <summary>Test seam (v2 §5): a smaller hash chunk than core's bound,
    /// so a fact proves the chunking without a thousand tabs as well.</summary>
    internal uint? IndexedHashChunkForTests { get; set; }

    /// <summary>What an entry at a vault-relative path UNDER THIS SPELLING
    /// is (codex AR-18 review round 2, finding 7: three states, never a
    /// guess).</summary>
    private enum SpellingOnDisk
    {
        Present,
        Absent,
        Failed,
    }

    /// <summary>Whether an entry exists at the vault-relative
    /// <paramref name="path"/> UNDER THIS SPELLING — asked on the worker,
    /// only for a path the index has no row for. Core's canonical path is
    /// the spelling the filesystem stores (#1077): a case-only rename
    /// outside Slate (<c>ghost.md</c> → <c>Ghost.md</c>) leaves the old
    /// spelling "not on disk" even on a case-insensitive volume, so its tab
    /// is marked missing and re-seated on the stored spelling. A probe that
    /// fails is <see cref="SpellingOnDisk.Failed"/>: the tab is kept, and the
    /// failure is counted.</summary>
    private SpellingOnDisk ProbeSpellingOnDisk(VaultSession session, string path)
    {
        string? stored;
        try
        {
            stored = StoredSpelling(session, path);
        }
        catch (VaultException exception) when (exception is not VaultException.Cancelled)
        {
            HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
            return SpellingOnDisk.Failed;
        }

        return string.Equals(stored, path, StringComparison.Ordinal)
            ? SpellingOnDisk.Present
            : SpellingOnDisk.Absent;
    }

    /// <summary>The spelling the filesystem stores for
    /// <paramref name="path"/>, or null (nothing there) — core's canonical
    /// path, or the fact's probe.</summary>
    private string? StoredSpelling(VaultSession session, string path) =>
        CanonicalPathForTests is { } probe ? probe(path) : session.CanonicalPath(path);

    /// <summary>
    /// W7-7 PR 7 (codex AR-18 review round 2, finding 1): the re-seat's
    /// probe of the missing tabs' paths — ON THE WORKER, through the seam,
    /// with the run's token: each path's stored spelling (a failed probe is
    /// marked failed), and for a Markdown path the index's hash of that
    /// spelling and a read of it. The dispatcher applies the result through
    /// the tabs' tickets (<c>ReseatMissingTabsAsync</c>).
    /// </summary>
    private Task<IReadOnlyDictionary<string, WorkspaceViewModel.ReseatProbe>> ProbeReseatAsync(
        VaultSession session,
        IReadOnlyList<(string Path, bool Markdown)> paths,
        CancelToken cancel,
        CancellationToken cancellation) =>
        RunRescanCoreAsync<IReadOnlyDictionary<string, WorkspaceViewModel.ReseatProbe>>(
            "reseat",
            () =>
            {
                var probes = new Dictionary<string, WorkspaceViewModel.ReseatProbe>(StringComparer.Ordinal);
                foreach ((string path, bool markdown) in paths)
                {
                    if (cancel.IsCancelled())
                    {
                        throw new VaultException.Cancelled();
                    }

                    string? stored;
                    try
                    {
                        stored = StoredSpelling(session, path);
                    }
                    catch (VaultException exception) when (exception is not VaultException.Cancelled)
                    {
                        HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
                        probes[path] = new WorkspaceViewModel.ReseatProbe(null, Failed: true, null, null);
                        continue;
                    }

                    if (stored is null || !markdown)
                    {
                        probes[path] = new WorkspaceViewModel.ReseatProbe(stored, Failed: false, null, null);
                        continue;
                    }

                    string? indexHash = session.IndexedContentHashes([stored], cancel)[0];
                    WorkspaceViewModel.RescanRead? read = null;
                    if (indexHash is not null)
                    {
                        try
                        {
                            string text = session.ReadText(stored);
                            read = new WorkspaceViewModel.RescanRead(
                                text,
                                SlateUniffiMethods.EditorTextContentHash(text));
                        }
                        catch (VaultException exception) when (exception is not VaultException.Cancelled)
                        {
                            // Unreadable now: the tab stays missing (counted).
                        }
                    }

                    probes[path] = new WorkspaceViewModel.ReseatProbe(stored, Failed: false, indexHash, read);
                }

                return probes;
            },
            cancellation);

    /// <summary>A Markdown path's re-read for the re-sync — on the seam, off
    /// the dispatcher, refused once the run is cancelled: its text and that
    /// text's content hash (the index's hash function), or null when the
    /// file cannot be read now (gone since the scan, locked).</summary>
    private Task<WorkspaceViewModel.RescanRead?> ReadForRescanAsync(
        VaultSession session,
        string path,
        CancelToken cancel,
        CancellationToken cancellation) =>
        RunRescanCoreAsync<WorkspaceViewModel.RescanRead?>(
            "read",
            () =>
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
            },
            cancellation);

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

    /// <summary>
    /// One session-touching rescan core call through the seam (codex AR-18
    /// review round 2, finding 3): ADMITTED only while the admission is open
    /// and the run is not cancelled — a refused call is
    /// <see cref="VaultException.Cancelled"/> — and tracked in the set a
    /// close drains, until it ends. Admission and registration are one
    /// step under the gate, so a close either refuses the call or waits
    /// for it.
    /// </summary>
    private Task<T> RunRescanCoreAsync<T>(string operation, Func<T> call, CancellationToken cancellation)
    {
        lock (_rescanCoreGate)
        {
            if (_rescanCoreAdmissionClosed || cancellation.IsCancellationRequested)
            {
                return Task.FromException<T>(new VaultException.Cancelled());
            }

            Task<T> task = StartRescanCoreCall(operation, call);
            _ = _rescanCoreCalls.Add(task);
            _ = task.ContinueWith(
                ended =>
                {
                    lock (_rescanCoreGate)
                    {
                        _ = _rescanCoreCalls.Remove(ended);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return task;
        }
    }

    /// <summary>Teardown, first (finding 3): no rescan core call is
    /// admitted after this.</summary>
    private void CloseRescanCoreAdmission()
    {
        lock (_rescanCoreGate)
        {
            _rescanCoreAdmissionClosed = true;
        }
    }

    /// <summary>Teardown (finding 3): wait until every admitted rescan core
    /// call has ended — the run's token is cancelled by then, and each call
    /// takes it or is a bounded read. Blocks only on pool work.</summary>
    private void DrainRescanCoreCalls()
    {
        while (true)
        {
            Task[] pending;
            lock (_rescanCoreGate)
            {
                pending = [.. _rescanCoreCalls.Where(call => !call.IsCompleted)];
            }

            if (pending.Length == 0)
            {
                return;
            }

            try
            {
                Task.WaitAll(pending);
            }
            catch (AggregateException)
            {
                // An ended call — cancelled or failed — is what the drain
                // waits for; its outcome is its awaiter's.
            }
        }
    }

    /// <summary>Teardown, last: the next session's rescans are admitted
    /// again (a cancelled run's late continuation is still refused by its
    /// own cancelled token).</summary>
    private void ReopenRescanCoreAdmission()
    {
        lock (_rescanCoreGate)
        {
            _rescanCoreAdmissionClosed = false;
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

    private void PostRescanIncomplete(RescanReason reason, ulong errors) =>
        PostRescanOutcome(reason, new A11yEvent.VaultRescanIncomplete(Math.Max(1UL, errors)));

    /// <summary>The status line shows what was spoken — core's rendering,
    /// the <c>RemoveRecentVault</c> shape. A Watcher run's sentence passes
    /// the watcher's gate first; one the gate holds back posts nothing, the
    /// status line included.</summary>
    private void PostRescanOutcome(RescanReason reason, A11yEvent outcome)
    {
        if (reason == RescanReason.Watcher && !AdmitWatcherSentence())
        {
            return;
        }

        StatusText = SlateUniffiMethods.A11yRender(outcome).Text;
        _announce(outcome);
    }

    /// <summary>
    /// Plan 43 PR 20b (design §4.5 items 2-3): a watcher sentence never
    /// floods a screen reader. None is posted while a modal surface is open,
    /// and at most one per <see cref="WatcherSentenceSpacing"/> per open
    /// vault: a run whose sentence would fall inside the window posts
    /// nothing, and its counts are not carried to a later one — the tree and
    /// Quick Open are current, and Refresh speaks its own scan's counts. A
    /// sentence the modal check holds back does not start the window.
    /// </summary>
    private bool AdmitWatcherSentence()
    {
        if (ModalSurfaceOpen?.Invoke() == true)
        {
            return false;
        }

        DateTimeOffset now = _scanClock();
        if (_lastWatcherSentenceAt is DateTimeOffset last && now - last < WatcherSentenceSpacing)
        {
            return false;
        }

        _lastWatcherSentenceAt = now;
        return true;
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
    /// the session load), its deferred watcher request and its watcher
    /// sentence window.</summary>
    private void ResetRescanState()
    {
        _rescanActive = false;
        _pendingRescanReason = null;
        _lastScanEndedAt = null;
        _rescanCompletion = Task.CompletedTask;
        _watcherRequestWhileAway = false;
        _lastWatcherSentenceAt = null;
    }
}
