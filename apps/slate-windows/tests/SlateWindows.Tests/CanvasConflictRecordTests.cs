// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 §E TE-3: the conflict record's state machine — the one
/// resolving door (IE-14), retention until a terminal transition
/// (IE-15), the retained context (IE-12/IE-17), and the ownership
/// tripwire.
/// </summary>
public sealed class CanvasConflictRecordTests
{
    private static CanvasConflictRecord Record()
    {
        var population = new CanvasPopulation(null, null, null, null);
        CanvasPublication published = CanvasPublication.Seed().WithLoaded(
            new CanvasHandleLease(7, _ => { }),
            population,
            CanvasProjectionUnit.Unfiltered(population));
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("conflicted"),
            owner: new object(),
            anchor: "card-1",
            basis: published.Loaded!,
            effect: CanvasMutationEffect.KeepSelection);
        return new CanvasConflictRecord(
            operation,
            new CanvasAction("edit card", []),
            "edit card",
            failedBasis: "rev-2",
            CanvasConflictHistoryPolicy.PushAndClearRedo,
            new CanvasEditorSeed("{}\n", "rev-2"));
    }

    /// <summary>IE-14: one resolving door — a second resolution while
    /// one runs is refused, and a finished record admits none.</summary>
    [Fact]
    public void TheResolvingDoorOpensOnce()
    {
        CanvasConflictRecord record = Record();
        Assert.True(record.TryBeginResolving(CanvasConflictResolution.Overwrite));
        Assert.False(
            record.TryBeginResolving(CanvasConflictResolution.Reload),
            "a second resolution began while one was running: the record "
            + "interleaved what IE-14 serializes.");

        record.CompleteResolved(CanvasConflictResolution.Overwrite);
        Assert.True(record.Terminal);
        Assert.False(
            record.TryBeginResolving(CanvasConflictResolution.SaveACopy),
            "a terminal record admitted a resolution.");
    }

    /// <summary>IE-15: a failed resolution returns to Pending with the
    /// whole retained context intact — no outcome discards recovery.</summary>
    [Fact]
    public void AFailedResolutionKeepsTheRecordWhole()
    {
        CanvasConflictRecord record = Record();
        Assert.True(record.TryBeginResolving(CanvasConflictResolution.SaveACopy));
        record.FailBackToPending(CanvasConflictResolution.SaveACopy);

        Assert.False(record.Terminal);
        Assert.Null(record.Resolving);
        Assert.Equal("edit card", record.AttemptedName);
        Assert.Equal("rev-2", record.FailedBasis);
        Assert.Equal("{}\n", record.PreConflictSnapshot.Text);
        Assert.True(
            record.TryBeginResolving(CanvasConflictResolution.Reload),
            "a pending record refused the next resolution attempt.");
        record.CompleteResolved(CanvasConflictResolution.Reload);
    }

    /// <summary>The ownership tripwire: a terminal transition from a
    /// resolution that does not own the record is the unsurvivable
    /// invariant breach, never a quiet transition.</summary>
    [Fact]
    public void ATerminalTransitionRequiresTheOwningResolution()
    {
        CanvasConflictRecord record = Record();
        var thrown = Assert.Throws<CanvasLeaseViolationException>(
            () => record.CompleteResolved(CanvasConflictResolution.Reload));
        Assert.False(CanvasFaults.Survivable(thrown));

        Assert.True(record.TryBeginResolving(CanvasConflictResolution.Overwrite));
        Assert.Throws<CanvasLeaseViolationException>(
            () => record.FailBackToPending(CanvasConflictResolution.Reload));
        record.CompleteReplaced(CanvasConflictResolution.Overwrite);
        Assert.True(record.Terminal);
    }
}
