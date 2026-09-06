// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>
/// One line of the Connections tree (contracts B-6, B-8): a GROUP row
/// ("Linked from, N notes" / "Links to, N notes"), a group's EMPTY row
/// ("Nothing links here." / "This note links to nothing."), or a
/// CONNECTION row named by core's row copy through the relay, its
/// ItemStatus the badge, its HelpText its activation's hint. The
/// occurrence id is the identity (B-6).
/// </summary>
internal sealed class ConnectionsRowViewModel : BindableBase
{
    private bool _isExpanded;
    private bool _isSelected;

    private ConnectionsRowViewModel(string id, string name, string status, string hint, bool isGroup, bool isEmptyMarker)
    {
        Id = id;
        Name = name;
        Status = status;
        Hint = hint;
        IsGroup = isGroup;
        IsEmptyMarker = isEmptyMarker;
    }

    internal static ConnectionsRowViewModel ForGroup(string id, string title, int count) =>
        new(id, ConnectionsPhrase.GroupHeader(title, count), string.Empty, string.Empty, isGroup: true, isEmptyMarker: false)
        {
            IsExpanded = true,
        };

    internal static ConnectionsRowViewModel ForEmpty(string id, string text) =>
        new(id, text, string.Empty, string.Empty, isGroup: false, isEmptyMarker: true);

    internal static ConnectionsRowViewModel ForRow(ConnectionsLeafViewModel model, GraphConnectionRow row, string? snippet) =>
        new(row.Id, model.RowName(row), Badge(row), model.RowHint(row), isGroup: false, isEmptyMarker: false)
        {
            Row = row,
            Snippet = snippet,
        };

    /// <summary>B-8: "Unresolved" before "Embed" before "Attachment"
    /// (`ConnectionsPanel.swift:311–315`); the status carries the role the
    /// mac's badge carries, hidden from the tree as the mac hides it.</summary>
    internal static string Badge(GraphConnectionRow row)
    {
        if (row.Kind == GraphNodeKind.Ghost)
        {
            return ConnectionsPhrase.BadgeUnresolved;
        }
        if (row.EmbedOnly)
        {
            return ConnectionsPhrase.BadgeEmbed;
        }
        return row.Kind == GraphNodeKind.Attachment ? ConnectionsPhrase.BadgeAttachment : string.Empty;
    }

    public string Id { get; }

    public GraphConnectionRow? Row { get; private init; }

    /// <summary>B-7: the depth-one snippet overlaid by exact path.</summary>
    public string? Snippet { get; private init; }

    public bool IsGroup { get; }

    public bool IsEmptyMarker { get; }

    public string Name { get; }

    public string Status { get; }

    public string Hint { get; }

    public string Display => Name;

    public ObservableCollection<ConnectionsRowViewModel> Children { get; } = [];

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

    internal Action<ConnectionsRowViewModel>? Activate { get; set; }

    internal void RaiseActivate() => Activate?.Invoke(this);
}

/// <summary>The tree container: every item is a
/// <see cref="ConnectionsTreeItem"/> carrying Invoke (B-9, B-13).</summary>
internal sealed class ConnectionsTree : TreeView
{
    protected override DependencyObject GetContainerForItemOverride() => new ConnectionsTreeItem();

    protected override bool IsItemItsOwnContainerOverride(object item) => item is ConnectionsTreeItem;

    protected override AutomationPeer OnCreateAutomationPeer() => new ConnectionsTreeAutomationPeer(this);
}

/// <summary>The tree's peer, overriding the item-peer factory: a
/// container peer's patterns are NOT what AT reads — WPF projects each
/// row as a data peer (`CanvasOutlineView.cs:188–197`).</summary>
internal sealed class ConnectionsTreeAutomationPeer : TreeViewAutomationPeer
{
    public ConnectionsTreeAutomationPeer(ConnectionsTree owner)
        : base(owner)
    {
    }

    protected override ItemAutomationPeer CreateItemAutomationPeer(object item) =>
        new ConnectionsRowDataPeer(item, this, null);
}

/// <summary>The peer a screen reader reads for a row: WPF's tree data
/// item peer (SelectionItem, ExpandCollapse, ScrollItem) plus Invoke.</summary>
internal sealed class ConnectionsRowDataPeer : TreeViewDataItemAutomationPeer, IInvokeProvider
{
    public ConnectionsRowDataPeer(object item, ItemsControlAutomationPeer itemsControlAutomationPeer, TreeViewDataItemAutomationPeer? parentDataItemAutomationPeer)
        : base(item, itemsControlAutomationPeer, parentDataItemAutomationPeer)
    {
    }

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);

    public void Invoke() => (Item as ConnectionsRowViewModel)?.RaiseActivate();
}

internal sealed class ConnectionsTreeItem : TreeViewItem
{
    protected override DependencyObject GetContainerForItemOverride() => new ConnectionsTreeItem();

    protected override bool IsItemItsOwnContainerOverride(object item) => item is ConnectionsTreeItem;

    protected override AutomationPeer OnCreateAutomationPeer() => new ConnectionsTreeItemAutomationPeer(this);

    internal void InvokeRow() => (DataContext as ConnectionsRowViewModel)?.RaiseActivate();
}

internal sealed class ConnectionsTreeItemAutomationPeer : TreeViewItemAutomationPeer, IInvokeProvider
{
    public ConnectionsTreeItemAutomationPeer(ConnectionsTreeItem owner)
        : base(owner)
    {
    }

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);

    /// <summary>NESTED rows are projected by THIS peer: the same
    /// Invoke-carrying data peer, one level down (`:285–287`).</summary>
    protected override ItemAutomationPeer CreateItemAutomationPeer(object item) =>
        new ConnectionsRowDataPeer(item, this, EventsSource as TreeViewDataItemAutomationPeer);

    public void Invoke() => ((ConnectionsTreeItem)Owner).InvokeRow();
}

/// <summary>
/// W6-2 PR B, slice B1 (#746): the Connections leaf's view — the heading,
/// the summary region, the depth control, the state label under Term 9's
/// focus anchor, and the tree in the outline's shape (B-13): patterns on
/// the data peer, Standard virtualisation, the model released on null,
/// the view-local state cleared on a root change and on a collapse (B-6,
/// Term 2), Term 9's entry line posted once per entry.
/// </summary>
internal sealed class ConnectionsLeafView : UserControl
{
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(
            nameof(Model),
            typeof(ConnectionsLeafViewModel),
            typeof(ConnectionsLeafView),
            new PropertyMetadata(null, OnModelChanged));

    private const string IncomingGroupId = "group:in";
    private const string OutgoingGroupId = "group:out";

    private readonly ConnectionsTree _tree;
    private readonly ContextMenu _rowMenu = new();
    private readonly TextBlock _heading;
    private readonly TextBlock _summary;
    private readonly ComboBox _depth;
    private readonly TextBlock _state;
    private readonly Border _anchor;
    private readonly ObservableCollection<ConnectionsRowViewModel> _roots = [];
    private readonly Dictionary<string, ConnectionsRowViewModel> _byOccurrence = new(StringComparer.Ordinal);
    private readonly Dictionary<ConnectionsRowViewModel, ConnectionsRowViewModel> _parentOf = [];

    /// <summary>This VIEW's expansion memory, keyed by occurrence id and
    /// scoped by the root and the mount (B-6, Term 2): cleared on a root
    /// change and a collapse, pruned on a same-root refresh.</summary>
    private readonly Dictionary<string, bool> _expansion = new(StringComparer.Ordinal);
    private string? _pendingFocus;
    private int _seenViewStateEpoch;

    /// <summary>The presentation the rows were last built from: a render
    /// for the SAME record — the install's three notifications, a
    /// selection change, a depth notification — syncs and does not
    /// rebuild, so the containers (and the UIA elements over them) live
    /// as long as the record does.</summary>
    private ConnectionsPublication? _renderedPublication;
    private bool _syncingSelection;
    private bool _syncingDepth;

    public ConnectionsLeafView()
    {
        _heading = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
        };
        AutomationProperties.SetHeadingLevel(_heading, AutomationHeadingLevel.Level2);
        AutomationProperties.SetAutomationId(_heading, "ConnectionsHeading");

        _summary = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
        AutomationProperties.SetAutomationId(_summary, "ConnectionsSummary");

        _depth = new ComboBox { Margin = new Thickness(0, 8, 0, 8), ItemsSource = ConnectionsPhrase.DepthTags };
        AutomationProperties.SetAutomationId(_depth, "ConnectionsDepth");
        AutomationProperties.SetName(_depth, ConnectionsPhrase.DepthName);
        AutomationProperties.SetHelpText(_depth, ConnectionsPhrase.DepthHint);
        _depth.SelectionChanged += OnDepthSelectionChanged;

        _state = new TextBlock { TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetAutomationId(_state, "ConnectionsStateText");

        // Term 9: the anchor — focusable when there is nothing to read.
        _anchor = new Border { Focusable = true, Child = _state, Margin = new Thickness(0, 8, 0, 8) };
        AutomationProperties.SetAutomationId(_anchor, "ConnectionsLeaf");
        AutomationProperties.SetName(_anchor, ConnectionsPhrase.Title);
        _anchor.GotKeyboardFocus += (_, e) => OnFocusEntered(e);

        _tree = new ConnectionsTree
        {
            ItemsSource = _roots,
            ItemTemplate = RowTemplate(),
            ItemContainerStyle = RowContainerStyle(),
            BorderThickness = new Thickness(0),
        };
        ScrollViewer.SetCanContentScroll(_tree, true);
        VirtualizingStackPanel.SetIsVirtualizing(_tree, true);
        // Standard, NOT Recycling (B-13): a recycled container re-uses one
        // automation peer for different rows.
        VirtualizingStackPanel.SetVirtualizationMode(_tree, VirtualizationMode.Standard);
        AutomationProperties.SetAutomationId(_tree, "ConnectionsTree");
        AutomationProperties.SetName(_tree, ConnectionsPhrase.Title);
        _tree.ItemContainerGenerator.StatusChanged += (_, _) =>
        {
            if (_tree.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
            {
                return;
            }
            // POSTED, not called: the generator's own pass is running.
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, () => DeliverPendingFocus());
        };
        _tree.SelectedItemChanged += OnTreeSelectionChanged;
        _tree.KeyDown += OnTreeKeyDown;
        _tree.MouseDoubleClick += OnTreeDoubleClick;
        _tree.GotKeyboardFocus += (_, e) => OnFocusEntered(e);
        // The row menu EXISTS from construction and is MUTATED per request,
        // never replaced (the grid's rule, `AccessibleDataGrid`): WPF decides
        // whether to open on the Menu key, Shift+F10 or a right-click by the
        // menu that exists when the request arrives, and a menu first
        // assigned inside ContextMenuOpening is too late for that request —
        // the Menu key on a row opened nothing (W6-2 PR B2's journey, T6).
        _tree.ContextMenu = _rowMenu;
        _tree.AddHandler(ContextMenuOpeningEvent, new ContextMenuEventHandler(OnRowContextMenuOpening), handledEventsToo: false);

        var panel = new DockPanel();
        DockPanel.SetDock(_heading, Dock.Top);
        DockPanel.SetDock(_summary, Dock.Top);
        DockPanel.SetDock(_depth, Dock.Top);
        DockPanel.SetDock(_anchor, Dock.Top);
        panel.Children.Add(_heading);
        panel.Children.Add(_summary);
        panel.Children.Add(_depth);
        panel.Children.Add(_anchor);
        panel.Children.Add(_tree);
        Content = panel;
        Render();
    }

    public ConnectionsLeafViewModel? Model
    {
        get => (ConnectionsLeafViewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    internal TreeView TreeForTests => _tree;

    /// <summary>The tree's persistent row menu (the grid's rule).</summary>
    internal ContextMenu RowMenuForTests => _rowMenu;

    /// <summary>What a request for the row's menu leaves in the persistent
    /// menu: the titles, in core's order; empty when the row has none.</summary>
    internal IReadOnlyList<string> RebuildRowMenuForTests(ConnectionsRowViewModel row) =>
        RebuildRowMenu(row) ? [.. _rowMenu.Items.OfType<MenuItem>().Select(item => (string)item.Header)] : [];

    internal IReadOnlyList<ConnectionsRowViewModel> RootsForTests => _roots;

    internal ComboBox DepthForTests => _depth;

    internal TextBlock StateForTests => _state;

    internal TextBlock SummaryForTests => _summary;

    internal TextBlock HeadingForTests => _heading;

    internal Border AnchorForTests => _anchor;

    internal IReadOnlyDictionary<string, bool> ExpansionForTests => _expansion;

    internal string? PendingFocusForTests => _pendingFocus;

    internal bool IsKeyboardFocusInside => IsKeyboardFocusWithin;

    /// <summary>Term 9: the anchor — the tree's first row when it has rows,
    /// else the host's own focusable element; the window's right-pane
    /// boundary lands here when the leaf is active.</summary>
    public bool FocusAnchor()
    {
        if (_tree.Visibility == Visibility.Visible && _roots.Count > 0)
        {
            ConnectionsRowViewModel first = _tree.SelectedItem as ConnectionsRowViewModel ?? _roots[0];
            return RealizeContainer(first) is { } container && container.Focus();
        }
        return _anchor.Focus();
    }

    private static void OnModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var view = (ConnectionsLeafView)sender;
        if (e.OldValue is ConnectionsLeafViewModel old)
        {
            old.PublicationInstalled -= view.OnPublicationInstalled;
            old.PropertyChanged -= view.OnModelPropertyChanged;
        }
        if (e.NewValue is ConnectionsLeafViewModel model)
        {
            model.PublicationInstalled += view.OnPublicationInstalled;
            model.PropertyChanged += view.OnModelPropertyChanged;
            view._seenViewStateEpoch = model.ViewStateEpoch;
            view.ClearViewState();
            view.Render();
        }
        else
        {
            // B-13: on null the view drops rows, indexes, selection and
            // pending focus and the delegates die with them.
            view.ClearViewState();
            view.DropRows();
            view.Render();
        }
    }

    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ConnectionsLeafViewModel.ViewStateEpoch):
                // B-6 / Term 2: a root change or a collapse CLEARS selection,
                // expansion and pending focus — and only those; a same-root
                // refresh prunes (Rebuild), a vanished selection is not a
                // clear, and a notification without a changed value is not
                // one either.
                if (Model is { } epochModel && epochModel.ViewStateEpoch != _seenViewStateEpoch)
                {
                    _seenViewStateEpoch = epochModel.ViewStateEpoch;
                    ClearViewState();
                }
                Render();
                break;
            case nameof(ConnectionsLeafViewModel.Root):
            case nameof(ConnectionsLeafViewModel.SelectedOccurrence):
            case nameof(ConnectionsLeafViewModel.Depth):
            case nameof(ConnectionsLeafViewModel.IsStale):
                Render();
                break;
        }
    }

    private void OnPublicationInstalled(ConnectionsPublicationInstall install) => Render();

    private void ClearViewState()
    {
        _expansion.Clear();
        _pendingFocus = null;
        SetSelectedRow(null);
        // The next render rebuilds: the rows come back expanded, as the
        // mac's re-mounted panel does.
        _renderedPublication = null;
    }

    /// <summary>The expansion handler each row carries, kept so the drop
    /// can detach it.</summary>
    private readonly Dictionary<ConnectionsRowViewModel, PropertyChangedEventHandler> _expansionHandlers = [];

    /// <summary>Dropped rows are INERT (codex post-implementation pass 2,
    /// IPB-6): a screen reader's cached automation peer can still hold a
    /// row the view dropped after a root change, a refresh or a release,
    /// so its activation delegate and its expansion handler go with it —
    /// it can neither act on the document nor write the view's expansion
    /// memory. The document refuses a row its tree no longer lists as the
    /// second wall.</summary>
    private void DropRows()
    {
        _rowsReplaced = true;
        foreach (ConnectionsRowViewModel root in _roots)
        {
            Release(root);
        }
        _roots.Clear();
        _byOccurrence.Clear();
        _parentOf.Clear();
    }

    private void Release(ConnectionsRowViewModel row)
    {
        row.Activate = null;
        if (_expansionHandlers.Remove(row, out PropertyChangedEventHandler? handler))
        {
            row.PropertyChanged -= handler;
        }
        foreach (ConnectionsRowViewModel child in row.Children)
        {
            Release(child);
        }
    }

    // --- Rendering -------------------------------------------------------------------

    /// <summary>A render that REPLACES the rows (a root change, a same-root
    /// refresh, a state) under keyboard focus would drop focus to the
    /// window with the row it was on: the leaf keeps focus inside itself —
    /// on its anchor, the selected or first row when it has rows, the
    /// state's host otherwise — so a re-root's or Back's request that landed
    /// before the load applied (Term 9's line) still ends in the leaf
    /// (IGL-3; W6-2 PR B2's journey, T6: Back's pop issues its load after
    /// its open, so the boundary request lands on the rows the pop
    /// replaces).</summary>
    private void Render()
    {
        bool inside = _tree.IsKeyboardFocusWithin || _anchor.IsKeyboardFocusWithin;
        _rowsReplaced = false;
        RenderCore();
        if (inside && _rowsReplaced)
        {
            KeepFocusInside();
        }
    }

    private bool _rowsReplaced;

    /// <summary>The anchor, at once, then again after the layout pass and
    /// after the generator's when the first attempt could not land: the
    /// selected or first row's container REALIZED (the anchor's own route,
    /// <see cref="FocusAnchor"/> — a group row's id is no occurrence id, so
    /// the pending-focus delivery cannot carry it), or the state's host. The
    /// containers of a tree just made visible are not always there to
    /// realize at once — the journey's pop landed on the window one run in
    /// three without the retries — and WPF leaves keyboard focus on the
    /// element that collapsed, so focus on an invisible element counts as
    /// lost.</summary>
    private void KeepFocusInside()
    {
        if (TryFocusInside())
        {
            return;
        }
        RetryFocusInside(attempts: 3);
    }

    private void RetryFocusInside(int attempts)
    {
        if (attempts <= 0)
        {
            return;
        }
        _ = Dispatcher.BeginInvoke(attempts == 3 ? DispatcherPriority.Loaded : DispatcherPriority.Background, () =>
        {
            bool inside = IsKeyboardFocusWithin && Keyboard.FocusedElement is UIElement { IsVisible: true };
            if (!inside && !TryFocusInside())
            {
                RetryFocusInside(attempts - 1);
            }
        });
    }

    private bool TryFocusInside()
    {
        if (_tree.Visibility == Visibility.Visible && _roots.Count > 0)
        {
            ConnectionsRowViewModel target = _tree.SelectedItem as ConnectionsRowViewModel ?? _roots[0];
            return RealizeContainer(target) is { } container && container.Focus();
        }
        return _anchor.Focus();
    }

    private void RenderCore()
    {
        ConnectionsLeafViewModel? model = Model;
        if (model is null)
        {
            _heading.Text = ConnectionsPhrase.Title;
            _summary.Visibility = Visibility.Collapsed;
            _depth.IsEnabled = false;
            ShowState(ConnectionsPhrase.NoNote, ConnectionsPhrase.NoNote);
            return;
        }
        SyncDepthControl(model);
        ConnectionsPublication publication = model.Publication;
        _heading.Text = model.Root is { } root ? System.IO.Path.GetFileName(root) : ConnectionsPhrase.Title;
        _depth.IsEnabled = model.Root is not null;
        if (model.Root is null || publication.State == ConnectionsLoadState.NoNote)
        {
            _summary.Visibility = Visibility.Collapsed;
            ShowState(ConnectionsPhrase.NoNote, ConnectionsPhrase.NoNote);
            return;
        }
        if (model.IsStale || publication.State == ConnectionsLoadState.Loading)
        {
            _summary.Visibility = Visibility.Collapsed;
            ShowState(ConnectionsPhrase.LoadingVisible, ConnectionsPhrase.LoadingAccessible);
            return;
        }
        if (publication.State == ConnectionsLoadState.Error)
        {
            _summary.Visibility = Visibility.Collapsed;
            string error = ConnectionsPhrase.Error(publication.Failure ?? string.Empty);
            ShowState(error, error);
            return;
        }
        GraphConnectionsTree tree = publication.Tree!;
        _summary.Text = GraphAnnouncer.RenderLabel(new GraphA11yEvent.GraphNeighborhoodSummary(tree.SummaryCounts));
        _summary.Visibility = Visibility.Visible;
        if (!publication.HasRows)
        {
            ShowState(ConnectionsPhrase.Empty, ConnectionsPhrase.Empty);
            return;
        }
        _anchor.Visibility = Visibility.Collapsed;
        _tree.Visibility = Visibility.Visible;
        if (!ReferenceEquals(_renderedPublication, publication))
        {
            Rebuild(model, tree, publication.Bundle);
            _renderedPublication = publication;
        }
        else if (model.SelectedOccurrence is { } selected && _byOccurrence.TryGetValue(selected, out ConnectionsRowViewModel? row))
        {
            SetSelectedRow(row);
        }
    }

    private void ShowState(string visible, string accessible)
    {
        _state.Text = visible;
        AutomationProperties.SetName(_state, accessible);
        AutomationProperties.SetName(_anchor, accessible);
        _anchor.Visibility = Visibility.Visible;
        _tree.Visibility = Visibility.Collapsed;
        DropRows();
        _renderedPublication = null;
    }

    private void SyncDepthControl(ConnectionsLeafViewModel model)
    {
        _syncingDepth = true;
        try
        {
            // The tags are core's window in order; the depth shows as the tag
            // at its position. No host arithmetic decides the DEPTH here: the
            // model's SetDepth receives the chosen tag's position converted,
            // the one view producer B-19 (v) names.
            int index = checked((int)model.Depth) - 1;
            _depth.SelectedIndex = index >= 0 && index < ConnectionsPhrase.DepthTags.Length ? index : -1;
        }
        finally
        {
            _syncingDepth = false;
        }
    }

    private void OnDepthSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingDepth || Model is not { } model || _depth.SelectedIndex < 0)
        {
            return;
        }
        model.SetDepth(checked((uint)(_depth.SelectedIndex + 1)));
    }

    private void Rebuild(ConnectionsLeafViewModel model, GraphConnectionsTree tree, NoteLoadBundle? bundle)
    {
        _rowsReplaced = true;
        var snippetsIn = new Dictionary<string, string>(StringComparer.Ordinal);
        var snippetsOut = new Dictionary<string, string>(StringComparer.Ordinal);
        if (bundle is not null)
        {
            foreach (Backlink backlink in bundle.Backlinks.Items)
            {
                if (backlink.Snippet.Length > 0)
                {
                    snippetsIn[backlink.SourcePath] = backlink.Snippet;
                }
            }
            foreach (OutgoingLink link in bundle.OutgoingLinks)
            {
                if (link.TargetPath is { } target && link.Snippet.Length > 0)
                {
                    snippetsOut[target] = link.Snippet;
                }
            }
        }
        string? selected = model.SelectedOccurrence;
        var liveIds = new HashSet<string>(StringComparer.Ordinal);
        DropRows();
        _roots.Add(BuildGroup(model, IncomingGroupId, ConnectionsPhrase.IncomingTitle, ConnectionsPhrase.IncomingEmpty, tree.Incoming, snippetsIn, liveIds));
        _roots.Add(BuildGroup(model, OutgoingGroupId, ConnectionsPhrase.OutgoingTitle, ConnectionsPhrase.OutgoingEmpty, tree.Outgoing, snippetsOut, liveIds));
        // B-6: a same-root refresh PRUNES expansion and pending focus to the
        // ids the new tree carries.
        foreach (string id in _expansion.Keys.Where(id => !liveIds.Contains(id)).ToList())
        {
            _expansion.Remove(id);
        }
        if (_pendingFocus is { } pending && !liveIds.Contains(pending))
        {
            _pendingFocus = null;
        }
        if (selected is not null && _byOccurrence.TryGetValue(selected, out ConnectionsRowViewModel? row))
        {
            SetSelectedRow(row);
        }
    }

    private ConnectionsRowViewModel BuildGroup(
        ConnectionsLeafViewModel model, string id, string title, string empty,
        GraphConnectionRow[] rows, Dictionary<string, string> snippets, HashSet<string> liveIds)
    {
        int firstHops = rows.Count(row => row.Level == 1);
        ConnectionsRowViewModel group = ConnectionsRowViewModel.ForGroup(id, title, firstHops);
        group.Activate = g => g.IsExpanded = !g.IsExpanded;
        if (rows.Length == 0)
        {
            group.Children.Add(ConnectionsRowViewModel.ForEmpty(id + ":empty", empty));
            return group;
        }
        // The mac's `nest` (`ConnectionsPanel.swift:406–435`): pre-order
        // with parent ids — a stack of open ancestors nests each row under
        // the nearest open parent in one pass; the host reorders nothing.
        var stack = new List<(ConnectionsRowViewModel Item, uint Level)>();
        foreach (GraphConnectionRow row in rows)
        {
            while (stack.Count > 0 && stack[^1].Level >= row.Level)
            {
                stack.RemoveAt(stack.Count - 1);
            }
            string? snippet = row.Level == 1 && row.Path is { } path && snippets.TryGetValue(path, out string? s) ? s : null;
            ConnectionsRowViewModel item = ConnectionsRowViewModel.ForRow(model, row, snippet);
            item.Activate = r => model.Activate(r.Row!, newTab: (Keyboard.Modifiers & ModifierKeys.Control) != 0);
            item.IsExpanded = !_expansion.TryGetValue(row.Id, out bool expanded) || expanded;
            PropertyChangedEventHandler remember = (_, args) =>
            {
                if (args.PropertyName == nameof(ConnectionsRowViewModel.IsExpanded))
                {
                    _expansion[row.Id] = item.IsExpanded;
                }
            };
            item.PropertyChanged += remember;
            _expansionHandlers[item] = remember;
            ConnectionsRowViewModel parent = stack.Count > 0 ? stack[^1].Item : group;
            parent.Children.Add(item);
            _parentOf[item] = parent;
            _byOccurrence[row.Id] = item;
            liveIds.Add(row.Id);
            stack.Add((item, row.Level));
        }
        return group;
    }

    // --- Selection, focus, keys, the menu ---------------------------------------------

    private void SetSelectedRow(ConnectionsRowViewModel? row)
    {
        foreach (ConnectionsRowViewModel existing in _byOccurrence.Values)
        {
            if (!ReferenceEquals(existing, row))
            {
                existing.IsSelected = false;
            }
        }
        if (row is not null)
        {
            row.IsSelected = true;
        }
    }

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_syncingSelection || Model is not { } model)
        {
            return;
        }
        if (e.NewValue is ConnectionsRowViewModel { Row: not null } row)
        {
            model.SelectedOccurrence = row.Id;
        }
    }

    private void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (Model is not { } model)
        {
            return;
        }
        ConnectionsRowViewModel? current = _tree.SelectedItem as ConnectionsRowViewModel;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Return && current is { Row: not null })
        {
            model.Activate(current.Row, newTab: (Keyboard.Modifiers & ModifierKeys.Control) != 0);
            e.Handled = true;
        }
        else if (key == Key.Return && current is { IsGroup: true })
        {
            current.IsExpanded = !current.IsExpanded;
            e.Handled = true;
        }
        else if (key is Key.Up or Key.Down && alt)
        {
            // Alt+Up / Alt+Down: the first row of the previous / next group
            // (the mac's `jumpSection`, `:296–306`).
            JumpGroup(key == Key.Down);
            e.Handled = true;
        }
        // The Menu key and Shift+F10 are WPF's: its popup service raises
        // ContextMenuOpening on the focused row and opens the tree's
        // persistent menu (the grid's route, `AccessibleDataGrid`). B1 opened
        // a menu of its own here as well and marked the key handled, and the
        // two answers to the one key left NO menu open on the Menu key
        // (W6-2 PR B2's journey, T6).
    }

    private void JumpGroup(bool down)
    {
        ConnectionsRowViewModel[] anchors = [.. _roots.Where(g => g.Children.Count > 0).Select(g => g.Children[0])];
        if (anchors.Length == 0)
        {
            return;
        }
        ConnectionsRowViewModel target = down ? anchors[^1] : anchors[0];
        _pendingFocus = target.Id;
        DeliverPendingFocus();
    }

    private void OnTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_tree.SelectedItem is ConnectionsRowViewModel { Row: not null } row)
        {
            Model?.Activate(row.Row, newTab: false);
        }
    }

    /// <summary>WPF's request for the tree's menu (the Menu key, Shift+F10,
    /// a right-click): a POINTER request targets the row under the cursor
    /// and gets no menu over a group header or empty chrome; the keyboard
    /// request (cursor coordinates -1) targets the selected row. The
    /// persistent menu's items are rebuilt from that row.</summary>
    private void OnRowContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        bool pointerRequest = e.CursorLeft >= 0 || e.CursorTop >= 0;
        ConnectionsRowViewModel? row = pointerRequest
            ? (e.OriginalSource as FrameworkElement)?.DataContext as ConnectionsRowViewModel
            : _tree.SelectedItem as ConnectionsRowViewModel;
        if (Model is null || row?.Row is null || !RebuildRowMenu(row))
        {
            e.Handled = true;
        }
    }

    /// <summary>The persistent menu MUTATED to the row's actions (never
    /// replaced): the items of <see cref="BuildRowMenu"/> moved across.</summary>
    private bool RebuildRowMenu(ConnectionsRowViewModel row)
    {
        ContextMenu built = BuildRowMenu(row);
        _rowMenu.Items.Clear();
        while (built.Items.Count > 0)
        {
            object item = built.Items[0];
            built.Items.RemoveAt(0);
            _ = _rowMenu.Items.Add(item);
        }
        return _rowMenu.Items.Count > 0;
    }

    /// <summary>B-9: core's action inventory for the row's kind, in
    /// core's order; admission the host's; the disabled Show connections
    /// carries B-D6's reason as its HelpText — the row's hint does not.</summary>
    internal ContextMenu BuildRowMenu(ConnectionsRowViewModel row)
    {
        ConnectionsLeafViewModel model = Model!;
        GraphConnectionRow core = row.Row!;
        var menu = new ContextMenu();
        foreach (GraphRowActionSpec spec in model.ActionSpecs(core.Kind))
        {
            var item = new MenuItem { Header = spec.Title, IsEnabled = model.IsActionEnabled(spec.Action, core) };
            string? reason = model.ActionDisabledReason(spec.Action);
            AutomationProperties.SetHelpText(item, reason ?? spec.Title);
            GraphRowAction action = spec.Action;
            // A cached menu item acts only through a row the view still holds
            // (codex post-implementation pass 3, IPB-12): a collapse and a
            // root change re-render, releasing the rows they drop, so a
            // dropped row's delegate is gone; and the document refuses a row
            // its current root's tree does not list.
            item.Click += (_, _) =>
            {
                if (row.Activate is null || !ReferenceEquals(Model, model))
                {
                    return;
                }
                model.Execute(action, core);
            };
            menu.Items.Add(item);
        }
        return menu;
    }

    /// <summary>W6-2 PR B2 (B2-4): the leaf BODY owns Back's chord, tunnelling
    /// — the mac's panel-level key, so it works from the depth control and
    /// the anchor as from the tree — and marks the event handled only when
    /// the workspace popped; with nothing to pop the chord falls through to
    /// whatever owns it next (the mac's <c>.ignored</c>).</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPreviewKeyDown(e);
        if (!e.Handled && TryHandleBackChord(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers))
        {
            e.Handled = true;
        }
    }

    /// <summary>The key owner's decision, for the facts: handled iff the
    /// chord is Back's and the workspace popped.</summary>
    internal bool TryHandleBackChord(Key key, ModifierKeys modifiers) =>
        IsTheBackChord(key, modifiers) && Model?.BackFromSurface?.Invoke() == true;

    /// <summary>Back's chord, matched EXACTLY (B2-D11): Control alone, so
    /// Ctrl+Shift+[ (Previous Tab) and Ctrl+Alt+[ (the sidebar's history)
    /// keep their owners. The chord table's scope scrape reads this body.</summary>
    private static bool IsTheBackChord(Key key, ModifierKeys modifiers) =>
        key == Key.OemOpenBrackets && modifiers == ModifierKeys.Control;

    private void OnFocusEntered(KeyboardFocusChangedEventArgs e)
    {
        // Entry only: focus arriving from OUTSIDE the leaf's host — a move
        // between the leaf's own rows or controls is not an entry.
        if (e.OldFocus is DependencyObject old && (ReferenceEquals(old, this) || IsAncestorOf(old)))
        {
            return;
        }
        Model?.FocusEntered();
    }

    /// <summary>Deliver a pending focus to its row, REALISING the container
    /// first (`CanvasOutlineView.cs:431–453`); a failure leaves it pending
    /// for the next realisation.</summary>
    private void DeliverPendingFocus()
    {
        if (_pendingFocus is not { } id || !_byOccurrence.TryGetValue(id, out ConnectionsRowViewModel? target))
        {
            return;
        }
        _syncingSelection = true;
        try
        {
            SetSelectedRow(target);
            if (RealizeContainer(target) is { } container && container.Focus())
            {
                _pendingFocus = null;
                if (Model is { } model)
                {
                    model.SelectedOccurrence = target.Id;
                }
            }
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    /// <summary>The container for a row at any depth, realised if it can
    /// be — the outline's walk (`CanvasOutlineView.cs:530–586`). Null when
    /// the panel would not make it.</summary>
    internal ConnectionsTreeItem? RealizeContainer(ConnectionsRowViewModel target)
    {
        var path = new List<ConnectionsRowViewModel>();
        for (ConnectionsRowViewModel? step = target; step is not null; step = _parentOf.GetValueOrDefault(step))
        {
            path.Add(step);
        }
        path.Reverse();
        ItemsControl parent = _tree;
        ConnectionsTreeItem? container = null;
        foreach (ConnectionsRowViewModel step in path)
        {
            container = RealizeChild(parent, step);
            if (container is null)
            {
                return null;
            }
            if (!ReferenceEquals(step, target))
            {
                step.IsExpanded = true;
                container.UpdateLayout();
            }
            parent = container;
        }
        container?.BringIntoView();
        return container;
    }

    private static ConnectionsTreeItem? RealizeChild(ItemsControl parent, object item)
    {
        parent.ApplyTemplate();
        parent.UpdateLayout();
        if (parent.ItemContainerGenerator.ContainerFromItem(item) is ConnectionsTreeItem ready)
        {
            return ready;
        }
        int index = parent.Items.IndexOf(item);
        if (index < 0)
        {
            return null;
        }
        if (FindVisualChild<VirtualizingStackPanel>(parent) is { } panel)
        {
            panel.BringIndexIntoViewPublic(index);
            parent.UpdateLayout();
        }
        return parent.ItemContainerGenerator.ContainerFromItem(item) as ConnectionsTreeItem;
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

    private static DataTemplate RowTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(StackPanel));
        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetBinding(TextBlock.TextProperty, new Binding(nameof(ConnectionsRowViewModel.Display)));
        factory.AppendChild(name);
        var snippet = new FrameworkElementFactory(typeof(TextBlock));
        snippet.SetBinding(TextBlock.TextProperty, new Binding(nameof(ConnectionsRowViewModel.Snippet)));
        snippet.SetValue(TextBlock.FontSizeProperty, 11.0);
        snippet.SetValue(OpacityProperty, 0.75);
        snippet.SetBinding(VisibilityProperty, new Binding(nameof(ConnectionsRowViewModel.Snippet))
        {
            Converter = new TextToVisibilityConverter(),
        });
        factory.AppendChild(snippet);
        return new HierarchicalDataTemplate(typeof(ConnectionsRowViewModel))
        {
            VisualTree = factory,
            ItemsSource = new Binding(nameof(ConnectionsRowViewModel.Children)),
        };
    }

    private static Style RowContainerStyle()
    {
        var style = new Style(typeof(ConnectionsTreeItem));
        style.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, new Binding(nameof(ConnectionsRowViewModel.IsExpanded)) { Mode = BindingMode.TwoWay }));
        style.Setters.Add(new Setter(TreeViewItem.IsSelectedProperty, new Binding(nameof(ConnectionsRowViewModel.IsSelected)) { Mode = BindingMode.TwoWay }));
        style.Setters.Add(new Setter(AutomationProperties.NameProperty, new Binding(nameof(ConnectionsRowViewModel.Name))));
        style.Setters.Add(new Setter(AutomationProperties.ItemStatusProperty, new Binding(nameof(ConnectionsRowViewModel.Status))));
        style.Setters.Add(new Setter(AutomationProperties.HelpTextProperty, new Binding(nameof(ConnectionsRowViewModel.Hint))));
        style.Setters.Add(new Setter(AutomationProperties.AutomationIdProperty, new Binding(nameof(ConnectionsRowViewModel.Id))));
        return style;
    }

    private sealed class TextToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            value is string { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
