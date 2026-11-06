// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace SlateWindows;

/// <summary>DS-12 (#1813): the title bar row, its caption buttons and the Snap Layouts hook.</summary>
public partial class MainWindow
{
    private const string MaximizeGlyph = "\uE922";
    private const string RestoreGlyph = "\uE923";
    private bool _trackingMaximizeLeave;

    private void InitializeTitleBar()
    {
        CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (_, _) => SystemCommands.MinimizeWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, (_, _) => SystemCommands.MaximizeWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, (_, _) => SystemCommands.RestoreWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => SystemCommands.CloseWindow(this)));
        StateChanged += (_, _) => FollowWindowState();
        DpiChanged += (_, _) => FollowFrame();
        FollowWindowState();
    }

    /// <summary>WindowChrome adds its own hook as the window's source appears, and WPF asks the
    /// newest hook first, so the title bar's goes in once the event has run: it has to answer
    /// <c>WM_NCHITTEST</c> over maximize before WindowChrome answers it as client.</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        AttachTitleBarHook();
        FollowFrame();
    }

    /// <summary>The edge takes the pointer over Windows' whole frame, as a native window's does,
    /// and the frame changes with the monitor's DPI.</summary>
    private void FollowFrame()
    {
        if (WindowChrome.GetWindowChrome(this) is { } chrome)
        {
            chrome.ResizeBorderThickness = TitleBarChrome.FrameThickness(this);
        }

        FollowWindowState();
    }

    /// <summary>The caption area is the row, which grows with text size.</summary>
    private void TitleBar_SizeChanged(object sender, SizeChangedEventArgs eventArgs)
    {
        if (WindowChrome.GetWindowChrome(this) is { } chrome)
        {
            chrome.CaptionHeight = TitleBar.ActualHeight;
        }
    }

    private void TitleBarPalette_Click(object sender, RoutedEventArgs eventArgs) => _viewModel.Palette.Open();

    /// <summary>The middle caption says what it does, and a maximized window is padded by the
    /// frame Windows lets it overhang the screen with.</summary>
    private void FollowWindowState()
    {
        bool maximized = WindowState == WindowState.Maximized;
        CaptionMaximize.Content = maximized ? RestoreGlyph : MaximizeGlyph;
        CaptionMaximize.Command = maximized ? SystemCommands.RestoreWindowCommand : SystemCommands.MaximizeWindowCommand;
        AutomationProperties.SetName(CaptionMaximize, maximized ? "Restore" : "Maximize");
        if (Content is FrameworkElement root)
        {
            root.Margin = maximized ? TitleBarChrome.FrameThickness(this) : new Thickness(0);
        }
    }

    private void AttachTitleBarHook()
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(TitleBarHook);
        }
    }

    private IntPtr TitleBarHook(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (message)
        {
            case TitleBarChrome.WmNcHitTest when IsOverMaximize(lParam):
                handled = true;
                return new IntPtr(TitleBarChrome.HtMaxButton);
            case TitleBarChrome.WmNcMouseMove:
                bool over = wParam.ToInt32() == TitleBarChrome.HtMaxButton;
                TitleBarChrome.SetIsHovered(CaptionMaximize, over);
                if (!over)
                {
                    TitleBarChrome.SetIsPressed(CaptionMaximize, false);
                }
                else if (!_trackingMaximizeLeave)
                {
                    _trackingMaximizeLeave = TrackNonClientLeave(window);
                }

                break;
            case TitleBarChrome.WmNcMouseLeave:
                _trackingMaximizeLeave = false;
                TitleBarChrome.SetIsHovered(CaptionMaximize, false);
                TitleBarChrome.SetIsPressed(CaptionMaximize, false);
                break;
            case TitleBarChrome.WmNcLButtonDown when wParam.ToInt32() == TitleBarChrome.HtMaxButton:
                TitleBarChrome.SetIsPressed(CaptionMaximize, true);
                handled = true;
                break;
            case TitleBarChrome.WmNcLButtonUp when wParam.ToInt32() == TitleBarChrome.HtMaxButton:
                // A press that moved off and back still counts, as a native caption's does;
                // one released elsewhere never reaches here.
                bool pressed = TitleBarChrome.GetIsPressed(CaptionMaximize);
                TitleBarChrome.SetIsPressed(CaptionMaximize, false);
                handled = true;
                if (pressed && CaptionMaximize.Command.CanExecute(null))
                {
                    CaptionMaximize.Command.Execute(null);
                }

                break;
        }

        return IntPtr.Zero;
    }

    private bool IsOverMaximize(IntPtr lParam)
    {
        if (!CaptionMaximize.IsVisible || PresentationSource.FromVisual(CaptionMaximize) is null)
        {
            return false;
        }

        return TitleBarChrome.Contains(
            TitleBarChrome.PointOf(lParam),
            CaptionMaximize.PointToScreen(new Point(0, 0)),
            new Size(CaptionMaximize.ActualWidth, CaptionMaximize.ActualHeight),
            VisualTreeHelper.GetDpi(CaptionMaximize).DpiScaleX);
    }

    /// <summary>Windows sends <c>WM_NCMOUSELEAVE</c> only to a window that asked for it.</summary>
    private static bool TrackNonClientLeave(IntPtr window)
    {
        var track = new TrackMouseEventInfo
        {
            Size = (uint)Marshal.SizeOf<TrackMouseEventInfo>(),
            Flags = TmeLeave | TmeNonClient,
            Window = window,
        };
        return TrackMouseEvent(ref track);
    }

    private const uint TmeLeave = 0x00000002;
    private const uint TmeNonClient = 0x00000010;

    [StructLayout(LayoutKind.Sequential)]
    private struct TrackMouseEventInfo
    {
        public uint Size;
        public uint Flags;
        public IntPtr Window;
        public uint HoverTime;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool TrackMouseEvent(ref TrackMouseEventInfo track);
}
