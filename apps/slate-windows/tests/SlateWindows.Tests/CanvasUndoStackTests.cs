// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 §E TE-2: the history domain — the basis quarantine (IE-10),
/// the two-phase checkout's restore-on-every-non-commit (IE-9), and
/// the menu snapshot's exact-entry execution (IE-20).
/// </summary>
public sealed class CanvasUndoStackTests
{
    private static CanvasHistoryEntry Entry(string name, string basis) =>
        new(name, new CanvasAction(name, []), basis);

    /// <summary>IE-10: a reload that attaches a different revision
    /// quarantines the entries — kept, named as quarantined, never
    /// offered — and the exact basis returning re-offers them.</summary>
    [Fact]
    public void AForeignBasisQuarantinesAndTheExactBasisReturns()
    {
        var stack = new CanvasUndoStack();
        stack.Rebase("rev-1");
        stack.PushAndClearRedo(Entry("create card", "rev-2"), "rev-2");
        Assert.NotNull(stack.OfferedUndo);
        Assert.False(stack.UndoQuarantined);

        stack.Rebase("rev-external");
        Assert.Null(stack.OfferedUndo);
        Assert.True(
            stack.UndoQuarantined,
            "a foreign basis left the entry OFFERED: the old inverse could pass "
            + "the new CAS by coincidence, which is IE-10's overwrite.");

        stack.Rebase("rev-2");
        Assert.NotNull(stack.OfferedUndo);
        Assert.False(stack.UndoQuarantined);
    }

    /// <summary>IE-9: every non-commit outcome restores the popped
    /// entry exactly where it was; success crosses it over.</summary>
    [Fact]
    public void TheCheckoutRestoresOnFailureAndCrossesOnSuccess()
    {
        var stack = new CanvasUndoStack();
        stack.PushAndClearRedo(Entry("edit card", "rev-2"), "rev-2");

        CanvasHistorySnapshot refused = stack.SnapshotUndo()!;
        Assert.True(stack.TryCheckOut(refused));
        stack.RestoreCheckout();
        Assert.Same(refused.Entry, stack.OfferedUndo);

        CanvasHistorySnapshot undone = stack.SnapshotUndo()!;
        Assert.True(stack.TryCheckOut(undone));
        stack.CommitCheckout(Entry("edit card", "rev-3"), "rev-3");
        Assert.Null(stack.OfferedUndo);
        Assert.NotNull(stack.OfferedRedo);
        Assert.Equal("rev-3", stack.AttachedBasis);

        CanvasHistorySnapshot redone = stack.SnapshotRedo()!;
        Assert.True(stack.TryCheckOut(redone));
        stack.CommitCheckout(Entry("edit card", "rev-4"), "rev-4");
        Assert.NotNull(stack.OfferedUndo);
        Assert.Null(stack.OfferedRedo);
    }

    /// <summary>IE-20: the snapshot names the exact entry and epoch —
    /// a stack that moved after the menu opened refuses execution
    /// instead of undoing whatever is on top now.</summary>
    [Fact]
    public void AMovedStackRefusesTheStaleMenuSnapshot()
    {
        var stack = new CanvasUndoStack();
        stack.PushAndClearRedo(Entry("first", "rev-2"), "rev-2");
        CanvasHistorySnapshot menuOpened = stack.SnapshotUndo()!;

        stack.PushAndClearRedo(Entry("second", "rev-3"), "rev-3");
        Assert.False(
            stack.TryCheckOut(menuOpened),
            "a stale snapshot checked out: the title said one entry and the "
            + "click would undo another (IE-20).");
        Assert.NotNull(stack.OfferedUndo);

        // The epoch's OWN scenario — the one the reference compare
        // cannot catch: a checkout/restore cycle puts the SAME entry
        // back on top, and the pre-cycle snapshot must still refuse,
        // or one stale menu could execute the entry twice.
        CanvasHistorySnapshot preCycle = stack.SnapshotUndo()!;
        Assert.True(stack.TryCheckOut(stack.SnapshotUndo()!));
        stack.RestoreCheckout();
        Assert.Same(preCycle.Entry, stack.OfferedUndo);
        Assert.False(
            stack.TryCheckOut(preCycle),
            "a pre-cycle snapshot checked out after a restore cycle: the "
            + "same entry is back on top, but the stack MOVED — only the "
            + "epoch can say so, and it did not.");
    }

    /// <summary>The two-phase tripwires: phase two with no checkout,
    /// and structural motion while one is open, are invariant
    /// breaches — unsurvivable, never quiet.</summary>
    [Fact]
    public void TheTwoPhaseContractTripsOnMisuse()
    {
        var stack = new CanvasUndoStack();
        Assert.Throws<CanvasLeaseViolationException>(stack.RestoreCheckout);

        stack.PushAndClearRedo(Entry("held", "rev-2"), "rev-2");
        Assert.True(stack.TryCheckOut(stack.SnapshotUndo()!));
        Assert.Throws<CanvasLeaseViolationException>(
            () => stack.PushAndClearRedo(Entry("intruder", "rev-9"), "rev-9"));
        stack.RestoreCheckout();
    }

    /// <summary>Session scope: Clear empties both stacks and the
    /// basis; nothing survives to a next document.</summary>
    [Fact]
    public void ClearEndsTheSession()
    {
        var stack = new CanvasUndoStack();
        stack.PushAndClearRedo(Entry("gone", "rev-2"), "rev-2");
        stack.Clear();
        Assert.Null(stack.OfferedUndo);
        Assert.Null(stack.OfferedRedo);
        Assert.False(stack.UndoQuarantined);
        Assert.Null(stack.AttachedBasis);
    }
}
