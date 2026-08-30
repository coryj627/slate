// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;

namespace SlateWindows.Tests;

/// <summary>
/// A background thread whose fault reaches the fact instead of the
/// test host: an assertion failing on a raw thread aborts the whole
/// run. The suite's capture-and-rethrow convention, for the canvas
/// batteries that race real threads.
/// </summary>
internal sealed class CanvasWorker
{
    private readonly Thread _thread;
    private ExceptionDispatchInfo? _fault;

    internal CanvasWorker(Action body)
    {
        _thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception fault)
            {
                _fault = ExceptionDispatchInfo.Capture(fault);
            }
        })
        { IsBackground = true };
    }

    /// <summary>Blocked in a wait, sleep or join — a contended lock
    /// reports as one — or already finished.</summary>
    internal bool IsBlockedOrFinished =>
        (_thread.ThreadState
            & (System.Threading.ThreadState.WaitSleepJoin | System.Threading.ThreadState.Stopped))
        != 0;

    internal void Start() => _thread.Start();

    /// <summary>Join, then rethrow whatever the body threw.</summary>
    internal bool Join(TimeSpan budget)
    {
        bool finished = _thread.Join(budget);
        _fault?.Throw();
        return finished;
    }
}
