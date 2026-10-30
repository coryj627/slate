// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
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

    /// <summary>The surface's selection in the index's offsets. A math match
    /// selects its whole element, so while the selection is still exactly
    /// what revealing one made, it is that match — found again in the current
    /// index, since a completion elsewhere can move the element's offset.</summary>
    public (int Offset, int Length) Selection
    {
        get
        {
            TextSelection selection = _surface.Selection;
            if (_revealed is { } revealed
                && selection.Start.CompareTo(revealed.Start) == 0
                && selection.End.CompareTo(revealed.End) == 0
                && Index.UnitStart(revealed.Unit) is >= 0 and int unitStart)
            {
                return (unitStart + revealed.Within, revealed.Length);
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
        _revealed = index.UnitAt(offset) is { } unit
            ? new Revealed(unit, offset - index.UnitStart(unit), length, _surface.Selection.Start, _surface.Selection.End)
            : null;
        if (_surface.DocumentRenderScope is { } scope)
        {
            // Only a rectangle that exists: none before layout, and an empty
            // default would scroll to the top.
            foreach (Rect rect in RectsOf(offset, length, scope, _surface.TransformToDescendant(scope)))
            {
                scope.BringIntoView(rect);
                break;
            }
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
        return new RestorePoint(
            selection.Start, selection.End, _surface.ReplacedRevision,
            PendingDiagramOf(selection.Start) ?? PendingDiagramOf(selection.End));
    }

    /// <summary>The surface keeps one document for its whole life, so a
    /// point taken on it stays on it.</summary>
    public object RebaseRestorePoint(object restorePoint) => restorePoint;

    /// <summary>Re-selects where opening found the selection unless a
    /// replacement (a merge or a model switch) moved every pointer since, or
    /// the pending diagram it lay in completed in place (the completion
    /// rewrote that paragraph's runs): then the caret stays where it is.</summary>
    public void Restore(object restorePoint)
    {
        if (restorePoint is RestorePoint { } point
            && point.Replacements == _surface.ReplacedRevision
            && (point.PendingDiagram is null || ReadingSemantics.PendingDiagramKeyOf(point.PendingDiagram) is not null))
        {
            _surface.Selection.Select(point.Start, point.End);
        }
    }

    /// <summary>The reading view is on screen while its bar is open, so the
    /// keys go straight back to it (as the editor surface's do); a landing
    /// request is the shell's alone (OD-12).</summary>
    public void ReturnFocus() => _surface.Focus();

    public void Detach()
    {
        _surface.ContentChanged -= OnContentChanged;
        ClearHighlights();
    }

    /// <summary>The embedded element a match at <paramref name="offset"/>
    /// marks whole, or null for plain text.</summary>
    internal UIElement? UnitAt(int offset) => Index.UnitAt(offset);

    /// <summary>The rectangles a match covers in <paramref name="scope"/>'s
    /// coordinates (<paramref name="transform"/>: the surface's to the
    /// scope's, taken once per drawing): a unit's element bounds, else one
    /// per line.</summary>
    internal IEnumerable<Rect> RectsOf(int offset, int length, UIElement scope, GeneralTransform? transform)
    {
        if (Index.UnitAt(offset) is { IsVisible: true } element)
        {
            if (element.TransformToVisual(scope) is { } toScope)
            {
                yield return toScope.TransformBounds(new Rect(element.RenderSize));
            }
            yield break;
        }
        if (transform is null)
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
        }
        if (change == ReadingContentChange.Replaced)
        {
            // Every pointer moved: no revealed match survives. A completion
            // keeps one whose element and selection it left alone.
            _revealed = null;
        }
        TextChanged?.Invoke();
    }

    private static Paragraph? PendingDiagramOf(TextPointer pointer) =>
        pointer.Paragraph is { } paragraph && ReadingSemantics.PendingDiagramKeyOf(paragraph) is not null ? paragraph : null;

    /// <summary>A revealed math match: its element, where in the element's
    /// source it starts, and the selection the reveal made.</summary>
    private sealed record Revealed(UIElement Unit, int Within, int Length, TextPointer Start, TextPointer End);

    private sealed record RestorePoint(TextPointer Start, TextPointer End, int Replacements, Paragraph? PendingDiagram);
}
