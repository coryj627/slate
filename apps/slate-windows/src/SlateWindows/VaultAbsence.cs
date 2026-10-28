// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;

namespace SlateWindows;

/// <summary>
/// WB-12 (#1505): proof that vault entries are gone, for the stale-pin
/// prune. <see cref="File.Exists(string)"/> and
/// <see cref="Directory.Exists(string)"/> answer false on access and I/O
/// errors too, so they cannot prove absence. Here a name is absent only
/// when its folder's listing succeeded without it, and every failure, every
/// reparse point on the way and every unexpected shape proves nothing.
/// </summary>
internal static class VaultAbsence
{
    /// <summary>The candidates among <paramref name="relatives"/> — direct
    /// children of the vault-relative <paramref name="folder"/> ("" is the
    /// root) — that are definitely absent. One walk of the folder's
    /// components from the root down, never statting a candidate leaf, and at
    /// most one listing; any exception or reparse point answers the empty
    /// set.</summary>
    /// <param name="getAttributes">The stat each component takes (a fact
    /// records the paths it was asked for).</param>
    internal static IReadOnlySet<string> AbsentAmong(
        string vaultRoot,
        string folder,
        IReadOnlyCollection<string> relatives,
        Func<string, FileAttributes>? getAttributes = null)
    {
        getAttributes ??= File.GetAttributes;
        var candidates = relatives
            .Where(relative => string.Equals(ParentOf(relative), folder, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        if (candidates.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            string current = Path.GetFullPath(vaultRoot);
            if ((getAttributes(current) & FileAttributes.Directory) == 0)
            {
                return Nothing();
            }

            foreach (string component in folder.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                string next = Path.Combine(current, component);
                FileAttributes attributes;
                try
                {
                    attributes = getAttributes(next);
                }
                catch (Exception missing) when (missing is FileNotFoundException or DirectoryNotFoundException)
                {
                    // The folder itself is gone: every candidate under it is
                    // absent, but only if its parent's listing says so.
                    return Listing(current) is { } names && !names.Contains(component)
                        ? candidates
                        : Nothing();
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0
                    || (attributes & FileAttributes.Directory) == 0)
                {
                    return Nothing();
                }

                current = next;
            }

            if (Listing(current) is not { } listed)
            {
                return Nothing();
            }

            candidates.RemoveWhere(relative => listed.Contains(NameOf(relative)));
            return candidates;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException
            or ArgumentException
            or NotSupportedException)
        {
            _ = exception;
            return Nothing();
        }
    }

    /// <summary>Whether the one vault-relative entry is definitely absent.</summary>
    internal static bool IsDefinitelyAbsent(string vaultRoot, string relative) =>
        AbsentAmong(vaultRoot, ParentOf(relative), [relative]).Count == 1;

    /// <summary>The folder's entry names (NTFS compares them ignoring case),
    /// or null when it could not be listed.</summary>
    private static HashSet<string>? Listing(string folder)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(folder)
                .Select(entry => Path.GetFileName(entry))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            _ = exception;
            return null;
        }
    }

    private static string ParentOf(string relative) =>
        relative.LastIndexOf('/') is var slash and >= 0 ? relative[..slash] : string.Empty;

    private static string NameOf(string relative) =>
        relative.LastIndexOf('/') is var slash and >= 0 ? relative[(slash + 1)..] : relative;

    private static HashSet<string> Nothing() => new(StringComparer.Ordinal);
}
