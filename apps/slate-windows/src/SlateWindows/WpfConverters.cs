// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SlateWindows;

internal sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility visibility && visibility != Visibility.Visible;
}

internal sealed class AxisToOrientationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value as string, "vertical", StringComparison.Ordinal)
            ? Orientation.Vertical
            : Orientation.Horizontal;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Orientation.Vertical ? "vertical" : "horizontal";
}

internal sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>
/// Projects "this sheet is open" (a non-null view model) to a bool a
/// <c>DataTrigger</c> can compare. Exists for the Menu's disable
/// triggers (W5-2 red-team round 1): the two overlays expose
/// <c>IsOpen</c> flags, but the seven sheets are object properties, and
/// a trigger can only equality-match a value.
/// </summary>
internal sealed class IsNotNullConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

internal sealed class SidebarSortModeLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            SidebarSortMode.NameAscending => "Name (A to Z)",
            SidebarSortMode.NameDescending => "Name (Z to A)",
            SidebarSortMode.ModifiedNewest => "Modified (newest)",
            SidebarSortMode.ModifiedOldest => "Modified (oldest)",
            SidebarSortMode.CreatedNewest => "Created (newest)",
            SidebarSortMode.CreatedOldest => "Created (oldest)",
            _ => value?.ToString() ?? string.Empty,
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
