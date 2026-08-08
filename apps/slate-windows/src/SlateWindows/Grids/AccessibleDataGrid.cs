// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Grids;

/// <summary>
/// The W4-1 accessible grid substrate: one wrapped WPF DataGrid playing
/// the mac <c>AccessibleDataGrid</c> v2 role (05 §8.7 verbatim) that
/// every W4+ feature grid consumes — Bases, tasks, citations,
/// properties, graph and canvas tables.
///
/// Contract highlights, each pinned by tests:
/// - Announcements are CANONICAL events (#969 grid family): sort posts
///   <c>GridSorted</c>, vertical row moves post <c>GridRowMoved</c>
///   (core renders the audio-description/focused-cell dedup), all other
///   cell moves post <c>GridCellMoved</c>. The substrate never composes
///   announcement strings — text and priority come from core, exactly
///   as the mac twin now does.
/// - The summary line is a SEPARATELY-FOCUSABLE region below the grid
///   (the mac `isSummaryElement` contract).
/// - Sort is keyboard-first: Ctrl+Alt+S toggles the current column's
///   sort (header click parity comes free from DataGrid, but the
///   §8.7 matrix forbids header-click-only sorting).
/// - Export commands surface core-composed text (`base_export`
///   precedent): the substrate raises the command with the injected
///   producer's text; DELIVERY wording belongs to the consuming
///   surface's own announcement family, never to the substrate.
/// - Virtualization stays AT-safe: row virtualization ON in STANDARD
///   mode (the default Recycling mode is a documented UIA crash class,
///   dotnet/wpf #8528/#5428/#11519), column virtualization OFF,
///   deferred scrolling ON. Pinned in code so a template swap cannot
///   silently regress it.
/// </summary>
internal sealed class AccessibleDataGrid : UserControl
{
    private readonly DataGrid _grid;
    private readonly TextBlock _summary;
    private readonly ContextMenu _persistentMenu = new();
    private readonly ObservableCollection<object> _items = new();
    private IReadOnlyList<AccessibleGridColumn> _columns = Array.Empty<AccessibleGridColumn>();
    private IReadOnlyList<AccessibleGridRowAction> _rowActions =
        Array.Empty<AccessibleGridRowAction>();
    private Func<object, string?>? _rowAudioDescription;
    private Func<ExportFormat, string>? _exportProducer;
    private Action<object>? _rowActivated;
    private AccessibleGridColumn? _rowHeaderColumn;
    private object? _lastAnnouncedRow;
    private (int ColumnIndex, bool Ascending)? _activeSort;
    private string _typeAheadBuffer = string.Empty;
    private DateTime _typeAheadLast = DateTime.MinValue;
    private AccessibilityNotificationDispatcher? _dispatcher;

    /// <summary>Injectable announce seam (the mac hook's twin). The
    /// default posts through the canonical dispatcher; tests and
    /// specialized surfaces (graph/canvas funnels) swap it.</summary>
    public Action<A11yEvent> Announce { get; set; }

    /// <summary>Raised when an export command produced text — the
    /// consuming surface owns delivery (clipboard, save dialog) and its
    /// own announcement family.</summary>
    public event Action<ExportFormat, string>? ExportProduced;

    public static readonly RoutedCommand ExportCsvCommand = new(
        nameof(ExportCsvCommand), typeof(AccessibleDataGrid));

    public static readonly RoutedCommand ExportMarkdownCommand = new(
        nameof(ExportMarkdownCommand), typeof(AccessibleDataGrid));

    public static readonly RoutedCommand ToggleSortCommand = new(
        nameof(ToggleSortCommand),
        typeof(AccessibleDataGrid),
        new InputGestureCollection
        {
            new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Alt),
        });

    public static readonly RoutedCommand FilterCommand = new(
        nameof(FilterCommand),
        typeof(AccessibleDataGrid),
        new InputGestureCollection
        {
            new KeyGesture(Key.F, ModifierKeys.Control),
        });

    /// <summary>The §8.7 keyboard filter hook: Ctrl+F, grid-scoped.
    /// The substrate owns no filter UI or predicate — the consuming
    /// surface does (W4-6's transient quick filter is the intended
    /// consumer). With no subscriber the gesture continues routing,
    /// so the app-level find is never shadowed by a grid that cannot
    /// filter.</summary>
    public event Action? FilterRequested;

    public AccessibleDataGrid()
    {
        _grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.Cell,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.None,
            // AT-safe virtualization (see class doc): Standard, rows
            // only, deferred scrolling.
            EnableRowVirtualization = true,
            EnableColumnVirtualization = false,
            ItemsSource = _items,
        };
        VirtualizingPanel.SetVirtualizationMode(_grid, VirtualizationMode.Standard);
        ScrollViewer.SetIsDeferredScrollingEnabled(_grid, true);
        _grid.SetResourceReference(ForegroundProperty, "Slate.TextBrush");
        _grid.SetResourceReference(BackgroundProperty, "Slate.SurfaceBrush");
        _grid.CurrentCellChanged += OnCurrentCellChanged;
        _grid.CellEditEnding += OnCellEditEnding;
        _grid.Sorting += OnHeaderSorting;
        _grid.PreviewTextInput += OnTypeAhead;
        _grid.PreviewKeyDown += OnActivationKey;
        _grid.MouseDoubleClick += OnActivationDoubleClick;
        _grid.ContextMenuOpening += OnContextMenuOpening;
        _grid.LoadingRow += OnLoadingRow;
        // The menu EXISTS from construction (see OnContextMenuOpening:
        // first-request opening requires it); its items are rebuilt
        // per opening from the current row.
        _grid.ContextMenu = _persistentMenu;
        AutomationProperties.SetAutomationId(_grid, "AccessibleDataGrid");
        // The grid precedes the summary in tab order — MoveFocus(First)
        // and plain Tab must land on cells, never on the summary that
        // happens to sit earlier in the tree (adversarial round 1).
        _grid.TabIndex = 0;

        _summary = new TextBlock
        {
            Focusable = true,
            Margin = new Thickness(8, 4, 8, 4),
        };
        KeyboardNavigation.SetTabIndex(_summary, 1);
        _summary.SetResourceReference(TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        AutomationProperties.SetAutomationId(_summary, "AccessibleDataGridSummary");

        var layout = new DockPanel();
        DockPanel.SetDock(_summary, Dock.Bottom);
        layout.Children.Add(_summary);
        layout.Children.Add(_grid);
        Content = layout;

        Announce = @event => (_dispatcher ??= new AccessibilityNotificationDispatcher(this))
            .Post(@event);

        CommandBindings.Add(new CommandBinding(
            ToggleSortCommand, (_, _) => ToggleSortOnCurrentColumn()));
        CommandBindings.Add(new CommandBinding(
            ExportCsvCommand,
            (_, _) => ProduceExport(ExportFormat.Csv),
            (_, e) => e.CanExecute = _exportProducer is not null));
        CommandBindings.Add(new CommandBinding(
            ExportMarkdownCommand,
            (_, _) => ProduceExport(ExportFormat.Markdown),
            (_, e) => e.CanExecute = _exportProducer is not null));
        CommandBindings.Add(new CommandBinding(
            FilterCommand,
            (_, _) => FilterRequested?.Invoke(),
            (_, e) =>
            {
                e.CanExecute = FilterRequested is not null;
                e.ContinueRouting = FilterRequested is null;
            }));
    }

    /// <summary>The wrapped grid, for tests and conformance probes.</summary>
    internal DataGrid Grid => _grid;

    /// <summary>Realized rows carry the row-header column's value as
    /// their native DataGrid row header — the UIA Table pattern's row
    /// identity. LoadingRow covers virtualization: it fires per
    /// realization, so headers exist exactly where rows do.</summary>
    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (_rowHeaderColumn is { } primary)
        {
            e.Row.Header = primary.Cell(e.Row.Item);
        }
    }

    /// <summary>
    /// The group-heading grammar, rendered CORE-side (#969 GridGroup)
    /// — the mac twin labels its group rows with the same render, so
    /// the two hosts can never drift. Grouped PRESENTATION arrives
    /// with its owning surface (W4-6's Bases grid consumes
    /// `BasesResultSet.groups`); the substrate owns the canonical
    /// heading text every grouped consumer must use.
    /// </summary>
    public static string ComposeGroupHeading(
        string label, uint rowCount, string? summary) =>
        SlateUniffiMethods.A11yRender(
            new A11yEvent.GridGroup(label, rowCount, summary)).Text;

    /// <summary>
    /// Put keyboard focus on the FIRST CELL — the §8.7 entry point.
    /// Window openers call this on load so entry announces headers +
    /// cell, never the summary that precedes the grid in the tree
    /// (adversarial round 1).
    /// </summary>
    public bool FocusFirstCell()
    {
        if (_items.Count == 0 || _grid.Columns.Count == 0)
        {
            return _grid.Focus();
        }
        return FocusCellElement(_items[0], _grid.Columns[0]);
    }

    /// <summary>
    /// Put keyboard focus on the first cell of the row matching
    /// <paramref name="predicate"/>, if the bound set contains one.
    /// Returns false and moves nothing when it does not — the caller
    /// decides what to say about a miss.
    ///
    /// The bool reports whether the row was IN THE BOUND SET, not
    /// whether the OS granted keyboard focus. Those differ whenever
    /// the grid is not in a loaded window, and currency plus selection
    /// — which is what AT reports — is set either way; conflating them
    /// would make a successful jump indistinguishable from a missing
    /// key.
    ///
    /// W4-5 (#737) needed this for the Ctrl+J bibliography jump. It
    /// lives here rather than in the citations code because reaching
    /// into <see cref="Grid"/> to set currency by hand would be a
    /// second implementation of cell focus, which the grid-conformance
    /// contract forbids (W4-5 contract 8 / D-12).
    /// </summary>
    public bool FocusRow(Func<object, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (_grid.Columns.Count == 0)
        {
            return false;
        }
        foreach (object item in _items)
        {
            if (predicate(item))
            {
                _ = FocusCellElement(item, _grid.Columns[0]);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Put keyboard focus on the CELL ELEMENT for (item, column):
    /// scroll, realize the container synchronously (under a starved
    /// session the deferred generation leaves Focus() on the grid, and
    /// AT announces the grid instead of the cell), set currency, focus
    /// the realized DataGridCell. Falls back to the grid only when the
    /// container genuinely cannot exist yet (pre-load).
    /// </summary>
    private bool FocusCellElement(object item, DataGridColumn column)
    {
        _grid.ScrollIntoView(item, column);
        if (_grid.IsLoaded)
        {
            _grid.UpdateLayout();
        }
        _grid.CurrentCell = new DataGridCellInfo(item, column);
        _grid.SelectedCells.Clear();
        _grid.SelectedCells.Add(_grid.CurrentCell);
        if (_grid.ItemContainerGenerator.ContainerFromItem(item)
                is DataGridRow row
            && column.GetCellContent(row)?.Parent is DataGridCell cell)
        {
            return cell.Focus();
        }
        return _grid.Focus();
    }

    /// <summary>
    /// The automation id of the inner grid; the summary region takes
    /// the same id with a "Summary" suffix. Defaults to
    /// <c>AccessibleDataGrid</c> / <c>AccessibleDataGridSummary</c>.
    ///
    /// Hosts must set this when a window shows MORE THAN ONE grid.
    /// W4-5 (#737) is the first — two bibliography segments plus the
    /// bulk-rename preview — and with a hardcoded id an AT user, or a
    /// FlaUI lookup, cannot tell the three apart. Fixing it in the
    /// substrate rather than working around it in the citations code
    /// is what D-12 requires.
    /// </summary>
    public string GridAutomationId
    {
        get => AutomationProperties.GetAutomationId(_grid);
        set
        {
            AutomationProperties.SetAutomationId(_grid, value);
            AutomationProperties.SetAutomationId(_summary, $"{value}Summary");
        }
    }

    /// <summary>The separately-focusable summary region.</summary>
    internal TextBlock SummaryRegion => _summary;

    internal (int ColumnIndex, bool Ascending)? ActiveSort => _activeSort;

    /// <summary>
    /// Bind a surface onto the substrate. Columns become read-only
    /// text columns whose cells carry the mac "Header: value" label
    /// contract; <paramref name="rowAudioDescription"/> is the core
    /// `audio_description` the row-move announcement consumes.
    /// </summary>
    public void Bind(
        IReadOnlyList<AccessibleGridColumn> columns,
        IReadOnlyList<object> rows,
        string summary,
        string accessibilityLabel,
        Func<object, string?>? rowAudioDescription = null,
        IReadOnlyList<AccessibleGridRowAction>? rowActions = null,
        Func<ExportFormat, string>? exportProducer = null,
        Action<object>? rowActivated = null)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        // The user's sort is a user decision, and a re-publish is not
        // the user changing their mind. Dropping it here meant that
        // typing one character into a consuming surface's filter box
        // silently reverted the ordering, cleared the header indicator,
        // and reordered rows under the reader with no announcement.
        //
        // Carried as the column's HEADER, resolved back to an index
        // below, and captured HERE because _columns is about to be
        // replaced. A bare index re-applies the sort to whatever column
        // now occupies that slot, which is the same column only as long
        // as every re-bind passes the same list — true of every
        // consumer today, false the moment one grid instance serves
        // more than one column set.
        // Externally-sorted grids (W4-6): the rows ALREADY arrive in
        // core's order, so capturing and re-applying a host sort here
        // would re-dispatch a core execute per publish. The surface
        // owns its sort state and re-asserts the indicator after Bind.
        (string Header, bool Ascending)? previousSort =
            ExternalSortHandler is null
            && _activeSort is { } active
            && active.ColumnIndex < _columns.Count
                ? (_columns[active.ColumnIndex].Header, active.Ascending)
                : null;
        _activeSort = null;
        _columns = columns;
        _rowAudioDescription = rowAudioDescription;
        _rowActions = rowActions ?? Array.Empty<AccessibleGridRowAction>();
        _exportProducer = exportProducer;
        _rowActivated = rowActivated;
        _lastAnnouncedRow = null;
        // The reader's position, captured BEFORE the repopulate
        // destroys every container. ApplySort already does this and
        // says why: "re-populating destroys the focused cell's
        // container, and without a restore keyboard focus falls to the
        // window". Bind destroys the same containers, and consuming
        // surfaces re-Bind on every publish — so a background save
        // while someone is arrowing through the grid moved them.
        //
        // Keyed on the ROW HEADER, not object identity: every publish
        // builds fresh row view models, so identity is gone by
        // definition. The row header is already the row's identity
        // under §8.7.
        string? previousRowIdentity = RowIdentityOf(_grid.CurrentCell.Item);
        // Row-header text is not guaranteed unique — two notes can both
        // be "Untitled". Where it repeats, the previous ordinal breaks
        // the tie so the reader keeps the OCCURRENCE they were on.
        int previousRowOrdinal = _items.IndexOf(_grid.CurrentCell.Item);
        int previousColumnIndex = _grid.CurrentCell.Column is { } previousColumn
            ? _grid.Columns.IndexOf(previousColumn)
            : -1;
        _grid.Columns.Clear();
        for (int index = 0; index < columns.Count; index++)
        {
            AccessibleGridColumn column = columns[index];
            var gridColumn = new AccessibleGridTextColumn(column, index, this)
            {
                Header = column.Header,
                CanUserSort = column.Sort is not null
                    || (column.IsExternallySortable && ExternalSortHandler is not null),
                // The comparator sorts; a KVC-style member path never
                // does (the mac prototype-key precedent).
                SortMemberPath = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            _grid.Columns.Add(gridColumn);
        }

        // Native row identity (§8.7 "headers on entry", adversarial
        // round 1): the row-header column — the core ColumnRole
        // Primary twin — feeds real DataGrid row headers, so the UIA
        // Table pattern associates every row with its identity and AT
        // entering any cell hears which row it is on.
        _rowHeaderColumn = columns.FirstOrDefault(column => column.IsRowHeader);
        _grid.HeadersVisibility = _rowHeaderColumn is null
            ? DataGridHeadersVisibility.Column
            : DataGridHeadersVisibility.All;

        _items.Clear();
        foreach (object row in rows)
        {
            _items.Add(row);
        }

        _summary.Text = summary;
        AutomationProperties.SetName(_summary, $"Summary: {summary}");
        AutomationProperties.SetName(_grid, accessibilityLabel);

        WithoutAnnouncing(() => RestoreReaderPosition(
            previousRowIdentity, previousColumnIndex, previousRowOrdinal));

        // Re-apply silently: ApplySort posts a GridSorted announcement,
        // which is right when the USER sorts and wrong when a
        // background re-publish restores what they already chose. A
        // column that is gone, renamed, or no longer sortable drops the
        // sort rather than moving it somewhere the user did not choose.
        if (previousSort is { } sort)
        {
            for (int index = 0; index < _columns.Count; index++)
            {
                if (_columns[index].Sort is not null
                    && string.Equals(
                        _columns[index].Header, sort.Header, StringComparison.Ordinal))
                {
                    WithoutAnnouncing(() => ApplySort(index, sort.Ascending));
                    break;
                }
            }
        }
    }

    /// <summary>
    /// The row's identity for position-restore purposes: the row-header
    /// cell's text, or null when the surface declares no row-header
    /// column (nothing stable to key on).
    ///
    /// Only ever called with a row from the CURRENTLY BOUND set. The
    /// `Cell` delegates cast to the surface's row type, so handing one
    /// anything else — most reachably `CollectionView.NewItemPlaceholder`,
    /// which is what `CurrentCell.Item` holds on an empty grid — throws
    /// out of Bind and the surface never renders at all. The row-action
    /// hit-test next door rejects the placeholder for the same reason.
    /// </summary>
    private string? RowIdentityOf(object? item) =>
        item is not null
        && item != CollectionView.NewItemPlaceholder
        && _items.Contains(item)
            ? BoundRowIdentityOf(item)
            : null;

    /// <summary>
    /// Identity for a row ALREADY KNOWN to be in the bound set.
    ///
    /// Split from <see cref="RowIdentityOf"/> because that one's
    /// `_items.Contains` guard is needed exactly once — at capture,
    /// where `CurrentCell.Item` may be foreign or the new-item
    /// placeholder. Calling it from inside a loop that is already
    /// walking `_items` made every re-publish O(n²): 8,000 rows took
    /// 708 ms on the UI thread against 33 ms before, and the
    /// bulk-rename preview has no row cap.
    /// </summary>
    private string? BoundRowIdentityOf(object row) =>
        _rowHeaderColumn is { } header ? header.Cell(row) : null;

    /// <summary>
    /// Put the reader back where they were, or leave them alone.
    ///
    /// A row that is GONE after the republish is NOT substituted with a
    /// neighbour: silently moving someone to a different row is worse
    /// than leaving currency where the grid put it, because nothing
    /// announces the move.
    /// </summary>
    private void RestoreReaderPosition(
        string? rowIdentity, int columnIndex, int previousOrdinal)
    {
        if (rowIdentity is null
            || columnIndex < 0
            || columnIndex >= _grid.Columns.Count)
        {
            return;
        }
        // Nearest match to where the reader WAS, not the first match:
        // with unique row headers these are the same row, and with
        // repeated ones this is the difference between staying put and
        // being moved to a namesake without an announcement. A
        // previousOrdinal of -1 (no prior currency) degrades to the
        // first match, which is the only sensible answer there.
        int best = -1;
        for (int index = 0; index < _items.Count; index++)
        {
            if (!string.Equals(
                BoundRowIdentityOf(_items[index]), rowIdentity, StringComparison.Ordinal))
            {
                continue;
            }
            if (best < 0
                || Math.Abs(index - previousOrdinal) < Math.Abs(best - previousOrdinal))
            {
                best = index;
            }
        }
        if (best < 0)
        {
            return;
        }
        _grid.CurrentCell = new DataGridCellInfo(_items[best], _grid.Columns[columnIndex]);
        _grid.SelectedCells.Clear();
        _grid.SelectedCells.Add(_grid.CurrentCell);
    }

    /// <summary>
    /// Run something that moves currency WITHOUT speaking.
    ///
    /// Currency changes announce, which is right when the user moved
    /// and wrong when a background re-publish restored what they had.
    /// Both non-user movers — the sort re-apply and the reader-position
    /// restore — go through here, so neither can be silenced without
    /// the other.
    /// </summary>
    private void WithoutAnnouncing(Action action)
    {
        Action<A11yEvent> announce = Announce;
        Announce = _ => { };
        try
        {
            action();
        }
        finally
        {
            Announce = announce;
        }
    }

    /// <summary>
    /// W4-6 (#738, contract C1/C6): core-executed sorting. When set,
    /// a sort request on an <see cref="AccessibleGridColumn.IsExternallySortable"/>
    /// column is DELEGATED — the handler dispatches the sort in core
    /// and the reordered rows arrive through a later <see cref="Bind"/>;
    /// the grid neither reorders nor announces (the surface announces
    /// its own canonical event when the rows actually land, so the
    /// spoken order can never diverge from the rendered rows). The
    /// surface re-asserts the header indicator after each publish via
    /// <see cref="SetSortIndicator"/>.
    /// </summary>
    internal Func<int, bool, bool>? ExternalSortHandler { get; set; }

    /// <summary>Header arrows + toggle state for an externally-sorted
    /// grid, with NO reorder and NO announcement — the rows already
    /// arrived in core's order. Pass null to clear.</summary>
    internal void SetSortIndicator((int ColumnIndex, bool Ascending)? sort)
    {
        _activeSort = sort;
        for (int index = 0; index < _grid.Columns.Count; index++)
        {
            _grid.Columns[index].SortDirection =
                sort is { } active && index == active.ColumnIndex
                    ? (active.Ascending
                        ? System.ComponentModel.ListSortDirection.Ascending
                        : System.ComponentModel.ListSortDirection.Descending)
                    : null;
        }
    }

    /// <summary>
    /// Apply a sort — the unit-testable seam, the mac twin's shape.
    /// Returns the rendered announcement text it posted, or null for
    /// an unsortable column.
    /// </summary>
    internal string? ApplySort(int columnIndex, bool ascending)
    {
        if (columnIndex < 0 || columnIndex >= _columns.Count)
        {
            return null;
        }
        if (ExternalSortHandler is { } externalSort
            && _columns[columnIndex].IsExternallySortable)
        {
            _ = externalSort(columnIndex, ascending);
            return null;
        }
        if (_columns[columnIndex].Sort is not { } comparer)
        {
            return null;
        }
        _activeSort = (columnIndex, ascending);
        // The reader's position survives the sort: re-populating
        // destroys the focused cell's container, and without a restore
        // keyboard focus falls to the window — the NEXT sort chord
        // then routes past this control entirely (measured 2026-08-01:
        // chord one announced ascending, chord two vanished).
        object? currentItem = _grid.CurrentCell.Item;
        DataGridColumn? currentColumn = _grid.CurrentCell.Column;
        bool hadFocus = _grid.IsKeyboardFocusWithin;
        var sorted = _items
            .OrderBy(row => row, new DirectionalComparer(comparer, ascending))
            .ToList();
        _items.Clear();
        foreach (object row in sorted)
        {
            _items.Add(row);
        }
        foreach (DataGridColumn gridColumn in _grid.Columns)
        {
            gridColumn.SortDirection =
                ReferenceEquals(gridColumn, _grid.Columns[columnIndex])
                    ? (ascending
                        ? System.ComponentModel.ListSortDirection.Ascending
                        : System.ComponentModel.ListSortDirection.Descending)
                    : null;
        }
        // Publish the reordered tree NOW: the UIA bridge pushes
        // structure updates at the end of a layout pass, and a starved
        // session defers layout indefinitely — an AT (and the gate)
        // would keep reading the pre-sort rows (measured 2026-08-01:
        // GridSorted announced, row 0 stale for the full wait).
        if (_grid.IsLoaded)
        {
            _grid.UpdateLayout();
        }
        if (currentItem is not null
            && currentItem != DependencyProperty.UnsetValue
            && _items.Contains(currentItem))
        {
            _lastAnnouncedRow = currentItem;
            DataGridColumn column = currentColumn ?? _grid.Columns[columnIndex];
            if (hadFocus)
            {
                // The CELL element, not the grid (round 4): currency
                // is metadata, and a grid-level Focus() after the
                // container was destroyed announces the grid — the
                // reader's cell position is what must survive a sort.
                _ = FocusCellElement(currentItem, column);
            }
            else
            {
                _grid.CurrentCell = new DataGridCellInfo(currentItem, column);
                _grid.SelectedCells.Clear();
                _grid.SelectedCells.Add(_grid.CurrentCell);
            }
        }
        var @event = new A11yEvent.GridSorted(_columns[columnIndex].Header, ascending);
        Announce(@event);
        return SlateUniffiMethods.A11yRender(@event).Text;
    }

    private void ToggleSortOnCurrentColumn()
    {
        int columnIndex = CurrentColumnIndex();
        if (columnIndex < 0)
        {
            return;
        }
        bool ascending = _activeSort is not { } sort
            || sort.ColumnIndex != columnIndex
            || !sort.Ascending;
        ApplySort(columnIndex, ascending);
    }

    private void OnHeaderSorting(object sender, DataGridSortingEventArgs e)
    {
        // Route header clicks through the SAME seam as the keyboard
        // command so both paths sort and announce identically.
        e.Handled = true;
        if (e.Column is AccessibleGridTextColumn column)
        {
            bool ascending = _activeSort is not { } sort
                || sort.ColumnIndex != column.ColumnIndex
                || !sort.Ascending;
            ApplySort(column.ColumnIndex, ascending);
        }
    }

    /// <summary>The current ROW for consuming surfaces (W4-6 row
    /// commands target it); null when currency leaves the bound set.</summary>
    internal event Action<object?>? CurrentRowChanged;

    private void OnCurrentCellChanged(object? sender, EventArgs e)
    {
        CurrentRowChanged?.Invoke(
            _grid.CurrentCell.Item is { } current
            && current != CollectionView.NewItemPlaceholder
            && _items.Contains(current)
                ? current
                : null);
        if (_grid.CurrentCell.Item is not object row
            || row == CollectionView.NewItemPlaceholder
            || _grid.CurrentCell.Column is not AccessibleGridTextColumn column)
        {
            return;
        }
        string focusedCell =
            $"{_columns[column.ColumnIndex].Header}: {_columns[column.ColumnIndex].Cell(row)}";
        bool movedToDifferentRow = !ReferenceEquals(row, _lastAnnouncedRow);
        _lastAnnouncedRow = row;
        if (movedToDifferentRow)
        {
            // Mac parity: a row move ALWAYS posts GridRowMoved — core
            // renders an empty description as the focused cell alone.
            Announce(new A11yEvent.GridRowMoved(
                _rowAudioDescription?.Invoke(row) ?? string.Empty, focusedCell));
        }
        else
        {
            Announce(new A11yEvent.GridCellMoved(
                _columns[column.ColumnIndex].Header,
                _columns[column.ColumnIndex].Cell(row)));
        }
    }

    /// <summary>
    /// Enter or double-click on the focused row.
    ///
    /// The substrate had no activation mechanism at all, which is why
    /// W4-5's bibliography entries could not be expanded: mac makes
    /// every entry row a Button that opens the citation popover, and on
    /// Windows there was simply no way in — the row even advertised
    /// "Activate to expand citation fields." Row ACTIONS (the context
    /// menu) are not a substitute; Enter is the primary affordance a
    /// keyboard user reaches for. Lives here rather than in the
    /// citations code because a second implementation of row activation
    /// is exactly what the grid-conformance contract forbids (D-12).
    /// </summary>
    private void OnActivationKey(object sender, KeyEventArgs e)
    {
        // W4-6 (#738, contract C7): editing outranks activation on an
        // EDITABLE cell — Enter or F2 begins the edit; Enter on a
        // non-editable cell keeps its activation meaning, while F2
        // (the edit-only key) surfaces the refusal so the reason is
        // spoken, not swallowed.
        if (e.Key is Key.Enter or Key.F2 && _editDraft is not null && !_editing)
        {
            int editColumn = CurrentColumnIndex();
            if (_grid.CurrentCell.Item is { } editItem
                && _items.Contains(editItem)
                && editColumn >= 0)
            {
                if (_editDraft(editItem, editColumn) is not null)
                {
                    _ = BeginEditAt(editItem, editColumn);
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.F2)
                {
                    _editRefused?.Invoke(editItem, editColumn);
                    e.Handled = true;
                    return;
                }
            }
        }
        if (e.Key != Key.Enter || _rowActivated is null)
        {
            return;
        }
        if (_grid.CurrentCell.Item is { } item && _items.Contains(item))
        {
            _rowActivated(item);
            e.Handled = true;
        }
    }

    // --- W4-6 in-grid editing (contract C7/C8): the substrate owns
    // the EDITOR LIFECYCLE only — begin, keystroke routing, commit
    // text handoff, cancel. The surface owns the policy (which cells
    // edit, what the draft is) and the write. The DataGrid never
    // writes data itself: commit hands the editor TEXT to the surface
    // and cancels the native edit, so a binding can never race the
    // FFI write path. ---

    private Func<object, int, string?>? _editDraft;
    private Action<object, int, string, GridEditCommitNavigation>? _editCommit;
    private Action? _editCancel;
    private Action<object, int>? _editRefused;
    private bool _editing;
    private bool _committing;
    private TextBox? _activeEditor;

    /// <summary>Configure the edit seam. A null return from
    /// <paramref name="editDraft"/> marks that cell read-only — F2
    /// routes the refusal to <paramref name="editRefused"/> so the
    /// surface can announce WHY (contract C7).</summary>
    internal void ConfigureEditing(
        Func<object, int, string?> editDraft,
        Action<object, int, string, GridEditCommitNavigation> editCommit,
        Action editCancel,
        Action<object, int> editRefused)
    {
        _editDraft = editDraft;
        _editCommit = editCommit;
        _editCancel = editCancel;
        _editRefused = editRefused;
    }

    internal bool IsEditingForTests => _editing;

    internal int CurrentColumnIndexForTests() => CurrentColumnIndex();

    internal object? CurrentRowForTests() =>
        _grid.CurrentCell.Item is { } item
        && item != CollectionView.NewItemPlaceholder
        && _items.Contains(item)
            ? item
            : null;

    internal object? FirstItemForTests() => _items.Count > 0 ? _items[0] : null;

    /// <summary>Begin editing a cell (keyboard, row action, or the
    /// editProperty command). Returns false when the cell is
    /// read-only (after routing the refusal) or unreachable.</summary>
    internal bool BeginEditAt(object row, int columnIndex, string? draftOverride = null)
    {
        if (_editDraft is null
            || _editing
            || columnIndex < 0
            || columnIndex >= _grid.Columns.Count
            || !_items.Contains(row))
        {
            return false;
        }
        if (_editDraft(row, columnIndex) is not { } draft)
        {
            _editRefused?.Invoke(row, columnIndex);
            return false;
        }
        // A validation failure re-arms with the USER'S text, not the
        // stored value — the draft is never lost (contract C7).
        _pendingDraft = draftOverride ?? draft;
        _editing = true;
        _grid.IsReadOnly = false;
        _grid.CurrentCell = new DataGridCellInfo(row, _grid.Columns[columnIndex]);
        _grid.SelectedCells.Clear();
        _grid.SelectedCells.Add(_grid.CurrentCell);
        if (!_grid.BeginEdit())
        {
            _editing = false;
            _grid.IsReadOnly = true;
            _pendingDraft = null;
            return false;
        }
        return true;
    }

    private string? _pendingDraft;

    /// <summary>Called by the editing column: the editor arrives
    /// seeded with the surface's draft, wired for the mac keys —
    /// Return commit-stay, Tab commit-next, Shift+Tab commit-previous,
    /// Escape cancel.</summary>
    internal void AttachCellEditor(TextBox editor, string header)
    {
        _activeEditor = editor;
        editor.Text = _pendingDraft ?? string.Empty;
        _pendingDraft = null;
        AutomationProperties.SetName(editor, $"{header} edit");
        editor.Loaded += (_, _) =>
        {
            _ = editor.Focus();
            editor.SelectAll();
        };
        editor.PreviewKeyDown += OnEditorKeyDown;
    }

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox editor)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Enter:
                CommitEdit(editor.Text, GridEditCommitNavigation.Stay);
                e.Handled = true;
                break;
            case Key.Tab:
                CommitEdit(
                    editor.Text,
                    (Keyboard.Modifiers & ModifierKeys.Shift) != 0
                        ? GridEditCommitNavigation.Previous
                        : GridEditCommitNavigation.Next);
                e.Handled = true;
                break;
            case Key.Escape:
                // Native cancel; CellEditEnding routes to _editCancel.
                break;
        }
    }

    private void CommitEdit(string text, GridEditCommitNavigation navigation)
    {
        if (!_editing)
        {
            return;
        }
        object? item = _grid.CurrentCell.Item;
        int columnIndex = CurrentColumnIndex();
        _committing = true;
        try
        {
            _grid.CancelEdit();
        }
        finally
        {
            _committing = false;
        }
        EndEditCore();
        if (item is not null && columnIndex >= 0)
        {
            _editCommit?.Invoke(item, columnIndex, text, navigation);
        }
    }

    /// <summary>Post-commit selection movement (the mac
    /// moveAfterEditCommit twin): the grid is a linear
    /// row*columnCount+column sequence, clamped at both ends.</summary>
    internal void MoveCurrentCell(GridEditCommitNavigation navigation)
    {
        if (navigation == GridEditCommitNavigation.Stay
            || _grid.CurrentCell.Item is not { } item
            || !_items.Contains(item)
            || _grid.Columns.Count == 0)
        {
            return;
        }
        int columnCount = _grid.Columns.Count;
        int rowIndex = _items.IndexOf(item);
        int columnIndex = Math.Max(0, CurrentColumnIndex());
        int linear = (rowIndex * columnCount) + columnIndex
            + (navigation == GridEditCommitNavigation.Next ? 1 : -1);
        linear = Math.Clamp(linear, 0, (_items.Count * columnCount) - 1);
        object targetRow = _items[linear / columnCount];
        DataGridColumn targetColumn = _grid.Columns[linear % columnCount];
        _grid.CurrentCell = new DataGridCellInfo(targetRow, targetColumn);
        _grid.SelectedCells.Clear();
        _grid.SelectedCells.Add(_grid.CurrentCell);
        // Keyboard focus FOLLOWS the moved currency (red team round 1:
        // EndEditCore had refocused the committed cell, so a Tab
        // commit left the reader speaking one cell while currency —
        // and the next edit — sat on another).
        if (_grid.IsKeyboardFocusWithin)
        {
            _ = FocusCellElement(targetRow, targetColumn);
        }
    }

    private void OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        // EVERY native ending that is not our programmatic
        // commit-cancel handoff ends the session as a cancel. The
        // draft-based columns bind nothing, so a native COMMIT (click
        // away) discards the draft exactly like Escape — and the first
        // cut's Cancel-only guard leaked `_editing=true` +
        // `IsReadOnly=false` forever after one click-away (red team
        // round 1: every later F2 refused, and native sessions opened
        // on ANY cell bypassing the edit policy).
        if (_editing && !_committing)
        {
            EndEditCore();
            _editCancel?.Invoke();
        }
    }

    private void EndEditCore()
    {
        _editing = false;
        _grid.IsReadOnly = true;
        if (_activeEditor is { } editor)
        {
            editor.PreviewKeyDown -= OnEditorKeyDown;
            _activeEditor = null;
        }
        // Focus returns to the CELL, not the grid (the reader's
        // position is what must survive an edit — the sort-restore
        // precedent).
        if (_grid.CurrentCell.Item is { } item
            && _grid.CurrentCell.Column is { } column)
        {
            _ = FocusCellElement(item, column);
        }
    }

    private void OnActivationDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_rowActivated is null)
        {
            return;
        }
        // HIT-TEST what was actually double-clicked. MouseDoubleClick
        // fires for the whole control, so acting on CurrentCell.Item
        // meant a double-click on a column HEADER — the ordinary way to
        // sort, which OnHeaderSorting also handles — additionally
        // activated whichever row happened to be current, opening a
        // focus-trapping dialog the user never asked for. Same for the
        // empty chrome below the last row and the scrollbar gutter.
        //
        // TargetRowActionsAt is the guard the context menu already uses
        // for exactly this ("a pointer request that resolves to NO row
        // gets no menu at all"); reusing it rather than writing a
        // second hit-test is the point.
        if (e.OriginalSource is not DependencyObject origin
            || !TargetRowActionsAt(origin))
        {
            return;
        }
        if (_grid.CurrentCell.Item is { } item && _items.Contains(item))
        {
            _rowActivated(item);
            e.Handled = true;
        }
    }

    private void OnTypeAhead(object sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0]))
        {
            return;
        }
        e.Handled = TypeAhead(e.Text);
    }

    /// <summary>Type-ahead seam: appends to the 1s rolling buffer and
    /// selects the first row whose FIRST-column text matches.</summary>
    internal bool TypeAhead(string text)
    {
        if (_columns.Count == 0)
        {
            return false;
        }
        DateTime now = DateTime.UtcNow;
        _typeAheadBuffer = now - _typeAheadLast > TimeSpan.FromSeconds(1)
            ? text
            : _typeAheadBuffer + text;
        _typeAheadLast = now;
        Func<object, string> firstColumn = _columns[0].Cell;
        object? match = _items.FirstOrDefault(row =>
            firstColumn(row).StartsWith(
                _typeAheadBuffer, StringComparison.CurrentCultureIgnoreCase));
        if (match is null)
        {
            return false;
        }
        // The realized CELL, not just currency + scroll (round 9):
        // currency is metadata, and after a distant virtualized jump
        // the reader would stay on the old cell — or fall back to the
        // grid — while SelectionPattern reported success.
        _ = FocusCellElement(match, _grid.Columns[0]);
        return true;
    }

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // A POINTER-invoked menu targets the CLICKED row (rounds 5-6):
        // WPF moves currency only on the left-button path, so a
        // right-click over row B — its cells OR its row header —
        // would otherwise build a menu that executes, destructively
        // for real actions, against row A. A pointer request that
        // resolves to NO row (column headers, scrollbars, empty
        // chrome) gets no menu at all. The keyboard path (Menu /
        // Shift+F10, cursor coordinates -1) keeps the current cell.
        bool pointerRequest = e.CursorLeft >= 0 || e.CursorTop >= 0;
        if (pointerRequest
            && (e.OriginalSource is not DependencyObject origin
                || !TargetRowActionsAt(origin)))
        {
            e.Handled = true;
            return;
        }
        // The persistent menu is MUTATED, never replaced: WPF decides
        // whether to open based on the menu that exists when the
        // request arrives, and a menu first assigned inside this event
        // is documented as too late for the initiating request — the
        // first Menu-key press on a fresh grid showed nothing
        // (adversarial round 4; the CI runner reproduced it every
        // time, local timing masked it).
        ContextMenu? built = BuildRowActionsMenu();
        if (built is null)
        {
            e.Handled = true;
            return;
        }
        _persistentMenu.Items.Clear();
        while (built.Items.Count > 0)
        {
            object item = built.Items[0];
            built.Items.RemoveAt(0);
            _ = _persistentMenu.Items.Add(item);
        }
    }

    /// <summary>Make the row the context-menu request ORIGINATED from
    /// current and focused — the row a pointer-invoked action must
    /// execute against. Recognizes a cell OR the row itself (a row-
    /// header click walks up through DataGridRow, never a cell —
    /// round 6). False when the origin is inside neither: column
    /// headers, scrollbars, empty chrome have no row to act on.</summary>
    internal bool TargetRowActionsAt(DependencyObject origin)
    {
        DependencyObject? current = origin;
        while (current is not null and not DataGridCell and not DataGridRow)
        {
            current = current is System.Windows.Media.Visual
                or System.Windows.Media.Media3D.Visual3D
                    ? System.Windows.Media.VisualTreeHelper.GetParent(current)
                    : LogicalTreeHelper.GetParent(current);
        }
        switch (current)
        {
            case DataGridCell cell
                when cell.DataContext is { } clickedRow
                    && clickedRow != CollectionView.NewItemPlaceholder
                    && cell.Column is { } column:
                _grid.CurrentCell = new DataGridCellInfo(clickedRow, column);
                _grid.SelectedCells.Clear();
                _grid.SelectedCells.Add(_grid.CurrentCell);
                _ = cell.Focus();
                return true;
            case DataGridRow rowElement
                when rowElement.Item is { } clickedRow
                    && clickedRow != CollectionView.NewItemPlaceholder
                    && (_grid.CurrentCell.Column
                        ?? _grid.Columns.FirstOrDefault()) is { } column:
                _ = FocusCellElement(clickedRow, column);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Row-actions menu seam (Menu key / Shift+F10): visible
    /// actions only; disabled-but-relevant actions retain their reason
    /// as HelpText (the mac RowAction contract).</summary>
    internal ContextMenu? BuildRowActionsMenu()
    {
        object? row = _grid.CurrentCell.Item;
        if (row is null || row == CollectionView.NewItemPlaceholder || _rowActions.Count == 0)
        {
            return null;
        }
        var menu = new ContextMenu();
        foreach (AccessibleGridRowAction action in _rowActions)
        {
            if (!action.IsVisible(row))
            {
                continue;
            }
            var item = new MenuItem { Header = action.Name };
            if (action.IsEnabled(row))
            {
                object target = row;
                item.Click += (_, _) => action.Execute(target);
            }
            else
            {
                item.IsEnabled = false;
                // Context menus retain a temporarily unavailable
                // relevant action WITH its reason (the mac RowAction
                // contract; AX custom actions have no disabled state,
                // menus do).
                if (action.DisabledReason is { Length: > 0 } reason)
                {
                    item.ToolTip = reason;
                    AutomationProperties.SetHelpText(item, reason);
                }
            }
            menu.Items.Add(item);
        }
        return menu;
    }

    private void ProduceExport(ExportFormat format)
    {
        if (_exportProducer is null)
        {
            return;
        }
        ExportProduced?.Invoke(format, _exportProducer(format));
    }

    private int CurrentColumnIndex() =>
        _grid.CurrentCell.Column is AccessibleGridTextColumn column ? column.ColumnIndex : -1;

    private sealed class DirectionalComparer : IComparer<object>
    {
        private readonly IComparer<object> _inner;
        private readonly bool _ascending;

        public DirectionalComparer(IComparer<object> inner, bool ascending)
        {
            _inner = inner;
            _ascending = ascending;
        }

        public int Compare(object? x, object? y)
        {
            int result = _inner.Compare(x!, y!);
            return _ascending ? result : -result;
        }
    }
}

/// <summary>The commit navigation the mac field editor promises:
/// Return stays, Tab moves right/down, Shift+Tab left/up — the
/// SURFACE moves selection after a successful write.</summary>
internal enum GridEditCommitNavigation
{
    Stay,
    Next,
    Previous,
}

/// <summary>A substrate column: text cells generated from the column's
/// accessor, carrying the mac "Header: value" cell-label contract and
/// the optional accessibility hint as HelpText.</summary>
internal sealed class AccessibleGridTextColumn : DataGridTextColumn
{
    private readonly AccessibleGridColumn _column;
    private readonly AccessibleDataGrid? _owner;

    public AccessibleGridTextColumn(
        AccessibleGridColumn column, int columnIndex, AccessibleDataGrid? owner = null)
    {
        _column = column;
        ColumnIndex = columnIndex;
        _owner = owner;
    }

    public int ColumnIndex { get; }

    /// <summary>W4-6 in-grid editing: the editor is a plain TextBox
    /// seeded and wired by the owning grid — the DataGrid's own
    /// binding pipeline is never engaged (commit is a text handoff,
    /// not a binding write).</summary>
    protected override FrameworkElement GenerateEditingElement(
        DataGridCell cell, object dataItem)
    {
        var editor = new TextBox
        {
            Margin = new Thickness(4, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _owner?.AttachCellEditor(editor, _column.Header);
        return editor;
    }

    /// <summary>Test seam over the protected generator.</summary>
    internal FrameworkElement TestGenerateElement(DataGridCell cell, object dataItem) =>
        GenerateElement(cell, dataItem);

    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
    {
        var text = new TextBlock
        {
            Text = _column.Cell(dataItem),
            Margin = new Thickness(8, 2, 8, 2),
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "Slate.TextBrush");
        AutomationProperties.SetName(cell, $"{_column.Header}: {_column.Cell(dataItem)}");
        if (_column.AccessibilityHint?.Invoke(dataItem) is { Length: > 0 } hint)
        {
            AutomationProperties.SetHelpText(cell, hint);
        }
        return text;
    }
}

/// <summary>A substrate column model (the mac Column twin).</summary>
internal sealed class AccessibleGridColumn
{
    public required string Header { get; init; }

    public required Func<object, string> Cell { get; init; }

    /// <summary>Typed comparator — null means unsortable. Sorting
    /// always runs through the comparator, never a member path.</summary>
    public IComparer<object>? Sort { get; init; }

    public Func<object, string?>? AccessibilityHint { get; init; }

    /// <summary>The core <c>ColumnRole::Primary</c> twin: this column's
    /// value becomes each row's native DataGrid row header — the UIA
    /// Table pattern's row identity (§8.7 "headers on entry"). At most
    /// one column should carry it; the first marked one wins.</summary>
    public bool IsRowHeader { get; init; }

    /// <summary>W4-6 (#738, contract C1/C6): the column sorts, but the
    /// ORDERING lives in core, not in a host comparator. Honored only
    /// when the grid has an <see cref="AccessibleDataGrid.ExternalSortHandler"/>;
    /// meaningless (and ignored) alongside <see cref="Sort"/>.</summary>
    public bool IsExternallySortable { get; init; }
}

/// <summary>A named row-level action (the mac RowAction twin): context
/// menus retain disabled-but-relevant actions with their reason.</summary>
internal sealed class AccessibleGridRowAction
{
    public required string Name { get; init; }

    public required Action<object> Execute { get; init; }

    public Func<object, bool> IsVisible { get; init; } = _ => true;

    public Func<object, bool> IsEnabled { get; init; } = _ => true;

    public string? DisabledReason { get; init; }
}
