// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;

namespace SlateWindows;

/// <summary>
/// Converts ordinary long absolute paths at the CreateFileW boundary.
/// A longPathAware manifest alone is insufficient when LongPathsEnabled is off;
/// test hosts also have their own manifests. Every device or extended spelling
/// .NET recognizes (<c>\\?\</c>, <c>\??\</c>, <c>\\.\</c> and their
/// forward-slash forms) and every path shorter than MAX_PATH is returned as
/// given. Callers retain responsibility for path validation, identity checks,
/// and reparse-point policy.
/// </summary>
internal static class WindowsNativePath
{
    public static string ForCreateFile(string path)
    {
        if (path.Length < 260
            || IsDevice(path)
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

    /// <summary>
    /// .NET's <c>PathInternal.IsDevice</c>: an extended path (<c>\\?\</c> or
    /// the NT <c>\??\</c>), or two separators, then '.' or '?', then a
    /// separator. A hand-edited canvas card can name <c>\??\C:\…</c>; the
    /// prefix test missed it and produced <c>\\?\\??\C:\…</c>.
    /// </summary>
    private static bool IsDevice(string path) =>
        path.Length >= 4
        && ((path[0] == '\\' && (path[1] is '\\' or '?') && path[2] == '?' && path[3] == '\\')
            || (IsSeparator(path[0]) && IsSeparator(path[1]) && (path[2] is '.' or '?')
                && IsSeparator(path[3])));

    private static bool IsSeparator(char character) => character is '\\' or '/';
}
