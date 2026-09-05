// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;
using Xunit;

namespace SlateWindows.Tests;

/// <summary>
/// W4-4 feature contract 10 (round-trip fidelity) and the picker
/// gate: the codec decodes stored value_json to drafts and encodes
/// drafts to PropertyValue without inventing, flipping, or losing
/// anything.
/// </summary>
public class PropertyValueCodecTests
{
    /// <summary>A tagged date element with the invisible U+F8FF
    /// discriminator prefix spelled as an escape.</summary>
    private const string TaggedWikilinkElement =
        "{\"slate.property-kind\":\"wikilink\",\"value\":\"basic\"}";

    private const string TaggedDateElement =
        "{\"slate.property-kind\":\"date\",\"value\":\"2026-04-01\"}";

    [Fact]
    public void NumberKindNeverFlipsIntegerAndFloat()
    {
        // Integer vs float is decided by peeking the RAW JSON.
        Assert.IsType<PropertyDraft.IntegerDraft>(
            PropertyValueCodec.Decode("number", "42"));
        Assert.IsType<PropertyDraft.FloatDraft>(
            PropertyValueCodec.Decode("number", "1.5"));
        Assert.IsType<PropertyDraft.FloatDraft>(
            PropertyValueCodec.Decode("number", "1.0"));
        Assert.IsType<PropertyDraft.FloatDraft>(
            PropertyValueCodec.Decode("number", "1e3"));

        Assert.IsType<PropertyValue.Integer>(
            PropertyValueCodec.Encode(new PropertyDraft.IntegerDraft("42")));
        Assert.IsType<PropertyValue.Float>(
            PropertyValueCodec.Encode(new PropertyDraft.FloatDraft("1.0")));
    }

    [Fact]
    public void DateAndDatetimeStringsSurviveVerbatim()
    {
        var date = Assert.IsType<PropertyDraft.ScalarText>(
            PropertyValueCodec.Decode("date", "\"2026-03-01\""));
        Assert.Equal("2026-03-01", date.Value);
        Assert.Equal("date", date.Kind);

        var zulu = Assert.IsType<PropertyDraft.ScalarText>(
            PropertyValueCodec.Decode("datetime", "\"2026-03-01T09:30:00Z\""));
        Assert.Equal("2026-03-01T09:30:00Z", zulu.Value);
        var naive = Assert.IsType<PropertyDraft.ScalarText>(
            PropertyValueCodec.Decode("datetime", "\"2026-03-01T09:30:00\""));
        Assert.Equal("2026-03-01T09:30:00", naive.Value);

        // The serialized FORM is preserved on encode: Z stays Z,
        // naive stays naive.
        var zEncoded = Assert.IsType<PropertyValue.Datetime>(
            PropertyValueCodec.Encode(zulu));
        Assert.Equal("2026-03-01T09:30:00Z", zEncoded.Value);
        var nEncoded = Assert.IsType<PropertyValue.Datetime>(
            PropertyValueCodec.Encode(naive));
        Assert.Equal("2026-03-01T09:30:00", nEncoded.Value);
    }

    [Fact]
    public void MalformedStoredDatesStayRawText()
    {
        // The properties_metadata fixture pins core keeping the raw
        // string under a date kind; the DRAFT keeps it verbatim and
        // the picker gate refuses (a date is never invented).
        var draft = Assert.IsType<PropertyDraft.ScalarText>(
            PropertyValueCodec.Decode("date", "\"2026-13-45\""));
        Assert.Equal("2026-13-45", draft.Value);
        Assert.False(PropertyRowViewModel.StoredValueTakesDatePicker("date", "\"2026-13-45\""));
        Assert.True(PropertyRowViewModel.StoredValueTakesDatePicker("date", "\"2026-03-01\""));
        Assert.True(PropertyRowViewModel.StoredValueTakesDatePicker(
            "datetime", "\"2026-03-01T09:30:00Z\""));
        Assert.True(PropertyRowViewModel.StoredValueTakesDatePicker(
            "datetime", "\"2026-03-01T09:30:00\""));
        Assert.False(PropertyRowViewModel.StoredValueTakesDatePicker(
            "datetime", "\"yesterday\""));
        Assert.False(PropertyRowViewModel.StoredValueTakesDatePicker("text", "\"2026-03-01\""));
    }

    [Fact]
    public void ListsAndTagListsRoundTripBothEncodings()
    {
        // Plain string arrays (the parse path)...
        var list = Assert.IsType<PropertyDraft.ListDraft>(
            PropertyValueCodec.Decode("list", "[\"a\",\"b\"]"));
        Assert.Equal(new[] { "a", "b" }, list.Items.Select(item => item.Text));

        // ...and tagged element objects (the DB round-trip path).
        var tagged = Assert.IsType<PropertyDraft.ListDraft>(
            PropertyValueCodec.Decode(
                "list",
                "[{\"slate.property-kind\":\"date\",\"value\":\"2026-04-01\"},\"plain\"]"));
        Assert.Equal(new[] { "2026-04-01", "plain" }, tagged.Items.Select(item => item.Text));

        var tags = Assert.IsType<PropertyDraft.TagListDraft>(
            PropertyValueCodec.Decode("tag_list", "[\"fixture\",\"properties\"]"));
        Assert.Equal(new[] { "fixture", "properties" }, tags.Tags);

        var encodedList = Assert.IsType<PropertyValue.List>(
            PropertyValueCodec.Encode(list));
        Assert.All(encodedList.Items, item => Assert.IsType<PropertyValue.Text>(item));
        var encodedTags = Assert.IsType<PropertyValue.TagList>(
            PropertyValueCodec.Encode(tags));
        Assert.Equal(new[] { "fixture", "properties" }, encodedTags.Tags);

        // Contract 10 (adversarial round 1): UNTOUCHED tagged and
        // numeric elements re-encode with their original types —
        // editing item 2 must not turn item 1's date into a string.
        var typed = Assert.IsType<PropertyDraft.ListDraft>(
            PropertyValueCodec.Decode(
                "list", "[" + TaggedDateElement + ",7,1.5,true,\"plain\"]"));
        typed.Items[4] = typed.Items[4] with { Text = "edited", Edited = true };
        var reEncoded = Assert.IsType<PropertyValue.List>(
            PropertyValueCodec.Encode(typed));
        Assert.Equal(new PropertyValue.Date("2026-04-01"), reEncoded.Items[0]);
        Assert.Equal(new PropertyValue.Integer(7), reEncoded.Items[1]);
        Assert.Equal(new PropertyValue.Float(1.5), reEncoded.Items[2]);
        Assert.Equal(new PropertyValue.Boolean(true), reEncoded.Items[3]);
        Assert.Equal(new PropertyValue.Text("edited"), reEncoded.Items[4]);

        // An EDITED typed element converts along its kind only when
        // the new text still FITS it (round-3 below-bar: the guards
        // must bite) — an invalid date or a bracketed wikilink
        // degrades to Text instead of handing core an unencodable
        // value.
        typed.Items[0] = typed.Items[0] with { Text = "2027-01-02", Edited = true };
        typed.Items[1] = typed.Items[1] with { Text = "not a number", Edited = true };
        var converted = Assert.IsType<PropertyValue.List>(
            PropertyValueCodec.Encode(typed));
        Assert.Equal(new PropertyValue.Date("2027-01-02"), converted.Items[0]);
        Assert.Equal(new PropertyValue.Text("not a number"), converted.Items[1]);

        typed.Items[0] = typed.Items[0] with { Text = "not a date", Edited = true };
        var degraded = Assert.IsType<PropertyValue.List>(
            PropertyValueCodec.Encode(typed));
        Assert.Equal(new PropertyValue.Text("not a date"), degraded.Items[0]);

        var link = Assert.IsType<PropertyDraft.ListDraft>(
            PropertyValueCodec.Decode(
                "list", "[" + TaggedWikilinkElement + "]"));
        // Prove the tagged decode reached the Wikilink branch — the
        // degradation below must exercise the real guard.
        Assert.IsType<PropertyValue.Wikilink>(link.Items[0].Source);
        link.Items[0] = link.Items[0] with { Text = "a]]b", Edited = true };
        Assert.Equal(
            new PropertyValue.Text("a]]b"),
            Assert.IsType<PropertyValue.List>(
                PropertyValueCodec.Encode(link)).Items[0]);
    }

    [Fact]
    public void UndecodableValuesDegradeToRawTextInsteadOfThrowing()
    {
        var draft = Assert.IsType<PropertyDraft.ScalarText>(
            PropertyValueCodec.Decode("list", "not json at all"));
        Assert.Equal("not json at all", draft.Value);
    }

    [Fact]
    public void RowValidationPinsTheShapeErrors()
    {
        var row = MakeRow("count", "number", "42");
        row.Draft = new PropertyDraft.IntegerDraft("not a number");
        Assert.False(row.ValidateForCommit());
        Assert.Equal(
            "Validation error: Must be a whole number.",
            row.ValidationLabel);

        row.Draft = new PropertyDraft.FloatDraft("NaN");
        Assert.False(row.ValidateForCommit());

        row.Draft = new PropertyDraft.IntegerDraft("41");
        Assert.True(row.ValidateForCommit());
        Assert.Null(row.ValidationError);

        var dateRow = MakeRow("due", "date", "\"2026-03-01\"");
        dateRow.Draft = new PropertyDraft.ScalarText("date", "2026-3-1");
        Assert.False(dateRow.ValidateForCommit());

        var linkRow = MakeRow("source", "wikilink", "\"basic\"");
        linkRow.Draft = new PropertyDraft.WikilinkDraft("");
        Assert.False(linkRow.ValidateForCommit());
        linkRow.Draft = new PropertyDraft.WikilinkDraft("a]]b");
        Assert.False(linkRow.ValidateForCommit());
    }

    [Fact]
    public void RevertRestoresTheBaselineAndAnnouncesOnce()
    {
        int reverts = 0;
        var row = new PropertyRowViewModel(
            new Property("aliases", "list", "[\"a\",\"b\"]"),
            "note.md",
            "hash-1",
            _ => { },
            _ => reverts++,
            _ => { });
        var draft = Assert.IsType<PropertyDraft.ListDraft>(row.Draft);
        draft.Items.Add(PropertyDraft.ListElementDraft.ForNew("c"));
        row.Draft = draft; // re-publish
        Assert.True(row.IsDirty);

        row.Revert();
        Assert.False(row.IsDirty);
        Assert.Equal(1, reverts);
        Assert.Equal(
            new[] { "a", "b" },
            Assert.IsType<PropertyDraft.ListDraft>(row.Draft)
                .Items.Select(item => item.Text));
        // The baseline was never aliased into the mutable draft.
        Assert.Equal(
            new[] { "a", "b" },
            Assert.IsType<PropertyDraft.ListDraft>(row.CommittedBaseline)
                .Items.Select(item => item.Text));
    }

    private static PropertyRowViewModel MakeRow(string key, string kind, string valueJson) =>
        new(new Property(key, kind, valueJson), "note.md", "hash-1",
            _ => { }, _ => { }, _ => { });
}
