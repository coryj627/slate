// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Text;
using SlateWindows.Graph;
using SlateWindows.Search;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 44 WB-28 PR 2: the anchored stores reach the vault through the
/// session's held root — a duplicate of core's handle — so a spelling that
/// now names another folder is never followed, and two folders whose weak
/// identities collide are never confused.
/// </summary>
[Collection(nameof(ByPathSwitchCollection))]
public sealed partial class W1AnchoredVaultStoreTests
{
    [Fact]
    public void OpenReachesTheHeldRootWhenTheVaultsSpellingIsRetargeted()
    {
        using var retarget = Retargeted.Make("wb28-open");
        using VaultSession session = retarget.OpenThroughTheSpelling();
        retarget.PointTheSpellingAtTheDecoy();
        Dictionary<string, byte[]> decoy = FilesUnder(retarget.Decoy);

        using (AnchoredVaultStore store = Assert.IsType<AnchoredVaultStore>(
            AnchoredVaultStore.Open(VaultRootAnchor.Of(session), createDirectory: true)))
        {
            store.WriteAtomically("probe.json", Encoding.UTF8.GetBytes("[\"held\"]"));
            Assert.Equal("[\"held\"]", Encoding.UTF8.GetString(store.ReadAllBytesBounded("probe.json", 1024)!));
        }

        Assert.Equal("[\"held\"]", File.ReadAllText(Path.Combine(retarget.Held, ".slate", "probe.json")));
        AssertUnchanged(decoy, retarget.Decoy);
    }

    [Fact]
    public void EveryStoreWritesBeneathTheHeldRootAfterTheSpellingIsRetargeted()
    {
        using var retarget = Retargeted.Make("wb28-stores");
        using VaultSession session = retarget.OpenThroughTheSpelling();
        retarget.PointTheSpellingAtTheDecoy();
        Dictionary<string, byte[]> decoy = FilesUnder(retarget.Decoy);
        string spelling = retarget.Spelling;

        new WorkspacePersistence(spelling, root: VaultRootAnchor.Of(session)).Save(Snapshot("held"));
        new SearchRecentsStore(spelling, VaultRootAnchor.Of(session)).Add("held query");
        new SidebarSettingsStore(spelling, root: VaultRootAnchor.Of(session)).SetOrganization(SidebarSortMode.NameAscending, groupByDate: true);
        new GraphConfigStore(spelling, VaultRootAnchor.Of(session)).Write(SlateUniffiMethods.GraphConfigDefault() with { ConnectionsDepth = 1 });

        string slate = Path.Combine(retarget.Held, ".slate");
        foreach (string name in new[] { "workspace.json", "search-recents.json", "sidebar.json", GraphConfigStore.FileName })
        {
            Assert.True(File.Exists(Path.Combine(slate, name)), $"{name} did not land beneath the held root");
        }

        AssertUnchanged(decoy, retarget.Decoy);
    }

    /// <summary>The reads go through the held root too: after the retarget,
    /// each store's load, and the old in-vault recents file's migration,
    /// see only the held vault, whose spelling now names a decoy with files
    /// of its own.</summary>
    [Fact]
    public void EveryStoreReadsTheHeldRootAfterTheSpellingIsRetargeted()
    {
        using var retarget = Retargeted.Make("wb28-reads");
        foreach ((string root, string tag, SidebarSortMode sort) in new[]
        {
            (retarget.Held, "held", SidebarSortMode.NameDescending),
            (retarget.Decoy, "decoy", SidebarSortMode.ModifiedNewest),
        })
        {
            new WorkspacePersistence(root).Save(Snapshot(tag));
            new SearchRecentsStore(root).Add(tag);
            new SidebarSettingsStore(root).SetOrganization(sort, groupByDate: false);
            File.WriteAllText(Path.Combine(root, ".slate", "file-recents.json"), $"[\"{tag}.md\"]");
        }

        string deviceRoot = Path.Combine(Path.GetTempPath(), $"slate-recents-device-{Guid.NewGuid():N}");
        try
        {
            using VaultSession session = retarget.OpenThroughTheSpelling();
            retarget.PointTheSpellingAtTheDecoy();
            Dictionary<string, byte[]> decoy = FilesUnder(retarget.Decoy);
            string spelling = retarget.Spelling;

            Assert.Equal("held", new WorkspacePersistence(spelling, root: VaultRootAnchor.Of(session)).Load()?.ActiveLeaf);
            Assert.Equal(["held"], new SearchRecentsStore(spelling, VaultRootAnchor.Of(session)).Load());
            Assert.Equal(
                SidebarSortMode.NameDescending,
                new SidebarSettingsStore(spelling, root: VaultRootAnchor.Of(session)).Load().SortMode);
            var recents = new FileRecentsStore(spelling, session.RootIdentity(), deviceRoot, VaultRootAnchor.Of(session));
            Assert.Equal(["held.md"], recents.Load());
            Assert.False(File.Exists(Path.Combine(retarget.Held, ".slate", "file-recents.json")), "the held vault's old file migrated");
            AssertUnchanged(decoy, retarget.Decoy);
        }
        finally
        {
            try
            {
                Directory.Delete(deviceRoot, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void ARootForcedToAnotherVaultsWeakIndexIsNeverReachedThroughIt()
    {
        const string WeakSeam = "SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED";
        const string CollideSeam = "SLATE_TEST_ROOT_FILE_INDEX_COLLIDES";
        using var retarget = Retargeted.Make("wb28-collide");
        string? priorWeak = Environment.GetEnvironmentVariable(WeakSeam);
        string? priorCollide = Environment.GetEnvironmentVariable(CollideSeam);
        try
        {
            Environment.SetEnvironmentVariable(WeakSeam, retarget.Token);
            Environment.SetEnvironmentVariable(CollideSeam, retarget.Token);
            using VaultSession a = retarget.OpenThroughTheSpelling();
            using VaultSession b = VaultSession.OpenFilesystem(retarget.Decoy);
            VaultRootIdentity identity = Assert.IsType<VaultRootIdentity>(a.RootIdentity());
            Assert.False(identity.Persistent, "premise: a weak identity");
            Assert.Equal(identity, b.RootIdentity());
            retarget.PointTheSpellingAtTheDecoy();
            Dictionary<string, byte[]> decoy = FilesUnder(retarget.Decoy);

            using (AnchoredVaultStore store = AnchoredVaultStore.Open(VaultRootAnchor.Of(a), createDirectory: true)!)
            {
                store.WriteAtomically("probe.json", Encoding.UTF8.GetBytes("[\"a\"]"));
            }

            Assert.Equal("[\"a\"]", File.ReadAllText(Path.Combine(retarget.Held, ".slate", "probe.json")));
            AssertUnchanged(decoy, retarget.Decoy);
            Dictionary<string, byte[]> held = FilesUnder(retarget.Held);

            using (AnchoredVaultStore store = AnchoredVaultStore.Open(VaultRootAnchor.Of(b), createDirectory: true)!)
            {
                store.WriteAtomically("probe.json", Encoding.UTF8.GetBytes("[\"b\"]"));
            }

            Assert.Equal("[\"b\"]", File.ReadAllText(Path.Combine(retarget.Decoy, ".slate", "probe.json")));
            AssertUnchanged(held, retarget.Held);
        }
        finally
        {
            Environment.SetEnvironmentVariable(WeakSeam, priorWeak);
            Environment.SetEnvironmentVariable(CollideSeam, priorCollide);
        }
    }

    [Fact]
    public void AStoreRefusesOnceItsSessionIsDisposed()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "wb28-disposed");
        var session = VaultSession.OpenFilesystem(fixture.Root);
        VaultRootAnchor anchor = VaultRootAnchor.Of(session);
        session.Dispose();
        string[] before = Directory.GetFiles(Path.Combine(fixture.Root, ".slate"), "*", SearchOption.AllDirectories);

        Assert.Throws<IOException>(() => AnchoredVaultStore.Open(anchor, createDirectory: true));
        Assert.Throws<IOException>(() => new WorkspacePersistence(fixture.Root, root: anchor).Save(Snapshot("late")));
        Assert.Equal(
            before.Order(StringComparer.Ordinal),
            Directory.GetFiles(Path.Combine(fixture.Root, ".slate"), "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
    }

    /// <summary>A by-path root that is a junction is refused, never
    /// followed, as main's root open refused it.</summary>
    [Fact]
    public void AByPathRootThatIsAJunctionIsRefused()
    {
        using FixtureVault target = FixtureVault.Create(0, "wb28-by-path-target");
        string junction = target.Root + "-junction";
        Junctions.Create(junction, target.Root);
        try
        {
            Assert.Throws<IOException>(() => new WorkspacePersistence(junction).Save(Snapshot("through")));
            Assert.False(Directory.Exists(Path.Combine(target.Root, ".slate")), "the save followed the junction");
        }
        finally
        {
            Junctions.Remove(junction);
        }
    }

    /// <summary>A store built with no anchor refuses unless the process
    /// allowed the by-path root, as only this test assembly does. The switch
    /// is process-wide, so this class runs alone.</summary>
    [Fact]
    public void AStoreGivenNoAnchorRefusesOutsideTheTestAssembly()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "wb28-by-path");
        string slate = Path.Combine(fixture.Root, ".slate");
        try
        {
            VaultRootAnchor.ByPathAllowed = false;
            Assert.Throws<IOException>(() => new WorkspacePersistence(fixture.Root).Save(Snapshot("refused")));
            Assert.False(Directory.Exists(slate), "nothing is written");
        }
        finally
        {
            VaultRootAnchor.ByPathAllowed = true;
        }

        new WorkspacePersistence(fixture.Root).Save(Snapshot("written"));
        Assert.True(File.Exists(Path.Combine(slate, "workspace.json")));
    }

    /// <summary>A <c>.slate</c> that is a junction is refused, never
    /// followed: <c>.slate</c> is opened as itself beneath the root.</summary>
    [Fact]
    public void AStoreDirectoryThatIsAJunctionIsRefused()
    {
        using FixtureVault vault = FixtureVault.Create(0, "wb28-slate-junction");
        using FixtureVault external = FixtureVault.Create(0, "wb28-slate-external");
        Junctions.Create(Path.Combine(vault.Root, ".slate"), external.Root);
        try
        {
            Assert.Throws<IOException>(() => AnchoredVaultStore.Open(VaultRootAnchor.ByPathForTests(vault.Root), createDirectory: true));
            Assert.Throws<IOException>(() => new WorkspacePersistence(vault.Root).Save(Snapshot("external")));
            Assert.Empty(Directory.GetFiles(external.Root, "*", SearchOption.AllDirectories));
        }
        finally
        {
            Junctions.Remove(Path.Combine(vault.Root, ".slate"));
        }
    }

    // A file an open session holds (its index database) is compared by name.
    private static Dictionary<string, byte[]> FilesUnder(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), ReadOrHeld, StringComparer.OrdinalIgnoreCase);

    private static byte[] ReadOrHeld(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static void AssertUnchanged(Dictionary<string, byte[]> before, string root)
    {
        Dictionary<string, byte[]> after = FilesUnder(root);
        Assert.Equal(before.Keys.Order(StringComparer.OrdinalIgnoreCase), after.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach ((string name, byte[] bytes) in before)
        {
            Assert.True(bytes.AsSpan().SequenceEqual(after[name]), $"{name} changed");
        }
    }

    /// <summary>The held vault <c>T\a</c>, a decoy <c>T\b</c> with its own
    /// <c>.slate</c>, and the spelling <c>T\vault</c>, a junction to
    /// <c>T\a</c> until <see cref="PointTheSpellingAtTheDecoy"/>.</summary>
    private sealed class Retargeted : IDisposable
    {
        private Retargeted(string token)
        {
            Token = token;
            string parent = Path.Combine(Path.GetTempPath(), token);
            Held = Path.Combine(parent, "a");
            Decoy = Path.Combine(parent, "b");
            Spelling = Path.Combine(parent, "vault");
            Directory.CreateDirectory(Held);
            Directory.CreateDirectory(Path.Combine(Decoy, ".slate"));
            File.WriteAllText(Path.Combine(Held, "note.md"), "# Held\n");
            File.WriteAllText(Path.Combine(Decoy, "decoy.md"), "# Decoy\n");
            File.WriteAllText(Path.Combine(Decoy, ".slate", "workspace.json"), "decoy");
            Junctions.Create(Spelling, Held);
        }

        public string Token { get; }

        public string Held { get; }

        public string Decoy { get; }

        public string Spelling { get; }

        public static Retargeted Make(string label) => new($"{label}-{Guid.NewGuid():N}");

        public VaultSession OpenThroughTheSpelling() => VaultSession.OpenFilesystem(Spelling);

        public void PointTheSpellingAtTheDecoy() => Junctions.Retarget(Spelling, Decoy);

        public void Dispose()
        {
            Junctions.Remove(Spelling);
            try
            {
                Directory.Delete(Path.GetDirectoryName(Held)!, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

/// <summary>Plan 44 WB-28 PR 2: <see cref="VaultRootAnchor.ByPathAllowed"/> is
/// process-wide, so the class that switches it runs alone.</summary>
[CollectionDefinition(nameof(ByPathSwitchCollection), DisableParallelization = true)]
public sealed class ByPathSwitchCollection;
