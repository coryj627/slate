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
/// SPEAKING at apply — over B-10's worked routes crossed with the pane, the
/// leaf, the root (a note; none; the graph tab in view with a note beside
/// it), the presentation (Ready and current; stale; Error) and the
/// in-flight flag. Every reachable cell arranges a fresh workspace in that
/// state, drives it through the route, and asserts the recorded timeline,
/// the loads issued, the final root, the depth and the staleness equal the
/// derivation. A load left in flight is PARKED inside its fetch, after its
/// crossings, until the route's synchronous part has run, so "in flight
/// during the route" is a state and not a race; every expected line is
/// derived from the FIXTURE through the session (the tree's summary or the
/// failure core reports for that root and depth), never from the
/// workspace's own publication; the cells that are not states of the
/// system are NAMED, and the totals, the exclusions, the driven count and
/// the route inventory are pinned exactly (codex post-implementation passes
/// 1 and 2, IPB-3 and IPB-7). A route or a state the model cannot derive
/// fails the fact; a worked row is never the contract. The rows the model
/// does not drive are pinned elsewhere and named in TGB-9: the
/// directional-focus routes (the journey) and the table's sort and count
/// against the one relay (GraphAnnouncerTests).
/// </summary>
public sealed partial class ConnectionsLeafTests
{
    private enum Pane { Visible, Collapsed }

    private enum LeafState { Connections, Other }

    /// <summary>The root: a note in view; no tab at all; the GRAPH tab in
    /// view with a note's tab beside it (a non-note tab active while a note
    /// tab remains — IPB-7).</summary>
    private enum RootState { Note, None, NoneBesideNote }

    /// <summary>The presentation: Ready and current; stale (the root moved
    /// while unmounted or inactive); Error (the root is a note core cannot
    /// load — a missing path — and the load that failed is current); Loading
    /// (the root's FIRST load in flight, no tree yet — the flag set by
    /// definition). A load in flight over a Ready or an Error presentation is
    /// a same-root RELOAD, the rows or the Error kept (Term 7) — IPB-14.</summary>
    private enum Presentation { Current, Stale, Error, Loading }

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
        NonNoteActivation,
        TasksReview,
        HistoryCommand,
        ShowBacklinks,
        FocusEnter,
        TabActivateOther,
        TabActivateDuplicate,
        TabCloseSuccessor,
        TabCloseDuplicate,
        Split,
        SplitSoleTabClose,
        GroupCloseSameRoot,
        GroupCloseOtherRoot,
        GhostCreate,
        GhostCreateFails,
        GhostCreateAlreadyOpen,
        GhostCreateDirtyRefusal,
        GhostCreateSourceMoved,
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

    /// <summary>The derivation: the timeline in order — shell event names
    /// and relay lines, three placeholders standing for the LINE core
    /// reports for a root at a depth (the tree's summary, or the failure):
    /// the final root's after the route, the arranged root's before it (a
    /// completion that speaks the tree it fetched before the route), and
    /// the one the driver captured mid-route — the loads the route issues
    /// in all (counted after the settle: a probe's load is asynchronous),
    /// and the final root (null: none) and staleness (null: not asserted).</summary>
    private sealed record Derivation(string[] Timeline, int Loads, string? Root, bool? Stale);

    private sealed record Fixture(string GhostPath, string GraphSummary);

    private const string LinePlaceholder = "<line>";
    private const string LineBeforePlaceholder = "<line-before>";
    private const string CapturedPlaceholder = "<captured>";
    private const string RenamedRoot = "hub-renamed.md";
    private const string Missing = "missing.md";
    private const string InjectedFailure = "injected failure";

    /// <summary>The pinned cardinalities (IPB-3, IPB-7): the dimensions
    /// crossed with the route inventory; the cells the model names as not
    /// states of the system; the cells driven.</summary>
    private const int PinnedRoutes = 30;
    private const int PinnedCells = 2 * 2 * 3 * 4 * 2 * PinnedRoutes;
    private const int PinnedUnreachable = 2172;
    private const int PinnedDriven = 708;

    private static readonly Route[] SecondTabRoutes =
    [
        Route.TabActivateOther,
        Route.TabActivateDuplicate,
        Route.TabCloseSuccessor,
        Route.TabCloseDuplicate,
        Route.Split,
        Route.SplitSoleTabClose,
        Route.GroupCloseSameRoot,
        Route.GroupCloseOtherRoot,
    ];

    /// <summary>The routes the graph tab in view cannot take: the singleton
    /// neither duplicates nor splits (its commands' CanExecute), and a pane
    /// close onto the SAME root would need a second non-note beside it
    /// (IPB-15: the closes onto the note beside it ARE driven).</summary>
    private static readonly Route[] SingletonRefusedRoutes =
    [
        Route.TabActivateDuplicate,
        Route.TabCloseDuplicate,
        Route.Split,
        Route.GroupCloseSameRoot,
    ];

    private static readonly Route[] GhostRoutes =
    [
        Route.GhostCreate,
        Route.GhostCreateFails,
        Route.GhostCreateAlreadyOpen,
        Route.GhostCreateDirtyRefusal,
        Route.GhostCreateSourceMoved,
    ];

    /// <summary>Why a cell is not a state of the system — the model must
    /// name the reason, never silently skip.</summary>
    private static string? Unreachable(Cell cell)
    {
        bool noRoot = cell.Root is RootState.None or RootState.NoneBesideNote;
        if (noRoot && (cell.Presentation != Presentation.Current || cell.InFlight))
        {
            return "no root: the presentation is NoNote and nothing is in flight";
        }
        if (cell.Presentation == Presentation.Loading && !cell.InFlight)
        {
            return "a Loading presentation IS the root's first load in flight";
        }
        if (cell.InFlight && cell.Presentation == Presentation.Stale)
        {
            return "a root transition clears the in-flight flag (Term 7), so a stale presentation never has a load in flight";
        }
        if (cell.Route == Route.DeeperAtBound && cell.InFlight && cell.Presentation == Presentation.Current)
        {
            return "at the bound over a current tree no same-root reload can be issued: Deeper is core's clamp returning the depth, and the probe finds the tree current and equal";
        }
        if (cell.Presentation == Presentation.Stale && cell.Leaf == LeafState.Connections && cell.Pane == Pane.Visible)
        {
            return "an ACTIVE and MOUNTED leaf with a root has loaded (Terms 3(a), 3(b), 3(d)): STALE is only ever unmounted or inactive at rest";
        }
        if (cell.Route == Route.FocusEnter && (cell.Leaf != LeafState.Connections || cell.Pane != Pane.Visible))
        {
            return "focus enters the anchor only while the leaf is shown";
        }
        if (SecondTabRoutes.Contains(cell.Route) && cell.Root == RootState.None)
        {
            return "the route needs a tab beside the one in view; with no tab at all there is none";
        }
        if (SingletonRefusedRoutes.Contains(cell.Route) && cell.Root == RootState.NoneBesideNote)
        {
            return "the tab in view is the graph's singleton, which neither duplicates nor splits (its commands' CanExecute refuse it); a pane close onto the same root would need a second non-note";
        }
        if (GhostRoutes.Contains(cell.Route) && (cell.Root != RootState.Note || cell.Presentation != Presentation.Current))
        {
            return "a ghost row is activated from a RENDERED tree; a leaf with no root, a stale one or an Error one shows none";
        }
        if (cell.Route == Route.Launch && (cell.Presentation == Presentation.Stale || cell.InFlight))
        {
            return "a launch restores a root, never a stale presentation or a load in flight";
        }
        if (cell.Route == Route.Launch && cell.Pane == Pane.Collapsed)
        {
            return "the pane's visibility is not persisted: a launch comes up MOUNTED";
        }
        if (cell.Route == Route.RenameRoot && cell.Presentation == Presentation.Error)
        {
            return "a root missing from disk cannot be renamed there: core refuses the rename";
        }
        return null;
    }

    private static string ArrangedNote(Cell cell) => cell.Presentation == Presentation.Error ? Missing : Hub;

    /// <summary>The root the arrangement leaves in view: the note (a missing
    /// one for the Error presentation), or — for the split's sole-tab close,
    /// whose arrangement opens Two in the split's group — Two.</summary>
    private static string? RootBefore(Cell cell) => cell.Root switch
    {
        RootState.Note when cell.Route == Route.SplitSoleTabClose => Two,
        RootState.Note => ArrangedNote(cell),
        _ => null,
    };

    /// <summary>The depth the arrangement leaves: three at the bound's
    /// route; two where the load left in flight is a same-root reload with
    /// the rows or the Error kept (every in-flight cell but a Loading one);
    /// one otherwise.</summary>
    private static uint DepthBefore(Cell cell) =>
        cell.Route == Route.DeeperAtBound ? 3u
        : SameRootReloadInFlight(cell) ? 2u
        : 1u;

    /// <summary>A load in flight over a Ready or an Error presentation is a
    /// same-root RELOAD (IPB-14); over a Loading one it is the root's first.</summary>
    private static bool SameRootReloadInFlight(Cell cell) =>
        cell.InFlight && cell.Presentation != Presentation.Loading;

    private static Derivation Derive(Cell cell, Fixture fixture)
    {
        bool mounted = cell.Pane == Pane.Visible;
        bool active = cell.Leaf == LeafState.Connections;
        bool hasRoot = cell.Root == RootState.Note;
        bool stale = cell.Presentation == Presentation.Stale;
        string? rootBefore = RootBefore(cell);
        // Term 2 and B-D3: SPEAKING is ACTIVE at apply — the leaf is the
        // active leaf when the body runs — and MOUNTED is not consulted; a
        // hide alone leaves an in-flight completion audible, a switch away
        // silences it (IGH-14). A completion left in flight that no route
        // supersedes or makes foreign applies after the route's synchronous
        // part and speaks the line core reported for the tree it fetched
        // BEFORE the route — except the probe's silent one, the only reload
        // the bound admits over an Error.
        bool silentInFlight = cell.Route == Route.DeeperAtBound && cell.Presentation == Presentation.Error;
        string[] pending = cell.InFlight && active && !silentInFlight ? [LineBeforePlaceholder] : [];
        bool? restStale = hasRoot ? stale : null;
        string opened = Render(new GraphA11yEvent.GraphStatus(new GraphStatusNote.Opened()));
        string failed = Render(new GraphA11yEvent.GraphBlocked(new GraphBlockedReason.NoteCreateFailed(InjectedFailure)));
        switch (cell.Route)
        {
            case Route.SwitchToLeaf:
                if (active)
                {
                    // A same-leaf switch is the setter's no-op.
                    return new(pending, 0, rootBefore, restStale);
                }
                {
                    // Term 3(b): a mounted switch loads only when STALE — an Error
                    // that is current does not reload on a switch; a load in
                    // flight is a current request. The in-flight completion speaks
                    // iff the user switched back before it applied (IGH-14).
                    int loads = mounted && hasRoot && stale && !cell.InFlight ? 1 : 0;
                    string[] spoken = loads == 1 ? [LinePlaceholder] : cell.InFlight && !silentInFlight ? [LineBeforePlaceholder] : [];
                    return new(["LeafPanelShown", .. spoken], loads, rootBefore, hasRoot ? stale && loads == 0 && !cell.InFlight : null);
                }
            case Route.SwitchAway:
                if (!active)
                {
                    return new([], 0, rootBefore, restStale);
                }
                // The completion, when one is in flight, applies silently.
                return new(["LeafPanelShown"], 0, rootBefore, restStale);
            case Route.Show:
                {
                    // Term 3(c): one audible load whether current or not; the
                    // shell's pane line when collapsed (B-D9), the setter's leaf
                    // line when the leaf changes, the graph family's panel line
                    // when it does not (B-D5); a load in flight is superseded.
                    var timeline = new List<string>();
                    if (!mounted)
                    {
                        timeline.Add("RightPaneShown");
                    }
                    if (!active)
                    {
                        timeline.Add("LeafPanelShown");
                    }
                    if (active)
                    {
                        timeline.Add(PanelLine());
                    }
                    if (hasRoot)
                    {
                        timeline.Add(LinePlaceholder);
                    }
                    return new([.. timeline], hasRoot ? 1 : 0, rootBefore, hasRoot ? false : null);
                }
            case Route.PaneToggle:
                if (mounted)
                {
                    // A hide: the view state clears, the document keeps its
                    // presentation; a completion in flight still speaks when the
                    // leaf is the active one (B-D3), silently otherwise.
                    return new(["RightPaneHidden", .. pending], 0, rootBefore, restStale);
                }
                {
                    // Term 3(a): a MOUNT with the leaf active loads, current or
                    // not (superseding a load in flight); with another leaf
                    // active nothing loads and a completion in flight applies
                    // silently.
                    int loads = active && hasRoot ? 1 : 0;
                    return new(["RightPaneShown", .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, rootBefore, hasRoot ? stale && loads == 0 && !cell.InFlight : null);
                }
            case Route.RootChange:
                {
                    // Term 3(d): a load only while ACTIVE and MOUNTED, else STALE;
                    // an in-flight result for the old root is foreign. The
                    // shell's `TabFocused` when the open creates the first tab or
                    // activates the note's existing tab (an in-place open into an
                    // existing tab posts none).
                    int loads = active && mounted ? 1 : 0;
                    return new([.. (hasRoot ? Array.Empty<string>() : ["TabFocused"]), .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, Two, loads == 0);
                }
            case Route.DepthChange:
                {
                    // Term 3(e): a load with a root (superseding a load in
                    // flight); spoken iff SPEAKING at apply, installed either way.
                    int loads = hasRoot ? 1 : 0;
                    return new(loads == 1 && active ? [LinePlaceholder] : [], loads, rootBefore, hasRoot ? false : null);
                }
            case Route.DeeperAtBound:
                // Deeper at the maximum is core's clamp returning the same
                // depth: no write, no load (B-14, B-19 v).
                return new(pending, 0, rootBefore, restStale);
            case Route.RootToNone:
                if (cell.Root == RootState.None)
                {
                    return new([], 0, null, null);
                }
                if (cell.Root == RootState.NoneBesideNote)
                {
                    // The close of the graph tab: its successor is the note beside
                    // it — a root change from none (Term 3(d)).
                    int loads = active && mounted ? 1 : 0;
                    return new(["TabFocused", "TabClosed", .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, Two, loads == 0);
                }
                // Term 3(g): NoNote synchronously, the in-flight result dropped.
                return new(["TabClosed"], 0, null, null);
            case Route.NonNoteActivation:
                // The graph tab opened (PR A's own lines through the same relay:
                // Opened, then its snapshot's summary — Opened alone when the
                // tab is already effective and READY); NoNote for the leaf,
                // nothing from it; the in-flight result dropped.
                if (cell.Root == RootState.NoneBesideNote)
                {
                    return new([opened], 0, null, null);
                }
                return new(["TabFocused", opened, fixture.GraphSummary], 0, null, null);
            case Route.TasksReview:
            case Route.HistoryCommand:
                {
                    // The reveal-then-switch commands: MOUNT is evaluated at the
                    // route's end with the review or History active — no
                    // Connections load; the shell's pane line when collapsed, the
                    // setter's leaf line, that command's own line.
                    var timeline = new List<string>();
                    if (!mounted)
                    {
                        timeline.Add("RightPaneShown");
                    }
                    timeline.Add("LeafPanelShown");
                    timeline.Add(cell.Route == Route.TasksReview ? "TasksReviewShown" : "HistoryPanelShown");
                    return new([.. timeline], 0, rootBefore, hasRoot ? stale && !cell.InFlight : null);
                }
            case Route.ShowBacklinks:
                {
                    // Bases' Show backlinks (IPB-2): ONE outer mutation — the open,
                    // the switch to Backlinks, the reveal, the consume — so the
                    // boundary reconciles the new root against the FINAL leaf:
                    // STALE, no load; an in-flight result is foreign. The Bases
                    // line is the document's and is not driven here.
                    var timeline = new List<string>();
                    if (!hasRoot)
                    {
                        timeline.Add("TabFocused");
                    }
                    timeline.Add("LeafPanelShown");
                    if (!mounted)
                    {
                        timeline.Add("RightPaneShown");
                    }
                    return new([.. timeline], 0, Two, true);
                }
            case Route.FocusEnter:
                {
                    // Term 9: LoadingConnections while STALE or Loading — an Error
                    // that is current and a same-root reload that kept its rows
                    // or its Error are neither — nothing for a ready tree with
                    // rows, for Error or for NoNote; then the in-flight
                    // completion speaks (the leaf is shown).
                    var timeline = new List<string>();
                    if (hasRoot && cell.Presentation == Presentation.Loading)
                    {
                        timeline.Add(LoadingLine());
                    }
                    timeline.AddRange(pending);
                    return new([.. timeline], 0, rootBefore, hasRoot ? false : null);
                }
            case Route.TabActivateOther:
                {
                    // An existing tab activated: the shell's `TabFocused`; a root
                    // change — Term 3(d) — from a note or from the graph tab.
                    int loads = active && mounted ? 1 : 0;
                    return new(["TabFocused", .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, Two, loads == 0);
                }
            case Route.TabActivateDuplicate:
                // The same note's other tab: `TabFocused`; no root change.
                return new(["TabFocused", .. pending], 0, rootBefore, restStale);
            case Route.TabCloseSuccessor:
                {
                    // The close's successor is another note: the successor's
                    // `TabFocused` (the group's setter) then `TabClosed`; a root
                    // change — Term 3(d).
                    int loads = active && mounted ? 1 : 0;
                    return new(["TabFocused", "TabClosed", .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, Two, loads == 0);
                }
            case Route.TabCloseDuplicate:
                // The successor is the same note: its `TabFocused`, `TabClosed`;
                // no root change.
                return new(["TabFocused", "TabClosed", .. pending], 0, rootBefore, restStale);
            case Route.Split:
                // The duplicate's `TabFocused`; the root unchanged, no load.
                return new(["TabFocused", .. pending], 0, rootBefore, restStale);
            case Route.SplitSoleTabClose:
                {
                    // The split's sole tab closed: A → none → B inside ONE mutation
                    // (IGH-4), reconciled once at the boundary with the surviving
                    // group's note — the note before the split — a root change
                    // (Term 3(d)); the in-flight result for the closed note
                    // foreign.
                    int loads = active && mounted ? 1 : 0;
                    string survivor = cell.Root == RootState.NoneBesideNote ? Two : ArrangedNote(cell);
                    return new(["TabClosed", .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, survivor, loads == 0);
                }
            case Route.GroupCloseSameRoot:
                // `EditorPaneFocused`; the surviving group shows the same note.
                return new(["EditorPaneFocused", .. pending], 0, rootBefore, restStale);
            case Route.GroupCloseOtherRoot:
                {
                    // `EditorPaneFocused`; the surviving group shows another note:
                    // a root change — Term 3(d).
                    int loads = active && mounted ? 1 : 0;
                    return new(["EditorPaneFocused", .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, Two, loads == 0);
                }
            case Route.GhostCreate:
                {
                    // B-11: the open is silent, then ONE NoteCreated (the shell's
                    // `A11yEvent.Graph`, A-8's direct post), then the root change's
                    // summary iff the load was issued (ACTIVE and MOUNTED) and
                    // SPEAKING; inactive or unmounted, the root moves STALE; a
                    // same-root reload in flight is foreign once the root moved.
                    int loads = active && mounted ? 1 : 0;
                    return new(["Graph", .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, fixture.GhostPath, loads == 0);
                }
            case Route.GhostCreateFails:
                // The failure is the relay's HIGH line (IPB-1); nothing else —
                // then the reload left in flight applies and speaks the tree it
                // fetched before the attempt, iff active.
                return new([failed, .. pending], 0, rootBefore, false);
            case Route.GhostCreateAlreadyOpen:
                {
                    // The created note's path already has a tab: the open ACTIVATES
                    // it (`TabFocused`), then NoteCreated; a root change (Term 3(d)).
                    int loads = active && mounted ? 1 : 0;
                    return new(["TabFocused", "Graph", .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, fixture.GhostPath, loads == 0);
                }
            case Route.GhostCreateDirtyRefusal:
                // The dirty tab refuses the navigation: NoteCreated, no root
                // move, nothing more; then the reload left in flight applies.
                return new(["Graph", .. pending], 0, rootBefore, false);
            case Route.GhostCreateSourceMoved:
                {
                    // The root moved while the create was parked (B-11): the root
                    // change's own load (Term 3(d)) — its line the tree fetched
                    // BEFORE the create landed, captured by the driver — then
                    // NoteCreated after the attempt, the open suppressed.
                    int loads = active && mounted ? 1 : 0;
                    return new([.. (loads == 1 ? [CapturedPlaceholder] : Array.Empty<string>()), "Graph"], loads, Two, loads == 0);
                }
            case Route.RenameRoot:
                if (!hasRoot)
                {
                    return new([], 0, null, null);
                }
                {
                    // The workspace's retarget re-derives the panels (the outline's
                    // count is the shell's line): a root change to the new path —
                    // ONE audible load while ACTIVE and MOUNTED, else STALE; then
                    // the probe (Term 6): a load in flight is marked only, a stale
                    // leaf reloads silently once. One load either way; an
                    // in-flight result for the old root is foreign.
                    bool audible = active && mounted;
                    return new(["OutlineCount", .. (audible ? [LinePlaceholder] : Array.Empty<string>())], 1, RenamedRoot, false);
                }
            case Route.DeleteRoot:
                if (!hasRoot)
                {
                    return new([], 0, null, null);
                }
                {
                    // The shell's missing-file line; the tab and the root are KEPT;
                    // the probe (Term 6): the held tree is older, or Error — a
                    // silent load — or, with a load in flight, the mark, whose
                    // completion speaks what it fetched before the delete (iff
                    // active): a tree's summary, reloaded silently once after it
                    // (the mark above its generation); a failure, after which the
                    // mark waits for the next install. Nothing else spoken.
                    bool failsInFlight = cell.InFlight && cell.Presentation == Presentation.Error;
                    int loads = failsInFlight ? 0 : 1;
                    return new(["HostComposed", .. pending], loads, rootBefore, false);
                }
            case Route.Launch:
                {
                    // The launch restores the leaf and the tabs (the pane comes up
                    // visible): the seeded mount's ONE load with the leaf active
                    // and a note restored, its line iff SPEAKING; a restored graph
                    // tab speaks PR A's summary (Activation); the shell's line is
                    // an existing note's outline count.
                    if (cell.Root == RootState.NoneBesideNote)
                    {
                        return new([fixture.GraphSummary], 0, null, null);
                    }
                    int loads = active && hasRoot ? 1 : 0;
                    bool exists = hasRoot && cell.Presentation != Presentation.Error;
                    return new([.. (exists ? ["OutlineCount"] : Array.Empty<string>()), .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, rootBefore, hasRoot ? loads == 0 : null);
                }
            case Route.Shutdown:
                // The leaf retires into the drain, the relay after it: nothing.
                return new([], 0, rootBefore, null);
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

    private sealed class ParkedCreator(VaultSession session) : FileManagement.ISurfaceNoteCreator
    {
        private readonly RecordingCreator _inner = new(session);

        public ManualResetEventSlim Reached { get; } = new(false);

        public ManualResetEventSlim Gate { get; } = new(false);

        public FileManagement.NoteCreateResult TryCreateNote(string path, string content)
        {
            Reached.Set();
            Gate.Wait(TimeSpan.FromSeconds(10));
            return _inner.TryCreateNote(path, content);
        }

        public void NoteLanded(string path) => _inner.NoteLanded(path);

        public void SpeakCaveat(string caveat) => _inner.SpeakCaveat(caveat);
    }

    /// <summary>A model host: a production-like lifecycle generation (a
    /// restored graph tab publishes under the lifecycle's counter, PR A).</summary>
    private static Host ModelHost(string root) => new(root, () => 1);

    /// <summary>Arrange the workspace in the cell's state, from a fresh
    /// vault: the leaf activated and the note opened while ACTIVE and
    /// MOUNTED for a current or an Error presentation (then moved away
    /// from or hidden), the note opened while inactive for a stale one,
    /// the last load left in flight — PARKED after its crossings until the
    /// route's synchronous part has run, so its completion is a state of
    /// the route and not a race; a same-root reload with the rows or the
    /// Error kept where the route needs them rendered; the route's own
    /// props before or after — a tab beside, a split, the depth at its
    /// bound, a creator, a dirty tab. Returns the parked fetch's gate.</summary>
    private static ManualResetEventSlim? Arrange(Host host, Cell cell, Fixture fixture)
    {
        host.Workspace.ActiveLeaf = Host.OutlineLeaf;
        bool rootInNewTab = false;
        switch (cell.Route)
        {
            case Route.TabActivateOther:
            case Route.TabCloseSuccessor:
                if (cell.Root == RootState.Note)
                {
                    host.OpenNote(Two);
                    rootInNewTab = true;
                }
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
            case Route.GhostCreateAlreadyOpen:
            case Route.GhostCreateDirtyRefusal:
                host.Workspace.GraphNoteCreator = new RecordingCreator(host.Session);
                break;
            case Route.GhostCreateFails:
                host.Workspace.GraphNoteCreator = new FailingCreator(InjectedFailure);
                break;
            case Route.GhostCreateSourceMoved:
                host.Workspace.GraphNoteCreator = new ParkedCreator(host.Session);
                break;
        }
        host.Settle();

        string root = ArrangedNote(cell);
        ManualResetEventSlim? gate = null;
        ManualResetEventSlim? reached = null;
        bool park = cell.InFlight && cell.Route != Route.Shutdown;

        // Park the NEXT fetch after its crossings; the wait, after the load
        // is issued, makes sure it HAS crossed — the tree it holds is the
        // one before the route.
        ManualResetEventSlim? Arm()
        {
            if (!park)
            {
                return null;
            }
            (ManualResetEventSlim parked, ManualResetEventSlim crossed) = Park(host.Leaf);
            reached = crossed;
            return parked;
        }

        void WaitParked()
        {
            if (reached is not null)
            {
                Assert.True(reached.Wait(TimeSpan.FromSeconds(10)), $"{cell}: the fetch never parked");
            }
        }

        void OpenRoot()
        {
            if (rootInNewTab)
            {
                host.Workspace.OpenPath(root, WorkspaceOpenTarget.NewTab);
            }
            else
            {
                host.Workspace.OpenPath(root, WorkspaceOpenTarget.CurrentTab);
            }
        }

        // The route's props after the note, and — for the routes whose load
        // in flight must be a SAME-ROOT reload with the rows or the Error
        // kept, and for the split's sole-tab close — that pending load.
        bool AfterTheNote()
        {
            switch (cell.Route)
            {
                case Route.TabActivateDuplicate:
                case Route.TabCloseDuplicate:
                    // A second open of an open path is a registry hit that
                    // activates the existing tab; the duplicate is the command's.
                    host.Workspace.DuplicateTabCommand.Execute(null);
                    Assert.Equal(2, host.Workspace.ActiveGroup.Tabs.Count(tab => string.Equals(tab.Path, root, StringComparison.Ordinal)));
                    break;
                case Route.GroupCloseSameRoot:
                    host.Workspace.SplitRightCommand.Execute(null);
                    break;
                case Route.SplitSoleTabClose:
                    host.Settle();
                    host.Workspace.SplitRightCommand.Execute(null);
                    host.Settle();
                    if (cell.Presentation == Presentation.Loading)
                    {
                        // Two's FIRST load is the one in flight.
                        gate = Arm();
                        host.OpenNote(Two);
                        WaitParked();
                        return true;
                    }
                    host.OpenNote(Two);
                    host.Settle();
                    break;
                case Route.GhostCreateAlreadyOpen:
                    host.Settle();
                    host.Workspace.OpenPath(fixture.GhostPath, WorkspaceOpenTarget.NewTab);
                    host.Settle();
                    host.Workspace.ActiveGroup.ActiveTab = TabFor(host, root);
                    host.Settle();
                    break;
                case Route.GhostCreateDirtyRefusal:
                    host.Settle();
                    host.Workspace.ActiveGroup.ActiveTab!.Text = "# hub, edited and unsaved\n";
                    Assert.True(host.Workspace.ActiveGroup.ActiveTab!.IsDirty, "the tab did not become dirty");
                    break;
            }
            if (SameRootReloadInFlight(cell))
            {
                // A same-root reload in flight, the rows (or the Error) kept:
                // Deeper's audible one — or, at the bound, the probe's silent
                // one over the Error (Term 6), which decides on the pool.
                host.Settle();
                gate = Arm();
                if (cell.Route == Route.DeeperAtBound)
                {
                    host.Workspace.NotifyGraphOfVaultChange();
                    Assert.True(
                        SpinWait.SpinUntil(() =>
                        {
                            PumpedDispatcher.Drain();
                            return host.Leaf.InFlight;
                        }, TimeSpan.FromSeconds(10)),
                        $"{cell}: the probe issued no reload over the Error");
                }
                else
                {
                    host.Workspace.ConnectionsDeeperCommand.Execute(null);
                }
                WaitParked();
                return true;
            }
            return false;
        }

        if (cell.Root == RootState.None || cell.Root == RootState.NoneBesideNote)
        {
            if (cell.Root == RootState.NoneBesideNote)
            {
                host.OpenNote(Two);
                bool splitPane = cell.Route is Route.SplitSoleTabClose or Route.GroupCloseOtherRoot;
                if (splitPane)
                {
                    // The graph in a pane of its own beside the note's (IPB-15).
                    host.Workspace.SplitRightCommand.Execute(null);
                    host.Settle();
                }
                host.Workspace.OpenGraph();
                // The graph tab's own load is the ARRANGEMENT's: drained before
                // the timeline is cleared, so it cannot land inside the route.
                SettleTheGraph(host);
                if (cell.Route == Route.SplitSoleTabClose)
                {
                    // The graph the SOLE tab of the split's pane.
                    host.Workspace.ActiveGroup.ActiveTab = TabFor(host, Two);
                    host.Workspace.CloseActiveTabCommand.Execute(null);
                    host.Settle();
                    SettleTheGraph(host);
                    Assert.True(
                        host.Workspace.ActiveGroup.Tabs.Count == 1 && host.Workspace.ActiveGroup.ActiveTab!.IsGraph,
                        $"{cell}: the split's pane does not hold the graph alone");
                }
            }
            if (cell.Leaf == LeafState.Connections)
            {
                host.ActivateLeaf();
            }
            if (cell.Pane == Pane.Collapsed)
            {
                host.Workspace.IsRightPaneVisible = false;
            }
            host.Settle();
            SettleTheGraph(host);
            Assert.Null(host.Leaf.Root);
            return null;
        }
        if (cell.Presentation == Presentation.Stale)
        {
            OpenRoot();
            _ = AfterTheNote();
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
            return null;
        }
        host.ActivateLeaf();
        bool pendingIsTheRootsOwn = cell.Presentation == Presentation.Loading && cell.Route != Route.SplitSoleTabClose;
        if (pendingIsTheRootsOwn)
        {
            gate = Arm();
        }
        OpenRoot();
        if (pendingIsTheRootsOwn)
        {
            WaitParked();
        }
        if (!cell.InFlight || !pendingIsTheRootsOwn)
        {
            host.Settle();
        }
        _ = AfterTheNote();
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
            // The arranged state, asserted (IPB-14): the flag, the presentation
            // the flag is over, the root and the pending request's depth.
            Assert.True(host.Leaf.InFlight, $"{cell}: the arrangement left nothing in flight");
            // The presentation is the root IN VIEW's: Missing's Error, or Two's
            // Ready where the split's sole-tab close arranged Two.
            ConnectionsLoadState expectedState = cell.Presentation == Presentation.Loading
                ? ConnectionsLoadState.Loading
                : RootBefore(cell) == Missing ? ConnectionsLoadState.Error : ConnectionsLoadState.Ready;
            Assert.True(
                host.Leaf.Publication.State == expectedState,
                $"{cell}: the arrangement left the presentation {host.Leaf.Publication.State}, not {expectedState}");
            Assert.True(
                string.Equals(host.Leaf.Root, RootBefore(cell), StringComparison.Ordinal),
                $"{cell}: the arrangement left the root {host.Leaf.Root ?? "none"}, not {RootBefore(cell)}");
            Assert.True(
                host.Leaf.Request is { } request && request.Depth == DepthBefore(cell),
                $"{cell}: the pending request is not at depth {DepthBefore(cell)}");
        }
        else
        {
            host.Settle();
            Assert.True(host.Leaf.IsCurrent, $"{cell}: the arrangement did not leave the presentation current");
            bool errorInView = RootBefore(cell) == Missing;
            Assert.True(
                errorInView == (host.Leaf.Publication.State == ConnectionsLoadState.Error),
                $"{cell}: the arrangement left the presentation {host.Leaf.Publication.State} over {host.Leaf.Root}");
        }
        return gate;
    }

    /// <summary>Drive the route; returns the line captured mid-route where
    /// the derivation names one.</summary>
    private static string? Drive(Host host, Cell cell, Fixture fixture, ManualResetEventSlim? parked)
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
            case Route.NonNoteActivation:
                host.Workspace.OpenGraph();
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
                host.Workspace.ActiveGroup.ActiveTab = TabFor(host, RootBefore(cell)!, inactiveOne: true);
                break;
            case Route.TabCloseSuccessor:
            case Route.TabCloseDuplicate:
            case Route.SplitSoleTabClose:
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
            case Route.GhostCreateAlreadyOpen:
            case Route.GhostCreateDirtyRefusal:
                host.Leaf.Activate(FirstGhost(host.Leaf).Ghost, newTab: false);
                // The creation drained and its completion applied — the leaf
                // NOT settled: a reload parked in flight stays parked until
                // the route's synchronous part has run.
                PumpedDispatcher.PumpUntilDrained(host.Workspace.DrainGraphNoteCreationForTests());
                PumpedDispatcher.Drain();
                break;
            case Route.GhostCreateSourceMoved:
                {
                    var creator = (ParkedCreator)host.Workspace.GraphNoteCreator!;
                    host.Leaf.Activate(FirstGhost(host.Leaf).Ghost, newTab: false);
                    Assert.True(creator.Reached.Wait(TimeSpan.FromSeconds(10)), "the create never reached the gate");
                    host.OpenNote(Two);
                    // The root's own load lands BEFORE the create does: its tree
                    // is the one before the ghost healed into a note.
                    parked?.Set();
                    host.Settle();
                    string captured = LineFor(host, Two, DepthBefore(cell));
                    creator.Gate.Set();
                    DrainCreate(host);
                    return captured;
                }
            case Route.RenameRoot:
                host.Session.RenameFile(Hub, RenamedRoot);
                host.Workspace.RetargetPath(Hub, RenamedRoot);
                {
                    int pending = host.Leaf.PendingWorkForTests;
                    host.Workspace.NotifyGraphOfVaultChange();
                    WaitForTheProbesDecision(host, cell, pending);
                }
                break;
            case Route.DeleteRoot:
                {
                    // The delete as the lifecycle sees it — the file gone and the
                    // vault rescanned (core's own delete trashes through COM and
                    // wants a thread of its own), then the lifecycle's two arms.
                    string path = Path.Combine(host.Root, RootBefore(cell) ?? Hub);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    using (var cancel = new CancelToken())
                    {
                        host.Session.ScanInitial(cancel);
                    }
                    host.Workspace.InvalidatePath(RootBefore(cell) ?? Hub);
                    int pending = host.Leaf.PendingWorkForTests;
                    host.Workspace.NotifyGraphOfVaultChange();
                    WaitForTheProbesDecision(host, cell, pending);
                    break;
                }
            case Route.Shutdown:
                host.Workspace.Dispose();
                break;
            case Route.Launch:
                throw new InvalidOperationException("the launch is driven by the host's construction");
            default:
                throw new InvalidOperationException($"no driver for {cell.Route}");
        }
        return null;
    }

    /// <summary>The graph tab's own work drained, where a route or a state
    /// seated one (PR A's lines ride the same relay).</summary>
    private static void SettleTheGraph(Host host)
    {
        if (host.Workspace.GraphDocument is { IsRetired: false } graph)
        {
            PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
        }
    }

    /// <summary>The probe decides on the pool and applies on the dispatcher
    /// (Term 6): the route's synchronous part is over only once that
    /// decision has applied — the mark, with a load in flight; a load,
    /// otherwise — so a fetch parked in flight is released AFTER it and
    /// the two never race.</summary>
    private static void WaitForTheProbesDecision(Host host, Cell cell, int pendingBefore)
    {
        // The probe's tracked task completes after its apply (Term 7), so the
        // leaf's pending count is back at or below what it was before the
        // probe was issued only once the decision — the mark, a load, or
        // nothing — has applied; a load the route issued may complete
        // meanwhile, which only lowers the count further.
        Assert.True(
            SpinWait.SpinUntil(() =>
            {
                PumpedDispatcher.Drain();
                return host.Leaf.PendingWorkForTests <= pendingBefore;
            }, TimeSpan.FromSeconds(30)),
            $"{cell}: the probe's decision never applied (pending {host.Leaf.PendingWorkForTests} against {pendingBefore}, in flight {host.Leaf.InFlight}, root {host.Leaf.Root ?? "none"})");
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

    /// <summary>The line core reports for a root at a depth — the tree's
    /// summary, or the failure the leaf would render — read through the
    /// session, never from the workspace's publication.</summary>
    private static string LineFor(Host host, string root, uint depth)
    {
        try
        {
            return Render(new GraphA11yEvent.GraphNeighborhoodSummary(
                host.Session.GraphConnectionsTree(root, depth, SlateUniffiMethods.GraphConnectionsFilter()).SummaryCounts));
        }
        catch (VaultException failure)
        {
            return Render(new GraphA11yEvent.GraphBlocked(new GraphBlockedReason.ConnectionsLoadFailed(failure.Message)));
        }
    }

    private static Fixture FixtureOf(string vaultRoot)
    {
        using VaultSession session = VaultSession.OpenFilesystem(vaultRoot);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        GraphConnectionsTree hub = session.GraphConnectionsTree(Hub, 1, SlateUniffiMethods.GraphConnectionsFilter());
        GraphConnectionRow ghost = hub.Outgoing.First(row => row.Kind == GraphNodeKind.Ghost);
        GraphSnapshot snapshot = session.GraphSnapshot(GraphViewState.DefaultFilter());
        return new Fixture(
            SlateUniffiMethods.GraphGhostNotePath(ghost.TargetRaw),
            Render(new GraphA11yEvent.GraphSnapshotSummary(snapshot.SummaryCounts)));
    }

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
            Fixture? fixture = null;
            foreach (Cell cell in cells)
            {
                if (Unreachable(cell) is { } reason)
                {
                    unreachable.Add($"{cell}: {reason}");
                    continue;
                }
                using GraphVault vault = GraphVault.Copy($"model-{driven}");
                fixture ??= FixtureOf(vault.Root);
                Derivation expected = Derive(cell, fixture);
                Host host;
                int before;
                string? lineBefore = null;
                string? captured = null;
                if (cell.Route == Route.Launch)
                {
                    using (Host first = ModelHost(vault.Root))
                    {
                        _ = Arrange(first, cell, fixture);
                        first.Settle();
                        SettleTheGraph(first);
                    }
                    // The launch IS the construction: its timeline from the start.
                    host = ModelHost(vault.Root);
                    before = 0;
                }
                else
                {
                    host = ModelHost(vault.Root);
                    ManualResetEventSlim? parked = Arrange(host, cell, fixture);
                    if (RootBefore(cell) is { } rootBefore)
                    {
                        lineBefore = LineFor(host, rootBefore, DepthBefore(cell));
                    }
                    host.Clear();
                    before = host.Loads;
                    captured = Drive(host, cell, fixture, parked);
                    parked?.Set();
                }
                driven++;
                if (cell.Route != Route.Shutdown)
                {
                    host.Settle();
                    SettleTheGraph(host);
                }
                int loads = host.Loads - before;

                uint depthAfter = cell.Route == Route.DepthChange ? DepthBefore(cell) + 1 : DepthBefore(cell);
                string[] timeline =
                [
                    .. expected.Timeline.Select(entry => entry switch
                    {
                        LinePlaceholder => expected.Root is { } root ? LineFor(host, root, depthAfter) : "<no root to report>",
                        LineBeforePlaceholder => lineBefore ?? "<no tree before the route>",
                        CapturedPlaceholder => captured ?? "<nothing captured>",
                        _ => entry,
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
                if (cell.Route != Route.Shutdown && !string.Equals(host.Leaf.Root, expected.Root, StringComparison.Ordinal))
                {
                    mismatch.Add($"root {host.Leaf.Root ?? "none"}, derived {expected.Root ?? "none"}");
                }
                if (expected.Stale is { } stale && host.Leaf.Root is not null && host.Leaf.IsStale != stale)
                {
                    mismatch.Add($"stale {host.Leaf.IsStale}, derived {stale}");
                }
                if (cell.Route != Route.Shutdown && host.Leaf.Root is not null && host.Leaf.Depth != depthAfter)
                {
                    mismatch.Add($"depth {host.Leaf.Depth}, derived {depthAfter}");
                }
                if (mismatch.Count > 0)
                {
                    string tabs = cell.Route == Route.Shutdown
                        ? string.Empty
                        : $" — tabs [{string.Join(", ", host.Workspace.Groups.SelectMany(g => g.Tabs).Select(t => t.Item.Kind + ":" + (t.Path ?? t.Title) + (ReferenceEquals(t, host.Workspace.ActiveGroup.ActiveTab) ? "*" : "")))}]";
                    failures.Add($"{cell}: {string.Join("; ", mismatch)}{tabs}");
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
            $"the model named {unreachable.Count} cells as not states of the system and drove {driven}; pinned {PinnedUnreachable} and {PinnedDriven}");
    }
}
