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
/// OD-7 (2026-09-24), readiness: ONE predicate in every phase — a listening
/// client and a connected status provider. WPF's Notification advise map is
/// not part of it: the map gates only WPF's own RaiseNotificationEvent, which
/// this dispatcher never calls, and the ungated raise was measured delivered
/// with the map empty (7 of 7), so the map is logged with each transition,
/// never waited for. A line is raised only when both hold; otherwise it is
/// QUEUED, never raised — there is no provider to raise on, or no client to
/// hear it — and a queued line is raised only by the drain, so no line is
/// raised twice. The queue keeps the last
/// <see cref="LaunchQueueCapacity"/> lines (the launch lines are about five). While it holds any, a <see cref="LaunchPollInterval"/>
/// poll on the UI thread checks again — the only production wake-up, so it
/// runs during the launch and after it alike and stops when the queue
/// empties — and the first ready check (a tick or a post) drains the queue
/// once, in order, through the same raiser. Every queued line expires
/// <see cref="LaunchWindow"/> after its own post and is dropped unspoken,
/// never raised late — never a wholesale clear — so the poll always ends.
/// The launch phase is bookkeeping only and moves forward once: from
/// Unadvised to Done when readiness is first observed and the queue drained,
/// or to Expired when <see cref="LaunchWindow"/> passes after the first frame
/// first. That expiry is only the launch lines, posted by the first frame,
/// reaching their own deadline; a line posted at 29 s survives until 59 s.
/// Both are final, and neither raises without readiness: a client that
/// stops listening or a provider lost after Done queues the line again, and
/// the poll restarts. Nothing is composed: the drain raises core's rendered
/// tuples.
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

    /// <summary>OD-7: how many lines the queue keeps while not ready; the
    /// oldest drops first.</summary>
    internal const int LaunchQueueCapacity = 16;

    /// <summary>OD-7: how long a queued line waits for readiness after its
    /// own post before it is dropped — and so how long after the first frame
    /// the launch phase lasts, its lines being posted by then.</summary>
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

    // OD-7: the launch phase (Unadvised, then Done or Expired, both final)
    // and the one bounded queue, oldest first, that holds every line posted
    // while not ready.
    private LaunchPhase _phase = LaunchPhase.Unadvised;
    private readonly Queue<QueuedLine> _queue = new();
    private int _droppedOldest;
    private IDisposable? _poll;

    // The status provider's state last recorded by a check: 1 or 0, -1
    // before the first.
    private int _recordedProviderState = -1;

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
    // readiness predicate and the launch phase are facts
    // (ProductionRaiseWaitsWhileNoClientListens,
    // AnEmptyAdviseMapNeverHoldsALine,
    // AfterDoneALineWaitsWhileReadinessRegressesAndOneTickDeliversIt,
    // AfterExpiryALineWaitsForTheProviderInsteadOfBeingLost) rather than
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
        Check(new QueuedLine(AutomationNotificationKind.Other, processing, rendered.Text, ActivityId));
    }

    /// <summary>
    /// One check (every post, and every tick of the poll): the ONE readiness
    /// predicate, the same in every phase — R-1's listening client (UIA's own
    /// answer) and a connected status provider, each asked every time and
    /// never short-circuited (codex round 2). WPF's advise map is read beside
    /// them for the diagnostics only. Ready, the queue drains once, in order,
    /// and then the posted line is raised. Unready, the posted line is queued
    /// and nothing is raised. Lines past their own deadline go first, so none
    /// is raised late. The phase is bookkeeping only, never a path that raises
    /// without readiness (codex round 19).
    /// </summary>
    private void Check(QueuedLine? posted)
    {
        bool clientsListening = _clientsAreListening();
        bool advised = _launch.Advised();
        LogListenerState(clientsListening, advised);
        bool connected = _launch.Connected();
        if (NotificationSource.SourceChange(ref _recordedProviderState, connected) is { } provider)
        {
            _launch.Diagnose(HostDiagnosticEvent.AnnouncementSource, provider);
        }

        bool ready = clientsListening && connected;
        TimeSpan now = _launch.Elapsed();
        DropExpiredLines(now);
        if (_phase == LaunchPhase.Unadvised)
        {
            if (ready)
            {
                _phase = LaunchPhase.Done;
            }
            else if (now >= LaunchWindow)
            {
                _phase = LaunchPhase.Expired;
                _launch.Diagnose(HostDiagnosticEvent.AnnouncementReplay, "expired, never ready");
            }
        }

        if (ready)
        {
            Drain(advised);
            if (posted is { } line)
            {
                _raise(line.Kind, line.Processing, line.Text, line.ActivityId);
            }

            return;
        }

        if (posted is { } unready)
        {
            Enqueue(unready with { PostedAt = now });
        }
    }

    /// <summary>Queues a line posted while not ready — the last
    /// <see cref="LaunchQueueCapacity"/> kept — and starts the poll unless it
    /// runs: the poll runs exactly while lines wait, during the launch and
    /// after it alike, because nothing else ever wakes the queue (neither an
    /// advise nor a provider connecting calls in).</summary>
    private void Enqueue(QueuedLine line)
    {
        if (_queue.Count == LaunchQueueCapacity)
        {
            _ = _queue.Dequeue();
            _droppedOldest++;
        }

        _queue.Enqueue(line);
        _poll ??= _launch.StartPoll(() => Check(null));
    }

    /// <summary>Each queued line raised exactly once, in order, through the
    /// same raiser at the first ready check — the launch's lines and later
    /// ones alike — and the poll stopped with the queue empty. The log line
    /// records whether WPF's advise map knew of a listener then: the
    /// diagnostic that replaced the gate.</summary>
    private void Drain(bool advised)
    {
        StopPoll();
        if (_queue.Count == 0 && _droppedOldest == 0)
        {
            return;
        }

        QueuedLine[] queued = [.. _queue];
        int dropped = _droppedOldest;
        _queue.Clear();
        _droppedOldest = 0;
        foreach (QueuedLine line in queued)
        {
            _raise(line.Kind, line.Processing, line.Text, line.ActivityId);
        }

        _launch.Diagnose(
            HostDiagnosticEvent.AnnouncementReplay,
            $"drained={queued.Length}, droppedOldest={dropped}, at={(int)_launch.Elapsed().TotalMilliseconds}ms, "
            + $"notificationListenerExists={advised}");
    }

    /// <summary>OD-7, codex round 20: each queued line expires
    /// <see cref="LaunchWindow"/> after its own post, in every phase, and is
    /// dropped unspoken — oldest first, never the whole queue at once — which
    /// is what ends the poll when readiness never comes.</summary>
    private void DropExpiredLines(TimeSpan now)
    {
        int dropped = 0;
        while (_queue.TryPeek(out QueuedLine oldest) && now - oldest.PostedAt >= LaunchWindow)
        {
            _ = _queue.Dequeue();
            dropped++;
        }

        if (_queue.Count == 0)
        {
            dropped += _droppedOldest;
            _droppedOldest = 0;
            StopPoll();
        }

        if (dropped > 0)
        {
            _launch.Diagnose(HostDiagnosticEvent.AnnouncementReplay, $"dropped={dropped}, unready too long");
        }
    }

    private void StopPoll()
    {
        _poll?.Dispose();
        _poll = null;
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
    /// listener (WPF's map, which only an advise fills — logged, never a
    /// gate), whether the status element has a connected provider to raise
    /// on, the readiness poll, the
    /// time since the first frame, and the diagnostics sink. Production reads
    /// the map and <see cref="NotificationSource"/>, polls on the UI thread,
    /// counts from the window's first frame and logs under
    /// SLATE_UIA_DIAGNOSTICS=1; the facts hold each one.
    /// </summary>
    internal sealed record LaunchSeams(
        Func<bool> Advised,
        Func<bool> Connected,
        Func<Action, IDisposable> StartPoll,
        Func<TimeSpan> Elapsed,
        Action<HostDiagnosticEvent, string> Diagnose)
    {
        internal static LaunchSeams ForProduction(FrameworkElement source) => new(
            () => AutomationPeer.ListenerExists(AutomationEvents.Notification),
            () => NotificationSource.Of(source) is not null,
            PollOnThisThread,
            SinceFirstFrame(source),
            HostLog.WriteUiAutomationDiagnostic);

        /// <summary>
        /// The time since the source's window first rendered — zero before
        /// its <c>ContentRendered</c> — so the launch window is measured from
        /// the first frame, not from construction (codex round 2): MainWindow
        /// builds its dispatcher well before <c>App</c> shows it. An element
        /// in no window, or in one already showing, counts from now.
        /// </summary>
        private static Func<TimeSpan> SinceFirstFrame(FrameworkElement source)
        {
            Stopwatch? sinceFirstFrame = null;
            Window? window = Window.GetWindow(source);
            if (window is null || window.IsVisible)
            {
                sinceFirstFrame = Stopwatch.StartNew();
            }
            else
            {
                EventHandler? firstFrame = null;
                firstFrame = (_, _) =>
                {
                    window.ContentRendered -= firstFrame;
                    sinceFirstFrame ??= Stopwatch.StartNew();
                };
                window.ContentRendered += firstFrame;
            }

            return () => sinceFirstFrame?.Elapsed ?? TimeSpan.Zero;
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

    /// <summary>OD-7's launch phase, bookkeeping only: Unadvised until
    /// readiness is first observed (then Done, the queue drained) or the
    /// launch window closes first (then Expired, logged once; its lines
    /// expire at their own deadlines like every other). Done and Expired are
    /// final.</summary>
    private enum LaunchPhase
    {
        Unadvised,
        Done,
        Expired,
    }

    /// <summary>One posted tuple, queued as rendered, so the drain raises
    /// exactly what core rendered; <see cref="PostedAt"/>, when it was
    /// queued (the first frame for a line posted before it), sets its own
    /// deadline.</summary>
    private readonly record struct QueuedLine(
        AutomationNotificationKind Kind,
        AutomationNotificationProcessing Processing,
        string Text,
        string ActivityId)
    {
        public TimeSpan PostedAt { get; init; }
    }

    /// <summary>
    /// R-1: the provider a notification is raised on — the source element's
    /// own, through its peer resolved as the dispatcher always has. It exists
    /// only once the window's automation root is connected — by any
    /// WM_GETOBJECT the window receives, or by WPF itself when its
    /// process-wide event map already has a listener as the window gets its
    /// root visual. At a launch's first frame with neither,
    /// <c>ProviderFromPeer</c> answers null
    /// (<c>AtTheFirstFrameTheStatusPeerHasNoProviderUntilItsWindowIsConnected</c>
    /// records it), and the queue keeps its lines until it answers (OD-7).
    /// The window's HWND host provider was measured as the alternative and
    /// delivered nothing to a desktop-scoped client, advised or not, so it is
    /// not used.
    /// </summary>
    internal static class NotificationSource
    {
        internal static IRawElementProviderSimple? Of(FrameworkElement source)
        {
            AutomationPeer peer = UIElementAutomationPeer.FromElement(source)
                ?? UIElementAutomationPeer.CreatePeerForElement(source)
                ?? new FrameworkElementAutomationPeer(source);
            return NotificationProviderPeer.Current.ProviderOf(peer);
        }

        /// <summary>The diagnostics line (SLATE_UIA_DIAGNOSTICS=1) for the
        /// status provider's state as a check found it, or null
        /// when it matches the one <paramref name="last"/> holds — once per
        /// change, never once per check.</summary>
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
