// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// UTF-8 byte span to UTF-16 editor mapping. This is coordinate conversion,
/// never Markdown classification: <see cref="EditorSpanKind"/> arrives from
/// the canonical Rust API unchanged.
/// </summary>
internal static class EditorSpanMapper
{
    public static EditorSemanticSpan[] MapWindow(
        string windowText,
        int windowStartUtf16,
        uint windowStartByte,
        IReadOnlyList<EditorSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(windowText);
        ArgumentNullException.ThrowIfNull(spans);
        if (spans.Count == 0)
        {
            return [];
        }

        var needed = new HashSet<uint>();
        foreach (EditorSpan span in spans)
        {
            needed.Add(span.StartByte);
            needed.Add(span.EndByte);
        }

        var mapped = new Dictionary<uint, int>();
        uint byteOffset = windowStartByte;
        int utf16Offset = windowStartUtf16;
        if (needed.Contains(byteOffset))
        {
            mapped[byteOffset] = utf16Offset;
        }

        foreach (Rune rune in windowText.EnumerateRunes())
        {
            byteOffset = checked(byteOffset + (uint)rune.Utf8SequenceLength);
            utf16Offset += rune.Utf16SequenceLength;
            if (needed.Contains(byteOffset))
            {
                mapped[byteOffset] = utf16Offset;
            }
        }

        var result = new EditorSemanticSpan[spans.Count];
        for (int index = 0; index < spans.Count; index++)
        {
            EditorSpan span = spans[index];
            if (!mapped.TryGetValue(span.StartByte, out int start)
                || !mapped.TryGetValue(span.EndByte, out int end)
                || end <= start)
            {
                throw new InvalidOperationException(
                    $"Canonical span [{span.StartByte}, {span.EndByte}) was outside "
                    + "the returned highlight window or not scalar-aligned.");
            }

            result[index] = new EditorSemanticSpan(
                start,
                end - start,
                span.StartByte,
                span.EndByte,
                span.Kind);
        }

        return result;
    }
}

/// <summary>
/// Windows presentation roles matching macOS EditorSyntaxPalette. Values are
/// Slate theme resources; adding Markdown recognition here is forbidden.
/// </summary>
internal static class EditorSyntaxPalette
{
    internal const string HeadingBrushKey = "Slate.EditorHeadingBrush";
    internal const string CodeBrushKey = "Slate.EditorCodeBrush";
    internal const string WikilinkBrushKey = "Slate.EditorWikilinkBrush";
    internal const string TagBrushKey = "Slate.EditorTagBrush";
    internal const string MetadataBrushKey = "Slate.EditorMetadataBrush";

    public static string? ResourceKeyFor(EditorSpanKind kind) => kind switch
    {
        EditorSpanKind.Emphasis
            or EditorSpanKind.Strong
            or EditorSpanKind.Strikethrough
            or EditorSpanKind.Link
            or EditorSpanKind.Image
            or EditorSpanKind.BlockQuote => null,
        EditorSpanKind.Frontmatter
            or EditorSpanKind.Comment
            or EditorSpanKind.Citation => MetadataBrushKey,
        EditorSpanKind.Heading => HeadingBrushKey,
        EditorSpanKind.CodeFence
            or EditorSpanKind.InlineCode
            or EditorSpanKind.Code => CodeBrushKey,
        EditorSpanKind.Wikilink
            or EditorSpanKind.Embed => WikilinkBrushKey,
        EditorSpanKind.Tag => TagBrushKey,
        _ => throw new InvalidOperationException($"Unmapped canonical editor span kind {kind}."),
    };

    public static Brush? BrushFor(FrameworkElement owner, EditorSpanKind kind)
    {
        string? key = ResourceKeyFor(kind);
        if (key is null)
        {
            return null;
        }

        return owner.TryFindResource(key) as Brush
            ?? throw new InvalidOperationException($"Editor palette resource {key} is missing.");
    }
}

/// <summary>
/// Avalon apply layer. It reads an immutable canonical window and changes
/// visual-line foregrounds only; it never mutates TextDocument or tokenizes.
/// </summary>
internal sealed class AvalonCanonicalSpanColorizer : DocumentColorizingTransformer
{
    private readonly SlateTextEditor _editor;
    private EditorHighlightWindow? _window;

    public AvalonCanonicalSpanColorizer(SlateTextEditor editor)
    {
        _editor = editor;
    }

    public void SetWindow(EditorHighlightWindow window) => _window = window;

    public void ClearWindow() => _window = null;

    internal EditorHighlightWindow? WindowForCensus => _window;

    protected override void ColorizeLine(DocumentLine line)
    {
        EditorHighlightWindow? window = _window;
        if (window is null)
        {
            return;
        }

        int lineStart = line.Offset;
        int lineEnd = line.EndOffset;
        foreach (EditorSemanticSpan span in window.Spans)
        {
            int spanEnd = span.StartUtf16 + span.LengthUtf16;
            int start = Math.Max(lineStart, span.StartUtf16);
            int end = Math.Min(lineEnd, spanEnd);
            if (end <= start)
            {
                continue;
            }

            Brush? brush = EditorSyntaxPalette.BrushFor(_editor, span.Kind);
            if (brush is null)
            {
                continue;
            }

            ChangeLinePart(
                start,
                end,
                element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }
}

/// <summary>
/// One editor's viewport/highlight lifecycle. Requests cover visible lines
/// plus a line margin and are reissued after edit, scroll, resize, and theme
/// changes. A short debounce collapses scroll/typing bursts.
/// </summary>
internal sealed class AvalonHighlightingCoordinator : IDisposable
{
    internal static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(40);
    internal const int MarginLines = 40;

    private readonly SlateTextEditor _editor;
    private readonly AvalonDocumentBufferSession _session;
    private readonly AvalonCanonicalSpanColorizer _colorizer;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    public AvalonHighlightingCoordinator(
        SlateTextEditor editor,
        AvalonDocumentBufferSession session,
        TimeSpan? debounce = null)
    {
        _editor = editor;
        _session = session;
        _colorizer = new AvalonCanonicalSpanColorizer(editor);
        _timer = new DispatcherTimer(DispatcherPriority.Background, editor.Dispatcher)
        {
            Interval = debounce ?? DefaultDebounce,
        };
        _timer.Tick += Timer_Tick;
        editor.TextArea.TextView.LineTransformers.Add(_colorizer);
        editor.TextArea.TextView.ScrollOffsetChanged += TextView_ScrollOffsetChanged;
        editor.SizeChanged += Editor_SizeChanged;
        session.HighlightInvalidated += Session_HighlightInvalidated;
        ThemeManager.ResourcesChanged += ThemeManager_ResourcesChanged;
        Schedule(immediate: true);
    }

    internal EditorHighlightWindow RefreshRangeForCensus(int startUtf16, int endUtf16)
    {
        ThrowIfDisposed();
        _timer.Stop();
        EditorHighlightWindow window = _session.HighlightInRange(startUtf16, endUtf16);
        _colorizer.SetWindow(window);
        RefreshCountForCensus++;
        _editor.TextArea.TextView.Redraw(
            window.AppliedStartUtf16,
            window.AppliedLengthUtf16,
            DispatcherPriority.Render);
        return window;
    }

    internal int RefreshCountForCensus { get; private set; }

    internal EditorHighlightWindow? ColorizerWindowForCensus => _colorizer.WindowForCensus;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
        _editor.TextArea.TextView.ScrollOffsetChanged -= TextView_ScrollOffsetChanged;
        _editor.SizeChanged -= Editor_SizeChanged;
        _session.HighlightInvalidated -= Session_HighlightInvalidated;
        ThemeManager.ResourcesChanged -= ThemeManager_ResourcesChanged;
        _editor.TextArea.TextView.LineTransformers.Remove(_colorizer);
    }

    private void Schedule(bool immediate)
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        if (immediate)
        {
            _editor.Dispatcher.BeginInvoke(
                RefreshVisibleWindow,
                DispatcherPriority.Background);
        }
        else
        {
            _timer.Start();
        }
    }

    private void RefreshVisibleWindow()
    {
        if (_disposed || !_editor.IsLoaded || _editor.Document is null)
        {
            return;
        }

        (int start, int end) = VisibleRangeWithMargin(
            _editor.Document,
            _editor.TextArea.TextView);
        RefreshRangeForCensus(start, end);
    }

    internal static (int Start, int End) VisibleRangeWithMargin(
        TextDocument document,
        TextView textView)
    {
        if (document.TextLength == 0)
        {
            return (0, 0);
        }

        int firstLine = 1;
        int lastLine = Math.Min(document.LineCount, 1 + MarginLines);
        if (textView.VisualLinesValid && textView.VisualLines.Count > 0)
        {
            firstLine = textView.VisualLines[0].FirstDocumentLine.LineNumber;
            lastLine = textView.VisualLines[^1].LastDocumentLine.LineNumber;
        }

        firstLine = Math.Max(1, firstLine - MarginLines);
        lastLine = Math.Min(document.LineCount, lastLine + MarginLines);
        DocumentLine first = document.GetLineByNumber(firstLine);
        DocumentLine last = document.GetLineByNumber(lastLine);
        return (first.Offset, last.EndOffset);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _timer.Stop();
        RefreshVisibleWindow();
    }

    private void TextView_ScrollOffsetChanged(object? sender, EventArgs e) => Schedule(immediate: false);

    private void Editor_SizeChanged(object sender, SizeChangedEventArgs e) => Schedule(immediate: false);

    private void Session_HighlightInvalidated(object? sender, EventArgs e)
    {
        _colorizer.ClearWindow();
        _editor.TextArea.TextView.Redraw(DispatcherPriority.Render);
        Schedule(immediate: false);
    }

    private void ThemeManager_ResourcesChanged(object? sender, EventArgs e)
    {
        _editor.TextArea.TextView.Redraw(DispatcherPriority.Render);
        Schedule(immediate: false);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}