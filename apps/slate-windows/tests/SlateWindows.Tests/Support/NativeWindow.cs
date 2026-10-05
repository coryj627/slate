// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.InteropServices;

namespace SlateWindows.Tests;

/// <summary>
/// The Win32 calls the facts make as a UI Automation client would: one
/// declaration of each and named constants, shared by every suite.
/// </summary>
internal static class NativeWindow
{
    internal const int WmGetObject = 0x003D;

    internal const int WhCallWndProc = 4;

    // UiaRootObjectId: the object id a UIA client sends with WM_GETOBJECT.
    internal static readonly IntPtr UiaRootObjectId = new(-25);

    internal delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    /// <summary>CWPSTRUCT: a sent message as WH_CALLWNDPROC sees it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct SentMessage
    {
        public IntPtr LParam;
        public IntPtr WParam;
        public int Message;
        public IntPtr Window;
    }

    /// <summary>A client's first contact: ask the window for its UI
    /// Automation root, which connects WPF's actual root peer. A freshly
    /// constructed, unconnected test peer can return null for every
    /// provider and would prove nothing.</summary>
    internal static void RequestUiaRoot(IntPtr window) =>
        _ = SendMessage(window, WmGetObject, IntPtr.Zero, UiaRootObjectId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId);

    [DllImport("user32.dll")]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    internal static extern int GetCurrentThreadId();
}
