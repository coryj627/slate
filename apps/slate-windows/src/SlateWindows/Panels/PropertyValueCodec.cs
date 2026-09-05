// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Text.Json;
using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>
/// W4-4 (#736): the one place `Property.ValueJson` is decoded into
/// an editable draft and a draft is encoded back into the generated
/// `PropertyValue` union for writes — the mirror of mac's
/// PropertyValueDisplay.decode. Round-trip fidelity is a feature
/// contract: committing an unchanged draft must be value-preserving
/// (stored date/datetime strings survive verbatim, `number` never
/// flips integer/float, list and tag_list typing survives the
/// tagged-element encoding both directions).
/// </summary>
internal static class PropertyValueCodec
{
    /// <summary>The core's tagged list-element discriminator key —
    /// legacy untagged arrays of plain strings are also accepted.</summary>
    private const string KindTag = "slate.property-kind";

    public static PropertyDraft Decode(string kind, string valueJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(valueJson);
            var root = doc.RootElement;
            return kind switch
            {
                "boolean" when root.ValueKind is JsonValueKind.True or JsonValueKind.False =>
                    new PropertyDraft.BooleanDraft(root.GetBoolean()),
                "number" => DecodeNumber(root, valueJson),
                "date" => new PropertyDraft.ScalarText("date", AsString(root)),
                "datetime" => new PropertyDraft.ScalarText("datetime", AsString(root)),
                "wikilink" => new PropertyDraft.WikilinkDraft(AsString(root)),
                "list" => new PropertyDraft.ListDraft(DecodeElements(root)),
                "tag_list" => new PropertyDraft.TagListDraft(DecodeTagStrings(root)),
                _ => new PropertyDraft.ScalarText("text", AsString(root)),
            };
        }
        catch (JsonException)
        {
            // An undecodable stored value degrades to raw text —
            // honest editing beats a crash, and Save round-trips the
            // raw string through PropertyValue.Text.
            return new PropertyDraft.ScalarText("text", valueJson);
        }
    }

    private static PropertyDraft DecodeNumber(JsonElement root, string rawJson)
    {
        // Integer vs float is decided by PEEKING THE RAW JSON, not
        // by numeric range (mac parity): "1.0" is a float even
        // though it fits an integer.
        if (root.ValueKind == JsonValueKind.Number)
        {
            bool looksFloat = rawJson.Contains('.')
                || rawJson.Contains('e')
                || rawJson.Contains('E');
            if (!looksFloat && root.TryGetInt64(out long integer))
            {
                return new PropertyDraft.IntegerDraft(
                    integer.ToString(CultureInfo.InvariantCulture));
            }
            return new PropertyDraft.FloatDraft(rawJson.Trim());
        }
        return new PropertyDraft.ScalarText("text", AsString(root));
    }

    private static List<PropertyDraft.ListElementDraft> DecodeElements(JsonElement root)
    {
        var items = new List<PropertyDraft.ListElementDraft>();
        if (root.ValueKind != JsonValueKind.Array)
        {
            items.Add(new PropertyDraft.ListElementDraft(
                new PropertyValue.Text(AsString(root)), AsString(root), Edited: false));
            return items;
        }
        foreach (var element in root.EnumerateArray())
        {
            items.Add(DecodeElement(element));
        }
        return items;
    }

    /// <summary>Contract 10 (adversarial round 1): every element
    /// retains its decoded SOURCE value so untouched typed elements
    /// (tagged dates/wikilinks, JSON numbers/booleans) re-encode
    /// verbatim. Unknown tagged kinds degrade to Text — the
    /// version-skew type-loss risk is tracked with #1078.</summary>
    private static PropertyDraft.ListElementDraft DecodeElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(KindTag, out var kindTag)
            && element.TryGetProperty("value", out var tagged))
        {
            string text = AsString(tagged);
            PropertyValue source = kindTag.GetString() switch
            {
                "date" => new PropertyValue.Date(text),
                "datetime" => new PropertyValue.Datetime(text),
                "wikilink" => new PropertyValue.Wikilink(text),
                _ => new PropertyValue.Text(text),
            };
            return new PropertyDraft.ListElementDraft(source, text, Edited: false);
        }
        string display = AsString(element);
        PropertyValue decoded = element.ValueKind switch
        {
            JsonValueKind.True => new PropertyValue.Boolean(true),
            JsonValueKind.False => new PropertyValue.Boolean(false),
            JsonValueKind.Number when !LooksFloat(element.GetRawText())
                && element.TryGetInt64(out long integer) =>
                new PropertyValue.Integer(integer),
            JsonValueKind.Number => new PropertyValue.Float(element.GetDouble()),
            _ => new PropertyValue.Text(display),
        };
        return new PropertyDraft.ListElementDraft(decoded, display, Edited: false);
    }

    private static bool LooksFloat(string rawJson) =>
        rawJson.Contains('.') || rawJson.Contains('e') || rawJson.Contains('E');

    /// <summary>An EDITED element converts along its source kind when
    /// the new text still fits it (a re-typed date stays a date);
    /// otherwise it becomes Text — the sanctioned explicit
    /// conversion for user-touched elements.</summary>
    private static PropertyValue EncodeElement(PropertyDraft.ListElementDraft item)
    {
        if (!item.Edited && item.Source is not null)
        {
            return item.Source;
        }
        return item.Source switch
        {
            // Round-2 below-bar: an edited element keeps its kind
            // only when the new text still FITS it — an invalid date
            // or a bracketed/empty wikilink target degrades to Text
            // instead of handing core an unencodable value. (Core
            // classifies plain strings by shape on the next read, so
            // this is honest, not lossy.)
            PropertyValue.Date when DateOnly.TryParseExact(
                item.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out _) =>
                new PropertyValue.Date(item.Text),
            PropertyValue.Datetime when DateTimeOffset.TryParse(
                    item.Text, CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _)
                || DateTime.TryParseExact(
                    item.Text, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _) =>
                new PropertyValue.Datetime(item.Text),
            // A CR/LF target is rejected by core outright, so it
            // degrades like the other unfit edits (round-5
            // below-bar) rather than failing the whole list write.
            PropertyValue.Wikilink when item.Text.Trim().Length > 0
                && !item.Text.Contains("]]")
                && !item.Text.Contains('\n')
                && !item.Text.Contains('\r') =>
                new PropertyValue.Wikilink(item.Text),
            PropertyValue.Integer when long.TryParse(
                item.Text, System.Globalization.NumberStyles.Integer,
                CultureInfo.InvariantCulture, out long integer) =>
                new PropertyValue.Integer(integer),
            PropertyValue.Float when double.TryParse(
                item.Text, System.Globalization.NumberStyles.Float,
                CultureInfo.InvariantCulture, out double floating)
                && !double.IsNaN(floating) && !double.IsInfinity(floating) =>
                new PropertyValue.Float(floating),
            PropertyValue.Boolean when bool.TryParse(item.Text, out bool flag) =>
                new PropertyValue.Boolean(flag),
            _ => new PropertyValue.Text(item.Text),
        };
    }

    private static List<string> DecodeTagStrings(JsonElement root)
    {
        var tags = new List<string>();
        if (root.ValueKind != JsonValueKind.Array)
        {
            tags.Add(AsString(root));
            return tags;
        }
        foreach (var element in root.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(KindTag, out _)
                && element.TryGetProperty("value", out var tagged))
            {
                tags.Add(AsString(tagged));
            }
            else
            {
                tags.Add(AsString(element));
            }
        }
        return tags;
    }

    private static string AsString(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? "",
        JsonValueKind.Null => "",
        _ => element.GetRawText(),
    };

    /// <summary>Encode a draft for the write path. Numeric drafts
    /// must already be validated (shape errors are pre-core).</summary>
    public static PropertyValue Encode(PropertyDraft draft) => draft switch
    {
        PropertyDraft.BooleanDraft b => new PropertyValue.Boolean(b.Value),
        PropertyDraft.IntegerDraft i => new PropertyValue.Integer(
            long.Parse(i.Value, CultureInfo.InvariantCulture)),
        PropertyDraft.FloatDraft f => new PropertyValue.Float(
            double.Parse(f.Value, CultureInfo.InvariantCulture)),
        PropertyDraft.WikilinkDraft w => new PropertyValue.Wikilink(w.Target),
        PropertyDraft.ListDraft l => new PropertyValue.List(
            l.Items.Select(EncodeElement).ToArray()),
        PropertyDraft.TagListDraft t => new PropertyValue.TagList(t.Tags.ToArray()),
        PropertyDraft.ScalarText s => s.Kind switch
        {
            "date" => new PropertyValue.Date(s.Value),
            "datetime" => new PropertyValue.Datetime(s.Value),
            _ => new PropertyValue.Text(s.Value),
        },
        _ => throw new InvalidOperationException($"unencodable draft {draft.GetType().Name}"),
    };
}
