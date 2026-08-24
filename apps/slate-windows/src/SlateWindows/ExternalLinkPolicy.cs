// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;

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
    /// The absolute path to hand the shell, or null to refuse — the
    /// whole containment decision, made against the target's PHYSICAL
    /// identity and failing closed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A textual prefix check is not containment: a symlink or a
    /// directory junction inside the vault names a file anywhere on the
    /// disk while its path still starts with the vault root. Every
    /// reparse point on the way — the leaf AND every ancestor directory
    /// — is resolved, and it is the fully resolved identity that has to
    /// be inside the resolved vault root.
    /// </para>
    /// <para>
    /// <b>What this does not cover, stated rather than implied.</b>
    /// HARDLINKS are invisible to it: a hardlink is a second directory
    /// entry for the same file data, not a reparse point, so
    /// <c>ResolveLinkTarget</c> returns null for one and there is no
    /// "real" path to resolve to — the in-vault name IS a real name for
    /// that file. An earlier version of this comment claimed hardlink
    /// coverage; that was false. The residual is bounded by what a
    /// hardlink can be: same volume only, no directories, and it must be
    /// created by something that already has write access inside the
    /// vault. What it cannot do is reach a file the vault's own
    /// filesystem cannot reach, and the extension gate still applies to
    /// the name being opened. Closing it needs file-identity comparison
    /// (volume serial + file index) against a vault-wide enumeration,
    /// which is a different and much more expensive check; recorded in
    /// CD-38 as the accepted residual rather than pretended away.
    /// </para>
    /// <para>
    /// Path comparison is ORDINAL while Windows paths are
    /// case-insensitive, so a casing difference between the resolved
    /// root and the resolved target refuses a file that is in fact
    /// inside the vault. That is a false REFUSAL — the fail-closed
    /// direction — and it is left as it is: both sides come from
    /// <c>GetFullPath</c> over the same root, so it needs a filesystem
    /// that reported two spellings, and admitting a case-insensitive
    /// compare here would widen what reaches the shell to buy back a
    /// case nobody has hit.
    /// </para>
    /// <para>
    /// Every failure mode is a refusal, including the ones the framework
    /// raises: a target with a NUL character, an invalid device name, a
    /// path too long, a cycle in the link chain, a permission error
    /// reading the link. Any of them throws somewhere in here, and an
    /// exception escaping to the caller would abort the activation
    /// without a word rather than refuse it audibly — so the whole thing
    /// is wrapped, and the answer is always "no" when anything at all
    /// goes wrong. Fail closed: this decides what reaches
    /// <c>ShellExecute</c>.
    /// </para>
    /// </remarks>
    internal static string? ResolveInsideVault(string vaultRoot, string target)
    {
        try
        {
            if (!IsOpenableMedia(target))
            {
                return null;
            }
            string root = ResolveVaultRoot(vaultRoot);
            string absolute = Path.GetFullPath(Path.Combine(root, target));
            if (!File.Exists(absolute))
            {
                return null;
            }
            if (ResolveUnderRoot(absolute, root) is not { } resolved)
            {
                return null;
            }
            if (!resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return null;
            }
            // The RESOLVED identity must still be media: a `.png` whose
            // link chain ends at an `.exe` is the case resolution exists
            // for.
            return IsOpenableMedia(resolved) ? resolved : null;
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
        {
            return null;
        }
    }

    /// <summary>
    /// The vault root's own final identity: the root may itself be a
    /// link. Ancestors ABOVE it are deliberately not walked — every one
    /// of them is shared with the target, so a link up there moves both
    /// sides identically and cannot move a file out of the vault.
    /// </summary>
    /// <remarks>
    /// Not walking them is also what keeps this off the volume root:
    /// <c>ResolveLinkTarget</c> on <c>C:\</c> throws, and a version of
    /// this that interrogated it refused every file in the vault. A
    /// throw here degrades to the unresolved root rather than refusing,
    /// because the root is the reference point both sides are measured
    /// against — an attacker gains nothing from it.
    /// </remarks>
    private static string ResolveVaultRoot(string vaultRoot)
    {
        string current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(vaultRoot));
        for (int round = 0; round < ResolveRounds; round++)
        {
            FileSystemInfo? target;
            try
            {
                target = new DirectoryInfo(current).ResolveLinkTarget(returnFinalTarget: true);
            }
            catch (IOException)
            {
                return current;
            }
            catch (UnauthorizedAccessException)
            {
                return current;
            }
            if (target is null)
            {
                return current;
            }
            string next = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(target.FullName));
            if (string.Equals(next, current, StringComparison.Ordinal))
            {
                return current;
            }
            current = next;
        }
        return current;
    }

    /// <summary>
    /// The target's final identity, resolving the leaf and every
    /// reparse point on the way down from <paramref name="root"/>. Null
    /// when the chain cannot be resolved — fail closed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolving only the leaf is not enough, and the gap is trivially
    /// reachable: a DIRECTORY junction inside the vault whose target is
    /// outside it, holding an ordinary `.png`. The leaf is not a link,
    /// so <c>ResolveLinkTarget</c> answers null for it, and the lexical
    /// path still begins with the vault root — the file opens. Junctions
    /// need neither elevation nor Developer Mode (<c>mklink /J</c>), so
    /// the "symlinks are privileged" argument never covered them.
    /// </para>
    /// <para>
    /// Only ancestors strictly INSIDE the vault are interrogated: one
    /// at or above the root is shared with the root itself and cannot
    /// move anything out. The walk substitutes the deepest linked
    /// ancestor and starts again, since a resolved target can sit under
    /// another link; the round budget bounds a cycle. Any exception
    /// inside the subtree is a refusal — this decides what reaches
    /// <c>ShellExecute</c>.
    /// </para>
    /// </remarks>
    private static string? ResolveUnderRoot(string absolute, string root)
    {
        string prefix = root + Path.DirectorySeparatorChar;
        string current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(absolute));
        for (int round = 0; round < ResolveRounds; round++)
        {
            FileSystemInfo leaf = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : new FileInfo(current);
            if (leaf.ResolveLinkTarget(returnFinalTarget: true) is { } leafTarget)
            {
                string next = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(leafTarget.FullName));
                if (string.Equals(next, current, StringComparison.Ordinal))
                {
                    return current;
                }
                current = next;
                continue;
            }

            string? rewritten = null;
            for (DirectoryInfo? ancestor = Directory.GetParent(current);
                ancestor is not null;
                ancestor = ancestor.Parent)
            {
                string ancestorPath = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(ancestor.FullName));
                if (!ancestorPath.StartsWith(prefix, StringComparison.Ordinal))
                {
                    // At or outside the root: nothing above here can
                    // move the target out of a vault it is measured
                    // against with the same ancestors.
                    break;
                }
                if (ancestor.ResolveLinkTarget(returnFinalTarget: true)
                    is not { } ancestorTarget)
                {
                    continue;
                }
                rewritten = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(ancestorTarget.FullName))
                    + current[ancestorPath.Length..];
                break;
            }
            if (rewritten is null
                || string.Equals(rewritten, current, StringComparison.Ordinal))
            {
                return current;
            }
            current = rewritten;
        }
        // A chain this deep is a cycle or an attack; fail closed.
        return null;
    }

    /// <summary>Bounds a link cycle.</summary>
    private const int ResolveRounds = 32;

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
