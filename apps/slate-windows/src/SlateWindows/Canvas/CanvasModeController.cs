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
    /// of the shell — another pane, the sidebar, the menu bar.</summary>
    PaneFocus,

    /// <summary>The window itself lost activation.</summary>
    WindowDeactivated,

    /// <summary>Focus moved into a shell overlay that is layered OVER
    /// this tab — the command palette, Quick Open, the search overlay, a
    /// sheet. The canvas is still the tab underneath.</summary>
    ModalOverlay,
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
/// <param name="OnCommit">The commit side effect. Returns the
/// confirmation EVENT (t0 §1.3), or null to stay silent because the
/// action announces for itself.</param>
/// <param name="OnCancel">The cancel side effect (restore prior state).
/// Returns what was put back, which core phrases.</param>
internal sealed record CanvasModeSpec(
    CanvasMode Mode,
    CanvasModeObject Object,
    Func<CanvasA11yEvent?> OnCommit,
    Func<CanvasModeRestoration> OnCancel);

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

    /// <summary>M2 commit — Enter, the header's Commit button, or
    /// <c>slate.canvas.commitMode</c>. False when no mode was
    /// active.</summary>
    public bool Commit()
    {
        if (_active is not { } spec)
        {
            return false;
        }
        // Cleared BEFORE the side effect runs: a commit that announces
        // through a path which asks whether a mode is active (Where-am-I
        // does) must see the stack as it will be, not as it was.
        Active = null;
        if (spec.OnCommit() is { } confirmation)
        {
            _announce(confirmation);
        }
        return true;
    }

    /// <summary>M2 cancel — Escape's first rung, the header's Cancel
    /// button, or <c>slate.canvas.cancelMode</c>. False when no mode was
    /// active, which is what lets the Esc ladder fall through to the next
    /// rung.</summary>
    public bool Cancel()
    {
        if (_active is not { } spec)
        {
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
    /// <see cref="CanvasFocusDeparture.ModalOverlay"/> is the ONE arm
    /// that keeps the mode alive, and it is a recorded divergence from
    /// t0 §2 M4's literal list (contract C8, CD-41). t0 names the palette
    /// among the departures; the mac controller excludes it deliberately
    /// after red-team #521, because Commit Mode, Cancel Mode and the
    /// resize presets ARE palette commands — cancelling on palette open
    /// makes three registered verbs unreachable and contradicts M6's own
    /// "never depend on the keyboard-only path". The exclusion is one
    /// named arm of one total switch so the decision can be reversed in
    /// one place.
    /// </para>
    /// <para>
    /// Every other arm cancels. The switch is total over the enum, and
    /// <c>EveryFocusDepartureHasARecordedAnswer</c> enumerates the enum
    /// rather than restating the list.
    /// </para>
    /// </remarks>
    public bool HandleFocusDeparture(CanvasFocusDeparture departure)
    {
        bool cancels = departure switch
        {
            CanvasFocusDeparture.TabSwitch => true,
            CanvasFocusDeparture.PaneFocus => true,
            CanvasFocusDeparture.WindowDeactivated => true,
            CanvasFocusDeparture.ModalOverlay => false,
            _ => throw new UnreachableException(
                $"CanvasFocusDeparture.{departure} has no M4 answer. The mode "
                + "stack's focus-departure rule is a closed table (contract C7); "
                + "a new departure is a decision, not a default."),
        };
        return cancels && Cancel();
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
