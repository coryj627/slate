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
    /// <summary>DS-4: a row's 40 pitch makes Workspace taller than a small display, so its
    /// last rows are reached by scrolling. This is launched because a hosted popup can't
    /// take focus. Arrowing to a row that stays off screen would leave a reader lost, and
    /// a menu that slides over its own bar item hides that item and its neighbours.</summary>
    [Fact]
    public void Menus_ALongMenuScrollsToItsLastRow()
    {
        const string lastRow = "Refresh Sync Diagnostics";
        string root = Path.Combine(Path.GetTempPath(), "slate-long-menu-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "alpha.md"), "# Alpha\n");

        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(start);
            start.ArgumentList.Add(vault);
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "long-menu-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "long-menu"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(30)).Focus();

            // Halfway down the display, where the menu fits neither above nor below.
            System.Drawing.Rectangle display = automation.GetDesktop().BoundingRectangle;
            window.Patterns.Transform.Pattern.Move(window.BoundingRectangle.X, display.Top + (display.Height / 2) - 60);
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));
            window.SetForeground();

            AutomationElement opener = WaitForElement(window, "WorkspaceMenu", TimeSpan.FromSeconds(10));
            opener.Patterns.ExpandCollapse.Pattern.Expand();
            System.Drawing.Rectangle openerBounds = opener.BoundingRectangle;

            AutomationElement? row = null;
            for (int press = 0; press < 40; press++)
            {
                PressKey(VirtualKeyShort.DOWN);
                Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(150));
                row = automation.FocusedElement();
                System.Drawing.Rectangle shown = row?.BoundingRectangle ?? default;
                Assert.False(
                    shown.IntersectsWith(openerBounds),
                    $"{row?.Properties.Name.ValueOrDefault} at {shown} covers its menu's item at {openerBounds}.");
                if (row?.Properties.Name.ValueOrDefault == lastRow)
                {
                    break;
                }
            }

            Assert.True(
                row?.Properties.Name.ValueOrDefault == lastRow,
                $"Down never reached {lastRow}; focus is {DescribeFocusedElement(automation)}");
            System.Drawing.Rectangle bounds = row!.BoundingRectangle;
            System.Drawing.Rectangle screen = automation.GetDesktop().BoundingRectangle;
            Assert.False(row.IsOffscreen, $"{lastRow} took focus off screen, at {bounds}.");
            Assert.True(
                bounds.Top >= screen.Top && bounds.Bottom <= screen.Bottom,
                $"{lastRow} is at {bounds} on a display of {screen}, so it wasn't scrolled into view.");
            PressKey(VirtualKeyShort.ESCAPE);
        }
        finally
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }
}
