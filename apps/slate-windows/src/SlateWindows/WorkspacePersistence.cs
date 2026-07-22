// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using System.IO;

namespace SlateWindows;

internal enum WorkspaceItemKind
{
    Markdown,
    Canvas,
    Base,
    SavedQuery,
    Dashboard,
    Graph,
}

internal sealed record WorkspaceItemState(
    WorkspaceItemKind Kind,
    string Path,
    string? Id = null,
    string? Name = null)
{
    public string Discriminator => Kind switch
    {
        WorkspaceItemKind.Markdown => "markdown",
        WorkspaceItemKind.Canvas => "canvas",
        WorkspaceItemKind.Base => "base",
        WorkspaceItemKind.SavedQuery => "savedQuery",
        WorkspaceItemKind.Dashboard => "dashboard",
        WorkspaceItemKind.Graph => "graph",
        _ => throw new InvalidOperationException("Unknown workspace item kind."),
    };

    public string Title => Kind switch
    {
        WorkspaceItemKind.SavedQuery or WorkspaceItemKind.Dashboard =>
            string.IsNullOrWhiteSpace(Name) ? "Untitled" : Name,
        WorkspaceItemKind.Graph => "Graph",
        _ => System.IO.Path.GetFileNameWithoutExtension(Path) is { Length: > 0 } title
            ? title
            : System.IO.Path.GetFileName(Path),
    };
}

internal sealed record WorkspaceTabState(
    Guid Id,
    WorkspaceItemState Item,
    string? Mode = null,
    bool? PropsCollapsed = null,
    string? ActiveCanvasSurface = null);

internal abstract record WorkspaceNodeState;

internal sealed record WorkspaceGroupState(
    Guid Id,
    Guid? ActiveTab,
    IReadOnlyList<WorkspaceTabState> Tabs) : WorkspaceNodeState;

internal sealed record WorkspaceSplitState(
    string Axis,
    IReadOnlyList<double> Weights,
    IReadOnlyList<WorkspaceNodeState> Children) : WorkspaceNodeState;

internal sealed record WorkspaceSnapshot(
    int Version,
    Guid ActiveGroup,
    WorkspaceNodeState Root,
    string? ActiveLeaf,
    IReadOnlyList<string>? ExpandedDirPaths);

/// <summary>
/// Same-shape implementation of the mac WorkspaceStore schema-v1 contract.
/// Reads are bounded and hostile paths are rejected; unknown tab kinds are
/// dropped independently so a future tab never loses the rest of a layout.
/// </summary>
internal sealed class WorkspacePersistence
{
    internal const int SchemaVersion = 1;
    internal const int MaxFileBytes = 256 * 1024;
    internal const int MaxExpandedDirectories = 500;
    internal const int MaxGroups = 6;
    internal const double MinGroupWeight = 0.15;
    private const int MaxNodeDepth = 32;
    private const int MaxTabs = 4096;
    private const string WorkspaceFileName = "workspace.json";

    private readonly string _vaultRoot;
    private readonly Action? _afterDirectoryAnchored;

    public WorkspacePersistence(string vaultRoot, Action? afterDirectoryAnchored = null)
    {
        _vaultRoot = Path.GetFullPath(vaultRoot);
        _afterDirectoryAnchored = afterDirectoryAnchored;
    }

    public WorkspaceSnapshot? Load()
    {
        try
        {
            using AnchoredVaultStore? store = AnchoredVaultStore.Open(
                _vaultRoot,
                createDirectory: false,
                _afterDirectoryAnchored);
            byte[]? input = store?.ReadAllBytesBounded(WorkspaceFileName, MaxFileBytes);
            if (input is null)
            {
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(input, new JsonDocumentOptions
            {
                MaxDepth = MaxNodeDepth * 2,
            });
            JsonElement root = document.RootElement;
            if (!TryInt(root, "version", out int version) || version != SchemaVersion
                || !TryGuid(root, "activeGroup", out Guid activeGroup)
                || !root.TryGetProperty("root", out JsonElement rootNode))
            {
                return null;
            }

            int tabCount = 0;
            int groupCount = 0;
            var groupIds = new HashSet<Guid>();
            var tabIds = new HashSet<Guid>();
            WorkspaceNodeState? node = ReadNode(
                rootNode,
                0,
                ref tabCount,
                ref groupCount,
                groupIds,
                tabIds);
            if (node is null || !IsStructurallyValid(node, isRoot: true))
            {
                return null;
            }

            Guid fallbackGroup = EnumerateGroups(node).Select(group => group.Id).FirstOrDefault();
            if (fallbackGroup == Guid.Empty)
            {
                return null;
            }

            if (!EnumerateGroups(node).Any(group => group.Id == activeGroup))
            {
                activeGroup = fallbackGroup;
            }

            string? activeLeaf = root.TryGetProperty("activeLeaf", out JsonElement leaf)
                && leaf.ValueKind == JsonValueKind.String
                ? leaf.GetString()
                : null;
            IReadOnlyList<string>? expanded = ReadExpandedPaths(root);
            return new WorkspaceSnapshot(version, activeGroup, node, activeLeaf, expanded);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidOperationException)
        {
            return null;
        }
    }

    public void Save(WorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using AnchoredVaultStore store = AnchoredVaultStore.Open(
            _vaultRoot,
            createDirectory: true,
            _afterDirectoryAnchored)
            ?? throw new IOException("Could not anchor the workspace store directory.");
        using var output = new BoundedMemoryStream(MaxFileBytes);
        using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", SchemaVersion);
            writer.WriteString("activeGroup", snapshot.ActiveGroup);
            writer.WritePropertyName("root");
            WriteNode(writer, snapshot.Root);
            if (!string.IsNullOrWhiteSpace(snapshot.ActiveLeaf))
            {
                writer.WriteString("activeLeaf", snapshot.ActiveLeaf);
            }

            IReadOnlyList<string> expanded = NormalizeExpandedPaths(snapshot.ExpandedDirPaths ?? []);
            if (expanded.Count > 0)
            {
                writer.WritePropertyName("expandedDirPaths");
                writer.WriteStartArray();
                foreach (string path in expanded)
                {
                    writer.WriteStringValue(path);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        if (output.Length > MaxFileBytes)
        {
            throw new InvalidOperationException("Workspace state exceeds the 256 KiB safety limit.");
        }

        store.WriteAtomically(
            WorkspaceFileName,
            output.GetBuffer().AsSpan(0, checked((int)output.Length)));
    }

    internal static IReadOnlyList<string> NormalizeExpandedPaths(IEnumerable<string> paths)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var newestFirst = new List<string>();
        foreach (string path in paths.Reverse())
        {
            if (IsSafeExpandedPath(path) && seen.Add(path))
            {
                newestFirst.Add(path);
            }
        }

        newestFirst.Reverse();
        return newestFirst.Count <= MaxExpandedDirectories
            ? newestFirst
            : newestFirst.GetRange(
                newestFirst.Count - MaxExpandedDirectories,
                MaxExpandedDirectories);
    }

    private static WorkspaceNodeState? ReadNode(
        JsonElement element,
        int depth,
        ref int tabCount,
        ref int groupCount,
        ISet<Guid> groupIds,
        ISet<Guid> tabIds)
    {
        if (depth > MaxNodeDepth || !TryString(element, "kind", out string? kind))
        {
            return null;
        }

        if (kind == "group")
        {
            if (++groupCount > MaxGroups)
            {
                return null;
            }

            if (!TryGuid(element, "id", out Guid id)
                || !groupIds.Add(id)
                || !element.TryGetProperty("tabs", out JsonElement tabsElement)
                || tabsElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var tabs = new List<WorkspaceTabState>();
            foreach (JsonElement tabElement in tabsElement.EnumerateArray())
            {
                if (++tabCount > MaxTabs)
                {
                    return null;
                }

                WorkspaceTabState? tab = ReadTab(tabElement);
                if (tab is not null)
                {
                    if (!tabIds.Add(tab.Id))
                    {
                        return null;
                    }

                    tabs.Add(tab);
                }
            }

            Guid? active = TryGuid(element, "activeTab", out Guid activeTab)
                && tabs.Any(tab => tab.Id == activeTab)
                ? activeTab
                : tabs.FirstOrDefault()?.Id;
            return new WorkspaceGroupState(id, active, tabs);
        }

        if (kind == "split")
        {
            if (!TryString(element, "axis", out string? axis)
                || axis is not ("horizontal" or "vertical")
                || !element.TryGetProperty("children", out JsonElement childrenElement)
                || childrenElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var children = new List<WorkspaceNodeState>();
            foreach (JsonElement childElement in childrenElement.EnumerateArray())
            {
                WorkspaceNodeState? child = ReadNode(
                    childElement,
                    depth + 1,
                    ref tabCount,
                    ref groupCount,
                    groupIds,
                    tabIds);
                if (child is null)
                {
                    return null;
                }

                if (child is WorkspaceSplitState childSplit && childSplit.Axis == axis)
                {
                    return null;
                }

                children.Add(child);
            }

            if (children.Count < 2)
            {
                return null;
            }

            IReadOnlyList<double>? weights = ReadWeights(element, children.Count);
            if (weights is null)
            {
                return null;
            }

            return new WorkspaceSplitState(axis, weights, children);
        }

        return null;
    }

    private static WorkspaceTabState? ReadTab(JsonElement element)
    {
        if (!TryGuid(element, "id", out Guid id)
            || !element.TryGetProperty("item", out JsonElement itemElement)
            || !TryString(itemElement, "kind", out string? discriminator)
            || !TryString(itemElement, "path", out string? path)
            || string.IsNullOrWhiteSpace(path)
            || path.Length > 4096)
        {
            return null;
        }

        WorkspaceItemKind? kind = discriminator switch
        {
            "markdown" => WorkspaceItemKind.Markdown,
            "canvas" => WorkspaceItemKind.Canvas,
            "base" => WorkspaceItemKind.Base,
            "savedQuery" => WorkspaceItemKind.SavedQuery,
            "dashboard" => WorkspaceItemKind.Dashboard,
            "graph" => WorkspaceItemKind.Graph,
            _ => null,
        };
        if (kind is null)
        {
            return null;
        }

        string? itemId = OptionalString(itemElement, "id");
        string? name = OptionalString(itemElement, "name");
        if (kind is WorkspaceItemKind.SavedQuery or WorkspaceItemKind.Dashboard)
        {
            itemId ??= path;
            name ??= kind == WorkspaceItemKind.SavedQuery ? "Saved query" : "Dashboard";
        }

        if (kind == WorkspaceItemKind.Graph)
        {
            path = "graph:singleton";
        }

        string? mode = OptionalString(element, "mode") == "reading" ? "reading" : null;
        bool? collapsed = element.TryGetProperty("propsCollapsed", out JsonElement props)
            && props.ValueKind == JsonValueKind.True
            ? true
            : null;
        string? canvasSurface = OptionalString(element, "activeCanvasSurface");
        if (canvasSurface is not ("table" or "visual"))
        {
            canvasSurface = null;
        }
        return new WorkspaceTabState(
            id,
            new WorkspaceItemState(kind.Value, path, itemId, name),
            mode,
            collapsed,
            canvasSurface);
    }

    private static IReadOnlyList<double>? ReadWeights(JsonElement element, int count)
    {
        var weights = new List<double>();
        if (element.TryGetProperty("weights", out JsonElement array)
            && array.ValueKind == JsonValueKind.Array)
        {
            weights.AddRange(array.EnumerateArray().Select(value =>
                value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double weight)
                    && double.IsFinite(weight) && weight > 0
                    ? weight
                    : 0));
        }

        if (weights.Count != count || weights.Any(weight => weight <= 0))
        {
            return null;
        }

        double total = weights.Sum();
        double[] normalized = weights.Select(weight => weight / total).ToArray();
        return normalized.Any(weight => weight < MinGroupWeight - 1e-9)
            ? null
            : normalized;
    }

    private static bool IsStructurallyValid(WorkspaceNodeState node, bool isRoot)
    {
        if (node is WorkspaceGroupState group)
        {
            return isRoot || group.Tabs.Count > 0;
        }

        var split = (WorkspaceSplitState)node;
        return split.Children.Count >= 2
            && split.Children.All(child => IsStructurallyValid(child, isRoot: false));
    }

    private static IReadOnlyList<string>? ReadExpandedPaths(JsonElement root)
    {
        if (!root.TryGetProperty("expandedDirPaths", out JsonElement element)
            || element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return NormalizeExpandedPaths(element.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString() ?? string.Empty));
    }

    private static bool IsSafeExpandedPath(string path) =>
        !string.IsNullOrEmpty(path)
        && path.Length <= 1024
        && !Path.IsPathRooted(path)
        && !path.StartsWith("/", StringComparison.Ordinal)
        && !path.StartsWith('\\')
        && !path.Split('/', '\\').Contains("..", StringComparer.Ordinal);

    private static IEnumerable<WorkspaceGroupState> EnumerateGroups(WorkspaceNodeState node)
    {
        if (node is WorkspaceGroupState group)
        {
            yield return group;
            yield break;
        }

        foreach (WorkspaceNodeState child in ((WorkspaceSplitState)node).Children)
        {
            foreach (WorkspaceGroupState descendant in EnumerateGroups(child))
            {
                yield return descendant;
            }
        }
    }

    private static void WriteNode(Utf8JsonWriter writer, WorkspaceNodeState node)
    {
        writer.WriteStartObject();
        if (node is WorkspaceGroupState group)
        {
            writer.WriteString("kind", "group");
            writer.WriteString("id", group.Id);
            if (group.ActiveTab is Guid active)
            {
                writer.WriteString("activeTab", active);
            }

            writer.WritePropertyName("tabs");
            writer.WriteStartArray();
            foreach (WorkspaceTabState tab in group.Tabs)
            {
                WriteTab(writer, tab);
            }

            writer.WriteEndArray();
        }
        else
        {
            var split = (WorkspaceSplitState)node;
            writer.WriteString("kind", "split");
            writer.WriteString("axis", split.Axis);
            writer.WritePropertyName("weights");
            writer.WriteStartArray();
            foreach (double weight in split.Weights)
            {
                writer.WriteNumberValue(weight);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("children");
            writer.WriteStartArray();
            foreach (WorkspaceNodeState child in split.Children)
            {
                WriteNode(writer, child);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WriteTab(Utf8JsonWriter writer, WorkspaceTabState tab)
    {
        writer.WriteStartObject();
        writer.WriteString("id", tab.Id);
        writer.WritePropertyName("item");
        writer.WriteStartObject();
        writer.WriteString("kind", tab.Item.Discriminator);
        writer.WriteString("path", tab.Item.Path);
        if (tab.Item.Id is not null)
        {
            writer.WriteString("id", tab.Item.Id);
        }

        if (tab.Item.Name is not null)
        {
            writer.WriteString("name", tab.Item.Name);
        }

        writer.WriteEndObject();
        if (tab.Mode == "reading")
        {
            writer.WriteString("mode", "reading");
        }

        if (tab.PropsCollapsed == true)
        {
            writer.WriteBoolean("propsCollapsed", true);
        }

        if (tab.ActiveCanvasSurface is "table" or "visual")
        {
            writer.WriteString("activeCanvasSurface", tab.ActiveCanvasSurface);
        }

        writer.WriteEndObject();
    }

    private static bool TryString(JsonElement element, string property, out string? value)
    {
        value = null;
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out JsonElement child)
            && child.ValueKind == JsonValueKind.String
            && (value = child.GetString()) is not null;
    }

    private static string? OptionalString(JsonElement element, string property) =>
        TryString(element, property, out string? value) ? value : null;

    private static bool TryGuid(JsonElement element, string property, out Guid value)
    {
        value = Guid.Empty;
        return TryString(element, property, out string? text) && Guid.TryParse(text, out value);
    }

    private static bool TryInt(JsonElement element, string property, out int value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out JsonElement child)
            && child.ValueKind == JsonValueKind.Number
            && child.TryGetInt32(out value);
    }

    private sealed class BoundedMemoryStream : MemoryStream
    {
        private readonly long _maximumBytes;

        public BoundedMemoryStream(int maximumBytes)
            : base(Math.Min(maximumBytes, 16 * 1024))
        {
            _maximumBytes = maximumBytes;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureCapacityFor(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCapacityFor(buffer.Length);
            base.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            EnsureCapacityFor(1);
            base.WriteByte(value);
        }

        private void EnsureCapacityFor(int count)
        {
            if (count < 0 || Position > _maximumBytes - count)
            {
                throw new InvalidOperationException(
                    "Workspace state exceeds the 256 KiB safety limit.");
            }
        }
    }

}
