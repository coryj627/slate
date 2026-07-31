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
            // Card chrome (the embed Jump link, AutomationId
            // "ReadingBlockEmbed") is excluded: the pin is about the
            // INLINE run population — the embed itself is a card, not
            // an inline link.
            Hyperlink[] links = CollectHyperlinks(model.Document)
                .Where(link => System.Windows.Automation.AutomationProperties
                    .GetAutomationId(link) != "ReadingBlockEmbed")
                .Where(link => !ReadingSemantics.IsCodeCopy(link))
                .ToArray();

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
                // W3-2: math blocks.
                System.Windows.Input.Key.M,
                // W3-3: diagram blocks; G aliases D (field 2026-07-30:
                // Ctrl+Alt+D never reached the app on the tester's
                // machine — the same theft class as L).
                System.Windows.Input.Key.D,
                System.Windows.Input.Key.G,
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

    /// <summary>Block quotes answer StyleId_Quote AND StyleName
    /// "Quote" through the decorator. NVDA consumes only StyleName (its
    /// "report style" setting, off by default — owner call, field pass
    /// 3 2026-07-31: zero visual change accepted over a visible
    /// prefix); StyleId stays for other ATs.</summary>
    [Fact]
    public void BlockQuotesAnswerThroughTheTextPatternStyleId()
    {
        RunSta(() =>
        {
            FlowDocument built = BuildSource(
                "plain paragraph\n\n> a quoted line\n");
            var surface = new ReadingSurface();
            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer
                .CreatePeerForElement(surface);
            surface.ApplyBuiltDocument(built);

            var provider = peer!.GetPattern(
                System.Windows.Automation.Peers.PatternInterface.Text)
                as System.Windows.Automation.Provider.ITextProvider;
            var decorated = Assert.IsType<HeadingStyleTextProvider>(provider);

            Paragraph quote = surface.Document.Blocks
                .OfType<Paragraph>()
                .Single(ReadingSemantics.IsQuote);
            surface.CaretPosition = quote.ContentStart;
            Assert.Equal(
                HeadingStyleTextProvider.StyleIdQuote,
                decorated.GetSelection()[0].GetAttributeValue(
                    HeadingStyleTextProvider.StyleIdAttribute));

            Paragraph plain = surface.Document.Blocks
                .OfType<Paragraph>()
                .First(p => !ReadingSemantics.IsQuote(p));
            surface.CaretPosition = plain.ContentStart;
            Assert.NotEqual(
                HeadingStyleTextProvider.StyleIdQuote,
                decorated.GetSelection()[0].GetAttributeValue(
                    HeadingStyleTextProvider.StyleIdAttribute));

            surface.CaretPosition = quote.ContentStart;
            Assert.Equal(
                HeadingStyleTextProvider.QuoteStyleName,
                decorated.GetSelection()[0].GetAttributeValue(
                    HeadingStyleTextProvider.StyleNameAttribute));
            surface.CaretPosition = plain.ContentStart;
            Assert.NotEqual(
                HeadingStyleTextProvider.QuoteStyleName,
                decorated.GetSelection()[0].GetAttributeValue(
                    HeadingStyleTextProvider.StyleNameAttribute));
        });
    }

    /// <summary>Synthetic style attributes are range-aware
    /// (adversarial round 1): a selection spanning quote and plain
    /// paragraphs answers MixedAttributeValue in either document
    /// order — never the start paragraph's value alone — and a
    /// uniform all-quote span answers the single value.</summary>
    [Fact]
    public void QuoteStyleAttributesReportMixedAcrossSpanningRanges()
    {
        RunSta(() =>
        {
            foreach (string source in new[]
            {
                "plain paragraph\n\n> a quoted line\n",
                "> a quoted line\n\nplain paragraph\n",
            })
            {
                FlowDocument built = BuildSource(source);
                var surface = new ReadingSurface();
                var peer = System.Windows.Automation.Peers
                    .UIElementAutomationPeer.CreatePeerForElement(surface);
                surface.ApplyBuiltDocument(built);
                var provider = peer!.GetPattern(
                    System.Windows.Automation.Peers.PatternInterface.Text)
                    as System.Windows.Automation.Provider.ITextProvider;

                surface.Selection.Select(
                    surface.Document.ContentStart, surface.Document.ContentEnd);
                var range = provider!.GetSelection()[0];
                Assert.Same(
                    System.Windows.Automation.TextPattern.MixedAttributeValue,
                    range.GetAttributeValue(
                        HeadingStyleTextProvider.StyleIdAttribute));
                Assert.Same(
                    System.Windows.Automation.TextPattern.MixedAttributeValue,
                    range.GetAttributeValue(
                        HeadingStyleTextProvider.StyleNameAttribute));
            }

            FlowDocument uniform = BuildSource(
                "> first quote\n\n> second quote\n");
            var uniformSurface = new ReadingSurface();
            var uniformPeer = System.Windows.Automation.Peers
                .UIElementAutomationPeer.CreatePeerForElement(uniformSurface);
            uniformSurface.ApplyBuiltDocument(uniform);
            var uniformProvider = uniformPeer!.GetPattern(
                System.Windows.Automation.Peers.PatternInterface.Text)
                as System.Windows.Automation.Provider.ITextProvider;
            uniformSurface.Selection.Select(
                uniformSurface.Document.ContentStart,
                uniformSurface.Document.ContentEnd);
            var uniformRange = uniformProvider!.GetSelection()[0];
            Assert.Equal(
                HeadingStyleTextProvider.QuoteStyleName,
                uniformRange.GetAttributeValue(
                    HeadingStyleTextProvider.StyleNameAttribute));
            Assert.Equal(
                HeadingStyleTextProvider.StyleIdQuote,
                uniformRange.GetAttributeValue(
                    HeadingStyleTextProvider.StyleIdAttribute));
        });
    }

    /// <summary>The paragraph walk has NO count ceiling (adversarial
    /// round 2: a 10k cap silently truncated documents a 64 KiB embed
    /// preview can legally exceed, hiding later quotes/headings from
    /// both GetAttributeValue and FindAttribute). 10,050 plain
    /// paragraphs precede the only quote; every synthetic answer must
    /// still see it.</summary>
    [Fact]
    public void SyntheticAttributeWalkSurvivesHugeDocuments()
    {
        RunSta(() =>
        {
            var source = new System.Text.StringBuilder();
            for (int i = 0; i < 10_050; i++)
            {
                source.Append('p').Append(i).Append("\n\n");
            }
            source.Append("> a quoted line\n");
            FlowDocument built = BuildSource(source.ToString());
            var surface = new ReadingSurface();
            var peer = System.Windows.Automation.Peers
                .UIElementAutomationPeer.CreatePeerForElement(surface);
            surface.ApplyBuiltDocument(built);
            var provider = peer!.GetPattern(
                System.Windows.Automation.Peers.PatternInterface.Text)
                as System.Windows.Automation.Provider.ITextProvider;
            var whole = provider!.DocumentRange;

            Assert.Same(
                System.Windows.Automation.TextPattern.MixedAttributeValue,
                whole.GetAttributeValue(
                    HeadingStyleTextProvider.StyleNameAttribute));

            var forward = whole.FindAttribute(
                HeadingStyleTextProvider.StyleNameAttribute,
                HeadingStyleTextProvider.QuoteStyleName,
                false);
            Assert.NotNull(forward);
            Assert.Contains(
                "a quoted line", forward!.GetText(-1), StringComparison.Ordinal);

            var backward = whole.FindAttribute(
                HeadingStyleTextProvider.StyleIdAttribute,
                HeadingStyleTextProvider.StyleIdQuote,
                true);
            Assert.NotNull(backward);
            Assert.Contains(
                "a quoted line", backward!.GetText(-1), StringComparison.Ordinal);
        });
    }

    /// <summary>FindAttribute resolves synthetic styles (adversarial
    /// round 1): WPF's own search cannot see the quote marker, so the
    /// decorator answers — both directions, headings too, and no
    /// false positives for values nothing carries.</summary>
    [Fact]
    public void FindAttributeLocatesSyntheticQuoteStyles()
    {
        RunSta(() =>
        {
            FlowDocument built = BuildSource(
                "# Heading one\n\nplain paragraph\n\n> a quoted line\n");
            var surface = new ReadingSurface();
            var peer = System.Windows.Automation.Peers
                .UIElementAutomationPeer.CreatePeerForElement(surface);
            surface.ApplyBuiltDocument(built);
            var provider = peer!.GetPattern(
                System.Windows.Automation.Peers.PatternInterface.Text)
                as System.Windows.Automation.Provider.ITextProvider;
            var whole = provider!.DocumentRange;

            var quote = whole.FindAttribute(
                HeadingStyleTextProvider.StyleNameAttribute,
                HeadingStyleTextProvider.QuoteStyleName,
                false);
            Assert.NotNull(quote);
            Assert.Contains(
                "a quoted line", quote!.GetText(-1), StringComparison.Ordinal);

            var backwardQuote = whole.FindAttribute(
                HeadingStyleTextProvider.StyleIdAttribute,
                HeadingStyleTextProvider.StyleIdQuote,
                true);
            Assert.NotNull(backwardQuote);
            Assert.Contains(
                "a quoted line",
                backwardQuote!.GetText(-1),
                StringComparison.Ordinal);

            var heading = whole.FindAttribute(
                HeadingStyleTextProvider.StyleIdAttribute,
                HeadingStyleTextProvider.StyleIdHeading1,
                false);
            Assert.NotNull(heading);
            Assert.Contains(
                "Heading one", heading!.GetText(-1), StringComparison.Ordinal);

            Assert.Null(whole.FindAttribute(
                HeadingStyleTextProvider.StyleNameAttribute,
                "Caption",
                false));

            // Degenerate range = empty by UIA definition (adversarial
            // round 3): a caret INSIDE the quote still answers
            // GetAttributeValue, but FindAttribute must return null in
            // both directions — not a zero-length self-match.
            Paragraph quoteParagraph = surface.Document.Blocks
                .OfType<Paragraph>()
                .Single(ReadingSemantics.IsQuote);
            surface.Selection.Select(
                quoteParagraph.ContentStart, quoteParagraph.ContentStart);
            var caret = provider!.GetSelection()[0];
            Assert.Equal(
                HeadingStyleTextProvider.QuoteStyleName,
                caret.GetAttributeValue(
                    HeadingStyleTextProvider.StyleNameAttribute));
            Assert.Null(caret.FindAttribute(
                HeadingStyleTextProvider.StyleNameAttribute,
                HeadingStyleTextProvider.QuoteStyleName,
                false));
            Assert.Null(caret.FindAttribute(
                HeadingStyleTextProvider.StyleIdAttribute,
                HeadingStyleTextProvider.StyleIdQuote,
                true));
        });
    }

    /// <summary>The Copy affordance announces as a BUTTON: the
    /// CodeCopyHyperlink peer overrides only the control type; Invoke
    /// and the in-range name stay HyperlinkAutomationPeer's (field,
    /// 2026-07-31: "link Copy code" misread an in-place action as
    /// navigation).</summary>
    [Fact]
    public void CopyCodeAnnouncesAsAButton()
    {
        RunSta(() =>
        {
            FlowDocument built = BuildSource("```rust\nfn f() {}\n```\n");
            var surface = new ReadingSurface();
            surface.ApplyBuiltDocument(built);
            Hyperlink copy = FindCopyLinks(surface.Document).Single();
            Assert.IsType<CodeCopyHyperlink>(copy);
            var peer = System.Windows.Automation.Peers
                .ContentElementAutomationPeer.CreatePeerForElement(copy);
            Assert.NotNull(peer);
            Assert.Equal(
                System.Windows.Automation.Peers.AutomationControlType.Button,
                peer!.GetAutomationControlType());
        });
    }

    /// <summary>The preamble is a CAPTION (owner call, field pass 3
    /// 2026-07-31): smaller than the 15px body with a tight margin —
    /// visible but subtle, the recorded divergence from mac's hidden
    /// AX-only preamble.</summary>
    [Fact]
    public void CodePreambleIsCaptionStyled()
    {
        RunSta(() =>
        {
            FlowDocument built = BuildSource("```rust\nfn f() {}\n```\n");
            var surface = new ReadingSurface();
            surface.ApplyBuiltDocument(built);
            Hyperlink copy = FindCopyLinks(surface.Document).Single();
            var paragraph = Assert.IsType<Paragraph>(copy.Parent);
            Assert.Equal(
                ReadingDocumentBuilder.PreambleCaptionFontSize,
                paragraph.FontSize);
            Assert.Equal(2, paragraph.Margin.Top);
        });
    }

    /// <summary>The code preamble is IN-RANGE text (field,
    /// 2026-07-30: the paragraph Name feeds object navigation, but
    /// linear caret reading arrowed straight into raw code with no
    /// structure cue), with the Copy affordance a labelled hyperlink
    /// on the same line.</summary>
    [Fact]
    public void CodePreambleReadsInTheTextRange()
    {
        RunSta(() =>
        {
            FlowDocument built = BuildSource("```rust\nfn f() {}\n```\n");
            string text = new TextRange(
                built.ContentStart, built.ContentEnd).Text;
            Assert.Contains(
                "Code block, rust, 1 line.", text, StringComparison.Ordinal);
            Assert.Contains("Copy code", text, StringComparison.Ordinal);
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
                .Where(link => System.Windows.Automation.AutomationProperties
                    .GetAutomationId(link) != "ReadingBlockEmbed")
                .Select(link => link.Tag)
                .OfType<ReadingInlineRunKind>()
                .ToArray();
            // known, absent, #atag, and the citation — core classifies
            // [@smith2020] as a Citation run even with no configured CSL
            // style (unmatched raw), so it stays activatable. The embed
            // card's Jump link (AutomationId "ReadingBlockEmbed") is
            // card chrome, not an inline run.
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

            Assert.True(ReadingSemantics.TryDecodeTaskRange(
                boxes[0].Tag, out ulong start, out ulong end));
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
            Assert.True(ReadingSemantics.TryDecodeTaskRange(
                CollectCheckBoxes(reading.Document!).Single().Tag,
                out ulong start,
                out ulong end));

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

            // Switch mid-stream to the not-yet-published model: A's
            // content leaves the tree IMMEDIATELY (round-3 fix — a
            // pending fetch must not leave the previous note readable
            // under the new tab), replaced by the loading placeholder.
            surface.Model = readingB;
            Assert.Empty(surface.Document.Blocks.OfType<System.Windows.Documents.List>());
            Assert.DoesNotContain("item 0", SurfaceText(surface), StringComparison.Ordinal);
            var loading = Assert.IsType<Paragraph>(surface.Document.Blocks.FirstBlock);
            Assert.Equal(
                "ReadingLoadingNotice",
                System.Windows.Automation.AutomationProperties.GetAutomationId(loading));

            // Drain everything A's orphaned stream had queued: the
            // canceled continuations must not repopulate anything.
            for (int i = 0; i < 20; i++)
            {
                PumpOneBackgroundPass();
            }
            Assert.Empty(surface.Document.Blocks.OfType<System.Windows.Documents.List>());

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

    /// <summary>
    /// Adversarial-review round 3 fix: exceptions the retry policy does
    /// not recognize (interop faults, teardown races) reach the SAME
    /// terminal-failure state as transient ones — before the outer
    /// boundary they faulted a discarded task and vanished.
    /// </summary>
    [Fact]
    public void UnexpectedFetchFaultsReachTheTerminalFailureState()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-fault-types");
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

            // Async pipeline, non-transient exception: no retries, one
            // terminal state.
            using var asyncReading = new SlateWindows.Reading.ReadingContentViewModel(
                session, tab, announced.Add);
            asyncReading.FetchFaultForTests = () => new InvalidOperationException("interop");
            asyncReading.Refresh();
            WaitForUi(() => asyncReading.Document is not null);
            Assert.False(asyncReading.IsLoading);
            Assert.Equal(
                "ReadingRefreshFailedNotice",
                System.Windows.Automation.AutomationProperties.GetAutomationId(
                    Assert.IsType<Paragraph>(asyncReading.Document!.Blocks.FirstBlock)));

            // Sync pipeline, teardown-shaped exception: same state.
            announced.Clear();
            using var syncReading = new SlateWindows.Reading.ReadingContentViewModel(
                session, tab, announced.Add, synchronousForTests: true);
            syncReading.FetchFaultForTests = () => new ObjectDisposedException("session");
            syncReading.Refresh();
            Assert.Equal(
                "ReadingRefreshFailedNotice",
                System.Windows.Automation.AutomationProperties.GetAutomationId(
                    Assert.IsType<Paragraph>(syncReading.Document!.Blocks.FirstBlock)));
            Assert.Contains(
                announced,
                item => SlateUniffiMethods.A11yRender(item).Text.Contains(
                    "could not load this note", StringComparison.Ordinal));
        });
    }

    /// <summary>
    /// Adversarial-review round 3 fix: tab teardown cascades into the
    /// reading projection — disposal stops its background work, and
    /// deactivation pauses buffer observation until the next surface
    /// bind resumes it.
    /// </summary>
    [Fact]
    public void TabLifecycleCascadesIntoTheReadingModel()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-lifecycle");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "# Body\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            SlateWindows.Reading.ReadingContentViewModel reading = tab.Reading!;
            tab.Dispose();
            Assert.True(reading.IsDisposedForTests);
            Assert.Null(tab.Reading);

            // Observation pause/resume across deactivation (the async
            // pipeline owns the observer; sync mode never attaches).
            using var observerTab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            using var observing = new SlateWindows.Reading.ReadingContentViewModel(
                session, observerTab, _ => { });
            observing.Activate();
            Assert.True(observing.ObservesEditorForTests);
            observing.Deactivate();
            Assert.False(observing.ObservesEditorForTests);
            WaitForUi(() => observing.Document is not null);
            observing.EnsureProjected();
            Assert.True(observing.ObservesEditorForTests);
        });
    }

    /// <summary>
    /// Adversarial-review round 4 fix: exceptions thrown on the
    /// DISPATCHER side (publish, chunk build/merge) reach the terminal
    /// state too — InvokeAsync delegates fault their discarded
    /// operation, invisible to the fetch task's boundary.
    /// </summary>
    [Fact]
    public void PublicationFaultsReachTheTerminalFailureState()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-publish-fault");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "# Fine content\n");
            var big = new System.Text.StringBuilder();
            for (int i = 0; i < 600; i++)
            {
                big.Append("- item ").Append(i).Append('\n');
            }
            File.WriteAllText(Path.Combine(fixture.Root, "big.md"), big.ToString());
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

            // First publish faults → notice; clearing the fault and
            // re-projecting recovers.
            using var reading = new SlateWindows.Reading.ReadingContentViewModel(
                session, tab, announced.Add);
            reading.PublishFaultForTests = () => new InvalidOperationException("wpf build");
            reading.Refresh();
            WaitForUi(() => reading.Document is not null);
            Assert.Equal(
                "ReadingRefreshFailedNotice",
                System.Windows.Automation.AutomationProperties.GetAutomationId(
                    Assert.IsType<Paragraph>(reading.Document!.Blocks.FirstBlock)));
            reading.PublishFaultForTests = null;
            reading.EnsureProjected();
            WaitForUi(() =>
                new TextRange(
                    reading.Document!.ContentStart,
                    reading.Document.ContentEnd).Text.Contains(
                        "Fine content", StringComparison.Ordinal));

            // A mid-stream CHUNK fault (second dispatcher pass) lands in
            // the same state instead of stranding the torso.
            using var bigTab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "big.md")),
                announce: announced.Add,
                startInteractionBackgroundWork: false);
            using var streaming = new SlateWindows.Reading.ReadingContentViewModel(
                session, bigTab, announced.Add);
            int faultCalls = 0;
            streaming.PublishFaultForTests = () =>
                ++faultCalls >= 2 ? new InvalidOperationException("chunk build") : null;
            announced.Clear();
            streaming.Refresh();
            WaitForUi(() =>
                streaming.Document is { Blocks.FirstBlock: Paragraph first }
                && System.Windows.Automation.AutomationProperties.GetAutomationId(first)
                    == "ReadingRefreshFailedNotice");
            Assert.Contains(
                announced,
                item => SlateUniffiMethods.A11yRender(item).Text.Contains(
                    "could not load this note", StringComparison.Ordinal));
        });
    }

    /// <summary>
    /// Adversarial-review round 4 fix: tab Deactivate also fires when
    /// focus merely moves to another split pane while this tab stays
    /// visible — so it must NOT pause reading observation (a peer-pane
    /// edit would leave the visible projection stale forever). The
    /// pause belongs to the surface detach, the true hidden signal.
    /// </summary>
    [Fact]
    public void SplitPaneFocusChangeKeepsAVisibleReadingProjectionLive()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-split-focus");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "# Body\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            using var reading = new SlateWindows.Reading.ReadingContentViewModel(
                session, tab, _ => { });
            reading.Activate();
            WaitForUi(() => reading.Document is not null);
            Assert.True(reading.ObservesEditorForTests);

            // Focus moves to another pane: the tab deactivates but stays
            // visible — observation must survive...
            tab.Deactivate();
            Assert.True(reading.ObservesEditorForTests);

            // ...so a peer-pane edit still re-projects.
            tab.EditorDocument!.Insert(
                tab.EditorDocument.TextLength, "\nPeer edit arrived.\n");
            WaitForUi(() =>
                new TextRange(
                    reading.Document!.ContentStart,
                    reading.Document.ContentEnd).Text.Contains(
                        "Peer edit arrived", StringComparison.Ordinal));

            // The surface unbinding is what pauses observation.
            var surface = new ReadingSurface { Model = reading };
            surface.Model = null;
            Assert.False(reading.ObservesEditorForTests);
        });
    }

    /// <summary>
    /// Adversarial-review round 5 fix: switching away and back BEFORE
    /// the first publication must restart the refresh the detach
    /// canceled — without live-refresh tracking the rebind's
    /// null-document branch returned and the surface sat on its
    /// loading placeholder forever.
    /// </summary>
    [Fact]
    public void RebindBeforeTheFirstPublishStillProjects()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-prepublish");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "# Body appears\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "other.md"), "# Other\n");
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
            using var readingA = new SlateWindows.Reading.ReadingContentViewModel(
                session, tabA, _ => { });
            using var readingB = new SlateWindows.Reading.ReadingContentViewModel(
                session, tabB, _ => { });

            // Bind, detach, rebind — all before a single dispatcher
            // pump, so A's first publication can never have landed.
            var surface = new ReadingSurface { Model = readingA };
            surface.Model = readingB;
            surface.Model = readingA;

            WaitForUi(() => SurfaceText(surface).Contains(
                "Body appears", StringComparison.Ordinal));
        });
    }

    /// <summary>
    /// Adversarial-review round 6 fix: the §10.1 publication gate
    /// rejects a fetch whose captured tuple drifted — and the session
    /// interaction generation is VAULT-WIDE, so an unrelated save
    /// during the fetch used to strand the projection (placeholder on
    /// first load, stale content after) with nothing scheduled to
    /// retry. A drift rejection now retires the live refresh and
    /// immediately re-refreshes with the latest tuple.
    /// </summary>
    [Fact]
    public void ConcurrentVaultWritesDoNotStrandTheProjection()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-drift");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "# First body\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "tasker.md"), "- [ ] bump target\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            using var reading = new SlateWindows.Reading.ReadingContentViewModel(
                session, tab, _ => { });

            // Phase 1 — initial projection: an unrelated core write
            // lands DURING the fetch (the seam runs between the
            // dispatcher's tuple capture and the publish).
            bool bumped = false;
            reading.FetchFaultForTests = () =>
            {
                if (!bumped)
                {
                    bumped = true;
                    _ = session.ToggleTaskStatus("tasker.md", 0, "x", null);
                }
                return null;
            };
            reading.Refresh();
            WaitForUi(() =>
                reading.Document is not null
                && new TextRange(
                    reading.Document.ContentStart,
                    reading.Document.ContentEnd).Text.Contains(
                        "First body", StringComparison.Ordinal));

            // Phase 2 — previously published projection: the buffer
            // changes, and the re-projection's fetch races another
            // unrelated write. The new text must still arrive without
            // any tab or mode cycle.
            bumped = false;
            reading.FetchFaultForTests = () =>
            {
                if (!bumped)
                {
                    bumped = true;
                    _ = session.ToggleTaskStatus("tasker.md", 0, " ", null);
                }
                return null;
            };
            tab.EditorDocument!.Insert(
                tab.EditorDocument.TextLength, "\nSecond body arrived.\n");
            reading.Refresh();
            WaitForUi(() =>
                new TextRange(
                    reading.Document!.ContentStart,
                    reading.Document.ContentEnd).Text.Contains(
                        "Second body arrived", StringComparison.Ordinal));
        });
    }

    /// <summary>
    /// Field repro (2026-07-26 NVDA pass): clicking a task checkbox in
    /// the ASYNC pipeline — buffer observation attached, debounce, the
    /// real ButtonBase.Click route, layout in place — must complete the
    /// round trip: text toggled, re-projection lands with the checkbox
    /// checked, NO terminal failure, and the caret still navigates
    /// afterwards. The sync-mode unit test missed all of this.
    /// </summary>
    [Fact]
    public void TaskToggleClickRoundTripSurvivesTheFieldPath()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-field-toggle");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "# Reading smoke test\n\n- [ ] an open task\n- [x] a done task\n");
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
                session, tab, announced.Add);
            var surface = new ReadingSurface { Model = reading };
            surface.Measure(new System.Windows.Size(900, 4000));
            surface.Arrange(new System.Windows.Rect(0, 0, 900, 4000));
            surface.UpdateLayout();
            reading.Activate();
            WaitForUi(() =>
                CollectCheckBoxes(surface.Document).Count() == 2);

            System.Windows.Controls.CheckBox box =
                CollectCheckBoxes(surface.Document).First();
            Assert.False(box.IsChecked);
            box.RaiseEvent(new System.Windows.RoutedEventArgs(
                System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            // The full field round trip: core command → buffer edit →
            // debounce → refresh → re-projection.
            WaitForUi(() => tab.Text.Contains("- [x] an open task", StringComparison.Ordinal));
            WaitForUi(() =>
                CollectCheckBoxes(surface.Document).FirstOrDefault()?.IsChecked == true);

            Assert.True(
                reading.LastTerminalFailureForTests is null,
                $"terminal failure: {reading.LastTerminalFailureForTests}");
            Assert.DoesNotContain(
                announced,
                item => SlateUniffiMethods.A11yRender(item).Text.Contains(
                    "could not load", StringComparison.Ordinal));

            // The caret survives the rebuild: park it at the start and
            // walk down — it must MOVE.
            surface.CaretPosition = surface.Document.ContentStart;
            System.Windows.Documents.EditingCommands.MoveDownByLine.Execute(null, surface);
            Assert.True(
                surface.Document.ContentStart.GetOffsetToPosition(surface.CaretPosition) > 1,
                "the caret did not move after the toggle re-projection");
        });
    }

    /// <summary>
    /// Field repro, second stage: the same click round trip with REAL
    /// keyboard focus in a shown window — the field click focuses the
    /// CheckBox, and the debounced re-projection then destroys the
    /// focused element mid-merge. The headless variant cannot see any
    /// of the focus-driven failure modes.
    /// </summary>
    [Fact]
    public void TaskToggleWithRealWindowFocusSurvives()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-field-focus");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "# Reading smoke test\n\n- [ ] an open task\n- [x] a done task\n");
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
                session, tab, announced.Add);
            var surface = new ReadingSurface { Model = reading };
            var window = new System.Windows.Window
            {
                Content = surface,
                Width = 900,
                Height = 700,
                ShowActivated = true,
                ShowInTaskbar = false,
                WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
                Left = -2000,
                Top = -2000,
            };
            try
            {
                window.Show();
                reading.Activate();
                WaitForUi(() => CollectCheckBoxes(surface.Document).Count() == 2);

                System.Windows.Controls.CheckBox box =
                    CollectCheckBoxes(surface.Document).First();
                // The field path: the mouse click gives the checkbox
                // real keyboard focus before Click is raised.
                Assert.True(box.Focus(), "checkbox did not take focus");
                box.RaiseEvent(new System.Windows.RoutedEventArgs(
                    System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

                WaitForUi(() => tab.Text.Contains(
                    "- [x] an open task", StringComparison.Ordinal));
                WaitForUi(() =>
                    CollectCheckBoxes(surface.Document).FirstOrDefault()?.IsChecked == true);

                Assert.True(
                    reading.LastTerminalFailureForTests is null,
                    $"terminal failure: {reading.LastTerminalFailureForTests}");
                Assert.DoesNotContain(
                    announced,
                    item => SlateUniffiMethods.A11yRender(item).Text.Contains(
                        "could not load", StringComparison.Ordinal));

                // The caret must still navigate after the focused
                // element was destroyed by the merge.
                _ = surface.Focus();
                surface.CaretPosition = surface.Document.ContentStart;
                System.Windows.Documents.EditingCommands.MoveDownByLine.Execute(null, surface);
                Assert.True(
                    surface.Document.ContentStart.GetOffsetToPosition(surface.CaretPosition) > 1,
                    "the caret did not move after the toggle re-projection");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Field repro, third stage: the EXACT smoke-note content (embed
    /// card, table, quote, code fence, thematic break) with the caret
    /// parked ON the task line — the field caret sat there, so the
    /// merge's offset-restore branch runs, which the earlier repros
    /// skipped at offset zero.
    /// </summary>
    [Fact]
    public void TaskToggleWithSmokeNoteContentAndParkedCaretSurvives()
    {
        RunSta(() =>
        {
            const string smoke =
                "# Reading smoke test\n\n"
                + "A paragraph with a resolved [[Target Note]] link, a missing "
                + "[[Nowhere Note]] link, a #smoke tag, an "
                + "[external link](https://example.com/reading), plus **bold**, "
                + "*italic* and `inline code`.\n\n"
                + "## Lists and tasks\n\n"
                + "- first bullet\n- second bullet\n  - nested bullet\n\n"
                + "1. ordered one\n2. ordered two\n\n"
                + "- [ ] an open task\n- [x] a done task\n\n"
                + "## Structures\n\n"
                + "> A block quote with another [[Target Note]] link.\n\n"
                + "```rust\nfn spoken_interior() -> usize { 42 }\n```\n\n"
                + "| column a | column b |\n| --- | --- |\n"
                + "| cell one | cell two |\n\n---\n\n![[Target Note]]\n";
            using var fixture = FixtureVault.Create(1, "reading-field-smoke");
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), smoke);
            File.WriteAllText(
                Path.Combine(fixture.Root, "Target Note.md"), "# Target\nBody.\n");
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
                session, tab, announced.Add);
            var surface = new ReadingSurface { Model = reading };
            var window = new System.Windows.Window
            {
                Content = surface,
                Width = 900,
                Height = 700,
                ShowInTaskbar = false,
                WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
                Left = -2000,
                Top = -2000,
            };
            try
            {
                window.Show();
                reading.Activate();
                WaitForUi(() => CollectCheckBoxes(surface.Document).Count() == 2);

                System.Windows.Controls.CheckBox box =
                    CollectCheckBoxes(surface.Document).First();
                // Park the caret ON the task line (the field position),
                // then take focus and click, exactly as the mouse does.
                var container = (InlineUIContainer)box.Parent;
                surface.CaretPosition = container.ElementStart;
                Assert.True(box.Focus(), "checkbox did not take focus");
                box.RaiseEvent(new System.Windows.RoutedEventArgs(
                    System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

                WaitForUi(() => tab.Text.Contains(
                    "- [x] an open task", StringComparison.Ordinal));
                WaitForUi(() =>
                    CollectCheckBoxes(surface.Document).FirstOrDefault()?.IsChecked == true);

                Assert.True(
                    reading.LastTerminalFailureForTests is null,
                    $"terminal failure: {reading.LastTerminalFailureForTests}");

                // Toggle the second checkbox too — the field hit both.
                System.Windows.Controls.CheckBox second =
                    CollectCheckBoxes(surface.Document).Last();
                Assert.True(second.IsChecked);
                surface.CaretPosition =
                    ((InlineUIContainer)second.Parent).ElementStart;
                _ = second.Focus();
                second.RaiseEvent(new System.Windows.RoutedEventArgs(
                    System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                WaitForUi(() => tab.Text.Contains(
                    "- [ ] a done task", StringComparison.Ordinal));
                WaitForUi(() =>
                    CollectCheckBoxes(surface.Document).LastOrDefault()?.IsChecked == false);
                Assert.True(
                    reading.LastTerminalFailureForTests is null,
                    $"terminal failure: {reading.LastTerminalFailureForTests}");
                Assert.DoesNotContain(
                    announced,
                    item => SlateUniffiMethods.A11yRender(item).Text.Contains(
                        "could not load", StringComparison.Ordinal));

                // Caret navigation must survive both rebuilds.
                _ = surface.Focus();
                surface.CaretPosition = surface.Document.ContentStart;
                System.Windows.Documents.EditingCommands.MoveDownByLine.Execute(null, surface);
                Assert.True(
                    surface.Document.ContentStart.GetOffsetToPosition(surface.CaretPosition) > 1,
                    "the caret did not move after the toggle re-projections");
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Field gap (2026-07-26): Space and Enter at the caret on a task
    /// line did nothing — a caret position is not element focus, so
    /// the checkbox never saw the key. Both now toggle through the
    /// caret path; Space on a non-task line stays unhandled.
    /// </summary>
    [Fact]
    public void SpaceAndEnterAtTheCaretToggleTheTaskLine()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-caret-toggle");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "# Heading\n\nPlain paragraph.\n\n- [ ] an open task\n");
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
            surface.Measure(new System.Windows.Size(900, 4000));
            surface.Arrange(new System.Windows.Rect(0, 0, 900, 4000));
            surface.UpdateLayout();

            // Caret on a non-task line: not handled.
            surface.CaretPosition = surface.Document.ContentStart;
            Assert.False(surface.TryToggleTaskAtCaret());

            // Caret on the task line: Space's path toggles.
            System.Windows.Controls.CheckBox box =
                CollectCheckBoxes(surface.Document).Single();
            surface.CaretPosition = ((InlineUIContainer)box.Parent).ElementStart;
            Assert.True(surface.TryToggleTaskAtCaret());
            WaitForUi(() => tab.Text.Contains(
                "- [x] an open task", StringComparison.Ordinal));

            // Enter's shared activation path reaches the same toggle
            // (no link at the caret → falls through to the task line).
            tab.Reading!.Refresh();
            box = CollectCheckBoxes(surface.Document).Single();
            Assert.True(box.IsChecked);
            surface.CaretPosition = ((InlineUIContainer)box.Parent).ElementStart;
            Assert.True(surface.TryActivateAtCaret());
            WaitForUi(() => tab.Text.Contains(
                "- [ ] an open task", StringComparison.Ordinal));
        });
    }

    /// <summary>
    /// Field root cause (2026-07-26): WPF's text engine SERIALIZES
    /// document content — undo records on every merge, XAML clipboard
    /// on copy — and XamlWriter refuses generic types, so a
    /// ValueTuple checkbox Tag turned the first post-click merge into
    /// "Reading view could not load this note" and a dead caret. Every
    /// stamped payload must survive TextRange.Save, and copying the
    /// whole reading document must never throw.
    /// </summary>
    [Fact]
    public void ReadingDocumentContentSurvivesClipboardSerialization()
    {
        RunSta(() =>
        {
            FlowDocument document = BuildSource(
                "# H\n\nA [[known]] link and #tag and [x](https://e.com) here.\n\n"
                + "- [ ] an open task\n\n![[known]]\n");
            using var stream = new MemoryStream();
            new TextRange(document.ContentStart, document.ContentEnd)
                .Save(stream, System.Windows.DataFormats.Xaml);
            Assert.True(stream.Length > 0);
        });
    }

    /// <summary>
    /// Adversarial round 8: Enter/Space mutate state, so the caret
    /// gate fires only on the physical press and only when the surface
    /// ITSELF owns focus — a held key must not replay the toggle, and
    /// a focused embedded checkbox owns its own keys (preview
    /// tunneling would otherwise hijack Space for the caret's task).
    /// </summary>
    [Fact]
    public void CaretActivationGateRejectsRepeatsAndEmbeddedFocus()
    {
        Assert.True(ReadingNavigator.CaretActivationApplies(
            surfaceIsKeyboardFocused: true, isRepeat: false));
        Assert.False(ReadingNavigator.CaretActivationApplies(
            surfaceIsKeyboardFocused: true, isRepeat: true));
        Assert.False(ReadingNavigator.CaretActivationApplies(
            surfaceIsKeyboardFocused: false, isRepeat: false));
        Assert.False(ReadingNavigator.CaretActivationApplies(
            surfaceIsKeyboardFocused: false, isRepeat: true));
    }

    /// <summary>
    /// W3-4: a saved fence matches its canonical CodeBlock by byte
    /// containment and renders TOKEN runs inside the text range — the
    /// W3-1 in-range invariant survives, the paragraph carries core's
    /// preamble, and the copy button precedes the code.
    /// </summary>
    [Fact]
    public void CodeFenceRendersCanonicalTokensInsideTheTextRange()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-code-tokens");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "# Code\n\n```rust\nfn answer() -> usize { 42 }\n```\n");
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

            Paragraph code = FindCodeParagraph(surface.Document);
            // Core's preamble, verbatim, on the paragraph's name.
            Assert.Equal(
                "Code block, rust, 1 line.",
                System.Windows.Automation.AutomationProperties.GetName(code));
            // Token runs: the paragraph is SPLIT (tokenized), and its
            // concatenated text is the authoritative interior.
            Assert.True(
                code.Inlines.Count > 1,
                $"expected token runs, got {code.Inlines.Count} inline(s)");
            Assert.Equal(
                "fn answer() -> usize { 42 }",
                string.Concat(code.Inlines.OfType<Run>().Select(run => run.Text)));
            // Still inside the text range (the W3-1 spike invariant).
            Assert.Contains(
                "fn answer()",
                SurfaceText(surface),
                StringComparison.Ordinal);
            // Landmark unchanged: one code-block stop.
            Assert.Contains(
                surface.LandmarksForTests,
                landmark => landmark.Kind == ReadingLandmarkKind.CodeBlock);
        });
    }

    /// <summary>
    /// W3-4: the Copy button copies the exact source as plain text and
    /// announces core's "Code copied."; it is marked so the click
    /// router never confuses it with an embed card.
    /// </summary>
    [Fact]
    public void CodeCopyButtonCopiesTheSourceAndAnnounces()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-code-copy");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "```rust\nfn copied() -> u8 { 7 }\n```\n");
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

            System.Windows.Documents.Hyperlink copy =
                FindCopyLinks(surface.Document).Single();
            Assert.Equal(
                "Copies the code block source as plain text.",
                System.Windows.Automation.AutomationProperties.GetHelpText(copy));
            copy.RaiseEvent(new System.Windows.RoutedEventArgs(
                System.Windows.Documents.Hyperlink.ClickEvent));

            // The LIVE fence interior (round-1 fix: never the saved
            // artifact) — the reading parser's authoritative form,
            // which strips the single trailing newline.
            Assert.Equal(
                "fn copied() -> u8 { 7 }",
                System.Windows.Clipboard.GetText());
            Assert.Equal(
                "Code copied.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);
        });
    }

    /// <summary>
    /// W3-4 drift fallback (mac codeModel parity): a fence with no
    /// matching pipeline block renders the authoritative interior
    /// un-highlighted, and the preamble is still correct — including
    /// the "plain text" spoken language for untagged fences.
    /// </summary>
    [Fact]
    public void UnmatchedFencesDegradeToPlainInteriorWithCorrectPreamble()
    {
        RunSta(() =>
        {
            FlowDocument document = BuildSource("```\nplain one\nplain two\n```\n");
            Paragraph code = FindCodeParagraph(document);
            Assert.Equal(
                "Code block, plain text, 2 lines.",
                System.Windows.Automation.AutomationProperties.GetName(code));
            Run run = Assert.IsType<Run>(Assert.Single(code.Inlines));
            Assert.Equal("plain one\nplain two", run.Text);
        });
    }

    /// <summary>
    /// W3-4: the code block's UIA peer speaks CORE's preamble as its
    /// Name (the K contract's "AT preamble behind a UIA peer") while
    /// the interior stays in the text range — object navigation gets
    /// the summary, say-all gets the code.
    /// </summary>
    [Fact]
    public void CodeBlockPeerSpeaksTheCanonicalPreamble()
    {
        RunSta(() =>
        {
            FlowDocument document = BuildSource(
                "```rust\nfn a() {}\nfn b() {}\n```\n");
            var children = new List<System.Windows.Automation.Peers.AutomationPeer>();
            foreach (Block block in document.Blocks)
            {
                ReadingSurfacePeer.AppendStructuralForTest(block, children);
            }
            System.Windows.Automation.Peers.AutomationPeer peer = Assert.Single(
                children,
                candidate => candidate is ReadingCodeBlockPeer);
            Assert.Equal("Code block, rust, 2 lines.", peer.GetName());
            Assert.Equal(
                System.Windows.Automation.Peers.AutomationControlType.Group,
                peer.GetAutomationControlType());
        });
    }

    /// <summary>
    /// W3-4 CodePrefs parity (shipped-mac behavior: persisted +
    /// announced; rendering stays preamble-only). The verbosity
    /// round-trips the store, announces through the canonical
    /// vocabulary, and rejects unknown keys.
    /// </summary>
    [Fact]
    public void CodeVerbosityPersistsAnnouncesAndRejectsUnknownKeys()
    {
        string directory = Path.Combine(
            Path.GetTempPath(), $"slate-code-verbosity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var store = new AppPreferencesStore(Path.Combine(directory, "preferences.json"));
            var announced = new List<A11yEvent>();
            using var preferences = new EditorPreferencesViewModel(
                announced.Add,
                new FakeEditorSpellingService(),
                preferencesStore: store);
            Assert.True(preferences.IsCodeVerbosityPreambleOnly);

            preferences.SetCodePreambleVerbosityCommand.Execute("preambleFirstLine");
            Assert.True(preferences.IsCodeVerbosityFirstLine);
            Assert.Equal(
                "Code preamble verbosity: Preamble + first line.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);

            preferences.SetCodePreambleVerbosityCommand.Execute("nonsense");
            Assert.True(preferences.IsCodeVerbosityFirstLine);
            Assert.Single(announced);

            using var second = new EditorPreferencesViewModel(
                _ => { },
                new FakeEditorSpellingService(),
                preferencesStore: store);
            Assert.True(second.IsCodeVerbosityFirstLine);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// W3-4 adversarial round 1 [high]: an unsaved edit INSIDE a fence
    /// keeps byte containment, and the saved artifact must then lose —
    /// display, preamble, and Copy all follow the LIVE interior;
    /// tokens simply drop until the save catches up.
    /// </summary>
    [Fact]
    public void DirtyFenceEditsNeverShowOrCopyStaleSavedCode()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-code-dirty");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "```rust\nfn saved() -> u8 { 1 }\n```\n");
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

            // Edit inside the fence without moving it, then re-project.
            tab.EditorDocument!.Replace(
                tab.Text.IndexOf("saved", StringComparison.Ordinal), 5, "live!");
            tab.Reading!.Refresh();

            Paragraph code = FindCodeParagraph(surface.Document);
            Assert.Contains("fn live!()", SurfaceText(surface), StringComparison.Ordinal);
            Assert.DoesNotContain("fn saved()", SurfaceText(surface), StringComparison.Ordinal);
            // Preamble counts the LIVE interior; tokens dropped (single
            // plain run) rather than lying.
            Assert.Equal(
                "Code block, rust, 1 line.",
                System.Windows.Automation.AutomationProperties.GetName(code));
            Assert.Single(code.Inlines);
            // Copy carries the live source.
            System.Windows.Documents.Hyperlink copy =
                FindCopyLinks(surface.Document).Single();
            Assert.Contains(
                "fn live!()", (string)copy.Tag, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// W3-4 adversarial round 1 [medium]: a degraded token fetch must
    /// not memo-match the successful fetch that recovers from it —
    /// highlighting returns with the live text unchanged.
    /// </summary>
    [Fact]
    public void DegradedTokenFetchesRecoverThroughTheMemo()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-code-recover");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "```rust\nfn recovered() -> u8 { 9 }\n```\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            using var reading = new SlateWindows.Reading.ReadingContentViewModel(
                session, tab, _ => { }, synchronousForTests: true);
            var surface = new ReadingSurface { Model = reading };

            // Degraded token fetch → plain single-run fence.
            reading.CodeTokenFaultForTests =
                () => new VaultException.Io("tokens unavailable");
            reading.Refresh();
            Assert.Single(FindCodeParagraph(surface.Document).Inlines);

            // Recovery with IDENTICAL live text: the degraded digest
            // must not memo-match — highlighting returns.
            reading.CodeTokenFaultForTests = null;
            reading.Refresh();
            Assert.True(
                FindCodeParagraph(surface.Document).Inlines.Count > 1,
                "tokens did not recover after the degraded fetch");

            // Baseline still holds: identical successful fetches
            // memo-hit and skip the rebuild.
            FlowDocument before = reading.Document!;
            reading.Refresh();
            Assert.Same(before, reading.Document);
        });
    }

    /// <summary>
    /// W3-4 adversarial round 2 [medium]: a SAME-LENGTH dirty edit
    /// (41 → 42) then a save leaves live text, offsets, lengths, and
    /// token counts identical — only CONTENT differs, so the memo
    /// digest must be content-complete or highlighting never returns.
    /// </summary>
    [Fact]
    public void SameLengthEditThenSaveRecoversHighlighting()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-code-save");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "```rust\nfn f() -> u8 { 41 }\n```\n");
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
            Assert.True(FindCodeParagraph(surface.Document).Inlines.Count > 1);

            // Same-length dirty edit inside the fence → incoherent →
            // plain run.
            tab.EditorDocument!.Replace(
                tab.Text.IndexOf("41", StringComparison.Ordinal), 2, "42");
            tab.Reading!.Refresh();
            Assert.Single(FindCodeParagraph(surface.Document).Inlines);

            // Save: live text unchanged from the dirty refresh, saved
            // artifact now coherent again — highlighting must return.
            Assert.True(tab.Save());
            tab.Reading!.Refresh();
            Assert.True(
                FindCodeParagraph(surface.Document).Inlines.Count > 1,
                "highlighting did not recover after the save");
            Assert.Contains(
                "42",
                string.Concat(
                    FindCodeParagraph(surface.Document)
                        .Inlines.OfType<Run>().Select(run => run.Text)),
                StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// W3-4 adversarial round 2 [high]: clipboard contention surfaces
    /// as ExternalException — the copy handler logs, announces the
    /// failure, and never lets it escape the click.
    /// </summary>
    [Fact]
    public void ClipboardContentionAnnouncesInsteadOfCrashing()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-code-clip");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "```rust\nfn x() {}\n```\n");
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
            tab.Reading!.ClipboardForTests = _ =>
                throw new System.Runtime.InteropServices.ExternalException("busy");

            tab.Reading!.CopyCode("anything");
            Assert.Equal(
                "Could not copy code. Try again.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);
        });
    }

    /// <summary>
    /// W3-4 adversarial round 1 [medium]: explicit JSON null and
    /// unknown verbosity values decode to the default instead of
    /// throwing out of initialization.
    /// </summary>
    [Fact]
    public void NullAndUnknownVerbosityValuesDecodeToTheDefault()
    {
        string directory = Path.Combine(
            Path.GetTempPath(), $"slate-verbosity-null-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "preferences.json");
            var store = new AppPreferencesStore(path);

            File.WriteAllText(
                path,
                "{\"readingLinksOpenInNewTab\":true,\"codePreambleVerbosity\":null}");
            Assert.Equal("preambleOnly", store.Load().CodePreambleVerbosity);
            using var fromNull = new EditorPreferencesViewModel(
                _ => { }, new FakeEditorSpellingService(), preferencesStore: store);
            Assert.True(fromNull.IsCodeVerbosityPreambleOnly);

            File.WriteAllText(
                path,
                "{\"codePreambleVerbosity\":\"futureMode\"}");
            using var fromUnknown = new EditorPreferencesViewModel(
                _ => { }, new FakeEditorSpellingService(), preferencesStore: store);
            Assert.True(fromUnknown.IsCodeVerbosityPreambleOnly);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// W3-4 adversarial round 2: CRLF-authored fences must keep their
    /// highlighting (the saved artifact preserves raw CRLF while the
    /// reading interior may normalize), and authored trailing blank
    /// lines survive display — the interior renders verbatim, so
    /// what is spoken, shown, and counted by the preamble agree.
    /// </summary>
    [Fact]
    public void CrlfFencesKeepTokensAndTrailingBlankLinesSurvive()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-code-crlf");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "```rust\r\nfn crlf() -> u8 { 3 }\r\n```\r\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "blanks.md"),
                "```rust\nfn a() {}\n\n\n```\n");
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
            Paragraph crlf = FindCodeParagraph(surface.Document);
            Assert.True(
                crlf.Inlines.Count > 1,
                "CRLF fence lost its highlighting — coherence must normalize line endings");
            Assert.Contains(
                "fn crlf()",
                string.Concat(crlf.Inlines.OfType<Run>().Select(run => run.Text)),
                StringComparison.Ordinal);

            using var blanksTab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "blanks.md")),
                startInteractionBackgroundWork: false);
            blanksTab.ToggleViewMode();
            var blanksSurface = new ReadingSurface { Model = blanksTab.Reading };
            Paragraph blanks = FindCodeParagraph(blanksSurface.Document);
            string displayed = string.Concat(
                blanks.Inlines.OfType<Run>().Select(run => run.Text));
            // Interior = "fn a() {}\n\n" (one trailing newline stripped
            // by the parser): the authored blank line SURVIVES display,
            // and the preamble counts the same content.
            Assert.EndsWith("fn a() {}\n\n", displayed.Length >= 2 ? displayed : "  ");
            Assert.Equal(
                "Code block, rust, 2 lines.",
                System.Windows.Automation.AutomationProperties.GetName(blanks));
        });
    }

    /// <summary>
    /// W3-4 adversarial round 3 [high]: a fence above core's 256 KiB
    /// highlighting cap arrives as one uncolored "oversized" token —
    /// the builder short-circuits to the plain paragraph with zero
    /// offset machinery instead of allocating per-byte maps on the
    /// dispatcher for nothing.
    /// </summary>
    [Fact]
    public void OversizedFencesShortCircuitToThePlainParagraph()
    {
        RunSta(() =>
        {
            var big = new System.Text.StringBuilder("```rust\n");
            while (big.Length < 300_000)
            {
                big.Append("fn filler() -> usize { 123456789 }\n");
            }
            big.Append("```\n");

            using var fixture = FixtureVault.Create(1, "reading-code-oversized");
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), big.ToString());
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

            Paragraph code = FindCodeParagraph(surface.Document);
            Assert.Single(code.Inlines);
            Assert.StartsWith(
                "Code block, rust,",
                System.Windows.Automation.AutomationProperties.GetName(code));
        });
    }

    /// <summary>
    /// W3-4 adversarial round 4 [high]: token DENSITY under the byte
    /// cap — a valid dense JSON fence would fan out a WPF Run per
    /// token plus gaps. Over the run budget the block degrades to one
    /// plain run, exactly like oversized.
    /// </summary>
    [Fact]
    public void DenseTokenFencesDegradeToThePlainParagraph()
    {
        RunSta(() =>
        {
            var dense = new System.Text.StringBuilder("```json\n[");
            for (int i = 0; i < 30_000; i++)
            {
                dense.Append("0,");
            }
            dense.Append("0]\n```\n");

            using var fixture = FixtureVault.Create(1, "reading-code-dense");
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), dense.ToString());
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

            Paragraph code = FindCodeParagraph(surface.Document);
            Assert.Single(code.Inlines);
        });
    }

    /// <summary>
    /// W3-4 adversarial round 5 [high]: many individually
    /// sub-threshold dense fences must not aggregate past the
    /// projection-wide budget — later fences degrade to plain runs,
    /// and total Run fan-out stays bounded.
    /// </summary>
    [Fact]
    public void ManySubThresholdFencesStayWithinTheProjectionBudget()
    {
        RunSta(() =>
        {
            var text = new System.Text.StringBuilder();
            for (int fence = 0; fence < 25; fence++)
            {
                text.Append("```json\n[");
                for (int i = 0; i < 400; i++)
                {
                    text.Append("0,");
                }
                text.Append("0]\n```\n\n");
            }

            using var fixture = FixtureVault.Create(1, "reading-code-aggregate");
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

            Paragraph[] fences = AllBlocks(surface.Document.Blocks)
                .OfType<Paragraph>()
                .Where(ReadingSemantics.IsCodeBlock)
                .ToArray();
            Assert.Equal(25, fences.Length);
            // Early fences are highlighted; once the shared pool is
            // exhausted, later fences are single plain runs.
            Assert.True(fences[0].Inlines.Count > 1, "first fence lost its highlighting");
            Assert.Single(fences[^1].Inlines);
            int totalInlines = fences.Sum(paragraph => paragraph.Inlines.Count);
            Assert.True(
                totalInlines
                    <= 2 * SlateWindows.Reading.ReadingDocumentBuilder
                        .ProjectionHighlightTokenBudget
                        + 2 * fences.Length,
                $"aggregate inline count {totalInlines} exceeds the budget bound");
        });
    }

    /// <summary>
    /// W3-4 adversarial round 6 [medium]: a fence that renders plain
    /// for its own reasons (over the per-fence cap) must not drain the
    /// projection pool — a small ordinary fence after two over-cap
    /// ones keeps its highlighting.
    /// </summary>
    [Fact]
    public void OverCapFencesDoNotDrainTheBudgetForOthers()
    {
        RunSta(() =>
        {
            // 2,998 repetitions → 5,999 tokens per fence (2,999
            // numbers + 2,998 commas + 2 brackets): the BROKEN charge
            // order deducts both fences (12,000 → 2) and starves the
            // rust fence, while the fixed order charges neither —
            // counted by the round-7 review, which caught the first
            // fixture passing against the broken parent.
            var text = new System.Text.StringBuilder();
            for (int fence = 0; fence < 2; fence++)
            {
                text.Append("```json\n[");
                for (int i = 0; i < 2_998; i++)
                {
                    text.Append("0,");
                }
                text.Append("0]\n```\n\n");
            }
            text.Append("```rust\nfn still_colored() -> u8 { 1 }\n```\n");

            using var fixture = FixtureVault.Create(1, "reading-code-overcap");
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

            Paragraph[] fences = AllBlocks(surface.Document.Blocks)
                .OfType<Paragraph>()
                .Where(ReadingSemantics.IsCodeBlock)
                .ToArray();
            Assert.Equal(3, fences.Length);
            Assert.Single(fences[0].Inlines);
            Assert.Single(fences[1].Inlines);
            Assert.True(
                fences[2].Inlines.Count > 1,
                "the small fence lost highlighting to over-cap fences that rendered plain");
        });
    }

    private static Paragraph FindCodeParagraph(FlowDocument document) =>
        AllBlocks(document.Blocks)
            .OfType<Paragraph>()
            .Single(ReadingSemantics.IsCodeBlock);

    private static IEnumerable<Block> AllBlocks(BlockCollection blocks)
    {
        foreach (Block block in blocks)
        {
            yield return block;
            if (block is Section section)
            {
                foreach (Block inner in AllBlocks(section.Blocks))
                {
                    yield return inner;
                }
            }
        }
    }

    private static IEnumerable<System.Windows.Controls.Button> FindVisualButtons(
        FlowDocument document) =>
        AllBlocks(document.Blocks)
            .OfType<BlockUIContainer>()
            .Select(container => container.Child)
            .OfType<System.Windows.Controls.Button>();

    /// <summary>The code Copy affordance — a hyperlink since the
    /// 2026-07-30 field fix (in-range text, labelled in the caret
    /// stream).</summary>
    private static IEnumerable<Hyperlink> FindCopyLinks(FlowDocument document) =>
        AllBlocks(document.Blocks)
            .OfType<Paragraph>()
            .SelectMany(paragraph => paragraph.Inlines.OfType<Hyperlink>())
            .Where(link => ReadingSemantics.IsCodeCopy(link));

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
