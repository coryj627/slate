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
            _observedBibliography.PropertyChanged -= Bibliography_PropertyChanged;
            _observedBibliography.Entries.CollectionChanged -= BibliographyEntries_Changed;
            _observedBibliography.Unresolved.CollectionChanged -= BibliographyUnresolved_Changed;
            _observedBibliography.KeyFocusRequested -= Bibliography_KeyFocusRequested;
            _observedBibliography = null;
        }
        if (_observedCitations is not null)
        {
            _observedCitations.Rows.CollectionChanged -= CitationRows_Changed;
            _observedCitations = null;
        }
        if (workspace is null)
        {
            return;
        }
        _observedBibliography = workspace.Bibliography;
        _observedBibliography.PropertyChanged += Bibliography_PropertyChanged;
        _observedBibliography.Entries.CollectionChanged += BibliographyEntries_Changed;
        _observedBibliography.Unresolved.CollectionChanged += BibliographyUnresolved_Changed;
        _observedBibliography.KeyFocusRequested += Bibliography_KeyFocusRequested;
        _observedCitations = workspace.Citations;
        _observedCitations.Rows.CollectionChanged += CitationRows_Changed;
        BindBibliographyEntriesGrid();
        BindBibliographyUnresolvedGrid();
    }

    private void CitationRows_Changed(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // The citations leaf is an ItemsSource-bound ListBox; nothing to
        // rebind. A row vanishing under an open details sheet would
        // leave the sheet describing a citation the note no longer has,
        // so the sheet closes with it.
        if (_observedWorkspace?.CitationDetails is not null
            && _observedCitations is { Rows.Count: 0 })
        {
            _observedWorkspace.CloseCitationDetailsCommand.Execute(null);
        }
    }

    private void Bibliography_PropertyChanged(
        object? sender, PropertyChangedEventArgs eventArgs)
    {
        switch (eventArgs.PropertyName)
        {
            case nameof(BibliographyViewModel.EntriesSummary):
                BindBibliographyEntriesGrid();
                break;
            case nameof(BibliographyViewModel.UnresolvedSummary):
                BindBibliographyUnresolvedGrid();
                break;
            default:
                break;
        }
    }

    private void BibliographyEntries_Changed(
        object? sender, NotifyCollectionChangedEventArgs eventArgs) =>
        BindBibliographyEntriesGrid();

    private void BibliographyUnresolved_Changed(
        object? sender, NotifyCollectionChangedEventArgs eventArgs) =>
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
            rowActions: BibliographyRowActions());
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
            ExpandSelectedCitation();
            e.Handled = true;
        }
    }

    /// <summary>A placeholder row has nothing to expand — core never
    /// looked one up (contract 2) — and the workspace seam refuses it,
    /// so no guard is duplicated here.</summary>
    private void ExpandSelectedCitation()
    {
        if (PanelCitationsList.SelectedItem is CitationRowViewModel row)
        {
            _observedWorkspace?.OpenCitationDetails(row, Keyboard.FocusedElement);
        }
    }

    /// <summary>Sheet focus choreography. Shares
    /// <c>_focusBeforeSheet</c> with the W4-4 property sheets: the
    /// <c>??=</c> capture means a sheet opened from inside another
    /// sheet's flow still returns focus to where the user actually
    /// started (contract 11).</summary>
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
                OpenOrCloseSheet(
                    workspace.CitationDetails is not null,
                    () => CitationDetailsCloseButton.Focus());
                break;
            case nameof(WorkspaceViewModel.CitationSummary):
                OpenOrCloseSheet(
                    workspace.CitationSummary is not null,
                    () => (workspace.CitationSummary?.CanWalkThrough == true
                        ? CitationSummaryWalkButton
                        : CitationSummaryDismissButton).Focus());
                break;
            case nameof(WorkspaceViewModel.FilesCiting):
                OpenOrCloseSheet(
                    workspace.FilesCiting is not null,
                    () => FilesCitingCloseButton.Focus());
                break;
            default:
                break;
        }
    }

    private void OpenOrCloseSheet(bool isOpen, Action focusInitial)
    {
        if (isOpen)
        {
            _focusBeforeSheet ??= Keyboard.FocusedElement;
            _ = Dispatcher.InvokeAsync(
                focusInitial, System.Windows.Threading.DispatcherPriority.Input);
        }
        else
        {
            RestoreFocusAfterSheet();
        }
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
