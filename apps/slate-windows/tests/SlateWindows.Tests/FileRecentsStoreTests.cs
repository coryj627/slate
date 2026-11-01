// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 44 WB-28 design item 7: a weak root identity (a FAT, FAT32 or exFAT
/// vault, whose file index changes when the folder is renamed or moved and
/// repeats on a cloned volume) keeps the path key every Windows vault used
/// on main, and moves or deletes no recents file. In PR 1 every identity
/// keeps the path key; PR 2 adds the identity key for a persistent one.
/// </summary>
public sealed partial class FileRecentsStoreTests
{
    [Fact]
    public void AWeakIdentityKeepsThePathKeyAndMigratesNothing()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "weak-recents");
        string deviceRoot = Path.Combine(fixture.Root, "device-state");
        var pathKeyed = new FileRecentsStore(fixture.Root, localAppDataRoot: deviceRoot);
        pathKeyed.Add("note0.md");
        string unrelated = Path.Combine(deviceRoot, "0123456789ABCDEF.json");
        File.WriteAllText(unrelated, "[\"elsewhere.md\"]");

        var weak = new VaultRootIdentity(7, 0, 42, false);
        var store = new FileRecentsStore(fixture.Root, weak, deviceRoot);

        Assert.Equal(pathKeyed.FilePathForTests, store.FilePathForTests);
        Assert.Equal(["note0.md"], store.Load());
        Assert.Equal("[\"elsewhere.md\"]", File.ReadAllText(unrelated));
        Assert.Equal(2, Directory.GetFiles(deviceRoot).Length);
    }
}
