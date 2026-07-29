// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W3-5 embed cards: core-resolved content IN the document text range
/// (the W3-1 spike's BlockUIContainer say-all lesson), mac EmbedView
/// name shapes and failure strings verbatim, bounded fetch with
/// per-key degradation, and the W3-1 activation contract preserved.
/// </summary>
public sealed class ReadingEmbedTests
{
    /// <summary>A 1×1 red PNG — the smallest well-formed raster the
    /// decode path accepts.</summary>
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8"
        + "z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    /// <summary>
    /// The core card contract: a whole-paragraph ![[note]] expands the
    /// TARGET's resolved body into the text range (say-all reads it),
    /// the header carries mac's exact name shape, the landmark speaks
    /// the header, and Enter at the header activates through the
    /// existing embed seam.
    /// </summary>
    [Fact]
    public void FullNoteEmbedExpandsContentInRange()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-note");
            File.WriteAllText(
                Path.Combine(fixture.Root, "target.md"),
                "Body text the reader must hear.\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "![[target]]\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            string text = new System.Windows.Documents.TextRange(
                surface.Document.ContentStart,
                surface.Document.ContentEnd).Text;
            Assert.Contains(
                "Embedded note: target.md", text, StringComparison.Ordinal);
            Assert.Contains(
                "Body text the reader must hear.", text, StringComparison.Ordinal);

            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Embed);
            Assert.Equal("Embedded note: target.md", landmark.Text);

            surface.CaretPosition = landmark.Position;
            Assert.True(
                surface.TryActivateAtCaret(),
                "Enter at the embed header must activate the card");
        });
    }

    /// <summary>Section and block embeds use mac's exact header
    /// shapes, with the anchored slice — not the whole note — in
    /// range.</summary>
    [Fact]
    public void SectionAndBlockEmbedsUseMacHeaderShapes()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-anchors");
            File.WriteAllText(
                Path.Combine(fixture.Root, "target.md"),
                "# Heading One\n\nFirst section body.\n\n# Heading Two\n\n"
                + "Second section body.\n\nAnchored paragraph. ^blk\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "![[target#Heading Two]]\n\n![[target^blk]]\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            string text = new System.Windows.Documents.TextRange(
                surface.Document.ContentStart,
                surface.Document.ContentEnd).Text;
            Assert.Contains(
                "Embedded section: Heading Two from target.md",
                text,
                StringComparison.Ordinal);
            Assert.Contains(
                "Embedded block from target.md", text, StringComparison.Ordinal);
            Assert.Contains(
                "Second section body.", text, StringComparison.Ordinal);
            Assert.Contains("Anchored paragraph.", text, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "First section body.", text, StringComparison.Ordinal);
            Assert.Equal(
                2,
                surface.LandmarksForTests.Count(
                    candidate => candidate.Kind == ReadingLandmarkKind.Embed));
        });
    }

    /// <summary>
    /// Image embeds title alt-or-filename (mac audits #196/#198/#419)
    /// with the authored alias threading through the link record —
    /// the mac batch precedent, minus its fallback path's alt loss —
    /// and render through the hardened decode path with the image
    /// itself carrying no automation presence (the header IS the
    /// announcement, mac's accessibilityHidden parity).
    /// </summary>
    [Fact]
    public void ImageEmbedTitlesAltOrFilenameAndRendersBounded()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-image");
            Directory.CreateDirectory(Path.Combine(fixture.Root, "attachments"));
            File.WriteAllBytes(
                Path.Combine(fixture.Root, "attachments", "pic.png"), TinyPng);
            // The aliased and alias-less occurrences live in SEPARATE
            // notes deliberately: duplicates of one key inside a note
            // share the LAST record's alt (mac's documented duplicate
            // rule; per-occurrence alt deferred with rationale there).
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "![[pic.png|A tiny chart]]\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note1.md"), "![[pic.png]]\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };
            string text = new System.Windows.Documents.TextRange(
                surface.Document.ContentStart,
                surface.Document.ContentEnd).Text;
            Assert.Contains(
                "Embedded image: A tiny chart", text, StringComparison.Ordinal);

            using var second = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note1.md")),
                startInteractionBackgroundWork: false);
            second.ToggleViewMode();
            var secondSurface = new ReadingSurface { Model = second.Reading };
            Assert.Contains(
                "Embedded image: pic.png",
                new System.Windows.Documents.TextRange(
                    secondSurface.Document.ContentStart,
                    secondSurface.Document.ContentEnd).Text,
                StringComparison.Ordinal);
        });
    }

    /// <summary>Undecodable image bytes show mac's exact failure
    /// string in range instead of a blank card.</summary>
    [Fact]
    public void UndecodableImageEmbedShowsMacFailureString()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-badimage");
            File.WriteAllBytes(
                Path.Combine(fixture.Root, "broken.png"),
                new byte[] { 0x00, 0x01, 0x02, 0x03 });
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "![[broken.png]]\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            string text = new System.Windows.Documents.TextRange(
                surface.Document.ContentStart,
                surface.Document.ContentEnd).Text;
            Assert.Contains(
                "Could not decode image. MIME: image/png. "
                + "The file may be corrupt or an unsupported codec.",
                text,
                StringComparison.Ordinal);
        });
    }

    /// <summary>Unresolved embeds carry mac's exact visible string as
    /// the header and landmark, with the AX-only explanatory suffix
    /// on the Jump button's HelpText (on request, never in the
    /// reading flow).</summary>
    [Fact]
    public void UnresolvedEmbedShowsMacStringWithSuffixOnRequest()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-unresolved");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "![[missing-note]]\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Embed);
            Assert.Equal("Unresolved embed: missing-note", landmark.Text);

            System.Windows.Controls.Button jump = FindJumpButtons(surface.Document).Single();
            Assert.Equal(
                "The target note or attachment doesn't exist in this vault.",
                System.Windows.Automation.AutomationProperties.GetHelpText(jump));
        });
    }

    /// <summary>A per-key fetch failure degrades to a header-only
    /// card that still activates — never a dead block, never a failed
    /// projection.</summary>
    [Fact]
    public void EmbedFetchFailureDegradesToHeaderOnlyCard()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-degraded");
            File.WriteAllText(
                Path.Combine(fixture.Root, "target.md"), "Body.\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "![[target]]\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            tab.Reading!.EmbedFaultForTests =
                () => new VaultException.Io("embed resolution unavailable");
            var surface = new ReadingSurface { Model = tab.Reading };

            string text = new System.Windows.Documents.TextRange(
                surface.Document.ContentStart,
                surface.Document.ContentEnd).Text;
            Assert.Contains("Embedded note target", text, StringComparison.Ordinal);
            Assert.Contains(
                "Embed preview unavailable. Activate to open the source.",
                text,
                StringComparison.Ordinal);
            Assert.DoesNotContain("Body.", text, StringComparison.Ordinal);

            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Embed);
            surface.CaretPosition = landmark.Position;
            Assert.True(surface.TryActivateAtCaret());
        });
    }

    /// <summary>
    /// Nested embeds splice at their core byte offsets in mac's
    /// INITIAL (collapsed) presentation: the child's header line with
    /// its own Jump button, the child's body deliberately absent
    /// (recorded divergence: activation opens the source; no in-place
    /// expansion).
    /// </summary>
    [Fact]
    public void NestedEmbedRendersCollapsedHeaderOnly()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-nested");
            File.WriteAllText(
                Path.Combine(fixture.Root, "leaf.md"), "Leaf body content.\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "target.md"),
                "Before the nested embed.\n\n![[leaf]]\n\nAfter it.\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "![[target]]\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            string text = new System.Windows.Documents.TextRange(
                surface.Document.ContentStart,
                surface.Document.ContentEnd).Text;
            Assert.Contains(
                "Before the nested embed.", text, StringComparison.Ordinal);
            Assert.Contains(
                "Embedded note: leaf.md", text, StringComparison.Ordinal);
            Assert.Contains("After it.", text, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Leaf body content.", text, StringComparison.Ordinal);
            Assert.DoesNotContain("![[", text, StringComparison.Ordinal);

            // The nested Jump button activates the NESTED source.
            Assert.Contains(
                FindJumpButtons(surface.Document),
                button => Equals(button.Tag, "leaf"));
        });
    }

    /// <summary>The depth-limit marker and truncation notice, through
    /// the real builder over hand-built artifacts (core produces the
    /// depth shape only three levels deep — the builder must honor it
    /// wherever it appears).</summary>
    [Fact]
    public void DepthLimitAndTruncationRenderHonestNotices()
    {
        RunSta(() =>
        {
            const string markdown = "![[deep]]\n";
            ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(markdown);
            ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
                markdown,
                Array.Empty<RenderedCitation>(),
                Array.Empty<OutgoingLink>());
            var model = new List<(ReadingBlock, ReadingBlockInlines)>();
            for (int i = 0; i < blocks.Length; i++)
            {
                model.Add((blocks[i], inlines[i]));
            }
            var artifact = new ReadingEmbedArtifact(
                "deep",
                null,
                new EmbedPreviewResolution(
                    new EmbedResolution.FullNote(
                        "deep.md",
                        "Intro text.\n\n![[cycle]]",
                        new[]
                        {
                            new NestedEmbed(
                                "cycle",
                                12,
                                23,
                                new EmbedResolution.Unresolved(
                                    new EmbedUnresolvedReason.DepthLimitReached())),
                        }),
                    Truncated: true));

            ReadingDocumentModel built = ReadingDocumentBuilder.Build(
                model,
                new ReadingListBuildContext(),
                Array.Empty<CodeBlock>(),
                Array.Empty<MathBlock>(),
                Array.Empty<DiagramBlock>(),
                new[] { artifact });

            string text = new System.Windows.Documents.TextRange(
                built.Document.ContentStart,
                built.Document.ContentEnd).Text;
            Assert.Contains(
                "Unresolved embed: depth limit reached.", text, StringComparison.Ordinal);
            Assert.Contains(
                "Preview truncated. Open the source note for the full content.",
                text,
                StringComparison.Ordinal);
        });
    }

    /// <summary>Mac parity for `.canvas` embeds: canvas JSON is valid
    /// UTF-8, so the card renders as an embedded note of raw JSON —
    /// deliberately mirrored, recorded as a cross-platform follow-up
    /// rather than silently diverging.</summary>
    [Fact]
    public void CanvasEmbedRendersRawJsonAsNoteCard()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-canvas");
            File.WriteAllText(
                Path.Combine(fixture.Root, "board.canvas"),
                "{\"nodes\":[],\"edges\":[]}\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "![[board.canvas]]\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            string text = new System.Windows.Documents.TextRange(
                surface.Document.ContentStart,
                surface.Document.ContentEnd).Text;
            Assert.Contains(
                "Embedded note: board.canvas", text, StringComparison.Ordinal);
            Assert.Contains("\"nodes\"", text, StringComparison.Ordinal);
        });
    }

    private static IEnumerable<System.Windows.Controls.Button> FindJumpButtons(
        System.Windows.Documents.FlowDocument document)
    {
        foreach (System.Windows.Documents.Block block in document.Blocks)
        {
            if (block is not System.Windows.Documents.Section section)
            {
                continue;
            }
            foreach (System.Windows.Documents.Block inner in section.Blocks)
            {
                if (inner is not System.Windows.Documents.Paragraph paragraph)
                {
                    continue;
                }
                foreach (System.Windows.Documents.Inline inline in paragraph.Inlines)
                {
                    if (inline is System.Windows.Documents.InlineUIContainer
                        {
                            Child: System.Windows.Controls.Button button,
                        })
                    {
                        yield return button;
                    }
                }
            }
        }
    }

    /// <summary>WPF objects require STA; xunit runs MTA.</summary>
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
