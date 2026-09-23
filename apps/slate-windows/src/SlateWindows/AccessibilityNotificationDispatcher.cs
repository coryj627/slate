// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>One canonical A11yEvent-to-UIA notification funnel.</summary>
/// <remarks>
/// <para>
/// R-1 (#1244): the notification is raised through UI Automation itself,
/// never through <c>AutomationPeer.RaiseNotificationEvent</c>. WPF gates
/// that call on a process-static listener map that only
/// <c>ElementProxy.AdviseEventAdded</c> fills, and in the 2026-09-22 NVDA
/// run UIA never advised the main window for the already-running reader:
/// every announcement from launch died there until a menu's popup window
/// was advised (record F1). The guard here is UIA's own answer to "is
/// anyone listening", which that advise gap cannot hide; the source it is
/// raised on is <see cref="NotificationSource"/>'s.
/// </para>
/// <para>
/// OD-7 (2026-09-24), the launch phase: a monotonic Unadvised → Done. While
/// Unadvised — no listening client, no advise in WPF's map, or no connected
/// status provider — a line is QUEUED, never raised: raising it could reach a
/// client UIA already knows of (measured: it did, on every unadvised launch)
/// and then be raised again. The first check that finds all three (every
/// post, and a <see cref="LaunchPollInterval"/> poll on the UI thread) drains
/// the queue once, in order, through the same raiser, and the phase is Done:
/// every later line is raised at once, as before. No advise within
/// <see cref="LaunchWindow"/> of the first frame ends the phase too, dropping
/// the queue unspoken — a deaf process stays deaf and nothing stale is raised
/// later. The queue keeps the last <see cref="LaunchQueueCapacity"/> lines
/// (the launch lines are about five). Nothing is composed: the drain raises
/// core's rendered tuples.
/// </para>
/// </remarks>
internal sealed class AccessibilityNotificationDispatcher
{
    /// <summary>
    /// OD-7: the window's ONE dispatcher, inherited by everything inside it.
    /// MainWindow sets it on itself; an element that announces on its own
    /// behalf (<see cref="Grids.AccessibleDataGrid"/>'s default seam) finds
    /// it with <see cref="For"/>, so a window has one launch phase, one
    /// queue and one provider — the status element's — however many
    /// surfaces post into it.
    /// </summary>
    internal static readonly DependencyProperty AnnouncerProperty = DependencyProperty.RegisterAttached(
        "Announcer",
        typeof(AccessibilityNotificationDispatcher),
        typeof(AccessibilityNotificationDispatcher),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>OD-7: how many lines the unadvised launch phase keeps; the
    /// oldest drops first.</summary>
    internal const int LaunchQueueCapacity = 16;

    /// <summary>OD-7: how long after the first frame the launch phase waits
    /// for the advise before it ends and drops what it queued.</summary>
    internal static readonly TimeSpan LaunchWindow = TimeSpan.FromSeconds(30);

    private const string ActivityId = "slate-accessibility-announcement";

    private static readonly TimeSpan LaunchPollInterval = TimeSpan.FromMilliseconds(250);

    // The listener state last written to the diagnostics log, packed as
    // two bits (a client listens; WPF's map knows of it), -1 before the
    // first. Process-wide like both facts it reports, so the window's
    // dispatcher and a grid's write one line per change between them.
    private static int s_loggedListenerState = -1;

    private readonly Action<AutomationNotificationKind, AutomationNotificationProcessing, string, string> _raise;
    private readonly Func<bool> _clientsAreListening;
    private readonly LaunchSeams _launch;

    // OD-7: the lines posted while Unadvised, oldest first; null once the
    // phase is Done (drained, or expired) — it never starts again.
    private Queue<QueuedLine>? _launchQueue = new();
    private int _droppedOldest;
    private IDisposable? _launchPoll;

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
            () => AutomationInteropProvider.ClientsAreListening,
            LaunchSeams.ForProduction(source))
    {
        ArgumentNullException.ThrowIfNull(source);
    }

    // The native raise is a static UIA call, so nothing can override it.
    // Recording this required boundary tests the actual native arguments;
    // production always supplies NotificationSource's provider. The
    // listener probe and the launch seams are injected beside it, so the
    // guard and the launch phase are facts (ProductionRaiseSkipsWhenNoClientListens,
    // LinesPostedBeforeTheAdviseAreQueuedThenRaisedOnceInOrder) rather than
    // comments.
    internal AccessibilityNotificationDispatcher(
        Action<AutomationNotificationKind, AutomationNotificationProcessing, string, string> raise,
        Func<bool> clientsAreListening,
        LaunchSeams launch)
    {
        ArgumentNullException.ThrowIfNull(raise);
        ArgumentNullException.ThrowIfNull(clientsAreListening);
        ArgumentNullException.ThrowIfNull(launch);
        _raise = raise;
        _clientsAreListening = clientsAreListening;
        _launch = launch;
    }

    internal static void SetAnnouncer(DependencyObject element, AccessibilityNotificationDispatcher? announcer) =>
        element.SetValue(AnnouncerProperty, announcer);

    internal static AccessibilityNotificationDispatcher? GetAnnouncer(DependencyObject element) =>
        (AccessibilityNotificationDispatcher?)element.GetValue(AnnouncerProperty);

    /// <summary>
    /// OD-7: the dispatcher an element posts through — its window's, inherited,
    /// or for an element in a window of its own (the reading view's table
    /// grid) the nearest owner window's. Null outside any window that has one
    /// (a hosted test, the conformance tool): nothing is announced there.
    /// </summary>
    internal static AccessibilityNotificationDispatcher? For(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (GetAnnouncer(element) is { } announcer)
        {
            return announcer;
        }

        for (Window? window = Window.GetWindow(element); window is not null; window = window.Owner)
        {
            if (GetAnnouncer(window) is { } owned)
            {
                return owned;
            }
        }

        return null;
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
        var line = new QueuedLine(AutomationNotificationKind.Other, processing, rendered.Text, ActivityId);
        // R-1: cheap when nobody listens, and asked of UIA, which knows —
        // never of WPF's listener map, the gate this raise exists to skip.
        // The map decides only when the launch phase ends (OD-7).
        bool clientsListening = _clientsAreListening();
        if (StillUnadvised(clientsListening))
        {
            QueueUntilAdvised(line);
            return;
        }

        if (!clientsListening)
        {
            return;
        }

        _raise(line.Kind, line.Processing, line.Text, line.ActivityId);
    }

    /// <summary>OD-7: whether the launch phase is still Unadvised after this
    /// check — which ends it when a listening client, an advise and a
    /// connected status provider are all there (the queue drains), or when
    /// the launch window has closed (the queue is dropped).</summary>
    private bool StillUnadvised(bool clientsListening)
    {
        bool advised = _launch.Advised();
        LogListenerState(clientsListening, advised);
        if (_launchQueue is null)
        {
            return false;
        }

        if (clientsListening && advised && _launch.Connected())
        {
            DrainLaunchQueue();
            return false;
        }

        if (_launch.Elapsed() >= LaunchWindow)
        {
            ExpireLaunchQueue();
            return false;
        }

        return true;
    }

    private void QueueUntilAdvised(QueuedLine line)
    {
        Queue<QueuedLine> queued = _launchQueue!;
        if (queued.Count == LaunchQueueCapacity)
        {
            _ = queued.Dequeue();
            _droppedOldest++;
        }

        queued.Enqueue(line);
        _launchPoll ??= _launch.StartPoll(() => _ = StillUnadvised(_clientsAreListening()));
    }

    /// <summary>OD-7's flip: each queued line raised exactly once, in order,
    /// through the same raiser; the phase is Done before the first of them.</summary>
    private void DrainLaunchQueue()
    {
        Queue<QueuedLine> queued = _launchQueue!;
        _launchQueue = null;
        StopLaunchPoll();
        foreach (QueuedLine line in queued)
        {
            _raise(line.Kind, line.Processing, line.Text, line.ActivityId);
        }

        if (queued.Count > 0 || _droppedOldest > 0)
        {
            _launch.Diagnose(
                $"drained={queued.Count}, droppedOldest={_droppedOldest}, at={(int)_launch.Elapsed().TotalMilliseconds}ms");
        }
    }

    /// <summary>OD-7: a launch window that closes unadvised drops its queue
    /// unspoken; later lines are raised at once, as before.</summary>
    private void ExpireLaunchQueue()
    {
        Queue<QueuedLine> queued = _launchQueue!;
        _launchQueue = null;
        StopLaunchPoll();
        int dropped = queued.Count + _droppedOldest;
        if (dropped > 0)
        {
            _launch.Diagnose($"expired={dropped}, never advised");
        }
    }

    private void StopLaunchPoll()
    {
        _launchPoll?.Dispose();
        _launchPoll = null;
    }

    /// <summary>
    /// Under SLATE_UIA_DIAGNOSTICS=1, once per change: whether UIA reports
    /// a listening client, and whether WPF's listener map (the old raise's
    /// gate) has been told of one. A run can then tell "raised into a deaf
    /// process" from "not raised"; <c>True, False</c> is F1's state itself.
    /// </summary>
    private static void LogListenerState(bool clientsListening, bool notificationListenerExists)
    {
        if (ListenerStateChange(ref s_loggedListenerState, clientsListening, notificationListenerExists) is { } change)
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
    /// OD-7's seams: whether UIA has advised the process of a notification
    /// listener (WPF's map, which only an advise fills), whether the status
    /// element has a connected provider to raise on, a poll for both, the
    /// time since the first frame, and the diagnostics sink. Production reads
    /// the map and <see cref="NotificationSource"/>, polls on the UI thread
    /// and logs under SLATE_UIA_DIAGNOSTICS=1; the launch facts hold each one.
    /// </summary>
    internal sealed record LaunchSeams(
        Func<bool> Advised,
        Func<bool> Connected,
        Func<Action, IDisposable> StartPoll,
        Func<TimeSpan> Elapsed,
        Action<string> Diagnose)
    {
        internal static LaunchSeams ForProduction(FrameworkElement source)
        {
            var started = Stopwatch.StartNew();
            return new LaunchSeams(
                () => AutomationPeer.ListenerExists(AutomationEvents.Notification),
                () => NotificationSource.Of(source) is not null,
                PollOnThisThread,
                () => started.Elapsed,
                line => HostLog.WriteUiAutomationDiagnostic(HostDiagnosticEvent.AnnouncementReplay, line));
        }

        // Posts arrive on the UI thread, so the poll ticks there too, beside
        // the raise it drains through.
        private static IDisposable PollOnThisThread(Action tick) =>
            new StopOnDispose(new DispatcherTimer(
                LaunchPollInterval, DispatcherPriority.Background, (_, _) => tick(), Dispatcher.CurrentDispatcher));

        private sealed class StopOnDispose(DispatcherTimer timer) : IDisposable
        {
            public void Dispose() => timer.Stop();
        }
    }

    /// <summary>One posted tuple, queued as rendered, so the drain raises
    /// exactly what core rendered.</summary>
    private readonly record struct QueuedLine(
        AutomationNotificationKind Kind,
        AutomationNotificationProcessing Processing,
        string Text,
        string ActivityId);

    /// <summary>
    /// R-1: the provider a notification is raised on — the source element's
    /// own, through its peer resolved as the dispatcher always has. It exists
    /// only once a UIA client has asked the window for its root: at the first
    /// frame <c>ProviderFromPeer</c> answers null
    /// (<c>AtTheFirstFrameTheStatusPeerHasNoProviderUntilTheWindowIsAsked</c>
    /// records it), and the launch phase keeps its lines until it answers
    /// (OD-7). The window's HWND host provider was measured as the
    /// alternative and delivered nothing to a desktop-scoped client, advised
    /// or not, so it is not used.
    /// </summary>
    internal static class NotificationSource
    {
        // Whether the status peer had a provider when last asked: 1 or 0, -1
        // before the first. Process-wide, like the listener state.
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

        /// <summary>Under SLATE_UIA_DIAGNOSTICS=1, the line for the status
        /// provider's state, or null when it matches the one <paramref
        /// name="last"/> holds — once per change, never once per ask.</summary>
        internal static string? SourceChange(ref int last, bool connected) =>
            Interlocked.Exchange(ref last, connected ? 1 : 0) == (connected ? 1 : 0)
                ? null
                : connected
                    ? "statusPeerProvider=connected"
                    : "statusPeerProvider=null";
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
