// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>
    /// #1708 (plan 47 CB-20, the merge judge's M1), with the real mouse: a
    /// press in the margin left of an embedded image, a formula or a rendered
    /// diagram puts WPF's caret in the hidden speech at the line's start, and
    /// a drag down from there selects the element with it. A plain click there
    /// leaves the caret just after the element, where it is drawn at the
    /// line's height.
    /// </summary>
    [Fact]
    public void ReadingSurface_AMousePressBesideAnElementKeepsItInADrag()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), $"slate-reading-caret-mouse-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Caret Mouse Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(
            Path.Combine(vaultRoot, "note.md"),
            "# Mouse note\n\nBefore the image.\n\n![[box.svg]]\n\nAfter the image.\n\n$$\nx^2\n$$\n\nAfter the formula.\n\n"
                + "```mermaid\nflowchart LR\nA --> B\nB --> C\n```\n\nAfter the diagram.\n");
        File.WriteAllText(
            Path.Combine(vaultRoot, "box.svg"),
            "<svg xmlns='http://www.w3.org/2000/svg' width='160' height='60'><rect width='160' height='60' fill='teal'/></svg>");

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
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] = $"slate-reading-caret-mouse-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");
            if (!HasInteractiveDesktop(process, "reading-caret-mouse"))
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
            AssertItemNamesAreSpeakable(process, "reading-caret-mouse");
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindAllDescendants(cf => cf.ByLocalizedControlType("diagram"))
                        .Any(diagram => diagram.Name == "Flowchart with 2 steps."),
                    TimeSpan.FromSeconds(300)),
                "the diagram never rendered");

            window.SetForeground();
            surface = WaitForElement(window, "ReadingSurface", TimeSpan.FromSeconds(10));
            FlaUI.Core.Patterns.ITextPattern text = surface.Patterns.Text.Pattern;
            Rectangle page = surface.BoundingRectangle;
            var failures = new List<string>();
            foreach ((string kind, string hidden) in new[]
            {
                ("image", "Embedded image: box.svg"),
                ("formula", "x squared"),
                ("diagram", "Flowchart with 2 steps."),
            })
            {
                FlaUI.Core.ITextRange? speech = text.DocumentRange.FindText(hidden, true, false);
                if (speech is null)
                {
                    failures.Add($"{kind}: \"{hidden}\" is not in the document");
                    continue;
                }
                if (PointsBeside(speech, page) is not { } points)
                {
                    failures.Add($"{kind}: its line has no margin on screen to press in");
                    continue;
                }
                (Point press, Point release, Rectangle box) = points;

                Mouse.Drag(press, release);
                string selected = string.Empty;
                _ = SpinWait.SpinUntil(
                    () => (selected = text.GetSelection()[0].GetText(-1) ?? string.Empty).Length > 0,
                    TimeSpan.FromSeconds(5));
                if (!selected.Contains(hidden, StringComparison.Ordinal))
                {
                    failures.Add($"{kind}: a drag from beside it selected \"{selected}\", which leaves the element out");
                }

                // A press inside the drag's selection would wait for a drag, so
                // the selection is collapsed past it first, as a reader would.
                Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.RIGHT);
                Mouse.Click(press);
                // The release moves a plain click's caret past the element.
                if (!SpinWait.SpinUntil(
                    () => text.GetSelection()[0].CompareEndpoints(
                        TextPatternRangeEndpoint.Start, speech, TextPatternRangeEndpoint.End) > 0,
                    TimeSpan.FromSeconds(5)))
                {
                    FlaUI.Core.ITextRange caret = text.GetSelection()[0];
                    FlaUI.Core.ITextRange caretLine = caret.Clone();
                    caretLine.ExpandToEnclosingUnit(TextUnit.Line);
                    failures.Add(
                        $"{kind}: a click beside it left the caret in its hidden speech (against the speech's start "
                        + $"{caret.CompareEndpoints(TextPatternRangeEndpoint.Start, speech, TextPatternRangeEndpoint.Start)}, "
                        + $"its end {caret.CompareEndpoints(TextPatternRangeEndpoint.Start, speech, TextPatternRangeEndpoint.End)}; "
                        + $"the caret's line \"{caretLine.GetText(-1)}\"; pressed at {press}, the speech's box {box})");
                }
            }

            // Codex round 7, on the image's line: Home puts the caret on the
            // speech's start along the line, Shift+Down anchors a selection
            // there, and a Shift-click back on the anchor collapses it. The
            // caret stays on its anchor; only a plain click is moved.
            FlaUI.Core.ITextRange? image = text.DocumentRange.FindText("Embedded image: box.svg", true, false);
            if (image is null || PointsBeside(image, page) is not { } imagePoints)
            {
                failures.Add("image: its line has no margin on screen for the Shift-click");
            }
            else
            {
                Point anchorPoint = imagePoints.Press;
                Mouse.Click(anchorPoint);
                Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.HOME);
                Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.SHIFT, FlaUI.Core.WindowsAPI.VirtualKeyShort.DOWN);
                using (Keyboard.Pressing(FlaUI.Core.WindowsAPI.VirtualKeyShort.SHIFT))
                {
                    Mouse.Click(anchorPoint);
                }
                if (SpinWait.SpinUntil(
                    () => text.GetSelection()[0].CompareEndpoints(
                        TextPatternRangeEndpoint.Start, image, TextPatternRangeEndpoint.Start) != 0,
                    TimeSpan.FromSeconds(2)))
                {
                    failures.Add("image: a Shift-click back onto the selection's anchor moved the caret off it");
                }
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

    /// <summary>#1708 (the merge judge's M1): where to press beside an
    /// element's line and where to drag to past the element, on the screen.
    /// The hidden speech's box spans its line's height at the line's left;
    /// scrolled to the view's bottom, the line's lower part shows even when a
    /// tall diagram is taller than the view. The press is to the speech's
    /// left near that part's bottom, and the drag ends in the same line's
    /// right margin. Null when the line shows no margin.</summary>
    private static (Point Press, Point Release, Rectangle Box)? PointsBeside(FlaUI.Core.ITextRange speech, Rectangle page)
    {
        speech.ScrollIntoView(false);
        Rectangle[] boxes = [];
        _ = SpinWait.SpinUntil(() => (boxes = speech.GetBoundingRectangles()).Length > 0, TimeSpan.FromSeconds(5));
        if (boxes.Length == 0)
        {
            return null;
        }
        Rectangle box = boxes[0];
        int top = Math.Max(box.Top, page.Top);
        int bottom = Math.Min(box.Bottom, page.Bottom);
        if (bottom - top < 4 || box.Left - 12 <= page.Left + 2)
        {
            return null;
        }
        int y = bottom - Math.Min(10, (bottom - top) / 2);
        return (new Point(box.Left - 12, y), new Point(page.Right - 40, y), box);
    }
}
