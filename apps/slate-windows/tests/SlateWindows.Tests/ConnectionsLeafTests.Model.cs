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
/// it), the presentation (Ready and current; stale; a transient Error over
/// a note that exists; a MISSING root; Loading) and the load in flight (none;
/// an audible one; a silent one — the token's policy, Term 5). Every
/// reachable cell arranges a fresh workspace in that state, asserts the
/// arrangement, drives it through the route, and asserts the recorded
/// timeline, the loads issued, the final root, depth, presentation state
/// and in-flight flag equal the derivation. A load left in flight is
/// PARKED inside its fetch, after its crossings, until the route's
/// synchronous part has run, so "in flight during the route" is a state
/// and not a race; every expected line is derived from the FIXTURE through
/// the session, never from the workspace's own publication; the cells that
/// are not states of the system are NAMED, and the totals, the exclusions,
/// the driven count and the route inventory are pinned exactly (codex
/// post-implementation passes 1–4: IPB-3, IPB-7, IPB-14, IPB-15, IPB-22..26).
/// A route or a state the model cannot derive fails the fact; a worked
/// row is never the contract. The rows the model does not drive are
/// pinned elsewhere and named in TGB-9: the directional-focus routes (the
/// journey) and the table's sort and count against the one relay
/// (GraphAnnouncerTests).
/// </summary>
/// <remarks>
/// W6-2 PR B2 (#746), contract B2-8 — rule D over the same model: a MODE
/// dimension crosses every B1 route with three literal PINNED arrangements
/// (IGJ-12) beside FOLLOWING — PinnedFresh (pin P, note in view P, stack
/// [(FOLLOWING, A)]), PinnedDrifted (pin P, note in view N ≠ P, stack
/// [(FOLLOWING, A), (PINNED B)]) and PinnedNoOrigin (pin P, note in view
/// P, stack empty) — with the presentation and the load in flight the
/// PIN's. Under a pin a note change is recorded and is not a root change
/// (Term 11): the note-in-view routes keep the shell's lines, load nothing,
/// leave the pin and let the load in flight apply; the rename and delete
/// routes act on the PIN, and five routes join the inventory for the
/// hooks over the entries and the note in view, with a file and a folder
/// (Term 16). The state compared after every route gains the mode, the
/// note in view, the WHOLE stack (IGI-7), the shared key, the pending
/// mount and the last focus request (IGL-3). The re-root and Back routes
/// have a product of their own (<c>ConnectionsLeafTests.Model.ReRoot.cs</c>).
/// </remarks>
public sealed partial class ConnectionsLeafTests
{
    /// <summary>The root mode (rule D, Term 11) and, under a pin, which of
    /// IGJ-12's three literal arrangements the leaf is in.</summary>
    private enum Mode { Following, PinnedFresh, PinnedDrifted, PinnedNoOrigin }

    private enum Pane { Visible, Collapsed }

    private enum LeafState { Connections, Other }

    /// <summary>The root: a note in view; no tab at all; the GRAPH tab in
    /// view with a note's tab beside it (a non-note tab active while a note
    /// tab remains — IPB-7). Under a pin this is the NOTE IN VIEW's state;
    /// the root is the pin.</summary>
    private enum RootState { Note, None, NoneBesideNote }

    /// <summary>The presentation: Ready and current; stale (the root moved
    /// while unmounted or inactive); Error — a TRANSIENT failure over a note
    /// that exists (IPB-26); Missing — the root is a path core cannot load,
    /// its Error core's own; Loading — no tree yet: the root's first load
    /// in flight, or REJECTED at the receiver and nothing in flight (IPB-22).</summary>
    private enum Presentation { Current, Stale, Error, Missing, Loading }

    /// <summary>The load in flight, PARKED after its crossings: none; an
    /// AUDIBLE one (Summary on the token — the root's first load over
    /// Loading, Deeper's reload over a tree or an Error); a SILENT one (the
    /// probe's reload, Term 6). The token's policy decides the completion's
    /// speech (Term 5), not the route that follows — IPB-23.</summary>
    private enum Pending { None, Audible, Silent }

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
        // W6-2 PR B2 (Term 16): the hooks over the entries and the note in
        // view, with a file and a folder.
        RenameOrigin,
        DeleteOrigin,
        RenameNoteInView,
        RenameFolder,
        DeleteFolder,
    }

    private sealed record Cell(Mode Mode, Pane Pane, LeafState Leaf, RootState Root, Presentation Presentation, Pending Pending, Route Route)
    {
        public bool InFlight => Pending != Pending.None;

        public bool Pinned => Mode != Mode.Following;

        /// <summary>The leaf has an effective root: the pin, or FOLLOWING a
        /// note in view.</summary>
        public bool HasRoot => Pinned || Root == RootState.Note;

        public override string ToString() =>
            $"{Route} from [{Mode}, {Pane}, {Leaf}, {Root}, {Presentation}{Pending switch { Pending.Audible => ", an audible load in flight", Pending.Silent => ", a silent load in flight", _ => "" }}]";
    }

    /// <summary>The derivation: the timeline in order — shell event names
    /// and relay lines, three placeholders standing for the LINE core
    /// reports for a root at a depth (the tree's summary, or the failure):
    /// the final root's after the route, the arranged root's before it (a
    /// completion that speaks the tree it fetched before the route), and
    /// the one the driver captured mid-route — the loads the route issues
    /// in all (counted after the settle: a probe's load is asynchronous),
    /// the final root (null: none) and staleness (null: not asserted),
    /// whether the load left in flight APPLIES (rather than being made
    /// foreign by a root change or superseded by a newer load), and an
    /// explicit final presentation state where the default rule does not
    /// hold.</summary>
    private sealed record Derivation(
        string[] Timeline,
        int Loads,
        string? Root,
        bool? Stale,
        bool Reload = true,
        ConnectionsLoadState? State = null,
        int? LoadsOr = null);

    /// <summary>Rule D's state after a route: the pin (null FOLLOWING), the
    /// note in view, the whole stack and the shared key.</summary>
    private sealed record RootMode(string? Pin, string? NoteInView, (string? Pin, string Effective)[] Stack, string? Key);

    private sealed record Fixture(string GhostPath, string PinnedGhostPath, string GraphSummary, string GraphSummaryAfterBump);

    private const string LinePlaceholder = "<line>";
    private const string LineBeforePlaceholder = "<line-before>";
    private const string CapturedPlaceholder = "<captured>";
    private const string RenamedRoot = "hub-renamed.md";
    private const string MissingRoot = "missing.md";
    private const string Board = "board.canvas";
    private const string InjectedFailure = "injected failure";
    private const string InjectedTransientFailure = "injected transient failure";

    // W6-2 PR B2: the pinned arrangements' paths. The pin is Deep — a note
    // in a folder, so a file and a folder rename and delete each have a
    // subject; the origin A is Two, the prior pin B is Hub, the drifted
    // note in view N is the orphan (linked to nothing the arrangements
    // touch).
    private const string Orphan = "orphan.md";
    private const string PinFolder = "notes/nested";
    private const string StaleFolder = "notes/stale";
    private const string MovedFolder = "notes/moved";
    private const string RenamedOrigin = "two-renamed.md";
    private const string RenamedNoteInView = "orphan-renamed.md";
    private const string RenamedPinName = "deep-renamed.md";
    private const string GraphTabPath = "graph:singleton";

    /// <summary>The pinned cardinalities (IPB-3, IPB-7; B2-8): the dimensions
    /// crossed with the route inventory; the cells the model names as not
    /// states of the system; the cells driven.</summary>
    private const int PinnedModes = 4;
    private const int PinnedRoutes = 35;
    private const int PinnedCells = PinnedModes * 2 * 2 * 3 * 5 * 3 * PinnedRoutes;
    private const int PinnedUnreachable = 17162;
    private const int PinnedDriven = 8038;

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
    /// neither duplicates nor splits (its commands' CanExecute). Its pane's
    /// closes ARE driven (IPB-15, IPB-25): onto the note beside it, and onto
    /// a canvas beside the note so the root stays none.</summary>
    private static readonly Route[] SingletonRefusedRoutes =
    [
        Route.TabActivateDuplicate,
        Route.TabCloseDuplicate,
        Route.Split,
    ];

    private static readonly Route[] GhostRoutes =
    [
        Route.GhostCreate,
        Route.GhostCreateFails,
        Route.GhostCreateAlreadyOpen,
        Route.GhostCreateDirtyRefusal,
        Route.GhostCreateSourceMoved,
    ];

    private static readonly Route[] OriginRoutes = [Route.RenameOrigin, Route.DeleteOrigin];

    /// <summary>The routes under which the leaf's effective root changes
    /// nothing but the vault moves on — a rename or a delete of a path the
    /// leaf does not hold as its root: the probe alone (Term 6). The folder
    /// routes are these only FOLLOWING (the pin lives in the folder).</summary>
    private static bool ProbeOnly(Cell cell) => cell.Route switch
    {
        Route.RenameOrigin or Route.DeleteOrigin or Route.RenameNoteInView => true,
        Route.RenameFolder or Route.DeleteFolder => !cell.Pinned,
        _ => false,
    };

    /// <summary>Why a cell is not a state of the system — the model must
    /// name the reason, never silently skip.</summary>
    private static string? Unreachable(Cell cell)
    {
        if (cell.Pinned && cell.Route == Route.Launch)
        {
            return "nothing persists (B2D-2): a launch comes up FOLLOWING, so no pinned arrangement survives it";
        }
        if (cell.Mode is Mode.PinnedFresh or Mode.PinnedNoOrigin && cell.Root != RootState.Note)
        {
            return "the arrangement's note in view is the pin (IGJ-12): its tab is the one in view";
        }
        if (OriginRoutes.Contains(cell.Route) && cell.Mode is Mode.Following or Mode.PinnedNoOrigin)
        {
            return "no entry names an origin: the stack is empty";
        }
        if (cell.Route == Route.RenameNoteInView && cell.Mode != Mode.PinnedDrifted)
        {
            return "the note in view is the root (FOLLOWING) or the pin itself: that is RenameRoot's route";
        }
        if (cell.Route == Route.RenameNoteInView && cell.Root != RootState.Note)
        {
            return "no note in view to rename";
        }
        bool noRoot = !cell.HasRoot;
        if (noRoot && (cell.Presentation != Presentation.Current || cell.InFlight))
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
        if (SecondTabRoutes.Contains(cell.Route) && cell.Root == RootState.None)
        {
            return "the route needs a tab beside the one in view; with no tab at all there is none";
        }
        if (SingletonRefusedRoutes.Contains(cell.Route) && cell.Root == RootState.NoneBesideNote)
        {
            return "the tab in view is the graph's singleton, which neither duplicates nor splits (its commands' CanExecute refuse it)";
        }
        if (GhostRoutes.Contains(cell.Route) && (noRoot || cell.Presentation != Presentation.Current))
        {
            return "a ghost row is activated from a RENDERED tree; a leaf with no root, a stale, an Error, a Missing or a Loading one shows none";
        }
        if (cell.Route == Route.GhostCreateAlreadyOpen && cell.Root == RootState.None)
        {
            return "the already-open note's tab would be the note in view, and the cell has no tab at all";
        }
        if (cell.Route == Route.GhostCreateDirtyRefusal && cell.Root != RootState.Note)
        {
            return "the refusal needs a dirty note tab in view";
        }
        if (cell.Route == Route.Launch && (cell.Presentation is Presentation.Stale or Presentation.Loading or Presentation.Error || cell.InFlight))
        {
            return "a launch restores a root, never a presentation or a load in flight (a missing root IS the root's own state, restored)";
        }
        if (cell.Route == Route.Launch && cell.Pane == Pane.Collapsed)
        {
            return "the pane's visibility is not persisted: a launch comes up MOUNTED";
        }
        if (cell.Route == Route.RenameRoot && cell.Presentation == Presentation.Missing)
        {
            return "a root missing from disk cannot be renamed there: core refuses the rename";
        }
        return null;
    }

    // --- The arranged state --------------------------------------------------------------

    /// <summary>The pin the arrangement leaves: a missing path for the
    /// Missing presentation; under the Stale presentation the pin's folder
    /// was moved while the leaf was inactive, so the pin is under it.</summary>
    private static string PinBefore(Cell cell) => cell.Presentation switch
    {
        Presentation.Missing => MissingRoot,
        Presentation.Stale => StaleFolder + "/deep.md",
        _ => Deep,
    };

    private static string PinFolderBefore(Cell cell) =>
        cell.Presentation == Presentation.Stale ? StaleFolder : PinFolder;

    private static string RenamedPin(Cell cell) => PinFolderBefore(cell) + "/" + RenamedPinName;

    private static string MovedPin() => MovedFolder + "/deep.md";

    /// <summary>The note the arrangement opens as the root FOLLOWING (a
    /// missing one for the Missing presentation), or the pin.</summary>
    private static string ArrangedNote(Cell cell) =>
        cell.Pinned ? PinBefore(cell) : cell.Presentation == Presentation.Missing ? MissingRoot : Hub;

    /// <summary>The effective root the arrangement leaves: the pin; or,
    /// FOLLOWING, the note in view (the note, a missing one, or — for the
    /// split's sole-tab close, whose arrangement opens Two in the split's
    /// group — Two).</summary>
    private static string? RootBefore(Cell cell) => cell.Pinned
        ? PinBefore(cell)
        : cell.Root switch
        {
            RootState.Note when cell.Route == Route.SplitSoleTabClose => Two,
            RootState.Note => ArrangedNote(cell),
            _ => null,
        };

    /// <summary>The note in view the arrangement leaves (Term 11): FOLLOWING,
    /// the root; PinnedFresh and PinnedNoOrigin, the pin; PinnedDrifted, the
    /// orphan, or none.</summary>
    private static string? NoteInViewBefore(Cell cell) => cell.Mode switch
    {
        Mode.Following => RootBefore(cell),
        Mode.PinnedDrifted => cell.Root == RootState.Note ? Orphan : null,
        _ => PinBefore(cell),
    };

    /// <summary>IGJ-12's stacks: the prior mode and the effective root at
    /// each push, oldest first.</summary>
    private static (string? Pin, string Effective)[] StackBefore(Cell cell) => cell.Mode switch
    {
        Mode.PinnedFresh => [(null, Two)],
        Mode.PinnedDrifted => [(null, Two), (Hub, Hub)],
        _ => [],
    };

    /// <summary>The depth the arrangement leaves: three at the bound's
    /// route; two where the load left in flight is Deeper's audible reload
    /// over a tree or an Error; one otherwise (the probe's silent reload
    /// keeps the depth; the root's first load is at one).</summary>
    private static uint DepthBefore(Cell cell) =>
        cell.Route == Route.DeeperAtBound ? 3u
        : cell.Pending == Pending.Audible && cell.Presentation != Presentation.Loading ? 2u
        : 1u;

    /// <summary>The state the arrangement leaves the publication in: NoNote
    /// over a root for a stale presentation (the note opened while the leaf
    /// was inactive — a root the presentation is not for, Term 7), Error
    /// for a missing root, else the presentation's own state. Under a pin
    /// the stale presentation is the tree held for the pin's path BEFORE
    /// its folder moved (Term 16 runs Term 3(d)'s transition, which keeps
    /// the publication): Ready, and stale.</summary>
    private static ConnectionsLoadState StateBefore(Cell cell)
    {
        if (cell.Pinned)
        {
            return cell.Presentation switch
            {
                Presentation.Missing => ConnectionsLoadState.Error,
                Presentation.Error => ConnectionsLoadState.Error,
                Presentation.Loading => ConnectionsLoadState.Loading,
                _ => ConnectionsLoadState.Ready,
            };
        }
        return cell.Root switch
        {
            RootState.Note when cell.Presentation == Presentation.Stale => ConnectionsLoadState.NoNote,
            RootState.Note when RootBefore(cell) == MissingRoot => ConnectionsLoadState.Error,
            // The split's sole-tab close arranges the presentation over Two (the
            // tab it closes); a MISSING root is the survivor's, so Two is Ready.
            RootState.Note when cell.Route == Route.SplitSoleTabClose => cell.Presentation switch
            {
                Presentation.Loading => ConnectionsLoadState.Loading,
                Presentation.Error => ConnectionsLoadState.Error,
                _ => ConnectionsLoadState.Ready,
            },
            RootState.Note => cell.Presentation switch
            {
                Presentation.Error => ConnectionsLoadState.Error,
                Presentation.Loading => ConnectionsLoadState.Loading,
                _ => ConnectionsLoadState.Ready,
            },
            _ => ConnectionsLoadState.NoNote,
        };
    }

    // --- The derivation --------------------------------------------------------------------

    private static Derivation Derive(Cell cell, Fixture fixture)
    {
        bool mounted = cell.Pane == Pane.Visible;
        bool active = cell.Leaf == LeafState.Connections;
        bool pinned = cell.Pinned;
        bool hasRoot = cell.HasRoot;
        // A note's tab is the one in view: an open lands IN PLACE (no
        // `TabFocused`); from no tab or the graph tab it creates or activates
        // one. FOLLOWING this is having a root; under a pin it is the note
        // in view's state.
        bool noteTab = cell.Root == RootState.Note;
        bool stale = cell.Presentation == Presentation.Stale;
        string? rootBefore = RootBefore(cell);
        // Term 2 and B-D3: SPEAKING is ACTIVE at apply — the leaf is the
        // active leaf when the body runs — and MOUNTED is not consulted; a
        // hide alone leaves an in-flight completion audible, a switch away
        // silences it (IGH-14). A completion left in flight that no route
        // supersedes or makes foreign applies after the route's synchronous
        // part and speaks the line core reported for the tree it fetched
        // BEFORE the route — iff its TOKEN carries the audible policy (Term
        // 5): the probe's silent reload says nothing whatever the route.
        // Under a pin the load in flight is the PIN's, and a note change
        // neither supersedes nor forecloses it (Term 11).
        string[] pending = cell.Pending == Pending.Audible && active ? [LineBeforePlaceholder] : [];
        bool? restStale = hasRoot ? stale : null;
        // The probe after a vault move the leaf's root took no part in (Term
        // 6): the held tree is older, or Error, or Loading — one silent load;
        // with a load in flight, the mark — whose completion, a tree,
        // reloads once after it, and a failure (over a missing root) leaves
        // the mark waiting.
        int probeLoads = cell.InFlight && cell.Presentation == Presentation.Missing ? 0 : 1;
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
                    // Term 3(b): a mounted switch loads only when STALE — an Error,
                    // a Missing or a Loading that is current does not reload on a
                    // switch; a load in flight is a current request. The in-flight
                    // completion speaks iff the user switched back before it
                    // applied (IGH-14).
                    int loads = mounted && hasRoot && stale ? 1 : 0;
                    string[] spoken = loads == 1 ? [LinePlaceholder] : cell.Pending == Pending.Audible ? [LineBeforePlaceholder] : [];
                    return new(["LeafPanelShown", .. spoken], loads, rootBefore, hasRoot ? stale && loads == 0 : null);
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
                    return new([.. timeline], hasRoot ? 1 : 0, rootBefore, hasRoot ? false : null, Reload: false);
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
                    return new(["RightPaneShown", .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, rootBefore, hasRoot ? stale && loads == 0 : null, Reload: loads == 0);
                }
            case Route.RootChange:
                if (pinned)
                {
                    // Term 11: the note in view moves to Two and is RECORDED —
                    // no epoch, no load, the pin's presentation and its load in
                    // flight kept; the shell's `TabFocused` when the open
                    // creates or activates a tab.
                    return new([.. (noteTab ? Array.Empty<string>() : ["TabFocused"]), .. pending], 0, rootBefore, restStale);
                }
                {
                    // Term 3(d): a load only while ACTIVE and MOUNTED, else STALE;
                    // an in-flight result for the old root is foreign. The
                    // shell's `TabFocused` when the open creates the first tab or
                    // activates the note's existing tab (an in-place open into an
                    // existing tab posts none).
                    int loads = active && mounted ? 1 : 0;
                    return new([.. (hasRoot ? Array.Empty<string>() : ["TabFocused"]), .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, Two, loads == 0, Reload: false);
                }
            case Route.DepthChange:
                {
                    // Term 3(e): a load with a root (superseding a load in
                    // flight); spoken iff SPEAKING at apply, installed either way.
                    int loads = hasRoot ? 1 : 0;
                    return new(loads == 1 && active ? [LinePlaceholder] : [], loads, rootBefore, hasRoot ? false : null, Reload: loads == 0);
                }
            case Route.DeeperAtBound:
                // Deeper at the maximum is core's clamp returning the same
                // depth: no write, no load (B-14, B-19 v).
                return new(pending, 0, rootBefore, restStale);
            case Route.RootToNone:
                if (cell.Root == RootState.None)
                {
                    // Nothing to close; under a pin the load in flight applies.
                    return new(pinned ? pending : [], 0, rootBefore, restStale);
                }
                if (pinned)
                {
                    // Term 11: the tab's close changes the note in view (to the
                    // note beside the graph, or to none); the pin stands, nothing
                    // loads, the load in flight applies.
                    return cell.Root == RootState.NoneBesideNote
                        ? new(["TabFocused", "TabClosed", .. pending], 0, rootBefore, restStale)
                        : new(["TabClosed", .. pending], 0, rootBefore, restStale);
                }
                if (cell.Root == RootState.NoneBesideNote)
                {
                    // The close of the graph tab: its successor is the note beside
                    // it — a root change from none (Term 3(d)).
                    int loads = active && mounted ? 1 : 0;
                    return new(["TabFocused", "TabClosed", .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, Two, loads == 0);
                }
                // Term 3(g): NoNote synchronously, the in-flight result dropped.
                return new(["TabClosed"], 0, null, null, Reload: false);
            case Route.NonNoteActivation:
                // The graph tab opened (PR A's own lines through the same relay:
                // Opened, then its snapshot's summary — Opened alone when the
                // tab is already effective and READY); NoNote for the leaf,
                // nothing from it; the in-flight result dropped. Under a pin the
                // note in view becomes none and is recorded (Term 11): the pin's
                // load in flight applies after the graph's own lines (the graph
                // document is drained inside the drive).
                if (cell.Root == RootState.NoneBesideNote)
                {
                    return pinned ? new([opened, .. pending], 0, rootBefore, restStale) : new([opened], 0, null, null);
                }
                // The summary counts the vault the arrangement left: one more
                // note, an orphan, where the probe's reload needed it moved.
                {
                    string summary = VaultBumped(cell) ? fixture.GraphSummaryAfterBump : fixture.GraphSummary;
                    return pinned
                        ? new(["TabFocused", opened, summary, .. pending], 0, rootBefore, restStale)
                        : new(["TabFocused", opened, summary], 0, null, null, Reload: false);
                }
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
                    return new([.. timeline], 0, rootBefore, hasRoot ? stale : null);
                }
            case Route.ShowBacklinks:
                {
                    // Bases' Show backlinks (IPB-2): ONE outer mutation — the open,
                    // the switch to Backlinks, the reveal, the consume — so the
                    // boundary reconciles the new root against the FINAL leaf:
                    // STALE, no load; an in-flight result is foreign. The Bases
                    // line is the document's and is not driven here. Under a pin
                    // the note in view is recorded and the pin's load in flight
                    // applies — silently, the leaf no longer active at apply.
                    var timeline = new List<string>();
                    if (!noteTab)
                    {
                        timeline.Add("TabFocused");
                    }
                    timeline.Add("LeafPanelShown");
                    if (!mounted)
                    {
                        timeline.Add("RightPaneShown");
                    }
                    return pinned
                        ? new([.. timeline], 0, rootBefore, restStale)
                        : new([.. timeline], 0, Two, true, Reload: false);
                }
            case Route.FocusEnter:
                {
                    // Term 9: LoadingConnections while STALE or Loading — a same-root
                    // reload that kept its rows or its Error is neither — nothing
                    // for a ready tree with rows, for Error or for NoNote; then the
                    // in-flight completion speaks (the leaf is shown).
                    var timeline = new List<string>();
                    if (hasRoot && cell.Presentation == Presentation.Loading)
                    {
                        timeline.Add(LoadingLine());
                    }
                    timeline.AddRange(pending);
                    return new([.. timeline], 0, rootBefore, hasRoot ? false : null);
                }
            case Route.TabActivateOther:
                if (pinned)
                {
                    // Term 11: `TabFocused`; the note in view Two, recorded.
                    return new(["TabFocused", .. pending], 0, rootBefore, restStale);
                }
                {
                    // An existing tab activated: the shell's `TabFocused`; a root
                    // change — Term 3(d) — from a note or from the graph tab.
                    int loads = active && mounted ? 1 : 0;
                    return new(["TabFocused", .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, Two, loads == 0, Reload: false);
                }
            case Route.TabActivateDuplicate:
                // The same note's other tab: `TabFocused`; no root change.
                return new(["TabFocused", .. pending], 0, rootBefore, restStale);
            case Route.TabCloseSuccessor:
                if (pinned)
                {
                    return new(["TabFocused", "TabClosed", .. pending], 0, rootBefore, restStale);
                }
                {
                    // The close's successor is another note: the successor's
                    // `TabFocused` (the group's setter) then `TabClosed`; a root
                    // change — Term 3(d).
                    int loads = active && mounted ? 1 : 0;
                    return new(["TabFocused", "TabClosed", .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, Two, loads == 0, Reload: false);
                }
            case Route.TabCloseDuplicate:
                // The successor is the same note: its `TabFocused`, `TabClosed`;
                // no root change.
                return new(["TabFocused", "TabClosed", .. pending], 0, rootBefore, restStale);
            case Route.Split:
                // The duplicate's `TabFocused`; the root unchanged, no load.
                return new(["TabFocused", .. pending], 0, rootBefore, restStale);
            case Route.SplitSoleTabClose:
                if (pinned)
                {
                    // Term 11: the note in view becomes the survivor's (the same
                    // note, or the note beside the graph); the pin stands.
                    return new(["TabClosed", .. pending], 0, rootBefore, restStale);
                }
                {
                    // The split's sole tab closed: A → none → B inside ONE mutation
                    // (IGH-4), reconciled once at the boundary with the surviving
                    // group's note — the note before the split, or the note beside
                    // the graph — a root change (Term 3(d)); the in-flight result
                    // for the closed note foreign.
                    int loads = active && mounted ? 1 : 0;
                    string survivor = cell.Root == RootState.NoneBesideNote ? Two : ArrangedNote(cell);
                    return new(["TabClosed", .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, survivor, loads == 0, Reload: false);
                }
            case Route.GroupCloseSameRoot:
                // `EditorPaneFocused`; the surviving group shows the same note —
                // or, beside the graph, the canvas that keeps the root none.
                return new(["EditorPaneFocused", .. pending], 0, rootBefore, restStale);
            case Route.GroupCloseOtherRoot:
                if (pinned)
                {
                    return new(["EditorPaneFocused", .. pending], 0, rootBefore, restStale);
                }
                {
                    // `EditorPaneFocused`; the surviving group shows another note:
                    // a root change — Term 3(d).
                    int loads = active && mounted ? 1 : 0;
                    return new(["EditorPaneFocused", .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, Two, loads == 0, Reload: false);
                }
            case Route.GhostCreate:
                if (pinned)
                {
                    // B-11 under a pin (Term 11): the open of the created note is
                    // a note change, recorded — `TabFocused` only when there is no
                    // tab at all and one is created; from the graph tab it lands
                    // in place (A-9) — then NoteCreated; the pin's load in flight
                    // applies.
                    return new([.. (cell.Root == RootState.None ? ["TabFocused"] : Array.Empty<string>()), "Graph", .. pending], 0, rootBefore, restStale);
                }
                {
                    // B-11: the open is silent, then ONE NoteCreated (the shell's
                    // `A11yEvent.Graph`, A-8's direct post), then the root change's
                    // summary iff the load was issued (ACTIVE and MOUNTED) and
                    // SPEAKING; inactive or unmounted, the root moves STALE; a
                    // same-root reload in flight is foreign once the root moved.
                    int loads = active && mounted ? 1 : 0;
                    return new(["Graph", .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, fixture.GhostPath, loads == 0, Reload: false);
                }
            case Route.GhostCreateFails:
                // The failure is the relay's HIGH line (IPB-1); nothing else —
                // then the reload left in flight applies and speaks the tree it
                // fetched before the attempt, iff audible and active.
                return new([failed, .. pending], 0, rootBefore, false);
            case Route.GhostCreateAlreadyOpen:
                if (pinned)
                {
                    return new(["TabFocused", "Graph", .. pending], 0, rootBefore, restStale);
                }
                {
                    // The created note's path already has a tab: the open ACTIVATES
                    // it (`TabFocused`), then NoteCreated; a root change (Term 3(d)).
                    int loads = active && mounted ? 1 : 0;
                    return new(["TabFocused", "Graph", .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, fixture.GhostPath, loads == 0, Reload: false);
                }
            case Route.GhostCreateDirtyRefusal:
                // The dirty tab refuses the navigation: NoteCreated, no root
                // move, nothing more; then the reload left in flight applies.
                return new(["Graph", .. pending], 0, rootBefore, false);
            case Route.GhostCreateSourceMoved:
                if (pinned)
                {
                    // The note in view moves to Two while the create is parked
                    // (recorded; `TabFocused` when the open creates or activates
                    // a tab); the pin's load in flight is released and applies
                    // before the create lands; then NoteCreated, the open
                    // suppressed.
                    return new([.. (noteTab ? Array.Empty<string>() : ["TabFocused"]), .. pending, "Graph"], 0, rootBefore, restStale);
                }
                {
                    // The root moved while the create was parked (B-11): the root
                    // change's own load (Term 3(d)) — its line the tree fetched
                    // BEFORE the create landed, captured by the driver — then
                    // NoteCreated after the attempt, the open suppressed.
                    int loads = active && mounted ? 1 : 0;
                    return new([.. (loads == 1 ? [CapturedPlaceholder] : Array.Empty<string>()), "Graph"], loads, Two, loads == 0, Reload: false);
                }
            case Route.RenameRoot:
                if (!hasRoot)
                {
                    return new([], 0, null, null);
                }
                if (pinned)
                {
                    // Term 16, the rename hook over the PIN: Term 3(d)'s root move
                    // to the new path — ONE audible load while ACTIVE and MOUNTED,
                    // else STALE; then the probe (Term 6) as FOLLOWING. No outline
                    // line: the pin's note carries no heading. When the pin's own
                    // tab is the one in view, the hook's fetch — issued before
                    // `SyncPanels()` by Term 16's frozen order — races the panels'
                    // first read of the renamed note, which stages core's
                    // MetadataTouched and moves the generation (`graph.rs`,
                    // `apply_batch`): the tree lands stamped before or after it,
                    // and the probe's mark reloads it silently once when before.
                    // One load, or two — the pool's order, not the system's; no
                    // line either way (recorded for the owner, TGB2-5).
                    bool audible = active && mounted;
                    bool pinsTab = cell.Mode != Mode.PinnedDrifted;
                    return new(audible ? [LinePlaceholder] : [], 1, RenamedPin(cell), false, Reload: false, LoadsOr: audible && pinsTab ? 2 : null);
                }
                {
                    // The workspace's retarget re-derives the panels (the outline's
                    // count is the shell's line): a root change to the new path —
                    // ONE audible load while ACTIVE and MOUNTED, else STALE; then
                    // the probe (Term 6): a load in flight is marked only, a stale
                    // or Loading or Error leaf reloads silently once. One load
                    // either way; an in-flight result for the old root is foreign.
                    bool audible = active && mounted;
                    return new(["OutlineCount", .. (audible ? [LinePlaceholder] : Array.Empty<string>())], 1, RenamedRoot, false, Reload: false);
                }
            case Route.DeleteRoot:
            case Route.DeleteFolder when pinned && cell.Presentation != Presentation.Missing:
                if (!hasRoot)
                {
                    return new([], 0, null, null);
                }
                {
                    // The shell's missing-file line iff a tab shows the deleted path
                    // (under PinnedDrifted the pin has no tab); the tab and the root
                    // are KEPT — under a pin the delete hook prunes the entries
                    // under the path and keeps the pin (Term 16); the probe (Term
                    // 6): the held tree is older, or Error, or Loading — a silent
                    // load — or, with a load in flight, the mark, whose completion
                    // speaks what it fetched before the delete (iff audible and
                    // active): a tree's summary, reloaded silently once after it
                    // (the mark above its generation); a failure, after which the
                    // mark waits for the next install. Nothing else spoken; the
                    // root gone, the presentation Error.
                    string[] missing = pinned && cell.Mode == Mode.PinnedDrifted ? [] : ["HostComposed"];
                    return new([.. missing, .. pending], probeLoads, rootBefore, false, State: ConnectionsLoadState.Error);
                }
            case Route.RenameFolder when pinned && cell.Presentation != Presentation.Missing:
                {
                    // Term 16 over the pin's FOLDER: the pin moves under the new
                    // folder — Term 3(d)'s root move, one audible load while ACTIVE
                    // and MOUNTED, else STALE — and the probe (one load either way;
                    // the pool's order may make it two, see RenameRoot).
                    bool audible = active && mounted;
                    bool pinsTab = cell.Mode != Mode.PinnedDrifted;
                    return new(audible ? [LinePlaceholder] : [], 1, MovedPin(), false, Reload: false, LoadsOr: audible && pinsTab ? 2 : null);
                }
            case Route.RenameOrigin:
            case Route.DeleteOrigin:
            case Route.RenameNoteInView:
            case Route.RenameFolder:
            case Route.DeleteFolder:
                {
                    // The probe alone (Term 6): the leaf's root took no part in the
                    // move — the entry, the note in view, or a folder the root is
                    // not in — so no root transition, no audible line; the
                    // shell's missing-file line iff a tab shows the deleted origin
                    // (only beside the graph is the origin's tab open).
                    string[] missing = cell.Route == Route.DeleteOrigin && cell.Root == RootState.NoneBesideNote ? ["HostComposed"] : [];
                    return new([.. missing, .. pending], hasRoot ? probeLoads : 0, rootBefore, hasRoot ? false : null);
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
                    // With another leaf restored active, the restored root is
                    // recorded and nothing loads (Term 3(d)): NoNote over a
                    // root — STALE until (a), (b) or the probe.
                    int loads = active && hasRoot ? 1 : 0;
                    bool exists = hasRoot && cell.Presentation != Presentation.Missing;
                    return new([.. (exists ? ["OutlineCount"] : Array.Empty<string>()), .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, rootBefore, hasRoot ? loads == 0 : null, State: hasRoot && loads == 0 ? ConnectionsLoadState.NoNote : null);
                }
            case Route.Shutdown:
                // The leaf retires into the drain, the relay after it: nothing.
                return new([], 0, rootBefore, null);
            default:
                throw new InvalidOperationException($"the model derives no route {cell.Route}");
        }
    }

    /// <summary>Rule D's state after the route (B2-8): FOLLOWING — no pin,
    /// the note in view is the root, an empty stack, and nothing here
    /// writes the shared key (the table view's write is the VIEW's, absent
    /// from a view-model workspace); under a pin — the pin, moved by the
    /// rename hooks; the note in view the route left (Term 11); the stack,
    /// rewritten by the origin's rename and pruned by its delete (Term 16);
    /// the key core's stable key of the pin (Term 15) — unless the graph
    /// document revalidated it against a snapshot that lacks the node: a
    /// MISSING pin, once a graph tab publishes (A-7).</summary>
    private static RootMode RootModeAfter(Cell cell, Fixture fixture, Derivation expected)
    {
        if (!cell.Pinned)
        {
            return new(null, expected.Root, [], null);
        }
        // A missing pin lives in no folder: the folder routes leave it.
        bool pinInTheFolder = cell.Presentation != Presentation.Missing;
        string pin = cell.Route switch
        {
            Route.RenameRoot => RenamedPin(cell),
            Route.RenameFolder when pinInTheFolder => MovedPin(),
            _ => PinBefore(cell),
        };
        string? before = cell.Mode == Mode.PinnedDrifted ? NoteInViewBefore(cell) : pin;
        // Under a pin the parked create's open is NOT suppressed — the root
        // it compares is the pin, which did not move (B-11) — so the created
        // note opens in place after the note in view moved to Two.
        string? noteInView = cell.Route switch
        {
            Route.RootChange or Route.TabActivateOther or Route.TabCloseSuccessor
                or Route.GroupCloseOtherRoot or Route.ShowBacklinks => Two,
            Route.RootToNone => cell.Root == RootState.NoneBesideNote ? Two : null,
            Route.NonNoteActivation => null,
            Route.GhostCreate or Route.GhostCreateAlreadyOpen or Route.GhostCreateSourceMoved => fixture.PinnedGhostPath,
            Route.SplitSoleTabClose => cell.Root == RootState.NoneBesideNote ? Two : before,
            Route.RenameNoteInView => RenamedNoteInView,
            _ => before,
        };
        var stack = StackBefore(cell).ToList();
        if (cell.Route == Route.RenameOrigin)
        {
            stack[0] = (null, RenamedOrigin);
        }
        if (cell.Route == Route.DeleteOrigin)
        {
            stack.RemoveAt(0);
        }
        // A-7: a graph document that publishes clears a key whose node the
        // snapshot no longer carries — a missing pin, or a pin the route
        // deleted (the document's own probe republishes after the delete).
        bool graphPublished = cell.Root == RootState.NoneBesideNote || cell.Route == Route.NonNoteActivation;
        bool pinGone = cell.Route == Route.DeleteRoot || (cell.Route == Route.DeleteFolder && pinInTheFolder);
        string? key = graphPublished && (cell.Presentation == Presentation.Missing || pinGone) ? null : StableKey(pin);
        return new(pin, noteInView, [.. stack], key);
    }

    /// <summary>The last focus request the route raises (IGL-3): the
    /// reveal-then-switch commands ask for the right pane's boundary; an
    /// open, a split, a duplicate and the pane-close COMMAND ask for the
    /// editor (`RequestActiveEditorFocus`); a tab activation, a tab's
    /// close — the sole tab's included, whose group collapses without the
    /// command — a leaf switch, a depth change, the hooks and the probe ask
    /// for nothing.</summary>
    private static string? FocusAfter(Cell cell) => cell.Route switch
    {
        Route.Show or Route.TasksReview or Route.HistoryCommand => "RightPane",
        Route.RootChange or Route.ShowBacklinks or Route.NonNoteActivation
            or Route.GhostCreate or Route.GhostCreateAlreadyOpen or Route.GhostCreateSourceMoved
            or Route.Split or Route.GroupCloseSameRoot or Route.GroupCloseOtherRoot => "editor",
        _ => null,
    };

    /// <summary>The presentation's state after the route: NoNote without a
    /// root; a load's result — the root's Ready, a missing root's Error —
    /// when the route loaded or the load left in flight applied; else the
    /// state the arrangement left, which a root change without a load keeps
    /// STALE.</summary>
    private static ConnectionsLoadState StateAfter(Cell cell, Derivation expected)
    {
        if (expected.State is { } explicitly)
        {
            return explicitly;
        }
        if (expected.Root is null)
        {
            return ConnectionsLoadState.NoNote;
        }
        if (expected.Loads > 0 || (cell.InFlight && expected.Reload))
        {
            return expected.Root == MissingRoot ? ConnectionsLoadState.Error : ConnectionsLoadState.Ready;
        }
        return StateBefore(cell);
    }

    // --- The arrangement -------------------------------------------------------------------

    private static WorkspaceTabViewModel TabFor(Host host, string path, bool inactiveOne = false)
    {
        WorkspaceTabViewModel[] tabs = [.. host.Workspace.ActiveGroup.Tabs.Where(tab => string.Equals(tab.Path, path, StringComparison.Ordinal))];
        Assert.True(tabs.Length > 0, $"no tab of the active group shows {path}");
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

    /// <summary>Every document the route may touch, drained: the leaf's, the
    /// graph tab's (PR A's lines ride the same relay), every canvas tab's.</summary>
    private static void SettleTheDocuments(Host host)
    {
        host.Settle();
        SettleTheGraph(host);
    }

    /// <summary>The graph tab's document and every canvas's, drained — NOT
    /// the leaf's, whose fetch may be parked (B2-8's pinned arrangements
    /// open the graph after the pin).</summary>
    private static void SettleTheGraph(Host host)
    {
        if (host.Workspace.GraphDocument is { IsRetired: false } graph)
        {
            PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
        }
        foreach (WorkspaceTabViewModel tab in host.Workspace.Groups.SelectMany(group => group.Tabs))
        {
            if (tab.Canvas is { } canvas)
            {
                PumpedDispatcher.PumpUntilDrained(canvas.WhenAllWorkDrained());
            }
        }
        PumpedDispatcher.Drain();
    }

    /// <summary>Whether the arrangement moves the vault on: the probe's
    /// silent reload over a Ready tree needs an older generation (Term 6);
    /// over an Error, a Missing or a Loading it reloads as it stands.</summary>
    private static bool VaultBumped(Cell cell) =>
        cell.Pending == Pending.Silent && StateBefore(cell) == ConnectionsLoadState.Ready;

    /// <summary>The vault moved on: a new note, scanned.</summary>
    private static void BumpTheVault(Host host, int stamp)
    {
        WriteTheBump(host.Root, $"bump-{stamp}.md");
        using var cancel = new CancelToken();
        host.Session.ScanInitial(cancel);
    }

    /// <summary>The note that moves the generation: no links, so the graph
    /// gains one note and one orphan.</summary>
    private static void WriteTheBump(string vaultRoot, string name) =>
        File.WriteAllText(Path.Combine(vaultRoot, name), "a note that moved the generation\n");

    /// <summary>A transient failure for the leaf's NEXT fetches.</summary>
    private static void FailTheNextFetches(Host host, int times)
    {
        int remaining = times;
        host.Leaf.FetchGateForTests = () =>
        {
            if (remaining > 0)
            {
                remaining--;
                throw new InvalidOperationException(InjectedTransientFailure);
            }
        };
    }

    /// <summary>The root's NEXT envelope rejected at the receiver (the
    /// Loading presentation with nothing in flight): the tree's echo names
    /// another path. Bound to the root being arranged, so an envelope for
    /// another root — foreign to the receiver anyway — cannot spend the one
    /// shot.</summary>
    private static void RejectTheNextEnvelope(Host host, string root)
    {
        bool armed = true;
        host.Leaf.EnvelopeForTests = envelope =>
        {
            if (!armed
                || envelope is not { Token.Request.Root: { } envelopeRoot }
                || !string.Equals(envelopeRoot, root, StringComparison.Ordinal))
            {
                return envelope;
            }
            armed = false;
            return envelope with { TreePath = "rejected.md" };
        };
    }

    /// <summary>Arrange the workspace in the cell's state, from a fresh
    /// vault: the leaf activated and the note opened while ACTIVE and
    /// MOUNTED for a current, an Error, a Missing or a Loading presentation
    /// (then moved away from or hidden), the note opened while inactive for
    /// a stale one; the load left in flight PARKED after its crossings —
    /// the root's first load over Loading, Deeper's reload over a tree or an
    /// Error (Shallower first at the bound), the probe's silent reload over
    /// a moved vault, an Error, a Missing or a Loading — until the route's
    /// synchronous part has run; a Loading with nothing in flight through a
    /// REJECTED first-load envelope; the route's own props before or after —
    /// a tab beside, a split, the depth at its bound, a creator, a dirty
    /// tab, a canvas. Asserts the arranged state. Returns the parked
    /// fetch. Under a pin, <see cref="ArrangePinned"/>.</summary>
    private static ParkedFetch? Arrange(Host host, Cell cell, Fixture fixture, int stamp)
    {
        host.Workspace.ActiveLeaf = Host.OutlineLeaf;
        bool rootInNewTab = false;
        switch (cell.Route)
        {
            case Route.TabActivateOther:
            case Route.TabCloseSuccessor:
                if (cell.Root == RootState.Note && !cell.Pinned)
                {
                    host.OpenNote(Two);
                    rootInNewTab = true;
                }
                break;
            case Route.GroupCloseOtherRoot:
                if (cell.Root == RootState.Note && !cell.Pinned)
                {
                    host.OpenNote(Two);
                    host.Workspace.SplitRightCommand.Execute(null);
                }
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
        if (cell.Pinned)
        {
            return ArrangePinned(host, cell, fixture, stamp);
        }

        string root = ArrangedNote(cell);
        ParkedFetch? parked = null;
        bool park = cell.InFlight && cell.Route != Route.Shutdown;

        // Park the NEXT fetch after its crossings; the wait, after the load
        // is issued, makes sure it HAS crossed — the tree it holds is the
        // one before the route.
        void Arm()
        {
            if (park)
            {
                parked = Park(host.Leaf);
            }
        }

        void WaitParked()
        {
            if (parked is not null)
            {
                Assert.True(parked.Reached.Wait(TimeSpan.FromSeconds(10)), $"{cell}: the fetch never parked");
            }
        }

        void OpenThe(string path, bool newTab)
        {
            host.Workspace.OpenPath(path, newTab ? WorkspaceOpenTarget.NewTab : WorkspaceOpenTarget.CurrentTab);
        }

        // The root in view, at rest or with its first load in flight: the
        // presentation's own arrangement over an ACTIVE and MOUNTED leaf.
        void ArrangeTheRoot(string path, bool newTab)
        {
            switch (cell.Presentation)
            {
                case Presentation.Error:
                    // A transient failure for the NEXT fetches — two at the bound
                    // with an audible reload to arrange, since the Shallower that
                    // precedes the parked Deeper would otherwise heal it.
                    FailTheNextFetches(host, cell.Route == Route.DeeperAtBound && cell.Pending == Pending.Audible ? 2 : 1);
                    OpenThe(path, newTab);
                    host.Settle();
                    break;
                case Presentation.Loading:
                    if (cell.Pending == Pending.Audible)
                    {
                        // The root's FIRST load, parked.
                        Arm();
                        OpenThe(path, newTab);
                        WaitParked();
                        return;
                    }
                    RejectTheNextEnvelope(host, path);
                    OpenThe(path, newTab);
                    host.Settle();
                    host.Leaf.EnvelopeForTests = null;
                    break;
                default:
                    OpenThe(path, newTab);
                    host.Settle();
                    break;
            }
        }

        // The same-root reload left in flight over a tree, an Error or a
        // rejected Loading: Deeper's audible one (Shallower first at the
        // bound), or the probe's silent one (over a moved vault, an Error, a
        // Missing or a Loading).
        void ArrangeTheReload()
        {
            if (cell.Pending == Pending.None || cell.Presentation == Presentation.Loading && cell.Pending == Pending.Audible)
            {
                return;
            }
            host.Settle();
            if (cell.Pending == Pending.Audible)
            {
                if (cell.Route == Route.DeeperAtBound)
                {
                    host.Workspace.ConnectionsShallowerCommand.Execute(null);
                    host.Settle();
                }
                Arm();
                host.Workspace.ConnectionsDeeperCommand.Execute(null);
                WaitParked();
                return;
            }
            if (VaultBumped(cell))
            {
                // A Ready tree reloads only when the generation moved.
                BumpTheVault(host, stamp);
            }
            Arm();
            host.Workspace.NotifyGraphOfVaultChange();
            Assert.True(
                SpinWait.SpinUntil(() =>
                {
                    PumpedDispatcher.Drain();
                    return host.Leaf.InFlight;
                }, TimeSpan.FromSeconds(10)),
                $"{cell}: the probe issued no reload");
            WaitParked();
            // The graph document's own silent refresh, which the same probe
            // issued, is the ARRANGEMENT's: drained here (the leaf's fetch
            // stays parked), so a refresh still in flight cannot revalidate
            // the shared key against a snapshot older than the route's move
            // (CI, 35ed323: a pinned rename beside the graph tab lost its key
            // to the pre-rename snapshot once, in Release).
            SettleTheGraph(host);
        }

        // The route's props after the note.
        void AfterTheNote()
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
                case Route.GhostCreateAlreadyOpen:
                    host.Workspace.OpenPath(fixture.GhostPath, WorkspaceOpenTarget.NewTab);
                    host.Settle();
                    host.Workspace.ActiveGroup.ActiveTab = TabFor(host, root);
                    host.Settle();
                    break;
                case Route.GhostCreateDirtyRefusal:
                    host.Workspace.ActiveGroup.ActiveTab!.Text = "# hub, edited and unsaved\n";
                    Assert.True(host.Workspace.ActiveGroup.ActiveTab!.IsDirty, "the tab did not become dirty");
                    break;
            }
        }

        if (cell.Root == RootState.None || cell.Root == RootState.NoneBesideNote)
        {
            if (cell.Root == RootState.NoneBesideNote)
            {
                host.OpenNote(Two);
                bool splitPane = cell.Route is Route.SplitSoleTabClose or Route.GroupCloseOtherRoot or Route.GroupCloseSameRoot;
                if (cell.Route == Route.GroupCloseSameRoot)
                {
                    // A canvas beside the note (IPB-25): the pane close lands on
                    // it, and the root stays none.
                    File.WriteAllText(Path.Combine(host.Root, Board), "{\"nodes\":[],\"edges\":[]}\n");
                    using (var cancel = new CancelToken())
                    {
                        host.Session.ScanInitial(cancel);
                    }
                    host.Workspace.OpenPath(Board, WorkspaceOpenTarget.NewTab);
                    SettleTheDocuments(host);
                }
                if (splitPane)
                {
                    // The graph in a pane of its own beside the note's (IPB-15).
                    host.Workspace.SplitRightCommand.Execute(null);
                    SettleTheDocuments(host);
                }
                host.Workspace.OpenGraph();
                // The graph tab's own load is the ARRANGEMENT's: drained before
                // the timeline is cleared, so it cannot land inside the route.
                SettleTheDocuments(host);
                if (cell.Route == Route.SplitSoleTabClose)
                {
                    // The graph the SOLE tab of the split's pane.
                    host.Workspace.ActiveGroup.ActiveTab = TabFor(host, Two);
                    host.Workspace.CloseActiveTabCommand.Execute(null);
                    SettleTheDocuments(host);
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
            SettleTheDocuments(host);
            Assert.Null(host.Leaf.Root);
            return null;
        }
        if (cell.Presentation == Presentation.Stale)
        {
            OpenThe(root, rootInNewTab);
            AfterTheNote();
            if (cell.Route == Route.SplitSoleTabClose)
            {
                // The note before the split, the split, Two in the split's
                // group — opened while the leaf is inactive, so the root is
                // Two's and the presentation is not for it.
                host.Settle();
                host.Workspace.SplitRightCommand.Execute(null);
                host.Settle();
                OpenThe(Two, newTab: false);
            }
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
        // The activation's own load — Two's, for a route whose tab beside
        // was opened while the leaf was inactive (Term 3(b)) — LANDS before
        // any seam arms: CI (434d6bf) spent the one-shot rejection on that
        // foreign envelope, the root landed Ready and the probe rightly
        // issued nothing.
        host.Settle();
        if (cell.Route == Route.SplitSoleTabClose)
        {
            // The note before the split, at rest; the split; Two in the
            // split's group, in the cell's presentation, its load the one in
            // flight.
            OpenThe(root, false);
            host.Settle();
            host.Workspace.SplitRightCommand.Execute(null);
            host.Settle();
            ArrangeTheRoot(Two, newTab: false);
            ArrangeTheReload();
        }
        else
        {
            ArrangeTheRoot(root, rootInNewTab);
            AfterTheNote();
            ArrangeTheReload();
        }
        if (cell.Leaf == LeafState.Other)
        {
            host.Workspace.ActiveLeaf = Host.OutlineLeaf;
        }
        if (cell.Pane == Pane.Collapsed)
        {
            host.Workspace.IsRightPaneVisible = false;
        }
        AssertArranged(host, cell, parked);
        return parked;
    }

    /// <summary>The pinned arrangement (IGJ-12), over an ACTIVE and MOUNTED
    /// leaf: the origin (Two; then Hub as the prior pin for PinnedDrifted;
    /// the graph tab for PinnedNoOrigin), the PIN through the workspace's
    /// own funnel in the cell's presentation — a transient failure ahead of
    /// its load, its first load parked or its first envelope rejected, a
    /// missing path — its folder moved while the leaf is inactive for the
    /// stale one; then the note in view (the orphan in place; no tab; the
    /// graph tab beside Two) and the graph tab closed for PinnedNoOrigin;
    /// the route's props; Deeper's or the probe's reload parked; the leaf
    /// and the pane as the cell says. Nothing settles the leaf once a fetch
    /// is parked. Asserts the arranged state and the root mode.</summary>
    private static ParkedFetch? ArrangePinned(Host host, Cell cell, Fixture fixture, int stamp)
    {
        string pinnedPath = cell.Presentation == Presentation.Missing ? MissingRoot : Deep;
        string pin = PinBefore(cell);
        string? noteInView = NoteInViewBefore(cell);
        ParkedFetch? parked = null;

        void Arm() => parked = Park(host.Leaf);

        void WaitParked()
        {
            Assert.True(parked!.Reached.Wait(TimeSpan.FromSeconds(10)), $"{cell}: the fetch never parked");
        }

        // The leaf's drain, or — with a fetch parked — the dispatcher's alone.
        void Rest()
        {
            if (parked is null)
            {
                host.Settle();
            }
            PumpedDispatcher.Drain();
        }

        void OpenThe(string path, bool newTab)
        {
            host.Workspace.OpenPath(path, newTab ? WorkspaceOpenTarget.NewTab : WorkspaceOpenTarget.CurrentTab);
            Rest();
        }

        void CloseTheTab(string path)
        {
            host.Workspace.ActiveGroup.ActiveTab = TabFor(host, path);
            host.Workspace.CloseActiveTabCommand.Execute(null);
            Rest();
        }

        void PinThe(string path)
        {
            switch (cell.Presentation)
            {
                case Presentation.Error:
                    FailTheNextFetches(host, cell.Route == Route.DeeperAtBound && cell.Pending == Pending.Audible ? 2 : 1);
                    Assert.True(host.Workspace.ReRootConnectionsOn(path), $"{cell}: the funnel refused the pin");
                    host.Settle();
                    break;
                case Presentation.Loading:
                    if (cell.Pending == Pending.Audible)
                    {
                        // The pin's FIRST load, parked.
                        Arm();
                        Assert.True(host.Workspace.ReRootConnectionsOn(path), $"{cell}: the funnel refused the pin");
                        WaitParked();
                        return;
                    }
                    RejectTheNextEnvelope(host, path);
                    Assert.True(host.Workspace.ReRootConnectionsOn(path), $"{cell}: the funnel refused the pin");
                    host.Settle();
                    host.Leaf.EnvelopeForTests = null;
                    break;
                default:
                    Assert.True(host.Workspace.ReRootConnectionsOn(path), $"{cell}: the funnel refused the pin");
                    host.Settle();
                    break;
            }
        }

        host.ActivateLeaf();
        host.Settle();
        if (cell.Route == Route.GroupCloseOtherRoot)
        {
            // Two, then the split whose group the arrangement fills: the pane
            // close lands on Two.
            OpenThe(Two, newTab: false);
            host.Workspace.SplitRightCommand.Execute(null);
            host.Settle();
        }

        // The origins.
        switch (cell.Mode)
        {
            case Mode.PinnedFresh:
                OpenThe(Two, newTab: false);
                break;
            case Mode.PinnedDrifted:
                OpenThe(Two, newTab: false);
                Assert.True(host.Workspace.ReRootConnectionsOn(Hub), $"{cell}: the funnel refused the prior pin");
                host.Settle();
                break;
            case Mode.PinnedNoOrigin:
                host.Workspace.OpenGraph();
                SettleTheDocuments(host);
                break;
        }

        // The pin, in the cell's presentation.
        PinThe(pinnedPath);
        if (cell.Mode == Mode.PinnedNoOrigin)
        {
            // The pin's open replaced the graph's tab IN PLACE (contract A-9):
            // the pin's tab alone, the graph document retired, the stack empty.
            SettleTheGraph(host);
            Assert.DoesNotContain(host.Workspace.ActiveGroup.Tabs, tab => tab.IsGraph);
        }
        if (cell.Presentation == Presentation.Stale)
        {
            // The pin's folder moved while the leaf is inactive: Term 16 runs
            // Term 3(d)'s transition without a load — the tree held is for
            // the old path, the pin the new one.
            host.Workspace.ActiveLeaf = Host.OutlineLeaf;
            host.Settle();
            host.Session.RenameFolder(PinFolder, "stale");
            host.Workspace.RetargetPath(PinFolder, StaleFolder);
            host.Settle();
            Assert.True(host.Leaf.IsStale, $"{cell}: the folder move did not leave the pin stale");
        }

        // The note in view.
        if (cell.Mode == Mode.PinnedDrifted)
        {
            switch (cell.Root)
            {
                case RootState.Note:
                    OpenThe(Orphan, newTab: false);
                    break;
                case RootState.None:
                    CloseTheTab(pin);
                    break;
                case RootState.NoneBesideNote:
                    // Two beside, the pin's tab gone, then the graph tab in view —
                    // in a pane of its own for the split's sole-tab close and the
                    // pane closes (IPB-15), with a canvas beside Two for the
                    // same-root pane close (IPB-25).
                    OpenThe(Two, newTab: true);
                    CloseTheTab(pin);
                    if (cell.Route == Route.GroupCloseSameRoot)
                    {
                        File.WriteAllText(Path.Combine(host.Root, Board), "{\"nodes\":[],\"edges\":[]}\n");
                        using (var cancel = new CancelToken())
                        {
                            host.Session.ScanInitial(cancel);
                        }
                        host.Workspace.OpenPath(Board, WorkspaceOpenTarget.NewTab);
                        SettleTheGraph(host);
                    }
                    if (cell.Route is Route.SplitSoleTabClose or Route.GroupCloseSameRoot)
                    {
                        host.Workspace.SplitRightCommand.Execute(null);
                        Rest();
                    }
                    host.Workspace.OpenGraph();
                    SettleTheGraph(host);
                    if (cell.Route == Route.SplitSoleTabClose)
                    {
                        CloseTheTab(Two);
                        Assert.True(
                            host.Workspace.ActiveGroup.Tabs.Count == 1 && host.Workspace.ActiveGroup.ActiveTab!.IsGraph,
                            $"{cell}: the split's pane does not hold the graph alone");
                    }
                    break;
            }
        }

        // The route's props: a tab beside, a split, a duplicate, an open ghost,
        // a dirty tab.
        switch (cell.Route)
        {
            case Route.TabActivateOther:
            case Route.TabCloseSuccessor:
                if (cell.Root == RootState.Note)
                {
                    OpenThe(Two, newTab: true);
                    host.Workspace.ActiveGroup.ActiveTab = TabFor(host, noteInView!);
                    Rest();
                }
                break;
            case Route.TabActivateDuplicate:
            case Route.TabCloseDuplicate:
                host.Workspace.DuplicateTabCommand.Execute(null);
                Rest();
                Assert.Equal(2, host.Workspace.ActiveGroup.Tabs.Count(tab => string.Equals(tab.Path, noteInView, StringComparison.Ordinal)));
                break;
            case Route.SplitSoleTabClose:
                if (cell.Root == RootState.Note)
                {
                    host.Workspace.SplitRightCommand.Execute(null);
                    Rest();
                }
                break;
            case Route.GroupCloseSameRoot:
                if (cell.Root == RootState.Note)
                {
                    host.Workspace.SplitRightCommand.Execute(null);
                    Rest();
                }
                break;
            case Route.GhostCreateAlreadyOpen:
                OpenThe(fixture.PinnedGhostPath, newTab: true);
                host.Workspace.ActiveGroup.ActiveTab = TabFor(host, noteInView ?? GraphTabPath);
                Rest();
                // A re-activated graph tab speaks its activation summary
                // asynchronously: the ARRANGEMENT's line, drained here.
                SettleTheGraph(host);
                break;
            case Route.GhostCreateDirtyRefusal:
                host.Workspace.ActiveGroup.ActiveTab!.Text = "# edited and unsaved\n";
                Assert.True(host.Workspace.ActiveGroup.ActiveTab!.IsDirty, "the tab did not become dirty");
                break;
        }

        // The reload left in flight over the pin: Deeper's audible one
        // (Shallower first at the bound), or the probe's silent one.
        if (cell.Pending != Pending.None && !(cell.Presentation == Presentation.Loading && cell.Pending == Pending.Audible))
        {
            host.Settle();
            if (cell.Pending == Pending.Audible)
            {
                if (cell.Route == Route.DeeperAtBound)
                {
                    host.Workspace.ConnectionsShallowerCommand.Execute(null);
                    host.Settle();
                }
                Arm();
                host.Workspace.ConnectionsDeeperCommand.Execute(null);
                WaitParked();
            }
            else
            {
                if (VaultBumped(cell))
                {
                    BumpTheVault(host, stamp);
                }
                Arm();
                host.Workspace.NotifyGraphOfVaultChange();
                Assert.True(
                    SpinWait.SpinUntil(() =>
                    {
                        PumpedDispatcher.Drain();
                        return host.Leaf.InFlight;
                    }, TimeSpan.FromSeconds(10)),
                    $"{cell}: the probe issued no reload");
                WaitParked();
                // The graph document's own refresh is the arrangement's (see
                // the FOLLOWING arrangement): drained before the route.
                SettleTheGraph(host);
            }
        }

        // The leaf and the pane as the cell says (a stale pin's leaf is
        // already inactive; the cell keeps it inactive or unmounted).
        if (cell.Leaf == LeafState.Connections)
        {
            if (cell.Pane == Pane.Collapsed)
            {
                host.Workspace.IsRightPaneVisible = false;
            }
            host.ActivateLeaf();
        }
        else
        {
            host.Workspace.ActiveLeaf = Host.OutlineLeaf;
            if (cell.Pane == Pane.Collapsed)
            {
                host.Workspace.IsRightPaneVisible = false;
            }
        }
        Rest();

        AssertArranged(host, cell, parked);
        Assert.True(
            string.Equals(host.Leaf.Pin, pin, StringComparison.Ordinal),
            $"{cell}: the arrangement left the pin {host.Leaf.Pin ?? "none"}, not {pin}");
        Assert.True(
            string.Equals(host.Leaf.NoteInView, noteInView, StringComparison.Ordinal),
            $"{cell}: the arrangement left the note in view {host.Leaf.NoteInView ?? "none"}, not {noteInView ?? "none"}");
        Assert.True(
            host.Leaf.BackStack.SequenceEqual(StackBefore(cell)),
            $"{cell}: the arrangement left the stack [{StackText(host.Leaf.BackStack)}], not [{StackText(StackBefore(cell))}]");
        return parked;
    }

    /// <summary>The arranged state, asserted (IPB-14): with a load in flight
    /// the flag, the presentation the flag is over, the root, the pending
    /// request's depth and the token's policy; at rest a current
    /// presentation, nothing in flight, and the state.</summary>
    private static void AssertArranged(Host host, Cell cell, ParkedFetch? parked)
    {
        if (cell.InFlight)
        {
            Assert.True(host.Leaf.InFlight, $"{cell}: the arrangement left nothing in flight");
            Assert.True(
                host.Leaf.Publication.State == StateBefore(cell),
                $"{cell}: the arrangement left the presentation {host.Leaf.Publication.State}, not {StateBefore(cell)}");
            Assert.True(
                string.Equals(host.Leaf.Root, RootBefore(cell), StringComparison.Ordinal),
                $"{cell}: the arrangement left the root {host.Leaf.Root ?? "none"}, not {RootBefore(cell)}");
            Assert.True(
                host.Leaf.Request is { } request && request.Depth == DepthBefore(cell),
                $"{cell}: the pending request is not at depth {DepthBefore(cell)}");
            GraphAnnouncePolicy policy = cell.Pending == Pending.Audible ? GraphAnnouncePolicy.Summary : GraphAnnouncePolicy.Silent;
            Assert.True(
                host.Leaf.PendingPolicyForTests == policy,
                $"{cell}: the pending token's policy is {host.Leaf.PendingPolicyForTests}, not {policy}");
            // The shutdown route leaves its load genuinely in flight, unparked:
            // the leaf retires into the drain over it.
            Assert.True(parked is not null || cell.Route == Route.Shutdown, $"{cell}: the load in flight was not parked");
        }
        else
        {
            if (cell.Presentation != Presentation.Stale)
            {
                host.Settle();
                Assert.True(host.Leaf.IsCurrent, $"{cell}: the arrangement did not leave the presentation current");
            }
            Assert.False(host.Leaf.InFlight, $"{cell}: the arrangement left a load in flight");
            Assert.True(
                host.Leaf.Publication.State == StateBefore(cell),
                $"{cell}: the arrangement left the presentation {host.Leaf.Publication.State} over {host.Leaf.Root}, not {StateBefore(cell)}");
        }
    }

    private static string StackText(IEnumerable<(string? Pin, string Effective)> stack) =>
        string.Join(", ", stack.Select(entry => $"({entry.Pin ?? "FOLLOWING"}, {entry.Effective})"));

    // --- The drive -------------------------------------------------------------------------

    /// <summary>Drive the route; returns the line captured mid-route where
    /// the derivation names one.</summary>
    private static string? Drive(Host host, Cell cell, Fixture fixture, ParkedFetch? parked)
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
                // The graph's own lines land inside the route, before a parked
                // load of the leaf's is released (B2-8: under a pin it applies).
                SettleTheGraph(host);
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
                host.Workspace.ActiveGroup.ActiveTab = TabFor(host, NoteInViewBefore(cell)!, inactiveOne: true);
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
                    // is the one before the ghost healed into a note. Under a pin
                    // the load released is the pin's.
                    parked?.Gate.Set();
                    host.Settle();
                    string captured = LineFor(host, cell.Pinned ? RootBefore(cell)! : Two, DepthBefore(cell));
                    creator.Gate.Set();
                    DrainCreate(host);
                    return captured;
                }
            case Route.RenameRoot:
                {
                    // With no root in view the rename is Hub's, as B1 drove it.
                    string from = RootBefore(cell) ?? Hub;
                    string to = cell.Pinned ? RenamedPin(cell) : RenamedRoot;
                    host.Session.RenameFile(from, Path.GetFileName(to));
                    host.Workspace.RetargetPath(from, to);
                    ProbeAfterTheMove(host, cell);
                }
                break;
            case Route.DeleteRoot:
                {
                    // The delete as the lifecycle sees it — the file gone and the
                    // vault rescanned (core's own delete trashes through COM and
                    // wants a thread of its own), then the lifecycle's two arms.
                    DeleteTheFile(host, RootBefore(cell) ?? Hub);
                    host.Workspace.InvalidatePath(RootBefore(cell) ?? Hub);
                    ProbeAfterTheMove(host, cell);
                    break;
                }
            case Route.RenameOrigin:
                host.Session.RenameFile(Two, RenamedOrigin);
                host.Workspace.RetargetPath(Two, RenamedOrigin);
                ProbeAfterTheMove(host, cell);
                break;
            case Route.DeleteOrigin:
                DeleteTheFile(host, Two);
                host.Workspace.InvalidatePath(Two);
                ProbeAfterTheMove(host, cell);
                break;
            case Route.RenameNoteInView:
                host.Session.RenameFile(Orphan, RenamedNoteInView);
                host.Workspace.RetargetPath(Orphan, RenamedNoteInView);
                ProbeAfterTheMove(host, cell);
                break;
            case Route.RenameFolder:
                {
                    string folder = cell.Pinned ? PinFolderBefore(cell) : PinFolder;
                    host.Session.RenameFolder(folder, "moved");
                    host.Workspace.RetargetPath(folder, MovedFolder);
                    ProbeAfterTheMove(host, cell);
                }
                break;
            case Route.DeleteFolder:
                {
                    string folder = cell.Pinned ? PinFolderBefore(cell) : PinFolder;
                    string path = Path.Combine(host.Root, folder);
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, recursive: true);
                    }
                    using (var cancel = new CancelToken())
                    {
                        host.Session.ScanInitial(cancel);
                    }
                    host.Workspace.InvalidatePath(folder);
                    ProbeAfterTheMove(host, cell);
                }
                break;
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

    private static void DeleteTheFile(Host host, string relative)
    {
        string path = Path.Combine(host.Root, relative);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        using var cancel = new CancelToken();
        host.Session.ScanInitial(cancel);
    }

    /// <summary>The lifecycle's file-change arm after a rename or a delete
    /// (Term 3(f)): the probe, waited for.</summary>
    private static void ProbeAfterTheMove(Host host, Cell cell)
    {
        int pending = host.Leaf.PendingWorkForTests;
        host.Workspace.NotifyGraphOfVaultChange();
        WaitForTheProbesDecision(host, cell, pending);
    }

    /// <summary>The probe decides on the pool and applies on the dispatcher
    /// (Term 6): the route's synchronous part is over only once that
    /// decision has applied, and the probe's tracked task completes after
    /// its apply (Term 7), so the leaf's pending count is back at or below
    /// what it was before the probe was issued only then; a load the route
    /// issued may complete meanwhile, which only lowers the count further.</summary>
    private static void WaitForTheProbesDecision(Host host, Cell cell, int pendingBefore)
    {
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
        foreach (Mode mode in Enum.GetValues<Mode>())
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
                                foreach (Pending pending in Enum.GetValues<Pending>())
                                {
                                    yield return new Cell(mode, pane, leaf, root, presentation, pending, route);
                                }
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

    /// <summary>The expected lines core reports for the vault — over its
    /// own copy, as it stands and after the arrangement's bump — and the
    /// ghost each root's tree heals first.</summary>
    private static Fixture FixtureOf()
    {
        using GraphVault vault = GraphVault.Copy("model-fixture");
        using VaultSession session = VaultSession.OpenFilesystem(vault.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        string summary = SummaryOf(session);
        string ghost = GhostOf(session, Hub);
        string pinnedGhost = GhostOf(session, Deep);
        WriteTheBump(vault.Root, "bump-fixture.md");
        session.ScanInitial(cancel);
        return new Fixture(ghost, pinnedGhost, summary, SummaryOf(session));

        static string SummaryOf(VaultSession session) =>
            Render(new GraphA11yEvent.GraphSnapshotSummary(session.GraphSnapshot(GraphViewState.DefaultFilter()).SummaryCounts));

        static string GhostOf(VaultSession session, string root)
        {
            GraphConnectionsTree tree = session.GraphConnectionsTree(root, 1, SlateUniffiMethods.GraphConnectionsFilter());
            GraphConnectionRow ghost = tree.Outgoing.First(row => row.Kind == GraphNodeKind.Ghost);
            return SlateUniffiMethods.GraphGhostNotePath(ghost.TargetRaw);
        }
    }

    [Fact]
    public void TheModelOfTermsTwoToNineDerivesEveryRoutesTimelineAcrossEveryState()
    {
        Assert.Equal(PinnedRoutes, Enum.GetValues<Route>().Length);
        Assert.Equal(PinnedModes, Enum.GetValues<Mode>().Length);
        Cell[] cells = [.. Cells()];
        Assert.Equal(PinnedCells, cells.Length);
        string[] only = (Environment.GetEnvironmentVariable("SLATE_MODEL_ONLY") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
                if (only.Length > 0 && !only.All(term => cell.ToString().Contains(term, StringComparison.Ordinal)))
                {
                    continue;
                }
                using GraphVault vault = GraphVault.Copy($"model-{driven}");
                fixture ??= FixtureOf();
                Derivation expected = Derive(cell, fixture);
                Host host;
                Host? created = null;
                int before;
                string? lineBefore = null;
                string? captured = null;
                ParkedFetch? parked = null;
                driven++;
                try
                {
                    if (cell.Route == Route.Launch)
                    {
                        using (Host first = ModelHost(vault.Root))
                        {
                            _ = Arrange(first, cell, fixture, driven);
                            SettleTheDocuments(first);
                        }
                        // The launch IS the construction: its timeline from the start.
                        host = created = ModelHost(vault.Root);
                        before = 0;
                    }
                    else
                    {
                        host = created = ModelHost(vault.Root);
                        try
                        {
                            parked = Arrange(host, cell, fixture, driven);
                        }
                        catch (Exception arrangement) when (arrangement is Xunit.Sdk.XunitException or InvalidOperationException)
                        {
                            // A state the model cannot arrange is a divergence of
                            // its own, reported with the rest rather than aborting
                            // the run (the fact still fails).
                            parked?.Gate.Set();
                            parked?.Dispose();
                            host.Dispose();
                            failures.Add($"{cell}: the arrangement failed — {arrangement.Message.ReplaceLineEndings(" ")}");
                            continue;
                        }
                        if (RootBefore(cell) is { } rootBefore)
                        {
                            lineBefore = LineFor(host, rootBefore, DepthBefore(cell));
                        }
                        host.Clear();
                        before = host.Loads;
                        captured = Drive(host, cell, fixture, parked);
                        parked?.Gate.Set();
                    }
                }
                catch (Exception drive) when (drive is Xunit.Sdk.XunitException or InvalidOperationException)
                {
                    parked?.Gate.Set();
                    parked?.Dispose();
                    created?.Dispose();
                    failures.Add($"{cell}: the drive failed — {drive.Message.ReplaceLineEndings(" ")}");
                    continue;
                }
                if (cell.Route != Route.Shutdown)
                {
                    SettleTheDocuments(host);
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
                if (parked is { TimedOut: true })
                {
                    mismatch.Add("the parked fetch resumed on its own before the route released it");
                }
                if (loads != expected.Loads && loads != expected.LoadsOr)
                {
                    mismatch.Add($"loads {loads}, derived {expected.Loads}{(expected.LoadsOr is { } alternative ? $" or {alternative}" : string.Empty)}");
                }
                if (!timeline.SequenceEqual(host.Timeline))
                {
                    mismatch.Add($"timeline [{string.Join(" | ", host.Timeline)}], derived [{string.Join(" | ", timeline)}]");
                }
                if (cell.Route != Route.Shutdown)
                {
                    if (!string.Equals(host.Leaf.Root, expected.Root, StringComparison.Ordinal))
                    {
                        mismatch.Add($"root {host.Leaf.Root ?? "none"}, derived {expected.Root ?? "none"}");
                    }
                    if (expected.Stale is { } stale && host.Leaf.Root is not null && host.Leaf.IsStale != stale)
                    {
                        mismatch.Add($"stale {host.Leaf.IsStale}, derived {stale}");
                    }
                    if (host.Leaf.Root is not null && host.Leaf.Depth != depthAfter)
                    {
                        mismatch.Add($"depth {host.Leaf.Depth}, derived {depthAfter}");
                    }
                    ConnectionsLoadState stateAfter = StateAfter(cell, expected);
                    if (host.Leaf.Publication.State != stateAfter)
                    {
                        mismatch.Add($"state {host.Leaf.Publication.State}, derived {stateAfter}");
                    }
                    if (host.Leaf.InFlight)
                    {
                        mismatch.Add("a load still in flight after the settle");
                    }
                    // Rule D's state (B2-8): the mode, the note in view, the whole
                    // stack, the shared key, the pending mount, the last focus request.
                    RootMode mode = RootModeAfter(cell, fixture, expected);
                    if (!string.Equals(host.Leaf.Pin, mode.Pin, StringComparison.Ordinal))
                    {
                        mismatch.Add($"pin {host.Leaf.Pin ?? "FOLLOWING"}, derived {mode.Pin ?? "FOLLOWING"}");
                    }
                    if (!string.Equals(host.Leaf.NoteInView, mode.NoteInView, StringComparison.Ordinal))
                    {
                        mismatch.Add($"note in view {host.Leaf.NoteInView ?? "none"}, derived {mode.NoteInView ?? "none"}");
                    }
                    if (!host.Leaf.BackStack.SequenceEqual(mode.Stack))
                    {
                        mismatch.Add($"stack [{StackText(host.Leaf.BackStack)}], derived [{StackText(mode.Stack)}]");
                    }
                    string? key = host.Workspace.GraphViewStateForTests.SelectedKey;
                    if (!string.Equals(key, mode.Key, StringComparison.Ordinal))
                    {
                        mismatch.Add($"key {key ?? "none"}, derived {mode.Key ?? "none"}");
                    }
                    if (host.Workspace.ConnectionsMountPendingForTests)
                    {
                        mismatch.Add("a mount still pending after the settle");
                    }
                    string? focus = host.FocusRequests.LastOrDefault();
                    if (!string.Equals(focus, FocusAfter(cell), StringComparison.Ordinal))
                    {
                        mismatch.Add($"focus {focus ?? "none"}, derived {FocusAfter(cell) ?? "none"}");
                    }
                }
                if (mismatch.Count > 0)
                {
                    string tabs = cell.Route == Route.Shutdown
                        ? string.Empty
                        : $" — tabs [{string.Join(", ", host.Workspace.Groups.SelectMany(g => g.Tabs).Select(t => t.Item.Kind + ":" + (t.Path ?? t.Title) + (ReferenceEquals(t, host.Workspace.ActiveGroup.ActiveTab) ? "*" : "")))}]";
                    failures.Add($"{cell}: {string.Join("; ", mismatch)}{tabs}");
                }
                parked?.Dispose();
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
        if (only.Length > 0)
        {
            // A narrowed run (SLATE_MODEL_ONLY, a development and mutation
            // affordance): the cells it drove agree; the totals are the whole
            // model's.
            Assert.True(driven > 0, $"the narrowing [{string.Join(", ", only)}] matched no cell");
            return;
        }
        Assert.True(
            unreachable.Count == PinnedUnreachable && driven == PinnedDriven,
            $"the model named {unreachable.Count} cells as not states of the system and drove {driven}; pinned {PinnedUnreachable} and {PinnedDriven}");
    }
}
