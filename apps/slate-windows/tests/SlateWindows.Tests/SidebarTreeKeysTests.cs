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
    /// R-2 (codex PR 2 round 2): a selection-driven open is heard as the
    /// row's selection, then the Outline panel's once-per-file count of the
    /// note it now shows (mac's announcedFilePath rule — true whatever has
    /// focus). Focus stayed on the row, so no tab took it: neither the first
    /// tab of an empty pane nor an open, inactive tab the selection
    /// re-activates speaks a tab-focus line.
    /// </summary>
    [Fact]
    public async Task SelectionOpens_SpeakNoTabFocus()
    {
        string root = NewVault("select-speech");
        using VaultLifecycleViewModel lifecycle = NewLifecycle(root);
        await lifecycle.OpenVaultAsync(root);
        WorkspaceViewModel workspace = Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace);
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
        await SettleSidebarAsync(lifecycle);

        Assert.Null(workspace.ActiveGroup.ActiveTab);
        int before = Announced().Count;
        sidebar.SelectedNode = Node(sidebar, "alpha.md");
        Assert.Equal("alpha.md", workspace.ActiveGroup.ActiveTab?.Path);
        Assert.Equal(
            [new A11yEvent.RowSelected("alpha.md"), new A11yEvent.OutlineCount(1)],
            Announced().Skip(before));

        // beta opens explicitly in a second tab; a folder without a note
        // moves the selection off alpha and opens nothing.
        workspace.OpenPath("beta.md", WorkspaceOpenTarget.NewTab);
        sidebar.SelectedNode = Node(sidebar, "Docs");
        Assert.Equal("beta.md", workspace.ActiveGroup.ActiveTab?.Path);
        before = Announced().Count;
        sidebar.SelectedNode = Node(sidebar, "alpha.md");
        Assert.Equal("alpha.md", workspace.ActiveGroup.ActiveTab?.Path);
        Assert.Equal(2, workspace.ActiveGroup.Tabs.Count);
        Assert.Equal(
            [new A11yEvent.RowSelected("alpha.md"), new A11yEvent.OutlineCount(1)],
            Announced().Skip(before));
    }

    /// <summary>
    /// R-2 (codex PR 2 round 2): a selection never raises the modal
    /// dirty-navigation gate. With the current note dirty, an arrow in
    /// Files shows the destination in another tab of the group — no
    /// dialog, no editor focus, the edits untouched — and the next arrow
    /// replaces that clean tab in place. An explicit open still asks.
    /// </summary>
    [Fact]
    public async Task SelectionBesideADirtyNote_OpensAnotherTabWithoutTheGate()
    {
        string root = NewVault("select-dirty");
        var gate = new List<string>();
        using VaultLifecycleViewModel lifecycle = NewLifecycle(
            root,
            confirmDirtyNavigation: (tab, _) =>
            {
                gate.Add(tab.Path);
                return WorkspaceDirtyNavigationDecision.Cancel;
            });
        await lifecycle.OpenVaultAsync(root);
        WorkspaceViewModel workspace = Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace);
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
        await SettleSidebarAsync(lifecycle);
        workspace.OpenPath("alpha.md");
        WorkspaceTabViewModel dirty = Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        const string edited = "# Alpha\n\nEdited, unsaved.\n";
        dirty.Text = edited;
        Assert.True(dirty.IsDirty);
        var focusRequests = new List<string>();
        workspace.EditorPaneFocusRequested += (_, group) => focusRequests.Add(group.ActiveTab?.Path ?? "<no tab>");
        int before = Announced().Count;

        sidebar.SelectedNode = Node(sidebar, "beta.md");

        Assert.Empty(gate);
        Assert.Empty(focusRequests);
        Assert.Equal("beta.md", workspace.ActiveGroup.ActiveTab?.Path);
        Assert.Equal(new[] { "alpha.md", "beta.md" }, workspace.ActiveGroup.Tabs.Select(tab => tab.Path));
        Assert.True(dirty.IsDirty);
        Assert.Equal(edited, dirty.Text);
        Assert.Equal(
            [new A11yEvent.RowSelected("beta.md"), new A11yEvent.OutlineCount(1)],
            Announced().Skip(before));

        sidebar.SelectedNode = Node(sidebar, "Folder");
        Assert.Equal(new[] { "alpha.md", "Folder/Folder.md" }, workspace.ActiveGroup.Tabs.Select(tab => tab.Path));
        Assert.Empty(gate);
        Assert.Equal(edited, dirty.Text);

        // An explicit open from the dirty note still asks (and Cancel keeps it).
        workspace.ActiveGroup.ActiveTab = dirty;
        Assert.True(sidebar.OpenNode(Node(sidebar, "beta.md"), WorkspaceOpenTarget.CurrentTab));
        Assert.Equal(new[] { "alpha.md" }, gate);
        Assert.Same(dirty, workspace.ActiveGroup.ActiveTab);
        Assert.Equal(edited, dirty.Text);
    }

    /// <summary>
    /// R-2 transient tab (codex PR 2 round 4), the whole sequence: arrow onto
    /// A, Ctrl+Enter, back to Files, arrow onto B. Ctrl+Enter gives A a tab
    /// of its own — the transient tab that showed it, kept — so B lands in a
    /// new transient tab: A intact and permanent, the tab count one higher.
    /// </summary>
    [Fact]
    public async Task ArrowCtrlEnterArrow_KeepsTheNoteInItsOwnTab()
    {
        (VaultLifecycleViewModel lifecycle, WorkspaceViewModel workspace, FilesSidebarViewModel sidebar) =
            await OpenAsync("transient-ctrl-enter");
        using VaultLifecycleViewModel owned = lifecycle;

        sidebar.SelectedNode = Node(sidebar, "alpha.md");
        WorkspaceTabViewModel alpha = Assert.Single(workspace.ActiveGroup.Tabs);
        Assert.True(alpha.IsTransient);
        int before = workspace.ActiveGroup.Tabs.Count;

        Assert.True(sidebar.OpenNode(Node(sidebar, "alpha.md"), WorkspaceOpenTarget.NewTab));
        Assert.Same(alpha, Assert.Single(workspace.ActiveGroup.Tabs));

        sidebar.SelectedNode = Node(sidebar, "beta.md");

        Assert.Equal(before + 1, workspace.ActiveGroup.Tabs.Count);
        Assert.Equal(new[] { "alpha.md", "beta.md" }, workspace.ActiveGroup.Tabs.Select(tab => tab.Path));
        Assert.Same(alpha, workspace.ActiveGroup.Tabs[0]);
        Assert.Equal("alpha.md", alpha.Path);
        Assert.False(alpha.IsTransient);
        Assert.True(workspace.ActiveGroup.Tabs[1].IsTransient);
        Assert.Equal("beta.md", workspace.ActiveGroup.ActiveTab?.Path);
    }

    /// <summary>
    /// R-2 transient tab: arrow onto A, Enter (focus into the note — the tab
    /// stays transient), back to Files, arrow onto B. A was transient and
    /// clean, so B takes its tab in place: one tab, showing B.
    /// </summary>
    [Fact]
    public async Task ArrowEnterArrow_ReplacesTheCleanTransientTab()
    {
        (VaultLifecycleViewModel lifecycle, WorkspaceViewModel workspace, FilesSidebarViewModel sidebar) =
            await OpenAsync("transient-enter");
        using VaultLifecycleViewModel owned = lifecycle;
        var focusRequests = new List<string>();
        workspace.EditorPaneFocusRequested += (_, group) => focusRequests.Add(group.ActiveTab?.Path ?? "<no tab>");

        sidebar.SelectedNode = Node(sidebar, "alpha.md");
        WorkspaceTabViewModel transient = Assert.Single(workspace.ActiveGroup.Tabs);
        Assert.True(sidebar.OpenNode(Node(sidebar, "alpha.md"), WorkspaceOpenTarget.CurrentTab));
        Assert.Equal(new[] { "alpha.md" }, focusRequests);
        Assert.True(transient.IsTransient);

        sidebar.SelectedNode = Node(sidebar, "beta.md");

        Assert.Same(transient, Assert.Single(workspace.ActiveGroup.Tabs));
        Assert.Equal("beta.md", transient.Path);
        Assert.True(transient.IsTransient);
    }

    /// <summary>
    /// R-2 transient tab: arrow onto A, type in A, back to Files, arrow onto
    /// B. An edited note is never replaced: A keeps its tab and its edits,
    /// no longer transient, and B shows in a new transient tab.
    /// </summary>
    [Fact]
    public async Task ArrowEditArrow_KeepsTheEditedNote()
    {
        (VaultLifecycleViewModel lifecycle, WorkspaceViewModel workspace, FilesSidebarViewModel sidebar) =
            await OpenAsync("transient-edit");
        using VaultLifecycleViewModel owned = lifecycle;

        sidebar.SelectedNode = Node(sidebar, "alpha.md");
        WorkspaceTabViewModel alpha = Assert.Single(workspace.ActiveGroup.Tabs);
        const string edited = "# Alpha\n\nTyped into the transient tab.\n";
        alpha.Text = edited;
        Assert.True(alpha.IsDirty);

        sidebar.SelectedNode = Node(sidebar, "beta.md");

        Assert.Equal(new[] { "alpha.md", "beta.md" }, workspace.ActiveGroup.Tabs.Select(tab => tab.Path));
        Assert.Same(alpha, workspace.ActiveGroup.Tabs[0]);
        Assert.True(alpha.IsDirty);
        Assert.Equal(edited, alpha.Text);
        Assert.False(alpha.IsTransient);
        Assert.True(workspace.ActiveGroup.Tabs[1].IsTransient);
    }

    /// <summary>
    /// R-2 transient tab: a tab opened explicitly survives arrowing — the
    /// selection shows its notes beside it, in the transient tab, and the
    /// next arrow replaces only that.
    /// </summary>
    [Fact]
    public async Task AnExplicitlyOpenedTabSurvivesArrowing()
    {
        (VaultLifecycleViewModel lifecycle, WorkspaceViewModel workspace, FilesSidebarViewModel sidebar) =
            await OpenAsync("transient-explicit");
        using VaultLifecycleViewModel owned = lifecycle;
        workspace.OpenPath("Docs/readme.md");
        WorkspaceTabViewModel explicitTab = Assert.Single(workspace.ActiveGroup.Tabs);
        Assert.False(explicitTab.IsTransient);
        Assert.False(explicitTab.IsDirty);

        sidebar.SelectedNode = Node(sidebar, "alpha.md");
        Assert.Equal(new[] { "Docs/readme.md", "alpha.md" }, workspace.ActiveGroup.Tabs.Select(tab => tab.Path));
        Assert.True(workspace.ActiveGroup.Tabs[1].IsTransient);

        sidebar.SelectedNode = Node(sidebar, "beta.md");
        Assert.Equal(new[] { "Docs/readme.md", "beta.md" }, workspace.ActiveGroup.Tabs.Select(tab => tab.Path));
        Assert.Same(explicitTab, workspace.ActiveGroup.Tabs[0]);
    }

    /// <summary>
    /// R-2 transient tab (spec review rounds 21–22): the flag clears for good
    /// on the tab's FIRST dirty transition. Arrow onto A, edit it, save it —
    /// clean again — then arrow onto B: A is kept, permanent, and B shows in
    /// a new transient tab. Driven through the workspace's own selection
    /// route (<c>OpenPath(fromSelection: true)</c>, what a Files selection
    /// calls), where a save runs without the vault lifecycle's refresh.
    /// </summary>
    [Fact]
    public void ArrowEditSaveArrow_KeepsTheSavedNote()
    {
        (VaultSession session, WorkspaceViewModel workspace) = NewWorkspace("transient-save");
        using VaultSession ownedSession = session;
        using WorkspaceViewModel ownedWorkspace = workspace;

        workspace.OpenPath("alpha.md", fromSelection: true);
        WorkspaceTabViewModel alpha = Assert.Single(workspace.ActiveGroup.Tabs);
        Assert.True(alpha.IsTransient);
        alpha.EditorDocument!.Insert(alpha.EditorDocument.TextLength, "Edited, then saved.\n");
        Assert.True(alpha.IsDirty);
        Assert.True(alpha.Save());
        Assert.False(alpha.IsDirty);

        workspace.OpenPath("beta.md", fromSelection: true);

        Assert.Equal(new[] { "alpha.md", "beta.md" }, workspace.ActiveGroup.Tabs.Select(tab => tab.Path));
        Assert.Same(alpha, workspace.ActiveGroup.Tabs[0]);
        Assert.False(alpha.IsTransient);
        Assert.True(workspace.ActiveGroup.Tabs[1].IsTransient);
    }

    /// <summary>
    /// R-2 transient tab (spec review round 22): promotion happens at the
    /// first dirty transition, period. Arrow onto A, type into it, undo back
    /// to clean, arrow onto B: A is kept, permanent — the undo does not
    /// make it transient again — and B shows in a new transient tab.
    /// </summary>
    [Fact]
    public void ArrowEditUndoArrow_KeepsTheNote()
    {
        (VaultSession session, WorkspaceViewModel workspace) = NewWorkspace("transient-undo");
        using VaultSession ownedSession = session;
        using WorkspaceViewModel ownedWorkspace = workspace;

        workspace.OpenPath("alpha.md", fromSelection: true);
        WorkspaceTabViewModel alpha = Assert.Single(workspace.ActiveGroup.Tabs);
        Assert.True(alpha.IsTransient);
        alpha.EditorDocument!.Insert(alpha.EditorDocument.TextLength, "typed");
        Assert.True(alpha.IsDirty);
        Assert.True(alpha.EditorDocument.UndoStack.CanUndo);
        alpha.EditorDocument.UndoStack.Undo();
        Assert.False(alpha.IsDirty);

        workspace.OpenPath("beta.md", fromSelection: true);

        Assert.Equal(new[] { "alpha.md", "beta.md" }, workspace.ActiveGroup.Tabs.Select(tab => tab.Path));
        Assert.Same(alpha, workspace.ActiveGroup.Tabs[0]);
        Assert.False(alpha.IsTransient);
        Assert.True(workspace.ActiveGroup.Tabs[1].IsTransient);
    }

    /// <summary>
    /// R-2 transient tab: an explicit open into the current tab makes it
    /// the note's own tab. Arrow onto A — the transient tab shows it — then
    /// open C explicitly in the current tab, which takes C in place, then
    /// arrow onto B: C's tab is kept, permanent, and B shows in a new
    /// transient tab.
    /// </summary>
    [Fact]
    public void AnExplicitOpenIntoTheTransientTab_KeepsIt()
    {
        (VaultSession session, WorkspaceViewModel workspace) = NewWorkspace("transient-replace");
        using VaultSession ownedSession = session;
        using WorkspaceViewModel ownedWorkspace = workspace;

        workspace.OpenPath("alpha.md", fromSelection: true);
        WorkspaceTabViewModel tab = Assert.Single(workspace.ActiveGroup.Tabs);
        Assert.True(tab.IsTransient);

        workspace.OpenPath("Docs/readme.md");
        Assert.Same(tab, Assert.Single(workspace.ActiveGroup.Tabs));
        Assert.Equal("Docs/readme.md", tab.Path);
        Assert.False(tab.IsTransient);

        workspace.OpenPath("beta.md", fromSelection: true);

        Assert.Equal(new[] { "Docs/readme.md", "beta.md" }, workspace.ActiveGroup.Tabs.Select(open => open.Path));
        Assert.Same(tab, workspace.ActiveGroup.Tabs[0]);
        Assert.True(workspace.ActiveGroup.Tabs[1].IsTransient);
    }

    /// <summary>
    /// R-2 transient tab: a tab born dirty is never transient. With beta
    /// open in the left pane and alpha edited, unsaved, in a split, arrow
    /// onto alpha with the left pane active: its new tab takes on the other
    /// pane's unsaved state and is kept — the next arrow shows the readme in
    /// a new transient tab beside it, never in its place.
    /// </summary>
    [Fact]
    public void ASelectionOntoAnotherPanesUnsavedNote_IsNeverTransient()
    {
        (VaultSession session, WorkspaceViewModel workspace) = NewWorkspace("transient-born-dirty");
        using VaultSession ownedSession = session;
        using WorkspaceViewModel ownedWorkspace = workspace;
        workspace.OpenPath("beta.md");
        WorkspaceGroupViewModel left = workspace.ActiveGroup;
        workspace.OpenPath("alpha.md", WorkspaceOpenTarget.SplitRight);
        Assert.NotSame(left, workspace.ActiveGroup);
        WorkspaceTabViewModel edited = Assert.Single(workspace.ActiveGroup.Tabs);
        edited.EditorDocument!.Insert(edited.EditorDocument.TextLength, "Unsaved in the right pane.\n");
        Assert.True(edited.IsDirty);
        workspace.SelectGroupFromKeyboardFocus(left);
        Assert.Same(left, workspace.ActiveGroup);

        workspace.OpenPath("alpha.md", fromSelection: true);
        WorkspaceTabViewModel mirror = Assert.IsType<WorkspaceTabViewModel>(left.ActiveTab);
        Assert.Equal("alpha.md", mirror.Path);
        Assert.NotSame(edited, mirror);
        Assert.True(mirror.IsDirty);
        Assert.False(mirror.IsTransient);

        workspace.OpenPath("Docs/readme.md", fromSelection: true);

        Assert.Equal(new[] { "beta.md", "alpha.md", "Docs/readme.md" }, left.Tabs.Select(open => open.Path));
        Assert.Same(mirror, left.Tabs[1]);
        Assert.True(left.Tabs[2].IsTransient);
    }

    private (VaultSession Session, WorkspaceViewModel Workspace) NewWorkspace(string label)
    {
        string root = NewVault(label);
        VaultSession session = VaultSession.OpenFilesystem(root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }

        return (session, new WorkspaceViewModel(
            session,
            root,
            () => [],
            Record,
            startInteractionBackgroundWork: false));
    }

    /// <summary>
    /// R-2 transient tab (spec review round 21): with A in a tab of its own
    /// and B in the transient tab, selecting A activates A's tab — B stays
    /// where it is, still transient, and A still has exactly one tab.
    /// </summary>
    [Fact]
    public async Task SelectingAPermanentNote_ActivatesItsTab()
    {
        (VaultLifecycleViewModel lifecycle, WorkspaceViewModel workspace, FilesSidebarViewModel sidebar) =
            await OpenAsync("transient-reselect");
        using VaultLifecycleViewModel owned = lifecycle;
        sidebar.SelectedNode = Node(sidebar, "alpha.md");
        Assert.True(sidebar.OpenNode(Node(sidebar, "alpha.md"), WorkspaceOpenTarget.NewTab));
        sidebar.SelectedNode = Node(sidebar, "beta.md");
        WorkspaceTabViewModel alpha = workspace.ActiveGroup.Tabs[0];
        WorkspaceTabViewModel beta = workspace.ActiveGroup.Tabs[1];
        Assert.True(beta.IsTransient);

        sidebar.SelectedNode = Node(sidebar, "alpha.md");

        Assert.Same(alpha, workspace.ActiveGroup.ActiveTab);
        Assert.Equal(new[] { "alpha.md", "beta.md" }, workspace.ActiveGroup.Tabs.Select(tab => tab.Path));
        Assert.Same(beta, workspace.ActiveGroup.Tabs[1]);
        Assert.True(beta.IsTransient);
        Assert.Single(workspace.ActiveGroup.Tabs, tab => tab.Path == "alpha.md");
    }

    private async Task<(VaultLifecycleViewModel Lifecycle, WorkspaceViewModel Workspace, FilesSidebarViewModel Sidebar)>
        OpenAsync(string label)
    {
        string root = NewVault(label);
        VaultLifecycleViewModel lifecycle = NewLifecycle(root);
        await lifecycle.OpenVaultAsync(root);
        await SettleSidebarAsync(lifecycle);
        return (lifecycle,
            Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace),
            Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar));
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
        Assert.Equal(NavigationHelpText(), AutomationProperties.GetHelpText(host.Tree));
    });

    /// <summary>
    /// R-2 (spec 3.2 item 2, codex PR 2 round 3): Space on a focused tree
    /// row that has no check box — a group header of the date-grouped
    /// tree, the "Loading…" placeholder a level shows while it loads — is
    /// a consumed no-op, through the shipped key route: the key is handled
    /// and nothing moves — the row's batch state and ItemStatus, the
    /// selection, the expansion, the scroll offset — and nothing is said.
    /// </summary>
    [Fact]
    public void Space_IsAConsumedNoOpOnARowWithoutACheckBox() => RunSta(() =>
    {
        using var host = new TreeHost(NewVault("space-no-box"));
        host.Initialize();
        host.Sidebar.GroupByDate = true;
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.TreeRefreshCompletion);
        FileTreeNodeViewModel group = Assert.IsType<FileTreeNodeViewModel>(
            host.Sidebar.RootNodes.FirstOrDefault(node => node.IsGroupHeader));
        FileTreeNodeViewModel placeholder = FileTreeNodeViewModel.Loading();
        host.Sidebar.RootNodes.Add(placeholder);

        foreach (FileTreeNodeViewModel node in new[] { group, placeholder })
        {
            TreeViewItem row = host.FocusRow(node);
            var before = RowState(host, node, row);
            int announced = host.AnnouncementCount;
            int requests = host.Requests.Count;

            Assert.True(host.Press(row, Key.Space), $"Space on '{node.DisplayName}' went unhandled.");

            Assert.Equal(before, RowState(host, node, row));
            Assert.Equal(announced, host.AnnouncementCount);
            Assert.Equal(requests, host.Requests.Count);
            Assert.Same(row, Keyboard.FocusedElement);
        }
    });

    private static (bool Batch, string ItemStatus, bool Selected, bool Expanded, FileTreeNodeViewModel? SelectedNode, int Checked, double Offset)
        RowState(TreeHost host, FileTreeNodeViewModel node, TreeViewItem row) =>
        (node.IsBatchSelected,
            AutomationProperties.GetItemStatus(row),
            row.IsSelected,
            row.IsExpanded,
            host.Sidebar.SelectedNode,
            host.Sidebar.BatchSelectionCount,
            host.ScrollOffset);

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
    /// R-2 on the lists (codex PR 2 round 5): an arrow in the dual pane and
    /// in the filter results — the key through the list's own route, not
    /// an assignment — moves the list's selection, and the shipped
    /// SelectionChanged handlers turn that into the selection-driven open:
    /// the note the arrow reached is shown without asking for focus, its
    /// selection is spoken, and after settling focus is still on the row
    /// the arrow reached. Focusing the starting row moves no selection, so
    /// nothing opens before the arrow.
    /// </summary>
    [Fact]
    public void ArrowInTheLists_ShowsTheNoteAndKeepsFocusOnTheRow() => RunSta(() =>
    {
        using var host = new TreeHost(NewVault("list-arrows"));
        host.Initialize();

        // The dual pane with no folder selected lists the vault root's
        // files: alpha.md and beta.md.
        host.Sidebar.IsDualPaneEnabled = true;
        AssertArrowShowsTheNote(host, host.DualPane, "beta.md");

        // Every note in the vault is a result.
        host.Sidebar.FilterText = "ext:md";
        AssertArrowShowsTheNote(host, host.FilterResults, "Folder/Folder.md");
    });

    /// <summary>Focus the row next to <paramref name="target"/>'s in
    /// <paramref name="list"/>, then arrow onto <paramref name="target"/>
    /// (Down from the row above it, or Up from the row below).</summary>
    private static void AssertArrowShowsTheNote(TreeHost host, ListBox list, string target)
    {
        List<FileTreeNodeViewModel> rows = [];
        Assert.True(PumpedDispatcher.PumpUntil(() =>
        {
            rows = list.Items.OfType<FileTreeNodeViewModel>().ToList();
            return rows.Count >= 2 && rows.Exists(node => node.Path == target);
        }));
        int targetIndex = rows.FindIndex(node => node.Path == target);
        int startIndex = targetIndex == 0 ? 1 : targetIndex - 1;
        FileTreeNodeViewModel reached = rows[targetIndex];
        ListBoxItem start = host.FocusListRow(list, rows[startIndex].Path);
        Assert.Null(list.SelectedItem);
        host.Requests.Clear();
        int announced = host.AnnouncementCount;

        Assert.True(
            host.PressThrough(start, startIndex < targetIndex ? Key.Down : Key.Up),
            $"The arrow in {AutomationProperties.GetAutomationId(list)} went unhandled.");
        // Settle: whatever the open queued has run before focus is read.
        _ = PumpedDispatcher.PumpUntil(() => false, TimeSpan.FromMilliseconds(300));

        Assert.Same(reached, list.SelectedItem);
        Assert.Equal((target, WorkspaceOpenTarget.CurrentTab, false), Assert.Single(host.Requests));
        Assert.Equal(new A11yEvent.RowSelected(reached.DisplayName), host.LastAnnouncement);
        Assert.Equal(announced + 1, host.AnnouncementCount);
        Assert.Same(list.ItemContainerGenerator.ContainerFromItem(reached), Keyboard.FocusedElement);
    }

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

    /// <summary>
    /// R-3 (codex PR 2 round 2): the Tags tree applies a tag when its
    /// selection CHANGES, so every filter clear releases that selection —
    /// Clear Sidebar Filter and an emptied field alike — and selecting the
    /// same tag again applies it again. A one-tag vault has no other row
    /// to move to first.
    /// </summary>
    [Fact]
    public void TagTree_ReappliesTheSameTagAfterAClear() => RunSta(() =>
    {
        string root = NewVault("tag-again");
        File.WriteAllText(Path.Combine(root, "tagged.md"), "---\ntags: [solo]\n---\n\n# Tagged\n");
        using var host = new TreeHost(root);
        host.Initialize();
        host.Sidebar.ShowTags = true;
        TreeViewItem row = host.TagRow("solo");

        row.IsSelected = true;
        Assert.Equal("#solo", host.Sidebar.FilterText);

        host.Sidebar.ClearFilterCommand.Execute(null);
        Assert.False(host.Sidebar.IsFilterActive);
        row.IsSelected = true;
        Assert.Equal("#solo", host.Sidebar.FilterText);
        Assert.True(host.Sidebar.IsFilterActive);

        host.Sidebar.FilterText = string.Empty;
        row.IsSelected = true;
        Assert.Equal("#solo", host.Sidebar.FilterText);
    });

    /// <summary>
    /// R-3 (codex PR 2 round 4; spec review round 21): Escape in the focused
    /// filter field is a promised clear route. With a whitespace tag's scope
    /// active (the field empty, the scope otherwise invisible) and with a
    /// typed filter alike: the scope and text go, the results go, the status
    /// line says "Filter cleared.", and the one thing posted is core's typed
    /// SidebarFilterCleared — no host-composed copy at all.
    /// </summary>
    [Fact]
    public void Escape_ClearsAnActiveFilter() => RunSta(() =>
    {
        using var host = new TreeHost(NewVaultWithSpacedTag("clear-escape"));
        host.Initialize();
        foreach (Action<FilesSidebarViewModel> activate in Activations())
        {
            int before = ActivateFilter(host, activate);

            Assert.True(host.FilterField.Focus());
            Assert.True(host.Press(host.FilterField, Key.Escape), "Escape in the filter field went unhandled.");
            PumpedDispatcher.PumpUntilDrained(host.Sidebar.FilterCompletion);

            AssertClearedOnce(host, before);
            Assert.Same(host.FilterField, Keyboard.FocusedElement);
        }
    });

    /// <summary>
    /// R-3 (codex PR 2 round 4; spec review round 21): the Clear filter
    /// button, invoked the way UI Automation does, clears exactly as Escape
    /// does — and, having disabled itself, hands the keys to the filter
    /// field instead of keeping them on a disabled control.
    /// </summary>
    [Fact]
    public void ClearButton_ClearsAnActiveFilterAndKeepsTheKeysInTheField() => RunSta(() =>
    {
        using var host = new TreeHost(NewVaultWithSpacedTag("clear-button"));
        host.Initialize();
        foreach (Action<FilesSidebarViewModel> activate in Activations())
        {
            int before = ActivateFilter(host, activate);
            Assert.True(host.ClearButton.IsEnabled);
            Assert.True(host.ClearButton.Focus());

            var invoke = (System.Windows.Automation.Provider.IInvokeProvider)
                new System.Windows.Automation.Peers.ButtonAutomationPeer(host.ClearButton)
                    .GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke);
            invoke.Invoke();
            PumpedDispatcher.Drain();
            PumpedDispatcher.PumpUntilDrained(host.Sidebar.FilterCompletion);

            AssertClearedOnce(host, before);
            Assert.Same(host.FilterField, Keyboard.FocusedElement);
        }
    });

    /// <summary>
    /// R-3 (spec review round 22): the field emptied by the user — select-all
    /// (Ctrl+A's command) and Delete through the focused field's own key
    /// route — is the third clear route, witnessed like Escape and the
    /// button: with a typed query narrowing a whitespace tag's scope, and
    /// with a typed filter alone, the text and the scope reset together in
    /// one change, the results go, the status says "Filter cleared.", and
    /// the one thing posted is core's typed SidebarFilterCleared.
    /// </summary>
    [Fact]
    public void EmptyingTheField_ClearsLikeEscape() => RunSta(() =>
    {
        using var host = new TreeHost(NewVaultWithSpacedTag("clear-emptied"));
        host.Initialize();
        foreach (Action<FilesSidebarViewModel> activate in new Action<FilesSidebarViewModel>[]
        {
            sidebar =>
            {
                sidebar.ActivateTag("two words");
                sidebar.FilterText = "spaced";
            },
            sidebar => sidebar.FilterText = "alpha",
        })
        {
            int before = ActivateFilter(host, activate);
            Assert.True(host.FilterField.Focus());
            ApplicationCommands.SelectAll.Execute(null, host.FilterField);
            Assert.Equal(host.FilterField.Text.Length, host.FilterField.SelectionLength);

            Assert.True(host.PressThrough(host.FilterField, Key.Delete), "Delete in the filter field went unhandled.");
            PumpedDispatcher.PumpUntilDrained(host.Sidebar.FilterCompletion);

            AssertClearedOnce(host, before);
            Assert.Equal(string.Empty, host.FilterField.Text);
        }
    });

    private string NewVaultWithSpacedTag(string label)
    {
        string root = NewVault(label);
        File.WriteAllText(Path.Combine(root, "spaced.md"), "---\ntags: [\"two words\"]\n---\n\n# Spaced\n");
        return root;
    }

    /// <summary>A whitespace tag's out-of-band scope, then a typed filter.</summary>
    private static Action<FilesSidebarViewModel>[] Activations() =>
    [
        sidebar => sidebar.ActivateTag("two words"),
        sidebar => sidebar.FilterText = "alpha",
    ];

    private static int ActivateFilter(TreeHost host, Action<FilesSidebarViewModel> activate)
    {
        activate(host.Sidebar);
        PumpedDispatcher.PumpUntilDrained(host.Sidebar.FilterCompletion);
        Assert.True(host.Sidebar.IsFilterActive);
        Assert.NotEmpty(host.Sidebar.FilterResults);
        return host.AnnouncementCount;
    }

    private static void AssertClearedOnce(TreeHost host, int before)
    {
        Assert.False(host.Sidebar.IsFilterActive);
        Assert.Null(host.Sidebar.ScopeTag);
        Assert.Equal(string.Empty, host.Sidebar.FilterText);
        Assert.Empty(host.Sidebar.FilterResults);
        Assert.Equal("Filter cleared.", host.Sidebar.Status);
        A11yEvent[] spoken = [.. host.Announcements.Skip(before)];
        Assert.DoesNotContain(spoken, announcement => announcement is A11yEvent.HostComposed);
        Assert.IsType<A11yEvent.SidebarFilterCleared>(Assert.Single(spoken));
        Assert.False(host.ClearButton.IsEnabled);
    }

    /// <summary>
    /// R-3 (codex PR 2 round 4): with nothing filtering there is nothing to
    /// clear — the Clear filter button is disabled, Escape in the field is
    /// left unhandled for the window's own Escape, and nothing is said.
    /// </summary>
    [Fact]
    public void EscapeAndTheClearButton_LeaveAnInactiveFilterAlone() => RunSta(() =>
    {
        using var host = new TreeHost(NewVault("clear-inactive"));
        host.Initialize();
        Assert.False(host.Sidebar.IsFilterActive);
        int before = host.AnnouncementCount;

        Assert.False(host.ClearButton.IsEnabled);
        Assert.True(host.FilterField.Focus());
        Assert.False(host.Press(host.FilterField, Key.Escape));

        Assert.Equal(before, host.AnnouncementCount);
        Assert.Equal(string.Empty, host.Sidebar.FilterText);
    });

    private static string NavigationHelpText() => Commands.NavigationHelp.FilesTree;

    // ---- Helpers --------------------------------------------------------

    private VaultLifecycleViewModel NewLifecycle(
        string root,
        Func<WorkspaceTabViewModel, WorkspaceItemState, WorkspaceDirtyNavigationDecision>? confirmDirtyNavigation = null) =>
        new(
            pickVault: () => Task.FromResult<string?>(root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(root, "device-state", "recent-vaults.json")),
            announce: Record,
            confirmDirtyNavigation: confirmDirtyNavigation,
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
        public TreeView TagsTree { get; private set; } = null!;
        public TextBox FilterField { get; private set; } = null!;
        public Button ClearButton { get; private set; } = null!;
        public List<A11yEvent> Announcements => _announced;
        public List<(string Path, WorkspaceOpenTarget Target, bool FocusEditor)> Requests { get; } = [];
        public A11yEvent LastAnnouncement => _announced[^1];
        public int AnnouncementCount => _announced.Count;

        /// <summary>The Files tree's own scroll position.</summary>
        public double ScrollOffset => Descendants<ScrollViewer>(Tree).First().VerticalOffset;

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
            TagsTree = Detach<TreeView>(Assert.Single(
                LogicalDescendants(Shell).OfType<TreeView>(),
                tree => AutomationProperties.GetAutomationId(tree) == "SidebarTagTree"));
            DockPanel.SetDock(TagsTree, Dock.Top);
            layout.Children.Add(TagsTree);
            FilterField = Detach<TextBox>(Assert.IsType<TextBox>(Shell.FindName("SidebarFilterTextBox")));
            ClearButton = Detach<Button>(Assert.Single(
                LogicalDescendants(Shell).OfType<Button>(),
                button => AutomationProperties.GetAutomationId(button) == "SidebarFilterClear"));
            DockPanel.SetDock(FilterField, Dock.Top);
            DockPanel.SetDock(ClearButton, Dock.Top);
            layout.Children.Add(FilterField);
            layout.Children.Add(ClearButton);
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

        /// <summary>The shipped Tags tree's row for <paramref name="full"/>,
        /// once the tree shows it.</summary>
        public TreeViewItem TagRow(string full)
        {
            TreeViewItem? row = null;
            Assert.True(PumpedDispatcher.PumpUntil(() =>
            {
                TagsTree.UpdateLayout();
                row = Sidebar.Tags
                    .Where(tag => tag.Full == full)
                    .Select(tag => TagsTree.ItemContainerGenerator.ContainerFromItem(tag) as TreeViewItem)
                    .FirstOrDefault(container => container is not null);
                return row is not null;
            }));
            return row!;
        }

        /// <summary>The key's tunnelling route through the real surface:
        /// the event is raised on the focused element, so the list's own
        /// preview handler sees it as that element's key.</summary>
        public bool Press(UIElement target, Key key)
        {
            AwaitNoHeldModifier();
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

        /// <summary>The key through its whole route, as the input manager
        /// delivers it: the tunnelling preview and — unless that was
        /// handled — the bubbling KeyDown, which runs the focused control's
        /// own key bindings (a text box's Delete).</summary>
        public bool PressThrough(UIElement target, Key key)
        {
            AwaitNoHeldModifier();
            PresentationSource source = PresentationSource.FromVisual(target)!;
            var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };
            target.RaiseEvent(preview);
            bool handled = preview.Handled;
            if (!handled)
            {
                var down = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
                {
                    RoutedEvent = Keyboard.KeyDownEvent,
                };
                target.RaiseEvent(down);
                handled = down.Handled;
            }

            PumpedDispatcher.Drain();
            return handled;
        }

        /// <summary>These facts press unmodified keys, and WPF reads the
        /// modifiers off the real keyboard (<c>Keyboard.Modifiers</c>, the
        /// thread's key state): a modifier held on a shared desktop — someone
        /// typing, another run's input landing in this activated window —
        /// turns the key into a chord no route claims. A brief press is
        /// waited out; one still held fails here, by name, instead of as an
        /// unhandled key.</summary>
        private static void AwaitNoHeldModifier() =>
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => Keyboard.Modifiers == ModifierKeys.None,
                    TimeSpan.FromSeconds(5)),
                $"A real modifier key is held on this desktop ({Keyboard.Modifiers}); the key facts press unmodified keys.");

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
