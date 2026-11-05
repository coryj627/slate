// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// DS-10 (#1811): the Settings sheet's shell wiring: the admission, focus on present, its
/// keys, and focus back. The Move To picker's split.
/// </summary>
public partial class MainWindow
{
    private IInputElement? _focusBeforeSettings;

    private bool TryClearTheWayForSettings()
    {
        switch (ModalSurfaces.DecideSettingsOpen(OpenModalSurface))
        {
            case PaletteOpenDecision.Open:
                return true;
            case PaletteOpenDecision.DismissQuickOpenThenOpen:
                _focusBeforeSettings ??= ConsumePreSwitcherFocus();
                _viewModel.QuickSwitcher!.Dismiss();
                return true;
            case PaletteOpenDecision.DismissSearchThenOpen:
                IInputElement? preSearch = ConsumePreSearchFocus();
                _viewModel.Search.Supersede();
                _focusBeforeSettings ??= preSearch;
                return true;
            case PaletteOpenDecision.DismissPaletteThenOpen:
                IInputElement? prePalette = ConsumePrePaletteFocus();
                _viewModel.Palette.Dismiss();
                _focusBeforeSettings ??= prePalette;
                return true;
            default:
                return false;
        }
    }

    private void SettingsSheet_Changed()
    {
        if (_viewModel.SettingsSheet is { } sheet)
        {
            _focusBeforeSettings ??= CapturePreSheetFocus();
            _ = Dispatcher.InvokeAsync(
                () =>
                {
                    if (ReferenceEquals(_viewModel.SettingsSheet, sheet))
                    {
                        _ = SelectorFocus.FocusFirstOrSelectedItem(SettingsCategoriesList) || SettingsCloseButton.Focus();
                    }
                },
                DispatcherPriority.Input);
        }
        else
        {
            RestoreFocusAfterSettings();
        }
    }

    private void RestoreFocusAfterSettings()
    {
        IInputElement? focusBefore = _focusBeforeSettings;
        _focusBeforeSettings = null;
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                // Invariant 4's backstop, then the supersession stand-down.
                if (TryFocusSearchIfTopmost() || OpenModalSurface is not null)
                {
                    return;
                }

                if (!LandToken(focusBefore))
                {
                    FocusActiveEditorPane();
                }
            },
            DispatcherPriority.Input);
    }

    /// <summary>Bubbling, not preview: a combo box with its list open takes Esc first, to
    /// close the list.</summary>
    private void SettingsOverlay_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            _viewModel.CloseSettings();
            e.Handled = true;
        }
    }

    private void SettingsClose_Click(object sender, RoutedEventArgs e) => _viewModel.CloseSettings();
}
