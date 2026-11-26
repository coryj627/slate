// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>
    /// Plan 47 CB-36 (#1722, contract 34 D3): a UI Automation client's FIRST name search on the
    /// visual board's ItemContainer pattern returns the off-window card or labelled edge it names,
    /// as the element the search realized. Before CB-36 the first call answered nothing while it
    /// still panned the item into view, and only a later, identical call returned it; a client
    /// that takes nothing as "not found" (an inspection tool or a script; whether a screen reader
    /// does is not checked here) missed the item. The search runs out of process, through UI
    /// Automation's core, as a reader's does. A scan follows, with the item names census.
    /// </summary>
    [Fact]
    [Trait("gate", "W-C")]
    public void CanvasBoard_TheFirstNameSearchReturnsAnOffWindowItem()
    {
        const string FarCard = "Far One";
        const string FarEdge = "Text card \"Alpha\" connects to Text card \"Beta\", labelled \"cites\"";
        string testRoot = Path.Combine(Path.GetTempPath(), $"slate-canvas-first-search-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Canvas Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        // Home in view at the board's opening zoom; "Far One" and the labelled edge's two cards
        // tens of thousands of units away, so no window the board opens with reaches them. Alpha
        // sits over Beta, so the edge's union (200 by 260) fits a small board at zoom 1.
        File.WriteAllText(
            Path.Combine(vaultRoot, "far.canvas"),
            "{\"nodes\":["
            + "{\"id\":\"home\",\"type\":\"text\",\"text\":\"Home\",\"x\":40,\"y\":40,\"width\":200,\"height\":100},"
            + "{\"id\":\"far\",\"type\":\"text\",\"text\":\"Far One\",\"x\":20000,\"y\":40,\"width\":200,\"height\":100},"
            + "{\"id\":\"a\",\"type\":\"text\",\"text\":\"Alpha\",\"x\":30000,\"y\":40,\"width\":200,\"height\":100},"
            + "{\"id\":\"b\",\"type\":\"text\",\"text\":\"Beta\",\"x\":30000,\"y\":200,\"width\":200,\"height\":100}"
            + "],\"edges\":["
            + "{\"id\":\"e1\",\"fromNode\":\"a\",\"toNode\":\"b\",\"toEnd\":\"arrow\",\"label\":\"cites\"}"
            + "]}");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(startInfo);
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] = $"slate-canvas-first-search-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo) ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");
            if (!HasInteractiveDesktop(process, "Canvas first search"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process, automation, Path.Combine(logDirectory, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            window.Focus();

            OpenCanvasFromTree(window, automation, "far");
            AutomationElement visualChoice = WaitForElement(window, "CanvasShowVisual", TimeSpan.FromSeconds(20));
            visualChoice.AsRadioButton().IsChecked = true;
            AutomationElement board = WaitForElement(window, "CanvasVisualBoard", TimeSpan.FromSeconds(20));
            Assert.True(
                Retry.WhileFalse(
                    () => BoardChildNames(board).Contains("Home"),
                    TimeSpan.FromSeconds(20)).Success,
                "premise: the board never showed its \"Home\" card.");
            var container = board.Patterns.ItemContainer;
            Assert.True(container.IsSupported, "premise: the board has no ItemContainer pattern (contract 34 D3).");

            // Every search is made and judged before the journey fails, so a red run says what each
            // first call answered.
            var failures = new List<string>();
            foreach (string name in new[] { FarCard, FarEdge })
            {
                Assert.False(
                    BoardChildNames(board).Contains(name),
                    $"premise: \"{name}\" is already one of the board's children, so a search for it proves nothing.");
                AutomationElement? found = container.Pattern.FindItemByProperty(
                    null, automation.PropertyLibrary.Element.Name, name);
                if (found is null)
                {
                    // The issue's control: say whether the same search, retried, finds it.
                    AutomationElement? again = Retry.WhileNull(
                        () => container.Pattern.FindItemByProperty(null, automation.PropertyLibrary.Element.Name, name),
                        TimeSpan.FromSeconds(10)).Result;
                    failures.Add(
                        $"the first name search for \"{name}\" returned nothing; the same search, retried for up to 10 s, returned "
                        + (again is null ? "nothing too" : "it") + " (#1722)");
                    continue;
                }

                if (found.Properties.Name.ValueOrDefault != name)
                {
                    failures.Add($"the first name search for \"{name}\" returned \"{found.Properties.Name.ValueOrDefault}\"");
                    continue;
                }

                // The element the client holds is the board's own item: once the install lands it is the
                // board's child of that name, on screen inside the board. Its parent is read after the
                // install may have landed, which makes the board its parent either way; the hosted
                // TheItemTheDoorHandsOutIsUnderTheBoardBeforeItsInstall shows the door connects it first.
                AutomationElement? parent = automation.TreeWalkerFactory.GetRawViewWalker().GetParent(found);
                if (parent?.Properties.AutomationId.ValueOrDefault != "CanvasVisualBoard")
                {
                    failures.Add(
                        $"the element the search for \"{name}\" returned has the parent "
                        + $"\"{parent?.Properties.AutomationId.ValueOrDefault ?? "(none)"}\", not the board");
                    continue;
                }

                bool promoted = Retry.WhileFalse(
                    () => board.FindAllChildren().Any(child => child.Properties.Name.ValueOrDefault == name && child.Equals(found))
                        && !found.Properties.IsOffscreen.ValueOrDefault
                        && IsOnTheBoard(board, found.Properties.BoundingRectangle.ValueOrDefault),
                    TimeSpan.FromSeconds(10)).Success;
                if (!promoted)
                {
                    failures.Add(
                        $"the element the search for \"{name}\" returned never became the board's child of that name on "
                        + $"screen inside the board (offscreen {found.Properties.IsOffscreen.ValueOrDefault}, "
                        + $"at {found.Properties.BoundingRectangle.ValueOrDefault}; contract 34 D3)");
                }
            }

            Assert.True(failures.Count == 0, string.Join("; ", failures) + ".");

            // The board after both realizations, scanned with its new children.
            AssertAxeClean(process, "canvas-board-first-search");
        }
        catch (Exception)
        {
            PreserveEvidence(nameof(CanvasBoard_TheFirstNameSearchReturnsAnOffWindowItem), logDirectory);
            throw;
        }
        finally
        {
            process?.Kill(entireProcessTree: true);
            process?.Dispose();
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>The names of the board's children, read again from the board each time; an empty
    /// set while a transient fault stands.</summary>
    private static HashSet<string> BoardChildNames(AutomationElement board)
    {
        try
        {
            return [.. board.FindAllChildren().Select(child => child.Properties.Name.ValueOrDefault ?? string.Empty)];
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return [];
        }
    }
}
