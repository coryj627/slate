// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR A (#746), contract A-10: the ONE announcement funnel for every
/// graph surface — the mac <c>GraphAnnouncer</c> twin and the sibling of
/// <c>CanvasAnnouncer</c>. The GRAMMAR is core's: callers hand over a typed
/// <c>GraphA11yEvent</c> from the PR 0a vocabulary; this class wraps it in
/// <c>A11yEvent.Graph</c>, renders it through the FFI, and posts the
/// rendered pair. What stays host-side is the 200 ms same-class
/// coalescing window and the High flush-and-drop (a11y.rs's pinned
/// list). No graph code posts on its own: the directory censuses parse
/// every file under <c>Graph/</c> and this is the one exempt file, by
/// its normalised path.
/// </summary>
internal sealed class GraphAnnouncer
{
    /// <summary>The coalescing classes core pins on the graph family
    /// (a11y.rs, "Coalescing class keys": navigation, filter,
    /// forceValue, settle). Membership is asserted from the Rust side by
    /// <c>the_windows_graph_coalescing_switch_matches_the_pinned_class_list</c>.</summary>
    private enum EventClass
    {
        Navigation,
        Filter,
        ForceValue,
        Settle,
    }

    /// <summary>The mac cadence: 200 ms latest-wins, per class.</summary>
    internal static readonly TimeSpan DefaultWindow = TimeSpan.FromMilliseconds(200);

    private readonly Action<RenderedAnnouncement> _post;
    private readonly Dictionary<EventClass, PendingLine> _pending = [];
    private readonly TimeSpan _window;
    private readonly Dispatcher _dispatcher;
    private bool _isShutDown;

    public GraphAnnouncer(Action<RenderedAnnouncement> post, TimeSpan? coalesceWindow = null)
    {
        ArgumentNullException.ThrowIfNull(post);
        _post = post;
        _window = coalesceWindow ?? DefaultWindow;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    /// <summary>The only announcement API graph code may use.</summary>
    public void Announce(GraphA11yEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        Emit(new A11yEvent.Graph(@event), CoalescingClassOf(@event), gate: null);
    }

    /// <summary>The filter count's GATED entry (0a-9's fire-time gate; A-10
    /// as amended, W6-2 PR B BD-12): the gate is stored with the pending
    /// line and re-checked when the window elapses — the mac's
    /// <c>graphTabActive</c> at fire (`GraphAnnouncer.swift:150`,
    /// `:194–207`) — so a count queued while the graph tab was effective
    /// is DROPPED if the tab left effective before it spoke. The census
    /// asserts the graph document enqueues its count through this entry
    /// and through no other.</summary>
    public void AnnounceGatedFilterCount(GraphA11yEvent.GraphFilterCount @event, Func<bool> gate)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(gate);
        Emit(new A11yEvent.Graph(@event), EventClass.Filter, gate);
    }

    /// <summary>Relay a core event that is NOT graph vocabulary — the
    /// shared <c>AccessibleDataGrid</c>'s sort, row-move and cell-move
    /// events — uncoalesced, with core's own priority.</summary>
    public void Relay(A11yEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        Emit(@event, null, gate: null);
    }

    /// <summary>Render a graph event WITHOUT posting (contract A-6): the
    /// row's UIA Name and its row-move description are the same P1 copy
    /// the announcer would speak, obtained here so the relay stays the
    /// one file that renders.</summary>
    public static string RenderLabel(GraphA11yEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return SlateUniffiMethods.A11yRender(new A11yEvent.Graph(@event)).Text;
    }

    /// <summary>Retirement (contract A-1): every pending class dropped,
    /// nothing later accepted — the mac's <c>cancelPending</c>.</summary>
    internal void Shutdown()
    {
        _isShutDown = true;
        DropAllPending();
    }

    internal int RefusedAfterShutdownForTests { get; private set; }

    internal bool IsRetired => _isShutDown;

    private void Emit(A11yEvent @event, EventClass? eventClass, Func<bool>? gate)
    {
        if (_isShutDown)
        {
            RefusedAfterShutdownForTests++;
            Debug.Fail(
                "GraphAnnouncer posted after Shutdown: the workspace it belongs to has "
                + "been torn down, so nothing it says can be about a surface the user can see.");
            return;
        }
        if (!_dispatcher.CheckAccess())
        {
            _ = _dispatcher.BeginInvoke(() => Emit(@event, eventClass, gate));
            return;
        }
        RenderedAnnouncement rendered = SlateUniffiMethods.A11yRender(@event);
        if (rendered.Text.Length == 0)
        {
            Debug.Fail(
                $"the vocabulary rendered an empty string for {@event.GetType().Name}; "
                + "an announcement was dropped silently.");
            return;
        }
        if (eventClass is { } coalesced)
        {
            Debounce(coalesced, rendered, gate);
            return;
        }
        // a11y.rs: a High graph event, announced or relayed, flushes and
        // DROPS all four pending classes — never posts them.
        if (rendered.Priority == A11yPriority.High)
        {
            DropAllPending();
        }
        _post(rendered);
    }

    /// <summary>The coalescing class of a graph event, per the list
    /// pinned on the graph family core-side (0a-9): navigation for the
    /// row focus, filter for the count, forceValue for a force control,
    /// settle for the layout's settle; immediate for everything else.</summary>
    private static EventClass? CoalescingClassOf(GraphA11yEvent @event) => @event switch
    {
        GraphA11yEvent.GraphRow => EventClass.Navigation,
        GraphA11yEvent.GraphFilterCount => EventClass.Filter,
        GraphA11yEvent.GraphForceValue => EventClass.ForceValue,
        GraphA11yEvent.GraphLayoutSettled => EventClass.Settle,
        _ => null,
    };

    private void Debounce(EventClass eventClass, RenderedAnnouncement rendered, Func<bool>? gate)
    {
        if (!_pending.TryGetValue(eventClass, out PendingLine? line))
        {
            line = new PendingLine(_window, _dispatcher, () => Fire(eventClass));
            _pending[eventClass] = line;
        }
        line.Restart(rendered, gate);
    }

    private void Fire(EventClass eventClass)
    {
        if (!_pending.TryGetValue(eventClass, out PendingLine? line))
        {
            return;
        }
        (RenderedAnnouncement? rendered, Func<bool>? gate) = line.Take();
        if (rendered is null)
        {
            return;
        }
        // 0a-9's fire-time gate (A-10 as amended): a stored gate that has
        // gone false since the line was queued DROPS it.
        if (gate is not null && !gate())
        {
            DroppedAtFireForTests++;
            return;
        }
        _post(rendered);
    }

    /// <summary>How many gated lines the fire-time gate dropped.</summary>
    internal int DroppedAtFireForTests { get; private set; }

    /// <summary>Drop every pending class without posting — the mac's
    /// <c>cancelPending</c> on view departure (0a-9); a document's
    /// retirement calls it on the shared relay (A-1 as amended).</summary>
    internal void DropAllPending()
    {
        foreach (PendingLine line in _pending.Values)
        {
            _ = line.Take();
        }
    }

    /// <summary>Test hook: emit every pending debounced line NOW (the
    /// mac <c>flushForTests</c> twin).</summary>
    internal void FlushForTests()
    {
        foreach (EventClass eventClass in _pending.Keys.ToList())
        {
            Fire(eventClass);
        }
    }

    /// <summary>How many classes hold a pending line — the retirement
    /// fact's witness.</summary>
    internal int PendingForTests => _pending.Values.Count(line => line.HasLine);

    /// <summary>One class's queued line and its timer; the timer stops
    /// whenever the line is taken.</summary>
    private sealed class PendingLine
    {
        private readonly DispatcherTimer _timer;
        private RenderedAnnouncement? _rendered;
        private Func<bool>? _gate;

        internal PendingLine(TimeSpan window, Dispatcher dispatcher, Action onElapsed)
        {
            _timer = new DispatcherTimer(window, DispatcherPriority.Normal, (_, _) => onElapsed(), dispatcher);
            _timer.Stop();
        }

        internal bool HasLine => _rendered is not null;

        /// <summary>Latest wins — the line AND its gate (0a-9: the gate is
        /// stored with the pending line, re-checked at fire).</summary>
        internal void Restart(RenderedAnnouncement rendered, Func<bool>? gate)
        {
            _rendered = rendered;
            _gate = gate;
            _timer.Stop();
            _timer.Start();
        }

        internal (RenderedAnnouncement? Rendered, Func<bool>? Gate) Take()
        {
            _timer.Stop();
            RenderedAnnouncement? rendered = _rendered;
            Func<bool>? gate = _gate;
            _rendered = null;
            _gate = null;
            return (rendered, gate);
        }
    }
}
