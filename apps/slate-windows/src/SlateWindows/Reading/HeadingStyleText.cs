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
/// the base provider and answers exactly one extra question.
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

    /// <summary>StyleId_Quote — the same linear-reading channel, for
    /// block quotes (field, 2026-07-30: quotes read as plain
    /// prose).</summary>
    internal const int StyleIdQuote = 70014;

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
        if (attributeId == HeadingStyleTextProvider.StyleIdAttribute
            && ParagraphAtStart() is { } paragraph)
        {
            if (ReadingSemantics.HeadingLevelOf(paragraph) is byte level and > 0)
            {
                return HeadingStyleTextProvider.StyleIdHeading1 + (level - 1);
            }
            if (ReadingSemantics.IsQuote(paragraph))
            {
                return HeadingStyleTextProvider.StyleIdQuote;
            }
        }
        return _inner.GetAttributeValue(attributeId);
    }

    /// <summary>
    /// The range's paragraph, via the adaptor's internal start pointer.
    /// Any failure — field missing, unexpected type — degrades to "no
    /// style" rather than throwing into UIA marshalling.
    /// </summary>
    private Paragraph? ParagraphAtStart()
    {
        try
        {
            if (StartPointerField.ForType(_inner.GetType()) is not { } field
                || field.GetValue(_inner) is not TextPointer start)
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
        ITextRangeProvider? found = _inner.FindAttribute(attribute, value, backward);
        return found is null ? null : new HeadingStyleTextRange(found);
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
