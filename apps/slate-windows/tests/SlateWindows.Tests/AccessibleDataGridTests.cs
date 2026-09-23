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

    /// <summary>The row identity every bind now requires (codex PR 3
    /// round 2): a person's name, a widget's id, else the first
    /// non-empty cell the grid falls back to.</summary>
    private static string? Identity(object row) => row switch
    {
        Person person => person.Name,
        Widget widget => widget.Id,
        _ => null,
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
            rowAutomationName: Identity,
            rowAudioDescription: rowAudioDescription,
            rowActions: rowActions,
            exportProducer: exportProducer);
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
            grid.Bind(WidgetColumns(), first, "3 rows.", "Widgets", rowAutomationName: Identity);
            grid.Grid.CurrentCell = new DataGridCellInfo(first[1], grid.Grid.Columns[1]);

            // Fresh instances, same identities — a real re-publish.
            grid.Bind(WidgetColumns(), FreshWidgets(), "3 rows.", "Widgets", rowAutomationName: Identity);

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
            grid.Bind(WidgetColumns(), first, "3 rows.", "Widgets", rowAutomationName: Identity);
            grid.Grid.CurrentCell = new DataGridCellInfo(first[2], grid.Grid.Columns[0]);

            grid.Bind(
                WidgetColumns(), [new Widget("one")], "1 row.", "Widgets", rowAutomationName: Identity);

            Assert.DoesNotContain(
                grid.Grid.Items.Cast<object>(),
                row => ((Widget)row).Id == "three");
            Assert.False(grid.Grid.CurrentCell.IsValid);
            Assert.Empty(grid.Grid.SelectedCells);
            Assert.True(grid.SelectRow(row => ((Widget)row).Id == "one"));
            Assert.Same(grid.Grid.Items[0], grid.Grid.CurrentCell.Item);
            Assert.Same(grid.Grid.Columns[0], grid.Grid.CurrentCell.Column);
        });
    }

    [Fact]
    public void ARepublishThatDropsTheCurrentColumnClearsCurrencyAndCanSelectAgain()
    {
        RunSta(() =>
        {
            var announced = new List<A11yEvent>();
            var grid = new AccessibleDataGrid { Announce = announced.Add };
            IReadOnlyList<object> rows = FreshWidgets();
            grid.Bind(WidgetColumns(), rows, "3 rows.", "Widgets", rowAutomationName: Identity);
            grid.Grid.CurrentCell = new DataGridCellInfo(rows[1], grid.Grid.Columns[1]);
            announced.Clear();

            grid.Bind([WidgetColumns()[0]], FreshWidgets(), "3 rows.", "Widgets", rowAutomationName: Identity);

            Assert.False(grid.Grid.CurrentCell.IsValid);
            Assert.Empty(grid.Grid.SelectedCells);
            Assert.True(grid.SelectRow(row => ((Widget)row).Id == "two"));
            Assert.Same(grid.Grid.Columns[0], grid.Grid.CurrentCell.Column);
            Assert.Equal("two", Assert.IsType<Widget>(grid.Grid.CurrentCell.Item).Id);
            Assert.Empty(announced);
        });
    }

    [Fact]
    public void AnEmptyRepublishDoesNotLeaveAStaleCellForTheNextBinding()
    {
        RunSta(() =>
        {
            var grid = new AccessibleDataGrid { Announce = _ => { } };
            IReadOnlyList<object> rows = FreshWidgets();
            grid.Bind(WidgetColumns(), rows, "3 rows.", "Widgets", rowAutomationName: Identity);
            grid.Grid.CurrentCell = new DataGridCellInfo(rows[2], grid.Grid.Columns[1]);

            grid.Bind([], [], "No rows.", "Widgets", rowAutomationName: Identity);
            Assert.False(grid.Grid.CurrentCell.IsValid);
            Assert.Empty(grid.Grid.SelectedCells);

            grid.Bind(WidgetColumns(), FreshWidgets(), "3 rows.", "Widgets", rowAutomationName: Identity);
            Assert.True(grid.SelectRow(_ => true));
            Assert.Same(grid.Grid.Columns[0], grid.Grid.CurrentCell.Column);
            Assert.Equal("one", Assert.IsType<Widget>(grid.Grid.CurrentCell.Item).Id);
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
            grid.Bind(WidgetColumns(), rows, "3 rows.", "Widgets", rowAutomationName: Identity);
            grid.Grid.CurrentCell = new DataGridCellInfo(rows[1], grid.Grid.Columns[1]);
            announced.Clear();

            grid.Bind(WidgetColumns(), FreshWidgets(), "3 rows.", "Widgets", rowAutomationName: Identity);

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
            grid.Bind(CountingColumns(), rows, "rows.", "Rows", rowAutomationName: Identity);
            // Worst case: the reader is on the LAST row, so the restore
            // scan runs to the end.
            grid.Grid.CurrentCell =
                new DataGridCellInfo(rows[count - 1], grid.Grid.Columns[0]);

            CountingRow.EqualsCalls = 0;
            grid.Bind(CountingColumns(), First(), "rows.", "Rows", rowAutomationName: Identity);

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
                rowActivated: row => activated = row, rowAutomationName: Identity);
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
                rowActivated: row => activated = row, rowAutomationName: Identity);
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

            grid.Bind(Columns(), People, "3 rows, 2 columns.", "People, data grid", rowAutomationName: Identity);

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
                "People, data grid", rowAutomationName: Identity);

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
                "People, data grid", rowAutomationName: Identity);
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

    /// <summary>
    /// Row activation listens on the GRID's PreviewKeyDown, and the row
    /// actions menu is logically parented to that same grid — so if a
    /// key press inside the open menu tunnelled through it, Enter on
    /// "Open" would run the menu action AND activate the row, opening
    /// two things for one keystroke.
    ///
    /// It does not: the popup builds its own route. This pins that,
    /// because the day it stops being true the failure is silent and
    /// destructive for any consumer whose row actions are not merely
    /// navigational.
    /// </summary>
    [Fact]
    public void EnterInsideTheRowActionsMenuDoesNotAlsoActivateTheRow()
    {
        RunSta(() =>
        {
            object? activated = null;
            object? executed = null;
            var actions = new[]
            {
                new AccessibleGridRowAction { Name = "Open", Execute = row => executed = row },
            };
            var grid = new AccessibleDataGrid { Announce = _ => { } };
            grid.Bind(
                Columns(), People, "3 rows.", "People",
                rowActions: actions, rowActivated: row => activated = row, rowAutomationName: Identity);
            var window = new System.Windows.Window
            {
                Content = grid,
                Width = 400,
                Height = 300,
                ShowInTaskbar = false,
                WindowStyle = System.Windows.WindowStyle.None,
            };
            window.Show();
            try
            {
                grid.Grid.CurrentCell = new DataGridCellInfo(People[1], grid.Grid.Columns[0]);
                ContextMenu menu = grid.Grid.ContextMenu!;
                ContextMenu built = grid.BuildRowActionsMenu()!;
                menu.Items.Clear();
                while (built.Items.Count > 0)
                {
                    object item = built.Items[0];
                    built.Items.RemoveAt(0);
                    _ = menu.Items.Add(item);
                }
                int sawKeyAtGrid = 0;
                // handledEventsToo: the constructor's own handler runs
                // first on this element and marks Enter handled, which
                // would skip a plain += counter and make a zero mean
                // nothing.
                grid.Grid.AddHandler(
                    System.Windows.UIElement.PreviewKeyDownEvent,
                    new KeyEventHandler((_, _) => sawKeyAtGrid++),
                    handledEventsToo: true);
                KeyEventArgs Enter() => new(
                    Keyboard.PrimaryDevice,
                    System.Windows.PresentationSource.FromVisual(grid.Grid)!,
                    0,
                    Key.Enter)
                { RoutedEvent = System.Windows.UIElement.PreviewKeyDownEvent };

                // Control FIRST, with the menu closed: this is the path
                // that must work, and it proves the counter and the
                // synthesized event are wired before anything is
                // concluded from a zero.
                grid.Grid.RaiseEvent(Enter());
                Assert.Equal(1, sawKeyAtGrid);
                Assert.Same(People[1], activated);
                activated = null;

                menu.PlacementTarget = grid.Grid;
                menu.IsOpen = true;
                Assert.True(menu.IsOpen, "the menu never opened — the probe would be vacuous");
                var open = (MenuItem)menu.Items[0];
                _ = open.Focus();

                open.RaiseEvent(Enter());
                Assert.Equal(1, sawKeyAtGrid);
                Assert.Null(activated);
                Assert.Null(executed);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// The sort is re-applied by COLUMN IDENTITY, not by position.
    ///
    /// Bind captured the sort as a bare index and re-applied it to
    /// whatever column now sits at that index. Every consumer today
    /// re-binds the same column list, so the index happens to agree —
    /// but W4-6 swaps column sets per view into one grid instance, and
    /// there the reader's "sort by Role" silently becomes "sort by
    /// Name": right indicator, wrong column, no announcement.
    /// </summary>
    [Fact]
    public void ARebindThatReordersColumnsKeepsTheSortOnTheSameColumn()
    {
        RunSta(() =>
        {
            IReadOnlyList<object> people =
            [
                new Person("Charlie", "Alpha"),
                new Person("Alice", "Zulu"),
                new Person("Bora", "Mid"),
            ];
            var name = new AccessibleGridColumn
            {
                Header = "Name",
                Cell = row => ((Person)row).Name,
                Sort = Comparer<object>.Create(
                    (x, y) => string.CompareOrdinal(((Person)x).Name, ((Person)y).Name)),
            };
            var role = new AccessibleGridColumn
            {
                Header = "Role",
                Cell = row => ((Person)row).Role,
                Sort = Comparer<object>.Create(
                    (x, y) => string.CompareOrdinal(((Person)x).Role, ((Person)y).Role)),
            };
            var grid = new AccessibleDataGrid { Announce = _ => { } };
            grid.Bind([name, role], people, "3 rows.", "People", rowAutomationName: Identity);
            _ = grid.ApplySort(1, ascending: true);
            Assert.Equal("Charlie", Assert.IsType<Person>(grid.Grid.Items[0]).Name);

            // Same grid instance, columns swapped.
            grid.Bind([role, name], people, "3 rows.", "People", rowAutomationName: Identity);

            Assert.Equal((0, true), grid.ActiveSort);
            Assert.Equal("Charlie", Assert.IsType<Person>(grid.Grid.Items[0]).Name);
            Assert.Equal(
                System.ComponentModel.ListSortDirection.Ascending,
                grid.Grid.Columns[0].SortDirection);
            Assert.Null(grid.Grid.Columns[1].SortDirection);
        });
    }

    /// <summary>
    /// Two rows can share row-header text, and the restore must not
    /// quietly pick the first one.
    ///
    /// The reader's position is keyed on that text because a re-publish
    /// builds fresh row view models and object identity is gone by
    /// definition. Where the key is ambiguous the previous ORDINAL
    /// breaks the tie, so someone reading the second "Untitled" stays
    /// on the second one instead of being moved to the first with
    /// nothing announced. Citation keys are unique; base views and
    /// task titles are not.
    /// </summary>
    [Fact]
    public void ARepublishWithDuplicateRowHeadersRestoresTheSameOccurrence()
    {
        RunSta(() =>
        {
            IReadOnlyList<object> first =
                [new Widget("dup"), new Widget("dup"), new Widget("tail")];
            var grid = new AccessibleDataGrid { Announce = _ => { } };
            grid.Bind(WidgetColumns(), first, "3 rows.", "Widgets", rowAutomationName: Identity);
            grid.Grid.CurrentCell = new DataGridCellInfo(first[1], grid.Grid.Columns[1]);
            Assert.Same(first[1], grid.Grid.CurrentCell.Item);

            IReadOnlyList<object> second =
                [new Widget("dup"), new Widget("dup"), new Widget("tail")];
            grid.Bind(WidgetColumns(), second, "3 rows.", "Widgets", rowAutomationName: Identity);

            Assert.Same(second[1], grid.Grid.CurrentCell.Item);
        });
    }

    /// <summary>W6-2 PR A (contract A-6, AD-2): the row-name and
    /// item-status seams are applied per REALIZED row and cleared when
    /// the container unloads — Standard virtualization creates and
    /// discards containers, so a newly realized row carries its own
    /// values and an unloaded one carries none.</summary>
    [Fact]
    public void RowNameAndItemStatusSeamsFollowRealizedRows()
    {
        RunSta(() =>
        {
            var grid = new AccessibleDataGrid { Announce = _ => { } };
            grid.Bind(
                Columns(), People, "3 rows.", "People",
                rowAutomationName: row => $"row {((Person)row).Name}",
                rowItemStatus: row => ((Person)row).Role);
            var window = new System.Windows.Window
            {
                Content = grid,
                Width = 400,
                Height = 300,
                ShowInTaskbar = false,
                WindowStyle = System.Windows.WindowStyle.None,
            };
            window.Show();
            try
            {
                grid.Grid.UpdateLayout();
                var realized = (DataGridRow?)grid.Grid.ItemContainerGenerator.ContainerFromItem(People[1]);
                Assert.NotNull(realized);
                Assert.Equal("row Alice", AutomationProperties.GetName(realized));
                Assert.Equal("Dev", AutomationProperties.GetItemStatus(realized));

                // A re-bind with different rows discards the old
                // containers: the one we held unloads and drops its values;
                // the new rows' containers carry theirs.
                grid.Bind(
                    Columns(), new object[] { new Person("Dana", "QA") }, "1 row.", "People",
                    rowAutomationName: row => $"row {((Person)row).Name}",
                    rowItemStatus: row => ((Person)row).Role);
                grid.Grid.UpdateLayout();
                Assert.Equal(string.Empty, AutomationProperties.GetName(realized));
                Assert.Equal(string.Empty, AutomationProperties.GetItemStatus(realized));
                var fresh = (DataGridRow?)grid.Grid.ItemContainerGenerator.ContainerFromIndex(0);
                Assert.NotNull(fresh);
                Assert.Equal("row Dana", AutomationProperties.GetName(fresh));
                Assert.Equal("QA", AutomationProperties.GetItemStatus(fresh));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Codex PR 3 round 2: a teardown takes the rows' names with
    /// the rows. A bind of no rows swapped the naming delegate out BEFORE
    /// the rows unloaded, so OnUnloadingRow — which clears only what a bound
    /// delegate set — left every realized row its name and status, and a
    /// peer a client still held read the torn-down row.</summary>
    [Fact]
    public void ATeardownLeavesNoRealizedRowItsName() => RunSta(() =>
    {
        var grid = new AccessibleDataGrid { Announce = _ => { } };
        grid.Bind(
            Columns(), People, "3 rows.", "People",
            rowAutomationName: row => ((Person)row).Name,
            rowItemStatus: row => ((Person)row).Role);
        GridRowNames.Hosted(grid, () =>
        {
            DataGridRow[] realized = [.. People.Select(person =>
                Assert.IsType<DataGridRow>(grid.Grid.ItemContainerGenerator.ContainerFromItem(person)))];
            Assert.All(realized, row => Assert.NotEqual(string.Empty, AutomationProperties.GetName(row)));

            grid.Clear();
            grid.UpdateLayout();

            Assert.All(realized, row =>
            {
                Assert.Equal(string.Empty, AutomationProperties.GetName(row));
                Assert.Equal(string.Empty, AutomationProperties.GetItemStatus(row));
            });
        });
    });

    /// <summary>W6-2 PR A (contract A-9): the modified activation seam.
    /// Ctrl+Enter and Ctrl+double-click reach the modified handler when
    /// the surface bound one; a surface that bound none keeps the plain
    /// handler for both gestures, as before.</summary>
    [Fact]
    public void ModifiedActivationReachesItsHandlerAndFallsBackToPlain()
    {
        RunSta(() =>
        {
            object? plain = null;
            object? modified = null;
            var grid = new AccessibleDataGrid { Announce = _ => { } };
            grid.Bind(
                Columns(), People, "3 rows.", "People",
                rowActivated: row => plain = row,
                rowActivatedModified: row => modified = row, rowAutomationName: Identity);
            grid.Grid.CurrentCell = new DataGridCellInfo(People[2], grid.Grid.Columns[0]);

            Assert.True(grid.ActivateCurrentRow(modified: true));
            Assert.Same(People[2], modified);
            Assert.Null(plain);

            Assert.True(grid.ActivateCurrentRow(modified: false));
            Assert.Same(People[2], plain);

            // Without a modified handler the modifier is ignored.
            plain = null;
            modified = null;
            grid.Bind(Columns(), People, "3 rows.", "People", rowActivated: row => plain = row, rowAutomationName: Identity);
            grid.Grid.CurrentCell = new DataGridCellInfo(People[0], grid.Grid.Columns[0]);
            Assert.True(grid.ActivateCurrentRow(modified: true));
            Assert.Same(People[0], plain);
            Assert.Null(modified);

            // The keyboard arm: a plain Enter still reaches the plain
            // handler through the routed event.
            plain = null;
            grid.Grid.RaiseEvent(new KeyEventArgs(
                Keyboard.PrimaryDevice,
                System.Windows.PresentationSource.FromVisual(grid.Grid)
                    ?? new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "t", IntPtr.Zero),
                0,
                Key.Enter)
            { RoutedEvent = System.Windows.UIElement.PreviewKeyDownEvent });
            Assert.Same(People[0], plain);
        });
    }

    // W7-7 PR 3 (#1246, contract R-4): every caller names its rows by
    // identity. Each fact below drives the caller's own bind and reads
    // the realized rows' UIA NAMES — the DataItem a reader lands on, so
    // WPF's ToString() fallback is what a missing name would show (the
    // record: "SlateWindows.Bases.BaseGridRowViewModel, data item").

    /// <summary>R-4 (amended after codex PR 0 round 5): a row is never
    /// left unnamed. A delegate that answers blank would let WPF read the
    /// item's ToString() (a blank Name is no Name), so the first non-empty
    /// cell stands in, else "Row {n}" — n the row's place in the rows the
    /// caller bound, which a sort does not move (the view position does).</summary>
    [Fact]
    public void ABlankRowNameFallsBackToTheFirstNonEmptyCellThenAStableOrdinal() => RunSta(() =>
    {
        object[] rows =
        [
            new Person("Charlie", "Ops"),
            new Person(string.Empty, "Dev"),
            new Person(string.Empty, " "),
            new Person("Bora", "Docs"),
        ];
        var grid = new AccessibleDataGrid { Announce = _ => { } };
        grid.Bind(
            Columns(), rows, "4 rows.", "People",
            rowAutomationName: row => ((Person)row).Name == "Charlie" ? "Charlie" : " ");
        var window = new System.Windows.Window
        {
            Content = grid,
            Width = 640,
            Height = 480,
            ShowInTaskbar = false,
            WindowStyle = System.Windows.WindowStyle.None,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Equal(
                ["Charlie", "Dev", "Row 3", "Bora"],
                GridRowNames.Read(grid).Select(row => row.Name));

            // Ascending by Name puts the two blank names first: the blank
            // row now SITS second, and is still the third row it was bound as.
            Assert.NotNull(grid.ApplySort(0, ascending: true));
            window.UpdateLayout();
            Assert.Equal(
                ["Dev", "Row 3", "Bora", "Charlie"],
                GridRowNames.Read(grid).Select(row => row.Name));
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>The reading table names a row by its first non-empty
    /// cell — the first cell is the row header, but a markdown row may
    /// leave it blank, run short, or hold nothing at all, and
    /// <c>CellText</c> reads those as "" by design. A row with no text
    /// reads its place in the parsed table, so two such rows read
    /// differently, and a sort — which re-populates the grid — leaves every
    /// name on the source row it was given to (codex PR 0 round 6: a
    /// non-empty check alone accepts a constant or a display index).
    /// Unnamed, a row of cells read "System.String[]".</summary>
    [Fact]
    public void ReadingTableRowsAreNamedByTheirFirstNonEmptyCell() => RunSta(() =>
    {
        AccessibleDataGrid grid = Assert.IsType<AccessibleDataGrid>(Reading.ReadingTableGrid.Build(
            "| Name | Status |\n"
            + "| --- | --- |\n"
            + "| alpha | Open |\n"
            + "|  |  |\n"
            + "|  | Done |\n"
            + "|  |\n"
            + "| gamma |\n"));
        GridRowNames.Hosted(grid, () =>
        {
            // Source order: a full row, a wholly empty row, an empty first
            // cell, a ragged empty row, a ragged row.
            List<(object Item, string Name)> source = GridRowNames.Read(grid);
            Assert.Equal(["alpha", "Row 2", "Done", "Row 4", "gamma"], source.Select(row => row.Name));

            // Ascending by Name brings the three blank first cells to the
            // top; every row keeps the name it had.
            Assert.NotNull(grid.ApplySort(0, ascending: true));
            grid.UpdateLayout();
            List<(object Item, string Name)> sorted = GridRowNames.Read(grid);
            Assert.Equal(["Row 2", "Done", "Row 4", "alpha", "gamma"], sorted.Select(row => row.Name));
            Assert.All(sorted, row => Assert.Equal(
                source.Single(before => ReferenceEquals(before.Item, row.Item)).Name, row.Name));
        });
    });

    /// <summary>The same attachment across re-realization and a rebind:
    /// with virtualization a scrolled-away row loses its container and a
    /// new one is named when it returns, and a rebind hands the grid fresh
    /// row objects — both times a text-less row reads the ordinal of its
    /// place in the parsed table, never of where it now sits.</summary>
    [Fact]
    public void ReadingTableFallbackNamesStayOnTheirSourceRows() => RunSta(() =>
    {
        var markdown = new System.Text.StringBuilder("| Name | Status |\n| --- | --- |\n");
        for (int row = 1; row <= 300; row++)
        {
            markdown.Append(row is 2 or 4 or 250 ? "|  |  |\n" : $"| note {row:D3} | Open |\n");
        }
        string table = markdown.ToString();
        var model = Reading.ReadingTableGrid.BuildModel(table)!.Value;
        var grid = new AccessibleDataGrid { Announce = _ => { } };
        Reading.ReadingTableGrid.Bind(grid, model);
        GridRowNames.Hosted(grid, () =>
        {
            Assert.Equal("Row 2", GridRowNames.NameOf(grid, model.Rows[1]));
            Assert.Equal("Row 4", GridRowNames.NameOf(grid, model.Rows[3]));
            DataGridRow firstContainer = Assert.IsType<DataGridRow>(
                grid.Grid.ItemContainerGenerator.ContainerFromItem(model.Rows[1]));

            // Scroll far away; row 250 is named. The panel discards the top
            // rows' containers on a later scroll pass (measured: the third).
            grid.Grid.ScrollIntoView(model.Rows[249]);
            Settle(grid);
            Assert.Equal("Row 250", GridRowNames.NameOf(grid, model.Rows[249]));
            grid.Grid.ScrollIntoView(model.Rows[280]);
            Settle(grid);
            grid.Grid.ScrollIntoView(model.Rows[200]);
            Settle(grid);
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => grid.Grid.ItemContainerGenerator.ContainerFromItem(model.Rows[1]) is null,
                    TimeSpan.FromSeconds(5)),
                "row 2's container was never discarded, so nothing re-realized it");

            // And back: a new container for the same row, the same name.
            grid.Grid.ScrollIntoView(model.Rows[0]);
            Settle(grid);
            Assert.NotSame(
                firstContainer,
                grid.Grid.ItemContainerGenerator.ContainerFromItem(model.Rows[1]));
            Assert.Equal("Row 2", GridRowNames.NameOf(grid, model.Rows[1]));
            Assert.Equal("Row 4", GridRowNames.NameOf(grid, model.Rows[3]));

            // Sorted, the three text-less rows lead in source order.
            Assert.NotNull(grid.ApplySort(0, ascending: true));
            grid.Grid.ScrollIntoView(model.Rows[1]);
            Settle(grid);
            Assert.Equal(
                ["Row 2", "Row 4", "Row 250"],
                GridRowNames.Read(grid).Take(3).Select(row => row.Name));

            // A rebind brings new row objects and keeps the sort: each
            // text-less row still reads its place in the parsed table.
            var rebound = Reading.ReadingTableGrid.BuildModel(table)!.Value;
            Reading.ReadingTableGrid.Bind(grid, rebound);
            grid.Grid.ScrollIntoView(rebound.Rows[1]);
            Settle(grid);
            Assert.Equal("Row 2", GridRowNames.NameOf(grid, rebound.Rows[1]));
            Assert.Equal("Row 4", GridRowNames.NameOf(grid, rebound.Rows[3]));
            Assert.Equal("Row 250", GridRowNames.NameOf(grid, rebound.Rows[249]));
        });
    });

    /// <summary>Codex PR 3 round 2: rows that share an identity — here a
    /// reading table's first cell — would read alike, so each carries its
    /// source row: "same, row 2". Only the colliding rows do, and the
    /// suffix stays on its source row through a sort, re-realization and
    /// a rebind; a rebind that leaves one carrier drops its suffix.</summary>
    [Fact]
    public void DuplicateIdentitiesAreToldApartByTheirSourceRow() => RunSta(() =>
    {
        static string Table(IEnumerable<int> duplicated)
        {
            var markdown = new System.Text.StringBuilder("| Name | Status |\n| --- | --- |\n");
            var same = new HashSet<int>(duplicated);
            for (int row = 1; row <= 300; row++)
            {
                markdown.Append(same.Contains(row) ? $"| same | {row} |\n" : $"| note {row:D3} | Open |\n");
            }
            return markdown.ToString();
        }
        var model = Reading.ReadingTableGrid.BuildModel(Table([2, 150, 299]))!.Value;
        var grid = new AccessibleDataGrid { Announce = _ => { } };
        Reading.ReadingTableGrid.Bind(grid, model);
        GridRowNames.Hosted(grid, () =>
        {
            Assert.Equal("note 001", GridRowNames.NameOf(grid, model.Rows[0]));
            Assert.Equal("same, row 2", GridRowNames.NameOf(grid, model.Rows[1]));
            DataGridRow firstContainer = Assert.IsType<DataGridRow>(
                grid.Grid.ItemContainerGenerator.ContainerFromItem(model.Rows[1]));

            // Scroll far away and back: a new container, the same name.
            grid.Grid.ScrollIntoView(model.Rows[298]);
            Settle(grid);
            Assert.Equal("same, row 299", GridRowNames.NameOf(grid, model.Rows[298]));
            grid.Grid.ScrollIntoView(model.Rows[149]);
            Settle(grid);
            Assert.Equal("same, row 150", GridRowNames.NameOf(grid, model.Rows[149]));
            grid.Grid.ScrollIntoView(model.Rows[200]);
            Settle(grid);
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => grid.Grid.ItemContainerGenerator.ContainerFromItem(model.Rows[1]) is null,
                    TimeSpan.FromSeconds(5)),
                "row 2's container was never discarded, so nothing re-realized it");
            grid.Grid.ScrollIntoView(model.Rows[0]);
            Settle(grid);
            Assert.NotSame(firstContainer, grid.Grid.ItemContainerGenerator.ContainerFromItem(model.Rows[1]));
            Assert.Equal("same, row 2", GridRowNames.NameOf(grid, model.Rows[1]));

            // Sorted descending, the three carriers lead ("same" sorts after
            // every "note …"), each still with its own source row.
            Assert.NotNull(grid.ApplySort(0, ascending: false));
            grid.Grid.ScrollIntoView(model.Rows[1]);
            Settle(grid);
            Assert.Equal(
                ["same, row 150", "same, row 2", "same, row 299"],
                GridRowNames.Read(grid).Take(3).Select(row => row.Name).Order(StringComparer.Ordinal));

            // A rebind recomputes from the rows passed: the same table keeps
            // every suffix; a table with one carrier left drops it.
            var rebound = Reading.ReadingTableGrid.BuildModel(Table([2, 150, 299]))!.Value;
            Reading.ReadingTableGrid.Bind(grid, rebound);
            grid.Grid.ScrollIntoView(rebound.Rows[1]);
            Settle(grid);
            Assert.Equal("same, row 2", GridRowNames.NameOf(grid, rebound.Rows[1]));
            var single = Reading.ReadingTableGrid.BuildModel(Table([150]))!.Value;
            Reading.ReadingTableGrid.Bind(grid, single);
            grid.Grid.ScrollIntoView(single.Rows[149]);
            Settle(grid);
            Assert.Equal("same", GridRowNames.NameOf(grid, single.Rows[149]));
        });
    });

    /// <summary>Two bibliography entries with one title and year read
    /// apart by their source row; a lone entry reads bare.</summary>
    [Fact]
    public void BibliographyEntriesSharingATitleAndYearAreToldApart() => RunSta(() =>
    {
        var grid = new AccessibleDataGrid { Announce = _ => { } };
        MainWindow.BindBibliographyEntries(
            grid,
            [
                new Panels.BibliographyRowViewModel(Entry("doe2021a", "Accessible grids", 2021)),
                new Panels.BibliographyRowViewModel(Entry("roe2020", "Reading tables", 2020)),
                new Panels.BibliographyRowViewModel(Entry("doe2021b", "Accessible grids", 2021)),
            ],
            "3 entries.");

        Assert.Equal(
            ["Accessible grids (2021), row 1", "Accessible grids (2021), row 3", "Reading tables (2020)"],
            GridRowNames.Realized(grid).Select(row => row.Name).Order(StringComparer.Ordinal));
    });

    /// <summary>A bibliography entry row is its entry: "Title (year)",
    /// the text its row header carries — the key when there is no title,
    /// no parenthesis when there is no year.</summary>
    [Fact]
    public void BibliographyEntryRowsAreNamedByTitleAndYear() => RunSta(() =>
    {
        var grid = new AccessibleDataGrid { Announce = _ => { } };
        MainWindow.BindBibliographyEntries(
            grid,
            [
                new Panels.BibliographyRowViewModel(Entry("doe2021", "Accessible grids", 2021)),
                new Panels.BibliographyRowViewModel(Entry("roe", string.Empty, null)),
            ],
            "2 entries.");

        Assert.Equal(
            ["Accessible grids (2021)", "roe"],
            GridRowNames.Realized(grid).Select(row => row.Name));
    });

    /// <summary>An unresolved-citation row is its key, the row header.</summary>
    [Fact]
    public void BibliographyUnresolvedRowsAreNamedByTheirKey() => RunSta(() =>
    {
        var grid = new AccessibleDataGrid { Announce = _ => { } };
        MainWindow.BindBibliographyUnresolved(
            grid,
            [new Panels.UnresolvedRowViewModel(new UnresolvedCitation(Path: "notes/a.md", Key: "smith2020"))],
            "1 unresolved citation.");

        Assert.Equal(
            ["smith2020"],
            GridRowNames.Realized(grid).Select(row => row.Name));
    });

    /// <summary>A bulk-rename preview row is the note it renames.</summary>
    [Fact]
    public void BulkRenamePreviewRowsAreNamedByTheNoteTheyRename() => RunSta(() =>
    {
        var grid = new AccessibleDataGrid { Announce = _ => { } };
        MainWindow.BindBulkRenamePreview(
            grid,
            [
                new Panels.BulkRenameViewModel.PreviewRow("notes/a.md", "Will rename", "title", "heading"),
                new Panels.BulkRenameViewModel.PreviewRow("b.md", "Skipped", "", ""),
            ],
            string.Empty);

        Assert.Equal(
            ["notes/a.md", "b.md"],
            GridRowNames.Realized(grid).Select(row => row.Name));
    });

    /// <summary>Let a scroll or re-population finish: layout, then the
    /// dispatcher work it queued (realization runs at Background), then
    /// layout again.</summary>
    private static void Settle(AccessibleDataGrid grid)
    {
        grid.UpdateLayout();
        PumpedDispatcher.Drain();
        grid.UpdateLayout();
    }

    private static BibEntry Entry(string key, string title, int? year) =>
        new(
            Key: key,
            ItemType: "article",
            Title: title,
            Authors: [],
            Year: year,
            Journal: null,
            Doi: null,
            Url: null,
            Publisher: null,
            AbstractText: null);

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

/// <summary>W7-7 PR 3 (#1246, contract R-4), the surface callers: the
/// Base tab and dock grids, a dashboard section and the canvas table, each
/// driven over a real session.</summary>
public sealed class AccessibleDataGridSurfaceRowNameTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;

    public AccessibleDataGridSurfaceRowNameTests()
    {
        _fixture = FixtureVault.Create(3, "grid-row-names");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "Notes.base"),
            "filters: 'file.ext == \"md\"'\n"
            + "views:\n"
            + "  - type: table\n"
            + "    name: Main\n"
            + "    order:\n"
            + "      - file.name\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "board.canvas"),
            """
            {
              "nodes": [
                {"id":"grp","type":"group","x":-40,"y":-40,"width":480,"height":240,"label":"Research"},
                {"id":"q","type":"text","text":"Core question","x":0,"y":0,"width":200,"height":100},
                {"id":"e","type":"text","text":"Evidence so far","x":220,"y":0,"width":200,"height":100}
              ],
              "edges": []
            }
            """);
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    /// <summary>Both of the Base surface's binds — the tab's and the
    /// read-only dock's — name a row by its note's file name. Unnamed, NVDA
    /// read "SlateWindows.Bases.BaseGridRowViewModel, data item".</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BasesRowsAreNamedByTheirFile(bool readOnlySurface) => RunSta(() =>
    {
        var document = new Bases.BaseDocumentViewModel(
            _session, "Notes.base", _ => { }, synchronousForTests: true);
        document.Load();
        var surface = new Bases.BaseSurfaceView
        {
            IsReadOnlySurface = readOnlySurface,
            Model = document,
        };

        List<(object Item, string Name)> rows =
            GridRowNames.Realized(surface.GridForTests, surface);
        Assert.Equal(
            ["note0.md", "note1.md", "note2.md"],
            rows.Select(row => row.Name).Order(StringComparer.Ordinal));
        Assert.All(rows, row => Assert.Equal(
            Path.GetFileName(((Bases.BaseGridRowViewModel)row.Item).Row.FilePath), row.Name));
        document.Shutdown();
    });

    /// <summary>Codex PR 3 round 2: one file name in two folders would read
    /// alike, so each row carries its source row.</summary>
    [Fact]
    public void BasesRowsSharingAFileNameAreToldApart() => RunSta(() =>
    {
        using FixtureVault vault = FixtureVault.Create(0, "grid-shared-file-names");
        foreach (string folder in new[] { "A", "B" })
        {
            Directory.CreateDirectory(Path.Combine(vault.Root, folder));
            File.WriteAllText(Path.Combine(vault.Root, folder, "same.md"), $"# In {folder}\n");
        }
        File.WriteAllText(
            Path.Combine(vault.Root, "Same.base"),
            "filters: 'file.ext == \"md\"'\n"
            + "views:\n"
            + "  - type: table\n"
            + "    name: Main\n"
            + "    order:\n"
            + "      - file.name\n");
        using VaultSession session = VaultSession.OpenFilesystem(vault.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        var document = new Bases.BaseDocumentViewModel(
            session, "Same.base", _ => { }, synchronousForTests: true);
        document.Load();
        var surface = new Bases.BaseSurfaceView { Model = document };

        List<(object Item, string Name)> rows =
            GridRowNames.Realized(surface.GridForTests, surface);
        Assert.Equal(
            ["same.md, row 1", "same.md, row 2"],
            rows.Select(row => row.Name).Order(StringComparer.Ordinal));
        document.Shutdown();
    });

    /// <summary>A dashboard section's read-only grid names rows as the
    /// Base tab does.</summary>
    [Fact]
    public void DashboardSectionRowsAreNamedByTheirFile() => RunSta(() =>
    {
        var document = new Bases.BaseDocumentViewModel(
            _session, "Notes.base", _ => { }, synchronousForTests: true);
        document.Load();
        AccessibleDataGrid grid = Bases.DashboardSurfaceView.BuildSectionGrid(
            "RowNames", 0, Assert.IsType<BasesResultSet>(document.Result));

        Assert.Equal(
            ["note0.md", "note1.md", "note2.md"],
            GridRowNames.Realized(grid).Select(row => row.Name).Order(StringComparer.Ordinal));
        document.Shutdown();
    });

    /// <summary>A canvas table row is core's speakable name, its row
    /// header. Unnamed, NVDA read the record: "CanvasTableRow { NodeId =
    /// grp-research, … GroupPath = System.String[] … }".</summary>
    [Fact]
    public void CanvasTableRowsAreNamedByTheirSpeakableName() => RunSta(() =>
    {
        var document = new Canvas.CanvasDocumentViewModel(
            _session,
            "board.canvas",
            new Canvas.CanvasAnnouncer(_ => { }, TimeSpan.FromMinutes(1)),
            synchronousForTests: true);
        document.Load();
        var table = new Canvas.CanvasTableView { Model = document };

        List<(object Item, string Name)> rows = GridRowNames.Realized(table.GridForTests, table);
        Assert.Contains("Core question", rows.Select(row => row.Name));
        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.Equal(((CanvasTableRow)row.Item).SpeakableName, row.Name));
        document.Shutdown();
    });

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

/// <summary>The realized rows of a grid, as UIA reads them.</summary>
internal static class GridRowNames
{
    /// <summary>Show <paramref name="root"/> (the grid itself by
    /// default), realize, and read each realized row's item and the NAME
    /// of its DataItem peer — not the property the hook set.</summary>
    internal static List<(object Item, string Name)> Realized(
        AccessibleDataGrid grid, System.Windows.FrameworkElement? root = null)
    {
        var window = new System.Windows.Window
        {
            Content = root ?? grid,
            Width = 640,
            Height = 480,
            ShowInTaskbar = false,
            WindowStyle = System.Windows.WindowStyle.None,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            return Read(grid);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Run <paramref name="body"/> with <paramref name="grid"/>
    /// shown — short enough that a long table virtualizes.</summary>
    internal static void Hosted(AccessibleDataGrid grid, Action body)
    {
        var window = new System.Windows.Window
        {
            Content = grid,
            Width = 640,
            Height = 240,
            ShowInTaskbar = false,
            WindowStyle = System.Windows.WindowStyle.None,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            body();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The UIA name of <paramref name="item"/>'s realized row.</summary>
    internal static string NameOf(AccessibleDataGrid grid, object item) =>
        Read(grid).Single(row => ReferenceEquals(row.Item, item)).Name;

    /// <summary>The realized rows of a grid already shown, in view order.</summary>
    internal static List<(object Item, string Name)> Read(AccessibleDataGrid grid)
    {
        var peer = (System.Windows.Automation.Peers.DataGridAutomationPeer)
            System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(grid.Grid);
        // The peer caches its children; a re-populated grid (a sort) must
        // be read in its new order, not the cached one.
        peer.ResetChildrenCache();
        List<(object Item, string Name)> rows = peer.GetChildren()
            .OfType<System.Windows.Automation.Peers.DataGridItemAutomationPeer>()
            .Select(row => (row.Item, row.GetName()))
            .ToList();
        Assert.NotEmpty(rows);
        return rows;
    }
}
