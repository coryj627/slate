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

    protected PanelWorkScheduler(bool synchronousForTests)
    {
        _uiContext = SynchronizationContext.Current;
        _synchronous = synchronousForTests;
    }

    protected bool IsShutDown => _isShutDown;

    /// <summary>Workspace teardown: refuse new work. Subclasses
    /// override to also invalidate their in-flight publishes.</summary>
    internal virtual void Shutdown() => _isShutDown = true;

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
        TrackWork(Task.Run(body));
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
