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

    private static AccessibilityNotificationDispatcher Recording(List<Notification> raised) =>
        new((kind, processing, text, activityId) =>
            raised.Add(new Notification(kind, processing, text, activityId)));

    private sealed record Notification(AutomationNotificationKind Kind,
        AutomationNotificationProcessing Processing, string Text, string ActivityId);
}
