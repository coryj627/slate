// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlateWindows;

/// <summary>One entry in the device-local recent-vault list.</summary>
internal sealed record RecentVault(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("lastOpenedMs")] long LastOpenedMs)
{
    public static RecentVault FromPath(string path, DateTimeOffset? now = null)
    {
        string fullPath = System.IO.Path.GetFullPath(path);
        string trimmed = System.IO.Path.TrimEndingDirectorySeparator(fullPath);
        string displayName = System.IO.Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(displayName))
        {
            displayName = fullPath;
        }

        return new RecentVault(
            fullPath,
            displayName,
            (now ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds());
    }
}

/// <summary>
/// Persistent, device-local recent-vault state shared by the welcome screen
/// and (later in W1-1) the Windows Jump List.
/// </summary>
internal sealed class RecentVaultsStore
{
    public const int MaxEntries = 8;
    public const int MaxFileBytes = 1 << 16;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;

    public RecentVaultsStore(string? filePath = null)
    {
        _filePath = filePath ?? DefaultFilePath;
    }

    public static string DefaultFilePath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Slate",
        "recent-vaults.json");

    public IReadOnlyList<RecentVault> Load()
    {
        try
        {
            byte[] buffer = SafeFile.ReadAllBytesBounded(
                _filePath,
                MaxFileBytes,
                FileShare.ReadWrite | FileShare.Delete);

            List<RecentVault>? decoded = JsonSerializer.Deserialize<List<RecentVault>>(
                buffer,
                JsonOptions);
            return decoded?.Take(MaxEntries).ToArray() ?? [];
        }
        catch (FileSizeLimitExceededException exception)
        {
            HostLog.WriteSizeLimit(HostDiagnosticEvent.RecentVaultsPayloadRejected, exception);
            return [];
        }
        catch (FileNotFoundException)
        {
            return [];
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public void Save(IEnumerable<RecentVault> entries)
    {
        string? directory = System.IO.Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        RecentVault[] bounded = entries.Take(MaxEntries).ToArray();
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(bounded, JsonOptions);
        string temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, json);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            SafeFile.TryDelete(temporaryPath);
        }
    }

    public IReadOnlyList<RecentVault> Add(RecentVault entry)
    {
        List<RecentVault> entries = Load().ToList();
        entries.RemoveAll(candidate =>
            StringComparer.OrdinalIgnoreCase.Equals(candidate.Path, entry.Path));
        entries.Insert(0, entry);
        IReadOnlyList<RecentVault> bounded = entries.Take(MaxEntries).ToArray();
        Save(bounded);
        return bounded;
    }

    public IReadOnlyList<RecentVault> Remove(string path)
    {
        List<RecentVault> entries = Load().ToList();
        int removed = entries.RemoveAll(candidate =>
            StringComparer.OrdinalIgnoreCase.Equals(candidate.Path, path));
        if (removed > 0)
        {
            Save(entries);
        }

        return entries;
    }
}
