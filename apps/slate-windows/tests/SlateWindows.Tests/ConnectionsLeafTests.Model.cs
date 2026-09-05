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
/// SPEAKING at apply — over every worked route crossed with the pane,
/// leaf, root and presentation states and the in-flight flag. Every
/// reachable cell arranges a fresh workspace in that state, drives it
/// through the route, and asserts the recorded timeline, the loads issued
/// and the final root and staleness equal the derivation. A route or a
/// state the model cannot derive fails the fact; a worked row is never
/// the contract.
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
        RootToNone,
        TasksReview,
        FocusEnter,
    }

    private sealed record Cell(Pane Pane, LeafState Leaf, RootState Root, Presentation Presentation, bool InFlight, Route Route)
    {
        public override string ToString() =>
            $"{Route} from [{Pane}, {Leaf}, {Root}, {Presentation}{(InFlight ? ", in flight" : "")}]";
    }

    /// <summary>The derivation: the timeline (shell lines then relay lines,
    /// a summary written as the placeholder resolved after the settle), the
    /// loads the route issues synchronously, and the final root and
    /// staleness.</summary>
    private sealed record Derivation(string[] Shell, string[] Relay, int Loads, bool RootIsNull, bool? Stale);

    private const string SummaryPlaceholder = "<summary>";
    private const string SkippedRoute = "<no-op>";

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
        // silences it (IGH-14).
        switch (cell.Route)
        {
            case Route.SwitchToLeaf:
                if (active)
                {
                    // A same-leaf switch is the setter's no-op; the in-flight
                    // completion then speaks, the leaf being active.
                    return new([], cell.InFlight ? summary : [], 0, !hasRoot, hasRoot ? stale : null);
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
                    return new([], [], 0, !hasRoot, hasRoot ? stale : null);
                }
                // The completion, when one is in flight, applies silently.
                return new(["LeafPanelShown"], [], 0, !hasRoot, hasRoot ? stale : null);
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
                    return new(["RightPaneHidden"], cell.InFlight && active ? summary : [], 0, !hasRoot, hasRoot ? stale : null);
                }
                {
                    // Term 3(a): a MOUNT with the leaf active loads, current or
                    // not; with another leaf active nothing loads and a
                    // completion in flight applies silently.
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
                    return new(hasRoot ? [] : ["TabFocused"], loads == 1 ? summary : [], loads, false, loads == 0);
                }
            case Route.DepthChange:
                {
                    // Term 3(e): a load with a root; spoken iff SPEAKING at apply
                    // (the active leaf, mounted or not), installed either way.
                    int loads = hasRoot ? 1 : 0;
                    bool speaks = loads == 1 && active;
                    return new([], speaks ? summary : [], loads, !hasRoot, hasRoot ? false : null);
                }
            case Route.RootToNone:
                if (!hasRoot)
                {
                    return new([], [], 0, true, null);
                }
                // Term 3(g): NoNote synchronously, the in-flight result dropped.
                return new(["TabClosed"], [], 0, true, null);
            case Route.TasksReview:
                {
                    // The reveal-then-switch command: MOUNT is evaluated at the
                    // route's end with the review active — no Connections load.
                    var shell = new List<string>();
                    if (!mounted)
                    {
                        shell.Add("RightPaneShown");
                    }
                    shell.Add("LeafPanelShown");
                    shell.Add("TasksReviewShown");
                    return new([.. shell], [], 0, !hasRoot, hasRoot ? stale && !cell.InFlight : null);
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
            default:
                throw new InvalidOperationException($"the model derives no route {cell.Route}");
        }
    }

    /// <summary>Arrange the workspace in the cell's state, from a fresh
    /// vault: the leaf activated and the note opened while ACTIVE and
    /// MOUNTED for a current presentation (then moved away or hidden), the
    /// note opened while inactive for a stale one, the last load left
    /// pending for the in-flight flag.</summary>
    private static void Arrange(Host host, Cell cell)
    {
        host.Workspace.ActiveLeaf = Host.OutlineLeaf;
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
            host.OpenNote(Hub);
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
        host.OpenNote(Hub);
        if (!cell.InFlight)
        {
            host.Settle();
        }
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
            case Route.FocusEnter:
                host.Leaf.FocusEntered();
                break;
            default:
                throw new InvalidOperationException($"no driver for {cell.Route}");
        }
    }

    private static IEnumerable<Cell> Cells()
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
                            foreach (Route route in Enum.GetValues<Route>())
                            {
                                yield return new Cell(pane, leaf, root, presentation, inFlight, route);
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TheModelOfTermsTwoToNineDerivesEveryRoutesTimelineAcrossEveryState()
    {
        var failures = new List<string>();
        var unreachable = new List<string>();
        int driven = 0;
        PumpedDispatcher.Run(() =>
        {
            foreach (Cell cell in Cells())
            {
                if (Unreachable(cell) is { } reason)
                {
                    unreachable.Add($"{cell}: {reason}");
                    continue;
                }
                Derivation expected = Derive(cell);
                using GraphVault vault = GraphVault.Copy($"model-{driven}");
                using var host = new Host(vault.Root);
                Arrange(host, cell);
                host.Clear();
                int before = host.Loads;

                Drive(host, cell);
                int loads = host.Loads - before;
                host.Settle();
                driven++;

                string[] timeline = [.. expected.Shell, .. expected.Relay.Select(line => line == SummaryPlaceholder ? Summary(host.Leaf) : line)];
                var mismatch = new List<string>();
                if (loads != expected.Loads)
                {
                    mismatch.Add($"loads {loads}, derived {expected.Loads}");
                }
                if (!timeline.SequenceEqual(host.Timeline))
                {
                    mismatch.Add($"timeline [{string.Join(" | ", host.Timeline)}], derived [{string.Join(" | ", timeline)}]");
                }
                if ((host.Leaf.Root is null) != expected.RootIsNull)
                {
                    mismatch.Add($"root {(host.Leaf.Root is null ? "none" : host.Leaf.Root)}, derived {(expected.RootIsNull ? "none" : "a note")}");
                }
                if (expected.Stale is { } stale && host.Leaf.Root is not null && host.Leaf.IsStale != stale)
                {
                    mismatch.Add($"stale {host.Leaf.IsStale}, derived {stale}");
                }
                if (mismatch.Count > 0)
                {
                    failures.Add($"{cell}: {string.Join("; ", mismatch)}");
                }
            }
        });
        Assert.True(driven >= 100, $"the model drove only {driven} cells; unreachable:\n{string.Join("\n", unreachable)}");
        Assert.True(failures.Count == 0, $"{failures.Count} of {driven} cells diverge from the model:\n{string.Join("\n", failures)}");
    }
}
