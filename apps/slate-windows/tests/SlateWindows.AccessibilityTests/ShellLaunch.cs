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

    /// <summary>DS-5: the text scale a launch reads in place of the machine's Text size.
    /// The app honours it only under a census instance.</summary>
    internal const string TextScaleVariable = "SLATE_TEXT_SCALE_FOR_TESTS";

    private static readonly Lazy<string> RunRoot = new(CreateRoot, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The variable when the run already sets it, else a
    /// <c>%TEMP%\slate-gate-&lt;pid&gt;-&lt;guid&gt;\device</c> folder made on
    /// first use and removed, with its run folder, when the run ends.</summary>
    internal static string Root => RunRoot.Value;

    /// <summary>Points a shell launch's device state at <see cref="Root"/>, whose accent is
    /// gold, and its text scale at 1, so the gate doesn't depend on the machine's settings. A
    /// journey that needs another accent launches with a device folder of its own.</summary>
    internal static void ApplyDeviceState(ProcessStartInfo startInfo)
    {
        startInfo.Environment[Variable] = Root;
        startInfo.Environment[TextScaleVariable] = "1";
    }

    private static string CreateRoot()
    {
        string? preset = Environment.GetEnvironmentVariable(Variable);
        if (!string.IsNullOrWhiteSpace(preset))
        {
            string resolved = Path.GetFullPath(preset);
            Environment.SetEnvironmentVariable(Variable, resolved);
            StartGold(resolved);
            return resolved;
        }
        string run = Path.Combine(
            Path.GetTempPath(),
            $"slate-gate-{Environment.ProcessId}-{Guid.NewGuid():N}");
        // WB-29: the device root sits inside the run's own folder. Hot exit
        // flushes the root's parent before it keeps anything (§1), and Windows
        // refuses a flush of %TEMP% itself (access denied) on some desktops,
        // which would leave every launch with no hot exit.
        string root = Path.Combine(run, "device");
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable(Variable, root);
        StartGold(root);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Directory.Delete(run, recursive: true);
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

    /// <summary>DS-11: Automatic follows an accent the user chose in Windows (DSD16), so on a
    /// desktop with one, a journey that measures gold would see that colour instead. Only a
    /// folder with no preferences yet is seeded, so nothing a launch wrote is lost.</summary>
    private static void StartGold(string root)
    {
        Directory.CreateDirectory(root);
        string preferences = Path.Combine(root, "preferences.json");
        if (!File.Exists(preferences))
        {
            File.WriteAllText(preferences, "{\"accentColor\":\"gold\"}");
        }
    }
}
