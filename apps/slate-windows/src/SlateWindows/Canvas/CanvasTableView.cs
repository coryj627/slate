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
        // Spec §7 / §PR B's consumes-note: the substrate's Ctrl+F is the
        // TABLE's delivery site for `slate.canvas.filterCards`, so it
        // subscribes to the ONE canvas filter rather than the canvas
        // shadowing the grid's own route. With a subscriber the gesture
        // stops continuing-routing, which is what keeps the app-level
        // find from firing behind a grid that CAN filter (contract C10).
        _grid.FilterRequested += () => Model?.Navigator.FilterCards();
        // A14.3: realization is part of delivery, so a request that
        // could not reach a virtualized row is retried when the panel
        // makes containers — the outline's `ContainersRealized` shape,
        // one projection over.
        _grid.ContainersRealized += () => ContainersRealized?.Invoke();
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

    /// <summary>The grid realized row containers — a pending focus
    /// request that could not reach its row may be deliverable now
    /// (contract A14.3, the outline's twin).</summary>
    internal event Action? ContainersRealized;

    internal AccessibleDataGrid GridForTests => _grid;

    /// <summary>
    /// Deliver a focus request to a row (contract A14's table arm).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The SEAT is silent — <see cref="AccessibleDataGrid.SelectRow"/>
    /// suppresses the grid's own row-move line, because a landing is not
    /// a move the user made and the reader is about to read the row they
    /// land on anyway (A12's "landing selection is silent", applied to
    /// this projection).
    /// </para>
    /// <para>
    /// The DELIVERY as a whole is not unconditionally silent, and an
    /// earlier version of this comment said it was. Seating currency
    /// raises <c>CurrentRowChanged</c> outside the sync guard, so a
    /// request that lands on a node OTHER than the current selection
    /// reaches <c>SelectNode</c> and the document narrates the move —
    /// reachable when `LastActivatedNode` differs from `Selected`, since
    /// A14 prefers the activated row and never consults the selection.
    /// That is inherited behaviour, not this projection's: the outline's
    /// <c>DeliverFocus</c> drives the same path through its own
    /// selection binding, and diverging here would make the two
    /// projections behave differently on the same request. Filed for
    /// A14's owner (PR C adds the first caller that passes a NodeId) —
    /// see §B's round record.
    /// </para>
    /// <para>
    /// Delivery is reported only when the REALIZED ROW took focus.
    /// <see cref="AccessibleDataGrid.SelectRow"/> with
    /// <c>moveFocus</c> answers exactly that — a bound row whose
    /// container does not exist yet returns false, and the substrate's
    /// grid-level fallback is no longer dressed up as success. The first
    /// version asked <c>IsKeyboardFocusWithin</c> instead, which is TRUE
    /// for that fallback and true again whenever the reader was already
    /// anywhere in the grid: the request was consumed, the reader never
    /// reached the row, and nothing retried — the exact defect A14.3 was
    /// rewritten over on the outline, reproduced here (codex B round 1,
    /// B1). A failed delivery leaves the request pending, and
    /// <see cref="ContainersRealized"/> brings the surface back when the
    /// panel makes the container.
    /// </para>
    /// </remarks>
    internal bool DeliverFocus(string nodeId)
    {
        if (Model is not { } model)
        {
            return false;
        }
        // Contract C12 / CD-40, the outline's twin one projection over:
        // seating currency IS taking focus in a DataGrid, so the guard
        // covers the whole delivery and the shared selection is seated
        // silently rather than narrated. The paragraph this replaces
        // recorded the doubling as inherited behaviour and filed it for
        // PR C; PR C is here, and it is fixed in BOTH projections
        // together, because a table-only fix would make the two behave
        // differently on the same request.
        _syncingSelection = true;
        try
        {
            bool delivered = _grid.SelectRow(row => IsNode(row, nodeId), moveFocus: true);
            if (delivered)
            {
                model.SeatSelectionSilently(nodeId);
            }
            return delivered;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    /// <summary>The navigator's boundary question (contract C3) — the
    /// grid's own order, which the reader's sort may have changed.</summary>
    internal bool CanMoveRow(bool forward) => _grid.CanMoveRow(forward);

    internal bool HasKeyboardFocus => _grid.IsKeyboardFocusWithin;

    internal void FocusGrid() => _ = _grid.FocusFirstCell();

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
        // The FILTERED rows (contract C10) — narrowed by the same match
        // set the outline shows, so the two projections never disagree
        // about what the needle answered.
        IReadOnlyList<CanvasTableRow> rows = Model?.FilteredTableRows ?? [];
        // The summary counts the WHOLE canvas, filtered or not, and
        // always has: it is a never-announced label describing the
        // document ("Canvas table: 7 cards, 2 groups."), not a match
        // count — the filter's own count lives in the header's result
        // summary, which is the one bound by *displayed rows == announced
        // count* (contract C10, VA-1's recorded carve-out).
        IReadOnlyList<CanvasTableRow> whole = Model?.TableRows ?? [];
        int cards = 0;
        foreach (CanvasTableRow row in whole)
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
        // The bind runs under the sync guard for the same reason the
        // re-seat below does, and it is NOT redundant with the
        // substrate's own silence. `Bind` restores the reader's position
        // by ROW-HEADER TEXT, because every publish builds fresh row
        // objects and identity is gone by definition — and this
        // projection's row header is core's `speakable_name`, which a
        // republish can RENUMBER. Two cards titled "Shared" are "Shared"
        // and "Shared 2"; rename the first and the second becomes
        // "Shared", so the restore lands the reader on a DIFFERENT card
        // that now spells what they were reading. The substrate suppresses
        // its own announcement there, but `CurrentRowChanged` still
        // fires, and without this guard it would reach `SelectNode` and
        // speak a canvas move the reader never made — from a background
        // republish, which is the one thing that must never talk.
        _syncingSelection = true;
        try
        {
            _grid.Bind(
                Columns(),
                rows.Cast<object>().ToArray(),
                summary: CanvasPhrase.TableSummary(cards, whole.Count - cards),
                accessibilityLabel: CanvasPhrase.TableName,
                rowAudioDescription: null,
                rowActions: RowActions(),
                exportProducer: null,
                rowActivated: ActivateRow);
        }
        finally
        {
            _syncingSelection = false;
        }
        // The model is the authority the reader comes back to: whatever
        // the header-text restore landed on, the selected NODE is where
        // the reader is seated.
        //
        // Currency ONLY, deliberately — and the alternative was tried and
        // rejected with evidence (codex B round 1, B2). Re-seating WITH
        // focus when the reader was in the grid is unnecessary: while the
        // DataGrid holds focus it moves focus with currency, so the
        // reader, currency and the shared selection all land on the same
        // row (measured — the reader/currency split the finding
        // described did not reproduce, with the precondition holding).
        // It is also harmful: `IsKeyboardFocusWithin` on this control
        // includes the separately-focusable SUMMARY region, so a reader
        // sitting on the summary when a background publish arrives would
        // be yanked onto a row — the W4-6 background-publish focus-steal
        // defect, reintroduced. `ARepublishNeverYanksTheReaderOffTheSummaryRegion`
        // now guards that, and fails if this ever grows a focus re-seat.
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
        // SAVED and restored, not forced false — the outline's twin
        // reason: `DeliverFocus` seats the shared selection inside its
        // own guard and re-enters here through the property change.
        bool outer = _syncingSelection;
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
            _syncingSelection = outer;
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

    /// <summary>
    /// Mac's <c>&lt;</c>, transliterated — and the transliteration is
    /// NOT exact. Swift's <c>String</c> ordering is defined over Unicode
    /// canonical equivalence: the standard library normalizes before
    /// comparing (the ordering implementation is the stdlib's
    /// <c>StringComparison.swift</c>; <i>The Swift Programming
    /// Language</i> documents the equality half). <c>CompareOrdinal</c>
    /// normalizes nothing and compares UTF-16 code units. So the two
    /// disagree on targets that differ in normalization form — an NFD
    /// "Café.md" sorts before "Caff.md" here and after it on mac — and
    /// on supplementary-plane pairs, where code-unit order and scalar
    /// order differ. The first class is ordinary, not exotic: macOS
    /// hands back decomposed filenames, so a Mac-authored canvas carries
    /// NFD targets. Kind and colour name are closed ASCII sets core owns
    /// and reach neither class.
    ///
    /// This repo already depends on the difference: the Swift parity
    /// harness sorts with an explicit
    /// <c>Array($0.utf16).lexicographicallyPrecedes(…)</c> rather than
    /// <c>&lt;</c>, because native Swift ordering would not match the C#
    /// twin's <c>StringComparer.Ordinal</c>.
    ///
    /// Ordinal stays anyway, and CD-39 records why: it is deterministic
    /// and locale-independent, normalizing host-side would be the host
    /// deriving an ordering core does not define, and no §W-A artifact
    /// serializes a host's sort order, so nothing cross-host compares
    /// these (the <c>ReadingTableGrid</c> precedent for the choice).
    /// </summary>
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
