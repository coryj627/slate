// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SlateWindows.FileManagement;

/// <summary>
/// W5-4 (codex round 2): the stable filesystem identity behind the
/// undo journal's changed-files preflight. Existence alone admits a
/// REPLACEMENT — delete b.md, create an unrelated b.md, Ctrl+Z renames
/// the stranger. NTFS's 128-bit file ID survives renames, moves, and
/// content edits (the operations undo legitimately spans) but never
/// survives delete-and-recreate — and unlike creation time it is
/// immune to NTFS tunneling. A null identity (a filesystem without
/// the query) degrades the preflight to the existence check,
/// recorded, not hidden.
/// </summary>
internal static class FileIdentity
{
    private const uint FileReadAttributes = 0x0080;
    private const uint FileShareAll = 0x1 | 0x2 | 0x4;
    private const uint OpenExisting = 3;

    /// <summary>Required to open DIRECTORY handles.</summary>
    private const uint FileFlagBackupSemantics = 0x02000000;

    /// <summary>FILE_INFO_BY_HANDLE_CLASS.FileIdInfo.</summary>
    private const int FileIdInfoClass = 18;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdInfo
    {
        public ulong VolumeSerialNumber;
        public ulong FileIdLow;
        public ulong FileIdHigh;
    }

    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        CharSet = CharSet.Unicode,
        SetLastError = true,
        ExactSpelling = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle file,
        int infoClass,
        out FileIdInfo info,
        int bufferSize);

    /// <summary>The identity token for a file or directory, or null
    /// when the path cannot be opened or the volume does not answer
    /// the query.</summary>
    internal static string? TryGet(string absolutePath)
    {
        using SafeFileHandle handle = CreateFile(
            absolutePath,
            FileReadAttributes,
            FileShareAll,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return null;
        }

        if (!GetFileInformationByHandleEx(
            handle, FileIdInfoClass, out FileIdInfo info,
            Marshal.SizeOf<FileIdInfo>()))
        {
            return null;
        }

        return $"{info.VolumeSerialNumber:x16}-{info.FileIdHigh:x16}{info.FileIdLow:x16}";
    }
}
