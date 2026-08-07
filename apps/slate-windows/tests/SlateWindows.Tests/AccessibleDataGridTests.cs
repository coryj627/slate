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

    /// <summary>Reference equality, like every production row view
    /// model — a record would let value equality preserve currency and
    /// hide what a real re-publish does.</summary>
    private sealed class Widget(string id)
    {
        public string Id { get; } = id;
    }

    private static IReadOnlyList<AccessibleGridColumn> WidgetColumns() => new[]
    {
        new AccessibleGridColumn
        {
            Header = "Id",
            Cell = row => ((Widget)row).Id,
            IsRowHeader = true,
        },
        new AccessibleGridColumn
        {
            Header = "Note",
            Cell = row => $"note for {((Widget)row).Id}",
        },
    };

    private static IReadOnlyList<object> FreshWidgets() =>
        [new Widget("one"), new Widget("two"), new Widget("three")];

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
    /// A re-publish must not move the reader.
    ///
    /// ApplySort documents this hazard and restores currency: "the
    /// reader's position survives the sort: re-populating destroys the
    /// focused cell's container, and without a restore keyboard focus
    /// falls to the window". Bind destroys the same containers and had
    /// no restore — and consuming surfaces re-Bind on every publish, so
    /// a background save while the user is arrowing through the grid
    /// silently lost their row and their column.
    ///
    /// Restored by ROW-HEADER TEXT, not object identity: every publish
    /// builds fresh row view models, so identity is gone by definition.
    /// The row header is what §8.7 already treats as the row's
    /// identity.
    /// </summary>
    [Fact]
    public void ARepublishKeepsTheReaderOnTheirRowAndColumn()
    {
        RunSta(() =>
        {
            var grid = new AccessibleDataGrid { Announce = _ => { } };
            IReadOnlyList<object> first = FreshWidgets();
            grid.Bind(WidgetColumns(), first, "3 rows.", "Widgets");
            grid.Grid.CurrentCell = new DataGridCellInfo(first[1], grid.Grid.Columns[1]);

            // Fresh instances, same identities — a real re-publish.
            grid.Bind(WidgetColumns(), FreshWidgets(), "3 rows.", "Widgets");

            Assert.Equal("two", Assert.IsType<Widget>(grid.Grid.CurrentCell.Item).Id);
            Assert.Same(grid.Grid.Columns[1], grid.Grid.CurrentCell.Column);
        });
    }

    /// <summary>A row that is GONE after the republish must not be
    /// restored — the reader is not left pointing at a discarded
    /// object, and no other row is silently substituted.</summary>
    [Fact]
    public void ARepublishThatDropsTheCurrentRowRestoresNothing()
    {
        RunSta(() =>
        {
            var grid = new AccessibleDataGrid { Announce = _ => { } };
            IReadOnlyList<object> first = FreshWidgets();
            grid.Bind(WidgetColumns(), first, "3 rows.", "Widgets");
            grid.Grid.CurrentCell = new DataGridCellInfo(first[2], grid.Grid.Columns[0]);

            grid.Bind(
                WidgetColumns(), [new Widget("one")], "1 row.", "Widgets");

            Assert.DoesNotContain(
                grid.Grid.Items.Cast<object>(),
                row => ((Widget)row).Id == "three");
        });
    }

    /// <summary>Counts equality probes, so a quadratic restore scan is
    /// observable. Production row view models are classes, and
    /// `_items.Contains` calls Equals on each.</summary>
    private sealed class CountingRow(string id)
    {
        internal static int EqualsCalls;

        public string Id { get; } = id;

        public override bool Equals(object? obj)
        {
            EqualsCalls++;
            return ReferenceEquals(this, obj);
        }

        public override int GetHashCode() => Id.GetHashCode(StringComparison.Ordinal);
    }

    private static IReadOnlyList<AccessibleGridColumn> CountingColumns() => new[]
    {
        new AccessibleGridColumn
        {
            Header = "Id",
            Cell = row => ((CountingRow)row).Id,
            IsRowHeader = true,
        },
    };

    /// <summary>
    /// A re-publish must not ANNOUNCE a row move the user did not make.
    ///
    /// Bind nulls _lastAnnouncedRow, then the reader-position restore
    /// assigns CurrentCell — so OnCurrentCellChanged sees a move from
    /// "nothing" and posts GridRowMoved. ApplySort defuses exactly this
    /// Every consumer that re-binds inherited a spurious announcement,
    /// and for bulk-rename it lands AFTER the rename summary the user
    /// actually asked for.
    ///
    /// Seeding _lastAnnouncedRow the way ApplySort does is NOT the fix:
    /// it only flips OnCurrentCellChanged's branch, trading a spurious
    /// GridRowMoved for a spurious GridCellMoved. The restore is not a
    /// user action, so it must not speak at all — hence the assertion
    /// is on the event count, not on the event kind.
    /// </summary>
    [Fact]
    public void ARepublishRestoresPositionWithoutAnnouncingAMove()
    {
        RunSta(() =>
        {
            var announced = new List<A11yEvent>();
            var grid = new AccessibleDataGrid { Announce = announced.Add };
            IReadOnlyList<object> rows = FreshWidgets();
            grid.Bind(WidgetColumns(), rows, "3 rows.", "Widgets");
            grid.Grid.CurrentCell = new DataGridCellInfo(rows[1], grid.Grid.Columns[1]);
            announced.Clear();

            grid.Bind(WidgetColumns(), FreshWidgets(), "3 rows.", "Widgets");

            Assert.Equal("two", Assert.IsType<Widget>(grid.Grid.CurrentCell.Item).Id);
            Assert.Empty(announced);
        });
    }

    /// <summary>
    /// The restore scan must be LINEAR in the row count.
    ///
    /// RowIdentityOf guards with _items.Contains — necessary at the
    /// capture call, where the item may be foreign or the new-item
    /// placeholder, but pure waste inside a loop that is already
    /// walking _items. That made every re-publish O(n²): measured at
    /// 708 ms for 8,000 rows against 33 ms before this branch, on the
    /// UI thread, with the bulk-rename preview uncapped.
    /// </summary>
    [Fact]
    public void ARepublishScansLinearlyNotQuadratically()
    {
        RunSta(() =>
        {
            const int count = 60;
            var grid = new AccessibleDataGrid { Announce = _ => { } };
            object[] First() =>
                [.. Enumerable.Range(0, count).Select(i => (object)new CountingRow($"r{i}"))];

            IReadOnlyList<object> rows = First();
            grid.Bind(CountingColumns(), rows, "rows.", "Rows");
            // Worst case: the reader is on the LAST row, so the restore
            // scan runs to the end.
            grid.Grid.CurrentCell =
                new DataGridCellInfo(rows[count - 1], grid.Grid.Columns[0]);

            CountingRow.EqualsCalls = 0;
            grid.Bind(CountingColumns(), First(), "rows.", "Rows");

            Assert.Equal($"r{count - 1}", Assert.IsType<CountingRow>(grid.Grid.CurrentCell.Item).Id);
            // Quadratic would be ~count²/2 ≈ 1,800 here.
            Assert.True(
                CountingRow.EqualsCalls < count * 4,
                $"restore probed equality {CountingRow.EqualsCalls} times for {count} rows");
        });
    }

    /// <summary>Enter on a bound row activates it — the affordance the
    /// bibliography rows advertise as "Activate to expand citation
    /// fields." Row activation shipped with no test at all.</summary>
    [Fact]
    public void EnterActivatesTheCurrentRow()
    {
        RunSta(() =>
        {
            object? activated = null;
            var grid = new AccessibleDataGrid { Announce = _ => { } };
            grid.Bind(
                Columns(), People, "3 rows.", "People",
                rowActivated: row => activated = row);
            grid.Grid.CurrentCell = new DataGridCellInfo(People[1], grid.Grid.Columns[0]);

            grid.Grid.RaiseEvent(new KeyEventArgs(
                Keyboard.PrimaryDevice,
                System.Windows.PresentationSource.FromVisual(grid.Grid)
                    ?? new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "t", IntPtr.Zero),
                0,
                Key.Enter)
            { RoutedEvent = System.Windows.UIElement.PreviewKeyDownEvent });

            Assert.Same(People[1], activated);
        });
    }

    /// <summary>
    /// A double-click that did not land on a row must not activate one.
    ///
    /// The handler read CurrentCell.Item without hit-testing what was
    /// actually clicked, and MouseDoubleClick fires for the whole
    /// control — so double-clicking a column HEADER to sort, the
    /// ordinary mouse idiom, also opened the details sheet for whatever
    /// row happened to be current, trapping focus in a dialog the user
    /// never asked for. The context menu next door already hit-tests
    /// through TargetRowActionsAt for exactly this reason.
    /// </summary>
    [Fact]
    public void ADoubleClickOffAnyRowActivatesNothing()
    {
        RunSta(() =>
        {
            object? activated = null;
            var grid = new AccessibleDataGrid { Announce = _ => { } };
            grid.Bind(
                Columns(), People, "3 rows.", "People",
                rowActivated: row => activated = row);
            grid.Grid.CurrentCell = new DataGridCellInfo(People[0], grid.Grid.Columns[0]);

            // Source is the grid itself: no cell, no row — a header or
            // the empty chrome below the last row.
            grid.Grid.RaiseEvent(new MouseButtonEventArgs(
                Mouse.PrimaryDevice, 0, MouseButton.Left)
            { RoutedEvent = Control.MouseDoubleClickEvent });

            Assert.Null(activated);
        });
    }

    /// <summary>
    /// A re-publish must not silently undo the user's sort.
    ///
    /// Bind is a whole-surface reset, and consuming surfaces re-bind
    /// whenever their rows change — for the citations bibliography that
    /// is every keystroke in its filter box. Dropping the sort there
    /// reordered rows under the reader with no announcement and no
    /// header indicator, so the ordering they had chosen just
    /// evaporated. Re-applied silently: announcing on a background
    /// re-publish would be a second lie in the other direction.
    /// </summary>
    [Fact]
    public void SortSurvivesARebindAndIsNotReannounced()
    {
        RunSta(() =>
        {
            var announced = new List<A11yEvent>();
            var grid = MakeGrid(announced);
            _ = grid.ApplySort(0, ascending: true);
            Assert.Equal("Alice", Assert.IsType<Person>(grid.Grid.Items[0]).Name);
            int afterUserSort = announced.Count;

            grid.Bind(Columns(), People, "3 rows, 2 columns.", "People, data grid");

            Assert.Equal("Alice", Assert.IsType<Person>(grid.Grid.Items[0]).Name);
            Assert.Equal((0, true), grid.ActiveSort);
            Assert.Equal(
                System.ComponentModel.ListSortDirection.Ascending,
                grid.Grid.Columns[0].SortDirection);
            Assert.Equal(afterUserSort, announced.Count);
        });
    }

    /// <summary>A rebind whose columns can no longer support the old
    /// sort must drop it rather than throw or half-apply it.</summary>
    [Fact]
    public void ARebindWhoseColumnsCannotSortDropsTheSort()
    {
        RunSta(() =>
        {
            var grid = MakeGrid(new List<A11yEvent>());
            _ = grid.ApplySort(0, ascending: true);
            Assert.Equal((0, true), grid.ActiveSort);

            // Same position, but this column carries no comparator.
            grid.Bind(
                [
                    new AccessibleGridColumn
                    {
                        Header = "Name",
                        Cell = row => ((Person)row).Name,
                    },
                ],
                People,
                "3 rows, 1 column.",
                "People, data grid");

            Assert.Null(grid.ActiveSort);
            Assert.Equal("Charlie", Assert.IsType<Person>(grid.Grid.Items[0]).Name);
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
