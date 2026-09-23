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
    /// #1252 (R-9, OD-1): a file created outside Slate appears after one
    /// Files Sidebar → Refresh — in the tree and in Quick Open — an open
    /// note changed outside Slate shows its new content after the next, and
    /// a deleted file leaves the tree after the one after that.
    /// </summary>
    /// <remarks>
    /// TODO(#1244): the announcement legs — "Files refreshed. 1 new or
    /// changed, 0 removed." after the first Refresh, the modification's
    /// sentence after the second, and "Files refreshed. 0 new or changed, 1
    /// removed." after the third, each heard exactly once and nothing else
    /// spoken — wait for PR 1's desktop-scoped notification listener (R-1),
    /// which this branch does not carry; see the TODO blocks below for where
    /// it subscribes and what it asserts. Until then the journey pins what
    /// the UI shows, and the whole-sequence speech is pinned by the hosted
    /// <c>RescanTests</c> facts through the real lifecycle.
    /// </remarks>
    [Fact]
    public void ExternalFiles_AppearAfterRefresh()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-external-files-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "External Files Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        string notePath = Path.Combine(vaultRoot, "note.md");
        string latePath = Path.Combine(vaultRoot, "late.md");
        File.WriteAllText(notePath, "# Note\n\nOriginal body.\n");

        // TODO(#1244): subscribe PR 1's desktop-scoped notification listener
        // HERE, before launch (R-1), and keep it for every leg below.

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-external-files-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");
            if (!HasInteractiveDesktop(process, "external-files"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            window.SetForeground();
            WaitForVaultOpen(window);
            AutomationElement filesTree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));

            // The note is open in the editor before anything changes on disk.
            _ = SelectTreeItem(window, automation, "note");
            _ = WaitForEditor(window, automation, "note.md editor", TimeSpan.FromSeconds(10));
            WaitForEditorText(window, automation, "note.md editor", "Original body.");

            // --- leg 1: a file created outside Slate --------------------------
            File.WriteAllText(latePath, "# Late\n");
            InvokeFilesSidebarRefreshFromTheMenu(window);

            _ = WaitForTreeItemStartingWith(filesTree, automation, "late");
            AssertQuickOpenFinds(window, automation, "late", "late");

            // TODO(#1244), with the listener: exactly one notification since the
            // Refresh — "Files refreshed. 1 new or changed, 0 removed." — and no
            // "Scanning vault…" / "Scan complete…" line (a rescan never speaks
            // the scan family).

            // --- leg 2: the OPEN note changed outside Slate --------------------
            DateTime before = File.GetLastWriteTimeUtc(notePath);
            File.WriteAllText(notePath, "# Note\n\nChanged outside Slate.\n");
            File.SetLastWriteTimeUtc(notePath, before.AddSeconds(5));
            InvokeRefreshButton(window);

            WaitForEditorText(window, automation, "note.md editor", "Changed outside Slate.");
            Assert.DoesNotContain(
                "Original body.",
                EditorText(window, automation, "note.md editor"),
                StringComparison.Ordinal);

            // TODO(#1244), with the listener: "Files refreshed. 1 new or changed,
            // 0 removed." once, and no "missing from disk" line.

            // --- leg 3: a file deleted outside Slate ---------------------------
            File.Delete(latePath);
            InvokeRefreshButton(window);

            Assert.True(
                SpinWait.SpinUntil(
                    () => !TreeListsItemStartingWith(filesTree, automation, "late"),
                    TimeSpan.FromSeconds(15)),
                "late.md stayed in the files tree after it was deleted and Refresh ran.");

            // TODO(#1244), with the listener: "Files refreshed. 0 new or changed,
            // 1 removed." once — the announcement counts the removal.
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }

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

    /// <summary>File ▸ Files Sidebar ▸ Refresh — the menu route.</summary>
    private static void InvokeFilesSidebarRefreshFromTheMenu(Window window)
    {
        AutomationElement fileMenu = WaitForElement(window, "FileMenu", TimeSpan.FromSeconds(10));
        fileMenu.Patterns.ExpandCollapse.Pattern.Expand();
        AutomationElement sidebarMenu = WaitForElement(window, "FilesSidebarMenu", TimeSpan.FromSeconds(10));
        sidebarMenu.Patterns.ExpandCollapse.Pattern.Expand();
        AutomationElement refresh = WaitForElement(
            window, "FilesSidebarRefreshMenuItem", TimeSpan.FromSeconds(10));
        Assert.True(refresh.IsEnabled, "Files Sidebar ▸ Refresh is disabled.");
        refresh.Patterns.Invoke.Pattern.Invoke();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
    }

    /// <summary>The Files pane's "Refresh files" button — the button route.</summary>
    private static void InvokeRefreshButton(Window window)
    {
        AutomationElement button = WaitForElement(window, "SidebarRefresh", TimeSpan.FromSeconds(10));
        Assert.Equal("Refresh files", button.Name);
        button.Patterns.Invoke.Pattern.Invoke();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
    }

    /// <summary>Ctrl+O, type the query, find a row whose name starts with
    /// <paramref name="expectedPrefix"/>, then Escape.</summary>
    private static void AssertQuickOpenFinds(
        Window window, UIA3Automation automation, string query, string expectedPrefix)
    {
        WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10)).Focus();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
        ReassertForegroundForAChord(window);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_O);
        AutomationElement search = WaitForElement(window, "QuickSwitcherSearch", TimeSpan.FromSeconds(10));
        AssertEventuallyFocused(search, "Quick Open did not move focus to its search field.");
        Keyboard.Type(query);
        AutomationElement results = WaitForElement(window, "QuickSwitcherResults", TimeSpan.FromSeconds(10));
        string[] names = [];
        bool found = SpinWait.SpinUntil(
            () =>
            {
                try
                {
                    names = [.. results
                        .FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.ListItem))
                        .Select(item => item.Properties.Name.ValueOrDefault ?? string.Empty)];
                    return names.Any(name => name.StartsWith(expectedPrefix, StringComparison.Ordinal));
                }
                catch (Exception exception) when (IsTransientUiaFault(exception))
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(10));
        Keyboard.Press(VirtualKeyShort.ESCAPE);
        AssertQuickSwitcherDisappears(window, automation);
        Assert.True(
            found,
            $"Quick Open did not find '{expectedPrefix}' for \"{query}\"; rows: [{string.Join(" | ", names)}]");
    }

    private static bool TreeListsItemStartingWith(
        AutomationElement tree, UIA3Automation automation, string prefix)
    {
        try
        {
            return tree
                .FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.TreeItem))
                .Any(item => (item.Properties.Name.ValueOrDefault ?? string.Empty)
                    .StartsWith(prefix, StringComparison.Ordinal));
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
            return true;
        }
    }

    /// <summary>The named editor's whole text, re-found on every read: a
    /// reload swaps the document under the control, and a stale element
    /// must not read as an empty note.</summary>
    private static string EditorText(Window window, UIA3Automation automation, string editorName)
    {
        try
        {
            AutomationElement? editor = window
                .FindAllDescendants(automation.ConditionFactory.ByAutomationId("MarkdownEditor"))
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Properties.Name.ValueOrDefault, editorName, StringComparison.Ordinal));
            return editor?.Patterns.Text.Pattern.DocumentRange.GetText(-1) ?? string.Empty;
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
            return string.Empty;
        }
    }

    private static void WaitForEditorText(
        Window window, UIA3Automation automation, string editorName, string expected) =>
        Assert.True(
            SpinWait.SpinUntil(
                () => EditorText(window, automation, editorName).Contains(expected, StringComparison.Ordinal),
                TimeSpan.FromSeconds(15)),
            $"the open note never showed \"{expected}\"; it shows "
            + $"\"{EditorText(window, automation, editorName)}\"");
}
