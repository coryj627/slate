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
/// so each row is re-materialised, by the same helper so there stays
/// one construction site, with a group path of this population's own.
/// What that does NOT close is a consumer mutating
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
        string? lastActivatedNode,
        IEnumerable<CanvasSceneNode>? scene = null)
    {
        Outline = CanvasModelCopy.Rows(outline);
        Table = CanvasModelCopy.Rows(table);
        Warnings = CanvasModelCopy.Ordered(warnings);
        PreservedCount = preservedCount;
        LastActivatedNode = lastActivatedNode;
        Subpaths = CanvasModelCopy.Subpaths(scene);

        ImmutableDictionary<string, CanvasOutlineRow>.Builder byId =
            ImmutableDictionary.CreateBuilder<string, CanvasOutlineRow>(
                StringComparer.Ordinal);
        ImmutableDictionary<string, string>.Builder parents =
            ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var children =
            new Dictionary<string, ImmutableArray<string>.Builder>(StringComparer.Ordinal);

        // One walk, by the derivation the outline view already uses: a
        // stack of open ancestors, popped down to the nearest row
        // SHALLOWER than this one. Depth-indexed ancestry — "the slot
        // at depth-1 is the parent" — reads as the same thing and is
        // not: a depth gap leaves that slot holding a sibling, and the
        // T2 review showed two rows at depth 2 under a root attaching
        // to each other. Core's outline is gap-free today; this
        // constructor accepts any sequence, and must not build a wrong
        // tree quietly on the day that changes.
        var ancestry = new Stack<CanvasOutlineRow>();
        foreach (CanvasOutlineRow row in Outline)
        {
            byId[row.NodeId] = row;

            while (ancestry.Count > 0 && ancestry.Peek().Depth >= row.Depth)
            {
                _ = ancestry.Pop();
            }

            if (ancestry.Count > 0)
            {
                string parent = ancestry.Peek().NodeId;
                parents[row.NodeId] = parent;
                if (!children.TryGetValue(parent, out ImmutableArray<string>.Builder? kids))
                {
                    kids = ImmutableArray.CreateBuilder<string>();
                    children[parent] = kids;
                }
                kids.Add(row.NodeId);
            }

            ancestry.Push(row);
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

    /// <summary>The JSON Canvas subpath per file card that names one,
    /// from core's scene — population-class because it is a function
    /// of ONE load, and the only thing the scene contributes to the
    /// model: geometry is the renderer's and never enters here.</summary>
    internal ImmutableDictionary<string, string> Subpaths { get; }

    internal string? Subpath(string nodeId) =>
        Subpaths.TryGetValue(nodeId, out string? subpath) ? subpath : null;

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

}
