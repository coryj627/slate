// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Bases;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR B2 (#746), contract B2-8 — THE MODEL's second family: rule D's
/// re-root (Term 12) and Back (Term 13) derived over the mode (IGJ-12's
/// three pinned arrangements beside FOLLOWING), the pane, the leaf, the
/// note in view (a note; no tab — or the base tab alone for the Bases'
/// entrance; the graph tab — or the base tab — with a note beside), the
/// ENTRANCE (the leaf's row, the graph table's row, the Bases grid's row,
/// or the funnel itself for the targets no fixture surface lists), the
/// TARGET (a Note, an Attachment, the pin itself — the same-root case — a
/// canvas, a base) and the open's GATE (clean; a dirty tab whose dialog
/// pumps a real dispatcher frame and allows; one that refuses). Every
/// reachable cell arranges the mode as the first family does, adds the
/// entrance's surface and the target, drives the route and compares the
/// timeline in Term 14's order, the loads, the root, the presentation, the
/// pin, the note in view, the whole stack, the shared key, the pending
/// mount and the last focus request — the leaf's boundary, never the
/// editor (IGL-3) — against the derivation; the totals are literals.
/// </summary>
public sealed partial class ConnectionsLeafTests
{
    private enum Entrance { Leaf, Table, Bases, Funnel }

    private enum Target { Note, Attachment, SameRoot, Canvas, Base }

    /// <summary>The open's dirty gate: no dialog; a dirty tab whose dialog
    /// pumps a frame and DISCARDS; one whose dialog CANCELS.</summary>
    private enum Gate { Clean, Allowed, Refused }

    private enum ReRootRoute { ReRoot, Back }

    private sealed record ReRootCell(Mode Mode, Pane Pane, LeafState Leaf, RootState Root, Entrance Entrance, Target Target, Gate Gate, ReRootRoute Route)
    {
        public bool Pinned => Mode != Mode.Following;

        public bool HasRoot => Pinned || Root == RootState.Note;

        public override string ToString() => Route == ReRootRoute.Back
            ? $"Back ({Gate}) from [{Mode}, {Pane}, {Leaf}, {Root}]"
            : $"ReRoot from the {Entrance} to a {Target} ({Gate}) from [{Mode}, {Pane}, {Leaf}, {Root}]";
    }

    /// <summary>The derivation of a re-root or a Back: the timeline, the
    /// loads, the effective root after, rule D's state, and how many of the
    /// timeline's LAST entries are completions of independent documents,
    /// whose order among themselves the system does not fix (two fetches
    /// on the pool: the model compares them as a set).</summary>
    private sealed record ReRootDerivation(string[] Timeline, int Loads, string? Root, RootMode Mode, string? Focus, int Unordered = 0);

    private const string TableTarget = "10.md";
    private const string Attachment = "pic.png";
    private const string NotesBase = "Notes.base";
    private const string AllBase = "All.base";

    private const int ReRootCells = PinnedModes * 2 * 2 * 3 * 4 * 5 * 3 * 2;
    private const int ReRootUnreachable = 5352;
    private const int ReRootDriven = 408;

    /// <summary>A mutable dirty gate for the model: the decision to give,
    /// and what lands inside the dialog first (the composed routes).</summary>
    private sealed class GateSeam
    {
        public WorkspaceDirtyNavigationDecision Decision { get; set; } = WorkspaceDirtyNavigationDecision.Discard;

        /// <summary>What lands inside the dialog, ONCE: a re-entrant route
        /// asks again from inside, and the second dialog runs nothing.</summary>
        public Action? InsideTheDialog { get; set; }

        /// <summary>The pin's load the drive parked, for a dialog that
        /// releases it.</summary>
        public ParkedFetch? Parked { get; set; }

        public int Asked { get; private set; }

        public WorkspaceDirtyNavigationDecision Decide(WorkspaceTabViewModel tab, WorkspaceItemState item)
        {
            Asked++;
            // The dialog is modal and pumps the dispatcher (IGK-20): a real
            // frame under the production scheduler, then whatever lands.
            PumpedDispatcher.Drain();
            Action? inside = InsideTheDialog;
            InsideTheDialog = null;
            inside?.Invoke();
            return Decision;
        }
    }

    private static Host ReRootHost(string root, GateSeam gate) => new(root, () => 1, gate.Decide);

    private static string? UnreachableReRoot(ReRootCell cell)
    {
        if (cell.Mode is Mode.PinnedFresh or Mode.PinnedNoOrigin && cell.Root != RootState.Note)
        {
            return "the arrangement's note in view is the pin (IGJ-12): its tab is the one in view";
        }
        if (cell.Gate != Gate.Clean && cell.Root != RootState.Note)
        {
            return "the dirty gate stands before an in-place open of a note's tab; from no tab, the graph tab or the base tab the open creates a tab and asks nothing";
        }
        if (cell.Route == ReRootRoute.Back)
        {
            if (cell.Entrance != Entrance.Funnel || cell.Target != Target.Note)
            {
                return "Back takes no entrance and no target";
            }
            return null;
        }
        switch (cell.Entrance)
        {
            case Entrance.Funnel:
                if (cell.Target is not (Target.Canvas or Target.Base))
                {
                    return "the funnel is reached through an entrance (B2-3); it is driven directly for the canvas and the base targets alone, which no fixture surface lists as a row";
                }
                break;
            case Entrance.Leaf:
                if (!cell.HasRoot)
                {
                    return "the leaf's row action needs a RENDERED tree: with no effective root the leaf shows none";
                }
                if (cell.Target == Target.SameRoot)
                {
                    return "the leaf's own tree never lists its root: the leaf's same-root case is named unreachable (B2-8)";
                }
                if (cell.Target is Target.Canvas or Target.Base)
                {
                    return "no note of the fixture links a canvas or a base: the leaf's tree lists neither";
                }
                if (cell.Pinned && cell.Target == Target.Attachment)
                {
                    return "the pin's tree (Deep's) carries no attachment row; the leaf's attachment target is driven FOLLOWING from Hub's";
                }
                break;
            case Entrance.Table:
                if (cell.Root == RootState.None)
                {
                    return "no graph tab: the table's entrance has nothing to address";
                }
                if (cell.Gate != Gate.Clean)
                {
                    return "the table's entrance addresses the graph tab first, so its open replaces the graph's tab in place (A-9), which is never dirty";
                }
                if (cell.Target == Target.SameRoot && !cell.Pinned)
                {
                    return "FOLLOWING has no pin to match: a row naming the note in view is a fresh pin";
                }
                if (cell.Target is Target.Canvas or Target.Base)
                {
                    return "driven through the funnel: a canvas or base row is the table's business, not the target's";
                }
                break;
            case Entrance.Bases:
                if (cell.Gate != Gate.Clean)
                {
                    return "the base tab is the one in view when the entrance is addressed, so its open replaces the base's tab in place, which is never dirty";
                }
                if (cell.Target == Target.SameRoot && !cell.Pinned)
                {
                    return "FOLLOWING has no pin to match: a row naming the note in view is a fresh pin";
                }
                if (cell.Target is Target.Canvas or Target.Base)
                {
                    return "driven through the funnel: a canvas or base row is the grid's business, not the target's";
                }
                break;
        }
        return null;
    }

    /// <summary>The path a target names.</summary>
    private static string TargetPath(ReRootCell cell) => cell.Target switch
    {
        Target.Note => cell.Entrance == Entrance.Leaf ? Two : TableTarget,
        Target.Attachment => Attachment,
        Target.SameRoot => Deep,
        Target.Canvas => Board,
        Target.Base => NotesBase,
        _ => throw new InvalidOperationException($"no path for {cell.Target}"),
    };

    /// <summary>The first family's cell the arrangement borrows: the mode
    /// and the note in view, current, nothing in flight, on a route that
    /// arranges nothing of its own.</summary>
    private static Cell ModeCellOf(ReRootCell cell) =>
        new(cell.Mode, cell.Pane, cell.Leaf, cell.Root, Presentation.Current, Pending.None, Route.SwitchToLeaf);

    private static ReRootDerivation DeriveReRoot(ReRootCell cell, Fixture fixture)
    {
        Cell modeCell = ModeCellOf(cell);
        bool mounted = cell.Pane == Pane.Visible;
        bool active = cell.Leaf == LeafState.Connections;
        bool noteTab = cell.Root == RootState.Note;
        string? pinBefore = cell.Pinned ? PinBefore(modeCell) : null;
        string? noteInViewBefore = NoteInViewBefore(modeCell);
        (string? Pin, string Effective)[] stackBefore = StackBefore(modeCell);
        string? rootBefore = RootBefore(modeCell);
        RootMode unchanged = new(pinBefore, noteInViewBefore, stackBefore, cell.Pinned ? StableKey(pinBefore!) : null);
        // The pin and the pop mutations run on Show's shape: the shell's pane
        // line when collapsed (B-D9), the setter's leaf line when the leaf
        // changes (Term 14).
        string[] reveal = [.. (mounted ? Array.Empty<string>() : ["RightPaneShown"]), .. (active ? Array.Empty<string>() : ["LeafPanelShown"])];

        if (cell.Route == ReRootRoute.Back)
        {
            // Term 13: FOLLOWING, or an empty stack, falls through; a refused
            // open pops nothing (B2-D5); else the ordinary open of the top
            // entry's note — `TabFocused` when it creates a tab — then the pop
            // mutation: the reveal, the re-root's line, ONE audible load.
            if (!cell.Pinned || stackBefore.Length == 0 || cell.Gate == Gate.Refused)
            {
                return new([], 0, rootBefore, unchanged, null);
            }
            (string? priorPin, string effective) = stackBefore[^1];
            // An open lands IN PLACE over the tab in view whatever its kind
            // (a note's, the graph's — contract A-9 — a base's): `TabFocused`
            // only when there is no tab at all and one is created.
            string[] popOpened = cell.Root == RootState.None ? ["TabFocused"] : [];
            RootMode popped = new(priorPin, effective, stackBefore[..^1], StableKey(effective));
            return new([.. popOpened, .. reveal, ReRooted(effective), LinePlaceholder], 1, priorPin ?? effective, popped, "RightPane");
        }

        string target = TargetPath(cell);
        if (cell.Entrance == Entrance.Bases && noteTab)
        {
            // IGJ-9: the invoking document is not the active tab's — refused
            // by address, nothing moves.
            return new([], 0, rootBefore, unchanged, null);
        }
        // The table's entrance makes the graph tab active first (IGI-4): the
        // shell's `TabFocused` when a note was in view, and the note in view
        // becomes none — FOLLOWING, the root goes with it (Term 3(g)), so
        // nothing is pushed (B2-D7); under a pin it is recorded.
        string[] address = cell.Entrance == Entrance.Table && noteTab ? ["TabFocused"] : [];
        string? noteInViewAtThePin = cell.Entrance == Entrance.Table ? null : noteInViewBefore;
        if (cell.Target == Target.SameRoot)
        {
            // B2D-6: already pinned on the path — the key repaired, the reveal
            // and the activation as B1's triggers say: a MOUNT with the leaf
            // active at its consume loads (Term 3(a)); a mounted switch to a
            // current leaf does not (Term 3(b)); no push, no re-root line. The
            // table's address re-activated a WARM graph tab that stays: PR A's
            // activation summary lands after the synchronous lines, ahead of
            // the leaf's own load (issued after it); a re-root's open replaces
            // that tab in place and its document retires before its line.
            int loads = mounted ? 0 : 1;
            string[] graph = address.Length > 0 ? [fixture.GraphSummary] : [];
            RootMode repaired = new(pinBefore, noteInViewAtThePin, stackBefore, StableKey(pinBefore!));
            // Two fetches on the pool — the graph's and the leaf's — land in
            // either order.
            int unordered = graph.Length + loads == 2 ? 2 : 0;
            return new([.. address, .. reveal, .. graph, .. (loads == 1 ? [LinePlaceholder] : Array.Empty<string>())], loads, rootBefore, repaired, "RightPane", unordered);
        }
        // Term 12: the pin mutation — the effective root pushed (the pin, or
        // FOLLOWING the note in view; none from the graph tab, the base tab
        // or no tab), the pin, ONE audible load, the key, the line — then
        // the ordinary open, which lands IN PLACE over the tab in view
        // whatever its kind (a note's, the graph's — contract A-9 — or a
        // base's): `TabFocused` only when the target's own tab exists beside
        // and is activated (Two beside the graph), or there is no tab at all
        // and one is created; refused, the pin stands and the note in view
        // is untouched. A canvas or a base opens as its own kind of tab: no
        // Markdown candidate, the note in view none (IGL-1).
        List<(string? Pin, string Effective)> stack = [.. stackBefore];
        if (cell.Pinned)
        {
            stack.Add((pinBefore, pinBefore!));
        }
        else if (noteTab && cell.Entrance is Entrance.Leaf or Entrance.Funnel)
        {
            stack.Add((null, noteInViewBefore!));
        }
        bool refused = cell.Gate == Gate.Refused;
        bool targetTabBeside = string.Equals(target, Two, StringComparison.Ordinal) && cell.Root == RootState.NoneBesideNote;
        bool noTab = cell.Root == RootState.None && cell.Entrance != Entrance.Bases;
        string[] opened = !refused && (targetTabBeside || noTab) ? ["TabFocused"] : [];
        string? noteInView = refused
            ? noteInViewAtThePin
            : cell.Target is Target.Canvas or Target.Base ? null : target;
        RootMode pinned = new(target, noteInView, [.. stack], StableKey(target));
        return new([.. address, .. reveal, ReRooted(target), .. opened, LinePlaceholder], 1, target, pinned, "RightPane");
    }

    /// <summary>Arrange the cell: the mode and the note in view through the
    /// first family's arrangement (current, nothing in flight), then the
    /// entrance's surface — the graph tab beside a note with the filter
    /// admitting attachments for that target; the base tabs — and the
    /// canvas or base the funnel is aimed at; the dirty tab for a gate.</summary>
    private static void ArrangeReRoot(Host host, ReRootCell cell, Fixture fixture, GateSeam gate)
    {
        Cell modeCell = ModeCellOf(cell);
        if (cell.Entrance == Entrance.Table && cell.Target == Target.Attachment)
        {
            host.Workspace.GraphViewStateForTests.Filter = new GraphFilter(IncludeAttachments: true, IncludeGhosts: true, OrphansOnly: false);
        }
        if (cell.Target is Target.Canvas or Target.Base || cell.Entrance == Entrance.Bases)
        {
            File.WriteAllText(Path.Combine(host.Root, Board), "{\"nodes\":[],\"edges\":[]}\n");
            File.WriteAllText(
                Path.Combine(host.Root, NotesBase),
                "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n    order:\n      - file.name\n");
            File.WriteAllText(
                Path.Combine(host.Root, AllBase),
                "views:\n  - type: table\n    name: Main\n    order:\n      - file.name\n");
            using var cancel = new CancelToken();
            host.Session.ScanInitial(cancel);
        }
        if (cell.Entrance == Entrance.Bases && !cell.Pinned && cell.Root != RootState.Note)
        {
            // The base tab alone, or beside Two: the first family's arrangement
            // would open the graph tab instead.
            host.Workspace.ActiveLeaf = Host.OutlineLeaf;
            if (cell.Root == RootState.NoneBesideNote)
            {
                host.OpenNote(Two);
            }
            host.Workspace.OpenPath(cell.Target == Target.Attachment ? AllBase : NotesBase, WorkspaceOpenTarget.NewTab);
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
        }
        else
        {
            ParkedFetch? parked = Arrange(host, modeCell, fixture, 0);
            Assert.Null(parked);
            switch (cell.Entrance)
            {
                case Entrance.Table when cell.Root == RootState.Note:
                    // The graph tab beside the note, the note back in view.
                    host.Workspace.OpenGraph();
                    SettleTheDocuments(host);
                    host.Workspace.ActiveGroup.ActiveTab = TabFor(host, NoteInViewBefore(modeCell)!);
                    SettleTheDocuments(host);
                    break;
                case Entrance.Bases:
                    host.Workspace.OpenPath(cell.Target == Target.Attachment ? AllBase : NotesBase, WorkspaceOpenTarget.NewTab);
                    SettleTheDocuments(host);
                    if (cell.Root == RootState.Note)
                    {
                        host.Workspace.ActiveGroup.ActiveTab = TabFor(host, NoteInViewBefore(modeCell)!);
                        SettleTheDocuments(host);
                    }
                    break;
            }
        }
        if (cell.Gate != Gate.Clean)
        {
            WorkspaceTabViewModel tab = host.Workspace.ActiveGroup.ActiveTab!;
            Assert.True(tab.IsMarkdown, $"{cell}: the tab in view is not a note's");
            tab.Text = "# edited and unsaved\n";
            Assert.True(tab.IsDirty, $"{cell}: the tab did not become dirty");
            gate.Decision = cell.Gate == Gate.Refused ? WorkspaceDirtyNavigationDecision.Cancel : WorkspaceDirtyNavigationDecision.Discard;
        }
        // The surfaces moved the note in view through none and back, which
        // leaves a FOLLOWING presentation stale while the leaf is inactive or
        // unmounted (Term 3(d)): the leaf shown and active loads it current
        // (Terms 3(a), 3(b)); then the pane and the leaf as the cell says.
        if (!host.Workspace.IsRightPaneVisible)
        {
            // The command's reveal: the mount is consumed at its boundary (a
            // property write alone leaves it pending).
            host.Workspace.ToggleRightPaneCommand.Execute(null);
        }
        host.ActivateLeaf();
        SettleTheDocuments(host);
        if (cell.Leaf == LeafState.Other)
        {
            host.Workspace.ActiveLeaf = Host.OutlineLeaf;
        }
        if (cell.Pane == Pane.Collapsed)
        {
            host.Workspace.IsRightPaneVisible = false;
        }
        SettleTheDocuments(host);
        Assert.False(host.Leaf.InFlight, $"{cell}: the arrangement left a load in flight");
        Assert.True(host.Leaf.Root is null || host.Leaf.IsCurrent, $"{cell}: the arrangement left the presentation stale");
        Assert.True(
            string.Equals(host.Leaf.Root, RootBefore(modeCell), StringComparison.Ordinal),
            $"{cell}: the arrangement left the root {host.Leaf.Root ?? "none"}, not {RootBefore(modeCell) ?? "none"}");
        Assert.True(
            string.Equals(host.Leaf.Pin, cell.Pinned ? PinBefore(modeCell) : null, StringComparison.Ordinal),
            $"{cell}: the arrangement left the pin {host.Leaf.Pin ?? "FOLLOWING"}");
        Assert.True(
            host.Leaf.BackStack.SequenceEqual(StackBefore(modeCell)),
            $"{cell}: the arrangement left the stack [{StackText(host.Leaf.BackStack)}]");
        string? noteInView = cell.Entrance == Entrance.Bases && cell.Root != RootState.Note ? null : NoteInViewBefore(modeCell);
        Assert.True(
            string.Equals(host.Leaf.NoteInView, noteInView, StringComparison.Ordinal),
            $"{cell}: the arrangement left the note in view {host.Leaf.NoteInView ?? "none"}, not {noteInView ?? "none"}");
    }

    private static void DriveReRoot(Host host, ReRootCell cell)
    {
        if (cell.Route == ReRootRoute.Back)
        {
            _ = host.Workspace.ConnectionsBack();
            return;
        }
        string target = TargetPath(cell);
        switch (cell.Entrance)
        {
            case Entrance.Leaf:
                {
                    GraphConnectionsTree tree = host.Leaf.Publication.Tree!;
                    GraphConnectionRow row = tree.Outgoing.Concat(tree.Incoming)
                        .First(candidate => string.Equals(candidate.Path, target, StringComparison.Ordinal));
                    Assert.True(host.Leaf.IsActionEnabled(GraphRowAction.ShowConnections, row), $"{cell}: the leaf's action is disabled");
                    host.Leaf.Execute(GraphRowAction.ShowConnections, row);
                    break;
                }
            case Entrance.Table:
                {
                    GraphDocumentViewModel document = host.Workspace.GraphDocument!;
                    GraphTableRow row = document.Publication.Rows.First(candidate => string.Equals(candidate.Path, target, StringComparison.Ordinal));
                    Assert.True(document.IsActionEnabled(GraphRowAction.ShowConnections, row), $"{cell}: the table's action is disabled");
                    document.Execute(GraphRowAction.ShowConnections, row);
                    break;
                }
            case Entrance.Bases:
                {
                    string basePath = cell.Target == Target.Attachment ? AllBase : NotesBase;
                    WorkspaceTabViewModel baseTab = TabFor(host, basePath);
                    BaseDocumentViewModel document = Assert.IsType<BaseDocumentViewModel>(baseTab.Base);
                    Assert.Equal(BaseLoadState.Ready, document.State);
                    BasesRow row = document.Result!.Rows.First(candidate => string.Equals(candidate.FilePath, target, StringComparison.Ordinal));
                    _ = host.Workspace.BasesShowConnectionsFor(document, row);
                    break;
                }
            case Entrance.Funnel:
                _ = host.Workspace.ReRootConnectionsOn(target);
                break;
        }
    }

    private static IEnumerable<ReRootCell> ReRootCellsOf()
    {
        foreach (Mode mode in Enum.GetValues<Mode>())
        {
            foreach (ReRootRoute route in Enum.GetValues<ReRootRoute>())
            {
                foreach (Entrance entrance in Enum.GetValues<Entrance>())
                {
                    foreach (Target target in Enum.GetValues<Target>())
                    {
                        foreach (Gate gate in Enum.GetValues<Gate>())
                        {
                            foreach (Pane pane in Enum.GetValues<Pane>())
                            {
                                foreach (LeafState leaf in Enum.GetValues<LeafState>())
                                {
                                    foreach (RootState root in Enum.GetValues<RootState>())
                                    {
                                        yield return new ReRootCell(mode, pane, leaf, root, entrance, target, gate, route);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>Whether core loads a tree for the path (a base is no graph
    /// node): the presentation a load of it leaves.</summary>
    private static ConnectionsLoadState LoadedStateOf(Host host, string root)
    {
        try
        {
            _ = host.Session.GraphConnectionsTree(root, 1, SlateUniffiMethods.GraphConnectionsFilter());
            return ConnectionsLoadState.Ready;
        }
        catch (VaultException)
        {
            return ConnectionsLoadState.Error;
        }
    }

    /// <summary>The recorded timeline against the derived one: exact, but
    /// for the last <paramref name="unordered"/> entries — independent
    /// completions — compared as a set.</summary>
    private static bool TimelinesAgree(IReadOnlyList<string> recorded, string[] derived, int unordered)
    {
        if (unordered < 2)
        {
            return recorded.SequenceEqual(derived);
        }
        if (recorded.Count != derived.Length || recorded.Count < unordered)
        {
            return false;
        }
        int head = derived.Length - unordered;
        return recorded.Take(head).SequenceEqual(derived.Take(head))
            && recorded.Skip(head).OrderBy(line => line, StringComparer.Ordinal)
                .SequenceEqual(derived.Skip(head).OrderBy(line => line, StringComparer.Ordinal));
    }

    [Fact]
    public void TheModelOfRuleDDerivesEveryReRootAndBackAcrossEveryState()
    {
        ReRootCell[] cells = [.. ReRootCellsOf()];
        Assert.Equal(ReRootCells, cells.Length);
        string[] only = (Environment.GetEnvironmentVariable("SLATE_MODEL_ONLY") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var failures = new List<string>();
        var unreachable = new List<string>();
        int driven = 0;
        PumpedDispatcher.Run(() =>
        {
            Fixture? fixture = null;
            foreach (ReRootCell cell in cells)
            {
                if (UnreachableReRoot(cell) is { } reason)
                {
                    unreachable.Add($"{cell}: {reason}");
                    continue;
                }
                if (only.Length > 0 && !only.All(term => cell.ToString().Contains(term, StringComparison.Ordinal)))
                {
                    continue;
                }
                using GraphVault vault = GraphVault.Copy($"reroot-{driven}");
                fixture ??= FixtureOf();
                var gate = new GateSeam();
                using Host host = ReRootHost(vault.Root, gate);
                ReRootDerivation expected = DeriveReRoot(cell, fixture);
                driven++;
                int before;
                try
                {
                    ArrangeReRoot(host, cell, fixture, gate);
                    host.Clear();
                    before = host.Loads;
                    DriveReRoot(host, cell);
                    SettleTheDocuments(host);
                }
                catch (Exception failure) when (failure is Xunit.Sdk.XunitException or InvalidOperationException)
                {
                    // A state the model cannot arrange or drive is a divergence of
                    // its own, reported with the rest (the fact still fails).
                    failures.Add($"{cell}: failed — {failure.Message.ReplaceLineEndings(" ")}");
                    continue;
                }
                int loads = host.Loads - before;
                string[] timeline =
                [
                    .. expected.Timeline.Select(entry => entry == LinePlaceholder
                        ? expected.Root is { } root ? LineFor(host, root, host.Leaf.Depth) : "<no root to report>"
                        : entry),
                ];
                var mismatch = new List<string>();
                if (loads != expected.Loads)
                {
                    mismatch.Add($"loads {loads}, derived {expected.Loads}");
                }
                if (!TimelinesAgree(host.Timeline, timeline, expected.Unordered))
                {
                    mismatch.Add($"timeline [{string.Join(" | ", host.Timeline)}], derived [{string.Join(" | ", timeline)}]{(expected.Unordered > 0 ? $" (the last {expected.Unordered} in either order)" : string.Empty)}");
                }
                if (!string.Equals(host.Leaf.Root, expected.Root, StringComparison.Ordinal))
                {
                    mismatch.Add($"root {host.Leaf.Root ?? "none"}, derived {expected.Root ?? "none"}");
                }
                if (host.Leaf.Root is not null && host.Leaf.IsStale)
                {
                    mismatch.Add("stale after the route");
                }
                if (host.Leaf.InFlight)
                {
                    mismatch.Add("a load still in flight after the settle");
                }
                ConnectionsLoadState state = expected.Root is null ? ConnectionsLoadState.NoNote : LoadedStateOf(host, expected.Root);
                if (host.Leaf.Publication.State != state)
                {
                    mismatch.Add($"state {host.Leaf.Publication.State}, derived {state}");
                }
                if (!string.Equals(host.Leaf.Pin, expected.Mode.Pin, StringComparison.Ordinal))
                {
                    mismatch.Add($"pin {host.Leaf.Pin ?? "FOLLOWING"}, derived {expected.Mode.Pin ?? "FOLLOWING"}");
                }
                if (!string.Equals(host.Leaf.NoteInView, expected.Mode.NoteInView, StringComparison.Ordinal))
                {
                    mismatch.Add($"note in view {host.Leaf.NoteInView ?? "none"}, derived {expected.Mode.NoteInView ?? "none"}");
                }
                if (!host.Leaf.BackStack.SequenceEqual(expected.Mode.Stack))
                {
                    mismatch.Add($"stack [{StackText(host.Leaf.BackStack)}], derived [{StackText(expected.Mode.Stack)}]");
                }
                string? key = host.Workspace.GraphViewStateForTests.SelectedKey;
                if (!string.Equals(key, expected.Mode.Key, StringComparison.Ordinal))
                {
                    mismatch.Add($"key {key ?? "none"}, derived {expected.Mode.Key ?? "none"}");
                }
                if (host.Workspace.ConnectionsMountPendingForTests)
                {
                    mismatch.Add("a mount still pending after the settle");
                }
                string? focus = host.FocusRequests.LastOrDefault();
                if (!string.Equals(focus, expected.Focus, StringComparison.Ordinal))
                {
                    mismatch.Add($"focus {focus ?? "none"}, derived {expected.Focus ?? "none"}");
                }
                if (host.FocusRequests.Contains("editor"))
                {
                    mismatch.Add("the editor's focus was requested (IGL-3)");
                }
                if (cell.Gate == Gate.Clean && gate.Asked > 0)
                {
                    mismatch.Add("the dirty gate asked with nothing dirty");
                }
                if (cell.Gate != Gate.Clean && gate.Asked == 0 && cell.Route == ReRootRoute.ReRoot && cell.Target != Target.SameRoot && !(cell.Entrance == Entrance.Bases && cell.Root == RootState.Note))
                {
                    mismatch.Add("the dirty gate never asked");
                }
                if (mismatch.Count > 0)
                {
                    string tabs = $" — tabs [{string.Join(", ", host.Workspace.Groups.SelectMany(g => g.Tabs).Select(t => t.Item.Kind + ":" + (t.Path ?? t.Title) + (ReferenceEquals(t, host.Workspace.ActiveGroup.ActiveTab) ? "*" : "")))}]";
                    failures.Add($"{cell}: {string.Join("; ", mismatch)}{tabs}");
                }
            }
        });
        Assert.True(failures.Count == 0, $"{failures.Count} of {driven} cells diverge from the model (unreachable {unreachable.Count}):\n{string.Join("\n", failures)}");
        if (only.Length > 0)
        {
            Assert.True(driven > 0, $"the narrowing [{string.Join(", ", only)}] matched no cell");
            return;
        }
        Assert.True(
            unreachable.Count == ReRootUnreachable && driven == ReRootDriven,
            $"the model named {unreachable.Count} cells as not states of the system and drove {driven}; pinned {ReRootUnreachable} and {ReRootDriven}");
    }
}
