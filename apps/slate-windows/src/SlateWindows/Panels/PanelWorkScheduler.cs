// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;

namespace SlateWindows.Panels;

/// <summary>
/// The shared panel worker lifecycle (extracted from the W4-2 panels
/// for W4-3): tracked background work with a synchronous test mode,
/// UI-context marshalling, and a shutdown that refuses new work so
/// nothing publishes into a dying UI. Subclasses guard their own
/// publishes with generation/request tokens; bodies catch their own
/// failures, so tracked tasks never fault.
/// </summary>
internal abstract class PanelWorkScheduler : BindableBase
{
    private readonly object _workLock = new();
    private readonly HashSet<Task> _pendingWork = [];
    private readonly SynchronizationContext? _uiContext;
    private readonly bool _synchronous;
    private volatile bool _isShutDown;
    private Task? _prerequisite;

    protected PanelWorkScheduler(bool synchronousForTests)
    {
        _uiContext = SynchronizationContext.Current;
        _synchronous = synchronousForTests;
    }

    protected bool IsShutDown => _isShutDown;

    /// <summary>Whether this scheduler runs bodies inline (test mode)
    /// — for the rare subclass step that must choose between inline
    /// and pool execution OUTSIDE StartWork (e.g. a post-shutdown
    /// handle close that must not block the dispatcher).</summary>
    protected internal bool IsSynchronousForTests => _synchronous;

    /// <summary>
    /// Whether the CURRENT SynchronizationContext is WPF's dispatcher
    /// context — the one UI context whose posts run on the thread
    /// that constructs the panels, serialized with it. Nothing else
    /// qualifies: a null context makes <see cref="Post"/> run the
    /// publish inline on the WORKER, and a test host's context
    /// (xunit wraps every test in one whose Post hands the callback
    /// to the thread pool) runs it on an arbitrary pool thread. Either
    /// way the publish races the constructing thread (#1129: a
    /// history publish enumerated its loaded rows while the test
    /// thread's tab switch cleared them — intermittent on CI). The
    /// workspace consults this at construction: background
    /// interaction work is started only under the dispatcher;
    /// everywhere else it runs inline.
    /// </summary>
    internal static bool CurrentContextIsUiDispatcher() =>
        SynchronizationContext.Current is DispatcherSynchronizationContext;

    /// <summary>Workspace teardown: refuse new work. Subclasses
    /// override to also invalidate their in-flight publishes.</summary>
    internal virtual void Shutdown() => _isShutDown = true;

    /// <summary>Hold every background body until a workspace-level
    /// prerequisite has landed. W4-5: a citation render issued before
    /// SetBibliographySources completes sees no sources and publishes
    /// every key as unresolved, and nothing re-queries afterwards — so
    /// the ordering has to be a real dependency, not a hope about which
    /// Task.Run wins. Synchronous mode needs no gate: the workspace
    /// completes the prerequisite inline before it starts any load.
    /// </summary>
    internal void GateWorkOn(Task prerequisite) => _prerequisite = prerequisite;

    /// <summary>All load work funnels through here. Synchronous mode
    /// (the ReadingContentViewModel test pattern) runs the body
    /// inline: without a UI SynchronizationContext, worker publishes
    /// would land on background threads and race every list the test
    /// thread is reading.</summary>
    protected void StartWork(Action body)
    {
        if (_isShutDown)
        {
            return;
        }
        if (_synchronous)
        {
            body();
            return;
        }
        Task? gate = _prerequisite;
        if (gate is null)
        {
            TrackWork(Task.Run(() => RunIfLive(body)));
            return;
        }
        TrackWork(RunGatedAsync(gate, body));
    }

    /// <summary>
    /// The last gate before touching the session. Teardown can land
    /// between queueing a body and the pool picking it up, and the
    /// check in <see cref="StartWork"/> cannot see that — without this
    /// an ungated body (FilesCiting, which has no prerequisite) calls
    /// into a session the vault lifecycle has already disposed.
    /// </summary>
    private void RunIfLive(Action body)
    {
        if (_isShutDown)
        {
            return;
        }
        body();
    }

    /// <summary>
    /// Wait for the prerequisite WITHOUT occupying a pool thread.
    ///
    /// The first version called <c>gate.Wait()</c> inside a
    /// <c>Task.Run</c>, so every gated request parked a pool thread
    /// while the seed it was waiting for was itself queued on that same
    /// pool — opening several tabs during initialization could starve
    /// the work that would release them.
    /// </summary>
    private async Task RunGatedAsync(Task gate, Action body)
    {
        try
        {
            await gate.ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
        {
            // A faulted prerequisite is NOT this body's failure to
            // report — the seeding path owns that notice. Swallow it so
            // tracked tasks stay non-faulting, then load anyway.
        }
        // Re-checked AFTER the wait. Teardown routinely lands while
        // bodies are parked here, and the check before the wait cannot
        // see it — without this, shutdown publishes into a dying UI.
        if (_isShutDown)
        {
            return;
        }
        // ALWAYS hand the body to the pool, never run it inline.
        //
        // Awaiting an ALREADY-COMPLETED task does not yield: the state
        // machine runs straight through on the caller's thread. The
        // seed settles seconds after vault open and stays settled, so
        // from that moment every gated body would execute on whichever
        // thread called StartWork — and every caller is the UI thread
        // (SyncPanels, the ActiveLeaf reveal). That silently put every
        // citation FFI call, including the whole-vault unresolved
        // query, on the dispatcher. `ConfigureAwait(false)` does not
        // help; it only governs where a SUSPENDED continuation resumes.
        await Task.Run(() => RunIfLive(body)).ConfigureAwait(false);
    }

    private void TrackWork(Task work)
    {
        lock (_workLock)
        {
            _ = _pendingWork.Add(work);
        }
        _ = work.ContinueWith(
            completed =>
            {
                lock (_workLock)
                {
                    _ = _pendingWork.Remove(completed);
                }
            },
            TaskScheduler.Default);
    }

    internal Task DrainForTests() => WhenWorkDrained();

    /// <summary>Every tracked body completed — the test seam, and the
    /// TEARDOWN drain: a worker mid-FFI holds resources whose
    /// finally-close must run before the session disposes (INV-2;
    /// codex round 2 — ephemeral dashboard/preview handles).</summary>
    internal Task WhenWorkDrained()
    {
        Task[] snapshot;
        lock (_workLock)
        {
            snapshot = [.. _pendingWork];
        }
        return Task.WhenAll(snapshot);
    }

    protected void Post(Action action)
    {
        // Synchronous mode runs the publish INLINE: test hosts (xunit)
        // install their own SynchronizationContext, and queueing to it
        // would defer the publish past the assertion that awaits it.
        if (_synchronous || _uiContext is null)
        {
            action();
        }
        else
        {
            _uiContext.Post(_ => action(), null);
        }
    }

    /// <summary>Whether a UI context was captured at construction —
    /// the fact a subclass that refuses to run without one asks
    /// (W6-2 PR A, contract A-2: the graph document has no inline
    /// mode).</summary>
    protected bool HasUiContext => _uiContext is not null;

    /// <summary>
    /// W6-2 PR A (contracts A-2, AD-4; the round-4 ledger's IGA-42, 53,
    /// 60): compute on the pool in EVERY mode — never inline, the
    /// <see cref="RunGatedAsync"/> rule made unconditional — and apply on
    /// the owner context captured at construction, with the tracked task
    /// completing AFTER the apply, so the drains wait for the apply too.
    /// Without a captured context the apply runs where the compute
    /// finished; a subclass that cannot accept that refuses construction
    /// (<see cref="HasUiContext"/>). The compute must not throw (the
    /// tracked-tasks-never-fault rule); a failure is a value it returns.
    /// </summary>
    protected void StartWorkAlwaysAsync<T>(Func<T> compute, Action<T> apply)
    {
        ArgumentNullException.ThrowIfNull(compute);
        ArgumentNullException.ThrowIfNull(apply);
        if (_isShutDown)
        {
            return;
        }
        TrackWork(RunAlwaysAsync(compute, apply));
    }

    private async Task RunAlwaysAsync<T>(Func<T> compute, Action<T> apply)
    {
        if (_prerequisite is { } gate)
        {
            await gate.ConfigureAwait(false);
        }
        if (_isShutDown)
        {
            return;
        }
        T result = await Task.Run(compute).ConfigureAwait(false);
        if (_uiContext is null)
        {
            if (!_isShutDown)
            {
                apply(result);
            }
            return;
        }
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiContext.Post(
            _ =>
            {
                try
                {
                    if (!_isShutDown)
                    {
                        apply(result);
                    }
                }
                finally
                {
                    applied.SetResult();
                }
            },
            null);
        await applied.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// The FIXED-POINT drain (W6-2 PR A, IGA-60): <see cref="WhenWorkDrained"/>
    /// snapshots the tracked set once, so a body an apply enqueued after the
    /// snapshot — the silent replacement pair A-2 requires — could still be
    /// running when it returned. This waits, re-snapshots, and returns only
    /// when nothing tracked remains.
    /// </summary>
    internal async Task WhenAllWorkDrained()
    {
        while (true)
        {
            Task[] snapshot;
            lock (_workLock)
            {
                snapshot = [.. _pendingWork];
            }
            if (snapshot.Length == 0)
            {
                return;
            }
            await Task.WhenAll(snapshot).ConfigureAwait(false);
        }
    }
}
