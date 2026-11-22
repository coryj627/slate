// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using SlateWindows.Reading;

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
    private static string Words(string line) => line.Trim(' ', '￼', '\r', '\n');

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

    /// <summary>The lead's ruling on CB-20's Call 11: an anchor that was in a
    /// pending diagram's source, or at its placeholder, lands at the rendered
    /// diagram's start, where its landmark is, so the line reads the
    /// description and the previous-diagram chord moves on from it. A failed
    /// landing keeps a source anchor on its character and puts one at the
    /// placeholder at the paragraph's start, where the name stood.</summary>
    [Theory]
    [InlineData("rendered", "the name")]
    [InlineData("rendered", "just after the placeholder")]
    [InlineData("rendered", "the source")]
    [InlineData("failed", "the name")]
    [InlineData("failed", "just after the placeholder")]
    public void ALandedDiagramPutsTheCaretAtItsStart(string outcome, string where)
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
            harness.Surface.Selection.Select(caret, caret);
            harness.Surface.CaretPosition = caret;

            harness.Land();

            TextPointer landed = harness.Surface.CaretPosition;
            if (outcome == "failed" && where == "just after the placeholder")
            {
                // The source's start: the failure shape's source run.
                Assert.Equal("weirdDiagram", Words(LineAt(harness.Surface, landed)));
                return;
            }
            Assert.Equal(0, landed.CompareTo(DiagramStart(harness)));
            ReadingLandmark landmark = Assert.Single(
                harness.Surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Diagram);
            Assert.True(landmark.Position.CompareTo(landed) >= 0, "the previous-diagram chord would land on this diagram again");
            if (outcome == "rendered")
            {
                Assert.Equal("Flowchart with 2 steps.", Words(LineAt(harness.Surface, landed)));
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

            string rtf = (string)dragged.GetData(DataFormats.Rtf);
            string pasted = RtfText(rtf);
            Assert.DoesNotContain(hidden, pasted, StringComparison.Ordinal);
            Assert.Contains("After the diagram.", pasted, StringComparison.Ordinal);
            Assert.Equal(
                ((string)dragged.GetData(DataFormats.UnicodeText)).Replace("\r\n", "\n").TrimEnd('\n'),
                pasted.Replace("\r\n", "\n").TrimEnd('\n'));
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
