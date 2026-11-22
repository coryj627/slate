// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace SlateWindows.Tests;

/// <summary>
/// A key held down, as the keyboard sends it: one KeyDown, then KeyDowns marked as the key
/// repeating (<see cref="KeyEventArgs.IsRepeat"/>), then one KeyUp, each through WPF's input
/// pipeline to the element holding the keys. WPF gives a test no public way to mark a key
/// event as a repeat; its own keyboard device sets the mark through an internal method,
/// which this calls.
/// </summary>
internal static class HeldKeys
{
    private static readonly MethodInfo SetRepeat =
        typeof(KeyEventArgs).GetMethod("SetRepeat", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("WPF's KeyEventArgs has no SetRepeat to mark a repeat with");

    /// <summary>Holds <paramref name="key"/> down in <paramref name="window"/>'s input source
    /// for <paramref name="repeats"/> repeats, then lets it go.</summary>
    internal static void Hold(Window window, Key key, int repeats)
    {
        PresentationSource source = PresentationSource.FromVisual(window)
            ?? throw new InvalidOperationException("the window has no presentation source");
        Send(source, key, Keyboard.PreviewKeyDownEvent, repeat: false);
        for (int index = 0; index < repeats; index++)
        {
            Send(source, key, Keyboard.PreviewKeyDownEvent, repeat: true);
        }

        Send(source, key, Keyboard.PreviewKeyUpEvent, repeat: false);
    }

    private static void Send(PresentationSource source, Key key, RoutedEvent direction, bool repeat)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = direction };
        if (repeat)
        {
            _ = SetRepeat.Invoke(args, [true]);
        }

        _ = InputManager.Current.ProcessInput(args);
    }
}
