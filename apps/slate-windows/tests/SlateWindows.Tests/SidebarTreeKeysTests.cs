// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 2 (#1245, contract R-2, owner decision OD-2): keyboard
/// selection in the Files tree keeps opening the note but never takes
/// focus out of the tree; Enter and Ctrl+Enter are the explicit opens
/// that move it, and Space toggles the row's batch check box. The open
/// facts run the REAL wiring — sidebar event → vault lifecycle →
/// workspace open → the one editor-focus funnel — over a real session,
/// because the defect was a flag that one joint of that chain never
/// carried. The key fact runs the shipped tree and its handler.
/// </summary>
public sealed class SidebarTreeKeysTests : IDisposable
{
    private readonly List<string> _roots = [];
    private readonly object _announceGate = new();
    private readonly List<A11yEvent> _announced = [];

    public void Dispose()
    {
        foreach (string root in _roots)
        {
            DeleteQuietly(root);
        }
    }

    /// <summary>
    /// R-2: a selection-driven open shows the note and asks for no focus
    /// on every arm of the workspace open — a fresh tab in an empty pane,
    /// the in-place replacement of the current tab, an existing tab
    /// re-activated, and a folder's note — while the selection is still
    /// spoken.
    /// </summary>
    [Fact]
    public async Task SelectedNode_OpensWithoutRequestingEditorFocus()
    {
        string root = NewVault("select-no-focus");
        using VaultLifecycleViewModel lifecycle = NewLifecycle(root);
        await lifecycle.OpenVaultAsync(root);
        WorkspaceViewModel workspace = Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace);
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
        await SettleSidebarAsync(lifecycle);
        var focusRequests = new List<string>();
        workspace.EditorPaneFocusRequested += (_, group) => focusRequests.Add(group.ActiveTab?.Path ?? "<no tab>");

        // An empty pane takes its first tab.
        Assert.Null(workspace.ActiveGroup.ActiveTab);
        sidebar.SelectedNode = Node(sidebar, "alpha.md");
        Assert.Equal("alpha.md", workspace.ActiveGroup.ActiveTab?.Path);

        // The current tab is replaced in place.
        sidebar.SelectedNode = Node(sidebar, "beta.md");
        Assert.Equal("beta.md", workspace.ActiveGroup.ActiveTab?.Path);
        Assert.Single(workspace.ActiveGroup.Tabs);

        // A folder with a note shows the note.
        sidebar.SelectedNode = Node(sidebar, "Folder");
        Assert.Equal("Folder/Folder.md", workspace.ActiveGroup.ActiveTab?.Path);
        Assert.Empty(focusRequests);

        // An existing tab is re-activated. Alpha gets a second tab and the
        // first tab is re-activated through explicit opens, which do ask;
        // the selection then re-activates alpha's tab without asking.
        workspace.OpenPath("alpha.md", WorkspaceOpenTarget.NewTab);
        workspace.OpenPath("Folder/Folder.md");
        Assert.Equal(new[] { "alpha.md", "Folder/Folder.md" }, focusRequests);
        focusRequests.Clear();
        sidebar.SelectedNode = Node(sidebar, "alpha.md");
        Assert.Equal("alpha.md", workspace.ActiveGroup.ActiveTab?.Path);
        Assert.Equal(2, workspace.ActiveGroup.Tabs.Count);
        Assert.Empty(focusRequests);

        // The selection announcement stays.
        Assert.Contains(new A11yEvent.RowSelected("alpha.md"), Announced());
        Assert.Contains(new A11yEvent.RowSelected("beta.md"), Announced());
    }

    /// <summary>
    /// R-2: the explicit opens move focus — Enter's verb on the row the
    /// reader is on (the Open button's command), Ctrl+Enter's new tab, a
    /// folder's note — and a row with nothing to open asks for nothing,
    /// so its key falls through.
    /// </summary>
    [Fact]
    public async Task OpenSelected_RequestsEditorFocus()
    {
        string root = NewVault("open-focus");
        using VaultLifecycleViewModel lifecycle = NewLifecycle(root);
        await lifecycle.OpenVaultAsync(root);
        WorkspaceViewModel workspace = Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace);
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
        await SettleSidebarAsync(lifecycle);
        var focusRequests = new List<string>();
        workspace.EditorPaneFocusRequested += (_, group) => focusRequests.Add(group.ActiveTab?.Path ?? "<no tab>");

        sidebar.SelectedNode = Node(sidebar, "alpha.md");
        Assert.Empty(focusRequests);
        sidebar.OpenCurrentCommand.Execute(null);
        Assert.Equal(new[] { "alpha.md" }, focusRequests);

        Assert.True(sidebar.OpenNode(Node(sidebar, "beta.md"), WorkspaceOpenTarget.NewTab));
        Assert.Equal(2, workspace.ActiveGroup.Tabs.Count);
        Assert.Equal(new[] { "alpha.md", "beta.md" }, focusRequests);

        Assert.True(sidebar.OpenNode(Node(sidebar, "Folder"), WorkspaceOpenTarget.CurrentTab));
        Assert.Equal("Folder/Folder.md", workspace.ActiveGroup.ActiveTab?.Path);
        Assert.Equal(new[] { "alpha.md", "beta.md", "Folder/Folder.md" }, focusRequests);

        Assert.False(sidebar.OpenNode(Node(sidebar, "Docs"), WorkspaceOpenTarget.CurrentTab));
        Assert.False(sidebar.OpenNode(FileTreeNodeViewModel.Loading(), WorkspaceOpenTarget.CurrentTab));
        Assert.False(sidebar.OpenNode(null, WorkspaceOpenTarget.NewTab));
        Assert.Equal(3, focusRequests.Count);
    }

    /// <summary>
    /// R-2/OD-2 through the shipped tree: its batch check box is out of
    /// the arrow order; Space on the focused row toggles the row's batch
    /// state, which the row reports as its ItemStatus, and focus stays on
    /// the row; Enter on the same row is the explicit open. Focusing the
    /// row (the arrow keys' selection) sent an open that withholds focus.
    /// </summary>
    [Fact]
    public void Space_TogglesBatchSelectionOnTheFocusedRow() => RunSta(() =>
    {
        using var host = new TreeHost(NewVault("space-toggle"));
        host.Initialize();
        FileTreeNodeViewModel alpha = Node(host.Sidebar, "alpha.md");
        TreeViewItem row = host.FocusRow(alpha);
        Assert.Equal(("alpha.md", WorkspaceOpenTarget.CurrentTab, false), Assert.Single(host.Requests));

        CheckBox box = Assert.Single(Descendants<CheckBox>(row));
        Assert.False(box.Focusable);
        Assert.False(box.IsTabStop);
        Assert.Equal(string.Empty, AutomationProperties.GetItemStatus(row));

        Assert.True(host.Press(row, Key.Space));
        Assert.True(alpha.IsBatchSelected);
        Assert.True(box.IsChecked);
        Assert.Equal("Checked for batch actions", AutomationProperties.GetItemStatus(row));
        Assert.Equal(new A11yEvent.ItemsSelected(1), host.LastAnnouncement);
        Assert.Same(row, Keyboard.FocusedElement);

        Assert.True(host.Press(row, Key.Space));
        Assert.False(alpha.IsBatchSelected);
        Assert.Equal(string.Empty, AutomationProperties.GetItemStatus(row));
        Assert.IsType<A11yEvent.NoItemsSelected>(host.LastAnnouncement);
        Assert.Same(row, Keyboard.FocusedElement);

        Assert.True(host.Press(row, Key.Enter));
        Assert.Equal(("alpha.md", WorkspaceOpenTarget.CurrentTab, true), host.Requests[^1]);
        Assert.Equal(2, host.Requests.Count);

        // A placeholder or a group header has no check box: Space is not
        // this route's to take.
        Assert.False(host.Sidebar.ToggleBatchSelection(FileTreeNodeViewModel.Loading()));
        Assert.False(host.Sidebar.ToggleBatchSelection(FileTreeNodeViewModel.Group("Today", [])));
        Assert.Equal(NavigationHelpText(), AutomationProperties.GetHelpText(host.Tree));
    });

    /// <summary>
    /// R-2 on every Files surface: Enter on a focused row of the tree, the
    /// dual pane and the filter results is the explicit open, asking for
    /// focus, and Space is a tree gesture only — a list row does not take
    /// it. (Ctrl+Enter's real modifier state is driven by the FlaUI
    /// journey on the same three surfaces.)
    /// </summary>
    [Fact]
    public void Enter_OpensTheFocusedRowOnEveryFilesSurface() => RunSta(() =>
    {
        using var host = new TreeHost(NewVault("every-surface"));
        host.Initialize();

        TreeViewItem treeRow = host.FocusRow(Node(host.Sidebar, "alpha.md"));
        host.Requests.Clear();
        Assert.True(host.Press(treeRow, Key.Enter));
        Assert.Equal(("alpha.md", WorkspaceOpenTarget.CurrentTab, true), Assert.Single(host.Requests));

        host.Sidebar.IsDualPaneEnabled = true;
        ListBoxItem paneRow = host.FocusListRow(host.DualPane, "beta.md");
        host.Requests.Clear();
        Assert.False(host.Press(paneRow, Key.Space));
        Assert.True(host.Press(paneRow, Key.Enter));
        Assert.Equal(("beta.md", WorkspaceOpenTarget.CurrentTab, true), Assert.Single(host.Requests));

        host.Sidebar.FilterText = "alpha";
        ListBoxItem resultRow = host.FocusListRow(host.FilterResults, "alpha.md");
        host.Requests.Clear();
        Assert.False(host.Press(resultRow, Key.Space));
        Assert.True(host.Press(resultRow, Key.Enter));
        Assert.Equal(("alpha.md", WorkspaceOpenTarget.CurrentTab, true), Assert.Single(host.Requests));
    });

    /// <summary>
    /// R-2 (design review): the row arm acts on a focused ROW container,
    /// never on a text field: a text box inside a row keeps its Enter and
    /// Space.
    /// </summary>
    [Fact]
    public void TextFieldsKeepTheirKeysWithTheRowArmInstalled() => RunSta(() =>
    {
        using var host = new TreeHost(NewVault("text-fields"));
        host.Initialize();
        FileTreeNodeViewModel alpha = Node(host.Sidebar, "alpha.md");
        TreeViewItem row = host.FocusRow(alpha);
        host.Requests.Clear();

        var inRow = new TextBox();
        // The row template's own grid: the one holding the check box.
        Grid template = Assert.IsType<Grid>(Assert.Single(Descendants<CheckBox>(row)).Parent);
        template.Children.Add(inRow);
        host.Tree.UpdateLayout();
        Assert.True(inRow.Focus());
        Assert.False(host.Press(inRow, Key.Enter));
        Assert.False(host.Press(inRow, Key.Space));
        Assert.Empty(host.Requests);
        Assert.False(alpha.IsBatchSelected);
        template.Children.Remove(inRow);
    });

    /// <summary>
    /// R-2 (spec §3.2 item 2): with the row arm installed, the shipped
    /// inline rename field still cancels on Escape — the name reverts and
    /// nothing is renamed — and commits on Enter; neither asks for the
    /// editor's focus.
    /// </summary>
    [Fact]
    public void InlineRename_EnterCommitsAndEscapeCancelsWithTheArmInstalled() => RunSta(() =>
    {
        string root = NewVault("inline-rename");
        using var host = new TreeHost(root);
        host.Initialize();
        _ = host.FocusRow(Node(host.Sidebar, "alpha.md"));
        host.Requests.Clear();

        TextBox rename = host.RenameField;
        Assert.True(rename.Focus());
        rename.Text = "discarded.md";
        Assert.True(host.Press(rename, Key.Escape));
        Assert.Equal("alpha.md", host.Sidebar.MutationName);

        Assert.True(rename.Focus());
        rename.Text = "gamma.md";
        Assert.True(host.Press(rename, Key.Enter));
        Assert.True(PumpedDispatcher.PumpUntil(() => File.Exists(Path.Combine(root, "gamma.md"))));
        Assert.False(File.Exists(Path.Combine(root, "alpha.md")));
        Assert.False(File.Exists(Path.Combine(root, "discarded.md")));
        Assert.DoesNotContain(host.Requests, request => request.FocusEditor);
    });

    private static string NavigationHelpText() => Commands.NavigationHelp.FilesTree;

    // ---- Helpers --------------------------------------------------------

    private VaultLifecycleViewModel NewLifecycle(string root) =>
        new(
            pickVault: () => Task.FromResult<string?>(root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(root, "device-state", "recent-vaults.json")),
            announce: Record,
            sessionLoadWorker: work => Task.FromResult(work()));

    private void Record(A11yEvent announcement)
    {
        lock (_announceGate)
        {
            _announced.Add(announcement);
        }
    }

    private List<A11yEvent> Announced()
    {
        lock (_announceGate)
        {
            return [.. _announced];
        }
    }

    private static FileTreeNodeViewModel Node(FilesSidebarViewModel sidebar, string path) =>
        Assert.Single(sidebar.RootNodes, node => node.Path == path);

    /// <summary>The W1 close barrier settle: background tree work
    /// publishes the rows the facts select.</summary>
    private static async Task SettleSidebarAsync(VaultLifecycleViewModel lifecycle)
    {
        if (lifecycle.FileSidebar is FilesSidebarViewModel sidebar)
        {
            await sidebar.TreeRefreshCompletion.WaitAsync(TimeSpan.FromSeconds(20));
        }
    }

    /// <summary>Two notes, a folder with its folder note, and a folder
    /// without one.</summary>
    private string NewVault(string label)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"slate-windows-test-tree-keys-{label}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "Folder"));
        Directory.CreateDirectory(Path.Combine(root, "Docs"));
        File.WriteAllText(Path.Combine(root, "alpha.md"), "# Alpha\n\nBody.\n");
        File.WriteAllText(Path.Combine(root, "beta.md"), "# Beta\n\nBody.\n");
        File.WriteAllText(Path.Combine(root, "Folder", "Folder.md"), "# Folder\n");
        File.WriteAllText(Path.Combine(root, "Docs", "readme.md"), "# Readme\n");
        _roots.Add(root);
        return root;
    }

    private static void DeleteQuietly(string root)
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            // A row's own descendants only: a nested row's check box is
            // that row's.
            if (child is not TreeViewItem)
            {
                foreach (T descendant in Descendants<T>(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    /// <summary>The shipped Files surfaces — MainWindow's own tree, filter
    /// results, dual pane and inline rename field, with their templates,
    /// container styles and key handlers — rehosted in a shown window over
    /// a real sidebar (the MoveToFocusTests shape: no Application and no
    /// shown MainWindow, so no app-wide state, Jump Lists or window
    /// placement are touched).</summary>
    private sealed class TreeHost(string root) : IDisposable
    {
        private readonly List<A11yEvent> _announced = [];
        private VaultSession? _session;
        private Window? _window;

        public FilesSidebarViewModel Sidebar { get; private set; } = null!;
        public MainWindow Shell { get; private set; } = null!;
        public VaultLifecycleViewModel Lifecycle { get; private set; } = null!;
        public TreeView Tree { get; private set; } = null!;
        public ListBox FilterResults { get; private set; } = null!;
        public ListBox DualPane { get; private set; } = null!;
        public TextBox RenameField { get; private set; } = null!;
        public List<(string Path, WorkspaceOpenTarget Target, bool FocusEditor)> Requests { get; } = [];
        public A11yEvent LastAnnouncement => _announced[^1];

        public void Initialize()
        {
            Assert.Null(Application.Current);
            _session = VaultSession.OpenFilesystem(root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            Sidebar = new FilesSidebarViewModel(
                _session,
                _announced.Add,
                vaultRoot: root,
                localAppDataRoot: Path.Combine(root, "device-state"));
            PumpedDispatcher.PumpUntilDrained(Sidebar.TreeRefreshCompletion);
            Sidebar.OpenTargetRequested += (_, request) => Requests.Add(request);

            Shell = new MainWindow();
            Lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
            SetPrivateProperty(Lifecycle, nameof(VaultLifecycleViewModel.FileSidebar), Sidebar);
            Tree = Detach<TreeView>(Assert.IsType<TreeView>(Shell.FindName("FilesTree")));
            FilterResults = Detach<ListBox>(Assert.IsType<ListBox>(Shell.FindName("FilterResultsList")));
            RenameField = Detach<TextBox>(Assert.IsType<TextBox>(Shell.FindName("SidebarMutationNameTextBox")));
            DualPane = Detach<ListBox>(Assert.Single(
                LogicalDescendants(Shell).OfType<ListBox>(),
                list => AutomationProperties.GetAutomationId(list) == "SidebarDualPane"));

            var rows = new Grid();
            rows.Children.Add(Tree);
            rows.Children.Add(FilterResults);
            var layout = new DockPanel();
            DockPanel.SetDock(RenameField, Dock.Top);
            DockPanel.SetDock(DualPane, Dock.Bottom);
            layout.Children.Add(RenameField);
            layout.Children.Add(DualPane);
            layout.Children.Add(rows);
            _window = new Window
            {
                Content = layout,
                DataContext = Sidebar,
                Width = 320,
                Height = 480,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            _window.Show();
            _window.Activate();
            _window.UpdateLayout();
            PumpedDispatcher.Drain();
        }

        public TreeViewItem FocusRow(FileTreeNodeViewModel node)
        {
            TreeViewItem? row = null;
            Assert.True(PumpedDispatcher.PumpUntil(() =>
                (row = Tree.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem) is not null));
            Assert.True(row!.Focus());
            PumpedDispatcher.Drain();
            Assert.Same(row, Keyboard.FocusedElement);
            return row;
        }

        /// <summary>Focus a list's row for <paramref name="path"/> once the
        /// list shows it — the dual pane loads on its toggle, the filter
        /// results after the filter's debounce.</summary>
        public ListBoxItem FocusListRow(ListBox list, string path)
        {
            ListBoxItem? row = null;
            Assert.True(PumpedDispatcher.PumpUntil(() =>
            {
                list.UpdateLayout();
                row = list.Items.OfType<FileTreeNodeViewModel>()
                    .Where(node => node.Path == path)
                    .Select(node => list.ItemContainerGenerator.ContainerFromItem(node) as ListBoxItem)
                    .FirstOrDefault(container => container is { IsVisible: true });
                return row is not null;
            }));
            Assert.True(row!.Focus());
            PumpedDispatcher.Drain();
            Assert.Same(row, Keyboard.FocusedElement);
            return row;
        }

        /// <summary>The key's tunnelling route through the real surface:
        /// the event is raised on the focused element, so the list's own
        /// preview handler sees it as that element's key.</summary>
        public bool Press(UIElement target, Key key)
        {
            var args = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(target)!,
                Environment.TickCount,
                key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };
            target.RaiseEvent(args);
            PumpedDispatcher.Drain();
            return args.Handled;
        }

        public void Dispose()
        {
            var failures = new List<Exception>();
            void CleanUp(Action action)
            {
                try { action(); }
                catch (Exception exception) { failures.Add(exception); }
            }

            if (Lifecycle is not null)
            {
                CleanUp(() => SetPrivateProperty(Lifecycle, nameof(VaultLifecycleViewModel.FileSidebar), null));
            }

            if (Sidebar is not null)
            {
                CleanUp(() => PumpedDispatcher.PumpUntilDrained(Sidebar.BeginSessionShutdownAndCaptureWork().SessionWork));
            }

            CleanUp(() => _window?.Close());
            CleanUp(() => Shell?.Close());
            CleanUp(() => _session?.Dispose());
            if (failures.Count > 0)
            {
                throw new AggregateException("Files surfaces fixture cleanup failed.", failures);
            }
        }

        private static T Detach<T>(T element)
            where T : FrameworkElement
        {
            Assert.IsAssignableFrom<Panel>(element.Parent).Children.Remove(element);
            return element;
        }

        private static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject root)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject node)
                {
                    yield return node;
                    foreach (DependencyObject descendant in LogicalDescendants(node))
                    {
                        yield return descendant;
                    }
                }
            }
        }

        private static void SetPrivateProperty(object target, string name, object? value) =>
            (target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Missing state property: {name}"))
            .SetValue(target, value);
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { PumpedDispatcher.Run(body); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "The Files tree fixture timed out.");
        if (failure is not null) { ExceptionDispatchInfo.Capture(failure).Throw(); }
    }
}
