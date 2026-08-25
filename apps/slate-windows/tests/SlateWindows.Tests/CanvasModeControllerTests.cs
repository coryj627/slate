// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// The M1–M7 conformance body, written ONCE so PR F re-runs it against
/// the real move, resize and connect modes rather than re-deriving the
/// contract (spec §PR C Tests: "the suite F reuses").
/// </summary>
/// <remarks>
/// <para>
/// The probe is the seam: a controller, a factory that produces a FRESH
/// spec each time (a mode may only be entered once), and the rendered
/// announcements the funnel actually posted. PR C supplies a test mode
/// whose commit and cancel record what ran; PR F supplies
/// <c>CanvasModes.Move()</c> and friends, and every assertion below
/// stays a statement about the STACK rather than about what a mode does.
/// </para>
/// <para>
/// The announcements are read as RENDERED TEXT out of the production
/// funnel, not as event objects: a machine that constructed the right
/// variant and rendered nothing would pass an object-level check, and
/// the whole point of M1 is what the user hears.
/// </para>
/// </remarks>
/// <param name="NewRefusingSpec">
/// A fresh spec whose commit effect REFUSES and announces its own reason
/// — PR C's test mode fakes the refusal, PR F supplies a real one (a
/// canvas that went degraded or lost its handle mid-mode).
/// </param>
internal sealed record CanvasModeProbe(
    CanvasModeController Controller,
    Func<CanvasModeSpec> NewSpec,
    Func<CanvasModeSpec> NewRefusingSpec,
    Func<IReadOnlyList<string>> Announced,
    Action Clear);

internal static class CanvasModeConformance
{
    /// <summary>M1: entry names the mode, the object and the exits.</summary>
    public static void EntryNamesTheModeTheObjectAndTheExits(CanvasModeProbe probe)
    {
        CanvasModeSpec spec = probe.NewSpec();
        probe.Clear();
        Assert.True(probe.Controller.Enter(spec));

        string line = Assert.Single(probe.Announced());
        // Every clause core's template promises, checked as CONTENT
        // rather than as one frozen string, so PR F's real modes satisfy
        // the same fact with their own object and exits.
        Assert.Equal(
            CanvasAnnouncer.RenderLabel(
                new CanvasA11yEvent.CanvasModeEntered(spec.Mode, spec.Object)),
            line);
        Assert.Contains("Escape to cancel", line, StringComparison.Ordinal);
        Assert.True(probe.Controller.IsActive);
    }

    /// <summary>M2: Return commits and the mode's own confirmation is
    /// what speaks; Escape cancels, restores, and says what came
    /// back.</summary>
    public static void CommitRunsTheEffectAndSpeaksItsConfirmation(CanvasModeProbe probe)
    {
        Assert.True(probe.Controller.Enter(probe.NewSpec()));
        probe.Clear();

        Assert.True(probe.Controller.Commit());
        Assert.False(probe.Controller.IsActive);
        Assert.NotEmpty(probe.Announced());
    }

    public static void CancelRestoresAndSaysWhatCameBack(CanvasModeProbe probe)
    {
        CanvasModeSpec spec = probe.NewSpec();
        Assert.True(probe.Controller.Enter(spec));
        probe.Clear();

        Assert.True(probe.Controller.Cancel());
        Assert.False(probe.Controller.IsActive);
        string line = Assert.Single(probe.Announced());
        Assert.Contains("cancelled", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// M2's other half: a REFUSED commit keeps the mode alive, with its
    /// transient state and its M3 value intact.
    /// </summary>
    /// <remarks>
    /// The machine was modelled as if a commit could not fail, and it
    /// can: the canvas goes degraded or loses its handle mid-mode and the
    /// funnel's admission says no. Mac keeps the mode up in that case and
    /// consumes the key with the refusal, so the user can fix the problem
    /// or cancel out with the restoration still available — the
    /// alternative loses their move with neither a commit nor a
    /// restoration. PR F's real modes commit through the funnel, which is
    /// exactly where the refusal comes from, so this arm is here now
    /// rather than after F discovers it.
    /// </remarks>
    public static void ARefusedCommitKeepsTheModeAlive(CanvasModeProbe probe)
    {
        Assert.True(probe.Controller.Enter(probe.NewRefusingSpec()));
        string? value = probe.Controller.ContainerValue;
        Assert.NotNull(value);
        probe.Clear();

        Assert.False(
            probe.Controller.Commit(),
            "a commit whose effect refused must report that it did not apply.");
        Assert.True(
            probe.Controller.IsActive,
            "a refused commit must KEEP the mode — dropping it loses the user's "
            + "transient state with neither a commit nor a restoration (t0 §2 M2).");
        Assert.Equal(value, probe.Controller.ContainerValue);
        Assert.True(
            probe.Controller.CanCommitOrCancel,
            "the mode is still running, so its visible controls stay live (M6).");
        // The effect owns the sentence — the controller has none for a
        // refusal — but the user is not left in silence.
        Assert.NotEmpty(probe.Announced());

        // And the mode is still cancellable, which is the escape hatch
        // the kept state exists for.
        probe.Clear();
        Assert.True(probe.Controller.Cancel());
        Assert.False(probe.Controller.IsActive);
    }

    /// <summary>M3: the mode is INSPECTABLE, not merely announced.</summary>
    public static void TheModeIsReadableFromTheContainerValue(CanvasModeProbe probe)
    {
        Assert.Null(probe.Controller.ContainerValue);
        CanvasModeSpec spec = probe.NewSpec();
        Assert.True(probe.Controller.Enter(spec));

        string value = Assert.IsType<string>(probe.Controller.ContainerValue);
        // Core's own mode sentence opens with "⟨Mode name⟩ — ⟨object⟩",
        // so the host-composed value is pinned against core rather than
        // against itself (contract C7).
        string spoken = CanvasAnnouncer.RenderLabel(
            new CanvasA11yEvent.CanvasModeEntered(spec.Mode, spec.Object));
        string[] head = spoken.Split(" — ", 2);
        Assert.Equal(2, head.Length);
        Assert.Equal($"{head[0]}: {head[1].Split(". ")[0]}", value);

        _ = probe.Controller.Cancel();
        Assert.Null(probe.Controller.ContainerValue);
    }

    /// <summary>
    /// M4: a focus departure cancels with restoration and an announcement
    /// — every arm but the two the shell layers OVER the tab (contract
    /// C8, CD-41).
    /// </summary>
    /// <remarks>
    /// The keep-alive set is named here and the enum is ENUMERATED, so a
    /// new departure joins one side or the other by decision rather than
    /// by falling into whichever branch it happens to hit. Both members
    /// exist for one reason: the mode-lifecycle verbs live on surfaces
    /// that take keyboard focus without the reader leaving the canvas —
    /// the palette, and the menus (this shell's Canvas menu carries
    /// Commit Mode and Cancel Mode; PR E/F's context menus carry every
    /// mode verb).
    /// </remarks>
    public static void EveryFocusDepartureHasARecordedAnswer(CanvasModeProbe probe)
    {
        CanvasFocusDeparture[] keepAlive =
        [
            CanvasFocusDeparture.ModalOverlay,
            CanvasFocusDeparture.MenuOpen,
        ];

        foreach (CanvasFocusDeparture departure
            in Enum.GetValues<CanvasFocusDeparture>())
        {
            Assert.True(probe.Controller.Enter(probe.NewSpec()));
            probe.Clear();

            bool cancelled = probe.Controller.HandleFocusDeparture(departure);
            if (keepAlive.Contains(departure))
            {
                Assert.True(
                    probe.Controller.IsActive,
                    $"{departure} is layered OVER the canvas tab and must keep the "
                    + "mode alive — the mode-lifecycle verbs live there, so "
                    + "cancelling on it kills the controls that would have ended "
                    + "the mode (contract C8).");
                Assert.False(cancelled);
                Assert.Empty(probe.Announced());
                _ = probe.Controller.Cancel();
                continue;
            }

            Assert.True(cancelled, $"{departure} must cancel the mode (t0 §2 M4).");
            Assert.False(probe.Controller.IsActive);
            Assert.NotEmpty(probe.Announced());
        }
    }

    /// <summary>M7: a second entry is rejected, names the active mode,
    /// and commits nothing.</summary>
    public static void ASecondEntryIsRejectedAndCommitsNothing(CanvasModeProbe probe)
    {
        CanvasModeSpec first = probe.NewSpec();
        Assert.True(probe.Controller.Enter(first));
        probe.Clear();

        Assert.False(probe.Controller.Enter(probe.NewSpec()));
        Assert.Equal(
            CanvasAnnouncer.RenderLabel(
                new CanvasA11yEvent.CanvasModeRejected(first.Mode)),
            Assert.Single(probe.Announced()));
        Assert.Same(first, probe.Controller.Active);
    }
}

/// <summary>
/// W6-1 PR C (#745): t0 §2 M1–M7 and the M5 Escape ladder, against a
/// TEST mode. Contracts C7 (the machine), C8 (the M4 table) and C6 (the
/// ladder).
/// </summary>
public sealed class CanvasModeControllerTests
{
    private readonly List<RenderedAnnouncement> _announced = [];

    /// <summary>
    /// The test mode. It records what its commit and cancel did, which is
    /// the whole of what M1–M7 needs from a mode — the real ones arrive
    /// in PR F and run the same conformance body.
    /// </summary>
    private sealed class TestMode
    {
        public bool Committed { get; private set; }

        public bool Cancelled { get; private set; }

        public bool Refused { get; private set; }

        /// <summary>
        /// A fresh spec for <paramref name="mode"/> — and its commit and
        /// cancel HONOUR that mode rather than always speaking Move's.
        /// </summary>
        /// <remarks>
        /// PR F reuses this file, so a spec that ignored its parameter
        /// would have every mode's conformance run against Move's
        /// confirmation and Move's restoration, and the first real
        /// difference would surface as an F defect rather than here.
        /// Connect has no `CanvasTransientVerb` because it commits no
        /// geometry — its confirmation is the connection it made, which
        /// is also why its restoration is `Unstated`.
        /// </remarks>
        public CanvasModeSpec Spec(CanvasMode mode = CanvasMode.Move) =>
            new(
                mode,
                new CanvasModeObject.Card("Research"),
                () =>
                {
                    Committed = true;
                    return CanvasModeCommitResult.Committed(mode switch
                    {
                        CanvasMode.Move => new CanvasA11yEvent.CanvasModeCommitted(
                            CanvasTransientVerb.Move,
                            new CanvasModeObject.Card("Research")),
                        CanvasMode.Resize => new CanvasA11yEvent.CanvasModeCommitted(
                            CanvasTransientVerb.Resize,
                            new CanvasModeObject.Card("Research")),
                        _ => new CanvasA11yEvent.CanvasConnected(
                            "Research", "Evidence", null),
                    });
                },
                () =>
                {
                    Cancelled = true;
                    return mode switch
                    {
                        CanvasMode.Move => new CanvasModeRestoration.BackAt("Research"),
                        CanvasMode.Resize => new CanvasModeRestoration.SizeRestored(),
                        _ => new CanvasModeRestoration.Unstated(),
                    };
                });

        /// <summary>
        /// A spec whose commit REFUSES and speaks its own reason — the
        /// shape PR F's real modes take when the funnel's admission says
        /// no (a canvas that went degraded or lost its handle mid-mode).
        /// </summary>
        /// <remarks>
        /// The refusal is announced by the EFFECT, through the same
        /// funnel the controller uses, because which refusal it is is the
        /// effect's knowledge — the controller has no sentence for it.
        /// `CanvasMutationRefused` is the vocabulary's own arm for
        /// exactly this, so no new copy is invented for a test.
        /// </remarks>
        public CanvasModeSpec RefusingSpec(
            CanvasMode mode, Action<CanvasA11yEvent> announce) =>
            new(
                mode,
                new CanvasModeObject.Card("Research"),
                () =>
                {
                    Refused = true;
                    announce(new CanvasA11yEvent.CanvasMutationRefused(
                        CanvasMutationRefusal.ReadOnly));
                    return CanvasModeCommitResult.Refused();
                },
                () =>
                {
                    Cancelled = true;
                    return new CanvasModeRestoration.BackAt("Research");
                });
    }

    private CanvasModeController NewController() =>
        new(new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)).Announce);

    private CanvasModeProbe NewProbe(CanvasMode mode = CanvasMode.Move)
    {
        var test = new TestMode();
        CanvasModeController controller = NewController();
        var announcer = new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1));
        return new CanvasModeProbe(
            controller,
            () => test.Spec(mode),
            () => test.RefusingSpec(mode, announcer.Announce),
            () => _announced.Select(line => line.Text).ToArray(),
            _announced.Clear);
    }

    /// <summary>Every mode the vocabulary knows, ENUMERATED — a new one
    /// joins these facts without anyone remembering to add it.</summary>
    public static TheoryData<CanvasMode> EveryMode
    {
        get
        {
            var data = new TheoryData<CanvasMode>();
            foreach (CanvasMode mode in Enum.GetValues<CanvasMode>())
            {
                data.Add(mode);
            }
            return data;
        }
    }

    // --- M1–M7, over every mode the vocabulary knows ---------------------

    [Theory]
    [MemberData(nameof(EveryMode))]
    public void M1_EntryNamesTheModeTheObjectAndTheExits(CanvasMode mode) =>
        CanvasModeConformance.EntryNamesTheModeTheObjectAndTheExits(NewProbe(mode));

    [Theory]
    [MemberData(nameof(EveryMode))]
    public void M2_CommitRunsTheEffectAndSpeaksItsConfirmation(CanvasMode mode) =>
        CanvasModeConformance.CommitRunsTheEffectAndSpeaksItsConfirmation(NewProbe(mode));

    [Theory]
    [MemberData(nameof(EveryMode))]
    public void M2_CancelRestoresAndSaysWhatCameBack(CanvasMode mode) =>
        CanvasModeConformance.CancelRestoresAndSaysWhatCameBack(NewProbe(mode));

    [Theory]
    [MemberData(nameof(EveryMode))]
    public void M3_TheModeIsReadableFromTheContainerValue(CanvasMode mode) =>
        CanvasModeConformance.TheModeIsReadableFromTheContainerValue(NewProbe(mode));

    [Theory]
    [MemberData(nameof(EveryMode))]
    public void M2_ARefusedCommitKeepsTheModeAlive(CanvasMode mode) =>
        CanvasModeConformance.ARefusedCommitKeepsTheModeAlive(NewProbe(mode));

    [Theory]
    [MemberData(nameof(EveryMode))]
    public void M4_EveryFocusDepartureHasARecordedAnswer(CanvasMode mode) =>
        CanvasModeConformance.EveryFocusDepartureHasARecordedAnswer(NewProbe(mode));

    [Theory]
    [MemberData(nameof(EveryMode))]
    public void M7_ASecondEntryIsRejectedAndCommitsNothing(CanvasMode mode) =>
        CanvasModeConformance.ASecondEntryIsRejectedAndCommitsNothing(NewProbe(mode));

    /// <summary>
    /// M2's side effects actually RAN — the conformance body asserts what
    /// the user hears, and this asserts the mode's own work happened, so
    /// neither stands in for the other.
    /// </summary>
    [Fact]
    public void CommitAndCancelRunTheModesOwnEffect()
    {
        var test = new TestMode();
        CanvasModeController controller = NewController();

        Assert.True(controller.Enter(test.Spec()));
        Assert.True(controller.Commit());
        Assert.True(test.Committed);
        Assert.False(test.Cancelled);

        Assert.True(controller.Enter(test.Spec()));
        Assert.True(controller.Cancel());
        Assert.True(test.Cancelled);
    }

    /// <summary>
    /// M2 with no mode active is not a silent success: the transitions
    /// report FALSE, which is what lets the Escape ladder fall through to
    /// its next rung and what makes the palette rows disable (contract
    /// C9).
    /// </summary>
    [Fact]
    public void CommitAndCancelReportFalseWithNoActiveMode()
    {
        CanvasModeController controller = NewController();
        Assert.False(controller.Commit());
        Assert.False(controller.Cancel());
        Assert.False(controller.CanCommitOrCancel);
        Assert.Empty(_announced);
    }

    /// <summary>
    /// M3's value uses core's own <c>mode_object</c> arithmetic for a
    /// SET, ungrouped, at the singular boundary and past the thousands
    /// separator — the two places a host-side pluralisation drifts.
    /// </summary>
    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(1000u)]
    public void TheModeValueAgreesWithCoresOwnModeSentence(uint count)
    {
        CanvasModeController controller = NewController();
        var @object = new CanvasModeObject.Cards(count);
        Assert.True(controller.Enter(new CanvasModeSpec(
            CanvasMode.Move,
            @object,
            () => CanvasModeCommitResult.Committed(),
            () => new CanvasModeRestoration.CardsReturned(count))));

        string spoken = CanvasAnnouncer.RenderLabel(
            new CanvasA11yEvent.CanvasModeEntered(CanvasMode.Move, @object));
        string objectClause = spoken.Split(" — ", 2)[1].Split(". ")[0];
        Assert.EndsWith(
            $": {objectClause}",
            Assert.IsType<string>(controller.ContainerValue),
            StringComparison.Ordinal);
    }

    // --- M5: the Escape ladder -------------------------------------------

    /// <summary>
    /// The ladder table (t0 §2 M5). Each row is a state and the rung one
    /// Escape press consumes in it, INNERMOST FIRST — and the answer is
    /// the rung's NAME, so "exactly one rung per press" is checked rather
    /// than asserted.
    /// </summary>
    private static readonly (bool Mode, bool Filter, bool Transient, CanvasEscapeRung Rung)[]
        Ladder =
        [
            (true, true, true, CanvasEscapeRung.Mode),
            (true, false, false, CanvasEscapeRung.Mode),
            (false, true, true, CanvasEscapeRung.Filter),
            (false, true, false, CanvasEscapeRung.Filter),
            (false, false, true, CanvasEscapeRung.Surface),
            (false, false, false, CanvasEscapeRung.WorkspaceTab),
        ];

    /// <summary>
    /// One <c>[Fact]</c> over the table rather than a <c>[Theory]</c>:
    /// <see cref="CanvasEscapeRung"/> is internal and a public theory
    /// parameter cannot name it. Each row carries itself into the
    /// assertion message, so a failure still says which state it was in.
    /// </summary>
    [Fact]
    public void TheEscapeLadderConsumesExactlyOneRungPerPress()
    {
        foreach ((bool mode, bool filter, bool transient, CanvasEscapeRung rung) in Ladder)
        {
            AssertLadderRow(mode, filter, transient, rung);
        }

        // The table covers every rung the ladder can answer with, so a
        // new rung cannot slip in unexercised.
        Assert.Equal(
            Enum.GetValues<CanvasEscapeRung>().ToHashSet(),
            Ladder.Select(row => row.Rung).ToHashSet());
    }

    private void AssertLadderRow(
        bool modeActive, bool filterActive, bool transientOpen, CanvasEscapeRung expected)
    {
        _announced.Clear();
        CanvasModeController controller = NewController();
        var consumed = new List<CanvasEscapeRung>();
        bool filter = filterActive;
        bool transient = transientOpen;
        controller.RegisterRung(
            CanvasEscapeRung.Filter,
            () =>
            {
                if (!filter)
                {
                    return false;
                }
                filter = false;
                consumed.Add(CanvasEscapeRung.Filter);
                return true;
            });
        controller.RegisterRung(
            CanvasEscapeRung.Surface,
            () =>
            {
                if (!transient)
                {
                    return false;
                }
                transient = false;
                consumed.Add(CanvasEscapeRung.Surface);
                return true;
            });
        if (modeActive)
        {
            Assert.True(controller.Enter(new TestMode().Spec()));
        }

        string row =
            $"mode={modeActive} filter={filterActive} transient={transientOpen}";
        Assert.True(
            expected == controller.HandleEscape(),
            $"the ladder answered the wrong rung for [{row}]; expected {expected}.");

        // Exactly ONE rung ran, and the ones BELOW it are untouched —
        // which is the half a "did the right rung fire" check misses.
        switch (expected)
        {
            case CanvasEscapeRung.Mode:
                Assert.Empty(consumed);
                Assert.False(controller.IsActive);
                Assert.Equal(filterActive, filter);
                Assert.Equal(transientOpen, transient);
                break;
            case CanvasEscapeRung.WorkspaceTab:
                Assert.Empty(consumed);
                break;
            default:
                Assert.Equal([expected], consumed);
                Assert.False(controller.IsActive);
                break;
        }
    }

    /// <summary>
    /// The rungs are held in ladder order whatever order they were
    /// registered in, and neither end of the ladder is registrable: rung
    /// 1 is the stack's own cancel and rung 4 is the un-consumed answer.
    /// </summary>
    [Fact]
    public void TheLadderIsOrderedByRungNotByRegistrationOrder()
    {
        CanvasModeController controller = NewController();
        controller.RegisterRung(CanvasEscapeRung.Surface, () => false);
        controller.RegisterRung(CanvasEscapeRung.Filter, () => false);
        Assert.Equal(
            [CanvasEscapeRung.Filter, CanvasEscapeRung.Surface],
            controller.RegisteredRungs);

        Assert.Throws<ArgumentException>(
            () => controller.RegisterRung(CanvasEscapeRung.Filter, () => false));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => controller.RegisterRung(CanvasEscapeRung.Mode, () => false));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => controller.RegisterRung(CanvasEscapeRung.WorkspaceTab, () => false));
    }
}
