// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.FileManagement;

/// <summary>
/// W6-2 PR A (#746), contract A-8 / AD-8: the surface-neutral note
/// creator — <c>ICanvasNoteCreator</c> generalised with TYPED outcomes.
/// The sidebar is the one implementation: its worker-safe create runs
/// under the session-work lease, and its landing runs the structural
/// history barrier and the tree refresh; the caveat is spoken separately,
/// AFTER the surface's own confirmation (the sidebar's order).
/// </summary>
internal interface ISurfaceNoteCreator
{
    /// <summary>The worker-safe half: create exclusively with the given
    /// content. Never throws for a vault refusal; the outcome says.</summary>
    NoteCreateResult TryCreateNote(string path, string content);

    /// <summary>The dispatcher-side landing: the structural history
    /// barrier and the tree refresh — no speech.</summary>
    void NoteLanded(string path);

    /// <summary>The landed-but-unindexed caveat, spoken the sidebar's
    /// way — called by the surface after its own confirmation.</summary>
    void SpeakCaveat(string caveat);
}

/// <summary>The seam's typed answer (the round-3 ledger's IGA-26).</summary>
internal abstract record NoteCreateResult
{
    private NoteCreateResult()
    {
    }

    /// <summary>Committed, or published but unindexed (the caveat).</summary>
    public sealed record Landed(string? Caveat) : NoteCreateResult;

    /// <summary>The destination exists — with the vault's message.</summary>
    public sealed record Exists(string Message) : NoteCreateResult;

    /// <summary>The vault refused for another reason.</summary>
    public sealed record Failed(string Message) : NoteCreateResult;

    /// <summary>Session work refused at shutdown: nothing ran.</summary>
    public sealed record Unavailable : NoteCreateResult;
}
