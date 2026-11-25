// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Input;

namespace SlateWindows;

public partial class MainWindow
{
    /// <summary>The element the keys came to the trash's Cancel from, for the facts.</summary>
    internal IInputElement? TrashCancelCameFromForTests => _trashCancelKeys.CameFrom;

    private void TrashFallback_KeyDown(object sender, KeyEventArgs e)
    {
        // This bubbling fallback runs only after the focused control declines
        // Escape. It belongs to the content root: the Window's disabled Escape
        // KeyBinding still consumes input before Window instance handlers run.
        if (!e.Handled && e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None
            && OpenModalSurface is null && _viewModel.FileSidebar?.ShowTrashProgress == true
            && _viewModel.Workspace?.ActiveGroup.ActiveTab?.EditorInteractions?.ClosePopoverCommand.CanExecute(null) != true)
        {
            _viewModel.FileSidebar.CancelTrashCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void TrashCancel_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        CancelTookTheKeys(_trashCancelKeys, e);

    private void TrashCancel_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        CancelLostTheKeys(_trashCancelKeys, e);

    /// <summary>Plan 47 CB-17b (#1866, CD11): the trash's Cancel's own landing, for when it
    /// goes off under the keys (its press sets Stopping; the trash's end hides it)
    /// (<see cref="LandWhereCancelCameFrom"/>).</summary>
    private bool LandWhereTrashCancelCameFrom() => LandWhereCancelCameFrom(_trashCancelKeys);

    private void TrashCancel_AvailabilityChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        CancelAvailabilityChanged(_trashCancelKeys, sender);
}
