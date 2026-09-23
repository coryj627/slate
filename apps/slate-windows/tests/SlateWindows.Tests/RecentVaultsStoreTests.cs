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
        var output = new StringWriter();
        TextWriter original = Console.Error;
        try
        {
            Console.SetError(output);
            Assert.Empty(CreateStore().Load());
        }
        finally
        {
            Console.SetError(original);
        }

        string logged = output.ToString();
        Assert.Contains(
            $"SlateWindows.{HostDiagnosticEvent.RecentVaultsPayloadRejected}",
            logged,
            StringComparison.Ordinal);
        Assert.Contains(
            $"observedBytes={RecentVaultsStore.MaxFileBytes + 1}",
            logged,
            StringComparison.Ordinal);
        Assert.Contains(
            $"maximumBytes={RecentVaultsStore.MaxFileBytes}",
            logged,
            StringComparison.Ordinal);
        Assert.DoesNotContain(StorePath, logged, StringComparison.Ordinal);
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

    /// <summary>W7-7 PR 3 (#1246, R-4): a recent vault's button is the one
    /// stop in its row, so its name must tell it apart from its siblings.
    /// The display name alone, unless another recent vault shares it (in
    /// any case, as speech would) — then the path too. Two "Notes" folders
    /// that read alike are what axe's SiblingUniqueAndFocusable failed on
    /// the welcome scan once the containers stopped separating them.</summary>
    [Fact]
    public void ASharedDisplayNameIsSpokenWithItsPath()
    {
        var alpha = new RecentVault(@"C:\Vaults\Alpha", "Alpha", 1);
        var notes = new RecentVault(@"C:\Work\Notes", "Notes", 2);
        var otherNotes = new RecentVault(@"D:\Home\notes", "notes", 3);
        RecentVault[] all = [alpha, notes, otherNotes];

        Assert.Equal("Alpha", RecentVault.SpokenName(alpha, all));
        Assert.Equal(@"Notes, C:\Work\Notes", RecentVault.SpokenName(notes, all));
        Assert.Equal(@"notes, D:\Home\notes", RecentVault.SpokenName(otherNotes, all));
        Assert.Equal("Notes", RecentVault.SpokenName(notes, [alpha, notes]));
    }

    private string StorePath => Path.Combine(_directory, "recent-vaults.json");
    private RecentVaultsStore CreateStore() => new(StorePath);
}
