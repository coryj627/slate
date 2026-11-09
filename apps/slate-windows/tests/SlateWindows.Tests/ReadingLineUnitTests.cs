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
    /// hard breaks, a wrapped paragraph, a table whose cells wrap unequally (and some are
    /// empty), a code block with a blank line and an embed whose lines are LineBreaks: its
    /// Line is one visual line, holds the caret, and lies inside the caret's paragraph; and
    /// NVDA's read of the line's last character (collapse to the end, back a character, expand
    /// to it) is that character, never the break after it.</summary>
    [Fact]
    public void EveryCaretReadsTheOneLineItIsOn() => RunSta(() =>
    {
        string words = string.Join(' ', Enumerable.Range(1, 40).Select(index => $"word{index}"));
        string cell = string.Join(' ', Enumerable.Range(1, 12).Select(index => $"cell{index}"));
        string table = $"| Left | Right |\n| --- | --- |\n| {cell} x y z | Cell two short |\n|  | neighbour |\n| alone |  |\n";
        using var host = new ShownShell(
            ("Every.md", Note + "\nFirst line  \nSecond line  \nThird line\n\n" + words + "\n\n" + table + "\n" + Fence + "\n![[Other]]\n"),
            ("Other.md", EmbeddedLines));
        host.Shell.ContentPaneBorder.Width = 320;
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "Every.md", "Gamma line");
        Assert.True(
            Paragraphs(surface.Document).Any(p => p.Inlines.OfType<LineBreak>().Any()),
            "premise: the embed's lines aren't LineBreaks inside one paragraph");
        Assert.True(
            Paragraphs(surface.Document).Any(p => Text(p).Contains('\n')),
            "premise: the hard breaks aren't newlines inside one paragraph");
        Assert.True(
            Paragraphs(surface.Document).Any(HasABlankLine),
            "premise: the code block's blank line isn't inside one paragraph");
        Paragraph left = Paragraphs(surface.Document).Single(p => Text(p).StartsWith("cell1 ", StringComparison.Ordinal));
        Assert.True(
            left.ContentEnd.GetLineStartPosition(0) is { } lastLine && lastLine.CompareTo(left.ContentStart) > 0,
            "premise: the table's left cell doesn't wrap");
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

                // A visual line lies inside one paragraph (or one table cell's): its text is
                // part of the caret's paragraph, never a neighbour's.
                string lineText = line.GetText(-1).TrimEnd('\r', '\n');
                if (at.Paragraph is { } own
                    && !new TextRange(own.ContentStart, own.ContentEnd).Text.Contains(lineText, StringComparison.Ordinal))
                {
                    wrong.Add($"{where}: \"{lineText.Replace("\t", "\\t")}\" reaches past its paragraph");
                }

                if (lineText.Length > 0)
                {
                    ITextRangeProvider last = line.Clone();
                    last.MoveEndpointByRange(TextPatternRangeEndpoint.Start, last, TextPatternRangeEndpoint.End);
                    last.Move(TextUnit.Character, -1);
                    last.ExpandToEnclosingUnit(TextUnit.Character);
                    string character = last.GetText(-1);
                    if (character != lineText[^1].ToString())
                    {
                        wrong.Add($"{where}: \"{lineText}\" ends on \"{character.Replace("\r", "\\r").Replace("\n", "\\n")}\"");
                    }
                }

                checkedCarets++;
            }
        }

        Assert.True(checkedCarets > 100, $"premise: only {checkedCarets} carets walked");
        Assert.True(wrong.Count == 0, $"{wrong.Count} of {checkedCarets} carets read wrong:\n" + string.Join('\n', wrong.Take(30)));
    });

    /// <summary>A line cut where it ends (a soft wrap, a hard break's newline, a LineBreak)
    /// moves as WPF's own expansion of that line does: the caret at the line's end and the
    /// caret at its start give the same line, and each endpoint and Line move from it lands
    /// in the same place, reporting the same count.</summary>
    [Theory]
    [InlineData("wrap")]
    [InlineData("newline")]
    [InlineData("LineBreak")]
    public void ALineCutWhereItEndsMovesAsWpfsOwnDoes(string end) => RunSta(() =>
    {
        string words = string.Join(' ', Enumerable.Range(1, 60).Select(index => $"word{index}"));
        (string Path, string Text)[] notes = end switch
        {
            "wrap" => [("Cut.md", $"# Cut\n\n{words}\n")],
            "newline" => [("Cut.md", "# Cut\n\nFirst line  \nSecond line  \nThird line  \nFourth line\n")],
            _ => [("Cut.md", "# Cut\n\n![[Other]]\n"), ("Other.md", EmbeddedLines)],
        };
        string first = end switch { "wrap" => "word1 ", "newline" => "First line", _ => "Alpha line" };
        using var host = new ShownShell(notes);
        host.Shell.ContentPaneBorder.Width = 320;
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "Cut.md", end == "wrap" ? "word60" : end == "newline" ? "Fourth line" : "Gamma line");
        Paragraph long_ = Paragraphs(surface.Document).Single(p => Text(p).StartsWith(first, StringComparison.Ordinal));
        Assert.True(
            long_.ContentStart.GetLineStartPosition(2) is { } third && third.Paragraph == long_,
            $"premise: the {end} paragraph doesn't run to three lines");
        Assert.True(
            end != "LineBreak" || long_.Inlines.OfType<LineBreak>().Any(),
            "premise: the embed's lines aren't LineBreaks");

        ITextRangeProvider LineAt(Action place)
        {
            surface.CaretPosition = long_.ContentStart;
            host.Settle();
            EditingCommands.MoveDownByLine.Execute(null, surface);
            host.Settle();
            place();
            host.Settle();
            ITextRangeProvider line = provider.GetSelection()[0];
            line.ExpandToEnclosingUnit(TextUnit.Line);
            return line;
        }

        var wrong = new List<string>();
        var moves = new (string Name, Func<ITextRangeProvider, int> Move)[]
        {
            ("End back a line", range => range.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Line, -1)),
            ("End on a line", range => range.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Line, 1)),
            ("Start back a line", range => range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Line, -1)),
            ("back a line", range => range.Move(TextUnit.Line, -1)),
            ("on a line", range => range.Move(TextUnit.Line, 1)),
        };
        foreach ((string name, Func<ITextRangeProvider, int> move) in moves)
        {
            ITextRangeProvider fromEnd = LineAt(() => EditingCommands.MoveToLineEnd.Execute(null, surface));
            ITextRangeProvider fromStart = LineAt(() => EditingCommands.MoveToLineStart.Execute(null, surface));
            Assert.Equal(fromStart.GetText(-1), fromEnd.GetText(-1));
            int movedFromEnd = move(fromEnd);
            int movedFromStart = move(fromStart);
            if (movedFromEnd != movedFromStart || fromEnd.GetText(-1) != fromStart.GetText(-1))
            {
                wrong.Add($"{name}: from the end moved {movedFromEnd} to \"{fromEnd.GetText(-1)}\"; from the start {movedFromStart} to \"{fromStart.GetText(-1)}\"");
            }
        }

        Assert.True(wrong.Count == 0, "The cut line moved unlike WPF's:\n" + string.Join('\n', wrong));
    });

    /// <summary>Down and Up through a code block with a blank line read each line once: the
    /// blank line reads as blank, not as the line after it.</summary>
    [Fact]
    public void ABlankLineReadsAsItselfFromTheKeys() => RunSta(() =>
    {
        using var host = new ShownShell(("Fence.md", "# Fence\n\n" + Fence));
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "Fence.md", "gamma");
        Paragraph code = Paragraphs(surface.Document).Single(HasABlankLine);
        var read = new List<string>();
        var wrong = new List<string>();
        surface.CaretPosition = code.ContentStart;
        host.Settle();
        for (int press = 0; press < 3; press++)
        {
            read.Add(LineText(provider));
            Check(provider, $"Down {press}", wrong);
            EditingCommands.MoveDownByLine.Execute(null, surface);
            host.Settle();
        }

        for (int press = 0; press < 2; press++)
        {
            EditingCommands.MoveUpByLine.Execute(null, surface);
            host.Settle();
            read.Add(LineText(provider));
            Check(provider, $"Up {press}", wrong);
        }

        Assert.True(wrong.Count == 0, "Lines read as more than one line:\n" + string.Join('\n', wrong));
        Assert.Equal(["alpha", "", "gamma", "", "alpha"], read);
    });

    /// <summary>A caret just after a hard break (a newline, a blank line's newline, a
    /// LineBreak, or in a code block a lone carriage return, a line or a paragraph separator)
    /// starts the next line, whichever way it leans: its Line is that line.</summary>
    [Theory]
    [InlineData("newline", "Second line")]
    [InlineData("blank line", "gamma")]
    [InlineData("LineBreak", "Beta line")]
    [InlineData("carriage return", "Second line")]
    [InlineData("line separator", "Second line")]
    [InlineData("paragraph separator", "Second line")]
    public void ACaretAfterABreakReadsTheLineItStarts(string after, string expected) => RunSta(() =>
    {
        (string Path, string Text)[] notes = after switch
        {
            "newline" => [("Cut.md", "# Cut\n\nFirst line  \nSecond line\n")],
            "blank line" => [("Cut.md", "# Cut\n\n" + Fence)],
            "carriage return" => [("Cut.md", "# Cut\n\n```\nFirst line\rSecond line\n```\n")],
            "line separator" => [("Cut.md", "# Cut\n\n```\nFirst line\u2028Second line\n```\n")],
            "paragraph separator" => [("Cut.md", "# Cut\n\n```\nFirst line\u2029Second line\n```\n")],
            _ => [("Cut.md", "# Cut\n\n![[Other]]\n"), ("Other.md", EmbeddedLines)],
        };
        using var host = new ShownShell(notes);
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "Cut.md", expected);
        TextPointer? start = null;
        for (TextPointer? at = surface.Document.ContentStart; at is not null && start is null; at = at.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (at.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text
                && at.GetTextInRun(LogicalDirection.Forward) is { } text
                && text.IndexOf(expected, StringComparison.Ordinal) is int index and >= 0)
            {
                start = at.GetPositionAtOffset(index);
            }
        }

        Assert.True(start is not null, $"premise: no \"{expected}\" in the reading view");
        var wrong = new List<string>();
        foreach (LogicalDirection lean in new[] { LogicalDirection.Backward, LogicalDirection.Forward })
        {
            TextPointer caret = start!.GetPositionAtOffset(0, lean);
            surface.Selection.Select(caret, caret);
            surface.CaretPosition = caret;
            host.Settle();
            string read = LineText(provider);
            if (read != expected)
            {
                wrong.Add($"leaning {lean}: \"{read}\"");
            }
        }

        Assert.True(wrong.Count == 0, $"The caret starting \"{expected}\" read another line:\n" + string.Join('\n', wrong));
    });

    /// <summary>From a blank line's Line, the moves land as on any line: on a line is the line
    /// after it, back a line the line before, and End back a line empties it.</summary>
    [Fact]
    public void ABlankLineMovesAsAnyLineDoes() => RunSta(() =>
    {
        using var host = new ShownShell(("Fence.md", "# Fence\n\n" + Fence));
        (ReadingSurface surface, ITextProvider provider) = OpenReading(host, "Fence.md", "gamma");
        Paragraph code = Paragraphs(surface.Document).Single(HasABlankLine);

        ITextRangeProvider Blank()
        {
            surface.CaretPosition = code.ContentStart;
            host.Settle();
            EditingCommands.MoveDownByLine.Execute(null, surface);
            host.Settle();
            ITextRangeProvider line = provider.GetSelection()[0];
            line.ExpandToEnclosingUnit(TextUnit.Line);
            Assert.Equal(string.Empty, line.GetText(-1).Trim('\r', '\n'));
            return line;
        }

        string Read(ITextRangeProvider range)
        {
            range.ExpandToEnclosingUnit(TextUnit.Line);
            return range.GetText(-1).Trim('\r', '\n');
        }

        ITextRangeProvider on = Blank();
        Assert.Equal(1, on.Move(TextUnit.Line, 1));
        Assert.Equal("gamma", Read(on));
        ITextRangeProvider back = Blank();
        Assert.Equal(-1, back.Move(TextUnit.Line, -1));
        Assert.Equal("alpha", Read(back));
        ITextRangeProvider shrunk = Blank();
        Assert.Equal(-1, shrunk.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Line, -1));
        Assert.Equal(string.Empty, shrunk.GetText(-1));
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

    private const string Fence = "```\nalpha\n\ngamma\n```\n";

    private static bool HasABlankLine(Paragraph paragraph)
    {
        string text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Replace("\r\n", "\n");
        return text.Contains("alpha\n\ngamma", StringComparison.Ordinal);
    }

    private static string LineText(ITextProvider provider)
    {
        ITextRangeProvider line = provider.GetSelection()[0];
        line.ExpandToEnclosingUnit(TextUnit.Line);
        return line.GetText(-1).Trim('\r', '\n');
    }

    private const string EmbeddedLines = "Alpha line\nBeta line\nGamma line\nDelta line\n";

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
        Table table => table.RowGroups.SelectMany(group => group.Rows).SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks.SelectMany(Paragraphs)),
        _ => [],
    };

    private static string Text(Paragraph paragraph) =>
        new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Trim();

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(90), "STA test body timed out.");
}
