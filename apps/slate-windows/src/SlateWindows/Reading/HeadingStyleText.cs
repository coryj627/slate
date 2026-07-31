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
        object? first = null;
        bool haveFirst = false;
        bool anySynthetic = false;
        foreach ((ITextRangeProvider _, Paragraph paragraph) in ParagraphRanges())
        {
            object? value = SyntheticValueOf(paragraph, attributeId);
            anySynthetic |= value is not null;
            if (!haveFirst)
            {
                first = value;
                haveFirst = true;
                continue;
            }
            if (!Equals(first, value))
            {
                return System.Windows.Automation.TextPattern.MixedAttributeValue;
            }
        }
        if (!anySynthetic)
        {
            return _inner.GetAttributeValue(attributeId);
        }
        return first;
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

    /// <summary>Walk cap: the paragraph walk must terminate even if a
    /// future adaptor's Move misbehaves on an exotic block.</summary>
    private const int MaximumSyntheticWalk = 10_000;

    /// <summary>
    /// Paragraph sub-ranges overlapping this range, in document order,
    /// built from PUBLIC range operations on fresh clones of the raw
    /// adaptor range (no new reflection surface). The cursor stays
    /// degenerate at paragraph starts; each yield expands a probe to
    /// the enclosing paragraph. Non-paragraph positions (container
    /// blocks) contribute nothing and the walk continues past them.
    /// </summary>
    private IEnumerable<(ITextRangeProvider Range, Paragraph Paragraph)> ParagraphRanges()
    {
        ITextRangeProvider cursor = _inner.Clone();
        cursor.MoveEndpointByRange(
            TextPatternRangeEndpoint.End, cursor, TextPatternRangeEndpoint.Start);
        for (int i = 0; i < MaximumSyntheticWalk; i++)
        {
            ITextRangeProvider probe = cursor.Clone();
            probe.ExpandToEnclosingUnit(TextUnit.Paragraph);
            if (ParagraphAtStartOf(probe) is { } paragraph)
            {
                yield return (probe, paragraph);
            }
            if (cursor.Move(TextUnit.Paragraph, 1) == 0)
            {
                yield break;
            }
            if (cursor.CompareEndpoints(
                TextPatternRangeEndpoint.Start,
                _inner,
                TextPatternRangeEndpoint.End) >= 0)
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// A range's paragraph, via the adaptor's internal start pointer.
    /// Any failure — field missing, unexpected type — degrades to "no
    /// style" rather than throwing into UIA marshalling.
    /// </summary>
    private static Paragraph? ParagraphAtStartOf(ITextRangeProvider range)
    {
        try
        {
            if (StartPointerField.ForType(range.GetType()) is not { } field
                || field.GetValue(range) is not TextPointer start)
            {
                return null;
            }
            return start.Paragraph;
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
            ITextRangeProvider? last = null;
            foreach ((ITextRangeProvider paragraphRange, Paragraph paragraph)
                in ParagraphRanges())
            {
                if (!Equals(SyntheticValueOf(paragraph, attribute), value))
                {
                    continue;
                }
                ITextRangeProvider found = ClampToThisRange(paragraphRange);
                if (!backward)
                {
                    return new HeadingStyleTextRange(found);
                }
                last = found;
            }
            return last is null ? null : new HeadingStyleTextRange(last);
        }
        ITextRangeProvider? foundInner = _inner.FindAttribute(attribute, value, backward);
        return foundInner is null ? null : new HeadingStyleTextRange(foundInner);
    }

    /// <summary>FindAttribute results must stay inside the searched
    /// range; a paragraph can begin before it or end after it.</summary>
    private ITextRangeProvider ClampToThisRange(ITextRangeProvider candidate)
    {
        if (candidate.CompareEndpoints(
            TextPatternRangeEndpoint.Start, _inner, TextPatternRangeEndpoint.Start) < 0)
        {
            candidate.MoveEndpointByRange(
                TextPatternRangeEndpoint.Start, _inner, TextPatternRangeEndpoint.Start);
        }
        if (candidate.CompareEndpoints(
            TextPatternRangeEndpoint.End, _inner, TextPatternRangeEndpoint.End) > 0)
        {
            candidate.MoveEndpointByRange(
                TextPatternRangeEndpoint.End, _inner, TextPatternRangeEndpoint.End);
        }
        return candidate;
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
