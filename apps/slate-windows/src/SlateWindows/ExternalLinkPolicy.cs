// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using Microsoft.Win32.SafeHandles;

namespace SlateWindows;

// The shell hand-off policies. Two rules, one file, because they answer
// the same question for two kinds of target — what may reach
// `Process.Start(UseShellExecute: true)` — and a reviewer looking for
// either should find both.

/// <summary>
/// The ONE scheme allowlist for every surface that hands a URL to the
/// shell — the mac allowlist verbatim. Anything else (<c>file:</c>,
/// <c>javascript:</c>, a custom scheme) is refused, so a hostile
/// <c>.bib</c> entry, a note's link, or a canvas link card cannot smuggle
/// a local-execution target past a surface that forgot to check.
/// </summary>
/// <remarks>
/// <para>
/// W6-1 PR A (#745) extracted this. It had been copied three times —
/// the right-pane panels, the citation popover, and then the canvas
/// link card — and the third copy is what made it a drift surface
/// rather than a coincidence: three literals of
/// <c>"http" or "https" or "mailto"</c> that a future scheme addition
/// would have to find all of.
/// </para>
/// <para>
/// The PREDICATE is shared; the announcements deliberately are not.
/// The panels and the citation popover speak the
/// <c>ExternalLink*</c> vocabulary; the canvas speaks its own
/// (<c>CanvasOpened</c> / <c>CanvasBlocked</c>), because a canvas
/// surface that suddenly spoke a different family's sentences would be
/// the §W-D drift this programme exists to stop. One rule, three
/// voices.
/// </para>
/// </remarks>
internal static class ExternalLinkPolicy
{
    /// <summary>Whether the shell may be handed this target.</summary>
    internal static bool IsLaunchable(string? target) =>
        target is { Length: > 0 }
        && Uri.TryCreate(target, UriKind.Absolute, out Uri? uri)
        && uri.Scheme is "http" or "https" or "mailto";
}

/// <summary>
/// The other shell hand-off, and the stricter one: which vault FILES a
/// canvas card may open in their default app (W6-1 PR A, CD-38).
/// </summary>
/// <remarks>
/// <para>
/// A canvas is untrusted input — it arrives over sync, from a shared
/// vault, from Obsidian — and a file card is a path the author chose.
/// <c>Process.Start(UseShellExecute: true)</c> on Windows is
/// <c>ShellExecute</c>, which EXECUTES what it is given: a
/// <c>{"type":"file","file":"setup.exe"}</c> node would run on one
/// Enter. So the default-app open is gated to media by extension, and
/// everything else is refused audibly. Never silent, never launched.
/// </para>
/// <para>
/// Windows is deliberately stricter than mac here, which opens any
/// non-Markdown target through <c>NSWorkspace</c>
/// (<c>CanvasContainerView.swift:181–187</c>). The threat models are
/// not the same — Gatekeeper and quarantine adjudicate an NSWorkspace
/// open, and ShellExecute adjudicates nothing — so this is a divergence
/// recorded as CD-38 rather than a parity break; mac's laxer arm is an
/// upstream note.
/// </para>
/// <para>
/// <b>The set is core's, and since §E TE-0 it is ASKED FOR, not
/// copied.</b> Core owns the classification in
/// <c>canvas::model::media_class</c> — the same function whose answer
/// becomes the <c>image</c> kind label and the "Image:"/"Audio:"/
/// "Video:" title prefixes — and the CD-38 drift note's staged export
/// landed with §E's first task: the transliterated set, its two edge
/// rules and the kind-label detour pin all retired with it. The gate
/// below is one FFI call; the behavior table in
/// <c>CanvasDocumentTests</c> pins the set through the export.
/// </para>
/// </remarks>
internal static class CanvasMediaPolicy
{
    /// <summary>
    /// Whether this vault-relative target is media the canvas may hand
    /// to the shell — CORE'S answer over the FFI (§E TE-0: the CD-38
    /// staged export landed and the transliterated set retired with
    /// it; the empty-target guard is the one host-side arm, because
    /// the question "is nothing media" never needs a canvas).
    /// </summary>
    internal static bool IsOpenableMedia(string? target) =>
        target is { Length: > 0 }
        && uniffi.slate_uniffi.SlateUniffiMethods.CanvasMediaClass(target) is not null;

    /// <summary>
    /// Open a media file card in its default app, or refuse — the whole
    /// containment decision, made against the target's OS FILE IDENTITY
    /// captured in ONE coherent handle snapshot, revalidated by that
    /// identity immediately before the launch, and failing closed.
    /// <paramref name="launch"/> is the shell hand-off; it runs with
    /// NOTHING between it and the final identity check. Returns whether
    /// the shell was handed the file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Five codex rounds converged on this design. The class codex named
    /// is <b>filesystem identity reduced to path text</b>; path text is
    /// retired as the decision substrate. Containment, revalidation and
    /// launch decide on OS file identity — the 128-bit
    /// <c>FILE_ID_INFO</c> (volume serial + 128-bit file id, ReFS-safe;
    /// the 64-bit <c>nFileIndex</c> is NOT unique on ReFS). That is the
    /// ONLY identity method: there is no fallback, and a failed identity
    /// query refuses rather than downgrading (round 5).
    /// </para>
    /// <para>
    /// <b>One coherent snapshot (round 4, B1).</b> The leaf is opened
    /// ONCE; its identity and its resolved terminal path come from THAT
    /// handle; and its ancestors are opened and HELD simultaneously while
    /// their identities are compared to the vault root's. The captured
    /// leaf identity is the one from the containment handle — not a fresh
    /// re-open. A re-open to capture would have opened a second window: an
    /// ancestor swap between containment and that re-open made the
    /// captured identity the OUTSIDE object, and revalidating
    /// outside-against-outside passed. Fusing capture into containment
    /// closes that sub-window by construction.
    /// </para>
    /// <para>
    /// Resolution keeps the handle-resolved EXTENDED (<c>\\?\</c>) form
    /// end to end: it is verified and it is what is launched, which is
    /// what stops <c>ShellExecute</c> renormalizing <c>vault.\file</c> to
    /// <c>vault\file</c>.
    /// </para>
    /// <para>
    /// <b>The single remaining residual (CD-38).</b> Immediately before
    /// launch the resolved path is re-opened and its identity compared to
    /// the snapshot's; the launch happens only if identical. After that,
    /// the ONLY residual is that <c>ShellExecute</c> re-opens by PATH and
    /// resolves it itself — a path-taking launcher cannot be handed the
    /// verified handle. Closing it needs a handle-based launcher, which
    /// <c>ShellExecute</c> is not.
    /// </para>
    /// <para>
    /// Every failure mode is a refusal — a NUL character, a device name,
    /// a path too long, a cycle, a permission error, an ancestor that
    /// cannot be opened — because an exception or an unopenable handle
    /// escaping to the caller would abort the activation silently rather
    /// than refuse it audibly.
    /// </para>
    /// </remarks>
    internal static bool OpenMediaInVault(
        string vaultRoot, string target, Func<string, bool> launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        try
        {
            if (!IsOpenableMedia(target))
            {
                return false;
            }
            if (IdentityOf(Path.GetFullPath(vaultRoot)) is not { } rootIdentity)
            {
                return false;
            }
            string absolute = Path.GetFullPath(
                Path.Combine(Path.GetFullPath(vaultRoot), target));
            // ONE coherent snapshot: the resolved path AND the identity
            // that revalidation checks come from the same held handles as
            // the containment decision.
            if (ResolveContained(absolute, rootIdentity)
                is not var (resolved, checkIdentity))
            {
                return false;
            }

            // The TOCTOU window (CD-38): an in-vault attacker may swap the
            // namespace here. The test seam simulates it.
            BetweenCheckAndLaunchForTests?.Invoke();

            // Revalidate by IDENTITY immediately before launch — nothing
            // between this and launch(). Re-open the resolved path and
            // require the SAME file identity as the snapshot captured; a
            // swap that redirected it changes the identity and refuses.
            if (IdentityOf(resolved) is not { } relaunchIdentity
                || relaunchIdentity != checkIdentity)
            {
                return false;
            }
            return launch(resolved);
        }
        catch (Exception exception) when (NotFatal(exception))
        {
            return false;
        }
    }

    /// <summary>
    /// The resolved extended path for the facts that assert containment
    /// without launching: the fully-resolved terminal <c>\\?\</c> path
    /// when it is media whose identity chain reaches the vault root, else
    /// null.
    /// </summary>
    internal static string? ResolveInsideVault(string vaultRoot, string target)
    {
        try
        {
            if (!IsOpenableMedia(target))
            {
                return null;
            }
            if (IdentityOf(Path.GetFullPath(vaultRoot)) is not { } rootIdentity)
            {
                return null;
            }
            string absolute = Path.GetFullPath(
                Path.Combine(Path.GetFullPath(vaultRoot), target));
            return ResolveContained(absolute, rootIdentity) is var (resolved, _)
                ? resolved
                : null;
        }
        catch (Exception exception) when (NotFatal(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// Set only by tests: invoked once, after the coherent snapshot and
    /// before the revalidation-and-launch, so a fact can act in the
    /// TOCTOU window the identity revalidation exists to catch.
    /// </summary>
    internal static Action? BetweenCheckAndLaunchForTests { get; set; }

    /// <summary>
    /// The ONE coherent snapshot: open the leaf, capture its resolved
    /// extended path and its identity from that handle, and — holding it
    /// and every ancestor handle simultaneously — confirm the ancestor
    /// chain reaches <paramref name="rootIdentity"/> by identity. Returns
    /// the resolved path and the leaf identity, or null when it does not
    /// resolve, is not media, or is not contained.
    /// </summary>
    /// <remarks>
    /// Holding every handle to the decision keeps the leaf identity, the
    /// resolved path and the ancestor chain a single coherent view. The
    /// leaf identity returned here is what revalidation re-checks, so
    /// there is no second "capture" open to race (round 4, B1).
    /// </remarks>
    private static (string Resolved, FileIdentity Identity)? ResolveContained(
        string absolute, FileIdentity rootIdentity)
    {
        var held = new List<SafeFileHandle>();
        try
        {
            SafeFileHandle leaf = OpenForQuery(absolute);
            held.Add(leaf);
            if (leaf.IsInvalid)
            {
                return null;
            }
            string? resolved = FinalPath(leaf, NativeIo.VolumeNameDos)
                ?? FinalPath(leaf, NativeIo.VolumeNameGuid);
            if (resolved is null || IdentityOfHandle(leaf) is not { } leafIdentity)
            {
                return null;
            }
            // The RESOLVED name must still be media: a `.png` whose chain
            // ends at an `.exe` is the case resolution exists for.
            if (!IsOpenableMedia(resolved))
            {
                return null;
            }

            // Ancestor walk, HOLDING each handle to the decision. The walk
            // is purely lexical (ParentOf shortens strictly and ends at
            // the volume root), so it needs no arbitrary depth cap — only
            // the shortening guard (round 4, #3): a valid file 70 dirs
            // deep must open.
            string? current = ParentOf(resolved);
            string? previous = null;
            while (current is not null
                && !string.Equals(current, previous, StringComparison.Ordinal))
            {
                SafeFileHandle ancestor = OpenForQuery(current);
                held.Add(ancestor);
                if (ancestor.IsInvalid || IdentityOfHandle(ancestor) is not { } ancestorId)
                {
                    // An ancestor we cannot open cannot be confirmed as
                    // the root; fail closed rather than walk past it.
                    return null;
                }
                if (ancestorId == rootIdentity)
                {
                    return (resolved, leafIdentity);
                }
                previous = current;
                current = ParentOf(current);
            }
            return null;
        }
        finally
        {
            foreach (SafeFileHandle handle in held)
            {
                handle.Dispose();
            }
        }
    }

    /// <summary>The parent of an extended (<c>\\?\</c>) path, or null at
    /// the volume root. Text names the parent to OPEN; the decision is
    /// identity.</summary>
    private static string? ParentOf(string extendedPath)
    {
        const string localPrefix = @"\\?\";
        bool extended = extendedPath.StartsWith(localPrefix, StringComparison.Ordinal);
        string bare = extended ? extendedPath[localPrefix.Length..] : extendedPath;
        string? parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(bare));
        if (string.IsNullOrEmpty(parent))
        {
            return null;
        }
        return extended ? localPrefix + parent : parent;
    }

    /// <summary>
    /// The OS file identity of the object a handle to <paramref name="path"/>
    /// names. Null when it cannot be opened. Reparse points are followed,
    /// so this is the identity of the TERMINAL object. Used for the vault
    /// root and the launch-time revalidation; the snapshot uses
    /// <see cref="IdentityOfHandle"/> on its held handles.
    /// </summary>
    private static FileIdentity? IdentityOf(string path)
    {
        using SafeFileHandle handle = OpenForQuery(path);
        return handle.IsInvalid ? null : IdentityOfHandle(handle);
    }

    /// <summary>
    /// The identity of an OPEN handle: the 128-bit <c>FILE_ID_INFO</c>,
    /// or NULL. There is no second identity method — a failure is a
    /// refusal, never a downgrade.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>nFileIndex</c> from <c>BY_HANDLE_FILE_INFORMATION</c> is
    /// documented as NOT unique on ReFS and its ids are reused, so a
    /// 64-bit compare can call two different files the same — a fail-OPEN
    /// in the gate this identity anchors.
    /// <c>GetFileInformationByHandleEx(FileIdInfo)</c> returns a 128-bit
    /// file id that is stable and unique on NTFS and ReFS alike, and it is
    /// the ONLY identity this gate accepts.
    /// </para>
    /// <para>
    /// There is deliberately NO legacy fallback (codex round 5). A
    /// per-CALL fallback is strictly worse than none: any transient
    /// failure of the primary query — not just an old host — silently
    /// downgrades that one read to the non-unique index, which on ReFS is
    /// a fail-open, and it does so exactly when something is already
    /// wrong. Its mere existence WAS the mixed-method class.
    /// </para>
    /// <para>
    /// <b>The constraint is the FILESYSTEM, not the OS version.</b>
    /// <c>FileIdInfo</c> is not answered by every filesystem: FAT32 and
    /// exFAT commonly fail it, as do some redirectors and virtual
    /// filesystems. So the supported volumes for opening vault media are
    /// NTFS and ReFS. On a vault whose volume does not answer
    /// <c>FileIdInfo</c> — a FAT32/exFAT stick, some network or virtual
    /// mounts — <b>every media open refuses, audibly</b>. That is a known,
    /// recorded fail-CLOSED limitation, and it is a real availability
    /// regression against the round-4 code, which would have opened those
    /// files via the legacy index. It is accepted deliberately: the only
    /// alternative is deciding containment on a weaker identity, which is
    /// the fail-OPEN codex round 5 killed. Refusing to open a photo is
    /// recoverable; launching a file that escaped the vault is not.
    /// </para>
    /// </remarks>
    private static FileIdentity? IdentityOfHandle(SafeFileHandle handle)
    {
        if (FailIdentityQueryForTests
            || !NativeIo.TryGetFileIdInfo(handle, out NativeIo.FileIdInformation idInfo))
        {
            // No downgrade. FileIdInfo or nothing.
            return null;
        }
        return new FileIdentity(
            idInfo.VolumeSerialNumber, idInfo.FileIdLow, idInfo.FileIdHigh);
    }

    /// <summary>
    /// The terminal extended (<c>\\?\</c>) path a handle names — every
    /// reparse point resolved by the OS. Kept extended: verified and
    /// launched unchanged, so <c>ShellExecute</c> cannot renormalize a
    /// trailing dot out from under the check. The volume-GUID form is the
    /// fallback for a driveless (folder-mounted) volume with no DOS path.
    /// </summary>
    private static string? FinalPath(SafeFileHandle handle, uint volumeNameFlag)
    {
        if (handle.IsInvalid)
        {
            return null;
        }
        // GetFinalPathNameByHandle returns the length WITHOUT the null when
        // the buffer fits, and the required length WITH the null when it
        // does not. So a too-small buffer is grown to the reported size and
        // re-read ONCE — a long, deeply-nested but legitimate media path
        // must not be refused for buffer reasons (the availability sibling
        // of the depth cap, round 4 #3). An extended path caps at ~32767.
        var buffer = new char[1024];
        uint length = NativeIo.GetFinalPathNameByHandleW(
            handle, buffer, (uint)buffer.Length, volumeNameFlag);
        if (length == 0)
        {
            // Failure (e.g. ERROR_PATH_NOT_FOUND for a driveless volume
            // under VOLUME_NAME_DOS — the caller falls back to the GUID
            // name).
            return null;
        }
        if (length >= buffer.Length)
        {
            buffer = new char[length];
            length = NativeIo.GetFinalPathNameByHandleW(
                handle, buffer, (uint)buffer.Length, volumeNameFlag);
            if (length == 0 || length >= buffer.Length)
            {
                // Still not fitting (or now failing) — refuse rather than
                // return a truncated path.
                return null;
            }
        }
        return new string(buffer, 0, (int)length);
    }

    private static SafeFileHandle OpenForQuery(string path) =>
        NativeIo.CreateFileW(
            path,
            0,
            NativeIo.FileShareRead | NativeIo.FileShareWrite | NativeIo.FileShareDelete,
            IntPtr.Zero,
            NativeIo.OpenExisting,
            NativeIo.FileFlagBackupSemantics,
            IntPtr.Zero);

    /// <summary>The OS's identity for a file object: same triple ⇒ same
    /// file, regardless of the path spelling used to reach it. The file
    /// id is always the 128-bit ReFS-safe one — there is no other identity
    /// method, so no value of this type is ever a weaker id.</summary>
    internal readonly record struct FileIdentity(
        ulong VolumeSerial, ulong FileIdLow, ulong FileIdHigh);

    private static bool NotFatal(Exception exception) =>
        exception is not OutOfMemoryException
            and not StackOverflowException
            and not AccessViolationException;

    private static class NativeIo
    {
        internal const uint FileShareRead = 0x00000001;
        internal const uint FileShareWrite = 0x00000002;
        internal const uint FileShareDelete = 0x00000004;
        internal const uint OpenExisting = 3;

        /// <summary>Lets a DIRECTORY handle be opened, so an ancestor
        /// directory resolves too; reparse points are followed (no
        /// OPEN_REPARSE_POINT), which is the point.</summary>
        internal const uint FileFlagBackupSemantics = 0x02000000;

        internal const uint VolumeNameDos = 0x0;
        internal const uint VolumeNameGuid = 0x1;

        /// <summary>FILE_INFO_BY_HANDLE_CLASS.FileIdInfo.</summary>
        private const int FileIdInfoClass = 18;

        internal static bool TryGetFileIdInfo(
            SafeFileHandle handle, out FileIdInformation info)
        {
            info = default;
            if (handle.IsInvalid)
            {
                return false;
            }
            return GetFileInformationByHandleEx(
                handle,
                FileIdInfoClass,
                out info,
                (uint)System.Runtime.InteropServices.Marshal.SizeOf<FileIdInformation>());
        }

        [System.Runtime.InteropServices.DllImport(
            "kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode,
            SetLastError = true)]
        internal static extern SafeFileHandle CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [System.Runtime.InteropServices.DllImport(
            "kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode,
            SetLastError = true)]
        internal static extern uint GetFinalPathNameByHandleW(
            SafeFileHandle hFile,
            [System.Runtime.InteropServices.Out] char[] lpszFilePath,
            uint cchFilePath,
            uint dwFlags);

        [System.Runtime.InteropServices.DllImport(
            "kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(
            System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(
            SafeFileHandle hFile,
            int fileInformationClass,
            out FileIdInformation lpFileInformation,
            uint dwBufferSize);

        /// <summary>FILE_ID_INFO: 64-bit volume serial + 128-bit file id
        /// (two ulongs = the 16-byte FILE_ID_128).</summary>
        [System.Runtime.InteropServices.StructLayout(
            System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct FileIdInformation
        {
            public ulong VolumeSerialNumber;
            public ulong FileIdLow;
            public ulong FileIdHigh;
        }
    }

    /// <summary>Test seam over <see cref="IdentityOf"/>, so a fact can
    /// pin the identity primitive the whole gate stands on.</summary>
    internal static FileIdentity? IdentityForTests(string path) => IdentityOf(path);

    /// <summary>Whether the 128-bit <c>FileIdInfo</c> query succeeds on
    /// this box: pins that the ReFS-safe primitive is real and available,
    /// so refusing on its failure costs nothing on a vault volume this gate
    /// supports (NTFS/ReFS — a filesystem that does not answer
    /// <c>FileIdInfo</c> refuses every media open, by design).</summary>
    internal static bool UsesFileIdInfoForTests(string path)
    {
        using SafeFileHandle handle = OpenForQuery(path);
        return !handle.IsInvalid && NativeIo.TryGetFileIdInfo(handle, out _);
    }

    /// <summary>
    /// Set only by tests: makes the PRIMARY identity query fail, so a
    /// fact can prove the whole resolution REFUSES rather than falling
    /// back to a weaker identity (codex round 5).
    /// </summary>
    /// <remarks>
    /// This injects failure at the one place a fallback would be
    /// reintroduced — between the primary query failing and the method
    /// returning null. A fallback arm added there would satisfy the
    /// injection and hand back a 64-bit identity, which is exactly the
    /// mutation `IdentityQueryFailureRefusesRatherThanDowngrading`
    /// detects.
    /// </remarks>
    /// <seealso cref="BetweenCheckAndLaunchForTests"/>
    // Static mutable state: while this is set, EVERY identity query in the
    // process refuses. That is safe only because the test assembly runs
    // serially — `[assembly: CollectionBehavior(DisableTestParallelization
    // = true)]` in AssemblyInfo.cs. If assembly-wide parallelization is
    // ever enabled, this seam (and BetweenCheckAndLaunchForTests, which has
    // the same exposure) must become scoped state rather than a static
    // flag, or it will fail unrelated facts nondeterministically.
    internal static bool FailIdentityQueryForTests { get; set; }

    /// <summary>Test seam over the volume-GUID resolution (Major-4): the
    /// fallback path when a driveless volume has no DOS name.</summary>
    internal static string? GuidNameForTests(string path)
    {
        using SafeFileHandle handle = OpenForQuery(path);
        return handle.IsInvalid ? null : FinalPath(handle, NativeIo.VolumeNameGuid);
    }
}
