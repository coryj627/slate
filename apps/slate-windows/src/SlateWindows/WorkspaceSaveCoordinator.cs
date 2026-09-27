// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// #1280 (locked decision 05 §4.1; contract 35 A-1): every save in a
/// workspace runs through one coordinator.
/// <list type="bullet">
/// <item>Saves to one file run strictly one after another across EVERY tab
/// on it: each starts, on the dispatcher, only after the previous one has
/// published, so it snapshots the content hash that publication recorded —
/// a same-path peer's save never races a second CAS write from the same old
/// hash into a false "modified externally". A rename carries the chain: the
/// new path's saves wait for every save admitted under the old path.</item>
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
    private readonly Dictionary<string, Task> _tails = new(StringComparer.Ordinal);
    private readonly Lock _workersGate = new();
    private readonly HashSet<Task> _workers = [];
    private int _pending;
    private TaskCompletionSource _idle = CompletedIdle();
    private bool _closed;

    internal WorkspaceSaveCoordinator(Dispatcher dispatcher) => _dispatcher = dispatcher;

    /// <summary>True when no admitted save is still waiting or writing. Saves
    /// are admitted and completed only on the dispatcher, so a caller on
    /// another thread (a headless teardown) reads a settled count.</summary>
    internal bool IsIdle => Volatile.Read(ref _pending) == 0;

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

    /// <summary>#1280 test seam: runs on the joining thread as teardown
    /// begins to join the save workers — a fact releases a parked worker
    /// here, deterministically inside the join.</summary>
    internal Action? BeforeJoinForTests { get; set; }

    /// <summary>The serialization keys of a tab's save: the tab itself — so
    /// its saves stay ordered across a rename that changes its path — and,
    /// for a note, its file (<see cref="PathKey"/>), so every tab on one file
    /// shares the chain.</summary>
    internal static string[] KeysFor(Guid tab, string? path) =>
        path is { Length: > 0 } ? [$"tab:{tab:N}", PathKey(path)] : [$"tab:{tab:N}"];

    /// <summary>A file's chain key: its path folded (NFC, lowercase). Folding
    /// can only merge chains, never split one, and a merged chain costs
    /// ordering, never correctness.</summary>
    private static string PathKey(string path) =>
        $"path:{path.Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant()}";

    /// <summary>Admit a save under <paramref name="keys"/>. <paramref name="start"/>
    /// runs on the dispatcher once every earlier save under ANY of the keys
    /// has completed, and completes the ticket it is handed exactly once —
    /// from the publication, or from an early exit. A closed coordinator
    /// admits nothing.</summary>
    internal Task<bool> Enqueue(IReadOnlyList<string> keys, Action<SaveTicket> start)
    {
        _dispatcher.VerifyAccess();
        if (_closed)
        {
            return Task.FromResult(false);
        }

        var ticket = new SaveTicket(this, keys);
        if (_pending++ == 0)
        {
            _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        Task[] previous = [.. keys
            .Select(key => _tails.GetValueOrDefault(key))
            .OfType<Task>()
            .Where(tail => !tail.IsCompleted)
            .Distinct()];
        foreach (string key in keys)
        {
            _tails[key] = ticket.Task;
        }
        if (previous.Length == 0)
        {
            Run(ticket, start);
        }
        else
        {
            Task.WhenAll(previous).ContinueWith(
                _ => _dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(() => Run(ticket, start))),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
        return ticket.Task;
    }

    /// <summary>
    /// The file at <paramref name="oldPath"/> is now <paramref name="newPath"/>
    /// (#1280, codex round 2a): the new path's chain continues from the old
    /// path's tail, so every save admitted under the new path waits for every
    /// save admitted before the rename — a peer's save after a rename starts
    /// from the hash the pre-rename write published, never beside it.
    /// </summary>
    internal void ContinueChain(string oldPath, string newPath)
    {
        _dispatcher.VerifyAccess();
        string from = PathKey(oldPath);
        string to = PathKey(newPath);
        if (string.Equals(from, to, StringComparison.Ordinal)
            || _tails.GetValueOrDefault(from) is not { IsCompleted: false } before)
        {
            return;
        }
        _tails[to] = _tails.GetValueOrDefault(to) is { IsCompleted: false } already
            ? Task.WhenAll(before, already)
            : before;
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
        BeforeJoinForTests?.Invoke();
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
        foreach (string key in ticket.Keys)
        {
            if (_tails.TryGetValue(key, out Task? tail)
                && ReferenceEquals(tail, ticket.Task))
            {
                _tails.Remove(key);
            }
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
        private readonly TaskCompletionSource<bool> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal SaveTicket(WorkspaceSaveCoordinator owner, IReadOnlyList<string> keys)
        {
            _owner = owner;
            Keys = keys;
        }

        internal IReadOnlyList<string> Keys { get; }

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
                // Codex round 2b: every failed save is observed and logged
                // ONCE, here — the Save command, Save All, a close or replace
                // gate and teardown read only its answer (PumpedWait.Result
                // turns a fault into "not saved").
                _ = _completion.Task.Exception;
                HostLog.Write(HostDiagnosticEvent.VaultCommandFailed, exception.GetBaseException());
                _owner.Completed(this);
            }
        }
    }
}

/// <summary>#1280: a wait that keeps the dispatcher pumping — a nested
/// frame that ends when the task completes. Everything the dispatcher can
/// run may run inside it, so a caller re-reads what it acts on afterwards
/// (the pumped-wait invariant, contract 38 D-10). Only the callers that need
/// a synchronous yes/no wait here: close tab, close pane, the replace gate
/// and vault teardown with its Save All — never the Save command.</summary>
internal static class PumpedWait
{
    // Per thread: a frame is entered on its dispatcher's thread, and facts
    // running in parallel on other threads must not move this one's count.
    [ThreadStatic]
    private static int _framesEntered;

    /// <summary>#1280 test seam: nested frames this thread has entered, ever
    /// — a fact proves a caller returned without pumping.</summary>
    internal static int FramesEnteredForTests => _framesEntered;

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
        _framesEntered++;
        Dispatcher.PushFrame(frame);
        return task.IsCompleted;
    }

    /// <summary>The task's answer; false when the dispatcher shut down
    /// before it completed, or when the save faulted (codex round 2b: a
    /// yes/no caller fails closed — its ticket already logged the fault
    /// once, and nothing escapes the command that asked).</summary>
    internal static bool Result(Dispatcher dispatcher, Task<bool> task) =>
        Until(dispatcher, task) && task.IsCompletedSuccessfully && task.Result;
}
