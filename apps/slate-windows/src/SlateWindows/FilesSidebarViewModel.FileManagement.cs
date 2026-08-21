// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using SlateWindows.FileManagement;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W5-4 (#744): structural-report consumption, the hardened rename,
/// and the structural undo domain — the machinery every file verb
/// shares. Contracts: docs/plans/31_file_management_contracts.md
/// (F3, F9, F10).
/// </summary>
internal sealed partial class FilesSidebarViewModel
{
    private readonly StructuralUndoJournal _structuralUndo = new();

    // W5-4 Phase B/C verbs (assigned with the other commands in the
    // main-file constructor).
    public System.Windows.Input.ICommand DuplicateCommand { get; }

    public System.Windows.Input.ICommand CopyPathCommand { get; }

    public System.Windows.Input.ICommand RevealCommand { get; }

    public System.Windows.Input.ICommand MoveToCommand { get; }

    /// <summary>Lifecycle-supplied retarget seam (F9): the report's
    /// <c>Moved</c> pairs retarget open tabs SYNCHRONOUSLY at the
    /// mutation site; the event-stream retarget stays wired and
    /// no-ops on already-retargeted tabs (idempotent by
    /// construction — it matches on the old path).</summary>
    internal Action<string, string>? RetargetRequested { get; set; }

    /// <summary>The pending failure detail from the last consumed
    /// report — the caller's FINAL status composes it in (codex round
    /// 1: setting Status here and letting the success sentence
    /// overwrite it made the affected paths unreachable the moment
    /// the command finished).</summary>
    private string? _pendingRewriteFailureDetail;

    /// <summary>Consume a structural report (F9): retarget every
    /// moved pair, surface failed rewrites, and hand back the
    /// distinct rewritten count for the announcement suffix.</summary>
    private int ConsumeStructuralReport(StructuralReport report)
    {
        foreach (MovedPath moved in report.Moved)
        {
            RetargetRequested?.Invoke(moved.OldPath, moved.NewPath);
        }

        if (report.Failed.Length > 0)
        {
            // Per-file rewrite failures never abort the operation
            // (core's rule); they surface as a recorded refusal with
            // the affected paths reachable in Status (F9). Kinds, not
            // platform io-text, travel in the sentence.
            string paths = string.Join(
                ", ", report.Failed.Select(failure => failure.Path));
            _pendingRewriteFailureDetail =
                $"Links in {report.Failed.Length} "
                + $"{(report.Failed.Length == 1 ? "note" : "notes")} could not "
                + $"be updated: {paths}";
            // W0.5-3 residue: structural-mutation announcement family
            // (the U2-6 wrappers are residue on mac too — F11).
            _announce(new A11yEvent.HostComposed(
                $"Links in {report.Failed.Length} "
                + $"{(report.Failed.Length == 1 ? "note" : "notes")} could not "
                + "be updated.",
                A11yPriority.High));
        }

        return report.Rewritten
            .Select(outcome => outcome.Path)
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    /// <summary>Set when TransformStoredPaths could not persist the
    /// rewritten pins/shortcuts (codex round 2) — composed into the
    /// mutation's final status like the rewrite-failure
    /// detail.</summary>
    private bool _pendingStoredPathPersistFailure;

    private void RecordStoredPathPersistFailure() =>
        _pendingStoredPathPersistFailure = true;

    /// <summary>Compose the mutation's FINAL status: the success
    /// sentence plus any consumed report's failure detail and any
    /// stored-path persistence failure, so nothing the user must act
    /// on vanishes under the success sentence (codex rounds
    /// 1-2).</summary>
    private string WithRewriteFailureDetail(string sentence)
    {
        string? detail = _pendingRewriteFailureDetail;
        _pendingRewriteFailureDetail = null;
        string composed = detail is null ? sentence : $"{sentence} {detail}";
        if (_pendingStoredPathPersistFailure)
        {
            _pendingStoredPathPersistFailure = false;
            composed += " Sidebar pins and shortcuts could not be saved.";
        }

        return composed;
    }

    /// <summary>The status to re-assert after the in-flight refresh
    /// publishes (codex round 2): ApplyTreeRefresh's own arms
    /// (root overflow, settings notice, restored-expansion overflow)
    /// write Status AFTER the mutation reported, erasing the only
    /// inspectable failure detail. The mutation result wins the turn;
    /// a persistent condition returns on the next organic
    /// refresh.</summary>
    private string? _statusToReassert;

    private void ReassertStatusAfterPublication()
    {
        if (_statusToReassert is string reassert)
        {
            _statusToReassert = null;
            Status = reassert;
        }
    }

    /// <summary>The mutation result channel (codex round 1): Status
    /// carries the sentence PLUS any failure detail (the paths stay
    /// inspectable); speech carries the sentence only — the High
    /// failure announcement already spoke, and reading a path list
    /// aloud twice is noise.</summary>
    private void ReportMutationResult(string sentence)
    {
        Status = WithRewriteFailureDetail(sentence);
        if (IsRefreshingTree)
        {
            _statusToReassert = Status;
        }

        // W0.5-3 residue: Windows sidebar action-result copy.
        _announce(new A11yEvent.HostComposed(sentence, A11yPriority.Medium));
    }

    /// <summary>Red team (correctness 2): a compound folder+note
    /// rename moves the NOTE'S LEAF beyond prefix math
    /// (<c>docs/docs.md → guide/guide.md</c>), so stored paths —
    /// pins, shortcuts, recents, history — transformed by the folder
    /// prefix still point at <c>guide/docs.md</c>, a path that does
    /// not exist. The report's <c>Moved</c> pairs carry the truth:
    /// correct every pair whose real destination differs from its
    /// prefix image. Plain renames and moves no-op here (image ==
    /// destination).</summary>
    private void CorrectStoredPathsFromReport(
        StructuralReport report, string oldPath, string newPath)
    {
        foreach (MovedPath moved in report.Moved)
        {
            string prefixImage;
            if (moved.OldPath == oldPath)
            {
                prefixImage = newPath;
            }
            else if (moved.OldPath.StartsWith(
                oldPath + "/", StringComparison.Ordinal))
            {
                prefixImage = newPath + moved.OldPath[oldPath.Length..];
            }
            else
            {
                continue;
            }

            if (prefixImage != moved.NewPath)
            {
                TransformStoredPaths(
                    prefixImage, moved.NewPath, isDirectory: false, deleted: false);
            }
        }
    }

    /// <summary>mac's mutation sentence with the links suffix: the
    /// suffix replaces the period — "Renamed a to b, updated links in
    /// N notes." (distinct-count; singular "note").</summary>
    private static string WithLinksSuffix(string sentenceWithPeriod, int rewritten) =>
        rewritten > 0
            ? $"{sentenceWithPeriod[..^1]}, updated links in {rewritten} "
                + $"{(rewritten == 1 ? "note" : "notes")}."
            : sentenceWithPeriod;

    /// <summary>
    /// The hardened rename (F3): folder renames go through
    /// <c>RenameFolderWithNote</c> UNCONDITIONALLY — core decides
    /// folder-note presence under the structural lock (the host-side
    /// <c>HasFolderNote</c> probe was the raced probe mac's review
    /// removed) — the report is consumed, the inverse lands on the
    /// undo stack, and a failure KEEPS THE FIELD: returns false with
    /// the reason in Status and <c>MutationName</c> untouched, so the
    /// caller leaves focus in the rename box.
    /// </summary>
    internal bool TryRenameSelected()
    {
        if (SelectedNode is not FileTreeNodeViewModel node)
        {
            return false;
        }

        _pendingRewriteFailureDetail = null;
        _pendingStoredPathPersistFailure = false;
        string oldPath = node.Path;
        string oldName = node.Name;
        string newName = MutationName;
        string newPath = CombineVaultPath(ParentPath(oldPath), newName);
        try
        {
            StructuralReport? report = null;
            if (!TryRunSessionWork(() =>
            {
                report = node.IsDirectory
                    ? _session.RenameFolderWithNote(oldPath, newName)
                    : _session.RenameFile(oldPath, newName);
            }) || report is null)
            {
                return false;
            }

            int rewritten = ConsumeStructuralReport(report);
            TransformStoredPaths(oldPath, newPath, node.IsDirectory, deleted: false);
            CorrectStoredPathsFromReport(report, oldPath, newPath);
            _structuralUndo.Push(new StructuralUndoStep(
                StructuralUndoKind.Rename,
                Path: newPath,
                Argument: oldName,
                node.IsDirectory,
                Noun: oldName,
                Identity: FileIdentity.TryGet(AbsoluteVaultPath(newPath))));
            // Refresh FIRST (codex round 1): Refresh() writes
            // "Loading files…" into Status synchronously, so a
            // sentence reported before it never survived the same
            // dispatcher turn — the result must be the LAST Status
            // writer.
            RequestSelectionAt(newPath);
            Refresh();
            ReportMutationResult(WithLinksSuffix(
                $"Renamed {node.DisplayName} to {newName}.", rewritten));
            return true;
        }
        catch (VaultException exception)
        {
            // F3: the field stays open with the reason inline — core's
            // message relayed, MutationName untouched.
            ReportFailure($"Rename failed: {exception.Message}");
            return false;
        }
    }

    /// <summary>Structural undo (F10): pop, preflight, re-run the
    /// forward FFI with the inverse arguments, land the re-inverse on
    /// the redo stack. Empty-stack chords still speak (mac:
    /// "Nothing to undo.").</summary>
    internal void UndoStructural()
    {
        if (!_structuralUndo.TryPopUndo(out StructuralUndoStep step))
        {
            AnnounceUndoResidue("Nothing to undo.");
            return;
        }

        ExecuteUndoStep(step, redo: false);
    }

    internal void RedoStructural()
    {
        if (!_structuralUndo.TryPopRedo(out StructuralUndoStep step))
        {
            AnnounceUndoResidue("Nothing to redo.");
            return;
        }

        ExecuteUndoStep(step, redo: true);
    }

    /// <summary>Creates, duplicates, and trash are history BARRIERS
    /// (mac's table): both stacks clear so a stale inverse can never
    /// target a path a barrier op now owns. Internal so the lifecycle
    /// can barrier for W5-3's template create too (F10 lists it; red
    /// team, contracts 1).</summary>
    internal void StructuralHistoryBarrier() => _structuralUndo.Barrier();

    private void ExecuteUndoStep(StructuralUndoStep step, bool redo)
    {
        _pendingRewriteFailureDetail = null;
        _pendingStoredPathPersistFailure = false;
        string verb = redo ? "Redid" : "Undid";
        // The executability preflight (mac: drop suspect history
        // rather than replay inverses against strangers). Existence
        // alone admits a REPLACEMENT at the recorded path (codex
        // round 2: delete b.md, create an unrelated b.md, Ctrl+Z
        // renames the stranger) — the filesystem identity captured at
        // push must still match; a null token on either side degrades
        // to the existence check. BatchMove preflights core-side —
        // UndoBatchMove refuses typed when the latest journal row is
        // not the batch.
        if (step.Kind is not StructuralUndoKind.BatchMove)
        {
            string absolute = AbsoluteVaultPath(step.Path);
            bool exists = System.IO.File.Exists(absolute)
                || System.IO.Directory.Exists(absolute);
            bool identityHolds = exists
                && (step.Identity is not string recorded
                    || FileIdentity.TryGet(absolute) is not string current
                    || current == recorded);
            if (!exists || !identityHolds)
            {
                _structuralUndo.DropForChangedFiles();
                AnnounceUndoResidue(redo
                    ? "Can't redo — the files have changed."
                    : "Can't undo — the files have changed.");
                return;
            }
        }

        try
        {
            StructuralUndoStep? inverse = null;
            var batchReport = default(BatchMoveReport);
            bool sessionRan = TryRunSessionWork(() =>
            {
                switch (step.Kind)
                {
                    case StructuralUndoKind.Rename:
                        string currentName =
                            System.IO.Path.GetFileName(step.Path);
                        string restoredPath = CombineVaultPath(
                            ParentPath(step.Path), step.Argument);
                        StructuralReport renamed = step.IsDirectory
                            ? _session.RenameFolderWithNote(step.Path, step.Argument)
                            : _session.RenameFile(step.Path, step.Argument);
                        _ = ConsumeStructuralReport(renamed);
                        TransformStoredPaths(
                            step.Path, restoredPath, step.IsDirectory, deleted: false);
                        CorrectStoredPathsFromReport(renamed, step.Path, restoredPath);
                        inverse = step with
                        {
                            Path = restoredPath,
                            Argument = currentName,
                            Noun = currentName,
                        };
                        break;
                    case StructuralUndoKind.Move:
                        string leaf = System.IO.Path.GetFileName(step.Path);
                        string previousParent = ParentPath(step.Path);
                        string movedPath = CombineVaultPath(step.Argument, leaf);
                        StructuralReport movedReport = step.IsDirectory
                            ? _session.MoveFolder(step.Path, step.Argument)
                            : _session.MoveFile(step.Path, step.Argument);
                        _ = ConsumeStructuralReport(movedReport);
                        TransformStoredPaths(
                            step.Path, movedPath, step.IsDirectory, deleted: false);
                        CorrectStoredPathsFromReport(movedReport, step.Path, movedPath);
                        inverse = step with
                        {
                            Path = movedPath,
                            Argument = previousParent,
                        };
                        break;
                    case StructuralUndoKind.BatchMove:
                        batchReport = _session.UndoBatchMove(step.BatchOpId);
                        foreach (BatchPathChange change in batchReport.Standing)
                        {
                            RetargetRequested?.Invoke(change.OldPath, change.NewPath);
                            TransformStoredPaths(
                                change.OldPath, change.NewPath,
                                change.IsDirectory, deleted: false);
                        }

                        // Red team (correctness 1): the endpoint reports
                        // failure as a STATE, not an exception — a
                        // Rejected/RolledBack/Partial undo must never
                        // announce success or push a re-inverse (the
                        // OpId fallback inverted undo and redo). Only a
                        // clean Succeeded round-trip records one.
                        if (batchReport.State == BatchMoveState.Succeeded
                            && batchReport.OpId is long inverseOpId)
                        {
                            inverse = step with { BatchOpId = inverseOpId };
                        }

                        break;
                }
            });
            if (!sessionRan)
            {
                // The shutdown lease refused; the step is consumed but
                // the vault is closing — nothing to restore.
                return;
            }

            if (inverse is null)
            {
                // The batch endpoint refused as a STATE (Rejected — the
                // recorded destinations changed out from under the op —
                // or a Partial/RolledBack residue, whose Standing pairs
                // were retargeted above): the files DID change; drop
                // the suspect history and say so, never success. The
                // failure leg reconciles focus too (verification 2).
                _structuralUndo.DropForChangedFiles();
                RequestSelectionAt(null);
                Refresh();
                AnnounceUndoResidue(redo
                    ? "Can't redo — the files have changed."
                    : "Can't undo — the files have changed.");
                return;
            }

            // The re-inverse targets the entry's post-undo location;
            // re-capture the identity there (the file ID survives the
            // move, but a fresh read keeps the token honest on
            // filesystems that rewrite it).
            if (inverse.Kind is not StructuralUndoKind.BatchMove)
            {
                inverse = inverse with
                {
                    Identity = FileIdentity.TryGet(AbsoluteVaultPath(inverse.Path)),
                };
            }

            if (redo)
            {
                _structuralUndo.PushUndoFromRedo(inverse);
            }
            else
            {
                _structuralUndo.PushRedo(inverse);
            }

            // The undo's consumed report may carry rewrite failures
            // too — same final-status composition (codex round 1),
            // reported AFTER Refresh so it survives the "Loading
            // files…" write. The re-inverse's Path is the entry's
            // CURRENT location; batches have no single successor to
            // name.
            RequestSelectionAt(
                step.Kind == StructuralUndoKind.BatchMove ? null : inverse.Path);
            Refresh();
            ReportMutationResult(step.Kind switch
            {
                StructuralUndoKind.Rename => $"{verb} rename to {step.Argument}.",
                StructuralUndoKind.Move => $"{verb} move of {step.Noun}.",
                _ => $"{verb} move of {step.Noun}.",
            });
        }
        catch (VaultException)
        {
            // The inverse no longer applies (a stale batch handle, an
            // occupied destination, changed files): drop the suspect
            // history wholesale — mac's rule.
            _structuralUndo.DropForChangedFiles();
            AnnounceUndoResidue(redo
                ? "Can't redo — the files have changed."
                : "Can't undo — the files have changed.");
        }
    }

    /// <summary>Test-visible reveal seam (F8): defaults to
    /// <c>explorer.exe /select,</c> on the vault-resolved absolute
    /// path. No announcement (the OS surface change is the feedback),
    /// no chord, no undo.</summary>
    internal Action<string>? RevealRequested { get; set; }

    /// <summary>F1/F2 hand-off: raised after a create's refresh has
    /// published the new node and the sidebar has selected it — the
    /// window arms the rename flow (expander open, focus in the name
    /// field, stem selected: the F2 flow re-armed
    /// programmatically).</summary>
    internal event Action? InlineRenameRequested;

    private string? _pendingRenameArmPath;

    /// <summary>The unique-untitled sequence (F1/F2): "Untitled.md",
    /// "Untitled 2.md", … / "Untitled Folder", "Untitled Folder 2", …
    /// — the exclusive create's typed <c>DestinationExists</c> is the
    /// advance signal, never a pre-check.</summary>
    internal static IEnumerable<string> UntitledCandidates(
        string parent, string baseName, string extension)
    {
        for (int i = 1; i <= 200; i++)
        {
            string stem = i == 1 ? baseName : $"{baseName} {i}";
            yield return CombineVaultPath(parent, stem + extension);
        }
    }

    private void RequestInlineRenameAt(string vaultPath) =>
        _pendingRenameArmPath = vaultPath;

    /// <summary>Raised at tree publication after a mutation that asked
    /// for selection/focus reconciliation — the window restores
    /// keyboard focus to the tree unless another surface claimed the
    /// moment (red team, a11y 2: the refresh discards the focused
    /// container and WPF ejects keyboard focus to the window, leaving
    /// every tree-scoped chord dead and silent).</summary>
    internal event Action? TreeSelectionRestored;

    private string? _pendingSelectPath;
    private bool _pendingTreeFocusRestore;

    /// <summary>Ask the NEXT publication to re-seat selection at
    /// <paramref name="vaultPath"/> (null = no selection — just the
    /// focus restore; deletes have no successor to name).</summary>
    private void RequestSelectionAt(string? vaultPath)
    {
        _pendingSelectPath = vaultPath;
        _pendingTreeFocusRestore = true;
    }

    /// <summary>Publication half of <see cref="RequestSelectionAt"/>:
    /// re-seat the view-model selection on the FRESH node (silently —
    /// the mutation already spoke; the setter's open-on-select must
    /// not fire for a restoration) and raise the focus-restore
    /// event.</summary>
    private void ConsumePendingSelection()
    {
        if (!_pendingTreeFocusRestore)
        {
            return;
        }

        _pendingTreeFocusRestore = false;
        string? pending = _pendingSelectPath;
        _pendingSelectPath = null;
        if (pending is not null
            && Flatten(RootNodes).FirstOrDefault(node => node.Path == pending)
                is { } fresh)
        {
            SelectSilently(fresh);
        }

        TreeSelectionRestored?.Invoke();
    }

    /// <summary>Consumed at tree publication: select the created node
    /// and raise the hand-off. A node the refresh did not materialize
    /// (a collapsed deep parent) drops the arm — the create already
    /// spoke and opened; only the rename hand-off degrades.</summary>
    private void ConsumePendingRenameArm()
    {
        if (_pendingRenameArmPath is not string pending)
        {
            return;
        }

        _pendingRenameArmPath = null;
        FileTreeNodeViewModel? created = Flatten(RootNodes)
            .FirstOrDefault(node => node.Path == pending);
        if (created is null)
        {
            // The node did not materialize (a collapsed deep parent):
            // the rename hand-off degrades, but the keyboard user must
            // not stay stranded on a discarded container
            // (verification 3).
            TreeSelectionRestored?.Invoke();
            return;
        }

        SelectedNode = created;
        InlineRenameRequested?.Invoke();
    }

    // ---- The Move-To picker (F4) ---------------------------------

    private MoveToPickerViewModel? _moveToSheet;

    /// <summary>The Move-To picker sheet, or null when closed. The
    /// window observes this for present/dismiss (the template-sheet
    /// shape) and the modal-surface state reads it.</summary>
    public MoveToPickerViewModel? MoveToSheet
    {
        get => _moveToSheet;
        private set => SetField(ref _moveToSheet, value);
    }

    /// <summary>The window's modal admission seam (T9's shape):
    /// consulted BEFORE presenting, so the sheet never opens beneath a
    /// higher surface. Null (headless tests) admits.</summary>
    internal Func<bool>? MoveToOpenAdmission { get; set; }

    /// <summary>F4: open the picker for the batch-checked items, or
    /// the tree selection when no checks are active (the CanExecute
    /// defect this retires: the verb was dead without both checks and
    /// a typed destination).</summary>
    internal void OpenMoveTo()
    {
        StructuralBatchItem[] items = MoveTargets();
        if (items.Length == 0)
        {
            return;
        }

        // Enumerate BEFORE the admission (red team, a11y 7): the
        // admission dismisses overlays and captures their focus
        // lineage — a walk failure after it would strand the captured
        // token with no sheet to restore it.
        IReadOnlyList<string> folders;
        try
        {
            folders = EnumerateVaultFolders();
        }
        catch (VaultException exception)
        {
            ReportFailure($"Move failed: {exception.Message}");
            return;
        }

        if (MoveToOpenAdmission?.Invoke() == false)
        {
            return;
        }

        var illegalParents = new HashSet<string>(
            items.Select(item => ParentPath(item.Path)), StringComparer.Ordinal);
        string[] movingFolders = [.. items
            .Where(item => item.IsDirectory)
            .Select(item => item.Path)];
        var knownFolders = new HashSet<string>(
            folders, StringComparer.OrdinalIgnoreCase);
        var illegalParentsTyped = new HashSet<string>(
            illegalParents, StringComparer.OrdinalIgnoreCase);
        string[] legal = [.. folders.Where(folder =>
            !illegalParents.Contains(folder)
            && !movingFolders.Any(moving => folder == moving
                || folder.StartsWith(moving + "/", StringComparison.Ordinal)))];

        // Red team (correctness 4): the typed New Folder path obeys
        // the SAME legality the folder list does — an item's current
        // parent, a moving folder's own subtree, and any KNOWN folder
        // (the legal ones are picked as rows; the filtered-illegal
        // ones must not resurface as a "create" that refuses typed).
        // Every arm is case-INSENSITIVE against user-typed text
        // (verification 5): "Docs/archive" under moving folder "docs"
        // is the same subtree on this filesystem.
        bool NewFolderPathAllowed(string typed) =>
            !knownFolders.Contains(typed)
            && !illegalParentsTyped.Contains(typed)
            && !movingFolders.Any(moving =>
                string.Equals(typed, moving, StringComparison.OrdinalIgnoreCase)
                || typed.StartsWith(
                    moving + "/", StringComparison.OrdinalIgnoreCase));

        string noun = items.Length == 1
            ? System.IO.Path.GetFileName(items[0].Path)
            : $"{items.Length:N0} items";
        MoveToSheet = new MoveToPickerViewModel(
            legal,
            rootIsLegal: !illegalParents.Contains(string.Empty),
            itemNoun: noun,
            confirmed: destination => ExecuteMoveTo(items, destination),
            createAndMove: path => CreateFolderThenMove(items, path),
            cancelled: () => MoveToSheet = null,
            newFolderPathAllowed: NewFolderPathAllowed,
            announce: _announce);
        // W0.5-3 residue: Move-To presentation copy.
        _announce(new A11yEvent.HostComposed(
            $"Move {noun}: choose a destination folder.", A11yPriority.Medium));
    }

    /// <summary>Batch checks win; otherwise the tree selection (F4's
    /// unified targeting).</summary>
    private StructuralBatchItem[] MoveTargets()
    {
        StructuralBatchItem[] batch = SelectedBatchItems();
        if (batch.Length > 0)
        {
            return batch;
        }

        return SelectedNode is { IsPlaceholder: false, IsGroupHeader: false } node
            ? [new StructuralBatchItem(node.Path, node.IsDirectory)]
            : [];
    }

    /// <summary>Every vault folder via the paged walk (F4): breadth-
    /// first over <c>ListDirChildrenPage</c> with cursor continuation
    /// (core caps a single page at 10,000 rows), bounded at 50,000
    /// folders total.</summary>
    private IReadOnlyList<string> EnumerateVaultFolders()
    {
        const int Cap = 50_000;
        const uint PageLimit = 1_000;
        var folders = new List<string>();
        var queue = new Queue<string>();
        queue.Enqueue(string.Empty);
        using var cancel = new CancelToken();
        while (queue.Count > 0 && folders.Count < Cap)
        {
            string parent = queue.Dequeue();
            string? cursor = null;
            do
            {
                DirListingPage page = _session.ListDirChildrenPage(
                    parent, new Paging(cursor, PageLimit), cancel);
                foreach (DirNodeSummary dir in page.Dirs)
                {
                    if (folders.Count >= Cap)
                    {
                        return folders;
                    }

                    folders.Add(dir.Path);
                    queue.Enqueue(dir.Path);
                }

                cursor = page.NextCursor;
            }
            while (cursor is not null);
        }

        return folders;
    }

    /// <summary>F4 execution: one item rides the single-entry FFIs
    /// with report consumption; multiple ride <c>BatchMove</c> with
    /// its one summary. Destination "" speaks "vault root".</summary>
    private void ExecuteMoveTo(StructuralBatchItem[] items, string destination)
    {
        _pendingRewriteFailureDetail = null;
        _pendingStoredPathPersistFailure = false;
        MoveToSheet = null;
        if (items.Length == 1)
        {
            StructuralBatchItem single = items[0];
            string leaf = System.IO.Path.GetFileName(single.Path);
            try
            {
                StructuralReport? report = null;
                if (!TryRunSessionWork(() =>
                {
                    report = single.IsDirectory
                        ? _session.MoveFolder(single.Path, destination)
                        : _session.MoveFile(single.Path, destination);
                }) || report is null)
                {
                    return;
                }

                int rewritten = ConsumeStructuralReport(report);
                string movedPath = CombineVaultPath(destination, leaf);
                TransformStoredPaths(single.Path, movedPath, single.IsDirectory, deleted: false);
                CorrectStoredPathsFromReport(report, single.Path, movedPath);
                _structuralUndo.Push(new StructuralUndoStep(
                    StructuralUndoKind.Move,
                    Path: movedPath,
                    Argument: ParentPath(single.Path),
                    single.IsDirectory,
                    Noun: leaf,
                    Identity: FileIdentity.TryGet(AbsoluteVaultPath(movedPath))));
                string destLeaf = destination.Length == 0
                    ? "vault root"
                    : System.IO.Path.GetFileName(destination.TrimEnd('/'));
                RequestSelectionAt(movedPath);
                Refresh();
                ReportMutationResult(WithLinksSuffix(
                    $"Moved {leaf} to {destLeaf}.", rewritten));
            }
            catch (VaultException exception)
            {
                // A create-then-move whose move half refused must not
                // leave the created folder invisible until the next
                // organic refresh (red team, correctness 4) — and the
                // failure leg reconciles focus too (verification 2).
                // The failure reports AFTER Refresh so its reason
                // survives the "Loading files…" write.
                RequestSelectionAt(null);
                Refresh();
                ReportFailure($"Move failed: {exception.Message}");
            }

            return;
        }

        MoveDestination = destination;
        BatchMove();
    }

    /// <summary>The "New Folder…" row (F4): create the typed folder,
    /// then move — one user gesture, two core ops. A create failure
    /// keeps the sheet open with the reason, so the gesture can be
    /// corrected in place.</summary>
    private void CreateFolderThenMove(StructuralBatchItem[] items, string folderPath)
    {
        try
        {
            StructuralReport? report = null;
            if (!TryRunSessionWork(() => report = _session.CreateFolder(folderPath)))
            {
                return;
            }

            if (report is not null)
            {
                _ = ConsumeStructuralReport(report);
            }
        }
        catch (VaultException exception)
        {
            ReportFailure($"Could not create folder {folderPath}: {exception.Message}");
            return;
        }

        // F10: a create is a history barrier here exactly as the
        // standalone verb is (red team, correctness 5).
        StructuralHistoryBarrier();
        ExecuteMoveTo(items, folderPath);
    }

    /// <summary>The F6 confirmation seam — the History seam pattern:
    /// callers stage only the title and message; the destructive
    /// button label is PINNED here ("Move to the Recycle Bin" /
    /// "Cancel"), and the dialog focuses Cancel with no default
    /// button, so bare Enter never confirms. Supersedes the bare
    /// message-only <c>_confirmDestructive</c> for the trash
    /// verbs.</summary>
    internal Func<(string Title, string Message), bool> ConfirmRecycle { get; set; } =
        request => HistoryConfirmationDialog.Confirm(
            request.Title, request.Message,
            confirmLabel: RecycleBinCopy.ActionLabel);

    /// <summary>The F6 child-count probe, re-run AT STAGE TIME so a
    /// stale zero can never bypass the confirmation. Null (an
    /// unreadable folder) is fail-closed: it stages the confirmation
    /// like a non-empty folder.</summary>
    private int? CountFolderContents(string vaultRelative)
    {
        if (_vaultRoot is null)
        {
            return null;
        }

        try
        {
            return System.IO.Directory.EnumerateFileSystemEntries(
                AbsoluteVaultPath(vaultRelative),
                "*",
                System.IO.SearchOption.AllDirectories).Count();
        }
        catch (Exception exception)
            when (exception is System.IO.IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>F5: mac's Finder-parity duplicate — no new FFI, a
    /// <c>ReadText</c> + <c>CreateExclusive</c> loop advancing on
    /// typed <c>DestinationExists</c> (bounded at 200 candidates).
    /// Files only: a folder selection announces the canonical
    /// <c>DuplicateFilesOnly</c> — consumed here for the first time
    /// on Windows. A duplicate is a history BARRIER (mac's rule for
    /// creates).</summary>
    internal void DuplicateSelected()
    {
        if (SelectedNode is not FileTreeNodeViewModel node)
        {
            return;
        }

        if (node.IsDirectory)
        {
            Status = "Duplicate applies to files only.";
            _announce(new A11yEvent.DuplicateFilesOnly());
            return;
        }

        try
        {
            if (!TryRunSessionWork(() => _session.ReadText(node.Path), out string? text)
                || text is null)
            {
                return;
            }

            string? created = null;
            foreach (string candidate in DuplicateCandidates(node.Path))
            {
                try
                {
                    if (!TryRunSessionWork(() => _session.CreateExclusive(candidate, text)))
                    {
                        return;
                    }

                    created = candidate;
                    break;
                }
                catch (VaultException.DestinationExists)
                {
                    // The namer's advance signal — typed, never a
                    // pre-check (F1's CreateExclusive rule).
                }
            }

            if (created is null)
            {
                ReportFailure(
                    $"Could not duplicate {node.DisplayName}: no free name.");
                return;
            }

            StructuralHistoryBarrier();
            // Selection lands on the copy (Finder's shape), and focus
            // reconciles (verification 3); the sentence reports AFTER
            // Refresh so it survives the "Loading files…" write.
            RequestSelectionAt(created);
            Refresh();
            ReportResult(
                $"Duplicated {node.DisplayName} as {LeafName(created)}.");
        }
        catch (VaultException exception)
        {
            ReportFailure(
                $"Could not duplicate {node.DisplayName}: {exception.Message}");
        }
    }

    /// <summary>mac's Finder-parity namer (AppState.duplicateName,
    /// verbatim semantics): strip an existing <c>" copy"</c>/<c>"
    /// copy N"</c> suffix from the stem, then walk <c>{base} copy</c>,
    /// <c>{base} copy 2</c>, <c>{base} copy 3</c>, … — the LOWEST
    /// free name wins (the source itself occupies its own slot and
    /// advances the walk via typed <c>DestinationExists</c>, which is
    /// mac's taken-names set translated to core's exclusive
    /// create).</summary>
    internal static IEnumerable<string> DuplicateCandidates(string sourcePath)
    {
        string parent = ParentPath(sourcePath);
        string leaf = LeafName(sourcePath);
        int dot = leaf.LastIndexOf('.');
        string stem = dot > 0 ? leaf[..dot] : leaf;
        string extension = dot > 0 ? leaf[dot..] : string.Empty;

        if (stem.EndsWith(" copy", StringComparison.Ordinal))
        {
            stem = stem[..^" copy".Length];
        }
        else
        {
            Match numbered = Regex.Match(stem, @" copy \d+$");
            if (numbered.Success)
            {
                stem = stem[..numbered.Index];
            }
        }

        for (int i = 0; i < 200; i++)
        {
            string candidateStem = i == 0 ? $"{stem} copy" : $"{stem} copy {i + 1}";
            yield return CombineVaultPath(parent, candidateStem + extension);
        }
    }

    /// <summary>F7: copy the VAULT-RELATIVE path (mac's semantics —
    /// the tree path string) through the copy seam; the CopyWikilink
    /// pattern with the canonical <c>SelectionCopied</c>.</summary>
    internal void CopyPathSelected()
    {
        if (SelectedNode is not FileTreeNodeViewModel node)
        {
            return;
        }

        _copyText(node.Path);
        ReportResult($"Copied path for {node.DisplayName}.");
        _announce(new A11yEvent.SelectionCopied());
    }

    /// <summary>F8: "Reveal in File Explorer" (FD-5 records the label
    /// divergence from mac's "Reveal in Finder").</summary>
    internal void RevealSelected()
    {
        if (SelectedNode is not FileTreeNodeViewModel node)
        {
            return;
        }

        if (_vaultRoot is null)
        {
            return;
        }

        string absolute = AbsoluteVaultPath(node.Path);
        if (RevealRequested is { } reveal)
        {
            reveal(absolute);
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "explorer.exe", $"/select,\"{absolute}\"")
        {
            UseShellExecute = false,
        });
    }

    private static string LeafName(string vaultPath)
    {
        int slash = vaultPath.LastIndexOf('/');
        return slash >= 0 ? vaultPath[(slash + 1)..] : vaultPath;
    }

    private string AbsoluteVaultPath(string vaultRelative) =>
        System.IO.Path.Combine(
            _vaultRoot, vaultRelative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    private void AnnounceUndoResidue(string message)
    {
        Status = message;
        // W0.5-3 residue: structural undo announcements (mac's #871
        // strings are host-composed there too — F11).
        _announce(new A11yEvent.HostComposed(message, A11yPriority.Medium));
    }
}
