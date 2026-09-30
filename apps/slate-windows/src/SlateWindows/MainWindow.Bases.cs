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
    private Func<string, bool>? _basesDeleteConfirmation;

    /// <summary>Injectable confirmation (the W4-4 dialog-seam
    /// pattern): production shows a message box owned by the shell (#1275:
    /// an owned box disables the shell, which seals the palette); facts
    /// inject.</summary>
    internal Func<string, bool> BasesDeleteConfirmation
    {
        get => _basesDeleteConfirmation ??= ConfirmBasesDelete;
        set => _basesDeleteConfirmation = value;
    }

    private string? _pendingRenameSavedQueryId;

    private WorkspaceViewModel? BasesWorkspace =>
        (DataContext as VaultLifecycleViewModel)?.Workspace;

    // --- Overlay focus lifecycle (the W4-5 citation-sheet pattern:
    // capture at open, initial focus when ready, Escape closes,
    // restore on close — red team round 1: both Bases overlays
    // appeared without moving focus and closed into limbo). ---

    private IInputElement? _focusBeforeBuilder;
    private IInputElement? _focusBeforeDashboardEditor;

    private void WireWorkspaceBases(WorkspaceViewModel workspace)
    {
        workspace.PropertyChanged += Workspace_BasesSheetChanged;
        workspace.BaseQueriesRepublishing += Queries_Republishing;
        workspace.BaseQueriesRepublished += Queries_Republished;
        // #1275: the scope question is owned by the shell, like every
        // other prompt it raises.
        workspace.BasesExportScopePrompt = verb => WorkspaceViewModel.AskBasesExportScope(this, verb);
    }

    private bool ConfirmBasesDelete(string message) => MessageBox.Show(
        this,
        message,
        "Slate",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private void UnwireWorkspaceBases(WorkspaceViewModel workspace)
    {
        workspace.PropertyChanged -= Workspace_BasesSheetChanged;
        workspace.BaseQueriesRepublishing -= Queries_Republishing;
        workspace.BaseQueriesRepublished -= Queries_Republished;
    }

    /// <summary>The Queries leaf's selections, by identity, as the registry
    /// rebuild found them.</summary>
    private (string? SavedQuery, string? BaseFile, string? Dashboard) _queriesSelection;

    private void Queries_Republishing() =>
        _queriesSelection = (
            (QueriesSavedList.SelectedItem as SavedQuerySummary)?.Id,
            (QueriesBaseFilesList.SelectedItem as BaseFileSummary)?.Path,
            (QueriesDashboardsList.SelectedItem as DashboardSummary)?.Id);

    /// <summary>
    /// W7-7 PR 4 (#1247, R-5; codex PR 4 round 6 high 3 and the completeness
    /// sweep's G5): the registry refresh rebuilds the three lists (Clear +
    /// Add), which drops every selection, and every action button follows
    /// its list's selection: Pin from its own button disabled the button
    /// under the keys, and WPF's re-evaluation stranded them at the window;
    /// a rename's refresh removed the row the keys were on. Each list's
    /// selection is re-seated by identity here, in the rebuild's own
    /// dispatcher operation, so the buttons are enabled again before that
    /// re-evaluation runs and the list keeper lands a removed row's keys on
    /// the fresh row of the same query. A button whose selection is gone
    /// for good (Delete) hands the keys to its list's row, else to the
    /// leaf's own landing.
    /// </summary>
    private void Queries_Republished()
    {
        (string? query, string? file, string? dashboard) = _queriesSelection;
        bool lost = !Reselect(QueriesSavedList, query, (SavedQuerySummary summary) => summary.Id)
            | !Reselect(QueriesBaseFilesList, file, (BaseFileSummary summary) => summary.Path)
            | !Reselect(QueriesDashboardsList, dashboard, (DashboardSummary summary) => summary.Id);
        if (!lost
            || Keyboard.FocusedElement is not UIElement { IsEnabled: false } disabled
            || LeafBodyOf(QueriesSavedList) is not { } body
            || !IsWithin(disabled, body))
        {
            return;
        }

        ListBox home = ListAbove(disabled) ?? QueriesSavedList;
        if (!(home.HasItems && SelectorFocus.FocusFirstOrSelectedItem(home)) && !LandInLeaf(body))
        {
            // Not even the rail's row took them — the pane's last stable
            // stop: WPF's own re-evaluation keeps them in the window.
        }
    }

    /// <summary>The list an action row acts on: in the leaf's column each
    /// row of buttons sits just after its list.</summary>
    private static ListBox? ListAbove(DependencyObject element)
    {
        for (DependencyObject? current = element; current is not null; current = LogicalTreeHelper.GetParent(current))
        {
            if (LogicalTreeHelper.GetParent(current) is Panel column && current is UIElement child)
            {
                for (int index = column.Children.IndexOf(child) - 1; index >= 0; index--)
                {
                    if (column.Children[index] is ListBox list)
                    {
                        return list;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>Selects the item with <paramref name="id"/>; true when there
    /// was none to re-seat or it was re-seated.</summary>
    private static bool Reselect<T>(ListBox list, string? id, Func<T, string> identity)
        where T : class
    {
        if (id is null)
        {
            return true;
        }

        T? fresh = list.Items.OfType<T>().FirstOrDefault(item => string.Equals(identity(item), id, StringComparison.Ordinal));
        list.SelectedItem = fresh;
        return fresh is not null;
    }

    private void Workspace_BasesSheetChanged(
        object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (sender is not WorkspaceViewModel workspace)
        {
            return;
        }
        switch (eventArgs.PropertyName)
        {
            case nameof(WorkspaceViewModel.BaseQueryBuilderSheet):
                if (workspace.BaseQueryBuilderSheet is not null)
                {
                    // Through the shared helper: this sheet presents
                    // from the edit-JSON continuation, so a picker may
                    // still be open and focused here (red team after
                    // round 11) — its pre-open token is the lineage.
                    _focusBeforeBuilder = CapturePreSheetFocus();
                    FocusWhenReady(() => BuilderCombinatorBox.Focus());
                }
                else
                {
                    RestoreBasesOverlayFocus(_focusBeforeBuilder);
                    _focusBeforeBuilder = null;
                }
                break;
            case nameof(WorkspaceViewModel.DashboardEditorSheet):
                if (workspace.DashboardEditorSheet is not null)
                {
                    _focusBeforeDashboardEditor = CapturePreSheetFocus();
                    FocusWhenReady(() => DashboardEditorNameBox.Focus());
                }
                else
                {
                    RestoreBasesOverlayFocus(_focusBeforeDashboardEditor);
                    _focusBeforeDashboardEditor = null;
                }
                break;
            default:
                break;
        }
    }

    /// <summary>The Bases-overlay restore: the captured element is
    /// often a DataGridCell that the save-triggered republish
    /// DESTROYED, and the citations helper's miss-fallback focuses
    /// the citations list — the wrong panel entirely for Bases (red
    /// team round 2). Miss here falls back to the active tab's pane.</summary>
    private void RestoreBasesOverlayFocus(IInputElement? token)
    {
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                // Codex round 3 (#742): search topmost takes priority —
                // see TryFocusSearchIfTopmost.
                if (TryFocusSearchIfTopmost())
                {
                    return;
                }

                // R-5 (#1247; codex round 4): a list, tree or grid token
                // restores onto its row or cell, never the bare container —
                // and a DEAD token (a Queries row the save's refresh rebuilt,
                // codex PR 4b r1 F3) onto the rows it was in, else the
                // scopes it was in (LandToken), before the editor.
                if (LandToken(token))
                {
                    return;
                }
                BasesWorkspace?.RequestActiveEditorFocus();
            },
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void BaseQueryBuilderOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            BasesWorkspace?.CloseQueryBuilder();
            e.Handled = true;
        }
    }

    private void DashboardEditorOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            BasesWorkspace?.CloseDashboardEditor();
            e.Handled = true;
        }
    }

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

    // W7-7 PR 4 (#1247; codex PR 4's final check): a double-click acts only
    // on a row it HIT — a double-click on a list's empty area ran whatever
    // query was selected. A pressed row is the selection by then (a list
    // selects on the press).
    private void QueriesSavedList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectorFocus.ClickedItem(QueriesSavedList, e.OriginalSource) is not null)
        {
            QueriesRun_Click(sender, e);
        }
    }

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
            // Focus returns to the list the rename came from — the
            // collapsed row must not strand focus (red team round 1) — on
            // the renamed query's row, never the bare list (R-5, #1247).
            LandOnSavedQueries();
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
        LandOnSavedQueries();
        e.Handled = true;
    }

    /// <summary>The saved query's row; a row that cannot be landed yet
    /// leaves the keys to the pane's stable stop, the rail's row (R-5,
    /// #1247).</summary>
    private void LandOnSavedQueries()
    {
        if (!SelectorFocus.FocusFirstOrSelectedItem(QueriesSavedList))
        {
            _ = SelectorFocus.FocusFirstOrSelectedItem(RightPaneLeavesList);
        }
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
        // The FFI expects a VAULT-RELATIVE path; the prefix test needs
        // the trailing separator or a SIBLING directory sharing the
        // prefix (C:\vaults\work vs C:\vaults\work2) mangles into an
        // in-vault path (red team round 2). Out-of-vault falls through
        // absolute and refuses with BasesPathOutsideVault.
        string vaultRoot = (DataContext as VaultLifecycleViewModel)?.VaultPath ?? string.Empty;
        string rootPrefix = vaultRoot.TrimEnd('\\', '/') + "\\";
        string chosen = dialog.FileName;
        string relative =
            chosen.Replace('/', '\\').StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                ? chosen[rootPrefix.Length..].Replace('\\', '/')
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

    private void QueriesBaseFilesList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectorFocus.ClickedItem(QueriesBaseFilesList, e.OriginalSource) is not null)
        {
            QueriesOpenBaseFile_Click(sender, e);
        }
    }

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

    private void QueriesDashboardsList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectorFocus.ClickedItem(QueriesDashboardsList, e.OriginalSource) is not null)
        {
            QueriesOpenDashboard_Click(sender, e);
        }
    }

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
