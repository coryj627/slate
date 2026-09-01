// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>The document's load posture (contract A3) — the mac
/// <c>CanvasDocument.LoadState</c> twin with mac's <c>.degraded</c>
/// renamed <see cref="ParseError"/>, because core's <c>degraded</c>
/// flag is the parse failure and the t0 §5 "unsupported items" banner
/// is the SkippedEntry count, and calling both degraded is what
/// CD-28 exists to stop.</summary>
internal enum CanvasLoadState
{
    /// <summary>Before the first publish.</summary>
    Loading,

    /// <summary>Loaded and navigable — possibly with entry-level
    /// warnings, which ride as the A4 banner, not as a state.</summary>
    Ready,

    /// <summary>The file could not be loaded as a canvas at all
    /// (<c>CanvasOpenInfo.degraded</c>). Read-only BY CONSTRUCTION: the
    /// handle is released immediately, so nothing can mutate it.</summary>
    ParseError,

    /// <summary>Filesystem/session failure — missing file, IO, UTF-8.</summary>
    Failed,

    /// <summary>A moved canvas could not be reopened at its new path
    /// (CD-32).</summary>
    RetargetAbsent,
}

/// <summary>What <see cref="CanvasDocumentViewModel.Activate"/> did, so
/// the surface can finish a view-level action (expanding a group) in
/// the view while the per-kind decision stays in one place
/// (contract A13).</summary>
internal enum CanvasActivation
{
    /// <summary>Nothing happened; a refusal was announced.</summary>
    Refused,

    /// <summary>A group row: the view expands it.</summary>
    ExpandGroup,

    /// <summary>A text card: the editor sheet was REQUESTED through
    /// the TE-8 seam (§E TE-11b) - the workspace opens it and the
    /// modal machinery owns focus, so the view does nothing.</summary>
    EditorRequested,

    /// <summary>A file/image card opened a note tab.</summary>
    Navigated,

    /// <summary>A link card was handed to the shell.</summary>
    Opened,
}

/// <summary>
/// A pending focus delivery (contract A14): who asked, which row, and
/// which request it is. A record so a surface can hand the exact
/// instance back when it delivers, and the document can tell a late
/// delivery of an old request from a live one.
/// </summary>
/// <remarks>
/// No generation counter: completion compares the RECORD, by reference.
/// The contract already is that a surface hands back the exact instance
/// it was given, so identity answers "is this the request that is still
/// pending" exactly — and a counter answers it only until it wraps or
/// repeats, which is a class of bug with no upside here.
/// </remarks>
internal sealed record CanvasFocusRequest(object Owner, string? NodeId);

/// <summary>
/// A pending request to put the reader in the filter field (contract
/// C10) — <see cref="CanvasFocusRequest"/>'s twin, and addressed for the
/// same reason: two panes share one document.
/// </summary>
internal sealed record CanvasFilterFocusRequest(object? Owner);

/// <summary>
/// What the surfaces are showing for the filter, and whether that answers
/// the needle now in the field — the mac <c>CanvasDocument.FilterView</c>
/// twin (contract C10).
/// </summary>
/// <remarks>
/// Every consumer reads this ONE value, so the rows on screen, the
/// summary's number and the announced count cannot come from three
/// different answers. The invariant is <i>displayed rows == announced
/// count</i>, always.
/// </remarks>
/// <param name="Rows">Exactly what the surfaces display.</param>
/// <param name="Narrowed">True when a filter narrowed
/// <paramref name="Rows"/> at all.</param>
/// <param name="Current">True when <paramref name="Rows"/> answer the
/// needle in the field AND the surface is showing them. False has two
/// causes now and one sentence: no handle could answer, or the document
/// is not rendering rows at all (a load, a failure), so no number
/// describes what is on screen. Either way the PREVIOUS answer stays up
/// and the caller says the state's own sentence rather than counting
/// these rows as if they matched what the user just typed.</param>
/// <param name="MatchedIds">The matched node ids when narrowed, so the
/// table projection filters core's table rows by the SAME answer the
/// outline shows; null when nothing is narrowed.</param>
internal sealed record CanvasFilterView(
    IReadOnlyList<CanvasOutlineRow> Rows,
    bool Narrowed,
    bool Current,
    IReadOnlySet<string>? MatchedIds);

/// <summary>The four-branch answer classification (task T6), computed
/// ONCE — the cleanup pass folded five hand-kept ladders into this,
/// so the spoken count, the visible summary and the view cannot
/// drift.</summary>
internal enum CanvasFilterVerdict
{
    /// <summary>No active needle: the filter is off.</summary>
    Inactive,

    /// <summary>The answer on screen is the field's needle's, on rows
    /// the surface renders.</summary>
    Current,

    /// <summary>An active needle whose answer has not landed — the
    /// completion's projection speaks when it does.</summary>
    InFlight,

    /// <summary>The match for the field's needle ran and could not
    /// answer, on a document that renders rows.</summary>
    Failed,

    /// <summary>Everything else: the state mapping owns the
    /// sentence.</summary>
    StateSentence,
}

/// <summary>
/// W6-1 PR A (#745): the per-path canvas document — the mac
/// <c>CanvasDocument</c> twin, built on the W4-6 <c>BaseDocument</c>
/// pattern. Owns the native handle, the load state, the published
/// projections and the shared <see cref="CanvasSelection"/>. One
/// instance per open vault-relative path, shared by every tab and pane
/// on that path (contract A1); the workspace registry owns creation and
/// shutdown.
///
/// Threading (contract A17), stated as it is since task T3: the LOAD
/// runs inside a <see cref="PanelWorkScheduler.StartWork"/> body and
/// publishes to the one slot from whatever thread it is on — the slot's
/// gate serializes it, and nothing marshals a publication. What is
/// marshalled through <c>Post</c> is the PROJECTION of the publication
/// onto this object's bindable properties, which reads the slot once at
/// dispatch and applies whatever is current. The per-node DETAIL reads
/// (<see cref="NeighborsOf"/>, <see cref="NodeTextOf"/>) go through the
/// lease the publication names, admitted under that lease's own lock,
/// which is what makes a handle replacement unable to race a read. The
/// handle is closed exactly once, by whoever un-named its lease: the
/// reload's worker, or the terminal publication's caller.
///
/// Canvas tabs are never dirty: mutations write through on commit
/// (PR E), so the U1 close gate is bypassed for canvas tabs
/// (contract A19).
/// </summary>
internal sealed class CanvasDocumentViewModel : PanelWorkScheduler
{
    private readonly VaultSession _session;
    private readonly string? _retargetedFrom;

    /// <summary>
    /// The one mutable currency authority (U1), and the pipeline that
    /// writes it. Task T3: the handle, the generation counter and the
    /// FFI lock this class used to own are the LEASE's now — reached
    /// only through the publication that names it — and the load state
    /// below is a PROJECTION of the publication, applied on the
    /// dispatcher by <see cref="ApplyPublication"/>.
    /// </summary>
    private readonly CanvasPublicationSlot _slot = new(CanvasPublication.Seed());
    private readonly CanvasLoadPipeline _pipeline;

    /// <summary>W6-1 §E TE-5b: the history domain (per-document,
    /// session-scoped), rebased by every ready publish and cleared
    /// with the document.</summary>
    internal CanvasUndoStack UndoStack { get; } = new();

    private readonly CanvasMutationGate _mutationGate = new();
    private readonly CanvasBusyGate _busyGate = new();

    /// <summary>W6-1 §E TE-5b: THE FUNNEL — every mutation verb on
    /// this document enters here (R-A).</summary>
    internal CanvasMutationFunnel Funnel { get; }
    private readonly CanvasFilterMachine _machine;
    private CanvasPublication? _applied;
    private bool _applying;
    private CanvasLoadState _state = CanvasLoadState.Loading;
    private string? _stateMessage;
    private IReadOnlyList<CanvasOutlineRow> _outline = [];
    private IReadOnlyList<CanvasTableRow> _tableRows = [];
    private IReadOnlyList<CanvasLoadWarning> _warnings = [];
    private bool _announcedDegradedLoad;
    private Task? _asyncClose;
    private string _filterText = string.Empty;
    private string? _whereAmIText;
    private CanvasFilterFocusRequest? _filterFocusRequest;

    /// <summary>Row lookup by node id — the outline is walked by id from
    /// selection, activation and the tree's own callbacks.</summary>
    private readonly Dictionary<string, CanvasOutlineRow> _rows =
        new(StringComparer.Ordinal);

    /// <summary>Per-node activation targets (file path / URL) and
    /// subpaths, derived once at load from core's table rows and scene
    /// — activation never re-queries (the mac <c>targets</c> shape).</summary>
    private readonly Dictionary<string, string> _targets = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, string> _subpaths =
        System.Collections.Immutable.ImmutableDictionary<string, string>.Empty;

    /// <summary>Per-node adjacency, fetched lazily on first selection
    /// and cached; dropped on every load (the mac
    /// <c>neighborsCache</c>). A refusal caches nothing, so the skew of
    /// contract A16 heals on the next ask.</summary>
    private readonly Dictionary<string, IReadOnlyList<CanvasNeighbor>> _neighbors =
        new(StringComparer.Ordinal);

    public CanvasDocumentViewModel(
        VaultSession session,
        string path,
        CanvasAnnouncer announcer,
        bool synchronousForTests = false,
        string? retargetedFrom = null,
        Func<CanvasVerbosity>? verbosity = null)
        : base(synchronousForTests)
    {
        _session = session;
        Path = path;
        _announcer = announcer;
        _retargetedFrom = retargetedFrom;
        _verbosity = verbosity ?? (static () => CanvasVerbosity.Standard);
        _machine = new CanvasFilterMachine(
            _slot,
            new FilterSource(this),
            // The tracked worker, with the projection posted after every
            // job — inline in synchronous tests, so a keystroke's answer
            // is applied before the setter returns, exactly as the
            // synchronous filter behaved (task T6).
            run: body => StartWork(() =>
            {
                try
                {
                    body();
                }
                finally
                {
                    Post(ApplyPublication);
                }
            }));
        _pipeline = new CanvasLoadPipeline(
            _slot,
            new SessionSource(this),
            // U9's load column, second half: the reseeded request the
            // acceptance minted goes to the machine for its job.
            onReseeded: (request, needle) => _machine.StartReseeded(request, needle));
        // The UI-originated intent publications — U11's fourth writer
        // family. The shared selection stays the surfaces' authority
        // for what they SHOW; what it publishes here is the INTENT the
        // rebase resolves against the next population, so a selection
        // made while a load is in flight survives that load's
        // acceptance (task T3).
        Funnel = new CanvasMutationFunnel(
            _slot,
            _mutationGate,
            UndoStack,
            _busyGate,
            new MutationSource(this),
            _pipeline,
            // The same tracked worker the filter rides, with the
            // projection posted after every job — inline in synchronous
            // tests, so a verb's whole transaction lands before the
            // caller returns (§E TE-5b).
            run: body => StartWork(() =>
            {
                try
                {
                    body();
                }
                finally
                {
                    Post(ApplyPublication);
                }
            }),
            announce: Speak);
        Selection.PropertyChanged += (_, changed) => PublishIntent(changed.PropertyName);
        Modes = new CanvasModeController(Speak);
        Navigator = new CanvasNavigator(this);
    }

    /// <summary>Vault-relative path — the registry's identity, compared
    /// byte-exact (Ordinal) everywhere. Immutable: a rename re-keys the
    /// registry rather than mutating this (CD-32).</summary>
    public string Path { get; }

    public string DisplayName => System.IO.Path.GetFileNameWithoutExtension(Path);

    /// <summary>
    /// The one funnel every canvas sentence goes through (contract A5),
    /// and PRIVATE so that "goes through the boundary" is a fact about
    /// the type rather than a convention a census has to police.
    /// </summary>
    /// <remarks>
    /// Production reaches it through exactly two named members —
    /// <see cref="Speak"/> and <see cref="GridRelaySeam"/> — so
    /// `document.Announcer.Announce(…)`, the shape this branch fixed
    /// twice, no longer compiles. What accessibility CANNOT separate is
    /// the test surface: the shell and its tests share an assembly
    /// boundary via `InternalsVisibleTo`, so
    /// <see cref="AnnouncerForTests"/> is reachable from production too.
    /// That residue is named rather than hidden, and the census is the
    /// second wall over it.
    /// </remarks>
    private readonly CanvasAnnouncer _announcer;

    /// <summary>
    /// The B7 relay seam: the substrate's CANONICAL grid events (sort,
    /// row move, cell move) ride the canvas funnel uncoalesced, carrying
    /// core's own priority through unwrapped.
    /// </summary>
    /// <remarks>
    /// Named and narrow on purpose. It is the one production path that
    /// is not a canvas sentence — the grid composes its own, and
    /// re-classifying them here would be host prose — so it gets a
    /// member of its own instead of a hole in the boundary.
    /// </remarks>
    internal Action<A11yEvent> GridRelaySeam => _announcer.Relay;

    /// <summary>
    /// The funnel itself, for facts that drive it deliberately — the
    /// coalescer flush, the refusal counter, and the misuse a guard
    /// exists for.
    /// </summary>
    /// <remarks>
    /// The NAME is the guard: `internal` cannot separate this assembly's
    /// tests from its production code, so what stops production using
    /// it is that a reviewer reading `AnnouncerForTests` in a shipping
    /// file knows immediately, and the announce census fails on it by
    /// name. Recorded as the residue of an otherwise structural fix.
    /// </remarks>
    internal CanvasAnnouncer AnnouncerForTests => _announcer;

    /// <summary>Shared selection + marks for every pane showing this
    /// canvas (contract A1/R-B).</summary>
    public CanvasSelection Selection { get; } = new();

    private readonly Func<CanvasVerbosity> _verbosity;

    /// <summary>
    /// Contract A7/C13: a parameter at every announce site, READ LIVE.
    /// </summary>
    /// <remarks>
    /// A delegate rather than a value, because the preference is
    /// app-level and live-switchable (t0 §1.2) while this object is
    /// per-document: reading it at every announce is what makes a
    /// verbosity change take effect on the very next movement without
    /// anything pushing the new value into every open canvas. PR A's
    /// settable field is gone; no announce site changed, which is what
    /// A7 said would happen.
    /// </remarks>
    public CanvasVerbosity Verbosity => _verbosity();

    /// <summary>The per-document mode stack (t0 §2). One per document, so
    /// two panes on one canvas share the mode exactly as they share the
    /// selection (R-B).</summary>
    public CanvasModeController Modes { get; }

    /// <summary>The per-document command layer (contract C1) — not a
    /// fourth view: every projection hosts it.</summary>
    public CanvasNavigator Navigator { get; }

    public CanvasLoadState State
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

    /// <summary>The failure/absent message. Null in Loading and Ready —
    /// a ready canvas with skipped entries speaks through
    /// <see cref="DegradedBannerText"/>, not through this.</summary>
    public string? StateMessage
    {
        get => _stateMessage;
        private set => SetField(ref _stateMessage, value);
    }

    /// <summary>Core's rows, untransformed and in core's reading order
    /// (R-D).</summary>
    public IReadOnlyList<CanvasOutlineRow> Outline
    {
        get => _outline;
        private set => SetField(ref _outline, value);
    }

    /// <summary>Core's table rows, untransformed and in core's order
    /// (R-D) — the PR B projection's whole content. Published from the
    /// same load that publishes <see cref="Outline"/>, because they are
    /// two reads of one open (contract B4).</summary>
    public IReadOnlyList<CanvasTableRow> TableRows
    {
        get => _tableRows;
        private set => SetField(ref _tableRows, value);
    }

    /// <summary>Entry-level load warnings — the t0 §5 detail rows.</summary>
    public IReadOnlyList<CanvasLoadWarning> Warnings
    {
        get => _warnings;
        private set
        {
            if (SetField(ref _warnings, value))
            {
                NotifyStateChanged();
            }
        }
    }

    /// <summary>Skipped-but-preserved entries (t0 §5). The mac
    /// <c>preservedItemCount</c>: the SkippedEntry warnings, NOT
    /// <c>CanvasOpenInfo.degraded</c> — CD-28.</summary>
    public int PreservedItemCount => _preservedCount;

    private int _preservedCount;

    /// <summary>The t0 §5 banner, rendered from the SAME event the
    /// once-per-open announcement speaks (contract A4), so banner and
    /// speech cannot drift. Null when nothing was skipped.</summary>
    public string? DegradedBannerText =>
        State == CanvasLoadState.Ready && PreservedItemCount > 0
            ? CanvasAnnouncer.RenderLabel(
                new CanvasA11yEvent.CanvasLoadedDegraded((uint)PreservedItemCount))
            : null;

    /// <summary>
    /// The empty-state region's text (0a-13 LABEL class, core-rendered).
    /// </summary>
    /// <remarks>
    /// CD-37's tie-breaker INVERTED (§E TE-11, E14's first staged
    /// swap): the New Card chord is real now, so the onboarding event
    /// ships with it — core's render, host-supplied SPELLED-OUT chords
    /// (0a-13: a screen reader receives words, not glyphs; the glyph
    /// forms stay table labels). The interim palette sentence retires
    /// with the rule that demanded it.
    /// </remarks>
    public string? EmptyOnboardingText =>
        State == CanvasLoadState.Ready && _outline.Count == 0
            ? CanvasAnnouncer.RenderLabel(
                new CanvasA11yEvent.CanvasEmptyOnboarding(
                    "Control Alt N", "Control Shift P"))
            : null;

    /// <summary>The host-owned display chord the vocabulary takes as a
    /// parameter (contract 0a-9, decision 12: the chord table is
    /// host-owned). Read from the table so a rebinding cannot make the
    /// spoken chord a lie.</summary>
    internal static string PaletteChord =>
        Commands.ChordTable.WindowsChordFor("windows.view.showCommandPalette")
        ?? "Ctrl+Shift+P";

    /// <summary>Mutations are refused outside Ready (spec behavior 2);
    /// PR E's funnel is the first consumer.</summary>
    public bool IsReadOnly => State != CanvasLoadState.Ready;

    /// <summary>The row whose activation opened a card — the focus
    /// restoration target when the user comes back (WCAG 2.4.3,
    /// contract A14).</summary>
    public string? LastActivatedNode { get; set; }

    /// <summary>Rows republished — the surface rebuilds the tree on
    /// this, never on property-change granularity.</summary>
    public event EventHandler? OutlinePublished;

    /// <summary>The post-apply notification (§D ID-1): raised after
    /// EVERY apply, from whatever thread the apply ran on, carrying
    /// the publication that is now applied. The presentation engine
    /// subscribes and marshals itself — the document does not promise
    /// a thread here, and saying so is the contract.</summary>
    internal event Action<CanvasPublication>? PublicationApplied;

    /// <summary>The notification's subscriber count — the lifecycle
    /// facts pin that a renderer's attach/detach returns it to
    /// baseline (the review round's leak minor). Test seam by
    /// name.</summary>
    internal int PublicationAppliedSubscriberCountForTests =>
        PublicationApplied?.GetInvocationList().Length ?? 0;

    /// <summary>The surface switch landed — the workspace writes the
    /// persisted token for every tab on this path (contract A15).</summary>
    internal event EventHandler<CanvasSurfaceKind>? SurfaceChanged;

    /// <summary>
    /// A USER-INITIATED open wants keyboard focus on this canvas
    /// (contract A14).
    /// </summary>
    /// <remarks>
    /// Raised from the workspace's one focus-request funnel, which every
    /// open path already calls and no background path does. Focus used
    /// to be a side effect of the first publish while visible, which got
    /// both halves wrong: a retarget or a session restore published and
    /// STOLE focus from wherever the user was (defeating the guard
    /// comment right beside it), while a second tab on an already-open
    /// path is a registry hit that never publishes — so it never landed
    /// focus at all.
    /// </remarks>
    private CanvasFocusRequest? _focusRequest;

    /// <summary>
    /// The pending focus delivery — STATE, not an event edge
    /// (contract A14) — and ALWAYS null once the document is retired
    /// (C7).
    /// </summary>
    /// <remarks>
    /// <para>
    /// An edge is delivered once, at the instant it is raised, to
    /// whoever happens to be subscribed and ready. That is the wrong
    /// shape for this: the surface may not be loaded yet, may not be
    /// visible yet, and its virtualized container may not exist yet —
    /// three independent reasons the instant can be the wrong one, and
    /// none of them observable from the document. Four consecutive
    /// review rounds found a defect in this delivery, every one of them
    /// a variation on "the trigger fired and nothing was listening in
    /// the right state".
    /// </para>
    /// <para>
    /// As STATE it survives all three: the request stays pending until a
    /// surface actually delivers it and says so, surfaces retry on mount,
    /// on visibility, on publish and on container realization, and a
    /// newer request supersedes an older one by REFERENCE IDENTITY of
    /// the record (there is no generation counter; see `SetRequest`).
    /// </para>
    /// <para>
    /// The terminality is on the READ, not on a list of places that
    /// clear it. That is the mode stack's lesson one object over: a
    /// retirement that enumerates what to clear is a list somebody has
    /// to keep complete, and the surfaces do not consult a document's
    /// liveness before delivering — they consult this. `Shutdown` also
    /// drops the field, which is the other half and a different job: the
    /// request holds the closing tab's `DataContext`, and a retired
    /// document should not keep that graph alive.
    /// </para>
    /// </remarks>
    public CanvasFocusRequest? FocusRequest
    {
        get => IsShutDown ? null : _focusRequest;
        private set => SetRequest(ref _focusRequest, value);
    }

    /// <summary>
    /// `SetField` for a request: change detection by REFERENCE, because
    /// that is what a request IS to everything that reads it.
    /// </summary>
    /// <remarks>
    /// The requests are records, so `SetField`'s value equality made an
    /// identical re-raise a silent no-op — a reader who invokes Ctrl+F
    /// twice while the first request is still pending got no second
    /// notification, so the surfaces' request-property trigger never
    /// fired. That was impossible while the requests carried a
    /// generation counter, and became reachable the moment the counter
    /// was dropped for a reference comparison; the completion side
    /// already compares identity, and this makes the notification side
    /// agree with it. Raising the SAME instance twice is still silent,
    /// which is the case the equality check exists for.
    /// </remarks>
    private void SetRequest<T>(
        ref T? field, T? value, [CallerMemberName] string? name = null)
        where T : class
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }
        field = value;
        OnPropertyChanged(name);
    }

    /// <param name="owner">
    /// The view this request is FOR — the tab a surface carries as its
    /// <c>DataContext</c>. Required, with no default: an unaddressed
    /// request reaches every pane on the shared path and each one lands
    /// focus, and a default made that the easy thing to write.
    /// </param>
    /// <param name="nodeId">
    /// The row to land on, or null for the document's own answer (the
    /// last activated row, else the first). PR C's navigator passes one.
    /// </param>
    internal void RequestFocusLanding(object owner, string? nodeId = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (IsShutDown)
        {
            // WRITE-side terminality, and it is not redundant with the
            // read side: the read HIDES a field that was written, so a
            // late request repopulated the very reference retirement
            // exists to drop — the closed tab's owner and its object
            // graph — while every public read went on saying null.
            // `Shutdown` clears once; a request arriving after it has to
            // be refused, not cleared again.
            return;
        }
        FocusRequest = new CanvasFocusRequest(owner, nodeId);
    }

    /// <summary>
    /// A surface delivered the request and is saying so. Only the
    /// request that is still PENDING clears it, compared by reference:
    /// a request raised while an older one was in flight must not be
    /// consumed by the older one's late delivery.
    /// </summary>
    internal void CompleteFocusLanding(CanvasFocusRequest delivered)
    {
        ArgumentNullException.ThrowIfNull(delivered);
        if (ReferenceEquals(_focusRequest, delivered))
        {
            FocusRequest = null;
        }
    }

    /// <summary>The row a focus request should land on when it names
    /// none: the row whose activation the user is returning from (WCAG
    /// 2.4.3), else the first.</summary>
    internal string? FocusLandingNodeFor(CanvasFocusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.NodeId is { } named && _rows.ContainsKey(named))
        {
            return named;
        }
        if (LastActivatedNode is { } last && _rows.ContainsKey(last))
        {
            return last;
        }
        return _outline.Count > 0 ? _outline[0].NodeId : null;
    }

    /// <summary>The ONE surface switch (contracts A15/A18): the header
    /// switcher and the three <c>slate.canvas.show*</c> commands share
    /// it, so the state, the persisted token and the spoken sentence
    /// cannot disagree.</summary>
    public void ShowSurface(CanvasSurfaceKind surface)
    {
        if (Selection.ActiveSurface == surface)
        {
            return;
        }
        Selection.ActiveSurface = surface;
        Speak(new CanvasA11yEvent.CanvasSurfaceShown(surface));
        SurfaceChanged?.Invoke(this, surface);
    }

    /// <summary>The file-card route, installed by the workspace registry
    /// (the Bases <c>OpenRowFromSurface</c> precedent): the document
    /// never opens tabs; it hands (path, anchor) to the workspace, which
    /// owns the one navigation seam. Returns whether a tab opened.</summary>
    internal Func<string, LinkAnchor?, bool>? OpenFileCardFromSurface { get; set; }

    /// <summary>The external-link route, installed beside it. The scheme
    /// allowlist is applied HERE (contract A13) so the canvas cannot
    /// hand the shell a target the shared policy would refuse.</summary>
    internal Func<string, bool>? OpenExternalLinkFromSurface { get; set; }

    /// <summary>The default-app route for a non-Markdown attachment
    /// (contract A13, the mac media arm). Takes the VAULT-RELATIVE path:
    /// only the workspace knows the vault root, and only it should be
    /// composing an absolute one.</summary>
    internal Func<string, bool>? OpenMediaCardFromSurface { get; set; }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(PreservedItemCount));
        OnPropertyChanged(nameof(DegradedBannerText));
        OnPropertyChanged(nameof(EmptyOnboardingText));
    }

    public CanvasOutlineRow? RowFor(string? nodeId) =>
        nodeId is not null && _rows.TryGetValue(nodeId, out CanvasOutlineRow? row)
            ? row
            : null;

    /// <summary>Core's target for a card — the file path, the URL, or
    /// empty for text cards and groups (0b-13's one definition).</summary>
    public string TargetOf(string nodeId) =>
        _targets.TryGetValue(nodeId, out string? target) ? target : string.Empty;

    // --- The never-silent read gate (contract C4) -----------------------

    /// <summary>
    /// <b>The one state → response mapping for canvas READ verbs</b> —
    /// the mac <c>canvasReadRefusal(for:)</c> twin (VA-1/VA-2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null means the document can answer a core query. Anything else is
    /// the sentence its state owes the user. Static and total over
    /// (<see cref="CanvasLoadState"/> × handle), because the mac
    /// equivalent's history is three consecutive review rounds finding a
    /// state missing from a hand-written list:
    /// <c>TheReadMappingAnswersEveryLoadState</c> enumerates the enum
    /// rather than restating it, so a sixth state fails the test by name.
    /// </para>
    /// <para>
    /// The <c>Ready</c>-with-no-handle arm is VA-1's reopening window.
    /// Windows cannot reach it the way mac does — a rename RE-KEYS the
    /// registry and builds a fresh document rather than detaching a
    /// handle (CD-32) — so today it is reachable only on a RETIRED
    /// document, whose announcer is already silenced (contract A5). The
    /// arm exists because the mapping is a table over the state space,
    /// not over the states somebody remembered were reachable, and PR E's
    /// funnel and file watcher are the first things that could make it
    /// live.
    /// </para>
    /// </remarks>
    internal static CanvasStatusNote? ReadRefusalFor(CanvasLoadState state, bool handleLive) =>
        state switch
        {
            CanvasLoadState.Ready =>
                handleLive ? null : new CanvasStatusNote.Reopening(),
            CanvasLoadState.Loading => new CanvasStatusNote.Loading(),
            CanvasLoadState.ParseError => new CanvasStatusNote.NotReadable(),
            CanvasLoadState.Failed => new CanvasStatusNote.NotReadable(),
            CanvasLoadState.RetargetAbsent => new CanvasStatusNote.NotReadable(),
            _ => throw new UnreachableException(
                $"CanvasLoadState.{state} has no read-refusal answer. The read "
                + "mapping is a total table (contract C4); a new state is a "
                + "decision, not a silent arm."),
        };

    /// <summary>This document's refusal, or null when it can answer.</summary>
    internal CanvasStatusNote? ReadRefusal =>
        // Both halves from the APPLIED snapshot (the cleanup pass): a
        // sentence derived from the applied state and the LIVE slot is
        // a torn pair mid-reload — two currency authorities in one
        // answer.
        ReadRefusalFor(State, _applied?.Lease is not null);

    /// <summary>
    /// Admission for a read verb: true when the document can answer,
    /// false HAVING ANNOUNCED what the state owes. The announcement
    /// happens here so no verb decides which sentence its state deserves.
    /// </summary>
    internal bool AdmitStructuralRead()
    {
        if (ReadRefusal is not { } note)
        {
            return true;
        }
        Speak(new CanvasA11yEvent.CanvasStatus(note));
        return false;
    }

    /// <summary>
    /// Whether the surface is putting this document's retained rows on
    /// screen — the question the recorded precedence asks before the
    /// state's (contract C4, the mac <c>rendersRetainedSnapshot</c>
    /// twin): a selection question is meaningful exactly where the user
    /// can see rows to have a caret in.
    /// </summary>
    /// <remarks>
    /// DERIVED from what <see cref="CanvasSurfaceView"/> actually
    /// renders, not from the state's name — and
    /// <c>TheSnapshotVisibilityPredicateMatchesTheSurfaceRender</c>
    /// parses that method and fails when the two disagree, so changing
    /// what a state shows forces this to follow. Windows shows a
    /// projection only under <c>Ready</c>; mac additionally renders a
    /// read-only snapshot for its <c>retargetFailed</c>, which Windows
    /// has no equivalent of (CD-32).
    /// </remarks>
    internal bool RendersRetainedSnapshot => State == CanvasLoadState.Ready;

    /// <summary>
    /// The recorded precedence, in one place beside the mapping that owns
    /// the state story: a verb answers its OWN selection question before
    /// the state's — but only on a canvas whose rows the user can see.
    /// Returns true HAVING ANNOUNCED, so the caller returns.
    /// </summary>
    internal bool AnsweredMissingSelection()
    {
        if (!RendersRetainedSnapshot || Selection.Selected is not null)
        {
            return false;
        }
        AnnounceSelectionUnresolvable();
        return true;
    }

    /// <summary>
    /// The other never-silent arm: a structural query THREW while the
    /// handle was live, so the selection no longer names a card this
    /// canvas can answer for (contract 0b-6's row/model skew).
    /// </summary>
    /// <remarks>
    /// <c>Nothing selected.</c> is the accurate existing phrase for that:
    /// nothing RESOLVABLE is selected. Deliberately not the verb-specific
    /// phrase, because none of those was learned — the group might have
    /// children, the card might have a parent, the row might not be at
    /// canvas level. Announcing one of those asserts an answer no query
    /// gave.
    /// </remarks>
    internal void AnnounceSelectionUnresolvable() =>
        Speak(new CanvasA11yEvent.CanvasStatus(
            new CanvasStatusNote.NothingSelected()));

    // --- Structural queries (contract C5) --------------------------------

    /// <summary>
    /// Core's children of a group, in reading order (0b-8). False means
    /// the query could not answer — never "the group is empty", which is
    /// the distinction the VA-1 throw-arm table exists to keep.
    /// </summary>
    internal bool TryChildrenOf(string groupId, out IReadOnlyList<string> children) =>
        TryQuery<IReadOnlyList<string>>(
            handle => _session.CanvasChildrenOf(handle, groupId), [], out children);

    /// <summary>Core's containing group (0b-8). The out value is null for
    /// "at canvas level", which is why the ANSWER and the FAILURE are
    /// different returns — an <c>Option</c> collapsed into a nullable
    /// would erase exactly the distinction the two arms make.</summary>
    internal bool TryParentOf(string nodeId, out string? parent) =>
        TryQuery<string?>(handle => _session.CanvasParentOf(handle, nodeId), null, out parent);

    /// <summary>Core's cycle-safe greedy walk (0b-9), hops EXCLUDING the
    /// start card.</summary>
    internal bool TryTracePath(string nodeId, out IReadOnlyList<CanvasTraceHop> hops) =>
        TryQuery<IReadOnlyList<CanvasTraceHop>>(
            handle => _session.CanvasTracePath(handle, nodeId), [], out hops);

    /// <summary>
    /// Core's full readback for one card (t0 §1.4). The failure DETAIL
    /// comes out too: Where-am-I is the one read verb whose vocabulary
    /// carries a dynamic reason (<c>CanvasActionFailed{WhereAmI}</c>),
    /// and swallowing the message would make that parameter a
    /// constant.
    /// </summary>
    internal bool TryWhereAmI(string nodeId, out CanvasWhereAmI? context, out string detail)
    {
        detail = string.Empty;
        try
        {
            if (!Query(handle => _session.CanvasWhereAmI(handle, nodeId),
                out CanvasWhereAmI? answer))
            {
                context = null;
                return false;
            }
            context = answer;
            return true;
        }
        catch (VaultException exception)
        {
            context = null;
            detail = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// The selected card's connections, or NULL when nothing could
    /// answer.
    /// </summary>
    /// <remarks>
    /// The primitive <see cref="NeighborsOf"/> is built on: A11's outline
    /// rows want "no rows" for an unanswerable lookup, while
    /// follow-connection must not say <c>No outgoing connection.</c> —
    /// a claim about an adjacency list — when there is no list. One
    /// query, two honest answers.
    /// </remarks>
    public IReadOnlyList<CanvasNeighbor>? NeighborsIfKnown(string nodeId)
    {
        if (_neighbors.TryGetValue(nodeId, out IReadOnlyList<CanvasNeighbor>? cached))
        {
            return cached;
        }
        if (!TryQuery<IReadOnlyList<CanvasNeighbor>>(
            handle => _session.CanvasNeighbors(handle, nodeId),
            [],
            out IReadOnlyList<CanvasNeighbor> neighbors))
        {
            // A refusal caches nothing, so the skew of contract A16 heals
            // on the next ask.
            return null;
        }
        _neighbors[nodeId] = neighbors;
        return neighbors;
    }

    /// <summary>
    /// One handle-guarded FFI read. False means "no answer" — either no
    /// handle at all, or the model refused the id (<c>bad_node</c>) —
    /// and the caller decides which sentence that owes.
    /// </summary>
    private bool TryQuery<T>(Func<ulong, T> read, T fallback, out T value)
    {
        try
        {
            if (!Query(read, out T? answer))
            {
                value = fallback;
                return false;
            }
            value = answer!;
            return true;
        }
        catch (VaultException)
        {
            // bad_node / bad_handle — one refusal, never discriminated by
            // message (0b-12's API note).
            value = fallback;
            return false;
        }
    }

    /// <summary>
    /// One FFI read through the lease the publication names, admitted
    /// at LOCK time: the publication must still name that lease and the
    /// document must not be retired. False means no answer — nothing
    /// loaded, the lease replaced while the call waited, or the document
    /// retired — and the caller decides which sentence that owes.
    /// </summary>
    /// <remarks>
    /// U6's three validations in one predicate: a lease named by a live
    /// publication is current, and so is the population beside it,
    /// because the chain is one value. The predicate reads the slot and
    /// takes no gate, so there is no lock order here to get wrong; and
    /// it is evaluated INSIDE the lease's lock, because a check made
    /// before the lock cannot see a reload that landed while this
    /// caller was waiting for it (task T3).
    /// </remarks>
    private bool Query<T>(Func<ulong, T> read, out T? answer)
    {
        T? result = default;
        answer = default;
        if (_slot.Current.Lease is not { } lease)
        {
            return false;
        }
        bool admitted = lease.Invoke(
            () =>
            {
                CanvasPublication now = _slot.Current;
                return !now.Retired && now.Names(lease);
            },
            handle => result = read(handle));
        answer = result;
        return admitted;
    }

    // --- Filter (contract C10) -------------------------------------------

    /// <summary>
    /// The in-canvas filter's needle (#373). A VIEW over the outline and
    /// table — never a mutation; filtered-out cards stay in the file, and
    /// Escape's second rung restores the full canvas.
    /// </summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            string next = value ?? string.Empty;
            if (string.Equals(_filterText, next, StringComparison.Ordinal))
            {
                return;
            }
            _filterText = next;
            // The keystroke event, U9's first column: the needle INTENT,
            // the schedule transition and — when the request starts at
            // once — the pending unit, published together, and the job
            // runs off the dispatcher (task T6). Inline in synchronous
            // tests, so the answer is applied before this setter
            // returns.
            _machine.Typed(next);
            // Applied HERE, not only after a job: the clear's widen and
            // the keystroke's pending unit publish with no job to post
            // the projection, and an unapplied publication is how a
            // cleared filter's rows resurfaced under the next needle —
            // the T6 review's lead finding.
            ApplyPublication();
            OnPropertyChanged(nameof(FilterText));
            OnPropertyChanged(nameof(FilterActive));
            OutlinePublished?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// True when a non-blank needle narrows the surfaces. UI state —
    /// whether to show the Clear button, the result summary and the Esc
    /// rung — not the match rule, which is core's.
    /// </summary>
    /// <remarks>
    /// Mac keeps Foundation's <c>.whitespaces</c>, which does NOT include
    /// newlines, so a needle of nothing but a newline reads as active and
    /// (core trimming it) matches everything. .NET's
    /// <c>IsNullOrWhiteSpace</c> trims newlines too, which would make the
    /// same needle inactive — so the predicate is spelled out rather than
    /// borrowed, to keep the NEWLINE carve-out.
    /// <para>
    /// It is not mac's rule, and it was described as one until codex
    /// round 11. `.whitespaces` is Zs plus tab; <c>char.IsWhiteSpace</c>
    /// is wider, so U+000B, U+000C, U+0085, U+2028 and U+2029 read
    /// INACTIVE here and ACTIVE on mac. Five ratified divergences,
    /// recorded in §C's micro-divergence m2 and pinned as arms of
    /// <c>WhitespaceIsNotAFilterButANewlineIs</c> — the carve-out is
    /// matched deliberately, the rest is not matched at all.
    /// </para>
    /// </remarks>
    public bool FilterActive => IsFilterActive(_filterText);

    internal static bool IsFilterActive(string needle) =>
        CanvasFilterMachine.IsActiveNeedle(needle);

    /// <summary>Whether an active needle's answer has not landed yet:
    /// the document renders rows, a needle is in the field, and the
    /// applied unit's answer is neither the ANSWER nor the FAILURE for
    /// that needle. The count boundary says nothing in this state — the
    /// completion's projection announces when the answer lands — which
    /// is the distinction only an async match creates (§C's travelling
    /// row, task T6).</summary>
    internal bool FilterAnswerInFlight =>
        FilterVerdict == CanvasFilterVerdict.InFlight;

    /// <summary>The one classification (the cleanup pass): the view,
    /// the count boundary and the summary all read THIS, so the spoken
    /// count and the visible summary cannot drift — the invariant the
    /// old hand-kept ladders maintained separately.</summary>
    internal CanvasFilterVerdict FilterVerdict
    {
        get
        {
            if (!FilterActive)
            {
                return CanvasFilterVerdict.Inactive;
            }

            if (!RendersRetainedSnapshot)
            {
                return CanvasFilterVerdict.StateSentence;
            }

            if (_applied?.Unit is not { } unit
                || unit.Answer is CanvasAnswerState.Unfiltered or CanvasAnswerState.Pending
                || !string.Equals(unit.Needle, _filterText, StringComparison.Ordinal))
            {
                return CanvasFilterVerdict.InFlight;
            }

            return unit.Answer == CanvasAnswerState.Failed
                ? CanvasFilterVerdict.Failed
                : CanvasFilterVerdict.Current;
        }
    }

    /// <summary>The match for the needle in the field ran and could not
    /// answer, on a document that renders rows — the failed-answer bit,
    /// owed its own sentence rather than the state mapping's (T6
    /// review).</summary>
    internal bool FilterAnswerFailed =>
        FilterVerdict == CanvasFilterVerdict.Failed;

    /// <summary>The applied unit's answer state — the projection
    /// bookkeeping the clear-then-retype fact pins, reachable no other
    /// way. Test seam by name.</summary>
    internal CanvasAnswerState? AppliedFilterAnswerForTests => _applied?.Unit?.Answer;

    /// <summary>The applied population — the graph whose rows are on
    /// screen. The outline view resolves TRUE ancestry through this
    /// instead of re-deriving the depth walk the population already
    /// owns (the cleanup pass).</summary>
    internal CanvasPopulation? AppliedPopulation => _applied?.Population;

    /// <summary>The applied publication itself — the renderer's engine
    /// seeds from this on attach (§D TD-6), then rides the post-apply
    /// notification; both read the same committed value.</summary>
    internal CanvasPublication? AppliedPublication => _applied;

    /// <summary>
    /// What the surfaces show for the current needle, in reading order —
    /// ONE value, so the rows on screen, the summary's number and the
    /// announced count cannot disagree.
    /// </summary>
    /// <remarks>
    /// The MATCH is core's <c>canvas_filter</c> (0b-13/0b-14): title, the
    /// kind type word, any one element of the group path, and the
    /// activation target, run off the dispatcher by the machine (task
    /// T6). The needle goes over UNTRIMMED — core trims it, and an empty
    /// needle matches everything, so whitespace answers the full outline
    /// exactly as <see cref="FilterActive"/> says it should. The view
    /// derives from the APPLIED unit, so it moves when the projection
    /// applies — never mid-read — and the ids moving under an unchanged
    /// needle is a reload, which reseeds.
    /// </remarks>
    internal CanvasFilterView Filter
    {
        get
        {
            // Memoized per (applied publication, needle) — a VM-derived
            // view cache, not a publication cache: several consumers
            // read the view per publish, and it is a pure function of
            // the pair (the cleanup pass).
            if (_filterViewMemo is { } memo
                && ReferenceEquals(memo.Applied, _applied)
                && string.Equals(memo.Needle, _filterText, StringComparison.Ordinal))
            {
                return memo.View;
            }

            CanvasFilterView view = DeriveFilterView();
            _filterViewMemo = (_applied, _filterText, view);
            return view;
        }
    }

    private (CanvasPublication? Applied, string Needle, CanvasFilterView View)? _filterViewMemo;

    private CanvasFilterView DeriveFilterView()
    {
        if (!FilterActive)
        {
            return new CanvasFilterView(_outline, Narrowed: false, Current: true, null);
        }
        // Derived from the APPLIED unit — the same publication the
        // rows on screen came from — never from a fresh query (task
        // T6). The unit and the rows it filters are one immutable
        // value, so the count and the rows cannot disagree, which
        // is the one invariant C10 has.
        if (_applied?.Unit is not { } unit)
        {
            // Nothing ever applied: the unfiltered outline IS the
            // prior view, and `Narrowed: false` keeps the summary
            // from claiming these rows matched anything.
            return new CanvasFilterView(_outline, Narrowed: false, Current: false, null);
        }
        if (unit.Answer == CanvasAnswerState.Answered
            && string.Equals(unit.Needle, _filterText, StringComparison.Ordinal))
        {
            return new CanvasFilterView(
                RowsOf(unit),
                Narrowed: true,
                Current: RendersRetainedSnapshot,
                unit.Matched);
        }
        // Pending, failed, or answered for an older needle: the
        // previous answer stays on screen and `Current` is false —
        // widening silently back to the full outline would show
        // every card while the field still claims to be filtering,
        // and then speak that number as a match count.
        // The unit CARRIES whether its rows came from a landed
        // answer (the cleanup pass; the set-size arithmetic this
        // replaces misread a match-everything answer).
        return unit.Narrowed
            ? new CanvasFilterView(
                RowsOf(unit), Narrowed: true, Current: false, unit.Matched)
            : new CanvasFilterView(_outline, Narrowed: false, Current: false, null);
    }

    /// <summary>The outline rows the surfaces display.</summary>
    public IReadOnlyList<CanvasOutlineRow> FilteredOutline => Filter.Rows;

    /// <summary>The table rows the grid displays — core's rows, narrowed
    /// by the SAME answer the outline shows (contract C10).</summary>
    public IReadOnlyList<CanvasTableRow> FilteredTableRows =>
        Filter.MatchedIds is { } ids
            ? _tableRows.Where(row => ids.Contains(row.NodeId)).ToArray()
            : _tableRows;

    /// <summary>The unit's rows by its own ordered answer: FilteredOrder
    /// is already the matched ids in population order, and the row index
    /// makes this O(matched) per read — every publish consumer walks the
    /// getter, and the O(outline) scan this replaced was the T6 review's
    /// efficiency note.</summary>
    private IReadOnlyList<CanvasOutlineRow> RowsOf(CanvasProjectionUnit unit)
    {
        var rows = new List<CanvasOutlineRow>(unit.FilteredOrder.Length);
        foreach (string nodeId in unit.FilteredOrder)
        {
            if (_rows.TryGetValue(nodeId, out CanvasOutlineRow? row))
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    /// <summary>
    /// The pending request to put the reader in the filter field, or
    /// null. ADDRESSED and DURABLE, the A14 shape (contract C10).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A bare token was neither. A surface that could not satisfy it had
    /// to choose between consuming it — which is how the palette route
    /// silently did nothing — and keeping it, which is how the OTHER
    /// pane on the same document pulled the reader into its own filter
    /// field minutes later. Both are focus moves the user did not ask
    /// for; addressing the request removes the choice.
    /// </para>
    /// <para>
    /// Owner-scoped like <see cref="FocusRequest"/>: a surface delivers
    /// only what is addressed to it, and the delivery clears it on the
    /// DOCUMENT, so peers stop holding it. Superseded by REFERENCE
    /// IDENTITY of the record, so a newer request replaces an older one
    /// rather than being consumed by its late delivery — the surface
    /// hands back the instance it was given, and a value-equal record
    /// from a different raise is not that instance.
    /// </para>
    /// </remarks>
    public CanvasFilterFocusRequest? FilterFocusRequest
    {
        get => IsShutDown ? null : _filterFocusRequest;
        private set => SetRequest(ref _filterFocusRequest, value);
    }

    /// <summary>
    /// The ONE place this document speaks (contracts A5/C7).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A retired document composes nothing — the mode stack's `Speak`,
    /// one object out, and found the same way: the Debug gate caught
    /// `AdmitStructuralRead` announcing a refusal through a retired
    /// announcer, because a verb invoked on a document the shell had
    /// already closed still walked the never-silent mapping. There are
    /// sixteen announce sites here; gating them one at a time is the
    /// list this project has now been bitten by four times.
    /// </para>
    /// <para>
    /// The navigator speaks through this too, so the whole canvas has
    /// one boundary rather than one per class. The announcer keeps its
    /// `Debug.Fail`: this stops the SPEAKER from composing, and anything
    /// still reaching the funnel afterwards is a real defect the guard
    /// should be loud about.
    /// </para>
    /// </remarks>
    internal void Speak(CanvasA11yEvent @event)
    {
        // The FUNNEL's retirement, not the document's own shutdown flag.
        // They are not the same instant and the difference is C7's SPEAK
        // phase: `Shutdown` marks the document first and then drains the
        // mode stack, whose restoration is the last sentence a
        // retirement owes and must still be heard. Everything composed
        // after `_announcer.Shutdown()` is what has nobody to hear it.
        if (_announcer.IsRetired)
        {
            return;
        }
        _announcer.Announce(@event);
    }

    /// <summary>
    /// Drop any pending request addressed to an owner that no longer
    /// exists (contracts A14/C10).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A request is addressed to a TAB, and a tab can close while the
    /// document lives on — two panes share one, so closing the pane that
    /// asked leaves the request pending forever: no peer may take it
    /// (the address gate), nothing supersedes it, and the closed tab's
    /// object graph stays reachable through it. Retirement does not
    /// cover this, because the document is not being retired.
    /// </para>
    /// <para>
    /// Taken at the boundary where the tab SET changes rather than in
    /// `Unloaded`, which also fires when a tab is merely hidden — a
    /// hidden pane's request must survive, since becoming visible again
    /// is one of the conditions that delivers it.
    /// </para>
    /// </remarks>
    internal void DropRequestsAddressedOutside(IReadOnlyCollection<object> liveOwners)
    {
        ArgumentNullException.ThrowIfNull(liveOwners);
        if (_focusRequest is { } landing && !liveOwners.Contains(landing.Owner))
        {
            FocusRequest = null;
        }
        if (_filterFocusRequest is { Owner: { } owner } && !liveOwners.Contains(owner))
        {
            FilterFocusRequest = null;
        }
    }

    /// <summary>
    /// Whether the document is still HOLDING either durable request's
    /// object — retirement's reference half.
    /// </summary>
    /// <remarks>
    /// It needs its own observable because the boundary makes both
    /// properties read null either way: the two do different jobs, and a
    /// mechanism whose removal changes nothing observable is a claim
    /// without a power. This one's job is that a retired document stops
    /// holding a closed tab's `DataContext`, and through it that tab's
    /// object graph.
    /// </remarks>
    internal bool HoldsPendingRequestsForTests =>
        _focusRequest is not null || _filterFocusRequest is not null;

    /// <param name="owner">
    /// The view the request is FOR — the surface's tab. Null when no
    /// surface has ever held the keys on this document, which is the one
    /// case with nobody to address: the request is raised anyway and the
    /// first eligible surface takes it, because a verb the user invoked
    /// must not evaporate.
    /// </param>
    internal void RequestFilterFocus(object? owner)
    {
        if (IsShutDown)
        {
            // The twin's write-side terminality, for the twin's reason.
            return;
        }
        FilterFocusRequest = new CanvasFilterFocusRequest(owner);
    }

    /// <summary>
    /// A surface put the reader in its filter field and is saying so.
    /// Only the request that is still PENDING clears it, by reference,
    /// for <see cref="CompleteFocusLanding"/>'s reason.
    /// </summary>
    internal void CompleteFilterFocus(CanvasFilterFocusRequest delivered)
    {
        ArgumentNullException.ThrowIfNull(delivered);
        if (ReferenceEquals(_filterFocusRequest, delivered))
        {
            FilterFocusRequest = null;
        }
    }

    // --- Where am I (contract C11) ---------------------------------------

    /// <summary>
    /// The rendered Where-am-I readback, or null when the panel is
    /// closed. The SAME string the announcement speaks — one render, no
    /// second composition (t0 §1.4/§3).
    /// </summary>
    public string? WhereAmIText
    {
        get => _whereAmIText;
        internal set => SetField(ref _whereAmIText, value);
    }

    // --- Load -----------------------------------------------------------

    /// <summary>Open (or reopen) the canvas and publish its
    /// projections. The full-reload shape — close, open, outline,
    /// table, scene — the mac <c>load</c> twin. A reload IS an open, so
    /// the once-per-open degraded announcement re-arms here
    /// (contract A4).</summary>
    /// <remarks>
    /// Task T3: U4's operation. The request publishes Loading and
    /// un-names the old lease in one swap, on this thread; the worker
    /// closes that lease, opens, builds and delivers on its own; and
    /// the projection of whatever the slot then holds is posted back
    /// here. A retired document requests nothing — the publication says
    /// so, and the scheduler's own refusal is the second wall.
    /// </remarks>
    public void Load()
    {
        if (_pipeline.Request() is not { } request)
        {
            return;
        }
        _announcedDegradedLoad = false;
        ApplyPublication();
        StartWork(() =>
        {
            try
            {
                _ = _pipeline.Deliver(request);
            }
            finally
            {
                // Whatever the delivery did, the surface reflects the
                // publication that resulted — a failure's included.
                Post(ApplyPublication);
            }
        });
    }

    /// <summary>
    /// The projection: read the slot ONCE and apply what it holds to the
    /// bindable surface — the load state and message, the rows, the
    /// warnings, the indexes — on the dispatcher.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The effect obligation I3 names for the load path, with its
    /// validation at DISPATCH: a posted projection reads the slot when
    /// it runs, not when it was posted, so one queued behind a newer
    /// load applies the newer load, and one arriving after retirement
    /// applies nothing. Idempotent, because two deliveries post two of
    /// them and the second finds its publication already applied.
    /// </para>
    /// <para>
    /// The rows are this object's COHERENT PAST while a reload is in
    /// flight: the publication has no population then, and the surface
    /// collapses its projections under Loading anyway, so the rows stay
    /// until the new ones land or a failure clears them — presentation
    /// outside the chain, as the design says.
    /// </para>
    /// </remarks>
    private void ApplyPublication()
    {
        CanvasPublication current = _slot.Current;
        if (current.Retired || ReferenceEquals(current, _applied))
        {
            return;
        }
        CanvasPublication? was = _applied;
        _applied = current;
        _applying = true;
        try
        {
            Apply(current, was);
        }
        finally
        {
            _applying = false;
            // In the FINALLY (the review round): a throwing Apply must
            // not suppress the notification — the doc contract is
            // "after EVERY apply", and a skipped raise left the
            // presentation engine on the previous population for the
            // life of the publication.
            PublicationApplied?.Invoke(current);
            // §F TF-2 (F1a): displacement ends a held mode.
            WatchTransientDisplacement(current);
        }
        // The APPLY-WINDOW seat, reconciled (the tooltip slice's
        // finding): a projection can seat the shared selection DURING
        // an apply — the outline's tree auto-selects its first row on
        // rebuild — and the `_applying` guard, built to stop a
        // failure-CLEAR from erasing the durable intent, swallowed
        // that positive seat too, leaving the surface authority ahead
        // of the published intent for as long as the reader stayed
        // put. A NON-NULL seat that differs from the applied intent
        // publishes now, after the window closes; a cleared selection
        // still never erases the intent, which is T3's rule intact.
        if (Selection.Selected is { } seated
            && !string.Equals(_applied?.SelectedIntent, seated, StringComparison.Ordinal))
        {
            PublishIntent(nameof(CanvasSelection.Selected));
        }
    }

    private void Apply(CanvasPublication current, CanvasPublication? was)
    {
        switch (current.LoadState)
        {
            case CanvasLoadState.Loading:
                StateMessage = null;
                State = CanvasLoadState.Loading;
                break;
            case CanvasLoadState.Ready:
                // Ready names a population by construction — the
                // acceptance writes both in one swap — and the same
                // population under a later publication (an intent moved)
                // is already on screen.
                if (current.Loaded is not { } loaded)
                {
                    break;
                }
                if (!ReferenceEquals(loaded.Population, was?.Population))
                {
                    PublishReady(
                        loaded.Population,
                        loaded.Unit,
                        intentRepublished: !string.Equals(
                            current.SelectedIntent,
                            was?.SelectedIntent,
                            StringComparison.Ordinal));
                    break;
                }
                if (!ReferenceEquals(loaded.Unit, was?.Unit)
                    && loaded.Unit.Answer
                        is CanvasAnswerState.Answered or CanvasAnswerState.Failed)
                {
                    // A filter completion's successor unit (task T6):
                    // same population, new projection. PENDING
                    // successors keep their rows verbatim — a promotion
                    // rebuild would repaint identical rows (T6 review) —
                    // so only a landed answer, or its failure, rebuilds
                    // the surfaces; and that is the debounced count's
                    // moment to speak.
                    //
                    // E16/m7: the supersession happens IN the
                    // publication that accepts the filter ANSWER. A
                    // pending focus request naming a node this answer
                    // excludes would otherwise sleep through the
                    // narrowing and land a surprise jump when the
                    // filter later cleared; the request and the result
                    // cannot cross, so they meet here — before any
                    // surface hears the rows changed. A FAILED answer
                    // keeps the rows it was showing (contract C10), so
                    // it excludes nothing new and supersedes nothing.
                    if (loaded.Unit.Answer is CanvasAnswerState.Answered
                        && _focusRequest is { NodeId: { } sought }
                        && !loaded.Unit.FilteredOrder.Contains(sought))
                    {
                        FocusRequest = null;
                    }
                    OutlinePublished?.Invoke(this, EventArgs.Empty);
                    Navigator.AnnounceFilterCount();
                }
                break;
            default:
                if (was?.LoadState != current.LoadState
                    || was.LoadMessage != current.LoadMessage)
                {
                    PublishEmpty();
                    Warnings = [];
                    _preservedCount = 0;
                    StateMessage = current.LoadMessage;
                    State = current.LoadState;
                    OutlinePublished?.Invoke(this, EventArgs.Empty);
                }
                break;
        }
    }

    /// <summary>The UI-originated intents, published as they change —
    /// captured BEFORE the gate, because a transform that read the live
    /// selection would be reading C-lite state inside it (I4).</summary>
    private void PublishIntent(string? property)
    {
        if (_applying)
        {
            // The apply's own seats are not user intents: a failure
            // clearing the selection must not erase the durable intent
            // the NEXT load's rebase exists to resolve — "a node that
            // comes back on the next load should come back selected"
            // (the T3 review's fourth finding).
            return;
        }
        switch (property)
        {
            case nameof(CanvasSelection.Selected):
                string? selected = Selection.Selected;
                _ = _slot.Publish(s => s.Retired ? null : s.WithSelectedIntent(selected));
                break;
            case nameof(CanvasSelection.ActiveSurface):
                CanvasSurfaceKind surface = Selection.ActiveSurface;
                _ = _slot.Publish(s => s.Retired ? null : s.WithActiveSurface(surface));
                break;
            case nameof(CanvasSelection.Marked):
                ImmutableHashSet<string> marked = CanvasModelCopy.Ids(Selection.Marked);
                _ = _slot.Publish(s => s.Retired ? null : s.WithMarkedIntent(marked));
                break;
            default:
                return;
        }
        // Applied HERE (§D's tooltip slice found the gap): an intent
        // publication used to reach the slot and wait for the NEXT
        // load or filter apply, so the applied snapshot — and the
        // presentation engine reading it — lagged the selection the
        // reader just made. The apply is cheap for a same-population
        // successor and keeps the one-snapshot story true.
        ApplyPublication();
    }

    private void PublishEmpty()
    {
        _rows.Clear();
        _targets.Clear();
        _subpaths = System.Collections.Immutable.ImmutableDictionary<string, string>.Empty;
        _neighbors.Clear();
        WhereAmIText = null;
        Outline = [];
        TableRows = [];
        Selection.Selected = null;
    }

    private void PublishReady(
        CanvasPopulation population,
        CanvasProjectionUnit unit,
        bool intentRepublished = false)
    {
        // §E TE-5b: every ready publish rebases the history domain to
        // the population's basis — a reload quarantines what no longer
        // applies, a mutation's own refresh re-affirms what does.
        UndoStack.Rebase(population.ContentHash);
        _rows.Clear();
        _targets.Clear();
        _subpaths = System.Collections.Immutable.ImmutableDictionary<string, string>.Empty;
        _neighbors.Clear();
        WhereAmIText = null;
        foreach (CanvasOutlineRow row in population.Outline)
        {
            _rows[row.NodeId] = row;
        }
        foreach (CanvasTableRow row in population.Table)
        {
            _targets[row.NodeId] = row.Target;
        }
        // Aliased, not copied: the population's index is immutable and
        // already keyed the way this reads it (the cleanup pass).
        _subpaths = population.Subpaths;
        Warnings = population.Warnings;
        // The population derives this from its own warnings; the VM
        // captures it once per load instead of recounting per binding
        // read (the cleanup pass).
        _preservedCount = (int)population.PreservedCount;
        Outline = population.Outline;
        TableRows = population.Table;
        StateMessage = null;
        State = CanvasLoadState.Ready;
        // A reload keeps the selection when the node survived; a
        // selection pointing at a node that is gone would leave every
        // selection-scoped verb acting on nothing (contract A12).
        // Checked against the selection AS IT IS NOW, at apply time —
        // the user may have moved between the swap and this apply, and
        // the T3 review caught the rebase's answer clobbering that
        // move. The rebase's resolution is the FALLBACK: when the
        // current seat did not survive, the resolved intent is what
        // comes back, else the first row. Silent seat either way (A12):
        // the focus lands on the row and the screen reader reads it.
        if (intentRepublished)
        {
            // §E TE-5b (E4): the publication CARRIES a new intent —
            // only deliberate publishes change it, and a user's own
            // move publishes an intent equal to the seat it came
            // from, so this arm fires exactly for the funnel's
            // declared effects (select-created seats the new card,
            // clear-selection seats nothing) and never for the T3
            // mid-flight move the survivor rule protects.
            Selection.Selected = unit.ResolvedSelection;
        }
        else if (Selection.Selected is not { } kept || !_rows.ContainsKey(kept))
        {
            Selection.Selected = unit.ResolvedSelection
                ?? (population.Count > 0 ? population.Outline[0].NodeId : null);
        }
        OutlinePublished?.Invoke(this, EventArgs.Empty);
        AnnounceDegradedLoadIfNeeded();
    }

    /// <summary>Contract A4: the polite "loaded with skipped items"
    /// announcement, exactly once per DOCUMENT open — the registry's
    /// 0→1 transition, so two panes on one canvas hear it once
    /// (CD-29).</summary>
    private void AnnounceDegradedLoadIfNeeded()
    {
        int skipped = PreservedItemCount;
        if (skipped == 0 || _announcedDegradedLoad)
        {
            return;
        }
        _announcedDegradedLoad = true;
        Speak(new CanvasA11yEvent.CanvasLoadedDegraded((uint)skipped));
    }

    private static string ParseErrorDetail(IReadOnlyList<CanvasLoadWarning> warnings) =>
        warnings.FirstOrDefault(
            warning => warning.Kind == CanvasLoadWarningKind.ParseFailed)?.Detail
            ?? CanvasPhrase.NotValidJsonCanvas;

    // --- Per-node reads -------------------------------------------------

    /// <summary>
    /// The selected card's connections (contract A11). Fallible by
    /// contract A16: the rows are SQLite-served while the handle holds
    /// an open-time snapshot, so an id the outline names can be one the
    /// model refuses. A refusal answers EMPTY and caches nothing, so the
    /// row stays selectable and the next ask heals.
    /// </summary>
    public IReadOnlyList<CanvasNeighbor> NeighborsOf(string nodeId) =>
        NeighborsIfKnown(nodeId) ?? [];

    /// <summary>The interim card text (t2 #362). Null and an announced
    /// refusal for a non-text card or an id the model does not know —
    /// the 0b never-silent table.</summary>
    public string? NodeTextOf(string nodeId)
    {
        try
        {
            if (!Query<string?>(handle => _session.CanvasNodeText(handle, nodeId),
                out string? text))
            {
                Speak(new CanvasA11yEvent.CanvasStatus(
                    new CanvasStatusNote.NotReadable()));
                return null;
            }
            if (text is null)
            {
                Speak(new CanvasA11yEvent.CanvasStatus(
                    new CanvasStatusNote.NotATextCard()));
            }
            return text;
        }
        catch (VaultException)
        {
            Speak(new CanvasA11yEvent.CanvasBlocked(
                new CanvasBlockedReason.CardTextUnreadable()));
            return null;
        }
    }

    // --- Selection ------------------------------------------------------

    /// <summary>
    /// The ONE selection mutation that narrates (contract A12): group
    /// boundary first, then the move. Re-selecting the same node is a
    /// no-op, which is what breaks the surface ⇄ model echo.
    /// </summary>
    public void SelectNode(string? nodeId, bool announce = true)
    {
        if (string.Equals(Selection.Selected, nodeId, StringComparison.Ordinal))
        {
            return;
        }
        CanvasOutlineRow? previous = RowFor(Selection.Selected);
        Selection.Selected = nodeId;
        if (!announce || RowFor(nodeId) is not { } row)
        {
            return;
        }
        if (GroupBoundaryEvent(previous?.GroupPath ?? [], row) is { } boundary)
        {
            Speak(boundary);
        }
        Speak(new CanvasA11yEvent.CanvasMovedTo(
            Verbosity: Verbosity,
            KindLabel: row.Kind,
            Title: row.Title,
            OrdinalN: row.OrdinalN,
            TotalM: row.TotalM,
            Container: row.GroupPath.Length > 0 ? row.GroupPath[^1] : null,
            ConnectionCount: row.ConnectionCount,
            ColorName: row.ColorName,
            Marked: Selection.IsMarked(row.NodeId)));
    }

    /// <summary>
    /// Seat the shared selection with NO announcement — the A12 silent
    /// seat, extended to focus delivery (contract C12, CD-40).
    /// </summary>
    /// <remarks>
    /// A14 lands focus on the row the user is returning FROM, which can
    /// differ from the selection when another pane moved it. Both
    /// projections seat selection as an inseparable part of taking
    /// focus — WPF's <c>TreeViewItem</c> selects itself on
    /// <c>GotFocus</c>, and a <c>DataGrid</c>'s currency IS its focused
    /// row — so the reachable choice is not "seat or don't", it is
    /// "audibly or silently". Silently: the screen reader reads the row
    /// it just landed on, and a <c>CanvasMovedTo</c> on top of that is
    /// the t0 §1.5 doubling rule broken on a landing the user did not
    /// make.
    /// </remarks>
    internal void SeatSelectionSilently(string? nodeId) =>
        Selection.Selected = nodeId;

    /// <summary>
    /// The group-boundary event a move crosses into, or null when the
    /// container did not change (contract A12). PURE, so the CD-4 count
    /// rule is unit-tested without a coalescer in the way — the mac
    /// <c>CanvasOutlineView.returnOpensRow</c> pattern.
    ///
    /// Note the audible consequence, which is mac's too: this event and
    /// the <c>CanvasMovedTo</c> that follows it share the
    /// <c>navigation</c> coalescing class (0a-8), so within the 200 ms
    /// window the move supersedes the boundary. The membership list is
    /// pinned core-side and is not this host's to change; both hosts
    /// therefore behave identically, which is the property §W-D exists
    /// to protect.
    /// </summary>
    internal static CanvasA11yEvent? GroupBoundaryEvent(
        IReadOnlyList<string> previousPath, CanvasOutlineRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.GroupPath.SequenceEqual(previousPath, StringComparer.Ordinal))
        {
            return null;
        }
        if (row.GroupPath.Length > 0
            && !previousPath.Contains(row.GroupPath[^1], StringComparer.Ordinal))
        {
            // CD-4: the ENTERED group's own card count is exactly the
            // arrived-at row's container size — never the sibling count.
            return new CanvasA11yEvent.CanvasGroupEntered(row.GroupPath[^1], row.TotalM);
        }
        if (previousPath.Count > 0
            && !row.GroupPath.Contains(previousPath[^1], StringComparer.Ordinal))
        {
            return new CanvasA11yEvent.CanvasGroupLeft(previousPath[^1]);
        }
        return null;
    }

    /// <summary>Follow a connection row (contract A11): select the other
    /// card and narrate the move, so the outline and the PR C navigator
    /// speak one grammar.</summary>
    public void FollowConnection(CanvasNeighbor neighbor)
    {
        ArgumentNullException.ThrowIfNull(neighbor);
        SelectNode(neighbor.OtherNode);
    }

    // --- Activation -----------------------------------------------------

    /// <summary>Activation per kind (contract A13). Every arm speaks
    /// canvas vocabulary; nothing here composes a sentence.</summary>
    public CanvasActivation Activate(CanvasOutlineRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        switch (row.Kind)
        {
            case "group":
                return CanvasActivation.ExpandGroup;
            case "text":
                return ActivateTextCard(row);
            case "link":
                return ActivateLinkCard(row);
            default:
                return ActivateFileCard(row);
        }
    }

    private CanvasActivation ActivateTextCard(CanvasOutlineRow row)
    {
        // §E TE-11b (E14's second staged swap, A13): activation
        // opens the REAL editor sheet through the TE-8 seam - the
        // workspace answers the request, and OpenCardEditor owns every
        // refusal (not-a-text-card, unreadable) with its spoken arms.
        // The interim read-only detail retired with this.
        LastActivatedNode = row.NodeId;
        RequestCardEditor(row.NodeId);
        return CanvasActivation.EditorRequested;
    }

    private CanvasActivation ActivateLinkCard(CanvasOutlineRow row)
    {
        string url = TargetOf(row.NodeId);
        // The ONE allowlist every shell hand-off shares
        // (ExternalLinkPolicy): a canvas file cannot smuggle a `file:`
        // or `javascript:` target past it. The PREDICATE is shared; the
        // refusal below is canvas vocabulary, because a canvas surface
        // speaking the ExternalLink* family would be §W-D drift.
        if (!ExternalLinkPolicy.IsLaunchable(url))
        {
            Speak(new CanvasA11yEvent.CanvasBlocked(
                new CanvasBlockedReason.NotAUrl()));
            return CanvasActivation.Refused;
        }
        if (OpenExternalLinkFromSurface?.Invoke(url) == true)
        {
            LastActivatedNode = row.NodeId;
            Speak(new CanvasA11yEvent.CanvasOpened(
                row.Title, CanvasOpenTarget.Browser));
            return CanvasActivation.Opened;
        }
        Speak(new CanvasA11yEvent.CanvasBlocked(
            new CanvasBlockedReason.LinkOpenFailed()));
        return CanvasActivation.Refused;
    }

    /// <summary>
    /// File and image cards route on the TARGET, not on the kind — the
    /// mac reference (<c>CanvasContainerView.swift:169–187</c>): a
    /// Markdown target opens a note tab, at its subpath anchor; anything
    /// else opens in whatever app owns the type.
    /// </summary>
    /// <remarks>
    /// Routing on the KIND instead sent every image card through the
    /// note-open seam, and <c>ItemForPath</c> calls any extension that is
    /// not <c>.canvas</c>/<c>.base</c> Markdown — so activating an image
    /// replaced the canvas tab with an editor over the PNG's bytes. The
    /// vocabulary's never-used <c>CanvasOpenTarget.DefaultApp</c> arm was
    /// the tell that a whole branch was missing.
    /// </remarks>
    private CanvasActivation ActivateFileCard(CanvasOutlineRow row)
    {
        string target = TargetOf(row.NodeId);
        if (target.Length == 0 || !TargetExistsInVault(target))
        {
            // t0 §5: the card stays navigable; the vocabulary names the
            // recovery (Locate File, PR E).
            Speak(new CanvasA11yEvent.CanvasFileNotFound(
                target.Length == 0 ? row.Title : target));
            return CanvasActivation.Refused;
        }
        if (!IsMarkdownTarget(target))
        {
            // The shell-execution gate (CD-38). A canvas is untrusted
            // input and ShellExecute EXECUTES what it is handed, so only
            // media opens; everything else is refused, audibly. NOT the
            // link allowlist — that gates URLs handed to a browser, a
            // different hand-off with a different failure mode.
            if (!CanvasMediaPolicy.IsOpenableMedia(target))
            {
                // The vocabulary has no "this file type is not openable"
                // reason, so the refusal rides the generic failed-action
                // arm with the TARGET as its dynamic detail — never a
                // host-authored sentence (0a-1). The gap is recorded in
                // CD-38 as a STOP point: the typed reason is a core
                // change this task may not make.
                Speak(new CanvasA11yEvent.CanvasActionFailed(
                    CanvasFailedAction.CanvasAction, target));
                return CanvasActivation.Refused;
            }
            if (OpenMediaCardFromSurface?.Invoke(target) == true)
            {
                LastActivatedNode = row.NodeId;
                Speak(new CanvasA11yEvent.CanvasOpened(
                    row.Title, CanvasOpenTarget.DefaultApp));
                return CanvasActivation.Opened;
            }
            // NOT file-not-found. Control only reaches here with a target
            // that EXISTS (checked above) and IS media (checked above), so
            // every way the open can fail is a refusal, not an absence:
            // the containment gate refusing a junction that escapes the
            // vault, the identity query failing on a volume whose
            // filesystem does not answer FileIdInfo (CD-38's recorded
            // NTFS/ReFS limitation), the TOCTOU revalidation catching a
            // swap, ShellExecute finding no association, or the closure
            // being absent entirely. Announcing CanvasFileNotFound told
            // the user the file "is missing from the vault" and sent them
            // to Locate File to repoint a card whose target is perfectly
            // fine — a wrong answer, which this vocabulary never gives.
            // It rides the same generic failed-action arm as the
            // non-media refusal above, for the same reason and with the
            // same recorded STOP: a typed "could not be opened" reason
            // for FILES is a core change this task may not make
            // (CanvasBlockedReason::LinkOpenFailed is for URLs).
            Speak(new CanvasA11yEvent.CanvasActionFailed(
                CanvasFailedAction.CanvasAction, target));
            return CanvasActivation.Refused;
        }
        LastActivatedNode = row.NodeId;
        return OpenFileCardFromSurface?.Invoke(target, AnchorFor(row.NodeId)) == true
            ? CanvasActivation.Navigated
            : CanvasActivation.Refused;
    }

    /// <summary>Mac's own test (<c>target.lowercased().hasSuffix</c>),
    /// transliterated — and the same set <c>ItemForPath</c> treats as an
    /// editable note.</summary>
    internal static bool IsMarkdownTarget(string target) =>
        target.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
        || target.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the vault knows this target. A session-level identity
    /// read that touches no handle, so it has no lease to hold: A17's
    /// lock is the LEASE's since task T3, and it guards handle
    /// replacement against handle reads — a rule with nothing to say
    /// about a call that names no handle.
    /// </summary>
    private bool TargetExistsInVault(string target)
    {
        try
        {
            return _session.CanonicalPath(target) is not null;
        }
        catch (VaultException)
        {
            // An identity we cannot read is left alone (fail closed,
            // the #1077 contract I7 shape): treat it as present and let
            // the navigation seam surface whatever it finds.
            return true;
        }
    }

    /// <summary>The JSON Canvas subpath as the W3-5 anchor the editor
    /// navigation resolves: <c>#^id</c> is a block reference, any other
    /// <c>#…</c> a heading (contract A13).</summary>
    internal LinkAnchor? AnchorFor(string nodeId)
    {
        if (!_subpaths.TryGetValue(nodeId, out string? subpath)
            || !subpath.StartsWith('#'))
        {
            return null;
        }
        string body = subpath[1..];
        if (body.StartsWith('^'))
        {
            return body.Length > 1 ? new LinkAnchor("block", body[1..]) : null;
        }
        return body.Length > 0 ? new LinkAnchor("heading", body) : null;
    }

    // --- Teardown -------------------------------------------------------

    /// <summary>Non-blocking in production: the close runs off the
    /// dispatcher (a non-cancellable FFI call may hold the lock) and is
    /// exposed as <see cref="WhenHandleClosed"/> for the workspace's
    /// bounded close-before-session drain. Synchronous test mode closes
    /// inline. Marks and selection die with the object — contract A1's
    /// "cleared when the last tab closes".</summary>
    internal override void Shutdown()
    {
        // U12's sequence (task T3). Step 1: the PRETERMINAL retired
        // publication — the marker set, everything else retained — so
        // the retired interval is a published model state and every
        // model boundary refuses from here. Step 2: before the base
        // shutdown (SEAM 3), so the scheduler's refusal can never
        // precede the model's. Step 3: the T4 seam — the mode stack's
        // last sentence, the announcer, the durable requests — which
        // the model neither authorizes nor describes. Step 4, in the
        // finally: the TERMINAL publication, unconditionally, and the
        // close of whatever lease it un-named, under that lease's own
        // lock and off the dispatcher in production.
        _ = _slot.Publish(s => s.WithRetired());
        UndoStack.Clear();
        try
        {
            base.Shutdown();
            // M4's last case, the deferred-departure drain with it, and
            // the stack's own retirement. All of it BEFORE the announcer
            // is silenced or the restorations would never be spoken —
            // closing the tab a mode is running in is exactly the
            // departure M4 names, and a departure still held from a
            // failed commit owes a sentence too (contract C7).
            //
            // FALLIBLE, and retirement is not allowed to depend on it: a
            // restoration effect is host code and can fault. Without
            // this guard the announcer below was never silenced, so a
            // coalesced line stayed queued on a document that no longer
            // exists and spoke ~200 ms later — the A5 defect reached
            // from the mode side — and the handle below was never
            // closed either.
            try
            {
                Modes.Shutdown();
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                HostLog.Write(HostDiagnosticEvent.CanvasModeTeardownFailed, exception);
            }
            WhereAmIText = null;
            // Every retirement route reaches here — the release sweep,
            // the retarget, the vault-close drain — so this is the one
            // place the announcer has to be silenced (contract A5): a
            // coalesced line queued on a dying document would otherwise
            // fire ~200 ms later and speak about a surface that no
            // longer exists.
            _announcer.Shutdown();
            // BOTH durable requests, because they are twins and a
            // retirement that dropped one of them was the shape this
            // review found.
            FocusRequest = null;
            FilterFocusRequest = null;
        }
        finally
        {
            if (CanvasLeaseTransfer.Terminalize(_slot) is { } unnamed)
            {
                if (IsSynchronousForTests)
                {
                    CloseGuarded(unnamed);
                }
                else
                {
                    _asyncClose = Task.Run(() => CloseGuarded(unnamed));
                }
            }
        }
    }

    private static void CloseGuarded(CanvasHandleLease lease)
    {
        try
        {
            _ = lease.Close();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
        {
            // Teardown race, or a panic out of the FFI — either way the
            // attempt is on the record, and a faulted close task here
            // would fault the vault-close drain over a handle that is
            // already unusable. PanicException is a UniffiException,
            // not a VaultException, which is why the filter is the
            // survivable set rather than the narrow pair (T3 review).
        }
    }

    /// <summary>Every native handle this document could still hold is
    /// closed: the terminal close, and any tracked delivery whose finally
    /// closes a handle it opened mid-flight — the drain the workspace
    /// awaits before the session disposes (INV-2).</summary>
    internal Task WhenHandleClosed() =>
        Task.WhenAll(_asyncClose ?? Task.CompletedTask, WhenWorkDrained());

    /// <summary>The pipeline's view of this document's session: the five
    /// FFI calls, and the two pieces of host prose the pipeline
    /// publishes on the document's behalf (contract A3's states and
    /// messages).</summary>
    private sealed class SessionSource(CanvasDocumentViewModel document) : ICanvasLoadSource
    {
        public CanvasOpenInfo Open() => document._session.OpenCanvas(document.Path);

        public void Close(ulong handle) => document._session.CloseCanvas(handle);

        public CanvasOutlineRow[] Outline(ulong handle) => document._session.CanvasOutline(handle);

        public CanvasTableRow[] TableRows(ulong handle) =>
            document._session.CanvasTableRows(handle);

        public CanvasScene Scene(ulong handle) => document._session.CanvasScene(handle);

        public CanvasLoadFailure FailureFor(Exception exception) =>
            document._retargetedFrom is { } from
                ? new(
                    CanvasLoadState.RetargetAbsent,
                    CanvasPhrase.RetargetAbsent(from, document.Path, exception))
                : new(CanvasLoadState.Failed, CanvasPhrase.OpenFailed(document.Path, exception));

        public CanvasLoadFailure ParseError(IReadOnlyList<CanvasLoadWarning> warnings) =>
            new(CanvasLoadState.ParseError, ParseErrorDetail(warnings));
    }

    /// <summary>§E TE-11 (ED-1, D-6 adopted): the history verbs.
    /// Ctrl+Z / Ctrl+Y through the navigator ladder, the palette's
    /// siblings by the same two-phase checkout: the OFFERED entry is
    /// snapshotted, checked out under the gate, its inverse applied
    /// through the one funnel, and the receipt crosses to the opposite
    /// stack - a raced mutation displaces the snapshot and the entry
    /// returns exactly where it was (IE-9).</summary>
    public void CanvasUndo() => RunHistory(redo: false);

    /// <summary>The redo twin - §E ED-1's Ctrl+Y, no alias.</summary>
    public void CanvasRedo() => RunHistory(redo: true);

    private void RunHistory(bool redo)
    {
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        CanvasHistorySnapshot? snapshot =
            redo ? UndoStack.SnapshotRedo() : UndoStack.SnapshotUndo();
        if (snapshot is null)
        {
            // Quarantined or empty: the OFFERED read is the gate
            // (basis quarantine, TE-4), and an unoffered stack has
            // nothing true to run - the status arm says so.
            Speak(new CanvasA11yEvent.CanvasStatus(
                redo
                    ? new CanvasStatusNote.NothingToRedo()
                    : new CanvasStatusNote.NothingToUndo()));
            return;
        }
        var operation = new CanvasMutationOperation(
            new CanvasOperationId(redo ? "redo" : "undo"),
            this,
            Selection.Selected,
            basis,
            CanvasMutationEffect.KeepSelection);
        _ = Funnel.ApplyHistory(operation, snapshot, redo);
    }

    /// <summary>§E TE-11c (E8a/E2): the verbs' no-basis
    /// short-circuit SPEAKS the same typed refusal the funnel's
    /// ladder announces - one shared derivation, so the guard and
    /// the admission can never say different things.</summary>

    /// <summary>§F TF-1: the basis a mode-entry operation mints
    /// against - null while nothing is loaded, and the entry then
    /// refuses with the not-ready sentence.</summary>
    internal CanvasLoaded? CurrentLoadedForModeEntry => _slot.Current.Loaded;

    /// <summary>§F TF-3: the session the mode's reads run
    /// against - the navigator's steps query overlap and the
    /// relative description under the lease.</summary>
    internal VaultSession SessionForModeEntry => _session;

    /// <summary>§F TF-1: the navigator's no-basis entry refusal
    /// speaks the same shared derivation the guards use.</summary>
    internal void SpeakModeEntryNotReady() => SpeakNotReady();

    /// <summary>§F TF-2 (F1): the active mode's transient, or null.
    /// The renderer reads it; the outline and table never do.</summary>
    internal CanvasTransientHolder? Transient { get; private set; }

    /// <summary>§F TF-5 (F10): the ONE aggregate mode-visible
    /// observable. Fired exactly once per transition — entry after
    /// the machine entered AND the holder installed, teardown after
    /// both cleared, and each step after the rects moved — so an
    /// observer can never see active-without-overlay or
    /// idle-with-overlay.</summary>
    internal event Action? ModeVisibleChanged;

    /// <summary>§F TF-5: the navigator's step notify — the rects
    /// moved in place; the observable is the change.</summary>
    internal void NotifyTransientChanged() => ModeVisibleChanged?.Invoke();

    /// <summary>§F TF-2: install the captured holder — entry's
    /// last step, after the preflight admitted and the machine
    /// entered.</summary>
    internal void InstallTransient(CanvasTransientHolder holder)
    {
        ArgumentNullException.ThrowIfNull(holder);
        Transient = holder;
        ModeVisibleChanged?.Invoke();
    }

    /// <summary>§F TF-2: discard — cancel's arm (no backend call)
    /// and every F1a teardown.</summary>
    internal void DiscardTransient()
    {
        bool held = Transient is not null;
        Transient = null;
        if (held)
        {
            ModeVisibleChanged?.Invoke();
        }
    }

    /// <summary>§F TF-2 (F1a): the displacement watcher. A publish
    /// whose LOADED reference differs from the transient's identity
    /// — and that is not the mode's own completion in flight — ends
    /// the mode through the machine's cancel: the transient is a
    /// guess about rows that moved, so it discards, the suspended
    /// or pending marks are forgotten, and the restoration sentence
    /// is the cancel's own. The own-commit exemption (IF-2): while
    /// a mode commit is PENDING, the funnel's completion is the one
    /// arbiter — its refresh publishes a new Loaded and must not
    /// cancel the mode it is completing.</summary>
    private void WatchTransientDisplacement(CanvasPublication current)
    {
        if (Transient is not { } transient
            || ReferenceEquals(current.Loaded, transient.Identity)
            || Modes.HasPendingCommitForTests)
        {
            return;
        }
        DiscardTransient();
        _ = Modes.Cancel();
    }

    private void SpeakNotReady() =>
        Speak(new CanvasA11yEvent.CanvasMutationRefused(
            CanvasMutationFunnel.NotReadyReason(_slot.Current)));

    /// <summary>§E TE-5b, the first funnel verb: a text card at
    /// core's placement (anchor = the selection, defaults from
    /// `canvas_constants`, no hint), landing selected. Preparation
    /// runs under the gate and the lease; the confirmation speaks
    /// core's relative phrase.</summary>
    public void CanvasNewCard()
    {
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("new card"),
            this,
            Selection.Selected,
            basis,
            CanvasMutationEffect.SelectCreated);
        CanvasRelativeDesc? relative = null;
        _ = Funnel.Apply(
            operation,
            handle =>
            {
                CanvasConstants constants = SlateUniffiMethods.CanvasConstants();
                CanvasPlacement placement = _session.CanvasPlaceNew(
                    handle,
                    operation.Anchor,
                    constants.DefaultCardW,
                    constants.DefaultCardH,
                    null,
                    []);
                relative = placement.Relative;
                string id = SlateUniffiMethods.CanvasNewId();
                operation.CreatedId = id;
                return new CanvasAction(
                    "create card",
                    [
                        new CanvasOp.CreateNode(
                            id,
                            new CanvasNodeContent.Text(string.Empty),
                            placement.X,
                            placement.Y,
                            constants.DefaultCardW,
                            constants.DefaultCardH,
                            null),
                    ]);
            },
            "create card",
            confirm: () => new CanvasA11yEvent.CanvasCreated(
                "text", "Untitled", relative!));
    }

    /// <summary>§E TE-5b: the editor's commit — one SetNodeContent
    /// when the text changed; the no-change arm is the editor task's
    /// (TE-7), which owns the seed comparison.</summary>
    public void CanvasCommitCardEdit(string nodeId, string newText)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        ArgumentNullException.ThrowIfNull(newText);
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("edit card"),
            this,
            nodeId,
            basis,
            CanvasMutationEffect.KeepSelection);
        _ = Funnel.Apply(
            operation,
            _ => new CanvasAction(
                $"edit \"{TitleOf(nodeId)}\"",
                [new CanvasOp.SetNodeContent(nodeId, new CanvasNodeContent.Text(newText))]),
            $"edit \"{TitleOf(nodeId)}\"",
            confirm: () => new CanvasA11yEvent.CanvasCardUpdated(TitleOf(nodeId)));
    }

    /// <summary>§E TE-5b: the delete verb's CARD arm — mac's
    /// clear-selection behavior typed in the effect (IE-26); the
    /// group's Ungroup/Cancel confirmation and the connection arm
    /// ride the next slice with their tables.</summary>
    public void CanvasDeleteSelection()
    {
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        if (Selection.Selected is not { } selected
            || !_rows.TryGetValue(selected, out CanvasOutlineRow? row))
        {
            Speak(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.NothingSelected()));
            return;
        }
        if (row.Kind == "group")
        {
            // UNREACHABLE from the surfaces (§E TE-11c): the grid's
            // Delete gates on kind, the context-menu plan omits Delete
            // on groups (ED-3 - the group's one removal is Ungroup),
            // and the chord path seats through the same rows. A guard,
            // not a cell.
            return;
        }
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("delete card"),
            this,
            selected,
            basis,
            CanvasMutationEffect.ClearSelection);
        _ = Funnel.Apply(
            operation,
            _ => new CanvasAction(
                $"delete \"{row.Title}\"",
                [new CanvasOp.DeleteNode(selected)]),
            $"delete \"{row.Title}\"",
            confirm: () => new CanvasA11yEvent.CanvasDeleted(
                new CanvasDeleteTarget.Card(row.Kind, row.Title),
                _verbosity(),
                CanvasPhrase.UndoChord));
    }

    private string TitleOf(string nodeId) =>
        _rows.TryGetValue(nodeId, out CanvasOutlineRow? row) ? row.Title : nodeId;

    /// <summary>§E TE-5c: New Group — the label arrives as a
    /// parameter (the prompt sheet is TE-7's); geometry from core's
    /// group defaults at the selection anchor.</summary>
    public void CanvasNewGroup(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("new group"), this, Selection.Selected,
            basis, CanvasMutationEffect.SelectCreated);
        CanvasRelativeDesc? relative = null;
        _ = Funnel.Apply(
            operation,
            handle =>
            {
                CanvasConstants constants = SlateUniffiMethods.CanvasConstants();
                CanvasPlacement placement = _session.CanvasPlaceNew(
                    handle, operation.Anchor,
                    constants.DefaultGroupW, constants.DefaultGroupH, null, []);
                relative = placement.Relative;
                string id = SlateUniffiMethods.CanvasNewId();
                operation.CreatedId = id;
                return new CanvasAction(
                    $"create group \"{label}\"",
                    [
                        new CanvasOp.CreateGroup(
                            id, label, placement.X, placement.Y,
                            constants.DefaultGroupW, constants.DefaultGroupH, null),
                    ]);
            },
            $"create group \"{label}\"",
            confirm: () => new CanvasA11yEvent.CanvasCreated("group", label, relative!));
    }

    /// <summary>§E TE-5c: Rename Group — the real op (IE-23's round-1
    /// catch: never SetNodeContent on a group).</summary>
    public void CanvasRenameGroup(string groupId, string label)
    {
        ArgumentNullException.ThrowIfNull(groupId);
        ArgumentNullException.ThrowIfNull(label);
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("rename group"), this, groupId,
            basis, CanvasMutationEffect.KeepSelection);
        _ = Funnel.Apply(
            operation,
            _ => new CanvasAction(
                $"rename group to \"{label}\"",
                [new CanvasOp.RenameGroup(groupId, label)]),
            $"rename group to \"{label}\"",
            confirm: () => new CanvasA11yEvent.CanvasRenamedGroup(label));
    }

    /// <summary>§E TE-5c: the delete verb's GROUP arm — the algebra's
    /// one group removal (Ungroup: frame and incident edges go, cards
    /// stay; ED-3's confirmation sheet is TE-7's, this is its commit
    /// path).</summary>
    public void CanvasUngroup(string groupId)
    {
        ArgumentNullException.ThrowIfNull(groupId);
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        if (!_rows.TryGetValue(groupId, out CanvasOutlineRow? row))
        {
            Speak(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.NotAGroup()));
            return;
        }
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("ungroup"), this, groupId,
            basis, CanvasMutationEffect.ClearSelection);
        _ = Funnel.Apply(
            operation,
            _ => new CanvasAction(
                $"ungroup \"{row.Title}\"",
                [new CanvasOp.Ungroup(groupId)]),
            $"ungroup \"{row.Title}\"",
            confirm: () => new CanvasA11yEvent.CanvasDeleted(
                new CanvasDeleteTarget.Group(row.Title),
                _verbosity(),
                CanvasPhrase.UndoChord));
    }

    /// <summary>§E TE-5c: Move into Group — the typed three-outcome
    /// preparation (the placement table): Placed commits; TooSmall
    /// commits only when core's overlap check clears its inset; Full
    /// — and an occupied inset — refuse audibly with the group's
    /// label, from PREPARATION, inside the gate; nothing
    /// half-happens (the mac twin, verb for verb).</summary>
    public void CanvasMoveIntoGroup(string groupId)
    {
        ArgumentNullException.ThrowIfNull(groupId);
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        if (Selection.Selected is not { } selected
            || basis.Population.SceneByNode.GetValueOrDefault(selected)
                is not { } node)
        {
            Speak(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.NothingSelected()));
            return;
        }
        if (!_rows.TryGetValue(groupId, out CanvasOutlineRow? group))
        {
            Speak(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.NotAGroup()));
            return;
        }
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("move into group"), this, selected,
            basis, CanvasMutationEffect.KeepSelection);
        _ = Funnel.Apply(
            operation,
            handle =>
            {
                CanvasInsideGroupPlacement placement = _session.CanvasPlaceInsideGroup(
                    handle, groupId, node.Width, node.Height, [selected]);
                double x;
                double y;
                switch (placement)
                {
                    case CanvasInsideGroupPlacement.Placed placed:
                        (x, y) = (placed.X, placed.Y);
                        break;
                    case CanvasInsideGroupPlacement.TooSmall inset
                        when _session.CanvasCheckOverlap(
                            handle,
                            new CanvasRect(inset.X, inset.Y, node.Width, node.Height),
                            [selected]).Length == 0:
                        (x, y) = (inset.X, inset.Y);
                        break;
                    default:
                        Speak(new CanvasA11yEvent.CanvasBlocked(
                            new CanvasBlockedReason.NoFreeSpaceInGroup(group.Title)));
                        return null;
                }
                return new CanvasAction(
                    $"move \"{TitleOf(selected)}\" into \"{group.Title}\"",
                    [
                        new CanvasOp.UpdateNodeGeometry(
                            selected, x, y, node.Width, node.Height),
                    ]);
            },
            "move into group",
            confirm: () => new CanvasA11yEvent.CanvasMovedIntoGroup(group.Title));
    }

    /// <summary>§E TE-5c: Set Color — a preset "1".."6", a validated
    /// hex, or null to clear (the picker's field owns the invalid-hex
    /// error surface; an invalid value never reaches the funnel).</summary>
    public void CanvasSetColor(string? color)
    {
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        if (Selection.Selected is not { } selected)
        {
            Speak(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.NothingSelected()));
            return;
        }
        if (color is not null && !IsCanvasColor(color))
        {
            // UNREACHABLE from the surfaces (§E TE-11c): every color
            // arrives from the fixed palette rows. A guard, not a cell.
            return;
        }
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("set color"), this, selected,
            basis, CanvasMutationEffect.KeepSelection);
        _ = Funnel.Apply(
            operation,
            _ => new CanvasAction(
                color is null
                    ? $"clear color of \"{TitleOf(selected)}\""
                    : $"color \"{TitleOf(selected)}\"",
                [new CanvasOp.SetNodeColor(selected, color)]),
            "set color",
            confirm: () =>
            {
                CanvasSceneNode? colored =
                    _slot.Current.Population?.SceneByNode.GetValueOrDefault(selected);
                return new CanvasA11yEvent.CanvasColorSet(
                    TitleOf(selected),
                    colored?.Color is { } value
                        ? value.Length == 1
                            ? new CanvasColor.Preset(byte.Parse(value))
                            : new CanvasColor.Hex(value)
                        : null);
            });
    }

    /// <summary>§E TE-5c: Connect — one edge from the picker's
    /// answer, sides defaulted by core over the two rects (0b-3),
    /// mac's end styles (plain source, arrow target).</summary>
    public void CanvasConnect(string fromId, string toId, string? label)
    {
        ArgumentNullException.ThrowIfNull(fromId);
        ArgumentNullException.ThrowIfNull(toId);
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        if (basis.Population.SceneByNode.GetValueOrDefault(fromId) is not { } from
            || basis.Population.SceneByNode.GetValueOrDefault(toId) is not { } to)
        {
            Speak(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.NothingSelected()));
            return;
        }
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("connect"), this, fromId,
            basis, CanvasMutationEffect.KeepSelection);
        _ = Funnel.Apply(
            operation,
            _ =>
            {
                CanvasSidePair sides = SlateUniffiMethods.CanvasAutoSides(
                    new CanvasRect(from.X, from.Y, from.Width, from.Height),
                    new CanvasRect(to.X, to.Y, to.Width, to.Height));
                return new CanvasAction(
                    $"connect \"{TitleOf(fromId)}\" to \"{TitleOf(toId)}\"",
                    [
                        new CanvasOp.AddEdge(
                            SlateUniffiMethods.CanvasNewId(),
                            fromId, sides.From, toId, sides.To,
                            CanvasEndStyle.None, CanvasEndStyle.Arrow,
                            label, null),
                    ]);
            },
            "connect",
            confirm: () => new CanvasA11yEvent.CanvasConnected(
                TitleOf(fromId), TitleOf(toId), label));
    }

    /// <summary>§E TE-5c: Edit Connection — label and the four
    /// end-style mappings ONLY; sides and color PRESERVED from the
    /// scene edge (the mac twin's rule: never recompute what the
    /// author placed).</summary>
    public void CanvasEditConnection(
        string edgeId, string? label, CanvasConnectionDirection direction)
    {
        ArgumentNullException.ThrowIfNull(edgeId);
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        CanvasSceneEdge? edge =
            basis.Population.SceneEdges.FirstOrDefault(e => e.EdgeId == edgeId);
        if (edge is null)
        {
            Speak(new CanvasA11yEvent.CanvasStatus(new CanvasStatusNote.NoConnections()));
            return;
        }
        (CanvasEndStyle fromEnd, CanvasEndStyle toEnd) = direction switch
        {
            CanvasConnectionDirection.ToTarget =>
                (CanvasEndStyle.None, CanvasEndStyle.Arrow),
            CanvasConnectionDirection.Both =>
                (CanvasEndStyle.Arrow, CanvasEndStyle.Arrow),
            CanvasConnectionDirection.None =>
                (CanvasEndStyle.None, CanvasEndStyle.None),
            _ => (CanvasEndStyle.Arrow, CanvasEndStyle.None),
        };
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("edit connection"), this, edgeId,
            basis, CanvasMutationEffect.KeepSelection);
        _ = Funnel.Apply(
            operation,
            _ => new CanvasAction(
                "edit connection",
                [
                    new CanvasOp.UpdateEdge(
                        edgeId, edge.FromSide, edge.ToSide,
                        fromEnd, toEnd, label, edge.Color),
                ]),
            "edit connection",
            confirm: () => new CanvasA11yEvent.CanvasConnectionUpdated(label));
    }

    /// <summary>§E TE-5c: Delete Connection — the lookup runs BEFORE
    /// the apply and a miss refuses AUDIBLY (0a-2's rule: the mac
    /// trailing-space sentence is unreachable by construction).</summary>
    public void CanvasDeleteConnection(string edgeId)
    {
        ArgumentNullException.ThrowIfNull(edgeId);
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        if (basis.Population.SceneEdges.All(e => e.EdgeId != edgeId))
        {
            Speak(new CanvasA11yEvent.CanvasStatus(new CanvasStatusNote.NoConnections()));
            return;
        }
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("delete connection"), this, edgeId,
            basis, CanvasMutationEffect.KeepSelection);
        _ = Funnel.Apply(
            operation,
            _ => new CanvasAction(
                "delete connection",
                [new CanvasOp.DeleteEdge(edgeId)]),
            "delete connection",
            confirm: () =>
            {
                CanvasSceneEdge gone = basis.Population.SceneEdges
                    .First(e => e.EdgeId == edgeId);
                return new CanvasA11yEvent.CanvasDeleted(
                    new CanvasDeleteTarget.Connection(
                        (gone.FromArrow, gone.ToArrow) switch
                        {
                            (true, true) => CanvasEdgeDirection.Bidirectional,
                            (false, false) => CanvasEdgeDirection.Undirected,
                            (false, true) => CanvasEdgeDirection.Outgoing,
                            (true, false) => CanvasEdgeDirection.Incoming,
                        },
                        TitleOf(gone.ToNode),
                        gone.Label),
                    _verbosity(),
                    CanvasPhrase.UndoChord);
            });
    }

    /// <summary>§E TE-5c: Add Note / Add Media — a file card at
    /// core's placement; the vault picker (TE-6) supplies the path,
    /// kinds and labels are core's (`media_class` via the row).</summary>
    public void CanvasAddFileCard(string path, string? subpath)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("add file card"), this, Selection.Selected,
            basis, CanvasMutationEffect.SelectCreated);
        CanvasRelativeDesc? relative = null;
        _ = Funnel.Apply(
            operation,
            handle =>
            {
                CanvasConstants constants = SlateUniffiMethods.CanvasConstants();
                CanvasPlacement placement = _session.CanvasPlaceNew(
                    handle, operation.Anchor,
                    constants.DefaultCardW, constants.DefaultCardH, null, []);
                relative = placement.Relative;
                string id = SlateUniffiMethods.CanvasNewId();
                operation.CreatedId = id;
                return new CanvasAction(
                    $"add \"{path}\"",
                    [
                        new CanvasOp.CreateNode(
                            id, new CanvasNodeContent.File(path, subpath),
                            placement.X, placement.Y,
                            constants.DefaultCardW, constants.DefaultCardH, null),
                    ]);
            },
            $"add \"{path}\"",
            confirm: () => new CanvasA11yEvent.CanvasCreated(
                RowKindOf(operation.CreatedId),
                PublishedTitleOf(operation.CreatedId),
                relative!));
    }

    /// <summary>§E TE-5c: Add Link Card — the URL prompt's commit;
    /// an unparseable URL refuses audibly and never reaches the
    /// funnel (the NotAUrl arm).</summary>
    public void CanvasAddLinkCard(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed)
            || parsed.Scheme is not ("http" or "https"))
        {
            Speak(new CanvasA11yEvent.CanvasBlocked(new CanvasBlockedReason.NotAUrl()));
            return;
        }
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("add link card"), this, Selection.Selected,
            basis, CanvasMutationEffect.SelectCreated);
        CanvasRelativeDesc? relative = null;
        _ = Funnel.Apply(
            operation,
            handle =>
            {
                CanvasConstants constants = SlateUniffiMethods.CanvasConstants();
                CanvasPlacement placement = _session.CanvasPlaceNew(
                    handle, operation.Anchor,
                    constants.DefaultCardW, constants.DefaultCardH, null, []);
                relative = placement.Relative;
                string id = SlateUniffiMethods.CanvasNewId();
                operation.CreatedId = id;
                return new CanvasAction(
                    "add link card",
                    [
                        new CanvasOp.CreateNode(
                            id, new CanvasNodeContent.Link(url),
                            placement.X, placement.Y,
                            constants.DefaultCardW, constants.DefaultCardH, null),
                    ]);
            },
            "add link card",
            confirm: () => new CanvasA11yEvent.CanvasCreated(
                "link", PublishedTitleOf(operation.CreatedId), relative!));
    }

    /// <summary>§E TE-5c: Locate File — repoint a missing-target file
    /// card (the card stayed navigable throughout, t0 §5); the vault
    /// picker (TE-6) supplies the replacement path.</summary>
    public void CanvasLocateFile(string nodeId, string newPath)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        ArgumentNullException.ThrowIfNull(newPath);
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        if (!_rows.ContainsKey(nodeId))
        {
            Speak(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.NothingSelected()));
            return;
        }
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("locate file"), this, nodeId,
            basis, CanvasMutationEffect.KeepSelection);
        _ = Funnel.Apply(
            operation,
            _ => new CanvasAction(
                $"repoint \"{TitleOf(nodeId)}\"",
                [
                    new CanvasOp.SetNodeContent(
                        nodeId, new CanvasNodeContent.File(newPath, null)),
                ]),
            $"repoint \"{TitleOf(nodeId)}\"",
            confirm: () => new CanvasA11yEvent.CanvasCardRetargeted(
                TitleOf(nodeId), newPath));
    }

    /// <summary>§E TE-7: the editor factory — one locked seed read
    /// (text + basis together, IE-4), refusing a non-text card and an
    /// unreadable one with mac's arms.</summary>
    internal CanvasCardEditorViewModel? OpenCardEditor(string nodeId)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        if (!_rows.TryGetValue(nodeId, out CanvasOutlineRow? row))
        {
            Speak(new CanvasA11yEvent.CanvasStatus(new CanvasStatusNote.NothingSelected()));
            return null;
        }
        if (row.Kind != "text")
        {
            Speak(new CanvasA11yEvent.CanvasStatus(new CanvasStatusNote.NotATextCard()));
            return null;
        }
        CanvasEditorSeed? seed = null;
        if (_slot.Current.Lease is { } lease)
        {
            _ = lease.Invoke(
                () =>
                {
                    CanvasPublication now = _slot.Current;
                    return !now.Retired && now.Names(lease);
                },
                handle => seed = _session.CanvasEditorSeed(handle, nodeId));
        }
        if (seed is null)
        {
            Speak(new CanvasA11yEvent.CanvasBlocked(
                new CanvasBlockedReason.CardTextUnreadable()));
            return null;
        }
        return new CanvasCardEditorViewModel(this, nodeId, row.Title, seed);
    }

    /// <summary>§E TE-8: a row surface asks for the editor. The sheet
    /// lives on the WORKSPACE (the modal censuses' convention), so the
    /// document raises and the workspace opens — the row is seated
    /// first, the mac RowAction contract's acts-on-its-row rule.</summary>
    internal event Action<string>? CardEditorRequested;

    internal void RequestCardEditor(string nodeId)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        SeatSelectionSilently(nodeId);
        CardEditorRequested?.Invoke(nodeId);
    }

    /// <summary>The editor model's announce door — the same relay the
    /// verbs use, named so the editor's arms read as the M8 carve-out
    /// they are.</summary>
    internal void SpeakForEditor(CanvasA11yEvent @event) => Speak(@event);

    /// <summary>§F TF-7 (F5/F6): open a card picker — the request
    /// captures IF-19's immutable context in ONE lease read (the
    /// TF-2 capture, reading-ordered, never silent), and the model's
    /// proximity anchor is the PRIMARY MOVER (IF-20), the moving set
    /// excluded. The TF-8 sheet consumes the event; refusals speak
    /// here.</summary>
    internal event Action<CanvasCardPickerRequest, CanvasCardPickerModel>?
        CardPickerRequested;

    internal void OpenCardPicker(CanvasCardPickerPurpose purpose)
    {
        if (_slot.Current.Loaded is not { } loaded)
        {
            SpeakNotReady();
            return;
        }
        // §F TF-8 (F7): connect stages from the SELECTED card only —
        // the marked set is placement's rigid unit, never an edge's.
        var members = purpose != CanvasCardPickerPurpose.ConnectTo
            && Selection.Marked.Count > 0
            ? new List<string>(Selection.Marked)
            : Selection.Selected is { } one ? [one] : [];
        if (members.Count == 0)
        {
            Speak(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.NothingSelected()));
            return;
        }
        CanvasTransientHolder? captured = CanvasTransientHolder.TryCapture(
            _session, loaded, members, isResize: false);
        if (captured is null)
        {
            Speak(new CanvasA11yEvent.CanvasActionFailed(
                CanvasFailedAction.Placement, "open the picker"));
            return;
        }
        var request = new CanvasCardPickerRequest(
            purpose,
            captured.Ids,
            captured.Originals,
            loaded);
        LastCardPickerRequestForTests = request;
        CardPickerRequested?.Invoke(
            request,
            BuildCardPickerModel(captured.Ids[0], [.. captured.Ids]));
    }

    internal CanvasCardPickerRequest? LastCardPickerRequestForTests
    {
        get;
        private set;
    }

    /// <summary>§F TF-7 (F5/F6): route a completed pick. The request
    /// re-validates FIRST — a reload between open and confirm makes
    /// the context a guess, and a guess refuses PickDifferentTarget
    /// (IF-19); a target inside the moving set refuses
    /// PickOutsideMovingSet with the picker's state kept.</summary>
    internal bool HandleCardPick(CanvasCardPickerRequest request, string target)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(target);
        if (_slot.Current.Loaded is not { } loaded
            || !ReferenceEquals(loaded, request.Identity))
        {
            Speak(new CanvasA11yEvent.CanvasStatus(
                new CanvasStatusNote.PickDifferentTarget()));
            return false;
        }
        if (request.Moving.Contains(target))
        {
            // §F TF-8 (IF-26): connect's self-pick is a TARGET
            // problem, mac's sentence; placement's is a moving-set
            // problem, F5's.
            Speak(new CanvasA11yEvent.CanvasStatus(
                request.Purpose == CanvasCardPickerPurpose.ConnectTo
                    ? new CanvasStatusNote.PickDifferentTarget()
                    : new CanvasStatusNote.PickOutsideMovingSet()));
            return false;
        }
        if (request.Purpose == CanvasCardPickerPurpose.ConnectTo)
        {
            // §F TF-8 (F7): confirm STAGES; the label step runs before
            // any apply exists.
            var stage = new CanvasConnectStage(
                request.Moving[0],
                RowFor(request.Moving[0])?.Title ?? "card",
                target,
                RowFor(target)?.Title ?? "card",
                request.Identity);
            ConnectPromptRequested?.Invoke(stage);
            return true;
        }
        switch (request.Purpose)
        {
            case CanvasCardPickerPurpose.PlaceBelow:
                CanvasPlaceRelative(request, target, CanvasPlaceDirection.Below);
                break;
            case CanvasCardPickerPurpose.PlaceRightOf:
                CanvasPlaceRelative(request, target, CanvasPlaceDirection.RightOf);
                break;
            case CanvasCardPickerPurpose.PlaceAbove:
                CanvasPlaceRelative(request, target, CanvasPlaceDirection.Above);
                break;
            case CanvasCardPickerPurpose.PlaceLeftOf:
                CanvasPlaceRelative(request, target, CanvasPlaceDirection.LeftOf);
                break;
            default:
                CanvasAlignWith(request, target);
                break;
        }

        return true;
    }

    /// <summary>§F TF-8 (F7): the staged apply — ONCE, inside
    /// prepare-under-the-gate. Both endpoints re-resolve against the
    /// held handle (either vanished refuses PickDifferentTarget, the
    /// STAGE kept — it is immutable and the operation wrote nothing);
    /// the self gate stands belt-and-braces under the pick's; sides
    /// come from core's CanvasAutoSides over the RECTS; every
    /// generated AddEdge parameter is spelled. An empty submitted
    /// label normalizes to null (IF-27) — Enter-skips and
    /// click-with-empty-field serialize identically.</summary>
    internal void CanvasConnect(CanvasConnectStage stage, string? label)
    {
        ArgumentNullException.ThrowIfNull(stage);
        string? clean = string.IsNullOrEmpty(label) ? null : label;
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("connect"),
            this,
            Selection.Selected,
            stage.Identity,
            CanvasMutationEffect.KeepSelection);
        _ = Funnel.Apply(
            operation,
            handle =>
            {
                try
                {
                    CanvasPopulation? population = _slot.Current.Population;
                    CanvasSceneNode? origin =
                        population?.SceneByNode.GetValueOrDefault(stage.OriginId);
                    CanvasSceneNode? target =
                        population?.SceneByNode.GetValueOrDefault(stage.TargetId);
                    if (origin is null || target is null
                        || stage.OriginId == stage.TargetId)
                    {
                        Speak(new CanvasA11yEvent.CanvasStatus(
                            new CanvasStatusNote.PickDifferentTarget()));
                        return null;
                    }
                    CanvasSidePair sides = SlateUniffiMethods.CanvasAutoSides(
                        new CanvasRect(
                            origin.X, origin.Y, origin.Width, origin.Height),
                        new CanvasRect(
                            target.X, target.Y, target.Width, target.Height));
                    return new CanvasAction(
                        $"connect \"{stage.OriginTitle}\" to \"{stage.TargetTitle}\"",
                        [
                            new CanvasOp.AddEdge(
                                SlateUniffiMethods.CanvasNewId(),
                                stage.OriginId,
                                sides.From,
                                stage.TargetId,
                                sides.To,
                                CanvasEndStyle.None,
                                CanvasEndStyle.Arrow,
                                clean,
                                null),
                        ]);
                }
                catch (VaultException)
                {
                    Speak(new CanvasA11yEvent.CanvasActionFailed(
                        CanvasFailedAction.CanvasAction, "connect"));
                    return null;
                }
            },
            "connect",
            confirm: () => new CanvasA11yEvent.CanvasConnected(
                stage.OriginTitle, stage.TargetTitle, clean));
    }

    /// <summary>§F TF-8 (F7): the staged-prompt seam — the workspace
    /// presents the label step; tests tap it directly.</summary>
    internal event Action<CanvasConnectStage>? ConnectPromptRequested;

    /// <summary>§F TF-8 (FD-4): the carried Rename Group prompt — the
    /// COMMIT verb shipped in §E; this is its front door.</summary>
    internal event Action<string, string>? GroupRenameRequested;

    internal void RequestGroupRename(string groupId)
    {
        ArgumentNullException.ThrowIfNull(groupId);
        GroupRenameRequested?.Invoke(groupId, RowFor(groupId)?.Title ?? "group");
    }

    /// <summary>§F TF-8 (FD-4): the carried Set Color prompt — acts
    /// on the selection, mac's shape.</summary>
    internal event Action? SetColorRequested;

    internal void RequestSetColor() => SetColorRequested?.Invoke();

    /// <summary>§F TF-7 (F5): picker-anchored engine placement, one
    /// action end to end. EVERYTHING against the handle runs inside
    /// prepare-under-the-gate: existence, the engine query, the op —
    /// each refusal typed, each null action writing nothing. The set
    /// routes CanvasPlaceSet with boxes IN THE SET'S READING ORDER
    /// and the positional Origins map back by that same order; a
    /// cardinality mismatch refuses WHOLE (IF-21). Verb identity is
    /// Placement (IF-22).</summary>
    internal void CanvasPlaceRelative(
        CanvasCardPickerRequest request, string target, CanvasPlaceDirection direction)
    {
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("place relative"),
            this,
            Selection.Selected,
            basis,
            CanvasMutationEffect.KeepSelection);
        bool single = request.Moving.Length == 1;
        string primaryTitle = RowFor(request.Moving[0])?.Title ?? "card";
        CanvasRelativeDesc? relative = null;
        string name = single
            ? $"move \"{primaryTitle}\""
            : $"move {SlateUniffiMethods.CountNoun((ulong)request.Moving.Length, "card", "cards")}";
        _ = Funnel.Apply(
            operation,
            handle =>
            {
                try
                {
                    CanvasPopulation? population = _slot.Current.Population;
                    if (population is null
                        || !population.SceneByNode.ContainsKey(target))
                    {
                        Speak(new CanvasA11yEvent.CanvasStatus(
                            new CanvasStatusNote.PickDifferentTarget()));
                        return null;
                    }
                    foreach (string id in request.Moving)
                    {
                        if (!population.SceneByNode.ContainsKey(id))
                        {
                            Speak(new CanvasA11yEvent.CanvasStatus(
                                new CanvasStatusNote.NothingSelected()));
                            return null;
                        }
                    }
                    if (single)
                    {
                        CanvasRect box = request.Rects[request.Moving[0]];
                        CanvasPlacement placement = _session.CanvasPlaceNew(
                            handle,
                            target,
                            box.Width,
                            box.Height,
                            direction,
                            [.. request.Moving]);
                        relative = placement.Relative;
                        return new CanvasAction(
                            name,
                            [
                                new CanvasOp.UpdateNodeGeometry(
                                    request.Moving[0],
                                    placement.X,
                                    placement.Y,
                                    box.Width,
                                    box.Height),
                            ]);
                    }
                    CanvasRect[] boxes =
                        [.. request.Moving.Select(id => request.Rects[id])];
                    CanvasSetPlacement set = _session.CanvasPlaceSet(
                        handle, target, boxes, direction, [.. request.Moving]);
                    if (set.Origins.Length != request.Moving.Length)
                    {
                        // IF-21: the positional contract broke — refuse
                        // WHOLE rather than move a subset.
                        Speak(new CanvasA11yEvent.CanvasActionFailed(
                            CanvasFailedAction.Placement, "place the set"));
                        return null;
                    }
                    relative = set.Relative;
                    var ops = new List<CanvasOp>();
                    for (int i = 0; i < request.Moving.Length; i++)
                    {
                        CanvasRect box = request.Rects[request.Moving[i]];
                        ops.Add(new CanvasOp.UpdateNodeGeometry(
                            request.Moving[i],
                            set.Origins[i].X,
                            set.Origins[i].Y,
                            box.Width,
                            box.Height));
                    }
                    return new CanvasAction(name, [.. ops]);
                }
                catch (VaultException)
                {
                    Speak(new CanvasA11yEvent.CanvasActionFailed(
                        CanvasFailedAction.Placement, "place"));
                    return null;
                }
            },
            name,
            confirm: () => single
                ? new CanvasA11yEvent.CanvasCardPlaced(
                    CanvasPlaceVerb.Moved, primaryTitle, relative!)
                : new CanvasA11yEvent.CanvasBulkMoved(
                    (uint)request.Moving.Length, relative!));
    }

    /// <summary>§F TF-7 (F6): the same-axis slot with a TOTAL refusal
    /// table — top edges (FD-2), already-aligned answers NoChanges
    /// writing nothing (the arm mac lacks, added by contract), an
    /// occupied slot refuses AlignWouldOverlap, and the overlap check
    /// runs inside prepare with the write it guards. Verb identity is
    /// Align (IF-22).</summary>
    internal void CanvasAlignWith(CanvasCardPickerRequest request, string target)
    {
        if (_slot.Current.Loaded is not { } basis)
        {
            SpeakNotReady();
            return;
        }
        string mover = request.Moving[0];
        string primaryTitle = RowFor(mover)?.Title ?? "card";
        string targetTitle = RowFor(target)?.Title ?? "card";
        var operation = new CanvasMutationOperation(
            new CanvasOperationId("align with"),
            this,
            Selection.Selected,
            basis,
            CanvasMutationEffect.KeepSelection);
        _ = Funnel.Apply(
            operation,
            handle =>
            {
                try
                {
                    CanvasPopulation? population = _slot.Current.Population;
                    CanvasSceneNode? node =
                        population?.SceneByNode.GetValueOrDefault(mover);
                    CanvasSceneNode? anchor =
                        population?.SceneByNode.GetValueOrDefault(target);
                    if (node is null || anchor is null)
                    {
                        Speak(new CanvasA11yEvent.CanvasStatus(
                            new CanvasStatusNote.PickDifferentTarget()));
                        return null;
                    }
                    if (node.Y == anchor.Y)
                    {
                        Speak(new CanvasA11yEvent.CanvasStatus(
                            new CanvasStatusNote.NoChanges()));
                        return null;
                    }
                    var slot = new CanvasRect(
                        node.X, anchor.Y, node.Width, node.Height);
                    if (_session.CanvasCheckOverlap(handle, slot, [mover])
                        .Length > 0)
                    {
                        Speak(new CanvasA11yEvent.CanvasBlocked(
                            new CanvasBlockedReason.AlignWouldOverlap()));
                        return null;
                    }
                    return new CanvasAction(
                        $"align \"{primaryTitle}\"",
                        [
                            new CanvasOp.UpdateNodeGeometry(
                                mover, node.X, anchor.Y, node.Width, node.Height),
                        ]);
                }
                catch (VaultException)
                {
                    Speak(new CanvasA11yEvent.CanvasActionFailed(
                        CanvasFailedAction.Align, "align"));
                    return null;
                }
            },
            $"align \"{primaryTitle}\"",
            confirm: () => new CanvasA11yEvent.CanvasCardAligned(
                primaryTitle, targetTitle));
    }

    /// <summary>The live population's basis — the editor's commit
    /// compares its seed against THIS (IE-18's re-validation).</summary>
    internal string? PublishedBasis => _slot.Current.Population?.ContentHash;

    /// <summary>§E TE-6: the card picker's rows — CORE's proximity
    /// order over the lease (anchor = the selection; reading-order
    /// ties; groups included), labelled the palette way. The model
    /// only filters; a picker with no handle answers empty.</summary>
    internal CanvasCardPickerModel BuildCardPickerModel(params string[] exclude) =>
        BuildCardPickerModel(Selection.Selected, exclude);

    /// <summary>§F TF-7 (IF-20): the F5 pickers anchor proximity at
    /// the PRIMARY MOVER — the anchor is a parameter, and the §E
    /// callers keep the selection default.</summary>
    internal CanvasCardPickerModel BuildCardPickerModel(
        string? anchor, string[] exclude)
    {
        string[] ordered = [];
        if (_slot.Current.Lease is { } lease)
        {
            _ = lease.Invoke(
                () =>
                {
                    CanvasPublication now = _slot.Current;
                    return !now.Retired && now.Names(lease);
                },
                handle => ordered = _session.CanvasProximityOrder(
                    handle, anchor, exclude));
        }
        return new CanvasCardPickerModel(
            ordered
                .Where(_rows.ContainsKey)
                .Select(id =>
                {
                    CanvasOutlineRow row = _rows[id];
                    string type = row.Kind == "group"
                        ? "Group"
                        : $"{char.ToUpperInvariant(row.Kind[0])}{row.Kind[1..]} card";
                    string container = row.GroupPath.Length > 0
                        ? row.GroupPath[^1]
                        : "canvas";
                    return new CanvasCardPickerRow(
                        id, $"{type} \"{row.Title}\", in {container}");
                }));
    }

    /// <summary>§E TE-6: the vault-file picker's generation — paged to
    /// completion, classified BEFORE the cap (notes = markdown; media
    /// = core's classification over the FFI).</summary>
    internal CanvasVaultFilePickerModel BuildVaultFilePickerModel(bool mediaOnly) =>
        CanvasVaultFilePickerModel.LoadAll(
            cursor => _session.ListFiles(
                FileFilter.All, new Paging(cursor, 128)),
            file => mediaOnly
                ? SlateUniffiMethods.CanvasMediaClass(file.Path) is not null
                : file.IsMarkdown);

    private string PublishedTitleOf(string? nodeId) =>
        PublishedRowOf(nodeId)?.Title ?? nodeId ?? string.Empty;

    private CanvasOutlineRow? PublishedRowOf(string? nodeId) =>
        nodeId is null
            ? null
            : _slot.Current.Population?.Outline
                .FirstOrDefault(row => row.NodeId == nodeId);

    private string RowKindOf(string? nodeId) =>
        PublishedRowOf(nodeId)?.Kind ?? "file";

    /// <summary>Core's color grammar: one preset digit or 3/6-digit
    /// hex with the leading hash.</summary>
    internal static bool IsCanvasColor(string color) =>
        color.Length == 1 && color[0] is >= '1' and <= '6'
        || (color.Length is 4 or 7
            && color[0] == '#'
            && color.Skip(1).All(Uri.IsHexDigit));

    /// <summary>The funnel's two writes-and-reads beyond the load
    /// trio (§E TE-5b) — the one place `canvas_apply` crosses this
    /// document's session.</summary>
    private sealed class MutationSource(CanvasDocumentViewModel document)
        : ICanvasMutationSource
    {
        public CanvasApplyResult Apply(ulong handle, CanvasAction action) =>
            document._session.CanvasApply(handle, action);

        public CanvasEditorSeed CurrentText(ulong handle) =>
            document._session.CanvasCurrentText(handle);
    }

    /// <summary>The machine's one FFI call, through this document's
    /// session — core's <c>canvas_filter</c> (0b-13/0b-14): title, the
    /// kind type word, any one element of the group path, and the
    /// activation target, the needle passed untrimmed.</summary>
    private sealed class FilterSource(CanvasDocumentViewModel document) : ICanvasFilterSource
    {
        public string[] Match(ulong handle, string needle) =>
            document._session.CanvasFilter(handle, needle);
    }
}

/// <summary>Host-composed BANNER and LABEL text only — never
/// announcements (the <c>BasePhrase</c> category: static UI copy is a
/// §W-C label concern, not §W-D vocabulary; contracts A9/A10 name each
/// entry's designation). Wording is mac's verbatim where mac has the
/// sentence.</summary>
internal static class CanvasPhrase
{
    /// <summary>The tree's own accessible name.</summary>
    public const string OutlineName = "Canvas outline";

    /// <summary>The Windows display chord for undo, carried in the
    /// destructive confirmations' hint (t0 §1.3; mac's ⌘Z twin).</summary>
    public const string UndoChord = "Ctrl+Z";

    /// <summary>The surface switcher's accessible name (spec §PR A
    /// Builds, verbatim).</summary>
    public const string SurfaceSwitcherName = "Canvas view";

    public const string Loading = "Opening canvas…";

    /// <summary>Mac's fallback when a parse failure carries no detail
    /// (<c>CanvasDocument.swift</c>, verbatim).</summary>
    public const string NotValidJsonCanvas = "the file is not valid JSON Canvas";

    /// <summary>The footer list holds EVERY load warning, so it is
    /// named for that rather than for the banner's narrower
    /// skipped-entry count.</summary>
    public const string WarningsRegionName = "Canvas load warnings";

    public const string DetailRegionName = "Card text";

    // --- W6-1 PR C: the filter field, its summary, and the Where-am-I
    // panel. The mac label inventory verbatim where mac has the string
    // (§W-C label class, contract C14).

    /// <summary>Mac's <c>accessibilityLabel</c> for the filter field,
    /// verbatim.</summary>
    public const string FilterFieldName = "Filter cards";

    /// <summary>Mac's <c>accessibilityHint</c> for it, with mac's Escape
    /// sentence kept: the Escape rung behaves identically here (t0
    /// M5).</summary>
    public const string FilterFieldHint =
        "Narrows the outline and table by title, type, group, or target. Escape clears.";

    /// <summary>Mac's <c>Button("Clear")</c> and its accessibility label,
    /// verbatim.</summary>
    public const string ClearFilterLabel = "Clear";

    public const string ClearFilterName = "Clear filter";

    /// <summary>The result-summary region's own name. Windows-authored:
    /// mac's summary is an unlabelled caption beside the field, and an
    /// unlabelled region is not readable on demand, which is the whole
    /// point of t0 §3's "result summary element".</summary>
    public const string FilterSummaryName = "Filter results";

    /// <summary>Mac's Where-am-I panel heading, verbatim.</summary>
    public const string WhereAmIHeading = "Where am I?";

    /// <summary>Mac's Close button on that panel, verbatim.</summary>
    public const string WhereAmICloseLabel = "Close";

    /// <summary>
    /// The M6 visible controls. The VISIBLE text is short because the
    /// buttons sit in a header beside the mode's own value; the
    /// ACCESSIBLE name is the mac catalog's verb, so a Voice Control user
    /// says the same words the palette row is called.
    /// </summary>
    public const string ModeCommitLabel = "Commit";

    public const string ModeCancelLabel = "Cancel";

    public const string ModeCommitName = "Commit Mode";

    public const string ModeCancelName = "Cancel Mode";

    /// <summary>
    /// The filter's result summary — mac's sentence, verbatim, including
    /// its verb agreement ("1 of 40 cards matches", "3 of 40 cards
    /// match").
    /// </summary>
    /// <remarks>
    /// The spec's §PR C Builds line writes this slot as "n of m shown",
    /// which is t0's spelling for the SPOKEN Where-am-I filter clause —
    /// core renders that one, and CD-5 already settled that there is
    /// exactly one of it. This is the visible LABEL, and §1 R-C says a
    /// static label is the mac inventory verbatim. CD-42 records the
    /// choice so the two spellings are not read as drift.
    /// </remarks>
    public static string FilterSummary(int matched, int total) =>
        $"{matched} of {SlateUniffiMethods.CountNoun((ulong)total, "card", "cards")} "
        + (matched == 1 ? "matches" : "match");

    /// <summary>The switcher's per-surface labels; the SPOKEN surface
    /// change is <c>CanvasSurfaceShown</c>, which core renders.</summary>
    public const string OutlineSurfaceLabel = "Outline";

    public const string TableSurfaceLabel = "Table";

    public const string VisualSurfaceLabel = "Visual";

    /// <summary>The visual board's UIA name (§D D3, "Canvas visual
    /// view"). NOT the switcher arm's label above: the peer shipped
    /// speaking the radio's word and the FlaUI journey caught it —
    /// the two phrases are neighbours here so they can never be
    /// confused for one another again.</summary>
    public const string VisualBoardName = "Canvas visual view";

    /// <summary>The table projection's accessible name (mac's
    /// <c>accessibilityLabel</c>, verbatim).</summary>
    public const string TableName = "Canvas table";

    // The mac column inventory, in mac's order (§W-G row J: the table's
    // projection config is host-by-designation, and its labels are the
    // mac label inventory verbatim — contract B2).
    public const string TypeColumn = "Type";

    public const string TitleColumn = "Title";

    public const string GroupColumn = "Group";

    public const string TargetColumn = "Target";

    public const string ConnectionsColumn = "Connections";

    public const string ColorColumn = "Color";

    // The mac row-action names, verbatim (contract B6).
    public const string OpenRowAction = "Open";

    public const string ToggleMarkRowAction = "Toggle Mark";

    public const string DeleteRowAction = "Delete";

    /// <summary>§E TE-8: the plan's kind-scoped rows (mac's labels).</summary>
    public const string EditCardRowAction = "Edit Card Text…";

    public const string RenameGroupRowAction = "Rename Group…";

    public const string UngroupRowAction = "Ungroup";

    public const string SetColorRowAction = "Set Color…";

    /// <summary>The group row's Delete stays disabled with the
    /// algebra's why: removal of a group IS Ungroup (ED-3).</summary>
    public const string GroupRemovalIsUngroup =
        "A group is removed by Ungroup — its cards stay.";

    /// <summary>§E TE-8: verbs whose COMMIT path shipped but whose
    /// prompt sheet has not — visible with the why, mac's
    /// temporarily-unavailable shape, never a dead click.</summary>
    public const string PromptArrivesLater =
        "Its prompt arrives with a later slice.";

    /// <summary>The reason the Toggle Mark row action is listed but
    /// disabled: the marking verbs are PR G's. Carried as the action's
    /// <c>DisabledReason</c>, which the substrate exposes as HelpText —
    /// the mac RowAction contract's "retain the relevant action with its
    /// reason".</summary>
    public const string MarkingArrivesLater = "Marking cards arrives in a later slice.";

    /// <summary>The same, for Delete: the mutation funnel is PR E's.</summary>

    /// <summary>
    /// The table's summary line — mac's sentence verbatim, including its
    /// pluralisation (contract B9).
    /// </summary>
    /// <remarks>
    /// A static LABEL, not an announcement (0a-13, §W-G row J): mac
    /// never speaks it, so the vocabulary has no <c>CanvasTableSummary</c>
    /// event to render and inventing one would put a string in the
    /// canonical corpus that no host announces. The substrate makes it a
    /// separately-focusable region, which is how a screen-reader user
    /// reads it on demand.
    /// </remarks>
    public static string TableSummary(int cards, int groups) =>
        $"Canvas table: {cards} card{(cards == 1 ? "" : "s")}, "
        + $"{groups} group{(groups == 1 ? "" : "s")}.";

    /// <summary>The Type cell: core's kind word with its leading
    /// character capitalised — mac's <c>.capitalized</c> over the same
    /// closed set of five ASCII words.</summary>
    public static string TypeCell(string kind) => Capitalized(kind);

    /// <summary>
    /// t0 §1.1's card reference, composed from core's parts — core's
    /// kind word and core's <c>speakable_name</c> (contract A9, CD-30).
    /// The mac <c>CanvasCardRef.phrase</c> twin, and a transliteration
    /// of core's own <c>a11y.rs::card_ref</c>, which is why
    /// <c>TheCardReferenceMatchesCoresOwnComposition</c> pins it
    /// against a RENDERED <c>CanvasMovedTo</c> rather than against
    /// itself.
    /// </summary>
    public static string CardReference(string kind, string speakableName) =>
        string.Equals(kind, "group", StringComparison.Ordinal)
            ? $"Group \"{speakableName}\""
            : $"{Capitalized(kind)} card \"{speakableName}\"";

    /// <summary>
    /// Core's <c>capitalize_first</c>: the LEADING character only, so a
    /// user-typed title passes through verbatim.
    /// </summary>
    /// <remarks>
    /// Rust's <c>char::to_uppercase</c> is the FULL Unicode mapping (one
    /// scalar may become several) and .NET's <c>ToUpperInvariant</c> is
    /// the simple one, so the two disagree on scalars like
    /// <c>ß</c> → <c>SS</c> — CD-34. Unreachable in practice
    /// and checked rather than asserted: the only argument is core's
    /// <c>kind_label</c>, a closed set of five ASCII words, and
    /// <c>TheCardReferenceMatchesCoresOwnComposition</c> renders all
    /// five through core. The split takes two UTF-16 units when the
    /// first is a high surrogate, which keeps a surrogate PAIR intact —
    /// not the same thing as a text element, which is what an earlier
    /// version of this remark wrongly claimed: a combining mark or a ZWJ
    /// sequence is still split. Nothing reaches it.
    /// </remarks>
    private static string Capitalized(string word)
    {
        if (word.Length == 0)
        {
            return word;
        }
        int lead = char.IsHighSurrogate(word[0]) && word.Length > 1 ? 2 : 1;
        return string.Concat(
            word[..lead].ToUpperInvariant(), word[lead..]);
    }

    /// <summary>t0 §1.2 standard context + §3 inspectability (contract
    /// A10): position, colour and marked state are pull-readable, never
    /// announcement-only. The mac <c>nodeValue</c> twin minus the
    /// <c>, filtered</c> clause PR C introduces.</summary>
    public static string RowStatus(
        uint ordinalN,
        uint totalM,
        string? container,
        string? colorName,
        bool marked,
        bool filtered = false)
    {
        string status = $"{ordinalN} of {totalM} in {container ?? "canvas"}";
        if (colorName is { Length: > 0 } color)
        {
            status += $", {color}";
        }
        if (marked)
        {
            status += ", marked";
        }
        if (filtered)
        {
            // t0 §3 / spec §PR C behavior 4: a row a reader reaches while
            // a filter is on carries the CONTEXT that it is one of a
            // narrowed set — mac's `CanvasOutlineView` appends the same
            // clause to the same value.
            status += ", filtered";
        }
        return status;
    }

    public static string ConnectionStatus(int ordinal, int total) =>
        $"connection {ordinal} of {total}";

    /// <summary>
    /// The mac <c>activationHint</c> inventory, verbatim — except the
    /// media one (CD-36).
    /// </summary>
    /// <remarks>
    /// Mac's image hint says media "arriving in a later milestone
    /// slice", while mac's own activate opens a non-Markdown target in
    /// its default app today. A HelpText that contradicts what the row
    /// does fails the one job this inventory has, so Windows describes
    /// the behaviour both hosts actually have; the mac hint is filed as
    /// an upstream note.
    /// </remarks>
    public static string ActivationHint(string kind) => kind switch
    {
        "group" => "Group. Cards inside follow in the outline.",
        "text" => "Opens the card text.",
        "file" => "Opens the note in this tab.",
        "image" => "Opens the media file in its default app.",
        "link" => "Opens the link in your browser.",
        _ => string.Empty,
    };

    public static string ConnectionHint => "Opens the connected card's row.";

    public static string OpenFailed(string path, Exception exception) =>
        $"This canvas could not be opened: {exception.Message} ({path})";

    public static string RetargetAbsent(string from, string to, Exception exception) =>
        $"{from} moved to {to}, which could not be reopened: {exception.Message}";
}

/// <summary>§E TE-5c: the connection editor's direction radio, mapped
/// onto the four end-style combinations (the mac twin's table).</summary>
internal enum CanvasConnectionDirection
{
    ToTarget,
    Both,
    None,
    FromTarget,
}
