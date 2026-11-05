// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SlateWindows;

/// <summary>
/// DS-8 (#1809): the tab strip's rows. WPF's <see cref="TabPanel"/> stretches a wrapped row's
/// cells across it, so a tab's width limit left gaps, and the open tab's flares and its
/// neighbours' dividers stopped meeting. This packs each row from the left and keeps the rest
/// of TabPanel's shape: it's still one, which the F6 ring reads as the tab bar, and the row
/// holding the open tab is the last, next to the note.
/// </summary>
internal sealed class TabStripPanel : TabPanel
{
    /// <summary>The narrowest a tab goes, unless the strip is narrower: a split can be, and
    /// a tab past the strip's edge would take its close button out of reach.</summary>
    private const double TabFloor = 100;

    /// <summary>Whether a tab starts its row, so has nothing to divide itself from.</summary>
    public static readonly DependencyProperty IsRowStartProperty = DependencyProperty.RegisterAttached(
        "IsRowStart", typeof(bool), typeof(TabStripPanel), new PropertyMetadata(false));

    public TabStripPanel()
    {
        // Choosing a tab can move its row. TabPanel doesn't ask for that itself.
        AddHandler(Selector.SelectedEvent, new RoutedEventHandler((_, _) => InvalidateArrange()));
    }

    public static bool GetIsRowStart(DependencyObject element) => (bool)element.GetValue(IsRowStartProperty);

    public static void SetIsRowStart(DependencyObject element, bool value) => element.SetValue(IsRowStartProperty, value);

    protected override Size MeasureOverride(Size constraint)
    {
        double floor = Math.Min(TabFloor, constraint.Width);
        double height = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.SetCurrentValue(MinWidthProperty, floor);
            child.Measure(new Size(constraint.Width, double.PositiveInfinity));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        List<List<UIElement>> rows = Rows(constraint.Width);
        double widest = 0;
        foreach (List<UIElement> row in rows)
        {
            widest = Math.Max(widest, row.Sum(child => child.DesiredSize.Width));
        }

        return new Size(widest, rows.Count * height);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        double height = 0;
        foreach (UIElement child in InternalChildren)
        {
            height = Math.Max(height, child.DesiredSize.Height);
        }

        List<List<UIElement>> rows = Rows(arrangeSize.Width);
        int open = rows.FindIndex(row => row.Any(child => child is TabItem { IsSelected: true }));
        if (open >= 0)
        {
            List<UIElement> openRow = rows[open];
            rows.RemoveAt(open);
            rows.Add(openRow);
        }

        double top = 0;
        foreach (List<UIElement> row in rows)
        {
            double left = 0;
            foreach (UIElement child in row)
            {
                SetIsRowStart(child, left == 0);
                child.Arrange(new Rect(left, top, child.DesiredSize.Width, height));
                left += child.DesiredSize.Width;
            }

            top += height;
        }

        return arrangeSize;
    }

    private List<List<UIElement>> Rows(double width)
    {
        var rows = new List<List<UIElement>>();
        var row = new List<UIElement>();
        double used = 0;
        foreach (UIElement child in InternalChildren)
        {
            double wanted = child.DesiredSize.Width;
            if (row.Count > 0 && used + wanted > width)
            {
                rows.Add(row);
                row = new List<UIElement>();
                used = 0;
            }

            row.Add(child);
            used += wanted;
        }

        if (row.Count > 0)
        {
            rows.Add(row);
        }

        return rows;
    }
}
