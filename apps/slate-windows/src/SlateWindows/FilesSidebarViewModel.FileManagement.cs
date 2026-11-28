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

    /// <summary>Plan 47 CB-9b (#1889; CD5(b); F9): the lifecycle wires this
    /// to the workspace, which brings each clean open tab on a note whose
    /// links a rename or a move rewrote up to the rewritten bytes
    /// (WorkspaceViewModel.RebaselineCleanTabsAfterLinkRewrite). Raised with a
    /// report's rewrites once its moved pairs are retargeted, so a tab on a
    /// rewritten note inside a moved folder already carries its new
    /// path.</summary>
    internal Action<IReadOnlyList<RewriteOutcome>>? LinksRewritten { get; set; }

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

        if (report.Rewritten.Length > 0)
        {
            LinksRewritten?.Invoke(report.Rewritten);
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

    /// <summary>Every structural verb begins here (codex round 3):
    /// the pending result details are PER-OPERATION — a leaked flag
    /// from an aborted op must never be misattributed to the next
    /// mutation's sentence.</summary>
    private void BeginStructuralResult()
    {
        _pendingRewriteFailureDetail = null;
        _pendingStoredPathPersistFailure = false;
    }

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

    /// <summary>Hold the status just written for the tree publication
    /// still to come — or for none. W7-7 (codex PR 2 round 2): a
    /// reassertion belongs to a pending publication, the refresh
    /// generation that has not yet published. A status written after
    /// that publication (the refresh still finishing) has nothing to
    /// guard against, and holding it let a LATER refresh resurrect it
    /// over whatever the status said by then; an older hold is
    /// superseded by this status either way.</summary>
    private void HoldStatusForPendingPublication()
    {
        _statusToReassert = IsTreePublicationPending ? Status : null;
        _statusToReassertWrittenByTree = _statusToReassert is not null && _statusWrittenByTree;
    }

    /// <summary>Plan 47 CB-16 (#1791, W9): whether the tree wrote the held line, so its
    /// reassertion keeps the mark (a held tag-tree failure stays the tree's own).</summary>
    private bool _statusToReassertWrittenByTree;

    /// <summary>The held line written back, with the mark it was held with.</summary>
    private void WriteReassertedStatus(string reassert) =>
        WriteStatus(reassert, _statusToReassertWrittenByTree);

    /// <summary>Plan 47 CB-16 (#1791, W9, Call 3): whether the tree wrote the
    /// line now shown: a publication's arm (the count, an overflow line, the
    /// settings notice, a silent tag-tree error), a folder expansion's or the
    /// dual pane's overflow line, a tag tree's failure or a tree failure's
    /// report. Set only through <see cref="WriteStatus"/> by
    /// <see cref="WriteTreeStatus"/>, <see cref="ReportTreeReadFailure"/>
    /// and a held line's reassertion; every other write clears it. A flag
    /// cleared by every other write is used rather than a record of the tree's
    /// last text, which would misfire whenever another writer wrote the same
    /// words.</summary>
    private bool _statusWrittenByTree;

    /// <summary>The transient line now shown ("Loading files…", "Filtering
    /// files…"), from <see cref="ShowTransientStatus"/> until a put-back; the
    /// line it replaced; and whether the tree wrote that line.</summary>
    private string? _transientStatus;

    private string _statusBeforeTransient = string.Empty;
    private bool _treeWroteStatusBeforeTransient;

    /// <summary>A line the tree writes: see <see cref="_statusWrittenByTree"/>.</summary>
    private void WriteTreeStatus(string text) => WriteStatus(text, writtenByTree: true);

    /// <summary>Plan 47 CB-16 (#1791, W9; Codex rounds 1 and 2): a read of the tree's
    /// content failed (the tree, the tag tree, a folder's children, the dual pane's files,
    /// Expand Loaded): said and shown as <see cref="ReportFailure(string, bool)"/> says it,
    /// and the tree's own line, marked before it is held for a pending publication, so no
    /// refresh puts it back once a later read succeeds. A mutation's failure is not a
    /// read's, and keeps its line.</summary>
    private void ReportTreeReadFailure(string message) =>
        ReportFailure(message, writtenByTree: true);

    /// <summary>Plan 47 CB-16 (#1791, W9): show a line that says work is
    /// running, remembering the line it replaces and whether the tree wrote
    /// that line. When the line already shows a transient line, the memory
    /// it has stays: a refresh that replaces a running one puts back the line
    /// from before the first, never the first's "Loading files…".</summary>
    private void ShowTransientStatus(string text)
    {
        bool showsTransient = _transientStatus is string shown
            && string.Equals(Status, shown, StringComparison.Ordinal);
        if (!showsTransient)
        {
            _statusBeforeTransient = Status;
            _treeWroteStatusBeforeTransient = _statusWrittenByTree;
        }

        _transientStatus = text;
        Status = text;
    }

    /// <summary>Plan 47 CB-16 (#1791, W9): the work that showed
    /// <paramref name="text"/> ended with nothing of its own to say. Acts only
    /// while the line still shows that text, so a line written while the work
    /// ran wins (a mutation's result, a failure's detail, a count). It puts
    /// back the remembered line, or leaves the line empty when the tree wrote
    /// that line: an old count, overflow line, notice or failure is stale once
    /// the tree has moved, and putting back a count is the count CD10 forbids
    /// a plain refresh. An end that does not match leaves the memory alone;
    /// only a put-back clears it.</summary>
    private void EndTransientStatus(string text)
    {
        if (!string.Equals(_transientStatus, text, StringComparison.Ordinal)
            || !string.Equals(Status, text, StringComparison.Ordinal))
        {
            return;
        }

        string restored = _treeWroteStatusBeforeTransient ? string.Empty : _statusBeforeTransient;
        _transientStatus = null;
        _statusBeforeTransient = string.Empty;
        _treeWroteStatusBeforeTransient = false;
        Status = restored;
    }

    private void ReassertStatusAfterPublication()
    {
        if (_statusToReassert is string reassert)
        {
            WriteReassertedStatus(reassert);
            // An active filter's AUTOMATIC refilter is still coming
            // (this publication just scheduled it) and would stomp
            // Status after the debounce — leave the value for
            // ApplyFilterOutcome to consume (codex round 5).
            if (!IsFilterActive || !IsFiltering)
            {
                _statusToReassert = null;
            }
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
        HoldStatusForPendingPublication();

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
        // #1435: the same fix-ups the journal records as committed.
        foreach (SidebarJournalRename fixUp in StoredPathFixUps(report, oldPath, newPath))
        {
            TransformStoredPaths(
                fixUp.OldPath, fixUp.NewPath, isDirectory: false, deleted: false);
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
        if (IsTrashing) { return false; }
        if (SelectedNode is not FileTreeNodeViewModel node)
        {
            return false;
        }

        BeginStructuralResult();
        string oldPath = node.Path;
        string oldName = node.Name;
        string newName = MutationName;
        string newPath = CombineVaultPath(ParentPath(oldPath), newName);
        // Identity captured BEFORE the forward op (codex round 3): the
        // file ID survives the rename, and capturing after it left a
        // window where a replacement's identity got recorded as the
        // mutated object's.
        StructuralIdentity? identity = null;
        // #1435: the renames this implies — the entry and, for a folder,
        // its note under the new name — are journaled durably before core
        // can commit them.
        Guid? journalEntry = null;
        try
        {
            StructuralReport? report = null;
            if (!TryRunSessionWork(() =>
            {
                identity = TryCaptureStructuralIdentity(oldPath, node.IsDirectory);
                journalEntry = JournalBeforeStructuralCommit(
                    PlannedRename(oldPath, newPath, node.IsDirectory));
                report = node.IsDirectory
                    ? _session.RenameFolderWithNote(oldPath, newName)
                    : _session.RenameFile(oldPath, newName);
            }) || report is null)
            {
                return false;
            }

            JournalStructuralCommitted(
                journalEntry, CommittedMove(report, oldPath, newPath, node.IsDirectory));
            int rewritten = ConsumeStructuralReport(report);
            TransformStoredPaths(oldPath, newPath, node.IsDirectory, deleted: false);
            CorrectStoredPathsFromReport(report, oldPath, newPath);
            StoredPathsFollowed(journalEntry);
            PushIdentityCheckedStep(new StructuralUndoStep(
                StructuralUndoKind.Rename,
                Path: newPath,
                Argument: oldName,
                node.IsDirectory,
                Noun: oldName,
                Identity: identity));
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
            JournalStructuralFailed(journalEntry, exception);
            // F3: the field stays open with the reason inline — core's
            // message relayed, MutationName untouched.
            ReportFailure($"Rename failed: {SlateUniffiMethods.VaultErrorDetail(exception)}");
            return false;
        }
    }

    /// <summary>Structural undo (F10): pop, preflight, re-run the
    /// forward FFI with the inverse arguments, land the re-inverse on
    /// the redo stack. Empty-stack chords still speak (mac:
    /// "Nothing to undo.").</summary>
    internal void UndoStructural()
    {
        if (IsTrashing) { AnnounceUndoResidue("Trash is in progress. Try again when it finishes."); return; }
        if (!_structuralUndo.TryPopUndo(out StructuralUndoStep step))
        {
            AnnounceUndoResidue("Nothing to undo.");
            return;
        }

        ExecuteUndoStep(step, redo: false);
    }

    internal void RedoStructural()
    {
        if (IsTrashing) { AnnounceUndoResidue("Trash is in progress. Try again when it finishes."); return; }
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

    private StructuralIdentity? TryCaptureStructuralIdentity(string path, bool includeFolderNote)
    {
        try { return _session.CaptureStructuralIdentity(path, includeFolderNote); }
        catch (VaultException) { return null; }
    }

    private void PushIdentityCheckedStep(StructuralUndoStep step)
    {
        if (StepIdentitiesStillHold(step)) { _structuralUndo.Push(step); }
        else { StructuralHistoryBarrier(); }
    }

    private bool StepIdentitiesStillHold(StructuralUndoStep step)
    {
        try
        {
            return TryRunSessionWork(() =>
            {
                if (step.Kind == StructuralUndoKind.BatchMove)
                {
                    return step.BatchIdentities is not null
                        && _session.StructuralIdentitiesMatch(step.BatchIdentities);
                }
                if (step.Identity is null) { return false; }
                StructuralIdentity current = _session.CaptureStructuralIdentity(
                    step.Path, step.Kind == StructuralUndoKind.Rename && step.IsDirectory);
                return current.Entry == step.Identity.Entry && current.FolderNote == step.Identity.FolderNote;
            }, out bool holds) && holds;
        }
        catch (VaultException) { return false; }
    }

    private void ExecuteUndoStep(StructuralUndoStep step, bool redo)
    {
        BeginStructuralResult();
        string verb = redo ? "Redid" : "Undid";
        // Core validates and renames one opened object. Missing identities must
        // refuse rather than degrade to an existence-only preflight.
        if ((step.Kind == StructuralUndoKind.BatchMove && step.BatchIdentities is null)
            || (step.Kind != StructuralUndoKind.BatchMove && step.Identity is null))
        {
            _structuralUndo.DropForChangedFiles();
            AnnounceUndoResidue(redo
                ? "Can't redo — the files have changed."
                : "Can't undo — the files have changed.");
            return;
        }

        // #1435: the inverse's renames are journaled durably before core
        // can commit them, as a fresh rename's or move's are.
        Guid? journalEntry = null;
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
                        journalEntry = JournalBeforeStructuralCommit(
                            PlannedRename(step.Path, restoredPath, step.IsDirectory));
                        StructuralReport renamed = step.IsDirectory
                            ? _session.RenameFolderWithNoteIfIdentity(step.Path, step.Argument, step.Identity!)
                            : _session.RenameFileIfIdentity(step.Path, step.Argument, step.Identity!.Entry);
                        JournalStructuralCommitted(
                            journalEntry, CommittedMove(renamed, step.Path, restoredPath, step.IsDirectory));
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
                        journalEntry = JournalBeforeStructuralCommit(
                            [new SidebarJournalRename(step.Path, movedPath, step.IsDirectory)]);
                        StructuralReport movedReport = step.IsDirectory
                            ? _session.MoveFolderIfIdentity(step.Path, step.Argument, step.Identity!.Entry)
                            : _session.MoveFileIfIdentity(step.Path, step.Argument, step.Identity!.Entry);
                        JournalStructuralCommitted(
                            journalEntry, CommittedMove(movedReport, step.Path, movedPath, step.IsDirectory));
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
                        // The edge moves every recorded change back from its
                        // new path.
                        journalEntry = JournalBeforeStructuralCommit(BatchInverse(step.BatchChanges));
                        batchReport = _session.UndoBatchMoveIfIdentities(step.BatchOpId, step.BatchIdentities!);
                        JournalStructuralCommitted(journalEntry, BatchStanding(batchReport.Standing));
                        foreach (BatchPathChange change in batchReport.Standing)
                        {
                            RetargetRequested?.Invoke(change.OldPath, change.NewPath);
                            TransformStoredPaths(
                                change.OldPath, change.NewPath,
                                change.IsDirectory, deleted: false);
                        }

                        if (batchReport.Rewritten.Length > 0)
                        {
                            LinksRewritten?.Invoke(batchReport.Rewritten);
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
                            inverse = step with
                            {
                                BatchOpId = inverseOpId,
                                BatchIdentities = batchReport.Standing.ToDictionary(
                                    change => change.NewPath,
                                    change => step.BatchIdentities![change.OldPath],
                                    StringComparer.Ordinal),
                                BatchChanges = batchReport.Standing,
                            };
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

            StoredPathsFollowed(journalEntry);

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

            // The re-inverse carries the SAME identity — the file ID
            // survives the undo's rename/move, and the validated
            // pre-op capture is the honest token (re-reading at the
            // new path would reopen the push-time window codex round
            // 3 closed).
            if (inverse.Kind is not StructuralUndoKind.BatchMove)
            {
                inverse = inverse with { Identity = step.Identity };
            }

            // Our own atomic link rewrites can replace a renamed entry. Never
            // adopt the ID now found at its pathname; discard invalid history.
            if (!StepIdentitiesStillHold(inverse))
            {
                StructuralHistoryBarrier();
            }
            else if (redo)
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
        catch (VaultException.StructuralMutationIncomplete exception)
        {
            JournalStructuralOutcomeUnknown(journalEntry);
            _structuralUndo.DropForChangedFiles();
            RequestSelectionAt(exception.path);
            Refresh();
            ReportMutationResult($"{(redo ? "Redo" : "Undo")} stopped. {exception.message}");
        }
        catch (VaultException exception)
        {
            // #1435: a single inverse that refused committed nothing; a
            // batch edge that threw says nothing of what committed.
            if (step.Kind == StructuralUndoKind.BatchMove)
            {
                JournalStructuralOutcomeUnknown(journalEntry);
            }
            else
            {
                JournalStructuralFailed(journalEntry, exception);
            }

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

    /// <summary>A create's rename arm: an intent (Plan 47 CB-39) that drops a
    /// waiting Reveal and a pending row (WB-6, Codex round 4: kept, the Reveal
    /// would take the selection from the new row and open its own
    /// note).</summary>
    private void RequestInlineRenameAt(string vaultPath) =>
        InstallIntent(IntentKind.RenameArm, vaultPath, TakeIntent());

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
    /// <remarks>Plan 47 CB-39: an intent, a named row or (null) one that names
    /// no row, which drops a waiting Reveal and an older pending row.</remarks>
    /// <param name="intentAtGesture">For a mutation that awaited between its
    /// gesture and its request (a trash, whose work runs on a worker behind its
    /// progress bar): the number it took at its gesture. If a newer intent was
    /// taken since (a Reveal asked during its work), it installs nothing and
    /// arms no focus restore (Codex round 7). Null for a mutation that requests
    /// in its gesture.</param>
    private void RequestSelectionAt(string? vaultPath, long? intentAtGesture = null)
    {
        long number = intentAtGesture ?? TakeIntent();
        if (number != _intentsTaken)
        {
            return;
        }

        InstallIntent(vaultPath is null ? IntentKind.NoRow : IntentKind.NamedRow, vaultPath, number);
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
            // WB-6 (Codex round 1): the named successor takes the keys too,
            // when they are in the tree. The publication keeps the row they
            // were on now (a duplicate's original), so they no longer fall to
            // the selection on their own; a successor the tree hides hands
            // them to the collapsed row holding it.
            _vanishedSelection = new VanishedSelection(ShownRowHolding(fresh, RootNodes), Successor: true);
        }

        TreeSelectionRestored?.Invoke();
    }

    /// <summary>Every publication reconciles the selection (codex
    /// round 3): a selection left pointing at a node the tree no longer
    /// holds made destructive verbs target whatever now lives at that path.
    /// WB-6 (#1515): the one resolver, on both publication paths, and the
    /// last selection step — after a mutation's named successor is seated,
    /// so the keys move once. With nothing selected before the publication,
    /// or the selection still in the tree, there is nothing to do. A row of
    /// the same key the publication rebuilt — its date group changed, the
    /// grouping changed, its folder was rebuilt — is re-selected silently,
    /// and the keys land on it, or on the collapsed row now holding it
    /// (<see cref="ShownRowHolding"/>). Only a row that is gone is
    /// disarmed.</summary>
    /// <remarks>W7-7 PR 7 (#1252, R-9 over R-5), codex's merge-delta check
    /// (finding 1): a selected row that VANISHED — deleted or moved outside
    /// Slate, published by a rescan — took the keys with it, and WPF strands
    /// them on the window when the focused row unloads. The disarm raises the
    /// guarded tree restore (<see cref="TreeSelectionRestored"/>) with the row
    /// to land on: the nearest one that survived
    /// (<see cref="NearestSurvivor"/>), focused without selecting it — R-5's
    /// focus-without-select, since a selection would open the note.</remarks>
    /// <param name="place">The sidebar's selection's place, read before the
    /// publication.</param>
    /// <param name="treeRow">The tree's own selected row's place when it is
    /// not the sidebar's selection (Codex round 1): a delete let go of the
    /// selection before its refresh, or the dual pane holds it. Gone, that
    /// row hands the keys to its nearest survivor, and the selection is left
    /// as it is.</param>
    private void ReconcileSelectionAfterPublication(SelectionPlace? place, SelectionPlace? treeRow)
    {
        if (place is not null && !(SelectedNode is { } current && InTree(current)))
        {
            if (RowWithKey(RootNodes, place.SelectedKey, string.Empty) is { } replacement)
            {
                // WB-6 (S2): a list's selection (the publication replaced the
                // tree for it) is re-bound by path on both halves, exactly as
                // main did. WPF focuses the container it selects while the
                // keys are in the tree, so they follow it, as on main, and the
                // tree's own row records no second landing.
                SelectSilently(replacement, reseatOf: place.SelectedPath);
                if (place.InTree)
                {
                    RecordSelectionLanding(ShownRowHolding(replacement, RootNodes));
                }

                return;
            }
            else
            {
                if (SetField(ref _selectedNode, null, nameof(SelectedNode)))
                {
                    RaiseCommandStates();
                }

                if (place.InTree)
                {
                    FileTreeNodeViewModel? survivor = NearestSurvivor(place);
                    if (survivor is not null || treeRow is null)
                    {
                        RecordSelectionLanding(survivor);
                        return;
                    }
                }
            }
        }

        // The tree's own row, when it was not the selection: a row of the
        // same key the publication rebuilt takes the keys (unselected — the
        // selection is the list's), else its nearest survivor does.
        if (treeRow is not null && !InTree(treeRow.Row))
        {
            RecordSelectionLanding(
                RowWithKey(RootNodes, treeRow.SelectedKey, string.Empty) is { } rebuilt
                    ? ShownRowHolding(rebuilt, RootNodes)
                    : NearestSurvivor(treeRow));
        }
    }

    private bool InTree(FileTreeNodeViewModel row) =>
        Flatten(RootNodes).Any(node => ReferenceEquals(node, row));

    /// <summary>Records where the keys go after a publication moved the
    /// selected row — <paramref name="landing"/>, or the tree's own landing
    /// for null — and raises the tree's restore once. The window lands them
    /// there only when they were on a row the publication removed.</summary>
    private void RecordSelectionLanding(FileTreeNodeViewModel? landing)
    {
        _vanishedSelection = new VanishedSelection(landing);
        TreeSelectionRestored?.Invoke();
    }

    /// <summary>WB-6: the selected row's place, read before a publication
    /// moves anything — its key and path, and the keys of the rows its keys
    /// would land on if it is gone, nearest first: its next siblings in
    /// order, then its previous ones nearest first, then its parent, and the
    /// same around the parent, up to the root.</summary>
    /// <param name="InTree">Whether the published tree held the row: a
    /// list's row (the filter results, the dual pane) has no place in
    /// it.</param>
    private sealed record SelectionPlace(
        FileTreeNodeViewModel Row,
        string SelectedKey,
        string SelectedPath,
        IReadOnlyList<string> CandidateKeys,
        bool InTree);

    /// <summary><paramref name="selected"/>'s <see cref="SelectionPlace"/> in
    /// the published tree; null with nothing selected.</summary>
    private SelectionPlace? SnapshotSelectionPlace(FileTreeNodeViewModel? selected)
    {
        if (selected is null)
        {
            return null;
        }

        var ancestry = new List<(IList<FileTreeNodeViewModel> Level, FileTreeNodeViewModel Row, string FolderPath)>();
        if (!Find(RootNodes, string.Empty))
        {
            return new SelectionPlace(selected, RowKey(selected, string.Empty), selected.Path, [], InTree: false);
        }

        var candidates = new List<string>();
        for (int depth = ancestry.Count - 1; depth >= 0; depth--)
        {
            (IList<FileTreeNodeViewModel> level, FileTreeNodeViewModel row, string folderPath) = ancestry[depth];
            int index = level.IndexOf(row);
            foreach (FileTreeNodeViewModel sibling in level.Skip(index + 1).Concat(level.Take(index).Reverse()))
            {
                if (!sibling.IsPlaceholder)
                {
                    candidates.Add(RowKey(sibling, folderPath));
                }
            }

            if (depth > 0)
            {
                candidates.Add(RowKey(ancestry[depth - 1].Row, ancestry[depth - 1].FolderPath));
            }
        }

        (_, FileTreeNodeViewModel self, string selfFolder) = ancestry[^1];
        return new SelectionPlace(selected, RowKey(self, selfFolder), selected.Path, candidates, InTree: true);

        bool Find(IList<FileTreeNodeViewModel> level, string folderPath)
        {
            foreach (FileTreeNodeViewModel row in level)
            {
                ancestry.Add((level, row, folderPath));
                if (ReferenceEquals(row, selected) || Find(row.Children, row.IsDirectory ? row.Path : folderPath))
                {
                    return true;
                }

                ancestry.RemoveAt(ancestry.Count - 1);
            }

            return false;
        }
    }

    /// <summary>WB-6: a row's key across the whole tree — a file's or a
    /// folder's path; a date group's header, whose path is empty, by the
    /// folder it is in (<paramref name="folderPath"/>, empty at the root) and
    /// its label, as the reconcile keys it within its level; a placeholder
    /// (the loading, error and overflow rows) by its folder and its text, so
    /// a selected one is re-seated on its fresh row (the red team's L3). A
    /// placeholder is never a candidate for the keys.</summary>
    private static string RowKey(FileTreeNodeViewModel row, string folderPath) => row switch
    {
        { IsPlaceholder: true } => folderPath + "\u0000placeholder:" + row.Name,
        { IsGroupHeader: true } => folderPath + LevelKey(row),
        _ => row.Path,
    };

    /// <summary>The row anywhere in the tree, shown or not, whose
    /// <see cref="RowKey"/> is <paramref name="key"/>.</summary>
    private static FileTreeNodeViewModel? RowWithKey(
        IEnumerable<FileTreeNodeViewModel> level,
        string key,
        string folderPath)
    {
        foreach (FileTreeNodeViewModel row in level)
        {
            if (RowKey(row, folderPath) == key)
            {
                return row;
            }

            if (RowWithKey(row.Children, key, row.IsDirectory ? row.Path : folderPath) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>The row the reader sees holding <paramref name="row"/>: the
    /// outermost collapsed row above it, or the row itself when every
    /// ancestor is open. A note that moved into a date group the reader
    /// collapsed lands the keys on that group's header, and the group stays
    /// collapsed.</summary>
    private static FileTreeNodeViewModel ShownRowHolding(
        FileTreeNodeViewModel row,
        IEnumerable<FileTreeNodeViewModel> roots)
    {
        var path = new List<FileTreeNodeViewModel>();
        if (!PathTo(roots, row, path))
        {
            return row;
        }

        return path.Take(path.Count - 1).FirstOrDefault(ancestor => !ancestor.IsExpanded) ?? row;

        static bool PathTo(
            IEnumerable<FileTreeNodeViewModel> level,
            FileTreeNodeViewModel target,
            List<FileTreeNodeViewModel> path)
        {
            foreach (FileTreeNodeViewModel node in level)
            {
                path.Add(node);
                if (ReferenceEquals(node, target) || PathTo(node.Children, target, path))
                {
                    return true;
                }

                path.RemoveAt(path.Count - 1);
            }

            return false;
        }
    }

    /// <summary>A selected row a publication removed or rebuilt, and the row
    /// its keys land on — null for the tree's own landing (its first row, or
    /// the empty tree, the region's stop). A mutation's named successor
    /// (<paramref name="Successor"/>) takes the keys wherever they are in the
    /// tree; any other landing only when they were on a row the publication
    /// removed.</summary>
    /// <param name="Reveal">Plan 47 CB-39: a Reveal's row, landed before it is
    /// selected; it takes the keys from wherever they are outside a modal
    /// surface.</param>
    /// <param name="RevealAsked">Plan 47 CB-39: not a landing but a Reveal's
    /// gesture asking where the keys are. The window lands nothing and answers
    /// in <see cref="Place"/>.</param>
    /// <param name="LateReveal">Plan 47 CB-39: not a landing but a question
    /// from a Reveal that seats after its gesture's own turn. The window lands
    /// nothing and answers in <see cref="Answer"/>.</param>
    /// <param name="GesturePlace">The place the window recorded at that
    /// Reveal's gesture.</param>
    internal sealed record VanishedSelection(
        FileTreeNodeViewModel? Survivor,
        bool Successor = false,
        bool Reveal = false,
        bool RevealAsked = false,
        bool LateReveal = false,
        object? GesturePlace = null)
    {
        /// <summary>Plan 47 CB-39: the window's answer to a Reveal's gesture:
        /// where the keys are, as a place it can compare later.</summary>
        public object? Place { get; set; }

        /// <summary>Plan 47 CB-39: the window's answer to a late
        /// Reveal.</summary>
        public LateRevealAnswer Answer { get; set; }
    }

    /// <summary>Plan 47 CB-39: where a Reveal seating after its gesture's own
    /// turn may put the keys.</summary>
    internal enum LateRevealAnswer
    {
        /// <summary>The keys are where the gesture left them, stranded, or in
        /// the Files tree: the seat lands them, opens and speaks.</summary>
        Land,

        /// <summary>The keys are the reader's elsewhere, or Files is hidden:
        /// the row is selected silently.</summary>
        SelectSilently,

        /// <summary>The keys are in the Files pane's fields, lists or buttons:
        /// the wait ends, and nothing changes.</summary>
        End,
    }

    private VanishedSelection? _vanishedSelection;

    /// <summary>The last publication's vanished selection, taken once — by
    /// the window's restore, which lands the keys there only when they were
    /// on a row that publication removed. The next publication drops one
    /// nobody took.</summary>
    internal VanishedSelection? TakeVanishedSelection()
    {
        VanishedSelection? taken = _vanishedSelection;
        _vanishedSelection = null;
        return taken;
    }

    /// <summary>The published row nearest the selected row's place, read
    /// before the publication (<paramref name="place"/>): its next siblings
    /// in order, then its previous ones nearest first, then its parent — and,
    /// when none of those survived either, the parent's own nearest survivor.
    /// Matched by <see cref="RowKey"/>, and only a row the published tree
    /// SHOWS (every ancestor expanded) — one under a collapsed folder or
    /// group cannot take the keys. A surviving date group's header can
    /// (WB-6): under Group by date a row's parent is its group. Null when
    /// nothing survived.</summary>
    private FileTreeNodeViewModel? NearestSurvivor(SelectionPlace place)
    {
        var shown = new Dictionary<string, FileTreeNodeViewModel>(StringComparer.Ordinal);
        Collect(RootNodes, string.Empty);
        foreach (string key in place.CandidateKeys)
        {
            if (shown.TryGetValue(key, out FileTreeNodeViewModel? survivor))
            {
                return survivor;
            }
        }

        return null;

        void Collect(IEnumerable<FileTreeNodeViewModel> level, string folderPath)
        {
            foreach (FileTreeNodeViewModel row in level)
            {
                if (!row.IsPlaceholder)
                {
                    _ = shown.TryAdd(RowKey(row, folderPath), row);
                }

                if (row.IsExpanded)
                {
                    Collect(row.Children, row.IsDirectory ? row.Path : folderPath);
                }
            }
        }
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

        SelectOnBothHalves(created);
        InlineRenameRequested?.Invoke();
    }

    /// <summary>WB-6 (Codex round 1): a mutation's or a surface's named row
    /// takes the selection through the setter — it opens and announces as a
    /// selection does, and every other selected tree row lets go of it (S1)
    /// — and on both halves.</summary>
    private void SelectOnBothHalves(FileTreeNodeViewModel node)
    {
        // Plan 47 CB-39: the sidebar's own seat, not the reader's selection.
        bool outer = _seatingOwnSelection;
        _seatingOwnSelection = true;
        try
        {
            SelectedNode = node;
            node.IsSelected = true;
        }
        finally
        {
            _seatingOwnSelection = outer;
        }
    }

    // ---- The Move-To picker (F4) ---------------------------------

    private MoveToPickerViewModel? _moveToSheet;

    /// <summary>The Move-To picker sheet, or null when closed. The
    /// window observes this for present/dismiss (the template-sheet
    /// shape) and the modal-surface state reads it.</summary>
    public MoveToPickerViewModel? MoveToSheet
    {
        get => _moveToSheet;
        private set
        {
            if (ReferenceEquals(_moveToSheet, value)) { return; }
            CancelMoveToLoading();
            // Plan 47 CB-17b (#1866, D5): the sheet closes before its picker is retired, so
            // its Cancel has left the screen, and its command binding with it, before the
            // retired picker turns that command off; it never goes off under the keys.
            MoveToPickerViewModel? retiring = _moveToSheet;
            SetField(ref _moveToSheet, value);
            retiring?.Retire();
        }
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

        if (SessionShutdownStarted || MoveToSheet is not null
            || MoveToOpenAdmission?.Invoke() == false)
        {
            return;
        }

        // Plan 47 CB-39: opening Move To is the reader's gesture on the
        // selected (or checked) rows, a newer intent that names no row, as F2
        // and an open are: a Reveal still waiting ends, so it never seats
        // under the sheet or after it. The selection stays: it is the move's
        // own subject, and a confirm's request names the moved row with a
        // newer number.
        _ = StateIntentNamingNoRow();

        var illegalParents = new HashSet<string>(
            items.Select(item => ParentPath(item.Path)), StringComparer.Ordinal);
        string[] movingFolders = [.. items
            .Where(item => item.IsDirectory)
            .Select(item => item.Path)];
        var knownFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var illegalParentsTyped = new HashSet<string>(
            illegalParents, StringComparer.OrdinalIgnoreCase);
        bool IsLegal(string folder) => !illegalParents.Contains(folder)
            && !movingFolders.Any(moving => folder == moving
                || folder.StartsWith(moving + "/", StringComparison.Ordinal));

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
        MoveToPickerViewModel? picker = null;
        picker = new MoveToPickerViewModel(
            [],
            rootIsLegal: !illegalParents.Contains(string.Empty),
            itemNoun: noun,
            confirmed: destination => ExecuteMoveTo(items, destination),
            createAndMove: path => CreateFolderThenMove(items, path),
            cancelled: () => { if (ReferenceEquals(MoveToSheet, picker)) { MoveToSheet = null; } },
            newFolderPathAllowed: NewFolderPathAllowed,
            announce: _announce,
            loading: true,
            retry: () => StartMoveToLoading(picker!, knownFolders, IsLegal),
            ownsPresentation: () => OwnsMoveToPresentation(picker!));
        MoveToSheet = picker;
        StartMoveToLoading(picker, knownFolders, IsLegal);
        // W0.5-3 residue: Move-To presentation copy.
        _announce(new A11yEvent.HostComposed(
            $"Move {noun}: loading destination folders. Escape to cancel.", A11yPriority.Medium));
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

    /// <summary>F4 execution: one item rides the single-entry FFIs
    /// with report consumption; multiple ride <c>BatchMove</c> with
    /// its one summary. Destination "" speaks "vault root".</summary>
    private void ExecuteMoveTo(StructuralBatchItem[] items, string destination)
    {
        BeginStructuralResult();
        MoveToSheet = null;
        if (items.Length == 1)
        {
            StructuralBatchItem single = items[0];
            string leaf = System.IO.Path.GetFileName(single.Path);
            string movedPath = CombineVaultPath(destination, leaf);
            // Identity captured BEFORE the forward op (codex round 3).
            StructuralIdentity? identity = null;
            // #1435: journaled durably before core can commit the move.
            Guid? journalEntry = null;
            try
            {
                StructuralReport? report = null;
                if (!TryRunSessionWork(() =>
                {
                    identity = TryCaptureStructuralIdentity(single.Path, includeFolderNote: false);
                    journalEntry = JournalBeforeStructuralCommit(
                        [new SidebarJournalRename(single.Path, movedPath, single.IsDirectory)]);
                    report = single.IsDirectory
                        ? _session.MoveFolder(single.Path, destination)
                        : _session.MoveFile(single.Path, destination);
                }) || report is null)
                {
                    return;
                }

                JournalStructuralCommitted(
                    journalEntry, CommittedMove(report, single.Path, movedPath, single.IsDirectory));
                int rewritten = ConsumeStructuralReport(report);
                TransformStoredPaths(single.Path, movedPath, single.IsDirectory, deleted: false);
                CorrectStoredPathsFromReport(report, single.Path, movedPath);
                StoredPathsFollowed(journalEntry);
                PushIdentityCheckedStep(new StructuralUndoStep(
                    StructuralUndoKind.Move,
                    Path: movedPath,
                    Argument: ParentPath(single.Path),
                    single.IsDirectory,
                    Noun: leaf,
                    Identity: identity));
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
                JournalStructuralFailed(journalEntry, exception);
                // A create-then-move whose move half refused must not
                // leave the created folder invisible until the next
                // organic refresh (red team, correctness 4) — and the
                // failure leg reconciles focus too (verification 2).
                // The failure reports AFTER Refresh so its reason
                // survives the "Loading files…" write.
                RequestSelectionAt(null);
                Refresh();
                ReportFailure($"Move failed: {FailureDetail.Of(exception)}");
            }

            return;
        }

        BatchMove(items, destination);
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
            ReportFailure($"Could not create folder {folderPath}: {FailureDetail.Of(exception)}");
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
            string? caveat = null;
            foreach (string candidate in DuplicateCandidates(node.Path))
            {
                try
                {
                    // #1123: a post-publish failure is a LANDED copy —
                    // never the namer's advance signal.
                    if (!TryRunSessionWork(
                        () => CreateOutcomes.CreateReporting(
                            _session, candidate, text, LeafName(candidate)),
                        out caveat))
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
            // Refresh so it survives the "Loading files…" write. A
            // landed-but-unindexed copy has no node to land on yet.
            if (caveat is null)
            {
                RequestSelectionAt(created);
            }
            else
            {
                // Plan 47 CB-39: a duplicate that names no row is still a
                // gesture, newer than a waiting Reveal.
                _ = StateIntentNamingNoRow();
            }

            Refresh();
            ReportResult(
                $"Duplicated {node.DisplayName} as {LeafName(created)}.");
            if (caveat is not null)
            {
                ReportFailure(caveat);
            }
        }
        catch (VaultException exception)
        {
            ReportFailure(
                $"Could not duplicate {node.DisplayName}: {FailureDetail.Of(exception)}");
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

        if (_vaultRoot is not string vaultRoot)
        {
            return;
        }

        string absolute = AbsoluteVaultPath(vaultRoot, node.Path);
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

    private static string AbsoluteVaultPath(string vaultRoot, string vaultRelative) =>
        System.IO.Path.Combine(
            vaultRoot, vaultRelative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    private void AnnounceUndoResidue(string message)
    {
        Status = message;
        // W0.5-3 residue: structural undo announcements (mac's #871
        // strings are host-composed there too — F11).
        _announce(new A11yEvent.HostComposed(message, A11yPriority.Medium));
    }
}
