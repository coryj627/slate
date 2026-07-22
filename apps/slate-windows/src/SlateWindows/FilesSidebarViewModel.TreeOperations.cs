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
    private CancellationTokenSource? _treeRefreshCancellation;
    private CancellationTokenSource? _bulkExpandCancellation;
    private Task _treeRefreshCompletion = Task.CompletedTask;
    private Task _expandLoadedCompletion = Task.CompletedTask;
    private int _treeGeneration;
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
    internal Task ExpandLoadedCompletion => _expandLoadedCompletion;

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

    public void LoadChildren(FileTreeNodeViewModel node)
    {
        if (!node.IsDirectory || node.IsPlaceholder
            || (node.Children.Count > 0 && !node.Children[0].IsPlaceholder))
        {
            return;
        }

        try
        {
            node.Children.Clear();
            DirectoryLevel level = LoadDirectoryLevel(node.Path, node.Level + 1);
            foreach (FileTreeNodeViewModel child in level.Nodes)
            {
                node.Children.Add(child);
            }

            RestoreExpansions(node.Children);
            if (level.Truncated)
            {
                Status = DirectoryOverflowStatus(node.Path);
            }
        }
        catch (VaultException exception)
        {
            ReportFailure($"Could not load {node.Path}: {exception.Message}");
        }
    }

    public void Refresh(bool reportCount = false)
    {
        CancelBulkExpansion();
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
            try
            {
                ApplyTreeRefresh(BuildTreeRefresh(
                    ordering,
                    expandedPaths,
                    tagGeneration,
                    CancellationToken.None), reportCount);
                _treeRefreshCompletion = Task.CompletedTask;
            }
            catch (VaultException exception)
            {
                ReportFailure($"Could not load files: {exception.Message}");
                _treeRefreshCompletion = Task.CompletedTask;
            }
            catch (Exception exception)
            {
                HostLog.Write(HostDiagnosticEvent.SidebarTreeRefreshFailed, exception);
                try
                {
                    ReportFailure("Could not load files.");
                }
                catch (Exception callbackException)
                {
                    HostLog.Write(HostDiagnosticEvent.SidebarTreeRefreshFailed, callbackException);
                }

                _treeRefreshCompletion = Task.CompletedTask;
            }

            return;
        }

        var cancellation = new CancellationTokenSource();
        _treeRefreshCancellation = cancellation;
        Task previous = _treeRefreshCompletion;
        Status = "Loading files…";
        _treeRefreshCompletion = RefreshTreeAsync(
            previous,
            generation,
            ordering,
            expandedPaths,
            tagGeneration,
            reportCount,
            cancellation);
    }

    private async Task RefreshTreeAsync(
        Task previous,
        int generation,
        DirectoryOrdering ordering,
        IReadOnlySet<string> expandedPaths,
        int tagGeneration,
        bool reportCount,
        CancellationTokenSource cancellation)
    {
        CancellationToken token = cancellation.Token;
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
            await _runTreeWorker(
                () => outcome = BuildTreeRefresh(
                    ordering,
                    expandedPaths,
                    tagGeneration,
                    token),
                token).ConfigureAwait(false);
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
                            ApplyTreeRefresh(outcome, reportCount);
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
                token).ConfigureAwait(false);
        }
        finally
        {
            if (ReferenceEquals(_treeRefreshCancellation, cancellation))
            {
                _treeRefreshCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private async Task ReportTreeRefreshFailureAsync(
        int generation,
        string message,
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
                            ReportFailure(message);
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
        return new TreeRefreshOutcome(
            new ObservableCollection<FileTreeNodeViewModel>(level.Nodes),
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

    private void ApplyTreeRefresh(TreeRefreshOutcome outcome, bool reportCount)
    {
        RootNodes = outcome.RootNodes;
        if (outcome.TagGeneration == _tagGeneration)
        {
            ApplyTags(outcome.Tags);
        }

        ScheduleFilter();
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
    }

    internal bool CancelTreeRefresh()
    {
        bool wasPending = IsRefreshingTree;
        if (wasPending)
        {
            ++_treeGeneration;
            CancelTreeRefreshCore();
        }

        return wasPending;
    }

    private void CancelTreeRefreshCore()
    {
        CancellationTokenSource? cancellation = _treeRefreshCancellation;
        _treeRefreshCancellation = null;
        cancellation?.Cancel();
    }

    private void CollapseAll()
    {
        CancelBulkExpansion();
        foreach (FileTreeNodeViewModel node in Flatten(RootNodes).Where(node => node.IsDirectory))
        {
            node.IsExpanded = false;
        }
    }

    private async Task ExpandLoadedAsync()
    {
        CancelBulkExpansion();
        using var cancellation = new CancellationTokenSource();
        _bulkExpandCancellation = cancellation;
        int generation = _treeGeneration;
        DirectoryOrdering ordering = CaptureDirectoryOrdering();
        FileTreeNodeViewModel[] materializedDirectories = Flatten(RootNodes)
            .Where(node => node.IsDirectory)
            .ToArray();
        IsExpandingLoaded = true;
        try
        {
            foreach (FileTreeNodeViewModel node in materializedDirectories)
            {
                cancellation.Token.ThrowIfCancellationRequested();
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

                DirectoryLevel level = await Task.Run(
                    () => LoadDirectoryLevel(
                        node.Path,
                        node.Level + 1,
                        ordering: ordering,
                        cancellationToken: cancellation.Token),
                    cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (generation != _treeGeneration || !node.IsExpanded)
                {
                    return;
                }

                // Expand Loaded is deliberately snapshot-based: children that
                // materialize here are not recursively expanded in this run.
                node.ReplaceChildren(level.Nodes);
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
        catch (VaultException exception)
        {
            ReportFailure($"Could not expand loaded folders: {exception.Message}");
        }
        finally
        {
            IsExpandingLoaded = false;
            if (ReferenceEquals(_bulkExpandCancellation, cancellation))
            {
                _bulkExpandCancellation = null;
            }
        }
    }

    private void CancelBulkExpansion()
    {
        _bulkExpandCancellation?.Cancel();
        _bulkExpandCancellation = null;
    }

    public void CancelExpandLoaded() => CancelBulkExpansion();

    private sealed record TreeRefreshOutcome(
        ObservableCollection<FileTreeNodeViewModel> RootNodes,
        DirectoryLevel Level,
        string? RestoredOverflowPath,
        TagLoadOutcome Tags,
        int TagGeneration);
}
