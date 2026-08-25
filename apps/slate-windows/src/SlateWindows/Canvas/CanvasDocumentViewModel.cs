// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
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

    /// <summary>A text card: <see cref="CanvasDocumentViewModel.DetailText"/>
    /// is published and the view focuses the detail region.</summary>
    DetailShown,

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
internal sealed record CanvasFocusRequest(object Owner, string? NodeId, int Generation);

/// <summary>
/// The ONE immutable unit every projection reads (contract C10).
/// </summary>
/// <remarks>
/// <para>
/// Rows, table rows, totals and matches are CORRELATED state, and while
/// they were separate fields any consumer could read a mixture: the
/// table's rows narrowed by a match set computed for a different
/// outline, a summary combining a stale count with a live total, a pane
/// binding mid-publish and getting its outline from one load and its
/// table from the next. Holding them in one record makes a mixture
/// unrepresentable rather than merely avoided — the projections read a
/// value, and a value cannot be half-updated.
/// </para>
/// <para>
/// Built by <see cref="Build"/> so the filtered halves are always
/// derived from the SAME rows the unit carries. Nothing outside
/// constructs one.
/// </para>
/// </remarks>
internal sealed record CanvasProjectionUnit(
    IReadOnlyList<CanvasOutlineRow> Outline,
    IReadOnlyList<CanvasTableRow> TableRows,
    IReadOnlyDictionary<string, CanvasOutlineRow> Rows,
    string? AnswerNeedle,
    IReadOnlySet<string>? MatchedIds,
    IReadOnlyList<CanvasOutlineRow> FilteredOutline,
    IReadOnlyList<CanvasTableRow> FilteredTableRows)
{
    internal static readonly CanvasProjectionUnit Empty = Build([], [], null, null);

    /// <summary>True when a match set narrowed this unit at all.</summary>
    internal bool Narrowed => MatchedIds is not null;

    /// <summary>The canvas's own size — the denominator every count in
    /// this unit is over, taken from the unit rather than from a live
    /// field so it can never describe a different canvas than the
    /// numerator.</summary>
    internal int Total => Outline.Count;

    internal static CanvasProjectionUnit Build(
        IReadOnlyList<CanvasOutlineRow> outline,
        IReadOnlyList<CanvasTableRow> tableRows,
        string? answerNeedle,
        IReadOnlySet<string>? matched)
    {
        var rows = new Dictionary<string, CanvasOutlineRow>(StringComparer.Ordinal);
        foreach (CanvasOutlineRow row in outline)
        {
            rows[row.NodeId] = row;
        }
        return matched is null
            ? new CanvasProjectionUnit(
                outline, tableRows, rows, null, null, outline, tableRows)
            : new CanvasProjectionUnit(
                outline,
                tableRows,
                rows,
                answerNeedle,
                matched,
                outline.Where(row => matched.Contains(row.NodeId)).ToArray(),
                tableRows.Where(row => matched.Contains(row.NodeId)).ToArray());
    }
}

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
/// needle in the field. False means no handle could answer it and the
/// PREVIOUS answer is still on screen — the caller must say so rather
/// than count these rows as if they matched what the user just
/// typed.</param>
/// <param name="MatchedIds">The matched node ids when narrowed, so the
/// table projection filters core's table rows by the SAME answer the
/// outline shows; null when nothing is narrowed.</param>
/// <param name="Total">The size of the canvas
/// <paramref name="Rows"/> came out of — the denominator, carried on
/// the view rather than read live so "n of m" can never put a number
/// from one canvas over a total from another.</param>
internal sealed record CanvasFilterView(
    IReadOnlyList<CanvasOutlineRow> Rows,
    bool Narrowed,
    bool Current,
    IReadOnlySet<string>? MatchedIds,
    int Total);

/// <summary>
/// W6-1 PR A (#745): the per-path canvas document — the mac
/// <c>CanvasDocument</c> twin, built on the W4-6 <c>BaseDocument</c>
/// pattern. Owns the native handle, the load state, the published
/// projections and the shared <see cref="CanvasSelection"/>. One
/// instance per open vault-relative path, shared by every tab and pane
/// on that path (contract A1); the workspace registry owns creation and
/// shutdown.
///
/// Threading (contract A17), stated as it is: the LOAD, the CLOSE and
/// the FILTER MATCH run inside <see cref="PanelWorkScheduler.StartWork"/>
/// bodies, marshal their publications through <c>Post</c> and re-check
/// the generation; the per-node DETAIL reads (<see cref="NeighborsOf"/>,
/// <see cref="NodeTextOf"/>) and the activation identity read are
/// synchronous UI-thread calls. The line between the two lists is
/// WHOLE-MODEL versus per-node: a whole-model read can contend with a
/// load holding the lock across <c>open_canvas</c> plus three
/// projections, and the filter's match is the third such read (W6-1
/// PR C, contract C10) — it used to run in a property getter on the
/// dispatcher, which is exactly the stall this convention exists to
/// prevent. Every one of them — scheduled or not — holds
/// <c>_ffiLock</c> for its FFI section, which is what makes a handle
/// replacement unable to race a read. The handle is closed exactly once:
/// on replacement inside the load body, or on shutdown.
///
/// Canvas tabs are never dirty: mutations write through on commit
/// (PR E), so the U1 close gate is bypassed for canvas tabs
/// (contract A19).
/// </summary>
internal sealed class CanvasDocumentViewModel : PanelWorkScheduler
{
    private readonly VaultSession _session;
    private readonly object _ffiLock = new();
    private readonly string? _retargetedFrom;
    private ulong? _handle;
    private int _generation;
    private CanvasLoadState _state = CanvasLoadState.Loading;
    private string? _stateMessage;
    /// <summary>The ONE published unit (contract C10). Swapped whole;
    /// never edited in place.</summary>
    private CanvasProjectionUnit _view = CanvasProjectionUnit.Empty;

    /// <summary>A load whose rows are computed but NOT yet published,
    /// because a coherent prior unit is on screen and its replacement is
    /// waiting on the re-ask's matches.</summary>
    private (CanvasOpenInfo Info, IReadOnlyList<CanvasOutlineRow> Outline,
        IReadOnlyList<CanvasTableRow> TableRows)? _pendingLoad;
    private IReadOnlyList<CanvasLoadWarning> _warnings = [];
    private string? _detailText;
    private string? _detailTitle;
    private bool _announcedDegradedLoad;
    private Task? _asyncClose;
    private string _filterText = string.Empty;
    private string? _whereAmIText;
    private int _filterFocusToken;
    private int _filterGeneration;
    private bool _filterAnswerFailed;

    /// <summary>Per-node activation targets (file path / URL) and
    /// subpaths, derived once at load from core's table rows and scene
    /// — activation never re-queries (the mac <c>targets</c> shape).</summary>
    private readonly Dictionary<string, string> _targets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _subpaths = new(StringComparer.Ordinal);

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
        Announcer = announcer;
        _retargetedFrom = retargetedFrom;
        _verbosity = verbosity ?? (static () => CanvasVerbosity.Standard);
        Modes = new CanvasModeController(Announcer.Announce);
        Navigator = new CanvasNavigator(this);
    }

    /// <summary>Vault-relative path — the registry's identity, compared
    /// byte-exact (Ordinal) everywhere. Immutable: a rename re-keys the
    /// registry rather than mutating this (CD-32).</summary>
    public string Path { get; }

    public string DisplayName => System.IO.Path.GetFileNameWithoutExtension(Path);

    /// <summary>The one funnel every canvas surface announces
    /// through (contract A5).</summary>
    public CanvasAnnouncer Announcer { get; }

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
    public IReadOnlyList<CanvasOutlineRow> Outline => _view.Outline;

    /// <summary>Core's table rows, untransformed and in core's order
    /// (R-D) — the PR B projection's whole content. Published from the
    /// same load that publishes <see cref="Outline"/>, because they are
    /// two reads of one open (contract B4).</summary>
    public IReadOnlyList<CanvasTableRow> TableRows => _view.TableRows;

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
    public int PreservedItemCount =>
        _warnings.Count(warning => warning.Kind == CanvasLoadWarningKind.SkippedEntry);

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
    /// `CanvasEmptyOnboarding` is the event this region is FOR, and PR E
    /// installs it with the real New Card chord. It cannot ship here:
    /// its template renders "Press ⟨chord⟩ to create your first card"
    /// unconditionally, and PR A has no create command, so any chord in
    /// that slot — including the palette's — tells a screen-reader user
    /// to press a key that will not create anything. That is exactly the
    /// t2 rule the spec cites in the same sentence ("don't advertise a
    /// command that doesn't exist yet"), so until the command exists the
    /// region renders the true statement the vocabulary already has.
    /// CD-37.
    /// </remarks>
    public string? EmptyOnboardingText =>
        State == CanvasLoadState.Ready && _view.Outline.Count == 0
            ? CanvasAnnouncer.RenderLabel(
                new CanvasA11yEvent.CanvasStatus(new CanvasStatusNote.Empty()))
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

    /// <summary>The interim read-only card text (t2 #362), published by
    /// activating a text card; PR E swaps in the editor sheet.</summary>
    public string? DetailText
    {
        get => _detailText;
        private set => SetField(ref _detailText, value);
    }

    public string? DetailTitle
    {
        get => _detailTitle;
        private set => SetField(ref _detailTitle, value);
    }

    public void CloseDetail()
    {
        DetailText = null;
        DetailTitle = null;
    }

    /// <summary>The row whose activation opened a card — the focus
    /// restoration target when the user comes back (WCAG 2.4.3,
    /// contract A14).</summary>
    public string? LastActivatedNode { get; set; }

    /// <summary>Rows republished — the surface rebuilds the tree on
    /// this, never on property-change granularity.</summary>
    public event EventHandler? OutlinePublished;

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
    private int _focusRequestGeneration;

    /// <summary>
    /// The pending focus delivery — STATE, not an event edge
    /// (contract A14).
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
    /// newer request supersedes an older one by generation.
    /// </para>
    /// </remarks>
    public CanvasFocusRequest? FocusRequest
    {
        get => _focusRequest;
        private set => SetField(ref _focusRequest, value);
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
        FocusRequest = new CanvasFocusRequest(
            owner, nodeId, Interlocked.Increment(ref _focusRequestGeneration));
    }

    /// <summary>
    /// A surface delivered the request and is saying so. Only the
    /// matching generation clears it: a request raised while an older
    /// one was still pending must not be consumed by the older one's
    /// late delivery.
    /// </summary>
    internal void CompleteFocusLanding(CanvasFocusRequest delivered)
    {
        ArgumentNullException.ThrowIfNull(delivered);
        if (_focusRequest is { } pending && pending.Generation == delivered.Generation)
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
        if (request.NodeId is { } named && _view.Rows.ContainsKey(named))
        {
            return named;
        }
        if (LastActivatedNode is { } last && _view.Rows.ContainsKey(last))
        {
            return last;
        }
        return _view.Outline.Count > 0 ? _view.Outline[0].NodeId : null;
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
        Announcer.Announce(new CanvasA11yEvent.CanvasSurfaceShown(surface));
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
        nodeId is not null && _view.Rows.TryGetValue(nodeId, out CanvasOutlineRow? row)
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
        ReadRefusalFor(State, _handle is not null);

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
        Announcer.Announce(new CanvasA11yEvent.CanvasStatus(note));
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
        Announcer.Announce(new CanvasA11yEvent.CanvasStatus(
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
            lock (_ffiLock)
            {
                if (_handle is not { } handle)
                {
                    context = null;
                    return false;
                }
                context = _session.CanvasWhereAmI(handle, nodeId);
                return true;
            }
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
    /// <summary>
    /// Failure injection for the one branch no fixture can drive: a
    /// PANIC-CLASS exception out of a structural query.
    /// </summary>
    /// <remarks>
    /// The `CanvasMediaPolicy.FailIdentityQueryForTests` idiom, one
    /// subsystem over, and it exists for the same reason: a `bad_node`
    /// refusal is reachable with a bad id, but nothing a test can hand
    /// the real library makes it PANIC — and the panic path is the one
    /// that used to fault a scheduler task silently. Null in production
    /// and asserted so by the fact that uses it, which sets it inside a
    /// `try`/`finally`.
    /// </remarks>
    internal static Func<Exception>? StructuralQueryFaultForTests { get; set; }

    private bool TryQuery<T>(Func<ulong, T> read, T fallback, out T value)
    {
        try
        {
            lock (_ffiLock)
            {
                if (_handle is not { } handle)
                {
                    value = fallback;
                    return false;
                }
                // Injected HERE — after the handle check, immediately
                // before the call — so it reproduces "the query threw"
                // rather than "there was nothing to ask".
                if (StructuralQueryFaultForTests is { } fault)
                {
                    throw fault();
                }
                value = read(handle);
                return true;
            }
        }
        catch (VaultException)
        {
            // bad_node / bad_handle — one refusal, never discriminated by
            // message (0b-12's API note).
            value = fallback;
            return false;
        }
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
            OnPropertyChanged(nameof(FilterText));
            OnPropertyChanged(nameof(FilterActive));
            RefreshFilter();
        }
    }

    /// <summary>
    /// Ask core for the current needle's match set — OFF the dispatcher
    /// (contract C10/A17).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The match used to run inside the <c>Filter</c> getter, on the UI
    /// thread, taking <c>_ffiLock</c> — the lock a LOAD body holds across
    /// <c>open_canvas</c> plus three whole-model projections. A keystroke
    /// arriving during a load therefore blocked the dispatcher until the
    /// load finished, which on a large canvas over a slow filesystem is a
    /// stall the user types into. Every other whole-model read in this
    /// class is scheduled; this one now is too, which is A17's recorded
    /// convention rather than a new one.
    /// </para>
    /// <para>
    /// Two paths short-circuit and publish on THIS frame, because they
    /// need no query: an inactive needle IS the full outline, and an
    /// unchanged needle already has its answer memoized. So clearing the
    /// filter — the Escape rung, the Clear button — widens the rows
    /// immediately rather than after a scheduler hop.
    /// </para>
    /// <para>
    /// The generation guard is doubled on purpose: a stale NEEDLE's answer
    /// must not overwrite a newer one, and a stale DOCUMENT's answer must
    /// not land at all (a reload republishes rows the ids no longer
    /// describe).
    /// </para>
    /// </remarks>
    private void RefreshFilter()
    {
        int generation = Interlocked.Increment(ref _filterGeneration);
        // A new ask is pending, so whatever the LAST one did is no longer
        // this needle's story.
        _filterAnswerFailed = false;
        // The rows the answer will be about: a load waiting on this
        // re-ask has its rows in hand but unpublished, so the query is
        // ASKED for those and the unit is built from them.
        IReadOnlyList<CanvasOutlineRow> outline =
            _pendingLoad is { } pending ? pending.Outline : _view.Outline;
        if (!FilterActive)
        {
            PublishLoadedUnit(outline, null, null);
            return;
        }
        if (_view.Narrowed
            && string.Equals(_view.AnswerNeedle, _filterText, StringComparison.Ordinal)
            && ReferenceEquals(_view.Outline, outline))
        {
            // The published unit already answers this needle over these
            // rows; re-announcing it is all that is left.
            PublishUnit(_view);
            return;
        }
        string needle = _filterText;
        int documentGeneration = Volatile.Read(ref _generation);
        StartWork(() => FilterBody(needle, generation, documentGeneration, outline));
    }

    private void FilterBody(
        string needle,
        int generation,
        int documentGeneration,
        IReadOnlyList<CanvasOutlineRow> outline)
    {
        bool answered;
        IReadOnlyList<string> matched;
        try
        {
            answered = TryQuery<IReadOnlyList<string>>(
                handle => _session.CanvasFilter(handle, needle),
                [],
                out matched);
        }
        // LoadBody's contract, for LoadBody's reason (m6): the scheduler's
        // rule is that bodies catch their own failures, and a panic-class
        // uniffi exception escaping here faults the tracked task SILENTLY.
        // That is the only route to a permanently stranded filter — no
        // publish, so the rows never move, the summary never resolves and
        // the needle sits in the field describing nothing, with no
        // sentence anywhere saying why. Anything the process cannot
        // survive still propagates.
        catch (Exception exception) when (
            exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
        {
            answered = false;
            matched = [];
        }
        IReadOnlySet<string> ids = matched.ToHashSet(StringComparer.Ordinal);
        Post(() =>
        {
            // THREE guards, and the third is the one reasoning cannot
            // replace. A stale NEEDLE's answer must not overwrite a newer
            // one; a stale DOCUMENT's must not land at all; and an answer
            // computed against a DIFFERENT set of rows than the one it is
            // about to be published with must never publish, because its
            // ids describe rows that are not those rows. The handle can be
            // swapped by a load between this query taking the lock and its
            // rows being published, so "which outline did I intersect" is
            // a question the generations cannot answer on their own.
            IReadOnlyList<CanvasOutlineRow> target =
                _pendingLoad is { } waiting ? waiting.Outline : _view.Outline;
            if (Volatile.Read(ref _filterGeneration) != generation
                || Volatile.Read(ref _generation) != documentGeneration
                || !ReferenceEquals(target, outline))
            {
                return;
            }
            _filterAnswerFailed = !answered;
            // Answered or not, the LOAD that was waiting on this must
            // land: holding it back would leave a dead outline on screen
            // forever. An answer builds a narrowed unit; a failure builds
            // an unnarrowed one over the SAME new rows — every card
            // shown, and the summary saying the filter could not be
            // applied, so the widening is stated rather than silent.
            PublishLoadedUnit(outline, answered ? needle : null, answered ? ids : null);
            // The count is announced HERE — when an answer exists —
            // rather than on the keystroke, so it can never describe rows
            // that are not on screen yet. The announcer's filter class
            // still coalesces a burst into one line (t0 §1.5).
            Navigator.AnnounceFilterCount();
        });
    }

    /// <summary>
    /// Publish rows and matches as ONE unit — and, when a load was
    /// waiting on those matches, its state with them (contract C10).
    /// </summary>
    /// <remarks>
    /// This is the atomic step the consumers see. A load that deferred
    /// left its rows, warnings and readiness in <c>_pendingLoad</c>
    /// precisely so no render between the two could read half of each.
    /// The landing therefore writes state through the BACKING FIELDS —
    /// silently — and lets <see cref="PublishUnit"/> raise the whole
    /// notification burst once, so a render woken by the state flip can
    /// only ever see the unit that flip belongs to. Before that burst the
    /// consumers hold the PRIOR unit, entire.
    /// </remarks>
    private void PublishLoadedUnit(
        IReadOnlyList<CanvasOutlineRow> outline,
        string? answerNeedle,
        IReadOnlySet<string>? matched)
    {
        IReadOnlyList<CanvasTableRow> tableRows = _pendingLoad is { } pending
            ? pending.TableRows
            : _view.TableRows;
        CanvasProjectionUnit unit = CanvasProjectionUnit.Build(
            outline, tableRows, answerNeedle, matched);
        if (_pendingLoad is { } landing)
        {
            _pendingLoad = null;
            _view = unit;
            _warnings = landing.Info.Warnings;
            _stateMessage = null;
            _state = CanvasLoadState.Ready;
            // A reload keeps the selection when the node survived; a
            // selection pointing at a node that is gone would leave every
            // selection-scoped verb acting on nothing (contract A12).
            if (Selection.Selected is not { } selected || !unit.Rows.ContainsKey(selected))
            {
                // Silent seat (contract A12): the focus lands on the row
                // and the screen reader reads it; a CanvasMovedTo on top
                // of that is the t0 §1.5 doubling rule broken at the
                // first keystroke of the surface's life.
                Selection.Selected = unit.Outline.Count > 0 ? unit.Outline[0].NodeId : null;
            }
            PublishUnit(unit);
            AnnounceDegradedLoadIfNeeded();
            return;
        }
        PublishUnit(unit);
    }

    /// <summary>
    /// Swap the published unit and tell everyone — the ONE place any
    /// projection's rows change (contract C10).
    /// </summary>
    /// <remarks>
    /// The swap precedes every notification, and every notification the
    /// swap owes follows it, so there is no observable instant in which
    /// half a unit is readable. The state properties are notified here
    /// too because <see cref="PublishLoadedUnit"/> writes them silently
    /// for exactly that reason.
    /// </remarks>
    private void PublishUnit(CanvasProjectionUnit unit)
    {
        _view = unit;
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(StateMessage));
        OnPropertyChanged(nameof(Warnings));
        OnPropertyChanged(nameof(Outline));
        OnPropertyChanged(nameof(TableRows));
        OnPropertyChanged(nameof(FilteredOutline));
        OnPropertyChanged(nameof(FilteredTableRows));
        NotifyStateChanged();
        OutlinePublished?.Invoke(this, EventArgs.Empty);
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
    /// same needle inactive — so <c>\n</c> and <c>\r</c> are carved out
    /// to match mac there. NOT a full transcription of
    /// <c>.whitespaces</c> (Zs plus tab): U+000B, U+000C, U+0085, U+2028
    /// and U+2029 read active on mac and inactive here, which C10 records
    /// rather than chases — they are unreachable from a keyboard and
    /// belong to the trimming differences CD-22 covers.
    /// </remarks>
    public bool FilterActive => IsFilterActive(_filterText);

    internal static bool IsFilterActive(string needle) =>
        needle.Any(character => !char.IsWhiteSpace(character) || character is '\n' or '\r');

    /// <summary>
    /// What the surfaces show for the current needle, in reading order —
    /// ONE value, so the rows on screen, the summary's number and the
    /// announced count cannot disagree.
    /// </summary>
    /// <remarks>
    /// The MATCH is core's <c>canvas_filter</c> (0b-13/0b-14): title, the
    /// kind type word, any one element of the group path, and the
    /// activation target. The needle goes over UNTRIMMED — core trims it,
    /// and an empty needle matches everything, so whitespace answers the
    /// full outline exactly as <see cref="FilterActive"/> says it should.
    ///
    /// PURE, and now a pure read of the PUBLISHED UNIT (contract C10):
    /// the rows, the matches and the total all come out of one immutable
    /// snapshot, so the count in the summary is over the same canvas the
    /// rows came from even mid-reload. <c>Current</c> covers two causes —
    /// "no handle could answer" and "the answer has not landed yet" — and
    /// the callers that speak distinguish them by asking
    /// <see cref="ReadRefusal"/>, because only the first is a state the
    /// user needs told about.
    /// </remarks>
    internal CanvasFilterView Filter
    {
        get
        {
            if (!FilterActive)
            {
                return new CanvasFilterView(
                    _view.Outline, Narrowed: false, Current: true, null, _view.Total);
            }
            if (_view.Narrowed)
            {
                // The published answer either IS this needle's or is the
                // previous coherent one still on screen; either way its
                // rows and its ids came from the same unit, so a view can
                // never be an intersection of two different canvases.
                return new CanvasFilterView(
                    _view.FilteredOutline,
                    Narrowed: true,
                    Current: string.Equals(
                        _view.AnswerNeedle, _filterText, StringComparison.Ordinal),
                    _view.MatchedIds,
                    _view.Total);
            }
            // Nothing was ever applied, so the unfiltered outline IS the
            // prior view. `Narrowed: false` keeps the summary from
            // claiming these rows matched anything.
            return new CanvasFilterView(
                _view.Outline, Narrowed: false, Current: false, null, _view.Total);
        }
    }

    /// <summary>The outline rows the surfaces display — the unit's own
    /// filtered half, never a live re-intersection.</summary>
    public IReadOnlyList<CanvasOutlineRow> FilteredOutline => _view.FilteredOutline;

    /// <summary>The table rows the grid displays — core's rows, narrowed
    /// by the SAME answer over the SAME canvas the outline shows, because
    /// both halves are fields of one snapshot (contract C10).</summary>
    public IReadOnlyList<CanvasTableRow> FilteredTableRows => _view.FilteredTableRows;

    /// <summary>
    /// True when the last query for the CURRENT needle came back with
    /// nothing — it ran and failed, as distinct from not having run yet.
    /// </summary>
    /// <remarks>
    /// One bit, because the two are different facts the reader is owed
    /// different answers about: a query still in flight is a frame of
    /// latency and says nothing, while a query that FAILED has to say so
    /// or the summary sits blank forever with a needle in the field
    /// above it. Reset the moment a new ask is scheduled.
    /// </remarks>
    internal bool FilterAnswerFailed => _filterAnswerFailed;

    /// <summary>
    /// A request to put keyboard focus in the filter field (Ctrl+F). A
    /// TOKEN rather than an event: only the surface the user is looking
    /// at should take focus, and a token lets each surface decide for
    /// itself when it observes the change (the mac
    /// <c>canvasFilterFocusToken</c> shape, Codoki #626).
    /// </summary>
    public int FilterFocusToken
    {
        get => _filterFocusToken;
        private set => SetField(ref _filterFocusToken, value);
    }

    internal void RequestFilterFocus() => FilterFocusToken++;

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
    public void Load()
    {
        if (IsShutDown)
        {
            // A shut-down document must not even MUTATE state: the
            // scheduler would refuse the body, leaving a permanent
            // "Loading" lie on whatever UI still observes this VM (the
            // Bases lesson).
            return;
        }
        int generation = Interlocked.Increment(ref _generation);
        State = CanvasLoadState.Loading;
        StateMessage = null;
        _announcedDegradedLoad = false;
        CloseDetail();
        StartWork(() => LoadBody(generation));
    }

    private void LoadBody(int generation)
    {
        CanvasOpenInfo info;
        CanvasOutlineRow[] outline;
        CanvasTableRow[] tableRows;
        CanvasScene scene;
        try
        {
            lock (_ffiLock)
            {
                if (Volatile.Read(ref _generation) != generation)
                {
                    return;
                }
                CloseHandleLocked();
                info = _session.OpenCanvas(Path);
                if (info.Degraded)
                {
                    // Read-only by construction (contract A3): nothing
                    // will use the handle, so release it now rather
                    // than holding native state until teardown.
                    _session.CloseCanvas(info.Handle);
                    CanvasOpenInfo degraded = info;
                    Post(() =>
                    {
                        if (Volatile.Read(ref _generation) != generation)
                        {
                            return;
                        }
                        PublishEmpty(
                            degraded.Warnings,
                            ParseErrorDetail(degraded.Warnings),
                            CanvasLoadState.ParseError);
                    });
                    return;
                }
                _handle = info.Handle;
                outline = _session.CanvasOutline(info.Handle);
                tableRows = _session.CanvasTableRows(info.Handle);
                scene = _session.CanvasScene(info.Handle);
            }
        }
        // Not just VaultException (m6): the scheduler's contract is that
        // bodies catch their own failures, and a panic-class uniffi
        // exception escaping here faults the tracked task silently and
        // leaves the tab reading "Opening canvas…" forever. Anything the
        // process cannot survive still propagates.
        catch (Exception exception) when (
            exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
        {
            Post(() =>
            {
                if (Volatile.Read(ref _generation) != generation)
                {
                    return;
                }
                PublishEmpty(
                    [],
                    _retargetedFrom is { } from
                        ? CanvasPhrase.RetargetAbsent(from, Path, exception)
                        : CanvasPhrase.OpenFailed(Path, exception),
                    _retargetedFrom is null
                        ? CanvasLoadState.Failed
                        : CanvasLoadState.RetargetAbsent);
            });
            return;
        }
        CanvasOpenInfo opened = info;
        Post(() =>
        {
            if (Volatile.Read(ref _generation) != generation)
            {
                return;
            }
            PublishReady(opened, outline, tableRows, scene);
        });
    }

    /// <summary>
    /// A load that produced no canvas: the empty unit and the failure's
    /// own state, published as one step (contract C10).
    /// </summary>
    private void PublishEmpty(
        IReadOnlyList<CanvasLoadWarning> warnings,
        string? stateMessage,
        CanvasLoadState state)
    {
        _targets.Clear();
        _subpaths.Clear();
        _neighbors.Clear();
        WhereAmIText = null;
        // A held load never outlives the failure that replaced it: rows
        // computed for a canvas that would not open must not land later
        // as an answer to a needle.
        _pendingLoad = null;
        // A read cache never outlives the rows it describes (contract
        // C10) — and it cannot, now that the matches are FIELDS of the
        // unit: publishing the empty unit retires the ids with the rows
        // they named in one step. Written through the backing fields for
        // `PublishLoadedUnit`'s reason: the state and the unit reach the
        // consumers in one notification burst, never one before the
        // other.
        CanvasProjectionUnit unit = CanvasProjectionUnit.Empty;
        _view = unit;
        _warnings = warnings;
        _stateMessage = stateMessage;
        _state = state;
        Selection.Selected = null;
        PublishUnit(unit);
    }

    private void PublishReady(
        CanvasOpenInfo info,
        IReadOnlyList<CanvasOutlineRow> outline,
        IReadOnlyList<CanvasTableRow> tableRows,
        CanvasScene scene)
    {
        _targets.Clear();
        _subpaths.Clear();
        _neighbors.Clear();
        WhereAmIText = null;
        foreach (CanvasTableRow row in tableRows)
        {
            _targets[row.NodeId] = row.Target;
        }
        foreach (CanvasSceneNode node in scene.Nodes)
        {
            if (node.Subpath is { Length: > 0 } subpath)
            {
                _subpaths[node.NodeId] = subpath;
            }
        }
        // The load is HELD, not published, and the whole of it — rows,
        // table rows, warnings, readiness, the selection re-seat — lands
        // in one step from `PublishLoadedUnit` (contract C10).
        //
        // Publishing here would raise a frame in which the rows are the
        // NEW outline and no match set describes it: every card on screen
        // with a populated filter field, then a second frame a scheduler
        // hop later where they narrow again. Worse, a render driven by
        // the STATE flip in between would read the new totals against the
        // old matches, and a pane binding in that gap would take its
        // outline from one load and its table from the next. Correlated
        // state, two producers — so there is one producer now.
        //
        // With no needle there is nothing to wait for and the unit is
        // built and published immediately; the branch is the same code.
        _pendingLoad = (info, outline, tableRows);
        RefreshFilter();
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
        Announcer.Announce(new CanvasA11yEvent.CanvasLoadedDegraded((uint)skipped));
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
            lock (_ffiLock)
            {
                if (_handle is not { } handle)
                {
                    Announcer.Announce(new CanvasA11yEvent.CanvasStatus(
                        new CanvasStatusNote.NotReadable()));
                    return null;
                }
                string? text = _session.CanvasNodeText(handle, nodeId);
                if (text is null)
                {
                    Announcer.Announce(new CanvasA11yEvent.CanvasStatus(
                        new CanvasStatusNote.NotATextCard()));
                }
                return text;
            }
        }
        catch (VaultException)
        {
            Announcer.Announce(new CanvasA11yEvent.CanvasBlocked(
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
            Announcer.Announce(boundary);
        }
        Announcer.Announce(new CanvasA11yEvent.CanvasMovedTo(
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
        if (NodeTextOf(row.NodeId) is not { } text)
        {
            return CanvasActivation.Refused;
        }
        LastActivatedNode = row.NodeId;
        DetailTitle = row.Title;
        DetailText = text;
        return CanvasActivation.DetailShown;
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
            Announcer.Announce(new CanvasA11yEvent.CanvasBlocked(
                new CanvasBlockedReason.NotAUrl()));
            return CanvasActivation.Refused;
        }
        if (OpenExternalLinkFromSurface?.Invoke(url) == true)
        {
            LastActivatedNode = row.NodeId;
            Announcer.Announce(new CanvasA11yEvent.CanvasOpened(
                row.Title, CanvasOpenTarget.Browser));
            return CanvasActivation.Opened;
        }
        Announcer.Announce(new CanvasA11yEvent.CanvasBlocked(
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
            Announcer.Announce(new CanvasA11yEvent.CanvasFileNotFound(
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
                Announcer.Announce(new CanvasA11yEvent.CanvasActionFailed(
                    CanvasFailedAction.CanvasAction, target));
                return CanvasActivation.Refused;
            }
            if (OpenMediaCardFromSurface?.Invoke(target) == true)
            {
                LastActivatedNode = row.NodeId;
                Announcer.Announce(new CanvasA11yEvent.CanvasOpened(
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
            Announcer.Announce(new CanvasA11yEvent.CanvasActionFailed(
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
    /// read — it touches no handle — but it runs under <c>_ffiLock</c>
    /// anyway (contract A17): "every FFI section holds the lock" is a
    /// rule worth being able to state without an exception, and the cost
    /// is one uncontended acquire on an activation.
    /// </summary>
    private bool TargetExistsInVault(string target)
    {
        try
        {
            lock (_ffiLock)
            {
                return _session.CanonicalPath(target) is not null;
            }
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
        base.Shutdown();
        _ = Interlocked.Increment(ref _generation);
        // M4's last case, and the deferred-departure drain with it. Both
        // have to happen BEFORE the announcer is silenced or their
        // restorations would never be spoken — closing the tab a mode is
        // running in is exactly the departure M4 names, and a departure
        // still held from a failed commit owes a sentence too.
        Modes.Shutdown();
        WhereAmIText = null;
        // Every retirement route reaches here — the release sweep, the
        // retarget, the vault-close drain — so this is the one place the
        // announcer has to be silenced (contract A5): a coalesced line
        // queued on a dying document would otherwise fire ~200 ms later
        // and speak about a surface that no longer exists.
        Announcer.Shutdown();
        FocusRequest = null;
        if (IsSynchronousForTests)
        {
            CloseHandleGuarded();
            return;
        }
        _asyncClose = Task.Run(CloseHandleGuarded);
    }

    private void CloseHandleGuarded()
    {
        try
        {
            lock (_ffiLock)
            {
                CloseHandleLocked();
            }
        }
        catch (Exception exception) when (
            exception is VaultException or ObjectDisposedException)
        {
            // Teardown race: the session died first — the handle died
            // with it.
        }
    }

    internal Task WhenHandleClosed() => _asyncClose ?? Task.CompletedTask;

    private void CloseHandleLocked()
    {
        if (_handle is { } handle)
        {
            _handle = null;
            _session.CloseCanvas(handle);
        }
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

    public const string VisualShipsLater = "The canvas visual view arrives in a later slice.";

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

    /// <summary>The reason the Toggle Mark row action is listed but
    /// disabled: the marking verbs are PR G's. Carried as the action's
    /// <c>DisabledReason</c>, which the substrate exposes as HelpText —
    /// the mac RowAction contract's "retain the relevant action with its
    /// reason".</summary>
    public const string MarkingArrivesLater = "Marking cards arrives in a later slice.";

    /// <summary>The same, for Delete: the mutation funnel is PR E's.</summary>
    public const string DeletingArrivesLater = "Deleting cards arrives in a later slice.";

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
