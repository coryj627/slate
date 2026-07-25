// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Axe.Windows.Automation;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace ContainerSpikeProbe;

/// Measures both W3-1 container candidates through UI Automation — the
/// half of the §10.6 spike that does not need a human listening.
///
/// It answers, per container: does the surface expose a Text pattern over
/// the whole note; does `GetText(-1)` return the blocks in reading order
/// INCLUDING the non-text children; do native `List`/`ListItem` control
/// types appear; does `AutomationProperties.HeadingLevel` survive to UIA;
/// do hyperlinks carry `HelpText` and stay keyboard-focusable; are the
/// interactive children Invoke-able; is the tree axe-clean.
///
/// What it deliberately does NOT answer: what JAWS and NVDA actually SAY,
/// and whether their `H`/`K`/`L`/`I` quick-nav lands where a reader
/// expects. UIA exposure is necessary but not sufficient for that, and
/// §10.6 makes recorded AT evidence the pass criterion — so the report
/// ends with the manual script rather than pretending to have run it.
internal static class Program
{
    private static readonly string[] Variants = { "flow", "items" };

    public static int Main(string[] args)
    {
        string outputDir = ArgValue(args, "--out")
            ?? Path.Combine(AppContext.BaseDirectory, "spike-evidence");
        Directory.CreateDirectory(outputDir);

        // `UserInteractive` is a WARNING, not a gate. It is false whenever
        // the process is not on WinSta0 — which includes an SSH session,
        // where UIA can still work because the probe and the spike it
        // launches share that session's own window station. The failure
        // that actually matters is CROSS-session (a session-0 shell trying
        // to see a session-1 desktop), and that surfaces as a real error
        // below rather than as a guess made up front.
        if (!Environment.UserInteractive)
        {
            Console.Error.WriteLine(
                "warning: this process is not on an interactive window station "
                + $"(session {GetSessionId()}). "
                + "UIA works only between processes sharing a desktop — fine over SSH, "
                + "impossible from a session-0 shell against your logged-in desktop. "
                + "Attempting anyway; a timeout waiting for the window means it is the "
                + "latter.");
        }

        if (args.Contains("--nvda"))
        {
            // NVDA is the pass criterion §10.6 names; the UIA table this
            // file produces is the necessary-but-not-sufficient half.
            return NvdaProbe.RunAsync(outputDir, Variants).GetAwaiter().GetResult();
        }

        var results = new List<VariantReport>();
        foreach (string variant in Variants)
        {
            Console.WriteLine($"probing '{variant}' ...");
            try
            {
                results.Add(Probe(variant));
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"  {variant} FAILED: {exception.Message}");
                results.Add(new VariantReport { Variant = variant, Error = exception.ToString() });
            }
        }

        string json = JsonSerializer.Serialize(
            results, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(outputDir, "container-spike.json"), json);
        string markdown = Report.Render(results);
        File.WriteAllText(Path.Combine(outputDir, "container-spike.md"), markdown);

        Console.WriteLine();
        Console.WriteLine(markdown);
        Console.WriteLine($"evidence written to {outputDir}");
        return results.Any(r => r.Error is not null) ? 1 : 0;
    }

    private static int GetSessionId()
    {
        try
        {
            return Process.GetCurrentProcess().SessionId;
        }
        catch
        {
            return -1;
        }
    }

    private static string? ArgValue(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static VariantReport Probe(string variant)
    {
        var report = new VariantReport { Variant = variant };
        Process? process = null;
        try
        {
            process = SpikeLauncher.Start(variant);
            using var automation = new UIA3Automation();
            Window window = SpikeLauncher.WaitForWindow(
                process, automation, TimeSpan.FromSeconds(30));

            AutomationElement surface =
                window.FindFirstDescendant(cf => cf.ByAutomationId("ReadingSurface"))
                ?? throw new InvalidOperationException("ReadingSurface not found.");

            report.SurfaceControlType = surface.ControlType.ToString();
            MeasureTextPattern(surface, report);
            MeasureTree(window, report);
            report.AxeErrors = ScanAxe(process);
        }
        finally
        {
            SpikeLauncher.TryKill(process);
        }
        return report;
    }

    /// The decisive measurement. A genuine document Text pattern returns
    /// the WHOLE note in reading order; a container that merely holds text
    /// elements returns nothing, or returns only its own fragment.
    private static void MeasureTextPattern(AutomationElement surface, VariantReport report)
    {
        // The pattern may live on the surface itself or on a document
        // child, so look for it in both places before concluding it is
        // absent — concluding too early would bias the comparison.
        AutomationElement? textHost = surface.Patterns.Text.IsSupported
            ? surface
            : surface.FindAllDescendants().FirstOrDefault(e => e.Patterns.Text.IsSupported);

        report.TextPatternSupported = textHost is not null;
        if (textHost is null)
        {
            return;
        }

        report.TextPatternHostControlType = textHost.ControlType.ToString();
        try
        {
            string text = textHost.Patterns.Text.Pattern.DocumentRange.GetText(-1) ?? string.Empty;
            report.ReadingOrderText = text;
            report.ReadingOrderLength = text.Length;

            // Every landmark must appear, and in authored order — that is
            // what "text ranges expose the reading order" (§W3-1 item 4)
            // actually means for a reader using say-all.
            string[] landmarks =
            {
                "Reading container spike", "resolved note", "absent note",
                "Second level heading", "first bullet", "nested bullet",
                "ordered one", "an open task", "a done task", "block quote",
                "fn main", "header a", "cell 1", "Embedded note",
            };
            var found = new List<string>();
            var missing = new List<string>();
            int cursor = 0;
            var outOfOrder = new List<string>();
            foreach (string landmark in landmarks)
            {
                int index = text.IndexOf(landmark, StringComparison.Ordinal);
                if (index < 0)
                {
                    missing.Add(landmark);
                    continue;
                }
                found.Add(landmark);
                if (index < cursor)
                {
                    outOfOrder.Add(landmark);
                }
                cursor = index;
            }
            report.LandmarksFound = found.ToArray();
            report.LandmarksMissing = missing.ToArray();
            report.LandmarksOutOfOrder = outOfOrder.ToArray();
        }
        catch (Exception exception)
        {
            report.ReadingOrderError = exception.Message;
        }
    }

    private static void MeasureTree(Window window, VariantReport report)
    {
        AutomationElement[] all = window.FindAllDescendants();
        report.DescendantCount = all.Length;

        report.ControlTypeHistogram = all
            .GroupBy(e => Safe(() => e.ControlType.ToString(), "unknown"))
            .ToDictionary(g => g.Key, g => g.Count());

        report.ListControlTypes = all.Count(e =>
            Safe(() => e.ControlType, ControlType.Custom) == ControlType.List);
        report.ListItemControlTypes = all.Count(e =>
            Safe(() => e.ControlType, ControlType.Custom) == ControlType.ListItem);

        // HeadingLevel: set on every heading by both builders. Whether it
        // SURVIVES to UIA is the open question §10.6 item 2 records.
        report.HeadingLevels = all
            .Select(e => new
            {
                Name = Safe(() => e.Name ?? string.Empty, string.Empty),
                Level = Safe(
                    () => e.Properties.HeadingLevel.IsSupported
                        ? e.Properties.HeadingLevel.Value.ToString()
                        : "unsupported",
                    "error"),
            })
            .Where(x => x.Level is not ("unsupported" or "error" or "None"))
            .Select(x => $"{x.Level}: {Truncate(x.Name, 40)}")
            .ToArray();
        report.HeadingLevelSupportedAnywhere = report.HeadingLevels.Length > 0;

        report.Hyperlinks = all
            .Where(e => Safe(() => e.ControlType, ControlType.Custom) == ControlType.Hyperlink)
            .Select(e => new LinkReport
            {
                Name = Truncate(Safe(() => e.Name ?? string.Empty, string.Empty), 40),
                HelpText = Truncate(Safe(() => e.HelpText ?? string.Empty, string.Empty), 60),
                KeyboardFocusable = Safe(() => e.Properties.IsKeyboardFocusable.Value, false),
                InvokeSupported = Safe(() => e.Patterns.Invoke.IsSupported, false),
            })
            .ToArray();

        report.FocusableElements = all
            .Where(e => Safe(() => e.Properties.IsKeyboardFocusable.Value, false))
            .Select(e =>
                $"{Safe(() => e.ControlType.ToString(), "unknown")}: "
                + Truncate(Safe(() => e.Name ?? string.Empty, string.Empty), 40))
            .ToArray();

        // The interactive children that must remain reachable INSIDE the
        // container: the task checkboxes, the code-block copy button, and
        // the block-embed card.
        report.InteractiveChildren = all
            .Where(e =>
            {
                ControlType type = Safe(() => e.ControlType, ControlType.Custom);
                return type is ControlType.CheckBox or ControlType.Button;
            })
            .Select(e => new LinkReport
            {
                Name = Truncate(Safe(() => e.Name ?? string.Empty, string.Empty), 40),
                HelpText = Truncate(Safe(() => e.HelpText ?? string.Empty, string.Empty), 60),
                KeyboardFocusable = Safe(() => e.Properties.IsKeyboardFocusable.Value, false),
                InvokeSupported = Safe(() =>
                    e.Patterns.Invoke.IsSupported || e.Patterns.Toggle.IsSupported, false),
            })
            .ToArray();
    }

    private static string[] ScanAxe(Process process)
    {
        try
        {
            var config = Config.Builder.ForProcessId(process.Id).Build();
            var output = ScannerFactory.CreateScanner(config).Scan(null);
            return output.WindowScanOutputs
                .SelectMany(result => result.Errors)
                .Select(error => $"{error.Rule.ID}: {error.Rule.Description}")
                .Distinct()
                .ToArray();
        }
        catch (Exception exception)
        {
            return new[] { $"axe scan failed: {exception.Message}" };
        }
    }

    private static T Safe<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch
        {
            return fallback;
        }
    }

    private static string Truncate(string value, int max)
    {
        value = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return value.Length <= max ? value : value[..max] + "…";
    }
}

internal sealed class VariantReport
{
    public string Variant { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string? SurfaceControlType { get; set; }
    public bool TextPatternSupported { get; set; }
    public string? TextPatternHostControlType { get; set; }
    public string? ReadingOrderText { get; set; }
    public int ReadingOrderLength { get; set; }
    public string? ReadingOrderError { get; set; }
    public string[] LandmarksFound { get; set; } = Array.Empty<string>();
    public string[] LandmarksMissing { get; set; } = Array.Empty<string>();
    public string[] LandmarksOutOfOrder { get; set; } = Array.Empty<string>();
    public int DescendantCount { get; set; }
    public Dictionary<string, int> ControlTypeHistogram { get; set; } = new();
    public int ListControlTypes { get; set; }
    public int ListItemControlTypes { get; set; }
    public bool HeadingLevelSupportedAnywhere { get; set; }
    public string[] HeadingLevels { get; set; } = Array.Empty<string>();
    public LinkReport[] Hyperlinks { get; set; } = Array.Empty<LinkReport>();
    public LinkReport[] InteractiveChildren { get; set; } = Array.Empty<LinkReport>();
    public string[] FocusableElements { get; set; } = Array.Empty<string>();
    public string[] AxeErrors { get; set; } = Array.Empty<string>();
}

internal sealed class LinkReport
{
    public string Name { get; set; } = string.Empty;
    public string HelpText { get; set; } = string.Empty;
    public bool KeyboardFocusable { get; set; }
    public bool InvokeSupported { get; set; }
}

internal static class Report
{
    public static string Render(IReadOnlyList<VariantReport> reports)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# W3-1 container spike — UIA measurements");
        sb.AppendLine();
        sb.AppendLine(
            "Programmatic half of the §10.6 spike. The JAWS/NVDA behavioural half is "
            + "NOT covered here — see the manual script at the end.");
        sb.AppendLine();

        sb.AppendLine("| measurement | " + string.Join(" | ", reports.Select(r => r.Variant)) + " |");
        sb.AppendLine("|---|" + string.Join("", reports.Select(_ => "---|")));
        Row(sb, reports, "surface control type", r => r.SurfaceControlType ?? "—");
        Row(sb, reports, "Text pattern present", r => r.TextPatternSupported ? "**yes**" : "**no**");
        Row(sb, reports, "Text pattern host", r => r.TextPatternHostControlType ?? "—");
        Row(sb, reports, "reading-order chars", r => r.ReadingOrderLength.ToString());
        Row(sb, reports, "landmarks found", r => $"{r.LandmarksFound.Length}/14");
        Row(sb, reports, "landmarks missing", r =>
            r.LandmarksMissing.Length == 0 ? "none" : string.Join(", ", r.LandmarksMissing));
        Row(sb, reports, "landmarks out of order", r =>
            r.LandmarksOutOfOrder.Length == 0 ? "none" : string.Join(", ", r.LandmarksOutOfOrder));
        Row(sb, reports, "List control types", r => r.ListControlTypes.ToString());
        Row(sb, reports, "ListItem control types", r => r.ListItemControlTypes.ToString());
        Row(sb, reports, "HeadingLevel reaches UIA", r =>
            r.HeadingLevelSupportedAnywhere ? "**yes**" : "**no**");
        Row(sb, reports, "hyperlinks", r => r.Hyperlinks.Length.ToString());
        Row(sb, reports, "hyperlinks w/ HelpText", r =>
            r.Hyperlinks.Count(l => l.HelpText.Length > 0).ToString());
        Row(sb, reports, "hyperlinks focusable", r =>
            r.Hyperlinks.Count(l => l.KeyboardFocusable).ToString());
        Row(sb, reports, "interactive children", r => r.InteractiveChildren.Length.ToString());
        Row(sb, reports, "interactive focusable", r =>
            r.InteractiveChildren.Count(c => c.KeyboardFocusable).ToString());
        Row(sb, reports, "focusable elements", r => r.FocusableElements.Length.ToString());
        Row(sb, reports, "axe errors", r =>
            r.AxeErrors.Length == 0 ? "0" : $"**{r.AxeErrors.Length}**");
        sb.AppendLine();

        foreach (VariantReport report in reports)
        {
            sb.AppendLine($"## {report.Variant}");
            sb.AppendLine();
            if (report.Error is not null)
            {
                sb.AppendLine("```");
                sb.AppendLine(report.Error);
                sb.AppendLine("```");
                sb.AppendLine();
                continue;
            }

            sb.AppendLine("Control-type histogram: "
                + string.Join(", ", report.ControlTypeHistogram
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => $"{kv.Key}={kv.Value}")));
            sb.AppendLine();

            if (report.HeadingLevels.Length > 0)
            {
                sb.AppendLine("Heading levels exposed:");
                foreach (string heading in report.HeadingLevels)
                {
                    sb.AppendLine($"- {heading}");
                }
                sb.AppendLine();
            }

            if (report.Hyperlinks.Length > 0)
            {
                sb.AppendLine("| hyperlink | HelpText | focusable | invoke |");
                sb.AppendLine("|---|---|---|---|");
                foreach (LinkReport link in report.Hyperlinks)
                {
                    sb.AppendLine(
                        $"| {link.Name} | {(link.HelpText.Length == 0 ? "—" : link.HelpText)} "
                        + $"| {link.KeyboardFocusable} | {link.InvokeSupported} |");
                }
                sb.AppendLine();
            }

            if (report.InteractiveChildren.Length > 0)
            {
                sb.AppendLine("| interactive child | focusable | invoke/toggle |");
                sb.AppendLine("|---|---|---|");
                foreach (LinkReport child in report.InteractiveChildren)
                {
                    sb.AppendLine(
                        $"| {child.Name} | {child.KeyboardFocusable} | {child.InvokeSupported} |");
                }
                sb.AppendLine();
            }

            if (report.AxeErrors.Length > 0)
            {
                sb.AppendLine("axe-windows errors:");
                foreach (string error in report.AxeErrors)
                {
                    sb.AppendLine($"- {error}");
                }
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(report.ReadingOrderText))
            {
                sb.AppendLine("<details><summary>reading-order text</summary>");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(report.ReadingOrderText);
                sb.AppendLine("```");
                sb.AppendLine();
                sb.AppendLine("</details>");
                sb.AppendLine();
            }
            else if (report.ReadingOrderError is not null)
            {
                sb.AppendLine($"reading-order read failed: `{report.ReadingOrderError}`");
                sb.AppendLine();
            }
        }

        sb.AppendLine("## Manual AT pass (not covered above)");
        sb.AppendLine();
        sb.AppendLine(
            "UIA exposure is necessary but not sufficient: §10.6 makes recorded JAWS + "
            + "NVDA evidence the pass criterion, and no probe can hear a screen reader. "
            + "For EACH variant (`ContainerSpike.exe --container flow|items`):");
        sb.AppendLine();
        sb.AppendLine("1. **Say-all** (NVDA `Insert+Down`, JAWS `Insert+Down`) from the top — "
            + "does it read the whole note without stalling at the code block, table or "
            + "embed card, and in authored order?");
        sb.AppendLine("2. **Heading nav** (`H`, then `1`/`2`) — does it land on both headings "
            + "and announce their level?");
        sb.AppendLine("3. **Link nav** (`K`) — does it reach all links, and is the unresolved "
            + "one announced differently from the resolved one?");
        sb.AppendLine("4. **List nav** (`L` to jump to a list, `I` to move by item) — does it "
            + "work at all, and is the nested item announced with its depth?");
        sb.AppendLine("5. **Tab** — do the task checkboxes, the code Copy button and the embed "
            + "card take focus in reading order, and does Space/Enter act on them?");
        sb.AppendLine("6. **Browse vs focus mode** — does the reader enter browse mode on the "
            + "surface automatically, and does typing still reach the app?");
        sb.AppendLine();
        sb.AppendLine("Record the answers in `w_c_matrix.md` with the AT versions used.");
        return sb.ToString();
    }

    private static void Row(
        StringBuilder sb, IReadOnlyList<VariantReport> reports, string label,
        Func<VariantReport, string> cell)
    {
        sb.AppendLine($"| {label} | "
            + string.Join(" | ", reports.Select(r => r.Error is null ? cell(r) : "ERROR"))
            + " |");
    }
}
