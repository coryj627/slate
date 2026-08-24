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
/// W6-1 PR A (#745): the per-path canvas document — the mac
/// <c>CanvasDocument</c> twin, built on the W4-6 <c>BaseDocument</c>
/// pattern. Owns the native handle, the load state, the published
/// projections and the shared <see cref="CanvasSelection"/>. One
/// instance per open vault-relative path, shared by every tab and pane
/// on that path (contract A1); the workspace registry owns creation and
/// shutdown.
///
/// Threading (contract A17): every FFI touch runs inside a
/// <see cref="PanelWorkScheduler.StartWork"/> body under one lock, so a
/// handle replacement can never race an in-flight read; publications
/// marshal through <c>Post</c> and re-check the generation. The handle
/// is closed exactly once — on replacement inside the load body, or on
/// shutdown.
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

    /// <summary>The empty-state onboarding copy (0a-13 LABEL class).
    /// PR E ships New Card; until then the New Card slot carries the
    /// palette chord too, so the copy never advertises a command that
    /// does not exist (the t2 rule).</summary>
    public string? EmptyOnboardingText =>
        State == CanvasLoadState.Ready && _outline.Count == 0
            ? CanvasAnnouncer.RenderLabel(
                new CanvasA11yEvent.CanvasEmptyOnboarding(
                    NewCardChord: PaletteChord, PaletteChord: PaletteChord))
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
        catch (VaultException exception)
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
        // The shared external-link policy's allowlist
        // (WorkspaceViewModel.Citations.cs): a canvas file cannot
        // smuggle a `file:` or `javascript:` target past it.
        bool allowed = Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            && uri.Scheme is "http" or "https" or "mailto";
        if (!allowed)
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
        LastActivatedNode = row.NodeId;
        return OpenFileCardFromSurface?.Invoke(target, AnchorFor(row.NodeId)) == true
            ? CanvasActivation.Navigated
            : CanvasActivation.Refused;
    }

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
        base.Shutdown();
        _ = Interlocked.Increment(ref _generation);
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

    public const string WarningsRegionName = "Unsupported items";

    public const string DetailRegionName = "Card text";

    /// <summary>The switcher's per-surface labels; the SPOKEN surface
    /// change is <c>CanvasSurfaceShown</c>, which core renders.</summary>
    public const string OutlineSurfaceLabel = "Outline";

    public const string TableSurfaceLabel = "Table";

    public const string VisualSurfaceLabel = "Visual";

    public const string TableShipsLater = "The canvas table view arrives in a later slice.";

    public const string VisualShipsLater = "The canvas visual view arrives in a later slice.";

    /// <summary>t0 §1.1's card reference, composed from core's parts —
    /// core's kind word and core's <c>speakable_name</c> (contract A9,
    /// CD-30). The mac <c>CanvasCardRef.phrase</c> twin.</summary>
    public static string CardReference(string kind, string speakableName) =>
        string.Equals(kind, "group", StringComparison.Ordinal)
            ? $"Group \"{speakableName}\""
            : $"{Capitalized(kind)} card \"{speakableName}\"";

    private static string Capitalized(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];

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

    /// <summary>The mac <c>activationHint</c> inventory, verbatim.</summary>
    public static string ActivationHint(string kind) => kind switch
    {
        "group" => "Group. Cards inside follow in the outline.",
        "text" => "Opens the card text.",
        "file" => "Opens the note in this tab.",
        "image" => "Media cards open with canvas actions, arriving in a later milestone slice.",
        "link" => "Opens the link in your browser.",
        _ => string.Empty,
    };

    public static string ConnectionHint => "Opens the connected card's row.";

    public static string OpenFailed(string path, VaultException exception) =>
        $"This canvas could not be opened: {exception.Message} ({path})";

    public static string RetargetAbsent(string from, string to, VaultException exception) =>
        $"{from} moved to {to}, which could not be reopened: {exception.Message}";
}
