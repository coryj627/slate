// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

/// <summary>DS-12 (#1813): the title bar is one 48 row with Snap Layouts.</summary>
public sealed partial class ShellAccessibilityTests
{
    /// <summary>Every button in the row has a name that isn't a glyph, a reader meets one set
    /// of captions (DSD13: with the native frame gone, no native Close is left beside Slate's),
    /// and the captions drive the window as UIA's own Window pattern does.</summary>
    [Fact]
    public void TitleBar_ButtonsAreNamedAndWindowPatternsWork()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-title-bar-" + Guid.NewGuid().ToString("N"));
        string device = Path.Combine(root, "device");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(device);
        Process? process = null;
        try
        {
            using var automation = new UIA3Automation();
            process = LaunchWith(device, logs);
            if (!HasInteractiveDesktop(process, "title-bar")) { return; }
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            Assert.Equal("Slate", window.Title);

            foreach (string id in new[] { "TitleBarPalette", "TitleBarSettings", "CaptionMinimize", "CaptionMaximize", "CaptionClose" })
            {
                string name = WaitForElement(window, id, TimeSpan.FromSeconds(10)).Name;
                Assert.False(string.IsNullOrWhiteSpace(name), $"{id} has no name.");
                Assert.DoesNotContain(name, character => character is >= '\uE000' and <= '\uF8FF');
            }

            AssertAxeClean(process, "title-bar");

            AutomationElement[] Buttons(string name) => window
                .FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Button))
                .Where(button => button.Name == name)
                .ToArray();
            Assert.Single(Buttons("Close"));
            Assert.Single(Buttons("Minimize"));
            Assert.Single(Buttons("Maximize"));

            var states = window.Patterns.Window.Pattern;
            AutomationElement middle = WaitForElement(window, "CaptionMaximize", TimeSpan.FromSeconds(5));
            void WaitFor(WindowVisualState state, string middleName)
            {
                Retry.WhileFalse(() => states.WindowVisualState.Value == state && middle.Name == middleName, TimeSpan.FromSeconds(5));
                Assert.Equal(state, states.WindowVisualState.Value);
                Assert.Equal(middleName, middle.Name);
            }

            middle.Patterns.Invoke.Pattern.Invoke();
            WaitFor(WindowVisualState.Maximized, "Restore");
            middle.Patterns.Invoke.Pattern.Invoke();
            WaitFor(WindowVisualState.Normal, "Maximize");

            states.SetWindowVisualState(WindowVisualState.Maximized);
            WaitFor(WindowVisualState.Maximized, "Restore");
            states.SetWindowVisualState(WindowVisualState.Normal);
            WaitFor(WindowVisualState.Normal, "Maximize");

            WaitForElement(window, "CaptionMinimize", TimeSpan.FromSeconds(5)).Patterns.Invoke.Pattern.Invoke();
            Retry.WhileFalse(() => states.WindowVisualState.Value == WindowVisualState.Minimized, TimeSpan.FromSeconds(5));
            Assert.Equal(WindowVisualState.Minimized, states.WindowVisualState.Value);
            states.SetWindowVisualState(WindowVisualState.Normal);
            WaitFor(WindowVisualState.Normal, "Maximize");

            WaitForElement(window, "CaptionClose", TimeSpan.FromSeconds(5)).Patterns.Invoke.Pattern.Invoke();
            Assert.True(process.WaitForExit(15_000), "Close didn't close the window.");
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            process?.WaitForExit(10_000);
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }
}
