// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Win32.SafeHandles;
using System.IO;
using System.Runtime.InteropServices;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// Plan 44 WB-28 PR 2 (open item 1): which vault root a pending write or a
/// store belongs to. A persistent identity is the file system's own id, so
/// two sessions on one folder (any spelling) share a scope (owner decision
/// D1); a weak identity (FAT, FAT32, exFAT) can collide between folders, so
/// each session is its own scope, compared by reference.
/// </summary>
internal abstract record VaultRootScope
{
    internal sealed record Persistent(ulong Volume, ulong IdHigh, ulong IdLow) : VaultRootScope;

    internal sealed record Session(object Token) : VaultRootScope
    {
        public bool Equals(Session? other) =>
            other is not null && ReferenceEquals(Token, other.Token);

        public override int GetHashCode() =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Token);
    }

    internal sealed record PathForTests(string Key) : VaultRootScope;
}

/// <summary>
/// Plan 44 WB-28 PR 2: the capability a host vault store reaches the vault
/// through — the root the session holds, never a path and never an identity
/// compare. <see cref="OpenRoot"/> hands out a fresh handle the caller owns.
/// </summary>
internal sealed class VaultRootAnchor : IDisposable
{
    /// <summary>
    /// Whether <see cref="ByPathForTests"/> may open a root by its path. Only
    /// the test assembly's module initializer sets it; production refuses.
    /// </summary>
    internal static bool ByPathAllowed;

    private readonly Func<SafeFileHandle> _open;
    private readonly SafeFileHandle? _pinned;
    private bool _disposed;

    private VaultRootAnchor(
        Func<SafeFileHandle> open,
        VaultRootScope scope,
        string displayPath,
        SafeFileHandle? pinned = null)
    {
        _open = open;
        Scope = scope;
        DisplayPath = displayPath;
        _pinned = pinned;
    }

    /// <summary>The root's scope, read once when the anchor was made.</summary>
    internal VaultRootScope Scope { get; }

    /// <summary>The root's path for display and for a store's messages;
    /// nothing is ever opened through it.</summary>
    internal string DisplayPath { get; }

    /// <summary>
    /// The anchor of <paramref name="session"/>'s held root: each
    /// <see cref="OpenRoot"/> asks core for a fresh duplicate of it.
    /// </summary>
    public static VaultRootAnchor Of(VaultSession session)
    {
        VaultRootIdentity? identity = session.RootIdentity();
        VaultRootScope scope = identity is { Persistent: true }
            ? new VaultRootScope.Persistent(identity.Volume, identity.IdHigh, identity.IdLow)
            : new VaultRootScope.Session(session);
        return new VaultRootAnchor(
            () =>
            {
                try
                {
                    return new SafeFileHandle((nint)session.DuplicateRootHandle(), ownsHandle: true);
                }
                catch (VaultException exception)
                {
                    throw new IOException(
                        "The vault root is not available: " + SlateUniffiMethods.VaultErrorDetail(exception),
                        exception);
                }
                catch (ObjectDisposedException exception)
                {
                    throw new IOException("The vault session has closed.", exception);
                }
            },
            scope,
            session.RootDisplayPath() ?? string.Empty);
    }

    /// <summary>
    /// main's by-path root, for the tests that build a store with no session.
    /// Refuses unless <see cref="ByPathAllowed"/> is set.
    /// </summary>
    public static VaultRootAnchor ByPathForTests(string vaultRoot)
    {
        if (!ByPathAllowed)
        {
            throw new IOException("A vault store needs the session's held root.");
        }

        // GraphConfigWriter.KeyOf's spelling rule: a trailing separator
        // trimmed, a drive root left rooted, case folded.
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(vaultRoot));
        return new VaultRootAnchor(
            () => OpenByPath(full),
            new VaultRootScope.PathForTests(full.ToUpperInvariant()),
            full);
    }

    /// <summary>A fresh handle on the root; the caller owns it.</summary>
    public SafeFileHandle OpenRoot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _open();
    }

    /// <summary>
    /// An anchor that owns one handle on the root taken now and hands out
    /// duplicates of it until disposed: a write queued while the session
    /// lives lands in that root even after the session closes.
    /// </summary>
    public VaultRootAnchor Pin()
    {
        SafeFileHandle pinned = OpenRoot();
        return new VaultRootAnchor(() => Duplicate(pinned), Scope, DisplayPath, pinned);
    }

    /// <summary>Whether <see cref="Dispose"/> has run.</summary>
    internal bool IsDisposedForTests => _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pinned?.Dispose();
    }

    private static SafeFileHandle OpenByPath(string path)
    {
        const uint FileReadAttributes = 0x00000080;
        const uint FileListDirectory = 0x00000001;
        const uint FileFlagBackupSemantics = 0x02000000;
        SafeFileHandle handle = CreateFileW(
            WindowsNativePath.ForCreateFile(path),
            FileReadAttributes | FileListDirectory,
            FileShare.Read | FileShare.Write | FileShare.Delete,
            IntPtr.Zero,
            FileMode.Open,
            FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            if (error is 2 or 3)
            {
                // A root that is gone: the stores' missing-root arm.
                throw new DirectoryNotFoundException("The vault root is gone.");
            }

            throw new IOException(
                "Could not open the vault root.",
                new System.ComponentModel.Win32Exception(error));
        }

        return handle;
    }

    private static SafeFileHandle Duplicate(SafeFileHandle handle)
    {
        const uint DuplicateSameAccess = 0x00000002;
        IntPtr process = GetCurrentProcess();
        bool addedReference = false;
        try
        {
            handle.DangerousAddRef(ref addedReference);
            if (!DuplicateHandle(
                process,
                handle.DangerousGetHandle(),
                process,
                out IntPtr duplicate,
                0,
                false,
                DuplicateSameAccess))
            {
                throw new IOException(
                    "Could not duplicate the pinned vault root.",
                    new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError()));
            }

            return new SafeFileHandle(duplicate, ownsHandle: true);
        }
        finally
        {
            if (addedReference)
            {
                handle.DangerousRelease();
            }
        }
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

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateHandle(
        IntPtr sourceProcess,
        IntPtr sourceHandle,
        IntPtr targetProcess,
        out IntPtr targetHandle,
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint options);
}
