// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 PR A (#745), contract A1/R-B: the ONE selection state for a
/// canvas — the mac <c>CanvasSelection</c> twin. It lives on the
/// document, so every tab and pane showing that path shares it;
/// surfaces BIND to it and never keep a copy of their own.
///
/// Arrows move <see cref="Selected"/> and never touch
/// <see cref="Marked"/> (R-B). The marked set exists from this PR so
/// the outline's ItemStatus can carry ", marked" (t0 §3) and so the
/// registry has something to clear when the last tab closes; the verbs
/// that populate it arrive in PR G.
/// </summary>
internal sealed class CanvasSelection : BindableBase
{
    private string? _selected;
    private CanvasSurfaceKind _activeSurface = CanvasSurfaceKind.Outline;
    private readonly HashSet<string> _marked = new(StringComparer.Ordinal);

    /// <summary>The selected node id. Set through
    /// <see cref="CanvasDocumentViewModel.SelectNode"/> on every path
    /// that should narrate; assigned directly only by the document's
    /// own silent seat (contract A12).</summary>
    public string? Selected
    {
        get => _selected;
        internal set => SetField(ref _selected, value);
    }

    /// <summary>The persisted surface (contract A15): outline is the
    /// ABSENT default, so the two writable tokens are the two
    /// non-outline kinds.</summary>
    public CanvasSurfaceKind ActiveSurface
    {
        get => _activeSurface;
        internal set => SetField(ref _activeSurface, value);
    }

    /// <summary>Mark-then-act's set (t4 #524), read-only until PR G
    /// ships the verbs. Ordinal — node ids are byte-exact everywhere.</summary>
    public IReadOnlyCollection<string> Marked => _marked;

    public bool IsMarked(string nodeId) => _marked.Contains(nodeId);

    /// <summary>Cleared when the last tab for the path closes (R-B).
    /// The registry drops the whole document there, so this exists for
    /// the retarget seam, which carries the set across a rename
    /// (CD-32).</summary>
    internal void ClearMarks()
    {
        if (_marked.Count == 0)
        {
            return;
        }
        _marked.Clear();
        OnPropertyChanged(nameof(Marked));
    }

    /// <summary>Seed from a document being retired by a retarget
    /// (CD-32): a rename is not a close, so the user's selection and
    /// marks survive it.</summary>
    internal void SeedFrom(CanvasSelection source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _marked.Clear();
        foreach (string id in source._marked)
        {
            _ = _marked.Add(id);
        }
        OnPropertyChanged(nameof(Marked));
        Selected = source.Selected;
        ActiveSurface = source.ActiveSurface;
    }

    /// <summary>PR G's entry point, present now so the marked set is
    /// never mutated from two places. Returns the new state.</summary>
    internal bool ToggleMark(string nodeId)
    {
        bool marked = _marked.Add(nodeId);
        if (!marked)
        {
            _ = _marked.Remove(nodeId);
        }
        OnPropertyChanged(nameof(Marked));
        return marked;
    }
}
