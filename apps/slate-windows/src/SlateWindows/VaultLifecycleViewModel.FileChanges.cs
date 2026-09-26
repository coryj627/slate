// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 7 (#1252, R-9, round 28): the host-side effects of file changes,
/// ONE routine for both of their sources — a Slate-owned file-change event
/// (<see cref="HandleFileChange"/>, the contract-05 channel, which stays
/// Slate-owned writes only) and a rescan's delta page
/// (<see cref="ReconcileScanDeltaAsync"/>, the only way an external change
/// reaches the host on Windows). Every dependent a change owes is notified
/// here and nowhere else, so neither source can drift from the other.
/// </summary>
internal sealed partial class VaultLifecycleViewModel
{
    /// <summary>Where a batch of file changes came from.</summary>
    private enum FileChangeOrigin
    {
        /// <summary>A Slate-owned write's own event: its tab is already
        /// current, so a Modified change marks staleness; a deletion
        /// speaks its "missing from disk" line.</summary>
        SlateOwned,

        /// <summary>A rescan page: a change made outside Slate, so each open
        /// tab kind reloads (awaited); removals are batched and silent (the
        /// run speaks one sentence).</summary>
        Rescan,
    }

    /// <summary>
    /// The effects, in the funnel's order: first each change's primary
    /// effect — a rename retargets its tabs; a removal invalidates its path;
    /// a Modified change marks staleness (Slate-owned) or reloads every open
    /// tab kind on the path (rescan: Markdown from a worker's read, the
    /// canvas and base documents through their own workers); a creation or
    /// rename re-seats missing tabs once — then the dependents: the editor
    /// interaction caches, the reading models (each applies its own
    /// reverse-dependency filter, so a note's embedders re-render), the
    /// Bases surfaces, the graph probe, the history panel for a Modified
    /// path, and Quick Open. The returned Task completes when every rescan
    /// reload has PUBLISHED (a Slate-owned batch completes at once) and
    /// faults when any read or publication fails.
    /// </summary>
    private Task ApplyFileChangeEffectsAsync(
        IReadOnlyList<(FileChangeEvent Change, bool Openable)> changes,
        FileChangeOrigin origin,
        Func<string, bool>? reconciledSince = null,
        CancelToken? cancel = null)
    {
        WorkspaceViewModel? workspace = Workspace;
        if (changes.Count == 0)
        {
            return Task.CompletedTask;
        }

        // --- the primary effects ------------------------------------------------
        if (origin == FileChangeOrigin.Rescan)
        {
            // Removals before creations: a missing tab's file back under
            // another spelling (`ghost.md` → `Ghost.md`) is re-seated only
            // once its removal has marked the tab missing.
            workspace?.InvalidatePaths(
                [.. changes.Where(c => c.Change.Kind == FileChangeKind.Deleted).Select(c => c.Change.Path)]);
        }

        var reloads = new List<Task>();
        foreach ((FileChangeEvent change, _) in changes)
        {
            switch (change.Kind)
            {
                case FileChangeKind.Renamed when change.PreviousPath is string previousPath:
                    workspace?.RetargetPath(previousPath, change.Path);
                    break;
                case FileChangeKind.Deleted when origin == FileChangeOrigin.SlateOwned:
                    workspace?.InvalidatePath(change.Path);
                    break;
                case FileChangeKind.Modified when origin == FileChangeOrigin.SlateOwned:
                    workspace?.InvalidateModifiedPath(change.Path);
                    break;
                case FileChangeKind.Modified when workspace is not null:
                    string modified = change.Path;
                    reloads.Add(workspace.ReconcileModifiedPathAsync(
                        modified,
                        work => ReadForRescanAsync(work, cancel),
                        () => reconciledSince?.Invoke(modified) == true));
                    break;
            }
        }

        // #1077 (contract I6): a Created or Renamed publication may be a
        // missing tab's file coming back under ANOTHER spelling (`Ghost.md`
        // → `ghost.md` on NTFS); re-seat those tabs once, here, rather than
        // re-litigating identity per comparison.
        if (changes.Any(c => c.Change.Kind is FileChangeKind.Created or FileChangeKind.Renamed))
        {
            workspace?.ReseatMissingTabs();
        }

        // --- the dependents -------------------------------------------------------
        workspace?.InvalidateAllInteractionStates();
        foreach ((FileChangeEvent change, _) in changes)
        {
            // Reading embed cards depend on OTHER files (W3-5): the change
            // reaches every open reading model, which applies its own
            // reverse-dependency filter. A rename notifies both sides.
            workspace?.NotifyReadingOfVaultChange(change.Kind, change.Path);
            // Bases surfaces re-execute on vault changes (contract C9's
            // vault-event arm).
            workspace?.NotifyBasesOfVaultChange(change.Path);
            if (change.Kind == FileChangeKind.Renamed && change.PreviousPath is string renamedFrom)
            {
                workspace?.NotifyReadingOfVaultChange(change.Kind, renamedFrom);
                workspace?.NotifyBasesOfVaultChange(renamedFrom);
            }

            // W4-7 (HR-2's vault-event arm): a Modified on the active path
            // appended a version row the save funnel never saw.
            if (change.Kind == FileChangeKind.Modified)
            {
                workspace?.NotifyHistoryOfVaultChange(change.Path);
            }
        }

        // W6-2 PR A (contract A-3): the graph's generation probe, while a
        // graph tab is visible, and the Connections leaf's.
        workspace?.NotifyGraphOfVaultChange();
        QuickSwitcher?.ApplyFileChanges(changes);
        return reloads.Count == 0 ? Task.CompletedTask : Task.WhenAll(reloads);
    }

    /// <summary>A rescan reload's read: through the rescan core seam, off the
    /// dispatcher, refused once the run is cancelled.</summary>
    private Task<T> ReadForRescanAsync<T>(Func<T> work, CancelToken? cancel) =>
        RunRescanCoreAsync("read", () =>
            cancel?.IsCancelled() == true ? throw new VaultException.Cancelled() : work());
}
