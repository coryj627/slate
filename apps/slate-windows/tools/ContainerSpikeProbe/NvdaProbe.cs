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

        using var nvda = new NvdaDriver();
        Console.WriteLine("connecting to NVDA (starts the bundled portable copy) ...");
        await nvda.ConnectAsync();
        try
        {
            foreach (string variant in variants)
            {
                Console.WriteLine($"NVDA pass: '{variant}' ...");
                results.Add(await ProbeAsync(nvda, variant));
            }
        }
        finally
        {
            await nvda.DisconnectAsync();
        }

        File.WriteAllText(
            Path.Combine(outputDirectory, "container-spike-nvda.json"),
            JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        string markdown = RenderNvda(results);
        File.WriteAllText(
            Path.Combine(outputDirectory, "container-spike-nvda.md"), markdown);
        Console.WriteLine();
        Console.WriteLine(markdown);
        return results.Any(r => r.Error is not null) ? 1 : 0;
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
                try
                {
                    spoken = await nvda.SendCommandAndGetSpokenTextAsync(command)
                        ?? string.Empty;
                }
                catch (Exception exception)
                {
                    spoken = $"<error: {exception.Message}>";
                }

                report.Steps.Add(new NvdaStep
                {
                    Label = label,
                    Spoken = Collapse(spoken),
                    Expected = expect,
                    Matched = expect.Length == 0
                        || spoken.Contains(expect, StringComparison.OrdinalIgnoreCase),
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
            sb.AppendLine($"{matched}/{report.Steps.Count} steps produced the expected speech.");
            sb.AppendLine();
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
    public bool Matched { get; set; }
}
