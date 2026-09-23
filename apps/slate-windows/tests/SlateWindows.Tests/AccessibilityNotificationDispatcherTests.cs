// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Automation;
using System.Windows.Automation.Peers;
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

    private static AccessibilityNotificationDispatcher Recording(List<Notification> raised) =>
        new((kind, processing, text, activityId) =>
            raised.Add(new Notification(kind, processing, text, activityId)),
            () => true);

    private sealed record Notification(AutomationNotificationKind Kind,
        AutomationNotificationProcessing Processing, string Text, string ActivityId);
}
