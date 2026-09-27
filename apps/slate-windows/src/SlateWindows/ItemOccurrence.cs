// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows;

/// <summary>
/// W7-7 PR 3 (#1246, R-4; codex PR 3 round 6, owner decision OD-9): one
/// OCCURRENCE of an item in a list — the item, its place and its name —
/// with REFERENCE identity (sealed, no <c>Equals</c> override). WPF keys an
/// items host's automation peers by item, with the item's own equality, so
/// two value-equal records — two view definitions a base repeats — share ONE
/// peer: the list shows two, the reader reaches one. Wrapped, each is its own
/// item. Selection maps back by <see cref="Index"/>. (<see cref="SiblingText"/>
/// is the same identity for a repeatable string.) A sibling-rule host over an
/// item type with value equality binds occurrences, or its census pin states
/// why no two of its items are ever equal.
/// </summary>
internal sealed class ItemOccurrence<T>(int index, T item, string name)
{
    /// <summary>The item's place in the list it was taken from.</summary>
    public int Index { get; } = index;

    public T Item { get; } = item;

    /// <summary>The item's own name, verbatim — the sibling rule reads it
    /// and a template shows it.</summary>
    public string Name { get; } = name;

    /// <summary>What a row shows with no template, and what a name that
    /// fell back to the item would read: the name, never this type's or the
    /// item's dump.</summary>
    public override string ToString() => Name;
}

/// <summary>Builds <see cref="ItemOccurrence{T}"/> lists.</summary>
internal static class ItemOccurrence
{
    /// <summary>Each item of <paramref name="items"/> as its own occurrence,
    /// in order.</summary>
    internal static ItemOccurrence<T>[] Of<T>(IReadOnlyList<T> items, Func<T, string> name) =>
        [.. items.Select((item, index) => new ItemOccurrence<T>(index, item, name(item)))];
}
