// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;

namespace SlateWindows;

/// <summary>
/// Owns ordinary, user-triggered child-folder materialization. Native provider,
/// projection, ordering, and restored descendants run on the serialized tree
/// worker; the dispatcher receives one current-generation collection swap.
/// </summary>
internal sealed partial class FilesSidebarViewModel
{
    private const string ChildExpansionFailure =
        "Could not load folder. Collapse and expand to retry.";
    private const string ChildExpansionCanceled =
        "Folder loading was canceled. Collapse and expand to retry.";
    private readonly object _childExpansionGate = new();
    private readonly Dictionary<FileTreeNodeViewModel, ChildExpansionOperation> _childExpansions = [];
    private int _activeChildExpansions;
    private TaskCompletionSource? _childExpansionsIdle;

    internal bool IsLoadingChildren
    {
        get
        {
            lock (_childExpansionGate)
            {
                return _activeChildExpansions > 0;
            }
        }
    }

    internal Task ChildExpansionCompletion
    {
        get
        {
            lock (_childExpansionGate)
            {
                return _activeChildExpansions == 0
                    ? Task.CompletedTask
                    : _childExpansionsIdle!.Task;
            }
        }
    }

    public void LoadChildren(FileTreeNodeViewModel node)
    {
        ObservableCollection<FileTreeNodeViewModel> rootNodes = RootNodes;
        if (SessionShutdownStarted
            || node.IsPlaceholder
            || !node.IsAttachedToTree(rootNodes)
            || !node.PrepareChildLoad())
        {
            return;
        }

        if (_treeUiContext is null)
        {
            LoadChildrenSynchronously(node);
            return;
        }

        CancelBulkExpansion();
        CancelChildExpansion(node);
        int generation = _treeGeneration;
        DirectoryOrdering ordering = CaptureDirectoryOrdering();
        var expandedPaths = _expandedPaths.ToHashSet(StringComparer.Ordinal);
        Task treeRefresh = _treeRefreshCompletion;
        var cancellation = new CancellationTokenSource();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = LoadChildrenAsync(
            node,
            generation,
            rootNodes,
            ordering,
            expandedPaths,
            treeRefresh,
            cancellation,
            start.Task,
            cancellation.Token);
        lock (_childExpansionGate)
        {
            if (_activeChildExpansions == 0)
            {
                _childExpansionsIdle = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            _activeChildExpansions++;
            _childExpansions[node] = new ChildExpansionOperation(cancellation);
        }

        start.SetResult();
    }

    internal void CancelChildExpansion(FileTreeNodeViewModel node)
    {
        ChildExpansionOperation? operation = null;
        lock (_childExpansionGate)
        {
            if (_childExpansions.Remove(node, out ChildExpansionOperation? current))
            {
                operation = current;
            }
        }

        CancelAndDisposeWithoutThrowing(
            operation?.Cancellation,
            HostDiagnosticEvent.SidebarChildExpansionShutdownFailed);
        if (operation is not null)
        {
            MarkChildExpansionCanceled(node, operation.Cancellation);
        }
    }

    internal Task CancelChildExpansionsAndGetCompletion()
    {
        Task completion = ChildExpansionCompletion;
        CancelChildExpansions();
        return completion;
    }

    /// <summary>Cancels every folder load. Plan 47 CB-39: a Reveal waiting on
    /// one of them ends, since nothing lists the folder after this cancel (a
    /// refused close, a close's cancelled refresh, the shutdown, Collapse
    /// all).</summary>
    internal void CancelChildExpansions() => CancelChildExpansions(listsTheFoldersItself: false);

    /// <param name="listsTheFoldersItself">Plan 47 CB-39: true for a canceller
    /// that lists the folders itself (a whole-tree refresh restores every open
    /// folder; Expand loaded folders lists them), which keeps a waiting
    /// Reveal.</param>
    private void CancelChildExpansions(bool listsTheFoldersItself)
    {
        KeyValuePair<FileTreeNodeViewModel, ChildExpansionOperation>[] operations;
        lock (_childExpansionGate)
        {
            operations = _childExpansions.ToArray();
            _childExpansions.Clear();
        }

        foreach ((FileTreeNodeViewModel node, ChildExpansionOperation operation) in operations)
        {
            CancelAndDisposeWithoutThrowing(
                operation.Cancellation,
                HostDiagnosticEvent.SidebarChildExpansionShutdownFailed);
            MarkChildExpansionCanceled(node, operation.Cancellation);
            if (!listsTheFoldersItself)
            {
                EndWaitingRevealThrough(node);
            }
        }
    }

    private void MarkChildExpansionCanceled(
        FileTreeNodeViewModel node,
        CancellationTokenSource canceledOperation)
    {
        void ApplyWithoutThrowing()
        {
            try
            {
                lock (_childExpansionGate)
                {
                    if (_childExpansions.TryGetValue(
                        node,
                        out ChildExpansionOperation? current)
                        && !ReferenceEquals(current.Cancellation, canceledOperation))
                    {
                        return;
                    }
                }

                node.CancelChildLoad(ChildExpansionCanceled);
            }
            catch (Exception exception)
            {
                HostLog.Write(
                    HostDiagnosticEvent.SidebarChildExpansionShutdownFailed,
                    exception);
            }
        }

        if (_treeUiContext is null
            || ReferenceEquals(SynchronizationContext.Current, _treeUiContext))
        {
            ApplyWithoutThrowing();
        }
        else
        {
            try
            {
                _treeUiContext.Post(_ => ApplyWithoutThrowing(), null);
            }
            catch (Exception exception)
            {
                HostLog.Write(
                    HostDiagnosticEvent.SidebarChildExpansionShutdownFailed,
                    exception);
            }
        }
    }

    private void LoadChildrenSynchronously(FileTreeNodeViewModel node)
    {
        try
        {
            if (!TryBeginSessionWork(out SessionWorkLease? lease))
            {
                return;
            }

            DirectoryLevel level;
            using (lease)
            {
                DirectoryOrdering ordering = CaptureDirectoryOrdering();
                level = LoadDirectoryLevel(node.Path, node.Level + 1, ordering: ordering);
                RestoreExpansionsForRefresh(
                    level.Nodes,
                    _expandedPaths,
                    ordering,
                    CancellationToken.None);
            }

            node.ReplaceChildren(level.Nodes);
            // Dispatcher publication boundary (codex round 7): the
            // authoritative checks project over the whole published
            // subtree, UI-thread-only.
            ProjectBatchChecksOnto(level.Nodes);
            StartReportedPinPrunes();
            if (level.Truncated)
            {
                // Plan 47 CB-16 (#1791, W9; the diff red team's finding 2): an
                // overflow line is the tree's own, stale once the folder closes or
                // shrinks, so no refresh puts it back.
                WriteTreeStatus(DirectoryOverflowStatus(node.Path));
            }

            // Plan 47 CB-39 (#1670): no Reveal step here. In this headless
            // fallback a Reveal's own walk starts this load and passes through
            // the listed folder in the same call, and no Reveal can be waiting
            // when anything else starts one, since nothing it waits for is ever
            // pending.
        }
        catch (Exception exception)
        {
            ReportChildExpansionFailure(node, exception);
        }
    }

    private async Task LoadChildrenAsync(
        FileTreeNodeViewModel node,
        int generation,
        ObservableCollection<FileTreeNodeViewModel> rootNodes,
        DirectoryOrdering ordering,
        IReadOnlySet<string> expandedPaths,
        Task treeRefresh,
        CancellationTokenSource cancellation,
        Task start,
        CancellationToken token)
    {
        await start.ConfigureAwait(false);
        try
        {
            token.ThrowIfCancellationRequested();
            await treeRefresh.WaitAsync(token).ConfigureAwait(false);
            if (!CanPublishChildExpansion(
                node,
                cancellation,
                generation,
                rootNodes,
                token))
            {
                return;
            }

            ChildExpansionOutcome? outcome = null;
            bool admitted = await RunAdmittedTreeWorkerAsync(
                () =>
                {
                    DirectoryLevel level = LoadDirectoryLevel(
                        node.Path,
                        node.Level + 1,
                        ordering: ordering,
                        cancellationToken: token);
                    string? restoredOverflowPath = RestoreExpansionsForRefresh(
                        level.Nodes,
                        expandedPaths,
                        ordering,
                        token);
                    foreach (FileTreeNodeViewModel child in level.Nodes)
                    {
                        child.AttachToTree(rootNodes);
                    }

                    outcome = new ChildExpansionOutcome(
                        new ObservableCollection<FileTreeNodeViewModel>(level.Nodes),
                        level,
                        restoredOverflowPath);
                },
                token).ConfigureAwait(false);
            if (!admitted)
            {
                return;
            }

            token.ThrowIfCancellationRequested();
            if (outcome is null)
            {
                throw new InvalidOperationException(
                    "Tree worker completed without a child expansion outcome.");
            }

            var applied = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _treeUiContext!.Post(
                _ =>
                {
                    try
                    {
                        if (CanPublishChildExpansion(
                            node,
                            cancellation,
                            generation,
                            rootNodes,
                            token))
                        {
                            node.ReplaceChildren(outcome.Children);
                            // Dispatcher publication boundary (codex
                            // round 7): recursive check projection.
                            ProjectBatchChecksOnto(outcome.Children);
                            StartReportedPinPrunes();
                            // Plan 47 CB-16 (#1791, W9): the tree's own lines.
                            if (outcome.Level.Truncated)
                            {
                                WriteTreeStatus(DirectoryOverflowStatus(node.Path));
                            }
                            else if (outcome.RestoredOverflowPath is string overflowPath)
                            {
                                WriteTreeStatus(DirectoryOverflowStatus(overflowPath));
                            }

                            // Plan 47 CB-39 (#1670): the folder's own load
                            // seats a waiting Reveal, or opens the next folder
                            // on its path.
                            AdvanceWaitingReveal();
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
            HostLog.Write(HostDiagnosticEvent.SidebarChildExpansionFailed, exception);
            await ReportChildExpansionFailureAsync(
                node,
                cancellation,
                generation,
                rootNodes,
                token).ConfigureAwait(false);
        }
        finally
        {
            CompleteChildExpansion(node, cancellation);
        }
    }

    private bool CanPublishChildExpansion(
        FileTreeNodeViewModel node,
        CancellationTokenSource cancellation,
        int generation,
        ObservableCollection<FileTreeNodeViewModel> rootNodes,
        CancellationToken token)
    {
        // Plan 43 PR 20b: a rescan's reconcile keeps the published collection
        // and lets go of the rows it replaced, so a row's own attachment is
        // checked as well as the collection's.
        if (token.IsCancellationRequested
            || SessionShutdownStarted
            || generation != _treeGeneration
            || !ReferenceEquals(rootNodes, RootNodes)
            || !node.IsAttachedToTree(RootNodes)
            || !node.IsExpanded)
        {
            return false;
        }

        lock (_childExpansionGate)
        {
            return _childExpansions.TryGetValue(node, out ChildExpansionOperation? operation)
                && ReferenceEquals(operation.Cancellation, cancellation);
        }
    }

    private async Task ReportChildExpansionFailureAsync(
        FileTreeNodeViewModel node,
        CancellationTokenSource cancellation,
        int generation,
        ObservableCollection<FileTreeNodeViewModel> rootNodes,
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
                        if (CanPublishChildExpansion(
                            node,
                            cancellation,
                            generation,
                            rootNodes,
                            token))
                        {
                            node.FailChildLoad(ChildExpansionFailure);
                            // Plan 47 CB-39: a Reveal waiting through the folder ends.
                            EndWaitingRevealThrough(node);
                            ReportTreeReadFailure(ChildExpansionFailure);
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
            HostLog.Write(HostDiagnosticEvent.SidebarChildExpansionFailed, exception);
        }
    }

    private void ReportChildExpansionFailure(
        FileTreeNodeViewModel node,
        Exception exception)
    {
        HostLog.Write(HostDiagnosticEvent.SidebarChildExpansionFailed, exception);
        node.FailChildLoad(ChildExpansionFailure);
        // Plan 47 CB-39: a Reveal waiting through the folder ends.
        EndWaitingRevealThrough(node);
        try
        {
            ReportTreeReadFailure(ChildExpansionFailure);
        }
        catch (Exception callbackException)
        {
            HostLog.Write(HostDiagnosticEvent.SidebarChildExpansionFailed, callbackException);
        }
    }

    private void CompleteChildExpansion(
        FileTreeNodeViewModel node,
        CancellationTokenSource cancellation)
    {
        bool ownsCancellation = false;
        TaskCompletionSource? idle = null;
        lock (_childExpansionGate)
        {
            if (_childExpansions.TryGetValue(node, out ChildExpansionOperation? operation)
                && ReferenceEquals(operation.Cancellation, cancellation))
            {
                _childExpansions.Remove(node);
                ownsCancellation = true;
            }

            _activeChildExpansions--;
            if (_activeChildExpansions == 0)
            {
                idle = _childExpansionsIdle;
                _childExpansionsIdle = null;
            }
        }

        if (ownsCancellation)
        {
            cancellation.Dispose();
        }

        idle?.TrySetResult();
    }

    private sealed record ChildExpansionOperation(
        CancellationTokenSource Cancellation);

    private sealed record ChildExpansionOutcome(
        ObservableCollection<FileTreeNodeViewModel> Children,
        DirectoryLevel Level,
        string? RestoredOverflowPath);
}
