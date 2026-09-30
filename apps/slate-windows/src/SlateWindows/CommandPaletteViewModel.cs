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
/// only while it is still the latest: rows, sections and selection move
/// together at publication, never at request, so what the list shows,
/// what the keys navigate and what Enter runs are always one query's
/// (P18). The P7 selection rule is applied at publication too — it reads
/// the row the user is on when the rows land — and the P10 count names
/// the published query, so R-11's one selection per query change holds
/// across the asynchronous hop. A superseded result is discarded unseen;
/// a superseded request still waiting its turn never runs. The contract
/// 28 #1275 record carries the whole state machine.
/// </para>
/// <para>
/// <b>The keys act on what is published</b> (P7). Enter runs the
/// selection on screen, at once, even while a newer query's rows are on
/// their way — the list the user sees is the list Enter acts on — and is a
/// no-op while nothing is published. A rank that fails resolves its
/// generation without touching the list, so nothing waits on it. The
/// P10 count's trailing window opens at the keystroke; the count speaks
/// once that window has elapsed AND that query's rows have published.
/// </para>
/// <para>
/// <b>The palette is sealed while a command it runs is running, or while
/// any modal loop runs over the shell</b> (#1275 codex rounds 3 and 4). A
/// command can pump nested dispatcher frames before it returns — the folder
/// picker, the unsaved-changes prompt — and so can a prompt the palette did
/// not start, such as the one closing the app raises. While either cause
/// holds, the palette is sealed: the pending rank's token is cancelled and
/// the generation advanced, so nothing in flight can publish; the count
/// window is cancelled; selection moves, Enter and a re-open are refused;
/// and a query typed meanwhile is kept for later. The published list stays
/// on screen, but nothing the palette owns publishes, changes or speaks.
/// When the last cause ends the seal lifts — a successful command dismisses
/// instead — and if the seal took a pending rank or an unspoken count, or
/// the query changed, the current query ranks again so the list and the
/// count come back consistent. The shell reports modal loops through
/// <see cref="SetModalLoop"/> (<see cref="ShellModalLoopMonitor"/>).
/// </para>
/// <para>
/// <b>A successful invocation retires the surface in the same dispatcher
/// turn</b> (P9 step 4 across the lane). The recents transition is handed
/// to the lane first — committing its place ahead of every later reader
/// of recents and of teardown — and the palette then dismisses at once,
/// so a surface the command opened is the one open modal when the turn
/// ends and takes focus without the palette above it. The write itself
/// lands afterwards (<see cref="RecordCompletion"/>); a failed or
/// unpersisted write is logged. <see cref="Shutdown"/> is the teardown:
/// nothing in flight publishes or announces afterwards, and the lane —
/// a pending recents write included — is quiet before the command source
/// goes.
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
    private readonly Action<HostDiagnosticEvent, Exception?> _diagnostics;
    private CountWindow? _countWindow;
    private Task? _filterCountCompletion;

    private Command[] _snapshot = [];
    private bool _snapshotLoaded;
    private Task<PaletteSnapshot> _snapshotLoad = Task.FromResult(PaletteSnapshot.Empty);
    private CancellationTokenSource? _snapshotCancellation;
    private CancellationTokenSource? _rankCancellation;
    private int _rankGeneration;
    private int _publishedGeneration;
    private bool _isShutDown;
    private bool _isInvoking;
    private bool _inModalLoop;
    private bool _resumeRanking;
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
    /// <param name="diagnostics">Where lane failures are reported —
    /// <see cref="HostLog.Write"/> unless a fact listens. Called from the
    /// lane's worker as well as the owning thread.</param>
    /// <remarks>
    /// Constructed on the thread that owns the palette — the dispatcher's
    /// in the shell — whose synchronization context receives every
    /// publication a worker completes.
    /// </remarks>
    public CommandPaletteViewModel(IPaletteCommandSource source, Action<A11yEvent> announce,
        Func<CancellationToken, Task>? filterCountWindow = null,
        ICommandPaletteWorkLane? lane = null,
        Func<Command[], string, string[], string[], PaletteSection[]>? rank = null,
        Action<HostDiagnosticEvent, Exception?>? diagnostics = null)
    {
        _source = source;
        _announce = announce;
        _filterCountWindow = filterCountWindow ?? (token => Task.Delay(FilterCountWindowMilliseconds, token));
        _lane = lane ?? new CommandPaletteWorkLane();
        _rank = rank ?? SlateUniffiMethods.PaletteSections;
        _diagnostics = diagnostics ?? HostLog.Write;
        _uiContext = SynchronizationContext.Current;
        _countDispatcher = _uiContext is System.Windows.Threading.DispatcherSynchronizationContext
            ? System.Windows.Threading.Dispatcher.CurrentDispatcher
            : null;
        _ownerThreadId = Environment.CurrentManagedThreadId;
    }

    /// <summary>P10 (amended at the W7-7 wave close): the dispatcher a count is
    /// spoken on — at Background priority, below input, so the count is
    /// spoken only after all queued input has been dispatched. Null without a
    /// dispatcher context (the facts' synchronous or custom contexts).</summary>
    private readonly System.Windows.Threading.Dispatcher? _countDispatcher;

    /// <summary>The open-time snapshot (contract P4): the command list and the
    /// recents, loaded together off the UI thread.</summary>
    private sealed record PaletteSnapshot(Command[] Commands, string[] Recents)
    {
        internal static PaletteSnapshot Empty { get; } = new([], []);
    }

    /// <summary>
    /// P10's trailing window for one query change, opened at the keystroke:
    /// <paramref name="Elapsed"/> completes when the window has run out, and
    /// the count for <paramref name="Generation"/> speaks only once that has
    /// happened AND that generation's rows have published. A newer
    /// keystroke, a dismissal or a failed rank cancels it.
    /// </summary>
    private sealed record CountWindow(
        int Generation,
        Task Elapsed,
        CancellationTokenSource Source,
        CancellationToken Token);

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
    /// keys and the count still speak for the last published query. A rank
    /// that fails resolves too, so this never stays true for a query that
    /// will not publish.</summary>
    internal bool IsRankPending => _publishedGeneration != _rankGeneration;

    /// <summary>
    /// The latest successful invocation's recents write — complete once it
    /// has landed, or failed and been logged. The palette has already
    /// dismissed by then (P9 step 4 across the lane); the lane's order is
    /// what puts the write ahead of the next open's recents read and of
    /// teardown. Never faults.
    /// </summary>
    internal Task RecordCompletion { get; private set; } = Task.CompletedTask;

    /// <summary>Whether <see cref="Shutdown"/> has run: the palette
    /// refuses to open again.</summary>
    internal bool IsShutDown => _isShutDown;

    /// <summary>Whether a command the palette invoked is still running —
    /// the palette is sealed until it returns, however it returns.</summary>
    internal bool IsInvoking => _isInvoking;

    /// <summary>Whether the shell has reported a modal loop running over it
    /// (<see cref="SetModalLoop"/>).</summary>
    internal bool IsInModalLoop => _inModalLoop;

    /// <summary>Whether the palette is sealed — by a command it is running,
    /// by a modal loop over the shell, or both.</summary>
    internal bool IsSealed => _isInvoking || _inModalLoop;

    /// <summary>How long teardown waits for the lane's in-flight item — one
    /// bounded native call or recents write — before it lets go.</summary>
    internal static TimeSpan ShutdownDrainBudget { get; } = TimeSpan.FromSeconds(5);

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

            if (IsOpen && IsSealed)
            {
                // A command or a modal loop owns the moment: the text is
                // kept, and its ranking waits for the seal to lift — then it
                // ranks, unless a successful command dismissed the palette.
                _resumeRanking = true;
            }
            else if (IsOpen)
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
        // A sealed palette neither opens nor re-opens: a re-open (PD-2)
        // would clear the list under a running command or a modal loop and
        // start ranking inside it, and an open after a running command
        // dismissed the palette would be torn down again by that command's
        // own success.
        if (_isShutDown || IsSealed)
        {
            return;
        }

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
        CancelSnapshotLoad();
        CancelRankRequest();
        _publishedGeneration = ++_rankGeneration;
        // A count still in its window has nothing to say once the palette closes.
        CancelFilterCountWindow();
        _isOpen = false;
        OnPropertyChanged(nameof(SelectedId));
        OnPropertyChanged(nameof(SelectedRow));
        OnPropertyChanged(nameof(IsOpen));
        RaiseDerivedState();
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Teardown (#1275): closes the palette, makes every request in flight
    /// stale, cancels and releases its tokens, and waits — bounded by
    /// <paramref name="drainBudget"/> — for the lane to go quiet, so a
    /// command load or a recents write still running or queued finishes
    /// before the caller disposes the command source. Nothing that
    /// completes afterwards publishes or announces, and the palette
    /// refuses to open again.
    /// </summary>
    /// <remarks>
    /// The wait is on the owning thread, but it waits on nothing that needs
    /// that thread: the lane runs pure FFI and file work, never a
    /// dispatcher call. What it waits for is one bounded native call or one
    /// recents write — a superseded rank waiting its turn is cancelled and
    /// never runs.
    /// </remarks>
    /// <returns>Whether the lane went quiet inside the budget.</returns>
    internal bool Shutdown(TimeSpan drainBudget)
    {
        if (_isShutDown)
        {
            return true;
        }

        _isShutDown = true;
        Dismiss();
        CancelSnapshotLoad();
        CancelRankRequest();
        CancelFilterCountWindow();
        _publishedGeneration = ++_rankGeneration;

        bool quiet = _lane.WhenIdle().Wait(drainBudget);
        if (!quiet)
        {
            _diagnostics(HostDiagnosticEvent.PaletteWorkFailed, null);
        }

        return quiet;
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
    /// Acts on the PUBLISHED selection — the row on screen — at once,
    /// whether or not a newer query's rows are still ranking (contract P7:
    /// the keys operate the list the user sees). Nothing is deferred to a
    /// publication the user has not seen, so Enter can never run a row
    /// that was not on screen when it was pressed. With nothing published
    /// yet (the open's snapshot still loading) there is no selection, and
    /// Enter does nothing. While the palette is sealed — a command running, or
    /// a modal loop over the shell — it does nothing either: one command at a
    /// time, and none under a prompt.
    /// </remarks>
    public void InvokeSelected()
    {
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
    /// palette open while its announcement plays — which is why the only
    /// thing a <c>finally</c> does here is end the seal.
    /// </summary>
    /// <remarks>
    /// Step 3 runs sealed (#1275 codex round 3): the command may pump
    /// nested dispatcher frames before it returns, and nothing the palette
    /// owns may publish, change or speak inside them. Steps 1 and 2 cannot
    /// pump, so an unavailable row never seals.
    /// </remarks>
    public void Invoke(CommandPaletteRowViewModel row)
    {
        if (IsSealed)
        {
            // One command at a time, and none under a prompt: an Enter or a
            // double-click reaching a sealed palette runs nothing.
            return;
        }

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

        A11yEvent? failure = null;
        BeginInvocation();
        try
        {
            _source.Invoke(row.Id);
        }
        catch (CommandException.UnknownId unknown)
        {
            failure = new A11yEvent.PaletteCommandNotFound(unknown.id);
        }
        catch (CommandException.ActionFailed failed)
        {
            // The command layer owns the availability vocabulary (contract
            // P10). Asking it — rather than comparing against a string
            // held here — is what keeps a bridge-side rejection from being
            // announced as "{label} failed: {rejection}".
            failure = _source.IsAvailabilityRejection(failed.message)
                ? new A11yEvent.PaletteCommandUnavailable(failed.message)
                : new A11yEvent.PaletteCommandFailed(row.Label, failed.message);
        }
        catch (CommandException)
        {
            // Defensive: CommandError's declared variants are the two
            // above. A future variant announces the generic failure
            // rather than escaping into the dispatcher.
            failure = new A11yEvent.PaletteCommandFailed(row.Label, null);
        }
        finally
        {
            // The seal lasts exactly as long as the command: however it
            // returns — or throws something unexpected — it ends here.
            _isInvoking = false;
        }

        // The command's own action tore the shell down — a window close
        // runs Shutdown synchronously, and so can a prompt's nested loop.
        // Nothing speaks after teardown and nothing may join the lane after
        // its wait (I8): no announcement, no record.
        if (_isShutDown)
        {
            return;
        }

        if (failure is not null)
        {
            _announce(failure);

            // The invocation was the seal's last cause unless a modal loop
            // is still reported; then the seal lifts when that loop ends.
            if (!IsSealed)
            {
                ResumeIfOwed();
            }

            return;
        }

        // P9 step 4, in order: record, THEN dismiss. The transition is FFI
        // and the write is disk (locked decision 05 §4, principle 2), so it
        // is handed to the lane — which commits its place ahead of the next
        // open's recents read and of teardown — and only then does the
        // palette dismiss. The dismissal does NOT wait for the write: a
        // command that opened its own surface (Quick Open, Search) queued
        // that surface's focus during the invoke, and a palette left up
        // over it would be a second open modal hiding the focused field.
        // Retiring here, the moment the command returns, keeps the palette
        // from outliving the invocation it was sealed for.
        RecordCompletion = RecordOnLane(row.Id);
        Dismiss();
    }

    /// <summary>
    /// T8's seal cause, immediately before the command runs. Invoke refuses
    /// while sealed, so a command is always the seal's first cause.
    /// </summary>
    private void BeginInvocation()
    {
        _isInvoking = true;
        Seal();
    }

    /// <summary>
    /// T13/T14: the shell reports a modal loop beginning over it (a message
    /// box, a common dialog, a WPF <c>ShowDialog</c>) or the last one ending.
    /// The first cause seals; a second is already covered. When the last
    /// cause ends the seal lifts and what it took is ranked again; a command
    /// still running keeps it sealed until the command returns.
    /// </summary>
    internal void SetModalLoop(bool active)
    {
        if (active == _inModalLoop)
        {
            return;
        }

        if (active)
        {
            bool wasSealed = IsSealed;
            _inModalLoop = true;
            if (!wasSealed)
            {
                Seal();
            }

            return;
        }

        _inModalLoop = false;
        if (!IsSealed)
        {
            ResumeIfOwed();
        }
    }

    /// <summary>
    /// The seal, applied when its first cause arrives: remember whether it
    /// takes anything the user is still owed — rows still ranking, or a count
    /// not yet spoken — then cancel the pending rank's token and advance the
    /// generation (nothing in flight can publish or resolve), and cancel the
    /// count window. The published list stays on screen.
    /// </summary>
    private void Seal()
    {
        _resumeRanking = IsRankPending || _countWindow is not null;
        CancelRankRequest();
        _publishedGeneration = ++_rankGeneration;
        CancelFilterCountWindow();
    }

    /// <summary>
    /// The seal's last cause has ended with the palette still up (a failed
    /// command, or a modal loop closing): if the seal took a pending rank or
    /// an unspoken count, or the query changed meanwhile, rank the current
    /// query again (T2) so the list and its count come back consistent with
    /// what is typed. Otherwise both already are, and nothing more is said.
    /// </summary>
    private void ResumeIfOwed()
    {
        bool resume = _resumeRanking;
        _resumeRanking = false;
        if (resume && _isOpen && !_isShutDown)
        {
            RequestRank();
        }
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
        // window — HERE, at the keystroke, not when the rank returns, so a
        // slow rank cannot push the count later than the window says. The
        // count speaks when this window has elapsed AND this generation's
        // rows have published; a superseded query never has one to say,
        // and an empty query opens no window at all.
        CancelFilterCountWindow();
        if (_query.Length > 0)
        {
            OpenCountWindow(generation);
        }

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
        catch (Exception exception) when (
            exception is OperationCanceledException or ObjectDisposedException
            && cancellation.IsCancellationRequested)
        {
            // T5: THIS request was cancelled — superseded, dismissed, sealed
            // by an invocation or torn down — and a cancelled request's
            // token may already be released. Whoever cancelled it has moved
            // the generation on; nothing to do and nothing to log.
            return;
        }
        catch (Exception exception)
        {
            // T4: a load or rank that failed — including a cancellation-
            // shaped exception whose token was NOT cancelled, such as a
            // released native object. It publishes no rows, but the
            // generation RESOLVES on the owning thread so nothing waits on
            // a query that will never publish; whether it is still the
            // latest is decided there, and only then is it logged.
            OnOwnerThread(() => ResolveFailedRank(generation, exception));
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
            _diagnostics(HostDiagnosticEvent.PaletteWorkFailed, null);
        }
    }

    /// <summary>
    /// The failure state of a rank (or of the open's snapshot load): the
    /// latest generation is resolved — no longer pending — with the list,
    /// the selection and the no-matches copy left exactly as last
    /// published, which is what the keys act on. Its count has nothing to
    /// say. The failure is logged by type only (W1-RT-01). A superseded
    /// generation, or a palette closed, sealed or torn down meanwhile,
    /// changes nothing and is not logged (T5).
    /// </summary>
    private void ResolveFailedRank(int generation, Exception exception)
    {
        if (generation != _rankGeneration || !_isOpen || _isShutDown || IsSealed)
        {
            return;
        }

        _diagnostics(HostDiagnosticEvent.PaletteWorkFailed, exception);
        _publishedGeneration = generation;
        CancelFilterCountWindow();
    }

    /// <summary>
    /// Contract P18: one <c>palette_sections</c> result per query change,
    /// stored, and read from that field by rendering, navigation, and the
    /// count — published only while it is still the latest request's.
    /// </summary>
    /// <remarks>
    /// What the reader hears about these rows is decided here, at
    /// publication, never at request (R-11): the P7 rule reads the row the
    /// user is on NOW — they may have moved while the rank ran — and the
    /// P10 count names the query these rows answer, speaking once the
    /// window its keystroke opened has also run out. A superseded
    /// generation, or a palette that closed meanwhile, publishes nothing.
    /// </remarks>
    private void Publish(
        int generation,
        string query,
        PaletteSnapshot snapshot,
        PaletteSection[] computed,
        (int QueryChange, long Requested, long RankTicks)? timed)
    {
        if (generation != _rankGeneration || !_isOpen || _isShutDown || IsSealed)
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
        // after a trailing window — opened at THIS generation's keystroke
        // (RequestRank), so a typed query is one count, not a trail queued
        // under Medium=All (D-1), and it speaks when the later of the
        // window and this publication completes. Suppressed entirely on
        // an empty query, which opened no window.
        if (query.Length > 0 && _countWindow is CountWindow window && window.Generation == generation)
        {
            _filterCountCompletion = SpeakAfterWindowAsync(
                new A11yEvent.PaletteFilterCount((uint)_rows.Length, query),
                window);
        }
    }

    /// <summary>A selection the user makes on the published rows — refused
    /// while sealed, with the palette's own selection re-asserted so a list
    /// the pointer moved shows it again.</summary>
    private void UserSelect(string? id)
    {
        if (IsSealed)
        {
            OnPropertyChanged(nameof(SelectedRow));
            return;
        }

        SetSelection(id);
    }

    /// <summary>
    /// Hands a successful invocation's recents transition to the lane,
    /// behind any load still reading the file and ahead of the next open's
    /// read and of teardown. A write that throws, or returns without
    /// persisting (the store's normal IO and access failures), is logged
    /// here on the lane — recents are a convenience, so neither reaches
    /// the user. Never faults.
    /// </summary>
    private Task RecordOnLane(string commandId)
    {
        IPaletteCommandSource source = _source;
        Action<HostDiagnosticEvent, Exception?> diagnostics = _diagnostics;
        return _lane.Run(
            () =>
            {
                try
                {
                    if (!source.RecordInvocation(commandId))
                    {
                        diagnostics(HostDiagnosticEvent.PaletteWorkFailed, null);
                    }
                }
                catch (Exception exception)
                {
                    diagnostics(HostDiagnosticEvent.PaletteWorkFailed, exception);
                }

                return true;
            },
            CancellationToken.None);
    }

    private void CancelRankRequest()
    {
        _rankCancellation?.Cancel();
        _rankCancellation?.Dispose();
        _rankCancellation = null;
    }

    private void CancelSnapshotLoad()
    {
        _snapshotCancellation?.Cancel();
        _snapshotCancellation?.Dispose();
        _snapshotCancellation = null;
    }

    /// <summary>The pending filter count's window and its posting, for the
    /// facts that drive the window deterministically (the search overlay's
    /// <c>SearchCompletion</c> shape).</summary>
    internal Task FilterCountCompletion => _filterCountCompletion ?? Task.CompletedTask;

    /// <summary>Opens <paramref name="generation"/>'s trailing window at its
    /// keystroke. The window runs whether or not the rank has returned.</summary>
    private void OpenCountWindow(int generation)
    {
        var source = new CancellationTokenSource();
        CancellationToken token = source.Token;
        _countWindow = new CountWindow(generation, _filterCountWindow(token), source, token);
    }

    /// <summary>The search overlay's shape: wait out what is left of the
    /// window — nothing, when it elapsed before the rows published — then
    /// post back to the owner context; a window a later keystroke
    /// cancelled, or a palette that closed meanwhile, says nothing.
    /// P10 as amended at the W7-7 wave close: on the dispatcher the post is at
    /// Background priority, below input. A Normal post is dispatched ahead of
    /// OS input the user has already typed, so under a publication that
    /// settles past the window (about 200 ms under NVDA) the count of a query
    /// the user had already typed past was spoken; below input, a keystroke
    /// already typed reopens the window first, and an Escape or Enter already
    /// typed closes the palette first.</summary>
    private async Task SpeakAfterWindowAsync(A11yEvent count, CountWindow window)
    {
        try
        {
            await window.Elapsed.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (window.Token.IsCancellationRequested)
        {
            return;
        }
        if (_uiContext is null)
        {
            Speak();
        }
        else if (_countDispatcher is not null)
        {
            _ = _countDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, Speak);
        }
        else
        {
            _uiContext.Post(_ => Speak(), null);
        }

        void Speak()
        {
            if (!window.Token.IsCancellationRequested && IsOpen && !IsSealed)
            {
                _announce(count);

                // Spoken: nothing is owed for this window any more, which is
                // what an invocation's seal asks (BeginInvocation).
                if (ReferenceEquals(_countWindow, window))
                {
                    _countWindow = null;
                    window.Source.Dispose();
                }
            }
        }
    }

    private void CancelFilterCountWindow()
    {
        _countWindow?.Source.Cancel();
        _countWindow?.Source.Dispose();
        _countWindow = null;
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
