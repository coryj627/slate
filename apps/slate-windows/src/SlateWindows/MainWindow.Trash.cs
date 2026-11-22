// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SlateWindows;

public partial class MainWindow
{
    private IInputElement? _focusBeforeTrashCancel;
    private FilesSidebarViewModel? _trashCancelSidebar;
    private bool _trashCancelOwnedFocus;

    /// <summary>Plan 47 CB-17b (red team F1): the element the keys came to Cancel from, for
    /// Cancel's own landing. Only Cancel taking the keys sets it: the restore's copy is
    /// cleared as soon as Cancel is hidden, which the trash does before it turns Cancel off
    /// when it asks to confirm.</summary>
    private IInputElement? _trashCancelCameFrom;

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

    private void TrashCancel_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _focusBeforeTrashCancel = e.OldFocus;
        _trashCancelCameFrom = e.OldFocus;
        _trashCancelSidebar = _viewModel.FileSidebar;
        _trashCancelOwnedFocus = true;
    }

    private void TrashCancel_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is not null && !ReferenceEquals(e.NewFocus, this))
        {
            _trashCancelOwnedFocus = false;
        }
    }

    /// <summary>
    /// Plan 47 CB-17b (#1866, CD11): Cancel's own landing, for when it goes off under the
    /// keys (its press sets Stopping; the trash's end hides it). The keys go back to the
    /// element they came to Cancel from, else the scopes it was in, else the editor: where
    /// its restore put them after the off (<see cref="TrashCancel_AvailabilityChanged"/>).
    /// <see cref="Commands.CommandKeysFirst"/> takes it before Cancel goes off; the keys
    /// leaving Cancel give up the restore's claim (<see cref="TrashCancel_LostKeyboardFocus"/>),
    /// and it stands down. It runs while the keys are on Cancel, so none of the restore's
    /// later stand-downs (a modal opened, another vault, a control chosen meanwhile) can
    /// have happened; if nothing takes the keys, the restore is still there after the off.
    /// It reads <see cref="_trashCancelCameFrom"/>, which a hide does not clear (red team F1).
    /// </summary>
    private bool LandWhereTrashCancelCameFrom()
    {
        if (!LandToken(_trashCancelCameFrom))
        {
            FocusActiveEditorPane();
        }

        return true;
    }

    private void TrashCancel_AvailabilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not Button button || (button.IsVisible && button.IsEnabled)
            || !_trashCancelOwnedFocus) { return; }
        IInputElement? target = _focusBeforeTrashCancel;
        FilesSidebarViewModel? sidebar = _trashCancelSidebar;
        _trashCancelOwnedFocus = false;
        _focusBeforeTrashCancel = null;
        _trashCancelSidebar = null;
        _ = Dispatcher.InvokeAsync(() =>
        {
            // Completion cannot take focus from a newly opened modal, another
            // vault, or a control the user selected while cancellation drained.
            if (!ReferenceEquals(sidebar, _viewModel.FileSidebar) || OpenModalSurface is not null
                || (Keyboard.FocusedElement is { } focused && !ReferenceEquals(focused, this)
                    && !ReferenceEquals(focused, button))) { return; }
            if (!LandToken(target)) { FocusActiveEditorPane(); }
        }, DispatcherPriority.Input);
    }
}
