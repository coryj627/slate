// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

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

    /// <summary>Lifecycle-supplied retarget seam (F9): the report's
    /// <c>Moved</c> pairs retarget open tabs SYNCHRONOUSLY at the
    /// mutation site; the event-stream retarget stays wired and
    /// no-ops on already-retargeted tabs (idempotent by
    /// construction — it matches on the old path).</summary>
    internal Action<string, string>? RetargetRequested { get; set; }

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
            Status = $"Links in {report.Failed.Length} "
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
            _structuralUndo.Push(new StructuralUndoStep(
                StructuralUndoKind.Rename,
                Path: newPath,
                Argument: oldName,
                node.IsDirectory,
                Noun: oldName));
            ReportResult(WithLinksSuffix(
                $"Renamed {node.DisplayName} to {newName}.", rewritten));
            Refresh();
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
    /// target a path a barrier op now owns.</summary>
    private void StructuralHistoryBarrier() => _structuralUndo.Barrier();

    private void ExecuteUndoStep(StructuralUndoStep step, bool redo)
    {
        string verb = redo ? "Redid" : "Undid";
        // The executability preflight (mac: drop suspect history
        // rather than replay inverses against strangers). BatchMove
        // preflights core-side — UndoBatchMove refuses typed when the
        // latest journal row is not the batch.
        if (step.Kind is not StructuralUndoKind.BatchMove
            && !System.IO.File.Exists(AbsoluteVaultPath(step.Path))
            && !System.IO.Directory.Exists(AbsoluteVaultPath(step.Path)))
        {
            _structuralUndo.DropForChangedFiles();
            AnnounceUndoResidue(redo
                ? "Can't redo — the files have changed."
                : "Can't undo — the files have changed.");
            return;
        }

        try
        {
            StructuralUndoStep? inverse = null;
            var batchReport = default(BatchMoveReport);
            if (!TryRunSessionWork(() =>
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

                        inverse = step with
                        {
                            BatchOpId = batchReport.OpId ?? step.BatchOpId,
                        };
                        break;
                }
            }) || inverse is null)
            {
                // The shutdown lease refused; the step is consumed but
                // the vault is closing — nothing to restore.
                return;
            }

            if (redo)
            {
                _structuralUndo.PushUndoFromRedo(inverse);
            }
            else
            {
                _structuralUndo.PushRedo(inverse);
            }

            AnnounceUndoResidue(step.Kind switch
            {
                StructuralUndoKind.Rename => $"{verb} rename to {step.Argument}.",
                StructuralUndoKind.Move => $"{verb} move of {step.Noun}.",
                _ => $"{verb} move of {step.Noun}.",
            });
            Refresh();
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
