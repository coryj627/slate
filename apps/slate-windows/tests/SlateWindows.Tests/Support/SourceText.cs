// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// Where the Windows shell's source lives, for the drift twins.
/// </summary>
internal static class SourceText
{
    /// <summary>The shell's source directory.</summary>
    /// <remarks>
    /// Three test classes each carry a private copy of this walk. This is
    /// the shared one; consolidating the rest is tracked separately rather
    /// than folded into a review-response commit.
    /// </remarks>
    internal static string ShellSourceRoot() =>
        System.IO.Path.Combine(
            RepoRoot(), "apps", "slate-windows", "src", "SlateWindows");

    private static string RepoRoot()
    {
        var directory = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (directory is not null
            && !System.IO.File.Exists(System.IO.Path.Combine(directory.FullName, "Cargo.toml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new System.InvalidOperationException("repository root not found");
    }

}
