// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
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
        Content = _grid;
    }

    public GraphDocumentViewModel? Model
    {
        get => (GraphDocumentViewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    internal AccessibleDataGrid GridForTests => _grid;

    private static void OnModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var view = (GraphTableView)sender;
        if (e.OldValue is GraphDocumentViewModel old)
        {
            old.PublicationInstalled -= view.OnPublicationInstalled;
            old.ViewState.PropertyChanged -= view.OnViewStateChanged;
        }
        if (e.NewValue is GraphDocumentViewModel model)
        {
            model.PublicationInstalled += view.OnPublicationInstalled;
            model.ViewState.PropertyChanged += view.OnViewStateChanged;
            view._grid.Announce = model.GridRelaySeam;
            view.Rebind(model, model.Publication);
        }
        else
        {
            view._grid.Announce = _ => { };
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
    private void Reseat(GraphDocumentViewModel model)
    {
        bool wasSyncing = _syncingSelection;
        _syncingSelection = true;
        try
        {
            string? key = model.ViewState.SelectedKey;
            bool seated = key is not null
                && _grid.SelectRow(row => string.Equals(((GraphTableRow)row).StableKey, key, StringComparison.Ordinal));
            if (!seated)
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

    private void OnCurrentRowChanged(object? row)
    {
        if (_syncingSelection || Model is not { } model)
        {
            return;
        }
        if (row is GraphTableRow current)
        {
            model.ViewState.SelectedKey = current.StableKey;
        }
    }

    private bool OnExternalSort(int columnIndex, bool ascending)
    {
        if (Model is not { } model || columnIndex < 0 || columnIndex >= model.ColumnSpecs.Count)
        {
            return false;
        }
        model.SetSort(new GraphTableSort(model.ColumnSpecs[columnIndex].Column, ascending));
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
