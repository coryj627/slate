// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using SlateWindows;

namespace SlateWindows.Tests;

public sealed class RecentVaultsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"slate-recents-test-{Guid.NewGuid():N}");

    public RecentVaultsStoreTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void MissingFileLoadsAsEmpty()
    {
        Assert.Empty(CreateStore().Load());
    }

    [Fact]
    public void SaveAndLoadRoundTripsMacCompatibleShape()
    {
        var expected = new[]
        {
            new RecentVault(@"C:\Vaults\Alpha", "Alpha", 1_700_000_000_000),
            new RecentVault(@"D:\Notes\Beta", "Beta", 1_700_000_500_000),
        };

        RecentVaultsStore store = CreateStore();
        store.Save(expected);

        Assert.Equal(expected, store.Load());
        string json = File.ReadAllText(StorePath);
        Assert.Contains("\"displayName\"", json, StringComparison.Ordinal);
        Assert.Contains("\"lastOpenedMs\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MalformedAndOversizedFilesLoadAsEmpty()
    {
        File.WriteAllText(StorePath, "not json");
        Assert.Empty(CreateStore().Load());

        File.WriteAllBytes(StorePath, new byte[RecentVaultsStore.MaxFileBytes + 1]);
        Assert.Empty(CreateStore().Load());
    }

    [Fact]
    public void AddIsCaseInsensitiveLruAndCapsTheList()
    {
        RecentVaultsStore store = CreateStore();
        for (int index = 0; index < RecentVaultsStore.MaxEntries + 3; index++)
        {
            store.Add(new RecentVault($@"C:\Vault-{index}", $"Vault-{index}", index));
        }

        IReadOnlyList<RecentVault> refreshed = store.Add(
            new RecentVault(@"c:\VAULT-5", "Vault-5 refreshed", 99));

        Assert.Equal(RecentVaultsStore.MaxEntries, refreshed.Count);
        Assert.Equal(@"c:\VAULT-5", refreshed[0].Path);
        Assert.Equal("Vault-5 refreshed", refreshed[0].DisplayName);
        Assert.Single(
            refreshed,
            item => string.Equals(item.Path, @"C:\Vault-5", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RemoveMatchesWindowsPathsCaseInsensitively()
    {
        RecentVaultsStore store = CreateStore();
        store.Save(
        [
            new RecentVault(@"C:\Vaults\Alpha", "Alpha", 1),
            new RecentVault(@"C:\Vaults\Beta", "Beta", 2),
        ]);

        IReadOnlyList<RecentVault> result = store.Remove(@"c:\vaults\ALPHA");

        RecentVault remaining = Assert.Single(result);
        Assert.Equal("Beta", remaining.DisplayName);
        Assert.Equal(result, store.Load());
    }

    [Fact]
    public void ExactlyMaximumFileSizeIsStillRead()
    {
        const string prefix = "[{\"path\":\"C:\\\\x\",\"displayName\":\"x\",\"lastOpenedMs\":1}";
        const string suffix = "]";
        int padding = RecentVaultsStore.MaxFileBytes
            - Encoding.UTF8.GetByteCount(prefix)
            - Encoding.UTF8.GetByteCount(suffix);
        File.WriteAllBytes(
            StorePath,
            Encoding.UTF8.GetBytes(prefix + new string(' ', padding) + suffix));

        RecentVault entry = Assert.Single(CreateStore().Load());
        Assert.Equal("x", entry.DisplayName);
    }

    private string StorePath => Path.Combine(_directory, "recent-vaults.json");
    private RecentVaultsStore CreateStore() => new(StorePath);
}
