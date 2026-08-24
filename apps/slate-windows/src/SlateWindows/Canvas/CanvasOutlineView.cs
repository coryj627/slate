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
        CanvasOutlineRow row, bool marked) =>
        new(
            row.NodeId,
            CanvasPhrase.CardReference(row.Kind, row.SpeakableName),
            CanvasPhrase.RowStatus(
                row.OrdinalN,
                row.TotalM,
                row.GroupPath.Length > 0 ? row.GroupPath[^1] : null,
                row.ColorName,
                marked),
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

    internal TreeView TreeForTests => _tree;

    internal IReadOnlyList<CanvasOutlineRowViewModel> RootsForTests => _roots;

    /// <summary>Contract A14: opening lands keyboard focus on the first
    /// item; coming back from an opened card lands on the row that
    /// opened it, never the top.</summary>
    internal bool FocusLandingRow()
    {
        CanvasOutlineRowViewModel? target =
            (Model?.LastActivatedNode is { } last
                && _byNode.TryGetValue(last, out CanvasOutlineRowViewModel? restored))
                ? restored
                : _roots.FirstOrDefault();
        if (target is null)
        {
            return false;
        }
        target.IsSelected = true;
        return FocusContainerFor(target);
    }

    private bool FocusContainerFor(CanvasOutlineRowViewModel row)
    {
        if (_tree.ItemContainerGenerator.ContainerFromItem(row)
            is CanvasOutlineItem container)
        {
            return container.Focus();
        }
        return _tree.Focus();
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
    /// tree with one stack pass — the host derives no containment
    /// (R-D; the depth column is 0b-8's tree, already).</summary>
    private void Rebuild()
    {
        _roots.Clear();
        _byNode.Clear();
        _connectionHost = null;
        _connectionCount = 0;
        if (Model is not { } model)
        {
            return;
        }
        var stack = new Stack<(uint Depth, CanvasOutlineRowViewModel Row)>();
        foreach (CanvasOutlineRow row in model.Outline)
        {
            var line = CanvasOutlineRowViewModel.ForNode(
                row, model.Selection.IsMarked(row.NodeId));
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
                // A group with members is expandable; it opens by
                // default so a first read is the whole structure.
                stack.Peek().Row.IsExpanded = true;
            }
            stack.Push((row.Depth, line));
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
        if (_connectionHost is { } previous
            && !string.Equals(previous, model.Selection.Selected, StringComparison.Ordinal)
            && _byNode.TryGetValue(previous, out CanvasOutlineRowViewModel? host))
        {
            for (int index = 0; index < _connectionCount; index++)
            {
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
        _syncingSelection = true;
        try
        {
            line.IsSelected = true;
        }
        finally
        {
            _syncingSelection = false;
        }
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
        // The echo is broken by the flag, not by a value compare: the
        // compare cannot tell "the model already agrees" from "another
        // pane set it to the same node" (contract A12).
        if (line.Neighbor is { } neighbor)
        {
            // Connection lines are navigational, not selectable state.
            model.FollowConnection(neighbor);
            return;
        }
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
