// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SlateWindows;

/// <summary>
/// Holds no-delete-share handles to a vault root and its <c>.slate</c>
/// directory for one store operation. Child files are opened without following
/// their final reparse point and are verified against the anchored directory.
/// </summary>
internal sealed class AnchoredVaultStore : IDisposable
{
    private const string StoreDirectoryName = ".slate";
    private const uint DeleteAccess = 0x00010000;
    private const uint FileReadAttributes = 0x00000080;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagSequentialScan = 0x08000000;
    private const uint FileFlagWriteThrough = 0x80000000;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;

    private readonly SafeFileHandle _vaultHandle;
    private readonly SafeFileHandle _directoryHandle;
    private readonly string _directoryPath;
    private readonly string _directoryFinalPath;
    private readonly Action? _beforeRename;
    private readonly Action? _afterRename;
    private bool _disposed;

    private AnchoredVaultStore(
        SafeFileHandle vaultHandle,
        SafeFileHandle directoryHandle,
        string directoryPath,
        string directoryFinalPath,
        Action? beforeRename,
        Action? afterRename)
    {
        _vaultHandle = vaultHandle;
        _directoryHandle = directoryHandle;
        _directoryPath = directoryPath;
        _directoryFinalPath = directoryFinalPath;
        _beforeRename = beforeRename;
        _afterRename = afterRename;
    }

    public static AnchoredVaultStore? Open(
        string vaultRoot,
        bool createDirectory,
        Action? afterDirectoryAnchored = null,
        Action? beforeRename = null,
        Action? afterRename = null)
    {
        string fullRoot = Path.GetFullPath(vaultRoot);
        SafeFileHandle vaultHandle = OpenRequiredDirectory(fullRoot);
        SafeFileHandle? directoryHandle = null;
        try
        {
            string rootFinalPath = FinalPath(vaultHandle);
            string directoryPath = Path.Combine(fullRoot, StoreDirectoryName);
            if (createDirectory)
            {
                Directory.CreateDirectory(directoryPath);
            }

            directoryHandle = TryOpenDirectory(directoryPath);
            if (directoryHandle is null)
            {
                vaultHandle.Dispose();
                return null;
            }

            string directoryFinalPath = FinalPath(directoryHandle);
            string expectedDirectory = NormalizeFinalPath(
                Path.Combine(rootFinalPath, StoreDirectoryName));
            if (!string.Equals(
                directoryFinalPath,
                expectedDirectory,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Vault store directory identity changed.");
            }

            var store = new AnchoredVaultStore(
                vaultHandle,
                directoryHandle,
                directoryPath,
                directoryFinalPath,
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
        VerifyDirectoryIdentity();
        SafeFileHandle? handle = TryOpenChild(
            fileName,
            GenericRead | FileReadAttributes,
            FileShare.Read,
            FileMode.Open,
            FileAttributeNormal | FileFlagOpenReparsePoint | FileFlagSequentialScan);
        if (handle is null)
        {
            return null;
        }

        using (handle)
        {
            EnsureRegularChild(handle, fileName);
            using var stream = new FileStream(handle, FileAccess.Read, 64 * 1024, isAsync: false);
            return SafeFile.ReadAllBytesBounded(stream, maximumBytes);
        }
    }

    public FileStream OpenExclusiveLock(string fileName)
    {
        ThrowIfDisposed();
        ValidateFileName(fileName);
        VerifyDirectoryIdentity();
        SafeFileHandle handle = OpenChild(
            fileName,
            GenericRead | GenericWrite | FileReadAttributes,
            FileShare.None,
            FileMode.OpenOrCreate,
            FileAttributeNormal | FileFlagOpenReparsePoint);
        try
        {
            EnsureRegularChild(handle, fileName);
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
        VerifyDirectoryIdentity();
        string temporaryName = $"{fileName}.tmp-{Guid.NewGuid():N}";
        SafeFileHandle handle = OpenChild(
            temporaryName,
            GenericWrite | DeleteAccess | FileReadAttributes,
            FileShare.Read,
            FileMode.CreateNew,
            FileAttributeNormal | FileFlagOpenReparsePoint | FileFlagWriteThrough);
        bool renameCommitted = false;
        try
        {
            EnsureRegularChild(handle, temporaryName);
            RandomAccess.Write(handle, contents, 0);
            RandomAccess.FlushToDisk(handle);
            VerifyDirectoryIdentity();
            _beforeRename?.Invoke();
            RenameAnchored(handle, fileName);
            renameCommitted = true;
            _afterRename?.Invoke();
            EnsureRegularChild(handle, fileName);
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
        VerifyDirectoryIdentity();
        SafeFileHandle? handle = TryOpenChild(
            fileName,
            DeleteAccess | FileReadAttributes,
            FileShare.Read,
            FileMode.Open,
            FileAttributeNormal | FileFlagOpenReparsePoint);
        if (handle is null)
        {
            return;
        }

        using (handle)
        {
            EnsureRegularChild(handle, fileName);
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

    private static SafeFileHandle OpenRequiredDirectory(string path) =>
        TryOpenDirectory(path)
        ?? throw new DirectoryNotFoundException("Vault root does not exist.");

    private static SafeFileHandle? TryOpenDirectory(string path)
    {
        SafeFileHandle handle = CreateFileW(
            path,
            FileReadAttributes,
            FileShare.Read | FileShare.Write,
            IntPtr.Zero,
            FileMode.Open,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            if (error is ErrorFileNotFound or ErrorPathNotFound)
            {
                return null;
            }

            throw IoException("Could not safely open the vault store directory.", error);
        }

        try
        {
            EnsureAttributes(handle, expectDirectory: true);
            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
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
        SafeFileHandle handle = CreateFileW(
            Path.Combine(_directoryPath, fileName),
            desiredAccess,
            share,
            IntPtr.Zero,
            mode,
            flags,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            if (error is ErrorFileNotFound or ErrorPathNotFound)
            {
                return null;
            }

            throw IoException("Could not safely open a vault store file.", error);
        }

        return handle;
    }

    private void EnsureRegularChild(SafeFileHandle handle, string fileName)
    {
        EnsureAttributes(handle, expectDirectory: false);
        string actual = FinalPath(handle);
        string expected = NormalizeFinalPath(Path.Combine(_directoryFinalPath, fileName));
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Vault store file identity changed.");
        }
    }

    private void VerifyDirectoryIdentity()
    {
        if (!string.Equals(
            FinalPath(_directoryHandle),
            _directoryFinalPath,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Vault store directory identity changed.");
        }
    }

    private static void EnsureAttributes(SafeFileHandle handle, bool expectDirectory)
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

    private static string FinalPath(SafeFileHandle handle)
    {
        char[] buffer = new char[512];
        while (true)
        {
            uint length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
            if (length == 0)
            {
                throw IoException(
                    "Could not resolve an anchored vault store handle.",
                    Marshal.GetLastPInvokeError());
            }

            if (length < buffer.Length)
            {
                return NormalizeFinalPath(new string(buffer, 0, (int)length));
            }

            buffer = new char[checked((int)length + 1)];
        }
    }

    private static string NormalizeFinalPath(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        IntPtr securityAttributes,
        FileMode creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle file,
        FileInfoByHandleClass informationClass,
        out FileAttributeTagInfo information,
        uint bufferSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle file,
        [Out] char[] filePath,
        uint filePathLength,
        uint flags);

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
