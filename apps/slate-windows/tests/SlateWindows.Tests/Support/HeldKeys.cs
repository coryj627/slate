// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SlateWindows.Tests;

/// <summary>
/// Keys as the keyboard sends them: WM_KEYDOWN and WM_KEYUP posted to the window and
/// dispatched through its message loop, so WPF reads them as it reads a real keyboard and
/// <see cref="ComponentDispatcher.CurrentKeyboardMessage"/> carries each one while its key
/// events run. A key held down repeats as WM_KEYDOWN with the previous-key-state bit set
/// (lParam bit 30), which is how the shell tells a held key from a new press (plan 47
/// CB-17b; Codex round 2: WPF's own <see cref="KeyEventArgs.IsRepeat"/> is its guess from
/// the last key-down and key-up it saw).
/// </summary>
internal static class HeldKeys
{
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int PreviousKeyState = 1 << 30;
    private const int TransitionState = 1 << 31;

    /// <summary>Holds <paramref name="key"/> down in <paramref name="window"/> for
    /// <paramref name="repeats"/> repeats, then lets it go, and dispatches the
    /// messages.</summary>
    internal static void Hold(Window window, Key key, int repeats)
    {
        Down(window, key, held: false);
        for (int index = 0; index < repeats; index++)
        {
            Down(window, key, held: true);
        }

        Up(window, key);
    }

    /// <summary>One WM_KEYDOWN, a new press (<paramref name="held"/> false) or the key held
    /// down repeating (true), dispatched.</summary>
    internal static void Down(Window window, Key key, bool held)
    {
        int lParam = 1 | (ScanCode(key) << 16) | (held ? PreviousKeyState : 0);
        Post(window, WmKeyDown, key, lParam);
    }

    /// <summary>One WM_KEYUP, dispatched.</summary>
    internal static void Up(Window window, Key key)
    {
        int lParam = 1 | (ScanCode(key) << 16) | PreviousKeyState | TransitionState;
        Post(window, WmKeyUp, key, lParam);
    }

    private static void Post(Window window, int message, Key key, int lParam)
    {
        IntPtr handle = new WindowInteropHelper(window).Handle;
        Assert.True(handle != IntPtr.Zero, "the window has no handle to post a key to");
        Assert.True(
            PostMessage(handle, message, (IntPtr)KeyInterop.VirtualKeyFromKey(key), (IntPtr)lParam),
            $"PostMessage failed for {key}: {Marshal.GetLastWin32Error()}");
        PumpedDispatcher.Drain();
    }

    private static int ScanCode(Key key) => (int)MapVirtualKey((uint)KeyInterop.VirtualKeyFromKey(key), 0) & 0xFF;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);
}
