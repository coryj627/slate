// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "w2-canonical-highlight")]
public class CanonicalHighlightCensus
{
    [Fact]
    public void WindowRetainsCanonicalByteSpansAndUnicodeUtf16Coordinates()
    {
        const string text = "before\n\n## Héading 😀\n\n[[目标]] #tag `code`\n";
        using var session = new AvalonDocumentBufferSession(text, _ => { });

        int dirty = text.IndexOf("目标", StringComparison.Ordinal);
        EditorHighlightWindow window = session.HighlightInRange(dirty, dirty + 2);
        RangedHighlight canonical = session.BufferForCensus.HighlightInRange(
            checked((uint)dirty),
            checked((uint)(dirty + 2)));

        Assert.Same(window, session.LatestHighlightWindow);
        Assert.All(
            window.Spans,
            span =>
            {
                Assert.InRange(span.StartByte, canonical.AppliedStart, canonical.AppliedEnd);
                Assert.InRange(span.EndByte, canonical.AppliedStart, canonical.AppliedEnd);
            });
        Assert.Equal(
            canonical.Spans.Select(span => (span.StartByte, span.EndByte, span.Kind)),
            window.Spans.Select(span => (span.StartByte, span.EndByte, span.Kind)));
        foreach (EditorSemanticSpan span in window.Spans)
        {
            Assert.Equal(
                (int)session.BufferForCensus.ByteToUtf16(span.StartByte),
                span.StartUtf16);
            Assert.Equal(
                (int)session.BufferForCensus.ByteToUtf16(span.EndByte),
                span.StartUtf16 + span.LengthUtf16);
        }
    }

    [Fact]
    public void MapperHandlesAsciiTwoByteThreeByteAndAstralBoundaries()
    {
        const string text = "aé中😀z";
        uint end = checked((uint)Encoding.UTF8.GetByteCount(text));
        EditorSemanticSpan[] mapped = EditorSpanMapper.MapWindow(
            text,
            7,
            100,
            [
                new EditorSpan(101, 103, new EditorSpanKind.Emphasis()),
                new EditorSpan(103, 106, new EditorSpanKind.Strong()),
                new EditorSpan(106, 110, new EditorSpanKind.Tag()),
                new EditorSpan(100, checked(100 + end), new EditorSpanKind.Heading(2)),
            ]);

        Assert.Equal((8, 1), (mapped[0].StartUtf16, mapped[0].LengthUtf16));
        Assert.Equal((9, 1), (mapped[1].StartUtf16, mapped[1].LengthUtf16));
        Assert.Equal((10, 2), (mapped[2].StartUtf16, mapped[2].LengthUtf16));
        Assert.Equal((7, 6), (mapped[3].StartUtf16, mapped[3].LengthUtf16));
    }

    [Fact]
    public void PaletteMapsEveryCanonicalKindWithoutMarkdownClassification()
    {
        EditorSpanKind[] uncolored =
        [
            new EditorSpanKind.Emphasis(),
            new EditorSpanKind.Strong(),
            new EditorSpanKind.Strikethrough(),
            new EditorSpanKind.Link(),
            new EditorSpanKind.Image(),
            new EditorSpanKind.BlockQuote(),
        ];
        Assert.All(uncolored, kind => Assert.Null(EditorSyntaxPalette.ResourceKeyFor(kind)));

        Assert.Equal(
            EditorSyntaxPalette.MetadataBrushKey,
            EditorSyntaxPalette.ResourceKeyFor(new EditorSpanKind.Frontmatter()));
        Assert.Equal(
            EditorSyntaxPalette.MetadataBrushKey,
            EditorSyntaxPalette.ResourceKeyFor(new EditorSpanKind.Comment()));
        Assert.Equal(
            EditorSyntaxPalette.MetadataBrushKey,
            EditorSyntaxPalette.ResourceKeyFor(new EditorSpanKind.Citation()));
        Assert.Equal(
            EditorSyntaxPalette.HeadingBrushKey,
            EditorSyntaxPalette.ResourceKeyFor(new EditorSpanKind.Heading(6)));
        Assert.Equal(
            EditorSyntaxPalette.CodeBrushKey,
            EditorSyntaxPalette.ResourceKeyFor(new EditorSpanKind.InlineCode()));
        Assert.Equal(
            EditorSyntaxPalette.CodeBrushKey,
            EditorSyntaxPalette.ResourceKeyFor(new EditorSpanKind.CodeFence()));
        Assert.Equal(
            EditorSyntaxPalette.CodeBrushKey,
            EditorSyntaxPalette.ResourceKeyFor(
                new EditorSpanKind.Code(new TokenKind.Keyword())));
        Assert.Equal(
            EditorSyntaxPalette.WikilinkBrushKey,
            EditorSyntaxPalette.ResourceKeyFor(new EditorSpanKind.Wikilink()));
        Assert.Equal(
            EditorSyntaxPalette.WikilinkBrushKey,
            EditorSyntaxPalette.ResourceKeyFor(new EditorSpanKind.Embed()));
        Assert.Equal(
            EditorSyntaxPalette.TagBrushKey,
            EditorSyntaxPalette.ResourceKeyFor(new EditorSpanKind.Tag()));
    }

    [Fact]
    public void CoordinatorRetainsSameWindowItGivesTheColorizerAndInvalidatesOnEdit()
    {
        RunOnSta(() =>
        {
            const string text = "# Heading\n\nBody with [[link]] and #tag.\n";
            using var session = new AvalonDocumentBufferSession(text, _ => { });
            var editor = new SlateTextEditor { Document = session.Document };
            using var coordinator = new AvalonHighlightingCoordinator(
                editor,
                session,
                TimeSpan.FromHours(1));

            EditorHighlightWindow window = coordinator.RefreshRangeForCensus(0, text.Length);
            Assert.Same(window, session.LatestHighlightWindow);
            Assert.Same(window, coordinator.ColorizerWindowForCensus);
            Assert.NotEmpty(window.Spans);

            session.Document.Insert(text.IndexOf("Body", StringComparison.Ordinal), "new ");
            Assert.Null(session.LatestHighlightWindow);
            Assert.Null(coordinator.ColorizerWindowForCensus);
        });
    }

    [Fact]
    public void LoadedCoordinatorQuiescesAndRefreshesForEditScrollAndResize()
    {
        RunOnSta(() =>
        {
            string text = string.Join(
                "\n",
                Enumerable.Range(1, 160).Select(index => $"## Heading {index}\nBody [[link-{index}]] #tag"));
            using var session = new AvalonDocumentBufferSession(text, _ => { });
            var editor = new SlateTextEditor
            {
                Document = session.Document,
                HighlightSession = session,
            };
            InstallPaletteResources(editor);
            var window = new Window
            {
                Content = editor,
                Width = 480,
                Height = 240,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.ToolWindow,
            };

            try
            {
                window.Show();
                window.UpdateLayout();
                RunDispatcherFor(TimeSpan.FromMilliseconds(200));
                AvalonHighlightingCoordinator coordinator = Assert.IsType<AvalonHighlightingCoordinator>(
                    editor.HighlightingForCensus);
                Assert.True(coordinator.RefreshCountForCensus > 0);
                Assert.Same(session.LatestHighlightWindow, coordinator.ColorizerWindowForCensus);

                int settled = coordinator.RefreshCountForCensus;
                RunDispatcherFor(TimeSpan.FromMilliseconds(160));
                Assert.Equal(settled, coordinator.RefreshCountForCensus);

                session.Document.Insert(0, "x");
                Assert.Null(session.LatestHighlightWindow);
                Assert.Null(coordinator.ColorizerWindowForCensus);
                RunDispatcherFor(TimeSpan.FromMilliseconds(100));
                Assert.True(coordinator.RefreshCountForCensus > settled);
                Assert.Same(session.LatestHighlightWindow, coordinator.ColorizerWindowForCensus);

                int afterEdit = coordinator.RefreshCountForCensus;
                editor.ScrollToLine(120);
                window.UpdateLayout();
                RunDispatcherFor(TimeSpan.FromMilliseconds(100));
                Assert.True(coordinator.RefreshCountForCensus > afterEdit);

                int afterScroll = coordinator.RefreshCountForCensus;
                window.Width += 80;
                window.UpdateLayout();
                RunDispatcherFor(TimeSpan.FromMilliseconds(100));
                Assert.True(coordinator.RefreshCountForCensus > afterScroll);

                int afterResize = coordinator.RefreshCountForCensus;
                RunDispatcherFor(TimeSpan.FromMilliseconds(160));
                Assert.Equal(afterResize, coordinator.RefreshCountForCensus);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void LoadedEditorRebindsAfterEitherSessionOrDocumentChangesLast()
    {
        RunOnSta(() =>
        {
            using var first = new AvalonDocumentBufferSession("# First\n", _ => { });
            using var second = new AvalonDocumentBufferSession("# Second\n", _ => { });
            var editor = new SlateTextEditor
            {
                Document = first.Document,
                HighlightSession = first,
            };
            InstallPaletteResources(editor);
            var window = new Window
            {
                Content = editor,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.ToolWindow,
            };

            try
            {
                window.Show();
                window.UpdateLayout();
                Assert.NotNull(editor.HighlightingForCensus);

                editor.HighlightSession = second;
                Assert.Null(editor.HighlightingForCensus);
                editor.Document = second.Document;
                Assert.NotNull(editor.HighlightingForCensus);

                editor.Document = first.Document;
                Assert.Null(editor.HighlightingForCensus);
                editor.HighlightSession = first;
                Assert.NotNull(editor.HighlightingForCensus);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ViewportRequestIncludesFortyLineMargin()
    {
        RunOnSta(() =>
        {
            var document = new ICSharpCode.AvalonEdit.Document.TextDocument(
                string.Join("\n", Enumerable.Range(1, 120).Select(index => $"line {index}")));
            var editor = new TextEditor { Document = document };

            (int start, int end) = AvalonHighlightingCoordinator.VisibleRangeWithMargin(
                document,
                editor.TextArea.TextView);

            Assert.Equal(0, start);
            Assert.Equal(document.GetLineByNumber(81).EndOffset, end);
            Assert.True(end < document.TextLength);
        });
    }

    private static void RunDispatcherFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Send)
        {
            Interval = duration,
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void InstallPaletteResources(FrameworkElement owner)
    {
        foreach (string key in new[]
        {
            EditorSyntaxPalette.HeadingBrushKey,
            EditorSyntaxPalette.CodeBrushKey,
            EditorSyntaxPalette.WikilinkBrushKey,
            EditorSyntaxPalette.TagBrushKey,
            EditorSyntaxPalette.MetadataBrushKey,
        })
        {
            owner.Resources[key] = Brushes.Black;
        }
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA highlight census timed out.");
        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException(failure.ToString());
        }
    }
}
