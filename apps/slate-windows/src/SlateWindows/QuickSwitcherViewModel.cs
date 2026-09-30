// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows;

internal sealed record QuickSwitcherRowViewModel(
    string Path,
    string Name,
    string DisplayName,
    int Score,
    MatchSpan[] DisplayNameMatchSpans);

/// <summary>
/// W1-4 Quick Open chrome. All scoring, display-name derivation, recency
/// blending, and count strings come from slate-core through the binding.
/// </summary>
internal sealed class QuickSwitcherViewModel : BindableBase, IDisposable
{
    internal const int DisplayCap = 50;
    private readonly FileRecentsStore _recentsStore;
    private readonly Action<A11yEvent> _announce;
    private readonly SynchronizationContext? _uiContext;
    private readonly QuickSwitcherRankCoordinator _rankCoordinator;
    private readonly Func<SwitcherFile[], string, string[], SwitcherRankPage> _rankTop;
    private readonly Func<CancellationToken, Task> _rankDelay;
    private CancellationTokenSource? _rankCancellation;
    private int _rankGeneration;
    private SwitcherFile[] _files = [];
    private IReadOnlyList<string> _recents = [];
    private string _query = string.Empty;
    private QuickSwitcherRowViewModel? _selectedRow;
    private bool _isOpen;
    private int _totalResults;
    private bool _isRanking;
    private string? _rankingError;
    private bool _disposed;

    // W7-7 PR 7 (#1252, R-9; v2 §7): the rescan re-sync's awaited
    // replacements — each settled INSIDE the publication of its rank
    // generation or a later one, by its terminal failure, or when the
    // switcher closes (F7).
    private readonly List<(int Generation, TaskCompletionSource Published)> _rescanRankWaiters = [];

    // The replacement journals open while a rescan reads the new list.
    private readonly List<ReplacementJournal> _openJournals = [];

    public QuickSwitcherViewModel(
        VaultSession session,
        string vaultRoot,
        Action<A11yEvent> announce,
        IEnumerable<SwitcherFile>? initialFiles = null,
        string? localAppDataRoot = null,
        bool debounceRanking = true,
        QuickSwitcherRankCoordinator? rankCoordinator = null,
        Func<SwitcherFile[], string, string[], SwitcherRankPage>? rankTop = null,
        Func<CancellationToken, Task>? rankDelay = null)
    {
        _announce = announce;
        _uiContext = debounceRanking ? SynchronizationContext.Current : null;
        _rankCoordinator = rankCoordinator ?? QuickSwitcherRankCoordinator.Shared;
        _rankDelay = rankDelay ?? (token => Task.Delay(60, token));
        _rankTop = rankTop ?? ((files, query, recents) =>
            SlateUniffiMethods.SwitcherRankTop(files, query, recents, DisplayCap));
        _recentsStore = new FileRecentsStore(vaultRoot, session.RootIdentity(), localAppDataRoot);
        _files = initialFiles?.ToArray() ?? [];
        OpenCommand = new RelayCommand(_ => Open(), _ => !IsOpen);
        DismissCommand = new RelayCommand(_ => Dismiss(), _ => IsOpen);
        OpenCurrentCommand = new RelayCommand(_ => OpenSelected(WorkspaceOpenTarget.CurrentTab), _ => SelectedRow is not null);
        OpenNewTabCommand = new RelayCommand(_ => OpenSelected(WorkspaceOpenTarget.NewTab), _ => SelectedRow is not null);
        OpenSplitRightCommand = new RelayCommand(_ => OpenSelected(WorkspaceOpenTarget.SplitRight), _ => SelectedRow is not null);
        OpenSplitDownCommand = new RelayCommand(_ => OpenSelected(WorkspaceOpenTarget.SplitDown), _ => SelectedRow is not null);
        MoveNextCommand = new RelayCommand(_ => MoveSelection(1), _ => Results.Count > 0);
        MovePreviousCommand = new RelayCommand(_ => MoveSelection(-1), _ => Results.Count > 0);
    }

    public event EventHandler<(string Path, WorkspaceOpenTarget Target)>? OpenRequested;
    public event EventHandler? Dismissed;

    internal Task RankCompletion { get; private set; } = Task.CompletedTask;

    // W7-7 PR 7 (codex PR 7 round 3, finding 5): the rank the coordinator
    // last ADMITTED, completing once its native call has returned and
    // released the process-wide lane. Written on the pool at admission.
    private Task _admittedRank = Task.CompletedTask;

    public ObservableCollection<QuickSwitcherRowViewModel> Results { get; } = [];

    public string Query
    {
        get => _query;
        set
        {
            if (SetField(ref _query, value))
            {
                ScheduleRefresh();
            }
        }
    }

    public QuickSwitcherRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetField(ref _selectedRow, value) && value is not null)
            {
                // R-4 (#1246; codex PR 3 round 5): the row the arrow reached,
                // named as the list names it — namesakes read apart here too.
                _announce(new A11yEvent.RowSelected(SiblingNames.SpokenAmong(
                    Results, value, row => row.DisplayName, row => row.Path, "result")));
                RaiseCommandStates();
            }
        }
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetField(ref _isOpen, value);
    }

    public int TotalResults
    {
        get => _totalResults;
        private set
        {
            if (SetField(ref _totalResults, value))
            {
                OnPropertyChanged(nameof(ResultSummary));
                OnPropertyChanged(nameof(HasResults));
            }
        }
    }

    public bool HasResults => TotalResults > 0;
    public bool IsRanking
    {
        get => _isRanking;
        private set
        {
            if (SetField(ref _isRanking, value))
            {
                OnPropertyChanged(nameof(ResultSummary));
            }
        }
    }

    public string ResultSummary => _rankingError is not null
        ? _rankingError
        : IsRanking
        ? "Searching files…"
        : TotalResults == 0
        ? "No matching files"
        : $"{TotalResults:N0} {(TotalResults == 1 ? "file" : "files")}";

    public ICommand OpenCommand { get; }
    public ICommand DismissCommand { get; }
    public ICommand OpenCurrentCommand { get; }
    public ICommand OpenNewTabCommand { get; }
    public ICommand OpenSplitRightCommand { get; }
    public ICommand OpenSplitDownCommand { get; }
    public ICommand MoveNextCommand { get; }
    public ICommand MovePreviousCommand { get; }

    public void Open()
    {
        _recents = _recentsStore.Load();
        _query = string.Empty;
        OnPropertyChanged(nameof(Query));
        IsOpen = true;
        ScheduleRefresh();
        RaiseCommandStates();
    }

    public void Dismiss()
    {
        if (!IsOpen)
        {
            return;
        }

        CloseSwitcher();
        Dismissed?.Invoke(this, EventArgs.Empty);
        RaiseCommandStates();
    }

    public void MoveSelection(int delta)
    {
        if (Results.Count == 0)
        {
            return;
        }

        int index = SelectedRow is null ? 0 : Results.IndexOf(SelectedRow);
        SelectedRow = Results[(index + delta + Results.Count) % Results.Count];
    }

    public void OpenSelected(WorkspaceOpenTarget target)
    {
        if (SelectedRow is not QuickSwitcherRowViewModel row)
        {
            return;
        }

        _recents = _recentsStore.Add(row.Path);
        CloseSwitcher();
        OpenRequested?.Invoke(this, (row.Path, target));
        Dismissed?.Invoke(this, EventArgs.Empty);
        RaiseCommandStates();
    }

    public void RecordOpen(string path)
    {
        _recents = _recentsStore.Add(path);
    }

    private void CloseSwitcher()
    {
        CancelRanking();
        // F7: a closed switcher publishes no rank — a rescan awaiting one
        // is settled here (Dismiss, OpenSelected), never left waiting.
        SettleRescanRankWaiters(int.MaxValue, failure: null);
        // Remove result peers while their parent is still visible so UIA
        // clients do not retain orphaned children after the overlay collapses.
        Results.Clear();
        SelectedRow = null;
        IsOpen = false;
    }

    public void ClearRecents()
    {
        _recentsStore.Clear();
        _recents = [];
        if (IsOpen)
        {
            ScheduleRefresh();
        }
    }

    public void ApplyFileChange(FileChangeEvent change)
    {
        bool openable = IsOpenablePath(change.Path);
        JournalChange(change, openable);
        _files = Applied(_files, change, openable);
        if (IsOpen)
        {
            ScheduleRefresh();
        }
    }

    /// <summary>One file change applied to a list: pure, idempotent per
    /// path (a Created already present, a Deleted already gone, a Renamed
    /// already moved each change nothing), so replaying a change the list
    /// already reflects is harmless.</summary>
    private static SwitcherFile[] Applied(SwitcherFile[] current, FileChangeEvent change, bool openable)
    {
        var files = current.ToList();
        if (change.PreviousPath is string previous)
        {
            string previousPrefix = previous + "/";
            string nextPrefix = change.Path + "/";
            for (int index = 0; index < files.Count; index++)
            {
                if (files[index].Path.StartsWith(previousPrefix, StringComparison.Ordinal))
                {
                    string nextPath = nextPrefix + files[index].Path[previousPrefix.Length..];
                    files[index] = new SwitcherFile(nextPath, System.IO.Path.GetFileName(nextPath));
                }
            }

            files.RemoveAll(file => string.Equals(file.Path, previous, StringComparison.Ordinal));
        }

        if (change.Kind == FileChangeKind.Deleted)
        {
            string deletedPrefix = change.Path + "/";
            files.RemoveAll(file => string.Equals(file.Path, change.Path, StringComparison.Ordinal)
                || file.Path.StartsWith(deletedPrefix, StringComparison.Ordinal));
        }
        else if (change.Kind is FileChangeKind.Created or FileChangeKind.Renamed
            && openable
            && !files.Any(file => string.Equals(file.Path, change.Path, StringComparison.Ordinal)))
        {
            files.Add(new SwitcherFile(change.Path, System.IO.Path.GetFileName(change.Path)));
        }

        return [.. files];
    }

    /// <summary>
    /// A batch of Slate-owned changes, in order, in one pass — the
    /// funnel's per-event <see cref="ApplyFileChange"/> batched. An open
    /// switcher re-ranks once.
    /// </summary>
    public void ApplyFileChanges(IReadOnlyCollection<(FileChangeEvent Change, bool Openable)> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        SwitcherFile[] files = _files;
        foreach ((FileChangeEvent change, bool openable) in changes)
        {
            JournalChange(change, openable);
            files = Applied(files, change, openable);
        }

        _files = files;
        if (IsOpen)
        {
            ScheduleRefresh();
        }
    }

    /// <summary>
    /// W7-7 PR 7 (#1252, R-9; v2 §7): a rescan's replacement list is read
    /// from the index in pages, on a worker, while Slate-owned writes keep
    /// arriving on the dispatcher. The journal records each change applied
    /// meanwhile, so <see cref="ReplaceFilesAsync"/> replays them onto the
    /// pages it read — a stale page can never revert a Slate write
    /// (<see cref="Applied"/> is idempotent, so a change the pages already
    /// reflect replays as nothing).
    /// </summary>
    internal sealed class ReplacementJournal
    {
        internal List<(FileChangeEvent Change, bool Openable)> Changes { get; } = [];
    }

    internal ReplacementJournal BeginReplacement()
    {
        var journal = new ReplacementJournal();
        _openJournals.Add(journal);
        return journal;
    }

    internal void EndReplacement(ReplacementJournal journal) => _ = _openJournals.Remove(journal);

    private void JournalChange(FileChangeEvent change, bool openable)
    {
        foreach (ReplacementJournal journal in _openJournals)
        {
            journal.Changes.Add((change, openable));
        }
    }

    /// <summary>
    /// W7-7 PR 7 (#1252, R-9; v2 §7): replace the list with the index's
    /// openable documents <paramref name="files"/>, the journal's changes
    /// replayed onto it. A closed switcher completes at once; an open one
    /// re-runs its query SILENTLY — no count is spoken, the rows are
    /// replaced in place and the selected path is kept when it survives —
    /// and the Task completes INSIDE the publication of that rank (or of a
    /// later one), faults on its terminal failure (counted by the rescan,
    /// never spoken here), completes when the switcher closes (F7), and is
    /// cancelled with <paramref name="cancellation"/>.
    /// </summary>
    internal Task ReplaceFilesAsync(
        SwitcherFile[] files,
        ReplacementJournal journal,
        CancellationToken cancellation)
    {
        EndReplacement(journal);
        if (cancellation.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellation);
        }

        SwitcherFile[] replayed = files;
        foreach ((FileChangeEvent change, bool openable) in journal.Changes)
        {
            replayed = Applied(replayed, change, openable);
        }

        _files = replayed;
        if (!IsOpen || _disposed)
        {
            return Task.CompletedTask;
        }

        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _rescanRankWaiters.Add((_rankGeneration + 1, published));
        if (cancellation.CanBeCanceled)
        {
            CancellationTokenRegistration registration =
                cancellation.Register(() => published.TrySetCanceled(cancellation));
            _ = published.Task.ContinueWith(
                _ => registration.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        ScheduleRefresh(silent: true);
        return published.Task;
    }

    /// <summary>Settle every awaited replacement up to
    /// <paramref name="generation"/>: published, or failed.</summary>
    private void SettleRescanRankWaiters(int generation, Exception? failure)
    {
        if (_rescanRankWaiters.Count == 0)
        {
            return;
        }

        List<(int Generation, TaskCompletionSource Published)> settled =
            [.. _rescanRankWaiters.Where(waiter => waiter.Generation <= generation)];
        _ = _rescanRankWaiters.RemoveAll(waiter => waiter.Generation <= generation);
        foreach ((_, TaskCompletionSource waiter) in settled)
        {
            _ = failure is null ? waiter.TrySetResult() : waiter.TrySetException(failure);
        }
    }

    /// <summary>Test seam (W7-7 PR 7, F7): replaces the rank debounce, so
    /// a fact can park a rank between its scheduling and its publication.</summary>
    internal Func<CancellationToken, Task>? RankDelayForTests { get; set; }

    /// <summary>Test seam (W7-7 PR 7, codex PR 7 round 3 finding 5): runs on
    /// the pool INSIDE the admitted native rank — after the coordinator's
    /// lane admitted it, where its token no longer reaches.</summary>
    internal Action? InsideRankForTests { get; set; }

    /// <summary>The paths Quick Open ranks over — for the rescan facts.</summary>
    internal IReadOnlyList<string> FilePathsForTests => [.. _files.Select(file => file.Path)];

    /// <summary>A Slate-owned event's path, classified as core does (W7-7
    /// PR 7, round 26). A rescan's delta rows carry core's own flag.</summary>
    private static bool IsOpenablePath(string path) => CoreDocumentClassification.IsOpenable(path);

    public void Dispose()
    {
        _disposed = true;
        CancelRanking();
        // F7: a disposed switcher (a close, a vault switch) publishes no
        // rank — a rescan awaiting one is settled, never left waiting.
        SettleRescanRankWaiters(int.MaxValue, failure: null);
        // W7-7 PR 7 (codex PR 7 round 3, finding 5): an ADMITTED rank's
        // native call no longer sees its token. The close waits for it to
        // return and release the process-wide lane, so nothing this vault
        // started outlives it and the next vault ranks at once. Pool work
        // only: the wait never needs this thread. A rank admitted after
        // the read below finds its token already cancelled and never runs.
        try
        {
            Volatile.Read(ref _admittedRank).Wait();
        }
        catch (AggregateException)
        {
        }
    }

    /// <summary>Re-rank the current query. <paramref name="silent"/> — a
    /// rescan's replacement (v2 §7) — keeps the published rows until the
    /// new ones replace them, keeps the selected path when it survives, and
    /// speaks neither the count nor a ranking failure: the rescan's one
    /// sentence speaks for the run.</summary>
    private void ScheduleRefresh(bool silent = false)
    {
        if (!IsOpen)
        {
            return;
        }

        if (_uiContext is null)
        {
            int inline = ++_rankGeneration;
            ApplyRanked(
                _rankTop(_files, Query, [.. _recents]),
                Query,
                inline,
                silent);
            return;
        }

        CancelRanking();
        var cancellation = new CancellationTokenSource();
        _rankCancellation = cancellation;
        int generation = ++_rankGeneration;
        string query = Query;
        SwitcherFile[] files = _files;
        string[] recents = [.. _recents];
        IsRanking = true;
        _rankingError = null;
        OnPropertyChanged(nameof(ResultSummary));
        if (!silent)
        {
            Results.Clear();
            SelectedRow = null;
        }

        RaiseCommandStates();
        RankCompletion = RankAsync(files, query, recents, generation, silent, cancellation.Token);
    }

    private async Task RankAsync(
        SwitcherFile[] files,
        string query,
        string[] recents,
        int generation,
        bool silent,
        CancellationToken cancellationToken)
    {
        try
        {
            await (RankDelayForTests ?? _rankDelay)(cancellationToken);
            SwitcherRankPage ranked = await _rankCoordinator.RankAsync(
                () =>
                {
                    InsideRankForTests?.Invoke();
                    return _rankTop(files, query, recents);
                },
                cancellationToken,
                admitted => Interlocked.Exchange(ref _admittedRank, admitted));
            _uiContext!.Post(
                _ =>
                {
                    if (!cancellationToken.IsCancellationRequested
                        && generation == _rankGeneration
                        && IsOpen
                        && string.Equals(Query, query, StringComparison.Ordinal))
                    {
                        ApplyRanked(ranked, query, generation, silent);
                    }
                },
                null);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _uiContext!.Post(
                _ =>
                {
                    if (!cancellationToken.IsCancellationRequested
                        && generation == _rankGeneration
                        && IsOpen)
                    {
                        IsRanking = false;
                        _rankingError = "Quick Open could not rank files.";
                        OnPropertyChanged(nameof(ResultSummary));
                        if (!silent)
                        {
                            // W0.5-3 residue: Windows Quick Open engine failure copy.
                            _announce(new A11yEvent.HostComposed(
                                _rankingError,
                                A11yPriority.High));
                        }

                        RaiseCommandStates();
                        HostLog.Write(HostDiagnosticEvent.QuickOpenRankingFailed, exception);
                        SettleRescanRankWaiters(generation, exception);
                    }
                },
                null);
        }
    }

    private void ApplyRanked(SwitcherRankPage ranked, string query, int generation, bool silent)
    {
        IsRanking = false;
        _rankingError = null;
        string? keptPath = silent ? _selectedRow?.Path : null;

        TotalResults = ClampTotal(ranked.Total);
        Results.Clear();
        foreach (SwitcherRow row in ranked.Rows.Take(DisplayCap))
        {
            Results.Add(new QuickSwitcherRowViewModel(
                row.Path,
                row.Name,
                row.DisplayName,
                row.Score,
                row.DisplayNameMatchSpans));
        }

        _selectedRow = (keptPath is null
                ? null
                : Results.FirstOrDefault(row => string.Equals(row.Path, keptPath, StringComparison.Ordinal)))
            ?? Results.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedRow));
        if (!silent)
        {
            _announce(new A11yEvent.QuickSwitcherCount(
                (uint)Math.Min(TotalResults, int.MaxValue),
                query.Length == 0 ? null : query));
        }

        RaiseCommandStates();
        SettleRescanRankWaiters(generation, failure: null);
    }

    internal static int ClampTotal(ulong total) =>
        (int)Math.Min(total, (ulong)int.MaxValue);

    private void CancelRanking()
    {
        _rankCancellation?.Cancel();
        _rankCancellation?.Dispose();
        _rankCancellation = null;
        IsRanking = false;
    }

    private void RaiseCommandStates()
    {
        foreach (ICommand command in new[]
        {
            OpenCommand,
            DismissCommand,
            OpenCurrentCommand,
            OpenNewTabCommand,
            OpenSplitRightCommand,
            OpenSplitDownCommand,
            MoveNextCommand,
            MovePreviousCommand,
        })
        {
            ((RelayCommand)command).RaiseCanExecuteChanged();
        }
    }
}
