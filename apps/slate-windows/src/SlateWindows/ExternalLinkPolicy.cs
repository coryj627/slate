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
/// <b>The set is core's, copied because it is not exported.</b> Core
/// owns this classification in <c>canvas::model::media_class</c>
/// (<c>crates/slate-core/src/canvas/model.rs:661</c>) — the same
/// function whose answer becomes the <c>image</c> kind label and the
/// "Image:"/"Audio:"/"Video:" title prefixes — but it is a private
/// Rust fn with no <c>#[uniffi::export]</c>, so no host can ask for it.
/// This is a transliteration, including both of its edge rules
/// (basename only; a dotfile like <c>.mov</c> is a hidden file, not
/// media). <b>Drift note for PR E:</b> PR E is the first PR that needs
/// core's classification for its own reasons (the spec's Add Media
/// note — "media kinds by extension set — core's <c>media_class</c>
/// decides the label"), so PR E should export it and delete this copy.
/// Until then this is the one place the set lives host-side.
/// </para>
/// </remarks>
internal static class CanvasMediaPolicy
{
    /// <summary>Core's <c>MediaClass::Image</c> extensions.</summary>
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.Ordinal)
    {
        "png", "jpg", "jpeg", "gif", "svg", "webp", "bmp", "heic", "avif", "tiff",
    };

    /// <summary>Core's <c>MediaClass::Audio</c> extensions.</summary>
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.Ordinal)
    {
        "mp3", "wav", "m4a", "flac", "ogg", "aac",
    };

    /// <summary>Core's <c>MediaClass::Video</c> extensions.</summary>
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.Ordinal)
    {
        "mp4", "mov", "mkv", "webm", "m4v",
    };

    /// <summary>
    /// Whether this vault-relative target is media the canvas may hand
    /// to the shell. Core's <c>media_class(path).is_some()</c>.
    /// </summary>
    internal static bool IsOpenableMedia(string? target)
    {
        if (target is not { Length: > 0 })
        {
            return false;
        }
        // Core: the basename's REAL extension — a file with no `.` in
        // its basename (even one literally named `mov`) is not media.
        int slash = target.LastIndexOfAny(['/', '\\']);
        string basename = slash >= 0 ? target[(slash + 1)..] : target;
        int dot = basename.LastIndexOf('.');
        if (dot <= 0)
        {
            // No dot at all, or an empty stem — core's dotfile rule:
            // `.mov` is a hidden file, not a video.
            return false;
        }
        string extension = AsciiLowered(basename[(dot + 1)..]);
        return ImageExtensions.Contains(extension)
            || AudioExtensions.Contains(extension)
            || VideoExtensions.Contains(extension);
    }

    /// <summary>
    /// Open a media file card in its default app, or refuse — the whole
    /// containment decision, made against the target's OS FILE IDENTITY
    /// (volume serial + file index), revalidated by that identity
    /// immediately before the launch, and failing closed.
    /// <paramref name="launch"/> is the shell hand-off; it runs with
    /// NOTHING between it and the final identity check. Returns whether
    /// the shell was handed the file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three consecutive codex rounds found containment defects, and
    /// codex named the class: <b>filesystem identity reduced to path
    /// text</b>, where two different normalization or case rules on the
    /// same string disagree. Path text is retired as the decision
    /// substrate. Every containment and every "unchanged since check"
    /// question is answered by <c>GetFileInformationByHandle</c>'s
    /// <c>dwVolumeSerialNumber</c> + <c>nFileIndex</c>, the 64-bit key
    /// the OS uses to say two handles name the same file — immune to
    /// case, to trailing dots, to per-directory case sensitivity, to
    /// SUBST and to which spelling reached it.
    /// </para>
    /// <para>
    /// Resolution keeps the handle-resolved EXTENDED (<c>\\?\</c>) form
    /// end to end: it is verified and it is what is launched. The
    /// extended prefix is exactly what stops <c>ShellExecute</c>
    /// renormalizing <c>vault.\file</c> to <c>vault\file</c> — verifying
    /// one string and launching another was a real launch-integrity bug.
    /// </para>
    /// <para>
    /// The TOCTOU narrowing (CD-38): the target's identity is captured at
    /// check time; immediately before launch the resolved path is
    /// re-opened and its identity compared, and the launch happens only
    /// if the 64-bit identity is unchanged. This makes "unchanged since
    /// check" an OS guarantee rather than a string compare. The
    /// irreducible residual shrinks to the re-open→<c>ShellExecute</c>
    /// gap — a path-taking launcher re-opens by name, and closing that
    /// needs a handle-based launcher, which <c>ShellExecute</c> is not.
    /// </para>
    /// <para>
    /// Every failure mode is a refusal — a NUL character, a device name,
    /// a path too long, a cycle, a permission error — because an
    /// exception escaping to the caller would abort the activation
    /// silently rather than refuse it audibly.
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
            if (ResolveMediaTarget(vaultRoot, target, rootIdentity)
                is not var (resolved, checkIdentity))
            {
                return false;
            }

            // The TOCTOU window (CD-38): an in-vault attacker may swap the
            // namespace here. The test seam simulates it.
            BetweenCheckAndLaunchForTests?.Invoke();

            // Revalidate by IDENTITY immediately before launch — nothing
            // between this and launch(). Re-open the resolved path and
            // require the SAME 64-bit file identity; a swap that
            // redirected it changes the identity and the launch refuses.
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
            return ResolveMediaTarget(vaultRoot, target, rootIdentity) is var (resolved, _)
                ? resolved
                : null;
        }
        catch (Exception exception) when (NotFatal(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// Set only by tests: invoked once, after the first resolution and
    /// before the revalidation-and-launch, so a fact can act in the
    /// TOCTOU window the identity revalidation exists to catch.
    /// </summary>
    internal static Action? BetweenCheckAndLaunchForTests { get; set; }

    /// <summary>
    /// Resolve the vault-relative target to its terminal extended path
    /// and capture its identity, requiring the resolved identity chain
    /// to reach <paramref name="rootIdentity"/>. Null when it does not
    /// resolve, is not media, or is not contained.
    /// </summary>
    private static (string Resolved, FileIdentity Identity)? ResolveMediaTarget(
        string vaultRoot, string target, FileIdentity rootIdentity)
    {
        // Compose lexically only to name a starting point; the identity
        // decision below does not trust this string.
        string absolute = Path.GetFullPath(
            Path.Combine(Path.GetFullPath(vaultRoot), target));
        if (ResolveThroughHandle(absolute) is not { } resolved)
        {
            return null;
        }
        // The RESOLVED name must still be media: a `.png` whose chain
        // ends at an `.exe` is the case resolution exists for.
        if (!IsOpenableMedia(resolved))
        {
            return null;
        }
        if (!ReachesRootByIdentity(resolved, rootIdentity))
        {
            return null;
        }
        return IdentityOf(resolved) is { } identity ? (resolved, identity) : null;
    }

    /// <summary>
    /// Whether the resolved target's ancestor chain reaches the vault
    /// root BY IDENTITY — the containment decision, made on OS file
    /// identity, not path text.
    /// </summary>
    /// <remarks>
    /// The resolved path names canonical ancestors; each is opened and
    /// its <c>(volumeSerial, fileIndex)</c> compared to the root's. A
    /// case-sensitive-directory sibling (<c>C:\work\VAULT</c> vs
    /// <c>C:\work\vault</c>) that a text prefix falsely accepted is a
    /// DIFFERENT file object with a different identity, so it does not
    /// match — which is the defect this ends. The walk is depth-bounded
    /// against a cycle and is O(depth) handle opens on a user-initiated
    /// action.
    /// </remarks>
    private static bool ReachesRootByIdentity(string resolved, FileIdentity rootIdentity)
    {
        string? current = ParentOf(resolved);
        for (int depth = 0; depth < ResolveRounds && current is not null; depth++)
        {
            if (IdentityOf(current) is { } identity)
            {
                if (identity == rootIdentity)
                {
                    return true;
                }
            }
            else
            {
                // An ancestor we cannot open cannot be confirmed as the
                // root; fail closed rather than walk past it.
                return false;
            }
            string? next = ParentOf(current);
            if (next is null || string.Equals(next, current, StringComparison.Ordinal))
            {
                return false;
            }
            current = next;
        }
        return false;
    }

    /// <summary>The parent of an extended (<c>\\?\</c>) path, or null at
    /// the volume root. Text is used only to name the parent to open;
    /// the decision is identity.</summary>
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
    /// names — <c>(dwVolumeSerialNumber, nFileIndex)</c>. Null when it
    /// cannot be opened. Reparse points are followed, so this is the
    /// identity of the TERMINAL object.
    /// </summary>
    /// <summary>Test seam over <see cref="IdentityOf"/>, so a fact can
    /// pin the identity primitive the whole gate stands on.</summary>
    internal static FileIdentity? IdentityForTests(string path) => IdentityOf(path);

    /// <summary>Test seam over the volume-GUID resolution (Major-4): the
    /// fallback path when a driveless volume has no DOS name.</summary>
    internal static string? GuidNameForTests(string path)
    {
        using SafeFileHandle handle = OpenForQuery(path);
        return handle.IsInvalid ? null : FinalPath(handle, NativeIo.VolumeNameGuid);
    }

    private static FileIdentity? IdentityOf(string path)
    {
        using SafeFileHandle handle = OpenForQuery(path);
        if (handle.IsInvalid)
        {
            return null;
        }
        if (!NativeIo.GetFileInformationByHandle(
            handle, out NativeIo.ByHandleFileInformation info))
        {
            return null;
        }
        ulong index = ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow;
        return new FileIdentity(info.VolumeSerialNumber, index);
    }

    /// <summary>
    /// The terminal extended (<c>\\?\</c>) path of <paramref name="path"/>
    /// — every reparse point resolved by the OS — or null. Kept in the
    /// extended form: it is verified and launched unchanged, so
    /// <c>ShellExecute</c> cannot renormalize a trailing dot or space out
    /// from under the check. Falls back to the volume-GUID name when a
    /// driveless (folder-mounted) volume has no DOS path.
    /// </summary>
    private static string? ResolveThroughHandle(string path)
    {
        using SafeFileHandle handle = OpenForQuery(path);
        if (handle.IsInvalid)
        {
            return null;
        }
        return FinalPath(handle, NativeIo.VolumeNameDos)
            ?? FinalPath(handle, NativeIo.VolumeNameGuid);
    }

    private static string? FinalPath(SafeFileHandle handle, uint volumeNameFlag)
    {
        var buffer = new char[1024];
        uint length = NativeIo.GetFinalPathNameByHandleW(
            handle, buffer, (uint)buffer.Length, volumeNameFlag);
        if (length == 0 || length >= buffer.Length)
        {
            // 0 = failure (e.g. ERROR_PATH_NOT_FOUND for a driveless
            // volume under VOLUME_NAME_DOS — the caller falls back to
            // the GUID name); >= length = it did not fit. Either way,
            // null.
            return null;
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

    /// <summary>The OS's identity for a file object: same 64-bit key ⇒
    /// same file, regardless of the path spelling used to reach it.</summary>
    internal readonly record struct FileIdentity(uint VolumeSerial, ulong FileIndex);

    private static bool NotFatal(Exception exception) =>
        exception is not OutOfMemoryException
            and not StackOverflowException
            and not AccessViolationException;

    /// <summary>Bounds an ancestor-chain cycle.</summary>
    private const int ResolveRounds = 64;

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
        internal static extern bool GetFileInformationByHandle(
            SafeFileHandle hFile,
            out ByHandleFileInformation lpFileInformation);

        [System.Runtime.InteropServices.StructLayout(
            System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public uint CreationTimeLow;
            public uint CreationTimeHigh;
            public uint LastAccessTimeLow;
            public uint LastAccessTimeHigh;
            public uint LastWriteTimeLow;
            public uint LastWriteTimeHigh;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }
    }

    /// <summary>
    /// Core's <c>to_ascii_lowercase</c>, not .NET's
    /// <c>ToLowerInvariant</c>.
    /// </summary>
    /// <remarks>
    /// The two differ outside ASCII — <c>ToLowerInvariant</c> lowers the
    /// Kelvin sign to <c>k</c> and İ to <c>i̇</c>, Rust's leaves both
    /// alone — and this set is compared against ASCII literals, so the
    /// difference can only ever ADMIT something core would classify as
    /// not-media. That is the wrong direction for a gate that decides
    /// what reaches <c>ShellExecute</c>, so it matches core exactly
    /// rather than approximately.
    /// </remarks>
    private static string AsciiLowered(string value)
    {
        Span<char> lowered = value.Length <= 32
            ? stackalloc char[value.Length]
            : new char[value.Length];
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            lowered[index] = character is >= 'A' and <= 'Z'
                ? (char)(character + 32)
                : character;
        }
        return new string(lowered);
    }
}
