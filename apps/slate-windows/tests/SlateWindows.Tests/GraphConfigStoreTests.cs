// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json.Nodes;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>Two configs, field by field: the generated record's Groups is an
/// array, and a record compares an array by reference.</summary>
internal static class GraphConfigs
{
    public static void AssertEqual(GraphConfig expected, GraphConfig actual)
    {
        Assert.Equal(expected.Filters, actual.Filters);
        Assert.Equal(expected.Groups, actual.Groups);
        Assert.Equal(expected.Display, actual.Display);
        Assert.Equal(expected.Forces, actual.Forces);
        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.ConnectionsDepth, actual.ConnectionsDepth);
        Assert.Equal(expected.Verbosity, actual.Verbosity);
    }
}

/// <summary>
/// W6-2 PR C (#746), contract C-10: the host I/O over core's codec — the
/// mac's <c>GraphConfigTests</c>' Windows twin. Term W7's decode arms on
/// the read; the throwing existing-read, core's merge and the atomic move
/// on the write.
/// </summary>
public sealed class GraphConfigStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"slate-graph-store-{Guid.NewGuid():N}");

    public GraphConfigStoreTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string ConfigPath => Path.Combine(_root, ".slate", GraphConfigStore.FileName);

    [Fact]
    public void AMissingFileReadsTheDefaultAndWrites()
    {
        var store = new GraphConfigStore(_root);
        Assert.Equal(ConfigPath, store.FilePath);
        GraphConfigLoad load = store.Read();
        GraphConfigs.AssertEqual(SlateUniffiMethods.GraphConfigDefault(), load.Config);
        Assert.True(load.Writable);
        Assert.Null(load.Failure);
        Assert.False(File.Exists(ConfigPath), "a read never writes");

        GraphConfig terse = load.Config with { Verbosity = GraphVerbosity.Terse };
        store.Write(terse);
        Assert.True(File.Exists(ConfigPath), ".slate created, the file written");
        GraphConfigs.AssertEqual(terse, store.Read().Config);
    }

    [Fact]
    public void EachDecodeFailureReadsTheDefaultReadOnlyAndRefusesEveryLaterSave()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var store = new GraphConfigStore(_root);
        foreach (string text in new[] { "not json", "[1, 2]", "{\"version\": 999999}" })
        {
            File.WriteAllText(ConfigPath, text);
            GraphConfigLoad load = store.Read();
            GraphConfigs.AssertEqual(SlateUniffiMethods.GraphConfigDefault(), load.Config);
            Assert.False(load.Writable, text);
            Assert.NotNull(load.Failure);
            Assert.Equal(text, File.ReadAllText(ConfigPath));
            // The preferences over such a store refuse every save (Term W7).
            PumpedDispatcher.Run(() =>
            {
                var preferences = new GraphPreferencesViewModel(_root, new GraphConfigWriter(), store);
                Assert.False(preferences.IsWritable);
                Assert.Equal(GraphVerbosity.Standard, preferences.Verbosity);
                preferences.SetNameQuery("hub");
                preferences.ScheduleSave();
                Assert.Equal(2, preferences.RefusedForTests);
                Assert.False(preferences.HasPendingForTests);
                preferences.Shutdown();
            });
            Assert.Equal(text, File.ReadAllText(ConfigPath));
        }
    }

    [Fact]
    public void AnUnreadableExistingFileRefusesTheWrite()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var store = new GraphConfigStore(_root);
        store.Write(SlateUniffiMethods.GraphConfigDefault());
        string before = File.ReadAllText(ConfigPath);
        // Held open with no sharing: the existing read throws, and the write
        // refuses rather than clobbering.
        using (var hold = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Throws<IOException>(() => store.Write(SlateUniffiMethods.GraphConfigDefault() with { Verbosity = GraphVerbosity.Terse }));
        }
        Assert.Equal(before, File.ReadAllText(ConfigPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(ConfigPath)!, "*.tmp"));
        // The read failing on its own — the file replaceable but not
        // readable (the mac's finding 2): refused, never treated as missing.
        store.ReadExistingForTests = _ => throw new IOException("unreadable");
        Assert.Throws<IOException>(() => store.Write(SlateUniffiMethods.GraphConfigDefault() with { Verbosity = GraphVerbosity.Verbose }));
        Assert.Equal(before, File.ReadAllText(ConfigPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(ConfigPath)!, "*.tmp"));
        store.ReadExistingForTests = null;
        store.Write(SlateUniffiMethods.GraphConfigDefault() with { Verbosity = GraphVerbosity.Verbose });
        Assert.Equal(GraphVerbosity.Verbose, store.Read().Config.Verbosity);
    }

    [Fact]
    public void TheWrittenBytesAreCoresCanonicalTextWithAnUnknownKeyPreserved()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var store = new GraphConfigStore(_root);
        // A file a newer Slate — or a human — put a key into.
        JsonObject root = JsonNode.Parse(SlateUniffiMethods.GraphConfigEncode(SlateUniffiMethods.GraphConfigDefault(), null))!.AsObject();
        root["future"] = 42;
        string existing = root.ToJsonString();
        File.WriteAllText(ConfigPath, existing);

        GraphConfig changed = SlateUniffiMethods.GraphConfigDefault() with { Verbosity = GraphVerbosity.Verbose };
        store.Write(changed);

        string written = File.ReadAllText(ConfigPath);
        Assert.Equal(SlateUniffiMethods.GraphConfigEncode(changed, existing), written);
        Assert.Contains("\"future\"", written, StringComparison.Ordinal);
        GraphConfigs.AssertEqual(changed, store.Read().Config);
    }

    [Fact]
    public void TheTargetIsNeverTorn()
    {
        var store = new GraphConfigStore(_root);
        for (int i = 0; i < 5; i++)
        {
            // The needle: a field core does not clamp (the depth clamps to 1..3).
            store.Write(SlateUniffiMethods.GraphConfigDefault() with { Filters = SlateUniffiMethods.GraphConfigDefault().Filters with { NameQuery = $"n{i}" } });
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(ConfigPath)!, "*.tmp"));
            Assert.Equal($"n{i}", store.Read().Config.Filters.NameQuery);
        }
    }
}
