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
/// effective (rule L, Term 6): the snapshot summary, or nothing.</summary>
internal enum GraphAnnouncePolicy
{
    Summary,
    Silent,
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
    GraphAnnouncePolicy Announce);

/// <summary>The worker ENVELOPE (contract A-2; the round-3 ledger's
/// IGA-22, IGA-43): the inputs the body actually used beside its
/// results, because neither result carries its inputs.</summary>
internal sealed record GraphLoadEnvelope(
    GraphLoadToken Token,
    GraphFilter Filter,
    GraphVisibilityQuery Query,
    GraphTableSort Sort,
    GraphSnapshot? Snapshot,
    GraphTableRows? Rows,
    string? Failure);

/// <summary>What one installed publication answered — the surface's
/// adoption announcement reads it (contract A-5).</summary>
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
    private readonly Func<int> _lifecycleGeneration;
    private readonly Dictionary<GraphNodeKind, IReadOnlyList<GraphRowActionSpec>> _actionsByKind;
    private ulong _seq;
    private GraphTableRequest? _request;
    private GraphTableSort? _requestedSort;
    private ulong _highWater;
    private bool _pairInFlight;
    private bool _retired;
    private GraphPublication _publication;

    public GraphDocumentViewModel(
        VaultSession session,
        GraphAnnouncer announcer,
        Func<bool> isEffectiveActive,
        Func<GraphVerbosity> verbosity,
        SynchronizationContext? ownerContext = null,
        Func<int>? lifecycleGeneration = null)
        : base(
            synchronousForTests: false,
            ownerContext
                ?? SynchronizationContext.Current as DispatcherSynchronizationContext
                ?? new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher))
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(isEffectiveActive);
        ArgumentNullException.ThrowIfNull(verbosity);
        _session = session;
        _announcer = announcer;
        _isEffectiveActive = isEffectiveActive;
        _verbosity = verbosity;
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
        ViewState = new GraphViewState();
        _publication = GraphPublication.Initial(ViewState.Filter, DefaultSort);
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

    public bool IsRetired => _retired;

    public ulong SeqForTests => _seq;

    public GraphTableSort? RequestedSortForTests => _requestedSort;

    public ulong HighWaterForTests => _highWater;

    /// <summary>The verbosity the row copy is rendered at (AD-6:
    /// Standard until PR C).</summary>
    public GraphVerbosity Verbosity => _verbosity();

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

    // --- The load (contract A-2) ------------------------------------------

    /// <summary>Issue a token and start the body (rule A).</summary>
    public GraphLoadToken Load(GraphLoadKind kind, GraphAnnouncePolicy announce, GraphTableSort? sort = null)
    {
        if (_retired)
        {
            throw new InvalidOperationException("the graph document is retired");
        }
        _seq++;
        GraphTableSort requestedSort = sort ?? Publication.AcceptedSort;
        var request = new GraphTableRequest(
            new GraphVisibilityQuery(ViewState.Filter, ViewState.NameQuery, null),
            requestedSort);
        _request = request;
        _requestedSort = requestedSort == Publication.AcceptedSort ? null : requestedSort;
        if (kind == GraphLoadKind.Pair)
        {
            _pairInFlight = true;
            if (!Publication.HoldsSnapshot && Publication.State != GraphLoadState.Loading)
            {
                // A retry after a failure shows LOADING, not the old
                // grid (the mac's `:257–265`).
                Publication = GraphPublication.Initial(request.Query.Filter, Publication.AcceptedSort);
            }
        }
        var token = new GraphLoadToken(this, _session, _lifecycleGeneration(), request, _seq, kind, announce);
        StartWorkAlwaysAsync(() => Fetch(token), Receive);
        return token;
    }

    /// <summary>The grid's sort request (contract A-5): a rows-only token,
    /// unless the sort equals the accepted one AND nothing is pending —
    /// with a request pending, a request for the accepted sort
    /// supersedes it (the mac's whole guard, `:360`).</summary>
    public void SetSort(GraphTableSort sort)
    {
        if (sort == Publication.AcceptedSort && _requestedSort is null)
        {
            return;
        }
        _ = Load(GraphLoadKind.RowsOnly, GraphAnnouncePolicy.Silent, sort);
    }

    private GraphLoadEnvelope Fetch(GraphLoadToken token)
    {
        GraphFilter filter = token.Request.Query.Filter;
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
            return new GraphLoadEnvelope(token, filter, token.Request.Query, token.Request.Sort, snapshot, rows, null);
        }
        catch (VaultException exception)
        {
            return new GraphLoadEnvelope(token, filter, token.Request.Query, token.Request.Sort, null, null, exception.Message);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.IO.IOException)
        {
            return new GraphLoadEnvelope(token, filter, token.Request.Query, token.Request.Sort, null, null, exception.Message);
        }
    }

    /// <summary>The receiver, at DISPATCH time on the owner context: the
    /// four-step rule of contract A-2, the mac's `receiveGraphTableRows`.</summary>
    private void Receive(GraphLoadEnvelope envelope)
    {
        GraphLoadToken token = envelope.Token;
        // (i) the token is current in every field — the lifecycle
        // generation included (IPA-6).
        if (!ReferenceEquals(token.Document, this)
            || _retired
            || !ReferenceEquals(token.Session, _session)
            || token.LifecycleGeneration != _lifecycleGeneration()
            || token.Seq != _seq
            || _request is null
            || token.Request != _request)
        {
            return;
        }
        if (token.Kind == GraphLoadKind.Pair)
        {
            _pairInFlight = false;
        }
        // (ii) the envelope answers THIS request, and its inputs agree
        // with each other — validated before EITHER arm (IPA-6): a
        // failure envelope whose filter is not its query's is as foreign
        // as a success's.
        if (envelope.Query != token.Request.Query
            || envelope.Sort != token.Request.Sort
            || envelope.Filter != envelope.Query.Filter)
        {
            return;
        }
        if (envelope.Failure is { } failure)
        {
            _requestedSort = null;
            if (token.Kind == GraphLoadKind.Pair)
            {
                Publication = Publication.AsPairFailure(failure);
            }
            AnnounceIfEffective(new GraphA11yEvent.GraphBlocked(new GraphBlockedReason.LoadFailed(failure)));
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
                // The two crossings straddled a rebuild: drop, and read again.
                _ = Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent, token.Request.Sort);
                return;
            }
            next = GraphPublication.FromPair(snapshot, envelope.Filter, rows, token.Request.Sort);
        }
        else
        {
            if (!previous.HoldsSnapshot
                || previous.Filter != envelope.Query.Filter
                || rows.Generation != previous.Generation)
            {
                _ = Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent, token.Request.Sort);
                return;
            }
            next = previous.WithRows(rows, token.Request.Sort);
        }
        // (iii) ONE swap, rows before state — the record carries both.
        bool answeredSort = _requestedSort is not null;
        _requestedSort = null;
        Publication = next;
        RevalidateSelection(next);
        PublicationInstalled?.Invoke(new GraphPublicationInstall(previous, next, answeredSort));
        if (token.Kind == GraphLoadKind.Pair)
        {
            if (token.Announce == GraphAnnouncePolicy.Summary)
            {
                AnnounceIfEffective(new GraphA11yEvent.GraphSnapshotSummary(next.Snapshot!.SummaryCounts));
            }
            if (_highWater > next.Generation)
            {
                _highWater = 0;
                _ = Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent, token.Request.Sort);
            }
        }
        else
        {
            // The mac's rows-only publish speaks the filter count
            // (`requestGraphTableRows`, `:343–347`), coalesced.
            AnnounceIfEffective(new GraphA11yEvent.GraphFilterCount(
                (uint)rows.Rows.Length, (uint)Math.Min(rows.Total, uint.MaxValue)));
        }
    }

    /// <summary>Contract A-7: the shared key survives a reorder and a
    /// filter overlay; it clears only when the SNAPSHOT no longer carries
    /// the node (the mac's `revalidateGraphSelection(against:)`).</summary>
    private void RevalidateSelection(GraphPublication publication)
    {
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
                        _ = Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
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
            GraphRowAction.ShowConnections => ShowConnectionsFromSurface is not null,
            GraphRowAction.CreateNote => CreateNoteFromSurface is not null && CreateAdmissionReason?.Invoke() is null,
            _ => false,
        };
    }

    public string? ActionDisabledReason(GraphRowAction action) =>
        action == GraphRowAction.CreateNote ? CreateAdmissionReason?.Invoke() : null;

    public void Execute(GraphRowAction action, GraphTableRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (_retired || !IsActionEnabled(action, row))
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
        Shutdown();
        _announcer.Shutdown();
        ViewState.Reset();
    }
}
