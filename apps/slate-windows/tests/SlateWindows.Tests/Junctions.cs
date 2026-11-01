// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 44 WB-28 PR 2: NTFS junctions for the held-root facts — one spelling
/// that names one folder, then another (a retarget), or a second spelling of
/// one folder (an alias). <c>mklink /J</c> needs no privilege.
/// </summary>
internal static class Junctions
{
    internal static void Create(string junction, string target)
    {
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junction}\" \"{target}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using System.Diagnostics.Process mklink = System.Diagnostics.Process.Start(start)
            ?? throw new InvalidOperationException("premise: cmd.exe did not start to run mklink /J");
        Assert.True(mklink.WaitForExit(10_000) && mklink.ExitCode == 0, "premise: mklink /J made the junction");
    }

    /// <summary>The spelling now names <paramref name="target"/>; the folder
    /// it named before is untouched.</summary>
    internal static void Retarget(string junction, string target)
    {
        // Removing a junction removes the link alone.
        Directory.Delete(junction);
        Create(junction, target);
    }

    internal static void Remove(string junction)
    {
        try
        {
            Directory.Delete(junction);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
    }
}
