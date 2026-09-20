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
    private readonly Action<AutomationNotificationKind, AutomationNotificationProcessing, string, string> _raise;

    public AccessibilityNotificationDispatcher(FrameworkElement source)
        : this((kind, processing, text, activityId) =>
        {
            AutomationPeer peer = UIElementAutomationPeer.FromElement(source)
                ?? UIElementAutomationPeer.CreatePeerForElement(source)
                ?? new FrameworkElementAutomationPeer(source);
            peer.RaiseNotificationEvent(kind, processing, text, activityId);
        })
    {
        ArgumentNullException.ThrowIfNull(source);
    }

    // RaiseNotificationEvent is nonvirtual. Recording this required boundary
    // tests the actual native arguments; production always supplies the peer.
    internal AccessibilityNotificationDispatcher(
        Action<AutomationNotificationKind, AutomationNotificationProcessing, string, string> raise)
    {
        ArgumentNullException.ThrowIfNull(raise);
        _raise = raise;
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
        AutomationNotificationProcessing processing = rendered.Priority switch
        {
            A11yPriority.High => AutomationNotificationProcessing.ImportantMostRecent,
            A11yPriority.Medium => AutomationNotificationProcessing.All,
            // D-1's exhaustiveness lives in the tests, which fail the build
            // when a third priority appears. At runtime an unknown value is
            // still an announcement: it degrades to the polite queue rather
            // than throwing out of a command handler on the UI thread.
            _ => AutomationNotificationProcessing.All,
        };
        _raise(
            AutomationNotificationKind.Other,
            processing,
            rendered.Text,
            ActivityId);
    }
}
