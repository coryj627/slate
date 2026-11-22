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
    /// <summary>
    /// Plan 47 CB-17b (#1866, CD11; CB-17a's NVDA record, step B4): Enter on Import…
    /// shows its box, "Import Files and Folders", owned by the window. Cancelling the box
    /// gives the keys back to Import…, which stays available while its box is up, and F6
    /// then moves on to the next region. Before CB-17b the button was off for the box's
    /// whole life, so the window came back with the keys on nothing, and F6, Ctrl+O and
    /// every other chord did nothing until Alt. The real box is Win32's: no unit fact
    /// reaches its close.
    /// </summary>
    [Fact]
    public void ImportBox_CancelGivesTheKeysBackToImportAndF6MovesOn()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-import-box-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "alpha.md"), "# Alpha\n\nBody.\n");

        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(start);
            start.ArgumentList.Add(vault);
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "import-box-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "import-box"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            _ = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));

            AutomationElement fileActions = WaitForElement(window, "SidebarFileActions", TimeSpan.FromSeconds(10));
            fileActions.Patterns.ExpandCollapse.Pattern.Expand();
            AutomationElement import = WaitForButton(fileActions, automation, "Import…");
            import.Focus();
            AssertEventuallyFocused(import, "Import… did not take the keys.");

            // Enter presses the button that holds the keys, as a reader does.
            PressKey(VirtualKeyShort.RETURN);
            bool cancelled = SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        Window? box = window.ModalWindows.FirstOrDefault(candidate =>
                            candidate.Title.Contains("Import Files and Folders", StringComparison.Ordinal));
                        AutomationElement? cancel = box?
                            .FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Button))
                            .FirstOrDefault(button => string.Equals(button.Name, "Cancel", StringComparison.Ordinal));
                        if (cancel is null)
                        {
                            return false;
                        }

                        cancel.Patterns.Invoke.Pattern.Invoke();
                        return true;
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception))
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(15));
            Assert.True(cancelled, "Import…'s box, \"Import Files and Folders\", never showed its Cancel.");
            Assert.True(
                SpinWait.SpinUntil(() => window.ModalWindows.Length == 0, TimeSpan.FromSeconds(10)),
                "Import…'s box was still up after its Cancel.");

            AssertEventuallyFocused(import, "the keys did not come back to Import… after its box was cancelled.");
            Assert.True(import.IsEnabled, "Import… was off after its box was cancelled.");

            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(
                WaitForElement(window, "ContentPane", TimeSpan.FromSeconds(10)),
                "F6 from Import… did not move on to the empty editor pane.");
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
                Directory.Delete(root, recursive: true);
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
