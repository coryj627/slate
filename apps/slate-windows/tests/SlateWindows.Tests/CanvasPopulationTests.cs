// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 PR C-unit, task T2: the population and the unit — the two
/// finer classes, their eager indexes, and obligation I7's
/// construction half one level below where task T1 needed it.
/// </summary>
public sealed class CanvasPopulationTests
{
    // ---------------------------------------------------------------
    // The population
    // ---------------------------------------------------------------

    /// <summary>The adjacency memo is built once, in the constructor,
    /// from the rows — the placement rounds 3 and 4 confirmed. Depth
    /// decides ancestry, because a flattened outline carries its tree
    /// in the depth column and nowhere else.</summary>
    [Fact]
    public void AdjacencyIsBuiltEagerlyFromTheRowsDepthColumn()
    {
        CanvasPopulation population = Outline(
            ("root", 0), ("a", 1), ("a1", 2), ("a2", 2), ("b", 1));

        Assert.True(
            population.Count == 5,
            $"premise: the population took {population.Count} of five rows.");

        Assert.True(population.Parent("root") is null, "a depth-zero row has no parent.");
        Assert.True(population.Parent("a") == "root", "depth one hangs off depth zero.");
        Assert.True(population.Parent("a1") == "a", "depth two hangs off the nearest depth one.");
        Assert.True(
            population.Parent("b") == "root",
            "and a later depth-one row re-parents to the root rather than to the "
            + "deeper row that preceded it — the case a naive last-row-seen walk "
            + "gets wrong.");

        Assert.True(
            population.Children("root").SequenceEqual(["a", "b"]),
            "children come back in population order.");
        Assert.True(
            population.Children("a").SequenceEqual(["a1", "a2"]),
            "and so do a deeper row's.");
        Assert.True(
            population.Children("a1").IsEmpty,
            "a leaf has no children, and asking is not an error.");
    }

    /// <summary>A row lookup answers from the eager index, and a node
    /// the graph does not have answers null rather than throwing —
    /// because "is this node here" is a question a stale intent asks
    /// every reload.</summary>
    [Fact]
    public void RowLookupAnswersFromTheIndex()
    {
        CanvasPopulation population = Outline(("root", 0), ("a", 1));

        Assert.True(population.Contains("a"), "premise: the node is in the graph.");
        Assert.True(
            population.Row("a")?.NodeId == "a",
            "the index returns the row it was built from.");
        Assert.True(
            population.Row("gone") is null && !population.Contains("gone"),
            "a node this graph does not have answers null, which is what makes a "
            + "stale selection intent resolvable rather than fatal.");
    }

    /// <summary>The rebase's arithmetic: an intent naming a missing node
    /// resolves to nothing and is still carried, because a node that
    /// comes back on a later load should come back selected.</summary>
    [Fact]
    public void ResolvingAnIntentKeepsWhatTheGraphHasAndDropsWhatItDoesNot()
    {
        CanvasPopulation population = Outline(("root", 0), ("a", 1));

        Assert.True(population.Resolve("a") == "a", "a present intent resolves to itself.");
        Assert.True(population.Resolve("gone") is null, "an absent intent resolves to nothing.");
        Assert.True(population.Resolve(null) is null, "and no intent resolves to nothing.");

        ImmutableHashSet<string> marks = population.ResolveMarks(["a", "gone", "root"]);
        Assert.True(
            marks.SetEquals(["a", "root"]),
            $"the resolved marks were [{string.Join(", ", marks)}]; the subset this "
            + "graph has is what resolves, and the rest stay INTENT rather than "
            + "being lost.");
    }

    /// <summary>
    /// Obligation I7 one level below task T1: a caller who keeps the
    /// row sequence, or the group-path array inside a row, cannot move
    /// a population that has already been built.
    /// </summary>
    [Fact]
    public void ACallerRetainedRowOrGroupPathCannotMoveTheBuiltPopulation()
    {
        var groupPath = new[] { "outer", "inner" };
        var rows = new List<CanvasOutlineRow>
        {
            new("root", 0, "text", "Root", "Root", groupPath, 1, 1, 0, null),
        };

        var population = new CanvasPopulation(rows, null, null, 0, null);
        Assert.True(
            population.Outline[0].GroupPath.SequenceEqual(["outer", "inner"]),
            "premise: the population took the group path it was given.");

        // Both aliases the caller still holds.
        rows.Add(new("late", 0, "text", "Late", "Late", [], 1, 1, 0, null));
        groupPath[0] = "mutated";

        Assert.True(
            population.Count == 1,
            $"the population grew to {population.Count} rows when the caller "
            + "mutated the list it handed in.");
        Assert.True(
            population.Outline[0].GroupPath[0] == "outer",
            "the population's group path tracked the caller's array. A core row is "
            + "a record carrying a string[], so copying the row SEQUENCE is not "
            + "enough — obligation I7's arrangement one field down.");
    }

    /// <summary>The empty population is built per call, not shared. A
    /// publication-valued sentinel is exactly what obligation I5
    /// forbids, and a population reached through one would be the same
    /// defect a level down.</summary>
    [Fact]
    public void TheEmptyPopulationIsNotASharedSentinel()
    {
        CanvasPopulation first = CanvasPopulation.Empty();
        CanvasPopulation second = CanvasPopulation.Empty();

        Assert.True(
            first.Count == 0 && second.Count == 0,
            "premise: both are empty, or this is not the comparison it says.");
        Assert.False(
            ReferenceEquals(first, second),
            "the empty population is a shared instance; a reused reference is how "
            + "a stale compare-and-swap succeeds against a value that has come "
            + "round again.");
    }

    // ---------------------------------------------------------------
    // The unit
    // ---------------------------------------------------------------

    /// <summary>An unfiltered unit shows the whole population, in
    /// population order, and holds no request.</summary>
    [Fact]
    public void AnUnfilteredUnitProjectsTheWholePopulation()
    {
        CanvasPopulation population = Outline(("root", 0), ("a", 1), ("b", 1));
        CanvasProjectionUnit unit = CanvasProjectionUnit.Unfiltered(population);

        Assert.True(
            unit.Answer == CanvasAnswerState.Unfiltered && unit.Request is null,
            $"an unfiltered unit is {unit.Answer} with no request outstanding.");
        Assert.True(
            unit.FilteredOrder.SequenceEqual(["root", "a", "b"]),
            "and every row is visible, in population order.");
        Assert.True(unit.VisibleCount == 3, "with the count to match.");
    }

    /// <summary>A pending unit KEEPS the rows it had. Publishing
    /// immediately is what takes the match off the dispatcher, and
    /// blanking the surface while a match runs would be the cost that
    /// buys.</summary>
    [Fact]
    public void APendingUnitKeepsTheRowsItHad()
    {
        CanvasPopulation population = Outline(("root", 0), ("a", 1));
        var request = new CanvasRequestIdentity("F1");
        CanvasProjectionUnit pending = CanvasProjectionUnit
            .Unfiltered(population)
            .Pending(request, "roo");

        Assert.True(
            pending.Answer == CanvasAnswerState.Pending
                && ReferenceEquals(pending.Request, request)
                && pending.Needle == "roo",
            "the pending unit carries its request and needle.");
        Assert.True(
            pending.FilteredOrder.SequenceEqual(["root", "a"]),
            "and the rows stay where they were until the answer lands.");
    }

    /// <summary>An answered unit filters to the matched set, in
    /// POPULATION order rather than in the order the matches arrived —
    /// the order a reader arrows through has to be the graph's.</summary>
    [Fact]
    public void AnAnsweredUnitFiltersInPopulationOrder()
    {
        CanvasPopulation population = Outline(("root", 0), ("a", 1), ("b", 1));
        CanvasProjectionUnit answered = CanvasProjectionUnit
            .Unfiltered(population)
            .Pending(new CanvasRequestIdentity("F1"), "x")
            .Answered(population, ["b", "root"]);

        Assert.True(
            answered.Answer == CanvasAnswerState.Answered,
            $"the unit is {answered.Answer}.");
        Assert.True(
            answered.FilteredOrder.SequenceEqual(["root", "b"]),
            "matches come back in population order, not in the order they were "
            + "supplied.");
        Assert.True(
            answered.Matched.SetEquals(["root", "b"]) && answered.VisibleCount == 2,
            "with the matched set and the count agreeing.");

        CanvasProjectionUnit unknown = CanvasProjectionUnit
            .Unfiltered(population)
            .Answered(population, ["b", "not-in-this-graph"]);
        Assert.True(
            unknown.FilteredOrder.SequenceEqual(["b"]),
            "an answer naming a node this population does not have contributes "
            + "nothing — a match is a claim about THIS graph.");
    }

    /// <summary>An empty answer and a failed one are different facts,
    /// which is why the answer state has four branches and not a "no
    /// rows" flag.</summary>
    [Fact]
    public void AnEmptyAnswerIsNotAFailedOne()
    {
        CanvasPopulation population = Outline(("root", 0));
        CanvasProjectionUnit empty = CanvasProjectionUnit
            .Unfiltered(population)
            .Answered(population, []);
        CanvasProjectionUnit failed = CanvasProjectionUnit
            .Unfiltered(population)
            .Failed();

        Assert.True(
            empty.VisibleCount == 0 && failed.VisibleCount == 0,
            "premise: both show nothing, or the distinction below is visible for "
            + "the wrong reason.");
        Assert.True(
            empty.Answer == CanvasAnswerState.Answered
                && failed.Answer == CanvasAnswerState.Failed,
            "a match that ran and found nothing must stay distinguishable from a "
            + "match that could not run; a reader is owed different sentences.");
    }

    /// <summary>The DISPLAYED ordinal is a position in the unit, and
    /// zero for a row the unit does not show. Core's sibling ordinal is
    /// a position in the model — the two disagreeing is why they are
    /// different classes.</summary>
    [Fact]
    public void TheDisplayedOrdinalIsAPositionInTheUnit()
    {
        CanvasPopulation population = Outline(("root", 0), ("a", 1), ("b", 1));
        CanvasProjectionUnit answered = CanvasProjectionUnit
            .Unfiltered(population)
            .Answered(population, ["a", "b"]);

        Assert.True(
            answered.DisplayedOrdinal("a") == 1 && answered.DisplayedOrdinal("b") == 2,
            "the ordinal is one-based over the VISIBLE rows, so the first match is "
            + "one even though it is the second row of the graph.");
        Assert.True(
            answered.DisplayedOrdinal("root") == 0,
            "a filtered-out row has no displayed position, and zero says so rather "
            + "than an exception.");
    }

    /// <summary>A resolved selection that filters out is dropped, not
    /// carried into a unit that cannot show it.</summary>
    [Fact]
    public void AResolvedSelectionThatFiltersOutIsDropped()
    {
        CanvasPopulation population = Outline(("root", 0), ("a", 1));
        CanvasProjectionUnit unit = CanvasProjectionUnit
            .Unfiltered(population, resolvedSelection: "root");

        Assert.True(
            unit.ResolvedSelection == "root",
            "premise: the unfiltered unit carries the resolved selection.");

        CanvasProjectionUnit narrowed = unit.Answered(population, ["a"]);
        Assert.True(
            narrowed.ResolvedSelection is null,
            "a selection the unit no longer shows stays resolved, so a consumer "
            + "reading the pair sees a selection that is not in the rows beside "
            + "it.");

        CanvasProjectionUnit kept = unit.Answered(population, ["root", "a"]);
        Assert.True(
            kept.ResolvedSelection == "root",
            "and a selection the unit still shows survives.");
    }

    /// <summary>Model values compare by reference here too — the guard
    /// task T1 established for the publication, extended as its types
    /// land.</summary>
    [Fact]
    public void TheFinerClassesAreComparedByReferenceAndNeverByValue()
    {
        CanvasPopulation p1 = CanvasPopulation.Empty();
        CanvasPopulation p2 = CanvasPopulation.Empty();
        Assert.False(
            p1.Equals(p2),
            "two empty populations compared equal, so this type has acquired value "
            + "equality and every currency question about it now has two answers.");

        CanvasProjectionUnit u1 = CanvasProjectionUnit.Unfiltered(p1);
        CanvasProjectionUnit u2 = CanvasProjectionUnit.Unfiltered(p1);
        Assert.False(
            u1.Equals(u2),
            "two identical unfiltered units compared equal; the unit is compared by "
            + "reference inside a publication and must stay that way.");
    }

    private static CanvasPopulation Outline(params (string Id, uint Depth)[] rows) => new(
        rows.Select(row => new CanvasOutlineRow(
            row.Id, row.Depth, "text", row.Id, row.Id, [], 1, (uint)rows.Length, 0, null)),
        null,
        null,
        0,
        null);
}
