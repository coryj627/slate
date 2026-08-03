// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Panels;

/// <summary>
/// W4-4 (#736): the draft union behind a property editor row.
/// Numeric drafts are STRING-backed so partial input ("-", "1.")
/// is typable; shape validation happens at commit, pre-core.
/// Drafts are value-cloneable so `CommittedBaseline` snapshots are
/// independent of live edits.
/// </summary>
internal abstract record PropertyDraft
{
    internal sealed record ScalarText(string Kind, string Value) : PropertyDraft;

    internal sealed record IntegerDraft(string Value) : PropertyDraft;

    internal sealed record FloatDraft(string Value) : PropertyDraft;

    internal sealed record BooleanDraft(bool Value) : PropertyDraft;

    internal sealed record WikilinkDraft(string Target) : PropertyDraft;

    internal sealed record ListDraft(List<string> Items) : PropertyDraft
    {
        public ListDraft Copy() => new(new List<string>(Items));

        public bool ValueEquals(ListDraft other) => Items.SequenceEqual(other.Items);
    }

    internal sealed record TagListDraft(List<string> Tags) : PropertyDraft
    {
        public TagListDraft Copy() => new(new List<string>(Tags));

        public bool ValueEquals(TagListDraft other) => Tags.SequenceEqual(other.Tags);
    }

    /// <summary>Structural equality that respects list CONTENTS
    /// (record equality on List&lt;string&gt; is reference-based).</summary>
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
