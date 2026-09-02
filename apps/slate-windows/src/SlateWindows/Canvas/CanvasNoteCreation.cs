// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Canvas;

/// <summary>§G2 TG2-6 (G2-11, IG2-6, IG2-55, IG2-56): the note-creation
/// SEAM Convert Card to Note… writes through — the sidebar's own create
/// path (its session-work lease, the typed create outcomes, its history
/// barrier and tree refresh), reached from the canvas without the canvas
/// learning the sidebar. <see cref="TryCreateNote"/> is worker-safe and
/// runs INSIDE the canvas gate; <see cref="NoteLanded"/> touches UI-owned
/// state and is called on the dispatcher after the note landed.</summary>
internal interface ICanvasNoteCreator
{
    CanvasNoteCreateResult TryCreateNote(string path, string content);

    /// <summary>The note landed (committed, or published but unindexed):
    /// the structural history barrier, the tree refresh, and the
    /// unindexed caveat spoken AFTER the confirmation, never instead.</summary>
    void NoteLanded(string path, string? caveat);
}

/// <summary>The seam's answer — every arm the create path can take,
/// including the session-work refusal at shutdown (IG2-56).</summary>
internal abstract record CanvasNoteCreateResult
{
    private CanvasNoteCreateResult()
    {
    }

    /// <summary>The note is REAL on disk: committed (no caveat) or
    /// published-but-unindexed (the #1123 caveat to speak after).</summary>
    internal sealed record Landed(string? Caveat) : CanvasNoteCreateResult;

    /// <summary>A file already exists at the path — nothing written.</summary>
    internal sealed record Exists : CanvasNoteCreateResult;

    /// <summary>The vault refused the write for another reason.</summary>
    internal sealed record Failed(string Message) : CanvasNoteCreateResult;

    /// <summary>The sidebar's session work is shutting down — nothing ran.</summary>
    internal sealed record Unavailable : CanvasNoteCreateResult;
}
