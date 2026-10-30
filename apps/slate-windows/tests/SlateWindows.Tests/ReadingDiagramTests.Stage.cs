// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-20 (#1317): the reading view projects in two stages. Text, code, math
/// and embeds publish first, each diagram as its source plus a placeholder
/// named "Diagram, rendering" (WD11); then the diagram stage, decoded off the
/// dispatcher, completes each placeholder in place without moving the
/// reader, and says nothing.
/// </summary>
public sealed partial class ReadingDiagramTests
{
    private const string StageNote =
        "# Diagram\n\nBefore the diagram.\n\n$$\nx^2\n$$\n\n"
        + "```mermaid\nflowchart LR\nA --> B\nB --> C\n```\n\n"
        + "After the diagram.\n\nThe last paragraph here.\n";

    private const string UnsupportedNote =
        "# Diagram\n\nBefore the diagram.\n\n```mermaid\nweirdDiagram\nstuff\n```\n\n"
        + "After the diagram.\n";

    /// <summary>One note in reading mode with its diagram stage gated, and
    /// the surface bound — hosted in a window when a fact needs focus.</summary>
    private sealed class StageHarness : IDisposable
    {
        private readonly FixtureVault _fixture;
        private readonly CancelToken _cancel;
        private readonly Window? _window;
        private readonly Thread _thread = Thread.CurrentThread;

        private StageHarness(string name, string markdown, Action<ReadingContentViewModel>? configure, bool hosted)
        {
            _fixture = FixtureVault.Create(1, name);
            File.WriteAllText(Path.Combine(_fixture.Root, "note0.md"), markdown);
            Session = VaultSession.OpenFilesystem(_fixture.Root);
            _cancel = new CancelToken();
            Session.ScanInitial(_cancel);
            Tab = new WorkspaceTabViewModel(
                Session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                announce: Announced.Add,
                startInteractionBackgroundWork: false);
            Tab.ReadingCreatedForTests = model =>
            {
                model.DiagramStageGateForTests = Gate;
                model.DiagramsCompleted += (document, _) => Completed.Add(document);
                configure?.Invoke(model);
            };
            ReadingSurface.ApplyProbeForTests = () =>
            {
                if (Thread.CurrentThread == _thread)
                {
                    Applies++;
                }
            };
            Tab.ToggleViewMode();
            Surface = new ReadingSurface { Model = Tab.Reading };
            if (hosted)
            {
                _window = new Window
                {
                    Content = Surface,
                    Width = 600,
                    Height = 400,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -2000,
                    Top = -2000,
                };
                _window.Show();
                _ = _window.Activate();
                _window.UpdateLayout();
                PumpedDispatcher.Drain();
            }
        }

        public static StageHarness Open(
            string name, string markdown, Action<ReadingContentViewModel>? configure = null, bool hosted = false) =>
            new(name, markdown, configure, hosted);

        public VaultSession Session { get; }

        public WorkspaceTabViewModel Tab { get; }

        public ReadingSurface Surface { get; }

        public ManualResetEventSlim Gate { get; } = new(false);

        public List<A11yEvent> Announced { get; } = new();

        public List<FlowDocument> Completed { get; } = new();

        public int Applies { get; private set; }

        public ReadingContentViewModel Model => Tab.Reading!;

        /// <summary>The surface TextPattern every reader uses.</summary>
        public string PatternText()
        {
            var provider = (ITextProvider)UIElementAutomationPeer.CreatePeerForElement(Surface)
                .GetPattern(PatternInterface.Text);
            return provider.DocumentRange.GetText(-1);
        }

        public Paragraph DiagramParagraph() =>
            Surface.Document.Blocks.OfType<Paragraph>().Single(ReadingSemantics.IsDiagramBlock);

        public ReadingDiagramElement Placeholder() =>
            Assert.Single(FindDiagramElements(Surface.Document));

        /// <summary>Releases the gate and pumps until the stage landed.</summary>
        public void Land()
        {
            Gate.Set();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => !Model.DiagramsPendingForTests, TimeSpan.FromSeconds(30)),
                "the diagram stage never landed");
            PumpedDispatcher.Drain();
            Assert.False(Model.DiagramStageGateTimedOutForTests, "the stage's gate wait timed out");
        }

        public void Dispose()
        {
            Gate.Set();
            ReadingSurface.ApplyProbeForTests = null;
            _window?.Close();
            Tab.Dispose();
            Session.Dispose();
            _cancel.Dispose();
            _fixture.Dispose();
        }
    }

    private static int OffsetOf(TextPointer from, TextPointer to) => from.GetOffsetToPosition(to);

    private static TextPointer PositionAtText(FlowDocument document, string text)
    {
        TextPointer? navigator = document.ContentStart;
        while (navigator is not null)
        {
            if (navigator.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                string run = navigator.GetTextInRun(LogicalDirection.Forward);
                int index = run.IndexOf(text, StringComparison.Ordinal);
                if (index >= 0)
                {
                    return navigator.GetPositionAtOffset(index)!;
                }
            }
            navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
        }
        throw new InvalidOperationException($"'{text}' is not in the document");
    }

    private static string CharacterAfter(TextPointer pointer) =>
        pointer.GetTextInRun(LogicalDirection.Forward) is { Length: > 0 } run ? run[..1] : string.Empty;

    /// <summary>A paragraph's shape for the shared-shape differential: its
    /// text, the kinds of its inlines, and its marks.</summary>
    private static string ShapeOf(Paragraph paragraph) =>
        string.Join(
            "|",
            new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text,
            string.Join(",", paragraph.Inlines.Select(inline => inline.GetType().Name)),
            ReadingSemantics.IsDiagramBlock(paragraph),
            ReadingSemantics.DiagramDescriptionOf(paragraph),
            ReadingSemantics.PendingDiagramKeyOf(paragraph) ?? "-",
            // The local value: the inherited one is the containing
            // document's, and the surface's differs from the builder's.
            paragraph.ReadLocalValue(Block.TextAlignmentProperty));

    /// <summary>The diagram paragraph of a fresh single-stage build of
    /// <paramref name="markdown"/> over the vault's own artifacts.</summary>
    private static Paragraph FreshDiagramParagraph(VaultSession session, string markdown)
    {
        ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(markdown);
        ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
            markdown, Array.Empty<RenderedCitation>(), Array.Empty<OutgoingLink>());
        var model = blocks.Zip(inlines, (block, inline) => (block, inline)).ToList();
        ReadingDocumentModel built = ReadingDocumentBuilder.Build(
            model,
            new ReadingListBuildContext(),
            Array.Empty<CodeBlock>(),
            Array.Empty<MathBlock>(),
            session.GetDiagramBlocks("note0.md"));
        return built.Document.Blocks.OfType<Paragraph>().Single(ReadingSemantics.IsDiagramBlock);
    }

    [Fact]
    public void TextAndMathAreReadableWhileADiagramRenderIsParked()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-parked", StageNote);

            Assert.False(harness.Model.IsLoading);
            Assert.DoesNotContain(
                harness.Surface.Document.Blocks,
                block => AutomationProperties.GetAutomationId(block) == "ReadingLoadingNotice");
            string text = harness.PatternText();
            Assert.Contains("Before the diagram.", text, StringComparison.Ordinal);
            Assert.Contains("flowchart LR", text, StringComparison.Ordinal);
            Assert.Contains("The last paragraph here.", text, StringComparison.Ordinal);
            Assert.NotEmpty(FindElements<ReadingMathElement>(harness.Surface.Document));
            ReadingDiagramElement placeholder = harness.Placeholder();
            Assert.Equal("Diagram, rendering", AutomationProperties.GetName(placeholder));
            Assert.Null(placeholder.Content);
            Assert.True(harness.Model.DiagramsPendingForTests);
        });
    }

    [Fact]
    public void TheDiagramCompletesItsPlaceholderWithoutMovingTheReader()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-in-place", StageNote, hosted: true);
            ReadingDiagramElement placeholder = harness.Placeholder();
            int blockIndex = harness.Surface.Document.Blocks.ToList().IndexOf(harness.DiagramParagraph());
            harness.Surface.CaretPosition = PositionAtText(harness.Surface.Document, "paragraph here");
            Assert.True(harness.Surface.Focus());
            IInputElement? focused = Keyboard.FocusedElement;
            string after = CharacterAfter(harness.Surface.CaretPosition);
            int fromDiagram = OffsetOf(harness.DiagramParagraph().ElementEnd, harness.Surface.CaretPosition);
            int applies = harness.Applies;

            harness.Land();

            ReadingDiagramElement completed = harness.Placeholder();
            Assert.Same(placeholder, completed);
            Assert.NotNull(completed.Content);
            Assert.Equal(blockIndex, harness.Surface.Document.Blocks.ToList().IndexOf(harness.DiagramParagraph()));
            Assert.Equal(after, CharacterAfter(harness.Surface.CaretPosition));
            Assert.Equal(fromDiagram, OffsetOf(harness.DiagramParagraph().ElementEnd, harness.Surface.CaretPosition));
            Assert.Same(focused, Keyboard.FocusedElement);
            Assert.Equal(applies, harness.Applies);
            Assert.DoesNotContain("flowchart LR", harness.PatternText(), StringComparison.Ordinal);
            Assert.Equal(
                ShapeOf(FreshDiagramParagraph(harness.Session, StageNote)),
                ShapeOf(harness.DiagramParagraph()));
        });
    }

    [Fact]
    public void ADiagramLandingWhileTheReaderIsOnThePlaceholderKeepsFocus()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-focus", StageNote, hosted: true);
            ReadingDiagramElement placeholder = harness.Placeholder();
            Assert.True(placeholder.Focus(), "premise: the placeholder takes focus");
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(placeholder);

            harness.Land();

            Assert.True(placeholder.IsKeyboardFocused, "the placeholder lost focus");
            Assert.Equal("Flowchart with 2 steps.", AutomationProperties.GetName(placeholder));
            Assert.Same(peer, UIElementAutomationPeer.FromElement(placeholder));
            Assert.Equal("Flowchart with 2 steps.", placeholder.Description);
        });
    }

    [Fact]
    public void ADiagramLandingUnderASelectionKeepsTheSelection()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-selection", StageNote, hosted: true);
            FlowDocument document = harness.Surface.Document;
            harness.Surface.Selection.Select(
                PositionAtText(document, "Before the diagram.").GetPositionAtOffset(7)!,
                PositionAtText(document, "After the diagram.").GetPositionAtOffset(5)!);
            int start = OffsetOf(document.ContentStart, harness.Surface.Selection.Start);
            string endCharacter = CharacterAfter(harness.Surface.Selection.End);
            string selected = harness.Surface.Selection.Text;

            harness.Land();

            Assert.Equal(start, OffsetOf(document.ContentStart, harness.Surface.Selection.Start));
            Assert.Equal(endCharacter, CharacterAfter(harness.Surface.Selection.End));
            Assert.StartsWith(selected[..5], harness.Surface.Selection.Text, StringComparison.Ordinal);
            Assert.EndsWith(selected[^5..], harness.Surface.Selection.Text, StringComparison.Ordinal);
        });
    }

    /// <summary>A selection anchored at the placeholder's container — from it
    /// into the paragraph after, forward or backward — keeps its endpoints
    /// and its direction through the landing (Codoki on #1707; red-team
    /// round 1's backward-selection fix).</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASelectionAnchoredAtThePlaceholderKeepsItsEndpointsAndDirection(bool backward)
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open(
                "reading-diagram-anchored-" + (backward ? "backward" : "forward"), StageNote, hosted: true);
            FlowDocument document = harness.Surface.Document;
            var container = (InlineUIContainer)harness.Placeholder().Parent;
            TextPointer after = PositionAtText(document, "After the diagram.").GetPositionAtOffset(5)!;
            if (backward)
            {
                harness.Surface.Selection.Select(after, container.ElementStart);
            }
            else
            {
                harness.Surface.Selection.Select(container.ElementStart, after);
            }
            string endCharacter = CharacterAfter(harness.Surface.Selection.End);
            Assert.Equal(
                backward,
                harness.Surface.CaretPosition.CompareTo(harness.Surface.Selection.Start) == 0);

            harness.Land();

            Assert.Same(container, harness.Placeholder().Parent);
            Assert.Equal(0, harness.Surface.Selection.Start.CompareTo(container.ElementStart));
            Assert.Equal(endCharacter, CharacterAfter(harness.Surface.Selection.End));
            Assert.Equal(
                backward,
                harness.Surface.CaretPosition.CompareTo(harness.Surface.Selection.Start) == 0);
        });
    }

    [Fact]
    public void AnchorsInsideThePendingSourceAreRemapped()
    {
        RunSta(() =>
        {
            // (a) The caret inside the pending source, Rendered: before the diagram.
            using (var harness = StageHarness.Open("reading-diagram-remap-a", StageNote, hosted: true))
            {
                harness.Surface.CaretPosition =
                    PositionAtText(harness.Surface.Document, "flowchart LR").GetPositionAtOffset(5)!;
                harness.Land();
                var container = (InlineUIContainer)harness.Placeholder().Parent;
                Assert.Equal(0, harness.Surface.CaretPosition.CompareTo(container.ElementStart));
            }

            // (b) A selection from the paragraph before into the source,
            // Rendered: it now covers the diagram.
            using (var harness = StageHarness.Open("reading-diagram-remap-b", StageNote, hosted: true))
            {
                harness.Surface.Selection.Select(
                    PositionAtText(harness.Surface.Document, "Before the diagram."),
                    PositionAtText(harness.Surface.Document, "flowchart LR").GetPositionAtOffset(5)!);
                harness.Land();
                var container = (InlineUIContainer)harness.Placeholder().Parent;
                Assert.Equal(0, harness.Surface.Selection.End.CompareTo(container.ElementEnd));
                Assert.True(harness.Surface.Selection.Start.CompareTo(container.ElementStart) <= 0);
            }

            // (c) The caret inside an unsupported fence's source, Failed: the
            // same character of the failure shape's source run.
            using (var harness = StageHarness.Open("reading-diagram-remap-c", UnsupportedNote, hosted: true))
            {
                harness.Surface.CaretPosition =
                    PositionAtText(harness.Surface.Document, "weirdDiagram").GetPositionAtOffset(5)!;
                string after = CharacterAfter(harness.Surface.CaretPosition);
                harness.Land();
                Assert.Equal(after, CharacterAfter(harness.Surface.CaretPosition));
                Assert.Equal(
                    "weird",
                    new TextRange(
                        harness.Surface.CaretPosition.GetPositionAtOffset(-5)!,
                        harness.Surface.CaretPosition).Text);
            }
        });
    }

    [Fact]
    public void AFailedDiagramRenderLeavesTheRestReadableAndNamesTheFailure()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-failed", UnsupportedNote);
            ReadingDiagramElement placeholder = harness.Placeholder();

            harness.Land();

            string text = harness.PatternText();
            Assert.Contains("Diagram dialect not supported", text, StringComparison.Ordinal);
            Assert.Contains("weirdDiagram", text, StringComparison.Ordinal);
            Assert.Contains("Before the diagram.", text, StringComparison.Ordinal);
            Assert.Contains("After the diagram.", text, StringComparison.Ordinal);
            Assert.Same(placeholder, harness.Placeholder());
            Assert.Null(placeholder.Content);
            Assert.StartsWith("Mermaid diagram, source:", AutomationProperties.GetName(placeholder), StringComparison.Ordinal);
            Assert.Equal(
                ShapeOf(FreshDiagramParagraph(harness.Session, UnsupportedNote)),
                ShapeOf(harness.DiagramParagraph()));
        });
    }

    [Fact]
    public void ANoMatchCompletionRestoresTheUnmatchedShapeUnlessThePlaceholderHasFocus()
    {
        RunSta(() =>
        {
            // Unfocused: main's unmatched shape, and the removed element
            // leaves the surface's children.
            using (var harness = StageHarness.Open(
                "reading-diagram-nomatch-a",
                StageNote,
                model => model.DiagramFaultForTests = () => new VaultException.Io("injected"),
                hosted: true))
            {
                AutomationPeer surfacePeer = UIElementAutomationPeer.CreatePeerForElement(harness.Surface);
                AutomationPeer held = Assert.Single(
                    surfacePeer.GetChildren().OfType<ReadingDiagramElementPeer>());
                harness.Land();
                Assert.Empty(FindDiagramElements(harness.Surface.Document));
                Assert.Contains("flowchart LR", harness.PatternText(), StringComparison.Ordinal);
                Assert.Single(
                    harness.Surface.LandmarksForTests,
                    candidate => candidate.Kind == ReadingLandmarkKind.Diagram);
                Assert.DoesNotContain(held, surfacePeer.GetChildren());
                Assert.Empty(surfacePeer.GetChildren().OfType<ReadingDiagramElementPeer>());
            }

            // Focused: the placeholder stays, keeps focus, is named by the
            // source, and is the same peer.
            using (var harness = StageHarness.Open(
                "reading-diagram-nomatch-b",
                StageNote,
                model => model.DiagramFaultForTests = () => new VaultException.Io("injected"),
                hosted: true))
            {
                ReadingDiagramElement placeholder = harness.Placeholder();
                AutomationPeer surfacePeer = UIElementAutomationPeer.CreatePeerForElement(harness.Surface);
                AutomationPeer held = Assert.Single(
                    surfacePeer.GetChildren().OfType<ReadingDiagramElementPeer>());
                Assert.True(placeholder.Focus(), "premise: the placeholder takes focus");
                harness.Land();
                Assert.Same(placeholder, harness.Placeholder());
                Assert.True(placeholder.IsKeyboardFocused);
                Assert.Contains("flowchart LR", AutomationProperties.GetName(placeholder), StringComparison.Ordinal);
                Assert.Contains(held, surfacePeer.GetChildren());
            }
        });
    }

    [Fact]
    public void AnUnchangedDiagramStaysCompleteAcrossARefresh()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-unchanged", StageNote, hosted: true);
            harness.Land();
            int runs = harness.Model.DiagramStageRunsForTests;

            // An unchanged refresh memo-hits.
            int applies = harness.Applies;
            harness.Model.Refresh();
            PumpedDispatcher.Drain();
            Assert.Equal(applies, harness.Applies);
            Assert.Equal(runs, harness.Model.DiagramStageRunsForTests);

            // An edit elsewhere re-projects: the stage-1 document already
            // holds the rendered diagram, and the caret stays on its character.
            harness.Surface.CaretPosition = PositionAtText(harness.Surface.Document, "paragraph here");
            string after = CharacterAfter(harness.Surface.CaretPosition);
            harness.Tab.EditorDocument!.Insert(
                harness.Tab.EditorDocument.TextLength, "\nA paragraph added at the end.\n");
            harness.Model.Refresh();
            PumpedDispatcher.Drain();
            Assert.Equal(applies + 1, harness.Applies);
            Paragraph diagram = harness.DiagramParagraph();
            Assert.Null(ReadingSemantics.PendingDiagramKeyOf(diagram));
            Assert.NotNull(harness.Placeholder().Content);
            Assert.DoesNotContain("flowchart LR", harness.PatternText(), StringComparison.Ordinal);
            Assert.Equal(runs, harness.Model.DiagramStageRunsForTests);
            Assert.Equal(after, CharacterAfter(harness.Surface.CaretPosition));
        });
    }

    [Fact]
    public void ALateDiagramIsSilent()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-silent", StageNote);
            harness.Announced.Clear();
            harness.Land();
            Assert.Empty(harness.Announced);
        });
    }

    [Fact]
    public void ASupersededDiagramStageTouchesNothing()
    {
        RunSta(() =>
        {
            using var posted = new ManualResetEventSlim(false);
            using var harness = StageHarness.Open(
                "reading-diagram-superseded",
                StageNote,
                model => model.DiagramStagePostedForTests = posted);
            FlowDocument replaced = harness.Model.Document!;
            ReadingDiagramElement oldPlaceholder = harness.Placeholder();

            harness.Gate.Set();
            Assert.True(posted.Wait(TimeSpan.FromSeconds(10)), "the stage never posted its landing");
            posted.Reset();
            // An edit after the fence: the live fence keeps the saved
            // artifact's bytes, so the new projection's stage renders it.
            harness.Tab.EditorDocument!.Insert(
                harness.Tab.EditorDocument.TextLength, "\nA new last paragraph.\n");
            harness.Model.Refresh();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => !harness.Model.DiagramsPendingForTests, TimeSpan.FromSeconds(30)),
                "the new projection's stage never landed");
            PumpedDispatcher.Drain();

            Assert.Equal(1, harness.Model.DiagramStageLandingsDroppedForTests);
            Assert.DoesNotContain(replaced, harness.Completed);
            Assert.Contains(harness.Model.Document, harness.Completed);
            Assert.Equal("Diagram, rendering", AutomationProperties.GetName(oldPlaceholder));
            Assert.Null(oldPlaceholder.Content);
            Assert.NotNull(harness.Placeholder().Content);
        });
    }

    [Theory]
    [InlineData("memo-hit")]
    [InlineData("preserved-failure")]
    [InlineData("cancelled-rescan")]
    public void AnUnchangedRefreshWhileTheDiagramRendersLeavesItsStageToFinish(string ending)
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-kept-" + ending, StageNote);
            FlowDocument stageOne = harness.Model.Document!;
            ReadingDiagramElement placeholder = harness.Placeholder();
            int runs = harness.Model.DiagramStageRunsForTests;
            int applies = harness.Applies;

            switch (ending)
            {
                case "memo-hit":
                    harness.Model.Refresh();
                    PumpedDispatcher.Drain();
                    Assert.Equal(applies, harness.Applies);
                    Assert.False(harness.Model.RefreshInFlight);
                    break;
                case "preserved-failure":
                    harness.Model.FetchFaultForTests = () => new VaultException.Io("injected");
                    harness.Model.Refresh();
                    PumpedDispatcher.Drain();
                    Assert.True(harness.Model.LastRefreshFailed);
                    harness.Model.FetchFaultForTests = null;
                    break;
                default:
                    _ = harness.Model.ActivateForRescanAsync(new CancellationToken(canceled: true), attachObserver: false);
                    PumpedDispatcher.Drain();
                    break;
            }
            Assert.Same(stageOne, harness.Model.Document);
            Assert.Equal("Diagram, rendering", AutomationProperties.GetName(placeholder));

            harness.Announced.Clear();
            harness.Land();

            Assert.Same(placeholder, harness.Placeholder());
            Assert.Equal("Flowchart with 2 steps.", AutomationProperties.GetName(placeholder));
            Assert.NotNull(placeholder.Content);
            Assert.DoesNotContain("flowchart LR", harness.PatternText(), StringComparison.Ordinal);
            Assert.Equal(new[] { stageOne }, harness.Completed.Where(document => ReferenceEquals(document, stageOne)));
            Assert.Equal(runs, harness.Model.DiagramStageRunsForTests);
            Assert.Equal(0, harness.Model.DiagramStageLandingsDroppedForTests);
            Assert.Empty(harness.Announced);
        });
    }

    [Fact]
    public void AFailedPreferenceRefreshLeavesTheDiagramStageToLand()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-prefs", StageNote);
            FlowDocument stageOne = harness.Model.Document!;
            ReadingDiagramElement placeholder = harness.Placeholder();
            int runs = harness.Model.DiagramStageRunsForTests;

            harness.Model.FetchFaultForTests = () => new VaultException.Io("injected");
            harness.Model.InvalidateForPrefsChange();
            Assert.True(PumpedDispatcher.PumpUntil(() => !harness.Model.RefreshInFlight));
            Assert.True(harness.Model.LastRefreshFailed);
            Assert.False(harness.Model.PublishedFailureNotice);
            Assert.Same(stageOne, harness.Model.Document);
            Assert.Equal("Diagram, rendering", AutomationProperties.GetName(placeholder));
            Assert.Null(placeholder.Content);

            harness.Announced.Clear();
            harness.Land();
            Assert.Same(placeholder, harness.Placeholder());
            Assert.NotNull(placeholder.Content);
            Assert.DoesNotContain("flowchart LR", harness.PatternText(), StringComparison.Ordinal);
            Assert.Contains(stageOne, harness.Completed);
            Assert.Equal(runs, harness.Model.DiagramStageRunsForTests);
            Assert.Equal(0, harness.Model.DiagramStageLandingsDroppedForTests);
            Assert.Empty(harness.Announced);

            // The preference change dropped the memo and the landing did not
            // restore it: the next unchanged refresh rebuilds, complete.
            harness.Model.FetchFaultForTests = null;
            int applies = harness.Applies;
            harness.Model.Refresh();
            PumpedDispatcher.Drain();
            Assert.Equal(applies + 1, harness.Applies);
            Assert.Null(ReadingSemantics.PendingDiagramKeyOf(harness.DiagramParagraph()));
            Assert.NotNull(harness.Placeholder().Content);
            Assert.Equal(runs, harness.Model.DiagramStageRunsForTests);
        });
    }

    [Fact]
    public void ARebindToTheSameProjectionResumesItsPendingDiagram()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-rebind", StageNote);
            ReadingDiagramElement placeholder = harness.Placeholder();
            int runs = harness.Model.DiagramStageRunsForTests;
            ReadingContentViewModel model = harness.Model;
            harness.Surface.Model = null;
            int applies = harness.Applies;
            harness.Surface.Model = model;

            harness.Land();

            Assert.Same(placeholder, harness.Placeholder());
            Assert.NotNull(placeholder.Content);
            Assert.Equal(runs + 1, model.DiagramStageRunsForTests);
            Assert.Equal(0, model.DiagramStageLandingsDroppedForTests);
            Assert.Equal(applies, harness.Applies);
        });
    }

    [Fact]
    public void AnUnmatchedDiagramIsRetriedByTheNextRefresh()
    {
        RunSta(() =>
        {
            Func<Exception?>? fault = () => new VaultException.Io("injected");
            using var harness = StageHarness.Open(
                "reading-diagram-retry",
                StageNote,
                model => model.DiagramFaultForTests = () => fault?.Invoke());
            harness.Land();
            Assert.Empty(FindDiagramElements(harness.Surface.Document));

            fault = null;
            int applies = harness.Applies;
            harness.Model.Refresh();
            PumpedDispatcher.Drain();
            Assert.Equal(applies + 1, harness.Applies);
            Assert.True(harness.Model.DiagramsPendingForTests);
            harness.Land();
            ReadingDiagramElement element = harness.Placeholder();
            Assert.Equal("Flowchart with 2 steps.", AutomationProperties.GetName(element));
            Assert.NotNull(element.Content);
        });
    }

    /// <summary>The owner's call of 2026-10-26: a transient refusal — core's
    /// "busy" render — is shown but never landed, so the next refresh
    /// retries it.</summary>
    [Fact]
    public void ABusyDiagramRefusalIsRetriedByTheNextRefresh()
    {
        RunSta(() =>
        {
            bool busy = true;
            using var harness = StageHarness.Open(
                "reading-diagram-busy",
                StageNote,
                model => model.DiagramBlocksOverrideForTests = blocks => busy
                    ? blocks.Select(block => block with
                    {
                        Svg = null,
                        RenderStatus = new DiagramRenderStatus.RenderFailed(
                            ReadingDocumentBuilder.CoreDiagramBusyMessage),
                    }).ToArray()
                    : blocks);
            harness.Land();
            Assert.Contains(
                ReadingDocumentBuilder.CoreDiagramBusyMessage, harness.PatternText(), StringComparison.Ordinal);

            busy = false;
            int applies = harness.Applies;
            harness.Model.Refresh();
            PumpedDispatcher.Drain();
            Assert.Equal(applies + 1, harness.Applies);
            Assert.True(harness.Model.DiagramsPendingForTests);
            harness.Land();
            Assert.NotNull(harness.Placeholder().Content);
        });
    }

    /// <summary>Red-team round 1: a stage fault the degrade path does not
    /// catch — a core panic, a decoder failure — never strands a placeholder:
    /// every fence lands as its source, and the fault is recorded.</summary>
    [Fact]
    public void AStageThatThrowsLandsEveryFenceAsItsSource()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open(
                "reading-diagram-throws",
                StageNote,
                model => model.DiagramBlocksOverrideForTests =
                    _ => throw new InvalidOperationException("injected stage fault"));

            harness.Land();

            Assert.Empty(FindDiagramElements(harness.Surface.Document));
            Assert.Contains("flowchart LR", harness.PatternText(), StringComparison.Ordinal);
            Assert.IsType<InvalidOperationException>(harness.Model.LastTerminalFailureForTests);
        });
    }

    [Fact]
    public void SvgIsDecodedOffTheDispatcher()
    {
        RunSta(() =>
        {
            var threads = new List<int>();
            ReadingDocumentBuilder.DiagramRenderProbeForTests = _ => threads.Add(Environment.CurrentManagedThreadId);
            try
            {
                using var harness = StageHarness.Open("reading-diagram-thread", StageNote);
                int dispatcher = Environment.CurrentManagedThreadId;
                harness.Land();
                Assert.NotEmpty(threads);
                Assert.DoesNotContain(dispatcher, threads);
            }
            finally
            {
                ReadingDocumentBuilder.DiagramRenderProbeForTests = null;
            }
        });
    }

    private static IEnumerable<T> FindElements<T>(FlowDocument document)
        where T : UIElement
    {
        foreach (Block block in document.Blocks)
        {
            if (block is not Paragraph paragraph)
            {
                continue;
            }
            foreach (Inline inline in paragraph.Inlines)
            {
                if (inline is InlineUIContainer { Child: T element })
                {
                    yield return element;
                }
            }
        }
    }
}
