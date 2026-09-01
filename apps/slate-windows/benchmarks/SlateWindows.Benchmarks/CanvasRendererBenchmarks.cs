// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using BenchmarkDotNet.Attributes;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

/// <summary>
/// §D D16 / §K: the renderer's derivation budgets over the 2,000-node
/// fixture — the first windowed topology build, a pan's window hop,
/// and a selection step's derived read — validated against mac's
/// 500 / 100 / 50 ms budgets by the canvas suite's budget arm (the
/// W2-2 shape), with the measured medians recorded in BENCHMARKS.md.
/// The derivation is the PURE half the engine runs off-thread; the
/// dispatcher commit adds queueing, not work.
/// </summary>
[MemoryDiagnoser]
[BenchmarkDotNet.Attributes.MedianColumn]
[SimpleJob(warmupCount: 3, iterationCount: 15)]
public class CanvasRendererBenchmarks
{
    private const string Fixture = "large_2000.canvas";
    private string _root = string.Empty;
    private VaultSession? _session;
    private CanvasPopulation _population = null!;
    private CanvasViewportState _viewport = null!;
    private System.Collections.Immutable.ImmutableHashSet<CanvasPeerKey> _none = [];
    private string _midNode = string.Empty;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _root = Path.Combine(
            Path.GetTempPath(), $"slate-canvas-renderer-bench-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        File.Copy(
            Path.Combine(
                RepoRoot(), "crates", "slate-core", "tests",
                "fixtures", "canvas", Fixture),
            Path.Combine(_root, Fixture));
        _session = VaultSession.OpenFilesystem(_root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
        CanvasOpenInfo info = _session.OpenCanvas(Fixture);
        try
        {
            _population = new CanvasPopulation(
                _session.CanvasOutline(info.Handle),
                _session.CanvasTableRows(info.Handle),
                null,
                lastActivatedNode: null,
                scene: _session.CanvasScene(info.Handle));
        }
        finally
        {
            _session.CloseCanvas(info.Handle);
        }
        _viewport = CanvasViewportState.Seed().WithViewSize(1600, 1200);
        _midNode = _population.SceneNodes[_population.SceneNodes.Length / 2].NodeId;
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

    /// <summary>First windowed derivation (budget 500 ms).</summary>
    [Benchmark]
    public int FirstWindowedDerivation() =>
        CanvasPeerTopology.Derive(_population, _viewport, _none).Placements.Count;

    /// <summary>A pan's window hop (budget 100 ms).</summary>
    [Benchmark]
    public int PanWindowHop() =>
        CanvasPeerTopology.Derive(
            _population, _viewport.PannedTo(-800, -600), _none).Placements.Count;

    /// <summary>A selection step's derived read (budget 50 ms): the
    /// descriptor lookup a ring redraw costs.</summary>
    [Benchmark]
    public CanvasSceneNode? SelectionStepRead() =>
        _population.SceneByNode.TryGetValue(_midNode, out CanvasSceneNode? node)
            ? node
            : null;

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "Cargo.toml")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException("repo root not found");
    }
}
