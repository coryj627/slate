// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;

namespace SlateWindows;

/// <summary>
/// WB-12: an absolute path against the open vault's root — whether it lies
/// inside, and its vault-relative spelling (forward slashes, as core takes
/// it). The one containment check the import and the Bases export share.
/// </summary>
internal static class VaultPaths
{
    /// <summary>Whether <paramref name="absolutePath"/> is the vault root or
    /// lies under it (ordinal-ignore-case, as NTFS compares names).</summary>
    internal static bool IsInside(string vaultRoot, string absolutePath)
    {
        string root = Path.GetFullPath(vaultRoot).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(absolutePath);
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                candidate.TrimEnd(Path.DirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary><paramref name="absolutePath"/> relative to the vault root,
    /// with forward slashes.</summary>
    internal static string Relative(string vaultRoot, string absolutePath) =>
        Path.GetRelativePath(Path.GetFullPath(vaultRoot), Path.GetFullPath(absolutePath))
            .Replace('\\', '/');
}
