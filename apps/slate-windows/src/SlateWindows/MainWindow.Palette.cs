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
    /// state. The precedence walk itself lives in
    /// <see cref="ModalSurfaces.TopmostOpen"/> so it can be gated; this
    /// property only answers "is THIS one open".
    /// </remarks>
    internal ModalSurface? OpenModalSurface =>
        ModalSurfaces.TopmostOpen(CurrentModalSurfaceState);

    /// <summary>
    /// Reads the live view-model flags into the pure state record.
    /// </summary>
    /// <remarks>
    /// Deliberately a flat list of named assignments: each field sits
    /// next to the property it reads, so a wrong-property error is
    /// visible here rather than hidden in a switch. The ranking and the
    /// enum mapping are both pure and gated in <c>ModalSurfaceTests</c>.
    /// </remarks>
    private ModalSurfaceState CurrentModalSurfaceState
    {
        get
        {
            WorkspaceViewModel? workspace = _viewModel.Workspace;
            return new ModalSurfaceState(
                QuickOpen: _viewModel.QuickSwitcher?.IsOpen == true,
                SearchOverlay: _viewModel.Search.IsOpen,
                CommandPalette: _viewModel.Palette.IsOpen,
                AddProperty: workspace?.AddPropertySheet is not null,
                BulkRename: workspace?.BulkRenameSheet is not null,
                CitationDetails: workspace?.CitationDetails is not null,
                CitationSummary: workspace?.CitationSummary is not null,
                FilesCiting: workspace?.FilesCiting is not null,
                DashboardEditor: workspace?.DashboardEditorSheet is not null,
                BaseQueryBuilder: workspace?.BaseQueryBuilderSheet is not null,
                TemplatePicker: workspace?.TemplatePickerSheet is not null,
                TemplateFlow: workspace?.TemplateFlowSheet is not null,
                MoveTo: _viewModel.FileSidebar?.MoveToSheet is not null,
                CanvasCardEditor: workspace?.CanvasCardEditorSheet is not null,
                CanvasCardPicker: workspace?.CanvasCardPickerSheet is not null,
                CanvasPrompt: workspace?.CanvasPromptSheet is not null);
        }
    }

    /// <summary>
    /// Applies <see cref="ModalSurfaces.DecidePaletteOpen"/>, performing
    /// the dismissal it calls for.
    /// </summary>
    /// <summary>
    /// Hands the palette's captured pre-open focus to a caller that is
    /// closing it on its own initiative (a presenting sheet consuming
    /// lineage), clearing it here so the palette's own dismissal
    /// restore runs inert — the <c>ConsumePreSearchFocus</c> twin.
    /// </summary>
    internal IInputElement? ConsumePrePaletteFocus()
    {
        IInputElement? previous = _focusBeforePalette;
        _focusBeforePalette = null;
        return previous;
    }

    private bool TryClearTheWayForThePalette()
    {
        switch (ModalSurfaces.DecidePaletteOpen(OpenModalSurface))
        {
            case PaletteOpenDecision.Open:
                return true;
            case PaletteOpenDecision.DismissQuickOpenThenOpen:
                // Red team after codex round 11: this arm dismissed
                // WITHOUT adopting, so the palette's ??= capture then
                // grabbed the collapsing Quick Open box and Escape fell
                // back to the editor instead of the element the user
                // started from — the lineage every other supersession
                // already hands down (invariant 5).
                _focusBeforePalette = ConsumePreSwitcherFocus();
                _viewModel.QuickSwitcher!.Dismiss();
                return true;
            case PaletteOpenDecision.DismissSearchThenOpen:
                // SD-5: the palette supersedes an open search overlay the
                // way it supersedes Quick Open, with the same focus
                // lineage — the palette's return target becomes the
                // element from before SEARCH opened, so Escape lands the
                // user where they started, not on the collapsed search
                // box. Consuming first leaves search's own queued restore
                // inert (it stands down while the palette is open), and
                // Supersede rather than Close keeps the SCOPE alongside
                // the query, so Ctrl+Shift+F restores a tag-scoped
                // overlay as that tag's listing.
                IInputElement? preSearch = ConsumePreSearchFocus();
                _viewModel.Search.Supersede();
                _focusBeforePalette = preSearch;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Moves focus into the search box, synchronously.
    /// </summary>
    /// <remarks>
    /// P9 orders the focus step before the availability gate, and codex
    /// found that only the REQUEST was ordered: this queued the actual
    /// <c>Focus()</c> at <c>Input</c> priority, so on a double-click the
    /// <c>ListBoxItem</c> still owned focus while the disabled reason was
    /// computed and announced, and while the command itself ran. Doing it
    /// now makes the contract true rather than nearly true.
    ///
    /// The OPEN path above still queues, and must: the overlay is not
    /// realized at the moment <c>IsOpen</c> flips, so a synchronous
    /// <c>Focus()</c> there finds nothing to focus. This one runs while
    /// the palette is on screen.
    /// </remarks>
    private void Palette_SearchFocusRequested(object? sender, EventArgs e) =>
        CommandPaletteSearchTextBox.Focus();

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
                // Codex round 6 (#742): a palette-invoked "Search Vault"
                // captured the PALETTE box as search's return target
                // (P9 had focus there at invoke time); the palette's own
                // pre-open token is the true lineage and replaces it.
                // Same rule for a palette-invoked Quick Open (red team
                // after round 11 — the missing twin).
                AdoptPaletteFocusIntoSearch(
                    focusBefore, IsDescendantOfPaletteOverlay);
                AdoptPaletteFocusIntoQuickOpen(
                    focusBefore, IsDescendantOfPaletteOverlay);

                // Codex round 4 (#742): search topmost takes priority
                // here too. Since SD-5 the one reachable case is the
                // palette-invoked "Search Vault": the unguarded toggle
                // opens search beneath the still-open palette, P9
                // dismisses it in the same flow, and this hands focus
                // to the search box the user just asked for — not to
                // whatever sat beneath before the palette opened. Runs
                // FIRST: with a sheet or picker above, SearchOwnsKeys
                // is false and the ordinary logic runs.
                if (TryFocusSearchIfTopmost())
                {
                    return;
                }

                // The supersession stand-down (red team after round
                // 11): any other surface open at restore time owns the
                // moment — a palette-invoked Quick Open or sheet has
                // its own grab queued or landed, and restoring here
                // would flash-and-announce the editor mid-handoff.
                if (OpenModalSurface is not null)
                {
                    return;
                }

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
        // Codex round 7 (#742): VisualTreeHelper.GetParent THROWS for a
        // FrameworkContentElement — a focused reading-view Hyperlink is
        // one — so the walk must go through the logical parent until it
        // reaches a Visual. This walker predates W5-2 and the crash was
        // latent in the claimed-focus probe too.
        for (; node is not null; node = FocusAncestry.Parent(node))
        {
            if (ReferenceEquals(node, CommandPaletteOverlay))
            {
                return true;
            }
        }

        return false;
    }
}
