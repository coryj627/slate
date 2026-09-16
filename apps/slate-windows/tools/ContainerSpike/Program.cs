// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Windows;
using System.Windows.Automation;

namespace ContainerSpike;

/// W3-1 container spike host (#728, `w3_inline_runs_spec.md` §10.6).
///
///   ContainerSpike.exe --container flow
///   ContainerSpike.exe --container items
///
/// One window, one fixture note, one container. The probe launches this
/// twice and compares what UIA reports; a human runs it with NVDA or JAWS
/// for the half no probe can answer.
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        string container = "flow";
        string? probe = null;
        string outputDirectory = Path.Combine(AppContext.BaseDirectory, "spike-evidence");
        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--container":
                    container = args[i + 1].ToLowerInvariant();
                    break;
                case "--probe":
                    probe = args[i + 1].ToLowerInvariant();
                    break;
                case "--out":
                    outputDirectory = args[i + 1];
                    break;
            }
        }

        // Provider-side measurement needs no window and no desktop, so it
        // runs anywhere — including a session-0 shell and CI.
        if (probe == "peers")
        {
            return PeerProbe.Run(outputDirectory);
        }

        // Size and stability of the selected container. Also needs no
        // window, so it runs anywhere the peer probe does.
        if (probe == "perf")
        {
            return PerfProbe.Run(outputDirectory);
        }

        if (probe == "perfone")
        {
            int size = 0;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--size")
                {
                    _ = int.TryParse(args[i + 1], out size);
                }
            }
            return PerfProbe.RunSingle(outputDirectory, size);
        }

        var model = Fixture.Load();
        FrameworkElement surface = container switch
        {
            "items" => ItemsControlBuilder.Build(model),
            "flow" => FlowDocumentBuilder.Build(model),
            "flowpeers" => FlowDocumentBuilder.Build(model, withSemanticPeers: true),
            "richtext" => FlowDocumentBuilder.Build(
                model, withSemanticPeers: true, richTextHost: true),
            _ => throw new ArgumentException(
                $"unknown container '{container}' (expected 'flow', 'flowpeers', 'richtext' or 'items')"),
        };

        var window = new Window
        {
            // The title carries the variant so the probe attaches to the
            // right window and a human always knows which one is on screen.
            Title = $"Slate container spike — {container}",
            Width = 900,
            Height = 760,
            Content = surface,
            Background = Palette.Surface,
        };
        AutomationProperties.SetAutomationId(window, "ContainerSpikeWindow");

        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        return app.Run(window);
    }
}
