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
    /// <summary>How long a selection-driven open is watched for a focus
    /// move. The old route queued the editor's focus at Input priority
    /// in the same dispatcher turn as the open, so it landed within
    /// milliseconds; this is generous by two orders of magnitude.</summary>
    private static readonly TimeSpan FocusSettle = TimeSpan.FromMilliseconds(1500);

    /// <summary>W7-7 PR 2 (#1245 R-2, OD-2; #1250 R-3), spec §3.4, with
    /// real keys: Down through a folder row and two file rows keeps focus
    /// on each row while the editor shows the file; Space toggles the
    /// focused row's batch check box (the row's ItemStatus reports it,
    /// focus stays); Enter moves focus into the note; Enter and
    /// Ctrl+Enter open from the filter results, the tree and the dual
    /// pane, Ctrl+Enter in a new tab; and the Tags tree filters by core's
    /// grammar — <c>#accessibility</c> in the field, one result — and a
    /// whitespace tag by the out-of-band scope, whose summary is shown.
    /// The vault is this journey's own, so its extra notes touch no
    /// shared fixture.</summary>
    [Fact]
    public void FilesTree_ArrowsKeepFocusEnterOpens()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-files-tree-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(Path.Combine(vault, "Folder"));
        File.WriteAllText(Path.Combine(vault, "alpha.md"), "# Alpha\n\nBody.\n");
        File.WriteAllText(
            Path.Combine(vault, "note.md"),
            "---\ntags:\n  - accessibility\n---\n\n# Accessible note\n");
        File.WriteAllText(
            Path.Combine(vault, "zeta.md"),
            "---\ntags: [\"two words\"]\n---\n\n# Zeta\n");
        File.WriteAllText(Path.Combine(vault, "Folder", "child.md"), "# Child note\n");

        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            start.ArgumentList.Add(vault);
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "files-tree-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "files-tree")) { return; }
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(30));
            // The launch lands focus on the tree once the vault has opened
            // and published its rows (W7-5); interacting before that races
            // the landing and the first publication.
            AssertEventuallyFocused(tree, "Opening the vault did not land focus on the Files tree.");
            // Rows sort by name, folders among files: alpha.md, Folder,
            // note.md, zeta.md.
            foreach (string row in new[] { "alpha.md", "Folder", "note.md", "zeta.md" })
            {
                _ = WaitForTreeItemStartingWith(tree, automation, row);
            }

            Assert.StartsWith(
                "Up and Down move through files and folders",
                tree.Properties.HelpText.ValueOrDefault ?? string.Empty,
                StringComparison.Ordinal);

            // A file row, a folder row, a file row: each file is shown,
            // and focus never leaves its row (R-2). Focusing the first row
            // selects it, the same selection-driven open an arrow makes.
            WaitForTreeItemStartingWith(tree, automation, "alpha.md").Focus();
            _ = WaitForEditor(window, automation, "alpha.md editor", TimeSpan.FromSeconds(10));
            AssertFocusStaysOnRow(automation, tree, "alpha.md", "Selecting alpha.md moved focus off the Files tree.");
            PressDownArrow();
            AssertFocusStaysOnRow(automation, tree, "Folder", "Down did not move to the Folder row.");
            _ = WaitForEditor(window, automation, "alpha.md editor", TimeSpan.FromSeconds(10));
            PressDownArrow();
            AutomationElement noteEditor = WaitForEditor(window, automation, "note.md editor", TimeSpan.FromSeconds(10));
            AssertFocusStaysOnRow(automation, tree, "note.md", "Down onto note.md moved focus off the Files tree.");
            Assert.Equal(1, TabCount(window, automation));

            // Space checks the focused row for batch actions — the row
            // reports it, the check box follows, focus stays — and again
            // unchecks it.
            AutomationElement noteRow = WaitForTreeItemStartingWith(tree, automation, "note.md");
            PressKey(VirtualKeyShort.SPACE);
            AssertBatchChecked(automation, noteRow, true);
            AssertFocusStaysOnRow(automation, tree, "note.md", "Space moved focus off the note.md row.");
            PressKey(VirtualKeyShort.SPACE);
            AssertBatchChecked(automation, noteRow, false);

            // Enter is the explicit open: focus moves into the note.
            PressKey(VirtualKeyShort.ENTER);
            AssertEventuallyFocused(noteEditor, "Enter on the note.md row did not move focus into the note.");
            Assert.Equal("MarkdownEditor", automation.FocusedElement()?.Properties.AutomationId.ValueOrDefault);
            Assert.Equal(1, TabCount(window, automation));

            // The filter results: Enter replaces the current tab's note
            // and moves focus; Ctrl+Enter opens a new tab.
            // The results list exists in the UIA tree only while a
            // filter is active, so it is found after each activation.
            AutomationElement field = WaitForElement(window, "SidebarFilter", TimeSpan.FromSeconds(10));
            field.Patterns.Value.Pattern.SetValue("alpha");
            FocusOnlyListRow(automation, FilterResults(window), "alpha.md", "The alpha filter result did not take focus.");
            PressKey(VirtualKeyShort.ENTER);
            AssertEventuallyFocused(
                WaitForEditor(window, automation, "alpha.md editor", TimeSpan.FromSeconds(10)),
                "Enter on a filter result did not move focus into its note.");
            Assert.Equal(1, TabCount(window, automation));
            field.Patterns.Value.Pattern.SetValue("zeta");
            FocusOnlyListRow(automation, FilterResults(window), "zeta.md", "The zeta filter result did not take focus.");
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.ENTER);
            AssertEventuallyFocused(
                WaitForEditor(window, automation, "zeta.md editor", TimeSpan.FromSeconds(10)),
                "Ctrl+Enter on a filter result did not move focus into its note.");
            AssertTabCount(window, automation, 2, "Ctrl+Enter on a filter result did not open a new tab.");

            // The tree again: its selected row (note.md, which no tab
            // holds any more) takes focus without an open, and Ctrl+Enter
            // opens it in a new tab.
            field.Patterns.Value.Pattern.SetValue(string.Empty);
            AutomationElement selectedRow = WaitForTreeItemStartingWith(tree, automation, "note.md");
            selectedRow.Focus();
            AssertFocusStaysOnRow(automation, tree, "note.md", "The selected note.md row did not take focus.");
            AssertTabCount(window, automation, 2, "Focusing the selected row opened something.");
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.ENTER);
            AssertEventuallyFocused(
                WaitForEditor(window, automation, "note.md editor", TimeSpan.FromSeconds(10)),
                "Ctrl+Enter on a Files tree row did not move focus into its note.");
            AssertTabCount(window, automation, 3, "Ctrl+Enter on a Files tree row did not open a new tab.");

            // The dual pane, showing the selected folder's files:
            // Ctrl+Enter opens child.md in a new tab; Enter on the same row
            // re-activates it and moves focus.
            WaitForElement(window, "SidebarDualPaneToggle", TimeSpan.FromSeconds(10)).Patterns.Toggle.Pattern.Toggle();
            AutomationElement dualPane = WaitForElement(window, "SidebarDualPane", TimeSpan.FromSeconds(10));
            WaitForTreeItemStartingWith(tree, automation, "Folder").Focus();
            AssertFocusStaysOnRow(automation, tree, "Folder", "The Folder row did not take focus back.");
            FocusOnlyListRow(automation, dualPane, "child.md", "The dual pane's child.md row did not take focus.");
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.ENTER);
            AssertEventuallyFocused(
                WaitForEditor(window, automation, "child.md editor", TimeSpan.FromSeconds(10)),
                "Ctrl+Enter on a dual-pane row did not move focus into its note.");
            AssertTabCount(window, automation, 4, "Ctrl+Enter on a dual-pane row did not open a new tab.");
            FocusOnlyListRow(automation, dualPane, "child.md", "The dual pane's child.md row did not take focus back.");
            PressKey(VirtualKeyShort.ENTER);
            AssertEventuallyFocused(
                WaitForEditor(window, automation, "child.md editor", TimeSpan.FromSeconds(10)),
                "Enter on a dual-pane row did not move focus into its note.");
            AssertTabCount(window, automation, 4, "Enter on an open dual-pane row opened another tab.");

            // R-3: a tag row filters by core's grammar, and a whitespace
            // tag by the out-of-band scope, whose summary names it.
            WaitForElement(window, "SidebarShowTags", TimeSpan.FromSeconds(10)).Patterns.Toggle.Pattern.Toggle();
            AutomationElement tags = WaitForElement(window, "SidebarTagTree", TimeSpan.FromSeconds(10));
            WaitForNamedElement(tags, automation, "accessibility, 1 file", TimeSpan.FromSeconds(10))
                .Patterns.SelectionItem.Pattern.Select();
            AssertFieldValue(field, "#accessibility");
            AssertListRowCount(automation, FilterResults(window), 1, "The accessibility tag did not filter to its one note.");
            WaitForNamedElement(tags, automation, "two words, 1 file", TimeSpan.FromSeconds(10))
                .Patterns.SelectionItem.Pattern.Select();
            AssertFieldValue(field, string.Empty);
            AssertListRowCount(automation, FilterResults(window), 1, "The whitespace tag's scope did not filter to its one note.");
            _ = WaitForNamedElement(window, automation, "1 result for #two words.", TimeSpan.FromSeconds(10));
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>The Down arrow as the keyboard's own arrow key: scan code
    /// 0x50 with the extended flag. A virtual-key press goes out without
    /// that flag, which Windows and a running screen reader read as
    /// numpad 2 — NVDA consumes it as a review-cursor gesture before the
    /// app sees it (its log records <c>kb(laptop):numpad2</c>), so the
    /// journey would drive a key no user presses on the arrow
    /// cluster.</summary>
    private static void PressDownArrow() => Keyboard.TypeScanCode(0x50, true);

    /// <summary>R-2's observable: focus is on the row named
    /// <paramref name="prefix"/> inside <paramref name="container"/>, and
    /// it STAYS there for <see cref="FocusSettle"/> — the window in which
    /// the old route's queued editor focus landed.</summary>
    private static void AssertFocusStaysOnRow(
        UIA3Automation automation, AutomationElement container, string prefix, string message)
    {
        Assert.True(
            SpinWait.SpinUntil(() => FocusIsOnRow(automation, container, prefix) == true, TimeSpan.FromSeconds(10)),
            $"{message} Focused: {DescribeFocus(automation)}. {FocusDiagnosis()}");
        var clock = Stopwatch.StartNew();
        int observed = 0;
        while (clock.Elapsed < FocusSettle)
        {
            // A transient UIA fault is no observation either way.
            if (FocusIsOnRow(automation, container, prefix) is bool onRow)
            {
                Assert.True(
                    onRow,
                    $"{message} Focus left the row after {clock.ElapsedMilliseconds} ms for {DescribeFocus(automation)}.");
                observed++;
            }

            Thread.Sleep(100);
        }

        Assert.True(observed >= 5, $"{message} Focus could be read only {observed} times in the settle window.");
    }

    private static bool? FocusIsOnRow(UIA3Automation automation, AutomationElement container, string prefix)
    {
        try
        {
            return automation.FocusedElement() is { } focused
                && focused.Properties.ControlType.ValueOrDefault is ControlType.TreeItem or ControlType.ListItem
                && (focused.Properties.Name.ValueOrDefault ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal)
                && IsDescendantOf(focused, container);
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
            return null;
        }
    }

    private static string DescribeFocus(UIA3Automation automation)
    {
        try
        {
            return automation.FocusedElement() is { } focused
                ? $"{focused.Properties.ControlType.ValueOrDefault} '{focused.Properties.Name.ValueOrDefault}' "
                    + $"#{focused.Properties.AutomationId.ValueOrDefault}"
                : "<nothing>";
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
            return "<unavailable>";
        }
    }

    /// <summary>A list whose filter or folder leaves exactly one row, the
    /// one showing <paramref name="text"/> — a list keeps its previous
    /// rows until the new ones publish, so the text is the proof the list
    /// is current: focus that row through UIA (focus moves no ListBox
    /// selection, so nothing opens) and confirm it is the focused
    /// element.</summary>
    private static void FocusOnlyListRow(UIA3Automation automation, AutomationElement list, string text, string message)
    {
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        AutomationElement[] rows = list.FindAllDescendants(
                            automation.ConditionFactory.ByControlType(ControlType.ListItem));
                        if (rows.Length != 1
                            || (rows[0].Properties.Name.ValueOrDefault != text
                                && rows[0].FindFirstDescendant(automation.ConditionFactory.ByName(text)) is null))
                        {
                            return false;
                        }

                        rows[0].Focus();
                        return automation.FocusedElement() is { } focused
                            && focused.Properties.ControlType.ValueOrDefault == ControlType.ListItem
                            && IsDescendantOf(focused, list);
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception) || exception is InvalidOperationException)
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(10)),
            $"{message} Focused: {DescribeFocus(automation)}. {FocusDiagnosis()}");
    }

    private static void AssertBatchChecked(UIA3Automation automation, AutomationElement row, bool expected)
    {
        string status = expected ? "Checked for batch actions" : string.Empty;
        ToggleState toggle = expected ? ToggleState.On : ToggleState.Off;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        return (row.Properties.ItemStatus.ValueOrDefault ?? string.Empty) == status
                            && row.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.CheckBox))
                                ?.Patterns.Toggle.Pattern.ToggleState.Value == toggle;
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception))
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(10)),
            $"Space did not leave the row's batch state {(expected ? "checked" : "unchecked")}: "
            + $"ItemStatus '{row.Properties.ItemStatus.ValueOrDefault}'.");
    }

    private static AutomationElement FilterResults(Window window) =>
        WaitForElement(window, "SidebarFilterResults", TimeSpan.FromSeconds(10));

    private static int TabCount(Window window, UIA3Automation automation) =>
        WaitForElement(window, "WorkspaceTabs", TimeSpan.FromSeconds(10))
            .FindAllChildren(automation.ConditionFactory.ByControlType(ControlType.TabItem)).Length;

    private static void AssertTabCount(Window window, UIA3Automation automation, int expected, string message)
    {
        Assert.True(
            SpinWait.SpinUntil(() => TabCount(window, automation) == expected, TimeSpan.FromSeconds(10)),
            $"{message} Tabs: {TabCount(window, automation)}.");
    }

    private static void AssertFieldValue(AutomationElement field, string expected)
    {
        Assert.True(
            SpinWait.SpinUntil(
                () => (field.Patterns.Value.Pattern.Value.ValueOrDefault ?? string.Empty) == expected,
                TimeSpan.FromSeconds(10)),
            $"The filter field reads '{field.Patterns.Value.Pattern.Value.ValueOrDefault}', not '{expected}'.");
    }

    private static void AssertListRowCount(UIA3Automation automation, AutomationElement list, int expected, string message)
    {
        int count = -1;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        count = list.FindAllDescendants(
                            automation.ConditionFactory.ByControlType(ControlType.ListItem)).Length;
                        return count == expected;
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception))
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(10)),
            $"{message} Rows: {count}.");
    }
}
