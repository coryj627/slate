// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;

namespace SlateWindows;

/// <summary>
/// Plan 47 CB-35 (#1837): a search field's list laid out in the operation that publishes it,
/// as the palette's is (DS-10, <see cref="CommandPaletteResultsPresenter"/>). TSF can be
/// granted its lock between dispatcher operations, and its <c>GetTextExt</c> then runs
/// whatever layout and automation events it finds pending in the dispatcher, inside the lock;
/// a UIA event raised there waits on a client reading the field until UIA gives up, about 20
/// seconds a time (contract 28). A publication that settles as it ends leaves TSF nothing.
/// </summary>
internal static class PublishedList
{
    /// <summary>Lays out everything the dispatcher has pending, not only the list: the layout
    /// pass is the dispatcher's, and it is the dispatcher's whole queue that TSF would run.
    /// Guarded as <c>AccessibleDataGrid.ApplySort</c> guards its flush, by the anchor's being
    /// loaded, or by its window's: a list inside a sheet's scroll viewer that has never shown
    /// is not loaded until the pass that first lays the sheet out applies the viewer's
    /// template, and Move To publishes its first rows in the operation that shows its sheet.
    /// An anchor in no loaded window has nothing waiting on a pass.</summary>
    internal static void Settle(FrameworkElement anchor)
    {
        if (anchor.IsLoaded || Window.GetWindow(anchor) is { IsLoaded: true })
        {
            anchor.UpdateLayout();
        }
    }

    /// <summary>The palette's order (its <c>Refresh</c>, then its <c>SyncSelection</c>): the
    /// rows laid out, the row scrolled into view, and the scroll laid out, so no scroll is left
    /// deferred to the render pass. A list that does not show would defer the scroll to its
    /// Loaded pass, so it is only laid out.</summary>
    internal static void Show(ListBox list, object? row)
    {
        Settle(list);
        if (row is null || !list.IsVisible)
        {
            return;
        }

        list.ScrollIntoView(row);
        Settle(list);
    }

    /// <summary>A publication's end on the view's side: shown when the publication chose the
    /// list's selection, else laid out only, so a reader's own scroll is kept.</summary>
    internal static void Published(ListBox list, RowsPublishedEventArgs published, object? selected)
    {
        if (published.ChoseTheSelection)
        {
            Show(list, selected);
        }
        else
        {
            Settle(list);
        }
    }
}

/// <summary>
/// Plan 47 CB-35 (#1837): a search field's list published, raised as the last thing its
/// publication does, after its status writes. <see cref="ChoseTheSelection"/> says whether the
/// publication chose the row the list selects (the view shows it) or kept the reader's or none
/// (the view lays it out and scrolls nothing: a background page under a reader who scrolled
/// keeps the reader's view).
/// </summary>
internal sealed class RowsPublishedEventArgs : EventArgs
{
    internal static readonly RowsPublishedEventArgs SelectionKept = new(choseTheSelection: false);

    internal static readonly RowsPublishedEventArgs SelectionChosen = new(choseTheSelection: true);

    private RowsPublishedEventArgs(bool choseTheSelection) => ChoseTheSelection = choseTheSelection;

    internal bool ChoseTheSelection { get; }

    internal static RowsPublishedEventArgs Of(bool choseTheSelection) =>
        choseTheSelection ? SelectionChosen : SelectionKept;
}
