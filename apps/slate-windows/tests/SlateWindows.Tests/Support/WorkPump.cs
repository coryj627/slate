// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;

namespace SlateWindows.Tests.Support;

/// <summary>
/// A dedicated single-threaded work pump — the census stand-in for a UI
/// dispatcher. Work items posted to it execute in order on its one
/// thread, so a census can (a) originate FFI calls from a "UI thread"
/// that stays busy processing real work, and (b) assert thread affinity
/// of foreign callbacks against <see cref="ManagedThreadId"/>.
/// </summary>
internal sealed class WorkPump : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;

    public WorkPump()
    {
        _thread = new Thread(() =>
        {
            foreach (var work in _queue.GetConsumingEnumerable())
            {
                work();
            }
        })
        {
            IsBackground = true,
            Name = "census-ui-pump",
        };
        _thread.Start();
    }

    public int ManagedThreadId => _thread.ManagedThreadId;

    /// <summary>Post a work item; the returned task completes (or faults)
    /// when the pump thread has executed it.</summary>
    public Task Post(Action work)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try
            {
                work();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(10));
    }
}
