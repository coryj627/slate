// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 PR B (#745): the canvas table projection — every card as a flat,
/// sortable row on the W4-1 <see cref="AccessibleDataGrid"/> substrate,
/// the mac <c>CanvasTableView</c> twin (t4 #363).
///
/// This file is a CONFIGURATION of that substrate, not a second grid
/// (contract B1). Sorting, the reader-position restore, the "Header:
/// value" cell labels, native row headers, type-ahead, the row-actions
/// menu, activation plumbing and the AT-safe virtualization are all the
/// substrate's and are never re-implemented here — the W4-5 D-12 rule
/// that the grid-conformance matrix exists to protect. What IS here:
/// core's rows, the mac column inventory and comparators, the shared
/// selection in both directions, per-kind activation routed through the
/// document's ONE activation seam, and the announce-seam swap that puts
/// the grid's own canonical events on the canvas funnel (contract B7,
/// DoD §H).
/// </summary>
internal sealed class CanvasTableView : UserControl
{
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(
            nameof(Model),
            typeof(CanvasDocumentViewModel),
            typeof(CanvasTableView),
            new PropertyMetadata(null, OnModelChanged));

    private readonly AccessibleDataGrid _grid;

    /// <summary>The model→view direction, so the substrate's currency
    /// change cannot read back as a user selection (contract B5, the
    /// A12 idiom).</summary>
    private bool _syncingSelection;

    public CanvasTableView()
    {
        _grid = new AccessibleDataGrid
        {
            // The shell shows more than one grid, so the substrate's
            // default id would make three surfaces indistinguishable to
            // AT and to a journey (D-12). The summary region takes the
            // same id with a "Summary" suffix.
            GridAutomationId = "CanvasTableGrid",
            // Silent until a document is attached: the substrate's
            // default seam posts straight through the canonical
            // dispatcher, and a canvas grid speaking outside the canvas
            // funnel is exactly what contract A6 forbids. The real seam
            // arrives with the model, below.
            Announce = _ => { },
        };
        _grid.CurrentRowChanged += OnCurrentRowChanged;
        Content = _grid;
    }

    public CanvasDocumentViewModel? Model
    {
        get => (CanvasDocumentViewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>Raised when a text card's read-only detail was published
    /// — the surface moves focus there, exactly as the outline does
    /// (contract A13).</summary>
    internal event Action? DetailRequested;

    internal AccessibleDataGrid GridForTests => _grid;

    /// <summary>
    /// Deliver a focus request to a row (contract A14's table arm).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The seat is SILENT: a landing is not a move the user made, and
    /// the reader is about to read the row they land on, so a grid
    /// row-move announcement on top of it is the t0 §1.5 doubling rule
    /// broken at the first keystroke of the surface's life (A12's
    /// "landing selection is silent", applied to this projection).
    /// </para>
    /// <para>
    /// Delivery is reported only when keyboard focus is actually INSIDE
    /// the grid. The substrate's own bool answers a different question
    /// on purpose — "was the row in the bound set", not "did the OS
    /// grant focus" — and A14's rule is that a surface may not report a
    /// landing it did not make: the outline's tree-focus fallback
    /// returning success is the exact defect that rule came from. A
    /// failed delivery leaves the request pending for the next try.
    /// </para>
    /// </remarks>
    internal bool DeliverFocus(string nodeId) =>
        _grid.SelectRow(row => IsNode(row, nodeId), moveFocus: true)
        && _grid.IsKeyboardFocusWithin;

    private static void OnModelChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (CanvasTableView)d;
        if (e.OldValue is CanvasDocumentViewModel oldModel)
        {
            oldModel.OutlinePublished -= view.OnRowsPublished;
            oldModel.Selection.PropertyChanged -= view.OnSelectionChanged;
        }
        if (e.NewValue is CanvasDocumentViewModel model)
        {
            model.OutlinePublished += view.OnRowsPublished;
            model.Selection.PropertyChanged += view.OnSelectionChanged;
            // Contract B7 (DoD §H): the substrate raises CANONICAL grid
            // events — sort, row move, cell move — and they ride the
            // canvas announcer from here on. `Relay` posts them
            // uncoalesced (they are not canvas vocabulary and have no
            // canvas coalescing class) and carries core's own priority
            // through unwrapped, rather than re-classifying them here.
            view._grid.Announce = model.Announcer.Relay;
        }
        else
        {
            view._grid.Announce = _ => { };
        }
        view.Rebuild();
    }

    private void OnRowsPublished(object? sender, EventArgs e) => Rebuild();

    private void OnSelectionChanged(
        object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CanvasSelection.Selected))
        {
            ApplySelection();
        }
    }

    /// <summary>
    /// One <see cref="AccessibleDataGrid.Bind"/> per publish — core's
    /// table rows, untransformed and in core's order (R-D).
    /// </summary>
    private void Rebuild()
    {
        IReadOnlyList<CanvasTableRow> rows = Model?.TableRows ?? [];
        int cards = 0;
        foreach (CanvasTableRow row in rows)
        {
            if (!IsGroup(row))
            {
                cards++;
            }
        }
        // Two arguments are deliberately NULL, and each has a reason.
        // `rowAudioDescription`: core composes none for a canvas row and
        // mac passes none either, so `GridRowMoved` renders the focused
        // cell alone — the substrate's documented shape for a grid with
        // no engine-authored row context. `exportProducer` (contract
        // B8): no core canvas export exists, and a host-composed one is
        // what the residue census forbids — the `ReadingTableGrid`
        // precedent — so the substrate's export commands stay disabled.
        _grid.Bind(
            Columns(),
            rows.Cast<object>().ToArray(),
            summary: CanvasPhrase.TableSummary(cards, rows.Count - cards),
            accessibilityLabel: CanvasPhrase.TableName,
            rowAudioDescription: null,
            rowActions: RowActions(),
            exportProducer: null,
            rowActivated: ActivateRow);
        ApplySelection();
    }

    /// <summary>
    /// Model → view (contract B5). The whole seat runs under the sync
    /// guard: the substrate raises <c>CurrentRowChanged</c> for a
    /// programmatic seat exactly as it does for an arrow key, and
    /// without the guard a selection another pane made would come back
    /// through <see cref="OnCurrentRowChanged"/> as a fresh user
    /// selection (the A12 echo, one surface over).
    /// </summary>
    private void ApplySelection()
    {
        if (Model?.Selection.Selected is not { } selected)
        {
            return;
        }
        _syncingSelection = true;
        try
        {
            // Silent and focus-free: another pane's move must not speak
            // twice, and a background publish that stole keyboard focus
            // is the W4-6 defect this substrate already records.
            _ = _grid.SelectRow(row => IsNode(row, selected));
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    /// <summary>View → model (contract B5): the row the reader is on IS
    /// the canvas selection, so the document's ONE narrating mutation
    /// runs — the same call the outline makes, which is why both
    /// surfaces speak one grammar.</summary>
    private void OnCurrentRowChanged(object? row)
    {
        // A null is currency LEAVING the bound set during a rebind, not
        // the user deselecting: the canvas has no "nothing selected"
        // gesture, and clearing here would drop the target of every
        // selection-scoped verb mid-publish.
        if (_syncingSelection || Model is not { } model || row is not CanvasTableRow bound)
        {
            return;
        }
        model.SelectNode(bound.NodeId);
    }

    /// <summary>
    /// Activation per kind, through the document's one seam (contract
    /// A13) — the mac shape exactly: the table looks its row up in the
    /// outline and calls the SAME activate the outline calls, so there
    /// is one activation semantic per kind across surfaces, media gate
    /// and all.
    /// </summary>
    private void ActivateRow(object row)
    {
        if (Model is not { } model
            || row is not CanvasTableRow bound
            || model.RowFor(bound.NodeId) is not { } outlineRow)
        {
            return;
        }
        if (model.Activate(outlineRow) == CanvasActivation.DetailShown)
        {
            DetailRequested?.Invoke();
        }
        // ExpandGroup has nothing to expand on a flat projection, and
        // mac's table falls through the same way (its activate has no
        // group arm at all). The row stays where it is; nothing is
        // announced, because nothing happened.
    }

    /// <summary>
    /// The mac row actions, in mac's order. Mark and Delete are the
    /// verbs PR G and PR E own; they are listed DISABLED with a reason
    /// rather than hidden, because the substrate's menu retains a
    /// temporarily unavailable relevant action with its reason (the mac
    /// RowAction contract) and a silently absent verb is the harder
    /// thing for a screen-reader user to notice.
    /// </summary>
    private IReadOnlyList<AccessibleGridRowAction> RowActions() =>
    [
        new()
        {
            Name = CanvasPhrase.OpenRowAction,
            Execute = ActivateRow,
        },
        new()
        {
            Name = CanvasPhrase.ToggleMarkRowAction,
            Execute = static _ => { },
            IsEnabled = static _ => false,
            DisabledReason = CanvasPhrase.MarkingArrivesLater,
        },
        new()
        {
            Name = CanvasPhrase.DeleteRowAction,
            Execute = static _ => { },
            IsEnabled = static _ => false,
            DisabledReason = CanvasPhrase.DeletingArrivesLater,
        },
    ];

    /// <summary>
    /// The mac column inventory, in mac's order, with mac's comparators
    /// (contract B2/B3). Every cell is a core field; the only host parts
    /// are the capitalisation of core's kind word and the choice of the
    /// group path's last element, both of which mac makes identically.
    /// </summary>
    private static IReadOnlyList<AccessibleGridColumn> Columns() =>
    [
        new()
        {
            Header = CanvasPhrase.TypeColumn,
            Cell = row => CanvasPhrase.TypeCell(Row(row).Kind),
            Sort = Ordinal(row => row.Kind),
        },
        new()
        {
            Header = CanvasPhrase.TitleColumn,
            Cell = row => Row(row).SpeakableName,
            Sort = Localized(row => row.SpeakableName),
            // The row's identity for the UIA Table pattern and for the
            // substrate's reader-position restore. `speakable_name` is
            // core's one uniqueness algorithm (0b-5), so this identity
            // is unique by construction where a bare title would not be.
            IsRowHeader = true,
        },
        new()
        {
            Header = CanvasPhrase.GroupColumn,
            Cell = row => GroupOf(Row(row)),
            Sort = Localized(GroupOf),
        },
        new()
        {
            Header = CanvasPhrase.TargetColumn,
            Cell = row => Row(row).Target,
            Sort = Ordinal(row => row.Target),
        },
        new()
        {
            Header = CanvasPhrase.ConnectionsColumn,
            Cell = row => Row(row).ConnectionCount
                .ToString(CultureInfo.InvariantCulture),
            // NUMERIC, not the cell text: 2 sorts before 10, which an
            // ordinal compare of the rendered digits gets backwards.
            Sort = Comparer<object>.Create(
                (x, y) => Row(x).ConnectionCount.CompareTo(Row(y).ConnectionCount)),
        },
        new()
        {
            Header = CanvasPhrase.ColorColumn,
            Cell = row => ColorOf(Row(row)),
            Sort = Ordinal(ColorOf),
        },
    ];

    private static CanvasTableRow Row(object row) => (CanvasTableRow)row;

    /// <summary>The containing group's label, or empty — core's group
    /// path, last element, exactly as mac reads it.</summary>
    private static string GroupOf(CanvasTableRow row) =>
        row.GroupPath.Length > 0 ? row.GroupPath[^1] : string.Empty;

    /// <summary>Core's colour NAME (never a hex): presets are words and
    /// a custom is "⟨nearest preset⟩ (custom)", which is what makes a
    /// custom sort beside its family (contract B3).</summary>
    private static string ColorOf(CanvasTableRow row) =>
        row.ColorName ?? string.Empty;

    private static bool IsGroup(CanvasTableRow row) =>
        string.Equals(row.Kind, "group", StringComparison.Ordinal);

    private static bool IsNode(object row, string nodeId) =>
        string.Equals(Row(row).NodeId, nodeId, StringComparison.Ordinal);

    /// <summary>Mac's <c>&lt;</c> over the same ASCII values: ordinal
    /// keeps the order deterministic across cultures where the value is
    /// an identifier rather than prose (the <c>ReadingTableGrid</c>
    /// precedent).</summary>
    private static IComparer<object> Ordinal(Func<CanvasTableRow, string> value) =>
        Comparer<object>.Create(
            (x, y) => string.CompareOrdinal(value(Row(x)), value(Row(y))));

    /// <summary>Mac's <c>localizedCaseInsensitiveCompare</c>:
    /// user-authored prose sorts the way the user's locale reads
    /// it.</summary>
    private static IComparer<object> Localized(Func<CanvasTableRow, string> value) =>
        Comparer<object>.Create(
            (x, y) => string.Compare(
                value(Row(x)), value(Row(y)), StringComparison.CurrentCultureIgnoreCase));
}
