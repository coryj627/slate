// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Text;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// One matched range inside a rendered command label, in <b>UTF-16 code
/// units</b> — the units a C# string and a WPF <c>Run</c> index by.
/// Core reports matches as UTF-8 byte offsets; the single conversion
/// helper (<see cref="CommandPaletteViewModel.ToMatchRuns"/>, PINV-6)
/// produces this shape and nothing else re-derives it.
/// </summary>
internal sealed record CommandPaletteMatchRun(int Start, int Length);

/// <summary>
/// One contiguous stretch of a label, flagged for bolding. The
/// segments of a row always concatenate back to the full label, so a
/// hint-only match (no label runs) renders as exactly one unbolded
/// segment — never bold-everything, never a hidden row (contract P6).
/// </summary>
internal sealed record CommandPaletteLabelSegment(string Text, bool IsMatch);

/// <summary>
/// One palette row. Purely data: the view binds it, the view model
/// navigates it. Match runs are pre-converted to UTF-16; no WPF object
/// is built here.
/// </summary>
internal sealed class CommandPaletteRowViewModel
{
    internal CommandPaletteRowViewModel(
        Command command,
        string sectionTitle,
        IReadOnlyList<CommandPaletteMatchRun> labelMatchRuns,
        IReadOnlyList<CommandPaletteLabelSegment> labelSegments,
        int score,
        string? disabledReason)
    {
        Id = command.Id;
        Label = command.Label;
        AccessibilityHint = command.AccessibilityHint;
        HotkeyHint = command.HotkeyHint;
        Section = command.Section;
        SectionTitle = sectionTitle;
        LabelMatchRuns = labelMatchRuns;
        LabelSegments = labelSegments;
        Score = score;
        DisabledReason = disabledReason;
    }

    public string Id { get; }

    public string Label { get; }

    public string? AccessibilityHint { get; }

    /// <summary>
    /// The registration-supplied chord hint. Presentation-only here;
    /// the accessible name that composes label + <i>spoken</i> chord
    /// (contract P6) is the view's, because the spoken form is walked
    /// over the chord table (PINV-5), which this view model does not
    /// and must not own.
    /// </summary>
    public string? HotkeyHint { get; }

    public CommandSection Section { get; }

    /// <summary>
    /// The title of the section core actually placed this row in — the
    /// view groups on this, never on <see cref="Section"/>.
    /// </summary>
    /// <remarks>
    /// A Recent row keeps its native <see cref="Section"/> while core has
    /// excluded it from that section, so grouping by the enum would file
    /// it under the section it was deliberately lifted out of.
    /// </remarks>
    public string SectionTitle { get; }

    /// <summary>
    /// The row's whole accessible name: the label, plus the spoken chord
    /// when one exists. ONE name per row (contract P6) — the bolded runs
    /// and the visible chord are presentation-only.
    /// </summary>
    /// <remarks>
    /// The spoken form is walked over the chord table's producer
    /// (PINV-5), so composing here consumes that single authority rather
    /// than restating it — and XAML cannot compose it at all.
    /// </remarks>
    public string AccessibleName =>
        string.IsNullOrEmpty(HotkeyHint)
            ? Label
            : $"{Label}, {WindowsHotkeySpoken.Spoken(HotkeyHint)}";

    /// <summary>Converted match ranges, UTF-16 code units.</summary>
    public IReadOnlyList<CommandPaletteMatchRun> LabelMatchRuns { get; }

    /// <summary>
    /// The label split into bold / non-bold stretches — the binding
    /// surface for the view, derived from <see cref="LabelMatchRuns"/>
    /// with no second pass over byte offsets.
    /// </summary>
    public IReadOnlyList<CommandPaletteLabelSegment> LabelSegments { get; }

    /// <summary>
    /// Core's winning fuzzy score. <b>Not a display order</b> (contract
    /// P1): exposed only so a caller can identify the strongest match
    /// overall without re-scoring. Nothing in this file sorts by it.
    /// </summary>
    public int Score { get; }

    /// <summary>
    /// Why this command cannot run, captured when the row was built.
    /// Drives the visible caption and <c>HelpText</c>; the Enter gate
    /// and the selection announcement re-ask the resolver instead of
    /// trusting this render-time value (contract P8).
    /// </summary>
    public string? DisabledReason { get; }

    /// <summary>
    /// Unavailable rows keep their row, their place in the selection
    /// cycle, and their selectability — only activation is refused
    /// (contract P8). This never gates <c>IsEnabled</c>.
    /// </summary>
    public bool IsUnavailable => DisabledReason is not null;
}

/// <summary>
/// What one query change cost, split by step — the #1254 profile
/// (SLATE_UIA_DIAGNOSTICS=1 only). <see cref="QueryChange"/> is 0 for the
/// open's own recompute and counts keystrokes after it; the three spans
/// are <see cref="System.Diagnostics.Stopwatch"/> ticks, and
/// <see cref="StartTimestamp"/> anchors the view's end-to-end reading.
/// </summary>
internal sealed record CommandPaletteRecomputeTiming(
    int QueryChange,
    int Rows,
    long StartTimestamp,
    long RankTicks,
    long AvailabilityTicks,
    long RowBuildTicks);

/// <summary>
/// One rendered palette section, in the order core returned it.
/// </summary>
internal sealed class CommandPaletteSectionViewModel
{
    internal CommandPaletteSectionViewModel(
        string title,
        CommandSection? kind,
        IReadOnlyList<CommandPaletteRowViewModel> rows)
    {
        Title = title;
        Kind = kind;
        Rows = rows;
    }

    /// <summary>
    /// Canonical copy from core, rendered verbatim. The host never maps
    /// a <see cref="CommandSection"/> to a heading string (contract P1).
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// <see langword="null"/> is the synthetic Recent section.
    /// Identity only — never a sort key.
    /// </summary>
    public CommandSection? Kind { get; }

    public IReadOnlyList<CommandPaletteRowViewModel> Rows { get; }
}

/// <summary>
/// W5-1 command palette state (#741). Core ranks, sections, and titles;
/// this view model snapshots, navigates, gates, and announces. It
/// renders nothing and builds no WPF objects.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ranked off the UI thread</b> (locked decision 05 §4, principle 2:
/// "Callers dispatch off the UI thread"; #1275). The open-time snapshot
/// load, every ranking and the recents write run on a
/// <see cref="ICommandPaletteWorkLane"/>; the FFI stays synchronous, the
/// caller does not. Each query change is a request tagged with a
/// generation, and its result is <i>published</i> on the owning thread
/// only while it is still the latest: rows, sections, selection and the
/// count's window move together at publication, never at request, so
/// what the list shows, what the keys navigate and what Enter runs are
/// always one query's (P18). Everything that decides what is SAID is
/// applied at publication too — the P7 selection rule reads the row the
/// user is on when the rows land, and the P10 count names the published
/// query — so R-11's one selection per query change holds across the
/// asynchronous hop. A superseded result is discarded unseen; a superseded
/// request still waiting its turn never runs.
/// </para>
/// <para>
/// Enter while a newer query's rows are still on their way runs once
/// they land, against the selection they bring — the user asked to run
/// what they typed for, not the previous keystroke's row. Moving the
/// selection meanwhile withdraws that request.
/// </para>
/// <para>
/// <b>No <c>ICommand</c> surface.</b> The palette exposes methods, not
/// commands: PR-4 records that command objects with no invocation path
/// are a liability the registration-forward drift test now surfaces, so
/// this view model does not speculatively add eight of them. The host
/// binds key handlers to these methods.
/// </para>
/// </remarks>
internal sealed class CommandPaletteViewModel : BindableBase
{
    // W0.5-3 residue: the three host-composed palette strings
    // inventoried by contract P14, transcribed from the mac view. The
    // deliberate asymmetry — the visible no-matches line quotes the
    // query, its accessible name does not — is part of the contract.
    internal const string EmptyRegistryTitle = "No commands available";
    internal const string EmptyRegistryDetail = "Open a vault to access the palette.";
    internal const string NoMatchesTitle = "No matches";


    /// <summary>The filter count's trailing window (P10, amended): the search
    /// overlay's 150 ms, so a typed query speaks once for its latest state.</summary>
    internal const int FilterCountWindowMilliseconds = 150;

    /// <summary>The #1254 profile times the open and this many keystrokes
    /// after it — the first keystroke is the one the NVDA pass heard stall.</summary>
    internal const int TimedKeystrokes = 3;

    private readonly IPaletteCommandSource _source;
    private readonly Action<A11yEvent> _announce;
    private readonly Func<CancellationToken, Task> _filterCountWindow;
    private readonly ICommandPaletteWorkLane _lane;
    private readonly Func<Command[], string, string[], string[], PaletteSection[]> _rank;
    private readonly SynchronizationContext? _uiContext;
    private readonly int _ownerThreadId;
    private CancellationTokenSource? _filterCountCancellation;
    private Task? _filterCountCompletion;

    private Command[] _snapshot = [];
    private bool _snapshotLoaded;
    private Task<PaletteSnapshot> _snapshotLoad = Task.FromResult(PaletteSnapshot.Empty);
    private CancellationTokenSource? _snapshotCancellation;
    private CancellationTokenSource? _rankCancellation;
    private int _rankGeneration;
    private int _publishedGeneration;
    private bool _invokeWhenPublished;
    private IReadOnlyList<CommandPaletteSectionViewModel> _sections = [];
    private CommandPaletteRowViewModel[] _rows = [];
    private string _query = string.Empty;
    private string _publishedQuery = string.Empty;
    private string? _selectedId;
    private CommandPaletteRowViewModel? _selectedRow;
    private bool _isOpen;
    private bool _suppressSelectionAnnouncement;
    private int _pageSize = 10;
    private int _queryChangesSinceOpen;

    /// <param name="source">The command layer (registry, availability, recents).</param>
    /// <param name="announce">The shell's one announcement funnel.</param>
    /// <param name="filterCountWindow">P10's trailing window; facts hold it.</param>
    /// <param name="lane">Where the FFI work runs — the serialized
    /// <see cref="CommandPaletteWorkLane"/> unless a fact supplies another.</param>
    /// <param name="rank">Core's ranking, <c>palette_sections</c>, unless a
    /// fact parks it.</param>
    /// <remarks>
    /// Constructed on the thread that owns the palette — the dispatcher's
    /// in the shell — whose synchronization context receives every
    /// publication a worker completes.
    /// </remarks>
    public CommandPaletteViewModel(IPaletteCommandSource source, Action<A11yEvent> announce,
        Func<CancellationToken, Task>? filterCountWindow = null,
        ICommandPaletteWorkLane? lane = null,
        Func<Command[], string, string[], string[], PaletteSection[]>? rank = null)
    {
        _source = source;
        _announce = announce;
        _filterCountWindow = filterCountWindow ?? (token => Task.Delay(FilterCountWindowMilliseconds, token));
        _lane = lane ?? new CommandPaletteWorkLane();
        _rank = rank ?? SlateUniffiMethods.PaletteSections;
        _uiContext = SynchronizationContext.Current;
        _ownerThreadId = Environment.CurrentManagedThreadId;
    }

    /// <summary>The open-time snapshot (contract P4): the command list and the
    /// recents, loaded together off the UI thread.</summary>
    private sealed record PaletteSnapshot(Command[] Commands, string[] Recents)
    {
        internal static PaletteSnapshot Empty { get; } = new([], []);
    }

    /// <summary>
    /// Raised before any availability check on an activation attempt so
    /// the host can put the caret back in the search field (contract P9
    /// step 1).
    /// </summary>
    public event EventHandler? SearchFocusRequested;

    /// <summary>Raised after the palette closes, for focus return.</summary>
    public event EventHandler? Dismissed;

    /// <summary>
    /// Sections in the order core returned them. <b>Never sorted</b> —
    /// <c>SECTION_ORDER</c> is not the <see cref="CommandSection"/> enum
    /// order (contract P1). The same instance is returned until the
    /// query changes (contract P18).
    /// </summary>
    public IReadOnlyList<CommandPaletteSectionViewModel> Sections => _sections;

    /// <summary>
    /// The one flat selection cycle over every row of every section.
    /// Section headers are not stops (contract P7).
    /// </summary>
    public IReadOnlyList<CommandPaletteRowViewModel> Rows => _rows;

    /// <summary>Rows matching the current query — the filter count.</summary>
    public int MatchCount => _rows.Length;

    public bool HasResults => _rows.Length > 0;

    /// <summary>
    /// The latest query change's step costs, for the view to finish the
    /// reading with its own rebuild (#1254). Non-null only for the open and
    /// the first <see cref="TimedKeystrokes"/> keystrokes while
    /// SLATE_UIA_DIAGNOSTICS=1; otherwise no clock is read at all.
    /// </summary>
    internal CommandPaletteRecomputeTiming? LastRecomputeTiming { get; private set; }

    /// <summary>
    /// The latest request's ranking and publication hand-off — complete
    /// once its result is published, discarded or posted to the owning
    /// thread. For the facts that drive the lane deterministically.
    /// </summary>
    internal Task RankCompletion { get; private set; } = Task.CompletedTask;

    /// <summary>Whether a query's rows are still on their way: the list, the
    /// keys and the count still speak for the last published query.</summary>
    internal bool IsRankPending => _publishedGeneration != _rankGeneration;

    /// <summary>
    /// Rows moved by one Page Up / Page Down. Windows-only navigation
    /// per divergence PD-1; the host pushes its viewport size here.
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => SetField(ref _pageSize, Math.Max(1, value));
    }

    public string Query
    {
        get => _query;
        set
        {
            if (!SetField(ref _query, value ?? string.Empty))
            {
                return;
            }

            if (IsOpen)
            {
                RequestRank();
            }
            else
            {
                RaiseDerivedState();
            }
        }
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set
        {
            if (SetField(ref _isOpen, value))
            {
                RaiseDerivedState();
            }
        }
    }

    /// <summary>
    /// The selected row, or <see langword="null"/> on zero matches.
    /// Read-only by design: WPF's <c>ListBox</c> pushes
    /// <see langword="null"/> into a two-way <c>SelectedItem</c> binding
    /// whenever <c>ItemsSource</c> is replaced, which would destroy the
    /// selection this view model just preserved across a keystroke
    /// (contract P7). Bind one-way and route pointer selection through
    /// <see cref="Select(CommandPaletteRowViewModel)"/>.
    /// </summary>
    public CommandPaletteRowViewModel? SelectedRow => _selectedRow;

    public string? SelectedId => _selectedId;

    /// <summary>Empty-registry state (contract P14) — once the open's
    /// snapshot has landed, so a palette still loading never claims the
    /// registry is empty.</summary>
    public bool ShowsEmptyRegistry => IsOpen && _snapshotLoaded && _snapshot.Length == 0;

    /// <summary>No-matches state (contract P14), for the published query.</summary>
    public bool ShowsNoMatches => IsOpen && _snapshotLoaded && _snapshot.Length > 0 && _rows.Length == 0;

    /// <summary>The visible no-matches line, which quotes the query whose
    /// empty rows are on screen — the published one, not the one still
    /// ranking.</summary>
    public string NoMatchesDetail =>
        $"No command matches \"{_publishedQuery}\". Try fewer letters or a different word.";

    /// <summary>
    /// The accessible name for the no-matches state, which deliberately
    /// does <b>not</b> quote the query (contract P14) — screen readers
    /// announce the quotation marks.
    /// </summary>
    public string NoMatchesAccessibleName =>
        $"No command matches {_publishedQuery}. Try fewer letters or a different word.";

    /// <summary>
    /// Whether either empty state is showing. The view collapses the
    /// block when this is false — an empty container left on-screen at
    /// zero size fails the axe <c>BoundingRectangleNotNull</c> check.
    /// </summary>
    public bool ShowsEmptyState => ShowsEmptyRegistry || ShowsNoMatches;

    /// <summary>The showing empty state's headline.</summary>
    /// <remarks>
    /// The two states are mutually exclusive by construction —
    /// <see cref="ShowsNoMatches"/> requires a non-empty snapshot — so an
    /// empty registry reports "no commands" even with a query typed,
    /// which is the more accurate diagnosis of the two.
    /// </remarks>
    public string EmptyStateTitle =>
        ShowsEmptyRegistry ? EmptyRegistryTitle : NoMatchesTitle;

    /// <summary>The showing empty state's visible detail line.</summary>
    public string EmptyStateDetail =>
        ShowsEmptyRegistry ? EmptyRegistryDetail : NoMatchesDetail;

    /// <summary>
    /// The showing empty state's accessible name — the whole block reads
    /// as one stop, and the no-matches variant drops the quotation marks
    /// (contract P14).
    /// </summary>
    public string EmptyStateAccessibleName =>
        ShowsEmptyRegistry
            ? $"{EmptyRegistryTitle}. {EmptyRegistryDetail}"
            : NoMatchesAccessibleName;

    /// <summary>
    /// Open the palette. Refuses without a vault: announces
    /// <c>CommandPaletteNeedsVault</c> and leaves <see cref="IsOpen"/>
    /// <see langword="false"/>, because a set flag would auto-present an
    /// empty palette on the next vault open (contract P14). Opening
    /// while already open re-opens rather than toggles (divergence
    /// PD-2).
    /// </summary>
    public void Open()
    {
        if (!_source.IsVaultOpen)
        {
            _announce(new A11yEvent.CommandPaletteNeedsVault());
            return;
        }

        // Contract P4: the command snapshot and the recents list are
        // taken once, here, and ranked for the palette's whole
        // lifetime. A command invoked during this session does not
        // appear under Recent until the next open. Both are FFI and the
        // recents are disk, so they load on the lane (locked decision 05
        // §4, principle 2) and the first ranking waits for them; until
        // they land the palette shows neither rows nor an empty state.
        //
        // Guarded on !IsOpen because PD-2 makes the chord RE-OPEN rather
        // than toggle, so this method is re-entered while open — and an
        // unguarded snapshot made that one chord press re-read the
        // registry and re-read recents from disk, swapping the rows
        // underneath a palette the user is already looking at. The
        // comment above claimed a whole-lifetime snapshot; this is what
        // makes it true. Re-opening still clears the query and the
        // selection, which is the visible half of PD-2.
        if (!_isOpen)
        {
            _snapshotCancellation = new CancellationTokenSource();
            _snapshotLoaded = false;
            IPaletteCommandSource source = _source;
            _snapshotLoad = _lane.Run(
                () => new PaletteSnapshot(source.ListCommands(), source.LoadRecents()),
                _snapshotCancellation.Token);
        }

        _query = string.Empty;
        _selectedId = null;
        _selectedRow = null;
        _invokeWhenPublished = false;
        _queryChangesSinceOpen = 0;
        // Contract P10: the first selection change after open is
        // silent — the initial row is not announced before any user
        // action.
        _suppressSelectionAnnouncement = true;
        _isOpen = true;
        OnPropertyChanged(nameof(Query));
        OnPropertyChanged(nameof(SelectedId));
        OnPropertyChanged(nameof(SelectedRow));
        OnPropertyChanged(nameof(IsOpen));
        RequestRank();
    }

    /// <summary>
    /// Close the palette and drop the ranked rows. Rows are cleared
    /// while the overlay is still realised so UIA clients do not retain
    /// orphaned children (the Quick Open precedent).
    /// </summary>
    public void Dismiss()
    {
        if (!IsOpen)
        {
            return;
        }

        _sections = [];
        _rows = [];
        _selectedId = null;
        _selectedRow = null;
        LastRecomputeTiming = null;
        // Whatever is still loading or ranking belongs to a palette that is
        // gone: stop what has not started, and let a newer generation make
        // anything already running publish nothing.
        _snapshotCancellation?.Cancel();
        _snapshotCancellation = null;
        CancelRankRequest();
        _publishedGeneration = ++_rankGeneration;
        _invokeWhenPublished = false;
        // A count still in its window has nothing to say once the palette closes.
        CancelFilterCountWindow();
        _isOpen = false;
        OnPropertyChanged(nameof(SelectedId));
        OnPropertyChanged(nameof(SelectedRow));
        OnPropertyChanged(nameof(IsOpen));
        RaiseDerivedState();
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Down (<c>delta = 1</c>) / Up (<c>delta = -1</c>), wrapping.</summary>
    public void MoveSelection(int delta)
    {
        if (_rows.Length == 0 || delta == 0)
        {
            return;
        }

        int index = IndexOfSelection();
        if (index < 0)
        {
            // From no selection: Down lands on the first row, Up on the
            // last (contract P7).
            index = delta > 0 ? -1 : _rows.Length;
        }

        int next = (((index + delta) % _rows.Length) + _rows.Length) % _rows.Length;
        UserSelect(_rows[next].Id);
    }

    /// <summary>Home (divergence PD-1).</summary>
    public void SelectFirst()
    {
        if (_rows.Length > 0)
        {
            UserSelect(_rows[0].Id);
        }
    }

    /// <summary>End (divergence PD-1).</summary>
    public void SelectLast()
    {
        if (_rows.Length > 0)
        {
            UserSelect(_rows[^1].Id);
        }
    }

    /// <summary>
    /// Page Down (<c>delta = 1</c>) / Page Up (<c>delta = -1</c>),
    /// divergence PD-1. Clamped at the ends rather than wrapping —
    /// standard Windows list behaviour, and the reason these are not
    /// just <see cref="MoveSelection"/> with a bigger step.
    /// </summary>
    public void MovePage(int delta)
    {
        if (_rows.Length == 0 || delta == 0)
        {
            return;
        }

        int index = IndexOfSelection();
        if (index < 0)
        {
            UserSelect(delta > 0 ? _rows[0].Id : _rows[^1].Id);
            return;
        }

        int next = Math.Clamp(index + (delta * PageSize), 0, _rows.Length - 1);
        UserSelect(_rows[next].Id);
    }

    /// <summary>Pointer / explicit selection.</summary>
    public void Select(CommandPaletteRowViewModel row) => UserSelect(row.Id);

    /// <summary>
    /// Activate the selection. No selection is a no-op that does not
    /// even request focus — mac's pinned ordering resolves the command
    /// first.
    /// </summary>
    /// <remarks>
    /// While a newer query's rows are still ranking, the selection on
    /// screen belongs to the previous keystroke; running it would run a
    /// command the user typed past. The request waits for the rows it was
    /// typed for and runs against the selection they bring, unless the
    /// user moves the selection first.
    /// </remarks>
    public void InvokeSelected()
    {
        if (IsRankPending)
        {
            _invokeWhenPublished = true;
            return;
        }

        if (_selectedRow is CommandPaletteRowViewModel row)
        {
            Invoke(row);
        }
    }

    /// <summary>
    /// Contract P9, in order: (1) request focus restore, unconditionally
    /// and before any availability check; (2) a disabled reason
    /// announces verbatim and returns <b>without reaching the command
    /// source</b>; (3) invoke; (4) on success only, record the
    /// invocation, then dismiss. Every non-success outcome leaves the
    /// palette open while its announcement plays — which is why no part
    /// of this runs in a <c>finally</c>.
    /// </summary>
    public void Invoke(CommandPaletteRowViewModel row)
    {
        SearchFocusRequested?.Invoke(this, EventArgs.Empty);

        // Contract P8: re-evaluated here rather than trusted from the
        // render-time value carried on the row.
        if (_source.DisabledReason(row.Id) is string reason)
        {
            // Verbatim, no prefix: the row already displays this
            // sentence, and "Unavailable: {reason}" makes the AT say it
            // twice, differently (contract P10).
            _announce(new A11yEvent.PaletteCommandUnavailable(reason));
            return;
        }

        try
        {
            _source.Invoke(row.Id);
        }
        catch (CommandException.UnknownId unknown)
        {
            _announce(new A11yEvent.PaletteCommandNotFound(unknown.id));
            return;
        }
        catch (CommandException.ActionFailed failed)
        {
            // The command layer owns the availability vocabulary (contract
            // P10). Asking it — rather than comparing against a string
            // held here — is what keeps a bridge-side rejection from being
            // announced as "{label} failed: {rejection}".
            _announce(_source.IsAvailabilityRejection(failed.message)
                ? new A11yEvent.PaletteCommandUnavailable(failed.message)
                : new A11yEvent.PaletteCommandFailed(row.Label, failed.message));
            return;
        }
        catch (CommandException)
        {
            // Defensive: CommandError's declared variants are the two
            // above. A future variant announces the generic failure
            // rather than escaping into the dispatcher.
            _announce(new A11yEvent.PaletteCommandFailed(row.Label, null));
            return;
        }

        // Handed to the lane, not run here: the recents transition is FFI
        // and the write is disk (locked decision 05 §4, principle 2). The
        // lane's order still puts it before the next open's recents load.
        RecordOnLane(row.Id);
        Dismiss();
    }

    /// <summary>
    /// Byte → UTF-16 conversion, the only one in the host (PINV-6).
    /// <see cref="MatchSpan"/> carries half-open UTF-8 byte offsets into
    /// the label; C# strings and WPF <c>Run</c>s index UTF-16 code
    /// units. Core only ever emits grapheme-aligned offsets; an interior
    /// one would be a core defect, and this clamps it <i>outward</i> to
    /// the containing rune so the fallout is a slightly wide bold run
    /// rather than a split surrogate pair or a silently erased match.
    /// Degenerate and past-the-end spans drop.
    /// </summary>
    internal static IReadOnlyList<CommandPaletteMatchRun> ToMatchRuns(
        string label,
        IReadOnlyList<MatchSpan> spans)
    {
        if (label.Length == 0 || spans.Count == 0)
        {
            return [];
        }

        MatchSpan[] ordered = [.. spans
            .Where(span => span.EndByte > span.StartByte)
            .OrderBy(span => span.StartByte)];
        if (ordered.Length == 0)
        {
            return [];
        }

        var runs = new List<CommandPaletteMatchRun>(ordered.Length);
        int spanIndex = 0;
        long byteCursor = 0;
        long runeEndByte = 0;
        int charCursor = 0;
        int openStart = -1;

        // Boundary events are settled at the START of each rune, where
        // the byte and char cursors are both on a boundary. Clamping is
        // outward on both ends — a start anywhere inside the current
        // rune opens at its first code unit, and an end anywhere inside
        // it closes past its last — so an interior offset can neither
        // split the rune nor silently erase the whole match.
        void Settle()
        {
            while (spanIndex < ordered.Length)
            {
                if (openStart < 0)
                {
                    if (ordered[spanIndex].StartByte >= runeEndByte)
                    {
                        return;
                    }

                    openStart = charCursor;
                }

                if (ordered[spanIndex].EndByte > byteCursor)
                {
                    return;
                }

                if (charCursor > openStart)
                {
                    runs.Add(new CommandPaletteMatchRun(openStart, charCursor - openStart));
                }

                openStart = -1;
                spanIndex++;
            }
        }

        foreach (Rune rune in label.EnumerateRunes())
        {
            runeEndByte = byteCursor + rune.Utf8SequenceLength;
            Settle();
            byteCursor = runeEndByte;
            charCursor += rune.Utf16SequenceLength;
        }

        Settle();
        if (openStart >= 0 && charCursor > openStart)
        {
            runs.Add(new CommandPaletteMatchRun(openStart, charCursor - openStart));
        }

        return runs;
    }

    /// <summary>
    /// Split the label into bold / non-bold stretches. Pure function of
    /// the already-converted runs — it never touches a byte offset, so
    /// PINV-6's "exactly one helper" survives.
    /// </summary>
    internal static IReadOnlyList<CommandPaletteLabelSegment> ToLabelSegments(
        string label,
        IReadOnlyList<CommandPaletteMatchRun> runs)
    {
        if (label.Length == 0)
        {
            return [];
        }

        if (runs.Count == 0)
        {
            return [new CommandPaletteLabelSegment(label, false)];
        }

        var segments = new List<CommandPaletteLabelSegment>((runs.Count * 2) + 1);
        int cursor = 0;
        foreach (CommandPaletteMatchRun run in runs)
        {
            if (run.Start > cursor)
            {
                segments.Add(new CommandPaletteLabelSegment(label[cursor..run.Start], false));
            }

            segments.Add(new CommandPaletteLabelSegment(
                label.Substring(run.Start, run.Length),
                true));
            cursor = run.Start + run.Length;
        }

        if (cursor < label.Length)
        {
            segments.Add(new CommandPaletteLabelSegment(label[cursor..], false));
        }

        return segments;
    }

    /// <summary>
    /// One query change: a request for its rows, tagged with the next
    /// generation. Nothing the list shows, the keys navigate or the reader
    /// hears changes here — that all waits for <see cref="Publish"/>.
    /// </summary>
    private void RequestRank()
    {
        int generation = ++_rankGeneration;
        CancelRankRequest();
        var request = new CancellationTokenSource();
        _rankCancellation = request;

        // Contract P10 (amended): every keystroke reopens the count's
        // window. The count itself is scheduled when its rows publish, so
        // a superseded query never has one to say.
        CancelFilterCountWindow();

        // The #1254 profile: read the clock only while diagnostics are on,
        // and only for the open and the first keystrokes after it.
        int? timedChange = _queryChangesSinceOpen <= TimedKeystrokes
            && HostLog.UiAutomationDiagnosticsEnabled
                ? _queryChangesSinceOpen
                : null;
        if (_queryChangesSinceOpen <= TimedKeystrokes)
        {
            _queryChangesSinceOpen++;
        }

        RankCompletion = RankAndPublishAsync(
            generation,
            _query,
            _source.SidebarPinnedOrder,
            _snapshotLoad,
            request.Token,
            timedChange);
    }

    /// <summary>
    /// Waits for the open's snapshot, ranks on the lane, and hands the
    /// result to the owning thread. Everything read from the view model
    /// was captured at request; nothing here writes to it.
    /// </summary>
    private async Task RankAndPublishAsync(
        int generation,
        string query,
        string[] sidebarPinnedOrder,
        Task<PaletteSnapshot> snapshotLoad,
        CancellationToken cancellation,
        int? timedChange)
    {
        long requested = timedChange is null ? 0 : Stopwatch.GetTimestamp();
        long rankTicks = 0;
        PaletteSnapshot snapshot;
        PaletteSection[] sections;
        try
        {
            snapshot = await snapshotLoad.ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            sections = await _lane.Run(
                () =>
                {
                    long started = timedChange is null ? 0 : Stopwatch.GetTimestamp();
                    PaletteSection[] ranked = _rank(
                        snapshot.Commands,
                        query,
                        snapshot.Recents,
                        sidebarPinnedOrder);
                    if (timedChange is not null)
                    {
                        rankTicks = Stopwatch.GetTimestamp() - started;
                    }

                    return ranked;
                },
                cancellation).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            // A load or rank that throws publishes nothing: the rows on
            // screen stay the last query's, and the log names the failure
            // by type only (W1-RT-01).
            HostLog.Write(HostDiagnosticEvent.PaletteWorkFailed, exception);
            return;
        }

        OnOwnerThread(() => Publish(
            generation,
            query,
            snapshot,
            sections,
            timedChange is int change ? (change, requested, rankTicks) : null));
    }

    /// <summary>
    /// Runs <paramref name="publish"/> on the thread that owns the palette:
    /// at once when a synchronous lane finished there, otherwise through the
    /// owner's synchronization context.
    /// </summary>
    private void OnOwnerThread(Action publish)
    {
        if (Environment.CurrentManagedThreadId == _ownerThreadId)
        {
            publish();
        }
        else if (_uiContext is not null)
        {
            _uiContext.Post(_ => publish(), null);
        }
        else
        {
            // A palette built without an owner context has no thread to
            // publish on; dropping the result is the only safe answer.
            HostLog.Write(HostDiagnosticEvent.PaletteWorkFailed);
        }
    }

    /// <summary>
    /// Contract P18: one <c>palette_sections</c> result per query change,
    /// stored, and read from that field by rendering, navigation, and the
    /// count — published only while it is still the latest request's.
    /// </summary>
    /// <remarks>
    /// Everything that decides what the reader hears is applied here, at
    /// publication, never at request (R-11): the P7 rule reads the row the
    /// user is on NOW — they may have moved while the rank ran — and the
    /// P10 count names the query these rows answer. A superseded
    /// generation, or a palette that closed meanwhile, publishes nothing.
    /// </remarks>
    private void Publish(
        int generation,
        string query,
        PaletteSnapshot snapshot,
        PaletteSection[] computed,
        (int QueryChange, long Requested, long RankTicks)? timed)
    {
        if (generation != _rankGeneration || !_isOpen)
        {
            return;
        }

        _publishedGeneration = generation;
        _snapshot = snapshot.Commands;
        _snapshotLoaded = true;
        _publishedQuery = query;
        string? previousId = _selectedId;
        long availabilityTicks = 0;
        long building = timed is null ? 0 : Stopwatch.GetTimestamp();

        var sections = new List<CommandPaletteSectionViewModel>(computed.Length);
        var rows = new List<CommandPaletteRowViewModel>();
        // Rendered in the order returned. Sorting by CommandSection's
        // enum value silently produces the wrong layout (contract P1).
        foreach (PaletteSection section in computed)
        {
            var sectionRows = new List<CommandPaletteRowViewModel>(section.Rows.Length);
            foreach (PaletteRow row in section.Rows)
            {
                IReadOnlyList<CommandPaletteMatchRun> matchRuns =
                    ToMatchRuns(row.Command.Label, row.LabelMatchSpans);
                // Dispatcher-affine (the resolver reads live ICommand
                // state), so it is asked here, on the owning thread.
                long asked = timed is null ? 0 : Stopwatch.GetTimestamp();
                string? disabledReason = _source.DisabledReason(row.Command.Id);
                if (timed is not null)
                {
                    availabilityTicks += Stopwatch.GetTimestamp() - asked;
                }

                var built = new CommandPaletteRowViewModel(
                    row.Command,
                    section.Title,
                    matchRuns,
                    ToLabelSegments(row.Command.Label, matchRuns),
                    row.Score,
                    disabledReason);
                sectionRows.Add(built);
                rows.Add(built);
            }

            sections.Add(new CommandPaletteSectionViewModel(
                section.Title,
                section.Kind,
                sectionRows));
        }

        _sections = sections;
        _rows = [.. rows];
        LastRecomputeTiming = timed is (int change, long requested, long rankTicks)
            ? new CommandPaletteRecomputeTiming(
                change,
                _rows.Length,
                requested,
                rankTicks,
                availabilityTicks,
                Stopwatch.GetTimestamp() - building - availabilityTicks)
            : null;
        RaiseDerivedState();

        // Contract P7: preserve the selection when its id survived,
        // snap to the first row when it vanished or was null, and go
        // null on zero matches.
        string? nextId = _rows.Length == 0
            ? null
            : previousId is not null && Array.Exists(_rows, row => row.Id == previousId)
                ? previousId
                : _rows[0].Id;
        SetSelection(nextId);

        // Contract P10 (amended): the latest non-empty query speaks once
        // after a trailing window — every keystroke reopened it at
        // request, so a typed query is one count, not a trail queued under
        // Medium=All (D-1). Suppressed entirely on an empty query.
        if (query.Length > 0)
        {
            ScheduleFilterCount(new A11yEvent.PaletteFilterCount((uint)_rows.Length, query));
        }

        if (_invokeWhenPublished)
        {
            _invokeWhenPublished = false;
            InvokeSelected();
        }
    }

    /// <summary>A selection the user makes: it withdraws an Enter still
    /// waiting for its rows — the user has chosen again.</summary>
    private void UserSelect(string? id)
    {
        _invokeWhenPublished = false;
        SetSelection(id);
    }

    /// <summary>Hands a successful invocation's recents transition to the
    /// lane, behind any load still reading the file.</summary>
    private void RecordOnLane(string commandId)
    {
        IPaletteCommandSource source = _source;
        _ = _lane.Run(
            () =>
            {
                try
                {
                    source.RecordInvocation(commandId);
                }
                catch (Exception exception)
                {
                    // Recents are a convenience; a failed transition must
                    // not escape the lane unobserved or reach the user.
                    HostLog.Write(HostDiagnosticEvent.PaletteWorkFailed, exception);
                }

                return true;
            },
            CancellationToken.None);
    }

    private void CancelRankRequest()
    {
        _rankCancellation?.Cancel();
        _rankCancellation = null;
    }

    /// <summary>The pending filter count's window and its posting, for the
    /// facts that drive the window deterministically (the search overlay's
    /// <c>SearchCompletion</c> shape).</summary>
    internal Task FilterCountCompletion => _filterCountCompletion ?? Task.CompletedTask;

    private void ScheduleFilterCount(A11yEvent count)
    {
        var window = new CancellationTokenSource();
        _filterCountCancellation = window;
        _filterCountCompletion = SpeakAfterWindowAsync(count, window.Token);
    }

    /// <summary>The search overlay's shape: wait out the window, then post
    /// back to the owner context; a window a later keystroke cancelled, or
    /// a palette that closed meanwhile, says nothing.</summary>
    private async Task SpeakAfterWindowAsync(A11yEvent count, CancellationToken window)
    {
        try
        {
            await _filterCountWindow(window).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (window.IsCancellationRequested)
        {
            return;
        }
        if (_uiContext is null)
        {
            Speak();
        }
        else
        {
            _uiContext.Post(_ => Speak(), null);
        }

        void Speak()
        {
            if (!window.IsCancellationRequested && IsOpen)
            {
                _announce(count);
            }
        }
    }

    private void CancelFilterCountWindow()
    {
        _filterCountCancellation?.Cancel();
        _filterCountCancellation?.Dispose();
        _filterCountCancellation = null;
    }

    private void SetSelection(string? id)
    {
        // An id with no row in the current set normalizes to no
        // selection, so SelectedId is non-null exactly when SelectedRow
        // is. Reachable when the host activates a stale row captured
        // from an earlier keystroke's Sections.
        CommandPaletteRowViewModel? row = id is null
            ? null
            : Array.Find(_rows, candidate => candidate.Id == id);
        string? nextId = row?.Id;
        bool changed = !string.Equals(_selectedId, nextId, StringComparison.Ordinal);
        _selectedId = nextId;

        if (!ReferenceEquals(_selectedRow, row))
        {
            _selectedRow = row;
            OnPropertyChanged(nameof(SelectedRow));
        }

        if (!changed)
        {
            // Disarm even when nothing changed, so an open that lands on
            // no selection cannot leave the flag armed.
            //
            // DEFENSIVE ONLY — deliberately ungated. The state is
            // unreachable today: Open() re-arms the flag every time, and
            // P4 freezes the snapshot, so a palette that opens with zero
            // rows can never gain one within that session. A test here
            // could not discriminate, and writing one that passes either
            // way would be worse than none.
            _suppressSelectionAnnouncement = false;
            return;
        }

        OnPropertyChanged(nameof(SelectedId));

        // Contract P10: the first selection change after open is
        // suppressed, whatever it is — including one that lands on no
        // selection at all.
        if (_suppressSelectionAnnouncement)
        {
            _suppressSelectionAnnouncement = false;
            return;
        }

        if (row is null)
        {
            return;
        }

        // The same availability resolver that built the row, re-asked
        // so the announcement cannot disagree with the Enter gate
        // (contract P8).
        _announce(new A11yEvent.PaletteCommandSelected(row.Label, _source.DisabledReason(row.Id)));
    }

    private int IndexOfSelection() =>
        _selectedId is null ? -1 : Array.FindIndex(_rows, row => row.Id == _selectedId);

    private void RaiseDerivedState()
    {
        OnPropertyChanged(nameof(Sections));
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(MatchCount));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowsEmptyRegistry));
        OnPropertyChanged(nameof(ShowsNoMatches));
        OnPropertyChanged(nameof(NoMatchesDetail));
        OnPropertyChanged(nameof(NoMatchesAccessibleName));
        // The unified accessors the view binds are derived from the four
        // above; a bare setter that forgets them leaves the empty-state
        // block stale on screen (the recorded bare-setter class).
        OnPropertyChanged(nameof(ShowsEmptyState));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDetail));
        OnPropertyChanged(nameof(EmptyStateAccessibleName));
    }
}
