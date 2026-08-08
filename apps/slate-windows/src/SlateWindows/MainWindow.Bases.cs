// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W4-6 (#738): the Queries leaf, the Base dock leaf, and the
/// dashboard editor overlay — button routes into the workspace's one
/// implementation per action (contract C12/C15). Deletes confirm
/// through an injectable seam so facts run headless.
/// </summary>
public partial class MainWindow
{
    /// <summary>Injectable confirmation (the W4-4 dialog-seam
    /// pattern): production shows a message box; facts inject.</summary>
    internal Func<string, bool> BasesDeleteConfirmation { get; set; } =
        message => MessageBox.Show(
            message,
            "Slate",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private string? _pendingRenameSavedQueryId;

    private WorkspaceViewModel? BasesWorkspace =>
        (DataContext as VaultLifecycleViewModel)?.Workspace;

    private SavedQuerySummary? SelectedSavedQuery =>
        QueriesSavedList.SelectedItem as SavedQuerySummary;

    private BaseFileSummary? SelectedBaseFile =>
        QueriesBaseFilesList.SelectedItem as BaseFileSummary;

    private DashboardSummary? SelectedDashboard =>
        QueriesDashboardsList.SelectedItem as DashboardSummary;

    private void QueriesRefresh_Click(object sender, RoutedEventArgs e) =>
        BasesWorkspace?.RefreshBaseQueries();

    private void QueriesNewDashboard_Click(object sender, RoutedEventArgs e) =>
        BasesWorkspace?.OpenDashboardEditor(dashboardId: null);

    private void QueriesRun_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSavedQuery is { } summary)
        {
            BasesWorkspace?.RunSavedQuery(summary.Id);
        }
    }

    private void QueriesSavedList_DoubleClick(object sender, MouseButtonEventArgs e) =>
        QueriesRun_Click(sender, e);

    private void QueriesPin_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSavedQuery is { } summary)
        {
            BasesWorkspace?.ToggleSavedQueryPin(summary.Id);
        }
    }

    private void QueriesRename_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSavedQuery is not { } summary)
        {
            return;
        }
        _pendingRenameSavedQueryId = summary.Id;
        QueriesRenameRow.Visibility = Visibility.Visible;
        QueriesRenameBox.Text = summary.Name;
        _ = QueriesRenameBox.Focus();
        QueriesRenameBox.SelectAll();
    }

    private void QueriesRenameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            QueriesRenameRow.Visibility = Visibility.Collapsed;
            _pendingRenameSavedQueryId = null;
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Enter || _pendingRenameSavedQueryId is not { } id)
        {
            return;
        }
        BasesWorkspace?.RenameSavedQuery(id, QueriesRenameBox.Text);
        QueriesRenameRow.Visibility = Visibility.Collapsed;
        _pendingRenameSavedQueryId = null;
        e.Handled = true;
    }

    private void QueriesExport_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSavedQuery is not { } summary || BasesWorkspace is not { } workspace)
        {
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = summary.Name + ".base",
            DefaultExt = "base",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        // The FFI expects a VAULT-RELATIVE path (out-of-vault refuses
        // with InvalidPath → BasesSavedQueryExportFailed).
        string vaultRoot = (DataContext as VaultLifecycleViewModel)?.VaultPath ?? string.Empty;
        string chosen = dialog.FileName;
        string relative = chosen.StartsWith(vaultRoot, StringComparison.OrdinalIgnoreCase)
            ? chosen[vaultRoot.Length..].TrimStart('\\', '/').Replace('\\', '/')
            : chosen;
        workspace.ExportSavedQueryAsBase(summary.Id, relative);
    }

    private void QueriesDockQuery_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSavedQuery is { } summary)
        {
            BasesWorkspace?.DockSavedQueryToSidebar(summary.Id, summary.Name);
        }
    }

    private void QueriesDelete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSavedQuery is not { } summary)
        {
            return;
        }
        if (BasesDeleteConfirmation($"Delete saved query “{summary.Name}”?"))
        {
            BasesWorkspace?.DeleteSavedQuery(summary.Id);
        }
    }

    private void QueriesOpenBaseFile_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedBaseFile is { } file)
        {
            BasesWorkspace?.OpenPath(file.Path);
        }
    }

    private void QueriesBaseFilesList_DoubleClick(object sender, MouseButtonEventArgs e) =>
        QueriesOpenBaseFile_Click(sender, e);

    private void QueriesDockBaseFile_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedBaseFile is { } file)
        {
            BasesWorkspace?.DockBaseFileToSidebar(file.Path, file.Name);
        }
    }

    private void QueriesOpenDashboard_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDashboard is { } dashboard)
        {
            BasesWorkspace?.OpenDashboard(dashboard.Id, dashboard.Name);
        }
    }

    private void QueriesDashboardsList_DoubleClick(object sender, MouseButtonEventArgs e) =>
        QueriesOpenDashboard_Click(sender, e);

    private void QueriesEditDashboard_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDashboard is { } dashboard)
        {
            BasesWorkspace?.OpenDashboardEditor(dashboard.Id);
        }
    }

    private void QueriesDockDashboard_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDashboard is { } dashboard)
        {
            BasesWorkspace?.DockDashboardToSidebar(dashboard.Id, dashboard.Name);
        }
    }

    private void QueriesDeleteDashboard_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDashboard is not { } dashboard)
        {
            return;
        }
        if (BasesDeleteConfirmation($"Delete dashboard “{dashboard.Name}”?"))
        {
            BasesWorkspace?.DeleteDashboard(dashboard.Id);
        }
    }

    // --- Query builder overlay ---

    private System.Windows.Threading.DispatcherTimer? _builderPreviewDebounce;

    private void QueriesEditInBuilder_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSavedQuery is { } summary)
        {
            BasesWorkspace?.EditSavedQueryInBuilder(summary.Id);
        }
    }

    private void BuilderExpression_TextChanged(object sender, TextChangedEventArgs e)
    {
        // The mac preview cadence: 300 ms after the last keystroke.
        _builderPreviewDebounce ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300),
        };
        _builderPreviewDebounce.Stop();
        _builderPreviewDebounce.Tick -= BuilderPreviewTick;
        _builderPreviewDebounce.Tick += BuilderPreviewTick;
        _builderPreviewDebounce.Start();
    }

    private void BuilderPreviewTick(object? sender, EventArgs e)
    {
        _builderPreviewDebounce?.Stop();
        if (BasesWorkspace?.BaseQueryBuilderSheet is { } builder)
        {
            builder.PreviewPublished -= BuilderPreviewPublished;
            builder.PreviewPublished += BuilderPreviewPublished;
            builder.RunPreview();
        }
    }

    private void BuilderPreviewPublished(object? sender, EventArgs e)
    {
        if (BasesWorkspace?.BaseQueryBuilderSheet is not { } builder)
        {
            return;
        }
        BuilderPreviewLine.Text = builder.PreviewState switch
        {
            Bases.BuilderPreviewState.Idle => "Preview not loaded.",
            Bases.BuilderPreviewState.Loading => "Preview loading.",
            Bases.BuilderPreviewState.Failed =>
                $"Preview failed: {builder.PreviewMessage}",
            _ => builder.PreviewResult?.AudioSummary ?? string.Empty,
        };
    }

    private void BuilderAddCondition_Click(object sender, RoutedEventArgs e) =>
        BasesWorkspace?.BaseQueryBuilderSheet?.AddCondition();

    private void BuilderAddGroup_Click(object sender, RoutedEventArgs e) =>
        BasesWorkspace?.BaseQueryBuilderSheet?.AddGroup();

    private void BuilderRemoveCondition_Click(object sender, RoutedEventArgs e)
    {
        if (BasesWorkspace?.BaseQueryBuilderSheet is { } builder
            && (sender as FrameworkElement)?.DataContext
                is Bases.BuilderConditionRow row)
        {
            builder.RemoveCondition(row);
        }
    }

    private void BuilderSaveToView_Click(object sender, RoutedEventArgs e) =>
        BasesWorkspace?.BuilderSaveToView();

    private void BuilderUpdateSavedQuery_Click(object sender, RoutedEventArgs e) =>
        BasesWorkspace?.BuilderUpdateSavedQuery();

    private void BuilderSaveAsSavedQuery_Click(object sender, RoutedEventArgs e)
    {
        if (BasesWorkspace?.BaseQueryBuilderSheet is { } builder
            && builder.SaveAsSavedQuery(BuilderSaveNameBox.Text, description: null))
        {
            BasesWorkspace?.RefreshBaseQueries();
        }
    }

    private void BuilderSaveAsBase_Click(object sender, RoutedEventArgs e)
    {
        if (BasesWorkspace?.BaseQueryBuilderSheet is { } builder
            && builder.SaveAsBase(BuilderSavePathBox.Text))
        {
            BasesWorkspace?.RefreshBaseQueries();
        }
    }

    private void BuilderDone_Click(object sender, RoutedEventArgs e) =>
        BasesWorkspace?.CloseQueryBuilder();

    // --- Dashboard editor overlay ---

    private void DashboardEditorSave_Click(object sender, RoutedEventArgs e) =>
        BasesWorkspace?.SaveDashboardEditor();

    private void DashboardEditorCancel_Click(object sender, RoutedEventArgs e) =>
        BasesWorkspace?.CloseDashboardEditor();

    private void DashboardEditorAddSection_Click(object sender, RoutedEventArgs e)
    {
        if (BasesWorkspace?.DashboardEditorSheet is not { } editor
            || DashboardEditorQueryPicker.SelectedItem is not SavedQuerySummary summary)
        {
            return;
        }
        editor.Sections.Add(new Bases.DashboardEditorSection(summary.Id, summary.Name));
    }

    private void DashboardEditorRemoveSection_Click(object sender, RoutedEventArgs e)
    {
        if (BasesWorkspace?.DashboardEditorSheet is { } editor
            && (sender as FrameworkElement)?.DataContext
                is Bases.DashboardEditorSection section)
        {
            _ = editor.Sections.Remove(section);
        }
    }

    private void DashboardEditorMoveSection(int delta, object sender)
    {
        if (BasesWorkspace?.DashboardEditorSheet is not { } editor
            || (sender as FrameworkElement)?.DataContext
                is not Bases.DashboardEditorSection section)
        {
            return;
        }
        int index = editor.Sections.IndexOf(section);
        int target = index + delta;
        if (index < 0 || target < 0 || target >= editor.Sections.Count)
        {
            return;
        }
        editor.Sections.Move(index, target);
    }

    private void DashboardEditorMoveUp_Click(object sender, RoutedEventArgs e) =>
        DashboardEditorMoveSection(-1, sender);

    private void DashboardEditorMoveDown_Click(object sender, RoutedEventArgs e) =>
        DashboardEditorMoveSection(+1, sender);
}
