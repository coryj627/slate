// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using ICSharpCode.AvalonEdit.Document;
using SlateWindows.Bases;
using SlateWindows.Canvas;
using SlateWindows.Graph;
using SlateWindows.Panels;
using SlateWindows.Reading;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 7 (#1252, contract R-9; AR-18's fallback): the workspace's half of
/// a rescan's re-sync from the index. After every rescan the lifecycle reads,
/// on a worker, the index's content hash of every document the workspace
/// shows (<see cref="RescanPathsToHash"/>); the workspace compares, reloads
/// and marks — through the seams below, never through the file-change event
/// channel, which stays Slate-owned writes only (locked decision
/// <c>05_locked_architecture_decisions.md:338-342</c>). Nothing is retained
/// between runs: every run re-syncs from what the index holds now.
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    private int _silentReconciliationDepth;

    /// <summary>True while a rescan invalidates missing documents.</summary>
    internal bool IsReconcilingSilently => _silentReconciliationDepth > 0;

    /// <summary>
    /// Run the funnel's operations silently for the scope's lifetime.
    /// <see cref="InvalidatePath(string)"/>'s "missing from disk" line is
    /// host-composed residue that must not speak during a re-sync — an open
    /// file's external deletion, and a case-only rename's removal before its
    /// re-seat, speak only the rescan's one core-rendered completion
    /// sentence. The scope is held only around the synchronous invalidation,
    /// so a Slate-owned deletion handled while the re-sync awaits still
    /// speaks.
    /// </summary>
    internal IDisposable BeginSilentReconciliation()
    {
        _silentReconciliationDepth++;
        return new SilentReconciliationScope(this);
    }

    /// <summary>Test seam (W7-7 PR 7, rounds 28-29): wraps EVERY publication
    /// a rescan awaits — (kind, path, the real publication) to the Task the
    /// re-sync awaits — so a fact can park one or fail it. Kinds: the
    /// document kinds (markdown, canvas, base) and the dependents (reading,
    /// history, bases, graph).</summary>
    internal Func<string, string, Func<Task>, Task>? RescanPublicationForTests { get; set; }

    private Task RunKindReload(string kind, string path, Func<Task> reload) =>
        RescanPublicationForTests is { } seam ? seam(kind, path, reload) : reload();

    /// <summary>A reload that reports how many of its own operations failed
    /// (codex PR 7 round 4, finding 6) — a group's children, a path's tabs'
    /// projections — through the same seam.</summary>
    private async Task<ulong> RunCountedReload(string kind, string path, Func<Task<ulong>> reload)
    {
        if (RescanPublicationForTests is not { } seam)
        {
            return await reload();
        }

        ulong counted = 0;
        await seam(kind, path, async () => counted = await reload());
        return counted;
    }

    /// <summary>The index's view of one path, as the lifecycle read it on a
    /// worker: its content hash (null — no index row), and whether a file is
    /// on disk there under this spelling (asked only when there is no row:
    /// an unindexed file that exists — a dot-folder note, say — is never
    /// marked missing). <paramref name="ProbeFailed"/> (codex AR-18 review
    /// round 2, finding 7): that probe failed — the path is kept as present
    /// (never marked missing on a guess, contract I7) and the failure counts
    /// one error in the rescan's sentence.</summary>
    internal readonly record struct IndexedPath(string? Hash, bool OnDisk, bool ProbeFailed = false);

    /// <summary>A missing tab's path as the re-sync's worker probed it
    /// (codex AR-18 review round 2, finding 1): the spelling the filesystem
    /// stores (null — nothing there), whether that probe failed, and — for
    /// a Markdown path — the index's hash of the stored spelling and the
    /// worker's read of it. A clean tab re-seats only onto bytes whose hash
    /// is that index hash.</summary>
    internal readonly record struct ReseatProbe(
        string? Stored,
        bool Failed,
        string? IndexHash,
        RescanRead? Read);

    /// <summary>A worker's read of one Markdown path: its text and that
    /// text's content hash — the index's hash function, over the same bytes.</summary>
    internal readonly record struct RescanRead(string Text, string Hash);

    /// <summary>What the document re-sync did, for the dependents: how many
    /// operations failed, which open paths the index showed changed (a tab
    /// whose baseline, a board whose basis, a base whose definition hash
    /// differed), and the base documents it reopened.</summary>
    internal sealed record RescanDocumentsOutcome(
        ulong Failed,
        IReadOnlySet<string> ChangedOpenPaths,
        IReadOnlySet<BaseDocumentViewModel> ReopenedBases)
    {
        /// <summary>What the dependents see when the document re-sync
        /// itself failed (codex PR 7 round 3, finding 4): nothing changed,
        /// nothing reopened — every dependent still re-syncs.</summary>
        internal static RescanDocumentsOutcome Empty { get; } = new(
            0,
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<BaseDocumentViewModel>(ReferenceEqualityComparer.Instance));
    }

    /// <summary>
    /// The distinct vault paths a re-sync reads index hashes for: every
    /// path-backed open tab, every open canvas and file-backed base document
    /// (a docked base included), and the history panel's note.
    /// </summary>
    internal IReadOnlyList<string> RescanPathsToHash()
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (IsPathBacked(tab.Item) && NormalizeWorkspacePath(tab.Path) is { Length: > 0 } path)
            {
                _ = paths.Add(path);
            }
        }

        foreach (CanvasDocumentViewModel canvas in _canvasDocuments.Values)
        {
            if (NormalizeWorkspacePath(canvas.Path) is { Length: > 0 } path)
            {
                _ = paths.Add(path);
            }
        }

        foreach (BaseDocumentViewModel document in FileBackedBaseDocuments())
        {
            if (NormalizeWorkspacePath(document.Path) is { Length: > 0 } path)
            {
                _ = paths.Add(path);
            }
        }

        if (History.Path is string historyPath && NormalizeWorkspacePath(historyPath) is { Length: > 0 } shown)
        {
            _ = paths.Add(shown);
        }

        return [.. paths.Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// W7-7 PR 7 (#1252, R-9): re-sync every open document from the index's
    /// hashes <paramref name="indexed"/>. In order: a path-backed tab whose
    /// file has no index row and nothing on disk is marked missing —
    /// silently; missing tabs whose file is back (under its own spelling or
    /// another) re-seat (#1077); then, each awaited to its publication, a
    /// Markdown path whose index hash differs from a tab's baseline is read
    /// once on a worker and applied per tab (<see cref="ReloadMarkdownPathAsync"/>),
    /// a board whose published basis differs — or that shows a load error —
    /// reloads, and a base whose definition's hash differs reopens. The
    /// returned count is the operations that failed.
    /// </summary>
    internal async Task<RescanDocumentsOutcome> ReSyncOpenDocumentsAsync(
        IReadOnlyDictionary<string, IndexedPath> indexed,
        Func<string, Task<RescanRead?>> readOnWorker,
        Func<IReadOnlyList<(string Path, bool Markdown)>, Task<IReadOnlyDictionary<string, ReseatProbe>>> probeReseatOnWorker,
        CancellationToken cancellation)
    {
        // A new run registers its own work for the close's drain; what an
        // earlier run registered stays only while it is still running.
        CompleteRescanWork();

        // Finding 7: a path whose spelling probe failed was kept as present
        // and counts one error.
        ulong failed = (ulong)indexed.Values.Count(entry => entry.ProbeFailed);
        string[] missing =
        [
            .. Groups.SelectMany(group => group.Tabs)
                .Where(tab => IsPathBacked(tab.Item) && !tab.IsMissingFromDisk)
                .Select(tab => NormalizeWorkspacePath(tab.Path))
                .Where(path => indexed.TryGetValue(path, out IndexedPath entry)
                    && entry.Hash is null
                    && !entry.OnDisk)
                .Distinct(StringComparer.Ordinal),
        ];
        if (missing.Length > 0)
        {
            using (BeginSilentReconciliation())
            {
                InvalidatePaths(missing);
            }
        }

        // The boards and bases the re-seat attached and loaded (codex PR 7
        // round 4, finding 1): loaded once — the passes below and the Bases
        // dependent skip them.
        var reseated = new HashSet<object>(ReferenceEqualityComparer.Instance);
        failed += await ReseatMissingTabsAsync(probeReseatOnWorker, reseated, cancellation);

        var changed = new HashSet<string>(StringComparer.Ordinal);
        var reopened = new HashSet<BaseDocumentViewModel>(
            reseated.OfType<BaseDocumentViewModel>(),
            ReferenceEqualityComparer.Instance);
        var work = new List<Task>();
        foreach (IGrouping<string, WorkspaceTabViewModel> group in Groups.SelectMany(group => group.Tabs)
            .Where(tab => tab.IsMarkdown && !tab.IsMissingFromDisk)
            .GroupBy(tab => NormalizeWorkspacePath(tab.Path), StringComparer.Ordinal))
        {
            if (!indexed.TryGetValue(group.Key, out IndexedPath entry) || entry.Hash is not string indexHash)
            {
                continue;
            }

            WorkspaceTabViewModel[] tabs = [.. group];
            if (tabs.All(tab => string.Equals(tab.SavedContentHash, indexHash, StringComparison.Ordinal)))
            {
                // Current: re-derive the staleness mark (a tab re-baselined
                // since an earlier mark clears it).
                foreach (WorkspaceTabViewModel tab in tabs)
                {
                    tab.ApplyExternalStaleness(indexHash);
                }

                continue;
            }

            string path = group.Key;
            _ = changed.Add(path);
            TabTicket[] tickets = [.. tabs.Select(TabTicket.Capture)];
            work.Add(RunCountedReload(
                "markdown",
                path,
                () => ReloadMarkdownPathAsync(path, indexHash, tickets, readOnWorker, cancellation)));
        }

        foreach (CanvasDocumentViewModel canvas in _canvasDocuments.Values.Distinct())
        {
            string path = NormalizeWorkspacePath(canvas.Path);
            if (reseated.Contains(canvas)
                || !indexed.TryGetValue(path, out IndexedPath entry)
                || entry.Hash is not string indexHash)
            {
                continue;
            }

            bool stale = canvas.State switch
            {
                CanvasLoadState.Ready => !string.Equals(canvas.PublishedBasis, indexHash, StringComparison.Ordinal),
                // Finding 8's rule for a board: a load error recovers on an
                // unchanged Refresh.
                CanvasLoadState.ParseError or CanvasLoadState.Failed => true,
                _ => false,
            };
            if (stale)
            {
                _ = changed.Add(path);
                work.Add(RunKindReload("canvas", path, () =>
                {
                    Task reload = canvas.ReloadAsync(cancellation);
                    TouchRescanScheduler(canvas);
                    return reload;
                }));
            }
        }

        foreach (BaseDocumentViewModel document in FileBackedBaseDocuments())
        {
            string path = NormalizeWorkspacePath(document.Path);
            if (reseated.Contains(document)
                || !indexed.TryGetValue(path, out IndexedPath entry)
                || entry.Hash is not string indexHash
                || string.Equals(document.LoadedDefinitionHash, indexHash, StringComparison.Ordinal))
            {
                continue;
            }

            _ = changed.Add(path);
            _ = reopened.Add(document);
            work.Add(RunKindReload("base", path, () =>
            {
                Task reopen = document.LoadAsync(cancellation);
                TouchRescanScheduler(document);
                return reopen;
            }));
        }

        failed += await CountRescanFailuresAsync(work, cancellation);
        return new RescanDocumentsOutcome(failed, changed, reopened);
    }

    /// <summary>
    /// W7-7 PR 7 (#1252, R-9; codex AR-18 review round 2, finding 1): the
    /// re-sync's re-seat of missing tabs (#1077). Every core call — the
    /// stored spelling, the index's hash of it, the read of it — runs on
    /// the rescan worker with the run's token; back on the dispatcher, in
    /// ONE turn, a tab re-seats only on its ticket (the same tab, item and
    /// still missing): a dirty tab takes the stored spelling as its
    /// identity and keeps its buffer (contract I8); a clean Markdown tab,
    /// unedited since the probe, re-seats onto the worker's bytes only when
    /// their hash is the index's hash of the stored spelling — a tab never
    /// shows bytes the index does not vouch for; any other clean Markdown
    /// tab stays missing and counts one error (the next rescan re-seats
    /// it); a tab of another kind re-seats as before. A failed probe keeps
    /// the tab missing and counts one error. Returns the errors.
    /// </summary>
    private async Task<ulong> ReseatMissingTabsAsync(
        Func<IReadOnlyList<(string Path, bool Markdown)>, Task<IReadOnlyDictionary<string, ReseatProbe>>> probeOnWorker,
        ISet<object> reseated,
        CancellationToken cancellation)
    {
        TabTicket[] tickets =
        [
            .. Groups.SelectMany(group => group.Tabs)
                .Where(tab => tab.IsMissingFromDisk && !string.IsNullOrEmpty(tab.Path))
                .Select(TabTicket.Capture),
        ];
        if (tickets.Length == 0)
        {
            return 0;
        }

        (string Path, bool Markdown)[] paths =
        [
            .. tickets
                .GroupBy(ticket => ticket.Tab.Path, StringComparer.Ordinal)
                .Select(group => (group.Key, group.Any(ticket => ticket.Tab.IsMarkdown))),
        ];
        IReadOnlyDictionary<string, ReseatProbe> probes;
        try
        {
            probes = await probeOnWorker(paths);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Codex PR 7 round 3, finding 4: the re-seat's worker failed (its
            // index-hash read, say). Contained: one failed operation, the
            // tabs stay missing, and the rest of the re-sync still runs.
            if (cancellation.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellation);
            }

            HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, exception);
            return 1;
        }

        cancellation.ThrowIfCancellationRequested();

        // Back on the dispatcher: the apply turn.
        HashSet<WorkspaceTabViewModel> live =
            new(Groups.SelectMany(group => group.Tabs), ReferenceEqualityComparer.Instance);
        var failedPaths = new HashSet<string>(StringComparer.Ordinal);
        // The re-seated documents' publications (a board's or base's load, a
        // reading-mode note's projection), each counted.
        var documents = new List<Task>();
        foreach (TabTicket ticket in tickets)
        {
            WorkspaceTabViewModel tab = ticket.Tab;
            if (!live.Contains(tab)
                || tab.Id != ticket.Id
                || tab.Item != ticket.Item
                || !tab.IsMissingFromDisk
                || !probes.TryGetValue(tab.Path, out ReseatProbe probe))
            {
                continue;
            }

            if (probe.Failed)
            {
                // An identity we cannot read is left as it is (fail
                // closed, contract I7): the tab stays missing.
                _ = failedPaths.Add(tab.Path);
                continue;
            }

            if (probe.Stored is not string stored)
            {
                continue;
            }

            bool respelled = !string.Equals(stored, tab.Path, StringComparison.Ordinal);
            if (tab.IsDirty)
            {
                if (respelled)
                {
                    tab.RetargetPath(stored);
                }

                continue;
            }

            if (!tab.IsMarkdown)
            {
                if (respelled)
                {
                    tab.RetargetPath(stored);
                }

                // Codex PR 7 round 4, finding 1: through the workspace's
                // replace — the attach funnel gives the tab its board or base
                // on the stored spelling, the release sweep retires the old
                // one — and that document's load is the rescan's, awaited.
                ReplaceTabItem(tab, tab.Item);
                if (tab.Canvas is { } board)
                {
                    if (!reseated.Add(board))
                    {
                        continue;
                    }

                    documents.Add(RunKindReload("canvas", tab.Path, () =>
                    {
                        Task reload = board.ReloadAsync(cancellation);
                        TouchRescanScheduler(board);
                        return reload;
                    }));
                }
                else if (tab.Base is { } document && reseated.Add(document))
                {
                    documents.Add(RunKindReload("base", tab.Path, () =>
                    {
                        Task reopen = document.LoadAsync(cancellation);
                        TouchRescanScheduler(document);
                        return reopen;
                    }));
                }

                continue;
            }

            if (tab.ContentGeneration == ticket.Generation && probe.IndexHash is null)
            {
                // The index does not track the stored spelling (an
                // unindexed folder): nothing can vouch for its bytes, so
                // the tab stays missing.
                continue;
            }

            bool vouched = tab.ContentGeneration == ticket.Generation
                && probe.Read is { } read
                && string.Equals(read.Hash, probe.IndexHash, StringComparison.Ordinal);
            if (!vouched)
            {
                _ = failedPaths.Add(tab.Path);
                continue;
            }

            if (respelled)
            {
                tab.RetargetPath(stored);
            }

            tab.ReplaceItemWithReadText(tab.Item, probe.Read!.Value.Text, cancellation);
            documents.Add(tab.RescanReadingPublication);
            TrackTabRescanWork(tab);
        }

        return (ulong)failedPaths.Count + await CountRescanFailuresAsync(documents, cancellation);
    }

    /// <summary>
    /// W7-7 PR 7 (#1252, R-9; v2 §1, F3): one Markdown path's reload. The
    /// worker reads the text once and hashes it; back on the dispatcher, in
    /// ONE turn, every tab's decision is taken before any is applied:
    /// <list type="bullet">
    /// <item>a tab that is no longer the item that was read — closed,
    /// retargeted, a transient tab reused in place for another note (even
    /// one with the same bytes: the ticket carries the item and the tab's
    /// generation) — is left alone;</item>
    /// <item>a tab re-baselined since the read (its own save, a reload, a
    /// peer's mirror: the ticket carries the baseline hash) is current —
    /// neither reloaded nor marked;</item>
    /// <item>a FULL ticket — the same tab, item and generation, clean, no
    /// Slate-owned save in flight on the path — reloads, but only bytes whose
    /// hash is the index hash read for the path: a tab never shows bytes the
    /// index does not vouch for;</item>
    /// <item>any other — dirty, a save in flight, edited since the read —
    /// keeps its buffer and is marked stale against the index hash
    /// (staleness keyed by path, separate from the reload).</item>
    /// </list>
    /// A clean, current tab whose bytes could not be vouched for (the read
    /// failed, or the disk moved past the index) is marked stale too, and
    /// the path counts one failure: the next rescan reloads it.
    /// </summary>
    /// <returns>The path's failed operations: one when a clean, current
    /// tab's bytes could not be vouched for, plus one per reloaded
    /// reading-mode tab whose projection failed (codex PR 7 round 4,
    /// finding 3: the projection is the rescan's — silent, under the run's
    /// token, and awaited here).</returns>
    private async Task<ulong> ReloadMarkdownPathAsync(
        string path,
        string indexHash,
        IReadOnlyList<TabTicket> tickets,
        Func<string, Task<RescanRead?>> readOnWorker,
        CancellationToken cancellation)
    {
        RescanRead? read = await readOnWorker(path);
        cancellation.ThrowIfCancellationRequested();

        // Back on the dispatcher: the apply turn.
        HashSet<WorkspaceTabViewModel> live =
            new(Groups.SelectMany(group => group.Tabs), ReferenceEqualityComparer.Instance);
        bool saveInFlight = PropertyWriteInFlightFor(path)
            || live.Any(tab => IsMarkdownTabAt(tab, path) && tab.IsTaskToggleInFlight);
        var reload = new List<WorkspaceTabViewModel>();
        var stale = new List<WorkspaceTabViewModel>();
        bool unvouched = false;
        foreach (TabTicket ticket in tickets)
        {
            WorkspaceTabViewModel tab = ticket.Tab;
            if (!live.Contains(tab)
                || tab.Id != ticket.Id
                || tab.Item != ticket.Item
                || tab.IsMissingFromDisk)
            {
                continue;
            }

            if (!string.Equals(tab.SavedContentHash, ticket.Baseline, StringComparison.Ordinal))
            {
                continue;
            }

            bool fullTicket = !tab.IsDirty
                && !saveInFlight
                && tab.ContentGeneration == ticket.Generation;
            if (fullTicket && read is { } bytes && string.Equals(bytes.Hash, indexHash, StringComparison.Ordinal))
            {
                reload.Add(tab);
                continue;
            }

            unvouched |= fullTicket;
            stale.Add(tab);
        }

        foreach (WorkspaceTabViewModel tab in stale)
        {
            tab.InvalidateExternalState();
            tab.ApplyExternalStaleness(indexHash);
            TrackTabRescanWork(tab);
        }

        var projections = new List<Task>();
        foreach (WorkspaceTabViewModel tab in reload)
        {
            tab.ReloadKeepingCaretLine(read!.Value.Text, cancellation);
            projections.Add(tab.RescanReadingPublication);
            TrackTabRescanWork(tab);
        }

        if (reload.Count > 0)
        {
            // The whole-document refresh a clean re-baseline owes its
            // panels (the MirrorSamePathDocumentState precedent): the
            // outline, tasks and citations re-read the new bytes.
            NotePersisted(path);
            TasksReview.NoteRefreshed(path);
            TouchRescanScheduler(Panels);
            TouchRescanScheduler(Citations);
            TouchRescanScheduler(History);
            TouchRescanScheduler(TasksReview);
        }

        ulong failed = 0;
        if (unvouched)
        {
            HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, new RescanReadNotVouchedException());
            failed++;
        }

        return failed + await CountRescanFailuresAsync(projections, cancellation);
    }

    /// <summary>
    /// W7-7 PR 7 (#1252, R-9; AR-18's fallback): the dependents, once per
    /// rescan, each awaited to its publication — a published failure state
    /// is a publication (round 29; F6: a dashboard's D-12 line speaks from
    /// its load as always). The editor interaction caches drop; every
    /// reading model that depends on other files re-projects (a memo hit
    /// when unchanged; the re-sync has no per-path delta); the history
    /// panel reloads when its note changed; every open base re-runs (one
    /// reopened above is not re-run twice) and every dashboard reloads; the
    /// graph probes (a STALE or Error graph recovers on an unchanged
    /// Refresh — finding 8). Returns how many publications failed.
    /// </summary>
    internal async Task<ulong> ReSyncDependentsAsync(
        RescanDocumentsOutcome documents,
        IReadOnlyDictionary<string, IndexedPath> indexed,
        CancellationToken cancellation)
    {
        InvalidateAllInteractionStates();
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            TrackTabRescanWork(tab);
        }

        var work = new List<Task>();
        foreach (ReadingContentViewModel reading in Groups.SelectMany(group => group.Tabs)
            .Select(tab => tab.Reading)
            .OfType<ReadingContentViewModel>()
            .Distinct())
        {
            // Finding 5: the re-projection is the rescan's — silent, and
            // its failure faults this Task for the count.
            work.Add(RunKindReload("reading", string.Empty, () =>
            {
                Task projection = reading.NotifyRescanAsync(cancellation);
                TouchRescanReading(reading);
                return projection;
            }));
        }

        if (History.Path is string historyPath
            && NormalizeWorkspacePath(historyPath) is { Length: > 0 } shown
            && HistoryNoteChanged(shown, documents, indexed))
        {
            work.Add(RunKindReload("history", shown, () =>
            {
                Task reload = History.NoteSavedAsync(historyPath);
                TouchRescanScheduler(History);
                return reload;
            }));
        }

        work.Add(RunCountedReload("bases", string.Empty, () => ReSyncBasesAsync(documents.ReopenedBases, cancellation)));
        work.Add(RunCountedReload("graph", string.Empty, () => NotifyGraphOfRescanAsync(cancellation)));
        return await CountRescanFailuresAsync(work, cancellation);
    }

    /// <summary>Whether the history panel's note changed: an open note's
    /// tab baseline differed from the index (the document re-sync saw it);
    /// a note that is not open compares the index with the head of the
    /// version list the panel loaded.</summary>
    private bool HistoryNoteChanged(
        string path,
        RescanDocumentsOutcome documents,
        IReadOnlyDictionary<string, IndexedPath> indexed)
    {
        if (documents.ChangedOpenPaths.Contains(path))
        {
            return true;
        }

        bool open = Groups.SelectMany(group => group.Tabs)
            .Any(tab => IsPathBacked(tab.Item)
                && string.Equals(NormalizeWorkspacePath(tab.Path), path, StringComparison.Ordinal));
        if (open)
        {
            return false;
        }

        return !indexed.TryGetValue(path, out IndexedPath entry)
            || !string.Equals(entry.Hash, History.HeadContentHash, StringComparison.Ordinal);
    }

    /// <summary>The Bases dependent: every open base document re-runs its
    /// view (keeping its quick filter and transient sort) and every
    /// dashboard reloads — any file may have changed their rows. A base the
    /// document re-sync reopened is not re-run twice. Each is its OWN
    /// operation (codex PR 7 round 4, finding 6): the returned count is how
    /// many of them failed.</summary>
    private Task<ulong> ReSyncBasesAsync(
        IReadOnlySet<BaseDocumentViewModel> reopened,
        CancellationToken cancellation)
    {
        var publications = new List<Task>();
        var seen = new HashSet<BaseDocumentViewModel>(ReferenceEqualityComparer.Instance);
        IEnumerable<BaseDocumentViewModel> documents = BasesDockDocument is { } dock
            ? _baseDocuments.Values.Append(dock)
            : _baseDocuments.Values;
        foreach (BaseDocumentViewModel document in documents)
        {
            if (seen.Add(document) && !reopened.Contains(document))
            {
                publications.Add(RunKindReload("base-refresh", document.Path, () =>
                {
                    Task refresh = document.RefreshAsync(cancellation);
                    TouchRescanScheduler(document);
                    return refresh;
                }));
            }
        }

        foreach (DashboardViewModel dashboard in BasesDockDashboard is { } dockDashboard
            ? _dashboardDocuments.Values.Append(dockDashboard).Distinct()
            : _dashboardDocuments.Values)
        {
            publications.Add(RunKindReload("dashboard", string.Empty, () =>
            {
                Task load = dashboard.LoadAsync(cancellation);
                TouchRescanScheduler(dashboard);
                return load;
            }));
        }

        return CountRescanFailuresAsync(publications, cancellation);
    }

    /// <summary>The graph dependent (round 29): the SAME probes a
    /// Slate-owned event sends — through
    /// <see cref="NotifyGraphOfVaultChange"/>, the probes' one owner
    /// (rule C) — then awaited to their publications: each document's
    /// fixed-point drain and owner-context barrier covers the generation
    /// read and any reload it issued.</summary>
    /// <remarks>Codex PR 7 round 4: the run's token rides the probes into
    /// the native graph queries of the loads they issue (finding 5), and the
    /// graph document's publication and the Connections leaf's are two
    /// operations, each counted (finding 6).</remarks>
    private Task<ulong> NotifyGraphOfRescanAsync(CancellationToken cancellation)
    {
        NotifyGraphOfVaultChange(cancellation);
        var probes = new List<Task>
        {
            RunKindReload("connections", string.Empty, () =>
            {
                TouchRescanScheduler(Connections);
                return Connections.WhenPublishedAsync();
            }),
        };
        if (_graphDocument is { IsRetired: false } document)
        {
            probes.Add(RunKindReload("graph-document", string.Empty, () =>
            {
                TouchRescanScheduler(document);
                return document.WhenPublishedAsync();
            }));
        }

        return CountRescanFailuresAsync(probes, cancellation);
    }

    /// <summary>Await every re-sync operation; each that faults counts one
    /// failure (logged by type only). A cancelled run stops awaiting AT
    /// ONCE (codex AR-18 review round 2, finding 4) — a publication parked
    /// behind a close never holds it — and unwinds; the work it started is
    /// the close's to drain (<see cref="DrainRescanWork"/>).</summary>
    private static async Task<ulong> CountRescanFailuresAsync(
        IReadOnlyList<Task> work,
        CancellationToken cancellation)
    {
        Task all = Task.WhenAll(work);
        if (!all.IsCompleted)
        {
            var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellation.Register(() => cancelled.TrySetResult()))
            {
                _ = await Task.WhenAny(all, cancelled.Task);
            }
        }

        if (cancellation.IsCancellationRequested)
        {
            // Abandoned: observed, so a later fault is never unobserved.
            _ = all.ContinueWith(
                abandoned => _ = abandoned.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            throw new OperationCanceledException(cancellation);
        }

        ulong failed = 0;
        foreach (Task task in work)
        {
            // A counted operation reports its own children's failures
            // (codex PR 7 round 4, finding 6).
            if (task is Task<ulong> { IsCompletedSuccessfully: true } counted)
            {
                failed += counted.Result;
                continue;
            }

            if (task.IsCompletedSuccessfully)
            {
                continue;
            }

            Exception failure = task.Exception?.InnerException
                ?? new OperationCanceledException("A re-sync operation was cancelled outside the run.");
            HostLog.Write(HostDiagnosticEvent.VaultRescanFailed, failure);
            failed++;
        }

        return failed;
    }

    // W7-7 PR 7 (codex AR-18 review round 2, finding 4; codex PR 7 round 3,
    // finding 6): the owners of the work a run started outside the rescan
    // seam — its reloads' and dependents' worker bodies, the SUCCESSORS
    // their publications start (a graph or Connections probe's apply issues
    // a reload after the probe itself completed), the reading fetches and
    // editor-cache loads it triggered. Each is registered as its FIXED-POINT
    // drain, evaluated when waited on, so work started after the
    // registration is drained too.
    private readonly List<Func<Task>> _rescanTouched = [];
    private readonly HashSet<object> _rescanTouchedOwners = new(ReferenceEqualityComparer.Instance);

    // A completed run's fixed-point drains, started when it completed; the
    // close waits for those still running.
    private readonly List<Task> _rescanTails = [];

    /// <summary>A scheduler the run started work on — drained to its fixed
    /// point (<see cref="PanelWorkScheduler.WhenAllWorkDrained"/>).</summary>
    private void TouchRescanScheduler(PanelWorkScheduler scheduler)
    {
        if (_rescanTouchedOwners.Add(scheduler))
        {
            _rescanTouched.Add(scheduler.WhenAllWorkDrained);
        }
    }

    /// <summary>A reading model the run re-projected — its fetches drained
    /// until none is left.</summary>
    private void TouchRescanReading(ReadingContentViewModel reading)
    {
        if (_rescanTouchedOwners.Add(reading))
        {
            _rescanTouched.Add(() => DrainToEmptyAsync(reading.WhenRefreshWorkDrained));
        }
    }

    /// <summary>A tab's workers a run started — its editor coordinator's
    /// cache loads and its reading model's fetch — drained until none is
    /// left, so a retry they issue later is drained too.</summary>
    private void TrackTabRescanWork(WorkspaceTabViewModel tab)
    {
        if (tab.EditorInteractions is { } coordinator && _rescanTouchedOwners.Add(coordinator))
        {
            _rescanTouched.Add(() => DrainToEmptyAsync(coordinator.WhenBackgroundWorkDrained));
        }

        if (tab.Reading is { } reading)
        {
            TouchRescanReading(reading);
        }
    }

    /// <summary>Wait on a snapshot drain until a snapshot finds nothing
    /// running — off the calling thread's context.</summary>
    private static async Task DrainToEmptyAsync(Func<Task> snapshot)
    {
        while (true)
        {
            Task pending = snapshot();
            if (pending.IsCompleted)
            {
                return;
            }

            try
            {
                await pending.ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // A worker that ended by failing has ended.
            }
        }
    }

    /// <summary>
    /// A run whose re-sync ended without being cancelled (and, defensively,
    /// the next run's start): its owners' fixed-point drains start now and
    /// are kept until they end — work the run started can outlive it (a
    /// successor reload, a cache load), and the close waits for it.
    /// </summary>
    internal void CompleteRescanWork()
    {
        foreach (Func<Task> drain in _rescanTouched)
        {
            Task tail = drain();
            if (!tail.IsCompleted)
            {
                _rescanTails.Add(tail);
            }
        }

        _rescanTouched.Clear();
        _rescanTouchedOwners.Clear();
        _ = _rescanTails.RemoveAll(tail => tail.IsCompleted);
    }

    /// <summary>
    /// W7-7 PR 7 (codex AR-18 review round 2, finding 4): teardown — after
    /// every scheduler has shut down (their posted applies settled, so no
    /// drain below waits on this thread) — waits, WITHOUT a bound, until
    /// none of the work the last rescan started is still inside a core
    /// call: no session disposal ever races a rescan-originated native
    /// call. Each such call is bounded (a single-note query) or takes the
    /// run's token, which the close has cancelled.
    /// </summary>
    private void DrainRescanWork()
    {
        while (true)
        {
            Task[] pending =
            [
                .. _rescanTouched.Select(drain => drain())
                    .Concat(_rescanTails)
                    .Where(task => !task.IsCompleted),
            ];
            if (pending.Length == 0)
            {
                break;
            }

            try
            {
                Task.WaitAll(pending);
            }
            catch (AggregateException)
            {
                // Tracked bodies never fault by contract; a fault is still
                // an ended call.
            }
        }

        _rescanTouched.Clear();
        _rescanTouchedOwners.Clear();
        _rescanTails.Clear();
    }

    private IEnumerable<BaseDocumentViewModel> FileBackedBaseDocuments()
    {
        var seen = new HashSet<BaseDocumentViewModel>(ReferenceEqualityComparer.Instance);
        foreach (BaseDocumentViewModel document in _baseDocuments.Values)
        {
            if (!document.IsSavedQuery && seen.Add(document))
            {
                yield return document;
            }
        }

        if (BasesDockDocument is { IsSavedQuery: false } dock && seen.Add(dock))
        {
            yield return dock;
        }
    }

    private static bool IsMarkdownTabAt(WorkspaceTabViewModel tab, string path) =>
        tab.IsMarkdown && string.Equals(NormalizeWorkspacePath(tab.Path), path, StringComparison.Ordinal);

    /// <summary>
    /// The re-sync's batch form of <see cref="InvalidatePath(string)"/> for
    /// the documents it found missing: the same per-path operations — open
    /// tabs marked missing (their buffers kept), closed-tab history and the
    /// Connections stack pruned — with ONE command-state refresh and ONE
    /// workspace persist for the batch. Silent by contract: only a
    /// reconciliation scope may call it.
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

    /// <summary>A tab as the re-sync's worker read found it (v2 §1): the
    /// reference, its identity and item, its content generation and its
    /// baseline hash — all compared in the apply turn.</summary>
    private readonly record struct TabTicket(
        WorkspaceTabViewModel Tab,
        Guid Id,
        WorkspaceItemState Item,
        long Generation,
        string? Baseline)
    {
        internal static TabTicket Capture(WorkspaceTabViewModel tab) =>
            new(tab, tab.Id, tab.Item, tab.ContentGeneration, tab.SavedContentHash);
    }

    /// <summary>A clean, current tab whose bytes the index could not vouch
    /// for: counted as one failed re-sync operation.</summary>
    private sealed class RescanReadNotVouchedException()
        : Exception("A clean tab's re-read did not match the index hash read for it.");

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
    internal void ReloadKeepingCaretLine(string text, CancellationToken rescan = default)
    {
        TextDocument? before = EditorDocument;
        TextLocation caret = before is null
            ? new TextLocation(1, 1)
            : before.GetLocation(Math.Clamp(EditorCaretOffset, 0, before.TextLength));
        ReplaceItemWithReadText(Item, text, rescan);
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
