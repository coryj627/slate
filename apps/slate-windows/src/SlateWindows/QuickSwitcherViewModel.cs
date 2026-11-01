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
/// WB-25 stage 2 (#1290; locked decision 05 §9.3.1): the rank runs against
/// core's index (<c>SwitcherRankTopIndexed</c>) under a token the
/// keystroke's source cancels — the host holds no file list.
/// </summary>
internal sealed class QuickSwitcherViewModel : BindableBase, IDisposable
{
    internal const int DisplayCap = 50;
    private readonly FileRecentsStore _recentsStore;
    private readonly Action<A11yEvent> _announce;
    private readonly SynchronizationContext? _uiContext;
    private readonly QuickSwitcherRankCoordinator _rankCoordinator;
    private readonly VaultSession _session;
    private readonly Func<string, string[], CancelToken, SwitcherRankPage> _rankTop;
    private readonly Func<CancellationToken, Task> _rankDelay;
    private CancellationTokenSource? _rankCancellation;
    private int _rankGeneration;
    // Whether the rank in flight is a silent one: a silent re-rank never
    // downgrades a keystroke's rank still pending (red team).
    private bool _pendingRankSilent;
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

    public QuickSwitcherViewModel(
        VaultSession session,
        string vaultRoot,
        Action<A11yEvent> announce,
        string? localAppDataRoot = null,
        bool debounceRanking = true,
        QuickSwitcherRankCoordinator? rankCoordinator = null,
        Func<string, string[], CancelToken, SwitcherRankPage>? rankTop = null,
        Func<CancellationToken, Task>? rankDelay = null)
    {
        _announce = announce;
        _uiContext = debounceRanking ? SynchronizationContext.Current : null;
        _rankCoordinator = rankCoordinator ?? QuickSwitcherRankCoordinator.Shared;
        _rankDelay = rankDelay ?? (token => Task.Delay(60, token));
        _session = session;
        _rankTop = rankTop ?? ((query, recents, cancel) =>
            session.SwitcherRankTopIndexed(query, recents, DisplayCap, cancel));
        _recentsStore = new FileRecentsStore(
            vaultRoot,
            session.RootIdentity(),
            localAppDataRoot,
            VaultRootAnchor.Of(session));
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

    /// <summary>
    /// A batch of Slate-owned changes, from the funnel. The index already
    /// holds them (WB-25 stage 2: no host list to edit), so an open
    /// switcher re-ranks once.
    /// </summary>
    public void ApplyFileChanges(IReadOnlyCollection<(FileChangeEvent Change, bool Openable)> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        AppliedChangeBatchesForTests++;
        if (IsOpen)
        {
            ScheduleRefresh();
        }
    }

    /// <summary>How many non-empty Slate-owned batches the funnel has
    /// handed the switcher — for the facts that wait for a write's event to
    /// be applied.</summary>
    internal int AppliedChangeBatchesForTests { get; private set; }

    /// <summary>
    /// WB-25 stage 2 (#1290): a rescan's scan committed a new index. A
    /// closed switcher completes at once; an open one re-runs its query
    /// SILENTLY against that index — no count is spoken, the rows are
    /// replaced in place and the selected path is kept when it survives —
    /// and the Task completes INSIDE the publication of that rank (or of a
    /// later one), faults on its terminal failure (counted by the rescan,
    /// never spoken here), completes when the switcher closes (F7), and is
    /// cancelled with <paramref name="cancellation"/>.
    /// </summary>
    internal Task ReRankForRescanAsync(CancellationToken cancellation) =>
        cancellation.IsCancellationRequested
            ? Task.FromCanceled(cancellation)
            : RepublishSilentlyAsync(cancellation);

    /// <summary>
    /// WB-10 (#1293, design §4.3): one page of a change-feed window. Its
    /// records are already in the index (WB-25 stage 2), and a rank reads
    /// only paths and names, so a page re-ranks an open switcher silently —
    /// awaited as <see cref="ReRankForRescanAsync"/> is — only when one of
    /// its file records can change the ranked set: an openable document
    /// created, removed or renamed, or a rename across the openable boundary
    /// (<c>note.md</c> to <c>note.png</c> removes a row). An edit to a file
    /// already ranked, an attachment-only change, or a folder record changes
    /// nothing Quick Open shows (codex).
    /// </summary>
    internal Task ApplyRecordsAsync(IReadOnlyList<ChangeRecord> records, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellation);
        }

        return records.Any(ChangesTheRankedSet)
            ? RepublishSilentlyAsync(cancellation)
            : Task.CompletedTask;
    }

    private static bool ChangesTheRankedSet(ChangeRecord record) =>
        record.Entry == ChangeEntry.File
        && !(record.Effect == ChangeEffect.Present && record.Counted == ChangeCount.Modified)
        && (record.Openable
            || CoreDocumentClassification.IsOpenable(record.Path)
            || (record.RenamedFrom is string from && CoreDocumentClassification.IsOpenable(from)));

    /// <summary>The index changed under a rescan: a closed switcher
    /// completes at once; an open one re-runs its query silently, and the
    /// Task completes inside that rank's publication (or a later one).</summary>
    private Task RepublishSilentlyAsync(CancellationToken cancellation)
    {
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
    /// lane admitted it, before the core call.</summary>
    internal Action? InsideRankForTests { get; set; }

    /// <summary>The paths Quick Open's empty query ranks, straight from the
    /// index (at most 10,000) — for the rescan facts.</summary>
    internal IReadOnlyList<string> RankedPathsForTests() => [.. RankForTests().Rows.Select(row => row.Path)];

    /// <summary>The empty query's exact total, straight from the index.</summary>
    internal int RankedTotalForTests() => ClampTotal(RankForTests().Total);

    private SwitcherRankPage RankForTests()
    {
        using var cancel = new CancelToken();
        return _session.SwitcherRankTopIndexed(string.Empty, [], 10_000, cancel);
    }

    public void Dispose()
    {
        _disposed = true;
        CancelRanking();
        // F7: a disposed switcher (a close, a vault switch) publishes no
        // rank — a rescan awaiting one is settled, never left waiting.
        SettleRescanRankWaiters(int.MaxValue, failure: null);
        // W7-7 PR 7 (codex PR 7 round 3, finding 5): an ADMITTED rank's
        // core call sees its cancelled token (WB-25 stage 2) but still has
        // to return before the session goes. The close waits for it to
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
            using var inlineCancel = new CancelToken();
            ApplyRanked(
                _rankTop(Query, [.. _recents], inlineCancel),
                Query,
                inline,
                silent);
            return;
        }

        // A silent re-rank that supersedes a keystroke's pending rank runs
        // as that keystroke's: its count is still spoken.
        silent &= !(IsRanking && !_pendingRankSilent);
        CancelRanking();
        var cancellation = new CancellationTokenSource();
        _rankCancellation = cancellation;
        _pendingRankSilent = silent;
        int generation = ++_rankGeneration;
        string query = Query;
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
        RankCompletion = RankAsync(query, recents, generation, silent, cancellation.Token);
    }

    private async Task RankAsync(
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
                    // A superseded keystroke's source cancels the core
                    // query too, so it stops inside core's statement.
                    using var core = new CancelToken();
                    using CancellationTokenRegistration linked = cancellationToken.Register(core.Cancel);
                    return _rankTop(query, recents, core);
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
