// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// Device-local, per-vault LRU shared by the sidebar and Quick Open. Plan 44
/// WB-28 PR 2 (design item 7): a persistent root identity keys the file, so
/// the list follows the vault through a rename; the path-keyed file of
/// earlier builds and the old in-vault <c>.slate/file-recents.json</c>
/// migrate into it, each source deleted only after a confirmed commit. A
/// weak identity keeps the path key and migrates nothing but the in-vault
/// file.
/// </summary>
internal sealed class FileRecentsStore
{
    internal const int MaxEntries = 50;
    internal const int MaxEntryLength = 4096;
    internal const int MaxPayloadBytes = 256 * 1024;
    private const string LegacyFileName = "file-recents.json";
    private readonly string _path;
    private readonly string _pathKeyedPath;
    private readonly string _vaultRoot;
    private readonly VaultRootAnchor? _root;

    // The in-vault step is settled once the vault has no old file, or its
    // entries are committed here and the file deleted; until then every
    // Load retries it, so a file unreadable at the first load still merges.
    private bool _legacySettled;

    /// <summary>
    /// The root this store reaches the vault through (plan 44 WB-28): the
    /// session's held root in production; a store built with no anchor
    /// opens by path, which only the tests may do.
    /// </summary>
    private VaultRootAnchor Root => _root ?? VaultRootAnchor.ByPathForTests(_vaultRoot);

    public FileRecentsStore(
        string vaultRoot,
        VaultRootIdentity? identity = null,
        string? localAppDataRoot = null,
        VaultRootAnchor? root = null)
    {
        _vaultRoot = Path.GetFullPath(vaultRoot);
        _root = root;
        string directory = localAppDataRoot ?? Path.Combine(
            SlateDeviceState.Root,
            "file-recents");
        _pathKeyedPath = Path.Combine(directory, $"{KeyOf(_vaultRoot.ToUpperInvariant())}.json");
        // A weak identity (FAT, FAT32, exFAT) changes with a rename and can
        // be reused or cloned, so it keys nothing durable: the path key.
        _path = identity is { Persistent: true }
            ? Path.Combine(directory, $"{KeyOf(IdentityText(identity))}.json")
            : _pathKeyedPath;
    }

    /// <summary>Test seam (#1331): the file this store reads and
    /// writes.</summary>
    internal string FilePathForTests => _path;

    /// <summary>Test seam: the path-keyed file of earlier builds.</summary>
    internal string PathKeyedPathForTests => _pathKeyedPath;

    /// <summary>Test seam: runs in either migration after the destination's
    /// read and before the merge (a fact changes the destination there).</summary>
    internal Action? AfterDestinationReadForTests { get; set; }

    /// <summary>Test seam: each anchored open the in-vault step makes.</summary>
    internal int LegacyProbesForTests { get; private set; }

    /// <summary>The lossless identity text the key hashes (WB-28 item 7).</summary>
    internal static string IdentityText(VaultRootIdentity identity) =>
        $"{identity.Volume:x16}-{identity.IdHigh:x16}{identity.IdLow:x16}";

    public IReadOnlyList<string> Load()
    {
        IReadOnlyList<string>? pathMerged = MigratePathKeyed();
        if (!_legacySettled && MigrateLegacy() is { } legacyMerged)
        {
            return legacyMerged;
        }

        return pathMerged ?? Read(_path);
    }

    public IReadOnlyList<string> Add(string path)
    {
        var entries = Load().ToList();
        entries.RemoveAll(candidate =>
            string.Equals(candidate, path, StringComparison.OrdinalIgnoreCase));
        entries.Insert(0, path);
        IReadOnlyList<string> sanitized = Sanitize(entries);
        Save(sanitized);
        return sanitized;
    }

    public void Clear()
    {
        if (!TryCommit([]))
        {
            return;
        }

        // Clear means none, so no source may merge its entries back: the
        // path-keyed file goes, and so does an unmigrated in-vault file.
        DropPathKeyedSource();
        if (_legacySettled)
        {
            return;
        }

        // Clear means none: the unmigrated in-vault file goes too, and the
        // step settles whether or not that delete succeeded.
        try
        {
            LegacyProbesForTests++;
            using AnchoredVaultStore? store = AnchoredVaultStore.Open(Root, createDirectory: false);
            store?.DeleteRegularFileIfExists(LegacyFileName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _ = exception;
        }

        _legacySettled = true;
    }

    public IReadOnlyList<string> Replace(IEnumerable<string> paths)
    {
        IReadOnlyList<string> sanitized = Sanitize(paths);
        // The replacement came from a Load that merged a readable
        // path-keyed source, so a source still there would bring back the
        // paths it replaced; one that cannot be read now keeps its entries.
        bool sourceRead = TryRead(_pathKeyedPath) is RecentsRead.Read;
        if (TryCommit(sanitized) && sourceRead)
        {
            DropPathKeyedSource();
        }

        return sanitized;
    }

    private void DropPathKeyedSource()
    {
        if (!string.Equals(_pathKeyedPath, _path, StringComparison.OrdinalIgnoreCase))
        {
            DeleteSource(_pathKeyedPath);
        }
    }

    // A consumed source goes even when it is read-only (std's delete
    // ignores the attribute too): left behind, it would merge back.
    private static void DeleteSource(string path)
    {
        try
        {
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _ = exception;
        }

        SafeFile.TryDelete(path);
    }

    /// <summary>
    /// The path-keyed file into the identity-keyed one, whenever it exists:
    /// null when there is nothing to migrate or the migration stopped short
    /// of a merge; the merged list (committed or not) otherwise.
    /// </summary>
    private IReadOnlyList<string>? MigratePathKeyed()
    {
        if (string.Equals(_pathKeyedPath, _path, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (TryRead(_pathKeyedPath) is not RecentsRead.Read source
            || TryRead(_path) is not { } destination
            || destination is RecentsRead.Unreadable)
        {
            // Absent: nothing to do. Unreadable, either side: nothing is
            // written or deleted, and the next load retries.
            return null;
        }

        AfterDestinationReadForTests?.Invoke();
        IReadOnlyList<string> merged = Merge(destination, source.Entries);
        if (TryCommit(merged))
        {
            DeleteSource(_pathKeyedPath);
        }

        return merged;
    }

    /// <summary>
    /// The old in-vault file into this store's, through the held root: null
    /// when it is absent (settled) or could not be read (retried); the
    /// merged list otherwise, settled once committed and deleted.
    /// </summary>
    private IReadOnlyList<string>? MigrateLegacy()
    {
        if (File.Exists(_pathKeyedPath)
            && !string.Equals(_pathKeyedPath, _path, StringComparison.OrdinalIgnoreCase))
        {
            // The path-keyed source first, so the order stays destination,
            // path-keyed, in-vault.
            return null;
        }

        AnchoredVaultStore? store = null;
        try
        {
            LegacyProbesForTests++;
            RecentsRead legacy;
            try
            {
                store = AnchoredVaultStore.Open(Root, createDirectory: false);
                byte[]? input = store?.ReadAllBytesBounded(LegacyFileName, MaxPayloadBytes);
                legacy = input is null ? new RecentsRead.Absent() : Parse(input);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or JsonException)
            {
                legacy = new RecentsRead.Unreadable(exception);
            }

            if (legacy is RecentsRead.Absent)
            {
                _legacySettled = true;
                return null;
            }

            if (legacy is not RecentsRead.Read source
                || TryRead(_path) is not { } destination
                || destination is RecentsRead.Unreadable)
            {
                return null;
            }

            AfterDestinationReadForTests?.Invoke();
            IReadOnlyList<string> merged = Merge(destination, source.Entries);
            if (TryCommit(merged))
            {
                try
                {
                    store!.DeleteRegularFileIfExists(LegacyFileName);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    _ = exception;
                }

                _legacySettled = true;
            }

            return merged;
        }
        finally
        {
            store?.Dispose();
        }
    }

    // The destination as it was read once, before the merge: a second read
    // could fail where the first did not and turn its entries into none.
    // Its entries are the newer, so they lead and win the cap.
    private static IReadOnlyList<string> Merge(RecentsRead destination, IReadOnlyList<string> older) =>
        Sanitize((destination is RecentsRead.Read current ? current.Entries : []).Concat(older));

    private IReadOnlyList<string> Read(string path) =>
        TryRead(path) is RecentsRead.Read read ? read.Entries : [];

    private static RecentsRead TryRead(string path)
    {
        // Absence is the read's own answer, never File.Exists: that answers
        // false for a file it cannot stat, and a migration would then
        // overwrite a destination it never read (IPG-8).
        try
        {
            return Parse(SafeFile.ReadAllBytesBounded(path, MaxPayloadBytes));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            _ = exception;
            return new RecentsRead.Absent();
        }
        catch (UnauthorizedAccessException) when (Directory.Exists(path))
        {
            // A folder at the file's name holds no list: nothing to read, and
            // a commit over it fails on its own.
            return new RecentsRead.Absent();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new RecentsRead.Unreadable(exception);
        }
    }

    private static RecentsRead Parse(byte[] input) =>
        new RecentsRead.Read(Sanitize(JsonSerializer.Deserialize<string[]>(input) ?? []));

    private void Save(IEnumerable<string> paths) => _ = TryCommit(Sanitize(paths));

    /// <summary>
    /// Writes <paramref name="entries"/> to a flushed temporary file, moves
    /// it over the destination and reads it back: true only when the
    /// read-back is exactly <paramref name="entries"/>. A failure is logged
    /// and leaves no temporary file.
    /// </summary>
    private bool TryCommit(IReadOnlyList<string> entries)
    {
        string? temporary = null;
        try
        {
            string directory = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(directory);
            temporary = Path.Combine(directory, $"recents.tmp-{Guid.NewGuid():N}");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(JsonSerializer.SerializeToUtf8Bytes(entries));
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, _path, overwrite: true);
            if (TryRead(_path) is RecentsRead.Read back && back.Entries.SequenceEqual(entries, StringComparer.Ordinal))
            {
                return true;
            }

            throw new IOException("The recents file did not read back as written.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            HostLog.Write(HostDiagnosticEvent.FileRecentsPersistFailed, exception);
            return false;
        }
        finally
        {
            if (temporary is not null)
            {
                TryDelete(temporary);
            }
        }
    }

    private static string KeyOf(string identityText) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identityText)));

    private static IReadOnlyList<string> Sanitize(IEnumerable<string> paths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = new List<string>(MaxEntries);
        foreach (string path in paths)
        {
            if (path.Length <= MaxEntryLength && seen.Add(path))
            {
                output.Add(path);
                if (output.Count == MaxEntries)
                {
                    break;
                }
            }
        }

        return output;
    }

    private static void TryDelete(string path)
        => SafeFile.TryDelete(path);

    private abstract record RecentsRead
    {
        internal sealed record Absent : RecentsRead;

        internal sealed record Read(IReadOnlyList<string> Entries) : RecentsRead;

        internal sealed record Unreadable(Exception Error) : RecentsRead;
    }
}
