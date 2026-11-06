// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace SlateWindows;

/// <summary>
/// DS-12 (#1813): what the title bar's hook needs. Windows 11 opens Snap Layouts over a
/// window's maximize button only when <c>WM_NCHITTEST</c> answers <c>HTMAXBUTTON</c> there,
/// which <see cref="System.Windows.Shell.WindowChrome"/> never does. Once it does, Windows
/// owns the mouse over the button, so WPF's own hover and press never fire on it and the
/// hook says them through these two properties.
/// </summary>
internal static class TitleBarChrome
{
    internal const int WmNcHitTest = 0x0084;
    internal const int WmNcMouseMove = 0x00A0;
    internal const int WmNcLButtonDown = 0x00A1;
    internal const int WmNcLButtonUp = 0x00A2;
    internal const int WmNcMouseLeave = 0x02A2;
    internal const int HtMaxButton = 9;

    public static readonly DependencyProperty IsHoveredProperty = DependencyProperty.RegisterAttached(
        "IsHovered", typeof(bool), typeof(TitleBarChrome), new PropertyMetadata(false));

    public static readonly DependencyProperty IsPressedProperty = DependencyProperty.RegisterAttached(
        "IsPressed", typeof(bool), typeof(TitleBarChrome), new PropertyMetadata(false));

    public static bool GetIsHovered(DependencyObject element) => (bool)element.GetValue(IsHoveredProperty);

    public static void SetIsHovered(DependencyObject element, bool value) => element.SetValue(IsHoveredProperty, value);

    public static bool GetIsPressed(DependencyObject element) => (bool)element.GetValue(IsPressedProperty);

    public static void SetIsPressed(DependencyObject element, bool value) => element.SetValue(IsPressedProperty, value);

    /// <summary>Whether a <c>WM_NCHITTEST</c> point, in screen pixels, is over a button whose
    /// top left is <paramref name="origin"/> in screen pixels and whose size is
    /// <paramref name="size"/> in DIPs at <paramref name="scale"/>.</summary>
    internal static bool Contains(Point point, Point origin, Size size, double scale) =>
        point.X >= origin.X
        && point.X < origin.X + (size.Width * scale)
        && point.Y >= origin.Y
        && point.Y < origin.Y + (size.Height * scale);

    /// <summary>Windows' own frame around <paramref name="visual"/>'s window, in DIPs: the
    /// sizing border and the padded border outside it. A native window's edge takes the
    /// pointer over all of it, and a maximized window overhangs the screen by all of it. WPF's
    /// <see cref="SystemParameters.WindowResizeBorderThickness"/> is the sizing border alone.</summary>
    internal static Thickness FrameThickness(Visual visual)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(visual);
        double padded = GetSystemMetricsForDpi(SmCxPaddedBorder, (uint)Math.Round(96 * dpi.DpiScaleX)) / dpi.DpiScaleX;
        Thickness sizing = SystemParameters.WindowResizeBorderThickness;
        return new Thickness(sizing.Left + padded, sizing.Top + padded, sizing.Right + padded, sizing.Bottom + padded);
    }

    /// <summary>The screen point a <c>WM_NCHITTEST</c>'s lParam carries: two signed 16-bit
    /// halves, since a second monitor can sit left of or above the first.</summary>
    internal static Point PointOf(IntPtr lParam)
    {
        long value = lParam.ToInt64();
        return new Point(unchecked((short)(value & 0xFFFF)), unchecked((short)((value >> 16) & 0xFFFF)));
    }

    private const int SmCxPaddedBorder = 92;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);
}
