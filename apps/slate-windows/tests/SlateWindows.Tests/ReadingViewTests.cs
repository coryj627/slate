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

            // The caret starts ON the first heading's line, and quick-nav
            // convention (NVDA/JAWS alike) is that "next heading" from a
            // heading goes to the FOLLOWING one — the strict
            // caret-before-landmark comparison encodes exactly that.
            navigator.Move(ReadingLandmarkKind.Heading, forward: true);
            Assert.Equal(0, surface.CaretPosition.CompareTo(model.Landmarks[5].Position));
            navigator.Move(ReadingLandmarkKind.Heading, forward: true);
            A11yEvent miss = Assert.Single(announced);
            string missText = SlateUniffiMethods.A11yRender(miss).Text;
            Assert.Equal("No next heading.", missText);

            // Level-targeted: 1 goes back-to-start miss-free from here?
            // No — level 1 is BEHIND the caret; forward must miss.
            announced.Clear();
            navigator.MoveToHeadingLevel(1, forward: true);
            Assert.Equal(
                "No next level 1 heading.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);

            // And backward reaches it.
            announced.Clear();
            navigator.MoveToHeadingLevel(1, forward: false);
            Assert.Empty(announced);
            Assert.Equal(0, surface.CaretPosition.CompareTo(model.Landmarks[0].Position));

            // Links: forward from the top hits the first link.
            navigator.Move(ReadingLandmarkKind.Link, forward: true);
            Assert.Equal(0, surface.CaretPosition.CompareTo(model.Landmarks[1].Position));

            // A kind with one instance: table forward, then miss.
            navigator.Move(ReadingLandmarkKind.Table, forward: true);
            announced.Clear();
            navigator.Move(ReadingLandmarkKind.Table, forward: true);
            Assert.Equal(
                "No next table.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);
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
            Assert.NotEmpty(reading.Landmarks);

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
