// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;

namespace SlateWindows.FileManagement;

/// <summary>Which forward operation a journal step re-runs to undo
/// (mac's #871 model: singles are undone by re-running the forward
/// FFI with the inverse arguments — never <c>undo_structural</c>;
/// batch moves ride the dedicated <c>UndoBatchMove</c> endpoint).</summary>
internal enum StructuralUndoKind
{
    Rename,
    Move,
    BatchMove,
}

/// <summary>
/// One inverse operation. For <see cref="StructuralUndoKind.Rename"/>,
/// <c>Path</c> is the entry's CURRENT path and <c>Argument</c> the
/// name to rename back to; for <c>Move</c>, <c>Argument</c> is the
/// parent to move back to (empty = vault root); for
/// <c>BatchMove</c>, <c>BatchOpId</c> is the journal handle and
/// <c>Path</c>/<c>Argument</c> are unused. <c>Noun</c> is the display
/// name the undo announcement speaks (mac: "Undid rename to {y}." /
/// "Undid move of {x}.").
/// </summary>
internal sealed record StructuralUndoStep(
    StructuralUndoKind Kind,
    string Path,
    string Argument,
    bool IsDirectory,
    string Noun,
    long BatchOpId = 0);

/// <summary>
/// The per-vault structural undo/redo journal (W5-4 F10) — mac's
/// #871 stacks translated: LIFO, redo cleared by every fresh push,
/// both cleared by history BARRIERS (creates, duplicate, trash —
/// mac's exact table) and by vault transitions (the owner dies with
/// the vault). Pure state; the sidebar executes steps and pushes the
/// re-inverse onto the opposite stack.
/// </summary>
internal sealed class StructuralUndoJournal
{
    private readonly List<StructuralUndoStep> _undo = [];
    private readonly List<StructuralUndoStep> _redo = [];

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Record a fresh user operation's inverse. A new
    /// forward operation forks history: redo clears (mac's rule).</summary>
    public void Push(StructuralUndoStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _undo.Add(step);
        _redo.Clear();
    }

    /// <summary>A history barrier (create/duplicate/trash): both
    /// stacks clear — "a stale inverse must never target a path the
    /// barrier op now owns" (mac 19013-19017's rationale).</summary>
    public void Barrier()
    {
        _undo.Clear();
        _redo.Clear();
    }

    public bool TryPopUndo(out StructuralUndoStep step) => TryPop(_undo, out step);

    public bool TryPopRedo(out StructuralUndoStep step) => TryPop(_redo, out step);

    /// <summary>The executed step's re-inverse lands on the OPPOSITE
    /// stack (undo → redo and back), never through Push — a completed
    /// undo must not clear the redo it just created.</summary>
    public void PushRedo(StructuralUndoStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _redo.Add(step);
    }

    public void PushUndoFromRedo(StructuralUndoStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _undo.Add(step);
    }

    /// <summary>The executability preflight found changed files: the
    /// suspect history is dropped wholesale (mac: "Can't undo — the
    /// files have changed."). Partial trust is worse than none — a
    /// half-valid stack replays inverses against strangers.</summary>
    public void DropForChangedFiles() => Barrier();

    private static bool TryPop(List<StructuralUndoStep> stack, out StructuralUndoStep step)
    {
        if (stack.Count == 0)
        {
            step = null!;
            return false;
        }

        step = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return true;
    }
}
