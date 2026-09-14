// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>The four reads of a build or a refresh, taken under the session's
/// own lock in ONE gate call (Term G2, Term G6): the ids in slot order, the
/// collapsed edges, the node metadata and the generation they belong to.</summary>
internal sealed record GraphDiagramTopologyRead(ulong[] Ids, GraphEdge[] Edges, GraphNode[] Nodes, ulong Generation);

/// <summary>
/// W6-2 PR D (#746), rule G, Term G1: the diagram lineage — ONE
/// <see cref="LayoutSession"/>, the backend filter it was built under, the
/// layout's node ids in slot order, the node metadata by id, the collapsed
/// edges, the layout's generation, the pinned ids and the build sequence
/// that created it (the mac's <c>GraphDiagramModel</c>). The document holds
/// at most one; a model is CREATED by the build, REPLACED by the rebuild and
/// DISPOSED by the teardown — never mutated into another filter's.
/// <para>Term G7 (DD-19, the design pass): the session is PRIVATE and
/// reachable through exactly one member, <see cref="WithSession{T}"/> — an
/// ADMISSION GATE with a count, the binding's own <c>CallWithPointer</c>
/// pattern lifted one level: a call is admitted only while the model is
/// live, a retired model refuses every later call with a value of the
/// call's own shape, and the handle is freed exactly once, by whichever
/// comes last — the retirement or the last admitted call's return. No
/// site catches <c>ObjectDisposedException</c>: the throw is unreachable.</para>
/// </summary>
internal sealed class GraphDiagramModel
{
    // Term G7: read by WithSession alone, freed by FreeHandle alone (the
    // gate's wall, D-15 iii).
    private readonly LayoutSession _session;
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _crossings;
    private int _inFlight;
    private bool _retired;
    private bool _freed;

    /// <summary>Constructed ON THE POOL in the build's compute around the
    /// fresh handle (IGT-1, IGT-2): the gate is born with the session, and
    /// no raw session exists outside a model at any instant.</summary>
    internal GraphDiagramModel(
        LayoutSession session,
        GraphFilter filter,
        LayoutForces forces,
        ulong buildSequence,
        GraphLayoutDriver.Scheduler scheduler,
        Func<bool> reduceMotion,
        Dictionary<string, int> crossings)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(forces);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(reduceMotion);
        ArgumentNullException.ThrowIfNull(crossings);
        _session = session;
        _crossings = crossings;
        Filter = filter;
        Forces = forces;
        BuildSequence = buildSequence;
        NodeIds = [];
        NodesById = new Dictionary<ulong, GraphNode>();
        Edges = [];
        Positions = new Dictionary<ulong, GraphPoint>();
        // D-15 ii: exactly one driver per model, born here.
        Driver = new GraphLayoutDriver(this, scheduler, reduceMotion);
    }

    /// <summary>The backend filter the layout was built under (design A): a
    /// query under another filter is never issued against this model.</summary>
    internal GraphFilter Filter { get; }

    /// <summary>The forces the layout runs under — the captured ones, then
    /// <see cref="SetForces"/>'s.</summary>
    internal LayoutForces Forces { get; private set; }

    /// <summary>Term G2's captured sequence: the apply installs the model
    /// only when it is still the document's current one.</summary>
    internal ulong BuildSequence { get; }

    /// <summary>Term G4: the settle loop over the document's scheduler.</summary>
    internal GraphLayoutDriver Driver { get; }

    /// <summary>Backend node ids in slot order: <c>NodeIds[i]</c> owns
    /// <c>Positions[2i], [2i+1]</c> of every frame.</summary>
    internal ulong[] NodeIds { get; private set; }

    /// <summary>The node metadata by id — the row copy's and the actions'
    /// source BEFORE the first epoch lands (Term G3).</summary>
    internal IReadOnlyDictionary<ulong, GraphNode> NodesById { get; private set; }

    /// <summary>The collapsed edges among <see cref="NodeIds"/>.</summary>
    internal GraphEdge[] Edges { get; private set; }

    /// <summary>The layout's generation — what every frame carries; a frame
    /// from another generation is dropped (Term G5).</summary>
    internal ulong Generation { get; private set; }

    /// <summary>The ids the user pinned; a refresh prunes the ones the
    /// topology lost (Term G6).</summary>
    internal HashSet<ulong> Pinned { get; } = [];

    /// <summary>Term G3: the accepted epoch's record — null until the first
    /// epoch of the model lands; written by the document's topology apply
    /// alone.</summary>
    internal GraphTopology? Topology { get; set; }

    /// <summary>Term G5: the position map of the last applied frame, by id.</summary>
    internal IReadOnlyDictionary<ulong, GraphPoint> Positions { get; private set; }

    /// <summary>The last frame the driver applied, null before the first.</summary>
    internal LayoutFrame? LastFrame { get; private set; }

    internal bool IsRetired
    {
        get
        {
            lock (_gate)
            {
                return _retired;
            }
        }
    }

    internal int InFlightForTests
    {
        get
        {
            lock (_gate)
            {
                return _inFlight;
            }
        }
    }

    internal bool IsHandleFreedForTests
    {
        get
        {
            lock (_gate)
            {
                return _freed;
            }
        }
    }

    /// <summary>Test seam: runs INSIDE an admitted call, before the session is
    /// touched — a fact parks here to prove the teardown waits for the
    /// call's return (D-4, D-6).</summary>
    internal Action? InsideGateForTests { get; set; }

    /// <summary>
    /// Term G7, the ONE gate: under the model's lock, REFUSED when the model
    /// is retired — <paramref name="refused"/> returned and no handle
    /// touched — else the in-flight count incremented; <paramref name="call"/>
    /// runs OUTSIDE the lock; on return the count is decremented and, when it
    /// reaches zero AND the model is retired, the handle is freed. Every
    /// session call in the shell is a <paramref name="call"/> handed here
    /// with a refused value OF ITS OWN SHAPE (IGS-1).
    /// </summary>
    internal T WithSession<T>(Func<LayoutSession, T> call, T refused)
    {
        ArgumentNullException.ThrowIfNull(call);
        lock (_gate)
        {
            if (_retired)
            {
                return refused;
            }
            _inFlight++;
        }
        try
        {
            InsideGateForTests?.Invoke();
            return call(_session);
        }
        finally
        {
            bool free;
            lock (_gate)
            {
                _inFlight--;
                free = _retired && _inFlight == 0 && !_freed;
                if (free)
                {
                    _freed = true;
                }
            }
            if (free)
            {
                FreeHandle();
            }
        }
    }

    /// <summary>Term G7's last step: later calls are refused; the handle is
    /// freed at the count's zero — at once when nothing is in flight.
    /// Idempotent: the refusal, the sweep and the teardown may each reach it.</summary>
    internal void Retire()
    {
        bool free;
        lock (_gate)
        {
            if (_retired)
            {
                return;
            }
            _retired = true;
            free = _inFlight == 0 && !_freed;
            if (free)
            {
                _freed = true;
            }
        }
        if (free)
        {
            FreeHandle();
        }
    }

    // The ONE disposal site (D-15 iii): reached from the gate's exit and
    // from Retire, never from anywhere else.
    private void FreeHandle() => _session.Dispose();

    /// <summary>The build's seed and the refresh's adoption (Term G6): the
    /// ids, the metadata, the edges and the generation replaced, the pins the
    /// topology lost pruned. The position map stands — a frame of the new
    /// generation re-pairs it.</summary>
    internal void Adopt(GraphDiagramTopologyRead read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var byId = new Dictionary<ulong, GraphNode>(read.Nodes.Length);
        foreach (GraphNode node in read.Nodes)
        {
            byId[node.Id] = node;
        }
        NodeIds = read.Ids;
        NodesById = byId;
        Edges = read.Edges;
        Generation = read.Generation;
        _ = Pinned.RemoveWhere(id => !byId.ContainsKey(id));
    }

    /// <summary>Term G5: an applied frame replaces the position map; the
    /// driver judged its currency first.</summary>
    internal void AdoptFrame(LayoutFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var positions = new Dictionary<ulong, GraphPoint>(NodeIds.Length);
        for (int i = 0; i < NodeIds.Length; i++)
        {
            positions[NodeIds[i]] = new GraphPoint(NodeIds[i], frame.Positions[2 * i], frame.Positions[(2 * i) + 1]);
        }
        Positions = positions;
        LastFrame = frame;
    }

    /// <summary>Term N7 through the gate (Term G8's synchronous mutator, the
    /// mac's <c>:260</c>, <c>:264</c>): pin the id at its position, or unpin
    /// it; false — nothing changed — once the model is retired (IGS-2).</summary>
    internal bool TogglePin(ulong id, float x, float y)
    {
        bool pin = !Pinned.Contains(id);
        bool admitted = WithSession(
            session =>
            {
                if (pin)
                {
                    Count("layout_pin_node");
                    session.PinNode(id, x, y);
                }
                else
                {
                    Count("layout_unpin_node");
                    session.UnpinNode(id);
                }
                return true;
            },
            false);
        if (admitted)
        {
            if (pin)
            {
                _ = Pinned.Add(id);
            }
            else
            {
                _ = Pinned.Remove(id);
            }
        }
        return admitted;
    }

    /// <summary>PR E's forces edit and the install's re-read (Term G2)
    /// through the gate: re-tunes the live layout; false once retired.</summary>
    internal bool SetForces(LayoutForces forces)
    {
        ArgumentNullException.ThrowIfNull(forces);
        bool admitted = WithSession(
            session =>
            {
                Count("layout_set_forces");
                session.SetForces(forces);
                return true;
            },
            false);
        if (admitted)
        {
            Forces = forces;
        }
        return admitted;
    }

    /// <summary>The document's crossing counter, shared (the seam facts read
    /// one dictionary).</summary>
    internal void Count(string crossing)
    {
        lock (_crossings)
        {
            _crossings[crossing] = _crossings.TryGetValue(crossing, out int n) ? n + 1 : 1;
        }
    }
}
