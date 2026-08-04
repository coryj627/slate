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
        private set => SetField(ref _isLoadingEntries, value);
    }

    public bool IsLoadingUnresolved
    {
        get => _isLoadingUnresolved;
        private set => SetField(ref _isLoadingUnresolved, value);
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

    public bool ShowNoSourcesState =>
        HasNoSources && _entriesError is null && !_isLoadingEntries;

    /// <summary>Empty-with-a-query is a different sentence from
    /// empty-outright, so the user knows the filter caused it.</summary>
    public bool ShowNoFilterHitsState =>
        !HasNoSources
        && _entriesError is null
        && !_isLoadingEntries
        && Entries.Count == 0
        && _allEntries.Count > 0;

    public string NoFilterHitsText => CitationPhrase.BibliographyNoFilterHits(_searchText);

    public bool ShowUnresolvedEmptyState =>
        _unresolvedError is null && !_isLoadingUnresolved && Unresolved.Count == 0;

    /// <summary>The grid summary — carries the truncation sentence
    /// verbatim when the cap bites, so the bound is announced rather
    /// than silent (contract 9).</summary>
    public string EntriesSummary
    {
        get
        {
            string counted = CitationPhrase.Counted(Entries.Count, "entry", "entries");
            return _totalEntryCount > MaxEntryRows
                ? $"{counted}. {CitationPhrase.TruncationNotice(MaxEntryRows, _totalEntryCount)}"
                : counted;
        }
    }

    public string UnresolvedSummary =>
        CitationPhrase.Counted(Unresolved.Count, "unresolved key", "unresolved keys");

    internal Action? InterleaveForTests { get; set; }

    internal long GenerationForTests => Interlocked.Read(ref _generation);

    internal int EntriesRequestIdForTests => _entriesRequestId;

    /// <summary>Called by the workspace once the vault's sources have
    /// been seeded. Notices are shown as-is; an empty source list is
    /// the distinct "no sources configured" state.</summary>
    public void ApplySeedOutcome(IReadOnlyList<string> notices, bool hasSources)
    {
        LoadNotices = notices;
        HasNoSources = !hasSources;
        OnPropertyChanged(nameof(LoadNotices));
        OnPropertyChanged(nameof(HasNoSources));
        NotifyStateChanged();
    }

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

    /// <summary>Explicit reload: a new identity for both segments, so
    /// every parked publish is invalidated first (contract 3).</summary>
    public void ForceReload()
    {
        Interlocked.Increment(ref _generation);
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
            foreach (var row in rows)
            {
                Unresolved.Add(new UnresolvedRowViewModel(row));
            }
        }
        IsLoadingUnresolved = false;
        UnresolvedError = loadError;
        OnPropertyChanged(nameof(UnresolvedSummary));
        NotifyStateChanged();
    }

    /// <summary>Re-project the loaded set through the mac predicate
    /// and the row cap. Never queries core.</summary>
    private void ApplyFilter()
    {
        Entries.Clear();
        foreach (var entry in Matching(_allEntries, _searchText).Take(MaxEntryRows))
        {
            Entries.Add(new BibliographyRowViewModel(entry));
        }
        OnPropertyChanged(nameof(EntriesSummary));
        NotifyStateChanged();
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
        return entries.Where(entry =>
            entry.Title.ToLowerInvariant().Contains(q, StringComparison.Ordinal)
            || entry.Key.ToLowerInvariant().Contains(q, StringComparison.Ordinal)
            || entry.Authors.Any(author =>
                author.Family.ToLowerInvariant().Contains(q, StringComparison.Ordinal)
                || (author.Given is { } given
                    && given.ToLowerInvariant().Contains(q, StringComparison.Ordinal))));
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(ShowNoSourcesState));
        OnPropertyChanged(nameof(ShowNoFilterHitsState));
        OnPropertyChanged(nameof(ShowUnresolvedEmptyState));
        OnPropertyChanged(nameof(NoFilterHitsText));
    }

    internal override void Shutdown()
    {
        base.Shutdown();
        Interlocked.Increment(ref _generation);
    }
}

/// <summary>One bibliography entry row. A plain snapshot — the leaf
/// rebuilds rows on every publish and filter change.</summary>
internal sealed class BibliographyRowViewModel(BibEntry entry)
{
    public BibEntry Entry { get; } = entry;

    public string TitleLine => CitationPhrase.EntryTitleLine(Entry);

    public string Subtitle => CitationPhrase.EntrySubtitle(Entry);

    public string? YearText => CitationPhrase.YearText(Entry.Year);

    public string Journal => Entry.Journal ?? "";

    public string Key => Entry.Key;

    public string RowDescription => CitationPhrase.EntryRowDescription(Entry);

    public string RowHelp => CitationPhrase.CitationRowHelp;
}

/// <summary>One unresolved citation row (key + citing file).</summary>
internal sealed class UnresolvedRowViewModel(UnresolvedCitation row)
{
    public UnresolvedCitation Row { get; } = row;

    public string Key => Row.Key;

    public string Path => Row.Path.Replace('\\', '/');

    public string RowDescription => CitationPhrase.UnresolvedRowLabel(Row.Key, Row.Path);
}
