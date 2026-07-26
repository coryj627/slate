// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using uniffi.slate_uniffi;
using WpfList = System.Windows.Documents.List;
using WpfTable = System.Windows.Documents.Table;

namespace ContainerSpike;

/// Option A: one `FlowDocument` in a `FlowDocumentScrollViewer`.
///
/// The hypothesis §10.6 records is that this yields a genuine Text pattern
/// over the whole note, and therefore real reading order across non-text
/// children. The two things the spike must find out are whether
/// `AutomationProperties.HeadingLevel` survives on a `Paragraph`, and
/// whether interactive children hosted in `BlockUIContainer` /
/// `InlineUIContainer` stay keyboard-reachable and Invoke-able while
/// remaining inside the document's text range.
internal static class FlowDocumentBuilder
{
    public static FrameworkElement Build(
        IReadOnlyList<(ReadingBlock Block, ReadingBlockInlines Inlines)> model,
        bool withSemanticPeers = false,
        bool richTextHost = false)
    {
        var document = new FlowDocument
        {
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 15,
            PagePadding = new Thickness(24),
            Background = Palette.Surface,
            Foreground = Palette.Text,
            ColumnWidth = double.PositiveInfinity,
        };

        WpfList? openList = null;
        ListItem? lastItem = null;

        foreach ((ReadingBlock block, ReadingBlockInlines inlines) in model)
        {
            // A block that IS one embed expands as a card, never as text
            // (§10.5) — the same rule the mac reading view applies.
            if (inlines.BlockEmbedKey is { Length: > 0 } embedKey)
            {
                openList = null;
                lastItem = null;
                document.Blocks.Add(EmbedCard(embedKey));
                continue;
            }

            if (block.Kind is ReadingBlockKind.ListItem listKind)
            {
                AppendListItem(document, ref openList, ref lastItem, listKind, inlines);
                continue;
            }

            openList = null;
            lastItem = null;
            document.Blocks.Add(NonListBlock(block, inlines));
        }

        // A read-only RichTextBox hosts the IDENTICAL document; only the
        // host differs, so a difference the probe reports is the host's.
        if (richTextHost)
        {
            var box = new PeeredRichTextBox { Document = document };
            AutomationProperties.SetAutomationId(box, "ReadingSurface");
            AutomationProperties.SetName(box, "Reading view");
            return box;
        }

        FlowDocumentScrollViewer viewer = withSemanticPeers
            ? new PeeredFlowDocumentViewer()
            : new FlowDocumentScrollViewer();
        viewer.Document = document;
        viewer.IsSelectionEnabled = true;
        viewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        AutomationProperties.SetAutomationId(viewer, "ReadingSurface");
        AutomationProperties.SetName(viewer, "Reading view");
        return viewer;
    }

    private static void AppendListItem(
        FlowDocument document,
        ref WpfList? openList,
        ref ListItem? lastItem,
        ReadingBlockKind.ListItem kind,
        ReadingBlockInlines inlines)
    {
        if (openList is null)
        {
            openList = new WpfList
            {
                MarkerStyle = kind.Ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            };
            document.Blocks.Add(openList);
        }

        Paragraph content = Paragraph(inlines, kind.Task is not null);

        // Depth > 0 nests a child List inside the previous item, which is
        // what produces a real nested-list structure rather than an
        // indent that only looks like one.
        if (kind.Depth > 0 && lastItem is not null)
        {
            WpfList nested = lastItem.Blocks.OfType<WpfList>().LastOrDefault()
                ?? AddNested(lastItem, kind.Ordered);
            nested.ListItems.Add(new ListItem(content));
            return;
        }

        var item = new ListItem(content);
        openList.ListItems.Add(item);
        lastItem = item;
    }

    private static WpfList AddNested(ListItem parent, bool ordered)
    {
        var nested = new WpfList
        {
            MarkerStyle = ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
        };
        parent.Blocks.Add(nested);
        return nested;
    }

    private static Block NonListBlock(ReadingBlock block, ReadingBlockInlines inlines)
    {
        switch (block.Kind)
        {
            case ReadingBlockKind.Heading heading:
            {
                Paragraph paragraph = Paragraph(inlines, task: false);
                paragraph.FontSize = heading.Level switch
                {
                    1 => 28,
                    2 => 22,
                    3 => 18,
                    _ => 16,
                };
                paragraph.FontWeight = FontWeights.SemiBold;
                // THE measurement: does this reach UIA from a Paragraph?
                AutomationProperties.SetHeadingLevel(paragraph, HeadingLevel(heading.Level));
                return paragraph;
            }

            case ReadingBlockKind.BlockQuote:
            {
                Paragraph quote = Paragraph(inlines, task: false);
                quote.Padding = new Thickness(12, 0, 0, 0);
                quote.BorderThickness = new Thickness(3, 0, 0, 0);
                quote.BorderBrush = Palette.Accent;
                return quote;
            }

            case ReadingBlockKind.CodeFence fence:
                return new BlockUIContainer(CodeCard(fence));

            case ReadingBlockKind.Table:
                return TableBlock(block);

            case ReadingBlockKind.ThematicBreak:
                return new BlockUIContainer(new Separator());

            default:
                return Paragraph(inlines, task: false);
        }
    }

    /// A task row puts a real `CheckBox` inline, next to the item's text —
    /// the interleaving that decides whether a focusable non-text child
    /// can live inside the document without leaving the text range.
    private static Paragraph Paragraph(ReadingBlockInlines inlines, bool task)
    {
        var paragraph = new Paragraph();
        ReadingInlineSegment? segment = inlines.Segments.FirstOrDefault();

        if (task && segment is not null)
        {
            var box = new CheckBox
            {
                IsChecked = segment.TaskCompleted ?? false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
            };
            // Named from the item's own text: two siblings both called
            // "Task" is an axe SiblingUniqueAndFocusable violation the
            // FIXTURE would be causing, which would then show up
            // identically in both variants and discriminate nothing.
            AutomationProperties.SetName(
                box, $"Task: {segment?.Content ?? string.Empty}".Trim());
            paragraph.Inlines.Add(new InlineUIContainer(box));
        }

        if (segment is not null)
        {
            foreach (Inline inline in InlineBuilder.Build(segment))
            {
                paragraph.Inlines.Add(inline);
            }
        }

        return paragraph;
    }

    private static Block TableBlock(ReadingBlock block)
    {
        ReadingTableCells? cells = SlateUniffiMethods.ReadingTableCells(block.Source);
        var table = new WpfTable();
        var group = new TableRowGroup();
        table.RowGroups.Add(group);

        if (cells is null)
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
            row.Cells.Add(new TableCell(paragraph));
        }
        return row;
    }

    private static UIElement CodeCard(ReadingBlockKind.CodeFence fence)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
        var copy = new Button
        {
            Content = "Copy",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8, 2, 8, 2),
        };
        AutomationProperties.SetName(
            copy, $"Copy {(fence.Language.Length == 0 ? "code" : fence.Language)} block");
        panel.Children.Add(copy);
        panel.Children.Add(new TextBlock
        {
            Text = fence.Interior,
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, monospace"),
            Padding = new Thickness(8),
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xF2, 0xF2, 0xF2)),
        });
        return panel;
    }

    private static Block EmbedCard(string key)
    {
        var button = new Button
        {
            Content = $"Embedded note: {key}",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 6, 10, 6),
        };
        AutomationProperties.SetName(button, $"Embedded note {key}");
        AutomationProperties.SetAutomationId(button, "BlockEmbedCard");
        return new BlockUIContainer(button);
    }

    private static AutomationHeadingLevel HeadingLevel(byte level) => level switch
    {
        1 => AutomationHeadingLevel.Level1,
        2 => AutomationHeadingLevel.Level2,
        3 => AutomationHeadingLevel.Level3,
        4 => AutomationHeadingLevel.Level4,
        5 => AutomationHeadingLevel.Level5,
        _ => AutomationHeadingLevel.Level6,
    };
}
