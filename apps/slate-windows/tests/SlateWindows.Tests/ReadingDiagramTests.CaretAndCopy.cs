// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1708 (plan 47 CB-20), the merge judge's findings on the caret rule and
/// the copy: a mouse press keeps WPF's anchor where it was pressed, a
/// selection collapsed onto its anchor stays there, the caret moves only
/// onto the element's own line, a republish or a rebind keeps a reader's
/// hidden letter, "next" chords move on from inside the speech, and a copy
/// that puts a fenced source in a block's place keeps the rest's rich text
/// and pictures.
/// </summary>
public sealed partial class ReadingDiagramTests
{
    /// <summary>A mouse button pressed on the surface, as WPF's input
    /// manager raises it before its own selection handling.</summary>
    private static void PressMouse(ReadingSurface surface) =>
        surface.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = Mouse.PreviewMouseDownEvent,
        });

    /// <summary>The button released, and whatever the release posted.</summary>
    private static void ReleaseMouse(ReadingSurface surface)
    {
        surface.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = Mouse.PreviewMouseUpEvent,
        });
        PumpedDispatcher.Drain();
    }

    /// <summary>The merge judge's M1, a plain click: a press in the margin
    /// left of an element puts WPF's caret at the line's start, in the hidden
    /// speech. It stays there while the button is down (WPF's anchor for a
    /// drag), and on release it rests just after the element, at the line's
    /// height.</summary>
    [Theory]
    [InlineData("math")]
    [InlineData("rendered")]
    [InlineData("tiny image")]
    public void AClickBesideAnElementLeavesTheCaretWhereItWasPressedUntilRelease(string kind)
    {
        RunSta(() =>
        {
            (StageHarness harness, Paragraph line) = OpenEmbeddedLine(kind, $"reading-caret-click-{kind}");
            using (harness)
            {
                ReadingSurface surface = harness.Surface;
                surface.Focus();
                TextPointer body = PositionAtText(surface.Document, "Before the line.");
                surface.CaretPosition = body;
                Run hidden = Assert.Single(HiddenRuns(line));
                InlineUIContainer container = line.Inlines.OfType<InlineUIContainer>().First();

                PressMouse(surface);
                surface.Selection.Select(hidden.ContentStart, hidden.ContentStart);

                Assert.Equal(0, surface.CaretPosition.CompareTo(hidden.ContentStart));

                ReleaseMouse(surface);

                Assert.Equal(0, surface.CaretPosition.CompareTo(container.ElementEnd));
            }
        });
    }

    /// <summary>The merge judge's M1, a drag: WPF extends a drag from where
    /// the press put the caret, so a press in the margin left of an element
    /// and a drag down select the element too, and a copy carries it: the
    /// picture, or the fenced source.</summary>
    [Theory]
    [InlineData("math")]
    [InlineData("rendered")]
    [InlineData("tiny image")]
    public void ADragFromBesideAnElementKeepsTheElementInTheSelection(string kind)
    {
        RunSta(() =>
        {
            (StageHarness harness, Paragraph line) = OpenEmbeddedLine(kind, $"reading-caret-drag-{kind}");
            using (harness)
            {
                ReadingSurface surface = harness.Surface;
                surface.Focus();
                surface.CaretPosition = PositionAtText(surface.Document, "Before the line.");
                Run hidden = Assert.Single(HiddenRuns(line));
                InlineUIContainer container = line.Inlines.OfType<InlineUIContainer>().First();
                TextPointer below = PositionAtText(surface.Document, "After the line.").GetPositionAtOffset(5)!;

                PressMouse(surface);
                surface.Selection.Select(hidden.ContentStart, hidden.ContentStart);
                // WPF's drag extends from the selection's anchor, which is its
                // start while the selection is empty.
                surface.Selection.Select(surface.Selection.Start, below);
                ReleaseMouse(surface);

                Assert.True(
                    surface.Selection.Start.CompareTo(container.ElementStart) <= 0,
                    "the drag's selection begins after the element it was pressed beside");
                if (kind == "tiny image")
                {
                    IDataObject? copied = null;
                    DataObject.AddCopyingHandler(surface, (_, e) =>
                    {
                        copied = e.DataObject;
                        e.CancelCommand();
                    });
                    surface.Copy();
                    Assert.NotNull(copied);
                    Assert.Contains(@"\pict", (string)copied!.GetData(DataFormats.Rtf)!, StringComparison.Ordinal);
                }
                else
                {
                    string copy = surface.CopyTextOf(surface.Selection.Start, surface.Selection.End) ?? string.Empty;
                    Assert.Contains(kind == "math" ? "x^2" : "flowchart LR", copy, StringComparison.Ordinal);
                }
            }
        });
    }

    /// <summary>The merge judge's M1, the keyboard twin: a Shift move that
    /// collapses a selection back onto its anchor in hidden speech leaves the
    /// caret on that anchor, where the reader put it.</summary>
    [Fact]
    public void ASelectionCollapsedOntoItsAnchorInHiddenSpeechStaysThere()
    {
        RunSta(() =>
        {
            (StageHarness harness, Paragraph line) = OpenEmbeddedLine("math", "reading-caret-collapse");
            using (harness)
            {
                ReadingSurface surface = harness.Surface;
                surface.Focus();
                Run hidden = Assert.Single(HiddenRuns(line));
                InlineUIContainer container = line.Inlines.OfType<InlineUIContainer>().First();
                // On the line first, then onto a letter, as Left brings a reader there.
                surface.CaretPosition = container.ElementEnd;
                TextPointer letter = hidden.ContentStart.GetPositionAtOffset(2)!;
                surface.CaretPosition = letter;
                TextPointer below = PositionAtText(surface.Document, "After the line.").GetPositionAtOffset(5)!;

                surface.Selection.Select(letter, below);
                surface.Selection.Select(letter, letter);

                Assert.Equal(0, surface.CaretPosition.CompareTo(letter));
            }
        });
    }

    /// <summary>The merge judge's m1: the caret moves past an element only
    /// onto the element's own line. Forced onto a line of its own (a width
    /// past the line's), the element leaves its hidden speech above it, and a
    /// caret that arrives there stays, so Up can pass the block.</summary>
    [Fact]
    public void TheCaretMovesPastAnElementOnlyOntoItsOwnLine()
    {
        RunSta(() =>
        {
            (StageHarness harness, Paragraph line) = OpenEmbeddedLine("image", "reading-caret-wrapped");
            using (harness)
            {
                ReadingSurface surface = harness.Surface;
                surface.Focus();
                Run hidden = Assert.Single(HiddenRuns(line));
                InlineUIContainer container = line.Inlines.OfType<InlineUIContainer>().First();
                ((FrameworkElement)container.Child).Width = 5000;
                surface.UpdateLayout();
                PumpedDispatcher.Drain();
                Assert.NotEqual(
                    0,
                    hidden.ContentStart.GetLineStartPosition(0)!.CompareTo(container.ElementEnd.GetLineStartPosition(0)));
                surface.CaretPosition = PositionAtText(surface.Document, "Before the line.");

                surface.Selection.Select(hidden.ContentStart, hidden.ContentStart);

                Assert.Equal(0, surface.CaretPosition.CompareTo(hidden.ContentStart));
            }
        });
    }

    /// <summary>The merge judge's m2 (AGENTS.md's republish fact): a reader
    /// stepped onto a hidden letter keeps that letter when the note is
    /// re-projected by an edit elsewhere and when the surface is rebound to
    /// its projection.</summary>
    [Theory]
    [InlineData("math")]
    [InlineData("rendered")]
    [InlineData("tiny image")]
    public void AReaderOnAHiddenLetterKeepsItAcrossARepublishAndARebind(string kind)
    {
        RunSta(() =>
        {
            (StageHarness harness, Paragraph line) = OpenEmbeddedLine(kind, $"reading-caret-republish-{kind}");
            using (harness)
            {
                ReadingSurface surface = harness.Surface;
                surface.Focus();
                Run hidden = Assert.Single(HiddenRuns(line));
                InlineUIContainer container = line.Inlines.OfType<InlineUIContainer>().First();
                surface.CaretPosition = container.ElementEnd;
                surface.CaretPosition = hidden.ContentStart.GetPositionAtOffset(3)!;

                int Letter()
                {
                    Paragraph now = kind switch
                    {
                        "math" => surface.Document.Blocks.OfType<Paragraph>().Single(ReadingSemantics.IsMathBlock),
                        "rendered" => harness.DiagramParagraph(),
                        _ => (Paragraph)((InlineUIContainer)AwaitEmbedImage(harness).Parent).Parent,
                    };
                    Run run = Assert.Single(HiddenRuns(now));
                    Assert.Same(now, surface.CaretPosition.Paragraph);
                    return run.ContentStart.GetOffsetToPosition(surface.CaretPosition);
                }

                Assert.Equal(3, Letter());

                int applies = harness.Applies;
                harness.Tab.EditorDocument!.Insert(
                    harness.Tab.EditorDocument.TextLength, "\nA paragraph added at the end.\n");
                harness.Model.Refresh();
                PumpedDispatcher.Drain();
                Assert.Equal(applies + 1, harness.Applies);
                Assert.Equal(3, Letter());

                ReadingContentViewModel model = harness.Model;
                surface.Model = null;
                surface.Model = model;
                PumpedDispatcher.Drain();
                Assert.Equal(3, Letter());
            }
        });
    }

    /// <summary>The merge judge's nit 5: a formula's landmark sits after its
    /// element, so "next math" from inside its speech (Home on its line, or a
    /// letter stepped onto) moves on to the next formula, not to this one's
    /// end.</summary>
    [Fact]
    public void NextMathFromInsideAFormulasSpeechMovesOnToTheNextFormula()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open(
                "reading-caret-next-math", "Before.\n\n$$\nx^2\n$$\n\nMiddle.\n\n$$\ny^2\n$$\n\nAfter.\n", hosted: true);
            ReadingSurface surface = harness.Surface;
            Paragraph[] formulas = surface.Document.Blocks.OfType<Paragraph>().Where(ReadingSemantics.IsMathBlock).ToArray();
            Assert.Equal(2, formulas.Length);
            Run hidden = Assert.Single(HiddenRuns(formulas[0]));
            surface.CaretPosition = formulas[0].Inlines.OfType<InlineUIContainer>().First().ElementEnd;
            surface.CaretPosition = hidden.ContentStart.GetPositionAtOffset(2)!;
            var announced = new List<A11yEvent>();
            var navigator = new ReadingNavigator(surface, announced.Add);
            navigator.SetLandmarks(surface.LandmarksForTests);
            ReadingLandmark second = surface.LandmarksForTests.Where(landmark => landmark.Kind == ReadingLandmarkKind.Math).Last();

            navigator.Move(ReadingLandmarkKind.Math, forward: true);

            Assert.Equal(0, surface.CaretPosition.CompareTo(second.Position));
        });
    }

    /// <summary>The merge judge's m3: a copy that puts a block's fenced
    /// source in its place (a rendered diagram, a formula) keeps the rest's
    /// rich text and pictures, with the source as a paragraph of its own
    /// where the block was, and no hidden speech, for a copy and a
    /// drag.</summary>
    [Theory]
    [InlineData("rendered", false)]
    [InlineData("rendered", true)]
    [InlineData("math", false)]
    [InlineData("math", true)]
    public void ACopyWithAFencedSourceKeepsTheRestsRichTextAndPictures(string kind, bool drag)
    {
        RunSta(() =>
        {
            string block = kind == "math" ? "$$\nx^2\n$$" : StageFence;
            using var harness = StageHarness.Open(
                $"reading-copy-rich-{kind}-{drag}",
                "**Bold** before.\n\n![[pic.png]]\n\n" + block + "\n\nAfter the block.\n",
                hosted: true,
                files: EmbedImages);
            if (kind == "rendered")
            {
                harness.Land();
            }
            _ = AwaitEmbedImage(harness);
            ReadingSurface surface = harness.Surface;
            string hidden = kind == "math"
                ? FindElements<ReadingMathElement>(surface.Document).First().Speech
                : "Flowchart with 2 steps.";
            surface.SelectAll();
            IDataObject data;
            bool cancelled = false;
            if (drag)
            {
                using var saved = new MemoryStream();
                new TextRange(surface.Selection.Start, surface.Selection.End).Save(saved, DataFormats.Rtf);
                data = new DataObject(DataFormats.Rtf, System.Text.Encoding.ASCII.GetString(saved.ToArray()));
                surface.RaiseEvent(new DataObjectCopyingEventArgs(data, isDragDrop: true));
            }
            else
            {
                IDataObject? copied = null;
                DataObject.AddCopyingHandler(surface, (_, e) =>
                {
                    copied = e.DataObject;
                    cancelled = e.CommandCancelled;
                    e.CancelCommand();
                });
                surface.Copy();
                Assert.NotNull(copied);
                data = copied!;
            }

            Assert.False(cancelled, "the copy put plain text alone on the clipboard");
            string rtf = (string)data.GetData(DataFormats.Rtf)!;
            string text = RtfText(rtf);
            Assert.Contains(@"\pict", rtf, StringComparison.Ordinal);
            Assert.Contains(@"\b", rtf, StringComparison.Ordinal);
            Assert.Contains("Bold before.", text, StringComparison.Ordinal);
            Assert.Contains(kind == "math" ? "x^2" : "flowchart LR", text, StringComparison.Ordinal);
            Assert.Contains("After the block.", text, StringComparison.Ordinal);
            Assert.DoesNotContain(hidden, text, StringComparison.Ordinal);
            Assert.Equal(1, text.Split("pic.png").Length - 1);
        });
    }
}
