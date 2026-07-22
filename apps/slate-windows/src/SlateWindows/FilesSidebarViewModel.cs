// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
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

internal sealed class FileTreeNodeViewModel : BindableBase
{
    private readonly FilesSidebarViewModel? _owner;
    private bool _isExpanded;
    private bool _isBatchSelected;

    private FileTreeNodeViewModel(string loadingLabel)
    {
        Name = loadingLabel;
        Path = string.Empty;
        IsPlaceholder = true;
    }

    private FileTreeNodeViewModel(string groupLabel, IEnumerable<FileTreeNodeViewModel> children)
    {
        Name = groupLabel;
        Path = string.Empty;
        IsGroupHeader = true;
        _isExpanded = true;
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
        }
    }

    public static FileTreeNodeViewModel Loading() => new("Loading…");
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
    public ObservableCollection<FileTreeNodeViewModel> Children { get; } = [];

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
            if (SetField(ref _isExpanded, value) && value)
            {
                _owner?.LoadChildren(this);
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
                _owner?.BatchSelectionChanged();
            }
        }
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

    internal void ReplaceChildren(IEnumerable<FileTreeNodeViewModel> children)
    {
        Children.Clear();
        foreach (FileTreeNodeViewModel child in children)
        {
            Children.Add(child);
        }
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
internal sealed class FilesSidebarViewModel : BindableBase
{
    private const uint PageLimit = 500;
    internal const int MaxMaterializedDirectoryItems = 5_000;
    internal const int MaxImportEntries = 10_000;
    internal const long MaxImportFileBytes = 256L * 1024 * 1024;
    private const int FilterDebounceMilliseconds = 200;
    private readonly VaultSession _session;
    private readonly Action<A11yEvent> _announce;
    private readonly Action<string> _copyText;
    private readonly Func<string, bool> _confirmDestructive;
    private readonly Func<Task<IReadOnlyList<string>>> _pickImportSources;
    private readonly string? _vaultRoot;
    private readonly SidebarSettingsStore? _settingsStore;
    private readonly FileRecentsStore? _recentsStore;
    private readonly string? _settingsNotice;
    private readonly SynchronizationContext? _filterUiContext;
    private readonly HashSet<string> _expandedPaths;
    private readonly HashSet<string> _pinned = new(StringComparer.Ordinal);
    private readonly List<string> _recents = [];
    private readonly List<string> _history = [];
    private int _historyIndex = -1;
    private CancellationTokenSource? _importCancellation;
    private CancellationTokenSource? _filterCancellation;
    private CancellationTokenSource? _bulkExpandCancellation;
    private Task _filterCompletion = Task.CompletedTask;
    private Task _expandLoadedCompletion = Task.CompletedTask;
    private int _filterGeneration;
    private int _treeGeneration;
    private FileTreeNodeViewModel? _selectedNode;
    private SidebarShortcutViewModel? _selectedShortcut;
    private string _filterText = string.Empty;
    private string _status = string.Empty;
    private string _mutationName = string.Empty;
    private string _tagInput = string.Empty;
    private SidebarSortMode _sortMode;
    private bool _groupByDate;
    private bool _isDualPaneEnabled;
    private bool _showTags;
    private bool _isImporting;
    private bool _isExpandingLoaded;
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
        SynchronizationContext? filterUiContext = null)
    {
        _session = session;
        _announce = announce;
        _copyText = copyText ?? (_ => { });
        _confirmDestructive = confirmDestructive ?? (_ => true);
        _pickImportSources = pickImportSources ?? (() => Task.FromResult<IReadOnlyList<string>>([]));
        _filterUiContext = filterUiContext ?? SynchronizationContext.Current;
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
        CreateFolderCommand = new RelayCommand(_ => CreateFolder(), _ => !IsImporting && MutationName.Length > 0);
        CreateNoteCommand = new RelayCommand(_ => CreateNote(), _ => !IsImporting && MutationName.Length > 0);
        RenameCommand = new RelayCommand(
            _ => RenameSelected(),
            _ => !IsImporting
                && SelectedNode is { IsPlaceholder: false, IsGroupHeader: false }
                && MutationName.Length > 0);
        DeleteCommand = new RelayCommand(_ => DeleteSelected(), _ => !IsImporting && SelectedNode is not null);
        CreateFolderNoteCommand = new RelayCommand(_ => CreateFolderNote(), _ => !IsImporting && SelectedNode?.IsDirectory == true && !SelectedNode.HasFolderNote);
        DeleteFolderNoteCommand = new RelayCommand(_ => DeleteFolderNote(), _ => !IsImporting && SelectedNode?.IsDirectory == true && SelectedNode.HasFolderNote);
        CopyWikilinkCommand = new RelayCommand(_ => CopyWikilink(), _ => SelectedNode is { IsDirectory: false });
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
        ImportCommand = new AsyncRelayCommand(_ => ImportAsync(), () => !IsImporting);
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

    public ObservableCollection<FileTreeNodeViewModel> RootNodes { get; } = [];
    public ObservableCollection<FileTreeNodeViewModel> FilterResults { get; } = [];
    public ObservableCollection<FileTreeNodeViewModel> DualPaneFiles { get; } = [];
    public ObservableCollection<SidebarTagViewModel> Tags { get; } = [];
    public ObservableCollection<SidebarShortcutViewModel> Shortcuts { get; } = [];
    public IReadOnlySet<string> RestoredExpandedPaths => _expandedPaths;

    internal Task FilterCompletion => _filterCompletion;
    internal Task ExpandLoadedCompletion => _expandLoadedCompletion;
    internal bool IsFiltering => !_filterCompletion.IsCompleted;

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

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetField(ref _filterText, value))
            {
                OnPropertyChanged(nameof(IsFilterActive));
                ScheduleFilter();
                RaiseCommandStates();
            }
        }
    }

    public bool IsFilterActive => !string.IsNullOrWhiteSpace(FilterText);

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

    public bool IsImporting
    {
        get => _isImporting;
        private set
        {
            if (SetField(ref _isImporting, value))
            {
                OnPropertyChanged(nameof(ImportStatus));
                RaiseCommandStates();
            }
        }
    }

    public bool IsExpandingLoaded
    {
        get => _isExpandingLoaded;
        private set => SetField(ref _isExpandingLoaded, value);
    }

    public string ImportStatus => IsImporting ? "Import in progress" : "Import is idle";

    public void CancelImport() => _importCancellation?.Cancel();

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

    public IReadOnlyList<string> ExpandedDirectoryPaths() =>
        Flatten(RootNodes).Where(node => node.IsDirectory && node.IsExpanded).Select(node => node.Path).ToArray();

    public void LoadChildren(FileTreeNodeViewModel node)
    {
        if (!node.IsDirectory || node.IsPlaceholder
            || (node.Children.Count > 0 && !node.Children[0].IsPlaceholder))
        {
            return;
        }

        try
        {
            node.Children.Clear();
            DirectoryLevel level = LoadDirectoryLevel(node.Path, node.Level + 1);
            foreach (FileTreeNodeViewModel child in level.Nodes)
            {
                node.Children.Add(child);
            }

            RestoreExpansions(node.Children);
            if (level.Truncated)
            {
                Status = DirectoryOverflowStatus(node.Path);
            }
        }
        catch (VaultException exception)
        {
            ReportFailure($"Could not load {node.Path}: {exception.Message}");
        }
    }

    public void BatchSelectionChanged()
    {
        BatchSelectionCount = Flatten(RootNodes).Count(node => node.IsBatchSelected && node.IsBatchSelectable);
        _announce(BatchSelectionCount == 0
            ? new A11yEvent.NoItemsSelected()
            : new A11yEvent.ItemsSelected((uint)BatchSelectionCount));
    }

    public void ActivateTag(SidebarTagViewModel? tag)
    {
        if (tag is not null)
        {
            FilterText = $"tag:\"{tag.Full}\"";
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

    public void Refresh(bool reportCount = false)
    {
        try
        {
            CancelBulkExpansion();
            _treeGeneration++;
            if (RootNodes.Count > 0)
            {
                string[] liveExpansions = ExpandedDirectoryPaths().ToArray();
                _expandedPaths.Clear();
                _expandedPaths.UnionWith(liveExpansions);
            }

            RootNodes.Clear();
            DirectoryLevel level = LoadDirectoryLevel(string.Empty, 1);
            foreach (FileTreeNodeViewModel node in level.Nodes)
            {
                RootNodes.Add(node);
            }

            RestoreExpansions(RootNodes);
            LoadTags();
            ScheduleFilter();
            if (level.Truncated)
            {
                Status = DirectoryOverflowStatus(string.Empty);
            }
            else if (reportCount || _settingsNotice is not null)
            {
                Status = $"{level.MaterializedCount:N0} top-level items."
                    + (_settingsNotice is null ? string.Empty : $" {_settingsNotice}");
            }
        }
        catch (VaultException exception)
        {
            ReportFailure($"Could not load files: {exception.Message}");
        }
    }

    private DirectoryLevel LoadDirectoryLevel(
        string parentPath,
        int level,
        bool includeDirectories = true,
        DirectoryOrdering? ordering = null)
    {
        ordering ??= CaptureDirectoryOrdering();
        DirListing listing = _session.ListDirChildren(parentPath, new Paging(null, PageLimit));
        FileTreeNodeViewModel[] directories = includeDirectories
            ? listing.Dirs
                .Take(MaxMaterializedDirectoryItems)
                .Select(directory =>
                new FileTreeNodeViewModel(
                    this,
                    directory.Path,
                    directory.Name,
                    isDirectory: true,
                    level,
                    directory.ChildDirCount + directory.ChildFileCount > 0,
                    directory.HasFolderNote)).ToArray()
            : [];
        var files = new List<FileTreeNodeViewModel>();
        string? cursor = null;
        bool truncated = includeDirectories && listing.Dirs.Length > directories.Length;
        do
        {
            int remaining = MaxMaterializedDirectoryItems - directories.Length - files.Count;
            int filesTaken = Math.Min(listing.Files.Items.Length, Math.Max(0, remaining));
            foreach (FileSummary summary in listing.Files.Items.Take(filesTaken))
            {
                files.Add(new FileTreeNodeViewModel(
                    this,
                    summary.Path,
                    summary.Name,
                    isDirectory: false,
                    level,
                    hasChildren: false,
                    summary: summary));
            }

            cursor = listing.Files.NextCursor;
            if (directories.Length + files.Count >= MaxMaterializedDirectoryItems)
            {
                truncated |= filesTaken < listing.Files.Items.Length || cursor is not null;
                break;
            }

            if (cursor is not null)
            {
                listing = _session.ListDirChildren(parentPath, new Paging(cursor, PageLimit));
            }
        }
        while (cursor is not null);

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
            .GroupBy(node => DateBucket(node, today))
            .OrderBy(group => Array.IndexOf(order, group.Key))
            .Select(group => FileTreeNodeViewModel.Group(group.Key, SortNodes(group, ordering)));
    }

    private string DateBucket(FileTreeNodeViewModel node, DateTime today)
    {
        long? milliseconds = SortMode is SidebarSortMode.CreatedNewest or SidebarSortMode.CreatedOldest
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

    private void ScheduleFilter()
    {
        CancelFilterCore();
        int generation = ++_filterGeneration;
        string query = FilterText.Trim();
        if (query.Length == 0)
        {
            FilterResults.Clear();
            _filterCompletion = Task.CompletedTask;
            return;
        }

        if (_filterUiContext is null)
        {
            ApplyFilterOutcome(RunFilterQuery(query, CancellationToken.None));
            _filterCompletion = Task.CompletedTask;
            return;
        }

        var cancellation = new CancellationTokenSource();
        _filterCancellation = cancellation;
        Status = "Filtering files…";
        _filterCompletion = FilterAfterDelayAsync(query, generation, cancellation.Token);
    }

    internal bool CancelFilter()
    {
        bool wasPending = IsFiltering;
        if (_filterCancellation is not null)
        {
            ++_filterGeneration;
            CancelFilterCore();
        }

        return wasPending;
    }

    private void CancelFilterCore()
    {
        CancellationTokenSource? cancellation = _filterCancellation;
        _filterCancellation = null;
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        Task completion = _filterCompletion;
        if (completion.IsCompleted)
        {
            cancellation.Dispose();
            return;
        }

        _ = completion.ContinueWith(
            _ => cancellation.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task FilterAfterDelayAsync(
        string query,
        int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(FilterDebounceMilliseconds, cancellationToken).ConfigureAwait(false);
            FilterOutcome outcome = await Task.Run(
                () => RunFilterQuery(query, cancellationToken),
                cancellationToken).ConfigureAwait(false);
            var applied = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _filterUiContext!.Post(
                _ =>
                {
                    try
                    {
                        if (!cancellationToken.IsCancellationRequested
                            && generation == _filterGeneration
                            && string.Equals(FilterText.Trim(), query, StringComparison.Ordinal))
                        {
                            ApplyFilterOutcome(outcome);
                        }

                        applied.TrySetResult();
                    }
                    catch (Exception exception)
                    {
                        applied.TrySetException(exception);
                    }
                },
                null);
            await applied.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private FilterOutcome RunFilterQuery(string query, CancellationToken cancellationToken)
    {
        try
        {
            string[] requirements = _session.SidebarFilterDateRequirements(query);
            SidebarFilterDateWindow[] windows = BuildDateWindows(requirements);
            var files = new List<FileSummary>();
            string? cursor = null;
            ulong total = 0;
            string audioSummary = string.Empty;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                SidebarFilterPage page = _session.FilterFiles(
                    query,
                    null,
                    null,
                    windows,
                    new Paging(cursor, PageLimit));
                files.AddRange(page.Files.Take((int)PageLimit - files.Count));
                total = page.Total;
                cursor = files.Count >= PageLimit ? null : page.NextCursor;
                audioSummary = page.AudioSummary;
            }
            while (cursor is not null);

            return new FilterOutcome(files, total, audioSummary, null);
        }
        catch (Exception exception) when (exception is VaultException or FormatException or ArgumentOutOfRangeException)
        {
            return new FilterOutcome([], 0, string.Empty, $"Filter not applied: {exception.Message}");
        }
    }

    private void ApplyFilterOutcome(FilterOutcome outcome)
    {
        FilterResults.Clear();
        foreach (FileSummary summary in outcome.Files)
        {
            FilterResults.Add(new FileTreeNodeViewModel(
                this,
                summary.Path,
                summary.Name,
                isDirectory: false,
                level: 1,
                hasChildren: false,
                summary: summary));
        }

        if (outcome.Error is not null)
        {
            ReportFailure(outcome.Error);
        }
        else
        {
            Status = outcome.AudioSummary;
            _announce(new A11yEvent.FileListCount((uint)Math.Min(outcome.Total, uint.MaxValue)));
        }
    }

    private sealed record FilterOutcome(
        IReadOnlyList<FileSummary> Files,
        ulong Total,
        string AudioSummary,
        string? Error);

    internal static SidebarFilterDateWindow[] BuildDateWindows(
        IEnumerable<string> requirements,
        DateTimeOffset? now = null,
        TimeZoneInfo? timeZone = null)
    {
        TimeZoneInfo zone = timeZone ?? TimeZoneInfo.Local;
        DateTimeOffset current = now ?? DateTimeOffset.Now;
        DateTime localNow = TimeZoneInfo.ConvertTime(current, zone).DateTime;
        DateTime today = DateTime.SpecifyKind(localNow.Date, DateTimeKind.Unspecified);
        var windows = new List<SidebarFilterDateWindow>();
        foreach (string requirement in requirements)
        {
            DateTime start = requirement switch
            {
                "@today" => today,
                "@yesterday" => today.AddDays(-1),
                "@last7d" => today.AddDays(-6),
                "@last30d" => today.AddDays(-29),
                _ when requirement.StartsWith('@')
                    && DateTime.TryParseExact(
                        requirement[1..],
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime parsed) => DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified),
                _ => throw new FormatException($"Unsupported date term {requirement}."),
            };
            DateTime end = requirement switch
            {
                "@last7d" or "@last30d" => today.AddDays(1),
                _ => start.AddDays(1),
            };
            windows.Add(new SidebarFilterDateWindow(
                requirement,
                ToUnixMilliseconds(start, zone),
                ToUnixMilliseconds(end, zone)));
        }

        return [.. windows];
    }

    private static long ToUnixMilliseconds(DateTime local, TimeZoneInfo zone)
    {
        DateTime safe = zone.IsInvalidTime(local) ? local.AddHours(1) : local;
        DateTime utc = TimeZoneInfo.ConvertTimeToUtc(safe, zone);
        return new DateTimeOffset(utc).ToUnixTimeMilliseconds();
    }

    private void LoadTags()
    {
        try
        {
            TagTree tree = _session.TagTree();
            Tags.Clear();
            var ancestors = new List<SidebarTagViewModel>();
            foreach (TagTreeEntry entry in tree.Entries)
            {
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
                    Tags.Add(tag);
                }
                else
                {
                    ancestors[^1].Children.Add(tag);
                }

                ancestors.Add(tag);
            }
        }
        catch (VaultException exception)
        {
            ReportFailure($"Could not load tags: {exception.Message}");
        }
    }

    private void EditTag(bool add)
    {
        string[] paths = Flatten(RootNodes)
            .Where(node => node.IsBatchSelected && !node.IsDirectory)
            .Select(node => node.Path)
            .ToArray();
        if (paths.Length == 0 || TagInput.Length == 0)
        {
            return;
        }

        try
        {
            TagEditReport report = add
                ? _session.AddTagToFiles(paths, TagInput)
                : _session.RemoveTagFromFiles(paths, TagInput);
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
        string parent = SelectedNode?.IsDirectory == true ? SelectedNode.Path : ParentPath(SelectedNode?.Path);
        string path = CombineVaultPath(parent, MutationName);
        try
        {
            _session.CreateFolderExclusive(path);
            ReportResult($"Created folder {path}.");
            Refresh();
        }
        catch (VaultException exception)
        {
            ReportFailure($"Could not create folder: {exception.Message}");
        }
    }

    private void CreateNote()
    {
        string parent = SelectedNode?.IsDirectory == true ? SelectedNode.Path : ParentPath(SelectedNode?.Path);
        string name = MutationName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? MutationName
            : $"{MutationName}.md";
        string path = CombineVaultPath(parent, name);
        try
        {
            _session.CreateExclusive(path, string.Empty);
            ReportResult($"Created {path}.");
            Refresh();
            RequestOpen(path);
        }
        catch (VaultException exception)
        {
            ReportFailure($"Could not create note: {exception.Message}");
        }
    }

    private void RenameSelected()
    {
        if (SelectedNode is not FileTreeNodeViewModel node)
        {
            return;
        }

        try
        {
            string oldPath = node.Path;
            string newPath = CombineVaultPath(ParentPath(oldPath), MutationName);
            if (node.IsDirectory)
            {
                if (node.HasFolderNote)
                {
                    _session.RenameFolderWithNote(node.Path, MutationName);
                }
                else
                {
                    _session.RenameFolder(node.Path, MutationName);
                }
            }
            else
            {
                _session.RenameFile(node.Path, MutationName);
            }

            TransformStoredPaths(oldPath, newPath, node.IsDirectory, deleted: false);
            ReportResult($"Renamed {node.DisplayName} to {MutationName}.");
            Refresh();
        }
        catch (VaultException exception)
        {
            ReportFailure($"Rename failed: {exception.Message}");
        }
    }

    private void DeleteSelected()
    {
        if (SelectedNode is not FileTreeNodeViewModel node)
        {
            return;
        }

        if (!_confirmDestructive(
            $"Move {node.DisplayName} to the Recycle Bin? Links to this item may stop resolving."))
        {
            return;
        }

        try
        {
            BatchTrashReport report = _session.BatchTrash(new BatchTrashRequest(
                [new StructuralBatchItem(node.Path, node.IsDirectory)]));
            foreach (StructuralBatchItem trashed in report.Trashed)
            {
                TransformStoredPaths(trashed.Path, trashed.Path, trashed.IsDirectory, deleted: true);
            }

            Status = BatchTrashSummary(report);
            // W0.5-3 residue: Windows single-item system-Recycle-Bin report copy.
            _announce(new A11yEvent.HostComposed(Status, A11yPriority.Medium));
            if (report.Trashed.Any(item => item.Path == node.Path))
            {
                SelectedNode = null;
            }

            Refresh();
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
            _session.CreateExclusive(path, $"# {node.Name}\n");
            ReportResult($"Created folder note {path}.");
            Refresh();
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
            _session.DeleteFile(FolderNotePath(node));
            ReportResult($"Deleted the {node.DisplayName} folder note.");
            Refresh();
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
            string? link = _session.WikilinkForPath(node.Path);
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

    private StructuralBatchItem[] SelectedBatchItems() => Flatten(RootNodes)
        .Where(node => node.IsBatchSelected && node.IsBatchSelectable)
        .Select(node => new StructuralBatchItem(node.Path, node.IsDirectory))
        .ToArray();

    private void BatchMove()
    {
        StructuralBatchItem[] items = SelectedBatchItems();
        if (items.Length == 0)
        {
            return;
        }

        try
        {
            BatchMoveReport report = _session.BatchMove(new BatchMoveRequest(items, MoveDestination));
            foreach (BatchPathChange change in report.Standing)
            {
                TransformStoredPaths(change.OldPath, change.NewPath, change.IsDirectory, deleted: false);
            }

            Status = BatchMoveSummary(report, MoveDestination);
            // W0.5-3 residue: Windows batch-move report copy.
            _announce(new A11yEvent.HostComposed(Status, A11yPriority.Medium));
            Refresh();
        }
        catch (VaultException exception)
        {
            ReportFailure($"Move failed: {exception.Message}");
        }
    }

    private void BatchTrash()
    {
        StructuralBatchItem[] items = SelectedBatchItems();
        if (items.Length == 0
            || !_confirmDestructive(
                $"Move {items.Length:N0} selected {(items.Length == 1 ? "item" : "items")} to the Recycle Bin?"))
        {
            return;
        }

        try
        {
            BatchTrashReport report = _session.BatchTrash(new BatchTrashRequest(items));
            foreach (StructuralBatchItem item in report.Trashed)
            {
                TransformStoredPaths(item.Path, item.Path, item.IsDirectory, deleted: true);
            }

            Status = BatchTrashSummary(report);
            // W0.5-3 residue: Windows system-Recycle-Bin report copy.
            _announce(new A11yEvent.HostComposed(Status, A11yPriority.Medium));
            Refresh();
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

    private async Task ImportAsync()
    {
        IReadOnlyList<string> sources;
        try
        {
            sources = await _pickImportSources();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ReportFailure($"Could not choose import sources: {exception.Message}");
            return;
        }

        if (sources.Count == 0 || _vaultRoot is null)
        {
            return;
        }

        string destination = SelectedNode?.IsDirectory == true
            ? SelectedNode.Path
            : ParentPath(SelectedNode?.Path);
        _importCancellation = new CancellationTokenSource();
        IsImporting = true;
        try
        {
            string[] acceptedSources = sources.Take(256).ToArray();
            int omittedSources = sources.Count - acceptedSources.Length;
            ImportResult result = await Task.Run(
                () => ImportSources(
                    acceptedSources,
                    destination,
                    _importCancellation.Token,
                    omittedSources));
            Status = ImportSummary(result, destination);
            // W0.5-3 residue: Windows import-engine completion copy.
            _announce(new A11yEvent.HostComposed(Status, A11yPriority.Medium));
            Refresh();
        }
        finally
        {
            _importCancellation.Dispose();
            _importCancellation = null;
            IsImporting = false;
        }
    }

    private ImportResult ImportSources(
        IEnumerable<string> sources,
        string destination,
        CancellationToken cancellationToken,
        int initialFailures = 0)
    {
        int importedFiles = 0;
        int importedFolders = 0;
        int failed = initialFailures;
        int visitedEntries = 0;
        bool limitReached = false;
        foreach (string source in sources)
        {
            if (!TryReserveImportEntry(ref visitedEntries))
            {
                limitReached = true;
                break;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string absolute = Path.GetFullPath(source);
                if (IsInsideVault(absolute)
                    || HasReparsePointInPath(absolute))
                {
                    failed++;
                    continue;
                }

                if (File.Exists(absolute))
                {
                    ImportFile(absolute, destination, cancellationToken);
                    importedFiles++;
                }
                else if (Directory.Exists(absolute))
                {
                    string importedRoot = CreateUniqueFolder(
                        destination,
                        new DirectoryInfo(absolute).Name);
                    importedFolders++;
                    var pending = new Stack<(string Source, string Destination)>();
                    pending.Push((absolute, importedRoot));
                    while (pending.Count > 0 && !limitReached)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        (string sourceDirectory, string destinationDirectory) = pending.Pop();
                        foreach (string child in Directory.EnumerateFileSystemEntries(sourceDirectory))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!TryReserveImportEntry(ref visitedEntries))
                            {
                                limitReached = true;
                                pending.Clear();
                                break;
                            }

                            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                            {
                                failed++;
                                continue;
                            }

                            if (Directory.Exists(child))
                            {
                                string childDestination = CombineVaultPath(
                                    destinationDirectory,
                                    Path.GetFileName(child));
                                _session.CreateFolderExclusive(childDestination);
                                pending.Push((child, childDestination));
                                importedFolders++;
                            }
                            else
                            {
                                ImportFile(child, destinationDirectory, cancellationToken);
                                importedFiles++;
                            }
                        }
                    }
                }
                else
                {
                    failed++;
                }
            }
            catch (OperationCanceledException)
            {
                return new ImportResult(
                    importedFiles,
                    importedFolders,
                    failed,
                    Cancelled: true,
                    limitReached);
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or ArgumentException
                    or NotSupportedException
                    or System.Security.SecurityException
                    or VaultException)
            {
                failed++;
            }
        }

        return new ImportResult(
            importedFiles,
            importedFolders,
            failed,
            Cancelled: false,
            limitReached);
    }

    private void ImportFile(string source, string destination, CancellationToken cancellationToken)
    {
        byte[] bytes = SafeFile.ReadAllBytesBounded(
            source,
            MaxImportFileBytes,
            cancellationToken: cancellationToken);
        string originalName = Path.GetFileName(source);
        for (int suffix = 1; suffix <= 100; suffix++)
        {
            string name = suffix == 1 ? originalName : CopyName(originalName, suffix);
            try
            {
                _session.CreateExclusiveBytes(CombineVaultPath(destination, name), bytes);
                return;
            }
            catch (VaultException.DestinationExists) when (suffix < 100)
            {
            }
        }
    }

    private string CreateUniqueFolder(string destination, string originalName)
    {
        for (int suffix = 1; suffix <= 100; suffix++)
        {
            string name = suffix == 1 ? originalName : $"{originalName} {suffix}";
            string path = CombineVaultPath(destination, name);
            try
            {
                _session.CreateFolderExclusive(path);
                return path;
            }
            catch (VaultException.DestinationExists) when (suffix < 100)
            {
            }
        }

        throw new IOException($"Could not reserve a name for {originalName}.");
    }

    private bool IsInsideVault(string absolutePath)
    {
        string root = Path.GetFullPath(_vaultRoot!).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(absolutePath);
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryReserveImportEntry(ref int visitedEntries)
    {
        if (visitedEntries >= MaxImportEntries)
        {
            return false;
        }

        visitedEntries++;
        return true;
    }

    internal static bool HasReparsePointInPath(
        string absolutePath,
        Func<string, FileAttributes>? getAttributes = null)
    {
        getAttributes ??= File.GetAttributes;
        string current = Path.GetFullPath(absolutePath);
        while (true)
        {
            if ((getAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }

            string? parent = Path.GetDirectoryName(current.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(parent)
                || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            current = parent;
        }
    }

    private static string CopyName(string name, int suffix)
    {
        string extension = Path.GetExtension(name);
        string stem = Path.GetFileNameWithoutExtension(name);
        return $"{stem} {suffix}{extension}";
    }

    private static string ImportSummary(ImportResult result, string destination)
    {
        var copied = new List<string>();
        if (result.ImportedFiles > 0)
        {
            copied.Add($"{result.ImportedFiles:N0} {(result.ImportedFiles == 1 ? "file" : "files")}");
        }

        if (result.ImportedFolders > 0)
        {
            copied.Add($"{result.ImportedFolders:N0} {(result.ImportedFolders == 1 ? "folder" : "folders")}");
        }

        string location = string.IsNullOrEmpty(destination)
            ? "the vault root"
            : Path.GetFileName(destination.TrimEnd('/'));
        string message = copied.Count == 0
            ? $"No items were imported to {location}."
            : $"Copied {string.Join(" and ", copied)} to {location}.";
        if (result.Cancelled)
        {
            message += " Import cancelled; completed copies remain.";
        }
        else if (result.Failed > 0)
        {
            message += $" {result.Failed:N0} "
                + (result.Failed == 1 ? "item was" : "items were")
                + " not imported.";
        }

        if (result.LimitReached)
        {
            message += " Import stopped at the 10,000-item safety limit; remaining entries were not enumerated.";
        }

        return message;
    }

    private sealed record ImportResult(
        int ImportedFiles,
        int ImportedFolders,
        int Failed,
        bool Cancelled,
        bool LimitReached);

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
        DualPaneFiles.Clear();
        if (!IsDualPaneEnabled)
        {
            return;
        }

        try
        {
            DirectoryLevel level = LoadDirectoryLevel(
                parentPath,
                1,
                includeDirectories: false);
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

    private void CollapseAll()
    {
        CancelBulkExpansion();
        foreach (FileTreeNodeViewModel node in Flatten(RootNodes).Where(node => node.IsDirectory))
        {
            node.IsExpanded = false;
        }
    }

    private async Task ExpandLoadedAsync()
    {
        CancelBulkExpansion();
        using var cancellation = new CancellationTokenSource();
        _bulkExpandCancellation = cancellation;
        int generation = _treeGeneration;
        DirectoryOrdering ordering = CaptureDirectoryOrdering();
        FileTreeNodeViewModel[] materializedDirectories = Flatten(RootNodes)
            .Where(node => node.IsDirectory)
            .ToArray();
        IsExpandingLoaded = true;
        try
        {
            foreach (FileTreeNodeViewModel node in materializedDirectories)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (generation != _treeGeneration)
                {
                    return;
                }

                node.MarkExpandedWithoutLoading();
                if (node.IsPlaceholder
                    || (node.Children.Count > 0 && !node.Children[0].IsPlaceholder))
                {
                    await Task.Yield();
                    continue;
                }

                DirectoryLevel level = await Task.Run(
                    () => LoadDirectoryLevel(
                        node.Path,
                        node.Level + 1,
                        ordering: ordering),
                    cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (generation != _treeGeneration || !node.IsExpanded)
                {
                    return;
                }

                // Expand Loaded is deliberately snapshot-based: children that
                // materialize here are not recursively expanded in this run.
                node.ReplaceChildren(level.Nodes);
                if (level.Truncated)
                {
                    Status = DirectoryOverflowStatus(node.Path);
                }

                await Task.Yield();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (VaultException exception)
        {
            ReportFailure($"Could not expand loaded folders: {exception.Message}");
        }
        finally
        {
            IsExpandingLoaded = false;
            if (ReferenceEquals(_bulkExpandCancellation, cancellation))
            {
                _bulkExpandCancellation = null;
            }
        }
    }

    private void CancelBulkExpansion()
    {
        _bulkExpandCancellation?.Cancel();
        _bulkExpandCancellation = null;
    }

    public void CancelExpandLoaded() => CancelBulkExpansion();

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
        // W0.5-3 residue: Windows sidebar availability/error copy.
        _announce(new A11yEvent.HostComposed(message, A11yPriority.High));
    }

    private void ReportResult(string message)
    {
        Status = message;
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

        _historyIndex = Math.Clamp(_historyIndex, -1, _history.Count - 1);
        _ = PersistPins();
        _ = PersistShortcuts();
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
