// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>Which half of the bibliography leaf is showing.</summary>
internal enum BibliographySegment
{
    Entries,
    Unresolved,
}

/// <summary>
/// W4-5 (#737): the vault-scoped bibliography leaf — the mac
/// BibliographyPanel twin. Two segments (Entries, Unresolved), each
/// with its own load, riding the W4-1 AccessibleDataGrid substrate.
///
/// The leaf is READ-ONLY (feature contract 6): it never calls
/// SetBibliographySources — the workspace seeds sources once per
/// vault open and hands the outcome here for display.
///
/// Loading is LAZY and idempotent (mac's `.task`): the rail reveal
/// calls <see cref="EnsureLoaded"/>, which is a no-op once loaded;
/// only an explicit action calls <see cref="ForceReload"/>. Both
/// segments carry generation + requestId guards (contract 3).
///
/// Search is a HOST-SIDE predicate over the loaded set (recorded
/// divergence D-4): core's SearchBibliography is a LIKE over title
/// and authors only, while mac matches title, key, family AND given.
/// Matching mac — and the promised help text — requires the host
/// predicate, so SearchBibliography is deliberately not called.
/// </summary>
internal sealed class BibliographyViewModel : PanelWorkScheduler
{
    /// <summary>Windows addition D-3: the entry grid is bounded, and
    /// the bound is SPOKEN in the grid summary (contract 9). Core's
    /// own guidance is "&lt;10k entries typically", so this is
    /// unreachable in practice.</summary>
    internal const int MaxEntryRows = 5000;

    private readonly VaultSession _session;
    private readonly Action<A11yEvent> _announce;
    private long _generation;
    private int _entriesRequestId;
    private int _unresolvedRequestId;
    private bool _loadStarted;
    private BibliographySegment _segment = BibliographySegment.Entries;
    private string _searchText = "";
    private bool _isLoadingEntries;
    private bool _isLoadingUnresolved;
    private string? _entriesError;
    private string? _unresolvedError;
    private int _totalEntryCount;
    private IReadOnlyList<BibEntry> _allEntries = [];
    private BibliographySeed? _seed;

    public BibliographyViewModel(
        VaultSession session,
        Action<A11yEvent> announce,
        bool synchronousForTests = false)
        : base(synchronousForTests)
    {
        _session = session;
        _announce = announce;
    }

    /// <summary>The filtered, capped entry rows the grid binds.</summary>
    public ObservableCollection<BibliographyRowViewModel> Entries { get; } = [];

    public ObservableCollection<UnresolvedRowViewModel> Unresolved { get; } = [];

    /// <summary>Warnings core reported when the workspace seeded the
    /// sources, plus any unreadable-source message. Surfaced verbatim
    /// (contract 5) — never swallowed.</summary>
    public IReadOnlyList<string> LoadNotices { get; private set; } = [];

    public BibliographySegment Segment
    {
        get => _segment;
        set
        {
            if (SetField(ref _segment, value))
            {
                OnPropertyChanged(nameof(ShowEntries));
                OnPropertyChanged(nameof(ShowUnresolved));
                NotifyStateChanged();
                // A segment switch must never announce (§2.6) and
                // never re-query: both segments load once.
                if (value == BibliographySegment.Unresolved && _loadStarted)
                {
                    EnsureUnresolvedLoaded();
                }
            }
        }
    }

    public bool ShowEntries => _segment == BibliographySegment.Entries;

    public bool ShowUnresolved => _segment == BibliographySegment.Unresolved;

    /// <summary>The search box. Re-filters the LOADED set only — no
    /// core call, no announcement.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool IsLoadingEntries
    {
        get => _isLoadingEntries;
        private set
        {
            if (SetField(ref _isLoadingEntries, value))
            {
                NotifyStateChanged();
            }
        }
    }

    public bool IsLoadingUnresolved
    {
        get => _isLoadingUnresolved;
        private set
        {
            if (SetField(ref _isLoadingUnresolved, value))
            {
                NotifyStateChanged();
            }
        }
    }

    public string? EntriesError
    {
        get => _entriesError;
        private set
        {
            if (SetField(ref _entriesError, value))
            {
                OnPropertyChanged(nameof(EntriesErrorSpoken));
                NotifyStateChanged();
            }
        }
    }

    public string? UnresolvedError
    {
        get => _unresolvedError;
        private set
        {
            if (SetField(ref _unresolvedError, value))
            {
                OnPropertyChanged(nameof(UnresolvedErrorSpoken));
                NotifyStateChanged();
            }
        }
    }

    public string? EntriesErrorSpoken =>
        _entriesError is null ? null : CitationPhrase.BibliographyErrorSpoken(_entriesError);

    public string? UnresolvedErrorSpoken =>
        _unresolvedError is null ? null : CitationPhrase.BibliographyErrorSpoken(_unresolvedError);

    /// <summary>True when the vault configured no bibliography source
    /// at all — a distinct state from "sources exist but yielded no
    /// entries" (contract 5).</summary>
    public bool HasNoSources { get; private set; }

    /// <summary>
    /// Every state line below is SEGMENT-SCOPED. Without that, each one
    /// rendered on whichever segment happened to be showing: the
    /// entries grid carried "No unresolved citations. Every key in your
    /// notes has a bibliography entry." — a factual claim about data
    /// the lazy unresolved segment had never queried — and the
    /// unresolved grid carried "Loading bibliography…" and the
    /// entries-filter sentence. mac's per-segment if/else chain has
    /// this property structurally; independent booleans lost it.
    ///
    /// This one is shown whenever the loaded entry set is empty and
    /// nothing failed
    /// — mac's branch order exactly (BibliographyPanel.swift:104-108),
    /// which uses this one sentence for BOTH "no sources configured"
    /// and "sources configured but empty". Narrowing it to
    /// <see cref="HasNoSources"/> left the second case with no copy at
    /// all, so an empty-but-configured bibliography rendered as a
    /// silent "0 entries". <see cref="HasNoSources"/> still keeps the
    /// two states distinct internally (contract 5); inventing a second
    /// user-facing sentence would be a §W-C divergence mac does not
    /// have.
    /// </summary>
    public bool ShowNoSourcesState =>
        ShowEntries
        && _loadStarted
        && _entriesError is null
        && !_isLoadingEntries
        && _allEntries.Count == 0;

    /// <summary>Empty-with-a-query is a different sentence from
    /// empty-outright, so the user knows the filter caused it.</summary>
    public bool ShowNoFilterHitsState =>
        ShowEntries
        && !HasNoSources
        && _entriesError is null
        && !_isLoadingEntries
        && Entries.Count == 0
        && _allEntries.Count > 0;

    public string NoFilterHitsText => CitationPhrase.BibliographyNoFilterHits(_searchText);

    /// <summary>Only after the lazy unresolved load has actually run —
    /// otherwise this asserts "every key resolves" about a query that
    /// was never issued.</summary>
    public bool ShowUnresolvedEmptyState =>
        ShowUnresolved
        && _unresolvedLoadStarted
        && _unresolvedError is null
        && !_isLoadingUnresolved
        && Unresolved.Count == 0;

    public bool ShowEntriesLoading => ShowEntries && _isLoadingEntries;

    public bool ShowUnresolvedLoading => ShowUnresolved && _isLoadingUnresolved;

    /// <summary>A read failure needs a SURFACE, not just a property.
    /// These were computed and never bound, so a failed core read
    /// rendered as a silent "0 entries" — contract 5 satisfied in the
    /// view model and broken at the view.</summary>
    public bool ShowEntriesError => ShowEntries && _entriesError is not null;

    public bool ShowUnresolvedError => ShowUnresolved && _unresolvedError is not null;

    /// <summary>The grid summary — carries the truncation sentence
    /// verbatim when the cap bites, so the bound is announced rather
    /// than silent (contract 9).</summary>
    public string EntriesSummary
    {
        get
        {
            string counted = CitationPhrase.Counted(Entries.Count, "entry", "entries");
            // The cap is announced against the MATCHED count, not the
            // loaded one. Comparing the unfiltered total made a 6000-
            // entry library with 12 search hits read "12 entries.
            // Showing the first 5000 of 6000 entries." — nothing was
            // truncated, and the spoken bound became noise.
            return _matchedEntryCount > MaxEntryRows
                ? $"{counted}. {CitationPhrase.TruncationNotice(MaxEntryRows, _matchedEntryCount)}"
                : counted;
        }
    }

    private int _matchedEntryCount;

    /// <summary>Carries the truncation sentence on the same terms as
    /// the entries grid, so the unresolved cap is SPOKEN too (contract
    /// 9 / D-3) rather than silently dropping rows.</summary>
    public string UnresolvedSummary
    {
        get
        {
            string counted = CitationPhrase.Counted(
                Unresolved.Count, "unresolved key", "unresolved keys");
            return _totalUnresolvedCount > MaxEntryRows
                ? $"{counted}. {CitationPhrase.TruncationNotice(MaxEntryRows, _totalUnresolvedCount)}"
                : counted;
        }
    }

    /// <summary>Raised when Ctrl+J has settled on an entry the grid
    /// should land on. This is an EVENT rather than a bindable
    /// property because the outcome is settled asynchronously on the
    /// first press: the entries are still loading when the command
    /// returns, so a view that invoked the command and then read a
    /// property would see nothing and silently fail to move focus.
    /// </summary>
    internal event EventHandler? KeyFocusRequested;

    private string? _pendingKeyFocus;

    /// <summary>Atomically take the outstanding focus request. Returns
    /// null when there is none — a request is delivered to exactly one
    /// consumer, so a stale value can never steal focus from a later
    /// interaction.</summary>
    internal string? ConsumeKeyFocusRequest() =>
        Interlocked.Exchange(ref _pendingKeyFocus, null);

    private string? _pendingJumpKey;
    private Action<string, bool>? _pendingJumpOutcome;
    private long _pendingJumpGeneration;

    /// <summary>Ctrl+J's entry point. "Is this key in the bibliography"
    /// is only answerable once the entries have landed, and on the
    /// first press EnsureLoaded merely STARTS that load — answering
    /// from the empty pre-load list would tell the user an entry they
    /// are looking at does not exist. So the outcome is decided now if
    /// the entries are already published, and parked until the publish
    /// otherwise.</summary>
    internal void RequestKeyFocus(string key, Action<string, bool> announceOutcome)
    {
        EnsureLoaded();
        if (!IsLoadingEntries)
        {
            ResolveKeyFocus(key, announceOutcome);
            return;
        }
        _pendingJumpKey = key;
        _pendingJumpOutcome = announceOutcome;
        _pendingJumpGeneration = Interlocked.Read(ref _generation);
    }

    /// <summary>Membership is asked of the LOADED SET, not the visible
    /// rows: those are filtered by the search box and capped at
    /// <see cref="MaxEntryRows"/>, so a present entry can be absent
    /// from them.</summary>
    private void ResolveKeyFocus(string key, Action<string, bool> announceOutcome)
    {
        bool present = _allEntries.Any(
            entry => string.Equals(entry.Key, key, StringComparison.Ordinal));
        announceOutcome(key, present);
        if (!present || IsShutDown)
        {
            return;
        }
        _ = Interlocked.Exchange(ref _pendingKeyFocus, key);
        KeyFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Called once the entries publish. Generation-gated: a
    /// reload or a path change between the press and the publish
    /// invalidates the parked jump rather than focusing a stale grid.
    /// </summary>
    private void ResolveParkedKeyFocus()
    {
        if (_pendingJumpKey is not { } key || _pendingJumpOutcome is not { } outcome)
        {
            return;
        }
        _pendingJumpKey = null;
        _pendingJumpOutcome = null;
        if (_pendingJumpGeneration != Interlocked.Read(ref _generation))
        {
            return;
        }
        ResolveKeyFocus(key, outcome);
    }

    internal Action? InterleaveForTests { get; set; }

    internal long GenerationForTests => Interlocked.Read(ref _generation);

    internal int EntriesRequestIdForTests => _entriesRequestId;

    /// <summary>The prerequisite this leaf branches on. Attached once,
    /// by the workspace, before any load can start.</summary>
    internal void AttachSeed(BibliographySeed seed)
    {
        _seed = seed;
        GateWorkOn(seed.Completion);
    }

    /// <summary>
    /// Replace the outcome this leaf branches on, after an explicit
    /// re-seed. The seed itself is first-settle-wins — that is what
    /// makes teardown safe — so a retry cannot change it, and without
    /// this the leaf would keep refusing to read core on the strength
    /// of a failure the user has since fixed.
    /// </summary>
    internal void OverrideSeedOutcome(BibliographySeedOutcome outcome) =>
        _retrySeedOutcome = outcome;

    private BibliographySeedOutcome? _retrySeedOutcome;

    /// <summary>Called by the workspace once the vault's sources have
    /// settled. Notices are shown as-is; no sources configured is the
    /// distinct state (contract 5).</summary>
    public void ApplySeedOutcome(BibliographySeedOutcome outcome)
    {
        // The one publish into this leaf that had no shutdown guard,
        // and the only one whose input can be a Cancelled outcome.
        if (IsShutDown)
        {
            return;
        }
        LoadNotices = outcome.Notices;
        HasNoSources = !outcome.HasSources;
        OnPropertyChanged(nameof(LoadNotices));
        OnPropertyChanged(nameof(HasNoSources));
        NotifyStateChanged();
    }

    /// <summary>
    /// Whether core's bibliography tables may be read for this load.
    ///
    /// A failed seed leaves the PREVIOUS session's entries and index
    /// live (see <see cref="BibliographySeedOutcome.MayReadEntries"/>),
    /// so querying would present stale data as authoritative under a
    /// notice saying the load failed. Publishing nothing is the honest
    /// answer: the notice region already says why (contract 5, D-13).
    ///
    /// A seed still in flight cannot happen on a gated body — the gate
    /// is the whole point — but if the gate is ever removed, refusing
    /// is the fail-closed direction.
    /// </summary>
    private bool MayQueryCore =>
        _retrySeedOutcome is { } retry
            ? retry.MayReadEntries
            : _seed is null || _seed.Outcome?.MayReadEntries == true;

    /// <summary>Idempotent lazy load (the rail-reveal hook). Does NOT
    /// re-query once loaded — that is ForceReload's job.</summary>
    public void EnsureLoaded()
    {
        if (_loadStarted)
        {
            return;
        }
        _loadStarted = true;
        LoadEntries();
        if (_segment == BibliographySegment.Unresolved)
        {
            EnsureUnresolvedLoaded();
        }
    }

    /// <summary>
    /// Teardown drops both: nothing should speak into a closing
    /// workspace, and an unconsumed focus request points at a grid
    /// that is going away.
    /// </summary>
    private void DropKeyFocusState()
    {
        _ = Interlocked.Exchange(ref _pendingKeyFocus, null);
        _pendingJumpKey = null;
        _pendingJumpOutcome = null;
    }

    /// <summary>
    /// A reload mid-jump RE-TARGETS the parked request at the new
    /// identity instead of discarding it.
    ///
    /// Ctrl+J semantics: the latest press wins and exactly one
    /// announcement is heard — a superseded press is silent only
    /// because its successor speaks. A press whose answer is discarded
    /// with no successor is different: the user pressed a key and
    /// heard NOTHING, which in a screen-reader-first app reads as a
    /// dead keystroke. So the unconsumed focus request is dropped (the
    /// rows it named are being cleared) but the parked jump survives
    /// and resolves against the reloaded set.
    /// </summary>
    private void RetargetKeyFocusAfterReload(long generation)
    {
        _ = Interlocked.Exchange(ref _pendingKeyFocus, null);
        if (_pendingJumpKey is not null)
        {
            _pendingJumpGeneration = generation;
        }
    }

    /// <summary>Explicit reload: a new identity for both segments, so
    /// every parked publish is invalidated first (contract 3).</summary>
    public void ForceReload()
    {
        long generation = Interlocked.Increment(ref _generation);
        RetargetKeyFocusAfterReload(generation);
        _loadStarted = true;
        Entries.Clear();
        Unresolved.Clear();
        _allEntries = [];
        _totalEntryCount = 0;
        _unresolvedLoadStarted = false;
        EntriesError = null;
        UnresolvedError = null;
        NotifyStateChanged();
        LoadEntries();
        if (_segment == BibliographySegment.Unresolved)
        {
            EnsureUnresolvedLoaded();
        }
    }

    private bool _unresolvedLoadStarted;

    private void EnsureUnresolvedLoaded()
    {
        if (_unresolvedLoadStarted)
        {
            return;
        }
        _unresolvedLoadStarted = true;
        LoadUnresolved();
    }

    private void LoadEntries()
    {
        long generation = Interlocked.Read(ref _generation);
        int requestId = Interlocked.Increment(ref _entriesRequestId);
        IsLoadingEntries = true;
        StartWork(() =>
        {
            if (!MayQueryCore)
            {
                // Stale-on-failure refusal: publish NOTHING rather than
                // last session's entries (D-13).
                InterleaveForTests?.Invoke();
                Post(() => PublishEntries(generation, requestId, [], null));
                return;
            }
            try
            {
                var entries = _session.GetBibliographyEntries();
                InterleaveForTests?.Invoke();
                Post(() => PublishEntries(generation, requestId, entries, null));
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                InterleaveForTests?.Invoke();
                Post(() => PublishEntries(generation, requestId, [], exception.Message));
            }
        });
    }

    private void LoadUnresolved()
    {
        long generation = Interlocked.Read(ref _generation);
        int requestId = Interlocked.Increment(ref _unresolvedRequestId);
        IsLoadingUnresolved = true;
        StartWork(() =>
        {
            if (!MayQueryCore)
            {
                InterleaveForTests?.Invoke();
                Post(() => PublishUnresolved(generation, requestId, [], null));
                return;
            }
            try
            {
                var unresolved = _session.ListUnresolvedCitations();
                InterleaveForTests?.Invoke();
                Post(() => PublishUnresolved(generation, requestId, unresolved, null));
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                InterleaveForTests?.Invoke();
                Post(() => PublishUnresolved(generation, requestId, [], exception.Message));
            }
        });
    }

    internal void PublishEntries(
        long generation, int requestId, BibEntry[] entries, string? loadError)
    {
        if (IsShutDown
            || generation != Interlocked.Read(ref _generation)
            || requestId != _entriesRequestId)
        {
            return;
        }
        _allEntries = loadError is null ? entries : [];
        _totalEntryCount = _allEntries.Count;
        IsLoadingEntries = false;
        EntriesError = loadError;
        ApplyFilter();
        ResolveParkedKeyFocus();
    }

    internal void PublishUnresolved(
        long generation, int requestId, UnresolvedCitation[] rows, string? loadError)
    {
        if (IsShutDown
            || generation != Interlocked.Read(ref _generation)
            || requestId != _unresolvedRequestId)
        {
            return;
        }
        Unresolved.Clear();
        if (loadError is null)
        {
            // Bounded like the entries grid (D-3). Unresolved is the
            // MORE likely of the two to be huge: a vault with no
            // bibliography configured still runs the query, and every
            // distinct (file, key) pair in the vault comes back.
            foreach (var row in rows.Take(MaxEntryRows))
            {
                Unresolved.Add(new UnresolvedRowViewModel(row));
            }
        }
        _totalUnresolvedCount = loadError is null ? rows.Length : 0;
        IsLoadingUnresolved = false;
        UnresolvedError = loadError;
        OnPropertyChanged(nameof(UnresolvedSummary));
        NotifyStateChanged();
        RaiseUnresolvedPublished();
    }

    private int _totalUnresolvedCount;

    /// <summary>Re-project the loaded set through the mac predicate
    /// and the row cap. Never queries core.</summary>
    /// <summary>
    /// Raised ONCE when the entry rows have finished changing.
    ///
    /// The window used to rebind the grid from
    /// <c>Entries.CollectionChanged</c>, and this method clears and
    /// re-adds row by row — so a publish of N rows triggered N+2 full
    /// rebinds, each copying every row and rebuilding all five columns.
    /// That is O(N²) notifications on the UI thread per publish, and
    /// again on every keystroke in the search box. One signal per
    /// publish is what the grid actually needs: Bind is a whole-surface
    /// reset, not an incremental update.
    /// </summary>
    internal event EventHandler? EntriesPublished;

    internal event EventHandler? UnresolvedPublished;

    internal void RaiseUnresolvedPublished() =>
        UnresolvedPublished?.Invoke(this, EventArgs.Empty);

    private void ApplyFilter()
    {
        Entries.Clear();
        IEnumerable<BibEntry> matching = Matching(_allEntries, _searchText);
        // Materialise only when a filter actually ran: an empty query
        // returns the loaded list itself, and copying it per keystroke
        // would be pure waste.
        IReadOnlyList<BibEntry> matched =
            matching as IReadOnlyList<BibEntry> ?? [.. matching];
        _matchedEntryCount = matched.Count;
        for (int i = 0; i < matched.Count && i < MaxEntryRows; i++)
        {
            Entries.Add(new BibliographyRowViewModel(matched[i]));
        }
        OnPropertyChanged(nameof(EntriesSummary));
        NotifyStateChanged();
        EntriesPublished?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The mac filter predicate verbatim
    /// (AppState.swift:23667-23684): trimmed, lowercased, substring
    /// match over title, key, and every author's family AND given
    /// name. Core's SearchBibliography matches neither key nor given
    /// name, which is why it is not used (D-4).</summary>
    internal static IEnumerable<BibEntry> Matching(
        IReadOnlyList<BibEntry> entries, string query)
    {
        string q = query.Trim().ToLowerInvariant();
        if (q.Length == 0)
        {
            return entries;
        }
        // Case-insensitive COMPARISON rather than lowercasing every
        // field: this runs over the whole loaded set on each keystroke,
        // and ToLowerInvariant allocated a string per field per entry
        // per character typed. Same predicate, same results.
        return entries.Where(entry =>
            entry.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
            || entry.Key.Contains(q, StringComparison.OrdinalIgnoreCase)
            || entry.Authors.Any(author =>
                author.Family.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (author.Given is { } given
                    && given.Contains(q, StringComparison.OrdinalIgnoreCase))));
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(ShowNoSourcesState));
        OnPropertyChanged(nameof(ShowNoFilterHitsState));
        OnPropertyChanged(nameof(ShowUnresolvedEmptyState));
        OnPropertyChanged(nameof(ShowEntriesLoading));
        OnPropertyChanged(nameof(ShowUnresolvedLoading));
        OnPropertyChanged(nameof(ShowEntriesError));
        OnPropertyChanged(nameof(ShowUnresolvedError));
        OnPropertyChanged(nameof(NoFilterHitsText));
    }

    internal override void Shutdown()
    {
        base.Shutdown();
        Interlocked.Increment(ref _generation);
        // Drop any focus request nobody consumed, so a leaf that binds
        // later cannot inherit it.
        DropKeyFocusState();
    }
}

/// <summary>
/// One bibliography entry row. A plain snapshot — the leaf rebuilds
/// rows on every publish and filter change.
///
/// The display strings are COMPUTED ONCE, not on each access. The grid
/// sorts through comparators that read them, so an n log n sort over
/// the 5000-row cap called EntrySubtitle ~122,000 times, each running
/// a LINQ projection over the author array plus a join — a visible
/// freeze and tens of MB of garbage for one Ctrl+Alt+S.
/// </summary>
internal sealed class BibliographyRowViewModel(BibEntry entry)
{
    public BibEntry Entry { get; } = entry;

    public string TitleLine { get; } = CitationPhrase.EntryTitleLine(entry);

    public string Subtitle { get; } = CitationPhrase.EntrySubtitle(entry);

    public string? YearText { get; } = CitationPhrase.YearText(entry.Year);

    public string Journal { get; } = entry.Journal ?? "";

    public string Key { get; } = entry.Key;

    public string RowDescription { get; } = CitationPhrase.EntryRowDescription(entry);

    public string RowHelp => CitationPhrase.CitationRowHelp;
}

/// <summary>One unresolved citation row (key + citing file).</summary>
internal sealed class UnresolvedRowViewModel(UnresolvedCitation row)
{
    public UnresolvedCitation Row { get; } = row;

    public string Key { get; } = row.Key;

    public string Path { get; } = row.Path.Replace('\\', '/');

    public string RowDescription { get; } =
        CitationPhrase.UnresolvedRowLabel(row.Key, row.Path);
}
