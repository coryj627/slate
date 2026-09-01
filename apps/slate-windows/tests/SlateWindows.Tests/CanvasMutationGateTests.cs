// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 §E TE-1: the operation value's currency and the gate's
/// admission — IE-1 (identity), IE-2 (currency by reference), IE-33
/// (atomic acquisition), and the holder-only release tripwire.
/// </summary>
public sealed class CanvasMutationGateTests
{
    private static CanvasLoaded LoadedTriple()
    {
        var population = new CanvasPopulation(null, null, null, null);
        CanvasPublication published = CanvasPublication.Seed().WithLoaded(
            new CanvasHandleLease(7, _ => { }),
            population,
            CanvasProjectionUnit.Unfiltered(population));
        return published.Loaded!;
    }

    private static CanvasMutationOperation Operation(CanvasLoaded basis, string label) =>
        new(
            new CanvasOperationId(label),
            owner: new object(),
            anchor: null,
            basis: basis,
            effect: CanvasMutationEffect.KeepSelection);

    /// <summary>IE-33: acquisition is one atomic cell — the second
    /// operation refuses while the first holds, and holds the moment
    /// the holder releases.</summary>
    [Fact]
    public void TheGateAdmitsExactlyOneOperationAtATime()
    {
        CanvasLoaded basis = LoadedTriple();
        var gate = new CanvasMutationGate();
        CanvasMutationOperation first = Operation(basis, "first");
        CanvasMutationOperation second = Operation(basis, "second");

        Assert.True(gate.TryAcquire(first), "a free gate refused its first holder.");
        Assert.False(
            gate.TryAcquire(second),
            "a held gate admitted a second operation: ED-5's refusal became a queue.");
        Assert.Same(first, gate.Held);

        gate.Release(first);
        Assert.Null(gate.Held);
        Assert.True(gate.TryAcquire(second), "a released gate refused the next operation.");
        gate.Release(second);
    }

    /// <summary>The tripwire: release by a non-holder surfaces as the
    /// unsurvivable exception, never a quiet no-op a broad catch could
    /// absorb.</summary>
    [Fact]
    public void ReleaseByANonHolderTripsTheInvariantWire()
    {
        CanvasLoaded basis = LoadedTriple();
        var gate = new CanvasMutationGate();
        CanvasMutationOperation holder = Operation(basis, "holder");
        CanvasMutationOperation stranger = Operation(basis, "stranger");
        Assert.True(gate.TryAcquire(holder));

        var thrown = Assert.Throws<CanvasLeaseViolationException>(
            () => gate.Release(stranger));
        Assert.False(
            CanvasFaults.Survivable(thrown),
            "the gate's tripwire is survivable, so a broad catch would absorb "
            + "an invariant breach as an ordinary failure.");
        Assert.Same(holder, gate.Held);
        gate.Release(holder);
    }

    /// <summary>IE-2: currency is the basis REFERENCE against the live
    /// publication — a successor triple, or retirement, makes the one
    /// boundary question answer no. No stamp, no counter, no ABA
    /// window.</summary>
    [Fact]
    public void AnOperationsCurrencyIsItsBasisReference()
    {
        var population = new CanvasPopulation(null, null, null, null);
        var slot = new CanvasPublicationSlot(CanvasPublication.Seed());
        _ = slot.Publish(s => s.WithLoaded(
            new CanvasHandleLease(7, _ => { }),
            population,
            CanvasProjectionUnit.Unfiltered(population)));
        CanvasMutationOperation operation = Operation(slot.Current.Loaded!, "minted");
        Assert.True(
            operation.IsCurrentAgainst(slot.Current),
            "premise: an operation is current against the publication it was "
            + "minted from.");

        var successor = new CanvasPopulation(null, null, null, null);
        _ = slot.Publish(s => s.WithLoaded(
            new CanvasHandleLease(8, _ => { }),
            successor,
            CanvasProjectionUnit.Unfiltered(successor)));
        Assert.False(
            operation.IsCurrentAgainst(slot.Current),
            "a reloaded publication still admitted an operation minted against "
            + "the retired triple: the stale-apply door IE-2 names is open.");

        _ = slot.Publish(s => s.WithRetired());
        Assert.False(operation.IsCurrentAgainst(slot.Current));
    }

    /// <summary>IE-10: the committed-but-unpresented state is a
    /// spellable publication state — set by the funnel when a commit's
    /// refresh fails, cleared only by the refresh-only recovery, and
    /// carried untouched across unrelated publications.</summary>
    [Fact]
    public void TheCommittedUnpresentedStateSpellsAndClears()
    {
        var operation = new CanvasOperationId("landed");
        CanvasPublication marked =
            CanvasPublication.Seed().WithCommittedUnpresented(operation);
        Assert.Same(operation, marked.CommittedUnpresented);

        CanvasPublication carried = marked.WithSelectedIntent("a");
        Assert.Same(
            operation,
            carried.CommittedUnpresented);

        Assert.Null(carried.WithPresented().CommittedUnpresented);
    }

    /// <summary>IE-1: equal inputs mint DISTINCT invocations — the id
    /// is a reference, so a retry is never the original.</summary>
    [Fact]
    public void EqualInputsMintDistinctOperationIdentities()
    {
        CanvasLoaded basis = LoadedTriple();
        Assert.NotSame(Operation(basis, "same").Id, Operation(basis, "same").Id);
    }
}
