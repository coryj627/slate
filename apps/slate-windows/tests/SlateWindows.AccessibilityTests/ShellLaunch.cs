// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;

namespace SlateWindows.AccessibilityTests;

/// <summary>
/// #1331: the device state every shell-gate launch writes. The shell keeps
/// its recent vaults, window placement, preferences, palette recents, file
/// recents and write journal under <c>SLATE_LOCAL_APP_DATA</c> when it is
/// set (<c>SlateDeviceState</c>), so each launch carries this run's own root
/// and none writes the developer's <c>%LOCALAPPDATA%\Slate</c>. Every launch
/// in this suite passes its <see cref="ProcessStartInfo"/> through
/// <see cref="ApplyDeviceState"/> before it starts; the unit project's
/// <c>ShellLaunchCensus</c> keeps it so.
/// </summary>
/// <remarks>
/// Made on first use, so a run that launches nothing makes no folder. One
/// root per run, shared by every journey, as the profile was: a journey that
/// relaunches reads what the previous launch wrote. Creating it also sets the
/// variable for this test process, so once it exists a launch that forgot
/// <see cref="ApplyDeviceState"/> inherits the run's root rather than the
/// profile — but not a forgetful launch before the first use. The unit
/// project's <c>ShellLaunchCensus</c> is what keeps every launch explicit.
/// </remarks>
internal static class ShellLaunch
{
    internal const string Variable = "SLATE_LOCAL_APP_DATA";

    private static readonly Lazy<string> RunRoot = new(CreateRoot, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The variable when the run already sets it, else a
    /// <c>%TEMP%\slate-gate-&lt;pid&gt;-&lt;guid&gt;</c> folder made on first
    /// use and removed when the run ends.</summary>
    internal static string Root => RunRoot.Value;

    /// <summary>Points a shell launch's device state at <see cref="Root"/>.</summary>
    internal static void ApplyDeviceState(ProcessStartInfo startInfo) =>
        startInfo.Environment[Variable] = Root;

    private static string CreateRoot()
    {
        string? preset = Environment.GetEnvironmentVariable(Variable);
        if (!string.IsNullOrWhiteSpace(preset))
        {
            string resolved = Path.GetFullPath(preset);
            Environment.SetEnvironmentVariable(Variable, resolved);
            return resolved;
        }
        string root = Path.Combine(
            Path.GetTempPath(),
            $"slate-gate-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable(Variable, root);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Best effort: a launch still closing may hold a file, and a
                // leaked temp folder is harmless.
            }
            catch (UnauthorizedAccessException)
            {
            }
        };
        return root;
    }
}
