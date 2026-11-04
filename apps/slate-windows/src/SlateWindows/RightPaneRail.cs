// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace SlateWindows;

/// <summary>DS-5: a rail that fits its labels took two thirds of a 320 pane at 225%, and
/// the leaf beside it wrapped a letter at a time. So it goes under the leaf when that
/// leaves the leaf too little room, and scrolls in a share of the pane's height. The tab
/// order doesn't change: the leaf comes first either way.</summary>
internal static class RightPaneRail
{
    /// <summary>About sixteen characters of the leaf's text.</summary>
    private const double LeafRoomInEms = 8;

    private const double TallestShare = 0.4;

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(RightPaneRail),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is FrameworkElement rail && (bool)e.NewValue)
        {
            // A size change reaches this through either the pane or the rail's text.
            rail.LayoutUpdated += (_, _) => Place(rail);
        }
    }

    /// <summary>What the rail asks for is what its labels need, in either place, until
    /// the pane is narrower than they are, so the choice can't flip back and forth.</summary>
    private static void Place(FrameworkElement rail)
    {
        if (rail.Parent is not Grid { ActualWidth: > 0 } host)
        {
            return;
        }

        double leafRoom = host.ActualWidth - rail.DesiredSize.Width;
        bool under = leafRoom < LeafRoomInEms * TextElement.GetFontSize(rail);
        if (Grid.GetRow(rail) != (under ? 1 : 0))
        {
            Grid.SetRow(rail, under ? 1 : 0);
            Grid.SetColumn(rail, under ? 0 : 1);
        }

        double tallest = under ? host.ActualHeight * TallestShare : double.PositiveInfinity;
        if (rail.MaxHeight != tallest)
        {
            rail.MaxHeight = tallest;
        }
    }
}
