// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// #1673: what a fixture's teardown did for the work it let go. A fixture
/// that releases held work must wait for that work before its session goes;
/// this is how a fact sees that it did, with no sleep. The fixture calls
/// <see cref="DrainBeforeTheSessionGoes"/> where it waits and
/// <see cref="SessionGone"/> right after the session's <c>Dispose</c>. A
/// fact that arms it puts <see cref="Hold"/> in the fixture's own seam, so a
/// worker stands in the seam until one of those two calls comes. With the
/// right drain in place the worker runs while the session lives and the
/// drain is still waiting for it. Without a drain the worker can only run
/// after the session is gone, and with a drain that does not cover it the
/// worker finds that drain already ended while it is still in the seam; the
/// witness counts both.
/// </summary>
/// <remarks>Built on <see cref="TaskCompletionSource"/>, as
/// <see cref="HeldWork"/> is, so the witness has nothing to dispose while a
/// worker may still be leaving the hold.</remarks>
internal sealed class TeardownWitness
{
    /// <summary>How long a worker stands in the hold when the fixture never
    /// tears down, and how long a fact waits for a worker.</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    /// <summary>Completed when the fixture starts to drain or says its
    /// session is gone, whichever comes first.</summary>
    private readonly TaskCompletionSource _opened =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _entered =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _passed =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _drainsGate = new();
    private readonly List<Task> _drains = [];
    private int _gone;
    private int _lateRuns;
    private int _runsOutsideTheDrain;
    private int _timedOut;
    private int _drainedBeforeGone = -1;

    /// <summary>The seam's side. Blocks until the fixture is draining or its
    /// session is gone, at most 30 seconds. Answers true for a late run, one
    /// that leaves the hold with the session already gone (or was never let
    /// through), so the seam can stop the worker before it calls a disposed
    /// session.</summary>
    public bool Hold()
    {
        _ = _entered.TrySetResult();
        bool opened = _opened.Task.Wait(Bound);
        if (!opened)
        {
            Volatile.Write(ref _timedOut, 1);
        }
        bool late = !opened || Volatile.Read(ref _gone) == 1;
        if (late)
        {
            _ = Interlocked.Increment(ref _lateRuns);
        }
        else if (TheDrainHasEnded())
        {
            // This worker is still in the seam, so a drain that has already
            // ended did not wait for it.
            _ = Interlocked.Increment(ref _runsOutsideTheDrain);
        }
        _ = _passed.TrySetResult();
        return late;
    }

    /// <summary>The fixture's side: takes the product's drain of the work the
    /// fixture released, says the fixture is now waiting for it, then pumps
    /// until that drain ends and observes its fault. One call on purpose: a
    /// fixture that loses its drain loses the signal with it, so an armed
    /// fact goes red, not racy.</summary>
    /// <param name="work">The product's own drain of the released work.</param>
    /// <param name="bound">How long the drain may take; ten seconds when
    /// omitted.</param>
    public void DrainBeforeTheSessionGoes(Func<Task> work, TimeSpan? bound = null)
    {
        Task drain = work();
        lock (_drainsGate)
        {
            _drains.Add(drain);
        }
        _ = _opened.TrySetResult();
        PumpedDispatcher.PumpUntilDrained(drain, bound ?? TimeSpan.FromSeconds(10));
    }

    /// <summary>The fixture's side, right after its session's
    /// <c>Dispose</c>: records whether every drain had ended by then, and
    /// opens the hold for a worker no drain let through.</summary>
    public void SessionGone()
    {
        bool drained;
        lock (_drainsGate)
        {
            drained = _drains.Count > 0 && _drains.TrueForAll(drain => drain.IsCompleted);
        }
        _ = Interlocked.CompareExchange(ref _drainedBeforeGone, drained ? 1 : 0, -1);
        Volatile.Write(ref _gone, 1);
        _ = _opened.TrySetResult();
    }

    private bool TheDrainHasEnded()
    {
        lock (_drainsGate)
        {
            return _drains.Count > 0 && _drains[^1].IsCompleted;
        }
    }

    /// <summary>Fails the fact unless a worker reaches the hold.</summary>
    public void WaitUntilEntered() =>
        Assert.True(_entered.Task.Wait(Bound), "no worker reached the teardown hold");

    /// <summary>Fails the fact unless a worker has left the hold.</summary>
    public void WaitUntilPassed() =>
        Assert.True(_passed.Task.Wait(Bound + TimeSpan.FromSeconds(5)), "no worker left the teardown hold");

    /// <summary>Workers that left the hold with the session already gone, or
    /// were never let through at all.</summary>
    public int LateRuns => Volatile.Read(ref _lateRuns);

    /// <summary>Workers that left the hold while the session lived but found
    /// the fixture's drain already ended: the drain did not wait for them,
    /// so nothing kept the session alive under them.</summary>
    public int RunsOutsideTheDrain => Volatile.Read(ref _runsOutsideTheDrain);

    /// <summary>Whether a worker's hold ran out its bound: the fixture
    /// neither drained nor said its session was gone.</summary>
    public bool TimedOut => Volatile.Read(ref _timedOut) == 1;

    /// <summary>Whether the fixture drained, and every drain had ended,
    /// before it said its session was gone.</summary>
    public bool DrainedBeforeGone => Volatile.Read(ref _drainedBeforeGone) == 1;

    /// <summary>The three things an armed fact asserts once its worker has
    /// left the hold: no late run, no run the drain did not wait for, and
    /// the drain ended before the session went.</summary>
    public void AssertTheTeardownWaited(string what)
    {
        WaitUntilPassed();
        Assert.True(LateRuns == 0, $"{what} ran after the session was gone ({LateRuns} late)");
        Assert.True(
            RunsOutsideTheDrain == 0,
            $"the teardown's drain had ended while {what} was still running: it does not wait for that work");
        Assert.True(DrainedBeforeGone, $"the session went before the teardown's drain of {what} had ended");
    }
}
