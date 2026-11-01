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
