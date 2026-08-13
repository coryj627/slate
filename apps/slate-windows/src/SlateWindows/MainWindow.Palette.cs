// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// W5-1 (#741) command-palette shell wiring: grouping, focus, and the
/// keyboard route. The view model owns navigation and gating; this part
/// owns only what needs a live visual tree.
/// </summary>
public partial class MainWindow
{
    private IInputElement? _focusBeforePalette;
    private bool _syncingPaletteSelection;

    /// <summary>
    /// Subscribes to the palette. Touching <c>Palette</c> constructs it,
    /// which registers the whole command catalog — deliberately at
    /// startup, so a duplicate id fails fast here rather than reaching a
    /// user (contract P2).
    /// </summary>
    private void ObservePalette()
    {
        CommandPaletteViewModel palette = _viewModel.Palette;
        palette.PropertyChanged += Palette_PropertyChanged;
        palette.SearchFocusRequested += Palette_SearchFocusRequested;
        RefreshPaletteGrouping();
    }

    private void Palette_PropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        CommandPaletteViewModel palette = _viewModel.Palette;
        switch (eventArgs.PropertyName)
        {
            case nameof(CommandPaletteViewModel.IsOpen):
                if (palette.IsOpen)
                {
                    // ??= so a re-open while open (PD-2 — the chord does not
                    // toggle) cannot overwrite the pre-palette focus with an
                    // element inside the palette itself.
                    _focusBeforePalette ??= Keyboard.FocusedElement;
                    _ = Dispatcher.InvokeAsync(
                        () =>
                        {
                            CommandPaletteSearchTextBox.Focus();
                            CommandPaletteSearchTextBox.SelectAll();
                        },
                        DispatcherPriority.Input);
                }
                else
                {
                    RestoreFocusAfterPalette();
                }

                break;

            case nameof(CommandPaletteViewModel.Rows):
                RefreshPaletteGrouping();
                break;

            case nameof(CommandPaletteViewModel.SelectedRow):
                SyncPaletteSelectionToList();
                break;
        }
    }

    private void Palette_SearchFocusRequested(object? sender, EventArgs e) =>
        _ = Dispatcher.InvokeAsync(
            () => CommandPaletteSearchTextBox.Focus(),
            DispatcherPriority.Input);

    /// <summary>
    /// Rebuilds the grouped view over the current rows.
    /// </summary>
    /// <remarks>
    /// Grouping is applied here rather than through a XAML
    /// <c>CollectionViewSource</c> because it keys on
    /// <c>SectionTitle</c> — the section core actually PLACED the row in.
    /// Grouping on the <c>CommandSection</c> enum would file a Recent row
    /// under the very section core excluded it from. A fresh view per
    /// publish also matches the view model's replace-wholesale rows, and
    /// the groups keep core's order because a view with no sort
    /// description creates groups in encounter order (contract P1).
    /// </remarks>
    private void RefreshPaletteGrouping()
    {
        var grouped = new CollectionViewSource { Source = _viewModel.Palette.Rows };
        grouped.GroupDescriptions.Add(
            new PropertyGroupDescription(nameof(CommandPaletteRowViewModel.SectionTitle)));
        CommandPaletteResultsList.ItemsSource = grouped.View;
        SyncPaletteSelectionToList();
    }

    /// <summary>
    /// Pushes the view model's selection into the list and scrolls it into
    /// view. The view model is the authority: a two-way
    /// <c>SelectedItem</c> binding would push null on every ItemsSource
    /// replacement and destroy the selection the view model just
    /// preserved across a query change (contract P7).
    /// </summary>
    private void SyncPaletteSelectionToList()
    {
        CommandPaletteRowViewModel? selected = _viewModel.Palette.SelectedRow;
        if (ReferenceEquals(CommandPaletteResultsList.SelectedItem, selected))
        {
            return;
        }

        _syncingPaletteSelection = true;
        try
        {
            CommandPaletteResultsList.SelectedItem = selected;
            if (selected is not null)
            {
                CommandPaletteResultsList.ScrollIntoView(selected);
            }
        }
        finally
        {
            _syncingPaletteSelection = false;
        }
    }

    private void CommandPaletteResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingPaletteSelection)
        {
            return;
        }

        if (CommandPaletteResultsList.SelectedItem is CommandPaletteRowViewModel row)
        {
            _viewModel.Palette.Select(row);
        }
    }

    private void CommandPaletteResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.Palette.IsOpen)
        {
            _viewModel.Palette.InvokeSelected();
        }
    }

    private void CommandPaletteSearch_PreviewKeyDown(object sender, KeyEventArgs e) =>
        HandleCommandPaletteKey(e, Keyboard.Modifiers);

    /// <summary>
    /// The palette's keyboard route. Modified arrows pass through
    /// untouched so screen-reader navigation keys reach the AT
    /// (contract P7).
    /// </summary>
    private void HandleCommandPaletteKey(KeyEventArgs e, ModifierKeys modifiers)
    {
        CommandPaletteViewModel palette = _viewModel.Palette;
        if (!palette.IsOpen)
        {
            return;
        }

        if (modifiers == ModifierKeys.None)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    palette.Dismiss();
                    e.Handled = true;
                    return;
                case Key.Enter:
                    palette.InvokeSelected();
                    e.Handled = true;
                    return;
                case Key.Down:
                    palette.MoveSelection(1);
                    e.Handled = true;
                    return;
                case Key.Up:
                    palette.MoveSelection(-1);
                    e.Handled = true;
                    return;

                // PD-1: Home / End / Page keys navigate on Windows, where
                // mac deliberately handles none of them. A recorded
                // platform-convention divergence, vetoable at PR.
                case Key.Home:
                    palette.SelectFirst();
                    e.Handled = true;
                    return;
                case Key.End:
                    palette.SelectLast();
                    e.Handled = true;
                    return;
                case Key.PageDown:
                    palette.MovePage(1);
                    e.Handled = true;
                    return;
                case Key.PageUp:
                    palette.MovePage(-1);
                    e.Handled = true;
                    return;
            }
        }
    }

    private void RestoreFocusAfterPalette()
    {
        IInputElement? focusBefore = _focusBeforePalette;
        _focusBeforePalette = null;
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                // An invoked command usually moves focus itself, and the
                // element that had focus before may no longer be alive, so
                // a failed restore falls back to the editor rather than
                // stranding focus on the window root.
                if (focusBefore is null || !TryFocus(focusBefore))
                {
                    FocusActiveEditorPane();
                }
            },
            DispatcherPriority.Input);
    }
}
