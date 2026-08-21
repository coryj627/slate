// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using System.Windows.Input;
using SlateWindows.FileManagement;
using uniffi.slate_uniffi;

namespace SlateWindows;

internal enum SidebarSortMode
{
    NameAscending,
    NameDescending,
    ModifiedNewest,
    ModifiedOldest,
    CreatedNewest,
    CreatedOldest,
}

internal enum FileTreeChildLoadState
{
    Unloaded,
    Loading,
    Loaded,
    Failed,
}

internal sealed class FileTreeNodeViewModel : BindableBase
{
    private readonly FilesSidebarViewModel? _owner;
    private bool _isExpanded;
    private bool _isBatchSelected;
    private bool _isSelected;
    private ObservableCollection<FileTreeNodeViewModel> _children = [];
    private FileTreeChildLoadState _childLoadState;
    private object? _treeIdentity;

    private FileTreeNodeViewModel(string loadingLabel)
    {
        Name = loadingLabel;
        Path = string.Empty;
        IsPlaceholder = true;
        _childLoadState = FileTreeChildLoadState.Loaded;
    }

    private FileTreeNodeViewModel(string groupLabel, IEnumerable<FileTreeNodeViewModel> children)
    {
        Name = groupLabel;
        Path = string.Empty;
        IsGroupHeader = true;
        _isExpanded = true;
        _childLoadState = FileTreeChildLoadState.Loaded;
        foreach (FileTreeNodeViewModel child in children)
        {
            Children.Add(child);
        }
    }

    public FileTreeNodeViewModel(
        FilesSidebarViewModel owner,
        string path,
        string name,
        bool isDirectory,
        int level,
        bool hasChildren,
        bool hasFolderNote = false,
        FileSummary? summary = null)
    {
        _owner = owner;
        Path = path;
        Name = name;
        IsDirectory = isDirectory;
        Level = level;
        HasFolderNote = hasFolderNote;
        Summary = summary;
        if (isDirectory && hasChildren)
        {
            Children.Add(new FileTreeNodeViewModel("Loading…"));
            _childLoadState = FileTreeChildLoadState.Unloaded;
        }
        else
        {
            _childLoadState = FileTreeChildLoadState.Loaded;
        }
    }

    public static FileTreeNodeViewModel Loading() => new("Loading…");
    public static FileTreeNodeViewModel Error(string label) => new(label);
    public static FileTreeNodeViewModel Overflow(string label) => new(label);
    public static FileTreeNodeViewModel Group(
        string label,
        IEnumerable<FileTreeNodeViewModel> children) => new(label, children);

    public string Path { get; }
    public string Name { get; private set; }
    public bool IsDirectory { get; }
    public bool IsPlaceholder { get; }
    public bool IsGroupHeader { get; }
    public bool IsBatchSelectable => !IsPlaceholder && !IsGroupHeader;
    public int Level { get; }
    public bool HasFolderNote { get; private set; }
    public FileSummary? Summary { get; }
    public ObservableCollection<FileTreeNodeViewModel> Children => _children;
    internal FileTreeChildLoadState ChildLoadState => _childLoadState;
    internal bool IsAttachedToTree(object treeIdentity) =>
        ReferenceEquals(_treeIdentity, treeIdentity);

    public string DisplayName => Summary?.DisplayName ?? Name;
    public string KindLabel => IsGroupHeader ? "group" : IsDirectory ? "folder" : "file";
    public string AutomationName => IsPlaceholder
        ? Name
        : $"{DisplayName}, {KindLabel}{(HasFolderNote ? ", has folder note" : string.Empty)}";
    public string MetadataText
    {
        get
        {
            if (Summary is null)
            {
                return HasFolderNote ? "Folder note" : string.Empty;
            }

            var parts = new List<string>();
            if (Summary.WordCount is uint words)
            {
                parts.Add($"{words:N0} words");
            }

            if (Summary.TaskTotal > 0)
            {
                parts.Add($"{Summary.TaskOpen:N0} of {Summary.TaskTotal:N0} tasks open");
            }

            if (!string.IsNullOrWhiteSpace(Summary.CreatedDate))
            {
                parts.Add($"created {Summary.CreatedDate}");
            }

            return string.Join(" · ", parts);
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!SetField(ref _isExpanded, value))
            {
                return;
            }

            if (value)
            {
                _owner?.LoadChildren(this);
            }
            else
            {
                ResetChildLoadAfterCollapse();
                _owner?.CancelChildExpansion(this);
            }
        }
    }

    public bool IsBatchSelected
    {
        get => _isBatchSelected;
        set
        {
            if (IsBatchSelectable && SetField(ref _isBatchSelected, value))
            {
                _owner?.BatchCheckChanged(this);
            }
        }
    }

    /// <summary>Whether this folder's children are COMPLETELY loaded
    /// real rows — the signal the batch-check reconciliation uses to
    /// distinguish "provably gone" from "merely unmaterialized"
    /// (codex rounds 5-6): the load state must be Loaded AND no
    /// placeholder of any shape may remain (Loading…, error, or
    /// overflow rows all make the listing non-authoritative).</summary>
    internal bool HasLoadedChildren =>
        _childLoadState == FileTreeChildLoadState.Loaded
        && Children.All(child => !child.IsPlaceholder);

    /// <summary>W5-4 (verification 1): the VM→view selection channel.
    /// The container style binds TreeViewItem.IsSelected TwoWay, so a
    /// publication-time restore materializes as the REAL tree
    /// selection (and the view's own selection moves keep the node
    /// state honest). The container's resulting SelectedItemChanged
    /// re-enters the sidebar's SelectedNode setter with the SAME node
    /// and no-ops there.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    internal void RenameTo(string name)
    {
        Name = name;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(AutomationName));
    }

    internal bool MarkExpandedWithoutLoading()
    {
        return SetField(ref _isExpanded, true, nameof(IsExpanded));
    }

    /// <summary>Publication-time batch-check rebind (codex round 4):
    /// no per-node announcement — the owner recomputes the count once
    /// after the whole rebind.</summary>
    internal bool MarkBatchSelectedSilently() =>
        IsBatchSelectable
        && SetField(ref _isBatchSelected, true, nameof(IsBatchSelected));

    internal bool PrepareChildLoad()
    {
        if (!IsDirectory || _childLoadState is FileTreeChildLoadState.Loading or FileTreeChildLoadState.Loaded)
        {
            return false;
        }

        _childLoadState = FileTreeChildLoadState.Loading;
        if (Children.Count != 1 || !Children[0].IsPlaceholder || Children[0].Name != "Loading…")
        {
            ReplaceChildrenCore(new ObservableCollection<FileTreeNodeViewModel> { Loading() });
        }

        return true;
    }

    internal void ResetChildLoadAfterCollapse()
    {
        if (_childLoadState == FileTreeChildLoadState.Loading)
        {
            _childLoadState = FileTreeChildLoadState.Unloaded;
        }
        else if (_childLoadState == FileTreeChildLoadState.Failed)
        {
            _childLoadState = FileTreeChildLoadState.Unloaded;
        }
    }

    internal void FailChildLoad(string label)
    {
        _childLoadState = FileTreeChildLoadState.Failed;
        ReplaceChildrenCore(new ObservableCollection<FileTreeNodeViewModel> { Error(label) });
    }

    internal void CancelChildLoad(string label)
    {
        if (IsExpanded && _childLoadState == FileTreeChildLoadState.Loading)
        {
            FailChildLoad(label);
        }
    }

    internal void AttachToTree(object treeIdentity)
    {
        _treeIdentity = treeIdentity;
        foreach (FileTreeNodeViewModel child in Children)
        {
            child.AttachToTree(treeIdentity);
        }
    }

    internal void ReplaceChildren(IEnumerable<FileTreeNodeViewModel> children)
    {
        var replacement = new ObservableCollection<FileTreeNodeViewModel>();
        foreach (FileTreeNodeViewModel child in children)
        {
            if (_treeIdentity is object treeIdentity)
            {
                child.AttachToTree(treeIdentity);
            }

            replacement.Add(child);
        }

        ReplaceChildren(replacement);
    }

    internal void ReplaceChildren(ObservableCollection<FileTreeNodeViewModel> children)
    {
        // No check projection HERE (codex round 7): the refresh-
        // restore path calls this on the TREE WORKER, and the
        // authoritative check set is UI-thread-confined — projection
        // happens at each dispatcher publication boundary instead
        // (ApplyTreeRefresh and the child-expansion applies).
        _childLoadState = FileTreeChildLoadState.Loaded;
        ReplaceChildrenCore(children);
    }

    private void ReplaceChildrenCore(ObservableCollection<FileTreeNodeViewModel> children)
    {
        _children = children;
        OnPropertyChanged(nameof(Children));
    }
}

internal sealed class SidebarTagViewModel
{
    public SidebarTagViewModel(
        string segment,
        string full,
        uint fileCount,
        uint directCount,
        uint depth)
    {
        Segment = segment;
        Full = full;
        FileCount = fileCount;
        DirectCount = directCount;
        Depth = depth;
    }

    public string Segment { get; }
    public string Full { get; }
    public uint FileCount { get; }
    public uint DirectCount { get; }
    public uint Depth { get; }
    public string DisplayLabel => $"{Segment} ({FileCount:N0})";
    public string AutomationName => $"{Segment}, {FileCount:N0} {(FileCount == 1 ? "file" : "files")}";
    public ObservableCollection<SidebarTagViewModel> Children { get; } = [];
}

internal sealed record SidebarShortcutViewModel(string Kind, string Path)
{
    public string DisplayName => System.IO.Path.GetFileName(Path.TrimEnd('/'));
    public string KindLabel => Kind == "folder" ? "folder" : "file";
    public string AutomationName => $"{DisplayName}, {KindLabel} shortcut";
}

/// <summary>
/// W1 files-sidebar adapter. Core owns filtering, tag mutation, exclusive
/// creation, and structural rewrites; this class owns WPF presentation state.
/// </summary>
internal sealed partial class FilesSidebarViewModel : BindableBase
{
    private const uint PageLimit = 500;
    internal const int MaxMaterializedDirectoryItems = 5_000;
    private readonly VaultSession _session;
    private readonly Action<A11yEvent> _announce;
    private readonly Action<string> _copyText;
    private readonly Func<string, bool> _confirmDestructive;
    private readonly SidebarSettingsStore? _settingsStore;
    private readonly FileRecentsStore? _recentsStore;
    private readonly string? _settingsNotice;
    private readonly HashSet<string> _pinned = new(StringComparer.Ordinal);
    private readonly List<string> _recents = [];
    private readonly List<string> _history = [];
    private int _historyIndex = -1;
    private int _tagGeneration;
    private FileTreeNodeViewModel? _selectedNode;
    private SidebarShortcutViewModel? _selectedShortcut;
    private string _status = string.Empty;
    private string _mutationName = string.Empty;
    private string _tagInput = string.Empty;
    private SidebarSortMode _sortMode;
    private bool _groupByDate;
    private bool _isDualPaneEnabled;
    private bool _showTags;
    private string _moveDestination = string.Empty;
    private int _batchSelectionCount;

    public FilesSidebarViewModel(
        VaultSession session,
        Action<A11yEvent> announce,
        Action<string>? copyText = null,
        IEnumerable<string>? restoredExpandedPaths = null,
        string? vaultRoot = null,
        Func<string, bool>? confirmDestructive = null,
        Func<Task<IReadOnlyList<string>>>? pickImportSources = null,
        string? localAppDataRoot = null,
        SynchronizationContext? filterUiContext = null,
        SynchronizationContext? treeUiContext = null,
        Func<Action, CancellationToken, Task>? treeWorker = null,
        Func<Action, CancellationToken, Task>? filterWorker = null,
        Func<Action, CancellationToken, Task>? importWorker = null)
    {
        _session = session;
        _announce = announce;
        _copyText = copyText ?? (_ => { });
        _confirmDestructive = confirmDestructive ?? (_ => true);
        _pickImportSources = pickImportSources ?? (() => Task.FromResult<IReadOnlyList<string>>([]));
        SynchronizationContext? currentUiContext = SynchronizationContext.Current;
        _filterUiContext = filterUiContext ?? currentUiContext;
        _treeUiContext = treeUiContext
            ?? (currentUiContext is DispatcherSynchronizationContext ? currentUiContext : null);
        _runTreeWorker = treeWorker ?? ((work, token) => Task.Run(work, token));
        _runFilterWorker = filterWorker ?? ((work, token) => Task.Run(work, token));
        _runImportWorker = importWorker ?? ((work, token) => Task.Run(work, token));
        _vaultRoot = vaultRoot;
        if (vaultRoot is not null)
        {
            _settingsStore = new SidebarSettingsStore(vaultRoot);
            _recentsStore = new FileRecentsStore(vaultRoot, session.RootIdentity(), localAppDataRoot);
            SidebarSettingsSnapshot settings = _settingsStore.Load();
            _sortMode = settings.SortMode;
            _groupByDate = settings.GroupByDate;
            _pinned.UnionWith(settings.Pins);
            foreach (SidebarShortcutState shortcut in settings.Shortcuts)
            {
                Shortcuts.Add(new SidebarShortcutViewModel(shortcut.Kind, shortcut.Path));
            }

            _recents.AddRange(_recentsStore.Load());
            if (settings.ReadOnlyReason is not null)
            {
                _settingsNotice = settings.ReadOnlyReason;
            }
        }
        _expandedPaths = new HashSet<string>(
            restoredExpandedPaths ?? [],
            StringComparer.Ordinal);

        RefreshCommand = new RelayCommand(_ => Refresh(reportCount: true), _ => true);
        ClearFilterCommand = new RelayCommand(_ => FilterText = string.Empty, _ => FilterText.Length > 0);
        ToggleTagsCommand = new RelayCommand(_ => ShowTags = !ShowTags, _ => true);
        ToggleDualPaneCommand = new RelayCommand(_ => IsDualPaneEnabled = !IsDualPaneEnabled, _ => true);
        AddTagCommand = new RelayCommand(_ => EditTag(add: true), _ => !IsImporting && BatchSelectionCount > 0 && TagInput.Length > 0);
        RemoveTagCommand = new RelayCommand(_ => EditTag(add: false), _ => !IsImporting && BatchSelectionCount > 0 && TagInput.Length > 0);
        // W5-4 F1/F2: the creates are auto-named and selection-
        // independent — the MutationName text-box flow retired for
        // these verbs.
        CreateFolderCommand = new RelayCommand(_ => CreateFolder(), _ => !IsImporting);
        CreateNoteCommand = new RelayCommand(_ => CreateNote(), _ => !IsImporting);
        RenameCommand = new RelayCommand(
            _ => TryRenameSelected(),
            _ => !IsImporting
                && SelectedNode is { IsPlaceholder: false, IsGroupHeader: false }
                && MutationName.Length > 0);
        // Red team: placeholders and group headers are selectable rows
        // with Path "" — Delete on one reached DeleteFile("") and spoke
        // a spurious failure; the guard matches every sibling verb.
        DeleteCommand = new RelayCommand(
            _ => DeleteSelected(),
            _ => !IsImporting
                && SelectedNode is { IsPlaceholder: false, IsGroupHeader: false });
        CreateFolderNoteCommand = new RelayCommand(_ => CreateFolderNote(), _ => !IsImporting && SelectedNode?.IsDirectory == true && !SelectedNode.HasFolderNote);
        DeleteFolderNoteCommand = new RelayCommand(_ => DeleteFolderNote(), _ => !IsImporting && SelectedNode?.IsDirectory == true && SelectedNode.HasFolderNote);
        CopyWikilinkCommand = new RelayCommand(_ => CopyWikilink(), _ => SelectedNode is { IsDirectory: false });
        // W5-4 Phase B (F5/F7/F8). Duplicate stays executable on a
        // folder so the canonical DuplicateFilesOnly refusal can speak.
        DuplicateCommand = new RelayCommand(
            _ => DuplicateSelected(),
            _ => !IsImporting && SelectedNode is { IsPlaceholder: false, IsGroupHeader: false });
        CopyPathCommand = new RelayCommand(
            _ => CopyPathSelected(),
            _ => SelectedNode is { IsPlaceholder: false, IsGroupHeader: false });
        RevealCommand = new RelayCommand(
            _ => RevealSelected(),
            _ => SelectedNode is { IsPlaceholder: false, IsGroupHeader: false });
        // W5-4 Phase C (F4): batch checks win; otherwise the tree
        // selection — the verb is live with either.
        MoveToCommand = new RelayCommand(
            _ => OpenMoveTo(),
            _ => !IsImporting
                && (BatchSelectionCount > 0
                    || SelectedNode is { IsPlaceholder: false, IsGroupHeader: false }));
        PinCommand = new RelayCommand(_ => PinSelected(), _ => SelectedNode is { IsDirectory: false });
        UnpinCommand = new RelayCommand(_ => UnpinSelected(), _ => SelectedNode is { IsDirectory: false });
        UnpinAllCommand = new RelayCommand(_ => UnpinAllInFolder(), _ => _pinned.Count > 0);
        AddShortcutCommand = new RelayCommand(_ => AddShortcut(), _ => SelectedNode is { IsPlaceholder: false, IsGroupHeader: false });
        RemoveShortcutCommand = new RelayCommand(_ => RemoveShortcut(), _ => SelectedShortcut is not null);
        UseVaultDefaultSortCommand = new RelayCommand(_ => UseVaultDefaultSort(), _ => SortMode != SidebarSortMode.NameAscending || GroupByDate);
        OpenCurrentCommand = new RelayCommand(_ => OpenSelected(WorkspaceOpenTarget.CurrentTab), _ => CanOpenSelected());
        OpenNewTabCommand = new RelayCommand(_ => OpenSelected(WorkspaceOpenTarget.NewTab), _ => CanOpenSelected());
        OpenSplitCommand = new RelayCommand(_ => OpenSelected(WorkspaceOpenTarget.SplitRight), _ => CanOpenSelected());
        BatchMoveCommand = new RelayCommand(_ => BatchMove(), _ => !IsImporting && BatchSelectionCount > 0 && MoveDestination.Length > 0);
        BatchTrashCommand = new RelayCommand(_ => BatchTrash(), _ => !IsImporting && BatchSelectionCount > 0);
        ImportCommand = new AsyncRelayCommand(
            _ => _importCompletion = ImportAsync(),
            () => !IsImporting);
        CancelImportCommand = new RelayCommand(_ => CancelImport(), _ => IsImporting);
        ClearRecentsCommand = new RelayCommand(_ => ClearRecents(), _ => _recents.Count > 0);
        CollapseAllCommand = new RelayCommand(_ => CollapseAll(), _ => true);
        ExpandLoadedCommand = new AsyncRelayCommand(
            _ => _expandLoadedCompletion = ExpandLoadedAsync(),
            () => true);
        HistoryBackCommand = new RelayCommand(_ => History(-1), _ => _historyIndex > 0);
        HistoryForwardCommand = new RelayCommand(_ => History(1), _ => _historyIndex >= 0 && _historyIndex < _history.Count - 1);
        Refresh(reportCount: true);
    }

    public event EventHandler<(string Path, WorkspaceOpenTarget Target)>? OpenTargetRequested;

    public ObservableCollection<FileTreeNodeViewModel> DualPaneFiles { get; } = [];
    public ObservableCollection<SidebarTagViewModel> Tags { get; } = [];
    public ObservableCollection<SidebarShortcutViewModel> Shortcuts { get; } = [];
    public IReadOnlyList<SidebarSortMode> SortModes { get; } = Enum.GetValues<SidebarSortMode>();

    public SidebarSortMode SortMode
    {
        get => _sortMode;
        set
        {
            if (SetField(ref _sortMode, value))
            {
                if (GroupByDate && value is SidebarSortMode.NameAscending or SidebarSortMode.NameDescending)
                {
                    _sortMode = SidebarSortMode.ModifiedNewest;
                    OnPropertyChanged();
                }

                bool saved = PersistOrganization();
                Refresh();
                if (saved)
                {
                    AnnounceSort();
                }
                RaiseCommandStates();
            }
        }
    }

    public bool GroupByDate
    {
        get => _groupByDate;
        set
        {
            if (SetField(ref _groupByDate, value))
            {
                if (value && SortMode is SidebarSortMode.NameAscending or SidebarSortMode.NameDescending)
                {
                    _sortMode = SidebarSortMode.ModifiedNewest;
                    OnPropertyChanged(nameof(SortMode));
                }

                bool saved = PersistOrganization();
                Refresh();
                if (saved)
                {
                    AnnounceSort();
                }
                RaiseCommandStates();
            }
        }
    }

    public FileTreeNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (!SetField(ref _selectedNode, value)
                || value is null
                || value.IsPlaceholder
                || value.IsGroupHeader)
            {
                return;
            }

            MutationName = value.Name;
            if (value.IsDirectory)
            {
                _announce(new A11yEvent.TreeFolderSelected(value.DisplayName));
                LoadDualPane(value.Path);
                if (value.HasFolderNote)
                {
                    RequestOpen(FolderNotePath(value));
                }
            }
            else
            {
                _announce(new A11yEvent.RowSelected(value.DisplayName));
                RequestOpen(value.Path);
            }

            RaiseCommandStates();
        }
    }

    /// <summary>W5-4 (red team, a11y 2): selection RESTORATION after a
    /// mutation's tree publication — the setter's open-on-select and
    /// selection announcements must not fire for a re-seat (the
    /// mutation already spoke, and re-opening the just-renamed note
    /// would churn recents/history for a gesture the user never
    /// made).</summary>
    private void SelectSilently(FileTreeNodeViewModel node)
    {
        if (SetField(ref _selectedNode, node, nameof(SelectedNode)))
        {
            // The view half (verification 1): without the container's
            // IsSelected, the restore was invisible and the next arrow
            // key jumped to the tree top and opened the first file.
            node.IsSelected = true;
            MutationName = node.Name;
            RaiseCommandStates();
        }
    }

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public string MutationName
    {
        get => _mutationName;
        set
        {
            if (SetField(ref _mutationName, value.TrimStart()))
            {
                RaiseCommandStates();
            }
        }
    }

    public string TagInput
    {
        get => _tagInput;
        set
        {
            if (SetField(ref _tagInput, value.Trim()))
            {
                RaiseCommandStates();
            }
        }
    }

    public string MoveDestination
    {
        get => _moveDestination;
        set
        {
            if (SetField(ref _moveDestination, value.Trim()))
            {
                RaiseCommandStates();
            }
        }
    }

    public SidebarShortcutViewModel? SelectedShortcut
    {
        get => _selectedShortcut;
        set
        {
            if (SetField(ref _selectedShortcut, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool ShowTags
    {
        get => _showTags;
        set
        {
            if (SetField(ref _showTags, value) && value)
            {
                LoadTags();
            }
        }
    }

    public bool IsDualPaneEnabled
    {
        get => _isDualPaneEnabled;
        set
        {
            if (SetField(ref _isDualPaneEnabled, value) && value)
            {
                LoadDualPane(SelectedNode?.IsDirectory == true ? SelectedNode.Path : string.Empty);
            }
        }
    }

    public int BatchSelectionCount
    {
        get => _batchSelectionCount;
        private set
        {
            if (SetField(ref _batchSelectionCount, value))
            {
                OnPropertyChanged(nameof(BatchSelectionSummary));
                RaiseCommandStates();
            }
        }
    }

    public string BatchSelectionSummary => BatchSelectionCount == 0
        ? "No files selected"
        : $"{BatchSelectionCount:N0} {(BatchSelectionCount == 1 ? "file" : "files")} selected";

    public ICommand RefreshCommand { get; }
    public ICommand ClearFilterCommand { get; }
    public ICommand ToggleTagsCommand { get; }
    public ICommand ToggleDualPaneCommand { get; }
    public ICommand AddTagCommand { get; }
    public ICommand RemoveTagCommand { get; }
    public ICommand CreateFolderCommand { get; }
    public ICommand CreateNoteCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CreateFolderNoteCommand { get; }
    public ICommand DeleteFolderNoteCommand { get; }
    public ICommand CopyWikilinkCommand { get; }
    public ICommand PinCommand { get; }
    public ICommand UnpinCommand { get; }
    public ICommand UnpinAllCommand { get; }
    public ICommand AddShortcutCommand { get; }
    public ICommand RemoveShortcutCommand { get; }
    public ICommand UseVaultDefaultSortCommand { get; }
    public ICommand OpenCurrentCommand { get; }
    public ICommand OpenNewTabCommand { get; }
    public ICommand OpenSplitCommand { get; }
    public ICommand BatchMoveCommand { get; }
    public ICommand BatchTrashCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand CancelImportCommand { get; }
    public ICommand ClearRecentsCommand { get; }
    public ICommand CollapseAllCommand { get; }
    public ICommand ExpandLoadedCommand { get; }
    public ICommand HistoryBackCommand { get; }
    public ICommand HistoryForwardCommand { get; }

    /// <summary>The AUTHORITATIVE batch-check set (codex round 5):
    /// path → isDirectory. Node checkboxes are its projection — a
    /// checked descendant inside a collapsed folder survives every
    /// publication because the truth never lived on the discarded
    /// node.</summary>
    private readonly Dictionary<string, bool> _batchChecked =
        new(StringComparer.Ordinal);

    /// <summary>Project the authoritative checks onto a freshly
    /// published subtree, RECURSIVELY (codex rounds 6-7) — silent
    /// (the count did not change, only its visual projection), and
    /// UI-thread-only: the check set is dispatcher-confined, so
    /// callers are the publication boundaries, never the tree
    /// worker.</summary>
    private void ProjectBatchChecksOnto(
        IEnumerable<FileTreeNodeViewModel> nodes)
    {
        if (_batchChecked.Count == 0)
        {
            return;
        }

        foreach (FileTreeNodeViewModel node in Flatten(nodes))
        {
            if (_batchChecked.ContainsKey(node.Path))
            {
                _ = node.MarkBatchSelectedSilently();
            }
        }
    }

    internal void BatchCheckChanged(FileTreeNodeViewModel node)
    {
        if (node.IsBatchSelected)
        {
            _batchChecked[node.Path] = node.IsDirectory;
        }
        else
        {
            _batchChecked.Remove(node.Path);
        }

        BatchSelectionChanged();
    }

    public void BatchSelectionChanged()
    {
        BatchSelectionCount = _batchChecked.Count;
        _announce(BatchSelectionCount == 0
            ? new A11yEvent.NoItemsSelected()
            : new A11yEvent.ItemsSelected((uint)BatchSelectionCount));
    }

    public void ActivateTag(SidebarTagViewModel? tag)
    {
        if (tag is not null)
        {
            ActivateTag(tag.Full);
        }
    }

    public void ActivateTag(string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
        {
            FilterText = $"tag:\"{tag}\"";
        }
    }

    public void AssignShortcut(int index)
    {
        if (index is < 1 or > 9 || SelectedNode is not { IsGroupHeader: false, IsPlaceholder: false } node)
        {
            return;
        }

        var shortcut = new SidebarShortcutViewModel(node.IsDirectory ? "folder" : "file", node.Path);
        if (Shortcuts.Count < index)
        {
            Shortcuts.Add(shortcut);
            index = Shortcuts.Count;
        }
        else
        {
            Shortcuts[index - 1] = shortcut;
        }

        if (PersistShortcuts())
        {
            Status = $"Assigned {node.DisplayName} to shortcut {index}.";
        }
    }

    public void OpenShortcut(int index)
    {
        if (index >= 1 && index <= Math.Min(9, Shortcuts.Count))
        {
            RequestOpen(Shortcuts[index - 1].Path);
        }
    }

    private DirectoryLevel LoadDirectoryLevel(
        string parentPath,
        int level,
        bool includeDirectories = true,
        DirectoryOrdering? ordering = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ordering ??= CaptureDirectoryOrdering();
        int lookaheadLimit = MaxMaterializedDirectoryItems + 1;
        var directoryRows = new List<DirNodeSummary>();
        var fileRows = new List<FileSummary>();
        using var nativeCancellation = new CancelToken();
        using CancellationTokenRegistration registration =
            cancellationToken.Register(nativeCancellation.Cancel);
        bool providerTruncated;
        try
        {
            DirListingPage page = includeDirectories
                ? _session.ListDirChildrenPage(
                    parentPath,
                    new Paging(null, checked((uint)lookaheadLimit)),
                    nativeCancellation)
                : _session.ListDirFilesPage(
                    parentPath,
                    new Paging(null, checked((uint)lookaheadLimit)),
                    nativeCancellation);
            cancellationToken.ThrowIfCancellationRequested();
            if (includeDirectories)
            {
                directoryRows.AddRange(page.Dirs);
            }

            fileRows.AddRange(page.Files);
            providerTruncated = page.Truncated;
        }
        catch (VaultException.Cancelled) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        FileTreeNodeViewModel[] directories = includeDirectories
            ? directoryRows
                .Take(MaxMaterializedDirectoryItems)
                .Select(directory =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new FileTreeNodeViewModel(
                        this,
                        directory.Path,
                        directory.Name,
                        isDirectory: true,
                        level,
                        directory.ChildDirCount + directory.ChildFileCount > 0,
                        directory.HasFolderNote);
                }).ToArray()
            : [];
        var files = new List<FileTreeNodeViewModel>();
        bool truncated = providerTruncated
            || (includeDirectories && directoryRows.Count > directories.Length);
        int remaining = MaxMaterializedDirectoryItems - directories.Length;
        int filesTaken = Math.Min(fileRows.Count, Math.Max(0, remaining));
        foreach (FileSummary summary in fileRows.Take(filesTaken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            files.Add(new FileTreeNodeViewModel(
                this,
                summary.Path,
                summary.Name,
                isDirectory: false,
                level,
                hasChildren: false,
                summary: summary));
        }

        truncated |= filesTaken < fileRows.Count;

        IEnumerable<FileTreeNodeViewModel> sortedDirectories = SortNodes(directories, ordering);
        IEnumerable<FileTreeNodeViewModel> nodes;
        if (!ordering.GroupByDate)
        {
            nodes = SortNodes(directories.Concat(files), ordering);
        }
        else
        {
            nodes = sortedDirectories.Concat(GroupFilesByDate(files, ordering));
        }

        var output = nodes.ToList();
        if (truncated)
        {
            output.Add(FileTreeNodeViewModel.Overflow(
                $"More than {MaxMaterializedDirectoryItems:N0} items; refine the folder or filter."));
        }

        return new DirectoryLevel(output, files, directories.Length + files.Count, truncated);
    }

    private static string DirectoryOverflowStatus(string parentPath) => string.IsNullOrEmpty(parentPath)
        ? $"Showing the first {MaxMaterializedDirectoryItems:N0} items at the vault root. Use the filter to narrow the list."
        : $"Showing the first {MaxMaterializedDirectoryItems:N0} items in {Path.GetFileName(parentPath)}. Use the filter to narrow the list.";

    private sealed record DirectoryLevel(
        IReadOnlyList<FileTreeNodeViewModel> Nodes,
        IReadOnlyList<FileTreeNodeViewModel> FileNodes,
        int MaterializedCount,
        bool Truncated);

    private IEnumerable<FileTreeNodeViewModel> GroupFilesByDate(
        IEnumerable<FileTreeNodeViewModel> files,
        DirectoryOrdering ordering)
    {
        DateTime today = DateTime.Today;
        string[] order = ["Today", "Yesterday", "Previous 7 days", "Previous 30 days", "Older", "Unknown date"];
        return files
            .GroupBy(node => DateBucket(node, today, ordering.SortMode))
            .OrderBy(group => Array.IndexOf(order, group.Key))
            .Select(group => FileTreeNodeViewModel.Group(group.Key, SortNodes(group, ordering)));
    }

    private static string DateBucket(
        FileTreeNodeViewModel node,
        DateTime today,
        SidebarSortMode sortMode)
    {
        long? milliseconds = sortMode is SidebarSortMode.CreatedNewest or SidebarSortMode.CreatedOldest
            ? node.Summary?.CreatedMs
            : node.Summary?.MtimeMs;
        if (milliseconds is null)
        {
            return "Unknown date";
        }

        DateTime date;
        try
        {
            date = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds.Value).LocalDateTime.Date;
        }
        catch (ArgumentOutOfRangeException)
        {
            return "Unknown date";
        }

        double days = (today - date).TotalDays;
        return days switch
        {
            < 1 => "Today",
            < 2 => "Yesterday",
            < 7 => "Previous 7 days",
            < 30 => "Previous 30 days",
            _ => "Older",
        };
    }

    private IEnumerable<FileTreeNodeViewModel> SortNodes(
        IEnumerable<FileTreeNodeViewModel> nodes,
        DirectoryOrdering? ordering = null)
    {
        ordering ??= CaptureDirectoryOrdering();
        IOrderedEnumerable<FileTreeNodeViewModel> ordered = nodes.OrderByDescending(
            node => ordering.Pinned.Contains(node.Path));
        return ordering.SortMode switch
        {
            SidebarSortMode.NameDescending => ordered.ThenByDescending(node => node.DisplayName, StringComparer.OrdinalIgnoreCase),
            SidebarSortMode.ModifiedNewest => ordered.ThenByDescending(node => node.Summary?.MtimeMs ?? long.MinValue),
            SidebarSortMode.ModifiedOldest => ordered.ThenBy(node => node.Summary?.MtimeMs ?? long.MaxValue),
            SidebarSortMode.CreatedNewest => ordered.ThenByDescending(node => node.Summary?.CreatedMs ?? long.MinValue),
            SidebarSortMode.CreatedOldest => ordered.ThenBy(node => node.Summary?.CreatedMs ?? long.MaxValue),
            _ => ordered.ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase),
        };
    }

    private DirectoryOrdering CaptureDirectoryOrdering() => new(
        SortMode,
        GroupByDate,
        _pinned.ToHashSet(StringComparer.Ordinal));

    private void ResortMaterializedTree()
    {
        if (GroupByDate)
        {
            Refresh();
            return;
        }

        Resort(RootNodes);
        foreach (FileTreeNodeViewModel directory in Flatten(RootNodes).Where(node => node.IsDirectory))
        {
            Resort(directory.Children);
        }
    }

    private void Resort(ObservableCollection<FileTreeNodeViewModel> collection)
    {
        FileTreeNodeViewModel[] sorted = SortNodes(collection).ToArray();
        collection.Clear();
        foreach (FileTreeNodeViewModel node in sorted)
        {
            collection.Add(node);
        }
    }

    private void LoadTags()
    {
        if (!TryRunSessionWork(
            () =>
            {
                ++_tagGeneration;
                return BuildTags(CancellationToken.None);
            },
            out TagLoadOutcome outcome))
        {
            return;
        }

        ApplyTags(outcome);
    }

    private TagLoadOutcome BuildTags(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            TagTree tree = _session.TagTree();
            var roots = new List<SidebarTagViewModel>();
            var ancestors = new List<SidebarTagViewModel>();
            foreach (TagTreeEntry entry in tree.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tag = new SidebarTagViewModel(
                    entry.Segment,
                    entry.Full,
                    entry.FileCount,
                    entry.DirectCount,
                    entry.Depth);
                while (ancestors.Count > entry.Depth)
                {
                    ancestors.RemoveAt(ancestors.Count - 1);
                }

                if (entry.Depth == 0 || ancestors.Count == 0)
                {
                    roots.Add(tag);
                }
                else
                {
                    ancestors[^1].Children.Add(tag);
                }

                ancestors.Add(tag);
            }

            return new TagLoadOutcome(roots, null);
        }
        catch (VaultException exception)
        {
            return new TagLoadOutcome([], $"Could not load tags: {exception.Message}");
        }
    }

    private void ApplyTags(TagLoadOutcome outcome)
    {
        Tags.Clear();
        foreach (SidebarTagViewModel tag in outcome.Tags)
        {
            Tags.Add(tag);
        }

        if (outcome.Error is not null)
        {
            ReportFailure(outcome.Error);
        }
    }

    private sealed record TagLoadOutcome(
        IReadOnlyList<SidebarTagViewModel> Tags,
        string? Error);

    private void EditTag(bool add)
    {
        string[] paths = [.. _batchChecked
            .Where(pair => !pair.Value)
            .Select(pair => pair.Key)
            .OrderBy(path => path, StringComparer.Ordinal)];
        if (paths.Length == 0 || TagInput.Length == 0)
        {
            return;
        }

        try
        {
            if (!TryRunSessionWork(
                () => add
                    ? _session.AddTagToFiles(paths, TagInput)
                    : _session.RemoveTagFromFiles(paths, TagInput),
                out TagEditReport report))
            {
                return;
            }

            Status = report.AudioSummary;
            // W0.5-3 residue: core tag report carries engine-composed audio copy.
            _announce(new A11yEvent.HostComposed(report.AudioSummary, A11yPriority.Medium));
            LoadTags();
        }
        catch (VaultException exception)
        {
            ReportFailure($"Tag edit failed: {exception.Message}");
        }
    }

    private void CreateFolder()
    {
        // W5-4 F2: auto-named untitled create — the MutationName
        // text-box flow retired for this verb (F1's shape). The
        // report-returning CreateFolder is the structural verb; its
        // report is consumed per F9.
        string parent = CreationParentPath();
        string attempted = "Untitled Folder";
        try
        {
            string? created = null;
            foreach (string candidate in UntitledCandidates(
                parent, "Untitled Folder", string.Empty))
            {
                attempted = System.IO.Path.GetFileName(candidate);
                try
                {
                    StructuralReport? report = null;
                    if (!TryRunSessionWork(() => report = _session.CreateFolder(candidate)))
                    {
                        return;
                    }

                    if (report is not null)
                    {
                        _ = ConsumeStructuralReport(report);
                    }

                    created = candidate;
                    break;
                }
                catch (VaultException.DestinationExists)
                {
                    // The unique-untitled advance — typed, never a
                    // pre-check (F1's CreateExclusive rule).
                }
            }

            if (created is null)
            {
                ReportFailure($"Could not create folder {attempted}: no free name.");
                return;
            }

            // W5-4 F10: creates are structural history barriers. The
            // sentence reports AFTER Refresh (codex round 1).
            StructuralHistoryBarrier();
            RequestInlineRenameAt(created);
            Refresh();
            ReportResult($"Created folder {System.IO.Path.GetFileName(created)}.");
        }
        catch (VaultException exception)
        {
            ReportFailure($"Could not create folder {attempted}: {exception.Message}");
        }
    }

    /// <summary>The creation-parent rule, shared with the template flow
    /// (W5-3 T12): the selected directory, else the selection's parent,
    /// else the vault root — mac's canonicalSidebarCreationParent
    /// semantics.</summary>
    internal string CreationParentPath() =>
        SelectedNode?.IsDirectory == true ? SelectedNode.Path : ParentPath(SelectedNode?.Path);

    private void CreateNote()
    {
        // W5-4 F1: mac's auto-named untitled flow — the unique-untitled
        // sequence via CreateExclusive ONLY (typed DestinationExists,
        // never a pre-check), open in the current tab, then hand off to
        // inline rename with the stem selected.
        string parent = CreationParentPath();
        string attempted = "Untitled.md";
        try
        {
            string? created = null;
            foreach (string candidate in UntitledCandidates(parent, "Untitled", ".md"))
            {
                attempted = System.IO.Path.GetFileName(candidate);
                try
                {
                    if (!TryRunSessionWork(
                        () => _session.CreateExclusive(candidate, string.Empty)))
                    {
                        return;
                    }

                    created = candidate;
                    break;
                }
                catch (VaultException.DestinationExists)
                {
                    // The unique-untitled advance signal.
                }
            }

            if (created is null)
            {
                ReportFailure($"Could not create note {attempted}: no free name.");
                return;
            }

            // W5-4 F10: a create is a structural history barrier (mac's
            // table) — a stale inverse must never target this path.
            // The sentence reports AFTER Refresh so it survives the
            // "Loading files…" write (codex round 1).
            StructuralHistoryBarrier();
            RequestInlineRenameAt(created);
            Refresh();
            ReportResult($"Created note {System.IO.Path.GetFileName(created)}.");
            RequestOpen(created);
        }
        catch (VaultException exception)
        {
            ReportFailure($"Could not create note {attempted}: {exception.Message}");
        }
    }

    // RenameSelected moved to FilesSidebarViewModel.FileManagement.cs
    // as TryRenameSelected (W5-4 F3: report consumption, the
    // unconditional RenameFolderWithNote, error-keeps-field).

    private void DeleteSelected()
    {
        if (SelectedNode is not
            { IsPlaceholder: false, IsGroupHeader: false } node)
        {
            return;
        }

        // W5-4 F6 (mac #860/#852 semantics): files and EMPTY folders
        // trash immediately — no confirmation (Finder parity). A
        // non-empty folder stages the styled confirmation; the probe
        // runs AT STAGE TIME, and an unreadable folder is fail-closed
        // (confirmed like a non-empty one, with the folder-clause
        // message because no count exists to speak).
        BeginStructuralResult();
        if (node.IsDirectory)
        {
            int? contents = CountFolderContents(node.Path);
            if (contents is not 0)
            {
                string message = contents is int known
                    ? RecycleBinCopy.SingleFolderMessage(node.DisplayName, known)
                    : RecycleBinCopy.BatchMessage(1, 1);
                if (!ConfirmRecycle(
                    (RecycleBinCopy.SingleFolderTitle(node.DisplayName), message)))
                {
                    return;
                }
            }
        }

        try
        {
            if (!TryRunSessionWork(() =>
            {
                if (node.IsDirectory)
                {
                    _session.DeleteFolder(node.Path);
                }
                else
                {
                    _session.DeleteFile(node.Path);
                }
            }))
            {
                return;
            }

            TransformStoredPaths(node.Path, node.Path, node.IsDirectory, deleted: true);
            // W5-4 F10: trash is not undoable AND a history barrier
            // (mac's rule — the bytes are in the Recycle Bin).
            StructuralHistoryBarrier();
            SelectedNode = null;
            // F6: "focus returns to the tree" — the publication's
            // container discard would otherwise eject keyboard focus
            // to the window (red team, a11y 2a). The sentence reports
            // AFTER Refresh (codex round 1).
            RequestSelectionAt(null);
            Refresh();
            ReportMutationResult($"Moved {node.DisplayName} to the Recycle Bin.");
        }
        catch (VaultException exception)
        {
            ReportFailure($"Delete failed: {exception.Message}");
        }
    }

    private void CreateFolderNote()
    {
        if (SelectedNode is not { IsDirectory: true } node)
        {
            return;
        }

        try
        {
            string path = FolderNotePath(node);
            if (!TryRunSessionWork(() => _session.CreateExclusive(path, $"# {node.Name}\n")))
            {
                return;
            }

            // W5-4 F10 (verification 4): a folder-note create is a
            // CREATE — a structural history barrier like its siblings.
            StructuralHistoryBarrier();
            RequestSelectionAt(null);
            Refresh();
            ReportResult($"Created folder note {path}.");
            RequestOpen(path);
        }
        catch (VaultException exception)
        {
            ReportFailure($"Could not create folder note: {exception.Message}");
        }
    }

    private void DeleteFolderNote()
    {
        if (SelectedNode is not { IsDirectory: true } node)
        {
            return;
        }

        if (!_confirmDestructive($"Delete the folder note for {node.DisplayName}?"))
        {
            return;
        }

        try
        {
            if (!TryRunSessionWork(() => _session.DeleteFile(FolderNotePath(node))))
            {
                return;
            }

            // W5-4 F10 (verification 4): trash is not undoable AND a
            // barrier — the folder-note delete is a trash.
            StructuralHistoryBarrier();
            RequestSelectionAt(null);
            Refresh();
            ReportResult($"Deleted the {node.DisplayName} folder note.");
        }
        catch (VaultException exception)
        {
            ReportFailure($"Could not delete folder note: {exception.Message}");
        }
    }

    private void CopyWikilink()
    {
        if (SelectedNode is not { IsDirectory: false } node)
        {
            return;
        }

        try
        {
            if (!TryRunSessionWork(
                () => _session.WikilinkForPath(node.Path),
                out string? link))
            {
                return;
            }

            if (link is not null)
            {
                _copyText(link);
                ReportResult($"Copied wikilink for {node.DisplayName}.");
                _announce(new A11yEvent.SelectionCopied());
            }
        }
        catch (VaultException exception)
        {
            ReportFailure($"Could not copy wikilink: {exception.Message}");
        }
    }

    private void PinSelected()
    {
        if (SelectedNode is { IsDirectory: false } node)
        {
            _pinned.Add(node.Path);
            bool saved = PersistPins();
            ResortMaterializedTree();
            if (saved)
            {
                ReportResult($"Pinned {node.DisplayName}.");
            }
        }
    }

    private void UnpinSelected()
    {
        if (SelectedNode is { IsDirectory: false } node)
        {
            _pinned.Remove(node.Path);
            bool saved = PersistPins();
            ResortMaterializedTree();
            if (saved)
            {
                ReportResult($"Unpinned {node.DisplayName}.");
            }
        }
    }

    private void UnpinAllInFolder()
    {
        string folder = SelectedNode?.IsDirectory == true
            ? SelectedNode.Path
            : ParentPath(SelectedNode?.Path);
        _pinned.RemoveWhere(path => string.Equals(ParentPath(path), folder, StringComparison.Ordinal));
        bool saved = PersistPins();
        Refresh();
        if (saved)
        {
            Status = string.IsNullOrEmpty(folder)
                ? "Unpinned all files at the vault root."
                : $"Unpinned all files in {folder}.";
        }
    }

    private void AddShortcut()
    {
        if (SelectedNode is not { IsPlaceholder: false, IsGroupHeader: false } node)
        {
            return;
        }

        string kind = node.IsDirectory ? "folder" : "file";
        if (!Shortcuts.Any(item => item.Kind == kind && item.Path == node.Path))
        {
            Shortcuts.Add(new SidebarShortcutViewModel(kind, node.Path));
            if (!PersistShortcuts())
            {
                return;
            }
        }

        ReportResult($"Added {node.DisplayName} to shortcuts.");
        RaiseCommandStates();
    }

    private void RemoveShortcut()
    {
        if (SelectedShortcut is not SidebarShortcutViewModel shortcut)
        {
            return;
        }

        Shortcuts.Remove(shortcut);
        SelectedShortcut = null;
        if (PersistShortcuts())
        {
            ReportResult($"Removed {shortcut.DisplayName} from shortcuts.");
        }
    }

    private void UseVaultDefaultSort()
    {
        _sortMode = SidebarSortMode.NameAscending;
        _groupByDate = false;
        OnPropertyChanged(nameof(SortMode));
        OnPropertyChanged(nameof(GroupByDate));
        bool saved = PersistOrganization();
        Refresh();
        if (saved)
        {
            AnnounceSort();
        }

        RaiseCommandStates();
    }

    private bool CanOpenSelected() => SelectedNode is
    {
        IsPlaceholder: false,
        IsGroupHeader: false,
    } node && (!node.IsDirectory || node.HasFolderNote);

    private void OpenSelected(WorkspaceOpenTarget target)
    {
        if (!CanOpenSelected() || SelectedNode is not FileTreeNodeViewModel node)
        {
            return;
        }

        RequestOpen(node.IsDirectory ? FolderNotePath(node) : node.Path, target, trackHistory: true);
    }

    private StructuralBatchItem[] SelectedBatchItems() => [.. _batchChecked
        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => new StructuralBatchItem(pair.Key, pair.Value))];

    private void BatchMove()
    {
        StructuralBatchItem[] items = SelectedBatchItems();
        if (items.Length == 0)
        {
            return;
        }

        BeginStructuralResult();
        try
        {
            if (!TryRunSessionWork(
                () => _session.BatchMove(new BatchMoveRequest(items, MoveDestination)),
                out BatchMoveReport report))
            {
                return;
            }

            foreach (BatchPathChange change in report.Standing)
            {
                RetargetRequested?.Invoke(change.OldPath, change.NewPath);
                TransformStoredPaths(change.OldPath, change.NewPath, change.IsDirectory, deleted: false);
            }

            // W5-4 F10: a SUCCEEDED batch move is undoable through the
            // dedicated endpoint; anything less than Succeeded is not a
            // clean inverse and records nothing.
            if (report.State == BatchMoveState.Succeeded && report.OpId is long opId)
            {
                _structuralUndo.Push(new StructuralUndoStep(
                    StructuralUndoKind.BatchMove,
                    Path: string.Empty,
                    Argument: string.Empty,
                    IsDirectory: false,
                    Noun: $"{report.Standing.Length:N0} "
                        + (report.Standing.Length == 1 ? "item" : "items"),
                    BatchOpId: opId));
            }

            RequestSelectionAt(null);
            Refresh();
            ReportMutationResult(BatchMoveSummary(report, MoveDestination));
        }
        catch (VaultException exception)
        {
            ReportFailure($"Move failed: {exception.Message}");
        }
    }

    private void BatchTrash()
    {
        StructuralBatchItem[] items = SelectedBatchItems();
        if (items.Length == 0)
        {
            return;
        }

        // W5-4 F6: a batch confirms only when it carries a non-empty
        // folder (Finder parity — files and empty folders go
        // straight to the Recycle Bin). The probe runs at stage time;
        // unreadable counts as non-empty (fail-closed).
        int nonEmptyFolders = items.Count(
            item => item.IsDirectory && CountFolderContents(item.Path) is not 0);
        if (nonEmptyFolders > 0 && !ConfirmRecycle((
            RecycleBinCopy.BatchTitle(items.Length),
            RecycleBinCopy.BatchMessage(items.Length, nonEmptyFolders))))
        {
            return;
        }

        BeginStructuralResult();
        try
        {
            if (!TryRunSessionWork(
                () => _session.BatchTrash(new BatchTrashRequest(items)),
                out BatchTrashReport report))
            {
                return;
            }

            foreach (StructuralBatchItem item in report.Trashed)
            {
                TransformStoredPaths(item.Path, item.Path, item.IsDirectory, deleted: true);
            }

            // W5-4 F10: trash is not undoable AND a history barrier.
            StructuralHistoryBarrier();
            RequestSelectionAt(null);
            Refresh();
            ReportMutationResult(BatchTrashSummary(report));
        }
        catch (VaultException exception)
        {
            ReportFailure($"Trash failed: {exception.Message}");
        }
    }

    private static string BatchMoveSummary(BatchMoveReport report, string destination)
    {
        string Count(int count) => $"{count:N0} {(count == 1 ? "item" : "items")}";
        return report.State switch
        {
            BatchMoveState.Rejected => "Move could not start. No items were moved.",
            BatchMoveState.NoOp => "Nothing moved.",
            BatchMoveState.Succeeded =>
                $"Moved {Count(report.Standing.Length)} to "
                + (string.IsNullOrEmpty(destination)
                    ? "vault root."
                    : $"{Path.GetFileName(destination.TrimEnd('/'))}."),
            BatchMoveState.RolledBack =>
                "Move stopped. Slate restored every item to its original location.",
            _ => $"Move stopped. Slate restored {Count(report.RolledBack.Length)}. "
                + $"{Count(report.Standing.Length)} "
                + (report.Standing.Length == 1 ? "remains in its" : "remain in their")
                + " new location.",
        };
    }

    private static string BatchTrashSummary(BatchTrashReport report)
    {
        string Count(int count) => $"{count:N0} {(count == 1 ? "item" : "items")}";
        int total = Math.Max(
            report.Envelope.Planned.Length,
            report.Trashed.Length + report.Untrashed.Length + report.Unknown.Length);
        string UnknownSentence() =>
            $"Couldn’t verify whether {Count(report.Unknown.Length)} moved to the Recycle Bin."
            + (report.RequiresRescan ? " Rescan required." : string.Empty);
        return report.State switch
        {
            BatchTrashState.Rejected when report.Unknown.Length > 0 =>
                "Couldn’t start moving the selected items to the Recycle Bin. " + UnknownSentence(),
            BatchTrashState.Rejected =>
                "Couldn’t start moving the selected items to the Recycle Bin.",
            BatchTrashState.NoOp when report.Unknown.Length > 0 => UnknownSentence(),
            BatchTrashState.NoOp => "Nothing was moved to the Recycle Bin.",
            BatchTrashState.Succeeded when report.Trashed.Length == 0 && report.Unknown.Length > 0 =>
                UnknownSentence(),
            BatchTrashState.Succeeded when report.Trashed.Length == 0 =>
                "Recycle Bin result could not be reconciled safely.",
            BatchTrashState.Succeeded => $"Moved {Count(report.Trashed.Length)} to the Recycle Bin."
                + (report.Unknown.Length > 0 ? " " + UnknownSentence() : string.Empty),
            BatchTrashState.Partial =>
                $"Moved {report.Trashed.Length:N0} of {Count(total)} to the Recycle Bin."
                + (report.Untrashed.Length > 0
                    ? $" {Count(report.Untrashed.Length)} "
                        + (report.Untrashed.Length == 1 ? "was" : "were") + " not moved."
                    : string.Empty)
                + (report.Unknown.Length > 0 ? " " + UnknownSentence() : string.Empty),
            _ when report.Unknown.Length > 0 =>
                (report.Trashed.Length > 0
                    ? $"Moved {report.Trashed.Length:N0} of {Count(total)} to the Recycle Bin. "
                    : string.Empty)
                + (report.Untrashed.Length > 0
                    ? $"{Count(report.Untrashed.Length)} "
                        + (report.Untrashed.Length == 1 ? "was" : "were") + " not moved. "
                    : string.Empty)
                + UnknownSentence(),
            _ when report.Trashed.Length == 0 =>
                $"Couldn’t move {Count(total)} to the Recycle Bin.",
            _ => $"Moved {report.Trashed.Length:N0} of {Count(total)} to the Recycle Bin, "
                + "but the operation did not finish safely.",
        };
    }

    private void RequestOpen(string path)
    {
        RequestOpen(path, WorkspaceOpenTarget.CurrentTab, trackHistory: true);
    }

    private void RequestOpen(
        string path,
        WorkspaceOpenTarget target,
        bool trackHistory)
    {
        _recents.Remove(path);
        _recents.Insert(0, path);
        if (_recents.Count > FileRecentsStore.MaxEntries)
        {
            _recents.RemoveRange(FileRecentsStore.MaxEntries, _recents.Count - FileRecentsStore.MaxEntries);
        }

        _recentsStore?.Add(path);
        if (trackHistory)
        {
            if (_historyIndex >= 0 && _historyIndex < _history.Count - 1)
            {
                _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            }

            if (_history.Count == 0 || !string.Equals(_history[^1], path, StringComparison.Ordinal))
            {
                _history.Add(path);
            }

            _historyIndex = _history.Count - 1;
        }

        OpenTargetRequested?.Invoke(this, (path, target));
        RaiseCommandStates();
    }

    private void ClearRecents()
    {
        _recents.Clear();
        _recentsStore?.Clear();
        Status = "Cleared sidebar recents.";
        RaiseCommandStates();
    }

    private void History(int direction)
    {
        int candidate = _historyIndex + direction;
        if (candidate >= 0 && candidate < _history.Count)
        {
            _historyIndex = candidate;
            RequestOpen(_history[_historyIndex], WorkspaceOpenTarget.CurrentTab, trackHistory: false);
        }
    }

    private void LoadDualPane(string parentPath)
    {
        if (!IsDualPaneEnabled)
        {
            return;
        }

        try
        {
            if (!TryRunSessionWork(
                () => LoadDirectoryLevel(
                    parentPath,
                    1,
                    includeDirectories: false),
                out DirectoryLevel level))
            {
                return;
            }

            DualPaneFiles.Clear();
            foreach (FileTreeNodeViewModel item in level.FileNodes)
            {
                DualPaneFiles.Add(item);
            }

            if (level.Truncated)
            {
                DualPaneFiles.Add(FileTreeNodeViewModel.Overflow(
                    $"More than {MaxMaterializedDirectoryItems:N0} items; refine the folder."));
                Status = DirectoryOverflowStatus(parentPath);
            }
        }
        catch (VaultException exception)
        {
            ReportFailure($"Could not load folder files: {exception.Message}");
        }
    }

    private sealed record DirectoryOrdering(
        SidebarSortMode SortMode,
        bool GroupByDate,
        IReadOnlySet<string> Pinned);

    private void RestoreExpansions(IEnumerable<FileTreeNodeViewModel> nodes)
    {
        foreach (FileTreeNodeViewModel node in nodes.Where(node => node.IsDirectory))
        {
            if (RestoredExpandedPaths.Contains(node.Path))
            {
                node.IsExpanded = true;
            }
        }
    }

    private static IEnumerable<FileTreeNodeViewModel> Flatten(IEnumerable<FileTreeNodeViewModel> roots)
    {
        foreach (FileTreeNodeViewModel node in roots)
        {
            yield return node;
            foreach (FileTreeNodeViewModel child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }

    private static string FolderNotePath(FileTreeNodeViewModel node) =>
        CombineVaultPath(node.Path, $"{node.Name}.md");

    private static string ParentPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.Contains('/'))
        {
            return string.Empty;
        }

        return path[..path.LastIndexOf('/')];
    }

    private static string CombineVaultPath(string parent, string name) =>
        string.IsNullOrEmpty(parent) ? name : $"{parent}/{name}";

    private bool PersistOrganization()
    {
        try
        {
            _settingsStore?.SetOrganization(SortMode, GroupByDate);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ReportFailure($"Could not save sidebar organization: {exception.Message}");
            HostLog.Write(HostDiagnosticEvent.SidebarOrganizationPersistFailed, exception);
            return false;
        }
    }

    private void AnnounceSort()
    {
        Status = SortAnnouncement();
        // W0.5-3 residue: mac SidebarOrganization.sortAnnouncement builder.
        _announce(new A11yEvent.HostComposed(Status, A11yPriority.Medium));
    }

    private string SortAnnouncement()
    {
        string field = SortMode switch
        {
            SidebarSortMode.CreatedNewest or SidebarSortMode.CreatedOldest => "created",
            SidebarSortMode.ModifiedNewest or SidebarSortMode.ModifiedOldest => "modified",
            _ => "name",
        };
        string direction = SortMode switch
        {
            SidebarSortMode.NameDescending => "Z to A",
            SidebarSortMode.NameAscending => "A to Z",
            SidebarSortMode.CreatedOldest or SidebarSortMode.ModifiedOldest => "oldest first",
            _ => "newest first",
        };
        return $"Sorted by {field}, {direction}{(GroupByDate ? ", grouped by date" : string.Empty)}.";
    }

    private void ReportFailure(string message)
    {
        Status = message;
        if (IsRefreshingTree)
        {
            // Codex round 2: same reassert discipline as results.
            _statusToReassert = Status;
        }

        // W0.5-3 residue: Windows sidebar availability/error copy.
        _announce(new A11yEvent.HostComposed(message, A11yPriority.High));
    }

    private void ReportResult(string message)
    {
        Status = message;
        if (IsRefreshingTree)
        {
            // Codex round 2: the in-flight refresh's publication arms
            // must not erase the result the user just heard.
            _statusToReassert = Status;
        }

        // W0.5-3 residue: Windows sidebar action-result copy.
        _announce(new A11yEvent.HostComposed(message, A11yPriority.Medium));
    }

    private bool PersistPins()
    {
        try
        {
            _settingsStore?.ReplacePins(_pinned);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ReportFailure($"Could not save pins: {exception.Message}");
            HostLog.Write(HostDiagnosticEvent.SidebarPinsPersistFailed, exception);
            return false;
        }
    }

    private bool PersistShortcuts()
    {
        try
        {
            _settingsStore?.SetShortcuts(
                Shortcuts.Select(item => new SidebarShortcutState(item.Kind, item.Path)));
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ReportFailure($"Could not save shortcuts: {exception.Message}");
            HostLog.Write(HostDiagnosticEvent.SidebarShortcutsPersistFailed, exception);
            return false;
        }
    }

    private void TransformStoredPaths(
        string oldPath,
        string newPath,
        bool isDirectory,
        bool deleted)
    {
        string? Transform(string path)
        {
            if (string.Equals(path, oldPath, StringComparison.Ordinal))
            {
                return deleted ? null : newPath;
            }

            string prefix = oldPath + "/";
            if (isDirectory && path.StartsWith(prefix, StringComparison.Ordinal))
            {
                return deleted ? null : newPath + "/" + path[prefix.Length..];
            }

            return path;
        }

        string[] transformedPins = _pinned
            .Select(Transform)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        _pinned.Clear();
        _pinned.UnionWith(transformedPins);

        SidebarShortcutViewModel? selected = SelectedShortcut;
        SidebarShortcutViewModel[] transformedShortcuts = Shortcuts
            .Select(item => (Item: item, Path: Transform(item.Path)))
            .Where(pair => pair.Path is not null)
            .Select(pair => new SidebarShortcutViewModel(pair.Item.Kind, pair.Path!))
            .Distinct()
            .ToArray();
        Shortcuts.Clear();
        foreach (SidebarShortcutViewModel shortcut in transformedShortcuts)
        {
            Shortcuts.Add(shortcut);
        }

        SelectedShortcut = selected is null
            ? null
            : Shortcuts.FirstOrDefault(item =>
                item.Kind == selected.Kind && item.Path == Transform(selected.Path));

        string[] transformedRecents = _recents
            .Select(Transform)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        _recents.Clear();
        _recents.AddRange(transformedRecents);
        _recentsStore?.Replace(_recents);

        for (int index = _history.Count - 1; index >= 0; index--)
        {
            string? transformed = Transform(_history[index]);
            if (transformed is null)
            {
                _history.RemoveAt(index);
                if (_historyIndex >= index)
                {
                    _historyIndex--;
                }
            }
            else
            {
                _history[index] = transformed;
            }
        }

        // The authoritative batch checks follow renames/moves and drop
        // with deletions exactly like the other stored paths (codex
        // round 5). Silent — a rename must not speak selection.
        var transformedChecked = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach ((string path, bool isDir) in _batchChecked)
        {
            if (Transform(path) is string transformedPath)
            {
                transformedChecked[transformedPath] = isDir;
            }
        }

        _batchChecked.Clear();
        foreach ((string path, bool isDir) in transformedChecked)
        {
            _batchChecked[path] = isDir;
        }

        BatchSelectionCount = _batchChecked.Count;

        _historyIndex = Math.Clamp(_historyIndex, -1, _history.Count - 1);
        // Codex round 2: a failed settings write must not vanish under
        // the mutation's success sentence — the detail composes into
        // the FINAL status, so a restart-level pins/shortcuts
        // divergence is announced when it is created, not discovered
        // later.
        bool pinsPersisted = PersistPins();
        bool shortcutsPersisted = PersistShortcuts();
        if (!pinsPersisted || !shortcutsPersisted)
        {
            RecordStoredPathPersistFailure();
        }
    }

    private void RaiseCommandStates()
    {
        foreach (ICommand command in new[]
        {
            ClearFilterCommand,
            AddTagCommand,
            RemoveTagCommand,
            CreateFolderCommand,
            CreateNoteCommand,
            RenameCommand,
            DeleteCommand,
            CreateFolderNoteCommand,
            DeleteFolderNoteCommand,
            CopyWikilinkCommand,
            DuplicateCommand,
            CopyPathCommand,
            RevealCommand,
            MoveToCommand,
            PinCommand,
            UnpinCommand,
            UnpinAllCommand,
            AddShortcutCommand,
            RemoveShortcutCommand,
            UseVaultDefaultSortCommand,
            OpenCurrentCommand,
            OpenNewTabCommand,
            OpenSplitCommand,
            BatchMoveCommand,
            BatchTrashCommand,
            ClearRecentsCommand,
            HistoryBackCommand,
            HistoryForwardCommand,
        })
        {
            ((RelayCommand)command).RaiseCanExecuteChanged();
        }


        ((AsyncRelayCommand)ImportCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CancelImportCommand).RaiseCanExecuteChanged();
    }
}
