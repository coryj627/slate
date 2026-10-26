// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SlateWindows.Tests;

/// <summary>
/// #1331: this test process's device state. Every store the suite builds with
/// its default path — recent vaults, window placement, preferences, palette
/// recents, per-vault file recents, the sidebar write journal — and the host
/// log resolve through <see cref="SlateDeviceState.Root"/>, so the module
/// initializer below points <c>SLATE_LOCAL_APP_DATA</c> at a per-run
/// temporary folder before xUnit runs any fact, and nothing the suite does
/// writes the developer's own <c>%LOCALAPPDATA%\Slate</c>. A run that sets
/// the variable itself keeps its value.
/// </summary>
/// <remarks>
/// The initializer also snapshots the real profile, for
/// <see cref="DeviceStateGuardTests"/>: a fast, partial signal. The
/// whole-run witness is <c>scripts/windows/guard-device-state.ps1</c>, which
/// CI runs every test step under, outside this process.
/// </remarks>
internal static class DeviceStateForTests
{
    /// <summary>This run's device-state root.</summary>
    internal static string Root { get; private set; } = string.Empty;

    /// <summary>A file-recents folder no other construction shares, under
    /// this run's root and outside every vault (a folder inside the vault
    /// would show in its files tree).</summary>
    internal static string FreshFileRecentsRoot() =>
        Path.Combine(Root, "file-recents", Guid.NewGuid().ToString("N"));

    /// <summary>The developer's own Slate folder, whatever the variable
    /// says.</summary>
    internal static string Profile => SlateDeviceState.Resolve(null);

    /// <summary>The real profile as it stood when the assembly
    /// loaded.</summary>
    internal static IReadOnlyDictionary<string, (long Length, long Ticks)> ProfileAtLoad { get; private set; } =
        new Dictionary<string, (long Length, long Ticks)>();

    /// <summary>Runs once, as the test assembly loads, before xUnit runs any
    /// fact.</summary>
    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255", Justification = "The device-state root must be set before any store resolves its default path.")]
    internal static void PointTheDeviceStateAtThisRunsRoot()
    {
        ProfileAtLoad = Snapshot(Profile);
        string? preset = Environment.GetEnvironmentVariable(SlateDeviceState.Variable);
        if (!string.IsNullOrWhiteSpace(preset))
        {
            Root = SlateDeviceState.Resolve(preset);
            return;
        }
        string root = Path.Combine(
            Path.GetTempPath(),
            $"slate-tests-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable(SlateDeviceState.Variable, root);
        Root = root;
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Best effort, like ScratchTrashBin: a leaked temp folder is harmless.
            }
            catch (UnauthorizedAccessException)
            {
            }
        };
    }

    /// <summary>Every file under <paramref name="folder"/> by its relative
    /// path, with its length and last-write time, and every folder, the
    /// folder's own included as "." (a folder's entry is (-1, 0)). A folder
    /// that does not exist is empty, so creating it is a difference, as
    /// <c>guard-device-state.ps1</c> counts it.</summary>
    internal static Dictionary<string, (long Length, long Ticks)> Snapshot(string folder)
    {
        var entries = new Dictionary<string, (long Length, long Ticks)>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(folder))
        {
            return entries;
        }
        entries["."] = (-1, 0);
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = 0,
        };
        foreach (FileSystemInfo entry in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", options))
        {
            string relative = Path.GetRelativePath(folder, entry.FullName);
            if (entry is FileInfo file)
            {
                entries[relative] = (file.Length, file.LastWriteTimeUtc.Ticks);
            }
            else
            {
                entries[relative + Path.DirectorySeparatorChar] = (-1, 0);
            }
        }
        return entries;
    }

    /// <summary>The files created, changed or removed between two
    /// snapshots.</summary>
    internal static List<string> Differences(
        IReadOnlyDictionary<string, (long Length, long Ticks)> before,
        IReadOnlyDictionary<string, (long Length, long Ticks)> after)
    {
        var changes = new List<string>();
        foreach ((string path, (long Length, long Ticks) now) in after)
        {
            if (!before.TryGetValue(path, out (long Length, long Ticks) was))
            {
                changes.Add($"created {path}");
            }
            else if (was != now)
            {
                changes.Add($"changed {path}");
            }
        }
        changes.AddRange(before.Keys.Where(path => !after.ContainsKey(path)).Select(path => $"removed {path}"));
        changes.Sort(StringComparer.OrdinalIgnoreCase);
        return changes;
    }
}
