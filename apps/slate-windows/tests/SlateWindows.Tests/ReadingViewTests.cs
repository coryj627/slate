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
                    ReadingLandmarkKind.List,      // task list
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
