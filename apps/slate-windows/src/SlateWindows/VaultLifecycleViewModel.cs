// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using SlateWindows.Commands;
using SlateWindows.Search;
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
/// <remarks>
/// It is also the command bridge's <see cref="ISlateCommandHost"/> (contract
/// P17): registered actions resolve live state through it at invoke time
/// instead of capturing a workspace, so vault open and close never mutate
/// the registry. The interface names members this type already exposed —
/// implementing it added no surface.
/// </remarks>
internal sealed class VaultLifecycleViewModel
    : INotifyPropertyChanged, IDisposable, ISlateCommandHost
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
    private readonly Func<Action, CancellationToken, Task>? _filterWorker;
    private readonly Func<Action, CancellationToken, Task>? _importWorker;
    private readonly Func<
        Func<(ScanReport Report, SwitcherFile[] SwitcherFiles)>,
        Task<(ScanReport Report, SwitcherFile[] SwitcherFiles)>> _runSessionLoad;
    private readonly Func<Action, Task> _runSyncMarkerArm;
    private readonly TimeSpan? _syncMarkerDebounce;

    /// <summary>
    /// W4-8 (SD6/SDR-5): the once-per-vault-PATH announce gate, keyed
    /// by normalized vault root and deliberately never cleared.
    ///
    /// It lives here rather than on the workspace because it has to
    /// OUTLIVE the workspace: <c>CloseSession</c> disposes and nulls
    /// the workspace, and every open builds a fresh one, so a
    /// workspace-scoped gate re-arms on reopen and interrupts the
    /// reader again with a risk story that has not changed just
    /// because the vault was closed and reopened mid-session. The mac
    /// twin (<c>AppState.syncAnnouncedVaultPath</c>, AppState.swift
    /// :11549) makes the same call for the same reason, in the same
    /// words — but it is a single-slot LATCH, so switching away and
    /// back re-announces there and stays silent here (divergence
    /// SDD-6; a SET is the strictly quieter reading of "at most once
    /// per vault"). A different vault path re-arms; the same path
    /// stays silent, for the life of the process.
    ///
    /// Comparison follows the vault-root convention this file already
    /// uses for Recents (<c>RecentVaultsStore.Add/Remove</c> compare
    /// <c>Path.GetFullPath</c> results with
    /// <c>StringComparer.OrdinalIgnoreCase</c>), plus the trailing
    /// separator trim <see cref="SyncMarkerWatcher"/> applies, so
    /// <c>C:\Vault</c> and <c>c:\vault\</c> are one vault. It is a
    /// STRING key, not a filesystem identity: a vault reached through
    /// a substituted drive or a junction reads as a different vault
    /// and re-announces, which is the safe direction to be wrong in.
    ///
    /// Touched only from the announcement path, which runs on the UI
    /// context, so it needs no lock.
    /// </summary>
    private readonly HashSet<string> _announcedSyncVaultPaths =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dispatcher? _lifecycleDispatcher;
    private CommandPaletteViewModel? _palette;
    private PaletteCommandSource? _paletteSource;
    private SearchOverlayViewModel? _search;
    private readonly AsyncRelayCommand _openVaultCommand;
    private readonly AsyncRelayCommand _openRecentCommand;
    private readonly RelayCommand _closeVaultCommand;
    private readonly RelayCommand _toggleSearchCommand;

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
    private Task _sessionLoadCompletion = Task.CompletedTask;
    private int _sidebarRefreshTicket;
    // W4-8 (SD8): the bounded sync-marker watch, owned by the vault
    // lifecycle for exactly the open-vault state.
    private SyncMarkerWatcher? _syncMarkerWatcher;

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
        Func<Action, CancellationToken, Task>? treeWorker = null,
        Func<Action, CancellationToken, Task>? filterWorker = null,
        Func<Action, CancellationToken, Task>? importWorker = null,
        Func<
            Func<(ScanReport Report, SwitcherFile[] SwitcherFiles)>,
            Task<(ScanReport Report, SwitcherFile[] SwitcherFiles)>>? sessionLoadWorker = null,
        Func<Action, Task>? syncArmWorker = null,
        TimeSpan? syncMarkerDebounce = null)
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
        _lifecycleDispatcher = currentUiContext is DispatcherSynchronizationContext
            ? Dispatcher.CurrentDispatcher
            : null;
        _treeUiContext = treeUiContext
            ?? (currentUiContext is DispatcherSynchronizationContext ? currentUiContext : null);
        _treeWorker = treeWorker;
        _filterWorker = filterWorker;
        _importWorker = importWorker;
        _runSessionLoad = sessionLoadWorker ?? (work => Task.Run(work));
        // W4-8 (SD4/SDR-2): the marker arm is filesystem I/O, so it
        // rides its own hop off the dispatcher; injectable for the
        // interleave facts, exactly like the session load above.
        _runSyncMarkerArm = syncArmWorker ?? (work => Task.Run(work));
        _syncMarkerDebounce = syncMarkerDebounce;
        _openVaultCommand = new AsyncRelayCommand(PickAndOpenVaultAsync, () => !IsBusy);
        _openRecentCommand = new AsyncRelayCommand(
            OpenRecentAsync,
            parameter => parameter is RecentVault && !IsBusy);
        _closeVaultCommand = new RelayCommand(
            _ => CloseVault(),
            _ => IsVaultOpen && !IsBusy);
        // W5-2 close-out (#742): UNGUARDED, matching mac's palette
        // action (toggleSearchOverlay(), SlateCommands.swift:1483-1494).
        // The palette invokes BEFORE dismissing (P9), so a modal gate
        // here would see the palette itself open and refuse every
        // palette invocation; the modal decision stays on the chord
        // path (MainWindow.Window_PreviewKeyDown). The no-vault refusal
        // lives inside Toggle() → Open(), which announces
        // SearchNeedsVault — mac's exact posture.
        _toggleSearchCommand = new RelayCommand(_ => Search.Toggle(), _ => true);
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

    /// <summary>W5-2 close-out (#742): the registered
    /// <c>slate.view.toggleSearch</c> surface — the palette and the
    /// Workspace ▸ Search Vault… menu item both run it. See the
    /// constructor note for why it is unguarded.</summary>
    public ICommand ToggleSearchCommand => _toggleSearchCommand;

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

    /// <summary>
    /// The command palette (W5-1, #741). Public because WPF binding
    /// reflection only sees public properties — an internal one fails
    /// silently and renders nothing (the W4-4 lesson).
    /// </summary>
    /// <remarks>
    /// Created on first access rather than in the constructor: creating
    /// it registers the whole command catalog and needs a dispatcher,
    /// and the shell is the only caller that wants either. The
    /// null-coalescing assignment is what keeps PINV-3's "exactly one
    /// registry" true — every later access returns the same instance.
    /// It survives vault open and close by contract P17, so it is
    /// deliberately not reset alongside <see cref="Workspace"/>.
    /// </remarks>
    public CommandPaletteViewModel Palette =>
        _palette ??= new CommandPaletteViewModel(
            _paletteSource ??= new PaletteCommandSource(
                this,
                _lifecycleDispatcher ?? Dispatcher.CurrentDispatcher),
            _announce);

    /// <summary>
    /// The vault-search overlay (W5-2, #742). Public for the same W4-4
    /// reason as <see cref="Palette"/>: WPF binding reflection only sees
    /// public properties.
    /// </summary>
    /// <remarks>
    /// One app-lifetime instance, the palette's shape: the
    /// <see cref="VaultSearchSource"/> reads the session and vault root
    /// through live delegates, so a vault switch invalidates in-flight
    /// results through the view model's session-identity staleness arm
    /// (contract S5) rather than by rebuilding the overlay. Constructed
    /// on first access — the shell touches it at startup, on the UI
    /// thread, which is where the view model captures its
    /// <see cref="SynchronizationContext"/>.
    /// </remarks>
    public SearchOverlayViewModel Search
    {
        get
        {
            if (_search is null)
            {
                _search = new SearchOverlayViewModel(
                    new VaultSearchSource(
                        () => _session,
                        () => _session is null || _vaultPath.Length == 0
                            ? null
                            : _vaultPath),
                    _announce);
                _search.OpenRequested += Search_OpenRequested;
            }

            return _search;
        }
    }

    /// <summary>
    /// W5-2 SD-4: the shell's modal-surface gate over opening the
    /// search overlay from a view-model path. <c>MainWindow</c>
    /// installs <c>TryClearTheWayForSearch</c> — the same
    /// <c>ModalSurfaces.DecideSearchOpen</c> decision the Ctrl+Shift+F
    /// chord applies, including the Quick Open dismissal — so a
    /// reading-view tag activation can never open the overlay beneath
    /// a sheet. Null (headless tests, window-free hosts) admits: with
    /// no window there is no modal surface to open beneath.
    /// </summary>
    internal Func<bool>? SearchOpenAdmission { get; set; }

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

        // P14: dismissed AFTER the cancellable gate, matching CloseVault.
        // Dismissing before it meant a refused close — a dirty-tab prompt
        // the user cancels, an import in flight — left the vault open with
        // the palette already gone.
        _palette?.Dismiss();
        // The W5-1 vault-transition precedent, extended to search: a
        // direct A→B open bypasses CloseVault, and mac routes exactly
        // this path through closeSearchOverlay too (AppState.swift:9761,
        // the #876 Codex round-2 bug). Closed BEFORE CloseSession so the
        // in-flight cancellation targets vault A's search.
        CloseSearchForVaultTransition();

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
            Task<(ScanReport Report, SwitcherFile[] SwitcherFiles)> loadTask = _runSessionLoad(() =>
            {
                ScanReport report = activeSession.ScanInitialWithProgress(activeCancel, activeProgressListener);
                return (report, LoadSwitcherFiles(activeSession));
            });
            _sessionLoadCompletion = loadTask;
            (ScanReport Report, SwitcherFile[] SwitcherFiles) loaded = await loadTask;
            if (generation == _generation)
            {
                StatusText = $"Scan finished: {loaded.Report.FilesIndexed} files indexed.";
                ProgressMaximum = Math.Max(1, loaded.Report.FilesSeen);
                ProgressValue = ProgressMaximum;
                IsProgressIndeterminate = false;
                InitializeWorkspace(_session, root, loaded.SwitcherFiles);
                StartSyncMarkerWatch(generation, root);
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
            if (_sessionLoadCompletion.IsCompleted)
            {
                _sessionLoadCompletion = Task.CompletedTask;
            }

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

        // P14: the palette must never be open with no vault, or the
        // next vault open auto-presents it. Dismissed BEFORE the session
        // goes away, so the overlay cannot survive the gap.
        _palette?.Dismiss();
        // Search joins the same teardown (mac closeVault,
        // AppState.swift:11016-11017): overlay closed, retained query
        // cleared, while the session still exists.
        CloseSearchForVaultTransition();

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
        if (_lifecycleDispatcher is Dispatcher dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(DisposeCore);
            return;
        }

        DisposeCore();
    }

    private void DisposeCore()
    {
        ++_generation;
        CloseSession();

        // Releases the one CommandRegistry (PINV-3). Null unless the
        // shell actually reached for the palette.
        _paletteSource?.Dispose();
        _paletteSource = null;
        _palette = null;

        if (_search is not null)
        {
            _search.OpenRequested -= Search_OpenRequested;
            _search.Dispose();
            _search = null;
        }
    }

    /// <summary>
    /// The vault-transition teardown mac performs in both closeVault and
    /// the direct-switch path (<c>AppState.swift:9761-9762</c>,
    /// <c>:11016-11017</c>): nothing of vault A's search — query, scope,
    /// rows, announcement memory — may re-arm inside vault B. Through
    /// <see cref="SearchOverlayViewModel.ResetForVaultTransition"/>
    /// rather than <c>Close()</c> (codex round 12): Close early-returns
    /// on a CLOSED overlay, and a superseded overlay is closed with its
    /// scope deliberately preserved — the old body carried vault A's
    /// tag scope across the switch.
    /// </summary>
    private void CloseSearchForVaultTransition()
    {
        if (_search is SearchOverlayViewModel search)
        {
            search.ResetForVaultTransition();
        }
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
                ProgressMaximum = Math.Max(1, finished.Report.FilesSeen);
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
        if (generation != _generation)
        {
            return;
        }
        if (code == EventErrorCode.CompactionFailed)
        {
            // W4-7 (contract H13, divergence HD-4): core's composed
            // message relayed as a MEDIUM announcement, once per path
            // per session — a background maintenance failure is not a
            // High-priority interruption.
            Workspace?.AnnounceHistoryCompactionFailure(path, message);
            return;
        }
        ReportTerminalStatus(message, A11yPriority.High);
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
            else if (@event.Kind == FileChangeKind.Modified)
            {
                Workspace?.InvalidateModifiedPath(@event.Path);
            }

            Workspace?.InvalidateAllInteractionStates();
            // Reading embed cards depend on OTHER files (W3-5): the
            // change stream reaches every open reading model, which
            // applies its own reverse-dependency filter. A rename
            // notifies both sides of the move.
            Workspace?.NotifyReadingOfVaultChange(@event.Kind, @event.Path);
            if (@event.Kind == FileChangeKind.Renamed
                && @event.PreviousPath is string renamedFrom)
            {
                Workspace?.NotifyReadingOfVaultChange(@event.Kind, renamedFrom);
            }
            // Bases surfaces re-execute on vault changes too (contract
            // C9's vault-event arm — property panel, task toggles,
            // editor saves, and external edits all land here).
            Workspace?.NotifyBasesOfVaultChange(@event.Path);
            // W4-7 (HR-2's vault-event arm): a Modified on the active
            // path appended a version row the save funnel never saw
            // (Bases grid edits, sync, external editors).
            if (@event.Kind == FileChangeKind.Modified)
            {
                Workspace?.NotifyHistoryOfVaultChange(@event.Path);
            }
            if (@event.Kind == FileChangeKind.Renamed
                && @event.PreviousPath is string basesRenamedFrom)
            {
                Workspace?.NotifyBasesOfVaultChange(basesRenamedFrom);
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
        // W4-8 (SD8/SDINV-8): the marker watch dies FIRST and
        // synchronously. Stop is idempotent and a stopped watcher never
        // invokes its callback, so nothing can enqueue a refresh into the
        // workspace this method is about to dispose. This is the single
        // deepest teardown seam — vault switch (OpenVaultAsync),
        // CloseVault, and DisposeCore all funnel here.
        //
        // The ARM moved off the dispatcher (StartSyncMarkerWatch) but
        // this teardown deliberately did NOT. Two reasons: SDINV-5's
        // "teardown drains" means a background stop would have to be
        // JOINED right here anyway — the same block, one hop later —
        // and the cost is asymmetric, because what stalls on a
        // virtualized or unreachable root is opening the change
        // notification handle, not closing it. Stop also has to happen
        // before the workspace below it is disposed, which is exactly
        // the ordering a synchronous call gives for free.
        _syncMarkerWatcher?.Dispose();
        _syncMarkerWatcher = null;

        if (FileSidebar is FilesSidebarViewModel sidebar)
        {
            SidebarSessionShutdown shutdown = sidebar.BeginSessionShutdownAndCaptureWork();
            try
            {
                shutdown.TreeRefresh.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                HostLog.Write(HostDiagnosticEvent.SidebarTreeRefreshShutdownFailed, exception);
            }

            try
            {
                shutdown.Filter.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                // A faulted task is terminal, so the session can now be
                // released without racing live FFI work.
                HostLog.Write(HostDiagnosticEvent.SidebarFilterShutdownFailed, exception);
            }

            try
            {
                shutdown.ChildExpansions.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                HostLog.Write(HostDiagnosticEvent.SidebarChildExpansionShutdownFailed, exception);
            }

            shutdown.SessionWork.GetAwaiter().GetResult();
        }

        _scanAnnouncements.Reset();
        try
        {
            _scanCancel?.Cancel();
        }
        catch (Exception exception)
        {
            HostLog.Write(HostDiagnosticEvent.VaultCommandFailed, exception);
        }

        try
        {
            _sessionLoadCompletion.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        catch (VaultException.Cancelled)
        {
        }
        catch (Exception exception)
        {
            HostLog.Write(HostDiagnosticEvent.VaultCommandFailed, exception);
        }

        _sessionLoadCompletion = Task.CompletedTask;
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
            Workspace.EditorTagActivated -= Workspace_EditorTagActivated;
            Workspace.ReadingTagActivated -= Workspace_ReadingTagActivated;
            Workspace.FocusBoundaryRequested -= Workspace_FocusBoundaryRequested;
            Workspace.TemplateNoteWritten -= Workspace_TemplateNoteWritten;
            Workspace.PropertyChanged -= Workspace_SheetPresented;
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
            _confirmDirtyClose,
            preferencesStore: new AppPreferencesStore());
        // W4-8 (SD6/SDR-5): hand the workspace an admission gate over
        // the LIFECYCLE's per-path set instead of letting it keep its
        // own flag, which would die with it. Installed before the
        // first probe can run — SD4's arm-then-probe is started by
        // StartSyncMarkerWatch, strictly after this returns.
        string announceKey = SyncAnnounceKey(root);
        workspace.SyncAnnounceAdmission = () => _announcedSyncVaultPaths.Add(announceKey);
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
            treeWorker: _treeWorker,
            filterWorker: _filterWorker,
            importWorker: _importWorker);
        var switcher = new QuickSwitcherViewModel(session, root, _announce, switcherFiles);

        workspace.FileOpened += Workspace_FileOpened;
        workspace.EditorTagActivated += Workspace_EditorTagActivated;
        workspace.ReadingTagActivated += Workspace_ReadingTagActivated;
        workspace.FocusBoundaryRequested += Workspace_FocusBoundaryRequested;
        // W5-3 (T12, T7): the creation parent is the sidebar's rule —
        // frozen by the workspace at picker open — and `{{vault}}` is
        // the root's basename; a written template note refreshes the
        // sidebar the way its own creates do.
        FilesSidebarViewModel capturedSidebar = sidebar;
        workspace.TemplateCreationParentProvider = capturedSidebar.CreationParentPath;
        workspace.TemplateVaultNameProvider =
            () => Path.GetFileName(Path.TrimEndingDirectorySeparator(root));
        workspace.TemplateNoteWritten += Workspace_TemplateNoteWritten;
        sidebar.OpenTargetRequested += FileSidebar_OpenTargetRequested;
        switcher.OpenRequested += QuickSwitcher_OpenRequested;
        switcher.Dismissed += QuickSwitcher_Dismissed;

        Workspace = workspace;

        // Subscribed AFTER the Workspace assignment on purpose: the
        // shell observes that assignment synchronously and subscribes
        // its own sheet handlers first, so on a sheet presentation the
        // sheet's focus grab is queued BEFORE any restore this
        // handler's dismissals queue — the same converged order the
        // pickers already produce.
        workspace.PropertyChanged += Workspace_SheetPresented;

        // PINV-7: the workspace requeries far more often than the vault
        // lifecycle does — on every tab switch — and that is the frequency
        // the invariant's own example needs.
        workspace.RegisteredCommandStatesChanged = RaiseRegisteredCommandStates;
        FileSidebar = sidebar;
        QuickSwitcher = switcher;
        WorkspaceReady?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>W4-8 test seam: the marker watch currently owned by
    /// this lifecycle, so a fact can prove the arm/teardown race opens
    /// no directory handles.</summary>
    internal SyncMarkerWatcher? SyncMarkerWatchForTests => _syncMarkerWatcher;

    /// <summary>The announce-gate key for a vault root. The root is
    /// already <c>Path.GetFullPath</c>'d by <see cref="OpenVaultAsync"/>
    /// before it reaches here, so only the trailing separator needs
    /// normalizing; the OrdinalIgnoreCase comparison lives on the set
    /// itself (see <see cref="_announcedSyncVaultPaths"/>).</summary>
    private static string SyncAnnounceKey(string root) =>
        Path.TrimEndingDirectorySeparator(root);

    /// <summary>
    /// W4-8 (SD8 + SD4): arm the bounded marker watch, THEN run the
    /// vault-open detection probe. The order is the contract, not a
    /// preference — a marker landing between a probe and a later arm
    /// emits no event and would stay invisible until the next manual
    /// refresh. <see cref="SyncMarkerWatcher.Start"/> returns with the
    /// handles open, and the probe is chained INSIDE the same hop, so
    /// there is no window between them.
    ///
    /// Why the arm is not inline: it is synchronous filesystem I/O —
    /// three <c>Directory.Exists</c> probes plus up to three
    /// <c>CreateFileW</c> calls against the vault root — and on SDR-2's
    /// exact scenario (an unresponsive SMB share, a files-on-demand
    /// OneDrive root) each of those can block for seconds. Run on the
    /// dispatcher it freezes the window at vault open. One background
    /// hop keeps the ordering contract and unblocks the UI.
    ///
    /// The generation is re-checked TWICE: once here, and once on the
    /// arming thread immediately before the handles open. A teardown
    /// can land while <see cref="InitializeWorkspace"/> is still
    /// running, in which case <see cref="CloseSession"/> reads a
    /// still-null <c>_syncMarkerWatcher</c> and tears everything down —
    /// and without these checks this method would then arm three
    /// <see cref="FileSystemWatcher"/>s with no owner left to stop
    /// them. They would live for the process lifetime and hold the
    /// vault directory undeletable (SDINV-5/SDINV-8).
    /// </summary>
    private void StartSyncMarkerWatch(int generation, string root)
    {
        _syncMarkerWatcher?.Dispose();
        _syncMarkerWatcher = null;
        if (generation != Volatile.Read(ref _generation))
        {
            return;
        }

        SyncMarkerWatcher watcher = new(
            root,
            () => OnSyncMarkerFire(generation),
            _syncMarkerDebounce);
        // Published BEFORE the hop is queued, so any teardown from here
        // on finds it and stops it; Start is a no-op after Stop.
        _syncMarkerWatcher = watcher;
        _ = _runSyncMarkerArm(() =>
        {
            // Defence in depth for the hop that gets parked: a teardown
            // that already read a null field cannot be relied on to
            // stop this watcher, so prove liveness again before opening
            // a single handle.
            if (generation != Volatile.Read(ref _generation))
            {
                watcher.Dispose();
                return;
            }

            watcher.Start();
            // SD4 trigger (a): the vault-open probe, strictly after the
            // arm and back on the UI context, where the workspace's
            // refresh funnel and the SD6 gate live.
            _enqueueUi(() =>
            {
                if (generation == Volatile.Read(ref _generation))
                {
                    Workspace?.RefreshSyncDiagnostics();
                }
            });
        });
    }

    /// <summary>
    /// A debounced marker fire. <see cref="SyncMarkerWatcher"/> invokes
    /// this INSIDE its own lock — that is what makes "never fires after
    /// Stop returns" structural — so this must enqueue and return.
    ///
    /// It therefore hands the UI hop to the threadpool instead of
    /// calling <c>_enqueueUi</c> directly: the enqueue delegate is
    /// injected, and a blocking one (a dispatcher <c>Invoke</c>, or a
    /// test's inline <c>action =&gt; action()</c>) would otherwise run
    /// the whole refresh funnel under the watcher's lock and deadlock
    /// against <see cref="CloseSession"/>'s watcher stop. Structural,
    /// not a documented promise the next caller has to remember.
    ///
    /// The generation is read with <see cref="Volatile"/> because this
    /// comparison can run on a pool thread (an inline enqueue delegate
    /// never reaches the dispatcher), and the liveness re-check on the
    /// UI context is what SDINV-5 requires.
    /// </summary>
    private void OnSyncMarkerFire(int generation)
    {
        _ = Task.Run(() => _enqueueUi(() =>
        {
            if (generation == Volatile.Read(ref _generation))
            {
                Workspace?.RefreshSyncDiagnostics();
            }
        }));
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

        if (FileSidebar?.IsExpandingLoaded == true || FileSidebar?.IsLoadingChildren == true)
        {
            FileSidebar.CancelExpandLoaded();
            FileSidebar.CancelChildExpansions();
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

    /// <summary>W5-3 (T7): a template note was written outside the
    /// sidebar's own mutation paths, which refresh inline — this one
    /// refreshes the tree the same way so the new note is visible and
    /// selectable immediately.</summary>
    private void Workspace_TemplateNoteWritten(object? sender, string path) =>
        FileSidebar?.Refresh();

    private void Workspace_EditorTagActivated(object? sender, string tag) =>
        FileSidebar?.ActivateTag(tag);

    /// <summary>
    /// W5-2 SD-4: a reading-view tag opens the tag-scoped search
    /// overlay, never the sidebar filter. The shell's modal gate runs
    /// first — an overlay must not open (invisibly) beneath a sheet —
    /// and refusal leaves the overlay untouched: no cleared query, no
    /// armed scope. Past the gate, <see
    /// cref="SearchOverlayViewModel.OpenTagScoped"/> performs mac's
    /// exact ordering (clear query, open, scope last).
    /// </summary>
    private void Workspace_ReadingTagActivated(object? sender, string tag)
    {
        SearchOverlayViewModel search = Search;
        if (!search.IsOpen && SearchOpenAdmission?.Invoke() == false)
        {
            return;
        }

        search.OpenTagScoped(tag);
    }

    private void Workspace_FocusBoundaryRequested(
        object? sender,
        WorkspaceFocusBoundary boundary) =>
        WorkspaceFocusBoundaryRequested?.Invoke(this, boundary);

    /// <summary>
    /// Invariant 6's presentation-time admission (codex round 11,
    /// #742). A sheet may PRESENT from a deferred continuation — the
    /// files-citing load, the bases edit-JSON fetch, a citation
    /// summary parked on <c>RowsPublished</c> — and the modal decision
    /// taken at command dispatch is stale by the time the sheet lands:
    /// a picker opened during that window would sit hidden-but-live
    /// beneath the sheet, the round-1/round-10 class SD-5 retires.
    /// Enforced here, reactively, the moment a sheet property becomes
    /// non-null, so every present and future presentation path is
    /// covered without per-site admission calls. All three dismissals
    /// are idempotent; the palette arm also fires during the
    /// sanctioned P9 transient (a palette-invoked SYNCHRONOUS sheet),
    /// where it runs the dismissal P9 itself would run moments later.
    /// P9's subsequent success steps survive that early dismissal:
    /// <c>RecordInvocation(row.Id)</c> reads only its parameter, never
    /// palette state, and P9's own <c>Dismiss()</c> becomes a no-op.
    /// </summary>
    private void Workspace_SheetPresented(
        object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (sender is not WorkspaceViewModel workspace
            || !ReferenceEquals(workspace, Workspace))
        {
            return;
        }

        // A flat name-by-name read, the CurrentModalSurfaceState
        // idiom: each arm sits next to the property it reads, so a
        // wrong-property error is visible here rather than hidden in
        // shared plumbing. Census-pinned against the sheet members of
        // ModalSurface in ModalSurfaceTests.
        bool presented = eventArgs.PropertyName switch
        {
            nameof(WorkspaceViewModel.AddPropertySheet) =>
                workspace.AddPropertySheet is not null,
            nameof(WorkspaceViewModel.BulkRenameSheet) =>
                workspace.BulkRenameSheet is not null,
            nameof(WorkspaceViewModel.CitationDetails) =>
                workspace.CitationDetails is not null,
            nameof(WorkspaceViewModel.CitationSummary) =>
                workspace.CitationSummary is not null,
            nameof(WorkspaceViewModel.FilesCiting) =>
                workspace.FilesCiting is not null,
            nameof(WorkspaceViewModel.DashboardEditorSheet) =>
                workspace.DashboardEditorSheet is not null,
            nameof(WorkspaceViewModel.BaseQueryBuilderSheet) =>
                workspace.BaseQueryBuilderSheet is not null,
            nameof(WorkspaceViewModel.TemplatePickerSheet) =>
                workspace.TemplatePickerSheet is not null,
            nameof(WorkspaceViewModel.TemplateFlowSheet) =>
                workspace.TemplateFlowSheet is not null,
            _ => false,
        };
        if (!presented)
        {
            return;
        }

        // SUPERSEDE, not Close, for search (red team after round 11):
        // the landing sheet is borrowing the screen the way the
        // palette does, so the scope survives and Ctrl+Shift+F after
        // the sheet restores the overlay the user actually had.
        // Backing fields, not the lazy getters: closing a picker that
        // was never constructed should not construct it — in a
        // window-free host the Palette getter registers the whole
        // command catalog as a side effect of this dispatch.
        _search?.Supersede();
        QuickSwitcher?.Dismiss();
        _palette?.Dismiss();
    }

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

    /// <summary>
    /// A search activation (contract S9): open the hit in the CURRENT
    /// tab (contract S10 — no modifier variants; mac's ⌘Return is
    /// rejected by its own key monitor and only ⌘-click works, so
    /// Windows builds neither), derive the match line host-side, park
    /// the caret at its start, and announce
    /// <c>SearchResultOpened(filename, line, snippet)</c> with the
    /// WHOLE-FILE line number.
    /// </summary>
    /// <remarks>
    /// The overlay has already closed and recorded the recent
    /// (record→close→open, the phase-1 ordering fact); this handler runs
    /// synchronously after it on the UI thread — the Windows tab load is
    /// synchronous, so there is no mac-style await on a pending note
    /// load. An open the dirty-navigation prompt refuses leaves the user
    /// on their current note, so nothing is scrolled or announced.
    /// </remarks>
    private void Search_OpenRequested(object? sender, SearchOpenRequest request)
    {
        if (Workspace is not WorkspaceViewModel workspace)
        {
            return;
        }

        // S10: opening a file that is already the selected file does not
        // re-open it (mac wasAlreadyOpen, AppState.swift:9471-9477).
        WorkspaceTabViewModel? active = workspace.ActiveGroup.ActiveTab;
        bool alreadyActive = active is { IsMarkdown: true }
            && string.Equals(active.Path, request.Path, StringComparison.Ordinal);
        if (!alreadyActive)
        {
            workspace.OpenPath(request.Path, WorkspaceOpenTarget.CurrentTab);
        }

        WorkspaceTabViewModel? tab = workspace.ActiveGroup.ActiveTab;
        if (tab is not { IsMarkdown: true }
            || !string.Equals(tab.Path, request.Path, StringComparison.Ordinal))
        {
            return;
        }

        int fileLine = DeriveSearchResultLine(tab, request);
        // The caret park follows the W4-3 dirty posture rather than mac's
        // unverified scroll: a dirty or externally stale buffer no longer
        // matches the indexed bytes the line was derived from, so the
        // caret stays put (the silent non-move is the honest observable).
        // The announcement still fires — the note did open, and the line
        // describes the saved note the search indexed.
        if (!tab.IsDirty && !tab.IsExternallyStale)
        {
            tab.EditorInteractions?.RequestCaret(
                SearchLineLocator.LineStartOffset(tab.Text, fileLine));
        }

        _announce(new A11yEvent.SearchResultOpened(
            Path.GetFileName(request.Path),
            (uint)fileLine,
            request.Snippet));
    }

    /// <summary>
    /// The whole-file line for an activated hit. The scan runs over the
    /// BODY (mac scans its body-space buffer) and the result is rebased
    /// to file space with core's <c>BodyLineOffset</c> — the one
    /// conversion authority (<c>read_note_parts</c>, the U3-5 law:
    /// frontmatter geometry is never re-derived host-side). Windows
    /// buffers are whole-file, so the same number both scrolls the
    /// editor and is announced (mac needs <c>fileLine(fromBodyLine:)</c>
    /// only for the announcement).
    /// </summary>
    private int DeriveSearchResultLine(
        WorkspaceTabViewModel tab, SearchOpenRequest request)
    {
        if (_session is VaultSession session)
        {
            try
            {
                NotePartsBundle parts = session.ReadNoteParts(request.Path);
                return SearchLineLocator.FirstTokenLine(parts.Body, request.Query)
                    + (int)parts.BodyLineOffset;
            }
            catch (VaultException)
            {
                // The note opened but its parts read failed (deleted in
                // the gap, a reparse refusal): degrade to a whole-file
                // scan of the loaded buffer, which is already file-space.
            }
        }

        return SearchLineLocator.FirstTokenLine(tab.Text, request.Query);
    }

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

        // PINV-7: requery the registered catalog by ENUMERATION, so a
        // newly registered command cannot be silently omitted the way the
        // four hand-maintained lists allow. Only meaningful once the
        // bridge exists, hence the null check — an unopened palette has
        // registered nothing yet.
        RaiseRegisteredCommandStates();
    }

    /// <summary>
    /// PINV-7: requery the registered catalog by ENUMERATION, so a newly
    /// registered command cannot be silently omitted the way the
    /// hand-maintained lists allow. No-op until the bridge exists.
    /// </summary>
    private void RaiseRegisteredCommandStates()
    {
        if (_paletteSource is not null)
        {
            SlateCommandRegistrar.RaiseCommandStates(this);
        }
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
    private ScanProgress.Started? _pendingStarted;
    private ScanProgress.FileIndexed? _pendingFileIndexed;
    private ScanProgress? _pendingTerminal;
    private bool _terminalSeen;
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
            if (_terminalSeen)
            {
                return;
            }

            switch (@event)
            {
                case ScanProgress.Started started:
                    _pendingStarted = started;
                    break;
                case ScanProgress.FileIndexed indexed:
                    _pendingFileIndexed = indexed;
                    break;
                case ScanProgress.Finished or ScanProgress.Cancelled or ScanProgress.Failed:
                    _terminalSeen = true;
                    _pendingTerminal = @event;
                    break;
            }

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
        ScanProgress? started;
        ScanProgress? indexed;
        ScanProgress? terminal;
        lock (_gate)
        {
            started = _pendingStarted;
            indexed = _pendingFileIndexed;
            terminal = _pendingTerminal;
            _pendingStarted = null;
            _pendingFileIndexed = null;
            _pendingTerminal = null;
            _dispatchScheduled = false;
        }

        if (started is not null)
        {
            _emit(started);
        }
        if (indexed is not null)
        {
            _emit(indexed);
        }
        if (terminal is not null)
        {
            _emit(terminal);
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
