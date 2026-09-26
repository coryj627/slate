// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using ICSharpCode.AvalonEdit.Document;
using SlateWindows.Bases;
using SlateWindows.Canvas;

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

    /// <summary>Test seam (W7-7 PR 7, round 28): wraps each per-kind reload
    /// a rescan awaits — (kind, path, the real reload) to the Task the
    /// reconciliation awaits — so a fact can park one or fail it.</summary>
    internal Func<string, string, Func<Task>, Task>? RescanKindReloadForTests { get; set; }

    /// <summary>
    /// W7-7 PR 7 (rounds 27-28): a rescan's Modified row, per tab kind, as ONE
    /// awaitable. The funnel's Modified arm is Markdown-only and the canvas
    /// registry serves a cached document, so an externally modified open
    /// board would stay stale while the tree, Quick Open and the sentence
    /// all report the change. Every kind reads on a worker and publishes on
    /// the dispatcher: clean Markdown tabs reload from text
    /// <paramref name="readOnWorker"/> read (the rescan core seam, its cancel
    /// token checked there), an open canvas document re-reads its file
    /// through the registry, an open base on the path reopens its
    /// definition and re-runs its view. The Task completes only when every
    /// kind has PUBLISHED, and faults when any read or publication fails —
    /// the rescan marks the page applied only after it.
    /// </summary>
    /// <remarks>
    /// EVERY clean Markdown tab on the path reloads — the workspace mirrors
    /// same-path documents edit-by-edit with cross-document offsets, so
    /// reloading one and leaving a stale peer arms a divergence (the W5-3
    /// verification finding) — adopting the new saved hash as its baseline
    /// with <c>IsDirty</c> false, a fresh undo stack so no undo restores the
    /// pre-rescan bytes, its caret line kept where the new text allows, and
    /// a reading-mode tab re-projected. A DIRTY tab keeps its buffer and its
    /// stale baseline, marked externally stale against the index hash the
    /// worker read. No tab on the path reloads while a Slate-owned save to it
    /// is in flight (a property write's note lease, or a task toggle on any
    /// same-path tab): that save's own completion re-baselines the tab (round
    /// 24). And none reloads when a Slate-owned write to the path was
    /// journaled while the worker read (<paramref name="reconciledSince"/>):
    /// that write already reconciled it.
    /// </remarks>
    internal async Task ReconcileModifiedPathAsync(
        string path,
        Func<Func<(string Text, string IndexedHash)>, Task<(string Text, string IndexedHash)>> readOnWorker,
        Func<bool> reconciledSince)
    {
        string modified = NormalizeWorkspacePath(path);
        if (modified.Length == 0)
        {
            return;
        }

        var reloads = new List<Task>();
        if (Groups.SelectMany(group => group.Tabs).Any(tab => IsMarkdownTabAt(tab, modified)))
        {
            reloads.Add(RunKindReload(
                "markdown", modified, () => ReloadMarkdownTabsAsync(modified, readOnWorker, reconciledSince)));
        }

        if (_canvasDocuments.TryGetValue(CanvasKey(modified), out CanvasDocumentViewModel? canvas))
        {
            reloads.Add(RunKindReload("canvas", modified, canvas.ReloadAsync));
        }

        foreach (BaseDocumentViewModel document in OpenBaseDocumentsAt(modified))
        {
            reloads.Add(RunKindReload("base", modified, document.LoadAsync));
        }

        await Task.WhenAll(reloads);
    }

    private Task RunKindReload(string kind, string path, Func<Task> reload) =>
        RescanKindReloadForTests is { } seam ? seam(kind, path, reload) : reload();

    private static bool IsMarkdownTabAt(WorkspaceTabViewModel tab, string path) =>
        tab.IsMarkdown && string.Equals(tab.Path, path, StringComparison.Ordinal);

    private IEnumerable<BaseDocumentViewModel> OpenBaseDocumentsAt(string path)
    {
        var seen = new HashSet<BaseDocumentViewModel>(ReferenceEqualityComparer.Instance);
        foreach (BaseDocumentViewModel document in _baseDocuments.Values)
        {
            if (!document.IsSavedQuery
                && string.Equals(document.Path, path, StringComparison.Ordinal)
                && seen.Add(document))
            {
                yield return document;
            }
        }

        if (BasesDockDocument is { IsSavedQuery: false } dock
            && string.Equals(dock.Path, path, StringComparison.Ordinal)
            && seen.Add(dock))
        {
            yield return dock;
        }
    }

    private async Task ReloadMarkdownTabsAsync(
        string modified,
        Func<Func<(string Text, string IndexedHash)>, Task<(string Text, string IndexedHash)>> readOnWorker,
        Func<bool> reconciledSince)
    {
        (string text, string indexedHash) = await readOnWorker(() =>
            (_session.ReadText(modified), _session.NoteTasks(modified, 1).ContentHash));

        // Back on the dispatcher: the tabs are read afresh — one may have
        // closed, turned dirty or started a save while the worker read.
        if (reconciledSince())
        {
            return;
        }

        List<WorkspaceTabViewModel> tabs =
            [.. Groups.SelectMany(group => group.Tabs).Where(tab => IsMarkdownTabAt(tab, modified))];
        bool saveInFlight = PropertyWriteInFlightFor(modified)
            || tabs.Any(tab => tab.IsTaskToggleInFlight);
        bool reloaded = false;
        foreach (WorkspaceTabViewModel tab in tabs)
        {
            if (tab.IsDirty || saveInFlight)
            {
                tab.InvalidateExternalState();
                tab.ApplyExternalStaleness(indexedHash);
                continue;
            }

            tab.ReloadKeepingCaretLine(text);
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
                InvalidatePath(invalidated, persist: false);
            }
        }

        RaiseCommandStates();
        Persist();
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
    /// W7-7 PR 7 (R-9, round 28): replace the tab in place from the text a
    /// worker read — the template and history reloads' replace — so the new
    /// saved hash becomes the
    /// baseline (<see cref="IsDirty"/> stays false), a fresh editor session
    /// means no undo restores the pre-rescan bytes, a reading-mode tab
    /// re-projects — and keep the caret's LINE where the new text allows,
    /// clamping line and column otherwise. The caret lands through the
    /// bound offset, never a caret-navigation request, which would take
    /// keyboard focus into the editor from wherever the user is.
    /// </summary>
    internal void ReloadKeepingCaretLine(string text)
    {
        TextDocument? before = EditorDocument;
        TextLocation caret = before is null
            ? new TextLocation(1, 1)
            : before.GetLocation(Math.Clamp(EditorCaretOffset, 0, before.TextLength));
        ReplaceItemWithReadText(Item, text);
        if (EditorDocument is TextDocument after)
        {
            int line = Math.Clamp(caret.Line, 1, after.LineCount);
            DocumentLine target = after.GetLineByNumber(line);
            int column = Math.Clamp(caret.Column, 1, target.Length + 1);
            EditorCaretOffset = target.Offset + column - 1;
        }
    }

    /// <summary>W7-7 PR 7 (round 28): <see cref="RefreshExternalStaleness"/>
    /// with the index hash already read on a worker, so no core read runs
    /// on the dispatcher.</summary>
    internal void ApplyExternalStaleness(string indexedHash)
    {
        if (!IsMarkdown || _disposed)
        {
            return;
        }

        IsExternallyStale = indexedHash.Length > 0
            && !string.Equals(SavedContentHash, indexedHash, StringComparison.Ordinal);
    }
}
