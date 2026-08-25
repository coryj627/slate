// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

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
/// W6-1 PR A (#745): the per-path canvas document — the mac
/// <c>CanvasDocument</c> twin, built on the W4-6 <c>BaseDocument</c>
/// pattern. Owns the native handle, the load state, the published
/// projections and the shared <see cref="CanvasSelection"/>. One
/// instance per open vault-relative path, shared by every tab and pane
/// on that path (contract A1); the workspace registry owns creation and
/// shutdown.
///
/// Threading (contract A17), stated as it is: the LOAD and the CLOSE run
/// inside <see cref="PanelWorkScheduler.StartWork"/> bodies, marshal
/// their publications through <c>Post</c> and re-check the generation;
/// the per-node DETAIL reads (<see cref="NeighborsOf"/>,
/// <see cref="NodeTextOf"/>) and the activation identity read are
/// synchronous UI-thread calls. Every one of them — scheduled or not —
/// holds <c>_ffiLock</c> for its FFI section, which is what makes a
/// handle replacement unable to race a read. The handle is closed
/// exactly once: on replacement inside the load body, or on shutdown.
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
    private IReadOnlyList<CanvasOutlineRow> _outline = [];
    private IReadOnlyList<CanvasLoadWarning> _warnings = [];
    private string? _detailText;
    private string? _detailTitle;
    private bool _announcedDegradedLoad;
    private Task? _asyncClose;

    /// <summary>Row lookup by node id — the outline is walked by id from
    /// selection, activation and the tree's own callbacks.</summary>
    private readonly Dictionary<string, CanvasOutlineRow> _rows =
        new(StringComparer.Ordinal);

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
        string? retargetedFrom = null)
        : base(synchronousForTests)
    {
        _session = session;
        Path = path;
        Announcer = announcer;
        _retargetedFrom = retargetedFrom;
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

    /// <summary>Contract A7: a parameter at every announce site,
    /// defaulted until PR C ships the persisted, live-switchable
    /// preference. PR C replaces the field and changes no call site.</summary>
    public CanvasVerbosity Verbosity { get; set; } = CanvasVerbosity.Standard;

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
        State == CanvasLoadState.Ready && _outline.Count == 0
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
        nodeId is not null && _rows.TryGetValue(nodeId, out CanvasOutlineRow? row)
            ? row
            : null;

    /// <summary>Core's target for a card — the file path, the URL, or
    /// empty for text cards and groups (0b-13's one definition).</summary>
    public string TargetOf(string nodeId) =>
        _targets.TryGetValue(nodeId, out string? target) ? target : string.Empty;

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
                        PublishEmpty();
                        Warnings = degraded.Warnings;
                        StateMessage = ParseErrorDetail(degraded.Warnings);
                        State = CanvasLoadState.ParseError;
                        OutlinePublished?.Invoke(this, EventArgs.Empty);
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
                PublishEmpty();
                Warnings = [];
                StateMessage = _retargetedFrom is { } from
                    ? CanvasPhrase.RetargetAbsent(from, Path, exception)
                    : CanvasPhrase.OpenFailed(Path, exception);
                State = _retargetedFrom is null
                    ? CanvasLoadState.Failed
                    : CanvasLoadState.RetargetAbsent;
                OutlinePublished?.Invoke(this, EventArgs.Empty);
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

    private void PublishEmpty()
    {
        _rows.Clear();
        _targets.Clear();
        _subpaths.Clear();
        _neighbors.Clear();
        Outline = [];
        Selection.Selected = null;
    }

    private void PublishReady(
        CanvasOpenInfo info,
        IReadOnlyList<CanvasOutlineRow> outline,
        IReadOnlyList<CanvasTableRow> tableRows,
        CanvasScene scene)
    {
        _rows.Clear();
        _targets.Clear();
        _subpaths.Clear();
        _neighbors.Clear();
        foreach (CanvasOutlineRow row in outline)
        {
            _rows[row.NodeId] = row;
        }
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
        Warnings = info.Warnings;
        Outline = outline;
        StateMessage = null;
        State = CanvasLoadState.Ready;
        // A reload keeps the selection when the node survived; a
        // selection pointing at a node that is gone would leave every
        // selection-scoped verb acting on nothing (contract A12).
        if (Selection.Selected is not { } selected || !_rows.ContainsKey(selected))
        {
            // Silent seat (contract A12): the focus lands on the row
            // and the screen reader reads it; a CanvasMovedTo on top of
            // that is the t0 §1.5 doubling rule broken at the first
            // keystroke of the surface's life.
            Selection.Selected = outline.Count > 0 ? outline[0].NodeId : null;
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
    public IReadOnlyList<CanvasNeighbor> NeighborsOf(string nodeId)
    {
        if (_neighbors.TryGetValue(nodeId, out IReadOnlyList<CanvasNeighbor>? cached))
        {
            return cached;
        }
        CanvasNeighbor[] neighbors;
        try
        {
            lock (_ffiLock)
            {
                if (_handle is not { } handle)
                {
                    return [];
                }
                neighbors = _session.CanvasNeighbors(handle, nodeId);
            }
        }
        catch (VaultException)
        {
            // bad_node / bad_handle — one refusal, never discriminated
            // by message (0b-12's API note). Uncached: the handle may
            // be reopened on the next change event.
            return [];
        }
        _neighbors[nodeId] = neighbors;
        return neighbors;
    }

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

    /// <summary>The switcher's per-surface labels; the SPOKEN surface
    /// change is <c>CanvasSurfaceShown</c>, which core renders.</summary>
    public const string OutlineSurfaceLabel = "Outline";

    public const string TableSurfaceLabel = "Table";

    public const string VisualSurfaceLabel = "Visual";

    public const string TableShipsLater = "The canvas table view arrives in a later slice.";

    public const string VisualShipsLater = "The canvas visual view arrives in a later slice.";

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
        uint ordinalN, uint totalM, string? container, string? colorName, bool marked)
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
