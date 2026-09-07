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

    private RelayCommand? _connectionsBackCommand;

    /// <summary>`slate.graph.connectionsBack` (W6-2 PR B2, B2-4): the
    /// palette's and the registrar's route to <see cref="ConnectionsBack"/>;
    /// with nothing to pop it is a no-op (the key owner, unlike the
    /// palette, reads the result to fall through).</summary>
    public System.Windows.Input.ICommand ConnectionsBackCommand =>
        _connectionsBackCommand ??= new RelayCommand(_ => _ = ConnectionsBack(), _ => true);

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
            _graphViewState,
            isActive: () => ConnectionsLeafIsActive(),
            verbosity: () => GraphVerbosity.Standard,
            lifecycleGeneration: () => LifecycleGeneration());
        leaf.OpenRowFromSurface = (path, target) => OpenConnectionsRowFromSurface(path, target);
        leaf.ShowConnectionsFromRow = path => ReRootConnectionsOn(path);
        // The view's key owner reads the result to fall through (B2-4).
        leaf.BackFromSurface = () => ConnectionsBack();
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

    // --- Rule D: the re-root funnel and Back (W6-2 PR B2, Terms 12, 13) --------------

    /// <summary>Term 12 — the ONE re-root funnel, every surface's entrance
    /// (B2-3): admission first; the same-root case repairs the key, reveals
    /// and activates WITHOUT the suppression (B1's own triggers apply,
    /// B2D-6) and proposes nothing; else the PIN MUTATION on the palette's
    /// Show shape — under the suppression, the pane revealed, the leaf
    /// activated, the mount consumed inert; then the leaf's <c>PinTo</c>
    /// (its push, pin, epoch, ONE audible load, the key, the line) and the
    /// focus request — followed, in a mutation of its own, by the ORDINARY
    /// open of the note with the editor's focus withheld (IGL-3): nothing
    /// is held across the open, whose refusal leaves the pin standing (the
    /// mac's outcome, B2D-7). Returns whether it pinned.</summary>
    internal bool ReRootConnectionsOn(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_workspaceDisposed || Connections.IsRetired)
        {
            return false;
        }
        if (string.Equals(Connections.Pin, path, StringComparison.Ordinal))
        {
            // Already pinned here (the mac's early return): the shared key
            // repaired, the reveal and the activation as B1's triggers say.
            Connections.RepairSharedKey(path);
            if (!IsRightPaneVisible)
            {
                IsRightPaneVisible = true;
            }
            if (!ConnectionsLeafIsActive())
            {
                ActiveLeaf = Leaves.First(option => string.Equals(option.Id, ConnectionsLeafId, StringComparison.Ordinal));
            }
            ConsumePendingMount();
            FocusBoundaryRequested?.Invoke(this, WorkspaceFocusBoundary.RightPane);
            return false;
        }
        bool pinned = false;
        RunWorkspaceMutation(() =>
        {
            _suppressConnectionsTriggers = true;
            try
            {
                if (!IsRightPaneVisible)
                {
                    IsRightPaneVisible = true;
                }
                if (!ConnectionsLeafIsActive())
                {
                    ActiveLeaf = Leaves.First(option => string.Equals(option.Id, ConnectionsLeafId, StringComparison.Ordinal));
                }
                ConsumePendingMount();
            }
            finally
            {
                _suppressConnectionsTriggers = false;
            }
            pinned = Connections.PinTo(path);
        });
        if (!pinned)
        {
            return false;
        }
        // The ordinary open, beside the pin: under the pin the note change
        // is recorded and loads nothing (Term 11).
        RunWorkspaceMutation(() => _ = OpenPathCore(path, WorkspaceOpenTarget.CurrentTab, requestEditorFocus: false));
        FocusBoundaryRequested?.Invoke(this, WorkspaceFocusBoundary.RightPane);
        return true;
    }

    /// <summary>Term 13 — Back: false (the chord falls through) unless live,
    /// PINNED and the stack non-empty; then the ORDINARY open of the top
    /// entry's note with the editor's focus withheld, before anything moves
    /// — a refusal pops nothing (B2-D5); RE-ADMISSION after the open, since
    /// its dialog's pump can tear the workspace or the leaf down (IGL-5);
    /// then the POP MUTATION on Show's shape and the leaf's <c>PopTo</c>
    /// with the path the open INSTALLED and the Markdown candidate the
    /// boundary reconciled — the pop proceeds only when the top entry, as
    /// retargeted, names the installed path (IGL-1, IGK-3) — and the focus
    /// request. Returns whether it popped.</summary>
    internal bool ConnectionsBack()
    {
        if (_workspaceDisposed || Connections.IsRetired || Connections.Pin is null || Connections.BackStack.Count == 0)
        {
            return false;
        }
        string target = Connections.BackStack[^1].Effective;
        bool opened = false;
        RunWorkspaceMutation(() => opened = OpenPathCore(target, WorkspaceOpenTarget.CurrentTab, requestEditorFocus: false));
        if (!opened || _workspaceDisposed || Connections.IsRetired)
        {
            return false;
        }
        string? installed = ActiveGroup.ActiveTab?.Path;
        string? candidate = ActiveGroup.ActiveTab is { IsMarkdown: true } tab ? tab.Path : null;
        if (installed is null)
        {
            return false;
        }
        bool popped = false;
        RunWorkspaceMutation(() =>
        {
            _suppressConnectionsTriggers = true;
            try
            {
                if (!IsRightPaneVisible)
                {
                    IsRightPaneVisible = true;
                }
                if (!ConnectionsLeafIsActive())
                {
                    ActiveLeaf = Leaves.First(option => string.Equals(option.Id, ConnectionsLeafId, StringComparison.Ordinal));
                }
                ConsumePendingMount();
            }
            finally
            {
                _suppressConnectionsTriggers = false;
            }
            popped = Connections.PopTo(installed, candidate);
        });
        if (popped)
        {
            FocusBoundaryRequested?.Invoke(this, WorkspaceFocusBoundary.RightPane);
        }
        return popped;
    }

    /// <summary>Terms 3(d) and 3(g), B-19 (iv): <c>SyncPanels()</c> hands
    /// the note in view — the markdown tab, as the note panels' funnel
    /// has it. Inside a workspace mutation the candidate is RECORDED and
    /// reconciled once at the outermost boundary (a split's sole-tab
    /// close is one A → B transition, IGH-4); outside one, immediately.</summary>
    private void SyncConnectionsRoot()
    {
        string? candidate = ActiveGroup.ActiveTab is { IsMarkdown: true } tab ? tab.Path : null;
        // Inside a mutation, AND through the boundary's own SyncPanels (W6-2
        // PR B2, IGL-2): the boundary reconciles ONCE with the LAST candidate
        // recorded — a candidate a nested SyncPanels recorded earlier inside a
        // pump (a rename hook's) is overwritten, never replayed over the note
        // the mutation installed.
        if (_persistenceBatchDepth > 0 || _connectionsBoundaryRecording)
        {
            _connectionsRootCandidate = candidate;
            _connectionsRootPending = true;
            return;
        }
        ReconcileConnectionsRootTo(candidate);
    }

    private bool _connectionsBoundaryRecording;

    /// <summary>The outermost boundary's panel sync, with the leaf's root
    /// RECORDED through it and reconciled once afterwards (IGL-2).</summary>
    private void SyncPanelsAtTheBoundary()
    {
        _connectionsBoundaryRecording = true;
        try
        {
            SyncPanels();
        }
        finally
        {
            _connectionsBoundaryRecording = false;
        }
        ReconcileConnectionsRoot();
    }

    /// <summary>Rule C, Term 3(d)'s classification, the one producer the
    /// funnel and the rename hook share (IGJ-10): the root change loads only
    /// while ACTIVE and MOUNTED, not during construction, not under a
    /// pending mount, not under a route's suppression.</summary>
    private bool ConnectionsActiveAndMounted() =>
        !_constructingConnections
        && !_mountPending
        && !_suppressConnectionsTriggers
        && ConnectionsLeafIsActive()
        && ConnectionsLeafIsMounted();

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
        Connections.NoteChanged(candidate, ConnectionsActiveAndMounted());
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
