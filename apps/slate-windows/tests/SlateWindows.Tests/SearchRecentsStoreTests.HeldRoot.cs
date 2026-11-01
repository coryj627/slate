// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using SlateWindows.Search;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 44 WB-28 PR 2, open item 2: the search source's cached recents store
/// reaches the vault through the CURRENT session's held root, so it is
/// rebuilt for each session, not each spelling.
/// </summary>
public sealed partial class SearchRecentsStoreTests
{
    [Fact]
    public void ASamePathReopenReadsAndWritesThroughTheNewSession()
    {
        using FixtureVault vault = FixtureVault.Create(0, "search-recents-reopen");
        VaultSession? current = VaultSession.OpenFilesystem(vault.Root);
        var source = new VaultSearchSource(() => current, () => vault.Root);
        try
        {
            source.RecordRecent("first");
            Assert.Null(source.LastRecentsSaveError);

            current.Dispose();
            current = VaultSession.OpenFilesystem(vault.Root);

            source.RecordRecent("second");
            Assert.Null(source.LastRecentsSaveError);
            Assert.Equal(new[] { "second", "first" }, source.LoadRecents());
        }
        finally
        {
            current?.Dispose();
        }
    }

    [Fact]
    public void ARetargetedSpellingReadsOnlyTheNewVaultsRecents()
    {
        using FixtureVault first = FixtureVault.Create(0, "search-recents-a");
        using FixtureVault second = FixtureVault.Create(0, "search-recents-b");
        string spelling = Path.Combine(Path.GetTempPath(), $"slate-search-retarget-{Guid.NewGuid():N}");
        Junctions.Create(spelling, first.Root);
        VaultSession? current = null;
        try
        {
            current = VaultSession.OpenFilesystem(spelling);
            var source = new VaultSearchSource(() => current, () => spelling);
            source.RecordRecent("only-in-a");
            Assert.Equal(new[] { "only-in-a" }, source.LoadRecents());

            current.Dispose();
            current = null;
            Junctions.Retarget(spelling, second.Root);
            WriteRecentsFile(second.Root, "[\"only-in-b\"]");
            current = VaultSession.OpenFilesystem(spelling);

            Assert.Equal(new[] { "only-in-b" }, source.LoadRecents());
            Assert.Null(source.LastRecentsSaveError);
        }
        finally
        {
            current?.Dispose();
            Junctions.Remove(spelling);
        }
    }

    [Fact]
    public void NoVaultOpenLoadsNothingAndCachesNoStore()
    {
        using FixtureVault vault = FixtureVault.Create(0, "search-recents-none");
        VaultSession? current = null;
        var source = new VaultSearchSource(() => current, () => vault.Root);

        Assert.Empty(source.LoadRecents());
        source.RecordRecent("nowhere");
        Assert.False(source.HasCachedRecentsForTests);
        Assert.False(File.Exists(Path.Combine(vault.Root, ".slate", "search-recents.json")));

        current = VaultSession.OpenFilesystem(vault.Root);
        try
        {
            source.RecordRecent("somewhere");
            Assert.True(source.HasCachedRecentsForTests);
        }
        finally
        {
            current.Dispose();
            current = null;
        }

        Assert.Empty(source.LoadRecents());
        Assert.False(source.HasCachedRecentsForTests, "closing the vault drops the cached store");
    }
}
