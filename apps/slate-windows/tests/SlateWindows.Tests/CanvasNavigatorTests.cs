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
/// Contracts C1–C8 and C10–C14.
/// </summary>
public sealed class CanvasNavigatorTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;
    /// <summary>The pane a mode belongs to. Most facts here are not
    /// about panes, so they name an opaque stand-in — but a mode cannot
    /// be entered without one, which is the point (contract C8).</summary>
    private readonly object _modePane = new();

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
        // Three levels: a matching grandparent, a NON-matching parent
        // inside it, and a matching card inside that — the case CD-45's
        // "nearest surviving ancestor" half is about, which a
        // sibling-branch fixture cannot reach.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "nested-filter.canvas"),
            """
            {
              "nodes": [
                {"id":"gran","type":"group","x":0,"y":0,"width":900,"height":700,"label":"Gran zeta"},
                {"id":"mid","type":"group","x":40,"y":40,"width":500,"height":400,"label":"Middle"},
                {"id":"leaf","type":"text","text":"zeta leaf","x":80,"y":80,"width":200,"height":100},
                {"id":"outside","type":"text","text":"plain","x":1200,"y":0,"width":200,"height":100}
              ],
              "edges": []
            }
            """);
        // The mirror of `nested-filter`, and the shape that shows the
        // "no ancestor gap is reachable" claim was false: the OUTER group
        // does not match and the INNER one does, by its own title. Core
        // matches a row on its own title, kind, target, or its
        // ANCESTOR-ONLY group path — ancestor-only, so a parent is never
        // carried by a child — which leaves the inner group surviving
        // with an ancestor that did not.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "promoted.canvas"),
            """
            {
              "nodes": [
                {"id":"container","type":"group","x":0,"y":0,"width":900,"height":700,"label":"Container"},
                {"id":"pocket","type":"group","x":40,"y":40,"width":500,"height":400,"label":"Pocket zeta"},
                {"id":"inPocket","type":"text","text":"plain card","x":80,"y":80,"width":200,"height":100},
                {"id":"apart","type":"text","text":"plain other","x":1200,"y":0,"width":200,"height":100}
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
        document.AnnouncerForTests.FlushForTests();
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
        document.AnnouncerForTests.FlushForTests();
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
    /// never-silent rule, checked by DRIVING each verb and asserting the
    /// EXACT sentence its state owes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The expectation is derived from `ReadRefusal`, the mapping the
    /// verbs route through, so this catches a verb that walks PAST
    /// admission and not a mapping that is wrong — both sides would move
    /// together. `TheReadMappingAnswersEveryLoadState` pins what the
    /// mapping SAYS, per state, independently; the guard-may-not-exercise
    /// -the-mechanism rule is satisfied across the pair. The weaker "did
    /// something speak" it replaced is what let `clearFilter` announce a
    /// count over an empty outline for eight rounds.
    /// </para>
    /// <para>
    /// The verb list is the navigator's public read surface, and it is
    /// DERIVED rather than curated — every public method is either a row
    /// below or a named exclusion carrying its reason, checked by
    /// reflection. It was hand-maintained until codex round 11, and it
    /// was already wrong: `AnnounceFilterCount` was missing, which is a
    /// shipping read path (the filter field's `TextChanged` reaches it on
    /// every keystroke, including while the canvas cannot answer), so a
    /// regression confined to its unreadable branch passed a fact whose
    /// whole claim is that a verb added without a state answer "fails
    /// here by name". **A list in a test is a claim of completeness that
    /// nothing checks** — this branch's own rule, applied last to the
    /// last hand-maintained list.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryReadVerbAnswersInEveryLoadState()
    {
        (string Member, string Name, Action<CanvasNavigator> Run)[] verbs =
        [
            ("NextCard", "nextCard", navigator => navigator.NextCard()),
            ("PreviousCard", "previousCard", navigator => navigator.PreviousCard()),
            ("EnterGroup", "enterGroup", navigator => navigator.EnterGroup()),
            ("ExitGroup", "exitGroup", navigator => navigator.ExitGroup()),
            ("FollowConnection", "followForward",
                navigator => navigator.FollowConnection(forward: true)),
            ("FollowConnection", "followBack",
                navigator => navigator.FollowConnection(forward: false)),
            ("TracePath", "tracePath", navigator => navigator.TracePath()),
            ("WhereAmI", "whereAmI", navigator => navigator.WhereAmI()),
            ("FilterCards", "filterCards", navigator => navigator.FilterCards()),
            ("ClearFilter", "clearFilter", navigator => navigator.ClearFilter()),
            ("AnnounceFilterCount", "announceFilterCount",
                navigator => navigator.AnnounceFilterCount()),
        ];

        // NOT read verbs, each with the reason it is not — and each must
        // be FOUND, so an exclusion for a member that has gone fails here
        // rather than quietly excusing nothing.
        (string Member, string Why)[] notReadVerbs =
        [
            ("FilterSummaryText",
                "a LABEL, not an announcement: it returns the string the "
                + "summary region shows, from the same view and the same "
                + "mapping, and `TheFilterSummaryNeverCountsRowsTheSurfaceIsNotShowing` "
                + "pins it."),
            ("CommitMode",
                "an M2 mode transition. It answers through the mode "
                + "stack's own vocabulary and its states are the stack's, "
                + "not the load mapping's (contract C7)."),
            ("CancelMode",
                "the same, for M2's other exit."),
        ];

        string[] publicSurface =
        [
            .. typeof(CanvasNavigator)
                .GetMethods(
                    System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];
        Assert.True(publicSurface.Length >= verbs.Length - 1, "the scrape found nothing");
        string[] accountedFor =
        [
            .. verbs.Select(verb => verb.Member)
                .Concat(notReadVerbs.Select(excluded => excluded.Member))
                .Distinct(StringComparer.Ordinal),
        ];
        string[] unaccounted =
            [.. publicSurface.Except(accountedFor, StringComparer.Ordinal)];
        Assert.True(
            unaccounted.Length == 0,
            "a PUBLIC member of the navigator is neither exercised in every "
            + "load state below nor named as something else, so the claim "
            + "that a verb without a state answer fails here by name is not "
            + $"true: {string.Join(", ", unaccounted)}");
        string[] staleExclusions =
        [
            .. notReadVerbs.Select(excluded => excluded.Member)
                .Except(publicSurface, StringComparer.Ordinal),
        ];
        Assert.True(
            staleExclusions.Length == 0,
            "an exclusion names a member the navigator no longer has, so it "
            + $"is excusing nothing: {string.Join(", ", staleExclusions)}");

        foreach ((string label, Func<CanvasDocumentViewModel> open) in UnreadableStates())
        {
            foreach ((_, string name, Action<CanvasNavigator> run) in verbs)
            {
                CanvasDocumentViewModel document = open();
                // An ACTIVE needle, so `clearFilter` has something to
                // clear and the two routes to clearing can be compared
                // in a state where neither can answer.
                document.FilterText = "zeta";
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
                if (name == "clearFilter")
                {
                    // THE EFFECT HAPPENED. Admission chooses the
                    // sentence, never whether the user's request runs:
                    // the visible command used to return from admission
                    // before clearing, so during a reload it announced
                    // "Opening canvas…" and left the needle in the field
                    // while the Escape rung cleared it — two routes to
                    // one operation disagreeing in exactly the window
                    // C4 and C10 were fixed for.
                    Assert.Equal(string.Empty, document.FilterText);
                }
            }
        }
    }

    /// <summary>
    /// TYPING in the filter field answers per load state too — the same
    /// mapping, reached the way a reader reaches it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// `AnnounceFilterCount` is a public read verb, and the fact above
    /// now drives it in every unreadable state. This one drives its
    /// PRODUCTION trigger: the field's `TextChanged`, which fires on
    /// every keystroke including while the canvas is reloading or has
    /// failed. The two are not the same evidence — the verb fact calls
    /// the navigator directly, and what a reader actually does is type.
    /// </para>
    /// <para>
    /// The gap this closes was narrow and real: the field-wiring fact
    /// drives only a READY canvas, and the reload fact checks the summary
    /// LABEL rather than the announcement, so a regression confined to
    /// the unreadable branch of the count had nothing looking at it.
    /// </para>
    /// </remarks>
    [Fact]
    public void TypingInTheFieldAnswersInEveryLoadState() => RunSta(() =>
    {
        foreach ((string label, Func<CanvasDocumentViewModel> open) in UnreadableStates())
        {
            CanvasDocumentViewModel document = open();
            var surface = new CanvasSurfaceView
            {
                Model = document,
                DataContext = new object(),
            };
            using var host = Host(surface);
            CanvasStatusNote expected = Assert.IsType<CanvasStatusNote>(
                document.ReadRefusal, exactMatch: false);
            Drain(document);

            // The reader types. Nothing else — no verb, no chord.
            surface.FilterFieldForTests.Text = "zeta";
            host.UpdateLayout();

            Assert.Equal("zeta", document.FilterText);
            Assert.Equal(
                Rendered(expected),
                Assert.Single(Lines(document), line => true));
            document.Shutdown();
        }
    });

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
    /// The re-review's B1 concern: the OTHER pane on the same document
    /// never inherits a request the first pane already satisfied.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Making the request durable fixed the palette route and opened
    /// this: two panes share one document, so the pane that could not
    /// satisfy the request kept it pending — and pulled the reader into
    /// ITS filter field the next time it gained the keys or became
    /// visible, minutes later, unasked. That is the same class B1 is
    /// about, one arrangement over, and it is why the request is
    /// ADDRESSED and COMPLETED rather than merely retried.
    /// </para>
    /// <para>
    /// The two panes are given distinct `DataContext`s because that is
    /// what a tab IS to a surface — the owner key A14 already uses.
    /// </para>
    /// </remarks>
    [Fact]
    public void ASatisfiedFilterRequestIsNotInheritedByTheOtherPane() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var tabA = new object();
        var tabB = new object();
        var paneA = new CanvasSurfaceView { Model = document, DataContext = tabA };
        var paneB = new CanvasSurfaceView { Model = document, DataContext = tabB };
        var root = new StackPanel();
        root.Children.Add(paneA);
        root.Children.Add(paneB);
        using var host = Host(root);

        // The reader is in pane A, so that is the pane the verb is for.
        paneA.OutlineForTests.FocusTree();
        host.UpdateLayout();
        document.Navigator.FilterCards();
        host.UpdateLayout();
        Assert.True(
            paneA.FilterFieldForTests.IsKeyboardFocused,
            "the premise: the pane the reader was in satisfied the request.");
        // Completed on the DOCUMENT, or the peer pane is still holding it.
        Assert.Null(document.FilterFocusRequest);

        // …and paneA does not drag its OWN reader back either: a request
        // that is not completed is inherited by every pane, its own
        // included, which is the same defect without a second window to
        // notice it in.
        paneA.OutlineForTests.FocusTree();
        host.UpdateLayout();
        Assert.False(
            paneA.FilterFieldForTests.IsKeyboardFocused,
            "a satisfied request must not re-fire on the pane that satisfied it.");

        // Now the reader goes to pane B of their own accord. Nothing may
        // move them again.
        paneB.OutlineForTests.FocusTree();
        host.UpdateLayout();
        Assert.False(
            paneB.FilterFieldForTests.IsKeyboardFocused,
            "the OTHER pane inherited a request that was already satisfied and "
            + "moved the reader into its filter field — a focus move nobody "
            + "asked for (contract C10/A14).");
        Assert.True(paneB.OutlineForTests.TreeForTests.IsKeyboardFocusWithin);

        // --- And the ADDRESS carries its own weight, which completion
        // cannot: a request raised for a pane that is not showing must
        // not be taken by the pane that is. The reader switched away
        // from A's tab, asked for the filter from the palette, then
        // clicked into B — B is eligible, visible and holding the keys,
        // and the request is still not B's.
        paneA.OutlineForTests.FocusTree();
        host.UpdateLayout();
        paneA.Visibility = Visibility.Collapsed;
        host.UpdateLayout();
        document.Navigator.FilterCards();
        // THE PREMISE of everything below: the request really is
        // addressed to the hidden pane. Without this the assertions
        // below hold for the wrong reason on any WPF build that moves
        // focus somewhere unexpected when the focused element collapses.
        CanvasFilterFocusRequest pending =
            Assert.IsType<CanvasFilterFocusRequest>(document.FilterFocusRequest);
        Assert.Same(tabA, pending.Owner);

        paneB.OutlineForTests.FocusTree();
        host.UpdateLayout();
        Assert.False(
            paneB.FilterFieldForTests.IsKeyboardFocused,
            "a request addressed to another pane was answered by this one — "
            + "the reader asked for A's filter and landed in B's.");
        Assert.NotNull(document.FilterFocusRequest);

        // …and it is still waiting for the pane it was for.
        paneA.Visibility = Visibility.Visible;
        host.UpdateLayout();
        paneA.OutlineForTests.FocusTree();
        host.UpdateLayout();
        Assert.True(
            paneA.FilterFieldForTests.IsKeyboardFocused,
            "the addressed pane must still be able to satisfy it when it can.");
        Assert.Null(document.FilterFocusRequest);
    });

    /// <summary>
    /// A cancel RESTORATION that retires the document composes no
    /// cancellation — the announce boundary, on the verb the first fix
    /// did not gate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// `Cancel` builds its sentence as
    /// `new CanvasModeCancelled(spec.Mode, spec.OnCancel())`, so the
    /// restoration — arbitrary host code — runs while the ARGUMENT is
    /// being built, and can close the tab from inside itself exactly as
    /// a commit effect can. The entry gate cannot see that: it ran
    /// before the effect. Only a boundary at emit time can.
    /// </para>
    /// <para>
    /// Latent on this branch and not on the next one: C ships a TEST
    /// mode, and PR F ships restorations that move focus and touch the
    /// shell. The first fix gated `Commit`'s confirmation — the one site
    /// the failing test walked — and a per-verb gate is a list somebody
    /// has to keep complete.
    /// </para>
    /// </remarks>
    [Fact]
    public void ACancelRestorationThatRetiresTheDocumentComposesNothing()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var spec = new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("Research"),
            CanvasModeCommitResult.Refused,
            () =>
            {
                // The restoration retires the document — the shell
                // closing the tab as the mode unwinds.
                document.Shutdown();
                return new CanvasModeRestoration.BackAt("Research");
            });
        Assert.True(document.Modes.Enter(spec, _modePane));
        Drain(document);

        Assert.True(document.Modes.Cancel());

        // The mode ended, and NOTHING was composed after the funnel
        // closed. The counter is the half that works in Release too:
        // `Debug.Fail` says nothing there, and Release is what CI runs.
        Assert.False(document.Modes.IsActive);
        Assert.Equal(0, document.AnnouncerForTests.RefusedAfterShutdownForTests);
        Assert.Empty(Lines(document));
    }

    /// <summary>
    /// Escape from the filter field never strands the reader, in a state
    /// where NOTHING renders rows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// `Render` collapses both projections under `Loading` and under
    /// every failure state, and the restoration focused the projection
    /// unconditionally and ignored the result — so the keys stayed on
    /// the window root while the rung had already consumed the press,
    /// and the transient-region dismissal ate the ones after it. A
    /// keyboard reader in the filter field of a loading canvas had no
    /// way out.
    /// </para>
    /// <para>
    /// Two states, because they end differently and both are reachable:
    /// a FAILURE state has a focusable banner to land on, and `Loading`
    /// deliberately has none — a transient "Opening canvas…" is not
    /// somewhere to put a reader — so the restoration leaves an
    /// addressed A14 landing that the publish will deliver.
    /// </para>
    /// </remarks>
    [Fact]
    public void EscapeFromTheFilterFieldNeverStrandsTheReader() => RunSta(() =>
    {
        // --- A terminal failure: the banner is the tab stop.
        CanvasDocumentViewModel broken = Open("broken.canvas");
        var tabA = new object();
        var brokenSurface = new CanvasSurfaceView { Model = broken, DataContext = tabA };
        using (HostedWindow host = Host(brokenSurface))
        {
            Assert.Equal(CanvasLoadState.ParseError, broken.State);
            Assert.True(
                brokenSurface.FilterFieldForTests.Focus(),
                "premise: brokenSurface.FilterField refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();

            Assert.True(
                PressKey(brokenSurface, Key.Escape, ModifierKeys.None),
                "premise: the press was not consumed, so the rung under test never ran.");
            host.UpdateLayout();
            Assert.False(
                brokenSurface.FilterFieldForTests.IsKeyboardFocused,
                "Escape must take the reader OUT of the filter field.");
            Assert.True(
                brokenSurface.StateBannerForTests.IsKeyboardFocused,
                "…and onto the region this state actually shows.");
        }
        broken.Shutdown();

        // --- Loading: nothing is focusable, so the request is durable.
        var loading = new CanvasDocumentViewModel(
            _session,
            "board.canvas",
            new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
            synchronousForTests: true,
            verbosity: () => _verbosity);
        var tabB = new object();
        var surface = new CanvasSurfaceView { Model = loading, DataContext = tabB };
        using (HostedWindow host = Host(surface))
        {
            Assert.Equal(CanvasLoadState.Loading, loading.State);
            Assert.True(
                surface.FilterFieldForTests.Focus(),
                "premise: surface.FilterField refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();

            Assert.True(
                PressKey(surface, Key.Escape, ModifierKeys.None),
                "premise: the press was not consumed, so the rung under test never ran.");
            host.UpdateLayout();
            // The reader STAYS in the field, and that is the right
            // answer rather than a shortfall: there is nowhere better on
            // a canvas with no rows and no focusable banner, and a
            // focusable text box beats the window root — which is where
            // the old unconditional restoration left them. What must NOT
            // happen is the press being spent with nothing to follow it.
            Assert.True(surface.FilterFieldForTests.IsKeyboardFocused);
            // So a landing is pending, addressed to this tab: the way
            // out exists and the publish delivers it.
            CanvasFocusRequest pending =
                Assert.IsType<CanvasFocusRequest>(loading.FocusRequest);
            Assert.Same(tabB, pending.Owner);

            // …and the publish that ends the load seats them.
            loading.Load();
            host.UpdateLayout();
            Assert.Null(loading.FocusRequest);
            Assert.True(surface.OutlineForTests.TreeForTests.IsKeyboardFocusWithin);
        }
        loading.Shutdown();
    });

    /// <summary>
    /// A newer request supersedes an older one, and the older one's late
    /// delivery cannot clear it.
    /// </summary>
    /// <remarks>
    /// Supersession went untested through the change that replaced the
    /// generation counter with a reference comparison — the mechanism
    /// changed and the property it exists for had no fact. Raising
    /// OVERWRITES, and completion compares the pending RECORD by
    /// reference, so a surface that finally delivers request A after B
    /// has been raised reports A and clears nothing.
    /// </remarks>
    [Fact]
    public void ALateDeliveryOfASupersededRequestClearsNothing()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var tabA = new object();
        var tabB = new object();

        // The SAME owner twice, which is the case that matters: the two
        // requests are VALUE-EQUAL and distinct instances, so a
        // comparison by value cannot tell the superseded one from the
        // live one — the ABA window that dropping the generation counter
        // opened, and the reason completion compares identity.
        document.RequestFocusLanding(tabA);
        CanvasFocusRequest first =
            Assert.IsType<CanvasFocusRequest>(document.FocusRequest);
        document.RequestFocusLanding(tabA);
        CanvasFocusRequest second =
            Assert.IsType<CanvasFocusRequest>(document.FocusRequest);
        Assert.NotSame(first, second);
        Assert.Equal(first, second);

        // The FIRST request's surface finally delivers.
        document.CompleteFocusLanding(first);
        Assert.Same(
            second,
            document.FocusRequest);

        // …and the live one still completes normally.
        document.CompleteFocusLanding(second);
        Assert.Null(document.FocusRequest);

        // The filter twin, same shape and the same value-equal pair.
        document.RequestFilterFocus(tabB);
        CanvasFilterFocusRequest firstFilter =
            Assert.IsType<CanvasFilterFocusRequest>(document.FilterFocusRequest);
        document.RequestFilterFocus(tabB);
        CanvasFilterFocusRequest secondFilter =
            Assert.IsType<CanvasFilterFocusRequest>(document.FilterFocusRequest);
        document.CompleteFilterFocus(firstFilter);
        Assert.Same(secondFilter, document.FilterFocusRequest);
        document.Shutdown();
    }

    /// <summary>
    /// Invoking the verb again while an IDENTICAL request is pending
    /// still reaches the surfaces.
    /// </summary>
    /// <remarks>
    /// The requests are records, so value equality made a re-raise a
    /// silent no-op: `SetField` saw "no change" and the property
    /// notification the surfaces deliver on never fired. That was
    /// impossible while a generation counter made every request
    /// distinct, and became reachable the moment the counter was dropped
    /// for a reference comparison — a change to the completion side that
    /// silently altered the notification side. Change detection is by
    /// reference now, so the two agree.
    /// </remarks>
    [Fact]
    public void ReInvokingTheFilterVerbStillReachesTheSurfaces()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var tab = new object();
        int notifications = 0;
        document.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CanvasDocumentViewModel.FilterFocusRequest))
            {
                notifications++;
            }
        };

        document.RequestFilterFocus(tab);
        Assert.Equal(1, notifications);
        // The SAME owner, the same everything — a reader pressing Ctrl+F
        // twice because the first press appeared to do nothing.
        document.RequestFilterFocus(tab);
        Assert.Equal(
            2,
            notifications);

        // And raising the identical INSTANCE is still silent, which is
        // the case the equality check exists for.
        CanvasFilterFocusRequest pending =
            Assert.IsType<CanvasFilterFocusRequest>(document.FilterFocusRequest);
        document.CompleteFilterFocus(pending);
        Assert.Equal(3, notifications);
        document.CompleteFilterFocus(pending);
        Assert.Equal(3, notifications);
        document.Shutdown();
    }

    /// <summary>
    /// A restoration this surface deferred for itself is WITHDRAWN when
    /// the reader leaves — the publish seats nobody.
    /// </summary>
    /// <remarks>
    /// The Escape-in-`Loading` fix leaves a durable A14 landing because
    /// there is nowhere to put the reader yet. That landing is a
    /// RESTORATION, not an instruction: it exists only because the
    /// reader was already here. If they go somewhere else while the load
    /// runs, delivering it drags them back out of wherever they chose —
    /// which is the class A14 exists to prevent, reintroduced by the fix
    /// for a different one. A shell-raised landing is untouched: that
    /// one IS an instruction to put the reader in this tab.
    /// </remarks>
    [Fact]
    public void ADeferredRestorationIsWithdrawnWhenTheReaderLeaves() => RunSta(() =>
    {
        var loading = new CanvasDocumentViewModel(
            _session,
            "board.canvas",
            new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
            synchronousForTests: true,
            verbosity: () => _verbosity);
        var tab = new object();
        var surface = new CanvasSurfaceView { Model = loading, DataContext = tab };
        var elsewhere = new Button { Content = "the reader goes here" };
        var root = new StackPanel();
        root.Children.Add(surface);
        root.Children.Add(elsewhere);
        using var host = Host(root);

        Assert.Equal(CanvasLoadState.Loading, loading.State);
        Assert.True(
            surface.FilterFieldForTests.Focus(),
            "premise: surface.FilterField refused keyboard focus, so this arrangement never established.");
        host.UpdateLayout();
        Assert.True(
            PressKey(surface, Key.Escape, ModifierKeys.None),
            "premise: the press was not consumed, so the rung under test never ran.");
        host.UpdateLayout();
        // The premise: nowhere to sit, so a restoration is pending.
        Assert.NotNull(loading.FocusRequest);

        // The reader goes somewhere else of their own accord.
        Assert.True(
            elsewhere.Focus(),
            "premise: elsewhere refused keyboard focus, so this arrangement never established.");
        host.UpdateLayout();
        Assert.Null(
            loading.FocusRequest);

        // …and the publish that ends the load leaves them there.
        loading.Load();
        host.UpdateLayout();
        Assert.True(
            elsewhere.IsKeyboardFocused,
            "a load that finished after the reader moved must not pull them "
            + "back into the canvas (contract A14).");
        loading.Shutdown();
    });

    /// <summary>
    /// The other half of the same distinction: a restoration is HELD
    /// while the reader is behind something layered over this tab, and
    /// delivered when they come back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The withdrawal above made the restoration/instruction distinction
    /// exist on the WITHDRAWAL side only, which is half a distinction.
    /// The three departures `Depart` deliberately does not withdraw on —
    /// an overlay, a menu, a window deactivation — are things the reader
    /// is coming BACK from, so the landing is rightly kept; but keeping
    /// it while delivery was unconditional meant a load that finished
    /// behind the overlay seated the reader in a canvas they were not
    /// looking at, taking the keys off the dialog they were typing in.
    /// </para>
    /// <para>
    /// This is a shape this branch has built once already: read-side and
    /// write-side terminality on the request properties. A rule that
    /// governs one side of a lifecycle governs the other side too, or it
    /// governs neither.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(RestorationHold.Overlay)]
    [InlineData(RestorationHold.Menu)]
    [InlineData(RestorationHold.WindowDeactivation)]
    public void ARestorationWaitsWhileTheReaderIsBehindSomethingElse(
        RestorationHold hold) => RunSta(() =>
    {
        Func<bool> overlayWas = CanvasSurfaceView.ShellOverlayIsOpen;
        try
        {
            var loading = new CanvasDocumentViewModel(
                _session,
                "board.canvas",
                new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
                synchronousForTests: true,
                verbosity: () => _verbosity);
            var tab = new object();
            var surface = new CanvasSurfaceView { Model = loading, DataContext = tab };
            MenuItem item = MenuRow("Canvas");
            Menu menu = MenuThatTakesTheKeys(item);
            var elsewhere = new Button { Content = "the overlay's own field" };
            var root = new StackPanel();
            root.Children.Add(surface);
            root.Children.Add(menu);
            root.Children.Add(elsewhere);
            using ProbeWindow host = HostProbe(root);

            Assert.Equal(CanvasLoadState.Loading, loading.State);
            Assert.True(
                surface.FilterFieldForTests.Focus(),
                "premise: surface.FilterField refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();
            Assert.True(
                PressKey(surface, Key.Escape, ModifierKeys.None),
                "premise: the press was not consumed, so the rung under test never ran.");
            host.UpdateLayout();
            // The premise: nowhere to sit yet, so a RESTORATION is
            // pending — the same premise the withdrawal fact establishes.
            Assert.NotNull(loading.FocusRequest);

            // The thing layers itself over the tab.
            switch (hold)
            {
                case RestorationHold.Overlay:
                    CanvasSurfaceView.ShellOverlayIsOpen = static () => true;
                    Assert.True(
                        elsewhere.Focus(),
                        "premise: elsewhere refused keyboard focus, so this arrangement never established.");
                    break;
                case RestorationHold.Menu:
                    PutTheKeysInTheMenu(item, "the hold's menu arm");
                    break;
                default:
                    host.SimulateDeactivate();
                    break;
            }
            host.UpdateLayout();
            Assert.NotNull(loading.FocusRequest);

            // The load finishes WHILE they are behind it.
            loading.Load();
            host.UpdateLayout();
            Assert.False(
                surface.ProjectionHasFocus,
                "the canvas seated the reader while they were behind an "
                + "overlay, a menu or another window — a restoration is not "
                + "an instruction (contract A14).");
            Assert.NotNull(loading.FocusRequest);

            // …and it comes back.
            CanvasSurfaceView.ShellOverlayIsOpen = overlayWas;
            if (hold == RestorationHold.WindowDeactivation)
            {
                host.SimulateActivate();
            }
            else
            {
                // Discarded deliberately: focus returning to the surface
                // is what makes the held landing deliverable, and the
                // delivery takes the keys straight back off the field —
                // so `Focus` reports false precisely BECAUSE the fix
                // worked. The assertion below is the one that matters.
                _ = surface.FilterFieldForTests.Focus();
            }
            host.UpdateLayout();
            Assert.True(
                surface.ProjectionHasFocus,
                "the reader came back and the landing this surface deferred "
                + "for them was never delivered — held is not dropped.");
            Assert.Null(loading.FocusRequest);
            loading.Shutdown();
        }
        finally
        {
            CanvasSurfaceView.ShellOverlayIsOpen = overlayWas;
        }
    });

    /// <summary>What a held restoration is waiting behind.</summary>
    public enum RestorationHold
    {
        Overlay,
        Menu,
        WindowDeactivation,
    }

    /// <summary>
    /// A held restoration is WITHDRAWN when the cause ends and the reader
    /// turns out to have gone somewhere else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The hold's edge is written by a departure FROM this surface, and a
    /// departure only fires when this surface loses focus. So the move
    /// that ends the cause is invisible to it: open a menu, then click
    /// into another pane, and this surface hears nothing the second time.
    /// The landing was then held for the pane's lifetime — never
    /// delivered, never withdrawn, never completed. STARVATION, and the
    /// exact mirror of the theft the hold was added to prevent.
    /// </para>
    /// <para>
    /// Recorded as one pairing rather than two fixes: a lifecycle rule
    /// that answers only one failure direction answers neither properly,
    /// which is the third time this branch has met that shape (read-side
    /// vs write-side terminality, withdrawal vs delivery, and now hold vs
    /// release). The repair watches the WINDOW, because the destination
    /// is the thing the surface cannot see.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(RestorationHold.Menu)]
    [InlineData(RestorationHold.Overlay)]
    public void AHeldRestorationIsWithdrawnWhenTheReaderTurnsOutToHaveLeft(
        RestorationHold cause) => RunSta(() =>
    {
        Func<bool> overlayWas = CanvasSurfaceView.ShellOverlayIsOpen;
        try
        {
            var loading = new CanvasDocumentViewModel(
                _session,
                "board.canvas",
                new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
                synchronousForTests: true,
                verbosity: () => _verbosity);
            var tab = new object();
            var pane = new CanvasSurfaceView { Model = loading, DataContext = tab };
            // In-window, which is what a WPF menu is. A menu hosted in a
            // SECOND window makes the OS deactivate this one first, and
            // the arrangement would then be about `WindowDeactivated`
            // rather than about the menu.
            MenuItem item = MenuRow("Canvas");
            Menu menu = MenuThatTakesTheKeys(item);
            var behindIt = new Button { Content = "the overlay's own field" };
            var anotherPane = new Button { Content = "another pane" };
            var root = new StackPanel();
            root.Children.Add(pane);
            root.Children.Add(menu);
            root.Children.Add(behindIt);
            root.Children.Add(anotherPane);
            using ProbeWindow host = HostProbe(root);
            // TRANSIT, not end state: the false-green this arrangement
            // replaced worked by bouncing the keys back through this
            // surface, and an end-state check cannot see an arrival that
            // has already left. Counting the arrivals can.
            var returns = 0;
            pane.IsKeyboardFocusWithinChanged += (_, changed) =>
            {
                if (changed.NewValue is true)
                {
                    returns++;
                }
            };

            // A restoration, deferred the ordinary way: Escape in
            // `Loading` with nowhere to sit.
            Assert.True(
                pane.FilterFieldForTests.Focus(),
                "premise: pane.FilterField refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();
            Assert.True(
                PressKey(pane, Key.Escape, ModifierKeys.None),
                "premise: the press was not consumed, so the rung under test never ran.");
            host.UpdateLayout();
            Assert.NotNull(loading.FocusRequest);

            // The reader goes behind something they come back from, so
            // the landing is HELD rather than withdrawn.
            if (cause == RestorationHold.Menu)
            {
                PutTheKeysInTheMenu(item, "the starvation fact's menu arm");
            }
            else
            {
                CanvasSurfaceView.ShellOverlayIsOpen = static () => true;
                Assert.True(
                    behindIt.Focus(),
                    "premise: behindIt refused keyboard focus, so this arrangement never established.");
            }
            host.UpdateLayout();
            Assert.NotNull(loading.FocusRequest);

            // …and then it ends somewhere ELSE: the reader clicks
            // straight into another pane. The keys go from the menu item
            // (or the overlay's field) to `anotherPane` WITHOUT passing
            // through this surface, which is the whole difficulty — this
            // surface had already lost focus when the cause began, so it
            // hears nothing at all about the move that ends it.
            //
            // The menu is deliberately left OPEN: closing it first
            // bounces focus back through this pane, and the arm would
            // then pass through the ordinary withdrawal path while
            // proving nothing about the starvation. (It did, for one
            // revision, and the mutation battery is what caught it.)
            int returnsBefore = returns;
            CanvasSurfaceView.ShellOverlayIsOpen = overlayWas;
            Assert.True(
                anotherPane.Focus(),
                "premise: anotherPane refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();
            Assert.Equal(returnsBefore, returns);

            Assert.Null(
                loading.FocusRequest);
            Assert.False(
                loading.HoldsPendingRequestsForTests,
                "the landing is neither delivered nor withdrawn — it is "
                + "starving, and it holds the tab it is addressed to with it.");

            // The load finishes and finds nothing to seat, which is the
            // consumer-visible half: the reader stays where they chose.
            loading.Load();
            host.UpdateLayout();
            Assert.True(
                anotherPane.IsKeyboardFocused,
                "the publish pulled the reader back into a canvas they had "
                + "left by way of a menu (contract A14).");
            Assert.False(pane.ProjectionHasFocus);
            loading.Shutdown();
        }
        finally
        {
            CanvasSurfaceView.ShellOverlayIsOpen = overlayWas;
        }
    });

    /// <summary>
    /// Where the KEYS are when the mode is entered — the arrangement's
    /// focus locus, not the route the entry arrived by.
    /// </summary>
    /// <remarks>
    /// Named for the locus deliberately. Every arm below drives
    /// `Navigator.EnterMode` directly, which is the SEAM; there is no
    /// production entrant at this tip to route through, and the shell
    /// helper a palette row would reach discards the command parameter
    /// entirely. Calling these "Palette" and "Menu" made the theory read
    /// as covering two routes it has never touched — so they say what
    /// they actually vary, which is where the reader's keys are sitting
    /// when the owner is recorded.
    /// </remarks>
    public enum ModeEntryLocus
    {
        KeysOnTheProjection,
        KeysInThePalette,
        KeysInAMenu,
    }

    /// <summary>
    /// A mode belongs to the PANE it was entered from, and a sibling pane
    /// showing the same document may not end it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mode stack is document-shared: one controller, however many
    /// panes show the document. The window watch is per-SURFACE and asks
    /// "is a mode active" — a fact about the document — then acts as
    /// though the answer were about itself. So with two panes on one
    /// canvas and the mode entered from A, every focus movement inside A
    /// (returning from the palette, clicking A's own Commit or Cancel
    /// button) fired B's watcher, which saw a mode active and the keys
    /// outside B, and cancelled the mode its reader was in the middle of
    /// driving — before they could reach the M6 controls that exist for
    /// exactly that moment.
    /// </para>
    /// <para>
    /// THE THIRD AFFINITY. A14's landing carries its OWNER; the
    /// navigator's presenter carries the pane the reader is in; a mode
    /// now carries the pane it was entered from. The rule underneath all
    /// three is one rule: <b>anything document-shared that a per-surface
    /// mechanism acts on needs to say WHICH SURFACE, because "the
    /// document has one" and "this pane owns it" are different facts and
    /// only the second licenses acting.</b> The owner comes from the
    /// INVOCATION — `EnterMode` names the pane — so the keys-in-a-palette
    /// and keys-in-a-menu ARRANGEMENTS below, where the keys are
    /// elsewhere when the owner is recorded, belong to the pane that
    /// asked rather than to whichever pane held them last.
    /// This fact drives the NAVIGATOR SEAM; see the PR F hand-off row in
    /// §C for what still has to be carried through the routed-command
    /// boundary when real entrants arrive.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(ModeEntryLocus.KeysOnTheProjection)]
    [InlineData(ModeEntryLocus.KeysInThePalette)]
    [InlineData(ModeEntryLocus.KeysInAMenu)]
    public void AModeBelongsToThePaneItWasEnteredFrom(ModeEntryLocus locus) =>
        RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var restorations = 0;
        var spec = new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("Research"),
            CanvasModeCommitResult.Refused,
            () =>
            {
                restorations++;
                return new CanvasModeRestoration.BackAt("Research");
            });
        var owner = new CanvasSurfaceView { Model = document, DataContext = new object() };
        var peer = new CanvasSurfaceView { Model = document, DataContext = new object() };
        var palette = new TextBox();
        MenuItem item = MenuRow("Commit mode");
        Menu menu = MenuThatTakesTheKeys(item);
        var root = new StackPanel();
        root.Children.Add(owner);
        root.Children.Add(peer);
        root.Children.Add(palette);
        root.Children.Add(menu);
        using var host = Host(root);

        try
        {
            // The reader is in pane A. This is what makes it the pane the
            // navigator — and therefore the mode — calls theirs.
            Assert.True(
                owner.FilterFieldForTests.Focus(),
                "premise: owner.FilterField refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();

            switch (locus)
            {
                case ModeEntryLocus.KeysInThePalette:
                    Assert.True(
                        palette.Focus(),
                        "premise: palette refused keyboard focus, so this arrangement never established.");
                    break;
                case ModeEntryLocus.KeysInAMenu:
                    PutTheKeysInTheMenu(item, "the ownership theory's menu locus");
                    break;
                default:
                    Assert.True(
                        owner.FocusRow(document.FilteredOutline[0].NodeId),
                        "premise: the row never took focus, so the reader is not on the projection.");
                    break;
            }
            host.UpdateLayout();

            Assert.True(document.Navigator.EnterMode(spec, owner));
            host.UpdateLayout();
            Assert.True(document.Modes.IsActive);

            // The reader moves WITHIN their own pane — coming back from
            // the palette or the menu, or stepping from the projection to
            // the field. Every one of these raises the window's focus
            // event, which every pane hears.
            Assert.True(
                owner.FilterFieldForTests.Focus(),
                "premise: owner.FilterField refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();
            Assert.True(
                document.Modes.IsActive,
                "a sibling pane cancelled the mode because the reader moved "
                + "inside the pane that is running it — `IsActive` is a fact "
                + "about the DOCUMENT, and the peer read it as its own.");
            Assert.Equal(0, restorations);

            // A second movement inside the owner, because the first could
            // have been the returning edge rather than the watch.
            Assert.True(
                owner.FocusRow(document.FilteredOutline[0].NodeId),
                "premise: the row never took focus, so the reader is not on the projection.");
            host.UpdateLayout();
            Assert.True(document.Modes.IsActive);
            Assert.Equal(0, restorations);

            // …and now the reader really does leave, for the pane next
            // door. That ends the mode — ONCE. Two watchers plus the
            // owner's own departure edge are three routes to the same
            // cancellation, and a mode that restores twice has run the
            // reader's undo twice.
            Assert.True(
                peer.FilterFieldForTests.Focus(),
                "premise: peer.FilterField refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();
            Assert.False(document.Modes.IsActive);
            Assert.Equal(1, restorations);
        }
        finally
        {
            document.Shutdown();
        }
    });

    /// <summary>
    /// A mode cannot be entered without a pane to belong to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE PREREQUISITE, on its own. It shared a test with its downstream
    /// consequence for one wave, in an order where the first assertion
    /// short-circuited the second — so the pair proved one thing twice
    /// rather than two things once. The consequence lives in
    /// <see cref="AModeDoesNotOutliveThePaneThatOwnsIt"/>'s `PeerHidden`
    /// arm, which is where a peer failing to end somebody else's mode
    /// belongs.
    /// </para>
    /// <para>
    /// `Owner` reads null for "no mode active", and for one wave it also
    /// read null for "a mode nobody owns" — so every per-surface consumer
    /// had to guess which it was seeing, and the one that guessed
    /// "nobody owns it, so anyone may end it" handed a peer's departure
    /// to a mode unrelated to it. The second reading is gone rather than
    /// handled: the controller REFUSES a null owner, and the production
    /// route cannot be called without naming the invoking pane.
    /// </para>
    /// </remarks>
    [Fact]
    public void AModeCannotBeEnteredWithoutAPane()
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var spec = new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("Research"),
            CanvasModeCommitResult.Refused,
            () => new CanvasModeRestoration.BackAt("Research"));

        _ = Assert.Throws<ArgumentNullException>(
            () => document.Modes.Enter(spec, null!));
        _ = Assert.Throws<ArgumentNullException>(
            () => document.Navigator.EnterMode(spec, null!));
        Assert.False(
            document.Modes.IsActive,
            "a refused entry left a mode behind, which would be the "
            + "ownerless state by another door.");
        document.Shutdown();
    }

    /// <summary>Why a mode entry was turned down.</summary>
    public enum EntryRefusal
    {
        AnotherModeIsRunning,
        TheDocumentIsRetired,
        TheSpecIsMissing,
    }

    /// <summary>
    /// A REFUSED entry changes nothing — least of all which pane the
    /// verbs act on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The attach that makes the invoking pane the reader's pane ran
    /// unconditionally for one wave, ahead of an admission that can say
    /// no. So a second pane asking for a mode while the first pane's was
    /// still running left the mode owned by A and every movement verb
    /// acting through B — the reader hears their position change in a
    /// pane the mode does not belong to — and an entry into a retired
    /// controller left the navigator holding a presenter the terminal
    /// object had just rejected.
    /// </para>
    /// <para>
    /// **Asking for something and being told no must cost nothing.** That
    /// is the general form, and it is worth stating because the defect
    /// was not in either half: the attach is right, the refusal is right,
    /// and only their ORDER was wrong. The attach still happens before
    /// the mode is published — anything reacting to it becoming active
    /// has to see the pane it belongs to — so the fix is the window it
    /// sits in, not the place.
    /// </para>
    /// <para>
    /// All three refusal shapes, because they take different routes out:
    /// an active mode returns false and speaks, retirement returns false
    /// silently, and a missing spec throws. Each asserts the answer AND
    /// that affinity is where it was.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(EntryRefusal.AnotherModeIsRunning)]
    [InlineData(EntryRefusal.TheDocumentIsRetired)]
    [InlineData(EntryRefusal.TheSpecIsMissing)]
    public void ARefusedEntryLeavesAffinityWhereItWas(EntryRefusal refusal) => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        CanvasModeSpec Spec() => new(
            CanvasMode.Move,
            new CanvasModeObject.Card("Research"),
            CanvasModeCommitResult.Refused,
            () => new CanvasModeRestoration.BackAt("Research"));
        var held = new CanvasSurfaceView { Model = document, DataContext = new object() };
        var asking = new CanvasSurfaceView { Model = document, DataContext = new object() };
        var root = new StackPanel();
        root.Children.Add(held);
        root.Children.Add(asking);
        using var host = Host(root);

        try
        {
            // The first pane is the reader's, and the premise says so in
            // the terms the rest of the fact uses.
            Assert.True(
                held.FilterFieldForTests.Focus(),
                "premise: held.FilterField refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();
            Assert.Same(held, document.Navigator.AttachedPresenter);

            if (refusal == EntryRefusal.AnotherModeIsRunning)
            {
                Assert.True(document.Navigator.EnterMode(Spec(), held));
                Assert.True(document.Modes.IsActive);
            }
            if (refusal == EntryRefusal.TheDocumentIsRetired)
            {
                document.Shutdown();
            }
            Drain(document);

            // The SECOND pane asks, and is turned down.
            if (refusal == EntryRefusal.TheSpecIsMissing)
            {
                _ = Assert.Throws<ArgumentNullException>(
                    () => document.Navigator.EnterMode(null!, asking));
            }
            else
            {
                Assert.False(
                    document.Navigator.EnterMode(Spec(), asking),
                    "the entry was admitted, so this arm is not about a "
                    + "refusal at all.");
            }

            Assert.Same(
                held,
                document.Navigator.AttachedPresenter);

            // …and the consequence a reader would actually meet.
            if (refusal != EntryRefusal.TheDocumentIsRetired)
            {
                document.Navigator.NextCard();
                host.UpdateLayout();
                Assert.True(
                    held.ProjectionHasFocus,
                    "a refused entry moved the reader's pane: the verbs now "
                    + "act through the pane that ASKED rather than the pane "
                    + "the reader is in.");
                Assert.False(asking.ProjectionHasFocus);
            }
        }
        finally
        {
            document.Shutdown();
        }
    });

    /// <summary>
    /// Entering a mode from a pane makes that pane the one the verbs act
    /// on.
    /// </summary>
    /// <remarks>
    /// The invocation says which pane the reader is in — that is the
    /// whole reason the owner comes from it — so it would be incoherent
    /// for the mode to belong to one pane while the movement verbs seated
    /// the reader in another. A palette row serves the pane it was opened
    /// over; a menu row serves the pane it dropped from. `EnterMode`
    /// attaches the invoker for the same reason a chord does, and this is
    /// the arrangement where the two panes disagree unless it does: the
    /// OTHER pane held the keys last.
    /// </remarks>
    [Fact]
    public void EnteringAModeFromAPaneMakesItThePaneTheVerbsActOn() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var spec = new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("Research"),
            CanvasModeCommitResult.Refused,
            () => new CanvasModeRestoration.BackAt("Research"));
        var held = new CanvasSurfaceView { Model = document, DataContext = new object() };
        var invoking = new CanvasSurfaceView
        {
            Model = document,
            DataContext = new object(),
        };
        var palette = new TextBox();
        var root = new StackPanel();
        root.Children.Add(held);
        root.Children.Add(invoking);
        root.Children.Add(palette);
        using var host = Host(root);

        try
        {
            // The OTHER pane held the keys last, then a palette took
            // them — the arrangement in which the navigator's cache and
            // the invoker disagree.
            Assert.True(
                held.FilterFieldForTests.Focus(),
                "premise: held.FilterField refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();
            Assert.True(
                palette.Focus(),
                "premise: palette refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();

            Assert.True(document.Navigator.EnterMode(spec, invoking));
            document.Navigator.NextCard();
            host.UpdateLayout();

            Assert.True(
                invoking.ProjectionHasFocus,
                "the mode belongs to one pane while the movement verb seated "
                + "the reader in another — the invocation named the pane and "
                + "the navigator went on serving the pane that held the keys "
                + "last.");
            Assert.False(held.ProjectionHasFocus);
        }
        finally
        {
            document.Shutdown();
        }
    });

    /// <summary>
    /// A live pane can enter a mode after its PEER has gone, without
    /// waiting to regain the keys.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The production entry used to take the owner from the navigator's
    /// attached presenter. That field is a detachable CACHE, not a record
    /// of historical focus: when a pane unloads or is retargeted it
    /// detaches, clearing the slot for the WHOLE document — so the
    /// surviving pane's next mode invocation was refused, on a canvas
    /// that has plainly been focused, from a pane that is plainly live.
    /// The predicate was answering "has any pane held the keys lately",
    /// which is not the question, and no predicate over that cache could
    /// have been.
    /// </para>
    /// <para>
    /// The invoker names ITSELF now. A chord already carries its
    /// presenter; a palette or menu row knows which pane it is serving,
    /// because the shell resolved a canvas tab to put the row in front of
    /// the reader at all. Identity from the invocation is true at the
    /// moment of the call, which is the only moment that matters.
    /// </para>
    /// </remarks>
    [Fact]
    public void ASurvivingPaneCanEnterAModeAfterItsPeerDetaches() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        CanvasDocumentViewModel elsewhere = Open("connected.canvas");
        var spec = new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("Research"),
            CanvasModeCommitResult.Refused,
            () => new CanvasModeRestoration.BackAt("Research"));
        var first = new CanvasSurfaceView { Model = document, DataContext = new object() };
        var survivor = new CanvasSurfaceView
        {
            Model = document,
            DataContext = new object(),
        };
        var root = new StackPanel();
        root.Children.Add(first);
        root.Children.Add(survivor);
        using var host = Host(root);

        try
        {
            // The canvas HAS been focused — in the pane that is about to
            // go away, which is exactly what makes the cache lie.
            Assert.True(
                first.FilterFieldForTests.Focus(),
                "premise: first.FilterField refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();
            first.Model = elsewhere;
            host.UpdateLayout();
            Assert.False(survivor.IsKeyboardFocusWithin);

            Assert.True(
                document.Navigator.EnterMode(spec, survivor),
                "the surviving pane was refused a mode because the pane that "
                + "left took the navigator's cached presenter with it.");
            Assert.True(document.Modes.IsActive);

            // …and it is the SURVIVOR's mode, so the survivor's departure
            // is what ends it.
            survivor.Visibility = Visibility.Collapsed;
            host.UpdateLayout();
            Assert.False(document.Modes.IsActive);
        }
        finally
        {
            document.Shutdown();
            elsewhere.Shutdown();
        }
    });

    /// <summary>How a pane stopped showing the canvas.</summary>
    public enum OwnerExit
    {
        ModelReplaced,
        Closed,
        WindowClosed,
        PeerReplaced,
        PeerHidden,
    }

    /// <summary>
    /// A mode does not outlive the PANE that owns it — not when that
    /// pane's document is replaced under it, and not when it closes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ownership had to follow the lifecycle it names. A rename retargets
    /// pane A from canvas X to canvas Y while pane B keeps X open;
    /// `OnModelChanged` detached A from X's navigator, and X's mode went
    /// on naming a surface that no longer shows it. Nobody was then
    /// entitled to reclassify a departure for that mode — A watches its
    /// new document, B is not the owner — so it survived with no pane at
    /// all, while B rendered the shared Commit and Cancel controls for a
    /// transient state it could apply and did not own. M4 says no mode
    /// survives without focus; this was a mode surviving without a PANE.
    /// </para>
    /// <para>
    /// EXACTLY ONE restoration, counted, for the same reason the sibling
    /// fact counts: the owner's departure edge, its watcher and the
    /// detach transition are three routes to one cancellation, and a mode
    /// that restores twice has run the reader's undo twice.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(OwnerExit.ModelReplaced)]
    [InlineData(OwnerExit.Closed)]
    [InlineData(OwnerExit.WindowClosed)]
    [InlineData(OwnerExit.PeerReplaced)]
    [InlineData(OwnerExit.PeerHidden)]
    public void AModeDoesNotOutliveThePaneThatOwnsIt(OwnerExit exit) => RunSta(() =>
    {
        CanvasDocumentViewModel shared = Open("board.canvas");
        CanvasDocumentViewModel other = Open("connected.canvas");
        var restorations = 0;
        var spec = new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("Research"),
            CanvasModeCommitResult.Refused,
            () =>
            {
                restorations++;
                return new CanvasModeRestoration.BackAt("Research");
            });
        var owner = new CanvasSurfaceView { Model = shared, DataContext = new object() };
        var keepsItOpen = new CanvasSurfaceView
        {
            Model = shared,
            DataContext = new object(),
        };
        var root = new StackPanel();
        root.Children.Add(owner);
        root.Children.Add(keepsItOpen);
        using var host = Host(root);

        try
        {
            Assert.True(
                owner.FilterFieldForTests.Focus(),
                "premise: owner.FilterField refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();
            Assert.True(shared.Navigator.EnterMode(spec, owner));
            host.UpdateLayout();
            Assert.True(shared.Modes.IsActive);
            // The PREMISE: the peer is showing the same document, so the
            // mode's controls are on ITS header too — which is what makes
            // an ownerless mode a thing a reader can still press.
            Assert.Same(shared, keepsItOpen.Model);

            switch (exit)
            {
                case OwnerExit.ModelReplaced:
                    // The rename lands: this pane now shows a different
                    // canvas. The peer keeps the old one open.
                    owner.Model = other;
                    break;
                case OwnerExit.Closed:
                    root.Children.Remove(owner);
                    break;
                case OwnerExit.WindowClosed:
                    // The whole window goes. Unlike removing the element,
                    // this need not change `IsVisible` on the way out, so
                    // it is the arm that reaches the UNLOAD transition
                    // rather than the visibility one.
                    host.Dispose();
                    break;
                default:
                    // The PEER is retargeted. Nothing about the owner
                    // changed, and the mode is not the peer's to end.
                    keepsItOpen.Model = other;
                    break;
                case OwnerExit.PeerHidden:
                    // Never touches the keys — a split pane being
                    // collapsed. This is the arm that found the same
                    // defect one altitude below the one this wave was
                    // sent to fix.
                    keepsItOpen.Visibility = Visibility.Collapsed;
                    break;
            }
            host.UpdateLayout();

            if (exit is OwnerExit.PeerReplaced or OwnerExit.PeerHidden)
            {
                Assert.True(
                    shared.Modes.IsActive,
                    "a pane that is not the owner ended the mode by ceasing "
                    + "to show the canvas — the identity check is what "
                    + "separates 'the owner left' from 'somebody left'.");
                Assert.Equal(0, restorations);
                return;
            }

            Assert.False(
                shared.Modes.IsActive,
                "the mode outlived the pane running it: the peer still "
                + "renders its Commit and Cancel controls, and nothing is "
                + "entitled to end a mode whose owner is gone.");
            Assert.Equal(1, restorations);
        }
        finally
        {
            shared.Shutdown();
            other.Shutdown();
        }
    });

    /// <summary>
    /// A MODE kept alive across a menu is cancelled when the reader turns
    /// out to have left — with no restoration pending anywhere.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mode stack's M4 table survives `ModalOverlay` and `MenuOpen`
    /// precisely because the reader is coming back from them, and the
    /// M6 controls (Commit Mode, Cancel Mode) stay on screen so they can
    /// be clicked. A reader who leaves the menu for another pane instead
    /// has left, and no second event reaches this surface to say so — so
    /// the mode survived in a pane nobody was in, with its controls
    /// showing. M4's own rule, failing in the shape its own exception
    /// created.
    /// </para>
    /// <para>
    /// NOTHING is pending here, which is the whole point: the first
    /// version of the window watch was gated on a deferred restoration,
    /// so this arrangement ran none of it while both records claimed the
    /// mode was covered. The gate serves both constituencies now, because
    /// one classification holds both of them alive.
    /// </para>
    /// </remarks>
    [Fact]
    public void AModeHeldAcrossAMenuIsCancelledWhenTheReaderTurnsOutToHaveLeft() =>
        RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var restored = 0;
        var spec = new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("Research"),
            CanvasModeCommitResult.Refused,
            () =>
            {
                restored++;
                return new CanvasModeRestoration.BackAt("Research");
            });
        var pane = new CanvasSurfaceView { Model = document, DataContext = new object() };
        // The menu is a child of THIS window, which is what a WPF menu is
        // — a popup owned by the window it drops from. Hosting one in a
        // second window (the device the level theory needs for a
        // different reason) makes the OS deactivate this one first, and
        // `WindowDeactivated` CANCELS a mode: the arrangement would then
        // pass through the departure it is supposed to be testing around.
        // That is how the first version of this fact failed, and it is
        // worth the four lines to say so.
        MenuItem item = MenuRow("Commit mode");
        Menu menu = MenuThatTakesTheKeys(item);
        var anotherPane = new Button { Content = "another pane" };
        var root = new StackPanel();
        root.Children.Add(pane);
        root.Children.Add(menu);
        root.Children.Add(anotherPane);
        using var host = Host(root);
        try
        {
            Assert.True(
                pane.FilterFieldForTests.Focus(),
                "premise: pane.FilterField refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();
            Assert.True(document.Navigator.EnterMode(spec, pane));
            host.UpdateLayout();
            Assert.True(document.Modes.IsActive);
            // The PREMISE that makes this fact about the mode: no
            // landing is pending on this surface or this document.
            Assert.Null(document.FocusRequest);
            Assert.False(document.HoldsPendingRequestsForTests);

            // A menu opens over the tab. The mode is KEPT — that is the
            // M4 exception, and the Commit/Cancel controls are why.
            PutTheKeysInTheMenu(item, "the mode-held-across-a-menu fact");
            host.UpdateLayout();
            Assert.True(
                document.Modes.IsActive,
                "the menu cancelled the mode, so the M4 exception this fact "
                + "depends on is not in force and it would prove nothing.");

            // …and the reader clicks straight into another pane.
            Assert.True(
                anotherPane.Focus(),
                "premise: anotherPane refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();

            Assert.False(
                document.Modes.IsActive,
                "the mode outlived the reader: kept alive for a menu they "
                + "left, in a pane they are not in, with its Commit and "
                + "Cancel controls still showing (contract C7's M4 rule).");
            Assert.Equal(1, restored);
        }
        finally
        {
            document.Shutdown();
        }
    });

    /// <summary>
    /// Moving WITHIN an open menu is not the reader going somewhere.
    /// </summary>
    /// <remarks>
    /// The other side of the reclassification above, and the reason it
    /// asks whether the cause has ended before it asks where the keys
    /// went: a menu raises the window's focus event on every item the
    /// reader arrows through. A rule that read each of those as a
    /// destination would withdraw the landing on the first keystroke
    /// inside the menu the reader opened to keep it.
    /// </remarks>
    [Fact]
    public void MovingWithinAnOpenMenuDoesNotWithdrawTheHeldRestoration() => RunSta(() =>
    {
        var loading = new CanvasDocumentViewModel(
            _session,
            "board.canvas",
            new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
            synchronousForTests: true,
            verbosity: () => _verbosity);
        var pane = new CanvasSurfaceView { Model = loading, DataContext = new object() };
        MenuItem first = MenuRow("Commit mode");
        MenuItem second = MenuRow("Cancel mode");
        Menu menu = MenuThatTakesTheKeys(first, second);
        var root = new StackPanel();
        root.Children.Add(pane);
        root.Children.Add(menu);
        using var host = Host(root);

        Assert.True(
            pane.FilterFieldForTests.Focus(),
            "premise: pane.FilterField refused keyboard focus, so this arrangement never established.");
        host.UpdateLayout();
        Assert.True(
            PressKey(pane, Key.Escape, ModifierKeys.None),
            "premise: the press was not consumed, so the rung under test never ran.");
        host.UpdateLayout();
        Assert.NotNull(loading.FocusRequest);

        PutTheKeysInTheMenu(first, "the in-menu fact's first item");
        host.UpdateLayout();
        Assert.NotNull(loading.FocusRequest);

        // The reader arrows to the next item. Same menu, same intention.
        PutTheKeysInTheMenu(second, "the in-menu fact's second item");
        host.UpdateLayout();
        Assert.NotNull(loading.FocusRequest);

        // …and the landing is still THEIRS when they come back.
        _ = pane.FilterFieldForTests.Focus();
        host.UpdateLayout();
        loading.Load();
        host.UpdateLayout();
        Assert.True(
            pane.ProjectionHasFocus,
            "the landing was withdrawn while the reader was still inside the "
            + "menu they opened to keep it.");
        loading.Shutdown();
    });

    /// <summary>Which LEVEL of `RestorationMustWait` answers — named
    /// apart from <see cref="RestorationHold"/> because these three are
    /// arrangements with no departure edge at all.</summary>
    public enum RestorationLevel
    {
        Overlay,
        Menu,
        KeysOutside,
    }

    /// <summary>
    /// Each LEVEL of `RestorationMustWait` answers on its own, with no
    /// departure edge to help — one arm through the production route, two
    /// through the navigator's own seam, labelled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The edge theory above sets `_awayBecause` in all three of its
    /// arms, so the three LEVELS could be deleted without turning a case
    /// red — a guard whose removal changes nothing observable. This
    /// theory is what earns them, and each arm is arranged so that ITS
    /// level is the one that answers: `||` short-circuits in written
    /// order, so overlay is asked before menu and menu before
    /// keys-outside.
    /// </para>
    /// <para>
    /// **What is production-reachable, and what is a seam.** The OVERLAY
    /// arm is a real route end to end: the keys stay in this surface (a
    /// separate top-level overlay window does not clear this window's
    /// focus-within, and the shell flag is the only thing that knows), so
    /// the press goes through `OnPreviewKeyDown` exactly as a reader's
    /// Escape does. The MENU and KEYS-OUTSIDE arms cannot: with the keys
    /// outside this surface no production route reaches
    /// `FocusProjection` at all — its callers are Escape rungs 2 and 3
    /// and the CD-47 dismissal, and all three need the press, while no
    /// palette row touches it (`ClearFilter`, the palette's own clear,
    /// deliberately does not re-seat). Those two arms therefore drive
    /// `CanvasNavigator.HandleKey` directly, which is the SEAM and not
    /// the route, and they are here as the levels' unit exercise rather
    /// than as a reachability claim. Codex round 4, Min3: a fact may not
    /// present a synthetic call as production reachability, so this says
    /// which is which.
    /// </para>
    /// <para>
    /// The menu arm puts its menu in ANOTHER window — which is what a WPF
    /// menu popup actually is, and what makes it invisible to the
    /// keys-outside level.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(RestorationLevel.Overlay)]
    [InlineData(RestorationLevel.Menu)]
    [InlineData(RestorationLevel.KeysOutside)]
    public void EachLevelOfTheRestorationHoldAnswersOnItsOwn(
        RestorationLevel level) => RunSta(() =>
    {
        Func<bool> overlayWas = CanvasSurfaceView.ShellOverlayIsOpen;
        ProbeWindow? menuHost = null;
        try
        {
            var loading = new CanvasDocumentViewModel(
                _session,
                "board.canvas",
                new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
                synchronousForTests: true,
                verbosity: () => _verbosity);
            var tab = new object();
            var surface = new CanvasSurfaceView { Model = loading, DataContext = tab };
            var elsewhere = new Button { Content = "another pane" };
            var root = new StackPanel();
            root.Children.Add(surface);
            root.Children.Add(elsewhere);
            using ProbeWindow host = HostProbe(root);

            // The surface HAS held the keys, which is what attaches the
            // presenter the palette-driven verb below reaches through.
            Assert.True(
                surface.FilterFieldForTests.Focus(),
                "premise: surface.FilterField refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();

            // The reader goes away FIRST, with nothing pending — so the
            // departure retains nothing and no edge is recorded.
            switch (level)
            {
                case RestorationLevel.Overlay:
                    // Keys deliberately stay here: a separate overlay
                    // window is the case only the shell flag can see.
                    CanvasSurfaceView.ShellOverlayIsOpen = static () => true;
                    break;
                case RestorationLevel.Menu:
                    MenuItem item = MenuRow("Canvas");
                    menuHost = HostProbe(MenuThatTakesTheKeys(item));
                    PutTheKeysInTheMenu(item, "the levels' menu arm");
                    break;
                default:
                    Assert.True(
                        elsewhere.Focus(),
                        "premise: elsewhere refused keyboard focus, so this arrangement never established.");
                    break;
            }
            host.UpdateLayout();

            // Now the verb asks for a seat the surface cannot give:
            // rung 2, which clears the needle and re-seats.
            loading.FilterText = "zzz";
            host.UpdateLayout();
            Assert.True(
                level == RestorationLevel.Overlay
                    // The ROUTE: the keys are still here, so this is the
                    // reader's own Escape through `OnPreviewKeyDown`.
                    ? PressKey(surface, Key.Escape, ModifierKeys.None)
                    // The SEAM: with the keys elsewhere no production
                    // route reaches the rung, so these two arms exercise
                    // the levels through the navigator directly and say
                    // so rather than dressing it up as a reader.
                    : loading.Navigator.HandleKey(Key.Escape, ModifierKeys.None, surface),
                "rung 2 declined the press, so nothing deferred a landing and "
                + "this fact would be about nothing.");
            host.UpdateLayout();
            Assert.NotNull(loading.FocusRequest);

            // The load finishes while they are still away.
            loading.Load();
            host.UpdateLayout();
            Assert.False(
                surface.ProjectionHasFocus,
                "a restoration recorded while the reader was already away was "
                + "delivered on top of them — the levels answer for exactly "
                + "the case that raises no departure.");
            Assert.NotNull(loading.FocusRequest);

            // …and it is HELD, not dropped.
            CanvasSurfaceView.ShellOverlayIsOpen = overlayWas;
            if (level == RestorationLevel.Overlay)
            {
                // The overlay window closes and this one comes forward.
                host.SimulateActivate();
            }
            else
            {
                _ = surface.FilterFieldForTests.Focus();
            }
            host.UpdateLayout();
            Assert.True(
                surface.ProjectionHasFocus,
                "the reader came back and the held landing was never "
                + "delivered.");
            Assert.Null(loading.FocusRequest);
            loading.Shutdown();
        }
        finally
        {
            CanvasSurfaceView.ShellOverlayIsOpen = overlayWas;
            menuHost?.Dispose();
        }
    });

    /// <summary>
    /// An EMPTY canvas seats the reader on its ONBOARDING region, never
    /// on an empty projection — from both of <c>FocusProjection</c>'s
    /// callers, in both projections.
    /// </summary>
    /// <remarks>
    /// <para>
    /// `Ready` keeps the projection visible with nothing in it, and both
    /// implementations take focus while holding nothing (`TreeView.Focus`
    /// and the grid's own), so "ask the projection first" answered yes
    /// and left the reader on a silent empty control with the one
    /// sentence that would have told them what to do — "This canvas is
    /// empty", with the palette chord — sitting unread beside it.
    /// </para>
    /// <para>
    /// Both callers, because the fix is in the shared helper and a fix
    /// applied at one call site would look identical from the other's
    /// side of the ladder: rung 2 clears a needle and re-seats, rung 3
    /// dismisses a transient region and falls back to the same seat.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(CanvasSurfaceKind.Outline, true)]
    [InlineData(CanvasSurfaceKind.Outline, false)]
    [InlineData(CanvasSurfaceKind.Table, true)]
    [InlineData(CanvasSurfaceKind.Table, false)]
    public void AnEmptyCanvasSeatsTheReaderOnItsOnboarding(
        CanvasSurfaceKind projection, bool viaTheFilterRung) => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("empty.canvas");
        document.ShowSurface(projection);
        var surface = new CanvasSurfaceView
        {
            Model = document,
            DataContext = new object(),
        };
        using var host = Host(surface);
        Assert.Equal(CanvasLoadState.Ready, document.State);
        Assert.Empty(document.FilteredOutline);
        // The PREMISE, and the whole reason this fact exists: the
        // projection is on screen and would have taken the keys.
        FrameworkElement showing = projection == CanvasSurfaceKind.Table
            ? surface.TableForTests
            : surface.OutlineForTests;
        Assert.True(
            showing.IsVisible,
            "the projection was not showing, so nothing here could have "
            + "seated the reader on it and this fact proves nothing.");
        Assert.True(surface.OnboardingForTests.IsVisible);

        // Which RUNG the press takes is decided by whether there is a
        // needle to clear: with one, rung 2 consumes it and re-seats;
        // without one, rung 2 declines and rung 3's "the reader is in the
        // filter field" arm seats them instead. Same helper, two callers,
        // and the press is identical from the reader's side.
        if (viaTheFilterRung)
        {
            document.FilterText = "zzz";
            host.UpdateLayout();
        }
        Assert.True(
            surface.FilterFieldForTests.Focus(),
            "premise: surface.FilterField refused keyboard focus, so this arrangement never established.");
        host.UpdateLayout();
        Drain(document);
        Assert.True(
            PressKey(surface, Key.Escape, ModifierKeys.None),
            "premise: the press was not consumed, so the rung under test never ran.");
        host.UpdateLayout();
        Assert.Equal(string.Empty, document.FilterText);
        // …and the press really did take the rung this case is about,
        // because only rung 2 SAYS anything. Without this the theory is
        // one case run four times.
        if (viaTheFilterRung)
        {
            Assert.Equal(
                CanvasAnnouncer.RenderLabel(
                    new CanvasA11yEvent.CanvasFilterCleared(0)),
                OneLine(document));
        }
        else
        {
            Assert.Empty(Lines(document));
        }

        Assert.True(
            surface.OnboardingForTests.IsKeyboardFocused,
            "an empty canvas seated the reader somewhere other than the one "
            + "region that tells them what to do.");
        Assert.False(
            surface.ProjectionHasFocus,
            "the reader is on an empty projection — it takes focus while "
            + "holding nothing, which is exactly why it must be asked "
            + "second.");
        document.Shutdown();
    });

    /// <summary>
    /// A retired document has no PENDING REQUESTS — both of them,
    /// answered at the boundary rather than by a list of clear sites.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The filter-focus request was built as A14's twin and one line of
    /// A14's shape was left behind: `Shutdown` cleared the landing and
    /// not the filter request. A surface reads the REQUEST, never the
    /// document's liveness, so a bound pane that later regained the keys
    /// would have delivered a focus move into the filter field of a
    /// document that no longer exists — and the retired document would
    /// have held the closed tab's `DataContext` until then.
    /// </para>
    /// <para>
    /// Fixed at the boundary, which is the mode stack's lesson one
    /// object over: both requests READ as absent once the document is
    /// retired, so no consumer needs to ask whether it is alive and no
    /// list of clear sites has to be kept complete. `Shutdown` also
    /// drops the fields, which is the reference half.
    /// </para>
    /// </remarks>
    [Fact]
    public void ARetiredDocumentHasNoPendingRequests() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var tab = new object();
        var surface = new CanvasSurfaceView { Model = document, DataContext = tab };
        using var host = Host(surface);
        surface.OutlineForTests.FocusTree();
        host.UpdateLayout();

        // Both requests raised the way production raises them, and both
        // while the pane cannot take them — an eligible surface would
        // satisfy each one on the spot, which is the wrong premise for a
        // fact about what SURVIVES retirement.
        surface.Visibility = Visibility.Collapsed;
        host.UpdateLayout();
        document.RequestFocusLanding(tab);
        document.Navigator.FilterCards();
        Assert.NotNull(document.FocusRequest);
        Assert.NotNull(document.FilterFocusRequest);

        document.Shutdown();

        Assert.Null(document.FocusRequest);
        Assert.Null(document.FilterFocusRequest);
        // The RETENTION half, which the boundary hides: the fields are
        // empty, so a retired document is not holding the closed tab's
        // object graph through a request nobody will ever deliver.
        Assert.False(document.HoldsPendingRequestsForTests);

        // The BOUNDARY half, which the clear cannot cover because it has
        // already run: the workspace raises a landing on tab activation
        // with no admission gate, so a request CAN still arrive after
        // retirement — and a bound surface reads the request, never the
        // document's liveness.
        document.RequestFocusLanding(tab);
        document.Navigator.FilterCards();
        Assert.Null(document.FocusRequest);
        Assert.Null(document.FilterFocusRequest);
        // …and the FIELDS are still empty. The read boundary alone would
        // hide a late write, so a request arriving after retirement
        // would have repopulated the closed tab's reference — the exact
        // graph retirement exists to drop — while every public read went
        // on saying null. The write side has to refuse, not be hidden.
        Assert.False(document.HoldsPendingRequestsForTests);
        // …and the verbs those late calls ran COMPOSED nothing. The
        // never-silent mapping announces its refusal, so a verb invoked
        // on a retired canvas used to post through a closed funnel — the
        // Debug gate found it, and this assertion is the half that also
        // works in Release, where `Debug.Fail` says nothing.
        Assert.Equal(0, document.AnnouncerForTests.RefusedAfterShutdownForTests);

        // …and the boundary holds for a surface that comes back: the
        // reader is not dragged anywhere by a request on a dead document.
        surface.Visibility = Visibility.Visible;
        host.UpdateLayout();
        surface.OutlineForTests.FocusTree();
        host.UpdateLayout();
        Assert.False(surface.FilterFieldForTests.IsKeyboardFocused);
    });

    /// <summary>
    /// The CD-47 pre-ladder dismissal seats the reader even when the
    /// needle has left the projection with no rows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Escape belongs to an OPEN Where-am-I panel ahead of every rung, so
    /// unlike rungs 2 and 3 this path runs with a LIVE needle by design —
    /// which is what makes `Ready` + no rows reachable at the seat. The
    /// canvas has cards, so the onboarding region is hidden
    /// (`EmptyOnboardingText` keys on the UNFILTERED outline) and `Ready`
    /// has no focusable banner: without an arm for it the seat falls
    /// through to a deferred landing with the panel already collapsed and
    /// the reader on the window root — the exact trap the dismissal
    /// exists to prevent.
    /// </para>
    /// <para>
    /// Two panes on one document is the arrangement that reaches it: the
    /// reader opens the panel from a row in one pane while the other
    /// narrows the shared filter out from under them, which is what makes
    /// the remembered element stale. This is the caller a previous
    /// round's "every caller is an Escape rung" enumeration missed.
    /// </para>
    /// </remarks>
    [Fact]
    public void DismissingThePanelSeatsTheReaderEvenWithNoRowsToSitOn() => RunSta(() =>
    {
        CanvasDocumentViewModel document = Open("board.canvas");
        var paneA = new CanvasSurfaceView { Model = document, DataContext = new object() };
        var paneB = new CanvasSurfaceView { Model = document, DataContext = new object() };
        var root = new StackPanel();
        root.Children.Add(paneA);
        root.Children.Add(paneB);
        using var host = Host(root);

        string row = document.FilteredOutline[0].NodeId;
        Assert.True(
            paneA.FocusRow(row),
            "premise: the row never took focus, so the reader is not on the projection.");
        host.UpdateLayout();
        document.Navigator.WhereAmI();
        host.UpdateLayout();
        Assert.True(
            paneA.WhereAmIPanelForTests.IsKeyboardFocusWithin,
            "the panel never took focus in this pane, so the dismissal would "
            + "not restore and this fact would be about nothing.");

        // The OTHER pane narrows the shared filter to nothing, so the row
        // this pane's reader came from stops existing under them.
        paneB.FilterFieldForTests.Text = "nothingmatchesthis";
        host.UpdateLayout();
        Assert.Empty(document.FilteredOutline);
        Assert.False(
            paneA.OnboardingForTests.IsVisible,
            "the canvas has cards, so the onboarding arm must not be the one "
            + "that answers here.");

        // Whether the seat was taken HERE or deferred and then rescued by
        // the delivery path is the difference this arm exists for, and
        // the two are indistinguishable after the fact — both end with
        // the reader in the field and no pending request. So the landing
        // is watched instead: a dismissal that has to raise one is a
        // dismissal whose outcome depends on the delivery path's hold
        // conditions still being false a moment later.
        var landings = new List<string>();
        document.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(CanvasDocumentViewModel.FocusRequest))
            {
                landings.Add(changed.PropertyName);
            }
        };

        Assert.True(
            PressKey(paneA, Key.Escape, ModifierKeys.None),
            "premise: the press was not consumed, so the rung under test never ran.");
        host.UpdateLayout();

        Assert.Null(document.WhereAmIText);
        // CD-47: the dismissal leaves an active filter untouched.
        Assert.Equal("nothingmatchesthis", document.FilterText);
        Assert.True(
            paneA.IsKeyboardFocusWithin,
            "the panel collapsed and left the reader on the window root — a "
            + "keyboard user with nowhere to go, which is the trap this "
            + "dismissal exists to prevent (m6/C6).");
        Assert.True(
            paneA.FilterFieldForTests.IsKeyboardFocused,
            "nothing matched, so the field holding the needle is the only "
            + "control on this surface that can change the answer.");
        Assert.Empty(landings);
        document.Shutdown();
    });

    /// <summary>
    /// The half of CD-45 that DOES hold: a surviving group carries every
    /// descendant GROUP, so no gap can open between a survivor and a
    /// surviving ancestor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Core matches a row on its own title, its kind word, its
    /// activation target, or its ANCESTOR-ONLY group path
    /// (`queries.rs`). Ancestor→descendant-GROUP is the direction that
    /// survives every route: a matching title lands in every
    /// descendant's group path, the kind word is shared by every
    /// descendant group, an ancestor's own group path is a prefix of
    /// theirs, and a group's target is empty. So an intermediate group
    /// can never be missing between a survivor and a surviving ancestor,
    /// and `Rebuild`'s walk finds the TRUE parent whenever one survives.
    /// </para>
    /// <para>
    /// The GROUP qualification is load-bearing and was dropped once in
    /// this record before being put back. It does not extend to every
    /// descendant ROW: the needle `group` matches a group by its KIND
    /// word while a text card inside it matches nothing. This fixture
    /// cannot catch that, because `zeta` appears only in the
    /// grandparent's TITLE — the one route on which the broad claim
    /// happens to hold.
    /// </para>
    /// <para>
    /// Nor does it run descendant→ancestor, which an earlier version of
    /// the record asserted: "every route matching a group G also matches
    /// its parent P" is false, because ancestor-only means a child never
    /// carries a parent.
    /// <see cref="AGroupThatMatchesInsideANonMatchingGroupIsPromotedToTheRoot"/>
    /// is that counterexample.
    /// </para>
    /// </remarks>
    [Fact]
    public void AMatchingGroupCarriesItsDescendantsSoNoAncestorGapExists() =>
        RunSta(() =>
        {
            CanvasDocumentViewModel document = Open("nested-filter.canvas");
            var surface = new CanvasSurfaceView { Model = document };
            using var host = Host(surface);

            // The needle appears ONLY in the grandparent's label.
            document.FilterText = "zeta";
            host.UpdateLayout();
            Assert.DoesNotContain(
                "outside",
                document.FilteredOutline.Select(row => row.NodeId));

            IReadOnlyList<CanvasOutlineRowViewModel> roots =
                surface.OutlineForTests.RootsForTests;
            Assert.Equal(
                ["gran"],
                roots.Select(row => row.Id).ToArray());
            // …and the whole chain came with it, so there is no gap for
            // a nearest-ancestor promotion to fill.
            CanvasOutlineRowViewModel gran = roots.Single();
            CanvasOutlineRowViewModel mid = Assert.Single(
                gran.Children,
                child => !child.IsConnection);
            Assert.Equal("mid", mid.Id);
            Assert.Equal(
                ["leaf"],
                mid.Children.Where(child => !child.IsConnection)
                    .Select(child => child.Id)
                    .ToArray());
        });

    /// <summary>
    /// The case the record above once called unreachable: a group that
    /// matches by its OWN title inside a group that does not, promoted to
    /// the root rather than nested under a parent that is gone.
    /// </summary>
    /// <remarks>
    /// The proof step that ruled this out — "every route that matches a
    /// group also matches its parent" — is false, because the group path
    /// core matches against is ANCESTOR-ONLY. A child carries no parent.
    /// `Pocket zeta` inside `Container` is the whole counterexample, and
    /// it took one fixture: the earlier conclusion came from a single
    /// needle route tried against a single fixture, and "verified
    /// empirically" was doing work that only enumerating core's four
    /// routes against the shape could do.
    /// </remarks>
    [Fact]
    public void AGroupThatMatchesInsideANonMatchingGroupIsPromotedToTheRoot() =>
        RunSta(() =>
        {
            CanvasDocumentViewModel document = Open("promoted.canvas");
            var surface = new CanvasSurfaceView { Model = document };
            using var host = Host(surface);

            document.FilterText = "zeta";
            host.UpdateLayout();

            // The premise: the INNER group survived on its own title and
            // the outer one did not survive at all.
            string[] survivors =
                [.. document.FilteredOutline.Select(row => row.NodeId)];
            Assert.Contains("pocket", survivors);
            Assert.DoesNotContain("container", survivors);
            Assert.DoesNotContain("apart", survivors);

            IReadOnlyList<CanvasOutlineRowViewModel> roots =
                surface.OutlineForTests.RootsForTests;
            Assert.Equal(
                ["pocket"],
                roots.Select(row => row.Id).ToArray());
            // …and its own descendants came with it, which is the lemma
            // that does hold.
            Assert.Equal(
                ["inPocket"],
                roots.Single().Children
                    .Where(child => !child.IsConnection)
                    .Select(child => child.Id)
                    .ToArray());
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

        // THE PREMISE, asserted rather than assumed: the window this
        // fact is about was actually sampled. Every assertion below
        // SKIPS the state-sentence samples, which is the fixed
        // behaviour — so without this line the fact goes silently green
        // the day `Load` stops raising a notification while the
        // projections are hidden, and it would have been green for a
        // reason that has nothing to do with what it checks.
        (string Summary, int Shown, bool Visible)[] hidden =
            [.. samples.Where(sample => !sample.Visible)];
        Assert.NotEmpty(hidden);
        Assert.All(hidden, sample => Assert.Equal(
            Rendered(new CanvasStatusNote.Loading()),
            sample.Summary));

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
        Assert.True(document.Modes.Enter(spec, _modePane));
        Drain(document);

        _ = document.Modes.Commit();

        // The held departure was honoured on the way out: the mode is
        // gone, restored, and the restoration was SPOKEN.
        Assert.False(document.Modes.IsActive);
        Assert.Null(document.Modes.ContainerValue);
        string line = OneLine(document);
        Assert.Contains("cancelled", line, StringComparison.Ordinal);

        // NOTHING WAS COMPOSED after the funnel closed, which is a
        // stronger claim than "nothing was heard" and the one the A5
        // guard actually makes. The commit's confirmation used to be
        // announced into the retired announcer and silently dropped —
        // invisible in Release, a `Debug.Fail` in Debug, and the reason
        // this fact was red in the configuration nobody was running.
        Assert.Equal(0, document.AnnouncerForTests.RefusedAfterShutdownForTests);

        // …and nothing the retired document composes afterwards reaches
        // anybody — including the commit confirmation that resolved after
        // the funnel closed.
        Assert.DoesNotContain(
            Lines(document),
            spoken => spoken.Contains("Moved", StringComparison.Ordinal));
        // DELIBERATE misuse, scoped: production marks a post-retirement
        // announce with `Debug.Fail`, and this fact exists to prove what
        // happens when someone does it anyway. The suppression wraps this
        // ONE call, so a `Debug.Fail` from `Shutdown` itself still fails
        // the run.
        using (DebugAsserts.Suppressed())
        {
            document.AnnouncerForTests.Announce(
                new CanvasA11yEvent.CanvasStatus(new CanvasStatusNote.NoMarks()));
        }
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
        Assert.True(document.Modes.Enter(spec, _modePane));
        // A coalesced line is queued on the way out — the 0a-2 premise.
        document.SelectNode("evidence");
        _announced.Clear();

        // Retirement completes. It does not propagate the restoration's
        // failure to the registry sweeping every open document.
        document.Shutdown();

        Assert.False(document.Modes.IsActive);
        // The queued line was DROPPED and the funnel refuses anything
        // later, which is only true if `Announcer.Shutdown` was reached.
        using (DebugAsserts.Suppressed())
        {
            document.AnnouncerForTests.Announce(
                new CanvasA11yEvent.CanvasStatus(new CanvasStatusNote.NoMarks()));
        }
        Assert.Empty(Lines(document));
        // The counter has to COUNT, or the zero every other fact asserts
        // is the zero of a line that never runs.
        Assert.Equal(1, document.AnnouncerForTests.RefusedAfterShutdownForTests);
    }

    /// <summary>
    /// Whitespace is not a filter, and a bare NEWLINE is — the carve-out
    /// this predicate exists for, and the exact width of its agreement
    /// with mac.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The claim is BOUNDED, and this fact used to be called
    /// `TheFilterActivePredicateIsMacs`, which was wider than the truth
    /// and wider than the record. Foundation's `.whitespaces` is Zs plus
    /// tab, so the two hosts agree on the ordinary cases and on the
    /// newline carve-out (.NET's `IsNullOrWhiteSpace` would have trimmed
    /// it; mac reads it as active and core trims it, so it matches
    /// everything) — and they DIVERGE on five code points that
    /// Foundation excludes and `char.IsWhiteSpace` includes. Those are
    /// ratified in the contracts document, so they are pinned here as
    /// arms rather than left as a paragraph: a ratified boundary that
    /// nothing executes is a boundary that moves.
    /// </para>
    /// <para>
    /// Each of the five reads INACTIVE on this host and ACTIVE on mac.
    /// That is the recorded divergence, not a defect, and the arms exist
    /// so a future "let us just use `IsNullOrWhiteSpace`" — or a future
    /// widening toward mac — has to come back through the record.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("\t", false)]
    [InlineData("\n", true)]
    [InlineData(" a ", true)]
    // The five ratified divergences (CD-22 / §C's micro-divergence m2):
    // Foundation's `.whitespaces` excludes each, so mac reads them
    // ACTIVE; `char.IsWhiteSpace` includes them, so this host does not.
    [InlineData("\u000B", false)]
    [InlineData("\u000C", false)]
    [InlineData("\u0085", false)]
    [InlineData("\u2028", false)]
    [InlineData("\u2029", false)]
    public void WhitespaceIsNotAFilterButANewlineIs(string needle, bool active) =>
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
            () => new CanvasModeRestoration.Unstated()), _modePane));
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
        Assert.True(
            PressKey(surface, Key.Escape, ModifierKeys.None),
            "premise: the press was not consumed, so the rung under test never ran.");
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
        Assert.True(
            PressKey(surface, Key.Escape, ModifierKeys.None),
            "premise: the press was not consumed, so the rung under test never ran.");
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
        Assert.True(
            PressKey(surface, Key.Left, ModifierKeys.None),
            "premise: the press was not consumed, so the rung under test never ran.");
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
        Assert.True(document.Navigator.EnterMode(new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("Core question"),
            () =>
            {
                committed = true;
                return CanvasModeCommitResult.Committed();
            },
            () => new CanvasModeRestoration.BackAt("Core question")), surface));
        host.UpdateLayout();
        Assert.True(
            surface.CancelModeForTests.Focus(),
            "premise: surface.CancelMode refused keyboard focus, so this arrangement never established.");
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
        Assert.True(document.Navigator.EnterMode(new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("Core question"),
            () =>
            {
                committed = true;
                return CanvasModeCommitResult.Committed();
            },
            () => new CanvasModeRestoration.BackAt("Core question")), surface));
        Assert.True(
            surface.FilterFieldForTests.Focus(),
            "premise: surface.FilterField refused keyboard focus, so this arrangement never established.");
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

        Assert.True(document.Navigator.EnterMode(new CanvasModeSpec(
            CanvasMode.Move,
            new CanvasModeObject.Card("Core question"),
            () => CanvasModeCommitResult.Committed(),
            () => new CanvasModeRestoration.BackAt("Core question")), surface));
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
            Assert.True(document.Navigator.EnterMode(
                new CanvasModeSpec(
                    CanvasMode.Move,
                    new CanvasModeObject.Card("Core question"),
                    () => CanvasModeCommitResult.Committed(),
                    () => new CanvasModeRestoration.BackAt("Core question")),
                surface));
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
            document.AnnouncerForTests.FlushForTests();
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

    /// <summary>
    /// A menu whose items take the keys on ANY desktop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// `Menu` is a WPF FOCUS SCOPE, and handing KEYBOARD focus into a
    /// scope is a step a non-interactive desktop does not always
    /// perform. `MenuItem.Focus()` sets LOGICAL focus inside the menu's
    /// scope and returns whether keyboard focus followed — true on every
    /// developer machine here, false on the CI runner, which failed six
    /// menu arms on that one call with no message to say so. In-window
    /// and second-window menus failed alike, so it was never about
    /// window activation.
    /// </para>
    /// <para>
    /// The scope is switched off, and NOTHING the code under test reads
    /// changes by it: `ClassifyFocusLoss` walks the focused element's
    /// ancestors looking for a `MenuBase`, which is a TYPE test that a
    /// focus scope neither helps nor hinders. The arrangement keeps the
    /// production predicate exactly and drops a WPF focus-management step
    /// this branch owns no behaviour in. A menu that is a focus scope and
    /// a menu that is not are the same menu to the classifier, and the
    /// classifier is the thing under test.
    /// </para>
    /// </remarks>
    private static Menu MenuThatTakesTheKeys(params MenuItem[] items)
    {
        var menu = new Menu();
        System.Windows.Input.FocusManager.SetIsFocusScope(menu, false);
        foreach (MenuItem item in items)
        {
            menu.Items.Add(item);
        }
        return menu;
    }

    private static MenuItem MenuRow(string header) =>
        new() { Header = header, Focusable = true };

    /// <summary>
    /// Put the keys in a menu, and say which leg failed if they do not
    /// go.
    /// </summary>
    /// <remarks>
    /// TWO legs, because they fail for different reasons and a CI log
    /// that says only `Assert.True() Failure` costs a round trip: the
    /// item has to accept keyboard focus, and the classifier has to be
    /// able to SEE it — `ClassifyFocusLoss` reads
    /// `Keyboard.FocusedElement`, so an item that reports focus while the
    /// thread's focused element is something else is a premise that has
    /// not established.
    /// </remarks>
    private static void PutTheKeysInTheMenu(MenuItem item, string arrangement)
    {
        Assert.True(
            item.Focus(),
            $"{arrangement}: the menu item refused keyboard focus, so the "
            + "reader is not 'in a menu' and the classification under test "
            + "never runs. On a desktop that will not hand focus into a "
            + "menu, this is the premise that dies first.");
        Assert.True(
            ReferenceEquals(item, System.Windows.Input.Keyboard.FocusedElement),
            $"{arrangement}: the item reports focus but the thread's focused "
            + "element is "
            + (System.Windows.Input.Keyboard.FocusedElement?.GetType().Name ?? "null")
            + " — `ClassifyFocusLoss` reads the thread, so this arm would be "
            + "testing whatever that is instead.");
    }

    private static ProbeWindow HostProbe(UIElement content)
    {
        var window = new ProbeWindow
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
        return window;
    }

    /// <summary>
    /// A host window whose activation edges a test can cause.
    /// </summary>
    /// <remarks>
    /// `Window.Activated` and `Window.Deactivated` are raised by the OS,
    /// and a test cannot alt-tab. `OnActivated`/`OnDeactivated` are the
    /// framework's own documented extension point for exactly this, so
    /// the fact drives the REAL event through the REAL handler rather
    /// than reaching past the view into its state — no production seam
    /// exists for this, and none should.
    /// </remarks>
    private sealed class ProbeWindow : Window, IDisposable
    {
        internal void SimulateDeactivate() => OnDeactivated(EventArgs.Empty);

        internal void SimulateActivate() => OnActivated(EventArgs.Empty);

        public void Dispose() => Close();
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
