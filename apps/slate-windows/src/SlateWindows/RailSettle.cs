// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// WB-16 (#1305): the right-pane rail's arrows settle once. An arrow on the
/// rail commits the leaf it reaches at once (S4: the focused row is the shown
/// leaf), but the leaf's reload, the queries refresh and the workspace write
/// wait for the arrows to settle — when the last held rail arrow is released,
/// or focus leaves the rail — never while an arrow is still down, however
/// long the keyboard's first-repeat delay (a fixed timer would fire on the
/// first row before the held sweep began; the dispatcher goes idle between
/// two repeats, so idleness marks nothing either). The poll timer is only a
/// backstop for a release the rail never saw (the seal takes it first): each
/// tick drops the keys the thread's state reports up, and settles only when
/// none is held. Disposal is teardown, never a settle: the workspace's own
/// final write carries the leaf an arrow chose.
/// </summary>
internal sealed class RailSettle : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Func<TimeSpan> _pollInterval;
    private readonly Func<Key, bool> _isKeyDown;
    private readonly Action<WorkspaceLeafOption> _settled;
    private readonly HashSet<Key> _held = [];
    private WorkspaceLeafOption? _armed;
    private bool _disposed;

    internal RailSettle(
        Dispatcher dispatcher, Func<TimeSpan> pollInterval, Func<Key, bool> isKeyDown, Action<WorkspaceLeafOption> settled)
    {
        _pollInterval = pollInterval;
        _isKeyDown = isKeyDown;
        _settled = settled;
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>An arrow's leaf is waiting to settle.</summary>
    internal bool IsArmed => _armed is not null;

    /// <summary>A rail arrow is down.</summary>
    internal bool IsHeld => _held.Count > 0;

    /// <summary>Ticks that found an arrow still down.</summary>
    internal int HeldTicksForTests { get; private set; }

    /// <summary>A rail arrow went down: its first press or an
    /// auto-repeat.</summary>
    internal void Pressed(Key key)
    {
        if (!_disposed)
        {
            _ = _held.Add(key);
        }
    }

    /// <summary>An arrow chose <paramref name="leaf"/>: its deferred work
    /// waits for the settle.</summary>
    internal void Arm(WorkspaceLeafOption leaf)
    {
        if (_disposed)
        {
            return;
        }

        _armed = leaf;
        if (!_timer.IsEnabled)
        {
            _timer.Interval = _pollInterval();
            _timer.Start();
        }
    }

    /// <summary>A rail arrow came up; the last one settles at once.</summary>
    internal void Released(Key key)
    {
        if (_disposed)
        {
            return;
        }

        _ = _held.Remove(key);
        // A key whose release the rail never saw (the seal took it) is
        // dropped here too, so it cannot hold this settle back.
        _ = _held.RemoveWhere(held => !_isKeyDown(held));
        if (_held.Count == 0)
        {
            Settle();
        }
    }

    /// <summary>The keys left the rail: whatever is held, the arrows are
    /// done there.</summary>
    internal void FocusLeft()
    {
        if (_disposed)
        {
            return;
        }

        _held.Clear();
        Settle();
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _held.Clear();
        _armed = null;
    }

    private void Tick()
    {
        if (_disposed)
        {
            _timer.Stop();
            return;
        }

        _ = _held.RemoveWhere(key => !_isKeyDown(key));
        if (_held.Count > 0)
        {
            HeldTicksForTests++;
            return;
        }

        Settle();
    }

    private void Settle()
    {
        _timer.Stop();
        if (_armed is not { } leaf)
        {
            return;
        }

        _armed = null;
        _settled(leaf);
    }
}
