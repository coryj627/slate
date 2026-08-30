// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// What the navigator needs from whichever projection the reader is
/// actually looking at (contract C2).
/// </summary>
/// <remarks>
/// The navigator is per-DOCUMENT (two panes on one canvas share one
/// command layer, exactly as they share one selection), but focus lives
/// on a VIEW. This is the seam between the two, and it is deliberately
/// narrow: three questions, three focus moves and one IDENTITY —
/// `Owner`, the tab this pane shows, which is what an addressed request
/// is addressed TO and what a mode records as its owner. Nothing is on
/// it that the navigator does not call. Everything else the navigator
/// does is model state and announcements, which is what makes the verb
/// facts drivable without a window.
/// </remarks>
internal interface ICanvasSurfacePresenter
{
    /// <summary>Which projection is showing.</summary>
    CanvasSurfaceKind Projection { get; }

    /// <summary>Whether the showing projection owns keyboard focus —
    /// program rule R2: arrows and typing keys act only there.</summary>
    bool ProjectionHasFocus { get; }

    /// <summary>
    /// Whether the projection can move the reader one row in this
    /// direction ITSELF.
    /// </summary>
    /// <remarks>
    /// The tree's own Up/Down and the grid's own Up/Down already move the
    /// reader AND land on the document's one narrating selection
    /// mutation, so the navigator must not move a second time. What the
    /// projections cannot do is answer at the boundary: a tree that will
    /// not move says nothing, and t0's never-silent rule says a keypress
    /// that does nothing must say so. This is that question.
    /// </remarks>
    bool CanMoveWithinProjection(bool forward);

    /// <summary>Put keyboard focus on a row, silently (contract C12).
    /// False when the row could not take it — gone, filtered out, or
    /// unrealizable — so a caller with nowhere else to put the reader
    /// knows to fall back rather than leaving focus on the window
    /// root.</summary>
    bool FocusRow(string nodeId);

    /// <summary>
    /// Put keyboard focus back where this state can hold it, reporting
    /// whether anything took it (contract C6).
    /// </summary>
    /// <remarks>
    /// "The projection" is not always on screen: `Render` collapses both
    /// under `Loading` and under every failure state, so a restoration
    /// that focused the projection unconditionally and ignored the
    /// result left the reader on the window root — and the Escape that
    /// was supposed to get them out of the filter field had already been
    /// consumed. State-aware: the projection when it renders rows, else
    /// the region this state actually shows, else a durable A14 landing
    /// so the reader is seated when something can hold them.
    /// </remarks>
    bool FocusProjection();

    /// <summary>
    /// The VIEW this surface is for — its tab, the same key contract
    /// A14's focus request is addressed by.
    /// </summary>
    /// <remarks>
    /// Two panes share one document, so a request that named no owner
    /// reached both: the pane that could not satisfy it kept it pending
    /// and pulled the reader into ITS filter field the next time it saw
    /// the keys. A14 solved that by addressing the request; this is the
    /// same seam for the same reason (contract C10/A14).
    /// </remarks>
    object? Owner { get; }

    // Deliberately NOT here: "focus the filter field". Ctrl+F raises the
    // document's ADDRESSED filter-focus REQUEST instead, because the
    // field belongs to every pane showing the canvas and only the one
    // the reader is in should take it — a presenter call would have picked whichever pane the
    // navigator happened to be holding (the mac Codoki #626 rule,
    // restated for panes).

    /// <summary>
    /// Escape's third rung: dismiss a transient region inside the canvas
    /// (the Where-am-I panel, the interim card detail) or leave the filter
    /// field, re-seating the reader through C6's seat rule. False when
    /// there was nothing to leave, which is what lets the press fall through to the
    /// workspace.
    /// </summary>
    bool DismissTransientRegion();
}

/// <summary>
/// W6-1 PR C (#745): the canvas command layer — **not a fourth view**
/// (the t2/t3 shared-architecture decision). Every projection hosts it;
/// every movement operates on the shared <see cref="CanvasSelection"/>
/// and announces through the one funnel. The mac
/// <c>AppState+CanvasNavigation.swift</c> twin, consuming the same core
/// queries.
///
/// Two shapes per verb, deliberately (rule R1: a chord is a convenience,
/// never the only path):
/// <list type="bullet">
/// <item><description>the VERB (<see cref="NextCard"/>,
/// <see cref="EnterGroup"/>, …) — the palette row, always reachable, and
/// what the unit facts drive;</description></item>
/// <item><description>the CHORD, delivered from the canvas surface's
/// tunnelling key handler at <c>ChordScope.Canvas</c>
/// (<see cref="HandleKey"/>), which is where rule R2's "only while a
/// canvas projection has keyboard focus" is enforced.</description></item>
/// </list>
///
/// **Never silent** (contract C4): every verb answers in every load
/// state. A verb asks its OWN precondition first where the reader can see
/// rows to have a caret in, then the document's one state → response
/// mapping, and a query that THREW is never reported as a query that
/// came back empty.
/// </summary>
internal sealed class CanvasNavigator
{
    private readonly CanvasDocumentViewModel _document;
    private readonly Dictionary<(Key Key, ModifierKeys Modifiers), Func<bool>> _chords = [];
    private ICanvasSurfacePresenter? _presenter;

    internal CanvasNavigator(CanvasDocumentViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
        Bind();
        // The two ladder rungs between the mode stack (rung 1) and the
        // workspace (rung 4) are registered ONCE, here, rather than by
        // each surface: two panes on one canvas would otherwise claim the
        // same rung twice, and "exactly one rung per press" would depend
        // on which pane mounted first.
        document.Modes.RegisterRung(CanvasEscapeRung.Filter, ClearFilterRung);
        document.Modes.RegisterRung(CanvasEscapeRung.Surface, SurfaceRung);
    }

    /// <summary>
    /// The Canvas-scoped chord map. Every entry is a
    /// <c>ChordScope.Canvas</c> row in the chord table, and
    /// <c>ChordTableTests</c> scrapes THIS registration in both
    /// directions — a chord handled here with no row, or a row claiming
    /// this scope that nothing here delivers, fails.
    /// </summary>
    private void Bind()
    {
        AddChord(Key.Down, ModifierKeys.None, () => ArrowMove(forward: true));
        AddChord(Key.Up, ModifierKeys.None, () => ArrowMove(forward: false));
        AddChord(Key.Right, ModifierKeys.None, () => ArrowFollow(forward: true));
        AddChord(Key.Left, ModifierKeys.None, () => ArrowFollow(forward: false));
        AddChord(Key.Enter, ModifierKeys.None, CommitModeFromKey);
        AddChord(Key.Escape, ModifierKeys.None, EscapeFromKey);
        AddChord(Key.F, ModifierKeys.Control, FilterCardsFromKey);
        AddChord(
            Key.I,
            ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift,
            WhereAmIFromKey);
    }

    private void AddChord(Key key, ModifierKeys modifiers, Func<bool> action) =>
        _chords[(key, modifiers)] = action;

    /// <summary>
    /// The surface the reader's keys are coming from. Kept after the
    /// press rather than cleared, so a palette-invoked verb still knows
    /// which pane to move focus in.
    /// </summary>
    internal void AttachPresenter(ICanvasSurfacePresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        _presenter = presenter;
    }

    /// <summary>The pane the reader is working in, as this navigator
    /// understands it — the surface that owns the keys, or owned them
    /// last, or the one that last entered a mode. Null before any of
    /// those has happened, AND after the holder detaches.</summary>
    /// <remarks>
    /// A CACHE, not a log. It is cleared by any pane's detachment, so it
    /// cannot answer "has a pane ever held the keys" — a question that
    /// was asked of it once and got a wrong answer that refused a live
    /// surviving pane a mode. A mode's owner comes from the INVOCATION
    /// (contract C8); this field is what the verbs act through, and the
    /// two agree because an admitted entry attaches its invoker.
    /// </remarks>
    internal ICanvasSurfacePresenter? AttachedPresenter => _presenter;

    /// <summary>
    /// Enter a mode ON BEHALF OF the pane that invoked it (contract C8).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one production route into <see cref="CanvasModeController.Enter"/>,
    /// and the pane comes from the INVOCATION rather than from this
    /// navigator's cache. A chord carries its presenter already
    /// (<see cref="HandleKey"/>); a palette or menu row knows which pane
    /// it is serving, because the shell resolved a canvas tab to put the
    /// row in front of the reader at all. Naming it here is free, and it
    /// is the only source that is true at the moment of the call.
    /// </para>
    /// <para>
    /// It used to read <see cref="AttachedPresenter"/> and refuse when
    /// that was null. `_presenter` is a detachable CACHE, not a record of
    /// historical focus: a pane that unloads or is retargeted clears it
    /// for the whole document, so a surviving second pane invoking a mode
    /// before it regained the keys was refused — on a canvas that HAS
    /// been focused, from a pane that is live. The refusal was answering
    /// "has any pane held the keys lately", which is not the question,
    /// and no predicate over a cache could have been.
    /// </para>
    /// <para>
    /// The invoker is by definition the pane the reader is in, so it is
    /// ATTACHED here too — the same affinity a focus edge or a chord
    /// would have established, established by the fact of the call. PR
    /// E/F's real modes enter through this.
    /// </para>
    /// <para>
    /// ADMISSION FIRST, and that ordering is the whole of it. The attach
    /// ran unconditionally for one wave, so a REFUSED entry still moved
    /// affinity: a second pane asking for a mode while the first pane's
    /// was running left the mode owned by A and every movement verb
    /// acting through B, and an entry into a retired controller left it
    /// holding a presenter the terminal object had just rejected. Both
    /// are the reader and the selection disagreeing (CD-40), reached by
    /// asking for something and being told no.
    /// </para>
    /// <para>
    /// It stays BEFORE the publication, though. Anything reacting to the
    /// mode becoming active — the M6 controls, M3's inspectable state —
    /// must already see the pane it belongs to, so the attach sits inside
    /// the admitted window rather than after the return. `AdmitsEntry` is
    /// the controller's own condition rather than a second copy of it,
    /// because two spellings of "would this be admitted" is how the
    /// answer and the effect drift apart. Re-attaching the same presenter
    /// is an idempotent assignment, so the admitted path costs nothing.
    /// </para>
    /// </remarks>
    internal bool EnterMode(CanvasModeSpec spec, ICanvasSurfacePresenter pane)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(pane);
        if (_document.Modes.AdmitsEntry)
        {
            AttachPresenter(pane);
        }
        return _document.Modes.Enter(spec, pane);
    }

    /// <summary>
    /// Detach, reporting whether this presenter WAS the attached one.
    /// </summary>
    /// <remarks>
    /// The answer is what carries presenter affinity across a document
    /// REPLACEMENT (an external rename retargets the tab, contract A1 /
    /// CD-32): the surface detaches from the old navigator and has to
    /// tell the new one that it is the pane the reader is working in.
    /// Attachment otherwise needs a false→true keyboard-focus edge or a
    /// canvas chord, and a replacement under a persistently-focused
    /// filter field produces neither.
    /// </remarks>
    internal bool DetachPresenter(ICanvasSurfacePresenter presenter)
    {
        if (!ReferenceEquals(_presenter, presenter))
        {
            return false;
        }
        _presenter = null;
        return true;
    }

    /// <summary>
    /// Deliver one key press from the canvas surface's tunnelling
    /// handler. Returns whether the press was consumed; an unconsumed
    /// press keeps its ordinary meaning for whatever has focus, which is
    /// what lets the grid keep its cell navigation, a focused button keep
    /// its Enter, and the tree keep the numpad <c>+</c>/<c>-</c> the
    /// arrows no longer stand in for (CD-48).
    /// </summary>
    internal bool HandleKey(Key key, ModifierKeys modifiers, ICanvasSurfacePresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        AttachPresenter(presenter);
        return _chords.TryGetValue((key, modifiers), out Func<bool>? action) && action();
    }

    // --- Movement --------------------------------------------------------

    /// <summary>Select the next card in reading order over the FILTERED
    /// set (#373: movement walks what the surfaces display).</summary>
    public void NextCard() => SelectAdjacent(1);

    public void PreviousCard() => SelectAdjacent(-1);

    private void SelectAdjacent(int offset)
    {
        if (!_document.AdmitStructuralRead())
        {
            return;
        }
        IReadOnlyList<CanvasOutlineRow> rows = _document.FilteredOutline;
        if (rows.Count == 0)
        {
            AnnounceNothingToMoveThrough();
            return;
        }
        int current = -1;
        if (_document.Selection.Selected is { } selected)
        {
            for (int index = 0; index < rows.Count; index++)
            {
                if (string.Equals(rows[index].NodeId, selected, StringComparison.Ordinal))
                {
                    current = index;
                    break;
                }
            }
        }
        int target;
        if (current >= 0)
        {
            target = Math.Clamp(current + offset, 0, rows.Count - 1);
            if (target == current)
            {
                Announce(new CanvasA11yEvent.CanvasStatus(
                    offset > 0
                        ? new CanvasStatusNote.EndOfCanvas()
                        : new CanvasStatusNote.StartOfCanvas()));
                return;
            }
        }
        else
        {
            // No caret yet: forward starts at the top, backward at the
            // bottom — the same wrap-free seat mac makes.
            target = offset > 0 ? 0 : rows.Count - 1;
        }
        MoveTo(rows[target].NodeId);
    }

    /// <summary>
    /// Enter the selected group (select its first child in core's reading
    /// order — never a depth walk, §W-G row E / 0b-8).
    /// </summary>
    public void EnterGroup()
    {
        if (_document.AnsweredMissingSelection() || !_document.AdmitStructuralRead())
        {
            return;
        }
        if (_document.Selection.Selected is not { } selected
            || _document.RowFor(selected) is not { } row)
        {
            _document.AnnounceSelectionUnresolvable();
            return;
        }
        if (!string.Equals(row.Kind, "group", StringComparison.Ordinal))
        {
            Announce(new CanvasA11yEvent.CanvasStatus(new CanvasStatusNote.NotAGroup()));
            return;
        }
        // A THROW is not an empty group. Only a successful query that came
        // back empty may claim the group is empty (the VA-1 throw table).
        if (!_document.TryChildrenOf(selected, out IReadOnlyList<string> children))
        {
            _document.AnnounceSelectionUnresolvable();
            return;
        }
        if (children.Count == 0)
        {
            Announce(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.GroupIsEmpty(row.Title)));
            return;
        }
        MoveTo(children[0]);
    }

    /// <summary>Exit to the containing group (core's
    /// <c>canvas_parent_of</c>; no parent IS "at canvas level").</summary>
    public void ExitGroup()
    {
        if (_document.AnsweredMissingSelection() || !_document.AdmitStructuralRead())
        {
            return;
        }
        if (_document.Selection.Selected is not { } selected)
        {
            _document.AnnounceSelectionUnresolvable();
            return;
        }
        // A THROW is not "at canvas level" — a card the canvas cannot
        // resolve has no level. The query's ANSWER and its FAILURE are
        // separate returns for exactly that reason.
        if (!_document.TryParentOf(selected, out string? parent))
        {
            _document.AnnounceSelectionUnresolvable();
            return;
        }
        if (parent is null)
        {
            Announce(new CanvasA11yEvent.CanvasStatus(new CanvasStatusNote.AtCanvasLevel()));
            return;
        }
        MoveTo(parent);
    }

    /// <summary>
    /// Follow the selected card's Nth connection (1-based) in the given
    /// direction sense: forward = connections leaving or linking this
    /// card, back = connections arriving. Direction is core's, from the
    /// JSON Canvas <c>fromEnd</c>/<c>toEnd</c> (t0 §1.2).
    /// </summary>
    /// <remarks>
    /// <c>No outgoing connection.</c> is a claim about the ADJACENCY
    /// LIST, so it is spoken only when there is one. The order is VA-1's,
    /// and it is data before state: (1) the selection precondition; (2)
    /// the adjacency answer — a non-null list answers normally, traversal
    /// or accurate dead end, whatever the load state; (3) the state
    /// mapping's refusal, and only when (2) came back with nothing.
    /// </remarks>
    public void FollowConnection(bool forward, int ordinal = 1)
    {
        if (_document.AnsweredMissingSelection())
        {
            return;
        }
        IReadOnlyList<CanvasNeighbor>? known =
            _document.Selection.Selected is { } selected
                ? _document.NeighborsIfKnown(selected)
                : null;
        if (known is null)
        {
            // Nothing cached, nothing live. WHICH sentence that owes is
            // the mapping's call; when the mapping ADMITS the document
            // the handle is live, so the lookup was refused for the id —
            // the selection's problem, not the state's.
            if (_document.AdmitStructuralRead())
            {
                _document.AnnounceSelectionUnresolvable();
            }
            return;
        }
        CanvasNeighbor[] candidates = known
            .Where(neighbor => neighbor.Direction switch
            {
                CanvasEdgeDirection.Outgoing => forward,
                CanvasEdgeDirection.Incoming => !forward,
                _ => true,
            })
            .ToArray();
        if (ordinal < 1 || ordinal > candidates.Length)
        {
            Announce(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.NoConnection(
                    forward,
                    candidates.Length == 0 ? null : (uint)ordinal)));
            return;
        }
        CanvasNeighbor target = candidates[ordinal - 1];
        // Narrate the destination's REAL kind (Codoki #613: a group or
        // file target must not be introduced as a text card).
        string otherKind = _document.RowFor(target.OtherNode)?.Kind ?? "text";
        Announce(new CanvasA11yEvent.CanvasConnectionTraversed(
            target.Direction, otherKind, target.OtherTitle, target.Label));
        _document.SelectNode(target.OtherNode, announce: false);
        _presenter?.FocusRow(target.OtherNode);
    }

    /// <summary>
    /// Trace the outgoing chain from the selected card, ending with the
    /// visited count (core's cycle-safe walk, 0b-9). The event carries
    /// the TITLES only; core speaks their count as the sentence's tail,
    /// so the list and the number it claims can never disagree (CD-13).
    /// </summary>
    public void TracePath()
    {
        if (_document.AnsweredMissingSelection() || !_document.AdmitStructuralRead())
        {
            return;
        }
        if (_document.Selection.Selected is not { } start)
        {
            _document.AnnounceSelectionUnresolvable();
            return;
        }
        // A THROW is not a dead end: `No outgoing path from "X".` is
        // spoken only when the walk actually came back with no hops.
        if (!_document.TryTracePath(start, out IReadOnlyList<CanvasTraceHop> hops))
        {
            _document.AnnounceSelectionUnresolvable();
            return;
        }
        string? startTitle = _document.RowFor(start)?.Title;
        if (hops.Count == 0)
        {
            Announce(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.NoOutgoingPath(startTitle ?? string.Empty)));
            return;
        }
        _document.SelectNode(hops[^1].NodeId, announce: false);
        _presenter?.FocusRow(hops[^1].NodeId);
        string[] titles = startTitle is null
            ? hops.Select(hop => hop.Title).ToArray()
            : [startTitle, .. hops.Select(hop => hop.Title)];
        Announce(new CanvasA11yEvent.CanvasTracePathEnd(titles));
    }

    // --- Where am I (t0 §1.4) --------------------------------------------

    /// <summary>
    /// One pull-based readback of the selected card's full context —
    /// announced AND rendered into the focusable panel, from ONE render
    /// of ONE event so the two cannot drift.
    /// </summary>
    public void WhereAmI()
    {
        // Through the one state mapping, like every other read verb: this
        // is the PULL surface, and a pull that yields silence is the
        // failure t0 §1.4 exists to prevent.
        if (!_document.AdmitStructuralRead())
        {
            return;
        }
        // Nothing selected falls back to the first card in reading order
        // (a fresh landing) — "where am I" always answers.
        string? nodeId = _document.Selection.Selected
            ?? (_document.Outline.Count > 0 ? _document.Outline[0].NodeId : null);
        if (nodeId is null)
        {
            Announce(new CanvasA11yEvent.CanvasStatus(new CanvasStatusNote.Empty()));
            return;
        }
        if (!_document.TryWhereAmI(nodeId, out CanvasWhereAmI? context, out string detail)
            || context is null)
        {
            Announce(new CanvasA11yEvent.CanvasActionFailed(
                CanvasFailedAction.WhereAmI, detail));
            return;
        }
        // ONE view, and BOTH numbers read on this frame. The match is
        // ASYNC since task T6, but the VIEW is one applied publication's
        // worth of state — the unit and the rows it filters are one
        // immutable value — so nothing lands BETWEEN the numerator and
        // the denominator. The mixed-canvas window stays gone: the view
        // derives from the applied unit, never from a fresh query, so
        // there is no answer to take against a handle a reload has
        // already swapped.
        CanvasFilterView view = _document.Filter;
        uint shown = (uint)view.Rows.Count;
        uint total = (uint)_document.Outline.Count;
        var readback = new CanvasA11yEvent.CanvasWhereAmI(
            KindLabel: context.Kind,
            Title: context.Title,
            GroupPath: context.GroupPath,
            OrdinalN: context.OrdinalN,
            TotalM: context.TotalM,
            ConnectionCount: context.ConnectionCount,
            InCount: context.InCount,
            OutCount: context.OutCount,
            ColorName: context.ColorName,
            Marked: _document.Selection.IsMarked(nodeId),
            Mode: _document.Modes.Active?.Mode,
            // NARROWED, not "a needle is in the field" (mac's key): a
            // clause built from rows nothing narrowed would read
            // "9 of 9 shown" and claim a match nobody made, which is the
            // one thing C10's invariant forbids. Recorded as a
            // micro-divergence in C11.
            Filter: view.Narrowed
                ? new CanvasFilterState.Active(shown, total)
                : new CanvasFilterState.Inactive());
        // The panel shows the SAME string the announcement speaks — one
        // render, no second composition (t0 §1.4/§3). The pairing has one
        // narrow exception the announce boundary introduced: on a RETIRED
        // document whose handle close has not landed yet, admission can
        // still admit, so the panel string is composed while `Speak`
        // refuses the line. Nobody is looking at that panel — a retired
        // document has no surface — and the alternative is composing a
        // sentence for a closed funnel, which is the trade C7 already
        // took for the mode stack. Recorded rather than papered over.
        _document.WhereAmIText = CanvasAnnouncer.RenderLabel(readback);
        Announce(readback);
    }

    // --- Filter (#373) ---------------------------------------------------

    /// <summary>Ctrl+F / the palette row: reveal and focus the filter
    /// field.</summary>
    public void FilterCards()
    {
        if (!_document.AdmitStructuralRead())
        {
            return;
        }
        _document.RequestFilterFocus(_presenter?.Owner);
    }

    /// <summary>
    /// The palette row and the header's Clear button. It ANSWERS where
    /// mac stays silent: "Filter cleared — ⟨n⟩ cards." is true of the
    /// resulting state whether or not a needle was in the field, and a
    /// verb the user invoked that says nothing is the never-silent
    /// failure (CD-43 — mac guards and returns).
    ///
    /// On a canvas that cannot ANSWER, the mapping's sentence is the
    /// true one and this says that instead: the count would come from an
    /// empty outline, and "0 cards" reads as an empty canvas rather than
    /// an unreadable one.
    /// </summary>
    public void ClearFilter()
    {
        // THE EFFECT FIRST, then admission choosing which sentence is
        // true of it — the Escape rung's order, and the right one.
        // Clearing is what the user asked for and it always succeeds:
        // the needle is host state, not a question for the canvas.
        // Gating the CLEAR on admission made the visible command and the
        // Escape rung disagree during a reload — the command announced
        // "Opening canvas…" and left the needle in the field, the rung
        // cleared it — which is two routes to one operation differing in
        // exactly the window C4 and C10 were fixed for.
        _document.FilterText = string.Empty;
        // Only the SENTENCE is the mapping's (contract C4).
        // `Filter cleared — n cards.` is a claim about a canvas, and on
        // one that cannot answer it is false: the count would be the
        // empty outline's, and "0 cards" reads as an empty canvas rather
        // than an unreadable one.
        if (_document.AdmitStructuralRead())
        {
            // The widening is synchronous even under task T6's async
            // match — an inactive needle is the view's own answer, no
            // job — so this counts the rows this frame put on screen.
            Announce(new CanvasA11yEvent.CanvasFilterCleared(
                (uint)_document.Outline.Count));
        }
    }

    /// <summary>
    /// The debounced result count (t0 §1.5 — the announcer's filter class
    /// coalesces a keystroke burst into one line).
    /// </summary>
    /// <remarks>
    /// The number is taken from the view the surfaces are DISPLAYING,
    /// never recomputed, so the announced count and the rows on screen
    /// cannot disagree. When the needle changed but nothing could answer
    /// it, the previous rows are still on screen, so counting them as
    /// matches for what the user just typed would be a false number —
    /// the state mapping's sentence says why instead.
    /// </remarks>
    public void AnnounceFilterCount()
    {
        if (!_document.FilterActive)
        {
            return;
        }
        CanvasFilterView view = _document.Filter;
        if (view.Current)
        {
            Announce(new CanvasA11yEvent.CanvasFilterCount((uint)view.Rows.Count));
            return;
        }
        if (_document.FilterAnswerInFlight)
        {
            // The async match has not landed (task T6): a count now
            // would describe rows the needle did not choose, and the
            // completion's projection announces the honest one. Saying
            // nothing here is not a silent verb — the debounced count
            // is the keystroke's echo, and it arrives with the answer.
            return;
        }
        Announce(new CanvasA11yEvent.CanvasStatus(
            _document.ReadRefusal ?? new CanvasStatusNote.Reopening()));
    }

    /// <summary>
    /// The filter field's result-summary text: the same sentence, from
    /// the same view and the same state mapping the announcement uses —
    /// one composition, no second opinion about which sentence the state
    /// owes.
    /// </summary>
    /// <remarks>
    /// ASYNC since task T6, and the four-branch shape follows from
    /// that: a current answer counts; a previous answer still on screen
    /// counts, because the number describes displayed rows; an
    /// in-flight first match renders nothing rather than a claim; and
    /// everything else is the state mapping's sentence.
    ///
    /// `Current` is false for TWO reasons, not one, and both land in the
    /// same branch because the state mapping has the honest sentence for
    /// each: the surface is not rendering rows (a reload, a failure —
    /// a count is a claim about rows ON SCREEN), or no handle could
    /// answer the needle. The first subsumes what this comment used to
    /// call the swapped-handle case: the view is not current while the
    /// document is not rendering rows, so during a reload there is no
    /// query to take.
    /// </remarks>
    public string FilterSummaryText()
    {
        CanvasFilterView view = _document.Filter;
        if (view.Current || (_document.FilterAnswerInFlight && view.Narrowed))
        {
            // Current, or the PREVIOUS answer still on screen while the
            // new match runs (task T6): either way the number counts
            // the rows the surface is showing, which is the one
            // invariant C10 has.
            return CanvasPhrase.FilterSummary(view.Rows.Count, _document.Outline.Count);
        }
        if (_document.FilterAnswerInFlight)
        {
            // Nothing narrowed yet and the answer in flight: no number
            // is true of a match nobody made, and the state mapping has
            // no sentence for a healthy document. The region renders
            // empty until the completion's projection lands.
            return string.Empty;
        }
        return CanvasAnnouncer.RenderLabel(new CanvasA11yEvent.CanvasStatus(
            _document.ReadRefusal ?? new CanvasStatusNote.Reopening()));
    }

    // --- Modes (t0 §2) ---------------------------------------------------

    public bool CommitMode() => _document.Modes.Commit();

    public bool CancelMode() => _document.Modes.Cancel();

    // --- Chord arms ------------------------------------------------------

    /// <summary>
    /// Down/Up. Rule R2 gates it on the projection owning focus; the
    /// projection then MOVES the reader itself and the movement narrates
    /// through the document's one selection mutation, so the navigator's
    /// job here is the boundary the projection cannot speak to.
    /// </summary>
    private bool ArrowMove(bool forward)
    {
        if (_presenter is not { ProjectionHasFocus: true } presenter)
        {
            return false;
        }
        if (!_document.AdmitStructuralRead())
        {
            return true;
        }
        if (_document.FilteredOutline.Count == 0)
        {
            AnnounceNothingToMoveThrough();
            return true;
        }
        if (presenter.CanMoveWithinProjection(forward))
        {
            return false;
        }
        Announce(new CanvasA11yEvent.CanvasStatus(
            forward
                ? new CanvasStatusNote.EndOfCanvas()
                : new CanvasStatusNote.StartOfCanvas()));
        return true;
    }

    /// <summary>
    /// Right/Left FOLLOW, unconditionally, on the outline (contract C3,
    /// CD-48).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The spec asked for "connection-follow when the selected card has
    /// connections, else tree semantics, as mac does". Mac does not do
    /// that: <c>CanvasOutlineView.swift</c> delivers
    /// <c>canvasFollowConnection</c> unconditionally and returns handled,
    /// so a connectionless card ANSWERS there ("No outgoing connection.").
    /// The blend the spec describes was never shipped, and implementing
    /// it left one keypress on a leaf doing nothing and saying nothing —
    /// the never-silent rule broken by a precedence nobody had.
    /// </para>
    /// <para>
    /// Expand/collapse stays keyboard-reachable on the tree without
    /// arrows: Enter on a group toggles it (the activation seam), the
    /// numpad <c>+</c>/<c>-</c> are WPF's own <c>TreeViewItem</c> keys,
    /// and the <c>ExpandCollapse</c> pattern is what a screen reader
    /// drives — all three pinned by
    /// <c>ExpandCollapseSurvivesTheArrowsBeingClaimed</c>.
    /// </para>
    /// <para>
    /// The TABLE keeps Left/Right for the grid's CELL navigation, which
    /// the UIA Table pattern depends on and the W4-1 conformance matrix
    /// asserts; follow there is the palette row, and it answers the same
    /// way.
    /// </para>
    /// </remarks>
    private bool ArrowFollow(bool forward)
    {
        if (_presenter is not
            { ProjectionHasFocus: true, Projection: CanvasSurfaceKind.Outline })
        {
            return false;
        }
        FollowConnection(forward);
        return true;
    }

    /// <summary>
    /// Enter commits an active mode, and only while a PROJECTION owns the
    /// keys — rule R2's own question, asked here instead of a list of
    /// control types (contract C1/C3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A tunnelling handler runs before the element the reader is
    /// standing on, so an ungated Enter out-ranked every control on the
    /// surface: it would have COMMITTED the mode from the visible CANCEL
    /// MODE button — the user's intent inverted on the exact control M6
    /// exists for — and from the filter field, where Return is the
    /// field's own key on both platforms.
    /// </para>
    /// <para>
    /// Gated by asking where focus IS rather than by naming what owns
    /// Enter, because the list was the brittle half: <c>ComboBox</c>,
    /// <c>Hyperlink</c>, a templated item part and every control PR E and
    /// PR F add would each have had to be remembered, and the one that
    /// was not would re-open this silently. "A projection has the keys"
    /// is the same question every other bare-key arm already asks, and it
    /// is closed by construction.
    /// </para>
    /// <para>
    /// Escape stays broad on purpose: cancelling from anywhere in the
    /// surface is M4-adjacent — no mode may survive without focus — and
    /// the ladder is the canvas's answer for it everywhere.
    /// </para>
    /// <para>
    /// The key is CONSUMED whether or not the commit applied, because a
    /// mode was running and Enter belonged to it (mac consumes Return
    /// with the refusal for the same reason). Letting a refused commit
    /// fall through would hand the key to whatever is underneath while
    /// the mode is still up.
    /// </para>
    /// </remarks>
    private bool CommitModeFromKey()
    {
        if (_presenter is not { ProjectionHasFocus: true } || !_document.Modes.IsActive)
        {
            return false;
        }
        _ = _document.Modes.Commit();
        return true;
    }

    private bool EscapeFromKey() =>
        _document.Modes.HandleEscape() != CanvasEscapeRung.WorkspaceTab;

    private bool FilterCardsFromKey()
    {
        // On the TABLE the grid's own `FilterCommand` owns Ctrl+F and is
        // subscribed to this same field (spec §7), so the navigator stands
        // aside rather than shadowing the substrate's route.
        if (_presenter is not { Projection: CanvasSurfaceKind.Outline })
        {
            return false;
        }
        FilterCards();
        return true;
    }

    private bool WhereAmIFromKey()
    {
        WhereAmI();
        return true;
    }

    // --- Ladder rungs ----------------------------------------------------

    /// <summary>
    /// Rung 2. Consumes only when there is something to clear, so an
    /// Escape on an unfiltered canvas falls through to rung 3 instead of
    /// being swallowed with no effect.
    /// </summary>
    private bool ClearFilterRung()
    {
        if (!_document.FilterActive && _document.FilterText.Length == 0)
        {
            return false;
        }
        // The rung CONSUMES the press either way — there was a needle,
        // so Escape belongs to this rung and must not fall through to
        // the next one — but what it SAYS goes through the mapping, for
        // `ClearFilter`'s reason. The needle is cleared before the
        // admission check, because clearing it is the user's request and
        // an unreadable canvas is no reason to keep a needle they just
        // dismissed.
        _document.FilterText = string.Empty;
        if (_document.AdmitStructuralRead())
        {
            Announce(new CanvasA11yEvent.CanvasFilterCleared(
                (uint)_document.Outline.Count));
        }
        // AFTER the sentence, which is where it was before admission was
        // added and where it belongs: seating the projection can seat a
        // first selection when the needle filtered the previous one away,
        // and that seat has its own line. Speaking the clear first keeps
        // the two in the order the user's press caused them.
        _presenter?.FocusProjection();
        return true;
    }

    /// <summary>Rung 3 — the surface's own transient regions.</summary>
    private bool SurfaceRung() => _presenter?.DismissTransientRegion() ?? false;

    // --- Shared ----------------------------------------------------------

    /// <summary>
    /// The one movement every verb ends in: the document's narrating
    /// selection mutation, then the reader's focus follows it on whatever
    /// projection is showing.
    /// </summary>
    private void MoveTo(string nodeId)
    {
        _document.SelectNode(nodeId);
        _presenter?.FocusRow(nodeId);
    }

    /// <summary>There is nowhere to move: either the filter matched
    /// nothing, or the canvas is empty. Two different facts, two
    /// sentences.</summary>
    private void AnnounceNothingToMoveThrough() =>
        Announce(new CanvasA11yEvent.CanvasStatus(
            _document.FilterActive
                ? new CanvasStatusNote.NoCardsMatchFilter()
                : new CanvasStatusNote.Empty()));

    /// <summary>Through the DOCUMENT's announce boundary, so a verb
    /// invoked on a retired canvas composes nothing (contract C7).</summary>
    private void Announce(CanvasA11yEvent @event) => _document.Speak(@event);
}
