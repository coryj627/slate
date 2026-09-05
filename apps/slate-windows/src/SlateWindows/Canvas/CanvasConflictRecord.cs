// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 §E TE-3 (IE-12): what a conflicted operation's history motion
/// WOULD have been — retained whole so a resolution can perform the
/// right transfer instead of guessing. An Overwrite after a conflicted
/// UNDO transfers to redo; after an ordinary verb it pushes and clears;
/// NoHistory is the editor's no-op arm.
/// </summary>
internal enum CanvasConflictHistoryPolicy
{
    PushAndClearRedo,
    UndoTransfer,
    RedoTransfer,
    NoHistory,
}

/// <summary>The three typed resolutions (E6's table).</summary>
internal enum CanvasConflictResolution
{
    Reload,
    Overwrite,
    SaveACopy,
}

/// <summary>
/// W6-1 §E TE-3: THE CONFLICT RECORD — one WriteConflict's whole
/// context, retained until a TERMINAL transition (IE-15): the full
/// operation (owner, effects, mode token — IE-12), the attempted
/// action, the basis it failed against, the history policy, and the
/// pre-conflict document snapshot (text + basis, IE-17) Save a Copy
/// applies the action to detachedly.
/// </summary>
/// <remarks>
/// <para>
/// THE STATE MACHINE: Pending → Resolving(resolution) → terminal
/// (resolved) or back to Pending (a resolution attempt that failed
/// without replacing the record) or REPLACED by a fresh record (an
/// Overwrite that conflicted again). While any record stands,
/// admission refuses ordinary writes; the RESOLVING token (IE-14) is
/// what lets the resolution's own apply through — the record hands it
/// out once, and a second resolution while one runs is refused, not
/// interleaved.
/// </para>
/// <para>
/// Terminal means GONE: the document's conflict value (IE-18) and the
/// admission block both read "a record stands", so clearing exactly
/// once at the terminal transition is the whole of their lifecycle.
/// Discarding a record without a terminal transition is the tripwire.
/// </para>
/// </remarks>
internal sealed class CanvasConflictRecord
{
    private CanvasConflictResolution? _resolving;
    private bool _terminal;

    internal CanvasConflictRecord(
        CanvasMutationOperation operation,
        CanvasAction attempted,
        string attemptedName,
        string failedBasis,
        CanvasConflictHistoryPolicy historyPolicy,
        CanvasEditorSeed preConflictSnapshot)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(attempted);
        ArgumentNullException.ThrowIfNull(attemptedName);
        ArgumentNullException.ThrowIfNull(failedBasis);
        ArgumentNullException.ThrowIfNull(preConflictSnapshot);
        Operation = operation;
        Attempted = attempted;
        AttemptedName = attemptedName;
        FailedBasis = failedBasis;
        HistoryPolicy = historyPolicy;
        PreConflictSnapshot = preConflictSnapshot;
    }

    internal CanvasMutationOperation Operation { get; }

    internal CanvasAction Attempted { get; }

    internal string AttemptedName { get; }

    /// <summary>The basis the attempt was refused against.</summary>
    internal string FailedBasis { get; }

    internal CanvasConflictHistoryPolicy HistoryPolicy { get; }

    /// <summary>The document as it was when the conflict arose — the
    /// text Save a Copy applies the attempted action to (IE-17), with
    /// the basis proving which revision was captured.</summary>
    internal CanvasEditorSeed PreConflictSnapshot { get; }

    /// <summary>The resolution in flight, or null.</summary>
    internal CanvasConflictResolution? Resolving => _resolving;

    internal bool Terminal => _terminal;

    /// <summary>IE-14: the one door. True and this resolution owns the
    /// record — its own apply is the only write admission passes while
    /// the record stands. False and one is already running, or the
    /// record is finished; the caller refuses, never queues.</summary>
    internal bool TryBeginResolving(CanvasConflictResolution resolution)
    {
        if (_terminal || _resolving is not null)
        {
            return false;
        }
        _resolving = resolution;
        return true;
    }

    /// <summary>The resolution's terminal transition: the record is
    /// finished exactly once, by the resolution that owns it.</summary>
    internal void CompleteResolved(CanvasConflictResolution resolution)
    {
        RequireOwnership(resolution, "complete");
        _resolving = null;
        _terminal = true;
    }

    /// <summary>The resolution failed WITHOUT replacing the record —
    /// an I/O refusal, an invalid argument: back to Pending, the
    /// record and its retained context intact (IE-15's totality; no
    /// outcome discards recovery).</summary>
    internal void FailBackToPending(CanvasConflictResolution resolution)
    {
        RequireOwnership(resolution, "fail");
        _resolving = null;
    }

    /// <summary>An Overwrite that conflicted AGAIN: this record ends
    /// terminal and the caller stands up the fresh record atomically —
    /// the document is never conflict-free in between.</summary>
    internal void CompleteReplaced(CanvasConflictResolution resolution)
    {
        RequireOwnership(resolution, "replace");
        _resolving = null;
        _terminal = true;
    }

    private void RequireOwnership(CanvasConflictResolution resolution, string verb)
    {
        if (_terminal || _resolving != resolution)
        {
            throw new CanvasLeaseViolationException(
                $"a conflict {verb} arrived from a resolution that does not own "
                + "the record: one resolving door, opened once, closed by its "
                + "opener — anything else is the invariant contradiction");
        }
    }
}
