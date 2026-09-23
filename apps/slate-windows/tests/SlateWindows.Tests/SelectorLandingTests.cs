// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5): a list's landing is a ROW —
/// <see cref="SelectorFocus.FocusFirstOrSelectedItem"/> puts the keys on the
/// selected row, else the first that takes them — and a list that HAS items
/// never takes the keys itself, in any state: every keyboard focus change in
/// the hosted window is recorded, and the populated list is never among
/// them. An empty list lands on its showing notice, else on itself (AR-6).
/// Hosted lists, real keyboard focus; SelectorLandingCensus holds every
/// landing in the shell to this helper.
/// </summary>
public sealed class SelectorLandingTests
{
    [Fact]
    public void TheSelectedRowTakesTheKeys() => RunSta(() =>
    {
        var list = new ListBox { ItemsSource = Items(3), SelectedIndex = 1 };
        using HostedWindow host = Host(list);
        Assert.True(host.Elsewhere.Focus());

        Assert.True(SelectorFocus.FocusFirstOrSelectedItem(list));

        Assert.Same(list.ItemContainerGenerator.ContainerFromIndex(1), Keyboard.FocusedElement);
        Assert.Equal(1, list.SelectedIndex);
        host.AssertNeverFocused(list);
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

        Assert.True(SelectorFocus.FocusFirstOrSelectedItem(list));

        Assert.Same(list.ItemContainerGenerator.ContainerFromIndex(0), Keyboard.FocusedElement);
        Assert.Equal(-1, list.SelectedIndex);
        host.AssertNeverFocused(list);
    });

    /// <summary>Spec §5.2.2: an EMPTY list's stop is its notice when one
    /// is showing — the first of the named notices that is visible and takes
    /// the keys, a collapsed one passed over. The landing happened, so the
    /// caller does not fall back.</summary>
    [Fact]
    public void AnEmptyListLandsOnItsShowingNotice() => RunSta(() =>
    {
        var list = new ListBox { ItemsSource = Array.Empty<string>() };
        var hidden = new TextBlock { Text = "Select a file.", Focusable = true, Visibility = Visibility.Collapsed };
        var notice = new TextBlock { Text = "This note has no citations.", Focusable = true };
        using HostedWindow host = Host(list, hidden, notice);
        Assert.True(host.Elsewhere.Focus());

        Assert.True(SelectorFocus.FocusFirstOrSelectedItem(list, hidden, notice));

        Assert.Same(notice, Keyboard.FocusedElement);
    });

    /// <summary>AR-6: with no row to land on and no notice showing, the
    /// EMPTY list is still the stop.</summary>
    [Fact]
    public void AnEmptyListWithNoNoticeShowingTakesTheKeysItself() => RunSta(() =>
    {
        var list = new ListBox { ItemsSource = Array.Empty<string>() };
        var hidden = new TextBlock { Text = "This note has no citations.", Focusable = true, Visibility = Visibility.Collapsed };
        using HostedWindow host = Host(list, hidden);
        Assert.True(host.Elsewhere.Focus());

        Assert.True(SelectorFocus.FocusFirstOrSelectedItem(list, hidden));
        Assert.Same(list, Keyboard.FocusedElement);

        Assert.True(host.Elsewhere.Focus());
        Assert.True(SelectorFocus.FocusFirstOrSelectedItem(list));
        Assert.Same(list, Keyboard.FocusedElement);
    });

    /// <summary>A notice speaks for an EMPTY list only: with rows, the row
    /// is the stop even while a notice shows.</summary>
    [Fact]
    public void AListWithRowsLandsOnARowWhateverItsNoticeShows() => RunSta(() =>
    {
        var list = new ListBox { ItemsSource = Items(2) };
        var notice = new TextBlock { Text = "Loading.", Focusable = true };
        using HostedWindow host = Host(list, notice);
        Assert.True(host.Elsewhere.Focus());

        Assert.True(SelectorFocus.FocusFirstOrSelectedItem(list, notice));

        Assert.Same(list.ItemContainerGenerator.ContainerFromIndex(0), Keyboard.FocusedElement);
        host.AssertNeverFocused(list);
    });

    /// <summary>A row that cannot take the keys — a Bases list's group
    /// heading is a disabled separator — is passed over for the next.</summary>
    [Fact]
    public void WithNoSelectionADisabledHeadingIsPassedOver() => RunSta(() =>
    {
        var list = new ListBox { ItemsSource = Items(3) };
        using HostedWindow host = Host(list);
        ((ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0)).IsEnabled = false;
        Assert.True(host.Elsewhere.Focus());

        Assert.True(SelectorFocus.FocusFirstOrSelectedItem(list));

        Assert.Same(list.ItemContainerGenerator.ContainerFromIndex(1), Keyboard.FocusedElement);
        host.AssertNeverFocused(list);
    });

    /// <summary>A list whose rows ALL refuse the keys is not landed at all:
    /// the call answers false, the keys stay where they were for the
    /// caller's stable stop, and the populated list never takes them.</summary>
    [Fact]
    public void AListWhoseRowsAllRefuseTheKeysIsNeverLandedOn() => RunSta(() =>
    {
        var list = new ListBox { ItemsSource = Items(2) };
        using HostedWindow host = Host(list);
        ((ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0)).IsEnabled = false;
        ((ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(1)).IsEnabled = false;
        Assert.True(host.Elsewhere.Focus());

        Assert.False(SelectorFocus.FocusFirstOrSelectedItem(list));
        PumpedDispatcher.Drain();

        Assert.Same(host.Elsewhere, Keyboard.FocusedElement);
        host.AssertNeverFocused(list);
    });

    /// <summary>A SHOWN list whose rows cannot be realized at all — it has
    /// no items host, so no container is ever generated — is the state in
    /// which holding the keys on the list would actually move them there:
    /// the call answers false, the keys stay where they were, and no pumped
    /// step after it hands them to the list.</summary>
    [Fact]
    public void AShownListWhoseRowsCannotBeRealizedIsNeverLandedOnItself() => RunSta(() =>
    {
        var list = new ListBox
        {
            ItemsSource = Items(3),
            Template = new ControlTemplate(typeof(ListBox)) { VisualTree = new FrameworkElementFactory(typeof(Border)) },
        };
        using HostedWindow host = Host(list);
        Assert.True(list.IsVisible && list.Focusable && list.HasItems);
        Assert.True(host.Elsewhere.Focus());

        Assert.False(SelectorFocus.FocusFirstOrSelectedItem(list));
        for (int step = 0; step < 5; step++)
        {
            PumpedDispatcher.Drain();
        }

        Assert.Same(host.Elsewhere, Keyboard.FocusedElement);
        host.AssertNeverFocused(list);
    });

    [Fact]
    public void ATabControlLandsOnItsSelectedTab() => RunSta(() =>
    {
        var tabs = new TabControl { ItemsSource = Items(2), SelectedIndex = 1 };
        using HostedWindow host = Host(tabs);
        Assert.True(host.Elsewhere.Focus());

        Assert.True(SelectorFocus.FocusFirstOrSelectedItem(tabs));

        Assert.IsType<TabItem>(Keyboard.FocusedElement);
        Assert.Same(tabs.ItemContainerGenerator.ContainerFromIndex(1), Keyboard.FocusedElement);
        host.AssertNeverFocused(tabs);
    });

    /// <summary>Which stops are lists for this purpose: a combo box is its
    /// own stop (its items live in the drop-down) and a grid's stop is a
    /// cell its own code seats, so neither is routed to a row container.</summary>
    [Fact]
    public void OnlyListsAndTabControlsAreLandedOnARow() => RunSta(() =>
    {
        Assert.True(SelectorFocus.IsListLanding(new ListBox()));
        Assert.True(SelectorFocus.IsListLanding(new ListView()));
        Assert.True(SelectorFocus.IsListLanding(new TabControl()));
        Assert.False(SelectorFocus.IsListLanding(new ComboBox()));
        Assert.False(SelectorFocus.IsListLanding(new DataGrid()));
        Assert.False(SelectorFocus.IsListLanding(new TreeView()));
        Assert.False(SelectorFocus.IsListLanding(new Button()));
    });

    /// <summary>A leaf's first stop that is a list lands on its row, never
    /// on the bare list.</summary>
    [Fact]
    public void AListStopLandsOnItsRow() => RunSta(() =>
    {
        var list = new ListBox { ItemsSource = Items(3), SelectedIndex = 2 };
        using HostedWindow host = Host(list);
        Assert.True(host.Elsewhere.Focus());

        Assert.True(SelectorFocus.LandOnStop(list));

        Assert.Same(list.ItemContainerGenerator.ContainerFromIndex(2), Keyboard.FocusedElement);
        host.AssertNeverFocused(list);
    });

    /// <summary>A leaf's first stop that hands the keys on — a tree to its
    /// selected item — answers false from its own Focus() though the keys
    /// are inside it (W7-6 #1240). LandOnStop judges where the keys END UP,
    /// so the reveal's fallback to the rail does not take them away
    /// again.</summary>
    [Fact]
    public void AStopThatHandsTheKeysOnHasLanded() => RunSta(() =>
    {
        var tree = new TreeView();
        var heading = new TreeViewItem { Header = "Heading" };
        tree.Items.Add(heading);
        using HostedWindow host = Host(tree);
        heading.IsSelected = true;
        // The precondition that makes this fact discriminate: the tree's
        // own Focus() answers false while its item takes the keys.
        Assert.False(tree.Focus());
        Assert.Same(heading, Keyboard.FocusedElement);
        Assert.True(host.Elsewhere.Focus());

        Assert.True(SelectorFocus.LandOnStop(tree));

        Assert.Same(heading, Keyboard.FocusedElement);
    });

    /// <summary>A row far down a virtualizing list that was JUST populated:
    /// its container does not exist and the generator has not produced any
    /// yet, so a plain ScrollIntoView defers to Loaded priority — the
    /// Citations restore's measured case (#1098). The row is realized in
    /// the same call and takes the keys; through every pumped step after
    /// it, the populated list never held them, and row 400 still has
    /// them.</summary>
    [Fact]
    public void AFarRowOfAJustPopulatedListLandsWithoutTheBareList() => RunSta(() =>
    {
        ListBox list = VirtualizingList();
        using HostedWindow host = Host(list);
        Assert.True(host.Elsewhere.Focus());
        list.ItemsSource = Items(500);
        list.SelectedIndex = 400;

        Assert.True(
            SelectorFocus.FocusFirstOrSelectedItem(list),
            $"row 400 was not realized in the same call; the keys are on {Keyboard.FocusedElement}");
        Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
        Assert.Same(list.ItemContainerGenerator.ContainerFromIndex(400), Keyboard.FocusedElement);
        for (int step = 0; step < 5; step++)
        {
            PumpedDispatcher.Drain();
        }

        host.AssertNeverFocused(list);
        Assert.Same(list.ItemContainerGenerator.ContainerFromIndex(400), Keyboard.FocusedElement);
    });

    /// <summary>A row with no container even after a synchronous realize —
    /// the list is not laid out at all — answers false and is seated once
    /// the container exists, while the keys are still where the caller
    /// left them; the populated list never holds them meanwhile.</summary>
    [Fact]
    public void AnUnrealizedRowIsSeatedLaterNeverThroughTheBareList() => RunSta(() =>
    {
        ListBox list = VirtualizingList();
        list.Visibility = Visibility.Collapsed;
        using HostedWindow host = Host(list);
        Assert.True(host.Elsewhere.Focus());
        list.ItemsSource = Items(50);
        list.SelectedIndex = 20;

        Assert.False(SelectorFocus.FocusFirstOrSelectedItem(list));
        Assert.Same(host.Elsewhere, Keyboard.FocusedElement);
        list.Visibility = Visibility.Visible;

        Assert.True(
            PumpedDispatcher.PumpUntil(() => list.ItemContainerGenerator.ContainerFromIndex(20) is ListBoxItem row
                && ReferenceEquals(row, Keyboard.FocusedElement)),
            $"the deferred seat never reached row 20; the keys are on {Keyboard.FocusedElement}");
        host.AssertNeverFocused(list);
    });

    /// <summary>The deferred seat stands down when the keys have moved on
    /// before the container existed: it never takes them back.</summary>
    [Fact]
    public void TheLateSeatNeverTakesTheKeysBack() => RunSta(() =>
    {
        ListBox list = VirtualizingList();
        list.Visibility = Visibility.Collapsed;
        using HostedWindow host = Host(list);
        Assert.True(host.Elsewhere.Focus());
        list.ItemsSource = Items(50);
        list.SelectedIndex = 20;

        Assert.False(SelectorFocus.FocusFirstOrSelectedItem(list));
        Assert.True(host.Other.Focus());
        list.Visibility = Visibility.Visible;

        Assert.True(PumpedDispatcher.PumpUntil(() => list.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated));
        PumpedDispatcher.Drain();
        Assert.Same(host.Other, Keyboard.FocusedElement);
        host.AssertNeverFocused(list);
    });

    /// <summary>A newer landing supersedes a pending seat — the caller's own
    /// fallback among them — even one that leaves the keys exactly where
    /// they were: the fallback re-lands the rail row that already had
    /// them, and the older request's row never takes them from it.</summary>
    [Fact]
    public void ANewerLandingSupersedesAPendingSeat() => RunSta(() =>
    {
        ListBox pending = VirtualizingList();
        pending.Visibility = Visibility.Collapsed;
        var newer = new ListBox { ItemsSource = Items(3) };
        using HostedWindow host = Host(pending, newer);
        Assert.True(SelectorFocus.FocusFirstOrSelectedItem(newer));
        object newerRow = newer.ItemContainerGenerator.ContainerFromIndex(0);
        pending.ItemsSource = Items(50);
        pending.SelectedIndex = 20;

        Assert.False(SelectorFocus.FocusFirstOrSelectedItem(pending));
        Assert.True(SelectorFocus.FocusFirstOrSelectedItem(newer));
        Assert.Same(newerRow, Keyboard.FocusedElement);
        pending.Visibility = Visibility.Visible;

        Assert.True(PumpedDispatcher.PumpUntil(() => pending.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated));
        PumpedDispatcher.Drain();
        Assert.Same(newerRow, Keyboard.FocusedElement);
        host.AssertNeverFocused(pending);
        host.AssertNeverFocused(newer);
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

    private static HostedWindow Host(Control landing, params UIElement[] siblings)
    {
        var elsewhere = new Button { Content = "Elsewhere" };
        var other = new Button { Content = "Other" };
        var content = new StackPanel();
        content.Children.Add(elsewhere);
        content.Children.Add(other);
        foreach (UIElement sibling in siblings)
        {
            content.Children.Add(sibling);
        }
        content.Children.Add(landing);
        var window = new Window
        {
            Content = content,
            Width = 400,
            Height = 400,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        return new HostedWindow(window, elsewhere, other);
    }

    /// <summary>The hosted window, with every keyboard focus change in it
    /// recorded — the helper must never hand the keys to a populated list,
    /// not even for a moment a later step repairs.</summary>
    private sealed class HostedWindow : IDisposable
    {
        private readonly Window _window;
        private readonly List<IInputElement> _focusChanges = [];

        internal HostedWindow(Window window, Button elsewhere, Button other)
        {
            _window = window;
            Elsewhere = elsewhere;
            Other = other;
            window.AddHandler(
                Keyboard.GotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler((_, e) => _focusChanges.Add(e.NewFocus)),
                handledEventsToo: true);
        }

        internal Button Elsewhere { get; }

        internal Button Other { get; }

        internal void AssertNeverFocused(UIElement list) =>
            Assert.True(
                !_focusChanges.Any(focus => ReferenceEquals(focus, list)),
                $"the populated {list.GetType().Name} itself took the keys; focus went "
                + string.Join(" → ", _focusChanges.Select(focus => focus.GetType().Name)));

        public void Dispose() => _window.Close();
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
