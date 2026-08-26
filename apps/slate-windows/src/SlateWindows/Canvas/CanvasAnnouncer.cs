// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 PR A (#745), contract A5 (R-C/R-G): the ONE announcement funnel
/// for every canvas surface — the mac <c>CanvasAnnouncer</c> twin.
///
/// The GRAMMAR is core's. Callers hand over a typed
/// <c>CanvasA11yEvent</c> from the PR 0a vocabulary; this class wraps
/// it in <c>A11yEvent.Canvas</c>, renders it through the FFI, and posts
/// the rendered pair — so the spoken text AND the priority both come
/// from the vocabulary, never composed here and never re-classified
/// here. What stays host-side is exactly what a pure render cannot own:
/// the ~200 ms same-class coalescing window (t0 §1.5 — a render has no
/// clock).
///
/// No canvas code posts on its own: <c>CanvasAnnouncerCensus</c> parses
/// every file under <c>Canvas/</c> and fails on any dispatcher
/// reference, any <c>A11yRender</c> call and any <c>HostComposed</c>
/// construction. This file is the single exempt one.
/// </summary>
internal sealed class CanvasAnnouncer
{
    /// <summary>
    /// The coalescing classes (t0 §1.5). MEMBERSHIP is pinned core-side
    /// in one doc comment on the canvas family so both hosts copy one
    /// list (contract 0a-8); only the timing is ours.
    /// </summary>
    private enum EventClass
    {
        Navigation,
        Filter,
    }

    /// <summary>The mac cadence: 200 ms latest-wins, per class.</summary>
    internal static readonly TimeSpan DefaultWindow = TimeSpan.FromMilliseconds(200);

    private readonly Action<RenderedAnnouncement> _post;
    private readonly Dictionary<EventClass, PendingLine> _pending = [];
    private readonly TimeSpan _window;

    /// <summary>
    /// The dispatcher the coalescing timers run on, captured at
    /// CONSTRUCTION rather than at first use.
    /// </summary>
    /// <remarks>
    /// A <see cref="DispatcherTimer"/> binds to
    /// <c>Dispatcher.CurrentDispatcher</c> of whatever thread creates
    /// it, and these are created lazily on first debounce. The
    /// announcer is built on the UI thread by the canvas registry, but
    /// a first navigation announcement arriving from a scheduler body
    /// would otherwise have created a timer on a POOL thread — whose
    /// dispatcher nothing ever pumps, so the queued line would never
    /// fire and the move would simply never be spoken. Binding the
    /// timers to this dispatcher makes the hazard unreachable; the
    /// assert below makes a caller that is off it loud in Debug rather
    /// than merely survivable.
    /// </remarks>
    private readonly Dispatcher _dispatcher;
    private bool _isShutDown;

    /// <summary>
    /// The seam is a RENDERED line rather than an event because the
    /// window's winner is decided after the render and the loser is
    /// dropped without ever being spoken — the mac announcer's
    /// <c>(text, priority)</c> closure, for the same reason. The
    /// default posts through the canonical dispatcher.
    /// </summary>
    public CanvasAnnouncer(
        Action<RenderedAnnouncement> post,
        TimeSpan? coalesceWindow = null)
    {
        ArgumentNullException.ThrowIfNull(post);
        _post = post;
        _window = coalesceWindow ?? DefaultWindow;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    /// <summary>The only announcement API canvas code may use.</summary>
    public void Announce(CanvasA11yEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        Emit(new A11yEvent.Canvas(@event), CoalescingClassOf(@event));
    }

    /// <summary>
    /// Relay a core event that is NOT canvas vocabulary — the shared
    /// <c>AccessibleDataGrid</c> raises its own sort/filter events
    /// (PR B swaps its <c>Announce</c> seam onto this funnel), and the
    /// canvas must pass their rendered text AND priority through rather
    /// than re-wrapping them at a priority of its own choosing.
    /// </summary>
    public void Relay(A11yEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        Emit(@event, null);
    }

    /// <summary>
    /// Render a LABEL-class event (0a-13): the empty-state onboarding
    /// copy and the degraded banner are composed by core from core's
    /// data, and the surface shows the SAME render the announcement
    /// speaks so the two cannot drift (the mac CD-3 precedent). Not an
    /// announcement — nothing is posted.
    /// </summary>
    public static string RenderLabel(CanvasA11yEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(@event)).Text;
    }

    /// <summary>
    /// Retirement: cancel every pending line and refuse anything later
    /// (contract A5).
    /// </summary>
    /// <remarks>
    /// A coalesced line is a timer holding a rendered string. Without
    /// this, closing the last tab on a canvas the user had just moved
    /// around in left a navigation line queued on a document that no
    /// longer exists, and it fired ~200 ms later — the shell speaking
    /// about a surface that is gone. The pending lines are DROPPED, not
    /// flushed: the reason to say them (the user is reading that canvas)
    /// stopped being true.
    /// </remarks>
    internal void Shutdown()
    {
        _isShutDown = true;
        DropAllPending();
    }

    /// <summary>
    /// How many posts this announcer has REFUSED since retirement.
    /// </summary>
    /// <remarks>
    /// The refusal itself is a `Debug.Fail`, which means the guard only
    /// speaks in a developer's build — and CI runs Release, so a shipped
    /// path posting after retirement was invisible to the gate for six
    /// rounds. `Commit`'s confirmation was doing exactly that. A counter
    /// makes "retirement composes nothing" assertable in BOTH
    /// configurations, which is what a guard that matters needs.
    /// </remarks>
    internal int RefusedAfterShutdownForTests { get; private set; }

    private void Emit(A11yEvent @event, EventClass? eventClass)
    {
        if (_isShutDown)
        {
            // Not merely dropped: a post after retirement means some
            // path outlived the document that owns it, and silence would
            // hide that.
            RefusedAfterShutdownForTests++;
            Debug.Fail(
                "CanvasAnnouncer posted after Shutdown: the document it belongs "
                + "to has been retired, so nothing it says can be about a "
                + "surface the user can see.");
            return;
        }
        Debug.Assert(
            _dispatcher.CheckAccess(),
            "CanvasAnnouncer must be driven from the thread it was built on: its "
            + "coalescing timers are bound to that dispatcher, and the publishes "
            + "it feeds are UI state.");
        RenderedAnnouncement rendered = SlateUniffiMethods.A11yRender(@event);
        if (rendered.Text.Length == 0)
        {
            // No canvas template renders empty, so this arm is
            // defensive (mac's `guard !rendered.text.isEmpty` twin) —
            // and a silent drop is the worst possible way to learn that
            // a template started rendering nothing, because the symptom
            // is an announcement that simply does not happen.
            Debug.Fail(
                $"the vocabulary rendered an empty string for {@event.GetType().Name}; "
                + "an announcement was dropped silently.");
            return;
        }
        if (eventClass is { } coalesced)
        {
            Debounce(coalesced, rendered);
            return;
        }
        // t0 §1.5: an assertive event supersedes queued navigation
        // context — re-derivable by moving again — so both pending
        // classes are cancelled and DROPPED, never posted.
        if (rendered.Priority == A11yPriority.High)
        {
            DropAllPending();
        }
        _post(rendered);
    }

    /// <summary>
    /// The coalescing class of a canvas event, per the list pinned on
    /// the canvas family core-side (contract 0a-8): <c>navigation</c>
    /// for movement and transient geometry, <c>filter</c> for the two
    /// filter events, immediate for everything else.
    /// </summary>
    private static EventClass? CoalescingClassOf(CanvasA11yEvent @event) => @event switch
    {
        CanvasA11yEvent.CanvasMovedTo
            or CanvasA11yEvent.CanvasGroupEntered
            or CanvasA11yEvent.CanvasGroupLeft
            or CanvasA11yEvent.CanvasConnectionTraversed
            or CanvasA11yEvent.CanvasMoveRelative
            or CanvasA11yEvent.CanvasResizeGeometry => EventClass.Navigation,
        CanvasA11yEvent.CanvasFilterCount
            or CanvasA11yEvent.CanvasFilterCleared => EventClass.Filter,
        _ => null,
    };

    private void Debounce(EventClass eventClass, RenderedAnnouncement rendered)
    {
        if (!_pending.TryGetValue(eventClass, out PendingLine? line))
        {
            line = new PendingLine(_window, _dispatcher, () => Fire(eventClass));
            _pending[eventClass] = line;
        }
        line.Restart(rendered);
    }

    private void Fire(EventClass eventClass)
    {
        if (!_pending.TryGetValue(eventClass, out PendingLine? line))
        {
            return;
        }
        RenderedAnnouncement? rendered = line.Take();
        if (rendered is not null)
        {
            _post(rendered);
        }
    }

    /// <summary>Errors must not be preceded by a stale queued
    /// navigation line (t0 §1.5).</summary>
    private void DropAllPending()
    {
        foreach (PendingLine line in _pending.Values)
        {
            _ = line.Take();
        }
    }

    /// <summary>Test hook: emit every pending debounced line NOW, so a
    /// fact is deterministic without a wall-clock wait (the mac
    /// <c>flushForTests</c> twin).</summary>
    internal void FlushForTests()
    {
        foreach (EventClass eventClass in _pending.Keys.ToList())
        {
            Fire(eventClass);
        }
    }

    /// <summary>One class's queued line and its timer. The timer is
    /// stopped whenever the line is taken, so a fired-and-drained class
    /// cannot re-post the same text.</summary>
    private sealed class PendingLine
    {
        private readonly DispatcherTimer _timer;
        private RenderedAnnouncement? _rendered;

        internal PendingLine(TimeSpan window, Dispatcher dispatcher, Action onElapsed)
        {
            // The four-argument ctor binds the timer to the GIVEN
            // dispatcher; the parameterless one would bind it to
            // whatever thread got here first.
            _timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
            {
                Interval = window,
            };
            _timer.Tick += (_, _) =>
            {
                _timer.Stop();
                onElapsed();
            };
        }

        internal void Restart(RenderedAnnouncement rendered)
        {
            _rendered = rendered;
            _timer.Stop();
            _timer.Start();
        }

        internal RenderedAnnouncement? Take()
        {
            _timer.Stop();
            RenderedAnnouncement? taken = _rendered;
            _rendered = null;
            return taken;
        }
    }
}
