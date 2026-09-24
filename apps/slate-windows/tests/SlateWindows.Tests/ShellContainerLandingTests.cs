// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SlateWindows.Canvas;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5; spec review round 23): the container
/// landings the widened census found that STAY landings — each a stop of
/// its own, with no row to land on — keep all four arrows. The elements
/// are the shipped window's, built by its own XAML, lifted with their
/// region into a window of their own between buttons on every side, so an
/// arrow that leaves lands somewhere observable. Keys are real presses
/// through the input manager, so WPF's directional navigation answers
/// them.
/// </summary>
public sealed class ShellContainerLandingTests
{
    /// <summary>The ring's status bar landing (<c>TryLand(StatusBar)</c>
    /// focuses the bar itself: its items take no focus). Measured before
    /// the fix: every arrow left the bar.</summary>
    [Theory]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    public void TheStatusBarKeepsItsArrows(Key key) => RunSta(() =>
    {
        using var host = new Host();
        FrameworkElement region = host.Lift("ShellStatusBarRegion");
        var bar = Assert.IsType<StatusBar>(host.Shell.FindName("ShellStatusBar"));
        Assert.True(region.IsAncestorOf(bar));
        host.Show(region);
        Assert.True(bar.Focus());

        host.Press(key);

        Assert.Same(bar, Keyboard.FocusedElement);
    });

    /// <summary>The Bases query builder's landing (the sheet opens on its
    /// combinator box): a combo box is its own stop, and it takes every
    /// arrow as a move between its choices.</summary>
    [Theory]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    public void TheCombinatorBoxKeepsItsArrows(Key key) => RunSta(() =>
    {
        using var host = new Host();
        FrameworkElement box = host.Lift("BuilderCombinatorBox");
        var combo = Assert.IsType<ComboBox>(box);
        host.Show(combo);
        combo.SelectedIndex = 0;
        Assert.True(combo.Focus());

        host.Press(key);

        Assert.Same(combo, Keyboard.FocusedElement);
    });

    /// <summary>The shipped window, constructed and never shown (the
    /// MoveToFocusTests fixture's reasons: no Application, Jump Lists or
    /// window placement), whose named element is lifted into a shown window
    /// with a button on each side.</summary>
    private sealed class Host : IDisposable
    {
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private Window? _window;

        public Host()
        {
            Assert.Null(Application.Current);
            Shell = new MainWindow();
        }

        public MainWindow Shell { get; }

        public FrameworkElement Lift(string name)
        {
            var element = Assert.IsAssignableFrom<FrameworkElement>(Shell.FindName(name));
            switch (element.Parent)
            {
                case Panel panel:
                    panel.Children.Remove(element);
                    break;
                case Decorator decorator:
                    decorator.Child = null;
                    break;
                case ContentControl content:
                    content.Content = null;
                    break;
                default:
                    throw new InvalidOperationException($"{name}'s parent {element.Parent} cannot give it up.");
            }

            return element;
        }

        public void Show(FrameworkElement center)
        {
            var grid = new Grid();
            for (int index = 0; index < 3; index++)
            {
                grid.RowDefinitions.Add(new RowDefinition());
                grid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            foreach ((string text, int row, int column) in new[] { ("Above", 0, 1), ("Below", 2, 1), ("Left", 1, 0), ("Right", 1, 2) })
            {
                var button = new Button { Content = text };
                Grid.SetRow(button, row);
                Grid.SetColumn(button, column);
                grid.Children.Add(button);
            }

            Grid.SetRow(center, 1);
            Grid.SetColumn(center, 1);
            grid.Children.Add(center);
            _window = new Window
            {
                Content = grid,
                Width = 700,
                Height = 400,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            _window.Show();
            _window.Activate();
            _window.UpdateLayout();
        }

        public void Press(Key key)
        {
            InputManager.Current.ProcessInput(new KeyEventArgs(
                Keyboard.PrimaryDevice, PresentationSource.FromVisual(_window!)!, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            });
            PumpedDispatcher.Drain();
        }

        public void Dispose()
        {
            try
            {
                _window?.Close();
                Shell.Close();
            }
            finally
            {
                CanvasSurfaceView.ShellOverlayIsOpen = _priorOverlayProbe;
            }
        }
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
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
