// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlateWindows;

internal sealed record SidebarShortcutState(string Kind, string Path);

internal sealed record SidebarSettingsSnapshot(
    SidebarSortMode SortMode,
    bool GroupByDate,
    IReadOnlySet<string> Pins,
    IReadOnlyList<SidebarShortcutState> Shortcuts,
    string? ReadOnlyReason);

/// <summary>
/// Same-shape projection of macOS' version-1 <c>.slate/sidebar.json</c>.
/// Unknown sibling data and reserved shortcut kinds survive every write.
/// Unsafe authored input is read-only instead of being silently replaced.
/// </summary>
internal sealed class SidebarSettingsStore
{
    internal const int SchemaVersion = 1;
    internal const int MaxReadBytes = 2 * 1024 * 1024;
    internal const int MaxPathLength = 4096;
    internal const int MaxPins = 10_000;
    internal const int MaxPinsPerFolder = 1_000;
    internal const int MaxShortcuts = 200;
    private readonly string _slateDirectory;
    private readonly string _vaultRoot;
    private readonly string _path;
    private readonly string _lockPath;
    private JsonObject _root = new() { ["version"] = SchemaVersion };
    private string? _readOnlyReason;

    public SidebarSettingsStore(string vaultRoot)
    {
        _vaultRoot = Path.GetFullPath(vaultRoot);
        _slateDirectory = Path.Combine(_vaultRoot, ".slate");
        _path = Path.Combine(_slateDirectory, "sidebar.json");
        _lockPath = Path.Combine(_slateDirectory, "sidebar.json.lock");
    }

    public SidebarSettingsSnapshot Load()
    {
        _root = ReadRoot(out _readOnlyReason);
        SidebarSortMode sort = DecodeSort(_root["sort"] as JsonObject);
        bool grouped = string.Equals(
            StringValue(_root["grouping"]),
            "dateBuckets",
            StringComparison.Ordinal);

        var pins = new HashSet<string>(StringComparer.Ordinal);
        if (_root["pins"] is JsonObject pinFolders)
        {
            foreach ((string _, JsonNode? rawPaths) in pinFolders)
            {
                if (rawPaths is not JsonArray paths)
                {
                    continue;
                }

                foreach (JsonNode? rawPath in paths)
                {
                    if (rawPath is JsonValue value
                        && value.TryGetValue<string>(out string? path)
                        && path is { Length: <= MaxPathLength })
                    {
                        pins.Add(path);
                    }
                }
            }
        }

        var shortcuts = new List<SidebarShortcutState>();
        if (_root["shortcuts"] is JsonArray shortcutEntries)
        {
            foreach (JsonNode? rawEntry in shortcutEntries)
            {
                if (rawEntry is JsonObject entry
                    && StringValue(entry["kind"]) is string kind
                    && StringValue(entry["path"]) is string path
                    && (kind == "file" || kind == "folder")
                    && path.Length <= MaxPathLength
                    && !shortcuts.Any(item => item.Kind == kind && item.Path == path))
                {
                    shortcuts.Add(new SidebarShortcutState(kind, path));
                }
            }
        }

        return new SidebarSettingsSnapshot(sort, grouped, pins, shortcuts, _readOnlyReason);
    }

    public void SetOrganization(SidebarSortMode mode, bool groupByDate)
    {
        Update(root =>
        {
            (string field, string direction) = EncodeSort(mode, groupByDate);
            JsonObject sort = root["sort"] as JsonObject ?? [];
            sort["field"] = field;
            sort["direction"] = direction;
            root["sort"] = sort;
            root["grouping"] = groupByDate ? "dateBuckets" : "none";
        });
    }

    public void SetPinsForFolder(string folder, IEnumerable<string> paths)
    {
        if (folder is null || folder.Length > MaxPathLength)
        {
            throw new InvalidOperationException("Sidebar pin folder contains an invalid path.");
        }

        string[] values = ValidatePins(paths, MaxPinsPerFolder);
        Update(root =>
        {
            JsonObject pins = root["pins"] as JsonObject ?? [];
            if (values.Length == 0)
            {
                pins.Remove(folder);
            }
            else
            {
                pins[folder] = new JsonArray(values.Select(value => JsonValue.Create(value)).ToArray());
            }

            int totalPins = pins.Sum(entry => (entry.Value as JsonArray)?.Count ?? 0);
            if (totalPins > MaxPins)
            {
                throw new InvalidOperationException(
                    $"Sidebar pins cannot exceed {MaxPins:N0} entries in total.");
            }

            if (pins.Count == 0)
            {
                root.Remove("pins");
            }
            else
            {
                root["pins"] = pins;
            }
        });
    }

    public void ReplacePins(IEnumerable<string> paths)
    {
        string[] values = ValidatePins(paths, MaxPins);
        IGrouping<string, string>[] folders = values
            .GroupBy(ParentPath, StringComparer.Ordinal)
            .ToArray();
        if (folders.Any(folder => folder.Count() > MaxPinsPerFolder))
        {
            throw new InvalidOperationException(
                $"A sidebar folder cannot persist more than {MaxPinsPerFolder:N0} pins.");
        }

        Update(root =>
        {
            var pins = new JsonObject();
            foreach (IGrouping<string, string> folder in folders)
            {
                pins[folder.Key] = new JsonArray(
                    folder.Select(value => JsonValue.Create(value)).ToArray());
            }

            if (pins.Count == 0)
            {
                root.Remove("pins");
            }
            else
            {
                root["pins"] = pins;
            }
        });
    }

    public void SetShortcuts(IEnumerable<SidebarShortcutState> shortcuts)
    {
        SidebarShortcutState[] requested = shortcuts.ToArray();
        if (requested.Any(item => item is null
            || item.Path.Length > MaxPathLength
            || item.Kind is not ("file" or "folder")))
        {
            throw new InvalidOperationException("Sidebar shortcuts contain an invalid kind or path.");
        }

        SidebarShortcutState[] desired = requested.Distinct().ToArray();
        if (desired.Length > MaxShortcuts)
        {
            throw new InvalidOperationException(
                $"Sidebar shortcuts cannot exceed {MaxShortcuts:N0} entries.");
        }

        Update(root =>
        {
            JsonArray existing = root["shortcuts"] as JsonArray ?? [];
            int reservedCount = existing.Count(rawEntry => !IsVisibleShortcut(rawEntry));
            if (desired.Length > MaxShortcuts - reservedCount)
            {
                throw new InvalidOperationException(
                    "Sidebar shortcuts cannot be updated without dropping reserved future entries.");
            }

            var output = new JsonArray();
            int desiredIndex = 0;
            foreach (JsonNode? rawEntry in existing)
            {
                bool isVisible = IsVisibleShortcut(rawEntry);
                if (!isVisible)
                {
                    output.Add(rawEntry?.DeepClone());
                }
                else if (desiredIndex < desired.Length)
                {
                    SidebarShortcutState item = desired[desiredIndex++];
                    JsonObject replacement = (JsonObject)rawEntry!.DeepClone();
                    replacement["kind"] = item.Kind;
                    replacement["path"] = item.Path;
                    output.Add(replacement);
                }
            }

            while (desiredIndex < desired.Length)
            {
                SidebarShortcutState item = desired[desiredIndex++];
                output.Add(new JsonObject { ["kind"] = item.Kind, ["path"] = item.Path });
            }

            root["shortcuts"] = output;
        });
    }

    private void Update(Action<JsonObject> mutation)
    {
        if (_readOnlyReason is not null)
        {
            throw new InvalidOperationException(_readOnlyReason);
        }

        RejectReparsePoint(_vaultRoot);
        Directory.CreateDirectory(_slateDirectory);
        RejectReparsePoint(_slateDirectory);
        if (File.Exists(_lockPath))
        {
            RejectReparsePoint(_lockPath);
        }

        using var gate = new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        JsonObject current = ReadRoot(out string? blocked);
        if (blocked is not null)
        {
            _readOnlyReason = blocked;
            throw new InvalidOperationException(blocked);
        }

        mutation(current);
        current["version"] = SchemaVersion;
        byte[] output = JsonSerializer.SerializeToUtf8Bytes(
            current,
            new JsonSerializerOptions { WriteIndented = true });
        if (output.Length > MaxReadBytes)
        {
            throw new InvalidOperationException("Sidebar settings would exceed the 2 MiB safety limit.");
        }

        string temporary = Path.Combine(_slateDirectory, $"sidebar.tmp-{Guid.NewGuid():N}");
        try
        {
            using (var stream = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.WriteThrough))
            {
                stream.Write(output);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, _path, overwrite: true);
            _root = current;
        }
        finally
        {
            SafeFile.TryDelete(temporary);
        }
    }

    private JsonObject ReadRoot(out string? blocked)
    {
        blocked = null;
        try
        {
            RejectReparsePoint(_vaultRoot);
            if (Directory.Exists(_slateDirectory))
            {
                RejectReparsePoint(_slateDirectory);
            }

            if (!File.Exists(_path))
            {
                return new JsonObject { ["version"] = SchemaVersion };
            }

            RejectReparsePoint(_path);
            JsonNode? parsed = JsonNode.Parse(
                SafeFile.ReadAllBytesBounded(_path, MaxReadBytes));
            if (parsed is not JsonObject root || !KnownShapesAreValid(root))
            {
                blocked = "Sidebar settings are malformed and are read-only.";
                return new JsonObject { ["version"] = SchemaVersion };
            }

            int version = root["version"]?.GetValue<int>() ?? SchemaVersion;
            if (version > SchemaVersion)
            {
                blocked = $"Sidebar settings use newer version {version} and are read-only.";
                return new JsonObject { ["version"] = SchemaVersion };
            }

            return root;
        }
        catch (FileSizeLimitExceededException)
        {
            blocked = "Sidebar settings exceed the 2 MiB safety limit and are read-only.";
            return new JsonObject { ["version"] = SchemaVersion };
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException
                or InvalidOperationException)
        {
            blocked = "Sidebar settings could not be safely read and are read-only.";
            return new JsonObject { ["version"] = SchemaVersion };
        }
    }

    private static bool KnownShapesAreValid(JsonObject root)
    {
        if (root["sort"] is JsonNode sort && sort is not JsonObject
            || root["grouping"] is JsonNode grouping && grouping is not JsonValue
            || root["pins"] is JsonNode pins && pins is not JsonObject
            || root["shortcuts"] is JsonNode shortcuts && shortcuts is not JsonArray)
        {
            return false;
        }

        if (root["pins"] is JsonObject pinFolders)
        {
            int total = 0;
            foreach ((string folder, JsonNode? rawPaths) in pinFolders)
            {
                if (folder.Length > MaxPathLength || rawPaths is not JsonArray paths || paths.Count > 1_000)
                {
                    return false;
                }

                total += paths.Count;
                if (total > MaxPins || paths.Any(path =>
                    path is not JsonValue value
                    || !value.TryGetValue<string>(out string? pathText)
                    || pathText is null
                    || pathText.Length > MaxPathLength))
                {
                    return false;
                }
            }
        }

        return root["shortcuts"] is not JsonArray entries
            || entries.Count <= MaxShortcuts
                && entries.All(raw => raw is JsonObject entry
                    && entry["kind"] is JsonValue kind
                    && kind.TryGetValue(out string? kindText)
                    && kindText.Length <= 64
                    && entry["path"] is JsonValue path
                    && path.TryGetValue(out string? pathText)
                    && pathText.Length <= MaxPathLength);
    }

    private static SidebarSortMode DecodeSort(JsonObject? sort) =>
        (StringValue(sort?["field"]), StringValue(sort?["direction"])) switch
        {
            ("name", "desc") => SidebarSortMode.NameDescending,
            ("modified", "desc") => SidebarSortMode.ModifiedNewest,
            ("modified", "asc") => SidebarSortMode.ModifiedOldest,
            ("created", "desc") => SidebarSortMode.CreatedNewest,
            ("created", "asc") => SidebarSortMode.CreatedOldest,
            _ => SidebarSortMode.NameAscending,
        };

    private static (string Field, string Direction) EncodeSort(
        SidebarSortMode mode,
        bool grouped)
    {
        if (grouped)
        {
            string field = mode is SidebarSortMode.CreatedNewest or SidebarSortMode.CreatedOldest
                ? "created"
                : "modified";
            string direction = mode is SidebarSortMode.CreatedOldest or SidebarSortMode.ModifiedOldest
                ? "asc"
                : "desc";
            return (field, direction);
        }

        return mode switch
        {
            SidebarSortMode.NameDescending => ("name", "desc"),
            SidebarSortMode.ModifiedNewest => ("modified", "desc"),
            SidebarSortMode.ModifiedOldest => ("modified", "asc"),
            SidebarSortMode.CreatedNewest => ("created", "desc"),
            SidebarSortMode.CreatedOldest => ("created", "asc"),
            _ => ("name", "asc"),
        };
    }

    private static bool IsVisibleShortcut(JsonNode? rawEntry) => rawEntry is JsonObject entry
        && StringValue(entry["kind"]) is "file" or "folder"
        && entry["path"] is JsonValue;

    private static string[] ValidatePins(IEnumerable<string> paths, int maximum)
    {
        string[] requested = paths.ToArray();
        if (requested.Any(path => path is null || path.Length > MaxPathLength))
        {
            throw new InvalidOperationException("Sidebar pins contain an invalid path.");
        }

        string[] values = requested.Distinct(StringComparer.Ordinal).ToArray();
        if (values.Length > maximum)
        {
            throw new InvalidOperationException(
                $"Sidebar pins cannot exceed {maximum:N0} entries in this operation.");
        }

        return values;
    }

    private static string? StringValue(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out string? text) ? text : null;

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Sidebar settings path is a reparse point.");
        }
    }

    private static string ParentPath(string path)
    {
        int separator = path.LastIndexOf('/');
        return separator < 0 ? string.Empty : path[..separator];
    }
}
