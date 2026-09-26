// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows;

/// <summary>
/// Where the command palette's FFI work runs — never the UI thread (locked
/// decision 05 §4, principle 2: "Callers dispatch off the UI thread";
/// #1275). The palette hands its snapshot load, its ranking and its recents
/// write here, and publishes results back on the thread that owns it.
/// </summary>
internal interface ICommandPaletteWorkLane
{
    /// <summary>
    /// Runs <paramref name="work"/> after every item handed over before it.
    /// A cancelled item that has not started never runs; one that has
    /// started runs to completion, because the FFI call it makes is not
    /// cancellable, and its caller discards the result.
    /// </summary>
    Task<T> Run<T>(Func<T> work, CancellationToken cancellationToken);
}

/// <summary>
/// The production lane: one worker at a time, in hand-over order.
/// </summary>
/// <remarks>
/// <para>
/// Serialized rather than one thread-pool item per call for two reasons.
/// Ordering is a contract: a recents write handed over when a command runs
/// (P9) must land before the next open's recents load (P4) reads the file,
/// and a strict chain gives that without a lock around the store. And a
/// burst of keystrokes cannot pile up ranks: each keystroke cancels the one
/// before it, so a superseded rank still waiting its turn is skipped — the
/// Quick Open lane's shape (<see cref="QuickSwitcherRankCoordinator"/>).
/// </para>
/// <para>
/// Lazy cancellation keeps the chain strict: a cancelled link still waits
/// for its predecessor before it completes, so the link after it can never
/// overtake work that is already running.
/// </para>
/// </remarks>
internal sealed class CommandPaletteWorkLane : ICommandPaletteWorkLane
{
    private readonly object _gate = new();
    private Task _tail = Task.CompletedTask;

    public Task<T> Run<T>(Func<T> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        lock (_gate)
        {
            Task<T> next = _tail.ContinueWith(
                _ => work(),
                cancellationToken,
                TaskContinuationOptions.LazyCancellation,
                TaskScheduler.Default);
            _tail = next;
            return next;
        }
    }
}
