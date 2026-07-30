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
            // A degraded card never claims a kind it cannot know
            // (round 1 [medium]): neutral label, honest body.
            Assert.Contains("Embed: target", text, StringComparison.Ordinal);
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

    /// <summary>
    /// Round 1 [high]: nested Jump controls navigate through the
    /// RESOLVED destination core handed back — the host note's record
    /// snapshot cannot contain nested targets, so the old record
    /// re-match dead-ended on content the card was displaying. This
    /// is the real click route end to end: bubbled Click through the
    /// surface router into workspace navigation.
    /// </summary>
    [Fact]
    public void NestedJumpNavigatesToTheResolvedChild()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-nestednav");
            File.WriteAllText(
                Path.Combine(fixture.Root, "leaf.md"), "Leaf body.\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "target.md"), "![[leaf]]\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "![[target]]\n");
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
            WorkspaceTabViewModel tab = workspace.ActiveGroup.ActiveTab!;
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            System.Windows.Controls.Button nested = FindJumpButtons(surface.Document)
                .Single(button => Equals(button.Tag, "leaf"));
            nested.RaiseEvent(new System.Windows.RoutedEventArgs(
                System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            Assert.Contains(
                workspace.ActiveGroup.Tabs,
                candidate => string.Equals(
                    candidate.Path, "leaf.md", StringComparison.Ordinal));
        });
    }

    /// <summary>
    /// Round 1 [high]: a TARGET-note save after publication re-projects
    /// the cards built from it (reverse-dependency filtered), an
    /// unrelated change refetches nothing, and a CREATED file resolves
    /// a previously-unresolved card.
    /// </summary>
    [Fact]
    public void TargetSavesRefreshPublishedCardsWithDependencyFiltering()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-deps");
            File.WriteAllText(
                Path.Combine(fixture.Root, "target.md"), "Original body.\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "![[target]]\n\n![[missing-note]]\n");
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
            string Text() => new System.Windows.Documents.TextRange(
                surface.Document.ContentStart,
                surface.Document.ContentEnd).Text;
            Assert.Contains("Original body.", Text(), StringComparison.Ordinal);
            Assert.Contains(
                "Unresolved embed: missing-note", Text(), StringComparison.Ordinal);

            // Target save → refresh with the new content.
            File.WriteAllText(
                Path.Combine(fixture.Root, "target.md"), "Saved body.\n");
            tab.Reading!.NotifyVaultFileChanged(FileChangeKind.Modified, "target.md");
            Assert.Contains("Saved body.", Text(), StringComparison.Ordinal);
            Assert.DoesNotContain("Original body.", Text(), StringComparison.Ordinal);

            // Unrelated change → not even a fetch (the reverse-
            // dependency filter; the counting seam fires per resolved
            // key on any embed fetch).
            int fetches = 0;
            tab.Reading!.EmbedFaultForTests = () =>
            {
                fetches++;
                return null;
            };
            tab.Reading!.NotifyVaultFileChanged(
                FileChangeKind.Modified, "unrelated.md");
            Assert.Equal(0, fetches);

            // A CREATED file resolves the unresolved card. Production
            // emits the Created event AFTER indexing (the session's
            // own write pipeline); a direct disk write needs the scan
            // first, exactly like any external edit.
            File.WriteAllText(
                Path.Combine(fixture.Root, "missing-note.md"), "Now it exists.\n");
            using var rescanCancel = new CancelToken();
            session.ScanInitial(rescanCancel);
            tab.Reading!.NotifyVaultFileChanged(
                FileChangeKind.Created, "missing-note.md");
            Assert.True(fetches > 0, "a create must reach the fetch");
            Assert.Contains(
                "Embedded note: missing-note.md", Text(), StringComparison.Ordinal);
            Assert.Contains("Now it exists.", Text(), StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Round 1 [high]: the 128-key cap counts ATTEMPTS — persistent
    /// per-key failures stop at the cap instead of making one FFI
    /// round trip for every distinct key in an adversarial note.
    /// </summary>
    [Fact]
    public void PersistentFailuresStopAtTheAttemptCap()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-attempts");
            var note = new System.Text.StringBuilder();
            for (int i = 0; i < 130; i++)
            {
                note.Append("![[t").Append(i).Append("]]\n\n");
            }
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), note.ToString());
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
            int attempts = 0;
            // Attach before the first projection: the tab creates its
            // reading model on toggle.
            tab.ToggleViewMode();
            tab.Reading!.EmbedFaultForTests = () =>
            {
                attempts++;
                return new VaultException.Io("resolution down");
            };
            var surface = new ReadingSurface { Model = tab.Reading };

            Assert.Equal(
                ReadingContentViewModel.MaximumResolvedEmbedsPerNote, attempts);
            Assert.Equal(
                130,
                surface.LandmarksForTests.Count(
                    candidate => candidate.Kind == ReadingLandmarkKind.Embed));
        });
    }

    /// <summary>
    /// Round 1 [medium]: an image whose PAYLOAD the note-wide pool
    /// refused keeps its true identity — image header, resolved Jump
    /// destination, and an honest budget notice (never the
    /// decode-failure lie, never "note").
    /// </summary>
    [Fact]
    public void ImagePoolRefusalKeepsImageIdentity()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-imagepool");
            var junk = new byte[6 * 1024 * 1024];
            File.WriteAllBytes(Path.Combine(fixture.Root, "a.png"), junk);
            File.WriteAllBytes(Path.Combine(fixture.Root, "b.png"), junk);
            File.WriteAllBytes(Path.Combine(fixture.Root, "c.png"), junk);
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "![[a.png]]\n\n![[b.png]]\n\n![[c.png]]\n");
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
            // 6 + 6 admitted; the third exceeds the 16 MiB pool.
            Assert.Contains(
                "Embedded image: c.png", text, StringComparison.Ordinal);
            Assert.Contains(
                "Image not displayed: over this note's embedded-image budget.",
                text,
                StringComparison.Ordinal);
            Assert.Single(
                FindJumpButtons(surface.Document),
                button =>
                    Equals(button.Tag, "c.png")
                    && ReadingSemantics.TryGetEmbedJump(button, out string path, out _)
                    && path == "c.png");
        });
    }

    /// <summary>
    /// Round 2 [high]: a missing-ANCHOR card names an existing target
    /// file — saving that file to add the heading must refresh the
    /// card (Modified events reach only the dependency set), and its
    /// Jump must open the file directly instead of dead-ending a
    /// nested card on the host's record snapshot.
    /// </summary>
    [Fact]
    public void MissingAnchorCardsRefreshOnTargetSaveAndJumpToTheFile()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-anchor-dep");
            File.WriteAllText(
                Path.Combine(fixture.Root, "target.md"), "# Existing\n\nBody.\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "![[target#Later]]\n");
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
            string Text() => new System.Windows.Documents.TextRange(
                surface.Document.ContentStart,
                surface.Document.ContentEnd).Text;
            Assert.Contains(
                "Unresolved embed: target.md#Later", Text(), StringComparison.Ordinal);

            // The Jump still opens the EXISTING file (top, no anchor).
            Assert.Contains(
                FindJumpButtons(surface.Document),
                button =>
                    ReadingSemantics.TryGetEmbedJump(
                        button, out string path, out var anchor)
                    && path == "target.md"
                    && anchor is null);

            // Saving the target WITH the heading refreshes the card.
            File.WriteAllText(
                Path.Combine(fixture.Root, "target.md"),
                "# Existing\n\nBody.\n\n# Later\n\nAnchored body.\n");
            using var rescanCancel = new CancelToken();
            session.ScanInitial(rescanCancel);
            tab.Reading!.NotifyVaultFileChanged(FileChangeKind.Modified, "target.md");
            Assert.Contains(
                "Embedded section: Later from target.md",
                Text(),
                StringComparison.Ordinal);
            Assert.Contains("Anchored body.", Text(), StringComparison.Ordinal);
        });
    }

    /// <summary>Round 2 [high], the nested shape: a nested
    /// ![[leaf#missing]] card's Jump opens leaf.md through the
    /// resolved path — the host snapshot has no record for it.</summary>
    [Fact]
    public void NestedMissingAnchorJumpOpensTheExistingFile()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-anchor-nested");
            File.WriteAllText(
                Path.Combine(fixture.Root, "leaf.md"), "Leaf body.\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "target.md"), "![[leaf#missing]]\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "![[target]]\n");
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
            WorkspaceTabViewModel tab = workspace.ActiveGroup.ActiveTab!;
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            System.Windows.Controls.Button nested = FindJumpButtons(surface.Document)
                .Single(button => Equals(button.Tag, "leaf#missing"));
            nested.RaiseEvent(new System.Windows.RoutedEventArgs(
                System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            Assert.Contains(
                workspace.ActiveGroup.Tabs,
                candidate => string.Equals(
                    candidate.Path, "leaf.md", StringComparison.Ordinal));
        });
    }

    /// <summary>
    /// Round 2 [medium]: a self-embed depends on THIS note's saved
    /// file — the resolver reads the disk, not the buffer — so the
    /// note's own save event must refresh it (the old same-path guard
    /// dropped the only refresh; split-pane peer saves are exactly
    /// this shape).
    /// </summary>
    [Fact]
    public void SelfEmbedRefreshesOnOwnFileSave()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-self");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "![[note0#Section]]\n\n# Section\n\nSelf body one.\n");
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
            string Text() => new System.Windows.Documents.TextRange(
                surface.Document.ContentStart,
                surface.Document.ContentEnd).Text;
            Assert.Contains("Self body one.", Text(), StringComparison.Ordinal);

            // A peer save changes the FILE (same length, so heading
            // offsets stay valid without a rescan).
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "![[note0#Section]]\n\n# Section\n\nSelf body two.\n");
            tab.Reading!.NotifyVaultFileChanged(FileChangeKind.Modified, "note0.md");
            Assert.Contains(
                "Embedded section: Section from note0.md",
                Text(),
                StringComparison.Ordinal);
            Assert.Contains("Self body two.", Text(), StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Round 3 [high]: a nested missing-anchor target MOVED to a new
    /// path — parent text unchanged — must rebuild (content-complete
    /// unresolved digest), retarget its Jump, and track the new path
    /// for later saves; a type-only digest memo-hit into stale
    /// metadata forever.
    /// </summary>
    [Fact]
    public void NestedTargetMovesRetargetJumpAndDependencies()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-retarget");
            Directory.CreateDirectory(Path.Combine(fixture.Root, "a"));
            File.WriteAllText(
                Path.Combine(fixture.Root, "a", "leaf.md"), "Leaf body.\n");
            File.WriteAllText(
                Path.Combine(fixture.Root, "target.md"), "![[leaf#missing]]\n");
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
            string Text() => new System.Windows.Documents.TextRange(
                surface.Document.ContentStart,
                surface.Document.ContentEnd).Text;
            Assert.Contains(
                "Unresolved embed: a/leaf.md#missing", Text(), StringComparison.Ordinal);

            // Move the target; the parent's text is untouched.
            Directory.CreateDirectory(Path.Combine(fixture.Root, "b"));
            File.Move(
                Path.Combine(fixture.Root, "a", "leaf.md"),
                Path.Combine(fixture.Root, "b", "leaf.md"));
            using var rescanCancel = new CancelToken();
            session.ScanInitial(rescanCancel);
            tab.Reading!.NotifyVaultFileChanged(FileChangeKind.Renamed, "b/leaf.md");

            Assert.Contains(
                "Unresolved embed: b/leaf.md#missing", Text(), StringComparison.Ordinal);
            Assert.Contains(
                FindJumpButtons(surface.Document),
                button =>
                    ReadingSemantics.TryGetEmbedJump(button, out string path, out _)
                    && path == "b/leaf.md");

            // The NEW path is now a tracked dependency: adding the
            // heading there resolves the nested card on save.
            File.WriteAllText(
                Path.Combine(fixture.Root, "b", "leaf.md"),
                "# missing\n\nLeaf body.\n");
            using var secondRescan = new CancelToken();
            session.ScanInitial(secondRescan);
            tab.Reading!.NotifyVaultFileChanged(FileChangeKind.Modified, "b/leaf.md");
            Assert.Contains(
                "Embedded section: missing from b/leaf.md",
                Text(),
                StringComparison.Ordinal);
        });
    }

    /// <summary>Round 4 [high]: a nested image's identity survives
    /// core's payload elision — the header-only child card titles
    /// correctly with zero bytes marshalled (the byte-level pin lives
    /// in core: reading_card_elides_nested_images_and_honors_the_root_pool).</summary>
    [Fact]
    public void NestedImageCardTitlesWithoutMarshalledBytes()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-nested-image");
            File.WriteAllBytes(
                Path.Combine(fixture.Root, "pic.png"), TinyPng);
            File.WriteAllText(
                Path.Combine(fixture.Root, "target.md"),
                "Around ![[pic.png]] the image.\n");
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
                "Embedded image: pic.png", text, StringComparison.Ordinal);
            Assert.Contains("Around", text, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Round 5 [high]: duplicate occurrences of one image key share
    /// ONE decoded surface per projection — encoded-byte accounting
    /// charged once while every occurrence decoded independently
    /// (2,000 occurrences of one small PNG approached 9 GiB of
    /// surfaces).
    /// </summary>
    [Fact]
    public void DuplicateImageOccurrencesShareOneDecodedSurface()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-dup-image");
            File.WriteAllBytes(Path.Combine(fixture.Root, "pic.png"), TinyPng);
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "![[pic.png]]\n\n![[pic.png]]\n\n![[pic.png]]\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var decodes = new List<string>();
            ReadingDocumentBuilder.EmbedImageDecodeProbeForTests = decodes.Add;
            try
            {
                using var tab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(
                            WorkspaceItemKind.Markdown, "note0.md")),
                    startInteractionBackgroundWork: false);
                tab.ToggleViewMode();
                var surface = new ReadingSurface { Model = tab.Reading };

                Assert.Equal(
                    3,
                    surface.LandmarksForTests.Count(
                        candidate => candidate.Kind == ReadingLandmarkKind.Embed));
                // This harness projects twice (mode toggle + surface
                // rebind); the pin is one decode PER PROJECTION,
                // never one per occurrence.
                Assert.InRange(decodes.Count, 1, 2);
            }
            finally
            {
                ReadingDocumentBuilder.EmbedImageDecodeProbeForTests = null;
            }
        });
    }

    /// <summary>Round 5 [high]: the projection-wide decoded-pixel
    /// pool degrades images BEFORE allocation once drained — later
    /// distinct images keep header and destination with the honest
    /// budget notice.</summary>
    [Fact]
    public void DecodedPixelPoolBoundsDistinctImages()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-pixelpool");
            File.WriteAllBytes(Path.Combine(fixture.Root, "one.png"), TinyPng);
            File.WriteAllBytes(Path.Combine(fixture.Root, "two.png"), TinyPng);
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "![[one.png]]\n\n![[two.png]]\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var decodes = new List<string>();
            ReadingDocumentBuilder.EmbedImageDecodeProbeForTests = decodes.Add;
            // The 1×1 fixture costs one pixel; a one-pixel pool admits
            // exactly the first image.
            ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests = 1;
            try
            {
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
                    "Embedded image: two.png", text, StringComparison.Ordinal);
                Assert.Contains(
                    "Image not displayed: over this note's embedded-image budget.",
                    text,
                    StringComparison.Ordinal);
                // The drained pool refuses BEFORE the decoder runs:
                // per projection, only the admitted image decodes.
                Assert.DoesNotContain("two.png", decodes);
            }
            finally
            {
                ReadingDocumentBuilder.EmbedImageDecodeProbeForTests = null;
                ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests =
                    null;
            }
        });
    }

    /// <summary>A 2×2 red PNG — big enough to overshoot a 1-pixel
    /// pool WITHOUT the pool being pre-drained (the non-exact
    /// exhaustion path).</summary>
    private static readonly byte[] TwoByTwoPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEElEQVR4nGP4"
        + "z8AARAwQCgAf7gP9i18U1AAAAABJRU5ErkJggg==");

    /// <summary>
    /// Round 6 [high]: the exhaustion-DISCOVERING decode is the
    /// projection's last — the pool drains and the refusal memoizes,
    /// so repeating the over-budget key (or adding more images)
    /// never decodes again; an undrained pool re-decoded the same
    /// over-budget key at every one of up to 2,000 occurrences.
    /// </summary>
    [Fact]
    public void ExhaustionDiscoveryDrainsThePoolAndMemoizesTheRefusal()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-embed-drain");
            File.WriteAllBytes(Path.Combine(fixture.Root, "big.png"), TwoByTwoPng);
            File.WriteAllBytes(Path.Combine(fixture.Root, "tiny.png"), TinyPng);
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "![[big.png]]\n\n![[big.png]]\n\n![[tiny.png]]\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var decodes = new List<string>();
            ReadingDocumentBuilder.EmbedImageDecodeProbeForTests = decodes.Add;
            // One pixel remaining: the 2×2 (4 px) decode DISCOVERS
            // exhaustion rather than hitting a pre-drained pool.
            ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests = 1;
            try
            {
                using var tab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(
                            WorkspaceItemKind.Markdown, "note0.md")),
                    startInteractionBackgroundWork: false);
                tab.ToggleViewMode();
                var surface = new ReadingSurface { Model = tab.Reading };

                // Only the discovering decode reaches the decoder —
                // per projection (this harness projects twice), and
                // never for the repeat or the follow-on tiny image.
                Assert.InRange(decodes.Count, 1, 2);
                Assert.All(decodes, key => Assert.Equal("big.png", key));

                string text = new System.Windows.Documents.TextRange(
                    surface.Document.ContentStart,
                    surface.Document.ContentEnd).Text;
                Assert.Contains(
                    "Image not displayed: over this note's embedded-image budget.",
                    text,
                    StringComparison.Ordinal);
                Assert.Contains(
                    "Embedded image: tiny.png", text, StringComparison.Ordinal);
            }
            finally
            {
                ReadingDocumentBuilder.EmbedImageDecodeProbeForTests = null;
                ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests =
                    null;
            }
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
