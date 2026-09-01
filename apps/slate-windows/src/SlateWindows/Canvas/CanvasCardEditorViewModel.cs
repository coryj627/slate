// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 §E TE-7: the card editor's MODEL — the SEED TOKEN whole
/// (node, title, text and the BASIS it was read at, one locked core
/// read), the scratch buffer session, and M8's commit semantics:
/// **Escape commits** — the no-change arm speaks "No changes.", a
/// moved basis opens the IN-SHEET conflict state with the draft
/// intact (IE-18/IE-19: the sheet holds the only copy of the text
/// and never closes over it), and a funnel refusal keeps sheet and
/// draft (IE-9). Discarding is the editor's own undo before Escape;
/// no arm of this type cites M2.
/// </summary>
internal sealed class CanvasCardEditorViewModel
{
    private readonly CanvasDocumentViewModel _document;
    private readonly CanvasEditorSeed _seed;
    private bool _conflicted;

    internal CanvasCardEditorViewModel(
        CanvasDocumentViewModel document,
        string nodeId,
        string title,
        CanvasEditorSeed seed)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(nodeId);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(seed);
        _document = document;
        _seed = seed;
        NodeId = nodeId;
        Title = title;
        Draft = seed.Text;
    }

    internal string NodeId { get; }

    /// <summary>PUBLIC because WPF binds it (§E TE-11e): a binding
    /// against a non-public property fails SILENTLY, which left the
    /// sheet's box unbound - the box held the typing, the draft
    /// held the seed, and Escape's no-changes arm closed over the
    /// difference. The authoring journey caught it.</summary>
    public string Title { get; }

    /// <summary>The working text. The real sheet binds the buffer
    /// session's document here; the model-level facts drive it
    /// directly (the session's own machinery is W2-1's, already
    /// gated).PUBLIC for the binding reason <see cref="Title"/> records.</summary>
    public string Draft { get; set; }

    /// <summary>The basis the seed was read at — the token's currency
    /// half (IE-4/IE-18).</summary>
    internal string SeedBasis => _seed.ContentHash;

    /// <summary>The in-sheet conflict state: the document moved under
    /// the draft. Recovery reads the funnel's record; the draft stays
    /// here either way.</summary>
    internal bool Conflicted => _conflicted;

    /// <summary>True when the sheet may CLOSE — the one exit besides
    /// a discard-by-choice. Escape routes here (M8) and the ladder
    /// never sees it.</summary>
    internal bool CommitOnEscape()
    {
        if (string.Equals(Draft, _seed.Text, StringComparison.Ordinal))
        {
            _document.SpeakForEditor(
                new CanvasA11yEvent.CanvasStatus(new CanvasStatusNote.NoChanges()));
            return true;
        }
        if (!string.Equals(
            _document.PublishedBasis, _seed.ContentHash, StringComparison.Ordinal))
        {
            // The document is not the one the draft grew from: the
            // conflict surfaces IN the sheet, the draft survives, and
            // closing is a deliberate discard, never this commit.
            _conflicted = true;
            _document.SpeakForEditor(new CanvasA11yEvent.CanvasSaveConflict());
            return false;
        }
        _document.CanvasCommitCardEdit(NodeId, Draft);
        return true;
    }
}
