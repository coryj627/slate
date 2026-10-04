// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;

namespace SlateWindows;

/// <summary>
/// Converts ordinary long absolute paths at the CreateFileW boundary.
/// A longPathAware manifest alone is insufficient when LongPathsEnabled is off;
/// test hosts also have their own manifests. Existing device/extended spellings
/// and short DOS normalization remain unchanged. Callers retain responsibility
/// for path validation, identity checks, and reparse-point policy.
/// </summary>
internal static class WindowsNativePath
{
    public static string ForCreateFile(string path)
    {
        if (path.Length < 260
            || path.StartsWith(@"\\?\", StringComparison.Ordinal)
            || path.StartsWith(@"\\.\", StringComparison.Ordinal)
            || !Path.IsPathFullyQualified(path))
        {
            return path;
        }

        // The extended prefix disables Win32 slash/dot-segment normalization.
        string absolute = Path.GetFullPath(path);
        return absolute.StartsWith(@"\\", StringComparison.Ordinal)
            ? @"\\?\UNC\" + absolute[2..]
            : @"\\?\" + absolute;
    }
}
