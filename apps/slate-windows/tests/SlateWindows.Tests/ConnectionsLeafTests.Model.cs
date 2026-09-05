// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR B (#746), contract B-10: THE MODEL. An executable derivation of
/// rule C's Terms 2–9 — the shell's own lines for a route, then the
/// projection of the ONE trigger Term 3 classifies for the route's final
/// state, then Term 9's entry line, then every completion's line iff
/// SPEAKING at apply — over B-10's worked routes crossed with the pane,
/// leaf, root and presentation states and the in-flight flag. Every
/// reachable cell arranges a fresh workspace in that state, drives it
/// through the route, and asserts the recorded timeline, the loads issued
/// and the final root and staleness equal the derivation; the cells that
/// are not states of the system are NAMED, and the totals, the exclusions,
/// the driven count and the route inventory are pinned exactly (codex
/// post-implementation pass 1, IPB-3). A route or a state the model cannot
/// derive fails the fact; a worked row is never the contract. The rows the
/// model does not drive are pinned elsewhere and named in TGB-8: the
/// directional-focus routes (the journey), the table's sort and count
/// against the one relay (GraphAnnouncerTests), the graph tab's own lines
/// (PR A), and the vault's replacement (the lifecycle fact and Launch).
/// </summary>
public sealed partial class ConnectionsLeafTests
{
    private enum Pane { Visible, Collapsed }

    private enum LeafState { Connections, Other }

    private enum RootState { Note, None }

    private enum Presentation { Current, Stale }

    private enum Route
    {
        SwitchToLeaf,
        SwitchAway,
        Show,
        PaneToggle,
        RootChange,
        DepthChange,
        DeeperAtBound,
        RootToNone,
        TasksReview,
        HistoryCommand,
        ShowBacklinks,
        FocusEnter,
        TabActivateOther,
        TabActivateDuplicate,
        TabCloseSuccessor,
        TabCloseDuplicate,
        Split,
        GroupCloseSameRoot,
        GroupCloseOtherRoot,
        GhostCreate,
        GhostCreateFails,
        RenameRoot,
        DeleteRoot,
        Launch,
        Shutdown,
    }

    private sealed record Cell(Pane Pane, LeafState Leaf, RootState Root, Presentation Presentation, bool InFlight, Route Route)
    {
        public override string ToString() =>
            $"{Route} from [{Pane}, {Leaf}, {Root}, {Presentation}{(InFlight ? ", in flight" : "")}]";
    }

    /// <summary>The derivation: the timeline (shell lines then relay lines,
    /// a summary written as a placeholder resolved after the settle — the
    /// final tree's, or the pre-route tree's for a completion that speaks
    /// before a silent reload replaces it), the loads the route issues in
    /// all (counted after the settle: a probe's load is asynchronous), and
    /// the final root and staleness (null: not asserted).</summary>
    private sealed record Derivation(string[] Shell, string[] Relay, int Loads, bool? RootIsNull, bool? Stale, string? Root = null);

    private const string SummaryPlaceholder = "<summary>";
    private const string SummaryBeforePlaceholder = "<summary-before>";
    private const string RenamedRoot = "hub-renamed.md";
    private const string InjectedFailure = "injected failure";

    /// <summary>The pinned cardinalities (IPB-3): five binary dimensions
    /// crossed with the route inventory; the cells the model names as not
    /// states of the system; the cells driven.</summary>
    private const int PinnedRoutes = 25;
    private const int PinnedCells = 32 * PinnedRoutes;
    private const int PinnedUnreachable = 498;
    private const int PinnedDriven = 302;

    private static readonly Route[] TabRoutes =
    [
        Route.TabActivateOther,
        Route.TabActivateDuplicate,
        Route.TabCloseSuccessor,
        Route.TabCloseDuplicate,
        Route.Split,
        Route.GroupCloseSameRoot,
        Route.GroupCloseOtherRoot,
    ];

    /// <summary>Why a cell is not a state of the system — the model must
    /// name the reason, never silently skip.</summary>
    private static string? Unreachable(Cell cell)
    {
        if (cell.Root == RootState.None && (cell.Presentation == Presentation.Stale || cell.InFlight))
        {
            return "no root: the presentation is NoNote and nothing is in flight";
        }
        if (cell.InFlight && cell.Presentation == Presentation.Stale)
        {
            return "a root transition clears the in-flight flag (Term 7), so a stale presentation never has a load in flight";
        }
        if (cell.Presentation == Presentation.Stale && cell.Leaf == LeafState.Connections && cell.Pane == Pane.Visible)
        {
            return "an ACTIVE and MOUNTED leaf with a root has loaded (Terms 3(a), 3(b), 3(d)): STALE is only ever unmounted or inactive at rest";
        }
        if (cell.Route == Route.FocusEnter && (cell.Leaf != LeafState.Connections || cell.Pane != Pane.Visible))
        {
            return "focus enters the anchor only while the leaf is shown";
        }
        if (TabRoutes.Contains(cell.Route) && cell.Root == RootState.None)
        {
            return "the route needs a note tab beside the one in view; with no note in view there is none";
        }
        if (cell.Route is Route.GhostCreate or Route.GhostCreateFails
            && (cell.Root == RootState.None || cell.Presentation == Presentation.Stale || cell.InFlight))
        {
            return "a ghost row is activated from a RENDERED tree; a leaf with no root, a stale one or a loading one shows none";
        }
        if (cell.Route == Route.Launch && (cell.Presentation == Presentation.Stale || cell.InFlight))
        {
            return "a launch restores a root, never a presentation or a load in flight";
        }
        if (cell.Route == Route.Launch && cell.Pane == Pane.Collapsed)
        {
            return "the pane's visibility is not persisted: a launch comes up MOUNTED";
        }
        return null;
    }

    private static Derivation Derive(Cell cell)
    {
        bool mounted = cell.Pane == Pane.Visible;
        bool active = cell.Leaf == LeafState.Connections;
        bool hasRoot = cell.Root == RootState.Note;
        bool stale = cell.Presentation == Presentation.Stale;
        string[] summary = [SummaryPlaceholder];
        // Term 2 and B-D3: SPEAKING is ACTIVE at apply — the leaf is the
        // active leaf when the body runs — and MOUNTED is not consulted; a
        // hide alone leaves an in-flight completion audible, a switch away
        // silences it (IGH-14). An in-flight completion that no route
        // supersedes or makes foreign applies when the workspace settles.
        string[] pendingCompletion = cell.InFlight && active ? summary : [];
        bool? restStale = hasRoot ? stale : null;
        switch (cell.Route)
        {
            case Route.SwitchToLeaf:
                if (active)
                {
                    // A same-leaf switch is the setter's no-op.
                    return new([], pendingCompletion, 0, !hasRoot, restStale);
                }
                {
                    // Term 3(b): a mounted switch loads only when STALE; a load in
                    // flight is a current request. The in-flight completion speaks
                    // iff the user switched back before it applied (IGH-14).
                    int loads = mounted && hasRoot && stale && !cell.InFlight ? 1 : 0;
                    bool speaks = loads == 1 || cell.InFlight;
                    return new(["LeafPanelShown"], speaks ? summary : [], loads, !hasRoot, hasRoot ? stale && loads == 0 && !cell.InFlight : null);
                }
            case Route.SwitchAway:
                if (!active)
                {
                    return new([], [], 0, !hasRoot, restStale);
                }
                // The completion, when one is in flight, applies silently.
                return new(["LeafPanelShown"], [], 0, !hasRoot, restStale);
            case Route.Show:
                {
                    // Term 3(c): one audible load whether current or not; the
                    // shell's pane line when collapsed (B-D9), the setter's leaf
                    // line when the leaf changes, the graph family's panel line
                    // when it does not (B-D5); a load in flight is superseded.
                    var shell = new List<string>();
                    if (!mounted)
                    {
                        shell.Add("RightPaneShown");
                    }
                    if (!active)
                    {
                        shell.Add("LeafPanelShown");
                    }
                    var relay = new List<string>();
                    if (active)
                    {
                        relay.Add(PanelLine());
                    }
                    if (hasRoot)
                    {
                        relay.Add(SummaryPlaceholder);
                    }
                    return new([.. shell], [.. relay], hasRoot ? 1 : 0, !hasRoot, hasRoot ? false : null);
                }
            case Route.PaneToggle:
                if (mounted)
                {
                    // A hide: the view state clears, the document keeps its
                    // presentation; a completion in flight still speaks when the
                    // leaf is the active one (B-D3), silently otherwise.
                    return new(["RightPaneHidden"], pendingCompletion, 0, !hasRoot, restStale);
                }
                {
                    // Term 3(a): a MOUNT with the leaf active loads, current or
                    // not (superseding a load in flight); with another leaf
                    // active nothing loads and a completion in flight applies
                    // silently.
                    int loads = active && hasRoot ? 1 : 0;
                    return new(["RightPaneShown"], loads == 1 ? summary : [], loads, !hasRoot, hasRoot ? stale && loads == 0 && !cell.InFlight : null);
                }
            case Route.RootChange:
                {
                    // Term 3(d): a load only while ACTIVE and MOUNTED, else STALE;
                    // an in-flight result for the old root is foreign. The
                    // shell's `TabFocused` when the open creates the first tab
                    // (an in-place open into an existing tab posts none).
                    int loads = active && mounted ? 1 : 0;
                    return new(hasRoot ? [] : ["TabFocused"], loads == 1 ? summary : [], loads, false, loads == 0, Two);
                }
            case Route.DepthChange:
                {
                    // Term 3(e): a load with a root (superseding a load in
                    // flight); spoken iff SPEAKING at apply, installed either way.
                    int loads = hasRoot ? 1 : 0;
                    bool speaks = loads == 1 && active;
                    return new([], speaks ? summary : [], loads, !hasRoot, hasRoot ? false : null);
                }
            case Route.DeeperAtBound:
                // Deeper at the maximum is core's clamp returning the same
                // depth: no write, no load (B-14, B-19 v).
                return new([], pendingCompletion, 0, !hasRoot, restStale);
            case Route.RootToNone:
                if (!hasRoot)
                {
                    return new([], [], 0, true, null);
                }
                // Term 3(g): NoNote synchronously, the in-flight result dropped.
                return new(["TabClosed"], [], 0, true, null);
            case Route.TasksReview:
            case Route.HistoryCommand:
                {
                    // The reveal-then-switch commands: MOUNT is evaluated at the
                    // route's end with the review or History active — no
                    // Connections load; the shell's pane line when collapsed, the
                    // setter's leaf line, that command's own line.
                    var shell = new List<string>();
                    if (!mounted)
                    {
                        shell.Add("RightPaneShown");
                    }
                    shell.Add("LeafPanelShown");
                    shell.Add(cell.Route == Route.TasksReview ? "TasksReviewShown" : "HistoryPanelShown");
                    return new([.. shell], [], 0, !hasRoot, hasRoot ? stale && !cell.InFlight : null);
                }
            case Route.ShowBacklinks:
                {
                    // Bases' Show backlinks (IPB-2): ONE outer mutation — the open,
                    // the switch to Backlinks, the reveal, the consume — so the
                    // boundary reconciles the new root against the FINAL leaf:
                    // STALE, no load; an in-flight result is foreign. The Bases
                    // line is the document's and is not driven here.
                    var shell = new List<string>();
                    if (!hasRoot)
                    {
                        shell.Add("TabFocused");
                    }
                    shell.Add("LeafPanelShown");
                    if (!mounted)
                    {
                        shell.Add("RightPaneShown");
                    }
                    return new([.. shell], [], 0, false, true, Two);
                }
            case Route.FocusEnter:
                {
                    // Term 9: LoadingConnections while STALE or Loading, nothing
                    // for a ready tree with rows or for NoNote; then the in-flight
                    // completion speaks (the leaf is shown).
                    var relay = new List<string>();
                    if (hasRoot && (stale || cell.InFlight))
                    {
                        relay.Add(LoadingLine());
                    }
                    if (cell.InFlight)
                    {
                        relay.Add(SummaryPlaceholder);
                    }
                    return new([], [.. relay], 0, !hasRoot, hasRoot ? false : null);
                }
            case Route.TabActivateOther:
                {
                    // An existing tab activated: the shell's `TabFocused`; a root
                    // change — Term 3(d).
                    int loads = active && mounted ? 1 : 0;
                    return new(["TabFocused"], loads == 1 ? summary : [], loads, false, loads == 0, Two);
                }
            case Route.TabActivateDuplicate:
                // The same note's other tab: `TabFocused`; no root change.
                return new(["TabFocused"], pendingCompletion, 0, false, restStale, Hub);
            case Route.TabCloseSuccessor:
                {
                    // The close's successor is another note: the successor's
                    // `TabFocused` (the group's setter) then `TabClosed`; a root
                    // change — Term 3(d).
                    int loads = active && mounted ? 1 : 0;
                    return new(["TabFocused", "TabClosed"], loads == 1 ? summary : [], loads, false, loads == 0, Two);
                }
            case Route.TabCloseDuplicate:
                // The successor is the same note: its `TabFocused`, `TabClosed`;
                // no root change.
                return new(["TabFocused", "TabClosed"], pendingCompletion, 0, false, restStale, Hub);
            case Route.Split:
                // The duplicate's `TabFocused`; the root unchanged, no load.
                return new(["TabFocused"], pendingCompletion, 0, false, restStale, Hub);
            case Route.GroupCloseSameRoot:
                // `EditorPaneFocused`; the surviving group shows the same note.
                return new(["EditorPaneFocused"], pendingCompletion, 0, false, restStale, Hub);
            case Route.GroupCloseOtherRoot:
                {
                    // `EditorPaneFocused`; the surviving group shows another note:
                    // a root change — Term 3(d).
                    int loads = active && mounted ? 1 : 0;
                    return new(["EditorPaneFocused"], loads == 1 ? summary : [], loads, false, loads == 0, Two);
                }
            case Route.GhostCreate:
                {
                    // B-11: the open is silent, then ONE NoteCreated (the shell's
                    // `A11yEvent.Graph`, A-8's direct post), then the root change's
                    // summary iff the load was issued (ACTIVE and MOUNTED) and
                    // SPEAKING; inactive or unmounted, the root moves STALE.
                    int loads = active && mounted ? 1 : 0;
                    return new(["Graph"], loads == 1 ? summary : [], loads, false, loads == 0);
                }
            case Route.GhostCreateFails:
                // The failure is the relay's HIGH line (IPB-1); nothing else.
                return new([], [Render(new GraphA11yEvent.GraphBlocked(new GraphBlockedReason.NoteCreateFailed(InjectedFailure)))], 0, false, false, Hub);
            case Route.RenameRoot:
                if (!hasRoot)
                {
                    return new([], [], 0, true, null);
                }
                {
                    // The workspace's retarget re-derives the panels: a root change
                    // to the new path — ONE audible load while ACTIVE and MOUNTED,
                    // else STALE; then the probe (Term 6): a load in flight is
                    // marked only, a stale leaf reloads silently once. One load
                    // either way; an in-flight result for the old root is foreign.
                    // The shell's line is the outline's count, re-derived with
                    // the panels (its own facts pin it).
                    bool audible = active && mounted;
                    return new(["OutlineCount"], audible ? summary : [], 1, false, false, RenamedRoot);
                }
            case Route.DeleteRoot:
                if (!hasRoot)
                {
                    return new([], [], 0, true, null);
                }
                {
                    // The shell's missing-file line; the tab and the root are KEPT;
                    // the probe (Term 6): the held tree is older — a silent load —
                    // or, with a load in flight, the mark, whose completion speaks
                    // the tree it fetched before the delete (iff active) and then
                    // reloads silently once. One load either way, nothing else
                    // spoken.
                    return new(["HostComposed"], cell.InFlight && active ? [SummaryBeforePlaceholder] : [], 1, false, false, Hub);
                }
            case Route.Launch:
                {
                    // The launch restores the leaf and the note (the pane comes
                    // up visible: its visibility is not persisted): the seeded
                    // mount's ONE load with the leaf active and a note restored,
                    // nothing otherwise; the shell's line is the restored note's
                    // outline count.
                    int loads = active && hasRoot ? 1 : 0;
                    return new(hasRoot ? ["OutlineCount"] : [], loads == 1 ? summary : [], loads, !hasRoot, hasRoot ? loads == 0 : null);
                }
            case Route.Shutdown:
                // The leaf retires into the drain, the relay after it: nothing.
                return new([], [], 0, null, null);
            default:
                throw new InvalidOperationException($"the model derives no route {cell.Route}");
        }
    }

    private static WorkspaceTabViewModel TabFor(Host host, string path, bool inactiveOne = false)
    {
        WorkspaceTabViewModel[] tabs = [.. host.Workspace.ActiveGroup.Tabs.Where(tab => string.Equals(tab.Path, path, StringComparison.Ordinal))];
        Assert.True(tabs.Length > 0, $"no tab shows {path}");
        return inactiveOne
            ? tabs.First(tab => !ReferenceEquals(tab, host.Workspace.ActiveGroup.ActiveTab))
            : tabs[0];
    }

    /// <summary>Arrange the workspace in the cell's state, from a fresh
    /// vault: the leaf activated and the note opened while ACTIVE and
    /// MOUNTED for a current presentation (then moved away or hidden), the
    /// note opened while inactive for a stale one, the last load left
    /// pending for the in-flight flag; the route's own props before or
    /// after — a second tab, a split, the depth at its bound, a creator.</summary>
    private static void Arrange(Host host, Cell cell)
    {
        host.Workspace.ActiveLeaf = Host.OutlineLeaf;
        bool hubInNewTab = false;
        switch (cell.Route)
        {
            case Route.TabActivateOther:
            case Route.TabCloseSuccessor:
                host.OpenNote(Two);
                hubInNewTab = true;
                break;
            case Route.GroupCloseOtherRoot:
                host.OpenNote(Two);
                host.Workspace.SplitRightCommand.Execute(null);
                break;
            case Route.DeeperAtBound:
                host.Workspace.ConnectionsDeeperCommand.Execute(null);
                host.Workspace.ConnectionsDeeperCommand.Execute(null);
                Assert.Equal(3u, host.Leaf.Depth);
                break;
            case Route.GhostCreate:
                host.Workspace.GraphNoteCreator = new RecordingCreator(host.Session);
                break;
            case Route.GhostCreateFails:
                host.Workspace.GraphNoteCreator = new FailingCreator(InjectedFailure);
                break;
        }
        host.Settle();

        void OpenHub()
        {
            if (hubInNewTab)
            {
                host.Workspace.OpenPath(Hub, WorkspaceOpenTarget.NewTab);
            }
            else
            {
                host.OpenNote(Hub);
            }
        }

        void AfterTheNote()
        {
            switch (cell.Route)
            {
                case Route.TabActivateDuplicate:
                case Route.TabCloseDuplicate:
                    // A second open of an open path is a registry hit that
                    // activates the existing tab; the duplicate is the command's.
                    host.Workspace.DuplicateTabCommand.Execute(null);
                    Assert.Equal(2, host.Workspace.ActiveGroup.Tabs.Count(tab => string.Equals(tab.Path, Hub, StringComparison.Ordinal)));
                    break;
                case Route.GroupCloseSameRoot:
                    host.Workspace.SplitRightCommand.Execute(null);
                    break;
            }
        }

        if (cell.Root == RootState.None)
        {
            if (cell.Leaf == LeafState.Connections)
            {
                host.ActivateLeaf();
            }
            if (cell.Pane == Pane.Collapsed)
            {
                host.Workspace.IsRightPaneVisible = false;
            }
            host.Settle();
            return;
        }
        if (cell.Presentation == Presentation.Stale)
        {
            OpenHub();
            AfterTheNote();
            host.Settle();
            if (cell.Pane == Pane.Collapsed)
            {
                host.Workspace.IsRightPaneVisible = false;
            }
            if (cell.Leaf == LeafState.Connections)
            {
                host.ActivateLeaf();
            }
            host.Settle();
            Assert.True(host.Leaf.IsStale, $"{cell}: the arrangement did not leave the presentation stale");
            return;
        }
        host.ActivateLeaf();
        OpenHub();
        if (!cell.InFlight)
        {
            host.Settle();
        }
        AfterTheNote();
        if (cell.Leaf == LeafState.Other)
        {
            host.Workspace.ActiveLeaf = Host.OutlineLeaf;
        }
        if (cell.Pane == Pane.Collapsed)
        {
            host.Workspace.IsRightPaneVisible = false;
        }
        if (cell.InFlight)
        {
            Assert.True(host.Leaf.InFlight, $"{cell}: the arrangement left nothing in flight");
        }
        else
        {
            host.Settle();
            Assert.True(host.Leaf.IsCurrent, $"{cell}: the arrangement did not leave the presentation current");
        }
    }

    private static void Drive(Host host, Cell cell)
    {
        switch (cell.Route)
        {
            case Route.SwitchToLeaf:
                host.ActivateLeaf();
                break;
            case Route.SwitchAway:
                host.Workspace.ActiveLeaf = Host.OutlineLeaf;
                break;
            case Route.Show:
                host.Workspace.ShowConnections();
                break;
            case Route.PaneToggle:
                host.Workspace.ToggleRightPaneCommand.Execute(null);
                break;
            case Route.RootChange:
                host.OpenNote(Two);
                break;
            case Route.DepthChange:
            case Route.DeeperAtBound:
                host.Workspace.ConnectionsDeeperCommand.Execute(null);
                break;
            case Route.RootToNone:
                if (host.Workspace.CloseActiveTabCommand.CanExecute(null))
                {
                    host.Workspace.CloseActiveTabCommand.Execute(null);
                }
                break;
            case Route.TasksReview:
                host.Workspace.OpenTasksReview();
                break;
            case Route.HistoryCommand:
                host.Workspace.ShowHistoryPanel();
                break;
            case Route.ShowBacklinks:
                host.Workspace.ShowBacklinksFor(Two, announce: null);
                break;
            case Route.FocusEnter:
                host.Leaf.FocusEntered();
                break;
            case Route.TabActivateOther:
                host.Workspace.ActiveGroup.ActiveTab = TabFor(host, Two);
                break;
            case Route.TabActivateDuplicate:
                host.Workspace.ActiveGroup.ActiveTab = TabFor(host, Hub, inactiveOne: true);
                break;
            case Route.TabCloseSuccessor:
            case Route.TabCloseDuplicate:
                host.Workspace.CloseActiveTabCommand.Execute(null);
                break;
            case Route.Split:
                host.Workspace.SplitRightCommand.Execute(null);
                break;
            case Route.GroupCloseSameRoot:
            case Route.GroupCloseOtherRoot:
                host.Workspace.ClosePaneCommand.Execute(null);
                break;
            case Route.GhostCreate:
            case Route.GhostCreateFails:
                host.Leaf.Activate(FirstGhost(host.Leaf).Ghost, newTab: false);
                DrainCreate(host);
                break;
            case Route.RenameRoot:
                host.Session.RenameFile(Hub, RenamedRoot);
                host.Workspace.RetargetPath(Hub, RenamedRoot);
                host.Workspace.NotifyGraphOfVaultChange();
                break;
            case Route.DeleteRoot:
                // The delete as the lifecycle sees it — the file gone and the
                // vault rescanned (core's own delete trashes through COM and
                // wants a thread of its own), then the lifecycle's two arms.
                File.Delete(Path.Combine(host.Root, Hub));
                using (var cancel = new CancelToken())
                {
                    host.Session.ScanInitial(cancel);
                }
                host.Workspace.InvalidatePath(Hub);
                host.Workspace.NotifyGraphOfVaultChange();
                break;
            case Route.Shutdown:
                host.Workspace.Dispose();
                break;
            case Route.Launch:
                throw new InvalidOperationException("the launch is driven by the host's construction");
            default:
                throw new InvalidOperationException($"no driver for {cell.Route}");
        }
    }

    private static IEnumerable<Cell> Cells()
    {
        foreach (Route route in Enum.GetValues<Route>())
        {
            foreach (Pane pane in Enum.GetValues<Pane>())
            {
                foreach (LeafState leaf in Enum.GetValues<LeafState>())
                {
                    foreach (RootState root in Enum.GetValues<RootState>())
                    {
                        foreach (Presentation presentation in Enum.GetValues<Presentation>())
                        {
                            foreach (bool inFlight in (bool[])[false, true])
                            {
                                yield return new Cell(pane, leaf, root, presentation, inFlight, route);
                            }
                        }
                    }
                }
            }
        }
    }

    private static string SummaryOf(Host host, string root, uint depth) =>
        Render(new GraphA11yEvent.GraphNeighborhoodSummary(
            host.Session.GraphConnectionsTree(root, depth, SlateUniffiMethods.GraphConnectionsFilter()).SummaryCounts));

    [Fact]
    public void TheModelOfTermsTwoToNineDerivesEveryRoutesTimelineAcrossEveryState()
    {
        Assert.Equal(PinnedRoutes, Enum.GetValues<Route>().Length);
        Cell[] cells = [.. Cells()];
        Assert.Equal(PinnedCells, cells.Length);
        var failures = new List<string>();
        var unreachable = new List<string>();
        int driven = 0;
        PumpedDispatcher.Run(() =>
        {
            foreach (Cell cell in cells)
            {
                if (Unreachable(cell) is { } reason)
                {
                    unreachable.Add($"{cell}: {reason}");
                    continue;
                }
                Derivation expected = Derive(cell);
                using GraphVault vault = GraphVault.Copy($"model-{driven}");
                Host host;
                int before;
                string? summaryBefore = null;
                if (cell.Route == Route.Launch)
                {
                    using (var first = new Host(vault.Root))
                    {
                        Arrange(first, cell);
                        first.Settle();
                    }
                    // The launch IS the construction: its timeline from the start.
                    host = new Host(vault.Root);
                    before = 0;
                }
                else
                {
                    host = new Host(vault.Root);
                    Arrange(host, cell);
                    if (host.Leaf.Root is { } rootBefore)
                    {
                        summaryBefore = SummaryOf(host, rootBefore, host.Leaf.Depth);
                    }
                    host.Clear();
                    before = host.Loads;
                    Drive(host, cell);
                }
                driven++;
                if (cell.Route != Route.Shutdown)
                {
                    host.Settle();
                }
                int loads = host.Loads - before;

                string[] timeline =
                [
                    .. expected.Shell,
                    .. expected.Relay.Select(line => line switch
                    {
                        SummaryPlaceholder => Summary(host.Leaf),
                        SummaryBeforePlaceholder => summaryBefore ?? "<no tree before the route>",
                        _ => line,
                    }),
                ];
                var mismatch = new List<string>();
                if (loads != expected.Loads)
                {
                    mismatch.Add($"loads {loads}, derived {expected.Loads}");
                }
                if (!timeline.SequenceEqual(host.Timeline))
                {
                    mismatch.Add($"timeline [{string.Join(" | ", host.Timeline)}], derived [{string.Join(" | ", timeline)}]");
                }
                if (expected.RootIsNull is { } rootIsNull && (host.Leaf.Root is null) != rootIsNull)
                {
                    mismatch.Add($"root {(host.Leaf.Root is null ? "none" : host.Leaf.Root)}, derived {(rootIsNull ? "none" : "a note")}");
                }
                if (expected.Root is { } root && !string.Equals(host.Leaf.Root, root, StringComparison.Ordinal))
                {
                    mismatch.Add($"root {host.Leaf.Root ?? "none"}, derived {root}");
                }
                if (expected.Stale is { } stale && host.Leaf.Root is not null && host.Leaf.IsStale != stale)
                {
                    mismatch.Add($"stale {host.Leaf.IsStale}, derived {stale}");
                }
                if (mismatch.Count > 0)
                {
                    failures.Add($"{cell}: {string.Join("; ", mismatch)}");
                }
                if (cell.Route == Route.Shutdown)
                {
                    host.Session.Dispose();
                }
                else
                {
                    host.Dispose();
                }
            }
        });
        Assert.True(failures.Count == 0, $"{failures.Count} of {driven} cells diverge from the model (unreachable {unreachable.Count}):\n{string.Join("\n", failures)}");
        Assert.True(
            unreachable.Count == PinnedUnreachable && driven == PinnedDriven,
            $"the model named {unreachable.Count} cells as not states of the system and drove {driven}; pinned {PinnedUnreachable} and {PinnedDriven}:\n{string.Join("\n", unreachable)}");
    }
}
