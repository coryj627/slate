// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SlateWindows;

/// <summary>Bridges WPF window lifetime to native per-monitor placement data.</summary>
internal sealed class WindowPlacementManager
{
    private readonly Window _window;
    private readonly WindowStateStore _store;

    public WindowPlacementManager(Window window, WindowStateStore? store = null)
    {
        _window = window;
        _store = store ?? new WindowStateStore();
    }

    public void Restore()
    {
        WindowPlacementState? state = _store.Load();
        if (state is null)
        {
            return;
        }

        IntPtr handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        IReadOnlyList<NativeMonitor> nativeMonitors = EnumerateMonitors();
        WindowPlacementPlan? plan = WindowPlacementPlanner.Plan(
            state,
            nativeMonitors.Select(monitor => monitor.Snapshot),
            _window.MinWidth,
            _window.MinHeight);
        if (plan is null)
        {
            return;
        }

        const SetWindowPosFlags flags = SetWindowPosFlags.NoActivate | SetWindowPosFlags.NoZOrder;
        if (!NativeMethods.SetWindowPos(
                handle,
                IntPtr.Zero,
                plan.Bounds.Left,
                plan.Bounds.Top,
                plan.Bounds.Width,
                plan.Bounds.Height,
                flags))
        {
            Console.Error.WriteLine(
                $"WindowPlacementManager: SetWindowPos failed ({Marshal.GetLastWin32Error()}).");
            return;
        }

        if (plan.IsMaximized)
        {
            _window.WindowState = WindowState.Maximized;
        }
    }

    public void Save()
    {
        IntPtr handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero || !TryGetNormalBounds(handle, out PixelRect bounds))
        {
            return;
        }

        IntPtr monitorHandle = NativeMethods.MonitorFromWindow(handle, MonitorDefault.Nearest);
        if (!TryReadMonitor(monitorHandle, out NativeMonitor monitor))
        {
            return;
        }

        uint dpi = NativeMethods.GetDpiForWindow(handle);
        if (dpi is < 48 or > 960)
        {
            dpi = monitor.Snapshot.Dpi;
        }

        var state = new WindowPlacementState(
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            _window.WindowState == WindowState.Maximized,
            monitor.Snapshot.DeviceName,
            monitor.Snapshot.WorkArea.Left,
            monitor.Snapshot.WorkArea.Top,
            monitor.Snapshot.WorkArea.Width,
            monitor.Snapshot.WorkArea.Height,
            dpi);

        try
        {
            _store.Save(state);
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"WindowPlacementManager: could not persist window state: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            Console.Error.WriteLine($"WindowPlacementManager: could not persist window state: {exception.Message}");
        }
    }

    private bool TryGetNormalBounds(IntPtr handle, out PixelRect bounds)
    {
        if (_window.WindowState == WindowState.Normal
            && NativeMethods.GetWindowRect(handle, out NativeRect nativeBounds))
        {
            bounds = nativeBounds.ToPixelRect();
            return bounds.Width > 0 && bounds.Height > 0;
        }

        Rect restoreBounds = _window.RestoreBounds;
        if (restoreBounds.IsEmpty
            || !double.IsFinite(restoreBounds.Left)
            || !double.IsFinite(restoreBounds.Top)
            || !double.IsFinite(restoreBounds.Width)
            || !double.IsFinite(restoreBounds.Height)
            || restoreBounds.Width <= 0
            || restoreBounds.Height <= 0)
        {
            bounds = default;
            return false;
        }

        DpiScale dpi = VisualTreeHelper.GetDpi(_window);
        bounds = new PixelRect(
            RoundToInt(restoreBounds.Left * dpi.DpiScaleX),
            RoundToInt(restoreBounds.Top * dpi.DpiScaleY),
            RoundToInt(restoreBounds.Width * dpi.DpiScaleX),
            RoundToInt(restoreBounds.Height * dpi.DpiScaleY));
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static IReadOnlyList<NativeMonitor> EnumerateMonitors()
    {
        var monitors = new List<NativeMonitor>();
        NativeMethods.MonitorEnumProcedure callback = (monitorHandle, _, _, _) =>
        {
            if (TryReadMonitor(monitorHandle, out NativeMonitor monitor))
            {
                monitors.Add(monitor);
            }

            return true;
        };

        if (!NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero))
        {
            Console.Error.WriteLine(
                $"WindowPlacementManager: monitor enumeration failed ({Marshal.GetLastWin32Error()}).");
        }

        GC.KeepAlive(callback);
        return monitors;
    }

    private static bool TryReadMonitor(IntPtr handle, out NativeMonitor monitor)
    {
        var info = new MonitorInfoEx
        {
            Size = Marshal.SizeOf<MonitorInfoEx>(),
            DeviceName = string.Empty,
        };
        if (handle == IntPtr.Zero || !NativeMethods.GetMonitorInfo(handle, ref info))
        {
            monitor = null!;
            return false;
        }

        uint dpi = 96;
        try
        {
            if (NativeMethods.GetDpiForMonitor(handle, MonitorDpiType.Effective, out uint dpiX, out _) == 0)
            {
                dpi = dpiX;
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }

        monitor = new NativeMonitor(
            handle,
            new MonitorSnapshot(
                info.DeviceName,
                info.WorkArea.ToPixelRect(),
                dpi,
                (info.Flags & MonitorInfoFlags.Primary) != 0));
        return true;
    }

    private static int RoundToInt(double value) =>
        checked((int)Math.Round(value, MidpointRounding.AwayFromZero));

    private sealed record NativeMonitor(IntPtr Handle, MonitorSnapshot Snapshot);

    [Flags]
    private enum SetWindowPosFlags : uint
    {
        NoZOrder = 0x0004,
        NoActivate = 0x0010,
    }

    private enum MonitorDefault : uint
    {
        Nearest = 0x00000002,
    }

    private enum MonitorDpiType
    {
        Effective = 0,
    }

    [Flags]
    private enum MonitorInfoFlags : uint
    {
        Primary = 0x00000001,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly PixelRect ToPixelRect() => new(Left, Top, Right - Left, Bottom - Top);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public MonitorInfoFlags Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    private static partial class NativeMethods
    {
        internal delegate bool MonitorEnumProcedure(
            IntPtr monitorHandle,
            IntPtr monitorDeviceContext,
            IntPtr monitorRectangle,
            IntPtr userData);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumDisplayMonitors(
            IntPtr deviceContext,
            IntPtr clippingRectangle,
            MonitorEnumProcedure callback,
            IntPtr userData);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMonitorInfo(IntPtr monitorHandle, ref MonitorInfoEx monitorInfo);

        [DllImport("user32.dll")]
        internal static extern IntPtr MonitorFromWindow(IntPtr windowHandle, MonitorDefault flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect rectangle);

        [DllImport("user32.dll")]
        internal static extern uint GetDpiForWindow(IntPtr windowHandle);

        [DllImport("shcore.dll")]
        internal static extern int GetDpiForMonitor(
            IntPtr monitorHandle,
            MonitorDpiType dpiType,
            out uint dpiX,
            out uint dpiY);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(
            IntPtr windowHandle,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            SetWindowPosFlags flags);
    }
}
