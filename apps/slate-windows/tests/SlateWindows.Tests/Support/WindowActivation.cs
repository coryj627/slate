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

    private static void Send(Window window, nint state) =>
        _ = SendMessage(new WindowInteropHelper(window).Handle, WmActivate, state, 0);

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);
}
