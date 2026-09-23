// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Interop;
using SlateWindows.Tests.Censuses;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class AccessibilityNotificationDispatcherTests
{
    [Fact]
    public void EveryPriorityAndBothOverloadsReachTheExactNativeTuple()
    {
        Assert.Equal([A11yPriority.Medium, A11yPriority.High], Enum.GetValues<A11yPriority>());
        foreach (var (priority, processing) in new[]
        {
            (A11yPriority.Medium, AutomationNotificationProcessing.All),
            (A11yPriority.High, AutomationNotificationProcessing.ImportantMostRecent),
        })
        {
            var raised = new List<Notification>();
            var dispatcher = Recording(raised);
            const string text = "Exact text. café — unchanged.";
            dispatcher.Post(new RenderedAnnouncement(text, priority));
            dispatcher.Post(new A11yEvent.HostComposed(text, priority));
            Assert.Equal(Enumerable.Repeat(new Notification(
                AutomationNotificationKind.Other, processing, text,
                "slate-accessibility-announcement"), 2), raised);
        }
    }

    [Fact]
    public void EveryCorpusEventReachesTheNativeBoundaryWithItsGoldenTextAndPriority()
    {
        foreach (var (sample, priority, text) in A11yCorpusCensus.DispatcherCases())
        {
            var raised = new List<Notification>();
            Recording(raised).Post(sample);
            AutomationNotificationProcessing processing = priority switch
            {
                "medium" => AutomationNotificationProcessing.All,
                "high" => AutomationNotificationProcessing.ImportantMostRecent,
                _ => throw new InvalidOperationException($"Unknown golden priority: {priority}"),
            };
            Assert.Equal(new Notification(AutomationNotificationKind.Other,
                processing, text, "slate-accessibility-announcement"), Assert.Single(raised));
        }
    }

    /// <summary>D-1's exhaustiveness is the fact above: a third priority fails
    /// the build's tests. At runtime an unknown value is an announcement, not
    /// a crash on the UI thread — it degrades to the polite queue.</summary>
    [Fact]
    public void AnUnknownPriorityDegradesToThePoliteQueueInsteadOfThrowing()
    {
        var raised = new List<Notification>();
        Recording(raised).Post(new RenderedAnnouncement("Unknown priority.", (A11yPriority)42));
        Assert.Equal(new Notification(AutomationNotificationKind.Other, AutomationNotificationProcessing.All,
            "Unknown priority.", "slate-accessibility-announcement"), Assert.Single(raised));
    }

    /// <summary>R-1 (#1244): once the launch phase is Done, the production
    /// raise is guarded by UIA's own "is any client listening", injected here,
    /// and asked at EVERY post — a screen reader started after Slate hears the
    /// next line, which a probe read once at construction would never let it.</summary>
    [Fact]
    public void ProductionRaiseSkipsWhenNoClientListens()
    {
        bool listening = false;
        var raised = new List<Notification>();
        var dispatcher = new AccessibilityNotificationDispatcher(
            (kind, processing, text, activityId) => raised.Add(new Notification(kind, processing, text, activityId)),
            () => listening,
            AlwaysAdvised() with { Elapsed = () => AccessibilityNotificationDispatcher.LaunchWindow });

        dispatcher.Post(new RenderedAnnouncement("Nobody listens.", A11yPriority.High));
        dispatcher.Post(new A11yEvent.HostComposed("Nobody listens.", A11yPriority.Medium));
        Assert.Empty(raised);

        listening = true;
        dispatcher.Post(new RenderedAnnouncement("A client listens.", A11yPriority.High));
        Assert.Equal(new Notification(AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent,
            "A client listens.", "slate-accessibility-announcement"), Assert.Single(raised));
    }

    /// <summary>R-1's SLATE_UIA_DIAGNOSTICS line is written once per CHANGE
    /// of the listener state — the two facts a run needs to tell "raised into
    /// a deaf process" from "not raised" — never once per announcement.</summary>
    [Fact]
    public void TheListenerStateIsReportedOncePerChange()
    {
        int last = -1;
        Assert.Equal("clientsListening=True, notificationListenerExists=False",
            AccessibilityNotificationDispatcher.ListenerStateChange(ref last, true, false));
        Assert.Null(AccessibilityNotificationDispatcher.ListenerStateChange(ref last, true, false));
        Assert.Equal("clientsListening=True, notificationListenerExists=True",
            AccessibilityNotificationDispatcher.ListenerStateChange(ref last, true, true));
        Assert.Equal("clientsListening=False, notificationListenerExists=True",
            AccessibilityNotificationDispatcher.ListenerStateChange(ref last, false, true));
        Assert.Equal("clientsListening=False, notificationListenerExists=False",
            AccessibilityNotificationDispatcher.ListenerStateChange(ref last, false, false));
        Assert.Null(AccessibilityNotificationDispatcher.ListenerStateChange(ref last, false, false));
    }

    /// <summary>
    /// R-1, codex round 5, recorded rather than assumed: the status element's
    /// peer has a UIA provider exactly when something has asked its window
    /// for one (WM_GETOBJECT, which is how every client connects). At the
    /// first frame of a launch nothing has, so ProviderFromPeer answers null
    /// (the peer is unconnected, the helper peer has no window, and the
    /// dispatcher has no automation root) and a line posted then is not
    /// raised, exactly as WPF's gated call would not raise it.
    /// </summary>
    /// <remarks>
    /// Another UIA client on the desktop may ask a new window at once (one
    /// did during a full-suite run here), so the first-frame check follows
    /// the window's own record of whether it was asked; the explicit ask
    /// after it holds on every desktop.
    /// </remarks>
    [Fact]
    public void AtTheFirstFrameTheStatusPeerHasNoProviderUntilTheWindowIsAsked() => RunSta(() =>
    {
        var status = new TextBlock { Text = "Vault status" };
        var window = new Window
        {
            Content = status,
            Width = 400,
            Height = 300,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        int asked = 0;
        try
        {
            IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
            HwndSource.FromHwnd(handle).AddHook(
                (IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                {
                    if (message == NativeWindow.WmGetObject)
                    {
                        asked++;
                    }

                    return IntPtr.Zero;
                });
            window.Show();

            bool connected = AccessibilityNotificationDispatcher.NotificationSource.Of(status) is not null;
            Assert.True(
                connected == asked > 0,
                $"first frame: {asked} WM_GETOBJECT seen, status peer provider {(connected ? "present" : "null")}");

            int before = asked;
            _ = NativeWindow.SendMessage(handle, NativeWindow.WmGetObject, IntPtr.Zero, NativeWindow.UiaRootObjectId);
            Assert.True(asked > before, "the window's hook never saw the WM_GETOBJECT this fact sent, so it cannot tell asked from unasked.");
            Assert.NotNull(AccessibilityNotificationDispatcher.NotificationSource.Of(status));
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>R-1's source diagnostic, the same once-per-change rule:
    /// whether the status peer had a provider when asked (the launch
    /// condition the hosted fact records).</summary>
    [Fact]
    public void TheRaiseSourceIsReportedOncePerChange()
    {
        int last = -1;
        Assert.Equal("statusPeerProvider=null",
            AccessibilityNotificationDispatcher.NotificationSource.SourceChange(ref last, connected: false));
        Assert.Null(AccessibilityNotificationDispatcher.NotificationSource.SourceChange(ref last, connected: false));
        Assert.Equal("statusPeerProvider=connected",
            AccessibilityNotificationDispatcher.NotificationSource.SourceChange(ref last, connected: true));
        Assert.Null(AccessibilityNotificationDispatcher.NotificationSource.SourceChange(ref last, connected: true));
        Assert.Equal("statusPeerProvider=null",
            AccessibilityNotificationDispatcher.NotificationSource.SourceChange(ref last, connected: false));
    }

    private static class NativeWindow
    {
        internal const int WmGetObject = 0x003D;

        // UiaRootObjectId: the object id a UIA client sends with WM_GETOBJECT.
        internal static readonly IntPtr UiaRootObjectId = new(-25);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    /// <summary>
    /// OD-7, the launch phase: while Unadvised a posted line is QUEUED, never
    /// raised — zero raises — and the first check that finds a listening
    /// client, an advise and a connected provider (here the poll) raises each
    /// queued line exactly once, in order, as the same tuple. Then the phase
    /// is Done: every later line is raised at once, exactly once, whatever
    /// the listener map says afterwards.
    /// </summary>
    [Fact]
    public void LinesPostedBeforeTheAdviseAreQueuedThenRaisedOnceInOrder()
    {
        var launch = new LaunchHarness();
        launch.Post("Vault opened.", "Scanning vault. 2 files to index.");
        launch.Dispatcher.Post(new RenderedAnnouncement("Could not open vault.", A11yPriority.High));
        Assert.Empty(launch.Raised);
        Assert.Equal(1, launch.PollsStarted);

        launch.Tick!();
        Assert.Empty(launch.Raised);

        launch.Advised = true;
        launch.Tick!();
        Assert.Equal(
            [
                new Notification(AutomationNotificationKind.Other, AutomationNotificationProcessing.All, "Vault opened.", "slate-accessibility-announcement"),
                new Notification(AutomationNotificationKind.Other, AutomationNotificationProcessing.All, "Scanning vault. 2 files to index.", "slate-accessibility-announcement"),
                new Notification(AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent, "Could not open vault.", "slate-accessibility-announcement"),
            ],
            launch.Raised);
        Assert.Null(launch.Tick);

        launch.Post("Right pane hidden.");
        launch.Advised = false;
        launch.Connected = false;
        launch.Post("Right pane shown.");
        launch.Advised = true;
        launch.Connected = true;
        launch.Post("Pane resized, 60 percent.");
        Assert.Equal(
            ["Vault opened.", "Scanning vault. 2 files to index.", "Could not open vault.",
                "Right pane hidden.", "Right pane shown.", "Pane resized, 60 percent."],
            launch.Raised.Select(line => line.Text));
        Assert.Equal(1, launch.PollsStarted);
    }

    /// <summary>OD-7: a process already advised, with a listening client and a
    /// connected provider, at its first post ends the phase there: the line is
    /// raised at once, exactly once, and nothing is queued or polled for.</summary>
    [Fact]
    public void AProcessAdvisedAtItsFirstLineRaisesEveryLineAtOnce()
    {
        var launch = new LaunchHarness { Advised = true };
        launch.Post("Vault opened.", "Scanning vault. 2 files to index.");
        Assert.Equal(["Vault opened.", "Scanning vault. 2 files to index."], launch.Raised.Select(line => line.Text));
        Assert.Equal(0, launch.PollsStarted);
        Assert.Empty(launch.Logged);
    }

    /// <summary>OD-7: the phase ends only when all three are there — a
    /// listening client, the advise and a connected status provider. Any one
    /// missing keeps the lines queued; the post that completes the three
    /// drains them before its own line.</summary>
    [Fact]
    public void TheQueueWaitsForAListeningClientAnAdviseAndAConnectedProvider()
    {
        var launch = new LaunchHarness { Advised = true, Connected = false };
        launch.Post("Vault opened.");
        launch.Tick!();
        launch.Connected = true;
        launch.Listening = false;
        launch.Tick!();
        launch.Listening = true;
        launch.Advised = false;
        launch.Tick!();
        Assert.Empty(launch.Raised);

        launch.Advised = true;
        launch.Post("Scan complete. 2 files indexed.");
        Assert.Equal(["Vault opened.", "Scan complete. 2 files indexed."], launch.Raised.Select(line => line.Text));
        Assert.Null(launch.Tick);
    }

    /// <summary>OD-7: the queue keeps the last sixteen lines, the oldest
    /// dropped first, and the drain says how many it could not keep.</summary>
    [Fact]
    public void TheLaunchQueueKeepsOnlyTheLastSixteenLines()
    {
        var launch = new LaunchHarness();
        string[] lines = [.. Enumerable.Range(0, 20).Select(index => $"Line {index}.")];
        launch.Post(lines);
        Assert.Empty(launch.Raised);
        launch.Advised = true;
        launch.Tick!();
        Assert.Equal(lines.Skip(4), launch.Raised.Select(line => line.Text));
        Assert.Equal(["drained=16, droppedOldest=4, at=0ms"], launch.Logged);
    }

    /// <summary>OD-7: no advise within the launch window of the first frame
    /// ends the phase with the queue dropped unspoken and the poll stopped;
    /// later lines are raised at once, and a later advise brings nothing back.</summary>
    [Fact]
    public void AnUnadvisedLaunchExpiresAndDropsItsQueue()
    {
        var launch = new LaunchHarness();
        launch.Post("Vault opened.", "Scanning vault. 2 files to index.");
        launch.Now = AccessibilityNotificationDispatcher.LaunchWindow - TimeSpan.FromMilliseconds(1);
        launch.Tick!();
        Assert.NotNull(launch.Tick);

        launch.Now = AccessibilityNotificationDispatcher.LaunchWindow;
        launch.Tick!();
        Assert.Null(launch.Tick);
        Assert.Empty(launch.Raised);
        Assert.Equal(["expired=2, never advised"], launch.Logged);

        launch.Post("Scan complete. 2 files indexed.");
        launch.Advised = true;
        launch.Post("Right pane hidden.");
        Assert.Equal(["Scan complete. 2 files indexed.", "Right pane hidden."], launch.Raised.Select(line => line.Text));
        Assert.Equal(1, launch.PollsStarted);
    }

    /// <summary>OD-7: the drain and the expiry each write one diagnostics line
    /// (under SLATE_UIA_DIAGNOSTICS=1 in production), with the drain's time;
    /// a phase that ends with nothing queued writes none.</summary>
    [Fact]
    public void TheLaunchTransitionIsLogged()
    {
        var launch = new LaunchHarness();
        launch.Post("Vault opened.", "Scanning vault. 2 files to index.");
        Assert.Empty(launch.Logged);
        launch.Now = TimeSpan.FromMilliseconds(1250);
        launch.Advised = true;
        launch.Post("Scan complete. 2 files indexed.");
        Assert.Equal(["drained=2, droppedOldest=0, at=1250ms"], launch.Logged);
        launch.Post("Right pane hidden.");
        Assert.Equal(["drained=2, droppedOldest=0, at=1250ms"], launch.Logged);

        var quiet = new LaunchHarness { Advised = true };
        quiet.Post("Vault opened.");
        Assert.Empty(quiet.Logged);
    }

    /// <summary>
    /// OD-7 at the replay boundary, hosted: the queue drains only through a
    /// connected status provider — production's own probe over a real shown
    /// window. Whatever the desktop does, every raise happens with the status
    /// peer's provider connected, and each line exactly once, in order; when
    /// the window had not been asked for its UIA root, nothing is raised
    /// until it is.
    /// </summary>
    [Fact]
    public void TheLaunchQueueDrainsOnlyThroughAConnectedStatusProvider() => RunSta(() =>
    {
        var status = new TextBlock { Text = "Vault status" };
        var window = new Window
        {
            Content = status,
            Width = 400,
            Height = 300,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        int asked = 0;
        var raised = new List<(string Text, bool Connected)>();
        Action? tick = null;
        try
        {
            IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
            HwndSource.FromHwnd(handle).AddHook(
                (IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                {
                    if (message == NativeWindow.WmGetObject)
                    {
                        asked++;
                    }

                    return IntPtr.Zero;
                });
            window.Show();
            var dispatcher = new AccessibilityNotificationDispatcher(
                (kind, processing, text, activityId) =>
                    raised.Add((text, AccessibilityNotificationDispatcher.NotificationSource.Of(status) is not null)),
                () => true,
                new AccessibilityNotificationDispatcher.LaunchSeams(
                    () => true,
                    () => AccessibilityNotificationDispatcher.NotificationSource.Of(status) is not null,
                    poll =>
                    {
                        tick = poll;
                        return new StopPoll(() => tick = null);
                    },
                    () => TimeSpan.Zero,
                    _ => { }));

            bool askedBefore = asked > 0;
            dispatcher.Post(new RenderedAnnouncement("Vault opened.", A11yPriority.Medium));
            dispatcher.Post(new RenderedAnnouncement("Scanning vault. 2 files to index.", A11yPriority.Medium));
            if (!askedBefore && asked == 0)
            {
                Assert.Empty(raised);
                tick!();
                Assert.Empty(raised);
                _ = NativeWindow.SendMessage(handle, NativeWindow.WmGetObject, IntPtr.Zero, NativeWindow.UiaRootObjectId);
                tick!();
            }

            Assert.Equal(
                [("Vault opened.", true), ("Scanning vault. 2 files to index.", true)],
                raised);
            Assert.Null(tick);
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>
    /// OD-7: ONE launch phase per window. A grid in the window posts through
    /// the window's dispatcher (inherited), so its line is queued by the same
    /// state machine as the window's own and drained with them — once, in
    /// order, one poll — never raised by a dispatcher of its own; a grid in a
    /// window the main one owns (the reading view's table) finds the same one.
    /// </summary>
    [Fact]
    public void AGridAnnouncementDuringTheLaunchPhaseJoinsTheWindowsOneQueue() => RunSta(() =>
    {
        var launch = new LaunchHarness();
        var grid = new Grids.AccessibleDataGrid();
        var window = new Window
        {
            Content = new StackPanel { Children = { grid } },
            Width = 400,
            Height = 300,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        AccessibilityNotificationDispatcher.SetAnnouncer(window, launch.Dispatcher);
        try
        {
            window.Show();
            launch.Post("Vault opened.");
            grid.Announce(new A11yEvent.HostComposed("Sorted by Name, ascending.", A11yPriority.Medium));
            Assert.Empty(launch.Raised);
            Assert.Equal(1, launch.PollsStarted);

            launch.Advised = true;
            launch.Tick!();
            Assert.Equal(["Vault opened.", "Sorted by Name, ascending."], launch.Raised.Select(line => line.Text));

            var ownedGrid = new Grids.AccessibleDataGrid();
            var owned = new Window
            {
                Owner = window,
                Content = ownedGrid,
                Width = 300,
                Height = 200,
                ShowInTaskbar = false,
                ShowActivated = false,
            };
            owned.Show();
            ownedGrid.Announce(new A11yEvent.HostComposed("Row 2 of 3.", A11yPriority.Medium));
            Assert.Equal(
                ["Vault opened.", "Sorted by Name, ascending.", "Row 2 of 3."],
                launch.Raised.Select(line => line.Text));
            owned.Close();
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>The launch facts' dispatcher: a recording raise, and every
    /// launch input in the fact's hands — whether a client listens, whether
    /// WPF's map has been advised, whether the status provider is connected,
    /// the poll's tick, the clock and the log. A client listens and the
    /// provider is connected unless a fact says otherwise.</summary>
    private sealed class LaunchHarness
    {
        internal LaunchHarness()
        {
            Dispatcher = new AccessibilityNotificationDispatcher(
                (kind, processing, text, activityId) => Raised.Add(new Notification(kind, processing, text, activityId)),
                () => Listening,
                new AccessibilityNotificationDispatcher.LaunchSeams(
                    () => Advised,
                    () => Connected,
                    tick =>
                    {
                        PollsStarted++;
                        Tick = tick;
                        return new StopPoll(() => Tick = null);
                    },
                    () => Now,
                    Logged.Add));
        }

        internal AccessibilityNotificationDispatcher Dispatcher { get; }

        internal List<Notification> Raised { get; } = [];

        internal List<string> Logged { get; } = [];

        internal bool Listening { get; set; } = true;

        internal bool Advised { get; set; }

        internal bool Connected { get; set; } = true;

        internal TimeSpan Now { get; set; }

        internal Action? Tick { get; private set; }

        internal int PollsStarted { get; private set; }

        internal void Post(params string[] lines)
        {
            foreach (string line in lines)
            {
                Dispatcher.Post(new RenderedAnnouncement(line, A11yPriority.Medium));
            }
        }
    }

    private sealed class StopPoll(Action stop) : IDisposable
    {
        public void Dispose() => stop();
    }

    /// <summary>Seams for the facts about the raise itself: the process is
    /// advised and the provider connected, so the first post ends the launch
    /// phase with nothing queued, and no poll ever starts.</summary>
    private static AccessibilityNotificationDispatcher.LaunchSeams AlwaysAdvised() => new(
        () => true,
        () => true,
        _ => throw new InvalidOperationException("An advised process starts no launch poll."),
        () => TimeSpan.Zero,
        line => throw new InvalidOperationException($"An advised process logs no launch line: {line}"));

    private static AccessibilityNotificationDispatcher Recording(List<Notification> raised) =>
        new((kind, processing, text, activityId) =>
            raised.Add(new Notification(kind, processing, text, activityId)),
            () => true,
            AlwaysAdvised());

    private sealed record Notification(AutomationNotificationKind Kind,
        AutomationNotificationProcessing Processing, string Text, string ActivityId);
}
