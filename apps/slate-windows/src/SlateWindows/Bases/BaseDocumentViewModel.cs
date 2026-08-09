// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Bases;

/// <summary>The dock target (the mac BasesDockTarget twin), compared
/// byte-exact on kind+key.</summary>
internal enum BasesDockTargetKind
{
    File,
    SavedQuery,
    Dashboard,
}

internal sealed record BasesDockTargetState(
    BasesDockTargetKind Kind,
    string Key,
    string Name);

/// <summary>The document's load posture (the mac LoadState twin).
/// Degraded is READY WITH A BANNER: content renders under the message
/// (contract C4); Failed is terminal until Retry.</summary>
internal enum BaseLoadState
{
    Loading,
    Ready,
    Degraded,
    Failed,
}

/// <summary>
/// W4-6 (#738): the per-source Bases document — the mac BaseDocument
/// twin. Owns the native handle, the view list, the executed result,
/// and the transient sort; every execution, ordering, and spoken
/// string is core's (contract C1). One instance per source key,
/// shared by every tab on that source (contract C3); the workspace
/// registry owns creation and shutdown.
///
/// Threading: every FFI touch runs inside <see cref="StartWork"/>
/// bodies under one lock, so handle replacement can never race an
/// in-flight execute; publications marshal through Post and re-check
/// generation + shutdown (INV-5). The handle is closed exactly once —
/// on replacement inside Load, on detach, or on Shutdown (INV-2).
/// </summary>
internal sealed class BaseDocumentViewModel : PanelWorkScheduler
{
    private readonly VaultSession _session;
    private readonly Action<A11yEvent> _announce;
    private readonly object _ffiLock = new();
    private ulong? _handle;
    private int _generation;
    private BaseLoadState _state = BaseLoadState.Loading;
    private string? _stateMessage;
    private IReadOnlyList<BaseViewSummary> _views = [];
    private int _activeViewIndex;
    private BasesResultSet? _result;
    private (int ColumnIndex, bool Ascending)? _sortState;
    private CancelToken? _executeCancel;

    public BaseDocumentViewModel(
        VaultSession session,
        string path,
        Action<A11yEvent> announce,
        bool synchronousForTests = false)
        : base(synchronousForTests)
    {
        _session = session;
        Path = path;
        _announce = announce;
    }

    private BaseDocumentViewModel(
        VaultSession session,
        string savedQueryId,
        string savedQueryName,
        Action<A11yEvent> announce,
        bool synchronousForTests)
        : base(synchronousForTests)
    {
        _session = session;
        Path = string.Empty;
        _savedQueryId = savedQueryId;
        _savedQueryName = savedQueryName;
        _announce = announce;
    }

    /// <summary>A saved-query document (the mac BaseDocumentSource
    /// .savedQuery arm): an EPHEMERAL query handle — one view, never
    /// editable as a file, so SaveSortToView refuses silently and the
    /// transient sort stays transient.</summary>
    public static BaseDocumentViewModel ForSavedQuery(
        VaultSession session,
        string id,
        string name,
        Action<A11yEvent> announce,
        bool synchronousForTests = false) =>
        new(session, id, name, announce, synchronousForTests);

    private readonly string? _savedQueryId;
    private string? _savedQueryName;

    public bool IsSavedQuery => _savedQueryId is not null;

    /// <summary>Registry rename propagation: the display name follows
    /// the registry while identity (the id) never changes.</summary>
    internal void UpdateSavedQueryName(string name)
    {
        if (_savedQueryId is null)
        {
            return;
        }
        _savedQueryName = name;
        OnPropertyChanged(nameof(DisplayName));
    }

    internal string? SavedQueryId => _savedQueryId;

    /// <summary>Vault-relative path — the source identity for
    /// file-backed documents (empty for saved queries). Compared
    /// byte-exact (Ordinal) everywhere, never culture-aware.</summary>
    public string Path { get; }

    public string DisplayName => _savedQueryName
        ?? System.IO.Path.GetFileNameWithoutExtension(Path);

    public BaseLoadState State
    {
        get => _state;
        private set
        {
            if (SetField(ref _state, value))
            {
                NotifyStateChanged();
            }
        }
    }

    /// <summary>The degraded/failed banner sentence — mac wording
    /// verbatim (contract C4). Null when Ready/Loading.</summary>
    public string? StateMessage
    {
        get => _stateMessage;
        private set => SetField(ref _stateMessage, value);
    }

    public IReadOnlyList<BaseViewSummary> Views
    {
        get => _views;
        private set => SetField(ref _views, value);
    }

    public int ActiveViewIndex
    {
        get => _activeViewIndex;
        private set => SetField(ref _activeViewIndex, value);
    }

    public string? ActiveViewName =>
        _views.Count > _activeViewIndex ? _views[_activeViewIndex].Name : null;

    /// <summary>Core's result, untransformed (INV-1). Null until the
    /// first successful execute; retained across failed refreshes so
    /// a failure never blanks the pane (contract C9).</summary>
    public BasesResultSet? Result
    {
        get => _result;
        private set
        {
            if (SetField(ref _result, value))
            {
                NotifyStateChanged();
            }
        }
    }

    /// <summary>The published transient sort — set ONLY after the
    /// re-execute lands, so the indicator can never contradict the
    /// rows (contract C6).</summary>
    public (int ColumnIndex, bool Ascending)? SortState
    {
        get => _sortState;
        private set => SetField(ref _sortState, value);
    }

    /// <summary>Rows + columns republished — the view rebinds the grid
    /// on this, never on property-change granularity.</summary>
    public event EventHandler? ResultPublished;

    private string _quickFilterText = string.Empty;
    private bool _quickFilterActive;

    /// <summary>The draft text — bound by the header field. Setting it
    /// does NOT execute; the view debounces into
    /// <see cref="ApplyQuickFilter"/> (150 ms, the mac cadence).</summary>
    public string QuickFilterText
    {
        get => _quickFilterText;
        set => SetField(ref _quickFilterText, value);
    }

    /// <summary>True when the EXECUTED result was filtered — drives
    /// the count readout denominator and the summary prefix. Distinct
    /// from a non-empty draft the user has not applied yet.</summary>
    public bool QuickFilterActive
    {
        get => _quickFilterActive;
        private set => SetField(ref _quickFilterActive, value);
    }

    /// <summary>Transiency (contract C5): the filter lives HERE and in
    /// the execute argument only — never in the file; cleared on load,
    /// view switch, and tab switch-away.</summary>
    public void ClearQuickFilterState()
    {
        QuickFilterText = string.Empty;
        QuickFilterActive = false;
    }

    /// <summary>Execute the active view with the current draft filter
    /// and announce core's count (BaseQuickFilterResult). An empty
    /// draft clears: it re-executes unfiltered and announces the
    /// unfiltered count the same way — the mac shape.</summary>
    public void ApplyQuickFilter()
    {
        if (IsShutDown || State is BaseLoadState.Failed or BaseLoadState.Loading)
        {
            return;
        }
        int generation = Interlocked.Increment(ref _generation);
        StartWork(() => ExecuteBody(
            generation, (uint)_activeViewIndex, announceQuickFilterCount: true));
    }

    /// <summary>W4-6 phase C (contract C8): the cell-write route,
    /// installed by the workspace registry — the document VM never
    /// writes; it hands (row, column, value-or-null-for-delete) to the
    /// workspace coordinator, which owns the tab/tabless route split
    /// and the post-write funnel.</summary>
    internal Action<BasesRow, BasesColumn, PropertyValue?>? ApplyPropertyEdit { get; set; }

    /// <summary>Raised (with the file path) right after THIS document
    /// wrote its own .base definition (save-sort, builder edits) — the
    /// workspace registers the watcher-echo suppression here, at the
    /// WRITE, never at command dispatch (red team round 3: an echo
    /// registered before validation left a 5 s window where a real
    /// external change was consumed as self).</summary>
    internal Action<string>? DefinitionSelfSaved { get; set; }

    /// <summary>Row-action routes, installed by the workspace beside
    /// ApplyPropertyEdit: the surface's context menu and the palette
    /// commands share ONE implementation per action.</summary>
    internal Action<BasesRow>? OpenRowFromSurface { get; set; }

    internal Action<BasesRow>? CopyLinkFromSurface { get; set; }

    internal Action<BasesRow>? ShowBacklinksFromSurface { get; set; }

    /// <summary>Surface-originated canonical announcements (edit
    /// canceled, read-only refusals, validation refusals) — one
    /// announce seam for the whole surface.</summary>
    internal void AnnounceForSurface(A11yEvent @event) => _announce(@event);

    private string? _membershipSignature;
    private bool _membershipBaselinePublished;

    /// <summary>Funnel outcomes awaiting the next publish — a QUEUE,
    /// not a slot (red team round 1: a second write before the first
    /// publish overwrote the slot and the first write's terminal
    /// outcome was never spoken; a generation-bailed execute orphaned
    /// it). Every publish path — result, degraded, failed — drains the
    /// whole queue, so a superseding execute carries its predecessors'
    /// outcomes. UI-thread only.</summary>
    private readonly List<(int FunnelId, Action? Continuation)> _pendingFunnelOutcomes = [];

    /// <summary>Raised (funnelId, audioSummary) when a funnel-tagged
    /// refresh changed the row-membership multiset — the workspace
    /// dedupes across surfaces and announces BasesRefreshUpdated
    /// (contract C9). Baseline publishes are silent.</summary>
    internal event Action<int, string>? MembershipChanged;

    /// <summary>The post-write refresh (contract C9): re-execute with
    /// the CURRENT filter retained, report membership change under the
    /// funnel id, then run the continuation (the cell-outcome
    /// announcement) after the rows landed.</summary>
    internal void RefreshForFunnel(int funnelId, Action? onPublished = null)
    {
        if (IsShutDown || State is BaseLoadState.Failed or BaseLoadState.Loading)
        {
            onPublished?.Invoke();
            return;
        }
        int generation = Interlocked.Increment(ref _generation);
        _pendingFunnelOutcomes.Add((funnelId, onPublished));
        StartWork(() => ExecuteBody(generation, (uint)_activeViewIndex));
    }

    /// <summary>Drain every pending funnel outcome at a publish: the
    /// LATEST funnel id tags the membership comparison (one deduped
    /// BasesRefreshUpdated per pass), and every queued continuation
    /// runs in write order against the rows that just landed.</summary>
    private (int FunnelId, Action?[] Continuations) DrainFunnelOutcomes()
    {
        if (_pendingFunnelOutcomes.Count == 0)
        {
            return (0, []);
        }
        int funnelId = _pendingFunnelOutcomes[^1].FunnelId;
        Action?[] continuations =
            _pendingFunnelOutcomes.Select(outcome => outcome.Continuation).ToArray();
        _pendingFunnelOutcomes.Clear();
        return (funnelId, continuations);
    }

    /// <summary>The ONE settlement for every publish path — result,
    /// sort, degraded, failed (red team round 2: SortBody's posts
    /// bypassed PublishResult, so a queued write outcome went
    /// unspoken). With a result: compare membership under the latest
    /// funnel id, update the baseline. Always: run every queued
    /// continuation in write order.</summary>
    private void SettleFunnelOutcomes(BasesResultSet? result)
    {
        (int funnelId, Action?[] continuations) = DrainFunnelOutcomes();
        if (result is not null)
        {
            string signature = MembershipSignatureOf(result);
            if (funnelId != 0
                && _membershipBaselinePublished
                && !string.Equals(
                    _membershipSignature, signature, StringComparison.Ordinal))
            {
                MembershipChanged?.Invoke(funnelId, result.AudioSummary);
            }
            _membershipSignature = signature;
            _membershipBaselinePublished = true;
        }
        foreach (Action? continuation in continuations)
        {
            continuation?.Invoke();
        }
    }

    private static string MembershipSignatureOf(BasesResultSet result)
    {
        var keys = new List<string>(result.Rows.Length);
        foreach (BasesRow row in result.Rows)
        {
            keys.Add(row.FilePath + "|" + (row.TaskOrdinal?.ToString() ?? "-"));
        }
        keys.Sort(StringComparer.Ordinal);
        return string.Join(";", keys);
    }

    /// <summary>The picker/command announcement — "Base view: {name}."
    /// Callers announce AFTER a successful SelectView so the sentence
    /// follows the state it describes.</summary>
    public void AnnounceViewSelected(string name) =>
        _announce(new A11yEvent.BasesViewSelected(name));

    /// <summary>"Base refreshed." — the explicit-refresh sentence
    /// (command + header button), never spoken by Load itself (INV-4).</summary>
    public void AnnounceRefreshed() =>
        _announce(new A11yEvent.BaseRefreshed());

    /// <summary>slate.bases.saveSortToView (the mac twin verbatim):
    /// persist the published transient sort into the view's slate
    /// state, clear the engine sort, reload views, re-execute. The
    /// YAML fragment is mac's host-composed shape byte-for-byte.
    /// Announces BaseSortSavedToView on success, BasesSortSaveFailed
    /// on failure; silent when no sort is applied (mac returns nil).</summary>
    public void SaveSortToView()
    {
        if (IsSavedQuery)
        {
            // Ephemeral query handles cannot be edited as .base files
            // (core's own refusal); mac's save-sort is silent here.
            return;
        }
        if (IsShutDown
            || State is BaseLoadState.Failed or BaseLoadState.Loading
            || Result is not { } result
            || SortState is not { } sort
            || sort.ColumnIndex >= result.Columns.Length)
        {
            return;
        }
        BasesColumn column = result.Columns[sort.ColumnIndex];
        int generation = Interlocked.Increment(ref _generation);
        StartWork(() =>
        {
            BaseViewSummary[] views;
            try
            {
                lock (_ffiLock)
                {
                    if (Volatile.Read(ref _generation) != generation
                        || _handle is not { } handle)
                    {
                        return;
                    }
                    DrainPendingSortClearsLocked(handle);
                    _session.BaseApplyEdit(
                        handle,
                        new BaseEdit.SetSlateSort(
                            (uint)_activeViewIndex,
                            SlateSortYaml(column.Id, sort.Ascending)));
                    _session.BaseSetTransientSort(
                        handle, (uint)_activeViewIndex, columnId: null, ascending: true);
                    views = _session.BaseViews(handle);
                }
            }
            catch (VaultException failure)
            {
                Post(() =>
                {
                    if (Volatile.Read(ref _generation) == generation)
                    {
                        _announce(new A11yEvent.BasesSortSaveFailed(failure.Message));
                        // No publish follows a failed save — queued
                        // funnel outcomes settle here (round 2).
                        SettleFunnelOutcomes(null);
                    }
                });
                return;
            }
            // The write LANDED (even if a newer generation supersedes
            // the publish below) — the watcher echo is coming.
            Post(() => DefinitionSelfSaved?.Invoke(Path));
            Post(() =>
            {
                if (Volatile.Read(ref _generation) != generation)
                {
                    return;
                }
                Views = views;
                SortState = null;
                _announce(new A11yEvent.BaseSortSavedToView(column.Label, sort.Ascending));
            });
            ExecuteBody(generation, (uint)_activeViewIndex, freshViews: views);
        });
    }

    /// <summary>The active view's EDITABLE query JSON (as-authored,
    /// no inherited folding) — the builder's edit-context seed. Runs
    /// on the scheduler: the call shares the FFI lock with executes,
    /// so a dispatcher caller would freeze behind a slow query
    /// (INV-6). Continues on the UI context with (json, null) or
    /// (null, failureMessage).</summary>
    public void ViewEditQueryJson(Action<string?, string?> continuation)
    {
        if (IsShutDown)
        {
            continuation(null, "The base is not open.");
            return;
        }
        StartWork(() =>
        {
            string? json = null;
            string? failure = null;
            try
            {
                lock (_ffiLock)
                {
                    if (_handle is { } handle)
                    {
                        json = _session.BaseViewEditQueryJson(
                            handle, (uint)_activeViewIndex);
                    }
                    else
                    {
                        failure = "The base is not open.";
                    }
                }
            }
            catch (VaultException exception)
            {
                failure = exception.Message;
            }
            Post(() => continuation(json, failure));
        });
    }

    /// <summary>Apply the builder's minimal edit batch (contract C11):
    /// one BaseApplyEdits (validated + serialized whole in core, CAS
    /// guarded), then views reload + re-execute. Runs on the scheduler
    /// (INV-6 — apply-edits shares the FFI lock with executes); every
    /// refusal path announces BasesViewSaveFailed before the
    /// UI-context continuation gets false, so a save is never a
    /// silent no-op (red team round 1).</summary>
    public void ApplyBuilderEdits(IReadOnlyList<BaseEdit> edits, Action<bool> completed)
    {
        if (IsShutDown
            || IsSavedQuery
            || State is BaseLoadState.Failed or BaseLoadState.Loading)
        {
            _announce(new A11yEvent.BasesViewSaveFailed(
                "the base is not ready to accept edits"));
            completed(false);
            return;
        }
        int generation = Interlocked.Increment(ref _generation);
        BaseEdit[] batch = edits.ToArray();
        StartWork(() =>
        {
            BaseViewSummary[] views;
            try
            {
                lock (_ffiLock)
                {
                    if (_handle is not { } handle)
                    {
                        Post(() =>
                        {
                            _announce(new A11yEvent.BasesViewSaveFailed(
                                "the base is not open"));
                            completed(false);
                        });
                        return;
                    }
                    _session.BaseApplyEdits(handle, batch);
                    views = _session.BaseViews(handle);
                }
            }
            catch (VaultException failure)
            {
                Post(() =>
                {
                    _announce(new A11yEvent.BasesViewSaveFailed(failure.Message));
                    completed(false);
                });
                return;
            }
            // The write LANDED — the watcher echo is coming.
            Post(() => DefinitionSelfSaved?.Invoke(Path));
            Post(() =>
            {
                if (Volatile.Read(ref _generation) == generation)
                {
                    Views = views;
                    SortState = null;
                }
                completed(true);
            });
            ExecuteBody(generation, (uint)ClampedViewIndex(views.Length), freshViews: views);
        });
    }

    /// <summary>Mac's slateSortYAML shape.</summary>
    internal static string SlateSortYaml(string columnId, bool ascending) =>
        "- property: " + QuoteYamlString(columnId) + "\n"
        + "  direction: " + (ascending ? "ASC" : "DESC");

    /// <summary>Mac's quoteYAMLString — THE one YAML string quoter for
    /// the Bases family (the builder shares it). Red team round 1: the
    /// first port's control-character Replaces were no-ops
    /// (`.Replace("\n", "\n")`), emitting broken YAML for ids with
    /// newlines/tabs.</summary>
    internal static string QuoteYamlString(string value)
    {
        string escaped = value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
        return "\"" + escaped + "\"";
    }

    /// <summary>The filter the LAST EXECUTE ran with — the where-am-I
    /// readback describes executed state, never the draft still
    /// debouncing in the field (red team round 1).</summary>
    private string? _executedQuickFilter;

    /// <summary>slate.bases.whereAmI — core joins the present parts.</summary>
    public A11yEvent WhereAmIEvent() => new A11yEvent.BaseWhereAmI(
        DisplayName,
        ActiveViewName,
        QuickFilterActive ? _executedQuickFilter : null);

    /// <summary>slate.bases.resultsPopover — the readback rides only
    /// while a filter is active (the mac shape).</summary>
    public A11yEvent? ResultsPopoverEvent()
    {
        if (Result is not { } result)
        {
            return null;
        }
        string? whereAmI = QuickFilterActive
            ? SlateUniffiMethods.A11yRender(WhereAmIEvent()).Text
            : null;
        return new A11yEvent.BaseResultsPopover(result.AudioSummary, whereAmI);
    }

    /// <summary>Announce the event through the document's channel —
    /// the workspace command layer's post seam.</summary>
    public void AnnounceEvent(A11yEvent @event) => _announce(@event);

    /// <summary>slate.bases.exportCsv/exportMarkdown/copyMarkdown:
    /// core composes the bytes (contract C14); the CALLER owns the
    /// C14 scope choice (includeQuickFilter), delivery, and its
    /// announcement. Runs on the scheduler — base_export is
    /// result-proportional and shares the FFI lock with executes, so
    /// a dispatcher caller would freeze behind a slow query (INV-6;
    /// red team round 1 blocker). The continuation posts to the UI
    /// context with null after an announced failure.</summary>
    public void ExportText(
        ExportFormat format, bool includeQuickFilter, Action<string?> deliver)
    {
        if (IsShutDown || State is BaseLoadState.Failed or BaseLoadState.Loading)
        {
            deliver(null);
            return;
        }
        StartWork(() =>
        {
            string? text = null;
            string? failureMessage = null;
            try
            {
                lock (_ffiLock)
                {
                    if (_handle is { } handle)
                    {
                        text = _session.BaseExport(
                            handle,
                            (uint)_activeViewIndex,
                            format,
                            includeQuickFilter ? NormalizedFilter() : null);
                    }
                }
            }
            catch (VaultException failure)
            {
                failureMessage = failure.Message;
            }
            Post(() =>
            {
                if (IsShutDown)
                {
                    return;
                }
                if (failureMessage is { } message)
                {
                    _announce(new A11yEvent.BasesViewExportFailed(message));
                }
                deliver(text);
            });
        });
    }

    /// <summary>The surface-request seams (the mac token pattern):
    /// the ACTIVE tab's surface consumes these — WPF renders only the
    /// selected tab's content, so a group's inactive tabs hold no
    /// live surface.</summary>
    internal event Action? QuickFilterFocusRequested;

    internal event Action<BaseRendererOverride>? RendererOverrideRequested;

    internal event Action? SortCurrentColumnRequested;

    internal event Action? EditSelectedPropertyRequested;

    internal void RequestQuickFilterFocus() => QuickFilterFocusRequested?.Invoke();

    internal void RequestRendererOverride(BaseRendererOverride mode) =>
        RendererOverrideRequested?.Invoke(mode);

    internal void RequestSortCurrentColumn() => SortCurrentColumnRequested?.Invoke();

    internal void RequestEditSelectedProperty() =>
        EditSelectedPropertyRequested?.Invoke();

    private BasesRow? _selectedRow;

    /// <summary>The grid's current row, published by the active
    /// surface (mac updateActiveBaseSelection) — the row commands'
    /// target; null announces BasesRowSelectionNeeded.</summary>
    public BasesRow? SelectedRow
    {
        get => _selectedRow;
        internal set => SetField(ref _selectedRow, value);
    }

    public bool ShowEmptyState =>
        State is BaseLoadState.Ready or BaseLoadState.Degraded
        && Result is { Rows.Length: 0 };

    /// <summary>Derived-property re-raise (the W4-4 bare-setter class:
    /// every setter feeding a computed property must notify it).</summary>
    private void NotifyStateChanged() => OnPropertyChanged(nameof(ShowEmptyState));

    /// <summary>Open (or reopen) the source and execute the active
    /// view. The full-reload shape: close, open, views, execute — the
    /// mac `load` twin. Never announces by itself (INV-4); the
    /// explicit-refresh caller announces BaseRefreshed.</summary>
    public void Load()
    {
        if (IsShutDown)
        {
            // A shut-down document must not even MUTATE state: the
            // scheduler would refuse the body, leaving a permanent
            // "Loading" lie on whatever UI still observes this VM.
            return;
        }
        int generation = Interlocked.Increment(ref _generation);
        State = BaseLoadState.Loading;
        StateMessage = null;
        ClearQuickFilterState();
        // The reopen discards the engine's transient sort with the
        // handle, so the published indicator must fall with it — a
        // retained tuple would render a sort the rows don't have and
        // let SaveSortToView persist the fiction (red team round 1).
        SortState = null;
        StartWork(() => LoadBody(generation));
    }

    private void LoadBody(int generation)
    {
        ulong handle;
        BaseViewSummary[] views;
        try
        {
            lock (_ffiLock)
            {
                if (Volatile.Read(ref _generation) != generation)
                {
                    return;
                }
                CloseHandleLocked();
                handle = _savedQueryId is { } savedQueryId
                    ? _session.OpenSavedQuery(savedQueryId)
                    : _session.OpenBase(Path);
                _handle = handle;
                views = _session.BaseViews(handle);
            }
        }
        catch (VaultException exception)
        {
            Post(() =>
            {
                if (Volatile.Read(ref _generation) != generation)
                {
                    return;
                }
                Views = [];
                PublishFailed(BasePhrase.OpenFailed(exception));
            });
            return;
        }
        Post(() =>
        {
            if (Volatile.Read(ref _generation) != generation)
            {
                return;
            }
            Views = views;
            if (ActiveViewIndex >= views.Length)
            {
                ActiveViewIndex = 0;
            }
        });
        // The fresh view list rides as a PARAMETER: the posted Views
        // assignment above may not have landed yet, and reading the
        // field here published the wrong "no executable views" banner
        // on every first asynchronous load (red team round 1 blocker —
        // masked by synchronous test mode, where Post runs inline).
        ExecuteBody(generation, (uint)ClampedViewIndex(views.Length), freshViews: views);
    }

    private int ClampedViewIndex(int viewCount) =>
        _activeViewIndex < viewCount ? _activeViewIndex : 0;

    /// <summary>Re-run the active view on the CURRENT handle — the
    /// post-write refresh entry (contract C9). Keeps previous rows on
    /// failure.</summary>
    public void Refresh()
    {
        int generation = Interlocked.Increment(ref _generation);
        StartWork(() => ExecuteBody(generation, (uint)_activeViewIndex));
    }

    /// <summary>Switch the active view (the mac selectView twin):
    /// clears the engine's transient sort on the OLD view, drops the
    /// published sort state, and re-executes. Announcing the switch is
    /// the caller's job (BasesViewSelected — the command/picker path).</summary>
    public void SelectView(int index)
    {
        if (State is BaseLoadState.Failed or BaseLoadState.Loading
            || index < 0
            || index >= _views.Count
            || index == _activeViewIndex)
        {
            return;
        }
        int previousView = _activeViewIndex;
        ActiveViewIndex = index;
        SortState = null;
        ClearQuickFilterState();
        OnPropertyChanged(nameof(ActiveViewName));
        int generation = Interlocked.Increment(ref _generation);
        StartWork(() =>
        {
            lock (_ffiLock)
            {
                // Queued + drained (contract C6, see
                // _pendingSortClears): leaving a view clears its
                // transient sort so returning to it never resurrects
                // an unannounced order — and the queue survives this
                // body being superseded.
                _pendingSortClears.Add((uint)previousView);
                if (_handle is { } handle)
                {
                    DrainPendingSortClearsLocked(handle);
                }
            }
            ExecuteBody(generation, (uint)index);
        });
    }

    private void ExecuteBody(
        int generation,
        uint view,
        bool announceQuickFilterCount = false,
        IReadOnlyList<BaseViewSummary>? freshViews = null)
    {
        // Captured once per body: the executed filter and the ACTIVE
        // flag must describe the same run (contract C5).
        string? quickFilter = NormalizedFilter();
        BasesResultSet result;
        BaseViewSummary? summary;
        try
        {
            lock (_ffiLock)
            {
                if (Volatile.Read(ref _generation) != generation
                    || _handle is not { } handle)
                {
                    return;
                }
                IReadOnlyList<BaseViewSummary> viewList = freshViews ?? _views;
                summary = viewList.Count > view ? viewList[(int)view] : null;
                DrainPendingSortClearsLocked(handle);
                using var cancel = new CancelToken();
                _executeCancel = cancel;
                try
                {
                    result = _session.BaseExecute(
                        handle, view, ThisPath, quickFilter, cancel);
                }
                finally
                {
                    _executeCancel = null;
                }
            }
        }
        catch (VaultException exception)
        {
            Post(() =>
            {
                if (Volatile.Read(ref _generation) != generation)
                {
                    return;
                }
                // Previous rows and handle stay in place — a failed
                // refresh must never blank the pane (contract C9).
                PublishDegraded(BasePhrase.ExecuteFailed(exception));
                // The write-outcome continuations still run: each
                // outcome describes its WRITE (which landed), and the
                // retained rows are what the row-presence check reads
                // — the mac degraded-refresh shape.
                SettleFunnelOutcomes(null);
            });
            return;
        }
        Post(() =>
        {
            if (Volatile.Read(ref _generation) != generation)
            {
                return;
            }
            QuickFilterActive = quickFilter is not null;
            _executedQuickFilter = quickFilter;
            PublishResult(result, summary);
            if (announceQuickFilterCount)
            {
                _announce(new A11yEvent.BaseQuickFilterResult(
                    result.ShownCount, result.UnfilteredShownCount));
            }
        });
    }

    /// <summary>The dock's follow-the-active-note context: threaded
    /// as base_execute's this_path so `this`-relative queries resolve
    /// against the note the dock follows (contract C12). Tab documents
    /// leave it null.</summary>
    internal string? ThisPath { get; set; }

    private string? NormalizedFilter()
    {
        string trimmed = _quickFilterText.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private void PublishResult(BasesResultSet result, BaseViewSummary? summary)
    {
        Result = result;
        // Mac's executeActiveView precedence, verbatim wording
        // (contract C4): fallback status, then error status, then the
        // result's own view error; warnings render as their own banner
        // rows in the view.
        if (summary is { Status: BaseViewStatus.Fallback })
        {
            PublishDegraded(BasePhrase.FallbackView(summary.Name));
        }
        else if (summary is { Status: BaseViewStatus.Error })
        {
            PublishDegraded(BasePhrase.ViewHasErrors(summary.Name));
        }
        else if (result.ViewError is { Length: > 0 } viewError)
        {
            PublishDegraded(viewError);
        }
        else if (summary is null)
        {
            PublishDegraded(BasePhrase.NoExecutableViews);
        }
        else
        {
            State = BaseLoadState.Ready;
            StateMessage = null;
        }
        ResultPublished?.Invoke(this, EventArgs.Empty);
        SettleFunnelOutcomes(result);
    }

    private void PublishDegraded(string message)
    {
        StateMessage = message;
        State = BaseLoadState.Degraded;
    }

    private void PublishFailed(string message)
    {
        StateMessage = message;
        State = BaseLoadState.Failed;
        SortState = null;
        Result = null;
        ResultPublished?.Invoke(this, EventArgs.Empty);
        // Terminal for this load: pending write outcomes still speak
        // (the writes landed; only the refresh died).
        SettleFunnelOutcomes(null);
    }

    /// <summary>
    /// The grid's external-sort handler (contract C6): transactional.
    /// The engine sort is applied and the view re-executed; only a
    /// successful execute publishes rows, sort state, and the spoken
    /// BaseSortedByColumn — one sentence for the chord, the header
    /// click, and the palette command alike. A failed execute rolls
    /// the engine back with nothing published; a failed ROLLBACK
    /// detaches the document (handle closed, terminal Failed) so no
    /// later execute can render rows that contradict the announced
    /// order.
    /// </summary>
    public bool ApplySortFromGrid(int columnIndex, bool ascending)
    {
        if (State is BaseLoadState.Failed or BaseLoadState.Loading
            || Result is not { } result
            || columnIndex < 0
            || columnIndex >= result.Columns.Length)
        {
            return false;
        }
        BasesColumn column = result.Columns[columnIndex];
        int generation = Interlocked.Increment(ref _generation);
        (int ColumnIndex, bool Ascending)? previous = _sortState;
        string? previousColumnId =
            previous is { } p && p.ColumnIndex < result.Columns.Length
                ? result.Columns[p.ColumnIndex].Id
                : null;
        StartWork(() => SortBody(
            generation, (uint)_activeViewIndex, column, columnIndex, ascending,
            previousColumnId, previous));
        return true;
    }

    private void SortBody(
        int generation,
        uint view,
        BasesColumn column,
        int columnIndex,
        bool ascending,
        string? previousColumnId,
        (int ColumnIndex, bool Ascending)? previousSort)
    {
        BasesResultSet result;
        try
        {
            lock (_ffiLock)
            {
                if (Volatile.Read(ref _generation) != generation
                    || _handle is not { } handle)
                {
                    return;
                }
                DrainPendingSortClearsLocked(handle);
                _session.BaseSetTransientSort(handle, view, column.Id, ascending);
                try
                {
                    using var cancel = new CancelToken();
                    // Registered like ExecuteBody's token so Shutdown
                    // can trip a long sort execute too.
                    _executeCancel = cancel;
                    try
                    {
                        result = _session.BaseExecute(
                            handle, view, ThisPath, NormalizedFilter(), cancel);
                    }
                    finally
                    {
                        _executeCancel = null;
                    }
                }
                catch (VaultException executeFailure)
                {
                    // Roll the engine back so the NEXT execute cannot
                    // arrive in an order that was never announced.
                    try
                    {
                        _session.BaseSetTransientSort(
                            handle, view,
                            previousSort is null ? null : previousColumnId,
                            previousSort?.Ascending ?? true);
                    }
                    catch (VaultException)
                    {
                        // The rollback itself failed: detach (C6) —
                        // close the handle so no later execute renders
                        // rows contradicting the published sort.
                        CloseHandleLocked();
                        Post(() =>
                            // NOT generation-gated (red team round 2):
                            // the handle is CLOSED — a superseding
                            // execute cannot land, and skipping this
                            // publish stranded a Ready-looking zombie
                            // with dead affordances.
                            PublishFailed(
                                BasePhrase.ExecuteFailed(executeFailure)));
                        return;
                    }
                    Post(() =>
                    {
                        if (Volatile.Read(ref _generation) != generation)
                        {
                            return;
                        }
                        PublishDegraded(BasePhrase.ExecuteFailed(executeFailure));
                        // A rolled-back engine with a STALE previous
                        // tuple (column set shrank underneath it)
                        // cleared the engine sort — the published
                        // indicator must fall with it (INV-3).
                        if (previousSort is not null && previousColumnId is null)
                        {
                            SortState = null;
                        }
                        SettleFunnelOutcomes(null);
                    });
                    return;
                }
            }
        }
        catch (VaultException)
        {
            // BaseSetTransientSort itself refused (non-displayed
            // column, unknown handle): mac is silent here — the state
            // did not change, so there is nothing to announce. Queued
            // funnel outcomes still settle (round 2: they would
            // otherwise wait for a publish that may never come).
            Post(() =>
            {
                if (Volatile.Read(ref _generation) == generation)
                {
                    SettleFunnelOutcomes(null);
                }
            });
            return;
        }
        Post(() =>
        {
            if (Volatile.Read(ref _generation) != generation)
            {
                return;
            }
            Result = result;
            SortState = (columnIndex, ascending);
            ResultPublished?.Invoke(this, EventArgs.Empty);
            _announce(new A11yEvent.BaseSortedByColumn(column.Label, ascending));
            SettleFunnelOutcomes(result);
        });
    }

    internal override void Shutdown()
    {
        base.Shutdown();
        Interlocked.Increment(ref _generation);
        _pendingFunnelOutcomes.Clear();
        // An in-flight execute holds the lock; trip its token so
        // shutdown does not wait out a long query.
        try
        {
            _executeCancel?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The body's using-block won the race; the execute is over.
        }
        if (IsSynchronousForTests)
        {
            lock (_ffiLock)
            {
                CloseHandleLocked();
            }
            return;
        }
        // The close waits out whatever call still holds the lock, and
        // some (apply-edits, views, export) carry no cancel token — so
        // it must not run on the dispatcher (INV-6; red team round 1).
        // No new handle can appear behind it: every open is inside a
        // generation check that the bump above already invalidated.
        _ = Task.Run(() =>
        {
            try
            {
                lock (_ffiLock)
                {
                    CloseHandleLocked();
                }
            }
            catch (Exception exception) when (exception
                is VaultException or ObjectDisposedException)
            {
                // Teardown race: the session died first — the handle
                // died with it.
            }
        });
    }

    private void CloseHandleLocked()
    {
        _pendingSortClears.Clear();
        if (_handle is { } handle)
        {
            _handle = null;
            _session.CloseBase(handle);
        }
    }

    /// <summary>View indices whose engine sort must be cleared before
    /// the next engine mutation (guarded by _ffiLock). Round 1 found
    /// a generation-gated clear was SKIPPED on rapid double switches
    /// (sort resurrection); round 2 found an ungated clear could land
    /// AFTER a newer sort and clobber its slot under out-of-order
    /// pool scheduling. Queuing the clear and draining it from EVERY
    /// engine-touching body preserves both orders.</summary>
    private readonly List<uint> _pendingSortClears = [];

    private void DrainPendingSortClearsLocked(ulong handle)
    {
        foreach (uint view in _pendingSortClears)
        {
            try
            {
                _session.BaseSetTransientSort(
                    handle, view, columnId: null, ascending: true);
            }
            catch (VaultException)
            {
                // A refused clear on a dying handle is survivable.
            }
        }
        _pendingSortClears.Clear();
    }
}

/// <summary>Host-composed BANNER text only — labels, never
/// announcements (the TaskStatusPhrase category: static UI copy is a
/// §W-C label concern, not §W-D vocabulary). Wording is mac's
/// verbatim where mac has the sentence.</summary>
internal static class BasePhrase
{
    public const string NoExecutableViews = "No executable base views were found.";

    public const string EmptyResults = "No base results.";

    public static string FallbackView(string viewName) =>
        $"Using fallback view for {viewName}.";

    public static string ViewHasErrors(string viewName) =>
        $"View {viewName} has errors.";

    public static string OpenFailed(VaultException exception) =>
        $"This Base could not be opened: {Message(exception)}";

    public static string ExecuteFailed(VaultException exception) =>
        $"Base view could not be executed: {Message(exception)}";

    /// <summary>The uniffi exception's payload without the type
    /// prefix — the same detail string core put in the variant.</summary>
    private static string Message(VaultException exception) =>
        exception.Message;
}
