// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Automation;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR D (#746), rule G (contracts D-2..D-6): the build through the
/// scheduler, the model constructed on the pool around the fresh handle,
/// the epoch's one topology crossing, the driver's one tick per step, Reduce
/// Motion's one converged frame, the settle line when armed, the refresh
/// through the probe with RefreshAgain consumed on every terminal path, the
/// rebuild on a backend-filter change, and the teardown behind ONE gate —
/// the handle freed exactly once, by the retirement or by the last admitted
/// call's return, never thrown at. Every fact runs under the pumped
/// dispatcher on an STA thread over a fixture vault.
/// </summary>
public sealed partial class GraphDiagramTests
{
    /// <summary>A compute parked on a test seam: every hit in [from, until]
    /// signals the fact and waits for the release; the others pass.</summary>
    private sealed class Park(int from = 1, int until = int.MaxValue) : IDisposable
    {
        private readonly ManualResetEventSlim _reached = new(false);
        private readonly ManualResetEventSlim _gate = new(false);
        private int _hits;

        internal int Hits => Volatile.Read(ref _hits);

        internal void Hit()
        {
            int n = Interlocked.Increment(ref _hits);
            if (n < from || n > until)
            {
                return;
            }
            _reached.Set();
            Assert.True(_gate.Wait(TimeSpan.FromSeconds(30)), "the park was never released");
        }

        internal void WaitReached() => Assert.True(_reached.Wait(TimeSpan.FromSeconds(15)), "the compute never parked");

        internal void Release() => _gate.Set();

        /// <summary>Releases; the events are left to the collector — a parked
        /// pool thread may still be waking from the gate when the fact's
        /// block ends.</summary>
        public void Dispose() => _gate.Set();
    }

    private static long LayoutSessionsLive() => SlateUniffiMethods.CensusLiveObjectCounts().LayoutSessions;

    /// <summary>The counter at rest: earlier garbage finalised first, so a
    /// natural collection mid-fact cannot move it (the lifetime census's rule).</summary>
    private static long LayoutSessionsAtRest()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return LayoutSessionsLive();
    }

    /// <summary>The build landed and its settle converged, the scheduler drained.</summary>
    internal static GraphDiagramModel SettledModel(Host host, GraphDocumentViewModel document)
    {
        Assert.True(
            PumpedDispatcher.PumpUntil(() => document.HasLiveDiagram || document.DiagramError is not null, TimeSpan.FromSeconds(30)),
            "the build never landed");
        Assert.Null(document.DiagramError);
        GraphDiagramModel model = document.DiagramModel!;
        WaitForTheSettle(host, document, model);
        return model;
    }

    private static void WaitForTheSettle(Host host, GraphDocumentViewModel document, GraphDiagramModel model)
    {
        Assert.True(PumpedDispatcher.PumpUntil(() => !model.Driver.IsSettling, TimeSpan.FromSeconds(30)), "the settle never converged");
        host.Settle(document);
    }

    private static GraphDocumentViewModel BareDocument(Host host, GraphMotionPolicy policy, List<string> lines)
    {
        var viewState = new GraphViewState();
        return new GraphDocumentViewModel(
            host.Session,
            new GraphAnnouncer(line => lines.Add(line.Text)),
            viewState,
            isEffectiveActive: () => true,
            verbosity: () => GraphVerbosity.Standard,
            motionPolicy: policy);
    }

    private static void Settle(GraphDocumentViewModel document)
    {
        PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained());
        PumpedDispatcher.Drain();
    }

    private static string SettledLine() => Render(new GraphA11yEvent.GraphLayoutSettled());

    /// <summary>A run ends at the predicate or at the kernel's ceiling (a
    /// multi-node graph may jitter at the temperature floor and never meet
    /// the predicate — graph_layout.rs's own note; TGD-2's deviation).</summary>
    private static void AssertSettled(GraphDiagramModel model)
    {
        Assert.NotNull(model.LastFrame);
        Assert.True(
            model.LastFrame!.Converged
                || model.Driver.LastRunEndedAtTheCeilingForTests
                || model.LastFrame.Iteration >= GraphLayoutDriver.MaxIterationsPerRun,
            "the run neither converged nor reached the ceiling");
    }

    // --- D-2: the build, the states, the crossings (Terms G2, G8) ----------------

    [Fact]
    public void EnteringDiagramBuildsOneLayoutFromTheViewStatesFilterAndThePersistedForces()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-build");
            GraphDocumentViewModel document = host.Open();
            long baseline = LayoutSessionsAtRest();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            Assert.True(document.DiagramLoading);
            GraphDiagramModel model = SettledModel(host, document);
            Assert.False(document.DiagramLoading);
            Assert.True(document.HasLiveDiagram);
            Assert.True(document.IsDiagramEffective);
            // Term G8: the five crossings once.
            Assert.Equal(1, document.CrossingsForTests["start_graph_layout"]);
            foreach (string read in new[] { "layout_node_ids", "layout_edges", "layout_node_metadata", "layout_generation" })
            {
                Assert.Equal(1, document.CrossingsForTests[read]);
            }
            // The view state's filter, the persisted forces (the mac's `:40`).
            Assert.Equal(document.ViewState.Filter, model.Filter);
            Assert.Equal(GraphDocumentViewModel.ForcesOf(host.Workspace.GraphPreferences.CurrentConfig.Forces), model.Forces);
            // One model, two projections: the layout's node set is the table's.
            Assert.Equal(document.Publication.Rows.Count, model.NodeIds.Length);
            Assert.Equal(model.NodeIds.Length, model.NodesById.Count);
            Assert.Equal(baseline + 1, LayoutSessionsLive());
        });
    }

    [Fact]
    public void ABuildLandsOnlyForItsSequenceAndDiagramMode()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-build-sequence");
            GraphDocumentViewModel document = host.Open();
            long baseline = LayoutSessionsAtRest();
            // A switch back to Table during the build: the landed session disposed.
            using (var park = new Park())
            {
                document.FetchGateForTests = park.Hit;
                Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
                park.WaitReached();
                Assert.True(document.SetMode(GraphSurfaceMode.Table));
                park.Release();
                host.Settle(document);
            }
            Assert.Null(document.DiagramModel);
            Assert.False(document.HasLiveDiagram);
            Assert.False(document.DiagramLoading);
            Assert.Equal(1, document.CrossingsForTests["start_graph_layout"]);
            Assert.Equal(baseline, LayoutSessionsLive());

            // A second Enter supersedes the first's build: the first refused at
            // its apply, the second installed, one session live.
            using (var park = new Park(from: 1, until: 1))
            {
                document.FetchGateForTests = park.Hit;
                Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
                park.WaitReached();
                document.EnterDiagram();
                park.Release();
            }
            document.FetchGateForTests = null;
            GraphDiagramModel model = SettledModel(host, document);
            Assert.Equal(3, document.CrossingsForTests["start_graph_layout"]);
            Assert.Equal(baseline + 1, LayoutSessionsLive());
            Assert.True(model.BuildSequence >= 3);
        });
    }

    [Fact]
    public void AFailedBuildInstallsTheErrorStateAndNoModel()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-build-failed");
            GraphDocumentViewModel document = host.Open();
            GraphSurfaceView surface = SurfaceFor(host, document);
            using HostedWindow window = HostInWindow(surface);
            long baseline = LayoutSessionsAtRest();
            document.FetchGateForTests = () => throw new VaultException.Io("disk gone");
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            Assert.True(PumpedDispatcher.PumpUntil(() => document.DiagramError is not null), "the failure never landed");
            host.Settle(document);
            window.UpdateLayout();
            Assert.Equal("disk gone", document.DiagramError);
            Assert.False(document.DiagramLoading);
            Assert.False(document.HasLiveDiagram);
            Assert.Null(document.DiagramModel);
            Assert.False(document.IsDiagramEffective);
            // T19: the state host reads the failure under the prefix.
            Assert.Equal("disk gone", surface.StateTextForTests.Text);
            Assert.Equal(GraphPhrase.DiagramErrorPrefix + "disk gone", AutomationProperties.GetName(surface.StateHostForTests));
            // The handle the failed build had opened is freed.
            Assert.Equal(baseline, LayoutSessionsLive());
        });
    }

    [Fact]
    public void TheBuildReadsNoTableSnapshot()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-build-no-snapshot");
            GraphDocumentViewModel document = host.Open();
            // The table under ERROR.
            document.FetchGateForTests = () => throw new VaultException.Io("table down");
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            host.Settle(document);
            Assert.Equal(GraphLoadState.Error, document.Publication.State);
            document.FetchGateForTests = null;
            int snapshots = document.CrossingsForTests["graph_snapshot"];
            int rows = document.CrossingsForTests["graph_table_rows"];
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            // The layout snapshots the graph itself: no table crossing, and the
            // table's ERROR is not consulted.
            Assert.True(document.HasLiveDiagram);
            Assert.NotEmpty(model.NodeIds);
            Assert.Equal(snapshots, document.CrossingsForTests["graph_snapshot"]);
            Assert.Equal(rows, document.CrossingsForTests["graph_table_rows"]);
            Assert.Equal(GraphLoadState.Error, document.Publication.State);
        });
    }

    [Fact]
    public void AFilterChangeDuringTheBuildRefusesItAndRebuildsOnce()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-build-filter-change");
            GraphDocumentViewModel document = host.Open();
            long baseline = LayoutSessionsAtRest();
            using (var park = new Park(from: 1, until: 1))
            {
                document.FetchGateForTests = park.Hit;
                Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
                park.WaitReached();
                // The preset run while the first build is parked: the backend
                // filter changes under an in-flight build (Term G6).
                host.Workspace.GraphNavigator.RunPreset(GraphPreset.Orphans);
                Assert.True(document.ViewState.Filter.OrphansOnly);
                park.Release();
            }
            document.FetchGateForTests = null;
            GraphDiagramModel model = SettledModel(host, document);
            // One more StartGraphLayout under the preset's filter; the first's
            // session disposed; the model's filter the preset's (IGQ-2).
            Assert.Equal(2, document.CrossingsForTests["start_graph_layout"]);
            Assert.True(model.Filter.OrphansOnly);
            Assert.Equal(document.ViewState.Filter, model.Filter);
            Assert.Equal(baseline + 1, LayoutSessionsLive());
        });
    }

    [Fact]
    public void AForcesEditDuringTheBuildIsAppliedAtTheInstall()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-build-forces");
            GraphDocumentViewModel document = host.Open();
            GraphForcesConfig before = host.Workspace.GraphPreferences.CurrentConfig.Forces;
            GraphForcesConfig edited = before with { Repel = before.Repel + 0.25, LinkDistance = before.LinkDistance + 10 };
            using (var park = new Park(from: 1, until: 1))
            {
                document.FetchGateForTests = park.Hit;
                Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
                park.WaitReached();
                host.Workspace.GraphPreferences.SetForces(edited);
                park.Release();
            }
            document.FetchGateForTests = null;
            GraphDiagramModel model = SettledModel(host, document);
            // Re-read at the install and applied through the gate: PR E's edit
            // during a build is not lost.
            Assert.Equal(1, document.CrossingsForTests["layout_set_forces"]);
            Assert.Equal(GraphDocumentViewModel.ForcesOf(edited), model.Forces);
            Assert.Equal(1, document.CrossingsForTests["start_graph_layout"]);
        });
    }

    [Fact]
    public void AWithdrawnBuildApplyLeavesNoHandle()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-build-withdrawn");
            GraphDocumentViewModel document = host.Open();
            long baseline = LayoutSessionsAtRest();
            using var park = new Park();
            // Parked AFTER the registration in the unseated set (IGT-1).
            document.DiagramRegisteredGateForTests = park.Hit;
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            park.WaitReached();
            Assert.Equal(baseline + 1, LayoutSessionsLive());
            // The workspace retires the document: the sweep retires the
            // registered build under the same lock; the apply is withdrawn.
            host.Workspace.CloseActiveTabCommand.Execute(null);
            Assert.True(document.IsRetired);
            Assert.Equal(baseline, LayoutSessionsLive());
            park.Release();
            host.Settle(document);
            Assert.Null(document.DiagramModel);
            Assert.False(document.HasLiveDiagram);
            Assert.Equal(baseline, LayoutSessionsLive());
        });
    }

    [Fact]
    public void ABuildThatReturnsIntoARetiredDocumentRetiresItself()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-build-retired");
            GraphDocumentViewModel document = host.Open();
            long baseline = LayoutSessionsAtRest();
            using var park = new Park();
            // Parked BEFORE the registration (the fetch gate): the handle is open.
            document.FetchGateForTests = park.Hit;
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            park.WaitReached();
            Assert.Equal(baseline + 1, LayoutSessionsLive());
            host.Workspace.CloseActiveTabCommand.Execute(null);
            Assert.True(document.IsRetired);
            // Released into a retired document: the compute's own retirement
            // frees the handle in the gate (IGT-1).
            park.Release();
            host.Settle(document);
            Assert.Null(document.DiagramModel);
            Assert.Equal(baseline, LayoutSessionsLive());
        });
    }

    [Fact]
    public void NoCrossingHappensPerFrameOrPan()
    {
        RunSta(() =>
        {
            using var host = new Host(6, "diagram-no-per-frame-crossing");
            GraphDocumentViewModel document = host.Open();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            // A settle of N steps costs N ticks and ONE topology; every frame
            // was current and applied.
            Assert.True(model.Driver.StepsForTests >= 1);
            Assert.Equal(model.Driver.StepsForTests, document.CrossingsForTests["layout_tick"]);
            Assert.Equal(model.Driver.StepsForTests, model.Driver.FramesAppliedForTests);
            Assert.Equal(0, model.Driver.FramesDroppedForTests);
            Assert.Equal(1, document.CrossingsForTests["graph_topology"]);
            Assert.Equal(0, document.CrossingsForTests["layout_run_to_convergence"]);
            Assert.NotNull(model.LastFrame);
            AssertSettled(model);
            Assert.Equal(model.NodeIds.Length, model.Positions.Count);
        });
    }

    // --- D-3: the epoch and the topology (Term G3) --------------------------------

    [Fact]
    public void TheTopologyIsFetchedOncePerEpochWhileSettling()
    {
        RunSta(() =>
        {
            using var host = new Host(5, "diagram-epoch");
            GraphDocumentViewModel document = host.Open();
            int topologies = 0;
            document.DiagramTopologyChanged += () => topologies++;
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            Assert.Equal(1, document.CrossingsForTests["graph_topology"]);
            Assert.Equal(1, topologies);
            Assert.NotNull(model.Topology);
            Assert.Equal(model.Generation, model.Topology!.Generation);
            Assert.Equal(model.NodeIds.Length, model.Topology.Nodes.Length);

            // A needle: a new epoch.
            document.ViewState.NameQuery = "note1";
            host.Settle(document);
            Assert.Equal(2, document.CrossingsForTests["graph_topology"]);
            Assert.Single(model.Topology!.Nodes);
            // A kind overlay: a new epoch (the filter and the needle unchanged).
            document.ViewState.ApplyQuery(new GraphVisibilityQuery(document.ViewState.Filter, "note1", GraphNodeKind.Note));
            host.Settle(document);
            Assert.Equal(3, document.CrossingsForTests["graph_topology"]);
            // A groups change: a new epoch (IGT-4).
            document.ViewState.Groups = [new GraphGroup("tag:test", GraphColorToken.Red, GraphRingStyle.Solid)];
            host.Settle(document);
            Assert.Equal(4, document.CrossingsForTests["graph_topology"]);
            Assert.Equal(4, topologies);
            // A VERBOSITY change crosses nothing (the peers re-name locally).
            GraphVerbositySpec other = host.Workspace.GraphPreferences.Choices.First(c => c.Spec.Verbosity != host.Workspace.GraphPreferences.Verbosity).Spec;
            host.Workspace.GraphPreferences.SetVerbosityCommand.Execute(other.Tag);
            host.Settle(document);
            Assert.Equal(4, document.CrossingsForTests["graph_topology"]);
            // The same key again: nothing.
            document.ViewState.Groups = [new GraphGroup("tag:test", GraphColorToken.Red, GraphRingStyle.Solid)];
            host.Settle(document);
            Assert.Equal(4, document.CrossingsForTests["graph_topology"]);
            Assert.Same(model, document.DiagramModel);
            Assert.Equal(1, document.CrossingsForTests["start_graph_layout"]);
        });
    }

    [Fact]
    public void ATopologyFromAnotherGenerationIsDroppedAndTheSetEmptiesUntilTheRefreshAdopts()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-epoch-dropped");
            GraphDocumentViewModel document = host.Open();
            using var park = new Park(from: 2);
            // Every compute after the build's parks: the epoch's fetch and the
            // first step wait while a note is saved.
            document.BeforeComputeForTests = park.Hit;
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            Assert.True(PumpedDispatcher.PumpUntil(() => document.HasLiveDiagram, TimeSpan.FromSeconds(30)), "the build never landed");
            park.WaitReached();
            _ = host.Session.CreateExclusive("iota.md", "# Iota\n\n[[note0]]\n");
            document.BeforeComputeForTests = null;
            park.Release();
            GraphDiagramModel model = document.DiagramModel!;
            WaitForTheSettle(host, document, model);
            // The fetch answered from a newer generation: dropped, the set empty.
            Assert.Equal(1, document.CrossingsForTests["graph_topology"]);
            Assert.Null(model.Topology);
            ulong built = model.Generation;
            // The refresh adopts, opens a new epoch, and the topology lands.
            document.Probe();
            host.Settle(document);
            WaitForTheSettle(host, document, model);
            Assert.Equal(1, document.CrossingsForTests["layout_refresh"]);
            Assert.True(model.Generation > built);
            Assert.NotNull(model.Topology);
            Assert.Equal(model.Generation, model.Topology!.Generation);
            Assert.Equal(2, document.CrossingsForTests["graph_topology"]);
            Assert.Contains(model.NodesById.Values, node => node.Path == "iota.md");
            Assert.Same(model, document.DiagramModel);
        });
    }

    [Fact]
    public void AQueryUnderAnotherFilterIsNeverIssuedAgainstTheModel()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-epoch-filter");
            GraphDocumentViewModel document = host.Open();
            long baseline = LayoutSessionsAtRest();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel first = SettledModel(host, document);
            Assert.Equal(1, document.CrossingsForTests["graph_topology"]);
            GraphTopology? held = first.Topology;
            // The preset's ApplyQuery changes the backend filter: a REBUILD —
            // the old model torn down, no topology issued against it.
            host.Workspace.GraphNavigator.RunPreset(GraphPreset.Orphans);
            Assert.True(first.IsRetired);
            GraphDiagramModel second = SettledModel(host, document);
            Assert.NotSame(first, second);
            Assert.True(second.Filter.OrphansOnly);
            Assert.Equal(2, document.CrossingsForTests["start_graph_layout"]);
            // One topology for the new model's first epoch, none for the old.
            Assert.Equal(2, document.CrossingsForTests["graph_topology"]);
            Assert.Same(held, first.Topology);
            Assert.True(first.IsHandleFreedForTests);
            Assert.Equal(baseline + 1, LayoutSessionsLive());
        });
    }

    // --- D-4: the driver, the frames, Reduce Motion, the settle line (Terms G4, G5) ---

    [Fact]
    public void ASettleIsOneTickPerStepThroughTheSchedulerUntilConverged()
    {
        RunSta(() =>
        {
            using var host = new Host(8, "diagram-settle-steps");
            GraphDocumentViewModel document = host.Open();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            Assert.False(model.Driver.IsSettling);
            Assert.False(model.Driver.LastRunWasReduceMotionForTests);
            Assert.Equal(model.Driver.StepsForTests, document.CrossingsForTests["layout_tick"]);
            Assert.Equal(0, document.CrossingsForTests["layout_run_to_convergence"]);
            AssertSettled(model);
            // Each step ticked at most twenty iterations (a converged step
            // stops early); the ceiling bounds the run.
            Assert.True(model.LastFrame!.Iteration <= GraphLayoutDriver.IterationsPerStep * (uint)model.Driver.StepsForTests);
            Assert.True(model.Driver.StepsForTests <= GraphLayoutDriver.MaxStepsPerRun);
        });
    }

    [Fact]
    public void AFrameFromAnotherGenerationOrLengthIsDropped()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-frame-currency");
            GraphDocumentViewModel document = host.Open();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            int applied = model.Driver.FramesAppliedForTests;
            IReadOnlyDictionary<ulong, GraphPoint> positions = model.Positions;
            LayoutFrame current = model.LastFrame!;
            // Another generation: dropped.
            Assert.False(model.Driver.ApplyForTests(current with { Generation = current.Generation + 1 }));
            // A buffer that does not pair with the ids: dropped.
            Assert.False(model.Driver.ApplyForTests(current with { Positions = [.. current.Positions, 0f] }));
            Assert.Equal(applied, model.Driver.FramesAppliedForTests);
            Assert.Equal(2, model.Driver.FramesDroppedForTests);
            Assert.Same(positions, model.Positions);
            // The current generation and length: applied.
            Assert.True(model.Driver.ApplyForTests(current with { Iteration = current.Iteration + 1 }));
            Assert.Equal(applied + 1, model.Driver.FramesAppliedForTests);
            Assert.NotSame(positions, model.Positions);
        });
    }

    [Fact]
    public void ReduceMotionAppliesOneConvergedFrame()
    {
        RunSta(() =>
        {
            using var host = new Host(6, "diagram-reduce-motion");
            var lines = new List<string>();
            var policy = new GraphMotionPolicy(() => true);
            GraphDocumentViewModel document = BareDocument(host, policy, lines);
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            Assert.True(PumpedDispatcher.PumpUntil(() => document.HasLiveDiagram, TimeSpan.FromSeconds(30)), "the build never landed");
            GraphDiagramModel model = document.DiagramModel!;
            Assert.True(PumpedDispatcher.PumpUntil(() => !model.Driver.IsSettling, TimeSpan.FromSeconds(30)), "the converge never returned");
            Settle(document);
            Assert.True(model.Driver.LastRunWasReduceMotionForTests);
            Assert.Equal(1, document.CrossingsForTests["layout_run_to_convergence"]);
            Assert.Equal(0, document.CrossingsForTests["layout_tick"]);
            Assert.Equal(1, model.Driver.ConvergeRunsForTests);
            Assert.Equal(0, model.Driver.StepsForTests);
            Assert.Equal(1, model.Driver.FramesAppliedForTests);
            AssertSettled(model);
            Assert.Equal(model.NodeIds.Length, model.Positions.Count);
            document.Retire();
            Settle(document);
            Assert.True(model.IsHandleFreedForTests);
        });
    }

    [Fact]
    public void AMotionFlipWhileSettlingRestartsTheSettle()
    {
        RunSta(() =>
        {
            using var host = new Host(6, "diagram-motion-flip");
            var lines = new List<string>();
            bool reduce = false;
            var policy = new GraphMotionPolicy(() => reduce);
            GraphDocumentViewModel document = BareDocument(host, policy, lines);
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            Assert.True(PumpedDispatcher.PumpUntil(() => document.HasLiveDiagram, TimeSpan.FromSeconds(30)), "the build never landed");
            GraphDiagramModel model = document.DiagramModel!;
            Assert.True(PumpedDispatcher.PumpUntil(() => !model.Driver.IsSettling, TimeSpan.FromSeconds(30)), "the settle never converged");
            Settle(document);
            int ticks = document.CrossingsForTests["layout_tick"];
            int applied = model.Driver.FramesAppliedForTests;

            // A re-settle held on its first tick INSIDE the gate; the preference
            // flips meanwhile: the run restarts under Reduce Motion — the held
            // tick's frame is the cancelled run's and is not applied.
            using var park = new Park(from: 1, until: 1);
            model.InsideGateForTests = park.Hit;
            model.Driver.StartSettle();
            park.WaitReached();
            Assert.True(model.Driver.IsSettling);
            reduce = true;
            policy.Flip();
            PumpedDispatcher.Drain();
            model.InsideGateForTests = null;
            park.Release();
            Assert.True(PumpedDispatcher.PumpUntil(() => !model.Driver.IsSettling, TimeSpan.FromSeconds(30)), "the restarted settle never converged");
            Settle(document);
            Assert.True(model.Driver.LastRunWasReduceMotionForTests);
            Assert.Equal(1, model.Driver.ConvergeRunsForTests);
            Assert.Equal(1, document.CrossingsForTests["layout_run_to_convergence"]);
            Assert.Equal(ticks + 1, document.CrossingsForTests["layout_tick"]);
            Assert.Equal(applied + 1, model.Driver.FramesAppliedForTests);
            AssertSettled(model);

            // A flip while NOT settling restarts nothing.
            reduce = false;
            policy.Flip();
            PumpedDispatcher.Drain();
            Assert.False(model.Driver.IsSettling);
            Settle(document);
            Assert.Equal(ticks + 1, document.CrossingsForTests["layout_tick"]);
            document.Retire();
            Settle(document);
        });
    }

    [Fact]
    public void TheSettledLineSpeaksOnlyWhenArmedAndTeardownDisarms()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-settled-line");
            GraphDocumentViewModel document = host.Open();
            host.GraphLines.Clear();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            // The build's own convergence: the mode line alone.
            Assert.Equal([ModeLine(GraphSurfaceMode.Diagram)], host.GraphLines);

            // Armed (PR E's forces edit), then a re-settle converges: ONE line.
            host.GraphLines.Clear();
            document.SettleAnnouncementArmed = true;
            model.Driver.StartSettle();
            WaitForTheSettle(host, document, model);
            Assert.True(PumpedDispatcher.PumpUntil(() => host.GraphLines.Count > 0, TimeSpan.FromSeconds(10)), "the settled line never fired");
            host.Settle(document);
            Assert.Equal([SettledLine()], host.GraphLines);
            Assert.False(document.SettleAnnouncementArmed);

            // Armed, torn down, rebuilt: the teardown disarmed — no settled line.
            host.GraphLines.Clear();
            document.SettleAnnouncementArmed = true;
            Assert.True(document.SetMode(GraphSurfaceMode.Table));
            Assert.False(document.SettleAnnouncementArmed);
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            _ = SettledModel(host, document);
            PumpedDispatcher.PumpUntil(() => false, TimeSpan.FromMilliseconds(400));
            Assert.Equal([ModeLine(GraphSurfaceMode.Table), ModeLine(GraphSurfaceMode.Diagram)], host.GraphLines);
        });
    }

    [Fact]
    public void ACancelledRunAppliesNothingAndTheSessionIsFreedWhenTheInFlightCallReturns()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-cancelled-run");
            GraphDocumentViewModel document = host.Open();
            long baseline = LayoutSessionsAtRest();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            int applied = model.Driver.FramesAppliedForTests;
            using var park = new Park(from: 1, until: 1);
            model.InsideGateForTests = park.Hit;
            model.Driver.StartSettle();
            park.WaitReached();
            Assert.Equal(1, model.InFlightForTests);
            // The teardown: the run cancelled, the gate retired with the call
            // admitted — the handle waits for the return.
            Assert.True(document.SetMode(GraphSurfaceMode.Table));
            Assert.True(model.IsRetired);
            Assert.False(model.IsHandleFreedForTests);
            Assert.Equal(1, model.InFlightForTests);
            Assert.Equal(baseline + 1, LayoutSessionsLive());
            model.InsideGateForTests = null;
            park.Release();
            host.Settle(document);
            // No frame from the cancelled run; the handle freed on the return.
            Assert.Equal(applied, model.Driver.FramesAppliedForTests);
            Assert.Equal(0, model.InFlightForTests);
            Assert.True(model.IsHandleFreedForTests);
            Assert.Equal(baseline, LayoutSessionsLive());
        });
    }

    [Fact]
    public void AStepQueuedBeforeTeardownThatStartsAfterItIsRefusedAtTheGateAndTheDrainCompletes()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-step-refused");
            GraphDocumentViewModel document = host.Open();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            int ticks = document.CrossingsForTests["layout_tick"];
            int applied = model.Driver.FramesAppliedForTests;
            using var park = new Park();
            // The step's compute parked BEFORE its gate call (the scheduler's seam).
            document.BeforeComputeForTests = park.Hit;
            model.Driver.StartSettle();
            park.WaitReached();
            // The teardown: nothing admitted, the handle freed at once.
            Assert.True(document.SetMode(GraphSurfaceMode.Table));
            Assert.True(model.IsRetired);
            Assert.True(model.IsHandleFreedForTests);
            Assert.Equal(0, model.InFlightForTests);
            document.BeforeComputeForTests = null;
            park.Release();
            // The call is refused, the apply applies nothing, the drain completes
            // WITHOUT a fault (IGR-1, IGS-1).
            PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
            Assert.Equal(ticks, document.CrossingsForTests["layout_tick"]);
            Assert.Equal(applied, model.Driver.FramesAppliedForTests);
            // The gate's refusal, in the tick's own shape.
            Assert.Null(model.WithSession(session => session.Tick(1), null));
            Assert.Equal(ticks, document.CrossingsForTests["layout_tick"]);
        });
    }

    [Fact]
    public void AnAdmittedCallOutlivesTheTeardownAndFreesTheHandleOnReturn()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-admitted-call");
            GraphDocumentViewModel document = host.Open();
            long baseline = LayoutSessionsAtRest();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            using var park = new Park(from: 1, until: 1);
            model.InsideGateForTests = park.Hit;
            model.Driver.StartSettle();
            park.WaitReached();
            Assert.True(document.SetMode(GraphSurfaceMode.Table));
            // The gate retired with the count at one; the handle stands.
            Assert.True(model.IsRetired);
            Assert.Equal(1, model.InFlightForTests);
            Assert.False(model.IsHandleFreedForTests);
            Assert.Equal(baseline + 1, LayoutSessionsLive());
            // A second call meanwhile is refused (nothing admitted after the retirement).
            Assert.Null(model.WithSession(session => session.Tick(1), null));
            Assert.Equal(1, model.InFlightForTests);
            model.InsideGateForTests = null;
            park.Release();
            host.Settle(document);
            Assert.Equal(0, model.InFlightForTests);
            Assert.True(model.IsHandleFreedForTests);
            Assert.Equal(baseline, LayoutSessionsAtRest());
        });
    }

    [Fact]
    public void EachRefusedShapeIsItsOwn()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-refused-shapes");
            GraphDocumentViewModel document = host.Open();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            ulong id = model.NodeIds[0];
            Assert.True(document.SetMode(GraphSurfaceMode.Table));
            Assert.True(model.IsRetired);
            var counts = new Dictionary<string, int>(document.CrossingsForTests);
            using var cancel = new CancelToken();
            // A null frame, a null refresh, a false pin, a false forces edit (IGS-1).
            Assert.Null(model.WithSession(session => session.Tick(1), null));
            Assert.Null(model.WithSession(session => session.RunToConvergence(cancel), null));
            Assert.Null(model.WithSession(session => session.Refresh(), null));
            Assert.False(model.TogglePin(id, 0f, 0f));
            Assert.False(model.SetForces(new LayoutForces(0.1f, 0.2f, 0.3f, 0.4f)));
            Assert.Empty(model.Pinned);
            // Nothing crossed: the gate refused before any lambda ran.
            Assert.Equal(counts, document.CrossingsForTests);
        });
    }

    [Fact]
    public void APinOrAForcesEditOnARetiredModelIsRefusedSilently()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-pin-retired");
            GraphDocumentViewModel document = host.Open();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            ulong id = model.NodeIds[0];
            var forces = new LayoutForces(0.4f, 0.5f, 0.6f, 0.7f);
            // Live: admitted through the gate, one crossing each.
            Assert.True(model.TogglePin(id, 1f, 2f));
            Assert.Contains(id, model.Pinned);
            Assert.Equal(1, document.CrossingsForTests["layout_pin_node"]);
            Assert.True(model.SetForces(forces));
            Assert.Equal(forces, model.Forces);
            Assert.Equal(1, document.CrossingsForTests["layout_set_forces"]);
            Assert.True(model.TogglePin(id, 1f, 2f));
            Assert.DoesNotContain(id, model.Pinned);
            Assert.Equal(1, document.CrossingsForTests["layout_unpin_node"]);

            Assert.True(document.SetMode(GraphSurfaceMode.Table));
            // Retired: refused, nothing changed, nothing thrown (IGS-2).
            Assert.False(model.TogglePin(id, 3f, 4f));
            Assert.Empty(model.Pinned);
            Assert.False(model.SetForces(new LayoutForces()));
            Assert.Equal(forces, model.Forces);
            Assert.Equal(1, document.CrossingsForTests["layout_pin_node"]);
            Assert.Equal(1, document.CrossingsForTests["layout_set_forces"]);
        });
    }

    // --- D-5: the refresh and the rebuild (Term G6) --------------------------------

    [Fact]
    public void AProbeThatMovedTheGenerationRefreshesTheLayoutAndAdoptsMonotonically()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-refresh-adopts");
            GraphDocumentViewModel document = host.Open();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            ulong built = model.Generation;
            int steps = model.Driver.StepsForTests;
            _ = host.Session.CreateExclusive("iota.md", "# Iota\n\n[[note0]]\n");
            document.Probe();
            host.Settle(document);
            WaitForTheSettle(host, document, model);
            // One Refresh and its three reads; the generation moved up; the
            // ids gained the node; the settle restarted; a new epoch landed.
            Assert.Equal(1, document.CrossingsForTests["layout_refresh"]);
            Assert.Equal(2, document.CrossingsForTests["layout_node_ids"]);
            Assert.Equal(2, document.CrossingsForTests["layout_edges"]);
            Assert.Equal(2, document.CrossingsForTests["layout_node_metadata"]);
            Assert.Equal(1, document.CrossingsForTests["layout_generation"]);
            Assert.True(model.Generation > built);
            Assert.Contains(model.NodesById.Values, node => node.Path == "iota.md");
            Assert.Equal(4, model.NodeIds.Length);
            Assert.True(model.Driver.StepsForTests > steps);
            Assert.Equal(2, document.CrossingsForTests["graph_topology"]);
            Assert.Equal(model.Generation, model.Topology!.Generation);
            Assert.Same(model, document.DiagramModel);
            Assert.Equal(1, document.CrossingsForTests["start_graph_layout"]);
        });
    }

    [Fact]
    public void AProbeWhoseGenerationEqualsTheModelsIssuesNoRefresh()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-refresh-none");
            GraphDocumentViewModel document = host.Open();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            int probes = document.CrossingsForTests["graph_generation"];
            document.Probe();
            host.Settle(document);
            Assert.Equal(probes + 1, document.CrossingsForTests["graph_generation"]);
            // Zero Refresh crossings: the probe's own comparison (IGQ-6).
            Assert.Equal(0, document.CrossingsForTests["layout_refresh"]);
            Assert.Same(model, document.DiagramModel);
            Assert.False(model.Driver.IsSettling);
        });
    }

    /// <summary>Four terminal paths named by D-5: an adopt, a null answer, a
    /// failure through the fetch gate, and the stale drop. The stale drop —
    /// an answer NOT newer than the model's generation — is unreachable by
    /// construction: one refresh is in flight per model and the kernel's
    /// refresh is monotonic, so its arm is pinned by the adopt's guard
    /// (`frame.Generation > model.Generation`) and the teardown's reset.</summary>
    [Fact]
    public void AProbeDuringARefreshRunsOneMoreRefreshAfterEveryTerminalPath()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-refresh-again");
            GraphDocumentViewModel document = host.Open();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);

            // (a) after an ADOPT: the second request during the first sets
            // RefreshAgain; the adopt consumes it with one more refresh.
            _ = host.Session.CreateExclusive("iota.md", "# Iota\n\n[[note0]]\n");
            ulong built = model.Generation;
            using (var park = new Park(from: 1, until: 1))
            {
                model.InsideGateForTests = park.Hit;
                document.RefreshDiagramForTests();
                park.WaitReached();
                document.RefreshDiagramForTests();
                model.InsideGateForTests = null;
                park.Release();
            }
            host.Settle(document);
            WaitForTheSettle(host, document, model);
            Assert.Equal(2, document.CrossingsForTests["layout_refresh"]);
            Assert.True(model.Generation > built);

            // (b) after a NULL answer (the vault unchanged): one more refresh.
            using (var park = new Park(from: 1, until: 1))
            {
                model.InsideGateForTests = park.Hit;
                document.RefreshDiagramForTests();
                park.WaitReached();
                document.RefreshDiagramForTests();
                model.InsideGateForTests = null;
                park.Release();
            }
            host.Settle(document);
            Assert.Equal(4, document.CrossingsForTests["layout_refresh"]);

            // (c) after a FAILURE injected through the fetch gate: logged, the
            // model stands, one more refresh (IGQ-3).
            bool fail = true;
            document.FetchGateForTests = () =>
            {
                if (fail)
                {
                    fail = false;
                    throw new VaultException.Io("refresh down");
                }
            };
            using (var park = new Park(from: 1, until: 1))
            {
                model.InsideGateForTests = park.Hit;
                document.RefreshDiagramForTests();
                park.WaitReached();
                document.RefreshDiagramForTests();
                model.InsideGateForTests = null;
                park.Release();
            }
            host.Settle(document);
            document.FetchGateForTests = null;
            Assert.False(fail);
            Assert.Equal(5, document.CrossingsForTests["layout_refresh"]);
            Assert.Same(model, document.DiagramModel);
            Assert.True(document.HasLiveDiagram);
        });
    }

    [Fact]
    public void ARefreshFailureIsLoggedAndTheModelStands()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-refresh-failure");
            GraphDocumentViewModel document = host.Open();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            ulong generation = model.Generation;
            TextWriter original = Console.Error;
            var captured = new StringWriter();
            Console.SetError(captured);
            try
            {
                document.FetchGateForTests = () => throw new VaultException.Io("refresh down");
                document.RefreshDiagramForTests();
                host.Settle(document);
            }
            finally
            {
                Console.SetError(original);
                document.FetchGateForTests = null;
            }
            Assert.Contains("SlateWindows.GraphLayoutRefreshFailed", captured.ToString());
            Assert.Same(model, document.DiagramModel);
            Assert.True(document.HasLiveDiagram);
            Assert.Equal(generation, model.Generation);
            Assert.False(model.IsRetired);
            // The lineage is live for the next probe.
            document.RefreshDiagramForTests();
            host.Settle(document);
            Assert.Equal(1, document.CrossingsForTests["layout_refresh"]);
        });
    }

    [Fact]
    public void ARefreshPrunesAPinTheTopologyLost()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-refresh-prunes-pin");
            GraphDocumentViewModel document = host.Open();
            // Ghosts OUT: a deleted note leaves no ghost in the projection, so
            // its id is one the topology loses (with ghosts in, the dangling
            // link's ghost stands where the note was).
            document.ViewState.Filter = new GraphFilter(IncludeAttachments: false, IncludeGhosts: false, OrphansOnly: false);
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            ulong lost = model.NodesById.Values.Single(node => node.Path == "note3.md").Id;
            ulong kept = model.NodesById.Values.Single(node => node.Path == "note0.md").Id;
            Assert.True(model.TogglePin(lost, 0f, 0f));
            Assert.True(model.TogglePin(kept, 0f, 0f));
            File.Delete(Path.Combine(host.Vault.Root, "note3.md"));
            using (var cancel = new CancelToken())
            {
                _ = host.Session.ScanInitial(cancel);
            }
            document.Probe();
            host.Settle(document);
            WaitForTheSettle(host, document, model);
            Assert.Equal(1, document.CrossingsForTests["layout_refresh"]);
            Assert.DoesNotContain(model.NodesById.Values, node => node.Path == "note3.md");
            Assert.DoesNotContain(lost, model.Pinned);
            // The kept note's pin SURVIVES by its stable key under whatever id
            // the refresh gave the note (IPH-2-1) — the one pin left.
            ulong keptNow = model.NodesById.Values.Single(node => node.Path == "note0.md").Id;
            Assert.Equal([keptNow], model.Pinned.ToArray());
            Assert.Contains(model.NodesById[keptNow].StableKey, model.PinnedKeys);
            Assert.All(model.Pinned, id => Assert.Contains(id, model.NodeIds));
            _ = kept;
        });
    }

    /// <summary>Term N7 / G6 (IPH-2-1): a pin is the NODE's, by stable key —
    /// a read that reassigns every id (core's id contract across a
    /// generation) carries the pin to the node's new id and never to the
    /// node that inherited the old one.</summary>
    [Fact]
    public void APinSurvivesAnIdReassignmentByItsStableKey()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-pin-by-key");
            GraphDocumentViewModel document = host.Open();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            ulong[] ids = model.NodeIds;
            Assert.True(ids.Length >= 2);
            ulong pinnedOld = ids[0];
            ulong otherOld = ids[1];
            string pinnedKey = model.NodesById[pinnedOld].StableKey;
            Assert.True(model.TogglePin(pinnedOld, 0f, 0f));
            Assert.Equal([pinnedOld], model.Pinned.ToArray());
            // Every id shifted by one slot: the OTHER node inherits the pinned
            // node's old id.
            var shifted = new Dictionary<ulong, ulong>();
            for (int i = 0; i < ids.Length; i++)
            {
                shifted[ids[i]] = ids[(i + 1) % ids.Length];
            }
            GraphNode[] nodes = [.. ids.Select(id => model.NodesById[id] with { Id = shifted[id] })];
            GraphEdge[] edges = [.. model.Edges.Select(edge => edge with { SourceId = shifted[edge.SourceId], TargetId = shifted[edge.TargetId] })];
            model.Adopt(new GraphDiagramTopologyRead([.. ids.Select(id => shifted[id])], edges, nodes, model.Generation + 1));
            ulong pinnedNew = shifted[pinnedOld];
            Assert.Equal(pinnedKey, model.NodesById[pinnedNew].StableKey);
            Assert.Equal([pinnedNew], model.Pinned.ToArray());
            Assert.DoesNotContain(pinnedOld, model.Pinned.Where(id => id != pinnedNew));
            Assert.Equal([pinnedKey], model.PinnedKeys.ToArray());
            _ = otherOld;
        });
    }

    /// <summary>Term G3 / D-3 (IPH-2-3): a failed topology fetch leaves the
    /// epoch UNFETCHED — logged, the previous set standing — and the next
    /// request for the same key fetches again.</summary>
    [Fact]
    public void AFailedTopologyFetchIsRetriedOnTheNextEpochRequest()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-topology-retry");
            GraphDocumentViewModel document = host.Open();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            Assert.Equal(1, document.CrossingsForTests["graph_topology"]);
            TextWriter original = Console.Error;
            var captured = new StringWriter();
            Console.SetError(captured);
            try
            {
                document.TopologyGateForTests = () => throw new VaultException.Io("topology down");
                document.ViewState.NameQuery = "note";
                host.Settle(document);
            }
            finally
            {
                Console.SetError(original);
                document.TopologyGateForTests = null;
            }
            // The failed fetch never counted the crossing (the gate threw
            // first); the failure is logged; the previous epoch's set stands.
            Assert.Contains("SlateWindows.GraphTopologyFetchFailed", captured.ToString());
            Assert.Equal(1, document.CrossingsForTests["graph_topology"]);
            Assert.NotNull(model.Topology);
            // The same key asked again fetches — an unfetched epoch is not a
            // fetched one.
            document.ReopenEpochForTests();
            host.Settle(document);
            Assert.Equal(2, document.CrossingsForTests["graph_topology"]);
            Assert.NotNull(model.Topology);
            // And a fetched key returns early.
            document.ReopenEpochForTests();
            host.Settle(document);
            Assert.Equal(2, document.CrossingsForTests["graph_topology"]);
        });
    }

    /// <summary>Term G5 / G6 (IPH-2-4): a refresh's answer adopts its FRAME
    /// with its read — the new generation's ids have positions the moment the
    /// read lands, before any tick of the restarted settle.</summary>
    [Fact]
    public void ARefreshAdoptsItsFrameWithItsRead()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-refresh-frame");
            GraphDocumentViewModel document = host.Open();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            ulong generation = model.Generation + 1;
            const ulong added = 900_001;
            GraphNode[] nodes = [.. model.NodeIds.Select(id => model.NodesById[id]), new GraphNode(added, "p:added.md", "added.md", "added", GraphNodeKind.Note, 0, 0, 0, 0, 0, true, 0, null)];
            ulong[] ids = [.. nodes.Select(node => node.Id)];
            var positions = new float[ids.Length * 2];
            for (int i = 0; i < ids.Length; i++)
            {
                positions[2 * i] = 10f * i;
                positions[(2 * i) + 1] = -10f * i;
            }
            var frame = new LayoutFrame(positions, 1, false, generation);
            document.ApplyRefreshForTests(frame, new GraphDiagramTopologyRead(ids, model.Edges, nodes, generation));
            // Synchronously, before the dispatcher runs the settle's first
            // apply: the read landed and the frame with it.
            Assert.Equal(generation, model.Generation);
            Assert.Contains(added, model.NodeIds);
            Assert.True(model.Positions.TryGetValue(added, out GraphPoint? point));
            Assert.Equal(10.0 * (ids.Length - 1), point!.X);
            Assert.Same(frame, model.LastFrame);
            Assert.Equal(ids.Length, model.Positions.Count);
            host.Settle(document);
        });
    }

    /// <summary>Term G5 / G6 (IPH-4-1): a refresh answer whose frame is not
    /// two floats per id is MALFORMED — logged, nothing adopted: the
    /// generation, the ids, the positions and the last frame stand, no epoch
    /// opens and the settle does not restart.</summary>
    [Fact]
    public void AMalformedRefreshAnswerIsLoggedAndLeavesTheModelStanding()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-refresh-malformed");
            GraphDocumentViewModel document = host.Open();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            ulong generation = model.Generation;
            ulong[] ids = model.NodeIds;
            LayoutFrame? lastFrame = model.LastFrame;
            int positions = model.Positions.Count;
            int topologies = document.CrossingsForTests["graph_topology"];
            int steps = model.Driver.StepsForTests;
            GraphNode[] nodes = [.. ids.Select(id => model.NodesById[id]), new GraphNode(900_002, "p:added2.md", "added2.md", "added2", GraphNodeKind.Note, 0, 0, 0, 0, 0, true, 0, null)];
            ulong[] newIds = [.. nodes.Select(node => node.Id)];
            // One float short of two per id.
            var frame = new LayoutFrame(new float[(newIds.Length * 2) - 1], 1, false, generation + 1);
            TextWriter original = Console.Error;
            var captured = new StringWriter();
            Console.SetError(captured);
            try
            {
                document.ApplyRefreshForTests(frame, new GraphDiagramTopologyRead(newIds, model.Edges, nodes, generation + 1));
            }
            finally
            {
                Console.SetError(original);
            }
            Assert.Contains("SlateWindows.GraphLayoutRefreshFailed", captured.ToString());
            Assert.Equal(generation, model.Generation);
            Assert.Equal(ids, model.NodeIds);
            Assert.Equal(positions, model.Positions.Count);
            Assert.Same(lastFrame, model.LastFrame);
            Assert.False(model.Driver.IsSettling);
            host.Settle(document);
            Assert.Equal(topologies, document.CrossingsForTests["graph_topology"]);
            Assert.Equal(steps, model.Driver.StepsForTests);
        });
    }

    [Fact]
    public void ABackendFilterChangeUnderALiveModelRebuildsIt()
    {
        RunSta(() =>
        {
            using var host = new Host(4, "diagram-rebuild-preset");
            GraphDocumentViewModel document = host.Open();
            long baseline = LayoutSessionsAtRest();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel first = SettledModel(host, document);
            host.GraphLines.Clear();
            // The preset from Diagram mode: one teardown, one build under the
            // preset's filter, the headline alone — no GraphMode line.
            host.Workspace.GraphNavigator.RunPreset(GraphPreset.Orphans);
            Assert.True(first.IsRetired);
            Assert.True(first.IsHandleFreedForTests);
            GraphDiagramModel second = SettledModel(host, document);
            Assert.NotSame(first, second);
            Assert.True(second.Filter.OrphansOnly);
            Assert.Equal(2, document.CrossingsForTests["start_graph_layout"]);
            Assert.Equal(baseline + 1, LayoutSessionsLive());
            Assert.NotEmpty(host.GraphLines);
            Assert.DoesNotContain(host.GraphLines, line => line == ModeLine(GraphSurfaceMode.Diagram) || line == ModeLine(GraphSurfaceMode.Table));
            Assert.Equal(GraphSurfaceMode.Diagram, document.ViewState.Mode);
        });
    }

    // --- D-6: teardown, disposal, the drain (Term G7) ------------------------------

    [Fact]
    public void TheSwitchToTableRetiresTheGateAndFreesTheHandle()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-teardown-table");
            GraphDocumentViewModel document = host.Open();
            long baseline = LayoutSessionsAtRest();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            Assert.Equal(baseline + 1, LayoutSessionsLive());
            // Nothing in flight: freed at once, synchronously with the switch.
            Assert.True(document.SetMode(GraphSurfaceMode.Table));
            Assert.True(model.IsRetired);
            Assert.True(model.IsHandleFreedForTests);
            Assert.Equal(baseline, LayoutSessionsLive());
            Assert.Null(document.DiagramModel);
            Assert.False(document.HasLiveDiagram);
            Assert.False(model.Driver.IsSettling);
        });
    }

    [Fact]
    public void RetirementTearsDownTheDiagram()
    {
        RunSta(() =>
        {
            using var host = new Host(3, "diagram-teardown-retire");
            GraphDocumentViewModel document = host.Open();
            long baseline = LayoutSessionsAtRest();
            Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
            GraphDiagramModel model = SettledModel(host, document);
            // The tab closed: the retirement tears the diagram down; the handle
            // is freed by the gate at once — the retired scheduler's skipped
            // apply plays no part.
            host.Workspace.CloseActiveTabCommand.Execute(null);
            Assert.True(document.IsRetired);
            Assert.True(model.IsRetired);
            Assert.True(model.IsHandleFreedForTests);
            Assert.Null(document.DiagramModel);
            Assert.False(document.HasLiveDiagram);
            Assert.Equal(baseline, LayoutSessionsLive());
            host.Settle(document);
            Assert.Equal(baseline, LayoutSessionsLive());
        });
    }

    [Fact]
    public void TheWorkspaceDrainCoversTheLastAdmittedCallsReturn()
    {
        RunSta(() =>
        {
            var host = new Host(3, "diagram-teardown-drain");
            GraphDiagramModel model;
            long baseline;
            Task release;
            using (var park = new Park(from: 1, until: 1))
            {
                GraphDocumentViewModel document = host.Open();
                baseline = LayoutSessionsAtRest();
                Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
                model = SettledModel(host, document);
                model.InsideGateForTests = park.Hit;
                model.Driver.StartSettle();
                park.WaitReached();
                Assert.Equal(1, model.InFlightForTests);
                release = Task.Run(() =>
                {
                    Thread.Sleep(300);
                    park.Release();
                });
                // The workspace's bounded pre-session drain waits for the
                // admitted call's return; the gate frees the handle on it.
                host.Dispose();
                release.Wait(TimeSpan.FromSeconds(10));
            }
            Assert.True(model.IsRetired);
            Assert.Equal(0, model.InFlightForTests);
            Assert.True(model.IsHandleFreedForTests);
            Assert.Equal(baseline, LayoutSessionsLive());
        });
    }
}
