// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using uniffi.slate_uniffi;

namespace ContainerSpike;

/// Option B: an `ItemsControl` of per-block elements.
///
/// This is how a WPF list-of-blocks is normally built, and it is the
/// option §10.6 suspects does NOT give a genuine Text pattern over the
/// note. The spike builds it faithfully rather than as a straw man: same
/// core block model, same inline vocabulary, real controls for the
/// interactive rows — so any difference the probe reports is attributable
/// to the container and nothing else.
internal static class ItemsControlBuilder
{
    public static FrameworkElement Build(
        IReadOnlyList<(ReadingBlock Block, ReadingBlockInlines Inlines)> model)
    {
        var panel = new StackPanel { Margin = new Thickness(24) };

        foreach ((ReadingBlock block, ReadingBlockInlines inlines) in model)
        {
            if (inlines.BlockEmbedKey is { Length: > 0 } embedKey)
            {
                panel.Children.Add(EmbedCard(embedKey));
                continue;
            }
            panel.Children.Add(BlockElement(block, inlines));
        }

        var items = new ItemsControl
        {
            ItemsSource = new[] { panel },
            Background = Palette.Surface,
            Foreground = Palette.Text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 15,
        };

        var scroller = new ScrollViewer
        {
            Content = items,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        AutomationProperties.SetAutomationId(scroller, "ReadingSurface");
        AutomationProperties.SetName(scroller, "Reading view");
        return scroller;
    }

    private static UIElement BlockElement(ReadingBlock block, ReadingBlockInlines inlines)
    {
        switch (block.Kind)
        {
            case ReadingBlockKind.Heading heading:
            {
                TextBlock text = TextBlockFor(inlines);
                text.FontSize = heading.Level switch
                {
                    1 => 28,
                    2 => 22,
                    3 => 18,
                    _ => 16,
                };
                text.FontWeight = FontWeights.SemiBold;
                text.Margin = new Thickness(0, 12, 0, 6);
                AutomationProperties.SetHeadingLevel(text, HeadingLevel(heading.Level));
                return text;
            }

            case ReadingBlockKind.ListItem listKind:
                return ListRow(listKind, inlines);

            case ReadingBlockKind.BlockQuote:
            {
                var border = new Border
                {
                    BorderBrush = Palette.Accent,
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Padding = new Thickness(12, 4, 0, 4),
                    Margin = new Thickness(0, 6, 0, 6),
                    Child = TextBlockFor(inlines),
                };
                return border;
            }

            case ReadingBlockKind.CodeFence fence:
                return CodeCard(fence);

            case ReadingBlockKind.Table:
                return TableGrid(block);

            case ReadingBlockKind.ThematicBreak:
                return new Separator { Margin = new Thickness(0, 10, 0, 10) };

            default:
            {
                TextBlock text = TextBlockFor(inlines);
                text.Margin = new Thickness(0, 4, 0, 4);
                return text;
            }
        }
    }

    /// The authored marker verbatim (§10.5) — never renumbered. A task row
    /// gets a real `CheckBox`, which is the interleaved focusable child
    /// the container comparison turns on.
    private static UIElement ListRow(
        ReadingBlockKind.ListItem kind, ReadingBlockInlines inlines)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(kind.Depth * 20, 2, 0, 2),
        };

        ReadingInlineSegment? segment = inlines.Segments.FirstOrDefault();

        if (kind.Task is not null)
        {
            var box = new CheckBox
            {
                IsChecked = segment?.TaskCompleted ?? false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
            };
            // Named from the item's own text: two siblings both called
            // "Task" is an axe SiblingUniqueAndFocusable violation the
            // FIXTURE would be causing, which would then show up
            // identically in both variants and discriminate nothing.
            AutomationProperties.SetName(
                box, $"Task: {segment?.Content ?? string.Empty}".Trim());
            row.Children.Add(box);
        }
        else
        {
            row.Children.Add(new TextBlock
            {
                Text = kind.Ordered ? (inlines.ListMarker ?? "1.") : "•",
                Margin = new Thickness(0, 0, 8, 0),
            });
        }

        row.Children.Add(TextBlockFor(inlines));

        // Mac conveys depth through an AX value string; the owner call is
        // to expose NATIVE list semantics instead. Nothing in a plain
        // StackPanel does that — the probe records exactly this gap.
        AutomationProperties.SetItemStatus(row, $"list item, level {kind.Depth + 1}");
        return row;
    }

    private static TextBlock TextBlockFor(ReadingBlockInlines inlines)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        ReadingInlineSegment? segment = inlines.Segments.FirstOrDefault();
        if (segment is null)
        {
            return text;
        }
        foreach (Inline inline in InlineBuilder.Build(segment))
        {
            text.Inlines.Add(inline);
        }
        return text;
    }

    private static UIElement TableGrid(ReadingBlock block)
    {
        ReadingTableCells? cells = SlateUniffiMethods.ReadingTableCells(block.Source);
        var grid = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        if (cells is null || (cells.Header.Length == 0 && cells.Rows.Length == 0))
        {
            return new TextBlock { Text = block.Source };
        }

        // Header first, then body rows — one flat grid, which is exactly
        // the "plain accessible table" W3-1 ships before W4-1's substrate
        // replaces it (§10.7).
        var allRows = new List<string[]>();
        if (cells.Header.Length > 0)
        {
            allRows.Add(cells.Header);
        }
        allRows.AddRange(cells.Rows);

        int columns = allRows.Max(r => r.Length);
        for (int c = 0; c < columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
        }
        for (int r = 0; r < allRows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition());
            for (int c = 0; c < allRows[r].Length; c++)
            {
                var cell = new TextBlock
                {
                    Text = allRows[r][c],
                    Padding = new Thickness(6, 3, 6, 3),
                    FontWeight = r == 0 && cells.Header.Length > 0
                        ? FontWeights.SemiBold : FontWeights.Normal,
                };
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }
        return grid;
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
            FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
            Padding = new Thickness(8),
            Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2)),
        });
        return panel;
    }

    private static UIElement EmbedCard(string key)
    {
        var button = new Button
        {
            Content = $"Embedded note: {key}",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 6, 0, 6),
        };
        AutomationProperties.SetName(button, $"Embedded note {key}");
        AutomationProperties.SetAutomationId(button, "BlockEmbedCard");
        return button;
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
