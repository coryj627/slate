// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>
    /// Plan 47 CB-36 (#1721, contract 34 D5's accepted risk): the covering state. A long,
    /// diagonal labelled edge, the board zoomed in on a card in the middle of it, so the edge's
    /// two cards lie far outside the board while the edge itself is windowed. axe scans the board
    /// in that state. Before CB-36's chips the edge's element reported its two cards' union as its
    /// bounds, a rectangle that covered the whole board.
    /// </summary>
    [Fact]
    [Trait("gate", "W-C")]
    public void CanvasBoard_ALongLabelledEdgeZoomedInLeavesTheBoardAxeClean()
    {
        const string Spans = "Text card \"Start\" connects to Text card \"Finish\", labelled \"spans\"";
        string testRoot = Path.Combine(Path.GetTempPath(), $"slate-canvas-covering-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Canvas Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        // "Start" and "Finish" 3,000 by 2,400 units apart, joined by the labelled edge; "Middle"
        // inside the edge's union and off its line, unconnected.
        File.WriteAllText(
            Path.Combine(vaultRoot, "long.canvas"),
            "{\"nodes\":["
            + "{\"id\":\"s\",\"type\":\"text\",\"text\":\"Start\",\"x\":0,\"y\":0,\"width\":200,\"height\":100},"
            + "{\"id\":\"m\",\"type\":\"text\",\"text\":\"Middle\",\"x\":1500,\"y\":300,\"width\":200,\"height\":100},"
            + "{\"id\":\"n\",\"type\":\"text\",\"text\":\"Beside\",\"x\":1900,\"y\":300,\"width\":200,\"height\":100},"
            + "{\"id\":\"f\",\"type\":\"text\",\"text\":\"Finish\",\"x\":3000,\"y\":2400,\"width\":200,\"height\":100}"
            + "],\"edges\":["
            + "{\"id\":\"e1\",\"fromNode\":\"s\",\"toNode\":\"f\",\"toEnd\":\"arrow\",\"label\":\"spans\"}"
            + "]}");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(startInfo);
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] = $"slate-canvas-covering-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo) ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");
            if (!HasInteractiveDesktop(process, "Canvas covering edge"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process, automation, Path.Combine(logDirectory, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            window.Focus();

            OpenCanvasFromTree(window, automation, "long");
            AutomationElement visualChoice = WaitForElement(window, "CanvasShowVisual", TimeSpan.FromSeconds(20));
            visualChoice.AsRadioButton().IsChecked = true;
            AutomationElement board = WaitForElement(window, "CanvasVisualBoard", TimeSpan.FromSeconds(20));

            // Seat "Start" through its peer, then walk Down to "Middle" (the reading-order move,
            // contract 34 D15) and zoom to the seat (Shift+2).
            AutomationElement start = Retry.WhileNull(
                () => board.FindAllChildren(finder => finder.ByControlType(ControlType.Button))
                    .FirstOrDefault(card => card.Properties.Name.Value == "Start"),
                TimeSpan.FromSeconds(20)).Result
                ?? throw new Xunit.Sdk.XunitException("premise: the \"Start\" card peer never materialized.");
            start.Patterns.SelectionItem.Pattern.Select();
            Assert.True(
                Retry.WhileFalse(() => start.Patterns.SelectionItem.Pattern.IsSelected.Value, TimeSpan.FromSeconds(10)).Success,
                "premise: \"Start\" never took the seat through its peer.");
            ReassertForegroundForAChord(window);
            board.Focus();
            AssertEventuallyFocused(board, "premise: the visual board never took the keys");
            PressKey(VirtualKeyShort.DOWN);
            AssertTheSeatIsOnTheBoard(board, "Middle", "premise: DOWN from \"Start\"");
            AutomationElement middle = SeatedCard(board)
                ?? throw new Xunit.Sdk.XunitException("premise: the board reports no seat after Down.");
            System.Drawing.Rectangle beforeZoom = CardRectangle(middle);
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_2);
            Assert.True(
                SpinWait.SpinUntil(() => CardRectangle(middle) != beforeZoom, TimeSpan.FromSeconds(10)),
                "premise: Shift+2 never zoomed the board to \"Middle\".");

            // The covering state: the edge is one of the board's children, and its two cards are not.
            AutomationElement edge = Retry.WhileNull(
                () => board.FindAllChildren(finder => finder.ByControlType(ControlType.Custom))
                    .FirstOrDefault(child => child.Properties.Name.Value == Spans),
                TimeSpan.FromSeconds(10)).Result
                ?? throw new Xunit.Sdk.XunitException($"premise: the board has no \"{Spans}\" child after the zoom.");
            HashSet<string> children = BoardChildNames(board);
            Assert.False(
                children.Contains("Start") || children.Contains("Finish"),
                $"premise: an endpoint card is still one of the board's children ({string.Join(", ", children)}), "
                + "so the zoom did not reach the covering state.");
            System.Drawing.Rectangle boardBounds = board.Properties.BoundingRectangle.Value;
            System.Drawing.Rectangle edgeBounds = edge.Properties.BoundingRectangle.Value;
            // "Beside" is windowed (within one view of the board) and lies off the board: a child whose
            // rectangle is outside its container's, the state an edge's drawn chip can also be in.
            AutomationElement beside = board.FindAllChildren(finder => finder.ByControlType(ControlType.Button))
                .FirstOrDefault(card => card.Properties.Name.Value == "Beside")
                ?? throw new Xunit.Sdk.XunitException(
                    $"premise: \"Beside\" is not one of the board's children ({string.Join(", ", children)}).");
            System.Drawing.Rectangle besideBounds = beside.Properties.BoundingRectangle.Value;
            WriteCanvasBoardEvidence(
                "canvas-board-covering",
                $"board {boardBounds}; edge {edgeBounds}; beside {besideBounds}; children [{string.Join(", ", children)}]");
            Assert.False(
                boardBounds.IntersectsWith(besideBounds),
                $"premise: \"Beside\" at {besideBounds} is not off the board at {boardBounds}.");

            AssertAxeClean(process, "canvas-board-covering");
        }
        catch (Exception)
        {
            PreserveEvidence(nameof(CanvasBoard_ALongLabelledEdgeZoomedInLeavesTheBoardAxeClean), logDirectory);
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

    /// <summary>One line of the journey's geometry in the test's output, so a failing scan says
    /// what the board and the edge measured.</summary>
    private static void WriteCanvasBoardEvidence(string surface, string line) =>
        TestContext.Current.TestOutputHelper?.WriteLine($"{surface}: {line}");
}
