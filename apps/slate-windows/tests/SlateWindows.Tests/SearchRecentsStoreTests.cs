// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using System.Text.Json;
using SlateWindows.Search;

namespace SlateWindows.Tests;

/// <summary>
/// W5-2 (#742) contract S14 facts: the per-vault search-recents store
/// must interoperate with mac's <c>SearchRecentsStore</c> — same file,
/// same schema, same caps, same degrade-to-empty discipline.
/// </summary>
public sealed class SearchRecentsStoreTests
{
    private const string RelativePath = ".slate/search-recents.json";

    [Fact]
    public void AddIsLruMoveToFrontNotAppend()
    {
        using FixtureVault vault = FixtureVault.Create(0, "search-recents-lru");
        var store = new SearchRecentsStore(vault.Root);

        store.Add("alpha");
        store.Add("beta");
        store.Add("gamma");
        IReadOnlyList<string> updated = store.Add("beta");

        Assert.Equal(["beta", "gamma", "alpha"], updated);
        Assert.Equal(["beta", "gamma", "alpha"], new SearchRecentsStore(vault.Root).Load());
        Assert.Null(store.LastSaveError);
    }

    [Fact]
    public void AddCapsAtTwentyEntriesDroppingTheOldest()
    {
        using FixtureVault vault = FixtureVault.Create(0, "search-recents-cap");
        var store = new SearchRecentsStore(vault.Root);
        for (int index = 1; index <= SearchRecentsStore.MaxEntries; index++)
        {
            store.Add($"query {index}");
        }

        IReadOnlyList<string> updated = store.Add("newest");

        Assert.Equal(SearchRecentsStore.MaxEntries, updated.Count);
        Assert.Equal("newest", updated[0]);
        // "query 1" was the least recently used entry and falls off.
        Assert.DoesNotContain("query 1", updated);
        Assert.Equal("query 2", updated[^1]);
        Assert.Equal(updated, store.Load());
    }

    [Fact]
    public void LoadDedupesFirstOccurrenceWinsAndShortCircuitsAtTheCap()
    {
        using FixtureVault vault = FixtureVault.Create(0, "search-recents-load-cap");
        // 30 raw entries with a duplicate up front: mac would have never
        // written this file, but a partial write or another tool might.
        var entries = new List<string> { "dup", "unique 0", "dup" };
        for (int index = 1; index < 30; index++)
        {
            entries.Add($"unique {index}");
        }

        WriteRecentsFile(vault.Root, JsonSerializer.Serialize(entries));

        IReadOnlyList<string> loaded = new SearchRecentsStore(vault.Root).Load();

        Assert.Equal(SearchRecentsStore.MaxEntries, loaded.Count);
        Assert.Equal("dup", loaded[0]);
        Assert.Equal("unique 0", loaded[1]);
        // First occurrence won; the later duplicate did not move it.
        Assert.Single(loaded, entry => entry == "dup");
        // Short-circuited at the cap: entries past 20 never load.
        Assert.DoesNotContain("unique 29", loaded);
    }

    [Fact]
    public void OversizedFileLoadsEmpty()
    {
        using FixtureVault vault = FixtureVault.Create(0, "search-recents-oversized");
        // A VALID JSON array pushed past 64 KiB: size alone must reject
        // it before any decode happens.
        string huge = JsonSerializer.Serialize(
            new[] { new string('q', SearchRecentsStore.MaxFileBytes + 16) });
        Assert.True(Encoding.UTF8.GetByteCount(huge) > SearchRecentsStore.MaxFileBytes);
        WriteRecentsFile(vault.Root, huge);

        Assert.Empty(new SearchRecentsStore(vault.Root).Load());
    }

    [Theory]
    [InlineData("{ not json at all")]
    [InlineData("{\"queries\": []}")]
    [InlineData("[1, 2, 3]")]
    [InlineData("[\"ok\", null]")]
    [InlineData("\"just a string\"")]
    public void MalformedContentLoadsEmpty(string content)
    {
        using FixtureVault vault = FixtureVault.Create(0, "search-recents-malformed");
        WriteRecentsFile(vault.Root, content);

        // Whole-file rejection, matching mac's decode of [String].self:
        // an array with a non-string entry is malformed in whole, not
        // filtered entry-by-entry.
        Assert.Empty(new SearchRecentsStore(vault.Root).Load());
    }

    [Fact]
    public void MissingFileAndMissingVaultRootBothLoadEmptyWithoutThrowing()
    {
        using FixtureVault vault = FixtureVault.Create(0, "search-recents-missing");
        Assert.Empty(new SearchRecentsStore(vault.Root).Load());

        string nowhere = Path.Combine(
            Path.GetTempPath(),
            $"slate-search-recents-nowhere-{Guid.NewGuid():N}");
        Assert.Empty(new SearchRecentsStore(nowhere).Load());
    }

    [Fact]
    public void ClearPersistsAnEmptyFileRatherThanDeletingIt()
    {
        using FixtureVault vault = FixtureVault.Create(0, "search-recents-clear");
        var store = new SearchRecentsStore(vault.Root);
        store.Add("remembered");
        string filePath = Path.Combine(vault.Root, RelativePath);
        Assert.True(File.Exists(filePath));

        store.Clear();

        // The file survives so the subsequent load path is identical
        // (contract S14), and its content is an empty JSON array.
        Assert.True(File.Exists(filePath));
        Assert.Empty(store.Load());
        string[]? persisted = JsonSerializer.Deserialize<string[]>(File.ReadAllBytes(filePath));
        Assert.NotNull(persisted);
        Assert.Empty(persisted);
    }

    [Fact]
    public void LoadsAFileWrittenInMacsSchema()
    {
        using FixtureVault vault = FixtureVault.Create(0, "search-recents-mac-interop");
        // Byte-for-byte the shape mac's JSONEncoder (.prettyPrinted)
        // produces for ["budget 2026", "meeting notes"] — the vault may
        // have travelled from a Mac.
        WriteRecentsFile(
            vault.Root,
            "[\n  \"budget 2026\",\n  \"meeting notes\"\n]");

        Assert.Equal(
            ["budget 2026", "meeting notes"],
            new SearchRecentsStore(vault.Root).Load());
    }

    [Fact]
    public void WritesAFileMacsDecoderWouldAccept()
    {
        using FixtureVault vault = FixtureVault.Create(0, "search-recents-mac-write");
        var store = new SearchRecentsStore(vault.Root);
        store.Add("first");
        store.Add("second");

        byte[] raw = File.ReadAllBytes(Path.Combine(vault.Root, RelativePath));
        // No BOM — mac's JSONDecoder reads the bytes as-is — and a plain
        // JSON array of raw query strings, most-recent-first.
        Assert.False(raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF);
        string[]? decoded = JsonSerializer.Deserialize<string[]>(raw);
        Assert.NotNull(decoded);
        Assert.Equal(["second", "first"], decoded);
    }

    [Fact]
    public void LoadStripsAUtf8BomBeforeDecoding()
    {
        using FixtureVault vault = FixtureVault.Create(0, "search-recents-bom");
        string directory = Path.Combine(vault.Root, ".slate");
        Directory.CreateDirectory(directory);
        // A Notepad round trip prepends EF BB BF. mac's JSONDecoder
        // accepts the BOM, so the cross-platform file contract does too
        // (contract S14) — a Windows edit of the file must not silently
        // forget every recent.
        File.WriteAllText(
            Path.Combine(directory, "search-recents.json"),
            "[\n  \"budget 2026\",\n  \"meeting notes\"\n]",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Assert.Equal(
            ["budget 2026", "meeting notes"],
            new SearchRecentsStore(vault.Root).Load());
    }

    // ---- VaultSearchSource: the cached store (red-team round 1) ---------

    [Fact]
    public void VaultSearchSourceCachesOneStoreSoASaveErrorStaysObservable()
    {
        string root = Path.Combine(
            Path.GetTempPath(), $"slate-search-source-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // A FILE named .slate blocks the store directory, so every
            // save fails — the shape of a real broken vault.
            File.WriteAllText(Path.Combine(root, ".slate"), "not a directory");
            var source = new VaultSearchSource(() => null, () => root);

            source.RecordRecent("doomed");

            // Per-call construction discarded the failing store — and
            // LastSaveError with it — before anything could read it;
            // the cached store keeps the failure observable.
            Assert.NotNull(source.LastRecentsSaveError);
            // Load still degrades to empty rather than throwing, and
            // does not launder the recorded failure.
            Assert.Empty(source.LoadRecents());
            Assert.NotNull(source.LastRecentsSaveError);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteRecentsFile(string vaultRoot, string content)
    {
        string directory = Path.Combine(vaultRoot, ".slate");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "search-recents.json"),
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
