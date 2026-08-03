// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>
/// W4-4 (#736), adversarial rounds 3–7: the ONE place the host asks
/// "what kind would this actually be once stored?".
///
/// Core classifies property values by KEY and by SHAPE, and every
/// host copy of those rules drifted — case-insensitive `tags`,
/// structural-vs-calendar dates, `#`-prefixed list elements, which
/// scalar shapes get quoted. Rounds 5 and 6 removed the mirror from
/// the add sheet; round 7 found the same mirror still living in row
/// validation. The rule is therefore a TYPE, not a convention: any
/// surface that needs core's verdict calls this, and no surface
/// re-derives it.
/// </summary>
internal static class PropertyKindAuthority
{
    /// <summary>The kind a value would have after a real write and
    /// authoritative re-read, or null when core will not store it at
    /// all (empty wikilink target, non-finite float, …).</summary>
    public static string? WouldStoreAs(string key, PropertyValue value) =>
        SlateUniffiMethods.RoundTripPropertyKind(key, value);

    /// <summary>True when storing <paramref name="value"/> under
    /// <paramref name="key"/> yields exactly <paramref name="kind"/>.
    /// Unstorable values are false.</summary>
    public static bool Preserves(string key, string kind, PropertyValue value) =>
        string.Equals(WouldStoreAs(key, value), kind, StringComparison.Ordinal);
}
