// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Globalization;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// Owns the debounced sidebar-filter operation: cancellation, generation
/// ordering, worker execution, UI publication, and date-window conversion.
/// </summary>
internal sealed partial class FilesSidebarViewModel
{
    private const int FilterDebounceMilliseconds = 200;
    private readonly SynchronizationContext? _filterUiContext;
    private readonly Func<Action, CancellationToken, Task> _runFilterWorker;
    private readonly object _filterCancellationGate = new();
    private CancellationTokenSource? _filterCancellation;
    private Task _filterCompletion = Task.CompletedTask;
    private int _filterGeneration;
    private string _filterText = string.Empty;

    public ObservableCollection<FileTreeNodeViewModel> FilterResults { get; } = [];
    internal Task FilterCompletion
    {
        get
        {
            lock (_filterCancellationGate)
            {
                return _filterCompletion;
            }
        }
    }

    internal bool IsFiltering => !FilterCompletion.IsCompleted;

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetField(ref _filterText, value))
            {
                OnPropertyChanged(nameof(IsFilterActive));
                ScheduleFilter();
                RaiseCommandStates();
            }
        }
    }

    public bool IsFilterActive => !string.IsNullOrWhiteSpace(FilterText);

    private void ScheduleFilter()
    {
        Task previous = FilterCompletion;
        CancelFilterCore();
        int generation = ++_filterGeneration;
        string query = FilterText.Trim();
        if (query.Length == 0)
        {
            FilterResults.Clear();
            lock (_filterCancellationGate)
            {
                _filterCompletion = previous;
            }

            return;
        }

        if (_filterUiContext is null)
        {
            if (!TryBeginSessionWork(out SessionWorkLease? lease))
            {
                return;
            }

            try
            {
                FilterOutcome outcome;
                using (lease)
                {
                    outcome = RunFilterQuery(query, CancellationToken.None);
                }

                ApplyFilterOutcome(outcome);
            }
            catch (Exception exception)
            {
                HostLog.Write(HostDiagnosticEvent.SidebarFilterFailed, exception);
                ReportFailure("Could not filter files.");
            }

            lock (_filterCancellationGate)
            {
                _filterCompletion = Task.CompletedTask;
            }

            return;
        }

        var cancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellation.Token;
        lock (_filterCancellationGate)
        {
            if (SessionShutdownStarted)
            {
                cancellation.Dispose();
                _filterCompletion = previous;
                return;
            }

            _filterCancellation = cancellation;
        }

        Status = "Filtering files…";
        Task completion = FilterAfterDelayAsync(
            previous,
            query,
            generation,
            cancellation,
            cancellationToken);
        lock (_filterCancellationGate)
        {
            _filterCompletion = completion;
        }
    }

    internal bool CancelFilter()
    {
        bool wasPending = IsFiltering;
        if (wasPending)
        {
            ++_filterGeneration;
        }

        CancelFilterCore();
        return wasPending;
    }

    internal Task CancelFilterAndGetCompletion()
    {
        CancelFilter();
        return FilterCompletion;
    }

    private void CancelFilterCore()
    {
        CancellationTokenSource? cancellation;
        lock (_filterCancellationGate)
        {
            cancellation = _filterCancellation;
            _filterCancellation = null;
        }

        if (cancellation is null)
        {
            return;
        }

        try
        {
            try
            {
                cancellation.Cancel();
            }
            catch (Exception exception)
            {
                // Cancellation is best-effort during teardown. A callback
                // failure must not prevent later producers from being canceled.
                HostLog.Write(HostDiagnosticEvent.SidebarFilterShutdownFailed, exception);
            }
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private async Task FilterAfterDelayAsync(
        Task previous,
        string query,
        int generation,
        CancellationTokenSource cancellation,
        CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                await previous.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // A prior request is terminal even when its UI callback
                // faulted. Keep the newest filter moving and retain a
                // privacy-safe diagnostic for the unexpected fault.
                HostLog.Write(HostDiagnosticEvent.SidebarFilterFailed, exception);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(FilterDebounceMilliseconds, cancellationToken).ConfigureAwait(false);
            if (!TryBeginSessionWork(out SessionWorkLease? lease))
            {
                return;
            }

            FilterOutcome? outcome = null;
            using (lease)
            {
                await _runFilterWorker(
                    () => outcome = RunFilterQuery(query, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (outcome is null)
            {
                throw new InvalidOperationException("Filter worker completed without an outcome.");
            }

            var applied = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _filterUiContext!.Post(
                _ =>
                {
                    try
                    {
                        if (!cancellationToken.IsCancellationRequested
                            && generation == _filterGeneration
                            && string.Equals(FilterText.Trim(), query, StringComparison.Ordinal))
                        {
                            ApplyFilterOutcome(outcome);
                        }

                        applied.TrySetResult();
                    }
                    catch (Exception exception)
                    {
                        applied.TrySetException(exception);
                    }
                },
                null);
            await applied.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                HostLog.Write(HostDiagnosticEvent.SidebarFilterFailed, exception);
                await ReportFilterFailureAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            bool ownsCancellation = false;
            lock (_filterCancellationGate)
            {
                if (ReferenceEquals(_filterCancellation, cancellation))
                {
                    _filterCancellation = null;
                    ownsCancellation = true;
                }
            }

            if (ownsCancellation)
            {
                cancellation.Dispose();
            }
        }
    }

    private async Task ReportFilterFailureAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var applied = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _filterUiContext!.Post(
                _ =>
                {
                    try
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            ReportFailure("Could not filter files.");
                        }

                        applied.TrySetResult();
                    }
                    catch (Exception exception)
                    {
                        applied.TrySetException(exception);
                    }
                },
                null);
            await applied.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            HostLog.Write(HostDiagnosticEvent.SidebarFilterFailed, exception);
        }
    }

    private FilterOutcome RunFilterQuery(string query, CancellationToken cancellationToken)
    {
        try
        {
            string[] requirements = _session.SidebarFilterDateRequirements(query);
            SidebarFilterDateWindow[] windows = BuildDateWindows(requirements);
            var files = new List<FileSummary>();
            string? cursor = null;
            ulong total = 0;
            string audioSummary = string.Empty;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                SidebarFilterPage page = _session.FilterFiles(
                    query,
                    null,
                    null,
                    windows,
                    new Paging(cursor, PageLimit));
                files.AddRange(page.Files.Take((int)PageLimit - files.Count));
                total = page.Total;
                cursor = files.Count >= PageLimit ? null : page.NextCursor;
                audioSummary = page.AudioSummary;
            }
            while (cursor is not null);

            return new FilterOutcome(files, total, audioSummary, null);
        }
        catch (Exception exception) when (
            exception is VaultException or FormatException or ArgumentOutOfRangeException)
        {
            HostLog.Write(HostDiagnosticEvent.SidebarFilterFailed, exception);
            return new FilterOutcome([], 0, string.Empty, "Filter could not be applied.");
        }
    }

    private void ApplyFilterOutcome(FilterOutcome outcome)
    {
        FilterResults.Clear();
        foreach (FileSummary summary in outcome.Files)
        {
            FilterResults.Add(new FileTreeNodeViewModel(
                this,
                summary.Path,
                summary.Name,
                isDirectory: false,
                level: 1,
                hasChildren: false,
                summary: summary));
        }

        if (outcome.Error is not null)
        {
            ReportFailure(outcome.Error);
        }
        else
        {
            Status = outcome.AudioSummary;
            _announce(new A11yEvent.FileListCount((uint)Math.Min(outcome.Total, uint.MaxValue)));
        }
    }

    private sealed record FilterOutcome(
        IReadOnlyList<FileSummary> Files,
        ulong Total,
        string AudioSummary,
        string? Error);

    internal static SidebarFilterDateWindow[] BuildDateWindows(
        IEnumerable<string> requirements,
        DateTimeOffset? now = null,
        TimeZoneInfo? timeZone = null)
    {
        TimeZoneInfo zone = timeZone ?? TimeZoneInfo.Local;
        DateTimeOffset current = now ?? DateTimeOffset.Now;
        DateTime localNow = TimeZoneInfo.ConvertTime(current, zone).DateTime;
        DateTime today = DateTime.SpecifyKind(localNow.Date, DateTimeKind.Unspecified);
        var windows = new List<SidebarFilterDateWindow>();
        foreach (string requirement in requirements)
        {
            DateTime start = requirement switch
            {
                "@today" => today,
                "@yesterday" => today.AddDays(-1),
                "@last7d" => today.AddDays(-6),
                "@last30d" => today.AddDays(-29),
                _ when requirement.StartsWith('@')
                    && DateTime.TryParseExact(
                        requirement[1..],
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime parsed) => DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified),
                _ => throw new FormatException($"Unsupported date term {requirement}."),
            };
            DateTime end = requirement switch
            {
                "@last7d" or "@last30d" => today.AddDays(1),
                _ => start.AddDays(1),
            };
            windows.Add(new SidebarFilterDateWindow(
                requirement,
                ToUnixMilliseconds(start, zone),
                ToUnixMilliseconds(end, zone)));
        }

        return [.. windows];
    }

    private static long ToUnixMilliseconds(DateTime local, TimeZoneInfo zone)
    {
        DateTime safe = zone.IsInvalidTime(local) ? local.AddHours(1) : local;
        DateTime utc = TimeZoneInfo.ConvertTimeToUtc(safe, zone);
        return new DateTimeOffset(utc).ToUnixTimeMilliseconds();
    }
}
