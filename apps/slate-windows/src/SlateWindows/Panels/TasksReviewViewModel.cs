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
    private readonly Func<string, TaskItem, bool> _toggleViaOpenTab;
    private readonly Func<DateTimeOffset> _clock;

    private TaskReviewFilter _activeFilter = TaskReviewFilter.All;
    private string? _nextCursor;
    private long _totalFiltered;
    private bool _isLoading;
    private bool _isLoadingMore;
    private string? _loadError;
    private int _loadRequestId;
    private bool _pendingTabToggleRefresh;

    public TasksReviewViewModel(
        VaultSession session,
        Action<A11yEvent> announce,
        Func<string, WorkspaceOpenTarget, bool> openInternal,
        Func<string?> activeNotePath,
        Action<TaskItem> scrollToTask,
        Func<string, TaskItem, bool> toggleViaOpenTab,
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
        TaskReviewFilter filter = ActiveFilter;
        _isLoading = true;
        _loadError = null;
        RaiseStateChanges();
        StartWork(() =>
        {
            TaskWithLocationPage? page = null;
            string? failure = null;
            try
            {
                page = _session.TasksInVault(
                    ToTaskFilter(filter), new Paging(null, PageSize));
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                failure = exception.Message;
            }
            Post(() => PublishFirstPage(requestId, page, failure));
        });
    }

    /// <summary>Internal so stale-ordering is testable
    /// deterministically (the PublishOutline pattern).</summary>
    internal void PublishFirstPage(
        int requestId, TaskWithLocationPage? page, string? failure)
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
    /// load-more keeps existing rows (mac parity). Re-entrancy
    /// guarded.</summary>
    public void LoadMore()
    {
        if (_nextCursor is not { } cursor || _isLoadingMore)
        {
            return;
        }
        int requestId = _loadRequestId;
        TaskReviewFilter filter = ActiveFilter;
        _isLoadingMore = true;
        RaiseStateChanges();
        StartWork(() =>
        {
            TaskWithLocationPage? page = null;
            string? failure = null;
            try
            {
                page = _session.TasksInVault(
                    ToTaskFilter(filter), new Paging(cursor, PageSize));
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
                if (requestId != _loadRequestId)
                {
                    return;
                }
                _isLoadingMore = false;
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
            });
        });
    }

    /// <summary>Row toggle (mac toggleVaultTask): a file with an open
    /// tab routes through that tab's guarded ToggleTask (dirty
    /// refusal, conflict detection, canonical announcements — the
    /// refresh arrives via <see cref="NoteRefreshed"/>). A file with
    /// NO open tab toggles the session directly with no expected
    /// hash — explicit user intent overrides conflict detection, the
    /// mac review posture — then re-queries page one.</summary>
    public void ToggleTask(ReviewTaskRowViewModel row)
    {
        if (_toggleViaOpenTab(row.Path, row.Task))
        {
            _pendingTabToggleRefresh = true;
            return;
        }

        string path = row.Path;
        TaskItem task = row.Task;
        string nextStatus = task.Completed ? " " : "x";
        StartWork(() =>
        {
            string? failure = null;
            try
            {
                _ = _session.ToggleTaskStatus(
                    path, task.Ordinal, nextStatus, null);
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
    /// ONLY when this review initiated a tab-routed toggle — the
    /// snapshot must not chase unrelated saves (mac removed exactly
    /// that auto-refresh).</summary>
    public void NoteRefreshed(string path)
    {
        if (!_pendingTabToggleRefresh
            || !Rows.Any(row => string.Equals(
                row.Path, path, StringComparison.Ordinal)))
        {
            return;
        }
        _pendingTabToggleRefresh = false;
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
