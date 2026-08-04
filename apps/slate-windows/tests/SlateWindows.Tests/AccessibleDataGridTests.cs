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

    /// <summary>
    /// Two grids in one window must be tellable apart. The default is
    /// unchanged so W4-1's conformance fixture and the bulk-rename
    /// preview keep the ids they already publish.
    /// </summary>
    [Fact]
    public void GridAutomationIdDefaultsAndRenamesTheSummaryWithIt()
    {
        RunSta(() =>
        {
            var grid = MakeGrid(new List<A11yEvent>());
            Assert.Equal("AccessibleDataGrid", grid.GridAutomationId);
            Assert.Equal(
                "AccessibleDataGridSummary",
                AutomationProperties.GetAutomationId(grid.SummaryRegion));

            grid.GridAutomationId = "BibliographyEntries";
            Assert.Equal(
                "BibliographyEntries",
                AutomationProperties.GetAutomationId(grid.Grid));
            Assert.Equal(
                "BibliographyEntriesSummary",
                AutomationProperties.GetAutomationId(grid.SummaryRegion));
        });
    }

    /// <summary>
    /// W4-5 (#737) needs to land Ctrl+J on a named bibliography row.
    /// A HIT moves currency to that row's first cell; a MISS moves
    /// nothing at all — landing on row one after a failed jump would
    /// tell a screen-reader user they had arrived somewhere they had
    /// not.
    /// </summary>
    [Fact]
    public void FocusRowMovesCurrencyOnAHitAndLeavesItAloneOnAMiss()
    {
        RunSta(() =>
        {
            var grid = MakeGrid(new List<A11yEvent>());
            grid.Grid.CurrentCell = new DataGridCellInfo(People[0], grid.Grid.Columns[0]);

            Assert.True(grid.FocusRow(row => ((Person)row).Name == "Bora"));
            Assert.Equal("Bora", Assert.IsType<Person>(grid.Grid.CurrentCell.Item).Name);
            Assert.Equal(grid.Grid.Columns[0], grid.Grid.CurrentCell.Column);

            Assert.False(grid.FocusRow(row => ((Person)row).Name == "Nobody"));
            Assert.Equal("Bora", Assert.IsType<Person>(grid.Grid.CurrentCell.Item).Name);
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
            object? executed = null;
            var actions = new[]
            {
                new AccessibleGridRowAction
                {
                    Name = "Open",
                    Execute = row => executed = row,
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

            // Click → Execute wiring, not just composition (round 4):
            // the invoked item must run its action against the row it
            // was built for.
            open.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Same(People[0], executed);
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
    public void PointerInvokedMenuTargetsTheClickedRowNotTheCurrentOne()
    {
        RunSta(() =>
        {
            object? executed = null;
            var grid = MakeGrid(
                new List<A11yEvent>(),
                rowActions: new[]
                {
                    new AccessibleGridRowAction
                    {
                        Name = "Open",
                        Execute = row => executed = row,
                    },
                });
            // Realize containers so row B's cell element exists.
            grid.Measure(new System.Windows.Size(800, 600));
            grid.Arrange(new System.Windows.Rect(0, 0, 800, 600));
            grid.UpdateLayout();

            // Currency on row A; the pointer opens the menu over row B
            // (round 5: WPF moves currency only on the left-button
            // path — without targeting, the action executes against A,
            // destructively for real actions).
            grid.Grid.CurrentCell = new DataGridCellInfo(People[0], grid.Grid.Columns[0]);
            var rowB = (DataGridRow)grid.Grid.ItemContainerGenerator
                .ContainerFromItem(People[1]);
            var cellB = Assert.IsType<DataGridCell>(
                grid.Grid.Columns[0].GetCellContent(rowB)?.Parent);

            Assert.True(grid.TargetRowActionsAt(cellB));
            ContextMenu menu = grid.BuildRowActionsMenu()
                ?? throw new Xunit.Sdk.XunitException("no menu built");
            var open = Assert.IsType<MenuItem>(menu.Items[0]);
            open.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Same(People[1], executed);

            // A ROW-HEADER origin walks up through DataGridRow, never
            // a cell (round 6) — it must target that row too.
            grid.Grid.CurrentCell = new DataGridCellInfo(People[0], grid.Grid.Columns[0]);
            var rowC = (DataGridRow)grid.Grid.ItemContainerGenerator
                .ContainerFromItem(People[2]);
            Assert.True(grid.TargetRowActionsAt(rowC));
            menu = grid.BuildRowActionsMenu()
                ?? throw new Xunit.Sdk.XunitException("no menu built for row origin");
            var openRow = Assert.IsType<MenuItem>(menu.Items[0]);
            openRow.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Same(People[2], executed);

            // Column headers, scrollbars, empty chrome: no row to act
            // on — the pointer menu is refused.
            Assert.False(grid.TargetRowActionsAt(grid.Grid));
        });
    }

    [Fact]
    public void GroupHeadingComposesThroughTheCoreEventFamily()
    {
        // The corpus goldens, byte-for-byte — grouped consumers (W4-6)
        // label group rows with THIS text, the same render the mac
        // twin now labels with.
        Assert.Equal(
            "Group: Open, 1 row",
            AccessibleDataGrid.ComposeGroupHeading("Open", 1, null));
        Assert.Equal(
            "Group: Done, 12 rows. Summary: Count: 12",
            AccessibleDataGrid.ComposeGroupHeading("Done", 12, "Count: 12"));
        Assert.Equal(
            SlateUniffiMethods.A11yRender(
                new A11yEvent.GridGroup("Team A", 2, null)).Text,
            AccessibleDataGrid.ComposeGroupHeading("Team A", 2, null));
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
