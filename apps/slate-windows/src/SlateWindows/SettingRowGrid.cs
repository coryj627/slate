// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace SlateWindows;

/// <summary>DS-14 (#1815): a setting's label and description beside its control, or above
/// it once the row is too narrow for both at the text's size, as <see cref="PropertyRowGrid"/>
/// stacks a property row. Tab order stays the same either way.</summary>
internal sealed class SettingRowGrid : Grid
{
    /// <summary>Under this at the ramp's body size, the control leaves the label too
    /// narrow to read. It grows with the text (DS-5).</summary>
    internal const double StackBelow = 160;

    private bool _stacked;

    internal bool IsStacked => _stacked;

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Place(sizeInfo.NewSize.Width);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == TextElement.FontSizeProperty && IsLoaded)
        {
            Place(ActualWidth);
        }
    }

    private void Place(double width)
    {
        bool stacked = width < StackBelow * (TextElement.GetFontSize(this) / TypeRamp.Body.Size);
        if (stacked == _stacked || Children.Count != 2)
        {
            return;
        }

        _stacked = stacked;
        var label = (FrameworkElement)Children[0];
        var control = (FrameworkElement)Children[1];
        SetColumnSpan(label, stacked ? 2 : 1);
        label.Margin = stacked ? default : new Thickness(0, 0, 16, 0);
        SetRow(control, stacked ? 1 : 0);
        SetColumn(control, stacked ? 0 : 1);
        SetColumnSpan(control, stacked ? 2 : 1);
        control.HorizontalAlignment = stacked ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        control.Margin = stacked ? new Thickness(0, 8, 0, 0) : default;
    }
}
