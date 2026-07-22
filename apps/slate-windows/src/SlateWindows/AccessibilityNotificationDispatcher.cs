// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>One canonical A11yEvent-to-UIA notification funnel.</summary>
internal sealed class AccessibilityNotificationDispatcher
{
    private const string ActivityId = "slate-accessibility-announcement";
    private readonly FrameworkElement _source;

    public AccessibilityNotificationDispatcher(FrameworkElement source)
    {
        _source = source;
    }

    public void Post(A11yEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        RenderedAnnouncement rendered = SlateUniffiMethods.A11yRender(@event);
        AutomationPeer peer = UIElementAutomationPeer.FromElement(_source)
            ?? UIElementAutomationPeer.CreatePeerForElement(_source)
            ?? new FrameworkElementAutomationPeer(_source);
        AutomationNotificationProcessing processing = rendered.Priority switch
        {
            A11yPriority.High => AutomationNotificationProcessing.ImportantMostRecent,
            _ => AutomationNotificationProcessing.MostRecent,
        };
        peer.RaiseNotificationEvent(
            AutomationNotificationKind.Other,
            processing,
            rendered.Text,
            ActivityId);
    }
}
