// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

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
