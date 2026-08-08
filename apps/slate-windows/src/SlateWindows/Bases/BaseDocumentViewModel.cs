// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Bases;

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

    /// <summary>Vault-relative path — the source identity. Compared
    /// byte-exact (Ordinal) everywhere, never culture-aware.</summary>
    public string Path { get; }

    public string DisplayName =>
        System.IO.Path.GetFileNameWithoutExtension(Path);

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
    private int _publishFunnelId;
    private Action? _publishContinuation;

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
        _publishFunnelId = funnelId;
        _publishContinuation = onPublished;
        StartWork(() => ExecuteBody(generation, (uint)_activeViewIndex));
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
                Views = views;
                SortState = null;
                _announce(new A11yEvent.BaseSortSavedToView(column.Label, sort.Ascending));
            });
            ExecuteBody(generation, (uint)_activeViewIndex);
        });
    }

    /// <summary>Mac's slateSortYAML + quoteYAMLString, byte-for-byte.</summary>
    internal static string SlateSortYaml(string columnId, bool ascending) =>
        "- property: " + QuoteYamlString(columnId) + "\n"
        + "  direction: " + (ascending ? "ASC" : "DESC");

    private static string QuoteYamlString(string value)
    {
        string escaped = value
            .Replace("\\", "\\\\")
            .Replace("\n", "\n")
            .Replace("\r", "\r")
            .Replace("\t", "\t")
            .Replace("\"", "\\\"");
        return "\"" + escaped + "\"";
    }

    /// <summary>slate.bases.whereAmI — core joins the present parts.</summary>
    public A11yEvent WhereAmIEvent() => new A11yEvent.BaseWhereAmI(
        DisplayName,
        ActiveViewName,
        QuickFilterActive && QuickFilterText.Trim().Length > 0
            ? QuickFilterText.Trim()
            : null);

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
    /// core composes the bytes (contract C14); the CALLER owns
    /// delivery and its announcement. Returns null after announcing
    /// the failure.</summary>
    public string? ExportText(ExportFormat format)
    {
        if (State is BaseLoadState.Failed or BaseLoadState.Loading)
        {
            return null;
        }
        lock (_ffiLock)
        {
            if (_handle is not { } handle)
            {
                return null;
            }
            try
            {
                return _session.BaseExport(
                    handle, (uint)_activeViewIndex, format, NormalizedFilter());
            }
            catch (VaultException failure)
            {
                _announce(new A11yEvent.BasesViewExportFailed(failure.Message));
                return null;
            }
        }
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
                handle = _session.OpenBase(Path);
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
        ExecuteBody(generation, (uint)ClampedViewIndex(views.Length));
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
                if (Volatile.Read(ref _generation) != generation
                    || _handle is not { } handle)
                {
                    return;
                }
                try
                {
                    // Single-slot engine sort (contract C6): leaving a
                    // view clears its transient sort so returning to it
                    // never resurrects an unannounced order.
                    _session.BaseSetTransientSort(
                        handle, (uint)previousView, columnId: null, ascending: true);
                }
                catch (VaultException)
                {
                    // A refused clear on a dying handle is survivable;
                    // the execute below reports the real condition.
                }
            }
            ExecuteBody(generation, (uint)index);
        });
    }

    private void ExecuteBody(
        int generation, uint view, bool announceQuickFilterCount = false)
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
                summary = _views.Count > view ? _views[(int)view] : null;
                using var cancel = new CancelToken();
                _executeCancel = cancel;
                try
                {
                    result = _session.BaseExecute(
                        handle, view, thisPath: null, quickFilter, cancel);
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
                // The write-outcome continuation still runs: the
                // outcome describes the WRITE (which landed), and the
                // retained rows are what the row-presence check reads
                // — the mac degraded-refresh shape.
                _publishFunnelId = 0;
                Action? failedContinuation = _publishContinuation;
                _publishContinuation = null;
                failedContinuation?.Invoke();
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
            PublishResult(result, summary);
            if (announceQuickFilterCount)
            {
                _announce(new A11yEvent.BaseQuickFilterResult(
                    result.ShownCount, result.UnfilteredShownCount));
            }
        });
    }

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
        string signature = MembershipSignatureOf(result);
        int funnelId = _publishFunnelId;
        _publishFunnelId = 0;
        if (funnelId != 0
            && _membershipBaselinePublished
            && !string.Equals(_membershipSignature, signature, StringComparison.Ordinal))
        {
            MembershipChanged?.Invoke(funnelId, result.AudioSummary);
        }
        _membershipSignature = signature;
        _membershipBaselinePublished = true;
        ResultPublished?.Invoke(this, EventArgs.Empty);
        Action? continuation = _publishContinuation;
        _publishContinuation = null;
        continuation?.Invoke();
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
                _session.BaseSetTransientSort(handle, view, column.Id, ascending);
                try
                {
                    using var cancel = new CancelToken();
                    result = _session.BaseExecute(
                        handle, view, thisPath: null, NormalizedFilter(), cancel);
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
                        {
                            if (Volatile.Read(ref _generation) != generation)
                            {
                                return;
                            }
                            PublishFailed(
                                BasePhrase.ExecuteFailed(executeFailure));
                        });
                        return;
                    }
                    Post(() =>
                    {
                        if (Volatile.Read(ref _generation) != generation)
                        {
                            return;
                        }
                        PublishDegraded(BasePhrase.ExecuteFailed(executeFailure));
                    });
                    return;
                }
            }
        }
        catch (VaultException)
        {
            // BaseSetTransientSort itself refused (non-displayed
            // column, unknown handle): mac is silent here — the state
            // did not change, so there is nothing to announce.
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
        });
    }

    internal override void Shutdown()
    {
        base.Shutdown();
        Interlocked.Increment(ref _generation);
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
        lock (_ffiLock)
        {
            CloseHandleLocked();
        }
    }

    private void CloseHandleLocked()
    {
        if (_handle is { } handle)
        {
            _handle = null;
            _session.CloseBase(handle);
        }
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
