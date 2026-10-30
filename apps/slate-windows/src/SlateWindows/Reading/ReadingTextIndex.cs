// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using System.Windows;
using System.Windows.Documents;
using ICSharpCode.AvalonEdit.Document;

namespace SlateWindows.Reading;

/// <summary>
/// WB-22 (#1112): the reading document flattened for find. One forward walk
/// collects the text the surface's range holds — what caret reading and the
/// Text pattern reach — with one "\n" at each block edge and line break, so
/// no match spans two blocks, and nothing at inline edges, so a word split
/// across formatting runs is one word. Two things stand in for their range
/// text, each as one unit a match maps onto whole:
/// <list type="bullet">
/// <item>a math block is its TeX source, never the hidden speech run #1633
/// puts in its range (the owner's call of 2026-10-26: a match marks the
/// whole math element);</item>
/// <item>any other embedded element is one U+FFFC, so what it displays is
/// neither searched nor counted.</item>
/// </list>
/// The walk runs on the dispatcher (text pointers are dispatcher-bound),
/// once per content change.
/// </summary>
internal sealed class ReadingTextIndex
{
    private ReadingTextIndex(string text, List<Segment> segments, TextPointer start, TextPointer end)
    {
        Text = new StringTextSource(text);
        _segments = segments;
        _start = start;
        _end = end;
    }

    private readonly List<Segment> _segments;
    private readonly TextPointer _start;
    private readonly TextPointer _end;

    public ITextSource Text { get; }

    public static ReadingTextIndex Build(FlowDocument document)
    {
        var text = new StringBuilder();
        var segments = new List<Segment>();

        void Separate()
        {
            if (text.Length > 0 && text[^1] != '\n')
            {
                text.Append('\n');
            }
        }

        void AddUnit(TextPointer start, TextPointer end, object? child)
        {
            // A math block stands for its source; anything else embedded is
            // one object-replacement character.
            string standIn = child is ReadingMathElement { Source.Length: > 0 } math ? math.Source : "\uFFFC";
            segments.Add(new Segment(text.Length, standIn.Length, start, end, child as ReadingMathElement));
            text.Append(standIn);
        }

        TextPointer? pointer = document.ContentStart;
        while (pointer is not null)
        {
            switch (pointer.GetPointerContext(LogicalDirection.Forward))
            {
                case TextPointerContext.Text:
                    string run = pointer.GetTextInRun(LogicalDirection.Forward);
                    // A math paragraph's range holds its speech (or, with no
                    // core artifact, its fenced source): its element stands
                    // for it instead.
                    bool inMath = pointer.Paragraph is { } paragraph && ReadingSemantics.IsMathBlock(paragraph);
                    if (run.Length > 0 && !inMath)
                    {
                        segments.Add(new Segment(text.Length, run.Length, pointer, null, null));
                        text.Append(run);
                    }
                    break;
                case TextPointerContext.ElementStart:
                    switch (pointer.GetAdjacentElement(LogicalDirection.Forward))
                    {
                        case InlineUIContainer container:
                            AddUnit(container.ElementStart, container.ElementEnd, container.Child);
                            pointer = container.ElementEnd;
                            continue;
                        case BlockUIContainer block:
                            Separate();
                            AddUnit(block.ElementStart, block.ElementEnd, block.Child);
                            Separate();
                            pointer = block.ElementEnd;
                            continue;
                        case LineBreak:
                        case not Inline:
                            Separate();
                            break;
                    }
                    break;
                case TextPointerContext.ElementEnd:
                    if (pointer.GetAdjacentElement(LogicalDirection.Forward) is not Inline)
                    {
                        Separate();
                    }
                    break;
                case TextPointerContext.EmbeddedElement:
                    TextPointer after = pointer.GetNextContextPosition(LogicalDirection.Forward)!;
                    AddUnit(pointer, after, pointer.GetAdjacentElement(LogicalDirection.Forward));
                    pointer = after;
                    continue;
                case TextPointerContext.None:
                    pointer = null;
                    continue;
            }
            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
        }
        return new ReadingTextIndex(text.ToString(), segments, document.ContentStart, document.ContentEnd);
    }

    /// <summary>Where a match starting at <paramref name="offset"/> starts:
    /// inside a unit, the unit's start.</summary>
    public TextPointer StartOf(int offset)
    {
        // The first segment ending after the offset holds it, or follows the
        // separator it falls in.
        int index = FirstEndingAfter(offset);
        if (index == _segments.Count)
        {
            return _end;
        }
        Segment segment = _segments[index];
        return segment.End is null && offset > segment.FlatStart
            ? segment.Start.GetPositionAtOffset(offset - segment.FlatStart, LogicalDirection.Forward)!
            : segment.Start;
    }

    /// <summary>Where a match ending at <paramref name="offset"/> ends:
    /// inside a unit, the unit's end.</summary>
    public TextPointer EndOf(int offset)
    {
        // The last segment starting before the offset holds its last
        // character.
        int index = FirstEndingAfter(offset - 1);
        if (index == _segments.Count || _segments[index].FlatStart >= offset)
        {
            index--;
        }
        if (index < 0)
        {
            return _start;
        }
        Segment segment = _segments[index];
        return segment.End ?? segment.Start.GetPositionAtOffset(
            Math.Min(offset, segment.FlatEnd) - segment.FlatStart, LogicalDirection.Backward)!;
    }

    /// <summary>The flat offset at or before <paramref name="pointer"/>.</summary>
    public int OffsetOf(TextPointer pointer)
    {
        int low = 0;
        int high = _segments.Count - 1;
        int found = -1;
        while (low <= high)
        {
            int middle = (low + high) / 2;
            if (_segments[middle].Start.CompareTo(pointer) <= 0)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }
        if (found < 0)
        {
            return 0;
        }
        Segment segment = _segments[found];
        if (segment.End is { } end)
        {
            return pointer.CompareTo(end) >= 0 ? segment.FlatEnd : segment.FlatStart;
        }
        return segment.FlatStart + Math.Clamp(segment.Start.GetOffsetToPosition(pointer), 0, segment.Length);
    }

    /// <summary>The embedded element a match at <paramref name="offset"/>
    /// marks whole (a math element), or null for plain text.</summary>
    public UIElement? UnitAt(int offset)
    {
        int index = FirstEndingAfter(offset);
        return index < _segments.Count && _segments[index].FlatStart <= offset ? _segments[index].Element : null;
    }

    private int FirstEndingAfter(int offset)
    {
        int low = 0;
        int high = _segments.Count;
        while (low < high)
        {
            int middle = (low + high) / 2;
            if (_segments[middle].FlatEnd > offset)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }
        return low;
    }

    /// <summary>A text run (<see cref="End"/> null: its characters are its
    /// symbols) or a unit (an embedded element's whole range).</summary>
    private sealed record Segment(int FlatStart, int Length, TextPointer Start, TextPointer? End, UIElement? Element)
    {
        public int FlatEnd => FlatStart + Length;
    }
}
