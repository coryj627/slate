// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>W7-7 PR 4 (#1247, contract R-5; NVDA pass F4): arrow keys
    /// on a region's stop stay in the region. The empty editor stop keeps
    /// focus under every arrow (all four walked into a top-level menu),
    /// while the menu bar, closed to them, still wraps its own; Ctrl+R
    /// lands on the review's filter, not the bare rail, and Down there
    /// checks the next filter the way a Windows radio group does; Shift+F6
    /// into a Citations list with rows lands on a row; F6 to a rail that has
    /// lost its selection lands on a row; and the right-pane boundary into
    /// a leaf with no stop lands on the rail's row.</summary>
    [Fact]
    public void RegionStops_ArrowsStayInRegion()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-region-stops-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(vault);
        // Two open tasks, one due today, so All and Due today differ. The
        // review's windows are UTC days (TasksReviewViewModel.ToTaskFilter).
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        File.WriteAllText(
            Path.Combine(vault, "tasks.md"),
            $"# Tasks\n\n- [ ] due today task 📅 {today}\n- [ ] someday task\n");
        // The W4-5 journey's citation fixture: a style is required for
        // rows to render at all.
        File.WriteAllText(
            Path.Combine(vault, "library.bib"),
            "@article{knuth1984,\n  title = {Literate Programming},\n"
                + "  author = {Knuth, Donald E.},\n  year = {1984},\n"
                + "  journal = {The Computer Journal}\n}\n");
        File.WriteAllText(
            Path.Combine(vault, "cited.md"),
            "# Cited\n\nA citation [@knuth1984] and a ghost [@ghostkey].\n");
        File.Copy(CitationStyleFixture(), Path.Combine(vault, "ieee.csl"));
        File.WriteAllText(
            Path.Combine(vault, "slate.json"),
            "{\"citations\":{\"bibliography\":\"library.bib\",\"cite_style\":\"ieee\"}}");

        Process? process = null;
        try
        {
            process = StartRegionStopsApp(vault, logs, "region-stops");
            if (!HasInteractiveDesktop(process, "region-stops")) { return; }
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            AutomationElement rail = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));

            // 1. Zero tabs: the empty editor stop. Every arrow walked into a
            //    top-level menu (Down/Up Canvas, Left Graph, Right File).
            AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));
            ReassertForegroundForAChord(window);
            tree.Focus();
            AssertEventuallyFocused(tree, "The Files tree did not take focus.");
            PressKey(VirtualKeyShort.F6);
            AutomationElement contentPane = WaitForElement(window, "ContentPane", TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(contentPane, "F6 from Files with no tab did not land on the empty editor pane.");
            foreach (VirtualKeyShort arrow in new[] { VirtualKeyShort.DOWN, VirtualKeyShort.UP, VirtualKeyShort.LEFT, VirtualKeyShort.RIGHT })
            {
                PressKey(arrow);
                AssertFocusStays(automation, contentPane, $"{arrow} on the empty editor stop moved focus off it");
            }

            //    The bar closed to arrows still wraps its OWN: Left on File
            //    reaches the last menu, Right there comes back to File.
            AutomationElement statusBar = WaitForElement(window, "StatusBar", TimeSpan.FromSeconds(10));
            ReassertForegroundForAChord(window);
            statusBar.Focus();
            AssertEventuallyFocused(statusBar, "The status bar did not take focus.");
            PressKey(VirtualKeyShort.F6);
            AutomationElement fileMenu = WaitForElement(window, "FileMenu", TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(fileMenu, "F6 from the status bar did not land on the menu bar.");
            AutomationElement lastMenu = fileMenu.Parent
                .FindAllChildren(automation.ConditionFactory.ByControlType(ControlType.MenuItem))
                .Last();
            PressKey(VirtualKeyShort.LEFT);
            AssertEventuallyFocused(lastMenu, "Left on File did not wrap to the last menu.");
            PressKey(VirtualKeyShort.RIGHT);
            AssertEventuallyFocused(fileMenu, "Right on the last menu did not wrap to File.");
            PressKey(VirtualKeyShort.ESCAPE);
            AssertEventuallyFocused(statusBar, "Escape from the menu bar did not return to the status bar.");

            // 2. Ctrl+R lands on the review's filter, not on the bare rail;
            //    Down checks Due today (a Windows radio group), the review
            //    re-filters, and F6 moves on to the rail's row.
            ReassertForegroundForAChord(window);
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_R);
            AutomationElement all = WaitForElement(window, "PanelReviewFilterAll", TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(all, "Ctrl+R did not land on the review's All filter.");
            Assert.True(IsChosen(all), "the All filter must start checked");
            PressKey(VirtualKeyShort.DOWN);
            AutomationElement dueToday = WaitForElement(window, "PanelReviewFilterDueToday", TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(dueToday, "Down from the All filter did not move to Due today.");
            AssertChosen(dueToday, all, "Down on the review's filters moved focus without checking the radio it reached.");
            AutomationElement reviewList = WaitForElement(window, "PanelReviewList", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () => dueToday.Properties.Name.ValueOrDefault == "Due today, 1 task"
                        && ListItemNames(automation, reviewList) is [var only]
                        && only.Contains("due today task", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(10)),
                $"the review did not re-filter to Due today: the chip reads '{dueToday.Properties.Name.ValueOrDefault}', "
                + $"the rows are [{string.Join(" | ", ListItemNames(automation, reviewList))}]");
            PressKey(VirtualKeyShort.F6);
            AssertFocusedListItem(automation, rail, "Tasks Review", "F6 from the review's filter did not land on the rail's row.");

            // 3. Shift+F6 into a Citations list with rows lands on a row.
            AutomationElement cited = WaitForTreeItemStartingWith(tree, automation, "cited.md");
            cited.Patterns.SelectionItem.Pattern.Select();
            _ = WaitForEditor(window, automation, "cited.md editor", TimeSpan.FromSeconds(10));
            SelectRailLeaf(window, automation, "Citations");
            AutomationElement citations = WaitForElement(window, "PanelCitationsList", TimeSpan.FromSeconds(15));
            Assert.True(
                SpinWait.SpinUntil(() => ListItemNames(automation, citations).Length >= 2, TimeSpan.FromSeconds(20)),
                "the citations leaf never rendered its two rows");
            ReassertForegroundForAChord(window);
            statusBar.Focus();
            AssertEventuallyFocused(statusBar, "The status bar did not take focus.");
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6);
            AssertFocusedListItem(automation, rail, "Citations", "Shift+F6 from the status bar did not land on the rail's row.");
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6);
            AssertFocusedListItem(automation, citations, null, "Shift+F6 from the rail did not land on a Citations row.");

            // 4. F6 to a rail that has lost its selection lands on a row, not
            //    on the list. Deselecting through UIA moves the keys onto the
            //    rail, so they go back to the Citations row before the press.
            //    The rail's SelectedItem keeps naming the shown leaf (its
            //    TwoWay binding re-reads ActiveLeaf, which never takes null)
            //    while nothing is selected, so the row is that leaf's.
            AutomationElement citationRow = automation.FocusedElement();
            AutomationElement selectedLeaf = rail
                .FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.ListItem))
                .Single(item => IsChosen(item));
            selectedLeaf.Patterns.SelectionItem.Pattern.RemoveFromSelection();
            Assert.True(
                SpinWait.SpinUntil(
                    () => rail.Patterns.Selection.Pattern.Selection.ValueOrDefault is { Length: 0 },
                    TimeSpan.FromSeconds(10)),
                "the rail kept a selection");
            ReassertForegroundForAChord(window);
            citationRow.Focus();
            AssertFocusedListItem(automation, citations, null, "The Citations row did not take focus back.");
            Assert.True(
                rail.Patterns.Selection.Pattern.Selection.ValueOrDefault is { Length: 0 },
                "the rail regained a selection before the press");
            PressKey(VirtualKeyShort.F6);
            AssertFocusedListItem(automation, rail, "Citations", "F6 to a rail with no selection did not land on the shown leaf's row.");

            // 5. The right-pane boundary into a leaf with no stop of its own
            //    (Math shows the docked placeholder) lands on the rail's
            //    selected row. It was the bare rail.
            SelectRailLeaf(window, automation, "Math");
            AutomationElement editor = WaitForEditor(window, automation, "cited.md editor", TimeSpan.FromSeconds(10));
            ReassertForegroundForAChord(window);
            editor.Focus();
            AssertEventuallyFocused(editor, "The cited.md editor did not take focus.");
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.RIGHT);
            AssertFocusedListItem(automation, rail, "Math", "Ctrl+Alt+Right into a leaf without a stop did not land on the rail's row.");

            AssertAxeClean(process, "region-stops");
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>R-5's radio half on the canvas (codex design review of
    /// the W7-7 spec): Right on the checked Outline choice moves to Table,
    /// checks it, and the projection really switches — the reported failure
    /// was focus moving while the active projection did not. F6 then leaves
    /// the editor region for the right pane's first stop, as the ring
    /// says.</summary>
    [Fact]
    public void RegionStops_CanvasSwitcherArrowChecksAndShowsTheProjection()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-region-canvas-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(vault);
        File.Copy(Path.Combine(DemoVaultCanvasDirectory(), "sample.canvas"), Path.Combine(vault, "sample.canvas"));
        // The review's filter is the right pane's first stop — the ring's
        // next region after the editor.
        File.WriteAllText(Path.Combine(vault, "tasks.md"), "# Tasks\n\n- [ ] one open task\n");

        Process? process = null;
        try
        {
            process = StartRegionStopsApp(vault, logs, "region-canvas");
            if (!HasInteractiveDesktop(process, "region-canvas")) { return; }
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            WaitForVaultOpen(window);
            SelectRailLeaf(window, automation, "Tasks Review");
            _ = WaitForElement(window, "PanelReviewFilterAll", TimeSpan.FromSeconds(10));

            OpenCanvasFromTree(window, automation, "sample");
            _ = WaitForElement(window, "CanvasOutlineTree", TimeSpan.FromSeconds(20));
            AutomationElement outline = WaitForElement(window, "CanvasShowOutline", TimeSpan.FromSeconds(10));
            AutomationElement table = WaitForElement(window, "CanvasShowTable", TimeSpan.FromSeconds(10));
            Assert.True(IsChosen(outline), "the Outline projection must start checked");
            ReassertForegroundForAChord(window);
            outline.Focus();
            AssertEventuallyFocused(outline, "The Outline choice did not take focus.");

            PressKey(VirtualKeyShort.RIGHT);
            // (a) The arrow's destination is the checked choice.
            AssertEventuallyFocused(table, "Right on Outline did not move to Table.");
            AssertChosen(table, outline, "Right on the canvas switcher moved focus to Table without checking it.");
            // (b) The projection itself switched: the grid is in the tree
            // and the outline has left it (contract B11).
            _ = WaitForElement(window, "CanvasTableGrid", TimeSpan.FromSeconds(20));
            AssertElementDisappears(window, automation, "CanvasOutlineTree");
            // (c) The ring from the editor region: the right pane's first
            // stop, the review's checked filter.
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(
                WaitForElement(window, "PanelReviewFilterAll", TimeSpan.FromSeconds(10)),
                "F6 from the canvas switcher did not land on the right pane's first stop.");
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>R-5's radio half on the graph: Right on the checked Table
    /// choice checks Diagram and the diagram replaces the table — the
    /// user's switch, so the keys follow it into the renderer (graph
    /// contract Term M4). F6 then leaves the editor region for the right
    /// pane's first stop, as the ring says.</summary>
    [Fact]
    public void RegionStops_GraphSwitcherArrowChecksAndShowsTheView()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-region-graph-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Alpha.md"), "# Alpha\n\nLinks to [[Beta]].\n");
        File.WriteAllText(Path.Combine(vault, "Beta.md"), "# Beta\n\nLinks to [[Alpha]].\n");
        File.WriteAllText(Path.Combine(vault, "tasks.md"), "# Tasks\n\n- [ ] one open task\n");

        Process? process = null;
        try
        {
            process = StartRegionStopsApp(vault, logs, "region-graph");
            if (!HasInteractiveDesktop(process, "region-graph")) { return; }
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            WaitForVaultOpen(window);
            SelectRailLeaf(window, automation, "Tasks Review");
            _ = WaitForElement(window, "PanelReviewFilterAll", TimeSpan.FromSeconds(10));

            RunPaletteCommand(window, automation, "Open Graph");
            _ = WaitForElement(window, "GraphTableGrid", TimeSpan.FromSeconds(20));
            Assert.True(
                SpinWait.SpinUntil(() => FocusIsInside(automation, "GraphTableGrid"), TimeSpan.FromSeconds(10)),
                $"the open did not land focus in the grid; focus is {DescribeFocusedElement(automation)}");
            AutomationElement tableChoice = WaitForElement(window, "GraphMode.table", TimeSpan.FromSeconds(10));
            AutomationElement diagramChoice = WaitForElement(window, "GraphMode.diagram", TimeSpan.FromSeconds(10));
            Assert.True(IsChosen(tableChoice), "the Table view must start checked");
            ReassertForegroundForAChord(window);
            tableChoice.Focus();
            AssertEventuallyFocused(tableChoice, "The Table choice did not take focus.");

            PressKey(VirtualKeyShort.RIGHT);
            // (a) The arrow's destination is the checked choice.
            AssertChosen(diagramChoice, tableChoice, "Right on the graph switcher moved focus to Diagram without checking it.");
            // (b) The view itself switched: the diagram replaces the table,
            // and the user's switch hands the keys to the renderer (Term M4).
            _ = WaitForElement(window, "GraphDiagram", TimeSpan.FromSeconds(20));
            AssertElementDisappears(window, automation, "GraphTableGrid");
            Assert.True(
                SpinWait.SpinUntil(() => FocusIsInside(automation, "GraphDiagram"), TimeSpan.FromSeconds(10)),
                $"the switch did not land the keys on the renderer; focus is {DescribeFocusedElement(automation)}");
            // (c) The ring from the editor region: the right pane's first
            // stop, the review's checked filter.
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(
                WaitForElement(window, "PanelReviewFilterAll", TimeSpan.FromSeconds(10)),
                "F6 from the graph view did not land on the right pane's first stop.");
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>Launches the app for a region-stops journey. These journeys
    /// are bare keystrokes on a desktop other runs share, so any modifier a
    /// killed run left held is released first, before the app exists — a
    /// held Alt or Shift turns each arrow into a chord the radio groups
    /// rightly ignore — and each keyboard step re-asserts the foreground
    /// (<see cref="ReassertForegroundForAChord"/>), the suite's rule before
    /// a chord.</summary>
    private static Process StartRegionStopsApp(string vault, string logs, string instance)
    {
        foreach (VirtualKeyShort modifier in new[]
                 {
                     VirtualKeyShort.SHIFT,
                     VirtualKeyShort.CONTROL,
                     VirtualKeyShort.ALT,
                 })
        {
            Keyboard.Release(modifier);
        }

        var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
        start.ArgumentList.Add(vault);
        start.Environment["SLATE_CENSUS_INSTANCE_ID"] = instance + "-" + Guid.NewGuid().ToString("N");
        start.Environment["SLATE_LOG_DIR"] = logs;
        return Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
    }

    /// <summary>After a key that must not move focus. Directional
    /// navigation runs inside the key's own input processing, which
    /// <see cref="PressKey"/> already waits out; the settle absorbs the UIA
    /// focus event's delivery before the focused element is read.</summary>
    private static void AssertFocusStays(UIA3Automation automation, AutomationElement element, string message)
    {
        Thread.Sleep(300);
        string id = element.Properties.AutomationId.ValueOrDefault ?? string.Empty;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        return automation.FocusedElement()?.Properties.AutomationId.ValueOrDefault == id;
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception))
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(2)),
            $"{message}: focus is on {DescribeFocusedElement(automation)}. {FocusDiagnosis()}");
    }

    /// <summary>The focused element is a ROW of <paramref name="list"/>
    /// (named <paramref name="name"/> when given) — never the list
    /// itself, the container landing R-5 retires.</summary>
    private static void AssertFocusedListItem(UIA3Automation automation, AutomationElement list, string? name, string message)
    {
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        return automation.FocusedElement() is { } focused
                            && focused.Properties.ControlType.ValueOrDefault == ControlType.ListItem
                            && IsDescendantOf(focused, list)
                            && (name is null || focused.Properties.Name.ValueOrDefault == name);
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception))
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(10)),
            $"{message} Focus is on {DescribeFocusedElement(automation)}. {FocusDiagnosis()}");
    }

    /// <summary>The mutually exclusive choice moved: <paramref
    /// name="chosen"/> is checked and <paramref name="previous"/> is
    /// not.</summary>
    private static void AssertChosen(AutomationElement chosen, AutomationElement previous, string message) =>
        Assert.True(
            SpinWait.SpinUntil(() => IsChosen(chosen) && !IsChosen(previous), TimeSpan.FromSeconds(5)),
            $"{message} {chosen.Properties.AutomationId.ValueOrDefault} checked={IsChosen(chosen)}, "
            + $"{previous.Properties.AutomationId.ValueOrDefault} checked={IsChosen(previous)}.");

    private static bool IsChosen(AutomationElement element)
    {
        try
        {
            return element.Patterns.SelectionItem.PatternOrDefault?.IsSelected.ValueOrDefault == true;
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
            return false;
        }
    }

    private static string[] ListItemNames(UIA3Automation automation, AutomationElement list)
    {
        try
        {
            return list
                .FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.ListItem))
                .Select(item => item.Properties.Name.ValueOrDefault ?? string.Empty)
                .ToArray();
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
            return [];
        }
    }

    /// <summary>Shows a right-pane leaf by selecting its rail row through
    /// the SelectionItem pattern — the route that moves no focus.</summary>
    private static void SelectRailLeaf(Window window, UIA3Automation automation, string title)
    {
        AutomationElement rail = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));
        AutomationElement? entry = null;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    entry = rail
                        .FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.ListItem))
                        .FirstOrDefault(item => (item.Properties.Name.ValueOrDefault ?? string.Empty) == title);
                    return entry is not null;
                },
                TimeSpan.FromSeconds(15)),
            $"No rail entry named {title}.");
        entry!.Patterns.SelectionItem.Pattern.Select();
        Assert.True(
            SpinWait.SpinUntil(() => IsChosen(entry), TimeSpan.FromSeconds(10)),
            $"The rail entry {title} did not take the selection.");
    }
}
