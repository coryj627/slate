// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using uniffi.slate_uniffi;
using WpfList = System.Windows.Documents.List;
using WpfTable = System.Windows.Documents.Table;

namespace SlateWindows.Reading;

/// <summary>
/// Open-list state carried ACROSS chunked builds (§W3-1 item 9). After
/// the surface's merge the list objects live on in the persistent
/// document, so a later chunk appends items into them directly — list
/// structure never depends on where a chunk boundary fell, and the
/// render ceiling can stay absolute.
/// </summary>
internal sealed class ReadingListBuildContext
{
    /// <summary>Stack[d] holds the open list accepting items at depth
    /// d and the last item added to it (the parent of depth d+1).</summary>
    internal List<(WpfList List, ListItem? LastItem)> Stack { get; } = new();
}

/// <summary>The built reading document plus its navigation index.</summary>
internal sealed class ReadingDocumentModel
{
    public ReadingDocumentModel(FlowDocument document, IReadOnlyList<ReadingLandmark> landmarks)
    {
        Document = document;
        Landmarks = landmarks;
    }

    public FlowDocument Document { get; }

    /// <summary>Document-ordered; the navigator's whole input.</summary>
    public IReadOnlyList<ReadingLandmark> Landmarks { get; }
}

/// <summary>
/// Core reading model → <see cref="FlowDocument"/> (W3-1, #728).
///
/// Consumes the zipped output of <c>ReadingBlocksSource</c> +
/// <c>ReadingInlineSegmentsSource</c> exactly as
/// `w3_inline_runs_spec.md` §10 binds it: every semantic decision —
/// what is a link, what it activates with, whether it resolves, what a
/// run's accessible text is — arrived precomputed from core. This file
/// applies attributes and builds WPF structure; it never parses,
/// splits, re-derives, or re-classifies (§10.8 census-enforced).
///
/// Threading: pure WPF object construction — the caller runs the FFI
/// calls off the dispatcher (§10.1) and this on it.
/// </summary>
internal static class ReadingDocumentBuilder
{
    public static ReadingDocumentModel Build(
        IReadOnlyList<(ReadingBlock Block, ReadingBlockInlines Inlines)> model) =>
        Build(model, new ReadingListBuildContext());

    public static ReadingDocumentModel Build(
        IReadOnlyList<(ReadingBlock Block, ReadingBlockInlines Inlines)> model,
        ReadingListBuildContext context)
    {
        var document = new FlowDocument
        {
            FontSize = 15,
            PagePadding = new Thickness(16),
            // Column layout off: the reading view is a single flow, and
            // multi-column reading order is a §W-C hazard.
            ColumnWidth = double.PositiveInfinity,
        };
        document.SetResourceReference(FlowDocument.FontFamilyProperty, "Slate.ReadingFontFamily");

        List<(WpfList List, ListItem? LastItem)> stack = context.Stack;

        foreach ((ReadingBlock block, ReadingBlockInlines inlines) in model)
        {
            // §10.5: a paragraph that IS one wikilink embed expands as a
            // card. Detection is core's — BlockEmbedKey, never a string
            // test here.
            if (inlines.BlockEmbedKey is { Length: > 0 } embedKey)
            {
                stack.Clear();
                document.Blocks.Add(EmbedCard(embedKey));
                continue;
            }

            if (block.Kind is ReadingBlockKind.ListItem listKind)
            {
                AppendListItem(document, stack, listKind, block, inlines);
                continue;
            }

            stack.Clear();
            document.Blocks.Add(NonListBlock(block, inlines));
        }

        return new ReadingDocumentModel(document, CollectLandmarks(document));
    }

    /// <summary>
    /// Depth-stack list construction: every level nests inside the last
    /// item of the level above it — structure, not indentation, is what
    /// the ListItem peers expose. Ordered-ness is compared AT THE
    /// ITEM'S OWN LEVEL, so an ordered sublist inside a bullet list
    /// closes nothing above it; a marker change at one level replaces
    /// only that level's list (two lists, two quick-nav stops — the AT
    /// convention). A list opened in an earlier chunk continues through
    /// <see cref="ReadingListBuildContext"/>: its object already lives
    /// in the surface's persistent document, and items append into it
    /// directly.
    /// </summary>
    private static void AppendListItem(
        FlowDocument document,
        List<(WpfList List, ListItem? LastItem)> stack,
        ReadingBlockKind.ListItem kind,
        ReadingBlock block,
        ReadingBlockInlines inlines)
    {
        // A level can only open one deeper than the deepest open level;
        // core emits authored depths, so the clamp fires only on input
        // this builder never sees from it (defensive).
        int depth = Math.Min(kind.Depth, stack.Count);

        // Levels deeper than this item close.
        if (stack.Count > depth + 1)
        {
            stack.RemoveRange(depth + 1, stack.Count - depth - 1);
        }

        // Marker change at this level: close and replace this level only.
        if (stack.Count == depth + 1
            && (stack[depth].List.MarkerStyle == TextMarkerStyle.Decimal) != kind.Ordered)
        {
            stack.RemoveAt(depth);
        }

        if (stack.Count == depth)
        {
            var list = new WpfList
            {
                MarkerStyle = kind.Ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            };
            ReadingSemantics.MarkList(list);
            if (depth > 0 && stack[depth - 1].LastItem is { } parent)
            {
                parent.Blocks.Add(list);
            }
            else
            {
                document.Blocks.Add(list);
            }
            stack.Add((list, null));
        }

        Paragraph content = InlineParagraph(
            inlines,
            taskRange: kind.Task is not null ? (block.ByteStart, block.ByteEnd) : null);
        var item = new ListItem(content);
        ReadingSemantics.MarkListItem(item);
        stack[depth].List.ListItems.Add(item);
        stack[depth] = (stack[depth].List, item);
    }

    private static Block NonListBlock(ReadingBlock block, ReadingBlockInlines inlines)
    {
        switch (block.Kind)
        {
            case ReadingBlockKind.Heading heading:
                {
                    Paragraph paragraph = InlineParagraph(inlines, taskRange: null);
                    paragraph.FontSize = heading.Level switch
                    {
                        1 => 26,
                        2 => 21,
                        3 => 18,
                        _ => 16,
                    };
                    paragraph.FontWeight = FontWeights.SemiBold;
                    // Narrator (and any future UIA consumer of the property)
                    // reads this; NVDA does not during linear reading — the
                    // measured limitation G21 records. The chorded commands
                    // are the cross-AT navigation path.
                    AutomationProperties.SetHeadingLevel(paragraph, ToHeadingLevel(heading.Level));
                    ReadingSemantics.MarkHeading(paragraph, heading.Level);
                    return paragraph;
                }

            case ReadingBlockKind.BlockQuote:
                {
                    Paragraph quote = InlineParagraph(inlines, taskRange: null);
                    quote.Padding = new Thickness(12, 2, 0, 2);
                    quote.BorderThickness = new Thickness(3, 0, 0, 0);
                    quote.SetResourceReference(Block.BorderBrushProperty, "Slate.AccentBrush");
                    return quote;
                }

            case ReadingBlockKind.CodeFence fence:
                return CodeFenceBlock(fence);

            case ReadingBlockKind.Table:
                return TableBlock(block);

            case ReadingBlockKind.ThematicBreak:
                return new BlockUIContainer(new Separator { Margin = new Thickness(0, 8, 0, 8) });

            case ReadingBlockKind.MathBlock:
            case ReadingBlockKind.Diagram:
            case ReadingBlockKind.Html:
            default:
                {
                    // Math (W3-2) and diagrams (W3-3) get their canonical
                    // renderers in their own PRs; HTML renders as source per
                    // the mac contract. Until then the block's source stays
                    // IN the text range, monospace — never silently absent.
                    if (inlines.Segments.Length == 0)
                    {
                        return MonospaceParagraph(block.Source.TrimEnd('\n', '\r'));
                    }
                    return InlineParagraph(inlines, taskRange: null);
                }
        }
    }

    /// <summary>
    /// W3-1 baseline for code fences, superseded by W3-4's token
    /// renderer: the interior is a monospace paragraph INSIDE the text
    /// range. The spike measured the alternative — a `BlockUIContainer`
    /// child — as silently absent from say-all ("landmarks missing:
    /// fn main"), and a reading view that skips code while reading is a
    /// correctness failure, not a styling gap.
    /// </summary>
    private static Block CodeFenceBlock(ReadingBlockKind.CodeFence fence)
    {
        Paragraph paragraph = MonospaceParagraph(fence.Interior.TrimEnd('\n', '\r'));
        ReadingSemantics.MarkCodeBlock(paragraph);
        return paragraph;
    }

    private static Paragraph MonospaceParagraph(string text)
    {
        var run = new Run(text);
        var paragraph = new Paragraph(run)
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
            Padding = new Thickness(8),
        };
        paragraph.SetResourceReference(Block.BackgroundProperty, "Slate.RaisedSurfaceBrush");
        return paragraph;
    }

    private static Block TableBlock(ReadingBlock block)
    {
        // Plain accessible table until W4-1's grid substrate (§10.7).
        ReadingTableCells? cells = SlateUniffiMethods.ReadingTableCells(block.Source);
        var table = new WpfTable();
        ReadingSemantics.MarkTable(table);
        var group = new TableRowGroup();
        table.RowGroups.Add(group);

        if (cells is null || (cells.Header.Length == 0 && cells.Rows.Length == 0))
        {
            group.Rows.Add(Row(new[] { block.Source }, header: false));
            return table;
        }

        int columns = Math.Max(
            cells.Header.Length,
            cells.Rows.Length == 0 ? 1 : cells.Rows.Max(r => r.Length));
        for (int i = 0; i < columns; i++)
        {
            table.Columns.Add(new TableColumn());
        }
        if (cells.Header.Length > 0)
        {
            group.Rows.Add(Row(cells.Header, header: true));
        }
        foreach (string[] row in cells.Rows)
        {
            group.Rows.Add(Row(row, header: false));
        }
        return table;
    }

    private static TableRow Row(IEnumerable<string> cells, bool header)
    {
        var row = new TableRow();
        foreach (string cell in cells)
        {
            var paragraph = new Paragraph(new Run(cell));
            if (header)
            {
                paragraph.FontWeight = FontWeights.SemiBold;
            }
            row.Cells.Add(new TableCell(paragraph) { Padding = new Thickness(6, 2, 6, 2) });
        }
        return row;
    }

    /// <summary>
    /// The embed card placeholder: a named, invokable button carrying
    /// core's cache key. The full card state machine (#598/#511 parity)
    /// is W3-5's; the W3-1 contract is that the card exists, is
    /// reachable, and activates through <c>ReadingMatchLink</c>.
    /// </summary>
    private static Block EmbedCard(string key)
    {
        var button = new Button
        {
            Content = key,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 6, 10, 6),
            Tag = key,
        };
        AutomationProperties.SetName(button, $"Embedded note {key}");
        AutomationProperties.SetAutomationId(button, "ReadingBlockEmbed");
        var container = new BlockUIContainer(button);
        ReadingSemantics.MarkEmbed(container);
        return container;
    }

    private static Paragraph InlineParagraph(
        ReadingBlockInlines inlines,
        (ulong Start, ulong End)? taskRange)
    {
        var paragraph = new Paragraph();
        ReadingInlineSegment? segment = inlines.Segments.FirstOrDefault();
        if (segment is null)
        {
            return paragraph;
        }

        if (taskRange is { } range)
        {
            var box = new CheckBox
            {
                IsChecked = segment.TaskCompleted ?? false,
                // The stamped source range is how activation finds the
                // core TaskItem (checkbox byte within the block — the
                // same positional rule mac's taskRow applies by line).
                // The checkbox itself never holds state: the surface
                // reverts WPF's optimistic flip and routes the toggle
                // through the SAME core task command the editor and
                // Tasks panel use; the re-projection renders the
                // outcome. STRING-encoded because WPF text machinery
                // may XamlWriter-serialize document content, and
                // generic payloads (ValueTuple) make it throw — the
                // 2026-07-26 field root cause.
                Tag = ReadingSemantics.EncodeTaskRange(range.Start, range.End),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
            };
            AutomationProperties.SetName(box, ReadingRunText(segment, 0, segment.Content.Length));
            paragraph.Inlines.Add(new InlineUIContainer(box));
        }

        byte[] utf8 = Encoding.UTF8.GetBytes(segment.Content);
        foreach (ReadingInlineRun run in segment.Runs)
        {
            int start = (int)run.Start;
            int length = (int)(run.End - run.Start);
            if (start < 0 || length <= 0 || start + length > utf8.Length)
            {
                continue;
            }
            paragraph.Inlines.Add(
                BuildInline(Encoding.UTF8.GetString(utf8, start, length), run));
        }
        return paragraph;
    }

    private static string ReadingRunText(ReadingInlineSegment segment, int start, int end)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(segment.Content);
        int clampedEnd = Math.Min(end, utf8.Length);
        return start >= clampedEnd
            ? string.Empty
            : Encoding.UTF8.GetString(utf8, start, clampedEnd - start);
    }

    private static Inline BuildInline(string text, ReadingInlineRun run)
    {
        var body = new Run(text);
        ApplyStyles(body, run.Styles);

        Inline inline = run.Kind is ReadingInlineRunKind.Text
            ? body
            : Activatable(body, run.Kind);

        // §10.3: per-run accessible text is core's string, stamped via
        // the host AX mechanism — never composed here.
        if (run.AxText is { Length: > 0 } axText)
        {
            AutomationProperties.SetHelpText(inline, axText);
        }
        return inline;
    }

    private static void ApplyStyles(Run body, ReadingInlineStyle[] styles)
    {
        foreach (ReadingInlineStyle style in styles)
        {
            switch (style)
            {
                case ReadingInlineStyle.Emphasis:
                    body.FontStyle = FontStyles.Italic;
                    break;
                case ReadingInlineStyle.Strong:
                    body.FontWeight = FontWeights.Bold;
                    break;
                case ReadingInlineStyle.Strikethrough:
                    body.TextDecorations = TextDecorations.Strikethrough;
                    break;
                case ReadingInlineStyle.InlineCode:
                    body.FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace");
                    body.SetResourceReference(TextElement.ForegroundProperty, "Slate.EditorCodeBrush");
                    break;
            }
        }
    }

    /// <summary>
    /// Every activatable run: accent (or warning) token PLUS underline —
    /// the affordance is never colour-only (WCAG 1.4.1). Unresolved
    /// wikilinks take <c>Slate.WarningBrush</c> — the #849 distinction,
    /// a state, not an error.
    /// </summary>
    private static Inline Activatable(Run body, ReadingInlineRunKind kind)
    {
        var link = new Hyperlink(body)
        {
            TextDecorations = TextDecorations.Underline,
            Tag = kind,
        };
        link.SetResourceReference(
            TextElement.ForegroundProperty,
            kind is ReadingInlineRunKind.Wikilink { Resolved: false }
                ? "Slate.WarningBrush"
                : "Slate.AccentBrush");

        // A destination is REQUIRED: without NavigateUri NVDA announces
        // "Link has no apparent destination" on every link (measured).
        Uri? destination = ReadingRouting.RoutingUri(kind);
        if (destination is not null)
        {
            link.NavigateUri = destination;
        }
        return link;
    }

    private static AutomationHeadingLevel ToHeadingLevel(byte level) => level switch
    {
        1 => AutomationHeadingLevel.Level1,
        2 => AutomationHeadingLevel.Level2,
        3 => AutomationHeadingLevel.Level3,
        4 => AutomationHeadingLevel.Level4,
        5 => AutomationHeadingLevel.Level5,
        _ => AutomationHeadingLevel.Level6,
    };

    /// <summary>
    /// Landmarks are collected AFTER assembly, by walking the finished
    /// document: a <see cref="TextPointer"/> taken before an element
    /// joins the document belongs to the wrong text container, and a
    /// walk guarantees document order without bookkeeping during build.
    /// </summary>
    /// <summary>
    /// Landmarks store INSERTION positions, matching how RichTextBox
    /// normalizes its caret. Raw ContentStart compares strictly before a
    /// caret sitting on that very line, so backward navigation would
    /// "find" the line the user is already on instead of announcing the
    /// miss — measured as a wrap-like phantom hit in the never-wraps
    /// test.
    /// </summary>
    private static TextPointer Insertion(TextPointer position) =>
        position.GetInsertionPosition(LogicalDirection.Forward) ?? position;

    internal static IReadOnlyList<ReadingLandmark> CollectLandmarks(FlowDocument document)
    {
        var landmarks = new List<ReadingLandmark>();
        foreach (Block block in document.Blocks)
        {
            WalkBlock(block, landmarks);
        }
        return landmarks;
    }

    private static void WalkBlock(Block block, List<ReadingLandmark> landmarks)
    {
        switch (block)
        {
            case Paragraph paragraph:
                if (ReadingSemantics.HeadingLevelOf(paragraph) is byte level and > 0)
                {
                    landmarks.Add(new ReadingLandmark(
                        ReadingLandmarkKind.Heading,
                        Insertion(paragraph.ContentStart),
                        level,
                        ElementText(paragraph)));
                }
                else if (ReadingSemantics.IsCodeBlock(paragraph))
                {
                    landmarks.Add(new ReadingLandmark(
                        ReadingLandmarkKind.CodeBlock,
                        Insertion(paragraph.ContentStart),
                        text: ElementText(paragraph)));
                }
                WalkInlines(paragraph.Inlines, landmarks);
                break;

            case WpfList list:
                landmarks.Add(new ReadingLandmark(
                    ReadingLandmarkKind.List,
                    Insertion(list.ContentStart),
                    text: FirstItemText(list)));
                foreach (ListItem item in list.ListItems)
                {
                    foreach (Block inner in item.Blocks)
                    {
                        WalkBlock(inner, landmarks);
                    }
                }
                break;

            case WpfTable table:
                landmarks.Add(new ReadingLandmark(
                    ReadingLandmarkKind.Table,
                    Insertion(table.ContentStart),
                    text: FirstCellText(table)));
                break;

            case BlockUIContainer container when ReadingSemantics.IsEmbed(container):
                landmarks.Add(new ReadingLandmark(
                    ReadingLandmarkKind.Embed,
                    Insertion(container.ContentStart),
                    text: container.Child is System.Windows.UIElement child
                        ? AutomationProperties.GetName(child) ?? string.Empty
                        : string.Empty));
                break;

            case Section section:
                foreach (Block inner in section.Blocks)
                {
                    WalkBlock(inner, landmarks);
                }
                break;
        }
    }

    private static void WalkInlines(InlineCollection inlines, List<ReadingLandmark> landmarks)
    {
        foreach (Inline inline in inlines)
        {
            switch (inline)
            {
                case Hyperlink link:
                    landmarks.Add(new ReadingLandmark(
                        ReadingLandmarkKind.Link,
                        Insertion(link.ContentStart),
                        text: ElementText(link),
                        element: link));
                    break;
                case Span span:
                    WalkInlines(span.Inlines, landmarks);
                    break;
            }
        }
    }

    /// <summary>Announcement payloads are bounded: a landing must never
    /// read a whole pasted page.</summary>
    private const int LandmarkTextLimit = 120;

    private static string ElementText(TextElement element)
    {
        string text = new TextRange(element.ContentStart, element.ContentEnd).Text;
        int newline = text.IndexOfAny(new[] { '\r', '\n' });
        if (newline >= 0)
        {
            text = text[..newline];
        }
        text = text.Trim();
        return text.Length <= LandmarkTextLimit ? text : text[..LandmarkTextLimit];
    }

    /// <summary>
    /// The first ITEM PARAGRAPH's text, not the item's: a rendered list
    /// item's own range includes the marker glyph, and the landing
    /// announcement said "•	first bullet, list." (caught by the
    /// live-document test).
    /// </summary>
    private static string FirstItemText(WpfList list)
    {
        if (list.ListItems.FirstListItem?.Blocks.OfType<Paragraph>().FirstOrDefault()
            is not { } paragraph)
        {
            return string.Empty;
        }
        string text = ElementText(paragraph);
        // The rendered marker ("•	", "12.	") is materialized INSIDE
        // the paragraph's text range in a hosted document — the spike's
        // "list arrives as document text" finding, now in announcement
        // form: the landing said "•	first bullet, list.". The marker is
        // presentation, not content; a short prefix ending in a tab is
        // exactly it.
        int tab = text.IndexOf('	');
        return tab >= 0 && tab <= 4 ? text[(tab + 1)..].TrimStart() : text;
    }

    private static string FirstCellText(WpfTable table)
    {
        TableRowGroup? group = table.RowGroups.FirstOrDefault();
        TableRow? row = group?.Rows.FirstOrDefault();
        TableCell? cell = row?.Cells.FirstOrDefault();
        return cell is null ? string.Empty : ElementText(cell);
    }
}
