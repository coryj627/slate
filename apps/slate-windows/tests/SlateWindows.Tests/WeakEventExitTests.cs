// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;

namespace SlateWindows.Tests;

/// <summary>
/// #1555: xunit v3 runs this suite in a process of its own and fails the run
/// when that process is still alive ten seconds after the last fact
/// ("Foreground threads were left running"). Many facts leave WPF weak-event
/// tables on STA threads that ended without shutting their dispatcher down.
/// At exit, WPF's shutdown listener Invokes each table's clean-up on its
/// dispatcher and waits up to 300 ms for it (<c>WeakEventTable.OnShutDown</c>,
/// dotnet/wpf release/10.0); about 1,270 tables made the exit last over six
/// minutes. The test project sets WPF's own switch for this in its runtime
/// config, so a process that is ending skips that clean-up. The shipped app
/// does not set it.
/// </summary>
public sealed class WeakEventExitTests
{
    private const string Switch = "Switch.MS.Internal.DoNotInvokeInWeakEventTableShutdownListener";
    private const int Tables = 10;

    [Fact]
    public void TheTestProcessSkipsWpfsWeakEventCleanUpAtExit()
    {
        Assert.True(
            AppContext.TryGetSwitch(Switch, out bool enabled) && enabled,
            $"{Switch} is off: SlateWindows.Tests.csproj's RuntimeHostConfigurationOption no longer reaches the test process");
    }

    [Fact]
    public void LeftoverWeakEventTablesDelayAProcessExitOnlyWithoutTheSwitch()
    {
        TimeSpan without = ExitAfterReady(withSwitch: false);
        TimeSpan with = ExitAfterReady(withSwitch: true);

        Assert.True(
            without >= TimeSpan.FromMilliseconds(Tables * 300 * 0.8),
            $"{Tables} leftover tables cost {without.TotalMilliseconds:F0} ms at exit without the switch: WPF no longer waits for each, so this fact no longer shows the switch matters");
        Assert.True(
            with < TimeSpan.FromMilliseconds(Tables * 300 / 3),
            $"the exit took {with.TotalMilliseconds:F0} ms with the switch set: WPF no longer honours {Switch}");
    }

    /// <summary>From the probe's "ready" (its tables made) to its exit.</summary>
    private static TimeSpan ExitAfterReady(bool withSwitch)
    {
        string probe = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "tools", "WeakEventExitProbe", "bin", BuildConfiguration(), "net10.0-windows", "WeakEventExitProbe.dll"));
        Assert.True(File.Exists(probe), $"The declared WeakEventExitProbe project dependency did not build {probe}");

        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(probe);
        start.ArgumentList.Add(Tables.ToString(CultureInfo.InvariantCulture));
        if (withSwitch)
        {
            start.ArgumentList.Add("--switch");
        }
        using Process process = Process.Start(start)!;
        Task<string> error = process.StandardError.ReadToEndAsync();
        string? said = process.StandardOutput.ReadLine();
        var exit = Stopwatch.StartNew();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            Assert.Fail("the probe ran out its minute");
        }
        exit.Stop();
        string errorText = error.Result;
        Assert.True(
            said == "ready" && process.ExitCode == 0,
            $"the probe failed (exit {process.ExitCode}, said '{said}'): {errorText[..Math.Min(errorText.Length, 400)]}");
        return exit.Elapsed;
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
