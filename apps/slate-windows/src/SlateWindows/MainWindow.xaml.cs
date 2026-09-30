// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using Microsoft.Win32;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows;

public partial class MainWindow : Window
{
    private readonly VaultLifecycleViewModel _viewModel;
    private readonly WindowPlacementManager _windowPlacement;
    private readonly AccessibilityNotificationDispatcher _announcer;
    private readonly ForegroundRescanRoute _foregroundRescan;
    private readonly ShellModalLoopMonitor _modalLoops;
    private IInputElement? _focusBeforeSwitcher;
    private QuickSwitcherViewModel? _observedQuickSwitcher;
    private WorkspaceViewModel? _observedWorkspace;
    private FilesSidebarViewModel? _observedFileSidebar;
    private bool _quickSwitcherCommitted;

    public MainWindow()
        : this(paletteRecentsStore: null, paletteLane: null)
    {
    }

    /// <summary>
    /// The shipped shell over a palette whose recents file and work lane a
    /// fact supplies (#1275) — so a hosted fact can park a recents write
    /// without touching the user's %LOCALAPPDATA% file. Production passes
    /// neither.
    /// </summary>
    internal MainWindow(
        SlateWindows.Commands.CommandPaletteRecentsStore? paletteRecentsStore,
        ICommandPaletteWorkLane? paletteLane)
    {
        InitializeComponent();
        // R-5 (#1247, as the owner amended it): a landing in the Files tree —
        // a restore's, Tab's, a click's — lands as the region does: its
        // selected row, else its first row unselected, whose focus opens
        // nothing — never the bare tree.
        SelectorFocus.SetOwnLanding(FilesTree, LandOnFilesTree);
        // R-5 (#1247, OD-11c): a click on a populated list's empty area lands
        // on a row.
        SelectorFocus.RegisterClickRule();
        // The Tags tree's selection ACTIVATES a tag filter (R-3): its
        // landing is its selected tag, else its first tag UNSELECTED —
        // never one it would apply.
        TreeView tags = FindWithAutomationId<TreeView>(FilesPaneBorder, "SidebarTagTree")
            ?? throw new InvalidOperationException("SidebarTagTree is not in the shell's XAML.");
        SelectorFocus.SetOwnLanding(tags, () => LandOnSidebarTree(tags, selectedPath: null));
        // S4 (#1247): every selection-committing list owns its landing, so a
        // restore whose token is one of its rows lands as the region does —
        // the filter's results and the dual pane OPEN on selection (R-2),
        // the rail SWITCHES the leaf — and never re-selects a row by focus.
        SelectorFocus.SetOwnLanding(
            FilterResultsList, () => SelectorFocus.FocusFirstOrSelectedItem(FilterResultsList) || LandOnFilesTree());
        ListBox dualPane = FindWithAutomationId<ListBox>(FilesPaneBorder, "SidebarDualPane")
            ?? throw new InvalidOperationException("SidebarDualPane is not in the shell's XAML.");
        SelectorFocus.SetOwnLanding(dualPane, () => SelectorFocus.FocusFirstOrSelectedItem(dualPane) || LandOnFilesTree());
        SelectorFocus.SetOwnLanding(RightPaneLeavesList, () => SelectorFocus.FocusFirstOrSelectedItem(RightPaneLeavesList));
        KeepLeafKeysThroughPublications();
        // W7-7 PR 8 (R-10, OD-12): the one editor landing this window holds,
        // and its window-level cancellations — the window losing activation,
        // and WPF's restore when it comes back (no one's move).
        _editorLandings = new EditorLandingSlot(Dispatcher);
        Activated += (_, _) => _editorLandings.WindowActivated();
        Deactivated += (_, _) => _editorLandings.WindowDeactivated();
        _windowPlacement = new WindowPlacementManager(this);
        _announcer = new AccessibilityNotificationDispatcher(StatusTextBlock);
        // OD-7: the window's one dispatcher, inherited by every surface in
        // it (and found by the windows it owns), so the launch phase, its
        // queue and its provider exist once per window.
        AccessibilityNotificationDispatcher.SetAnnouncer(this, _announcer);
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (_, _) => Close()));
        _viewModel = new VaultLifecycleViewModel(
            PickVaultAsync,
            action => _ = Dispatcher.InvokeAsync(action),
            ConfirmRemoveMissingRecentAsync,
            announce: _announcer.Post,
            copyText: CopyText,
            confirmUnsavedClose: ConfirmUnsavedClose,
            confirmDirtyNavigation: ConfirmDirtyNavigation,
            confirmDirtyClose: ConfirmDirtyClose,
            confirmDestructive: ConfirmDestructive,
            pickImportSources: PickImportSourcesAsync,
            // W6-1 PR A (contract A5): the canvas coalescer queues
            // RENDERED lines, so it cannot use the event seam above.
            // BOTH seams must be threaded here or every canvas
            // announcement falls into the lifecycle's no-op default and
            // dies silently in production while every fact that injects
            // its own sink stays green. AnnouncementSeamCensus reads
            // this call and fails if either argument goes missing.
            announceRendered: _announcer.Post,
            paletteRecentsStore: paletteRecentsStore,
            paletteLane: paletteLane);
        _viewModel.RecentVaultsChanged += ViewModel_RecentVaultsChanged;
        _viewModel.ReturnedToWelcome += ViewModel_ReturnedToWelcome;
        _viewModel.WorkspaceReady += ViewModel_WorkspaceReady;
        _viewModel.QuickSwitcherDismissed += ViewModel_QuickSwitcherDismissed;
        _viewModel.WorkspaceFocusBoundaryRequested += ViewModel_WorkspaceFocusBoundaryRequested;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        DataContext = _viewModel;
        // W6-1 PR C (contract C8): the canvas mode stack's M4 rule needs
        // to tell "focus moved to another pane" (cancel) from "focus
        // moved into an overlay layered over this tab" (keep the mode
        // alive — Commit Mode, Cancel Mode and the resize presets are
        // palette commands). Only the shell knows which; the surface is
        // built by a XAML template with no injection point, so the answer
        // is installed here and CanvasAnnouncerCensus pins that it is.
        Canvas.CanvasSurfaceView.ShellOverlayIsOpen = () => OpenModalSurface is not null;
        ObservePalette();
        // #1275 (codex round 4, the owner's option (a)): every modal loop
        // over the shell — the unsaved-changes prompt on close, a folder or
        // file dialog, a WPF ShowDialog — seals the palette for as long as
        // it runs, so nothing it owns publishes or speaks behind the prompt.
        // W7-7 PR 8 (R-10, OD-12): the loop opening is also a modal opening
        // for the window's editor landing (ModalLoopChanged).
        _modalLoops = new ShellModalLoopMonitor((UIElement)Content, ModalLoopChanged);
        // #1275 (codex round 6): while sealed, a mnemonic finds no target in
        // the shell (MainWindow.Seal.cs) — past handled, so a menu item's
        // own answer cannot hide a candidate from the seal.
        AddHandler(
            AccessKeyManager.AccessKeyPressedEvent,
            new AccessKeyPressedEventHandler(Window_AccessKeyPressed),
            handledEventsToo: true);
        ObserveSearch();
        RecentVaultJumpList.Apply(_viewModel.RecentVaults);
        // W7-7 PR 7 (#1252, OD-1, R-9): coming back to the window rescans —
        // no modal in the way, not on the first activation — and the
        // lifecycle coalescer decides the rest.
        _foregroundRescan = new ForegroundRescanRoute(
            _viewModel.RescanAsync,
            () => OpenModalSurface is not null || ComponentDispatcher.IsThreadModal);
        Activated += (_, _) => _ = _foregroundRescan.OnActivated();
        Deactivated += (_, _) => _foregroundRescan.OnDeactivated();
    }

    /// <summary>The shell's modal-loop monitor, for the facts that drive a
    /// real dialog over the shell.</summary>
    internal ShellModalLoopMonitor ModalLoops => _modalLoops;

    internal async Task ActivateFromExternalRequestAsync(string? vaultPath)
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();

        if (!string.IsNullOrWhiteSpace(vaultPath))
        {
            await _viewModel.OpenVaultAsync(vaultPath);
        }
    }

    private Task<string?> PickVaultAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Open vault folder",
            Multiselect = false,
        };
        return Task.FromResult(dialog.ShowDialog(this) == true ? dialog.FolderName : null);
    }

    private Task<bool> ConfirmRemoveMissingRecentAsync(RecentVault recent)
    {
        MessageBoxResult result = MessageBox.Show(
            this,
            $"{recent.DisplayName} is no longer at {recent.Path}.\n\nRemove it from Recent Vaults?",
            "Vault Not Found",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }

    /// <summary>
    /// The unsaved-changes prompt's display boundary: the native message
    /// box, and nothing else (#1298). CI's Windows session is
    /// non-interactive, where a native box never becomes a window a fact
    /// could find and close, so a hosted fact replaces only this call — the
    /// prompt's wiring into the lifecycle and its answer mapping still run.
    /// Production never sets it.
    /// </summary>
    internal Func<Window, string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult, MessageBoxResult> ShowUnsavedClosePrompt { get; set; } =
        MessageBox.Show;

    private VaultCloseDecision ConfirmUnsavedClose()
    {
        MessageBoxResult result = ShowUnsavedClosePrompt(
            this,
            "One or more notes have unsaved changes.\n\n" +
            "Choose Yes to save all changes, No to discard them, or Cancel to keep the vault open.",
            "Close Vault",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);
        return result switch
        {
            MessageBoxResult.Yes => VaultCloseDecision.SaveAll,
            MessageBoxResult.No => VaultCloseDecision.Discard,
            _ => VaultCloseDecision.Cancel,
        };
    }

    private WorkspaceDirtyNavigationDecision ConfirmDirtyNavigation(
        WorkspaceTabViewModel current,
        WorkspaceItemState destination)
    {
        MessageBoxResult result = MessageBox.Show(
            this,
            $"Save changes to {current.Title} before opening {destination.Title}?\n\n" +
            "Choose Yes to save, No to discard these changes, or Cancel to stay on the current tab.",
            "Unsaved Changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);
        return result switch
        {
            MessageBoxResult.Yes => WorkspaceDirtyNavigationDecision.Save,
            MessageBoxResult.No => WorkspaceDirtyNavigationDecision.Discard,
            _ => WorkspaceDirtyNavigationDecision.Cancel,
        };
    }

    private WorkspaceDirtyNavigationDecision ConfirmDirtyClose(WorkspaceTabViewModel tab)
    {
        MessageBoxResult result = MessageBox.Show(
            this,
            $"Save changes to {tab.Title} before closing it?\n\n" +
            "Choose Yes to save, No to discard these changes, or Cancel to keep it open.",
            "Unsaved Changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);
        return result switch
        {
            MessageBoxResult.Yes => WorkspaceDirtyNavigationDecision.Save,
            MessageBoxResult.No => WorkspaceDirtyNavigationDecision.Discard,
            _ => WorkspaceDirtyNavigationDecision.Cancel,
        };
    }

    private bool ConfirmDestructive(string message) => MessageBox.Show(
        this,
        message,
        "Confirm File Operation",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning,
        MessageBoxResult.No) == MessageBoxResult.Yes;

    private Task<IReadOnlyList<string>> PickImportSourcesAsync()
    {
        MessageBoxResult kind = MessageBox.Show(
            this,
            "Choose Yes to import files, No to import folders, or Cancel to stop.",
            "Import Files and Folders",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question,
            MessageBoxResult.Cancel);
        if (kind == MessageBoxResult.Yes)
        {
            var files = new OpenFileDialog
            {
                Title = "Import files",
                Multiselect = true,
                CheckFileExists = true,
            };
            return Task.FromResult<IReadOnlyList<string>>(
                files.ShowDialog(this) == true ? files.FileNames : []);
        }

        if (kind == MessageBoxResult.No)
        {
            var folders = new OpenFolderDialog
            {
                Title = "Import folders",
                Multiselect = true,
            };
            return Task.FromResult<IReadOnlyList<string>>(
                folders.ShowDialog(this) == true ? folders.FolderNames : []);
        }

        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    private static void CopyText(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception exception) when (exception is System.Runtime.InteropServices.ExternalException)
        {
            HostLog.Write(HostDiagnosticEvent.ClipboardCopyFailed, exception);
        }
    }

    private void ViewModel_RecentVaultsChanged(object? sender, EventArgs e)
    {
        RecentVaultJumpList.Apply(_viewModel.RecentVaults);
    }

    private void ViewModel_ReturnedToWelcome(object? sender, EventArgs e)
    {
        _ = Dispatcher.InvokeAsync(() => OpenVaultButton.Focus());
    }

    private void ViewModel_WorkspaceReady(object? sender, EventArgs e)
    {
        // W7-6 (#1240): the window answers F6 for THIS workspace.
        if (_viewModel.Workspace is WorkspaceViewModel workspace)
        {
            workspace.ShellRegionHost = this;
        }

        _ = Dispatcher.InvokeAsync(FocusActiveEditorPane, DispatcherPriority.Loaded);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(VaultLifecycleViewModel.QuickSwitcher))
        {
            ObserveQuickSwitcher(_viewModel.QuickSwitcher);
        }
        else if (eventArgs.PropertyName == nameof(VaultLifecycleViewModel.Workspace))
        {
            ObserveWorkspace(_viewModel.Workspace);
        }
        else if (eventArgs.PropertyName == nameof(VaultLifecycleViewModel.FileSidebar))
        {
            ObserveFileSidebar(_viewModel.FileSidebar);
        }
    }

    private void ObserveFileSidebar(FilesSidebarViewModel? sidebar)
    {
        if (ReferenceEquals(_observedFileSidebar, sidebar))
        {
            return;
        }

        if (_observedFileSidebar is not null)
        {
            _observedFileSidebar.InlineRenameRequested -= ArmSidebarRename;
            _observedFileSidebar.TreeSelectionRestored -=
                FileSidebar_TreeSelectionRestored;
            _observedFileSidebar.PropertyChanged -= FileSidebar_MoveToSheetChanged;
            _observedFileSidebar.PropertyChanged -= ModalSource_PropertyChanged;
            _observedFileSidebar.MoveToOpenAdmission = null;
            _observedFileSidebar.MoveToOwnsModal = null;
        }

        _observedFileSidebar = sidebar;
        if (sidebar is not null)
        {
            // W5-4 F1/F2: a create's hand-off re-arms the F2 rename
            // flow programmatically once the new node is selected.
            sidebar.InlineRenameRequested += ArmSidebarRename;
            // W5-4 (red team, a11y 2): a mutation's publication
            // re-seats selection and asks the window to restore
            // keyboard focus to the tree.
            sidebar.TreeSelectionRestored += FileSidebar_TreeSelectionRestored;
            // W5-4 F4: the Move-To sheet's admission and present/
            // dismiss observation (the template sheets' shape).
            sidebar.MoveToOpenAdmission = TryClearTheWayForMoveTo;
            sidebar.MoveToOwnsModal = () => ReferenceEquals(_observedFileSidebar, sidebar)
                && OpenModalSurface == ModalSurface.MoveTo;
            sidebar.PropertyChanged += FileSidebar_MoveToSheetChanged;
            sidebar.PropertyChanged += ModalSource_PropertyChanged;
        }

        // A vault transition mid-pick never runs the restore (the sheet
        // dies with the discarded sidebar), so the captured token must
        // not leak into the next vault's first pick.
        if (sidebar is null)
        {
            _focusBeforeMoveTo = null;
        }
    }

    /// <summary>Restore keyboard focus to the files tree after a
    /// mutation's publication discarded the focused container (red
    /// team, a11y 2): WPF ejects focus to the WINDOW when the focused
    /// TreeViewItem unloads, leaving every tree-scoped chord dead. A
    /// modal surface or a real claim elsewhere (the editor, a sheet's
    /// own restore target) wins — window-root/null focus is the
    /// stranded state this repairs.</summary>
    private void FileSidebar_TreeSelectionRestored()
    {
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                if (OpenModalSurface is not null)
                {
                    return;
                }

                if (Keyboard.FocusedElement is DependencyObject focused
                    && !ReferenceEquals(focused, this)
                    && !FilesTree.IsKeyboardFocusWithin)
                {
                    return;
                }

                _ = LandOnFilesTree();
            },
            DispatcherPriority.Input);
    }

    /// <summary>The F2 arm, shared by the chord and the create
    /// hand-off: expander open, focus in the name field, stem
    /// selected; focus refusal falls back to the tree.</summary>
    private void ArmSidebarRename()
    {
        // The hand-off arrives at ASYNC tree publication (red team,
        // a11y 3): if a modal surface opened between the create and
        // its publication, grabbing the name field would pull focus
        // out from behind the scrim and kill the sheet's Esc. The
        // surface that owns the moment owns the focus story.
        if (OpenModalSurface is not null)
        {
            return;
        }

        SidebarFileActionsExpander.IsExpanded = true;
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (SidebarMutationNameTextBox.Focus())
            {
                SelectSidebarRenameText();
            }
            else
            {
                _ = LandOnFilesTree();
            }
        }, DispatcherPriority.Input);
    }

    private void ObserveWorkspace(WorkspaceViewModel? workspace)
    {
        if (ReferenceEquals(_observedWorkspace, workspace))
        {
            return;
        }

        if (_observedWorkspace is not null)
        {
            _observedWorkspace.EditorPaneFocusRequested -= Workspace_EditorPaneFocusRequested;
            _observedWorkspace.CanvasNodeLandingRequested -= Workspace_CanvasNodeLandingRequested;
            _observedWorkspace.PropertyChanged -= Workspace_CanvasSheetChanged;
            _observedWorkspace.PropertyChanged -= ModalSource_PropertyChanged;
            UnwireWorkspaceProperties(_observedWorkspace);
            UnwireWorkspaceCitations(_observedWorkspace);
            UnwireWorkspaceBases(_observedWorkspace);
            UnwireWorkspaceTemplates(_observedWorkspace);
            UnwireWorkspaceGraph(_observedWorkspace);
        }

        _observedWorkspace = workspace;
        if (workspace is not null)
        {
            workspace.EditorPaneFocusRequested += Workspace_EditorPaneFocusRequested;
            workspace.CanvasNodeLandingRequested += Workspace_CanvasNodeLandingRequested;
            workspace.PropertyChanged += Workspace_CanvasSheetChanged;
            workspace.PropertyChanged += ModalSource_PropertyChanged;
            // #1275: the workspace's message boxes are owned by the shell,
            // so they disable it — the signal the modal-loop monitor reads —
            // even when the shell is not the active window.
            workspace.HistoryAlert = (title, message) =>
                WorkspaceViewModel.ShowHistoryAlert(this, title, message);
            WireWorkspaceProperties(workspace);
            WireWorkspaceCitations(workspace);
            WireWorkspaceBases(workspace);
            WireWorkspaceTemplates(workspace);
            WireWorkspaceGraph(workspace);
        }
    }

    /// <summary>§G2 TG2-1 (G2-4, IG2-45): the ONE focus-delivery seam for
    /// the canvas sheets — a prompt or picker that just became current
    /// (opened, or swapped in by a staged prompt's Advanced) takes
    /// keyboard focus on its first focusable, after layout, on the
    /// dispatcher: the text field when the sheet has one, else its rows,
    /// else its one button. A sheet that stops being current delivers
    /// nothing; the modal machinery's own focus return stands.</summary>
    private void Workspace_CanvasSheetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not WorkspaceViewModel workspace)
        {
            return;
        }
        if (e.PropertyName == nameof(WorkspaceViewModel.CanvasPromptSheet)
            && workspace.CanvasPromptSheet is not null)
        {
            _ = Dispatcher.InvokeAsync(FocusCanvasPromptSheet, DispatcherPriority.Input);
        }
        else if (e.PropertyName == nameof(WorkspaceViewModel.CanvasCardPickerSheet)
            && workspace.CanvasCardPickerSheet is not null)
        {
            _ = Dispatcher.InvokeAsync(
                () => _ = TryFocus(CanvasCardPickerFilterBox), DispatcherPriority.Input);
        }
    }

    private void FocusCanvasPromptSheet()
    {
        if (_viewModel.Workspace?.CanvasPromptSheet is null)
        {
            return;
        }
        if (CanvasPromptDraftBox.IsVisible)
        {
            if (TryFocus(CanvasPromptDraftBox))
            {
                CanvasPromptDraftBox.SelectAll();
            }
            return;
        }
        // R-5 (#1247): the selected choice's row, never the bare list; a
        // row that cannot be landed yet leaves the keys to the sheet's
        // other stop.
        if (CanvasPromptChoicesList.IsVisible && CanvasPromptChoicesList.Focusable
            && SelectorFocus.FocusFirstOrSelectedItem(CanvasPromptChoicesList))
        {
            return;
        }
        _ = TryFocus(CanvasPromptClearMarksButton);
    }

    /// <summary>The editor's and the boundaries' deferred focus requests,
    /// arbitrated so the LAST one raised lands whatever their priorities
    /// (W6-2 PR B2, IPC-2).</summary>
    private readonly FocusRequestArbiter _focusRequests = new();

    private void Workspace_EditorPaneFocusRequested(
        object? sender,
        WorkspaceGroupViewModel group)
    {
        // W7-7 PR 8 (R-10): a newer editor landing is coming, so the one the
        // window holds (the ring's, or an earlier route's) is withdrawn NOW,
        // before its content can arrive ahead of the queued landing and seat
        // focus where the reader no longer is.
        _ = _editorLandings.Withdraw();
        // OD-12's one entry: the funnel asks, and the queued landing creates
        // the request (canvas and graph included, contract A14's instruction)
        // under the window's slot. A later focus request that supersedes it in
        // the arbiter leaves no request behind to seat the tab later.
        _ = _focusRequests.Post(
            Dispatcher,
            DispatcherPriority.Input,
            () => LandEditorForRoute(group, onLanded: null));
    }

    /// <summary>R-10 (OD-12's one entry): a canvas jump asks to land the editor
    /// on the card the reader chose (the marks list's Enter, IG-39) — in the
    /// turn that closed the sheet, so no input the reader queued behind the
    /// Enter runs first (codex PR 8 round 9). The ask is still checked: it
    /// lands only while its tab is the active tab of the active group, on the
    /// same document — otherwise it is stale and lands nothing, and withdraws
    /// nothing. A live ask supersedes the landing the window holds and goes
    /// through the one entry (which creates nothing under a modal surface),
    /// silently: its outcomes are the landing's own (IG-41).</summary>
    private void Workspace_CanvasNodeLandingRequested(object? sender, CanvasNodeLandingIntent intent)
    {
        if (_viewModel.Workspace is not WorkspaceViewModel workspace
            || !ReferenceEquals(workspace.ActiveGroup.ActiveTab, intent.Tab)
            || !ReferenceEquals(intent.Tab.Canvas, intent.Document))
        {
            return;
        }

        _ = _editorLandings.Withdraw();
        _ = FocusEditorPane(
            workspace.ActiveGroup, onLanded: null, onRefused: static () => { }, forTheRing: false, canvasNode: intent.NodeId);
    }

    private void ObserveQuickSwitcher(QuickSwitcherViewModel? switcher)
    {
        if (ReferenceEquals(_observedQuickSwitcher, switcher))
        {
            return;
        }

        if (_observedQuickSwitcher is not null)
        {
            _observedQuickSwitcher.PropertyChanged -= QuickSwitcher_PropertyChanged;
            _observedQuickSwitcher.PropertyChanged -= ModalSource_PropertyChanged;
            _observedQuickSwitcher.OpenRequested -= QuickSwitcher_OpenRequested;
        }

        _observedQuickSwitcher = switcher;
        if (switcher is not null)
        {
            switcher.PropertyChanged += QuickSwitcher_PropertyChanged;
            switcher.PropertyChanged += ModalSource_PropertyChanged;
            switcher.OpenRequested += QuickSwitcher_OpenRequested;
        }
    }

    private void QuickSwitcher_PropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(QuickSwitcherViewModel.IsOpen)
            && _observedQuickSwitcher?.IsOpen == true)
        {
            // Codex round 4 (#742): the pickers are mutually exclusive.
            // Quick Open paints BELOW the search overlay, so a
            // palette-invoked Quick Open under an open search left focus
            // in a hidden text box — typing edited an invisible query
            // while arrows and Escape operated search. Search opening
            // already dismisses Quick Open (DecideSearchOpen); this is
            // the symmetric half. The pre-SEARCH focus is adopted as the
            // switcher's return target so its eventual restore lands on
            // the element from before either picker, not on the
            // now-collapsed search box.
            if (_viewModel.Search.IsOpen)
            {
                IInputElement? preSearch = ConsumePreSearchFocus();
                // Supersede, not Close (red team after round 11): the
                // scope survives alongside the query, so Ctrl+Shift+F
                // after the switcher restores a tag-scoped overlay as
                // that tag's listing, not as an idle vault-wide field.
                _viewModel.Search.Supersede();
                _focusBeforeSwitcher = preSearch;
            }

            _focusBeforeSwitcher ??= Keyboard.FocusedElement;
            _quickSwitcherCommitted = false;
            _ = Dispatcher.InvokeAsync(() =>
            {
                QuickSwitcherSearchTextBox.Focus();
                QuickSwitcherSearchTextBox.SelectAll();
            }, DispatcherPriority.Input);
        }
    }

    private void QuickSwitcher_OpenRequested(
        object? sender,
        (string Path, WorkspaceOpenTarget Target) request)
    {
        _quickSwitcherCommitted = true;
    }

    private void ViewModel_QuickSwitcherDismissed(object? sender, EventArgs e) =>
        RestoreFocusAfterQuickOpen();

    /// <summary>
    /// Hands Quick Open's captured pre-open focus to a caller that is
    /// closing the switcher on its own initiative (the supersession
    /// paths), clearing it here so the ordinary dismissal restore runs
    /// inert instead of racing the caller — the
    /// <c>ConsumePreSearchFocus</c> twin.
    /// </summary>
    internal IInputElement? ConsumePreSwitcherFocus()
    {
        IInputElement? previous = _focusBeforeSwitcher;
        _focusBeforeSwitcher = null;
        return previous;
    }

    /// <summary>
    /// The Quick Open twin of <c>AdoptPaletteFocusIntoSearch</c>
    /// (codex round 6): a palette-invoked Quick Open captures the
    /// PALETTE's box as its return target — P9 focuses that box at
    /// invoke time — and it is collapsed by the time the switcher
    /// closes. The palette's own pre-open token is the true lineage.
    /// </summary>
    internal void AdoptPaletteFocusIntoQuickOpen(
        IInputElement? paletteFocusBefore,
        Func<DependencyObject, bool> isInsidePaletteOverlay)
    {
        if (_viewModel.QuickSwitcher?.IsOpen == true
            && _focusBeforeSwitcher is DependencyObject captured
            && isInsidePaletteOverlay(captured))
        {
            _focusBeforeSwitcher = paletteFocusBefore;
        }
    }

    /// <summary>
    /// The return token for a PRESENTING sheet (red team after codex
    /// round 11). Captured at presentation time, a picker may still be
    /// open and focused — the deferred continuations land before the
    /// lifecycle's observer closes it — so a raw
    /// <c>Keyboard.FocusedElement</c> is the about-to-collapse overlay
    /// box and the sheet's eventual restore misses into a fallback.
    /// The picker's own captured pre-open token IS the true lineage;
    /// consuming it also leaves the picker's dismissal restore inert.
    /// At most one picker can be open (invariant 6), so at most one
    /// consume returns a token.
    /// </summary>
    internal IInputElement? CapturePreSheetFocus() =>
        ConsumePreSearchFocus()
            ?? ConsumePreSwitcherFocus()
            ?? ConsumePrePaletteFocus()
            ?? Keyboard.FocusedElement;

    /// <summary>
    /// The Quick Open dismissal restore. Red team after codex round
    /// 11: this restore previously lived anonymous inside the
    /// Dismissed handler, where the invariant-4 census could not
    /// discover it, and it carried neither the topmost-search rule nor
    /// any stand-down — a sheet landing over an open Quick Open (the
    /// deferred-presentation window) had focus stolen straight back
    /// out from behind its scrim, leaving the sheet's Escape dead
    /// until a click.
    /// </summary>
    private void RestoreFocusAfterQuickOpen()
    {
        IInputElement? focusBeforeSwitcher = _focusBeforeSwitcher;
        bool committed = _quickSwitcherCommitted;
        _focusBeforeSwitcher = null;
        _quickSwitcherCommitted = false;
        _ = Dispatcher.InvokeAsync(() =>
        {
            // The shared topmost-search rule first (invariant 4) —
            // when Ctrl+Shift+F superseded the switcher, this hands
            // focus straight to the search box instead of flashing the
            // editor. Then the supersession stand-down: any OTHER open
            // surface owns the moment (its own grab is queued or has
            // landed), so restoring here would either steal focus from
            // behind a scrim or flash-and-announce the editor
            // mid-handoff.
            if (TryFocusSearchIfTopmost())
            {
                return;
            }

            if (OpenModalSurface is not null)
            {
                return;
            }

            if (committed)
            {
                if (_viewModel.Workspace is WorkspaceViewModel workspace)
                {
                    LandEditorForRoute(workspace.ActiveGroup, onLanded: null);
                }
            }
            else if (focusBeforeSwitcher is not null && TryFocus(focusBeforeSwitcher))
            {
            }
            else
            {
                FocusActiveEditorPane();
            }
        }, DispatcherPriority.Input);
    }

    private void ViewModel_WorkspaceFocusBoundaryRequested(
        object? sender,
        WorkspaceFocusBoundary boundary)
    {
        _ = _focusRequests.Post(Dispatcher, DispatcherPriority.Normal, () =>
        {
            if (boundary == WorkspaceFocusBoundary.Files)
            {
                _ = LandOnFilesTree();
            }
            else if (_viewModel.Workspace is WorkspaceViewModel workspace
                && workspace.ConnectionsLeafIsActive()
                && ConnectionsLeafSurface.FocusAnchor())
            {
                // W6-2 PR B (rule C, Term 9): with the Connections leaf
                // active the boundary lands on the leaf's anchor — the tree
                // when it has rows, the state element otherwise — so the
                // entry line speaks and the reader is IN the leaf, not on
                // the rail.
            }
            else if (_viewModel.Workspace is WorkspaceViewModel inspected
                && inspected.IsGraphInspectorShown)
            {
                // W6-2 PR E (Term I5; E-D1): with the inspector the shown
                // leaf the boundary lands on the pane's first stop — the
                // name field — so a reader who toggled is IN the pane. The
                // leaf's host becomes visible through a data trigger whose
                // EFFECTIVE visibility lands after the layout pass, so the
                // first stop is asked for after it — through the arbiter, so
                // a later request supersedes it, and re-reading the shown
                // state, since a hide raises no request (IPI-1-1: a stale
                // callback must not move the keys); a pane whose controls are
                // disabled (Term I7) falls through to the rail.
                _ = _focusRequests.Post(Dispatcher, DispatcherPriority.Background, () =>
                    Graph.GraphInspectorView.LandBoundary(
                        _viewModel.Workspace is WorkspaceViewModel current && current.IsGraphInspectorShown,
                        GraphInspectorSurface.FocusFirstStop,
                        () => _ = SelectorFocus.FocusFirstOrSelectedItem(RightPaneLeavesList)));
            }
            else if (boundary == WorkspaceFocusBoundary.RightPaneEdge)
            {
                // Ctrl+Alt+Right at the edge keeps its landing, the rail
                // (W7-6 §6: Ctrl+Alt+Arrow's semantics unchanged) — on the
                // shown leaf's row, never the bare rail, from which Down
                // walked into the menu bar (W7-7 PR 4, #1247, R-5).
                _ = SelectorFocus.FocusFirstOrSelectedItem(RightPaneLeavesList);
            }
            else
            {
                // A leaf reveal (Ctrl+R, Show History) puts the reader IN
                // the leaf — Ctrl+R on the review's filter — or on the
                // rail's row when the leaf has no stop (R-5).
                LandInRightPane();
            }
        });
    }

    /// <summary>The window's tunnelling key route, ahead of every focused
    /// control's own handlers. An unmodified Escape during an import
    /// cancels it unless an owner of the key is in play — an open modal
    /// overlay or sheet, WPF menu mode, the Files filter field with a
    /// filter or tag scope active, or the inline rename box while a row is
    /// being renamed there — and that owner takes the key through its own
    /// route instead (#1272). While the palette is sealed the seal's
    /// admission takes every key first (#1275, <see cref="SealTakes"/>).</summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // #1275 (codex round 6, owner decision): the seal's one admission,
        // ahead of every route below — the palette's own keys, its
        // carve-outs, the sheets, the shell chords — so none of them needs
        // a seal check of its own, and none can act while a command the
        // palette ran is still running or a prompt is up.
        if (SealTakes(e))
        {
            return;
        }

        ModifierKeys modifiers = Keyboard.Modifiers;

        // The palette is modal: while it is open it owns the keyboard, so
        // no shell chord fires underneath it. Placed ahead of Quick Open
        // because the two overlays are mutually exclusive and the palette
        // is the outer surface.
        if (_viewModel.Palette.IsOpen)
        {
            HandleCommandPaletteKey(e, modifiers);
            if (e.Handled)
            {
                return;
            }

            // PD-2: the chord does not toggle, so pressing it while open
            // re-opens. Handled here because the blanket swallow below
            // would otherwise eat it and make PD-2 unreachable.
            if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.P)
            {
                _viewModel.Palette.Open();
                e.Handled = true;
                return;
            }

            // W5-3 (red team, three-way convergence): without this
            // carve-out the blanket swallow below ate Ctrl+Shift+N
            // silently — neither mac's retire-and-open (its registry
            // dispatch retires the palette, AppState.swift:1980-1989)
            // nor a refusal announcement. The admission inside the
            // workspace open takes the DismissPaletteThenOpen arm,
            // which retires the palette with its focus lineage. A sealed
            // palette never gets here (#1275): the seal's admission at the
            // top of this route took the chord.
            if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.N)
            {
                _viewModel.Workspace?.OpenTemplatePicker();
                e.Handled = true;
                return;
            }

            // Swallow anything that would otherwise fire a shell command
            // underneath the open overlay — but NOT plain typing, and NOT
            // the chords that edit the search text. Marking every modified
            // key handled here kills Ctrl+V, Ctrl+A, Ctrl+C and
            // Shift-selection inside the palette's own box, because
            // TextBox reaches those through InputBindings, which WPF runs
            // only for UNHANDLED key events.
            e.Handled = IsUnderlyingShellShortcut(e.Key, modifiers)
                || (modifiers is not ModifierKeys.None
                    && !TextEditingChords.Allows(e.Key, modifiers));
            return;
        }

        // §E TE-11e: the card editor sheet's ONE key. M8 carves the
        // editor OUT of the mode stack - Escape COMMITS here (the
        // sheet's own seam decides commit, no-change, or in-sheet
        // conflict), and the ladder never sees it. Everything else
        // passes through to the draft TextBox untouched. The authoring
        // journey was the first real keyboard on this path: the seam
        // existed since TE-7, but nothing wired the key.
        // §F TF-8: the prompt sheet's two keys. Escape dismisses
        // committing nothing; Enter submits through the shipped verb
        // (an empty connect label SKIPS — the verb normalizes).
        if (_viewModel.Workspace?.CanvasPromptSheet is not null)
        {
            if (e.Key == Key.Escape && modifiers == ModifierKeys.None)
            {
                _viewModel.Workspace.CloseCanvasPrompt();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && modifiers == ModifierKeys.None)
            {
                _viewModel.Workspace.SubmitCanvasPrompt();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete && modifiers == ModifierKeys.None)
            {
                // §G TG-2: Delete unmarks the marks list's active row.
                _viewModel.Workspace.DeleteOnCanvasPrompt();
                e.Handled = true;
            }

            return;
        }

        // §F TF-8: the picker sheet — Enter routes the pick (a
        // refusal keeps the sheet, filter and highlight intact),
        // Escape closes choosing nothing.
        if (_viewModel.Workspace?.CanvasCardPickerSheet is not null)
        {
            if (e.Key == Key.Escape && modifiers == ModifierKeys.None)
            {
                _viewModel.Workspace.CloseCanvasCardPicker();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && modifiers == ModifierKeys.None)
            {
                _viewModel.Workspace.ConfirmCanvasCardPick();
                e.Handled = true;
            }

            return;
        }

        if (_viewModel.Workspace?.CanvasCardEditorSheet is { } cardEditor)
        {
            if (e.Key == Key.Escape && modifiers == ModifierKeys.None)
            {
                if (cardEditor.CommitOnEscape())
                {
                    _viewModel.Workspace.CloseCanvasCardEditor();
                }

                e.Handled = true;
            }

            return;
        }

        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.P)
        {
            // PD-2: Ctrl+Shift+P, the direct map of mac's Shift-Command-P
            // and the Windows convention. Handled here rather than as a
            // KeyBinding because the palette exposes methods, not
            // ICommands (PR-4).
            //
            // Quick Open is dismissed first. The two overlays are siblings
            // in one Grid and the palette is declared later, so it renders
            // ON TOP of an open Quick Open — leaving two IsDialog surfaces
            // and two hit-test scrims live while Quick Open's key handler
            // sits unreachable behind this branch.
            //
            // A sealed palette does not open (contract 28 T1″), and clearing
            // the way first would dismiss Quick Open or Search for an open
            // that is then refused — so the seal's admission at the top of
            // this route takes the chord before this branch (#1275).
            if (TryClearTheWayForThePalette())
            {
                _viewModel.Palette.Open();
            }

            // Handled either way: a refusal must not fall through and let
            // the chord reach the surface underneath.
            e.Handled = true;
            return;
        }

        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.F)
        {
            // W5-2: Ctrl+Shift+F, the direct map of mac's ⇧⌘F
            // (slate.view.toggleSearch). Delivered here rather than as a
            // KeyBinding because the overlay exposes methods, not
            // ICommands — the palette-chord precedent above. TOGGLING,
            // unlike Ctrl+Shift+P: DecideSearchOpen answers Open for an
            // already-open overlay and Toggle closes it. Placed BEFORE
            // the search-open branch below so its selective swallow
            // cannot eat the overlay's own chord, and after the palette
            // branches so an open palette (which paints ABOVE search)
            // keeps the keyboard.
            if (TryClearTheWayForSearch())
            {
                _viewModel.Search.Toggle();
            }

            // Handled either way: a refusal must not fall through and let
            // the chord reach the surface underneath.
            e.Handled = true;
            return;
        }

        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.N)
        {
            // W5-3 (#743): Ctrl+Shift+N, the direct map of mac's ⇧⌘N
            // (slate.file.newFromTemplate). The modal admission runs
            // INSIDE the workspace's open (contract T9 — one gate for
            // chord, menu, and palette row alike), so unlike #1118's
            // sheet chords this can never present beneath a higher
            // surface. Placed before the search-ownership swallow so
            // the chord supersedes an open search overlay the way the
            // palette chord does.
            _viewModel.Workspace?.OpenTemplatePicker();

            // Handled either way: a refusal must not fall through and let
            // the chord reach the surface underneath.
            e.Handled = true;
            return;
        }

        // Codex round 8 (#742): this branch must run BEFORE the
        // search-ownership branch, or the selective swallow marks
        // Ctrl+O handled and the picker handoff is unreachable
        // while search is open.
        if (e.Key == Key.O && modifiers == ModifierKeys.Control && _viewModel.QuickSwitcher is not null)
        {
            // Codex round 5 (#742): this branch was unconditional, so
            // Ctrl+O opened Quick Open BENEATH any sheet — and the
            // picker exclusion then closed a search overlay under the
            // sheet too, leaving only the hidden picker taking keys.
            if (ModalSurfaces.DecideQuickOpenOpen(OpenModalSurface)
                == PaletteOpenDecision.Open)
            {
                _focusBeforeSwitcher ??= Keyboard.FocusedElement;
                _viewModel.QuickSwitcher.Open();
            }

            e.Handled = true;
            return;
        }

        // Ownership, not openness (codex round 1): under the original
        // stacking design a palette-invoked sheet sat above a still-open
        // search overlay, and routing on IsOpen alone let the hidden
        // overlay steal the sheet's Enter and Escape. SD-5 removed
        // persistent stacking — open now implies topmost — so ownership
        // routing is invariant 3's backstop, kept so a future stacking
        // violation degrades to the visible surface.
        if (ModalSurfaces.SearchOwnsKeys(CurrentModalSurfaceState))
        {
            HandleSearchOverlayKey(e, modifiers);
            if (e.Handled)
            {
                return;
            }

            // The palette's selective swallow, verbatim (contract S12):
            // the overlay owns a TextBox, and marking every modified key
            // handled here kills Ctrl+V, Ctrl+A, Ctrl+C and
            // Shift-selection inside it, because TextBox reaches those
            // through InputBindings, which WPF runs only for UNHANDLED
            // key events. Ctrl+Shift+P never reaches this line — the
            // palette-chord branch above runs first, which is how the
            // palette SUPERSEDES an open search overlay (SD-5).
            e.Handled = IsUnderlyingShellShortcut(e.Key, modifiers)
                || (modifiers is not ModifierKeys.None
                    && !TextEditingChords.Allows(e.Key, modifiers));
            return;
        }

        if (_viewModel.QuickSwitcher?.IsOpen == true)
        {
            HandleQuickSwitcherKey(e, modifiers);
            if (e.Handled)
            {
                return;
            }
        }

        if (IsPaneNavigationGesture(e.Key, modifiers))
        {
            if (FilesPaneBorder.IsKeyboardFocusWithin)
            {
                if (e.Key == Key.Right)
                {
                    FocusActiveEditorPane();
                }
                else
                {
                    AnnounceNoPaneInDirection();
                }

                e.Handled = true;
                return;
            }

            // W6-2 PR B (rule C, Term 9): the whole right-pane region — the
            // rail AND every leaf body — is one focus boundary: Left returns
            // to the active editor (the mac's return route), Right posts the
            // shell's terminal line; focus inside a leaf's tree never falls
            // through to the editor-geometry route below.
            if (RightPaneBorder.IsKeyboardFocusWithin)
            {
                if (e.Key == Key.Left)
                {
                    FocusActiveEditorPane();
                }
                else
                {
                    AnnounceNoPaneInDirection();
                }

                e.Handled = true;
                return;
            }

            // TextBox editing owns several arrow-key gestures before a
            // Window-level InputBinding can execute. Route pane navigation
            // from the preview phase so the shortcut remains reliable while
            // an editor has keyboard focus.
            if (WorkspaceRoot.IsKeyboardFocusWithin
                && _viewModel.Workspace is WorkspaceViewModel workspace)
            {
                string axis = e.Key is Key.Left or Key.Right
                    ? "horizontal"
                    : "vertical";
                int direction = e.Key is Key.Left or Key.Up ? -1 : 1;
                workspace.FocusDirectionalPane(axis, direction);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Escape
            && modifiers == ModifierKeys.None
            && _viewModel.FileSidebar?.IsImporting == true
            && !ModalSurfaceOwnsEscape()
            && !MenuModeOwnsEscape(e)
            && !FilterFieldOwnsEscape()
            && !RenameBoxOwnsEscape())
        {
            _viewModel.FileSidebar.CancelImportCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Red team (a11y 4): while a SHEET owns the keyboard, the
        // underlying shell shortcuts must not act behind its scrim —
        // Ctrl+Alt+F moved focus out of the modal (killing its Esc)
        // and Ctrl+1..9 opened files beneath it. The pickers swallow
        // these through their own routes; sheets get the same fence.
        bool sheetOwnsKeys = OpenModalSurface
            is not (null
                or ModalSurface.QuickOpen
                or ModalSurface.SearchOverlay
                or ModalSurface.CommandPalette);

        if (e.Key == Key.F && modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            if (sheetOwnsKeys)
            {
                e.Handled = true;
                return;
            }

            SidebarFilterTextBox.Focus();
            SidebarFilterTextBox.SelectAll();
            e.Handled = true;
            return;
        }

        // W5-4 F10 (FD-1): structural undo/redo are TREE-SCOPED — the
        // editor owns Ctrl+Z/Ctrl+Y everywhere else (mac's
        // undoTargetsStructural focus gate translated). The inline
        // rename field is inside the sidebar expander, not the tree,
        // so IsKeyboardFocusWithin on the TREE already excludes it.
        if (modifiers == ModifierKeys.Control
            && e.Key is Key.Z or Key.Y
            && FilesTree.IsKeyboardFocusWithin
            && _viewModel.FileSidebar is FilesSidebarViewModel undoSidebar)
        {
            if (e.Key == Key.Z)
            {
                undoSidebar.UndoStructural();
            }
            else
            {
                undoSidebar.RedoStructural();
            }

            e.Handled = true;
            return;
        }

        // W5-4 (F6, FD-4): tree-scoped Delete — the selection verb with
        // Finder-parity confirmation staging. Imperative like F2-rename:
        // a KeyBinding would eat Delete inside the editor and every
        // text field.
        if (e.Key == Key.Delete
            && modifiers == ModifierKeys.None
            && FilesTree.IsKeyboardFocusWithin
            && FilesKeysAreOnTheSelection()
            && _viewModel.FileSidebar?.DeleteCommand.CanExecute(null) == true)
        {
            _viewModel.FileSidebar.DeleteCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2
            && FilesTree.IsKeyboardFocusWithin
            && FilesKeysAreOnTheSelection()
            && _viewModel.FileSidebar?.SelectedNode is
            { IsPlaceholder: false, IsGroupHeader: false })
        {
            ArmSidebarRename();
            e.Handled = true;
            return;
        }

        if (modifiers == ModifierKeys.Control && ShortcutNumber(e.Key) is int shortcut)
        {
            if (sheetOwnsKeys)
            {
                e.Handled = true;
                return;
            }

            _viewModel.FileSidebar?.OpenShortcut(shortcut);
            e.Handled = true;
        }
    }

    internal static bool IsPaneNavigationGesture(Key key, ModifierKeys modifiers) =>
        modifiers == (ModifierKeys.Control | ModifierKeys.Alt)
        && key is Key.Left or Key.Right or Key.Up or Key.Down;

    private void SidebarMutationNameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            SidebarMutationNameTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            // W5-4 F3: focus leaves the field only on SUCCESS — a
            // failed rename keeps the field open with the reason in
            // Status, so the user corrects in place instead of
            // re-arming the whole flow.
            if (_viewModel.FileSidebar?.RenameCommand.CanExecute(null) == true
                && _viewModel.FileSidebar.TryRenameSelected())
            {
                _ = LandOnFilesTree();
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (_viewModel.FileSidebar?.SelectedNode is FileTreeNodeViewModel selected)
            {
                _viewModel.FileSidebar.MutationName = selected.Name;
            }

            _ = LandOnFilesTree();
            e.Handled = true;
        }
    }

    private void SelectSidebarRenameText()
    {
        string text = SidebarMutationNameTextBox.Text;
        bool isFile = _viewModel.FileSidebar?.SelectedNode is { IsDirectory: false };
        int extension = isFile ? text.LastIndexOf('.') : -1;
        SidebarMutationNameTextBox.Select(0, extension > 0 ? extension : text.Length);
    }

    /// <summary>W7-7 PR 4 (#1247, the owner's focus-without-select): the
    /// tree's selection verbs — Delete, F2 — act on the row the reader is
    /// on. A landing may leave the keys on a first row that is NOT the
    /// sidebar's selection (the selection hidden under a collapsed folder);
    /// there the verbs leave the key alone rather than act on a file the
    /// reader cannot hear.</summary>
    private bool FilesKeysAreOnTheSelection() =>
        Keyboard.FocusedElement is not TreeViewItem { DataContext: var node }
        || ReferenceEquals(node, _viewModel.FileSidebar?.SelectedNode);

    internal bool TryFocus(IInputElement target)
    {
        // Red team after codex round 11: the window root is a visible,
        // enabled UIElement, so a token captured while focus sat
        // "nowhere" restored to the ROOT and bypassed the editor
        // fallback. Both claim probes already treat the root as
        // focus-nowhere; the restore side now agrees.
        if (ReferenceEquals(target, this))
        {
            return false;
        }

        return target switch
        {
            // R-5 (#1247; codex round 4): a token captured while a list was
            // empty restores onto its ROW once it has filled, never the bare
            // list — the one landing for element-typed targets.
            UIElement element when element.IsVisible && element.IsEnabled => SelectorFocus.LandOnStop(element),
            ContentElement element when element.IsEnabled => element.Focus(),
            _ => false,
        };
    }

    private void HandleQuickSwitcherKey(KeyEventArgs e, ModifierKeys modifiers)
    {
        QuickSwitcherViewModel switcher = _viewModel.QuickSwitcher!;
        if (e.Key == Key.Escape && modifiers == ModifierKeys.None)
        {
            switcher.Dismiss();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down && modifiers == ModifierKeys.None)
        {
            switcher.MoveSelection(1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up && modifiers == ModifierKeys.None)
        {
            switcher.MoveSelection(-1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && Keyboard.FocusedElement is not Button)
        {
            WorkspaceOpenTarget target = modifiers switch
            {
                ModifierKeys.Control => WorkspaceOpenTarget.NewTab,
                ModifierKeys.Control | ModifierKeys.Alt => WorkspaceOpenTarget.SplitRight,
                ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift => WorkspaceOpenTarget.SplitDown,
                _ => WorkspaceOpenTarget.CurrentTab,
            };
            switcher.OpenSelected(target);
            e.Handled = true;
            return;
        }

        if (IsUnderlyingShellShortcut(e.Key, modifiers))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Test seam for the shell-chord deny-list.
    /// </summary>
    /// <remarks>
    /// Exposed because the AltGr arm's safety depends on this list
    /// answering every Ctrl+Alt chord the shell delivers, and that
    /// dependency was previously only a comment.
    /// </remarks>
    internal static bool IsUnderlyingShellShortcutForTests(Key key, ModifierKeys modifiers) =>
        IsUnderlyingShellShortcut(key, modifiers);

    private static bool IsUnderlyingShellShortcut(Key key, ModifierKeys modifiers)
    {
        if (key == Key.F2 || (modifiers == ModifierKeys.Control && ShortcutNumber(key) is not null))
        {
            return true;
        }

        return (key, modifiers) switch
        {
            (Key.O, ModifierKeys.Control or ModifierKeys.Control | ModifierKeys.Shift) => true,
            (Key.S or Key.W or Key.T, ModifierKeys.Control) => true,
            (Key.T, ModifierKeys.Control | ModifierKeys.Shift) => true,
            (Key.Oem5, ModifierKeys.Control or ModifierKeys.Control | ModifierKeys.Alt) => true,
            (Key.OemOpenBrackets or Key.OemCloseBrackets,
                ModifierKeys.Control | ModifierKeys.Shift or ModifierKeys.Control | ModifierKeys.Alt) => true,
            (Key.Left or Key.Right or Key.Up or Key.Down,
                ModifierKeys.Control | ModifierKeys.Alt or ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift) => true,
            (Key.OemPlus or Key.OemMinus or Key.I or Key.F,
                ModifierKeys.Control | ModifierKeys.Alt) => true,
            // #1118: the W4 chords the list never tracked — under Quick
            // Open they fell through to the Window KeyBindings and fired
            // beneath the picker (the palette and search swallow them by
            // the text-editing allow-list; Quick Open relies on this
            // list alone). Ctrl+J/Ctrl+R jump and open leaves,
            // Ctrl+Shift+J/R open sheets, Ctrl+Shift+E toggles reading.
            (Key.J or Key.R, ModifierKeys.Control or ModifierKeys.Control | ModifierKeys.Shift) => true,
            (Key.E, ModifierKeys.Control | ModifierKeys.Shift) => true,
            // W7-6 (#1240): F6 / Shift+F6 cycle the shell regions, and
            // spec §4 makes them a no-op under any modal surface — so the
            // sheet layer suppresses them on the way down.
            (Key.F6, ModifierKeys.None or ModifierKeys.Shift) => true,
            _ => false,
        };
    }

    private void AnnounceNoPaneInDirection()
    {
        // W0.5-3 residue: Windows shell terminal focus-boundary copy.
        _announcer.Post(new A11yEvent.HostComposed(
            "No pane in that direction.",
            A11yPriority.Medium));
    }

    private static int? ShortcutNumber(Key key) => key switch
    {
        Key.D1 or Key.NumPad1 => 1,
        Key.D2 or Key.NumPad2 => 2,
        Key.D3 or Key.NumPad3 => 3,
        Key.D4 or Key.NumPad4 => 4,
        Key.D5 or Key.NumPad5 => 5,
        Key.D6 or Key.NumPad6 => 6,
        Key.D7 or Key.NumPad7 => 7,
        Key.D8 or Key.NumPad8 => 8,
        Key.D9 or Key.NumPad9 => 9,
        _ => null,
    };

    private void FilesTree_SelectedItemChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        // Non-null transitions only (red team, a11y 2d): a mutation's
        // tree refresh discards the selected container and this event
        // fires with null — propagating it wiped the view model's
        // selection and left every selection-gated verb (F2, Delete,
        // Ctrl+Z) dead until the user re-selected by hand. A TreeView
        // offers no user gesture that deselects, so null here is only
        // ever the refresh artifact.
        if (_viewModel.FileSidebar is not null
            && e.NewValue is FileTreeNodeViewModel node)
        {
            _viewModel.FileSidebar.SelectedNode = node;
        }
    }

    private void FilterResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: FileTreeNodeViewModel node }
            && _viewModel.FileSidebar is not null)
        {
            _viewModel.FileSidebar.SelectedNode = node;
        }
    }

    private void DualPaneFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: FileTreeNodeViewModel node }
            && _viewModel.FileSidebar is not null)
        {
            _viewModel.FileSidebar.SelectedNode = node;
        }
    }

    /// <summary>W7-7 (R-2, OD-2): the Files region's explicit gestures,
    /// delivered by the rows' own lists — the Files tree, the filter
    /// results and the dual pane — while one of their rows has focus
    /// (a rename field, outside the lists, never sees them). Arrow
    /// selection only shows the note and keeps focus on the row; Enter
    /// opens the row and moves focus into the note, Ctrl+Enter opens it in
    /// a new tab, and Space on a tree row toggles its batch check box,
    /// which left the arrow order. These are the
    /// <c>windows.filesTree.*</c> chord rows (<c>ChordScope.FilesTree</c>),
    /// and the tree's HelpText speaks them (contract 39 N-1). A row with
    /// nothing to open leaves Enter unhandled. Space is consumed on every
    /// tree row: a row without a check box — a placeholder, a group
    /// header — changes nothing, and the key goes no further (spec 3.2
    /// item 2, codex PR 2 round 3).</summary>
    private void FilesRows_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel.FileSidebar is not FilesSidebarViewModel sidebar
            || e.OriginalSource is not FrameworkElement { DataContext: FileTreeNodeViewModel row } source
            || source is not (TreeViewItem or ListBoxItem))
        {
            return;
        }

        ModifierKeys modifiers = Keyboard.Modifiers;
        if (e.Key == Key.Enter && modifiers == ModifierKeys.None)
        {
            e.Handled = sidebar.OpenNode(row, WorkspaceOpenTarget.CurrentTab);
        }
        else if (e.Key == Key.Enter && modifiers == ModifierKeys.Control)
        {
            e.Handled = sidebar.OpenNode(row, WorkspaceOpenTarget.NewTab);
        }
        else if (e.Key == Key.Space && modifiers == ModifierKeys.None && source is TreeViewItem)
        {
            _ = sidebar.ToggleBatchSelection(row);
            e.Handled = true;
        }
    }

    /// <summary>W7-7 (R-3, codex PR 2 round 4): Escape in the Files filter
    /// field is a user clear — the Clear filter button's and the palette's
    /// Clear Sidebar Filter — while a filter or tag scope is active; with
    /// nothing filtering the key is left unhandled for the window's own
    /// Escape. The <c>windows.sidebarFilter.clear</c> chord row
    /// (<c>ChordScope.SidebarFilter</c>); the field's HelpText speaks it
    /// (contract 39 N-1).</summary>
    private void SidebarFilterTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        ModifierKeys modifiers = Keyboard.Modifiers;
        if (e.Key == Key.Escape && modifiers == ModifierKeys.None
            && _viewModel.FileSidebar is FilesSidebarViewModel { IsFilterActive: true } sidebar)
        {
            sidebar.ClearFilterCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>#1272: the focused Files filter field owns an unmodified
    /// Escape while a filter or tag scope is active — its clear route
    /// (<see cref="SidebarFilterTextBox_PreviewKeyDown"/>) runs ahead of the
    /// window's import cancellation.</summary>
    private bool FilterFieldOwnsEscape() =>
        SidebarFilterTextBox.IsKeyboardFocusWithin
        && _viewModel.FileSidebar is FilesSidebarViewModel { IsFilterActive: true };

    /// <summary>#1272: the inline rename box owns an unmodified Escape while
    /// a row is being renamed in it — keyboard focus in the box, with a row
    /// F2 can rename selected (F2's own gate) — so its rename cancel
    /// (<see cref="SidebarMutationNameTextBox_PreviewKeyDown"/>) runs ahead
    /// of the window's import cancellation. With no such row the box only
    /// names a new item, and once the rename has ended focus is back in the
    /// tree: either way the import keeps the key.</summary>
    private bool RenameBoxOwnsEscape() =>
        SidebarMutationNameTextBox.IsKeyboardFocusWithin
        && _viewModel.FileSidebar?.SelectedNode is { IsPlaceholder: false, IsGroupHeader: false };

    /// <summary>#1272 (codex round 2 on the follow-up): an open modal
    /// surface owns an unmodified Escape — the overlays (palette, search,
    /// Quick Open, the canvas sheets) take it earlier in
    /// <see cref="Window_PreviewKeyDown"/>, and every other sheet (the
    /// template picker and flow, Move To, the property and citation
    /// sheets…) dismisses through its own route (contract 30 T3/TR-7), so
    /// the import never cancels from behind a scrim.</summary>
    private bool ModalSurfaceOwnsEscape() => OpenModalSurface is not null;

    /// <summary>#1272 (codex round 2 on the follow-up): WPF menu mode owns an
    /// unmodified Escape — the key comes from an item of the menu bar or one
    /// of its open submenus, the menu routes that traverse this window
    /// handler, and the menu's own handling closes the open menu or leaves
    /// menu mode (W7-6 §4) instead of the import being cancelled underneath
    /// it. A context menu needs no yield here: its keys stay inside its
    /// Popup's route and never reach this handler, and WPF dismisses it
    /// natively.</summary>
    private static bool MenuModeOwnsEscape(KeyEventArgs e)
    {
        for (DependencyObject? node = e.OriginalSource as DependencyObject; node is not null; node = ParentOf(node))
        {
            if (node is System.Windows.Controls.Primitives.MenuBase or MenuItem)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>W7-7 (R-3, spec review round 21): the Clear filter button
    /// disables itself once the filter is cleared, so its own invocation
    /// hands the keys to the filter field — focus never stays on a disabled
    /// control. Click is raised before the button's command runs, so the
    /// field already has focus when the button turns disabled.</summary>
    private void SidebarFilterClear_Click(object sender, RoutedEventArgs e) =>
        _ = SidebarFilterTextBox.Focus();

    private void Tags_SelectedItemChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is SidebarTagViewModel tag)
        {
            _viewModel.FileSidebar?.ActivateTag(tag);
        }
    }

    private void QuickSwitcherSearch_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        QuickSwitcherViewModel? switcher = _viewModel.QuickSwitcher;
        if (switcher is null)
        {
            return;
        }

        if (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.None)
        {
            switcher.MoveSelection(1);
            e.Handled = true;
        }
        else if (e.Key == Key.Up && Keyboard.Modifiers == ModifierKeys.None)
        {
            switcher.MoveSelection(-1);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            switcher.Dismiss();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            WorkspaceOpenTarget target = Keyboard.Modifiers switch
            {
                ModifierKeys.Control => WorkspaceOpenTarget.NewTab,
                ModifierKeys.Control | ModifierKeys.Alt => WorkspaceOpenTarget.SplitRight,
                ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift => WorkspaceOpenTarget.SplitDown,
                ModifierKeys.None => WorkspaceOpenTarget.CurrentTab,
                _ => WorkspaceOpenTarget.CurrentTab,
            };
            switcher.OpenSelected(target);
            e.Handled = true;
        }
    }

    private void QuickSwitcherResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Only a double-click on a RESULT opens (codex PR 4's final check,
        // the audit of every double-click over a list): one on the empty
        // area below the results opened the current result, never clicked.
        // A pressed result is the current one by then.
        if (sender is ItemsControl results && SelectorFocus.ClickedItem(results, e.OriginalSource) is null)
        {
            return;
        }

        _viewModel.QuickSwitcher?.OpenSelected(WorkspaceOpenTarget.CurrentTab);
        e.Handled = true;
    }

    // ---- W4-2 link/structure leaf activation (#734). Double-click or
    // Enter opens; the Ctrl variant opens a new tab (mac's Cmd-click,
    // spelled as the Quick Open convention); the context menu adds the
    // split target (mac #457). Outline activation scrolls the current
    // note, so it has no open-target variants.

    private RightPanePanelsViewModel? PanelsViewModel => _viewModel.Workspace?.Panels;

    private static WorkspaceOpenTarget PanelModifierTarget() =>
        (Keyboard.Modifiers & ModifierKeys.Control) != 0
            ? WorkspaceOpenTarget.NewTab
            : WorkspaceOpenTarget.CurrentTab;

    private void PanelBacklinks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Activate the CLICKED row only — a double-click on empty
        // chrome must not open whatever happened to be selected
        // (adversarial round 8; same contract as the context menus).
        if (PanelRowTargeting.TargetRowAt(
                PanelBacklinksList, e.OriginalSource, pointerRequest: true)
            && PanelBacklinksList.SelectedItem is BacklinkRowViewModel row)
        {
            PanelsViewModel?.OpenBacklink(row, PanelModifierTarget());
            e.Handled = true;
        }
    }

    // W7-7 PR 4 (#1247; codex round 7 finding 2): every key below acts on
    // the row that holds the keys — a landing focuses a row without
    // selecting it — and on the selection only when no row does
    // (SelectorFocus.FocusedOrSelectedItem, FocusedRowCensus).
    private void PanelBacklinks_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter
            && SelectorFocus.FocusedOrSelectedItem(PanelBacklinksList) is BacklinkRowViewModel row)
        {
            PanelsViewModel?.OpenBacklink(row, PanelModifierTarget());
            e.Handled = true;
        }
    }

    private void PanelBacklinks_ContextMenuOpening(
        object sender, ContextMenuEventArgs e) =>
        PanelList_ContextMenuOpening(PanelBacklinksList, e);

    private void PanelOutgoingLinks_ContextMenuOpening(
        object sender, ContextMenuEventArgs e)
    {
        PanelList_ContextMenuOpening(PanelOutgoingLinksList, e);
        if (e.Handled)
        {
            return;
        }
        // Row targeted — now gate the items to what this row can
        // actually honor (round 3: external rows advertised tab and
        // split actions that launch the browser regardless).
        if (SelectorFocus.FocusedOrSelectedItem(PanelOutgoingLinksList)
                is not OutgoingLinkRowViewModel row
            || !PanelRowTargeting.ComposeOutgoingMenu(
                PanelOutgoingLinksList.ContextMenu, row))
        {
            e.Handled = true;
        }
    }

    private static void PanelList_ContextMenuOpening(
        ListBox list, ContextMenuEventArgs e)
    {
        // Pointer-invoked menus act on the CLICKED row; keyboard
        // requests (cursor coordinates -1) keep the selection but
        // refuse to open over nothing (adversarial round 2).
        bool pointerRequest = e.CursorLeft >= 0 || e.CursorTop >= 0;
        if (!PanelRowTargeting.TargetRowAt(
            list, e.OriginalSource, pointerRequest))
        {
            e.Handled = true;
        }
    }

    private void PanelBacklinksOpen_Click(object sender, RoutedEventArgs e) =>
        OpenSelectedBacklink(WorkspaceOpenTarget.CurrentTab);

    private void PanelBacklinksOpenNewTab_Click(object sender, RoutedEventArgs e) =>
        OpenSelectedBacklink(WorkspaceOpenTarget.NewTab);

    private void PanelBacklinksOpenSplit_Click(object sender, RoutedEventArgs e) =>
        OpenSelectedBacklink(WorkspaceOpenTarget.SplitRight);

    private void OpenSelectedBacklink(WorkspaceOpenTarget target)
    {
        if (PanelBacklinksList.SelectedItem is BacklinkRowViewModel row)
        {
            PanelsViewModel?.OpenBacklink(row, target);
        }
    }

    private void PanelOutgoingLinks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PanelRowTargeting.TargetRowAt(
                PanelOutgoingLinksList, e.OriginalSource, pointerRequest: true)
            && PanelOutgoingLinksList.SelectedItem is OutgoingLinkRowViewModel row)
        {
            PanelsViewModel?.OpenOutgoingLink(row, PanelModifierTarget());
            e.Handled = true;
        }
    }

    private void PanelOutgoingLinks_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter
            && SelectorFocus.FocusedOrSelectedItem(PanelOutgoingLinksList) is OutgoingLinkRowViewModel row)
        {
            PanelsViewModel?.OpenOutgoingLink(row, PanelModifierTarget());
            e.Handled = true;
        }
    }

    private void PanelOutgoingLinksOpen_Click(object sender, RoutedEventArgs e) =>
        OpenSelectedOutgoingLink(WorkspaceOpenTarget.CurrentTab);

    private void PanelOutgoingLinksOpenNewTab_Click(object sender, RoutedEventArgs e) =>
        OpenSelectedOutgoingLink(WorkspaceOpenTarget.NewTab);

    private void PanelOutgoingLinksOpenSplit_Click(object sender, RoutedEventArgs e) =>
        OpenSelectedOutgoingLink(WorkspaceOpenTarget.SplitRight);

    private void PanelOutgoingLinksOpenBrowser_Click(object sender, RoutedEventArgs e) =>
        OpenSelectedOutgoingLink(WorkspaceOpenTarget.CurrentTab);

    private void OpenSelectedOutgoingLink(WorkspaceOpenTarget target)
    {
        if (PanelOutgoingLinksList.SelectedItem is OutgoingLinkRowViewModel row)
        {
            PanelsViewModel?.OpenOutgoingLink(row, target);
        }
    }

    private void PanelOutline_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PanelRowTargeting.TargetRowAt(
                PanelOutlineList, e.OriginalSource, pointerRequest: true)
            && PanelOutlineList.SelectedItem is OutlineRowViewModel row)
        {
            PanelsViewModel?.OpenHeading(row);
            e.Handled = true;
        }
    }

    private void PanelOutline_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter
            && SelectorFocus.FocusedOrSelectedItem(PanelOutlineList) is OutlineRowViewModel row)
        {
            PanelsViewModel?.OpenHeading(row);
            e.Handled = true;
        }
    }

    // ---- W4-3: tasks + tasks-review leaf handlers ----

    private TasksReviewViewModel? TasksReviewViewModel =>
        _viewModel.Workspace?.TasksReview;

    /// <summary>The clicked-row contract (W4-2 rounds 5-8) plus the
    /// task keyboard model: Enter activates (scroll/open), Space
    /// toggles the selected row — the reading-surface convention.</summary>
    private void PanelTasks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox list
            && PanelRowTargeting.TargetRowAt(list, e.OriginalSource, pointerRequest: true)
            && list.SelectedItem is NoteTaskRowViewModel row)
        {
            PanelsViewModel?.OpenTask(row);
            e.Handled = true;
        }
    }

    private void PanelTasks_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not ListBox list
            || SelectorFocus.FocusedOrSelectedItem(list) is not NoteTaskRowViewModel row)
        {
            return;
        }
        if (e.Key == Key.Enter)
        {
            PanelsViewModel?.OpenTask(row);
            e.Handled = true;
        }
        else if (e.Key == Key.Space)
        {
            PanelsViewModel?.ToggleTask(row);
            e.Handled = true;
        }
    }

    private void PanelTaskCheckbox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox box)
        {
            return;
        }
        // Revert WPF's optimistic flip: the row re-renders from the
        // refreshed task data once the toggle lands (the
        // reading-surface checkbox convention).
        box.IsChecked = box.IsChecked != true;
        if (box.DataContext is NoteTaskRowViewModel row)
        {
            PanelsViewModel?.ToggleTask(row);
        }
        e.Handled = true;
    }

    private void PanelReview_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PanelRowTargeting.TargetRowAt(
                PanelReviewList, e.OriginalSource, pointerRequest: true)
            && PanelReviewList.SelectedItem is ReviewTaskRowViewModel row)
        {
            TasksReviewViewModel?.OpenRow(row);
            e.Handled = true;
        }
    }

    private void PanelReview_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (SelectorFocus.FocusedOrSelectedItem(PanelReviewList) is not ReviewTaskRowViewModel row)
        {
            return;
        }
        if (e.Key == Key.Enter)
        {
            TasksReviewViewModel?.OpenRow(row);
            e.Handled = true;
        }
        else if (e.Key == Key.Space)
        {
            TasksReviewViewModel?.ToggleTask(row);
            e.Handled = true;
        }
    }

    private void PanelReviewCheckbox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox box)
        {
            return;
        }
        box.IsChecked = box.IsChecked != true;
        if (box.DataContext is ReviewTaskRowViewModel row)
        {
            TasksReviewViewModel?.ToggleTask(row);
        }
        e.Handled = true;
    }

    private void PanelReviewLoadMore_Click(object sender, RoutedEventArgs e) =>
        TasksReviewViewModel?.LoadMore();

    private void PanelEmbedView_Loaded(object sender, RoutedEventArgs e)
    {
        // The shared card renderer's Jump seam for hosts without an
        // editor coordinator: route to the panel navigation (which
        // announces "Opened embed source" — the mac verb).
        ((EditorEmbedPreviewView)sender).JumpToSource =
            path => PanelsViewModel?.OpenEmbedSource(path);
    }

    private void WorkspaceContent_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_viewModel.Workspace is not WorkspaceViewModel workspace
            || e.OriginalSource is not DependencyObject focused)
        {
            return;
        }

        WorkspaceGroupViewModel? group = FindAncestorDataContext<WorkspaceGroupViewModel>(focused);
        if (group is not null)
        {
            workspace.SelectGroupFromKeyboardFocus(group);
        }
    }

    private void FocusFilter_Click(object sender, RoutedEventArgs e)
    {
        SidebarFilterTextBox.Focus();
        SidebarFilterTextBox.SelectAll();
    }

    private void QuickOpen_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.QuickSwitcher is not { IsOpen: false } switcher)
        {
            return;
        }

        _focusBeforeSwitcher ??= Keyboard.FocusedElement;
        switcher.Open();
        e.Handled = true;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _windowPlacement.Restore();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_viewModel.PrepareForApplicationClose())
        {
            e.Cancel = true;
            return;
        }

        _windowPlacement.Save();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _viewModel.RecentVaultsChanged -= ViewModel_RecentVaultsChanged;
        _viewModel.ReturnedToWelcome -= ViewModel_ReturnedToWelcome;
        _viewModel.WorkspaceReady -= ViewModel_WorkspaceReady;
        _viewModel.QuickSwitcherDismissed -= ViewModel_QuickSwitcherDismissed;
        _viewModel.WorkspaceFocusBoundaryRequested -= ViewModel_WorkspaceFocusBoundaryRequested;
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ObserveQuickSwitcher(null);
        ObserveWorkspace(null);
        ObserveFileSidebar(null);
        _editorLandings.Dispose();
        _modalLoops.Dispose();
        _viewModel.Dispose();
    }

    private void FocusActiveEditorPane()
    {
        if (_viewModel.Workspace is not WorkspaceViewModel workspace)
        {
            return;
        }

        // W7-7 PR 8 (R-10): the pane is announced ONCE, when focus is really
        // in the stop — at once, when a held landing seats, or after the
        // fallback a refused one takes — never while its content is arriving.
        LandEditorForRoute(workspace.ActiveGroup, workspace.AnnounceActivePaneFocus);
    }

    /// <summary>
    /// W7-7 PR 8 (#1253, contract R-10): an editor landing a ROUTE asks for —
    /// every caller but the F6 ring: the focus funnel behind every open (Quick
    /// Open's commit and a reading link's navigation among them), the reading
    /// toggle, tab cycling and pane moves; the switcher's committed dismissal;
    /// and the close fallbacks (<see cref="FocusActiveEditorPane"/>). It
    /// supersedes whatever landing the window holds. OD-12: the one entry
    /// creates nothing while a modal surface is open — the modal owns the
    /// keys, and nothing may seat beneath it. Its refusal — now, or later when
    /// a held landing's content fails, is torn down while current or will not
    /// take focus — falls back to the tab strip, else the Files region: its
    /// tree, or its filter field when a filter is active
    /// (<see cref="FallBackFromEditor"/>), guarded to the same tab with no
    /// modal open (<see cref="RouteFallback"/>): focus is never left on the
    /// window root or a closed overlay, and never moved beneath a modal.
    /// <paramref name="onLanded"/> is spoken once focus is in the stop or on
    /// the tab's own item, or — for an EMPTY pane, which has neither — in the
    /// Files region, its tree or its filter field (owner decisions 2026-09-28:
    /// the line tells the reader nothing is open, R-1's fourth launch line; and
    /// 2026-09-29: the filter field is the same Files fallback, and the line is
    /// just as true there). Never from the Files region when the pane has a
    /// tab whose stop was refused, and never when nothing took focus.
    /// </summary>
    private void LandEditorForRoute(WorkspaceGroupViewModel group, Action? onLanded)
    {
        _ = _editorLandings.Withdraw();
        WorkspaceTabViewModel? tab = group.ActiveTab;
        Action refused = RouteFallback(group, tab, onLanded);
        ShellRegionLanding landing = FocusEditorPane(group, onLanded, refused, forTheRing: false);
        if (landing == ShellRegionLanding.Refused)
        {
            refused();
            return;
        }

        if (landing == ShellRegionLanding.Landed)
        {
            onLanded?.Invoke();
        }
    }

    /// <summary>A route's refusal (R-10): only while the reader is still where
    /// the route put them — the same group and tab, no modal over it — the
    /// fallback runs, and <paramref name="onLanded"/> is spoken only when it
    /// took the tab's own item, or when the pane is EMPTY and it landed in the
    /// Files region — its tree, or its filter field when a filter is active
    /// (owner decisions 2026-09-28 and 2026-09-29). A pane with a tab whose
    /// stop was refused says nothing from the Files region (codex PR 8 round
    /// 6): its line would name a tab the reader is not on.</summary>
    private Action RouteFallback(WorkspaceGroupViewModel group, WorkspaceTabViewModel? tab, Action? onLanded) =>
        () =>
        {
            if (IsStillTheLandingTab(group, tab)
                && FallBackFromEditor(group, tab) switch
                {
                    EditorFallback.TabItem => true,
                    EditorFallback.Files => tab is null,
                    _ => false,
                })
            {
                onLanded?.Invoke();
            }
        };

    /// <summary>Whether a refused route landing still owns where the reader
    /// is: <paramref name="group"/> is the active group, <paramref name="tab"/>
    /// its active tab, and no modal holds the keys — neither a modal surface
    /// nor a modal loop over the shell (#1275's monitor).</summary>
    private bool IsStillTheLandingTab(WorkspaceGroupViewModel group, WorkspaceTabViewModel? tab) =>
        IsStillWhereAsked(group, tab) && OpenModalSurface is null && !_modalLoops.IsModalLoopActive;

    /// <summary>
    /// W7-7 PR 8 (#1253, contract R-10; OD-12's one entry): THE editor landing
    /// — the one place an editor landing request is created, for the F6 ring,
    /// every route and a canvas jump (<paramref name="canvasNode"/>). Nothing
    /// is created while a modal holds the keys: a modal surface open, or a
    /// modal loop over the shell — a message box it owns, a common dialog, a
    /// WPF <c>ShowDialog</c> (#1275's monitor). Its callers first let go of
    /// the editor landing the window holds (one at most, whoever asked); it
    /// lands the active tab's stop by kind and answers LANDED (focus is in the
    /// stop now), PENDING (held by the window's slot — a canvas or graph
    /// request owned before it is raised: its content, surface or row
    /// container is still to come; it then ends seated —
    /// <paramref name="onLanded"/> — refused — <paramref name="onRefused"/> —
    /// or cancelled, silently) or REFUSED (the stop cannot be taken; nothing
    /// is held and nothing moved, so the caller goes on: the ring to the next
    /// region, a route to its fallback).
    /// </summary>
    private ShellRegionLanding FocusEditorPane(
        WorkspaceGroupViewModel group, Action? onLanded, Action onRefused, bool forTheRing, string? canvasNode = null)
    {
        if (OpenModalSurface is not null || _modalLoops.IsModalLoopActive)
        {
            return ShellRegionLanding.Refused;
        }
        // W6-1 PR A (contract A14) and W6-2 PR C (rule F, Term F6): a canvas
        // or graph tab's focus belongs to its surface, which seats the row,
        // the board, the banner or the state host through the document's
        // addressed landing — asked FIRST, so no fallback below takes focus
        // back off what the surface seats (<see cref="LandDocument"/> raises
        // the request, under the window's slot).
        if (group.ActiveTab is { } documentTab && (documentTab.IsCanvas || documentTab.IsGraph))
        {
            return LandDocument(group, documentTab, onLanded, onRefused, forTheRing, canvasNode);
        }
        WorkspaceTabViewModel? activeTab = group.ActiveTab;
        if (activeTab is null)
        {
            return ShellRegionLanding.Refused;
        }
        // A reading-mode tab's editor is COLLAPSED (the template swaps the two
        // by visibility), so its stop is the reading surface (record F10). It
        // lands at once on an applied, current projection, and otherwise holds
        // the landing for the content — never focusing the loading
        // placeholder; a reader already inside a surface that is not ready (an
        // in-place navigation swapped the note under them) waits on this tab's
        // item, so the landing is an arrival NVDA speaks.
        if (activeTab.IsReadingMode)
        {
            if (ReadingSurfaceOf(activeTab) is not { IsVisible: true, IsEnabled: true } surface
                || !surface.RequestFocusLanding(
                    onLanded, onRefused, parkWhileUnready: () => FocusActiveTabItem(group)))
            {
                return ShellRegionLanding.Refused;
            }
            if (surface.HeldFocusLanding is { } held)
            {
                // Held by the window's slot (OD-12), after the park: the park is
                // this landing's own move, made before it is held.
                _editorLandings.Hold(new HeldEditorLanding(
                    target: () => surface,
                    isLive: () => ReferenceEquals(surface.HeldFocusLanding, held),
                    withdraw: () => surface.CancelFocusLanding(held),
                    stillWhereAsked: () => IsStillWhereAsked(group, activeTab),
                    scope: LandingScope(group),
                    ringRegion: forTheRing ? ShellRegionKind.Editor : null));
                return ShellRegionLanding.Pending;
            }
            return ShellRegionLanding.Landed;
        }
        SlateTextEditor? editor = FindVisualDescendants<SlateTextEditor>(ContentPaneBorder)
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, activeTab));
        return editor is { IsVisible: true, IsEnabled: true } && editor.FocusInputOwner()
            ? ShellRegionLanding.Landed
            : ShellRegionLanding.Refused;
    }

    /// <summary>
    /// A refused editor landing's fallback for a route: the tab strip's stop
    /// for the tab, else the tab control's selected or first tab, else the
    /// Files region's landing — its tree's row, or its filter field when a
    /// filter is active (W7-7 PR 4, R-5: never a bare container) — answering
    /// which took focus. W7-5 (#1239): an EMPTY tab control refuses focus (its
    /// Focusable follows HasItems, WorkspaceTemplates.xaml), so the last resort
    /// is the Files tree: the owner's launch landing when nothing is open, and
    /// the one region that always has something to say. Landing on the bare
    /// TabControl gave NVDA "Workspace tabs tab control" and let Down arrow
    /// wander into the menu bar. OD-12 (codex round 6's note): a group whose
    /// tab control is not realized still tries the Files tree, and the answer
    /// is what decides whether the route's line is spoken.
    /// </summary>
    private EditorFallback FallBackFromEditor(WorkspaceGroupViewModel group, WorkspaceTabViewModel? activeTab)
    {
        TabControl? tabs = FindVisualDescendants<TabControl>(ContentPaneBorder)
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, group));
        if (tabs is not null)
        {
            tabs.UpdateLayout();
            if (activeTab is not null
                && tabs.ItemContainerGenerator.ContainerFromItem(activeTab) is TabItem selectedTab
                && selectedTab.Focus())
            {
                return EditorFallback.TabItem;
            }

            // W7-7 PR 4 (#1247, R-5): a tab control that HAS tabs lands on
            // one, never the bare control — its selected tab, else its first.
            if (SelectorFocus.FocusFirstOrSelectedItem(tabs))
            {
                return EditorFallback.TabControl;
            }
        }

        // The Files region's landing (W7-7 PR 4, R-5 as the owner amended it):
        // the selected file's row, else the first row UNSELECTED — never the
        // bare tree.
        return LandOnFilesTree() ? EditorFallback.Files : EditorFallback.None;
    }

    /// <summary>Where a refused route landing's fallback put the reader.</summary>
    private enum EditorFallback
    {
        None,
        TabItem,
        TabControl,

        /// <summary>The Files region: its tree, or its filter field when a
        /// filter is active.</summary>
        Files,
    }

    /// <summary>The tab bar's stop for <paramref name="group"/>'s active tab
    /// (R-10: where a reader inside a reading surface waits while the content
    /// replacing what they were reading arrives).</summary>
    private bool FocusActiveTabItem(WorkspaceGroupViewModel group)
    {
        TabControl? tabs = FindVisualDescendants<TabControl>(ContentPaneBorder)
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, group));
        if (tabs is null || group.ActiveTab is not { } activeTab)
        {
            return false;
        }

        tabs.UpdateLayout();
        return tabs.ItemContainerGenerator.ContainerFromItem(activeTab) is TabItem item && item.Focus();
    }

    /// <summary>The reading surface showing <paramref name="tab"/> (R-10):
    /// one per tab control, shared by its tabs, so the DataContext names the
    /// tab it shows now.</summary>
    private Reading.ReadingSurface? ReadingSurfaceOf(WorkspaceTabViewModel tab) =>
        FindVisualDescendants<Reading.ReadingSurface>(ContentPaneBorder)
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, tab));

    internal static T? FindAncestorDataContext<T>(DependencyObject current)
        where T : class
    {
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: T elementMatch })
            {
                return elementMatch;
            }
            if (current is FrameworkContentElement { DataContext: T contentMatch })
            {
                return contentMatch;
            }

            // Keyboard focus can land on a CONTENT element — a Hyperlink
            // inside the reading view's document was the first (found as
            // an unhandled-exception crash on link focus, 2026-07-26:
            // VisualTreeHelper.GetParent throws for non-Visuals). Walk
            // content elements logically until the tree re-enters a
            // Visual, then continue visually.
            current = current switch
            {
                FrameworkElement element =>
                    element.Parent ?? element.TemplatedParent
                        ?? VisualTreeHelper.GetParent(current),
                FrameworkContentElement content =>
                    content.Parent ?? LogicalTreeHelper.GetParent(content),
                System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D =>
                    VisualTreeHelper.GetParent(current),
                _ => LogicalTreeHelper.GetParent(current),
            };
        }

        return null;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
