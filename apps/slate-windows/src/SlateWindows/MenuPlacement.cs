// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SlateWindows;

/// <summary>
/// DS-4: where a menu's popup goes. The popup is larger than the surface a reader sees,
/// by the room the shadow takes. WPF's placement modes flip without flipping their
/// offsets, which left a surface some 48 away from its item at a display's edge. WPF's
/// modes are kept, for their flipping, sliding and height limit, and placed against a
/// rectangle instead of the item: the side a popup opens from is the rectangle's, so
/// each side of it is where that side of the surface has to land.
/// </summary>
public static class MenuPlacement
{
    /// <summary>A bar menu's surface inside its popup. A popup below its item starts from
    /// the rectangle's bottom, and one above ends at its top, so a rectangle exists only
    /// if the top and bottom room together fit the item and two gaps. That's why this
    /// has less room for the shadow than a row's menu, which opens to the side.</summary>
    public static readonly Thickness BarSurfaceMargin = new(24, 8, 24, 26);

    /// <summary>A submenu's or a context menu's surface inside its popup: the shadow's
    /// room (<c>Slate.Shadow16</c>: blur 24, depth 8).</summary>
    public static readonly Thickness RowSurfaceMargin = new(24, 16, 24, 32);

    /// <summary>A bar menu's gap from its item.</summary>
    private const double BarGap = 1;

    /// <summary>What lines a submenu's first highlight up with the row that opened it:
    /// the surface's border and padding, and the row's inset.</summary>
    private const double FirstRowInset = 1 + 3 + 3;

    /// <summary>The rectangle a bar item's popup opens from, in the item's coordinates.</summary>
    internal static Rect BarRectangle(Size item)
    {
        Thickness room = BarSurfaceMargin;
        double top = room.Bottom - BarGap;
        double bottom = item.Height + BarGap - room.Top;
        return new Rect(-room.Left, top, item.Width + room.Left + room.Right, Math.Max(0, bottom - top));
    }

    /// <summary>The rectangle a row's popup opens from, in the row's coordinates.</summary>
    internal static Rect RowRectangle(Size row)
    {
        Thickness room = RowSurfaceMargin;
        double left = room.Right;
        double right = row.Width - room.Left;
        double above = room.Top + FirstRowInset;
        double below = room.Bottom + FirstRowInset;
        return new Rect(left, -above, Math.Max(0, right - left), row.Height + above + below);
    }
}

/// <summary>A menu item's width and height, to the rectangle its popup opens from.</summary>
internal sealed class MenuPlacementRectangleConverter : IMultiValueConverter
{
    public bool IsRow { get; set; }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not [double width, double height])
        {
            return Rect.Empty;
        }

        var size = new Size(width, height);
        return IsRow ? MenuPlacement.RowRectangle(size) : MenuPlacement.BarRectangle(size);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
