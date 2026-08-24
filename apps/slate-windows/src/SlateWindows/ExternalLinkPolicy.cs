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
    /// containment decision, made against the target's PHYSICAL identity
    /// through an OPENED HANDLE, revalidated immediately before the
    /// launch, and failing closed. <paramref name="launch"/> is the
    /// shell hand-off; it runs with NOTHING between it and the final
    /// containment check. Returns whether the shell was handed the file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The threat model is precise: an untrusted sync peer with
    /// write access inside the vault, racing the local click (CD-38).
    /// The naive gate resolved a path STRING; <c>ShellExecute</c>
    /// re-opens by path and re-resolves the namespace from scratch, so
    /// the attacker could swap a checked in-vault directory for an
    /// outward junction in between and the shell would follow the swap.
    /// A full close is impossible: <c>ShellExecute</c> takes no handle,
    /// it takes a path, and it will resolve that path itself.
    /// </para>
    /// <para>
    /// Two things shrink the window to near-zero. First, resolution goes
    /// through an OPENED HANDLE (<c>GetFinalPathNameByHandle</c> over a
    /// handle opened with backup semantics, so directories resolve too),
    /// and the path handed to <paramref name="launch"/> is the
    /// FULLY-RESOLVED terminal path — so <c>ShellExecute</c>'s own
    /// re-resolution walks a path with no reparse points left in it that
    /// the click named, and swapping the named junction afterward
    /// redirects nothing. Second, containment is checked once, and then
    /// RE-checked by re-resolving the named path immediately before
    /// launch with no work in between; a swap in that window changes the
    /// re-resolution and the launch is refused.
    /// </para>
    /// <para>
    /// The irreducible residual, stated rather than pretended closed
    /// (CD-38): the sub-instruction gap between that final re-resolution
    /// and <c>ShellExecute</c>'s own resolution cannot be closed with a
    /// path-taking launcher. The future-work shape is a launcher that
    /// accepts a HANDLE (or a verb executed against the open handle);
    /// <c>ShellExecute</c> is not one. HARDLINKS remain invisible to any
    /// path resolution (a second directory entry for the same data, not
    /// a reparse point), bounded as before: same volume, never a
    /// directory, requires existing in-vault write access, and the
    /// extension gate still applies to the name opened.
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
            string root = ResolveThroughHandle(Path.GetFullPath(vaultRoot))
                ?? Path.TrimEndingDirectorySeparator(Path.GetFullPath(vaultRoot));
            if (ResolveMediaTarget(root, target) is not { } resolved)
            {
                return false;
            }

            // The TOCTOU window (CD-38): an in-vault attacker may swap
            // the namespace here. The test seam simulates it.
            BetweenCheckAndLaunchForTests?.Invoke();

            // Revalidate IMMEDIATELY before launch — nothing between this
            // and launch(). Re-resolve the NAMED path (what the attacker
            // controls) and require the identical terminal identity.
            if (ResolveMediaTarget(root, target) is not { } revalidated
                || !string.Equals(revalidated, resolved, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return launch(revalidated);
        }
        catch (Exception exception) when (NotFatal(exception))
        {
            return false;
        }
    }

    /// <summary>
    /// The pure resolution, for the facts that assert containment
    /// without launching: the fully-resolved terminal path when it is
    /// media inside the vault, else null.
    /// </summary>
    internal static string? ResolveInsideVault(string vaultRoot, string target)
    {
        try
        {
            if (!IsOpenableMedia(target))
            {
                return null;
            }
            string root = ResolveThroughHandle(Path.GetFullPath(vaultRoot))
                ?? Path.TrimEndingDirectorySeparator(Path.GetFullPath(vaultRoot));
            return ResolveMediaTarget(root, target);
        }
        catch (Exception exception) when (NotFatal(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// Set only by tests: invoked once, after the first resolution and
    /// before the revalidation-and-launch, so a fact can act in the
    /// TOCTOU window the revalidation exists to catch.
    /// </summary>
    internal static Action? BetweenCheckAndLaunchForTests { get; set; }

    private static string? ResolveMediaTarget(string root, string target)
    {
        string absolute = Path.GetFullPath(Path.Combine(root, target));
        if (!File.Exists(absolute))
        {
            return null;
        }
        if (ResolveThroughHandle(absolute) is not { } resolved)
        {
            return null;
        }
        if (!IsInsideRoot(resolved, root))
        {
            return null;
        }
        // The RESOLVED name must still be media: a `.png` whose chain
        // ends at an `.exe` is the case resolution exists for.
        return IsOpenableMedia(resolved) ? resolved : null;
    }

    /// <summary>
    /// Containment that treats the root correctly when it IS a drive
    /// root. <c>C:\photo.png</c> is inside <c>C:\</c>; the file must be
    /// UNDER the root, never BE it, and never on another volume.
    /// </summary>
    /// <remarks>
    /// Major-1: a hand-built <c>root + separator</c> prefix produced
    /// <c>C:\\</c> for a drive-root vault, which no in-vault path starts
    /// with, so every media file under such a vault was refused.
    /// <c>GetRelativePath</c> treats the root correctly whether or not it
    /// is a drive root and is case-insensitive on Windows, so there is no
    /// prefix to get wrong: a path inside the root gives a relative
    /// result that neither is <c>.</c>, escapes upward with <c>..</c>,
    /// nor comes back rooted (a rooted result means a different volume).
    /// </remarks>
    internal static bool IsInsideRoot(string path, string root)
    {
        string relative = Path.GetRelativePath(root, path);
        return relative.Length > 0
            && relative != "."
            && !relative.StartsWith("..", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    /// <summary>
    /// The terminal filesystem path of <paramref name="path"/> — EVERY
    /// reparse point resolved (leaf and every ancestor) — via a handle
    /// opened to it, or null when it cannot be opened. This is the OS's
    /// own resolution, not a hand-rolled ancestor walk: the handle binds
    /// to a file object and <c>GetFinalPathNameByHandle</c> reports that
    /// object's canonical path.
    /// </summary>
    private static string? ResolveThroughHandle(string path)
    {
        using SafeFileHandle handle = NativeIo.CreateFileW(
            path,
            0,
            NativeIo.FileShareRead | NativeIo.FileShareWrite | NativeIo.FileShareDelete,
            IntPtr.Zero,
            NativeIo.OpenExisting,
            NativeIo.FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return null;
        }
        var buffer = new char[1024];
        uint length = NativeIo.GetFinalPathNameByHandleW(
            handle, buffer, (uint)buffer.Length, 0);
        if (length == 0 || length >= buffer.Length)
        {
            // 0 = failure; >= length = the path did not fit (a path we
            // will not hand the shell anyway). Fail closed.
            return null;
        }
        string resolved = new(buffer, 0, (int)length);
        // GetFinalPathNameByHandle returns the \\?\ (or \\?\UNC\) form;
        // strip the local prefix, leave UNC alone (it is refused by the
        // containment check against a local vault root anyway).
        const string localPrefix = @"\\?\";
        if (resolved.StartsWith(localPrefix, StringComparison.Ordinal)
            && !resolved.StartsWith(@"\\?\UNC\", StringComparison.Ordinal))
        {
            resolved = resolved[localPrefix.Length..];
        }
        return Path.TrimEndingDirectorySeparator(resolved);
    }

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

        /// <summary>Lets a DIRECTORY handle be opened, so a junction leaf
        /// resolves too; reparse points are followed (no
        /// OPEN_REPARSE_POINT), which is the point.</summary>
        internal const uint FileFlagBackupSemantics = 0x02000000;

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
