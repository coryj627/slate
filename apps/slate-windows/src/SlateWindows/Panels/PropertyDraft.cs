// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>
/// W4-4 (#736): the draft union behind a property editor row.
/// Numeric drafts are STRING-backed so partial input ("-", "1.")
/// is typable; shape validation happens at commit, pre-core.
/// Drafts are value-cloneable so `CommittedBaseline` snapshots are
/// independent of live edits.
///
/// List elements are TYPED (adversarial round 1, contract 10): each
/// element carries its original decoded `PropertyValue` so untouched
/// tagged elements (dates, wikilinks, numbers) re-encode verbatim —
/// editing item 2 must never turn item 1's date into a plain string.
/// </summary>
internal abstract record PropertyDraft
{
    internal sealed record ScalarText(string Kind, string Value) : PropertyDraft;

    internal sealed record IntegerDraft(string Value) : PropertyDraft;

    internal sealed record FloatDraft(string Value) : PropertyDraft;

    internal sealed record BooleanDraft(bool Value) : PropertyDraft;

    internal sealed record WikilinkDraft(string Target) : PropertyDraft;

    /// <summary>One list element: the decoded source value (null for
    /// user-added elements), its editable text, and whether the user
    /// touched it. Encode keeps untouched sources verbatim.</summary>
    internal sealed record ListElementDraft(PropertyValue? Source, string Text, bool Edited)
    {
        public static ListElementDraft ForNew(string text) => new(null, text, true);
    }

    internal sealed record ListDraft(List<ListElementDraft> Items) : PropertyDraft
    {
        public ListDraft Copy() => new([.. Items]);

        /// <summary>Equality ignores the transient Edited flag (round
        /// 2 below-bar): typing a value away and back restores a
        /// clean row — the flag only steers encode conversion, and a
        /// same-text conversion is value-identical.</summary>
        public bool ValueEquals(ListDraft other) =>
            Items.Count == other.Items.Count
            && Items.Zip(other.Items).All(pair =>
                pair.First.Text == pair.Second.Text
                && Equals(pair.First.Source, pair.Second.Source));
    }

    internal sealed record TagListDraft(List<string> Tags) : PropertyDraft
    {
        public TagListDraft Copy() => new(new List<string>(Tags));

        public bool ValueEquals(TagListDraft other) => Tags.SequenceEqual(other.Tags);
    }

    /// <summary>Structural equality that respects list CONTENTS
    /// (record equality on List&lt;T&gt; is reference-based).</summary>
    public static bool ValueEquals(PropertyDraft a, PropertyDraft b) => (a, b) switch
    {
        (ListDraft la, ListDraft lb) => la.ValueEquals(lb),
        (TagListDraft ta, TagListDraft tb) => ta.ValueEquals(tb),
        _ => a.Equals(b),
    };

    /// <summary>Deep copy for baselines. ("Clone" is a reserved
    /// member name in records — CS8859.)</summary>
    public static PropertyDraft Copy(PropertyDraft draft) => draft switch
    {
        ListDraft l => l.Copy(),
        TagListDraft t => t.Copy(),
        _ => draft,
    };
}
