// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    private const string ReadingFocusAlphaText = "The paragraph the reading focus journey reads.";
    private const string ReadingFocusBetaText = "The second note's paragraph links to";

    /// <summary>W7-7 PR 8 (#1253, contract R-10; NVDA record F10): in reading
    /// mode the reading surface is the editor stop, and every route that lands
    /// the editor lands it, reading the note rather than its loading
    /// placeholder — Ctrl+Shift+E in, F6 from Files through the tab bar,
    /// Shift+F6 back from the right pane's content stop (every locked W7-6
    /// stop visited and asserted), a Quick Open commit and an Outgoing-links
    /// activation into the reading tab, and a palette-invoked toggle — and
    /// Ctrl+Shift+E back lands the editor.</summary>
    [Fact]
    public void ReadingView_IsTheEditorStop()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-reading-focus-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "alpha.md"), "# Alpha\n\n" + ReadingFocusAlphaText + "\n");
        File.WriteAllText(Path.Combine(vault, "beta.md"), "# Beta\n\n" + ReadingFocusBetaText + " [[alpha]].\n");

        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            start.ArgumentList.Add(vault);
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "reading-focus-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "reading-focus")) { return; }
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));

            // Open the note and put the reader in its editor, where the field
            // pass pressed Ctrl+Shift+E.
            AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));
            WaitForTreeItemStartingWith(tree, automation, "alpha.md").Patterns.SelectionItem.Pattern.Select();
            AutomationElement editor = WaitForEditor(window, automation, "alpha.md editor", TimeSpan.FromSeconds(10));
            editor.Focus();
            AssertEventuallyFocused(editor, "The editor did not take focus.");

            // Ctrl+Shift+E: the surface, seated at the top of the note (the
            // field pass heard "Workspace tabs, tab control").
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_E);
            AssertReadingSurfaceLanding(window, automation, "Ctrl+Shift+E into reading", ReadingFocusAlphaText, caretOnHeading: "Alpha");

            // F6 from Files: the tab bar, then the reading surface (the field
            // pass went tab bar → right pane).
            FocusFilesTreeSelection(window, automation, "alpha.md", "The Files tree did not take focus.");
            PressKey(VirtualKeyShort.F6);
            AutomationElement tabs = WaitForElement(window, "WorkspaceTabs", TimeSpan.FromSeconds(10));
            AutomationElement tab = FindDescendantWithin(tabs, automation.ConditionFactory.ByControlType(ControlType.TabItem), TimeSpan.FromSeconds(10))
                ?? throw new Xunit.Sdk.XunitException("No tab item.");
            AssertEventuallyFocused(tab, "F6 from Files did not land on the tab bar.");
            PressKey(VirtualKeyShort.F6);
            AssertReadingSurfaceLanding(window, automation, "F6 from the tab bar", ReadingFocusAlphaText);

            // F6 on to the right pane's content stop (the Outline leaf's first
            // row: the note has a heading), then Shift+F6 from there back to
            // the reading surface (the field pass went right pane → tab bar).
            AutomationElement rail = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(10));
            PressKey(VirtualKeyShort.F6);
            AssertRightPaneContentStop(window, rail, automation, "F6 from the reading surface did not land on the right pane's content stop.");
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6);
            AssertReadingSurfaceLanding(window, automation, "Shift+F6 from the right pane's content stop", ReadingFocusAlphaText);

            // A Quick Open commit into the reading tab (the field pass landed
            // on the tab item).
            FocusFilesTreeSelection(window, automation, "alpha.md", "The Files tree did not take focus back.");
            QuickOpen(window, automation, "alpha", VirtualKeyShort.RETURN);
            AssertReadingSurfaceLanding(window, automation, "Quick Open into the reading tab", ReadingFocusAlphaText);

            // An Outgoing-links activation into the reading tab, from a second
            // note open beside it (the field pass landed on the tab item).
            QuickOpen(window, automation, "beta", VirtualKeyShort.CONTROL, VirtualKeyShort.RETURN);
            _ = WaitForEditor(window, automation, "beta.md editor", TimeSpan.FromSeconds(10));
            SelectRailLeaf(rail, automation, "Outgoing links");
            AutomationElement outgoing = WaitForElement(window, "PanelOutgoingLinksList", TimeSpan.FromSeconds(10));
            AutomationElement link = FindDescendantWithin(
                outgoing,
                automation.ConditionFactory.ByControlType(ControlType.ListItem).And(automation.ConditionFactory.ByName("Link to alpha.md")),
                TimeSpan.FromSeconds(15))
                ?? throw new Xunit.Sdk.XunitException("The Outgoing links leaf never listed alpha.md.");
            link.Patterns.SelectionItem.Pattern.Select();
            link.Focus();
            PressKey(VirtualKeyShort.RETURN);
            AssertReadingSurfaceLanding(window, automation, "Outgoing links into the reading tab", ReadingFocusAlphaText);

            // A palette-invoked toggle: the landing is requested while the
            // closing palette's search box still holds focus.
            QuickOpen(window, automation, "beta", VirtualKeyShort.RETURN);
            AssertEventuallyFocused(WaitForEditor(window, automation, "beta.md editor", TimeSpan.FromSeconds(10)), "Quick Open did not land beta's editor.");
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
            AutomationElement palette = WaitForElement(window, "CommandPaletteSearch", TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(palette, "The palette did not take focus.");
            palette.Patterns.Value.Pattern.SetValue("Toggle Reading Mode");
            AutomationElement paletteResults = WaitForElement(window, "CommandPaletteResults", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return paletteResults.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.ListItem))
                                .FirstOrDefault(row => row.Patterns.SelectionItem.PatternOrDefault?.IsSelected.ValueOrDefault == true)
                                ?.Properties.Name.ValueOrDefault
                                ?.StartsWith("Toggle Reading Mode", StringComparison.Ordinal) == true;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception)) { return false; }
                    },
                    TimeSpan.FromSeconds(10)),
                "The palette did not select Toggle Reading Mode.");
            PressKey(VirtualKeyShort.RETURN);
            AssertElementDisappears(window, automation, "CommandPaletteSearch");
            AssertReadingSurfaceLanding(window, automation, "the palette's Toggle Reading Mode", ReadingFocusBetaText, caretOnHeading: "Beta");

            // Ctrl+Shift+E back: the editor, not wherever WPF's recovery puts
            // focus when the focused surface collapses.
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_E);
            AssertEventuallyFocused(WaitForEditor(window, automation, "beta.md editor", TimeSpan.FromSeconds(10)), "Ctrl+Shift+E back did not land the editor.");
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>The reading surface holds keyboard focus and what its Text
    /// pattern reads is the note, never the loading placeholder: R-10 lands
    /// the surface only once its content is there. Re-resolved every spin —
    /// a re-projection can replace the peer under a stale handle.</summary>
    private static void AssertReadingSurfaceLanding(
        Window window, UIA3Automation automation, string route, string noteText, string? caretOnHeading = null)
    {
        AutomationElement? surface = null;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        surface = window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("ReadingSurface"));
                        return surface?.Properties.HasKeyboardFocus.ValueOrDefault == true;
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception)) { return false; }
                },
                TimeSpan.FromSeconds(10)),
            $"{route} did not land the reading surface; focus is {DescribeFocusedElement(automation)}. {FocusDiagnosis()}");
        var text = surface!.Patterns.Text.Pattern;
        string read = text.DocumentRange.GetText(-1);
        Assert.True(
            read.Contains(noteText, StringComparison.Ordinal)
                && !read.Contains("Loading reading view", StringComparison.Ordinal),
            $"{route}: the reading surface read '{read}'");
        if (caretOnHeading is not null)
        {
            var caret = text.GetSelection().Single().Clone();
            caret.ExpandToEnclosingUnit(TextUnit.Line);
            string line = caret.GetText(-1);
            Assert.True(line.Contains(caretOnHeading, StringComparison.Ordinal), $"{route}: the caret line reads '{line}'");
        }
    }

    /// <summary>Focus is on the right pane's CONTENT stop: inside the pane,
    /// not on its rail (the shown leaf has a stop, so the rail is the next
    /// region, never this one).</summary>
    private static void AssertRightPaneContentStop(
        Window window, AutomationElement rail, UIA3Automation automation, string message)
    {
        AutomationElement pane = WaitForElement(window, "InspectorPane", TimeSpan.FromSeconds(10));
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        return automation.FocusedElement() is { } focused
                            && IsDescendantOf(focused, pane)
                            && !IsDescendantOf(focused, rail);
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception)) { return false; }
                },
                TimeSpan.FromSeconds(10)),
            $"{message} Focus is {DescribeFocusedElement(automation)}. {FocusDiagnosis()}");
    }

    /// <summary>Ctrl+O, the query, then the commit chord (Enter, or
    /// Ctrl+Enter for a new tab) once the queried note is the SELECTED row —
    /// the list opens on the recent files, and the query's ranking publishes
    /// asynchronously behind them.</summary>
    private static void QuickOpen(Window window, UIA3Automation automation, string query, params VirtualKeyShort[] commit)
    {
        PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_O);
        AutomationElement search = WaitForElement(window, "QuickSwitcherSearch", TimeSpan.FromSeconds(10));
        AssertEventuallyFocused(search, "Quick Open did not take focus.");
        search.Patterns.Value.Pattern.SetValue(query);
        AutomationElement results = WaitForElement(window, "QuickSwitcherResults", TimeSpan.FromSeconds(10));
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        return results.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.ListItem))
                            .FirstOrDefault(row => row.Patterns.SelectionItem.PatternOrDefault?.IsSelected.ValueOrDefault == true)
                            ?.Properties.Name.ValueOrDefault
                            ?.StartsWith(query, StringComparison.OrdinalIgnoreCase) == true;
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception)) { return false; }
                },
                TimeSpan.FromSeconds(10)),
            $"Quick Open did not select '{query}'.");
        if (commit.Length == 1)
        {
            PressKey(commit[0]);
        }
        else
        {
            PressChord(commit[0], commit[1]);
        }
        AssertElementDisappears(window, automation, "QuickSwitcherSearch");
    }

    /// <summary>Select a right-pane leaf by its rail title — retried, because
    /// the rail's rows materialize lazily.</summary>
    private static void SelectRailLeaf(AutomationElement rail, UIA3Automation automation, string title)
    {
        AutomationElement? entry = null;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        entry = rail.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.ListItem))
                            .FirstOrDefault(item => (item.Properties.Name.ValueOrDefault ?? string.Empty) == title);
                        return entry is not null;
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception)) { return false; }
                },
                TimeSpan.FromSeconds(15)),
            $"No rail entry named {title}.");
        entry!.Patterns.SelectionItem.Pattern.Select();
    }
}
