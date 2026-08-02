// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>The mac TaskReviewFilter, ported: chips over the FFI
/// TaskFilter. Windows are UTC-midnight based — the documented V1
/// limitation both platforms share, so the two platforms agree on
/// which tasks are overdue.</summary>
internal enum TaskReviewFilter
{
    All,
    DueToday,
    Overdue,
    ThisWeek,
}

/// <summary>What the workspace did with a review toggle request
/// (adversarial round 1): the states are NOT collapsible — only a
/// STARTED toggle may arm a pending refresh, only a STALE refusal
/// should reload the snapshot, and a dirty refusal changes nothing.</summary>
internal enum ReviewToggleRoute
{
    /// <summary>No open tab holds the file — the review toggles the
    /// session directly.</summary>
    NoOpenTab,

    /// <summary>An open tab holds unsaved changes; the refusal was
    /// announced. Nothing changed.</summary>
    RefusedDirty,

    /// <summary>The tab refused because an earlier toggle is still
    /// in flight; the refusal was announced. Nothing changed —
    /// mapping this to Started would arm a refresh for an operation
    /// that never ran (adversarial round 2).</summary>
    RefusedBusy,

    /// <summary>The row's snapshot no longer matches the open tab's
    /// saved content; the conflict was announced. The snapshot needs
    /// a reload.</summary>
    RefusedStale,

    /// <summary>The tab's guarded toggle started; its completion
    /// arrives as a whole-document refresh.</summary>
    Started,
}

/// <summary>
/// W4-3 (#735): the vault-wide Tasks Review leaf — the mac
/// TasksReviewPanel flow, ported. An explicit SNAPSHOT: it loads on
/// first reveal, filter change, or the review command, and
/// deliberately does NOT auto-refresh on unrelated saves (a mac
/// auto-refresh was tried and removed: it reset paging and
/// re-queried a hidden pane). Toggles re-query page one because
/// filter-window membership may change.
/// </summary>
internal sealed class TasksReviewViewModel : PanelWorkScheduler
{
    /// <summary>Mac page size.</summary>
    internal const uint PageSize = 200;

    private const long DayMs = 24L * 60 * 60 * 1000;

    private readonly VaultSession _session;
    private readonly Action<A11yEvent> _announce;
    private readonly Func<string, WorkspaceOpenTarget, bool> _openInternal;
    private readonly Func<string?> _activeNotePath;
    private readonly Action<TaskItem> _scrollToTask;
    private readonly Func<string, TaskItem, string, ReviewToggleRoute> _toggleViaOpenTab;
    private readonly Func<DateTimeOffset> _clock;

    private TaskReviewFilter _activeFilter = TaskReviewFilter.All;
    private string? _nextCursor;
    private long _totalFiltered;
    private bool _isLoading;
    private bool _isLoadingMore;
    private string? _loadError;
    private int _loadRequestId;
    private int _loadMoreToken;
    private ulong _snapshotGeneration;
    private readonly HashSet<string> _pendingToggleRefreshPaths =
        new(StringComparer.Ordinal);

    public TasksReviewViewModel(
        VaultSession session,
        Action<A11yEvent> announce,
        Func<string, WorkspaceOpenTarget, bool> openInternal,
        Func<string?> activeNotePath,
        Action<TaskItem> scrollToTask,
        Func<string, TaskItem, string, ReviewToggleRoute> toggleViaOpenTab,
        Func<DateTimeOffset>? clock = null,
        bool synchronousForTests = false)
        : base(synchronousForTests)
    {
        _session = session;
        _announce = announce;
        _openInternal = openInternal;
        _activeNotePath = activeNotePath;
        _scrollToTask = scrollToTask;
        _toggleViaOpenTab = toggleViaOpenTab;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public ObservableCollection<ReviewTaskRowViewModel> Rows { get; } = [];

    public TaskReviewFilter ActiveFilter
    {
        get => _activeFilter;
        private set
        {
            if (SetField(ref _activeFilter, value))
            {
                RaiseStateChanges();
            }
        }
    }

    public bool IsLoading => _isLoading;

    public bool IsLoadingMore => _isLoadingMore;

    public bool HasMore => _nextCursor is not null;

    internal int LoadRequestIdForTests => _loadRequestId;

    /// <summary>Mac header, verbatim: "Tasks Review, showing N of M"
    /// while a next page exists, else "Tasks Review, N shown".</summary>
    public string Header => _nextCursor is not null
        ? $"Tasks Review, showing {Rows.Count} of {_totalFiltered}"
        : $"Tasks Review, {Rows.Count} shown";

    /// <summary>Mac state sentences, verbatim (the error header uses
    /// the typographic apostrophe the mac source ships).</summary>
    public string? EmptyMessage =>
        _loadError is { Length: > 0 } error
            ? $"Couldn’t load tasks. {error}"
        : _isLoading && Rows.Count == 0 ? "Loading tasks…"
        : Rows.Count == 0 ? $"No tasks matching {DisplayName(ActiveFilter)}."
        : null;

    /// <summary>Mac load-more strings, verbatim; remaining clamped
    /// to zero.</summary>
    public string LoadMoreLabel =>
        _isLoadingMore ? "Loading more tasks…" : "Load more tasks";

    public string LoadMoreAutomationName => _isLoadingMore
        ? "Loading more tasks"
        : $"Load more tasks. {Math.Max(0, _totalFiltered - Rows.Count)} remaining.";

    public string LoadMoreHelpText =>
        "Fetches the next page of vault tasks matching the active filter.";

    public static string DisplayName(TaskReviewFilter filter) => filter switch
    {
        TaskReviewFilter.DueToday => "Due today",
        TaskReviewFilter.Overdue => "Overdue",
        TaskReviewFilter.ThisWeek => "This week",
        _ => "All",
    };

    /// <summary>The active chip speaks the filter TOTAL, not the
    /// loaded-page count (mac chip labels, verbatim).</summary>
    public string FilterAutomationName(TaskReviewFilter filter) =>
        filter == ActiveFilter
            ? $"{DisplayName(filter)}, {_totalFiltered} "
                + (_totalFiltered == 1 ? "task" : "tasks")
            : DisplayName(filter);

    public static string FilterHelpText(TaskReviewFilter filter) =>
        $"Filter the review to {DisplayName(filter).ToLowerInvariant()} tasks.";

    /// <summary>The mac filter windows: UTC-midnight based. dueToday
    /// = open + [today, +1d); overdue = open + [0, today); thisWeek
    /// = open + [today, +7d); all = everything including done.</summary>
    internal TaskFilter ToTaskFilter(TaskReviewFilter filter)
    {
        long todayStart = _clock().ToUnixTimeMilliseconds() / DayMs * DayMs;
        return filter switch
        {
            TaskReviewFilter.DueToday => new TaskFilter(
                false, todayStart, todayStart + DayMs, null),
            TaskReviewFilter.Overdue => new TaskFilter(
                false, 0, todayStart, null),
            TaskReviewFilter.ThisWeek => new TaskFilter(
                false, todayStart, todayStart + (7 * DayMs), null),
            _ => new TaskFilter(null, null, null, null),
        };
    }

    /// <summary>Rail reveal (mac ensureVaultTasksLoaded): idempotent
    /// — loads only when nothing is loaded, nothing is in flight,
    /// and no error is showing.</summary>
    public void EnsureLoaded()
    {
        if (Rows.Count > 0 || _isLoading || _loadError is not null)
        {
            return;
        }
        LoadFirstPage();
    }

    /// <summary>The review command (mac openTasksReview): always a
    /// FRESH first page; the caller announces TasksReviewShown.</summary>
    public void ForceReload() => LoadFirstPage();

    /// <summary>Filter chip commit: re-query and announce (mac
    /// applyTaskReviewFilter). Re-selecting the active filter is a
    /// no-op.</summary>
    public void ApplyFilter(TaskReviewFilter filter)
    {
        if (filter == ActiveFilter)
        {
            return;
        }
        ActiveFilter = filter;
        _announce(new A11yEvent.TasksFilterSet(DisplayName(filter)));
        LoadFirstPage();
    }

    private void LoadFirstPage()
    {
        int requestId = ++_loadRequestId;
        // Starting a fresh page invalidates any in-flight load-more
        // (adversarial round 1): its stale completion must neither
        // append nor leave the button stuck on "Loading more tasks…".
        _ = ++_loadMoreToken;
        _isLoadingMore = false;
        TaskReviewFilter filter = ActiveFilter;
        _isLoading = true;
        _loadError = null;
        RaiseStateChanges();
        StartWork(() =>
        {
            TaskWithLocationPage? page = null;
            string? failure = null;
            ulong generation = 0;
            try
            {
                // The generation and the page must describe the SAME
                // index state, so the generation is re-read after the
                // query (adversarial round 2: checking only before
                // leaves a window where a save lands between check
                // and query). A mismatch retries; if writes keep
                // landing, the PRE-query generation is kept — it can
                // never match the live index again, so the next
                // load-more reloads instead of appending. The safe
                // direction, never the corrupt one.
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    generation = _session.InteractionGeneration();
                    InterleaveForTests?.Invoke();
                    page = _session.TasksInVault(
                        ToTaskFilter(filter), new Paging(null, PageSize));
                    if (_session.InteractionGeneration() == generation)
                    {
                        break;
                    }
                }
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                failure = exception.Message;
            }
            Post(() => PublishFirstPage(requestId, page, failure, generation));
        });
    }

    /// <summary>Test seam: runs between the generation snapshot and
    /// the index query inside both load workers, where a concurrent
    /// save is otherwise impossible to schedule deterministically.</summary>
    internal Action? InterleaveForTests { get; set; }

    /// <summary>Internal so stale-ordering is testable
    /// deterministically (the PublishOutline pattern).</summary>
    internal void PublishFirstPage(
        int requestId,
        TaskWithLocationPage? page,
        string? failure,
        ulong generation = 0)
    {
        if (requestId != _loadRequestId)
        {
            return;
        }
        if (failure is not null || page is null)
        {
            // Failed loads keep existing rows; the error is spoken by
            // the empty-state only when nothing is showing (mac keeps
            // rows on failed load-more; first-page parity here).
            _loadError = failure ?? "The vault could not be read.";
            _isLoading = false;
            RaiseStateChanges();
            return;
        }
        _snapshotGeneration = generation;
        Rows.Clear();
        foreach (TaskWithLocation row in page.Items)
        {
            Rows.Add(new ReviewTaskRowViewModel(row));
        }
        _nextCursor = page.NextCursor;
        _totalFiltered = checked((long)page.TotalFiltered);
        _loadError = null;
        _isLoading = false;
        RaiseStateChanges();
    }

    /// <summary>Load more appends via the opaque cursor; a failed
    /// load-more keeps existing rows (mac parity). The cursor is only
    /// honored against the SNAPSHOT it came from: an index-generation
    /// drift (any vault mutation since the first page) reloads
    /// instead of appending — a live cursor over a mutated index can
    /// duplicate moved rows and skip others (adversarial round 1).</summary>
    public void LoadMore()
    {
        if (_nextCursor is not { } cursor || _isLoadingMore || _isLoading)
        {
            return;
        }
        int token = ++_loadMoreToken;
        TaskReviewFilter filter = ActiveFilter;
        ulong snapshotGeneration = _snapshotGeneration;
        _isLoadingMore = true;
        RaiseStateChanges();
        StartWork(() =>
        {
            TaskWithLocationPage? page = null;
            string? failure = null;
            bool drifted = false;
            try
            {
                if (_session.InteractionGeneration() != snapshotGeneration)
                {
                    drifted = true;
                }
                else
                {
                    InterleaveForTests?.Invoke();
                    page = _session.TasksInVault(
                        ToTaskFilter(filter), new Paging(cursor, PageSize));
                    // Re-checked AFTER the query (adversarial round
                    // 2): a save can land between the check above and
                    // the query, bump the generation, and hand this
                    // cursor rows from the mutated index — which
                    // appended against the old page duplicates moved
                    // rows and skips others.
                    if (_session.InteractionGeneration() != snapshotGeneration)
                    {
                        drifted = true;
                        page = null;
                    }
                }
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                failure = exception.Message;
            }
            Post(() => PublishLoadMore(token, page, failure, drifted));
        });
    }

    /// <summary>Internal so token staleness and drift are testable
    /// deterministically.</summary>
    internal void PublishLoadMore(
        int token, TaskWithLocationPage? page, string? failure, bool drifted)
    {
        if (token != _loadMoreToken)
        {
            // Superseded by a fresh first page, which already cleared
            // the loading flag — nothing to do.
            return;
        }
        _isLoadingMore = false;
        if (drifted)
        {
            // The snapshot moved under the cursor: reload page one
            // rather than appending against a different index.
            LoadFirstPage();
            return;
        }
        if (failure is not null || page is null)
        {
            _loadError = failure ?? "The vault could not be read.";
            RaiseStateChanges();
            return;
        }
        foreach (TaskWithLocation row in page.Items)
        {
            Rows.Add(new ReviewTaskRowViewModel(row));
        }
        _nextCursor = page.NextCursor;
        _totalFiltered = checked((long)page.TotalFiltered);
        RaiseStateChanges();
    }

    /// <summary>Row toggle: a file with an open tab routes through
    /// that tab's guarded ToggleTask; a file with NO open tab toggles
    /// the session directly — WITH the row's snapshot hash as the
    /// expected hash (adversarial round 1, a deliberate divergence
    /// from the mac review's nil hash): task ordinals are only stable
    /// for a given source text, so a hash mismatch means the clicked
    /// row could name a DIFFERENT task now. Conflicts refuse loudly
    /// and reload the snapshot.</summary>
    public void ToggleTask(ReviewTaskRowViewModel row)
    {
        switch (_toggleViaOpenTab(row.Path, row.Task, row.ContentHash))
        {
            case ReviewToggleRoute.Started:
                _ = _pendingToggleRefreshPaths.Add(row.Path);
                return;
            case ReviewToggleRoute.RefusedStale:
                // The conflict was announced; the snapshot is stale.
                LoadFirstPage();
                return;
            case ReviewToggleRoute.RefusedDirty:
            case ReviewToggleRoute.RefusedBusy:
                return;
        }

        string path = row.Path;
        string fileName = row.FileName;
        TaskItem task = row.Task;
        string expectedHash = row.ContentHash;
        string nextStatus = task.Completed ? " " : "x";
        StartWork(() =>
        {
            bool conflict = false;
            string? failure = null;
            try
            {
                _ = _session.ToggleTaskStatus(
                    path, task.Ordinal, nextStatus, expectedHash);
            }
            catch (VaultException.WriteConflict)
            {
                conflict = true;
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                failure = exception.Message;
            }
            Post(() =>
            {
                if (IsShutDown)
                {
                    return;
                }
                if (conflict)
                {
                    // The file changed since the snapshot: refusing is
                    // the point — the stale ordinal could have toggled
                    // a different task. Reload so the rows are true.
                    _announce(new A11yEvent.TaskToggleConflict(fileName));
                    LoadFirstPage();
                    return;
                }
                if (failure is not null)
                {
                    // W0.5-3 residue: matches the tab toggle's failure string.
                    _announce(new A11yEvent.HostComposed(
                        $"Task could not be toggled: {failure}",
                        A11yPriority.High));
                    return;
                }
                // W0.5-3 residue: the Windows toggle-success strings —
                // the editor precedent (mac is silent on success).
                _announce(new A11yEvent.HostComposed(
                    task.Completed ? "Task reopened." : "Task completed.",
                    A11yPriority.Medium));
                LoadFirstPage();
            });
        });
    }

    /// <summary>A whole-document refresh landed for <paramref
    /// name="path"/> (the task-toggle publish): re-query page one
    /// ONLY when this review started a tab-routed toggle for that
    /// same path — the snapshot must not chase unrelated saves (mac
    /// removed exactly that auto-refresh), and refusals never arm
    /// the flag (adversarial round 1).</summary>
    public void NoteRefreshed(string path)
    {
        if (!_pendingToggleRefreshPaths.Remove(path))
        {
            return;
        }
        LoadFirstPage();
    }

    /// <summary>Row activation (mac openTaskRowInEditor): scroll in
    /// place when the task's file IS the active note, else open it
    /// first. Announces the mac verbs, only on what actually
    /// happened.</summary>
    public void OpenRow(ReviewTaskRowViewModel row)
    {
        if (string.Equals(
            _activeNotePath(), row.Path, StringComparison.Ordinal))
        {
            _scrollToTask(row.Task);
            _announce(new A11yEvent.ScrolledToLine(
                row.FileName, row.Task.Line));
            return;
        }
        if (_openInternal(row.Path, WorkspaceOpenTarget.CurrentTab))
        {
            _scrollToTask(row.Task);
            _announce(new A11yEvent.OpenedAtLine(row.FileName, row.Task.Line));
        }
    }

    internal override void Shutdown()
    {
        base.Shutdown();
        _ = ++_loadRequestId;
    }

    private void RaiseStateChanges()
    {
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(HasMore));
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(IsLoadingMore));
        OnPropertyChanged(nameof(LoadMoreLabel));
        OnPropertyChanged(nameof(LoadMoreAutomationName));
        OnPropertyChanged(nameof(ActiveFilter));
        OnPropertyChanged(nameof(AllFilterName));
        OnPropertyChanged(nameof(DueTodayFilterName));
        OnPropertyChanged(nameof(OverdueFilterName));
        OnPropertyChanged(nameof(ThisWeekFilterName));
    }

    // Per-chip binding surfaces (WPF bindings cannot pass the enum
    // through a method call).
    public string AllFilterName => FilterAutomationName(TaskReviewFilter.All);

    public string DueTodayFilterName =>
        FilterAutomationName(TaskReviewFilter.DueToday);

    public string OverdueFilterName =>
        FilterAutomationName(TaskReviewFilter.Overdue);

    public string ThisWeekFilterName =>
        FilterAutomationName(TaskReviewFilter.ThisWeek);
}

/// <summary>One review row (mac TasksReviewPanel row): the record
/// stays exact; rendered strings bound at the display ceiling. The
/// label leads with the FILENAME (not a status word) — the review
/// row shape.</summary>
internal sealed class ReviewTaskRowViewModel
{
    public ReviewTaskRowViewModel(TaskWithLocation row)
    {
        Task = row.Task;
        Path = row.Path;
        FileName = row.FileName;
        ContentHash = row.ContentHash;
        DisplayText = EditorInteractionCoordinator.BoundDisplayText(
            row.Task.Text);
        MetadataCaption = string.Join(
            " · ",
            TaskStatusPhrase.MetadataParts(row.Task)
                .Select(EditorInteractionCoordinator.BoundDisplayText));
    }

    public TaskItem Task { get; }

    public string Path { get; }

    public string FileName { get; }

    /// <summary>The file's hash at snapshot time — the toggle's
    /// identity check (round 1).</summary>
    public string ContentHash { get; }

    public string DisplayText { get; }

    public bool Completed => Task.Completed;

    public string MetadataCaption { get; }

    public bool HasMetadata => MetadataCaption.Length > 0;

    /// <summary>"&lt;fileName&gt;. &lt;text&gt;. Due &lt;date&gt;.
    /// Priority &lt;level&gt;. Repeats &lt;rec&gt;.
    /// &lt;statusPhrase&gt;" — the mac review row label.</summary>
    public string AutomationName => string.Join(
        ". ",
        new[] { FileName, DisplayText }
            .Concat(TaskStatusPhrase.MetadataParts(Task)
                .Select(EditorInteractionCoordinator.BoundDisplayText))
            .Append(TaskStatusPhrase.StatusPhrase(Task)));

    public string AutomationHelpText =>
        "Opens the source note at this task's line.";

    public string CheckboxLabel =>
        Completed ? "Mark incomplete" : "Mark complete";

    public string CheckboxHelpText => "Toggles the task between open and done.";
}
