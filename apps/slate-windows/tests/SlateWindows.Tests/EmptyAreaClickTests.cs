// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5 as the owner amended it): a click on the
/// EMPTY area of a populated list or tree puts the keys on a row — its
/// selected row, else its first, selecting nothing — as a click inside a
/// Win32 list does. WPF gives them to nothing there (measured on a ListBox,
/// a DataGrid and a TreeView: the keys stayed where they were), so a pointer
/// user who clicked into a list was not in it. A click on a row stays the
/// row's. The press is raised on the element the empty area hit-tests to,
/// and travels the routed MouseDown the input manager sends.
/// </summary>
public sealed class EmptyAreaClickTests
{
    [Fact]
    public void AClickOnAPopulatedListsEmptyAreaLandsOnItsFirstRow() => RunSta(() =>
    {
        var list = new ListBox { Height = 200, Background = Brushes.Transparent };
        list.Items.Add("one");
        list.Items.Add("two");
        using Hosted host = Host(list);

        host.ClickEmptyArea(list);

        Assert.Same(list.ItemContainerGenerator.ContainerFromIndex(0), Keyboard.FocusedElement);
        Assert.Equal(-1, list.SelectedIndex);
    });

    [Fact]
    public void AClickOnAPopulatedListsEmptyAreaLandsOnItsSelectedRow() => RunSta(() =>
    {
        var list = new ListBox { Height = 200, Background = Brushes.Transparent };
        list.Items.Add("one");
        list.Items.Add("two");
        list.SelectedIndex = 1;
        using Hosted host = Host(list);

        host.ClickEmptyArea(list);

        Assert.Same(list.ItemContainerGenerator.ContainerFromIndex(1), Keyboard.FocusedElement);
        Assert.Equal(1, list.SelectedIndex);
    });

    [Fact]
    public void AClickOnARowStaysTheRows() => RunSta(() =>
    {
        var list = new ListBox { Height = 200, Background = Brushes.Transparent };
        list.Items.Add("one");
        list.Items.Add("two");
        using Hosted host = Host(list);
        var second = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(1);

        host.Click(second);

        Assert.Same(second, Keyboard.FocusedElement);
    });

    [Fact]
    public void AClickOnAPopulatedTreesEmptyAreaLandsOnItsFirstRowUnselected() => RunSta(() =>
    {
        var tree = new LandingTreeView { Height = 200, Background = Brushes.Transparent };
        var first = new LandingTreeViewItem { Header = "one" };
        tree.Items.Add(first);
        tree.Items.Add(new LandingTreeViewItem { Header = "two" });
        using Hosted host = Host(tree);

        host.ClickEmptyArea(tree);

        Assert.Same(first, Keyboard.FocusedElement);
        Assert.False(first.IsSelected, "the click's landing selected the first row.");
    });

    [Fact]
    public void AClickOnAnEmptyListLeavesTheKeysAlone() => RunSta(() =>
    {
        var list = new ListBox { Height = 200, Background = Brushes.Transparent };
        using Hosted host = Host(list);

        host.ClickEmptyArea(list);

        Assert.Same(host.Above, Keyboard.FocusedElement);
    });

    private static Hosted Host(Control control)
    {
        // The window registers the rule as it is built; a list hosted
        // alone registers it the same way.
        SelectorFocus.RegisterClickRule();
        var above = new Button { Content = "Above" };
        var panel = new StackPanel();
        panel.Children.Add(above);
        panel.Children.Add(control);
        var window = new Window
        {
            Content = panel,
            Width = 400,
            Height = 400,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        Assert.True(above.Focus());
        return new Hosted(window, above);
    }

    private sealed class Hosted(Window window, Button above) : IDisposable
    {
        public Button Above { get; } = above;

        /// <summary>A left press on what the control's lowest point
        /// hit-tests to — below its rows.</summary>
        public void ClickEmptyArea(FrameworkElement control)
        {
            IInputElement hit = control.InputHitTest(new Point(10, control.ActualHeight - 5))
                ?? throw new InvalidOperationException("the empty area hit-tests to nothing.");
            Assert.IsNotType<ListBoxItem>(hit);
            Press((UIElement)hit);
        }

        public void Click(UIElement target) => Press(target);

        private static void Press(UIElement target)
        {
            target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = Mouse.MouseDownEvent,
            });
            PumpedDispatcher.Drain();
        }

        public void Dispose() => window.Close();
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PumpedDispatcher.Run(body);
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
