// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 §E TE-2: THE HISTORY DOMAIN — per-document undo/redo stacks of
/// basis-carrying entries, with a TWO-PHASE checkout so "transfer only
/// on success, restore on every non-commit outcome" (IE-9) is
/// structural, and the QUARANTINE (IE-10) is one comparison: an entry
/// is offered only while its basis equals the attached one; a reload
/// that attaches a different revision quarantines rather than letting
/// an old inverse pass the new CAS by coincidence. The EPOCH makes
/// IE-20's snapshot validation an integer plus a reference compare.
/// Session state; never persisted.
/// </summary>
internal sealed class CanvasUndoStack
{
    private ImmutableStack<CanvasHistoryEntry> _undo = ImmutableStack<CanvasHistoryEntry>.Empty;
    private ImmutableStack<CanvasHistoryEntry> _redo = ImmutableStack<CanvasHistoryEntry>.Empty;
    private CanvasHistorySnapshot? _checkout;
    private string? _attachedBasis;
    private int _epoch;

    internal int Epoch => _epoch;

    /// <summary>The attached revision; null before the first load.</summary>
    internal string? AttachedBasis => _attachedBasis;

    /// <summary>The offered top, or null when empty or quarantined.</summary>
    internal CanvasHistoryEntry? OfferedUndo => Offered(_undo);

    internal CanvasHistoryEntry? OfferedRedo => Offered(_redo);

    /// <summary>Entries exist but the top is not offered — the state
    /// the vocabulary's truthful quarantine sentence names.</summary>
    internal bool UndoQuarantined => !_undo.IsEmpty && OfferedUndo is null;

    internal bool RedoQuarantined => !_redo.IsEmpty && OfferedRedo is null;

    private CanvasHistoryEntry? Offered(ImmutableStack<CanvasHistoryEntry> stack) =>
        !stack.IsEmpty && stack.Peek() is { } top && top.Basis == _attachedBasis
            ? top
            : null;

    /// <summary>A committed operation's tail: push, clear redo, rebase
    /// to the reported successor — one motion.</summary>
    internal void PushAndClearRedo(CanvasHistoryEntry entry, string successorBasis)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(successorBasis);
        RefuseWhileCheckedOut("push");
        _undo = _undo.Push(entry);
        _redo = ImmutableStack<CanvasHistoryEntry>.Empty;
        _attachedBasis = successorBasis;
        _epoch++;
    }

    /// <summary>The attached document moved (load, reload).</summary>
    internal void Rebase(string attachedBasis)
    {
        ArgumentNullException.ThrowIfNull(attachedBasis);
        RefuseWhileCheckedOut("rebase");
        _attachedBasis = attachedBasis;
        _epoch++;
    }

    /// <summary>Session end: everything goes.</summary>
    internal void Clear()
    {
        RefuseWhileCheckedOut("clear");
        _undo = ImmutableStack<CanvasHistoryEntry>.Empty;
        _redo = ImmutableStack<CanvasHistoryEntry>.Empty;
        _attachedBasis = null;
        _epoch++;
    }

    /// <summary>The menu-open capture (IE-20); null when nothing is
    /// offered.</summary>
    internal CanvasHistorySnapshot? SnapshotUndo() =>
        OfferedUndo is { } entry ? new CanvasHistorySnapshot(entry, _epoch, redo: false) : null;

    internal CanvasHistorySnapshot? SnapshotRedo() =>
        OfferedRedo is { } entry ? new CanvasHistorySnapshot(entry, _epoch, redo: true) : null;

    /// <summary>Phase one: the snapshot's entry leaves its stack.
    /// Refuses when the stack moved since the snapshot; phase two is
    /// then mandatory.</summary>
    internal bool TryCheckOut(CanvasHistorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        RefuseWhileCheckedOut("check out");
        if (snapshot.Epoch != _epoch)
        {
            return false;
        }
        ImmutableStack<CanvasHistoryEntry> stack = snapshot.Redo ? _redo : _undo;
        if (stack.IsEmpty || !ReferenceEquals(stack.Peek(), snapshot.Entry))
        {
            return false;
        }
        _ = snapshot.Redo ? _redo = _redo.Pop() : _undo = _undo.Pop();
        _checkout = snapshot;
        _epoch++;
        return true;
    }

    /// <summary>Phase two, success: the executed inverse's own entry
    /// crosses to the OPPOSITE stack; the basis advances.</summary>
    internal void CommitCheckout(CanvasHistoryEntry successor, string successorBasis)
    {
        ArgumentNullException.ThrowIfNull(successor);
        ArgumentNullException.ThrowIfNull(successorBasis);
        CanvasHistorySnapshot checkout = TakeCheckout("commit");
        _ = checkout.Redo ? _undo = _undo.Push(successor) : _redo = _redo.Push(successor);
        _attachedBasis = successorBasis;
        _epoch++;
    }

    /// <summary>Phase two, EVERY non-commit outcome: the entry returns
    /// exactly where it was (IE-9).</summary>
    internal void RestoreCheckout()
    {
        CanvasHistorySnapshot checkout = TakeCheckout("restore");
        _ = checkout.Redo
            ? _redo = _redo.Push(checkout.Entry)
            : _undo = _undo.Push(checkout.Entry);
        _epoch++;
    }

    private CanvasHistorySnapshot TakeCheckout(string verb)
    {
        if (_checkout is not { } checkout)
        {
            throw new CanvasLeaseViolationException(
                $"a history {verb} arrived with no checkout open: one checkout, "
                + "then exactly one commit or restore");
        }
        _checkout = null;
        return checkout;
    }

    private void RefuseWhileCheckedOut(string verb)
    {
        if (_checkout is not null)
        {
            throw new CanvasLeaseViolationException(
                $"a history {verb} arrived while a checkout is open: the entry "
                + "in flight would be lost or doubled");
        }
    }
}
