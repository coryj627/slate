// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>One version row — core's <see cref="VersionSummary"/>
/// projected for display (contract H3): identity is
/// <see cref="PositionFromTail"/>, content operations key on
/// <see cref="ContentHashAfter"/>, and every sentence fragment is
/// core's verbatim (HINV-1).</summary>
internal sealed class HistoryVersionRow : BindableBase
{
    private bool _selectedForCompare;

    public HistoryVersionRow(VersionSummary summary, string absoluteDate, string relativeDate)
    {
        Summary = summary;
        AbsoluteDate = absoluteDate;
        RelativeDate = relativeDate;
        Annotations = summary.Annotations.Select(a => a.Display).ToArray();
        AccessibleName = Annotations.Count == 0
            ? $"{absoluteDate}, {summary.AudioFragment}"
            : $"{absoluteDate}, {summary.AudioFragment}, {string.Join(", ", Annotations)}";
    }

    public VersionSummary Summary { get; }

    public uint PositionFromTail => Summary.PositionFromTail;

    public string ContentHashAfter => Summary.ContentHashAfter;

    public bool IsMarker => Summary.IsMarker;

    public string AbsoluteDate { get; }

    public string RelativeDate { get; }

    public string AudioFragment => Summary.AudioFragment;

    public IReadOnlyList<string> Annotations { get; }

    public string AccessibleName { get; }

    /// <summary>The compare checkbox state — driven by the VM's
    /// max-two selection model (contract H6), never set directly.</summary>
    public bool SelectedForCompare
    {
        get => _selectedForCompare;
        internal set => SetField(ref _selectedForCompare, value);
    }
}

/// <summary>A consecutive same-day run of visible rows (contract H4):
/// id stable across pagination appends; collapse is per-session view
/// state living here so the view stays declarative.</summary>
internal sealed class HistoryDayGroup : BindableBase
{
    private bool _isCollapsed;

    public HistoryDayGroup(string id, string title, IReadOnlyList<HistoryVersionRow> rows)
    {
        Id = id;
        Title = title;
        Rows = rows;
    }

    public string Id { get; }

    public string Title { get; }

    public IReadOnlyList<HistoryVersionRow> Rows { get; }

    public string AccessibleName =>
        $"{Title}, {Rows.Count} {(Rows.Count == 1 ? "version" : "versions")}, "
        + (IsCollapsed ? "collapsed" : "expanded");

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (SetField(ref _isCollapsed, value))
            {
                OnPropertyChanged(nameof(AccessibleName));
            }
        }
    }
}

/// <summary>A deleted-file row (contract H10) — display text composed
/// from core's entry; "restorable"/"not restorable" is the recorded
/// accessible suffix.</summary>
internal sealed class HistoryDeletedRow
{
    public HistoryDeletedRow(DeletedFileEntry entry, string deletedText, string? sizeText)
    {
        Entry = entry;
        DeletedText = deletedText;
        SizeText = sizeText;
        AccessibleName =
            $"{entry.Path}, {deletedText.ToLowerInvariant()}, "
            + (entry.Recoverable ? "restorable" : "not restorable");
    }

    public DeletedFileEntry Entry { get; }

    public string Path => Entry.Path;

    public bool Recoverable => Entry.Recoverable;

    public string DeletedText { get; }

    public string? SizeText { get; }

    public string AccessibleName { get; }
}

/// <summary>An inline diff publication (contract H5/H6): anchored
/// under a version row (per-row Compare), or under the section header
/// (two-version compare, anchor null). Exactly one inline diff at a
/// time; failures carry the message inline, never a dialog.</summary>
internal sealed record HistoryInlineDiff(
    uint? AnchorPosition,
    StructuredDiff? Diff,
    string? Error);

/// <summary>The since-open section's publication (contract H8): only
/// Diff and BaselineCompacted render anything.</summary>
internal enum HistorySinceOpenKind
{
    None,
    Diff,
    BaselineCompacted,
}

internal sealed record HistorySinceOpenState(
    HistorySinceOpenKind Kind,
    StructuredDiff? Diff);

/// <summary>The open inline destination row's staged state (H9/H10,
/// divergence HD-2). Identity is captured AT OPEN (version hash /
/// deleted source path — the capture-at-staging rule), so a reload
/// that shifts positions can never re-target the row; the draft,
/// notice, and refusal live here so a republish re-renders them
/// instead of orphaning the view elements a completion closed
/// over (red team round 1).</summary>
internal sealed record HistoryDestinationStaging(
    bool ForDeletedFile,
    string NotePath,
    string VersionHash,
    string FormattedDate,
    string Notice,
    string Draft,
    string? Refusal);

/// <summary>
/// W4-7 (#739): the history leaf's document — the mac
/// AppState+History twin over the O FFI surface. Owns the version
/// list (paged, day-grouped, marker-filtered), the compare model, the
/// inline diff, the since-open funnel, and the deleted-file list.
/// Every FFI call runs on the scheduler (HINV-6: cost is proportional
/// to the op-log length and nothing is cancellable); publishes are
/// generation- and path-guarded (HINV-5). The VM never announces on
/// its own (HINV-4) — the workspace coordinator owns the four
/// canonical events.
/// </summary>
internal sealed class HistoryViewModel : PanelWorkScheduler
{
    private const uint FirstPageLimit = 50;
    private const uint DeletedPageLimit = 200;

    private readonly VaultSession _session;
    private int _generation;
    private int _deletedGeneration;
    private int _diffGeneration;
    private string? _path;
    private readonly List<VersionSummary> _loaded = [];
    private string? _nextCursor;
    private ulong _totalFiltered;
    private bool _showMarkers;
    private bool _isLoading;
    private string? _loadError;
    private IReadOnlyList<HistoryDayGroup> _dayGroups = [];
    private readonly HashSet<string> _collapsedGroupIds = new(StringComparer.Ordinal);
    private readonly List<uint> _compareSelection = [];
    private HistoryInlineDiff? _inlineDiff;
    private HistorySinceOpenState _sinceOpen = new(HistorySinceOpenKind.None, null);
    private bool _showChangesSinceOpen;
    private IReadOnlyList<HistoryDeletedRow> _deletedRows = [];
    private bool _deletedLoaded;
    private bool _isDeletedLoading;
    private string? _deletedError;
    private HistoryDestinationStaging? _destinationStaging;
    private Task? _pendingMarkOpened;

    public HistoryViewModel(
        VaultSession session,
        bool synchronousForTests = false)
        : base(synchronousForTests)
    {
        _session = session;
    }

    /// <summary>The active tab's SAVED content hash at compare time —
    /// the log tail after any save (contract H6). Installed by the
    /// workspace; null = no comparable current state.</summary>
    internal Func<string?>? CurrentContentHashProvider { get; set; }

    /// <summary>The newest loaded ledger row's content hash — the
    /// conforming CAS source for tab kinds that carry no editor buffer
    /// hash (H7/H12: .canvas/.base restores stay guarded; the list as
    /// loaded IS the staging basis).</summary>
    internal string? HeadContentHash =>
        _loaded.FirstOrDefault(summary => summary.PositionFromTail == 0)
            ?.ContentHashAfter;

    /// <summary>Test seams: block a worker between its FFI completion
    /// and its publish (the citations InterleaveForTests shape) so
    /// interleaving facts are deterministic, never raced.</summary>
    internal Action? LoadInterleaveForTests { get; set; }

    internal Action? DiffInterleaveForTests { get; set; }

    internal Action? MarkInterleaveForTests { get; set; }

    /// <summary>Action seams installed by the workspace coordinator
    /// (the Bases surface pattern): the view calls these; the
    /// coordinator owns tabs, dialogs, and announcements.</summary>
    internal Action<HistoryVersionRow>? RestoreFromSurface { get; set; }

    /// <summary>(versionHash, formattedDate, destination, completed) —
    /// the STAGED identity, never the row occupying a position at
    /// commit time (capture-at-staging; red team round 1).</summary>
    internal Action<string, string, string, Action<bool, string?>>?
        RestoreAsFromSurface
    { get; set; }

    internal Action<HistoryDeletedRow, Action<bool, bool>>? RecoverFromSurface
    { get; set; }

    internal Action<string, string, Action<bool, string?>>? RecoverAsFromSurface
    { get; set; }

    /// <summary>Raised after a successful restore so the view moves
    /// focus to the NEW HEAD row (position 0 — WCAG 2.4.3).</summary>
    internal event Action? FocusHeadRequested;

    internal void RequestFocusHead() => FocusHeadRequested?.Invoke();

    // --- Published state (all mutated on the UI context only) ---

    public string? Path => _path;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetField(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(ShowLoading));
            }
        }
    }

    public string? LoadError
    {
        get => _loadError;
        private set => SetField(ref _loadError, value);
    }

    public IReadOnlyList<HistoryDayGroup> DayGroups
    {
        get => _dayGroups;
        private set
        {
            if (SetField(ref _dayGroups, value))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(ShowLoading));
                OnPropertyChanged(nameof(HeaderText));
                OnPropertyChanged(nameof(CanLoadOlder));
                OnPropertyChanged(nameof(CanCompareSelected));
            }
        }
    }

    /// <summary>"Version history, {n} version|versions" — the count is
    /// core's TotalFiltered, which counts EVERY ledger row including
    /// markers (the mac comment claiming otherwise is stale; core is
    /// authoritative — contract H3).</summary>
    public string HeaderText =>
        $"Version history, {_totalFiltered} "
        + (_totalFiltered == 1 ? "version" : "versions");

    public bool ShowLoading => IsLoading && _dayGroups.Count == 0 && LoadError is null;

    public bool ShowEmptyState =>
        _path is not null
        && !IsLoading
        && LoadError is null
        && _dayGroups.Count == 0;

    public bool CanLoadOlder => _nextCursor is not null;

    /// <summary>Markers hidden by default (contract H3); toggling
    /// re-filters the cached rows — never a re-query.</summary>
    public bool ShowMarkers
    {
        get => _showMarkers;
        set
        {
            if (SetField(ref _showMarkers, value))
            {
                RebuildGroups();
            }
        }
    }

    public HistoryInlineDiff? InlineDiff
    {
        get => _inlineDiff;
        private set => SetField(ref _inlineDiff, value);
    }

    public HistorySinceOpenState SinceOpen
    {
        get => _sinceOpen;
        private set => SetField(ref _sinceOpen, value);
    }

    /// <summary>The host opt-in (contract H8), mirrored here by the
    /// workspace; turning it off clears the section.</summary>
    public bool ShowChangesSinceOpen
    {
        get => _showChangesSinceOpen;
        internal set
        {
            if (SetField(ref _showChangesSinceOpen, value) && !value)
            {
                SinceOpen = new HistorySinceOpenState(HistorySinceOpenKind.None, null);
            }
        }
    }

    public IReadOnlyList<HistoryDeletedRow> DeletedRows
    {
        get => _deletedRows;
        private set
        {
            if (SetField(ref _deletedRows, value))
            {
                OnPropertyChanged(nameof(ShowDeletedEmptyState));
            }
        }
    }

    public bool IsDeletedLoading
    {
        get => _isDeletedLoading;
        private set
        {
            if (SetField(ref _isDeletedLoading, value))
            {
                OnPropertyChanged(nameof(ShowDeletedEmptyState));
            }
        }
    }

    public string? DeletedError
    {
        get => _deletedError;
        private set
        {
            if (SetField(ref _deletedError, value))
            {
                OnPropertyChanged(nameof(ShowDeletedEmptyState));
            }
        }
    }

    public bool ShowDeletedEmptyState =>
        !IsDeletedLoading && DeletedError is null && _deletedRows.Count == 0;

    /// <summary>Rows + groups republished wholesale — the view rebinds
    /// on this, never on property granularity.</summary>
    public event EventHandler? Published;

    // --- Note lifecycle (contract H3/H4/H8) ---

    /// <summary>The activation funnel: same-path re-activation is a
    /// no-op; a switch resets per-note state (compare selection,
    /// inline diff, collapse, since-open section) and loads page one.
    /// The since-open funnel (H8) rides the SAME serialized load:
    /// verdict first, publish, then MarkOpened — never the other
    /// order (HINV-8).</summary>
    public void NoteChanged(string? path)
    {
        if (string.Equals(_path, path, StringComparison.Ordinal))
        {
            return;
        }
        _path = path;
        ResetPerNoteState();
        OnPropertyChanged(nameof(Path));
        if (path is null)
        {
            Published?.Invoke(this, EventArgs.Empty);
            return;
        }
        LoadFirstPage(runSinceOpenFunnel: ShowChangesSinceOpen);
    }

    /// <summary>The post-save funnel: the note gained a version row —
    /// reload page one. The HEAD group's id stays stable (its first
    /// visible row is still position 0), so its collapse state
    /// survives; older groups' ids shift with their positions and
    /// their collapse state honestly drops (the position-identity
    /// model, HINV-2). Never touches the since-open baseline (H8:
    /// marking happens only on activation).</summary>
    public void NoteSaved(string path)
    {
        if (!string.Equals(_path, path, StringComparison.Ordinal))
        {
            return;
        }
        LoadFirstPage(runSinceOpenFunnel: false);
    }

    /// <summary>Explicit reload of the current note's list.</summary>
    public void Reload()
    {
        if (_path is not null)
        {
            LoadFirstPage(runSinceOpenFunnel: false);
        }
    }

    private void ResetPerNoteState()
    {
        _loaded.Clear();
        _nextCursor = null;
        _totalFiltered = 0;
        LoadError = null;
        _collapsedGroupIds.Clear();
        _compareSelection.Clear();
        // A switch invalidates any in-flight diff (HINV-5): without
        // the bump, a worker whose (generation, path) still match
        // republishes a pre-switch diff into the fresh activation
        // after an A→B→A round trip (red team round 1).
        _ = Interlocked.Increment(ref _diffGeneration);
        InlineDiff = null;
        SinceOpen = new HistorySinceOpenState(HistorySinceOpenKind.None, null);
        // The note-scoped destination staging drops on switch; a
        // deleted-file staging is vault-scoped and survives.
        if (_destinationStaging is { ForDeletedFile: false })
        {
            DestinationStaging = null;
        }
        // The previous note's rows must never render actionable under
        // the new path (red team round 1): clear the published groups
        // NOW, not when the new load lands — a page-one load is
        // log-length-proportional (HR-1) and a load FAILURE never
        // replaces them at all.
        DayGroups = [];
    }

    private void LoadFirstPage(bool runSinceOpenFunnel)
    {
        if (IsShutDown || _path is not { } path)
        {
            return;
        }
        int generation = Interlocked.Increment(ref _generation);
        // A reload invalidates pagination NOW: core cursors survive
        // appends (only compaction bumps the cursor generation), so a
        // concurrent "Show older versions" click with the pre-save
        // cursor would drop this reload by generation and append an
        // old page onto the stale snapshot — the just-saved version
        // would silently never appear (red team round 1). A null
        // cursor makes that click a no-op instead.
        _nextCursor = null;
        OnPropertyChanged(nameof(CanLoadOlder));
        IsLoading = true;
        StartWork(() => LoadFirstPageBody(generation, path, runSinceOpenFunnel));
    }

    private void LoadFirstPageBody(int generation, string path, bool runSinceOpenFunnel)
    {
        VersionSummaryPage page;
        ChangesSinceOpen? sinceOpen = null;
        string? failure = null;
        try
        {
            page = _session.ListVersions(path, new Paging(null, FirstPageLimit));
            if (runSinceOpenFunnel)
            {
                // An earlier activation's MarkOpened may still be in
                // flight (StartWork bodies are unordered): the verdict
                // must observe the marked baseline, or a rapid A→B→A
                // re-reports changes the user was already shown
                // (HINV-8's cross-activation half). Bounded so a
                // shutdown-skipped mark can never park the drain.
                Volatile.Read(ref _pendingMarkOpened)
                    ?.Wait(TimeSpan.FromSeconds(5));
                // Verdict BEFORE the mark (the pinned core order,
                // HINV-8); a verdict failure is non-fatal to the list.
                try
                {
                    sinceOpen = _session.ChangesSinceLastOpen(path);
                }
                catch (VaultException)
                {
                    sinceOpen = null;
                }
            }
        }
        catch (VaultException exception)
        {
            failure = exception.Message;
            page = new VersionSummaryPage([], null, 0);
        }
        LoadInterleaveForTests?.Invoke();
        Post(() =>
        {
            if (IsShutDown
                || Volatile.Read(ref _generation) != generation
                || !string.Equals(_path, path, StringComparison.Ordinal))
            {
                return;
            }
            IsLoading = false;
            if (failure is not null)
            {
                LoadError = failure;
                Published?.Invoke(this, EventArgs.Empty);
                return;
            }
            LoadError = null;
            _loaded.Clear();
            _loaded.AddRange(page.Items);
            _nextCursor = page.NextCursor;
            _totalFiltered = page.TotalFiltered;
            // A reload replaced the rows the selection pointed at —
            // re-derive by position identity (HINV-2); vanished
            // positions drop.
            PruneCompareSelection();
            // The published diff resets on every reload (H6) — bump so
            // an in-flight diff worker cannot resurrect it afterwards.
            _ = Interlocked.Increment(ref _diffGeneration);
            InlineDiff = null;
            RebuildGroups();
            // Re-guarded on the preference AT PUBLISH: the View-menu
            // toggle can land while the funnel is in flight, and the
            // section must not reappear (nor the baseline be marked)
            // after the user opted out (H8/HINV-8).
            if (sinceOpen is not null && _showChangesSinceOpen)
            {
                PublishSinceOpen(sinceOpen);
                // Marked only AFTER the publish guards passed
                // (HINV-8); a failed mark is non-fatal — the next
                // activation re-reports.
                var marked = new TaskCompletionSource();
                _pendingMarkOpened = marked.Task;
                StartWork(() =>
                {
                    try
                    {
                        MarkOpenedBody(path);
                    }
                    finally
                    {
                        marked.TrySetResult();
                    }
                });
                if (IsShutDown)
                {
                    // StartWork refuses work after shutdown — settle
                    // the pending task so nothing ever waits on it.
                    marked.TrySetResult();
                }
            }
        });
    }

    private void MarkOpenedBody(string path)
    {
        MarkInterleaveForTests?.Invoke();
        try
        {
            _session.MarkOpened(path);
        }
        catch (VaultException)
        {
            // Non-fatal by contract (H8).
        }
    }

    private void PublishSinceOpen(ChangesSinceOpen verdict) =>
        SinceOpen = verdict switch
        {
            ChangesSinceOpen.Diff diff => new HistorySinceOpenState(
                HistorySinceOpenKind.Diff, diff.DiffValue),
            ChangesSinceOpen.BaselineCompacted => new HistorySinceOpenState(
                HistorySinceOpenKind.BaselineCompacted, null),
            _ => new HistorySinceOpenState(HistorySinceOpenKind.None, null),
        };

    /// <summary>"Show older versions" (contract H3): appends the next
    /// cursor page; a cursor-generation bump (core: "history changed,
    /// restart paging") silently reloads page one.</summary>
    public void LoadOlder()
    {
        if (IsShutDown || _path is not { } path || _nextCursor is not { } cursor)
        {
            return;
        }
        int generation = Interlocked.Increment(ref _generation);
        StartWork(() =>
        {
            VersionSummaryPage page;
            bool restart = false;
            string? failure = null;
            try
            {
                page = _session.ListVersions(path, new Paging(cursor, FirstPageLimit));
            }
            catch (VaultException.InvalidArgument)
            {
                restart = true;
                page = new VersionSummaryPage([], null, 0);
            }
            catch (VaultException exception)
            {
                failure = exception.Message;
                page = new VersionSummaryPage([], null, 0);
            }
            Post(() =>
            {
                if (IsShutDown
                    || Volatile.Read(ref _generation) != generation
                    || !string.Equals(_path, path, StringComparison.Ordinal)
                    || !string.Equals(_nextCursor, cursor, StringComparison.Ordinal))
                {
                    return;
                }
                if (restart)
                {
                    LoadFirstPage(runSinceOpenFunnel: false);
                    return;
                }
                if (failure is not null)
                {
                    LoadError = failure;
                    Published?.Invoke(this, EventArgs.Empty);
                    return;
                }
                _loaded.AddRange(page.Items);
                _nextCursor = page.NextCursor;
                _totalFiltered = page.TotalFiltered;
                RebuildGroups();
            });
        });
    }

    // --- Day grouping (contract H4) — pure over the cached rows ---

    private void RebuildGroups()
    {
        var visible = _loaded
            .Where(summary => _showMarkers || !summary.IsMarker)
            .ToList();
        var groups = new List<HistoryDayGroup>();
        List<HistoryVersionRow>? run = null;
        DateTime runDay = default;
        string runId = string.Empty;
        foreach (VersionSummary summary in visible)
        {
            DateTime local = DateTimeOffset
                .FromUnixTimeMilliseconds(summary.TimestampMs)
                .ToLocalTime()
                .DateTime;
            DateTime day = local.Date;
            if (run is null || day != runDay)
            {
                FlushRun(groups, run, runDay, runId);
                run = [];
                runDay = day;
                long dayStartUnix = new DateTimeOffset(day).ToUnixTimeSeconds();
                runId = $"{dayStartUnix}#{summary.PositionFromTail}";
            }
            run.Add(MakeRow(summary, local));
        }
        FlushRun(groups, run, runDay, runId);
        DayGroups = groups;
        Published?.Invoke(this, EventArgs.Empty);
    }

    private void FlushRun(
        List<HistoryDayGroup> groups,
        List<HistoryVersionRow>? run,
        DateTime runDay,
        string runId)
    {
        if (run is null || run.Count == 0)
        {
            return;
        }
        var group = new HistoryDayGroup(runId, DayTitle(runDay), run)
        {
            IsCollapsed = _collapsedGroupIds.Contains(runId),
        };
        group.PropertyChanged += (sender, changed) =>
        {
            if (changed.PropertyName == nameof(HistoryDayGroup.IsCollapsed)
                && sender is HistoryDayGroup toggled)
            {
                if (toggled.IsCollapsed)
                {
                    _ = _collapsedGroupIds.Add(toggled.Id);
                }
                else
                {
                    _ = _collapsedGroupIds.Remove(toggled.Id);
                }
            }
        };
        groups.Add(group);
    }

    private HistoryVersionRow MakeRow(VersionSummary summary, DateTime local)
    {
        var row = new HistoryVersionRow(
            summary,
            FormatAbsolute(local),
            RelativePhrase(local, DateTime.Now));
        row.SelectedForCompare = _compareSelection.Contains(summary.PositionFromTail);
        return row;
    }

    /// <summary>"Today" / "Yesterday" / full local date — the mac
    /// dayTitle shape (recorded labels).</summary>
    internal static string DayTitle(DateTime day)
    {
        DateTime today = DateTime.Today;
        if (day == today)
        {
            return "Today";
        }
        if (day == today.AddDays(-1))
        {
            return "Yesterday";
        }
        return day.ToString("D", CultureInfo.CurrentCulture);
    }

    /// <summary>The corpus-shaped host date ("July 19, 2026 at
    /// 9:41 AM") — used for rows AND the RestoredVersionFrom payload
    /// (contract H1: the date is host-formatted before the FFI).</summary>
    internal static string FormatAbsolute(DateTime local) =>
        local.ToString("MMMM d, yyyy 'at' h:mm tt", CultureInfo.CurrentCulture);

    /// <summary>A minimal relative phrase (recorded label class — mac
    /// uses RelativeDateTimeFormatter; .NET has none).</summary>
    internal static string RelativePhrase(DateTime local, DateTime now)
    {
        TimeSpan age = now - local;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }
        if (age.TotalMinutes < 1)
        {
            return "just now";
        }
        if (age.TotalHours < 1)
        {
            int minutes = (int)age.TotalMinutes;
            return minutes == 1 ? "1 minute ago" : $"{minutes} minutes ago";
        }
        if (age.TotalDays < 1)
        {
            int hours = (int)age.TotalHours;
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }
        if (age.TotalDays < 7)
        {
            int days = (int)age.TotalDays;
            return days == 1 ? "1 day ago" : $"{days} days ago";
        }
        int weeks = (int)(age.TotalDays / 7);
        return weeks == 1 ? "1 week ago" : $"{weeks} weeks ago";
    }

    // --- Compare (contract H6) ---

    /// <summary>At most two selected; a third drops the OLDER of the
    /// current two (higher PositionFromTail); dedup by position.</summary>
    public void ToggleCompareSelection(uint position)
    {
        if (_compareSelection.Contains(position))
        {
            _ = _compareSelection.Remove(position);
        }
        else
        {
            _compareSelection.Add(position);
            if (_compareSelection.Count > 2)
            {
                uint oldest = _compareSelection
                    .Where(candidate => candidate != position)
                    .Max();
                _ = _compareSelection.Remove(oldest);
            }
        }
        SyncCompareCheckboxes();
        OnPropertyChanged(nameof(CanCompareSelected));
    }

    public bool CanCompareSelected => _compareSelection.Count == 2;

    internal IReadOnlyList<uint> CompareSelectionForTests => _compareSelection;

    private void PruneCompareSelection()
    {
        var live = new HashSet<uint>(_loaded.Select(summary => summary.PositionFromTail));
        _ = _compareSelection.RemoveAll(position => !live.Contains(position));
        OnPropertyChanged(nameof(CanCompareSelected));
    }

    private void SyncCompareCheckboxes()
    {
        foreach (HistoryDayGroup group in _dayGroups)
        {
            foreach (HistoryVersionRow row in group.Rows)
            {
                row.SelectedForCompare = _compareSelection.Contains(row.PositionFromTail);
            }
        }
    }

    /// <summary>Two-version compare: endpoints oriented older = from
    /// (higher position), newer = to; renders under the section
    /// header (anchor null).</summary>
    public void CompareSelected()
    {
        if (_path is not { } path || _compareSelection.Count != 2)
        {
            return;
        }
        uint older = Math.Max(_compareSelection[0], _compareSelection[1]);
        uint newer = Math.Min(_compareSelection[0], _compareSelection[1]);
        string? fromHash = HashAtPosition(older);
        string? toHash = HashAtPosition(newer);
        if (fromHash is null || toHash is null)
        {
            return;
        }
        RunDiff(path, fromHash, toHash, anchor: null);
    }

    /// <summary>Per-row compare: this version → the CURRENT saved
    /// content (the log tail after any save), inline under the row.</summary>
    public void CompareAgainstCurrent(HistoryVersionRow row)
    {
        if (_path is not { } path)
        {
            return;
        }
        string? currentHash = CurrentContentHashProvider?.Invoke();
        if (currentHash is null || currentHash.Length == 0)
        {
            // The mac guard shape: no comparable current state is a
            // SILENT no-op — never a host-composed error (HINV-1).
            return;
        }
        RunDiff(path, row.ContentHashAfter, currentHash, anchor: row.PositionFromTail);
    }

    private string? HashAtPosition(uint position) =>
        _loaded.FirstOrDefault(summary => summary.PositionFromTail == position)
            ?.ContentHashAfter;

    private void RunDiff(string path, string fromHash, string toHash, uint? anchor)
    {
        if (IsShutDown)
        {
            return;
        }
        int generation = Interlocked.Increment(ref _diffGeneration);
        StartWork(() =>
        {
            StructuredDiff? diff = null;
            string? failure = null;
            try
            {
                diff = _session.DiffVersions(path, fromHash, toHash);
            }
            catch (VaultException exception)
            {
                failure = exception.Message;
            }
            DiffInterleaveForTests?.Invoke();
            Post(() =>
            {
                if (IsShutDown
                    || Volatile.Read(ref _diffGeneration) != generation
                    || !string.Equals(_path, path, StringComparison.Ordinal))
                {
                    return;
                }
                InlineDiff = new HistoryInlineDiff(anchor, diff, failure);
            });
        });
    }

    public void CloseInlineDiff()
    {
        // Closing invalidates any in-flight diff too — a late publish
        // must not resurrect the dismissed surface (HINV-5).
        _ = Interlocked.Increment(ref _diffGeneration);
        InlineDiff = null;
    }

    // --- The inline destination row (contracts H9/H10, HD-2) ---

    /// <summary>See <see cref="HistoryDestinationStaging"/>. Draft
    /// updates mutate WITHOUT a change notification (a per-keystroke
    /// re-render would orphan the box the user is typing into);
    /// open, refusal, and close notify.</summary>
    public HistoryDestinationStaging? DestinationStaging
    {
        get => _destinationStaging;
        private set => SetField(ref _destinationStaging, value);
    }

    /// <summary>The mac restore-as prompt copy for a live version.</summary>
    internal static string RestoreAsNotice(string formattedDate) =>
        $"Save a copy of the version from {formattedDate} to a new file.";

    /// <summary>The pinned H10 collision sentence — the ONLY thing
    /// telling the user why the destination row appeared.</summary>
    internal static string DeletedCollisionNotice(string path) =>
        $"A file already exists at {path}. Restore the deleted file to "
        + "a different location.";

    internal void OpenRestoreAsStaging(HistoryVersionRow row, string seededDraft)
    {
        if (_path is not { } path)
        {
            return;
        }
        // Same-row toggle closes (the current affordance).
        if (_destinationStaging is { ForDeletedFile: false } open
            && string.Equals(
                open.VersionHash, row.ContentHashAfter, StringComparison.Ordinal))
        {
            CloseDestinationStaging();
            return;
        }
        DestinationStaging = new HistoryDestinationStaging(
            ForDeletedFile: false,
            NotePath: path,
            VersionHash: row.ContentHashAfter,
            FormattedDate: row.AbsoluteDate,
            Notice: RestoreAsNotice(row.AbsoluteDate),
            Draft: seededDraft,
            Refusal: null);
    }

    internal void OpenRecoverAsStaging(HistoryDeletedRow row, string seededDraft) =>
        DestinationStaging = new HistoryDestinationStaging(
            ForDeletedFile: true,
            NotePath: row.Path,
            VersionHash: string.Empty,
            FormattedDate: string.Empty,
            Notice: DeletedCollisionNotice(row.Path),
            Draft: seededDraft,
            Refusal: null);

    internal void UpdateDestinationDraft(string draft)
    {
        if (_destinationStaging is { } staging)
        {
            _destinationStaging = staging with { Draft = draft };
        }
    }

    internal void SetDestinationRefusal(string message)
    {
        if (_destinationStaging is { } staging)
        {
            DestinationStaging = staging with { Refusal = message };
        }
    }

    public void CloseDestinationStaging() => DestinationStaging = null;

    // --- Deleted segment (contract H10) ---

    /// <summary>Lazy on first visit, reloaded on later visits — one
    /// 200-item page, no pagination UI (HR-3).</summary>
    public void LoadDeletedFiles()
    {
        if (IsShutDown)
        {
            return;
        }
        _deletedLoaded = true;
        int generation = Interlocked.Increment(ref _deletedGeneration);
        IsDeletedLoading = true;
        StartWork(() =>
        {
            DeletedFilePage page;
            string? failure = null;
            try
            {
                page = _session.ListDeletedFiles(new Paging(null, DeletedPageLimit));
            }
            catch (VaultException exception)
            {
                failure = exception.Message;
                page = new DeletedFilePage([], null, 0);
            }
            Post(() =>
            {
                if (IsShutDown
                    || Volatile.Read(ref _deletedGeneration) != generation)
                {
                    return;
                }
                IsDeletedLoading = false;
                if (failure is not null)
                {
                    DeletedError = failure;
                    Published?.Invoke(this, EventArgs.Empty);
                    return;
                }
                DeletedError = null;
                DeletedRows = page.Items.Select(MakeDeletedRow).ToArray();
                Published?.Invoke(this, EventArgs.Empty);
            });
        });
    }

    /// <summary>Reload only if the segment was ever visited — the
    /// post-recover refresh (H10).</summary>
    public void ReloadDeletedFilesIfLoaded()
    {
        if (_deletedLoaded)
        {
            LoadDeletedFiles();
        }
    }

    internal static HistoryDeletedRow MakeDeletedRow(DeletedFileEntry entry)
    {
        string deletedText = entry.DeletedAtMs is { } deletedAt
            ? "Deleted " + RelativePhrase(
                DateTimeOffset.FromUnixTimeMilliseconds(deletedAt)
                    .ToLocalTime().DateTime,
                DateTime.Now)
            : "Deletion time unknown";
        string? sizeText = entry.Recoverable && entry.SizeBytes is { } size
            ? FormatBytes(size)
            : null;
        return new HistoryDeletedRow(entry, deletedText, sizeText);
    }

    /// <summary>The mac ByteCountFormatter .file mirror (1000-based,
    /// the "Zero KB" idiom, precision growing with the unit — KB
    /// integral, MB one place, GB two) so a deleted row reads the same
    /// size on both platforms (red team round 1).</summary>
    internal static string FormatBytes(ulong bytes) => bytes switch
    {
        0 => "Zero KB",
        1 => "1 byte",
        < 1000 => $"{bytes} bytes",
        < 1_000_000 =>
            $"{Math.Round(bytes / 1000.0, MidpointRounding.AwayFromZero)} KB",
        < 1_000_000_000 => $"{bytes / 1_000_000.0:0.#} MB",
        _ => $"{bytes / 1_000_000_000.0:0.##} GB",
    };

    internal override void Shutdown()
    {
        base.Shutdown();
        Interlocked.Increment(ref _generation);
        Interlocked.Increment(ref _deletedGeneration);
        Interlocked.Increment(ref _diffGeneration);
    }
}
