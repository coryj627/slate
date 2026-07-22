// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Host-logging census (w0_spec §W0-3 item 4, #715): the C# host installs
// the `host_logging` sink and non-fatal core diagnostics surface in the
// app log. slate-core's sink writes to native fd 2, which in-process
// stderr redirection cannot see — so the census runs the HostLogProbe
// child process and asserts on its captured stderr, the same channel the
// app log reads.
//
// Scope note: stderr IS the current cross-platform host-log contract —
// host_logging.rs is a stderr sink by design, with per-platform durable
// bridges (macOS os_log and any Windows equivalent) explicitly deferred
// (#507). The WPF app installs the same sink at startup (App.OnStartup);
// wiring stderr into a durable app-log destination for the WinExe is
// W1-1 app-shell scope, not W0-3's.

using System.Diagnostics;
using System.Text.Json;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "host-logging")]
public class HostLoggingCensus
{
    [Fact]
    public void ProductionWinExe_SecondLaunchForwardsActivationToPrimary()
    {
        string exe = SlateWindowsExe();
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"slate-single-instance-census-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string output = Path.Combine(directory, "activation.json");
        string vaultPath = Path.Combine(directory, "Forwarded Vault");
        Directory.CreateDirectory(vaultPath);
        string identity = $"slate-census-{Guid.NewGuid():N}";

        try
        {
            var primaryInfo = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
            };
            primaryInfo.ArgumentList.Add("--census-single-instance-primary");
            primaryInfo.ArgumentList.Add(output);
            primaryInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] = identity;
            primaryInfo.Environment["SLATE_LOG_DIR"] = Path.Combine(directory, "logs");
            using var primary = Process.Start(primaryInfo)!;

            Assert.True(
                SpinWait.SpinUntil(() => File.Exists(output + ".ready"), 30_000),
                "primary instance did not start its activation listener");

            var secondaryInfo = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
            };
            secondaryInfo.ArgumentList.Add(vaultPath);
            secondaryInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] = identity;
            secondaryInfo.Environment["SLATE_LOG_DIR"] = Path.Combine(directory, "logs");
            using var secondary = Process.Start(secondaryInfo)!;
            Assert.True(secondary.WaitForExit(30_000), "secondary Slate instance did not exit");
            Assert.Equal(0, secondary.ExitCode);

            Assert.True(primary.WaitForExit(30_000), "primary Slate census did not exit");
            Assert.Equal(0, primary.ExitCode);
            string[] forwarded = JsonSerializer.Deserialize<string[]>(File.ReadAllBytes(output))
                ?? throw new Xunit.Sdk.XunitException("activation payload decoded as null");
            Assert.Equal([vaultPath], forwarded);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void ProductionWinExeStartupPath_AllThemeDictionariesResolve()
    {
        string exe = SlateWindowsExe();
        string logDir = Path.Combine(Path.GetTempPath(), $"slate-theme-census-{Guid.NewGuid():N}");
        try
        {
            var psi = new ProcessStartInfo(exe, "--census-theme-probe")
            {
                UseShellExecute = false,
            };
            psi.Environment["SLATE_LOG_DIR"] = logDir;
            using var app = Process.Start(psi)!;
            Assert.True(app.WaitForExit(60_000), "SlateWindows.exe theme probe did not exit");
            Assert.Equal(0, app.ExitCode);
        }
        finally
        {
            try
            {
                Directory.Delete(logDir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void ProductionWinExeStartupPath_DiagnosticReachesTheAppLog()
    {
        // The real SlateWindows.exe, launched with --census-log-probe:
        // OnStartup redirects native stderr into the app log (SLATE_LOG_DIR
        // here), installs the sink, triggers the deterministic warn, and
        // exits before any window shows — the exact production startup
        // path, windowless so it runs on CI.
        string exe = SlateWindowsExe();

        string logDir = Path.Combine(Path.GetTempPath(), $"slate-log-census-{Guid.NewGuid():N}");
        try
        {
            var psi = new ProcessStartInfo(exe, "--census-log-probe")
            {
                UseShellExecute = false,
            };
            psi.Environment["SLATE_LOG_DIR"] = logDir;
            using var app = Process.Start(psi)!;
            Assert.True(app.WaitForExit(60_000), "SlateWindows.exe log probe did not exit");
            Assert.Equal(0, app.ExitCode);

            string logFile = Path.Combine(logDir, "slate-windows.log");
            Assert.True(File.Exists(logFile), "app log file was not created by the WinExe startup path");
            string log = File.ReadAllText(logFile);
            Assert.Contains("slate[warn]", log);
            Assert.Contains("palette recents input exceeds", log);
        }
        finally
        {
            try
            {
                Directory.Delete(logDir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void NonFatalCoreDiagnostic_SurfacesThroughTheInstalledSink()
    {
        string probeDll = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "tools", "HostLogProbe", "bin", BuildConfiguration(), "net10.0", "HostLogProbe.dll");
        probeDll = Path.GetFullPath(probeDll);
        Assert.True(File.Exists(probeDll), $"HostLogProbe not built at {probeDll} (build the solution first)");

        var psi = new ProcessStartInfo("dotnet", $"\"{probeDll}\"")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var probe = Process.Start(psi)!;
        string stderr = probe.StandardError.ReadToEnd();
        string stdout = probe.StandardOutput.ReadToEnd();
        Assert.True(probe.WaitForExit(30_000), "HostLogProbe did not exit");

        Assert.True(probe.ExitCode == 0, $"probe failed: {stdout}\n{stderr}");
        // The sink's format is "slate[warn] <target>: <message>"
        // (crates/slate-uniffi/src/host_logging.rs). The oversized recents
        // input is the deterministic warn trigger.
        Assert.Contains("slate[warn]", stderr);
        Assert.Contains("palette recents input exceeds", stderr);
    }

    private static string BuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
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
}
