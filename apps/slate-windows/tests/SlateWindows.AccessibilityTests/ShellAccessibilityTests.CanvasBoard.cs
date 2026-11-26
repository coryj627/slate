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

    /// <summary>
    /// Plan 47 CB-36 (#1721, the owner's answer 3), with the real pointer and no seat: the pointer
    /// on a truncated card shows its title; on a cut-off chip drawn over that card it shows the
    /// connection's full label instead; and the pointer moved from the chip down onto the tooltip
    /// keeps it open. Before CB-36 the board drew no chips, and the chip's point showed the card's
    /// title.
    /// </summary>
    [Fact]
    [Trait("gate", "W-C")]
    public void CanvasBoard_APointerOnAChipOverACardShowsTheChipsLabel()
    {
        const string LongTitle = "A card title far too long to fit in two short lines of this narrow card at all";
        const string LongLabel = "a connection label far too long to fit in one chip of two hundred units";
        string testRoot = Path.Combine(Path.GetTempPath(), $"slate-canvas-chip-hover-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Canvas Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        // One truncated card with a self-loop: the loop's chip sits on the card's centre.
        File.WriteAllText(
            Path.Combine(vaultRoot, "hover.canvas"),
            "{\"nodes\":[{\"id\":\"c\",\"type\":\"text\",\"text\":\"" + LongTitle
            + "\",\"x\":40,\"y\":40,\"width\":260,\"height\":90}],\"edges\":["
            + "{\"id\":\"loop\",\"fromNode\":\"c\",\"toNode\":\"c\",\"toEnd\":\"arrow\",\"label\":\"" + LongLabel + "\"}]}");

        Process? process = null;
        try
        {
            // FlaUI's Mouse makes this process DPI-aware on first use: touch it before reading any
            // rectangle, so rectangles and pointer moves share one coordinate space.
            _ = Mouse.Position;
            var startInfo = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(startInfo);
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] = $"slate-canvas-chip-hover-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo) ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");
            if (!HasInteractiveDesktop(process, "Canvas chip hover"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process, automation, Path.Combine(logDirectory, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            OpenCanvasFromTree(window, automation, "hover");
            WaitForElement(window, "CanvasShowVisual", TimeSpan.FromSeconds(20)).AsRadioButton().IsChecked = true;
            AutomationElement board = WaitForElement(window, "CanvasVisualBoard", TimeSpan.FromSeconds(20));
            AutomationElement loop = Retry.WhileNull(
                () => board.FindAllChildren(finder => finder.ByControlType(ControlType.Custom)).FirstOrDefault(),
                TimeSpan.FromSeconds(20)).Result
                ?? throw new Xunit.Sdk.XunitException("premise: the self-loop is not one of the board's children.");
            AssertAxeClean(process, "canvas-board-chips");

            // No seat: a seated truncated card shows its title by the keyboard trigger, which
            // outranks the pointer (contract 34 D11).
            AutomationElement card = board.FindAllChildren(finder => finder.ByControlType(ControlType.Button)).FirstOrDefault()
                ?? throw new Xunit.Sdk.XunitException("premise: the card is not one of the board's children.");
            if (card.Patterns.SelectionItem.Pattern.IsSelected.Value)
            {
                card.Patterns.SelectionItem.Pattern.RemoveFromSelection();
            }
            Assert.True(
                Retry.WhileFalse(() => board.Patterns.Selection.Pattern.Selection.Value.Length == 0, TimeSpan.FromSeconds(10)).Success,
                "premise: the board kept its seat.");

            // The card is truncated: the pointer on it, away from the chip, shows its title.
            System.Drawing.Rectangle cardRect = card.Properties.BoundingRectangle.Value;
            System.Drawing.Rectangle chip = loop.Properties.BoundingRectangle.Value;
            window.SetForeground();
            Mouse.MoveTo(new System.Drawing.Point(cardRect.X + 6, cardRect.Y + 6));
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(400));
            Assert.True(
                Retry.WhileFalse(
                    () => TooltipText(automation, process.Id, LongTitle)?.Properties.Name.ValueOrDefault == LongTitle,
                    TimeSpan.FromSeconds(5)).Success,
                "premise: the pointer on the card away from the chip showed no title, so the card is not truncated.");

            Mouse.MoveTo(new System.Drawing.Point(chip.X + (chip.Width / 2), chip.Y + (chip.Height / 2)));
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(400));
            string shown = "(nothing)";
            AutomationElement? tip = Retry.WhileNull(
                () =>
                {
                    AutomationElement? text = TooltipText(automation, process.Id, LongLabel, LongTitle);
                    shown = text?.Properties.Name.ValueOrDefault ?? "(nothing)";
                    return text?.Properties.Name.ValueOrDefault == LongLabel ? text : null;
                },
                TimeSpan.FromSeconds(5)).Result;
            Assert.True(tip is not null, $"the pointer on the chip over the card showed {shown}, not the connection's label (#1721).");

            System.Drawing.Rectangle tipRect = tip.Properties.BoundingRectangle.Value;
            for (int y = chip.Y + (chip.Height / 2); y <= tipRect.Y + (tipRect.Height / 2); y += 3)
            {
                Mouse.MoveTo(new System.Drawing.Point(chip.X + (chip.Width / 2), y));
            }
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(600));
            // The tooltip lies below the chip: had it closed, the pointer would rest on the card or
            // on bare board, which show the title or nothing, so a read that keeps finding the
            // label is the tooltip that stayed.
            Assert.True(
                Retry.WhileFalse(
                    () => TooltipText(automation, process.Id, LongLabel, LongTitle)?.Properties.Name.ValueOrDefault == LongLabel,
                    TimeSpan.FromSeconds(3)).Success,
                "the chip's tooltip closed as the pointer moved onto it (1.4.13: hoverable).");
        }
        catch (Exception)
        {
            PreserveEvidence(nameof(CanvasBoard_APointerOnAChipOverACardShowsTheChipsLabel), logDirectory);
            throw;
        }
        finally
        {
            Mouse.MoveTo(new System.Drawing.Point(5, 5));
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

    /// <summary>The board's tooltip text, if its popup shows one of the given texts. UI Automation
    /// lists an owned popup window under its owner, so the search runs under the process's
    /// windows.</summary>
    private static AutomationElement? TooltipText(UIA3Automation automation, int processId, params string[] texts)
    {
        try
        {
            foreach (AutomationElement top in automation.GetDesktop().FindAllChildren(automation.ConditionFactory.ByProcessId(processId)))
            {
                AutomationElement? hit = top.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Text))
                    .FirstOrDefault(element => texts.Contains(element.Properties.Name.ValueOrDefault));
                if (hit is not null)
                {
                    return hit;
                }
            }
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }
        return null;
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
