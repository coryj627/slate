// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// WB-4 (#1316, #1315): work held on a seam, its expiry observed. The house
/// pattern for a fact that reasons about work that "has not landed yet":
/// the fact installs <see cref="Hook"/> on the seam between a worker's read
/// and its publish (the constructor does it), asserts while the work is
/// held — after <see cref="WaitUntilEntered"/> shows the work has done its
/// read and is parked before its publish — releases it in a <c>finally</c>,
/// drains, and then calls <see cref="AssertHeldUntilReleased"/>, so a hold
/// that ran out on a loaded machine fails the fact instead of passing a race
/// it lost.
/// </summary>
/// <remarks>The hook never throws: a throw from a seam lands in the worker,
/// not the fact — the citations leaf's catch swallows it into an error
/// publish, and the seed's or the Files Citing sheet's would fault a tracked
/// task — so the hook records the timeout and the fact observes it. A
/// <see cref="TaskCompletionSource"/>, not a <c>ManualResetEventSlim</c>,
/// so there is nothing to dispose while a worker may still be leaving the
/// hook.</remarks>
internal sealed class HeldWork : IDisposable
{
    private readonly TaskCompletionSource _released =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<Action?> _install;
    private readonly TimeSpan _bound;
    private int _entered;
    private int _inside;
    private int _timedOut;
    private int _uninstalled;

    /// <summary>Hold the work behind the seam <paramref name="install"/>
    /// sets — at most <paramref name="bound"/> (10 s) per worker.</summary>
    public HeldWork(Action<Action?> install, TimeSpan? bound = null)
    {
        _install = install;
        _bound = bound ?? TimeSpan.FromSeconds(10);
        Hook = Wait;
        install(Hook);
    }

    /// <summary>The seam's hook: waits for <see cref="Release"/>, at most
    /// the bound, and records a wait that ran out.</summary>
    public Action Hook { get; }

    /// <summary>Whether a worker has reached the hook.</summary>
    public bool Entered => Volatile.Read(ref _entered) == 1;

    private void Wait()
    {
        _ = Interlocked.Increment(ref _inside);
        Volatile.Write(ref _entered, 1);
        try
        {
            if (!_released.Task.Wait(_bound))
            {
                Volatile.Write(ref _timedOut, 1);
            }
        }
        finally
        {
            _ = Interlocked.Decrement(ref _inside);
        }
    }

    /// <summary>Fails the fact unless a worker reaches the hook within the
    /// bound — so the fact's "not yet" assertions run against work that is
    /// really held, not work that has not started.</summary>
    public void WaitUntilEntered() =>
        Assert.True(
            SpinWait.SpinUntil(() => Entered, _bound),
            "the work never reached its hold");

    /// <summary>Let the held work go, and take the hook off its seam so
    /// later work runs unheld. Idempotent.</summary>
    public void Release()
    {
        _ = _released.TrySetResult();
        if (Interlocked.Exchange(ref _uninstalled, 1) == 0)
        {
            _install(null);
        }
    }

    /// <summary>Fails the fact when a worker's wait ran out before
    /// <see cref="Release"/>: the work was not held while the fact
    /// asserted. It first waits for every worker to leave the hook, so a
    /// call that comes before a never-released wait has run out still
    /// reports the timeout.</summary>
    public void AssertHeldUntilReleased()
    {
        Assert.True(
            SpinWait.SpinUntil(() => Volatile.Read(ref _inside) == 0, _bound + TimeSpan.FromSeconds(5)),
            "a worker never left the hold");
        Assert.False(
            Volatile.Read(ref _timedOut) == 1,
            "the hold timed out before the fact released it");
    }

    public void Dispose() => Release();
}
