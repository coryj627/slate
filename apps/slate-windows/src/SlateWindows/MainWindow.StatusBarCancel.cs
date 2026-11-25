// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// The status bar's two Cancel buttons, the trash's and the import's (plan 47 CB-16, #1830;
/// the owner's answer of 2026-11-24, "Off, keys go back"). Each goes off in its own press,
/// and the keys go back to the element they came to it from, else the editor: first by its
/// stranded landing, which <see cref="Commands.CommandKeysFirst"/> takes before the button
/// goes off, then by the restore after an off nothing could move them ahead of. Each button
/// keeps its own record, so one never lands the keys where the other's came from.
/// </summary>
public partial class MainWindow
{
    private readonly StatusBarCancelKeys _trashCancelKeys = new();
    private readonly StatusBarCancelKeys _importCancelKeys = new();

    /// <summary>The element the keys came to the import's Cancel from, for the facts.</summary>
    internal IInputElement? ImportCancelCameFromForTests => _importCancelKeys.CameFrom;

    private void ImportCancel_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        CancelTookTheKeys(_importCancelKeys, e);

    private void ImportCancel_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        CancelLostTheKeys(_importCancelKeys, e);

    private void ImportCancel_AvailabilityChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        CancelAvailabilityChanged(_importCancelKeys, sender);

    /// <summary>The import's Cancel's own landing (<see cref="LandWhereCancelCameFrom"/>).</summary>
    private bool LandWhereImportCancelCameFrom() => LandWhereCancelCameFrom(_importCancelKeys);

    private void CancelTookTheKeys(StatusBarCancelKeys keys, KeyboardFocusChangedEventArgs e)
    {
        keys.FocusBefore = e.OldFocus;
        keys.CameFrom = e.OldFocus;
        keys.Sidebar = _viewModel.FileSidebar;
        keys.OwnedFocus = true;
    }

    private void CancelLostTheKeys(StatusBarCancelKeys keys, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is not null && !ReferenceEquals(e.NewFocus, this))
        {
            keys.OwnedFocus = false;
            // The merge judge's nit 6 (CB-17b): the landing reads it only while the keys
            // are on Cancel, so once they have gone to another element the window lets go.
            keys.CameFrom = null;
        }
    }

    /// <summary>
    /// Plan 47 CB-17b (#1866, CD11): a Cancel's own landing, for when it goes off under the
    /// keys (its press; its work ending). The keys go back to the element they came to Cancel
    /// from, else the scopes it was in, else the editor: where its restore put them after the
    /// off. <see cref="Commands.CommandKeysFirst"/> takes it before Cancel goes off, and the
    /// keys leaving Cancel give up the restore's claim. It reads
    /// <see cref="StatusBarCancelKeys.CameFrom"/>, which a hide does not clear (CB-17b red
    /// team F1).
    /// </summary>
    private bool LandWhereCancelCameFrom(StatusBarCancelKeys keys)
    {
        if (!LandCancelsTarget(keys.CameFrom))
        {
            FocusActiveEditorPane();
        }

        return true;
    }

    /// <summary>The element the keys came to a Cancel from, unless it is a command source
    /// whose command now refuses (Codex round 2): the Files pane's Cancel import goes off in
    /// the same raise as the status bar's, its IsEnabled not yet lowered, so landing on it
    /// would put the keys on a control about to go off. Such a target, like a dead one,
    /// gives way to the scopes it was in.</summary>
    private bool LandCancelsTarget(IInputElement? target) =>
        target is ICommandSource { Command: { } command } source && !command.CanExecute(source.CommandParameter)
            ? RegionFocusGuard.LandInScopesOf(target)
            : LandToken(target);

    private void CancelAvailabilityChanged(StatusBarCancelKeys keys, object sender)
    {
        if (sender is not Button button || (button.IsVisible && button.IsEnabled)
            || !keys.OwnedFocus) { return; }
        IInputElement? target = keys.FocusBefore;
        FilesSidebarViewModel? sidebar = keys.Sidebar;
        keys.OwnedFocus = false;
        keys.FocusBefore = null;
        keys.Sidebar = null;
        _ = Dispatcher.InvokeAsync(() =>
        {
            // Completion cannot take focus from a newly opened modal, another
            // vault, or a control the user selected while cancellation drained.
            if (!ReferenceEquals(sidebar, _viewModel.FileSidebar) || OpenModalSurface is not null
                || (Keyboard.FocusedElement is { } focused && !ReferenceEquals(focused, this)
                    && !ReferenceEquals(focused, button))) { return; }
            if (!LandCancelsTarget(target)) { FocusActiveEditorPane(); }
        }, DispatcherPriority.Input);
    }

    /// <summary>One status-bar Cancel's record of where the keys came to it from.</summary>
    private sealed class StatusBarCancelKeys
    {
        /// <summary>The restore's copy: cleared as soon as Cancel is hidden or off.</summary>
        public IInputElement? FocusBefore { get; set; }

        /// <summary>The landing's copy: only Cancel taking the keys sets it, and only the
        /// keys going to another element clear it (CB-17b red team F1).</summary>
        public IInputElement? CameFrom { get; set; }

        public FilesSidebarViewModel? Sidebar { get; set; }

        public bool OwnedFocus { get; set; }
    }
}
