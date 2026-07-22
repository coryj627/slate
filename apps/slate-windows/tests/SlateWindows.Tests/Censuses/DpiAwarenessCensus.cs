// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "dpi-awareness")]
public sealed class DpiAwarenessCensus
{
    [Fact]
    public void ProductionWinExeStartsPerMonitorV2Aware()
    {
        string exe = SlateWindowsExe();
        string logDirectory = Path.Combine(
            Path.GetTempPath(),
            $"slate-dpi-census-{Guid.NewGuid():N}");

        try
        {
            var startInfo = new ProcessStartInfo(exe, "--census-dpi-probe")
            {
                UseShellExecute = false,
            };
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            using var app = Process.Start(startInfo)!;
            Assert.True(app.WaitForExit(60_000), "SlateWindows.exe DPI probe did not exit");
            string logPath = Path.Combine(logDirectory, "slate-windows.log");
            string diagnostic = File.Exists(logPath) ? File.ReadAllText(logPath) : "no app log";
            Assert.True(app.ExitCode == 0, $"DPI probe failed: {diagnostic}");
        }
        finally
        {
            try
            {
                Directory.Delete(logDirectory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static string SlateWindowsExe()
    {
        string exe = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "SlateWindows", "bin", BuildConfiguration(), "net10.0-windows", "SlateWindows.exe");
        exe = Path.GetFullPath(exe);
        Assert.True(File.Exists(exe), $"SlateWindows.exe not built at {exe} (build the solution first)");
        return exe;
    }

    private static string BuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }
}
