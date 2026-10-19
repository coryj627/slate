// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;

namespace SlateWindows;

/// <summary>
/// Plan 43 PR 20b (#1267): a rescan's publication — every watcher run's, every
/// Refresh's and every foreground rescan's — keeps every row it did not
/// change.
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
/// So a rescan's publication reconciles the root level in place. A root row
/// the fresh listing shows unchanged — the same path, name, kind, summary,
/// folder note and expansion, and, for an open folder, every row it shows
/// kept the same way — stays the same node, so its row, its container and its
/// UI Automation element stay. Rows that are new, changed or gone are
/// inserted, replaced or removed; nothing else moves. A kept collapsed folder
/// takes the fresh listing's state (<see cref="FileTreeNodeViewModel.AdoptChildStateOf"/>),
/// and an open folder that anything below changed is replaced whole, as
/// before. When no root row is kept, the publication replaces the tree as it
/// always did.
/// </para>
/// <para>
/// A publication that changes or removes the selected row — the reader's —
/// also replaces the tree as before. The restore that then lands the keys
/// (the selection's reconcile and the window's restore) finds them on a row
/// no window shows, which a replaced tree's rows are at once and a row
/// removed in place is not: on the Windows lane the keys fell to the first
/// row instead of the next survivor or the selected row's new one (the
/// scratch run of 2026-10-19).
/// </para>
/// </remarks>
internal sealed partial class FilesSidebarViewModel
{
    /// <summary>The tree identity of a node a reconcile let go of: no child
    /// load starts for it, as for a node of a replaced tree.</summary>
    private static readonly object ReleasedRow = new();

    /// <summary>Publish <paramref name="fresh"/> over the published root level,
    /// keeping every row it did not change (see the remarks).</summary>
    private void PublishKeepingUnchangedRows(ObservableCollection<FileTreeNodeViewModel> fresh)
    {
        var byPath = new Dictionary<string, FileTreeNodeViewModel>(StringComparer.Ordinal);
        foreach (FileTreeNodeViewModel row in RootNodes)
        {
            if (row is { IsPlaceholder: false, IsGroupHeader: false })
            {
                _ = byPath.TryAdd(row.Path, row);
            }
        }

        // Decided before anything moves: the replacing path must find the
        // published tree as it was.
        var desired = new List<FileTreeNodeViewModel>(fresh.Count);
        var kept = new Dictionary<FileTreeNodeViewModel, FileTreeNodeViewModel>(ReferenceEqualityComparer.Instance);
        foreach (FileTreeNodeViewModel candidate in fresh)
        {
            if (byPath.Remove(candidate.Path, out FileTreeNodeViewModel? row) && CanKeep(row, candidate))
            {
                kept[row] = candidate;
                desired.Add(row);
            }
            else
            {
                desired.Add(candidate);
            }
        }

        if (kept.Count == 0
            || (SelectedNode is { } selected
                && !kept.Keys.Any(row => Flatten([row]).Any(node => ReferenceEquals(node, selected)))))
        {
            RootNodes = fresh;
            return;
        }

        foreach ((FileTreeNodeViewModel row, FileTreeNodeViewModel candidate) in kept)
        {
            Keep(row, candidate);
        }

        for (int index = RootNodes.Count - 1; index >= 0; index--)
        {
            FileTreeNodeViewModel row = RootNodes[index];
            if (!kept.ContainsKey(row))
            {
                RootNodes.RemoveAt(index);
                row.AttachToTree(ReleasedRow);
            }
        }

        for (int index = 0; index < desired.Count; index++)
        {
            FileTreeNodeViewModel row = desired[index];
            if (index < RootNodes.Count && ReferenceEquals(RootNodes[index], row))
            {
                continue;
            }

            if (kept.ContainsKey(row))
            {
                // A kept row further down: the fresh listing ordered it
                // earlier (a pin, a sort key).
                RootNodes.Move(IndexOf(RootNodes, row, index + 1), index);
            }
            else
            {
                row.AttachToTree(RootNodes);
                RootNodes.Insert(index, row);
            }
        }
    }

    /// <summary>Whether the published <paramref name="row"/> is
    /// <paramref name="candidate"/>'s row unchanged: the same row fields and
    /// expansion and, for an open folder, both listings complete and every row
    /// they show kept in the same order.</summary>
    private static bool CanKeep(FileTreeNodeViewModel row, FileTreeNodeViewModel candidate)
    {
        if (row.IsPlaceholder
            || row.IsGroupHeader
            || candidate.IsPlaceholder
            || candidate.IsGroupHeader
            || !string.Equals(row.Path, candidate.Path, StringComparison.Ordinal)
            || !string.Equals(row.Name, candidate.Name, StringComparison.Ordinal)
            || row.IsDirectory != candidate.IsDirectory
            || row.Level != candidate.Level
            || row.HasFolderNote != candidate.HasFolderNote
            || row.IsExpanded != candidate.IsExpanded
            || !Equals(row.Summary, candidate.Summary))
        {
            return false;
        }

        if (!row.IsDirectory || !row.IsExpanded)
        {
            return true;
        }

        if (!row.HasLoadedChildren
            || !candidate.HasLoadedChildren
            || row.Children.Count != candidate.Children.Count)
        {
            return false;
        }

        for (int index = 0; index < row.Children.Count; index++)
        {
            if (!CanKeep(row.Children[index], candidate.Children[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Keep <paramref name="row"/> for <paramref name="candidate"/>
    /// (<see cref="CanKeep"/> held): every collapsed folder in it takes the
    /// fresh listing's state where that differs.</summary>
    private static void Keep(FileTreeNodeViewModel row, FileTreeNodeViewModel candidate)
    {
        if (!row.IsDirectory)
        {
            return;
        }

        if (!row.IsExpanded)
        {
            if (!HasTheSameUnopenedListing(row, candidate))
            {
                row.AdoptChildStateOf(candidate, ReleasedRow);
            }

            return;
        }

        for (int index = 0; index < row.Children.Count; index++)
        {
            Keep(row.Children[index], candidate.Children[index]);
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

    private static int IndexOf(IList<FileTreeNodeViewModel> rows, FileTreeNodeViewModel row, int start)
    {
        for (int index = start; index < rows.Count; index++)
        {
            if (ReferenceEquals(rows[index], row))
            {
                return index;
            }
        }

        throw new InvalidOperationException("A kept row is not in the published tree.");
    }
}
