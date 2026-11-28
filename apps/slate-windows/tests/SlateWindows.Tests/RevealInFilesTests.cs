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

    /// <summary>The merge judge's minor 1, with the red team's M3: the reader's
    /// row selected with the keys on it, a Reveal waiting on a held folder, and
    /// F2 pressed in the same pass as the folder's publication, which runs
    /// first. F2 is the newer intent and ends the wait, so the publication seats
    /// nothing: the name field takes the keys holding the reader's row's name,
    /// never the revealed note's, and Enter renames the reader's note.</summary>
    [Fact]
    public void F2InTheRevealsPublicationPassEndsTheReveal() => StaThread.RunPumped(() =>
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
        Assert.Equal(["a.md"], nameFieldHeld);
        Assert.True(nameField.IsKeyboardFocused, "the name field does not hold the keys");
        shell.AssertNeverSeated("docs/n.md");
        nameField.Text = "renamed.md";
        _ = shell.PressWithoutPumping(Key.Enter);
        shell.SettleLoads();

        Assert.True(File.Exists(Path.Combine(shell.Host.Root, "docs", "n.md")), "Enter renamed the revealed note");
        Assert.True(File.Exists(Path.Combine(shell.Host.Root, "renamed.md")), "Enter did not rename the reader's note");
        Assert.False(File.Exists(Path.Combine(shell.Host.Root, "a.md")), "the reader's note kept its name");
    });

    /// <summary>The red team's M1: two tabs in one group share one editor, so a
    /// tab switch leaves the keys on the same element. A Reveal asked from one
    /// tab's editor, then Previous Tab: the keys are in another note's editor,
    /// so the late seat selects its row silently and opens nothing over that
    /// note.</summary>
    [Fact]
    public void ALateRevealAfterATabSwitchSelectsItsRowSilently() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.Host.Workspace.OpenPath("a.md");
        shell.Host.Settle();
        shell.Host.Workspace.OpenPath("park/here.md", WorkspaceOpenTarget.NewTab);
        shell.Host.Settle();
        UIElement editor = shell.Editor;
        Assert.True(editor.Focus(), "premise: the editor did not take the keys");
        shell.HoldTreeWork();
        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));
        IInputElement keys = Keyboard.FocusedElement;

        shell.Host.Workspace.PreviousTabCommand.Execute(null);
        shell.Host.Settle();
        Assert.Equal("a.md", shell.Host.Workspace.ActiveGroup.ActiveTab?.Path);
        Assert.Same(keys, Keyboard.FocusedElement);
        shell.Mark();
        string tabs = shell.Tabs;
        shell.ReleaseTreeWork();
        shell.SettleLoads();

        shell.AssertSilentlySelected("docs/n.md", tabs);
        Assert.Same(keys, Keyboard.FocusedElement);
    });

    /// <summary>The red team's M2: the keys moved into the editor after a
    /// header segment's Reveal, then the window's keyboard focus is cleared
    /// (as when another app is active), its logical focus still on the editor.
    /// Null keys are not stranded while the window keeps a focused element: the
    /// late seat selects its row silently.</summary>
    [Fact]
    public void ALateRevealWithTheKeysClearedSelectsItsRowSilently() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        _ = shell.OpenAndParkKeysOnASegment("park/here.md", "park");
        shell.HoldTreeWork();
        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));
        UIElement editor = shell.Editor;
        Assert.True(editor.Focus(), "premise: the editor did not take the keys");
        Keyboard.ClearFocus();
        PumpedDispatcher.Drain();
        Assert.Null(Keyboard.FocusedElement);
        Assert.True(
            FocusManager.GetFocusedElement(shell.Host.Shell) is Visual logical
                && (ReferenceEquals(logical, editor) || editor.IsAncestorOf(logical) || logical.IsAncestorOf(editor)),
            $"premise: the window's logical focus is on {FocusManager.GetFocusedElement(shell.Host.Shell)}, not the editor");
        shell.Mark();
        string tabs = shell.Tabs;
        shell.ReleaseTreeWork();
        shell.SettleLoads();

        shell.AssertSilentlySelected("docs/n.md", tabs);
    });

    /// <summary>The red team's M3: F2 on the reader's a.md and a new name
    /// typed while a Reveal waits on a held folder. F2 ends the wait: the
    /// folder's listing seats nothing, the field keeps the keys and the text,
    /// nothing opens or is said, and Enter renames a.md.</summary>
    [Fact]
    public void AnInlineRenameKeepsItsFieldThroughALateReveal() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.SelectAndFocusRow("a.md");
        TextBox nameField = shell.Host.Shell.SidebarMutationNameTextBox;
        shell.HoldTreeWork();
        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));
        Assert.True(shell.PressWithoutPumping(Key.F2), "premise: F2 on the selected row went unhandled");
        PumpedDispatcher.Drain();
        Assert.True(nameField.IsKeyboardFocused, "premise: F2 did not put the keys in the name field");
        nameField.Text = "renamed.md";
        shell.Mark();
        string tabs = shell.Tabs;

        shell.ReleaseTreeWork();
        shell.SettleLoads();

        Assert.True(nameField.IsKeyboardFocused, "the late seat took the keys out of the name field");
        Assert.Equal("renamed.md", nameField.Text);
        Assert.Empty(shell.FocusChanges);
        Assert.Empty(shell.OpensSinceMark);
        Assert.Empty(shell.SaidSinceMark);
        Assert.Equal(tabs, shell.Tabs);
        shell.AssertNeverSeated("docs/n.md");
        _ = shell.PressWithoutPumping(Key.Enter);
        shell.SettleLoads();
        Assert.True(File.Exists(Path.Combine(shell.Host.Root, "renamed.md")), "Enter did not rename a.md");
        Assert.True(File.Exists(Path.Combine(shell.Host.Root, "docs", "n.md")), "Enter renamed the revealed note");
    });

    /// <summary>Codex round 6: a duplicate of the reader's a.md with its
    /// publication held, then F2 on a.md and a new name typed. F2 fixes the
    /// rename's target and is the newer intent: the duplicate's named row
    /// drops, so its publication neither selects the copy nor overwrites the
    /// name field, and Enter renames a.md.</summary>
    [Fact]
    public void F2AfterAHeldDuplicateRenamesTheReadersRow() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.SelectAndFocusRow("a.md");
        TextBox nameField = shell.Host.Shell.SidebarMutationNameTextBox;
        shell.HoldTreeWork();
        shell.Sidebar.DuplicateSelected();
        Assert.True(File.Exists(Path.Combine(shell.Host.Root, "a copy.md")), "premise: the duplicate wrote no copy");
        PumpedDispatcher.Drain();
        Assert.Equal("a.md", shell.Sidebar.SelectedNode?.Path);
        Assert.True(shell.PressWithoutPumping(Key.F2), "premise: F2 on the selected row went unhandled");
        PumpedDispatcher.Drain();
        Assert.True(nameField.IsKeyboardFocused, "premise: F2 did not put the keys in the name field");
        nameField.Text = "renamed.md";
        shell.Mark();

        shell.ReleaseTreeWork();
        shell.SettleLoads();

        Assert.Equal("a.md", shell.Sidebar.SelectedNode?.Path);
        Assert.False(shell.Row("a copy.md").IsSelected, "the copy holds the selection");
        Assert.True(nameField.IsKeyboardFocused, "the publication took the keys out of the name field");
        Assert.Equal("renamed.md", nameField.Text);
        Assert.Empty(shell.FocusChanges);
        _ = shell.PressWithoutPumping(Key.Enter);
        shell.SettleLoads();
        Assert.True(File.Exists(Path.Combine(shell.Host.Root, "renamed.md")), "Enter did not rename a.md");
        Assert.False(File.Exists(Path.Combine(shell.Host.Root, "a.md")), "a.md kept its name");
        Assert.True(File.Exists(Path.Combine(shell.Host.Root, "a copy.md")), "Enter renamed the copy");
    });

    /// <summary>Codex round 6: a create with its publication held, then F2 on
    /// the reader's a.md and a new name typed. F2 is the newer intent: the
    /// create's rename arm drops, so its publication neither selects the new
    /// note nor re-arms the rename into it, and Enter renames a.md.</summary>
    [Fact]
    public void F2AfterAHeldCreateRenamesTheReadersRow() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.SelectAndFocusRow("a.md");
        TextBox nameField = shell.Host.Shell.SidebarMutationNameTextBox;
        shell.HoldTreeWork();
        shell.Sidebar.CreateNoteCommand.Execute(null);
        Assert.True(File.Exists(Path.Combine(shell.Host.Root, "Untitled.md")), "premise: the create wrote no note");
        PumpedDispatcher.Drain();
        shell.SelectAndFocusRow("a.md");
        Assert.True(shell.PressWithoutPumping(Key.F2), "premise: F2 on the selected row went unhandled");
        PumpedDispatcher.Drain();
        Assert.True(nameField.IsKeyboardFocused, "premise: F2 did not put the keys in the name field");
        nameField.Text = "renamed.md";
        int rearmed = 0;
        shell.Sidebar.InlineRenameRequested += () => rearmed++;
        shell.Mark();

        shell.ReleaseTreeWork();
        shell.SettleLoads();

        Assert.Equal(0, rearmed);
        Assert.Equal("a.md", shell.Sidebar.SelectedNode?.Path);
        Assert.False(shell.Row("Untitled.md").IsSelected, "the created note holds the selection");
        Assert.True(nameField.IsKeyboardFocused, "the publication took the keys out of the name field");
        Assert.Equal("renamed.md", nameField.Text);
        Assert.Empty(shell.FocusChanges);
        _ = shell.PressWithoutPumping(Key.Enter);
        shell.SettleLoads();
        Assert.True(File.Exists(Path.Combine(shell.Host.Root, "renamed.md")), "Enter did not rename a.md");
        Assert.True(File.Exists(Path.Combine(shell.Host.Root, "Untitled.md")), "Enter renamed the created note");
    });

    /// <summary>Codex round 7: a duplicate of the reader's a.md with its
    /// publication held, then a newer Reveal, then the keys moved into the
    /// editor and the keyboard focus cleared, the window's logical focus still
    /// there. The newer intent retires the duplicate's focus restore, so its
    /// publication queues no fallback landing; the late seat selects its row
    /// silently, and nothing takes the keys, opens or is said.</summary>
    [Fact]
    public void ALateRevealAfterAHeldDuplicateAndClearedKeysStaysSilent() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.SelectAndFocusRow("a.md");
        shell.HoldTreeWork();
        shell.Sidebar.DuplicateSelected();
        Assert.True(File.Exists(Path.Combine(shell.Host.Root, "a copy.md")), "premise: the duplicate wrote no copy");
        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));
        UIElement editor = shell.Editor;
        Assert.True(editor.Focus(), "premise: the editor did not take the keys");
        Keyboard.ClearFocus();
        PumpedDispatcher.Drain();
        Assert.Null(Keyboard.FocusedElement);
        Assert.True(
            FocusManager.GetFocusedElement(shell.Host.Shell) is Visual logical
                && (ReferenceEquals(logical, editor) || editor.IsAncestorOf(logical) || logical.IsAncestorOf(editor)),
            $"premise: the window's logical focus is on {FocusManager.GetFocusedElement(shell.Host.Shell)}, not the editor");
        shell.Mark();
        string tabs = shell.Tabs;

        shell.ReleaseTreeWork();
        shell.SettleLoads();
        shell.Sidebar.Refresh();
        shell.SettleLoads();

        Assert.Null(Keyboard.FocusedElement);
        shell.AssertSilentlySelected("docs/n.md", tabs);
    });

    /// <summary>Codex round 7: a batch trash of the reader's selected b.md,
    /// then a newer Reveal asked while its work runs (the keys on a note
    /// header's segment), then the trash's own request at the end of its work
    /// (the selected row was trashed), which is stale. The keys then move into
    /// the editor and the keyboard focus is cleared. The stale request arms no
    /// focus restore, so its publication queues no fallback landing: the
    /// Reveal's outcome is all that happens, its row selected silently, nothing
    /// taking the keys, opening or being said.</summary>
    [Fact]
    public void AStaleTrashRequestLeavesOnlyTheNewerRevealsOutcome() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.SelectAndFocusRow("b.md");
        shell.Row("b.md").IsBatchSelected = true;
        _ = shell.OpenAndParkKeysOnASegment("park/here.md", "park");
        bool revealed = false;
        shell.Sidebar.TrashStagedForTesting = _ => revealed = shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md");
        shell.HoldTreeWork();

        shell.Sidebar.BatchTrashCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => shell.Sidebar.TrashCompletion.IsCompleted), "the trash never finished");
        Assert.True(revealed, "premise: the Reveal was not asked during the trash");
        Assert.False(File.Exists(Path.Combine(shell.Host.Root, "b.md")), "premise: the trash left b.md");
        UIElement editor = shell.Editor;
        Assert.True(editor.Focus(), "premise: the editor did not take the keys");
        Keyboard.ClearFocus();
        PumpedDispatcher.Drain();
        Assert.Null(Keyboard.FocusedElement);
        Assert.NotNull(FocusManager.GetFocusedElement(shell.Host.Shell));
        shell.Mark();
        string tabs = shell.Tabs;

        shell.ReleaseTreeWork();
        shell.SettleLoads();
        shell.Sidebar.Refresh();
        shell.SettleLoads();

        Assert.Null(Keyboard.FocusedElement);
        shell.AssertSilentlySelected("docs/n.md", tabs);
    });

    /// <summary>Codex round 8: a rename's publication restores the selection
    /// with the keys outside the tree, so the window queues its fallback
    /// landing at Input priority. Before it runs, a newer Reveal of a listed
    /// note runs at Normal priority and lands, and then the keys move into the
    /// editor and the keyboard focus is cleared, the window's logical focus
    /// still there. The older fallback stands down: nothing lands in Files
    /// after the Reveal's own landing.</summary>
    [Fact]
    public void ARestoresQueuedFallbackGivesWayToANewerReveal() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.SelectAndFocusRow("b.md");
        shell.HoldTreeWork();
        shell.Sidebar.MutationName = "bb.md";
        Assert.True(shell.Sidebar.TryRenameSelected(), "premise: the rename failed");
        Assert.True(shell.Editor.Focus(), "premise: the editor did not take the keys");
        shell.Mark();
        int? movesAfterTheReveal = null;
        bool armed = true;
        shell.Sidebar.TreeSelectionRestored += () =>
        {
            if (!armed)
            {
                return;
            }

            armed = false;
            _ = shell.Host.Shell.Dispatcher.InvokeAsync(
                () =>
                {
                    _ = shell.Host.Workspace.RevealConnectionsRowFromSurface("a.md");
                    _ = shell.Editor.Focus();
                    Keyboard.ClearFocus();
                    movesAfterTheReveal = shell.FocusChanges.Count;
                },
                DispatcherPriority.Normal);
        };

        shell.ReleaseTreeWork();
        shell.SettleLoads();

        Assert.True(movesAfterTheReveal is not null, "premise: the newer Reveal never ran");
        Assert.Equal("a.md", shell.Sidebar.SelectedNode?.Path);
        Assert.Contains("a.md", shell.OpensSinceMark);
        Assert.Null(Keyboard.FocusedElement);
        Assert.True(
            shell.FocusChanges.Count == movesAfterTheReveal,
            $"the keys moved after the Reveal: {string.Join(" -> ", shell.FocusChanges.Skip(movesAfterTheReveal!.Value).Select(focus => focus.ToString()))}");
    });

    /// <summary>The deferred-work table, the restore's retirement: a filter
    /// active, a duplicate of the reader's a.md with its publication held,
    /// then a newer Reveal asked from a note header's segment, then the keys
    /// moved into the editor and the keyboard focus cleared. The Reveal ends
    /// at its seat (silent under a filter is an end) and takes no intent, so
    /// only the retirement keeps the duplicate's restore from queueing a
    /// fallback that would land the cleared keys in Files.</summary>
    [Fact]
    public void AnEndedRevealLeavesAHeldDuplicatesRestoreRetired() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.SelectAndFocusRow("a.md");
        shell.Sidebar.FilterText = "b";
        PumpedDispatcher.PumpUntilDrained(shell.Sidebar.FilterCompletion);
        shell.Host.Settle();
        _ = shell.OpenAndParkKeysOnASegment("park/here.md", "park");
        shell.HoldTreeWork();
        shell.Sidebar.DuplicateSelected();
        Assert.True(File.Exists(Path.Combine(shell.Host.Root, "a copy.md")), "premise: the duplicate wrote no copy");
        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));
        Assert.True(shell.Editor.Focus(), "premise: the editor did not take the keys");
        Keyboard.ClearFocus();
        PumpedDispatcher.Drain();
        Assert.Null(Keyboard.FocusedElement);
        Assert.NotNull(FocusManager.GetFocusedElement(shell.Host.Shell));
        shell.Mark();

        shell.ReleaseTreeWork();
        shell.SettleLoads();

        Assert.Null(Keyboard.FocusedElement);
        Assert.Empty(shell.FocusChanges);
        Assert.Empty(shell.OpensSinceMark);
        Assert.Equal("b", shell.Sidebar.FilterText);
        Assert.Equal("a.md", shell.Sidebar.SelectedNode?.Path);
        shell.AssertNeverSeated("docs/n.md");
    });

    /// <summary>The deferred-work table, the stale request: nothing selected,
    /// a filter active, b.md checked and batch-trashed, a newer Reveal asked
    /// during the trash's work from a note header's segment, so the trash's
    /// request at its end is stale; then the keys moved into the editor and
    /// the keyboard focus cleared. The Reveal ends at its seat and takes no
    /// intent, so only the stale request's rejection keeps a restore from
    /// queueing a fallback that would land the cleared keys in Files.</summary>
    [Fact]
    public void AStaleTrashRequestUnderAnEndedRevealLandsNothing() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        Assert.Null(shell.Sidebar.SelectedNode);
        shell.Sidebar.FilterText = "b";
        PumpedDispatcher.PumpUntilDrained(shell.Sidebar.FilterCompletion);
        shell.Host.Settle();
        shell.Row("b.md").IsBatchSelected = true;
        _ = shell.OpenAndParkKeysOnASegment("park/here.md", "park");
        bool revealed = false;
        shell.Sidebar.TrashStagedForTesting = _ => revealed = shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md");
        shell.HoldTreeWork();

        shell.Sidebar.BatchTrashCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => shell.Sidebar.TrashCompletion.IsCompleted), "the trash never finished");
        Assert.True(revealed, "premise: the Reveal was not asked during the trash");
        Assert.False(File.Exists(Path.Combine(shell.Host.Root, "b.md")), "premise: the trash left b.md");
        Assert.True(shell.Editor.Focus(), "premise: the editor did not take the keys");
        Keyboard.ClearFocus();
        PumpedDispatcher.Drain();
        Assert.Null(Keyboard.FocusedElement);
        Assert.NotNull(FocusManager.GetFocusedElement(shell.Host.Shell));
        shell.Mark();

        shell.ReleaseTreeWork();
        shell.SettleLoads();

        Assert.Null(Keyboard.FocusedElement);
        Assert.Empty(shell.FocusChanges);
        Assert.Empty(shell.OpensSinceMark);
        Assert.Equal("b", shell.Sidebar.FilterText);
        shell.AssertNeverSeated("docs/n.md");
    });

    /// <summary>The deferred-work table's Move To finding: a Reveal waiting on
    /// a held folder, then Move To opened on the reader's a.md, the reader's
    /// gesture on the selected row and a newer intent; the folder lists while
    /// the sheet is open. The open ended the Reveal, so it never seats. A
    /// cancel returns the keys to a.md's row with a.md still selected; a
    /// confirm moves a.md, and its moved row seats as before.</summary>
    [Theory]
    [InlineData("cancel")]
    [InlineData("confirm")]
    public void MoveToOpenedWhileARevealWaitsEndsIt(string close) => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.SelectAndFocusRow("a.md");
        shell.HoldTreeWork();
        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));

        shell.Sidebar.MoveToCommand.Execute(null);
        PumpedDispatcher.PumpUntilDrained(shell.Sidebar.MoveToCompletion);
        shell.Host.Settle();
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(shell.Sidebar.MoveToSheet);
        Assert.True(picker.IsReady, "premise: the picker did not load its folders");
        shell.ReleaseTreeWork();
        shell.SettleLoads();

        Assert.Same(picker, shell.Sidebar.MoveToSheet);
        Assert.Equal("a.md", shell.Sidebar.SelectedNode?.Path);
        if (close == "cancel")
        {
            picker.CancelCommand.Execute(null);
            shell.SettleLoads();

            Assert.Equal("a.md", shell.Sidebar.SelectedNode?.Path);
            Assert.Equal("tree row a.md", shell.KeysAt);
        }
        else
        {
            picker.ActivateCommand.Execute(picker.Rows.Single(row => row.Destination == "docs"));
            shell.SettleLoads();

            Assert.True(File.Exists(Path.Combine(shell.Host.Root, "docs", "a.md")), "premise: the move left a.md");
            Assert.Equal("docs/a.md", shell.Sidebar.SelectedNode?.Path);
            Assert.True(shell.Row("docs/a.md").IsSelected, "the moved row's node does not hold the selection");
        }

        shell.AssertNeverSeated("docs/n.md");
    });

    /// <summary>Codex round 9, an open's queued editor landing: the reader
    /// opens a.md from its row, the explicit open that queues the editor's
    /// landing, and a Reveal of the listed b.md is asked before the dispatcher
    /// drains. The Reveal lands on b.md's row and opens b.md as a selection,
    /// which asks for no editor landing of its own. The older landing gives way
    /// to the newer intent, so the keys stay on b.md's row. With nothing after
    /// the open, the landing puts the keys in the editor.</summary>
    [Theory]
    [InlineData("reveal")]
    [InlineData("nothing")]
    public void AnOpensQueuedEditorLandingGivesWayToANewerReveal(string after) => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell();
        shell.SelectAndFocusRow("a.md");
        shell.Mark();

        shell.Sidebar.OpenCurrentCommand.Execute(null);
        if (after == "reveal")
        {
            Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("b.md"));
        }

        shell.Host.Settle();

        if (after == "reveal")
        {
            Assert.Equal("tree row b.md", shell.KeysAt);
            shell.AssertSelectedOnBothHalves("b.md");
            Assert.Equal(["a.md", "b.md"], shell.OpensSinceMark);
            Assert.DoesNotContain(shell.FocusChanges, focus => focus is ICSharpCode.AvalonEdit.Editing.TextArea);
        }
        else
        {
            Assert.Equal(["a.md"], shell.OpensSinceMark);
            Assert.True(shell.Editor.IsKeyboardFocusWithin, $"premise: the open's landing did not put the keys in the editor; they are on {shell.KeysAt}");
        }
    });

    /// <summary>Codex round 9, Move To's queued focus return: Move To opened
    /// from the File actions pane's Move to button with the reader's a.md
    /// selected and docs listed. A cancel queues the return to the button; a Reveal of the
    /// listed b.md asked before it runs lands on b.md's row, a newer intent,
    /// and the return gives way: the keys stay on b.md's row. A confirm takes
    /// the move's own number before the sheet closes, so its return still runs
    /// and puts the keys back on the button, for one moved row and for checked
    /// rows; the moved row is selected.</summary>
    [Theory]
    [InlineData("cancel")]
    [InlineData("confirm")]
    [InlineData("confirm checked")]
    public void MoveTosQueuedFocusReturnGivesWayToANewerReveal(string close) => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.SelectAndFocusRow("a.md");
        shell.Row("docs").IsExpanded = true;
        shell.SettleLoads();
        Assert.Contains(shell.Row("docs").Children, child => child.Path == "docs/n.md");
        if (close == "confirm checked")
        {
            shell.Row("a.md").IsBatchSelected = true;
            shell.Row("b.md").IsBatchSelected = true;
        }

        Button moveTo = shell.FileActionsButton("Move to…");
        Assert.True(moveTo.Focus(), "premise: the Move to button refused the keys");
        Invoke(moveTo);
        PumpedDispatcher.PumpUntilDrained(shell.Sidebar.MoveToCompletion);
        shell.Host.Settle();
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(shell.Sidebar.MoveToSheet);
        Assert.True(picker.IsReady, "premise: the picker did not load its folders");
        Assert.False(moveTo.IsKeyboardFocused, "premise: the sheet did not take the keys");
        shell.Mark();

        if (close == "cancel")
        {
            picker.CancelCommand.Execute(null);
            Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("b.md"));
            shell.SettleLoads();

            Assert.Equal("tree row b.md", shell.KeysAt);
            shell.AssertSelectedOnBothHalves("b.md");
            Assert.DoesNotContain(moveTo, shell.FocusChanges);
            return;
        }

        shell.HoldTreeWork();
        picker.ActivateCommand.Execute(picker.Rows.Single(row => row.Destination == "docs"));
        PumpedDispatcher.Drain();
        Assert.True(moveTo.IsKeyboardFocused, $"the move's focus return did not run; the keys are on {shell.KeysAt}");
        shell.ReleaseTreeWork();
        shell.SettleLoads();

        Assert.True(File.Exists(Path.Combine(shell.Host.Root, "docs", "a.md")), "premise: the move left a.md");
        if (close == "confirm")
        {
            Assert.Equal("docs/a.md", shell.Sidebar.SelectedNode?.Path);
        }
        else
        {
            Assert.True(File.Exists(Path.Combine(shell.Host.Root, "docs", "b.md")), "premise: the move left b.md");
        }

        Assert.True(moveTo.IsKeyboardFocused, $"the keys left the button after the move's return; they are on {shell.KeysAt}");
    });

    /// <summary>The red team's M3, the pane's other fields: the reader puts
    /// the keys in the filter field while a Reveal waits. The Files pane's
    /// fields, lists and buttons outside the tree are the reader's own work
    /// there: the wait ends, nothing is selected, opened or said, and the keys
    /// stay in the field.</summary>
    [Fact]
    public void ALateRevealWithTheKeysInTheFilterFieldEndsUnseated() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.OpenAndParkKeysInTheEditor();
        shell.HoldTreeWork();
        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));
        TextBox filterField = shell.Host.Shell.SidebarFilterTextBox;
        Assert.True(filterField.Focus(), "premise: the filter field refused the keys");
        shell.Mark();
        string tabs = shell.Tabs;
        FileTreeNodeViewModel? selected = shell.Sidebar.SelectedNode;

        shell.ReleaseTreeWork();
        shell.SettleLoads();

        Assert.True(filterField.IsKeyboardFocused, "the late seat took the keys out of the filter field");
        Assert.Same(selected, shell.Sidebar.SelectedNode);
        Assert.Empty(shell.FocusChanges);
        Assert.Empty(shell.OpensSinceMark);
        Assert.Empty(shell.SaidSinceMark);
        Assert.Equal(tabs, shell.Tabs);
        shell.AssertNeverSeated("docs/n.md");
    });

    /// <summary>The red team's minor 1: the Connections leaf republishes while
    /// the Reveal waits (a depth change), rebuilding its rows, and keeps the
    /// keys in its tree on a new element. The keys are still in the place the
    /// gesture left them, so the late seat lands them once, and the selection
    /// is said.</summary>
    [Fact]
    public void ALateRevealAfterTheLeafRepublishesLandsOnce() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        ConnectionsLeafView surface = shell.KeysOnTheConnectionsRowOf("start.md", "docs/n.md", out TreeViewItem row);
        GraphConnectionRow target = ((ConnectionsRowViewModel)row.DataContext).Row!;
        shell.HoldTreeWork();
        shell.Host.Workspace.Connections.Execute(GraphRowAction.Reveal, target);

        shell.Host.Workspace.Connections.Deeper();
        shell.SettleLeaf();
        // The republish rebuilds the rows and keeps the keys in the leaf's
        // tree, on a new element (its own landing: the selected row, else the
        // first).
        Assert.True(
            Keyboard.FocusedElement is TreeViewItem now && surface.TreeForTests.IsAncestorOf(now) && !ReferenceEquals(now, row),
            $"premise: the republish did not keep the keys in the leaf's tree on a new element; they are on {Keyboard.FocusedElement}");
        shell.Mark();
        shell.ReleaseTreeWork();
        shell.SettleLoads();

        shell.AssertLandedOnceOn("docs/n.md", opens: "docs/n.md", said: "Selected: n.md");
    });

    /// <summary>The red team's minor 2: the Files pane hidden while the Reveal
    /// waits, the keys still on the graph table. Nothing can land, so the seat
    /// selects its row silently: no open, nothing said, and the graph tab stays
    /// active.</summary>
    [Fact]
    public void ALateRevealWhileFilesIsHiddenSelectsItsRowSilently() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.Host.Workspace.OpenGraph();
        PumpedDispatcher.PumpUntilDrained(shell.Host.Workspace.GraphDocument!.WhenAllWorkDrained());
        shell.Host.Settle();
        DataGrid table = ShownShell.Descendants(shell.Host.Shell).OfType<DataGrid>().First(grid => grid.IsVisible);
        Assert.True(table.Focus() || table.IsKeyboardFocusWithin, "premise: the graph table did not take the keys");
        shell.HoldTreeWork();
        Assert.True(shell.Host.Workspace.RevealGraphRowFromSurface("docs/n.md"));
        shell.Host.Workspace.IsFilesSidebarVisible = false;
        shell.Host.Settle();
        Assert.True(table.IsKeyboardFocusWithin, "premise: hiding Files moved the keys off the graph table");
        shell.Mark();
        string tabs = shell.Tabs;

        shell.ReleaseTreeWork();
        shell.SettleLoads();

        Assert.True(table.IsKeyboardFocusWithin, "the keys left the graph table");
        Assert.Equal(tabs, shell.Tabs);
        Assert.Empty(shell.FocusChanges);
        Assert.Empty(shell.OpensSinceMark);
        Assert.Empty(shell.SaidSinceMark);
        FileTreeNodeViewModel revealed = shell.Row("docs/n.md");
        Assert.Same(revealed, shell.Sidebar.SelectedNode);
        Assert.True(revealed.IsSelected, "the revealed row's node is not selected");
    });

    /// <summary>Codex round 5: the Files pane hidden while the Reveal waits,
    /// and then no element holds the keys or the window's logical focus.
    /// Stranded keys do not outrank the hidden pane: nothing can land, so the
    /// row is selected silently, with no open, no announcement and no focus
    /// change. With a filter or a tag scope active the wait ends instead, the
    /// filter kept and nothing said.</summary>
    [Theory]
    [InlineData("none")]
    [InlineData("text")]
    [InlineData("tag")]
    public void ALateRevealWhileFilesIsHiddenAndTheKeysStrandedStaysSilent(string filter) => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        if (filter == "text")
        {
            shell.Sidebar.FilterText = "b";
        }
        else if (filter == "tag")
        {
            shell.Sidebar.ActivateTag("alpha");
        }

        PumpedDispatcher.PumpUntilDrained(shell.Sidebar.FilterCompletion);
        shell.Host.Settle();
        string filterText = shell.Sidebar.FilterText;
        string? scopeTag = shell.Sidebar.ScopeTag;
        shell.OpenAndParkKeysInTheEditor();
        shell.HoldTreeWork();
        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));
        shell.Host.Workspace.IsFilesSidebarVisible = false;
        shell.Host.Settle();
        FocusManager.SetFocusedElement(shell.Host.Shell, null);
        Keyboard.ClearFocus();
        PumpedDispatcher.Drain();
        Assert.Null(Keyboard.FocusedElement);
        Assert.Null(FocusManager.GetFocusedElement(shell.Host.Shell));
        shell.Mark();
        string tabs = shell.Tabs;
        FileTreeNodeViewModel? selected = shell.Sidebar.SelectedNode;

        shell.ReleaseTreeWork();
        shell.SettleLoads();

        Assert.Empty(shell.FocusChanges);
        Assert.Empty(shell.OpensSinceMark);
        Assert.Empty(shell.SaidSinceMark);
        Assert.Equal(tabs, shell.Tabs);
        Assert.Equal(filterText, shell.Sidebar.FilterText);
        Assert.Equal(scopeTag, shell.Sidebar.ScopeTag);
        if (filter == "none")
        {
            FileTreeNodeViewModel revealed = shell.Row("docs/n.md");
            Assert.Same(revealed, shell.Sidebar.SelectedNode);
            Assert.True(revealed.IsSelected, "the revealed row's node is not selected");
        }
        else
        {
            Assert.True(shell.Sidebar.IsFilterActive, "the filter was cleared");
            Assert.Same(selected, shell.Sidebar.SelectedNode);
            shell.AssertNeverSeated("docs/n.md");
        }
    });

    /// <summary>The red team's minor 3: a filter or a tag scope active, and
    /// the keys moved off the gesture's place. A silent seat would select a
    /// row the filter hides while the results show the reader's, so the wait
    /// ends instead: nothing changes, the filter stays, nothing is
    /// said.</summary>
    [Theory]
    [InlineData("text")]
    [InlineData("tag")]
    public void ALateRevealUnderAFilterEndsUnseated(string filter) => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
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
        string filterText = shell.Sidebar.FilterText;
        string? scopeTag = shell.Sidebar.ScopeTag;
        _ = shell.OpenAndParkKeysOnASegment("park/here.md", "park");
        shell.HoldTreeWork();
        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));
        UIElement editor = shell.Editor;
        Assert.True(editor.Focus(), "premise: the editor did not take the keys");
        shell.Mark();
        string tabs = shell.Tabs;
        FileTreeNodeViewModel? selected = shell.Sidebar.SelectedNode;

        shell.ReleaseTreeWork();
        shell.SettleLoads();

        Assert.True(shell.Sidebar.IsFilterActive, "the filter was cleared");
        Assert.Equal(filterText, shell.Sidebar.FilterText);
        Assert.Equal(scopeTag, shell.Sidebar.ScopeTag);
        Assert.Same(selected, shell.Sidebar.SelectedNode);
        Assert.True(editor.IsKeyboardFocused, "the keys left the editor");
        Assert.Empty(shell.FocusChanges);
        Assert.Empty(shell.OpensSinceMark);
        Assert.Empty(shell.SaidSinceMark);
        Assert.Equal(tabs, shell.Tabs);
        shell.AssertNeverSeated("docs/n.md");
    });

    /// <summary>The red team's minor 5, the stranded clause: the keys moved
    /// off the header segment and then cleared, with no focused element left
    /// in the window. Stranded keys are taken: the late seat lands them once,
    /// opens and says the note.</summary>
    [Fact]
    public void ALateRevealWithTheKeysStrandedLandsThemOnce() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        _ = shell.OpenAndParkKeysOnASegment("park/here.md", "park");
        shell.HoldTreeWork();
        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));
        Assert.True(shell.Editor.Focus(), "premise: the editor did not take the keys");
        FocusManager.SetFocusedElement(shell.Host.Shell, null);
        Keyboard.ClearFocus();
        PumpedDispatcher.Drain();
        Assert.Null(Keyboard.FocusedElement);
        Assert.Null(FocusManager.GetFocusedElement(shell.Host.Shell));
        shell.Mark();

        shell.ReleaseTreeWork();
        shell.SettleLoads();

        shell.AssertLandedOnceOn("docs/n.md", opens: "docs/n.md", said: "Selected: n.md");
    });

    /// <summary>The red team's minor 5, the in-Files clause: the keys moved
    /// off the header segment onto another row of the Files tree, unselected.
    /// Keys already in the tree are taken: the late seat lands them once on
    /// its row.</summary>
    [Fact]
    public void ALateRevealWithTheKeysOnAnotherFilesRowLandsThemOnce() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        _ = shell.OpenAndParkKeysOnASegment("park/here.md", "park");
        shell.HoldTreeWork();
        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));
        shell.FocusRowUnselected("a.md");
        Assert.NotEqual("a.md", shell.Sidebar.SelectedNode?.Path);
        shell.Mark();

        shell.ReleaseTreeWork();
        shell.SettleLoads();

        shell.AssertLandedOnceOn("docs/n.md", opens: "docs/n.md", said: "Selected: n.md");
    });

    /// <summary>The red team's minor 7: a Reveal asked from inside the seat's
    /// own landing (as the window's restore runs). The seat's own open is not
    /// the reader's open, so it does not end that newer Reveal, which seats
    /// after it.</summary>
    [Fact]
    public void ARevealAskedFromTheSeatsOwnLandingSeatsAfterIt() => StaThread.RunPumped(() =>
    {
        using var shell = new RevealShell(heldTreeWork: true);
        shell.OpenAndParkKeysInTheEditor();
        shell.HoldTreeWork();
        Assert.True(shell.Host.Workspace.RevealConnectionsRowFromSurface("docs/n.md"));
        bool asked = false;
        shell.Host.Shell.FilesTree.GotKeyboardFocus += (_, _) =>
        {
            if (!asked)
            {
                asked = true;
                _ = shell.Host.Workspace.RevealConnectionsRowFromSurface("b.md");
            }
        };

        shell.ReleaseTreeWork();
        shell.SettleLoads();

        Assert.True(asked, "premise: the seat's landing never reached the Files tree");
        Assert.Equal(["docs/n.md", "b.md"], shell.OpensSinceMark);
        Assert.Equal("b.md", shell.Sidebar.SelectedNode?.Path);
        Assert.True(shell.Row("b.md").IsSelected, "b.md's node does not hold the selection");
        Assert.Equal("tree row b.md", shell.KeysAt);
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

        /// <summary>Where the keys are, described.</summary>
        public string KeysAt => Describe(Keyboard.FocusedElement);

        /// <summary>The File actions pane's button that says
        /// <paramref name="content"/>, the pane expanded.</summary>
        public Button FileActionsButton(string content)
        {
            Host.Shell.SidebarFileActionsExpander.IsExpanded = true;
            Host.Shell.UpdateLayout();
            return ShownShell.Descendants(Host.Shell.SidebarFileActionsExpander).OfType<Button>()
                .Single(candidate => candidate.IsVisible && Equals(candidate.Content, content));
        }

        /// <summary>The reader puts the keys on the row at
        /// <paramref name="path"/> without selecting it, as a landing
        /// does.</summary>
        public void FocusRowUnselected(string path)
        {
            Host.Shell.UpdateLayout();
            TreeViewItem? container = ContainerOf(Row(path));
            Assert.True(container is not null && LandingTreeViewItem.FocusUnselected(container), $"premise: {path}'s row did not take the keys");
            PumpedDispatcher.Drain();
        }

        /// <summary>A late seat's silent arm: since the mark nothing moved the
        /// keys, opened or was said, the tabs are <paramref name="tabs"/>, and
        /// the row at <paramref name="path"/> holds the selection on both
        /// halves.</summary>
        public void AssertSilentlySelected(string path, string tabs)
        {
            Assert.Empty(FocusChanges);
            Assert.Empty(OpensSinceMark);
            Assert.Empty(SaidSinceMark);
            Assert.Equal(tabs, Tabs);
            AssertSelectedOnBothHalves(path);
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
