// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>A load request's shape: a PAIR (snapshot + rows) or ROWS
/// ONLY (a sort) — contract A-2, rule A.</summary>
internal enum GraphLoadKind
{
    Pair,
    RowsOnly,
}

/// <summary>What a load speaks when it publishes with the tab
/// effective (rule L, Term 6; rule Q, Term Q4): the snapshot summary,
/// nothing, the preset's headline (rule P, Term P4), or the filter
/// count (a needle's or a filter's pair — W6-2 PR C).</summary>
internal enum GraphAnnouncePolicy
{
    Summary,
    Silent,
    Preset,
    FilterCount,
}

/// <summary>A USER request's four arms (W6-2 PR C, rule Q Term Q1): the
/// needle, the sort, the preset, the filter — every EXTERNAL query change
/// reaches the document through <see cref="GraphDocumentViewModel.Request"/>
/// and nothing else; the probe's and the receiver's tokens are the two
/// internal origins.</summary>
internal abstract record GraphRequest
{
    private GraphRequest()
    {
    }

    /// <summary>The needle changed (the navigator's write, C-6).</summary>
    public sealed record Needle() : GraphRequest;

    /// <summary>The grid asked for a sort (A-5).</summary>
    public sealed record Sort(GraphTableSort Requested) : GraphRequest;

    /// <summary>A preset over the already-effective graph (rule P, Term P3).</summary>
    public sealed record Preset(GraphPreset Chosen) : GraphRequest;

    /// <summary>PR E's manual filter change, written through
    /// <c>ApplyQuery</c> before the request (Term Q4).</summary>
    public sealed record Filter(GraphFilter Backend) : GraphRequest;
}

/// <summary>The full request record a token carries (contract A-2).</summary>
internal sealed record GraphTableRequest(GraphVisibilityQuery Query, GraphTableSort Sort);

/// <summary>The load token (contract A-2): the document instance, the
/// session the body was started against, the lifecycle generation the
/// body was started under (IPA-6), the request, the sequence.</summary>
internal sealed record GraphLoadToken(
    GraphDocumentViewModel Document,
    VaultSession Session,
    int LifecycleGeneration,
    GraphTableRequest Request,
    ulong Seq,
    GraphLoadKind Kind,
    GraphAnnouncePolicy Announce,
    GraphPreset? Preset = null,
    bool UserSort = false);

/// <summary>The worker ENVELOPE (contract A-2; the round-3 ledger's
/// IGA-22, IGA-43): the inputs the body actually used beside its
/// results, because neither result carries its inputs — and the
/// selection generation the worker OBSERVED immediately before its
/// snapshot crossing (IPC-13), which the apply compares against.</summary>
/// <summary>Term F3's terminal kinds (W6-2 PR C, C-17): the surface delivers
/// on an INSTALL and a PAIR failure (Term F4's arms) and WITHDRAWS its
/// pending request on a ROWS-ONLY failure or a REJECTION — the old
/// publication stands and is not called current.</summary>
internal enum GraphLineageEnd
{
    Install,
    PairFailure,
    RowsFailure,
    Rejection,
}

internal sealed record GraphLoadEnvelope(
    GraphLoadToken Token,
    GraphFilter Filter,
    GraphVisibilityQuery Query,
    GraphTableSort Sort,
    GraphSnapshot? Snapshot,
    GraphTableRows? Rows,
    string? Failure,
    int SelectionGeneration = 0);

/// <summary>What one installed publication answered — the surface's
/// adoption announcement reads it (contract A-5).</summary>
/// <summary>Rule F, Term F1 (W6-2 PR C, C-17): the landing's record — the
/// tab it is addressed to and nothing else.</summary>
internal sealed record GraphFocusRequest(object Owner);

internal sealed record GraphPublicationInstall(
    GraphPublication Previous,
    GraphPublication Current,
    bool AnsweredSortRequest);

/// <summary>
/// W6-2 PR A (#746): the ONE graph document (spec §1; contracts A-1..A-3,
/// A-7, A-8) — the mac <c>AppState+GraphTable</c> twin on the
/// <see cref="PanelWorkScheduler"/> substrate. It owns the view state,
/// the load token, the one immutable <see cref="GraphPublication"/>, the
/// generation probe with its high-water mark, the row copy, the cell
/// lookup and the fetched-once inventories. It runs every body through
/// <see cref="PanelWorkScheduler.StartWorkAlwaysAsync{T}"/> and has NO
/// inline mode (AD-4): the apply always lands on the owner context the
/// document captured — the current dispatcher context, or the
/// constructing thread's dispatcher when none is current.
/// </summary>
internal sealed class GraphDocumentViewModel : PanelWorkScheduler
{
    private readonly VaultSession _session;
    private readonly GraphAnnouncer _announcer;
    private readonly Func<bool> _isEffectiveActive;
    private readonly Func<GraphVerbosity> _verbosity;
    private readonly GraphPreferencesViewModel? _preferences;
    private readonly Func<GraphA11yEvent.GraphWhereAmI?> _tableReadback;
    private readonly Func<int> _lifecycleGeneration;
    private readonly Func<bool> _isSeated;
    private readonly Dictionary<GraphNodeKind, IReadOnlyList<GraphRowActionSpec>> _actionsByKind;
    private ulong _seq;
    private GraphTableRequest? _request;
    private GraphTableSort? _requestedSort;
    private ulong _highWater;
    // Rule Q, Term Q2: the lineage — the token in flight and nothing
    // else remembers a policy or a preset.
    private GraphLoadToken? _current;
    private string _filterCountText = string.Empty;
    // Volatile (IPD-4): read from the pool by the always-async bodies and
    // by a fact's release barrier; written by Retire on the owner's thread.
    private volatile bool _retired;
    private GraphPublication _publication;
    // W6-2 PR D, rule G (Terms G1–G7): the diagram lineage — at most one
    // model, the builds not yet seated (IGT-1), the build sequence, the
    // epoch key, the refresh's in-flight pair, the motion policy.
    private readonly GraphMotionPolicy _motion;
    private readonly bool _ownsMotion;
    private readonly object _diagramLock = new();
    private readonly HashSet<GraphDiagramModel> _unseatedBuilds = [];
    private GraphDiagramModel? _diagramModel;
    private ulong _diagramSeq;
    private ulong _epochSeq;
    private (GraphDiagramModel Model, ulong Generation, GraphVisibilityQuery Query, GraphGroup[] Groups)? _epoch;
    private bool _refreshRunning;
    private bool _refreshAgain;

    public GraphDocumentViewModel(
        VaultSession session,
        GraphAnnouncer announcer,
        GraphViewState viewState,
        Func<bool> isEffectiveActive,
        Func<GraphVerbosity> verbosity,
        SynchronizationContext? ownerContext = null,
        Func<int>? lifecycleGeneration = null,
        Func<bool>? isSeated = null,
        GraphNavigator? navigator = null,
        GraphPreferencesViewModel? preferences = null,
        GraphMotionPolicy? motionPolicy = null)
        : base(
            synchronousForTests: false,
            ownerContext
                ?? SynchronizationContext.Current as DispatcherSynchronizationContext
                ?? new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher),
            // The owner dispatcher, named only when this document CHOSE the
            // context and therefore knows it (IPF-3): the current dispatcher
            // context is WPF's own, installed on its dispatcher's thread,
            // and the fallback is built over this thread's dispatcher. A
            // context handed in names no dispatcher and posts through itself.
            ownerContext is null ? Dispatcher.CurrentDispatcher : null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(viewState);
        ArgumentNullException.ThrowIfNull(isEffectiveActive);
        ArgumentNullException.ThrowIfNull(verbosity);
        // A-1 as amended (W6-2 PR B2, B2D-1): the view state is the
        // WORKSPACE's, handed in; a bare document in a fact is its own
        // seated one.
        _isSeated = isSeated ?? (static () => true);
        _session = session;
        _announcer = announcer;
        _isEffectiveActive = isEffectiveActive;
        // C-9: the level is read LIVE from the workspace's preferences at
        // every render, and a change is forwarded as this document's own
        // Verbosity change (the table view re-labels on it); a bare
        // document in a fact reads its own function.
        _preferences = preferences;
        _verbosity = preferences is null ? verbosity : () => preferences.Verbosity;
        if (preferences is not null)
        {
            preferences.PropertyChanged += OnPreferencesChanged;
            preferences.DisplayChanged += OnDisplayChanged;
        }
        // Rule A (IPA-6): the lifecycle's generation, read when a body is
        // started and again at dispatch; a host without a lifecycle (a
        // fact's bare document, the runner) reads a constant.
        _lifecycleGeneration = lifecycleGeneration ?? (static () => 0);
        // Design B: every ordered inventory a core vector, fetched ONCE
        // per document — the columns, the default sort, the three
        // per-kind action vectors, the mode switcher's items.
        ColumnSpecs = SlateUniffiMethods.GraphTableColumns();
        DefaultSort = SlateUniffiMethods.GraphTableDefaultSort();
        SurfaceModes = SlateUniffiMethods.GraphSurfaceModes();
        _actionsByKind = new Dictionary<GraphNodeKind, IReadOnlyList<GraphRowActionSpec>>
        {
            [GraphNodeKind.Note] = FetchRowActions(GraphNodeKind.Note),
            [GraphNodeKind.Attachment] = FetchRowActions(GraphNodeKind.Attachment),
            [GraphNodeKind.Ghost] = FetchRowActions(GraphNodeKind.Ghost),
        };
        // COUNTED through the wrapper, never a literal (IPC-5): a fourth
        // crossing anywhere would show here.
        ActionInventoryCrossings = CrossingsForTests["graph_row_actions"];
        ViewState = viewState;
        Navigator = navigator;
        _publication = GraphPublication.Initial(
            new GraphVisibilityQuery(ViewState.Filter, ViewState.NameQuery, ViewState.KindOnly),
            DefaultSort);
        // C-8: the TABLE's readback seam, installed at the seat and cleared
        // at retirement — the navigator chooses the seam by the view state's
        // Mode; a bare document (a fact's) has no navigator and no seam.
        _tableReadback = TableWhereAmI;
        navigator?.InstallTableReadback(_tableReadback);
        // W6-2 PR D (Term G4, DD-9): the motion policy — the system's unless a
        // fact injects one — observed for the flip; the view state observed
        // for the rebuild (Term G6) and the epoch (Term G3).
        _motion = motionPolicy ?? GraphMotionPolicy.OfTheSystem();
        _ownsMotion = motionPolicy is null;
        _motion.Changed += OnMotionChanged;
        ViewState.PropertyChanged += OnViewStateChanged;
    }

    // --- The fetched-once inventories (design B) ------------------------

    public IReadOnlyList<GraphTableColumnSpec> ColumnSpecs { get; private set; }

    /// <summary>Test seam (contract A-6; IPA-11): swap the column inventory
    /// so a fact can prove the cell lookup keys by the VECTOR — a reordered
    /// vector moves the index the lookup answers.</summary>
    internal void ReplaceColumnInventoryForTests(IReadOnlyList<GraphTableColumnSpec> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ColumnSpecs = columns;
    }

    public GraphTableSort DefaultSort { get; }

    public IReadOnlyList<GraphSurfaceModeSpec> SurfaceModes { get; }

    /// <summary>How many `graph_row_actions` crossings the document has
    /// made — three, whatever the row count (contract A-8's fact).</summary>
    public int ActionInventoryCrossings { get; }

    public IReadOnlyList<GraphRowActionSpec> ActionSpecs(GraphNodeKind kind) => _actionsByKind[kind];

    /// <summary>The union of the three vectors in core's order
    /// (<c>GraphRowAction::ALL</c>): the ONE list the grid takes, each
    /// action visible per row by its kind's vector (contract A-8).</summary>
    public IReadOnlyList<GraphRowActionSpec> ActionUnion()
    {
        var union = new List<GraphRowActionSpec>();
        foreach (GraphRowAction action in Enum.GetValues<GraphRowAction>())
        {
            foreach (IReadOnlyList<GraphRowActionSpec> vector in _actionsByKind.Values)
            {
                GraphRowActionSpec? spec = vector.FirstOrDefault(s => s.Action == action);
                if (spec is not null)
                {
                    union.Add(spec);
                    break;
                }
            }
        }
        return union;
    }

    public bool ActionAppliesTo(GraphRowAction action, GraphNodeKind kind) =>
        _actionsByKind[kind].Any(spec => spec.Action == action);

    // --- The ONE cell lookup (contract A-6; the round-4 ledger's IGA-69) ---

    /// <summary>The position of a column in the fetched vector — never a
    /// typed index; the census asserts no other source under
    /// <c>Graph/</c> reads a row's cells.</summary>
    public int CellIndexOf(GraphTableColumn column)
    {
        for (int index = 0; index < ColumnSpecs.Count; index++)
        {
            if (ColumnSpecs[index].Column == column)
            {
                return index;
            }
        }
        throw new InvalidOperationException($"core's column vector carries no {column}");
    }

    public string CellOf(GraphTableRow row, GraphTableColumn column)
    {
        ArgumentNullException.ThrowIfNull(row);
        int index = CellIndexOf(column);
        return index < row.Cells.Length ? row.Cells[index] : string.Empty;
    }

    public string CellAt(GraphTableRow row, int vectorIndex)
    {
        ArgumentNullException.ThrowIfNull(row);
        return vectorIndex >= 0 && vectorIndex < row.Cells.Length ? row.Cells[vectorIndex] : string.Empty;
    }

    // --- State ------------------------------------------------------------

    public GraphViewState ViewState { get; }

    /// <summary>Contract A-7's write, GUARDED (W6-2 PR B2, Term 15, IGJ-6):
    /// the table's current row selects through the document, which refuses
    /// once retired, when it is not the workspace's seated document, or when
    /// its current snapshot lacks the key — so a retained view over a closed
    /// tab, or a stale document beside a re-seated one, cannot move the
    /// workspace's state. Returns whether the key was written.</summary>
    public bool SelectRow(string stableKey)
    {
        ArgumentNullException.ThrowIfNull(stableKey);
        if (_retired || !_isSeated())
        {
            return false;
        }
        GraphPublication publication = Publication;
        if (!publication.HoldsSnapshot || !publication.ContainsNode(stableKey))
        {
            return false;
        }
        ViewState.SelectedKey = stableKey;
        return true;
    }

    /// <summary>The residue (contract A-10's census): the one member that
    /// hands out the announcer, for the facts — production reaches the
    /// relay through the named seams below only.</summary>
    internal GraphAnnouncer AnnouncerForTests => _announcer;

    /// <summary>The grid's seam (contract A-10): the substrate's canonical
    /// events ride the graph relay uncoalesced, with core's priority.</summary>
    internal Action<A11yEvent> GridRelaySeam => _announcer.Relay;

    /// <summary>The surface's adoption line (contract A-5): the sort's
    /// <c>GridSorted</c>, relayed once when the publication adopts it.</summary>
    internal void RelayGridEvent(A11yEvent @event)
    {
        if (!_retired)
        {
            _announcer.Relay(@event);
        }
    }

    /// <summary>The one publication record (rule A): installed in ONE
    /// property swap on the owner context; every observer binds from it.</summary>
    public GraphPublication Publication
    {
        get => _publication;
        private set => SetField(ref _publication, value);
    }

    /// <summary>Raised after every install, with what it answered.</summary>
    public event Action<GraphPublicationInstall>? PublicationInstalled;

    /// <summary>The workspace's navigator (W6-2 PR C, C-1): the surface
    /// reaches it through the document, as the canvas surface does; a
    /// bare document in a fact has none.</summary>
    internal GraphNavigator? Navigator { get; }

    /// <summary>C-5: whether a needle NARROWS — core's trim, 0b-6's
    /// predicate: an empty label matches a needle exactly when core's
    /// trimmed needle is empty, so no host trim touches the needle.</summary>
    public static bool NeedleNarrows(string needle)
    {
        ArgumentNullException.ThrowIfNull(needle);
        return !SlateUniffiMethods.GraphLabelMatches(string.Empty, needle);
    }

    // --- Rule F, Term F1: the landing's record (contract C-17) -------------

    private GraphFocusRequest? _focusRequest;

    /// <summary>The pending landing — an addressed RESTORATION onto
    /// whatever the lineage settles to, no query and no sequence on the
    /// record; absent once the document is retired.</summary>
    public GraphFocusRequest? FocusRequest
    {
        get => _retired ? null : _focusRequest;
        private set => SetField(ref _focusRequest, value);
    }

    /// <summary>Raise the landing for a pane (the shell's routes, the
    /// presenter's RequestProjectionFocus); a later request supersedes
    /// by reference identity.</summary>
    internal void RequestFocusLanding(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!_retired)
        {
            // By REFERENCE (Term F1): the record has value equality, so a second
            // request for the same owner would read as no change through
            // SetField — assigned and raised unconditionally, every raise is a
            // new request and Term F2's own-change trigger fires (TGC-7).
            _focusRequest = new GraphFocusRequest(owner);
            OnPropertyChanged(nameof(FocusRequest));
        }
    }

    /// <summary>Completion, only on a delivered quiescent landing (Term F4).</summary>
    internal void CompleteFocus(GraphFocusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (ReferenceEquals(_focusRequest, request))
        {
            FocusRequest = null;
        }
    }

    public bool IsRetired => _retired;

    /// <summary>The graph tab EFFECTIVE — its group the active group (Term
    /// F2): a graph visible in another pane never takes the keys.</summary>
    internal bool IsEffective => _isEffectiveActive();

    public ulong SeqForTests => _seq;

    public GraphTableSort? RequestedSortForTests => _requestedSort;

    public ulong HighWaterForTests => _highWater;

    /// <summary>The verbosity the row copy is rendered at — the
    /// preferences' live level (C-9; AD-6 until PR C).</summary>
    public GraphVerbosity Verbosity => _verbosity();

    private void OnPreferencesChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // No retirement guard here: the retirement UNSUBSCRIBES, and that
        // is the fact a retired document forwards nothing pins.
        if (e.PropertyName == nameof(GraphPreferencesViewModel.Verbosity))
        {
            OnPropertyChanged(nameof(Verbosity));
        }
    }

    /// <summary>W6-2 PR E (Term Z2): the preferences' real display change,
    /// forwarded as this document's own <see cref="DiagramDisplay"/> change —
    /// the renderer redraws on it; no epoch (ED-Q6).</summary>
    private void OnDisplayChanged() => OnPropertyChanged(nameof(DiagramDisplay));

    // --- Seams the workspace wires (contracts A-8, A-9) --------------------

    /// <summary>Open the row's note in the addressed pane; the workspace
    /// posts the shell's <c>OpenedFile</c> on success.</summary>
    internal Action<GraphTableRow, WorkspaceOpenTarget>? OpenRowFromSurface { get; set; }

    /// <summary>Select the note in the files sidebar (the mac's meaning).</summary>
    internal Action<string>? RevealRowFromSurface { get; set; }

    /// <summary>PR B fills this; null keeps the action listed and disabled.</summary>
    internal Action<GraphTableRow>? ShowConnectionsFromSurface { get; set; }

    /// <summary>The create funnel (contract A-8): the workspace runs the
    /// two-phase note creation under its own lifecycle, independent of
    /// this document's liveness, given the ghost's path.</summary>
    internal Action<string>? CreateNoteFromSurface { get; set; }

    /// <summary>Host admission for Create note (0bD-8): a reason means
    /// disabled; null means admitted. Windows has no structural gate
    /// today, so the default admits.</summary>
    internal Func<string?>? CreateAdmissionReason { get; set; }

    /// <summary>W6-2 PR E (Term I2): the header toggle's route into the
    /// workspace's ToggleGraphInspector and the shown state it binds — wired
    /// by the workspace; a bare document has neither (the toggle unchecked,
    /// a click nothing).</summary>
    internal Action? ToggleInspectorFromSurface { get; set; }

    internal Func<bool>? InspectorShownFromSurface { get; set; }

    /// <summary>The toggle's checked state: the workspace's IsGraphInspectorShown, read live.</summary>
    internal bool IsInspectorShown => InspectorShownFromSurface?.Invoke() ?? false;

    /// <summary>The workspace's pane and leaf setters forward their change
    /// here; the surface's header re-reads the state.</summary>
    internal void NotifyInspectorShownChanged() => OnPropertyChanged(nameof(IsInspectorShown));

    /// <summary>The header toggle's click: the workspace's route; true when
    /// the pane was SHOWN and is now hidden — the surface then returns the
    /// keys to its projection (E-D5).</summary>
    internal bool ToggleInspectorFromHeader()
    {
        bool wasShown = IsInspectorShown;
        ToggleInspectorFromSurface?.Invoke();
        return wasShown && !IsInspectorShown;
    }

    /// <summary>Test seam: runs inside the worker AFTER the fetch and
    /// before the envelope returns — the canvas publish-gate shape, for
    /// the gated generation fact.</summary>
    internal Action? FetchGateForTests { get; set; }

    /// <summary>Test seam (IPH-2-3): invoked at the head of the epoch's
    /// topology compute — a throw is the injected fetch failure.</summary>
    internal Action? TopologyGateForTests { get; set; }

    /// <summary>Test seam: FFI crossings the document made, by name.</summary>
    internal Dictionary<string, int> CrossingsForTests { get; } = new(StringComparer.Ordinal)
    {
        ["graph_snapshot"] = 0,
        ["graph_table_rows"] = 0,
        ["graph_generation"] = 0,
        ["graph_row_actions"] = 0,
        ["graph_preset_outcome"] = 0,
        // W6-2 PR D (Term G8): the layout's crossings, per path.
        ["start_graph_layout"] = 0,
        ["layout_node_ids"] = 0,
        ["layout_edges"] = 0,
        ["layout_node_metadata"] = 0,
        ["layout_generation"] = 0,
        ["layout_tick"] = 0,
        ["layout_run_to_convergence"] = 0,
        ["layout_refresh"] = 0,
        ["layout_pin_node"] = 0,
        ["layout_unpin_node"] = 0,
        ["layout_set_forces"] = 0,
        ["graph_topology"] = 0,
    };

    /// <summary>Test seam (IGT-1): runs inside the build's compute AFTER the
    /// model's registration in the unseated set and before the compute
    /// returns — a fact parks here, retires the workspace, and proves the
    /// withdrawn apply leaves no handle.</summary>
    internal Action? DiagramRegisteredGateForTests { get; set; }

    /// <summary>The per-kind action vector, fetched ONCE per kind at
    /// construction and COUNTED (IPC-5).</summary>
    private IReadOnlyList<GraphRowActionSpec> FetchRowActions(GraphNodeKind kind)
    {
        CrossingsForTests["graph_row_actions"]++;
        return SlateUniffiMethods.GraphRowActions(kind);
    }

    // --- The row copy (contract A-6) --------------------------------------

    /// <summary>0a's row copy from the record: the label, the kind, the
    /// degrees, the ghost's references, and no focused relationship.</summary>
    public GraphRowCopy RowCopy(GraphTableRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new GraphRowCopy(
            row.Label,
            row.Kind,
            row.LinksIn,
            row.LinksOut,
            row.Kind == GraphNodeKind.Ghost ? row.LinksIn + row.EmbedsIn : 0,
            false);
    }

    /// <summary>The row's UIA Name and its row-move description: P1's
    /// copy at the set verbosity, rendered without posting.</summary>
    public string RowName(GraphTableRow row) =>
        GraphAnnouncer.RenderLabel(new GraphA11yEvent.GraphRow(Verbosity, RowCopy(row)));

    // --- Announcements the workspace asks for (rule L, Term 6) -------------

    // --- Where-am-I (contract C-8) -------------------------------------------

    /// <summary>The TABLE's readback: answers only while the lineage is
    /// QUIESCENT and the publication CURRENT — a READY or EMPTY record held
    /// (EMPTY holds the snapshot too: the ninth witness's state), its query
    /// the view state's, nothing in flight (Term Q7; IGO-29) — composing ONE
    /// GraphWhereAmI: the shared key's visible row, else (only with NO key)
    /// the current native seat or unambiguous sole row, rendered the
    /// diagram's way — references its in-links, no embed, its component — else
    /// NoSelection; NO zoom clause (0a-2b as amended); UnresolvedOnly under
    /// the kind overlay, else Normal from the view state's filter; the raw
    /// needle as the name filter (core trims, 0a-6).</summary>
    internal GraphA11yEvent.GraphWhereAmI? TableWhereAmI()
    {
        // No retirement guard: the retirement CLEARS the seam, and that is
        // what the no-tab fact pins (a retired document is never asked).
        if (IsRequestInFlight)
        {
            return null;
        }
        GraphPublication publication = Publication;
        // The snapshot check is Term Q7's "a READY record HELD", not a null
        // guard for the selection below: READY and EMPTY are reachable only
        // through FromPair (a snapshot by signature) and WithRows (the held
        // one, the receiver refusing a rows result with no snapshot), so a
        // record in either state always carries it. Keeping the check states
        // the term the readback answers under; it never short-circuits a
        // state the rows could have answered from.
        if (publication.State is not (GraphLoadState.Ready or GraphLoadState.Empty)
            || publication.Snapshot is null
            || publication.Query != new GraphVisibilityQuery(ViewState.Filter, ViewState.NameQuery, ViewState.KindOnly))
        {
            return null;
        }
        GraphTableRow? selected = null;
        if (ViewState.SelectedKey is { } key)
        {
            // The SHOWN rows, not the snapshot's nodes — the mac's twin
            // (`AppState+GraphDiagram.swift:311-320`) and TGC-1's recorded
            // reading of C-8: a shown row obeys the query by construction,
            // so 0a-2b's invariants hold without a second visibility
            // predicate. A-7 KEEPS the shared key when an overlay merely
            // hides its row, so reading the snapshot answered `Node Alpha`
            // over a table that does not show Alpha (IPG-3); the reader is
            // told `No node selected` instead.
            foreach (GraphTableRow row in publication.Rows)
            {
                if (string.Equals(row.StableKey, key, StringComparison.Ordinal))
                {
                    selected = row;
                    break;
                }
            }
        }
        else
        {
            // F5's silent landing never writes SelectedKey or announces a
            // move. Read its native seat; without one, a single shown row
            // is unambiguous. Never guess the first of several rows.
            selected = Navigator?.ReadTableSeat(this, publication);
            selected ??= publication.Rows.Count == 1 ? publication.Rows[0] : null;
        }
        GraphWhereAmISelection selection = selected is { } current
            ? new GraphWhereAmISelection.Node(
                new GraphRowCopy(current.Label, current.Kind, current.LinksIn, current.LinksOut, current.LinksIn, false),
                current.Component)
            : new GraphWhereAmISelection.NoSelection();
        GraphFilter backend = ViewState.Filter;
        GraphWhereAmIFilter filter = ViewState.KindOnly == GraphNodeKind.Ghost
            ? new GraphWhereAmIFilter.UnresolvedOnly()
            : new GraphWhereAmIFilter.Normal(backend.OrphansOnly, backend.IncludeAttachments, backend.IncludeGhosts);
        return new GraphA11yEvent.GraphWhereAmI(selection, null, filter, ViewState.NameQuery);
    }

    /// <summary>The announcement half of the verb (C-8): the ONE event,
    /// through the relay while the graph is effective — the boundary the
    /// announcement-seam census names.</summary>
    internal void AnnounceWhereAmI(GraphA11yEvent.GraphWhereAmI @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        AnnounceIfEffective(@event);
    }

    /// <summary>A status the cause owes, posted through the relay.</summary>
    internal void AnnounceStatus(GraphStatusNote note)
    {
        if (!_retired)
        {
            _announcer.Announce(new GraphA11yEvent.GraphStatus(note));
        }
    }

    private void AnnounceIfEffective(GraphA11yEvent @event)
    {
        if (!_retired && _isEffectiveActive())
        {
            _announcer.Announce(@event);
        }
    }

    /// <summary>The filter count's seam (0a-9, A-10 as amended; rule Q
    /// Term Q6): gated at enqueue AND at fire by the effective predicate
    /// and the token's currency — a count queued for one query drops when
    /// another is typed, and a preset's pair drops a count queued before
    /// it.</summary>
    private void AnnounceFilterCountIfEffective(GraphA11yEvent.GraphFilterCount @event, GraphLoadToken token)
    {
        if (!_retired && _isEffectiveActive())
        {
            _announcer.AnnounceGatedFilterCount(@event, () => !_retired && _isEffectiveActive() && _seq == token.Seq);
        }
    }

    /// <summary>C-6: the count region's text — the same GraphFilterCount
    /// the relay speaks, rendered by the one renderer from the CURRENT
    /// publication at each install; empty under LOADING and ERROR.</summary>
    public string FilterCountText
    {
        get => _filterCountText;
        private set => SetField(ref _filterCountText, value);
    }

    // --- W6-2 PR D, rule M: the mode switch (Terms M1–M3) ---------------------

    private bool _diagramLoading;
    private string? _diagramError;
    private bool _hasLiveDiagram;

    /// <summary>Rule G, Term G2: a build in flight — the projection cluster
    /// shows the diagram's state host named "Laying out graph." (T20)
    /// meanwhile; Term M4 reads "quiescent" as "not building".</summary>
    public bool DiagramLoading
    {
        get => _diagramLoading;
        private set => SetField(ref _diagramLoading, value);
    }

    /// <summary>Term G2: a failed build's humanised message; the state host
    /// reads "Graph diagram error: ⟨e⟩" (T19).</summary>
    public string? DiagramError
    {
        get => _diagramError;
        private set => SetField(ref _diagramError, value);
    }

    /// <summary>Term M3: a model is live — installed by Term G2's apply,
    /// dropped by Term G7 — so the renderer shows and the four viewport
    /// verbs admit.</summary>
    public bool HasLiveDiagram
    {
        get => _hasLiveDiagram;
        private set => SetField(ref _hasLiveDiagram, value);
    }

    /// <summary>Term M3's "Diagram effective": the seated document effective,
    /// Diagram mode, a model live — the mac's <c>graphDiagramZoomActive</c>.</summary>
    internal bool IsDiagramEffective =>
        !_retired && _isSeated() && _isEffectiveActive() && ViewState.Mode == GraphSurfaceMode.Diagram && HasLiveDiagram;

    /// <summary>Term M3: raised at the model's install, at teardown and at
    /// every effectiveness edge (the workspace forwards its own); the four
    /// viewport commands' <c>CanExecute</c> re-evaluates through it.</summary>
    internal event Action? DiagramAvailabilityChanged;

    internal void NotifyDiagramAvailabilityChanged()
    {
        DiagramAvailabilityChanged?.Invoke();
        // Term V3: the four viewport commands' CanExecute follows the same edge.
        Navigator?.NotifyDiagramAvailabilityChanged();
    }

    /// <summary>W6-2 PR E (Term X1): the inspector's backend filter change —
    /// PR E's named fourth caller of ApplyQuery (C-4): the view state's
    /// query rewritten with the overlay CLEARED (the mac's
    /// <c>setGraphTableFilter</c>), then rule Q's Filter arm (Term Q4: a pair
    /// under FilterCount, a Preset policy in flight not inherited, the pending
    /// sort carried). Refused — nothing written, nothing requested — when
    /// retired or unseated (the <see cref="SelectRow"/> guard) or for the
    /// current flags (Term X4); the caller persists the flags only on true.
    /// In Diagram mode the view state's write is what rebuilds the model
    /// (Term X2; Term G6) — nothing here touches the diagram.</summary>
    public bool ChangeFilter(GraphFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (_retired || !_isSeated() || filter == ViewState.Filter)
        {
            return false;
        }
        ViewState.ApplyQuery(new GraphVisibilityQuery(filter, ViewState.NameQuery, null));
        return Request(new GraphRequest.Filter(filter));
    }

    /// <summary>W6-2 PR E (Terms K2 (iii), K4): the inspector's forces edit
    /// reaching the kernel — over a LIVE model, <c>SetForces</c> through the
    /// gate (Term G7's synchronous mutator; refused, not thrown, once
    /// retired), and when admitted: the relay's pending settle dropped (a
    /// settle queued by the previous run must not speak for this one;
    /// IGX-2), the settle line ARMED (Term G4; the mac's
    /// <c>graphForcesSettlePending</c>) and the run RESTARTED, so the
    /// re-heated kernel is ticked to its predicate or ceiling (ED-5). A
    /// no-op — nothing armed, nothing ticked — for the model's current
    /// forces (nothing re-heats), with no live model (Table mode, a failed
    /// build) and under a build in flight, whose install re-reads the
    /// preferences' forces and arms there (Term G2; IGX-1). True iff the
    /// edit reached a live kernel.</summary>
    public bool ApplyForces(GraphForcesConfig forces)
    {
        ArgumentNullException.ThrowIfNull(forces);
        if (_retired || _diagramModel is not { } model)
        {
            return false;
        }
        LayoutForces layout = ForcesOf(forces);
        if (layout == model.Forces || !model.SetForces(layout))
        {
            return false;
        }
        _announcer.DropPendingSettle();
        SettleAnnouncementArmed = true;
        model.Driver.StartSettle();
        return true;
    }

    /// <summary>Term M1: the ONE writer of Mode beside the workspace's seed —
    /// refused when retired or unseated (the <see cref="SelectRow"/> guard),
    /// a no-op for the current mode; otherwise the field, the persisted mode
    /// (Term W7, the mac's <c>setGraphMode</c>), the mode line, Term M2's
    /// effects, then the two availability edges.</summary>
    public bool SetMode(GraphSurfaceMode mode)
    {
        if (_retired || !_isSeated() || ViewState.Mode == mode)
        {
            return false;
        }
        ViewState.Mode = mode;
        _preferences?.SetMode(mode);
        AnnounceMode(mode);
        if (mode == GraphSurfaceMode.Diagram)
        {
            EnterDiagram();
        }
        else
        {
            TeardownDiagram();
        }
        Navigator?.NotifyWhereAmIAvailabilityChanged();
        NotifyDiagramAvailabilityChanged();
        return true;
    }

    /// <summary>Term M2 / Term G2: entering Diagram starts the build; the
    /// state host shows "Laying out graph." until a model lands.</summary>
    internal void EnterDiagram() => BuildDiagram();

    /// <summary>Term M1's second half (DD-18): a persisted Diagram mode builds
    /// at the SEAT and speaks no mode line — the workspace's attach funnel
    /// calls it once the persisted query is re-applied; nothing when a build
    /// is in flight or a model is live (the mac's <c>ensureGraphDiagram</c>).</summary>
    internal void EnsureDiagram()
    {
        if (_retired || ViewState.Mode != GraphSurfaceMode.Diagram || DiagramLoading || HasLiveDiagram)
        {
            return;
        }
        EnterDiagram();
    }

    /// <summary>Term G7's order, the document's part, then the mode's states:
    /// a build in flight superseded (the sequence), the live model dropped in
    /// the gate's order, the readback seam cleared (Term M3), the diagram's
    /// states cleared, the availability re-evaluated; the switch to Table and
    /// the retirement call it.</summary>
    internal void TeardownDiagram()
    {
        _diagramSeq++;
        DropModel();
        Navigator?.InstallDiagramReadback(null);
        SettleAnnouncementArmed = false;
        HasLiveDiagram = false;
        DiagramLoading = false;
        DiagramError = null;
        NotifyDiagramAvailabilityChanged();
    }

    // --- W6-2 PR D, rule G: the diagram lineage (Terms G1–G8) -----------------------

    /// <summary>Term G1: the ONE model, null when no diagram is live; the
    /// navigator, the surface and the facts reach it here.</summary>
    internal GraphDiagramModel? DiagramModel => _diagramModel;

    /// <summary>Term G3: an epoch's topology landed on the model — the
    /// renderer rebuilds its visible set from <see cref="GraphDiagramModel.Topology"/>.</summary>
    internal event Action? DiagramTopologyChanged;

    /// <summary>Term G4: PR E's forces edit arms it; the build's and the
    /// refresh's convergence speak nothing; the teardown disarms it.</summary>
    internal bool SettleAnnouncementArmed { get; set; }

    /// <summary>The build's carrier from the compute to the apply: the
    /// sequence and the filter captured, the forces the layout was seeded
    /// with, the model (null on a failure or a retirement at registration)
    /// and the failure's humanised message.</summary>
    private sealed record GraphDiagramBuild(ulong Sequence, GraphFilter Filter, LayoutForces Forces, GraphDiagramModel? Model, string? Error);

    /// <summary>The refresh's answer (Term G6): null when the gate refused;
    /// a null frame when the generation is unchanged; else the frame and the
    /// three reads; a failure carried as a value (IGQ-3).</summary>
    private sealed record GraphDiagramRefresh(LayoutFrame? Frame, GraphDiagramTopologyRead? Read, Exception? Failure);

    private static readonly Lazy<GraphConfig> DefaultConfig = new(SlateUniffiMethods.GraphConfigDefault);

    /// <summary>The mac's <c>layoutForces</c>: the persisted forces onto the
    /// kernel's.</summary>
    internal static LayoutForces ForcesOf(GraphForcesConfig forces)
    {
        ArgumentNullException.ThrowIfNull(forces);
        return new LayoutForces((float)forces.Center, (float)forces.Repel, (float)forces.Link, (float)forces.LinkDistance);
    }

    private GraphConfig CurrentConfig => _preferences?.CurrentConfig ?? DefaultConfig.Value;

    private LayoutForces CurrentForces() => ForcesOf(CurrentConfig.Forces);

    /// <summary>
    /// Term G2 — the build is a load through the scheduler: the sequence
    /// bumped and captured; the filter, the forces and core's defaults
    /// captured on the dispatcher; ONE compute crossing <c>StartGraphLayout</c>
    /// then the four reads THROUGH the gate — the model constructed ON THE
    /// POOL around the fresh handle, so the gate is born with it (IGT-1,
    /// IGT-2) — and REGISTERED in the unseated set under the document's lock,
    /// or retired at once when the document is already retired under it; a
    /// failure returns the humanised message and no model.
    /// </summary>
    private void BuildDiagram()
    {
        ulong sequence = ++_diagramSeq;
        GraphFilter filter = ViewState.Filter;
        LayoutForces forces = CurrentForces();
        var config = new LayoutConfig();
        DiagramError = null;
        DiagramLoading = true;
        StartWorkAlwaysAsync(
            () =>
            {
                GraphDiagramModel? model = null;
                try
                {
                    lock (CrossingsForTests)
                    {
                        CrossingsForTests["start_graph_layout"]++;
                    }
                    // The fresh handle flows straight into the model's constructor:
                    // no raw session exists outside a model at any instant.
                    model = new GraphDiagramModel(
                        _session.StartGraphLayout(filter, forces, config),
                        filter,
                        forces,
                        sequence,
                        StartWorkAlwaysAsync,
                        () => _motion.ReduceMotion,
                        CrossingsForTests);
                    GraphDiagramTopologyRead? read = model.WithSession(
                        session =>
                        {
                            model.Count("layout_node_ids");
                            model.Count("layout_edges");
                            model.Count("layout_node_metadata");
                            model.Count("layout_generation");
                            return new GraphDiagramTopologyRead(session.NodeIds(), session.Edges(), session.NodeMetadata(), session.Generation());
                        },
                        null);
                    model.Adopt(read ?? throw new InvalidOperationException("a fresh model refused its own build"));
                    FetchGateForTests?.Invoke();
                }
                catch (VaultException exception)
                {
                    model?.Retire();
                    return new GraphDiagramBuild(sequence, filter, forces, null, HumanReadable(exception));
                }
                catch (Exception exception) when (exception is InvalidOperationException or System.IO.IOException)
                {
                    model?.Retire();
                    return new GraphDiagramBuild(sequence, filter, forces, null, HumanReadable(exception));
                }
                lock (_diagramLock)
                {
                    if (_retired)
                    {
                        // Retired at once: the gate frees the handle, nothing admitted.
                        model.Retire();
                        return new GraphDiagramBuild(sequence, filter, forces, null, null);
                    }
                    _ = _unseatedBuilds.Add(model);
                }
                DiagramRegisteredGateForTests?.Invoke();
                return new GraphDiagramBuild(sequence, filter, forces, model, null);
            },
            InstallBuild);
    }

    /// <summary>Term G2's apply: the model out of the set; installed ONLY when
    /// the document is live, seated, still in Diagram mode, the sequence the
    /// captured one AND the captured filter the view state's NOW (IGQ-2) —
    /// else retired, and on a filter refusal built once more under the
    /// current filter; the forces re-read at the install; a failure installs
    /// the error state and no model.</summary>
    private void InstallBuild(GraphDiagramBuild build)
    {
        GraphDiagramModel? model = build.Model;
        if (model is not null)
        {
            lock (_diagramLock)
            {
                _ = _unseatedBuilds.Remove(model);
            }
        }
        if (_retired || build.Sequence != _diagramSeq || !_isSeated() || ViewState.Mode != GraphSurfaceMode.Diagram)
        {
            // Superseded, torn down, retired or unseated: the gate frees the handle.
            model?.Retire();
            return;
        }
        if (model is null)
        {
            DiagramLoading = false;
            DiagramError = build.Error;
            NotifyDiagramAvailabilityChanged();
            return;
        }
        if (build.Filter != ViewState.Filter)
        {
            // IGQ-2: the guard, not the trigger, refuses the stale build — one
            // rebuild under the current filter, its own sequence.
            model.Retire();
            BuildDiagram();
            return;
        }
        LayoutForces forces = CurrentForces();
        if (forces != build.Forces && model.SetForces(forces))
        {
            // PR E's edit during the build is not lost (Term G2) — and the run
            // this install starts speaks the settle for it (W6-2 PR E, Term
            // K2; IGX-1, E-D7): armed here, before the install's StartSettle.
            SettleAnnouncementArmed = true;
        }
        _diagramModel = model;
        model.Driver.Converged += OnSettleConverged;
        DiagramLoading = false;
        HasLiveDiagram = true;
        // Term N6 / Term M3: the diagram's readback seam installed at the
        // model's install (cleared by the teardown's order).
        Navigator?.InstallDiagramReadback(DiagramWhereAmI);
        OpenEpoch();
        model.Driver.StartSettle();
        NotifyDiagramAvailabilityChanged();
    }

    /// <summary>Term G6's rebuild: the live model torn down (Term G7) or the
    /// build in flight superseded, then Term G2 under the current filter.</summary>
    private void RebuildDiagram()
    {
        DropModel();
        BuildDiagram();
    }

    /// <summary>Term G7 in order: the settle run's token cancelled, the settle
    /// announcement disarmed, the readback seam cleared, the model dropped
    /// from the document, then the gate RETIRED — the handle freed at the
    /// count's zero, at once when nothing is in flight.</summary>
    private void DropModel()
    {
        if (_diagramModel is not { } model)
        {
            return;
        }
        model.Driver.Stop();
        SettleAnnouncementArmed = false;
        // W6-2 PR E (Term K4; IGY-1): the disarm's other half — a settle
        // queued by this model's last run, still inside the relay's window,
        // must not speak for a model that is gone.
        _announcer.DropPendingSettle();
        Navigator?.InstallDiagramReadback(null);
        model.Driver.Converged -= OnSettleConverged;
        _diagramModel = null;
        _epoch = null;
        _refreshRunning = false;
        _refreshAgain = false;
        HasLiveDiagram = false;
        model.Retire();
    }

    /// <summary>Term G3: the epoch key — the model, its generation, the view
    /// state's query and the topology-relevant config (its groups; IGT-4) —
    /// compared by value; a new key fetches the topology ONCE through the
    /// scheduler. A display change and a verbosity change never reach here.</summary>
    private void OpenEpoch()
    {
        if (_diagramModel is not { } model)
        {
            return;
        }
        var query = new GraphVisibilityQuery(ViewState.Filter, ViewState.NameQuery, ViewState.KindOnly);
        GraphGroup[] groups = [.. ViewState.Groups];
        if (_epoch is { } current
            && ReferenceEquals(current.Model, model)
            && current.Generation == model.Generation
            && current.Query == query
            && current.Groups.AsSpan().SequenceEqual(groups))
        {
            return;
        }
        _epoch = (model, model.Generation, query, groups);
        FetchTopology(model, ++_epochSeq, query, CurrentConfig with { Groups = groups });
    }

    /// <summary>Term G3's compute crosses <c>GraphTopology</c>; the apply
    /// accepts the record only when its generation equals the model's
    /// (design A) and the epoch is still current — else drops it and leaves
    /// the previous epoch's set standing until the refresh adopts.</summary>
    private void FetchTopology(GraphDiagramModel model, ulong epoch, GraphVisibilityQuery query, GraphConfig config)
    {
        StartWorkAlwaysAsync(
            () =>
            {
                try
                {
                    TopologyGateForTests?.Invoke();
                    lock (CrossingsForTests)
                    {
                        CrossingsForTests["graph_topology"]++;
                    }
                    return (GraphTopology?)_session.GraphTopology(query, config);
                }
                catch (VaultException exception)
                {
                    HostLog.Write(HostDiagnosticEvent.GraphTopologyFetchFailed, exception);
                    return null;
                }
            },
            topology =>
            {
                if (topology is null)
                {
                    // IPH-2-3: the epoch was marked current BEFORE its fetch;
                    // a failed fetch leaves it UNFETCHED, so the next request
                    // for the same key — a probe's refresh, a view-state
                    // change — fetches again instead of returning early.
                    if (epoch == _epochSeq && _epoch is { } current && ReferenceEquals(current.Model, model))
                    {
                        _epoch = null;
                    }
                    return;
                }
                if (_retired || !ReferenceEquals(_diagramModel, model) || epoch != _epochSeq || topology.Generation != model.Generation)
                {
                    return;
                }
                model.Topology = topology;
                DiagramTopologyChanged?.Invoke();
            });
    }

    /// <summary>Term G6's refresh, from the probe's one line: ONE in flight per
    /// model — a probe during it sets RefreshAgain, which every terminal path
    /// consumes; the compute crosses <c>Refresh</c> then, on a non-null
    /// answer, the three reads (IGS-3), a failure caught and returned as a
    /// value (IGQ-3).</summary>
    private void RefreshDiagram()
    {
        if (_retired || _diagramModel is not { } model)
        {
            return;
        }
        if (_refreshRunning)
        {
            _refreshAgain = true;
            return;
        }
        _refreshRunning = true;
        StartWorkAlwaysAsync(
            () => model.WithSession(
                session =>
                {
                    try
                    {
                        // The fetch gate's throw is the injected failure (D-5).
                        FetchGateForTests?.Invoke();
                        model.Count("layout_refresh");
                        LayoutFrame? frame = session.Refresh();
                        if (frame is null)
                        {
                            return new GraphDiagramRefresh(null, null, null);
                        }
                        model.Count("layout_node_ids");
                        model.Count("layout_edges");
                        model.Count("layout_node_metadata");
                        return new GraphDiagramRefresh(
                            frame,
                            new GraphDiagramTopologyRead(session.NodeIds(), session.Edges(), session.NodeMetadata(), frame.Generation),
                            null);
                    }
                    catch (VaultException exception)
                    {
                        return new GraphDiagramRefresh(null, null, exception);
                    }
                },
                null),
            answer => ApplyRefresh(model, answer));
    }

    /// <summary>Test seam (D-5): a refresh issued without the probe's
    /// comparison — the four terminal paths' facts drive it.</summary>
    internal void RefreshDiagramForTests() => RefreshDiagram();

    /// <summary>Test seam (IPH-2-3): the epoch re-asked for the key in force
    /// — a fetched key returns early, a failed one fetches again.</summary>
    internal void ReopenEpochForTests() => OpenEpoch();

    /// <summary>Test seam (IPH-2-4): a refresh's answer applied as the
    /// scheduler would apply it — the frame with its read.</summary>
    internal void ApplyRefreshForTests(LayoutFrame frame, GraphDiagramTopologyRead read)
    {
        if (_diagramModel is { } model)
        {
            ApplyRefresh(model, new GraphDiagramRefresh(frame, read, null));
        }
    }

    /// <summary>Test seam (D-9): the epoch's landing raised over a topology a
    /// fact installed through the model's own seams.</summary>
    internal void RaiseDiagramTopologyChangedForTests() => DiagramTopologyChanged?.Invoke();

    // --- W6-2 PR D, the renderer's readings and Term N5 through the document ----

    /// <summary>Term T4/T5: the display the renderer draws under (PR E edits it).</summary>
    internal GraphDisplay DiagramDisplay => CurrentConfig.Display;

    /// <summary>The row copy from the topology entry (the mac's <c>rowCopy</c>):
    /// the SAME fields the table speaks — references = in-links, embed false.</summary>
    internal static GraphRowCopy RowCopyOf(GraphTopologyNode entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new GraphRowCopy(entry.Label, entry.Kind, entry.InLinks, entry.OutLinks, entry.InLinks, false);
    }

    /// <summary>Term T2's RemoveFromSelection: the shared key set null under
    /// <see cref="SelectRow"/>'s guard — a retired or unseated document
    /// refuses (the writers census names this arm).</summary>
    internal bool ClearSelectionFromSurface()
    {
        if (_retired || !_isSeated())
        {
            return false;
        }
        ViewState.SelectedKey = null;
        return true;
    }

    /// <summary>Term N1, the ONE rule: the accepted topology's entry whose
    /// <c>stable_key</c> equals the shared key, among the ids the live model
    /// knows; null while the key is absent, hidden or gone (the renderer's
    /// SelectedId is this entry's id within its visible set).</summary>
    internal GraphTopologyNode? DiagramSelectedEntry()
    {
        if (_diagramModel is not { Topology: { } topology } model || ViewState.SelectedKey is not { } key)
        {
            return null;
        }
        foreach (GraphTopologyNode entry in topology.Nodes)
        {
            if (string.Equals(entry.StableKey, key, StringComparison.Ordinal) && model.NodesById.ContainsKey(entry.Id))
            {
                return entry;
            }
        }
        return null;
    }

    /// <summary>Term V6: the renderer's ZoomPercent, installed on bind and
    /// cleared on unbind — the readback's clause and the container's Value
    /// read ONE number; 100 (the seed) with no renderer attached.</summary>
    internal Func<uint>? DiagramZoomPercent { get; set; }

    /// <summary>Term N6: the DIAGRAM's readback — null while no model is live
    /// (the seam is uninstalled then), else ONE GraphWhereAmI: the derived
    /// selection's topology entry rendered the row copy's way with its
    /// component, else NoSelection; <c>zoom_percent</c> present; the filter
    /// clause and the needle exactly <see cref="TableWhereAmI"/>'s; answered
    /// whatever the layout's settle state.</summary>
    internal GraphA11yEvent.GraphWhereAmI? DiagramWhereAmI()
    {
        if (_diagramModel is null)
        {
            return null;
        }
        GraphWhereAmISelection selection = DiagramSelectedEntry() is { } entry
            ? new GraphWhereAmISelection.Node(RowCopyOf(entry), entry.Component)
            : new GraphWhereAmISelection.NoSelection();
        GraphFilter backend = ViewState.Filter;
        GraphWhereAmIFilter filter = ViewState.KindOnly == GraphNodeKind.Ghost
            ? new GraphWhereAmIFilter.UnresolvedOnly()
            : new GraphWhereAmIFilter.Normal(backend.OrphansOnly, backend.IncludeAttachments, backend.IncludeGhosts);
        return new GraphA11yEvent.GraphWhereAmI(selection, DiagramZoomPercent?.Invoke() ?? 100u, filter, ViewState.NameQuery);
    }

    /// <summary>Term N5's currency: the node's id in the LIVE model's visible
    /// set — the accepted topology's, among the ids the model knows.</summary>
    internal bool IsNodeCurrent(ulong id) =>
        _diagramModel is { Topology: { } topology } model
        && model.NodesById.ContainsKey(id)
        && topology.Nodes.Any(node => node.Id == id);

    /// <summary>Term N5's currency for a captured ENTRY (IPH-2-2): its id in
    /// the live model's visible set AND its stable key the one that id
    /// names now — core may reassign ids across a generation, so a menu
    /// left open across a refresh must not act on a record whose id another
    /// node inherited.</summary>
    internal bool IsNodeCurrent(GraphTopologyNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return _diagramModel is { Topology: { } topology } model
            && model.NodesById.TryGetValue(node.Id, out GraphNode? live)
            && string.Equals(live.StableKey, node.StableKey, StringComparison.Ordinal)
            && topology.Nodes.Any(current => current.Id == node.Id && string.Equals(current.StableKey, node.StableKey, StringComparison.Ordinal));
    }

    /// <summary>Term N5's admission for a topology entry — <see cref="IsActionEnabled"/>'s
    /// rule addressed by the node's path, the create admission for a ghost.</summary>
    internal bool IsDiagramActionEnabled(GraphRowAction action, GraphTopologyNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return action switch
        {
            GraphRowAction.Open or GraphRowAction.OpenInNewTab => node.Path is not null && OpenRowFromSurface is not null,
            GraphRowAction.Reveal => node.Path is not null && RevealRowFromSurface is not null,
            GraphRowAction.ShowConnections => node.Path is not null && ShowConnectionsFromSurface is not null,
            GraphRowAction.CreateNote => node.Kind == GraphNodeKind.Ghost && CreateNoteFromSurface is not null && CreateAdmissionReason?.Invoke() is null,
            _ => false,
        };
    }

    /// <summary>Term N5: the actions are the table's, through the document —
    /// the same admission as <see cref="Execute"/> (live, the node current,
    /// the action in core's vector for its kind and enabled) and the same
    /// four workspace seams, addressed by the node's path or its label.</summary>
    internal bool ExecuteFromDiagram(GraphRowAction action, GraphTopologyNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (_retired || !IsNodeCurrent(node) || !ActionAppliesTo(action, node.Kind) || !IsDiagramActionEnabled(action, node))
        {
            return false;
        }
        GraphTableRow row = RowOf(node);
        switch (action)
        {
            case GraphRowAction.Open:
                OpenRowFromSurface!(row, WorkspaceOpenTarget.CurrentTab);
                return true;
            case GraphRowAction.OpenInNewTab:
                OpenRowFromSurface!(row, WorkspaceOpenTarget.NewTab);
                return true;
            case GraphRowAction.Reveal:
                RevealRowFromSurface!(node.Path!);
                return true;
            case GraphRowAction.ShowConnections:
                ShowConnectionsFromSurface!(row);
                return true;
            case GraphRowAction.CreateNote:
                CreateNoteFromSurface!(SlateUniffiMethods.GraphGhostNotePath(node.Label));
                return true;
            default:
                return false;
        }
    }

    /// <summary>Term N5: Enter and Invoke — a ghost creates, else Open
    /// (<see cref="Activate"/>'s rule).</summary>
    internal bool ActivateFromDiagram(GraphTopologyNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return ExecuteFromDiagram(node.Kind == GraphNodeKind.Ghost ? GraphRowAction.CreateNote : GraphRowAction.Open, node);
    }

    /// <summary>The seams take the table's row shape: the topology entry's
    /// fields, no cells (the seams read the path, the label and the kind).</summary>
    private static GraphTableRow RowOf(GraphTopologyNode node) =>
        new(node.StableKey, node.Id, node.Label, node.Path, node.Kind, [], node.InLinks, node.OutLinks, node.InEmbeds, node.OutEmbeds, node.Component, null);

    /// <summary>Term G6's apply: adopts ONLY when the answer's generation is
    /// NEWER than the model's (monotonic), pruning the pins the topology
    /// lost, restarting the settle and opening a new epoch; an unchanged, a
    /// non-monotonic, a refused or a failed answer adopts nothing — the
    /// failure logged; then RefreshAgain consumed on EVERY path.</summary>
    private void ApplyRefresh(GraphDiagramModel model, GraphDiagramRefresh? answer)
    {
        if (_retired || !ReferenceEquals(_diagramModel, model))
        {
            // Torn down meanwhile: the drop reset the refresh's pair.
            return;
        }
        _refreshRunning = false;
        if (answer is { Failure: { } failure })
        {
            HostLog.Write(HostDiagnosticEvent.GraphLayoutRefreshFailed, failure);
        }
        else if (answer is { Frame: { } frame, Read: { } read } && frame.Generation > model.Generation)
        {
            // IPH-2-4 (Term G5/G6): the refresh's frame carries the new
            // generation's positions for the new ids — adopted WITH the
            // read, so no window exists in which the peers, the hit grid
            // and a spatial step read new ids with no positions. The
            // adoption is atomic behind the frame's length guard (IPH-4-1):
            // a frame that is not two floats per id is a malformed answer,
            // logged, and the model stands for the next probe's refresh.
            if (frame.Positions.Length != read.Ids.Length * 2)
            {
                HostLog.Write(
                    HostDiagnosticEvent.GraphLayoutRefreshFailed,
                    new InvalidOperationException($"the refresh's frame carries {frame.Positions.Length} positions for {read.Ids.Length} ids."));
            }
            else
            {
                model.Adopt(read);
                model.AdoptFrame(frame);
                OpenEpoch();
                model.Driver.StartSettle();
            }
        }
        if (_refreshAgain)
        {
            _refreshAgain = false;
            RefreshDiagram();
        }
    }

    /// <summary>Term G4's settle line: spoken at convergence ONLY when armed.</summary>
    private void OnSettleConverged()
    {
        if (!SettleAnnouncementArmed)
        {
            return;
        }
        SettleAnnouncementArmed = false;
        AnnounceLayoutSettled();
    }

    /// <summary>Term G4: a flip while settling restarts the settle (the mac's
    /// <c>motionFlip</c>) — on the owner context, the channel's thread being
    /// the system's.</summary>
    private void OnMotionChanged() =>
        Post(() =>
        {
            if (!_retired && _diagramModel is { Driver.IsSettling: true } model)
            {
                model.Driver.StartSettle();
            }
        });

    /// <summary>Term G6: the backend filter's change while in Diagram mode is
    /// a REBUILD, whether a model is live or a build is in flight; Term G3: a
    /// needle, a kind overlay or a groups change is a new epoch.</summary>
    private void OnViewStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_retired || ViewState.Mode != GraphSurfaceMode.Diagram)
        {
            return;
        }
        switch (e.PropertyName)
        {
            case nameof(GraphViewState.Filter):
                RebuildDiagram();
                break;
            case nameof(GraphViewState.NameQuery):
            case nameof(GraphViewState.KindOnly):
            case nameof(GraphViewState.Groups):
                OpenEpoch();
                break;
            default:
                break;
        }
    }

    /// <summary>The mac's <c>humanReadable</c> arms reachable from the build
    /// (the search overlay's twin): the vault messages pass through.</summary>
    private static string HumanReadable(Exception failure) => failure switch
    {
        VaultException.Io io => io.message,
        VaultException.Db db => db.message,
        VaultException.InvalidQuery invalid => $"Search query is invalid: {invalid.message}",
        VaultException.Unsupported unsupported => $"{unsupported.feature} is not implemented yet.",
        _ => failure.Message,
    };

    // --- The six announcement seams of the diagram (D-1): each rides the
    // effective-gated boundary AnnounceIfEffective ------------------------------

    /// <summary>Term M1: the switch's line — <c>GraphMode{mode}</c>, immediate.</summary>
    internal void AnnounceMode(GraphSurfaceMode mode) =>
        AnnounceIfEffective(new GraphA11yEvent.GraphMode(mode));

    /// <summary>Term N4: a keyboard move's or a click's line — the row copy at
    /// the live verbosity, the navigation class (DD-Q2's default).</summary>
    internal void AnnounceRow(GraphRowCopy row)
    {
        ArgumentNullException.ThrowIfNull(row);
        AnnounceIfEffective(new GraphA11yEvent.GraphRow(_verbosity(), row));
    }

    /// <summary>Term V2: a Zoomed outcome's line, spoken by the navigator
    /// through this seam (the navigator posts nothing itself).</summary>
    internal void AnnounceZoom(bool fit, uint percent) =>
        AnnounceIfEffective(new GraphA11yEvent.GraphZoom(fit, percent));

    /// <summary>Term N7: the pin's line.</summary>
    internal void AnnouncePinned(bool pinned) =>
        AnnounceIfEffective(new GraphA11yEvent.GraphPinned(pinned));

    /// <summary>W6-2 PR E (Term K3): the changed force control's line —
    /// <c>GraphForceValue{control, percent}</c>, the relay's forceValue
    /// class (200 ms latest-wins, so a drag coalesces to its resting value);
    /// spoken by the inspector's route BEFORE the arm and the restart, so
    /// the value precedes the settle under every scheduler (IGU-2).</summary>
    internal void AnnounceForceValue(GraphForceControl control, uint percent) =>
        AnnounceIfEffective(new GraphA11yEvent.GraphForceValue(control, percent));

    /// <summary>Term T3: the tier latch's one line on the A→B edge.</summary>
    internal void AnnounceTierEntered() =>
        AnnounceIfEffective(new GraphA11yEvent.GraphTierEntered());

    /// <summary>Term G4: the settle line, spoken only when armed.</summary>
    internal void AnnounceLayoutSettled() =>
        AnnounceIfEffective(new GraphA11yEvent.GraphLayoutSettled());

    private void RefreshFilterCountText()
    {
        GraphPublication publication = Publication;
        FilterCountText = publication.State is GraphLoadState.Ready or GraphLoadState.Empty
            ? GraphAnnouncer.RenderLabel(CountOf(publication))
            : string.Empty;
    }

    private static GraphA11yEvent.GraphFilterCount CountOf(GraphPublication publication) =>
        new((uint)publication.Rows.Count, (uint)Math.Min(publication.Total, uint.MaxValue));

    /// <summary>Rule P, Term P4: the headline from THE PUBLISHED RESULT —
    /// core's rule (C-2), one crossing per successful publication of a
    /// current preset token, none on a failure or a supersession.</summary>
    private GraphPresetOutcome PresetOutcome(GraphPreset preset, GraphTableRows rows)
    {
        lock (CrossingsForTests)
        {
            CrossingsForTests["graph_preset_outcome"]++;
        }
        return SlateUniffiMethods.GraphPresetOutcome(preset, (ulong)rows.Rows.Length, rows.Rows.Length == 0 ? null : rows.Rows[0]);
    }

    // --- The load (contract A-2) ------------------------------------------

    /// <summary>Issue a token and start the body (rule A). The ACTIVATION's
    /// entry — rule L's follow method, Term 1's one outside caller — and,
    /// with <paramref name="preset"/>, the armed load of rule P (Term P1):
    /// policy <see cref="GraphAnnouncePolicy.Preset"/> with the preset on
    /// the token, the default sort, no user sort. Without an explicit sort
    /// the pair CARRIES the pending sort (Term Q5): a sort requested and not
    /// yet answered rides the activation and adopts at its install, with
    /// <c>GridSorted</c> before the cause's line (IGP-4).</summary>
    public GraphLoadToken Load(GraphLoadKind kind, GraphAnnouncePolicy announce, GraphTableSort? sort = null, GraphPreset? preset = null)
    {
        if (_retired)
        {
            throw new InvalidOperationException("the graph document is retired");
        }
        GraphTableSort requested;
        bool userSort;
        if (preset is not null)
        {
            // Term P4: the preset's default sort replaces a pending sort
            // silently (Term Q5 (d)) — no GridSorted for the default sort.
            _requestedSort = null;
            requested = DefaultSort;
            userSort = false;
        }
        else if (sort is { } explicitSort)
        {
            _requestedSort = explicitSort == Publication.AcceptedSort ? null : explicitSort;
            requested = explicitSort;
            userSort = _requestedSort is not null;
        }
        else
        {
            requested = _requestedSort ?? Publication.AcceptedSort;
            userSort = _requestedSort is not null;
        }
        return Issue(kind, announce, requested, userSort, preset);
    }

    /// <summary>Rule Q's ONE entry for a USER request (Term Q1): the needle,
    /// the sort, the preset, the filter. ADMISSION first — false, and
    /// nothing touched, unless the document is live and the workspace's
    /// seated one (IGO-8). The kind is Term Q3's, the policy Term Q4's, the
    /// pending sort's transitions Term Q5's.</summary>
    public bool Request(GraphRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_retired || !_isSeated())
        {
            return false;
        }
        GraphTableSort accepted = Publication.AcceptedSort;
        switch (request)
        {
            case GraphRequest.Needle:
                {
                    // Term Q3: rows only iff a snapshot is held, no pair is in
                    // flight and the backend filter is the held snapshot's;
                    // otherwise a pair — under FilterCount, ALWAYS (Term Q4): the
                    // reader's own needle replaces a displaced summary or
                    // headline with the count, the mac's order (IGP-8).
                    bool rowsOnly = Publication.HoldsSnapshot
                        && _current is not { Kind: GraphLoadKind.Pair }
                        && ViewState.Filter == Publication.Filter;
                    // FilterCount either way (Term Q4's "ALWAYS"): the
                    // POLICY is what the install speaks, for a rows-only
                    // token as much as for a pair (IPG-33). It used to be
                    // Silent here and the rows-only install spoke the count
                    // regardless, which made the policy a lie and left Term
                    // Q5 (c)'s cancellation — the one rows-only token that
                    // must say NOTHING — speaking too.
                    _ = Issue(
                        rowsOnly ? GraphLoadKind.RowsOnly : GraphLoadKind.Pair,
                        GraphAnnouncePolicy.FilterCount,
                        _requestedSort ?? accepted,
                        _requestedSort is not null,
                        preset: null);
                    return true;
                }
            case GraphRequest.Sort sortRequest:
                {
                    if (sortRequest.Requested == accepted)
                    {
                        // Term Q5 (c): the accepted sort asked back CANCELS the
                        // pending one at issue with no line (A-5's shipped
                        // guard); with nothing pending there is nothing to do.
                        if (_requestedSort is null)
                        {
                            return false;
                        }
                        _requestedSort = null;
                        _ = Issue(GraphLoadKind.RowsOnly, GraphAnnouncePolicy.Silent, accepted, userSort: false, preset: null);
                        return true;
                    }
                    // A-5 as frozen: rows only, always; the receiver's re-fetch
                    // carries the sort when the held snapshot is absent, differs
                    // or is stale (Term Q3, IGO-2; Term Q9).
                    _requestedSort = sortRequest.Requested;
                    // Term Q5's combined lines (IGP-19): the adoption's
                    // GridSorted precedes the receiver's own count, so the
                    // token carries FilterCount and the install speaks it.
                    _ = Issue(GraphLoadKind.RowsOnly, GraphAnnouncePolicy.FilterCount, sortRequest.Requested, userSort: true, preset: null);
                    return true;
                }
            case GraphRequest.Preset preset:
                {
                    // Rule P through the request entry (Term P3's already-
                    // effective route, A-5's class): a pair under Preset with the
                    // DEFAULT sort and no user sort — a pending sort replaced
                    // silently (Term Q5 (d)), no GridSorted (Term P4).
                    _requestedSort = null;
                    _ = Issue(GraphLoadKind.Pair, GraphAnnouncePolicy.Preset, DefaultSort, userSort: false, preset.Chosen);
                    return true;
                }
            case GraphRequest.Filter:
                {
                    // PR E's arm (Term Q4): a pair under FilterCount over the
                    // query the caller wrote through ApplyQuery — the overlay
                    // cleared there, a Preset policy in flight NOT inherited (the
                    // mac drops the pending preset, `:388–389`) — carrying the
                    // pending sort (Term Q5).
                    _ = Issue(GraphLoadKind.Pair, GraphAnnouncePolicy.FilterCount, _requestedSort ?? accepted, _requestedSort is not null, preset: null);
                    return true;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(request), request, "an unknown request arm");
        }
    }

    /// <summary>Term Q2's lineage: a token is in flight until its receiver
    /// ran to an install, a failure or a rejection — or a newer token
    /// replaced it.</summary>
    public bool IsRequestInFlight => _current is not null;

    /// <summary>Every terminal state of the lineage, by kind (Term F3),
    /// raised after the publication it ends on.</summary>
    internal event Action<GraphLineageEnd>? LineageEnded;

    private void SetCurrent(GraphLoadToken? token)
    {
        bool was = _current is not null;
        _current = token;
        if (was != (token is not null))
        {
            OnPropertyChanged(nameof(IsRequestInFlight));
            // C-8: the readback's availability follows every lineage edge
            // (IGN-13) — the ISSUE here; each terminal arm raises after its
            // publication so the answer is over the installed record.
            if (token is not null)
            {
                Navigator?.NotifyWhereAmIAvailabilityChanged();
            }
        }
    }

    internal GraphLoadToken? CurrentForTests => _current;

    /// <summary>Every token is issued here: the sequence advances, the
    /// request is the view state's three query fields as ONE record (C-4)
    /// with the sort, LOADING shows when no snapshot is held, and the token
    /// becomes the lineage (Term Q2).</summary>
    private GraphLoadToken Issue(GraphLoadKind kind, GraphAnnouncePolicy announce, GraphTableSort sort, bool userSort, GraphPreset? preset)
    {
        _seq++;
        var request = new GraphTableRequest(
            new GraphVisibilityQuery(ViewState.Filter, ViewState.NameQuery, ViewState.KindOnly),
            sort);
        _request = request;
        if (kind == GraphLoadKind.Pair && !Publication.HoldsSnapshot && Publication.State != GraphLoadState.Loading)
        {
            // A retry after a failure shows LOADING, not the old grid (the
            // mac's `:257–265`).
            Publication = GraphPublication.Initial(request.Query, Publication.AcceptedSort);
            RefreshFilterCountText();
        }
        var token = new GraphLoadToken(this, _session, _lifecycleGeneration(), request, _seq, kind, announce, preset, userSort);
        SetCurrent(token);
        StartWorkAlwaysAsync(() => Fetch(token), Receive);
        return token;
    }

    /// <summary>Rule Q, Term Q9: a pair that REPLACES a token in flight —
    /// the probe's superseding pair, the receiver's re-fetch — inherits the
    /// replaced request whole: its sort and user sort, its policy and its
    /// preset, under a fresh sequence, so the line the replaced token would
    /// have spoken is spoken over the newer generation — the POLICY, verbatim.
    /// TGC-3 recorded a deviation here: every replaced rows-only token was
    /// promoted to <c>FilterCount</c>, because the rows-only install spoke the
    /// count whatever its policy said. IPG-33 made the policy true, so the
    /// promotion is gone and the letter of Terms Q4 and Q9 stands: a needle's
    /// or a sort's replacement speaks the count, and Term Q5 (c)'s
    /// cancellation stays silent through its replacement too.</summary>
    private GraphLoadToken IssueReplacing(GraphLoadToken replaced) =>
        Issue(
            GraphLoadKind.Pair,
            replaced.Announce,
            replaced.Request.Sort,
            replaced.UserSort,
            replaced.Preset);

    private GraphLoadEnvelope Fetch(GraphLoadToken token)
    {
        GraphFilter filter = token.Request.Query.Filter;
        // The selection this fetch OBSERVES, read on the pool immediately
        // before the snapshot crossing (IPC-13): a key written between the
        // load's issue and this read is older than the snapshot and is this
        // publication's to judge; one written after it is the next's.
        int observed = ViewState.SelectionGeneration;
        try
        {
            GraphSnapshot? snapshot = null;
            if (token.Kind == GraphLoadKind.Pair)
            {
                lock (CrossingsForTests)
                {
                    CrossingsForTests["graph_snapshot"]++;
                }
                snapshot = token.Session.GraphSnapshot(filter);
            }
            lock (CrossingsForTests)
            {
                CrossingsForTests["graph_table_rows"]++;
            }
            GraphTableRows rows = token.Session.GraphTableRows(token.Request.Query, token.Request.Sort);
            FetchGateForTests?.Invoke();
            return new GraphLoadEnvelope(token, filter, token.Request.Query, token.Request.Sort, snapshot, rows, null, observed);
        }
        catch (VaultException exception)
        {
            return new GraphLoadEnvelope(token, filter, token.Request.Query, token.Request.Sort, null, null, exception.Message, observed);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.IO.IOException)
        {
            return new GraphLoadEnvelope(token, filter, token.Request.Query, token.Request.Sort, null, null, exception.Message, observed);
        }
    }

    /// <summary>Test seam: an envelope handed to the receiver as the pool
    /// would hand it — a rejected or straddled one built by a fact.</summary>
    internal void ReceiveForTests(GraphLoadEnvelope envelope) => Receive(envelope);

    /// <summary>The receiver, at DISPATCH time on the owner context: the
    /// four-step rule of contract A-2, the mac's `receiveGraphTableRows`,
    /// with rule Q's terminal states (Term Q2), lines (Terms Q4, Q5, P4)
    /// and replacing pairs (Term Q9).</summary>
    private void Receive(GraphLoadEnvelope envelope)
    {
        GraphLoadToken token = envelope.Token;
        // (i) the token is current in every field — the lifecycle
        // generation included (IPA-6). A superseded token is not the
        // lineage's: it changes nothing.
        if (!ReferenceEquals(token.Document, this)
            || _retired
            || !ReferenceEquals(token.Session, _session)
            || token.LifecycleGeneration != _lifecycleGeneration()
            || token.Seq != _seq
            || _request is null
            || token.Request != _request
            // Term Q2: `_current` IS the lineage's one token in flight, and
            // every terminal arm clears it — so a SECOND envelope for a token
            // that already installed, failed or was rejected is not the
            // lineage's either. Without this the fields above still matched
            // it, and a duplicate completion would publish and speak twice,
            // or replace a terminal failure with a success (IPG-30).
            || !ReferenceEquals(_current, token))
        {
            return;
        }
        // From here the token IS the lineage's (Term Q2): every exit below
        // is one of its terminal states or its replacement.
        // (ii) the envelope answers THIS request, and its inputs agree
        // with each other — validated before EITHER arm (IPA-6): a
        // failure envelope whose filter is not its query's is as foreign
        // as a success's. A REJECTION is terminal (IGO-6): no re-fetch,
        // the pending sort rolled back.
        if (envelope.Query != token.Request.Query
            || envelope.Sort != token.Request.Sort
            || envelope.Filter != envelope.Query.Filter)
        {
            // The request goes with the lineage: a second envelope for a
            // token already terminal can never install.
            SetCurrent(null);
            _request = null;
            _requestedSort = null;
            Navigator?.NotifyWhereAmIAvailabilityChanged();
            LineageEnded?.Invoke(GraphLineageEnd.Rejection);
            return;
        }
        if (envelope.Failure is { } failure)
        {
            // Terminal: the pending sort rolled back for ANY token (Term
            // Q2, IGP-2); a preset on the token is forgotten with it (Term
            // P4); the block where the line would have been.
            SetCurrent(null);
            _requestedSort = null;
            if (token.Kind == GraphLoadKind.Pair)
            {
                Publication = Publication.AsPairFailure(failure);
                RefreshFilterCountText();
            }
            Navigator?.NotifyWhereAmIAvailabilityChanged();
            AnnounceIfEffective(new GraphA11yEvent.GraphBlocked(new GraphBlockedReason.LoadFailed(failure)));
            LineageEnded?.Invoke(token.Kind == GraphLoadKind.Pair ? GraphLineageEnd.PairFailure : GraphLineageEnd.RowsFailure);
            return;
        }
        GraphTableRows rows = envelope.Rows!;
        GraphPublication previous = Publication;
        GraphPublication next;
        if (token.Kind == GraphLoadKind.Pair)
        {
            GraphSnapshot snapshot = envelope.Snapshot!;
            if (rows.Generation != snapshot.Generation)
            {
                // The two crossings straddled a rebuild: drop, and read
                // again as the token's own replacement (Term Q9).
                _ = IssueReplacing(token);
                return;
            }
            next = GraphPublication.FromPair(snapshot, envelope.Query, rows, token.Request.Sort);
        }
        else
        {
            if (!previous.HoldsSnapshot
                || previous.Filter != envelope.Query.Filter
                || rows.Generation != previous.Generation)
            {
                // The held snapshot is absent, another filter's or stale:
                // the pair for THIS request, carrying its sort (Term Q3,
                // IGO-2) and speaking its count (Term Q9).
                _ = IssueReplacing(token);
                return;
            }
            next = previous.WithRows(envelope.Query, rows, token.Request.Sort);
        }
        // (iii) ONE swap, rows before state — the record carries both. The
        // lineage ends at the install (Term Q2); the pending sort is
        // answered when the token carried it (Term Q5 (a)).
        bool answeredSort = token.UserSort;
        _requestedSort = null;
        SetCurrent(null);
        Publication = next;
        RefreshFilterCountText();
        // C-8: the readback answers over the INSTALLED record — re-evaluated
        // after the swap, not at the edge before it.
        Navigator?.NotifyWhereAmIAvailabilityChanged();
        if (token.Kind == GraphLoadKind.Pair)
        {
            // A NEW snapshot judges the key (the mac revalidates at the
            // snapshot's publish point and on its generation change alone);
            // a rows-only publication carries the held one, which already
            // judged — or never saw — the key.
            RevalidateSelection(next, envelope.SelectionGeneration);
        }
        if (token.Kind == GraphLoadKind.Pair && _highWater > next.Generation)
        {
            // A-3's recovery, issued BEFORE the install is raised (IPG-37):
            // this publication is KNOWN to be intermediate — the vault moved
            // on while it was in flight — and a landing delivered against it
            // completes on rows the recovery is about to replace. Issued
            // first, the lineage is in flight when the surface hears the
            // install, so the landing waits for the record the reader will
            // actually end on. Term Q9's "inherits nothing" stands: the
            // recovery is silent, and this install's own line is spoken
            // below — the count's Term Q6 gate drops it either way, since a
            // newer token is what the gate asks about.
            _highWater = 0;
            _ = Issue(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent, next.AcceptedSort, userSort: false, preset: null);
        }
        // The surface's GridSorted is raised synchronously from here, so it
        // PRECEDES the receiver's own line for the same install (Term Q5's
        // combined lines, IGP-19).
        PublicationInstalled?.Invoke(new GraphPublicationInstall(previous, next, answeredSort));
        LineageEnded?.Invoke(GraphLineageEnd.Install);
        if (token.Kind == GraphLoadKind.Pair)
        {
            switch (token.Announce)
            {
                case GraphAnnouncePolicy.Summary:
                    AnnounceIfEffective(new GraphA11yEvent.GraphSnapshotSummary(next.Snapshot!.SummaryCounts));
                    break;
                case GraphAnnouncePolicy.Preset:
                    // Term P4: the headline in place of the summary, no count.
                    AnnounceIfEffective(new GraphA11yEvent.GraphPreset(PresetOutcome(token.Preset!.Value, rows)));
                    break;
                case GraphAnnouncePolicy.FilterCount:
                    // A pair under FilterCount speaks the count through the
                    // gated entry (the mac's `:249–253`).
                    AnnounceFilterCountIfEffective(CountOf(next), token);
                    break;
                case GraphAnnouncePolicy.Silent:
                default:
                    break;
            }
        }
        else
        {
            // The mac's rows-only publish speaks the filter count
            // (`requestGraphTableRows`, `:343–347`), coalesced — through the
            // relay's GATED entry (A-10 as amended, W6-2 PR B): the
            // effective predicate is stored with the line and re-checked
            // at fire, the mac's `graphTabActive` at `:150`, with the
            // token's currency (Term Q6). BY THE TOKEN'S POLICY (IPG-33):
            // every rows-only token that speaks carries FilterCount, and
            // Term Q5 (c)'s cancellation carries Silent because it must say
            // nothing — an unconditional count here spoke for it too.
            if (token.Announce == GraphAnnouncePolicy.FilterCount)
            {
                AnnounceFilterCountIfEffective(CountOf(next), token);
            }
        }
    }

    /// <summary>Contract A-7: the shared key survives a reorder and a
    /// filter overlay; it clears only when the SNAPSHOT no longer carries
    /// the node (the mac's `revalidateGraphSelection(against:)`).</summary>
    private void RevalidateSelection(GraphPublication publication, int selectionGenerationAtFetch)
    {
        // Only the selection this load OBSERVED immediately before its
        // snapshot crossing: a key written since — a pinned rename's, a
        // re-root's — is newer than the snapshot and is the next
        // publication's to judge (codex post-implementation pass 2, IPC-8: a
        // pair fetched before the rename erased the retargeted key; pass 3,
        // IPC-13: the observation is the worker's, not the issue's, so a key
        // written before the fetch began IS judged by it).
        if (ViewState.SelectionGeneration != selectionGenerationAtFetch)
        {
            return;
        }
        if (ViewState.SelectedKey is { } key && publication.HoldsSnapshot && !publication.ContainsNode(key))
        {
            ViewState.SelectedKey = null;
        }
    }

    // --- The probe (contract A-3) -----------------------------------------

    /// <summary>Re-read the generation off the dispatcher and, on a
    /// change against a held snapshot, issue a superseding silent pair;
    /// while nothing READY is held, keep the high-water mark.</summary>
    /// <summary>W7-7 PR 7 (#1252, round 29): <see cref="Probe"/>, awaited
    /// to its publication (an error state included).</summary>
    internal async Task ProbeAsync()
    {
        Probe();
        await WhenPublishedAsync();
    }

    public void Probe()
    {
        if (_retired)
        {
            return;
        }
        StartWorkAlwaysAsync(
            () =>
            {
                lock (CrossingsForTests)
                {
                    CrossingsForTests["graph_generation"]++;
                }
                return _session.GraphGeneration();
            },
            generation =>
            {
                if (_retired)
                {
                    return;
                }
                // W6-2 PR D (Term G6, IGQ-6): the probe's own comparison — a
                // generation the live model lacks refreshes the layout; an
                // equal one issues no Refresh crossing.
                if (_diagramModel is { } diagram && generation != diagram.Generation)
                {
                    RefreshDiagram();
                }
                GraphPublication held = Publication;
                if (held.HoldsSnapshot)
                {
                    if (generation != held.Generation)
                    {
                        // A-3 as amended by the owner (rule Q, Term Q9): a
                        // token in flight is superseded by a pair inheriting
                        // its request whole; over a quiescent lineage the
                        // pair is silent and carries the accepted sort.
                        _ = _current is { } inFlight
                            ? IssueReplacing(inFlight)
                            : Issue(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent, held.AcceptedSort, userSort: false, preset: null);
                    }
                }
                else
                {
                    _highWater = Math.Max(_highWater, generation);
                }
            });
    }

    // --- Actions (contract A-8) -------------------------------------------

    public bool IsActionEnabled(GraphRowAction action, GraphTableRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return action switch
        {
            GraphRowAction.Open or GraphRowAction.OpenInNewTab => row.Path is not null && OpenRowFromSurface is not null,
            GraphRowAction.Reveal => row.Path is not null && RevealRowFromSurface is not null,
            GraphRowAction.ShowConnections => row.Path is not null && ShowConnectionsFromSurface is not null,
            GraphRowAction.CreateNote => CreateNoteFromSurface is not null && CreateAdmissionReason?.Invoke() is null,
            _ => false,
        };
    }

    public string? ActionDisabledReason(GraphRowAction action) =>
        action == GraphRowAction.CreateNote ? CreateAdmissionReason?.Invoke() : null;

    /// <summary>W6-2 PR B2 (B2-3, IGJ-7, IGK-9): a row acts only while it is
    /// the SAME object in the current publication's rows — a cached menu
    /// item over a row a republish dropped (a name or kind overlay keeps the
    /// node in the snapshot, so <see cref="GraphPublication.ContainsNode"/>,
    /// A-7's revalidation check, is not this wall) acts on nothing. The
    /// class TGB-9 walled in the Connections leaf.</summary>
    public bool IsRowCurrent(GraphTableRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return Publication.Rows.Any(current => ReferenceEquals(current, row));
    }

    public void Execute(GraphRowAction action, GraphTableRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (_retired || !IsRowCurrent(row) || !IsActionEnabled(action, row))
        {
            return;
        }
        switch (action)
        {
            case GraphRowAction.Open:
                OpenRowFromSurface!(row, WorkspaceOpenTarget.CurrentTab);
                break;
            case GraphRowAction.OpenInNewTab:
                OpenRowFromSurface!(row, WorkspaceOpenTarget.NewTab);
                break;
            case GraphRowAction.Reveal:
                RevealRowFromSurface!(row.Path!);
                break;
            case GraphRowAction.ShowConnections:
                ShowConnectionsFromSurface!(row);
                break;
            case GraphRowAction.CreateNote:
                CreateNoteFromSurface!(SlateUniffiMethods.GraphGhostNotePath(row.Label));
                break;
            default:
                break;
        }
    }

    /// <summary>Activation (contract A-9): Open for a note or attachment,
    /// Create note for a ghost — plain or modified through one gate.</summary>
    public void Activate(GraphTableRow row, bool modified)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Kind == GraphNodeKind.Ghost)
        {
            Execute(GraphRowAction.CreateNote, row);
            return;
        }
        Execute(modified ? GraphRowAction.OpenInNewTab : GraphRowAction.Open, row);
    }

    // --- Retirement (contract A-1) ----------------------------------------

    /// <summary>The last graph tab closed: the sequence advances so no
    /// in-flight result matches, the scheduler refuses new work, the
    /// announcer drops its pending lines, the view state resets.</summary>
    internal void Retire()
    {
        // W6-2 PR D (Term G2, IGT-1): the flip and the sweep of the unseated
        // builds are ONE transition under the diagram lock — a build's compute
        // registers before it (and is swept) or reads the flag after it (and
        // retires itself); an apply the retired scheduler withdraws leaves
        // nothing behind.
        GraphDiagramModel[] unseated;
        lock (_diagramLock)
        {
            _retired = true;
            unseated = [.. _unseatedBuilds];
            _unseatedBuilds.Clear();
        }
        _seq++;
        _request = null;
        SetCurrent(null);
        // W6-2 PR D (Term G7): the diagram torn down with the document, the
        // unseated builds retired, the channels released.
        TeardownDiagram();
        foreach (GraphDiagramModel build in unseated)
        {
            build.Retire();
        }
        ViewState.PropertyChanged -= OnViewStateChanged;
        _motion.Changed -= OnMotionChanged;
        if (_ownsMotion)
        {
            _motion.Dispose();
        }
        // C-8: the table's seam cleared — Where-am-I is refused with no seated
        // document — and the availability re-evaluated.
        Navigator?.ClearTableReadback(_tableReadback);
        Navigator?.NotifyWhereAmIAvailabilityChanged();
        if (_preferences is not null)
        {
            _preferences.PropertyChanged -= OnPreferencesChanged;
            _preferences.DisplayChanged -= OnDisplayChanged;
        }
        Shutdown();
        // A-1 as amended (W6-2 PR B, BD-12): the relay is the workspace's;
        // retirement drops THIS document's pending classes — the mac's
        // `cancelPending` on view departure — and leaves it live for the
        // Connections leaf. A-1 as amended again (W6-2 PR B2, B2D-1): the
        // view state is the workspace's too — retirement leaves it, and a
        // re-seated document revalidates the key it inherits at its first
        // pair publication (Term 15).
        _announcer.DropAllPending();
    }
}
