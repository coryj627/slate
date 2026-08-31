// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 PR C-unit, task T2: what a filter request has produced so far.
/// </summary>
/// <remarks>
/// Four branches, because §C's travelling row asks for a four-branch
/// summary and a failed answer that is distinguishable from an empty
/// one. An empty match and a match that could not run say different
/// things to a reader, and a single "no rows" state would have made
/// them the same sentence.
/// </remarks>
internal enum CanvasAnswerState
{
    /// <summary>No needle: the unit shows the whole population.</summary>
    Unfiltered,

    /// <summary>A needle is typed and its match has not landed. The
    /// unit publishes immediately in this state, which is what takes
    /// the match off the dispatcher without the surface waiting.</summary>
    Pending,

    /// <summary>The match landed. Its rows may be empty, and that is a
    /// different fact from the one below.</summary>
    Answered,

    /// <summary>The match could not run or could not be delivered.</summary>
    Failed,
}

/// <summary>
/// W6-1 PR C-unit, task T2: THE UNIT — one projection of one
/// population, and the finest class in the chain.
/// </summary>
/// <remarks>
/// <para>
/// A unit belongs to exactly one population and is replaced whenever
/// the filter request changes, which is why unit currency is the one
/// currency a QUERY does not validate: the counter-position that
/// survived four rounds is that the non-filter queries are functions of
/// the loaded model, so a filter change cannot invalidate a graph fact.
/// What unit currency governs is unit-SCOPED reads — the filtered rows,
/// their counts, the displayed ordinal, the resolved selection — and
/// unit-sourced effects.
/// </para>
/// <para>
/// The unit holds no population reference. It is published beside one,
/// and the chain nests, so a unit is current only while the population
/// it was projected from is: naming that population from here would put
/// the same fact in two places for a currency comparison to disagree
/// about.
/// </para>
/// </remarks>
internal sealed class CanvasProjectionUnit
{
    private CanvasProjectionUnit(
        CanvasRequestIdentity? request,
        string needle,
        CanvasAnswerState answer,
        ImmutableHashSet<string> matched,
        ImmutableArray<string> filteredOrder,
        string? resolvedSelection,
        bool narrowed)
    {
        Request = request;
        Needle = needle;
        Answer = answer;
        Matched = matched;
        FilteredOrder = filteredOrder;
        ResolvedSelection = resolvedSelection;
        Narrowed = narrowed;
    }

    /// <summary>The unfiltered projection of a population: every row
    /// shown, no request outstanding.</summary>
    internal static CanvasProjectionUnit Unfiltered(
        CanvasPopulation population, string? resolvedSelection = null)
    {
        ArgumentNullException.ThrowIfNull(population);
        return new(
            request: null,
            needle: string.Empty,
            answer: CanvasAnswerState.Unfiltered,
            matched: CanvasModelCopy.Ids(null),
            filteredOrder: CanvasModelCopy.Ordered(
                population.Outline.Select(row => row.NodeId)),
            resolvedSelection: resolvedSelection,
            narrowed: false);
    }

    /// <summary>A needle typed and its match not yet landed. The rows
    /// stay where they were — the design's "a pending unit publishes
    /// immediately" — so the surface does not blank while a match
    /// runs.</summary>
    internal CanvasProjectionUnit Pending(CanvasRequestIdentity request, string needle)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(needle);
        return new(
            request, needle, CanvasAnswerState.Pending,
            Matched, FilteredOrder, ResolvedSelection, Narrowed);
    }

    internal CanvasProjectionUnit Answered(
        CanvasPopulation population, IEnumerable<string>? matched)
    {
        ArgumentNullException.ThrowIfNull(population);
        ImmutableHashSet<string> ids = population.ResolveMarks(matched);
        return new(
            Request, Needle, CanvasAnswerState.Answered, ids,
            CanvasModelCopy.Ordered(
                population.Outline
                    .Select(row => row.NodeId)
                    .Where(ids.Contains)),
            ResolvedSelection is not null && ids.Contains(ResolvedSelection)
                ? ResolvedSelection
                : null,
            narrowed: true);
    }

    /// <summary>The failed answer KEEPS the rows it was showing — the
    /// coherent past lingers, exactly as a pending unit's does, because
    /// widening to the full canvas on a fault would show every card
    /// while the field still claims to filter (contract C10). What
    /// changes is the ANSWER STATE, the bit a surface needs to say the
    /// honest sentence — §C's travelling failed-answer bit, task
    /// T6.</summary>
    internal CanvasProjectionUnit Failed() => new(
        Request, Needle, CanvasAnswerState.Failed,
        Matched, FilteredOrder, ResolvedSelection, Narrowed);

    internal CanvasProjectionUnit WithResolvedSelection(string? nodeId) => new(
        Request, Needle, Answer, Matched, FilteredOrder, nodeId, Narrowed);

    /// <summary>Which request this unit's answer state belongs to.
    /// Null while unfiltered.</summary>
    internal CanvasRequestIdentity? Request { get; }

    internal string Needle { get; }

    internal CanvasAnswerState Answer { get; }

    internal ImmutableHashSet<string> Matched { get; }

    /// <summary>The visible rows, in population order — the ordering a
    /// reader arrows through, which is why the displayed ordinal below
    /// is a position in THIS and not in the graph.</summary>
    internal ImmutableArray<string> FilteredOrder { get; }

    internal int VisibleCount => FilteredOrder.Length;

    internal string? ResolvedSelection { get; }

    /// <summary>Whether this projection's rows came from a LANDED
    /// answer — set by the answer, carried through pending and failed
    /// successors, cleared by the unfiltered projection — so a surface
    /// reads the fact instead of reconstructing it from set sizes (the
    /// cleanup pass; the arithmetic misread a match-everything
    /// answer).</summary>
    internal bool Narrowed { get; }

    /// <summary>The DISPLAYED-unit ordinal: one-based, and zero when the
    /// node is not visible here. Distinct from core's sibling ordinal,
    /// which is a position in the model — the two disagreeing is the
    /// whole reason they are different classes.</summary>
    internal int DisplayedOrdinal(string nodeId) =>
        FilteredOrder.IndexOf(nodeId, 0, StringComparer.Ordinal) + 1;
}
