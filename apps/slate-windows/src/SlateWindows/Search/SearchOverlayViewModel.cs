// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using uniffi.slate_uniffi;

namespace SlateWindows.Search;

/// <summary>The overlay's panel state. <see cref="Idle"/> shows the
/// hint or the recent searches; <see cref="Results"/> shows
/// <see cref="SearchOverlayViewModel.Rows"/>.</summary>
internal enum SearchOverlayState
{
    Idle,
    Searching,
    Results,
    Error,
}

/// <summary>Raised on activation (contract S9): the shell opens
/// <see cref="Path"/> and derives the target line from
/// <see cref="Query"/> — the query that produced the activated row.
/// <see cref="Snippet"/> is the row's marker-stripped snippet, carried
/// for the shell's <c>SearchResultOpened</c> announcement (mac
/// <c>cleanSnippet</c>, <c>AppState.swift:9441-9443</c>).</summary>
internal sealed record SearchOpenRequest(string Path, string Query, string Snippet);

/// <summary>
/// W5-2 vault-search overlay state (#742). Core searches, ranks,
/// snippets, and summarises; this view model debounces, hops threads,
/// discards stale results, and announces. It renders nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The FFI call is synchronous and never runs on the dispatcher</b>
/// (contract S5). The pipeline follows the Quick Open shape
/// (<see cref="QuickSwitcherViewModel"/>): capture the UI
/// <see cref="SynchronizationContext"/> at construction, debounce 150 ms
/// trailing, run the search through <see cref="Task.Run(Action)"/>,
/// marshal back, and check staleness on <b>five</b> independent things
/// before publishing — token identity, session identity, query
/// unchanged, scope unchanged, overlay still open — because three of
/// them have each shipped as a bug on mac and the scope arm closed a
/// Windows-found one (red-team round 1).
/// </para>
/// <para>
/// <b>No dedup on the query pipeline</b> (contract S7): mac's red-team
/// record shows pipeline-lifetime dedup silently swallowed the
/// reopen-with-retained-query re-arm. Announcement dedup lives on the
/// rendered summary string instead, inside
/// <see cref="PublishSummary"/>.
/// </para>
/// <para>
/// <b>No <c>ICommand</c> surface</b>, the palette precedent (PR-4):
/// the host binds key handlers to these methods. The one registered
/// command (<c>slate.view.toggleSearch</c>) is an unguarded adapter
/// over <see cref="Toggle"/> on <c>VaultLifecycleViewModel</c>, added
/// at the W5-2 close-out.
/// </para>
/// </remarks>
internal sealed class SearchOverlayViewModel : BindableBase, IDisposable
{
    /// <summary>Trailing debounce, matching mac
    /// (<c>AppState.swift:8492-8497</c>).</summary>
    internal const int DebounceMilliseconds = 150;

    // W0.5-3 residue: mac-verbatim idle-state strings (contract S14),
    // transcribed from SearchOverlay.swift for the phase-2 view.
    internal const string IdleHint = "Type to search.";
    internal const string RecentSearchesHeader = "Recent Searches";
    internal const string ClearRecentsLabel = "Clear recent searches";
    internal const string ClearRecentsHint = "Forgets every remembered search in this vault.";
    internal const string RecentRowHint = "Runs this search again.";

    private readonly ISearchSource _source;
    private readonly Action<A11yEvent> _announce;
    private readonly SynchronizationContext? _uiContext;
    private readonly Func<CancellationToken, Task> _debounceDelay;

    private CancellationTokenSource? _debounceCancellation;
    private CancelToken? _searchCancellation;
    private string _query = string.Empty;
    private SearchScope _scope = new SearchScope.Vault();
    private SearchOverlayState _state = SearchOverlayState.Idle;
    private string _summary = string.Empty;
    private string? _lastResultsQuery;
    private int _selectedIndex = -1;
    private bool _suppressSelectionAnnouncement;
    private bool _isOpen;
    private IReadOnlyList<string> _recents = [];

    public SearchOverlayViewModel(
        ISearchSource source,
        Action<A11yEvent> announce,
        bool debounceSearches = true,
        Func<CancellationToken, Task>? debounceDelay = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(announce);
        _source = source;
        _announce = announce;
        // The Quick Open test seam: with debouncing off the whole
        // pipeline runs synchronously on the calling thread, so simple
        // facts need no pumping. Production always debounces.
        _uiContext = debounceSearches ? SynchronizationContext.Current : null;
        _debounceDelay = debounceDelay
            ?? (token => Task.Delay(DebounceMilliseconds, token));
    }

    /// <summary>Raised on activation, after the overlay has closed
    /// (contract S9). The shell consumes this in phase 2.</summary>
    public event EventHandler<SearchOpenRequest>? OpenRequested;

    /// <summary>Raised after the overlay closes, for focus restore
    /// (divergence SD-2, wired in phase 2).</summary>
    public event EventHandler? Dismissed;

    /// <summary>The whole pending pipeline for the latest keystroke —
    /// awaited by tests, then the captured context is drained.</summary>
    internal Task SearchCompletion { get; private set; } = Task.CompletedTask;

    public ObservableCollection<SearchResultRowViewModel> Rows { get; } = [];

    public string Query
    {
        get => _query;
        set
        {
            if (SetField(ref _query, value ?? string.Empty))
            {
                ScheduleSearch();
            }
        }
    }

    /// <summary>
    /// The active scope. There is no scope selector (divergence SD-3):
    /// Tag scope is armed programmatically by reading-view tag
    /// activation and cleared by the chip's clear button or by
    /// <see cref="Close"/>.
    /// </summary>
    public SearchScope Scope
    {
        get => _scope;
        private set
        {
            if (SetField(ref _scope, value))
            {
                OnPropertyChanged(nameof(TagScopeName));
            }
        }
    }

    /// <summary>
    /// The armed tag's name, or <see langword="null"/> outside Tag scope
    /// — the phase-2 view's binding surface for the read-only
    /// <c>Tag: {name}</c> chip (divergence SD-3: there is no scope
    /// selector; the chip and its clear button are the whole UI).
    /// </summary>
    public string? TagScopeName => _scope is SearchScope.Tag tag ? tag.Name : null;

    public SearchOverlayState State
    {
        get => _state;
        private set
        {
            if (SetField(ref _state, value))
            {
                RaisePanelState();
            }
        }
    }

    // ---- panel-state flags (phase-2 view surface) -----------------------
    //
    // The idle/searching/results/error panels are mutually exclusive on
    // mac (SearchOverlay.swift's state switch); these flags let the XAML
    // collapse every unused panel — an empty panel left on-screen at zero
    // size fails the axe BoundingRectangleNotNull check (the W4-5/W4-6
    // lesson) — without a MultiDataTrigger per panel.

    /// <summary>Idle with no remembered searches: the bare
    /// <see cref="IdleHint"/> (contract S14).</summary>
    public bool ShowsIdleHint => State == SearchOverlayState.Idle && Recents.Count == 0;

    /// <summary>Idle with remembered searches: the Recent Searches
    /// section (contract S14).</summary>
    public bool ShowsRecents => State == SearchOverlayState.Idle && Recents.Count > 0;

    /// <summary>The transition into searching is visible but silent
    /// (contract S8).</summary>
    public bool IsSearching => State == SearchOverlayState.Searching;

    /// <summary>Results with rows: the list itself (mac's
    /// <c>resultsList</c> arm).</summary>
    public bool ShowsResultRows =>
        State == SearchOverlayState.Results && Rows.Count > 0;

    /// <summary>Results with zero rows: the "No results." panel (mac's
    /// <c>emptyResultsState</c> arm).</summary>
    public bool ShowsNoResults =>
        State == SearchOverlayState.Results && Rows.Count == 0;

    /// <summary>Error: the "Search error" heading over
    /// <see cref="Summary"/>.</summary>
    public bool ShowsError => State == SearchOverlayState.Error;

    /// <summary>Core's summary string, displayed verbatim (contract
    /// S2). Never composed host-side — there is no "{n} results"
    /// anywhere in this feature.</summary>
    public string Summary => _summary;

    /// <summary>
    /// The selected result row. A change onto a valid row announces
    /// <c>RowSelected</c> with the row's <b>basename only</b> — mac's
    /// on-focus announcement (<c>SearchOverlay.swift:537-552</c>: the
    /// full label with snippet stays on the element; every hop must not
    /// speak a paragraph of snippet). The publish-time auto-select is
    /// suppressed (the palette's P10 shape) so a result set never
    /// double-speaks its top row over the summary announcement, and
    /// <see cref="BindableBase.SetField"/>'s equality gate keeps a
    /// re-selection of the same index silent.
    /// </summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            // Disarm even when nothing changed, so a publish that lands
            // on the already-selected index cannot leave the flag armed
            // to swallow the NEXT arrow move (the palette's disarm-on-
            // no-change discipline).
            bool suppress = _suppressSelectionAnnouncement;
            _suppressSelectionAnnouncement = false;
            if (!SetField(ref _selectedIndex, value))
            {
                return;
            }

            if (suppress || value < 0 || value >= Rows.Count)
            {
                return;
            }

            _announce(new A11yEvent.RowSelected(Rows[value].Basename));
        }
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetField(ref _isOpen, value);
    }

    /// <summary>The recent queries snapshot for the idle state,
    /// refreshed from disk on every open (contract S14).</summary>
    public IReadOnlyList<string> Recents
    {
        get => _recents;
        private set
        {
            if (SetField(ref _recents, value))
            {
                RaisePanelState();
            }
        }
    }

    /// <summary>
    /// Toggle the overlay (mac <c>toggleSearchOverlay</c>,
    /// <c>AppState.swift:8785-8791</c>): open if closed, close if open.
    /// </summary>
    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    /// <summary>
    /// Open the overlay. Refuses without a vault: announces
    /// <c>SearchNeedsVault</c> and leaves <see cref="IsOpen"/>
    /// <see langword="false"/> — a set flag would auto-present the
    /// overlay on the next vault open (the palette's P14 shape,
    /// mac <c>requestSearchOverlay</c>). Reopening with a retained
    /// query re-arms the search through the ordinary pipeline.
    /// </summary>
    public void Open()
    {
        if (!_source.IsVaultOpen)
        {
            _announce(new A11yEvent.SearchNeedsVault());
            return;
        }

        if (IsOpen)
        {
            return;
        }

        Recents = _source.LoadRecents();
        IsOpen = true;
        // The re-arm (mac SearchOverlay onAppear): the retained query
        // must not sit in the field with no results. It goes through
        // the debounced pipeline, which is exactly why that pipeline
        // has no dedup (contract S7) — with dedup the identical string
        // was silently swallowed and this line was dead code on mac.
        if (Query.Length > 0)
        {
            ScheduleSearch();
        }
    }

    /// <summary>
    /// Close the overlay (mac <c>closeSearchOverlay</c>,
    /// <c>AppState.swift:8832-8846</c>): cancel in-flight, reset scope
    /// to Vault — a tag scope left armed would silently scope the next
    /// search to a chip the user can't see — state Idle, announcement
    /// state cleared, and <b>Query preserved</b> so reopen lands the
    /// user back at the same results.
    /// </summary>
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        CancelDebounce();
        CancelInFlightSearch();
        // Rows are cleared while the overlay is still realised so UIA
        // clients do not retain orphaned children (the Quick Open
        // precedent).
        ClearRows();
        _lastResultsQuery = null;
        Scope = new SearchScope.Vault();
        State = SearchOverlayState.Idle;
        PublishSummary(string.Empty, null);
        IsOpen = false;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Arm a scope and re-run the current query under it (mac
    /// <c>setSearchScope</c>): under Tag scope an empty query is
    /// meaningful — core lists every tagged file — so the re-arm always
    /// fires.
    /// </summary>
    public void SetScope(SearchScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        Scope = scope;
        ScheduleSearch();
    }

    /// <summary>Drop back to Vault scope and refresh under the wider
    /// scope (the chip's clear button; mac <c>clearSearchScope</c>).</summary>
    public void ClearScope() => SetScope(new SearchScope.Vault());

    /// <summary>
    /// Reading-view tag activation (divergence SD-4), in mac's exact
    /// order (<c>ReadingLinkRouter.swift:243-258</c>): clear the query,
    /// open the overlay if closed, and arm Tag scope LAST — so the
    /// empty-query tag listing fires exactly once, through the ordinary
    /// pipeline. Clearing BEFORE opening keeps <see cref="Open"/>'s
    /// retained-query re-arm from firing a stale Vault-scope search
    /// first; arming the scope last keeps the listing from running
    /// under the wrong scope. The listing's own summary announcement
    /// (contract S2) is the only voice on this path — the editor tag
    /// path's "Filtered files by tag" residue string is never spoken
    /// here.
    /// </summary>
    public void OpenTagScoped(string tagName)
    {
        ArgumentNullException.ThrowIfNull(tagName);
        Query = string.Empty;
        if (!IsOpen)
        {
            Open();
            if (!IsOpen)
            {
                // Open refused (no vault): never leave a scope armed on
                // a closed overlay — Close() is what resets scope, and
                // a never-opened overlay never runs Close(), so the tag
                // would silently scope the NEXT open's first search.
                return;
            }
        }

        SetScope(new SearchScope.Tag(tagName));
    }

    /// <summary>
    /// Down (<c>delta = 1</c>) / Up (<c>delta = -1</c>), wrapping — the
    /// palette's shape (divergence SD-1: Windows ships arrow-key result
    /// navigation; mac has none, and matching mac's Tab-only traversal
    /// would make search the only list surface here that arrows do not
    /// drive).
    /// </summary>
    public void MoveSelection(int delta)
    {
        // Gated on Results, symmetric with ActivateSelected: the Error
        // and Searching states keep Rows populated while the list is
        // collapsed, and moving selection there announced rows of a
        // HIDDEN list — the round-1 announce fix made a pre-existing
        // silent selection audible (red team round 2, F1).
        if (State != SearchOverlayState.Results || Rows.Count == 0 || delta == 0)
        {
            return;
        }

        int index = SelectedIndex;
        if (index < 0 || index >= Rows.Count)
        {
            // From no selection: Down lands on the first row, Up on the
            // last (the palette's P7 shape).
            index = delta > 0 ? -1 : Rows.Count;
        }

        SelectedIndex = (((index + delta) % Rows.Count) + Rows.Count) % Rows.Count;
    }

    /// <summary>Activate the selected row; Enter on the field activates
    /// the top result via a zero <see cref="SelectedIndex"/> (contract S9).</summary>
    public void ActivateSelected()
    {
        if (State == SearchOverlayState.Results
            && SelectedIndex >= 0
            && SelectedIndex < Rows.Count)
        {
            ActivateRow(Rows[SelectedIndex]);
        }
    }

    /// <summary>
    /// Activate a result row (contract S9): record the recent, close
    /// the overlay, then hand the open to the shell. Recording is the
    /// ONLY recents write, and it records the query that <b>produced
    /// the visible rows</b> — not the live field text, which the 150 ms
    /// debounce lets run ahead (mac <c>lastResultsQuery</c>,
    /// <c>AppState.swift:9445-9453</c>).
    /// </summary>
    public void ActivateRow(SearchResultRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!IsOpen)
        {
            return;
        }

        // The fallback mirrors mac's `?? searchQuery`: defensive only —
        // row activation implies a results panel, which set the field.
        string query = _lastResultsQuery ?? Query.Trim();
        // Blank queries are never recorded (mac recordSearchRecent):
        // the empty-query tag-scope listing has rows but no query worth
        // remembering.
        if (query.Length > 0)
        {
            _source.RecordRecent(query);
        }

        Close();
        OpenRequested?.Invoke(
            this, new SearchOpenRequest(row.Path, query, row.StrippedSnippet));
    }

    /// <summary>
    /// Re-run a remembered query from the idle state (mac
    /// <c>runRecentSearch</c>): drop it into the field and push it
    /// through the same debounced pipeline a keystroke takes. Does
    /// <b>not</b> record — only activation records (contract S9).
    /// Schedules even when the field already holds the same string,
    /// which the property setter would swallow.
    /// </summary>
    public void ActivateRecent(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!IsOpen)
        {
            return;
        }

        // Written to the backing field directly: the property setter
        // schedules only on change, and this path must schedule even
        // when the field already holds the identical string (the S7
        // re-arm rule).
        _ = SetField(ref _query, query, nameof(Query));
        ScheduleSearch();
    }

    /// <summary>Announce a focused recent row (contract S14). Called by
    /// the phase-2 view's focus handler.</summary>
    public void NotifyRecentRowFocused(string query) =>
        _announce(new A11yEvent.RecentSearchFocused(query));

    /// <summary>
    /// Forget every remembered query, then re-read from disk: on a
    /// write failure the list stays visible — honest, it was NOT
    /// forgotten (mac's persist-first discipline).
    /// </summary>
    public void ClearRecents()
    {
        _source.ClearRecents();
        Recents = _source.LoadRecents();
    }

    public void Dispose()
    {
        CancelDebounce();
        CancelInFlightSearch();
    }

    // ---- pipeline ------------------------------------------------------

    private void ScheduleSearch()
    {
        if (!IsOpen)
        {
            return;
        }

        if (_uiContext is null)
        {
            _ = StartSearch(Query);
            return;
        }

        CancelDebounce();
        var debounce = new CancellationTokenSource();
        _debounceCancellation = debounce;
        SearchCompletion = DebounceThenSearchAsync(Query, debounce.Token);
    }

    /// <summary>Trailing 150 ms debounce: every keystroke cancels the
    /// pending window and opens a new one; the in-flight search is only
    /// cancelled when the next search actually starts (the mac shape).</summary>
    private async Task DebounceThenSearchAsync(string query, CancellationToken debounceToken)
    {
        try
        {
            await _debounceDelay(debounceToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (debounceToken.IsCancellationRequested)
        {
            return;
        }

        var dispatched = new TaskCompletionSource<Task?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _uiContext!.Post(
            _ =>
            {
                try
                {
                    dispatched.SetResult(
                        debounceToken.IsCancellationRequested ? null : StartSearch(query));
                }
                catch (Exception exception)
                {
                    dispatched.SetException(exception);
                }
            },
            null);
        if (await dispatched.Task is Task search)
        {
            await search;
        }
    }

    /// <summary>
    /// The mac <c>runSearch</c> twin (<c>AppState.swift:8874-8978</c>).
    /// Runs on the UI thread. Returns the off-thread search task, or
    /// <see langword="null"/> when the query short-circuited.
    /// </summary>
    private Task? StartSearch(string rawQuery)
    {
        if (!IsOpen)
        {
            return null;
        }

        string trimmed = rawQuery.Trim();
        // Empty query → Idle, EXCEPT under Tag scope: there an empty
        // query is meaningful (core lists every tagged file), so it must
        // reach the FFI instead of short-circuiting (mac
        // scopeListsOnEmpty, AppState.swift:8878-8894).
        bool scopeListsOnEmpty = Scope is SearchScope.Tag;
        if (trimmed.Length == 0 && !scopeListsOnEmpty)
        {
            CancelInFlightSearch();
            ClearRows();
            _lastResultsQuery = null;
            State = SearchOverlayState.Idle;
            PublishSummary(string.Empty, null);
            return null;
        }

        if (!_source.IsVaultOpen)
        {
            // No vault → benign idle, not an error toast (the mac guard).
            State = SearchOverlayState.Idle;
            return null;
        }

        CancelInFlightSearch();
        State = SearchOverlayState.Searching;
        var cancel = new CancelToken();
        _searchCancellation = cancel;
        SearchScope scope = Scope;
        object? sessionAtDispatch = _source.SessionIdentity;

        if (_uiContext is null)
        {
            QueryResultSet? resultSet = null;
            Exception? failure = null;
            try
            {
                resultSet = _source.Search(trimmed, scope, cancel);
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            Publish(trimmed, cancel, sessionAtDispatch, scope, resultSet, failure);
            return null;
        }

        return SearchAsync(trimmed, scope, cancel, sessionAtDispatch);
    }

    private async Task SearchAsync(
        string query,
        SearchScope scope,
        CancelToken cancel,
        object? sessionAtDispatch)
    {
        QueryResultSet? resultSet = null;
        Exception? failure = null;
        try
        {
            // Contract S5: FullTextSearch blocks — never on the dispatcher.
            resultSet = await Task.Run(() => _source.Search(query, scope, cancel));
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        _uiContext!.Post(
            _ => Publish(query, cancel, sessionAtDispatch, scope, resultSet, failure),
            null);
    }

    /// <summary>
    /// Publish a finished search — on the UI thread, behind the
    /// five-way staleness check (contract S5). Each arm has shipped as
    /// a bug somewhere: the token guards supersession, the session
    /// guards a vault switch racing the await (mac #876 Codex round 2),
    /// the query guards the debounce window's run-ahead, the scope
    /// guards a chip cleared mid-flight (red-team round 1: the re-armed
    /// wider search sits in an unfired debounce window, so token,
    /// session, query and open-flag all still pass while the landing
    /// rows belong to a scope the user just dismissed), and the
    /// open-flag guards a result resurrecting a closed overlay.
    /// </summary>
    private void Publish(
        string query,
        CancelToken cancel,
        object? sessionAtDispatch,
        SearchScope scopeAtDispatch,
        QueryResultSet? resultSet,
        Exception? failure)
    {
        // The scope arm compares structurally: the generated
        // SearchScope records carry value equality, and Close/SetScope
        // construct fresh instances, so reference identity would treat
        // every publish as stale.
        if (!ReferenceEquals(_searchCancellation, cancel)
            || !ReferenceEquals(_source.SessionIdentity, sessionAtDispatch)
            || !string.Equals(Query.Trim(), query, StringComparison.Ordinal)
            || !Equals(Scope, scopeAtDispatch)
            || !IsOpen)
        {
            return;
        }

        // Contract S6: cancellation is a user action, not a failure —
        // leave every piece of state exactly as it was: no state
        // change, no summary, no announcement.
        if (failure is VaultException.Cancelled)
        {
            return;
        }

        // Terminal outcome for the current search: release the native
        // token now rather than holding it until the next search.
        _searchCancellation = null;
        cancel.Dispose();

        if (failure is not null)
        {
            PublishError(failure);
            return;
        }

        PublishResults(resultSet!, query);
    }

    private void PublishResults(QueryResultSet resultSet, string query)
    {
        // Contract S1: rows arrive already sorted by bm25 ascending and
        // are published AS ORDERED — never re-sorted, Score never shown.
        Rows.Clear();
        foreach (QueryHit hit in resultSet.Rows)
        {
            Rows.Add(new SearchResultRowViewModel(hit));
        }

        // The auto-select must not double-speak with the summary
        // announcement below: arm the suppression AFTER the row
        // mutations (whose binding write-backs would disarm it) and
        // immediately before the assignment it covers.
        _suppressSelectionAnnouncement = true;
        SelectedIndex = Rows.Count > 0 ? 0 : -1;
        _lastResultsQuery = query;
        State = SearchOverlayState.Results;
        // Raised even when State was ALREADY Results — the row count can
        // flip between zero and non-zero without a state change, and the
        // ShowsResultRows/ShowsNoResults pair reads both.
        RaisePanelState();
        // Contract S2: display core's summary verbatim; announce the
        // typed event, which core renders from the same template.
        PublishSummary(
            resultSet.Summary,
            new A11yEvent.SearchResultsSummary((uint)resultSet.Rows.Length));
    }

    private void PublishError(Exception failure)
    {
        State = SearchOverlayState.Error;
        var announcement = new A11yEvent.SearchFailed(HumanReadable(failure));
        // The displayed line is the event's own rendering ("Search
        // error: {message}", a11y.rs:872) — the displayed and spoken
        // strings stay one template, never two copies (contract S6).
        PublishSummary(SlateUniffiMethods.A11yRender(announcement).Text, announcement);
    }

    /// <summary>
    /// The single summary/announcement choke point: the announcement
    /// fires only when the rendered summary string actually changed —
    /// the view-level dedup mac keeps (<c>SearchOverlay.swift:111-115</c>),
    /// which is where dedup lives instead of the query pipeline
    /// (contract S7). Close and the idle transition publish an empty
    /// summary, clearing the dedup memory, so a reopened overlay
    /// re-announces an identical result set.
    /// </summary>
    private void PublishSummary(string summary, A11yEvent? announcement)
    {
        bool changed = SetField(ref _summary, summary, nameof(Summary));
        if (changed && summary.Length > 0 && announcement is not null)
        {
            _announce(announcement);
        }
    }

    /// <summary>
    /// The mac <c>humanReadable</c> arms reachable from search
    /// (contract S6). The InvalidQuery message embeds a raw SQLite
    /// string: it is passed through, never parsed and never
    /// re-classified host-side.
    /// </summary>
    private static string HumanReadable(Exception failure) => failure switch
    {
        VaultException.Io io => io.message,
        VaultException.Db db => db.message,
        VaultException.InvalidQuery invalid => $"Search query is invalid: {invalid.message}",
        VaultException.Unsupported unsupported => $"{unsupported.feature} is not implemented yet.",
        _ => failure.Message,
    };

    private void ClearRows()
    {
        Rows.Clear();
        SelectedIndex = -1;
        RaisePanelState();
    }

    /// <summary>Re-derive every mutually-exclusive panel flag. Cheap and
    /// idempotent, so mutation sites call it without checking which flag
    /// actually flipped.</summary>
    private void RaisePanelState()
    {
        OnPropertyChanged(nameof(ShowsIdleHint));
        OnPropertyChanged(nameof(ShowsRecents));
        OnPropertyChanged(nameof(IsSearching));
        OnPropertyChanged(nameof(ShowsResultRows));
        OnPropertyChanged(nameof(ShowsNoResults));
        OnPropertyChanged(nameof(ShowsError));
    }

    private void CancelDebounce()
    {
        _debounceCancellation?.Cancel();
        _debounceCancellation?.Dispose();
        _debounceCancellation = null;
    }

    /// <summary>Cancel and release the in-flight search's native token
    /// (mac <c>cancelInFlightSearch</c>). Disposal is safe against an
    /// in-flight FFI call: the binding's call counter keeps the Rust
    /// side alive until the call returns, and a late lowering of the
    /// disposed token fails as a managed exception whose result the
    /// staleness check discards.</summary>
    private void CancelInFlightSearch()
    {
        if (_searchCancellation is CancelToken token)
        {
            _searchCancellation = null;
            token.Cancel();
            token.Dispose();
        }
    }
}
