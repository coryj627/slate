// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using NvdaTestingDriver;
using NvdaTestingDriver.Commands;
using NvdaTestingDriver.Commands.NvdaCommands;

namespace ContainerSpikeProbe;

/// The half §10.6 makes the actual pass criterion: what NVDA SAYS.
///
/// UIA exposure is necessary but not sufficient. The provider probe found
/// zero `Hyperlink` control types in the FlowDocument variant, but that
/// does not settle whether NVDA can reach those links — in a UIA document
/// NVDA builds its browse-mode buffer from the text pattern and can find
/// embedded objects through text ranges rather than through control-tree
/// children. Only NVDA itself can answer that, so this drives it directly.
///
/// Uses `NvdaTestingDriver`, which bundles a portable NVDA and controls it
/// through a modified NvdaRemote plugin. **Currency caveat**, recorded
/// rather than glossed: the package is `0.2.0-beta` and its repository has
/// been unchanged since 2022-12-08, so the NVDA it drives is not the NVDA
/// 2026.x a user runs. Treat a PASS as strong evidence and a FAIL as worth
/// re-checking by hand before acting on it.
internal static class NvdaProbe
{
    /// One quick-nav command per requirement §W3-1 item 4 states. The
    /// expectation strings are what a reader MUST hear for that
    /// requirement to be met; they are matched leniently (substring,
    /// case-insensitive) because NVDA's exact phrasing varies by version.
    private static readonly (string Label, INvdaCommand Command, string Expect)[] Steps =
    {
        ("say all", NavigatingSystemCaretCommands.SayAll, "Reading container spike"),
        ("next heading #1", BrowseModeCommands.NextHeading, "Reading container spike"),
        ("next heading #2", BrowseModeCommands.NextHeading, "Second level heading"),
        ("heading level 1", BrowseModeCommands.NextHeading1, "heading"),
        ("heading level 2", BrowseModeCommands.NextHeading2, "heading"),
        ("next link #1", BrowseModeCommands.NextLink, "resolved note"),
        ("next link #2", BrowseModeCommands.NextLink, "absent note"),
        ("next link #3", BrowseModeCommands.NextLink, "tag"),
        ("next link #4", BrowseModeCommands.NextLink, "Smith"),
        ("next list", BrowseModeCommands.NextList, "list"),
        ("next list item #1", BrowseModeCommands.NextListItem, "first bullet"),
        ("next list item #2", BrowseModeCommands.NextListItem, "second bullet"),
        ("next list item #3", BrowseModeCommands.NextListItem, "nested bullet"),
        ("next table", BrowseModeCommands.NextTable, "table"),
        ("next button", BrowseModeCommands.NextButton, "button"),
        ("read current line", NavigatingSystemCaretCommands.ReadCurrentLine, ""),
    };

    public static async Task<int> RunAsync(string outputDirectory, string[] variants)
    {
        Directory.CreateDirectory(outputDirectory);
        var results = new List<NvdaVariantReport>();

        Preflight();

        var nvda = new NvdaDriver();
        Console.WriteLine("connecting to NVDA (starts the bundled portable copy) ...");
        try
        {
            await nvda.ConnectAsync();
        }
        catch (Exception exception)
        {
            // Connect is where this fails, and it used to fail with an
            // unhandled exception and an EMPTY output directory — the
            // worst possible outcome, since the run has to be done by a
            // human on an interactive desktop and there was nothing to
            // diagnose afterwards.
            string diagnosis = Diagnose(exception);
            File.WriteAllText(
                Path.Combine(outputDirectory, "container-spike-nvda.md"),
                "# W3-1 container spike — NVDA pass FAILED TO START\n\n"
                + diagnosis + "\n\n## Exception\n\n```\n" + exception + "\n```\n");
            Console.Error.WriteLine(diagnosis);
            Console.Error.WriteLine(exception.Message);
            return 2;
        }

        try
        {
            foreach (string variant in variants)
            {
                Console.WriteLine($"NVDA pass: '{variant}' ...");
                results.Add(await ProbeAsync(nvda, variant));
                // Written after EVERY variant, not once at the end: a
                // hang or crash on the second variant must not discard
                // the first one's evidence.
                Persist(outputDirectory, results);
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"NVDA pass aborted: {exception.Message}");
            results.Add(new NvdaVariantReport
            {
                Variant = "<aborted>",
                Error = exception.ToString(),
            });
        }
        finally
        {
            try
            {
                await nvda.DisconnectAsync();
            }
            catch
            {
                // Disconnect failing must not discard the results above.
            }
        }

        Persist(outputDirectory, results);
        Console.WriteLine();
        Console.WriteLine(RenderNvda(results));

        // NvdaDriver.Dispose() waits on an internal task that is already
        // cancelled after a failed run and rethrows — which killed the
        // process with an unhandled AggregateException AFTER the report
        // was written, making a completed run look like a crash.
        try
        {
            nvda.Dispose();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"(ignored) NVDA driver dispose: {exception.Message}");
        }

        bool anyMeasured = results.Any(r => r.Steps.Any(s => !s.Errored));
        return anyMeasured && results.All(r => r.Error is null) ? 0 : 1;
    }

    private static void Persist(string outputDirectory, List<NvdaVariantReport> results)
    {
        File.WriteAllText(
            Path.Combine(outputDirectory, "container-spike-nvda.json"),
            JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(
            Path.Combine(outputDirectory, "container-spike-nvda.md"), RenderNvda(results));
    }

    /// Report the two conditions that actually stop this run, BEFORE
    /// spending a minute discovering them the hard way.
    private static void Preflight()
    {
        string[] conflicting = Process.GetProcesses()
            .Select(p =>
            {
                try
                {
                    return p.ProcessName;
                }
                catch
                {
                    return string.Empty;
                }
            })
            .Where(name => name.Contains("nvda", StringComparison.OrdinalIgnoreCase)
                || name.Contains("jfw", StringComparison.OrdinalIgnoreCase)
                || name.Contains("fsdomsrv", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToArray();

        if (conflicting.Length > 0)
        {
            Console.Error.WriteLine(
                $"warning: screen reader process(es) already running ({string.Join(", ", conflicting)}). "
                + "The driver starts its OWN portable NVDA; two instances fight over the "
                + "speech channel and the connect usually fails or returns silence. Exit "
                + "them first.");
        }

        if (!Environment.UserInteractive)
        {
            Console.Error.WriteLine(
                "warning: not on an interactive window station. NVDA cannot attach to a "
                + "desktop it has no access to.");
        }
    }

    private static string Diagnose(Exception exception)
    {
        string message = exception.ToString();
        if (message.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || exception is TimeoutException)
        {
            return "The driver started NVDA but never completed the remote handshake. The "
                + "usual causes, in order: another screen reader is already running; the "
                + "bundled 2022-era portable NVDA does not run on this Windows build; or "
                + "the NvdaRemote plugin was blocked. Check whether an `nvda.exe` appeared "
                + "in Task Manager while this ran.";
        }
        if (message.Contains("Access", StringComparison.OrdinalIgnoreCase)
            || message.Contains("denied", StringComparison.OrdinalIgnoreCase))
        {
            return "Access denied starting or attaching to NVDA. The portable copy is "
                + "unpacked under the package folder; SmartScreen, an EDR agent, or a "
                + "non-elevated session can all block it.";
        }
        return "NVDA failed to start or connect. The bundled copy is `0.2.0-beta` from "
            + "2022 and this is the most likely place for that staleness to bite — see the "
            + "currency table in `w3_1_container_spike.md`.";
    }

    private static async Task<NvdaVariantReport> ProbeAsync(NvdaDriver nvda, string variant)
    {
        var report = new NvdaVariantReport { Variant = variant };
        Process? process = null;
        try
        {
            process = SpikeLauncher.Start(variant);
            using var automation = new UIA3Automation();
            Window window = SpikeLauncher.WaitForWindow(
                process, automation, TimeSpan.FromSeconds(30));

            // NVDA follows focus, so the window must genuinely be
            // foreground before any command means anything.
            window.SetForeground();
            window.Focus();
            await Task.Delay(1500);

            foreach ((string label, INvdaCommand command, string expect) in Steps)
            {
                string spoken;
                bool errored = false;
                try
                {
                    spoken = await nvda.SendCommandAndGetSpokenTextAsync(command)
                        ?? string.Empty;
                }
                catch (Exception exception)
                {
                    spoken = $"<error: {exception.Message}>";
                    errored = true;
                }

                report.Steps.Add(new NvdaStep
                {
                    Label = label,
                    Spoken = Collapse(spoken),
                    Expected = expect,
                    Errored = errored,
                    // An errored step is never a match, whatever it was
                    // expecting. A step with no expectation used to score
                    // as a pass even when capture had thrown, which put a
                    // reassuring "1/16" on a run that measured nothing.
                    Matched = !errored
                        && (expect.Length == 0
                            || spoken.Contains(expect, StringComparison.OrdinalIgnoreCase)),
                });
            }
        }
        catch (Exception exception)
        {
            report.Error = exception.ToString();
        }
        finally
        {
            SpikeLauncher.TryKill(process);
        }
        return report;
    }

    private static string Collapse(string value)
    {
        value = value.Replace("\r", " ").Replace("\n", " | ").Trim();
        while (value.Contains("  ", StringComparison.Ordinal))
        {
            value = value.Replace("  ", " ", StringComparison.Ordinal);
        }
        return value.Length <= 300 ? value : value[..300] + "…";
    }

    private static string RenderNvda(IReadOnlyList<NvdaVariantReport> reports)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# W3-1 container spike — what NVDA actually says");
        sb.AppendLine();
        sb.AppendLine(
            "Driven with `NvdaTestingDriver` (bundled portable NVDA). **Currency "
            + "caveat:** package `0.2.0-beta`, repository unchanged since 2022-12-08, "
            + "so this is not the NVDA 2026.x a user runs. A PASS here is strong "
            + "evidence; a FAIL is worth re-checking by hand before acting on it.");
        sb.AppendLine();

        foreach (NvdaVariantReport report in reports)
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
            int matched = report.Steps.Count(s => s.Matched);
            int errored = report.Steps.Count(s => s.Errored);
            sb.AppendLine($"{matched}/{report.Steps.Count} steps produced the expected speech.");
            sb.AppendLine();

            // Uniform failure is a TOOLING result, not a measurement, and
            // must be labelled as such — a table of "NO" against every
            // container feature reads like a damning finding about the
            // container when it is nothing of the kind.
            if (errored == report.Steps.Count)
            {
                sb.AppendLine(
                    "> **Every step errored — this measured nothing about the container.** "
                    + "Speech capture never completed, so these rows carry no information "
                    + "about what NVDA can or cannot reach. Do not read the `NO` column as "
                    + "a finding about this container. See the currency note above: this is "
                    + "the failure mode a 2022-era bundled NVDA was expected to produce.");
                sb.AppendLine();
            }
            else if (errored > 0)
            {
                sb.AppendLine(
                    $"> {errored} step(s) errored rather than returning speech; those rows "
                    + "measure the harness, not the container.");
                sb.AppendLine();
            }
            sb.AppendLine("| step | expected | NVDA said | ok |");
            sb.AppendLine("|---|---|---|---|");
            foreach (NvdaStep step in report.Steps)
            {
                sb.AppendLine(
                    $"| {step.Label} | {(step.Expected.Length == 0 ? "—" : step.Expected)} "
                    + $"| {(step.Spoken.Length == 0 ? "*(silence)*" : step.Spoken)} "
                    + $"| {(step.Matched ? "yes" : "**NO**")} |");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## How to read this");
        sb.AppendLine();
        sb.AppendLine(
            "`next link` steps are the decisive ones for the container choice: the UIA "
            + "probe found NO `Hyperlink` control types in the FlowDocument variant, but "
            + "NVDA can reach links through text-range embedded objects. If the link "
            + "steps speak in `flow`, the missing control types are not disqualifying; "
            + "if they are silent, they are.");
        sb.AppendLine();
        sb.AppendLine(
            "`next list` / `next list item` test the owner call that lists expose native "
            + "semantics. Neither container produced a `ListItem` control type, so "
            + "silence here is expected and quantifies what custom `AutomationPeer`s "
            + "must add.");
        return sb.ToString();
    }
}

internal sealed class NvdaVariantReport
{
    public string Variant { get; set; } = string.Empty;
    public string? Error { get; set; }
    public List<NvdaStep> Steps { get; } = new();
}

internal sealed class NvdaStep
{
    public string Label { get; set; } = string.Empty;
    public string Expected { get; set; } = string.Empty;
    public string Spoken { get; set; } = string.Empty;
    /// Capture threw rather than returning speech. Kept distinct from
    /// "spoke the wrong thing": one is a broken harness, the other is a
    /// finding about the container.
    public bool Errored { get; set; }
    public bool Matched { get; set; }
}
