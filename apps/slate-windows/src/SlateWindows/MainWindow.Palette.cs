// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
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

    /// <summary>
    /// The topmost modal surface currently open, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Derived from the view-model flags that already exist — no new
    /// state. Enumerated in <see cref="ModalSurface"/> paint order, so the
    /// LAST open one is on top and therefore owns the keyboard. This
    /// exists because three review rounds produced blockers in the same
    /// branch, each because a guard was written for one overlay while
    /// eight others went unconsidered.
    /// </remarks>
    internal ModalSurface? OpenModalSurface
    {
        get
        {
            ModalSurface? top = null;
            if (_viewModel.QuickSwitcher?.IsOpen == true)
            {
                top = ModalSurface.QuickOpen;
            }

            if (_viewModel.Palette.IsOpen)
            {
                top = ModalSurface.CommandPalette;
            }

            if (_viewModel.Workspace is not { } workspace)
            {
                return top;
            }

            if (workspace.AddPropertySheet is not null)
            {
                top = ModalSurface.AddProperty;
            }

            if (workspace.BulkRenameSheet is not null)
            {
                top = ModalSurface.BulkRename;
            }

            if (workspace.CitationDetails is not null)
            {
                top = ModalSurface.CitationDetails;
            }

            if (workspace.CitationSummary is not null)
            {
                top = ModalSurface.CitationSummary;
            }

            if (workspace.FilesCiting is not null)
            {
                top = ModalSurface.FilesCiting;
            }

            if (workspace.DashboardEditorSheet is not null)
            {
                top = ModalSurface.DashboardEditor;
            }

            if (workspace.BaseQueryBuilderSheet is not null)
            {
                top = ModalSurface.BaseQueryBuilder;
            }

            return top;
        }
    }

    /// <summary>
    /// Whether the palette may open right now, dismissing what it
    /// supersedes.
    /// </summary>
    /// <remarks>
    /// It may not open underneath a sheet. Every sheet is declared AFTER
    /// the palette in <c>MainWindow.xaml</c> and carries its own scrim, so
    /// a palette opened beneath one is invisible and unreachable by
    /// pointer while still owning every keystroke — the round-3 blocker.
    /// Quick Open is the one surface the palette supersedes rather than
    /// defers to: it is declared before the palette, so the palette paints
    /// on top of it legibly.
    /// </remarks>
    private bool TryClearTheWayForThePalette()
    {
        switch (OpenModalSurface)
        {
            case null:
            case ModalSurface.CommandPalette:
                // PD-2: re-opening while open is allowed and clears the query.
                return true;
            case ModalSurface.QuickOpen:
                _viewModel.QuickSwitcher!.Dismiss();
                return true;
            default:
                return false;
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
        // Only a double-click on an actual ROW invokes. Without this, a
        // double-click on a section header or on the empty space below the
        // last row ran whatever happened to be selected.
        if (_viewModel.Palette.IsOpen
            && e.OriginalSource is DependencyObject source
            && ItemContainerOf(source) is not null)
        {
            _viewModel.Palette.InvokeSelected();
        }
    }

    private static ListBoxItem? ItemContainerOf(DependencyObject? node)
    {
        for (; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is ListBoxItem item)
            {
                return item;
            }
        }

        return null;
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
                // An invoked command often opens its own surface and
                // focuses it deliberately — the Add Property sheet's Key
                // field, Quick Open's search box. Those focus operations
                // are queued at the same priority and run BEFORE this one,
                // so restoring unconditionally steals focus straight back
                // out of the surface the user just asked for. Quick Open
                // solves the same problem with a commit flag; the live
                // focus is the more direct signal, and it also covers a
                // command that moved focus without opening anything.
                if (FocusClaimedOutsideThePalette())
                {
                    return;
                }

                // Otherwise the element that had focus before may no
                // longer be alive, so a failed restore falls back to the
                // editor rather than stranding focus on the window root.
                if (focusBefore is null || !TryFocus(focusBefore))
                {
                    FocusActiveEditorPane();
                }
            },
            DispatcherPriority.Input);
    }

    /// <summary>
    /// Whether something outside the palette has taken keyboard focus by
    /// the time the restore runs — the signal that an invoked command
    /// placed focus on purpose.
    /// </summary>
    private bool FocusClaimedOutsideThePalette()
    {
        if (Keyboard.FocusedElement is not DependencyObject focused)
        {
            return false;
        }

        // The window root is "focus nowhere", not a deliberate claim.
        if (ReferenceEquals(focused, this))
        {
            return false;
        }

        // Focus still inside the (now collapsed) overlay means nothing
        // claimed it — this is the ordinary Escape and success path.
        return !IsDescendantOfPaletteOverlay(focused);
    }

    private bool IsDescendantOfPaletteOverlay(DependencyObject? node)
    {
        for (; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, CommandPaletteOverlay))
            {
                return true;
            }
        }

        return false;
    }
}
