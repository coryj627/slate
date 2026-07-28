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
    string CodePreambleVerbosity = "preambleOnly",
    [property: JsonPropertyName("mathSpeechStyle")]
    string MathSpeechStyle = "clearSpeak",
    [property: JsonPropertyName("mathVerbosity")]
    string MathVerbosity = "medium",
    [property: JsonPropertyName("mathBrailleCode")]
    string MathBrailleCode = "nemeth");

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

            AppPreferencesState state =
                JsonSerializer.Deserialize<AppPreferencesState>(buffer, JsonOptions)
                    ?? new AppPreferencesState();
            // A reference-typed field deserializes JSON null WITHOUT
            // throwing, sidestepping the defaults-on-unreadable rule —
            // normalize at the boundary so no caller ever sees null.
            if (state.CodePreambleVerbosity is null)
            {
                state = state with { CodePreambleVerbosity = "preambleOnly" };
            }
            if (state.MathSpeechStyle is null
                || state.MathSpeechStyle == "mathSpeak")
            {
                // MathSpeak normalizes away while #1056 is open: the
                // upstream engine never implemented it, and restoring
                // it as the checked style would falsely confirm a
                // speech convention that silently stays ClearSpeak.
                state = state with { MathSpeechStyle = "clearSpeak" };
            }
            if (state.MathVerbosity is null)
            {
                state = state with { MathVerbosity = "medium" };
            }
            if (state.MathBrailleCode is null)
            {
                state = state with { MathBrailleCode = "nemeth" };
            }
            return state;
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
