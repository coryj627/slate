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

    /// <summary>Plan 47 CB-39: the newest intent number taken. A landing the
    /// window defers (a create's rename focus) reads it when it is asked and
    /// gives way when a newer intent has been taken by the time it
    /// runs.</summary>
    internal long NewestIntent => _intentsTaken;

    /// <summary>Plan 47 CB-39: set while the sidebar seats a row of its own
    /// through the setter (a Reveal, a create's rename arm), which is not the
    /// reader's selection.</summary>
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

    /// <summary>Plan 47 CB-39: where the keys were when the waiting Reveal was
    /// asked, as the window recorded it (a place and the active tab). A seat
    /// after the gesture's own turn hands it back to the window, which answers
    /// where the keys may go.</summary>
    private object? _revealGesturePlace;

    /// <summary>Plan 47 CB-39: set while a Reveal's own gesture runs, so a seat
    /// inside it is not a late one.</summary>
    private bool _revealGestureTurn;

    /// <summary>Plan 47 CB-39: set while the sidebar asks for an open of its own
    /// (a row's open, a selection's, a seat's, a create's), which took its intent
    /// at its own gesture, so the workspace's open that follows takes none.</summary>
    private bool _sidebarOpening;

    /// <summary>Plan 47 CB-39: how many seats have landed the keys.</summary>
    private long _landingsTaken;

    /// <summary>Plan 47 CB-39: the newest seat that landed the keys. The window's
    /// queued editor landing reads it when it is asked and gives way when a seat
    /// has landed since. A silent seat, an open, a filter change or a rename's arm
    /// lands nothing here, so a newer workspace landing (a pane move, a tab
    /// switch, a view switch) is left to run (the red team's item 7).</summary>
    internal long NewestLanding => _landingsTaken;

    /// <summary>Plan 47 CB-39: the row a silent seat selected, and the note its
    /// selection opens (none for a folder without a folder note), until the
    /// selection changes or that note opens. A seat that lands on the row while
    /// it is still selected opens the note and says it once, as a seat does:
    /// its selection changes nothing, so the setter would do neither (Codex
    /// round 11).</summary>
    private (string Row, string? Note)? _silentSeat;

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
    private void InstallIntent(IntentKind kind, string? path, long number)
    {
        if (number != _intentsTaken)
        {
            return;
        }

        _pendingSurfaceSelectPath = kind == IntentKind.Reveal ? path : null;
        _pendingSelectPath = kind == IntentKind.NamedRow ? path : null;
        _pendingRenameArmPath = kind == IntentKind.RenameArm ? path : null;
        _revealGesturePlace = null;
        // An older mutation's focus restore retires with its row: the window's
        // fallback landing must not follow a newer intent (Codex round 7). A
        // mutation's own request arms its restore after this.
        _pendingTreeFocusRestore = false;
    }

    /// <summary>A gesture that names no row, at the gesture: it drops a
    /// waiting Reveal and a pending row. Answers its number, for a mutation
    /// that awaits before its request.</summary>
    private long StateIntentNamingNoRow()
    {
        long number = TakeIntent();
        InstallIntent(IntentKind.NoRow, null, number);
        return number;
    }

    /// <summary>Plan 47 CB-39: the reader's filter change (text typed or
    /// cleared, a tag scope applied or cleared) is a newer gesture that names
    /// no row. A Reveal still waiting ends, so its seat neither clears that
    /// filter nor takes the keys out of the field, and a create's rename arm
    /// drops, so the publication does not pull the keys into the name field. A
    /// rename's or a move's named row stays: the publication seats it silently,
    /// as main did, and its landing needs the keys in the tree (the merge
    /// judge's minor 2).</summary>
    private void FilterChanged()
    {
        _ = TakeIntent();
        EndWaitingReveal();
        _pendingRenameArmPath = null;
    }

    /// <summary>Plan 47 CB-39: the window armed the inline rename (F2, or a
    /// create's hand-off), the reader's newer gesture, and it fixes the
    /// rename's target. It takes the one slot, naming no row: a Reveal still
    /// waiting ends, so its seat neither takes the keys out of the name field
    /// nor puts its row's name there (the red team's M3), and an older
    /// mutation's named row and an older create's rename arm drop, so their
    /// publication neither moves the selection nor overwrites the name field
    /// (Codex round 6). A filter change keeps a named row; this does
    /// not.</summary>
    internal void RenameArmed()
    {
        _ = StateIntentNamingNoRow();
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
        InstallIntent(IntentKind.Reveal, path, TakeIntent());
        _revealGesturePlace = AskGesturePlace();
        bool outer = _revealGestureTurn;
        _revealGestureTurn = true;
        try
        {
            AdvanceWaitingReveal();
        }
        finally
        {
            _revealGestureTurn = outer;
        }
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
        string[] segments = pending.Split('/');
        for (int depth = 0; depth < segments.Length - 1; depth++)
        {
            string folderPath = string.Join('/', segments, 0, depth + 1);
            FileTreeNodeViewModel? folder = level.FirstOrDefault(row => row.IsDirectory && row.Path == folderPath);
            if (folder is null)
            {
                SeatOrEndReveal(pending);
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

            level = folder.Children;
        }

        SeatOrEndReveal(pending);
    }

    /// <summary>The walk reached a listed level: a load it ran in its own turn
    /// may have listed the row, which is seated; otherwise the wait ends.</summary>
    private void SeatOrEndReveal(string pending)
    {
        if (Flatten(RootNodes).FirstOrDefault(node => node.Path == pending && !node.IsPlaceholder) is { } target)
        {
            SeatReveal(target);
            return;
        }

        EndRevealIfLevelIsListed();
    }

    /// <summary>Plan 47 CB-39: the walk reached a listed level (the root, or a
    /// folder whose load listed it, completely or truncated) without the
    /// Reveal's row or next folder: the wait ends. A whole-tree publication
    /// still to come keeps it, since it may list the row (a surface's landed
    /// note), and so does a tree that has not published at all.</summary>
    private void EndRevealIfLevelIsListed()
    {
        if (IsTreePublicationPending || !_treePublishedOnce)
        {
            return;
        }

        EndWaitingReveal();
    }

    /// <summary>Plan 47 CB-39: the waiting Reveal ends unseated, silently
    /// (#2084 owns a sentence for it).</summary>
    private void EndWaitingReveal()
    {
        _pendingSurfaceSelectPath = null;
        _revealGesturePlace = null;
    }

    /// <summary>Plan 47 CB-39: the workspace opened a note (Quick Open, a
    /// search hit, a link, a surface's open; the lifecycle's handler calls
    /// this): the reader's newer gesture, which names no row. A Reveal still
    /// waiting ends, so its seat never opens its note over the reader's, and a
    /// create's rename arm and a mutation's named row drop, so their publication
    /// neither selects their row over the reader's note nor takes the keys into
    /// the name field (the red team's item 1). The sidebar's own opens took
    /// their intent at their gesture and take none here; a seat's own open among
    /// them ends no Reveal asked from the seat's own handlers (the red team's
    /// minor 7).</summary>
    /// <remarks>The workspace raises its open after its dirty-tab gate, whose
    /// dialog runs a modal loop: work published inside that loop runs before
    /// this intent is taken (a late seat ends there, under the loop).</remarks>
    internal void NoteOpenedInTheWorkspace(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        // A silent seat's row whose own note opened, from anywhere, has had
        // its open; a seat that lands on it later says nothing again.
        if (_silentSeat is { Note: { } note } && string.Equals(note, path, StringComparison.Ordinal))
        {
            _silentSeat = null;
        }

        if (!_sidebarOpening)
        {
            _ = StateIntentNamingNoRow();
        }
    }

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
    /// (B1).</summary>
    /// <remarks>A seat after the gesture's own turn asks the window first
    /// where the keys may go (<see cref="LateRevealAnswer"/>). They land when
    /// they are still in the place the gesture left them (the same editor,
    /// header, table, tree or list, on the same tab), stranded, or already in
    /// the Files tree. In the Files pane's fields, lists or buttons they are
    /// the reader's own work there, and the wait ends. Anywhere else, or with
    /// Files hidden, the row is selected silently on both halves: no filter
    /// clear, no landing, no open, nothing said, and the reader's editor and
    /// tabs stay as they are (the lead's ruling (a) on the merge judge's major
    /// 1). Under an active filter or tag scope a silent seat would select a row
    /// the filter hides, so the wait ends instead (the red team's minor 3). A
    /// seat that selects takes an intent number of its own, so a gesture's
    /// deferred landing asked before it gives way (the judge's minor
    /// 1).</remarks>
    private void SeatReveal(FileTreeNodeViewModel target)
    {
        _pendingSurfaceSelectPath = null;
        object? gesturePlace = _revealGesturePlace;
        _revealGesturePlace = null;
        LateRevealAnswer answer = _revealGestureTurn ? LateRevealAnswer.Land : AskLateReveal(target, gesturePlace);
        if (answer == LateRevealAnswer.SelectSilently && IsFilterActive)
        {
            answer = LateRevealAnswer.End;
        }

        if (answer == LateRevealAnswer.End)
        {
            return;
        }

        _ = TakeIntent();
        if (answer == LateRevealAnswer.SelectSilently)
        {
            ExpandAncestorsOf(target);
            SelectSilently(target);
            _silentSeat = (target.Path, target.IsDirectory ? (target.HasFolderNote ? FolderNotePath(target) : null) : target.Path);
            return;
        }

        // A seat that lands moves the keys: the window's editor landing queued
        // before it gives way (Codex round 9). A silent seat counts none.
        _landingsTaken++;

        // The clear's intent drops nothing: the slot is empty, no newer intent waits.
        ClearFilter();
        ExpandAncestorsOf(target);

        var landing = new VanishedSelection(target, Reveal: true);
        _vanishedSelection = landing;
        TreeSelectionRestored?.Invoke();
        if (ReferenceEquals(_vanishedSelection, landing))
        {
            // No window took it (none observes this sidebar yet): a later
            // restore must not find a Reveal's landing to take.
            _vanishedSelection = null;
        }

        // A row a silent seat selected is this very node, selected already, so
        // the selection below changes nothing and its open and line would not
        // come: the seat runs them once (Codex round 11). A row the reader
        // selected and opened says nothing again.
        bool silentlySeated = ReferenceEquals(SelectedNode, target)
            && string.Equals(_silentSeat?.Row, target.Path, StringComparison.Ordinal);
        _silentSeat = null;

        // Its open is the sidebar's own (RequestOpen marks it), so it ends no
        // Reveal asked from the seat's own handlers.
        SelectOnBothHalves(target);
        if (silentlySeated)
        {
            SaySelectionAndOpen(target);
        }
    }

    /// <summary>Opens every collapsed row above <paramref name="target"/>, a
    /// date group among them (Call 14).</summary>
    private void ExpandAncestorsOf(FileTreeNodeViewModel target)
    {
        foreach (FileTreeNodeViewModel ancestor in AncestorsOf(target))
        {
            if (!ancestor.IsExpanded)
            {
                ancestor.IsExpanded = true;
            }
        }
    }

    /// <summary>Plan 47 CB-39: a Reveal's gesture asks the window where the
    /// keys are (the restore event, with
    /// <see cref="VanishedSelection.RevealAsked"/>). No window answers
    /// nothing.</summary>
    private object? AskGesturePlace()
    {
        var question = new VanishedSelection(null, RevealAsked: true);
        AskTheWindow(question);
        return question.Place;
    }

    /// <summary>Plan 47 CB-39: a late seat asks the window where the keys may
    /// go (the restore event, with <see cref="VanishedSelection.LateReveal"/>).
    /// No window answers <see cref="LateRevealAnswer.Land"/>, and the seat
    /// lands as before.</summary>
    private LateRevealAnswer AskLateReveal(FileTreeNodeViewModel target, object? gesturePlace)
    {
        var question = new VanishedSelection(target, LateReveal: true, GesturePlace: gesturePlace);
        AskTheWindow(question);
        return question.Answer;
    }

    private void AskTheWindow(VanishedSelection question)
    {
        _vanishedSelection = question;
        TreeSelectionRestored?.Invoke();
        if (ReferenceEquals(_vanishedSelection, question))
        {
            _vanishedSelection = null;
        }
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
