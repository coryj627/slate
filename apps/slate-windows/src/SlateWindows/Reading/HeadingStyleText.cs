// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Windows.Automation.Provider;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Windows.Documents;

namespace SlateWindows.Reading;

/// <summary>
/// A forwarding decorator over WPF's own text provider that answers the
/// UIA <c>StyleId</c> text attribute for heading paragraphs.
///
/// Why it exists: NVDA speaks heading level during LINEAR reading from
/// the `StyleId` text attribute — `AutomationProperties.HeadingLevel`
/// feeds Narrator but not NVDA's line reading, and WPF's
/// `TextRangeAdaptor` registers ~30 text attributes with no `StyleId`
/// among them. Measured across three manual passes: down-arrow onto a
/// heading reads only its text. This decorator forwards everything to
/// the base provider and answers two extra questions (StyleId,
/// StyleName) with range-aware Mixed semantics and synthetic
/// FindAttribute (adversarial round 1: the UIA range contract, not
/// just the caret).
///
/// The two known hazards, both handled:
/// - Base methods taking another range (`CompareEndpoints`,
///   `MoveEndpointByRange`) cast their argument to WPF's internal
///   adaptor type, so wrapped arguments must be UNWRAPPED first.
/// - Mapping a range to its paragraph requires the range's start
///   `TextPointer`, which the adaptor holds in an internal field. That
///   read is by reflection, resolved once and cached; if the field is
///   ever renamed the decorator degrades to exact base behavior —
///   headings lose their level announcement again, and the pinned test
///   fails loudly so the regression is a build break, not a silent
///   accessibility loss.
/// </summary>
internal sealed class HeadingStyleTextProvider : ITextProvider
{
    /// <summary>UIA_StyleIdAttributeId.</summary>
    internal const int StyleIdAttribute = 40034;

    /// <summary>StyleId_Heading1; levels 1–9 are contiguous.</summary>
    internal const int StyleIdHeading1 = 70001;

    /// <summary>StyleId_Quote. NVDA-source-verified UNCONSUMED (field
    /// pass 3, 2026-07-31, nvaccess/nvda@5ba9521: StyleId maps only
    /// Heading1—9); kept for non-NVDA ATs that do read it. NVDA users
    /// get quotes through <see cref="StyleNameAttribute"/>.</summary>
    internal const int StyleIdQuote = 70014;

    /// <summary>UIA_StyleNameAttributeId — NVDA's "report style"
    /// channel (speaks "style Quote"; the setting is OFF by default and
    /// the owner accepted that: no visible in-range prefix, zero visual
    /// change, quotes silent until Report Style is enabled. Owner call,
    /// field pass 3 2026-07-31).</summary>
    internal const int StyleNameAttribute = 40033;

    /// <summary>The style name answered for quote paragraphs.</summary>
    internal const string QuoteStyleName = "Quote";

    private readonly ITextProvider _inner;

    public HeadingStyleTextProvider(ITextProvider inner)
    {
        _inner = inner;
    }

    public ITextRangeProvider DocumentRange => Wrap(_inner.DocumentRange);

    public SupportedTextSelection SupportedTextSelection => _inner.SupportedTextSelection;

    public ITextRangeProvider[] GetSelection() => WrapAll(_inner.GetSelection());

    public ITextRangeProvider[] GetVisibleRanges() => WrapAll(_inner.GetVisibleRanges());

    public ITextRangeProvider RangeFromChild(IRawElementProviderSimple childElement) =>
        Wrap(_inner.RangeFromChild(childElement));

    public ITextRangeProvider RangeFromPoint(System.Windows.Point point) =>
        Wrap(_inner.RangeFromPoint(point));

    private ITextRangeProvider[] WrapAll(ITextRangeProvider[]? ranges)
    {
        if (ranges is null)
        {
            return Array.Empty<ITextRangeProvider>();
        }
        var wrapped = new ITextRangeProvider[ranges.Length];
        for (int i = 0; i < ranges.Length; i++)
        {
            wrapped[i] = Wrap(ranges[i]);
        }
        return wrapped;
    }

    private static ITextRangeProvider Wrap(ITextRangeProvider? range) =>
        range is null ? null! : new HeadingStyleTextRange(range);
}

/// <summary>One wrapped range. Forwards everything; answers StyleId.</summary>
internal sealed class HeadingStyleTextRange : ITextRangeProvider
{
    private readonly ITextRangeProvider _inner;

    public HeadingStyleTextRange(ITextRangeProvider inner)
    {
        _inner = inner;
    }

    internal ITextRangeProvider Inner => _inner;

    public object? GetAttributeValue(int attributeId)
    {
        if (attributeId is HeadingStyleTextProvider.StyleIdAttribute
            or HeadingStyleTextProvider.StyleNameAttribute)
        {
            return SyntheticAttributeValue(attributeId);
        }
        return _inner.GetAttributeValue(attributeId);
    }

    /// <summary>
    /// Range-aware synthetic evaluation (adversarial round 1): UIA
    /// requires MixedAttributeValue when a range spans differing
    /// values — paragraph-at-start alone made the answer depend on
    /// which end of a selection came first. When NO paragraph in the
    /// range carries a synthetic value the base provider keeps its
    /// answer, preserving pre-decorator behavior for plain text.
    /// </summary>
    private object? SyntheticAttributeValue(int attributeId)
    {
        // Mixed detection runs on style IDENTITY (the StyleId-level
        // value: heading level, quote, or none), not on the emitted
        // attribute (adversarial round 4): headings emit no synthetic
        // StyleName, so an H1+body or H1+H2 range compared by emitted
        // StyleName looked uniformly empty and delegated to WPF's
        // NotSupported — UIA requires Mixed for BOTH style attributes
        // whenever the style changes across the range.
        object? firstIdentity = null;
        bool haveFirst = false;
        foreach (Paragraph paragraph in ParagraphQuery.Of(_inner)?.Paragraphs() ?? [])
        {
            object? identity = SyntheticValueOf(
                paragraph, HeadingStyleTextProvider.StyleIdAttribute);
            if (!haveFirst)
            {
                firstIdentity = identity;
                haveFirst = true;
                continue;
            }
            if (!Equals(firstIdentity, identity))
            {
                return System.Windows.Automation.TextPattern.MixedAttributeValue;
            }
        }
        if (firstIdentity is null)
        {
            return _inner.GetAttributeValue(attributeId);
        }
        if (attributeId == HeadingStyleTextProvider.StyleIdAttribute)
        {
            return firstIdentity;
        }
        // Uniform identity, StyleName requested: only quotes carry a
        // synthetic name (headings deliberately don't — NVDA would
        // double-speak "style Heading 1" + "heading level 1" under
        // report-style); uniform heading ranges delegate.
        return Equals(firstIdentity, HeadingStyleTextProvider.StyleIdQuote)
            ? HeadingStyleTextProvider.QuoteStyleName
            : _inner.GetAttributeValue(attributeId);
    }

    /// <summary>The synthetic value a single paragraph contributes to
    /// <paramref name="attributeId"/>, or null when it has none.</summary>
    private static object? SyntheticValueOf(Paragraph paragraph, int attributeId)
    {
        if (attributeId == HeadingStyleTextProvider.StyleIdAttribute)
        {
            if (ReadingSemantics.HeadingLevelOf(paragraph) is byte level and > 0)
            {
                return HeadingStyleTextProvider.StyleIdHeading1 + (level - 1);
            }
            return ReadingSemantics.IsQuote(paragraph)
                ? HeadingStyleTextProvider.StyleIdQuote
                : null;
        }
        return ReadingSemantics.IsQuote(paragraph)
            ? HeadingStyleTextProvider.QuoteStyleName
            : null;
    }

    /// <summary>
    /// One query's paragraph view, resolved once on PRIVATE clones of the
    /// caller's range.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ENDPOINTS: WPF normalizes a range only inside some calls — never on
    /// construction, Clone, MoveEndpointByRange or DocumentRange — so the
    /// raw endpoints a client holds depend on what it called before. Every
    /// decision here reads WPF's own normalization of a clone instead, so a
    /// client that never normalized gets the answer of one that did, and the
    /// caller's range keeps its endpoints. NVDA normalizes first; Narrator,
    /// JAWS and automation clients need not.
    /// </para>
    /// <para>
    /// COST: WPF resolves the first paragraph and the endpoints once; the
    /// walk then moves forward from that paragraph in document order, so a
    /// query pays for the element edges between the paragraphs it touches,
    /// never for the blocks before them. #1320's block walk began at the
    /// outermost block, so a line deep inside one large list, table or
    /// section walked every entry before it, and say-all, which queries
    /// every line, was quadratic. There is no count ceiling: the finite
    /// block tree is the bound.
    /// </para>
    /// </remarks>
    private sealed class ParagraphQuery
    {
        private readonly TextPointer _start;
        private readonly TextPointer _end;
        private readonly ITextRangeProvider _firstRange;
        private readonly Paragraph? _first;

        private ParagraphQuery(
            ITextRangeProvider range,
            TextPointer start,
            TextPointer end,
            ITextRangeProvider firstRange)
        {
            Range = range;
            _start = start;
            _end = end;
            _firstRange = firstRange;
            _first = StartPointerOf(firstRange)?.Paragraph;
        }

        /// <summary>The caller's range, normalized: what found ranges are
        /// clamped to.</summary>
        internal ITextRangeProvider Range { get; }

        /// <summary>Empty by UIA definition, after normalization.</summary>
        internal bool IsDegenerate => _start.CompareTo(_end) == 0;

        /// <summary>Null when the adaptor's start pointer is unreadable; the
        /// decorator then keeps WPF's own answers.</summary>
        internal static ParagraphQuery? Of(ITextRangeProvider range)
        {
            ITextRangeProvider query = range.Clone();
            // WPF's CompareEndpoints normalizes both operands in place: here
            // only the private clone, never the caller's range.
            _ = query.CompareEndpoints(
                TextPatternRangeEndpoint.Start, query, TextPatternRangeEndpoint.Start);
            ITextRangeProvider endProbe = query.Clone();
            endProbe.MoveEndpointByRange(
                TextPatternRangeEndpoint.Start, endProbe, TextPatternRangeEndpoint.End);
            if (StartPointerOf(query) is not { } start
                || StartPointerOf(endProbe) is not { } end)
            {
                return null;
            }
            // Later WPF calls move an adaptor's pointers in place. Keep
            // immutable public copies of the boundaries for the walk.
            return new(
                query,
                start.GetPositionAtOffset(0, start.LogicalDirection)!,
                end.GetPositionAtOffset(0, end.LogicalDirection)!,
                ParagraphAt(query));
        }

        /// <summary>
        /// Paragraphs overlapping the range, in document order. A caret
        /// contributes its enclosing paragraph, and the end is exclusive for
        /// later ones.
        /// </summary>
        internal IEnumerable<Paragraph> Paragraphs()
        {
            if (IsDegenerate)
            {
                if (_first is not null)
                {
                    yield return _first;
                }
                yield break;
            }
            Paragraph? endParagraph = _end.Paragraph;
            for (Paragraph? paragraph = _first ?? _start.Paragraph ?? ParagraphFrom(_start);
                paragraph is not null;
                paragraph = ParagraphFrom(paragraph.ElementEnd))
            {
                // Endpoints are insertion positions. A Paragraph.ContentStart
                // can precede its first Run's opening tag, even when the query
                // ends at that first character; such a paragraph contributes
                // no content.
                if (paragraph.ContentStart.CompareTo(_end) >= 0
                    || ReferenceEquals(paragraph, endParagraph)
                        && paragraph.ContentStart.GetInsertionPosition(
                            LogicalDirection.Forward).CompareTo(_end) >= 0)
                {
                    yield break;
                }
                yield return paragraph;
            }
        }

        /// <summary>Build just the selected result using WPF's own paragraph
        /// expansion and clamping. Reposition a private clone through the same
        /// cached start field already used for reading; clone again before any
        /// public operation so WPF owns mutable copies of both pointers. Never
        /// write an endpoint of the query range or a document-owned pointer.</summary>
        internal ITextRangeProvider? RangeOf(Paragraph paragraph)
        {
            // WPF's first expansion is sensitive to the query's position and
            // logical direction, including a paragraph-end newline. Reuse that
            // exact expansion when the first paragraph is the match.
            if (ReferenceEquals(_first, paragraph))
            {
                return ClampToQuery(_firstRange);
            }
            ITextRangeProvider repositioned = Range.Clone();
            try
            {
                if (StartPointerField.ForType(repositioned.GetType()) is not { } field)
                {
                    return null;
                }
                field.SetValue(repositioned, paragraph.ContentStart);
            }
            catch
            {
                // The private adaptor shape is an optional compatibility seam.
                // Pinned real-WPF facts detect a change; UIA callers never receive
                // a reflection exception.
                return null;
            }
            return ClampToQuery(ParagraphAt(repositioned));
        }

        /// <summary>FindAttribute results must stay inside the searched
        /// range; a paragraph can begin before it or end after it.</summary>
        private ITextRangeProvider ClampToQuery(ITextRangeProvider candidate)
        {
            if (candidate.CompareEndpoints(
                TextPatternRangeEndpoint.Start, Range, TextPatternRangeEndpoint.Start) < 0)
            {
                candidate.MoveEndpointByRange(
                    TextPatternRangeEndpoint.Start, Range, TextPatternRangeEndpoint.Start);
            }
            if (candidate.CompareEndpoints(
                TextPatternRangeEndpoint.End, Range, TextPatternRangeEndpoint.End) > 0)
            {
                candidate.MoveEndpointByRange(
                    TextPatternRangeEndpoint.End, Range, TextPatternRangeEndpoint.End);
            }
            return candidate;
        }

        /// <summary>WPF's paragraph expansion of a range's start, on a clone:
        /// the expansion normalizes and moves the adaptor's pointers.</summary>
        private static ITextRangeProvider ParagraphAt(ITextRangeProvider range)
        {
            ITextRangeProvider paragraph = range.Clone();
            paragraph.MoveEndpointByRange(
                TextPatternRangeEndpoint.End, paragraph, TextPatternRangeEndpoint.Start);
            paragraph.ExpandToEnclosingUnit(TextUnit.Paragraph);
            return paragraph;
        }

        /// <summary>
        /// The first paragraph whose element starts at or after
        /// <paramref name="position"/>, in document order. Steps into the
        /// containers the reading view builds — sections, lists and their
        /// items, tables and their row groups, rows and cells — and over every
        /// other element whole, so a paragraph's inline content is never
        /// walked. Every step moves strictly forward.
        /// </summary>
        private static Paragraph? ParagraphFrom(TextPointer position)
        {
            for (TextPointer? cursor = position; cursor is not null;)
            {
                TextPointerContext context = cursor.GetPointerContext(LogicalDirection.Forward);
                if (context == TextPointerContext.None)
                {
                    return null;
                }
                if (context != TextPointerContext.ElementStart)
                {
                    cursor = cursor.GetNextContextPosition(LogicalDirection.Forward);
                    continue;
                }
                switch (cursor.GetAdjacentElement(LogicalDirection.Forward))
                {
                    case Paragraph paragraph:
                        return paragraph;
                    case TextElement container when container is Section
                        or System.Windows.Documents.List or ListItem
                        or Table or TableRowGroup or TableRow or TableCell:
                        cursor = container.ContentStart;
                        break;
                    case TextElement other:
                        cursor = other.ElementEnd;
                        break;
                    default:
                        cursor = cursor.GetNextContextPosition(LogicalDirection.Forward);
                        break;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// The adaptor's internal start pointer, read by reflection as the
    /// provider summary records. Any failure — field missing, unexpected
    /// type — degrades to "no style" rather than throwing into UIA
    /// marshalling.
    /// </summary>
    private static TextPointer? StartPointerOf(ITextRangeProvider range)
    {
        try
        {
            if (StartPointerField.ForType(range.GetType()) is not { } field
                || field.GetValue(range) is not TextPointer start)
            {
                return null;
            }
            return start;
        }
        catch
        {
            return null;
        }
    }

    // --- pure forwarding, with unwrap where the base casts ------------

    public ITextRangeProvider Clone() => new HeadingStyleTextRange(_inner.Clone());

    public bool Compare(ITextRangeProvider range) => _inner.Compare(Unwrap(range));

    public int CompareEndpoints(
        TextPatternRangeEndpoint endpoint,
        ITextRangeProvider targetRange,
        TextPatternRangeEndpoint targetEndpoint) =>
        _inner.CompareEndpoints(endpoint, Unwrap(targetRange), targetEndpoint);

    public void ExpandToEnclosingUnit(TextUnit unit) => _inner.ExpandToEnclosingUnit(unit);

    public ITextRangeProvider? FindAttribute(int attribute, object value, bool backward)
    {
        if (attribute is HeadingStyleTextProvider.StyleIdAttribute
            or HeadingStyleTextProvider.StyleNameAttribute)
        {
            // Synthetic values are invisible to WPF's own search
            // (adversarial round 1): without this branch, "find
            // StyleName Quote" answered nothing while GetAttributeValue
            // advertised the value.
            //
            // A DEGENERATE range is empty by UIA definition and must
            // answer null (adversarial round 3): the paragraph walk
            // deliberately yields the caret's paragraph so
            // GetAttributeValue works at a caret, but a search over
            // empty content has nothing to find — clamping would
            // otherwise return the caret itself as a zero-length
            // "match" an AT can rediscover forever.
            if (ParagraphQuery.Of(_inner) is not { IsDegenerate: false } query)
            {
                return null;
            }
            Paragraph? match = null;
            foreach (Paragraph paragraph in query.Paragraphs())
            {
                if (!Equals(SyntheticValueOf(paragraph, attribute), value))
                {
                    continue;
                }
                match = paragraph;
                if (!backward)
                {
                    break;
                }
            }
            ITextRangeProvider? found = match is null ? null : query.RangeOf(match);
            return found is null ? null : new HeadingStyleTextRange(found);
        }
        ITextRangeProvider? foundInner = _inner.FindAttribute(attribute, value, backward);
        return foundInner is null ? null : new HeadingStyleTextRange(foundInner);
    }

    public ITextRangeProvider? FindText(string text, bool backward, bool ignoreCase)
    {
        ITextRangeProvider? found = _inner.FindText(text, backward, ignoreCase);
        return found is null ? null : new HeadingStyleTextRange(found);
    }

    public double[] GetBoundingRectangles() => _inner.GetBoundingRectangles();

    public IRawElementProviderSimple GetEnclosingElement() => _inner.GetEnclosingElement();

    public string GetText(int maxLength) => _inner.GetText(maxLength);

    public int Move(TextUnit unit, int count) => _inner.Move(unit, count);

    public int MoveEndpointByUnit(
        TextPatternRangeEndpoint endpoint, TextUnit unit, int count) =>
        _inner.MoveEndpointByUnit(endpoint, unit, count);

    public void MoveEndpointByRange(
        TextPatternRangeEndpoint endpoint,
        ITextRangeProvider targetRange,
        TextPatternRangeEndpoint targetEndpoint) =>
        _inner.MoveEndpointByRange(endpoint, Unwrap(targetRange), targetEndpoint);

    public void Select() => _inner.Select();

    public void AddToSelection() => _inner.AddToSelection();

    public void RemoveFromSelection() => _inner.RemoveFromSelection();

    public void ScrollIntoView(bool alignToTop) => _inner.ScrollIntoView(alignToTop);

    public IRawElementProviderSimple[] GetChildren() => _inner.GetChildren();

    private static ITextRangeProvider Unwrap(ITextRangeProvider range) =>
        range is HeadingStyleTextRange wrapped ? wrapped.Inner : range;
}

/// <summary>
/// The internal start-pointer field of WPF's text-range adaptor,
/// resolved once per concrete type. Null when the shape is not what we
/// expect — the caller then reports "not a heading".
/// </summary>
internal static class StartPointerField
{
    private static Type? _cachedType;
    private static FieldInfo? _cachedField;

    public static FieldInfo? ForType(Type type)
    {
        if (!ReferenceEquals(type, _cachedType))
        {
            _cachedField = Resolve(type);
            _cachedType = type;
        }
        return _cachedField;
    }

    private static FieldInfo? Resolve(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            FieldInfo? field = current.GetField(
                "_start", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null && typeof(TextPointer).IsAssignableFrom(field.FieldType)
                || field is not null && field.FieldType.Name == "ITextPointer")
            {
                return field;
            }
        }
        return null;
    }
}
