// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlateWindows;

/// <summary>
/// Device-local application preferences (first entry: W3-1 #728 reading
/// link target). Every property carries its default, so a missing file,
/// a corrupt file, and a fresh install all decode to identical behavior
/// — and fields added later default cleanly against old files.
/// </summary>
internal sealed record AppPreferencesState(
    [property: JsonPropertyName("readingLinksOpenInNewTab")]
    bool ReadingLinksOpenInNewTab = true,
    [property: JsonPropertyName("codePreambleVerbosity")]
    string CodePreambleVerbosity = "preambleOnly");

/// <summary>Bounded device-local storage for app-level preferences.</summary>
internal sealed class AppPreferencesStore
{
    public const int MaxFileBytes = 1 << 14;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;

    public AppPreferencesStore(string? filePath = null)
    {
        _filePath = filePath ?? DefaultFilePath;
    }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Slate",
        "preferences.json");

    /// <summary>Never null and never throws: any unreadable state is the
    /// defaults (see <see cref="AppPreferencesState"/>).</summary>
    public AppPreferencesState Load()
    {
        try
        {
            byte[] buffer = SafeFile.ReadAllBytesBounded(
                _filePath,
                MaxFileBytes,
                FileShare.ReadWrite | FileShare.Delete);

            return JsonSerializer.Deserialize<AppPreferencesState>(buffer, JsonOptions)
                ?? new AppPreferencesState();
        }
        catch (FileNotFoundException)
        {
            return new AppPreferencesState();
        }
        catch (DirectoryNotFoundException)
        {
            return new AppPreferencesState();
        }
        catch (IOException)
        {
            return new AppPreferencesState();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppPreferencesState();
        }
        catch (JsonException)
        {
            return new AppPreferencesState();
        }
    }

    public void Save(AppPreferencesState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
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
}
