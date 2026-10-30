// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace SlateWindows;

/// <summary>
/// WB-22 (#1112): the editor as a find surface. Headless when
/// <paramref name="editor"/> is null (the pure facts read its recorded
/// selection and highlights); otherwise the selection is the editor's and
/// the matches are drawn by a <see cref="FindMatchBackgroundRenderer"/>
/// behind the text while the bar is open.
/// </summary>
internal sealed class EditorFindSurface : IFindInNoteSurface
{
    private readonly TextDocument _document;
    private readonly SlateTextEditor? _editor;
    private FindMatchBackgroundRenderer? _renderer;
    private (int Offset, int Length) _headlessSelection;

    public EditorFindSurface(TextDocument document, SlateTextEditor? editor)
    {
        _document = document;
        _editor = editor;
        _document.Changed += OnDocumentChanged;
    }

    public FindInNoteMode Mode => FindInNoteMode.Editor;

    public bool IsSearchable => true;

    public ITextSource Text => _document;

    public object Stamp => _document.Version;

    public TextDocument? Document => _document;

    public (int Offset, int Length) Selection => _editor is { } editor
        ? (editor.SelectionStart, editor.SelectionLength)
        : _headlessSelection;

    /// <summary>The highlights last drawn (the pure facts read them).</summary>
    internal IReadOnlyList<(int Offset, int Length)> Highlighted { get; private set; } = [];

    internal FindMatchBackgroundRenderer? Renderer => _renderer;

    /// <summary>A headless fact's caret move.</summary>
    internal void SetHeadlessSelection(int offset, int length) => _headlessSelection = (offset, length);

    public event Action? TextChanged;

    public void Reveal(int offset, int length)
    {
        if (_editor is { } editor)
        {
            editor.Select(offset, length);
            editor.TextArea.Caret.BringCaretToView();
        }
        else
        {
            _headlessSelection = (offset, length);
        }
    }

    public void Highlight(IReadOnlyList<(int Offset, int Length)> matches, int current)
    {
        Highlighted = matches;
        if (_editor is not { } editor)
        {
            return;
        }
        if (_renderer is null)
        {
            _renderer = new FindMatchBackgroundRenderer(editor, _document);
            editor.TextArea.TextView.BackgroundRenderers.Add(_renderer);
        }
        _renderer.SetMatches(matches);
        editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
    }

    public void ClearHighlights()
    {
        Highlighted = [];
        if (_editor is { } editor && _renderer is not null)
        {
            _ = editor.TextArea.TextView.BackgroundRenderers.Remove(_renderer);
            editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
            _renderer = null;
        }
    }

    /// <summary>The selection as anchors, so a restore follows every edit
    /// made while the bar was open.</summary>
    public object CaptureRestorePoint()
    {
        (int offset, int length) = Selection;
        return new RestorePoint(_document, Anchor(offset), Anchor(offset + length));
    }

    public object RebaseRestorePoint(object restorePoint) =>
        restorePoint is RestorePoint point && !ReferenceEquals(point.Document, _document)
            ? new RestorePoint(_document, Anchor(point.Start.Offset), Anchor(point.End.Offset))
            : restorePoint;

    public void Restore(object restorePoint)
    {
        if (restorePoint is not RestorePoint point)
        {
            return;
        }
        int start = Math.Clamp(point.Start.Offset, 0, _document.TextLength);
        int end = Math.Clamp(point.End.Offset, start, _document.TextLength);
        if (_editor is { } editor)
        {
            editor.Select(start, end - start);
        }
        else
        {
            _headlessSelection = (start, end - start);
        }
    }

    public void ReturnFocus() => _editor?.FocusInputOwner();

    /// <summary>Stops following the document (the bar detached it).</summary>
    internal void Detach()
    {
        ClearHighlights();
        _document.Changed -= OnDocumentChanged;
    }

    private TextAnchor Anchor(int offset)
    {
        TextAnchor anchor = _document.CreateAnchor(Math.Clamp(offset, 0, _document.TextLength));
        anchor.MovementType = AnchorMovementType.AfterInsertion;
        anchor.SurviveDeletion = true;
        return anchor;
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e) => TextChanged?.Invoke();

    private sealed record RestorePoint(TextDocument Document, TextAnchor Start, TextAnchor End);
}

/// <summary>
/// WB-22: the editor's find matches, filled behind the text in the
/// selection layer with the theme's <c>Slate.FindMatchBrush</c>.
/// </summary>
internal sealed class FindMatchBackgroundRenderer : IBackgroundRenderer
{
    private readonly FrameworkElement _owner;
    private readonly TextSegmentCollection<TextSegment> _segments;

    /// <summary>The segments are tied to <paramref name="document"/>, so the
    /// drawn matches follow the reader's typing until the debounced
    /// recount replaces them.</summary>
    public FindMatchBackgroundRenderer(FrameworkElement owner, TextDocument document)
    {
        _owner = owner;
        _segments = new TextSegmentCollection<TextSegment>(document);
    }

    public KnownLayer Layer => KnownLayer.Selection;

    internal int Count => _segments.Count;

    public void SetMatches(IReadOnlyList<(int Offset, int Length)> matches)
    {
        _segments.Clear();
        foreach ((int offset, int length) in matches)
        {
            _segments.Add(new TextSegment { StartOffset = offset, Length = length });
        }
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_segments.Count == 0 || !textView.VisualLinesValid)
        {
            return;
        }
        Brush brush = _owner.TryFindResource("Slate.FindMatchBrush") as Brush ?? Brushes.Khaki;
        // The contrast themes outline the match in the system highlight
        // colour instead of relying on a fill (the light and dark themes'
        // outline brush is transparent).
        Pen? outline = _owner.TryFindResource("Slate.FindMatchOutlineBrush") is Brush stroke
            ? new Pen(stroke, 2)
            : null;
        int viewStart = textView.VisualLines[0].FirstDocumentLine.Offset;
        int viewEnd = textView.VisualLines[^1].LastDocumentLine.EndOffset;
        foreach (TextSegment segment in _segments.FindOverlappingSegments(viewStart, viewEnd - viewStart))
        {
            var builder = new BackgroundGeometryBuilder { AlignToWholePixels = true, CornerRadius = 1 };
            builder.AddSegment(textView, segment);
            if (builder.CreateGeometry() is { } geometry)
            {
                drawingContext.DrawGeometry(brush, outline, geometry);
            }
        }
    }
}
