// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    private const string FindNote = "alpha beta alpha gamma alpha\n";

    /// <summary>
    /// WB-22 PR 1 (#1112), cross-process: Ctrl+F in a note lands in the
    /// find bar's query; typing counts the matches in the count readout's
    /// Name, and Enter moves the editor's selection (its Text pattern) to
    /// the next match. Axe-clean with text typed (the #1106 lesson).
    /// </summary>
    [Fact]
    public void FindInNote_TypingAnnouncesTheCountAndEnterMovesTheCaret() =>
        RunFindInNoteJourney("find-in-note-typing", (process, window, automation, editor) =>
        {
            AutomationElement query = OpenFindBarAndType(window, editor, "alpha");
            Assert.True(query.Properties.HasKeyboardFocus.ValueOrDefault, "Ctrl+F did not land in the find bar's query");

            int before = SelectionStart(editor);
            Keyboard.Type(VirtualKeyShort.RETURN);
            Assert.True(
                SpinWait.SpinUntil(() => SelectionStart(editor) != before, TimeSpan.FromSeconds(10)),
                "Enter in the query did not move the editor's selection");
            Assert.Equal("alpha", SelectionText(editor));
            Assert.Equal("2 of 3", WaitForElement(window, "FindInNoteCount", TimeSpan.FromSeconds(5)).Name);

            AssertAxeClean(process, "find-in-note");
        });

    /// <summary>
    /// WB-22 PR 1 (#1112), cross-process: with the bar open, Ctrl+G from the
    /// editor text moves the editor's selection to the next match and the
    /// focus stays in the editor.
    /// </summary>
    [Fact]
    public void FindInNote_CtrlGFromTheEditorTextMovesTheSelectionAndKeepsFocus() =>
        RunFindInNoteJourney("find-in-note-ctrl-g", (process, window, automation, editor) =>
        {
            _ = OpenFindBarAndType(window, editor, "alpha");
            editor.Focus();
            Assert.True(
                SpinWait.SpinUntil(
                    () => automation.FocusedElement()?.AutomationId == "MarkdownEditor",
                    TimeSpan.FromSeconds(5)),
                "the editor did not take the focus back");

            int before = SelectionStart(editor);
            using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
            {
                Keyboard.Type(VirtualKeyShort.KEY_G);
            }
            Assert.True(
                SpinWait.SpinUntil(() => SelectionStart(editor) != before, TimeSpan.FromSeconds(10)),
                "Ctrl+G from the editor text did not move its selection");
            Assert.Equal("alpha", SelectionText(editor));
            Assert.Equal("MarkdownEditor", automation.FocusedElement()?.AutomationId);
            // R-4: the runtime item-name census at this state (the bar open,
            // the editor holding the keys).
            AssertItemNamesAreSpeakable(process, "find-in-note-ctrl-g");
        });

    private static AutomationElement OpenFindBarAndType(Window window, AutomationElement editor, string query)
    {
        editor.Focus();
        using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
        {
            Keyboard.Type(VirtualKeyShort.KEY_F);
        }
        AutomationElement field = WaitForElement(window, "FindInNoteQuery", TimeSpan.FromSeconds(10));
        Assert.True(
            SpinWait.SpinUntil(() => field.Properties.HasKeyboardFocus.ValueOrDefault, TimeSpan.FromSeconds(5)),
            "Ctrl+F did not focus the find bar's query");
        Keyboard.Type(query);
        Assert.True(
            SpinWait.SpinUntil(
                () => window.FindFirstDescendant(cf => cf.ByAutomationId("FindInNoteCount"))?.Name == "1 of 3",
                TimeSpan.FromSeconds(10)),
            "the count readout never read \"1 of 3\"");
        return field;
    }

    private static int SelectionStart(AutomationElement editor)
    {
        var text = editor.Patterns.Text.Pattern;
        var selection = text.GetSelection().Single();
        return selection.CompareEndpoints(
            FlaUI.Core.Definitions.TextPatternRangeEndpoint.Start,
            text.DocumentRange,
            FlaUI.Core.Definitions.TextPatternRangeEndpoint.Start);
    }

    private static string SelectionText(AutomationElement editor) =>
        editor.Patterns.Text.Pattern.GetSelection().Single().GetText(-1);

    private static void RunFindInNoteJourney(
        string name, Action<Process, Window, UIA3Automation, AutomationElement> journey)
    {
        string testRoot = Path.Combine(Path.GetTempPath(), $"slate-{name}-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Find Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(Path.Combine(vaultRoot, "note.md"), FindNote);

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
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] = $"slate-{name}-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");
            if (!HasInteractiveDesktop(process, name))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process, automation, Path.Combine(logDirectory, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            AutomationElement filesTree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(30));
            WaitForTreeItemStartingWith(filesTree, automation, "note.md").Patterns.SelectionItem.Pattern.Select();
            AutomationElement editor = WaitForEditor(window, automation, "note.md editor", TimeSpan.FromSeconds(10));
            journey(process, window, automation, editor);
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
}
