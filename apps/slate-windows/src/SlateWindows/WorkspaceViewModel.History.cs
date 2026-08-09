// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W4-7 (#739): the history coordinator — the mac AppState+History
/// twin. The HistoryViewModel owns the data; THIS layer owns the
/// flows that touch tabs, dialogs, and announcements: reveal
/// (contract H11), restore (H7), Restore As… (H9), deleted-file
/// recovery (H10), the since-open preference mirror (H8), and the
/// compaction-failure relay (H13). Every write goes through core
/// (HINV-7) and runs off the dispatcher (HINV-6).
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    /// <summary>PUBLIC, not internal: the leaf body's
    /// Model="{Binding History}" resolves through WPF reflection,
    /// which only sees public properties — an internal property
    /// fails SILENTLY and the surface renders nothing (the recorded
    /// W4-4 lesson; caught by the journey's first live run).</summary>
    public HistoryViewModel History { get; }

    /// <summary>Injectable confirmation/refusal dialogs (the W4-4/W4-6
    /// seam pattern): production shows dialogs; facts inject. The
    /// confirmation's buttons are the PINNED "Cancel" / "Restore"
    /// (contract H7 — the mac alert's roles), which MessageBox cannot
    /// label, hence the dedicated dialog.</summary>
    internal Func<string, bool> HistoryRestoreConfirmation { get; set; } =
        message => HistoryConfirmationDialog.Confirm(
            "Restore version?", message, confirmLabel: "Restore");

    /// <summary>(title, message) — the titles are mac's: "Restore
    /// failed" for the restore flow, "Can't restore" for recovery and
    /// Restore As… failures.</summary>
    internal Action<string, string> HistoryAlert { get; set; } =
        (title, message) => System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);

    /// <summary>The surface's action seams, installed once (the Bases
    /// InstallBaseDocumentSeams pattern) — called from the workspace
    /// ctor right after History is constructed.</summary>
    internal void InstallHistorySeams()
    {
        History.RestoreFromSurface = RequestRestoreVersion;
        History.RestoreAsFromSurface = CommitRestoreAsVersion;
        History.RecoverFromSurface = RecoverDeletedFile;
        History.RecoverAsFromSurface = CommitRestoreAsDeleted;
    }

    /// <summary>slate.history.showPanel (contract H11): un-hide the
    /// pane, activate the leaf, announce the canonical event ONLY on
    /// an actual switch (the mac rule; the setter's generic
    /// LeafPanelShown pairs with it — the W4-3 precedent), and move
    /// focus to the right-pane boundary.</summary>
    public void ShowHistoryPanel()
    {
        WorkspaceLeafOption leaf = Leaves.First(
            option => string.Equals(option.Id, "history", StringComparison.Ordinal));
        bool switching = !string.Equals(
            ActiveLeaf?.Id, "history", StringComparison.Ordinal);
        if (!IsRightPaneVisible)
        {
            IsRightPaneVisible = true;
        }
        ActiveLeaf = leaf;
        if (switching)
        {
            _announce(new A11yEvent.HistoryPanelShown());
        }
        else
        {
            // The reveal refresh is idempotent (H11): a switch rode
            // the ActiveLeaf setter's history hook; a re-invoke on the
            // already-active leaf refreshes here.
            History.Reload();
        }
        FocusBoundaryRequested?.Invoke(this, WorkspaceFocusBoundary.RightPane);
    }

    private System.Windows.Input.ICommand? _showHistoryPanelCommand;

    public System.Windows.Input.ICommand ShowHistoryPanelCommand =>
        _showHistoryPanelCommand ??= new RelayCommand(
            _ => ShowHistoryPanel(), _ => true);

    /// <summary>The since-open host preference mirror (contract H8):
    /// the EditorPreferences VM persists it; the workspace threads the
    /// live value into the history document. Turning it ON takes
    /// effect at the next note activation (the mac rule).</summary>
    internal void SetHistoryShowChangesSinceOpen(bool enabled) =>
        History.ShowChangesSinceOpen = enabled;

    // --- Restore (contract H7) ---

    private sealed record HistoryRestoreRequest(
        string Path,
        string VersionHash,
        string FormattedDate,
        string? ExpectedContentHash);

    /// <summary>Stage + confirm + perform. Captures AT STAGE TIME
    /// (path, hash, date, expected hash — the mac capture-at-staging
    /// rule); a DIRTY open tab refuses with the D-14 sentence family
    /// before any disk touch (divergence HD-1).</summary>
    internal void RequestRestoreVersion(HistoryVersionRow row)
    {
        if (History.Path is not { } path)
        {
            return;
        }
        WorkspaceTabViewModel? tab = FindPathBackedTab(path);
        if (tab is { IsDirty: true })
        {
            _announce(new A11yEvent.HostComposed(
                // W0.5-3 residue: the D-14 refusal family, restore
                // verb (divergence HD-1).
                "Save the note before restoring a version. The editor has "
                + $"unsaved changes in {System.IO.Path.GetFileName(path)}.",
                A11yPriority.High));
            return;
        }
        // The CAS basis (H7): a markdown tab stages its loaded buffer
        // hash (the mac currentNoteContentHash); every other
        // path-backed kind stages the loaded list's head hash — H12
        // forbids extension special-casing, and core documents a null
        // expected hash as an UNCONDITIONAL save, so no restore may
        // ever go out unguarded (the mac guard refuses instead).
        string? expectedHash = tab is { IsMarkdown: true }
            ? tab.SavedContentHash
            : History.HeadContentHash;
        if (expectedHash is null)
        {
            return;
        }
        var request = new HistoryRestoreRequest(
            path,
            row.ContentHashAfter,
            row.AbsoluteDate,
            expectedHash);
        string filename = System.IO.Path.GetFileName(path);
        if (!HistoryRestoreConfirmation(
            $"Restore the version from {request.FormattedDate}? This replaces "
            + $"the current content of {filename}. The replaced state remains "
            + "available in version history."))
        {
            return;
        }
        PerformRestore(request);
    }

    private void PerformRestore(HistoryRestoreRequest request)
    {
        if (_workspaceDisposed)
        {
            return;
        }
        RunHistoryWork(() =>
        {
            string? failure = null;
            bool conflict = false;
            bool unavailable = false;
            try
            {
                _ = _session.RestoreVersion(
                    request.Path, request.VersionHash, request.ExpectedContentHash);
            }
            catch (VaultException.WriteConflict)
            {
                conflict = true;
            }
            catch (VaultException.HistoryUnavailable)
            {
                unavailable = true;
            }
            catch (VaultException exception)
            {
                failure = exception.Message;
            }
            RunOnDispatcher(() =>
            {
                if (_workspaceDisposed)
                {
                    return;
                }
                if (conflict)
                {
                    HistoryAlert(
                        "Restore failed",
                        "The file changed after history was loaded. The list "
                        + "will reload — try again.");
                    History.Reload();
                    return;
                }
                if (unavailable)
                {
                    HistoryAlert(
                        "Restore failed",
                        "This version can't be restored: its history failed an "
                        + "integrity check.");
                    return;
                }
                if (failure is not null)
                {
                    HistoryAlert("Restore failed", failure);
                    return;
                }
                _announce(new A11yEvent.RestoredVersionFrom(request.FormattedDate));
                ReloadOpenTabFromDisk(request.Path);
                // A restore IS a save (core fires Modified): route the
                // ONE post-save funnel so tasks/links/citations panels
                // never show the pre-restore snapshot (red team round
                // 1) — it reloads the history list too.
                NotePersisted(request.Path);
                History.RequestFocusHead();
            });
        });
    }

    /// <summary>The restore landed on disk; a CLEAN open tab reloads
    /// its buffer wholesale (the mac loadCurrentNote shape — the
    /// existing in-place replace path; contract H7's verification
    /// flag records that it also resets transient tab state, as mac's
    /// reload does).</summary>
    private void ReloadOpenTabFromDisk(string path)
    {
        WorkspaceTabViewModel? tab = FindPathBackedTab(path);
        if (tab is { IsDirty: false })
        {
            tab.ReplaceItem(tab.Item);
            AttachBaseDocumentIfNeeded(tab);
        }
    }

    private WorkspaceTabViewModel? FindPathBackedTab(string path) =>
        Groups.SelectMany(group => group.Tabs).FirstOrDefault(candidate =>
            IsPathBacked(candidate.Item)
            && string.Equals(candidate.Path, path, StringComparison.Ordinal));

    // --- Restore As… (contract H9) ---

    /// <summary>The seeded destination for the inline row: "{stem}
    /// (restored).{ext}" — collisions surface through the
    /// exclusive-create refusal rather than a pre-check (HD-2).</summary>
    internal static string SuggestedRestoreCopyPath(string path)
    {
        string directory = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/')
            ?? string.Empty;
        string stem = System.IO.Path.GetFileNameWithoutExtension(path);
        string extension = System.IO.Path.GetExtension(path);
        if (extension.Length == 0)
        {
            extension = ".md";
        }
        string name = $"{stem} (restored){extension}";
        return directory.Length == 0 ? name : $"{directory}/{name}";
    }

    /// <summary>Restore a LIVE version to a new path: verified
    /// VersionContent + CreateExclusive (no live restore-as FFI — the
    /// mac composition). The identity arrives STAGED (hash + date
    /// captured when the row opened — never the row occupying that
    /// position now). The completion runs on the dispatcher with
    /// (ok, refusalMessage): a refusal keeps the inline row open.</summary>
    internal void CommitRestoreAsVersion(
        string versionHash,
        string formattedDate,
        string destination,
        Action<bool, string?> completed)
    {
        if (History.Path is not { } path || _workspaceDisposed)
        {
            completed(false, null);
            return;
        }
        string trimmed = destination.Trim();
        if (trimmed.Length == 0)
        {
            completed(false, "Enter a destination path.");
            return;
        }
        RunHistoryWork(() =>
        {
            string? refusal = null;
            string? failure = null;
            try
            {
                string content = _session.VersionContent(path, versionHash);
                _ = _session.CreateExclusive(trimmed, content);
            }
            catch (VaultException.DestinationExists)
            {
                refusal = $"A file already exists at {trimmed}. Choose a "
                    + "different name.";
            }
            catch (VaultException exception)
            {
                failure = exception.Message;
            }
            RunOnDispatcher(() =>
            {
                if (_workspaceDisposed)
                {
                    completed(false, null);
                    return;
                }
                if (refusal is not null)
                {
                    completed(false, refusal);
                    return;
                }
                if (failure is not null)
                {
                    HistoryAlert("Can't restore", failure);
                    completed(false, null);
                    return;
                }
                _announce(new A11yEvent.RestoredFileAs(
                    $"version from {formattedDate}",
                    System.IO.Path.GetFileName(trimmed)));
                completed(true, null);
                OpenPath(trimmed);
            });
        });
    }

    // --- Deleted-file recovery (contract H10) ---

    /// <summary>Recovery to the ORIGINAL path is primary; a
    /// DestinationExists routes into the inline Restore As… row (the
    /// view drives that through the completion).</summary>
    internal void RecoverDeletedFile(
        HistoryDeletedRow row, Action<bool, bool> completed)
    {
        if (_workspaceDisposed || !row.Recoverable)
        {
            completed(false, false);
            return;
        }
        string path = row.Path;
        RunHistoryWork(() =>
        {
            bool destinationExists = false;
            string? failure = null;
            try
            {
                _ = _session.RecoverDeletedFile(path);
            }
            catch (VaultException.DestinationExists)
            {
                destinationExists = true;
            }
            catch (VaultException exception)
            {
                failure = exception.Message;
            }
            RunOnDispatcher(() =>
            {
                if (_workspaceDisposed)
                {
                    completed(false, false);
                    return;
                }
                if (destinationExists)
                {
                    completed(false, true);
                    return;
                }
                if (failure is not null)
                {
                    HistoryAlert("Can't restore", failure);
                    completed(false, false);
                    return;
                }
                _announce(new A11yEvent.RestoredFile(
                    System.IO.Path.GetFileName(path)));
                // Announce + refresh ONLY (the mac shape): recovery
                // deliberately does NOT navigate — selection stays in
                // the Deleted list so recovering several files in a
                // row is not disruptive (red team round 1). Restore
                // As… is the flow that opens the new file (H9).
                History.ReloadDeletedFilesIfLoaded();
                completed(true, false);
            });
        });
    }

    /// <summary>Recover a deleted file to a caller-chosen destination
    /// (the collision path — reachable only through
    /// DestinationExists).</summary>
    internal void CommitRestoreAsDeleted(
        string sourcePath, string destination, Action<bool, string?> completed)
    {
        if (_workspaceDisposed)
        {
            completed(false, null);
            return;
        }
        string trimmed = destination.Trim();
        if (trimmed.Length == 0)
        {
            completed(false, "Enter a destination path.");
            return;
        }
        RunHistoryWork(() =>
        {
            string? refusal = null;
            string? failure = null;
            try
            {
                _ = _session.RecoverDeletedFileAs(sourcePath, trimmed);
            }
            catch (VaultException.DestinationExists)
            {
                refusal = $"A file already exists at {trimmed}. Choose a "
                    + "different name.";
            }
            catch (VaultException exception)
            {
                failure = exception.Message;
            }
            RunOnDispatcher(() =>
            {
                if (_workspaceDisposed)
                {
                    completed(false, null);
                    return;
                }
                if (refusal is not null)
                {
                    completed(false, refusal);
                    return;
                }
                if (failure is not null)
                {
                    HistoryAlert("Can't restore", failure);
                    completed(false, null);
                    return;
                }
                _announce(new A11yEvent.RestoredFileAs(
                    System.IO.Path.GetFileName(sourcePath),
                    System.IO.Path.GetFileName(trimmed)));
                History.ReloadDeletedFilesIfLoaded();
                completed(true, null);
                OpenPath(trimmed);
            });
        });
    }

    /// <summary>History work runs off the dispatcher in production
    /// (HINV-6: log-length-proportional, uncancellable) and inline in
    /// synchronous test mode; TRACKED into the bounded teardown drain
    /// (the codex-round-5/6 lifecycle rule).</summary>
    private void RunHistoryWork(Action body)
    {
        if (!_startInteractionBackgroundWork)
        {
            body();
            return;
        }
        TrackRetiredBasesWork(Task.Run(body));
    }

    /// <summary>HR-2's vault-event arm: an external (or non-editor)
    /// write to the ACTIVE path appends a version row the NotePersisted
    /// funnel never sees — Bases dock grid edits, sync landings,
    /// out-of-app editors. Same-path guarded and generation-guarded in
    /// the VM; a self-save's Modified echo costs one extra guarded
    /// reload (accepted — the funnel has no echo discriminator).</summary>
    internal void NotifyHistoryOfVaultChange(string path) =>
        History.NoteSaved(path);

    // --- Compaction relay (contract H13, divergence HD-4) ---

    private readonly HashSet<string> _compactionAnnouncedPaths =
        new(StringComparer.Ordinal);

    /// <summary>Core's composed message relayed as a Medium
    /// announcement, once per path per session (the W0.5-3 residue
    /// class: a core string relayed, never host copy).</summary>
    internal void AnnounceHistoryCompactionFailure(string path, string message)
    {
        if (_compactionAnnouncedPaths.Add(path))
        {
            _announce(new A11yEvent.HostComposed(message, A11yPriority.Medium));
        }
    }
}
