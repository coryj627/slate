// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// Owns asynchronous tree refresh and snapshot-based bulk expansion, including
/// their shared generation boundary, cancellation, completion, and UI publish.
/// </summary>
internal sealed partial class FilesSidebarViewModel
{
    // When present, tree providers and projection run through _runTreeWorker,
    // which must not execute inline on this context. RootNodes, Tags, Status,
    // and announcements are published only after posting back to this context.
    // A null context is the explicit synchronous/headless fallback.
    private readonly SynchronizationContext? _treeUiContext;
    private readonly Func<Action, CancellationToken, Task> _runTreeWorker;
    private readonly HashSet<string> _expandedPaths;
    private readonly object _treeRefreshCancellationGate = new();
    private readonly object _bulkExpandCancellationGate = new();
    private readonly SemaphoreSlim _treeProviderLane = new(1, 1);
    private CancellationTokenSource? _treeRefreshCancellation;
    private CancellationTokenSource? _bulkExpandCancellation;
    private Task _treeRefreshCompletion = Task.CompletedTask;
    private Task _expandLoadedCompletion = Task.CompletedTask;
    private int _treeGeneration;
    /// <summary>The tree generation whose outcome — its publication, or
    /// its failure report — has been applied (W7-7, codex PR 2 round 2):
    /// a refresh still running after that has nothing left to
    /// overwrite.</summary>
    private int _settledTreeGeneration;
    private ObservableCollection<FileTreeNodeViewModel> _rootNodes = [];
    private bool _isExpandingLoaded;

    public ObservableCollection<FileTreeNodeViewModel> RootNodes
    {
        get => _rootNodes;
        private set => SetField(ref _rootNodes, value);
    }

    public IReadOnlySet<string> RestoredExpandedPaths => _expandedPaths;
    internal Task TreeRefreshCompletion => _treeRefreshCompletion;
    internal bool IsRefreshingTree => !_treeRefreshCompletion.IsCompleted;
    internal bool IsTreePublicationPending =>
        IsRefreshingTree && _settledTreeGeneration != _treeGeneration;
    internal Task ExpandLoadedCompletion => _expandLoadedCompletion;

    // W7-7 PR 7 (#1252, round 30): the awaited refreshes, each settled by
    // the publication of its generation or any later one, faulted by a
    // reported failure of one, cancelled when a cancellation leaves
    // nothing to publish (a close, a shutdown).
    // The result is whether the settling publication's tag tree failed.
    private readonly List<(int Generation, TaskCompletionSource<bool> Published)> _treeRefreshWaiters = [];

    /// <summary>
    /// W7-7 PR 7 (#1252, round 30): <see cref="Refresh"/> as the Task a
    /// rescan awaits with its last page, before the Applied mark and the
    /// release — it completes when the tree this request produced (or a
    /// later one) has PUBLISHED on the owner context, faults when the
    /// refresh reported its failure ("Could not load files."), and is
    /// cancelled when the refresh was cancelled with nothing left to
    /// publish.
    /// </summary>
    /// <remarks>W7-7 PR 7 (F5): the caller's <paramref name="cancellation"/>
    /// — a rescan run's — is linked into this refresh's own, so it reaches
    /// the tree read and the tag tree: checked before and after the native
    /// <c>TagTree</c> call, a cancel skips or discards the result, nothing
    /// publishes, and the returned Task is cancelled at once.</remarks>
    internal Task RefreshAsync(bool reportCount = false, CancellationToken cancellation = default) =>
        StartAwaitedRefresh(reportCount, cancellation, silent: false);

    /// <summary>
    /// W7-7 PR 7 (#1252, R-9; v2 §3, §6): the rescan's OWN tree refresh —
    /// <see cref="RefreshAsync"/> made silent, with a structured outcome.
    /// Nothing it hears speaks: a root-list failure ("Could not load
    /// files.") and a tag-tree failure ("Could not load tags: …") are shown
    /// on the sidebar's status line and returned as the number of failed
    /// operations (0, 1 or 2) for the rescan's one sentence to count. The
    /// run's token is linked in (F5); a cancellation throws.
    /// </summary>
    internal async Task<ulong> RefreshForRescanAsync(bool reportCount, CancellationToken cancellation)
    {
        try
        {
            bool tagTreeFailed = await StartAwaitedRefresh(reportCount, cancellation, silent: true);
            return tagTreeFailed ? 1UL : 0UL;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The root list failed: nothing published.
            return 1UL;
        }
    }

    private Task<bool> StartAwaitedRefresh(bool reportCount, CancellationToken cancellation, bool silent)
    {
        if (cancellation.IsCancellationRequested)
        {
            return Task.FromCanceled<bool>(cancellation);
        }

        var published = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        (int Generation, TaskCompletionSource<bool> Published) waiter = (_treeGeneration + 1, published);
        lock (_treeRefreshWaiters)
        {
            _treeRefreshWaiters.Add(waiter);
        }

        Refresh(reportCount, cancellation, silent);
        if (_treeGeneration < waiter.Generation)
        {
            // Refused before it began: the session is shutting down.
            lock (_treeRefreshWaiters)
            {
                _ = _treeRefreshWaiters.Remove(waiter);
            }

            _ = published.TrySetCanceled();
        }

        if (cancellation.CanBeCanceled)
        {
            CancellationTokenRegistration registration =
                cancellation.Register(() => published.TrySetCanceled(cancellation));
            _ = published.Task.ContinueWith(
                _ => registration.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        return published.Task;
    }

    /// <summary>Settle every awaited refresh up to
    /// <paramref name="generation"/>: published (no failure), failed, or
    /// cancelled.</summary>
    private void SettleTreeRefreshWaiters(
        int generation,
        Exception? failure,
        bool cancelled = false,
        bool tagTreeFailed = false)
    {
        List<(int Generation, TaskCompletionSource<bool> Published)> settled;
        lock (_treeRefreshWaiters)
        {
            settled = [.. _treeRefreshWaiters.Where(waiter => waiter.Generation <= generation)];
            _ = _treeRefreshWaiters.RemoveAll(waiter => waiter.Generation <= generation);
        }

        foreach ((_, TaskCompletionSource<bool> published) in settled)
        {
            _ = cancelled
                ? published.TrySetCanceled()
                : failure is null
                    ? published.TrySetResult(tagTreeFailed)
                    : published.TrySetException(failure);
        }
    }

    public bool IsExpandingLoaded
    {
        get => _isExpandingLoaded;
        private set => SetField(ref _isExpandingLoaded, value);
    }

    public IReadOnlyList<string> ExpandedDirectoryPaths() =>
        Flatten(RootNodes)
            .Where(node => node.IsDirectory && node.IsExpanded)
            .Select(node => node.Path)
            .ToArray();

    public void Refresh(bool reportCount = false) => Refresh(reportCount, CancellationToken.None, silent: false);

    /// <summary>The refresh, with a caller's cancellation linked into its
    /// own (W7-7 PR 7, F5): a cancelled caller publishes nothing.
    /// <paramref name="silent"/> — the rescan's own refresh — shows its
    /// failures on the status line without speaking them.</summary>
    private void Refresh(bool reportCount, CancellationToken external, bool silent)
    {
        if (SessionShutdownStarted)
        {
            return;
        }

        CancelBulkExpansion();
        CancelChildExpansions();
        CancelTreeRefreshCore();
        if (RootNodes.Count > 0)
        {
            string[] liveExpansions = ExpandedDirectoryPaths().ToArray();
            _expandedPaths.Clear();
            _expandedPaths.UnionWith(liveExpansions);
        }

        int generation = ++_treeGeneration;
        DirectoryOrdering ordering = CaptureDirectoryOrdering();
        var expandedPaths = _expandedPaths.ToHashSet(StringComparer.Ordinal);
        int tagGeneration = _tagGeneration;
        if (_treeUiContext is null)
        {
            if (!TryBeginSessionWork(out SessionWorkLease? lease))
            {
                SettleTreeRefreshWaiters(generation, failure: null, cancelled: true);
                return;
            }

            try
            {
                TreeRefreshOutcome outcome;
                using (lease)
                {
                    outcome = BuildTreeRefresh(
                        ordering,
                        expandedPaths,
                        tagGeneration,
                        external);
                }

                ApplyTreeRefresh(outcome, reportCount, silent);
                _treeRefreshCompletion = Task.CompletedTask;
                SettleTreeRefreshWaiters(generation, failure: null, tagTreeFailed: outcome.Tags.Error is not null);
            }
            catch (OperationCanceledException) when (external.IsCancellationRequested)
            {
                // The caller was cancelled (a closing rescan): nothing
                // publishes and nothing is reported.
                SettleTreeRefreshWaiters(generation, failure: null, cancelled: true);
                _treeRefreshCompletion = Task.CompletedTask;
            }
            catch (VaultException exception)
            {
                SettleTreeRefreshWaiters(generation, exception);
                ReportTreeFailure($"Could not load files: {exception.Message}", silent);
                _treeRefreshCompletion = Task.CompletedTask;
            }
            catch (Exception exception)
            {
                HostLog.Write(HostDiagnosticEvent.SidebarTreeRefreshFailed, exception);
                SettleTreeRefreshWaiters(generation, exception);
                try
                {
                    ReportTreeFailure("Could not load files.", silent);
                }
                catch (Exception callbackException)
                {
                    HostLog.Write(HostDiagnosticEvent.SidebarTreeRefreshFailed, callbackException);
                }

                _treeRefreshCompletion = Task.CompletedTask;
            }

            return;
        }

        CancellationTokenSource cancellation = external.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(external)
            : new CancellationTokenSource();
        CancellationToken token = cancellation.Token;
        lock (_treeRefreshCancellationGate)
        {
            _treeRefreshCancellation = cancellation;
        }

        Task previous = _treeRefreshCompletion;
        Status = "Loading files…";
        _treeRefreshCompletion = RefreshTreeAsync(
            previous,
            generation,
            ordering,
            expandedPaths,
            tagGeneration,
            reportCount,
            silent,
            cancellation,
            token);
    }

    /// <summary>A tree failure's line: spoken, or — for the rescan's own
    /// refresh — shown on the status line only (the rescan counts it).</summary>
    private void ReportTreeFailure(string message, bool silent)
    {
        if (silent)
        {
            Status = message;
            HoldStatusForPendingPublication();
            return;
        }

        ReportFailure(message);
    }

    private async Task RefreshTreeAsync(
        Task previous,
        int generation,
        DirectoryOrdering ordering,
        IReadOnlySet<string> expandedPaths,
        int tagGeneration,
        bool reportCount,
        bool silent,
        CancellationTokenSource cancellation,
        CancellationToken token)
    {
        // Publish the completion task before provider work can begin. Shutdown
        // can then cancel and observe a single coherent operation boundary.
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        try
        {
            try
            {
                await previous.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // A prior request is terminal even when its UI callback
                // faulted. Keep the newest refresh moving and retain a
                // privacy-safe diagnostic for the unexpected fault.
                HostLog.Write(HostDiagnosticEvent.SidebarTreeRefreshFailed, exception);
            }

            token.ThrowIfCancellationRequested();
            TreeRefreshOutcome? outcome = null;
            bool admitted = await RunAdmittedTreeWorkerAsync(
                () => outcome = BuildTreeRefresh(
                    ordering,
                    expandedPaths,
                    tagGeneration,
                    token),
                token).ConfigureAwait(false);
            if (!admitted)
            {
                return;
            }

            token.ThrowIfCancellationRequested();
            if (outcome is null)
            {
                throw new InvalidOperationException("Tree worker completed without an outcome.");
            }

            var applied = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _treeUiContext!.Post(
                _ =>
                {
                    try
                    {
                        if (!token.IsCancellationRequested && generation == _treeGeneration)
                        {
                            ApplyTreeRefresh(outcome, reportCount, silent);
                            SettleTreeRefreshWaiters(
                                generation,
                                failure: null,
                                tagTreeFailed: outcome.Tags.Error is not null);
                        }

                        applied.TrySetResult();
                    }
                    catch (Exception exception)
                    {
                        applied.TrySetException(exception);
                    }
                },
                null);
            try
            {
                await applied.Task.WaitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (VaultException exception)
        {
            await ReportTreeRefreshFailureAsync(
                generation,
                $"Could not load files: {exception.Message}",
                exception,
                silent,
                token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            HostLog.Write(HostDiagnosticEvent.SidebarTreeRefreshFailed, exception);
            await ReportTreeRefreshFailureAsync(
                generation,
                "Could not load files.",
                exception,
                silent,
                token).ConfigureAwait(false);
        }
        finally
        {
            bool ownsCancellation = false;
            lock (_treeRefreshCancellationGate)
            {
                if (ReferenceEquals(_treeRefreshCancellation, cancellation))
                {
                    _treeRefreshCancellation = null;
                    ownsCancellation = true;
                }
            }

            if (ownsCancellation)
            {
                cancellation.Dispose();
            }
        }
    }

    private async Task ReportTreeRefreshFailureAsync(
        int generation,
        string message,
        Exception cause,
        bool silent,
        CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return;
        }

        var applied = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _treeUiContext!.Post(
                _ =>
                {
                    try
                    {
                        if (generation == _treeGeneration)
                        {
                            // Settled before the report: this generation will
                            // never publish, so its failure is not held for a
                            // publication — a later refresh must not revive it.
                            _settledTreeGeneration = generation;
                            SettleTreeRefreshWaiters(generation, cause);
                            ReportTreeFailure(message, silent);
                        }

                        applied.TrySetResult();
                    }
                    catch (Exception exception)
                    {
                        applied.TrySetException(exception);
                    }
                },
                null);
            await applied.Task.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            // Refresh is terminal even when dispatch or presentation fails.
            // Teardown joins this task, so retain diagnostics without faulting it.
            HostLog.Write(HostDiagnosticEvent.SidebarTreeRefreshFailed, exception);
            SettleTreeRefreshWaiters(generation, cause);
        }
    }

    private TreeRefreshOutcome BuildTreeRefresh(
        DirectoryOrdering ordering,
        IReadOnlySet<string> expandedPaths,
        int tagGeneration,
        CancellationToken cancellationToken)
    {
        DirectoryLevel level = LoadDirectoryLevel(
            string.Empty,
            1,
            ordering: ordering,
            cancellationToken: cancellationToken);
        string? restoredOverflowPath = RestoreExpansionsForRefresh(
            level.Nodes,
            expandedPaths,
            ordering,
            cancellationToken);
        TagLoadOutcome tags = BuildTags(cancellationToken);
        var rootNodes = new ObservableCollection<FileTreeNodeViewModel>(level.Nodes);
        foreach (FileTreeNodeViewModel node in rootNodes)
        {
            node.AttachToTree(rootNodes);
        }

        return new TreeRefreshOutcome(
            rootNodes,
            level,
            restoredOverflowPath,
            tags,
            tagGeneration);
    }

    private string? RestoreExpansionsForRefresh(
        IEnumerable<FileTreeNodeViewModel> nodes,
        IReadOnlySet<string> expandedPaths,
        DirectoryOrdering ordering,
        CancellationToken cancellationToken)
    {
        string? overflowPath = null;
        foreach (FileTreeNodeViewModel node in nodes.Where(node => node.IsDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!expandedPaths.Contains(node.Path))
            {
                continue;
            }

            node.MarkExpandedWithoutLoading();
            if (node.Children.Count > 0 && node.Children[0].IsPlaceholder)
            {
                DirectoryLevel childLevel = LoadDirectoryLevel(
                    node.Path,
                    node.Level + 1,
                    ordering: ordering,
                    cancellationToken: cancellationToken);
                node.ReplaceChildren(childLevel.Nodes);
                if (childLevel.Truncated)
                {
                    overflowPath ??= node.Path;
                }
            }

            string? descendantOverflowPath = RestoreExpansionsForRefresh(
                node.Children,
                expandedPaths,
                ordering,
                cancellationToken);
            overflowPath ??= descendantOverflowPath;
        }

        return overflowPath;
    }

    private void ApplyTreeRefresh(TreeRefreshOutcome outcome, bool reportCount, bool silent = false)
    {
        _settledTreeGeneration = _treeGeneration;
        RootNodes = outcome.RootNodes;
        if (outcome.TagGeneration == _tagGeneration)
        {
            ApplyTags(outcome.Tags, announce: !silent);
        }

        ScheduleFilter(automatic: true);
        // W5-4 F1/F2: a create staged an inline-rename hand-off; the
        // published tree is the first moment the new node exists to
        // select. Other mutations stage a plain selection/focus
        // reconciliation (red team, a11y 2).
        ConsumePendingRenameArm();
        ConsumePendingSelection();
        // W6-2 PR A (contract A-8): a surface's "Reveal in File Tree" whose
        // node the previous tree had not materialised.
        ConsumePendingSurfaceSelection();
        ReconcileSelectionAfterPublication();

        // Project the AUTHORITATIVE checked set onto the published
        // nodes (codex rounds 4-5): a fresh node whose path is
        // checked re-checks silently; an entry is pruned only when
        // its ABSENCE is provable (the parent's real children are
        // materialized — or the root — and the path is not among
        // them). A checked descendant inside a COLLAPSED folder is
        // merely unmaterialized and survives. A truncated publication
        // proves nothing and prunes nothing.
        if (_batchChecked.Count > 0)
        {
            var materialized = new Dictionary<string, FileTreeNodeViewModel>(
                StringComparer.Ordinal);
            foreach (FileTreeNodeViewModel node in Flatten(RootNodes))
            {
                materialized[node.Path] = node;
            }

            // Absence pruning only from a COMPLETE publication (codex
            // round 6): any truncation — the root level or a restored
            // folder's overflow — makes the listing non-authoritative
            // for what does NOT exist.
            bool publicationComplete = !outcome.Level.Truncated
                && outcome.RestoredOverflowPath is null;
            foreach (string path in _batchChecked.Keys.ToList())
            {
                if (materialized.TryGetValue(path, out FileTreeNodeViewModel? node))
                {
                    _ = node.MarkBatchSelectedSilently();
                    continue;
                }

                if (!publicationComplete)
                {
                    continue;
                }

                string parent = ParentPath(path);
                bool absenceProvable = parent.Length == 0
                    || (materialized.TryGetValue(
                            parent, out FileTreeNodeViewModel? parentNode)
                        && parentNode.HasLoadedChildren);
                if (absenceProvable)
                {
                    _batchChecked.Remove(path);
                }
            }
        }

        BatchSelectionCount = _batchChecked.Count;
        if (outcome.Level.Truncated)
        {
            Status = DirectoryOverflowStatus(string.Empty);
        }
        else if (reportCount || _settingsNotice is not null)
        {
            Status = $"{outcome.Level.MaterializedCount:N0} top-level items."
                + (_settingsNotice is null ? string.Empty : $" {_settingsNotice}");
        }
        else if (outcome.RestoredOverflowPath is string overflowPath)
        {
            Status = DirectoryOverflowStatus(overflowPath);
        }

        // W7-7 PR 7 (v2 §6): a silent refresh's tag-tree failure is not
        // spoken — so it stays on the status line over this publication's
        // count, where it is seen; the rescan counts it.
        if (silent && outcome.TagGeneration == _tagGeneration && outcome.Tags.Error is string tagError)
        {
            Status = tagError;
        }

        // The mutation result wins the turn over this publication's
        // own status arms (codex round 2) — a persistent condition
        // (overflow, settings notice) returns on the next organic
        // refresh.
        ReassertStatusAfterPublication();
    }

    internal bool CancelTreeRefresh()
    {
        bool wasPending = IsRefreshingTree;
        if (wasPending)
        {
            CancelBulkExpansion();
            CancelChildExpansions();
            ++_treeGeneration;
        }

        CancelTreeRefreshCore();
        SettleTreeRefreshWaiters(int.MaxValue, failure: null, cancelled: true);

        return wasPending;
    }

    internal Task CancelTreeRefreshAndGetCompletion()
    {
        CancelTreeRefresh();
        return TreeRefreshCompletion;
    }

    private void CancelTreeRefreshCore()
    {
        CancellationTokenSource? cancellation;
        lock (_treeRefreshCancellationGate)
        {
            cancellation = _treeRefreshCancellation;
            _treeRefreshCancellation = null;
        }

        CancelAndDisposeWithoutThrowing(
            cancellation,
            HostDiagnosticEvent.SidebarTreeRefreshShutdownFailed);
    }

    private void CollapseAll()
    {
        CancelBulkExpansion();
        CancelChildExpansions();
        foreach (FileTreeNodeViewModel node in Flatten(RootNodes).Where(node => node.IsDirectory))
        {
            node.IsExpanded = false;
        }
    }

    private async Task ExpandLoadedAsync()
    {
        CancelBulkExpansion();
        CancelChildExpansions();
        var cancellation = new CancellationTokenSource();
        CancellationToken token = cancellation.Token;
        if (SessionShutdownStarted)
        {
            cancellation.Dispose();
            return;
        }

        lock (_bulkExpandCancellationGate)
        {
            _bulkExpandCancellation = cancellation;
        }

        Task treeRefresh = _treeRefreshCompletion;
        IsExpandingLoaded = true;
        try
        {
            await treeRefresh.WaitAsync(token);
            token.ThrowIfCancellationRequested();
            int generation = _treeGeneration;
            DirectoryOrdering ordering = CaptureDirectoryOrdering();
            FileTreeNodeViewModel[] materializedDirectories = Flatten(RootNodes)
                .Where(node => node.IsDirectory)
                .ToArray();
            foreach (FileTreeNodeViewModel node in materializedDirectories)
            {
                token.ThrowIfCancellationRequested();
                if (generation != _treeGeneration)
                {
                    return;
                }

                node.MarkExpandedWithoutLoading();
                if (node.IsPlaceholder
                    || (node.Children.Count > 0 && !node.Children[0].IsPlaceholder))
                {
                    await Task.Yield();
                    continue;
                }

                (bool admitted, DirectoryLevel? level) = await LoadExpandedLevelAsync(
                    node,
                    ordering,
                    token);
                if (!admitted)
                {
                    return;
                }

                token.ThrowIfCancellationRequested();
                if (level is null)
                {
                    throw new InvalidOperationException(
                        "Tree worker completed without an expanded directory level.");
                }

                if (generation != _treeGeneration || !node.IsExpanded)
                {
                    return;
                }

                // Expand Loaded is deliberately snapshot-based: children that
                // materialize here are not recursively expanded in this run.
                node.ReplaceChildren(level.Nodes);
                // Dispatcher publication boundary (codex round 7):
                // recursive check projection.
                ProjectBatchChecksOnto(level.Nodes);
                if (level.Truncated)
                {
                    Status = DirectoryOverflowStatus(node.Path);
                }

                await Task.Yield();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ReportExpansionFailure(exception);
        }
        finally
        {
            IsExpandingLoaded = false;
            bool ownsCancellation = false;
            lock (_bulkExpandCancellationGate)
            {
                if (ReferenceEquals(_bulkExpandCancellation, cancellation))
                {
                    _bulkExpandCancellation = null;
                    ownsCancellation = true;
                }
            }

            if (ownsCancellation)
            {
                cancellation.Dispose();
            }
        }
    }

    private async Task<(bool Admitted, DirectoryLevel? Level)> LoadExpandedLevelAsync(
        FileTreeNodeViewModel node,
        DirectoryOrdering ordering,
        CancellationToken cancellationToken)
    {
        DirectoryLevel? level = null;
        bool admitted = await RunAdmittedTreeWorkerAsync(
            () => level = LoadDirectoryLevel(
                node.Path,
                node.Level + 1,
                ordering: ordering,
                cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return (admitted, level);
    }

    private async Task<bool> RunAdmittedTreeWorkerAsync(
        Action work,
        CancellationToken cancellationToken)
    {
        await _treeProviderLane.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!TryBeginSessionWork(out SessionWorkLease? lease))
            {
                return false;
            }

            using (lease)
            {
                await _runTreeWorker(work, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }
        finally
        {
            _treeProviderLane.Release();
        }
    }

    private void ReportExpansionFailure(Exception exception)
    {
        HostLog.Write(HostDiagnosticEvent.SidebarBulkExpansionFailed, exception);
        try
        {
            ReportFailure("Could not expand loaded folders.");
        }
        catch (Exception callbackException)
        {
            HostLog.Write(HostDiagnosticEvent.SidebarBulkExpansionFailed, callbackException);
        }
    }

    private void CancelBulkExpansion()
    {
        CancellationTokenSource? cancellation;
        lock (_bulkExpandCancellationGate)
        {
            cancellation = _bulkExpandCancellation;
            _bulkExpandCancellation = null;
        }

        CancelAndDisposeWithoutThrowing(
            cancellation,
            HostDiagnosticEvent.SidebarBulkExpansionShutdownFailed);
    }

    private static void CancelAndDisposeWithoutThrowing(
        CancellationTokenSource? cancellation,
        HostDiagnosticEvent failureEvent)
    {
        if (cancellation is null)
        {
            return;
        }

        try
        {
            try
            {
                cancellation.Cancel();
            }
            catch (Exception exception)
            {
                // Cancellation is best-effort during teardown. A callback
                // failure must not prevent later producers from being canceled.
                HostLog.Write(failureEvent, exception);
            }
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    public void CancelExpandLoaded() => CancelBulkExpansion();

    private sealed record TreeRefreshOutcome(
        ObservableCollection<FileTreeNodeViewModel> RootNodes,
        DirectoryLevel Level,
        string? RestoredOverflowPath,
        TagLoadOutcome Tags,
        int TagGeneration);
}
