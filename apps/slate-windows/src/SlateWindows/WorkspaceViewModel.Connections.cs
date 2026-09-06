// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W6-2 PR B, slice B1 (#746): the workspace half of the Connections leaf
/// — the leaf's construction over the workspace's ONE graph relay (A-1
/// and A-10 as amended, BD-12), the TRIGGERS of rule C's Term 3 classified
/// once per mutation at its boundary (the shell decides MOUNTED and
/// ACTIVE and calls one entry point per trigger; the census names these
/// callers and no other), the pending mount and its consumers, the root's
/// buffered reconciliation, the three chordless commands (B-14), the
/// addressed open, the probe arm and the shutdown drain. The leaf's
/// document lives in <c>Graph/</c>; this partial is the shell's side of
/// the wall.
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    private const string ConnectionsLeafId = "connections";

    /// <summary>The leaf's document (B-1): PUBLIC for the leaf body's
    /// <c>Model</c> binding, constructed before the restore.</summary>
    public ConnectionsLeafViewModel Connections { get; }

    /// <summary>Term 3(a): armed when the pane becomes visible (and seeded
    /// once by the constructor); consumed at the END of the route that
    /// revealed it, with the leaf active THEN (IGG-3, IGH-7).</summary>
    private bool _mountPending;

    /// <summary>Term 3's precedence: the palette's Show issues the one
    /// load itself; the mount and switch its reveal causes are inert.</summary>
    private bool _suppressConnectionsTriggers;

    /// <summary>Term 3(d): the constructor's first sync is the initial
    /// value, not a change — no load; launch is the seeded mount's.</summary>
    private bool _constructingConnections = true;

    /// <summary>Term 3: the candidate root recorded inside a workspace
    /// mutation, reconciled ONCE at the outermost boundary.</summary>
    private bool _connectionsRootPending;
    private string? _connectionsRootCandidate;

    private RelayCommand? _showConnectionsCommand;
    private RelayCommand? _connectionsDeeperCommand;
    private RelayCommand? _connectionsShallowerCommand;

    /// <summary>`slate.graph.showConnections` (B-14): the palette's route
    /// to <see cref="ShowConnections"/>.</summary>
    public System.Windows.Input.ICommand ShowConnectionsCommand =>
        _showConnectionsCommand ??= new RelayCommand(_ => ShowConnections(), _ => true);

    /// <summary>`slate.graph.connectionsDeeper` (B-14): always enabled; a
    /// bound is a no-op through core's clamp (the mac's guard).</summary>
    public System.Windows.Input.ICommand ConnectionsDeeperCommand =>
        _connectionsDeeperCommand ??= new RelayCommand(_ => Connections.Deeper(), _ => true);

    /// <summary>`slate.graph.connectionsShallower` (B-14).</summary>
    public System.Windows.Input.ICommand ConnectionsShallowerCommand =>
        _connectionsShallowerCommand ??= new RelayCommand(_ => Connections.Shallower(), _ => true);

    /// <summary>Rule C, Term 2 — ACTIVE: the leaf is the active leaf, the
    /// mac's predicate; pane visibility is not consulted (B-D3).</summary>
    internal bool ConnectionsLeafIsActive() =>
        string.Equals(ActiveLeaf.Id, ConnectionsLeafId, StringComparison.Ordinal);

    /// <summary>Rule C, Term 2 — MOUNTED: the right pane is visible.</summary>
    internal bool ConnectionsLeafIsMounted() => IsRightPaneVisible;

    internal bool ConnectionsMountPendingForTests => _mountPending;

    /// <summary>The leaf's document over the shared relay — the twin of
    /// <see cref="NewGraphDocument"/>.</summary>
    private ConnectionsLeafViewModel NewConnectionsLeaf()
    {
        var leaf = new ConnectionsLeafViewModel(
            _session,
            _graphRelay,
            isActive: () => ConnectionsLeafIsActive(),
            verbosity: () => GraphVerbosity.Standard,
            lifecycleGeneration: () => LifecycleGeneration());
        leaf.OpenRowFromSurface = (path, target) => OpenConnectionsRowFromSurface(path, target);
        leaf.RevealRowFromSurface = path => RevealConnectionsRowFromSurface(path);
        leaf.CreateNoteFromSurface = (path, root, epoch) => CreateConnectionsNoteFromSurface(path, root, epoch);
        leaf.CreateAdmissionReason = () => GraphCreateAdmissionReason?.Invoke();
        return leaf;
    }

    // --- The triggers (rule C, Term 3) -------------------------------------------

    /// <summary>Called by the <see cref="IsRightPaneVisible"/> setter after
    /// the shell's line: a reveal ARMS the mount (consumed by the route's
    /// end); a collapse disarms it and clears the view-local state (Term 2).</summary>
    private void OnRightPaneVisibilityChanged(bool visible)
    {
        if (visible)
        {
            _mountPending = true;
        }
        else
        {
            _mountPending = false;
            Connections.ViewCollapsed();
        }
    }

    /// <summary>Term 3(a): the END of a route that revealed the pane — the
    /// toggle, directional focus, Show, the leaf-revealing commands, the
    /// outermost mutation boundary, the constructor's seeded mount. Loads
    /// the leaf when it is the active leaf NOW; the census asserts every
    /// writer of <c>IsRightPaneVisible = true</c> reaches here.</summary>
    private void ConsumePendingMount()
    {
        if (!_mountPending)
        {
            return;
        }
        _mountPending = false;
        if (!_suppressConnectionsTriggers && ConnectionsLeafIsActive())
        {
            Connections.Mounted();
        }
    }

    /// <summary>The constructor's seed (Term 3(a), B-1, IGH-7): the pane's
    /// field is initialised true and the snapshot carries no visibility,
    /// so nothing arms the initial mount — the constructor does, after
    /// the leaf's construction, and consumes it after the restore and the
    /// first sync, posting no <c>RightPaneShown</c>.</summary>
    private void SeedInitialConnectionsMount() => _mountPending = IsRightPaneVisible;

    private void FinishConnectionsConstruction()
    {
        _constructingConnections = false;
        ConsumePendingMount();
    }

    /// <summary>Term 3(b): the active leaf changed — a SWITCH while
    /// mounted loads only when the presentation is stale; inside a reveal
    /// (a pending mount) or a Show, the switch is consumed by them.
    /// Called by the <see cref="ActiveLeaf"/> setter after the shell's
    /// line.</summary>
    private void OnLeafShown(WorkspaceLeafOption leaf)
    {
        if (!_suppressConnectionsTriggers
            && !_mountPending
            && string.Equals(leaf.Id, ConnectionsLeafId, StringComparison.Ordinal)
            && ConnectionsLeafIsMounted())
        {
            Connections.Shown();
        }
    }

    /// <summary>Term 3(c): the palette's Show Connections (B-14; the mac's
    /// <c>showConnectionsPanel</c>): reveal the pane (the shell's
    /// `RightPaneShown`, B-D9), make the leaf active — the setter posts the
    /// shell's panel line — or, when already active, post the graph
    /// family's line (B-D5); the mount and switch are consumed inert;
    /// then ONE explicit audible load (B-D7) and focus to the pane.</summary>
    public void ShowConnections()
    {
        _suppressConnectionsTriggers = true;
        try
        {
            if (!IsRightPaneVisible)
            {
                IsRightPaneVisible = true;
            }
            if (ConnectionsLeafIsActive())
            {
                Connections.AnnounceStatus(new GraphStatusNote.ConnectionsPanel());
            }
            else
            {
                ActiveLeaf = Leaves.First(option => string.Equals(option.Id, ConnectionsLeafId, StringComparison.Ordinal));
            }
            ConsumePendingMount();
        }
        finally
        {
            _suppressConnectionsTriggers = false;
        }
        Connections.Show();
        FocusBoundaryRequested?.Invoke(this, WorkspaceFocusBoundary.RightPane);
    }

    /// <summary>Terms 3(d) and 3(g), B-19 (iv): <c>SyncPanels()</c> hands
    /// the note in view — the markdown tab, as the note panels' funnel
    /// has it. Inside a workspace mutation the candidate is RECORDED and
    /// reconciled once at the outermost boundary (a split's sole-tab
    /// close is one A → B transition, IGH-4); outside one, immediately.</summary>
    private void SyncConnectionsRoot()
    {
        string? candidate = ActiveGroup.ActiveTab is { IsMarkdown: true } tab ? tab.Path : null;
        if (_persistenceBatchDepth > 0)
        {
            _connectionsRootCandidate = candidate;
            _connectionsRootPending = true;
            return;
        }
        ReconcileConnectionsRootTo(candidate);
    }

    /// <summary>The outermost mutation boundary's reconciliation (Term 3).</summary>
    private void ReconcileConnectionsRoot()
    {
        if (!_connectionsRootPending)
        {
            return;
        }
        _connectionsRootPending = false;
        string? candidate = _connectionsRootCandidate;
        _connectionsRootCandidate = null;
        ReconcileConnectionsRootTo(candidate);
    }

    private void ReconcileConnectionsRootTo(string? candidate)
    {
        // A pending mount will load at its consume with the final leaf;
        // the root change itself loads only while active and mounted and
        // not during construction (the initial value is not a change).
        bool activeAndMounted = !_constructingConnections
            && !_mountPending
            && !_suppressConnectionsTriggers
            && ConnectionsLeafIsActive()
            && ConnectionsLeafIsMounted();
        Connections.NoteChanged(candidate, activeAndMounted);
    }

    /// <summary>Term 3(f): the lifecycle's file-change and scan-finished
    /// arms — the probe at EVERY level. Called from
    /// <see cref="NotifyGraphOfVaultChange"/>.</summary>
    private void ProbeConnections() => Connections.Probe();

    // --- The seams ----------------------------------------------------------------------

    /// <summary>B-9: reveal a row's note in the files sidebar from the LEAF —
    /// the sidebar's select-path seam directly, no graph tab addressed (the
    /// mac's <c>revealInFileTree</c>; codex post-implementation pass 4,
    /// IPB-19: the graph tab's addressed reveal returned early without a
    /// tab, so a standalone leaf's enabled Reveal did nothing).</summary>
    internal bool RevealConnectionsRowFromSurface(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (GraphRevealInSidebar is not { } reveal)
        {
            return false;
        }
        reveal(path);
        return true;
    }

    /// <summary>B-9: open a row's note in the ACTIVE pane; on success the
    /// shell's <c>OpenedFile</c> posts through the workspace's announcer.</summary>
    internal bool OpenConnectionsRowFromSurface(string path, WorkspaceOpenTarget target)
    {
        ArgumentNullException.ThrowIfNull(path);
        bool opened = false;
        RunWorkspaceMutation(() => opened = OpenPathCore(path, target));
        if (opened)
        {
            _announce(new A11yEvent.OpenedFile(System.IO.Path.GetFileName(path)));
        }
        return opened;
    }

    /// <summary>Teardown (B-1, A-1 as amended): the leaf into the bounded
    /// drain beside the graph document; the relay shuts down after both.</summary>
    private void ShutdownConnectionsLeaf(List<Task> drains)
    {
        Connections.Retire();
        drains.Add(Connections.WhenAllWorkDrained());
    }
}
