// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 7 (#1252, R-9, round 28): the host-side effects of a Slate-owned
/// file-change event (<see cref="HandleFileChange"/>, the contract-05
/// channel, which stays Slate-owned writes only). A change made OUTSIDE
/// Slate never arrives here: on Windows it reaches the host only through a
/// rescan's re-sync from the index (<c>VaultLifecycleViewModel.Rescan.cs</c>,
/// AR-18's fallback).
/// </summary>
internal sealed partial class VaultLifecycleViewModel
{
    /// <summary>
    /// The effects, in the funnel's order: first each change's primary
    /// effect — a rename retargets its tabs; a removal invalidates its path
    /// (speaking its "missing from disk" line); a Modified change marks
    /// staleness; a creation or rename re-seats missing tabs once — then the
    /// dependents: the editor interaction caches, Quick Open, the reading
    /// models (each applies its own reverse-dependency filter, so a note's
    /// embedders re-render), the Bases surfaces, the history panel for a
    /// Modified path, and the graph probe.
    /// </summary>
    private void ApplyFileChangeEffects(IReadOnlyList<(FileChangeEvent Change, bool Openable)> changes)
    {
        WorkspaceViewModel? workspace = Workspace;
        if (changes.Count == 0)
        {
            return;
        }

        // --- the primary effects ------------------------------------------------
        foreach ((FileChangeEvent change, _) in changes)
        {
            switch (change.Kind)
            {
                case FileChangeKind.Renamed when change.PreviousPath is string previousPath:
                    workspace?.RetargetPath(previousPath, change.Path);
                    break;
                case FileChangeKind.Deleted:
                    workspace?.InvalidatePath(change.Path);
                    break;
                case FileChangeKind.Modified:
                    workspace?.InvalidateModifiedPath(change.Path);
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
        QuickSwitcher?.ApplyFileChanges(changes);
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
    }
}
