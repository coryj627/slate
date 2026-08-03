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
                "list" => new PropertyDraft.ListDraft(DecodeItems(root)),
                "tag_list" => new PropertyDraft.TagListDraft(DecodeItems(root)),
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

    private static List<string> DecodeItems(JsonElement root)
    {
        var items = new List<string>();
        if (root.ValueKind != JsonValueKind.Array)
        {
            items.Add(AsString(root));
            return items;
        }
        foreach (var element in root.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(KindTag, out _)
                && element.TryGetProperty("value", out var tagged))
            {
                items.Add(AsString(tagged));
            }
            else
            {
                items.Add(AsString(element));
            }
        }
        return items;
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
            l.Items.Select(PropertyValue (item) => new PropertyValue.Text(item)).ToArray()),
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
