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
    [Benchmark] public object? DocumentFindLink() => _host!.DocumentFindLink();
    [GlobalCleanup] public void Cleanup() => _host?.Dispose();
}

internal sealed class SemanticBenchmarkHost : IDisposable
{
    private readonly Thread _thread;
    private Dispatcher? _dispatcher;
    private AvalonDocumentBufferSession? _session;
    private ITextRangeProvider? _line;
    private ITextRangeProvider? _document;

    internal SemanticBenchmarkHost(int bytes)
    {
        using var ready = new ManualResetEventSlim(false);
        Exception? failure = null;
        _thread = new Thread(() =>
        {
            try
            {
                _dispatcher = Dispatcher.CurrentDispatcher;
                const string block = "## Heading\n\nPlain prose for a bounded semantic read.\n\n";
                string text = string.Concat(Enumerable.Repeat(block, bytes / block.Length + 1)) + "[[last link]]";
                _session = new AvalonDocumentBufferSession(text, _ => { }, TimeSpan.FromHours(1));
                var editor = new SlateTextEditor { Document = _session.Document, HighlightSession = _session };
                var peer = UIElementAutomationPeer.CreatePeerForElement(editor);
                var provider = (EditorSemanticTextProvider)peer.GetPattern(PatternInterface.Text);
                int start = text.IndexOf("## Heading", text.Length / 2, StringComparison.Ordinal);
                _line = provider.Range(start, start + "## Heading".Length);
                _document = provider.DocumentRange;
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
    internal object? DocumentFindLink() => _dispatcher!.Invoke(() => _document!.FindAttribute(EditorSemanticTextRange.LinkAttribute, true, false));
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
            ("LineStyleId", 8 * 1024 * 1024), ("DocumentFindLink", 8 * 1024 * 1024),
        };
        var lineMedians = new Dictionary<int, double>();
        bool passed = true;
        foreach (BenchmarkReport report in summaries.SelectMany(summary => summary.Reports))
        {
            string name = report.BenchmarkCase.Descriptor.WorkloadMethod.Name;
            int bytes = (int)report.BenchmarkCase.Parameters["Bytes"];
            double? median = report.ResultStatistics?.Median / 1_000_000;
            double budget = name == "LineStyleId" ? 0.5 : 1000;
            bool row = expected.Remove((name, bytes)) && median is not null && median <= budget;
            passed &= row;
            if (name == "LineStyleId" && median is { } value) { lineMedians[bytes] = value; }
            Console.WriteLine($"W7-1 {name} {bytes} bytes p50 {median:F4} ms / {budget:F1} ms: {(row ? "PASS" : "MISS")}");
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
