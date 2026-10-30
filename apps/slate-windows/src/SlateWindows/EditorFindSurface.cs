// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
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

    /// <summary>
    /// The editing state Close gives back, in two forms. EXACT: AvalonEdit's
    /// own immutable selection, the caret's full position (visual column and
    /// end-of-line affinity included) and its desired column, with the
    /// document version they belong to — restored verbatim when the text is
    /// unchanged, the common case. ANCHORED: the selection's shape as anchors
    /// (where it was anchored, its active end, the caret, a rectangle's
    /// visual columns) — used when the text changed while the bar was open,
    /// so the restore follows every edit and never resurrects a selection an
    /// edit collapsed.
    /// </summary>
    public object CaptureRestorePoint()
    {
        if (_editor is not { } editor)
        {
            (int offset, int length) = _headlessSelection;
            return new RestorePoint(
                _document, Anchor(offset), Anchor(offset + length), Anchor(offset + length), -1, null, null);
        }
        TextArea area = editor.TextArea;
        var exact = new ExactState(
            area, _document.Version, area.Selection, area.Caret.Position, area.Caret.DesiredXPos);
        ICSharpCode.AvalonEdit.Editing.Selection selection = area.Selection;
        int caret = area.Caret.Offset;
        // The caret's column when it sits past its line's end (virtual space).
        int caretColumn = area.Caret.IsInVirtualSpace ? area.Caret.VisualColumn : -1;
        // A rectangle is kept even when AvalonEdit calls it empty (one lying
        // wholly past its lines' ends): typing into it still edits every line.
        if (selection is RectangleSelection)
        {
            return new RestorePoint(
                _document,
                Anchor(_document.GetOffset(selection.StartPosition.Location)),
                Anchor(_document.GetOffset(selection.EndPosition.Location)),
                Anchor(caret),
                caretColumn,
                new RectangleColumns(
                    VisualColumnOf(area, selection.StartPosition), VisualColumnOf(area, selection.EndPosition)),
                exact);
        }
        if (selection.IsEmpty)
        {
            return new RestorePoint(_document, Anchor(caret), Anchor(caret), Anchor(caret), caretColumn, null, exact);
        }
        int start = _document.GetOffset(selection.StartPosition.Location);
        int end = _document.GetOffset(selection.EndPosition.Location);
        return new RestorePoint(_document, Anchor(start), Anchor(end), Anchor(caret), caretColumn, null, exact);
    }

    /// <summary>A position's visual column; one built from a line and a
    /// column alone carries none (-1), so the visual line resolves it.</summary>
    private static int VisualColumnOf(TextArea area, TextViewPosition position) =>
        position.VisualColumn >= 0
            ? position.VisualColumn
            : area.TextView
                .GetOrConstructVisualLine(area.Document.GetLineByNumber(position.Line))
                .ValidateVisualColumn(position, area.Selection.EnableVirtualSpace);

    public object RebaseRestorePoint(object restorePoint) =>
        restorePoint is RestorePoint point && !ReferenceEquals(point.Document, _document)
            ? point with
            {
                Document = _document,
                Start = Anchor(point.Start.Offset),
                End = Anchor(point.End.Offset),
                Caret = Anchor(point.Caret.Offset),
                // Another document: only the anchored form can carry over.
                Exact = null,
            }
            : restorePoint;

    public void Restore(object restorePoint)
    {
        if (restorePoint is not RestorePoint point)
        {
            return;
        }
        int start = Math.Clamp(point.Start.Offset, 0, _document.TextLength);
        int end = Math.Clamp(point.End.Offset, 0, _document.TextLength);
        int caret = Math.Clamp(point.Caret.Offset, 0, _document.TextLength);
        if (_editor is not { } editor)
        {
            _headlessSelection = (Math.Min(start, end), Math.Abs(end - start));
            return;
        }
        TextArea area = editor.TextArea;
        if (point.Exact is { } exact
            && ReferenceEquals(exact.Area, area)
            && ReferenceEquals(point.Document, _document)
            && exact.Version.BelongsToSameDocumentAs(_document.Version)
            && exact.Version.CompareAge(_document.Version) == 0)
        {
            // Nothing changed while the bar was open: give back exactly what
            // AvalonEdit had (the caret first — setting it clears the
            // selection — and the desired column last, which the caret's
            // setter resets).
            area.Caret.Position = exact.CaretPosition;
            area.Selection = exact.Selection;
            area.Caret.DesiredXPos = exact.DesiredXPos;
            return;
        }
        if (point.CaretColumn >= 0)
        {
            area.Caret.Position = new TextViewPosition(_document.GetLocation(caret), point.CaretColumn);
        }
        else
        {
            area.Caret.Offset = caret;
        }
        if (point.Rectangle is { } columns)
        {
            area.Selection = new RectangleSelection(
                area,
                new TextViewPosition(_document.GetLocation(start), columns.Start),
                new TextViewPosition(_document.GetLocation(end), columns.End));
        }
        else if (start == end)
        {
            // An empty linear selection, or one an edit collapsed while the
            // bar was open: nothing to give back but the caret.
            area.ClearSelection();
        }
        else
        {
            // Anchored at Start, active at End: a backward selection stays
            // backward.
            area.Selection = ICSharpCode.AvalonEdit.Editing.Selection.Create(area, start, end);
        }
        // The caret is placed first: moving it after the selection is set
        // would clear the selection.
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

    /// <summary>Start is where the selection was anchored, End its active
    /// end (either may be the greater).</summary>
    private sealed record RestorePoint(
        TextDocument Document,
        TextAnchor Start,
        TextAnchor End,
        TextAnchor Caret,
        int CaretColumn,
        RectangleColumns? Rectangle,
        ExactState? Exact);

    /// <summary>AvalonEdit's own state at the open, valid while the document
    /// is at <see cref="Version"/> in the same text area.</summary>
    private sealed record ExactState(
        TextArea Area,
        ITextSourceVersion Version,
        ICSharpCode.AvalonEdit.Editing.Selection Selection,
        TextViewPosition CaretPosition,
        double DesiredXPos);

    private sealed record RectangleColumns(int Start, int End);
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
        // A view with no height lays out no lines yet reports them valid.
        if (_segments.Count == 0 || !textView.VisualLinesValid || textView.VisualLines.Count == 0)
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
