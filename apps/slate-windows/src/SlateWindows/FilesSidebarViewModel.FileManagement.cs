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

    // W5-4 Phase B verbs (assigned with the other commands in the
    // main-file constructor).
    public System.Windows.Input.ICommand DuplicateCommand { get; }

    public System.Windows.Input.ICommand CopyPathCommand { get; }

    public System.Windows.Input.ICommand RevealCommand { get; }

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
            return;
        }

        SelectedNode = created;
        InlineRenameRequested?.Invoke();
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
            ReportResult(
                $"Duplicated {node.DisplayName} as {LeafName(created)}.");
            Refresh();
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
