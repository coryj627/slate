// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SlateWindows.Templates;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W5-3 (#743) create-from-template shell wiring: the modal admission
/// (contract T9), per-state focus on present, the sheet keyboard
/// routes, and the end-of-flow focus restore with its
/// focus-follows-content stand-down (T8). The view models own the flow;
/// this part owns only what needs a live visual tree — the search
/// overlay's split, mirrored.
/// </summary>
public partial class MainWindow
{
    private IInputElement? _focusBeforeTemplates;
    private TemplateFlowViewModel? _observedTemplateFlow;
    private TemplatePickerViewModel? _observedTemplatePicker;

    /// <summary>mac `templateDialogBusyReason`, verbatim — spoken when
    /// the chord or menu asks for the flow beneath an UNRELATED sheet.</summary>
    internal const string TemplateDialogBusyReason =
        "Finish or cancel the current dialog before creating from a template.";

    /// <summary>mac `templateFlowBusyReason`, verbatim — spoken when the
    /// refusing surface IS the template flow itself (re-entry). mac
    /// partitions the two copies (AppState.swift:7901-7904); a single
    /// dialog-busy string would tell the user to finish "the current
    /// dialog" when that dialog is the create-from-template flow.</summary>
    internal const string TemplateFlowBusyReason =
        "Finish or cancel the current template note before starting another.";

    private void WireWorkspaceTemplates(WorkspaceViewModel workspace)
    {
        workspace.TemplateOpenAdmission = TryClearTheWayForTemplates;
        workspace.PropertyChanged += Workspace_TemplateSheetChanged;
    }

    private void UnwireWorkspaceTemplates(WorkspaceViewModel workspace)
    {
        workspace.PropertyChanged -= Workspace_TemplateSheetChanged;
        ObserveTemplateFlow(null);
        ObserveTemplatePicker(null);
        // A vault transition mid-flow never runs the restore (the sheet
        // properties die with the discarded workspace), so the captured
        // token must not leak into the next vault's first flow.
        _focusBeforeTemplates = null;
    }

    /// <summary>
    /// Applies <see cref="ModalSurfaces.DecideTemplateOpen"/>,
    /// performing the dismissal it calls for — the template twin of
    /// <see cref="TryClearTheWayForSearch"/>. Runs INSIDE the
    /// workspace's open (the admission seam), so every opener — chord,
    /// menu, palette row — passes one gate.
    /// </summary>
    private bool TryClearTheWayForTemplates()
    {
        switch (ModalSurfaces.DecideTemplateOpen(OpenModalSurface))
        {
            case PaletteOpenDecision.Open:
                return true;
            case PaletteOpenDecision.DismissQuickOpenThenOpen:
                // The lineage handoff (invariant 5): without adopting,
                // the sheet's ??= capture would grab the collapsing
                // Quick Open box and the end-of-flow restore would fall
                // back to the editor instead of where the user started.
                _focusBeforeTemplates ??= ConsumePreSwitcherFocus();
                _viewModel.QuickSwitcher!.Dismiss();
                return true;
            case PaletteOpenDecision.DismissSearchThenOpen:
                // SD-5's arm: Supersede keeps query AND scope so
                // Ctrl+Shift+F after the flow restores what the user
                // had; search's own queued restore stands down while a
                // higher surface is up, and consuming first makes it
                // inert either way.
                IInputElement? preSearch = ConsumePreSearchFocus();
                _viewModel.Search.Supersede();
                _focusBeforeTemplates ??= preSearch;
                return true;
            case PaletteOpenDecision.DismissPaletteThenOpen:
                // mac's rule at its registry dispatch: the palette is an
                // action launcher, retired before the sheet stages so
                // the two presentations never overlap. Serves both the
                // palette-invoked command (P9 invokes before dismissing)
                // and a chord which arrives while the palette is up.
                IInputElement? prePalette = ConsumePrePaletteFocus();
                _viewModel.Palette.Dismiss();
                _focusBeforeTemplates ??= prePalette;
                return true;
            default:
                // W0.5-3 residue: template availability/busy copy serves
                // double duty as dialog guidance (contracts doc T10).
                // mac's two-string partition: re-entry over the flow's
                // own surfaces speaks the flow-busy copy, any other
                // sheet the dialog-busy copy.
                bool refusedByOwnFlow = OpenModalSurface
                    is ModalSurface.TemplatePicker or ModalSurface.TemplateFlow;
                _announcer.Post(new A11yEvent.HostComposed(
                    refusedByOwnFlow ? TemplateFlowBusyReason : TemplateDialogBusyReason,
                    A11yPriority.Medium));
                return false;
        }
    }

    private void Workspace_TemplateSheetChanged(
        object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (sender is not WorkspaceViewModel workspace)
        {
            return;
        }

        if (eventArgs.PropertyName == nameof(WorkspaceViewModel.TemplatePickerSheet))
        {
            ObserveTemplatePicker(workspace.TemplatePickerSheet);
            if (workspace.TemplatePickerSheet is not null)
            {
                _focusBeforeTemplates ??= CapturePreSheetFocus();
                _ = Dispatcher.InvokeAsync(
                    FocusTemplatePicker, DispatcherPriority.Input);
            }
            else if (workspace.TemplateFlowSheet is null)
            {
                RestoreFocusAfterTemplates();
            }
        }
        else if (eventArgs.PropertyName == nameof(WorkspaceViewModel.TemplateFlowSheet))
        {
            ObserveTemplateFlow(workspace.TemplateFlowSheet);
            if (workspace.TemplateFlowSheet is not null)
            {
                _focusBeforeTemplates ??= CapturePreSheetFocus();
                _ = Dispatcher.InvokeAsync(
                    FocusTemplateFlowStep, DispatcherPriority.Input);
            }
            else if (workspace.TemplatePickerSheet is null)
            {
                RestoreFocusAfterTemplates();
            }
        }
    }

    private void ObserveTemplatePicker(TemplatePickerViewModel? picker)
    {
        if (ReferenceEquals(_observedTemplatePicker, picker))
        {
            return;
        }

        if (_observedTemplatePicker is not null)
        {
            _observedTemplatePicker.PropertyChanged -= TemplatePicker_PropertyChanged;
        }

        _observedTemplatePicker = picker;
        if (picker is not null)
        {
            picker.PropertyChanged += TemplatePicker_PropertyChanged;
        }
    }

    private void TemplatePicker_PropertyChanged(
        object? sender, PropertyChangedEventArgs eventArgs)
    {
        // A Try Again landing collapses the state panel holding the
        // focused button and seats a different one (red team, a11y
        // finding 1): without re-running the per-state focus, keyboard
        // focus is evicted from the collapsed element to the window —
        // outside the sheet's tab cycle, where Esc no longer reaches
        // the overlay's handler. mac wires exactly this observer
        // (.onChange(of: templateAvailability) → updateFocus,
        // TemplatePicker.swift:63-65).
        if (eventArgs.PropertyName == nameof(TemplatePickerViewModel.State))
        {
            _ = Dispatcher.InvokeAsync(
                FocusTemplatePicker, DispatcherPriority.Input);
        }
    }

    private void ObserveTemplateFlow(TemplateFlowViewModel? flow)
    {
        if (ReferenceEquals(_observedTemplateFlow, flow))
        {
            return;
        }

        if (_observedTemplateFlow is not null)
        {
            _observedTemplateFlow.PropertyChanged -= TemplateFlow_PropertyChanged;
        }

        _observedTemplateFlow = flow;
        if (flow is not null)
        {
            flow.PropertyChanged += TemplateFlow_PropertyChanged;
        }
    }

    private void TemplateFlow_PropertyChanged(
        object? sender, PropertyChangedEventArgs eventArgs)
    {
        // The prompts→name transition (and a failed create's
        // re-present) moves focus to the step's field, mac's
        // focus-per-step behavior.
        if (eventArgs.PropertyName == nameof(TemplateFlowViewModel.Step))
        {
            _ = Dispatcher.InvokeAsync(
                FocusTemplateFlowStep, DispatcherPriority.Input);
        }
    }

    /// <summary>Focus per picker state (T3): the first row when rows
    /// exist, Try Again when empty/failed, Cancel while loading.</summary>
    private void FocusTemplatePicker()
    {
        TemplatePickerViewModel? picker = _viewModel.Workspace?.TemplatePickerSheet;
        if (picker is null)
        {
            return;
        }

        switch (picker.State)
        {
            case TemplatePickerState.Available:
                if (TemplatePickerList.Items.Count > 0)
                {
                    TemplatePickerList.SelectedIndex =
                        TemplatePickerList.SelectedIndex < 0
                            ? 0
                            : TemplatePickerList.SelectedIndex;
                    if (TemplatePickerList.ItemContainerGenerator
                        .ContainerFromIndex(TemplatePickerList.SelectedIndex)
                        is ListBoxItem item)
                    {
                        _ = item.Focus();
                        return;
                    }
                }

                _ = TemplatePickerList.Focus();
                return;
            case TemplatePickerState.Empty:
                _ = TemplatePickerTryAgainButton.Focus();
                return;
            case TemplatePickerState.Failed:
                _ = TemplatePickerFailedTryAgainButton.Focus();
                return;
            default:
                _ = TemplatePickerCancelButton.Focus();
                return;
        }
    }

    /// <summary>Focus per flow step (T4/T6): the first prompt field,
    /// or the name box with its seed selected.</summary>
    private void FocusTemplateFlowStep()
    {
        TemplateFlowViewModel? flow = _viewModel.Workspace?.TemplateFlowSheet;
        if (flow is null)
        {
            return;
        }

        if (flow.IsNameStep)
        {
            TemplateFlowNameTextBox.Focus();
            TemplateFlowNameTextBox.SelectAll();
            return;
        }

        if (FindFirstTextBox(TemplateFlowPromptsList) is TextBox first)
        {
            _ = first.Focus();
        }
    }

    private static TextBox? FindFirstTextBox(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBox box)
            {
                return box;
            }

            if (FindFirstTextBox(child) is TextBox nested)
            {
                return nested;
            }
        }

        return null;
    }

    private void RestoreFocusAfterTemplates()
    {
        IInputElement? focusBefore = _focusBeforeTemplates;
        _focusBeforeTemplates = null;
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                // Invariant 4: search topmost wins even against a
                // restore that would succeed — the shared rule every
                // covered restore site runs FIRST. Since SD-5 search
                // cannot be open beneath these sheets; this is the
                // backstop.
                if (TryFocusSearchIfTopmost())
                {
                    return;
                }

                // The supersession stand-down (the search restore's
                // shape): if another surface owns the moment by the
                // time this runs, it owns the focus story too.
                if (OpenModalSurface is not null)
                {
                    return;
                }

                // Focus follows content (T8): a successful create
                // opened the new note and the editor claimed focus in
                // that flow — restoring would steal it straight back
                // out of the note the user just created.
                if (FocusClaimedOutsideTemplates())
                {
                    return;
                }

                if (focusBefore is null || !TryFocus(focusBefore))
                {
                    FocusActiveEditorPane();
                }
            },
            DispatcherPriority.Input);
    }

    /// <summary>Whether something outside both template sheets has
    /// taken keyboard focus by the time the restore runs — the signal
    /// that the create placed focus on purpose (the SD-2 probe,
    /// adapted).</summary>
    private bool FocusClaimedOutsideTemplates()
    {
        if (Keyboard.FocusedElement is not DependencyObject focused)
        {
            return false;
        }

        if (ReferenceEquals(focused, this))
        {
            return false;
        }

        return !IsDescendantOfTemplateSheets(focused);
    }

    private bool IsDescendantOfTemplateSheets(DependencyObject? node)
    {
        for (; node is not null; node = FocusAncestry.Parent(node))
        {
            if (ReferenceEquals(node, TemplatePickerOverlay)
                || ReferenceEquals(node, TemplateFlowOverlay))
            {
                return true;
            }
        }

        return false;
    }

    private void TemplatePickerOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        TemplatePickerViewModel? picker = _viewModel.Workspace?.TemplatePickerSheet;
        if (picker is null || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                picker.CancelCommand.Execute(null);
                e.Handled = true;
                return;
            case Key.Enter:
                // One physical press, one step (codex round 4): a HELD
                // Enter's repeats would otherwise activate the row and
                // then cascade through the flow sheet's freshly focused
                // fields — Prompts, Name, Create — in a single gesture.
                if (e.IsRepeat)
                {
                    e.Handled = true;
                    return;
                }

                // A focused button — Try Again, Cancel — keeps its own
                // Enter (the search overlay's guard).
                if (Keyboard.FocusedElement is Button)
                {
                    return;
                }

                if (TemplatePickerList.SelectedItem
                    is TemplatePickerRowViewModel row)
                {
                    picker.ActivateCommand.Execute(row);
                    e.Handled = true;
                }

                return;
        }
    }

    private void TemplatePickerList_MouseDoubleClick(
        object sender, MouseButtonEventArgs e)
    {
        // Only a double-click on an actual ROW activates (the
        // palette's clicked-row contract; empty space below the last
        // row must not open whatever happened to be selected).
        if (_viewModel.Workspace?.TemplatePickerSheet
                is TemplatePickerViewModel picker
            && e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(TemplatePickerList, source) is not null
            && TemplatePickerList.SelectedItem is TemplatePickerRowViewModel row)
        {
            picker.ActivateCommand.Execute(row);
            e.Handled = true;
        }
    }

    /// <summary>
    /// The pointer half of the one-gesture fence (codex round 4): Next
    /// and Create occupy the same footer position in mutually
    /// exclusive step panels, so the SECOND click of a double-click on
    /// Next lands on the freshly revealed Create button — mac's
    /// separate sequential sheets can never overlap this way (TD-5's
    /// mechanics). A press whose ClickCount exceeds one is the tail of
    /// a multi-click gesture that already acted; swallow it.
    /// </summary>
    private void TemplateNameCreate_PreviewMouseLeftButtonDown(
        object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount > 1)
        {
            e.Handled = true;
        }
    }

    private void TemplateFlowOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        TemplateFlowViewModel? flow = _viewModel.Workspace?.TemplateFlowSheet;
        if (flow is null || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                flow.CancelCommand.Execute(null);
                e.Handled = true;
                return;
            case Key.Enter:
                // One physical press, one step (codex round 4): key
                // repeats from a held Enter land in the NEXT step's
                // freshly focused field and would collapse Next and
                // Create — separate confirmations — into one gesture.
                if (e.IsRepeat)
                {
                    e.Handled = true;
                    return;
                }

                // Buttons keep their own Enter; a text field's Enter is
                // the step's default action (mac .defaultAction — bare
                // Return activates the primary button from a field).
                if (Keyboard.FocusedElement is Button)
                {
                    return;
                }

                if (flow.IsPromptStep)
                {
                    flow.NextCommand.Execute(null);
                }
                else
                {
                    flow.CreateCommand.Execute(null);
                }

                e.Handled = true;
                return;
        }
    }
}
