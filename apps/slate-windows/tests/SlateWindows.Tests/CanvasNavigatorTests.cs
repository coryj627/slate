// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 PR C (#745): the navigator command layer, the never-silent read
/// gate, the filter, Where-am-I and the verbosity preference — against a
/// REAL <see cref="VaultSession"/> and real <c>.canvas</c> bytes, with
/// every announcement read as the RENDERED text the production funnel
/// posted.
///
/// Contracts C1–C6 and C10–C14.
/// </summary>
public sealed class CanvasNavigatorTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;
    private readonly List<RenderedAnnouncement> _announced = [];
    private CanvasVerbosity _verbosity = CanvasVerbosity.Standard;

    public CanvasNavigatorTests()
    {
        _fixture = FixtureVault.Create(3, "canvas-navigator");
        WriteCanvasFixtures();
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    private void WriteCanvasFixtures()
    {
        // The sample shape: one group holding two text cards and a file
        // card, an ungrouped card, and enough edges for a multi-edge
        // follow in both directions.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "board.canvas"),
            """
            {
              "nodes": [
                {"id":"grp","type":"group","x":-40,"y":-40,"width":560,"height":400,"label":"Research"},
                {"id":"question","type":"text","text":"Core question","x":0,"y":0,"width":240,"height":140,"color":"1"},
                {"id":"evidence","type":"text","text":"Evidence zeta","x":260,"y":0,"width":220,"height":140},
                {"id":"note","type":"file","file":"note0.md","x":0,"y":180,"width":240,"height":140},
                {"id":"loose","type":"text","text":"Unfiled zeta thought","x":0,"y":460,"width":200,"height":100}
              ],
              "edges": [
                {"id":"e1","fromNode":"question","toNode":"evidence","label":"supports"},
                {"id":"e2","fromNode":"question","toNode":"note"},
                {"id":"e3","fromNode":"loose","toNode":"question","label":"revisit"}
              ]
            }
            """);
        // Nested groups, for the enter/exit boundaries and an EMPTY group.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "nested.canvas"),
            """
            {
              "nodes": [
                {"id":"outer","type":"group","x":0,"y":0,"width":1000,"height":800,"label":"Quarter"},
                {"id":"inner","type":"group","x":40,"y":40,"width":400,"height":300,"label":"Q3"},
                {"id":"hollow","type":"group","x":500,"y":40,"width":200,"height":150,"label":"Empty"},
                {"id":"deepcard","type":"text","text":"inside Q3","x":80,"y":80,"width":100,"height":60},
                {"id":"free","type":"text","text":"outside everything","x":1200,"y":0,"width":100,"height":60}
              ],
              "edges": []
            }
            """);
        // A cycle, so the trace walk has to terminate on its own.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "cycle.canvas"),
            """
            {
              "nodes": [
                {"id":"a","type":"text","text":"Alpha","x":0,"y":0,"width":100,"height":60},
                {"id":"b","type":"text","text":"Beta","x":200,"y":0,"width":100,"height":60},
                {"id":"c","type":"text","text":"Gamma","x":400,"y":0,"width":100,"height":60},
                {"id":"dead","type":"text","text":"Omega","x":600,"y":200,"width":100,"height":60}
              ],
              "edges": [
                {"id":"ab","fromNode":"a","toNode":"b"},
                {"id":"bc","fromNode":"b","toNode":"c"},
                {"id":"ca","fromNode":"c","toNode":"a"}
              ]
            }
            """);
        File.WriteAllText(Path.Combine(_fixture.Root, "empty.canvas"), "{}");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "broken.canvas"), "{ this is not json");

        // The §K shape: 2,000 nodes in a grid, so movement over the whole
        // reading order is exercised at the size the spec names.
        var large = new StringBuilder("{\"nodes\":[");
        for (int index = 0; index < 2000; index++)
        {
            large.Append(index == 0 ? string.Empty : ",")
                .Append(System.Globalization.CultureInfo.InvariantCulture, $$"""
                    {"id":"n{{index}}","type":"text","text":"Card {{index}}","x":{{index % 50 * 300}},"y":{{index / 50 * 200}},"width":240,"height":140}
                    """);
        }
        large.Append("],\"edges\":[]}");
        File.WriteAllText(Path.Combine(_fixture.Root, "large.canvas"), large.ToString());
    }

    private CanvasDocumentViewModel Open(string path)
    {
        var document = new CanvasDocumentViewModel(
            _session,
            path,
            new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
            synchronousForTests: true,
            verbosity: () => _verbosity);
        document.Load();
        Drain(document);
        return document;
    }

    /// <summary>
    /// Every rendered line the funnel has posted, with the coalescer
    /// drained first — a pending navigation line is exactly what the
    /// reader is about to hear, so leaving it queued would read as
    /// silence.
    /// </summary>
    private IReadOnlyList<string> Lines(CanvasDocumentViewModel document)
    {
        document.Announcer.FlushForTests();
        return _announced.Select(line => line.Text).ToArray();
    }

    private string OneLine(CanvasDocumentViewModel document) =>
        Assert.Single(Lines(document));

    /// <summary>
    /// Drain the funnel before an assertion's premise is set up. Clearing
    /// the RECORDER is not enough: the coalescer still holds a queued
    /// navigation line, and it would land in the middle of the next
    /// assertion and read as the verb's own announcement (the PR A
    /// lesson, one suite over).
    /// </summary>
    private void Drain(CanvasDocumentViewModel document)
    {
        document.Announcer.FlushForTests();
        _announced.Clear();
    }

    private static string Rendered(CanvasStatusNote note) =>
        CanvasAnnouncer.RenderLabel(new CanvasA11yEvent.CanvasStatus(note));

    // --- C4: the never-silent read gate ----------------------------------

    /// <summary>
    /// The state → response mapping is TOTAL over the load states, and
    /// the enum is ENUMERATED rather than restated: the mac equivalent's
    /// history is three review rounds finding a state missing from a
    /// hand-written list, and rule 4 says implement the invariant.
    /// </summary>
    [Fact]
    public void TheReadMappingAnswersEveryLoadState()
    {
        var answered = new HashSet<CanvasLoadState>();
        foreach (CanvasLoadState state in Enum.GetValues<CanvasLoadState>())
        {
            foreach (bool handleLive in new[] { true, false })
            {
                CanvasStatusNote? note =
                    CanvasDocumentViewModel.ReadRefusalFor(state, handleLive);
                _ = answered.Add(state);
                if (state == CanvasLoadState.Ready && handleLive)
                {
                    Assert.Null(note);
                    continue;
                }
                Assert.NotNull(note);
                // Every refusal RENDERS: a note core cannot speak would be
                // silence wearing a type.
                Assert.NotEmpty(Rendered(note!));
            }
        }
        Assert.Equal(Enum.GetValues<CanvasLoadState>().ToHashSet(), answered);

        Assert.IsType<CanvasStatusNote.Loading>(
            CanvasDocumentViewModel.ReadRefusalFor(CanvasLoadState.Loading, true));
        Assert.IsType<CanvasStatusNote.Reopening>(
            CanvasDocumentViewModel.ReadRefusalFor(CanvasLoadState.Ready, false));
        Assert.IsType<CanvasStatusNote.NotReadable>(
            CanvasDocumentViewModel.ReadRefusalFor(CanvasLoadState.ParseError, true));
    }

    /// <summary>
    /// Every read verb answers in every state a user can reach — the t0
    /// never-silent rule, checked by DRIVING each verb rather than by
    /// reading the mapping it goes through.
    /// </summary>
    /// <remarks>
    /// The verb list is the navigator's public read surface. A verb added
    /// without a state answer fails here by name rather than going
    /// quietly silent, which is the failure this whole gate exists to
    /// stop.
    /// </remarks>
    [Fact]
    public void EveryReadVerbAnswersInEveryLoadState()
    {
        (string Name, Action<CanvasNavigator> Run)[] verbs =
        [
            ("nextCard", navigator => navigator.NextCard()),
            ("previousCard", navigator => navigator.PreviousCard()),
            ("enterGroup", navigator => navigator.EnterGroup()),
            ("exitGroup", navigator => navigator.ExitGroup()),
            ("followForward", navigator => navigator.FollowConnection(forward: true)),
            ("followBack", navigator => navigator.FollowConnection(forward: false)),
            ("tracePath", navigator => navigator.TracePath()),
            ("whereAmI", navigator => navigator.WhereAmI()),
            ("filterCards", navigator => navigator.FilterCards()),
            ("clearFilter", navigator => navigator.ClearFilter()),
        ];

        foreach ((string label, Func<CanvasDocumentViewModel> open) in UnreadableStates())
        {
            foreach ((string name, Action<CanvasNavigator> run) in verbs)
            {
                CanvasDocumentViewModel document = open();
                Drain(document);
                run(document.Navigator);
                Assert.True(
                    Lines(document).Count > 0,
                    $"`{name}` said NOTHING on a {label} canvas. Every verb answers "
                    + "in every state (contract C4): a keypress that does nothing "
                    + "must say so.");
            }
        }
    }

    /// <summary>The states whose rows the surface does not render, each
    /// with a way to reach it.</summary>
    private (string Label, Func<CanvasDocumentViewModel> Open)[] UnreadableStates() =>
    [
        // Loading: constructed and never published.
        ("loading", () => new CanvasDocumentViewModel(
            _session,
            "board.canvas",
            new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
            synchronousForTests: true,
            verbosity: () => _verbosity)),
        ("parse-error", () => Open("broken.canvas")),
        ("failed", () => Open("missing.canvas")),
        ("retarget-absent", () =>
        {
            var document = new CanvasDocumentViewModel(
                _session,
                "gone.canvas",
                new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
                synchronousForTests: true,
                retargetedFrom: "board.canvas",
                verbosity: () => _verbosity);
            document.Load();
            return document;
        }),
    ];

    /// <summary>
    /// The state's sentence never overrules the verb's OWN precondition
    /// where the reader can see rows: a no-selection press on a ready
    /// canvas says "Nothing selected.", not something about the state.
    /// </summary>
    [Fact]
    public void TheSelectionQuestionOutranksTheStateOnACanvasTheReaderCanSee()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        Assert.True(document.RendersRetainedSnapshot);
        document.SeatSelectionSilently(null);
        Drain(document);

        document.Navigator.EnterGroup();
        Assert.Equal(Rendered(new CanvasStatusNote.NothingSelected()), OneLine(document));
    }

    /// <summary>
    /// A query that THREW is never reported as a query that came back
    /// empty (the VA-1 throw table). The selection names a node the model
    /// cannot resolve, so enter-group must not claim the group is empty
    /// and exit-group must not claim canvas level.
    /// </summary>
    [Fact]
    public void AnUnresolvableSelectionIsNeverReportedAsAnEmptyAnswer()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        document.SeatSelectionSilently("no-such-node");
        Drain(document);

        document.Navigator.ExitGroup();
        Assert.Equal(Rendered(new CanvasStatusNote.NothingSelected()), OneLine(document));
        Assert.DoesNotContain(
            Rendered(new CanvasStatusNote.AtCanvasLevel()), Lines(document));

        Drain(document);
        document.Navigator.TracePath();
        Assert.Equal(Rendered(new CanvasStatusNote.NothingSelected()), OneLine(document));
    }

    // --- C3: movement ----------------------------------------------------

    [Fact]
    public void MovementWalksCoresReadingOrderAndSpeaksEachArrival()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        IReadOnlyList<CanvasOutlineRow> rows = document.Outline;
        Assert.True(rows.Count >= 4);
        document.SeatSelectionSilently(rows[0].NodeId);
        Drain(document);

        document.Navigator.NextCard();
        Assert.Equal(rows[1].NodeId, document.Selection.Selected);
        Assert.Contains(rows[1].SpeakableName, OneLine(document), StringComparison.Ordinal);

        Drain(document);
        document.Navigator.PreviousCard();
        Assert.Equal(rows[0].NodeId, document.Selection.Selected);
    }

    [Fact]
    public void TheEndsOfTheCanvasAnnounceRatherThanDoingNothing()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        document.SeatSelectionSilently(document.Outline[^1].NodeId);
        Drain(document);

        document.Navigator.NextCard();
        Assert.Equal(Rendered(new CanvasStatusNote.EndOfCanvas()), OneLine(document));
        Assert.Equal(document.Outline[^1].NodeId, document.Selection.Selected);

        document.SeatSelectionSilently(document.Outline[0].NodeId);
        Drain(document);
        document.Navigator.PreviousCard();
        Assert.Equal(Rendered(new CanvasStatusNote.StartOfCanvas()), OneLine(document));
    }

    [Fact]
    public void AnEmptyCanvasAnswersRatherThanMovingNowhere()
    {
        CanvasDocumentViewModel document = Open("empty.canvas");
        Drain(document);
        document.Navigator.NextCard();
        Assert.Equal(Rendered(new CanvasStatusNote.Empty()), OneLine(document));
    }

    [Fact]
    public void GroupBoundariesUseCoresParentAndChildrenNeverADepthWalk()
    {
        CanvasDocumentViewModel document = Open("nested.canvas");
        document.SeatSelectionSilently("inner");
        Drain(document);

        document.Navigator.EnterGroup();
        Assert.Equal("deepcard", document.Selection.Selected);
        // The boundary the move crossed, from the document's PURE
        // composer: it and the arrival share the navigation coalescing
        // class, so within the window the arrival supersedes it — pinned
        // here rather than through the coalescer (contract A12's note).
        CanvasA11yEvent? boundary = CanvasDocumentViewModel.GroupBoundaryEvent(
            document.RowFor("inner")!.GroupPath,
            document.RowFor("deepcard")!);
        var entered = Assert.IsType<CanvasA11yEvent.CanvasGroupEntered>(boundary);
        Assert.Equal("Q3", entered.Label);

        Drain(document);
        document.Navigator.ExitGroup();
        Assert.Equal("inner", document.Selection.Selected);

        Drain(document);
        document.SeatSelectionSilently("outer");
        document.Navigator.ExitGroup();
        Assert.Equal(Rendered(new CanvasStatusNote.AtCanvasLevel()), OneLine(document));
    }

    [Fact]
    public void AnEmptyGroupAndANonGroupEachSayWhatTheyAre()
    {
        CanvasDocumentViewModel document = Open("nested.canvas");
        document.SeatSelectionSilently("hollow");
        Drain(document);
        document.Navigator.EnterGroup();
        Assert.Equal(
            Rendered(new CanvasStatusNote.GroupIsEmpty("Empty")), OneLine(document));
        Assert.Equal("hollow", document.Selection.Selected);

        document.SeatSelectionSilently("free");
        Drain(document);
        document.Navigator.EnterGroup();
        Assert.Equal(Rendered(new CanvasStatusNote.NotAGroup()), OneLine(document));
    }

    [Fact]
    public void FollowingAConnectionTraversesAndNamesTheDestinationsRealKind()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        document.SeatSelectionSilently("question");
        Drain(document);

        document.Navigator.FollowConnection(forward: true);
        Assert.Equal("evidence", document.Selection.Selected);
        string line = OneLine(document);
        Assert.Contains("Evidence zeta", line, StringComparison.Ordinal);
        Assert.Contains("supports", line, StringComparison.Ordinal);

        // The SECOND outgoing edge — multi-edge, by ordinal.
        document.SeatSelectionSilently("question");
        Drain(document);
        document.Navigator.FollowConnection(forward: true, ordinal: 2);
        Assert.Equal("note", document.Selection.Selected);

        // Backwards along the incoming one.
        document.SeatSelectionSilently("question");
        Drain(document);
        document.Navigator.FollowConnection(forward: false);
        Assert.Equal("loose", document.Selection.Selected);
    }

    [Fact]
    public void AMissingConnectionSaysWhichDirectionAndWhichOrdinal()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        document.SeatSelectionSilently("evidence");
        Drain(document);
        document.Navigator.FollowConnection(forward: true);
        Assert.Equal(
            Rendered(new CanvasStatusNote.NoConnection(true, null)), OneLine(document));

        document.SeatSelectionSilently("question");
        Drain(document);
        document.Navigator.FollowConnection(forward: true, ordinal: 9);
        Assert.Equal(
            Rendered(new CanvasStatusNote.NoConnection(true, 9)), OneLine(document));
    }

    [Fact]
    public void TracePathWalksCoresCycleSafeChainAndEndsWithTheCount()
    {
        CanvasDocumentViewModel document = Open("cycle.canvas");
        document.SeatSelectionSilently("a");
        Drain(document);

        document.Navigator.TracePath();
        string line = Assert.Single(Lines(document));
        Assert.Contains("Path: Alpha, then Beta, then Gamma", line, StringComparison.Ordinal);
        Assert.Contains("3 cards visited", line, StringComparison.Ordinal);
        Assert.Equal("c", document.Selection.Selected);

        document.SeatSelectionSilently("dead");
        Drain(document);
        document.Navigator.TracePath();
        Assert.Equal(
            Rendered(new CanvasStatusNote.NoOutgoingPath("Omega")), OneLine(document));
    }

    [Fact]
    public void MovementCrossesTheWholeLargeCanvasWithoutRederivingOrder()
    {
        CanvasDocumentViewModel document = Open("large.canvas");
        Assert.Equal(2000, document.Outline.Count);
        document.SeatSelectionSilently(document.Outline[0].NodeId);

        for (int step = 1; step < 2000; step++)
        {
            document.Navigator.NextCard();
            Assert.Equal(document.Outline[step].NodeId, document.Selection.Selected);
        }

        Drain(document);
        document.Navigator.NextCard();
        Assert.Equal(Rendered(new CanvasStatusNote.EndOfCanvas()), OneLine(document));
    }

    // --- C10: the filter -------------------------------------------------

    [Fact]
    public void MovementWalksTheFilteredSetAndTheProjectionsNarrowWithIt()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        document.FilterText = "zeta";
        IReadOnlyList<CanvasOutlineRow> narrowed = document.FilteredOutline;
        Assert.True(document.FilterActive);
        Assert.True(narrowed.Count > 0);
        Assert.True(narrowed.Count < document.Outline.Count);
        // The TABLE narrows by the SAME answer, so the two projections
        // can never disagree about what the needle matched.
        Assert.Equal(
            narrowed.Select(row => row.NodeId).ToHashSet(),
            document.FilteredTableRows.Select(row => row.NodeId).ToHashSet());

        document.SeatSelectionSilently(narrowed[0].NodeId);
        Drain(document);
        document.Navigator.NextCard();
        Assert.Equal(narrowed[1].NodeId, document.Selection.Selected);

        document.SeatSelectionSilently(narrowed[^1].NodeId);
        Drain(document);
        document.Navigator.NextCard();
        Assert.Equal(Rendered(new CanvasStatusNote.EndOfCanvas()), OneLine(document));
    }

    [Fact]
    public void AFilterThatMatchesNothingSaysSoRatherThanReadingAsAnEmptyCanvas()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        document.FilterText = "zzzz-no-such-card";
        Assert.Empty(document.FilteredOutline);
        Drain(document);

        document.Navigator.NextCard();
        Assert.Equal(
            Rendered(new CanvasStatusNote.NoCardsMatchFilter()), OneLine(document));
    }

    [Fact]
    public void TheAnnouncedCountIsTheRowsTheSurfacesShow()
    {
        CanvasDocumentViewModel document = Open("board.canvas");

        // The needle alone announces: the count rides the ANSWER landing,
        // not the keystroke, so it can never describe rows that are not
        // on screen yet (contract C10).
        document.FilterText = "zeta";
        Assert.Equal(
            CanvasAnnouncer.RenderLabel(new CanvasA11yEvent.CanvasFilterCount(
                (uint)document.FilteredOutline.Count)),
            OneLine(document));

        Drain(document);
        document.Navigator.AnnounceFilterCount();
        Assert.Equal(
            CanvasAnnouncer.RenderLabel(new CanvasA11yEvent.CanvasFilterCount(
                (uint)document.FilteredOutline.Count)),
            OneLine(document));
        // The summary label reads the SAME view, so the number on screen
        // and the number spoken cannot come from two answers.
        Assert.StartsWith(
            $"{document.FilteredOutline.Count} of ",
            Assert.IsType<string>(document.Navigator.FilterSummaryText()),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ClearingTheFilterRestoresEveryCardAndSaysHowMany()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        document.FilterText = "zeta";
        document.SeatSelectionSilently(document.FilteredOutline[0].NodeId);
        string selected = document.Selection.Selected!;
        Drain(document);

        document.Navigator.ClearFilter();
        Assert.False(document.FilterActive);
        Assert.Equal(document.Outline.Count, document.FilteredOutline.Count);
        // A view, never a mutation: the selection is where it was.
        Assert.Equal(selected, document.Selection.Selected);
        Assert.Equal(
            CanvasAnnouncer.RenderLabel(new CanvasA11yEvent.CanvasFilterCleared(
                (uint)document.Outline.Count)),
            OneLine(document));
    }

    /// <summary>
    /// Whitespace is not a filter — mac's rule, including the one place
    /// .NET would have disagreed (a bare newline reads as ACTIVE there,
    /// and core trims it, so it matches everything).
    /// </summary>
    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("\t", false)]
    [InlineData("\n", true)]
    [InlineData(" a ", true)]
    public void TheFilterActivePredicateIsMacs(string needle, bool active) =>
        Assert.Equal(active, CanvasDocumentViewModel.IsFilterActive(needle));

    // --- C11: Where am I --------------------------------------------------

    [Fact]
    public void WhereAmIRendersTheSameStringItSpeaks()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        document.SeatSelectionSilently("question");
        Drain(document);

        document.Navigator.WhereAmI();
        string spoken = OneLine(document);
        Assert.Equal(spoken, document.WhereAmIText);
        // Always verbose-grade: the readback carries the whole context
        // whatever the persisted level says.
        Assert.Contains("Research", spoken, StringComparison.Ordinal);
        Assert.Contains("connection", spoken, StringComparison.Ordinal);
    }

    [Fact]
    public void WhereAmICarriesTheActiveModeAndTheFilterState()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        document.SeatSelectionSilently("question");
        document.FilterText = "zeta";
        Assert.True(document.Modes.Enter(new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("Core question"),
            () => null,
            () => new CanvasModeRestoration.Unstated())));
        Drain(document);

        document.Navigator.WhereAmI();
        string spoken = Assert.IsType<string>(document.WhereAmIText);
        Assert.Contains("Move mode", spoken, StringComparison.Ordinal);
        Assert.Contains(
            $"{document.FilteredOutline.Count} of {document.Outline.Count} shown",
            spoken,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhereAmIOnAnEmptyCanvasStillAnswers()
    {
        CanvasDocumentViewModel document = Open("empty.canvas");
        Drain(document);
        document.Navigator.WhereAmI();
        Assert.Equal(Rendered(new CanvasStatusNote.Empty()), OneLine(document));
        Assert.Null(document.WhereAmIText);
    }

    // --- C13: verbosity ---------------------------------------------------

    /// <summary>
    /// The level is read at EVERY announce: the same movement, at three
    /// levels, renders three different lines with no reload and nothing
    /// pushed into the document.
    /// </summary>
    [Fact]
    public void VerbosityIsReadLiveAtEveryAnnouncement()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var lines = new List<string>();
        foreach (CanvasVerbosity level in Enum.GetValues<CanvasVerbosity>())
        {
            _verbosity = level;
            document.SeatSelectionSilently("evidence");
            Drain(document);
            document.Navigator.PreviousCard();
            lines.Add(OneLine(document));
        }

        Assert.Equal(3, lines.Distinct(StringComparer.Ordinal).Count());
        // Terse is the bare title; verbose carries the connection count.
        Assert.DoesNotContain(" of ", lines[0], StringComparison.Ordinal);
        Assert.Contains(" of ", lines[1], StringComparison.Ordinal);
        Assert.Contains("connection", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void TheVerbosityPreferenceRoundTripsThroughTheStore()
    {
        string path = Path.Combine(_fixture.Root, "prefs.json");
        var store = new AppPreferencesStore(path);
        var preferences = new CanvasPreferencesViewModel(store);
        Assert.Equal(CanvasVerbosity.Standard, preferences.Verbosity);
        Assert.True(preferences.IsVerbosityStandard);

        preferences.SetVerbosityCommand.Execute("verbose");
        Assert.Equal(CanvasVerbosity.Verbose, preferences.Verbosity);
        Assert.True(preferences.IsVerbosityVerbose);
        Assert.Equal("verbose", store.Load().CanvasVerbosity);

        Assert.Equal(
            CanvasVerbosity.Verbose,
            new CanvasPreferencesViewModel(store).Verbosity);

        // An unknown key is ignored rather than throwing or blanking the
        // level — every other preference field degrades the same way.
        preferences.SetVerbosityCommand.Execute("shouty");
        Assert.Equal(CanvasVerbosity.Verbose, preferences.Verbosity);
    }

    // --- C2/C6: the surface, its keys and its regions ---------------------

    /// <summary>
    /// The Escape ladder end to end through a REAL key press on the REAL
    /// surface: the rungs' EFFECTS in order, and the press left
    /// unconsumed at the bottom so the shell's own Escape still works
    /// with a canvas open (contract C6).
    /// </summary>
    /// <remarks>
    /// What this drives is the whole chain a user's Escape takes —
    /// `OnPreviewKeyDown` → `HandleKey` → the controller's ladder → the
    /// surface's presenter — and what it reads is each rung's effect on
    /// the surface, plus `e.Handled`. The rung NAMES are the mode
    /// controller's table test; the key reaching the surface from a real
    /// input stack is the journey's.
    /// </remarks>
    [Fact]
    public void TheSurfaceLadderClearsTheFilterThenTheRegionThenBubbles() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);
        surface.OutlineForTests.FocusTree();
        host.UpdateLayout();

        // Rung 2 — the filter.
        document.FilterText = "zeta";
        host.UpdateLayout();
        Assert.True(document.FilterActive);
        Assert.True(
            PressKey(surface, Key.Escape, ModifierKeys.None),
            "the ladder must CONSUME the press that clears the filter.");
        Assert.Equal(string.Empty, document.FilterText);
        host.UpdateLayout();

        // Rung 3 — the transient region. One press per rung: the filter
        // is already gone, so this press reaches the panel.
        document.Navigator.WhereAmI();
        host.UpdateLayout();
        Assert.NotNull(document.WhereAmIText);
        Assert.True(
            PressKey(surface, Key.Escape, ModifierKeys.None),
            "the ladder must CONSUME the press that dismisses the panel.");
        Assert.Null(document.WhereAmIText);
        host.UpdateLayout();

        // Rung 4 — nothing left in the canvas to consume it, so the press
        // is NOT handled and belongs to the workspace. This is the half
        // that keeps the shell's Escape working with a canvas open.
        Assert.False(
            PressKey(surface, Key.Escape, ModifierKeys.None),
            "an Escape the canvas has no rung for must bubble.");
    });

    /// <summary>
    /// M3 and M6 on the surface a client actually reads: the mode's value
    /// arrives as <c>ItemStatus</c> on a PEERED element, and the visible
    /// controls appear with it.
    /// </summary>
    [Fact]
    public void TheModeIsInspectableAndHasVisibleControls() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);

        // Read off the PEER, never off the attached property: the
        // attached property is the setter side, and PR A's round 8
        // recorded what happens when only that side is checked — a value
        // set on an element WPF never peers reaches no client and nothing
        // fails. The surface is a peered `UserControl`, so the two agree
        // here; asserting the peer is what makes that a finding rather
        // than an assumption.
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(surface);
        Assert.Equal(string.Empty, peer.GetItemStatus());
        Assert.Equal(Visibility.Collapsed, surface.CommitModeForTests.Visibility);

        Assert.True(document.Modes.Enter(new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("Core question"),
            () => null,
            () => new CanvasModeRestoration.BackAt("Core question"))));
        host.UpdateLayout();

        Assert.Equal("Move mode: \"Core question\"", peer.GetItemStatus());
        Assert.Equal(Visibility.Visible, surface.CommitModeForTests.Visibility);
        Assert.Equal(Visibility.Visible, surface.CancelModeForTests.Visibility);

        surface.CancelModeForTests.RaiseEvent(
            new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        host.UpdateLayout();
        Assert.False(document.Modes.IsActive);
        Assert.Equal(string.Empty, peer.GetItemStatus());
    });

    /// <summary>
    /// The filter field is an Edit with mac's label whose VALUE is the
    /// needle, and the result summary is a separately readable element
    /// (t0 §3) — read off the peers, not off the fields that fed them.
    /// </summary>
    [Fact]
    public void TheFilterFieldAndItsSummaryAreReadable() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);

        AutomationPeer field =
            UIElementAutomationPeer.CreatePeerForElement(surface.FilterFieldForTests);
        Assert.Equal("Filter cards", field.GetName());
        Assert.Equal(AutomationControlType.Edit, field.GetAutomationControlType());
        Assert.Equal(Visibility.Collapsed, surface.FilterSummaryForTests.Visibility);

        surface.FilterFieldForTests.Text = "zeta";
        host.UpdateLayout();
        Assert.Equal("zeta", document.FilterText);
        var value = (System.Windows.Automation.Provider.IValueProvider)
            field.GetPattern(PatternInterface.Value);
        Assert.Equal("zeta", value.Value);
        Assert.Equal(Visibility.Visible, surface.FilterSummaryForTests.Visibility);
        // The summary's NAME is read off its own peer, for the reason
        // PR A's round 8 recorded: the attached property is the setter
        // side, and a value on an element WPF never peers reaches no
        // client while every property-level assertion stays green. A
        // `TextBlock` peers, so the two agree — which is the fact, not
        // the assumption.
        AutomationPeer summary =
            UIElementAutomationPeer.CreatePeerForElement(surface.FilterSummaryForTests);
        Assert.Equal(
            $"Filter results: {document.Navigator.FilterSummaryText()}",
            summary.GetName());
        Assert.Equal(Visibility.Visible, surface.ClearFilterForTests.Visibility);
    });

    /// <summary>
    /// Ctrl+Alt+Shift+I lands focus in the panel, and the panel carries
    /// the same string the announcement spoke with a live setting of Off
    /// — pull, not push.
    /// </summary>
    [Fact]
    public void TheWhereAmIChordOpensAndFocusesThePanel() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);
        surface.OutlineForTests.FocusTree();
        host.UpdateLayout();
        Drain(document);

        Assert.True(PressKey(surface, Key.I, ModifierKeys.Control | ModifierKeys.Alt
            | ModifierKeys.Shift));
        host.UpdateLayout();

        Assert.Equal(Visibility.Visible, surface.WhereAmIPanelForTests.Visibility);
        Assert.Equal(document.WhereAmIText, surface.WhereAmIReadbackForTests.Text);
        Assert.Equal(OneLine(document), surface.WhereAmIReadbackForTests.Text);
        Assert.Equal(
            AutomationLiveSetting.Off,
            AutomationProperties.GetLiveSetting(surface.WhereAmIReadbackForTests));
        Assert.True(
            surface.WhereAmIReadbackForTests.IsKeyboardFocusWithin,
            "the chord must land focus in the panel (spec §PR C Tests).");
    });

    /// <summary>
    /// R2 and contract C3: the projection moves the reader itself, so the
    /// navigator does NOT consume Down while the tree can still move —
    /// and does consume it at the boundary, where the tree would be
    /// silent.
    /// </summary>
    /// <remarks>
    /// The fixture is the one with NO edges, deliberately: on a canvas
    /// with connections the last CARD is not the last row, because the
    /// selected card's connection rows sit under it as reading stops
    /// (contract A11) — which the second half of this fact pins on
    /// purpose, so the two behaviours are recorded rather than one of
    /// them being an accident of fixture choice.
    /// </remarks>
    [Fact]
    public void TheArrowDefersToTheProjectionAndAnswersOnlyAtTheBoundary() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("nested.canvas");
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);
        document.SeatSelectionSilently(document.Outline[0].NodeId);
        Assert.NotNull(surface.OutlineForTests.DeliverFocus(document.Outline[0].NodeId));
        host.UpdateLayout();
        // The premise: rule R2 gates every arrow on the projection owning
        // the keys, so a fact run without them would pass both halves for
        // the wrong reason.
        Assert.True(
            surface.ProjectionHasFocus,
            "the outline never took keyboard focus; the R2 gate makes both halves "
            + "below unconsumed, which would pass the first and fail the second "
            + "for a reason that is not the behaviour.");
        Drain(document);

        Assert.False(
            PressKey(surface, Key.Down, ModifierKeys.None),
            "the tree can still move, so the navigator must let the key through.");
        Assert.Empty(Lines(document));

        Assert.NotNull(surface.OutlineForTests.DeliverFocus(document.Outline[^1].NodeId));
        host.UpdateLayout();
        Drain(document);
        Assert.True(
            PressKey(surface, Key.Down, ModifierKeys.None),
            "at the boundary the navigator must consume the key and answer.");
        Assert.Equal(Rendered(new CanvasStatusNote.EndOfCanvas()), OneLine(document));
    });

    /// <summary>
    /// M4's menu arm, end to end (contract C8, CD-41): opening a menu
    /// moves keyboard focus onto its <c>MenuItem</c>, and the mode must
    /// SURVIVE that — the shell's own Canvas menu carries Commit Mode and
    /// Cancel Mode, so a cancel-on-open would kill the two items the user
    /// opened the menu to reach.
    /// </summary>
    /// <remarks>
    /// The contrast leg is the point: the SAME focus loss to an ordinary
    /// element outside the canvas cancels. Without it this fact would
    /// pass on a surface that had simply stopped classifying departures
    /// at all.
    /// </remarks>
    [Fact]
    public void OpeningAMenuKeepsTheModeAliveAndLeavingTheCanvasCancelsIt() => RunSta(() =>
    {
        foreach (bool intoMenu in new[] { true, false })
        {
            CanvasDocumentViewModel document = Open("board.canvas");
            var surface = new CanvasSurfaceView { Model = document };
            var menu = new Menu();
            var canvasMenu = new MenuItem { Header = "Canvas" };
            menu.Items.Add(canvasMenu);
            var elsewhere = new TextBox { Width = 40 };
            var root = new DockPanel();
            DockPanel.SetDock(menu, Dock.Top);
            DockPanel.SetDock(elsewhere, Dock.Bottom);
            root.Children.Add(menu);
            root.Children.Add(elsewhere);
            root.Children.Add(surface);
            using var host = Host(root);

            Assert.NotNull(
                surface.OutlineForTests.DeliverFocus(document.Outline[0].NodeId));
            host.UpdateLayout();
            // The premise: the surface really has the keys, so the loss
            // below is a real departure rather than a no-op.
            Assert.True(surface.IsKeyboardFocusWithin);
            Assert.True(document.Modes.Enter(new CanvasModeSpec(
                CanvasMode.Move,
                new CanvasModeObject.Card("Core question"),
                () => null,
                () => new CanvasModeRestoration.BackAt("Core question"))));
            Drain(document);

            Assert.True(intoMenu ? canvasMenu.Focus() : elsewhere.Focus());
            host.UpdateLayout();
            Assert.False(
                surface.IsKeyboardFocusWithin,
                "focus never left the canvas surface, so no departure was classified.");

            if (intoMenu)
            {
                Assert.True(
                    document.Modes.IsActive,
                    "opening a menu cancelled the mode — the Canvas menu's own "
                    + "Commit Mode and Cancel Mode items are dead the moment it "
                    + "opens (contract C8, CD-41).");
                Assert.True(document.Modes.CanCommitOrCancel);
                Assert.Empty(Lines(document));
                _ = document.Modes.Cancel();
            }
            else
            {
                Assert.False(
                    document.Modes.IsActive,
                    "leaving the canvas for another part of the shell must cancel "
                    + "the mode (t0 §2 M4).");
                Assert.NotEmpty(Lines(document));
            }
        }
    });

    /// <summary>
    /// The boundary question is the PROJECTION's own rows, not core's
    /// reading order: a connection row under the selected card is a
    /// reading stop the tree visits (contract A11), so the last CARD is
    /// not the end of the canvas while one is sitting below it.
    /// </summary>
    [Fact]
    public void TheBoundaryIsTheProjectionsRowsNotCoresReadingOrder() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);

        string lastCard = document.Outline[^1].NodeId;
        Assert.NotEmpty(document.NeighborsOf(lastCard));
        Assert.NotNull(surface.OutlineForTests.DeliverFocus(lastCard));
        host.UpdateLayout();
        Assert.True(
            surface.CanMoveWithinProjection(forward: true),
            "the last CARD still has its connection row below it, so the tree can "
            + "move and the navigator must not claim the end of the canvas.");

        // On the table — a flat projection with no connection rows — the
        // last row really is the end.
        document.Selection.ActiveSurface = CanvasSurfaceKind.Table;
        host.UpdateLayout();
        Assert.True(surface.TableForTests.DeliverFocus(lastCard));
        host.UpdateLayout();
        Assert.False(surface.CanMoveWithinProjection(forward: true));
        Assert.True(surface.CanMoveWithinProjection(forward: false));
    });

    // --- C12 / CD-40: the carried A14 focus-delivery defect ---------------

    /// <summary>
    /// A focus delivery to a node OTHER than the selection lands the
    /// reader and says NOTHING — in both projections, because the outline
    /// drives the identical path and a table-only fix would make them
    /// behave differently on the same request.
    /// </summary>
    [Fact]
    public void AFocusDeliveryToANodeOtherThanTheSelectionDoesNotDouble() => RunSta(() =>
    {
        foreach (CanvasSurfaceKind projection in
            new[] { CanvasSurfaceKind.Outline, CanvasSurfaceKind.Table })
        {
            CanvasDocumentViewModel document = Open("board.canvas");
            document.Selection.ActiveSurface = projection;
            var surface = new CanvasSurfaceView { Model = document };
            using var host = Host(surface);

            string landing = document.Outline[^1].NodeId;
            string elsewhere = document.Outline[0].NodeId;
            document.SeatSelectionSilently(elsewhere);
            host.UpdateLayout();
            // The premise the fact rests on: the two really do differ, so
            // the delivery below is the reachable case A14 describes.
            Assert.NotEqual(landing, document.Selection.Selected);
            document.Announcer.FlushForTests();
            Drain(document);

            surface.FocusRow(landing);
            host.UpdateLayout();

            Assert.True(
                Lines(document).Count == 0,
                $"the {projection} delivery narrated: [{string.Join(" | ", Lines(document))}]. "
                + "A landing is not a move the user made, and the screen reader "
                + "reads the row it lands on (t0 §1.5, contract C12).");
            // One shared selection (R-B), and it followed the reader
            // rather than being left pointing at a row nobody is on.
            Assert.Equal(landing, document.Selection.Selected);
        }
    });

    // --- Test plumbing ----------------------------------------------------

    /// <summary>Send one key through the surface's real tunnelling
    /// handler and report whether the canvas consumed it.</summary>
    private static bool PressKey(CanvasSurfaceView surface, Key key, ModifierKeys modifiers)
    {
        // The production route is `OnPreviewKeyDown`, and the navigator
        // reads `Keyboard.Modifiers` — which a synthesised event cannot
        // set — so the modified chords are delivered through the
        // navigator with the modifiers stated, and the BARE ones through
        // the real routed event. Both end in `CanvasNavigator.HandleKey`,
        // which is the seam under test.
        if (modifiers != ModifierKeys.None)
        {
            return surface.Model!.Navigator.HandleKey(key, modifiers, surface);
        }
        var args = new KeyEventArgs(
            Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(surface)
                ?? throw new InvalidOperationException("the surface is not in a window."),
            0,
            key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };
        surface.RaiseEvent(args);
        return args.Handled;
    }

    private static HostedWindow Host(UIElement content)
    {
        var window = new Window
        {
            Content = content,
            Width = 900,
            Height = 700,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        return new HostedWindow(window);
    }

    private sealed class HostedWindow(Window window) : IDisposable
    {
        internal void UpdateLayout() => window.UpdateLayout();

        public void Dispose() => window.Close();
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "STA test body timed out.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
