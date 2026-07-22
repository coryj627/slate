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
    private readonly string _path;
    private readonly string _legacyPath;

    public FileRecentsStore(
        string vaultRoot,
        VaultRootIdentity? identity = null,
        string? localAppDataRoot = null)
    {
        string identityText = identity is null
            ? Path.GetFullPath(vaultRoot).ToUpperInvariant()
            : $"{identity.Device}-{identity.Inode}";
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identityText)));
        string root = localAppDataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Slate",
            "file-recents");
        _path = Path.Combine(root, $"{key}.json");
        _legacyPath = Path.Combine(vaultRoot, ".slate", "file-recents.json");
    }

    public IReadOnlyList<string> Load()
    {
        if (!File.Exists(_path))
        {
            IReadOnlyList<string> legacy = CanReadLegacy() ? Read(_legacyPath) : [];
            Save(legacy);
            if (File.Exists(_path))
            {
                TryDelete(_legacyPath);
            }
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

    private bool CanReadLegacy()
    {
        try
        {
            string directory = Path.GetDirectoryName(_legacyPath)!;
            return Directory.Exists(directory)
                && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0
                && (!File.Exists(_legacyPath)
                    || (File.GetAttributes(_legacyPath) & FileAttributes.ReparsePoint) == 0);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
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
