// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
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
        // Two cards, an edge between them, and a file target — the
        // fixture the side-state fact reloads into a different shape.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "connected.canvas"),
            """
            {
              "nodes": [
                {"id":"hub","type":"file","file":"note0.md","x":0,"y":0,"width":200,"height":100},
                {"id":"spoke","type":"text","text":"one","x":300,"y":0,"width":200,"height":100},
                {"id":"other","type":"text","text":"two","x":600,"y":0,"width":200,"height":100}
              ],
              "edges": [
                {"id":"h1","fromNode":"hub","toNode":"spoke"},
                {"id":"h2","fromNode":"other","toNode":"hub"}
              ]
            }
            """);
        // Two sibling groups, one matching by its own label and one
        // whose CHILD matches: the shape a depth-stack over the filtered
        // rows fabricates containment from.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "branches.canvas"),
            """
            {
              "nodes": [
                {"id":"alpha","type":"group","x":0,"y":0,"width":400,"height":300,"label":"Alpha zeta"},
                {"id":"inAlpha","type":"text","text":"plain one","x":40,"y":40,"width":200,"height":100},
                {"id":"beta","type":"group","x":600,"y":0,"width":400,"height":300,"label":"Beta"},
                {"id":"inBeta","type":"text","text":"zeta two","x":640,"y":40,"width":200,"height":100}
              ],
              "edges": []
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
                CanvasStatusNote expected = Assert.IsType<CanvasStatusNote>(
                    document.ReadRefusal, exactMatch: false);
                run(document.Navigator);
                // THE EXACT SENTENCE, not "something spoke". A verb that
                // answers with the wrong state's line is as wrong as one
                // that says nothing, and the weaker assertion is what let
                // `clearFilter` walk past the mapping entirely and pass:
                // it announced `Filter cleared — 0 cards.` on a canvas
                // that could not answer, which is a count over an empty
                // outline reading as an empty canvas.
                Assert.Equal(
                    Rendered(expected),
                    Assert.Single(
                        Lines(document),
                        line => true));
            }
        }
    }

    /// <summary>
    /// Codex C-lite round 1, B1: Filter Cards from the PALETTE reaches
    /// the field, even though the palette held the keys when it ran.
    /// </summary>
    /// <remarks>
    /// The token was acknowledged before eligibility was asked, so the
    /// palette route — the one a reader who does not know the chord
    /// takes — consumed it and did nothing: the palette owns focus while
    /// it closes, every surface reads as ineligible, and nothing
    /// retried. Ctrl+F worked and the palette row did not, which is a
    /// §W-D difference between two routes to one verb. The request is
    /// durable now, like the A14 focus landing beside it.
    /// </remarks>
    [Fact]
    public void TheFilterVerbReachesTheFieldEvenWhenSomethingElseHoldsTheKeys() =>
        RunSta(() =>
        {
            CanvasDocumentViewModel document = Open("board.canvas");
            var surface = new CanvasSurfaceView { Model = document };
            var elsewhere = new Button { Content = "the palette stands here" };
            var root = new StackPanel();
            root.Children.Add(elsewhere);
            root.Children.Add(surface);
            using var host = Host(root);
            Assert.True(elsewhere.Focus(), "the premise: another element has the keys.");
            host.UpdateLayout();

            // The palette's route, with the palette still holding focus.
            document.Navigator.FilterCards();
            host.UpdateLayout();
            Assert.False(
                surface.FilterFieldForTests.IsKeyboardFocused,
                "a surface that does not have the keys must not steal them.");

            // …and the moment the keys come back, the request lands.
            surface.OutlineForTests.FocusTree();
            host.UpdateLayout();
            Assert.True(
                surface.FilterFieldForTests.IsKeyboardFocused,
                "the request must SURVIVE until a surface can satisfy it — a "
                + "token acknowledged by a surface that could not focus the "
                + "field is a verb that silently does nothing.");
        });

    /// <summary>
    /// Codex C-lite round 1, B2: a filtered outline never claims
    /// containment the canvas does not have.
    /// </summary>
    /// <remarks>
    /// Depth is a position in core's READING ORDER, so a depth stack run
    /// over the filtered rows attached a survivor whose own group was
    /// filtered out to whatever survivor happened to be shallower and
    /// earlier — a card from an unrelated branch. A screen reader reads
    /// that as containment, and it is false. CD-45 promotes such a
    /// survivor to a ROOT; the containment now comes from the unfiltered
    /// hierarchy, which is what makes that promotion true rather than
    /// approximately true.
    /// </remarks>
    [Fact]
    public void AFilteredOutlineNeverNestsACardUnderAGroupItIsNotIn() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("branches.canvas");
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);

        document.FilterText = "zeta";
        host.UpdateLayout();

        // The premise: the ALPHA group matched on its own label, and a
        // card inside BETA matched on its text — so the survivors are a
        // shallow row from one branch and a deep row from another.
        IReadOnlyList<CanvasOutlineRowViewModel> roots =
            surface.OutlineForTests.RootsForTests;
        string[] rootIds = [.. roots.Select(row => row.Id)];
        Assert.Contains("alpha", rootIds);
        Assert.DoesNotContain("inAlpha", rootIds);

        // `inBeta` is a ROOT. Under the depth stack it was a CHILD of
        // `alpha` — a card in Beta, presented as inside Alpha.
        Assert.Contains(
            "inBeta",
            rootIds);
        CanvasOutlineRowViewModel alpha =
            roots.Single(row => row.Id == "alpha");
        Assert.DoesNotContain(
            "inBeta",
            alpha.Children.Select(child => child.Id));
    });

    /// <summary>
    /// Codex C-lite round 1, B5: the filter summary never counts rows
    /// the surface is not showing.
    /// </summary>
    /// <remarks>
    /// C10's one invariant is <i>displayed rows == announced count</i>,
    /// and a RELOAD broke it from the state side rather than the filter
    /// side: the projections collapse while the canvas is `Loading`, and
    /// the memoized answer stayed `Current`, so the region read "2 of 5
    /// cards match" over a pane showing nothing. The view is only current
    /// while the rows are renderable now, so the label falls back to the
    /// state's own sentence for exactly that window.
    /// </remarks>
    [Fact]
    public void TheFilterSummaryNeverCountsRowsTheSurfaceIsNotShowing() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);
        document.FilterText = "zeta";
        host.UpdateLayout();
        Assert.StartsWith(
            "2 of ",
            surface.FilterSummaryForTests.Text,
            StringComparison.Ordinal);

        var samples = new List<(string Summary, int Shown, bool Visible)>();
        void Sample() => samples.Add((
            surface.FilterSummaryForTests.Text,
            CountMaterializedRows(surface.OutlineForTests.RootsForTests),
            surface.OutlineForTests.Visibility == Visibility.Visible));
        void OnProperty(object? sender, PropertyChangedEventArgs e) => Sample();
        void OnPublished(object? sender, EventArgs e) => Sample();
        document.PropertyChanged += OnProperty;
        document.OutlinePublished += OnPublished;
        try
        {
            document.Load();
        }
        finally
        {
            document.PropertyChanged -= OnProperty;
            document.OutlinePublished -= OnPublished;
        }

        Assert.NotEmpty(samples);
        foreach ((string summary, int shown, bool visible) in samples)
        {
            if (!summary.Contains(" of ", StringComparison.Ordinal))
            {
                // The state's own sentence, which is the honest answer
                // while nothing is on screen.
                continue;
            }
            Assert.True(
                visible,
                $"the summary counted while the projection was hidden: '{summary}'.");
            Assert.StartsWith(
                $"{shown} of ",
                summary,
                StringComparison.Ordinal);
        }
    });

    /// <summary>Every materialized node row in the tree, connections
    /// excluded — what a reader can actually reach.</summary>
    private static int CountMaterializedRows(
        IEnumerable<CanvasOutlineRowViewModel> rows)
    {
        int count = 0;
        foreach (CanvasOutlineRowViewModel row in rows)
        {
            if (!row.IsConnection)
            {
                count++;
            }
            count += CountMaterializedRows(row.Children);
        }
        return count;
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

    /// <summary>
    /// Typing in the FILTER FIELD announces the count, and the number
    /// spoken is the number on screen (contract C10).
    /// </summary>
    /// <remarks>
    /// Driven through the field rather than by calling the verb, because
    /// the field is the production trigger and the verb is not: the
    /// surface's <c>TextChanged</c> handler is the only thing that
    /// announces a count for a keystroke, and a fact that called
    /// <c>AnnounceFilterCount</c> itself stayed green while that handler
    /// was silent. The C-lite extraction proved it: an async-era body
    /// came across, the announcement was gone, and every filter fact
    /// passed.
    /// </remarks>
    [Fact]
    public void TypingInTheFilterFieldAnnouncesTheRowsTheSurfacesShow() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);
        Drain(document);

        surface.FilterFieldForTests.Text = "zeta";
        host.UpdateLayout();

        Assert.Equal("zeta", document.FilterText);
        Assert.Equal(
            CanvasAnnouncer.RenderLabel(new CanvasA11yEvent.CanvasFilterCount(
                (uint)document.FilteredOutline.Count)),
            OneLine(document));
        // The summary label reads the SAME view, so the number on screen
        // and the number spoken cannot come from two answers.
        Assert.StartsWith(
            $"{document.FilteredOutline.Count} of ",
            document.Navigator.FilterSummaryText(),
            StringComparison.Ordinal);
    });

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
    /// Codex round 2, B2-continued: the tab is retired while a commit
    /// effect is still running, with a focus departure already held.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Teardown is the one exit from the transition that never returns to
    /// <c>Commit</c>, so the held departure has to drain HERE — and
    /// before the funnel is silenced, because a departure owes a
    /// restoration and a sentence saying what came back. The order is the
    /// whole fact: drain, then silence.
    /// </para>
    /// <para>
    /// And then the 0a-2 lesson's own shape, one document over: nothing
    /// speaks after retirement. The commit's confirmation is composed
    /// after the funnel closed and is DROPPED rather than queued, so the
    /// user hears the restoration and nothing else — not a confirmation
    /// for a canvas that no longer exists.
    /// </para>
    /// </remarks>
    [Fact]
    public void AShutdownDuringACommitDrainsTheHeldDepartureThenSilences()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var spec = new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("Research"),
            () =>
            {
                // Focus leaves the canvas while the stack is still up…
                _ = document.Modes.HandleFocusDeparture(CanvasFocusDeparture.PaneFocus);
                // …and the shell retires the tab before the effect resolves.
                document.Shutdown();
                return CanvasModeCommitResult.Committed(
                    new CanvasA11yEvent.CanvasModeCommitted(
                        CanvasTransientVerb.Move,
                        new CanvasModeObject.Card("Research")));
            },
            () => new CanvasModeRestoration.BackAt("Research"));
        Assert.True(document.Modes.Enter(spec));
        Drain(document);

        _ = document.Modes.Commit();

        // The held departure was honoured on the way out: the mode is
        // gone, restored, and the restoration was SPOKEN.
        Assert.False(document.Modes.IsActive);
        Assert.Null(document.Modes.ContainerValue);
        string line = OneLine(document);
        Assert.Contains("cancelled", line, StringComparison.Ordinal);

        // …and nothing the retired document composes afterwards reaches
        // anybody — including the commit confirmation that resolved after
        // the funnel closed.
        Assert.DoesNotContain(
            Lines(document),
            spoken => spoken.Contains("Moved", StringComparison.Ordinal));
        document.Announcer.Announce(
            new CanvasA11yEvent.CanvasStatus(new CanvasStatusNote.NoMarks()));
        Assert.Equal(line, OneLine(document));

        // The slot is CLEARED at teardown: nothing stale is left to act
        // on a document that is gone.
        Assert.False(document.Modes.HandleFocusDeparture(CanvasFocusDeparture.PaneFocus));
        Assert.Equal(line, OneLine(document));
    }

    /// <summary>
    /// Codex round 3, B2: retirement does not depend on a restoration
    /// effect succeeding.
    /// </summary>
    /// <remarks>
    /// Closing a tab with a mode running ends that mode, and ending it
    /// runs host code — a restoration that can fault. When it did, the
    /// announcer below it was never silenced, so a coalesced line stayed
    /// queued on a document that no longer exists and spoke about it
    /// ~200 ms later: the A5 defect, reached from the mode side. The
    /// handle was never closed either. Logged and continued, so the
    /// failure is reported and the teardown still happens.
    /// </remarks>
    [Fact]
    public void ATeardownWhoseRestorationFaultsStillSilencesTheAnnouncer()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var spec = new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("Research"),
            () => CanvasModeCommitResult.Refused(),
            () => throw new NotSupportedException("the restoration faulted."));
        Assert.True(document.Modes.Enter(spec));
        // A coalesced line is queued on the way out — the 0a-2 premise.
        document.SelectNode("evidence");
        _announced.Clear();

        // Retirement completes. It does not propagate the restoration's
        // failure to the registry sweeping every open document.
        document.Shutdown();

        Assert.False(document.Modes.IsActive);
        // The queued line was DROPPED and the funnel refuses anything
        // later, which is only true if `Announcer.Shutdown` was reached.
        document.Announcer.Announce(
            new CanvasA11yEvent.CanvasStatus(new CanvasStatusNote.NoMarks()));
        Assert.Empty(Lines(document));
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
            () => CanvasModeCommitResult.Committed(),
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
    /// B1: Escape with the reader INSIDE the Where-am-I panel acts on the
    /// PANEL — it does not clear a typed filter out from under them
    /// (contract C6, CD-47).
    /// </summary>
    /// <remarks>
    /// Asking Where-am-I while filtering is a first-class t0 §1.4
    /// scenario — the readback carries the filter clause — so this
    /// combination is the designed use, not a corner. The second press,
    /// with focus back in the projection, is the ladder's: it takes rung
    /// 2 and clears the needle.
    /// </remarks>
    [Fact]
    public void EscapeInsideThePanelWhileFilteringActsOnThePanel() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);

        document.FilterText = "zeta";
        host.UpdateLayout();
        Assert.True(document.FilterActive);
        // Focus a row the filter KEPT, and capture the container after the
        // narrowing: a row the needle removed has no container to come
        // back to, and the restore would (correctly) fall back to the
        // projection — which would make the "prior element" assertion
        // below about the fallback instead of about the restore.
        string surviving = document.FilteredOutline[0].NodeId;
        Assert.NotNull(surface.OutlineForTests.DeliverFocus(surviving));
        host.UpdateLayout();
        IInputElement? cameFrom = Keyboard.FocusedElement;
        Assert.IsType<CanvasOutlineItem>(cameFrom);

        document.Navigator.WhereAmI();
        host.UpdateLayout();
        // The premise: the chord's contract is that focus lands IN the
        // panel, so this is the state a reader is actually in.
        Assert.True(
            surface.WhereAmIPanelForTests.IsKeyboardFocusWithin,
            "the panel never took focus, so this fact would be about the ladder "
            + "rather than about the panel.");
        Drain(document);

        Assert.True(
            PressKey(surface, Key.Escape, ModifierKeys.None),
            "Escape in the panel must be consumed by the panel.");
        host.UpdateLayout();

        Assert.Null(document.WhereAmIText);
        // The typed needle SURVIVES — destroying it was the defect.
        Assert.Equal("zeta", document.FilterText);
        Assert.True(document.FilterActive);
        Assert.Empty(Lines(document));
        // …and the reader is put back where they came from.
        Assert.Same(cameFrom, Keyboard.FocusedElement);

        // Now the ladder owns it again: the panel is gone, so the next
        // press takes rung 2.
        Assert.True(PressKey(surface, Key.Escape, ModifierKeys.None));
        Assert.Equal(string.Empty, document.FilterText);
        Assert.Equal(
            CanvasAnnouncer.RenderLabel(new CanvasA11yEvent.CanvasFilterCleared(
                (uint)document.Outline.Count)),
            OneLine(document));
    });

    /// <summary>
    /// The same rule with the panel OPEN but the reader elsewhere: the
    /// key is the panel being open, not focus being in it (CD-47).
    /// </summary>
    /// <remarks>
    /// mac's <c>.cancelAction</c> is WINDOW-scoped, so it resolves
    /// whatever the focus arrangement. Keying on focus instead left this
    /// exact hole — an open panel plus an Escape from the projection
    /// destroyed the needle AND left the panel sitting there. Focus
    /// RESTORE is the part that stays locus-dependent: a reader who was
    /// never in the panel must not be moved by dismissing it.
    /// </remarks>
    [Fact]
    public void EscapeDismissesAnOpenPanelEvenWhenTheReaderIsElsewhere() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);

        document.FilterText = "zeta";
        host.UpdateLayout();
        document.Navigator.WhereAmI();
        host.UpdateLayout();
        Assert.NotNull(document.WhereAmIText);

        // Put the reader back in the projection with the panel still up.
        string surviving = document.FilteredOutline[0].NodeId;
        Assert.NotNull(surface.OutlineForTests.DeliverFocus(surviving));
        host.UpdateLayout();
        IInputElement? stayPut = Keyboard.FocusedElement;
        Assert.False(
            surface.WhereAmIPanelForTests.IsKeyboardFocusWithin,
            "the premise is an OPEN panel the reader is NOT in.");
        Assert.Equal(Visibility.Visible, surface.WhereAmIPanelForTests.Visibility);
        Drain(document);

        Assert.True(
            PressKey(surface, Key.Escape, ModifierKeys.None),
            "an open panel takes the press ahead of the ladder however focus "
            + "is arranged.");
        host.UpdateLayout();

        Assert.Null(document.WhereAmIText);
        Assert.Equal("zeta", document.FilterText);
        Assert.Empty(Lines(document));
        // The reader was not moved: dismissing a panel they were not in
        // must not relocate them.
        Assert.Same(stayPut, Keyboard.FocusedElement);

        // And the ladder resumes.
        Assert.True(PressKey(surface, Key.Escape, ModifierKeys.None));
        Assert.Equal(string.Empty, document.FilterText);
    });

    /// <summary>
    /// M1: Right on a card with NO connections still answers — mac
    /// follows unconditionally and so does this (contract C3, CD-48).
    /// </summary>
    [Fact]
    public void ARightArrowOnAConnectionlessLeafStillAnswers() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("nested.canvas");
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);

        // A card with no edges at all — the fixture has none.
        CanvasOutlineRow leaf = document.Outline.First(row => row.NodeId == "free");
        Assert.Empty(document.NeighborsOf(leaf.NodeId));
        Assert.NotNull(surface.OutlineForTests.DeliverFocus(leaf.NodeId));
        host.UpdateLayout();
        Assert.True(surface.ProjectionHasFocus);
        Drain(document);

        Assert.True(
            PressKey(surface, Key.Right, ModifierKeys.None),
            "the follow chord must CONSUME the key and answer, not leave a "
            + "connectionless leaf silent.");
        Assert.Equal(
            Rendered(new CanvasStatusNote.NoConnection(true, null)), OneLine(document));

        Drain(document);
        Assert.True(PressKey(surface, Key.Left, ModifierKeys.None));
        Assert.Equal(
            Rendered(new CanvasStatusNote.NoConnection(false, null)), OneLine(document));

        // The TABLE's Left/Right stay the grid's cell navigation, and the
        // leaf answers there through the verb instead.
        document.Selection.ActiveSurface = CanvasSurfaceKind.Table;
        host.UpdateLayout();
        // Re-establish the premise AFTER the switch: the projection
        // changed under the reader, and without putting the keys back on
        // the new one this half would pass because R2's gate refused —
        // not because the table declines the chord.
        Assert.True(surface.TableForTests.DeliverFocus(leaf.NodeId));
        host.UpdateLayout();
        Assert.True(
            surface.ProjectionHasFocus,
            "the table never took the keys, so the next assertion would hold for "
            + "the wrong reason.");
        Drain(document);
        Assert.False(
            PressKey(surface, Key.Right, ModifierKeys.None),
            "the table's Left/Right belong to the grid's cell navigation.");
        document.Navigator.FollowConnection(forward: true);
        Assert.Equal(
            Rendered(new CanvasStatusNote.NoConnection(true, null)), OneLine(document));
    });

    /// <summary>
    /// M1's other half: claiming the arrows leaves every OTHER keyboard
    /// route to expand/collapse intact — Enter on a group, WPF's own
    /// numpad +/-, and the `ExpandCollapse` pattern a screen reader
    /// drives.
    /// </summary>
    /// <remarks>
    /// VERIFIED rather than assumed: the ruling asked whether +/- really
    /// works on this tree before the contract claimed it as the route.
    /// </remarks>
    [Fact]
    public void ExpandCollapseSurvivesTheArrowsBeingClaimed() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("nested.canvas");
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);
        CanvasOutlineRowViewModel group = Assert.Single(
            surface.OutlineForTests.RootsForTests, row => row.Id == "outer");
        Assert.NotNull(surface.OutlineForTests.DeliverFocus("outer"));
        host.UpdateLayout();
        Assert.True(group.IsExpanded);

        // The focused CONTAINER is where WPF's own tree keys land, and
        // it is what a real press bubbles up from.
        UIElement row = Assert.IsType<CanvasOutlineItem>(Keyboard.FocusedElement);

        // 1. Enter on a group toggles it, through the one activation
        // seam. The canvas STANDS ASIDE for it — with no mode active the
        // navigator does not consume Enter — and the tree's own bubbling
        // handler does the work, which is the routing this fact is about.
        Assert.False(
            PressKey(surface, Key.Enter, ModifierKeys.None),
            "with no mode running the canvas must leave Enter to the tree.");
        RaiseBubblingKey(row, Key.Enter);
        host.UpdateLayout();
        Assert.False(group.IsExpanded);
        RaiseBubblingKey(row, Key.Enter);
        host.UpdateLayout();
        Assert.True(group.IsExpanded);

        // 2. WPF's own TreeViewItem keys: numpad minus collapses, plus
        // expands. The canvas does not claim them, so they still arrive.
        Assert.False(PressKey(surface, Key.Subtract, ModifierKeys.None));
        RaiseBubblingKey(row, Key.Subtract);
        host.UpdateLayout();
        Assert.False(group.IsExpanded);
        RaiseBubblingKey(row, Key.Add);
        host.UpdateLayout();
        Assert.True(group.IsExpanded);

        // 3. The pattern AT actually drives.
        AutomationPeer tree = UIElementAutomationPeer.CreatePeerForElement(
            surface.OutlineForTests.TreeForTests);
        AutomationPeer groupPeer = tree.GetChildren()
            .First(child => child.GetName().StartsWith("Group \"", StringComparison.Ordinal));
        var expand = (IExpandCollapseProvider)groupPeer.GetPattern(
            PatternInterface.ExpandCollapse);
        expand.Collapse();
        host.UpdateLayout();
        Assert.Equal(ExpandCollapseState.Collapsed, expand.ExpandCollapseState);
        expand.Expand();
        host.UpdateLayout();
        Assert.Equal(ExpandCollapseState.Expanded, expand.ExpandCollapseState);
    });

    /// <summary>
    /// M2: a tunnelling chord must not out-rank the control the reader is
    /// standing on. With a mode active, Enter on the visible CANCEL MODE
    /// button belongs to the BUTTON (contract C1/C6).
    /// </summary>
    [Fact]
    public void EnterOnAFocusedModeButtonActivatesTheButtonNotTheChord() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);
        var committed = false;
        Assert.True(document.Modes.Enter(new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("Core question"),
            () =>
            {
                committed = true;
                return CanvasModeCommitResult.Committed();
            },
            () => new CanvasModeRestoration.BackAt("Core question"))));
        host.UpdateLayout();
        Assert.True(surface.CancelModeForTests.Focus());
        host.UpdateLayout();
        Drain(document);

        // The gate is R2's question — "does a PROJECTION have the keys" —
        // so the button wins by the natural route rather than by being
        // named in a list of control types.
        Assert.False(
            surface.ProjectionHasFocus,
            "the premise: the button has the keys, not the projection.");
        Assert.False(
            PressKey(surface, Key.Enter, ModifierKeys.None),
            "the canvas must stand aside so the focused button gets its own key "
            + "— committing here inverts the user's intent on the exact control "
            + "M6 exists for.");
        Assert.False(committed);
        Assert.True(document.Modes.IsActive);

        // And the button, reached the way WPF reaches it, cancels.
        RaiseBubblingKey(surface.CancelModeForTests, Key.Enter);
        host.UpdateLayout();
        Assert.False(document.Modes.IsActive);
        Assert.False(committed);

        // The same stand-aside protects the filter field, where Return is
        // the field's own key on both platforms.
        Assert.True(document.Modes.Enter(new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("Core question"),
            () =>
            {
                committed = true;
                return CanvasModeCommitResult.Committed();
            },
            () => new CanvasModeRestoration.BackAt("Core question"))));
        Assert.True(surface.FilterFieldForTests.Focus());
        host.UpdateLayout();
        Assert.False(PressKey(surface, Key.Enter, ModifierKeys.None));
        Assert.False(committed);
        Assert.True(document.Modes.IsActive);
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
            () => CanvasModeCommitResult.Committed(),
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
        Assert.Equal(
            $"Filter results: {document.Navigator.FilterSummaryText()}",
            AutomationProperties.GetName(surface.FilterSummaryForTests));
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
                () => CanvasModeCommitResult.Committed(),
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

    /// <summary>
    /// Raise a BUBBLING KeyDown on an element — the phase WPF's own
    /// controls listen in, which is exactly what the canvas's tunnelling
    /// handler must not out-rank.
    /// </summary>
    private static void RaiseBubblingKey(UIElement target, Key key)
    {
        var args = new KeyEventArgs(
            Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(target)
                ?? throw new InvalidOperationException("the element is not in a window."),
            0,
            key)
        {
            RoutedEvent = Keyboard.KeyDownEvent,
        };
        target.RaiseEvent(args);
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
