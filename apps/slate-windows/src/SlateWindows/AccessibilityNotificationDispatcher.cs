// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>One canonical A11yEvent-to-UIA notification funnel.</summary>
/// <remarks>
/// R-1 (#1244): the notification is raised through UI Automation itself,
/// never through <c>AutomationPeer.RaiseNotificationEvent</c>. WPF gates
/// that call on a process-static listener map that only
/// <c>ElementProxy.AdviseEventAdded</c> fills, and in the 2026-09-22 NVDA
/// run UIA never advised the main window for the already-running reader:
/// every announcement from launch died there until a menu's popup window
/// was advised (record F1). The guard here is UIA's own answer to "is
/// anyone listening", which that advise gap cannot hide; the source it is
/// raised on is <see cref="NotificationSource"/>'s.
/// </remarks>
internal sealed class AccessibilityNotificationDispatcher
{
    private const string ActivityId = "slate-accessibility-announcement";

    // The listener state last written to the diagnostics log, packed as
    // two bits (a client listens; WPF's map knows of it), -1 before the
    // first. Process-wide like both facts it reports, so the window's
    // dispatcher and a grid's write one line per change between them.
    private static int s_loggedListenerState = -1;

    private readonly Action<AutomationNotificationKind, AutomationNotificationProcessing, string, string> _raise;
    private readonly Func<bool> _clientsAreListening;

    public AccessibilityNotificationDispatcher(FrameworkElement source)
        : this(
            (kind, processing, text, activityId) =>
            {
                IRawElementProviderSimple? provider = NotificationSource.Of(source);
                if (provider is not null)
                {
                    AutomationInteropProvider.RaiseAutomationEvent(
                        AutomationElementIdentifiers.NotificationEvent,
                        provider,
                        new NotificationEventArgs(kind, processing, text, activityId));
                }
            },
            () => AutomationInteropProvider.ClientsAreListening)
    {
        ArgumentNullException.ThrowIfNull(source);
    }

    // The native raise is a static UIA call, so nothing can override it.
    // Recording this required boundary tests the actual native arguments;
    // production always supplies NotificationSource's provider. The
    // listener probe is injected beside it, so the guard is a fact
    // (ProductionRaiseSkipsWhenNoClientListens) rather than a comment.
    internal AccessibilityNotificationDispatcher(
        Action<AutomationNotificationKind, AutomationNotificationProcessing, string, string> raise,
        Func<bool> clientsAreListening)
    {
        ArgumentNullException.ThrowIfNull(raise);
        ArgumentNullException.ThrowIfNull(clientsAreListening);
        _raise = raise;
        _clientsAreListening = clientsAreListening;
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
        // R-1: cheap when nobody listens, and asked of UIA, which knows —
        // never of WPF's listener map, the gate this raise exists to skip.
        bool clientsListening = _clientsAreListening();
        LogListenerState(clientsListening);
        if (!clientsListening)
        {
            return;
        }

        _raise(
            AutomationNotificationKind.Other,
            processing,
            rendered.Text,
            ActivityId);
    }

    /// <summary>
    /// Under SLATE_UIA_DIAGNOSTICS=1, once per change: whether UIA reports
    /// a listening client, and whether WPF's listener map (the old raise's
    /// gate) has been told of one. A run can then tell "raised into a deaf
    /// process" from "not raised"; <c>True, False</c> is F1's state itself.
    /// </summary>
    private static void LogListenerState(bool clientsListening)
    {
        if (ListenerStateChange(
                ref s_loggedListenerState,
                clientsListening,
                AutomationPeer.ListenerExists(AutomationEvents.Notification)) is { } change)
        {
            HostLog.WriteUiAutomationDiagnostic(HostDiagnosticEvent.AnnouncementListenerState, change);
        }
    }

    /// <summary>The diagnostic line for this listener state, or null when
    /// it is the state <paramref name="last"/> already holds (packed as
    /// written above) — so a state is logged once per change, never once
    /// per announcement.</summary>
    internal static string? ListenerStateChange(ref int last, bool clientsListening, bool notificationListenerExists)
    {
        int state = (clientsListening ? 1 : 0) | (notificationListenerExists ? 2 : 0);
        return Interlocked.Exchange(ref last, state) == state
            ? null
            : $"clientsListening={clientsListening}, notificationListenerExists={notificationListenerExists}";
    }

    /// <summary>
    /// R-1: the provider a notification is raised on — the source element's
    /// own, through its peer resolved as the dispatcher always has. It exists
    /// only once a UIA client has asked the window for its root: at the first
    /// frame <c>ProviderFromPeer</c> answers null
    /// (<c>AtTheFirstFrameTheStatusPeerHasNoProviderUntilTheWindowIsAsked</c>
    /// records it), and a line posted then is not raised, exactly as WPF's
    /// gated call would not raise it (codex round 5). The window's HWND host
    /// provider was measured as the alternative and delivered nothing to a
    /// desktop-scoped client, advised or not, so it is not used.
    /// </summary>
    internal static class NotificationSource
    {
        // Whether the status peer had a provider at the last logged raise:
        // 1 or 0, -1 before the first. Process-wide, like the listener state.
        private static int s_loggedSource = -1;

        internal static IRawElementProviderSimple? Of(FrameworkElement source)
        {
            AutomationPeer peer = UIElementAutomationPeer.FromElement(source)
                ?? UIElementAutomationPeer.CreatePeerForElement(source)
                ?? new FrameworkElementAutomationPeer(source);
            IRawElementProviderSimple? provider = NotificationProviderPeer.Current.ProviderOf(peer);
            if (SourceChange(ref s_loggedSource, provider is not null) is { } change)
            {
                HostLog.WriteUiAutomationDiagnostic(HostDiagnosticEvent.AnnouncementSource, change);
            }

            return provider;
        }

        /// <summary>Under SLATE_UIA_DIAGNOSTICS=1, the line for this raise's
        /// source, or null when it matches the one <paramref name="last"/>
        /// holds — once per change, never once per announcement; a
        /// <c>null</c> provider is a line that was not raised.</summary>
        internal static string? SourceChange(ref int last, bool connected) =>
            Interlocked.Exchange(ref last, connected ? 1 : 0) == (connected ? 1 : 0)
                ? null
                : connected
                    ? "statusPeerProvider=connected, raised"
                    : "statusPeerProvider=null, not raised";
    }

    /// <summary>
    /// The door to a peer's UIA provider. <c>ProviderFromPeer</c> is
    /// protected internal on <see cref="AutomationPeer"/>, so only a peer
    /// can open it; this one exists for nothing else. Its element is
    /// private and never shown, so it is never a provider itself.
    /// </summary>
    private sealed class NotificationProviderPeer : FrameworkElementAutomationPeer
    {
        [ThreadStatic]
        private static NotificationProviderPeer? t_current;

        private NotificationProviderPeer()
            : base(new FrameworkElement())
        {
        }

        /// <summary>The raising thread's instance, created there on first
        /// use: a peer belongs to the thread of its element.</summary>
        internal static NotificationProviderPeer Current => t_current ??= new NotificationProviderPeer();

        /// <summary>The provider UIA knows <paramref name="peer"/> by, or
        /// null while no UIA-connected window reaches it.</summary>
        internal IRawElementProviderSimple? ProviderOf(AutomationPeer peer) => ProviderFromPeer(peer);
    }
}
