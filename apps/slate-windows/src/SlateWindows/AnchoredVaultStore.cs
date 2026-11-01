// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SlateWindows;

/// <summary>
/// Holds a vault root and its <c>.slate</c> directory for one store
/// operation. The root is the one the session holds (a
/// <see cref="VaultRootAnchor"/>), <c>.slate</c> is opened relative to it
/// and every child relative to <c>.slate</c>, each as itself (a reparse point
/// is refused, never followed), so no step resolves a path (plan 44 WB-28
/// PR 2): a vault whose spelling now names another folder is never reached.
/// </summary>
internal sealed class AnchoredVaultStore : IDisposable
{
    private const string StoreDirectoryName = ".slate";
    private const uint DeleteAccess = 0x00010000;
    private const uint FileListDirectory = 0x00000001;
    private const uint FileReadAttributes = 0x00000080;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagSequentialScan = 0x08000000;
    private const uint FileFlagWriteThrough = 0x80000000;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;

    private readonly SafeFileHandle _vaultHandle;
    private readonly SafeFileHandle _directoryHandle;
    private readonly Action? _beforeRename;
    private readonly Action? _afterRename;
    private bool _disposed;

    private AnchoredVaultStore(
        SafeFileHandle vaultHandle,
        SafeFileHandle directoryHandle,
        Action? beforeRename,
        Action? afterRename)
    {
        _vaultHandle = vaultHandle;
        _directoryHandle = directoryHandle;
        _beforeRename = beforeRename;
        _afterRename = afterRename;
    }

    /// <summary>
    /// Opens <c>.slate</c> beneath <paramref name="root"/>'s handle (made
    /// there when <paramref name="createDirectory"/>), or null when it is
    /// absent. A <c>.slate</c> that is a reparse point or not a folder is
    /// refused.
    /// </summary>
    public static AnchoredVaultStore? Open(
        VaultRootAnchor root,
        bool createDirectory,
        Action? afterDirectoryAnchored = null,
        Action? beforeRename = null,
        Action? afterRename = null)
    {
        SafeFileHandle vaultHandle = root.OpenRoot();
        SafeFileHandle? directoryHandle = null;
        try
        {
            // List access as well as attributes: only a handle with a data
            // right takes part in sharing checks, so this one keeps .slate
            // from being renamed or replaced for the operation (main compared
            // final paths after each open instead). Shared for reading and
            // writing as the session's own hold is; no synchronous I/O, since
            // it only parents the opens beneath it.
            directoryHandle = NativeRelativeOpen.Open(
                vaultHandle,
                StoreDirectoryName,
                FileReadAttributes | FileListDirectory,
                FileShare.Read | FileShare.Write,
                createDirectory ? NativeRelativeOpen.FileOpenIf : NativeRelativeOpen.FileOpen,
                NativeRelativeOpen.FileDirectoryFile | NativeRelativeOpen.FileOpenReparsePoint,
                out int error);
            if (directoryHandle is null)
            {
                if (error is ErrorFileNotFound or ErrorPathNotFound)
                {
                    vaultHandle.Dispose();
                    return null;
                }

                throw IoException("Could not safely open the vault store directory.", error);
            }

            EnsureAttributes(directoryHandle, expectDirectory: true);
            var store = new AnchoredVaultStore(
                vaultHandle,
                directoryHandle,
                beforeRename,
                afterRename);
            directoryHandle = null;
            try
            {
                afterDirectoryAnchored?.Invoke();
                return store;
            }
            catch
            {
                store.Dispose();
                throw;
            }
        }
        catch
        {
            directoryHandle?.Dispose();
            vaultHandle.Dispose();
            throw;
        }
    }

    public byte[]? ReadAllBytesBounded(string fileName, long maximumBytes)
    {
        ThrowIfDisposed();
        ValidateFileName(fileName);
        SafeFileHandle? handle = TryOpenChild(
            fileName,
            GenericRead | FileReadAttributes,
            FileShare.Read,
            FileMode.Open,
            FileFlagOpenReparsePoint | FileFlagSequentialScan);
        if (handle is null)
        {
            return null;
        }

        using (handle)
        {
            EnsureAttributes(handle, expectDirectory: false);
            using var stream = new FileStream(handle, FileAccess.Read, 64 * 1024, isAsync: false);
            return SafeFile.ReadAllBytesBounded(stream, maximumBytes);
        }
    }

    public FileStream OpenExclusiveLock(string fileName)
    {
        ThrowIfDisposed();
        ValidateFileName(fileName);
        SafeFileHandle handle = OpenChild(
            fileName,
            GenericRead | GenericWrite | FileReadAttributes,
            FileShare.None,
            FileMode.OpenOrCreate,
            FileFlagOpenReparsePoint);
        try
        {
            EnsureAttributes(handle, expectDirectory: false);
            return new FileStream(handle, FileAccess.ReadWrite, 4096, isAsync: false);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public void WriteAtomically(string fileName, ReadOnlySpan<byte> contents)
    {
        ThrowIfDisposed();
        ValidateFileName(fileName);
        string temporaryName = $"{fileName}.tmp-{Guid.NewGuid():N}";
        SafeFileHandle handle = OpenChild(
            temporaryName,
            GenericWrite | DeleteAccess | FileReadAttributes,
            FileShare.Read,
            FileMode.CreateNew,
            FileFlagOpenReparsePoint | FileFlagWriteThrough);
        bool renameCommitted = false;
        try
        {
            EnsureAttributes(handle, expectDirectory: false);
            RandomAccess.Write(handle, contents, 0);
            RandomAccess.FlushToDisk(handle);
            _beforeRename?.Invoke();
            RenameAnchored(handle, fileName);
            renameCommitted = true;
            _afterRename?.Invoke();
            EnsureAttributes(handle, expectDirectory: false);
        }
        finally
        {
            if (!renameCommitted)
            {
                _ = TryDeleteByHandle(handle, out _);
            }

            handle.Dispose();
        }
    }

    public void DeleteRegularFileIfExists(string fileName)
    {
        ThrowIfDisposed();
        ValidateFileName(fileName);
        SafeFileHandle? handle = TryOpenChild(
            fileName,
            DeleteAccess | FileReadAttributes,
            FileShare.Read,
            FileMode.Open,
            FileFlagOpenReparsePoint);
        if (handle is null)
        {
            return;
        }

        using (handle)
        {
            EnsureAttributes(handle, expectDirectory: false);
            if (!TryDeleteByHandle(handle, out int error))
            {
                throw IoException(
                    "Could not remove the migrated vault store file.",
                    error);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _directoryHandle.Dispose();
        _vaultHandle.Dispose();
    }

    private SafeFileHandle OpenChild(
        string fileName,
        uint desiredAccess,
        FileShare share,
        FileMode mode,
        uint flags) =>
        TryOpenChild(fileName, desiredAccess, share, mode, flags)
        ?? throw new FileNotFoundException("Vault store child does not exist.");

    private SafeFileHandle? TryOpenChild(
        string fileName,
        uint desiredAccess,
        FileShare share,
        FileMode mode,
        uint flags)
    {
        uint options = NativeRelativeOpen.FileNonDirectoryFile
            | NativeRelativeOpen.FileSynchronousIoNonalert;
        if ((flags & FileFlagOpenReparsePoint) != 0)
        {
            options |= NativeRelativeOpen.FileOpenReparsePoint;
        }

        if ((flags & FileFlagSequentialScan) != 0)
        {
            options |= NativeRelativeOpen.FileSequentialOnly;
        }

        if ((flags & FileFlagWriteThrough) != 0)
        {
            options |= NativeRelativeOpen.FileWriteThrough;
        }

        // A FileStream reads it, so it is synchronous.
        SafeFileHandle? handle = NativeRelativeOpen.Open(
            _directoryHandle,
            fileName,
            desiredAccess | NativeRelativeOpen.Synchronize,
            share,
            NativeRelativeOpen.DispositionOf(mode),
            options,
            out int error);
        if (handle is null)
        {
            if (error is ErrorFileNotFound or ErrorPathNotFound)
            {
                return null;
            }

            throw IoException("Could not safely open a vault store file.", error);
        }

        return handle;
    }

    internal static void EnsureAttributes(SafeFileHandle handle, bool expectDirectory)
    {
        if (!GetFileInformationByHandleEx(
            handle,
            FileInfoByHandleClass.FileAttributeTagInfo,
            out FileAttributeTagInfo info,
            (uint)Marshal.SizeOf<FileAttributeTagInfo>()))
        {
            throw IoException(
                "Could not inspect an anchored vault store handle.",
                Marshal.GetLastPInvokeError());
        }

        bool isDirectory = (info.FileAttributes & FileAttributeDirectory) != 0;
        if ((info.FileAttributes & FileAttributeReparsePoint) != 0
            || isDirectory != expectDirectory)
        {
            throw new IOException("Vault store handle has an unsafe file type.");
        }
    }

    private void RenameAnchored(SafeFileHandle handle, string destinationName)
    {
        byte[] information = BuildRenameInformation(destinationName, IntPtr.Size);
        IntPtr buffer = Marshal.AllocHGlobal(information.Length);
        try
        {
            Marshal.Copy(information, 0, buffer, information.Length);
            int status = NtSetInformationFile(
                handle,
                out _,
                buffer,
                (uint)information.Length,
                NtFileInformationClass.FileRenameInformation);
            if (status < 0)
            {
                throw IoException(
                    "Could not atomically replace the vault store file.",
                    unchecked((int)RtlNtStatusToDosError(status)));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal static byte[] BuildRenameInformation(string destinationName, int pointerSize)
    {
        ValidateFileName(destinationName);
        if (pointerSize is not (4 or 8))
        {
            throw new ArgumentOutOfRangeException(nameof(pointerSize));
        }

        byte[] name = Encoding.Unicode.GetBytes(destinationName);
        int rootOffset = pointerSize == 8 ? 8 : 4;
        int nameLengthOffset = rootOffset + pointerSize;
        int nameOffset = nameLengthOffset + sizeof(uint);
        int structureSize = pointerSize == 8 ? 24 : 16;
        byte[] information = new byte[checked(structureSize + name.Length)];
        information[0] = 1;
        BitConverter.GetBytes(name.Length).CopyTo(information, nameLengthOffset);
        name.CopyTo(information, nameOffset);
        // A null root plus a simple name tells the kernel to rename within the
        // already-open source handle's directory. This form neither reopens an
        // ancestor path nor sends an invalid directory handle over SMB.
        return information;
    }

    private static bool TryDeleteByHandle(SafeFileHandle handle, out int error)
    {
        IntPtr buffer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Marshal.WriteInt32(buffer, 1);
            bool deleted = SetFileInformationByHandle(
                handle,
                FileInfoByHandleClass.FileDispositionInfo,
                buffer,
                sizeof(int));
            error = deleted ? 0 : Marshal.GetLastPInvokeError();
            return deleted;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName is "." or ".."
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || fileName.Contains(Path.DirectorySeparatorChar)
            || fileName.Contains(Path.AltDirectorySeparatorChar)
            || fileName.Contains(':'))
        {
            throw new ArgumentException("Vault store child must be a simple file name.", nameof(fileName));
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static IOException IoException(string message, int error) =>
        new(message, new Win32Exception(error));

    private enum FileInfoByHandleClass
    {
        FileDispositionInfo = 4,
        FileAttributeTagInfo = 9,
    }

    private enum NtFileInformationClass
    {
        FileRenameInformation = 10,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        public IntPtr Status;
        public UIntPtr Information;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTagInfo
    {
        public uint FileAttributes;
        public uint ReparseTag;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle file,
        FileInfoByHandleClass informationClass,
        out FileAttributeTagInfo information,
        uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle file,
        FileInfoByHandleClass informationClass,
        IntPtr information,
        uint bufferSize);

    [DllImport("ntdll.dll")]
    private static extern int NtSetInformationFile(
        SafeFileHandle file,
        out IoStatusBlock ioStatusBlock,
        IntPtr information,
        uint bufferSize,
        NtFileInformationClass informationClass);

    [DllImport("ntdll.dll")]
    private static extern uint RtlNtStatusToDosError(int status);
}
