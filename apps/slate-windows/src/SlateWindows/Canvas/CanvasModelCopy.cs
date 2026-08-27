// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 PR C-unit, task T1: the ONE way a collection enters a model
/// record — obligation I7's construction half.
/// </summary>
/// <remarks>
/// <para>
/// The design's deep-immutability rule says an owned immutable type
/// that accepts a collection must COPY it rather than alias the
/// caller's. Codex round 7 was right that asserting this establishes
/// nothing: a nominally trusted immutable collection can be built
/// over a caller-retained array through the immutable-collections
/// marshal, which yields a trusted type with a live external alias —
/// after which a published snapshot can mutate in place and take
/// rebase repeatability and decision stability with it.
/// </para>
/// <para>
/// So the construction sites funnel through here, and every method
/// below is a COPY by construction: the range factories walk the
/// source and build fresh storage. The marshal that produces an
/// aliasing immutable array is never called, and obligation I7's
/// structural half — task T4's analyzer, with its closed whitelist of
/// copying operations and its transitive constructor/factory walk —
/// is what will make "never called" a compile-time fact rather than a
/// convention. Until then the fact battery plants a caller-retained
/// collection, mutates it after construction, and asserts the
/// published snapshot did not move.
/// </para>
/// <para>
/// Null is accepted and means empty. A model record's collection is
/// never null, so callers do not branch and there is no second
/// spelling of "no marks" for a currency comparison to disagree
/// about.
/// </para>
/// </remarks>
internal static class CanvasModelCopy
{
    /// <summary>Ordinal, because node ids are byte-exact everywhere in
    /// this subsystem and a culture-sensitive set would silently merge
    /// two distinct ids.</summary>
    internal static ImmutableHashSet<string> Ids(IEnumerable<string>? source)
    {
        if (source is null)
        {
            return ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);
        }

        ImmutableHashSet<string>.Builder builder =
            ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (string id in source)
        {
            _ = builder.Add(id);
        }
        return builder.ToImmutable();
    }

    /// <summary>Order-preserving copy for the ordered model lists task
    /// T2 brings — rows, warnings, indexes. Present from T1 so the
    /// construction discipline exists before the types that need
    /// it.</summary>
    internal static ImmutableArray<T> Ordered<T>(IEnumerable<T>? source) =>
        source is null ? [] : ImmutableArray.CreateRange(source);
}
