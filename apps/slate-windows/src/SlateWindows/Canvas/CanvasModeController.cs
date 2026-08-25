// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using uniffi.slate_uniffi;

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
    /// Where-am-I panel, the interim card detail, the filter field
    /// holding focus with nothing to clear) hands focus back to the
    /// projection.</summary>
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
    Func<CanvasModeRestoration> OnCancel);

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
internal readonly record struct CanvasModeCommitResult(
    bool Applied, CanvasA11yEvent? Confirmation)
{
    /// <summary>The effect applied. <paramref name="confirmation"/> is
    /// the t0 §1.3 sentence, or null when the action announces for
    /// itself.</summary>
    internal static CanvasModeCommitResult Committed(
        CanvasA11yEvent? confirmation = null) => new(true, confirmation);

    /// <summary>The effect REFUSED: the mode and its transient state
    /// survive, and the effect has already said why.</summary>
    internal static CanvasModeCommitResult Refused() => new(false, null);
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

    /// <summary>True while a commit effect is running — the window in
    /// which the stack must not be transitioned by anybody else (M2's
    /// one outcome).</summary>
    private bool _committing;

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

    /// <summary>The active mode, or null. Published so the surface's M3
    /// value and its Commit/Cancel buttons follow it.</summary>
    public CanvasModeSpec? Active
    {
        get => _active;
        private set
        {
            if (SetField(ref _active, value))
            {
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
    public bool CanCommitOrCancel => IsActive;

    /// <summary>
    /// M1 + M7. Returns false when the entry was REJECTED because a mode
    /// is already active — nothing commits, and the announcement names
    /// the mode that is holding the stack.
    /// </summary>
    public bool Enter(CanvasModeSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (_active is { } current)
        {
            _announce(new CanvasA11yEvent.CanvasModeRejected(current.Mode));
            return false;
        }
        Active = spec;
        _announce(new CanvasA11yEvent.CanvasModeEntered(spec.Mode, spec.Object));
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
        if (_active is not { } spec || _committing)
        {
            return false;
        }
        // THE TRANSITION IS CLOSED while the effect runs (t0 §2 M2's one
        // outcome). The effect is arbitrary host code — it opens a sheet,
        // it moves focus, it lets the shell switch tabs — and any of that
        // reaches this controller SYNCHRONOUSLY. Without the guard a
        // departure raised from inside the effect re-entered `Cancel`,
        // cleared the stack, announced a cancellation, and then this
        // method carried on and announced the confirmation too: two
        // outcomes for one press, and a final state that is neither.
        _committing = true;
        CanvasModeCommitResult result;
        try
        {
            // The effect runs FIRST and the stack is cleared only if it
            // APPLIED: a refused commit keeps the mode and its transient
            // state, so the user can fix what refused it or cancel out
            // with the restoration intact. Clearing first would drop a
            // move that was never made, with neither a commit nor a
            // restoration to show for it.
            result = spec.OnCommit();
        }
        catch
        {
            // An effect that THREW applied nothing, which is the refused
            // case by another name — so the mode is retained by rule, and
            // the departure it provoked before failing is honoured here,
            // BEFORE the exception propagates. Draining in a `finally`
            // after the throw is not equivalent: the caller may be gone
            // by then, and a departure left in the slot would cancel a
            // LATER commit's mode instead of this one's.
            _committing = false;
            DrainDeferredDeparture();
            throw;
        }
        // Reopened before the outcome is applied: a controller stuck in
        // the transition would refuse every later commit and cancel,
        // which is a worse failure than the one being guarded.
        _committing = false;
        if (result.Applied)
        {
            // Cleared BEFORE the confirmation is announced: a sentence
            // that asks whether a mode is active (Where-am-I does) must
            // see the stack as it will be, not as it was.
            Active = null;
            if (result.Confirmation is { } confirmation)
            {
                _announce(confirmation);
            }
        }
        // A refusal is silent HERE, not silent to the user: which refusal
        // it is is the effect's knowledge, so the effect said it.
        //
        // Now the departure the effect provoked applies to the RESULT,
        // which is the M4-correct order: a commit that APPLIED leaves no
        // mode for it to cancel and it is moot, while a REFUSED commit
        // keeps one — and a mode may not survive a focus departure, so it
        // cancels, with its restoration, after the single commit outcome
        // has been spoken.
        DrainDeferredDeparture();
        return result.Applied;
    }

    /// <summary>
    /// Apply a departure held across a commit, if there is one. Drained
    /// on EVERY exit from the transition — applied, refused, thrown, and
    /// teardown — because a slot that survives its commit cancels the
    /// NEXT one's mode instead.
    /// </summary>
    private void DrainDeferredDeparture()
    {
        if (_deferredDeparture is not { } departure)
        {
            return;
        }
        _deferredDeparture = null;
        _ = HandleFocusDeparture(departure);
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
        DrainDeferredDeparture();
        _ = HandleFocusDeparture(CanvasFocusDeparture.TabSwitch);
        _deferredDeparture = null;
    }

    /// <summary>M2 cancel — Escape's first rung, the header's Cancel
    /// button, or <c>slate.canvas.cancelMode</c>. False when no mode was
    /// active, which is what lets the Esc ladder fall through to the next
    /// rung.</summary>
    public bool Cancel()
    {
        if (_active is not { } spec || _committing)
        {
            // Refused rather than deferred while a commit is in flight:
            // a direct cancel from inside a commit effect is a caller
            // error, not a race, and the commit already owns this press's
            // outcome. The DEPARTURE path defers instead, because M4 must
            // still be honoured once the outcome is known.
            return false;
        }
        Active = null;
        _announce(new CanvasA11yEvent.CanvasModeCancelled(spec.Mode, spec.OnCancel()));
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
        if (_committing && CancelsFor(departure))
        {
            // DEFERRED, not applied: a commit effect is mid-flight and it
            // owns this press's outcome (M2). The departure is honoured
            // the moment that outcome is known — see `Commit`.
            _deferredDeparture = departure;
            return false;
        }
        return CancelsFor(departure) && Cancel();
    }

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
