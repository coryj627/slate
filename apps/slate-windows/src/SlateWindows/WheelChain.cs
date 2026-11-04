// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace SlateWindows;

/// <summary>DS-5: a list inside a scroller takes the wheel even with nowhere to go, so a
/// short window's sheet stopped scrolling under the pointer. On the outer scroller, this
/// leaves the wheel to an inner one that can move that way, and otherwise takes it. What's
/// in a popup, such as an open combo box, keeps its own wheel.</summary>
internal static class WheelChain
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(WheelChain),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is ScrollViewer scroller && (bool)e.NewValue)
        {
            scroller.PreviewMouseWheel += OnPreviewMouseWheel;
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var outer = (ScrollViewer)sender;
        for (DependencyObject? step = e.OriginalSource as DependencyObject; !ReferenceEquals(step, outer); step = ParentOf(step))
        {
            if (step is null || (step is ScrollViewer inner && CanScroll(inner, e.Delta)))
            {
                return;
            }
        }

        e.Handled = true;
        outer.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent });
    }

    private static bool CanScroll(ScrollViewer scroller, int delta) =>
        delta < 0 ? scroller.VerticalOffset < scroller.ScrollableHeight : scroller.VerticalOffset > 0;

    private static DependencyObject? ParentOf(DependencyObject? element) =>
        element is Visual or Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
}
