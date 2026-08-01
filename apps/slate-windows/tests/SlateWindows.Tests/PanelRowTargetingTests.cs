// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SlateWindows.Panels;

namespace SlateWindows.Tests;

/// <summary>
/// W4-2 adversarial round 2: context menus on the panel lists act on
/// the CLICKED row — WPF right-clicks never move selection, so the
/// menu previously executed against whatever happened to be selected
/// (or silently did nothing with no selection).
/// </summary>
public sealed class PanelRowTargetingTests
{
    [Fact]
    public void PointerRequestTargetsTheClickedRow()
    {
        RunSta(() =>
        {
            (ListBox list, ListBoxItem itemA, ListBoxItem itemB) =
                MakeRealizedList();
            itemA.IsSelected = true;
            DependencyObject origin = DeepestVisualOf(itemB);

            // Row A selected, row B clicked: the menu must act on B.
            Assert.True(
                PanelRowTargeting.TargetRowAt(
                    list, origin, pointerRequest: true));
            Assert.Same(itemB, list.SelectedItem);
        });
    }

    [Fact]
    public void PointerRequestOverChromeRefusesTheMenu()
    {
        RunSta(() =>
        {
            (ListBox list, ListBoxItem itemA, _) = MakeRealizedList();
            itemA.IsSelected = true;

            // Empty chrome resolves to no row: no menu, selection kept.
            Assert.False(
                PanelRowTargeting.TargetRowAt(
                    list, list, pointerRequest: true));
            Assert.Same(itemA, list.SelectedItem);
        });
    }

    [Fact]
    public void KeyboardRequestKeepsSelectionAndRefusesOverNothing()
    {
        RunSta(() =>
        {
            (ListBox list, _, ListBoxItem itemB) = MakeRealizedList();

            // Menu key with nothing selected: refuse rather than open
            // a menu that silently does nothing.
            Assert.False(
                PanelRowTargeting.TargetRowAt(
                    list, list, pointerRequest: false));

            itemB.IsSelected = true;
            Assert.True(
                PanelRowTargeting.TargetRowAt(
                    list, list, pointerRequest: false));
            Assert.Same(itemB, list.SelectedItem);
        });
    }

    private static (ListBox List, ListBoxItem A, ListBoxItem B)
        MakeRealizedList()
    {
        var itemA = new ListBoxItem { Content = new TextBlock { Text = "alpha" } };
        var itemB = new ListBoxItem { Content = new TextBlock { Text = "beta" } };
        var list = new ListBox();
        _ = list.Items.Add(itemA);
        _ = list.Items.Add(itemB);
        list.Measure(new Size(400, 400));
        list.Arrange(new Rect(0, 0, 400, 400));
        list.UpdateLayout();
        return (list, itemA, itemB);
    }

    /// <summary>A click's OriginalSource is the innermost visual —
    /// walk down to it so the test exercises the full ancestor walk.</summary>
    private static DependencyObject DeepestVisualOf(DependencyObject root)
    {
        DependencyObject current = root;
        while (VisualTreeHelper.GetChildrenCount(current) > 0)
        {
            current = VisualTreeHelper.GetChild(current, 0);
        }
        return current;
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
