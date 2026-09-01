// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// One line of the outline: a node row, or a connection row nested under
/// the SELECTED card (contract A8/A11). Every displayed and spoken part
/// is core's — the composition rules are contracts A9/A10 and the
/// connection name is core's own render.
/// </summary>
internal sealed class CanvasOutlineRowViewModel : BindableBase
{
    private bool _isExpanded;
    private bool _isSelected;

    private CanvasOutlineRowViewModel(
        string id, string name, string status, string hint, bool isGroup)
    {
        Id = id;
        Name = name;
        Status = status;
        Hint = hint;
        IsGroup = isGroup;
    }

    /// <summary>A node row.</summary>
    internal static CanvasOutlineRowViewModel ForNode(
        CanvasOutlineRow row, bool marked, bool filtered) =>
        new(
            row.NodeId,
            CanvasPhrase.CardReference(row.Kind, row.SpeakableName),
            CanvasPhrase.RowStatus(
                row.OrdinalN,
                row.TotalM,
                row.GroupPath.Length > 0 ? row.GroupPath[^1] : null,
                row.ColorName,
                marked,
                filtered),
            CanvasPhrase.ActivationHint(row.Kind),
            string.Equals(row.Kind, "group", StringComparison.Ordinal))
        {
            Row = row,
        };

    /// <summary>A connection row. Its NAME is the same event the
    /// navigator speaks when it traverses this connection (contract
    /// A11, CD-14) — one render, no second composition.</summary>
    internal static CanvasOutlineRowViewModel ForConnection(
        CanvasOutlineRow parent,
        CanvasNeighbor neighbor,
        string otherKind,
        int ordinal,
        int total) =>
        new(
            parent.NodeId + "→" + neighbor.EdgeId,
            CanvasAnnouncer.RenderLabel(
                new CanvasA11yEvent.CanvasConnectionTraversed(
                    Direction: neighbor.Direction,
                    KindLabel: otherKind,
                    Title: neighbor.OtherTitle,
                    Label: neighbor.Label)),
            CanvasPhrase.ConnectionStatus(ordinal, total),
            CanvasPhrase.ConnectionHint,
            isGroup: false)
        {
            Neighbor = neighbor,
        };

    /// <summary>Stable line identity — the node id, or
    /// <c>parent→edge</c> for a connection (the mac <c>Line.id</c>
    /// shape).</summary>
    public string Id { get; }

    public CanvasOutlineRow? Row { get; private init; }

    public CanvasNeighbor? Neighbor { get; private init; }

    public bool IsConnection => Neighbor is not null;

    public bool IsGroup { get; }

    /// <summary>UIA Name (contract A9).</summary>
    public string Name { get; }

    /// <summary>UIA ItemStatus (contract A10).</summary>
    public string Status { get; }

    /// <summary>UIA HelpText — the per-kind activation hint.</summary>
    public string Hint { get; }

    /// <summary>The visible text. Deliberately the same string as the
    /// Name: the outline's job is to be read, and a visible label that
    /// differs from the spoken one is a §W-C defect, not a
    /// feature.</summary>
    public string Display => Name;

    /// <summary>Indentation follows core's depth; the tree already
    /// nests, so this is presentation only.</summary>
    public ObservableCollection<CanvasOutlineRowViewModel> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    /// <summary>The owning view's activation route — the UIA
    /// <c>Invoke</c>, Enter and double-click all land here, so there is
    /// one activation path per row.</summary>
    internal Action<CanvasOutlineRowViewModel>? Activate { get; set; }

    internal void RaiseActivate() => Activate?.Invoke(this);
}

/// <summary>
/// The tree container. It exists so every item is a
/// <see cref="CanvasOutlineItem"/> and therefore carries the
/// <c>Invoke</c> pattern (contract A8).
/// </summary>
internal sealed class CanvasOutlineTree : TreeView
{
    protected override DependencyObject GetContainerForItemOverride() =>
        new CanvasOutlineItem();

    protected override bool IsItemItsOwnContainerOverride(object item) =>
        item is CanvasOutlineItem;

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new CanvasOutlineTreeAutomationPeer(this);
}

/// <summary>
/// The tree's peer, which exists ONLY to override
/// <see cref="CreateItemAutomationPeer"/>.
/// </summary>
/// <remarks>
/// This is the fix for the round-7 defect. A container peer's patterns
/// are NOT what assistive technology reads: WPF projects each row into
/// the UIA tree as a <see cref="TreeViewDataItemAutomationPeer"/>, and
/// that data peer implements SelectionItem, ExpandCollapse and ScrollItem
/// ITSELF rather than forwarding everything to the container. So the
/// <c>Invoke</c> added to <see cref="CanvasOutlineItemAutomationPeer"/>
/// was real but invisible — a screen reader saw <c>invoke=NULL</c> on
/// every row. Overriding the item-peer factory here and on the item peer
/// puts Invoke on the peer that is actually exposed.
/// </remarks>
internal sealed class CanvasOutlineTreeAutomationPeer : TreeViewAutomationPeer
{
    public CanvasOutlineTreeAutomationPeer(CanvasOutlineTree owner)
        : base(owner)
    {
    }

    protected override ItemAutomationPeer CreateItemAutomationPeer(object item) =>
        new CanvasOutlineRowDataPeer(item, this, null);
}

/// <summary>
/// The peer a screen reader actually reads for a row: WPF's tree data
/// item peer, plus the <c>Invoke</c> the outline's contract A8 requires.
/// </summary>
/// <remarks>
/// Name, ItemStatus and HelpText are deliberately NOT overridden — the
/// base forwards them to the realized container's peer, which is where
/// contracts A9/A10 already put them, and the journey's name/status
/// assertions pass on that path today.
/// </remarks>
internal sealed class CanvasOutlineRowDataPeer
    : TreeViewDataItemAutomationPeer, IInvokeProvider
{
    public CanvasOutlineRowDataPeer(
        object item,
        ItemsControlAutomationPeer itemsControlAutomationPeer,
        TreeViewDataItemAutomationPeer? parentDataItemAutomationPeer)
        : base(item, itemsControlAutomationPeer, parentDataItemAutomationPeer)
    {
    }

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Invoke
            ? this
            : base.GetPattern(patternInterface);

    /// <summary>The ONE activation route — the same
    /// <see cref="CanvasOutlineRowViewModel.RaiseActivate"/> Enter and
    /// double-click reach, so UIA activation cannot drift from them.</summary>
    public void Invoke() => (Item as CanvasOutlineRowViewModel)?.RaiseActivate();
}

/// <summary>
/// A tree item that also answers <c>Invoke</c>.
/// <see cref="TreeViewItemAutomationPeer"/> implements ExpandCollapse,
/// SelectionItem and ScrollItem but NOT <see cref="IInvokeProvider"/>,
/// and the spec's outline needs Invoke on both the node rows
/// (activation per kind) and the connection rows (follow). Adding an
/// invokable CHILD element instead would put a second peer inside every
/// row, which the journeys' recorded peered-elements-only trap forbids.
/// </summary>
internal sealed class CanvasOutlineItem : TreeViewItem
{
    protected override DependencyObject GetContainerForItemOverride() =>
        new CanvasOutlineItem();

    protected override bool IsItemItsOwnContainerOverride(object item) =>
        item is CanvasOutlineItem;

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new CanvasOutlineItemAutomationPeer(this);

    internal void InvokeRow() =>
        (DataContext as CanvasOutlineRowViewModel)?.RaiseActivate();
}

internal sealed class CanvasOutlineItemAutomationPeer
    : TreeViewItemAutomationPeer, IInvokeProvider
{
    public CanvasOutlineItemAutomationPeer(CanvasOutlineItem owner)
        : base(owner)
    {
    }

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Invoke
            ? this
            : base.GetPattern(patternInterface);

    /// <summary>
    /// NESTED rows — a group's children, and the selected card's
    /// connection rows — are projected by THIS peer, so it must build the
    /// same Invoke-carrying data peer the tree's peer does. Without this
    /// override only top-level rows would answer Invoke, which is the
    /// same defect one level down.
    /// </summary>
    protected override ItemAutomationPeer CreateItemAutomationPeer(object item) =>
        new CanvasOutlineRowDataPeer(
            item, this, EventsSource as TreeViewDataItemAutomationPeer);

    public void Invoke() => ((CanvasOutlineItem)Owner).InvokeRow();
}

/// <summary>
/// W6-1 PR A (#745): the accessible canvas outline — the primary
/// structured surface. Every card and group in core's reading order,
/// nested by core's <c>depth</c>, with the t0 §3 positional context in
/// each item's ItemStatus and the selected card's connection rows as
/// its children (contracts A8–A14).
///
/// Selection drives the shared <see cref="CanvasSelection"/> through
/// the document (single source of truth; surfaces never hold a
/// selection of their own). Activation opens the card per kind.
/// Returning from an opened card restores focus to its row (WCAG
/// 2.4.3).
/// </summary>
internal sealed class CanvasOutlineView : UserControl
{
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(
            nameof(Model),
            typeof(CanvasDocumentViewModel),
            typeof(CanvasOutlineView),
            new PropertyMetadata(null, OnModelChanged));

    private readonly CanvasOutlineTree _tree;
    private readonly ObservableCollection<CanvasOutlineRowViewModel> _roots = [];
    private readonly Dictionary<string, CanvasOutlineRowViewModel> _byNode =
        new(StringComparer.Ordinal);

    /// <summary>Row → its parent row. The realization walk needs the
    /// ancestor chain root-first, and nothing else in WPF will give it:
    /// a virtualized container has no visual parent until it exists.</summary>
    private readonly Dictionary<CanvasOutlineRowViewModel, CanvasOutlineRowViewModel> _parentOf =
        [];

    /// <summary>The row this view believes is selected — the one whose
    /// TwoWay flag must be cleared before another is set.</summary>
    private CanvasOutlineRowViewModel? _selectedRow;
    private string? _connectionHost;
    private int _connectionCount;
    private bool _syncingSelection;

    /// <summary>This VIEW's expansion memory (contract E15, IE-30):
    /// keyed by node id, session-scoped, pruned to the ids the current
    /// population knows (ED-4). Per-VIEW because per-surface — two
    /// panes share one document, and pane A's mutation must not
    /// overwrite pane B's independent expansion intent, which a
    /// document-level set would do.</summary>
    private readonly Dictionary<string, bool> _expansion =
        new(StringComparer.Ordinal);

    public CanvasOutlineView()
    {
        _tree = new CanvasOutlineTree
        {
            ItemsSource = _roots,
            ItemTemplate = RowTemplate(),
            ItemContainerStyle = RowContainerStyle(),
            // §E TE-8: the context menu builds lazily, per row, from
            // the ONE plan — assigned during ContextMenuOpening so the
            // Menu key, Shift+F10 and the pointer all take the same
            // derived rows (IE-31; the census asserts equality).
            BorderThickness = new Thickness(0),
        };
        _tree.AddHandler(
            System.Windows.FrameworkElement.ContextMenuOpeningEvent,
            new System.Windows.Controls.ContextMenuEventHandler(
                OnRowContextMenuOpening),
            handledEventsToo: false);
        ScrollViewer.SetCanContentScroll(_tree, true);
        VirtualizingStackPanel.SetIsVirtualizing(_tree, true);
        // Standard, NOT Recycling (contract A8): a recycled container
        // re-uses one automation peer for different rows, which is the
        // W4-1 UIA-safe setting's whole point.
        VirtualizingStackPanel.SetVirtualizationMode(
            _tree, VirtualizationMode.Standard);
        AutomationProperties.SetAutomationId(_tree, "CanvasOutlineTree");
        AutomationProperties.SetName(_tree, CanvasPhrase.OutlineName);
        _tree.ItemContainerGenerator.StatusChanged += (_, _) =>
        {
            if (_tree.ItemContainerGenerator.Status
                != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                return;
            }
            // POSTED, not called: this fires from inside the generator's
            // own pass, and a delivery attempt lays out and may ask the
            // panel to bring an index into view — re-entering generation
            // and throwing "Cannot call StartAt when content generation
            // is in progress". Background priority runs it after the
            // pass completes.
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                () => ContainersRealized?.Invoke());
        };
        _tree.SelectedItemChanged += OnTreeSelectionChanged;
        _tree.KeyDown += OnTreeKeyDown;
        _tree.MouseDoubleClick += OnTreeDoubleClick;
        Content = _tree;
    }

    public CanvasDocumentViewModel? Model
    {
        get => (CanvasDocumentViewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>Raised when a text card's read-only detail was
    /// published — the surface moves focus there (contract A13).</summary>

    /// <summary>The tree realized containers — a pending focus request
    /// that could not reach its row may be deliverable now.</summary>
    internal event Action? ContainersRealized;

    internal TreeView TreeForTests => _tree;

    internal IReadOnlyList<CanvasOutlineRowViewModel> RootsForTests => _roots;

    /// <summary>
    /// Deliver focus to a row, REALIZING its container first. Returns
    /// the row on success and null when the container could not be
    /// realized — and null must not be read as "delivered", which is
    /// the whole point (contract A14).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Under virtualization a row's container does not exist until the
    /// panel makes it, and an ancestor group must be expanded before its
    /// children are items at all. The walk is the documented one: from
    /// the root down, expand, lay out, ask the generator, and if the
    /// container is still absent bring its index into view through the
    /// virtualizing panel and lay out again.
    /// </para>
    /// <para>
    /// The previous version fell back to <c>_tree.Focus()</c> and
    /// returned the row anyway, so an unrealized container reported
    /// SUCCESS and consumed the request — focus landed on the tree, the
    /// row was never read, and nothing retried. A failure here leaves
    /// the request pending so the next realization delivers it.
    /// </para>
    /// </remarks>
    internal CanvasOutlineRowViewModel? DeliverFocus(string nodeId)
    {
        if (Model is not { } model
            || !_byNode.TryGetValue(nodeId, out CanvasOutlineRowViewModel? target))
        {
            return null;
        }
        // Contract C12 / CD-40: a delivery is a LANDING, not a move the
        // user made, so it is silent. The whole body runs inside the sync
        // guard — WPF's TreeViewItem selects itself on GotFocus, so the
        // container's own echo would otherwise reach `SelectNode` and
        // narrate a `CanvasMovedTo` on top of the row the screen reader
        // is already reading (t0 §1.5 doubling). The shared selection
        // still follows the reader, because R-B says there is exactly one
        // of it — it just follows silently.
        _syncingSelection = true;
        try
        {
            SetSelectedRow(target);
            model.SeatSelectionSilently(nodeId);
            return RealizeContainer(target) is { } container && container.Focus()
                ? target
                : null;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    /// <summary>
    /// Whether the tree can move the reader one row in this direction
    /// itself — the navigator's boundary question (contract C3).
    /// </summary>
    /// <remarks>
    /// The tree's own rows, not core's reading order: a connection row
    /// under the selected card is a reading stop the tree visits
    /// (contract A11), and asking core's order instead would report
    /// "End of canvas." while a connection row was still below the
    /// cursor.
    /// </remarks>
    internal bool CanMoveFocus(bool forward)
    {
        List<CanvasOutlineRowViewModel> visible = VisibleRows();
        if (visible.Count == 0)
        {
            return false;
        }
        CanvasOutlineRowViewModel? current = FocusedRow() ?? _selectedRow;
        if (current is null)
        {
            return true;
        }
        int index = visible.IndexOf(current);
        if (index < 0)
        {
            return true;
        }
        return forward ? index < visible.Count - 1 : index > 0;
    }

    /// <summary>The rows a reader can arrow through right now: the tree
    /// in order, descending only into expanded rows.</summary>
    private List<CanvasOutlineRowViewModel> VisibleRows()
    {
        var visible = new List<CanvasOutlineRowViewModel>();
        void Walk(IEnumerable<CanvasOutlineRowViewModel> rows)
        {
            foreach (CanvasOutlineRowViewModel row in rows)
            {
                visible.Add(row);
                if (row.IsExpanded)
                {
                    Walk(row.Children);
                }
            }
        }
        Walk(_roots);
        return visible;
    }

    /// <summary>
    /// The row that owns keyboard focus. WPF focuses the CONTAINER for a
    /// tree row, so the focused element is the item itself.
    /// </summary>
    private static CanvasOutlineRowViewModel? FocusedRow() =>
        Keyboard.FocusedElement is CanvasOutlineItem item
            ? item.DataContext as CanvasOutlineRowViewModel
            : null;

    internal bool HasKeyboardFocus => _tree.IsKeyboardFocusWithin;

    /// <summary>Put the reader on the tree, reporting whether it took
    /// the keys — a collapsed projection cannot, and a caller with
    /// nowhere else to go needs to know that (contract C6).</summary>
    internal bool FocusTree() => _tree.Focus();

    /// <summary>The container for a row at any depth, realized if it
    /// can be. Null when the panel would not make it.</summary>
    private CanvasOutlineItem? RealizeContainer(CanvasOutlineRowViewModel target)
    {
        // Root-first path, so every ancestor is expanded before its
        // children are asked for.
        var path = new List<CanvasOutlineRowViewModel>();
        for (CanvasOutlineRowViewModel? step = target;
            step is not null;
            step = _parentOf.GetValueOrDefault(step))
        {
            path.Add(step);
        }
        path.Reverse();

        ItemsControl parent = _tree;
        CanvasOutlineItem? container = null;
        foreach (CanvasOutlineRowViewModel step in path)
        {
            container = RealizeChild(parent, step);
            if (container is null)
            {
                return null;
            }
            if (!ReferenceEquals(step, target))
            {
                // An ancestor: its children are not items until it is
                // expanded, so expanding IS part of realization.
                step.IsExpanded = true;
                container.UpdateLayout();
            }
            parent = container;
        }
        container?.BringIntoView();
        return container;
    }

    private static CanvasOutlineItem? RealizeChild(ItemsControl parent, object item)
    {
        parent.ApplyTemplate();
        parent.UpdateLayout();
        if (parent.ItemContainerGenerator.ContainerFromItem(item) is CanvasOutlineItem ready)
        {
            return ready;
        }
        int index = parent.Items.IndexOf(item);
        if (index < 0)
        {
            return null;
        }
        // Virtualized away: ask the panel for it by index, which is the
        // one supported way to make a container that does not exist.
        if (FindVisualChild<VirtualizingStackPanel>(parent) is { } panel)
        {
            panel.BringIndexIntoViewPublic(index);
            parent.UpdateLayout();
        }
        return parent.ItemContainerGenerator.ContainerFromItem(item) as CanvasOutlineItem;
    }

    private static TChild? FindVisualChild<TChild>(DependencyObject parent)
        where TChild : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is TChild match)
            {
                return match;
            }
            if (FindVisualChild<TChild>(child) is { } nested)
            {
                return nested;
            }
        }
        return null;
    }

    private static void OnModelChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (CanvasOutlineView)d;
        if (e.OldValue is CanvasDocumentViewModel oldModel)
        {
            oldModel.OutlinePublished -= view.OnOutlinePublished;
            oldModel.Selection.PropertyChanged -= view.OnSelectionChanged;
        }
        if (e.NewValue is CanvasDocumentViewModel model)
        {
            model.OutlinePublished += view.OnOutlinePublished;
            model.Selection.PropertyChanged += view.OnSelectionChanged;
        }
        view.Rebuild();
    }

    private void OnOutlinePublished(object? sender, EventArgs e) => Rebuild();

    private void OnSelectionChanged(
        object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CanvasSelection.Selected))
        {
            ApplySelection();
        }
    }

    /// <summary>Core's flat, depth-annotated reading order becomes a
    /// tree — the host derives no containment (R-D; the depth column is
    /// 0b-8's tree, already). TWO passes, always: containment is read
    /// from the UNFILTERED rows, then the displayed rows are attached to
    /// it (CD-45). With no needle the second pass walks the same rows,
    /// which is the same shape rather than a special case.</summary>
    private void Rebuild()
    {
        // E15: remember what the reader expanded BEFORE the rows are
        // torn down — a funnel republish rebuilds every row, and the
        // default-open rule below would otherwise undo every collapse.
        // Group rows only: connection-host expansion is the selection's,
        // re-derived on every sync.
        foreach ((string id, CanvasOutlineRowViewModel row) in _byNode)
        {
            if (row.IsGroup)
            {
                _expansion[id] = row.IsExpanded;
            }
        }
        _roots.Clear();
        _byNode.Clear();
        _parentOf.Clear();
        _selectedRow = null;
        _connectionHost = null;
        _connectionCount = 0;
        if (Model is not { } model)
        {
            return;
        }
        // CONTAINMENT COMES FROM THE WHOLE CANVAS, not from the rows
        // that survived a filter (contract C10, CD-45).
        //
        // The depth stack used to run over the FILTERED rows, which reads
        // as the same thing and is not: depth is a position in core's
        // reading order, so a survivor whose own group was filtered out
        // attached to whatever survivor happened to be shallower and
        // earlier — a card from an UNRELATED branch. That is a containment
        // claim the file does not make, and a screen reader reads it as
        // one ("Evidence, inside Quarter"), which is worse than the flat
        // list CD-45 chose over it.
        //
        // So the parent chain is the UNFILTERED outline's, and each
        // survivor attaches to its nearest surviving TRUE ancestor — or
        // becomes a root, which is CD-45's promotion said properly.
        // mac's flat outline has no such case (CD-33). The chain comes
        // from the POPULATION, which owns the one depth-walk
        // derivation — this view carried a line-for-line copy of it,
        // with the depth-gap fix only in the copy that documented it,
        // until the cleanup pass folded the two.
        CanvasPopulation? population = model.AppliedPopulation;

        // ED-4's drop rule, against the POPULATION rather than the
        // displayed rows: a group hidden by a filter keeps its remembered
        // collapse for the filter's clearing, but an id this canvas no
        // longer contains — or never did, after a model swap — is
        // forgotten here, before the memory is consulted.
        if (population is not null)
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (CanvasOutlineRow row in population.Outline)
            {
                known.Add(row.NodeId);
            }
            var stale = new List<string>();
            foreach (string id in _expansion.Keys)
            {
                if (!known.Contains(id))
                {
                    stale.Add(id);
                }
            }
            foreach (string id in stale)
            {
                _ = _expansion.Remove(id);
            }
        }

        bool filtered = model.FilterActive;
        foreach (CanvasOutlineRow row in model.FilteredOutline)
        {
            var line = CanvasOutlineRowViewModel.ForNode(
                row, model.Selection.IsMarked(row.NodeId), filtered);
            line.Activate = ActivateRow;
            // Walk the TRUE chain until a surviving ancestor is found.
            // The rows arrive in reading order, so an ancestor that
            // survived has already been materialized.
            CanvasOutlineRowViewModel? parent = null;
            string? ancestor = population?.Parent(row.NodeId);
            while (ancestor is not null)
            {
                if (_byNode.TryGetValue(ancestor, out CanvasOutlineRowViewModel? found))
                {
                    parent = found;
                    break;
                }
                ancestor = population?.Parent(ancestor);
            }
            if (parent is null)
            {
                _roots.Add(line);
            }
            else
            {
                parent.Children.Add(line);
                _parentOf[line] = parent;
                // A group with members is expandable; it opens by
                // default so a first read is the whole structure —
                // unless THIS view's reader already chose (E15).
                parent.IsExpanded =
                    !_expansion.TryGetValue(parent.Id, out bool kept) || kept;
            }
            _byNode[row.NodeId] = line;
        }
        ApplySelection();
    }

    /// <summary>
    /// Connection rows materialize under the SELECTED card only
    /// (contract A11): linear reading stays concise at 2,000 nodes, and
    /// PR C's follow-connection commands are the canvas-wide traversal.
    /// They come FIRST among the row's children, which reproduces mac's
    /// flat line order as a depth-first walk (CD-33).
    /// </summary>
    private void ApplySelection()
    {
        if (Model is not { } model)
        {
            return;
        }
        // The WHOLE body is the model → view direction, so none of it
        // may be read back as a user action. Guarding only the
        // `IsSelected` assignment was not enough: removing the
        // connection rows below removes a row that may currently BE the
        // tree's selection (arrow onto a connection row, then follow),
        // and WPF answers by re-selecting the parent container — which
        // came back through OnTreeSelectionChanged as a fresh user
        // selection and dragged the model back to the card the user had
        // just left. Found by `ArrowingOntoAConnectionRowLeavesItReadable`.
        // SAVED and restored, not forced false: `DeliverFocus` seats the
        // shared selection inside its OWN guard, which re-enters here
        // through the selection's property change — and a bare
        // `finally { false }` would drop the outer guard while the
        // delivery still had a container focus to take, letting WPF's
        // own selection echo reach `SelectNode` (contract C12).
        bool outer = _syncingSelection;
        _syncingSelection = true;
        try
        {
            ApplySelectionCore(model);
        }
        finally
        {
            _syncingSelection = outer;
        }
    }

    private void ApplySelectionCore(CanvasDocumentViewModel model)
    {
        if (_connectionHost is { } previous
            && !string.Equals(previous, model.Selection.Selected, StringComparison.Ordinal)
            && _byNode.TryGetValue(previous, out CanvasOutlineRowViewModel? host))
        {
            for (int index = 0; index < _connectionCount; index++)
            {
                _ = _parentOf.Remove(host.Children[0]);
                host.Children.RemoveAt(0);
            }
            _connectionHost = null;
            _connectionCount = 0;
        }
        if (model.Selection.Selected is not { } selected
            || !_byNode.TryGetValue(selected, out CanvasOutlineRowViewModel? line))
        {
            return;
        }
        if (!string.Equals(_connectionHost, selected, StringComparison.Ordinal))
        {
            IReadOnlyList<CanvasNeighbor> neighbors = model.NeighborsOf(selected);
            for (int index = 0; index < neighbors.Count; index++)
            {
                var connection = CanvasOutlineRowViewModel.ForConnection(
                    line.Row!,
                    neighbors[index],
                    model.RowFor(neighbors[index].OtherNode)?.Kind ?? "text",
                    index + 1,
                    neighbors.Count);
                connection.Activate = ActivateRow;
                line.Children.Insert(index, connection);
                _parentOf[connection] = line;
            }
            _connectionHost = selected;
            _connectionCount = neighbors.Count;
            if (neighbors.Count > 0
                && model.Selection.ActiveSurface == CanvasSurfaceKind.Outline)
            {
                // Never hidden behind a collapse the user did not ask
                // for (CD-33) — while the outline IS the showing
                // surface. A seat synchronized into a hidden outline
                // seats WITHOUT expanding (E15's cause rule): the §C
                // m-5 sibling is discharged here, at the source of the
                // unwanted expansion bit, not papered over in the
                // preservation above.
                line.IsExpanded = true;
            }
        }
        SetSelectedRow(line);
    }

    /// <summary>
    /// Seat selection on exactly one row: the previous row's TwoWay flag
    /// is cleared FIRST, then the new one is set.
    /// </summary>
    /// <remarks>
    /// Leaving the old flag true let WPF's own deselection echo back
    /// through the binding after the new row was already seated — a
    /// stale write of the row the user had just left. Clearing it here,
    /// inside the sync guard, means the echo has nothing to say.
    /// </remarks>
    private void SetSelectedRow(CanvasOutlineRowViewModel row)
    {
        if (_selectedRow is { } previous && !ReferenceEquals(previous, row))
        {
            previous.IsSelected = false;
        }
        _selectedRow = row;
        row.IsSelected = true;
    }

    private void OnTreeSelectionChanged(
        object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_syncingSelection
            || Model is not { } model
            || e.NewValue is not CanvasOutlineRowViewModel line)
        {
            return;
        }
        // Connection lines are NOT canvas selection state, and arrowing
        // onto one must not act (contract A11; spec behavior 3 gives
        // them `Invoke` = follow, and mac's `returnOpensRow` is the same
        // split). Following here made them unreadable: the arrow key
        // that landed on the row immediately moved the model's
        // selection to the other card, which rebuilt the connection
        // children out from under the cursor — so the direction phrase
        // a screen reader was about to speak was gone before it spoke
        // it. Invoke and Enter both route through ActivateRow, which
        // follows; arrowing is pure reading.
        if (line.IsConnection)
        {
            return;
        }
        // The echo is broken by the flag, not by a value compare: the
        // compare cannot tell "the model already agrees" from "another
        // pane set it to the same node" (contract A12).
        model.SelectNode(line.Id);
    }

    private void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter
            || _tree.SelectedItem is not CanvasOutlineRowViewModel line)
        {
            return;
        }
        e.Handled = true;
        ActivateRow(line);
    }

    private void OnTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_tree.SelectedItem is CanvasOutlineRowViewModel line)
        {
            ActivateRow(line);
        }
    }

    private void ActivateRow(CanvasOutlineRowViewModel line)
    {
        if (Model is not { } model)
        {
            return;
        }
        if (line.Neighbor is { } neighbor)
        {
            model.FollowConnection(neighbor);
            return;
        }
        if (line.Row is not { } row)
        {
            return;
        }
        switch (model.Activate(row))
        {
            case CanvasActivation.ExpandGroup:
                line.IsExpanded = !line.IsExpanded;
                break;
            case CanvasActivation.EditorRequested:
                // The workspace opens the sheet; the modal machinery
                // owns focus (TE-11b).
                break;
            default:
                break;
        }
    }

    /// <summary>§E TE-8: the row's menu from the plan — headers,
    /// enabled flags and staged reasons verbatim; verbs map onto the
    /// document. A connection row (no node Row) gets no menu.</summary>
    internal System.Windows.Controls.ContextMenu? BuildContextMenu(
        CanvasOutlineRowViewModel rowModel)
    {
        ArgumentNullException.ThrowIfNull(rowModel);
        if (rowModel.Row is not { } row || Model is not { } model)
        {
            return null;
        }
        return BuildMenuFromPlan(row.Kind, (verb, nodeId) =>
        {
            switch (verb)
            {
                case CanvasContextVerb.Open:
                    rowModel.RaiseActivate();
                    break;
                case CanvasContextVerb.Delete:
                    model.SeatSelectionSilently(nodeId);
                    model.CanvasDeleteSelection();
                    break;
                case CanvasContextVerb.Ungroup:
                    model.CanvasUngroup(nodeId);
                    break;
                case CanvasContextVerb.EditCard:
                    model.RequestCardEditor(nodeId);
                    break;
                default:
                    break;
            }
        }, row.NodeId);
    }

    /// <summary>The ONE plan-to-menu mapping — the opening handler and
    /// the census fact share it, so the built rows cannot drift from
    /// the plan (IE-31).</summary>
    internal static System.Windows.Controls.ContextMenu BuildMenuFromPlan(
        string kind, Action<CanvasContextVerb, string> execute, string nodeId)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(nodeId);
        var menu = new System.Windows.Controls.ContextMenu();
        foreach (CanvasContextMenuRow planned in CanvasContextMenuPlan.RowsFor(kind))
        {
            var item = new System.Windows.Controls.MenuItem
            {
                Header = planned.Name,
                IsEnabled = planned.Enabled,
                ToolTip = planned.DisabledReason,
            };
            if (planned.DisabledReason is { } reason)
            {
                System.Windows.Automation.AutomationProperties.SetHelpText(item, reason);
                System.Windows.Controls.ToolTipService.SetShowOnDisabled(item, true);
            }
            CanvasContextVerb verb = planned.Verb;
            item.Click += (_, _) => execute(verb, nodeId);
            menu.Items.Add(item);
        }
        return menu;
    }

    private void OnRowContextMenuOpening(
        object sender, System.Windows.Controls.ContextMenuEventArgs e)
    {
        if (e.OriginalSource is System.Windows.DependencyObject source
            && ItemFromSource(source) is { } item
            && item.DataContext is CanvasOutlineRowViewModel rowModel)
        {
            System.Windows.Controls.ContextMenu? menu = BuildContextMenu(rowModel);
            if (menu is null)
            {
                e.Handled = true;
                return;
            }
            item.ContextMenu = menu;
        }
    }

    private static CanvasOutlineItem? ItemFromSource(
        System.Windows.DependencyObject source)
    {
        System.Windows.DependencyObject? walk = source;
        while (walk is not null and not CanvasOutlineItem)
        {
            walk = System.Windows.Media.VisualTreeHelper.GetParent(walk);
        }
        return walk as CanvasOutlineItem;
    }

    private static Style RowContainerStyle()
    {
        var style = new Style(typeof(TreeViewItem));
        style.Setters.Add(new Setter(
            TreeViewItem.IsExpandedProperty,
            new Binding(nameof(CanvasOutlineRowViewModel.IsExpanded))
            {
                Mode = BindingMode.TwoWay,
            }));
        style.Setters.Add(new Setter(
            TreeViewItem.IsSelectedProperty,
            new Binding(nameof(CanvasOutlineRowViewModel.IsSelected))
            {
                Mode = BindingMode.TwoWay,
            }));
        style.Setters.Add(new Setter(
            AutomationProperties.NameProperty,
            new Binding(nameof(CanvasOutlineRowViewModel.Name))));
        style.Setters.Add(new Setter(
            AutomationProperties.ItemStatusProperty,
            new Binding(nameof(CanvasOutlineRowViewModel.Status))));
        style.Setters.Add(new Setter(
            AutomationProperties.HelpTextProperty,
            new Binding(nameof(CanvasOutlineRowViewModel.Hint))));
        return style;
    }

    private static HierarchicalDataTemplate RowTemplate()
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(
            TextBlock.TextProperty,
            new Binding(nameof(CanvasOutlineRowViewModel.Display)));
        text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        var template = new HierarchicalDataTemplate(typeof(CanvasOutlineRowViewModel))
        {
            ItemsSource = new Binding(nameof(CanvasOutlineRowViewModel.Children)),
            VisualTree = text,
        };
        template.Seal();
        return template;
    }
}
