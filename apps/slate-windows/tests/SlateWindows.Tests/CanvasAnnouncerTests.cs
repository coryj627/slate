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

    [Fact]
    public void LabelRenderingPostsNothingAndReturnsCoresText()
    {
        var onboarding = new CanvasA11yEvent.CanvasEmptyOnboarding("Ctrl+Alt+N", "Ctrl+Shift+P");
        Assert.Equal(Render(onboarding), CanvasAnnouncer.RenderLabel(onboarding));
        Assert.Empty(_posted);
    }
}
