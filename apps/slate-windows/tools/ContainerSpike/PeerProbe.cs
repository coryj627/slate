// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;

namespace ContainerSpike;

/// The PROVIDER-side half of the §10.6 measurement.
///
/// A UIA *client* probe (FlaUI) needs the interactive desktop: a session-0
/// process cannot see a session-1 UIA tree at all. The provider side has
/// no such constraint — `AutomationPeer`s are ordinary objects the
/// framework builds from the element tree — so everything that is a
/// property of how WPF EXPOSES each container can be measured here, in
/// process, and in CI.
///
/// What this establishes: control types, native `List`/`ListItem`
/// exposure, whether `AutomationProperties.HeadingLevel` survives onto the
/// peer, `HelpText` per link, and whether a Text pattern provider exists
/// over the surface (and what it reads back).
///
/// What it CANNOT establish, and must not be read as establishing: how a
/// UIA client aggregates these peers after normalization, live keyboard
/// focus and tab order, axe-windows results, and what a screen reader
/// actually says. Those need the desktop and, for the last one, a human.
internal static class PeerProbe
{
    /// One probe string per element the fixture places in reading order.
    /// A reader doing say-all must meet every one of these, in this order.
    private static readonly string[] Landmarks =
    {
        "Reading container spike", "resolved note", "absent note",
        "Second level heading", "first bullet", "nested bullet",
        "ordered one", "an open task", "a done task", "block quote",
        "fn main", "header a", "cell 1", "Embedded note",
    };

    public static int Run(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var model = Fixture.Load();

        var reports = new List<object>();
        var markdown = new StringBuilder();
        markdown.AppendLine("# W3-1 container spike — provider-side (AutomationPeer) measurements");
        markdown.AppendLine();
        markdown.AppendLine(
            "Measured in-process from the WPF `AutomationPeer` tree, so it runs without "
            + "the interactive desktop a UIA client probe requires. Client-side "
            + "behaviour (focus order, axe, and what JAWS/NVDA actually say) is NOT "
            + "covered — see `ContainerSpikeProbe` and the manual script.");
        markdown.AppendLine();

        var summaries = new List<Summary>();
        foreach (string variant in new[] { "flow", "flowpeers", "items" })
        {
            // Measured BOTH ways on purpose. Detached Measure/Arrange is
            // enough to lay a FlowDocument out, but some WPF peers are
            // only constructed once the element has a PresentationSource —
            // so a detached-only reading risks UNDER-reporting the flow
            // variant and quietly biasing the comparison it exists to
            // settle. Where the two disagree, the windowed reading wins
            // and the difference is reported rather than smoothed over.
            summaries.Add(MeasureOnce(variant, model, hosted: false));
            Summary hosted = MeasureOnce(variant, model, hosted: true);
            if (hosted.Error is null)
            {
                summaries.Add(hosted);
            }
        }
        reports.AddRange(summaries);

        Render(markdown, summaries);

        File.WriteAllText(
            Path.Combine(outputDirectory, "container-spike-peers.json"),
            JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(
            Path.Combine(outputDirectory, "container-spike-peers.md"), markdown.ToString());
        Console.WriteLine(markdown.ToString());
        return 0;
    }

    /// Build one variant and read its peer tree, either detached or
    /// hosted in an off-screen `Window` so a `PresentationSource` exists.
    private static Summary MeasureOnce(
        string variant, IReadOnlyList<(uniffi.slate_uniffi.ReadingBlock,
            uniffi.slate_uniffi.ReadingBlockInlines)> model, bool hosted)
    {
        string label = hosted ? $"{variant}/windowed" : $"{variant}/detached";
        FrameworkElement surface = variant switch
        {
            "flow" => FlowDocumentBuilder.Build(model),
            "flowpeers" => FlowDocumentBuilder.Build(model, withSemanticPeers: true),
            _ => ItemsControlBuilder.Build(model),
        };

        Window? window = null;
        try
        {
            if (hosted)
            {
                window = new Window
                {
                    Content = surface,
                    Width = 900,
                    Height = 760,
                    // Off-screen rather than hidden: WPF skips layout for a
                    // collapsed window, and a peer tree over an unlaid-out
                    // element would be the very artefact this guards against.
                    Left = -32000,
                    Top = -32000,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                };
                window.Show();
            }

            surface.Measure(new Size(900, 4000));
            surface.Arrange(new Rect(0, 0, 900, 4000));
            surface.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Loaded);

            return Measure(label, surface);
        }
        catch (Exception exception)
        {
            return new Summary { Variant = label, Error = exception.Message };
        }
        finally
        {
            try
            {
                window?.Close();
            }
            catch
            {
                // A session-0 window station may refuse; the detached
                // reading still stands on its own.
            }
        }
    }

    private static Summary Measure(string variant, FrameworkElement surface)
    {
        var summary = new Summary { Variant = variant };
        AutomationPeer? root = UIElementAutomationPeer.CreatePeerForElement(surface);
        if (root is null)
        {
            summary.Error = "no AutomationPeer was created for the surface";
            return summary;
        }

        summary.SurfaceControlType = Safe(() => root.GetAutomationControlType().ToString(), "error");

        var visited = new List<AutomationPeer>();
        Walk(root, visited, depth: 0, maxDepth: 40);
        summary.PeerCount = visited.Count;

        foreach (AutomationPeer peer in visited)
        {
            string type = Safe(() => peer.GetAutomationControlType().ToString(), "error");
            summary.ControlTypes.TryGetValue(type, out int count);
            summary.ControlTypes[type] = count + 1;

            string name = Safe(() => peer.GetName() ?? string.Empty, string.Empty);
            string help = Safe(() => peer.GetHelpText() ?? string.Empty, string.Empty);

            string heading = Safe(() => peer.GetHeadingLevel().ToString(), "error");
            if (heading is not ("None" or "error"))
            {
                summary.HeadingLevels.Add($"{heading}: {Truncate(name, 48)}");
            }

            if (type == "Hyperlink")
            {
                summary.Links.Add(new LinkRow
                {
                    Name = Truncate(name, 48),
                    HelpText = Truncate(help, 60),
                    InvokeProvider = Safe(
                        () => peer.GetPattern(PatternInterface.Invoke) is not null, false),
                });
            }

            if (type is "CheckBox" or "Button")
            {
                summary.InteractiveChildren.Add(new LinkRow
                {
                    Name = Truncate(name, 48),
                    HelpText = Truncate(help, 60),
                    InvokeProvider = Safe(
                        () => peer.GetPattern(PatternInterface.Invoke) is not null
                            || peer.GetPattern(PatternInterface.Toggle) is not null,
                        false),
                });
            }
        }

        summary.ListPeers = summary.ControlTypes.TryGetValue("List", out int lists) ? lists : 0;
        summary.ListItemPeers =
            summary.ControlTypes.TryGetValue("ListItem", out int items) ? items : 0;
        summary.HeadingLevelSurvives = summary.HeadingLevels.Count > 0;

        MeasureText(visited, summary);
        return summary;
    }

    /// The decisive one: is there a Text provider over the surface, and
    /// does its document range read back the whole note in authored order?
    private static void MeasureText(List<AutomationPeer> peers, Summary summary)
    {
        // No Text provider means no landmark can be found, so record them
        // all as missing rather than leaving the list empty — "0 found,
        // none missing" reads as though nothing were wrong.
        if (!peers.Any(p => Safe(
            () => p.GetPattern(PatternInterface.Text) as ITextProvider, null) is not null))
        {
            summary.LandmarksMissing.AddRange(Landmarks);
            return;
        }

        foreach (AutomationPeer peer in peers)
        {
            ITextProvider? text = Safe(
                () => peer.GetPattern(PatternInterface.Text) as ITextProvider, null);
            if (text is null)
            {
                continue;
            }

            summary.TextProviderPresent = true;
            summary.TextProviderHost = Safe(
                () => peer.GetAutomationControlType().ToString(), "error");
            string document = Safe(
                () => text.DocumentRange?.GetText(-1) ?? string.Empty, string.Empty);
            summary.ReadingOrderText = document;
            summary.ReadingOrderLength = document.Length;

            int cursor = 0;
            foreach (string landmark in Landmarks)
            {
                int index = document.IndexOf(landmark, StringComparison.Ordinal);
                if (index < 0)
                {
                    summary.LandmarksMissing.Add(landmark);
                    continue;
                }
                summary.LandmarksFound.Add(landmark);
                if (index < cursor)
                {
                    summary.LandmarksOutOfOrder.Add(landmark);
                }
                cursor = index;
            }
            return;
        }
    }

    private static void Walk(
        AutomationPeer peer, List<AutomationPeer> visited, int depth, int maxDepth)
    {
        visited.Add(peer);
        if (depth >= maxDepth)
        {
            return;
        }
        List<AutomationPeer>? children = Safe(() => peer.GetChildren(), null);
        if (children is null)
        {
            return;
        }
        foreach (AutomationPeer child in children)
        {
            Walk(child, visited, depth + 1, maxDepth);
        }
    }

    private static void Render(StringBuilder sb, List<Summary> summaries)
    {
        sb.AppendLine("| measurement | " + string.Join(" | ", summaries.Select(s => s.Variant)) + " |");
        sb.AppendLine("|---|" + string.Join("", summaries.Select(_ => "---|")));
        Row(sb, summaries, "surface control type", s => s.SurfaceControlType ?? "—");
        Row(sb, summaries, "peers in tree", s => s.PeerCount.ToString());
        Row(sb, summaries, "Text provider present", s => s.TextProviderPresent ? "**yes**" : "**no**");
        Row(sb, summaries, "Text provider host", s => s.TextProviderHost ?? "—");
        Row(sb, summaries, "reading-order chars", s => s.ReadingOrderLength.ToString());
        Row(sb, summaries, "landmarks found", s => $"{s.LandmarksFound.Count}/{Landmarks.Length}");
        Row(sb, summaries, "landmarks missing", s =>
            s.LandmarksMissing.Count == 0 ? "none" : string.Join(", ", s.LandmarksMissing));
        Row(sb, summaries, "landmarks out of order", s =>
            s.LandmarksOutOfOrder.Count == 0 ? "none" : string.Join(", ", s.LandmarksOutOfOrder));
        Row(sb, summaries, "List peers", s => s.ListPeers.ToString());
        Row(sb, summaries, "ListItem peers", s => s.ListItemPeers.ToString());
        Row(sb, summaries, "HeadingLevel survives", s => s.HeadingLevelSurvives ? "**yes**" : "**no**");
        Row(sb, summaries, "hyperlink peers", s => s.Links.Count.ToString());
        Row(sb, summaries, "links w/ HelpText", s =>
            s.Links.Count(l => l.HelpText.Length > 0).ToString());
        Row(sb, summaries, "interactive children", s => s.InteractiveChildren.Count.ToString());
        sb.AppendLine();

        foreach (Summary summary in summaries)
        {
            sb.AppendLine($"## {summary.Variant}");
            sb.AppendLine();
            if (summary.Error is not null)
            {
                sb.AppendLine($"ERROR: {summary.Error}");
                sb.AppendLine();
                continue;
            }
            sb.AppendLine("Control types: " + string.Join(", ", summary.ControlTypes
                .OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}")));
            sb.AppendLine();
            if (summary.HeadingLevels.Count > 0)
            {
                sb.AppendLine("Heading levels on peers:");
                foreach (string heading in summary.HeadingLevels)
                {
                    sb.AppendLine($"- {heading}");
                }
                sb.AppendLine();
            }
            if (summary.Links.Count > 0)
            {
                sb.AppendLine("| hyperlink | HelpText | invoke provider |");
                sb.AppendLine("|---|---|---|");
                foreach (LinkRow link in summary.Links)
                {
                    sb.AppendLine($"| {link.Name} | "
                        + $"{(link.HelpText.Length == 0 ? "—" : link.HelpText)} | "
                        + $"{link.InvokeProvider} |");
                }
                sb.AppendLine();
            }
            if (summary.InteractiveChildren.Count > 0)
            {
                sb.AppendLine("| interactive child | invoke/toggle provider |");
                sb.AppendLine("|---|---|");
                foreach (LinkRow child in summary.InteractiveChildren)
                {
                    sb.AppendLine($"| {child.Name} | {child.InvokeProvider} |");
                }
                sb.AppendLine();
            }
            if (!string.IsNullOrEmpty(summary.ReadingOrderText))
            {
                sb.AppendLine("<details><summary>reading-order text</summary>");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(summary.ReadingOrderText);
                sb.AppendLine("```");
                sb.AppendLine();
                sb.AppendLine("</details>");
                sb.AppendLine();
            }
        }
    }

    private static void Row(
        StringBuilder sb, List<Summary> summaries, string label, Func<Summary, string> cell)
    {
        sb.AppendLine($"| {label} | " + string.Join(" | ", summaries.Select(cell)) + " |");
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

    private sealed class Summary
    {
        public string Variant { get; set; } = string.Empty;
        public string? Error { get; set; }
        public string? SurfaceControlType { get; set; }
        public int PeerCount { get; set; }
        public Dictionary<string, int> ControlTypes { get; } = new();
        public int ListPeers { get; set; }
        public int ListItemPeers { get; set; }
        public bool HeadingLevelSurvives { get; set; }
        public List<string> HeadingLevels { get; } = new();
        public List<LinkRow> Links { get; } = new();
        public List<LinkRow> InteractiveChildren { get; } = new();
        public bool TextProviderPresent { get; set; }
        public string? TextProviderHost { get; set; }
        public string? ReadingOrderText { get; set; }
        public int ReadingOrderLength { get; set; }
        public List<string> LandmarksFound { get; } = new();
        public List<string> LandmarksMissing { get; } = new();
        public List<string> LandmarksOutOfOrder { get; } = new();
    }

    private sealed class LinkRow
    {
        public string Name { get; set; } = string.Empty;
        public string HelpText { get; set; } = string.Empty;
        public bool InvokeProvider { get; set; }
    }
}
