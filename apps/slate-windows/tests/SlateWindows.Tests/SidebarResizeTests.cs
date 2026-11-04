// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4b (#1247; the owner's decision on AR-38): the keyboard resizes
/// the two sidebars. The splitters left the Tab order, so Widen/Narrow
/// Files Sidebar and Widen/Narrow Right Pane step each width by one step,
/// bounded below by the columns' own minimum and above by a fixed ceiling
/// and the room the editor needs, and core says the new width — or, at a
/// limit, that the sidebar is already there. The columns follow the widths,
/// and a splitter drag writes the width back.
/// </summary>
public sealed class SidebarResizeTests : IDisposable
{
    private readonly FixtureVault _fixture = FixtureVault.Create(1, "sidebar-resize");
    private readonly VaultSession _session;
    private readonly List<A11yEvent> _announced = [];

    public SidebarResizeTests()
    {
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public void WidenStepsTheFilesSidebarAndCoreSaysTheNewWidth() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.WorkspaceRowWidth = 1600;
        double before = workspace.FilesSidebarWidth;

        workspace.WidenFilesSidebarCommand.Execute(null);

        Assert.Equal(before + WorkspaceViewModel.SidebarWidthStep, workspace.FilesSidebarWidth);
        A11yEvent line = Assert.Single(_announced);
        Assert.Equal(
            $"Files sidebar resized, {(uint)workspace.FilesSidebarWidth} pixels.",
            SlateUniffiMethods.A11yRender(line).Text);
    });

    [Fact]
    public void NarrowStopsAtTheFloorAndSaysSo() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.WorkspaceRowWidth = 1600;
        workspace.RightPaneWidth = 280;

        workspace.NarrowRightPaneCommand.Execute(null);
        Assert.Equal(WorkspaceViewModel.MinimumRightPaneWidth, workspace.RightPaneWidth);
        Assert.Equal("Right pane resized, 250 pixels.", SlateUniffiMethods.A11yRender(_announced[^1]).Text);

        workspace.NarrowRightPaneCommand.Execute(null);
        Assert.Equal(WorkspaceViewModel.MinimumRightPaneWidth, workspace.RightPaneWidth);
        Assert.Equal("Right pane at its narrowest, 250 pixels.", SlateUniffiMethods.A11yRender(_announced[^1]).Text);
    });

    /// <summary>The editor keeps its minimum between the sidebars: in a
    /// 1,000-pixel row with a 280-pixel right pane, the Files sidebar stops
    /// at 1,000 − 280 − 300 − 10 = 410 pixels.</summary>
    [Fact]
    public void WidenStopsWhereTheEditorWouldBeSqueezed() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.WorkspaceRowWidth = 1000;
        workspace.RightPaneWidth = 280;
        workspace.FilesSidebarWidth = 390;

        workspace.WidenFilesSidebarCommand.Execute(null);
        Assert.Equal(410, workspace.FilesSidebarWidth);
        Assert.Equal("Files sidebar resized, 410 pixels.", SlateUniffiMethods.A11yRender(_announced[^1]).Text);

        workspace.WidenFilesSidebarCommand.Execute(null);
        Assert.Equal(410, workspace.FilesSidebarWidth);
        Assert.Equal("Files sidebar at its widest, 410 pixels.", SlateUniffiMethods.A11yRender(_announced[^1]).Text);
    });

    [Fact]
    public void AWideWindowStillHasACeiling() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.WorkspaceRowWidth = 4000;
        workspace.FilesSidebarWidth = WorkspaceViewModel.MaximumSidebarWidth - 10;

        workspace.WidenFilesSidebarCommand.Execute(null);
        Assert.Equal(WorkspaceViewModel.MaximumSidebarWidth, workspace.FilesSidebarWidth);
        workspace.WidenFilesSidebarCommand.Execute(null);
        Assert.Equal("Files sidebar at its widest, 640 pixels.", SlateUniffiMethods.A11yRender(_announced[^1]).Text);
    });

    /// <summary>The right pane's two steps are available exactly while the
    /// pane is shown, and every show or hide says so: both commands requery,
    /// and the registered catalog with them. A Workspace menu item caches its
    /// command's state and asks again only when told, so without the requery
    /// NVDA read "unavailable" on a shown pane (PR 4b codex round 2, F4).</summary>
    [Fact]
    public void TheRightPaneCommandsNeedTheRightPaneShown() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.IsRightPaneVisible = true;
        Assert.True(workspace.WidenRightPaneCommand.CanExecute(null));
        int widen = 0, narrow = 0, registered = 0;
        workspace.WidenRightPaneCommand.CanExecuteChanged += (_, _) => widen++;
        workspace.NarrowRightPaneCommand.CanExecuteChanged += (_, _) => narrow++;
        workspace.RegisteredCommandStatesChanged = () => registered++;

        workspace.IsRightPaneVisible = false;

        Assert.False(workspace.WidenRightPaneCommand.CanExecute(null));
        Assert.False(workspace.NarrowRightPaneCommand.CanExecute(null));
        Assert.True(workspace.WidenFilesSidebarCommand.CanExecute(null));
        Assert.Equal((1, 1, 1), (widen, narrow, registered));

        workspace.IsRightPaneVisible = false;
        Assert.Equal((1, 1, 1), (widen, narrow, registered));

        workspace.IsRightPaneVisible = true;

        Assert.True(workspace.WidenRightPaneCommand.CanExecute(null));
        Assert.True(workspace.NarrowRightPaneCommand.CanExecute(null));
        Assert.Equal((2, 2, 2), (widen, narrow, registered));
    });

    /// <summary>#1790: hiding the right pane collapsed its contents and kept its
    /// column and splitter, so the editor couldn't take the room.</summary>
    [Fact]
    public void HidingTheRightPaneGivesTheEditorItsRoom() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        WorkspaceViewModel workspace = host.Workspace;
        double shown = host.Shell.ContentPaneBorder.ActualWidth;
        double room = workspace.RightPaneWidth + WorkspaceViewModel.SplitterWidth;

        workspace.IsRightPaneVisible = false;
        host.Settle();

        Assert.Equal(shown + room, host.Shell.ContentPaneBorder.ActualWidth, 1);
        GridSplitter splitter = ShownShell.Descendants(host.Shell).OfType<GridSplitter>().Single(item => Grid.GetColumn(item) == 3);
        Assert.Equal(Visibility.Collapsed, splitter.Visibility);

        workspace.IsRightPaneVisible = true;
        host.Settle();

        Assert.Equal(shown, host.Shell.ContentPaneBorder.ActualWidth, 1);
        Assert.Equal(Visibility.Visible, splitter.Visibility);
    });

    [Fact]
    public void AHiddenRightPaneKeepsItsWidth() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        WorkspaceViewModel workspace = host.Workspace;
        workspace.RightPaneWidth = 280;
        host.Settle();

        workspace.IsRightPaneVisible = false;
        host.Settle();
        ColumnDefinition column = host.Shell.WorkspaceColumns.ColumnDefinitions[4];
        Assert.Equal(0, column.ActualWidth, 1);
        Assert.Equal(280, workspace.RightPaneWidth);

        workspace.IsRightPaneVisible = true;
        host.Settle();

        Assert.Equal(280, workspace.RightPaneWidth);
        Assert.Equal(280, column.ActualWidth, 1);
    });

    /// <summary>#1790: while the right pane is hidden, its room is the Files
    /// sidebar's to use, and it goes back when the pane does.</summary>
    [Fact]
    public void TheFilesSidebarCanUseAHiddenPanesRoomUntilThePaneComesBack() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.WorkspaceRowWidth = 1000;
        double widestBesideThePane = workspace.WidestSidebarWidth(ShellSidebar.Files);

        workspace.IsRightPaneVisible = false;

        Assert.Equal(WorkspaceViewModel.MaximumSidebarWidth, workspace.WidestSidebarWidth(ShellSidebar.Files));
        workspace.FilesSidebarWidth = WorkspaceViewModel.MaximumSidebarWidth;
        Assert.Equal(WorkspaceViewModel.MaximumSidebarWidth, workspace.FilesSidebarWidth);

        double rightPane = workspace.RightPaneWidth;
        workspace.IsRightPaneVisible = true;

        Assert.Equal(widestBesideThePane, workspace.FilesSidebarWidth);
        Assert.Equal(rightPane, workspace.RightPaneWidth);
    });

    /// <summary>#1790: the window shrinking under a wide Files sidebar isn't the
    /// hidden pane's concern, and its width is still there when it comes back.</summary>
    [Fact]
    public void AWindowResizeLeavesAHiddenPanesWidthAlone() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.WorkspaceRowWidth = 1000;
        workspace.IsRightPaneVisible = false;
        workspace.FilesSidebarWidth = WorkspaceViewModel.MaximumSidebarWidth;
        double rightPane = workspace.RightPaneWidth;

        workspace.WorkspaceRowWidth = 900;

        Assert.Equal(rightPane, workspace.RightPaneWidth);

        workspace.IsRightPaneVisible = true;

        Assert.Equal(rightPane, workspace.RightPaneWidth);
        Assert.Equal(
            900 - rightPane - WorkspaceViewModel.MinimumEditorWidth - (2 * WorkspaceViewModel.SplitterWidth),
            workspace.FilesSidebarWidth);
    });

    /// <summary>The window's two sidebar columns follow the widths, and a
    /// splitter drag — which writes the column's width — writes the width
    /// back, so the next step starts from where the pointer left it.</summary>
    [Fact]
    public void TheColumnsFollowTheWidthsBothWays() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        var shell = new MainWindow();
        try
        {
            var lifecycle = Assert.IsType<VaultLifecycleViewModel>(shell.DataContext);
            (typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.Workspace))
                ?? throw new InvalidOperationException("Workspace is gone"))
                .SetValue(lifecycle, workspace);
            var columns = Assert.IsType<Grid>(shell.FindName("WorkspaceColumns"));
            PumpedDispatcher.Drain();
            workspace.WorkspaceRowWidth = 1600;

            workspace.WidenFilesSidebarCommand.Execute(null);
            workspace.WidenRightPaneCommand.Execute(null);
            PumpedDispatcher.Drain();
            System.Windows.Data.BindingExpression? bound = System.Windows.Data.BindingOperations.GetBindingExpression(
                columns.ColumnDefinitions[0], ColumnDefinition.WidthProperty);
            Assert.True(bound is { Status: System.Windows.Data.BindingStatus.Active }, $"the column binding is {bound?.Status}");

            Assert.Equal(new GridLength(workspace.FilesSidebarWidth), columns.ColumnDefinitions[0].Width);
            Assert.Equal(new GridLength(workspace.RightPaneWidth), columns.ColumnDefinitions[4].Width);

            columns.ColumnDefinitions[0].Width = new GridLength(333);
            Assert.Equal(333, workspace.FilesSidebarWidth);
        }
        finally
        {
            (typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.Workspace)))!
                .SetValue(shell.DataContext, null);
            shell.Close();
        }
    });

    /// <summary>
    /// Codex PR 4b r1 F7: a splitter drag writes the column's width with no
    /// bound but the column's minimum — past the 640-pixel ceiling, past the
    /// room the editor needs — and the resize commands then spoke "at its
    /// widest" of a width over the ceiling and "narrowed" by hundreds of
    /// pixels. The width is held inside its range whoever writes it, and the
    /// column follows the held width.
    /// </summary>
    [Fact]
    public void ADraggedWidthIsHeldInsideTheRoom() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        var shell = new MainWindow();
        try
        {
            var lifecycle = Assert.IsType<VaultLifecycleViewModel>(shell.DataContext);
            (typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.Workspace))
                ?? throw new InvalidOperationException("Workspace is gone"))
                .SetValue(lifecycle, workspace);
            var columns = Assert.IsType<Grid>(shell.FindName("WorkspaceColumns"));
            PumpedDispatcher.Drain();
            workspace.WorkspaceRowWidth = 1600;

            // What GridSplitter.SetDefinitionLength writes on a drag.
            columns.ColumnDefinitions[0].Width = new GridLength(900);
            PumpedDispatcher.Drain();
            Assert.Equal(WorkspaceViewModel.MaximumSidebarWidth, workspace.FilesSidebarWidth);
            Assert.Equal(new GridLength(WorkspaceViewModel.MaximumSidebarWidth), columns.ColumnDefinitions[0].Width);

            workspace.WidenFilesSidebarCommand.Execute(null);
            Assert.Equal("Files sidebar at its widest, 640 pixels.", SlateUniffiMethods.A11yRender(_announced[^1]).Text);
            workspace.NarrowFilesSidebarCommand.Execute(null);
            Assert.Equal(600, workspace.FilesSidebarWidth);
            Assert.Equal("Files sidebar resized, 600 pixels.", SlateUniffiMethods.A11yRender(_announced[^1]).Text);

            // The room row: 1,000 − 280 − 300 − 10 leaves the Files sidebar 410.
            workspace.WorkspaceRowWidth = 1000;
            workspace.RightPaneWidth = 280;
            columns.ColumnDefinitions[0].Width = new GridLength(500);
            PumpedDispatcher.Drain();
            Assert.Equal(410, workspace.FilesSidebarWidth);
            Assert.Equal(new GridLength(410), columns.ColumnDefinitions[0].Width);
        }
        finally
        {
            (typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.Workspace)))!
                .SetValue(shell.DataContext, null);
            shell.Close();
        }
    });

    /// <summary>Codex PR 4b r1 F7: the row narrowing under a wide sidebar (a
    /// window shrunk, snapped) brings the width back into the room, so the
    /// next step is one step and says the truth.</summary>
    [Fact]
    public void AShrunkRowBringsTheWidthBackInRange() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.WorkspaceRowWidth = 1600;
        workspace.RightPaneWidth = 280;
        workspace.FilesSidebarWidth = 640;

        workspace.WorkspaceRowWidth = 1000;

        Assert.Equal(410, workspace.FilesSidebarWidth);
        workspace.WidenFilesSidebarCommand.Execute(null);
        Assert.Equal("Files sidebar at its widest, 410 pixels.", SlateUniffiMethods.A11yRender(_announced[^1]).Text);
        workspace.NarrowFilesSidebarCommand.Execute(null);
        Assert.Equal(370, workspace.FilesSidebarWidth);
        Assert.Equal("Files sidebar resized, 370 pixels.", SlateUniffiMethods.A11yRender(_announced[^1]).Text);
    });

    /// <summary>#1795: a window that shrank and grew back gives both widths back, and
    /// the reader's own step sets what comes back.</summary>
    [Fact]
    public void AGrownRowGivesTheWidthsBack() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.WorkspaceRowWidth = 1600;
        workspace.FilesSidebarWidth = 300;
        workspace.RightPaneWidth = 360;

        workspace.WorkspaceRowWidth = 800;
        Assert.Equal(WorkspaceViewModel.MinimumSidebarWidth, workspace.FilesSidebarWidth);
        Assert.Equal(290, workspace.RightPaneWidth);

        workspace.WorkspaceRowWidth = 1600;
        Assert.Equal(300, workspace.FilesSidebarWidth);
        Assert.Equal(360, workspace.RightPaneWidth);

        workspace.WorkspaceRowWidth = 800;
        workspace.NarrowRightPaneCommand.Execute(null);
        Assert.Equal(WorkspaceViewModel.MinimumRightPaneWidth, workspace.RightPaneWidth);

        workspace.WorkspaceRowWidth = 1600;
        Assert.Equal(WorkspaceViewModel.MinimumRightPaneWidth, workspace.RightPaneWidth);
    });

    private WorkspaceViewModel NewWorkspace() =>
        new(
            _session,
            _fixture.Root,
            () => [],
            _announced.Add,
            startInteractionBackgroundWork: false,
            preferencesStore: new AppPreferencesStore(Path.Combine(_fixture.Root, "preferences.json")));

    private static void RunSta(Action body)
    {
        Func<bool> priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        try
        {
            StaThread.RunPumped(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
        }
        finally
        {
            CanvasSurfaceView.ShellOverlayIsOpen = priorOverlayProbe;
        }
    }
}
