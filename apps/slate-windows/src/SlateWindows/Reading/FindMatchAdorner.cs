// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SlateWindows.Reading;

/// <summary>
/// WB-22 (#1112): the reading view's find matches, drawn OVER the text as
/// rings in the render scope's own adorner layer — the template's
/// <c>ScrollContentPresenter</c>'s, where WPF draws its caret — so they
/// are clipped to the viewport. Not hit-testable, not focusable and
/// without an automation peer: the matches add no element anywhere in the
/// tree. A ring, not a fill: a fill over the glyphs would cover them. Only
/// the matches the viewport shows are drawn, again on every scroll.
/// </summary>
internal sealed class FindMatchAdorner : Adorner
{
    private readonly ReadingFindSurface _owner;
    private readonly ScrollViewer? _scroller;
    private IReadOnlyList<(int Offset, int Length)> _matches = [];
    private int _current = -1;

    public FindMatchAdorner(UIElement renderScope, ReadingFindSurface owner)
        : base(renderScope)
    {
        _owner = owner;
        IsHitTestVisible = false;
        Focusable = false;
        _scroller = owner.Surface.ContentScrollViewer;
        if (_scroller is not null)
        {
            _scroller.ScrollChanged += OnScrollChanged;
        }
        if (renderScope is FrameworkElement scope)
        {
            scope.SizeChanged += OnSizeChanged;
        }
    }

    internal int Count => _matches.Count;

    internal void SetMatches(IReadOnlyList<(int Offset, int Length)> matches, int current)
    {
        _matches = matches;
        _current = current;
        InvalidateVisual();
    }

    /// <summary>Unhooks the scroll and size handlers (the surface removed
    /// it).</summary>
    internal void Retire()
    {
        if (_scroller is not null)
        {
            _scroller.ScrollChanged -= OnScrollChanged;
        }
        if (AdornedElement is FrameworkElement scope)
        {
            scope.SizeChanged -= OnSizeChanged;
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_matches.Count == 0)
        {
            return;
        }
        Brush brush = _owner.Surface.TryFindResource("Slate.FindMatchRingBrush") as Brush ?? Brushes.DarkOrange;
        var ring = new Pen(brush, 2);
        var currentRing = new Pen(brush, 3);
        (int first, int last) = _owner.VisibleOffsets();
        for (int i = FirstEndingAtOrAfter(first); i < _matches.Count && _matches[i].Offset <= last; i++)
        {
            (int offset, int length) = _matches[i];
            foreach (Rect rect in _owner.RectsOf(offset, length, AdornedElement))
            {
                drawingContext.DrawRectangle(null, i == _current ? currentRing : ring, rect);
            }
        }
    }

    private int FirstEndingAtOrAfter(int offset)
    {
        int low = 0;
        int high = _matches.Count;
        while (low < high)
        {
            int middle = (low + high) / 2;
            if (_matches[middle].Offset + _matches[middle].Length >= offset)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }
        return low;
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e) => InvalidateVisual();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => InvalidateVisual();
}
