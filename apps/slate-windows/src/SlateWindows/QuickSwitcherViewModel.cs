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

    public QuickSwitcherViewModel(
        VaultSession session,
        string vaultRoot,
        Action<A11yEvent> announce,
        IEnumerable<SwitcherFile>? initialFiles = null,
        string? localAppDataRoot = null,
        bool debounceRanking = true)
    {
        _announce = announce;
        _uiContext = debounceRanking ? SynchronizationContext.Current : null;
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
                _announce(new A11yEvent.RowSelected(value.DisplayName));
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

        IsOpen = false;
        CancelRanking();
        Results.Clear();
        SelectedRow = null;
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
        IsOpen = false;
        CancelRanking();
        OpenRequested?.Invoke(this, (row.Path, target));
        Dismissed?.Invoke(this, EventArgs.Empty);
        RaiseCommandStates();
    }

    public void RecordOpen(string path)
    {
        _recents = _recentsStore.Add(path);
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
        var files = _files.ToList();
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
            && IsOpenablePath(change.Path)
            && !files.Any(file => string.Equals(file.Path, change.Path, StringComparison.Ordinal)))
        {
            files.Add(new SwitcherFile(change.Path, System.IO.Path.GetFileName(change.Path)));
        }

        _files = [.. files];
        if (IsOpen)
        {
            ScheduleRefresh();
        }
    }

    private static bool IsOpenablePath(string path) =>
        System.IO.Path.GetExtension(path).ToLowerInvariant() is ".md" or ".markdown" or ".canvas" or ".base";

    public void Dispose() => CancelRanking();

    private void ScheduleRefresh()
    {
        if (!IsOpen)
        {
            return;
        }

        if (_uiContext is null)
        {
            ApplyRanked(SlateUniffiMethods.SwitcherRank(_files, Query, [.. _recents]), Query);
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
        Results.Clear();
        SelectedRow = null;
        RaiseCommandStates();
        _ = RankAsync(files, query, recents, generation, cancellation.Token);
    }

    private async Task RankAsync(
        SwitcherFile[] files,
        string query,
        string[] recents,
        int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(60, cancellationToken);
            SwitcherRow[] ranked = await Task.Run(
                () => SlateUniffiMethods.SwitcherRank(files, query, recents),
                cancellationToken);
            _uiContext!.Post(
                _ =>
                {
                    if (!cancellationToken.IsCancellationRequested
                        && generation == _rankGeneration
                        && IsOpen
                        && string.Equals(Query, query, StringComparison.Ordinal))
                    {
                        ApplyRanked(ranked, query);
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
                    if (generation == _rankGeneration && IsOpen)
                    {
                        IsRanking = false;
                        _rankingError = "Quick Open could not rank files.";
                        OnPropertyChanged(nameof(ResultSummary));
                        // W0.5-3 residue: Windows Quick Open engine failure copy.
                        _announce(new A11yEvent.HostComposed(
                            _rankingError,
                            A11yPriority.High));
                        RaiseCommandStates();
                        Console.Error.WriteLine($"Quick Open ranking failed: {exception}");
                    }
                },
                null);
        }
    }

    private void ApplyRanked(SwitcherRow[] ranked, string query)
    {
        IsRanking = false;
        _rankingError = null;

        TotalResults = ranked.Length;
        Results.Clear();
        foreach (SwitcherRow row in ranked.Take(DisplayCap))
        {
            Results.Add(new QuickSwitcherRowViewModel(
                row.Path,
                row.Name,
                row.DisplayName,
                row.Score,
                row.DisplayNameMatchSpans));
        }

        _selectedRow = Results.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedRow));
        _announce(new A11yEvent.QuickSwitcherCount(
            (uint)Math.Min(TotalResults, int.MaxValue),
            query.Length == 0 ? null : query));
        RaiseCommandStates();
    }

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
