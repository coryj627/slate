// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 PR A (#745), contract A5: what the HOST still owns after PR 0a
/// — the class-keyed coalescing window (t0 §1.5), the error
/// flush-and-drop rule, and the priority relay. The GRAMMAR is pinned in
/// <c>slate_core::a11y</c> and asserted through the real FFI by
/// <c>A11yCorpusCensus</c>; nothing here holds a second copy of it.
/// </summary>
public sealed class CanvasAnnouncerTests
{
    private readonly List<RenderedAnnouncement> _posted = [];

    /// <summary>A long window plus <c>FlushForTests</c> keeps coalescing
    /// deterministic without a wall-clock wait (the mac shape).</summary>
    private CanvasAnnouncer NewAnnouncer() =>
        new(_posted.Add, TimeSpan.FromMinutes(1));

    /// <summary>A terse move renders to the bare title — the cheapest
    /// event whose text names the step it came from.</summary>
    private static CanvasA11yEvent MovedTo(string title) =>
        new CanvasA11yEvent.CanvasMovedTo(
            CanvasVerbosity.Terse, "text", title, 1, 5, null, 0, null, false);

    private static string Render(CanvasA11yEvent @event) =>
        SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(@event)).Text;

    [Fact]
    public void CoalescingCollapsesRapidNavigationAndTheFinalStateWins()
    {
        CanvasAnnouncer announcer = NewAnnouncer();
        // A held arrow: five rapid moves — exactly one post, the LAST.
        foreach (string title in new[] { "A", "B", "C", "D", "E" })
        {
            announcer.Announce(MovedTo(title));
        }
        Assert.Empty(_posted);

        announcer.FlushForTests();
        RenderedAnnouncement spoken = Assert.Single(_posted);
        Assert.Equal(Render(MovedTo("E")), spoken.Text);
        Assert.Equal(A11yPriority.Medium, spoken.Priority);

        // Confirmations are immediate, never debounced.
        var created = new CanvasA11yEvent.CanvasCreated(
            "text", "New idea", new CanvasRelativeDesc.Below("E"));
        announcer.Announce(created);
        Assert.Equal(2, _posted.Count);
        Assert.Equal(Render(created), _posted[^1].Text);
    }

    [Fact]
    public void TheTwoClassesCoalesceIndependently()
    {
        CanvasAnnouncer announcer = NewAnnouncer();
        announcer.Announce(MovedTo("A"));
        announcer.Announce(new CanvasA11yEvent.CanvasFilterCount(2));
        announcer.Announce(new CanvasA11yEvent.CanvasFilterCount(3));
        Assert.Empty(_posted);

        announcer.FlushForTests();
        // A filter burst must not cancel a pending navigation line
        // (contract 0a-8: each class is independent).
        Assert.Equal(
            new HashSet<string>(
                [
                    Render(MovedTo("A")),
                    Render(new CanvasA11yEvent.CanvasFilterCount(3)),
                ],
                StringComparer.Ordinal),
            _posted.Select(line => line.Text).ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public void AnErrorIsAssertiveAndDropsPendingNavigationRatherThanFlushingIt()
    {
        CanvasAnnouncer announcer = NewAnnouncer();
        announcer.Announce(MovedTo("Research"));
        announcer.Announce(new CanvasA11yEvent.CanvasSaveConflict());

        RenderedAnnouncement spoken = Assert.Single(_posted);
        Assert.Equal(A11yPriority.High, spoken.Priority);
        Assert.Equal(Render(new CanvasA11yEvent.CanvasSaveConflict()), spoken.Text);

        // t0 §1.5: navigation context is re-derivable by moving again,
        // so the superseded line never resurfaces.
        announcer.FlushForTests();
        Assert.Single(_posted);
    }

    [Fact]
    public void TheRelayCarriesTheCorePriorityOfANonCanvasEvent()
    {
        CanvasAnnouncer announcer = NewAnnouncer();
        announcer.Relay(new A11yEvent.GridSorted("Status", true));
        announcer.Relay(new A11yEvent.CommandPaletteNeedsVault());

        Assert.Equal(2, _posted.Count);
        Assert.Equal(A11yPriority.Medium, _posted[0].Priority);
        // PR B routes the shared grid's own events through this funnel;
        // unwrapping the text and re-wrapping it as a status would
        // silently demote every assertive grid event to polite.
        Assert.Equal(A11yPriority.High, _posted[1].Priority);
        Assert.Equal(
            SlateUniffiMethods.A11yRender(
                new A11yEvent.CommandPaletteNeedsVault()).Text,
            _posted[1].Text);
    }

    [Fact]
    public void ARelayedHighEventAlsoDropsPendingNavigation()
    {
        CanvasAnnouncer announcer = NewAnnouncer();
        announcer.Announce(MovedTo("Research"));
        announcer.Relay(new A11yEvent.CommandPaletteNeedsVault());

        Assert.Single(_posted);
        announcer.FlushForTests();
        Assert.Single(_posted);
    }

    /// <summary>§H TH-7 (H6, IH-44): the label class's helpers have exact
    /// outputs — the container clause, the marked suffix, the picker row's
    /// label over the card reference — and <c>RowStatus</c> is composed
    /// from the same clauses, so the three sinks cannot drift.</summary>
    [Fact]
    public void TheLabelClassHelpersComposeTheStatusAndThePickerRow()
    {
        Assert.Equal("canvas", CanvasPhrase.ContainerClause(null));
        Assert.Equal("Q3", CanvasPhrase.ContainerClause("Q3"));
        Assert.Null(CanvasPhrase.ContainerOf([]));
        Assert.Equal("Inner", CanvasPhrase.ContainerOf(["Outer", "Inner"]));
        Assert.Equal(", marked", CanvasPhrase.MarkedSuffix);
        Assert.Equal(
            "2 of 5 in " + CanvasPhrase.ContainerClause("Q3") + ", red" + CanvasPhrase.MarkedSuffix,
            CanvasPhrase.RowStatus(2, 5, "Q3", "red", marked: true));
        Assert.Equal("1 of 1 in canvas", CanvasPhrase.RowStatus(1, 1, null, null, marked: false));
        Assert.Equal(
            CanvasPhrase.CardReference("text", "Alpha") + ", in Research",
            CanvasPhrase.PickerRowLabel("text", "Alpha", ["Research"]));
        Assert.Equal(
            CanvasPhrase.CardReference("group", "Zone") + ", in canvas",
            CanvasPhrase.PickerRowLabel("group", "Zone", []));
    }

    /// <summary>
    /// Contract A9/A10 drift control. <c>CanvasPhrase.CardReference</c>
    /// and <c>CanvasPhrase.RowStatus</c> are host LABEL-class copies of
    /// clauses core also composes — <c>a11y.rs::card_ref</c> and the
    /// positional clause of <c>CanvasMovedTo</c> — because no exported
    /// accessor renders either on its own (0a-10). A copy nothing
    /// compares is a copy that drifts, so this pins them against core's
    /// OWN rendering of the same data.
    /// </summary>
    /// <remarks>
    /// At Standard, <c>CanvasMovedTo</c> renders exactly
    /// <c>⟨card⟩, ⟨n⟩ of ⟨m⟩ in ⟨container‖canvas⟩</c> — which is
    /// <c>CardReference</c> followed by <c>RowStatus</c> with both
    /// optional clauses absent, so the assertion is a full equality and
    /// a core wording change to "card", to the quoting, to " of ", to
    /// " in ", or to the comma joining fails here. Verbose adds a
    /// connections clause the outline's status slot deliberately omits
    /// (t0 §3 gives the row position, colour and marked state), so its
    /// two ends are pinned instead: the head is the card reference and
    /// the tail is the colour and marked clauses RowStatus spells.
    /// </remarks>
    [Theory]
    [InlineData("text")]
    [InlineData("file")]
    [InlineData("image")]
    [InlineData("link")]
    [InlineData("group")]
    public void TheCardReferenceMatchesCoresOwnComposition(string kind)
    {
        const string title = "Research";
        const string container = "Q3";
        const uint ordinalN = 2;
        const uint totalM = 5;

        string standard = Render(new CanvasA11yEvent.CanvasMovedTo(
            CanvasVerbosity.Standard, kind, title, ordinalN, totalM, container,
            ConnectionCount: 3, ColorName: "red", Marked: true));
        Assert.Equal(
            CanvasPhrase.CardReference(kind, title)
            + ", "
            + CanvasPhrase.RowStatus(
                ordinalN, totalM, container, colorName: null, marked: false),
            standard);

        string verbose = Render(new CanvasA11yEvent.CanvasMovedTo(
            CanvasVerbosity.Verbose, kind, title, ordinalN, totalM, container,
            ConnectionCount: 3, ColorName: "red", Marked: true));
        Assert.StartsWith(
            CanvasPhrase.CardReference(kind, title) + ", ",
            verbose,
            StringComparison.Ordinal);
        // The colour and marked clauses, in core's order and spelling.
        Assert.EndsWith(", red, marked", verbose, StringComparison.Ordinal);
        Assert.EndsWith(
            ", red, marked",
            CanvasPhrase.RowStatus(
                ordinalN, totalM, container, colorName: "red", marked: true),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The container fallback is core's word, not the host's guess: an
    /// ungrouped card reads "in canvas" on both sides.
    /// </summary>
    [Fact]
    public void TheRowStatusFallbackContainerMatchesCore()
    {
        string standard = Render(new CanvasA11yEvent.CanvasMovedTo(
            CanvasVerbosity.Standard, "text", "Loose", 1, 4, Container: null,
            ConnectionCount: 0, ColorName: null, Marked: false));
        Assert.Equal(
            CanvasPhrase.CardReference("text", "Loose")
            + ", "
            + CanvasPhrase.RowStatus(1, 4, null, null, false),
            standard);
    }

    /// <summary>
    /// The REAL coalescer, with no <c>FlushForTests</c> anywhere: a
    /// pending navigation line fires by itself, on the dispatcher the
    /// announcer captured at construction.
    /// </summary>
    /// <remarks>
    /// Every other fact in this suite flushes manually, so all of them
    /// stay green with <c>_timer.Start()</c> deleted — while in
    /// production every coalesced line silently never fires. That is the
    /// W4-5 lesson ("test the mode users run") in its exact shape, and
    /// this is the one fact that runs the production timer.
    ///
    /// <para>
    /// <b>What the mutation check proves, exactly.</b> Deleting
    /// <c>_timer.Start()</c> fails this fact and
    /// <c>AFiredClassStartsEmptyForTheNextBurst</c>, and nothing else —
    /// verified. It does NOT independently prove the constructor's
    /// dispatcher capture: this body runs on the same STA thread that
    /// constructs the announcer, so a reverted capture would land on
    /// that thread anyway and stay green. The capture's real failure
    /// mode is a first announcement arriving from a scheduler body, and
    /// nothing here reproduces it; what protects it is the four-argument
    /// <c>DispatcherTimer</c> ctor making the wrong binding
    /// unexpressible, plus the Debug assert. Said plainly rather than
    /// folded into "both ways".
    /// </para>
    /// </remarks>
    [Fact]
    public void APendingNavigationLineFiresOnItsOwnWithoutAFlush() => RunSta(() =>
    {
        var posted = new List<RenderedAnnouncement>();
        var announcer = new CanvasAnnouncer(posted.Add, TimeSpan.FromMilliseconds(20));

        announcer.Announce(MovedTo("A"));
        announcer.Announce(MovedTo("B"));
        Assert.Empty(posted);

        PumpUntil(() => posted.Count > 0, TimeSpan.FromSeconds(5));

        // Latest-wins, and exactly once — the held-arrow resting
        // position of t0 §1.5.
        RenderedAnnouncement spoken = Assert.Single(posted);
        Assert.Equal(Render(MovedTo("B")), spoken.Text);
    });

    /// <summary>
    /// And the class is drained by firing: a second burst after the
    /// window elapsed is a new line, not a re-post of the old one.
    /// </summary>
    [Fact]
    public void AFiredClassStartsEmptyForTheNextBurst() => RunSta(() =>
    {
        var posted = new List<RenderedAnnouncement>();
        var announcer = new CanvasAnnouncer(posted.Add, TimeSpan.FromMilliseconds(20));

        announcer.Announce(MovedTo("A"));
        PumpUntil(() => posted.Count > 0, TimeSpan.FromSeconds(5));
        announcer.Announce(MovedTo("B"));
        PumpUntil(() => posted.Count > 1, TimeSpan.FromSeconds(5));

        Assert.Equal(
            [Render(MovedTo("A")), Render(MovedTo("B"))],
            posted.Select(line => line.Text).ToArray());
    });

    /// <summary>§F TF-6 (F11, IF-32 reconciled): a mode transition
    /// retires the pending NAVIGATION line — a cancel is never
    /// followed by a now-false position sentence.</summary>
    [Fact]
    public void ACancelRetiresThePendingNavigationLine() => RunSta(() =>
    {
        var posted = new List<RenderedAnnouncement>();
        var announcer = new CanvasAnnouncer(posted.Add, TimeSpan.FromMinutes(1));

        announcer.Announce(MovedTo("A"));
        announcer.Announce(new CanvasA11yEvent.CanvasModeCancelled(
            CanvasMode.Move, new CanvasModeRestoration.Unstated()));
        announcer.FlushForTests();

        Assert.DoesNotContain(posted, line => line.Text == Render(MovedTo("A")));
        Assert.Contains(
            posted,
            line => line.Text == Render(new CanvasA11yEvent.CanvasModeCancelled(
                CanvasMode.Move, new CanvasModeRestoration.Unstated())));
    });

    /// <summary>§F TF-6 (IF-32): FILTER SURVIVES — pending filter
    /// feedback is not made false by a mode ending; only Navigation
    /// retires.</summary>
    [Fact]
    public void AFilterLineSurvivesTheTransition() => RunSta(() =>
    {
        var posted = new List<RenderedAnnouncement>();
        var announcer = new CanvasAnnouncer(posted.Add, TimeSpan.FromMinutes(1));

        announcer.Announce(new CanvasA11yEvent.CanvasFilterCount(3));
        announcer.Announce(new CanvasA11yEvent.CanvasModeEntered(
            CanvasMode.Move, new CanvasModeObject.Card("A")));
        announcer.FlushForTests();

        Assert.Contains(
            posted,
            line => line.Text == Render(new CanvasA11yEvent.CanvasFilterCount(3)));
    });

    /// <summary>§F TF-6 (F11): the clamp is a transition too — a
    /// stale geometry line after the refusal would state a size the
    /// step never took.</summary>
    [Fact]
    public void TheClampRetiresTheGeometryLine() => RunSta(() =>
    {
        var posted = new List<RenderedAnnouncement>();
        var announcer = new CanvasAnnouncer(posted.Add, TimeSpan.FromMinutes(1));

        announcer.Announce(new CanvasA11yEvent.CanvasResizeGeometry(
            null, 40, 60, null));
        announcer.Announce(new CanvasA11yEvent.CanvasResizeClamped());
        announcer.FlushForTests();

        Assert.DoesNotContain(
            posted,
            line => line.Text == Render(new CanvasA11yEvent.CanvasResizeGeometry(
                null, 40, 60, null)));
        Assert.Contains(
            posted,
            line => line.Text == Render(new CanvasA11yEvent.CanvasResizeClamped()));
    });

    /// <summary>§F TF-6 (F11): the retirement is the TRANSITION'S
    /// — an ordinary Medium immediate event leaves the queued
    /// position line alone, still true and still firing. (A
    /// REJECTION also leaves the mode unchanged, but renders High
    /// and so drops pending lines through t0's frozen assertive
    /// rule — a different law, recorded, not this one.)</summary>
    [Fact]
    public void AnOrdinaryImmediateEventRetiresNothing() => RunSta(() =>
    {
        var posted = new List<RenderedAnnouncement>();
        var announcer = new CanvasAnnouncer(posted.Add, TimeSpan.FromMinutes(1));

        announcer.Announce(MovedTo("A"));
        announcer.Announce(new CanvasA11yEvent.CanvasStatus(
            new CanvasStatusNote.NothingSelected()));
        announcer.FlushForTests();

        Assert.Contains(posted, line => line.Text == Render(MovedTo("A")));
    });

    /// <summary>Run the dispatcher until the condition holds — the only
    /// way a DispatcherTimer ever ticks in a test.</summary>
    private static void PumpUntil(Func<bool> condition, TimeSpan timeout)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var deadline = DateTime.UtcNow + timeout;
        var poll = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Background,
            System.Windows.Threading.Dispatcher.CurrentDispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(5),
        };
        poll.Tick += (_, _) =>
        {
            if (condition() || DateTime.UtcNow > deadline)
            {
                poll.Stop();
                frame.Continue = false;
            }
        };
        poll.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        poll.Stop();
        Assert.True(condition(), "the coalescer never fired on its own within the timeout");
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA test body timed out.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    [Fact]
    public void LabelRenderingPostsNothingAndReturnsCoresText()
    {
        var onboarding = new CanvasA11yEvent.CanvasEmptyOnboarding("Ctrl+Alt+N", "Ctrl+Shift+P");
        Assert.Equal(Render(onboarding), CanvasAnnouncer.RenderLabel(onboarding));
        Assert.Empty(_posted);
    }
    /// <summary>§E TE-11e: the funnel's transactions run on the
    /// work seam, so announcements arrive on POOL threads in the
    /// real app - the authoring journey was the first keyboard on
    /// that path, and the old thread-affinity assert killed the
    /// Debug build while Release raced the coalescing timers. The
    /// announcer marshals itself; this pins the hop.</summary>
    [Fact]
    public void AnAnnouncementFromAForeignThreadMarshalsHome()
    {
        CanvasAnnouncer announcer = NewAnnouncer();
        // An ERROR-tier event posts immediately (no coalescing timer),
        // so the thread hop is the only thing between Announce and the
        // sink: without the marshal this posts synchronously on the
        // pool thread and the Empty assert below bites.
        // A DEDICATED thread: Task.Run(...).Wait() can inline the
        // lambda on the waiting thread, which un-crosses the very
        // boundary this fact exists to cross.
        var foreign = new System.Threading.Thread(
            () => announcer.Announce(new CanvasA11yEvent.CanvasSaveConflict()));
        foreign.Start();
        foreign.Join();
        Assert.Empty(_posted);

        // Pump one dispatcher frame so the self-marshal lands.
        var frame = new System.Windows.Threading.DispatcherFrame();
        _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);

        RenderedAnnouncement spoken = Assert.Single(_posted);
        Assert.Equal(
            Render(new CanvasA11yEvent.CanvasSaveConflict()), spoken.Text);
    }

}
