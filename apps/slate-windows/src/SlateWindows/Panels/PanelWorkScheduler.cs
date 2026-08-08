// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

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
    protected bool IsSynchronousForTests => _synchronous;

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

    internal Task DrainForTests()
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
}
