// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Grids;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5; spec review round 23): the grid's two
/// container landings the widened census found — the empty grid's own
/// stop, and the populated grid a cell that could not be realized fell
/// back to. The first fact is the platform, measured: DataGrid moves
/// between cells only from a cell, so from the grid itself every arrow goes
/// to directional navigation, a current cell or not. Every key here is a
/// real press through the input manager, and every grid sits between four
/// buttons, so an arrow that leaves lands somewhere observable.
/// </summary>
public sealed class GridLandingTests
{
    private sealed record Row(string Name);

    [Fact]
    public void WpfGivesABareGridsArrowsToDirectionalNavigation() => RunSta(() =>
    {
        var grid = new DataGrid { ItemsSource = new[] { new Row("one"), new Row("two") }, AutoGenerateColumns = true };
        using Hosted host = Host(grid);
        grid.CurrentCell = new DataGridCellInfo(grid.Items[0], grid.Columns[0]);
        Assert.True(grid.Focus());

        host.Press(Key.Left);

        Assert.Same(host.Left, Keyboard.FocusedElement);
    });

    /// <summary>An EMPTY grid is its own stop, and it keeps every
    /// arrow.</summary>
    [Theory]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    public void AnEmptyGridKeepsItsArrows(Key key) => RunSta(() =>
    {
        AccessibleDataGrid grid = BoundGrid([]);
        using Hosted host = Host(grid);
        Assert.True(grid.FocusFirstCell());
        Assert.Same(grid.Grid, Keyboard.FocusedElement);

        host.Press(key);

        Assert.Same(grid.Grid, Keyboard.FocusedElement);
    });

    /// <summary>A populated grid holding the keys itself — where WPF puts
    /// them when their row goes away — lands the first arrow on a
    /// cell.</summary>
    [Theory]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    public void FromTheBareGridAnArrowLandsOnACell(Key key) => RunSta(() =>
    {
        AccessibleDataGrid grid = BoundGrid([new Row("one"), new Row("two")]);
        using Hosted host = Host(grid);
        Assert.True(grid.Grid.Focus());

        host.Press(key);

        Assert.IsType<DataGridCell>(Keyboard.FocusedElement);
        Assert.True(grid.Grid.IsKeyboardFocusWithin, $"{key} took the keys out of the grid to {Keyboard.FocusedElement}");
    });

    /// <summary>A SHOWN grid whose rows cannot be realized — it has no rows
    /// presenter at all — is the state in which the old fallback put the
    /// keys on the populated grid: the landing answers false, the keys stay,
    /// and the grid itself never has them.</summary>
    [Fact]
    public void AnUnrealizableCellIsNeverLandedOnTheBareGrid() => RunSta(() =>
    {
        AccessibleDataGrid grid = BoundGrid([new Row("one"), new Row("two")]);
        grid.Grid.Template = new ControlTemplate(typeof(DataGrid)) { VisualTree = new FrameworkElementFactory(typeof(Border)) };
        using Hosted host = Host(grid);
        Assert.True(grid.Grid.IsVisible && grid.Grid.Focusable);
        Assert.True(host.Above.Focus());
        host.RecordFocus();

        Assert.False(grid.FocusFirstCell());
        for (int step = 0; step < 5; step++)
        {
            PumpedDispatcher.Drain();
        }

        Assert.Same(host.Above, Keyboard.FocusedElement);
        host.AssertNeverFocused(grid.Grid);
    });

    /// <summary>A cell that could not be realized is seated once it can be,
    /// while the keys have not moved — never through the bare grid.</summary>
    [Fact]
    public void ACellRealizedLaterIsSeatedWithoutTheBareGrid() => RunSta(() =>
    {
        AccessibleDataGrid grid = BoundGrid([new Row("one"), new Row("two")]);
        grid.Visibility = Visibility.Collapsed;
        using Hosted host = Host(grid);
        Assert.True(host.Above.Focus());
        host.RecordFocus();

        Assert.False(grid.FocusFirstCell());
        Assert.Same(host.Above, Keyboard.FocusedElement);
        grid.Visibility = Visibility.Visible;

        Assert.True(
            PumpedDispatcher.PumpUntil(() => Keyboard.FocusedElement is DataGridCell && grid.Grid.IsKeyboardFocusWithin),
            $"the seat never reached the first cell; the keys are on {Keyboard.FocusedElement}");
        host.AssertNeverFocused(grid.Grid);
    });

    private static AccessibleDataGrid BoundGrid(IReadOnlyList<object> rows)
    {
        var grid = new AccessibleDataGrid { Announce = _ => { } };
        grid.Bind(
            [new AccessibleGridColumn { Header = "Name", Cell = row => ((Row)row).Name, IsRowHeader = true }],
            rows,
            $"{rows.Count} rows.",
            "Rows");
        return grid;
    }

    private static Hosted Host(FrameworkElement center)
    {
        var grid = new Grid();
        for (int index = 0; index < 3; index++)
        {
            grid.RowDefinitions.Add(new RowDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        Button Place(string text, int row, int column)
        {
            var button = new Button { Content = text };
            Grid.SetRow(button, row);
            Grid.SetColumn(button, column);
            grid.Children.Add(button);
            return button;
        }

        Button above = Place("Above", 0, 1);
        _ = Place("Below", 2, 1);
        Button left = Place("Left", 1, 0);
        _ = Place("Right", 1, 2);
        Grid.SetRow(center, 1);
        Grid.SetColumn(center, 1);
        grid.Children.Add(center);
        var window = new Window
        {
            Content = grid,
            Width = 700,
            Height = 500,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        return new Hosted(window, above, left);
    }

    private sealed class Hosted(Window window, Button above, Button left) : IDisposable
    {
        private readonly List<IInputElement> _focusChanges = [];

        internal Button Above { get; } = above;

        internal Button Left { get; } = left;

        internal void RecordFocus() =>
            window.AddHandler(
                Keyboard.GotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler((_, e) => _focusChanges.Add(e.NewFocus)),
                handledEventsToo: true);

        internal void AssertNeverFocused(UIElement grid) =>
            Assert.True(
                !_focusChanges.Any(focus => ReferenceEquals(focus, grid)),
                "the populated grid itself took the keys; focus went "
                + string.Join(" → ", _focusChanges.Select(focus => focus.GetType().Name)));

        internal void Press(Key key)
        {
            InputManager.Current.ProcessInput(new KeyEventArgs(
                Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
