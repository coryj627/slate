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

    /// <summary>The publication these materialized rows came from
    /// (contract C10). Null until the first rebuild.</summary>
    private object? _built;

    /// <summary>The row this view believes is selected — the one whose
    /// TwoWay flag must be cleared before another is set.</summary>
    private CanvasOutlineRowViewModel? _selectedRow;
    private string? _connectionHost;
    private int _connectionCount;
    private bool _syncingSelection;

    public CanvasOutlineView()
    {
        _tree = new CanvasOutlineTree
        {
            ItemsSource = _roots,
            ItemTemplate = RowTemplate(),
            ItemContainerStyle = RowContainerStyle(),
            BorderThickness = new Thickness(0),
        };
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
    internal event Action? DetailRequested;

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

    internal void FocusTree() => _ = _tree.Focus();

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

    private void OnOutlinePublished(object? sender, EventArgs e) => EnsureCurrent();

    /// <summary>
    /// Materialize the CURRENT publication's rows, if these are not
    /// already them (contract C10).
    /// </summary>
    /// <remarks>
    /// Two callers, one question. This view's own subscription asks it
    /// on every publication — and a state-only publication carries the
    /// same unit, so the rebuild is skipped rather than re-running a
    /// 2,000-row tree pass for a banner. The SURFACE asks it before it
    /// renders anything over these rows, which is what makes "the state
    /// on screen and the rows under it came from one publication" a
    /// property of the code rather than of the subscription order.
    /// </remarks>
    internal void EnsureCurrent()
    {
        if (ReferenceEquals(_built, Model?.PublicationToken))
        {
            return;
        }
        Rebuild();
    }

    private void OnSelectionChanged(
        object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CanvasSelection.Selected))
        {
            ApplySelection();
        }
    }

    /// <summary>Core's flat, depth-annotated reading order becomes a
    /// tree with one stack pass — the host derives no containment
    /// (R-D; the depth column is 0b-8's tree, already).</summary>
    private void Rebuild()
    {
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
        var stack = new Stack<(uint Depth, CanvasOutlineRowViewModel Row)>();
        // The FILTERED rows (contract C10): the filter is a view over
        // core's reading order, so the tree is simply built from fewer
        // rows. A surviving child whose containing group did not match is
        // promoted to a root by the same depth-stack pass that nests
        // everything else — indenting it under a parent that is not on
        // screen would be the alternative, and it is worse (CD-45; mac's
        // flat outline has no such case, CD-33).
        bool filtered = model.FilterActive;
        foreach (CanvasOutlineRow row in model.FilteredOutline)
        {
            var line = CanvasOutlineRowViewModel.ForNode(
                row, model.Selection.IsMarked(row.NodeId), filtered);
            line.Activate = ActivateRow;
            while (stack.Count > 0 && stack.Peek().Depth >= row.Depth)
            {
                _ = stack.Pop();
            }
            if (stack.Count == 0)
            {
                _roots.Add(line);
            }
            else
            {
                stack.Peek().Row.Children.Add(line);
                _parentOf[line] = stack.Peek().Row;
                // A group with members is expandable; it opens by
                // default so a first read is the whole structure.
                stack.Peek().Row.IsExpanded = true;
            }
            stack.Push((row.Depth, line));
            _byNode[row.NodeId] = line;
        }
        ApplySelection();
        // Recorded LAST: only a rebuild that finished describes the
        // publication it was asked for.
        _built = Model?.PublicationToken;
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
            if (neighbors.Count > 0)
            {
                // Never hidden behind a collapse the user did not ask
                // for (CD-33).
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
            case CanvasActivation.DetailShown:
                DetailRequested?.Invoke();
                break;
            default:
                break;
        }
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
