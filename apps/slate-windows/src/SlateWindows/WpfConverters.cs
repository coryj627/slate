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

/// <summary>W7-7 PR 4b (#1247): a sidebar column's width, in
/// device-independent pixels, both ways — the resize commands step the
/// number and a splitter drag writes it back.</summary>
internal sealed class PixelGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double pixels ? new GridLength(pixels) : Binding.DoNothing;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is GridLength { IsAbsolute: true } length ? length.Value : Binding.DoNothing;
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
internal sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not true;
}

internal sealed class IsNotNullConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>A recent vault's button name, <see cref="RecentVault.SpokenName"/>,
/// bound as (entry, the whole list, its count) so each add while the
/// list is rebuilt re-evaluates every button.</summary>
internal sealed class RecentVaultNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values switch
        {
            [RecentVault vault, IEnumerable<RecentVault> all, ..] => RecentVault.SpokenName(vault, all),
            [RecentVault vault, ..] => vault.DisplayName,
            _ => string.Empty,
        };

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>#1795: the most of a height a part may take: a share of it, less what the
/// parts beside it have to keep, between a floor and a cap, so a short window can't give
/// the part all of the room it shares.</summary>
internal sealed class CappedShareConverter : IValueConverter, IMultiValueConverter
{
    /// <summary>DS-5: a height and the text size beside it. What the reserve holds room
    /// for is text, so it grows with the size, and a text size change reaches it.</summary>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values is [double height, double fontSize]
            ? Limit(height, Reserve * (fontSize / TypeRamp.Body.Size))
            : Cap;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    public double Share { get; set; } = 1;

    public double Cap { get; set; } = double.PositiveInfinity;

    public double Reserve { get; set; }

    public double Floor { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double height ? Limit(height, Reserve) : Cap;

    private double Limit(double height, double reserve) => Math.Max(Floor, Math.Min(Cap, (height * Share) - reserve));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

internal sealed class SidebarSortModeLabelConverter : IValueConverter
{
    /// <summary>DS-17: the labels are core's, the ones the Sort and group button's name
    /// is built from.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is SidebarSortMode mode
            ? uniffi.slate_uniffi.SlateUniffiMethods.SidebarSortOrderLabel(FilesSidebarViewModel.ToCore(mode))
            : value?.ToString() ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
