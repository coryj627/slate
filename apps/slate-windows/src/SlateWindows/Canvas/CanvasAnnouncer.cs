// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

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

    private void Emit(A11yEvent @event, EventClass? eventClass)
    {
        RenderedAnnouncement rendered = SlateUniffiMethods.A11yRender(@event);
        if (rendered.Text.Length == 0)
        {
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
            line = new PendingLine(_window, () => Fire(eventClass));
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

        internal PendingLine(TimeSpan window, Action onElapsed)
        {
            _timer = new DispatcherTimer { Interval = window };
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
