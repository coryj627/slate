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
        Post(SlateUniffiMethods.A11yRender(@event));
    }

    /// <summary>
    /// The rendered-pair primitive (W6-1 PR A, contract A5). A
    /// coalescer cannot hand back an event: the window's winner is
    /// decided AFTER the render and the loser is dropped without ever
    /// being spoken, so its queue holds rendered lines — the same seam
    /// shape mac's <c>CanvasAnnouncer</c> takes. Text and priority both
    /// stay core's either way.
    /// </summary>
    public void Post(RenderedAnnouncement rendered)
    {
        ArgumentNullException.ThrowIfNull(rendered);
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
