// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Xml.Linq;
using SlateWindows.Canvas;
using SlateWindows.Graph;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5): the shell's radio groups behave like
/// Windows radio groups — the radio an arrow reaches is the one checked.
/// WPF's own radios moved focus alone, so a screen reader announced a
/// filter or projection the view had not switched to. The behaviour is
/// driven through the real routed KeyDown on a hosted group; the three
/// shipped groups (the Tasks Review filters, the canvas and graph view
/// switchers) are pinned to carry it.
/// </summary>
public sealed class RadioGroupArrowsTests
{
    [Fact]
    public void TheRadioAnArrowReachesIsChecked() => RunSta(() =>
    {
        (StackPanel group, RadioButton[] radios) = Group(3);
        using HostedWindow host = Host(group);
        Assert.True(radios[0].Focus());

        foreach ((Key key, int from, int to) in new[]
                 {
                     (Key.Right, 0, 1),
                     (Key.Down, 1, 2),
                     (Key.Up, 2, 1),
                     (Key.Left, 1, 0),
                 })
        {
            Assert.True(RaiseKeyDown(radios[from], key), $"{key} was not the group's");
            Assert.Same(radios[to], Keyboard.FocusedElement);
            Assert.True(radios[to].IsChecked, $"{key} from radio {from} focused radio {to} without checking it");
            Assert.Equal(1, radios.Count(radio => radio.IsChecked == true));
        }
    });

    /// <summary>The dialog manager's group order, not geometry: the
    /// review's filters wrap onto a second row in a narrow pane, where a
    /// geometric Down would miss the next filter. A radio that cannot take
    /// focus is skipped, and the ends wrap.</summary>
    [Fact]
    public void TheGroupWrapsInItsOrderAndSkipsWhatCannotTakeFocus() => RunSta(() =>
    {
        (StackPanel group, RadioButton[] radios) = Group(4);
        radios[2].IsEnabled = false;
        radios[3].Visibility = Visibility.Collapsed;
        using HostedWindow host = Host(group);

        Assert.Same(radios[0], RadioGroupArrows.Target(group, radios[1], Key.Right, ModifierKeys.None));
        Assert.Same(radios[1], RadioGroupArrows.Target(group, radios[0], Key.Left, ModifierKeys.None));
        Assert.Same(radios[1], RadioGroupArrows.Target(group, radios[0], Key.Down, ModifierKeys.None));
        Assert.Same(radios[1], RadioGroupArrows.Target(group, radios[0], Key.Up, ModifierKeys.None));
    });

    /// <summary>A modified arrow is never the group's — Ctrl+Alt+Arrow is
    /// the window's pane chord, which the window's KeyBinding sees only if
    /// nothing handled the key on its way up — and neither is any other
    /// key or a radio from another panel.</summary>
    [Fact]
    public void OnlyAnUnmodifiedArrowFromTheGroupsOwnRadioIsTheGroups() => RunSta(() =>
    {
        (StackPanel group, RadioButton[] radios) = Group(3);
        using HostedWindow host = Host(group);

        foreach (ModifierKeys modifiers in new[]
                 {
                     ModifierKeys.Control,
                     ModifierKeys.Alt,
                     ModifierKeys.Shift,
                     ModifierKeys.Control | ModifierKeys.Alt,
                 })
        {
            Assert.Null(RadioGroupArrows.Target(group, radios[0], Key.Right, modifiers));
        }

        foreach (Key key in new[] { Key.Tab, Key.Home, Key.End, Key.Space, Key.Enter, Key.System })
        {
            Assert.Null(RadioGroupArrows.Target(group, radios[0], key, ModifierKeys.None));
        }

        Assert.Null(RadioGroupArrows.Target(group, new RadioButton(), Key.Right, ModifierKeys.None));
    });

    [Fact]
    public void RightToLeftMirrorsLeftAndRight() => RunSta(() =>
    {
        (StackPanel group, RadioButton[] radios) = Group(3);
        group.FlowDirection = FlowDirection.RightToLeft;
        using HostedWindow host = Host(group);

        Assert.Same(radios[0], RadioGroupArrows.Target(group, radios[1], Key.Right, ModifierKeys.None));
        Assert.Same(radios[2], RadioGroupArrows.Target(group, radios[1], Key.Left, ModifierKeys.None));
        Assert.Same(radios[2], RadioGroupArrows.Target(group, radios[1], Key.Down, ModifierKeys.None));
    });

    /// <summary>The Tasks Review filters are TwoWay-bound: the check must
    /// reach the view model (the filter applies) and leave the binding in
    /// place, the way RadioButton.OnToggle's SetCurrentValue does — a
    /// local value would sever it and freeze the chip.</summary>
    [Fact]
    public void TheCheckReachesATwoWayBindingAndLeavesItBound() => RunSta(() =>
    {
        var filters = new Filters();
        (StackPanel group, RadioButton[] radios) = Group(2);
        radios[0].SetBinding(RadioButton.IsCheckedProperty, new Binding(nameof(Filters.First)) { Source = filters, Mode = BindingMode.TwoWay });
        radios[1].SetBinding(RadioButton.IsCheckedProperty, new Binding(nameof(Filters.Second)) { Source = filters, Mode = BindingMode.TwoWay });
        using HostedWindow host = Host(group);
        Assert.True(radios[0].Focus());

        Assert.True(RaiseKeyDown(radios[0], Key.Right));
        Assert.True(filters.Second, "the arrow's check did not reach the bound source");

        filters.Choose(first: true);
        Assert.True(radios[0].IsChecked, "the binding did not survive the arrow's check");
        Assert.False(radios[1].IsChecked);
    });

    [Fact]
    public void TheBehaviourBelongsOnThePanel() => RunSta(() =>
        Assert.Throws<InvalidOperationException>(() => RadioGroupArrows.SetIsEnabled(new RadioButton(), true)));

    [Fact]
    public void TheReviewFiltersAreAWindowsRadioGroup()
    {
        XDocument window = XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"));
        XName name = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        XElement group = window.Descendants()
            .Single(element => (string?)element.Attribute(name) == "PanelReviewFilterAll")
            .Parent!;
        Assert.Equal(
            ["PanelReviewFilterAll", "PanelReviewFilterDueToday", "PanelReviewFilterOverdue", "PanelReviewFilterThisWeek"],
            group.Elements().Select(radio => (string?)radio.Attribute(name)));
        Assert.Equal("Cycle", (string?)group.Attribute("KeyboardNavigation.DirectionalNavigation"));
        Assert.Equal("True", (string?)group.Attribute(XName.Get("RadioGroupArrows.IsEnabled", "clr-namespace:SlateWindows")));
    }

    [Fact]
    public void TheCanvasSwitcherIsAWindowsRadioGroup() => RunSta(() =>
    {
        var surface = new CanvasSurfaceView();
        Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetDirectionalNavigation(surface.SwitcherForTests));
        Assert.True(RadioGroupArrows.GetIsEnabled(surface.SwitcherForTests));
    });

    [Fact]
    public void TheGraphSwitcherIsAWindowsRadioGroup() => RunSta(() =>
    {
        var surface = new GraphSurfaceView();
        Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetDirectionalNavigation(surface.SwitcherForTests));
        Assert.True(RadioGroupArrows.GetIsEnabled(surface.SwitcherForTests));
    });

    private static (StackPanel Group, RadioButton[] Radios) Group(int count)
    {
        var group = new StackPanel { Orientation = Orientation.Horizontal };
        RadioButtons(group, count, out RadioButton[] radios);
        RadioGroupArrows.SetIsEnabled(group, true);
        radios[0].IsChecked = true;
        return (group, radios);
    }

    private static void RadioButtons(Panel group, int count, out RadioButton[] radios)
    {
        string groupName = "Group" + Guid.NewGuid().ToString("N");
        radios = Enumerable.Range(0, count)
            .Select(index => new RadioButton { Content = $"Choice {index}", GroupName = groupName })
            .ToArray();
        foreach (RadioButton radio in radios)
        {
            group.Children.Add(radio);
        }
    }

    /// <summary>A real BUBBLING KeyDown from the radio, the phase the
    /// behaviour listens in; returns whether something handled it.</summary>
    private static bool RaiseKeyDown(UIElement target, Key key)
    {
        var args = new KeyEventArgs(
            Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(target)
                ?? throw new InvalidOperationException("the radio is not in a window."),
            0,
            key)
        {
            RoutedEvent = Keyboard.KeyDownEvent,
        };
        target.RaiseEvent(args);
        return args.Handled;
    }

    private sealed class Filters : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public bool First { get; set; } = true;

        public bool Second { get; set; }

        public void Choose(bool first)
        {
            First = first;
            Second = !first;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(First)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Second)));
        }
    }

    private static HostedWindow Host(UIElement content)
    {
        var window = new Window
        {
            Content = content,
            Width = 400,
            Height = 200,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        return new HostedWindow(window);
    }

    private sealed class HostedWindow(Window window) : IDisposable
    {
        public void Dispose() => window.Close();
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "STA test body timed out.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
