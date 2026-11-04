// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;

namespace SlateWindows;

/// <summary>#1795: a property row: its key, value, buttons and validation message, in
/// that order. In a narrow editor they stack, because side by side the key and the
/// buttons leave the value no width at all. Tab order stays the same either way.</summary>
internal sealed class PropertyRowGrid : Grid
{
    /// <summary>Under this, side by side leaves the value too narrow to read.</summary>
    internal const double StackBelow = 340;

    private bool _stacked;

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        bool stacked = sizeInfo.NewSize.Width < StackBelow;
        if (stacked == _stacked || Children.Count != 4)
        {
            return;
        }

        _stacked = stacked;
        Place(Children[0], 0, 0, stacked ? 3 : 1, default);
        Place(Children[1], stacked ? 1 : 0, stacked ? 0 : 1, stacked ? 3 : 1, stacked ? new Thickness(0, 4, 0, 0) : new Thickness(8, 0, 0, 0));
        Place(Children[2], stacked ? 2 : 0, stacked ? 0 : 2, stacked ? 3 : 1, stacked ? new Thickness(0, 4, 0, 0) : new Thickness(8, 0, 0, 0));
        Place(Children[3], stacked ? 3 : 1, stacked ? 0 : 1, stacked ? 3 : 2, stacked ? new Thickness(0, 2, 0, 0) : new Thickness(8, 2, 0, 0));
    }

    private static void Place(UIElement child, int row, int column, int span, Thickness margin)
    {
        SetRow(child, row);
        SetColumn(child, column);
        SetColumnSpan(child, span);
        ((FrameworkElement)child).Margin = margin;
    }
}
