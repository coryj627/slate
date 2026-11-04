// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace SlateWindows;

/// <summary>Which way a menu's popup opens from the item it belongs to.</summary>
public enum MenuOpening
{
    None,

    /// <summary>A bar item's menu: below it, or above when the display runs out.</summary>
    Vertical,

    /// <summary>A row's submenu: beside it, on the right or on the left.</summary>
    Horizontal,
}

/// <summary>
/// DS-4: where a menu's popup goes. The popup is larger than the surface a reader sees,
/// by the room the shadow takes, and WPF's own placement modes flip without flipping
/// their offsets, so a surface at a display's edge landed some 48 away from its item,
/// a gutter for the pointer to cross. Each candidate here puts the surface itself
/// against the item, on the side it opens to. WPF chooses between them and slides the
/// popup to fit, as it does for its own modes.
/// </summary>
public static class MenuPlacement
{
    /// <summary>The surface's place inside its popup: the shadow's room
    /// (<c>Slate.Shadow16</c>: blur 24, depth 8). The template reads this.</summary>
    public static readonly Thickness SurfaceMargin = new(24, 16, 24, 32);

    /// <summary>A bar menu's gap below its item.</summary>
    private const double BarGap = 1;

    /// <summary>What lines a submenu's first highlight up with the row that opened it:
    /// the surface's border and padding, and the row's inset.</summary>
    private const double FirstRowInset = 1 + 3 + 3;

    public static readonly DependencyProperty OpensProperty = DependencyProperty.RegisterAttached(
        "Opens",
        typeof(MenuOpening),
        typeof(MenuPlacement),
        new PropertyMetadata(MenuOpening.None, OnOpensChanged));

    public static MenuOpening GetOpens(DependencyObject element) => (MenuOpening)element.GetValue(OpensProperty);

    public static void SetOpens(DependencyObject element, MenuOpening value) => element.SetValue(OpensProperty, value);

    /// <summary>The candidates, in device pixels as WPF's callback deals in them, and
    /// relative to the target's top left. <paramref name="scale"/> is the display's.</summary>
    internal static CustomPopupPlacement[] Candidates(MenuOpening opening, Size popup, Size target, double scale)
    {
        double left = SurfaceMargin.Left * scale;
        double top = SurfaceMargin.Top * scale;
        double right = SurfaceMargin.Right * scale;
        double bottom = SurfaceMargin.Bottom * scale;
        if (opening == MenuOpening.Vertical)
        {
            double gap = BarGap * scale;
            return
            [
                new CustomPopupPlacement(new Point(-left, target.Height + gap - top), PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(new Point(-left, bottom - gap - popup.Height), PopupPrimaryAxis.Horizontal),
            ];
        }

        double y = -top - (FirstRowInset * scale);
        return
        [
            new CustomPopupPlacement(new Point(target.Width - left, y), PopupPrimaryAxis.Vertical),
            new CustomPopupPlacement(new Point(right - popup.Width, y), PopupPrimaryAxis.Vertical),
        ];
    }

    private static void OnOpensChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not Popup popup)
        {
            return;
        }

        var opening = (MenuOpening)e.NewValue;
        popup.Placement = PlacementMode.Custom;
        popup.CustomPopupPlacementCallback = (popupSize, targetSize, _) =>
            Candidates(opening, popupSize, targetSize, VisualTreeHelper.GetDpi(popup.PlacementTarget ?? popup).DpiScaleX);
    }
}
