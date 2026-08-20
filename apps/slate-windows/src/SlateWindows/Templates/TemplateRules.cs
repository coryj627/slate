// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Globalization;
using System.Text;

namespace SlateWindows.Templates;

/// <summary>
/// The pure rules of the create-from-template flow (W5-3, #743):
/// name validation, the default-name seed, `.md` normalization, and
/// the destination join. Static and side-effect-free so every rule is
/// pinned by unit facts rather than only observable through a live
/// sheet — mac's twins live inline in <c>AppState.swift</c>
/// (<c>validateTemplateNoteName</c>, <c>defaultNewNoteName</c>,
/// <c>isDailyTemplateName</c>) and their strings are carried verbatim.
/// </summary>
internal static class TemplateNameRules
{
    /// <summary>
    /// Reject a new-note name early so the user sees a useful inline
    /// error rather than a late create failure (contracts doc T6).
    /// Returns the mac-verbatim error, or <see langword="null"/> for a
    /// valid name. Mirrors core's <c>validate_save_path</c> rules plus
    /// the TD-4 platform-absolute arm (drive-rooted and UNC forms are
    /// absolute on Windows even though mac only sees <c>/</c>).
    /// </summary>
    public static string? Validate(string candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Length == 0)
        {
            return "Note name cannot be empty.";
        }

        if (candidate is "." or "..")
        {
            return "Note name cannot be `.` or `..`.";
        }

        if (candidate.StartsWith('/')
            || candidate.StartsWith('\\')
            || IsDriveRooted(candidate))
        {
            return "Note name must be vault-relative, not absolute.";
        }

        // Any segment equal to `..` is a path traversal. Block both
        // `../foo.md` and `foo/../bar.md` (mac's exact rule).
        foreach (string segment in candidate.Split('/'))
        {
            if (segment == "..")
            {
                return "Note name cannot contain `..` segments.";
            }
        }

        return null;
    }

    /// <summary>
    /// Append <c>.md</c> unless the path extension is already `md`
    /// case-insensitively. <c>archive.tar.MD</c> is left alone rather
    /// than becoming <c>archive.tar.MD.md</c>, and any-case `.md`
    /// keeps the user's casing (mac's <c>pathExtension</c> rule,
    /// Codoki PR #154 / #133).
    /// </summary>
    public static string NormalizeNoteName(string trimmed)
    {
        ArgumentNullException.ThrowIfNull(trimmed);
        string extension = System.IO.Path.GetExtension(trimmed);
        bool alreadyMarkdown = extension.Length == 3
            && string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase);
        return alreadyMarkdown ? trimmed : $"{trimmed}.md";
    }

    /// <summary>
    /// The name field's seed for a freshly selected template:
    /// templates whose name starts with the standalone word `daily`
    /// get a space-joined UTC date suffix so the user can
    /// confirm-Enter on the daily-note flow; everything else
    /// pre-fills with the template's own name (mac
    /// <c>defaultNewNoteName</c>, including the UTC choice — TR-4).
    /// </summary>
    public static string DefaultNoteName(string templateName, DateTime utcNow) =>
        IsDailyTemplateName(templateName)
            ? $"{templateName} {utcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.md"
            : $"{templateName}.md";

    /// <summary>
    /// <see langword="true"/> iff <paramref name="name"/> begins with
    /// the standalone word `daily` (case-insensitive): `Daily`,
    /// `Daily Standup`, `Daily-notes` qualify; `Dailyness` and
    /// `Daily123` do not (mac <c>isDailyTemplateName</c>).
    /// </summary>
    public static bool IsDailyTemplateName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!name.StartsWith("daily", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (name.Length == "daily".Length)
        {
            return true;
        }

        char next = name["daily".Length];
        return !char.IsLetter(next) && !char.IsDigit(next);
    }

    /// <summary>
    /// The vault-relative creation path: the frozen destination joined
    /// to the normalized name with a forward slash; the empty
    /// destination is the vault root (contracts doc T12).
    /// </summary>
    public static string CreationPath(string destination, string normalizedName)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(normalizedName);
        return destination.Length == 0
            ? normalizedName
            : $"{destination}/{normalizedName}";
    }

    /// <summary>
    /// The destination as every sheet's subtitle renders it (mac
    /// <c>templateCreationDestinationDescription</c>).
    /// </summary>
    public static string DestinationDescription(string destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return destination.Length == 0 ? "the vault root" : destination;
    }

    private static bool IsDriveRooted(string candidate) =>
        candidate.Length >= 2
        && candidate[1] == ':'
        && char.IsAsciiLetter(candidate[0]);
}

/// <summary>
/// The `{{cursor}}` coordinate conversion (contracts doc T8). Core
/// reports a UTF-8 byte offset into the whole rendered file,
/// guaranteed to fall on a char boundary; the Windows buffer is
/// whole-file text, so the only conversion is UTF-8 bytes → UTF-16
/// code units — no body rebase (mac's <c>bodyByte(fromFileByte:)</c>
/// exists because its buffer is body-only; porting it would be wrong
/// here).
/// </summary>
internal static class TemplateCursor
{
    /// <summary>
    /// The UTF-16 caret index for a rendered body and its cursor byte
    /// offset, or the end of the text when the template carried no
    /// marker (mac's observable resting state, made deliberate — T8).
    /// An out-of-range or boundary-splitting offset clamps to the end
    /// rather than throwing: the render is the authority, and a caret
    /// at the end is the honest degradation.
    /// </summary>
    public static int CaretIndex(string body, ulong? cursorByteOffset)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (cursorByteOffset is not { } offset)
        {
            return body.Length;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(body);
        if (offset >= (ulong)bytes.Length)
        {
            return body.Length;
        }

        int prefixBytes = (int)offset;
        // A char-boundary offset decodes cleanly; a hostile offset
        // (impossible per the FFI contract, clamped anyway) would
        // produce replacement chars whose UTF-16 length still lands
        // inside the string, never past it.
        return Encoding.UTF8.GetString(bytes, 0, prefixBytes).Length;
    }
}
