// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>
/// W4-5 (#737): the note-scoped citations leaf — the mac
/// CitationsPanel twin.
///
/// Rows are built from core artifacts and NOTHING ELSE (feature
/// contract 1): a row's spoken name is
/// <c>RenderedCitation.SpeechText</c> verbatim, with the single
/// sanctioned "Unresolved citation key. " prefix. The VM never
/// composes citation speech, never parses <c>VisualText</c>, and
/// never invents a style id — the only ids it passes to
/// <c>RenderCitation</c> come from <c>CitationsPrefs</c>.
///
/// Load shape mirrors mac exactly (AppState.swift:12120-12175):
/// an empty style id yields a PLACEHOLDER per reference with no
/// render calls at all, and a render failure fails the WHOLE load
/// rather than degrading row-by-row — one bad style is a panel-level
/// condition, not eight independently broken rows.
///
/// Publication is guarded by generation + requestId (contract 3):
/// a path change bumps the generation and clears rows synchronously,
/// so no stale row is ever actionable while a newer read is in
/// flight.
/// </summary>
internal sealed class CitationsPanelViewModel : PanelWorkScheduler
{
    private readonly VaultSession _session;
    private readonly Action<A11yEvent> _announce;
    private long _generation;
    private int _requestId;
    private string? _path;
    private string? _announcedPath;
    private bool _isLoading;
    private string? _loadError;

    public CitationsPanelViewModel(
        VaultSession session,
        Action<A11yEvent> announce,
        bool synchronousForTests = false)
        : base(synchronousForTests)
    {
        _session = session;
        _announce = announce;
    }

    public ObservableCollection<CitationRowViewModel> Rows { get; } = [];

    /// <summary>The structural references behind the rows, 1:1 with
    /// <see cref="Rows"/>. The summary sheet counts unique keys from
    /// these — the rendered rows cannot expose multi-key sites
    /// (contract 12).</summary>
    public IReadOnlyList<CitationReference> References { get; private set; } = [];

    public string? Path => _path;

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    /// <summary>Non-null after a failed load; rows are cleared so no
    /// ghost survives the failure.</summary>
    public string? LoadError
    {
        get => _loadError;
        private set
        {
            if (SetField(ref _loadError, value))
            {
                OnPropertyChanged(nameof(ErrorSpoken));
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    public string? ErrorSpoken =>
        _loadError is null ? null : CitationPhrase.CitationsErrorSpoken(_loadError);

    public bool ShowNoFileState => _path is null;

    public bool ShowEmptyState =>
        _path is not null && Rows.Count == 0 && _loadError is null && !_isLoading;

    public string NoFileText => CitationPhrase.CitationsNoFile;

    public string EmptyText => CitationPhrase.CitationsEmpty;

    public string LoadingVisibleText => CitationPhrase.CitationsLoadingVisible;

    public string LoadingSpokenText => CitationPhrase.CitationsLoadingSpoken;

    /// <summary>Test seam: runs between the worker's read and its
    /// publish so interleavings are driven explicitly rather than
    /// raced.</summary>
    internal Action? InterleaveForTests { get; set; }

    internal int RequestIdForTests => _requestId;

    internal long GenerationForTests => Interlocked.Read(ref _generation);

    /// <summary>The active note changed. A DIFFERENT path is an
    /// identity change: bump the generation (parked work for the old
    /// path can never publish), clear rows synchronously, and re-arm
    /// the once-per-path count announcement.</summary>
    public void NoteChanged(string? path)
    {
        if (string.Equals(path, _path, StringComparison.Ordinal))
        {
            return;
        }
        _path = path;
        Interlocked.Increment(ref _generation);
        Rows.Clear();
        References = [];
        LoadError = null;
        _announcedPath = null;
        NotifyStateChanged();
        if (path is null)
        {
            IsLoading = false;
            return;
        }
        Refresh();
    }

    /// <summary>
    /// A note was written. Re-read only when it is the note on screen.
    ///
    /// <see cref="Refresh"/> documented itself as the "post-save
    /// funnel" entry point and had NO production caller — every save
    /// path called <c>Panels.NoteSaved</c> and none of them told the
    /// citations leaf. So adding a citation and saving left the panel
    /// showing the old rows, the new key never appeared, and the
    /// summary sheet kept reporting the old count until the user tabbed
    /// away and back.
    /// </summary>
    public void NoteSaved(string path)
    {
        if (string.Equals(path, _path, StringComparison.Ordinal))
        {
            Refresh();
        }
    }

    /// <summary>Re-read the CURRENT path (post-save funnel, explicit
    /// reload). Same generation, new requestId.</summary>
    public void Refresh()
    {
        if (_path is not { } path)
        {
            return;
        }
        long generation = Interlocked.Read(ref _generation);
        int requestId = Interlocked.Increment(ref _requestId);
        IsLoading = true;
        StartWork(() =>
        {
            try
            {
                var references = _session.ListCitationsInFile(path);
                // A FAILED seed leaves the previous session's BibIndex
                // live (session.rs:2085-2088 / 8491), and RenderCitation
                // resolves against it — so rendering here would show
                // last session's title and year as this session's
                // answer, next to a bibliography leaf that is correctly
                // refusing to show anything (D-13). Rendering nothing
                // yields placeholders: the key, and no claim about
                // whether it resolves. Contract 2's "core never looked
                // one up" state is the honest one.
                string styleId = MayResolveKeys ? ActiveStyleId() : "";
                RenderedCitation[]? rendered = null;
                if (styleId.Length > 0)
                {
                    rendered = new RenderedCitation[references.Length];
                    for (int i = 0; i < references.Length; i++)
                    {
                        rendered[i] = _session.RenderCitation(references[i], styleId);
                    }
                }
                InterleaveForTests?.Invoke();
                Post(() => Publish(generation, requestId, path, references, rendered, null));
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                InterleaveForTests?.Invoke();
                Post(() => Publish(generation, requestId, path, [], null, exception.Message));
            }
        });
    }

    private BibliographySeed? _seed;

    /// <summary>The prerequisite this leaf waits on AND branches on.
    /// Attached once, by the workspace, before any load can start.
    /// </summary>
    internal void AttachSeed(BibliographySeed seed)
    {
        _seed = seed;
        GateWorkOn(seed.Completion);
    }

    /// <summary>
    /// Whether keys may be resolved against core's bibliography.
    ///
    /// The settled lifecycle design says a failed seed must stop the
    /// LEAVES — plural — reading core. The first implementation gave
    /// this branch to the bibliography leaf only, so the two surfaces
    /// disagreed: one refused while the other rendered last session's
    /// data as authoritative. Three independent reviewers found it.
    /// </summary>
    private bool MayResolveKeys =>
        _retrySeedOutcome is { } retry
            ? retry.MayReadEntries
            : _seed is null || _seed.Outcome?.MayReadEntries == true;

    private BibliographySeedOutcome? _retrySeedOutcome;

    /// <summary>Replace the outcome after an explicit re-seed; the seed
    /// itself is first-settle-wins and cannot be re-settled.</summary>
    internal void OverrideSeedOutcome(BibliographySeedOutcome outcome) =>
        _retrySeedOutcome = outcome;

    /// <summary>The configured default style's id, or empty when none
    /// is configured. Core matches ids, not paths, so the file stem is
    /// the id (contract 2: a style id is never invented).</summary>
    private string ActiveStyleId()
    {
        string? defaultStyle = _session.CitationsPrefs().DefaultStyle;
        return string.IsNullOrEmpty(defaultStyle)
            ? string.Empty
            : System.IO.Path.GetFileNameWithoutExtension(defaultStyle);
    }

    /// <summary>Publish seam (internal for deterministic test
    /// ordering). Mutations before notifications; stale tokens
    /// discard silently — they cannot announce.</summary>
    internal void Publish(
        long generation,
        int requestId,
        string path,
        CitationReference[] references,
        RenderedCitation[]? rendered,
        string? loadError)
    {
        if (IsShutDown
            || generation != Interlocked.Read(ref _generation)
            || requestId != _requestId)
        {
            return;
        }
        Rows.Clear();
        if (loadError is null)
        {
            References = references;
            for (int i = 0; i < references.Length; i++)
            {
                Rows.Add(rendered is null
                    ? CitationRowViewModel.Placeholder(references[i])
                    : CitationRowViewModel.FromRendered(references[i], rendered[i]));
            }
        }
        else
        {
            References = [];
        }
        IsLoading = false;
        LoadError = loadError;
        NotifyStateChanged();

        // A1: once per path, only for a NON-EMPTY set, never on a
        // stale publish (contract 4).
        if (loadError is null
            && Rows.Count > 0
            && !string.Equals(_announcedPath, path, StringComparison.Ordinal))
        {
            _announcedPath = path;
            _announce(new A11yEvent.CitationsCount((uint)Rows.Count));
        }
        RowsPublished?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised once per publish, after the rows and references
    /// are in place. Lets a caller that arrived mid-load wait for a
    /// real answer instead of reading zero.</summary>
    internal event EventHandler? RowsPublished;

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(ShowNoFileState));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    internal override void Shutdown()
    {
        base.Shutdown();
        Interlocked.Increment(ref _generation);
    }
}

/// <summary>One citation row. A plain snapshot — the leaf rebuilds
/// every row on publish, so nothing here notifies.</summary>
internal sealed class CitationRowViewModel
{
    private CitationRowViewModel(
        CitationReference reference,
        RenderedCitation? rendered,
        string displayText,
        string automationName,
        bool isUnresolved)
    {
        Reference = reference;
        Rendered = rendered;
        DisplayText = displayText;
        AutomationName = automationName;
        IsUnresolved = isUnresolved;
    }

    public CitationReference Reference { get; }

    /// <summary>Null for a placeholder row — there is no rendered
    /// citation to expand, so the details overlay has nothing to
    /// show but the raw reference.</summary>
    public RenderedCitation? Rendered { get; }

    public string DisplayText { get; }

    /// <summary>Core's speech verbatim (contract 1), prefixed only
    /// when unresolved.</summary>
    public string AutomationName { get; }

    public bool IsUnresolved { get; }

    /// <summary>
    /// The expand hint is offered ONLY when there is something to
    /// expand. A placeholder row has no rendered citation — core never
    /// looked the key up (contract 2) — and the workspace seam refuses
    /// to open a sheet for it, so advertising "Activate to expand
    /// citation fields." promised an affordance that silently did
    /// nothing. That is the whole panel's state whenever no cite_style
    /// is configured, which is an ordinary vault, not an edge case.
    /// </summary>
    public string AutomationHelpText =>
        Rendered is null ? "" : CitationPhrase.CitationRowHelp;

    /// <summary>Whether activating this row can open the details sheet.
    /// </summary>
    public bool CanExpand => Rendered is not null;

    public string UnresolvedBadge => CitationPhrase.UnresolvedBadge;

    /// <summary>A rendered row. The unresolved predicate is
    /// CORE-DERIVED (contract 7): no bib entry despite a real style
    /// id — never a substring search for "?" in the visual text. A
    /// MIXED site (one key resolved, one not) also lands here, which
    /// matches core's own speech for it.</summary>
    public static CitationRowViewModel FromRendered(
        CitationReference reference, RenderedCitation rendered)
    {
        bool unresolved = rendered.BibEntry is null && rendered.StyleId.Length > 0;
        string display = rendered.VisualText.Length > 0 ? rendered.VisualText : reference.Raw;
        string name = unresolved
            ? CitationPhrase.UnresolvedRowSpeech(rendered.SpeechText)
            : rendered.SpeechText;
        return new CitationRowViewModel(reference, rendered, display, name, unresolved);
    }

    /// <summary>A placeholder row: no style is configured, so core
    /// rendered nothing. Shows the reference's raw text and speaks
    /// the mac placeholder phrase. Never carries the Unresolved badge
    /// — the badge means "core looked and found nothing", and here
    /// core never looked (contract 2).</summary>
    public static CitationRowViewModel Placeholder(CitationReference reference) =>
        new(
            reference,
            rendered: null,
            displayText: reference.Raw,
            automationName: CitationPhrase.PlaceholderSpeech(reference),
            isUnresolved: false);
}
