// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>The four states of the leaf's PRESENTATION (rule C, Term 7).</summary>
internal enum ConnectionsLoadState
{
    NoNote,
    Loading,
    Ready,
    Error,
}

/// <summary>The request a token carries (contracts B-2, B-8): the root,
/// the depth core clamped, the fixed local filter, and the root EPOCH at
/// issue — the receiver checks the epoch against the leaf's live one.</summary>
internal sealed record ConnectionsRequest(string Root, uint Depth, GraphFilter Filter, int Epoch);

/// <summary>The load token (contract B-2): the leaf instance, the session
/// the body was started against, the lifecycle generation, the request,
/// the sequence, and the announce policy — fixed when the load is issued
/// (rule C, Term 5).</summary>
internal sealed record ConnectionsLoadToken(
    ConnectionsLeafViewModel Document,
    VaultSession Session,
    int LifecycleGeneration,
    ConnectionsRequest Request,
    ulong Seq,
    GraphAnnouncePolicy Announce);

/// <summary>The worker ENVELOPE (rule C, Term 8): the ACTUAL arguments of
/// BOTH calls, echoed separately — the tree's path, depth and filter; the
/// bundle's path and paging (null when no bundle was fetched) — beside
/// the tree, the bundle, or the failure.</summary>
internal sealed record ConnectionsLoadEnvelope(
    ConnectionsLoadToken Token,
    string TreePath,
    uint TreeDepth,
    GraphFilter TreeFilter,
    string? BundlePath,
    Paging? BundlePaging,
    GraphConnectionsTree? Tree,
    NoteLoadBundle? Bundle,
    string? Failure);

/// <summary>The PRESENTATION record (rule C, Term 7): four states, each
/// keyed by the request that produced it, the tree authoritative — a
/// Ready tree with no rows IS the empty neighbourhood; no reload
/// indicator exists. The REQUEST in flight is the document's own state
/// (<see cref="ConnectionsLeafViewModel.Request"/>,
/// <see cref="ConnectionsLeafViewModel.InFlight"/>).</summary>
internal sealed record ConnectionsPublication(
    ConnectionsLoadState State,
    ConnectionsRequest? ProducedBy,
    GraphConnectionsTree? Tree,
    NoteLoadBundle? Bundle,
    string? Failure)
{
    public static ConnectionsPublication NoNote { get; } =
        new(ConnectionsLoadState.NoNote, null, null, null, null);

    public static ConnectionsPublication Loading(ConnectionsRequest request) =>
        new(ConnectionsLoadState.Loading, request, null, null, null);

    public static ConnectionsPublication Ready(
        ConnectionsRequest request, GraphConnectionsTree tree, NoteLoadBundle? bundle) =>
        new(ConnectionsLoadState.Ready, request, tree, bundle, null);

    public static ConnectionsPublication Error(ConnectionsRequest request, string message) =>
        new(ConnectionsLoadState.Error, request, null, null, message);

    public bool HoldsTree => State == ConnectionsLoadState.Ready && Tree is not null;

    /// <summary>The binding maps core's vectors to arrays.</summary>
    public bool HasRows => HoldsTree && (Tree!.Incoming.Length > 0 || Tree.Outgoing.Length > 0);
}

/// <summary>Raised after every install, with what it replaced.</summary>
internal sealed record ConnectionsPublicationInstall(
    ConnectionsPublication Previous,
    ConnectionsPublication Current);

/// <summary>
/// W6-2 PR B, slice B1 (#746): the Connections leaf's OWN document (rule
/// C, Term 1) — a <see cref="PanelWorkScheduler"/> beside
/// <see cref="GraphDocumentViewModel"/>, the mac <c>AppState+Connections</c>
/// twin, over the workspace's ONE relay (A-10 as amended, BD-12). It owns
/// the follow root and its epoch, the depth, the presentation and the
/// request in flight (Term 7), the generation probe's state machine
/// (Term 6), the selected occurrence, the row copy and the fetched-once
/// inventories. The TRIGGERS are the workspace's (Term 3): the shell
/// decides MOUNTED and ACTIVE and calls one entry point per trigger; this
/// document issues the load, and every load advances the sequence so
/// the newest supersedes (Term 4). Speech is gated at dispatch on the
/// leaf being ACTIVE (Term 2).
/// </summary>
internal sealed class ConnectionsLeafViewModel : PanelWorkScheduler
{
    /// <summary>The mac's bundle paging (`AppState+Connections.swift:91–92`).</summary>
    private static readonly Paging BundlePagingSpec = new(null, 200);

    private readonly VaultSession _session;
    private readonly GraphAnnouncer _announcer;
    private readonly Func<bool> _isActive;
    private readonly Func<GraphVerbosity> _verbosity;
    private readonly Func<int> _lifecycleGeneration;
    private readonly GraphFilter _filter;
    private readonly Dictionary<GraphNodeKind, IReadOnlyList<GraphRowActionSpec>> _actionsByKind;
    private ulong _seq;
    private ConnectionsRequest? _request;
    private bool _inFlight;
    private string? _root;
    private int _rootEpoch;
    private uint _depth;
    private ulong _highWater;
    private string? _selectedOccurrence;
    // Volatile (IPD-4): read from the pool by the always-async bodies and
    // by a fact's release barrier; written by Retire on the owner's thread.
    private volatile bool _retired;
    private ConnectionsPublication _publication = ConnectionsPublication.NoNote;

    public ConnectionsLeafViewModel(
        VaultSession session,
        GraphAnnouncer announcer,
        Func<bool> isActive,
        Func<GraphVerbosity> verbosity,
        SynchronizationContext? ownerContext = null,
        Func<int>? lifecycleGeneration = null)
        : base(
            synchronousForTests: false,
            ownerContext
                ?? SynchronizationContext.Current as DispatcherSynchronizationContext
                ?? new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher),
            // The owner dispatcher, named only when this document CHOSE the
            // context (IPF-3): the graph document's rule.
            ownerContext is null ? Dispatcher.CurrentDispatcher : null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(isActive);
        ArgumentNullException.ThrowIfNull(verbosity);
        _session = session;
        _announcer = announcer;
        _isActive = isActive;
        _verbosity = verbosity;
        _lifecycleGeneration = lifecycleGeneration ?? (static () => 0);
        // Design B / B-15: the fixed local filter is core's, fetched ONCE.
        CountCrossing("graph_connections_filter");
        _filter = SlateUniffiMethods.GraphConnectionsFilter();
        _actionsByKind = new Dictionary<GraphNodeKind, IReadOnlyList<GraphRowActionSpec>>
        {
            [GraphNodeKind.Note] = FetchRowActions(GraphNodeKind.Note),
            [GraphNodeKind.Attachment] = FetchRowActions(GraphNodeKind.Attachment),
            [GraphNodeKind.Ghost] = FetchRowActions(GraphNodeKind.Ghost),
        };
        // B-5: the first depth REQUEST is core's own minimum, read through
        // the process-wide accessor, clamped through core like every
        // other — the leaf never writes a literal into the depth (B-19 v).
        SetDepth(GraphCoreConstants.Once.ConnectionsDepthMin);
    }

    // --- The fetched-once inventories --------------------------------------

    public GraphFilter Filter => _filter;

    public IReadOnlyList<GraphRowActionSpec> ActionSpecs(GraphNodeKind kind) => _actionsByKind[kind];

    public bool ActionAppliesTo(GraphRowAction action, GraphNodeKind kind) =>
        _actionsByKind[kind].Any(spec => spec.Action == action);

    private IReadOnlyList<GraphRowActionSpec> FetchRowActions(GraphNodeKind kind)
    {
        CountCrossing("graph_row_actions");
        return SlateUniffiMethods.GraphRowActions(kind);
    }

    /// <summary>B-5 / B-15 / B-19 (v): the ONLY depth arithmetic the leaf
    /// performs is core's clamp through the FFI; the dataflow census
    /// asserts every write to the depth storage takes this call's result
    /// and that only four producers reach its argument.</summary>
    private uint ClampDepth(uint requested)
    {
        CountCrossing("graph_clamp_connections_depth");
        return SlateUniffiMethods.GraphClampConnectionsDepth(requested);
    }

    // --- State ---------------------------------------------------------------

    /// <summary>The follow root (Term 3(d)): the note in view, written ONLY
    /// by <see cref="NoteChanged"/> from the workspace's reconciliation.</summary>
    public string? Root => _root;

    /// <summary>The root EPOCH (Term 3(d), B-11): advanced on every root
    /// transition; every token carries the epoch it was issued under.</summary>
    public int RootEpoch => _rootEpoch;

    public uint Depth => _depth;

    /// <summary>The PRESENTATION (Term 7).</summary>
    public ConnectionsPublication Publication
    {
        get => _publication;
        private set => SetField(ref _publication, value);
    }

    /// <summary>The newest issued REQUEST (Term 7), null with no root.</summary>
    public ConnectionsRequest? Request => _request;

    /// <summary>Term 7: the newest issued sequence has not landed — a load
    /// is IN FLIGHT; the probe reads this before the presentation.</summary>
    public bool InFlight => _inFlight;

    /// <summary>Term 7 — CURRENT: the presentation's root is the leaf's
    /// root (NoNote is current when there is no root).</summary>
    public bool IsCurrent =>
        _root is null
            ? Publication.State == ConnectionsLoadState.NoNote
            : string.Equals(Publication.ProducedBy?.Root, _root, StringComparison.Ordinal);

    /// <summary>Term 7 — STALE: a root is set and the presentation is not
    /// for it; the view shows the loading label (the mac's loading row).</summary>
    public bool IsStale => _root is not null && !IsCurrent;

    public event Action<ConnectionsPublicationInstall>? PublicationInstalled;

    /// <summary>The selected OCCURRENCE (B-6): keyed by the root and the
    /// mount; a root change and a collapse clear it, a same-root refresh
    /// prunes it.</summary>
    public string? SelectedOccurrence
    {
        get => _selectedOccurrence;
        set => SetField(ref _selectedOccurrence, value);
    }

    /// <summary>Term 2 / B-6: the VIEW-STATE epoch — advanced on a root
    /// change and on a collapse of the pane, and on nothing else, so the
    /// view clears its expansion and pending focus exactly then; a
    /// same-root refresh PRUNES instead (the view's own rule).</summary>
    public int ViewStateEpoch { get; private set; }

    public bool IsRetired => _retired;

    public ulong SeqForTests => _seq;

    public ulong HighWaterForTests => _highWater;

    /// <summary>How many loads this document ISSUED (Term 3's one-per-route
    /// pins read this on the owner thread; the FFI crossings are counted
    /// on the pool and are not a synchronous witness).</summary>
    public int LoadsIssuedForTests { get; private set; }

    public GraphVerbosity Verbosity => _verbosity();

    /// <summary>The residue the seam census names: the one member that
    /// hands out the relay, for the facts.</summary>
    internal GraphAnnouncer AnnouncerForTests => _announcer;

    // --- Seams the workspace wires (B-9, B-11) ------------------------------

    internal Action<string, WorkspaceOpenTarget>? OpenRowFromSurface { get; set; }

    internal Action<string>? RevealRowFromSurface { get; set; }

    /// <summary>The create funnel, addressed by the LEAF (B-11): the
    /// ghost's path, the root it belonged to and the epoch at invocation.</summary>
    internal Action<string, string, int>? CreateNoteFromSurface { get; set; }

    internal Func<string?>? CreateAdmissionReason { get; set; }

    /// <summary>Test seam: runs inside the worker AFTER the fetch and
    /// before the envelope returns.</summary>
    internal Action? FetchGateForTests { get; set; }

    /// <summary>Test seam: rewrite the envelope the body returns — the
    /// receiver's rejections are stated over its echoes (Term 8).</summary>
    internal Func<ConnectionsLoadEnvelope, ConnectionsLoadEnvelope>? EnvelopeForTests { get; set; }

    /// <summary>Every FFI crossing the facts count takes this ONE locked
    /// increment, whichever thread crosses — the pool's fetch and the
    /// owner's apply can overlap (codoki on PR #1184).</summary>
    private void CountCrossing(string name)
    {
        lock (CrossingsForTests)
        {
            CrossingsForTests[name]++;
        }
    }

    internal Dictionary<string, int> CrossingsForTests { get; } = new(StringComparer.Ordinal)
    {
        ["graph_connections_tree"] = 0,
        ["note_load_bundle"] = 0,
        ["graph_generation"] = 0,
        ["graph_row_actions"] = 0,
        ["graph_connections_filter"] = 0,
        ["graph_clamp_connections_depth"] = 0,
        ["graph_stable_key_for_path"] = 0,
    };

    // --- The row copy (B-8) ---------------------------------------------------

    public static GraphRowCopy RowCopy(GraphConnectionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new GraphRowCopy(row.Label, row.Kind, row.InLinks, row.OutLinks, row.References, row.EmbedOnly);
    }

    /// <summary>The row's UIA Name: P1's copy at the set verbosity,
    /// rendered through the relay without posting (B-8).</summary>
    public string RowName(GraphConnectionRow row) =>
        GraphAnnouncer.RenderLabel(new GraphA11yEvent.GraphRow(Verbosity, RowCopy(row)));

    // --- Announcements ----------------------------------------------------------

    /// <summary>A status the workspace asks for: the graph family's panel
    /// line on a Show of the already-active leaf (B-D5).</summary>
    internal void AnnounceStatus(GraphStatusNote note)
    {
        if (!_retired)
        {
            _announcer.Announce(new GraphA11yEvent.GraphStatus(note));
        }
    }

    /// <summary>Term 9: focus ENTERED the anchor with nothing to read —
    /// STALE or Loading → the loading status, the empty neighbourhood →
    /// NoConnections; Error, NoNote and rows → nothing, the element reads.</summary>
    public void FocusEntered()
    {
        if (_retired)
        {
            return;
        }
        ConnectionsPublication publication = Publication;
        if (IsStale || publication.State == ConnectionsLoadState.Loading)
        {
            AnnounceStatus(new GraphStatusNote.LoadingConnections());
        }
        else if (publication.HoldsTree && !publication.HasRows)
        {
            AnnounceStatus(new GraphStatusNote.NoConnections());
        }
    }

    /// <summary>Term 2: SPEAKING is ACTIVE at dispatch and nowhere earlier.</summary>
    private void AnnounceIfSpeaking(GraphA11yEvent @event)
    {
        if (!_retired && _isActive())
        {
            _announcer.Announce(@event);
        }
    }

    // --- The trigger entry points (rule C, Term 3; the workspace decides) ------

    /// <summary>Term 3(a): a MOUNT with the leaf ACTIVE at the boundary — an
    /// audible load whether or not a tree is held (the mac's `onAppear`).</summary>
    public void Mounted()
    {
        if (!_retired && _root is not null)
        {
            _ = Load(GraphAnnouncePolicy.Summary);
        }
    }

    /// <summary>Term 3(b): a leaf switch while MOUNTED — an audible load
    /// ONLY when the presentation is not current for the root (the mac's
    /// mounted switch neither reloads nor speaks; the stale case is B-D11).</summary>
    public void Shown()
    {
        if (!_retired && _root is not null && !IsCurrent)
        {
            _ = Load(GraphAnnouncePolicy.Summary);
        }
    }

    /// <summary>Term 3(c): the palette's Show — one explicit audible load
    /// always (`showConnectionsPanel`).</summary>
    public void Show()
    {
        if (!_retired && _root is not null)
        {
            _ = Load(GraphAnnouncePolicy.Summary);
        }
    }

    /// <summary>Terms 3(d) and 3(g): the workspace's reconciliation hands
    /// the note in view and whether the leaf is ACTIVE and MOUNTED. A
    /// same-path call is a no-op; a change advances the epoch and the
    /// sequence (an in-flight result for the old root is foreign) and
    /// clears the selection; with no note the NoNote transition installs
    /// synchronously; otherwise a load starts only while active and
    /// mounted — else the presentation is STALE until (a), (b) or the probe.</summary>
    public void NoteChanged(string? path, bool activeAndMounted)
    {
        if (_retired || string.Equals(path, _root, StringComparison.Ordinal))
        {
            return;
        }
        _root = path;
        _rootEpoch++;
        _seq++;
        _inFlight = false;
        SelectedOccurrence = null;
        ViewStateEpoch++;
        OnPropertyChanged(nameof(Root));
        OnPropertyChanged(nameof(RootEpoch));
        OnPropertyChanged(nameof(InFlight));
        OnPropertyChanged(nameof(ViewStateEpoch));
        if (path is null)
        {
            _request = null;
            OnPropertyChanged(nameof(Request));
            Install(ConnectionsPublication.NoNote);
            return;
        }
        OnPropertyChanged(nameof(IsCurrent));
        OnPropertyChanged(nameof(IsStale));
        if (activeAndMounted)
        {
            _ = Load(GraphAnnouncePolicy.Summary);
        }
    }

    /// <summary>Term 3(e), B-5, B-14: the depth through core's clamp; a
    /// bound is a no-op (the mac's guard); a change with a root reloads
    /// audibly; without a root, nothing.</summary>
    public void SetDepth(uint requested)
    {
        uint clamped = ClampDepth(requested);
        if (_retired || clamped == _depth)
        {
            return;
        }
        _depth = clamped;
        OnPropertyChanged(nameof(Depth));
        if (_root is not null)
        {
            _ = Load(GraphAnnouncePolicy.Summary);
        }
    }

    /// <summary>The mac's `connectionsDeeper` / `connectionsShallower`
    /// (`AppState+Connections.swift:171–172`) — the two arithmetic
    /// producers B-19 (v) allows.</summary>
    public void Deeper() => SetDepth(_depth + 1);

    // The depth is never below core's floor (every write is the clamp's
    // result), so the subtraction cannot underflow; no host comparison
    // guards it (B-19 v).
    public void Shallower() => SetDepth(_depth - 1);

    /// <summary>Term 2: the pane collapsed — the view-local selection
    /// clears, as the mac's destruction of the pane clears its `@State`;
    /// the document's root, depth, epoch and presentation survive.</summary>
    public void ViewCollapsed()
    {
        if (!_retired)
        {
            SelectedOccurrence = null;
            ViewStateEpoch++;
            OnPropertyChanged(nameof(ViewStateEpoch));
        }
    }

    /// <summary>Term 6, B-12: the generation probe as a state machine —
    /// a load IN FLIGHT → the high-water mark only (the install compares,
    /// A-3); else with a root: Ready and CURRENT with an older tree → a
    /// silent load; Ready and equal → nothing; Error or STALE → one silent
    /// load (the mac reloads in every state); no root → nothing. It never
    /// supersedes an audible load (B-D12).</summary>
    public void Probe()
    {
        if (_retired)
        {
            return;
        }
        StartWorkAlwaysAsync(
            () =>
            {
                CountCrossing("graph_generation");
                return _session.GraphGeneration();
            },
            generation =>
            {
                if (_retired || _root is null)
                {
                    return;
                }
                if (_inFlight)
                {
                    _highWater = Math.Max(_highWater, generation);
                    return;
                }
                ConnectionsPublication held = Publication;
                if (held.HoldsTree && IsCurrent)
                {
                    if (generation != held.Tree!.Generation)
                    {
                        _ = Load(GraphAnnouncePolicy.Silent);
                    }
                    return;
                }
                // Error, or STALE (a root the presentation is not for):
                // one silent load, so the leaf cannot stay errored or
                // stale without a user action (IGH-2).
                _ = Load(GraphAnnouncePolicy.Silent);
            });
    }

    // --- The load (Terms 4–8) -------------------------------------------------------

    /// <summary>Issue a token and start the body: a new sequence every
    /// call, so this load supersedes any in flight, silent or audible.</summary>
    internal ConnectionsLoadToken Load(GraphAnnouncePolicy announce)
    {
        if (_retired)
        {
            throw new InvalidOperationException("the connections leaf is retired");
        }
        string root = _root ?? throw new InvalidOperationException("a load needs a root (rule C, Term 3)");
        _seq++;
        LoadsIssuedForTests++;
        var request = new ConnectionsRequest(root, _depth, _filter, _rootEpoch);
        _request = request;
        _inFlight = true;
        OnPropertyChanged(nameof(Request));
        OnPropertyChanged(nameof(InFlight));
        if (!IsCurrent)
        {
            // Term 7's start transition: a different root installs
            // Loading from any state; a same-root reload keeps the
            // presentation — Ready's rows, Error's message, Loading —
            // with no indicator.
            Install(ConnectionsPublication.Loading(request));
        }
        var token = new ConnectionsLoadToken(this, _session, _lifecycleGeneration(), request, _seq, announce);
        StartWorkAlwaysAsync(() => Fetch(token), Receive);
        return token;
    }

    private ConnectionsLoadEnvelope Fetch(ConnectionsLoadToken token)
    {
        ConnectionsRequest request = token.Request;
        string? bundlePath = null;
        Paging? bundlePaging = null;
        ConnectionsLoadEnvelope envelope;
        try
        {
            CountCrossing("graph_connections_tree");
            GraphConnectionsTree tree = token.Session.GraphConnectionsTree(request.Root, request.Depth, request.Filter);
            NoteLoadBundle? bundle = null;
            if (request.Depth == 1)
            {
                bundlePath = request.Root;
                bundlePaging = BundlePagingSpec;
                CountCrossing("note_load_bundle");
                bundle = token.Session.NoteLoadBundle(bundlePath, bundlePaging);
            }
            FetchGateForTests?.Invoke();
            envelope = new ConnectionsLoadEnvelope(
                token, request.Root, request.Depth, request.Filter, bundlePath, bundlePaging, tree, bundle, null);
        }
        catch (VaultException exception)
        {
            // The test seam parks a FAILING fetch as it parks a successful one
            // (the model holds an Error's reload in flight across a route).
            FetchGateForTests?.Invoke();
            envelope = new ConnectionsLoadEnvelope(
                token, request.Root, request.Depth, request.Filter, bundlePath, bundlePaging, null, null, exception.Message);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.IO.IOException)
        {
            FetchGateForTests?.Invoke();
            envelope = new ConnectionsLoadEnvelope(
                token, request.Root, request.Depth, request.Filter, bundlePath, bundlePaging, null, null, exception.Message);
        }
        return EnvelopeForTests is { } rewrite ? rewrite(envelope) : envelope;
    }

    /// <summary>The receiver, at DISPATCH on the owner context (Term 8):
    /// the token's every field — the epoch against the leaf's LIVE root
    /// and epoch included — then BOTH echoes against the request, then
    /// the tree's depth and centre key, before EITHER arm. A rejection
    /// changes nothing but the in-flight flag of the newest sequence.</summary>
    private void Receive(ConnectionsLoadEnvelope envelope)
    {
        ConnectionsLoadToken token = envelope.Token;
        if (!ReferenceEquals(token.Document, this)
            || _retired
            || !ReferenceEquals(token.Session, _session)
            || token.Seq != _seq)
        {
            return;
        }
        // The newest sequence has landed, accepted or not: nothing is in
        // flight until the next Load (Term 6's probe reads this) — whatever
        // the checks below decide.
        _inFlight = false;
        OnPropertyChanged(nameof(InFlight));
        // The LIVE root and epoch (Term 8, IGH-3): the request record
        // carries the epoch beside the root, so its identity check IS the
        // epoch check, and every root transition advances the sequence, so
        // a result for the old root fails the check above — a separate
        // comparison against `_root` and `_rootEpoch` was an equivalent
        // mutant (TGB-2) and is not written twice.
        if (token.LifecycleGeneration != _lifecycleGeneration()
            || _request is null
            || token.Request != _request)
        {
            return;
        }
        ConnectionsRequest request = token.Request;
        if (!string.Equals(envelope.TreePath, request.Root, StringComparison.Ordinal)
            || envelope.TreeDepth != request.Depth
            || envelope.TreeFilter != request.Filter)
        {
            return;
        }
        bool wantBundle = request.Depth == 1;
        bool bundleEchoAgrees = wantBundle
            ? string.Equals(envelope.BundlePath, request.Root, StringComparison.Ordinal)
                && envelope.BundlePaging == BundlePagingSpec
            : envelope.BundlePath is null && envelope.BundlePaging is null;
        bool bundleNotReached = wantBundle && envelope.BundlePath is null && envelope.BundlePaging is null;
        if (envelope.Failure is { } failure)
        {
            // A failure envelope whose echoes disagree is as foreign as a
            // success's (IPA-6's shape); a failure before the bundle call
            // carries no bundle echo at depth one — accepted.
            if (!bundleEchoAgrees && !bundleNotReached)
            {
                return;
            }
            Install(ConnectionsPublication.Error(request, failure));
            // The policy rides the token (Term 5) for the failure line as for
            // the summary: the mac's `speak = announce && active`
            // (`AppState+Connections.swift:118, 135`) — a silent reload that
            // fails installs Error and says nothing (B-10's probe row; the
            // MODEL found the line spoken on a deleted root, TGB-8).
            if (token.Announce == GraphAnnouncePolicy.Summary)
            {
                AnnounceIfSpeaking(new GraphA11yEvent.GraphBlocked(new GraphBlockedReason.ConnectionsLoadFailed(failure)));
            }
            return;
        }
        if (!bundleEchoAgrees || (wantBundle && envelope.Bundle is null) || envelope.Tree is null)
        {
            return;
        }
        GraphConnectionsTree tree = envelope.Tree;
        CountCrossing("graph_stable_key_for_path");
        if (tree.Depth != request.Depth
            || !string.Equals(tree.CenterKey, SlateUniffiMethods.GraphStableKeyForPath(request.Root), StringComparison.Ordinal))
        {
            return;
        }
        PruneSelection(tree);
        Install(ConnectionsPublication.Ready(request, tree, envelope.Bundle));
        if (token.Announce == GraphAnnouncePolicy.Summary)
        {
            AnnounceIfSpeaking(new GraphA11yEvent.GraphNeighborhoodSummary(tree.SummaryCounts));
        }
        if (_highWater > tree.Generation)
        {
            _highWater = 0;
            _ = Load(GraphAnnouncePolicy.Silent);
        }
    }

    private void Install(ConnectionsPublication next)
    {
        ConnectionsPublication previous = Publication;
        Publication = next;
        OnPropertyChanged(nameof(IsCurrent));
        OnPropertyChanged(nameof(IsStale));
        PublicationInstalled?.Invoke(new ConnectionsPublicationInstall(previous, next));
    }

    /// <summary>B-6: a same-root refresh prunes the selection to the ids
    /// the new tree carries.</summary>
    private void PruneSelection(GraphConnectionsTree tree)
    {
        if (_selectedOccurrence is { } id
            && !tree.Incoming.Any(row => row.Id == id)
            && !tree.Outgoing.Any(row => row.Id == id))
        {
            SelectedOccurrence = null;
        }
    }

    // --- Actions (B-9) -------------------------------------------------------------

    public bool IsActionEnabled(GraphRowAction action, GraphConnectionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return action switch
        {
            GraphRowAction.Open or GraphRowAction.OpenInNewTab => row.Path is not null && OpenRowFromSurface is not null,
            GraphRowAction.Reveal => row.Path is not null && RevealRowFromSurface is not null,
            GraphRowAction.ShowConnections => false,
            GraphRowAction.CreateNote => CreateNoteFromSurface is not null && CreateAdmissionReason?.Invoke() is null,
            _ => false,
        };
    }

    /// <summary>B-9 / B-D6: Show connections carries the one B1 reason on
    /// the menu action alone; Create note the host's admission reason.</summary>
    public string? ActionDisabledReason(GraphRowAction action) => action switch
    {
        GraphRowAction.ShowConnections => ConnectionsPhrase.ShowConnectionsUnavailable,
        GraphRowAction.CreateNote => CreateAdmissionReason?.Invoke(),
        _ => null,
    };

    /// <summary>B-9: the ROW's hint is its activation's — T15 for a ghost,
    /// T16 otherwise; a disabled create's reason replaces the ghost's.</summary>
    public string RowHint(GraphConnectionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Kind == GraphNodeKind.Ghost)
        {
            return CreateAdmissionReason?.Invoke() ?? ConnectionsPhrase.GhostHint;
        }
        return ConnectionsPhrase.NoteHint;
    }

    /// <summary>Whether the current publication's tree still lists the
    /// row — by its occurrence id, which names the same target (B-6): a
    /// row of an earlier tree of the same root that the refresh kept is
    /// the same row; a row of a tree the document no longer holds is not.</summary>
    internal bool IsRowCurrent(GraphConnectionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        // The tree must be the CURRENT root's (codex post-implementation pass
        // 3, IPB-12): a root change while inactive or unmounted keeps the old
        // tree rendered and STALE (Term 3(d)), and a row of that tree must
        // not act against the root that replaced it.
        ConnectionsPublication publication = Publication;
        return publication.Tree is { } tree
            && publication.ProducedBy is { } produced
            && string.Equals(produced.Root, _root, StringComparison.Ordinal)
            && (tree.Incoming.Any(current => current.Id == row.Id)
                || tree.Outgoing.Any(current => current.Id == row.Id));
    }

    public void Execute(GraphRowAction action, GraphConnectionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        // A row acts only while the tree that rendered it is the one held
        // (codex post-implementation pass 2, IPB-6): a screen reader's cached
        // element can invoke a row the view dropped after a root change, a
        // refresh or a release, and a ghost's create would pair a stale
        // target with the CURRENT root and epoch.
        if (_retired || !IsRowCurrent(row) || !IsActionEnabled(action, row) || _root is not { } root)
        {
            return;
        }
        switch (action)
        {
            case GraphRowAction.Open:
                OpenRowFromSurface!(row.Path!, WorkspaceOpenTarget.CurrentTab);
                break;
            case GraphRowAction.OpenInNewTab:
                OpenRowFromSurface!(row.Path!, WorkspaceOpenTarget.NewTab);
                break;
            case GraphRowAction.Reveal:
                RevealRowFromSurface!(row.Path!);
                break;
            case GraphRowAction.CreateNote:
                CreateNoteFromSurface!(SlateUniffiMethods.GraphGhostNotePath(row.TargetRaw), root, _rootEpoch);
                break;
            default:
                break;
        }
    }

    /// <summary>Activation (B-9): Open for a note or attachment, Create
    /// note for a ghost; Ctrl opens in a new tab.</summary>
    public void Activate(GraphConnectionRow row, bool newTab)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Kind == GraphNodeKind.Ghost)
        {
            Execute(GraphRowAction.CreateNote, row);
        }
        else
        {
            Execute(newTab ? GraphRowAction.OpenInNewTab : GraphRowAction.Open, row);
        }
    }

    // --- Retirement (B-1) -------------------------------------------------------------

    /// <summary>Retire with the workspace (B-1): the sequence advanced so
    /// every parked envelope is foreign, this document's pending classes
    /// dropped from the SHARED relay (A-1 as amended: the relay stays
    /// live and the workspace shuts it down after both documents), the
    /// presentation cleared.</summary>
    public void Retire()
    {
        if (_retired)
        {
            return;
        }
        _retired = true;
        _seq++;
        _inFlight = false;
        _request = null;
        _announcer.DropAllPending();
        Shutdown();
        Install(ConnectionsPublication.NoNote);
    }
}
