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
    private readonly Func<CancellationToken, Task> _filterDelay;
    private readonly object _filterCancellationGate = new();
    private CancellationTokenSource? _filterCancellation;
    private Task _filterCompletion = Task.CompletedTask;
    private int _filterGeneration;
    private string _filterText = string.Empty;
    private string? _scopeTag;

    /// <summary>The last spoken count, de-duplicated on (query, scope,
    /// total) — mac's (key, total) with its <c>tagScopeAnnounceKey</c>: the
    /// same request published again with the same total (a refresh) is not
    /// news; a changed total (a rescan), or an equal total for another
    /// query or tag scope, is (W7-7 R-3, codex round 4). A request's
    /// staleness is (query, scope) alone — the total is its answer.</summary>
    private (string Query, string? ScopeTag, ulong Total)? _lastFilterAnnouncement;

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
                // W7-7 (R-3): typing narrows WITHIN a tag scope (core ANDs
                // the query with scope_tag); emptying the field is the
                // user's clear, and the scope goes with the text.
                if (string.IsNullOrWhiteSpace(value) && _scopeTag is not null)
                {
                    _scopeTag = null;
                    OnPropertyChanged(nameof(ScopeTag));
                }

                OnPropertyChanged(nameof(IsFilterActive));
                ScheduleFilter();
                RaiseCommandStates();
            }
        }
    }

    /// <summary>W7-7 (R-3): the out-of-band tag scope, passed as core's
    /// <c>filter_files</c> <c>scope_tag</c>, for a tag the query grammar
    /// cannot express because it contains whitespace. Set by a tag
    /// activation with the field emptied; text typed afterwards filters
    /// within it; emptying the field or Clear Sidebar Filter drops it. Both
    /// core renderings name the tag: the status line's summary and the
    /// count announcement, which carries the scope.</summary>
    public string? ScopeTag => _scopeTag;

    public bool IsFilterActive => !string.IsNullOrWhiteSpace(FilterText) || _scopeTag is not null;

    /// <summary>One filter change for a tag activation (core's answer) or
    /// a clear: the field's text and the scope together, one run.</summary>
    private void ApplyTagActivation(string filterText, string? scopeTag)
    {
        bool textChanged = !string.Equals(_filterText, filterText, StringComparison.Ordinal);
        bool scopeChanged = !string.Equals(_scopeTag, scopeTag, StringComparison.Ordinal);
        if (!textChanged && !scopeChanged)
        {
            return;
        }

        // Written past the FilterText setter, whose emptied-field rule
        // would drop the scope being entered.
        _filterText = filterText;
        _scopeTag = scopeTag;
        if (textChanged)
        {
            OnPropertyChanged(nameof(FilterText));
        }

        if (scopeChanged)
        {
            OnPropertyChanged(nameof(ScopeTag));
        }

        OnPropertyChanged(nameof(IsFilterActive));
        ScheduleFilter();
        RaiseCommandStates();
    }

    private void ClearFilter() => ApplyTagActivation(string.Empty, scopeTag: null);

    private void ScheduleFilter(bool automatic = false)
    {
        Task previous = FilterCompletion;
        CancelFilterCore();
        int generation = ++_filterGeneration;
        string query = FilterText.Trim();
        string? scopeTag = _scopeTag;
        if (query.Length == 0 && scopeTag is null)
        {
            // Codex round 6: a USER clearing the filter cancels the
            // automatic refilter that would have consumed the pending
            // mutation reassert — clear it here, or a later organic
            // refresh resurrects the obsolete status. The automatic
            // path preserves it for its own publication.
            if (!automatic)
            {
                _statusToReassert = null;
            }

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
                    outcome = RunFilterQuery(query, scopeTag, CancellationToken.None);
                }

                ApplyFilterOutcome(query, scopeTag, outcome, automatic);
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

        if (!automatic)
        {
            Status = "Filtering files…";
        }

        Task completion = FilterAfterDelayAsync(
            previous,
            query,
            scopeTag,
            generation,
            automatic,
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
        string? scopeTag,
        int generation,
        bool automatic,
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
            await _filterDelay(cancellationToken).ConfigureAwait(false);
            if (!TryBeginSessionWork(out SessionWorkLease? lease))
            {
                return;
            }

            FilterOutcome? outcome = null;
            using (lease)
            {
                await _runFilterWorker(
                    () => outcome = RunFilterQuery(query, scopeTag, cancellationToken),
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
                            && string.Equals(FilterText.Trim(), query, StringComparison.Ordinal)
                            && string.Equals(_scopeTag, scopeTag, StringComparison.Ordinal))
                        {
                            ApplyFilterOutcome(query, scopeTag, outcome, automatic);
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

    private FilterOutcome RunFilterQuery(string query, string? scopeTag, CancellationToken cancellationToken)
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
                    scopeTag,
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

    private void ApplyFilterOutcome(string query, string? scopeTag, FilterOutcome outcome, bool automatic)
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
            _statusToReassert = null;
            ReportFailure(outcome.Error);
        }
        else if (automatic && _statusToReassert is string reassert)
        {
            // Codex round 5: an AUTOMATIC refilter (tree publication)
            // must not erase the mutation's final status with the
            // filter summary, nor speak a redundant count after the
            // mutation result.
            _statusToReassert = null;
            Status = reassert;
        }
        else
        {
            // A USER-driven filter supersedes any pending mutation
            // reassert — the user asked for the summary.
            _statusToReassert = null;
            Status = outcome.AudioSummary;
            if (_lastFilterAnnouncement != (query, scopeTag, outcome.Total))
            {
                _lastFilterAnnouncement = (query, scopeTag, outcome.Total);
                // W7-7 (R-3): the field never shows a tag scope, so the
                // count carries it and core renders the tag into the
                // sentence ("File list, 1 item. Filtered by tag two words.").
                _announce(new A11yEvent.FileListCount(
                    (uint)Math.Min(outcome.Total, uint.MaxValue),
                    scopeTag));
            }
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
