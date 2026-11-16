// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>
    /// #1331: a shell-gate launch keeps its device state under the run's own
    /// root (<see cref="ShellLaunch.Root"/>), never the developer's profile.
    /// The shell records the vault it opened in <c>recent-vaults.json</c>;
    /// after the launch, the root's copy names this journey's own vault,
    /// whose folder name no other launch uses.
    /// </summary>
    [Fact]
    public void ShellGate_WritesItsRecentsUnderTheRunsOwnRoot()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(),
            $"slate-device-state-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, $"Device State Vault {Guid.NewGuid():N}"[..31]);
        string logDirectory = Path.Combine(testRoot, "logs");
        WriteShellFixtureVault(vaultRoot);
        string recents = Path.Combine(ShellLaunch.Root, "recent-vaults.json");

        Process? process = null;
        try
        {
            process = StartShellProcess(vaultRoot, logDirectory);
            if (Environment.UserInteractive)
            {
                using var automation = new UIA3Automation();
                Window window = WaitForMainWindow(
                    process,
                    automation,
                    Path.Combine(logDirectory, "slate-windows.log"),
                    TimeSpan.FromSeconds(30));
                _ = WaitForElement(window, "WorkspaceView", TimeSpan.FromSeconds(30));
                // R-4's runtime witness runs in every shell journey
                // (AnnouncementNameCensus).
                AssertItemNamesAreSpeakable(process, "device-state");
            }
            else if (string.Equals(
                Environment.GetEnvironmentVariable("SLATE_REQUIRE_UI_AUTOMATION"),
                "1",
                StringComparison.Ordinal))
            {
                throw new Xunit.Sdk.XunitException(
                    "The shell gate requires an interactive Windows desktop, "
                    + "but this runner is executing in a non-interactive session.");
            }

            // The open records the vault before the workspace is shown, so on
            // a desktop the save has been made by here and the read below is
            // the first to open the file. That order matters: the recents
            // store saves once, and a read open at that instant would refuse
            // the save and lose the record (GateFile). A session-0 sandbox
            // has no window to wait for, so there the paced poll is the wait.
            string expected = Path.GetFullPath(vaultRoot);
            _ = ExpectFileText(
                recents,
                text => RecordsVault(text, expected),
                $"the launch did not record {expected} in {recents} (SLATE_LOCAL_APP_DATA did not reach it).",
                nameof(ShellGate_WritesItsRecentsUnderTheRunsOwnRoot),
                logDirectory,
                TimeSpan.FromSeconds(30));
        }
        catch (Exception)
        {
            PreserveEvidence(nameof(ShellGate_WritesItsRecentsUnderTheRunsOwnRoot), logDirectory);
            throw;
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

        static bool RecordsVault(string recentsJson, string vault)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(recentsJson);
                return document.RootElement.ValueKind == JsonValueKind.Array
                    && document.RootElement.EnumerateArray().Any(entry =>
                        entry.TryGetProperty("path", out JsonElement path)
                        && string.Equals(path.GetString(), vault, StringComparison.OrdinalIgnoreCase));
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
