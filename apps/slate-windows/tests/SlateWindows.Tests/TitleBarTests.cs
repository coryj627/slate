// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;

namespace SlateWindows.Tests;

/// <summary>DS-12 (#1813): the title bar is one 48 row with Snap Layouts.</summary>
public sealed class TitleBarTests
{
    /// <summary>DSD9: the window says the note, the vault and the app, from core, and follows
    /// the active tab through a switch, a rename and a close.</summary>
    [Fact]
    public void TheTitleFollowsTheActiveNote() => RunSta(() =>
    {
        using var host = new ShownShell(("Why Slate.md", "# Why\n"), ("Golf gateway.md", "# Golf\n"));
        Assert.Equal("Slate", host.Shell.Title);

        typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.VaultDisplayName))!
            .SetValue(host.Lifecycle, "demo-vault");
        Assert.Equal("demo-vault — Slate", host.Shell.Title);

        host.Workspace.OpenPath("Why Slate.md");
        host.Settle();
        Assert.Equal("Why Slate — demo-vault — Slate", host.Shell.Title);

        host.Workspace.OpenPath("Golf gateway.md", WorkspaceOpenTarget.NewTab);
        host.Settle();
        Assert.Equal("Golf gateway — demo-vault — Slate", host.Shell.Title);

        host.Workspace.RetargetPath("Golf gateway.md", "Golf course.md");
        host.Settle();
        Assert.Equal("Golf course — demo-vault — Slate", host.Shell.Title);

        host.Workspace.CloseActiveTabCommand.Execute(null);
        host.Settle();
        Assert.Equal("Why Slate — demo-vault — Slate", host.Shell.Title);

        // A saved query's or a dashboard's rename goes through the tab alone.
        host.Workspace.OpenDashboard("plan", "Plan");
        host.Settle();
        Assert.Equal("Plan — demo-vault — Slate", host.Shell.Title);
        host.Workspace.ActiveGroup.ActiveTab!.RetargetName("Roadmap");
        host.Settle();
        Assert.Equal("Roadmap — demo-vault — Slate", host.Shell.Title);
    });

    /// <summary>At the narrowest window and the largest text, the menus wrap in what the quick
    /// commands and captions leave, and Close stays on the window.</summary>
    [Fact]
    public void AtTheNarrowestWindowAndLargestTextTheCaptionsStayOn() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.Shell.Width = host.Shell.MinWidth;
        int count = ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false).Length;
        for (int index = 0; index < count; index++)
        {
            host.Shell.Resources.MergedDictionaries.RemoveAt(0);
        }

        ResourceDictionary[] scaled = ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false, textScale: 2.25);
        for (int index = 0; index < scaled.Length; index++)
        {
            host.Shell.Resources.MergedDictionaries.Insert(index, scaled[index]);
        }

        host.Settle();

        Button close = Named(host, "CaptionClose");
        double right = close.TranslatePoint(new Point(close.ActualWidth, 0), host.Shell.TitleBar).X;
        Assert.True(right <= host.Shell.TitleBar.ActualWidth + 0.5, $"Close ends at {right} in a {host.Shell.TitleBar.ActualWidth} row.");
        Assert.True(host.Shell.TitleBar.ActualHeight > 48, $"the menus didn't wrap: the row is {host.Shell.TitleBar.ActualHeight}.");
        Assert.Equal(host.Shell.TitleBar.ActualHeight, WindowChrome.GetWindowChrome(host.Shell).CaptionHeight);
    });

    /// <summary>The window extends no glass frame: any, WindowChrome's default of the caption's
    /// height included, takes ClearType from all of its text (the gate's
    /// <c>ThemedShell_APalettesTextKeepsItsClearType</c> measures it). Windows 11 still rounds
    /// the corners.</summary>
    [Fact]
    public void TheWindowExtendsNoGlassSoTextKeepsClearType() => RunSta(() =>
    {
        using var host = new ShownShell();

        Assert.Equal(new Thickness(0), WindowChrome.GetWindowChrome(host.Shell).GlassFrameThickness);
    });

    /// <summary>The frame comes from one DPI's metrics: Windows' sizing and padded borders,
    /// in pixels at that DPI, as DIPs.</summary>
    [Theory]
    [InlineData(4, 4, 4, 1.0, 8.0, 8.0)]
    [InlineData(5, 5, 8, 2.0, 6.5, 6.5)]
    [InlineData(5, 6, 6, 1.5, 7.333, 8.0)]
    public void TheFrameIsOneDpisMetrics(int sizingX, int sizingY, int padded, double scale, double side, double edge)
    {
        Thickness frame = TitleBarChrome.FrameThickness(sizingX, sizingY, padded, scale, scale);

        Assert.Equal(side, frame.Left, 3);
        Assert.Equal(side, frame.Right, 3);
        Assert.Equal(edge, frame.Top, 3);
        Assert.Equal(edge, frame.Bottom, 3);
    }

    /// <summary>One row, 48 at body text, with the caption area the same height.</summary>
    [Fact]
    public void TheTitleRowIsFortyEightAtBodyText() => RunSta(() =>
    {
        using var host = new ShownShell();

        Assert.Equal(48, host.Shell.TitleBar.ActualHeight);
        Assert.Equal(48, WindowChrome.GetWindowChrome(host.Shell).CaptionHeight);
        Assert.Equal(Dock.Top, DockPanel.GetDock(host.Shell.TitleBar));
        Assert.Same(host.Shell.TitleBar, VisualTreeHelper.GetParent(host.Shell.MainMenu.Parent));
    });

    /// <summary>At 225% Text size everything in the row still fits it: DS-4's top-level menu
    /// item is 42 there, so the row stays 48, and the captions stay 46 by 48 as Windows' do.
    /// When the row is taller, the caption area follows it.</summary>
    [Fact]
    public void TheRowHoldsTextSizeAndTheCaptionAreaFollowsIt() => RunSta(() =>
    {
        using var host = new ShownShell();
        int count = ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false).Length;
        for (int index = 0; index < count; index++)
        {
            host.Shell.Resources.MergedDictionaries.RemoveAt(0);
        }

        ResourceDictionary[] scaled = ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false, textScale: 2.25);
        for (int index = 0; index < scaled.Length; index++)
        {
            host.Shell.Resources.MergedDictionaries.Insert(index, scaled[index]);
        }

        host.Settle();
        Assert.Equal(31.5, host.Shell.MainMenu.FontSize);
        foreach (FrameworkElement child in host.Shell.TitleBar.Children.OfType<FrameworkElement>())
        {
            Assert.True(
                child.DesiredSize.Height <= host.Shell.TitleBar.ActualHeight,
                $"{child.GetType().Name} wants {child.DesiredSize.Height} in a {host.Shell.TitleBar.ActualHeight} row.");
        }

        Button maximize = Named(host, "CaptionMaximize");
        Assert.Equal(new Size(46, 48), new Size(maximize.ActualWidth, maximize.ActualHeight));

        host.Shell.TitleBar.MinHeight = 60;
        host.Settle();
        Assert.Equal(60, host.Shell.TitleBar.ActualHeight);
        Assert.Equal(60, WindowChrome.GetWindowChrome(host.Shell).CaptionHeight);
    });

    /// <summary>What takes a pointer in the caption area, and what drags the window.</summary>
    [Fact]
    public void TheChromeButtonsAreHitTestVisibleAndTheTitleIsNot() => RunSta(() =>
    {
        using var host = new ShownShell();

        Assert.True(WindowChrome.GetIsHitTestVisibleInChrome(host.Shell.MainMenu));
        foreach (string id in new[] { "TitleBarPalette", "TitleBarSettings", "CaptionMinimize", "CaptionMaximize", "CaptionClose" })
        {
            Button button = Named(host, id);
            Assert.True(WindowChrome.GetIsHitTestVisibleInChrome(button), $"{id} would drag the window.");
            Assert.False(button.Focusable, $"{id} is in the Tab order.");
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)), $"{id} has no name.");
        }

        AutomationPresentationTextBlock title = Assert.Single(
            ShownShell.Descendants(host.Shell.TitleBar).OfType<AutomationPresentationTextBlock>(),
            block => ReferenceEquals(VisualTreeHelper.GetParent(block), host.Shell.MainMenu.Parent));
        Assert.False(WindowChrome.GetIsHitTestVisibleInChrome(title));
        Assert.False(WindowChrome.GetIsHitTestVisibleInChrome(host.Shell.TitleBar));
        Assert.Equal(host.Shell.Title, title.Text);
    });

    /// <summary>Snap Layouts: the hook answers HTMAXBUTTON over the button's own pixels at any
    /// scale, and its edges are where the button's are.</summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void HitTestOverMaximizeSaysMaxButton(double scale)
    {
        var origin = new Point(1000, 8);
        var size = new Size(46, 48);
        double right = origin.X + (46 * scale);
        double bottom = origin.Y + (48 * scale);

        Assert.True(TitleBarChrome.Contains(origin, origin, size, scale));
        Assert.True(TitleBarChrome.Contains(new Point(right - 1, bottom - 1), origin, size, scale));
        Assert.False(TitleBarChrome.Contains(new Point(right, origin.Y), origin, size, scale));
        Assert.False(TitleBarChrome.Contains(new Point(origin.X, bottom), origin, size, scale));
        Assert.False(TitleBarChrome.Contains(new Point(origin.X - 1, origin.Y), origin, size, scale));
    }

    /// <summary>The window itself answers HTMAXBUTTON over maximize, so Snap Layouts opens.
    /// WindowChrome answers every hit test it's shown, so the title bar's hook has to be
    /// asked first. The hosted window is far off screen, so the point is negative too.</summary>
    [Fact]
    public void TheWindowAnswersMaxButtonOverMaximize() => RunSta(() =>
    {
        using var host = new ShownShell();
        Button maximize = Named(host, "CaptionMaximize");
        Point centre = maximize.PointToScreen(new Point(maximize.ActualWidth / 2, maximize.ActualHeight / 2));
        IntPtr window = new System.Windows.Interop.WindowInteropHelper(host.Shell).Handle;

        Assert.Equal(TitleBarChrome.HtMaxButton, HitTest(window, centre));
        Point title = host.Shell.TitleBar.PointToScreen(new Point(host.Shell.TitleBar.ActualWidth / 2, 24));
        Assert.Equal(HtCaption, HitTest(window, title));
    });

    /// <summary>Windows' own frame is its sizing border and the padding around it, which is
    /// what Windows adds to a standard window's sides and bottom. WPF's
    /// WindowResizeBorderThickness is the first alone, so both the resize zone and a maximized
    /// window's padding take the whole frame. Some machines pad nothing (a CI VM's frame is
    /// WPF's 4), so the frame is checked against Windows, not against WPF's.</summary>
    [Fact]
    public void TheResizeZoneIsWindowsWholeFrame() => RunSta(() =>
    {
        using var host = new ShownShell();
        Thickness frame = TitleBarChrome.FrameThickness(host.Shell);
        IntPtr window = new System.Windows.Interop.WindowInteropHelper(host.Shell).Handle;
        uint dpi = GetDpiForWindow(window);
        double scale = dpi / 96.0;
        var standard = default(NativeRect);
        Assert.True(AdjustWindowRectExForDpi(ref standard, WsOverlappedWindow, false, 0, dpi));

        Assert.Equal(-standard.Left / scale, frame.Left, 3);
        Assert.Equal(standard.Right / scale, frame.Right, 3);
        Assert.Equal(standard.Bottom / scale, frame.Bottom, 3);
        Assert.Equal(frame, WindowChrome.GetWindowChrome(host.Shell).ResizeBorderThickness);
    });

    private const uint WsOverlappedWindow = 0x00CF0000;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool AdjustWindowRectExForDpi(ref NativeRect rect, uint style, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] bool menu, uint extendedStyle, uint dpi);

    /// <summary>Over maximize Windows owns the mouse, so the hook draws the hover and the
    /// press, and a press released on the button is a click. A release that no press began,
    /// as when one started elsewhere, does nothing.</summary>
    [Fact]
    public void TheHookDrawsHoverAndPressAndClicksMaximize() => RunSta(() =>
    {
        using var host = new ShownShell();
        Button maximize = Named(host, "CaptionMaximize");
        IntPtr window = new System.Windows.Interop.WindowInteropHelper(host.Shell).Handle;
        var overMaximize = new IntPtr(TitleBarChrome.HtMaxButton);

        _ = SendMessage(window, TitleBarChrome.WmNcLButtonUp, overMaximize, IntPtr.Zero);
        host.Settle();
        Assert.Equal(WindowState.Normal, host.Shell.WindowState);

        _ = SendMessage(window, TitleBarChrome.WmNcMouseMove, overMaximize, IntPtr.Zero);
        Assert.True(TitleBarChrome.GetIsHovered(maximize));
        _ = SendMessage(window, TitleBarChrome.WmNcLButtonDown, overMaximize, IntPtr.Zero);
        Assert.True(TitleBarChrome.GetIsPressed(maximize));
        _ = SendMessage(window, TitleBarChrome.WmNcLButtonUp, overMaximize, IntPtr.Zero);
        host.Settle();
        try
        {
            Assert.False(TitleBarChrome.GetIsPressed(maximize));
            Assert.Equal(WindowState.Maximized, host.Shell.WindowState);
            _ = SendMessage(window, TitleBarChrome.WmNcMouseLeave, IntPtr.Zero, IntPtr.Zero);
            Assert.False(TitleBarChrome.GetIsHovered(maximize));
        }
        finally
        {
            host.Shell.WindowState = WindowState.Normal;
            host.Settle();
        }
    });

    /// <summary>The quick commands do what their names say.</summary>
    [Fact]
    public void TheQuickCommandsOpenThePaletteAndSettings() => RunSta(() =>
    {
        using var host = new ShownShell();

        Named(host, "TitleBarPalette").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        host.Settle();
        Assert.True(host.Lifecycle.Palette.IsOpen);
        host.Lifecycle.Palette.Dismiss();
        host.Settle();

        Button settings = Named(host, "TitleBarSettings");
        settings.Command.Execute(settings.CommandParameter);
        host.Settle();
        Assert.Equal(ModalSurface.Settings, host.Shell.OpenModalSurface);
    });

    private const int HtCaption = 2;

    private static int HitTest(IntPtr window, Point screen)
    {
        int x = (int)Math.Round(screen.X);
        int y = (int)Math.Round(screen.Y);
        return SendMessage(window, TitleBarChrome.WmNcHitTest, IntPtr.Zero, new IntPtr(((y & 0xFFFF) << 16) | (x & 0xFFFF))).ToInt32();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    /// <summary>A point left of or above the first monitor is negative in each half.</summary>
    [Fact]
    public void AHitTestPointCanBeNegative()
    {
        var lParam = new IntPtr((0x0014 << 16) | 0xFFFB);

        Assert.Equal(new Point(-5, 20), TitleBarChrome.PointOf(lParam));
    }

    /// <summary>The quick commands go off under a modal surface, as the menu does, and the
    /// captions never do. Every surface sets the one flag (<see cref="MainWindow.IsModalSurfaceOpen"/>),
    /// so two kinds stand for them: an overlay and a sheet.</summary>
    [Fact]
    public void ThePaletteAndSettingsButtonsDisableUnderEveryModalSurface() => RunSta(() =>
    {
        using var host = new ShownShell();
        Button[] quick = { Named(host, "TitleBarPalette"), Named(host, "TitleBarSettings") };
        Button[] captions = { Named(host, "CaptionMinimize"), Named(host, "CaptionMaximize"), Named(host, "CaptionClose") };
        Assert.All(quick, button => Assert.True(button.IsEnabled));

        host.Lifecycle.Palette.Open();
        host.Settle();
        Assert.NotNull(host.Shell.OpenModalSurface);
        Assert.All(quick, button => Assert.False(button.IsEnabled));
        Assert.All(captions, button => Assert.True(button.IsEnabled));

        host.Lifecycle.Palette.Dismiss();
        host.Settle();
        Assert.All(quick, button => Assert.True(button.IsEnabled));

        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();
        Assert.Equal(ModalSurface.Settings, host.Shell.OpenModalSurface);
        Assert.All(quick, button => Assert.False(button.IsEnabled));
        Assert.All(captions, button => Assert.True(button.IsEnabled));
    });

    /// <summary>A maximized chrome window overhangs the screen by its resize border, so the
    /// root takes that border in, and the middle caption says Restore.</summary>
    [Fact]
    public void TheMaximizedWindowPadsItsRoot() => RunSta(() =>
    {
        using var host = new ShownShell();
        var root = (FrameworkElement)host.Shell.Content;
        Button middle = Named(host, "CaptionMaximize");
        Assert.Equal(new Thickness(0), root.Margin);
        Assert.Equal("Maximize", AutomationProperties.GetName(middle));

        host.Shell.WindowState = WindowState.Maximized;
        host.Settle();
        try
        {
            Assert.Equal(TitleBarChrome.FrameThickness(host.Shell), root.Margin);
            Assert.Equal("Restore", AutomationProperties.GetName(middle));
            Assert.Equal("\uE923", middle.Content);
            Assert.Same(SystemCommands.RestoreWindowCommand, middle.Command);
        }
        finally
        {
            host.Shell.WindowState = WindowState.Normal;
            host.Settle();
        }

        Assert.Equal(new Thickness(0), root.Margin);
        Assert.Equal("Maximize", AutomationProperties.GetName(middle));
        Assert.Same(SystemCommands.MaximizeWindowCommand, middle.Command);
    });

    private static Button Named(ShownShell host, string automationId) =>
        Assert.Single(ShownShell.Descendants(host.Shell.TitleBar).OfType<Button>(), button => AutomationProperties.GetAutomationId(button) == automationId);

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(120), "STA test body timed out.");
}
