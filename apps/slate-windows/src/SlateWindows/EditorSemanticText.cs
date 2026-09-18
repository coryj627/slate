// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation.Text;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>W7 E-1/E-2: native text operations, canonical semantic attributes.</summary>
internal sealed class EditorSemanticTextProvider : ITextProvider
{
    private readonly ITextProvider _inner;
    private readonly SlateTextEditor _editor;
    private readonly AvalonDocumentBufferSession _session;
    private readonly Func<IRawElementProviderSimple> _enclosingElement;
    internal EditorHyperlinkTree Links { get; }

    internal EditorSemanticTextProvider(ITextProvider inner, SlateTextEditor editor,
        AvalonDocumentBufferSession session, SlateTextEditorAutomationPeer peer, Func<IRawElementProviderSimple> enclosingElement)
    {
        _inner = inner;
        _editor = editor;
        _session = session;
        _enclosingElement = enclosingElement;
        Links = new(editor, this, session, peer);
    }

    internal bool HasCurrentDocument
    {
        get
        {
            Debug.Assert(_editor.Dispatcher.CheckAccess());
            _editor.Dispatcher.VerifyAccess();
            return ReferenceEquals(_editor.HighlightSession, _session)
                && ReferenceEquals(_editor.Document, _session.Document) && !_session.IsDisposed;
        }
    }

    internal bool CanRead => HasCurrentDocument && _session.SemanticReadsAvailable && !_editor.IsComposing;

    internal void VerifyCurrentDocument()
    {
        if (!HasCurrentDocument) { throw new ElementNotAvailableException("The editor document has been replaced or disposed."); }
    }

    internal int Length => _session.Document.TextLength;
    internal IRawElementProviderSimple EnclosingElement => _enclosingElement();
    internal EditorHighlightWindow Inspect(int start, int end) => _session.InspectInRange(start, end);
    internal void Track(ITextRangeProvider range) => AvalonTextRangeAccess.Track(range, _session.Document);
    internal EditorSemanticTextRange Wrap(ITextRangeProvider range)
    {
        Track(range);
        return new(range, this);
    }
    internal EditorSemanticTextRange Range(int start, int end)
    {
        VerifyCurrentDocument();
        return Wrap(AvalonTextRangeAccess.Create(_editor.TextArea, _session.Document, start, end - start));
    }

    public ITextRangeProvider DocumentRange
    {
        get { VerifyCurrentDocument(); return Wrap(_inner.DocumentRange); }
    }
    public SupportedTextSelection SupportedTextSelection => _inner.SupportedTextSelection;
    public ITextRangeProvider[] GetSelection()
    {
        VerifyCurrentDocument();
        return _inner.GetSelection().Select(Wrap).ToArray();
    }

    // AvalonEdit 6.3.1.120 throws for these three members (contract A-1).
    // Geometry remains AvalonEdit's, and every resulting range is native.
    public ITextRangeProvider[] GetVisibleRanges()
    {
        VerifyCurrentDocument();
        if (!_editor.IsVisible)
        {
            return [];
        }
        TextView view = _editor.TextArea.TextView;
        view.EnsureVisualLines();
        var ranges = new List<ITextRangeProvider>();
        foreach (VisualLine line in view.VisualLines)
        {
            double y = line.VisualTop;
            foreach (System.Windows.Media.TextFormatting.TextLine row in line.TextLines)
            {
                double bottom = y + row.Height;
                if (bottom > view.VerticalOffset && y < view.VerticalOffset + view.ActualHeight)
                {
                    double middle = (Math.Max(y, view.VerticalOffset)
                        + Math.Min(bottom, view.VerticalOffset + view.ActualHeight)) / 2;
                    int start = OffsetAt(view, new Point(view.HorizontalOffset, middle));
                    int end = OffsetAt(view, new Point(view.HorizontalOffset + view.ActualWidth, middle), includeClippedCharacter: true);
                    ranges.Add(Range(start, Math.Max(start, end)));
                }
                y = bottom;
            }
        }
        return ranges.ToArray();
    }

    public ITextRangeProvider RangeFromChild(IRawElementProviderSimple childElement)
    {
        VerifyCurrentDocument();
        return Links.RangeFromChild(childElement);
    }

    public ITextRangeProvider RangeFromPoint(Point screenLocation)
    {
        VerifyCurrentDocument();
        TextView view = _editor.TextArea.TextView;
        view.EnsureVisualLines();
        Point local = view.PointFromScreen(screenLocation);
        var documentPoint = new Point(
            Math.Clamp(local.X, 0, Math.Max(0, view.ActualWidth)) + view.HorizontalOffset,
            Math.Clamp(local.Y, 0, Math.Max(0, view.ActualHeight - 1)) + view.VerticalOffset);
        int offset = OffsetAt(view, documentPoint);
        return Range(offset, offset);
    }

    private int OffsetAt(TextView view, Point point, bool includeClippedCharacter = false)
    {
        if (view.GetPositionFloor(point) is not { } position) { return Length; }
        int offset = _session.Document.GetOffset(position.Location);
        if (includeClippedCharacter && offset < _session.Document.GetLineByNumber(position.Line).EndOffset
            && view.GetVisualPosition(position, VisualYPosition.LineTop).X < point.X)
        {
            return TextUtilities.GetNextCaretPosition(_session.Document, offset,
                System.Windows.Documents.LogicalDirection.Forward, CaretPositioningMode.Normal);
        }
        return offset;
    }
}

/// <summary>One pinned dependency adapter, never text scanning (contract A-2).</summary>
internal static class AvalonTextRangeAccess
{
    private static readonly Type RangeType = typeof(TextArea).Assembly.GetType(
        "ICSharpCode.AvalonEdit.Editing.TextRangeProvider", throwOnError: true)!;
    private static readonly FieldInfo Segment = RangeType.GetField("segment", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("AvalonEdit's text-range segment contract changed.");
    private static readonly ConstructorInfo Constructor = RangeType.GetConstructor(
        [typeof(TextArea), typeof(TextDocument), typeof(int), typeof(int)])
        ?? throw new InvalidOperationException("AvalonEdit's text-range constructor contract changed.");

    internal static (int Start, int End) Bounds(ITextRangeProvider range)
    {
        if (!RangeType.IsInstanceOfType(range) || Segment.GetValue(range) is not ISegment segment)
        {
            throw new InvalidOperationException("Expected the pinned AvalonEdit native text range.");
        }
        return (segment.Offset, segment.EndOffset);
    }

    // The offset constructor uses anchors, but GetSelection supplies a SimpleSegment.
    // Normalize that native segment once; movement/units/geometry remain AvalonEdit's.
    internal static void Track(ITextRangeProvider range, TextDocument document)
    {
        if (Segment.GetValue(range) is not AnchorSegment)
        {
            (int start, int end) = Bounds(range);
            Segment.SetValue(range, new AnchorSegment(document, start, end - start));
        }
    }

    internal static ITextRangeProvider Create(TextArea area, TextDocument document, int start, int length) =>
        (ITextRangeProvider)Constructor.Invoke([area, document, start, length]);
}

internal sealed class EditorSemanticTextRange : ITextRangeProvider
{
    internal const int StyleIdAttribute = 40034;
    internal const int StyleNameAttribute = 40033;
    internal const int LinkAttribute = 40035;
    internal const int IsItalicAttribute = 40014;
    internal const int FontWeightAttribute = 40007;
    internal const int StrikethroughStyleAttribute = 40026;

    private readonly ITextRangeProvider _inner;
    private readonly EditorSemanticTextProvider _provider;

    internal EditorSemanticTextRange(ITextRangeProvider inner, EditorSemanticTextProvider provider)
    {
        _inner = inner;
        _provider = provider;
    }

    internal (int Start, int End) Bounds => AvalonTextRangeAccess.Bounds(Native);
    private static ITextRangeProvider Unwrap(ITextRangeProvider range) =>
        range is EditorSemanticTextRange semantic ? semantic.Native : range;

    public object GetAttributeValue(int attributeId)
    {
        if (!_provider.CanRead || !Supported(attributeId))
        {
            return AutomationElement.NotSupported;
        }
        (int start, int end) = Bounds;
        if (start == end)
        {
            start = Math.Min(start, Math.Max(0, _provider.Length - 1));
            end = Math.Min(_provider.Length, start + 1);
        }
        EditorHighlightWindow window = _provider.Inspect(start, end);
        object? uniform = null;
        foreach (AttributeRun run in Runs(window.Spans, start, end, attributeId))
        {
            if (uniform is null)
            {
                uniform = run.Value;
            }
            else if (!Equals(uniform, run.Value))
            {
                return TextPattern.MixedAttributeValue;
            }
        }
        return uniform ?? AutomationElement.NotSupported;
    }

    public ITextRangeProvider? FindAttribute(int attributeId, object value, bool backward)
    {
        if (!_provider.CanRead || attributeId != StyleIdAttribute)
        {
            return null;
        }
        (int start, int end) = Bounds;
        EditorHighlightWindow window = _provider.Inspect(start, end);
        IEnumerable<AttributeRun> runs = Runs(window.Spans, start, end, attributeId);
        if (backward)
        {
            runs = runs.Reverse();
        }
        foreach (AttributeRun run in runs)
        {
            if (Equals(run.Value, value))
            {
                return _provider.Range(run.Start, run.End);
            }
        }
        return null;
    }

    private static bool Supported(int attributeId) => attributeId is StyleIdAttribute
        or StyleNameAttribute or IsItalicAttribute or FontWeightAttribute
        or StrikethroughStyleAttribute;

    // Table E-4: UIA idioms only. The kinds and intervals are canonical.
    internal static object Value(EditorSemanticSpan span, int attributeId) => (span.Kind, attributeId) switch
    {
        (EditorSpanKind.Heading heading, StyleIdAttribute) => 70000 + heading.Level,
        (EditorSpanKind.Heading heading, StyleNameAttribute) => $"Heading {heading.Level}",
        (EditorSpanKind.Wikilink, StyleNameAttribute) => "Wikilink",
        (EditorSpanKind.Link, StyleNameAttribute) => "Link",
        (EditorSpanKind.Embed, StyleNameAttribute) => "Embed",
        (EditorSpanKind.Image, StyleNameAttribute) => "Image",
        (EditorSpanKind.Tag, StyleNameAttribute) => "Tag",
        (EditorSpanKind.Citation, StyleNameAttribute) => "Citation",
        (EditorSpanKind.InlineCode or EditorSpanKind.CodeFence or EditorSpanKind.Code, StyleNameAttribute) => "Code",
        (EditorSpanKind.BlockQuote, StyleIdAttribute) => 70014,
        (EditorSpanKind.BlockQuote, StyleNameAttribute) => "Quote",
        (EditorSpanKind.Emphasis, IsItalicAttribute) => true,
        (EditorSpanKind.Strong, FontWeightAttribute) => 700,
        (EditorSpanKind.Strikethrough, StrikethroughStyleAttribute) => (int)TextDecorationLineStyle.Single,
        (EditorSpanKind.Comment, StyleNameAttribute) => "Comment",
        (EditorSpanKind.Frontmatter, StyleNameAttribute) => "Frontmatter",
        _ => AutomationElement.NotSupported,
    };

    private sealed record AttributeRun(int Start, int End, object Value);
    private sealed record Contribution(int Start, int End, int Length, int Index, object Value);

    /// <summary>Interval sweep handles plain gaps and nested canonical overlays.
    /// Values are merged into maximal runs; no per-character or document-text scan.</summary>
    private static IEnumerable<AttributeRun> Runs(IReadOnlyList<EditorSemanticSpan> spans, int start, int end, int attribute)
    {
        var boundaries = new SortedDictionary<int, List<(Contribution Span, bool Enter)>>();
        boundaries[start] = [];
        boundaries[end] = [];
        for (int index = 0; index < spans.Count; index++)
        {
            EditorSemanticSpan span = spans[index];
            int left = Math.Max(start, span.StartUtf16);
            int right = Math.Min(end, span.StartUtf16 + span.LengthUtf16);
            object value = Value(span, attribute);
            if (right <= left || ReferenceEquals(value, AutomationElement.NotSupported))
            {
                continue;
            }
            var contribution = new Contribution(left, right, span.LengthUtf16, index, value);
            Add(left, contribution, true);
            Add(right, contribution, false);
        }
        var active = new SortedSet<Contribution>(Comparer<Contribution>.Create((left, right) =>
        {
            int length = left.Length.CompareTo(right.Length);
            return length != 0 ? length : left.Index.CompareTo(right.Index);
        }));
        AttributeRun? pending = null;
        int position = start;
        foreach ((int offset, List<(Contribution Span, bool Enter)> changes) in boundaries)
        {
            if (offset > position)
            {
                object value = active.Count > 0 ? active.Min!.Value : AutomationElement.NotSupported;
                if (pending is not null && Equals(pending.Value, value))
                {
                    pending = pending with { End = offset };
                }
                else
                {
                    if (pending is not null)
                    {
                        yield return pending;
                    }
                    pending = new(position, offset, value);
                }
            }
            foreach ((Contribution span, bool enter) in changes)
            {
                if (enter) { active.Add(span); } else { active.Remove(span); }
            }
            position = offset;
        }
        if (pending is not null)
        {
            yield return pending;
        }

        void Add(int at, Contribution contribution, bool enter)
        {
            if (!boundaries.TryGetValue(at, out List<(Contribution, bool)>? changes))
            {
                boundaries[at] = changes = [];
            }
            changes.Add((contribution, enter));
        }
    }

    private ITextRangeProvider Native
    {
        get { _provider.VerifyCurrentDocument(); return _inner; }
    }
    public ITextRangeProvider Clone() => _provider.Wrap(Native.Clone());
    public bool Compare(ITextRangeProvider range) => Native.Compare(Unwrap(range));
    public int CompareEndpoints(TextPatternRangeEndpoint endpoint, ITextRangeProvider targetRange, TextPatternRangeEndpoint targetEndpoint) =>
        Native.CompareEndpoints(endpoint, Unwrap(targetRange), targetEndpoint);
    public void ExpandToEnclosingUnit(TextUnit unit) { Native.ExpandToEnclosingUnit(unit); _provider.Track(_inner); }
    public ITextRangeProvider? FindText(string text, bool backward, bool ignoreCase) =>
        Native.FindText(text, backward, ignoreCase) is { } found ? _provider.Wrap(found) : null;
    public double[] GetBoundingRectangles() => Native.GetBoundingRectangles();
    public IRawElementProviderSimple[] GetChildren() => _provider.Links.Children(Bounds.Start, Bounds.End);
    public IRawElementProviderSimple GetEnclosingElement() =>
        _provider.Links.Enclosing(Bounds.Start, Bounds.End)?.Provider ?? _provider.EnclosingElement;
    public string GetText(int maxLength) => Native.GetText(maxLength);
    public int Move(TextUnit unit, int count)
    {
        ITextRangeProvider native = Native;
        int direction = Math.Sign(count);
        int moved = 0;
        while (moved != count)
        {
            int before = AvalonTextRangeAccess.Bounds(native).Start;
            if (direction > 0 ? before == _provider.Length : before == 0) { break; }
            _ = native.Move(unit, direction);
            _provider.Track(native);
            int after = AvalonTextRangeAccess.Bounds(native).Start;
            if ((after - before) * direction <= 0) { break; }
            moved += direction;
        }
        return moved;
    }
    public void MoveEndpointByRange(TextPatternRangeEndpoint endpoint, ITextRangeProvider targetRange, TextPatternRangeEndpoint targetEndpoint)
    { Native.MoveEndpointByRange(endpoint, Unwrap(targetRange), targetEndpoint); _provider.Track(_inner); }
    public int MoveEndpointByUnit(TextPatternRangeEndpoint endpoint, TextUnit unit, int count)
    {
        ITextRangeProvider native = Native;
        int direction = Math.Sign(count);
        int moved = 0;
        while (moved != count)
        {
            int before = EndpointOffset();
            if (direction > 0 ? before == _provider.Length : before == 0) { break; }
            int originalStart = AvalonTextRangeAccess.Bounds(native).Start;
            _ = native.MoveEndpointByUnit(endpoint, unit, direction);
            int after = EndpointOffset();
            // A-9: the native final line/word has no following start boundary.
            // Its terminal boundary is the document end, even without LF.
            if (direction > 0 && after <= before
                && unit is TextUnit.Word or TextUnit.Format or TextUnit.Line or TextUnit.Paragraph)
            {
                native.MoveEndpointByRange(endpoint, Unwrap(_provider.DocumentRange), TextPatternRangeEndpoint.End);
                if (endpoint == TextPatternRangeEndpoint.End)
                {
                    // Native backward clamping may also have crossed the start.
                    native.MoveEndpointByRange(TextPatternRangeEndpoint.Start,
                        Unwrap(_provider.Range(originalStart, originalStart)), TextPatternRangeEndpoint.Start);
                }
                after = EndpointOffset();
            }
            _provider.Track(native);
            if ((after - before) * direction <= 0) { break; }
            moved += direction;
        }
        return moved;

        int EndpointOffset()
        {
            (int start, int end) = AvalonTextRangeAccess.Bounds(native);
            return endpoint == TextPatternRangeEndpoint.Start ? start : end;
        }
    }
    public void Select() => Native.Select();
    public void AddToSelection() => Native.AddToSelection();
    public void RemoveFromSelection() => Native.RemoveFromSelection();
    public void ScrollIntoView(bool alignToTop) => Native.ScrollIntoView(alignToTop);
}
