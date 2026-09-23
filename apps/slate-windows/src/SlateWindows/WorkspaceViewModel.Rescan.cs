// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using ICSharpCode.AvalonEdit.Document;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 7 (#1252, R-9): the workspace's half of a rescan's
/// reconciliation. <see cref="VaultLifecycleViewModel"/> walks core's delta
/// pages and applies, path by path, the operations its file-change funnel
/// applies — through the seams below, never through the file-change event
/// channel, which stays Slate-owned writes only (locked decision
/// <c>05_locked_architecture_decisions.md:338-342</c>).
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    private int _silentReconciliationDepth;

    /// <summary>True while a rescan reconciles its delta.</summary>
    internal bool IsReconcilingSilently => _silentReconciliationDepth > 0;

    /// <summary>
    /// Run the funnel's operations silently for the scope's lifetime.
    /// <see cref="InvalidatePath(string)"/>'s "missing from disk" line is
    /// host-composed residue that must not speak during a reconciliation —
    /// an open file's external deletion, and a case-only rename's removal
    /// before its creation, speak only the rescan's one core-rendered
    /// completion sentence.
    /// </summary>
    internal IDisposable BeginSilentReconciliation()
    {
        _silentReconciliationDepth++;
        return new SilentReconciliationScope(this);
    }

    /// <summary>
    /// The clean-tab reload seam a rescan's Modified entry reconciles
    /// through (the funnel's Modified arm only marks staleness,
    /// <see cref="InvalidateModifiedPath"/>). EVERY clean Markdown tab on
    /// the path reloads — the workspace mirrors same-path documents
    /// edit-by-edit with cross-document offsets, so reloading one and
    /// leaving a stale peer arms a divergence (the W5-3 verification
    /// finding) — adopting the new saved hash as its baseline with
    /// <c>IsDirty</c> false, a fresh undo stack so no undo restores the
    /// pre-rescan bytes, its caret line kept where the new text allows,
    /// and a reading-mode tab re-projected. A DIRTY tab keeps its buffer and
    /// its stale baseline, marked externally stale exactly as the funnel's
    /// Modified arm marks it.
    /// </summary>
    public void ReloadCleanTab(string path)
    {
        string modified = NormalizeWorkspacePath(path);
        if (modified.Length == 0)
        {
            return;
        }

        bool reloaded = false;
        foreach (WorkspaceTabViewModel tab in Groups
            .SelectMany(group => group.Tabs)
            .Where(candidate => candidate.IsMarkdown
                && string.Equals(candidate.Path, modified, StringComparison.Ordinal))
            .ToList())
        {
            if (tab.IsDirty)
            {
                tab.InvalidateExternalState();
                tab.RefreshExternalStaleness();
                continue;
            }

            tab.ReloadKeepingCaretLine();
            reloaded = true;
        }

        if (reloaded)
        {
            // The whole-document refresh a clean re-baseline owes its
            // panels (the MirrorSamePathDocumentState precedent): the
            // outline, tasks and citations re-read the new bytes.
            NotePersisted(modified);
            TasksReview.NoteRefreshed(modified);
        }
    }

    /// <summary>
    /// The reconciliation's batch form of <see cref="InvalidatePath(string)"/>
    /// for one page's removals: the same per-path operations — open tabs
    /// marked missing (their buffers kept), closed-tab history and the
    /// Connections stack pruned — with ONE command-state refresh and ONE
    /// workspace persist for the page instead of one per path. Silent by
    /// contract: only a reconciliation scope may call it.
    /// </summary>
    internal void InvalidatePaths(IReadOnlyCollection<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        if (!IsReconcilingSilently)
        {
            throw new InvalidOperationException(
                "Batch invalidation belongs to a rescan's silent reconciliation.");
        }

        foreach (string path in paths)
        {
            string invalidated = NormalizeWorkspacePath(path);
            if (invalidated.Length > 0)
            {
                _ = InvalidatePathWithoutPersisting(invalidated);
            }
        }

        RaiseCommandStates();
        Persist();
    }

    /// <summary>The per-path half <see cref="InvalidatePath(string)"/> and
    /// <see cref="InvalidatePaths"/> share. Returns how many open tabs the
    /// path invalidated.</summary>
    private int InvalidatePathWithoutPersisting(string invalidated)
    {
        int affected = 0;
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (IsPathBacked(tab.Item) && IsSameOrDescendantPath(tab.Path, invalidated))
            {
                tab.InvalidatePath();
                affected++;
            }
        }

        _closedTabs.RemoveAll(entry =>
            IsPathBacked(entry.Item) && IsSameOrDescendantPath(entry.Item.Path, invalidated));
        // W6-2 PR B2 (rule D, the delete hook; B2D-9): the leaf's stack
        // entries under the deleted path are pruned, so Back never opens a
        // note that is gone; the pin and the note in view are kept (the
        // Error presentation, B1's delete route).
        Connections.Prune(invalidated);
        return affected;
    }

    private sealed class SilentReconciliationScope(WorkspaceViewModel owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner._silentReconciliationDepth--;
        }
    }
}

internal sealed partial class WorkspaceTabViewModel
{
    /// <summary>
    /// W7-7 PR 7 (R-9): re-read the file through the in-place replace the
    /// template and history reloads use — the new saved hash becomes the
    /// baseline (<see cref="IsDirty"/> stays false), a fresh editor session
    /// means no undo restores the pre-rescan bytes, a reading-mode tab
    /// re-projects — and keep the caret's LINE where the new text allows,
    /// clamping line and column otherwise. The caret lands through the
    /// bound offset, never a caret-navigation request, which would take
    /// keyboard focus into the editor from wherever the user is.
    /// </summary>
    internal void ReloadKeepingCaretLine()
    {
        TextDocument? before = EditorDocument;
        TextLocation caret = before is null
            ? new TextLocation(1, 1)
            : before.GetLocation(Math.Clamp(EditorCaretOffset, 0, before.TextLength));
        ReplaceItem(Item);
        if (EditorDocument is TextDocument after)
        {
            int line = Math.Clamp(caret.Line, 1, after.LineCount);
            DocumentLine target = after.GetLineByNumber(line);
            int column = Math.Clamp(caret.Column, 1, target.Length + 1);
            EditorCaretOffset = target.Offset + column - 1;
        }
    }
}
