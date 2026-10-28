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
/// link on the way and every unexpected shape proves nothing.
/// </summary>
internal static class VaultAbsence
{
    /// <summary>The candidates among <paramref name="relatives"/> — direct
    /// children of the vault-relative <paramref name="folder"/> ("" is the
    /// root) — that are definitely absent. One walk of the folder's
    /// components from the root down, never statting a candidate leaf, and at
    /// most one listing; any exception or link (a junction or symbolic link,
    /// never a cloud placeholder) answers the empty set.</summary>
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

                if ((attributes & FileAttributes.Directory) == 0
                    || ((attributes & FileAttributes.ReparsePoint) != 0 && IsLink(next)))
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

    /// <summary>Whether a reparse-point folder is a link: its reparse tag is
    /// a name surrogate — a symbolic link, a junction or mount point, a WSL
    /// symbolic link — which leads elsewhere, and which core's walk refuses
    /// (<c>crates/slate-core/src/vault/mod.rs</c>). A cloud placeholder
    /// (OneDrive Files On-Demand) carries a tag that is no name surrogate, and
    /// core reads it as an ordinary folder (red team, WB-12). A tag that
    /// cannot be read is a link: it proves nothing. The tag is the folder's
    /// own, read without following it.</summary>
    internal static bool IsLink(string folder)
    {
        using Microsoft.Win32.SafeHandles.SafeFileHandle handle = CreateFileW(
            WindowsNativePath.ForCreateFile(folder),
            FileReadAttributes,
            FileShare.ReadWrite | FileShare.Delete,
            IntPtr.Zero,
            FileMode.Open,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            IntPtr.Zero);
        if (handle.IsInvalid
            || !GetFileInformationByHandleEx(
                handle,
                FileAttributeTagInfoClass,
                out FileAttributeTagInfo info,
                (uint)System.Runtime.InteropServices.Marshal.SizeOf<FileAttributeTagInfo>()))
        {
            return true;
        }

        return (info.FileAttributes & (uint)FileAttributes.ReparsePoint) != 0
            && (info.ReparseTag & NameSurrogateBit) != 0;
    }

    private const uint FileReadAttributes = 0x00000080;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const int FileAttributeTagInfoClass = 9;

    /// <summary><c>IsReparseTagNameSurrogate</c>: bit 29 of the tag.</summary>
    private const uint NameSurrogateBit = 0x20000000;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct FileAttributeTagInfo
    {
        public uint FileAttributes;
        public uint ReparseTag;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        IntPtr securityAttributes,
        FileMode creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        Microsoft.Win32.SafeHandles.SafeFileHandle file,
        int informationClass,
        out FileAttributeTagInfo information,
        uint bufferSize);

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
