// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SlateWindows.Grids;
using SlateWindows.Panels;

namespace SlateWindows;

/// <summary>
/// W4-4 (#736): the window layer of the property surfaces — the
/// dialogs the workspace seam delegates out (delete confirmation
/// with CANCEL as the default action, the three-way conflict
/// resolution, the vault-rooted wikilink picker), sheet focus
/// choreography, and the bulk-rename preview grid binding.
/// </summary>
public partial class MainWindow
{
    private BulkRenameViewModel? _observedBulkRenameSheet;
    private IInputElement? _focusBeforeSheet;

    private static readonly IReadOnlyList<AccessibleGridColumn> BulkRenameColumns =
    [
        new AccessibleGridColumn
        {
            Header = "Path",
            Cell = row => ((BulkRenameViewModel.PreviewRow)row).Path,
            IsRowHeader = true,
        },
        new AccessibleGridColumn
        {
            Header = "Status",
            Cell = row => ((BulkRenameViewModel.PreviewRow)row).Status,
        },
        new AccessibleGridColumn
        {
            Header = "Before",
            Cell = row => ((BulkRenameViewModel.PreviewRow)row).Before,
        },
        new AccessibleGridColumn
        {
            Header = "After",
            Cell = row => ((BulkRenameViewModel.PreviewRow)row).After,
        },
    ];

    private void WireWorkspaceProperties(WorkspaceViewModel workspace)
    {
        workspace.PropertyDeleteConfirmation = ConfirmPropertyDelete;
        workspace.PropertyConflictDialog = ShowPropertyConflictDialog;
        workspace.WikilinkPicker = PickWikilinkTarget;
        workspace.PropertyChanged += Workspace_PropertySheetChanged;
    }

    private void UnwireWorkspaceProperties(WorkspaceViewModel workspace)
    {
        workspace.PropertyChanged -= Workspace_PropertySheetChanged;
        ObserveBulkRenameSheet(null);
    }

    /// <summary>Cancel is the DEFAULT action and never deletes
    /// (feature contract 5); the modal restores focus to the row.</summary>
    private bool ConfirmPropertyDelete(string title, string message)
    {
        MessageBoxResult result = MessageBox.Show(
            this,
            message,
            title,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);
        return result == MessageBoxResult.OK;
    }

    /// <summary>The conflict resolution (§2.6) on the MessageBox
    /// precedent (the vault-close YesNoCancel shape): Yes = Keep
    /// Mine, No = Reload from Disk, Cancel (default) leaves the
    /// panel as it was.</summary>
    private void ShowPropertyConflictDialog(
        string filename, string key, Action keepMine, Action reloadFromDisk)
    {
        string message =
            PropertyPhrase.ConflictMessage(filename, key)
            + $"\n\nYes — Keep Mine: {PropertyPhrase.ConflictKeepMineHint}"
            + $"\nNo — Reload from Disk: {PropertyPhrase.ConflictReloadHint}"
            + $"\nCancel: {PropertyPhrase.ConflictCancelHint}";
        MessageBoxResult result = MessageBox.Show(
            this,
            message,
            PropertyPhrase.ConflictTitle,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);
        if (result == MessageBoxResult.Yes)
        {
            keepMine();
        }
        else if (result == MessageBoxResult.No)
        {
            reloadFromDisk();
        }
    }

    /// <summary>Vault-rooted .md picker: stores the vault-relative
    /// path minus ".md"; an outside-vault selection degrades to the
    /// filename (§2.2). The result lands in the row DRAFT only.</summary>
    private string? PickWikilinkTarget()
    {
        string vaultRoot = _viewModel.VaultPath;
        if (string.IsNullOrEmpty(vaultRoot))
        {
            return null;
        }
        var dialog = new OpenFileDialog
        {
            Title = "Pick vault file",
            InitialDirectory = vaultRoot,
            Filter = "Markdown notes (*.md)|*.md",
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return null;
        }
        string root = System.IO.Path.TrimEndingDirectorySeparator(vaultRoot);
        string target = dialog.FileName.StartsWith(
                root + System.IO.Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            ? System.IO.Path.GetRelativePath(root, dialog.FileName).Replace('\\', '/')
            : System.IO.Path.GetFileName(dialog.FileName);
        return target.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? target[..^3]
            : target;
    }

    private void Workspace_PropertySheetChanged(
        object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (sender is not WorkspaceViewModel workspace)
        {
            return;
        }
        if (eventArgs.PropertyName == nameof(WorkspaceViewModel.AddPropertySheet))
        {
            if (workspace.AddPropertySheet is not null)
            {
                _focusBeforeSheet ??= Keyboard.FocusedElement;
                _ = Dispatcher.InvokeAsync(
                    () => AddPropertyKeyTextBox.Focus(),
                    System.Windows.Threading.DispatcherPriority.Input);
            }
            else
            {
                RestoreFocusAfterSheet();
            }
        }
        else if (eventArgs.PropertyName == nameof(WorkspaceViewModel.BulkRenameSheet))
        {
            ObserveBulkRenameSheet(workspace.BulkRenameSheet);
            if (workspace.BulkRenameSheet is not null)
            {
                _focusBeforeSheet ??= Keyboard.FocusedElement;
                BindBulkRenameGrid(workspace.BulkRenameSheet);
                _ = Dispatcher.InvokeAsync(
                    () => BulkRenameOldKeyTextBox.Focus(),
                    System.Windows.Threading.DispatcherPriority.Input);
            }
            else
            {
                RestoreFocusAfterSheet();
            }
        }
    }

    private void RestoreFocusAfterSheet()
    {
        IInputElement? previous = _focusBeforeSheet;
        _focusBeforeSheet = null;
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                // Codex round 2 (#742): a palette-invoked sheet captures
                // the PALETTE's text box as its return target, and the
                // palette dismisses while the sheet is up — so the
                // captured element is collapsed by the time the sheet
                // closes, Focus() fails, and the false result was
                // ignored. With search still open beneath (it stays open
                // by design), the exposed overlay owned the keys but not
                // text focus: typing went nowhere until a click. When
                // the captured target cannot take focus, hand focus to
                // the topmost modal owner — the search box when search
                // is topmost.
                // Codex round 3 (#742): search topmost takes priority
                // over the captured target, not merely over a FAILED
                // restore — the pre-sheet element is behind the overlay
                // by construction. One shared rule for all three sheet
                // restore sites; see TryFocusSearchIfTopmost.
                if (TryFocusSearchIfTopmost())
                {
                    return;
                }

                _ = previous?.Focus();
            },
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void ObserveBulkRenameSheet(BulkRenameViewModel? sheet)
    {
        if (ReferenceEquals(_observedBulkRenameSheet, sheet))
        {
            return;
        }
        if (_observedBulkRenameSheet is not null)
        {
            _observedBulkRenameSheet.RunPublished -= BulkRenameSheet_RunPublished;
        }
        _observedBulkRenameSheet = sheet;
        if (sheet is not null)
        {
            sheet.RunPublished += BulkRenameSheet_RunPublished;
        }
    }

    private void BulkRenameSheet_RunPublished()
    {
        if (_observedBulkRenameSheet is { } sheet)
        {
            BindBulkRenameGrid(sheet);
        }
    }

    private void BindBulkRenameGrid(BulkRenameViewModel sheet)
    {
        BulkRenamePreviewGrid.Bind(
            BulkRenameColumns,
            sheet.Rows.Cast<object>().ToArray(),
            summary: sheet.FooterText.Length > 0
                ? sheet.FooterText
                : PropertyPhrase.BulkRenameEmptyState,
            accessibilityLabel: "Rename preview");
    }

    private void AddPropertyAdd_Click(object sender, RoutedEventArgs e) =>
        _ = _observedWorkspace?.AddPropertySheet?.Add();

    private void AddPropertyOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _observedWorkspace?.CloseAddPropertySheetCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter
            && e.OriginalSource is not System.Windows.Controls.Button)
        {
            _ = _observedWorkspace?.AddPropertySheet?.Add();
            e.Handled = true;
        }
    }

    private void BulkRenamePreview_Click(object sender, RoutedEventArgs e) =>
        _observedWorkspace?.BulkRenameSheet?.Preview();

    private void BulkRenameApply_Click(object sender, RoutedEventArgs e) =>
        _ = _observedWorkspace?.BulkRenameSheet?.Apply();

    private void BulkRenameOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            // Cancels in-flight core work through the CancelToken
            // and closes (CloseBulkRenameSheet owns both).
            _observedWorkspace?.CloseBulkRenameSheetCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter
            && e.OriginalSource is not System.Windows.Controls.Button
            && _observedWorkspace?.BulkRenameSheet is { CanPreview: true } sheet)
        {
            // The default action is PREVIEW, never Apply (§2.5).
            sheet.Preview();
            e.Handled = true;
        }
    }
}
