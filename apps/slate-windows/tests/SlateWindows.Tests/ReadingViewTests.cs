// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows.Documents;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W3-1 reading surface behavior: builder output, landmark index, and
/// chorded navigation, over real core output — the FFI calls run for
/// real, so these tests exercise the exact model the view consumes.
/// WPF text objects require STA; each test body runs on its own STA
/// thread (the ShellAccessibilityTests convention).
/// </summary>
public sealed class ReadingViewTests
{
    private const string Fixture =
        "# Top heading\n"
        + "\n"
        + "A paragraph with a [[known]] link, an [[absent]] link, a #tag, "
        + "and a citation [@smith2020].\n"
        + "\n"
        + "## Second heading\n"
        + "\n"
        + "- first bullet\n"
        + "- second bullet\n"
        + "  - nested bullet\n"
        + "\n"
        + "1. ordered one\n"
        + "2. ordered two\n"
        + "\n"
        + "- [x] a done task\n"
        + "\n"
        + "```rust\n"
        + "fn interior() -> usize { 7 }\n"
        + "```\n"
        + "\n"
        + "| h1 | h2 |\n"
        + "| --- | --- |\n"
        + "| c1 | c2 |\n"
        + "\n"
        + "![[known]]\n";

    private static readonly RenderedCitation[] Citations =
    {
        new(
            Raw: "[@smith2020]",
            VisualText: "(Smith, 2020)",
            SpeechText: "Smith, two thousand twenty.",
            BibEntry: null,
            StyleId: "test"),
    };

    private static readonly OutgoingLink[] Records =
    {
        new(
            TargetPath: "known.md", TargetRaw: "known", TargetAnchor: null,
            Kind: "wikilink", IsEmbed: false, IsExternal: false,
            IsUnresolved: false, Snippet: "", Ordinal: 0, SpanStart: 0,
            SpanEnd: 0, DisplayText: null),
        new(
            TargetPath: null, TargetRaw: "absent", TargetAnchor: null,
            Kind: "wikilink", IsEmbed: false, IsExternal: false,
            IsUnresolved: true, Snippet: "", Ordinal: 1, SpanStart: 0,
            SpanEnd: 0, DisplayText: null),
    };

    private static ReadingDocumentModel BuildFixture()
    {
        ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(Fixture);
        ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
            Fixture, Citations, Records);
        var model = new List<(ReadingBlock, ReadingBlockInlines)>();
        for (int i = 0; i < blocks.Length && i < inlines.Length; i++)
        {
            model.Add((blocks[i], inlines[i]));
        }
        return ReadingDocumentBuilder.Build(model);
    }

    [Fact]
    public void LandmarksArriveInDocumentOrderWithKindsAndLevels()
    {
        RunSta(() =>
        {
            ReadingDocumentModel model = BuildFixture();

            ReadingLandmarkKind[] kinds = model.Landmarks.Select(l => l.Kind).ToArray();
            Assert.Equal(
                new[]
                {
                    ReadingLandmarkKind.Heading,   // # Top heading
                    ReadingLandmarkKind.Link,      // [[known]]
                    ReadingLandmarkKind.Link,      // [[absent]]
                    ReadingLandmarkKind.Link,      // #tag
                    ReadingLandmarkKind.Link,      // [@smith2020]
                    ReadingLandmarkKind.Heading,   // ## Second heading
                    ReadingLandmarkKind.List,      // bullet list
                    ReadingLandmarkKind.List,      // nested list
                    ReadingLandmarkKind.List,      // ordered list (split on ordered-ness)
                    ReadingLandmarkKind.List,      // task list (split back)
                    ReadingLandmarkKind.CodeBlock, // rust fence
                    ReadingLandmarkKind.Table,
                    ReadingLandmarkKind.Embed,     // ![[known]] card
                },
                kinds);

            Assert.Equal(1, model.Landmarks[0].HeadingLevel);
            Assert.Equal(2, model.Landmarks[5].HeadingLevel);

            // Document order is the navigator's core assumption; prove it
            // rather than trust the walk.
            for (int i = 1; i < model.Landmarks.Count; i++)
            {
                Assert.True(
                    model.Landmarks[i - 1].Position.CompareTo(model.Landmarks[i].Position) <= 0,
                    $"landmark {i} is out of document order");
            }
        });
    }

    [Fact]
    public void CodeFenceInteriorIsInsideTheTextRange()
    {
        RunSta(() =>
        {
            ReadingDocumentModel model = BuildFixture();
            string text = new TextRange(
                model.Document.ContentStart, model.Document.ContentEnd).Text;
            // The spike measured the BlockUIContainer alternative as
            // silently absent from say-all ("landmarks missing: fn
            // main"). The W3-1 baseline keeps code readable.
            Assert.Contains("fn interior()", text);
        });
    }

    [Fact]
    public void EveryActivatableRunCarriesARoutingDestination()
    {
        RunSta(() =>
        {
            ReadingDocumentModel model = BuildFixture();
            Hyperlink[] links = CollectHyperlinks(model.Document).ToArray();

            // [[known]], [[absent]], #tag, [@smith2020] — the embed is a
            // card, not an inline link.
            Assert.Equal(4, links.Length);
            Assert.All(links, link => Assert.NotNull(link.NavigateUri));

            // The grammar rides the scheme; a destination-less link is
            // the measured "Link has no apparent destination".
            Assert.Equal(
                new[] { "slate-wiki", "slate-wiki", "slate-tag", "slate-cite" },
                links.Select(l => l.NavigateUri!.Scheme).ToArray());
        });
    }

    [Fact]
    public void CitationSpeechArrivesAsHelpTextNeverComposed()
    {
        RunSta(() =>
        {
            ReadingDocumentModel model = BuildFixture();
            Hyperlink citation = CollectHyperlinks(model.Document)
                .Single(l => l.NavigateUri!.Scheme == "slate-cite");
            Assert.Equal(
                "Smith, two thousand twenty.",
                System.Windows.Automation.AutomationProperties.GetHelpText(citation));
        });
    }

    [Fact]
    public void ChordedNavigationWalksHeadingsLinksAndMissesLoudly()
    {
        RunSta(() =>
        {
            ReadingDocumentModel model = BuildFixture();
            var surface = new ReadingSurface { Document = model.Document };
            var announced = new List<A11yEvent>();
            var navigator = new ReadingNavigator(surface, announced.Add);
            navigator.SetLandmarks(model.Landmarks);

            surface.CaretPosition = model.Document.ContentStart;
            string Last() => SlateUniffiMethods.A11yRender(announced[^1]).Text;

            // The caret starts ON the first heading's line, and quick-nav
            // convention (NVDA/JAWS alike) is that "next heading" from a
            // heading goes to the FOLLOWING one — the strict
            // caret-before-landmark comparison encodes exactly that.
            // Every LANDING is announced with the target's own text and
            // kind (2026-07-27: NVDA does not echo programmatic caret
            // moves, so a silent landing is an unusable one).
            navigator.Move(ReadingLandmarkKind.Heading, forward: true);
            Assert.Equal(0, surface.CaretPosition.CompareTo(model.Landmarks[5].Position));
            Assert.Equal("Second heading, level 2 heading.", Last());
            navigator.Move(ReadingLandmarkKind.Heading, forward: true);
            Assert.Equal("No next heading.", Last());

            // Level-targeted: 1 goes back-to-start miss-free from here?
            // No — level 1 is BEHIND the caret; forward must miss.
            navigator.MoveToHeadingLevel(1, forward: true);
            Assert.Equal("No next level 1 heading.", Last());

            // And backward reaches it, announcing the landing.
            navigator.MoveToHeadingLevel(1, forward: false);
            Assert.Equal("Top heading, level 1 heading.", Last());
            Assert.Equal(0, surface.CaretPosition.CompareTo(model.Landmarks[0].Position));

            // Links: forward from the top hits the first link, spoken as
            // the LINK's text, not the whole line.
            navigator.Move(ReadingLandmarkKind.Link, forward: true);
            Assert.Equal(0, surface.CaretPosition.CompareTo(model.Landmarks[1].Position));
            Assert.Equal("known, link.", Last());

            // A kind with one instance: table forward (first cell text),
            // then miss.
            navigator.Move(ReadingLandmarkKind.Table, forward: true);
            Assert.Equal("h1, table.", Last());
            navigator.Move(ReadingLandmarkKind.Table, forward: true);
            Assert.Equal("No next table.", Last());
        });
    }

    [Fact]
    public void NavigationNeverWraps()
    {
        RunSta(() =>
        {
            ReadingDocumentModel model = BuildFixture();
            var surface = new ReadingSurface { Document = model.Document };
            var announced = new List<A11yEvent>();
            var navigator = new ReadingNavigator(surface, announced.Add);
            navigator.SetLandmarks(model.Landmarks);

            // From the very start, backward is a miss for every kind —
            // wrap-around would disorient exactly the users this layer
            // exists for.
            surface.CaretPosition = model.Document.ContentStart;
            navigator.Move(ReadingLandmarkKind.Heading, forward: false);
            navigator.Move(ReadingLandmarkKind.Link, forward: false);
            navigator.Move(ReadingLandmarkKind.List, forward: false);
            Assert.Equal(3, announced.Count);
            Assert.Equal(
                new[] { "No previous heading.", "No previous link.", "No previous list." },
                announced.Select(e => SlateUniffiMethods.A11yRender(e).Text).ToArray());
        });
    }

    /// <summary>
    /// `slate.editor.toggleViewMode` delivery evidence (chords.json
    /// group `reading`): the persisted `"reading"` token flips, the
    /// projection builds synchronously in test mode, the reading VM is
    /// retained across toggles (§10.1 memoization), and non-markdown
    /// tabs refuse the command.
    /// </summary>
    [Fact]
    public void ReadingModeToggle_FlipsPersistedModeAndProjectsTheDocument()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-mode-toggle");
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), Fixture);
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);

            Assert.False(tab.IsReadingMode);
            Assert.True(tab.IsEditorVisible);
            Assert.Null(tab.Reading);

            tab.ToggleViewMode();
            Assert.Equal("reading", tab.Mode);
            Assert.True(tab.IsReadingVisible);
            Assert.False(tab.IsEditorVisible);
            SlateWindows.Reading.ReadingContentViewModel reading =
                Assert.IsType<SlateWindows.Reading.ReadingContentViewModel>(tab.Reading);
            Assert.NotNull(reading.Document);

            // Toggling back retains the projection — flipping again is a
            // memo hit, not a re-parse.
            tab.ToggleViewMode();
            Assert.Null(tab.Mode);
            Assert.True(tab.IsEditorVisible);
            Assert.Same(reading, tab.Reading);

            // Non-markdown tabs refuse the command.
            using var placeholder = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Canvas, "canvas:test")),
                startInteractionBackgroundWork: false);
            placeholder.ToggleViewMode();
            Assert.Null(placeholder.Mode);
            Assert.Null(placeholder.Reading);
        });
    }

    /// <summary>
    /// The 2026-07-27 manual pass regression: with the UIA peer created
    /// BEFORE the document arrives (exactly the live app's order — the
    /// template instantiates the surface, NVDA binds, then the async
    /// projection publishes), the text pattern must still expose the
    /// published content. Swapping the Document property on a live
    /// RichTextBox leaves the peer bound to the ORIGINAL text container:
    /// NVDA announced "Reading view document blank" and every caret move
    /// after was silent, while the visual caret moved normally.
    /// </summary>
    [Fact]
    public void TextPatternSurvivesDocumentArrivingAfterThePeer()
    {
        RunSta(() =>
        {
            ReadingDocumentModel model = BuildFixture();
            var surface = new ReadingSurface();

            // Peer first — the live binding order.
            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer
                .CreatePeerForElement(surface);
            Assert.NotNull(peer);
            _ = peer.GetChildren();

            System.Windows.Documents.FlowDocument persistent = surface.Document;
            surface.ApplyBuiltDocument(model.Document);

            // The persistent container must be retained — replacing it is
            // exactly the binding break.
            Assert.Same(persistent, surface.Document);

            var provider = peer.GetPattern(
                System.Windows.Automation.Peers.PatternInterface.Text)
                as System.Windows.Automation.Provider.ITextProvider;
            Assert.NotNull(provider);
            string text = provider!.DocumentRange.GetText(-1);
            Assert.Contains("Top heading", text);
            Assert.Contains("fn interior()", text);

            // Landmarks re-collected over the LIVE container, so the
            // navigator's pointers are never left aimed at the emptied
            // built document.
            IReadOnlyList<ReadingLandmark> live = surface.LandmarksForTests;
            Assert.NotEmpty(live);
            Assert.All(live, l => Assert.True(
                l.Position.IsInSameDocument(surface.Document.ContentStart),
                "landmark points into the emptied built document, not the live one"));
        });
    }

    /// <summary>
    /// The full chord table, pinned: 12 kind chords (6 kinds × 2
    /// directions) + 12 level chords (6 levels × 2). Dispatch moved to
    /// PreviewKeyDown after Ctrl+Alt+L was measured being consumed
    /// before KeyDown bindings ran (2026-07-27) — this test keeps the
    /// table itself from silently losing a row in that refactor or any
    /// future one.
    /// </summary>
    [Fact]
    public void EveryNavigationChordIsRegistered()
    {
        RunSta(() =>
        {
            var surface = new ReadingSurface();
            var navigator = new ReadingNavigator(surface, _ => { });

            const System.Windows.Input.ModifierKeys forward =
                System.Windows.Input.ModifierKeys.Control
                | System.Windows.Input.ModifierKeys.Alt;
            const System.Windows.Input.ModifierKeys backward =
                forward | System.Windows.Input.ModifierKeys.Shift;

            var kindKeys = new[]
            {
                System.Windows.Input.Key.H, System.Windows.Input.Key.K,
                System.Windows.Input.Key.L, System.Windows.Input.Key.T,
                System.Windows.Input.Key.E, System.Windows.Input.Key.C,
                // U aliases L: the field log measured Ctrl+Alt+L grabbed
                // globally on the reference machine before the app saw it.
                System.Windows.Input.Key.U,
            };
            foreach (System.Windows.Input.Key key in kindKeys)
            {
                Assert.True(navigator.HandlesChord(key, forward), $"{key} forward");
                Assert.True(navigator.HandlesChord(key, backward), $"{key} backward");
            }
            for (int level = 0; level < 6; level++)
            {
                System.Windows.Input.Key key = System.Windows.Input.Key.D1 + level;
                Assert.True(navigator.HandlesChord(key, forward), $"level {level + 1} forward");
                Assert.True(navigator.HandlesChord(key, backward), $"level {level + 1} backward");
            }

            // And nothing UNMODIFIED is ever claimed — the letters belong
            // to the AT layer (G21).
            Assert.False(navigator.HandlesChord(
                System.Windows.Input.Key.H, System.Windows.Input.ModifierKeys.None));
            Assert.False(navigator.HandlesChord(
                System.Windows.Input.Key.K, System.Windows.Input.ModifierKeys.None));
        });
    }

    /// <summary>
    /// The StyleId decorator, exercised through NVDA's exact flow:
    /// caret on a line → GetSelection → GetAttributeValue(StyleId).
    /// This is the pin the decorator's design depends on — it reads a
    /// WPF-internal field by reflection, and the deal is that a WPF
    /// change breaks THIS TEST loudly instead of silently muting heading
    /// levels for screen readers.
    /// </summary>
    [Fact]
    public void HeadingLevelsAnswerThroughTheTextPatternStyleId()
    {
        RunSta(() =>
        {
            ReadingDocumentModel model = BuildFixture();
            var surface = new ReadingSurface();
            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer
                .CreatePeerForElement(surface);
            surface.ApplyBuiltDocument(model.Document);

            var provider = peer!.GetPattern(
                System.Windows.Automation.Peers.PatternInterface.Text)
                as System.Windows.Automation.Provider.ITextProvider;
            var decorated = Assert.IsType<HeadingStyleTextProvider>(provider);

            IReadOnlyList<ReadingLandmark> landmarks = surface.LandmarksForTests;
            ReadingLandmark h1 = landmarks.First(
                l => l.Kind == ReadingLandmarkKind.Heading && l.HeadingLevel == 1);
            ReadingLandmark h2 = landmarks.First(
                l => l.Kind == ReadingLandmarkKind.Heading && l.HeadingLevel == 2);

            // NVDA's flow: the caret's collapsed selection range answers
            // for the line the reader is on.
            surface.CaretPosition = h1.Position;
            object? style = decorated.GetSelection()[0].GetAttributeValue(
                HeadingStyleTextProvider.StyleIdAttribute);
            Assert.Equal(HeadingStyleTextProvider.StyleIdHeading1, style);

            surface.CaretPosition = h2.Position;
            style = decorated.GetSelection()[0].GetAttributeValue(
                HeadingStyleTextProvider.StyleIdAttribute);
            Assert.Equal(HeadingStyleTextProvider.StyleIdHeading1 + 1, style);

            // A body line is NOT a heading — the decorator must defer to
            // the base provider rather than invent a style.
            ReadingLandmark link = landmarks.First(l => l.Kind == ReadingLandmarkKind.Link);
            surface.CaretPosition = link.Position;
            style = decorated.GetSelection()[0].GetAttributeValue(
                HeadingStyleTextProvider.StyleIdAttribute);
            Assert.NotEqual(HeadingStyleTextProvider.StyleIdHeading1, style);
            Assert.NotEqual(HeadingStyleTextProvider.StyleIdHeading1 + 1, style);

            // Wrapped-vs-wrapped endpoint comparison must not throw: the
            // base casts its argument to the internal adaptor type, so
            // the decorator unwraps before forwarding.
            var whole = decorated.DocumentRange;
            var selection = decorated.GetSelection()[0];
            _ = selection.CompareEndpoints(
                System.Windows.Automation.Text.TextPatternRangeEndpoint.Start,
                whole,
                System.Windows.Automation.Text.TextPatternRangeEndpoint.Start);
        });
    }

    /// <summary>
    /// List navigation over the LIVE merged document — the exact app
    /// path (merge → re-collected landmarks → navigator), not the
    /// detached built document the other navigation tests use. Guards
    /// the 2026-07-27 field failure where Ctrl+Alt+L produced nothing:
    /// if the live path breaks for lists, this fails locally instead of
    /// needing another manual NVDA round.
    /// </summary>
    [Fact]
    public void ListNavigationWorksOverTheLiveMergedDocument()
    {
        RunSta(() =>
        {
            ReadingDocumentModel model = BuildFixture();
            var surface = new ReadingSurface();
            var announced = new List<A11yEvent>();
            var navigator = new ReadingNavigator(surface, announced.Add);
            _ = System.Windows.Automation.Peers.UIElementAutomationPeer
                .CreatePeerForElement(surface);
            surface.ApplyBuiltDocument(model.Document);
            navigator.SetLandmarks(surface.LandmarksForTests);
            string Last() => SlateUniffiMethods.A11yRender(announced[^1]).Text;

            surface.CaretPosition = surface.Document.ContentStart;

            // Structure check first: the live document must still hold
            // FOUR list landmarks (bullet, nested, ordered, task) after
            // the merge — if the host normalized adjacent lists into
            // one, quick-nav stops silently vanish.
            Assert.Equal(
                4,
                surface.LandmarksForTests.Count(
                    l => l.Kind == ReadingLandmarkKind.List));

            // Four stops: bullet list, its nested list (a distinct list
            // and a distinct stop, per AT convention), then the ordered
            // run and the task run — which are SEPARATE lists even though
            // the ListItem blocks are consecutive, because ordered-ness
            // changed. One fused list with lying markers was the bug.
            navigator.Move(ReadingLandmarkKind.List, forward: true);
            Assert.Equal("first bullet, list.", Last());
            navigator.Move(ReadingLandmarkKind.List, forward: true);
            Assert.Equal("nested bullet, list.", Last());
            navigator.Move(ReadingLandmarkKind.List, forward: true);
            Assert.Equal("ordered one, list.", Last());
            navigator.Move(ReadingLandmarkKind.List, forward: true);
            Assert.Equal("a done task, list.", Last());
            navigator.Move(ReadingLandmarkKind.List, forward: true);
            Assert.Equal("No next list.", Last());
            navigator.Move(ReadingLandmarkKind.List, forward: false);
            Assert.Equal("ordered one, list.", Last());
        });
    }

    /// <summary>
    /// The core contract the list-splitting logic depends on: mixed
    /// consecutive list runs arrive with per-item ordered-ness.
    /// </summary>
    [Fact]
    public void CoreReportsOrderednessPerListItem()
    {
        ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(
            "- a\n- b\n\n1. one\n2. two\n\n- [ ] t\n");
        var flags = blocks
            .Select(b => b.Kind)
            .OfType<ReadingBlockKind.ListItem>()
            .Select(li => (li.Ordered, Task: li.Task is not null))
            .ToArray();
        Assert.Equal(
            new[]
            {
                (false, false), (false, false),
                (true, false), (true, false),
                (false, true),
            },
            flags);
    }

    /// <summary>
    /// The 2026-07-26 crash regression: keyboard focus landing on a
    /// Hyperlink INSIDE the reading document killed the app —
    /// MainWindow's ancestor-DataContext walk called
    /// VisualTreeHelper.GetParent on a ContentElement, which throws
    /// InvalidOperationException. The walk must traverse content
    /// elements logically until the tree re-enters a Visual.
    /// </summary>
    [Fact]
    public void AncestorDataContextWalkSurvivesFocusOnAHyperlink()
    {
        RunSta(() =>
        {
            ReadingDocumentModel model = BuildFixture();
            var sentinel = new object();
            var surface = new ReadingSurface { DataContext = sentinel };
            surface.ApplyBuiltDocument(model.Document);

            Hyperlink link = CollectHyperlinks(surface.Document).First();
            object? found = MainWindow.FindAncestorDataContext<object>(link);
            Assert.Same(sentinel, found);
        });
    }

    /// <summary>
    /// §10.3 activation, every row, through the REAL toggle path (tab VM
    /// with a live vault session, records from the scanned index): a
    /// resolved wikilink navigates through the editor's own seam, an
    /// unresolved one announces core's "is unresolved. Cannot open.", a
    /// tag routes to the tag seam, a citation speaks core's speech text,
    /// and an embed activates via ReadingMatchLink with embed:true. The
    /// run kinds come off the built document's own hyperlinks — exactly
    /// what a click delivers — so nothing is re-derived in the test
    /// either.
    /// </summary>
    [Fact]
    public void ActivationRoutesEveryRunKindThroughCoreAndTheEditorSeams()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-activation");
            File.WriteAllText(
                Path.Combine(fixture.Root, "known.md"), "# Known\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "See [[known]] and [[absent]] and #atag and [@smith2020].\n\n![[known]]\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var navigations = new List<EditorNavigationRequest>();
            var tags = new List<string>();
            var announced = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                navigate: navigations.Add,
                activateTag: tags.Add,
                announce: announced.Add,
                startInteractionBackgroundWork: false);

            tab.ToggleViewMode();
            SlateWindows.Reading.ReadingContentViewModel reading = tab.Reading!;
            var opened = new List<string>();
            reading.SetExternalOpenerForTests(url =>
            {
                opened.Add(url);
                return true;
            });

            ReadingInlineRunKind[] kinds = CollectHyperlinks(reading.Document!)
                .Select(link => link.Tag)
                .OfType<ReadingInlineRunKind>()
                .ToArray();
            // known, absent, #atag, and the citation — core classifies
            // [@smith2020] as a Citation run even with no configured CSL
            // style (unmatched raw), so it stays activatable.
            Assert.Equal(4, kinds.Length);
            Assert.IsType<ReadingInlineRunKind.Citation>(kinds[3]);

            // Resolved wikilink → the editor's navigation seam, no
            // announcement — and in a NEW tab by default (G22 owner call).
            reading.Activate(kinds[0]);
            EditorNavigationRequest navigation = Assert.Single(navigations);
            Assert.Equal("known.md", navigation.Path);
            Assert.True(navigation.OpenInNewTab);

            // The Editor-menu preference flips activation back to the
            // mac-style in-place navigation.
            tab.EditorPreferences.OpenReadingLinksInNewTab = false;
            navigations.Clear();
            reading.Activate(kinds[0]);
            Assert.False(Assert.Single(navigations).OpenInNewTab);
            tab.EditorPreferences.OpenReadingLinksInNewTab = true;
            navigations.Clear();
            reading.Activate(kinds[0]);
            Assert.True(Assert.Single(navigations).OpenInNewTab);
            navigations.Clear();

            // Unresolved wikilink → core's canonical refusal.
            announced.Clear();
            reading.Activate(kinds[1]);
            Assert.Empty(navigations.Skip(1));
            Assert.Equal(
                "absent is unresolved. Cannot open.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);

            // Tag → the tag seam.
            reading.Activate(kinds[2]);
            Assert.Equal("atag", Assert.Single(tags));

            // External link → the system opener, announced.
            announced.Clear();
            reading.Activate(new ReadingInlineRunKind.ExternalLink("https://example.com/x"));
            Assert.Equal("https://example.com/x", Assert.Single(opened));
            Assert.Equal(
                "Opened external link in default browser.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);

            // Embed → ReadingMatchLink(embed: true) → navigation.
            navigations.Clear();
            reading.Activate(new ReadingInlineRunKind.Embed("known"));
            Assert.Equal("known.md", Assert.Single(navigations).Path);

            // Citation → core speech verbatim.
            announced.Clear();
            reading.Activate(new ReadingInlineRunKind.Citation("[@x]", "Speech text."));
            Assert.Equal(
                "Speech text.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);
        });
    }

    /// <summary>
    /// Enter with the caret INSIDE a link activates it — caret position
    /// is not element focus, so without this the keyboard has no
    /// activation path at all (measured 2026-07-27: plain Enter did
    /// nothing on a landed link).
    /// </summary>
    [Fact]
    public void EnterActivatesTheLinkAtTheCaret()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-enter");
            File.WriteAllText(Path.Combine(fixture.Root, "known.md"), "# Known\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "Go to [[known]] now.\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var navigations = new List<EditorNavigationRequest>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                navigate: navigations.Add,
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();

            var surface = new ReadingSurface { Model = tab.Reading };
            // The caret setter cannot normalize a position INSIDE a
            // hyperlink without a text view — it silently snaps to the
            // document start. The field always has layout; give the test
            // host the same (the container-spike PeerProbe pattern).
            surface.Measure(new System.Windows.Size(900, 4000));
            surface.Arrange(new System.Windows.Rect(0, 0, 900, 4000));
            surface.UpdateLayout();

            var navigator = new ReadingNavigator(surface, _ => { });
            navigator.SetLandmarks(surface.LandmarksForTests);

            // Land on the link exactly as a user does — the chord — then
            // Enter.
            surface.CaretPosition = surface.Document.ContentStart;
            navigator.Move(ReadingLandmarkKind.Link, forward: true);

            Assert.True(surface.TryActivateAtCaret());
            Assert.Equal("known.md", Assert.Single(navigations).Path);

            // Caret OUTSIDE any link: no activation, no false positive.
            surface.CaretPosition = surface.Document.ContentStart;
            Assert.False(surface.TryActivateAtCaret());
            Assert.Single(navigations);
        });
    }

    /// <summary>
    /// Navigation replaces the tab's item in place; a reading-mode tab
    /// must re-project. Measured 2026-07-27: activating [[Target Note]]
    /// retitled the tab but the surface kept reading the OLD note —
    /// ReplaceItem disposed the reading VM and never rebuilt it.
    /// </summary>
    [Fact]
    public void NavigationReprojectsTheReadingSurface()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-navigate");
            File.WriteAllText(
                Path.Combine(fixture.Root, "known.md"), "# Known target\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "Go to [[known]] now.\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            SlateWindows.Reading.ReadingContentViewModel before = tab.Reading!;
            Assert.Contains(
                "Go to",
                new TextRange(
                    before.Document!.ContentStart,
                    before.Document.ContentEnd).Text);

            tab.ReplaceItem(new WorkspaceItemState(WorkspaceItemKind.Markdown, "known.md"));

            SlateWindows.Reading.ReadingContentViewModel after =
                Assert.IsType<SlateWindows.Reading.ReadingContentViewModel>(tab.Reading);
            Assert.NotSame(before, after);
            Assert.True(tab.IsReadingMode, "reading mode persists across navigation");
            Assert.Contains(
                "Known target",
                new TextRange(
                    after.Document!.ContentStart,
                    after.Document.ContentEnd).Text);
        });
    }

    /// <summary>
    /// G22 default: a reading-view link activation opens the target in a
    /// NEW tab — the reading tab keeps its note, its mode, and therefore
    /// the reader's position. (Editor activation is separately pinned to
    /// current-tab by WorkspaceNavigation_UsesCoreHeadingAndBlockArtifacts…)
    /// </summary>
    [Fact]
    public void ReadingActivationOpensTheTargetInANewTabByDefault()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-newtab");
            File.WriteAllText(
                Path.Combine(fixture.Root, "known.md"), "# Known target\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "Go to [[known]] now.\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var workspace = new WorkspaceViewModel(
                session,
                fixture.Root,
                () => [],
                _ => { },
                startInteractionBackgroundWork: false);
            workspace.OpenPath("note0.md");
            WorkspaceTabViewModel reader = workspace.ActiveGroup.ActiveTab!;
            reader.ToggleViewMode();

            ReadingInlineRunKind kind = CollectHyperlinks(reader.Reading!.Document!)
                .Select(link => link.Tag)
                .OfType<ReadingInlineRunKind>()
                .First();
            reader.Reading!.Activate(kind);

            Assert.Equal(2, workspace.ActiveGroup.Tabs.Count);
            Assert.Equal("known.md", workspace.ActiveGroup.ActiveTab!.Path);
            Assert.Equal("note0.md", reader.Path);
            Assert.True(reader.IsReadingMode, "the reading tab is undisturbed");

            // Re-activating never duplicates: TryOpenItem reuses the
            // already-open target tab.
            workspace.ActiveGroup.ActiveTab = reader;
            reader.Reading!.Activate(kind);
            Assert.Equal(2, workspace.ActiveGroup.Tabs.Count);
            Assert.Equal("known.md", workspace.ActiveGroup.ActiveTab!.Path);
        });
    }

    /// <summary>
    /// The Editor-menu preference restores the mac-style in-place
    /// navigation end-to-end: same tab, reading mode kept, surface
    /// re-projected onto the target note.
    /// </summary>
    [Fact]
    public void ReadingActivationHonorsTheCurrentTabPreference()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-curtab");
            File.WriteAllText(
                Path.Combine(fixture.Root, "known.md"), "# Known target\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "Go to [[known]] now.\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var workspace = new WorkspaceViewModel(
                session,
                fixture.Root,
                () => [],
                _ => { },
                startInteractionBackgroundWork: false);
            workspace.EditorPreferences.OpenReadingLinksInNewTab = false;
            workspace.OpenPath("note0.md");
            WorkspaceTabViewModel reader = workspace.ActiveGroup.ActiveTab!;
            reader.ToggleViewMode();

            ReadingInlineRunKind kind = CollectHyperlinks(reader.Reading!.Document!)
                .Select(link => link.Tag)
                .OfType<ReadingInlineRunKind>()
                .First();
            reader.Reading!.Activate(kind);

            WorkspaceTabViewModel active = workspace.ActiveGroup.ActiveTab!;
            Assert.Same(reader, active);
            Assert.Single(workspace.ActiveGroup.Tabs);
            Assert.Equal("known.md", active.Path);
            Assert.True(active.IsReadingMode);
            Assert.Contains(
                "Known target",
                new TextRange(
                    active.Reading!.Document!.ContentStart,
                    active.Reading.Document.ContentEnd).Text);
        });
    }

    /// <summary>
    /// Task checkbox activation, full round trip: the builder-stamped
    /// block range matches the core TaskItem by byte containment, the
    /// tab's core task command edits buffer AND disk, the canonical
    /// "Task completed." announces, and the re-projection renders the
    /// new state. The reading surface never edits text itself.
    /// </summary>
    [Fact]
    public void TaskCheckboxTogglesThroughTheCoreTaskCommand()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-task");
            string notePath = Path.Combine(fixture.Root, "note0.md");
            File.WriteAllText(
                notePath, "# Tasks\n\n- [ ] task one\n- [x] task two\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var announced = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                announce: announced.Add,
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            SlateWindows.Reading.ReadingContentViewModel reading = tab.Reading!;

            System.Windows.Controls.CheckBox[] boxes =
                CollectCheckBoxes(reading.Document!).ToArray();
            Assert.Equal(2, boxes.Length);
            Assert.All(boxes, box => Assert.True(box.IsEnabled));
            Assert.False(boxes[0].IsChecked);
            Assert.True(boxes[1].IsChecked);

            (ulong start, ulong end) = Assert.IsType<(ulong, ulong)>(boxes[0].Tag);
            reading.ToggleTaskAt(start, end);
            WaitForUi(() => tab.Text.Contains("- [x] task one", StringComparison.Ordinal));
            Assert.False(tab.IsDirty);
            Assert.Contains(
                "- [x] task one",
                File.ReadAllText(notePath),
                StringComparison.Ordinal);
            Assert.Contains(
                announced,
                item => SlateUniffiMethods.A11yRender(item).Text == "Task completed.");

            reading.Refresh();
            boxes = CollectCheckBoxes(reading.Document!).ToArray();
            Assert.True(boxes[0].IsChecked);
            Assert.True(boxes[1].IsChecked);
        });
    }

    /// <summary>
    /// The #158 rule holds in reading mode: a dirty buffer refuses the
    /// toggle with the editor's canonical announcement, and nothing is
    /// written anywhere.
    /// </summary>
    [Fact]
    public void TaskToggleRefusesWhileTheBufferIsDirty()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-task-dirty");
            string notePath = Path.Combine(fixture.Root, "note0.md");
            File.WriteAllText(notePath, "- [ ] task one\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var announced = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                announce: announced.Add,
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            SlateWindows.Reading.ReadingContentViewModel reading = tab.Reading!;
            (ulong start, ulong end) = Assert.IsType<(ulong, ulong)>(
                CollectCheckBoxes(reading.Document!).Single().Tag);

            tab.EditorDocument!.Insert(0, "edited ");
            WaitForUi(() => tab.IsDirty);

            reading.ToggleTaskAt(start, end);
            Assert.Contains(announced, item => item is A11yEvent.TaskToggleUnsaved);
            Assert.DoesNotContain("[x]", File.ReadAllText(notePath), StringComparison.Ordinal);
        });
    }

    /// <summary>A range no published task matches (snapshot older than
    /// the click) refuses with the interim not-ready wording instead of
    /// silently doing nothing.</summary>
    [Fact]
    public void TaskToggleAnnouncesWhenTheSnapshotIsStale()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-task-stale");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "- [ ] task one\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var announced = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                announce: announced.Add,
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();

            tab.Reading!.ToggleTaskAt(ulong.MaxValue - 1, ulong.MaxValue);
            Assert.Equal(
                "Tasks are still loading; try again.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);
        });
    }

    /// <summary>
    /// A same-note re-projection (task toggle, live edit) must not throw
    /// the reader to the document start: the caret's symbol offset
    /// survives the block merge.
    /// </summary>
    [Fact]
    public void ReprojectionPreservesTheCaretPosition()
    {
        RunSta(() =>
        {
            var surface = new ReadingSurface();
            surface.ApplyBuiltDocument(BuildFixture().Document);

            surface.CaretPosition =
                surface.Document.ContentStart.GetPositionAtOffset(12)
                ?? surface.Document.ContentEnd;
            int before = surface.Document.ContentStart.GetOffsetToPosition(
                surface.CaretPosition);
            Assert.True(before > 0);

            surface.ApplyBuiltDocument(BuildFixture().Document);
            Assert.Equal(
                before,
                surface.Document.ContentStart.GetOffsetToPosition(surface.CaretPosition));
        });
    }

    /// <summary>
    /// §W3-1 item 9, ceiling behavior: a large note streams in chunks —
    /// the first publish is bounded, later chunks append across
    /// dispatcher passes (never one monolithic build), and the final
    /// document carries every block with the landmark index covering
    /// all of it.
    /// </summary>
    [Fact]
    public void LargeNoteStreamsInChunksIntoTheSurface()
    {
        RunSta(() =>
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < 600; i++)
            {
                text.Append("Paragraph number ").Append(i).Append(".\n\n");
            }
            text.Append("# Tail heading\n");

            using var fixture = FixtureVault.Create(1, "reading-stream");
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), text.ToString());
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            // The ASYNC pipeline on purpose — chunk scheduling is the
            // behavior under test.
            using var reading = new SlateWindows.Reading.ReadingContentViewModel(
                session, tab, _ => { });
            var surface = new ReadingSurface { Model = reading };

            reading.Refresh();
            WaitForUi(() => reading.Document is not null);
            int firstPublish = surface.Document.Blocks.Count;
            Assert.True(
                firstPublish
                    <= SlateWindows.Reading.ReadingContentViewModel.BuildChunkBlocks + 8,
                $"first publish held {firstPublish} blocks — not a bounded chunk");

            WaitForUi(() => surface.Document.Blocks.Count >= 601);
            Assert.Equal(601, surface.Document.Blocks.Count);
            // The landmark index covers the streamed tail.
            Assert.Contains(
                surface.LandmarksForTests,
                landmark => landmark.Kind == ReadingLandmarkKind.Heading);
        });
    }

    /// <summary>
    /// §W3-1 item 9, above the ceiling: the deliberate degraded mode —
    /// first 2,000 blocks render, the terminal notice paragraph says
    /// so, and the announcement narrates it.
    /// </summary>
    [Fact]
    public void NotesBeyondTheCeilingDegradeDeliberately()
    {
        RunSta(() =>
        {
            int total = SlateWindows.Reading.ReadingContentViewModel.MaximumRenderedBlocks + 100;
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < total; i++)
            {
                text.Append("Block ").Append(i).Append(".\n\n");
            }

            using var fixture = FixtureVault.Create(1, "reading-degraded");
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), text.ToString());
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var announced = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                announce: announced.Add,
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            int rendered = SlateWindows.Reading.ReadingContentViewModel.MaximumRenderedBlocks;
            // Rendered blocks + the terminal notice.
            Assert.Equal(rendered + 1, surface.Document.Blocks.Count);
            var notice = Assert.IsType<Paragraph>(surface.Document.Blocks.LastBlock);
            Assert.Equal(
                "ReadingDegradedNotice",
                System.Windows.Automation.AutomationProperties.GetAutomationId(notice));
            string spoken = Assert.Single(
                announced.Select(item => SlateUniffiMethods.A11yRender(item).Text),
                item => item.Contains("first 2,000 blocks", StringComparison.Ordinal));
            Assert.Contains("Switch to the editor", spoken, StringComparison.Ordinal);
        });
    }

    /// <summary>Chunk boundaries never split an authored list: a list
    /// run crossing the chunk size stays ONE list (structure and
    /// quick-nav stop count are load-bearing, G20).</summary>
    [Fact]
    public void ChunkBoundariesNeverSplitAList()
    {
        RunSta(() =>
        {
            var text = new System.Text.StringBuilder();
            // 240 paragraphs, then a 30-item list straddling the
            // 250-block chunk boundary.
            for (int i = 0; i < 240; i++)
            {
                text.Append("Paragraph ").Append(i).Append(".\n\n");
            }
            for (int i = 0; i < 30; i++)
            {
                text.Append("- item ").Append(i).Append('\n');
            }

            using var fixture = FixtureVault.Create(1, "reading-chunk-list");
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), text.ToString());
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            System.Windows.Documents.List list = Assert.Single(
                surface.Document.Blocks.OfType<System.Windows.Documents.List>());
            Assert.Equal(30, list.ListItems.Count);
        });
    }

    /// <summary>
    /// Two reading-mode tabs sharing the one templated surface: the
    /// merge consumes each model's published blocks, so switching back
    /// rendered a BLANK surface until rebinding re-projects
    /// (EnsureProjected). Pins the fix.
    /// </summary>
    [Fact]
    public void RebindingBetweenTwoReadingTabsReprojectsBoth()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-rebind");
            File.WriteAllText(
                Path.Combine(fixture.Root, "first.md"), "# First note body\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "second.md"), "# Second note body\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var first = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "first.md")),
                startInteractionBackgroundWork: false);
            using var second = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "second.md")),
                startInteractionBackgroundWork: false);
            first.ToggleViewMode();
            second.ToggleViewMode();

            var surface = new ReadingSurface { Model = first.Reading };
            Assert.Contains("First note body", SurfaceText(surface));

            surface.Model = second.Reading;
            Assert.Contains("Second note body", SurfaceText(surface));

            surface.Model = first.Reading;
            Assert.Contains("First note body", SurfaceText(surface));

            surface.Model = second.Reading;
            Assert.Contains("Second note body", SurfaceText(surface));
        });
    }

    /// <summary>
    /// Adversarial-review fix: a prose-only note has content but ZERO
    /// landmarks, and the old landmark-gated focus rule stranded its
    /// readers on the collapsed editor. Blocks, not landmarks, are the
    /// focus condition.
    /// </summary>
    [Fact]
    public void ProseOnlyNotesClaimFocusWhenContentArrives()
    {
        RunSta(() =>
        {
            ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(
                "Just a paragraph.\n\nAnother paragraph.\n");
            ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
                "Just a paragraph.\n\nAnother paragraph.\n",
                Array.Empty<RenderedCitation>(),
                Array.Empty<OutgoingLink>());
            var model = new List<(ReadingBlock, ReadingBlockInlines)>();
            for (int i = 0; i < blocks.Length && i < inlines.Length; i++)
            {
                model.Add((blocks[i], inlines[i]));
            }

            var surface = new ReadingSurface();
            surface.ApplyBuiltDocument(ReadingDocumentBuilder.Build(model).Document);

            Assert.Empty(surface.LandmarksForTests);
            Assert.True(surface.Document.Blocks.Count > 0);
            Assert.True(ReadingSurface.ClaimsFocusAfterApply(
                isVisible: true,
                isKeyboardFocusWithin: false,
                surface.Document.Blocks.Count));
        });
    }

    /// <summary>
    /// Adversarial-review fix: nested lists build from the exact
    /// core-provided depth — an ordered sublist nests INSIDE its bullet
    /// parent without resetting it, and three levels chain instead of
    /// flattening to one.
    /// </summary>
    [Fact]
    public void NestedListsKeepTheirAuthoredDepthAndMarkers()
    {
        RunSta(() =>
        {
            FlowDocument mixed = BuildSource(
                "- alpha\n  1. beta\n  2. gamma\n- delta\n");
            System.Windows.Documents.List outer = Assert.Single(
                mixed.Blocks.OfType<System.Windows.Documents.List>());
            Assert.Equal(System.Windows.TextMarkerStyle.Disc, outer.MarkerStyle);
            ListItem[] outerItems = outer.ListItems.Cast<ListItem>().ToArray();
            Assert.Equal(2, outerItems.Length);
            System.Windows.Documents.List sublist = Assert.Single(
                outerItems[0].Blocks.OfType<System.Windows.Documents.List>());
            Assert.Equal(System.Windows.TextMarkerStyle.Decimal, sublist.MarkerStyle);
            Assert.Equal(2, sublist.ListItems.Count);
            Assert.Empty(outerItems[1].Blocks.OfType<System.Windows.Documents.List>());

            FlowDocument deep = BuildSource(
                "- one\n  - two\n    - three\n");
            System.Windows.Documents.List level0 = Assert.Single(
                deep.Blocks.OfType<System.Windows.Documents.List>());
            ListItem item0 = Assert.Single(level0.ListItems.Cast<ListItem>());
            System.Windows.Documents.List level1 = Assert.Single(
                item0.Blocks.OfType<System.Windows.Documents.List>());
            ListItem item1 = Assert.Single(level1.ListItems.Cast<ListItem>());
            System.Windows.Documents.List level2 = Assert.Single(
                item1.Blocks.OfType<System.Windows.Documents.List>());
            Assert.Single(level2.ListItems.Cast<ListItem>());
        });
    }

    /// <summary>
    /// Adversarial-review fix: the ceiling is ABSOLUTE. An all-list
    /// note cannot extend past it — exactly the ceiling's item count
    /// renders (one list, cut mid-run, continued by nothing), the
    /// notice follows, and the announcement fires once.
    /// </summary>
    [Fact]
    public void AllListNotesCannotBypassTheCeiling()
    {
        RunSta(() =>
        {
            int total = SlateWindows.Reading.ReadingContentViewModel.MaximumRenderedBlocks + 500;
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < total; i++)
            {
                text.Append("- item ").Append(i).Append('\n');
            }

            using var fixture = FixtureVault.Create(1, "reading-all-list");
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), text.ToString());
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var announced = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                announce: announced.Add,
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            // One list (chunk cuts continued it through the shared
            // context) holding exactly the ceiling's items, plus the
            // notice paragraph.
            System.Windows.Documents.List list = Assert.Single(
                surface.Document.Blocks.OfType<System.Windows.Documents.List>());
            Assert.Equal(
                SlateWindows.Reading.ReadingContentViewModel.MaximumRenderedBlocks,
                list.ListItems.Count);
            var notice = Assert.IsType<Paragraph>(surface.Document.Blocks.LastBlock);
            Assert.Equal(
                "ReadingDegradedNotice",
                System.Windows.Automation.AutomationProperties.GetAutomationId(notice));
            Assert.Single(
                announced.Select(item => SlateUniffiMethods.A11yRender(item).Text),
                item => item.Contains("first 2,000 blocks", StringComparison.Ordinal));
        });
    }

    /// <summary>
    /// Adversarial-review fix: a terminal refresh failure is never a
    /// silent blank surface — loading clears, the failure announces
    /// with a recovery route, a notice document publishes when nothing
    /// is on screen, and existing content is preserved when it is.
    /// </summary>
    [Fact]
    public void TerminalRefreshFailureAnnouncesAndShowsANotice()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-fail");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "# Fine content\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var announced = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                announce: announced.Add,
                startInteractionBackgroundWork: false);
            using var reading = new SlateWindows.Reading.ReadingContentViewModel(
                session, tab, announced.Add, synchronousForTests: true);

            // Failure with NOTHING on screen: notice document + announcement.
            reading.FetchFaultForTests = () => new IOException("injected");
            reading.Refresh();
            Assert.False(reading.IsLoading);
            var notice = Assert.IsType<Paragraph>(
                Assert.IsType<FlowDocument>(reading.Document).Blocks.FirstBlock);
            Assert.Equal(
                "ReadingRefreshFailedNotice",
                System.Windows.Automation.AutomationProperties.GetAutomationId(notice));
            Assert.Contains(
                announced,
                item => SlateUniffiMethods.A11yRender(item).Text.Contains(
                    "could not load this note", StringComparison.Ordinal));

            // Recovery: the next refresh retries in full.
            reading.FetchFaultForTests = null;
            reading.Refresh();
            Assert.Contains(
                "Fine content",
                new TextRange(
                    reading.Document!.ContentStart,
                    reading.Document.ContentEnd).Text);

            // Failure with content on screen: stale beats empty.
            FlowDocument shown = reading.Document!;
            reading.FetchFaultForTests = () => new IOException("injected again");
            reading.Refresh();
            Assert.Same(shown, reading.Document);
        });
    }

    /// <summary>
    /// Adversarial-review round 2 fix: switching tabs mid-stream must
    /// KILL the outgoing model's stream — its chunk continuations hold
    /// list objects mounted in this shared surface, and without the
    /// detach cancellation note A's stream keeps growing a list shown
    /// under note B. Rebinding A afterwards re-projects in full (the
    /// completeness-gated skip refuses the torso).
    /// </summary>
    [Fact]
    public void MidStreamTabSwitchCannotMutateTheSurface()
    {
        RunSta(() =>
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < 600; i++)
            {
                text.Append("- item ").Append(i).Append('\n');
            }

            using var fixture = FixtureVault.Create(1, "reading-midstream");
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), text.ToString());
            File.WriteAllText(Path.Combine(fixture.Root, "other.md"), "# Other note\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tabA = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            using var tabB = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "other.md")),
                startInteractionBackgroundWork: false);
            // ASYNC models on purpose — the chunk lifecycle is what is
            // under test. B never refreshes: the not-yet-published
            // replacement is the contamination window.
            using var readingA = new SlateWindows.Reading.ReadingContentViewModel(
                session, tabA, _ => { });
            using var readingB = new SlateWindows.Reading.ReadingContentViewModel(
                session, tabB, _ => { });
            var surface = new ReadingSurface { Model = readingA };

            readingA.Refresh();
            WaitForUi(() => readingA.Document is not null);
            int mountedItems = Assert.Single(
                surface.Document.Blocks.OfType<System.Windows.Documents.List>())
                .ListItems.Count;
            Assert.InRange(
                mountedItems,
                1,
                SlateWindows.Reading.ReadingContentViewModel.BuildChunkBlocks);

            // Switch mid-stream to the not-yet-published model, then
            // drain everything A's orphaned stream had queued.
            surface.Model = readingB;
            for (int i = 0; i < 20; i++)
            {
                PumpOneBackgroundPass();
            }

            Assert.Equal(
                mountedItems,
                Assert.Single(
                    surface.Document.Blocks.OfType<System.Windows.Documents.List>())
                    .ListItems.Count);

            // Rebinding A re-projects in full — the canceled torso is
            // never trusted by the skip path.
            surface.Model = readingA;
            WaitForUi(() =>
                surface.Document.Blocks.OfType<System.Windows.Documents.List>()
                    .FirstOrDefault()?.ListItems.Count == 600);
        });
    }

    /// <summary>
    /// Adversarial-review round 2 fix: a terminal failure during a
    /// rebind recovery publishes the visible notice — Document being
    /// non-null proves nothing after another tab's apply cleared this
    /// model's blocks, and the old check left the surface blank.
    /// </summary>
    [Fact]
    public void TerminalFailureAfterConsumedRebindShowsTheNotice()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-fail-rebind");
            File.WriteAllText(
                Path.Combine(fixture.Root, "first.md"), "# First note body\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "second.md"), "# Second note body\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var first = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "first.md")),
                startInteractionBackgroundWork: false);
            using var second = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "second.md")),
                startInteractionBackgroundWork: false);
            first.ToggleViewMode();
            second.ToggleViewMode();

            var surface = new ReadingSurface { Model = first.Reading };
            Assert.Contains("First note body", SurfaceText(surface));
            surface.Model = second.Reading;
            Assert.Contains("Second note body", SurfaceText(surface));

            // First's blocks were cleared by second's apply. Rebind it
            // with its refresh terminally failing: the surface must
            // show the failure notice, never stay blank.
            first.Reading!.FetchFaultForTests = () => new IOException("injected");
            surface.Model = first.Reading;
            Assert.Contains("could not load this note", SurfaceText(surface));

            // Clearing the fault and rebinding recovers in full.
            first.Reading!.FetchFaultForTests = null;
            surface.Model = second.Reading;
            surface.Model = first.Reading;
            Assert.Contains("First note body", SurfaceText(surface));
        });
    }

    private static void PumpOneBackgroundPass()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ContextIdle,
            new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static FlowDocument BuildSource(string source)
    {
        ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(source);
        ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
            source, Array.Empty<RenderedCitation>(), Array.Empty<OutgoingLink>());
        var model = new List<(ReadingBlock, ReadingBlockInlines)>();
        for (int i = 0; i < blocks.Length && i < inlines.Length; i++)
        {
            model.Add((blocks[i], inlines[i]));
        }
        return ReadingDocumentBuilder.Build(model).Document;
    }

    private static string SurfaceText(ReadingSurface surface) =>
        new TextRange(
            surface.Document.ContentStart,
            surface.Document.ContentEnd).Text;

    private static IEnumerable<System.Windows.Controls.CheckBox> CollectCheckBoxes(
        FlowDocument document)
    {
        for (TextPointer pointer = document.ContentStart;
            pointer is not null && pointer.CompareTo(document.ContentEnd) < 0;
            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward)!)
        {
            if (pointer.GetPointerContext(LogicalDirection.Forward)
                    == TextPointerContext.ElementStart
                && pointer.GetAdjacentElement(LogicalDirection.Forward)
                    is InlineUIContainer { Child: System.Windows.Controls.CheckBox box })
            {
                yield return box;
            }
        }
    }

    /// <summary>Pump the STA dispatcher until the condition holds — the
    /// W2 convention for the task command's background+dispatch hop.</summary>
    private static void WaitForUi(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Asynchronous action timed out.");
            var frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            Thread.Yield();
        }
    }

    private static IEnumerable<Hyperlink> CollectHyperlinks(FlowDocument document)
    {
        for (TextPointer pointer = document.ContentStart;
            pointer is not null && pointer.CompareTo(document.ContentEnd) < 0;
            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward)!)
        {
            if (pointer.GetPointerContext(LogicalDirection.Forward)
                    == TextPointerContext.ElementStart
                && pointer.GetAdjacentElement(LogicalDirection.Forward) is Hyperlink link)
            {
                yield return link;
            }
        }
    }

    /// <summary>WPF text objects require STA; xunit runs MTA.</summary>
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
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
