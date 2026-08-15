// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.IO;
using uniffi.slate_uniffi;

namespace SlateWindows.Commands;

/// <summary>
/// The host half of the command palette's Recent section (contract P11) —
/// the twin of mac's <c>CommandPaletteRecentsStore</c>.
/// </summary>
/// <remarks>
/// <para>
/// The host owns exactly two things: the platform path and the file I/O.
/// <b>Core owns every state transition</b> — the byte format, the
/// malformed-tolerant decode, dedupe and cap, and the LRU move-to-front —
/// so this class never hand-rolls a list operation (PD-3: mac hand-rolls
/// the same LRU in Swift and does not call core; converging mac is a
/// follow-up).
/// </para>
/// <para>
/// The location is global and device-local, never per-vault:
/// <c>%LOCALAPPDATA%\Slate\command-palette-recents.json</c>, alongside
/// <c>recent-vaults.json</c>.
/// </para>
/// </remarks>
internal sealed class CommandPaletteRecentsStore
{
    /// <summary>Hard cap on persisted recents — mirror of core's
    /// <c>palette::RECENTS_MAX_ENTRIES</c>, kept host-side only so tests can
    /// name the boundary they exercise.</summary>
    public const int MaxEntries = 10;

    /// <summary>Upper bound on the bytes <see cref="Load"/> will accept.
    /// Mirror of core's <c>palette::RECENTS_MAX_FILE_BYTES</c> (64 KiB).
    /// The host bound is I/O hygiene; core's decode is the authority and
    /// independently enforces the same cap.</summary>
    public const int MaxFileBytes = 1 << 16;

    private readonly string _filePath;

    public CommandPaletteRecentsStore()
        : this(DefaultFilePath)
    {
    }

    /// <summary>Test seam: point the store at a temporary file.</summary>
    public CommandPaletteRecentsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        _filePath = filePath;
    }

    public static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Slate",
        "command-palette-recents.json");

    /// <summary>The last persistence failure, or <see langword="null"/>.
    /// Persistence failure is non-fatal by contract P11, but it must not be
    /// invisible.</summary>
    public Exception? LastSaveError { get; private set; }

    /// <summary>
    /// The on-disk list, or an empty list when the file is absent,
    /// unreadable, oversized, or malformed. A corrupt recents file must
    /// never block the palette opening.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Bounded read.</b> Open once and read at most
    /// <see cref="MaxFileBytes"/> + 1 bytes from that one handle — never
    /// stat-then-read, which leaves a TOCTOU window where the file can grow
    /// or be swapped between the size check and the read. One byte past the
    /// cap is what distinguishes "at or under the limit" from "over it", so
    /// the guard is <c>&gt;</c>: a file of exactly 65536 bytes is
    /// <b>accepted</b> and 65537 is refused <b>before</b> decoding, even
    /// when its JSON is perfectly valid.
    /// </para>
    /// </remarks>
    public string[] Load()
    {
        try
        {
            using var stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                FileOptions.SequentialScan);

            byte[] buffer = new byte[MaxFileBytes + 1];
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            if (total > MaxFileBytes)
            {
                return [];
            }

            return SlateUniffiMethods.PaletteRecentsDecode(buffer[..total]);
        }
        catch (Exception exception) when (
            exception is FileNotFoundException
                or DirectoryNotFoundException
                or IOException
                or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Atomically overwrite the on-disk list with <paramref name="ids"/>, in
    /// core's canonical v1 byte format. Atomic because a plain truncating
    /// write can tear and silently decode as empty on the next open.
    /// </summary>
    /// <returns><see langword="true"/> when the bytes landed.</returns>
    public bool TrySave(string[] ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        string temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(temporaryPath, SlateUniffiMethods.PaletteRecentsEncode(ids));
            File.Move(temporaryPath, _filePath, overwrite: true);
            LastSaveError = null;
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // Non-fatal by contract P11: the caller's in-memory list still
            // moves, so the open palette stays consistent with what the user
            // just did. Surfaced on LastSaveError rather than swallowed
            // silently; a HostLog diagnostic event for it is integration
            // scope (HostLog.cs is outside this slice).
            LastSaveError = exception;
            return false;
        }
        finally
        {
            SafeFile.TryDelete(temporaryPath);
        }
    }

    /// <summary>
    /// Move <paramref name="id"/> to the front through core's LRU
    /// transition, persist, and return the updated list. The returned list
    /// is correct even when persistence failed.
    /// </summary>
    public string[] Add(string[] current, string id)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentException.ThrowIfNullOrEmpty(id);
        string[] updated = SlateUniffiMethods.PaletteRecentsAdd(current, id);
        _ = TrySave(updated);
        return updated;
    }
}
