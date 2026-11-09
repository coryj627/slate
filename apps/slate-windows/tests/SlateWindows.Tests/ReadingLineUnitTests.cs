// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation.Text;
using System.Windows.Documents;
using SlateWindows.Reading;

namespace SlateWindows.Tests;

/// <summary>
/// #1687: NVDA reads the reading view's line as the caret's range expanded to a Line through
/// the surface's Text pattern. Where Up, Down and End leave the caret (the end of a line),
/// that expansion held the caret's line and the next one, so each arrow spoke two lines.
/// A caret's Line is the one visual line it is on (its bounding rectangles one row), across
/// paragraphs, list items, a hard break and a soft wrap; a selection's is WPF's.
/// </summary>
public sealed class ReadingLineUnitTests
{
    private const string Note =
        "# Big list\n\nIntro line.\n\n- item 1\n- item 2\n- item 3\n\n## After the list\n\nClosing paragraph.\n\n1. First\n2. Second\n";

    [Fact]
    public void UpFromTheEndReadsOneLineAtATime() => RunSta(() =>
    {
        using var host = new ShownShell(("Lines.md", Note));
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "Lines.md", "Closing paragraph.");

        EditingCommands.MoveToDocumentEnd.Execute(null, surface);
        host.Settle();
        var wrong = new List<string>();
        for (int press = 0; press < 12; press++)
        {
            Check(provider, $"Up {press}", wrong);
            EditingCommands.MoveUpByLine.Execute(null, surface);
            host.Settle();
        }

        Assert.True(wrong.Count == 0, "Lines read as more than one line:\n" + string.Join('\n', wrong));
    });

    /// <summary>A long paragraph wrapped in a narrow pane: End on each visual line leaves the
    /// caret at the wrap, and its line is that visual line alone.</summary>
    [Fact]
    public void ASoftWrappedLineIsOneVisualLine() => RunSta(() =>
    {
        string words = string.Join(' ', Enumerable.Range(1, 60).Select(index => $"word{index}"));
        using var host = new ShownShell(("Wrap.md", $"# Wrap\n\n{words}\n"));
        host.Shell.ContentPaneBorder.Width = 320;
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "Wrap.md", "word60");
        Paragraph long_ = Paragraphs(surface.Document).Single(p => Text(p).StartsWith("word1 ", StringComparison.Ordinal));
        Assert.True(
            long_.ContentStart.GetLineStartPosition(2) is not null,
            "premise: the paragraph doesn't wrap to three lines");

        surface.CaretPosition = long_.ContentStart;
        host.Settle();
        var wrong = new List<string>();
        for (int line = 0; line < 3; line++)
        {
            EditingCommands.MoveToLineEnd.Execute(null, surface);
            host.Settle();
            Check(provider, $"line {line} at its end", wrong);
            EditingCommands.MoveToLineStart.Execute(null, surface);
            host.Settle();
            Check(provider, $"line {line} at its start", wrong);
            EditingCommands.MoveDownByLine.Execute(null, surface);
            host.Settle();
        }

        Assert.True(wrong.Count == 0, "Wrapped lines read as more than one line:\n" + string.Join('\n', wrong));
    });

    /// <summary>Every caret position, with either affinity, in a note of headings, list items,
    /// hard breaks and a wrapped paragraph: its Line is one visual line, and holds the caret.</summary>
    [Fact]
    public void EveryCaretReadsTheOneLineItIsOn() => RunSta(() =>
    {
        string words = string.Join(' ', Enumerable.Range(1, 40).Select(index => $"word{index}"));
        using var host = new ShownShell(("Every.md", Note + "\nFirst line  \nSecond line  \nThird line\n\n" + words + "\n"));
        host.Shell.ContentPaneBorder.Width = 320;
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "Every.md", "word40");
        Assert.True(
            Paragraphs(surface.Document).Any(p => Text(p).Contains('\n')),
            "premise: the hard breaks aren't newlines inside one paragraph");

        var wrong = new List<string>();
        int checkedCarets = 0;
        for (TextPointer? at = surface.Document.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
             at is not null;
             at = at.GetNextInsertionPosition(LogicalDirection.Forward))
        {
            foreach (LogicalDirection affinity in new[] { LogicalDirection.Backward, LogicalDirection.Forward })
            {
                TextPointer caret = at.GetPositionAtOffset(0, affinity);
                surface.Selection.Select(caret, caret);
                surface.CaretPosition = caret;
                host.Settle();
                string where = $"offset {surface.Document.ContentStart.GetOffsetToPosition(at)} {affinity}";
                Check(provider, where, wrong);
                ITextRangeProvider line = provider.GetSelection()[0];
                ITextRangeProvider point = line.Clone();
                line.ExpandToEnclosingUnit(TextUnit.Line);
                if (line.CompareEndpoints(TextPatternRangeEndpoint.Start, point, TextPatternRangeEndpoint.Start) > 0
                    || line.CompareEndpoints(TextPatternRangeEndpoint.End, point, TextPatternRangeEndpoint.End) < 0)
                {
                    wrong.Add($"{where}: \"{line.GetText(-1)}\" doesn't hold the caret");
                }

                checkedCarets++;
            }
        }

        Assert.True(checkedCarets > 100, $"premise: only {checkedCarets} carets walked");
        Assert.True(wrong.Count == 0, $"{wrong.Count} of {checkedCarets} carets read wrong:\n" + string.Join('\n', wrong.Take(30)));
    });

    /// <summary>A selection's Line expansion is WPF's own: only a caret's is cut.</summary>
    [Fact]
    public void ASelectionExpandsAsWpfDoes() => RunSta(() =>
    {
        using var host = new ShownShell(("Lines.md", Note));
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "Lines.md", "Closing paragraph.");
        Paragraph intro = Paragraphs(surface.Document).Single(p => Text(p) == "Intro line.");

        surface.Selection.Select(intro.ContentEnd, surface.Document.ContentEnd);
        host.Settle();
        var wrapped = Assert.IsType<HeadingStyleTextRange>(provider.GetSelection()[0]);
        ITextRangeProvider raw = wrapped.Inner.Clone();
        raw.ExpandToEnclosingUnit(TextUnit.Line);
        wrapped.ExpandToEnclosingUnit(TextUnit.Line);

        Assert.Equal(raw.GetText(-1), wrapped.GetText(-1));
    });

    private static (ReadingSurface Surface, ITextProvider Provider) OpenReading(ShownShell host, string path, string lastText)
    {
        host.Workspace.OpenPath(path);
        host.Settle();
        host.Workspace.ToggleReadingModeCommand.Execute(null);
        ReadingSurface? surface = null;
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () =>
                {
                    host.Settle();
                    surface = ShownShell.Descendants(host.Shell).OfType<ReadingSurface>().FirstOrDefault(s => s.IsVisible);
                    return surface is not null && Paragraphs(surface.Document).Any(p => Text(p).Contains(lastText, StringComparison.Ordinal));
                },
                TimeSpan.FromSeconds(30)),
            "the reading view never showed the note");
        Assert.True(surface!.Focus(), "premise: the reading view refused the keys");
        host.Settle();
        var provider = Assert.IsAssignableFrom<ITextProvider>(
            UIElementAutomationPeer.CreatePeerForElement(surface).GetPattern(PatternInterface.Text));
        return (surface, provider);
    }

    /// <summary>NVDA's read: the caret's range expanded to a Line. It must be one visual line:
    /// no break inside its text, and every bounding rectangle on one row.</summary>
    private static void Check(ITextProvider provider, string where, List<string> wrong)
    {
        ITextRangeProvider line = provider.GetSelection()[0];
        line.ExpandToEnclosingUnit(TextUnit.Line);
        string text = line.GetText(-1).TrimEnd('\r', '\n');
        string shown = text.Replace("\r", "\\r").Replace("\n", "\\n");
        double[] rects = line.GetBoundingRectangles();
        int rows = Rows(rects);
        if (text.Contains('\r') || text.Contains('\n') || text.Contains('\u2028') || rows > 1)
        {
            wrong.Add($"{where}: \"{shown}\" on {rows} rows");
        }
    }

    /// <summary>How many visual rows a range's rectangles (x, y, w, h quads) cover: a rect
    /// whose top is at or below an earlier row's bottom starts a new row.</summary>
    private static int Rows(double[] rects)
    {
        var bottoms = new List<double>();
        for (int index = 0; index + 3 < rects.Length; index += 4)
        {
            double top = rects[index + 1];
            double bottom = top + rects[index + 3];
            if (!bottoms.Any(row => top < row - 0.5))
            {
                bottoms.Add(bottom);
            }
        }

        return bottoms.Count;
    }

    private static IEnumerable<Paragraph> Paragraphs(FlowDocument document) =>
        document.Blocks.SelectMany(Paragraphs);

    private static IEnumerable<Paragraph> Paragraphs(Block block) => block switch
    {
        Paragraph paragraph => [paragraph],
        List list => list.ListItems.SelectMany(item => item.Blocks.SelectMany(Paragraphs)),
        Section section => section.Blocks.SelectMany(Paragraphs),
        _ => [],
    };

    private static string Text(Paragraph paragraph) =>
        new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Trim();

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(90), "STA test body timed out.");
}
