// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Document;
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

internal sealed class WorkspaceTabViewModel : BindableBase, IDisposable
{
    private readonly VaultSession _session;
    private readonly Action<WorkspaceTabViewModel, EditorDocumentSyncEvent?>? _documentChanged;
    private AvalonDocumentBufferSession? _editorSession;
    private string _text = string.Empty;
    private string? _contentHash;
    private bool _isDirty;
    private bool _isMissingFromDisk;
    private string _status = string.Empty;

    public WorkspaceTabViewModel(
        VaultSession session,
        WorkspaceTabState state,
        Action<WorkspaceTabViewModel, EditorDocumentSyncEvent?>? documentChanged = null)
    {
        _session = session;
        _documentChanged = documentChanged;
        Id = state.Id;
        Item = state.Item;
        Mode = state.Mode;
        PropsCollapsed = state.PropsCollapsed;
        ActiveCanvasSurface = state.ActiveCanvasSurface;
        Load();
        InitializeEditorSession();
    }

    public Guid Id { get; }
    public WorkspaceItemState Item { get; private set; }
    public string? Mode { get; }
    public bool? PropsCollapsed { get; }
    public string? ActiveCanvasSurface { get; }
    public string Title => Item.Title;
    public TextDocument? EditorDocument => _editorSession?.Document;
    internal AvalonDocumentBufferSession? EditorSession => _editorSession;
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
        get => _editorSession?.Document.Text ?? _text;
        set
        {
            if (_editorSession is null)
            {
                ApplyEditorText(value);
                return;
            }

            _editorSession.ReplaceAll(value);
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
        _editorSession?.Dispose();
        _editorSession = null;
        Item = item;
        _text = string.Empty;
        _contentHash = null;
        _isDirty = false;
        _status = string.Empty;
        _isMissingFromDisk = false;
        NotifyItemChanged();
        Load();
        InitializeEditorSession();
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(EditorDocument));
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
        _documentChanged?.Invoke(this, null);
    }

    public void MirrorDocumentStateFrom(
        WorkspaceTabViewModel source,
        bool reconstructUndoHistory = true)
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
        AvalonDocumentBufferSession? sourceSession = source._editorSession;
        if (_editorSession is not null && sourceSession is not null)
        {
            _editorSession.SynchronizeFromPeer(
                source.Text,
                sourceSession.SavedBaseline,
                reconstructUndoHistory);
        }
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(DirtyMarker));
        OnPropertyChanged(nameof(IsMissingFromDisk));
        OnPropertyChanged(nameof(Status));
    }

    public void ApplyPeerDocumentEvent(
        WorkspaceTabViewModel source,
        EditorDocumentSyncEvent syncEvent)
    {
        if (!IsMarkdown || !source.IsMarkdown || ReferenceEquals(this, source))
        {
            return;
        }

        AvalonDocumentBufferSession session = _editorSession
            ?? throw new InvalidOperationException("A Markdown tab has no editor session.");
        switch (syncEvent)
        {
            case EditorDocumentUpdateStarted:
                session.BeginPeerUpdate();
                break;
            case EditorDocumentChange change:
                session.ApplyPeerEdit(change);
                OnPropertyChanged(nameof(Text));
                break;
            case EditorDocumentUpdateFinished:
                session.EndPeerUpdate();
                _contentHash = source._contentHash;
                _isDirty = source._isDirty;
                _isMissingFromDisk = source._isMissingFromDisk;
                _status = source._status;
                if (!_isDirty)
                {
                    AvalonDocumentBufferSession sourceSession = source._editorSession
                        ?? throw new InvalidOperationException("A Markdown source tab has no editor session.");
                    session.MarkSaved(sourceSession.SavedBaseline);
                }

                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(DirtyMarker));
                OnPropertyChanged(nameof(IsMissingFromDisk));
                OnPropertyChanged(nameof(Status));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(syncEvent));
        }
    }

    public bool Save()
    {
        if (!IsMarkdown || !IsDirty)
        {
            return true;
        }

        string saveText;
        try
        {
            EditorSaveSnapshot? snapshot = _editorSession?.PrepareSaveSnapshot();
            saveText = snapshot?.Text ?? Text;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Status = $"Save blocked by editor integrity check: {exception.Message}";
            _documentChanged?.Invoke(this, null);
            return false;
        }

        try
        {
            SaveReport report = _session.SaveText(Path, saveText, _contentHash);
            _contentHash = report.NewContentHash;
            _text = saveText;
            _editorSession?.MarkSaved(saveText);
            IsDirty = false;
            Status = $"Saved {System.IO.Path.GetFileName(Path)}.";
            _documentChanged?.Invoke(this, null);
            return true;
        }
        catch (VaultException exception)
        {
            Status = $"Save blocked: {exception.Message}";
            _documentChanged?.Invoke(this, null);
            return false;
        }
    }

    public void Dispose()
    {
        _editorSession?.Dispose();
        _editorSession = null;
    }

    private void InitializeEditorSession()
    {
        if (IsMarkdown)
        {
            _editorSession = new AvalonDocumentBufferSession(_text, ApplyEditorSyncEvent);
        }
    }

    private void ApplyEditorSyncEvent(EditorDocumentSyncEvent syncEvent)
    {
        if (syncEvent is EditorDocumentChange)
        {
            OnPropertyChanged(nameof(Text));
        }
        else if (syncEvent is EditorDocumentUpdateFinished)
        {
            AvalonDocumentBufferSession session = _editorSession
                ?? throw new InvalidOperationException("A Markdown tab has no editor session.");
            IsDirty = !session.IsAtSavedBaseline;
        }

        _documentChanged?.Invoke(this, syncEvent);
    }

    private void ApplyEditorText(string text)
    {
        if (SetField(ref _text, text, nameof(Text)))
        {
            IsDirty = true;
            _documentChanged?.Invoke(this, null);
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
internal sealed partial class WorkspaceViewModel : BindableBase, IDisposable
{
    private readonly VaultSession _session;
    private readonly Action<A11yEvent> _announce;
    private readonly Func<WorkspaceTabViewModel, WorkspaceItemState, WorkspaceDirtyNavigationDecision>
        _dirtyNavigationDecision;
    private readonly Func<WorkspaceTabViewModel, WorkspaceDirtyNavigationDecision>
        _dirtyCloseDecision;
    private WorkspaceLeafOption _activeLeaf;
    private bool _isRightPaneVisible = true;

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

    public void Dispose()
    {
        Persist();
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            tab.Dispose();
        }
    }

    private void SaveActive()
    {
        if (ActiveGroup.ActiveTab is WorkspaceTabViewModel tab && tab.Save())
        {
            _announce(new A11yEvent.NoteSaved(System.IO.Path.GetFileName(tab.Path)));
        }
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

    private void MirrorSamePathDocumentState(
        WorkspaceTabViewModel source,
        EditorDocumentSyncEvent? syncEvent)
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
                if (syncEvent is null)
                {
                    peer.MirrorDocumentStateFrom(source, reconstructUndoHistory: false);
                }
                else
                {
                    peer.ApplyPeerDocumentEvent(source, syncEvent);
                }
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

}
