// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.FileManagement;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>C7: one admitted, cancellable destination read per picker request.
/// Native work owns a session lease; dispatcher publication never does.</summary>
internal sealed partial class FilesSidebarViewModel
{
    private const int MoveToFolderLimit = 50_000;
    private readonly Func<Action, CancellationToken, Task> _runMoveToWorker;
    private readonly object _moveToCancellationGate = new();
    private CancellationTokenSource? _moveToCancellation;
    private Task _moveToCompletion = Task.CompletedTask;
    private int _moveToGeneration;

    internal Task MoveToCompletion => _moveToCompletion;
    internal Func<bool>? MoveToOwnsModal { get; set; }
    internal Action<string, string?>? BeforeMoveToPageForTesting { get; set; }

    private bool OwnsMoveToPresentation(MoveToPickerViewModel picker) =>
        !SessionShutdownStarted && ReferenceEquals(MoveToSheet, picker)
            && MoveToOwnsModal?.Invoke() != false;

    private void StartMoveToLoading(
        MoveToPickerViewModel picker, HashSet<string> knownFolders, Func<string, bool> isLegal)
    {
        if (!OwnsMoveToPresentation(picker)) { return; }
        CancelMoveToLoading();
        knownFolders.Clear();
        picker.BeginLoading();
        int generation = Volatile.Read(ref _moveToGeneration);
        var cancellation = new CancellationTokenSource();
        CancellationToken token = cancellation.Token;
        lock (_moveToCancellationGate)
        {
            if (SessionShutdownStarted)
            {
                cancellation.Dispose();
                return;
            }
            _moveToCancellation = cancellation;
        }
        Task previous = _moveToCompletion;
        _moveToCompletion = Task.WhenAll(previous,
            LoadMoveToFoldersAsync(picker, knownFolders, isLegal, generation, cancellation, token));
    }

    private void CancelMoveToLoading()
    {
        Interlocked.Increment(ref _moveToGeneration);
        CancellationTokenSource? cancellation;
        lock (_moveToCancellationGate)
        {
            cancellation = _moveToCancellation;
            _moveToCancellation = null;
        }
        CancelAndDisposeWithoutThrowing(cancellation, HostDiagnosticEvent.SidebarMoveToShutdownFailed);
    }

    private async Task LoadMoveToFoldersAsync(
        MoveToPickerViewModel picker, HashSet<string> knownFolders, Func<string, bool> isLegal,
        int generation, CancellationTokenSource cancellation, CancellationToken token)
    {
        // Publish the completion boundary before the first provider admission.
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        try
        {
            var folders = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(string.Empty);
            bool truncated = false;
            while (queue.Count > 0 && !truncated)
            {
                string parent = queue.Dequeue();
                string? cursor = null;
                do
                {
                    token.ThrowIfCancellationRequested();
                    DirListingPage? page = null;
                    if (!TryBeginSessionWork(out SessionWorkLease? lease)) { return; }
                    using (lease)
                    {
                        await _runMoveToWorker(() =>
                        {
                            token.ThrowIfCancellationRequested();
                            BeforeMoveToPageForTesting?.Invoke(parent, cursor);
                            token.ThrowIfCancellationRequested();
                            using var nativeCancellation = new CancelToken();
                            using CancellationTokenRegistration registration = token.Register(nativeCancellation.Cancel);
                            page = _session.ListDirChildrenPage(parent, new Paging(cursor, 1_000), nativeCancellation);
                        }, token).ConfigureAwait(false);
                    }
                    token.ThrowIfCancellationRequested();
                    if (page is null) { throw new InvalidOperationException("Destination worker returned no page."); }
                    int priorCount = folders.Count;
                    foreach (DirNodeSummary directory in page.Dirs)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!seen.Add(directory.Path)) { continue; }
                        folders.Add(directory.Path);
                        queue.Enqueue(directory.Path);
                        if (folders.Count >= MoveToFolderLimit)
                        {
                            truncated = true;
                            break;
                        }
                    }
                    if (folders.Count != priorCount)
                    {
                        string[] snapshot = [.. folders];
                        if (!await PublishMoveToAsync(picker, generation, token, () =>
                        {
                            knownFolders.UnionWith(snapshot);
                            picker.PublishFolders([.. snapshot.Where(isLegal)], complete: false, truncated: false);
                        }).ConfigureAwait(false)) { return; }
                    }
                    if (page.NextCursor is not null && page.NextCursor == cursor)
                    {
                        throw new InvalidOperationException("Destination listing cursor did not advance.");
                    }
                    cursor = page.NextCursor;
                }
                while (cursor is not null && !truncated);
            }
            await PublishMoveToAsync(picker, generation, token, () =>
            {
                knownFolders.UnionWith(folders);
                picker.PublishFolders([.. folders.Where(isLegal)], complete: true, truncated);
                _announce(new A11yEvent.HostComposed(picker.Status, A11yPriority.Medium));
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (VaultException.Cancelled) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
            {
                HostLog.Write(HostDiagnosticEvent.SidebarMoveToFailed, exception);
                try
                {
                    await PublishMoveToAsync(picker, generation, token, () =>
                    {
                        picker.FailLoading("Could not load destination folders. Try again or cancel.");
                        _announce(new A11yEvent.HostComposed(picker.Status, A11yPriority.High));
                    }).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                catch (Exception publishFailure)
                {
                    HostLog.Write(HostDiagnosticEvent.SidebarMoveToFailed, publishFailure);
                }
            }
        }
        finally
        {
            lock (_moveToCancellationGate)
            {
                if (ReferenceEquals(_moveToCancellation, cancellation))
                {
                    _moveToCancellation = null;
                    cancellation.Dispose();
                }
            }
        }
    }

    private async Task<bool> PublishMoveToAsync(
        MoveToPickerViewModel picker, int generation, CancellationToken token, Action apply)
    {
        token.ThrowIfCancellationRequested();
        var published = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Publish()
        {
            try
            {
                bool owns = !token.IsCancellationRequested
                    && generation == Volatile.Read(ref _moveToGeneration)
                    && OwnsMoveToPresentation(picker);
                if (owns) { apply(); }
                else if (!SessionShutdownStarted && ReferenceEquals(MoveToSheet, picker)
                    && generation == Volatile.Read(ref _moveToGeneration))
                {
                    MoveToSheet = null;
                }
                published.TrySetResult(owns);
            }
            catch (Exception exception) { published.TrySetException(exception); }
        }
        if (_treeUiContext is { } context) { context.Post(_ => Publish(), null); }
        else { Publish(); } // Explicit headless fallback; native work still runs off-thread.
        return await published.Task.WaitAsync(token).ConfigureAwait(false);
    }
}
