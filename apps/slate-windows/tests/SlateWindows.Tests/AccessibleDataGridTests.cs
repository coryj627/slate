// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-1 substrate contract (the mac AccessibleDataGridTests twin,
/// Windows-idiomatic): canonical announcements from core (#969 grid
/// family), the sort seam, AT-safe virtualization pins, the
/// separately-focusable summary region, cell-label grammar, row
/// actions, type-ahead, and export sourcing.
/// </summary>
public sealed class AccessibleDataGridTests
{
    private sealed record Person(string Name, string Role);

    private static readonly IReadOnlyList<object> People = new object[]
    {
        new Person("Charlie", "Ops"),
        new Person("Alice", "Dev"),
        new Person("Bora", "Docs"),
    };

    private static IReadOnlyList<AccessibleGridColumn> Columns() => new[]
    {
        new AccessibleGridColumn
        {
            Header = "Name",
            Cell = row => ((Person)row).Name,
            Sort = Comparer<object>.Create(
                (x, y) => string.CompareOrdinal(((Person)x).Name, ((Person)y).Name)),
        },
        new AccessibleGridColumn
        {
            Header = "Role",
            Cell = row => ((Person)row).Role,
            AccessibilityHint = _ => "read-only: computed",
        },
    };

    private static AccessibleDataGrid MakeGrid(
        List<A11yEvent> announced,
        Func<object, string?>? rowAudioDescription = null,
        IReadOnlyList<AccessibleGridRowAction>? rowActions = null,
        Func<ExportFormat, string>? exportProducer = null)
    {
        var grid = new AccessibleDataGrid();
        grid.Announce = announced.Add;
        grid.Bind(
            Columns(),
            People,
            "3 rows, 2 columns.",
            "People, data grid",
            rowAudioDescription,
            rowActions,
            exportProducer);
        return grid;
    }

    [Fact]
    public void VirtualizationStaysAtSafe()
    {
        RunSta(() =>
        {
            var grid = MakeGrid(new List<A11yEvent>());
            // The documented UIA crash class is the RECYCLING default
            // (dotnet/wpf #8528): Standard mode is the substrate pin.
            Assert.Equal(
                VirtualizationMode.Standard,
                VirtualizingPanel.GetVirtualizationMode(grid.Grid));
            Assert.True(grid.Grid.EnableRowVirtualization);
            Assert.False(grid.Grid.EnableColumnVirtualization);
            Assert.True(ScrollViewer.GetIsDeferredScrollingEnabled(grid.Grid));
        });
    }

    [Fact]
    public void SortSeamPostsTheCanonicalEventAndReturnsItsText()
    {
        RunSta(() =>
        {
            var announced = new List<A11yEvent>();
            var grid = MakeGrid(announced);

            string? text = grid.ApplySort(0, ascending: true);
            Assert.Equal("Sorted by Name, ascending", text);
            var sorted = Assert.IsType<A11yEvent.GridSorted>(Assert.Single(announced));
            Assert.Equal("Name", sorted.Column);
            Assert.True(sorted.Ascending);

            // The rendered text IS core's render — no C# re-composition.
            Assert.Equal(
                SlateUniffiMethods.A11yRender(sorted).Text, text);

            Assert.Equal("Sorted by Name, descending", grid.ApplySort(0, ascending: false));
            Assert.Equal(2, announced.Count);
        });
    }

    [Fact]
    public void UnsortableColumnsRefuseTheSeam()
    {
        RunSta(() =>
        {
            var announced = new List<A11yEvent>();
            var grid = MakeGrid(announced);
            Assert.Null(grid.ApplySort(1, ascending: true));
            Assert.Null(grid.ApplySort(99, ascending: true));
            Assert.Empty(announced);
        });
    }

    [Fact]
    public void SortReordersRowsAndMarksTheHeaderDirection()
    {
        RunSta(() =>
        {
            var grid = MakeGrid(new List<A11yEvent>());
            grid.ApplySort(0, ascending: true);
            var first = Assert.IsType<Person>(grid.Grid.Items[0]);
            Assert.Equal("Alice", first.Name);
            Assert.Equal(
                System.ComponentModel.ListSortDirection.Ascending,
                grid.Grid.Columns[0].SortDirection);
            Assert.Null(grid.Grid.Columns[1].SortDirection);
        });
    }

    [Fact]
    public void RowMovesPostGridRowMovedWithTheCoreDedup()
    {
        RunSta(() =>
        {
            var announced = new List<A11yEvent>();
            var grid = MakeGrid(
                announced,
                rowAudioDescription: row => $"{((Person)row).Name}. Role: {((Person)row).Role}");

            grid.Grid.CurrentCell = new DataGridCellInfo(People[0], grid.Grid.Columns[0]);
            var rowMove = Assert.IsType<A11yEvent.GridRowMoved>(Assert.Single(announced));
            Assert.Equal("Charlie. Role: Ops", rowMove.Description);
            Assert.Equal("Name: Charlie", rowMove.FocusedCell);

            // Within-row move: the cell event.
            grid.Grid.CurrentCell = new DataGridCellInfo(People[0], grid.Grid.Columns[1]);
            var cellMove = Assert.IsType<A11yEvent.GridCellMoved>(announced[1]);
            Assert.Equal("Role", cellMove.Column);
            Assert.Equal("Ops", cellMove.Value);

            // Row change again: row event, dedup rendered by CORE -
            // the description already carries "Role: Dev", so it is
            // spoken alone (the mac dedup rule, now core-owned).
            grid.Grid.CurrentCell = new DataGridCellInfo(People[1], grid.Grid.Columns[1]);
            var second = Assert.IsType<A11yEvent.GridRowMoved>(announced[2]);
            Assert.Equal(
                "Alice. Role: Dev",
                SlateUniffiMethods.A11yRender(second).Text);
        });
    }

    [Fact]
    public void RowMoveWithoutDescriptionRendersTheFocusedCellAlone()
    {
        RunSta(() =>
        {
            var announced = new List<A11yEvent>();
            var grid = MakeGrid(announced);
            grid.Grid.CurrentCell = new DataGridCellInfo(People[2], grid.Grid.Columns[0]);
            var rowMove = Assert.IsType<A11yEvent.GridRowMoved>(Assert.Single(announced));
            Assert.Equal(string.Empty, rowMove.Description);
            Assert.Equal("Name: Bora", SlateUniffiMethods.A11yRender(rowMove).Text);
        });
    }

    [Fact]
    public void SummaryIsASeparatelyFocusableNamedRegion()
    {
        RunSta(() =>
        {
            var grid = MakeGrid(new List<A11yEvent>());
            Assert.True(grid.SummaryRegion.Focusable);
            Assert.Equal("3 rows, 2 columns.", grid.SummaryRegion.Text);
            Assert.Equal(
                "Summary: 3 rows, 2 columns.",
                AutomationProperties.GetName(grid.SummaryRegion));
        });
    }

    [Fact]
    public void CellLabelsCarryTheHeaderValueContractAndHints()
    {
        RunSta(() =>
        {
            var grid = MakeGrid(new List<A11yEvent>());
            var column = Assert.IsType<AccessibleGridTextColumn>(grid.Grid.Columns[1]);
            var cell = new DataGridCell();
            column.TestGenerateElement(cell, People[0]);
            Assert.Equal("Role: Ops", AutomationProperties.GetName(cell));
            Assert.Equal("read-only: computed", AutomationProperties.GetHelpText(cell));

            // The label string and the announced cell event render
            // IDENTICALLY — one grammar, two consumers.
            Assert.Equal(
                AutomationProperties.GetName(cell),
                SlateUniffiMethods.A11yRender(
                    new A11yEvent.GridCellMoved("Role", "Ops")).Text);
        });
    }

    [Fact]
    public void ExportComesFromTheInjectedProducerOnly()
    {
        RunSta(() =>
        {
            var produced = new List<(ExportFormat Format, string Text)>();
            var grid = MakeGrid(
                new List<A11yEvent>(),
                exportProducer: format => format == ExportFormat.Csv ? "a,b" : "|a|b|");
            grid.ExportProduced += (format, text) => produced.Add((format, text));

            AccessibleDataGrid.ExportCsvCommand.Execute(null, grid);
            AccessibleDataGrid.ExportMarkdownCommand.Execute(null, grid);
            Assert.Equal(2, produced.Count);
            Assert.Equal((ExportFormat.Csv, "a,b"), produced[0]);
            Assert.Equal((ExportFormat.Markdown, "|a|b|"), produced[1]);

            var bare = new AccessibleDataGrid();
            Assert.False(AccessibleDataGrid.ExportCsvCommand.CanExecute(null, bare));
        });
    }

    [Fact]
    public void TypeAheadSelectsByFirstColumnPrefix()
    {
        RunSta(() =>
        {
            var grid = MakeGrid(new List<A11yEvent>());
            Assert.True(grid.TypeAhead("al"));
            var current = Assert.IsType<Person>(grid.Grid.CurrentCell.Item);
            Assert.Equal("Alice", current.Name);
        });
    }

    [Fact]
    public void RowActionsMenuRetainsDisabledActionsWithTheirReason()
    {
        RunSta(() =>
        {
            var actions = new[]
            {
                new AccessibleGridRowAction
                {
                    Name = "Open",
                    Execute = _ => { },
                },
                new AccessibleGridRowAction
                {
                    Name = "Edit property",
                    Execute = _ => { },
                    IsEnabled = _ => false,
                    DisabledReason = "Read-only view",
                },
                new AccessibleGridRowAction
                {
                    Name = "Hidden",
                    Execute = _ => { },
                    IsVisible = _ => false,
                },
            };
            var grid = MakeGrid(new List<A11yEvent>(), rowActions: actions);
            grid.Grid.CurrentCell = new DataGridCellInfo(People[0], grid.Grid.Columns[0]);
            ContextMenu menu = grid.BuildRowActionsMenu()
                ?? throw new Xunit.Sdk.XunitException("no menu built");
            Assert.Equal(2, menu.Items.Count);
            var open = Assert.IsType<MenuItem>(menu.Items[0]);
            Assert.True(open.IsEnabled);
            var edit = Assert.IsType<MenuItem>(menu.Items[1]);
            Assert.False(edit.IsEnabled);
            Assert.Equal("Read-only view", AutomationProperties.GetHelpText(edit));
        });
    }

    [Fact]
    public void RowHeaderColumnDrivesVisibilityAndFirstCellFocusTargetsTheGrid()
    {
        RunSta(() =>
        {
            var grid = MakeGrid(new List<A11yEvent>());
            // No row-header column bound: column headers only.
            Assert.Equal(DataGridHeadersVisibility.Column, grid.Grid.HeadersVisibility);

            grid.Bind(
                new[]
                {
                    new AccessibleGridColumn
                    {
                        Header = "Name",
                        Cell = row => ((Person)row).Name,
                        IsRowHeader = true,
                    },
                },
                People,
                "3 rows, 1 column.",
                "People, data grid");
            Assert.Equal(DataGridHeadersVisibility.All, grid.Grid.HeadersVisibility);

            // Entry lands on the FIRST CELL (round 1: MoveFocus(First)
            // reached the summary), and the summary follows the grid
            // in tab order.
            _ = grid.FocusFirstCell();
            Assert.Equal(People[0], grid.Grid.CurrentCell.Item);
            Assert.Same(grid.Grid.Columns[0], grid.Grid.CurrentCell.Column);
            Assert.Equal(0, grid.Grid.TabIndex);
            Assert.Equal(1, KeyboardNavigation.GetTabIndex(grid.SummaryRegion));
        });
    }

    [Fact]
    public void FilterHookGatesCtrlFOnASubscriber()
    {
        RunSta(() =>
        {
            var grid = MakeGrid(new List<A11yEvent>());

            // No subscriber: the gesture must continue routing so the
            // app-level find is never shadowed by a grid that cannot
            // filter.
            Assert.False(AccessibleDataGrid.FilterCommand.CanExecute(null, grid.Grid));

            int requests = 0;
            grid.FilterRequested += () => requests++;
            Assert.True(AccessibleDataGrid.FilterCommand.CanExecute(null, grid.Grid));
            AccessibleDataGrid.FilterCommand.Execute(null, grid.Grid);
            Assert.Equal(1, requests);
        });
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
