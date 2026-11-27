// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.FileManagement;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>W6-2 PR A (#746), contracts A-8 and AD-11: the sidebar as
/// every surface's note creator (the canvas's seam generalised with typed
/// outcomes) and as the target of "Reveal in File Tree" — select the
/// note in the tree, the mac's meaning, distinct from the sidebar's own
/// "Reveal in File Explorer" (FD-5).</summary>
/// <remarks>
/// <para>
/// Plan 47 CB-39 (#1670): one ordering for every intent the tree hears. An
/// intent is a gesture that names what the tree selects or what the workspace
/// shows: a Reveal, a mutation's named row (and a create's rename arm), a
/// gesture that names no row (a create that lands with a caveat, New Canvas,
/// a surface's landed note, a template create, a delete's restore, a trash,
/// the sidebar's own explicit opens) and the reader's own selection. Each
/// takes the next number at its gesture, and one slot holds the pending one:
/// an intent installs only while its number is the newest ever taken, so an
/// older intent never seats after a newer one.
/// </para>
/// <para>
/// A Reveal whose row is not listed waits for the loads it starts: each
/// folder publication, and every whole-tree one, takes one step down its path
/// (<see cref="AdvanceWaitingReveal"/>), seating the row once it is listed at
/// any depth, under a date group too, or expanding the next folder. The wait
/// ends unseated at a newer intent, at a collapse of a folder on its path, at
/// a load on its path that ends without listing (a failure, or a cancel by
/// code that does not list the folder itself), or at a listed level without
/// the next segment. A seat lands the keys on the row first and then selects
/// it, so the open's tab switch moves nothing.
/// </para>
/// </remarks>
internal sealed partial class FilesSidebarViewModel : ISurfaceNoteCreator
{
    /// <summary>The waiting Reveal's path, or null.</summary>
    private string? _pendingSurfaceSelectPath;

    /// <summary>Plan 47 CB-39: the newest intent number ever taken. An intent
    /// installs only while its own number is this one (the watermark).</summary>
    private long _intentsTaken;

    /// <summary>Plan 47 CB-39: set while the sidebar seats a row of its own
    /// through the setter (a Reveal, a create's rename arm), which is not the
    /// reader's selection, and while a Reveal's seat clears the filter, which
    /// is not the reader's filter change.</summary>
    private bool _seatingOwnSelection;

    /// <summary>Plan 47 CB-39: set while <see cref="AdvanceWaitingReveal"/>
    /// runs. A step asked from inside it (a Reveal asked from a seat's own
    /// handlers, as the window's restore or the open runs) asks for one more
    /// pass instead of running inside the first.</summary>
    private bool _advancingReveal;
    private bool _advanceAgain;

    /// <summary>Plan 47 CB-39: whether the tree has published once; before
    /// that the root is empty, and a Reveal cannot be found absent.</summary>
    private bool _treePublishedOnce;

    private enum IntentKind
    {
        Reveal,
        NamedRow,
        RenameArm,
        NoRow,
    }

    /// <summary>The next intent number, taken at a gesture.</summary>
    private long TakeIntent() => ++_intentsTaken;

    /// <summary>Installs an intent in the one slot when
    /// <paramref name="number"/> is still the newest ever taken: the pending
    /// Reveal, named row and rename arm all give way, and the intent's own
    /// kind takes the slot. An intent whose gesture is older than a newer one
    /// installs nothing.</summary>
    private bool InstallIntent(IntentKind kind, string? path, long number)
    {
        if (number != _intentsTaken)
        {
            return false;
        }

        _pendingSurfaceSelectPath = kind == IntentKind.Reveal ? path : null;
        _pendingSelectPath = kind == IntentKind.NamedRow ? path : null;
        _pendingRenameArmPath = kind == IntentKind.RenameArm ? path : null;
        return true;
    }

    /// <summary>A gesture that names no row, at the gesture: it drops a
    /// waiting Reveal and a pending row. Answers its number, for a mutation
    /// that awaits before its request.</summary>
    private long StateIntentNamingNoRow()
    {
        long number = TakeIntent();
        _ = InstallIntent(IntentKind.NoRow, null, number);
        return number;
    }

    // Explicit: the canvas seam's TryCreateNote shares the parameter list
    // and answers with its own record; both stay one implementation.
    NoteCreateResult ISurfaceNoteCreator.TryCreateNote(string path, string content)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            if (!TryRunSessionWork(
                () => CreateOutcomes.CreateReporting(
                    _session, path, content, System.IO.Path.GetFileName(path)),
                out string? caveat))
            {
                return new NoteCreateResult.Unavailable();
            }
            return new NoteCreateResult.Landed(caveat);
        }
        catch (VaultException.DestinationExists exception)
        {
            return new NoteCreateResult.Exists(FailureDetail.Of(exception));
        }
        catch (VaultException exception)
        {
            return new NoteCreateResult.Failed(FailureDetail.Of(exception));
        }
    }

    public void NoteLanded(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        StructuralHistoryBarrier();
        // Plan 47 CB-39: a gesture that names no row; the number is taken at
        // the landing, since the create ran on a worker.
        _ = StateIntentNamingNoRow();
        Refresh();
    }

    /// <summary>Plan 47 CB-39: a template note was written outside the
    /// sidebar's own paths (the lifecycle's handler calls this): a create, so a
    /// structural history barrier, a gesture that names no row, and the
    /// refresh that lists it.</summary>
    internal void TemplateNoteLanded(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        StructuralHistoryBarrier();
        _ = StateIntentNamingNoRow();
        Refresh();
    }

    public void SpeakCaveat(string caveat)
    {
        ArgumentNullException.ThrowIfNull(caveat);
        ReportFailure(caveat);
    }

    /// <summary>Select the node for a vault-relative path: expand its
    /// ancestors, land the keys on it and select it. A node the tree has not
    /// listed yet is seated when its folder's load lists it (Plan 47 CB-39,
    /// #1670).</summary>
    internal void SelectPathFromSurface(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _ = InstallIntent(IntentKind.Reveal, path, TakeIntent());
        AdvanceWaitingReveal();
    }

    /// <summary>Plan 47 CB-39: one step down the waiting Reveal's path. The
    /// row listed anywhere in the tree (as <see cref="Flatten"/> finds it,
    /// under a date group or a collapsed folder too) is seated. Otherwise each
    /// folder on the path is opened in turn: one already listed is opened and
    /// passed through in the same call, one not listed starts its load and the
    /// Reveal waits for it. A listed level without the next segment ends the
    /// wait, unless a whole-tree publication is still to come.</summary>
    /// <param name="mayExpand">False inside Expand loaded folders, which
    /// opens folders itself; a load started here would cancel its run.</param>
    private void AdvanceWaitingReveal(bool mayExpand = true)
    {
        if (_advancingReveal)
        {
            _advanceAgain = true;
            return;
        }

        _advancingReveal = true;
        try
        {
            do
            {
                _advanceAgain = false;
                AdvanceWaitingRevealOnce(mayExpand);
            }
            while (_advanceAgain && _pendingSurfaceSelectPath is not null);
        }
        finally
        {
            _advancingReveal = false;
        }
    }

    private void AdvanceWaitingRevealOnce(bool mayExpand)
    {
        if (_pendingSurfaceSelectPath is not string pending)
        {
            return;
        }

        if (Flatten(RootNodes).FirstOrDefault(node => node.Path == pending && !node.IsPlaceholder) is { } target)
        {
            SeatReveal(target);
            return;
        }

        IEnumerable<FileTreeNodeViewModel> level = RootNodes;
        FileTreeNodeViewModel? parent = null;
        string[] segments = pending.Split('/');
        for (int depth = 0; depth < segments.Length - 1; depth++)
        {
            string folderPath = string.Join('/', segments, 0, depth + 1);
            FileTreeNodeViewModel? folder = level.FirstOrDefault(row => row.IsDirectory && row.Path == folderPath);
            if (folder is null)
            {
                SeatOrEndReveal(pending, parent);
                return;
            }

            if (!folder.IsExpanded)
            {
                if (!mayExpand)
                {
                    return;
                }

                // An already-listed folder (listed, then collapsed) starts no
                // load; one that is not listed starts its load here.
                folder.IsExpanded = true;
            }
            else if (folder.ChildLoadState is FileTreeChildLoadState.Unloaded or FileTreeChildLoadState.Failed)
            {
                // An open folder that is not listed: a cancelled Expand loaded
                // folders run leaves one, and a load that failed before this
                // Reveal is asked for once more.
                if (!mayExpand)
                {
                    return;
                }

                LoadChildren(folder);
            }

            if (_pendingSurfaceSelectPath != pending)
            {
                // A synchronous load seated or ended the Reveal inside the
                // expansion.
                return;
            }

            if (folder.ChildLoadState != FileTreeChildLoadState.Loaded)
            {
                return;
            }

            parent = folder;
            level = folder.Children;
        }

        SeatOrEndReveal(pending, parent);
    }

    /// <summary>The walk reached a listed level: a load it ran in its own turn
    /// may have listed the row, which is seated; otherwise the wait ends when
    /// the level is listed.</summary>
    private void SeatOrEndReveal(string pending, FileTreeNodeViewModel? parent)
    {
        if (Flatten(RootNodes).FirstOrDefault(node => node.Path == pending && !node.IsPlaceholder) is { } target)
        {
            SeatReveal(target);
            return;
        }

        EndRevealIfLevelIsListed(parent);
    }

    /// <summary>Plan 47 CB-39: the Reveal's row is not in a level that is
    /// listed (completely, or truncated without it): the wait ends. A level
    /// still loading keeps it, and so does a whole-tree publication still to
    /// come, which may list it (a surface's landed note), and a tree that has
    /// not published at all.</summary>
    private void EndRevealIfLevelIsListed(FileTreeNodeViewModel? parent)
    {
        if (IsTreePublicationPending || !_treePublishedOnce)
        {
            return;
        }

        if (parent is null || parent.ChildLoadState == FileTreeChildLoadState.Loaded)
        {
            EndWaitingReveal();
        }
    }

    /// <summary>Plan 47 CB-39: the waiting Reveal ends unseated, silently
    /// (#2084 owns a sentence for it).</summary>
    private void EndWaitingReveal() => _pendingSurfaceSelectPath = null;

    /// <summary>Plan 47 CB-39: a Reveal waiting on a load under
    /// <paramref name="folder"/> ends, when its path runs through it.</summary>
    private void EndWaitingRevealThrough(FileTreeNodeViewModel folder)
    {
        if (_pendingSurfaceSelectPath is string pending
            && pending.StartsWith(folder.Path + "/", StringComparison.Ordinal))
        {
            EndWaitingReveal();
        }
    }

    /// <summary>Plan 47 CB-39: a folder still in the tree was collapsed; a
    /// Reveal waiting through it ends.</summary>
    internal void FolderCollapsed(FileTreeNodeViewModel folder)
    {
        if (folder.IsAttachedToTree(RootNodes))
        {
            EndWaitingRevealThrough(folder);
        }
    }

    /// <summary>Plan 47 CB-39: seats the Reveal's row. A filter or tag scope
    /// that hides the tree is cleared first, through Clear filter (Call 13);
    /// every collapsed row above the target is opened, a date group the reader
    /// collapsed among them (Call 14); then the keys land on the row,
    /// unselected, and only then is it selected, so the selection's open
    /// switches tabs with the keys already in the tree and moves nothing
    /// (B1). The clear is the seat's own, not the reader's filter change, so
    /// it runs with the seating flag set and takes no intent.</summary>
    private void SeatReveal(FileTreeNodeViewModel target)
    {
        _pendingSurfaceSelectPath = null;
        bool outer = _seatingOwnSelection;
        _seatingOwnSelection = true;
        try
        {
            ClearFilter();
        }
        finally
        {
            _seatingOwnSelection = outer;
        }

        foreach (FileTreeNodeViewModel ancestor in AncestorsOf(target))
        {
            if (!ancestor.IsExpanded)
            {
                ancestor.IsExpanded = true;
            }
        }

        var landing = new VanishedSelection(target, Reveal: true);
        _vanishedSelection = landing;
        TreeSelectionRestored?.Invoke();
        if (ReferenceEquals(_vanishedSelection, landing))
        {
            // No window took it (none observes this sidebar yet): a later
            // restore must not find a Reveal's landing to take.
            _vanishedSelection = null;
        }

        SelectOnBothHalves(target);
    }

    /// <summary>The rows above <paramref name="target"/>, outermost first.</summary>
    private List<FileTreeNodeViewModel> AncestorsOf(FileTreeNodeViewModel target)
    {
        var path = new List<FileTreeNodeViewModel>();
        return PathTo(RootNodes) ? path : [];

        bool PathTo(IEnumerable<FileTreeNodeViewModel> level)
        {
            foreach (FileTreeNodeViewModel row in level)
            {
                if (ReferenceEquals(row, target))
                {
                    return true;
                }

                path.Add(row);
                if (PathTo(row.Children))
                {
                    return true;
                }

                path.RemoveAt(path.Count - 1);
            }

            return false;
        }
    }
}
