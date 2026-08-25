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

    /// <summary>The selected node id. READ-ONLY here: it is correlated
    /// with the rows it points into, so it moves inside the document's
    /// publication transaction like everything else the observers can
    /// see (contract C10). <see cref="CanvasDocumentViewModel.SelectNode"/>
    /// is the narrating path; the document's silent seat is the other
    /// (contract A12).</summary>
    public string? Selected => _selected;

    /// <summary>The persisted surface (contract A15): outline is the
    /// ABSENT default, so the two writable tokens are the two
    /// non-outline kinds. Read-only for <see cref="Selected"/>'s
    /// reason — a surface change re-renders both projections.</summary>
    public CanvasSurfaceKind ActiveSurface => _activeSurface;

    /// <summary>
    /// Write the selection WITHOUT notifying, and say whether it moved.
    /// </summary>
    /// <remarks>
    /// The document's publication transaction owns the notification and
    /// its ORDER: a selection change raised before the projections have
    /// rebuilt reaches a view that would seat the reader against rows
    /// that are about to be replaced, which is the two-channel class one
    /// object over (contract C10). Staging is the only way to write
    /// this, so that ordering is not something a caller can forget.
    /// </remarks>
    internal bool StageSelected(string? value)
    {
        if (string.Equals(_selected, value, StringComparison.Ordinal))
        {
            return false;
        }
        _selected = value;
        return true;
    }

    /// <summary>The surface's twin of <see cref="StageSelected"/>.</summary>
    internal bool StageActiveSurface(CanvasSurfaceKind value)
    {
        if (_activeSurface == value)
        {
            return false;
        }
        _activeSurface = value;
        return true;
    }

    /// <summary>Raise a staged change — called only by the document's
    /// publication when it commits.</summary>
    internal void RaiseStaged(string name) => OnPropertyChanged(name);

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
        // Seeded before any surface is bound to this document (the
        // retarget builds it, then attaches), so these raise directly
        // rather than through a publication that has nobody to notify.
        if (StageSelected(source.Selected))
        {
            RaiseStaged(nameof(Selected));
        }
        if (StageActiveSurface(source.ActiveSurface))
        {
            RaiseStaged(nameof(ActiveSurface));
        }
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
