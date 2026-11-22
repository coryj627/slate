// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>
    /// #1708 (plan 47 CB-20, CD12), cross-process: NVDA places the reader on
    /// an embedded element through <c>RangeFromChild</c> and reads the Line
    /// that encloses it. A pending diagram's Line reads "Diagram, rendering"
    /// (on its own line above the source: the owner's answer to Call 1,
    /// "Before the source"); once rendered, its Line reads its description;
    /// a failed diagram's Line reads its source's last line and never its
    /// description (CD12(a)).
    /// </summary>
    [Fact]
    public void ReadingDiagram_LineReadingSpeaksTheDescriptionAndThePlaceholder()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), $"slate-reading-diagram-lines-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Diagram Lines Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(
            Path.Combine(vaultRoot, "note.md"),
            "# Line note\n\nBefore the diagrams.\n\n```mermaid\nflowchart LR\nA --> B\nB --> C\n```\n\n"
                + "Between the diagrams.\n\n```mermaid\nweirdDiagram\nstuff\n```\n\nAfter the diagrams.\n");

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
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] = $"slate-reading-diagram-lines-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            // The census-gated seam holds the diagram stage, so the journey
            // reads the pending Lines before the diagrams land.
            startInfo.Environment["SLATE_TEST_DIAGRAM_STAGE_DELAY_MS"] = "15000";
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");
            if (!HasInteractiveDesktop(process, "reading-diagram-lines"))
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
            _ = WaitForElement(window, "ReadingSurface", TimeSpan.FromSeconds(10));
            // R-4: the runtime item-name census at this journey's
            // representative state (the reading view, diagrams pending).
            AssertItemNamesAreSpeakable(process, "reading-diagram-lines");

            AutomationElement[] Diagrams() => window.FindAllDescendants(cf => cf.ByLocalizedControlType("diagram"));
            Assert.True(
                SpinWait.SpinUntil(
                    () => Diagrams().Count(diagram => diagram.Name == "Diagram, rendering") == 2,
                    TimeSpan.FromSeconds(30)),
                "the two diagrams were never pending together: " + string.Join(" | ", Diagrams().Select(diagram => diagram.Name)));
            var failures = new List<string>();
            AutomationElement surface = WaitForElement(window, "ReadingSurface", TimeSpan.FromSeconds(10));
            FlaUI.Core.Patterns.ITextPattern text = surface.Patterns.Text.Pattern;
            foreach (AutomationElement pending in Diagrams().Where(diagram => diagram.Name == "Diagram, rendering"))
            {
                string line = EnclosingLineOf(text, pending);
                if (line != "Diagram, rendering")
                {
                    failures.Add($"a pending diagram's Line reads \"{line}\"");
                }
            }
            Assert.True(
                Diagrams().Count(diagram => diagram.Name == "Diagram, rendering") == 2,
                "a diagram landed before its pending Line was read: raise the stage delay");

            // The rendered diagram completes its placeholder (a cold runner's
            // first mermaid render keeps the 300 s bound).
            Assert.True(
                SpinWait.SpinUntil(
                    () => Diagrams().Any(diagram => diagram.Name == "Flowchart with 2 steps.")
                        && !Diagrams().Any(diagram => diagram.Name == "Diagram, rendering"),
                    TimeSpan.FromSeconds(300)),
                "the diagrams never landed: " + string.Join(" | ", Diagrams().Select(diagram => diagram.Name)));
            surface = WaitForElement(window, "ReadingSurface", TimeSpan.FromSeconds(10));
            text = surface.Patterns.Text.Pattern;
            AutomationElement rendered = Diagrams().Single(diagram => diagram.Name == "Flowchart with 2 steps.");
            AutomationElement failed = Diagrams().Single(diagram => diagram.Name.StartsWith("Mermaid diagram, source:", StringComparison.Ordinal));
            string renderedLine = EnclosingLineOf(text, rendered);
            if (renderedLine != "Flowchart with 2 steps.")
            {
                failures.Add($"the rendered diagram's Line reads \"{renderedLine}\"");
            }
            string failedLine = EnclosingLineOf(text, failed);
            if (failedLine != "stuff")
            {
                failures.Add($"the failed diagram's Line reads \"{failedLine}\"");
            }
            string document = text.DocumentRange.GetText(-1);
            if (document.Contains("Diagram, rendering", StringComparison.Ordinal))
            {
                failures.Add("the landed document still holds \"Diagram, rendering\"");
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures));
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

    /// <summary>#1708: the Line that encloses an embedded element's own range,
    /// cross-process, as NVDA reads it on reaching the element: its words, with
    /// the object's space, U+FFFC and the break trimmed.</summary>
    private static string EnclosingLineOf(FlaUI.Core.Patterns.ITextPattern text, AutomationElement element)
    {
        FlaUI.Core.ITextRange range = text.RangeFromChild(element);
        range.ExpandToEnclosingUnit(TextUnit.Line);
        return (range.GetText(-1) ?? string.Empty).Trim(' ', '￼', '\r', '\n');
    }

    /// <summary>#1708, for the RangeFromChild census: a math or diagram
    /// element's enclosing Line reads its name, or the failure says what it
    /// reads.</summary>
    private static string? LineReadsTheName(FlaUI.Core.Patterns.ITextPattern text, string kind, AutomationElement element)
    {
        string name = element.Properties.Name.ValueOrDefault ?? string.Empty;
        string line = EnclosingLineOf(text, element);
        return name.Length > 0 && line.Contains(name, StringComparison.Ordinal)
            ? null
            : $"{kind}: its enclosing Line reads \"{line}\", not its name \"{name}\"";
    }
}
