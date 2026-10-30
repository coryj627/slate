// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using ICSharpCode.AvalonEdit.Document;

namespace SlateWindows;

/// <summary>Which surface the find bar searches: the note's editor or its
/// reading view.</summary>
internal enum FindInNoteMode
{
    Editor,
    Reading,
}

/// <summary>
/// WB-22 (#1112): what the find bar needs from the surface that shows the
/// note. The editor's <see cref="EditorFindSurface"/> is one; the reading
/// view's is PR 2's. Offsets are in <see cref="Text"/>'s coordinates.
/// </summary>
internal interface IFindInNoteSurface
{
    FindInNoteMode Mode { get; }

    /// <summary>False while the surface shows nothing searchable (the
    /// reading view's loading placeholder).</summary>
    bool IsSearchable { get; }

    ITextSource Text { get; }

    /// <summary>Changes whenever <see cref="Text"/> does; compared by
    /// reference to tell a stale result set.</summary>
    object Stamp { get; }

    (int Offset, int Length) Selection { get; }

    /// <summary>The editor's document; null on a surface that cannot be
    /// edited (so Replace cannot execute there).</summary>
    TextDocument? Document { get; }

    /// <summary>The match becomes the surface's selection and is brought
    /// into view; keyboard focus is untouched.</summary>
    void Reveal(int offset, int length);

    void Highlight(IReadOnlyList<(int Offset, int Length)> matches, int current);

    void ClearHighlights();

    object CaptureRestorePoint();

    /// <summary>A restore point taken on another surface's text (a same-item
    /// reload swapped the document) moved onto this one, at the same
    /// offsets, clamped; one already on this surface is returned as is.</summary>
    object RebaseRestorePoint(object restorePoint);

    void Restore(object restorePoint);

    void ReturnFocus();

    event Action? TextChanged;
}
