// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Reports;
using SlateWindows;

namespace SlateWindows.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class EditorSemanticLineBenchmarks
{
    private SemanticBenchmarkHost? _host;
    [Params(100 * 1024, 1024 * 1024, 8 * 1024 * 1024)]
    public int Bytes { get; set; }
    [GlobalSetup] public void Setup() => _host = new SemanticBenchmarkHost(Bytes);
    [Benchmark] public object LineStyleId() => _host!.LineStyleId();
    [Benchmark] public object? PostEditLocalLink() => _host!.PostEditLocalLink();
    [GlobalCleanup] public void Cleanup() => _host?.Dispose();
}

[MemoryDiagnoser]
[MedianColumn]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class EditorSemanticSearchBenchmarks
{
    private SemanticBenchmarkHost? _host;
    [Params(8 * 1024 * 1024)]
    public int Bytes { get; set; }
    [GlobalSetup] public void Setup() => _host = new SemanticBenchmarkHost(Bytes);
    [Benchmark] public object? DocumentHyperlinks() => _host!.DocumentHyperlinks();
    [GlobalCleanup] public void Cleanup() => _host?.Dispose();
}

[MemoryDiagnoser]
[MedianColumn]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class EditorSemanticDenseBenchmarks
{
    private SemanticBenchmarkHost? _host;
    [Params(1000, 10000)] public int LinkCount { get; set; }
    [GlobalSetup] public void Setup() => _host = new SemanticBenchmarkHost(LinkCount, dense: true);
    [Benchmark] public object DenseInventoryAndWalk() => _host!.DenseInventoryAndWalk();
    [GlobalCleanup] public void Cleanup() => _host?.Dispose();
}

internal sealed class SemanticBenchmarkHost : IDisposable
{
    private readonly Thread _thread;
    private Dispatcher? _dispatcher;
    private AvalonDocumentBufferSession? _session;
    private ITextRangeProvider? _line;
    private EditorSemanticTextProvider? _provider;
    private ITextRangeProvider? _link;

    internal SemanticBenchmarkHost(int bytes, bool dense = false)
    {
        using var ready = new ManualResetEventSlim(false);
        Exception? failure = null;
        _thread = new Thread(() =>
        {
            try
            {
                _dispatcher = Dispatcher.CurrentDispatcher;
                const string block = "## Heading\n\nPlain prose for a bounded semantic read.\n\n";
                string text = dense ? string.Concat(Enumerable.Range(0, bytes).Select(index => $"## Heading\n\n[[Link{index}]]\n\n"))
                    : string.Concat(Enumerable.Repeat(block, bytes / block.Length + 1)) + "[[last link]]";
                _session = new AvalonDocumentBufferSession(text, _ => { }, TimeSpan.FromHours(1));
                var editor = new SlateTextEditor { Document = _session.Document, HighlightSession = _session };
                var peer = UIElementAutomationPeer.CreatePeerForElement(editor)
                    ?? throw new InvalidOperationException("Editor benchmark could not create the native automation peer.");
                var provider = peer.GetPattern(PatternInterface.Text) as EditorSemanticTextProvider
                    ?? throw new InvalidOperationException("Editor benchmark requires the semantic TextPattern provider.");
                int start = text.IndexOf("## Heading", text.Length / 2, StringComparison.Ordinal);
                _line = provider.Range(start, start + "## Heading".Length);
                _provider = provider;
                int linkStart = text.LastIndexOf("[[", StringComparison.Ordinal);
                _link = provider.Range(linkStart, linkStart + 1);
            }
            catch (Exception error) { failure = error; }
            finally { ready.Set(); }
            if (failure is null) { Dispatcher.Run(); }
        })
        { IsBackground = true, Name = "slate-editor-semantic-benchmark" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        if (!ready.Wait(TimeSpan.FromMinutes(2))) { throw new TimeoutException("Editor benchmark setup timed out."); }
        if (failure is not null) { throw new InvalidOperationException("Editor benchmark setup failed.", failure); }
    }

    internal object LineStyleId() => _dispatcher!.Invoke(() => _line!.GetAttributeValue(EditorSemanticTextRange.StyleIdAttribute));
    private void Edit()
    {
        int end = _session!.Document.TextLength;
        _session.Document.Insert(end, " ");
        _session.Document.Remove(end, 1);
    }
    internal object? PostEditLocalLink() => _dispatcher!.Invoke(() =>
    {
        Edit();
        var range = (EditorSemanticTextRange)_link!;
        return _provider!.Links.Enclosing(range.Bounds.Start, range.Bounds.End)?.GetName();
    });
    internal object DocumentHyperlinks() => _dispatcher!.Invoke(() =>
    {
        Edit();
        return _provider!.Links.RootChildren();
    });
    internal object DenseInventoryAndWalk() => _dispatcher!.Invoke(() =>
    {
        Edit();
        var peers = _provider!.Links.RootChildren();
        foreach (AutomationPeer peer in peers) { _ = peer.GetName(); _ = peer.IsEnabled(); }
        foreach (AutomationPeer peer in peers.AsEnumerable().Reverse()) { _ = peer.GetName(); }
        return peers.Count;
    });
    public void Dispose()
    {
        if (_dispatcher is { } dispatcher)
        {
            dispatcher.Invoke(() => _session?.Dispose());
            dispatcher.InvokeShutdown();
            _thread.Join(TimeSpan.FromSeconds(30));
        }
    }
}

internal static class EditorSemanticBudgets
{
    // First measurements and the frozen ceilings are recorded in contract E-11.
    internal static bool Validate(params Summary[] summaries)
    {
        var expected = new HashSet<(string Name, int Bytes)>
        {
            ("LineStyleId", 100 * 1024), ("LineStyleId", 1024 * 1024),
            ("LineStyleId", 8 * 1024 * 1024), ("DocumentHyperlinks", 8 * 1024 * 1024),
            ("PostEditLocalLink", 100 * 1024), ("PostEditLocalLink", 1024 * 1024),
            ("PostEditLocalLink", 8 * 1024 * 1024),
            ("DenseInventoryAndWalk", 1000), ("DenseInventoryAndWalk", 10000),
        };
        var lineMedians = new Dictionary<int, double>();
        bool passed = true;
        foreach (BenchmarkReport report in summaries.SelectMany(summary => summary.Reports))
        {
            string name = report.BenchmarkCase.Descriptor.WorkloadMethod.Name;
            int bytes = (int)report.BenchmarkCase.Parameters[name == "DenseInventoryAndWalk" ? "LinkCount" : "Bytes"];
            double? median = report.ResultStatistics?.Median / 1_000_000;
            double budget = name switch { "LineStyleId" => 0.5, "PostEditLocalLink" => 2, _ => 1000 };
            bool row = expected.Remove((name, bytes)) && median is not null && median <= budget;
            passed &= row;
            if (name == "LineStyleId" && median is { } value) { lineMedians[bytes] = value; }
            Console.WriteLine($"W7-1 {name} size={bytes} p50 {median:F4} ms / {budget:F1} ms: {(row ? "PASS" : "MISS")}");
        }
        passed &= expected.Count == 0;
        if (lineMedians.TryGetValue(1024 * 1024, out double one) && lineMedians.TryGetValue(8 * 1024 * 1024, out double eight))
        {
            double ratio = eight / one;
            passed &= ratio <= 4;
            Console.WriteLine($"W7-1 line read 8 MiB / 1 MiB flatness {ratio:F2}x / 4.00x");
        }
        else { passed = false; }
        return passed;
    }
}
