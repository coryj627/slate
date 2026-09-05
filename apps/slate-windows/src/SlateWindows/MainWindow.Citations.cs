// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Grids;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W4-5 (#737): the window layer of the citation surfaces — the two
/// bibliography grids, the three overlay sheets and their focus
/// choreography, and the Ctrl+J landing.
///
/// Both bibliography segments ride the W4-1 substrate through
/// <see cref="AccessibleDataGrid.Bind"/> with a row-header column,
/// typed comparators and a row audio description (contract 8). No
/// grid behaviour is re-implemented here, and neither grid takes an
/// export producer (D-7 / G29).
/// </summary>
public partial class MainWindow
{
    private BibliographyViewModel? _observedBibliography;
    private CitationsPanelViewModel? _observedCitations;

    /// <summary>Entries. Title is the row header — the mac row label
    /// leads with it too — and every column sorts through a typed
    /// comparator rather than a member path (contract 8).</summary>
    private static readonly IReadOnlyList<AccessibleGridColumn> BibliographyEntryColumns =
    [
        new AccessibleGridColumn
        {
            Header = CitationPhrase.FieldTitle,
            Cell = row => ((BibliographyRowViewModel)row).TitleLine,
            Sort = Comparer<object>.Create((x, y) => string.CompareOrdinal(
                ((BibliographyRowViewModel)x).TitleLine,
                ((BibliographyRowViewModel)y).TitleLine)),
            // mac hints the row action on the entry itself; the string
            // was pinned as RowHelp and then bound nowhere.
            AccessibilityHint = row => ((BibliographyRowViewModel)row).RowHelp,
            IsRowHeader = true,
        },
        new AccessibleGridColumn
        {
            Header = CitationPhrase.FieldAuthors,
            Cell = row => ((BibliographyRowViewModel)row).Subtitle,
            Sort = Comparer<object>.Create((x, y) => string.CompareOrdinal(
                ((BibliographyRowViewModel)x).Subtitle,
                ((BibliographyRowViewModel)y).Subtitle)),
        },
        new AccessibleGridColumn
        {
            Header = CitationPhrase.FieldYear,
            // Sorts NUMERICALLY on the underlying year, not on the
            // rendered text — "1998" vs "998" orders wrong as a string,
            // and a missing year sorts last rather than first.
            Cell = row => ((BibliographyRowViewModel)row).YearText ?? "",
            Sort = Comparer<object>.Create((x, y) => Comparer<int>.Default.Compare(
                ((BibliographyRowViewModel)x).Entry.Year ?? int.MaxValue,
                ((BibliographyRowViewModel)y).Entry.Year ?? int.MaxValue)),
        },
        new AccessibleGridColumn
        {
            Header = CitationPhrase.FieldJournal,
            Cell = row => ((BibliographyRowViewModel)row).Journal,
            Sort = Comparer<object>.Create((x, y) => string.CompareOrdinal(
                ((BibliographyRowViewModel)x).Journal,
                ((BibliographyRowViewModel)y).Journal)),
        },
        new AccessibleGridColumn
        {
            Header = "Key",
            Cell = row => ((BibliographyRowViewModel)row).Key,
            Sort = Comparer<object>.Create((x, y) => string.CompareOrdinal(
                ((BibliographyRowViewModel)x).Key,
                ((BibliographyRowViewModel)y).Key)),
        },
    ];

    /// <summary>Unresolved. mac groups these into a list with a path
    /// header per group; on Windows the data is two-column tabular and
    /// rides the same grid (D-6), so the mac row label survives
    /// verbatim as the row audio description.</summary>
    private static readonly IReadOnlyList<AccessibleGridColumn> BibliographyUnresolvedColumns =
    [
        new AccessibleGridColumn
        {
            Header = "Key",
            Cell = row => ((UnresolvedRowViewModel)row).Key,
            Sort = Comparer<object>.Create((x, y) => string.CompareOrdinal(
                ((UnresolvedRowViewModel)x).Key,
                ((UnresolvedRowViewModel)y).Key)),
            IsRowHeader = true,
        },
        new AccessibleGridColumn
        {
            Header = "File",
            Cell = row => ((UnresolvedRowViewModel)row).Path,
            Sort = Comparer<object>.Create((x, y) => string.CompareOrdinal(
                ((UnresolvedRowViewModel)x).Path,
                ((UnresolvedRowViewModel)y).Path)),
        },
    ];

    /// <summary>W0.5-3 residue: dialog guidance for Ctrl+Shift+J refused
    /// beneath a higher sheet (#1118) — the template flow's
    /// dialog-busy partition, with this verb.</summary>
    internal const string CitationSummaryDialogBusyReason =
        "Finish or cancel the current dialog before opening the citation summary.";

    private void WireWorkspaceCitations(WorkspaceViewModel workspace)
    {
        // #1118: the summary openers — chord, menu, palette row — pass
        // one admission inside the workspace's open.
        workspace.CitationSummaryOpenAdmission = TryClearTheWayForCitationSummary;
        workspace.PropertyChanged += Workspace_CitationSheetChanged;
        ObserveCitationPanels(workspace);
    }

    private void UnwireWorkspaceCitations(WorkspaceViewModel workspace)
    {
        workspace.PropertyChanged -= Workspace_CitationSheetChanged;
        workspace.CitationSummaryOpenAdmission = null;
        ObserveCitationPanels(null);
    }

    /// <summary>
    /// Applies <see cref="ModalSurfaces.DecideCitationSummaryOpen"/>,
    /// performing the dismissal it calls for — the citation-summary
    /// twin of <see cref="TryClearTheWayForTemplates"/> (#1118). Runs
    /// INSIDE the workspace's open, so every opener passes one gate;
    /// the sheet observer captures its restore token at presentation
    /// (<c>_focusBeforeCitationSummary = CapturePreSheetFocus()</c>),
    /// and the dismissal arms seed that capture with the picker's own
    /// pre-open token so the end-of-sheet restore lands where the user
    /// started, not on a collapsed overlay box.
    /// </summary>
    private bool TryClearTheWayForCitationSummary()
    {
        switch (ModalSurfaces.DecideCitationSummaryOpen(OpenModalSurface))
        {
            case PaletteOpenDecision.Open:
                return true;
            case PaletteOpenDecision.DismissQuickOpenThenOpen:
                _focusBeforeCitationSummary ??= ConsumePreSwitcherFocus();
                _viewModel.QuickSwitcher!.Dismiss();
                return true;
            case PaletteOpenDecision.DismissSearchThenOpen:
                IInputElement? preSearch = ConsumePreSearchFocus();
                _viewModel.Search.Supersede();
                _focusBeforeCitationSummary ??= preSearch;
                return true;
            case PaletteOpenDecision.DismissPaletteThenOpen:
                IInputElement? prePalette = ConsumePrePaletteFocus();
                _viewModel.Palette.Dismiss();
                _focusBeforeCitationSummary ??= prePalette;
                return true;
            default:
                // A refusal must speak, never be a dead key (the
                // template flow's three-way red-team finding).
                _announcer.Post(new A11yEvent.HostComposed(
                    CitationSummaryDialogBusyReason, A11yPriority.Medium));
                return false;
        }
    }

    /// <summary>The leaves outlive individual notes, so these
    /// subscriptions are attached once per workspace rather than per
    /// selection.</summary>
    private void ObserveCitationPanels(WorkspaceViewModel? workspace)
    {
        if (_observedBibliography is not null)
        {
            _observedBibliography.EntriesPublished -= BibliographyEntries_Changed;
            _observedBibliography.UnresolvedPublished -= BibliographyUnresolved_Changed;
            _observedBibliography.KeyFocusRequested -= Bibliography_KeyFocusRequested;
            _observedBibliography = null;
            // Drop the closed vault's rows rather than leaving up to
            // MaxEntryRows of them alive behind the welcome screen.
            BibliographyEntriesGrid.Bind([], [], summary: "", accessibilityLabel: "");
            BibliographyUnresolvedGrid.Bind([], [], summary: "", accessibilityLabel: "");
        }
        if (_observedCitations is not null)
        {
            _observedCitations.RowsPublishing -= CitationRows_Publishing;
            _observedCitations.RowsPublished -= CitationRows_Published;
            _observedCitations = null;
        }
        if (workspace is null)
        {
            return;
        }
        _observedBibliography = workspace.Bibliography;
        // ONE signal per publish. Subscribing to CollectionChanged made
        // a publish of N rows rebind the grid N+2 times.
        _observedBibliography.EntriesPublished += BibliographyEntries_Changed;
        _observedBibliography.UnresolvedPublished += BibliographyUnresolved_Changed;
        _observedBibliography.KeyFocusRequested += Bibliography_KeyFocusRequested;
        _observedCitations = workspace.Citations;
        _observedCitations.RowsPublishing += CitationRows_Publishing;
        _observedCitations.RowsPublished += CitationRows_Published;
        BindBibliographyEntriesGrid();
        BindBibliographyUnresolvedGrid();
    }

    /// <summary>
    /// Close the details sheet only when the citation it describes is
    /// genuinely gone from the SETTLED row set.
    ///
    /// This ran on `CollectionChanged` and closed whenever the
    /// collection was empty. `Publish` clears and re-adds, so the Reset
    /// fires with Count == 0 on EVERY publish — mid-publish emptiness
    /// says nothing about whether the note still cites the key. That
    /// was harmless only while nothing re-published under an open
    /// sheet; wiring the save funnel into this leaf made every Ctrl+S,
    /// property write and task toggle slam the sheet shut mid-read.
    ///
    /// Asking after the publish also fixes the converse the old shape
    /// could not see: rows republishing NON-empty while that particular
    /// citation disappeared left the sheet describing a citation the
    /// note no longer contains.
    /// </summary>
    /// <summary>
    /// The row the reader was on, remembered across a republish.
    ///
    /// `Publish` clears and re-adds, which destroys every container and
    /// drops SelectedItem — so any save while the user was sitting on a
    /// citation row silently lost their place, with no announcement.
    /// Keyed on the citation KEY rather than the row object, because
    /// every publish builds fresh row view models. Same shape as the
    /// grid substrate's row-header restore.
    /// </summary>
    private string? _selectedCitationKey;

    private void PanelCitations_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Remember a SELECTION, never a clearing (#1098, measured by the
        // shell gate): the publish's Rows.Clear() raises SelectionChanged
        // with a null SelectedItem, and nulling the key here erased the
        // reading position BEFORE RestoreCitationSelection ran — the W4-5
        // round-4 restore never fired for a real publish. Single-select
        // has no user deselection; a note without the key simply finds
        // no row to restore.
        if (PanelCitationsList.SelectedItem is CitationRowViewModel row)
        {
            _selectedCitationKey = row.Reference.Citations.FirstOrDefault()?.Key;
        }
    }

    private void RestoreCitationSelection()
    {
        if (_selectedCitationKey is not { Length: > 0 } key
            || PanelCitationsList.SelectedItem is not null
            || _observedCitations is not { } citations)
        {
            return;
        }
        foreach (CitationRowViewModel row in citations.Rows)
        {
            if (row.Reference.Citations.Any(
                cited => string.Equals(cited.Key, key, StringComparison.Ordinal)))
            {
                PanelCitationsList.SelectedItem = row;
                return;
            }
        }
    }

    /// <summary>Whether the citations list owned keyboard focus when the
    /// last publish began (#1098). Sampled BEFORE the rebuild: the
    /// publish clears the rows, which destroys the focused container,
    /// and WPF ejects keyboard focus to the window root when a focused
    /// item unloads (the W5-4 tree finding) — so after the rebuild the
    /// list reads as not-focused whether or not the user was on it.</summary>
    private bool _citationsListOwnedFocusBeforePublish;

    private void CitationRows_Publishing(object? sender, EventArgs e)
    {
        // Sample BOTH halves of the reading position while the rows are
        // alive: the key the selection restore re-seats by (belt and
        // braces with the selection handler above), and whether the list
        // owned keyboard focus.
        if (PanelCitationsList.SelectedItem is CitationRowViewModel row)
        {
            _selectedCitationKey = row.Reference.Citations.FirstOrDefault()?.Key;
        }

        _citationsListOwnedFocusBeforePublish = PanelCitationsList.IsKeyboardFocusWithin;
    }

    /// <summary>
    /// The focus half of the republish restore (#1098): the selection
    /// restore above keeps the reading position in the MODEL, but the
    /// user's keyboard focus was ejected with the old container, so
    /// they would have to Tab back to resume. When the list owned focus
    /// before the publish, put it back on the restored row's container
    /// (the list itself if the container has not generated yet) —
    /// guarded so it never steals: a modal surface owns the moment, and
    /// a real focus claim elsewhere (the editor after a save's own
    /// landing) wins; only window-root/null focus is the stranded state
    /// this repairs.
    /// </summary>
    private void RestoreCitationFocus()
    {
        if (!_citationsListOwnedFocusBeforePublish)
        {
            return;
        }

        _citationsListOwnedFocusBeforePublish = false;
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                // The shared topmost-search rule first (invariant 4 of
                // the search contracts; the Restore*Focus* census): a
                // republish can land while the search overlay is up —
                // an external change, the watcher — and focusing the
                // list behind its scrim would strand text focus.
                if (TryFocusSearchIfTopmost())
                {
                    return;
                }

                if (OpenModalSurface is not null)
                {
                    return;
                }

                if (Keyboard.FocusedElement is DependencyObject focused
                    && !ReferenceEquals(focused, this)
                    && !PanelCitationsList.IsKeyboardFocusWithin)
                {
                    return;
                }

                if (PanelCitationsList.SelectedItem is not { } selected)
                {
                    _ = PanelCitationsList.Focus();
                    return;
                }

                PanelCitationsList.ScrollIntoView(selected);
                PanelCitationsList.UpdateLayout();
                if (PanelCitationsList.ItemContainerGenerator.ContainerFromItem(selected)
                    is ListBoxItem container)
                {
                    _ = container.Focus();
                    return;
                }

                // The row's container has not generated yet — the
                // virtualizing panel materializes it on the next layout
                // pass (measured: at Input priority the generator still
                // answers null and focus landed on the list itself). Hold
                // focus on the list so it is never stranded, then seat it
                // on the row once the container exists; the second step
                // stands down if focus has moved on meanwhile.
                _ = PanelCitationsList.Focus();
                _ = Dispatcher.InvokeAsync(
                    () =>
                    {
                        if (PanelCitationsList.IsKeyboardFocusWithin
                            && ReferenceEquals(PanelCitationsList.SelectedItem, selected)
                            && PanelCitationsList.ItemContainerGenerator
                                .ContainerFromItem(selected) is ListBoxItem late)
                        {
                            _ = late.Focus();
                        }
                    },
                    System.Windows.Threading.DispatcherPriority.Background);
            },
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void CitationRows_Published(object? sender, EventArgs e)
    {
        RestoreCitationSelection();
        RestoreCitationFocus();
        if (_observedWorkspace?.CitationDetails is not { } details
            || _observedCitations is not { } citations)
        {
            return;
        }
        if (details.EntryKey.Length > 0 && citations.ContainsKey(details.EntryKey))
        {
            return;
        }
        if (citations.Rows.Count > 0 && details.EntryKey.Length == 0)
        {
            // No key to match on (a placeholder expansion): only an
            // empty set is evidence the citation is gone.
            return;
        }
        _observedWorkspace.CloseCitationDetailsCommand.Execute(null);
    }

    private void BibliographyEntries_Changed(object? sender, EventArgs eventArgs) =>
        BindBibliographyEntriesGrid();

    private void BibliographyUnresolved_Changed(object? sender, EventArgs eventArgs) =>
        BindBibliographyUnresolvedGrid();

    /// <summary>Row actions mirror mac's context menu. Insert-citation
    /// stays ENABLED even though it cannot be built — the announcement
    /// IS the product answer, and a greyed-out item would make the
    /// reason undiscoverable.</summary>
    private IReadOnlyList<AccessibleGridRowAction> BibliographyRowActions() =>
    [
        new AccessibleGridRowAction
        {
            Name = CitationPhrase.ShowFilesCitingAction,
            Execute = row => _observedWorkspace?.OpenFilesCiting(
                ((BibliographyRowViewModel)row).Key,
                Keyboard.FocusedElement),
        },
        new AccessibleGridRowAction
        {
            Name = CitationPhrase.InsertCitationAction,
            Execute = _ => _observedWorkspace?.AnnounceInsertCitationUnavailable(),
        },
    ];

    private void BindBibliographyEntriesGrid()
    {
        if (_observedBibliography is not { } bibliography)
        {
            return;
        }
        BibliographyEntriesGrid.Bind(
            BibliographyEntryColumns,
            [.. bibliography.Entries],
            summary: bibliography.EntriesSummary,
            accessibilityLabel: CitationPhrase.BibliographyHeading,
            rowAudioDescription: row => ((BibliographyRowViewModel)row).RowDescription,
            rowActions: BibliographyRowActions(),
            // Enter expands the entry, as mac's entry Button does. The
            // details overlay was reachable only from the citations
            // leaf, so half of contract 10's surface had no entrance
            // and the row's own "Activate to expand citation fields."
            // hint promised something that could not happen.
            rowActivated: row => _observedWorkspace?.OpenEntryDetails(
                ((BibliographyRowViewModel)row).Entry, Keyboard.FocusedElement));
    }

    private void BindBibliographyUnresolvedGrid()
    {
        if (_observedBibliography is not { } bibliography)
        {
            return;
        }
        BibliographyUnresolvedGrid.Bind(
            BibliographyUnresolvedColumns,
            [.. bibliography.Unresolved],
            summary: bibliography.UnresolvedSummary,
            accessibilityLabel: CitationPhrase.SegmentUnresolved,
            rowAudioDescription: row => ((UnresolvedRowViewModel)row).RowDescription);
    }

    /// <summary>Ctrl+J landed. The leaf has already decided the outcome
    /// and announced it; this only moves focus, and only if the entry
    /// is really in the bound set — a miss leaves focus alone rather
    /// than dumping the user at row one.</summary>
    private void Bibliography_KeyFocusRequested(object? sender, EventArgs eventArgs)
    {
        if (_observedBibliography?.ConsumeKeyFocusRequest() is not { } key)
        {
            return;
        }
        // Deferred past layout: the grid may have been collapsed until
        // the jump revealed the leaf a moment ago.
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                if (BibliographyEntriesGrid.FocusRow(
                    row => string.Equals(
                        ((BibliographyRowViewModel)row).Key, key, StringComparison.Ordinal)))
                {
                    return;
                }
                // A MISS still has to land somewhere. The jump has
                // already closed the sheet that held focus and filtered
                // the leaf to this key, so the search box — which now
                // contains it — is where the announcement leaves the
                // user. Without this, focus fell to the window root.
                _ = BibliographySearchBox.Focus();
            },
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void BibliographyEntriesSegment_Checked(object sender, RoutedEventArgs e)
    {
        if (_observedBibliography is { } bibliography)
        {
            bibliography.Segment = BibliographySegment.Entries;
        }
    }

    private void BibliographyUnresolvedSegment_Checked(object sender, RoutedEventArgs e)
    {
        if (_observedBibliography is { } bibliography)
        {
            bibliography.Segment = BibliographySegment.Unresolved;
        }
    }

    private void PanelCitations_MouseDoubleClick(object sender, MouseButtonEventArgs e) =>
        ExpandSelectedCitation();

    private void PanelCitations_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            // Only consume the key if something actually opened. It was
            // marked handled unconditionally, so on a placeholder row
            // the keystroke vanished with no sheet and no sound.
            e.Handled = ExpandSelectedCitation();
        }
    }

    /// <summary>A placeholder row has nothing to expand — core never
    /// looked one up (contract 2). Returns whether a sheet opened.
    /// </summary>
    private bool ExpandSelectedCitation()
    {
        if (PanelCitationsList.SelectedItem is not CitationRowViewModel { CanExpand: true } row)
        {
            return false;
        }
        _observedWorkspace?.OpenCitationDetails(row, Keyboard.FocusedElement);
        return true;
    }

    /// <summary>Sheet focus choreography. Shares
    /// <c>_focusBeforeSheet</c> with the W4-4 property sheets: the
    /// <c>??=</c> capture means a sheet opened from inside another
    /// sheet's flow still returns focus to where the user actually
    /// started (contract 11).</summary>
    private CitationDetailsViewModel? _openCitationDetails;
    private CitationSummaryViewModel? _openCitationSummary;
    private FilesCitingViewModel? _openFilesCiting;
    private IInputElement? _focusBeforeCitationSummary;

    private void Workspace_CitationSheetChanged(
        object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (sender is not WorkspaceViewModel workspace)
        {
            return;
        }
        switch (eventArgs.PropertyName)
        {
            case nameof(WorkspaceViewModel.CitationDetails):
                if (workspace.CitationDetails is { } details)
                {
                    _openCitationDetails = details;
                    FocusWhenReady(() => CitationDetailsCloseButton.Focus());
                }
                else
                {
                    RestoreFocusTo(_openCitationDetails?.ReturnFocusToken);
                    _openCitationDetails = null;
                }
                break;
            case nameof(WorkspaceViewModel.CitationSummary):
                if (workspace.CitationSummary is { } summary)
                {
                    _openCitationSummary = summary;
                    // The summary sheet has no row identity of its own
                    // — it is opened by a chord from wherever focus
                    // happens to be — so it captures at open time.
                    // Through the shared helper: a summary parked on
                    // RowsPublished presents while a picker may still
                    // be open and focused (red team after round 11),
                    // and the picker's pre-open token is the true
                    // lineage, not its about-to-collapse box.
                    _focusBeforeCitationSummary = CapturePreSheetFocus();
                    FocusWhenReady(() => (summary.CanWalkThrough
                        ? CitationSummaryWalkButton
                        : CitationSummaryDismissButton).Focus());
                }
                else
                {
                    RestoreFocusTo(_focusBeforeCitationSummary);
                    _focusBeforeCitationSummary = null;
                    _openCitationSummary = null;
                }
                break;
            case nameof(WorkspaceViewModel.FilesCiting):
                if (workspace.FilesCiting is { } filesCiting)
                {
                    _openFilesCiting = filesCiting;
                    FocusWhenReady(() => FilesCitingCloseButton.Focus());
                }
                else
                {
                    RestoreFocusTo(_openFilesCiting?.ReturnFocusToken);
                    _openFilesCiting = null;
                }
                break;
            default:
                break;
        }
    }

    private void FocusWhenReady(Action focusInitial) =>
        _ = Dispatcher.InvokeAsync(
            focusInitial, System.Windows.Threading.DispatcherPriority.Input);

    /// <summary>
    /// Contract 11, PER SHEET. The W4-4 sheets share one
    /// <c>_focusBeforeSheet</c> slot with a <c>??=</c> capture, which
    /// works only while exactly one sheet is ever open. Citation sheets
    /// stack — Ctrl+Shift+J is enabled over an open details sheet — and
    /// with a shared slot, closing the inner one restored focus to a
    /// row BEHIND the still-open outer sheet and then nulled the slot,
    /// so the outer sheet had nothing left to restore. Each citation
    /// sheet now carries its own return target.
    ///
    /// A container that has been re-generated since the sheet opened
    /// (the rows republish on every save) cannot take focus; falling
    /// back to the list keeps the user inside the panel they came from
    /// instead of stranding them on the window root.
    /// </summary>
    private void RestoreFocusTo(object? token)
    {
        // The queue is unconditional (red team after round 11): the
        // old null-token early return skipped the WHOLE restore,
        // including the invariant-4 backstop, dropping focus to the
        // window root — the Bases twin already queued unconditionally.
        IInputElement? target = token as IInputElement;
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                // Codex round 3 (#742): search topmost takes priority —
                // under the original stacking design this sheet was
                // palette-invokable over a still-open search overlay,
                // with both the captured target and the list fallback
                // behind it. SD-5 made that state unreachable; the rule
                // stays as invariant 4's backstop.
                if (TryFocusSearchIfTopmost())
                {
                    return;
                }

                if (target is not null && TryFocus(target))
                {
                    return;
                }
                _ = PanelCitationsList.Focus();
            },
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void CitationDetailsOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _observedWorkspace?.CloseCitationDetailsCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void CitationSummaryOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _observedWorkspace?.CloseCitationSummaryCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FilesCitingOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _observedWorkspace?.CloseFilesCitingCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>The walk-through closes the sheet and then starts —
    /// core owns the announcement, and it is announced exactly once
    /// (contract 4).</summary>
    private void CitationSummaryWalk_Click(object sender, RoutedEventArgs e) =>
        _observedWorkspace?.CitationSummary?.WalkThrough();
}
