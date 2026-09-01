// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using uniffi.slate_uniffi;
using System.Windows.Threading;

namespace SlateWindows.Canvas;

/// <summary>
/// Why keyboard focus left the canvas — the M4 question, as a closed set
/// so the answer is a table rather than a condition written out at each
/// site (contract C7).
/// </summary>
internal enum CanvasFocusDeparture
{
    /// <summary>The workspace moved to another tab, or this tab's body
    /// stopped being visible.</summary>
    TabSwitch,

    /// <summary>Keyboard focus left the canvas surface for another part
    /// of the shell — another pane, the sidebar, the files tree.</summary>
    PaneFocus,

    /// <summary>The window itself lost activation.</summary>
    WindowDeactivated,

    /// <summary>Focus moved into a shell overlay that is layered OVER
    /// this tab — the command palette, Quick Open, the search overlay, a
    /// sheet. The canvas is still the tab underneath.</summary>
    ModalOverlay,

    /// <summary>
    /// Focus moved into an open MENU — the menu bar, a submenu, or a
    /// row's context menu.
    /// </summary>
    /// <remarks>
    /// The same fact as <see cref="ModalOverlay"/> one surface over, and
    /// it earns its own name because the failure it prevents is
    /// self-inflicted: the shell's own Canvas menu carries Commit Mode
    /// and Cancel Mode, so a menu that cancelled the mode on OPENING
    /// would kill its own two items before the pointer reached them.
    /// PR E's and PR F's per-row context menus are the M6 visible
    /// controls for every mode verb and inherit the identical
    /// requirement — which is why this is an arm of the table rather
    /// than a condition at one site (CD-41).
    /// </remarks>
    MenuOpen,
}

/// <summary>
/// The Esc ladder's rungs, innermost first (t0 §2 M5). The value is the
/// answer to "which rung did this press consume", which is what makes
/// "exactly one rung per press" a table test rather than a claim.
/// </summary>
internal enum CanvasEscapeRung
{
    /// <summary>Rung 1 — an active mode is cancelled and restored.</summary>
    Mode,

    /// <summary>Rung 2 — an active filter is cleared.</summary>
    Filter,

    /// <summary>Rung 3 — a transient region inside the canvas (the
    /// interim card detail, or the filter field or its result summary
    /// holding focus with nothing to clear) re-seats the reader through
    /// C6's seat rule. NOT the Where-am-I panel: an open panel pre-empts
    /// the whole ladder (CD-47), so no reachable press arrives here with
    /// it up.</summary>
    Surface,

    /// <summary>Rung 4 — nothing in the canvas consumed the press, so it
    /// bubbles to the workspace exactly as it would with no canvas
    /// open.</summary>
    WorkspaceTab,
}

/// <summary>
/// One modal interaction (move / resize / connect …) — the mac
/// <c>CanvasModeController.ModeSpec</c> twin.
/// </summary>
/// <param name="Mode">The mode. Core owns its spoken name and its exit
/// instructions (t0 §2 M1).</param>
/// <param name="Object">What the mode acts on — one titled card, or a
/// count.</param>
/// <param name="OnCommit">The commit side effect. Returns whether it
/// APPLIED, and the confirmation event (t0 §1.3) when it has one of its
/// own to speak.</param>
/// <param name="OnCancel">The cancel side effect (restore prior state).
/// Returns what was put back, which core phrases.</param>
internal sealed record CanvasModeSpec(
    CanvasMode Mode,
    CanvasModeObject Object,
    Func<CanvasModeCommitResult> OnCommit,
    Func<CanvasModeRestoration> OnCancel,
    object? Token = null);

/// <summary>
/// What a mode's commit effect did (t0 §2 M2).
/// </summary>
/// <remarks>
/// <para>
/// M2 was modelled as infallible and it is not. A commit can be REFUSED
/// — the canvas went degraded or lost its handle mid-mode, the funnel's
/// admission says no — and mac keeps the mode alive when that happens:
/// its container checks <c>admitCanvasMutation</c> BEFORE calling
/// <c>commit()</c> and consumes the key with the refusal, leaving the
/// transient state intact so the user can fix the problem or cancel.
/// </para>
/// <para>
/// Modelled as an OUTCOME rather than as mac's call-site pre-gate on
/// purpose: a pre-gate has to be re-implemented at every entry point —
/// the key, the header button, the palette row, the menu item — and the
/// one that forgets loses the user's work silently. Here the machine
/// cannot clear the stack for an effect that did not apply.
/// </para>
/// <para>
/// A refusal announces for ITSELF. The controller has no sentence for
/// it: which refusal it is (degraded, detached, conflicted) is the
/// effect's knowledge, and inventing one here would be host prose.
/// </para>
/// </remarks>
internal enum CanvasModeCommitArm
{
    /// <summary>The effect ran and the mode ends now.</summary>
    Applied,

    /// <summary>The effect refused; the mode stands (M2).</summary>
    Refused,

    /// <summary>§F TF-0 (IF-6's recorded supersession of the frozen
    /// two-arm shape): the effect SUBMITTED an operation to the funnel
    /// and the truth arrives asynchronously — the mode STANDS until
    /// <see cref="CanvasModeController.ResolveCommit"/> delivers the
    /// outcome under the submitted operation's identity. Pending is
    /// legal ONLY when the submission was Admitted; a synchronous
    /// admission refusal must answer Refused instead (IF-12).</summary>
    Pending,
}

internal readonly record struct CanvasModeCommitResult(
    CanvasModeCommitArm Arm, CanvasA11yEvent? Confirmation, object? OperationId)
{
    internal bool Applied => Arm == CanvasModeCommitArm.Applied;

    internal static CanvasModeCommitResult Committed(
        CanvasA11yEvent? confirmation = null) =>
        new(CanvasModeCommitArm.Applied, confirmation, null);

    internal static CanvasModeCommitResult Refused() =>
        new(CanvasModeCommitArm.Refused, null, null);

    /// <summary>The pending arm carries the operation identity the
    /// completion must match (IF-9).</summary>
    internal static CanvasModeCommitResult Pending(object operationId) =>
        new(CanvasModeCommitArm.Pending, null, operationId);
}

/// <summary>
/// W6-1 PR C (#745): the canvas mode stack — t0 §2 M1–M7, and the mac
/// <c>CanvasModeController</c> twin.
///
/// <list type="bullet">
/// <item><description><b>M1</b> entry announces the mode, the object and
/// the exits (<c>CanvasModeEntered</c>).</description></item>
/// <item><description><b>M2</b> <see cref="Commit"/> applies (the spec's
/// own confirmation); <see cref="Cancel"/> restores prior state and
/// announces the restoration (<c>CanvasModeCancelled</c>).</description></item>
/// <item><description><b>M3</b> while active,
/// <see cref="ContainerValue"/> carries "⟨Mode⟩: ⟨card⟩" and the surface
/// publishes it as its <c>ItemStatus</c> — inspectable, never merely
/// announced (t0 §3).</description></item>
/// <item><description><b>M4</b> a focus departure cancels with
/// restoration and an announcement (<see cref="HandleFocusDeparture"/>);
/// no mode survives without focus, and no mode is a keyboard trap
/// (WCAG 2.1.2).</description></item>
/// <item><description><b>M5</b> <see cref="HandleEscape"/> consumes
/// exactly one rung and NAMES it.</description></item>
/// <item><description><b>M6</b> every transition is a plain method, so
/// the header's visible Commit/Cancel buttons and the palette rows drive
/// the same machine the keys do.</description></item>
/// <item><description><b>M7</b> entering while a mode is active is
/// rejected with <c>CanvasModeRejected</c>; nothing commits.</description></item>
/// </list>
///
/// The SENTENCES are core's (PR 0a): a spec carries the typed
/// <c>CanvasMode</c> and <c>CanvasModeObject</c>, and every lifecycle
/// phrasing comes from the vocabulary. This class owns the stack, not
/// the copy.
///
/// PR C ships it against a TEST mode; PR F re-runs the same M1–M7 suite
/// against the real move and resize modes, which is why nothing here
/// knows what a mode DOES.
/// </summary>
internal sealed class CanvasModeController : BindableBase
{
    private readonly Action<CanvasA11yEvent> _announce;
    private readonly List<(CanvasEscapeRung Rung, Func<bool> Consume)> _rungs = [];
    private CanvasModeSpec? _active;

    /// <summary>§F TF-0: the thread this controller was built
    /// on - ResolveCommit marshals home to it (the announcer's own
    /// capture idiom).</summary>
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    /// <summary>True while a commit effect is running — the window in
    /// which the stack must not be transitioned by anybody else (M2's
    /// one outcome).</summary>

    private bool _committing;

    /// <summary>§F TF-0: the in-flight commit's operation identity.
    /// While set, the mode STANDS, re-entrant Commit and Cancel refuse
    /// (IF-11), and only a completion bearing THIS identity resolves
    /// (IF-9) — a late or foreign completion drops itself.</summary>
    private object? _pendingCommit;

    /// <summary>Set by <see cref="Shutdown"/>. The stack is TERMINAL from
    /// then on: no mode may be entered, committed or cancelled, and
    /// nothing is announced about it.</summary>
    private bool _retired;

    /// <summary>
    /// A focus departure raised from inside a commit effect, held until
    /// the commit's outcome is known.
    /// </summary>
    /// <remarks>
    /// ONE slot, latest-wins: an effect that provokes two departures has
    /// still only left the canvas once as far as M4 is concerned, and
    /// queueing them would cancel a mode that a later arm already
    /// resolved. The same shape the announcer's coalescer takes, for the
    /// same reason.
    /// </remarks>
    private CanvasFocusDeparture? _deferredDeparture;

    public CanvasModeController(Action<CanvasA11yEvent> announce)
    {
        ArgumentNullException.ThrowIfNull(announce);
        _announce = announce;
    }

    private object? _owner;

    /// <summary>
    /// The SURFACE running the active mode, or null when no mode is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stack is document-shared — one controller, however many panes
    /// show the document — so `IsActive` is a fact about the DOCUMENT and
    /// says nothing about which pane the reader is driving. Anything that
    /// acts on a mode's behalf from a per-surface position needs this
    /// instead, and a sibling pane reading `IsActive` as its own is how
    /// one pane cancelled the mode another pane's reader was using.
    /// </para>
    /// <para>
    /// REQUIRED at entry rather than looked up, which is what makes
    /// "active but ownerless" unrepresentable. It was representable for
    /// one wave — `Enter` captured whatever the navigator happened to be
    /// attached to, including nothing — and a null owner then read
    /// identically to "no mode active" at every consumer, so a peer
    /// pane's departure was forwarded to a mode that was not its own.
    /// The ambiguity is gone because the state is gone.
    /// </para>
    /// <para>
    /// `CanvasNavigator.EnterMode` is the production entry, and the pane
    /// comes from the INVOCATION: the caller names the surface it is
    /// acting for. So a mode entered while a palette or a menu holds the
    /// keys belongs to the pane that asked, not to whichever pane held
    /// the keys most recently — the navigator's cached presenter is not
    /// consulted. Refusals are runtime guards: a null spec or pane
    /// throws, and an entry the controller does not admit returns false
    /// without moving affinity.
    /// </para>
    /// <para>
    /// A plain field read. It was written as a read-through
    /// (`_active is null ? null : _owner`) one wave before the field
    /// gained its clear, and once both existed the read-through's own
    /// mutation could not be made to fail: the field is nulled at the one
    /// place `Active` goes null, and `Enter` is the only writer, so there
    /// is no state in which a stale owner could be read. Removed on that
    /// evidence, with `ACompletedModeHoldsNoPane` left asserting the
    /// invariant — which is the guard a future teardown that stopped
    /// cancelling would trip, and which a read-through would have hidden.
    /// </para>
    /// </remarks>
    internal object? Owner => _owner;

    /// <summary>The active mode, or null. Published so the surface's M3
    /// value and its Commit/Cancel buttons follow it.</summary>
    public CanvasModeSpec? Active
    {
        get => _active;
        private set
        {
            if (SetField(ref _active, value))
            {
                if (value is null)
                {
                    // The RETENTION half, at the one place every ordinary
                    // exit passes through. `Owner` already READ as null
                    // once the mode ended, and that is exactly the shape
                    // round 2's B3 caught on the request properties: a
                    // read boundary hides a field that still holds a
                    // closed pane's control tree. A REFUSED commit never
                    // reaches here — it keeps its mode — so the
                    // preservation that case needs is by construction
                    // rather than by an exception.
                    _owner = null;
                }
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(ContainerValue));
            }
        }
    }

    public bool IsActive => _active is not null;

    /// <summary>
    /// The M3 inspectable state, for the canvas surface's
    /// <c>AutomationProperties.ItemStatus</c>.
    /// </summary>
    /// <remarks>
    /// §W-C LABEL class, never spoken (0a-13: the vocabulary carries
    /// announcements plus exactly two label-grade events, and the M3
    /// value is neither), so it is composed here from the SAME typed
    /// fields the spoken entry carries — the mac
    /// <c>containerAXValue</c> twin, and the one host-side spelling of
    /// the mode names that survived PR 0a on either platform.
    /// <c>TheModeValueAgreesWithCoresOwnModeSentence</c> pins it against
    /// core's render of <c>CanvasModeEntered</c> rather than against
    /// itself.
    /// </remarks>
    public string? ContainerValue =>
        _active is { } spec ? $"{Label(spec.Mode)}: {Label(spec.Object)}" : null;

    /// <summary>The M6 visible controls' enablement, and the palette
    /// rows': a mode transition is only offered while one is
    /// active.</summary>
    public bool CanCommitOrCancel => IsActive && _pendingCommit is null;

    /// <summary>§F TF-2 (IF-2): the own-commit exemption's read —
    /// while a commit is pending, the completion is the one arbiter
    /// and the displacement watcher stands down.</summary>
    internal bool HasPendingCommitForTests => _pendingCommit is not null;

    /// <summary>
    /// Whether an entry would be ADMITTED — asked by callers that must
    /// not act on a refusal (contract C8).
    /// </summary>
    /// <remarks>
    /// The same condition <see cref="Enter"/> applies, not a copy of it:
    /// a caller with a side effect to perform needs to know the answer
    /// BEFORE the effect, and two spellings of "would this be admitted"
    /// is how the answer and the effect drift apart. `EnterMode`'s
    /// presenter attach is the caller this exists for — a refused entry
    /// that had already moved affinity left the mode owned by one pane
    /// while the verbs acted through another.
    /// </remarks>
    internal bool AdmitsEntry => !_retired && _active is null;

    /// <summary>
    /// M1 + M7. Returns false when the entry was REJECTED because a mode
    /// is already active — nothing commits, and the announcement names
    /// the mode that is holding the stack.
    /// </summary>
    public bool Enter(CanvasModeSpec spec, object owner)
    {
        ArgumentNullException.ThrowIfNull(spec);
        // The OWNING pane, required. A mode with no pane running it is
        // the state every per-surface consumer had to guess about, so it
        // is not a state this type can be in.
        ArgumentNullException.ThrowIfNull(owner);
        if (!AdmitsEntry)
        {
            // An ACTIVE mode names itself; RETIREMENT is silent — which
            // is not the never-silent table being broken, it is that
            // table's precondition being absent. C4 answers a verb the
            // user invoked on a canvas they are READING; this document
            // has been retired, so there is no surface showing it and its
            // announcer is already shut — a sentence composed here would
            // be the A5 `Debug.Fail` rather than something anybody hears.
            // The refusal is the return value, for the caller that asked.
            // (`Shutdown` nulls `_active`, so the two arms cannot both
            // apply and retirement cannot speak by accident.)
            if (_active is { } current)
            {
                Speak(new CanvasA11yEvent.CanvasModeRejected(current.Mode));
            }
            return false;
        }
        _owner = owner;
        Active = spec;
        Speak(new CanvasA11yEvent.CanvasModeEntered(spec.Mode, spec.Object));
        return true;
    }

    /// <summary>
    /// M2 commit — Enter, the header's Commit button, or
    /// <c>slate.canvas.commitMode</c>.
    /// </summary>
    /// <returns>
    /// Whether the mode ENDED. False means one of two things and the
    /// caller usually wants neither distinguished: no mode was active, or
    /// the effect REFUSED and the mode is still up with its transient
    /// state intact. The one caller that must tell them apart is the
    /// Enter chord, which asks <see cref="IsActive"/> first so it can
    /// consume the key either way.
    /// </returns>
    public bool Commit()
    {
        if (_retired || _active is not { } spec || _committing
            || _pendingCommit is not null)
        {
            return false;
        }
        _committing = true;
        try
        {
            CanvasModeCommitResult result = spec.OnCommit();
            switch (result.Arm)
            {
                case CanvasModeCommitArm.Applied:
                    Active = null;
                    if (result.Confirmation is { } confirmation)
                    {
                        Speak(confirmation);
                    }

                    return true;
                case CanvasModeCommitArm.Pending:
                    // §F TF-0: the truth is in flight. The mode
                    // stands; departures follow the LIVE table rather
                    // than deferring forever (the drain below runs with
                    // the pending mark already set).
                    _pendingCommit = result.OperationId
                        ?? throw new CanvasLeaseViolationException(
                            "a Pending commit carried no operation identity");
                    return false;
                default:
                    return false;
            }
        }
        finally
        {
            _committing = false;
            DrainDeferredDeparture();
        }
    }

    /// <summary>§F TF-0: the completion side of the Pending arm.
    /// Identity-checked (IF-9) and dispatcher-marshaled (IF-10, the
    /// presentation engine's own intake discipline): a completion from
    /// the work seam re-invokes itself on the thread this controller
    /// was built on, and one that no longer matches the pending
    /// identity — a late arrival after a cancel, a foreign operation —
    /// drops itself without touching the mode.</summary>
    internal void ResolveCommit(object operationId, CanvasModeCommitResult outcome)
    {
        ArgumentNullException.ThrowIfNull(operationId);
        if (!_dispatcher.CheckAccess())
        {
            _ = _dispatcher.BeginInvoke(() => ResolveCommit(operationId, outcome));
            return;
        }
        if (_retired || !ReferenceEquals(_pendingCommit, operationId))
        {
            return;
        }
        _pendingCommit = null;
        if (_active is null)
        {
            return;
        }
        if (outcome.Arm == CanvasModeCommitArm.Applied)
        {
            // Clear FIRST, speak after — the §C Committed(confirmation)
            // order preserved across the bridge.
            Active = null;
            if (outcome.Confirmation is { } confirmation)
            {
                Speak(confirmation);
            }
        }
        // Refused: the mode and its state stand (M2's rule, one seam
        // over) — the effect's own sentence spoke already.
    }

    /// <summary>§F TF-0: a cancellation (F1a displacement, Esc-led
    /// teardown) clears any in-flight mark so a late completion finds
    /// nothing to resolve.</summary>
    internal void AbandonPendingCommit() => _pendingCommit = null;

    /// <summary>
    /// The ONE place this controller speaks (contract C7).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A retired stack composes nothing. That was first fixed as a gate
    /// on `Commit`'s confirmation — the one site the failing test walked
    /// — and a per-verb gate is a list somebody has to keep complete:
    /// `Cancel` had the identical defect in a worse form, and any fifth
    /// site added later would have had it too. So the check lives where
    /// every sentence passes instead, which is the move the document
    /// made for its requests and the ladder made for its rungs.
    /// </para>
    /// <para>
    /// EVALUATION ORDER is why this works, and it looks like a detail.
    /// C# evaluates a call's ARGUMENTS before its body, so
    /// `Speak(new CanvasModeCancelled(…, spec.OnCancel()))` runs the
    /// cancel effect first and reads <c>_retired</c> afterwards. That is
    /// exactly right: the effect is arbitrary host code that can retire
    /// the document from inside itself, and a check at call ENTRY would
    /// have been made before the thing it needs to observe happened. The
    /// boundary reads retirement at EMIT time.
    /// </para>
    /// <para>
    /// The announcer keeps its own <c>Debug.Fail</c> (A5). This is not a
    /// silent sink standing in front of it: it stops the SPEAKER from
    /// composing, and anything that still reaches the funnel after
    /// retirement is a real defect the guard should be loud about.
    /// </para>
    /// </remarks>
    private void Speak(CanvasA11yEvent @event)
    {
        if (_retired)
        {
            return;
        }
        _announce(@event);
    }

    /// <summary>
    /// Apply a departure held across a commit, if there is one. Drained
    /// on EVERY exit from the transition — applied, refused, thrown, and
    /// teardown — because a slot that survives its commit cancels the
    /// NEXT one's mode instead.
    /// </summary>
    /// <remarks>
    /// TOTAL by construction, because it is called from a `finally`: a
    /// restoration or an announcement that faults in here would REPLACE
    /// the exception the caller was already propagating, and the original
    /// failure — the one worth reporting — would vanish. The departure's
    /// own outcome is not worth that, so it is logged and the unwind
    /// continues (the shell's log-and-continue pattern). The slot is
    /// emptied BEFORE the departure runs, so a fault cannot leave it
    /// loaded either.
    /// </remarks>
    private void DrainDeferredDeparture()
    {
        if (_deferredDeparture is not { } departure)
        {
            return;
        }
        _deferredDeparture = null;
        try
        {
            _ = HandleFocusDeparture(departure);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
        {
            HostLog.Write(HostDiagnosticEvent.CanvasModeDepartureFailed, exception);
        }
    }

    /// <summary>
    /// Teardown (contract C7): the document is being retired.
    /// </summary>
    /// <remarks>
    /// Ordered so the announcements still reach the user: the deferred
    /// slot drains FIRST — a departure held across a failed commit owes a
    /// restoration sentence, and the caller silences the funnel straight
    /// after this returns — then the tab's own departure ends whatever
    /// mode is left (M4's last case), and the slot is cleared so nothing
    /// stale can outlive the object holding it.
    ///
    /// The transition is FORCED closed first. Teardown can reach here
    /// from inside a commit effect — the shell retires the tab while the
    /// effect is running — and a drain that still deferred would put the
    /// departure straight back in the slot, then clear it: the mode would
    /// outlive the document with its restoration never spoken. Nothing
    /// will return to <see cref="Commit"/> to close the transition, so
    /// closing it here is the truth rather than a workaround.
    /// </remarks>
    internal void Shutdown()
    {
        _committing = false;
        try
        {
            DrainDeferredDeparture();
            _ = HandleFocusDeparture(CanvasFocusDeparture.TabSwitch);
        }
        finally
        {
            // The slot is empty when this returns, whatever happened —
            // including a restoration that faulted on the way out. A
            // document being retired must not leave a departure behind
            // for a later object to act on.
            _deferredDeparture = null;
            // TERMINAL from here, and terminal is more than idle: a
            // controller that merely had no mode would have accepted the
            // next `Enter` — from a menu item on a closed tab, from a
            // palette row the shell still had registered — and run its
            // effect against a document whose handle is gone. Every verb
            // refuses, and the ladder is emptied so nothing holds a
            // closure over a surface that is gone.
            _retired = true;
            _active = null;
            _rungs.Clear();
            // NO `_owner = null` here, though this path bypasses the
            // property that clears it. The departure above cancels any
            // active mode, and `Cancel` nulls `Active` — through the
            // setter — BEFORE it runs the restoration that could fault,
            // so the owner is already gone by the time this runs. Written
            // and removed: its own mutation could not be made to fail,
            // and `ACompletedModeHoldsNoPane` asserts the INVARIANT after
            // retirement rather than the line, so a future teardown that
            // stopped cancelling is still caught.
        }
    }

    /// <summary>M2 cancel — Escape's first rung, the header's Cancel
    /// button, or <c>slate.canvas.cancelMode</c>. False when no mode was
    /// active, which is what lets the Esc ladder fall through to the next
    /// rung.</summary>
    public bool Cancel()
    {
        if (_retired || _active is not { } spec || _committing
            || _pendingCommit is not null)
        {
            // Refused rather than deferred while a commit is in flight:
            // a direct cancel from inside a commit effect is a caller
            // error, not a race, and the commit already owns this press's
            // outcome. The DEPARTURE path defers instead, because M4 must
            // still be honoured once the outcome is known.
            return false;
        }
        Active = null;
        // `spec.OnCancel()` runs as this argument is built, so the
        // boundary inside `Speak` reads a retirement the RESTORATION
        // caused — which a check written here, before the call, could
        // not have seen.
        Speak(new CanvasA11yEvent.CanvasModeCancelled(spec.Mode, spec.OnCancel()));
        return true;
    }

    /// <summary>
    /// M4: a focus departure cancels the mode with restoration and an
    /// announcement. Returns whether it cancelled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two arms KEEP the mode alive, and both are the same recorded
    /// divergence from t0 §2 M4's literal list (contract C8, CD-41).
    /// t0 names the palette among the departures; the mac controller
    /// excludes it deliberately after red-team #521, because Commit Mode,
    /// Cancel Mode and the resize presets ARE palette commands —
    /// cancelling on palette open makes three registered verbs
    /// unreachable and contradicts M6's own "never depend on the
    /// keyboard-only path". <see cref="CanvasFocusDeparture.MenuOpen"/>
    /// is the same argument for the surface a MENU is: this shell's own
    /// Canvas menu carries those two verbs, and PR E/F's context menus
    /// carry every mode verb.
    /// </para>
    /// <para>
    /// Every other arm cancels. The switch is total over the enum, and
    /// <c>EveryFocusDepartureHasARecordedAnswer</c> enumerates the enum
    /// rather than restating the list.
    /// </para>
    /// </remarks>
    public bool HandleFocusDeparture(CanvasFocusDeparture departure)
    {
        if (_retired)
        {
            return false;
        }
        if (_committing && CancelsFor(departure))
        {
            // DEFERRED, not applied: a commit effect is mid-flight and it
            // owns this press's outcome (M2). The departure is honoured
            // the moment that outcome is known — see `Commit`.
            _deferredDeparture = departure;
            return false;
        }
        if (CancelsFor(departure) && _pendingCommit is not null)
        {
            // §F TF-0: a departure during a PENDING commit follows
            // the LIVE table — the in-flight mark is abandoned so the
            // cancel can run now, and the late completion finds nothing
            // to resolve (the identity check drops it).
            AbandonPendingCommit();
        }
        return CancelsFor(departure) && Cancel();
    }

    /// <summary>
    /// The surface RUNNING the mode is going away — its document is being
    /// replaced under it, or it is unloading.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ownership has to follow the lifecycle it names. Without this, a
    /// pane whose tab was retargeted to another canvas detached from this
    /// navigator and left the mode naming a surface that no longer shows
    /// the document — so NOBODY was entitled to reclassify a departure
    /// for it, and a sibling pane went on rendering the shared M6
    /// controls for a transient state it could commit but not own. M4
    /// says no mode survives without focus; this is the arrangement where
    /// the mode survived without a PANE.
    /// </para>
    /// <para>
    /// Routed through <see cref="HandleFocusDeparture"/> rather than
    /// cancelling here, so the commit-time DEFERRAL applies exactly as it
    /// does to every other departure: an owner that vanishes mid-commit
    /// is honoured when the outcome is known, not on top of it. Classed
    /// as `TabSwitch` because that is what happened — the pane stopped
    /// showing this canvas.
    /// </para>
    /// </remarks>
    internal bool HandleOwnerDeparture(object presenter)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        return _active is not null
            && ReferenceEquals(_owner, presenter)
            && HandleFocusDeparture(CanvasFocusDeparture.TabSwitch);
    }

    /// <summary>Whether a pane reference is still held. `Owner` is a
    /// plain read of the same field now, so the two agree — but the
    /// retention half keeps its own observable because for one wave it
    /// did NOT: a read-through returned null while the field held the
    /// pane, which is round 2's B3 one object over, and an assertion on
    /// the read could not have seen it.</summary>
    internal bool HoldsOwnerForTests => _owner is not null;

    /// <summary>The M4 table itself, split out so the deferral above and
    /// the application below cannot drift apart.</summary>
    private static bool CancelsFor(CanvasFocusDeparture departure)
    {
        bool cancels = departure switch
        {
            CanvasFocusDeparture.TabSwitch => true,
            CanvasFocusDeparture.PaneFocus => true,
            CanvasFocusDeparture.WindowDeactivated => true,
            CanvasFocusDeparture.ModalOverlay => false,
            CanvasFocusDeparture.MenuOpen => false,
            _ => throw new UnreachableException(
                $"CanvasFocusDeparture.{departure} has no M4 answer. The mode "
                + "stack's focus-departure rule is a closed table (contract C7); "
                + "a new departure is a decision, not a default."),
        };
        return cancels;
    }

    /// <summary>
    /// Register a ladder rung between the mode (rung 1) and the workspace
    /// (rung 4). The delegate returns whether it consumed the press.
    /// </summary>
    /// <remarks>
    /// Rungs are held in <see cref="CanvasEscapeRung"/> order and each
    /// value may be registered at most once: "innermost first" is a
    /// property of the LADDER, and a caller registering out of order — or
    /// twice — would make the order an accident of construction sequence.
    /// <see cref="CanvasEscapeRung.Mode"/> is the controller's own and
    /// <see cref="CanvasEscapeRung.WorkspaceTab"/> is the un-consumed
    /// answer, so neither is registrable.
    /// </remarks>
    public void RegisterRung(CanvasEscapeRung rung, Func<bool> consume)
    {
        ArgumentNullException.ThrowIfNull(consume);
        if (_retired)
        {
            // A rung registered after retirement is a closure over a dead
            // surface, kept alive by a controller nobody will ask again.
            // Refused silently for `Enter`'s reason: there is no reader to
            // tell and nothing true to say.
            return;
        }
        if (rung is CanvasEscapeRung.Mode or CanvasEscapeRung.WorkspaceTab)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rung),
                rung,
                "rung 1 is the mode stack's own cancel and rung 4 is the "
                + "un-consumed answer; neither is a registrable rung.");
        }
        if (_rungs.Any(existing => existing.Rung == rung))
        {
            throw new ArgumentException(
                $"the {rung} rung is already registered; two claimants on one "
                + "rung would make 'exactly one rung per press' depend on "
                + "registration order.",
                nameof(rung));
        }
        _rungs.Add((rung, consume));
        _rungs.Sort((left, right) => left.Rung.CompareTo(right.Rung));
    }

    /// <summary>
    /// M5: one Escape press, one rung. Returns the rung that consumed it
    /// — <see cref="CanvasEscapeRung.WorkspaceTab"/> means nothing in the
    /// canvas consumed the press and the caller must let it bubble, which
    /// is what keeps the shell's own Escape (cancel import, dismiss an
    /// overlay) working exactly as it does with no canvas open.
    /// </summary>
    public CanvasEscapeRung HandleEscape()
    {
        // TERMINAL after retirement, and by construction rather than by
        // a gate here (contract C7). Codex round 7 found this and
        // `RegisterRung` still open after the terminal state landed, and
        // the hazard is running a rung whose closure holds a surface that
        // is gone — the filter's clear, the panel's dismissal. What
        // closes it is that `Shutdown` EMPTIES the ladder and
        // `RegisterRung` refuses afterwards, so there is nothing left to
        // run; `Cancel` below is gated too, so rung 1 refuses like every
        // other verb. A third check here would answer the same
        // `WorkspaceTab` this already answers, which is a guard with no
        // power — and this task has spent five rounds learning what those
        // cost.
        if (Cancel())
        {
            return CanvasEscapeRung.Mode;
        }
        foreach ((CanvasEscapeRung rung, Func<bool> consume) in _rungs)
        {
            if (consume())
            {
                return rung;
            }
        }
        return CanvasEscapeRung.WorkspaceTab;
    }

    /// <summary>The rungs registered between mode and workspace, in
    /// ladder order — the shape the ladder table test reads.</summary>
    internal IReadOnlyList<CanvasEscapeRung> RegisteredRungs =>
        _rungs.Select(entry => entry.Rung).ToArray();

    /// <summary>
    /// Core's <c>CanvasMode::name</c>, transliterated.
    /// </summary>
    private static string Label(CanvasMode mode) => mode switch
    {
        CanvasMode.Move => "Move mode",
        CanvasMode.Resize => "Resize mode",
        CanvasMode.Connect => "Connect mode",
        _ => throw new UnreachableException($"unknown canvas mode {mode}"),
    };

    /// <summary>
    /// Core's <c>mode_object</c>, transliterated.
    /// </summary>
    /// <remarks>
    /// The count is interpolated UNGROUPED, exactly as core's
    /// <c>mode_object</c> does, so the label matches the spoken clause at
    /// every count. Core exports <c>count_noun</c> — which GROUPS
    /// thousands — and not the bare agreement rule the ungrouped callers
    /// want, so the noun is taken off <c>count_noun</c>'s own answer and
    /// the number is formatted here. That is core's documented split
    /// ("callers that format the number themselves take [the bare noun]
    /// instead"), reached through the one export that exists;
    /// <c>TheModeValueAgreesWithCoresOwnModeSentence</c> pins the result
    /// against core at 1, 2 and 1,000.
    /// </remarks>
    private static string Label(CanvasModeObject @object) => @object switch
    {
        CanvasModeObject.Card card => $"\"{card.Title}\"",
        CanvasModeObject.Cards cards => $"{cards.Count} {CardNoun(cards.Count)}",
        _ => throw new UnreachableException(
            $"unknown canvas mode object {@object.GetType().Name}"),
    };

    private static string CardNoun(uint count)
    {
        string counted = SlateUniffiMethods.CountNoun(count, "card", "cards");
        int space = counted.LastIndexOf(' ');
        return space >= 0 ? counted[(space + 1)..] : counted;
    }
}
