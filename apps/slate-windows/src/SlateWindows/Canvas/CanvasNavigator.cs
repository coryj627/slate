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
/// narrow: three questions and three focus moves — nothing is on it that
/// the navigator does not call. Everything else the navigator does is
/// model state and announcements, which is what makes the verb facts
/// drivable without a window.
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

    /// <summary>Put keyboard focus on a row, silently (contract C12).</summary>
    void FocusRow(string nodeId);

    /// <summary>Put keyboard focus back on the showing projection.</summary>
    void FocusProjection();

    // Deliberately NOT here: "focus the filter field". Ctrl+F raises the
    // document's focus TOKEN instead, because the field belongs to every
    // pane showing the canvas and only the one the reader is in should
    // take it — a presenter call would have picked whichever pane the
    // navigator happened to be holding (the mac Codoki #626 rule,
    // restated for panes).

    /// <summary>
    /// Escape's third rung: dismiss a transient region inside the canvas
    /// (the Where-am-I panel, the interim card detail) or leave the filter
    /// field, handing focus back to the projection. False when there was
    /// nothing to leave, which is what lets the press fall through to the
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

    internal void DetachPresenter(ICanvasSurfacePresenter presenter)
    {
        if (ReferenceEquals(_presenter, presenter))
        {
            _presenter = null;
        }
    }

    /// <summary>
    /// Deliver one key press from the canvas surface's tunnelling
    /// handler. Returns whether the press was consumed; an unconsumed
    /// press keeps its ordinary meaning for whatever has focus, which is
    /// what lets the tree keep Right/Left expand-collapse and the grid
    /// keep cell navigation.
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
        CanvasFilterView view = _document.Filter;
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
            Filter: view.Narrowed
                ? new CanvasFilterState.Active(
                    (uint)view.Rows.Count, (uint)_document.Outline.Count)
                : new CanvasFilterState.Inactive());
        // The panel shows the SAME string the announcement speaks — one
        // render, no second composition (t0 §1.4/§3).
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
        _document.RequestFilterFocus();
    }

    /// <summary>
    /// The palette row and the header's Clear button. Always answers:
    /// "Filter cleared — ⟨n⟩ cards." is true of the resulting state
    /// whether or not a needle was in the field, and a verb the user
    /// invoked that says nothing is the never-silent failure (CD-43 —
    /// mac guards and stays silent here).
    /// </summary>
    public void ClearFilter()
    {
        _document.FilterText = string.Empty;
        Announce(new CanvasA11yEvent.CanvasFilterCleared((uint)_document.Outline.Count));
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
        if (!view.Current)
        {
            Announce(new CanvasA11yEvent.CanvasStatus(
                _document.ReadRefusal ?? new CanvasStatusNote.Reopening()));
            return;
        }
        Announce(new CanvasA11yEvent.CanvasFilterCount((uint)view.Rows.Count));
    }

    /// <summary>
    /// The filter field's result-summary text, or NULL when there is
    /// nothing to summarise yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same view and the same state mapping the announcement uses —
    /// one composition, no second opinion about which sentence the state
    /// owes. The number always counts the rows the surfaces are
    /// DISPLAYING, which is what keeps <i>displayed rows == the number on
    /// screen</i> true at every instant, including the frames between a
    /// keystroke and its answer.
    /// </para>
    /// <para>
    /// The causes of a non-current view get different answers, and that
    /// is the whole of why this is not one line, in this order:
    /// </para>
    /// <list type="number">
    /// <item><description>a document that CANNOT answer says so — the
    /// state's own sentence, mac's behaviour — and it says so IN
    /// PREFERENCE to a stale memo, because rows matched against an older
    /// needle are not an answer to this one and C10's rule is that a
    /// document which cannot answer says so on the LABEL too, not only in
    /// the announcement;</description></item>
    /// <item><description>a query that RAN AND FAILED with the document
    /// otherwise able to answer keeps the honest "not now" — the same
    /// sentence <see cref="AnnounceFilterCount"/> speaks, from the same
    /// fallback mac uses when a handle goes stale inside the call. Without
    /// this branch a failed match leaves the region blank forever with a
    /// needle in the field above it;</description></item>
    /// <item><description>a query merely IN FLIGHT says nothing new: the
    /// previous answer is still on screen and its count still describes
    /// it, so the label lags by a frame the way every async surface
    /// does;</description></item>
    /// <item><description>and before the FIRST answer there is no summary
    /// at all rather than a "9 of 9 cards match" that would claim a match
    /// nobody made.</description></item>
    /// </list>
    /// </remarks>
    public string? FilterSummaryText()
    {
        if (!_document.FilterActive)
        {
            return null;
        }
        CanvasFilterView view = _document.Filter;
        if (view.Current)
        {
            return CanvasPhrase.FilterSummary(view.Rows.Count, _document.Outline.Count);
        }
        if (_document.ReadRefusal is { } note)
        {
            return CanvasAnnouncer.RenderLabel(new CanvasA11yEvent.CanvasStatus(note));
        }
        if (_document.FilterAnswerFailed)
        {
            return CanvasAnnouncer.RenderLabel(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.Reopening()));
        }
        return view.Narrowed
            ? CanvasPhrase.FilterSummary(view.Rows.Count, _document.Outline.Count)
            : null;
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
    /// Right/Left, with the precedence mac pins: connection-follow when
    /// the selected card HAS connections, else the projection's own
    /// meaning. On the outline that is the tree's expand/collapse; on the
    /// table it is the grid's CELL navigation, which the Table pattern
    /// depends on — so the follow chord is outline-only there and the
    /// palette rows are the table's path (contract C3, CD-44).
    /// </summary>
    private bool ArrowFollow(bool forward)
    {
        if (_presenter is not
            { ProjectionHasFocus: true, Projection: CanvasSurfaceKind.Outline })
        {
            return false;
        }
        if (_document.Selection.Selected is not { } selected)
        {
            return false;
        }
        // An UNANSWERABLE lookup keeps the arrow's tree meaning rather
        // than claiming a connection state nothing returned.
        if (_document.NeighborsIfKnown(selected) is not { Count: > 0 })
        {
            return false;
        }
        FollowConnection(forward);
        return true;
    }

    private bool CommitModeFromKey() => _document.Modes.IsActive && _document.Modes.Commit();

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
        _document.FilterText = string.Empty;
        Announce(new CanvasA11yEvent.CanvasFilterCleared((uint)_document.Outline.Count));
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

    private void Announce(CanvasA11yEvent @event) => _document.Announcer.Announce(@event);
}
