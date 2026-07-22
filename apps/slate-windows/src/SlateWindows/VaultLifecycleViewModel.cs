// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows;

internal enum VaultCloseDecision
{
    SaveAll,
    Discard,
    Cancel,
}

/// <summary>
/// Owns the Windows vault lifecycle. The active FFI session remains alive for
/// the complete open-vault state; callbacks only enqueue work for the UI
/// thread and never synchronously re-enter the session.
/// </summary>
internal sealed class VaultLifecycleViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly Func<Task<string?>> _pickVault;
    private readonly Func<RecentVault, Task<bool>> _confirmRemoveMissingRecent;
    private readonly Action<Action> _enqueueUi;
    private readonly Action<A11yEvent> _announce;
    private readonly Action<string> _copyText;
    private readonly Func<VaultCloseDecision> _confirmUnsavedClose;
    private readonly Func<WorkspaceTabViewModel, WorkspaceItemState, WorkspaceDirtyNavigationDecision>
        _confirmDirtyNavigation;
    private readonly Func<WorkspaceTabViewModel, WorkspaceDirtyNavigationDecision>
        _confirmDirtyClose;
    private readonly Func<string, bool> _confirmDestructive;
    private readonly Func<Task<IReadOnlyList<string>>> _pickImportSources;
    private readonly RecentVaultsStore _recentVaultsStore;
    private readonly ScanAnnouncementGate _scanAnnouncements;
    private readonly SynchronizationContext? _filterUiContext;
    private readonly SynchronizationContext? _treeUiContext;
    private readonly Func<Action, CancellationToken, Task>? _treeWorker;
    private readonly AsyncRelayCommand _openVaultCommand;
    private readonly AsyncRelayCommand _openRecentCommand;
    private readonly RelayCommand _closeVaultCommand;

    private VaultSession? _session;
    private CancelToken? _scanCancel;
    private ulong? _eventListenerToken;
    private UiProgressListener? _progressListener;
    private UiVaultEventListener? _eventListener;
    private int _generation;
    private bool _isVaultOpen;
    private bool _isBusy;
    private string _vaultDisplayName = string.Empty;
    private string _vaultPath = string.Empty;
    private string _statusText = "No vault open.";
    private double _progressMaximum = 1;
    private double _progressValue;
    private bool _isProgressIndeterminate;
    private FilesSidebarViewModel? _fileSidebar;
    private WorkspaceViewModel? _workspace;
    private QuickSwitcherViewModel? _quickSwitcher;
    private int _sidebarRefreshTicket;

    public VaultLifecycleViewModel(
        Func<Task<string?>> pickVault,
        Action<Action> enqueueUi,
        Func<RecentVault, Task<bool>>? confirmRemoveMissingRecent = null,
        RecentVaultsStore? recentVaultsStore = null,
        Action<A11yEvent>? announce = null,
        Action<string>? copyText = null,
        Func<VaultCloseDecision>? confirmUnsavedClose = null,
        Func<WorkspaceTabViewModel, WorkspaceItemState, WorkspaceDirtyNavigationDecision>?
            confirmDirtyNavigation = null,
        Func<WorkspaceTabViewModel, WorkspaceDirtyNavigationDecision>? confirmDirtyClose = null,
        Func<string, bool>? confirmDestructive = null,
        Func<Task<IReadOnlyList<string>>>? pickImportSources = null,
        Func<DateTimeOffset>? scanClock = null,
        SynchronizationContext? filterUiContext = null,
        SynchronizationContext? treeUiContext = null,
        Func<Action, CancellationToken, Task>? treeWorker = null)
    {
        _pickVault = pickVault;
        _enqueueUi = enqueueUi;
        _announce = announce ?? (_ => { });
        _copyText = copyText ?? (_ => { });
        _confirmUnsavedClose = confirmUnsavedClose ?? (() => VaultCloseDecision.Cancel);
        _confirmDirtyNavigation = confirmDirtyNavigation
            ?? ((_, _) => WorkspaceDirtyNavigationDecision.Cancel);
        _confirmDirtyClose = confirmDirtyClose
            ?? (_ => WorkspaceDirtyNavigationDecision.Cancel);
        _confirmDestructive = confirmDestructive ?? (_ => true);
        _pickImportSources = pickImportSources ?? (() => Task.FromResult<IReadOnlyList<string>>([]));
        _confirmRemoveMissingRecent = confirmRemoveMissingRecent
            ?? (_ => Task.FromResult(false));
        _recentVaultsStore = recentVaultsStore ?? new RecentVaultsStore();
        _scanAnnouncements = new ScanAnnouncementGate(scanClock);
        _filterUiContext = filterUiContext;
        SynchronizationContext? currentUiContext = SynchronizationContext.Current;
        _treeUiContext = treeUiContext
            ?? (currentUiContext is DispatcherSynchronizationContext ? currentUiContext : null);
        _treeWorker = treeWorker;
        _openVaultCommand = new AsyncRelayCommand(PickAndOpenVaultAsync, () => !IsBusy);
        _openRecentCommand = new AsyncRelayCommand(
            OpenRecentAsync,
            parameter => parameter is RecentVault && !IsBusy);
        _closeVaultCommand = new RelayCommand(
            _ => CloseVault(),
            _ => IsVaultOpen && !IsBusy);
        ReloadRecentVaults();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? RecentVaultsChanged;
    public event EventHandler? ReturnedToWelcome;
    public event EventHandler? WorkspaceReady;
    public event EventHandler? QuickSwitcherDismissed;
    public event EventHandler<WorkspaceFocusBoundary>? WorkspaceFocusBoundaryRequested;

    public ObservableCollection<RecentVault> RecentVaults { get; } = [];
    public ICommand OpenVaultCommand => _openVaultCommand;
    public ICommand OpenRecentCommand => _openRecentCommand;
    public ICommand CloseVaultCommand => _closeVaultCommand;

    public bool IsVaultOpen
    {
        get => _isVaultOpen;
        private set
        {
            if (SetField(ref _isVaultOpen, value))
            {
                OnPropertyChanged(nameof(IsWelcomeVisible));
                OnPropertyChanged(nameof(IsWorkspaceVisible));
                RaiseCommandStates();
            }
        }
    }

    public bool IsWelcomeVisible => !IsVaultOpen;
    public bool IsWorkspaceVisible => IsVaultOpen;
    public bool HasRecentVaults => RecentVaults.Count > 0;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string VaultDisplayName
    {
        get => _vaultDisplayName;
        private set => SetField(ref _vaultDisplayName, value);
    }

    public string VaultPath
    {
        get => _vaultPath;
        private set => SetField(ref _vaultPath, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public double ProgressMaximum
    {
        get => _progressMaximum;
        private set => SetField(ref _progressMaximum, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetField(ref _progressValue, value);
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => SetField(ref _isProgressIndeterminate, value);
    }

    public FilesSidebarViewModel? FileSidebar
    {
        get => _fileSidebar;
        private set => SetField(ref _fileSidebar, value);
    }

    public WorkspaceViewModel? Workspace
    {
        get => _workspace;
        private set => SetField(ref _workspace, value);
    }

    public QuickSwitcherViewModel? QuickSwitcher
    {
        get => _quickSwitcher;
        private set => SetField(ref _quickSwitcher, value);
    }

    public async Task OpenVaultAsync(string path)
    {
        if (IsBusy)
        {
            return;
        }

        string root;
        try
        {
            root = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            ReportTerminalStatus($"Could not open vault: {exception.Message}", A11yPriority.High);
            return;
        }

        if (!TryCloseWorkspace())
        {
            return;
        }

        CloseSession();
        int generation = ++_generation;
        IsBusy = true;
        IsProgressIndeterminate = true;
        ProgressValue = 0;
        StatusText = $"Opening {root}…";

        VaultSession? openedSession = null;
        try
        {
            openedSession = await Task.Run(() => VaultSession.OpenFilesystem(root));
            if (generation != _generation)
            {
                openedSession.Dispose();
                return;
            }

            _session = openedSession;
            openedSession = null;
            _eventListener = new UiVaultEventListener(
                (code, eventPath, message) => _enqueueUi(
                    () => HandleVaultError(generation, code, eventPath, message)),
                @event => _enqueueUi(() => HandleFileChange(generation, @event)),
                (_, _) => { });
            _eventListenerToken = _session.RegisterEventListener(_eventListener);

            IsVaultOpen = true;
            VaultPath = root;
            VaultDisplayName = RecentVault.FromPath(root).DisplayName;
            AddRecentVault(root);

            _scanCancel = new CancelToken();
            _progressListener = new UiProgressListener(
                _enqueueUi,
                @event => HandleProgress(generation, @event));
            VaultSession activeSession = _session;
            CancelToken activeCancel = _scanCancel;
            UiProgressListener activeProgressListener = _progressListener;
            (ScanReport Report, SwitcherFile[] SwitcherFiles) loaded = await Task.Run(() =>
            {
                ScanReport report = activeSession.ScanInitialWithProgress(activeCancel, activeProgressListener);
                return (report, LoadSwitcherFiles(activeSession));
            });
            if (generation == _generation)
            {
                StatusText = $"Scan finished: {loaded.Report.FilesIndexed} files indexed.";
                ProgressValue = ProgressMaximum;
                IsProgressIndeterminate = false;
                InitializeWorkspace(_session, root, loaded.SwitcherFiles);
            }
        }
        catch (VaultException exception)
        {
            if (generation == _generation)
            {
                ReportTerminalStatus($"Could not open vault: {exception.Message}", A11yPriority.High);
                CloseSession();
                IsVaultOpen = false;
                ReturnedToWelcome?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception exception)
        {
            if (generation == _generation)
            {
                ReportTerminalStatus($"Unexpected vault error: {exception.Message}", A11yPriority.High);
                CloseSession();
                IsVaultOpen = false;
                ReturnedToWelcome?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            openedSession?.Dispose();
            if (generation == _generation)
            {
                _scanCancel?.Dispose();
                _scanCancel = null;
                _progressListener = null;
                IsBusy = false;
                IsProgressIndeterminate = false;
            }
        }
    }

    public void CloseVault()
    {
        if (IsBusy)
        {
            return;
        }

        bool hadDirtyTabs = Workspace?.HasDirtyTabs == true;
        if (!TryCloseWorkspace())
        {
            return;
        }

        ++_generation;
        CloseSession();
        IsVaultOpen = false;
        VaultDisplayName = string.Empty;
        VaultPath = string.Empty;
        ProgressValue = 0;
        ProgressMaximum = 1;
        IsProgressIndeterminate = false;
        StatusText = "Vault closed.";
        if (!hadDirtyTabs)
        {
            _announce(new A11yEvent.VaultClosed());
        }

        ReturnedToWelcome?.Invoke(this, EventArgs.Empty);
    }

    public bool PrepareForApplicationClose()
    {
        if (IsBusy)
        {
            return false;
        }

        return TryCloseWorkspace();
    }

    public void Dispose()
    {
        ++_generation;
        _scanCancel?.Cancel();
        CloseSession();
    }

    private async Task PickAndOpenVaultAsync(object? _)
    {
        string? path = await _pickVault();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await OpenVaultAsync(path);
        }
    }

    private async Task OpenRecentAsync(object? parameter)
    {
        if (parameter is RecentVault recent)
        {
            if (!Directory.Exists(recent.Path))
            {
                bool remove = await _confirmRemoveMissingRecent(recent);
                if (remove)
                {
                    RemoveRecentVault(recent);
                }
                else
                {
                    ReportTerminalStatus($"Vault not found: {recent.Path}", A11yPriority.High);
                }

                return;
            }

            await OpenVaultAsync(recent.Path);
        }
    }

    private void HandleProgress(int generation, ScanProgress @event)
    {
        if (generation != _generation)
        {
            return;
        }

        switch (@event)
        {
            case ScanProgress.Started started:
                ProgressMaximum = Math.Max(1, started.TotalFiles);
                ProgressValue = 0;
                IsProgressIndeterminate = started.TotalFiles == 0;
                StatusText = $"Scanning {started.TotalFiles} files…";
                _announce(_scanAnnouncements.Started(started.TotalFiles));
                break;
            case ScanProgress.FileIndexed indexed:
                ProgressMaximum = Math.Max(1, indexed.Total);
                ProgressValue = Math.Min(indexed.Indexed, ProgressMaximum);
                IsProgressIndeterminate = false;
                StatusText = $"Indexed {indexed.Indexed} of {indexed.Total}: {indexed.Path}";
                A11yEvent? progressAnnouncement = _scanAnnouncements.FileIndexed(
                    indexed.Indexed,
                    indexed.Total);
                if (progressAnnouncement is not null)
                {
                    _announce(progressAnnouncement);
                }

                break;
            case ScanProgress.Finished finished:
                ProgressValue = ProgressMaximum;
                IsProgressIndeterminate = false;
                StatusText = $"Scan finished: {finished.Report.FilesIndexed} files indexed.";
                _announce(_scanAnnouncements.Finished(finished.Report.FilesIndexed));
                break;
            case ScanProgress.Cancelled:
                IsProgressIndeterminate = false;
                ReportTerminalStatus("Scan cancelled.", A11yPriority.Medium);
                _scanAnnouncements.Reset();
                break;
            case ScanProgress.Failed failed:
                IsProgressIndeterminate = false;
                ReportTerminalStatus($"Scan failed: {failed.Message}", A11yPriority.High);
                _scanAnnouncements.Reset();
                break;
        }
    }

    private void HandleVaultError(
        int generation,
        EventErrorCode code,
        string path,
        string message)
    {
        HostLog.Write(HostDiagnosticEvent.VaultEventFailed);
        if (generation == _generation)
        {
            ReportTerminalStatus(message, A11yPriority.High);
        }
    }

    private void HandleFileChange(int generation, FileChangeEvent @event)
    {
        if (generation == _generation)
        {
            if (@event.Kind == FileChangeKind.Renamed
                && @event.PreviousPath is string previousPath)
            {
                Workspace?.RetargetPath(previousPath, @event.Path);
            }
            else if (@event.Kind == FileChangeKind.Deleted)
            {
                Workspace?.InvalidatePath(@event.Path);
            }

            QuickSwitcher?.ApplyFileChange(@event);
            int ticket = Interlocked.Increment(ref _sidebarRefreshTicket);
            _ = Task.Delay(150).ContinueWith(
                _ => _enqueueUi(() =>
                {
                    if (generation == _generation && ticket == _sidebarRefreshTicket)
                    {
                        FileSidebar?.Refresh();
                    }
                }),
                TaskScheduler.Default);
        }
    }

    private void AddRecentVault(string root)
    {
        try
        {
            IReadOnlyList<RecentVault> entries = _recentVaultsStore.Add(RecentVault.FromPath(root));
            ReplaceRecentVaults(entries);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            string message = $"Could not save recent vaults: {exception.Message}";
            HostLog.Write(HostDiagnosticEvent.RecentVaultsPersistFailed, exception);
            ReportTerminalStatus(message, A11yPriority.High);
        }
    }

    private void ReloadRecentVaults()
    {
        ReplaceRecentVaults(_recentVaultsStore.Load());
    }

    private void RemoveRecentVault(RecentVault recent)
    {
        try
        {
            ReplaceRecentVaults(_recentVaultsStore.Remove(recent.Path));
            ReportTerminalStatus(
                $"Removed {recent.DisplayName} from recent vaults.",
                A11yPriority.Medium);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ReportTerminalStatus(
                $"Could not update recent vaults: {exception.Message}",
                A11yPriority.High);
        }
    }

    private void ReplaceRecentVaults(IEnumerable<RecentVault> entries)
    {
        RecentVaults.Clear();
        foreach (RecentVault entry in entries)
        {
            RecentVaults.Add(entry);
        }

        OnPropertyChanged(nameof(HasRecentVaults));
        RecentVaultsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CloseSession()
    {
        if (FileSidebar is FilesSidebarViewModel sidebar)
        {
            sidebar.CancelTreeRefresh();
            try
            {
                sidebar.TreeRefreshCompletion.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                HostLog.Write(HostDiagnosticEvent.SidebarTreeRefreshShutdownFailed, exception);
            }

            sidebar.CancelFilter();
            try
            {
                sidebar.FilterCompletion.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                // A faulted task is terminal, so the session can now be
                // released without racing live FFI work.
                HostLog.Write(HostDiagnosticEvent.SidebarFilterShutdownFailed, exception);
            }
        }

        // TryCloseWorkspace normally supplies the asynchronous barrier. This
        // cancellation is the fail-safe for direct disposal/error teardown so
        // no queued bulk-expansion page begins after session shutdown starts.
        FileSidebar?.CancelExpandLoaded();
        _scanAnnouncements.Reset();
        _scanCancel?.Cancel();
        _scanCancel?.Dispose();
        _scanCancel = null;
        _progressListener = null;

        if (QuickSwitcher is not null)
        {
            QuickSwitcher.OpenRequested -= QuickSwitcher_OpenRequested;
            QuickSwitcher.Dismissed -= QuickSwitcher_Dismissed;
            QuickSwitcher.Dispose();
        }

        if (FileSidebar is not null)
        {
            FileSidebar.OpenTargetRequested -= FileSidebar_OpenTargetRequested;
        }

        if (Workspace is not null)
        {
            Workspace.FileOpened -= Workspace_FileOpened;
            Workspace.FocusBoundaryRequested -= Workspace_FocusBoundaryRequested;
            Workspace.Dispose();
        }

        QuickSwitcher = null;
        FileSidebar = null;
        Workspace = null;

        if (_session is not null && _eventListenerToken is ulong token)
        {
            try
            {
                _session.UnregisterEventListener(token);
            }
            catch (Exception exception)
            {
                HostLog.Write(HostDiagnosticEvent.VaultListenerUnregisterFailed, exception);
            }
        }

        _eventListenerToken = null;
        _eventListener = null;
        _session?.Dispose();
        _session = null;
    }

    private void InitializeWorkspace(
        VaultSession session,
        string root,
        IEnumerable<SwitcherFile> switcherFiles)
    {
        WorkspaceSnapshot? persisted = new WorkspacePersistence(root).Load();
        FilesSidebarViewModel? sidebar = null;
        var workspace = new WorkspaceViewModel(
            session,
            root,
            () => sidebar?.ExpandedDirectoryPaths() ?? [],
            _announce,
            _confirmDirtyNavigation,
            _confirmDirtyClose);
        sidebar = new FilesSidebarViewModel(
            session,
            _announce,
            _copyText,
            persisted?.ExpandedDirPaths,
            root,
            _confirmDestructive,
            _pickImportSources,
            filterUiContext: _filterUiContext,
            treeUiContext: _treeUiContext,
            treeWorker: _treeWorker);
        var switcher = new QuickSwitcherViewModel(session, root, _announce, switcherFiles);

        workspace.FileOpened += Workspace_FileOpened;
        workspace.FocusBoundaryRequested += Workspace_FocusBoundaryRequested;
        sidebar.OpenTargetRequested += FileSidebar_OpenTargetRequested;
        switcher.OpenRequested += QuickSwitcher_OpenRequested;
        switcher.Dismissed += QuickSwitcher_Dismissed;

        Workspace = workspace;
        FileSidebar = sidebar;
        QuickSwitcher = switcher;
        WorkspaceReady?.Invoke(this, EventArgs.Empty);
    }

    private bool TryCloseWorkspace()
    {
        if (FileSidebar?.CancelTreeRefresh() == true)
        {
            ReportTerminalStatus(
                "File tree refresh cancellation requested. Close the vault again after the current directory read finishes.",
                A11yPriority.Medium);
            return false;
        }

        if (FileSidebar?.IsExpandingLoaded == true)
        {
            FileSidebar.CancelExpandLoaded();
            ReportTerminalStatus(
                "Folder expansion cancellation requested. Close the vault again after the current directory read finishes.",
                A11yPriority.Medium);
            return false;
        }

        if (FileSidebar?.IsImporting == true)
        {
            FileSidebar.CancelImport();
            ReportTerminalStatus(
                "Import cancellation requested. Close the vault again after completed copies finish reconciling.",
                A11yPriority.Medium);
            return false;
        }

        if (FileSidebar?.CancelFilter() == true)
        {
            ReportTerminalStatus(
                "File filter cancellation requested. Close the vault again after the current query finishes.",
                A11yPriority.Medium);
            return false;
        }

        if (Workspace?.HasDirtyTabs != true)
        {
            return true;
        }

        VaultCloseDecision decision = _confirmUnsavedClose();
        if (decision == VaultCloseDecision.Cancel)
        {
            return false;
        }

        if (decision == VaultCloseDecision.SaveAll)
        {
            if (!Workspace.SaveAll())
            {
                ReportTerminalStatus(
                    "Vault remains open because one or more notes could not be saved.",
                    A11yPriority.High);
                return false;
            }

            _announce(new A11yEvent.VaultClosedAllSaved());
        }
        else
        {
            _announce(new A11yEvent.VaultClosedChangesDiscarded());
        }

        return true;
    }

    private void FileSidebar_OpenTargetRequested(
        object? sender,
        (string Path, WorkspaceOpenTarget Target) request)
    {
        Workspace?.OpenPath(request.Path, request.Target);
    }

    private void Workspace_FileOpened(object? sender, string path)
    {
        QuickSwitcher?.RecordOpen(path);
    }

    private void Workspace_FocusBoundaryRequested(
        object? sender,
        WorkspaceFocusBoundary boundary) =>
        WorkspaceFocusBoundaryRequested?.Invoke(this, boundary);

    private static SwitcherFile[] LoadSwitcherFiles(VaultSession session)
    {
        const uint pageLimit = 500;
        var files = new List<SwitcherFile>();
        string? cursor = null;
        do
        {
            FileSummaryPage page = session.ListFiles(
                FileFilter.OpenableDocuments,
                new Paging(cursor, pageLimit));
            files.AddRange(page.Items.Select(file => new SwitcherFile(file.Path, file.Name)));
            cursor = page.NextCursor;
        }
        while (cursor is not null);
        return [.. files];
    }

    private void QuickSwitcher_OpenRequested(
        object? sender,
        (string Path, WorkspaceOpenTarget Target) request)
    {
        Workspace?.OpenPath(request.Path, request.Target);
    }

    private void QuickSwitcher_Dismissed(object? sender, EventArgs e) =>
        QuickSwitcherDismissed?.Invoke(this, EventArgs.Empty);

    private void ReportTerminalStatus(string message, A11yPriority priority)
    {
        StatusText = message;
        // W0.5-3 residue: Windows lifecycle/error availability copy.
        _announce(new A11yEvent.HostComposed(message, priority));
    }

    private void RaiseCommandStates()
    {
        _openVaultCommand.RaiseCanExecuteChanged();
        _openRecentCommand.RaiseCanExecuteChanged();
        _closeVaultCommand.RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?> _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute(parameter);
    public void Execute(object? parameter) => _execute(parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?> _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<object?, Task> execute, Func<bool> canExecute)
        : this(execute, _ => canExecute())
    {
    }

    public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_isExecuting && _canExecute(parameter);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute(parameter);
        }
        catch (Exception exception)
        {
            HostLog.Write(HostDiagnosticEvent.VaultCommandFailed, exception);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class UiProgressListener : ScanProgressListener
{
    private readonly object _gate = new();
    private readonly Action<Action> _enqueueUi;
    private readonly Action<ScanProgress> _emit;
    private ScanProgress? _pending;
    private bool _dispatchScheduled;

    public UiProgressListener(Action<Action> enqueueUi, Action<ScanProgress> emit)
    {
        _enqueueUi = enqueueUi;
        _emit = emit;
    }

    public void OnProgress(ScanProgress @event)
    {
        lock (_gate)
        {
            _pending = @event;
            if (_dispatchScheduled)
            {
                return;
            }

            _dispatchScheduled = true;
        }

        _enqueueUi(Drain);
    }

    private void Drain()
    {
        ScanProgress? pending;
        lock (_gate)
        {
            pending = _pending;
            _pending = null;
            _dispatchScheduled = false;
        }

        if (pending is not null)
        {
            _emit(pending);
        }
    }
}

internal sealed class UiVaultEventListener : VaultEventListener
{
    private readonly Action<EventErrorCode, string, string> _onError;
    private readonly Action<FileChangeEvent> _onFileChange;
    private readonly Action<IndexPhase, ulong> _onIndexPhase;

    public UiVaultEventListener(
        Action<EventErrorCode, string, string> onError,
        Action<FileChangeEvent> onFileChange,
        Action<IndexPhase, ulong> onIndexPhase)
    {
        _onError = onError;
        _onFileChange = onFileChange;
        _onIndexPhase = onIndexPhase;
    }

    public void OnError(EventErrorCode code, string path, string message) =>
        _onError(code, path, message);
    public void OnFileChange(FileChangeEvent @event) => _onFileChange(@event);
    public void OnIndexPhase(IndexPhase phase, ulong filesSeen) =>
        _onIndexPhase(phase, filesSeen);
}
