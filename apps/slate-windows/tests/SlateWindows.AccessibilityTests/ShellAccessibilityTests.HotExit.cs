// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using uniffi.slate_uniffi;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>The words the hot-exit journeys type at the end of
    /// <c>note.md</c>: no fixture note holds them.</summary>
    private const string HotExitTyped = " Kept for next time.";

    /// <summary>
    /// Plan 44 WB-29 (#1525; the spec's Tests table): the real process closes
    /// with an unsaved note and asks nothing. <c>WM_CLOSE</c> ends Slate
    /// within fifteen seconds with no dialog; the file on disk is unchanged;
    /// relaunching on the same vault and device state brings the note back as
    /// a dirty tab holding the typed words. A listener on the desktop root,
    /// registered before the relaunch, hears core's
    /// <c>UnsavedNotesRestored</c> for one note exactly once, after
    /// <c>VaultOpened</c>.
    /// </summary>
    [Fact]
    public void HotExit_ClosingAndReopeningKeepsAnUnsavedNote()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), $"slate-hot-exit-close-{Guid.NewGuid():N}");
        string vaultName = $"Hot Exit Vault {Guid.NewGuid():N}"[..24];
        string vaultRoot = Path.Combine(testRoot, vaultName);
        string logDirectory = Path.Combine(testRoot, "logs");
        string relaunchLogDirectory = Path.Combine(testRoot, "logs-relaunch");
        WriteShellFixtureVault(vaultRoot);
        string notePath = Path.Combine(vaultRoot, "note.md");
        string onDisk = File.ReadAllText(notePath);
        var heard = new ConcurrentQueue<ReceivedNotification>();
        Process? process = null;
        try
        {
            process = StartShellProcess(vaultRoot, logDirectory);
            if (!HasInteractiveDesktop(process, "hot-exit-close"))
            {
                return;
            }

            string typed = TypeIntoTheNote(process, Path.Combine(logDirectory, "slate-windows.log"));

            // No prompt: WM_CLOSE alone ends the process.
            Assert.True(process.CloseMainWindow(), "premise: Slate had no main window to close.");
            Assert.True(
                process.WaitForExit(15_000),
                "Slate did not exit within 15 s of WM_CLOSE with an unsaved note: a close prompt is up, "
                + "or the close never settled. app log: "
                + ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log")));
            Assert.Equal(onDisk, File.ReadAllText(notePath));
            process.Dispose();
            process = null;

            using var automation = new UIA3Automation();
            // Before the relaunch, on the root: NVDA's shape.
            using DesktopNotificationListener listener = ListenOnTheDesktop(automation, heard);
            process = StartShellProcess(vaultRoot, relaunchLogDirectory);
            string relaunchLog = Path.Combine(relaunchLogDirectory, "slate-windows.log");
            Window window = WaitForMainWindow(process, automation, relaunchLog, TimeSpan.FromSeconds(30));
            AssertTheNoteCameBackDirty(window, automation, typed);

            RenderedAnnouncement opened = SlateUniffiMethods.A11yRender(
                new A11yEvent.VaultOpened(vaultName, string.Empty));
            RenderedAnnouncement restored = SlateUniffiMethods.A11yRender(
                new A11yEvent.UnsavedNotesRestored(1));
            int slateId = process.Id;
            string[] Lines() => [.. HeardFrom(heard, slateId).Select(line => line.DisplayString)];
            Assert.True(
                SpinWait.SpinUntil(() => Lines().Contains(restored.Text), TimeSpan.FromSeconds(15)),
                $"the relaunch never said \"{restored.Text}\". heard: " + string.Join(" | ", Lines()));
            // Late duplicates have the launch window to arrive in.
            Thread.Sleep(TimeSpan.FromSeconds(2));
            string[] lines = Lines();
            Assert.Equal(1, lines.Count(line => line == restored.Text));
            int openedAt = Array.IndexOf(lines, opened.Text);
            Assert.True(
                openedAt >= 0 && openedAt < Array.IndexOf(lines, restored.Text),
                $"\"{restored.Text}\" was not heard after \"{opened.Text}\": " + string.Join(" | ", lines));
            AssertRaisedAsRendered(restored, HeardFrom(heard, slateId).First(line => line.DisplayString == restored.Text));
            Assert.Equal(onDisk, File.ReadAllText(notePath));
        }
        finally
        {
            EndHotExitJourney(process, testRoot);
        }
    }

    /// <summary>
    /// Plan 44 WB-29 (#1525; the spec's Tests table, decision 7): the real
    /// process is killed after the typing has had its debounce. A crash writes
    /// nothing at close, so only the writes made while typing can bring the
    /// note back: relaunching shows it as a dirty tab holding the typed words.
    /// </summary>
    [Fact]
    public void HotExit_AKilledProcessKeepsAnUnsavedNote()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), $"slate-hot-exit-kill-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, $"Hot Exit Kill {Guid.NewGuid():N}"[..23]);
        string logDirectory = Path.Combine(testRoot, "logs");
        string relaunchLogDirectory = Path.Combine(testRoot, "logs-relaunch");
        WriteShellFixtureVault(vaultRoot);
        string notePath = Path.Combine(vaultRoot, "note.md");
        string onDisk = File.ReadAllText(notePath);
        Process? process = null;
        try
        {
            process = StartShellProcess(vaultRoot, logDirectory);
            if (!HasInteractiveDesktop(process, "hot-exit-kill"))
            {
                return;
            }

            string typed = TypeIntoTheNote(process, Path.Combine(logDirectory, "slate-windows.log"));
            // The writer's one-second debounce, with room for the store.
            Thread.Sleep(TimeSpan.FromSeconds(3));
            process.Kill(entireProcessTree: true);
            Assert.True(process.WaitForExit(10_000), "premise: the killed process did not exit.");
            Assert.Equal(onDisk, File.ReadAllText(notePath));
            process.Dispose();
            process = null;

            using var automation = new UIA3Automation();
            process = StartShellProcess(vaultRoot, relaunchLogDirectory);
            Window window = WaitForMainWindow(
                process, automation, Path.Combine(relaunchLogDirectory, "slate-windows.log"), TimeSpan.FromSeconds(30));
            AssertTheNoteCameBackDirty(window, automation, typed);
            Assert.Equal(onDisk, File.ReadAllText(notePath));
        }
        finally
        {
            EndHotExitJourney(process, testRoot);
        }
    }

    /// <summary>Opens <c>note.md</c>, types <see cref="HotExitTyped"/> at
    /// its end, and waits for the tab to read unsaved. Returns the editor's
    /// whole text.</summary>
    private static string TypeIntoTheNote(Process process, string logFile)
    {
        using var automation = new UIA3Automation();
        Window window = WaitForMainWindow(process, automation, logFile, TimeSpan.FromSeconds(30));
        window.SetForeground();
        AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(30));
        WaitForTreeItemStartingWith(tree, automation, "note").Patterns.SelectionItem.Pattern.Select();
        AutomationElement editor = WaitForEditor(window, automation, "note.md editor", TimeSpan.FromSeconds(10));
        FocusTheEditor(window, editor);
        PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
        Keyboard.Type(HotExitTyped);
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
        Assert.True(
            SpinWait.SpinUntil(
                () => EditorText(editor).TrimEnd('\n').EndsWith(HotExitTyped, StringComparison.Ordinal),
                TimeSpan.FromSeconds(10)),
            $"premise: the typed words did not reach the note; it reads '{EditorText(editor)}'.");
        AutomationElement tab = NoteTab(window, automation);
        Assert.True(
            SpinWait.SpinUntil(
                () => tab.Name.EndsWith(", unsaved changes", StringComparison.Ordinal),
                TimeSpan.FromSeconds(10)),
            $"premise: the edited tab did not read unsaved; it reads '{tab.Name}'.");
        return EditorText(editor);
    }

    /// <summary>The relaunch shows <c>note.md</c> as a dirty tab whose
    /// editor holds <paramref name="typed"/>.</summary>
    private static void AssertTheNoteCameBackDirty(Window window, UIA3Automation automation, string typed)
    {
        _ = WaitForElement(window, "WorkspaceView", TimeSpan.FromSeconds(30));
        AutomationElement? tab = null;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    tab = TryNoteTab(window, automation);
                    return tab?.Name.EndsWith(", unsaved changes", StringComparison.Ordinal) == true;
                },
                TimeSpan.FromSeconds(30)),
            $"the relaunch did not restore note.md as a dirty tab; its tab reads '{tab?.Name ?? "(none)"}'.");
        tab!.Patterns.SelectionItem.Pattern.Select();
        AutomationElement editor = WaitForEditor(window, automation, "note.md editor", TimeSpan.FromSeconds(10));
        Assert.True(
            SpinWait.SpinUntil(() => EditorText(editor) == typed, TimeSpan.FromSeconds(10)),
            $"the restored tab does not hold the typed text; it reads '{EditorText(editor)}'.");
    }

    private static AutomationElement NoteTab(Window window, UIA3Automation automation)
    {
        AutomationElement? tab = null;
        Assert.True(
            SpinWait.SpinUntil(() => (tab = TryNoteTab(window, automation)) is not null, TimeSpan.FromSeconds(10)),
            "note.md has no workspace tab.");
        return tab!;
    }

    private static AutomationElement? TryNoteTab(Window window, UIA3Automation automation)
    {
        try
        {
            AutomationElement? tabs = window.FindFirstDescendant(
                automation.ConditionFactory.ByAutomationId("WorkspaceTabs"));
            return tabs?.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.TabItem))
                .FirstOrDefault(item => item.Name.StartsWith("note", StringComparison.OrdinalIgnoreCase));
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static void EndHotExitJourney(Process? process, string testRoot)
    {
        if (process is not null)
        {
            StopProcess(process);
            process.Dispose();
        }

        try { Directory.Delete(testRoot, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
