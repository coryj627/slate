// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Text.Json;

namespace SlateWindows.Search;

/// <summary>
/// Per-vault persistence for the search overlay's recent-query list —
/// the interoperating twin of mac's <c>SearchRecentsStore</c>
/// (<c>SearchRecentsStore.swift:33-124</c>), contract S14.
/// </summary>
/// <remarks>
/// <para>
/// The store is a <b>vault artifact</b> — <c>{vault}/.slate/search-recents.json</c>,
/// a JSON array of raw query strings, most-recent-first — and vaults
/// move between platforms, so this class must read what mac wrote and
/// write what mac reads, not merely imitate the shape.
/// </para>
/// <para>
/// Every read and write goes through the W1 anchored-vault discipline:
/// <see cref="AnchoredVaultStore"/> holds the vault root and its
/// <c>.slate</c> directory open, verifies both against external reparse
/// points, and renames the temp file by handle — the same fail-closed
/// mechanism <c>sidebar.json</c> and <c>workspace.json</c> use. The
/// file is never opened naively. No lock file is taken: like mac's
/// store, this is single-writer by design (one user, one process), and
/// <see cref="Add"/> is read → mutate → atomic-write per call.
/// </para>
/// <para>
/// A missing, malformed, oversized, or unreadable file degrades to an
/// <b>empty list, never a throw</b> — the overlay must open regardless
/// of recents state. Write failures are equally non-fatal, surfaced on
/// <see cref="LastSaveError"/> (the palette recents-store precedent);
/// callers re-read via <see cref="Load"/> so a failed
/// <see cref="Clear"/> keeps showing the still-persisted list rather
/// than pretending it was forgotten.
/// </para>
/// </remarks>
internal sealed class SearchRecentsStore
{
    /// <summary>Hard cap on persisted recent queries — mirror of mac's
    /// <c>maxEntries</c>. Enforced on load (dedupe, first occurrence
    /// wins, short-circuit at the cap) and on <see cref="Add"/>.</summary>
    internal const int MaxEntries = 20;

    /// <summary>Upper bound on the bytes <see cref="Load"/> will accept —
    /// mirror of mac's <c>maxFileBytes</c> (64 KiB). The bounded read
    /// detects the first byte past the cap from the open handle (no
    /// stat-then-read TOCTOU window) and treats a larger file as
    /// malformed.</summary>
    internal const int MaxFileBytes = 64 * 1024;

    private const string FileName = "search-recents.json";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _vaultRoot;

    public SearchRecentsStore(string vaultRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(vaultRoot);
        _vaultRoot = Path.GetFullPath(vaultRoot);
    }

    /// <summary>The last persistence failure, or <see langword="null"/>.
    /// Non-fatal, but not invisible.</summary>
    public Exception? LastSaveError { get; private set; }

    /// <summary>
    /// The on-disk list (most-recent-first), or empty on a missing,
    /// malformed, oversized, or unreadable file. Dedupes on load and
    /// short-circuits at <see cref="MaxEntries"/>.
    /// </summary>
    /// <remarks>
    /// A JSON array containing a non-string element is treated as
    /// malformed in whole (empty list), matching mac's
    /// <c>JSONDecoder().decode([String].self)</c>, which rejects the
    /// entire file rather than skipping the entry.
    /// </remarks>
    public IReadOnlyList<string> Load()
    {
        try
        {
            using AnchoredVaultStore? store = AnchoredVaultStore.Open(
                _vaultRoot,
                createDirectory: false);
            byte[]? input = store?.ReadAllBytesBounded(FileName, MaxFileBytes);
            if (input is null)
            {
                return [];
            }

            string?[]? decoded = JsonSerializer.Deserialize<string?[]>(input);
            if (decoded is null)
            {
                return [];
            }

            var entries = new List<string>(decoded.Length);
            foreach (string? entry in decoded)
            {
                if (entry is null)
                {
                    return [];
                }

                entries.Add(entry);
            }

            return Sanitize(entries);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // FileSizeLimitExceededException (oversized) and
            // DirectoryNotFoundException (no vault root) are IOExceptions
            // and land here too.
            return [];
        }
    }

    /// <summary>
    /// Add a query, moving an existing entry to the front (LRU: remove
    /// equal, insert front, cap, atomic save). Returns the updated
    /// in-memory list, correct even when persistence failed.
    /// </summary>
    public IReadOnlyList<string> Add(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var entries = Load().ToList();
        entries.RemoveAll(entry => string.Equals(entry, query, StringComparison.Ordinal));
        entries.Insert(0, query);
        if (entries.Count > MaxEntries)
        {
            entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        }

        Save(entries);
        return entries;
    }

    /// <summary>
    /// Forget every remembered query. Persists an empty list rather
    /// than deleting the file, so the subsequent load path is identical
    /// (contract S14).
    /// </summary>
    public void Clear() => Save([]);

    private void Save(IReadOnlyList<string> queries)
    {
        try
        {
            using AnchoredVaultStore store = AnchoredVaultStore.Open(
                _vaultRoot,
                createDirectory: true)
                ?? throw new IOException("Could not anchor the search-recents directory.");
            // Pretty-printed like mac's encoder; the schema either side
            // reads is simply "a JSON array of strings".
            byte[] output = JsonSerializer.SerializeToUtf8Bytes(queries, WriteOptions);
            store.WriteAtomically(FileName, output);
            LastSaveError = null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            LastSaveError = exception;
        }
    }

    private static IReadOnlyList<string> Sanitize(IReadOnlyList<string> queries)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var output = new List<string>(MaxEntries);
        foreach (string query in queries)
        {
            if (seen.Add(query))
            {
                output.Add(query);
                if (output.Count == MaxEntries)
                {
                    break;
                }
            }
        }

        return output;
    }
}
