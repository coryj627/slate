// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.FileManagement;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-39 (#1670) on the shipped window: a Reveal in File Tree from an
/// editor, the graph tab or a note header's folder segment lands the keys on
/// its row before it selects, so the open's tab switch moves nothing and the
/// keys move once, never through the new tab's header. The window switches
/// tabs here as in the app: the sidebar's opens go to the workspace as the
/// lifecycle sends them.
/// </summary>
public sealed class RevealInFilesTests
{
    /// <summary>The keys in a note's editor; a Reveal of a listed note. Main
    /// left them on the new tab's header; landing after the selection moved
    /// them twice.</summary>
    [Fact]
    public void ARevealOfAListedNoteFromAnEditorLandsOnItsRowOnce() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell();
        shell.OpenAndParkKeysInTheEditor();

        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("b.md"));
        shell.SettleLoads();

        shell.AssertLandedOnceOn("b.md", opens: "b.md", said: "Selected: b.md");
    });

    /// <summary>The graph tab's own Reveal, the keys in its table.</summary>
    [Fact]
    public void AGraphTabsRevealLandsOnItsRowOnce() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell();
        shell.Host.Workspace.OpenGraph();
        PumpedDispatcher.PumpUntilDrained(shell.Host.Workspace.GraphDocument!.WhenAllWorkDrained());
        shell.Host.Settle();
        DataGrid table = ShownShell.Descendants(shell.Host.Shell).OfType<DataGrid>().First(grid => grid.IsVisible);
        Assert.True(table.Focus() || table.IsKeyboardFocusWithin, "premise: the graph table did not take the keys");
        shell.Mark();

        Assert.True(shell.Host.Workspace.RevealGraphRowFromSurface("b.md"));
        shell.SettleLoads();

        shell.AssertLandedOnceOn("b.md", opens: "b.md", said: "Selected: b.md");
    });

    /// <summary>A note in a folder that has not listed its rows, from an
    /// editor: it seats when the folder lists, the keys on its row once.</summary>
    [Fact]
    public void ARevealIntoAnUnlistedFolderFromAnEditorLandsOnItsRowOnce() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell();
        shell.OpenAndParkKeysInTheEditor();

        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));
        shell.SettleLoads();

        shell.AssertLandedOnceOn("docs/n.md", opens: "docs/n.md", said: "Selected: n.md");
    });

    /// <summary>A note header's folder segment into a folder whose parent has
    /// not listed its rows: the folder seats when its parent lists, the keys
    /// on its row once. Main waited for a later whole-tree publication and
    /// left the keys on the segment.</summary>
    [Fact]
    public void AFolderSegmentRevealsAnUnlistedFolderOnItsRow() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell();
        Button mid = shell.OpenAndParkKeysOnASegment("deep/mid/n2.md", "mid");

        Invoke(mid);
        shell.SettleLoads();

        shell.AssertLandedOnceOn("deep/mid", opens: null, said: "Selected: mid, folder");
    });

    /// <summary>A folder segment of a listed folder that has a folder note: the
    /// folder note opens in a new tab, and the keys go to the row once, never
    /// through that tab's header.</summary>
    [Fact]
    public void AFolderSegmentOfAFolderWithAFolderNoteLandsOnItsRowOnce() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell();
        FileTreeNodeViewModel fn = shell.Row("fn");
        fn.IsExpanded = true;
        shell.SettleLoads();
        Button sub = shell.OpenAndParkKeysOnASegment("fn/sub/x.md", "sub");

        Invoke(sub);
        shell.SettleLoads();

        shell.AssertLandedOnceOn("fn/sub", opens: "fn/sub/sub.md", said: "Selected: sub, folder");
    });

    /// <summary>Group by date on and the reader's "Today" group collapsed: a
    /// Reveal of a note in it opens the group and lands the keys on the row
    /// once (Call 14). Main left them on the new tab's header; a landing
    /// without the expansion fell to the tree's first row.</summary>
    [Fact]
    public void ARevealOfANoteInACollapsedDateGroupLandsOnItsRow() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell();
        shell.Sidebar.GroupByDate = true;
        shell.SettleLoads();
        FileTreeNodeViewModel today = shell.Sidebar.RootNodes.Single(node => node.IsGroupHeader && node.Name == "Today");
        today.IsExpanded = false;
        shell.SettleLoads();
        shell.OpenAndParkKeysInTheEditor();

        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("a.md"));
        shell.SettleLoads();

        Assert.True(today.IsExpanded, "the Reveal left the date group collapsed");
        shell.AssertLandedOnceOn("a.md", opens: "a.md", said: "Selected: a.md");
    });

    /// <summary>A template create, through the lifecycle's own handler, is a
    /// gesture that names no row: a Reveal waiting before it never seats.
    /// Main seated it at the handler's refresh.</summary>
    [Fact]
    public void TheTemplateCreateDropsAnOlderWaitingReveal() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.OpenAndParkKeysInTheEditor();
        shell.HoldTreeWork();
        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));

        Assert.IsType<NoteCreateResult.Landed>(((ISurfaceNoteCreator)shell.Sidebar).TryCreateNote("t.md", "# T\n"));
        shell.RaiseTemplateNoteWritten("t.md");
        shell.ReleaseTreeWork();
        shell.SettleLoads();
        shell.Sidebar.Refresh();
        shell.SettleLoads();

        shell.AssertNeverSeated("docs/n.md");
    });

    /// <summary>A Reveal while a filter or a tag scope is active clears it
    /// through Clear filter, then lands (Call 13). The keys never reach the
    /// filter field; the clear's own line, then the selection's, are what is
    /// said. Main left the filter in place and the keys out of the tree.</summary>
    [Theory]
    [InlineData("text")]
    [InlineData("tag")]
    public void ARevealNeverLandsTheKeysInTheFilterField(string filter) => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell();
        if (filter == "text")
        {
            shell.Sidebar.FilterText = "b";
        }
        else
        {
            shell.Sidebar.ActivateTag("alpha");
        }

        PumpedDispatcher.PumpUntilDrained(shell.Sidebar.FilterCompletion);
        shell.Host.Settle();
        Assert.True(shell.Sidebar.IsFilterActive, "premise: no filter is active");
        shell.OpenAndParkKeysInTheEditor();

        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("b.md"));
        shell.SettleLoads();

        Assert.DoesNotContain(shell.FocusChanges, focus => ReferenceEquals(focus, shell.Host.Shell.SidebarFilterTextBox));
        Assert.False(shell.Sidebar.IsFilterActive, "the filter is still active");
        Assert.Equal(string.Empty, shell.Sidebar.FilterText);
        Assert.Null(shell.Sidebar.ScopeTag);
        Assert.Equal(
            ["SidebarFilterCleared", "RowSelected: Selected: b.md"],
            shell.SaidSinceMark.Where(said => said is A11yEvent.SidebarFilterCleared or A11yEvent.RowSelected or A11yEvent.TreeFolderSelected)
                .Select(said => said is A11yEvent.SidebarFilterCleared ? "SidebarFilterCleared" : $"{said.GetType().Name}: {SlateUniffiMethods.A11yRender(said).Text}"));
        shell.AssertLandedOnceOn("b.md", opens: "b.md", said: "Selected: b.md");
    });

    private static void Invoke(UIElement element) =>
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(element).GetPattern(PatternInterface.Invoke)).Invoke();

    /// <summary>The shown shell over a vault with folders, a folder note and a
    /// tagged note, with a real Files sidebar attached through the lifecycle's
    /// own setter, its opens sent to the workspace as the lifecycle sends them,
    /// and every Reveal routed to it as the lifecycle binds it.</summary>
    private sealed class RevealShell : IDisposable
    {
        private static readonly (string Path, string Text)[] Notes =
        [
            ("a.md", "# A\n"),
            ("b.md", "# B\n"),
            ("docs/n.md", "# N\n"),
            ("docs/other.md", "# Other\n"),
            ("deep/mid/n2.md", "# N2\n"),
            ("deep/sibling.md", "# Sibling\n"),
            ("park/here.md", "# Here\n\nThe reader's note.\n"),
            ("fn/sub/sub.md", "# Sub\n\nThe folder note.\n"),
            ("fn/sub/x.md", "# X\n"),
            ("tagged.md", "# Tagged\n\n#alpha\n"),
        ];

        private readonly List<IInputElement> _focus;
        private int _focusMark;
        private int _opensMark;
        private int _saidMark;
        private TaskCompletionSource? _treeGate;

        public RevealShell(bool heldTreeWork = false)
        {
            Host = new ShownShell(Notes);
            Host.Workspace.IsRightPaneVisible = false;
            Sidebar = new FilesSidebarViewModel(
                Host.Session,
                Said.Add,
                vaultRoot: Host.Root,
                localAppDataRoot: Path.Combine(Host.Root, "device-state"),
                treeWorker: heldTreeWork ? new Func<Action, CancellationToken, Task>(RunTreeWork) : null);
            PumpedDispatcher.PumpUntilDrained(Sidebar.TreeRefreshCompletion);
            SetFileSidebar(Sidebar);
            Host.Workspace.GraphRevealInSidebar = Sidebar.SelectPathFromSurface;
            Sidebar.OpenTargetRequested += (_, request) =>
            {
                Opens.Add(request.Path);
                Host.Workspace.OpenPath(request.Path, request.Target, fromSelection: !request.FocusEditor);
            };
            Host.Settle();
            _focus = Host.RecordFocusChanges();
        }

        public ShownShell Host { get; }

        public FilesSidebarViewModel Sidebar { get; }

        public List<A11yEvent> Said { get; } = [];

        public List<string> Opens { get; } = [];

        public IReadOnlyList<IInputElement> FocusChanges => [.. _focus.Skip(_focusMark)];

        public IReadOnlyList<A11yEvent> SaidSinceMark => [.. Said.Skip(_saidMark)];

        private TreeView Tree => Host.Shell.FilesTree;

        public void Mark()
        {
            PumpedDispatcher.Drain();
            _focusMark = _focus.Count;
            _opensMark = Opens.Count;
            _saidMark = Said.Count;
        }

        public void HoldTreeWork() =>
            Volatile.Write(ref _treeGate, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

        public void ReleaseTreeWork() => Interlocked.Exchange(ref _treeGate, null)?.TrySetResult();

        private Task RunTreeWork(Action work, CancellationToken token) =>
            Volatile.Read(ref _treeGate) is { } gate ? RunAfter(gate.Task, work, token) : Task.Run(work, token);

        private static async Task RunAfter(Task gate, Action work, CancellationToken token)
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            await Task.Run(work, token).ConfigureAwait(false);
        }

        /// <summary>A note of its own, opened in a tab of its own, with the keys
        /// in its editor; the logs marked from there.</summary>
        public void OpenAndParkKeysInTheEditor(string note = "park/here.md")
        {
            Host.Workspace.OpenPath(note);
            Host.Settle();
            var area = ShownShell.Descendants(Host.Shell).OfType<ICSharpCode.AvalonEdit.Editing.TextArea>().First(candidate => candidate.IsVisible);
            Assert.True(area.Focus(), "premise: the editor did not take the keys");
            Mark();
        }

        /// <summary><paramref name="note"/> opened, the keys on its header's
        /// folder segment <paramref name="segment"/>; the logs marked from
        /// there.</summary>
        public Button OpenAndParkKeysOnASegment(string note, string segment)
        {
            Host.Workspace.OpenPath(note);
            Host.Settle();
            NoteHeaderView header = ShownShell.Descendants(Host.Shell).OfType<NoteHeaderView>()
                .Single(view => view.IsVisible && view.DataContext is WorkspaceTabViewModel tab && tab.Path == note);
            Button button = ShownShell.Descendants(header).OfType<Button>()
                .Single(candidate => candidate.IsVisible
                    && AutomationProperties.GetAutomationId(candidate) == "NoteHeaderFolder"
                    && ((candidate.Content as TextBlock)?.Text ?? AutomationProperties.GetName(candidate)) == segment);
            Assert.True(button.Focus(), "premise: the segment did not take the keys");
            Mark();
            return button;
        }

        public void SettleLoads()
        {
            for (int round = 0; round < 4; round++)
            {
                PumpedDispatcher.PumpUntilDrained(Sidebar.TreeRefreshCompletion);
                PumpedDispatcher.PumpUntilDrained(Sidebar.ChildExpansionCompletion);
                Host.Shell.UpdateLayout();
                PumpedDispatcher.Drain();
            }
        }

        public FileTreeNodeViewModel Row(string path) =>
            Every(Sidebar.RootNodes).Single(node => node.Path == path && !node.IsPlaceholder);

        /// <summary>The template flow's write reaches the lifecycle's own
        /// handler, subscribed as <c>InitializeWorkspace</c> subscribes it (a
        /// workspace attached through the setter has none).</summary>
        public void RaiseTemplateNoteWritten(string path)
        {
            var handler = (EventHandler<string>)Delegate.CreateDelegate(
                typeof(EventHandler<string>),
                Host.Lifecycle,
                typeof(VaultLifecycleViewModel).GetMethod("Workspace_TemplateNoteWritten", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new InvalidOperationException("The lifecycle's template handler is gone"));
            Host.Workspace.TemplateNoteWritten += handler;
            var raise = (EventHandler<string>?)typeof(WorkspaceViewModel)
                .GetField(nameof(WorkspaceViewModel.TemplateNoteWritten), BindingFlags.NonPublic | BindingFlags.Instance)?
                .GetValue(Host.Workspace);
            Assert.NotNull(raise);
            raise(Host.Workspace, path);
        }

        /// <summary>The row at <paramref name="path"/> is selected on both
        /// halves and holds the keys after exactly one focus change, to it, with
        /// no note header element on the way; what it opened and said since the
        /// mark is <paramref name="opens"/> and <paramref name="said"/>.</summary>
        public void AssertLandedOnceOn(string path, string? opens, string said)
        {
            FileTreeNodeViewModel row = Row(path);
            Assert.Same(row, Sidebar.SelectedNode);
            Assert.True(row.IsSelected, $"{path}'s node does not hold the selection");
            Assert.Same(row, Tree.SelectedItem);
            TreeViewItem? container = ContainerOf(row);
            Assert.True(container is not null, $"{path}'s row has no container");
            Assert.True(container.IsSelected, $"{path}'s container is not selected");
            IReadOnlyList<IInputElement> moves = FocusChanges;
            string trail = string.Join(" -> ", moves.Select(Describe));
            Assert.True(container.IsKeyboardFocused, $"the keys are on {Describe(Keyboard.FocusedElement)}, not on {path}'s row; they went {trail}");
            Assert.DoesNotContain(moves, InHeader);
            Assert.True(moves.Count == 1 && ReferenceEquals(moves[0], container), $"the keys moved {moves.Count} times: {trail}");
            string[] expectedOpens = opens is null ? [] : [opens];
            Assert.Equal(expectedOpens, Opens.Skip(_opensMark));
            Assert.Equal(
                [said],
                SaidSinceMark.Where(line => line is A11yEvent.RowSelected or A11yEvent.TreeFolderSelected)
                    .Select(line => SlateUniffiMethods.A11yRender(line).Text));
        }

        public void AssertNeverSeated(string path)
        {
            FileTreeNodeViewModel? row = Every(Sidebar.RootNodes).FirstOrDefault(node => node.Path == path && !node.IsPlaceholder);
            Assert.False(row?.IsSelected == true, $"{path}'s node holds the selection");
            Assert.NotEqual(path, Sidebar.SelectedNode?.Path);
            Assert.NotEqual(path, (Tree.SelectedItem as FileTreeNodeViewModel)?.Path);
            Assert.DoesNotContain(path, Opens.Skip(_opensMark));
            Assert.DoesNotContain(
                SaidSinceMark.Where(line => line is A11yEvent.RowSelected).Select(line => SlateUniffiMethods.A11yRender(line).Text),
                line => line == $"Selected: {Path.GetFileName(path)}");
            Assert.DoesNotContain(FocusChanges, focus => focus is TreeViewItem { DataContext: FileTreeNodeViewModel node } && node.Path == path);
        }

        private TreeViewItem? ContainerOf(FileTreeNodeViewModel node)
        {
            var path = new List<FileTreeNodeViewModel>();
            if (!PathTo(Sidebar.RootNodes, node, path))
            {
                return null;
            }

            ItemsControl owner = Tree;
            TreeViewItem? container = null;
            foreach (FileTreeNodeViewModel step in path)
            {
                container = owner.ItemContainerGenerator.ContainerFromItem(step) as TreeViewItem;
                if (container is null)
                {
                    return null;
                }

                owner = container;
            }

            return container;

            static bool PathTo(IEnumerable<FileTreeNodeViewModel> level, FileTreeNodeViewModel target, List<FileTreeNodeViewModel> path)
            {
                foreach (FileTreeNodeViewModel row in level)
                {
                    path.Add(row);
                    if (ReferenceEquals(row, target) || PathTo(row.Children, target, path))
                    {
                        return true;
                    }

                    path.RemoveAt(path.Count - 1);
                }

                return false;
            }
        }

        private static IEnumerable<FileTreeNodeViewModel> Every(IEnumerable<FileTreeNodeViewModel> level) =>
            level.SelectMany(node => Every(node.Children).Prepend(node));

        private static bool InHeader(IInputElement element)
        {
            DependencyObject? node = element as DependencyObject;
            while (node is not null)
            {
                if (node is NoteHeaderView)
                {
                    return true;
                }

                node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
            }

            return false;
        }

        private static string Describe(IInputElement? element) => element switch
        {
            null => "nothing",
            TreeViewItem { DataContext: FileTreeNodeViewModel node } => $"tree row {node.Path}",
            FrameworkElement framework => $"{framework.GetType().Name} '{AutomationProperties.GetName(framework)}' ({AutomationProperties.GetAutomationId(framework)})",
            _ => element.GetType().Name,
        };

        private void SetFileSidebar(FilesSidebarViewModel? sidebar) =>
            (typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.FileSidebar), BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException("FileSidebar is gone"))
            .SetValue(Host.Lifecycle, sidebar);

        public void Dispose()
        {
            ReleaseTreeWork();
            try
            {
                SetFileSidebar(null);
                PumpedDispatcher.PumpUntilDrained(Sidebar.BeginSessionShutdownAndCaptureWork().SessionWork);
            }
            finally
            {
                Host.Dispose();
            }
        }
    }
}
