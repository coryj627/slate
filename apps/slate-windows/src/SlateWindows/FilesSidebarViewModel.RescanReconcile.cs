// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;

namespace SlateWindows;

/// <summary>
/// Plan 43 PR 20b (#1267), #1515 and plan 44 WB-6 (#1522): every publication
/// of the tree — a rescan's (a watcher run's, a Refresh's, a foreground
/// rescan's) and an organic one's — keeps every row whose file, folder or
/// date group is still there, at every level the tree shows.
/// </summary>
/// <remarks>
/// <para>
/// A publication used to replace every node, so every row the reader or a
/// scan held let go of its container, and UI Automation then read a row the
/// tree no longer had: no rectangle, no place in its set, and its item's type
/// name for its name. The shell gate met both halves on 2026-10-18: axe-windows
/// read such a row mid-scan in the Templates journey, where the journey's own
/// template file started a watcher run under the open picker, and the shell
/// journey's held note row refused SetFocus. With the vault watcher a
/// publication can come at any moment — under a reader on a row, or under an
/// open dialog.
/// </para>
/// <para>
/// So a publication reconciles the tree in place, level by level. A
/// row the fresh listing still shows — the same path, name, kind, level and
/// expansion — stays the same node, so its row stays and so does its UI
/// Automation element, which the tree's peer, and an open folder's, key by
/// item. A row whose file changed outside Slate is updated in place: it takes
/// the fresh summary and folder-note state, with a change for each property
/// that reads them (<see cref="FileTreeNodeViewModel.TakeRowStateOf"/>). An
/// open folder's rows are reconciled the same way, recursively, so one note
/// changed inside an open folder no longer replaces the folder's subtree; a
/// kept collapsed folder takes the fresh listing's state
/// (<see cref="FileTreeNodeViewModel.AdoptChildStateOf"/>), since nothing
/// below it is shown. Rows that are new or gone are inserted or removed; a
/// placeholder (the loading, error and overflow rows) is never kept, and a
/// date group's header is kept like a folder's row (WB-6). A kept row moves
/// only when its place in the fresh
/// order changed — a title or a modified time it sorts by — and the rows
/// that stay are the longest run already in that order, through the selected
/// row, the tree's own selected row and the folders they are in: a row that
/// moves gets a new container, which the keys would not follow, so the
/// reader's row never moves.
/// </para>
/// <para>
/// The tree keeps a kept row's container through the publication's first
/// layout pass too: its recycling panel used to recycle the last rows it had
/// realized when a row was inserted above them, and an open folder recycled
/// that way gave every row under it a new element (#1515; see
/// <see cref="RealizedRowHold"/>).
/// </para>
/// <para>
/// Plan 44 WB-6 (#1522, #1515) made the rule total. Every publication keeps
/// the rows it did not change — an organic one (after Slate's own mutation)
/// too, whose pending selection and focus restores find kept and fresh rows by
/// path alike — and a date group's header is a kept row like a folder's,
/// keyed within its level by its label (<see cref="LevelKey"/>), its
/// expansion the published row's. Only a publication that keeps no root row
/// replaces the tree.
/// </para>
/// <para>
/// A selected row the reconcile cannot keep lets go of the selection, both
/// halves, before its container leaves (<see cref="DeselectBeforeRemoval"/>):
/// WPF's tree selects its first row when the selected row's container leaves
/// the tree in place (<c>TreeView.OnItemsChanged</c>), and a selection opens
/// its note — on the Windows lane in 20b's scratch run of 2026-10-19 the keys
/// fell to the first row. Where the selection and the keys land is the
/// publication's last step, after a mutation's named successor is seated: a
/// row of the same key the publication rebuilt (its date group changed, the
/// grouping changed) is re-selected silently, and only a row that is gone
/// hands the keys to the nearest survivor, read from the row's place before
/// anything moved (<c>ReconcileSelectionAfterPublication</c>).
/// </para>
/// </remarks>
internal sealed partial class FilesSidebarViewModel
{
    /// <summary>The tree identity of a node a reconcile let go of: no child
    /// load starts for it, as for a node of a replaced tree.</summary>
    private static readonly object ReleasedRow = new();

    /// <summary>Publish <paramref name="fresh"/> over the published tree,
    /// keeping every row it still shows (see the remarks).</summary>
    private void PublishKeepingUnchangedRows(ObservableCollection<FileTreeNodeViewModel> fresh)
    {
        // Decided before anything moves: the replacing path must find the
        // published tree as it was.
        var kept = new Dictionary<FileTreeNodeViewModel, FileTreeNodeViewModel>(ReferenceEqualityComparer.Instance);
        PlanLevel(RootNodes, fresh, kept);
        // WB-6 (S2): the keep path needs the sidebar's selection to be a tree
        // row, or nothing. While a list's row holds it (the filter results,
        // the dual pane), the publication replaces the tree as main did:
        // re-binding a list's selection onto kept rows made later
        // publications mistake it for a tree row (Codex round 3).
        if (kept.Count == 0 || (SelectedNode is { } selected && !InTree(selected)))
        {
            RootNodes = fresh;
            return;
        }

        // The selected row and the folders it is in anchor their levels:
        // read while the selection still names them.
        HashSet<FileTreeNodeViewModel> readerRows = ReaderRows();
        DeselectBeforeRemoval(kept);

        bool? selectedHadFolderNote = SelectedNode?.HasFolderNote;
        ReconcileLevel(RootNodes, fresh, kept, readerRows, RootNodes);
        if (SelectedNode?.HasFolderNote != selectedHadFolderNote)
        {
            // The folder-note verbs read the selected row's state.
            RaiseCommandStates();
        }
    }

    /// <summary>WB-6 (#1515 case 3): every selected row the reconcile could
    /// not keep lets go of the selection before its container leaves — the
    /// node's own half first, which the container's two-way binding follows,
    /// then the sidebar's — so WPF's tree has no selected container to
    /// replace with its first row. The tree's own selected row is not always
    /// the sidebar's selection (Codex round 1): a delete lets go of the
    /// sidebar's selection before its refresh, and the dual pane's selection
    /// is a row of its own listing, while the tree's container stays
    /// selected. Where the selection and the keys land is the publication's
    /// last step (<c>ReconcileSelectionAfterPublication</c>), after a
    /// mutation's named successor is seated.</summary>
    private void DeselectBeforeRemoval(Dictionary<FileTreeNodeViewModel, FileTreeNodeViewModel> kept)
    {
        foreach (FileTreeNodeViewModel row in Flatten(RootNodes).Where(row => row.IsSelected && !kept.ContainsKey(row)).ToList())
        {
            row.IsSelected = false;
        }

        if (SelectedNode is { } selected
            && !kept.ContainsKey(selected)
            && SetField(ref _selectedNode, null, nameof(SelectedNode)))
        {
            RaiseCommandStates();
        }
    }

    /// <summary>WB-6 (#1522): a row's key within its level — a file's or a
    /// folder's path, or a date group's label behind a NUL, which no path
    /// holds (every group's path is empty).</summary>
    internal static string LevelKey(FileTreeNodeViewModel row) =>
        row.IsGroupHeader ? GroupKeyPrefix + row.Name : row.Path;

    private const string GroupKeyPrefix = "\u0000group:";

    /// <summary>Find each row of the published level <paramref name="rows"/>
    /// that the fresh level <paramref name="fresh"/> still shows, and recurse
    /// into each kept open folder: <paramref name="kept"/> maps every kept row,
    /// at any level, to its fresh row.</summary>
    private static void PlanLevel(
        IEnumerable<FileTreeNodeViewModel> rows,
        IEnumerable<FileTreeNodeViewModel> fresh,
        Dictionary<FileTreeNodeViewModel, FileTreeNodeViewModel> kept)
    {
        var byPath = new Dictionary<string, FileTreeNodeViewModel>(StringComparer.Ordinal);
        foreach (FileTreeNodeViewModel row in rows)
        {
            if (IsKeyedRow(row))
            {
                _ = byPath.TryAdd(LevelKey(row), row);
            }
        }

        foreach (FileTreeNodeViewModel candidate in fresh)
        {
            if (IsKeyedRow(candidate)
                && byPath.Remove(LevelKey(candidate), out FileTreeNodeViewModel? row)
                && IsTheSameRow(row, candidate))
            {
                kept[row] = candidate;
                if (ListsItsRowsAgain(row))
                {
                    PlanLevel(row.Children, candidate.Children, kept);
                }
            }
        }
    }

    /// <summary>A row the reconcile keys (<see cref="LevelKey"/>): a file's,
    /// a folder's or a date group's header (WB-6), never a
    /// placeholder.</summary>
    private static bool IsKeyedRow(FileTreeNodeViewModel row) =>
        row is { IsPlaceholder: false };

    /// <summary>Whether the published <paramref name="row"/> is
    /// <paramref name="candidate"/>'s row: the same key, name, kind, level
    /// and expansion, and, open, a fresh folder that listed its rows. Its
    /// summary and folder note are taken in place. A date group's expansion
    /// is the published row's: a fresh group is always built open, and a
    /// group the reader collapsed stays collapsed (WB-6, #1522).</summary>
    private static bool IsTheSameRow(FileTreeNodeViewModel row, FileTreeNodeViewModel candidate) =>
        string.Equals(row.Name, candidate.Name, StringComparison.Ordinal)
        && row.IsDirectory == candidate.IsDirectory
        && row.Level == candidate.Level
        && (row.IsGroupHeader || row.IsExpanded == candidate.IsExpanded)
        && (!row.IsDirectory || !row.IsExpanded || candidate.ChildLoadState == FileTreeChildLoadState.Loaded);

    /// <summary>A kept open folder or date group whose rows the reconcile
    /// lists again: not a folder whose own load is still running, which
    /// publishes them itself.</summary>
    private static bool ListsItsRowsAgain(FileTreeNodeViewModel row) =>
        row is { IsExpanded: true }
        && (row.IsDirectory || row.IsGroupHeader)
        && row.ChildLoadState != FileTreeChildLoadState.Loading;

    /// <summary>Make the published level <paramref name="rows"/> the fresh
    /// level <paramref name="fresh"/>: every kept row stays, updated in place
    /// and, an open folder, reconciled in turn; the rest leave (released) or
    /// arrive (attached to <paramref name="tree"/>); and a kept row moves only
    /// when its place changed — never one of <paramref name="readerRows"/>.</summary>
    private static void ReconcileLevel(
        ObservableCollection<FileTreeNodeViewModel> rows,
        IReadOnlyList<FileTreeNodeViewModel> fresh,
        Dictionary<FileTreeNodeViewModel, FileTreeNodeViewModel> kept,
        HashSet<FileTreeNodeViewModel> readerRows,
        object tree)
    {
        var rowFor = new Dictionary<FileTreeNodeViewModel, FileTreeNodeViewModel>(ReferenceEqualityComparer.Instance);
        foreach (FileTreeNodeViewModel row in rows)
        {
            if (kept.TryGetValue(row, out FileTreeNodeViewModel? candidate))
            {
                rowFor[candidate] = row;
            }
        }

        // The fresh order, each kept row in its fresh row's place.
        List<FileTreeNodeViewModel> desired =
        [
            .. fresh.Select(candidate => rowFor.TryGetValue(candidate, out FileTreeNodeViewModel? row) ? row : candidate),
        ];
        foreach ((FileTreeNodeViewModel candidate, FileTreeNodeViewModel row) in rowFor)
        {
            row.TakeRowStateOf(candidate);
            if (!row.IsDirectory && !row.IsGroupHeader)
            {
                continue;
            }

            if (!row.IsExpanded)
            {
                if (!HasTheSameUnopenedListing(row, candidate))
                {
                    row.AdoptChildStateOf(candidate, ReleasedRow);
                }
            }
            else if (ListsItsRowsAgain(row))
            {
                ReconcileLevel(row.Children, candidate.Children, kept, readerRows, tree);
                row.TakeChildLoadStateOf(candidate);
            }
        }

        for (int index = rows.Count - 1; index >= 0; index--)
        {
            FileTreeNodeViewModel row = rows[index];
            if (!kept.ContainsKey(row))
            {
                rows.RemoveAt(index);
                row.AttachToTree(ReleasedRow);
            }
        }

        // A kept row whose place changed leaves here and is put back below:
        // either way WPF gives it a new container.
        HashSet<FileTreeNodeViewModel> staying = RowsThatStay(rows, desired, readerRows);
        for (int index = rows.Count - 1; index >= 0; index--)
        {
            if (!staying.Contains(rows[index]))
            {
                rows.RemoveAt(index);
            }
        }

        for (int index = 0; index < desired.Count; index++)
        {
            FileTreeNodeViewModel row = desired[index];
            if (index < rows.Count && ReferenceEquals(rows[index], row))
            {
                continue;
            }

            if (!kept.ContainsKey(row))
            {
                row.AttachToTree(tree);
            }

            rows.Insert(index, row);
        }
    }

    /// <summary>The kept rows of one level that need not move: the longest
    /// run of them already in the fresh order — through the reader's row, or
    /// the folder it is in, when that is one of them.</summary>
    /// <param name="rows">The level's kept rows, in their published order.</param>
    /// <param name="desired">The level in its fresh order.</param>
    /// <param name="readerRows">The selected row and every folder it is in.</param>
    private static HashSet<FileTreeNodeViewModel> RowsThatStay(
        IReadOnlyList<FileTreeNodeViewModel> rows,
        IReadOnlyList<FileTreeNodeViewModel> desired,
        HashSet<FileTreeNodeViewModel> readerRows)
    {
        var place = new Dictionary<FileTreeNodeViewModel, int>(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < desired.Count; index++)
        {
            place[desired[index]] = index;
        }

        int anchor = -1;
        for (int index = 0; index < rows.Count && anchor < 0; index++)
        {
            if (readerRows.Contains(rows[index]))
            {
                anchor = index;
            }
        }

        if (anchor < 0)
        {
            return LongestRisingRun(rows, place, int.MinValue, int.MaxValue);
        }

        int pivot = place[rows[anchor]];
        HashSet<FileTreeNodeViewModel> staying = LongestRisingRun(rows.Take(anchor), place, int.MinValue, pivot);
        staying.UnionWith(LongestRisingRun(rows.Skip(anchor + 1), place, pivot, int.MaxValue));
        _ = staying.Add(rows[anchor]);
        return staying;
    }

    /// <summary>The longest run of <paramref name="rows"/>, in their order,
    /// whose places rise — among the rows placed strictly between
    /// <paramref name="after"/> and <paramref name="before"/>.</summary>
    private static HashSet<FileTreeNodeViewModel> LongestRisingRun(
        IEnumerable<FileTreeNodeViewModel> rows,
        Dictionary<FileTreeNodeViewModel, int> place,
        int after,
        int before)
    {
        FileTreeNodeViewModel[] candidates = [.. rows.Where(row => place[row] > after && place[row] < before)];
        // ends[length - 1]: the candidate ending the run of that length whose
        // last place is lowest so far; previous[i]: the candidate before i in
        // its run.
        var ends = new List<int>();
        int[] previous = new int[candidates.Length];
        for (int index = 0; index < candidates.Length; index++)
        {
            int value = place[candidates[index]];
            int low = 0;
            int high = ends.Count;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (place[candidates[ends[middle]]] < value)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            previous[index] = low > 0 ? ends[low - 1] : -1;
            if (low == ends.Count)
            {
                ends.Add(index);
            }
            else
            {
                ends[low] = index;
            }
        }

        var run = new HashSet<FileTreeNodeViewModel>(ReferenceEqualityComparer.Instance);
        for (int index = ends.Count > 0 ? ends[^1] : -1; index >= 0; index = previous[index])
        {
            _ = run.Add(candidates[index]);
        }

        return run;
    }

    /// <summary>The selected row and every folder it is in, in the published
    /// tree; empty with nothing selected. The tree's own selected row counts
    /// too (WB-6, the red team's M1): when a list's row holds the sidebar's
    /// selection (the filter results, the dual pane), the tree still shows
    /// its own row selected, and a row that moves leaves its container —
    /// WPF would then select its first row, which opens its note.</summary>
    private HashSet<FileTreeNodeViewModel> ReaderRows()
    {
        var rows = new HashSet<FileTreeNodeViewModel>(ReferenceEqualityComparer.Instance);
        if (SelectedNode is { } selected)
        {
            _ = Collect(RootNodes, selected, rows);
        }

        foreach (FileTreeNodeViewModel row in Flatten(RootNodes).Where(row => row.IsSelected).ToList())
        {
            _ = Collect(RootNodes, row, rows);
        }

        return rows;

        static bool Collect(
            IEnumerable<FileTreeNodeViewModel> level,
            FileTreeNodeViewModel target,
            HashSet<FileTreeNodeViewModel> rows)
        {
            foreach (FileTreeNodeViewModel row in level)
            {
                if (ReferenceEquals(row, target) || Collect(row.Children, target, rows))
                {
                    _ = rows.Add(row);
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Two collapsed folders list alike when neither holds a real
    /// row: both still unloaded behind the same placeholder, or both loaded
    /// and empty.</summary>
    private static bool HasTheSameUnopenedListing(FileTreeNodeViewModel row, FileTreeNodeViewModel candidate) =>
        row.ChildLoadState == candidate.ChildLoadState
        && row.Children.Count == candidate.Children.Count
        && row.Children.All(child => child.IsPlaceholder)
        && candidate.Children.All(child => child.IsPlaceholder);
}
