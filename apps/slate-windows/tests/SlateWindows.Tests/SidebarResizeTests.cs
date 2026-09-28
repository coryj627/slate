// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
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
        workspace.RightPaneWidth = 230;

        workspace.NarrowRightPaneCommand.Execute(null);
        Assert.Equal(WorkspaceViewModel.MinimumSidebarWidth, workspace.RightPaneWidth);
        Assert.Equal("Right pane resized, 200 pixels.", SlateUniffiMethods.A11yRender(_announced[^1]).Text);

        workspace.NarrowRightPaneCommand.Execute(null);
        Assert.Equal(WorkspaceViewModel.MinimumSidebarWidth, workspace.RightPaneWidth);
        Assert.Equal("Right pane at its narrowest, 200 pixels.", SlateUniffiMethods.A11yRender(_announced[^1]).Text);
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

    [Fact]
    public void TheRightPaneCommandsNeedTheRightPaneShown() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.IsRightPaneVisible = true;
        Assert.True(workspace.WidenRightPaneCommand.CanExecute(null));

        workspace.IsRightPaneVisible = false;

        Assert.False(workspace.WidenRightPaneCommand.CanExecute(null));
        Assert.False(workspace.NarrowRightPaneCommand.CanExecute(null));
        Assert.True(workspace.WidenFilesSidebarCommand.CanExecute(null));
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
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PumpedDispatcher.Run(body);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA test body timed out.");
        CanvasSurfaceView.ShellOverlayIsOpen = priorOverlayProbe;
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
