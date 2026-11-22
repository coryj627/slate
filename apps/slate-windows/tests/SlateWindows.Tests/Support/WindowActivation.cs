// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SlateWindows.Tests;

/// <summary>A hosted window's activation, without the desktop's foreground. Hosted windows
/// show unactivated, because a test runner can't count on taking the foreground, so
/// <see cref="Window.IsActive"/> is false unless a fact says otherwise. WM_ACTIVATE is what
/// Windows would send: the window updates <c>IsActive</c> and raises its own event.</summary>
internal static class WindowActivation
{
    private const int WmActivate = 0x0006;

    internal static void Activate(Window window) => Send(window, 1);

    internal static void Deactivate(Window window) => Send(window, 0);

    /// <summary>The reader leaving the window, as WPF hears it: WM_ACTIVATE for inactive,
    /// then WM_KILLFOCUS, at which WPF remembers the element that holds the keys and takes
    /// them off it. The keys are on nothing after it.</summary>
    internal static void GoAway(Window window)
    {
        Deactivate(window);
        _ = SendMessage(new WindowInteropHelper(window).Handle, WmKillFocus, 0, 0);
    }

    /// <summary>The reader coming back, or a box over the window closing: WM_ACTIVATE for
    /// active, then WM_SETFOCUS, at which WPF hands the keys back to the element it
    /// remembered, and to the window when that element refuses them.</summary>
    internal static void ComeBack(Window window)
    {
        Activate(window);
        _ = SendMessage(new WindowInteropHelper(window).Handle, WmSetFocus, 0, 0);
    }

    private const int WmSetFocus = 0x0007;
    private const int WmKillFocus = 0x0008;

    private static void Send(Window window, nint state) =>
        _ = SendMessage(new WindowInteropHelper(window).Handle, WmActivate, state, 0);

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);
}
