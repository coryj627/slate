// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>The five viewport verbs plus the follow toggle (§D D14) —
/// one closed set, so the presenter seam grows ONE member for all six
/// and the binding record enumerates them by name.</summary>
internal enum CanvasViewportVerb
{
    ZoomIn,
    ZoomOut,
    ActualSize,
    FitCanvas,
    ZoomToSelection,
    ToggleFollowSelection,
}

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

    /// <summary>Answer a viewport verb on THIS pane's visual surface
    /// (§D D7, task TD-5) with what it COMMITTED (§H TH-5, IH-39): the
    /// zoom percent with its context, or the follow state, so the
    /// navigator can speak core's event with the exact payload; a pane
    /// that acted silently says so, and one with no visual renderer to
    /// address answers Refused — the caller owns the no-pane refusal,
    /// because the presenter answers for one pane and the refusal speaks
    /// for the document.</summary>
    CanvasViewportOutcome ViewportCommand(CanvasViewportVerb verb);

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
        // §F TF-3 (FD-3): a held transient owns the arrows — the
        // step runs ahead of selection movement, and Shift is the
        // large grid step. Without a transient the arrows keep their
        // §C meaning untouched.
        AddChord(Key.Down, ModifierKeys.None, () => ModeStepOr(() => ArrowMove(forward: true), 0, 1, large: false));
        AddChord(Key.Up, ModifierKeys.None, () => ModeStepOr(() => ArrowMove(forward: false), 0, -1, large: false));
        AddChord(Key.Right, ModifierKeys.None, () => ModeStepOr(() => ArrowFollow(forward: true), 1, 0, large: false));
        AddChord(Key.Left, ModifierKeys.None, () => ModeStepOr(() => ArrowFollow(forward: false), -1, 0, large: false));
        AddChord(Key.Down, ModifierKeys.Shift, () => ModeStepOr(null, 0, 1, large: true));
        AddChord(Key.Up, ModifierKeys.Shift, () => ModeStepOr(null, 0, -1, large: true));
        AddChord(Key.Right, ModifierKeys.Shift, () => ModeStepOr(null, 1, 0, large: true));
        AddChord(Key.Left, ModifierKeys.Shift, () => ModeStepOr(null, -1, 0, large: true));
        AddChord(Key.Enter, ModifierKeys.None, CommitModeFromKey);
        AddChord(Key.Escape, ModifierKeys.None, EscapeFromKey);
        AddChord(Key.F, ModifierKeys.Control, FilterCardsFromKey);
        // §E TE-11 (E19/ED-1): the verb chord and the history
        // pair - canvas-scoped delivery, live exactly while a canvas
        // surface holds the keys (rule R2).
        AddChord(Key.N, ModifierKeys.Control | ModifierKeys.Alt, NewCardFromKey);
        // §G2 TG2-4 (G2-1/G2-9): the connected-card chord, mac's ⌃⌥⌘N — the
        // presenter's owner rides into the operation (IG2-34).
        AddChord(
            Key.N,
            ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift,
            CreateConnectedCardFromKey);
        // §F TF-4 (F9): the mode front doors. G grabs, R is mac's
        // quick loop — during resize it COMMITS, otherwise it enters.
        AddChord(Key.G, ModifierKeys.Control | ModifierKeys.Alt, MoveModeFromKey);
        AddChord(Key.R, ModifierKeys.Control | ModifierKeys.Alt, CommitOrEnterResizeFromKey);
        AddChord(Key.C, ModifierKeys.Control | ModifierKeys.Alt, ConnectToFromKey);
        AddChord(Key.M, ModifierKeys.Control | ModifierKeys.Alt, ToggleMarkFromKey);
        AddChord(Key.Z, ModifierKeys.Control, UndoFromKey);
        AddChord(Key.Y, ModifierKeys.Control, RedoFromKey);
        // The viewport chords (§D D14). Zoom rides Ctrl and answers
        // from anywhere on the canvas tab; fit and zoom-to-selection
        // are bare Shift chords and therefore VISUAL SURFACE ONLY
        // (rule R2 — on the outline or table those keys belong to
        // typing).
        AddChord(Key.OemPlus, ModifierKeys.Control, () => ViewportFromKey(CanvasViewportVerb.ZoomIn));
        AddChord(Key.OemMinus, ModifierKeys.Control, () => ViewportFromKey(CanvasViewportVerb.ZoomOut));
        AddChord(Key.D0, ModifierKeys.Control, () => ViewportFromKey(CanvasViewportVerb.ActualSize));
        AddChord(Key.D1, ModifierKeys.Shift, () => VisualOnlyFromKey(CanvasViewportVerb.FitCanvas));
        AddChord(Key.D2, ModifierKeys.Shift, () => VisualOnlyFromKey(CanvasViewportVerb.ZoomToSelection));
        AddChord(
            Key.I,
            ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift,
            WhereAmIFromKey);
    }

    /// <summary>§E TE-11: Ctrl+Alt+N - the funnel owns every
    /// refusal, so the key only relays the verb.</summary>
    private bool NewCardFromKey()
    {
        _document.CanvasNewCard(_presenter?.Owner);
        return true;
    }

    private bool CreateConnectedCardFromKey()
    {
        _ = _document.CanvasCreateConnectedCard(owner: _presenter?.Owner);
        return true;
    }

    private bool UndoFromKey()
    {
        _document.CanvasUndo();
        return true;
    }

    private bool RedoFromKey()
    {
        _document.CanvasRedo();
        return true;
    }

    private bool MoveModeFromKey()
    {
        _ = EnterMoveMode();
        return true;
    }

    /// <summary>§G TG-0 (G1): Ctrl+Alt+M — mac's ⌃⌘M in FD-3's family;
    /// the document owns every refusal, the key only relays.</summary>
    private bool ToggleMarkFromKey()
    {
        _document.ToggleMark();
        return true;
    }

    private bool ConnectToFromKey()
    {
        _document.OpenCardPicker(CanvasCardPickerPurpose.ConnectTo);
        return true;
    }

    private bool CommitOrEnterResizeFromKey()
    {
        CommitOrEnterResize();
        return true;
    }

    /// <summary>§F TF-9 (F8): connect mode — the origin remembered
    /// (id + publication identity), the token installed through F4's
    /// admission, and the navigator's own movements stepping
    /// candidates untouched: NO transient is held, so the arrows keep
    /// their §C meaning verbatim. Return on the origin — or with no
    /// movement — ends without effect; Return elsewhere is F7's exact
    /// staged apply with label NULL (the mode is the no-label fast
    /// path by contract). A vanished origin at Return speaks
    /// PickDifferentTarget and returns REFUSED — frozen C forbids a
    /// cancel inside OnCommit — and the wrapper below posts the
    /// cancel OUTSIDE the commit stack, selection left where the
    /// reader stands (IF-28's mode half, reconciled).</summary>
    internal bool EnterConnectMode()
    {
        if (_presenter is not { } presenter)
        {
            return false;
        }
        if (_document.CurrentLoadedForModeEntry is not { } loaded)
        {
            _document.SpeakModeEntryNotReady();
            return false;
        }
        if (_document.Selection.Selected is not { } originId)
        {
            Announce(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.NothingSelected()));
            return false;
        }
        string originTitle = _document.RowFor(originId)?.Title ?? "card";
        var origin = new CanvasConnectOrigin(originId, originTitle, loaded);
        var token = new object();
        var spec = new CanvasModeSpec(
            CanvasMode.Connect,
            new CanvasModeObject.Card(originTitle),
            () => SubmitConnectCommit(origin, token),
            () =>
            {
                _document.ClearConnectOrigin();
                if (_document.RowFor(originId) is null)
                {
                    // The origin is gone: the selection stays where
                    // the reader stands and the restoration is
                    // honest about stating nothing.
                    return new CanvasModeRestoration.Unstated();
                }
                _document.SeatSelectionSilently(originId);
                // §F TF-9 (IF-29): the restoration addresses the
                // OWNING presenter and returns reader FOCUS, not just
                // selection — the silent seat is the fallback when the
                // row cannot take it.
                _ = presenter.FocusRow(originId);
                return new CanvasModeRestoration.BackAt(originTitle);
            },
            Token: token);
        bool entered = EnterMode(spec, presenter);
        if (!entered)
        {
            return false;
        }
        _document.InstallConnectOrigin(origin);
        return true;
    }

    /// <summary>§F TF-9 (F8): Return's arms — no movement ends
    /// without effect; a target applies F7's staged connect with
    /// label null and the completion resolving F4b's rows.</summary>
    private CanvasModeCommitResult SubmitConnectCommit(
        CanvasConnectOrigin origin, object token)
    {
        if (_document.RowFor(origin.OriginId) is null)
        {
            // BELT-AND-BRACES: while the token holds, no funnel verb
            // can remove the origin, and every real vanish arrives as
            // a displacement F1a already cancels - F8's demanded
            // cancel is satisfied by construction, recorded in the
            // TF-9 record. The arm still refuses honestly.
            Announce(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.PickDifferentTarget()));
            return CanvasModeCommitResult.Refused();
        }
        string? target = _document.Selection.Selected;
        if (target is null || target == origin.OriginId)
        {
            _document.ClearConnectOrigin();
            _document.Funnel.ClearModeToken(token);
            return CanvasModeCommitResult.Committed(
                new CanvasA11yEvent.CanvasModeEndedWithoutEffect(
                    CanvasMode.Connect));
        }
        string targetTitle = _document.RowFor(target)?.Title ?? "card";
        var stage = new CanvasConnectStage(
            origin.OriginId, origin.OriginTitle, target, targetTitle,
            origin.Identity);
        void Completion(
            CanvasMutationOperation operation, CanvasOperationOutcome outcome)
        {
            switch (outcome)
            {
                case CanvasOperationOutcome.Installed:
                    _document.ClearConnectOrigin();
                    _document.Funnel.ClearModeToken(token);
                    _document.Modes.ResolveCommit(
                        operation.Id,
                        CanvasModeCommitResult.Committed(
                            new CanvasA11yEvent.CanvasConnected(
                                stage.OriginTitle, stage.TargetTitle, null)));
                    break;
                case CanvasOperationOutcome.Conflict:
                    _document.Funnel.SuspendModeToken(token);
                    _document.Modes.AbandonPendingCommit();
                    break;
                case CanvasOperationOutcome.RefusedPrepare:
                case CanvasOperationOutcome.ApplyRefused:
                    _document.Modes.ResolveCommit(
                        operation.Id, CanvasModeCommitResult.Refused());
                    break;
                case CanvasOperationOutcome.DisplacedBeforeApply:
                case CanvasOperationOutcome.Displaced:
                    // IF-2's arbiter rule, as in the transient
                    // completion (review round 1).
                    _document.Modes.ResolveCommitDisplaced(operation.Id);
                    break;
                default:
                    _document.ClearConnectOrigin();
                    _document.Funnel.ClearModeToken(token);
                    _document.Modes.ResolveCommit(
                        operation.Id, CanvasModeCommitResult.Committed());
                    break;
            }
        }

        CanvasMutationOperation? submitted =
            _document.ConnectForMode(stage, token, Completion);
        return submitted is null
            ? CanvasModeCommitResult.Refused()
            : CanvasModeCommitResult.Pending(submitted.Id);
    }

    /// <summary>§F TF-4 (F3/IF-18 reconciled): mac's quick loop is
    /// the SAME-MODE exception only — the resize chord during resize
    /// commits it; any other active mode still gets frozen C's M7
    /// rejection inside EnterMode.</summary>
    internal void CommitOrEnterResize()
    {
        if (_document.Modes.Active?.Mode == CanvasMode.Resize)
        {
            _ = _document.Modes.Commit();
            return;
        }

        _ = EnterResizeMode();
    }

    /// <summary>§F TF-4 (F3): resize mode — a single SELECTED scene
    /// node, groups included through the Card grammar (the recorded
    /// mac divergence, adopted). Refusals speak; the capture is
    /// TF-2's never-silent read with the resize flag.</summary>
    internal bool EnterResizeMode()
    {
        if (_presenter is not { } presenter)
        {
            return false;
        }
        if (_document.CurrentLoadedForModeEntry is not { } loaded)
        {
            _document.SpeakModeEntryNotReady();
            return false;
        }
        if (_document.Selection.Selected is not { } selected)
        {
            Announce(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.NothingSelected()));
            return false;
        }
        CanvasTransientHolder? holder = CanvasTransientHolder.TryCapture(
            _document.SessionForModeEntry, loaded, [selected], isResize: true);
        if (holder is null)
        {
            Announce(new CanvasA11yEvent.CanvasActionFailed(
                CanvasFailedAction.CanvasAction, "resize"));
            return false;
        }
        string title = _document.RowFor(selected)?.Title ?? "card";
        var obj = new CanvasModeObject.Card(title);
        var token = new object();
        var spec = new CanvasModeSpec(
            CanvasMode.Resize,
            obj,
            () => SubmitTransientCommit(
                token, CanvasTransientVerb.Resize, obj, "resize", title),
            () =>
            {
                _document.DiscardTransient();
                return new CanvasModeRestoration.SizeRestored();
            },
            Token: token);
        bool entered = EnterMode(spec, presenter);
        if (entered)
        {
            _document.InstallTransient(holder);
        }

        return entered;
    }

    /// <summary>§F TF-4 (F3): the resize step — ←→ width, ↑↓ height.
    /// REJECT-THE-STEP, mac's rule copied by contract: neither
    /// dimension moves when either would cross MinCardSize; the
    /// refusal is CanvasResizeClamped and nothing changes.</summary>
    private bool ResizeStep(CanvasTransientHolder transient, CanvasLoaded loaded, int dx, int dy, bool large)
    {
        CanvasConstants constants = SlateUniffiMethods.CanvasConstants();
        double step = large ? constants.GridStepLarge : constants.GridStep;
        string id = transient.Ids[0];
        CanvasRect r = transient.Rects[id];
        double width = r.Width + (dx * step);
        double height = r.Height + (dy * step);
        if (width < constants.MinCardSize || height < constants.MinCardSize)
        {
            Announce(new CanvasA11yEvent.CanvasResizeClamped());
            return true;
        }

        return ApplyResizeRect(
            transient, loaded, new CanvasRect(r.X, r.Y, width, height), preset: null);
    }

    /// <summary>§F TF-4 (F3): presets and steps land through the SAME
    /// overlap machine — a preset that creates or clears overlap
    /// speaks the transition in its geometry sentence. The spoken
    /// width and height are MINTED (the safeInt twin), a material
    /// rule rather than a cast.</summary>
    private bool ApplyResizeRect(
        CanvasTransientHolder transient,
        CanvasLoaded loaded,
        CanvasRect rect,
        CanvasResizePreset? preset)
    {
        string id = transient.Ids[0];
        bool overlapping = false;
        bool ran;
        try
        {
            ran = loaded.Lease.Invoke(
                () => true,
                handle => overlapping = _document.SessionForModeEntry.CanvasCheckOverlap(
                    handle, rect, [id]).Length > 0);
        }
        catch (VaultException)
        {
            ran = false;
        }
        if (!ran)
        {
            Announce(new CanvasA11yEvent.CanvasActionFailed(
                CanvasFailedAction.CanvasAction, "resize"));
            return true;
        }
        CanvasOverlapTransition? transition =
            overlapping && !transient.WasOverlapping
                ? CanvasOverlapTransition.Onset
                : !overlapping && transient.WasOverlapping
                    ? CanvasOverlapTransition.Cleared
                    : null;
        transient.Rects = transient.Rects.SetItem(id, rect);
        transient.WasOverlapping = overlapping;
        _document.NotifyTransientChanged();
        Announce(new CanvasA11yEvent.CanvasResizeGeometry(
            preset, CanvasSafeUint(rect.Width), CanvasSafeUint(rect.Height), transition));
        return true;
    }

    /// <summary>§F TF-4 (F3): Default Size — core's DefaultCardW/H
    /// through the preset path. Outside resize mode the verb refuses
    /// with the mode reason (F9's typed refusal).</summary>
    internal bool ResizeDefaultSize()
    {
        if (_document.Funnel.ModeSuspended)
        {
            _document.Funnel.AnnounceConflictPending();
            return false;
        }
        if (_document.Transient is not { } transient || !transient.IsResize
            || _document.CurrentLoadedForModeEntry is not { } loaded)
        {
            Announce(new CanvasA11yEvent.CanvasBlocked(
                new CanvasBlockedReason.ModeBusy()));
            return false;
        }
        CanvasConstants constants = SlateUniffiMethods.CanvasConstants();
        CanvasRect r = transient.Rects[transient.Ids[0]];
        return ApplyResizeRect(
            transient,
            loaded,
            new CanvasRect(r.X, r.Y, constants.DefaultCardW, constants.DefaultCardH),
            CanvasResizePreset.DefaultSize);
    }

    /// <summary>§F TF-4 (F3): Fit to Content — the D-5 placeholder
    /// formula, identical on both hosts by contract: default width;
    /// height from the text length (32 chars a line, 24 a line plus
    /// 40, capped at 600, floored at MinCardSize). The content read
    /// is the VM's never-silent node-text table — a refused read
    /// keeps the transient and its own sentence has already
    /// spoken.</summary>
    internal bool ResizeFitContent()
    {
        if (_document.Funnel.ModeSuspended)
        {
            _document.Funnel.AnnounceConflictPending();
            return false;
        }
        if (_document.Transient is not { } transient || !transient.IsResize
            || _document.CurrentLoadedForModeEntry is not { } loaded)
        {
            Announce(new CanvasA11yEvent.CanvasBlocked(
                new CanvasBlockedReason.ModeBusy()));
            return false;
        }
        string id = transient.Ids[0];
        if (_document.NodeTextOf(id) is not { } text)
        {
            return false;
        }
        CanvasConstants constants = SlateUniffiMethods.CanvasConstants();
        int newlines = text.Count(c => c == '\n');
        int lines = Math.Max(1, (text.Length / 32) + newlines);
        double height = Math.Min(
            600, Math.Max((lines * 24) + 40, constants.MinCardSize));
        CanvasRect r = transient.Rects[id];
        return ApplyResizeRect(
            transient,
            loaded,
            new CanvasRect(r.X, r.Y, constants.DefaultCardW, height),
            CanvasResizePreset.FitToContent);
    }

    /// <summary>§F TF-4 (F3): the minting rule, mac's `canvasSafeInt`
    /// twin — non-finite mints 0, the clamp is [0, 9e15], the value
    /// rounds, and the cast saturates. A material rule: hostile
    /// .canvas geometry the parser tolerates must not trap the
    /// announcer.</summary>
    internal static uint CanvasSafeUint(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }
        double clamped = Math.Round(Math.Min(Math.Max(value, 0), 9e15));
        return clamped >= uint.MaxValue ? uint.MaxValue : (uint)clamped;
    }

    /// <summary>§F TF-3 (F2): move mode — entry on the moving set
    /// (the marked set reading-ordered, else the selection), through
    /// TF-1's admitted preflight, with TF-2's holder installed on
    /// success. Every refusal speaks; the capture is one never-silent
    /// read and a failed capture speaks the FD-6 arm.</summary>
    internal bool EnterMoveMode()
    {
        if (_presenter is not { } presenter)
        {
            return false;
        }
        if (_document.CurrentLoadedForModeEntry is not { } loaded)
        {
            _document.SpeakModeEntryNotReady();
            return false;
        }
        var members = _document.Selection.Marked.Count > 0
            ? new List<string>(_document.Selection.Marked)
            : _document.Selection.Selected is { } one ? [one] : [];
        if (members.Count == 0)
        {
            Announce(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.NothingSelected()));
            return false;
        }
        CanvasTransientHolder? holder = CanvasTransientHolder.TryCapture(
            _document.SessionForModeEntry, loaded, members, isResize: false);
        if (holder is null)
        {
            Announce(new CanvasA11yEvent.CanvasActionFailed(
                CanvasFailedAction.CanvasAction, "move"));
            return false;
        }
        string primaryTitle = _document.RowFor(holder.Ids[0])?.Title ?? "card";
        CanvasModeObject obj = holder.Ids.Length == 1
            ? new CanvasModeObject.Card(primaryTitle)
            : new CanvasModeObject.Cards((uint)holder.Ids.Length);
        var token = new object();
        var spec = new CanvasModeSpec(
            CanvasMode.Move,
            obj,
            () => SubmitTransientCommit(
                token, CanvasTransientVerb.Move, obj, "move", primaryTitle),
            () =>
            {
                _document.DiscardTransient();
                return holder.Ids.Length == 1
                    ? new CanvasModeRestoration.BackAt(primaryTitle)
                    : new CanvasModeRestoration.CardsReturned(
                        (uint)holder.Ids.Length);
            },
            Token: token);
        bool entered = EnterMode(spec, presenter);
        if (entered)
        {
            _document.InstallTransient(holder);
        }

        return entered;
    }

    /// <summary>§F TF-3 (F4a/F4b): the commit closure — no change ends
    /// without effect; otherwise ONE UpdateNodeGeometry action submits
    /// through the funnel with the mode token, the completion mapping
    /// each terminal row, and the machine holds Pending until it
    /// lands.</summary>
    private CanvasModeCommitResult SubmitTransientCommit(
        object token,
        CanvasTransientVerb verb,
        CanvasModeObject obj,
        string verbWord,
        string primaryTitle)
    {
        if (_document.Transient is not { } transient
            || _document.CurrentLoadedForModeEntry is not { } loaded)
        {
            return CanvasModeCommitResult.Refused();
        }
        var ops = new List<CanvasOp>();
        foreach (string id in transient.Ids)
        {
            CanvasRect now = transient.Rects[id];
            if (!now.Equals(transient.Originals[id]))
            {
                ops.Add(new CanvasOp.UpdateNodeGeometry(
                    id, now.X, now.Y, now.Width, now.Height));
            }
        }
        if (ops.Count == 0)
        {
            _document.DiscardTransient();
            _document.Funnel.ClearModeToken(token);
            return CanvasModeCommitResult.Committed(
                new CanvasA11yEvent.CanvasModeEndedWithoutEffect(
                    verb == CanvasTransientVerb.Move
                        ? CanvasMode.Move
                        : CanvasMode.Resize));
        }
        string name = transient.Ids.Length == 1
            ? $"{verbWord} \"{primaryTitle}\""
            : $"{verbWord} {SlateUniffiMethods.CountNoun((ulong)transient.Ids.Length, "card", "cards")}";
        var operation = new CanvasMutationOperation(
            new CanvasOperationId(name),
            this,
            _document.Selection.Selected,
            loaded,
            CanvasMutationEffect.KeepSelection,
            modeToken: token);
        operation.Completion = outcome =>
        {
            switch (outcome)
            {
                case CanvasOperationOutcome.Installed:
                    _document.DiscardTransient();
                    _document.Funnel.ClearModeToken(token);
                    _document.Modes.ResolveCommit(
                        operation.Id,
                        CanvasModeCommitResult.Committed(
                            new CanvasA11yEvent.CanvasModeCommitted(verb, obj)));
                    break;
                case CanvasOperationOutcome.Conflict:
                    // FD-5: the mode SUSPENDS — transient frozen, the
                    // token yields to the resolution, the pending mark
                    // clears so Esc/commit refuse honestly while the
                    // record stands.
                    _document.Funnel.SuspendModeToken(token);
                    _document.Modes.AbandonPendingCommit();
                    break;
                case CanvasOperationOutcome.RefusedPrepare:
                case CanvasOperationOutcome.ApplyRefused:
                    _document.Modes.ResolveCommit(
                        operation.Id, CanvasModeCommitResult.Refused());
                    break;
                case CanvasOperationOutcome.DisplacedBeforeApply:
                case CanvasOperationOutcome.Displaced:
                    // The completion is the one arbiter while pending
                    // (IF-2) — the F1a watcher stood down, so the
                    // displaced resolution cancels the mode itself
                    // (review round 1).
                    _document.Modes.ResolveCommitDisplaced(operation.Id);
                    break;
                default:
                    // Unindexed / RefreshRefused: the write landed; the
                    // mode ends with no spoken success — the refresh-only
                    // region is the surface (F4b).
                    _document.DiscardTransient();
                    _document.Funnel.ClearModeToken(token);
                    _document.Modes.ResolveCommit(
                        operation.Id, CanvasModeCommitResult.Committed());
                    break;
            }
        };
        CanvasMutationAdmission admission = _document.Funnel.Apply(
            operation, _ => new CanvasAction(name, [.. ops]), name);
        return admission == CanvasMutationAdmission.Admitted
            ? CanvasModeCommitResult.Pending(operation.Id)
            : CanvasModeCommitResult.Refused();
    }

    /// <summary>§F TF-3 (FD-3): the arrow router — a held transient
    /// takes the step; otherwise the §C handler (null for Shift rows,
    /// which exist only for the mode).</summary>
    private bool ModeStepOr(Func<bool>? fallback, int dx, int dy, bool large)
    {
        if (_document.Transient is not null && _document.Modes.IsActive)
        {
            return ModeStep(dx, dy, large);
        }

        return fallback?.Invoke() ?? false;
    }

    /// <summary>§F TF-3 (F2/F2a): one grid step over the whole rigid
    /// set — the overlap two-state machine speaks transitions only,
    /// and a throwing read leaves the transient untouched with the
    /// FD-6 arm spoken.</summary>
    internal bool ModeStep(int dx, int dy, bool large)
    {
        if (_document.Transient is not { } transient
            || _document.CurrentLoadedForModeEntry is not { } loaded)
        {
            return false;
        }
        // §F TF-10 (IF-30): the suspended column — the transient is
        // FROZEN while the conflict pends, so a step refuses with the
        // ladder's own sentence and moves nothing. The gate sits in
        // the ONE door every step takes, chord-routed or direct.
        if (_document.Funnel.ModeSuspended)
        {
            _document.Funnel.AnnounceConflictPending();
            return true;
        }
        if (transient.IsResize)
        {
            return ResizeStep(transient, loaded, dx, dy, large);
        }
        CanvasConstants constants = SlateUniffiMethods.CanvasConstants();
        double step = large ? constants.GridStepLarge : constants.GridStep;
        var moved = new Dictionary<string, CanvasRect>(StringComparer.Ordinal);
        foreach (string id in transient.Ids)
        {
            CanvasRect r = transient.Rects[id];
            moved[id] = new CanvasRect(
                r.X + (dx * step), r.Y + (dy * step), r.Width, r.Height);
        }
        bool overlapping = false;
        CanvasRelativeDesc[]? descs = null;
        try
        {
            bool ran = loaded.Lease.Invoke(
                () => true,
                handle =>
                {
                    foreach (string id in transient.Ids)
                    {
                        if (_document.SessionForModeEntry.CanvasCheckOverlap(
                            handle, moved[id], [.. transient.Ids]).Length > 0)
                        {
                            overlapping = true;
                            break;
                        }
                    }
                    descs = _document.SessionForModeEntry.CanvasDescribeRelative(
                        handle, moved[transient.Ids[0]], [.. transient.Ids]);
                });
            if (!ran || descs is null)
            {
                Announce(new CanvasA11yEvent.CanvasActionFailed(
                    CanvasFailedAction.CanvasAction, "move"));
                return true;
            }
        }
        catch (VaultException)
        {
            Announce(new CanvasA11yEvent.CanvasActionFailed(
                CanvasFailedAction.CanvasAction, "move"));
            return true;
        }
        CanvasOverlapTransition? transition =
            overlapping && !transient.WasOverlapping
                ? CanvasOverlapTransition.Onset
                : !overlapping && transient.WasOverlapping
                    ? CanvasOverlapTransition.Cleared
                    : null;
        transient.Rects = moved.ToImmutableDictionary(StringComparer.Ordinal);
        transient.WasOverlapping = overlapping;
        _document.NotifyTransientChanged();
        Announce(new CanvasA11yEvent.CanvasMoveRelative(descs, transition));
        return true;
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
        // §F TF-1 (IF-18): the C machine's OWN rejection arm answers
        // the active-mode case FIRST — M7's vocabulary, not the
        // funnel's Busy — so the preflight below never shadows it.
        if (!_document.Modes.AdmitsEntry)
        {
            return _document.Modes.Enter(spec, pane);
        }
        AttachPresenter(pane);
        // §F TF-1 (IF-8): entry is ADMITTED like a write — the same
        // ladder, each refusal speaking its §E sentence — and the mode
        // token installs under the held gate. If the machine then
        // refuses anyway, the token ROLLS BACK immediately: "token
        // installed, mode never entered" is the leak that bricks a
        // document.
        if (_document.CurrentLoadedForModeEntry is not { } basis)
        {
            _document.SpeakModeEntryNotReady();
            return false;
        }
        object token = spec.Token ?? new object();
        var operation = new CanvasMutationOperation(
            new CanvasOperationId($"enter {spec.Mode}"),
            pane,
            _document.Selection.Selected,
            basis,
            CanvasMutationEffect.KeepSelection,
            modeToken: token);
        if (_document.Funnel.AdmitModeEntry(operation)
            != CanvasMutationAdmission.Admitted)
        {
            return false;
        }
        // §F TF-1 (F4c): every exit clears the token — the wrap
        // covers commit, cancel, departures and retirement, because
        // they all run these closures; a Pending commit's clear rides
        // its completion instead. A leaked token bricks the document,
        // which is what the first cut proved by leaking one.
        Func<CanvasModeCommitResult> onCommit = spec.OnCommit;
        Func<CanvasModeRestoration> onCancel = spec.OnCancel;
        CanvasModeSpec wrapped = spec with
        {
            OnCommit = () =>
            {
                CanvasModeCommitResult result = onCommit();
                if (result.Arm == CanvasModeCommitArm.Applied)
                {
                    _document.Funnel.ClearModeToken(token);
                    _document.Funnel.ForgetSuspendedModeToken(token);
                }

                return result;
            },
            OnCancel = () =>
            {
                // §F TF-10 (IF-30): a cancel during SUSPENSION must
                // forget the yielded identity too — the first cut
                // cleared only the live token, and the suspended one
                // lingered forever.
                _document.Funnel.ClearModeToken(token);
                _document.Funnel.ForgetSuspendedModeToken(token);
                return onCancel();
            },
        };
        bool entered = _document.Modes.Enter(wrapped, pane);
        if (!entered)
        {
            _document.Funnel.ClearModeToken(token);
            _document.Funnel.ForgetSuspendedModeToken(token);
        }
        return entered;
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

    // --- Viewport (§D D14, task TD-5) ------------------------------------

    /// <summary>Ctrl+= / the palette row: one step in.</summary>
    public void ZoomIn() => ViewportVerb(CanvasViewportVerb.ZoomIn);

    /// <summary>Ctrl+- / the palette row: one step out.</summary>
    public void ZoomOut() => ViewportVerb(CanvasViewportVerb.ZoomOut);

    /// <summary>Ctrl+0 / the palette row: zoom 1.0.</summary>
    public void ActualSize() => ViewportVerb(CanvasViewportVerb.ActualSize);

    /// <summary>Shift+1 on the visual surface / the palette row.</summary>
    public void FitCanvas() => ViewportVerb(CanvasViewportVerb.FitCanvas);

    /// <summary>Shift+2 on the visual surface / the palette row.</summary>
    public void ZoomToSelection() => ViewportVerb(CanvasViewportVerb.ZoomToSelection);

    /// <summary>The follow toggle (§D D4's origin-sensitive rule).</summary>
    public void ToggleFollowSelection() =>
        ViewportVerb(CanvasViewportVerb.ToggleFollowSelection);

    /// <summary>The one route every viewport verb takes (§D D7): the
    /// load gate first — a document that cannot answer says so through
    /// the state mapping — then the pane. The PRESENTER answers for
    /// its own pane; when no pane can answer, the refusal is the
    /// document's typed no-pane sentence (obligation ID-7's arm),
    /// because C4's load mapping admits a Ready document and has
    /// nothing honest to say about panes.</summary>
    private void ViewportVerb(CanvasViewportVerb verb)
    {
        if (!_document.AdmitStructuralRead())
        {
            return;
        }
        if (verb == CanvasViewportVerb.ZoomToSelection
            && _document.Selection.Selected is null)
        {
            // D14's data arm: no selection is the verb's OWN
            // precondition, answered with the canonical sentence
            // before any pane is consulted — a zoom to nothing is not
            // a pane problem.
            Announce(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.NothingSelected()));
            return;
        }
        // §H TH-5 (IH-39): the pane answers with what it committed, and
        // the navigator speaks core's event with that payload — the
        // §W-D consumer the sweep found missing. A silent outcome is
        // mac's own silence (an empty canvas has nothing to fit); a
        // refusal, or no pane, is the typed no-pane sentence (ID-7).
        switch (_presenter?.ViewportCommand(verb))
        {
            case CanvasViewportOutcome.Zoomed zoomed:
                Announce(new CanvasA11yEvent.CanvasZoom(zoomed.Context, zoomed.Percent));
                return;
            case CanvasViewportOutcome.FollowChanged follow:
                Announce(new CanvasA11yEvent.CanvasFollowSelectionToggled(follow.Following));
                return;
            case CanvasViewportOutcome.SilentOutcome:
                return;
            default:
                break;
        }
        Announce(new CanvasA11yEvent.CanvasViewportNoPane());
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
        switch (_document.FilterVerdict)
        {
            case CanvasFilterVerdict.InFlight:
                // BEFORE the view is even built — the early return was
                // paying the row walk it discarded (T6 review). A count
                // now would describe rows the needle did not choose;
                // the completion's projection announces the honest one,
                // so saying nothing here is not a silent verb — the
                // debounced count is the keystroke's echo, and it
                // arrives with the answer.
                return;
            case CanvasFilterVerdict.Current:
                Announce(new CanvasA11yEvent.CanvasFilterCount(
                    (uint)_document.Filter.Rows.Count));
                return;
            default:
                Announce(FilterStatusSentence());
                return;
        }
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
        CanvasFilterVerdict verdict = _document.FilterVerdict;
        if (view.Current || (verdict == CanvasFilterVerdict.InFlight && view.Narrowed))
        {
            // Current, or the PREVIOUS answer still on screen while the
            // new match runs (task T6): either way the number counts
            // the rows the surface is showing, which is the one
            // invariant C10 has.
            return CanvasPhrase.FilterSummary(view.Rows.Count, _document.Outline.Count);
        }
        if (verdict == CanvasFilterVerdict.InFlight)
        {
            // Nothing narrowed yet and the answer in flight: no number
            // is true of a match nobody made, and the state mapping has
            // no sentence for a healthy document. The region renders
            // empty until the completion's projection lands.
            return string.Empty;
        }
        return CanvasAnnouncer.RenderLabel(FilterStatusSentence());
    }

    /// <summary>The ONE composition of the filter's status sentence —
    /// the count boundary ANNOUNCES it, the summary RENDERS it, and the
    /// two cannot drift (the cleanup pass; the invariant used to be
    /// kept by hand across two ladders). The failed arm rides the
    /// generic failed-action event with the needle as its dynamic
    /// detail — CD-38's recorded shape and its STOP: a typed
    /// filter-failed reason is a core change this task may not
    /// make.</summary>
    private CanvasA11yEvent FilterStatusSentence() =>
        _document.FilterVerdict == CanvasFilterVerdict.Failed
            ? new CanvasA11yEvent.CanvasActionFailed(
                CanvasFailedAction.CanvasAction, _document.FilterText)
            : new CanvasA11yEvent.CanvasStatus(
                _document.ReadRefusal ?? new CanvasStatusNote.Reopening());

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
        // subscribed to this same field (spec §7) — but only when the
        // GRID has the keys. §C's m9 (repaired here, in §D as assigned):
        // with the table showing and focus in the HEADER, the grid's
        // binding never fires, so standing aside on "the table is
        // showing" made Ctrl+F reach nobody. The stand-aside now asks
        // who OWNS the keys, not what is showing.
        if (_presenter is { Projection: CanvasSurfaceKind.Table } table
            && table.ProjectionHasFocus)
        {
            return false;
        }
        if (_presenter is null)
        {
            return false;
        }
        FilterCards();
        return true;
    }

    private bool ViewportFromKey(CanvasViewportVerb verb)
    {
        ViewportVerb(verb);
        return true;
    }

    /// <summary>Rule R2 for the two bare Shift chords: they exist only
    /// where the projection owns the keys AND it is the VISUAL board —
    /// on the outline or table, Shift+1 is a typed character.</summary>
    private bool VisualOnlyFromKey(CanvasViewportVerb verb)
    {
        // BOTH halves of R2 (the review round): the visual must be
        // showing AND own the keys — a bare Shift chord consumed while
        // the caret sits in the filter field would eat the '!' the
        // reader typed.
        if (_presenter is not { Projection: CanvasSurfaceKind.Visual } pane
            || !pane.ProjectionHasFocus)
        {
            return false;
        }
        ViewportVerb(verb);
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

/// <summary>§H TH-5 (IH-39): what a pane's viewport verb COMMITTED —
/// the payload core's <c>CanvasZoom</c> and
/// <c>CanvasFollowSelectionToggled</c> events need, which a bool could
/// not carry. <see cref="Silent"/> is a pane that acted, or had nothing
/// to act on, and speaks nothing (mac's empty-canvas fit);
/// <see cref="Refused"/> is a pane that could not act.</summary>
internal abstract record CanvasViewportOutcome
{
    private CanvasViewportOutcome()
    {
    }

    internal static CanvasViewportOutcome Silent { get; } = new SilentOutcome();

    internal static CanvasViewportOutcome Refused { get; } = new RefusedOutcome();

    /// <summary>The committed zoom, as the percent core renders, with
    /// the context the verb implies (fit, zoom to selection) or none.</summary>
    internal sealed record Zoomed(uint Percent, CanvasZoomContext? Context) : CanvasViewportOutcome;

    /// <summary>The committed follow-selection state.</summary>
    internal sealed record FollowChanged(bool Following) : CanvasViewportOutcome;

    internal sealed record SilentOutcome : CanvasViewportOutcome;

    internal sealed record RefusedOutcome : CanvasViewportOutcome;
}
