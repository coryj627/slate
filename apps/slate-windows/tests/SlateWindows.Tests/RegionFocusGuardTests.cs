// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4b (#1247, contract R-5; the owner's S3): the per-region focus
/// guard. The element holding the keys goes away under them — disabled,
/// collapsed, removed, its list's rows swapped, its list emptied — and the
/// keys land through the scope's own landing in ONE focus change: never on
/// nothing, the window, a scroll viewer or a bare populated container.
/// </summary>
/// <remarks>
/// A scope of its own in a window of its own: a landing button, the
/// element that goes away, a list, and a focusable scroll viewer and tab
/// control above them — the places WPF's own re-evaluation handed the keys
/// to (a focusable ancestor, else the window). Every change is made the way
/// the app makes it, and the dispatcher is drained through Input priority,
/// where WPF re-evaluates.
/// </remarks>
public sealed class RegionFocusGuardTests
{
    public static TheoryData<string> Departures() =>
        ["disable", "collapse", "unfocusable", "remove"];

    /// <summary>A focused button disabled, collapsed, made unfocusable or
    /// taken out of the tree: the keys go to the scope's landing, once.</summary>
    [Theory]
    [MemberData(nameof(Departures))]
    public void AFocusedElementThatGoesAwayLandsThroughTheScope(string departure) => RunSta(() =>
    {
        using var host = new Host();
        Assert.True(host.Target.Focus());
        host.Forget();

        switch (departure)
        {
            case "disable":
                host.Target.IsEnabled = false;
                break;
            case "collapse":
                host.Target.Visibility = Visibility.Collapsed;
                break;
            case "unfocusable":
                host.Target.Focusable = false;
                break;
            default:
                host.Content.Children.Remove(host.Target);
                break;
        }

        PumpedDispatcher.Drain();

        Assert.Same(host.Home, Keyboard.FocusedElement);
        Assert.Equal([host.Home], host.FocusChanges);
    });

    /// <summary>The element's own stranded landing comes first (the
    /// review's "Load more", the sweep's G4).</summary>
    [Fact]
    public void AnElementsOwnLandingComesFirst() => RunSta(() =>
    {
        using var host = new Host();
        var next = new Button { Content = "Next" };
        host.Content.Children.Add(next);
        host.Window.UpdateLayout();
        RegionFocusGuard.SetStrandedLanding(host.Target, () => next.Focus());
        Assert.True(host.Target.Focus());
        host.Forget();

        host.Target.Visibility = Visibility.Collapsed;
        PumpedDispatcher.Drain();

        Assert.Same(next, Keyboard.FocusedElement);
        Assert.Equal([next], host.FocusChanges);
    });

    /// <summary>A list's rows swapped under the keys (a new ItemsSource, the
    /// canvas prompt's choices, Move To's batches — the sweep's G9, G12): the
    /// removed row's hand-over to the bare list is declined, and the keys
    /// land on a row of the new rows — the selected one — once.</summary>
    [Fact]
    public void RowsSwappedUnderTheKeysLandOnARow() => RunSta(() =>
    {
        using var host = new Host();
        host.List.ItemsSource = new[] { "one", "two", "three" };
        host.Window.UpdateLayout();
        Assert.True(((UIElement)host.List.ItemContainerGenerator.ContainerFromIndex(1)).Focus());
        host.Forget();

        string[] swapped = ["uno", "dos", "tres"];
        host.List.ItemsSource = swapped;
        host.List.SelectedItem = "dos";
        PumpedDispatcher.Drain();

        var row = Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
        Assert.Equal("dos", row.DataContext);
        Assert.DoesNotContain(host.List, host.FocusChanges);
        host.AssertNeverStranded();
    });

    /// <summary>Rows removed one by one from an observable collection (the
    /// shortcuts list's Remove, the dashboard editor's sections): the keys go
    /// to a remaining row, never the bare list.</summary>
    [Fact]
    public void ARowRemovedUnderTheKeysLandsOnARemainingRow() => RunSta(() =>
    {
        using var host = new Host();
        var rows = new ObservableCollection<string>(["one", "two", "three"]);
        host.List.ItemsSource = rows;
        host.Window.UpdateLayout();
        Assert.True(((UIElement)host.List.ItemContainerGenerator.ContainerFromIndex(1)).Focus());
        host.Forget();

        rows.RemoveAt(1);
        PumpedDispatcher.Drain();

        Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
        Assert.DoesNotContain(host.List, host.FocusChanges);
        host.AssertNeverStranded();
    });

    /// <summary>A focusable scroll viewer or tab control INSIDE the scope —
    /// a dashboard's section scroller, a nested tab strip — is no stop
    /// either: WPF's re-evaluation climbed from a rebuilt section's cell to
    /// the dashboard's scroll viewer (the sweep's G10, G19). The keys land
    /// through the scope.</summary>
    [Theory]
    [InlineData("scroll viewer")]
    [InlineData("tab control")]
    public void AContainerInsideTheScopeIsNoStop(string holder) => RunSta(() =>
    {
        using var host = new Host();
        var inner = new Button { Content = "Inner" };
        host.Content.Children.Remove(host.Target);
        if (holder == "scroll viewer")
        {
            host.Content.Children.Add(new ScrollViewer { Content = inner, Height = 60 });
        }
        else
        {
            var tabs = new TabControl();
            tabs.Items.Add(new TabItem { Header = "Inner tab", Content = inner });
            host.Content.Children.Add(tabs);
        }

        host.Window.UpdateLayout();
        Assert.True(inner.Focus());
        host.Forget();

        inner.IsEnabled = false;
        PumpedDispatcher.Drain();

        Assert.Same(host.Home, Keyboard.FocusedElement);
        Assert.Equal([host.Home], host.FocusChanges);
    });

    /// <summary>An EMPTY publication under the keys: the empty list is its
    /// own stop (AR-6) — the keys rest there, not on the window.</summary>
    [Fact]
    public void AListEmptiedUnderTheKeysKeepsThemOnTheList() => RunSta(() =>
    {
        using var host = new Host();
        host.List.ItemsSource = new[] { "one", "two" };
        host.Window.UpdateLayout();
        Assert.True(((UIElement)host.List.ItemContainerGenerator.ContainerFromIndex(0)).Focus());
        host.Forget();

        host.List.ItemsSource = Array.Empty<string>();
        PumpedDispatcher.Drain();

        Assert.Same(host.List, Keyboard.FocusedElement);
        host.AssertNeverStranded();
    });

    /// <summary>A scope that is itself gone takes nothing, and the next
    /// scope out lands the keys.</summary>
    [Fact]
    public void AnInnerScopeThatIsGoneGivesWayToTheNextOut() => RunSta(() =>
    {
        using var host = new Host();
        var inner = new StackPanel();
        var innerTarget = new Button { Content = "Inner" };
        inner.Children.Add(innerTarget);
        host.Content.Children.Add(inner);
        RegionFocusGuard.SetLanding(inner, () => innerTarget.Focus());
        host.Window.UpdateLayout();
        Assert.True(innerTarget.Focus());
        host.Forget();

        inner.Visibility = Visibility.Collapsed;
        PumpedDispatcher.Drain();

        Assert.Same(host.Home, Keyboard.FocusedElement);
        Assert.Equal([host.Home], host.FocusChanges);
    });

    /// <summary>Keys on a live element elsewhere are never taken: a real
    /// stop outside the scope, moved to before WPF re-evaluates, stands.</summary>
    [Fact]
    public void AMoveToALiveStopIsNeverTaken() => RunSta(() =>
    {
        using var host = new Host();
        Assert.True(host.Target.Focus());
        host.Target.IsEnabled = false;
        Assert.True(host.Outside.Focus());
        host.Forget();

        PumpedDispatcher.Drain();

        Assert.Same(host.Outside, Keyboard.FocusedElement);
        Assert.Empty(host.FocusChanges);
    });

    /// <summary>A restore whose token is dead lands in the scopes it was
    /// in (the sweep's G20), not wherever the caller's last resort is.</summary>
    [Fact]
    public void ADeadTokenRestoresIntoItsScope() => RunSta(() =>
    {
        using var host = new Host();
        Assert.True(host.Target.Focus());
        IInputElement token = Keyboard.FocusedElement;
        Assert.True(host.Outside.Focus());
        host.Content.Children.Remove(host.Target);
        host.Forget();

        Assert.True(RegionFocusGuard.LandInScopesOf(token));

        Assert.Same(host.Home, Keyboard.FocusedElement);
    });

    private sealed class Host : IDisposable
    {
        private readonly List<IInputElement?> _stranded = [];

        public Host()
        {
            RegionFocusGuard.Register();
            Content = new StackPanel();
            Home = new Button { Content = "Home" };
            Target = new Button { Content = "Target" };
            List = new ListBox();
            Content.Children.Add(Home);
            Content.Children.Add(Target);
            Content.Children.Add(List);
            Scope = new Border { Child = Content };
            RegionFocusGuard.SetLanding(Scope, () => Home.Focus());
            // The focusable ancestors WPF hands stranded keys to.
            var scroll = new ScrollViewer { Content = Scope };
            var tabs = new TabControl();
            tabs.Items.Add(new TabItem { Header = "Tab", Content = scroll });
            Outside = new Button { Content = "Outside" };
            var root = new DockPanel();
            DockPanel.SetDock(Outside, Dock.Top);
            root.Children.Add(Outside);
            root.Children.Add(tabs);
            Window = new Window
            {
                Content = root,
                Width = 500,
                Height = 400,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ShowActivated = false,
            };
            Window.Show();
            Window.UpdateLayout();
            Keyboard.AddGotKeyboardFocusHandler(Window, (_, e) => FocusChanges.Add(e.NewFocus));
            Keyboard.AddLostKeyboardFocusHandler(Window, (_, e) =>
            {
                if (e.NewFocus is null or System.Windows.Window || (e.NewFocus is Visual visual && PresentationSource.FromVisual(visual) is null))
                {
                    _stranded.Add(e.NewFocus);
                }
            });
        }

        public Window Window { get; }

        public Border Scope { get; }

        public StackPanel Content { get; }

        public Button Home { get; }

        public Button Target { get; }

        public ListBox List { get; }

        public Button Outside { get; }

        public List<IInputElement> FocusChanges { get; } = [];

        public void Forget()
        {
            FocusChanges.Clear();
            _stranded.Clear();
        }

        public void AssertNeverStranded() =>
            Assert.True(
                _stranded.Count == 0,
                "the keys were stranded; focus went " + string.Join(" → ", FocusChanges.Select(focus => focus?.GetType().Name ?? "nothing")));

        public void Dispose() => Window.Close();
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
