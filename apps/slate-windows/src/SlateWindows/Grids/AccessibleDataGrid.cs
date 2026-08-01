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
        _grid.Sorting += OnHeaderSorting;
        _grid.PreviewTextInput += OnTypeAhead;
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
        Func<ExportFormat, string>? exportProducer = null)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        _columns = columns;
        _rowAudioDescription = rowAudioDescription;
        _rowActions = rowActions ?? Array.Empty<AccessibleGridRowAction>();
        _exportProducer = exportProducer;
        _activeSort = null;
        _lastAnnouncedRow = null;

        _grid.Columns.Clear();
        for (int index = 0; index < columns.Count; index++)
        {
            AccessibleGridColumn column = columns[index];
            var gridColumn = new AccessibleGridTextColumn(column, index)
            {
                Header = column.Header,
                CanUserSort = column.Sort is not null,
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
    }

    /// <summary>
    /// Apply a sort — the unit-testable seam, the mac twin's shape.
    /// Returns the rendered announcement text it posted, or null for
    /// an unsortable column.
    /// </summary>
    internal string? ApplySort(int columnIndex, bool ascending)
    {
        if (columnIndex < 0
            || columnIndex >= _columns.Count
            || _columns[columnIndex].Sort is not { } comparer)
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

    private void OnCurrentCellChanged(object? sender, EventArgs e)
    {
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
        _grid.CurrentCell = new DataGridCellInfo(match, _grid.Columns[0]);
        _grid.SelectedCells.Clear();
        _grid.SelectedCells.Add(_grid.CurrentCell);
        _grid.ScrollIntoView(match);
        return true;
    }

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
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

/// <summary>A substrate column: text cells generated from the column's
/// accessor, carrying the mac "Header: value" cell-label contract and
/// the optional accessibility hint as HelpText.</summary>
internal sealed class AccessibleGridTextColumn : DataGridTextColumn
{
    private readonly AccessibleGridColumn _column;

    public AccessibleGridTextColumn(AccessibleGridColumn column, int columnIndex)
    {
        _column = column;
        ColumnIndex = columnIndex;
    }

    public int ColumnIndex { get; }

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
