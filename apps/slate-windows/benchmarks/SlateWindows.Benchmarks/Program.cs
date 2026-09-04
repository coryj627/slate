// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using SlateWindows;
using uniffi.slate_uniffi;

bool validateBudgets = args.Contains("--validate-budgets", StringComparer.Ordinal);
// W6-1 PR A (§K, contract A20): the canvas open/marshalling suite is a
// SEPARATE runner selection rather than a second class in the same run,
// because the W2-2 budget gate below reads every report's `Bytes`
// parameter and a canvas report has none.
bool canvasSuite = args.Contains("--canvas", StringComparer.Ordinal);
// W6-2 PR A (§K, contract A-15): the graph suite is its own runner
// selection for the same reason the canvas's is.
bool graphSuite = args.Contains("--graph", StringComparer.Ordinal);
string[] benchmarkArgs = args
    .Where(argument =>
        !string.Equals(argument, "--validate-budgets", StringComparison.Ordinal)
        && !string.Equals(argument, "--canvas", StringComparison.Ordinal)
        && !string.Equals(argument, "--graph", StringComparison.Ordinal))
    .ToArray();
ManualConfig benchmarkConfig = ManualConfig.Create(DefaultConfig.Instance)
    .WithArtifactsPath(Path.Combine(AppContext.BaseDirectory, "BenchmarkDotNet.Artifacts"));
if (graphSuite)
{
    // Contract A-15 (the round-3 ledger's IGA-50): the suite's Summary is
    // CAPTURED and walked against a pinned inventory — every (workload,
    // scale) has an entry, each entry a budget or measurement-only, a
    // report missing from the inventory or an entry without a report
    // fails the run.
    Summary graphSummary = BenchmarkRunner.Run<GraphOpenBenchmarks>(benchmarkConfig, benchmarkArgs);
    if (!validateBudgets)
    {
        return 0;
    }
    return GraphOpenBenchmarks.ValidateInventory(graphSummary) ? 0 : 1;
}
if (canvasSuite)
{
    _ = BenchmarkRunner.Run<CanvasOpenBenchmarks>(benchmarkConfig, benchmarkArgs);
    Summary rendererSummary = BenchmarkRunner.Run<CanvasRendererBenchmarks>(
        benchmarkConfig, benchmarkArgs);
    if (!validateBudgets)
    {
        return 0;
    }
    // §D D16: mac's three budgets, asserted (the W2-2 arm's shape).
    var rendererBudgets = new Dictionary<string, double>
    {
        ["FirstWindowedDerivation"] = 500.0,
        ["PanWindowHop"] = 100.0,
        ["SelectionStepRead"] = 50.0,
    };
    bool rendererPassed = true;
    foreach (BenchmarkReport rendererReport in rendererSummary.Reports)
    {
        string name = rendererReport.BenchmarkCase.Descriptor.WorkloadMethod.Name;
        double? median = rendererReport.ResultStatistics?.Median;
        if (median is null || !rendererBudgets.TryGetValue(name, out double budget))
        {
            rendererPassed = false;
            continue;
        }
        double ms = median.Value / 1_000_000;
        bool ok = ms <= budget;
        rendererPassed &= ok;
        Console.WriteLine(
            $"§D D16 {name} p50 {ms:F3} ms / {budget:F0} ms: {(ok ? "PASS" : "MISS")}");
    }
    return rendererPassed ? 0 : 1;
}

Summary summary = BenchmarkRunner.Run<EditorHighlightBenchmarks>(
    benchmarkConfig,
    benchmarkArgs);

if (!validateBudgets)
{
    return 0;
}

var budgets = new Dictionary<int, double>
{
    [100 * 1024] = 0.5,
    [1024 * 1024] = 0.5,
    [8 * 1024 * 1024] = 1.0,
};
var medians = new Dictionary<int, double>();
bool passed = true;
foreach (BenchmarkReport report in summary.Reports)
{
    int bytes = (int)report.BenchmarkCase.Parameters["Bytes"];
    double? medianNanoseconds = report.ResultStatistics?.Median;
    if (medianNanoseconds is null)
    {
        Console.Error.WriteLine($"W2-2 budget gate: no median for {bytes} bytes.");
        passed = false;
        continue;
    }

    double medianMilliseconds = medianNanoseconds.Value / 1_000_000;
    medians[bytes] = medianMilliseconds;
    bool rowPassed = medianMilliseconds <= budgets[bytes];
    passed &= rowPassed;
    Console.WriteLine(
        $"W2-2 {bytes / 1024} KiB p50 {medianMilliseconds:F4} ms / "
        + $"{budgets[bytes]:F1} ms: {(rowPassed ? "PASS" : "MISS")}");
}

if (medians.TryGetValue(1024 * 1024, out double oneMiB)
    && medians.TryGetValue(8 * 1024 * 1024, out double eightMiB))
{
    double flatness = eightMiB / oneMiB;
    bool flatnessPassed = flatness <= 4.0;
    passed &= flatnessPassed;
    Console.WriteLine(
        $"W2-2 8 MiB / 1 MiB flatness {flatness:F2}x / 4.00x: "
        + $"{(flatnessPassed ? "PASS" : "MISS")}");
}
else
{
    passed = false;
}

return passed ? 0 : 1;

[MemoryDiagnoser]
[MedianColumn]
[SimpleJob(warmupCount: 4, iterationCount: 15)]
[InvocationCount(320)]
public class EditorHighlightBenchmarks
{
    private const int ViewportRadiusUtf16 = 2048;
    private AvalonDocumentBufferSession? _session;
    private string _fixture = string.Empty;
    private int _editOffset;
    private int _currentOffset;

    [Params(100 * 1024, 1024 * 1024, 8 * 1024 * 1024)]
    public int Bytes { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fixture = SyntheticNote(Bytes);
        _editOffset = NearestProseOffset(_fixture);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _session = new AvalonDocumentBufferSession(
            _fixture,
            _ => { },
            TimeSpan.FromHours(1));
        _currentOffset = _editOffset;
    }

    [Benchmark]
    public int DeltaAndWindowedHighlight()
    {
        AvalonDocumentBufferSession session = _session
            ?? throw new InvalidOperationException("Benchmark session was not initialized.");
        session.Document.Insert(_currentOffset, "x");
        _currentOffset++;
        return session.HighlightInRange(
            Math.Max(0, _currentOffset - ViewportRadiusUtf16),
            Math.Min(session.Document.TextLength, _currentOffset + ViewportRadiusUtf16))
            .Spans.Count;
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _session?.Dispose();
        _session = null;
    }

    private static string SyntheticNote(int targetBytes)
    {
        const string block =
            "## Section\n\nProse with a [[Wikilink]] and #tag around a mid-sentence edit anchor.\n\n"
            + "- [ ] a task\n- [x] a completed task\n\n"
            + "```rust\nlet value = \"fenced content\";\n```\n\n";
        var note = new System.Text.StringBuilder(targetBytes + block.Length);
        note.Append("---\ntitle: Big Note\ntags: [bench, editor]\n---\n\n");
        while (note.Length < targetBytes)
        {
            note.Append(block);
        }

        return note.ToString();
    }

    private static int NearestProseOffset(string document)
    {
        const string anchor = "mid-sentence";
        int target = document.Length / 2;
        int afterTarget = document.IndexOf(anchor, target, StringComparison.Ordinal);
        return afterTarget >= 0
            ? afterTarget
            : document.LastIndexOf(anchor, target, StringComparison.Ordinal);
    }
}

/// <summary>
/// W6-2 PR A §K (contract A-15, AD-7): the graph's snapshot marshalling
/// through the C# binding and the document's open through to its first
/// publication, over synthetic linked vaults at 1k and 10k notes. P set no
/// host-side budget; the two open-to-publication budgets are Windows host
/// budgets on the canvas's first-derivation precedent (500 ms at 10k,
/// 100 ms at 1k); the snapshot and rows workloads are measurement-only.
///
///   dotnet run --project apps/slate-windows/benchmarks/SlateWindows.Benchmarks ///     --configuration Release -- --graph --validate-budgets
/// </summary>
[MemoryDiagnoser]
[MedianColumn]
[SimpleJob(warmupCount: 3, iterationCount: 15)]
public class GraphOpenBenchmarks
{
    /// <summary>The pinned inventory: (workload, notes) → budget in ms,
    /// or null for measurement-only.</summary>
    internal static readonly IReadOnlyDictionary<(string Workload, int Notes), double?> Inventory =
        new Dictionary<(string, int), double?>
        {
            [("SnapshotDefaultFilter", 1_000)] = null,
            [("SnapshotDefaultFilter", 10_000)] = null,
            [("TableRowsDefaultSort", 1_000)] = null,
            [("TableRowsDefaultSort", 10_000)] = null,
            [("OpenToPublication", 1_000)] = 100.0,
            [("OpenToPublication", 10_000)] = 500.0,
        };

    private string _root = string.Empty;
    private VaultSession? _session;

    [Params(1_000, 10_000)]
    public int Notes { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _root = Path.Combine(Path.GetTempPath(), $"slate-graph-bench-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        // Core's `generate_linked_vault` shape: every note links to its
        // successor and back to the first, plus one unresolved target
        // per hundred notes so the ghost arm is exercised.
        for (int i = 0; i < Notes; i++)
        {
            string ghost = i % 100 == 0 ? $" and [[Missing {i / 100}]]" : string.Empty;
            File.WriteAllText(
                Path.Combine(_root, $"note{i}.md"),
                $"# Note {i}\n\nLinks to [[note{(i + 1) % Notes}]] and back to [[note0]]{ghost}.\n");
        }
        _session = VaultSession.OpenFilesystem(_root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
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

    private VaultSession Session() => _session
        ?? throw new InvalidOperationException("Benchmark session was not initialized.");

    /// <summary>The snapshot under core's default filter — the pair's
    /// first crossing.</summary>
    [Benchmark(Baseline = true)]
    public int SnapshotDefaultFilter() =>
        Session().GraphSnapshot(SlateWindows.Graph.GraphViewState.DefaultFilter()).Nodes.Length;

    /// <summary>The rows under the fetched default sort — the pair's second.</summary>
    [Benchmark]
    public int TableRowsDefaultSort() =>
        Session().GraphTableRows(
            new GraphVisibilityQuery(SlateWindows.Graph.GraphViewState.DefaultFilter(), string.Empty, null),
            SlateUniffiMethods.GraphTableDefaultSort()).Rows.Length;

    /// <summary>The document's open through to its first publication,
    /// under a pumped dispatcher — what the reader waits for.</summary>
    [Benchmark]
    public int OpenToPublication()
    {
        int rows = 0;
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(
            new System.Windows.Threading.DispatcherSynchronizationContext(
                System.Windows.Threading.Dispatcher.CurrentDispatcher));
        try
        {
            var announcer = new SlateWindows.Graph.GraphAnnouncer(_ => { });
            var document = new SlateWindows.Graph.GraphDocumentViewModel(
                Session(), announcer, () => false, () => GraphVerbosity.Standard);
            _ = document.Load(SlateWindows.Graph.GraphLoadKind.Pair, SlateWindows.Graph.GraphAnnouncePolicy.Silent);
            Task drain = document.WhenAllWorkDrained();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!drain.IsCompleted && clock.Elapsed < TimeSpan.FromSeconds(30))
            {
                var frame = new System.Windows.Threading.DispatcherFrame();
                _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    () => frame.Continue = false);
                System.Windows.Threading.Dispatcher.PushFrame(frame);
            }
            // A-15 (IPA-12): the number is a PUBLICATION's, never a failed or
            // unfinished load's — the drain observed, the budget enforced, a
            // READY snapshot held over the synthetic vault's whole
            // population (the notes plus one ghost per hundred).
            if (!drain.IsCompleted)
            {
                throw new TimeoutException("the graph's first pair did not publish within 30 s.");
            }
            drain.GetAwaiter().GetResult();
            if (document.Publication.State != SlateWindows.Graph.GraphLoadState.Ready
                || !document.Publication.HoldsSnapshot)
            {
                throw new InvalidOperationException(
                    $"the graph's first pair ended in {document.Publication.State}, not a held READY snapshot.");
            }
            rows = document.Publication.Rows.Count;
            int expected = Notes + Notes / 100;
            if (rows != expected)
            {
                throw new InvalidOperationException(
                    $"the publication carries {rows} rows; the synthetic vault has {expected}.");
            }
            document.Retire();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
        return rows;
    }

    /// <summary>Walk the captured summary against the inventory (A-15).</summary>
    internal static bool ValidateInventory(Summary summary)
    {
        bool passed = true;
        var seen = new HashSet<(string, int)>();
        foreach (BenchmarkReport report in summary.Reports)
        {
            string name = report.BenchmarkCase.Descriptor.WorkloadMethod.Name;
            int notes = (int)report.BenchmarkCase.Parameters["Notes"];
            if (!Inventory.TryGetValue((name, notes), out double? budget))
            {
                Console.Error.WriteLine($"§K graph: unlisted report {name} at {notes} notes.");
                passed = false;
                continue;
            }
            _ = seen.Add((name, notes));
            double? median = report.ResultStatistics?.Median;
            if (median is null)
            {
                Console.Error.WriteLine($"§K graph: no median for {name} at {notes} notes.");
                passed = false;
                continue;
            }
            double ms = median.Value / 1_000_000;
            if (budget is double limit)
            {
                bool ok = ms <= limit;
                passed &= ok;
                Console.WriteLine($"§K graph {name} @ {notes} p50 {ms:F3} ms / {limit:F0} ms: {(ok ? "PASS" : "MISS")}");
            }
            else
            {
                Console.WriteLine($"§K graph {name} @ {notes} p50 {ms:F3} ms (measurement-only)");
            }
        }
        foreach ((string Workload, int Notes) entry in Inventory.Keys)
        {
            if (!seen.Contains(entry))
            {
                Console.Error.WriteLine($"§K graph: inventory entry {entry.Workload} at {entry.Notes} notes has no report.");
                passed = false;
            }
        }
        return passed;
    }
}

/// <summary>
/// W6-1 PR A §K (contract A20): the canvas read path through the C#
/// binding, over the committed 2,000-node fixture. The mac core path is
/// 5.62 ms (BENCHMARKS.md, Milestone T Wave 1) — everything measured
/// here on top of that is marshalling, which is the whole point of the
/// row: core's cost is already benchmarked in Rust, and this suite
/// isolates what the Windows host adds.
///
///   dotnet run --project apps/slate-windows/benchmarks/SlateWindows.Benchmarks ///     --configuration Release -- --canvas
/// </summary>
[MemoryDiagnoser]
[MedianColumn]
[SimpleJob(warmupCount: 3, iterationCount: 15)]
public class CanvasOpenBenchmarks
{
    private const string Fixture = "large_2000.canvas";
    private string _root = string.Empty;
    private VaultSession? _session;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _root = Path.Combine(
            Path.GetTempPath(), $"slate-canvas-bench-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        File.Copy(FixturePath(), Path.Combine(_root, Fixture));
        _session = VaultSession.OpenFilesystem(_root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
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

    /// <summary>Parse + derive + index + the handle, with nothing
    /// marshalled back but the open info.</summary>
    [Benchmark(Baseline = true)]
    public uint Open()
    {
        VaultSession session = Session();
        CanvasOpenInfo info = session.OpenCanvas(Fixture);
        try
        {
            return info.NodeCount;
        }
        finally
        {
            session.CloseCanvas(info.Handle);
        }
    }

    [Benchmark]
    public int OpenAndOutline() => Marshalled(static (session, handle) =>
        session.CanvasOutline(handle).Length);

    [Benchmark]
    public int OpenAndTable() => Marshalled(static (session, handle) =>
        session.CanvasTableRows(handle).Length);

    [Benchmark]
    public int OpenAndScene() => Marshalled(static (session, handle) =>
        session.CanvasScene(handle).Nodes.Length);

    /// <summary>What PR A's document VM actually pays on a load: all
    /// three projections behind one open (contract A17).</summary>
    [Benchmark]
    public int OpenAndEveryProjection() => Marshalled(static (session, handle) =>
        session.CanvasOutline(handle).Length
        + session.CanvasTableRows(handle).Length
        + session.CanvasScene(handle).Nodes.Length);

    private int Marshalled(Func<VaultSession, ulong, int> body)
    {
        VaultSession session = Session();
        CanvasOpenInfo info = session.OpenCanvas(Fixture);
        try
        {
            return body(session, info.Handle);
        }
        finally
        {
            session.CloseCanvas(info.Handle);
        }
    }

    private VaultSession Session() => _session
        ?? throw new InvalidOperationException("Benchmark session was not initialized.");

    /// <summary>Walk UP to the workspace `Cargo.toml` rather than
    /// counting directory hops — a hop count breaks on a TFM, RID or
    /// runner change (the A11yCorpusCensus rationale).</summary>
    private static string FixturePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "Cargo.toml")))
        {
            directory = directory.Parent;
        }
        string root = directory?.FullName
            ?? throw new InvalidOperationException("repository root not found");
        return Path.Combine(
            root, "crates", "slate-core", "tests", "fixtures", "canvas", Fixture);
    }
}
