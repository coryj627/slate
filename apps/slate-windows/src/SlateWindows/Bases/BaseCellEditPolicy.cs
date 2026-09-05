// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Bases;

/// <summary>
/// W4-6 (#738, contract C7): the mac BaseCellEditPolicy twin — pure
/// policy, no I/O. Which cells edit, why a cell refuses, what the
/// draft text is, and how a draft becomes a typed PropertyValue.
/// Refusal wording is core's (BasesCellReadOnly / BasesCellMustBe*);
/// the hint and the announcement render the SAME event so they can
/// never drift.
/// </summary>
internal static class BaseCellEditPolicy
{
    /// <summary>The single editability predicate: `note.`-prefixed ids
    /// edit their suffix key; any other dotted id (file.*, task.*,
    /// formula.*) is read-only; the role must be Metadata or Primary;
    /// else the bare id is the frontmatter key.</summary>
    public static string? PropertyKey(BasesColumn column)
    {
        if (column.Id.StartsWith("note.", StringComparison.Ordinal))
        {
            string key = column.Id["note.".Length..];
            return key.Length == 0 ? null : key;
        }
        if (column.Id.Contains('.', StringComparison.Ordinal))
        {
            return null;
        }
        if (column.Role is not (ColumnRole.Metadata or ColumnRole.Primary))
        {
            return null;
        }
        return column.Id.Length == 0 ? null : column.Id;
    }

    public static A11yEvent ReadOnlyEvent(BasesColumn column) =>
        new A11yEvent.BasesCellReadOnly(
            column.Id.StartsWith("file.", StringComparison.Ordinal));

    /// <summary>The same wording as a STATIC LABEL for the column's
    /// accessibility hint — never spoken unsolicited (the
    /// TaskStatusPhrase category).</summary>
    public static string ReadOnlyHint(BasesColumn column) =>
        SlateUniffiMethods.A11yRender(ReadOnlyEvent(column)).Text;

    public static string DraftText(BasesValue value) =>
        value.List.Length > 0 ? string.Join(", ", value.List) : value.Display;

    /// <summary>Draft → typed value, the mac arm-for-arm port. A null
    /// return carries the refusal event in <paramref name="refusal"/>;
    /// numeric/boolean/date arms parse the TRIMMED draft, text and the
    /// list kinds keep the raw draft (mac verbatim).</summary>
    public static PropertyValue? PropertyValueFor(
        string draft, string valueKind, out A11yEvent? refusal)
    {
        refusal = null;
        string trimmed = draft.Trim();
        switch (valueKind.ToLowerInvariant())
        {
            case "number":
                if (long.TryParse(
                    trimmed,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out long asInteger))
                {
                    return new PropertyValue.Integer(asInteger);
                }
                if (double.TryParse(
                        trimmed,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double asNumber)
                    && double.IsFinite(asNumber))
                {
                    return new PropertyValue.Float(asNumber);
                }
                refusal = new A11yEvent.BasesCellMustBeFiniteNumber();
                return null;
            case "integer":
                if (long.TryParse(
                    trimmed,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out long whole))
                {
                    return new PropertyValue.Integer(whole);
                }
                refusal = new A11yEvent.BasesCellMustBeWholeNumber();
                return null;
            case "float":
            case "decimal":
                if (double.TryParse(
                        trimmed,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double asFloat)
                    && double.IsFinite(asFloat))
                {
                    return new PropertyValue.Float(asFloat);
                }
                refusal = new A11yEvent.BasesCellMustBeFiniteDecimal();
                return null;
            case "boolean":
            case "bool":
            case "checkbox":
                switch (trimmed.ToLowerInvariant())
                {
                    case "true":
                    case "yes":
                    case "1":
                        return new PropertyValue.Boolean(true);
                    case "false":
                    case "no":
                    case "0":
                        return new PropertyValue.Boolean(false);
                    default:
                        refusal = new A11yEvent.BasesCellMustBeBoolean();
                        return null;
                }
            case "date":
                if (LooksLikeDate(trimmed))
                {
                    return new PropertyValue.Date(trimmed);
                }
                refusal = new A11yEvent.BasesCellMustBeDate();
                return null;
            case "datetime":
                return new PropertyValue.Datetime(trimmed);
            case "wikilink":
            case "link":
                return new PropertyValue.Wikilink(WikilinkTarget(trimmed));
            case "list":
                return new PropertyValue.List(
                    SplitListDraft(draft)
                        .Select(item => (PropertyValue)new PropertyValue.Text(item))
                        .ToArray());
            case "tag_list":
                return new PropertyValue.TagList(SplitListDraft(draft).ToArray());
            default:
                return new PropertyValue.Text(draft);
        }
    }

    /// <summary>The save-announcement rendering (mac displayValue):
    /// float via %g, lists joined ", ".</summary>
    public static string DisplayValue(PropertyValue value) => value switch
    {
        PropertyValue.Text text => text.Value,
        PropertyValue.Date date => date.Value,
        PropertyValue.Datetime datetime => datetime.Value,
        PropertyValue.Integer integer =>
            integer.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        PropertyValue.Float floating =>
            floating.Value.ToString("G6", System.Globalization.CultureInfo.InvariantCulture),
        PropertyValue.Boolean boolean => boolean.Value ? "true" : "false",
        PropertyValue.Wikilink wikilink => wikilink.Target,
        PropertyValue.List list => string.Join(", ", list.Items.Select(DisplayValue)),
        PropertyValue.TagList tags => string.Join(", ", tags.Tags),
        _ => string.Empty,
    };

    private static IEnumerable<string> SplitListDraft(string draft) =>
        draft.Split(',', '\n')
            .Select(item => item.Trim())
            .Where(item => item.Length > 0);

    private static bool LooksLikeDate(string value)
    {
        if (value.Length != 10)
        {
            return false;
        }
        string[] parts = value.Split('-');
        return parts.Length == 3
            && parts[0].Length == 4
            && parts[1].Length == 2
            && parts[2].Length == 2
            && parts.All(part => part.All(char.IsAsciiDigit));
    }

    private static string WikilinkTarget(string value) =>
        value.StartsWith("[[", StringComparison.Ordinal)
        && value.EndsWith("]]", StringComparison.Ordinal)
            ? value[2..^2]
            : value;
}
