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
        GraphPreferencesViewModel? preferences = null)
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

    /// <summary>Test seam: runs inside the worker AFTER the fetch and
    /// before the envelope returns — the canvas publish-gate shape, for
    /// the gated generation fact.</summary>
    internal Action? FetchGateForTests { get; set; }

    /// <summary>Test seam: FFI crossings the document made, by name.</summary>
    internal Dictionary<string, int> CrossingsForTests { get; } = new(StringComparer.Ordinal)
    {
        ["graph_snapshot"] = 0,
        ["graph_table_rows"] = 0,
        ["graph_generation"] = 0,
        ["graph_row_actions"] = 0,
        ["graph_preset_outcome"] = 0,
    };

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
    /// GraphWhereAmI: the shared key's node in the held SNAPSHOT (scanned by
    /// StableKey — R-A's no-index rule) rendered the diagram's way — the
    /// references its in-links, no embed, the node's component — else
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
        GraphWhereAmISelection selection = new GraphWhereAmISelection.NoSelection();
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
                    selection = new GraphWhereAmISelection.Node(
                        new GraphRowCopy(row.Label, row.Kind, row.LinksIn, row.LinksOut, row.LinksIn, false),
                        row.Component);
                    break;
                }
            }
        }
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
            if (_highWater > next.Generation)
            {
                // A-3's recovery: the high-water pair is issued at an
                // INSTALL, when nothing is in flight, and inherits nothing
                // (Term Q9).
                _highWater = 0;
                _ = Issue(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent, next.AcceptedSort, userSort: false, preset: null);
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
        _retired = true;
        _seq++;
        _request = null;
        SetCurrent(null);
        // C-8: the table's seam cleared — Where-am-I is refused with no seated
        // document — and the availability re-evaluated.
        Navigator?.ClearTableReadback(_tableReadback);
        Navigator?.NotifyWhereAmIAvailabilityChanged();
        if (_preferences is not null)
        {
            _preferences.PropertyChanged -= OnPreferencesChanged;
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
