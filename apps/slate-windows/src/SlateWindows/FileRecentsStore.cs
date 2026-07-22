// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>Device-local, per-vault LRU shared by the sidebar and Quick Open.</summary>
internal sealed class FileRecentsStore
{
    internal const int MaxEntries = 50;
    internal const int MaxEntryLength = 4096;
    internal const int MaxPayloadBytes = 256 * 1024;
    private const string LegacyFileName = "file-recents.json";
    private readonly string _path;
    private readonly string _vaultRoot;

    public FileRecentsStore(
        string vaultRoot,
        VaultRootIdentity? identity = null,
        string? localAppDataRoot = null)
    {
        _vaultRoot = Path.GetFullPath(vaultRoot);
        string identityText = identity is null
            ? _vaultRoot.ToUpperInvariant()
            : $"{identity.Device}-{identity.Inode}";
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identityText)));
        string root = localAppDataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Slate",
            "file-recents");
        _path = Path.Combine(root, $"{key}.json");
    }

    public IReadOnlyList<string> Load()
    {
        if (!File.Exists(_path))
        {
            MigrateLegacy();
        }

        return Read(_path);
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

    public void Clear() => Save([]);

    public IReadOnlyList<string> Replace(IEnumerable<string> paths)
    {
        IReadOnlyList<string> sanitized = Sanitize(paths);
        Save(sanitized);
        return sanitized;
    }

    private void MigrateLegacy()
    {
        IReadOnlyList<string> legacy = [];
        AnchoredVaultStore? store = null;
        try
        {
            store = AnchoredVaultStore.Open(_vaultRoot, createDirectory: false);
            byte[]? input = store?.ReadAllBytesBounded(LegacyFileName, MaxPayloadBytes);
            legacy = input is null
                ? []
                : Sanitize(JsonSerializer.Deserialize<string[]>(input) ?? []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
        }

        try
        {
            Save(legacy);
            if (File.Exists(_path))
            {
                try
                {
                    store?.DeleteRegularFileIfExists(LegacyFileName);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        finally
        {
            store?.Dispose();
        }
    }

    private IReadOnlyList<string> Read(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            string[]? paths = JsonSerializer.Deserialize<string[]>(
                SafeFile.ReadAllBytesBounded(path, MaxPayloadBytes));
            return Sanitize(paths ?? []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    private void Save(IEnumerable<string> paths)
    {
        try
        {
            string directory = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, $"recents.tmp-{Guid.NewGuid():N}");
            try
            {
                File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(Sanitize(paths)));
                File.Move(temporary, _path, overwrite: true);
            }
            finally
            {
                TryDelete(temporary);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            HostLog.Write(HostDiagnosticEvent.FileRecentsPersistFailed, exception);
        }
    }

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
}
