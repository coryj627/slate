// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

/// <summary>DS-15 (#1816): a note has a header with its place and its mode.</summary>
public sealed partial class ShellAccessibilityTests
{
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetDpiForWindow")]
    private static extern uint NoteHeaderDpiForWindow(IntPtr window);

    /// <summary>Every stop in the bar has a name that isn't a glyph; Tab meets each folder,
    /// the switch's checked option once, and ⋯, in that order; an arrow moves the choice and
    /// keeps the keys on the switch; Ctrl+Shift+E still lands in the note.</summary>
    [Fact]
    public void NoteHeader_NamesKeysAndAxe_AreClean()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-note-header-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string device = Path.Combine(root, "device");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(Path.Combine(vault, "learning", "maths"));
        Directory.CreateDirectory(device);
        File.WriteAllText(Path.Combine(vault, "learning", "maths", "Lecture.md"), "# Lecture\n\nA line.\n");
        Process? process = null;
        try
        {
            using var automation = new UIA3Automation();
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(start);
            start.Environment[ShellLaunch.Variable] = device;
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "note-header-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            start.ArgumentList.Add(vault);
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "note-header")) { return; }
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            // At the app's default size with both panes the editor is about 520 wide, where the
            // bar goes compact; this pass is the full bar's. The pattern sizes in pixels, so
            // 1500 by 820 effective pixels is scaled by the window's DPI.
            double scale = NoteHeaderDpiForWindow(window.Properties.NativeWindowHandle.Value) / 96.0;
            window.Patterns.Transform.Pattern.Resize(1500 * scale, 820 * scale);
            AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(30));
            // A window can't be wider than its screen, and a CI VM's is about 1044: there the
            // right pane goes aside (Ctrl+Alt+I, with the keys in Files) so the bar is full.
            if (window.BoundingRectangle.Width < 1400 * scale)
            {
                tree.Focus();
                PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
                Retry.WhileTrue(
                    () => window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("RightPaneLeaves")) is { IsOffscreen: false },
                    TimeSpan.FromSeconds(5));
            }

            AutomationElement folder = WaitForTreeItemStartingWith(tree, automation, "learning");
            folder.Patterns.ExpandCollapse.Pattern.Expand();
            AutomationElement maths = WaitForTreeItemStartingWith(tree, automation, "maths");
            maths.Patterns.ExpandCollapse.Pattern.Expand();
            WaitForTreeItemStartingWith(tree, automation, "Lecture.md").Focus();
            PressKey(VirtualKeyShort.RETURN);

            AutomationElement source = WaitForElement(window, "NoteHeaderSource", TimeSpan.FromSeconds(15));
            AutomationElement reading = WaitForElement(window, "NoteHeaderReading", TimeSpan.FromSeconds(5));
            AutomationElement more = WaitForElement(window, "NoteHeaderMore", TimeSpan.FromSeconds(5));
            AutomationElement[] segments = [];
            Retry.WhileFalse(
                () => (segments = window.FindAllDescendants(automation.ConditionFactory.ByAutomationId("NoteHeaderFolder"))).Length == 2,
                TimeSpan.FromSeconds(10));
            Assert.True(
                segments.Length == 2,
                $"the bar shows {segments.Length} folders in a window at {window.BoundingRectangle}; "
                + $"compact (\"Folders\" shown): {window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("NoteHeaderFolders")) is not null}; "
                + $"the foreground window: {automation.FocusedElement()?.Properties.ProcessId.ValueOrDefault} (ours {process.Id}).");
            Assert.Equal(["learning", "maths"], segments.Select(segment => segment.Name));
            foreach (AutomationElement stop in segments.Append(source).Append(reading).Append(more))
            {
                Assert.False(string.IsNullOrWhiteSpace(stop.Name), $"{stop.AutomationId} has no name.");
                Assert.DoesNotContain(stop.Name, character => character is >= '\uE000' and <= '\uF8FF');
            }

            Assert.Equal(ControlType.RadioButton, source.ControlType);
            Assert.Equal("More note actions", more.Name);
            Assert.True(source.Patterns.SelectionItem.Pattern.IsSelected.Value);

            AssertAxeClean(process, "note-header");

            // Tab: each folder, the switch's checked option, then ⋯.
            segments[0].Focus();
            AssertEventuallyFocused(segments[0], "the first folder didn't take the keys.");
            PressKey(VirtualKeyShort.TAB);
            AssertEventuallyFocused(segments[1], "Tab didn't reach the second folder.");
            PressKey(VirtualKeyShort.TAB);
            AssertEventuallyFocused(source, "Tab didn't reach the checked option.");
            PressKey(VirtualKeyShort.TAB);
            AssertEventuallyFocused(more, "Tab went to the unchecked option, not ⋯.");

            // An arrow moves the choice and keeps the keys on the switch.
            source.Focus();
            AssertEventuallyFocused(source, "the switch didn't take the keys.");
            PressKey(VirtualKeyShort.RIGHT);
            Retry.WhileFalse(() => reading.Patterns.SelectionItem.Pattern.IsSelected.Value, TimeSpan.FromSeconds(5));
            Assert.True(reading.Patterns.SelectionItem.Pattern.IsSelected.Value, "Right didn't choose Reading.");
            AssertEventuallyFocused(reading, "the arrow took the keys off the switch.");

            // Ctrl+Shift+E lands in the note, and the switch follows.
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_E);
            Retry.WhileFalse(() => source.Patterns.SelectionItem.Pattern.IsSelected.Value, TimeSpan.FromSeconds(5));
            Assert.True(source.Patterns.SelectionItem.Pattern.IsSelected.Value, "Ctrl+Shift+E didn't go back to Source.");
            AssertEventuallyFocused(WaitForElement(window, "MarkdownEditor", TimeSpan.FromSeconds(5)), "Ctrl+Shift+E didn't land in the note.");
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            process?.WaitForExit(10_000);
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }
}
