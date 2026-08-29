// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 PR C-unit, task T2: THE POPULATION — one loaded graph, whole
/// and immutable, with its indexes built once.
/// </summary>
/// <remarks>
/// <para>
/// The five-class table puts the rows, the warnings and preserved
/// count, the indexes, adjacency, and the last-activated identity here,
/// because all of them are functions of ONE load. A population is
/// replaced, never edited: a reload builds a new one off-thread and the
/// publication installs it in a single swap, so no consumer can observe
/// a graph halfway between two loads.
/// </para>
/// <para>
/// THE ADJACENCY MEMO IS EAGER, built in this constructor from the rows
/// — the placement rounds 3 and 4 both confirmed. The alternative the
/// design sanctions if the eager cost ever proves unacceptable is an
/// immutable memo-enriched SUCCESSOR published with completion
/// validation, never an in-place fill and never a shared sentinel. A
/// lazy fill would make this type mutable and would put two consumers
/// on one dictionary, which is a pair the mutation battery already
/// carries.
/// </para>
/// <para>
/// COPY, NEVER ALIAS — obligation I7's construction half, applied one
/// level deeper than task T1 needed to. The row sequences come through
/// <see cref="CanvasModelCopy"/>, which copies. But a core row is a
/// uniffi record carrying a <c>string[]</c> group path, and an array
/// the caller still holds is exactly I7's arrangement one field down —
/// so each row is re-materialised here with a group path of this
/// population's own. What that does NOT close is a consumer mutating
/// the array through a row it obtained from this population; making
/// that unrepresentable means an owned row type or an analyzer, and it
/// is obligation I7's structural half, owned by task T4. The boundary
/// is stated rather than assumed, and the battery pins the half that is
/// closed.
/// </para>
/// </remarks>
internal sealed class CanvasPopulation
{
    private readonly ImmutableDictionary<string, CanvasOutlineRow> _byId;
    private readonly ImmutableDictionary<string, string> _parentById;
    private readonly ImmutableDictionary<string, ImmutableArray<string>> _childrenById;

    internal CanvasPopulation(
        IEnumerable<CanvasOutlineRow>? outline,
        IEnumerable<CanvasTableRow>? table,
        IEnumerable<CanvasLoadWarning>? warnings,
        uint preservedCount,
        string? lastActivatedNode)
    {
        Outline = CanvasModelCopy.Ordered(outline?.Select(Own));
        Table = CanvasModelCopy.Ordered(table?.Select(Own));
        Warnings = CanvasModelCopy.Ordered(warnings);
        PreservedCount = preservedCount;
        LastActivatedNode = lastActivatedNode;

        ImmutableDictionary<string, CanvasOutlineRow>.Builder byId =
            ImmutableDictionary.CreateBuilder<string, CanvasOutlineRow>(
                StringComparer.Ordinal);
        ImmutableDictionary<string, string>.Builder parents =
            ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var children =
            new Dictionary<string, ImmutableArray<string>.Builder>(StringComparer.Ordinal);

        // One walk. Depth-indexed ancestry: the row at depth d-1 most
        // recently seen is the parent of a row at depth d, which is the
        // shape a flattened outline has by construction.
        var ancestry = new List<string>();
        foreach (CanvasOutlineRow row in Outline)
        {
            byId[row.NodeId] = row;

            var depth = (int)row.Depth;
            if (ancestry.Count > depth)
            {
                ancestry.RemoveRange(depth, ancestry.Count - depth);
            }

            if (depth > 0 && ancestry.Count == depth)
            {
                string parent = ancestry[depth - 1];
                parents[row.NodeId] = parent;
                if (!children.TryGetValue(parent, out ImmutableArray<string>.Builder? kids))
                {
                    kids = ImmutableArray.CreateBuilder<string>();
                    children[parent] = kids;
                }
                kids.Add(row.NodeId);
            }

            ancestry.Add(row.NodeId);
        }

        _byId = byId.ToImmutable();
        _parentById = parents.ToImmutable();
        _childrenById = children.ToImmutableDictionary(
            pair => pair.Key,
            pair => pair.Value.ToImmutable(),
            StringComparer.Ordinal);
    }

    /// <summary>The empty population — what a document holds before its
    /// first load lands. Not a shared sentinel installed into the slot:
    /// every publication that names a population names one built for
    /// it, which is obligation I5's rule reaching this type.</summary>
    internal static CanvasPopulation Empty() => new(null, null, null, 0, null);

    internal ImmutableArray<CanvasOutlineRow> Outline { get; }

    internal ImmutableArray<CanvasTableRow> Table { get; }

    internal ImmutableArray<CanvasLoadWarning> Warnings { get; }

    /// <summary>How many unsupported items the load preserved — an A4
    /// banner fact, and population-class because it counts THIS
    /// load.</summary>
    internal uint PreservedCount { get; }

    /// <summary>Population class, per the five-class table: the row
    /// whose activation opened a card belongs to the graph it was found
    /// in, so a reload does not carry it forward.</summary>
    internal string? LastActivatedNode { get; }

    internal int Count => Outline.Length;

    internal bool Contains(string nodeId) => _byId.ContainsKey(nodeId);

    internal CanvasOutlineRow? Row(string nodeId) =>
        _byId.TryGetValue(nodeId, out CanvasOutlineRow? row) ? row : null;

    internal string? Parent(string nodeId) =>
        _parentById.TryGetValue(nodeId, out string? parent) ? parent : null;

    internal ImmutableArray<string> Children(string nodeId) =>
        _childrenById.TryGetValue(nodeId, out ImmutableArray<string> kids) ? kids : [];

    /// <summary>
    /// Resolve a durable selection INTENT against this graph.
    /// </summary>
    /// <remarks>
    /// The rebase's arithmetic: an intent naming a node this population
    /// does not have resolves to nothing, and the intent itself is
    /// carried anyway, because a node that comes back on the next load
    /// should come back selected. Task T3 calls this at acceptance;
    /// T2 owns that it is right.
    /// </remarks>
    internal string? Resolve(string? intent) =>
        intent is not null && _byId.ContainsKey(intent) ? intent : null;

    /// <summary>The subset of a durable mark set this graph actually
    /// has. The rest stay intent — they are not lost, they are just not
    /// resolvable here.</summary>
    internal ImmutableHashSet<string> ResolveMarks(IEnumerable<string>? intents) =>
        CanvasModelCopy.Ids(intents?.Where(_byId.ContainsKey));

    /// <summary>
    /// A row of this population's own, so no array a caller still holds
    /// reaches a published snapshot.
    /// </summary>
    private static CanvasOutlineRow Own(CanvasOutlineRow row) =>
        row with { GroupPath = [.. row.GroupPath] };

    private static CanvasTableRow Own(CanvasTableRow row) =>
        row with { GroupPath = [.. row.GroupPath] };
}
