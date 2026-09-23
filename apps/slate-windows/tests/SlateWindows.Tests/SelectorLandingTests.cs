// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5): a list's landing is a ROW —
/// <see cref="MainWindow.FocusFirstOrSelectedItem"/> puts the keys on the
/// selected row, else the first, and never on the bare container, from
/// which an arrow walked into the menu bar (NVDA pass F4). Hosted lists,
/// real keyboard focus; SelectorLandingCensus holds every landing in the
/// window to this helper.
/// </summary>
public sealed class SelectorLandingTests
{
    [Fact]
    public void TheSelectedRowTakesTheKeys() => RunSta(() =>
    {
        var list = new ListBox { ItemsSource = Items(3), SelectedIndex = 1 };
        using HostedWindow host = Host(list);
        Assert.True(host.Elsewhere.Focus());

        Assert.True(MainWindow.FocusFirstOrSelectedItem(list));

        Assert.Same(list.ItemContainerGenerator.ContainerFromIndex(1), Keyboard.FocusedElement);
        Assert.Equal(1, list.SelectedIndex);
    });

    /// <summary>With nothing selected the first row takes the keys, and the
    /// selection is left alone: selecting would switch the rail's shown
    /// leaf or open a filter result.</summary>
    [Fact]
    public void WithNoSelectionTheFirstRowTakesTheKeysAndNothingIsSelected() => RunSta(() =>
    {
        var list = new ListBox { ItemsSource = Items(3) };
        using HostedWindow host = Host(list);
        Assert.True(host.Elsewhere.Focus());

        Assert.True(MainWindow.FocusFirstOrSelectedItem(list));

        Assert.Same(list.ItemContainerGenerator.ContainerFromIndex(0), Keyboard.FocusedElement);
        Assert.Equal(-1, list.SelectedIndex);
    });

    /// <summary>AR-6: with no row to land on, the list is still the stop.</summary>
    [Fact]
    public void AnEmptyListTakesTheKeysItself() => RunSta(() =>
    {
        var list = new ListBox { ItemsSource = Array.Empty<string>() };
        using HostedWindow host = Host(list);
        Assert.True(host.Elsewhere.Focus());

        Assert.False(MainWindow.FocusFirstOrSelectedItem(list));

        Assert.Same(list, Keyboard.FocusedElement);
    });

    [Fact]
    public void ATabControlLandsOnItsSelectedTab() => RunSta(() =>
    {
        var tabs = new TabControl { ItemsSource = Items(2), SelectedIndex = 1 };
        using HostedWindow host = Host(tabs);
        Assert.True(host.Elsewhere.Focus());

        Assert.True(MainWindow.FocusFirstOrSelectedItem(tabs));

        Assert.IsType<TabItem>(Keyboard.FocusedElement);
        Assert.Same(tabs.ItemContainerGenerator.ContainerFromIndex(1), Keyboard.FocusedElement);
    });

    /// <summary>Which stops are lists for this purpose: a combo box is its
    /// own stop (its items live in the drop-down) and a grid's stop is a
    /// cell its own code seats, so neither is routed to a row container.</summary>
    [Fact]
    public void OnlyListsAndTabControlsAreLandedOnARow() => RunSta(() =>
    {
        Assert.True(MainWindow.IsListLanding(new ListBox()));
        Assert.True(MainWindow.IsListLanding(new ListView()));
        Assert.True(MainWindow.IsListLanding(new TabControl()));
        Assert.False(MainWindow.IsListLanding(new ComboBox()));
        Assert.False(MainWindow.IsListLanding(new DataGrid()));
        Assert.False(MainWindow.IsListLanding(new TreeView()));
        Assert.False(MainWindow.IsListLanding(new Button()));
    });

    /// <summary>A row far down a virtualizing list, just republished: its
    /// container does not exist yet and ScrollIntoView defers until the
    /// generator is ready — the Citations restore's measured case (#1098).
    /// The list holds the keys meanwhile, so they are never stranded, and
    /// the row takes them once its container exists.</summary>
    [Fact]
    public void ARowNotGeneratedYetIsSeatedOnceItsContainerExists() => RunSta(() =>
    {
        ListBox list = VirtualizingList();
        using HostedWindow host = Host(list);
        Assert.True(host.Elsewhere.Focus());
        list.ItemsSource = Items(500);
        list.SelectedIndex = 400;

        Assert.False(MainWindow.FocusFirstOrSelectedItem(list));
        Assert.Same(list, Keyboard.FocusedElement);

        Assert.True(
            PumpedDispatcher.PumpUntil(() => list.ItemContainerGenerator.ContainerFromIndex(400) is ListBoxItem row
                && ReferenceEquals(row, Keyboard.FocusedElement)),
            $"the held landing never reached row 400; the keys are on {Keyboard.FocusedElement}");
    });

    /// <summary>The seat stands down when the keys have moved on before the
    /// container existed: it never takes them back.</summary>
    [Fact]
    public void TheLateSeatNeverTakesTheKeysBack() => RunSta(() =>
    {
        ListBox list = VirtualizingList();
        using HostedWindow host = Host(list);
        Assert.True(host.Elsewhere.Focus());
        list.ItemsSource = Items(500);
        list.SelectedIndex = 400;

        Assert.False(MainWindow.FocusFirstOrSelectedItem(list));
        Assert.True(host.Elsewhere.Focus());

        Assert.True(PumpedDispatcher.PumpUntil(() => list.ItemContainerGenerator.ContainerFromIndex(400) is not null));
        PumpedDispatcher.Drain();
        Assert.Same(host.Elsewhere, Keyboard.FocusedElement);
    });

    private static string[] Items(int count) =>
        Enumerable.Range(0, count).Select(index => $"Row {index}").ToArray();

    private static ListBox VirtualizingList()
    {
        var list = new ListBox { Height = 120 };
        ScrollViewer.SetCanContentScroll(list, true);
        VirtualizingPanel.SetIsVirtualizing(list, true);
        VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        return list;
    }

    private static HostedWindow Host(Control landing)
    {
        var elsewhere = new Button { Content = "Elsewhere" };
        var content = new StackPanel();
        content.Children.Add(elsewhere);
        content.Children.Add(landing);
        var window = new Window
        {
            Content = content,
            Width = 400,
            Height = 300,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        return new HostedWindow(window, elsewhere);
    }

    private sealed class HostedWindow(Window window, Button elsewhere) : IDisposable
    {
        internal Button Elsewhere => elsewhere;

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
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "STA test body timed out.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
