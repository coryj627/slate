// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.InteropServices;

namespace SlateWindows;

/// <summary>Runtime census seam proving that the executable manifest took effect.</summary>
internal static class DpiAwarenessProbe
{
    private static readonly IntPtr PerMonitorAware = new(-3);
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);

    public static bool EnsurePerMonitorV2()
    {
        if (IsPerMonitorV2())
        {
            return true;
        }

        bool established = NativeMethods.SetThreadDpiAwarenessContext(PerMonitorAwareV2) != IntPtr.Zero
            && IsPerMonitorV2();
        if (!established)
        {
            WriteCurrentContextDiagnostic();
        }

        return established;
    }

    public static bool IsPerMonitorV2()
    {
        IntPtr current = NativeMethods.GetThreadDpiAwarenessContext();
        return current != IntPtr.Zero
            && NativeMethods.AreDpiAwarenessContextsEqual(current, PerMonitorAwareV2);
    }

    public static bool IsWindowPerMonitorV2(IntPtr windowHandle)
    {
        IntPtr context = NativeMethods.GetWindowDpiAwarenessContext(windowHandle);
        return context != IntPtr.Zero
            && NativeMethods.AreDpiAwarenessContextsEqual(context, PerMonitorAwareV2);
    }

    private static void WriteCurrentContextDiagnostic()
    {
        IntPtr current = NativeMethods.GetThreadDpiAwarenessContext();
        bool isV1 = current != IntPtr.Zero
            && NativeMethods.AreDpiAwarenessContextsEqual(current, PerMonitorAware);
        int awareness = current == IntPtr.Zero
            ? -1
            : NativeMethods.GetAwarenessFromDpiAwarenessContext(current);
        HostLog.Write(HostDiagnosticEvent.DpiCensusFailed);
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern IntPtr GetThreadDpiAwarenessContext();

        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindowDpiAwarenessContext(IntPtr windowHandle);

        [DllImport("user32.dll")]
        internal static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AreDpiAwarenessContextsEqual(
            IntPtr firstContext,
            IntPtr secondContext);

        [DllImport("user32.dll")]
        internal static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
    }
}
