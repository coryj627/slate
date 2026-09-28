// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

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

    /// <summary>Codex round 4: a landing on the GRID — SelectorFocus
    /// .LandOnStop, the shell's one landing for element-typed targets, given
    /// a restore token captured while an empty grid held the keys — puts
    /// them on a cell through the grid's substrate: the first, with no
    /// current cell.</summary>
    [Fact]
    public void AGridStopLandsOnACell() => RunSta(() =>
    {
        var announced = new List<A11yEvent>();
        AccessibleDataGrid grid = BoundGrid([new Row("one"), new Row("two")], announced);
        using Hosted host = Host(grid);
        Assert.True(host.Above.Focus());
        host.RecordFocus();
        announced.Clear();

        Assert.True(SelectorFocus.LandOnStop(grid.Grid));

        var cell = Assert.IsType<DataGridCell>(Keyboard.FocusedElement);
        Assert.Equal("one", Assert.IsType<Row>(cell.DataContext).Name);
        host.AssertNeverFocused(grid.Grid);
        Assert.Empty(announced);
    });

    /// <summary>...and the current cell while its row is still bound —
    /// silently: a re-seat's speech is the focus change's (contract 34 C6;
    /// codex round 5).</summary>
    [Fact]
    public void AGridStopLandsOnItsCurrentCell() => RunSta(() =>
    {
        var announced = new List<A11yEvent>();
        AccessibleDataGrid grid = BoundGrid([new Row("one"), new Row("two")], announced);
        using Hosted host = Host(grid);
        Assert.True(grid.SelectRow(row => ((Row)row).Name == "two"));
        Assert.True(host.Above.Focus());
        host.RecordFocus();
        announced.Clear();

        Assert.True(SelectorFocus.LandOnStop(grid.Grid));

        var cell = Assert.IsType<DataGridCell>(Keyboard.FocusedElement);
        Assert.Equal("two", Assert.IsType<Row>(cell.DataContext).Name);
        host.AssertNeverFocused(grid.Grid);
        Assert.Empty(announced);
    });

    /// <summary>Codex round 5 (contract 34 C6; t0 §1.5): a restore token
    /// captured while the EMPTY grid held the keys (its own stop) and
    /// restored after the grid filled behind an overlay lands on a cell and
    /// says nothing — the focus change is the speech. It posted
    /// GridRowMoved on every restore.</summary>
    [Fact]
    public void AnEmptyGridRestoredAfterItFilledLandsOnACellSilently() => RunSta(() =>
    {
        var announced = new List<A11yEvent>();
        AccessibleDataGrid grid = BoundGrid([], announced);
        using Hosted host = Host(grid);
        Assert.True(grid.FocusFirstCell());
        IInputElement token = Keyboard.FocusedElement;
        Assert.Same(grid.Grid, token);
        Assert.True(host.Above.Focus());
        Rebind(grid, [new Row("one"), new Row("two")]);
        PumpedDispatcher.Drain();
        Assert.Same(host.Above, Keyboard.FocusedElement);
        host.RecordFocus();
        announced.Clear();

        Assert.True(SelectorFocus.LandOnStop((UIElement)token));

        var cell = Assert.IsType<DataGridCell>(Keyboard.FocusedElement);
        Assert.Equal("one", Assert.IsType<Row>(cell.DataContext).Name);
        host.AssertNeverFocused(grid.Grid);
        Assert.Empty(announced);
    });

    /// <summary>The deferred seat is as silent as the immediate one: a
    /// restore onto a grid whose cell cannot be realized yet seats it
    /// later, without a movement line.</summary>
    [Fact]
    public void AnUnrealizedRestoreSeatedLaterIsSilentToo() => RunSta(() =>
    {
        var announced = new List<A11yEvent>();
        AccessibleDataGrid grid = BoundGrid([new Row("one"), new Row("two")], announced);
        grid.Visibility = Visibility.Collapsed;
        using Hosted host = Host(grid);
        Assert.True(host.Above.Focus());
        host.RecordFocus();
        announced.Clear();

        Assert.False(SelectorFocus.LandOnStop(grid.Grid));
        grid.Visibility = Visibility.Visible;

        Assert.True(
            PumpedDispatcher.PumpUntil(() => Keyboard.FocusedElement is DataGridCell && grid.Grid.IsKeyboardFocusWithin),
            $"the seat never reached a cell; the keys are on {Keyboard.FocusedElement}");
        host.AssertNeverFocused(grid.Grid);
        Assert.Empty(announced);
    });

    /// <summary>The silence is the re-seat's alone: the reader's own arrow
    /// from the restored cell still announces its move.</summary>
    [Fact]
    public void AReadersArrowAfterASilentRestoreStillAnnounces() => RunSta(() =>
    {
        var announced = new List<A11yEvent>();
        AccessibleDataGrid grid = BoundGrid([new Row("one"), new Row("two")], announced);
        using Hosted host = Host(grid);
        Assert.True(host.Above.Focus());
        Assert.True(SelectorFocus.LandOnStop(grid.Grid));
        Assert.Empty(announced);

        host.Press(Key.Down);

        var cell = Assert.IsType<DataGridCell>(Keyboard.FocusedElement);
        Assert.Equal("two", Assert.IsType<Row>(cell.DataContext).Name);
        Assert.IsType<A11yEvent.GridRowMoved>(Assert.Single(announced));
    });

    /// <summary>Codex round 5 (R-5): an EMPTY grid is its own stop, so the
    /// keys can sit on it while its rows load. The rows published under
    /// them re-land them on a cell — the first, silently — never leaving
    /// them on the bare populated grid.</summary>
    [Fact]
    public void RowsFillingTheEmptyGridUnderTheKeysLandThemOnACellSilently() => RunSta(() =>
    {
        var announced = new List<A11yEvent>();
        AccessibleDataGrid grid = BoundGrid([], announced);
        using Hosted host = Host(grid);
        Assert.True(grid.FocusFirstCell());
        Assert.Same(grid.Grid, Keyboard.FocusedElement);
        host.RecordFocus();
        announced.Clear();

        Rebind(grid, [new Row("one"), new Row("two")]);
        PumpedDispatcher.Drain();

        var cell = Assert.IsType<DataGridCell>(Keyboard.FocusedElement);
        Assert.Equal("one", Assert.IsType<Row>(cell.DataContext).Name);
        host.AssertNeverFocused(grid.Grid);
        Assert.Empty(announced);
    });

    /// <summary>A re-publish under the reader — every consuming surface
    /// re-binds on each publish — destroys the cell holding the keys; they
    /// land again on the reader's cell (the currency the bind restores by
    /// row identity), silently, never on the bare populated grid or
    /// nowhere.</summary>
    [Fact]
    public void ARepublishUnderTheReaderKeepsTheirCellSilently() => RunSta(() =>
    {
        var announced = new List<A11yEvent>();
        AccessibleDataGrid grid = BoundGrid([new Row("one"), new Row("two"), new Row("three")], announced);
        using Hosted host = Host(grid);
        Assert.True(grid.SelectRow(row => ((Row)row).Name == "two", moveFocus: true));
        Assert.Equal("two", Assert.IsType<Row>(Assert.IsType<DataGridCell>(Keyboard.FocusedElement).DataContext).Name);
        host.RecordFocus();
        announced.Clear();

        Rebind(grid, [new Row("one"), new Row("two"), new Row("three")]);
        PumpedDispatcher.Drain();

        var cell = Assert.IsType<DataGridCell>(Keyboard.FocusedElement);
        Assert.Equal("two", Assert.IsType<Row>(cell.DataContext).Name);
        host.AssertNeverFocused(grid.Grid);
        Assert.Empty(announced);
    });

    /// <summary>A publication never lands into a scope that is off screen —
    /// a projection switched away with the reader's keys stranded on it:
    /// nothing there could take them, and a seat would only write the
    /// grid's currency, which its consumer follows as the selection.</summary>
    [Fact]
    public void APublicationIntoAHiddenGridSeatsNothing() => RunSta(() =>
    {
        var announced = new List<A11yEvent>();
        AccessibleDataGrid grid = BoundGrid([new Row("one"), new Row("two")], announced);
        using Hosted host = Host(grid);
        Assert.True(grid.SelectRow(row => ((Row)row).Name == "two", moveFocus: true));
        var seated = new List<object?>();
        grid.CurrentRowChanged += row => seated.Add(row);
        grid.Visibility = Visibility.Collapsed;
        PumpedDispatcher.Drain();
        seated.Clear();

        Rebind(grid, [new Row("three"), new Row("four")]);
        PumpedDispatcher.Drain();

        Assert.DoesNotContain(seated, row => row is not null);
        Assert.Empty(announced);
    });

    /// <summary>W7-7 PR 4b (the completeness sweep's G13): a publication
    /// that EMPTIES the grid under the reader's cell — a canvas table's last
    /// visible card deleted, a graph table filtered to nothing — lands the
    /// keys on the empty grid, its own stop (AR-6), once: the keeper never
    /// resolved an empty publication, and the keys stranded.</summary>
    [Fact]
    public void AGridEmptiedUnderTheReaderKeepsTheKeysOnTheGrid() => RunSta(() =>
    {
        var announced = new List<A11yEvent>();
        AccessibleDataGrid grid = BoundGrid([new Row("one"), new Row("two")], announced);
        using Hosted host = Host(grid);
        Assert.True(grid.SelectRow(row => ((Row)row).Name == "two", moveFocus: true));
        Assert.IsType<DataGridCell>(Keyboard.FocusedElement);
        host.RecordFocus();
        var stranded = new List<IInputElement?>();
        Keyboard.AddLostKeyboardFocusHandler(grid, (_, e) =>
        {
            if (e.NewFocus is null or Window
                || (e.NewFocus is System.Windows.Media.Visual visual && PresentationSource.FromVisual(visual) is null))
            {
                stranded.Add(e.NewFocus);
            }
        });
        announced.Clear();

        Rebind(grid, []);
        PumpedDispatcher.Drain();

        Assert.Same(grid.Grid, Keyboard.FocusedElement);
        Assert.Empty(stranded);
        Assert.Empty(announced);
    });

    /// <summary>An EMPTY grid stays its own stop through the same landing
    /// (AnEmptyGridKeepsItsArrows).</summary>
    [Fact]
    public void AnEmptyGridStopIsTheGridItself() => RunSta(() =>
    {
        AccessibleDataGrid grid = BoundGrid([]);
        using Hosted host = Host(grid);
        Assert.True(host.Above.Focus());

        Assert.True(SelectorFocus.LandOnStop(grid.Grid));

        Assert.Same(grid.Grid, Keyboard.FocusedElement);
    });

    /// <summary>A grid no AccessibleDataGrid owns has nothing keeping its
    /// arrows (the first fact here): it is not landed on, and the caller's
    /// own stable stop takes the keys.</summary>
    [Fact]
    public void AGridTheSubstrateDoesNotOwnIsNotLandedOn() => RunSta(() =>
    {
        var grid = new DataGrid { ItemsSource = new[] { new Row("one") }, AutoGenerateColumns = true };
        using Hosted host = Host(grid);
        Assert.True(host.Above.Focus());
        host.RecordFocus();

        Assert.False(SelectorFocus.LandOnStop(grid));

        Assert.Same(host.Above, Keyboard.FocusedElement);
        host.AssertNeverFocused(grid);
    });

    /// <summary>A bound substrate whose announcements are RECORDED, never
    /// swallowed (codex round 5: a discarding seam hid the restore's
    /// movement line).</summary>
    private static AccessibleDataGrid BoundGrid(IReadOnlyList<object> rows, List<A11yEvent>? announced = null)
    {
        List<A11yEvent> sink = announced ?? [];
        var grid = new AccessibleDataGrid { Announce = sink.Add };
        Rebind(grid, rows);
        return grid;
    }

    private static void Rebind(AccessibleDataGrid grid, IReadOnlyList<object> rows) =>
        grid.Bind(
            [new AccessibleGridColumn { Header = "Name", Cell = row => ((Row)row).Name, IsRowHeader = true }],
            rows,
            $"{rows.Count} rows.",
            "Rows");

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

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(120), "STA test body timed out.");
}
