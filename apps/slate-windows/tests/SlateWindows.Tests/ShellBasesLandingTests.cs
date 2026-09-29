// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Bases;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4b (#1247, R-5; the completeness sweep's G20): the editor
/// region's landing on a Base tab — F6's, a restore's last resort — lands
/// INSIDE the surface: its current or first cell, silently. It landed on the
/// tab's header, which F6 then read as a refusal of the editor region and
/// moved on. The shipped window, shown, over a real workspace with a Base
/// open.
/// </summary>
public sealed class ShellBasesLandingTests : IDisposable
{
    private readonly FixtureVault _fixture = FixtureVault.Create(3, "shell-bases-landing");
    private readonly VaultSession _session;

    public ShellBasesLandingTests()
    {
        File.WriteAllText(
            Path.Combine(_fixture.Root, "Notes.base"),
            "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n    order:\n      - file.name\n");
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
    public void TheEditorRegionLandsInsideABaseTab() => RunSta(() =>
    {
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session, _fixture.Root, () => [], announced.Add, startInteractionBackgroundWork: false);
        var shell = new MainWindow
        {
            ShowActivated = false,
            ShowInTaskbar = false,
            Width = 1200,
            Height = 800,
        };
        var lifecycle = Assert.IsType<VaultLifecycleViewModel>(shell.DataContext);
        System.Reflection.PropertyInfo slot = typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.Workspace))
            ?? throw new InvalidOperationException("Workspace is gone");
        System.Reflection.PropertyInfo open = typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.IsVaultOpen))
            ?? throw new InvalidOperationException("IsVaultOpen is gone");
        try
        {
            slot.SetValue(lifecycle, workspace);
            open.SetValue(lifecycle, true);
            workspace.OpenPath("Notes.base");
            shell.Show();
            shell.UpdateLayout();
            PumpedDispatcher.Drain();
            BaseSurfaceView surface = Descendants(shell).OfType<BaseSurfaceView>().Single(view => view.IsVisible);
            Assert.True(surface.GridForTests.IsVisible, "premise: the Base shows its table");

            Assert.True(ShownShell.LandRegion(shell, ShellRegionKind.Editor), "the editor region refused a Base tab");

            Assert.IsType<DataGridCell>(Keyboard.FocusedElement);
            Assert.True(surface.IsKeyboardFocusWithin, "the keys are not in the Base's surface");
            Assert.Equal(ShellRegionKind.Editor, ((IShellRegionHost)shell).FocusedRegion());
        }
        finally
        {
            open.SetValue(lifecycle, false);
            slot.SetValue(lifecycle, null);
            shell.Close();
        }
    });

    /// <summary>
    /// Codex PR 4b r1 F3 (R-5 (h), G20): the query builder opened from the
    /// menu or the palette over a Queries row takes that ROW as its restore
    /// token; saving refreshes the leaf, which rebuilds every row, so the
    /// token is dead when the builder closes. The restore fell back to the
    /// active tab's editor — out of the right pane. It lands the keys back
    /// on the rows they were in: the same saved query's fresh row.
    /// </summary>
    [Fact]
    public void ABuilderClosedOverARepublishedQueriesRowLandsBackOnItsRow() => RunSta(() =>
    {
        ulong scratch = _session.OpenBase("Notes.base");
        try
        {
            _ = _session.SaveQuery(
                "All notes", description: null, _session.BaseViewQueryJson(scratch, 0), SavedQuerySourceSyntax.Builder);
        }
        finally
        {
            _session.CloseBase(scratch);
        }

        using var workspace = new WorkspaceViewModel(
            _session, _fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: false);
        var shell = new MainWindow
        {
            ShowActivated = false,
            ShowInTaskbar = false,
            Width = 1200,
            Height = 800,
        };
        var lifecycle = Assert.IsType<VaultLifecycleViewModel>(shell.DataContext);
        System.Reflection.PropertyInfo slot = typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.Workspace))
            ?? throw new InvalidOperationException("Workspace is gone");
        System.Reflection.PropertyInfo open = typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.IsVaultOpen))
            ?? throw new InvalidOperationException("IsVaultOpen is gone");
        try
        {
            slot.SetValue(lifecycle, workspace);
            open.SetValue(lifecycle, true);
            workspace.OpenPath("Notes.base");
            workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "queries");
            shell.Show();
            shell.UpdateLayout();
            PumpedDispatcher.Drain();
            ListBox list = shell.QueriesSavedList;
            SavedQuerySummary query = Assert.Single(workspace.SavedQueries);
            list.UpdateLayout();
            var token = Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromItem(query));
            Assert.True(token.Focus(), "premise: the saved query's row refused the keys");

            workspace.BasesNewQueryCommand.Execute(null);
            PumpedDispatcher.Drain();
            Assert.NotNull(workspace.BaseQueryBuilderSheet);
            Assert.True(workspace.BaseQueryBuilderSheet!.SaveAsSavedQuery("Second", null), "premise: the builder did not save");
            workspace.RefreshBaseQueries();
            PumpedDispatcher.Drain();
            Assert.Equal(2, workspace.SavedQueries.Count);
            Assert.Null(PresentationSource.FromVisual(token));

            workspace.CloseQueryBuilder();
            PumpedDispatcher.Drain();

            var row = Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
            Assert.Same(list, ItemsControl.ItemsControlFromItemContainer(row));
            Assert.Equal(query.Id, Assert.IsType<SavedQuerySummary>(row.DataContext).Id);
            Assert.False(shell.ContentPaneBorder.IsKeyboardFocusWithin, "the keys left the right pane for the editor");
        }
        finally
        {
            open.SetValue(lifecycle, false);
            slot.SetValue(lifecycle, null);
            shell.Close();
        }
    });

    /// <summary>PR 4b codex round 2, F2: a dashboard with no sections had no
    /// stop in its surface, so the editor region's landing fell through to
    /// the tab header, which F6 read as a refusal and moved on. It lands on
    /// the dashboard's empty notice.</summary>
    [Fact]
    public void TheEditorRegionLandsOnAnEmptyDashboardsNotice() => RunSta(() =>
    {
        string id = _session.SaveDashboard("Board", []);
        using var workspace = new WorkspaceViewModel(
            _session, _fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: false);
        var shell = new MainWindow
        {
            ShowActivated = false,
            ShowInTaskbar = false,
            Width = 1200,
            Height = 800,
        };
        var lifecycle = Assert.IsType<VaultLifecycleViewModel>(shell.DataContext);
        System.Reflection.PropertyInfo slot = typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.Workspace))
            ?? throw new InvalidOperationException("Workspace is gone");
        System.Reflection.PropertyInfo open = typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.IsVaultOpen))
            ?? throw new InvalidOperationException("IsVaultOpen is gone");
        try
        {
            slot.SetValue(lifecycle, workspace);
            open.SetValue(lifecycle, true);
            workspace.OpenDashboard(id, "Board");
            shell.Show();
            shell.UpdateLayout();
            PumpedDispatcher.Drain();
            DashboardSurfaceView surface = Descendants(shell).OfType<DashboardSurfaceView>().Single(view => view.IsVisible);
            Assert.True(surface.EmptyStateForTests.IsVisible, "premise: the empty dashboard shows no notice");

            Assert.True(ShownShell.LandRegion(shell, ShellRegionKind.Editor), "the editor region refused an empty dashboard tab");

            Assert.Same(surface.EmptyStateForTests, Keyboard.FocusedElement);
            Assert.Equal(ShellRegionKind.Editor, ((IShellRegionHost)shell).FocusedRegion());
        }
        finally
        {
            open.SetValue(lifecycle, false);
            slot.SetValue(lifecycle, null);
            shell.Close();
        }
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (DependencyObject nested in Descendants(child))
            {
                yield return nested;
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
