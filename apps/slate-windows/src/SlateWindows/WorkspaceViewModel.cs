// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows;

internal enum WorkspaceOpenTarget
{
    CurrentTab,
    NewTab,
    SplitRight,
    SplitDown,
}

internal enum WorkspaceFocusBoundary
{
    Files,
    RightPane,
}

internal enum WorkspaceDirtyNavigationDecision
{
    Cancel,
    Save,
    Discard,
}

internal sealed record WorkspaceLeafOption(string Id, string Title);

internal abstract class BindableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class WorkspaceTabViewModel : BindableBase
{
    private readonly VaultSession _session;
    private readonly Action<WorkspaceTabViewModel>? _documentChanged;
    private string _text = string.Empty;
    private string? _contentHash;
    private bool _isDirty;
    private bool _isMissingFromDisk;
    private string _status = string.Empty;

    public WorkspaceTabViewModel(
        VaultSession session,
        WorkspaceTabState state,
        Action<WorkspaceTabViewModel>? documentChanged = null)
    {
        _session = session;
        _documentChanged = documentChanged;
        Id = state.Id;
        Item = state.Item;
        Mode = state.Mode;
        PropsCollapsed = state.PropsCollapsed;
        ActiveCanvasSurface = state.ActiveCanvasSurface;
        Load();
    }

    public Guid Id { get; }
    public WorkspaceItemState Item { get; private set; }
    public string? Mode { get; }
    public bool? PropsCollapsed { get; }
    public string? ActiveCanvasSurface { get; }
    public string Title => Item.Title;
    public string EditorAutomationName =>
        $"{System.IO.Path.GetFileName(Path)} editor";
    public string Path => Item.Path;
    public bool IsMarkdown => Item.Kind == WorkspaceItemKind.Markdown;
    public bool IsPlaceholder => !IsMarkdown;
    public string KindLabel => Item.Kind switch
    {
        WorkspaceItemKind.Canvas => "Canvas",
        WorkspaceItemKind.Base => "Base",
        WorkspaceItemKind.SavedQuery => "Saved query",
        WorkspaceItemKind.Dashboard => "Dashboard",
        WorkspaceItemKind.Graph => "Graph",
        _ => "Note",
    };
    public string PlaceholderText =>
        $"{KindLabel} is docked in this workspace. Its full surface ships in its owning milestone.";

    public string Text
    {
        get => _text;
        set
        {
            if (SetField(ref _text, value))
            {
                IsDirty = true;
                _documentChanged?.Invoke(this);
            }
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetField(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(DirtyMarker));
            }
        }
    }

    public string DirtyMarker => IsDirty ? " •" : string.Empty;

    public bool IsMissingFromDisk
    {
        get => _isMissingFromDisk;
        private set => SetField(ref _isMissingFromDisk, value);
    }

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public WorkspaceTabState Snapshot() =>
        new(Id, Item, Mode, PropsCollapsed, ActiveCanvasSurface);

    public void ReplaceItem(WorkspaceItemState item)
    {
        Item = item;
        _text = string.Empty;
        _contentHash = null;
        _isDirty = false;
        _status = string.Empty;
        _isMissingFromDisk = false;
        NotifyItemChanged();
        Load();
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(DirtyMarker));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsMissingFromDisk));
    }

    public void RetargetPath(string path)
    {
        Item = Item with { Path = path };
        IsMissingFromDisk = false;
        Status = string.Empty;
        NotifyItemChanged();
    }

    public void InvalidatePath()
    {
        IsMissingFromDisk = true;
        Status = $"{Path} no longer exists on disk. Unsaved editor content is preserved.";
        _documentChanged?.Invoke(this);
    }

    public void MirrorDocumentStateFrom(WorkspaceTabViewModel source)
    {
        if (!IsMarkdown || !source.IsMarkdown || ReferenceEquals(this, source))
        {
            return;
        }

        _text = source._text;
        _contentHash = source._contentHash;
        _isDirty = source._isDirty;
        _isMissingFromDisk = source._isMissingFromDisk;
        _status = source._status;
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(DirtyMarker));
        OnPropertyChanged(nameof(IsMissingFromDisk));
        OnPropertyChanged(nameof(Status));
    }

    public bool Save()
    {
        if (!IsMarkdown || !IsDirty)
        {
            return true;
        }

        try
        {
            SaveReport report = _session.SaveText(Path, Text, _contentHash);
            _contentHash = report.NewContentHash;
            IsDirty = false;
            Status = $"Saved {System.IO.Path.GetFileName(Path)}.";
            _documentChanged?.Invoke(this);
            return true;
        }
        catch (VaultException exception)
        {
            Status = $"Save blocked: {exception.Message}";
            _documentChanged?.Invoke(this);
            return false;
        }
    }

    private void Load()
    {
        if (!IsMarkdown)
        {
            return;
        }

        try
        {
            NotePartsBundle note = _session.ReadNoteParts(Path);
            _text = note.FmSource + note.Body;
            _contentHash = note.ContentHash;
            _isDirty = false;
        }
        catch (VaultException exception)
        {
            Status = $"Could not open {Path}: {exception.Message}";
        }
    }

    private void NotifyItemChanged()
    {
        OnPropertyChanged(nameof(Item));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(EditorAutomationName));
        OnPropertyChanged(nameof(Path));
        OnPropertyChanged(nameof(IsMarkdown));
        OnPropertyChanged(nameof(IsPlaceholder));
        OnPropertyChanged(nameof(KindLabel));
        OnPropertyChanged(nameof(PlaceholderText));
    }
}

internal sealed class WorkspaceGroupViewModel : BindableBase
{
    private readonly WorkspaceViewModel _owner;
    private WorkspaceTabViewModel? _activeTab;

    public WorkspaceGroupViewModel(WorkspaceViewModel owner, Guid id)
    {
        _owner = owner;
        Id = id;
    }

    public Guid Id { get; }
    public WorkspaceViewModel Owner => _owner;
    public ObservableCollection<WorkspaceTabViewModel> Tabs { get; } = [];

    public WorkspaceTabViewModel? ActiveTab
    {
        get => _activeTab;
        set
        {
            if (SetField(ref _activeTab, value))
            {
                _owner.Activate(this, value);
            }
        }
    }

    internal void RestoreActive(WorkspaceTabViewModel? tab)
    {
        _activeTab = tab;
        OnPropertyChanged(nameof(ActiveTab));
    }
}

internal sealed class WorkspacePaneNodeViewModel : BindableBase
{
    private double _weight = 1;

    public WorkspacePaneNodeViewModel(WorkspaceGroupViewModel group)
    {
        Group = group;
    }

    public WorkspacePaneNodeViewModel(string axis)
    {
        Axis = axis;
    }

    public WorkspaceGroupViewModel? Group { get; }
    public string? Axis { get; }
    public bool IsGroup => Group is not null;
    public bool IsSplit => Group is null;
    public bool IsHorizontal => Axis == "horizontal";
    public ObservableCollection<WorkspacePaneNodeViewModel> Children { get; } = [];

    public double Weight
    {
        get => _weight;
        set => SetField(ref _weight, Math.Clamp(
            value,
            WorkspacePersistence.MinGroupWeight,
            1));
    }
}

/// <summary>
/// W1 workspace host: state transitions stay in this model; WPF renders native
/// TabControl peers and recursively arranged split groups.
/// </summary>
internal sealed class WorkspaceViewModel : BindableBase, IDisposable
{
    private readonly record struct PaneRect(double MinX, double MinY, double MaxX, double MaxY)
    {
        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
    }

    private const int ClosedTabCapacity = 20;
    private readonly VaultSession _session;
    private readonly WorkspacePersistence _persistence;
    private readonly Func<IReadOnlyList<string>> _expandedDirectoryPaths;
    private readonly Action<A11yEvent> _announce;
    private readonly Func<WorkspaceTabViewModel, WorkspaceItemState, WorkspaceDirtyNavigationDecision>
        _dirtyNavigationDecision;
    private readonly Func<WorkspaceTabViewModel, WorkspaceDirtyNavigationDecision>
        _dirtyCloseDecision;
    private readonly List<(WorkspaceItemState Item, Guid Group)> _closedTabs = [];
    private WorkspacePaneNodeViewModel _root;
    private WorkspaceGroupViewModel _activeGroup;
    private WorkspaceLeafOption _activeLeaf;
    private bool _isRightPaneVisible = true;
    private bool _restoring;
    private int _persistenceBatchDepth;
    private bool _persistencePending;

    public WorkspaceViewModel(
        VaultSession session,
        string vaultRoot,
        Func<IReadOnlyList<string>> expandedDirectoryPaths,
        Action<A11yEvent> announce,
        Func<WorkspaceTabViewModel, WorkspaceItemState, WorkspaceDirtyNavigationDecision>?
            dirtyNavigationDecision = null,
        Func<WorkspaceTabViewModel, WorkspaceDirtyNavigationDecision>?
            dirtyCloseDecision = null)
    {
        _session = session;
        _persistence = new WorkspacePersistence(vaultRoot);
        _expandedDirectoryPaths = expandedDirectoryPaths;
        _announce = announce;
        _dirtyNavigationDecision = dirtyNavigationDecision
            ?? ((_, _) => WorkspaceDirtyNavigationDecision.Cancel);
        _dirtyCloseDecision = dirtyCloseDecision
            ?? (_ => WorkspaceDirtyNavigationDecision.Cancel);
        _activeLeaf = Leaves[0];
        (_root, _activeGroup) = Restore(_persistence.Load());

        CloseTabCommand = new RelayCommand(
            parameter => RunWorkspaceMutation(() => CloseTab(parameter)),
            parameter => parameter is WorkspaceTabViewModel);
        CloseActiveTabCommand = new RelayCommand(
            _ => RunWorkspaceMutation(() => CloseTab(ActiveGroup.ActiveTab)),
            _ => ActiveGroup.ActiveTab is not null);
        DuplicateTabCommand = new RelayCommand(
            _ => RunWorkspaceMutation(DuplicateActiveTab),
            _ => ActiveGroup.ActiveTab is { Item.Kind: not WorkspaceItemKind.Graph });
        ReopenClosedTabCommand = new RelayCommand(
            _ => RunWorkspaceMutation(ReopenClosedTab),
            _ => _closedTabs.Count > 0);
        MoveTabLeftCommand = new RelayCommand(
            _ => RunWorkspaceMutation(() => MoveActiveTab(-1)),
            _ => CanMoveActiveTab(-1));
        MoveTabRightCommand = new RelayCommand(
            _ => RunWorkspaceMutation(() => MoveActiveTab(1)),
            _ => CanMoveActiveTab(1));
        NextTabCommand = new RelayCommand(
            _ => RunWorkspaceMutation(() => CycleTab(1)),
            _ => ActiveGroup.Tabs.Count > 1);
        PreviousTabCommand = new RelayCommand(
            _ => RunWorkspaceMutation(() => CycleTab(-1)),
            _ => ActiveGroup.Tabs.Count > 1);
        SplitRightCommand = new RelayCommand(
            _ => RunWorkspaceMutation(() => SplitActive("horizontal")),
            _ => CanSplitActive());
        SplitDownCommand = new RelayCommand(
            _ => RunWorkspaceMutation(() => SplitActive("vertical")),
            _ => CanSplitActive());
        ClosePaneCommand = new RelayCommand(
            _ => RunWorkspaceMutation(CloseActivePane),
            _ => Groups.Count > 1);
        FocusPaneLeftCommand = new RelayCommand(_ => FocusDirectionalPane("horizontal", -1), _ => true);
        FocusPaneRightCommand = new RelayCommand(_ => FocusDirectionalPane("horizontal", 1), _ => true);
        FocusPaneAboveCommand = new RelayCommand(_ => FocusDirectionalPane("vertical", -1), _ => true);
        FocusPaneBelowCommand = new RelayCommand(_ => FocusDirectionalPane("vertical", 1), _ => true);
        FocusNextPaneCommand = new RelayCommand(_ => FocusPane(1), _ => Groups.Count > 1);
        FocusPreviousPaneCommand = new RelayCommand(_ => FocusPane(-1), _ => Groups.Count > 1);
        GrowPaneCommand = new RelayCommand(_ => ResizeActivePane(0.05), _ => Groups.Count > 1);
        ShrinkPaneCommand = new RelayCommand(_ => ResizeActivePane(-0.05), _ => Groups.Count > 1);
        SaveActiveCommand = new RelayCommand(_ => SaveActive(), _ => ActiveGroup.ActiveTab?.IsMarkdown == true);
        ToggleRightPaneCommand = new RelayCommand(_ => IsRightPaneVisible = !IsRightPaneVisible, _ => true);
    }

    public event EventHandler<string>? FileOpened;
    public event EventHandler<WorkspaceFocusBoundary>? FocusBoundaryRequested;
    public event EventHandler<WorkspaceGroupViewModel>? EditorPaneFocusRequested;

    public static IReadOnlyList<WorkspaceLeafOption> Leaves { get; } =
    [
        new("outline", "Outline"),
        new("backlinks", "Backlinks"),
        new("outgoingLinks", "Outgoing links"),
        new("connections", "Connections"),
        new("embeds", "Embeds"),
        new("math", "Math"),
        new("code", "Code"),
        new("diagrams", "Diagrams"),
        new("tasks", "Tasks"),
        new("tasksReview", "Tasks Review"),
        new("history", "History"),
        new("citations", "Citations"),
        new("bibliography", "Bibliography"),
        new("queries", "Queries"),
        new("basesDock", "Base dock"),
        new("syncDiagnostics", "Sync"),
    ];
    public IReadOnlyList<WorkspaceLeafOption> LeafOptions => Leaves;

    public WorkspacePaneNodeViewModel Root
    {
        get => _root;
        private set => SetField(ref _root, value);
    }

    public WorkspaceGroupViewModel ActiveGroup
    {
        get => _activeGroup;
        private set
        {
            if (SetField(ref _activeGroup, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public IReadOnlyList<WorkspaceGroupViewModel> Groups => EnumerateGroups(Root).ToArray();
    public bool HasDirtyTabs => Groups.SelectMany(group => group.Tabs).Any(tab => tab.IsDirty);

    public WorkspaceLeafOption ActiveLeaf
    {
        get => _activeLeaf;
        set
        {
            if (value is not null && SetField(ref _activeLeaf, value))
            {
                _announce(new A11yEvent.LeafPanelShown(value.Title));
                Persist();
            }
        }
    }

    public bool IsRightPaneVisible
    {
        get => _isRightPaneVisible;
        set
        {
            if (SetField(ref _isRightPaneVisible, value))
            {
                _announce(value ? new A11yEvent.RightPaneShown() : new A11yEvent.RightPaneHidden());
            }
        }
    }

    public ICommand CloseTabCommand { get; }
    public ICommand CloseActiveTabCommand { get; }
    public ICommand DuplicateTabCommand { get; }
    public ICommand ReopenClosedTabCommand { get; }
    public ICommand MoveTabLeftCommand { get; }
    public ICommand MoveTabRightCommand { get; }
    public ICommand NextTabCommand { get; }
    public ICommand PreviousTabCommand { get; }
    public ICommand SplitRightCommand { get; }
    public ICommand SplitDownCommand { get; }
    public ICommand ClosePaneCommand { get; }
    public ICommand FocusPaneLeftCommand { get; }
    public ICommand FocusPaneRightCommand { get; }
    public ICommand FocusPaneAboveCommand { get; }
    public ICommand FocusPaneBelowCommand { get; }
    public ICommand FocusNextPaneCommand { get; }
    public ICommand FocusPreviousPaneCommand { get; }
    public ICommand GrowPaneCommand { get; }
    public ICommand ShrinkPaneCommand { get; }
    public ICommand SaveActiveCommand { get; }
    public ICommand ToggleRightPaneCommand { get; }

    public void OpenPath(string path, WorkspaceOpenTarget target = WorkspaceOpenTarget.CurrentTab) =>
        RunWorkspaceMutation(() => OpenPathCore(path, target));

    private void OpenPathCore(string path, WorkspaceOpenTarget target)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        WorkspaceItemState item = ItemForPath(path);
        if (TryOpenItem(item, target))
        {
            FileOpened?.Invoke(this, path);
            Persist();
        }
    }

    public void OpenGraph() => RunWorkspaceMutation(() => OpenItem(
        new WorkspaceItemState(WorkspaceItemKind.Graph, "graph:singleton"),
        WorkspaceOpenTarget.NewTab));

    public bool SaveAll()
    {
        bool saved = true;
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            saved &= tab.Save();
        }

        return saved;
    }

    public void RetargetPath(string oldPath, string newPath)
    {
        string source = NormalizeWorkspacePath(oldPath);
        string destination = NormalizeWorkspacePath(newPath);
        if (source.Length == 0 || destination.Length == 0)
        {
            return;
        }

        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (IsPathBacked(tab.Item)
                && TryRetargetPath(tab.Path, source, destination, out string retargeted))
            {
                tab.RetargetPath(retargeted);
            }
        }

        for (int index = 0; index < _closedTabs.Count; index++)
        {
            (WorkspaceItemState item, Guid group) = _closedTabs[index];
            if (IsPathBacked(item)
                && TryRetargetPath(item.Path, source, destination, out string retargeted))
            {
                _closedTabs[index] = (item with { Path = retargeted }, group);
            }
        }

        Persist();
    }

    public void InvalidatePath(string path)
    {
        string invalidated = NormalizeWorkspacePath(path);
        if (invalidated.Length == 0)
        {
            return;
        }

        int affected = 0;
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (IsPathBacked(tab.Item) && IsSameOrDescendantPath(tab.Path, invalidated))
            {
                tab.InvalidatePath();
                affected++;
            }
        }

        _closedTabs.RemoveAll(entry =>
            IsPathBacked(entry.Item) && IsSameOrDescendantPath(entry.Item.Path, invalidated));
        RaiseCommandStates();
        Persist();
        if (affected > 0)
        {
            // W0.5-3 residue: Windows missing-editor availability copy.
            _announce(new A11yEvent.HostComposed(
                $"{System.IO.Path.GetFileName(invalidated)} is missing from disk. Open editor content was preserved.",
                A11yPriority.High));
        }
    }

    public void SelectGroupFromKeyboardFocus(WorkspaceGroupViewModel group)
    {
        if (!Groups.Contains(group) || ReferenceEquals(ActiveGroup, group))
        {
            return;
        }

        ActiveGroup = group;
        AnnounceActivePane();
        Persist();
    }

    public void Dispose()
    {
        Persist();
    }

    internal void Activate(WorkspaceGroupViewModel group, WorkspaceTabViewModel? tab)
    {
        if (_restoring)
        {
            return;
        }

        ActiveGroup = group;
        if (tab is not null)
        {
            int index = group.Tabs.IndexOf(tab) + 1;
            _announce(new A11yEvent.TabFocused(
                Prefix: string.Empty,
                Filename: tab.Title,
                Index: (uint)Math.Max(1, index),
                Count: (uint)group.Tabs.Count));
        }

        RaiseCommandStates();
        Persist();
    }

    private void OpenItem(WorkspaceItemState item, WorkspaceOpenTarget target)
    {
        if (TryOpenItem(item, target))
        {
            Persist();
        }
    }

    private bool TryOpenItem(WorkspaceItemState item, WorkspaceOpenTarget target)
    {
        if (item.Kind == WorkspaceItemKind.Graph && TryFocusGlobalGraph())
        {
            return true;
        }

        if (target is WorkspaceOpenTarget.SplitRight or WorkspaceOpenTarget.SplitDown)
        {
            return AddSplitWithItem(
                item,
                target == WorkspaceOpenTarget.SplitRight ? "horizontal" : "vertical");
        }

        if (target is WorkspaceOpenTarget.CurrentTab or WorkspaceOpenTarget.NewTab)
        {
            WorkspaceTabViewModel? existing = ActiveGroup.Tabs.FirstOrDefault(
                tab => ItemsReferToSameTarget(tab.Item, item));
            if (existing is not null)
            {
                ActiveGroup.ActiveTab = existing;
                RequestActiveEditorFocus();
                return true;
            }

            if (target == WorkspaceOpenTarget.NewTab)
            {
                AddTab(ActiveGroup, item, activate: true);
                RequestActiveEditorFocus();
                return true;
            }
        }

        WorkspaceTabViewModel? active = ActiveGroup.ActiveTab;
        if (active is null)
        {
            AddTab(ActiveGroup, item, activate: true);
        }
        else if (!ItemsReferToSameTarget(active.Item, item))
        {
            if (active.IsDirty)
            {
                WorkspaceDirtyNavigationDecision decision = _dirtyNavigationDecision(active, item);
                if (decision == WorkspaceDirtyNavigationDecision.Cancel
                    || (decision == WorkspaceDirtyNavigationDecision.Save && !active.Save()))
                {
                    return false;
                }
            }

            WorkspaceTabViewModel? peer = FindSamePathTab(item, excluding: active);
            active.ReplaceItem(item);
            if (peer is not null)
            {
                active.MirrorDocumentStateFrom(peer);
            }

            ActiveGroup.ActiveTab = active;
            RaiseCommandStates();
        }

        RequestActiveEditorFocus();
        return true;
    }

    private bool TryFocusGlobalGraph()
    {
        WorkspaceTabViewModel? graph = Groups.SelectMany(group => group.Tabs)
            .FirstOrDefault(tab => tab.Item.Kind == WorkspaceItemKind.Graph);
        if (graph is null)
        {
            return false;
        }

        WorkspaceGroupViewModel owner = Groups.First(group => group.Tabs.Contains(graph));
        ActiveGroup = owner;
        owner.ActiveTab = graph;
        RequestActiveEditorFocus();
        return true;
    }

    private WorkspaceTabViewModel AddTab(
        WorkspaceGroupViewModel group,
        WorkspaceItemState item,
        bool activate)
    {
        WorkspaceTabViewModel? peer = FindSamePathTab(item);
        var tab = new WorkspaceTabViewModel(
            _session,
            new WorkspaceTabState(Guid.NewGuid(), item),
            MirrorSamePathDocumentState);
        if (peer is not null)
        {
            tab.MirrorDocumentStateFrom(peer);
        }

        group.Tabs.Add(tab);
        if (activate)
        {
            group.ActiveTab = tab;
        }

        RaiseCommandStates();
        return tab;
    }

    private bool AddSplitWithItem(WorkspaceItemState item, string axis)
    {
        if (item.Kind == WorkspaceItemKind.Graph)
        {
            _announce(new A11yEvent.GraphOpensSinglePane());
            return false;
        }

        if (Groups.Count >= WorkspacePersistence.MaxGroups)
        {
            // W0.5-3 residue: Windows workspace-cap availability copy.
            _announce(new A11yEvent.HostComposed(
                "Pane limit reached. Slate supports up to six editor panes.",
                A11yPriority.Medium));
            return false;
        }

        var newGroup = new WorkspaceGroupViewModel(this, Guid.NewGuid());
        var newNode = new WorkspacePaneNodeViewModel(newGroup) { Weight = 0.5 };
        WorkspaceTabViewModel newTab = AddTab(newGroup, item, activate: false);

        WorkspacePaneNodeViewModel activeNode = FindNode(Root, ActiveGroup)
            ?? throw new InvalidOperationException("Active group is not in the workspace tree.");
        if (TryFindParent(Root, activeNode, out WorkspacePaneNodeViewModel? parent)
            && parent!.Axis == axis)
        {
            int index = parent.Children.IndexOf(activeNode);
            parent.Children.Insert(index + 1, newNode);
            NormalizeWeights(parent);
        }
        else
        {
            var split = new WorkspacePaneNodeViewModel(axis) { Weight = activeNode.Weight };
            activeNode.Weight = 0.5;
            split.Children.Add(activeNode);
            split.Children.Add(newNode);
            if (parent is null)
            {
                Root = split;
            }
            else
            {
                int index = parent.Children.IndexOf(activeNode);
                parent.Children[index] = split;
            }
        }

        newGroup.ActiveTab = newTab;
        ActiveGroup = newGroup;
        OnPropertyChanged(nameof(Groups));
        RaiseCommandStates();
        RequestActiveEditorFocus();
        return true;
    }

    private void SplitActive(string axis)
    {
        if (ActiveGroup.ActiveTab is WorkspaceTabViewModel tab)
        {
            if (AddSplitWithItem(tab.Item, axis))
            {
                Persist();
            }
        }
    }

    private bool CanSplitActive() =>
        Groups.Count < WorkspacePersistence.MaxGroups
        && ActiveGroup.ActiveTab is { Item.Kind: not WorkspaceItemKind.Graph };

    private void CloseTab(object? parameter)
    {
        if (parameter is not WorkspaceTabViewModel tab)
        {
            return;
        }

        WorkspaceGroupViewModel? group = Groups.FirstOrDefault(candidate => candidate.Tabs.Contains(tab));
        if (group is null || !CanCloseTab(tab))
        {
            return;
        }

        int index = group.Tabs.IndexOf(tab);
        _closedTabs.Add((tab.Item, group.Id));
        if (_closedTabs.Count > ClosedTabCapacity)
        {
            _closedTabs.RemoveAt(0);
        }

        group.Tabs.Remove(tab);
        WorkspaceTabViewModel? successor = group.Tabs.Count == 0
            ? null
            : group.Tabs[Math.Min(index, group.Tabs.Count - 1)];
        group.ActiveTab = successor;
        _announce(new A11yEvent.TabClosed(tab.Title, successor?.Title));
        if (group.Tabs.Count == 0 && Groups.Count > 1)
        {
            RemoveEmptyGroup(group);
        }

        RaiseCommandStates();
        Persist();
    }

    private void RemoveEmptyGroup(WorkspaceGroupViewModel group)
    {
        WorkspacePaneNodeViewModel? node = FindNode(Root, group);
        if (node is null || !TryFindParent(Root, node, out WorkspacePaneNodeViewModel? parent))
        {
            return;
        }

        parent!.Children.Remove(node);
        if (parent.Children.Count > 1)
        {
            NormalizeWeightRatios(parent);
        }
        else
        {
            WorkspacePaneNodeViewModel replacement = parent.Children[0];
            replacement.Weight = parent.Weight;
            if (TryFindParent(Root, parent, out WorkspacePaneNodeViewModel? grandparent))
            {
                int index = grandparent!.Children.IndexOf(parent);
                grandparent.Children[index] = replacement;
            }
            else
            {
                replacement.Weight = 1;
                Root = replacement;
            }
        }

        ActiveGroup = EnumerateGroups(Root).First();
        OnPropertyChanged(nameof(Groups));
    }

    private void CloseActivePane()
    {
        if (Groups.Count <= 1)
        {
            return;
        }

        WorkspaceGroupViewModel group = ActiveGroup;
        foreach (WorkspaceTabViewModel tab in group.Tabs.ToArray())
        {
            if (!CanCloseTab(tab))
            {
                return;
            }
        }

        foreach (WorkspaceTabViewModel tab in group.Tabs)
        {
            _closedTabs.Add((tab.Item, group.Id));
        }

        if (_closedTabs.Count > ClosedTabCapacity)
        {
            _closedTabs.RemoveRange(0, _closedTabs.Count - ClosedTabCapacity);
        }

        group.Tabs.Clear();
        RemoveEmptyGroup(group);
        AnnounceActivePane();
        RequestActiveEditorFocus();
        RaiseCommandStates();
        Persist();
    }

    private bool CanCloseTab(WorkspaceTabViewModel tab)
    {
        if (!tab.IsDirty)
        {
            return true;
        }

        WorkspaceDirtyNavigationDecision decision = _dirtyCloseDecision(tab);
        return decision == WorkspaceDirtyNavigationDecision.Discard
            || (decision == WorkspaceDirtyNavigationDecision.Save && tab.Save());
    }

    private void DuplicateActiveTab()
    {
        if (ActiveGroup.ActiveTab is not WorkspaceTabViewModel tab
            || tab.Item.Kind == WorkspaceItemKind.Graph)
        {
            return;
        }

        int index = ActiveGroup.Tabs.IndexOf(tab);
        WorkspaceTabViewModel duplicate = new(
            _session,
            new WorkspaceTabState(Guid.NewGuid(), tab.Item),
            MirrorSamePathDocumentState);
        duplicate.MirrorDocumentStateFrom(tab);
        ActiveGroup.Tabs.Insert(index + 1, duplicate);
        ActiveGroup.ActiveTab = duplicate;
        RequestActiveEditorFocus();
        Persist();
    }

    private void ReopenClosedTab()
    {
        if (_closedTabs.Count == 0)
        {
            return;
        }

        (WorkspaceItemState item, Guid groupId) = _closedTabs[^1];
        _closedTabs.RemoveAt(_closedTabs.Count - 1);
        if (item.Kind == WorkspaceItemKind.Graph && TryFocusGlobalGraph())
        {
            _announce(new A11yEvent.ReopenedGraph());
            RaiseCommandStates();
            Persist();
            return;
        }

        WorkspaceGroupViewModel group = Groups.FirstOrDefault(candidate => candidate.Id == groupId)
            ?? ActiveGroup;
        WorkspaceTabViewModel tab = AddTab(group, item, activate: true);
        ActiveGroup = group;
        _announce(item.Kind switch
        {
            WorkspaceItemKind.Graph => new A11yEvent.ReopenedGraph(),
            WorkspaceItemKind.SavedQuery or WorkspaceItemKind.Dashboard =>
                new A11yEvent.ReopenedNamed(tab.Title),
            _ => new A11yEvent.ReopenedFile(tab.Title),
        });
        RaiseCommandStates();
        Persist();
    }

    private void MoveActiveTab(int delta)
    {
        if (ActiveGroup.ActiveTab is not WorkspaceTabViewModel tab)
        {
            return;
        }

        int oldIndex = ActiveGroup.Tabs.IndexOf(tab);
        int newIndex = oldIndex + delta;
        if (newIndex < 0 || newIndex >= ActiveGroup.Tabs.Count)
        {
            return;
        }

        ActiveGroup.Tabs.Move(oldIndex, newIndex);
        ActiveGroup.ActiveTab = tab;
        Persist();
    }

    private bool CanMoveActiveTab(int delta)
    {
        int index = ActiveGroup.ActiveTab is null ? -1 : ActiveGroup.Tabs.IndexOf(ActiveGroup.ActiveTab);
        return index >= 0 && index + delta >= 0 && index + delta < ActiveGroup.Tabs.Count;
    }

    private void CycleTab(int delta)
    {
        if (ActiveGroup.Tabs.Count == 0)
        {
            return;
        }

        int index = ActiveGroup.ActiveTab is null ? 0 : ActiveGroup.Tabs.IndexOf(ActiveGroup.ActiveTab);
        ActiveGroup.ActiveTab = ActiveGroup.Tabs[(index + delta + ActiveGroup.Tabs.Count) % ActiveGroup.Tabs.Count];
    }

    private void FocusPane(int delta)
    {
        IReadOnlyList<WorkspaceGroupViewModel> groups = Groups;
        int index = Array.IndexOf(groups.ToArray(), ActiveGroup);
        ActiveGroup = groups[(index + delta + groups.Count) % groups.Count];
        AnnounceActivePane();
        RequestActiveEditorFocus();
        Persist();
    }

    public bool FocusDirectionalPane(string axis, int direction)
    {
        var rects = new Dictionary<WorkspaceGroupViewModel, PaneRect>();
        BuildPaneRects(Root, new PaneRect(0, 0, 1, 1), rects);
        if (rects.TryGetValue(ActiveGroup, out PaneRect origin))
        {
            WorkspaceGroupViewModel? bestGroup = null;
            double bestDistance = double.PositiveInfinity;
            double bestOverlap = double.NegativeInfinity;
            double bestCross = double.PositiveInfinity;
            foreach ((WorkspaceGroupViewModel group, PaneRect candidate) in rects)
            {
                if (ReferenceEquals(group, ActiveGroup)
                    || !TryDirectionalScore(
                        origin,
                        candidate,
                        axis,
                        direction,
                        out double distance,
                        out double overlap,
                        out double cross))
                {
                    continue;
                }

                bool isBetter = distance < bestDistance - 1e-9
                    || (Math.Abs(distance - bestDistance) <= 1e-9
                        && (overlap > bestOverlap + 1e-9
                            || (Math.Abs(overlap - bestOverlap) <= 1e-9
                                && cross < bestCross - 1e-9)));
                if (isBetter)
                {
                    bestGroup = group;
                    bestDistance = distance;
                    bestOverlap = overlap;
                    bestCross = cross;
                }
            }

            if (bestGroup is not null)
            {
                ActiveGroup = bestGroup;
                AnnounceActivePane();
                RequestActiveEditorFocus();
                Persist();
                return true;
            }
        }

        if (axis == "horizontal")
        {
            WorkspaceFocusBoundary boundary = direction < 0
                ? WorkspaceFocusBoundary.Files
                : WorkspaceFocusBoundary.RightPane;
            if (boundary == WorkspaceFocusBoundary.RightPane && !IsRightPaneVisible)
            {
                IsRightPaneVisible = true;
            }

            _announce(boundary == WorkspaceFocusBoundary.Files
                ? new A11yEvent.FilesRegionFocused()
                : new A11yEvent.LeafPanelShown(ActiveLeaf.Title));
            FocusBoundaryRequested?.Invoke(this, boundary);
        }
        else
        {
            // W0.5-3 residue: Windows terminal vertical-focus availability copy.
            _announce(new A11yEvent.HostComposed(
                direction < 0 ? "No pane above." : "No pane below.",
                A11yPriority.Medium));
        }

        return false;
    }

    private static bool TryDirectionalScore(
        PaneRect origin,
        PaneRect candidate,
        string axis,
        int direction,
        out double distance,
        out double overlap,
        out double cross)
    {
        if (axis == "horizontal")
        {
            distance = direction < 0
                ? origin.MinX - candidate.MaxX
                : candidate.MinX - origin.MaxX;
            overlap = Math.Min(origin.MaxY, candidate.MaxY)
                - Math.Max(origin.MinY, candidate.MinY);
            cross = candidate.MinY;
        }
        else
        {
            distance = direction < 0
                ? origin.MinY - candidate.MaxY
                : candidate.MinY - origin.MaxY;
            overlap = Math.Min(origin.MaxX, candidate.MaxX)
                - Math.Max(origin.MinX, candidate.MinX);
            cross = candidate.MinX;
        }

        return distance >= -1e-9 && overlap > 1e-9;
    }

    private static void BuildPaneRects(
        WorkspacePaneNodeViewModel node,
        PaneRect rect,
        IDictionary<WorkspaceGroupViewModel, PaneRect> output)
    {
        if (node.Group is WorkspaceGroupViewModel group)
        {
            output[group] = rect;
            return;
        }

        double total = node.Children.Sum(child => child.Weight);
        if (!double.IsFinite(total) || total <= 0)
        {
            total = node.Children.Count;
        }

        double offset = 0;
        foreach (WorkspacePaneNodeViewModel child in node.Children)
        {
            double fraction = child.Weight / total;
            PaneRect childRect = node.Axis == "horizontal"
                ? new PaneRect(
                    rect.MinX + offset * rect.Width,
                    rect.MinY,
                    rect.MinX + (offset + fraction) * rect.Width,
                    rect.MaxY)
                : new PaneRect(
                    rect.MinX,
                    rect.MinY + offset * rect.Height,
                    rect.MaxX,
                    rect.MinY + (offset + fraction) * rect.Height);
            BuildPaneRects(child, childRect, output);
            offset += fraction;
        }
    }

    public void AnnounceActivePaneFocus() => AnnounceActivePane();

    private void RequestActiveEditorFocus() =>
        EditorPaneFocusRequested?.Invoke(this, ActiveGroup);

    private void AnnounceActivePane()
    {
        IReadOnlyList<WorkspaceGroupViewModel> groups = Groups;
        uint ordinal = (uint)(Array.IndexOf(groups.ToArray(), ActiveGroup) + 1);
        _announce(new A11yEvent.EditorPaneFocused(
            ordinal,
            (uint)groups.Count,
            ActiveGroup.ActiveTab?.Title ?? "Empty pane",
            string.Empty));
    }

    private void ResizeActivePane(double delta)
    {
        WorkspacePaneNodeViewModel? node = FindNode(Root, ActiveGroup);
        if (node is null || !TryFindParent(Root, node, out WorkspacePaneNodeViewModel? parent))
        {
            _announce(new A11yEvent.NoSplitPanesToResize());
            return;
        }

        int index = parent!.Children.IndexOf(node);
        int neighborIndex = index == parent.Children.Count - 1 ? index - 1 : index + 1;
        WorkspacePaneNodeViewModel neighbor = parent.Children[neighborIndex];
        double applied = Math.Clamp(
            delta,
            WorkspacePersistence.MinGroupWeight - node.Weight,
            neighbor.Weight - WorkspacePersistence.MinGroupWeight);
        node.Weight += applied;
        neighbor.Weight -= applied;
        _announce(new A11yEvent.PaneResized((uint)Math.Round(node.Weight * 100)));
        Persist();
    }

    private void SaveActive()
    {
        if (ActiveGroup.ActiveTab is WorkspaceTabViewModel tab && tab.Save())
        {
            _announce(new A11yEvent.NoteSaved(System.IO.Path.GetFileName(tab.Path)));
        }
    }

    private (WorkspacePaneNodeViewModel Root, WorkspaceGroupViewModel Active) Restore(
        WorkspaceSnapshot? snapshot)
    {
        _restoring = true;
        try
        {
            if (snapshot is null)
            {
                var group = new WorkspaceGroupViewModel(this, Guid.NewGuid());
                return (new WorkspacePaneNodeViewModel(group), group);
            }

            bool graphRestored = false;
            WorkspacePaneNodeViewModel restored = RestoreNode(snapshot.Root, ref graphRestored);
            WorkspacePaneNodeViewModel root = PruneEmptyRestoreGroups(restored, allowEmptyRoot: true)
                ?? new WorkspacePaneNodeViewModel(
                    new WorkspaceGroupViewModel(this, Guid.NewGuid()));
            root.Weight = 1;
            WorkspaceGroupViewModel active = EnumerateGroups(root)
                .FirstOrDefault(group =>
                    group.Id == snapshot.ActiveGroup
                    && (group.ActiveTab is not null || GroupsHaveNoTabs(root)))
                ?? EnumerateGroups(root).First();
            _activeLeaf = Leaves.FirstOrDefault(leaf => leaf.Id == snapshot.ActiveLeaf) ?? Leaves[0];
            return (root, active);
        }
        finally
        {
            _restoring = false;
        }
    }

    private WorkspacePaneNodeViewModel RestoreNode(WorkspaceNodeState state, ref bool graphRestored)
    {
        if (state is WorkspaceGroupState groupState)
        {
            var group = new WorkspaceGroupViewModel(this, groupState.Id);
            foreach (WorkspaceTabState tabState in groupState.Tabs)
            {
                if (tabState.Item.Kind == WorkspaceItemKind.Graph)
                {
                    if (graphRestored)
                    {
                        continue;
                    }

                    graphRestored = true;
                }

                group.Tabs.Add(new WorkspaceTabViewModel(
                    _session,
                    tabState,
                    MirrorSamePathDocumentState));
            }

            WorkspaceTabViewModel? active = group.Tabs.FirstOrDefault(tab => tab.Id == groupState.ActiveTab)
                ?? group.Tabs.FirstOrDefault();
            group.RestoreActive(active);
            return new WorkspacePaneNodeViewModel(group);
        }

        var splitState = (WorkspaceSplitState)state;
        var split = new WorkspacePaneNodeViewModel(splitState.Axis);
        for (int index = 0; index < splitState.Children.Count; index++)
        {
            WorkspacePaneNodeViewModel child = RestoreNode(
                splitState.Children[index],
                ref graphRestored);
            child.Weight = splitState.Weights[index];
            split.Children.Add(child);
        }

        return split;
    }

    private static WorkspacePaneNodeViewModel? PruneEmptyRestoreGroups(
        WorkspacePaneNodeViewModel node,
        bool allowEmptyRoot)
    {
        if (node.Group is WorkspaceGroupViewModel group)
        {
            return group.Tabs.Count > 0 || allowEmptyRoot ? node : null;
        }

        WorkspacePaneNodeViewModel[] children = node.Children.ToArray();
        node.Children.Clear();
        foreach (WorkspacePaneNodeViewModel child in children)
        {
            WorkspacePaneNodeViewModel? retained = PruneEmptyRestoreGroups(
                child,
                allowEmptyRoot: false);
            if (retained is not null)
            {
                node.Children.Add(retained);
            }
        }

        if (node.Children.Count == 0)
        {
            return null;
        }

        if (node.Children.Count == 1)
        {
            WorkspacePaneNodeViewModel only = node.Children[0];
            only.Weight = node.Weight;
            return only;
        }

        NormalizeWeightRatios(node);
        return node;
    }

    private static bool GroupsHaveNoTabs(WorkspacePaneNodeViewModel root) =>
        !EnumerateGroups(root).SelectMany(group => group.Tabs).Any();

    private void RunWorkspaceMutation(Action action)
    {
        _persistenceBatchDepth++;
        try
        {
            action();
        }
        finally
        {
            _persistenceBatchDepth--;
            if (_persistenceBatchDepth == 0 && _persistencePending)
            {
                _persistencePending = false;
                PersistCore();
            }
        }
    }

    private void Persist()
    {
        if (_restoring)
        {
            return;
        }

        if (_persistenceBatchDepth > 0)
        {
            _persistencePending = true;
            return;
        }

        PersistCore();
    }

    internal void PersistLayoutWeights() => Persist();

    internal void AnnouncePaneResize(double weight)
    {
        // W0.5-3 residue: Windows split-handle size feedback.
        _announce(new A11yEvent.HostComposed(
            $"Editor pane size {weight:P0}.",
            A11yPriority.Medium));
    }

    private void PersistCore()
    {
        if (_restoring)
        {
            return;
        }

        try
        {
            _persistence.Save(new WorkspaceSnapshot(
                WorkspacePersistence.SchemaVersion,
                ActiveGroup.Id,
                SnapshotNode(Root),
                ActiveLeaf.Id,
                _expandedDirectoryPaths()));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            HostLog.Write(HostDiagnosticEvent.WorkspacePersistFailed, exception);
        }
    }

    private static WorkspaceNodeState SnapshotNode(WorkspacePaneNodeViewModel node)
    {
        if (node.Group is WorkspaceGroupViewModel group)
        {
            return new WorkspaceGroupState(
                group.Id,
                group.ActiveTab?.Id,
                group.Tabs.Select(tab => tab.Snapshot()).ToArray());
        }

        double total = node.Children.Sum(child => child.Weight);
        return new WorkspaceSplitState(
            node.Axis!,
            node.Children.Select(child => child.Weight / total).ToArray(),
            node.Children.Select(SnapshotNode).ToArray());
    }

    private static WorkspaceItemState ItemForPath(string path)
    {
        string extension = System.IO.Path.GetExtension(path);
        return extension.ToLowerInvariant() switch
        {
            ".canvas" => new WorkspaceItemState(WorkspaceItemKind.Canvas, path),
            ".base" => new WorkspaceItemState(WorkspaceItemKind.Base, path),
            _ => new WorkspaceItemState(WorkspaceItemKind.Markdown, path),
        };
    }

    private static bool ItemsReferToSameTarget(WorkspaceItemState left, WorkspaceItemState right) =>
        left.Kind == right.Kind
        && string.Equals(left.Path, right.Path, StringComparison.Ordinal);

    private WorkspaceTabViewModel? FindSamePathTab(
        WorkspaceItemState item,
        WorkspaceTabViewModel? excluding = null) =>
        item.Kind == WorkspaceItemKind.Markdown
            ? Groups.SelectMany(group => group.Tabs).FirstOrDefault(tab =>
                !ReferenceEquals(tab, excluding)
                && tab.IsMarkdown
                && string.Equals(tab.Path, item.Path, StringComparison.Ordinal))
            : null;

    private void MirrorSamePathDocumentState(WorkspaceTabViewModel source)
    {
        if (!source.IsMarkdown)
        {
            return;
        }

        foreach (WorkspaceTabViewModel peer in Groups.SelectMany(group => group.Tabs))
        {
            if (!ReferenceEquals(peer, source)
                && peer.IsMarkdown
                && string.Equals(peer.Path, source.Path, StringComparison.Ordinal))
            {
                peer.MirrorDocumentStateFrom(source);
            }
        }
    }

    private static bool IsPathBacked(WorkspaceItemState item) =>
        item.Kind is WorkspaceItemKind.Markdown or WorkspaceItemKind.Canvas or WorkspaceItemKind.Base;

    private static string NormalizeWorkspacePath(string path) =>
        string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/').TrimEnd('/');

    private static bool IsSameOrDescendantPath(string path, string ancestor)
    {
        string normalized = NormalizeWorkspacePath(path);
        return string.Equals(normalized, ancestor, StringComparison.Ordinal)
            || normalized.StartsWith(ancestor + "/", StringComparison.Ordinal);
    }

    private static bool TryRetargetPath(
        string path,
        string source,
        string destination,
        out string retargeted)
    {
        string normalized = NormalizeWorkspacePath(path);
        if (string.Equals(normalized, source, StringComparison.Ordinal))
        {
            retargeted = destination;
            return true;
        }

        if (normalized.StartsWith(source + "/", StringComparison.Ordinal))
        {
            retargeted = destination + normalized[source.Length..];
            return true;
        }

        retargeted = normalized;
        return false;
    }

    private static IEnumerable<WorkspaceGroupViewModel> EnumerateGroups(WorkspacePaneNodeViewModel node)
    {
        if (node.Group is WorkspaceGroupViewModel group)
        {
            yield return group;
            yield break;
        }

        foreach (WorkspacePaneNodeViewModel child in node.Children)
        {
            foreach (WorkspaceGroupViewModel descendant in EnumerateGroups(child))
            {
                yield return descendant;
            }
        }
    }

    private static WorkspacePaneNodeViewModel? FindNode(
        WorkspacePaneNodeViewModel node,
        WorkspaceGroupViewModel group)
    {
        if (node.Group == group)
        {
            return node;
        }

        return node.Children.Select(child => FindNode(child, group)).FirstOrDefault(found => found is not null);
    }

    private static bool TryFindParent(
        WorkspacePaneNodeViewModel current,
        WorkspacePaneNodeViewModel target,
        out WorkspacePaneNodeViewModel? parent)
    {
        if (current.Children.Contains(target))
        {
            parent = current;
            return true;
        }

        foreach (WorkspacePaneNodeViewModel child in current.Children)
        {
            if (TryFindParent(child, target, out parent))
            {
                return true;
            }
        }

        parent = null;
        return false;
    }

    private static void NormalizeWeights(WorkspacePaneNodeViewModel split)
    {
        double weight = 1d / split.Children.Count;
        foreach (WorkspacePaneNodeViewModel child in split.Children)
        {
            child.Weight = weight;
        }
    }

    private static void NormalizeWeightRatios(WorkspacePaneNodeViewModel split)
    {
        double total = split.Children.Sum(child => child.Weight);
        if (!double.IsFinite(total) || total <= 0)
        {
            NormalizeWeights(split);
            return;
        }

        foreach (WorkspacePaneNodeViewModel child in split.Children)
        {
            child.Weight /= total;
        }
    }

    private void RaiseCommandStates()
    {
        foreach (ICommand command in new[]
        {
            CloseActiveTabCommand,
            DuplicateTabCommand,
            ReopenClosedTabCommand,
            MoveTabLeftCommand,
            MoveTabRightCommand,
            NextTabCommand,
            PreviousTabCommand,
            SplitRightCommand,
            SplitDownCommand,
            ClosePaneCommand,
            FocusPaneLeftCommand,
            FocusPaneRightCommand,
            FocusPaneAboveCommand,
            FocusPaneBelowCommand,
            FocusNextPaneCommand,
            FocusPreviousPaneCommand,
            GrowPaneCommand,
            ShrinkPaneCommand,
            SaveActiveCommand,
        })
        {
            ((RelayCommand)command).RaiseCanExecuteChanged();
        }
    }
}
