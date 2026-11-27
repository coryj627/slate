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
using System.Windows.Threading;
using SlateWindows.FileManagement;
using SlateWindows.Graph;
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
        // Every line the sidebar said, exactly: the clear's own, then the
        // selection's. An extra line fails this.
        Assert.Equal(
            ["SidebarFilterCleared: Filter cleared.", "RowSelected: Selected: b.md"],
            shell.SaidSinceMark.Select(said => $"{said.GetType().Name}: {SlateUniffiMethods.A11yRender(said).Text}"));
        shell.AssertLandedOnceOn("b.md", opens: "b.md", said: "Selected: b.md");
    });

    /// <summary>A create's publication arms its rename, and the window queues
    /// the name field's focus at Input priority. A newer Reveal of a listed
    /// note runs before that, at Normal priority, and lands (Codex round 3).
    /// The older rename's queued focus then takes nothing: the keys stay on the
    /// revealed row after one move, and the name field never takes
    /// them.</summary>
    [Fact]
    public void ACreatesQueuedRenameFocusGivesWayToANewerReveal() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.OpenAndParkKeysInTheEditor();
        TextBox nameField = shell.Host.Shell.SidebarMutationNameTextBox;
        bool nameFieldTookTheKeys = false;
        nameField.GotKeyboardFocus += (_, _) => nameFieldTookTheKeys = true;
        shell.HoldTreeWork();
        shell.Sidebar.CreateNoteCommand.Execute(null);
        shell.Mark();
        int armed = 0;
        int? movesBeforeTheReveal = null;
        bool revealed = false;
        shell.Sidebar.InlineRenameRequested += () =>
        {
            armed++;
            _ = shell.Host.Shell.Dispatcher.InvokeAsync(
                () =>
                {
                    movesBeforeTheReveal = shell.FocusChanges.Count;
                    revealed = shell.Host.Workspace.RevealConnectionsRowFromSurface("b.md");
                },
                DispatcherPriority.Normal);
        };

        shell.ReleaseTreeWork();
        shell.SettleLoads();

        Assert.Equal(1, armed);
        Assert.True(revealed && movesBeforeTheReveal is not null, "premise: the newer Reveal never ran");
        Assert.False(nameFieldTookTheKeys, "the older rename took the keys into the name field");
        shell.AssertKeysMovedOnceTo("b.md", movesBeforeTheReveal!.Value);
    });

    /// <summary>The keys on a Connections leaf row; the leaf's Reveal in File
    /// Tree of a note in a folder that has not listed its rows, whose open
    /// re-roots the leaf on the note (the NVDA head session, row 2). The keys
    /// move once, to the Files row, and never to a Connections row after the
    /// Reveal starts.</summary>
    [Fact]
    public void AConnectionsLeafsRevealLandsOnItsRowOnceThroughTheReRoot() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell();
        ConnectionsLeafView surface = shell.KeysOnTheConnectionsRowOf("start.md", "docs/n.md", out TreeViewItem row);
        GraphConnectionRow target = ((ConnectionsRowViewModel)row.DataContext).Row!;

        shell.Host.Workspace.Connections.Execute(GraphRowAction.Reveal, target);
        shell.SettleLoads();
        shell.SettleLeaf();

        Assert.Equal("docs/n.md", shell.Host.Workspace.Connections.Root);
        Assert.DoesNotContain(shell.FocusChanges, focus => focus is DependencyObject element && surface.IsAncestorOf(element));
        shell.AssertLandedOnceOn("docs/n.md", opens: "docs/n.md", said: "Selected: n.md");
    });

    /// <summary>A Reveal into a folder whose load is held, asked with the keys
    /// on a note header's segment; the reader then moves the keys into the
    /// editor, opening nothing (the merge judge's major 1, the lead's ruling
    /// (a)). When the folder lists, the row is selected silently on both
    /// halves: nothing lands, nothing opens, nothing is said, and the tabs
    /// stay. The reader's return to Files lands the keys on that row, and a
    /// later whole-tree publication neither opens it nor moves the keys.</summary>
    [Fact]
    public void ALateRevealAfterTheKeysMovedSelectsItsRowSilently() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        _ = shell.OpenAndParkKeysOnASegment("park/here.md", "park");
        shell.HoldTreeWork();
        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));
        UIElement editor = shell.Editor;
        Assert.True(editor.Focus(), "premise: the editor did not take the keys");
        shell.Mark();
        string tabs = shell.Tabs;

        shell.ReleaseTreeWork();
        shell.SettleLoads();

        Assert.True(editor.IsKeyboardFocused, "the keys left the editor");
        Assert.Empty(shell.FocusChanges);
        Assert.Empty(shell.OpensSinceMark);
        Assert.Empty(shell.SaidSinceMark);
        Assert.Equal(tabs, shell.Tabs);
        shell.AssertSelectedOnBothHalves("docs/n.md");

        shell.Sidebar.Refresh();
        shell.SettleLoads();

        Assert.True(editor.IsKeyboardFocused, "a later publication moved the keys");
        Assert.Empty(shell.FocusChanges);
        Assert.Empty(shell.OpensSinceMark);
        Assert.Empty(shell.SaidSinceMark);
        Assert.Equal(tabs, shell.Tabs);

        Assert.True(shell.Host.Shell.LandOnFilesTree(), "the window's Files landing took nothing");
        shell.SettleLoads();
        shell.AssertKeysMovedOnceTo("docs/n.md", 0);

        shell.Sidebar.Refresh();
        shell.SettleLoads();

        shell.AssertKeysMovedOnceTo("docs/n.md", 0);
        shell.AssertSelectedOnBothHalves("docs/n.md");
        Assert.Empty(shell.OpensSinceMark);
        Assert.DoesNotContain(shell.SaidSinceMark, said => said is A11yEvent.RowSelected);
        Assert.Equal(tabs, shell.Tabs);
    });

    /// <summary>A Reveal from the graph tab into a folder whose load is held,
    /// the keys left on the graph table where the gesture had them: when the
    /// folder lists, the keys land on the row once, as before.</summary>
    [Fact]
    public void ALateRevealWithTheKeysStillOnTheGraphLandsThemOnce() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.Host.Workspace.OpenGraph();
        PumpedDispatcher.PumpUntilDrained(shell.Host.Workspace.GraphDocument!.WhenAllWorkDrained());
        shell.Host.Settle();
        DataGrid table = ShownShell.Descendants(shell.Host.Shell).OfType<DataGrid>().First(grid => grid.IsVisible);
        Assert.True(table.Focus() || table.IsKeyboardFocusWithin, "premise: the graph table did not take the keys");
        shell.Mark();
        shell.HoldTreeWork();

        Assert.True(shell.Host.Workspace.RevealGraphRowFromSurface("docs/n.md"));
        shell.ReleaseTreeWork();
        shell.SettleLoads();

        shell.AssertLandedOnceOn("docs/n.md", opens: "docs/n.md", said: "Selected: n.md");
    });

    /// <summary>A Reveal into a folder whose load is held; the reader opens
    /// another note through the workspace (the merge judge's major 1). The
    /// open ends the wait: when the folder lists, the revealed note is never
    /// seated or opened, the reader's note and the keys stay, and nothing is
    /// said.</summary>
    [Fact]
    public void AnOpenWhileARevealWaitsEndsIt() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.OpenAndParkKeysInTheEditor();
        shell.HoldTreeWork();
        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));

        shell.Host.Workspace.OpenPath("a.md");
        shell.Host.Settle();
        Assert.True(shell.Host.Workspace.ActiveGroup.ActiveTab?.Path == "a.md", "premise: the reader's open failed");
        shell.Mark();
        string tabs = shell.Tabs;
        IInputElement keys = Keyboard.FocusedElement;
        shell.ReleaseTreeWork();
        shell.SettleLoads();
        shell.Sidebar.Refresh();
        shell.SettleLoads();

        shell.AssertNeverSeated("docs/n.md");
        Assert.Equal("a.md", shell.Host.Workspace.ActiveGroup.ActiveTab?.Path);
        Assert.Equal(tabs, shell.Tabs);
        Assert.Same(keys, Keyboard.FocusedElement);
        Assert.Empty(shell.FocusChanges);
        Assert.Empty(shell.OpensSinceMark);
        Assert.Empty(shell.SaidSinceMark);
    });

    /// <summary>The merge judge's minor 1: the reader's row selected with the
    /// keys on it, a Reveal waiting on a held folder, and F2 pressed in the
    /// same pass as the folder's publication, which runs first. The seat is the
    /// newer intent: the rename's queued focus gives way, so the name field
    /// never takes the keys holding the revealed note's name, and Enter renames
    /// nothing.</summary>
    [Fact]
    public void F2InTheRevealsPublicationPassRenamesNothing() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.SelectAndFocusRow("a.md");
        TextBox nameField = shell.Host.Shell.SidebarMutationNameTextBox;
        var nameFieldHeld = new List<string>();
        nameField.GotKeyboardFocus += (_, _) => nameFieldHeld.Add(nameField.Text);
        shell.HoldTreeWork();
        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));
        shell.Mark();

        Dispatcher dispatcher = shell.Host.Shell.Dispatcher;
        using var posted = new ManualResetEventSlim();
        DispatcherHookEventHandler onPosted = (_, e) =>
        {
            if (e.Operation.Priority == DispatcherPriority.Normal && !dispatcher.CheckAccess())
            {
                posted.Set();
            }
        };
        dispatcher.Hooks.OperationPosted += onPosted;
        shell.ReleaseTreeWork();
        Assert.True(posted.Wait(TimeSpan.FromSeconds(10)), "premise: the folder's publication was never posted");
        dispatcher.Hooks.OperationPosted -= onPosted;
        Assert.True(shell.PressWithoutPumping(Key.F2), "premise: F2 on the selected row went unhandled");
        shell.SettleLoads();

        Assert.DoesNotContain("n.md", nameFieldHeld);
        if (nameField.IsKeyboardFocused)
        {
            nameField.Text = "renamed.md";
        }

        _ = shell.PressWithoutPumping(Key.Enter);
        shell.SettleLoads();

        Assert.True(File.Exists(Path.Combine(shell.Host.Root, "docs", "n.md")), "Enter renamed the revealed note");
        Assert.True(File.Exists(Path.Combine(shell.Host.Root, "a.md")), "Enter renamed the reader's note");
        Assert.Empty(Directory.EnumerateFiles(shell.Host.Root, "renamed.md", SearchOption.AllDirectories));
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
            ("start.md", "# Start\n\nLinks to [[n]].\n"),
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
            // Every open reaches the lifecycle's own handler, subscribed as
            // InitializeWorkspace subscribes it (a workspace attached through
            // the setter has none).
            Host.Workspace.FileOpened += (EventHandler<string>)Delegate.CreateDelegate(
                typeof(EventHandler<string>),
                Host.Lifecycle,
                typeof(VaultLifecycleViewModel).GetMethod("Workspace_FileOpened", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new InvalidOperationException("The lifecycle's open handler is gone"));
            Host.Settle();
            _focus = Host.RecordFocusChanges();
        }

        public IReadOnlyList<string> OpensSinceMark => [.. Opens.Skip(_opensMark)];

        /// <summary>The workspace's tabs by path, the active one marked.</summary>
        public string Tabs => string.Join(
            " | ",
            Host.Workspace.ActiveGroup.Tabs.Select(tab => ReferenceEquals(tab, Host.Workspace.ActiveGroup.ActiveTab) ? $"*{tab.Path}" : tab.Path));

        /// <summary>The visible editor's text area.</summary>
        public UIElement Editor =>
            ShownShell.Descendants(Host.Shell).OfType<ICSharpCode.AvalonEdit.Editing.TextArea>().First(candidate => candidate.IsVisible);

        /// <summary>The reader selects the row at <paramref name="path"/> and
        /// puts the keys on it, as an arrow onto it does.</summary>
        public void SelectAndFocusRow(string path)
        {
            FileTreeNodeViewModel row = Row(path);
            Sidebar.SelectedNode = row;
            Host.Shell.UpdateLayout();
            TreeViewItem? container = ContainerOf(row);
            Assert.True(container is not null && container.Focus(), $"premise: {path}'s row did not take the keys");
            PumpedDispatcher.Drain();
        }

        /// <summary>A key pressed where the keys are, through the window's
        /// routes, with nothing pumped after it.</summary>
        public bool PressWithoutPumping(Key key)
        {
            var target = (UIElement)Keyboard.FocusedElement;
            PresentationSource source = PresentationSource.FromVisual(target)!;
            var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            target.RaiseEvent(preview);
            if (preview.Handled)
            {
                return true;
            }

            var down = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.KeyDownEvent };
            target.RaiseEvent(down);
            return down.Handled;
        }

        /// <summary>The row at <paramref name="path"/> holds the selection on
        /// both halves.</summary>
        public void AssertSelectedOnBothHalves(string path)
        {
            FileTreeNodeViewModel row = Row(path);
            Assert.Same(row, Sidebar.SelectedNode);
            Assert.True(row.IsSelected, $"{path}'s node does not hold the selection");
            Host.Shell.UpdateLayout();
            TreeViewItem? container = ContainerOf(row);
            Assert.True(container is not null && container.IsSelected, $"{path}'s container is not selected");
            Assert.Same(row, Tree.SelectedItem);
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

        /// <summary><paramref name="note"/> opened, the Connections leaf shown
        /// over it as the palette's Show connections shows it (following the
        /// open note), and the keys on its row for <paramref name="path"/>; the
        /// logs marked from there.</summary>
        public ConnectionsLeafView KeysOnTheConnectionsRowOf(string note, string path, out TreeViewItem row)
        {
            Host.RelayFocusBoundaries();
            Host.Workspace.OpenPath(note);
            Host.Settle();
            Host.Workspace.ShowConnections();
            SettleLeaf();
            ConnectionsLeafView surface = Host.Shell.ConnectionsLeafSurface;
            row = ShownShell.Descendants(surface.TreeForTests).OfType<TreeViewItem>()
                .First(item => item.IsVisible && item.DataContext is ConnectionsRowViewModel { Row: { } shown } && shown.Path == path);
            Assert.True(row.Focus(), $"premise: the Connections row for {path} did not take the keys");
            Mark();
            return surface;
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

        public void SettleLeaf()
        {
            for (int pass = 0; pass < 3; pass++)
            {
                PumpedDispatcher.PumpUntilDrained(Host.Workspace.Connections.WhenAllWorkDrained());
                Host.Settle();
            }
        }

        /// <summary>After the first <paramref name="since"/> focus changes from
        /// the mark, the keys moved exactly once more, to the row at
        /// <paramref name="path"/>, and are there.</summary>
        public void AssertKeysMovedOnceTo(string path, int since)
        {
            TreeViewItem? container = ContainerOf(Row(path));
            Assert.True(container is not null, $"{path}'s row has no container");
            IInputElement[] moves = [.. FocusChanges.Skip(since)];
            string trail = string.Join(" -> ", moves.Select(Describe));
            Assert.True(container.IsKeyboardFocused, $"the keys are on {Describe(Keyboard.FocusedElement)}, not on {path}'s row; they went {trail}");
            Assert.True(moves.Length == 1 && ReferenceEquals(moves[0], container), $"the keys moved {moves.Length} times: {trail}");
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
            TreeViewItem { DataContext: ConnectionsRowViewModel row } => $"Connections row {row.Row?.Path ?? row.Id}",
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
