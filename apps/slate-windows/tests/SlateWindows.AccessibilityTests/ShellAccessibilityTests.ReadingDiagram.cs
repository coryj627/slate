// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>
    /// WB-20 (#1317), cross-process: the reading view's text, math and
    /// failure text arrive before the diagram renders. Each pending diagram
    /// is a placeholder group named "Diagram, rendering" (WD11) beside its
    /// source in the text; the rendered one completes its placeholder in
    /// place, and an unsupported one puts its failure header, reason and
    /// source in the surface's TextPattern. The census trace pins the order:
    /// the projection completed before the diagram stage landed.
    /// </summary>
    [Fact]
    public void ReadingDiagram_TextIsReadableBeforeTheDiagramRenders()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), $"slate-reading-diagram-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Diagram Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        // A diagram large enough that its cold render takes visible time.
        var flowchart = new StringBuilder("flowchart LR\n");
        for (int step = 0; step < 60; step++)
        {
            flowchart.Append($"N{step} --> N{step + 1}\n");
        }
        File.WriteAllText(
            Path.Combine(vaultRoot, "note.md"),
            "# Diagram note\n\nThe first paragraph.\n\n$$x^2 + 1$$\n\n"
                + "```mermaid\n" + flowchart + "```\n\n"
                + "```mermaid\nweirdDiagram\nstuff\n```\n\n"
                + "The paragraph after the diagrams.\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            ShellLaunch.ApplyDeviceState(startInfo);
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] = $"slate-reading-diagram-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            // The census-gated seam holds the diagram stage, so the journey
            // must see the placeholder before the diagram lands.
            startInfo.Environment["SLATE_TEST_DIAGRAM_STAGE_DELAY_MS"] = "8000";
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");
            if (!HasInteractiveDesktop(process, "reading-diagram"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process, automation, Path.Combine(logDirectory, "slate-windows.log"), TimeSpan.FromSeconds(30));
            AutomationElement filesTree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(30));
            WaitForTreeItemStartingWith(filesTree, automation, "note.md").Patterns.SelectionItem.Pattern.Select();
            WaitForEditor(window, automation, "note.md editor", TimeSpan.FromSeconds(10));
            WaitForMenuItem(window, "EditorMenu", "EditorToggleReadingModeMenuItem", TimeSpan.FromSeconds(10))
                .Patterns.Invoke.Pattern.Invoke();
            AutomationElement surface = WaitForElement(window, "ReadingSurface", TimeSpan.FromSeconds(10));
            // R-4: the runtime item-name census at this journey's
            // representative state (the reading view, diagrams pending).
            AssertItemNamesAreSpeakable(process, "reading-diagram");

            // The text arrives with the first projection: math and the first
            // paragraph, within the ordinary budget.
            AutomationElement? math = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () => (math = window.FindFirstDescendant(cf => cf.ByLocalizedControlType("math"))) is not null,
                    TimeSpan.FromSeconds(30)),
                "no math element appeared before the diagrams");
            Assert.Contains(
                "The first paragraph.",
                surface.Patterns.Text.Pattern.DocumentRange.GetText(-1),
                StringComparison.Ordinal);
            // The stage is held, so the text is readable while the diagrams are
            // still placeholders: the first projection did not wait for them.
            Assert.True(
                window.FindAllDescendants(cf => cf.ByLocalizedControlType("diagram"))
                    .Any(diagram => diagram.Name == "Diagram, rendering"),
                "the text arrived without a \"Diagram, rendering\" placeholder");
            Assert.False(
                window.FindAllDescendants(cf => cf.ByLocalizedControlType("diagram"))
                    .Any(diagram => diagram.Name.StartsWith("Flowchart", StringComparison.Ordinal)),
                "the diagram rendered before the text was read");

            // The rendered diagram completes its placeholder (a cold
            // runner's first mermaid render keeps the 300 s bound).
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindAllDescendants(cf => cf.ByLocalizedControlType("diagram"))
                        .Any(diagram => diagram.Name.StartsWith("Flowchart", StringComparison.Ordinal)),
                    TimeSpan.FromSeconds(300)),
                "the rendered diagram never completed its placeholder");
            Assert.True(
                SpinWait.SpinUntil(
                    () => !window.FindAllDescendants(cf => cf.ByLocalizedControlType("diagram"))
                        .Any(diagram => diagram.Name == "Diagram, rendering"),
                    TimeSpan.FromSeconds(30)),
                "a placeholder still reads \"Diagram, rendering\"");

            string text = surface.Patterns.Text.Pattern.DocumentRange.GetText(-1);
            Assert.Contains("Diagram dialect not supported", text, StringComparison.Ordinal);
            Assert.Contains("weirdDiagram", text, StringComparison.Ordinal);
            Assert.Contains("The paragraph after the diagrams.", text, StringComparison.Ordinal);
            Assert.DoesNotContain("N0 --> N1", text, StringComparison.Ordinal);

            string diag = ReadSharedLog(Path.Combine(logDirectory, "slate-census-diag.log"));
            int complete = diag.IndexOf("reading projection complete", StringComparison.Ordinal);
            int landed = diag.IndexOf("reading diagram stage landed", StringComparison.Ordinal);
            Assert.True(
                complete >= 0 && landed > complete,
                $"the projection did not complete before the diagram stage landed\n"
                + $"census diag: {(diag.Length <= 2500 ? diag : diag[^2500..])}");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5_000);
                }
            }
            process?.Dispose();
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
