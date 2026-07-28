// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W3-3 diagram blocks: the canonical SVG artifact rendered through
/// the hardened W2-3 Svg.Skia path with the core structured
/// description as the entire AT surface (mac contract), source-visible
/// fallbacks for every failure shape, and bounded host decode work.
/// </summary>
public sealed class ReadingDiagramTests
{
    /// <summary>
    /// The core rendering path over a REAL vault: a mermaid fence
    /// matches its canonical artifact, renders into a focusable
    /// element whose Name is the structured description VERBATIM
    /// (never composed — mac contract), carries the source in
    /// HelpText, and lands in the landmark index with the
    /// trailing-period-stripped description as landing text.
    /// </summary>
    [Fact]
    public void DiagramRendersAFocusableElementSpeakingTheDescription()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-diagram-basic");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "# Diagram\n\n```mermaid\nflowchart LR\nA --> B\nB --> C\n```\n");
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

            ReadingDiagramElement element =
                Assert.Single(FindDiagramElements(surface.Document));
            Assert.True(element.Focusable);
            Assert.Equal(
                "Flowchart with 2 steps.",
                System.Windows.Automation.AutomationProperties.GetName(element));
            Assert.Contains(
                "flowchart LR",
                System.Windows.Automation.AutomationProperties.GetHelpText(element),
                StringComparison.Ordinal);
            Assert.NotNull(element.Content);

            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Diagram);
            Assert.Equal("Flowchart with 2 steps", landmark.Text);
        });
    }

    /// <summary>
    /// The unsupported-input fallback carries mac's exact header, the
    /// reason, and the full source IN the text range — and the
    /// zero-size element keeps the (bounded) description Tab-reachable.
    /// </summary>
    [Fact]
    public void UnsupportedDiagramDegradesToSourceInRangeWithHonestHeader()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-diagram-unsupported");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "```mermaid\nweirdDiagram\nstuff\n```\n");
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
                "Diagram dialect not supported", text, StringComparison.Ordinal);
            Assert.Contains("weirdDiagram", text, StringComparison.Ordinal);

            ReadingDiagramElement element =
                Assert.Single(FindDiagramElements(surface.Document));
            Assert.Null(element.Content);
            Assert.StartsWith(
                "Mermaid diagram, source:",
                System.Windows.Automation.AutomationProperties.GetName(element));
            Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Diagram);
        });
    }

    /// <summary>
    /// Live-source coherence (the W3-4/W3-2 lesson): a same-position
    /// unsaved edit keeps byte containment, and showing the OLD
    /// diagram — or speaking its old description — would be a lie.
    /// The stale case degrades to the live source in range with no
    /// element (mac's unmatched fallback: never a fabricated status).
    /// </summary>
    [Fact]
    public void UnsavedDiagramEditsNeverShowTheStaleDiagram()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-diagram-stale");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "```mermaid\nflowchart LR\nA --> B\n```\n");
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
            Assert.Single(FindDiagramElements(surface.Document));

            // Same-length, same-position unsaved edit inside the fence.
            tab.EditorDocument!.Replace(
                tab.Text.IndexOf("A --> B", StringComparison.Ordinal), 7, "A --> Z");
            tab.Reading!.Refresh();

            Assert.Empty(FindDiagramElements(surface.Document));
            string text = new System.Windows.Documents.TextRange(
                surface.Document.ContentStart,
                surface.Document.ContentEnd).Text;
            Assert.Contains("A --> Z", text, StringComparison.Ordinal);
            Assert.DoesNotContain("A --> B", text, StringComparison.Ordinal);
            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Diagram);
            Assert.Contains("A --> Z", landmark.Text, StringComparison.Ordinal);
        });
    }

    /// <summary>Enter at the caret on a diagram block re-reads the
    /// canonical description through the landed vocabulary —
    /// "{description}, diagram." — and Ctrl+Enter answers identically
    /// (diagrams have no braille-analog artifact to read instead).</summary>
    [Fact]
    public void EnterAtTheCaretSpeaksTheDiagramBlock()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-diagram-enter");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "```mermaid\nflowchart LR\nA --> B\nB --> C\nC --> D\n```\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var announced = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                announce: announced.Add,
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Diagram);
            surface.CaretPosition = landmark.Position;
            Assert.True(surface.TryActivateAtCaret());
            Assert.Equal(
                "Flowchart with 3 steps, diagram.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);

            announced.Clear();
            Assert.True(surface.TryActivateAtCaret(brailleRequested: true));
            Assert.Equal(
                "Flowchart with 3 steps, diagram.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);
        });
    }

    /// <summary>
    /// A diagram-fetch failure degrades to source-in-range fallbacks
    /// with navigation intact — never a failed projection (the
    /// per-artifact-type degradation contract).
    /// </summary>
    [Fact]
    public void DiagramFetchFailureDegradesToSourceInRange()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-diagram-degraded");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "```mermaid\nflowchart LR\nA --> B\n```\n");
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
            tab.Reading!.DiagramFaultForTests =
                () => new VaultException.Io("diagrams unavailable");
            var surface = new ReadingSurface { Model = tab.Reading };

            Assert.Empty(FindDiagramElements(surface.Document));
            string text = new System.Windows.Documents.TextRange(
                surface.Document.ContentStart,
                surface.Document.ContentEnd).Text;
            Assert.Contains("flowchart LR", text, StringComparison.Ordinal);
            Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Diagram);
        });
    }

    /// <summary>
    /// The projection-wide decode pool bounds AGGREGATE Svg.Skia work
    /// across a chunk of rendered diagrams — decoder entries stop when
    /// the pool drains, and every diagram past that point keeps its
    /// full description (Name) with the honest budget fallback in
    /// range. Override seam keeps the fixture cheap; the production
    /// pool relationship is pinned alongside.
    /// </summary>
    [Fact]
    public void ProjectionPoolBoundsAggregateDiagramDecoding()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-diagram-pool");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                string.Concat(
                    Enumerable.Repeat("```mermaid\nflowchart LR\nA --> B\n```\n\n", 5)));
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            DiagramBlock[] artifacts = session.GetDiagramBlocks("note0.md");
            Assert.Equal(5, artifacts.Length);
            int svgLength = artifacts[0].Svg!.Length;
            Assert.True(svgLength > 0);

            var decoderEntered = new List<int>();
            ReadingDocumentBuilder.DiagramRenderProbeForTests = decoderEntered.Add;
            // A pool sized for exactly two of the five identical SVGs.
            ReadingDocumentBuilder.ProjectionDiagramRenderByteBudgetOverrideForTests =
                (svgLength * 2) + (svgLength / 2);
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

                // This harness projects twice (mode toggle, then the
                // surface-bind re-projection); the pool is
                // per-projection by design.
                Assert.InRange(decoderEntered.Count, 2, 4);
                List<ReadingDiagramElement> elements =
                    FindDiagramElements(surface.Document).ToList();
                Assert.Equal(5, elements.Count);
                Assert.Equal(2, elements.Count(element => element.Content is not null));
                Assert.All(elements, element => Assert.Equal(
                    "Flowchart with 1 step.",
                    System.Windows.Automation.AutomationProperties.GetName(element)));
                // The production pool: four maximal diagrams.
                Assert.Equal(
                    4 * ReadingDocumentBuilder.MaximumRenderedDiagramSvgBytes,
                    ReadingDocumentBuilder.ProjectionDiagramRenderByteBudget);
            }
            finally
            {
                ReadingDocumentBuilder.DiagramRenderProbeForTests = null;
                ReadingDocumentBuilder.ProjectionDiagramRenderByteBudgetOverrideForTests =
                    null;
            }
        });
    }

    /// <summary>
    /// Mac audit #254 L1 analog: an artifact with Ok status but empty
    /// or undecodable SVG bytes routes to the decode-failure fallback
    /// — honest header and reason, description intact — instead of a
    /// blank visual. Exercised through the real builder with a
    /// hand-built artifact (core cannot produce this shape today,
    /// which is exactly why the host must not assume it never will).
    /// </summary>
    [Fact]
    public void OkStatusWithUndecodableSvgFallsBackHonestly()
    {
        RunSta(() =>
        {
            const string source = "flowchart LR\nA --> B\n";
            const string markdown = "```mermaid\n" + source + "```\n";
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
            var artifact = new DiagramBlock(
                Source: source,
                Dialect: DiagramDialect.Mermaid,
                Svg: Array.Empty<byte>(),
                PngFallback: null,
                StructuredDescription: "Flowchart with 1 step.",
                RenderStatus: new DiagramRenderStatus.Ok(),
                Line: 1,
                ByteOffset: (uint)markdown.IndexOf("```", StringComparison.Ordinal));

            ReadingDocumentModel built = ReadingDocumentBuilder.Build(
                model,
                new ReadingListBuildContext(),
                Array.Empty<CodeBlock>(),
                Array.Empty<MathBlock>(),
                new[] { artifact });

            string text = new System.Windows.Documents.TextRange(
                built.Document.ContentStart,
                built.Document.ContentEnd).Text;
            Assert.Contains(
                "Diagram could not be rendered", text, StringComparison.Ordinal);
            Assert.Contains(
                "diagram rendered but image could not be decoded",
                text,
                StringComparison.Ordinal);
            ReadingDiagramElement element =
                Assert.Single(FindDiagramElements(built.Document));
            Assert.Null(element.Content);
            Assert.Equal(
                "Flowchart with 1 step.",
                System.Windows.Automation.AutomationProperties.GetName(element));
        });
    }

    /// <summary>The mac tooltip rule verbatim: three lines, spaces,
    /// 120-char cap with ellipsis; empty source yields no tooltip.</summary>
    [Fact]
    public void TooltipPreviewFollowsTheMacRule()
    {
        Assert.Equal(
            "flowchart LR A --> B",
            ReadingDiagramElement.TooltipPreview("flowchart LR\nA --> B"));
        Assert.Equal(string.Empty, ReadingDiagramElement.TooltipPreview("   "));
        string longSource = string.Join(
            '\n', "flowchart LR", new string('a', 200), new string('b', 200));
        string preview = ReadingDiagramElement.TooltipPreview(longSource);
        Assert.Equal(121, preview.Length);
        Assert.EndsWith("…", preview);
    }

    private static IEnumerable<ReadingDiagramElement> FindDiagramElements(
        System.Windows.Documents.FlowDocument document)
    {
        foreach (System.Windows.Documents.Block block in document.Blocks)
        {
            if (block is not System.Windows.Documents.Paragraph paragraph)
            {
                continue;
            }
            foreach (System.Windows.Documents.Inline inline in paragraph.Inlines)
            {
                if (inline is System.Windows.Documents.InlineUIContainer
                    {
                        Child: ReadingDiagramElement element,
                    })
                {
                    yield return element;
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
