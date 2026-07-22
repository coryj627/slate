// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows;

/// <summary>
/// Owns admission and draining for every direct sidebar operation that enters
/// the shared native vault session. Shutdown closes admission before cancellation,
/// then waits only for already-admitted native work; UI continuations may
/// finish later without touching the released session.
/// </summary>
internal sealed partial class FilesSidebarViewModel
{
    private readonly object _sessionWorkGate = new();
    private int _activeSessionWork;
    private bool _sessionShutdownStarted;
    private TaskCompletionSource? _sessionWorkDrained;

    internal bool SessionShutdownStarted
    {
        get
        {
            lock (_sessionWorkGate)
            {
                return _sessionShutdownStarted;
            }
        }
    }

    internal SidebarSessionShutdown BeginSessionShutdownAndCaptureWork()
    {
        Task sessionWork;
        lock (_sessionWorkGate)
        {
            _sessionShutdownStarted = true;
            if (_activeSessionWork == 0)
            {
                sessionWork = Task.CompletedTask;
            }
            else
            {
                _sessionWorkDrained ??= new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                sessionWork = _sessionWorkDrained.Task;
            }
        }

        // Close admission first, then cancel every producer before lifecycle
        // waits on any one operation. This prevents a finishing import from
        // admitting a late tree refresh behind the shutdown snapshot.
        Task treeRefresh = CancelTreeRefreshAndGetCompletion();
        Task filter = CancelFilterAndGetCompletion();
        CancelExpandLoaded();
        CancelImport();
        return new SidebarSessionShutdown(sessionWork, treeRefresh, filter);
    }

    private bool TryBeginSessionWork(out SessionWorkLease? lease)
    {
        lock (_sessionWorkGate)
        {
            if (_sessionShutdownStarted)
            {
                lease = null;
                return false;
            }

            _activeSessionWork++;
            lease = new SessionWorkLease(this);
            return true;
        }
    }

    private bool TryRunSessionWork(Action work)
    {
        if (!TryBeginSessionWork(out SessionWorkLease? lease))
        {
            return false;
        }

        using (lease)
        {
            work();
        }

        return true;
    }

    private bool TryRunSessionWork<T>(Func<T> work, out T result)
    {
        result = default!;
        if (!TryBeginSessionWork(out SessionWorkLease? lease))
        {
            return false;
        }

        using (lease)
        {
            result = work();
        }

        return true;
    }

    private void EndSessionWork()
    {
        TaskCompletionSource? drained = null;
        lock (_sessionWorkGate)
        {
            _activeSessionWork--;
            if (_activeSessionWork == 0 && _sessionShutdownStarted)
            {
                drained = _sessionWorkDrained;
                _sessionWorkDrained = null;
            }
        }

        drained?.TrySetResult();
    }

    private sealed class SessionWorkLease(FilesSidebarViewModel owner) : IDisposable
    {
        private FilesSidebarViewModel? _owner = owner;

        public void Dispose() =>
            Interlocked.Exchange(ref _owner, null)?.EndSessionWork();
    }
}

internal sealed record SidebarSessionShutdown(
    Task SessionWork,
    Task TreeRefresh,
    Task Filter);
