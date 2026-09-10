// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR A (#746), contracts A-5..A-9: the graph table — a CONFIGURATION
/// of the W4-1 <see cref="AccessibleDataGrid"/> substrate, the mac
/// <c>GraphTableView</c> twin. Core's columns in core's order from the
/// fetched vector (nothing typed here), the rows as core's records, the
/// external sort as a rows-only token, <c>GridSorted</c> relayed once on
/// adoption, the row's UIA Name as P1's copy, the kind on ItemStatus, the
/// shared selection revalidated by the document and re-seated here under
/// a syncing guard, and the row actions as core's vectors.
/// </summary>
internal sealed class GraphTableView : UserControl
{
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(
            nameof(Model),
            typeof(GraphDocumentViewModel),
            typeof(GraphTableView),
            new PropertyMetadata(null, OnModelChanged));

    /// <summary>The mac's grid label, verbatim (contract A-7).</summary>
    internal const string GridLabel = "Graph, data grid";

    private readonly AccessibleDataGrid _grid;
    private bool _syncingSelection;
    private bool _observing;

    public GraphTableView()
    {
        _grid = new AccessibleDataGrid
        {
            GridAutomationId = "GraphTableGrid",
            // Silent until the document attaches (the canvas table's rule):
            // the graph's grid speaks only through the graph relay.
            Announce = _ => { },
            ExternalSortHandler = OnExternalSort,
        };
        _grid.CurrentRowChanged += OnCurrentRowChanged;
        // C-5: the grid's Ctrl+F reaches the field with no new row (C-D2)
        // — the canvas table's line, routed through the navigator to the
        // presenter that has the keys.
        _grid.FilterRequested += () => Model?.Navigator?.FocusFilterField();
        Content = _grid;
        // IPG-12: out of the tree, out of the document's subscriber list;
        // back in the tree, re-observed and re-bound from the record as it
        // is now (a reparented template keeps the same Model, so the
        // property-changed route never fires for it).
        Unloaded += (_, _) => StopObservingModel();
        Loaded += (_, _) => ObserveModel(Model);
        // C-5's Tab order: the surface scopes its header's indices LOCALLY
        // and the wrapper numbers its own two stops 0 (the grid) and 1 (the
        // summary) — so this view is a local scope of its own, ONE unit at
        // the surface's index 5. Without it the wrapper's 0 and 1 flatten
        // into the surface's order, and Shift+Tab from the switcher reached
        // the grid's summary, never the field (the journey's finding, TGC-9).
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Local);
        // And the grid is ONE unit inside that scope, at the wrapper's index
        // 0 ahead of its summary's 1: WPF's DataGrid is a Continue container,
        // so without a mode of its own its cells flatten into the scope at
        // the default index — behind the summary — and Shift+Tab from the
        // first cell reached the summary, not the switcher. Local keeps Tab
        // moving cell to cell inside the grid and leaves it at the edges.
        KeyboardNavigation.SetTabNavigation(_grid.Grid, KeyboardNavigationMode.Local);
    }

    public GraphDocumentViewModel? Model
    {
        get => (GraphDocumentViewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    internal AccessibleDataGrid GridForTests => _grid;

    /// <summary>Term F2's container realisation: the grid's own event,
    /// forwarded so the surface can re-ask a landing once rows exist.</summary>
    internal event Action? ContainersRealized
    {
        add => _grid.ContainersRealized += value;
        remove => _grid.ContainersRealized -= value;
    }

    private static void OnModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var view = (GraphTableView)sender;
        if (e.OldValue is GraphDocumentViewModel old)
        {
            view.StopObservingModel(old);
        }
        if (e.NewValue is GraphDocumentViewModel model)
        {
            view.ObserveModel(model);
        }
        else
        {
            // No model (the tab replaced in place, the surface detached):
            // the grid drops the rows and every delegate that captured the
            // old document beside going silent — a retired document must
            // not stay reachable through a bound grid (codoki on
            // 1de19b4; the null arm IPA-1 made reachable).
            view._grid.Announce = _ => { };
            view._grid.Bind([], [], summary: string.Empty, accessibilityLabel: GridLabel);
        }
    }

    private void OnPublicationInstalled(GraphPublicationInstall install)
    {
        if (Model is not { } model)
        {
            return;
        }
        Rebind(model, install.Current);
        // Contract A-5: the sort speaks ONCE, on adoption — when the
        // publication answered a sort request and the accepted sort
        // changed; a rejected, failed or superseded request says nothing.
        if (install.AnsweredSortRequest && install.Current.AcceptedSort != install.Previous.AcceptedSort)
        {
            int index = model.CellIndexOf(install.Current.AcceptedSort.Column);
            model.RelayGridEvent(new A11yEvent.GridSorted(
                model.ColumnSpecs[index].Header, install.Current.AcceptedSort.Ascending));
        }
    }

    /// <summary>C-9's RE-LABEL: a verbosity change re-binds the current
    /// publication under the syncing guard — the rows' Names at the new
    /// level, no load, no post.</summary>
    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GraphDocumentViewModel.Verbosity) && Model is { } model)
        {
            Rebind(model, model.Publication);
        }
    }

    private void OnViewStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GraphViewState.SelectedKey) && !_syncingSelection && Model is { } model)
        {
            Reseat(model);
        }
    }

    /// <summary>Bind the publication: columns from the fetched vector,
    /// the records as rows, the summary verbatim, the seams; then re-seat
    /// the shared selection and re-assert the accepted sort's indicator.</summary>
    private void Rebind(GraphDocumentViewModel model, GraphPublication publication)
    {
        var columns = new List<AccessibleGridColumn>(model.ColumnSpecs.Count);
        for (int index = 0; index < model.ColumnSpecs.Count; index++)
        {
            GraphTableColumnSpec spec = model.ColumnSpecs[index];
            int vectorIndex = index;
            columns.Add(new AccessibleGridColumn
            {
                Header = spec.Header,
                Cell = row => model.CellAt((GraphTableRow)row, vectorIndex),
                IsRowHeader = spec.Column == GraphTableColumn.Note,
                IsExternallySortable = true,
                AccessibilityHint = row =>
                    ((GraphTableRow)row).Kind == GraphNodeKind.Ghost
                        ? model.ActionDisabledReason(GraphRowAction.CreateNote)
                        : null,
            });
        }
        IReadOnlyList<object> rows = publication.State == GraphLoadState.Error
            ? []
            : publication.Rows.Cast<object>().ToArray();
        _syncingSelection = true;
        try
        {
            _grid.Bind(
                columns,
                rows,
                summary: publication.Summary,
                accessibilityLabel: GridLabel,
                rowAudioDescription: row => model.RowName((GraphTableRow)row),
                rowActions: RowActions(model),
                exportProducer: null,
                rowActivated: row => model.Activate((GraphTableRow)row, modified: false),
                rowAutomationName: row => model.RowName((GraphTableRow)row),
                rowItemStatus: row => model.CellOf((GraphTableRow)row, GraphTableColumn.Kind),
                rowActivatedModified: row => model.Activate((GraphTableRow)row, modified: true));
            _grid.SetSortIndicator((model.CellIndexOf(publication.AcceptedSort.Column), publication.AcceptedSort.Ascending));
            Reseat(model);
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    /// <summary>Contract A-7: seat the grid on the row whose key equals
    /// the shared selection; with no visible row for it, clear the grid's
    /// currency WITHOUT writing the key.</summary>
    /// <summary>Rule F, Terms F4 and F5: seat the reader on the grid's
    /// current row — the shared key's, else the first — SILENTLY: the
    /// syncing guard writes no key and the grid posts no row move. False
    /// when no realised cell took the keys.</summary>
    internal bool FocusProjection()
    {
        if (Model is not { } model)
        {
            return false;
        }
        bool wasSyncing = _syncingSelection;
        _syncingSelection = true;
        try
        {
            string? key = model.ViewState.SelectedKey;
            if (key is not null
                && _grid.SelectRow(row => string.Equals(((GraphTableRow)row).StableKey, key, StringComparison.Ordinal), moveFocus: true))
            {
                return true;
            }
            return _grid.SelectRow(_ => true, moveFocus: true);
        }
        finally
        {
            _syncingSelection = wasSyncing;
        }
    }

    private void Reseat(GraphDocumentViewModel model)
    {
        bool wasSyncing = _syncingSelection;
        _syncingSelection = true;
        try
        {
            string? key = model.ViewState.SelectedKey;
            if (key is null)
            {
                // No shared key: rule F's silent seat wrote none (Term F5) and
                // the wrapper's rebind restored the reader's row by identity.
                // Clearing the currency here stranded the reader on a cell
                // that was no longer current — Enter refused, the readback
                // empty — every time the table re-published under no key
                // (the table journey's finding, TGC-9). A row the republish
                // dropped is the one currency to clear: the wrapper leaves a
                // gone row's stale cell in place, and its old column object
                // would index at -1 on the next seat.
                if (_grid.Grid.CurrentCell.Item is { } item && !_grid.Grid.Items.Contains(item))
                {
                    _grid.Grid.CurrentCell = new System.Windows.Controls.DataGridCellInfo();
                }
                return;
            }
            if (!_grid.SelectRow(row => string.Equals(((GraphTableRow)row).StableKey, key, StringComparison.Ordinal)))
            {
                // No visible row carries the key: clear the grid's currency
                // WITHOUT writing the key (contract A-7).
                _grid.Grid.CurrentCell = new System.Windows.Controls.DataGridCellInfo();
            }
        }
        finally
        {
            _syncingSelection = wasSyncing;
        }
    }

    /// <summary>The three subscriptions this view holds on the document and
    /// the workspace's view state, in ONE place (IPG-12): taken with a model
    /// and on load, dropped on a replacement and on UNLOAD — the document
    /// and the view state outlive the element, so a table left subscribed
    /// after it leaves the tree renders every later publication into a grid
    /// nobody can see, one more of them per split collapse.</summary>
    private void ObserveModel(GraphDocumentViewModel? model)
    {
        if (model is null || _observing)
        {
            return;
        }
        _observing = true;
        model.PublicationInstalled += OnPublicationInstalled;
        model.PropertyChanged += OnModelPropertyChanged;
        model.ViewState.PropertyChanged += OnViewStateChanged;
        _grid.Announce = model.GridRelaySeam;
        Rebind(model, model.Publication);
    }

    private void StopObservingModel(GraphDocumentViewModel? model = null)
    {
        GraphDocumentViewModel? observed = model ?? Model;
        if (observed is null || !_observing)
        {
            return;
        }
        _observing = false;
        observed.PublicationInstalled -= OnPublicationInstalled;
        observed.PropertyChanged -= OnModelPropertyChanged;
        observed.ViewState.PropertyChanged -= OnViewStateChanged;
    }

    internal bool ObservingForTests => _observing;

    private void OnCurrentRowChanged(object? row)
    {
        if (_syncingSelection || Model is not { } model)
        {
            return;
        }
        if (row is GraphTableRow current)
        {
            // The view holds no write of its own (W6-2 PR B2, Term 15,
            // IGJ-6): the DOCUMENT's guarded selection refuses a retired or
            // unseated document and a key its current snapshot lacks, so a
            // retained view over a closed tab cannot move the workspace's
            // state.
            _ = model.SelectRow(current.StableKey);
        }
    }

    private bool OnExternalSort(int columnIndex, bool ascending)
    {
        if (Model is not { } model || columnIndex < 0 || columnIndex >= model.ColumnSpecs.Count)
        {
            return false;
        }
        // A-5's rows-only token through rule Q's one entry (Term Q1): the
        // table view's external sort handler is Request's named caller.
        _ = model.Request(new GraphRequest.Sort(new GraphTableSort(model.ColumnSpecs[columnIndex].Column, ascending)));
        return true;
    }

    /// <summary>The ONE list the grid takes (contract A-8): the union of
    /// the three per-kind vectors in core's order, each action visible
    /// for a row whose kind's vector carries it.</summary>
    private static IReadOnlyList<AccessibleGridRowAction> RowActions(GraphDocumentViewModel model) =>
        model.ActionUnion()
            .Select(spec => new AccessibleGridRowAction
            {
                Name = spec.Title,
                Execute = row => model.Execute(spec.Action, (GraphTableRow)row),
                IsVisible = row => model.ActionAppliesTo(spec.Action, ((GraphTableRow)row).Kind),
                IsEnabled = row => model.IsActionEnabled(spec.Action, (GraphTableRow)row),
                DisabledReason = model.ActionDisabledReason(spec.Action),
            })
            .ToArray();
}
