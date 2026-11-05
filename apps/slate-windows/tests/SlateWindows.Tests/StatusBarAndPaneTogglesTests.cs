// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SlateWindows.Canvas;
using SlateWindows.Commands;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// DS-7 (#1808): the status bar is the design's 24 row with a pane toggle in each
/// corner, the Files sidebar can be hidden, and hiding either pane gives the editor
/// its room. A <see cref="ToggleButton"/> has two input paths and they differ: a click
/// runs its command, UIA's Toggle doesn't, so both go through the two-way binding.
/// </summary>
public sealed class StatusBarAndPaneTogglesTests
{
    private const string FilesToggleId = "FilesSidebarToggle";
    private const string RightToggleId = "RightPaneToggle";
    private const string Click = "a click";
    private const string UiaToggle = "UIA's Toggle";

    [Fact]
    public void HidingTheFilesSidebarGivesTheEditorItsRoom() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        WorkspaceViewModel workspace = host.Workspace;
        double shown = host.Shell.ContentPaneBorder.ActualWidth;
        double room = workspace.FilesSidebarWidth + WorkspaceViewModel.SplitterWidth;

        workspace.IsFilesSidebarVisible = false;
        host.Settle();

        Assert.Equal(shown + room, host.Shell.ContentPaneBorder.ActualWidth, 1);
        Assert.Equal(Visibility.Collapsed, host.Shell.FilesPaneBorder.Visibility);
        GridSplitter splitter = FilesSplitter(host);
        Assert.Equal(Visibility.Collapsed, splitter.Visibility);

        workspace.IsFilesSidebarVisible = true;
        host.Settle();

        Assert.Equal(shown, host.Shell.ContentPaneBorder.ActualWidth, 1);
        Assert.Equal(Visibility.Visible, host.Shell.FilesPaneBorder.Visibility);
        Assert.Equal(Visibility.Visible, splitter.Visibility);
    });

    [Fact]
    public void AHiddenFilesSidebarKeepsItsWidth() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        WorkspaceViewModel workspace = host.Workspace;
        workspace.FilesSidebarWidth = 300;
        host.Settle();

        workspace.IsFilesSidebarVisible = false;
        host.Settle();
        ColumnDefinition column = host.Shell.WorkspaceColumns.ColumnDefinitions[0];
        Assert.Equal(0, column.ActualWidth, 1);
        Assert.Equal(300, workspace.FilesSidebarWidth);

        workspace.IsFilesSidebarVisible = true;
        host.Settle();

        Assert.Equal(300, workspace.FilesSidebarWidth);
        Assert.Equal(300, column.ActualWidth, 1);
    });

    [Fact]
    public void WithBothPanesHiddenTheEditorHasTheRow() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));

        host.Workspace.IsFilesSidebarVisible = false;
        host.Workspace.IsRightPaneVisible = false;
        host.Settle();

        Assert.Equal(host.Shell.WorkspaceColumns.ActualWidth, host.Shell.ContentPaneBorder.ActualWidth, 1);
    });

    [Fact]
    public void TheStatusBarIsTwentyFour() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        StatusBar bar = host.Shell.ShellStatusBar;

        Assert.Equal(24, bar.ActualHeight, 1);
        Assert.Equal(TypeRamp.Caption.Size, bar.FontSize);

        TypeRamp.Apply(host.Shell.Resources.MergedDictionaries.First(layer => layer.Contains(TypeRamp.Body.SizeKey)), 2.25);
        host.Settle();

        Assert.True(bar.ActualHeight > 24, $"the bar is {bar.ActualHeight} with its text at 225%");
    });

    /// <summary>The design's hairline is inside the 24, over what's transparent at rest,
    /// and takes no pointer from a toggle's top edge.</summary>
    [Fact]
    public void TheBarHasTheDesignsHairline() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        var row = (Grid)host.Shell.ShellStatusBarBorder.Parent;

        Border hairline = row.Children.OfType<Border>().Single(border => border.Height == 1);

        Assert.Same(host.Shell.TryFindResource("Slate.SubtleBorderBrush"), hairline.Background);
        Assert.False(hairline.IsHitTestVisible);
        Assert.Equal(VerticalAlignment.Top, hairline.VerticalAlignment);
        Assert.Equal(24, row.ActualHeight, 1);
    });

    /// <summary>A trash's Cancel button only shows while one runs, and the row keeps its
    /// height with it there rather than jumping.</summary>
    [Fact]
    public void TheBarStaysTwentyFourWhileATrashRuns() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        StatusBar bar = host.Shell.ShellStatusBar;
        Button cancel = LogicalButtons(bar).Single(button => AutomationProperties.GetAutomationId(button) == "CancelTrash");
        var item = (StatusBarItem)LogicalTreeHelper.GetParent(LogicalTreeHelper.GetParent(cancel));

        item.Visibility = Visibility.Visible;
        host.Settle();

        Assert.True(cancel.IsVisible, "premise: the button is not showing");
        Assert.Equal(24, bar.ActualHeight, 1);
        Assert.True(cancel.ActualHeight >= 24, $"the button is {cancel.ActualHeight} high, under the target's 24");
    });

    [Fact]
    public void TheProgressItemIsNoFixedWidth() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        StatusBar bar = host.Shell.ShellStatusBar;
        var item = ShownShell.Descendants(bar).OfType<StatusBarItem>().Single(candidate =>
            ShownShell.Descendants(candidate).OfType<ProgressBar>().Any());

        Assert.True(double.IsNaN(item.Width), "the item has a fixed width");
        Assert.Equal(180, item.ActualWidth, 1);

        bar.Width = 300;
        host.Settle();

        Assert.True(item.ActualWidth < 180, $"the item stayed {item.ActualWidth} in a 300 bar");
    });

    [Fact]
    public void EachToggleIsTheDesignsInItsCorner() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        StatusBar bar = host.Shell.ShellStatusBar;
        ToggleButton files = Toggle(host, FilesToggleId);
        ToggleButton right = Toggle(host, RightToggleId);

        Assert.Equal(((char)0xE90C).ToString(), files.Content);
        Assert.Equal(((char)0xE90D).ToString(), right.Content);
        Assert.Equal("Files sidebar", AutomationProperties.GetName(files));
        Assert.Equal("Right pane", AutomationProperties.GetName(right));
        foreach (ToggleButton toggle in new[] { files, right })
        {
            Assert.Equal(24, toggle.ActualWidth, 1);
            Assert.Equal(24, toggle.ActualHeight, 1);
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(toggle);
            Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());
            Assert.NotNull(peer.GetPattern(PatternInterface.Toggle));
            Assert.Equal(AutomationProperties.GetName(toggle), peer.GetName());
            Assert.False(peer.IsKeyboardFocusable(), "the keyboard's path is the command, not a stop on the bar");
        }

        Assert.Equal(0, files.TranslatePoint(default, bar).X, 1);
        Assert.Equal(bar.ActualWidth - 24, right.TranslatePoint(default, bar).X, 1);
        Assert.Contains("Ctrl+Alt+I", TipText(right), StringComparison.Ordinal);
    });

    /// <summary>No vault means no panes to toggle, and an unresolved binding has to
    /// keep them out of layout and automation.</summary>
    [Fact]
    public void TheTogglesGoWithTheWorkspace() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        ToggleButton files = Toggle(host, FilesToggleId);
        ToggleButton right = Toggle(host, RightToggleId);
        Assert.True(files.IsVisible && right.IsVisible, "premise: the toggles are not showing");

        host.SetVaultOpen(false);

        Assert.False(files.IsVisible);
        Assert.False(right.IsVisible);
    });

    /// <summary>The scrim over the bar takes no pointer, so the toggles refuse what they
    /// would do behind a sheet, as the menu does.</summary>
    [Fact]
    public void TheTogglesDisableUnderAModalSurface() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        ToggleButton files = Toggle(host, FilesToggleId);
        ToggleButton right = Toggle(host, RightToggleId);
        Assert.True(files.IsEnabled && right.IsEnabled, "premise: the toggles are disabled with nothing open");

        host.Workspace.OpenAddPropertySheet(synchronousForTests: true);
        host.Settle();
        host.Settle();
        Assert.NotNull(host.Shell.OpenModalSurface);

        Assert.False(files.IsEnabled);
        Assert.False(right.IsEnabled);

        host.Workspace.CloseAddPropertySheetCommand.Execute(null);
        host.Settle();
        host.Settle();

        Assert.True(files.IsEnabled);
        Assert.True(right.IsEnabled);
    });

    [Fact]
    public void ToggleFilesSidebarIsAPaletteCommandAndAMenuItem() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        const string Id = "slate.view.toggleFilesSidebar";
        ChordTableEntry row = ChordTable.RegisteredRows.Single(entry => entry.Id == Id);
        Assert.Equal("Toggle Files Sidebar", row.Label);
        Assert.Contains(Id, SlateCommandRegistrar.ResolvableIds);

        MenuItem item = MenuItems(host.Shell.MainMenu).Single(candidate =>
            AutomationProperties.GetAutomationId(candidate) == "WorkspaceToggleFilesSidebarMenuItem");
        Assert.Same(host.Workspace.ToggleFilesSidebarCommand, item.Command);
        host.Announced.Clear();

        item.Command.Execute(null);

        Assert.False(host.Workspace.IsFilesSidebarVisible);
        Assert.Equal(
            "Files sidebar hidden.",
            SlateUniffiMethods.A11yRender(Assert.Single(host.Announced)).Text);
    });

    /// <summary>A reveal isn't finished when the property flips: the route consumes the
    /// mount, and while it's armed a leaf switch loads nothing.</summary>
    [Theory]
    [InlineData(Click)]
    [InlineData(UiaToggle)]
    public void ARevealThroughTheToggleLoadsTheCurrentNote(string path) => RunSta(() =>
    {
        using var host = new ShownShell(("hub.md", "Links to [[other]].\n"), ("other.md", "Links to [[hub]].\n"));
        WorkspaceViewModel workspace = host.Workspace;
        WorkspaceLeafOption connections = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "connections");
        WorkspaceLeafOption outline = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "outline");
        ConnectionsLeafViewModel leaf = workspace.Connections;
        workspace.OpenPath("hub.md");
        workspace.ActiveLeaf = connections;
        Settle(host, leaf);
        ToggleButton toggle = Toggle(host, RightToggleId);

        Press(toggle, path);
        Assert.False(workspace.IsRightPaneVisible);
        workspace.OpenPath("other.md");
        Settle(host, leaf);
        int loads = leaf.LoadsIssuedForTests;
        host.Announced.Clear();

        Press(toggle, path);

        Assert.True(workspace.IsRightPaneVisible);
        Assert.False(workspace.ConnectionsMountPendingForTests);
        Assert.Equal(loads + 1, leaf.LoadsIssuedForTests);
        Settle(host, leaf);
        Assert.Equal("other.md", leaf.Root);
        Assert.Single(host.Announced, line => line is A11yEvent.RightPaneShown);

        // A mount left armed would suppress this switch's load.
        workspace.ActiveLeaf = outline;
        workspace.OpenPath("hub.md");
        Settle(host, leaf);
        workspace.ActiveLeaf = connections;
        Settle(host, leaf);
        Assert.Equal("hub.md", leaf.Root);
    });

    [Theory]
    [InlineData(Click)]
    [InlineData(UiaToggle)]
    [InlineData("the command")]
    [InlineData("a route")]
    public void TheToggleAndThePaneAgreeOnEveryPath(string path) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        WorkspaceViewModel workspace = host.Workspace;
        workspace.OpenPath("a.md");
        host.Settle();
        ToggleButton files = Toggle(host, FilesToggleId);
        ToggleButton right = Toggle(host, RightToggleId);
        Assert.True(files.IsChecked);
        Assert.True(right.IsChecked);

        switch (path)
        {
            case Click:
            case UiaToggle:
                Press(files, path);
                Press(right, path);
                break;
            case "the command":
                workspace.ToggleFilesSidebarCommand.Execute(null);
                workspace.ToggleRightPaneCommand.Execute(null);
                break;
            default:
                workspace.IsFilesSidebarVisible = false;
                workspace.IsRightPaneVisible = false;
                break;
        }

        Assert.False(workspace.IsFilesSidebarVisible);
        Assert.False(workspace.IsRightPaneVisible);
        Assert.False(files.IsChecked);
        Assert.False(right.IsChecked);

        switch (path)
        {
            case Click:
            case UiaToggle:
                Press(files, path);
                Press(right, path);
                break;
            case "the command":
                workspace.ToggleFilesSidebarCommand.Execute(null);
                workspace.ToggleRightPaneCommand.Execute(null);
                break;
            default:
                workspace.FocusDirectionalPane("horizontal", -1);
                workspace.FocusDirectionalPane("horizontal", 1);
                break;
        }

        Assert.True(workspace.IsFilesSidebarVisible);
        Assert.True(workspace.IsRightPaneVisible);
        Assert.True(files.IsChecked);
        Assert.True(right.IsChecked);
    });

    [Theory]
    [InlineData(Click)]
    [InlineData(UiaToggle)]
    public void HidingFilesMovesFocusToTheEditor(string path) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        Assert.True(host.Land(ShellRegionKind.Files), "premise: the Files pane took no keys");
        Assert.True(host.Shell.FilesPaneBorder.IsKeyboardFocusWithin, "premise: the keys are not in the Files pane");
        List<IInputElement> changes = host.RecordFocusChanges();

        Press(Toggle(host, FilesToggleId), path);
        PumpedDispatcher.Drain();

        Assert.False(host.Workspace.IsFilesSidebarVisible);
        Assert.False(host.Shell.FilesPaneBorder.IsVisible);
        Assert.IsNotType<MainWindow>(Keyboard.FocusedElement);
        Assert.Equal(ShellRegionKind.Editor, ((IShellRegionHost)host.Shell).FocusedRegion());
        Assert.Single(changes);
    });

    [Fact]
    public void HidingFilesUnderItsSplitterLandsTheKeysInTheEditorOnce() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        GridSplitter splitter = ShownShell.Descendants(host.Shell.WorkspaceColumns).OfType<GridSplitter>().Single(item => Grid.GetColumn(item) == 1);
        Assert.True(splitter.Focus(), "premise: the splitter took no keys");
        List<IInputElement> changes = host.RecordFocusChanges();

        host.Workspace.ToggleFilesSidebarCommand.Execute(null);
        PumpedDispatcher.Drain();

        Assert.False(splitter.IsVisible);
        Assert.IsNotType<MainWindow>(Keyboard.FocusedElement);
        Assert.Equal(ShellRegionKind.Editor, ((IShellRegionHost)host.Shell).FocusedRegion());
        Assert.Single(changes);
    });

    [Fact]
    public void HidingFilesWithNothingOpenLandsOnTheEmptyPane() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        Assert.True(host.Land(ShellRegionKind.Files), "premise: the Files pane took no keys");

        host.Workspace.ToggleFilesSidebarCommand.Execute(null);
        PumpedDispatcher.Drain();

        Assert.IsNotType<MainWindow>(Keyboard.FocusedElement);
        Assert.Equal(ShellRegionKind.EmptyEditor, ((IShellRegionHost)host.Shell).FocusedRegion());
    });

    /// <summary>A zero-width pane that's still there stays tabbable: its stops can still
    /// take the keys, and these can't.</summary>
    [Fact]
    public void AHiddenFilesPaneIsNotATabStop() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        Assert.True(host.Shell.SidebarFilterTextBox.Focus(), "premise: the filter takes no keys when the pane shows");
        Assert.True(host.Land(ShellRegionKind.Editor), "premise: the editor took no keys");

        host.Workspace.IsFilesSidebarVisible = false;
        host.Settle();

        Assert.False(host.Shell.FilesPaneBorder.IsVisible);
        Assert.False(host.Shell.SidebarFilterTextBox.Focus());
        Assert.False(host.Shell.FilesTree.Focus());
    });

    [Fact]
    public void CtrlAltLeftAtTheEdgeShowsFiles() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        host.RelayFocusBoundaries();
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Workspace.IsFilesSidebarVisible = false;
        host.Settle();
        host.Announced.Clear();

        host.Workspace.FocusDirectionalPane("horizontal", -1);
        PumpedDispatcher.Drain();

        Assert.True(host.Workspace.IsFilesSidebarVisible);
        Assert.True(host.Shell.FilesPaneBorder.IsKeyboardFocusWithin, $"the keys are on {Keyboard.FocusedElement}");
        Assert.Equal(new A11yEvent[] { new A11yEvent.FilesSidebarShown(), new A11yEvent.FilesRegionFocused() }, host.Announced);
    });

    [Theory]
    [InlineData("the chord")]
    [InlineData("the menu")]
    public void FocusSidebarFilterShowsFiles(string route) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Workspace.IsFilesSidebarVisible = false;
        host.Settle();

        if (route == "the chord")
        {
            CommandPaletteTests.ThreadModifiers.Hold(
                ModifierKeys.Control | ModifierKeys.Alt,
                () => host.Shell.RaiseEvent(new KeyEventArgs(
                    Keyboard.PrimaryDevice, PresentationSource.FromVisual(host.Shell)!, Environment.TickCount, Key.F)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                }));
        }
        else
        {
            (typeof(MainWindow).GetMethod("FocusFilter_Click", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("FocusFilter_Click is gone"))
                .Invoke(host.Shell, [host.Shell, new RoutedEventArgs()]);
        }

        PumpedDispatcher.Drain();

        Assert.True(host.Workspace.IsFilesSidebarVisible);
        Assert.True(host.Shell.SidebarFilterTextBox.IsKeyboardFocused, $"the keys are on {Keyboard.FocusedElement}");
    });

    [Fact]
    public void RevealingAConnectionsRowShowsFiles() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        var revealed = new List<(string Path, bool Shown)>();
        host.Workspace.GraphRevealInSidebar = path => revealed.Add((path, host.Workspace.IsFilesSidebarVisible));
        host.Workspace.IsFilesSidebarVisible = false;

        Assert.True(host.Workspace.RevealConnectionsRowFromSurface("a.md"));

        Assert.Equal([("a.md", true)], revealed);
    });

    [Fact]
    public void RevealingAGraphRowShowsFiles() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Links to [[b]].\n"), ("b.md", "Links to [[a]].\n"));
        host.Workspace.OpenGraph();
        PumpedDispatcher.PumpUntilDrained(host.Workspace.GraphDocument!.WhenAllWorkDrained());
        var revealed = new List<(string Path, bool Shown)>();
        host.Workspace.GraphRevealInSidebar = path => revealed.Add((path, host.Workspace.IsFilesSidebarVisible));
        host.Workspace.IsFilesSidebarVisible = false;

        Assert.True(host.Workspace.RevealGraphRowFromSurface("a.md"));

        Assert.Equal([("a.md", true)], revealed);
    });

    /// <summary>A new note hands off to the tree's rename field, which a hidden pane
    /// can't take.</summary>
    [Fact]
    public void NewNoteFromTheTreeShowsFiles() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        WithSidebar(host, sidebar =>
        {
            host.Workspace.IsFilesSidebarVisible = false;
            host.Settle();

            sidebar.CreateNoteCommand.Execute(null);
            PumpedDispatcher.PumpUntilDrained(sidebar.TreeRefreshCompletion);
            PumpedDispatcher.Drain();

            Assert.True(host.Workspace.IsFilesSidebarVisible);
            Assert.True(host.Shell.SidebarMutationNameTextBox.IsKeyboardFocused, $"the keys are on {Keyboard.FocusedElement}");
        });
    });

    /// <summary>What these do is only to be seen in the pane, and some of it isn't spoken,
    /// so with the pane hidden, from the palette or a menu, they'd do nothing anyone could
    /// tell.</summary>
    public static TheoryData<string> CommandsWhoseResultIsInThePane() => new()
    {
        "ClearFilterCommand",
        "ToggleTagsCommand",
        "ToggleDualPaneCommand",
        "PinCommand",
        "UnpinCommand",
        "UnpinAllCommand",
        "AddShortcutCommand",
        "RemoveShortcutCommand",
        "UseVaultDefaultSortCommand",
        "ClearRecentsCommand",
        "CollapseAllCommand",
        "ExpandLoadedCommand",
    };

    [Theory]
    [MemberData(nameof(CommandsWhoseResultIsInThePane))]
    public void ACommandWhoseResultIsInThePaneShowsIt(string name) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        WithSidebar(host, sidebar =>
        {
            host.Workspace.IsFilesSidebarVisible = false;
            host.Settle();

            CommandOf(sidebar, name).Execute(null);
            PumpedDispatcher.PumpUntilDrained(sidebar.TreeRefreshCompletion);
            PumpedDispatcher.Drain();

            Assert.True(host.Workspace.IsFilesSidebarVisible, $"{name} left the pane hidden");
        });
    });

    /// <summary>Where the rest show their results, so a hidden pane costs them nothing, and
    /// so a new sidebar command has to say which it is.</summary>
    private static readonly Dictionary<string, string> CommandsWhoseResultIsElsewhere = new()
    {
        ["RefreshCommand"] = "a rescan, reported on the status bar",
        ["RetrySettingsCommand"] = "its button is beside the pane's notice",
        ["AddTagCommand"] = "spoken, and its field is in the pane",
        ["RemoveTagCommand"] = "spoken, and its field is in the pane",
        ["CreateFolderCommand"] = "hands off to the name field, which shows the pane",
        ["CreateNoteCommand"] = "hands off to the name field, which shows the pane",
        ["CreateCanvasCommand"] = "hands off to the name field, which shows the pane",
        ["RenameCommand"] = "spoken, and the tab's title changes",
        ["DeleteCommand"] = "its prompt names the file",
        ["CreateFolderNoteCommand"] = "spoken",
        ["DeleteFolderNoteCommand"] = "spoken",
        ["CopyWikilinkCommand"] = "the clipboard",
        ["CopyPathCommand"] = "the clipboard",
        ["RevealCommand"] = "Explorer",
        ["DuplicateCommand"] = "spoken",
        ["MoveToCommand"] = "a sheet",
        ["OpenCurrentCommand"] = "the editor",
        ["OpenNewTabCommand"] = "the editor",
        ["OpenSplitCommand"] = "the editor",
        ["HistoryBackCommand"] = "the editor",
        ["HistoryForwardCommand"] = "the editor",
        ["BatchMoveCommand"] = "spoken, and its field is in the pane",
        ["BatchTrashCommand"] = "the status bar's progress",
        ["ImportCommand"] = "a file picker",
        ["CancelImportCommand"] = "spoken",
        ["CancelTrashCommand"] = "the status bar's progress",
    };

    [Fact]
    public void EverySidebarCommandSaysWhetherItsResultIsInThePane()
    {
        string[] named = typeof(FilesSidebarViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(property => typeof(ICommand).IsAssignableFrom(property.PropertyType))
            .Select(property => property.Name)
            .ToArray();
        string[] inThePane = CommandsWhoseResultIsInThePane().Select(row => row.Data).ToArray();

        Assert.Empty(named.Except(inThePane).Except(CommandsWhoseResultIsElsewhere.Keys));
        Assert.Empty(inThePane.Concat(CommandsWhoseResultIsElsewhere.Keys).Except(named));
    }

    /// <summary>The ring refuses it, and a fallback from the editor doesn't open it: a
    /// fallback isn't a route the reader asked for.</summary>
    [Fact]
    public void NoLandingOpensAHiddenFilesPane() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Workspace.IsFilesSidebarVisible = false;
        host.Settle();

        Assert.Equal(
            ShellRegionLanding.Refused,
            ((IShellRegionHost)host.Shell).TryLand(ShellRegionKind.Files, static () => { }, static () => { }));
        Assert.False(host.Shell.LandOnFilesTree());
        Assert.False(host.Workspace.IsFilesSidebarVisible);
    });

    /// <summary>A real sidebar over the shown shell, as the lifecycle attaches one.</summary>
    private static void WithSidebar(ShownShell host, Action<FilesSidebarViewModel> body)
    {
        string appData = Path.Combine(Path.GetTempPath(), $"slate-ds7-{Guid.NewGuid():N}");
        PropertyInfo slot = typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.FileSidebar))
            ?? throw new InvalidOperationException("FileSidebar is gone");
        var sidebar = new FilesSidebarViewModel(host.Session, _ => { }, localAppDataRoot: appData);
        try
        {
            PumpedDispatcher.PumpUntilDrained(sidebar.TreeRefreshCompletion);
            slot.SetValue(host.Shell.DataContext, sidebar);
            host.Settle();
            body(sidebar);
        }
        finally
        {
            slot.SetValue(host.Shell.DataContext, null);
            try
            {
                Directory.Delete(appData, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static ICommand CommandOf(FilesSidebarViewModel sidebar, string name) =>
        (ICommand)(typeof(FilesSidebarViewModel).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{name} is gone")).GetValue(sidebar)!;

    private static void Settle(ShownShell host, ConnectionsLeafViewModel leaf)
    {
        PumpedDispatcher.PumpUntilDrained(leaf.WhenAllWorkDrained());
        host.Settle();
    }

    private static ToggleButton Toggle(ShownShell host, string id) =>
        ShownShell.Descendants(host.Shell.ShellStatusBar).OfType<ToggleButton>()
            .Single(button => AutomationProperties.GetAutomationId(button) == id);

    private static GridSplitter FilesSplitter(ShownShell host) =>
        ShownShell.Descendants(host.Shell.WorkspaceColumns).OfType<GridSplitter>().Single(item => Grid.GetColumn(item) == 1);

    /// <summary>A click runs the button's own OnClick (a toggle flips, then its command
    /// runs); UIA's Toggle flips it and runs no command.</summary>
    private static void Press(ToggleButton toggle, string path)
    {
        if (path == Click)
        {
            (typeof(ButtonBase).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("ButtonBase.OnClick is gone")).Invoke(toggle, null);
        }
        else
        {
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(toggle);
            ((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)!).Toggle();
        }
    }

    private static string TipText(FrameworkElement element) => element.ToolTip switch
    {
        TextBlock text => new System.Windows.Documents.TextRange(text.ContentStart, text.ContentEnd).Text,
        string text => text,
        var other => other?.ToString() ?? string.Empty,
    };

    private static IEnumerable<MenuItem> MenuItems(ItemsControl root) =>
        root.Items.OfType<MenuItem>().SelectMany(item => MenuItems(item).Prepend(item));

    private static IEnumerable<Button> LogicalButtons(DependencyObject root)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is Button button)
            {
                yield return button;
            }

            if (child is DependencyObject element)
            {
                foreach (Button nested in LogicalButtons(element))
                {
                    yield return nested;
                }
            }
        }
    }

    private static void RunSta(Action body)
    {
        Func<bool> priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        try
        {
            StaThread.RunPumped(body, TimeSpan.FromSeconds(90), "STA test body timed out.");
        }
        finally
        {
            CanvasSurfaceView.ShellOverlayIsOpen = priorOverlayProbe;
        }
    }
}
