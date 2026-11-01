// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Win32.SafeHandles;
using System.IO;
using System.Runtime.InteropServices;

namespace SlateWindows;

/// <summary>
/// Plan 44 WB-28 PR 2: <c>NtCreateFile</c> of one simple name relative to an
/// open folder handle, so a host store reaches <c>.slate</c> and its files
/// beneath the root core holds, never by a path. The caller passes exactly
/// the access, sharing, disposition and options it needs; nothing is added
/// (no <c>SYNCHRONIZE</c>, no synchronous I/O) unless it asks, so a folder
/// that denies a right the caller does not need still opens.
/// </summary>
internal static class NativeRelativeOpen
{
    internal const uint Synchronize = 0x00100000;
    internal const uint FileOpen = 1;
    internal const uint FileCreate = 2;
    internal const uint FileOpenIf = 3;
    internal const uint FileOverwrite = 4;
    internal const uint FileOverwriteIf = 5;
    internal const uint FileDirectoryFile = 0x00000001;
    internal const uint FileWriteThrough = 0x00000002;
    internal const uint FileSequentialOnly = 0x00000004;
    internal const uint FileSynchronousIoNonalert = 0x00000020;
    internal const uint FileNonDirectoryFile = 0x00000040;
    internal const uint FileOpenReparsePoint = 0x00200000;
    private const uint ObjCaseInsensitive = 0x00000040;
    private const uint FileAttributeNormal = 0x00000080;

    /// <summary>
    /// Opens <paramref name="name"/> beneath <paramref name="parent"/>, or
    /// returns null with the Win32 error in <paramref name="error"/>.
    /// </summary>
    internal static SafeFileHandle? Open(
        SafeFileHandle parent,
        string name,
        uint access,
        FileShare share,
        uint disposition,
        uint options,
        out int error)
    {
        char[] chars = name.ToCharArray();
        GCHandle nameHandle = GCHandle.Alloc(chars, GCHandleType.Pinned);
        bool addedReference = false;
        try
        {
            parent.DangerousAddRef(ref addedReference);
            var unicode = new UnicodeString
            {
                Length = checked((ushort)(chars.Length * 2)),
                MaximumLength = checked((ushort)(chars.Length * 2)),
                Buffer = nameHandle.AddrOfPinnedObject(),
            };
            GCHandle unicodeHandle = GCHandle.Alloc(unicode, GCHandleType.Pinned);
            try
            {
                var attributes = new ObjectAttributes
                {
                    Length = (uint)Marshal.SizeOf<ObjectAttributes>(),
                    RootDirectory = parent.DangerousGetHandle(),
                    ObjectName = unicodeHandle.AddrOfPinnedObject(),
                    Attributes = ObjCaseInsensitive,
                };
                int status = NtCreateFile(
                    out SafeFileHandle handle,
                    access,
                    ref attributes,
                    out _,
                    IntPtr.Zero,
                    disposition == FileOpen ? 0 : FileAttributeNormal,
                    (uint)share,
                    disposition,
                    options,
                    IntPtr.Zero,
                    0);
                if (status < 0)
                {
                    handle.Dispose();
                    error = unchecked((int)RtlNtStatusToDosError(status));
                    return null;
                }

                error = 0;
                return handle;
            }
            finally
            {
                unicodeHandle.Free();
            }
        }
        finally
        {
            if (addedReference)
            {
                parent.DangerousRelease();
            }

            nameHandle.Free();
        }
    }

    /// <summary>The disposition <paramref name="mode"/> means.</summary>
    internal static uint DispositionOf(FileMode mode) => mode switch
    {
        FileMode.Open => FileOpen,
        FileMode.CreateNew => FileCreate,
        FileMode.OpenOrCreate => FileOpenIf,
        FileMode.Truncate => FileOverwrite,
        FileMode.Create => FileOverwriteIf,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        public uint Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        public IntPtr Status;
        public UIntPtr Information;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtCreateFile(
        out SafeFileHandle fileHandle,
        uint desiredAccess,
        ref ObjectAttributes objectAttributes,
        out IoStatusBlock ioStatusBlock,
        IntPtr allocationSize,
        uint fileAttributes,
        uint shareAccess,
        uint createDisposition,
        uint createOptions,
        IntPtr eaBuffer,
        uint eaLength);

    [DllImport("ntdll.dll")]
    private static extern uint RtlNtStatusToDosError(int status);
}
