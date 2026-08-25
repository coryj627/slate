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
string[] benchmarkArgs = args
    .Where(argument =>
        !string.Equals(argument, "--validate-budgets", StringComparison.Ordinal)
        && !string.Equals(argument, "--canvas", StringComparison.Ordinal))
    .ToArray();
ManualConfig benchmarkConfig = ManualConfig.Create(DefaultConfig.Instance)
    .WithArtifactsPath(Path.Combine(AppContext.BaseDirectory, "BenchmarkDotNet.Artifacts"));
if (canvasSuite)
{
    _ = BenchmarkRunner.Run<CanvasOpenBenchmarks>(benchmarkConfig, benchmarkArgs);
    return 0;
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
