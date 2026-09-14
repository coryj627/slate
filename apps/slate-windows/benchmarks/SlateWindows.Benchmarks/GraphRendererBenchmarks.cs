// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Reports;
using SlateWindows;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

/// <summary>
/// W6-2 PR D §K (contract D-18): the diagram's host-side budgets over a
/// synthetic 1,500-note vault — tier A's ceiling (the constants'
/// <c>tier_b_threshold</c>; <c>GraphOpenBenchmarks</c>' generator WITHOUT
/// its ghost arm, so the visible count sits exactly at the threshold and
/// every node has a peer) — with the surface hosted in a hidden window on
/// its own STA dispatcher thread, the shell's shape. Four workloads: a warm
/// <c>Tick(20)</c> through the model's admission gate (the driver's
/// compute; ≤ 100 ms), the first rebuild — the epoch's topology fetch, its
/// landing, the renderer's complete peer materialisation with every name
/// read (≤ 500 ms) — a pan hop — the transform, the three visuals, every
/// peer rectangle (≤ 100 ms) — and a spatial step — core's step over the
/// visible positions, the selection, the scroll, the row line (≤ 50 ms).
/// The medians are recorded in BENCHMARKS.md; the budgets are asserted by
/// the runner's exit code through the graph suite's inventory shape.
///
///   dotnet run --project apps/slate-windows/benchmarks/SlateWindows.Benchmarks ///     --configuration Release -- --graph-renderer --validate-budgets
/// </summary>
[MemoryDiagnoser]
[MedianColumn]
[SimpleJob(warmupCount: 3, iterationCount: 15)]
public class GraphRendererBenchmarks
{
    /// <summary>The pinned inventory: workload → budget in ms (D-18); a
    /// report missing from it or an entry without a report fails the run.</summary>
    internal static readonly IReadOnlyDictionary<string, double> Inventory =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["WarmTick"] = 100.0,
            ["FirstRebuild"] = 500.0,
            ["PanHop"] = 100.0,
            ["SpatialStep"] = 50.0,
        };

    /// <summary>Tier A's ceiling: the largest visible count at which the
    /// renderer still materialises every node's peer (Term T2).</summary>
    internal const int Notes = 1_500;

    private static readonly TimeSpan HostBudget = TimeSpan.FromSeconds(120);

    private string _root = string.Empty;
    private VaultSession? _session;
    private Thread? _ui;
    private Dispatcher? _dispatcher;
    private WorkspaceViewModel? _workspace;
    private GraphDocumentViewModel? _document;
    private Window? _window;
    private GraphDiagramView? _diagram;
    private GraphDiagramModel? _model;
    private GraphVisibilityQuery? _query;
    private GraphConfig? _config;
    private int _hops;
    private int _steps;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _root = Path.Combine(Path.GetTempPath(), $"slate-graph-renderer-bench-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        // GraphOpenBenchmarks' linked shape without the ghost per hundred:
        // every note links to its successor and back to the first, so the
        // population is exactly the threshold and the first note is the hub.
        for (int i = 0; i < Notes; i++)
        {
            File.WriteAllText(
                Path.Combine(_root, $"note{i}.md"),
                $"# Note {i}\n\nLinks to [[note{(i + 1) % Notes}]] and back to [[note0]].\n");
        }
        _session = VaultSession.OpenFilesystem(_root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);

        using var ready = new ManualResetEventSlim(false);
        Exception? failure = null;
        _ui = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                _dispatcher = Dispatcher.CurrentDispatcher;
                Host();
            }
            catch (Exception exception)
            {
                failure = exception;
                ready.Set();
                return;
            }
            ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "slate-graph-renderer-bench-ui",
        };
        _ui.SetApartmentState(ApartmentState.STA);
        _ui.Start();
        ready.Wait();
        if (failure is not null)
        {
            throw new InvalidOperationException("the renderer host did not come up.", failure);
        }
    }

    /// <summary>The shell's shape on the STA thread: the workspace, the
    /// graph document through its pair, the surface in a hidden window,
    /// Diagram mode, the build landed, the settle ended, the first epoch on
    /// the renderer — tier A at the ceiling, one node selected.</summary>
    private void Host()
    {
        _workspace = new WorkspaceViewModel(
            Session(),
            _root,
            () => [],
            _ => { },
            startInteractionBackgroundWork: false,
            announceRendered: _ => { });
        // Animation ON regardless of the box's preference: the settle runs in
        // the driver's steps, the shell's default.
        _workspace.GraphMotionPolicyForTests = new GraphMotionPolicy(() => false);
        _workspace.OpenGraph();
        _document = _workspace.GraphDocument
            ?? throw new InvalidOperationException("the graph document did not seat.");
        PumpUntilDrained(_document.WhenAllWorkDrained());
        WorkspaceTabViewModel tab = _workspace.Groups.SelectMany(g => g.Tabs).First(t => t.IsGraph);
        var surface = new GraphSurfaceView { Model = _document, DataContext = tab };
        _window = new Window
        {
            Content = surface,
            Width = 1600,
            Height = 1200,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        _window.Show();
        _window.UpdateLayout();
        Require(_document.SetMode(GraphSurfaceMode.Diagram), "the switch to Diagram was refused.");
        Require(
            PumpUntil(() => _document.HasLiveDiagram || _document.DiagramError is not null),
            "the build never landed.");
        if (_document.DiagramError is { } error)
        {
            throw new InvalidOperationException($"the build failed: {error}");
        }
        _model = _document.DiagramModel
            ?? throw new InvalidOperationException("the live diagram has no model.");
        Require(PumpUntil(() => !_model.Driver.IsSettling), "the settle never ended.");
        PumpUntilDrained(_document.WhenAllWorkDrained());
        _window.UpdateLayout();
        _diagram = surface.DiagramForTests;
        GraphDiagramView diagram = _diagram;
        GraphDiagramModel model = _model;
        Require(
            PumpUntil(() => diagram.Entries.Count == model.NodeIds.Length),
            "the first epoch never landed on the renderer.");
        _window.UpdateLayout();
        if (diagram.VisibleCount != Notes || diagram.IsTierB)
        {
            throw new InvalidOperationException(
                $"the diagram shows {diagram.VisibleCount} nodes (tier B: {diagram.IsTierB}); tier A's ceiling is {Notes}.");
        }
        GraphViewState view = _document.ViewState;
        _query = new GraphVisibilityQuery(view.Filter, view.NameQuery, view.KindOnly);
        GraphGroup[] groups = [.. view.Groups];
        _config = SlateUniffiMethods.GraphConfigDefault() with { Groups = groups };
        Require(
            diagram.SelectNode(diagram.VisibleIds[Notes / 2], announce: false),
            "the initial selection was refused.");
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (_dispatcher is { } dispatcher)
        {
            dispatcher.Invoke(() =>
            {
                _window?.Close();
                _window = null;
                _workspace?.Dispose();
                _workspace = null;
            });
            dispatcher.InvokeShutdown();
            _ = _ui?.Join(TimeSpan.FromSeconds(30));
            _dispatcher = null;
        }
        _session?.Dispose();
        _session = null;
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>A warm <c>Tick(20)</c> through the model's admission gate —
    /// the driver's compute, off the dispatcher as the driver runs it
    /// (budget 100 ms).</summary>
    [Benchmark(Baseline = true)]
    public int WarmTick()
    {
        LayoutFrame? frame = Model().WithSession<LayoutFrame?>(
            session => session.Tick(GraphLayoutDriver.IterationsPerStep), null);
        return frame?.Positions.Length
            ?? throw new InvalidOperationException("the gate refused the tick.");
    }

    /// <summary>The first rebuild (budget 500 ms): the epoch's topology fetch
    /// through the binding, its landing on the model, the renderer's rebuild
    /// — the visible set, every node's peer, the hit grid — and the UIA
    /// children materialised with every name read. The standing epoch is
    /// cleared first, so each invocation is a FIRST rebuild over a cleared
    /// renderer, never a reuse of its peers.</summary>
    [Benchmark]
    public int FirstRebuild() => OnTheUiThread(() =>
    {
        GraphDocumentViewModel document = Document();
        GraphDiagramModel model = Model();
        GraphDiagramView diagram = Diagram();
        model.Topology = null;
        document.RaiseDiagramTopologyChangedForTests();
        GraphTopology topology = Session().GraphTopology(
            _query ?? throw new InvalidOperationException("no query"),
            _config ?? throw new InvalidOperationException("no config"));
        if (topology.Generation != model.Generation)
        {
            throw new InvalidOperationException("the topology's generation is not the model's.");
        }
        model.Topology = topology;
        document.RaiseDiagramTopologyChangedForTests();
        var peer = (GraphDiagramAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(diagram);
        int named = 0;
        foreach (AutomationPeer child in peer.GetChildren())
        {
            if (child.GetName().Length > 0)
            {
                named++;
            }
        }
        if (named != Notes)
        {
            throw new InvalidOperationException($"{named} named peers over {Notes} notes.");
        }
        return named;
    });

    /// <summary>A pan hop (budget 100 ms): the viewport's transform
    /// committed, the three visuals redrawn, and every peer's screen
    /// rectangle read — a bounding walk after a pan. Hops alternate
    /// direction so the viewport stays bounded.</summary>
    [Benchmark]
    public int PanHop() => OnTheUiThread(() =>
    {
        GraphDiagramView diagram = Diagram();
        double sign = (_hops++ & 1) == 0 ? -1 : 1;
        diagram.PanBy(sign * 800, sign * 600);
        int placed = 0;
        foreach (ulong id in diagram.VisibleIds)
        {
            if (!diagram.NodeScreenRect(id).IsEmpty)
            {
                placed++;
            }
        }
        if (placed != Notes)
        {
            throw new InvalidOperationException($"{placed} placed peers over {Notes} notes.");
        }
        return placed;
    });

    /// <summary>A spatial step (budget 50 ms): core's <c>spatial_step</c>
    /// over the visible positions from the selected node, the selection
    /// through the document, the scroll into view and the announced row.
    /// Steps alternate direction so each is a real move; a step that finds
    /// no node fails the run rather than flattering it.</summary>
    [Benchmark]
    public bool SpatialStep() => OnTheUiThread(() =>
    {
        double dx = (_steps++ & 1) == 0 ? 1 : -1;
        if (!Diagram().SpatialMove(dx, 0))
        {
            throw new InvalidOperationException("the spatial step found no node.");
        }
        return true;
    });

    /// <summary>Walk the captured summary against the inventory (D-18, the
    /// graph suite's A-15 shape).</summary>
    internal static bool ValidateInventory(Summary summary)
    {
        bool passed = true;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (BenchmarkReport report in summary.Reports)
        {
            string name = report.BenchmarkCase.Descriptor.WorkloadMethod.Name;
            if (!Inventory.TryGetValue(name, out double budget))
            {
                Console.Error.WriteLine($"§K graph renderer: unlisted report {name}.");
                passed = false;
                continue;
            }
            _ = seen.Add(name);
            double? median = report.ResultStatistics?.Median;
            if (median is null)
            {
                Console.Error.WriteLine($"§K graph renderer: no median for {name}.");
                passed = false;
                continue;
            }
            double ms = median.Value / 1_000_000;
            bool ok = ms <= budget;
            passed &= ok;
            Console.WriteLine($"§K graph renderer {name} p50 {ms:F3} ms / {budget:F0} ms: {(ok ? "PASS" : "MISS")}");
        }
        foreach (string entry in Inventory.Keys)
        {
            if (!seen.Contains(entry))
            {
                Console.Error.WriteLine($"§K graph renderer: inventory entry {entry} has no report.");
                passed = false;
            }
        }
        return passed;
    }

    private T OnTheUiThread<T>(Func<T> body) =>
        (_dispatcher ?? throw new InvalidOperationException("the renderer host is not up.")).Invoke(body);

    private VaultSession Session() => _session
        ?? throw new InvalidOperationException("Benchmark session was not initialized.");

    private GraphDocumentViewModel Document() => _document
        ?? throw new InvalidOperationException("the graph document is not seated.");

    private GraphDiagramModel Model() => _model
        ?? throw new InvalidOperationException("the diagram model is not live.");

    private GraphDiagramView Diagram() => _diagram
        ?? throw new InvalidOperationException("the renderer is not hosted.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>One background-priority frame: everything queued on the
    /// host's dispatcher before it runs (the tests' pump).</summary>
    private static void Drain()
    {
        var frame = new DispatcherFrame();
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private static bool PumpUntil(Func<bool> condition)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && clock.Elapsed < HostBudget)
        {
            Drain();
            Thread.Yield();
        }
        return condition();
    }

    /// <summary>Pump until the scheduler's drain completes — and observe it:
    /// a faulted drain is a failed host, not a number.</summary>
    private static void PumpUntilDrained(Task drain)
    {
        Require(PumpUntil(() => drain.IsCompleted), "the drain never completed.");
        drain.GetAwaiter().GetResult();
    }
}
