// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using Microsoft.Win32;
using uniffi.slate_uniffi;

namespace SlateWindows;

public partial class MainWindow : Window
{
    private readonly VaultLifecycleViewModel _viewModel;
    private readonly WindowPlacementManager _windowPlacement;
    private readonly AccessibilityNotificationDispatcher _announcer;
    private IInputElement? _focusBeforeSwitcher;
    private QuickSwitcherViewModel? _observedQuickSwitcher;
    private WorkspaceViewModel? _observedWorkspace;
    private bool _quickSwitcherCommitted;

    public MainWindow()
    {
        InitializeComponent();
        _windowPlacement = new WindowPlacementManager(this);
        _announcer = new AccessibilityNotificationDispatcher(StatusTextBlock);
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
            pickImportSources: PickImportSourcesAsync);
        _viewModel.RecentVaultsChanged += ViewModel_RecentVaultsChanged;
        _viewModel.ReturnedToWelcome += ViewModel_ReturnedToWelcome;
        _viewModel.WorkspaceReady += ViewModel_WorkspaceReady;
        _viewModel.QuickSwitcherDismissed += ViewModel_QuickSwitcherDismissed;
        _viewModel.WorkspaceFocusBoundaryRequested += ViewModel_WorkspaceFocusBoundaryRequested;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        DataContext = _viewModel;
        RecentVaultJumpList.Apply(_viewModel.RecentVaults);
    }

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

    private VaultCloseDecision ConfirmUnsavedClose()
    {
        MessageBoxResult result = MessageBox.Show(
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
        }

        _observedWorkspace = workspace;
        if (workspace is not null)
        {
            workspace.EditorPaneFocusRequested += Workspace_EditorPaneFocusRequested;
        }
    }

    private void Workspace_EditorPaneFocusRequested(
        object? sender,
        WorkspaceGroupViewModel group)
    {
        _ = Dispatcher.InvokeAsync(
            () => FocusEditorPane(group),
            DispatcherPriority.Input);
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
            _observedQuickSwitcher.OpenRequested -= QuickSwitcher_OpenRequested;
        }

        _observedQuickSwitcher = switcher;
        if (switcher is not null)
        {
            switcher.PropertyChanged += QuickSwitcher_PropertyChanged;
            switcher.OpenRequested += QuickSwitcher_OpenRequested;
        }
    }

    private void QuickSwitcher_PropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(QuickSwitcherViewModel.IsOpen)
            && _observedQuickSwitcher?.IsOpen == true)
        {
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

    private void ViewModel_QuickSwitcherDismissed(object? sender, EventArgs e)
    {
        IInputElement? focusBeforeSwitcher = _focusBeforeSwitcher;
        bool committed = _quickSwitcherCommitted;
        _focusBeforeSwitcher = null;
        _quickSwitcherCommitted = false;
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (committed)
            {
                if (_viewModel.Workspace is WorkspaceViewModel workspace)
                {
                    FocusEditorPane(workspace.ActiveGroup);
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
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (boundary == WorkspaceFocusBoundary.Files)
            {
                FilesTree.Focus();
            }
            else
            {
                RightPaneLeavesList.Focus();
            }
        });
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        ModifierKeys modifiers = Keyboard.Modifiers;
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

            if (RightPaneLeavesList.IsKeyboardFocusWithin)
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
            && _viewModel.QuickSwitcher?.IsOpen != true
            && _viewModel.FileSidebar?.IsImporting == true)
        {
            _viewModel.FileSidebar.CancelImportCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.O && modifiers == ModifierKeys.Control && _viewModel.QuickSwitcher is not null)
        {
            _focusBeforeSwitcher ??= Keyboard.FocusedElement;
            _viewModel.QuickSwitcher.Open();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F && modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            SidebarFilterTextBox.Focus();
            SidebarFilterTextBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2
            && FilesTree.IsKeyboardFocusWithin
            && _viewModel.FileSidebar?.SelectedNode is
            { IsPlaceholder: false, IsGroupHeader: false })
        {
            SidebarFileActionsExpander.IsExpanded = true;
            _ = Dispatcher.InvokeAsync(() =>
            {
                if (SidebarMutationNameTextBox.Focus())
                {
                    SelectSidebarRenameText();
                }
                else
                {
                    FilesTree.Focus();
                }
            }, DispatcherPriority.Input);
            e.Handled = true;
            return;
        }

        if (modifiers == ModifierKeys.Control && ShortcutNumber(e.Key) is int shortcut)
        {
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
            ICommand? rename = _viewModel.FileSidebar?.RenameCommand;
            if (rename?.CanExecute(null) == true)
            {
                rename.Execute(null);
                FilesTree.Focus();
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (_viewModel.FileSidebar?.SelectedNode is FileTreeNodeViewModel selected)
            {
                _viewModel.FileSidebar.MutationName = selected.Name;
            }

            FilesTree.Focus();
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

    private static bool TryFocus(IInputElement target)
    {
        return target switch
        {
            UIElement element when element.IsVisible && element.IsEnabled => element.Focus(),
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
        if (_viewModel.FileSidebar is not null)
        {
            _viewModel.FileSidebar.SelectedNode = e.NewValue as FileTreeNodeViewModel;
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
        _viewModel.QuickSwitcher?.OpenSelected(WorkspaceOpenTarget.CurrentTab);
        e.Handled = true;
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
        _viewModel.Dispose();
    }

    private void FocusActiveEditorPane()
    {
        if (_viewModel.Workspace is not WorkspaceViewModel workspace)
        {
            return;
        }

        FocusEditorPane(workspace.ActiveGroup);
        workspace.AnnounceActivePaneFocus();
    }

    private void FocusEditorPane(WorkspaceGroupViewModel group)
    {
        WorkspaceTabViewModel? activeTab = group.ActiveTab;
        SlateTextEditor? editor = FindVisualDescendants<SlateTextEditor>(ContentPaneBorder)
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, activeTab));
        if (editor is { IsVisible: true, IsEnabled: true } && editor.FocusInputOwner())
        {
            return;
        }

        TabControl? tabs = FindVisualDescendants<TabControl>(ContentPaneBorder)
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, group));
        if (tabs is null)
        {
            return;
        }

        tabs.UpdateLayout();
        if (activeTab is not null
            && tabs.ItemContainerGenerator.ContainerFromItem(activeTab) is TabItem selectedTab
            && selectedTab.Focus())
        {
            return;
        }

        tabs.Focus();
    }

    private static T? FindAncestorDataContext<T>(DependencyObject current)
        where T : class
    {
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: T match })
            {
                return match;
            }

            current = current is FrameworkElement element
                ? element.Parent ?? element.TemplatedParent ?? VisualTreeHelper.GetParent(current)
                : VisualTreeHelper.GetParent(current);
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
