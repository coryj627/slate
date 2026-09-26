// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
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

    /// <summary>R-1 (#1244): UIA's own "is any client listening" is the first
    /// of readiness's three conjuncts, injected here and asked at EVERY check
    /// — a screen reader started after Slate hears what waited for it, which
    /// a probe read once at construction would never let it. After the launch
    /// (here Expired) a line posted while no client listens raises nothing and
    /// waits; the post that finds a client drains it before its own line, each
    /// exactly once, in order, as core's tuple.</summary>
    [Fact]
    public void ProductionRaiseWaitsWhileNoClientListens()
    {
        var launch = new LaunchHarness
        {
            Listening = false,
            Connected = true,
            Now = AccessibilityNotificationDispatcher.LaunchWindow,
        };
        launch.Dispatcher.Post(new RenderedAnnouncement("Nobody listens.", A11yPriority.High));
        launch.Dispatcher.Post(new A11yEvent.HostComposed("Nobody listens yet.", A11yPriority.Medium));
        Assert.Empty(launch.Raised);

        launch.Listening = true;
        launch.Dispatcher.Post(new RenderedAnnouncement("A client listens.", A11yPriority.High));
        Assert.Equal(
            [
                new Notification(AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent, "Nobody listens.", "slate-accessibility-announcement"),
                new Notification(AutomationNotificationKind.Other, AutomationNotificationProcessing.All, "Nobody listens yet.", "slate-accessibility-announcement"),
                new Notification(AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent, "A client listens.", "slate-accessibility-announcement"),
            ],
            launch.Raised);
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
    /// peer has a UIA provider exactly when its window's automation root is
    /// connected. WPF connects it on any WM_GETOBJECT the window receives
    /// (HwndTarget.CriticalHandleWMGetobject), and by itself, with no message,
    /// when its process-wide event map already has a listener as the window
    /// gets its root visual (HwndSource.RootVisual, EventMap's part (a)). At
    /// a launch's first frame with neither, ProviderFromPeer answers null and
    /// a line posted then is not raised, exactly as WPF's gated call would
    /// not raise it; a request for the UIA root connects it.
    /// </summary>
    /// <remarks>
    /// A full-suite run here met both routes: a desktop client can ask a new
    /// window while it is still being created, before an HwndSource hook
    /// exists — so the requests are recorded by a thread hook installed first
    /// — and a client that advised any earlier window of the test process
    /// leaves the event map listening. The first-frame check applies when the
    /// map read the same before the window and after its first frame.
    /// </remarks>
    [Fact]
    public void AtTheFirstFrameTheStatusPeerHasNoProviderUntilItsWindowIsConnected() => RunSta(() =>
    {
        using var requests = new RootRequests();
        bool listenersBefore = WpfEventMapHasListeners();
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
        try
        {
            IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
            window.Show();

            bool connected = AccessibilityNotificationDispatcher.NotificationSource.Of(status) is not null;
            int asked = requests.To(handle);
            if (WpfEventMapHasListeners() == listenersBefore)
            {
                Assert.True(
                    connected == (asked > 0 || listenersBefore),
                    $"first frame: {asked} WM_GETOBJECT, WPF's event map {(listenersBefore ? "listening" : "empty")}, "
                    + $"status peer provider {(connected ? "present" : "null")}");
            }

            _ = NativeWindow.SendMessage(handle, NativeWindow.WmGetObject, IntPtr.Zero, NativeWindow.UiaRootObjectId);
            Assert.True(
                requests.To(handle) > asked,
                "the thread hook never saw the WM_GETOBJECT this fact sent, so it cannot tell asked from unasked.");
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

    /// <summary>Whether WPF's process-wide event map has any listener (its
    /// EventMap.HasListeners, read through the public per-event probe). While
    /// it has, WPF connects each new window's automation root itself as the
    /// root visual is set, with no WM_GETOBJECT.</summary>
    private static bool WpfEventMapHasListeners() =>
        Enum.GetValues<AutomationEvents>().Any(AutomationPeer.ListenerExists);

    /// <summary>
    /// Every WM_GETOBJECT the windows of this thread receive, from before the
    /// window exists: a thread WH_CALLWNDPROC hook sees each sent message as
    /// it is delivered, including one a desktop client sends while the window
    /// is still being created, which an HwndSource hook — added only once the
    /// source exists — would miss. WPF connects the root on any object id, so
    /// every request counts.
    /// </summary>
    private sealed class RootRequests : IDisposable
    {
        private readonly NativeWindow.HookProc _onSent;
        private readonly IntPtr _hook;
        private readonly List<IntPtr> _asked = [];

        internal RootRequests()
        {
            _onSent = OnSent;
            _hook = NativeWindow.SetWindowsHookEx(NativeWindow.WhCallWndProc, _onSent, IntPtr.Zero, NativeWindow.GetCurrentThreadId());
            Assert.NotEqual(IntPtr.Zero, _hook);
        }

        /// <summary>How many requests <paramref name="window"/> has received.</summary>
        internal int To(IntPtr window) => _asked.Count(asked => asked == window);

        public void Dispose()
        {
            _ = NativeWindow.UnhookWindowsHookEx(_hook);
            GC.KeepAlive(_onSent);
        }

        private IntPtr OnSent(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                NativeWindow.SentMessage sent = Marshal.PtrToStructure<NativeWindow.SentMessage>(lParam);
                if (sent.Message == NativeWindow.WmGetObject)
                {
                    _asked.Add(sent.Window);
                }
            }

            return NativeWindow.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }
    }

    private static class NativeWindow
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
    /// OD-7, the launch phase: while Unadvised — here the status provider not
    /// yet connected, as at a launch's first frame — a posted line is QUEUED,
    /// never raised — zero raises — and the first check that finds a listening
    /// client and a connected provider (here the poll) raises each queued line
    /// exactly once, in order, as the same tuple, with WPF's advise map still
    /// empty. Then the phase is Done, and a line posted while ready is raised
    /// at once, exactly once, with no poll.
    /// </summary>
    [Fact]
    public void LinesPostedBeforeReadinessAreQueuedThenRaisedOnceInOrder()
    {
        var launch = new LaunchHarness();
        launch.Post("Vault opened.", "Scanning vault. 2 files to index.");
        launch.Dispatcher.Post(new RenderedAnnouncement("Could not open vault.", A11yPriority.High));
        Assert.Empty(launch.Raised);
        Assert.Equal(1, launch.PollsStarted);

        launch.Tick!();
        Assert.Empty(launch.Raised);

        launch.Connected = true;
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
        Assert.Equal(
            ["Vault opened.", "Scanning vault. 2 files to index.", "Could not open vault.", "Right pane hidden."],
            launch.Raised.Select(line => line.Text));
        Assert.Equal(1, launch.PollsStarted);
    }

    /// <summary>
    /// OD-7, codex rounds 17–18: expiry is its own state, and the poll is tied
    /// to the queue, not the launch. At the window's close the launch line
    /// reaches its own deadline and is dropped, and the launch is Expired; a
    /// later line posted while a client listens but the status provider is
    /// still null raises nothing and is not lost — the poll restarts for it —
    /// so when the provider connects on
    /// another thread, with no further post, production's own timer tick
    /// drains exactly those lines once, in order, and stops; the next line
    /// raises at once. The fact pumps the dispatcher and never calls the
    /// check.
    /// </summary>
    [Fact]
    public void AfterExpiryALineWaitsForTheProviderInsteadOfBeingLost() => RunSta(() =>
    {
        var launch = new TimerHarness { Connected = false };
        launch.Post("Vault opened.");
        launch.Now = AccessibilityNotificationDispatcher.LaunchWindow;
        Assert.True(TimerHarness.PumpUntil(() => launch.Drains.Count == 2), "no tick saw the launch window close");
        Assert.Equal(["dropped=1, unready too long", "expired, never ready"], launch.Drains);
        Assert.Equal((1, 1), (launch.PollsStarted, launch.PollsStopped));

        launch.Post("Pane resized, 60 percent.", "Right pane hidden.");
        Assert.Empty(launch.Raised);
        Task connect = TimerHarness.Later(() => launch.Connected = true);
        Assert.True(TimerHarness.PumpUntil(() => launch.Raised.Count >= 2), "no tick drained the lines once the provider connected");
        connect.GetAwaiter().GetResult();
        TimerHarness.PumpFor(TimeSpan.FromMilliseconds(750));
        Assert.Equal(["Pane resized, 60 percent.", "Right pane hidden."], launch.Raised);
        Assert.Equal(
            ["dropped=1, unready too long", "expired, never ready", "lines=2, droppedOldest=0, at=30000ms, advise=present, drained=advise"],
            launch.Drains);
        Assert.Equal((2, 2), (launch.PollsStarted, launch.PollsStopped));

        launch.Post("Right pane shown.");
        Assert.Equal(["Pane resized, 60 percent.", "Right pane hidden.", "Right pane shown."], launch.Raised);
    });

    /// <summary>
    /// OD-7, the hybrid (a): a status provider that connects before UIA has
    /// advised the process holds the queue — a raise into an unadvised
    /// process is delivered only sometimes — so nothing is raised before the
    /// advise, and when it lands, 800 ms after the connection here,
    /// production's own timer raises every queued line exactly once, in
    /// order. The fact pumps the dispatcher and never calls the check.
    /// </summary>
    [Fact]
    public void AProviderThatConnectsFirstHoldsTheQueueUntilTheAdvise() => RunSta(() =>
    {
        var launch = new TimerHarness { Connected = false, Advised = false };
        launch.Post("Vault opened.", "Scanning vault. 2 files to index.");
        launch.Now = TimeSpan.FromSeconds(1);
        launch.Connected = true;
        launch.PumpTicks(2);
        launch.Now = TimeSpan.FromMilliseconds(1800);
        launch.PumpTicks(2);
        Assert.Empty(launch.Raised);

        Task advise = TimerHarness.Later(() => launch.Advised = true);
        Assert.True(TimerHarness.PumpUntil(() => launch.Raised.Count >= 2), "no tick drained the queue once the advise landed");
        advise.GetAwaiter().GetResult();
        TimerHarness.PumpFor(TimeSpan.FromMilliseconds(750));
        Assert.Equal(["Vault opened.", "Scanning vault. 2 files to index."], launch.Raised);
        Assert.Equal(["lines=2, droppedOldest=0, at=1800ms, advise=present, drained=advise"], launch.Drains);
        Assert.Equal((1, 1), (launch.PollsStarted, launch.PollsStopped));
    });

    /// <summary>
    /// OD-7, the hybrid (b): a client that never advises the process still
    /// hears the lines. With the provider connected and no advise, nothing is
    /// raised until the hold has run for three seconds from the connection;
    /// then production's own timer drains the queue exactly once, in order.
    /// A later line, the provider long connected, is raised at once.
    /// </summary>
    [Fact]
    public void AProviderThatIsNeverAdvisedDrainsThreeSecondsAfterItConnects() => RunSta(() =>
    {
        TimeSpan connectedAt = TimeSpan.FromSeconds(1);
        var launch = new TimerHarness { Connected = false, Advised = false };
        launch.Post("Vault opened.", "Scanning vault. 2 files to index.");
        launch.Now = connectedAt;
        launch.Connected = true;
        launch.PumpTicks(2);
        launch.Now = connectedAt + AccessibilityNotificationDispatcher.AdviseHold - TimeSpan.FromMilliseconds(1);
        launch.PumpTicks(2);
        Assert.Empty(launch.Raised);

        launch.Now = connectedAt + AccessibilityNotificationDispatcher.AdviseHold;
        Assert.True(TimerHarness.PumpUntil(() => launch.Raised.Count >= 2), "the hold never ran out");
        TimerHarness.PumpFor(TimeSpan.FromMilliseconds(750));
        Assert.Equal(["Vault opened.", "Scanning vault. 2 files to index."], launch.Raised);
        Assert.Equal(["lines=2, droppedOldest=0, at=4000ms, advise=absent, drained=timeout"], launch.Drains);
        Assert.Equal((1, 1), (launch.PollsStarted, launch.PollsStopped));

        launch.Post("Right pane hidden.");
        Assert.Equal(["Vault opened.", "Scanning vault. 2 files to index.", "Right pane hidden."], launch.Raised);
    });

    /// <summary>
    /// OD-7, the hybrid (c): lines posted while the provider waits for the
    /// advise are queued behind the ones already waiting — none is raised —
    /// and the advise drains them all, once, in order.
    /// </summary>
    [Fact]
    public void LinesPostedDuringTheHoldAreQueuedAndNoneIsRaised() => RunSta(() =>
    {
        var launch = new TimerHarness { Connected = false, Advised = false };
        launch.Post("Vault opened.");
        launch.Now = TimeSpan.FromSeconds(1);
        launch.Connected = true;
        launch.PumpTicks(1);
        launch.Now = TimeSpan.FromSeconds(2);
        launch.Post("Scanning vault. 2 files to index.", "Scan complete. 2 files indexed.");
        launch.Now = TimeSpan.FromSeconds(3);
        launch.Post("Editor pane 1 of 1, Empty pane.");
        Assert.Empty(launch.Raised);

        Task advise = TimerHarness.Later(() => launch.Advised = true);
        Assert.True(TimerHarness.PumpUntil(() => launch.Raised.Count >= 4), "no tick drained the queue once the advise landed");
        advise.GetAwaiter().GetResult();
        TimerHarness.PumpFor(TimeSpan.FromMilliseconds(750));
        Assert.Equal(
            ["Vault opened.", "Scanning vault. 2 files to index.", "Scan complete. 2 files indexed.", "Editor pane 1 of 1, Empty pane."],
            launch.Raised);
        Assert.Equal(["lines=4, droppedOldest=0, at=3000ms, advise=present, drained=advise"], launch.Drains);
    });

    /// <summary>
    /// OD-7, the hybrid (d): a hold runs from the provider's latest connection
    /// — one lost before its three seconds and found again starts afresh — and
    /// a hold that runs out while a client listens delivers, the diagnostic
    /// recording <c>advise=absent, drained=timeout</c>.
    /// </summary>
    [Fact]
    public void AHoldThatRunsOutWhileAClientListensDeliversAndSaysSo() => RunSta(() =>
    {
        var launch = new TimerHarness { Connected = false, Advised = false };
        launch.Post("Vault opened.");
        launch.Now = TimeSpan.FromSeconds(1);
        launch.Connected = true;
        launch.PumpTicks(1);
        launch.Now = TimeSpan.FromSeconds(3);
        launch.Connected = false;
        launch.PumpTicks(1);
        launch.Now = TimeSpan.FromMilliseconds(3500);
        launch.Connected = true;
        launch.PumpTicks(1);
        launch.Now = TimeSpan.FromSeconds(4);
        launch.PumpTicks(2);
        launch.Now = TimeSpan.FromMilliseconds(6499);
        launch.PumpTicks(2);
        Assert.Empty(launch.Raised);

        launch.Now = TimeSpan.FromMilliseconds(6500);
        Assert.True(TimerHarness.PumpUntil(() => launch.Raised.Count >= 1), "the fresh hold never ran out");
        TimerHarness.PumpFor(TimeSpan.FromMilliseconds(750));
        Assert.Equal(["Vault opened."], launch.Raised);
        Assert.Equal(["lines=1, droppedOldest=0, at=6500ms, advise=absent, drained=timeout"], launch.Drains);
    });

    /// <summary>
    /// OD-7, codex round 19: Done is launch bookkeeping only. After a normal
    /// flip, a conjunct that regresses — the status provider lost, or no
    /// client listening any more — makes a post queue again, never raise into
    /// a deaf process, and restarts the poll; when readiness returns on
    /// another thread with no further post, production's own timer delivers
    /// that line exactly once and stops. The fact pumps the dispatcher and
    /// never calls the check.
    /// </summary>
    [Fact]
    public void AfterDoneALineWaitsWhileReadinessRegressesAndOneTickDeliversIt() => RunSta(() =>
    {
        var launch = new TimerHarness { Connected = false };
        launch.Post("Vault opened.");
        Task connect = TimerHarness.Later(() => launch.Connected = true);
        Assert.True(TimerHarness.PumpUntil(() => launch.Raised.Count == 1), "no tick flipped the launch phase");
        connect.GetAwaiter().GetResult();

        launch.Connected = false;
        launch.Post("Right pane hidden.");
        Assert.Equal(["Vault opened."], launch.Raised);
        Task reconnect = TimerHarness.Later(() => launch.Connected = true);
        Assert.True(TimerHarness.PumpUntil(() => launch.Raised.Count == 2), "no tick delivered the line once the provider returned");
        reconnect.GetAwaiter().GetResult();

        launch.Listening = false;
        launch.Post("Right pane shown.");
        Assert.Equal(["Vault opened.", "Right pane hidden."], launch.Raised);
        Task listen = TimerHarness.Later(() => launch.Listening = true);
        Assert.True(TimerHarness.PumpUntil(() => launch.Raised.Count == 3), "no tick delivered the line once a client listened again");
        listen.GetAwaiter().GetResult();

        TimerHarness.PumpFor(TimeSpan.FromMilliseconds(750));
        Assert.Equal(["Vault opened.", "Right pane hidden.", "Right pane shown."], launch.Raised);
        Assert.Equal(Enumerable.Repeat("lines=1, droppedOldest=0, at=0ms, advise=present, drained=advise", 3), launch.Drains);
        Assert.Equal((3, 3), (launch.PollsStarted, launch.PollsStopped));
    });

    /// <summary>
    /// OD-7, codex round 18 (iii): after the launch a queued line waits at
    /// most the launch window from its own post and is then dropped unspoken
    /// — never raised late, not even when readiness arrives on the very tick
    /// it turns thirty seconds old — and when the last one goes the poll
    /// stops, so it always ends. Production's own timer, pumped.
    /// </summary>
    [Fact]
    public void AQueuedLineIsDroppedThirtySecondsAfterItsPostNeverRaisedLate() => RunSta(() =>
    {
        TimeSpan window = AccessibilityNotificationDispatcher.LaunchWindow;
        var launch = new TimerHarness { Connected = false, Now = window };
        launch.Post("Pane resized, 60 percent.");
        launch.Now = window + TimeSpan.FromSeconds(10);
        launch.Post("Right pane hidden.");

        launch.Now = window + window - TimeSpan.FromMilliseconds(1);
        launch.PumpTicks(2);
        Assert.Equal(["expired, never ready"], launch.Drains);

        launch.Now = window + window;
        Assert.True(TimerHarness.PumpUntil(() => launch.Drains.Count == 2), "no tick dropped the line posted thirty seconds earlier");
        Assert.Equal((1, 0), (launch.PollsStarted, launch.PollsStopped));

        launch.Now = window + window + TimeSpan.FromSeconds(10);
        launch.Connected = true;
        Assert.True(TimerHarness.PumpUntil(() => launch.Drains.Count == 3), "no tick dropped the second line at its thirty seconds");
        Assert.Empty(launch.Raised);
        Assert.Equal((1, 1), (launch.PollsStarted, launch.PollsStopped));

        launch.Connected = false;
        launch.Now = window + window + TimeSpan.FromSeconds(20);
        launch.Post("Right pane shown.");
        launch.Now = window + window + window + TimeSpan.FromSeconds(20);
        Assert.True(TimerHarness.PumpUntil(() => launch.Drains.Count == 4), "no tick dropped the third line at its thirty seconds");
        Assert.Equal(
            ["expired, never ready", "dropped=1, unready too long", "dropped=1, unready too long", "dropped=1, unready too long"],
            launch.Drains);
        Assert.Equal((2, 2), (launch.PollsStarted, launch.PollsStopped));
        int ticks = launch.Ticks;
        TimerHarness.PumpFor(TimeSpan.FromMilliseconds(600));
        Assert.Equal(ticks, launch.Ticks);

        launch.Connected = true;
        launch.Post("Scan complete. 2 files indexed.");
        Assert.Equal(["Scan complete. 2 files indexed."], launch.Raised);
    });

    /// <summary>
    /// OD-7, codex round 20: expiry is per entry, never a wholesale clear.
    /// Each queued line expires thirty seconds after its own post — the
    /// launch's expiry is only its lines, posted by the first frame, reaching
    /// their deadline — so under the fake clock an old entry and a young one
    /// posted at 29 s part at 30 s: the old one is dropped and the young one
    /// still waits, past 58 s; and when readiness returns before its deadline,
    /// with no further post, production's own timer delivers it exactly once.
    /// </summary>
    [Fact]
    public void EachQueuedLineExpiresAtItsOwnDeadlineNeverTheWholeQueue() => RunSta(() =>
    {
        TimeSpan window = AccessibilityNotificationDispatcher.LaunchWindow;
        var launch = new TimerHarness { Connected = false };
        launch.Post("Vault opened.");
        launch.Now = window - TimeSpan.FromSeconds(1);
        launch.Post("Pane resized, 60 percent.");

        launch.Now = window;
        Assert.True(TimerHarness.PumpUntil(() => launch.Drains.Count == 2), "no tick expired the old line");
        Assert.Equal(["dropped=1, unready too long", "expired, never ready"], launch.Drains);

        launch.Now = window + window - TimeSpan.FromSeconds(1) - TimeSpan.FromMilliseconds(1);
        launch.PumpTicks(2);
        Assert.Equal(["dropped=1, unready too long", "expired, never ready"], launch.Drains);
        Assert.Empty(launch.Raised);
        Assert.Equal((1, 0), (launch.PollsStarted, launch.PollsStopped));

        Task connect = TimerHarness.Later(() => launch.Connected = true);
        Assert.True(TimerHarness.PumpUntil(() => launch.Raised.Count >= 1), "no tick delivered the young line once readiness returned");
        connect.GetAwaiter().GetResult();
        TimerHarness.PumpFor(TimeSpan.FromMilliseconds(750));
        Assert.Equal(["Pane resized, 60 percent."], launch.Raised);
        Assert.Equal(
            ["dropped=1, unready too long", "expired, never ready", "lines=1, droppedOldest=0, at=58999ms, advise=present, drained=advise"],
            launch.Drains);
        Assert.Equal((1, 1), (launch.PollsStarted, launch.PollsStopped));
    });

    /// <summary>OD-7: the same rule after Done — a line posted while the
    /// status provider is null waits for it, and is raised once, in order,
    /// when it connects. Done never goes back: the launch window closing while
    /// that line waits neither expires anything nor drops it early.</summary>
    [Fact]
    public void AfterDoneALineWaitsForTheProviderAcrossTheLaunchWindow()
    {
        var launch = new LaunchHarness { Connected = true };
        launch.Post("Vault opened.");
        launch.Connected = false;
        launch.Now = TimeSpan.FromSeconds(20);
        launch.Post("Scan complete. 2 files indexed.");
        Assert.Equal(["Vault opened."], launch.Raised.Select(line => line.Text));

        launch.Now = AccessibilityNotificationDispatcher.LaunchWindow;
        launch.Tick!();
        Assert.Empty(launch.Drains);

        launch.Connected = true;
        launch.Now = AccessibilityNotificationDispatcher.LaunchWindow + TimeSpan.FromSeconds(1);
        launch.Tick!();
        Assert.Equal(["Vault opened.", "Scan complete. 2 files indexed."], launch.Raised.Select(line => line.Text));
        Assert.Equal(["lines=1, droppedOldest=0, at=31000ms, advise=present, drained=advise"], launch.Drains);
        Assert.Null(launch.Tick);
    }

    /// <summary>OD-7: a process ready at its first post — a listening client
    /// and a connected provider — ends the phase there: the line is raised at
    /// once, exactly once, and nothing is queued or polled for.</summary>
    [Fact]
    public void AProcessReadyAtItsFirstLineRaisesEveryLineAtOnce()
    {
        var launch = new LaunchHarness { Connected = true };
        launch.Post("Vault opened.", "Scanning vault. 2 files to index.");
        Assert.Equal(["Vault opened.", "Scanning vault. 2 files to index."], launch.Raised.Select(line => line.Text));
        Assert.Equal(0, launch.PollsStarted);
        Assert.Empty(launch.Drains);
        Assert.Equal([(HostDiagnosticEvent.AnnouncementSource, "statusPeerProvider=connected")], launch.Logged);
    }

    /// <summary>OD-7, codex round 1: a screen reader started shortly AFTER
    /// Slate still hears the launch lines. Posted while no client listens at
    /// all, they are queued — not raised, not discarded — and when a client
    /// listens and the provider connects, exactly those tuples drain once, in
    /// order.</summary>
    [Fact]
    public void LinesPostedBeforeAnyClientListensAreQueuedForAReaderStartedLater()
    {
        var launch = new LaunchHarness { Listening = false };
        launch.Post("Vault opened.", "Scanning vault. 2 files to index.");
        launch.Dispatcher.Post(new RenderedAnnouncement("Could not open vault.", A11yPriority.High));
        launch.Tick!();
        Assert.Empty(launch.Raised);

        launch.Listening = true;
        launch.Connected = true;
        launch.Tick!();
        Assert.Equal(
            [
                new Notification(AutomationNotificationKind.Other, AutomationNotificationProcessing.All, "Vault opened.", "slate-accessibility-announcement"),
                new Notification(AutomationNotificationKind.Other, AutomationNotificationProcessing.All, "Scanning vault. 2 files to index.", "slate-accessibility-announcement"),
                new Notification(AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent, "Could not open vault.", "slate-accessibility-announcement"),
            ],
            launch.Raised);
        Assert.Null(launch.Tick);
        Assert.Equal(1, launch.PollsStarted);
    }

    /// <summary>OD-7: the phase ends only when a listening client and a
    /// connected status provider are both there (UIA's advise present, as the
    /// harness has it). Either one missing keeps the lines queued, and the
    /// post that completes the two drains them before its own line.</summary>
    [Fact]
    public void TheQueueWaitsForAListeningClientAndAConnectedProvider()
    {
        var launch = new LaunchHarness();
        launch.Post("Vault opened.");
        launch.Tick!();
        launch.Connected = true;
        launch.Listening = false;
        launch.Tick!();
        Assert.Empty(launch.Raised);

        launch.Listening = true;
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
        launch.Connected = true;
        launch.Tick!();
        Assert.Equal(lines.Skip(4), launch.Raised.Select(line => line.Text));
        Assert.Equal(["lines=16, droppedOldest=4, at=0ms, advise=present, drained=advise"], launch.Drains);
    }

    /// <summary>OD-7: no readiness within the launch window of the first frame
    /// expires the launch — its lines, posted by the first frame, reach their
    /// own deadline and are dropped unspoken, and the poll stops; expiry
    /// replays nothing. A later line waits for readiness like any other and is
    /// raised once when it comes; nothing dropped ever comes back.</summary>
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
        Assert.Equal(["dropped=2, unready too long", "expired, never ready"], launch.Drains);

        launch.Post("Scan complete. 2 files indexed.");
        Assert.Empty(launch.Raised);
        launch.Connected = true;
        launch.Post("Right pane hidden.");
        Assert.Equal(["Scan complete. 2 files indexed.", "Right pane hidden."], launch.Raised.Select(line => line.Text));
        Assert.Equal(
            ["dropped=2, unready too long", "expired, never ready", "lines=1, droppedOldest=0, at=30000ms, advise=present, drained=advise"],
            launch.Drains);
        Assert.Equal(2, launch.PollsStarted);
    }

    /// <summary>OD-7: the drain writes one diagnostics line (under
    /// SLATE_UIA_DIAGNOSTICS=1 in production) with its time and what released
    /// it — the advise, or the hold running out without one — and a flip with
    /// nothing queued writes none; the expiry's and the drops' own lines are
    /// the expiry facts'.</summary>
    [Fact]
    public void TheLaunchTransitionIsLogged()
    {
        var launch = new LaunchHarness();
        launch.Post("Vault opened.", "Scanning vault. 2 files to index.");
        Assert.Empty(launch.Drains);
        launch.Now = TimeSpan.FromMilliseconds(1250);
        launch.Connected = true;
        launch.Post("Scan complete. 2 files indexed.");
        Assert.Equal(["lines=2, droppedOldest=0, at=1250ms, advise=present, drained=advise"], launch.Drains);
        launch.Post("Right pane hidden.");
        Assert.Equal(["lines=2, droppedOldest=0, at=1250ms, advise=present, drained=advise"], launch.Drains);

        var held = new LaunchHarness { Advised = false };
        held.Post("Vault opened.");
        held.Connected = true;
        held.Tick!();
        Assert.Empty(held.Drains);
        held.Now = AccessibilityNotificationDispatcher.AdviseHold;
        held.Tick!();
        Assert.Equal(["lines=1, droppedOldest=0, at=3000ms, advise=absent, drained=timeout"], held.Drains);

        var quiet = new LaunchHarness { Connected = true };
        quiet.Post("Vault opened.");
        Assert.Empty(quiet.Drains);
    }

    /// <summary>
    /// OD-7 at the replay boundary, hosted: the queue drains only through a
    /// connected status provider — production's own probe over a real shown
    /// window. Whatever the desktop does, every raise happens with the status
    /// peer's provider connected, and each line exactly once, in order; when
    /// the window's root was not connected at the posts (no client had asked
    /// it, and WPF's event map had no listener to connect it), nothing is
    /// raised until a request for the UIA root connects it.
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
        var raised = new List<(string Text, bool Connected)>();
        Action? tick = null;
        try
        {
            IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
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
                    (_, _) => { }));

            // Nothing pumps this thread from here to the ask, so no client and
            // no event-map listener can connect the root in between.
            bool connectedAtThePosts = AccessibilityNotificationDispatcher.NotificationSource.Of(status) is not null;
            dispatcher.Post(new RenderedAnnouncement("Vault opened.", A11yPriority.Medium));
            dispatcher.Post(new RenderedAnnouncement("Scanning vault. 2 files to index.", A11yPriority.Medium));
            if (!connectedAtThePosts)
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

            launch.Connected = true;
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

    /// <summary>
    /// Codex round 2, production wiring: every launch-phase check asks for the
    /// status provider — never short-circuited behind the client and advise
    /// probes — so the provider state under the launch condition is recorded
    /// at the FIRST unadvised check, before any drain. Production's own
    /// provider probe over a real shown window; the record matches the
    /// provider's state at that post, however the desktop left the window.
    /// </summary>
    [Fact]
    public void TheFirstUnadvisedCheckRecordsTheProviderStateBeforeTheDrain() => RunSta(() =>
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
        bool listening = false;
        Action? tick = null;
        var logged = new List<(HostDiagnosticEvent Event, string Line)>();
        try
        {
            IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
            window.Show();
            var dispatcher = new AccessibilityNotificationDispatcher(
                (kind, processing, text, activityId) => { },
                () => listening,
                AccessibilityNotificationDispatcher.LaunchSeams.ForProduction(status) with
                {
                    Advised = () => listening,
                    StartPoll = poll =>
                    {
                        tick = poll;
                        return new StopPoll(() => tick = null);
                    },
                    Elapsed = () => TimeSpan.Zero,
                    Diagnose = (diagnosticEvent, line) => logged.Add((diagnosticEvent, line)),
                });

            bool connectedAtFirst = AccessibilityNotificationDispatcher.NotificationSource.Of(status) is not null;
            dispatcher.Post(new RenderedAnnouncement("Vault opened.", A11yPriority.Medium));
            Assert.Equal(
                (HostDiagnosticEvent.AnnouncementSource, connectedAtFirst ? "statusPeerProvider=connected" : "statusPeerProvider=null"),
                Assert.Single(logged));

            _ = NativeWindow.SendMessage(handle, NativeWindow.WmGetObject, IntPtr.Zero, NativeWindow.UiaRootObjectId);
            listening = true;
            tick!();
            Assert.Equal(HostDiagnosticEvent.AnnouncementSource, logged[0].Event);
            Assert.Equal(
                (HostDiagnosticEvent.AnnouncementReplay, "lines=1, droppedOldest=0, at=0ms, advise=present, drained=advise"),
                logged[^1]);
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>
    /// Codex round 2: OD-7's launch window is measured from the FIRST FRAME,
    /// not from construction. Production's clock over a real window reads
    /// zero until the window has rendered — however long construction and
    /// start-up took — and starts then.
    /// </summary>
    [Fact]
    public void TheLaunchWindowStartsAtTheFirstFrameNotAtConstruction() => RunSta(() =>
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
        try
        {
            AccessibilityNotificationDispatcher.LaunchSeams launch = AccessibilityNotificationDispatcher.LaunchSeams.ForProduction(status);
            TimeSpan startUp = TimeSpan.FromSeconds(1);
            Thread.Sleep(startUp);
            Assert.Equal(TimeSpan.Zero, launch.Elapsed());

            bool rendered = false;
            window.ContentRendered += (_, _) => rendered = true;
            window.Show();
            Assert.True(PumpedDispatcher.PumpUntil(() => rendered), "the window never rendered its first frame");
            TimeSpan sinceFirstFrame = launch.Elapsed();
            Assert.True(
                sinceFirstFrame < startUp,
                $"the launch window counted construction: {sinceFirstFrame} after the first frame, start-up was {startUp}");
            Thread.Sleep(TimeSpan.FromMilliseconds(100));
            Assert.True(launch.Elapsed() > sinceFirstFrame, "the launch window never started counting");
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>The launch facts' dispatcher: a recording raise, and every
    /// launch input in the fact's hands — whether a client listens, whether
    /// WPF's map has been advised, whether the status provider is connected,
    /// the poll's tick, the clock and the log. A client listens and UIA has
    /// advised the process, and the provider is not yet connected, as at a
    /// launch's first frame, unless a fact says otherwise.</summary>
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
                    (diagnosticEvent, line) => Logged.Add((diagnosticEvent, line))));
        }

        internal AccessibilityNotificationDispatcher Dispatcher { get; }

        internal List<Notification> Raised { get; } = [];

        internal List<(HostDiagnosticEvent Event, string Line)> Logged { get; } = [];

        /// <summary>The drain and expiry lines, in order.</summary>
        internal string[] Drains => [.. Logged
            .Where(entry => entry.Event == HostDiagnosticEvent.AnnouncementReplay)
            .Select(entry => entry.Line)];

        internal bool Listening { get; set; } = true;

        internal bool Advised { get; set; } = true;

        internal bool Connected { get; set; }

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

    /// <summary>
    /// The timer-driven facts' dispatcher: production's own readiness poll —
    /// the DispatcherTimer <c>LaunchSeams.ForProduction</c> starts, which
    /// ticks only while this STA thread pumps — over inputs the fact holds:
    /// whether a client listens, whether UIA has advised the process and
    /// whether the provider is connected (flags another thread may flip, as
    /// UIA does), the clock and the log. All three hold unless the fact says
    /// otherwise.
    /// </summary>
    private sealed class TimerHarness
    {
        private volatile bool _listening = true;
        private volatile bool _advised = true;
        private volatile bool _connected = true;

        internal TimerHarness()
        {
            AccessibilityNotificationDispatcher.LaunchSeams production =
                AccessibilityNotificationDispatcher.LaunchSeams.ForProduction(new TextBlock());
            Dispatcher = new AccessibilityNotificationDispatcher(
                (kind, processing, text, activityId) => Raised.Add(text),
                () => _listening,
                production with
                {
                    Advised = () => _advised,
                    Connected = () => _connected,
                    StartPoll = tick =>
                    {
                        PollsStarted++;
                        IDisposable timer = production.StartPoll(() =>
                        {
                            Ticks++;
                            tick();
                        });
                        return new StopPoll(() =>
                        {
                            PollsStopped++;
                            timer.Dispose();
                        });
                    },
                    Elapsed = () => Now,
                    Diagnose = (diagnosticEvent, line) =>
                    {
                        if (diagnosticEvent == HostDiagnosticEvent.AnnouncementReplay)
                        {
                            Drains.Add(line);
                        }
                    },
                });
        }

        internal AccessibilityNotificationDispatcher Dispatcher { get; }

        internal List<string> Raised { get; } = [];

        /// <summary>The drain, expiry and drop lines, in order.</summary>
        internal List<string> Drains { get; } = [];

        internal bool Listening
        {
            get => _listening;
            set => _listening = value;
        }

        internal bool Advised
        {
            get => _advised;
            set => _advised = value;
        }

        internal bool Connected
        {
            get => _connected;
            set => _connected = value;
        }

        internal TimeSpan Now { get; set; }

        internal int Ticks { get; private set; }

        internal int PollsStarted { get; private set; }

        internal int PollsStopped { get; private set; }

        /// <summary>Flips an input from a thread-pool thread a little later,
        /// with no post: only the poll can notice.</summary>
        internal static Task Later(Action flip) => Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            flip();
        });

        /// <summary>Runs this thread's dispatcher — the only way the poll's
        /// DispatcherTimer ticks — until the condition holds or ten seconds
        /// pass (a full suite can starve the thread pool the flips run on);
        /// the return value is the condition's final answer.</summary>
        internal static bool PumpUntil(Func<bool> condition) => Pump(condition, TimeSpan.FromSeconds(10));

        /// <summary>Runs this thread's dispatcher for the whole duration.</summary>
        internal static void PumpFor(TimeSpan duration) => _ = Pump(() => false, duration);

        internal void Post(params string[] lines)
        {
            foreach (string line in lines)
            {
                Dispatcher.Post(new RenderedAnnouncement(line, A11yPriority.Medium));
            }
        }

        /// <summary>Pumps until the poll has ticked <paramref name="count"/>
        /// more times.</summary>
        internal void PumpTicks(int count)
        {
            int target = Ticks + count;
            Assert.True(PumpUntil(() => Ticks >= target), $"the poll ticked {count - (target - Ticks)} of {count} times");
        }

        private static bool Pump(Func<bool> condition, TimeSpan budget)
        {
            var frame = new DispatcherFrame();
            var clock = Stopwatch.StartNew();
            var check = new DispatcherTimer(DispatcherPriority.Background, System.Windows.Threading.Dispatcher.CurrentDispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(5),
            };
            check.Tick += (_, _) =>
            {
                if (condition() || clock.Elapsed >= budget)
                {
                    check.Stop();
                    frame.Continue = false;
                }
            };
            check.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            return condition();
        }
    }

    /// <summary>Seams for the facts about the raise itself: the process is
    /// advised and the provider connected, so the first post ends the launch
    /// phase with nothing queued, and no poll ever starts.</summary>
    private static AccessibilityNotificationDispatcher.LaunchSeams AlwaysReady() => new(
        () => true,
        () => true,
        _ => throw new InvalidOperationException("A ready process starts no launch poll."),
        () => TimeSpan.Zero,
        (diagnosticEvent, line) =>
        {
            if (diagnosticEvent == HostDiagnosticEvent.AnnouncementReplay)
            {
                throw new InvalidOperationException($"A ready process logs no drain: {line}");
            }
        });

    private static AccessibilityNotificationDispatcher Recording(List<Notification> raised) =>
        new((kind, processing, text, activityId) =>
            raised.Add(new Notification(kind, processing, text, activityId)),
            () => true,
            AlwaysReady());

    private sealed record Notification(AutomationNotificationKind Kind,
        AutomationNotificationProcessing Processing, string Text, string ActivityId);
}
