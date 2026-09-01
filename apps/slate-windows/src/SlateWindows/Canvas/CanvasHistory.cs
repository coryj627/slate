// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 §E TE-2: one history entry — the action's name, the returned
/// inverse, and the BASIS it is valid against (the post-write hash its
/// own apply reported). The basis is the entry's whole admission: an
/// entry whose basis is not the attached document's is QUARANTINED,
/// never offered, because an old inverse passing a NEW document's CAS
/// by coincidence would overwrite an external writer's work (round 2's
/// IE-10 scenario, closed by construction).
/// </summary>
internal sealed class CanvasHistoryEntry
{
    internal CanvasHistoryEntry(string name, CanvasAction inverse, string basis)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(inverse);
        ArgumentNullException.ThrowIfNull(basis);
        Name = name;
        Inverse = inverse;
        Basis = basis;
    }

    internal string Name { get; }

    internal CanvasAction Inverse { get; }

    /// <summary>The content hash this inverse applies AGAINST — the
    /// revision that existed after the entry's own apply.</summary>
    internal string Basis { get; }
}

/// <summary>
/// W6-1 §E TE-2: the menu-open SNAPSHOT (IE-20) — the exact entry and
/// the stack epoch it was offered at, captured when the title was
/// composed. Execution validates THIS, so what the title names is what
/// the click undoes; a stack that moved makes the snapshot stale and
/// execution refuses instead of popping whatever is on top now.
/// </summary>
internal sealed class CanvasHistorySnapshot
{
    internal CanvasHistorySnapshot(CanvasHistoryEntry entry, int epoch, bool redo)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Entry = entry;
        Epoch = epoch;
        Redo = redo;
    }

    internal CanvasHistoryEntry Entry { get; }

    internal int Epoch { get; }

    internal bool Redo { get; }
}
