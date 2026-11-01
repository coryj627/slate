// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 44 WB-28 PR 2, design item 7: a persistent identity keys the device
/// recents file; the path-keyed file and the old in-vault file migrate into
/// it, and a source is deleted only after it was read and the destination's
/// commit was read back.
/// </summary>
public sealed partial class FileRecentsStoreTests
{
    private static readonly VaultRootIdentity Persistent = new(7, 0x1_0000_0001, 42, true);

    [Fact]
    public void APathKeyedRecentsFileMigratesToTheIdentityKeyOnce()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-migrate");
        string deviceRoot = Path.Combine(fixture.Root, "device-state");
        new FileRecentsStore(fixture.Root, localAppDataRoot: deviceRoot).Replace(["a.md", "b.md"]);

        var store = new FileRecentsStore(fixture.Root, Persistent, deviceRoot);

        Assert.NotEqual(store.PathKeyedPathForTests, store.FilePathForTests);
        Assert.Equal(["a.md", "b.md"], store.Load());
        Assert.Equal(["a.md", "b.md"], ReadList(store.FilePathForTests));
        Assert.False(File.Exists(store.PathKeyedPathForTests), "the migrated source is deleted");
        Assert.Equal(["a.md", "b.md"], store.Load());
        Assert.Single(Directory.GetFiles(deviceRoot));
    }

    [Fact]
    public void TheIdentityKeyIsTheLosslessIdentity()
    {
        Assert.Equal(
            "0000000000000007-0000000100000001000000000000002a",
            FileRecentsStore.IdentityText(Persistent));
        string deviceRoot = Path.Combine(Path.GetTempPath(), $"slate-recents-key-{Guid.NewGuid():N}");
        var store = new FileRecentsStore(@"C:\vault", Persistent, deviceRoot);
        string expected = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(FileRecentsStore.IdentityText(Persistent))));
        Assert.Equal(Path.Combine(deviceRoot, expected + ".json"), store.FilePathForTests);
        // The high bits are part of the key: two ids that differ only there
        // are two files.
        var low = new FileRecentsStore(@"C:\vault", Persistent with { IdHigh = 1 }, deviceRoot);
        Assert.NotEqual(store.FilePathForTests, low.FilePathForTests);
    }

    [Fact]
    public void AFailedDestinationCommitKeepsThePathKeyedFile()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-commit");
        string deviceRoot = Path.Combine(fixture.Root, "device-state");
        new FileRecentsStore(fixture.Root, localAppDataRoot: deviceRoot).Replace(["a.md", "b.md"]);
        var store = new FileRecentsStore(fixture.Root, Persistent, deviceRoot);
        byte[] source = File.ReadAllBytes(store.PathKeyedPathForTests);
        Directory.CreateDirectory(store.FilePathForTests);

        Assert.Equal(["a.md", "b.md"], store.Load());
        Assert.Equal(source, File.ReadAllBytes(store.PathKeyedPathForTests));
        Assert.False(File.Exists(store.FilePathForTests), "no identity-keyed file");

        Directory.Delete(store.FilePathForTests);
        Assert.Equal(["a.md", "b.md"], store.Load());
        Assert.Equal(["a.md", "b.md"], ReadList(store.FilePathForTests));
        Assert.False(File.Exists(store.PathKeyedPathForTests));
    }

    [Fact]
    public void AnUnreadablePathKeyedFileIsNeitherMigratedNorDeleted()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-unreadable");
        string deviceRoot = Path.Combine(fixture.Root, "device-state");
        new FileRecentsStore(fixture.Root, localAppDataRoot: deviceRoot).Replace(["a.md", "b.md"]);
        var store = new FileRecentsStore(fixture.Root, Persistent, deviceRoot);
        byte[] source = File.ReadAllBytes(store.PathKeyedPathForTests);

        using (new FileStream(store.PathKeyedPathForTests, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Empty(store.Load());
            Assert.False(File.Exists(store.FilePathForTests), "nothing is committed from an unread source");
        }

        Assert.Equal(source, File.ReadAllBytes(store.PathKeyedPathForTests));
        Assert.Equal(["a.md", "b.md"], store.Load());
        Assert.False(File.Exists(store.PathKeyedPathForTests));
    }

    [Fact]
    public void AnInterruptedMigrationCompletesOnALaterLoad()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-interrupted");
        string deviceRoot = Path.Combine(fixture.Root, "device-state");
        new FileRecentsStore(fixture.Root, localAppDataRoot: deviceRoot).Replace(["a.md", "b.md"]);
        var store = new FileRecentsStore(fixture.Root, Persistent, deviceRoot);

        using (new FileStream(store.PathKeyedPathForTests, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Equal(["c.md"], store.Add("c.md"));
        }

        Assert.Equal(["c.md", "a.md", "b.md"], store.Load());
        Assert.Equal(["c.md", "a.md", "b.md"], ReadList(store.FilePathForTests));
        Assert.False(File.Exists(store.PathKeyedPathForTests));
    }

    [Fact]
    public void AnUnreadableInVaultRecentsFileIsNotDeleted()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-in-vault");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        string deviceRoot = Path.Combine(Path.GetTempPath(), $"slate-recents-device-{Guid.NewGuid():N}");
        string inVault = WriteInVault(fixture.Root, ["a.md"]);
        byte[] before = File.ReadAllBytes(inVault);
        VaultRootIdentity identity = Assert.IsType<VaultRootIdentity>(session.RootIdentity());
        Assert.True(identity.Persistent, "premise: an NTFS fixture has a persistent identity");
        var store = new FileRecentsStore(fixture.Root, identity, deviceRoot, VaultRootAnchor.Of(session));
        try
        {
            using (new FileStream(inVault, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.Empty(store.Load());
                Assert.False(File.Exists(store.FilePathForTests), "nothing is committed from an unread source");
            }

            Assert.Equal(before, File.ReadAllBytes(inVault));
            Assert.Equal(["a.md"], store.Load());
            Assert.False(File.Exists(inVault), "the in-vault file goes once its entries are committed");
        }
        finally
        {
            DeleteQuietly(deviceRoot);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnInVaultRecentsFileUnreadableBeforeAnAddMigratesOnALaterLoad(bool persistent)
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-add-first");
        const string Seam = "SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED";
        string? prior = Environment.GetEnvironmentVariable(Seam);
        string deviceRoot = Path.Combine(Path.GetTempPath(), $"slate-recents-device-{Guid.NewGuid():N}");
        try
        {
            if (!persistent)
            {
                Environment.SetEnvironmentVariable(Seam, Path.GetFileName(fixture.Root));
            }

            using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
            VaultRootIdentity identity = Assert.IsType<VaultRootIdentity>(session.RootIdentity());
            Assert.Equal(persistent, identity.Persistent);
            string inVault = WriteInVault(fixture.Root, ["a.md", "b.md"]);
            byte[] before = File.ReadAllBytes(inVault);
            var store = new FileRecentsStore(fixture.Root, identity, deviceRoot, VaultRootAnchor.Of(session));

            using (new FileStream(inVault, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.Empty(store.Load());
                Assert.Equal(["c.md"], store.Add("c.md"));
                Assert.Equal(["c.md"], ReadList(store.FilePathForTests));
            }

            Assert.Equal(before, File.ReadAllBytes(inVault));
            Assert.Equal(["c.md", "a.md", "b.md"], store.Load());
            Assert.Equal(["c.md", "a.md", "b.md"], ReadList(store.FilePathForTests));
            Assert.False(File.Exists(inVault));
            int probes = store.LegacyProbesForTests;
            _ = store.Load();
            Assert.Equal(probes, store.LegacyProbesForTests);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Seam, prior);
            DeleteQuietly(deviceRoot);
        }
    }

    [Fact]
    public void AFailedInVaultCommitIsRetriedAfterTheDestinationExists()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-retry");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        string deviceRoot = Path.Combine(Path.GetTempPath(), $"slate-recents-device-{Guid.NewGuid():N}");
        VaultRootIdentity identity = Assert.IsType<VaultRootIdentity>(session.RootIdentity());
        var store = new FileRecentsStore(fixture.Root, identity, deviceRoot, VaultRootAnchor.Of(session));
        Directory.CreateDirectory(deviceRoot);
        File.WriteAllText(store.FilePathForTests, "[\"c.md\"]");
        byte[] destination = File.ReadAllBytes(store.FilePathForTests);
        string inVault = WriteInVault(fixture.Root, ["a.md", "b.md"]);
        byte[] before = File.ReadAllBytes(inVault);
        var logged = new System.Collections.Concurrent.ConcurrentQueue<string>();
        Action<string>? previous = HostLog.ObserverForTests;
        HostLog.ObserverForTests = line =>
        {
            previous?.Invoke(line);
            logged.Enqueue(line);
        };
        try
        {
            File.SetAttributes(store.FilePathForTests, FileAttributes.ReadOnly);
            Assert.Equal(["c.md", "a.md", "b.md"], store.Load());
            Assert.Single(logged, line => line.Contains("FileRecentsPersistFailed", StringComparison.Ordinal));
            Assert.Equal(destination, File.ReadAllBytes(store.FilePathForTests));
            Assert.Equal(before, File.ReadAllBytes(inVault));

            File.SetAttributes(store.FilePathForTests, FileAttributes.Normal);
            Assert.Equal(["c.md", "a.md", "b.md"], store.Load());
            Assert.Equal(["c.md", "a.md", "b.md"], ReadList(store.FilePathForTests));
            Assert.False(File.Exists(inVault));
        }
        finally
        {
            HostLog.ObserverForTests = previous;
            if (File.Exists(store.FilePathForTests))
            {
                File.SetAttributes(store.FilePathForTests, FileAttributes.Normal);
            }

            DeleteQuietly(deviceRoot);
        }
    }

    [Fact]
    public void ClearingRecentsDropsAnUnmigratedInVaultFile()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-clear");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        string deviceRoot = Path.Combine(Path.GetTempPath(), $"slate-recents-device-{Guid.NewGuid():N}");
        VaultRootIdentity identity = Assert.IsType<VaultRootIdentity>(session.RootIdentity());
        var store = new FileRecentsStore(fixture.Root, identity, deviceRoot, VaultRootAnchor.Of(session));
        string inVault = WriteInVault(fixture.Root, ["a.md"]);
        try
        {
            using (new FileStream(inVault, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.Empty(store.Load());
            }

            store.Clear();
            Assert.Empty(ReadList(store.FilePathForTests));
            Assert.False(File.Exists(inVault), "Clear drops the unmigrated in-vault file");
            Assert.Empty(store.Load());
        }
        finally
        {
            DeleteQuietly(deviceRoot);
        }
    }

    /// <summary>Clear means none: a path-keyed source that outlived its
    /// migration (the destination locked at the load) does not merge its
    /// entries back.</summary>
    [Fact]
    public void ClearingRecentsDropsAPathKeyedSourceTheMigrationLeft()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-clear-source");
        string deviceRoot = Path.Combine(fixture.Root, "device-state");
        new FileRecentsStore(fixture.Root, localAppDataRoot: deviceRoot).Replace(["a.md", "b.md"]);
        var store = new FileRecentsStore(fixture.Root, Persistent, deviceRoot);
        Directory.CreateDirectory(store.FilePathForTests);
        Assert.Equal(["a.md", "b.md"], store.Load());
        Directory.Delete(store.FilePathForTests);

        store.Clear();

        Assert.False(File.Exists(store.PathKeyedPathForTests), "the source would merge the cleared entries back");
        Assert.Empty(store.Load());
    }

    /// <summary>The sidebar's rename reconciliation replaces the list a load
    /// merged; the source it merged goes too, so a renamed path does not come
    /// back from it.</summary>
    [Fact]
    public void AReplacementDropsTheReadablePathKeyedSourceItWasMergedFrom()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-replace-source");
        string deviceRoot = Path.Combine(fixture.Root, "device-state");
        new FileRecentsStore(fixture.Root, localAppDataRoot: deviceRoot).Replace(["old.md", "b.md"]);
        var store = new FileRecentsStore(fixture.Root, Persistent, deviceRoot);
        Directory.CreateDirectory(store.FilePathForTests);
        Assert.Equal(["old.md", "b.md"], store.Load());
        Directory.Delete(store.FilePathForTests);

        store.Replace(["new.md", "b.md"]);

        Assert.False(File.Exists(store.PathKeyedPathForTests));
        Assert.Equal(["new.md", "b.md"], store.Load());
    }

    /// <summary>A path-keyed source that cannot be read when the list is
    /// replaced keeps its entries for a later load.</summary>
    [Fact]
    public void AReplacementKeepsAPathKeyedSourceItCouldNotRead()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-replace-held");
        string deviceRoot = Path.Combine(fixture.Root, "device-state");
        new FileRecentsStore(fixture.Root, localAppDataRoot: deviceRoot).Replace(["a.md"]);
        var store = new FileRecentsStore(fixture.Root, Persistent, deviceRoot);
        // Held against reads only: a delete of it would still succeed.
        using (new FileStream(store.PathKeyedPathForTests, FileMode.Open, FileAccess.Read, FileShare.Delete))
        {
            store.Replace(["c.md"]);
            Assert.True(File.Exists(store.PathKeyedPathForTests), "a source that could not be read was deleted");
        }

        Assert.True(File.Exists(store.PathKeyedPathForTests));
        Assert.Equal(["c.md", "a.md"], store.Load());
    }

    /// <summary>Codex round 1: the merge uses the destination as it was read,
    /// so a destination that turns unreadable between that read and the
    /// merge (another process holds it against reads but not replacement)
    /// does not lose its entries to an empty list.</summary>
    [Theory]
    [InlineData("path-keyed")]
    [InlineData("in-vault")]
    public void ADestinationThatTurnsUnreadableKeepsItsEntriesInTheMerge(string source)
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-turns-unreadable");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        string deviceRoot = Path.Combine(Path.GetTempPath(), $"slate-recents-device-{Guid.NewGuid():N}");
        VaultRootIdentity identity = Assert.IsType<VaultRootIdentity>(session.RootIdentity());
        if (source == "path-keyed")
        {
            new FileRecentsStore(fixture.Root, localAppDataRoot: deviceRoot).Replace(["old.md"]);
        }
        else
        {
            _ = WriteInVault(fixture.Root, ["old.md"]);
        }

        var store = new FileRecentsStore(fixture.Root, identity, deviceRoot, VaultRootAnchor.Of(session));
        Directory.CreateDirectory(deviceRoot);
        File.WriteAllText(store.FilePathForTests, "[\"new.md\"]");
        FileStream? hold = null;
        store.AfterDestinationReadForTests = () =>
            hold = new FileStream(store.FilePathForTests, FileMode.Open, FileAccess.Read, FileShare.Delete);
        try
        {
            IReadOnlyList<string> loaded = store.Load();
            hold?.Dispose();
            hold = null;
            store.AfterDestinationReadForTests = null;
            Assert.Equal(["new.md", "old.md"], loaded);
            // The replace over a file another process holds open fails here,
            // so the destination keeps its own entries and the source stays
            // for the next load, which completes the migration.
            Assert.Contains("new.md", ReadList(store.FilePathForTests));
            Assert.Equal(["new.md", "old.md"], store.Load());
            Assert.Equal(["new.md", "old.md"], ReadList(store.FilePathForTests));
        }
        finally
        {
            hold?.Dispose();
            DeleteQuietly(deviceRoot);
        }
    }

    /// <summary>Codex round 1: a read-only path-keyed source still goes once
    /// the list is cleared or replaced, so it cannot merge the entries back,
    /// in this store or the next one.</summary>
    [Theory]
    [InlineData("clear")]
    [InlineData("replace")]
    public void AReadOnlyPathKeyedSourceIsDroppedOnceConsumed(string action)
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-read-only-source");
        string deviceRoot = Path.Combine(fixture.Root, "device-state");
        new FileRecentsStore(fixture.Root, localAppDataRoot: deviceRoot).Replace(["old.md", "b.md"]);
        var store = new FileRecentsStore(fixture.Root, Persistent, deviceRoot);
        File.SetAttributes(store.PathKeyedPathForTests, FileAttributes.ReadOnly);
        try
        {
            // The migration's commit fails (a folder at the destination's
            // name), so the source is still there when the list is replaced.
            Directory.CreateDirectory(store.FilePathForTests);
            Assert.Equal(["old.md", "b.md"], store.Load());
            Assert.True(File.Exists(store.PathKeyedPathForTests), "premise: the source outlived the load");
            Directory.Delete(store.FilePathForTests);
            if (action == "clear")
            {
                store.Clear();
            }
            else
            {
                store.Replace(["new.md", "b.md"]);
            }

            string[] expected = action == "clear" ? [] : ["new.md", "b.md"];
            Assert.False(File.Exists(store.PathKeyedPathForTests), "the read-only source stayed and would merge back");
            Assert.Equal(expected, store.Load());
            Assert.Equal(expected, new FileRecentsStore(fixture.Root, Persistent, deviceRoot).Load());
        }
        finally
        {
            if (File.Exists(store.PathKeyedPathForTests))
            {
                File.SetAttributes(store.PathKeyedPathForTests, FileAttributes.Normal);
            }
        }
    }

    /// <summary>Codex round 2: a read-only in-vault file is deleted once its
    /// entries are committed, so neither a Clear nor a fresh store can bring
    /// them back.</summary>
    [Fact]
    public void AReadOnlyInVaultSourceIsDeletedOnceMigrated()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-read-only-in-vault");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        string deviceRoot = Path.Combine(Path.GetTempPath(), $"slate-recents-device-{Guid.NewGuid():N}");
        VaultRootIdentity identity = Assert.IsType<VaultRootIdentity>(session.RootIdentity());
        string inVault = WriteInVault(fixture.Root, ["old.md"]);
        File.SetAttributes(inVault, FileAttributes.ReadOnly);
        try
        {
            var store = new FileRecentsStore(fixture.Root, identity, deviceRoot, VaultRootAnchor.Of(session));
            Assert.Equal(["old.md"], store.Load());
            Assert.False(File.Exists(inVault), "the read-only in-vault file stayed and would merge back");
            store.Clear();
            Assert.Empty(new FileRecentsStore(fixture.Root, identity, deviceRoot, VaultRootAnchor.Of(session)).Load());
        }
        finally
        {
            if (File.Exists(inVault))
            {
                File.SetAttributes(inVault, FileAttributes.Normal);
            }

            DeleteQuietly(deviceRoot);
        }
    }

    /// <summary>Codex round 2, schedule A: a replacement built from a load
    /// that merged the in-vault file (its commit failed) retires that file,
    /// so the path the replacement renamed does not come back.</summary>
    [Fact]
    public void AReplacementRetiresTheInVaultSourceItsLoadMerged()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-replace-in-vault");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        string deviceRoot = Path.Combine(Path.GetTempPath(), $"slate-recents-device-{Guid.NewGuid():N}");
        VaultRootIdentity identity = Assert.IsType<VaultRootIdentity>(session.RootIdentity());
        string inVault = WriteInVault(fixture.Root, ["old.md", "b.md"]);
        var store = new FileRecentsStore(fixture.Root, identity, deviceRoot, VaultRootAnchor.Of(session));
        try
        {
            Directory.CreateDirectory(store.FilePathForTests);
            Assert.Equal(["old.md", "b.md"], store.Load());
            Assert.True(File.Exists(inVault), "premise: the commit failed and the source stayed");
            Directory.Delete(store.FilePathForTests);

            store.Replace(["new.md", "b.md"]);

            Assert.False(File.Exists(inVault), "the merged in-vault file would bring old.md back");
            Assert.Equal(["new.md", "b.md"], new FileRecentsStore(fixture.Root, identity, deviceRoot, VaultRootAnchor.Of(session)).Load());
        }
        finally
        {
            DeleteQuietly(deviceRoot);
        }
    }

    /// <summary>Codex round 2, schedule B: a path-keyed source the load could
    /// not read was never merged, so a replacement keeps it and its entries
    /// merge behind the replacement later.</summary>
    [Fact]
    public void AReplacementKeepsAPathKeyedSourceItsLoadCouldNotRead()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-replace-unmerged");
        string deviceRoot = Path.Combine(fixture.Root, "device-state");
        new FileRecentsStore(fixture.Root, localAppDataRoot: deviceRoot).Replace(["a.md"]);
        var store = new FileRecentsStore(fixture.Root, Persistent, deviceRoot);
        using (new FileStream(store.PathKeyedPathForTests, FileMode.Open, FileAccess.Read, FileShare.Delete))
        {
            Assert.Empty(store.Load());
        }

        store.Replace(["c.md"]);

        Assert.True(File.Exists(store.PathKeyedPathForTests), "a source never merged was deleted");
        Assert.Equal(["c.md", "a.md"], store.Load());
    }

    /// <summary>A source that changed after the load merged it holds entries
    /// the replacement never saw, so the replacement keeps it.</summary>
    [Fact]
    public void AReplacementKeepsAPathKeyedSourceThatChangedSinceItsLoad()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "recents-replace-changed");
        string deviceRoot = Path.Combine(fixture.Root, "device-state");
        var pathKeyed = new FileRecentsStore(fixture.Root, localAppDataRoot: deviceRoot);
        pathKeyed.Replace(["a.md"]);
        var store = new FileRecentsStore(fixture.Root, Persistent, deviceRoot);
        Directory.CreateDirectory(store.FilePathForTests);
        Assert.Equal(["a.md"], store.Load());
        Directory.Delete(store.FilePathForTests);
        pathKeyed.Replace(["z.md", "a.md"]);

        store.Replace(["c.md"]);

        Assert.True(File.Exists(store.PathKeyedPathForTests), "a source with entries the replacement never saw was deleted");
        Assert.Equal(["c.md", "z.md", "a.md"], store.Load());
    }

    private static string WriteInVault(string vaultRoot, string[] entries)
    {
        string directory = Path.Combine(vaultRoot, ".slate");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "file-recents.json");
        File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(entries));
        return path;
    }

    private static string[] ReadList(string path) =>
        JsonSerializer.Deserialize<string[]>(File.ReadAllBytes(path)) ?? [];

    private static void DeleteQuietly(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
