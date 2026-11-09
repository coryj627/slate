// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;

namespace SlateWindows;

/// <summary>DS-14 (#1815): the Settings sheet's body, its category list and the open page.
/// In a narrow sheet the list goes compact, as WinUI's navigation does
/// (<c>design/windows/winui-review.md</c>): its glyphs alone, each still named and with its
/// name as a tooltip, so the page keeps room for its rows. <see cref="IsCompactProperty"/>
/// inherits, so the list's items read it.</summary>
internal sealed class SettingsLayoutGrid : Grid
{
    internal const double CompactBelow = 600;
    internal const double ListWidth = 240;
    internal const double CompactListWidth = 56;

    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.RegisterAttached(
        "IsCompact",
        typeof(bool),
        typeof(SettingsLayoutGrid),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetIsCompact(DependencyObject element) => (bool)element.GetValue(IsCompactProperty);

    public static void SetIsCompact(DependencyObject element, bool value) => element.SetValue(IsCompactProperty, value);

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        bool compact = sizeInfo.NewSize.Width < CompactBelow;
        if (compact != GetIsCompact(this) && ColumnDefinitions.Count > 0)
        {
            SetIsCompact(this, compact);
            ColumnDefinitions[0].Width = new GridLength(compact ? CompactListWidth : ListWidth);
        }
    }
}
