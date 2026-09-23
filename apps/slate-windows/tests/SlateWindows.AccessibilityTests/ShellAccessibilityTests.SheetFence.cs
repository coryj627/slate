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
    /// W7-7 (#1248, contract R-6): the NVDA pass's F5 repro, keyed. With
    /// a note's editor holding focus, Ctrl+Shift+N opens the two-prompt
    /// template; Tab walks Topic → Attendees → Cancel → Next → Topic and
    /// Shift+Tab walks back, all inside the sheet, and after Escape the
    /// note behind it is byte-identical and its tab still reads Saved —
    /// where the run found three tab characters and "unsaved changes".
    /// Inside the sheet the field keeps Ctrl+A, and Ctrl+Shift+R stays
    /// refused beneath it (the modal routing, contract 30 TR-7). The same
    /// Tab press is made in Add property (Editor menu, Alt+E then P) and
    /// Bulk rename (Ctrl+Shift+R), each opened from the focused editor —
    /// the route that sends a leaked command to the editor.
    /// </summary>
    [Fact]
    public void Templates_PromptTabTraversalStaysInTheSheet()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-sheet-fence-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Fence Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(Path.Combine(vault, "Templates"));
        // The first line is indented: a leaked Shift+Tab (TabBackward)
        // unindents the caret's line, a leaked Tab (TabForward) inserts.
        const string Note = "\tIndented agenda line\n\nBody paragraph.\n";
        File.WriteAllText(Path.Combine(vault, "agenda.md"), Note);
        File.WriteAllText(
            Path.Combine(vault, "Templates", "meeting-note.md"),
            "---\ndescription: Two prompts\n---\n# {{title}}\n\n"
            + "Topic: {{prompt:Topic}}\nAttendees: {{prompt:Attendees}}\n");

        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            start.ArgumentList.Add(vault);
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "sheet-fence-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "sheet-fence"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            _ = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));

            AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));
            WaitForTreeItemStartingWith(tree, automation, "agenda.md").Patterns.SelectionItem.Pattern.Select();
            AutomationElement editor = WaitForEditor(window, automation, "agenda.md editor", TimeSpan.FromSeconds(10));
            AutomationElement tabs = WaitForElement(window, "WorkspaceTabs", TimeSpan.FromSeconds(10));
            AutomationElement tab = FindDescendantWithin(
                    tabs, automation.ConditionFactory.ByControlType(ControlType.TabItem), TimeSpan.FromSeconds(10))
                ?? throw new Xunit.Sdk.XunitException("The note's tab is absent.");
            AssertTheNoteIsUntouched(editor, tab, Note, "before any sheet opened");

            // --- the template prompt sheet, opened from the editor ------
            FocusTheEditor(window, editor);
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_N);
            _ = WaitForElement(window, "TemplatePickerSheet", TimeSpan.FromSeconds(10));
            AutomationElement pickerList = WaitForElement(window, "TemplatePickerList", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () => pickerList.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.ListItem))
                        ?.Properties.HasKeyboardFocus.ValueOrDefault == true,
                    TimeSpan.FromSeconds(10)),
                $"the picker did not focus its meeting-note row. {FocusDiagnosis()}");
            PressKey(VirtualKeyShort.RETURN);
            AutomationElement flow = WaitForElement(window, "TemplateFlowSheet", TimeSpan.FromSeconds(10));
            AutomationElement topic = PromptField(flow, automation, "Topic");
            AutomationElement attendees = PromptField(flow, automation, "Attendees");
            AssertEventuallyFocused(topic, "the prompt step did not focus Topic (T4).");

            PressKey(VirtualKeyShort.TAB);
            AssertEventuallyFocused(attendees, "Tab from Topic did not reach Attendees inside the sheet (R-6).");
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.TAB);
            AssertEventuallyFocused(topic, "Shift+Tab from Attendees did not return to Topic (R-6).");
            // The whole cycle, the recorded three presses and one more:
            // the footer precedes the prompts in the tree, so Tab wraps
            // from the last prompt to Cancel, then Next, then Topic.
            PressKey(VirtualKeyShort.TAB);
            AssertEventuallyFocused(attendees, "Tab did not reach Attendees.");
            PressKey(VirtualKeyShort.TAB);
            AssertEventuallyFocused(
                WaitForElement(window, "TemplatePromptsCancel", TimeSpan.FromSeconds(10)),
                "Tab from the last prompt did not wrap to Cancel inside the sheet.");
            PressKey(VirtualKeyShort.TAB);
            AssertEventuallyFocused(
                WaitForElement(window, "TemplatePromptsNext", TimeSpan.FromSeconds(10)),
                "Tab from Cancel did not reach Next.");
            PressKey(VirtualKeyShort.TAB);
            AssertEventuallyFocused(topic, "Tab from Next did not cycle back to Topic.");

            // The fence intercepts nothing else: the field keeps its own
            // select-all, and the shell chord stays the modal routing's
            // to refuse beneath the sheet.
            topic.Patterns.Value.Pattern.SetValue("Quarterly sync");
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type("Weekly");
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            Assert.True(
                SpinWait.SpinUntil(
                    () => topic.Patterns.Value.Pattern.Value.ValueOrDefault == "Weekly",
                    TimeSpan.FromSeconds(10)),
                $"Ctrl+A did not select Topic's text; it reads '{topic.Patterns.Value.Pattern.Value.ValueOrDefault}'.");
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_R);
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));
            Assert.Null(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("BulkRenameSheet")));
            AssertEventuallyFocused(topic, "Ctrl+Shift+R under the sheet moved focus out of Topic.");

            PressKey(VirtualKeyShort.ESCAPE);
            AssertElementDisappears(window, automation, "TemplateFlowSheet");
            AssertTheNoteIsUntouched(editor, tab, Note, "after the template prompt sheet");

            // --- Add property, through its menu route from the editor --
            FocusTheEditor(window, editor);
            PressChord(VirtualKeyShort.ALT, VirtualKeyShort.KEY_E);
            PressKey(VirtualKeyShort.KEY_P);
            _ = WaitForElement(window, "AddPropertySheet", TimeSpan.FromSeconds(10));
            AssertTabStaysInTheSheet(window, "AddPropertyKey", "AddPropertyType", "Add property");
            PressKey(VirtualKeyShort.ESCAPE);
            AssertElementDisappears(window, automation, "AddPropertySheet");
            AssertTheNoteIsUntouched(editor, tab, Note, "after the Add property sheet");

            // --- Bulk rename, through its chord from the editor ---------
            FocusTheEditor(window, editor);
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_R);
            _ = WaitForElement(window, "BulkRenameSheet", TimeSpan.FromSeconds(10));
            AssertTabStaysInTheSheet(window, "BulkRenameOldKey", "BulkRenameOldKeyType", "Bulk rename");
            PressKey(VirtualKeyShort.ESCAPE);
            AssertElementDisappears(window, automation, "BulkRenameSheet");
            AssertTheNoteIsUntouched(editor, tab, Note, "after the Bulk rename sheet");

            Assert.Equal(Note, File.ReadAllText(Path.Combine(vault, "agenda.md")));
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { Directory.Delete(root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>The note editor holds keyboard focus — it is then the
    /// window scope's logical focus, the element a sheet's focus scope
    /// hands an unanswered command to.</summary>
    private static void FocusTheEditor(Window window, AutomationElement editor)
    {
        ReassertForegroundForAChord(window);
        editor.Focus();
        AssertEventuallyFocused(editor, "The note's editor did not take focus.");
    }

    private static AutomationElement PromptField(AutomationElement flow, UIA3Automation automation, string label)
    {
        AutomationElement? field = null;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        field = flow.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Edit))
                            .FirstOrDefault(box => box.Name == label);
                        return field is not null;
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception))
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(10)),
            $"the prompt field '{label}' is absent.");
        return field!;
    }

    /// <summary>The sheet's first field takes focus on open; Tab reaches
    /// the next stop inside the sheet and Shift+Tab comes back.</summary>
    private static void AssertTabStaysInTheSheet(Window window, string firstId, string nextId, string sheet)
    {
        AutomationElement first = WaitForElement(window, firstId, TimeSpan.FromSeconds(10));
        AssertEventuallyFocused(first, $"{sheet} did not focus its first field.");
        PressKey(VirtualKeyShort.TAB);
        AssertEventuallyFocused(
            WaitForElement(window, nextId, TimeSpan.FromSeconds(10)),
            $"Tab from {sheet}'s first field did not reach the next stop inside the sheet (R-6).");
        PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.TAB);
        AssertEventuallyFocused(first, $"Shift+Tab did not return to {sheet}'s first field (R-6).");
    }

    /// <summary>The editor's text is exactly the note's, and its tab reads
    /// Saved — the "unsaved changes" the run heard is the leak's mark.</summary>
    private static void AssertTheNoteIsUntouched(AutomationElement editor, AutomationElement tab, string note, string when)
    {
        string text = editor.Patterns.Value.Pattern.Value.ValueOrDefault ?? string.Empty;
        Assert.True(
            text.Replace("\r\n", "\n", StringComparison.Ordinal) == note,
            $"The note behind the sheet changed {when}: '{text.Replace("\t", "\\t", StringComparison.Ordinal)}'.");
        Assert.True(
            SpinWait.SpinUntil(() => tab.Properties.ItemStatus.ValueOrDefault == "Saved", TimeSpan.FromSeconds(5)),
            $"The note's tab does not read Saved {when}: '{tab.Properties.Name.ValueOrDefault}', "
            + $"'{tab.Properties.ItemStatus.ValueOrDefault}'.");
    }
}
