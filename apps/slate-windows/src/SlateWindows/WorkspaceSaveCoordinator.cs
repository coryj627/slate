// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// #1280 (locked decision 05 §4.1; contract 35 A-1): every save in a
/// workspace runs through one coordinator.
/// <list type="bullet">
/// <item>Saves to one canonical path run strictly one after another across
/// EVERY tab at that path: each starts, on the dispatcher, only after the
/// previous one has published, so it snapshots the content hash that
/// publication recorded — a same-path peer's save never races a second CAS
/// write from the same old hash into a false "modified externally".</item>
/// <item>The worker phase is tracked apart from the dispatcher publication,
/// so teardown joins every worker without depending on a dispatcher
/// callback — no write lands, and no worker calls core, after the session is
/// disposed.</item>
/// <item>Admitted saves are counted, so teardown can settle every one of them
/// before it evaluates what is still dirty (a write admitted before the
/// prompt is never "discarded" after it has already landed).</item>
/// </list>
/// </summary>
internal sealed class WorkspaceSaveCoordinator
{
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<string, Task<bool>> _tails = new(StringComparer.Ordinal);
    private readonly Lock _workersGate = new();
    private readonly HashSet<Task> _workers = [];
    private int _pending;
    private TaskCompletionSource _idle = CompletedIdle();
    private bool _closed;

    internal WorkspaceSaveCoordinator(Dispatcher dispatcher) => _dispatcher = dispatcher;

    /// <summary>True when no admitted save is still waiting or writing.</summary>
    internal bool IsIdle
    {
        get
        {
            _dispatcher.VerifyAccess();
            return _pending == 0;
        }
    }

    /// <summary>True once teardown has closed the coordinator.</summary>
    internal bool IsClosed => _closed;

    /// <summary>Workers not yet finished — a test observes the join.</summary>
    internal int LiveWorkersForTests
    {
        get
        {
            lock (_workersGate)
            {
                return _workers.Count;
            }
        }
    }

    /// <summary>Admit a save for <paramref name="key"/>. <paramref name="start"/>
    /// runs on the dispatcher once every earlier save for the key has
    /// completed, and completes the ticket it is handed exactly once —
    /// from the publication, or from an early exit. A closed coordinator
    /// admits nothing.</summary>
    internal Task<bool> Enqueue(string key, Action<SaveTicket> start)
    {
        _dispatcher.VerifyAccess();
        if (_closed)
        {
            return Task.FromResult(false);
        }

        var ticket = new SaveTicket(this, key);
        if (_pending++ == 0)
        {
            _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        Task<bool>? previous = _tails.GetValueOrDefault(key);
        _tails[key] = ticket.Task;
        if (previous is null || previous.IsCompleted)
        {
            Run(ticket, start);
        }
        else
        {
            previous.ContinueWith(
                _ => _dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(() => Run(ticket, start))),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
        return ticket.Task;
    }

    private void Run(SaveTicket ticket, Action<SaveTicket> start)
    {
        if (_closed)
        {
            ticket.Complete(false);
            return;
        }
        try
        {
            start(ticket);
        }
        catch (Exception exception)
        {
            ticket.Fail(exception);
        }
    }

    /// <summary>Track a save's worker until it finishes, whatever the
    /// dispatcher does with its publication.</summary>
    internal void TrackWorker(Task worker)
    {
        lock (_workersGate)
        {
            _workers.Add(worker);
        }
        worker.ContinueWith(
            finished =>
            {
                lock (_workersGate)
                {
                    _workers.Remove(finished);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>Teardown step 1: pump until every admitted save — including
    /// any admitted while this waits — has published. False only when the
    /// dispatcher is shutting down.</summary>
    internal bool SettlePumping()
    {
        _dispatcher.VerifyAccess();
        while (_pending > 0)
        {
            if (!PumpedWait.Until(_dispatcher, _idle.Task))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Teardown's last step before the session goes: refuse every
    /// later save (a queued one completes <c>false</c> without starting) and
    /// join every worker. Synchronous — the workers call only core, so no
    /// dispatcher callback is needed for them to finish.</summary>
    internal void CloseAndJoinWorkers()
    {
        _closed = true;
        Task[] workers;
        lock (_workersGate)
        {
            workers = [.. _workers];
        }
        foreach (Task worker in workers)
        {
            try
            {
                worker.Wait();
            }
            catch (AggregateException)
            {
                // The worker's failure belongs to its publication, which a
                // disposed tab ignores; the join only needs it finished.
            }
        }
    }

    private void Completed(SaveTicket ticket)
    {
        if (_tails.TryGetValue(ticket.Key, out Task<bool>? tail)
            && ReferenceEquals(tail, ticket.Task))
        {
            _tails.Remove(ticket.Key);
        }
        if (--_pending == 0)
        {
            _idle.TrySetResult();
        }
    }

    private static TaskCompletionSource CompletedIdle()
    {
        var idle = new TaskCompletionSource();
        idle.SetResult();
        return idle;
    }

    /// <summary>One admitted save: completed once, on the dispatcher.</summary>
    internal sealed class SaveTicket
    {
        private readonly WorkspaceSaveCoordinator _owner;
        private readonly TaskCompletionSource<bool> _completion = new();

        internal SaveTicket(WorkspaceSaveCoordinator owner, string key)
        {
            _owner = owner;
            Key = key;
        }

        internal string Key { get; }

        internal Task<bool> Task => _completion.Task;

        internal void Complete(bool saved)
        {
            _owner._dispatcher.VerifyAccess();
            if (_completion.TrySetResult(saved))
            {
                _owner.Completed(this);
            }
        }

        internal void Fail(Exception exception)
        {
            _owner._dispatcher.VerifyAccess();
            if (_completion.TrySetException(exception))
            {
                _owner.Completed(this);
            }
        }
    }
}

/// <summary>#1280: a wait that keeps the dispatcher pumping — a nested
/// frame that ends when the task completes. Everything the dispatcher can
/// run may run inside it, so a caller re-reads what it acts on afterwards
/// (the pumped-wait invariant, contract 38 D-10).</summary>
internal static class PumpedWait
{
    /// <summary>True when <paramref name="task"/> completed; false when the
    /// dispatcher shut down first.</summary>
    internal static bool Until(Dispatcher dispatcher, Task task)
    {
        if (task.IsCompleted)
        {
            return true;
        }
        var frame = new DispatcherFrame();
        task.ContinueWith(
            _ => dispatcher.BeginInvoke(
                DispatcherPriority.Send,
                new Action(() => frame.Continue = false)),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        return task.IsCompleted;
    }

    /// <summary>The task's answer, or false when the dispatcher shut down
    /// before it completed.</summary>
    internal static bool Result(Dispatcher dispatcher, Task<bool> task) =>
        Until(dispatcher, task) && task.GetAwaiter().GetResult();
}
