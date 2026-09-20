// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>
    /// #1230 §W-C journey: a read-only <c>.slate/sidebar.json</c> stands
    /// in the Files pane as a notice with a keyboard-reachable Retry
    /// (Invoke). Retry while the file is still broken keeps the notice
    /// and reports "still use defaults" with the reason; repairing the
    /// file on disk and retrying again removes the notice and reports
    /// the reload — the same two sentences core renders for the
    /// announcements the unit facts observe.
    /// </summary>
    [Fact]
    [Trait("gate", "W-C")]
    public void SidebarSettings_RetryRecoversAReadOnlyFileReachably()
    {
        const string malformedReason = "Sidebar settings are malformed and are read-only.";
        string testRoot = Path.Combine(
            Path.GetTempPath(),
            $"slate-sidebar-settings-retry-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Settings Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        string settingsPath = Path.Combine(vaultRoot, ".slate", "sidebar.json");
        Directory.CreateDirectory(Path.Combine(vaultRoot, ".slate"));
        File.WriteAllText(Path.Combine(vaultRoot, "note.md"), "# Note\n");
        // A known section with an unrecognized shape: read-only, not
        // silently replaced.
        File.WriteAllText(settingsPath, "{\"version\":1,\"sort\":\"bogus\"}");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-sidebar-settings-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "accessibility"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            AutomationElement filesPane = WaitForElement(
                window, "FilesPane", TimeSpan.FromSeconds(30));

            // The notice carries the store's reason verbatim, and the
            // Retry beside it is a real button a keyboard user can reach.
            AutomationElement notice = WaitForElement(
                window, "SidebarSettingsNotice", TimeSpan.FromSeconds(20));
            Assert.Equal(ControlType.Text, notice.ControlType);
            Assert.Equal(malformedReason, notice.Name);
            AutomationElement retry = WaitForElement(
                window, "SidebarSettingsRetry", TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.Button, retry.ControlType);
            Assert.Equal("Retry sidebar settings", retry.Name);
            Assert.Equal(
                "Reads .slate/sidebar.json again after you repair it.",
                retry.HelpText);
            Assert.True(retry.Patterns.Invoke.IsSupported, "Retry does not expose Invoke.");
            Assert.True(retry.IsEnabled, "Retry is disabled while the notice stands.");
            Assert.True(
                retry.Properties.IsKeyboardFocusable.ValueOrDefault,
                "Retry is not reachable from the keyboard.");

            // Still broken: the notice stays, Retry stays live, and the
            // pane's status carries core's StillDefaults sentence with
            // the reason as its detail.
            retry.Patterns.Invoke.Pattern.Invoke();
            string stillDefaults = $"Sidebar settings still use defaults. {malformedReason}";
            Assert.True(
                SpinWait.SpinUntil(
                    () => PaneCarriesText(filesPane, stillDefaults),
                    TimeSpan.FromSeconds(10)),
                "a retry against the still-broken file did not report StillDefaults.");
            Assert.NotNull(window.FindFirstDescendant(
                automation.ConditionFactory.ByAutomationId("SidebarSettingsNotice")));
            Assert.True(retry.IsEnabled, "Retry went dark after a still-blocked retry.");

            // Repaired on disk: the second retry adopts the file, the
            // notice (and its button) leave the tree, and the status
            // carries core's Reloaded sentence.
            File.WriteAllText(settingsPath, "{\"version\":1}");
            retry.Patterns.Invoke.Pattern.Invoke();
            Assert.True(
                SpinWait.SpinUntil(
                    () => PaneCarriesText(filesPane, "Sidebar settings reloaded."),
                    TimeSpan.FromSeconds(10)),
                "a retry against the repaired file did not report Reloaded.");
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindFirstDescendant(
                        automation.ConditionFactory.ByAutomationId("SidebarSettingsNotice")) is null
                        && window.FindFirstDescendant(
                            automation.ConditionFactory.ByAutomationId("SidebarSettingsRetry")) is null,
                    TimeSpan.FromSeconds(10)),
                "the notice or its Retry stayed in the tree after a successful retry.");

            AssertAxeClean(process, "sidebar-settings-retry");
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

    /// <summary>The sidebar's status TextBlock has no id of its own; it
    /// is the one text element in the pane whose Name is the sentence.
    /// Tolerant of rows re-realising under the poll (the W7-3 lesson).</summary>
    private static bool PaneCarriesText(AutomationElement pane, string text)
    {
        try
        {
            return pane.FindAllDescendants().Any(node =>
                string.Equals(node.Properties.Name.ValueOrDefault, text, StringComparison.Ordinal));
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
            return false;
        }
    }
}
