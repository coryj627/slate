// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SlateWindows.FileManagement;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W5-4 (#744) F4 shell wiring for the Move-To picker: the modal
/// admission, focus on present, the sheet keyboard routes, and the
/// end-of-flow focus restore — the template sheets' split, mirrored.
/// The sidebar view model owns the flow; this part owns only what
/// needs a live visual tree.
/// </summary>
public partial class MainWindow
{
    private IInputElement? _focusBeforeMoveTo;

    /// <summary>Spoken when the verb asks for the picker beneath an
    /// unrelated sheet — the template dialog-busy copy's shape.</summary>
    internal const string MoveToDialogBusyReason =
        "Finish or cancel the current dialog before moving files.";

    /// <summary>
    /// Applies <see cref="ModalSurfaces.DecideMoveToOpen"/>, performing
    /// the dismissal it calls for — the template twin. Runs inside the
    /// sidebar's open (the admission seam), so chord, button, and
    /// palette row pass one gate.
    /// </summary>
    private bool TryClearTheWayForMoveTo()
    {
        switch (ModalSurfaces.DecideMoveToOpen(OpenModalSurface))
        {
            case PaletteOpenDecision.Open:
                return true;
            case PaletteOpenDecision.DismissQuickOpenThenOpen:
                _focusBeforeMoveTo ??= ConsumePreSwitcherFocus();
                _viewModel.QuickSwitcher!.Dismiss();
                return true;
            case PaletteOpenDecision.DismissSearchThenOpen:
                IInputElement? preSearch = ConsumePreSearchFocus();
                _viewModel.Search.Supersede();
                _focusBeforeMoveTo ??= preSearch;
                return true;
            case PaletteOpenDecision.DismissPaletteThenOpen:
                IInputElement? prePalette = ConsumePrePaletteFocus();
                _viewModel.Palette.Dismiss();
                _focusBeforeMoveTo ??= prePalette;
                return true;
            default:
                // W0.5-3 residue: dialog-busy guidance copy.
                _announcer.Post(new A11yEvent.HostComposed(
                    MoveToDialogBusyReason, A11yPriority.Medium));
                return false;
        }
    }

    private void FileSidebar_MoveToSheetChanged(
        object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName != nameof(FilesSidebarViewModel.MoveToSheet)
            || sender is not FilesSidebarViewModel sidebar)
        {
            return;
        }

        if (sidebar.MoveToSheet is not null)
        {
            _focusBeforeMoveTo ??= CapturePreSheetFocus();
            _ = Dispatcher.InvokeAsync(
                () => MoveToFilterTextBox.Focus(), DispatcherPriority.Input);
        }
        else
        {
            RestoreFocusAfterMoveTo();
        }
    }

    private void RestoreFocusAfterMoveTo()
    {
        IInputElement? focusBefore = _focusBeforeMoveTo;
        _focusBeforeMoveTo = null;
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                // Invariant 4's backstop, then the supersession
                // stand-down — the template restore's shape.
                if (TryFocusSearchIfTopmost())
                {
                    return;
                }

                if (OpenModalSurface is not null)
                {
                    return;
                }

                if (focusBefore is null || !TryFocus(focusBefore))
                {
                    _ = FilesTree.Focus();
                }
            },
            DispatcherPriority.Input);
    }

    private void MoveToOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        MoveToPickerViewModel? picker = _viewModel.FileSidebar?.MoveToSheet;
        if (picker is null)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape)
        {
            picker.CancelCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Down/Up from the filter box drive the list without leaving
        // the box (the quick-open pattern — filter-as-you-type stays
        // keyboard-first).
        if (Keyboard.Modifiers == ModifierKeys.None
            && e.Key is Key.Down or Key.Up
            && ReferenceEquals(Keyboard.FocusedElement, MoveToFilterTextBox)
            && picker.Rows.Count > 0)
        {
            int index = -1;
            for (int i = 0; i < picker.Rows.Count; i++)
            {
                if (ReferenceEquals(picker.Rows[i], picker.SelectedRow))
                {
                    index = i;
                    break;
                }
            }

            int next = e.Key == Key.Down
                ? System.Math.Min(index + 1, picker.Rows.Count - 1)
                : System.Math.Max(index - 1, 0);
            picker.SelectedRow = picker.Rows[next];
            MoveToList.ScrollIntoView(picker.SelectedRow);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Enter)
        {
            // One physical press, one move (the template picker's
            // held-Enter fence).
            if (e.IsRepeat)
            {
                e.Handled = true;
                return;
            }

            // A focused button — Cancel — keeps its own Enter.
            if (Keyboard.FocusedElement is Button)
            {
                return;
            }

            if (picker.SelectedRow is MoveToRowViewModel row)
            {
                picker.ActivateCommand.Execute(row);
                e.Handled = true;
            }
        }
    }

    private void MoveToList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Only a double-click on an actual row activates (the
        // clicked-row contract shared by every list surface).
        if (_viewModel.FileSidebar?.MoveToSheet is MoveToPickerViewModel picker
            && e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(MoveToList, source) is not null
            && MoveToList.SelectedItem is MoveToRowViewModel row)
        {
            picker.ActivateCommand.Execute(row);
            e.Handled = true;
        }
    }
}
