// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>W7-6 (#1240) spec §5: F6 walks the ring forward from the
    /// Files tree and Shift+F6 walks it back; hiding the right pane drops
    /// both right stops; a zero-tab vault stops on the empty pane.</summary>
    [Fact]
    public void ShellRegions_F6CyclesForwardAndShiftF6Back()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-shell-regions-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "alpha.md"), "# Alpha\n\nBody.\n");

        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            start.ArgumentList.Add(vault);
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "shell-regions-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "shell-regions")) { return; }
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));

            // Zero tabs: Files → empty pane → leaf/rail → status bar → menu bar → Files.
            AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));
            tree.Focus();
            AssertEventuallyFocused(tree, "The Files tree did not take focus.");
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(WaitForElement(window, "ContentPane", TimeSpan.FromSeconds(10)), "F6 from Files with no tab did not land on the empty editor pane.");
            PressKey(VirtualKeyShort.F6);
            AssertRightPaneStop(window, automation, "F6 from the empty pane did not land in the right pane.");
            PressKey(VirtualKeyShort.F6);
            AdvancePastTheRailIfItTookFocus(window, automation);
            AssertEventuallyFocused(WaitForElement(window, "StatusBar", TimeSpan.FromSeconds(10)), "F6 did not reach the status bar.");
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(WaitForElement(window, "FileMenu", TimeSpan.FromSeconds(10)), "F6 from the status bar did not wrap to the menu bar.");
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(tree, "F6 from the menu bar did not land on Files.");

            // Open a note: Files → tab bar → editor → … ; then Shift+F6 back.
            // The tree item's automation name is "{DisplayName}, {kind}"
            // ("alpha.md, file"), so this matches by prefix the way the
            // other journeys in this file do rather than an exact name.
            AutomationElement note = WaitForTreeItemStartingWith(tree, automation, "alpha.md");
            note.Patterns.SelectionItem.Pattern.Select();
            AutomationElement editor = WaitForEditor(window, automation, "alpha.md editor", TimeSpan.FromSeconds(10));
            // Selecting the row shows the note and leaves focus where it
            // was (W7-7, R-2); UIA's Select() moves no keyboard focus, and
            // once a TreeViewItem is selected, WPF's TreeView automation
            // peer throws InvalidOperationException from SetFocus() on the
            // TreeView container itself (a known WPF/UIA quirk for
            // ItemsControl containers with a live selection) — focus the
            // selected row instead and confirm with a descendant check
            // against the tree, the same pattern the right-pane rail uses
            // above for its own container-vs-selected-item quirk.
            FocusFilesTreeSelection(window, automation, "alpha.md", "The Files tree did not take focus back.");
            PressKey(VirtualKeyShort.F6);
            AutomationElement tabs = WaitForElement(window, "WorkspaceTabs", TimeSpan.FromSeconds(10));
            AutomationElement tab = FindDescendantWithin(tabs, automation.ConditionFactory.ByControlType(ControlType.TabItem), TimeSpan.FromSeconds(10))
                ?? throw new Xunit.Sdk.XunitException("No tab item.");
            AssertEventuallyFocused(tab, "F6 from Files did not land on the tab bar.");
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(editor, "F6 from the tab bar did not land in the editor.");

            // With a note open the right pane has a leaf body, so the ring's
            // two right stops are both live: forward to them and the status
            // bar, then back through them to the editor. The zero-tab walk
            // above cannot cover this — its Outline leaf is empty.
            PressKey(VirtualKeyShort.F6);
            AssertRightPaneStop(window, automation, "F6 from the editor did not land in the right pane.");
            PressKey(VirtualKeyShort.F6);
            AdvancePastTheRailIfItTookFocus(window, automation);
            AssertEventuallyFocused(WaitForElement(window, "StatusBar", TimeSpan.FromSeconds(10)), "F6 from the right pane did not reach the status bar.");
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6);
            AssertRightPaneStop(window, automation, "Shift+F6 from the status bar did not return to the right pane.");
            RetreatPastTheRightPane(window, automation);
            AssertEventuallyFocused(WaitForElement(window, "MarkdownEditor", TimeSpan.FromSeconds(10)), "Shift+F6 from the right pane did not return to the editor.");

            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6);
            AssertEventuallyFocused(tab, "Shift+F6 from the editor did not return to the tab bar.");
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6);
            AssertFilesTreeRegionFocused(window, automation, tree, "Shift+F6 from the tab bar did not return to Files.");
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6);
            AssertEventuallyFocused(WaitForElement(window, "FileMenu", TimeSpan.FromSeconds(10)), "Shift+F6 from Files did not land on the menu bar.");
            PressKey(VirtualKeyShort.ESCAPE);
            // Spec §4: Escape leaves menu mode and WPF restores focus to
            // where the menu took it from — the Files region, the origin of
            // the Shift+F6 that landed on the bar. Restored to the SELECTED
            // alpha.md row, not the TreeView container, so this is the
            // region check the two presses above already use.
            AssertFilesTreeRegionFocused(window, automation, tree, "Escape from the menu bar did not return to the origin region.");

            // Hidden right pane: editor → status bar directly.
            editor.Focus();
            AssertEventuallyFocused(editor, "The editor did not take focus.");
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
            AssertElementDisappears(window, automation, "RightPaneLeaves");
            editor.Focus();
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(WaitForElement(window, "StatusBar", TimeSpan.FromSeconds(10)), "With the right pane hidden, F6 from the editor did not skip to the status bar.");

            AssertAxeClean(process, "shell-regions");
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>Focuses the Files tree row starting with <paramref
    /// name="prefix"/> — not the TreeView container, whose SetFocus()
    /// throws InvalidOperationException once a TreeViewItem is selected
    /// (a WPF/UIA quirk, not this journey's or the host's defect) — and
    /// confirms the focus landed somewhere inside the tree. Re-finds the
    /// row each attempt: opening a note republishes the tree's row peers
    /// for a moment, so an element fetched an instant earlier can already
    /// be disconnected by the time Focus() runs.</summary>
    private static void FocusFilesTreeSelection(Window window, UIA3Automation automation, string prefix, string message)
    {
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));
                        AutomationElement? row = tree
                            .FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.TreeItem))
                            .FirstOrDefault(item => (item.Properties.Name.ValueOrDefault ?? string.Empty)
                                .StartsWith(prefix, StringComparison.Ordinal));
                        if (row is null) { return false; }
                        row.Focus();
                        return automation.FocusedElement() is { } focused && IsDescendantOf(focused, tree);
                    }
                    catch (InvalidOperationException) { return false; }
                    catch (Exception exception) when (IsTransientUiaFault(exception)) { return false; }
                },
                TimeSpan.FromSeconds(10)),
            $"{message} {FocusDiagnosis()}");
    }

    private static void AssertFilesTreeRegionFocused(Window window, UIA3Automation automation, AutomationElement tree, string message)
    {
        Assert.True(
            SpinWait.SpinUntil(() => automation.FocusedElement() is { } focused && IsDescendantOf(focused, tree), TimeSpan.FromSeconds(10)),
            $"{message} {FocusDiagnosis()}");
    }

    /// <summary>The right-pane content stop for the default Outline leaf
    /// with no note open is the rail itself when the leaf has no
    /// focusable stop; either is a right-pane landing.</summary>
    private static void AssertRightPaneStop(Window window, UIA3Automation automation, string message)
    {
        AutomationElement pane = WaitForElement(window, "InspectorPane", TimeSpan.FromSeconds(10));
        Assert.True(
            SpinWait.SpinUntil(() => automation.FocusedElement() is { } focused && IsDescendantOf(focused, pane), TimeSpan.FromSeconds(10)),
            $"{message} {FocusDiagnosis()}");
    }

    private static void AdvancePastTheRailIfItTookFocus(Window window, UIA3Automation automation)
    {
        // With the Outline leaf empty, the content stop is skipped and this
        // press already reached the rail; one more press reaches the status
        // bar. When a stop exists, this press lands on the rail. The rail
        // is a ListBox, and WPF/UIA report keyboard focus on the selected
        // ListBoxItem rather than the ListBox itself, so this checks
        // whether the focused element is the rail or a descendant of it
        // rather than the rail's own HasKeyboardFocus property.
        AutomationElement rail = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(10));
        if (automation.FocusedElement() is { } focused && IsDescendantOf(focused, rail))
        {
            PressKey(VirtualKeyShort.F6);
        }
    }

    /// <summary>The backward twin of <see
    /// cref="AdvancePastTheRailIfItTookFocus"/>: Shift+F6 out of the right
    /// pane, which holds one stop (the rail alone) or two (a leaf body's
    /// first stop and the rail) depending on the shown leaf.</summary>
    private static void RetreatPastTheRightPane(Window window, UIA3Automation automation)
    {
        AutomationElement pane = WaitForElement(window, "InspectorPane", TimeSpan.FromSeconds(10));
        for (int step = 0; step < 2; step++)
        {
            if (automation.FocusedElement() is not { } focused || !IsDescendantOf(focused, pane))
            {
                return;
            }

            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6);
        }
    }
}
