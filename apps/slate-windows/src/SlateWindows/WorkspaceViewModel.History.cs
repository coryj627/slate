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
    internal HistoryViewModel History { get; }

    /// <summary>Injectable confirmation/refusal dialogs (the W4-4/W4-6
    /// seam pattern): production shows message boxes; facts inject.</summary>
    internal Func<string, bool> HistoryRestoreConfirmation { get; set; } =
        message => System.Windows.MessageBox.Show(
            message,
            "Restore version?",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning)
            == System.Windows.MessageBoxResult.Yes;

    internal Action<string> HistoryAlert { get; set; } =
        message => System.Windows.MessageBox.Show(
            message,
            "History",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);

    /// <summary>Raised after a successful restore so the view moves
    /// focus to the NEW HEAD row (position 0 — WCAG 2.4.3).</summary>
    internal event Action? HistoryFocusHeadRequested;

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
        var request = new HistoryRestoreRequest(
            path,
            row.ContentHashAfter,
            row.AbsoluteDate,
            tab is { IsMarkdown: true } ? tab.SavedContentHash : null);
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
                        "The file changed after history was loaded. The list "
                        + "will reload — try again.");
                    History.Reload();
                    return;
                }
                if (unavailable)
                {
                    HistoryAlert(
                        "This version can't be restored: its history failed an "
                        + "integrity check.");
                    return;
                }
                if (failure is not null)
                {
                    HistoryAlert(failure);
                    return;
                }
                _announce(new A11yEvent.RestoredVersionFrom(request.FormattedDate));
                ReloadOpenTabFromDisk(request.Path);
                History.Reload();
                HistoryFocusHeadRequested?.Invoke();
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
    /// mac composition). The completion runs on the dispatcher with
    /// (ok, refusalMessage): a refusal keeps the inline row open.</summary>
    internal void CommitRestoreAsVersion(
        HistoryVersionRow row, string destination, Action<bool, string?> completed)
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
        string formattedDate = row.AbsoluteDate;
        string versionHash = row.ContentHashAfter;
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
                    HistoryAlert(failure);
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
                    HistoryAlert(failure);
                    completed(false, false);
                    return;
                }
                _announce(new A11yEvent.RestoredFile(
                    System.IO.Path.GetFileName(path)));
                History.ReloadDeletedFilesIfLoaded();
                completed(true, false);
                OpenPath(path);
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
                    HistoryAlert(failure);
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
