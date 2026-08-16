// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SlateWindows.Search;

namespace SlateWindows;

/// <summary>
/// W5-2 (#742) vault-search overlay shell wiring: focus, the keyboard
/// route, and the row/recent activation handlers. The view model owns
/// the pipeline, staleness, and announcements; this part owns only what
/// needs a live visual tree — the palette's split, mirrored.
/// </summary>
public partial class MainWindow
{
    private IInputElement? _focusBeforeSearch;

    /// <summary>
    /// Subscribes to the search overlay. Touching <c>Search</c>
    /// constructs it — deliberately at startup, on the UI thread, where
    /// the view model captures its <c>SynchronizationContext</c> (the
    /// palette's startup shape).
    /// </summary>
    private void ObserveSearch()
    {
        SearchOverlayViewModel search = _viewModel.Search;
        search.PropertyChanged += Search_PropertyChanged;
        search.Dismissed += Search_Dismissed;
        // W5-2 SD-4: reading-view tag activation opens the overlay from
        // the view-model side, and must respect the same modal decision
        // the Ctrl+Shift+F chord does — never open beneath a sheet.
        _viewModel.SearchOpenAdmission = TryClearTheWayForSearch;
    }

    private void Search_PropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(SearchOverlayViewModel.IsOpen)
            && _viewModel.Search.IsOpen)
        {
            // ??= so a stray re-open cannot overwrite the pre-search
            // focus with an element inside the overlay itself (the
            // palette's PD-2 guard, kept for the same reason).
            _focusBeforeSearch ??= Keyboard.FocusedElement;
            // Queued, not synchronous: the overlay is not realized at
            // the moment IsOpen flips, so a synchronous Focus() finds
            // nothing to focus (MainWindow.Palette.cs:43-57).
            _ = Dispatcher.InvokeAsync(
                () =>
                {
                    SearchOverlaySearchTextBox.Focus();
                    SearchOverlaySearchTextBox.SelectAll();
                },
                DispatcherPriority.Input);
        }
    }

    private void Search_Dismissed(object? sender, EventArgs e) => RestoreFocusAfterSearch();

    /// <summary>
    /// The shared topmost-search focus rule for every closing surface
    /// (codex rounds 2–3, #742). When search owns the keyboard, the
    /// pre-surface element is BEHIND the overlay by construction — the
    /// palette box that invoked the sheet is collapsed, and anything
    /// else is under the scrim — so restoring to it is wrong even when
    /// <c>Focus()</c> would succeed. Every sheet/overlay restore path
    /// calls this FIRST; a true return means the search box took focus
    /// and the caller's own restore logic must not run. Round 3 found
    /// the round-2 fix covering only one of the three independent
    /// restore implementations, which is why this is one shared helper
    /// rather than three copies of the rule.
    /// </summary>
    /// <summary>
    /// Hands the search overlay's captured pre-open focus to a caller
    /// that is closing search on its own initiative (the picker
    /// mutual-exclusion path), clearing it here so the ordinary
    /// dismissal restore does not race the caller for the same element.
    /// </summary>
    internal IInputElement? ConsumePreSearchFocus()
    {
        IInputElement? previous = _focusBeforeSearch;
        _focusBeforeSearch = null;
        return previous;
    }

    /// <summary>
    /// The palette half of the focus-lineage handoff (codex round 6,
    /// #742). A palette-invoked "Search Vault" opens search while P9
    /// has focus in the PALETTE's text box, so search captures that box
    /// as its return target — and it is collapsed by the time search
    /// closes, sending Escape's restore to the editor instead of the
    /// element the user came from. When the palette dismisses over a
    /// search whose captured token sits inside the palette overlay, the
    /// palette's own pre-open token replaces it. A search that predated
    /// the palette keeps its older, correct token.
    /// </summary>
    internal void AdoptPaletteFocusIntoSearch(
        IInputElement? paletteFocusBefore,
        Func<DependencyObject, bool> isInsidePaletteOverlay)
    {
        if (_viewModel.Search.IsOpen
            && _focusBeforeSearch is DependencyObject captured
            && isInsidePaletteOverlay(captured))
        {
            _focusBeforeSearch = paletteFocusBefore;
        }
    }

    internal bool TryFocusSearchIfTopmost()
    {
        if (!ModalSurfaces.SearchOwnsKeys(CurrentModalSurfaceState))
        {
            return false;
        }

        _ = SearchOverlaySearchTextBox.Focus();
        return true;
    }

    /// <summary>
    /// Applies <see cref="ModalSurfaces.DecideSearchOpen"/>, performing
    /// the dismissal it calls for — the search twin of
    /// <see cref="TryClearTheWayForThePalette"/>.
    /// </summary>
    private bool TryClearTheWayForSearch()
    {
        switch (ModalSurfaces.DecideSearchOpen(OpenModalSurface))
        {
            case PaletteOpenDecision.Open:
                return true;
            case PaletteOpenDecision.DismissQuickOpenThenOpen:
                // Codex round 5 (#742): the reverse half of the picker
                // handoff. Without adopting the pre-SWITCHER focus,
                // search captured the still-focused, about-to-collapse
                // Quick Open box as its return target, and Escape then
                // fell back to the editor instead of the element the
                // user actually came from. Symmetric with the
                // ConsumePreSearchFocus adoption in the switcher-open
                // observer.
                _focusBeforeSearch ??= _focusBeforeSwitcher;
                _focusBeforeSwitcher = null;
                _viewModel.QuickSwitcher!.Dismiss();
                return true;
            default:
                return false;
        }
    }

    private void SearchOverlaySearch_PreviewKeyDown(object sender, KeyEventArgs e) =>
        HandleSearchOverlayKey(e, Keyboard.Modifiers);

    /// <summary>
    /// The search overlay's keyboard route. Arrows move the result
    /// selection (divergence SD-1); Home/End/Page keys are deliberately
    /// NOT handled — mac's overlay has none and SD-1 covers arrows only,
    /// so those keys keep their TextBox caret meaning.
    /// </summary>
    private void HandleSearchOverlayKey(KeyEventArgs e, ModifierKeys modifiers)
    {
        SearchOverlayViewModel search = _viewModel.Search;
        if (!search.IsOpen)
        {
            return;
        }

        if (modifiers == ModifierKeys.None)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    search.Close();
                    e.Handled = true;
                    return;
                case Key.Enter:
                    // A focused button — a recent row, Clear recents, the
                    // tag chip's clear — keeps its own Enter (the Quick
                    // Open guard): marking it handled here would make
                    // every button in the overlay Enter-dead.
                    if (Keyboard.FocusedElement is Button)
                    {
                        return;
                    }

                    search.ActivateSelected();
                    e.Handled = true;
                    return;
                case Key.Down:
                    search.MoveSelection(1);
                    e.Handled = true;
                    return;
                case Key.Up:
                    search.MoveSelection(-1);
                    e.Handled = true;
                    return;
            }
        }
    }

    private void RestoreFocusAfterSearch()
    {
        IInputElement? focusBefore = _focusBeforeSearch;
        _focusBeforeSearch = null;
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                // Activation opens the hit and parks the caret, and the
                // editor claims focus in that same flow — so restoring
                // unconditionally would steal focus straight back out of
                // the note the user just opened (SD-2, the palette's
                // FocusClaimedOutsideThePalette probe adapted verbatim).
                if (FocusClaimedOutsideTheSearchOverlay())
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
    /// Whether something outside the search overlay has taken keyboard
    /// focus by the time the restore runs — the signal that activation
    /// placed focus on purpose (SD-2).
    /// </summary>
    private bool FocusClaimedOutsideTheSearchOverlay()
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
        // claimed it — this is the ordinary Escape path.
        return !IsDescendantOfSearchOverlay(focused);
    }

    private bool IsDescendantOfSearchOverlay(DependencyObject? node)
    {
        // Codex round 8 (#742): the round-7 walker fix missed this
        // CLONE — VisualTreeHelper.GetParent throws for a focused
        // reading-view Hyperlink, crashing the claimed-focus probe.
        for (; node is not null; node = FocusAncestry.Parent(node))
        {
            if (ReferenceEquals(node, SearchOverlay))
            {
                return true;
            }
        }

        return false;
    }

    private void SearchOverlayResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Selection lives on the view model (a two-way SelectedIndex
        // binding); the view only keeps the selected row visible.
        if (SearchOverlayResults.SelectedItem is object selected)
        {
            SearchOverlayResults.ScrollIntoView(selected);
        }
    }

    private void SearchOverlayResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Only a double-click on an actual ROW activates — a double-click
        // on empty space below the last row must not open whatever
        // happened to be selected (the palette's clicked-row contract).
        if (_viewModel.Search.IsOpen
            && e.OriginalSource is DependencyObject source
            && ItemContainerOf(source) is not null
            && SearchOverlayResults.SelectedItem is SearchResultRowViewModel row)
        {
            _viewModel.Search.ActivateRow(row);
            e.Handled = true;
        }
    }

    private void SearchOverlayRecentRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: string query })
        {
            // Re-run, then hand focus back to the field so the user can
            // refine (mac runRecentSearch + focus = .field).
            _viewModel.Search.ActivateRecent(query);
            SearchOverlaySearchTextBox.Focus();
        }
    }

    private void SearchOverlayRecentRow_GotKeyboardFocus(
        object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is Button { DataContext: string query })
        {
            _viewModel.Search.NotifyRecentRowFocused(query);
        }
    }

    private void SearchOverlayClearRecents_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Search.ClearRecents();
        // The recents section may collapse under the focused button;
        // the field is the surviving focus target (mac's clear button
        // sets focus = .field for the same reason).
        SearchOverlaySearchTextBox.Focus();
    }

    private void SearchOverlayClearTagScope_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Search.ClearScope();
        SearchOverlaySearchTextBox.Focus();
    }

    /// <summary>
    /// Pointer dismissal (contract S15): the "Close search" button runs
    /// the same <see cref="SearchOverlayViewModel.Close"/> Esc runs, so
    /// focus restore rides the ordinary <c>Dismissed</c> path.
    /// </summary>
    private void SearchOverlayClose_Click(object sender, RoutedEventArgs e) =>
        _viewModel.Search.Close();
}
