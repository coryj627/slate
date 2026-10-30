// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Documents;
using ICSharpCode.AvalonEdit.Document;

namespace SlateWindows.Reading;

/// <summary>
/// WB-22 (#1112): the reading view as a find surface. The search runs over a
/// <see cref="ReadingTextIndex"/> cached per content revision; the current
/// match is the surface's selection (a position, not content, so caret
/// reading and the Text pattern carry it) and every match is drawn over the
/// text by a <see cref="FindMatchAdorner"/> — the document, its elements and
/// its peers are never touched.
/// </summary>
internal sealed class ReadingFindSurface : IFindInNoteSurface
{
    private readonly ReadingSurface _surface;
    private ReadingTextIndex? _index;
    private int _indexRevision = -1;
    private FindMatchAdorner? _adorner;
    private Revealed? _revealed;
    private int _replacements;
    private (IReadOnlyList<(int Offset, int Length)> Matches, int Current)? _deferred;

    public ReadingFindSurface(ReadingSurface surface)
    {
        _surface = surface;
        _surface.ContentChanged += OnContentChanged;
    }

    public FindInNoteMode Mode => FindInNoteMode.Reading;

    /// <summary>Never the "Loading reading view…" placeholder.</summary>
    public bool IsSearchable => _surface.ShowsSearchableContent;

    public ITextSource Text => Index.Text;

    /// <summary>The index itself: a new one per content revision.</summary>
    public object Stamp => Index;

    /// <summary>The surface's selection in the index's offsets — the match
    /// last revealed while the selection is still exactly what the reveal
    /// made (a math match selects its whole element).</summary>
    public (int Offset, int Length) Selection
    {
        get
        {
            TextSelection selection = _surface.Selection;
            if (_revealed is { } revealed
                && selection.Start.CompareTo(revealed.Start) == 0
                && selection.End.CompareTo(revealed.End) == 0)
            {
                return (revealed.Offset, revealed.Length);
            }
            int start = Index.OffsetOf(selection.Start);
            return (start, Math.Max(0, Index.OffsetOf(selection.End) - start));
        }
    }

    public TextDocument? Document => null;

    internal ReadingSurface Surface => _surface;

    internal ReadingTextIndex Index
    {
        get
        {
            if (_index is null || _indexRevision != _surface.ContentRevision)
            {
                _index = ReadingTextIndex.Build(_surface.Document);
                _indexRevision = _surface.ContentRevision;
            }
            return _index;
        }
    }

    public event Action? TextChanged;

    /// <summary>The match becomes the surface's selection and is brought
    /// into view; keyboard focus is not moved.</summary>
    public void Reveal(int offset, int length)
    {
        ReadingTextIndex index = Index;
        _surface.Selection.Select(index.StartOf(offset), index.EndOf(offset + length));
        _revealed = new Revealed(offset, length, _surface.Selection.Start, _surface.Selection.End);
        if (_surface.DocumentRenderScope is { } scope && RectsOf(offset, length, scope).FirstOrDefault() is { IsEmpty: false } first)
        {
            scope.BringIntoView(first);
        }
    }

    public void Highlight(IReadOnlyList<(int Offset, int Length)> matches, int current)
    {
        if (matches.Count == 0)
        {
            ClearHighlights();
            return;
        }
        if (_adorner is null)
        {
            if (_surface.DocumentRenderScope is not { } scope || AdornerLayer.GetAdornerLayer(scope) is not { } layer)
            {
                // A surface just shown (a mode switch) has no template yet:
                // the matches are drawn by the first layout pass that has one.
                if (_deferred is null)
                {
                    _surface.LayoutUpdated += OnLayoutUpdated;
                }
                _deferred = (matches, current);
                return;
            }
            _adorner = new FindMatchAdorner(scope, this);
            layer.Add(_adorner);
        }
        StopDeferring();
        _adorner.SetMatches(matches, current);
    }

    public void ClearHighlights()
    {
        StopDeferring();
        if (_adorner is not { } adorner)
        {
            return;
        }
        _adorner = null;
        adorner.Retire();
        AdornerLayer.GetAdornerLayer(adorner.AdornedElement)?.Remove(adorner);
    }

    public object CaptureRestorePoint()
    {
        TextSelection selection = _surface.Selection;
        return new RestorePoint(selection.Start, selection.End, _replacements);
    }

    /// <summary>The surface keeps one document for its whole life, so a
    /// point taken on it stays on it.</summary>
    public object RebaseRestorePoint(object restorePoint) => restorePoint;

    /// <summary>Re-selects where opening found the selection unless a
    /// replacement (a merge or a model switch) moved every pointer since:
    /// then the caret stays where that change left it.</summary>
    public void Restore(object restorePoint)
    {
        if (restorePoint is RestorePoint { } point && point.Replacements == _replacements)
        {
            _surface.Selection.Select(point.Start, point.End);
        }
    }

    public void ReturnFocus() => _surface.RequestFocusLanding();

    public void Detach()
    {
        _surface.ContentChanged -= OnContentChanged;
        ClearHighlights();
    }

    /// <summary>The embedded element a match at <paramref name="offset"/>
    /// marks whole, or null for plain text.</summary>
    internal UIElement? UnitAt(int offset) => Index.UnitAt(offset);

    /// <summary>The rectangles a match covers in <paramref name="scope"/>'s
    /// coordinates: a unit's element bounds, else one per line.</summary>
    internal IEnumerable<Rect> RectsOf(int offset, int length, UIElement scope)
    {
        if (Index.UnitAt(offset) is { IsVisible: true } element)
        {
            if (element.TransformToVisual(scope) is { } toScope)
            {
                yield return toScope.TransformBounds(new Rect(element.RenderSize));
            }
            yield break;
        }
        if (_surface.TransformToDescendant(scope) is not { } transform)
        {
            yield break;
        }
        TextPointer start = Index.StartOf(offset);
        TextPointer end = Index.EndOf(offset + length);
        TextPointer line = start;
        while (line.CompareTo(end) < 0)
        {
            TextPointer? next = line.GetLineStartPosition(1);
            bool last = next is null || next.CompareTo(end) >= 0;
            TextPointer lineEnd = last ? end : next!;
            Rect rect = Rect.Union(
                line.GetCharacterRect(LogicalDirection.Forward),
                lineEnd.GetCharacterRect(LogicalDirection.Backward));
            if (!rect.IsEmpty)
            {
                yield return transform.TransformBounds(rect);
            }
            if (last)
            {
                yield break;
            }
            line = next!;
        }
    }

    /// <summary>The index offsets the viewport shows, first to last.</summary>
    internal (int First, int Last) VisibleOffsets()
    {
        TextPointer? first = _surface.GetPositionFromPoint(new Point(0, 0), snapToText: true);
        TextPointer? last = _surface.GetPositionFromPoint(new Point(_surface.ActualWidth, _surface.ActualHeight), snapToText: true);
        return (first is null ? 0 : Index.OffsetOf(first), last is null ? Index.Text.TextLength : Index.OffsetOf(last));
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_deferred is { } deferred && _surface.DocumentRenderScope is { } scope && AdornerLayer.GetAdornerLayer(scope) is not null)
        {
            Highlight(deferred.Matches, deferred.Current);
        }
    }

    private void StopDeferring()
    {
        if (_deferred is not null)
        {
            _deferred = null;
            _surface.LayoutUpdated -= OnLayoutUpdated;
        }
    }

    private void OnContentChanged(ReadingContentChange change)
    {
        // A replacement moves every pointer to the deletion point and a
        // completion rewrites a diagram paragraph's runs: the drawn matches
        // go at once. Appends only grow the tail and keep them.
        if (change != ReadingContentChange.Appended)
        {
            _adorner?.SetMatches([], -1);
            StopDeferring();
            _revealed = null;
        }
        if (change == ReadingContentChange.Replaced)
        {
            _replacements++;
        }
        TextChanged?.Invoke();
    }

    private sealed record Revealed(int Offset, int Length, TextPointer Start, TextPointer End);

    private sealed record RestorePoint(TextPointer Start, TextPointer End, int Replacements);
}
