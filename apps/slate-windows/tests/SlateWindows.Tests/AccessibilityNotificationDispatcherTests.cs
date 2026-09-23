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

    /// <summary>R-1 (#1244): the production raise is guarded by UIA's own
    /// "is any client listening", injected here, and asked at EVERY post —
    /// a screen reader started after Slate hears the next line, which a
    /// probe read once at construction would never let it.</summary>
    [Fact]
    public void ProductionRaiseSkipsWhenNoClientListens()
    {
        bool listening = false;
        var raised = new List<Notification>();
        var dispatcher = new AccessibilityNotificationDispatcher(
            (kind, processing, text, activityId) => raised.Add(new Notification(kind, processing, text, activityId)),
            () => listening);

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
    /// R-1, codex round 5, recorded rather than assumed: at the first frame —
    /// the window shown, no UIA client yet to ask it for anything — the status
    /// element's peer has NO provider (ProviderFromPeer answers null: the peer
    /// is unconnected, the helper peer has no window, and the dispatcher has
    /// no automation root), so a line posted then is not raised, exactly as
    /// WPF's gated call would not raise it. The provider appears the moment
    /// something asks the window for its UIA root (WM_GETOBJECT, which is how
    /// every client connects), and not before.
    /// </summary>
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
        try
        {
            window.Show();
            Assert.Null(AccessibilityNotificationDispatcher.NotificationSource.Of(status));

            _ = NativeWindow.SendMessage(
                new WindowInteropHelper(window).Handle, NativeWindow.WmGetObject, IntPtr.Zero, NativeWindow.UiaRootObjectId);
            Assert.NotNull(AccessibilityNotificationDispatcher.NotificationSource.Of(status));
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>R-1's source diagnostic, the same once-per-change rule:
    /// whether the status peer had a provider — a line raised — or none — a
    /// line not raised (the launch condition the hosted fact records).</summary>
    [Fact]
    public void TheRaiseSourceIsReportedOncePerChange()
    {
        int last = -1;
        Assert.Equal("statusPeerProvider=null, not raised",
            AccessibilityNotificationDispatcher.NotificationSource.SourceChange(ref last, connected: false));
        Assert.Null(AccessibilityNotificationDispatcher.NotificationSource.SourceChange(ref last, connected: false));
        Assert.Equal("statusPeerProvider=connected, raised",
            AccessibilityNotificationDispatcher.NotificationSource.SourceChange(ref last, connected: true));
        Assert.Null(AccessibilityNotificationDispatcher.NotificationSource.SourceChange(ref last, connected: true));
        Assert.Equal("statusPeerProvider=null, not raised",
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

    private static AccessibilityNotificationDispatcher Recording(List<Notification> raised) =>
        new((kind, processing, text, activityId) =>
            raised.Add(new Notification(kind, processing, text, activityId)),
            () => true);

    private sealed record Notification(AutomationNotificationKind Kind,
        AutomationNotificationProcessing Processing, string Text, string ActivityId);
}
