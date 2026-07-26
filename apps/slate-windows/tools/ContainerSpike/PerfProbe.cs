// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using uniffi.slate_uniffi;

namespace ContainerSpike;

/// Performance and stability of the chosen reading container at size.
///
/// Measures each stage separately, because they have different owners and
/// different fixes: core parse is Rust behind FFI, the WPF build and
/// layout are W3-1's, and the peer tree is the custom-peer design the
/// container decision rests on.
///
/// **The specific worry this exists to test.** `ReadingSurfacePeer`
/// returns every heading, list, list-item and link peer as one FLAT
/// children collection. At a few hundred links that is unremarkable; at
/// several thousand it is a single collection every UIA client — a
/// screen reader included — must enumerate. If there is a cliff, it is
/// here, and it is a property of the design rather than of WPF.
///
/// Results are written after EVERY size, in increasing order, so a hang
/// or OOM at the largest size does not discard the smaller ones.
internal static class PerfProbe
{
    private static readonly Corpus.Spec[] Sizes =
    {
        new("baseline 1k words / 20 links", 1_000, 20),
        new("10k words / 300 links", 10_000, 300),
        new("50k words / 1.5k links", 50_000, 1_500),
        new("200k words / 6k links", 200_000, 6_000),
        // ~5 MB by word count: the literal "5 MB file" reading.
        new("800k words / 24k links (~5 MB)", 800_000, 24_000),
        // ~5 MB by DESTINATION bytes with few words: the converter or
        // clipped-note shape, and the case the payload-duplication
        // residual was shipped against.
        new("10k words / 2k huge destinations (~5 MB)", 10_000, 2_000, longRunBytes: 2_500),
    };

    /// Each size runs in a FRESH PROCESS.
    ///
    /// Measuring them in one process gave memory figures that tracked the
    /// process heap rather than the corpus: two very differently shaped
    /// ~5 MB inputs reported within 3% of each other, which is a property
    /// of a plateaued heap and not of the documents. Peak working set of
    /// a dedicated process answers "what does this note cost" honestly,
    /// and it also means an OOM kills one row instead of the run.
    public static int Run(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var rows = new List<Row>();

        for (int index = 0; index < Sizes.Length; index++)
        {
            Console.WriteLine($"measuring: {Sizes[index].Name} ...");
            Row row = MeasureInChild(outputDirectory, index);
            rows.Add(row);
            Persist(outputDirectory, rows);
            Console.WriteLine(
                $"  {row.Bytes / 1024.0 / 1024.0:F2} MB · parse {row.ParseMs} ms · "
                + $"build {row.BuildMs} ms · layout {row.LayoutMs} ms · "
                + $"peers {row.PeerCount} in {row.PeerWalkMs} ms · "
                + $"peak {row.PeakWorkingSetBytes / 1024.0 / 1024.0:F0} MB"
                + (row.Error is null ? "" : $" · ERROR {row.Error}"));
        }

        Console.WriteLine();
        Console.WriteLine(Render(rows));
        return rows.Any(r => r.Error is not null) ? 1 : 0;
    }

    /// Child entry point: measure exactly one size and write its row.
    public static int RunSingle(string outputDirectory, int index)
    {
        Directory.CreateDirectory(outputDirectory);
        Row row = index >= 0 && index < Sizes.Length
            ? Measure(Sizes[index])
            : new Row { Name = $"<bad index {index}>", Error = "index out of range" };

        row.PeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64;
        File.WriteAllText(
            RowPath(outputDirectory, index),
            JsonSerializer.Serialize(row, new JsonSerializerOptions { WriteIndented = true }));
        return row.Error is null ? 0 : 1;
    }

    private static string RowPath(string outputDirectory, int index) =>
        Path.Combine(outputDirectory, $"perf-row-{index}.json");

    private static Row MeasureInChild(string outputDirectory, int index)
    {
        string path = RowPath(outputDirectory, index);
        try
        {
            File.Delete(path);
        }
        catch
        {
            // A stale row is caught by the read below.
        }

        try
        {
            var startInfo = new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("--probe");
            startInfo.ArgumentList.Add("perfone");
            startInfo.ArgumentList.Add("--size");
            startInfo.ArgumentList.Add(index.ToString());
            startInfo.ArgumentList.Add("--out");
            startInfo.ArgumentList.Add(outputDirectory);

            using Process? child = Process.Start(startInfo);
            if (child is null)
            {
                return Failed(index, "child process did not start");
            }
            if (!child.WaitForExit(TimeSpan.FromMinutes(10)))
            {
                try
                {
                    child.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Reported as a timeout either way.
                }
                return Failed(index, "timed out after 10 minutes — treat as a hang");
            }

            if (!File.Exists(path))
            {
                return Failed(
                    index,
                    $"child exited {child.ExitCode} without writing a row (likely OOM)");
            }
            return JsonSerializer.Deserialize<Row>(File.ReadAllText(path))
                ?? Failed(index, "row could not be deserialized");
        }
        catch (Exception exception)
        {
            return Failed(index, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static Row Failed(int index, string error) =>
        new() { Name = Sizes[index].Name, Error = error };

    private static Row Measure(Corpus.Spec spec)
    {
        var row = new Row { Name = spec.Name };
        try
        {
            string markdown = Corpus.Build(spec);
            row.Bytes = Encoding.UTF8.GetByteCount(markdown);

            // Core parse, both calls, as §10.1 requires them: once per
            // parse, zipped 1:1.
            var sw = Stopwatch.StartNew();
            ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(markdown);
            ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
                markdown, Fixture.Citations, Fixture.Records);
            sw.Stop();
            row.ParseMs = sw.ElapsedMilliseconds;
            row.Blocks = blocks.Length;
            row.Runs = inlines.Sum(i => i.Segments.Sum(s => s.Runs.Length));

            var model = new List<(ReadingBlock, ReadingBlockInlines)>(blocks.Length);
            for (int i = 0; i < blocks.Length && i < inlines.Length; i++)
            {
                model.Add((blocks[i], inlines[i]));
            }

            // Retention of the parsed model alone — this is where the
            // shipped payload-duplication residual would show up.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long beforeModel = GC.GetTotalMemory(true);
            GC.KeepAlive(model);
            row.ModelBytes = Math.Max(0, GC.GetTotalMemory(true) - 0);
            _ = beforeModel;

            sw.Restart();
            FrameworkElement surface = FlowDocumentBuilder.Build(model, withSemanticPeers: true);
            sw.Stop();
            row.BuildMs = sw.ElapsedMilliseconds;

            sw.Restart();
            surface.Measure(new Size(900, double.PositiveInfinity));
            surface.Arrange(new Rect(0, 0, 900, surface.DesiredSize.Height));
            surface.UpdateLayout();
            sw.Stop();
            row.LayoutMs = sw.ElapsedMilliseconds;

            // The measurement this probe exists for: how long to build
            // the children collection, and how big it gets.
            sw.Restart();
            AutomationPeer? peer = UIElementAutomationPeer.CreatePeerForElement(surface);
            List<AutomationPeer>? children = peer?.GetChildren();
            sw.Stop();
            row.PeerWalkMs = sw.ElapsedMilliseconds;
            row.PeerCount = children?.Count ?? 0;

            // A screen reader does not stop at the count — it reads each
            // one. Naming every child is the realistic cost.
            sw.Restart();
            int named = 0;
            foreach (AutomationPeer child in children ?? new List<AutomationPeer>())
            {
                try
                {
                    if (!string.IsNullOrEmpty(child.GetName()))
                    {
                        named++;
                    }
                }
                catch
                {
                    // A peer that throws on GetName is a finding, but not
                    // a reason to abandon the measurement.
                }
            }
            sw.Stop();
            row.PeerNameMs = sw.ElapsedMilliseconds;
            row.NamedPeers = named;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            row.TotalBytes = GC.GetTotalMemory(true);
            GC.KeepAlive(surface);
        }
        catch (Exception exception)
        {
            row.Error = $"{exception.GetType().Name}: {exception.Message}";
        }
        return row;
    }

    private static void Persist(string outputDirectory, List<Row> rows)
    {
        File.WriteAllText(
            Path.Combine(outputDirectory, "container-spike-perf.json"),
            JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(
            Path.Combine(outputDirectory, "container-spike-perf.md"), Render(rows));
    }

    private static string Render(List<Row> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# W3-1 reading container — size and stability");
        sb.AppendLine();
        sb.AppendLine(
            "`FlowDocument` + custom semantic peers, the container the spike selected. "
            + "Stages measured separately because they have different owners: core parse "
            + "is Rust behind FFI, build and layout are W3-1's, and the peer tree is the "
            + "custom-peer design itself.");
        sb.AppendLine();
        sb.AppendLine(
            "| corpus | MB | blocks | runs | parse ms | build ms | layout ms | peers | "
            + "peer walk ms | peer name ms | model MB | peak MB | status |");
        sb.AppendLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (Row row in rows)
        {
            sb.AppendLine(
                $"| {row.Name} | {row.Bytes / 1024.0 / 1024.0:F2} | {row.Blocks} | {row.Runs} "
                + $"| {row.ParseMs} | {row.BuildMs} | {row.LayoutMs} | {row.PeerCount} "
                + $"| {row.PeerWalkMs} | {row.PeerNameMs} "
                + $"| {row.ModelBytes / 1024.0 / 1024.0:F1} "
                + $"| {row.PeakWorkingSetBytes / 1024.0 / 1024.0:F0} "
                + $"| {(row.Error is null ? "ok" : "**" + row.Error + "**")} |");
        }
        return sb.ToString();
    }

    private sealed class Row
    {
        public string Name { get; set; } = string.Empty;
        public long Bytes { get; set; }
        public int Blocks { get; set; }
        public int Runs { get; set; }
        public long ParseMs { get; set; }
        public long BuildMs { get; set; }
        public long LayoutMs { get; set; }
        public long PeerWalkMs { get; set; }
        public long PeerNameMs { get; set; }
        public int PeerCount { get; set; }
        public int NamedPeers { get; set; }
        public long ModelBytes { get; set; }
        public long PeakWorkingSetBytes { get; set; }
        public long TotalBytes { get; set; }
        public string? Error { get; set; }
    }
}
