// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1708 (plan 47 CB-20, CD12): NVDA reads the reading view line by line as
/// the caret's Line through the surface's Text pattern, and an embedded
/// object there reads as one space and its role. So a rendered diagram's line
/// holds its description as a hidden run before the element, sharing the
/// element's line; a pending diagram's name stands on a line of its own above
/// its source (the owner's answer to Call 1: "Before the source"); a failed
/// diagram reads as before (CD12(a)). Find skips the hidden text, and a copy
/// carries the fenced source or what is shown (CD12(c)).
/// </summary>
public sealed partial class ReadingDiagramTests
{
    private static readonly string WideFlowchartNote =
        "# Diagram\n\nBefore the diagram.\n\n```mermaid\nflowchart LR\n"
        + string.Concat(Enumerable.Range(0, 30).Select(step => $"N{step} --> N{step + 1}\n"))
        + "```\n\nAfter the diagram.\n";

    private const string StageFence = "```mermaid\nflowchart LR\nA --> B\nB --> C\n```";

    /// <summary>The caret's Line at <paramref name="at"/>, read as NVDA reads
    /// it: the surface's own Text pattern, its selection expanded to a Line.</summary>
    private static string LineAt(ReadingSurface surface, TextPointer at)
    {
        surface.Selection.Select(at, at);
        surface.CaretPosition = at;
        PumpedDispatcher.Drain();
        var provider = (ITextProvider)UIElementAutomationPeer.CreatePeerForElement(surface)
            .GetPattern(PatternInterface.Text);
        ITextRangeProvider line = provider.GetSelection()[0];
        line.ExpandToEnclosingUnit(TextUnit.Line);
        return line.GetText(-1);
    }

    /// <summary>A Line's words: the object's space, U+FFFC and the break
    /// trimmed.</summary>
    private static string Words(string line) => line.Trim(' ', '\uFFFC', '\r', '\n');

    private static void Widen(ReadingSurface surface, double width)
    {
        Window window = Window.GetWindow(surface)!;
        window.Width = width;
        window.UpdateLayout();
        PumpedDispatcher.Drain();
    }

    private static InlineUIContainer ContainerOf(ReadingDiagramElement element) =>
        (InlineUIContainer)element.Parent;

    private static IEnumerable<Run> HiddenRuns(Paragraph paragraph) =>
        paragraph.Inlines.OfType<Run>().Where(ReadingSemantics.IsHiddenSpeech);

    private static string CopyOf(ReadingSurface surface, TextPointer start, TextPointer end) =>
        surface.CopyTextOf(start, end) ?? new TextRange(start, end).Text;

    [Theory]
    [InlineData(600)]
    [InlineData(420)]
    public void LineReadingOfARenderedDiagramIsItsDescription(double width)
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open($"reading-diagram-line-rendered-{width}", StageNote, hosted: true);
            harness.Land();
            Widen(harness.Surface, width);
            ReadingDiagramElement element = harness.Placeholder();
            string name = AutomationProperties.GetName(element);
            Assert.Equal("Flowchart with 2 steps.", name);

            Assert.Equal(name, Words(LineAt(harness.Surface, ContainerOf(element).ElementStart)));
            Assert.Equal(name, Words(LineAt(harness.Surface, harness.DiagramParagraph().ContentStart)));
            string range = new TextRange(harness.DiagramParagraph().ContentStart, harness.DiagramParagraph().ContentEnd).Text;
            Assert.Equal(1, range.Split(name).Length - 1);
        });
    }

    [Theory]
    [InlineData(600)]
    [InlineData(420)]
    [InlineData(320)]
    public void ARenderedDiagramSharesItsLineWithItsDescription(double width)
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open($"reading-diagram-line-wide-{width}", WideFlowchartNote, hosted: true);
            harness.Land();
            Widen(harness.Surface, width);
            Paragraph paragraph = harness.DiagramParagraph();
            Run run = Assert.Single(HiddenRuns(paragraph));
            InlineUIContainer container = ContainerOf(harness.Placeholder());
            Assert.True(harness.Placeholder().ActualWidth > 0, "premise: the diagram was not drawn");
            Assert.Equal(
                0,
                run.ContentStart.GetLineStartPosition(0)!.CompareTo(container.ElementStart.GetLineStartPosition(0)));
            Assert.True(
                paragraph.ContentStart.GetLineStartPosition(1) is not { } next || next.Paragraph != paragraph,
                "the diagram's paragraph runs to a second line");
            Assert.Equal("Flowchart with 30 steps.", Words(LineAt(harness.Surface, container.ElementStart)));
        });
    }

    /// <summary>The owner's answer to CB-20's Call 1: "Before the source".</summary>
    [Fact]
    public void LineReadingOfAPendingDiagramReadsItsNameBeforeItsSource()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-line-pending", StageNote, hosted: true);
            Paragraph paragraph = harness.DiagramParagraph();
            InlineUIContainer container = ContainerOf(harness.Placeholder());

            Assert.Equal("Diagram, rendering", Words(LineAt(harness.Surface, container.ElementStart)));
            Assert.Equal("Diagram, rendering", Words(LineAt(harness.Surface, paragraph.ContentStart)));
            var read = new List<string>();
            harness.Surface.Focus();
            harness.Surface.CaretPosition = paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
            for (int press = 0; press < 4; press++)
            {
                read.Add(Words(LineAt(harness.Surface, harness.Surface.CaretPosition)));
                EditingCommands.MoveDownByLine.Execute(null, harness.Surface);
                PumpedDispatcher.Drain();
            }
            Assert.Equal(["Diagram, rendering", "flowchart LR", "A --> B", "B --> C"], read);
        });
    }

    [Fact]
    public void ALandedDiagramSwapsThePlaceholderNameForItsDescription()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-line-swap", StageNote, hosted: true);
            Assert.Contains("Diagram, rendering", harness.PatternText(), StringComparison.Ordinal);

            harness.Land();

            string text = harness.PatternText();
            Assert.DoesNotContain("Diagram, rendering", text, StringComparison.Ordinal);
            Assert.Equal(1, text.Split("Flowchart with 2 steps.").Length - 1);
            Assert.Equal(
                ShapeOf(FreshDiagramParagraph(harness.Session, StageNote)),
                ShapeOf(harness.DiagramParagraph()));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ANoMatchCompletionLeavesNoPlaceholderNameInRange(bool focused)
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open(
                "reading-diagram-line-nomatch-" + (focused ? "focused" : "unfocused"),
                StageNote,
                model => model.DiagramFaultForTests = () => new uniffi.slate_uniffi.VaultException.Io("injected"),
                hosted: true);
            if (focused)
            {
                Assert.True(harness.Placeholder().Focus(), "premise: the placeholder takes focus");
            }

            harness.Land();

            Paragraph paragraph = harness.DiagramParagraph();
            Assert.Empty(HiddenRuns(paragraph));
            Assert.Empty(paragraph.Inlines.OfType<LineBreak>());
            Assert.DoesNotContain("Diagram, rendering", harness.PatternText(), StringComparison.Ordinal);
            Assert.Contains("flowchart LR", harness.PatternText(), StringComparison.Ordinal);
            Assert.Equal(focused, FindDiagramElements(harness.Surface.Document).Any());
            if (focused)
            {
                Assert.True(harness.Placeholder().IsKeyboardFocused, "the placeholder lost the keys");
            }
        });
    }

    /// <summary>CD12(a): a failed diagram's description is not read in line
    /// reading; its header, reason and source already are.</summary>
    [Fact]
    public void AFailedDiagramsLineCarriesNoHiddenText()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-line-failed", UnsupportedNote, hosted: true);
            harness.Land();
            Paragraph paragraph = harness.DiagramParagraph();
            Assert.Empty(HiddenRuns(paragraph));
            Assert.Null(ReadingSemantics.CopyTextOf(paragraph));
            Assert.Equal("stuff", Words(LineAt(harness.Surface, ContainerOf(harness.Placeholder()).ElementStart)));
        });
    }

    [Theory]
    [InlineData("rendered", "Flowchart")]
    [InlineData("rendered", "steps")]
    [InlineData("pending", "rendering")]
    public void FindSkipsADiagramsHiddenText(string state, string word)
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open($"reading-diagram-find-{state}-{word}", StageNote, hosted: true);
            if (state == "rendered")
            {
                harness.Land();
            }
            Assert.Contains(word, new TextRange(harness.Surface.Document.ContentStart, harness.Surface.Document.ContentEnd).Text, StringComparison.OrdinalIgnoreCase);

            string index = ReadingTextIndex.Build(harness.Surface.Document).Text.Text;

            Assert.DoesNotContain(word, index, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("After the diagram.", index, StringComparison.Ordinal);
            if (state == "pending")
            {
                Assert.Contains("A --> B", index, StringComparison.Ordinal);
            }
        });
    }

    private static readonly IReadOnlyDictionary<string, byte[]> EmbedImages = new Dictionary<string, byte[]>
    {
        ["wide.svg"] = System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns='http://www.w3.org/2000/svg' width='900' height='200'><rect width='900' height='200' fill='teal'/></svg>"),
    };

    private const string EmbedNote = "Before the card.\n\n![[wide.svg]]\n\nAfter the card.\n";

    private static ReadingDiagramImage AwaitEmbedImage(StageHarness harness)
    {
        ReadingDiagramImage? image = null;
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => (image = harness.Surface.Document.Blocks.OfType<Section>()
                    .SelectMany(section => section.Blocks.OfType<Paragraph>())
                    .SelectMany(paragraph => paragraph.Inlines.OfType<InlineUIContainer>())
                    .Select(container => container.Child)
                    .OfType<ReadingDiagramImage>()
                    .FirstOrDefault(candidate => candidate.Source is not null)) is not null,
                UnhurriedSvgDecodes.LandingWait(1)),
            "the embedded image never showed in its paragraph");
        return image!;
    }

    /// <summary>The owner's answer to CB-20's Call 4, "Fix it in CB-20": an
    /// embedded image's line reads its card's name, at any width, since the
    /// image leaves the name's run room on their line.</summary>
    [Theory]
    [InlineData(600)]
    [InlineData(320)]
    public void AnEmbeddedImagesLineReadsItsName(double width)
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open($"reading-embed-image-line-{width}", EmbedNote, hosted: true, files: EmbedImages);
            ReadingDiagramImage image = AwaitEmbedImage(harness);
            Widen(harness.Surface, width);
            var container = (InlineUIContainer)image.Parent;
            var paragraph = (Paragraph)container.Parent;
            string name = ReadingSemantics.EmbedNameOf((Section)paragraph.Parent);
            Assert.Equal("Embedded image: wide.svg", name);
            Assert.True(image.ActualWidth > 100, "premise: the image was not drawn wide");

            Assert.Equal(name, Words(LineAt(harness.Surface, container.ElementStart)));
            Run run = Assert.Single(HiddenRuns(paragraph));
            Assert.Equal(
                0,
                run.ContentStart.GetLineStartPosition(0)!.CompareTo(container.ElementStart.GetLineStartPosition(0)));
            Assert.Null(UIElementAutomationPeer.CreatePeerForElement(image));
        });
    }

    /// <summary>Call 4's run is hidden speech like the diagram's: find counts
    /// the card's name once, on its header, and a copy carries it once.</summary>
    [Fact]
    public void AnEmbeddedImagesNameIsFoundAndCopiedOnce()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-embed-image-find", EmbedNote, hosted: true, files: EmbedImages);
            ReadingDiagramImage image = AwaitEmbedImage(harness);
            FlowDocument document = harness.Surface.Document;
            Assert.Equal(2, new TextRange(document.ContentStart, document.ContentEnd).Text.Split("wide.svg").Length - 1);

            string index = ReadingTextIndex.Build(document).Text.Text;
            Assert.Equal(1, index.Split("wide.svg").Length - 1);
            string copied = CopyOf(harness.Surface, document.ContentStart, document.ContentEnd);
            Assert.Equal(1, copied.Split("wide.svg").Length - 1);
            Assert.Contains("After the card.", copied, StringComparison.Ordinal);
        });
    }

    /// <summary>Hidden speech is one line, or its run breaks its line in two
    /// and the element reads alone on the second. Core's rendered
    /// descriptions are one line today; this holds the host to it.</summary>
    [Fact]
    public void ARenderedDescriptionReadsAsOneLine()
    {
        RunSta(() =>
        {
            (List<(ReadingBlock, ReadingBlockInlines)> model, DiagramBlock rendered) = RenderedFlowchart();
            ReadingDocumentModel built = ReadingDocumentBuilder.Build(
                model,
                new ReadingListBuildContext(),
                Array.Empty<CodeBlock>(),
                Array.Empty<MathBlock>(),
                [rendered with { StructuredDescription = "Flowchart\nwith 1 step." }]);
            Paragraph paragraph = built.Document.Blocks.OfType<Paragraph>().Single(ReadingSemantics.IsDiagramBlock);
            Run run = Assert.Single(HiddenRuns(paragraph));
            Assert.Equal("Flowchart with 1 step.", run.Text);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopyingAcrossARenderedDiagramCopiesItsFencedSource(bool rebuilt)
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open(
                "reading-diagram-copy-" + (rebuilt ? "rebuilt" : "in-place"), StageNote, hosted: true);
            harness.Land();
            if (rebuilt)
            {
                // A fresh build of a note whose diagram had landed: the
                // builder's rendered case, not the completion in place.
                harness.Tab.EditorDocument!.Insert(harness.Tab.EditorDocument.TextLength, "\nAdded at the end.\n");
                harness.Model.Refresh();
                PumpedDispatcher.Drain();
                Assert.Contains("Added at the end.", harness.PatternText(), StringComparison.Ordinal);
            }
            FlowDocument document = harness.Surface.Document;
            Paragraph paragraph = harness.DiagramParagraph();

            string all = harness.Surface.CopyTextOf(document.ContentStart, document.ContentEnd)!;
            Assert.NotNull(all);
            Assert.Contains("After the diagram.", all, StringComparison.Ordinal);
            Assert.Contains(StageFence, all.Replace("\r\n", "\n"), StringComparison.Ordinal);
            Assert.DoesNotContain("Flowchart with 2 steps.", all, StringComparison.Ordinal);
            Assert.Equal(StageFence, harness.Surface.CopyTextOf(paragraph.ContentStart, paragraph.ContentEnd)!.Replace("\r\n", "\n"));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopyingAPendingDiagramDropsItsPlaceholderName(bool withMath)
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open(
                "reading-diagram-copy-pending-" + (withMath ? "math" : "plain"),
                withMath ? StageNote : UnsupportedNote,
                hosted: true);
            FlowDocument document = harness.Surface.Document;
            Paragraph paragraph = harness.DiagramParagraph();
            string firstLine = withMath ? "flowchart LR" : "weirdDiagram";

            string? all = harness.Surface.CopyTextOf(document.ContentStart, document.ContentEnd);
            Assert.NotNull(all);
            Assert.Contains(firstLine, all, StringComparison.Ordinal);
            Assert.DoesNotContain("Diagram, rendering", all, StringComparison.Ordinal);

            // From the name's line through the source's first line: the
            // selection reaches the hidden run.
            TextPointer firstEnd = PositionAtText(document, firstLine).GetPositionAtOffset(firstLine.Length)!;
            string? top = harness.Surface.CopyTextOf(paragraph.ContentStart, firstEnd);
            Assert.NotNull(top);
            Assert.EndsWith(firstLine, top, StringComparison.Ordinal);
            Assert.DoesNotContain("Diagram, rendering", top, StringComparison.Ordinal);

            // A part of the source: what is shown.
            TextPointer from = PositionAtText(document, firstLine);
            Assert.Equal(firstLine, CopyOf(harness.Surface, from, from.GetPositionAtOffset(firstLine.Length)!));
        });
    }

    /// <summary>The lead's ruling on CB-20's Call 11, with the diff red
    /// team's B1: a caret that was in a pending diagram's source, or at its
    /// placeholder, lands just after the rendered diagram, where its landmark
    /// is and where the caret is drawn at the line's height, so the line
    /// reads the description and the previous-diagram chord moves on from it.
    /// A failed landing keeps a source anchor on its character and puts one
    /// at the placeholder at the paragraph's start, where the name
    /// stood.</summary>
    [Theory]
    [InlineData("rendered", "the name")]
    [InlineData("rendered", "just after the placeholder")]
    [InlineData("rendered", "the source")]
    [InlineData("failed", "the name")]
    [InlineData("failed", "just after the placeholder")]
    public void ALandedDiagramPutsTheCaretWhereItsLandmarkIs(string outcome, string where)
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open(
                $"reading-diagram-anchor-{outcome}-{where.Replace(' ', '-')}",
                outcome == "rendered" ? StageNote : UnsupportedNote,
                hosted: true);
            Paragraph paragraph = harness.DiagramParagraph();
            InlineUIContainer container = ContainerOf(harness.Placeholder());
            Run name = Assert.Single(HiddenRuns(paragraph));
            TextPointer caret = where switch
            {
                "the name" => name.ContentStart.GetPositionAtOffset(3)!,
                "just after the placeholder" => container.ElementEnd,
                _ => PositionAtText(harness.Surface.Document, "flowchart LR").GetPositionAtOffset(5)!,
            };
            // On the name's line first, so the caret reaches the name as Left
            // would bring it there, along its line.
            harness.Surface.Selection.Select(container.ElementEnd, container.ElementEnd);
            harness.Surface.CaretPosition = container.ElementEnd;
            harness.Surface.Selection.Select(caret, caret);
            harness.Surface.CaretPosition = caret;
            Assert.Equal(0, harness.Surface.CaretPosition.CompareTo(caret));

            harness.Land();

            TextPointer landed = harness.Surface.CaretPosition;
            ReadingLandmark landmark = Assert.Single(
                harness.Surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Diagram);
            if (outcome == "failed")
            {
                if (where == "just after the placeholder")
                {
                    // The source's start: the failure shape's source run.
                    Assert.Equal("weirdDiagram", Words(LineAt(harness.Surface, landed)));
                    return;
                }
                Assert.Equal(0, landed.CompareTo(DiagramStart(harness)));
                Assert.True(landmark.Position.CompareTo(landed) >= 0, "the previous-diagram chord would land on this diagram again");
                return;
            }
            Assert.Equal(0, landed.CompareTo(ContainerOf(harness.Placeholder()).ElementEnd));
            Assert.Equal(0, landmark.Position.CompareTo(landed));
            Assert.Equal("Flowchart with 2 steps.", Words(LineAt(harness.Surface, landed)));
        });
    }

    /// <summary>The drawn caret's height as WPF lays it out: the selection's
    /// caret element, read by reflection, since WPF exposes no other.</summary>
    private static double CaretHeight(ReadingSurface surface)
    {
        surface.UpdateLayout();
        PumpedDispatcher.Drain();
        const System.Reflection.BindingFlags Hidden =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        object caret = typeof(TextSelection).GetProperty("CaretElement", Hidden)!.GetValue(surface.Selection)
            ?? throw new Xunit.Sdk.XunitException("the selection drew no caret");
        return (double)caret.GetType().GetField("_height", Hidden)!.GetValue(caret)!;
    }

    /// <summary>A key as the surface sees it: its PreviewKeyDown, the editing
    /// command WPF binds it to, then its PreviewKeyUp.</summary>
    private static void Press(ReadingSurface surface, Key key, RoutedUICommand move)
    {
        PresentationSource source = PresentationSource.FromVisual(surface)!;
        surface.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        move.Execute(null, surface);
        surface.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyUpEvent });
        PumpedDispatcher.Drain();
    }

    /// <summary>A line of each embedded kind between two body paragraphs: a
    /// display formula, a pending or a rendered diagram, or an embedded image
    /// (whose card header is the line just above it).</summary>
    private static (StageHarness Harness, Paragraph Line) OpenEmbeddedLine(string kind, string name)
    {
        const string Before = "Before the line.\n\n";
        const string After = "\n\nAfter the line.\n";
        if (kind == "image")
        {
            StageHarness card = StageHarness.Open(name, Before + "![[wide.svg]]" + After, hosted: true, files: EmbedImages);
            try
            {
                ReadingDiagramImage image = AwaitEmbedImage(card);
                return (card, (Paragraph)((InlineUIContainer)image.Parent).Parent);
            }
            catch
            {
                card.Dispose();
                throw;
            }
        }
        StageHarness harness = StageHarness.Open(
            name, Before + (kind == "math" ? "$$\nx^2\n$$" : StageFence) + After, hosted: true);
        if (kind == "rendered")
        {
            harness.Land();
        }
        Paragraph line = kind == "math"
            ? harness.Surface.Document.Blocks.OfType<Paragraph>().Single(ReadingSemantics.IsMathBlock)
            : harness.DiagramParagraph();
        return (harness, line);
    }

    /// <summary>The diff red team's B1: WPF draws a caret beside a run as
    /// tall as the run's font, and an embedded element's hidden speech is a
    /// sliver of a point, so Down onto its line drew a caret of 0.13 px where
    /// a body line's is 15.96 (measured). The caret now rests just after the
    /// element, on the same line, as tall as its paragraph's own text (a
    /// pending diagram's source, or else a body line), from the start or the
    /// end of the line above.</summary>
    [Theory]
    [InlineData("math")]
    [InlineData("pending")]
    [InlineData("rendered")]
    [InlineData("image")]
    public void ADownArrowOntoAnEmbeddedElementsLineDrawsTheCaretAtTheLinesHeight(string kind)
    {
        RunSta(() =>
        {
            (StageHarness harness, Paragraph line) = OpenEmbeddedLine(kind, $"reading-caret-down-{kind}");
            using (harness)
            {
                ReadingSurface surface = harness.Surface;
                surface.Focus();
                TextPointer body = line.Inlines.OfType<Run>().FirstOrDefault(run => !ReadingSemantics.IsHiddenSpeech(run)) is { } text
                    ? text.ContentStart
                    : PositionAtText(surface.Document, "After the line.");
                surface.Selection.Select(body, body);
                surface.CaretPosition = body;
                double bodyHeight = CaretHeight(surface);
                Assert.True(bodyHeight > 10, $"the reference caret is {bodyHeight:F2} px tall");
                Paragraph above = Assert.IsType<Paragraph>(line.PreviousBlock);
                foreach (TextPointer from in new[]
                {
                    above.ContentStart.GetInsertionPosition(LogicalDirection.Forward),
                    above.ContentEnd.GetInsertionPosition(LogicalDirection.Backward),
                })
                {
                    surface.Selection.Select(from, from);
                    surface.CaretPosition = from;
                    PumpedDispatcher.Drain();

                    EditingCommands.MoveDownByLine.Execute(null, surface);
                    PumpedDispatcher.Drain();

                    TextPointer caret = surface.CaretPosition;
                    Assert.Same(line, caret.Paragraph);
                    Assert.Null(ReadingDocumentBuilder.VisibleCaretPast(caret));
                    double height = CaretHeight(surface);
                    Assert.True(
                        Math.Abs(height - bodyHeight) <= 1,
                        $"Down from offset {above.ContentStart.GetOffsetToPosition(from)} of the line above drew a caret {height:F2} px tall "
                            + $"at offset {line.ContentStart.GetOffsetToPosition(caret)} of the line, gravity {caret.LogicalDirection}, "
                            + $"where its paragraph's text draws {bodyHeight:F2}");
                }
            }
        });
    }

    /// <summary>A Right that moves nothing (at the document's end) leaves no
    /// mark on the move after it: a caret a UIA client then sets on hidden
    /// speech, from another line, still rests just after the element.</summary>
    [Fact]
    public void AnArrowThatMovesNothingLeavesNoMarkOnTheNextMove()
    {
        RunSta(() =>
        {
            (StageHarness harness, Paragraph line) = OpenEmbeddedLine("math", "reading-caret-no-move");
            using (harness)
            {
                ReadingSurface surface = harness.Surface;
                surface.Focus();
                TextPointer end = surface.Document.ContentEnd.GetInsertionPosition(LogicalDirection.Backward);
                surface.Selection.Select(end, end);
                surface.CaretPosition = end;
                Press(surface, Key.Right, EditingCommands.MoveRightByCharacter);
                Assert.Equal(0, surface.CaretPosition.CompareTo(end));

                TextPointer hidden = Assert.Single(HiddenRuns(line)).ContentStart;
                surface.Selection.Select(hidden, hidden);
                PumpedDispatcher.Drain();

                Assert.Null(ReadingDocumentBuilder.VisibleCaretPast(surface.CaretPosition));
                Assert.Same(line, surface.CaretPosition.Paragraph);
            }
        });
    }

    /// <summary>The lead's condition on B1's fix: no caret stop is added or
    /// taken away. Right from the line above visits the hidden speech's every
    /// letter, then the element's end, and Left visits them back, as before
    /// the fix: a key that moves along the line leaves the caret where it put
    /// it.</summary>
    [Theory]
    [InlineData("math")]
    [InlineData("pending")]
    [InlineData("rendered")]
    [InlineData("image")]
    public void RightAndLeftAlongAnEmbeddedElementsLineStopWhereTheyDidBefore(string kind)
    {
        RunSta(() =>
        {
            (StageHarness harness, Paragraph line) = OpenEmbeddedLine(kind, $"reading-caret-along-{kind}");
            using (harness)
            {
                ReadingSurface surface = harness.Surface;
                surface.Focus();
                Run hidden = Assert.Single(HiddenRuns(line));
                InlineUIContainer container = line.Inlines.OfType<InlineUIContainer>().First();
                TextPointer lineStart = line.ContentStart.GetInsertionPosition(LogicalDirection.Forward).GetLineStartPosition(0)!;
                bool OnTheLine(TextPointer caret) =>
                    caret.Paragraph == line && caret.GetLineStartPosition(0)?.CompareTo(lineStart) == 0;
                string Stop(TextPointer caret) =>
                    caret.CompareTo(container.ElementEnd) >= 0 ? "after the element"
                    : caret.CompareTo(hidden.ContentStart) >= 0 && caret.CompareTo(hidden.ContentEnd) <= 0
                        ? $"letter {hidden.ContentStart.GetOffsetToPosition(caret)}"
                    : caret.CompareTo(hidden.ContentStart) < 0 ? "letter 0"
                    : $"letter {hidden.Text.Length}";
                List<string> stops = Enumerable.Range(0, hidden.Text.Length + 1)
                    .Select(letter => $"letter {letter}")
                    .Append("after the element")
                    .ToList();

                Paragraph above = Assert.IsType<Paragraph>(line.PreviousBlock);
                TextPointer end = above.ContentEnd.GetInsertionPosition(LogicalDirection.Backward);
                surface.Selection.Select(end, end);
                surface.CaretPosition = end;
                var right = new List<string>();
                for (int press = 0; press < 200; press++)
                {
                    Press(surface, Key.Right, EditingCommands.MoveRightByCharacter);
                    if (!OnTheLine(surface.CaretPosition))
                    {
                        break;
                    }
                    right.Add(Stop(surface.CaretPosition));
                }
                var left = new List<string>();
                for (int press = 0; press < 200; press++)
                {
                    Press(surface, Key.Left, EditingCommands.MoveLeftByCharacter);
                    if (!OnTheLine(surface.CaretPosition))
                    {
                        if (left.Count > 0)
                        {
                            break;
                        }
                        continue;
                    }
                    left.Add(Stop(surface.CaretPosition));
                }

                Assert.Equal(stops, right);
                stops.Reverse();
                Assert.Equal(stops, left);
            }
        });
    }

    /// <summary>The lead's ruling on CB-20's Call 12: a drag carries no hidden
    /// text in its RTF, for a diagram as for a formula, since both go through
    /// the same <c>OnCopying</c> path.</summary>
    [Theory]
    [InlineData("rendered")]
    [InlineData("pending")]
    [InlineData("math")]
    public void ADragAcrossADiagramCarriesNoHiddenTextInRtf(string kind)
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open($"reading-diagram-drag-{kind}", StageNote, hosted: true);
            if (kind == "rendered")
            {
                harness.Land();
            }
            string hidden = kind switch
            {
                "rendered" => "Flowchart with 2 steps.",
                "pending" => "Diagram, rendering",
                _ => FindElements<ReadingMathElement>(harness.Surface.Document).First().Speech,
            };
            ReadingSurface surface = harness.Surface;
            surface.SelectAll();
            using var saved = new MemoryStream();
            new TextRange(surface.Selection.Start, surface.Selection.End).Save(saved, DataFormats.Rtf);
            string before = System.Text.Encoding.ASCII.GetString(saved.ToArray());
            Assert.Contains(hidden, RtfText(before), StringComparison.Ordinal);
            var dragged = new DataObject(DataFormats.Rtf, before);
            dragged.SetData(DataFormats.UnicodeText, surface.Selection.Text);

            surface.RaiseEvent(new DataObjectCopyingEventArgs(dragged, isDragDrop: true));

            string rtf = (string)dragged.GetData(DataFormats.Rtf)!;
            string pasted = RtfText(rtf);
            Assert.DoesNotContain(hidden, pasted, StringComparison.Ordinal);
            Assert.Contains("After the diagram.", pasted, StringComparison.Ordinal);
            Assert.Equal(
                ((string)dragged.GetData(DataFormats.UnicodeText)!).Replace("\r\n", "\n").TrimEnd('\n'),
                pasted.Replace("\r\n", "\n").TrimEnd('\n'));
        });
    }

    /// <summary>The spec red team's M1: a copy that only leaves hidden runs
    /// out keeps WPF's rich text, without the runs, so a paste into a rich
    /// editor keeps its formatting and never carries the image's name
    /// twice.</summary>
    [Fact]
    public void ACopyAcrossAnEmbeddedImageKeepsItsRichTextWithoutItsName()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open(
                "reading-embed-image-copy-rtf", "**Bold** before the card.\n\n" + EmbedNote, hosted: true, files: EmbedImages);
            _ = AwaitEmbedImage(harness);
            ReadingSurface surface = harness.Surface;
            var written = new List<string>();
            ReadingSurface.PlainTextClipboardForTests = written.Add;
            try
            {
                surface.SelectAll();
                IDataObject? copied = null;
                bool cancelled = false;
                DataObject.AddCopyingHandler(surface, (_, e) =>
                {
                    copied = e.DataObject;
                    cancelled = e.CommandCancelled;
                    e.CancelCommand();
                });
                surface.Copy();

                Assert.NotNull(copied);
                Assert.False(cancelled, "the copy put plain text alone on the clipboard");
                Assert.Empty(written);
                string rtf = (string)copied!.GetData(DataFormats.Rtf)!;
                string text = RtfText(rtf);
                Assert.Contains("Bold before the card.", text, StringComparison.Ordinal);
                Assert.Contains("After the card.", text, StringComparison.Ordinal);
                Assert.Equal(1, text.Split("wide.svg").Length - 1);
                Assert.Contains(@"\b", rtf, StringComparison.Ordinal);
                Assert.Equal(1, ((string)copied.GetData(DataFormats.UnicodeText)!).Split("wide.svg").Length - 1);
            }
            finally
            {
                ReadingSurface.PlainTextClipboardForTests = null;
            }
        });
    }

    /// <summary>The spec red team: a selection of hidden letters alone
    /// shows nothing, so a copy of it leaves the clipboard as it was.</summary>
    [Fact]
    public void ACopyOfOnlyHiddenTextLeavesTheClipboardAlone()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-embed-image-copy-hidden", EmbedNote, hosted: true, files: EmbedImages);
            ReadingDiagramImage image = AwaitEmbedImage(harness);
            var paragraph = (Paragraph)((InlineUIContainer)image.Parent).Parent;
            Run name = Assert.Single(HiddenRuns(paragraph));
            var written = new List<string>();
            ReadingSurface.PlainTextClipboardForTests = written.Add;
            try
            {
                harness.Surface.Selection.Select(
                    name.ContentStart.GetPositionAtOffset(2)!, name.ContentStart.GetPositionAtOffset(8)!);
                IDataObject? copied = null;
                bool cancelled = false;
                DataObject.AddCopyingHandler(harness.Surface, (_, e) =>
                {
                    copied = e.DataObject;
                    cancelled = e.CommandCancelled;
                    e.CancelCommand();
                });
                harness.Surface.Copy();

                Assert.True(cancelled, "a copy of hidden letters alone went ahead");
                Assert.Empty(written);
            }
            finally
            {
                ReadingSurface.PlainTextClipboardForTests = null;
            }
        });
    }

    /// <summary>A drag that reaches no hidden run is WPF's own: its rich
    /// text is left as it was built.</summary>
    [Fact]
    public void ADragThatReachesNoHiddenTextKeepsWpfsRichText()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-drag-plain", StageNote, hosted: true);
            ReadingSurface surface = harness.Surface;
            Paragraph after = surface.Document.Blocks.OfType<Paragraph>().Single(p => new TextRange(p.ContentStart, p.ContentEnd).Text == "After the diagram.");
            surface.Selection.Select(after.ContentStart, after.ContentEnd);
            using var saved = new MemoryStream();
            new TextRange(surface.Selection.Start, surface.Selection.End).Save(saved, DataFormats.Rtf);
            string before = ShippedFontNames.InRtfFontTable(System.Text.Encoding.ASCII.GetString(saved.ToArray()));
            var dragged = new DataObject(DataFormats.Rtf, before);

            surface.RaiseEvent(new DataObjectCopyingEventArgs(dragged, isDragDrop: true));

            Assert.Equal(before, (string)dragged.GetData(DataFormats.Rtf)!);
        });
    }

    /// <summary>The RTF a drag carries when its copy changed the text holds
    /// that text exactly: braces (a mermaid decision node), backslashes, a
    /// literal control word, accents and a character past the BMP.</summary>
    [Fact]
    public void PlainTextRtfHoldsItsTextExactly()
    {
        RunSta(() =>
        {
            string text = "A{Decision} --> B\\C\r\nsecond \\par line, caf\u00e9 and \U0001D465\tend";
            Assert.Equal(
                text.ReplaceLineEndings("\n"),
                RtfText(ReadingSurface.PlainTextRtf(text)).ReplaceLineEndings("\n").TrimEnd('\n'));
        });
    }

    /// <summary>An embedded image's line reads its card's name, so Enter there
    /// does what Enter on the card's header does.</summary>
    [Fact]
    public void EnterOnAnEmbeddedImagesLineActivatesItsCard()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-embed-image-enter", EmbedNote, hosted: true, files: EmbedImages);
            ReadingDiagramImage image = AwaitEmbedImage(harness);
            var container = (InlineUIContainer)image.Parent;
            harness.Surface.CaretPosition = container.ElementEnd;
            Assert.True(harness.Surface.TryActivateAtCaret(), "Enter on the image's line did nothing");
        });
    }

    private static string RtfText(string rtf)
    {
        var recipient = new FlowDocument();
        var range = new TextRange(recipient.ContentStart, recipient.ContentEnd);
        using var bytes = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(rtf));
        range.Load(bytes, DataFormats.Rtf);
        return range.Text;
    }

    /// <summary>The lead's ruling on CB-20's Call 3: the rule is no
    /// unavailable control. Its peer is an enabled Separator with no name
    /// that takes no keys, under every theme the app ships.</summary>
    [Theory]
    [InlineData("none")]
    [InlineData("light")]
    [InlineData("dark")]
    [InlineData("high contrast")]
    public void TheRuleIsNotUnavailable(string theme)
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open(
                "reading-rule-" + theme.Replace(' ', '-'), "Before the rule.\n\n---\n\nAfter the rule.\n", hosted: true);
            if (theme != "none")
            {
                Window window = Window.GetWindow(harness.Surface)!;
                foreach (ResourceDictionary layer in ThemeManager.DictionariesFor(
                             theme == "dark" ? SlateTheme.Dark : SlateTheme.Light, theme == "high contrast"))
                {
                    window.Resources.MergedDictionaries.Add(layer);
                }
                window.UpdateLayout();
                PumpedDispatcher.Drain();
            }
            Separator rule = Assert.IsType<Separator>(
                Assert.Single(harness.Surface.Document.Blocks.OfType<BlockUIContainer>()).Child);
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(rule);

            Assert.Equal(AutomationControlType.Separator, peer.GetAutomationControlType());
            Assert.True(peer.IsEnabled(), "the rule says \"unavailable\"");
            Assert.Equal(string.Empty, peer.GetName());
            Assert.False(peer.IsKeyboardFocusable());
            Assert.False(rule.Focus());
        });
    }
}
