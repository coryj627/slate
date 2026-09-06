// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.FileManagement;
using SlateWindows.Graph;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>Rule L's cause (contract A-1, Term 5): what an explicit path
/// owes when the graph becomes effective. Only Open and Reopen are
/// causes; every other path — a header click, a chord, a close, a pane
/// focus, the restore — reads Activation.</summary>
internal enum GraphActivationCause
{
    Activation,
    Open,
    Reopen,
}

/// <summary>
/// W6-2 PR A (#746): the workspace half of the graph — the ONE document
/// (contract A-1), rule L's follow method (Terms 1–7), the cause, the
/// retirement and the teardown drain, the vault-change probe's
/// notification (A-3) and the addressed actions (A-8, A-9). Under
/// <c>Graph/</c> so the directory censuses cover it, as the canvas keeps
/// its own workspace partial under <c>Canvas/</c>. The create funnel's
/// workspace completion — the ONE graph-family posting site outside the
/// walled directory (AD-8) — lives in the root partial
/// <c>WorkspaceViewModel.GraphCreate.cs</c> (IPA-7).
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    private GraphDocumentViewModel? _graphDocument;

    /// <summary>The workspace's ONE graph relay (A-10 as amended, W6-2 PR B
    /// BD-12): assigned once in the constructor by <see cref="NewGraphRelay"/>.</summary>
    private readonly GraphAnnouncer _graphRelay;

    /// <summary>The workspace's one relay, for the facts that queue a
    /// pending class on it and prove a High flush drops it (IPB-1).</summary>
    internal GraphAnnouncer GraphRelayForTests => _graphRelay;
    private GraphActivationCause _graphCause = GraphActivationCause.Activation;
    private bool _graphWasEffective;
    private WorkspaceTabViewModel? _graphEffectiveTab;
    private readonly GraphNoteCreationWorker _graphNoteCreation = new();

    /// <summary>The one document, or null while no graph tab exists.</summary>
    internal GraphDocumentViewModel? GraphDocument => _graphDocument;

    /// <summary>The sidebar as the graph's note creator (contract A-8;
    /// the lifecycle installs it, the canvas's precedent).</summary>
    public ISurfaceNoteCreator? GraphNoteCreator { get; set; }

    /// <summary>The sidebar's select-path seam (AD-11); the lifecycle installs it.</summary>
    public Action<string>? GraphRevealInSidebar { get; set; }

    /// <summary>The Create-note admission (0bD-8): null admits. Windows
    /// has no structural-mutation gate today.</summary>
    public Func<string?>? GraphCreateAdmissionReason { get; set; }

    /// <summary>Rule A / AD-8 (IPA-6, IPB-1): the vault lifecycle's
    /// generation — carried by every load token and every create
    /// completion, compared at dispatch. A CONSTRUCTION input (the restore
    /// can start the first load inside the constructor); a bare workspace
    /// reads a constant.</summary>
    public Func<int> LifecycleGeneration { get; }

    /// <summary>Test seam: how many funnel calls started a graph load.</summary>
    internal int GraphLoadsForTests { get; private set; }

    private RelayCommand? _openGraphCommand;

    /// <summary>`slate.graph.openTab` (contract A-12): the palette's and the
    /// registrar's route to <see cref="OpenGraph"/>.</summary>
    public System.Windows.Input.ICommand OpenGraphCommand =>
        _openGraphCommand ??= new RelayCommand(_ => OpenGraph(), _ => true);

    // --- The document (contract A-1) --------------------------------------

    /// <summary>The attach funnel's graph arm: seat the document on the
    /// tab — construct it on the first attach — and start NOTHING; rule
    /// L's follow method starts the load when the tab is effective.</summary>
    private void AttachGraphDocumentTo(WorkspaceTabViewModel tab)
    {
        _graphDocument ??= NewGraphDocument();
        tab.AttachGraphDocument(_graphDocument);
    }

    /// <summary>The workspace's ONE graph relay (A-10 as amended, W6-2 PR B
    /// BD-12): the one seed the announcer census counts, constructed in
    /// the constructor before either document over the rendered seam,
    /// shared by the graph document and the Connections leaf, shut down
    /// in the drain after both. The filter class's fire-time gate is the
    /// graph tab's EFFECTIVE predicate — the mac's `graphTabActive`
    /// re-checked at fire (0a-9).</summary>
    private GraphAnnouncer NewGraphRelay() => new GraphAnnouncer(_announceRendered);

    private GraphDocumentViewModel NewGraphDocument()
    {
        var document = new GraphDocumentViewModel(
            _session,
            _graphRelay,
            isEffectiveActive: () => GraphTabIsEffective(),
            verbosity: () => GraphVerbosity.Standard,
            lifecycleGeneration: () => LifecycleGeneration());
        document.OpenRowFromSurface = (row, target) => OpenGraphRowFromSurface(row.Path!, target);
        document.RevealRowFromSurface = path => RevealGraphRowFromSurface(path);
        document.CreateNoteFromSurface = path => CreateGraphNoteFromSurface(path);
        document.CreateAdmissionReason = () => GraphCreateAdmissionReason?.Invoke();
        return document;
    }

    /// <summary>The graph tab, wherever it lives (the singleton).</summary>
    private WorkspaceTabViewModel? GraphTab() =>
        Groups.SelectMany(group => group.Tabs).FirstOrDefault(tab => tab.IsGraph);

    /// <summary>The graph's ADDRESS (contracts A-8, A-9; deviation (viii),
    /// IPC-4, IPD-2): the singleton tab, the group that owns it now, and
    /// the document seated on it — captured together so a completion
    /// compares all three identities; the document pins the address to
    /// the graph that was open at invocation, not merely to objects that
    /// could be re-seated.</summary>
    private (WorkspaceGroupViewModel Group, WorkspaceTabViewModel Tab, GraphDocumentViewModel Document)? GraphAddress()
    {
        foreach (WorkspaceGroupViewModel group in Groups)
        {
            foreach (WorkspaceTabViewModel tab in group.Tabs)
            {
                if (tab.IsGraph && tab.Graph is { } document)
                {
                    return (group, tab, document);
                }
            }
        }
        return null;
    }

    /// <summary>Rule L, Term 2 — VISIBLE: the graph tab is its group's
    /// active tab, in any group (the mac's <c>anyGraphTabVisible</c>).</summary>
    internal bool GraphTabIsVisible() =>
        Groups.Any(group => group.ActiveTab is { IsGraph: true });

    /// <summary>Rule L, Term 2 — EFFECTIVE: visible and its group active
    /// (the mac's <c>graphTabActive</c>).</summary>
    internal bool GraphTabIsEffective() =>
        ActiveGroup.ActiveTab is { IsGraph: true };

    /// <summary>The explicit paths set the cause before their mutation
    /// (Term 5); the outermost-mutation hook clears what was not consumed.</summary>
    private void SetGraphCause(GraphActivationCause cause) => _graphCause = cause;

    internal GraphActivationCause GraphCauseForTests => _graphCause;

    /// <summary>
    /// Rule L, Terms 3–6: called from <see cref="SyncPanels"/> — the one
    /// funnel every path that changes the effective tab reaches — it
    /// classifies the graph's level, detects the transition INTO effective
    /// (by tab or by group), and applies the mac's guard with the cause's
    /// announcement policy. Nothing else in the shell starts a graph load.
    /// </summary>
    private void GraphFollowActiveTab()
    {
        WorkspaceTabViewModel? effective = ActiveGroup.ActiveTab is { IsGraph: true } tab ? tab : null;
        bool wasEffective = _graphWasEffective;
        WorkspaceTabViewModel? wasTab = _graphEffectiveTab;
        bool wasVisible = _graphWasVisible;
        bool isVisible = GraphTabIsVisible();
        _graphWasEffective = effective is not null;
        _graphEffectiveTab = effective;
        _graphWasVisible = isVisible;
        if (effective is null || _graphDocument is null || _graphDocument.IsRetired)
        {
            return;
        }
        GraphActivationCause cause = _graphCause;
        bool became = !wasEffective || !ReferenceEquals(wasTab, effective);
        if (!became && cause == GraphActivationCause.Activation)
        {
            return;
        }
        _graphCause = GraphActivationCause.Activation;
        bool ready = _graphDocument.Publication.HoldsSnapshot;
        bool load;
        if (cause != GraphActivationCause.Activation)
        {
            // An explicit Open or Reopen: the mac's guard runs before the
            // group changes, so it loads whenever the level before the
            // mutation was not EFFECTIVE, and otherwise only without a
            // held snapshot (Term 4).
            load = !wasEffective || !ready;
            _graphDocument.AnnounceStatus(new GraphStatusNote.Opened());
        }
        else if (wasVisible)
        {
            // BY GROUP: the graph was already shown in its pane — no load
            // when READY (the mac's pane focus).
            load = !ready;
        }
        else
        {
            // BY TAB: always loads.
            load = true;
        }
        if (load)
        {
            GraphLoadsForTests++;
            _ = _graphDocument.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Summary);
        }
    }

    private bool _graphWasVisible;

    /// <summary>The outermost-mutation boundary (Term 5): a cause the
    /// graph's transition did not consume is cleared here.</summary>
    private void ClearGraphCauseAtMutationBoundary() => _graphCause = GraphActivationCause.Activation;

    /// <summary>The tab-set boundary (contract A-1): the last graph tab
    /// closed retires the document — seq advanced, shutdown, the announcer
    /// drained, the view state reset — and tracks its drain.</summary>
    private void ReleaseGraphDocumentIfUnreferenced()
    {
        if (_graphDocument is null || GraphTab() is not null)
        {
            return;
        }
        GraphDocumentViewModel retired = _graphDocument;
        _graphDocument = null;
        _graphWasEffective = false;
        _graphEffectiveTab = null;
        _graphWasVisible = false;
        retired.Retire();
        TrackRetiredBasesWork(retired.WhenAllWorkDrained());
    }

    /// <summary>Teardown (contract A-1): the live document into the
    /// bounded pre-session drain, beside the canvas documents.</summary>
    private void ShutdownGraphDocument(List<Task> drains)
    {
        if (_graphDocument is { } document)
        {
            _graphDocument = null;
            document.Retire();
            drains.Add(document.WhenAllWorkDrained());
        }
        _graphNoteCreation.Shutdown();
        drains.Add(_graphNoteCreation.WhenAllWorkDrained());
        // W6-2 PR B (B-1): the leaf beside the graph document; then the
        // workspace's relay, after both (A-10 as amended).
        ShutdownConnectionsLeaf(drains);
        _graphRelay.Shutdown();
    }

    // --- The probe (contract A-3) -----------------------------------------

    /// <summary>The lifecycle's file-change and scan-finished arms land
    /// here: probe only while the graph tab is VISIBLE.</summary>
    internal void NotifyGraphOfVaultChange()
    {
        if (_graphDocument is { IsRetired: false } document && GraphTabIsVisible())
        {
            document.Probe();
        }
        // W6-2 PR B (rule C, Term 3(f)): the leaf's probe at EVERY level.
        ProbeConnections();
    }

    // --- The addressed open (contract A-9) -------------------------------

    /// <summary>Open a row's note from the graph's own pane: the graph tab
    /// and its group are made active first (the mac's
    /// <c>focusOwningGroup</c>), the open runs, and on success the shell's
    /// <c>OpenedFile</c> posts through the workspace's announcer.</summary>
    internal bool OpenGraphRowFromSurface(string path, WorkspaceOpenTarget target)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!FocusGraphAddress())
        {
            return false;
        }
        bool opened = false;
        RunWorkspaceMutation(() => opened = OpenPathCore(path, target));
        if (opened)
        {
            _announce(new A11yEvent.OpenedFile(System.IO.Path.GetFileName(path)));
        }
        return opened;
    }

    /// <summary>Make the graph's group and tab active, synchronously —
    /// false when no graph tab exists (a stale address).</summary>
    private bool FocusGraphAddress()
    {
        WorkspaceTabViewModel? graph = GraphTab();
        if (graph is null)
        {
            return false;
        }
        WorkspaceGroupViewModel owner = Groups.First(group => group.Tabs.Contains(graph));
        if (!ReferenceEquals(ActiveGroup, owner) || !ReferenceEquals(owner.ActiveTab, graph))
        {
            ActiveGroup = owner;
            owner.ActiveTab = graph;
        }
        return true;
    }

    // --- The addressed reveal (contract A-9; IPA-4) ------------------------

    /// <summary>Reveal a row's note in the files sidebar: like every row
    /// action, addressed at invocation — the graph's group and tab are
    /// made active first (the round-4 ledger's IGA-41: EVERY action
    /// captures its tab and group and activates them), then the sidebar's
    /// select-path seam runs.</summary>
    internal bool RevealGraphRowFromSurface(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!FocusGraphAddress())
        {
            return false;
        }
        GraphRevealInSidebar?.Invoke(path);
        return true;
    }

    /// <summary>Test seam: the create worker's drain.</summary>
    internal Task DrainGraphNoteCreationForTests() => _graphNoteCreation.WhenAllWorkDrained();
}

/// <summary>The workspace-owned worker the create funnel runs on
/// (contract A-8, AD-8): its life is the workspace's, not the graph
/// document's, so a create that lands after the graph retired still
/// completes.</summary>
internal sealed class GraphNoteCreationWorker : PanelWorkScheduler
{
    public GraphNoteCreationWorker()
        : this(
            SynchronizationContext.Current as System.Windows.Threading.DispatcherSynchronizationContext,
            System.Windows.Threading.Dispatcher.CurrentDispatcher)
    {
    }

    /// <summary>The owner DISPATCHER is named beside its context (codex
    /// post-implementation pass 4, IPB-20): only the named-dispatcher post
    /// observes an aborted operation and withdraws its promise, so a
    /// completion posted after the dispatcher's shutdown began cannot leave
    /// the create's drain pending.</summary>
    private GraphNoteCreationWorker(
        System.Windows.Threading.DispatcherSynchronizationContext? current,
        System.Windows.Threading.Dispatcher dispatcher)
        : base(
            synchronousForTests: false,
            current ?? new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher),
            dispatcher)
    {
    }

    public void Run(Func<NoteCreateResult> create, Action<NoteCreateResult> completed) =>
        StartWorkAlwaysAsync(create, completed);
}
