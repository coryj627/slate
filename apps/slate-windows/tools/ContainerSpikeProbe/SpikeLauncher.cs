// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace ContainerSpikeProbe;

/// Starting and attaching to one spike variant, shared by the UIA probe
/// and the NVDA probe so both measure the same process in the same state.
internal static class SpikeLauncher
{
    public static Process Start(string variant)
    {
        var startInfo = new ProcessStartInfo(SpikeExe()) { UseShellExecute = false };
        startInfo.ArgumentList.Add("--container");
        startInfo.ArgumentList.Add(variant);
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("ContainerSpike.exe did not start.");
    }

    public static Window WaitForWindow(
        Process process, UIA3Automation automation, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"ContainerSpike exited early with code {process.ExitCode}.");
            }
            try
            {
                var app = FlaUI.Core.Application.Attach(process.Id);
                Window? window = app.GetAllTopLevelWindows(automation).FirstOrDefault();
                if (window is not null && !string.IsNullOrEmpty(window.Title))
                {
                    return window;
                }
            }
            catch
            {
                // Not up yet; keep waiting until the deadline.
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException(
            "ContainerSpike window never appeared. If this process is not on the same "
            + "desktop as the app (a session-0 shell against a logged-in desktop), UIA "
            + "cannot see it and no timeout will help.");
    }

    public static void TryKill(Process? process)
    {
        if (process is null || process.HasExited)
        {
            return;
        }
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
        }
        catch
        {
            // Best effort — a leaked spike window is not worth failing the run.
        }
    }

    private static string SpikeExe()
    {
        string exe = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "ContainerSpike", "bin",
            Configuration(), "net10.0-windows", "ContainerSpike.exe"));
        if (!File.Exists(exe))
        {
            throw new FileNotFoundException($"ContainerSpike.exe not built at {exe}.");
        }
        return exe;
    }

    private static string Configuration() =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif
}
