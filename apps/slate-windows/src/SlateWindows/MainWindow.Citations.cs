// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SlateWindows.Grids;
using SlateWindows.Panels;

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

    private void WireWorkspaceCitations(WorkspaceViewModel workspace)
    {
        workspace.PropertyChanged += Workspace_CitationSheetChanged;
        ObserveCitationPanels(workspace);
    }

    private void UnwireWorkspaceCitations(WorkspaceViewModel workspace)
    {
        workspace.PropertyChanged -= Workspace_CitationSheetChanged;
        ObserveCitationPanels(null);
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
    private void CitationRows_Published(object? sender, EventArgs e)
    {
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
            () => BibliographyEntriesGrid.FocusRow(
                row => string.Equals(
                    ((BibliographyRowViewModel)row).Key, key, StringComparison.Ordinal)),
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
                    _focusBeforeCitationSummary = Keyboard.FocusedElement;
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
        if (token is not IInputElement target)
        {
            return;
        }
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                if (target is UIElement { IsVisible: true } && target.Focus())
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
