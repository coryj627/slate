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

    private void TrashCancel_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _focusBeforeTrashCancel = e.OldFocus;
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
            if (target is null || !TryFocus(target)) { FocusActiveEditorPane(); }
        }, DispatcherPriority.Input);
    }
}
