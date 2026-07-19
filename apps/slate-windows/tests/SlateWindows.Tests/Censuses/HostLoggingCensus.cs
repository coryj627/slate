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

namespace SlateWindows.Tests.Censuses;

[Trait("census", "host-logging")]
public class HostLoggingCensus
{
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
}
