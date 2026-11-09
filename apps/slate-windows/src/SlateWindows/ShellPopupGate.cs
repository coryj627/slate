// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using SlateWindows.Canvas;

namespace SlateWindows;

/// <summary>
/// #1790: whether a surface's own popup may be open. A popup is its own window, and a
/// topmost one (measured: WS_EX_TOPMOST), so it doesn't go with the surface when the
/// surface is hidden (#1786), it sits on top of a sheet's fields, and it stays above other
/// applications with Slate in the background. The trigger that opened it can outlast all
/// three: a selection does. The owner closes its popup while this says no, and asks again
/// when this says something changed, so the popup comes back if its trigger is still there.
/// </summary>
internal sealed class ShellPopupGate
{
    private readonly FrameworkElement _owner;
    private readonly Action _changed;
    private Window? _window;

    internal ShellPopupGate(FrameworkElement owner, Action changed)
    {
        _owner = owner;
        _changed = changed;
        owner.Loaded += (_, _) => Attach();
        owner.Unloaded += (_, _) => Detach();
    }

    /// <summary>Reads the window instead of remembering its events, so an owner that arrives
    /// in a window already in the background answers the same as one that saw it go.</summary>
    internal bool Allows =>
        _owner.IsVisible
        && Window.GetWindow(_owner) is not { IsActive: false }
        && !CanvasSurfaceView.ShellOverlayIsOpen();

    /// <summary>The overlay's event is static, so the subscription is the owner's time in
    /// the tree, and a surface out of it holds no reference from there. Arriving asks too:
    /// the owner has been visible since before its Loaded, with no one telling it.</summary>
    private void Attach()
    {
        Detach();
        _window = Window.GetWindow(_owner);
        if (_window is not null)
        {
            _window.Activated += OnWindowActivationChanged;
            _window.Deactivated += OnWindowActivationChanged;
        }

        CanvasSurfaceView.ShellOverlayChanged += _changed;
        _changed();
    }

    private void Detach()
    {
        if (_window is not null)
        {
            _window.Activated -= OnWindowActivationChanged;
            _window.Deactivated -= OnWindowActivationChanged;
            _window = null;
        }

        CanvasSurfaceView.ShellOverlayChanged -= _changed;
    }

    private void OnWindowActivationChanged(object? sender, EventArgs e) => _changed();
}
