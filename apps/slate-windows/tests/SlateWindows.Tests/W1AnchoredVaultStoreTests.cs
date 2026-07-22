// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;

namespace SlateWindows.Tests;

public sealed class W1AnchoredVaultStoreTests
{
    [Fact]
    public void WorkspaceReadAndReplaceDoNotFollowAnExternalFileReparsePoint()
    {
        using FixtureVault external = FixtureVault.Create(1, "workspace-anchor-external");
        using FixtureVault local = FixtureVault.Create(1, "workspace-anchor-local");
        var externalStore = new WorkspacePersistence(external.Root);
        externalStore.Save(Snapshot("external"));
        string externalPath = Path.Combine(external.Root, ".slate", "workspace.json");
        byte[] sentinel = File.ReadAllBytes(externalPath);

        string localDirectory = Path.Combine(local.Root, ".slate");
        Directory.CreateDirectory(localDirectory);
        string localPath = Path.Combine(localDirectory, "workspace.json");
        File.CreateSymbolicLink(localPath, externalPath);

        Assert.Null(new WorkspacePersistence(local.Root).Load());

        new WorkspacePersistence(local.Root).Save(Snapshot("local"));

        Assert.Equal(sentinel, File.ReadAllBytes(externalPath));
        Assert.False(
            (File.GetAttributes(localPath) & FileAttributes.ReparsePoint) != 0);
        WorkspaceSnapshot loaded = Assert.IsType<WorkspaceSnapshot>(
            new WorkspacePersistence(local.Root).Load());
        Assert.Equal("local", loaded.ActiveLeaf);
    }

    [Fact]
    public void SidebarReadAndUpdateFailClosedOnAnExternalFileReparsePoint()
    {
        using FixtureVault external = FixtureVault.Create(0, "sidebar-anchor-external");
        using FixtureVault local = FixtureVault.Create(0, "sidebar-anchor-local");
        string externalPath = Path.Combine(external.Root, "sentinel.json");
        byte[] sentinel = Encoding.UTF8.GetBytes(
            """{"version":1,"grouping":"dateBuckets"}""");
        File.WriteAllBytes(externalPath, sentinel);

        string localDirectory = Path.Combine(local.Root, ".slate");
        Directory.CreateDirectory(localDirectory);
        string localPath = Path.Combine(localDirectory, "sidebar.json");
        File.CreateSymbolicLink(localPath, externalPath);

        SidebarSettingsSnapshot loaded = new SidebarSettingsStore(local.Root).Load();
        Assert.NotNull(loaded.ReadOnlyReason);
        Assert.False(loaded.GroupByDate);
        Assert.Throws<InvalidOperationException>(() =>
            new SidebarSettingsStore(local.Root).SetOrganization(
                SidebarSortMode.CreatedNewest,
                groupByDate: true));

        Assert.Equal(sentinel, File.ReadAllBytes(externalPath));
        Assert.True(
            (File.GetAttributes(localPath) & FileAttributes.ReparsePoint) != 0);
    }

    [Fact]
    public void LegacyRecentsMigrationDoesNotReadOrDeleteAnExternalFileReparsePoint()
    {
        using FixtureVault external = FixtureVault.Create(0, "recents-anchor-external");
        using FixtureVault local = FixtureVault.Create(0, "recents-anchor-local");
        string externalPath = Path.Combine(external.Root, "sentinel.json");
        byte[] sentinel = Encoding.UTF8.GetBytes("""["external.md"]""");
        File.WriteAllBytes(externalPath, sentinel);

        string localDirectory = Path.Combine(local.Root, ".slate");
        Directory.CreateDirectory(localDirectory);
        string localPath = Path.Combine(localDirectory, "file-recents.json");
        File.CreateSymbolicLink(localPath, externalPath);
        var store = new FileRecentsStore(
            local.Root,
            localAppDataRoot: Path.Combine(local.Root, "device-state"));

        Assert.Empty(store.Load());
        Assert.Equal(sentinel, File.ReadAllBytes(externalPath));
        Assert.True(
            (File.GetAttributes(localPath) & FileAttributes.ReparsePoint) != 0);
    }

    [Fact]
    public void ValidLegacyRecentsMigrateAndDeleteThroughTheAnchoredDirectory()
    {
        using FixtureVault local = FixtureVault.Create(0, "recents-anchor-migrate");
        string localDirectory = Path.Combine(local.Root, ".slate");
        Directory.CreateDirectory(localDirectory);
        string legacyPath = Path.Combine(localDirectory, "file-recents.json");
        File.WriteAllText(legacyPath, """["first.md","second.md"]""");
        var store = new FileRecentsStore(
            local.Root,
            localAppDataRoot: Path.Combine(local.Root, "device-state"));

        Assert.Equal(["first.md", "second.md"], store.Load());
        Assert.False(File.Exists(legacyPath));
    }

    [Fact]
    public void DeniedWorkspaceReplacementPreservesTheTargetAndCleansTheTemporary()
    {
        using FixtureVault local = FixtureVault.Create(1, "workspace-anchor-denied");
        var store = new WorkspacePersistence(local.Root);
        store.Save(Snapshot("before"));
        string directory = Path.Combine(local.Root, ".slate");
        string workspacePath = Path.Combine(directory, "workspace.json");
        byte[] original = File.ReadAllBytes(workspacePath);
        using var held = new FileStream(
            workspacePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        Assert.Throws<IOException>(() => store.Save(Snapshot("after")));

        Assert.Equal(original, File.ReadAllBytes(workspacePath));
        Assert.Empty(Directory.EnumerateFiles(directory, "workspace.json.tmp-*"));
    }

    [Fact]
    public void OversizedWorkspaceSerializationIsBoundedAndPreservesTheTarget()
    {
        using FixtureVault local = FixtureVault.Create(1, "workspace-anchor-oversized");
        var store = new WorkspacePersistence(local.Root);
        store.Save(Snapshot("before"));
        string directory = Path.Combine(local.Root, ".slate");
        string workspacePath = Path.Combine(directory, "workspace.json");
        byte[] original = File.ReadAllBytes(workspacePath);

        Assert.Throws<InvalidOperationException>(() => store.Save(
            Snapshot("after", new string('x', WorkspacePersistence.MaxFileBytes))));

        Assert.Equal(original, File.ReadAllBytes(workspacePath));
        Assert.Empty(Directory.EnumerateFiles(directory, "workspace.json.tmp-*"));
    }

    [Fact]
    public void WorkspaceDirectoryIdentityFailsClosedWhenSwapAttemptsSucceed()
    {
        using FixtureVault external = FixtureVault.Create(1, "workspace-anchor-swap-external");
        using FixtureVault local = FixtureVault.Create(1, "workspace-anchor-swap");
        new WorkspacePersistence(external.Root).Save(Snapshot("external"));
        string externalPath = Path.Combine(external.Root, ".slate", "workspace.json");
        byte[] externalSentinel = File.ReadAllBytes(externalPath);
        new WorkspacePersistence(local.Root).Save(Snapshot("before"));
        string slateDirectory = Path.Combine(local.Root, ".slate");
        string parkedDirectory = Path.Combine(local.Root, ".slate-parked");
        string attackPath = Path.Combine(slateDirectory, "workspace.json");
        int attempts = 0;
        void AttemptSwap()
        {
            Interlocked.Increment(ref attempts);
            try
            {
                Directory.Move(slateDirectory, parkedDirectory);
                Directory.CreateDirectory(slateDirectory);
                File.Copy(externalPath, attackPath);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        var guarded = new WorkspacePersistence(local.Root, AttemptSwap);
        WorkspaceSnapshot? loaded = guarded.Load();
        if (Directory.Exists(parkedDirectory))
        {
            Assert.Null(loaded);
            Assert.Equal(externalSentinel, File.ReadAllBytes(attackPath));
            RestoreOriginalDirectory();
        }
        else
        {
            Assert.Equal("before", Assert.IsType<WorkspaceSnapshot>(loaded).ActiveLeaf);
        }

        Exception? saveFailure = Record.Exception(() => guarded.Save(Snapshot("after")));
        if (Directory.Exists(parkedDirectory))
        {
            Assert.IsType<IOException>(saveFailure);
            Assert.Equal(externalSentinel, File.ReadAllBytes(attackPath));
            RestoreOriginalDirectory();
            new WorkspacePersistence(local.Root).Save(Snapshot("after"));
        }
        else
        {
            Assert.Null(saveFailure);
        }

        Assert.Equal(2, attempts);
        Assert.False(Directory.Exists(parkedDirectory));
        Assert.Equal(
            "after",
            Assert.IsType<WorkspaceSnapshot>(
                new WorkspacePersistence(local.Root).Load()).ActiveLeaf);

        void RestoreOriginalDirectory()
        {
            Directory.Delete(slateDirectory, recursive: true);
            Directory.Move(parkedDirectory, slateDirectory);
        }
    }

    private static WorkspaceSnapshot Snapshot(
        string activeLeaf,
        string path = "note0.md")
    {
        Guid groupId = Guid.NewGuid();
        Guid tabId = Guid.NewGuid();
        var tab = new WorkspaceTabState(
            tabId,
            new WorkspaceItemState(WorkspaceItemKind.Markdown, path));
        return new WorkspaceSnapshot(
            WorkspacePersistence.SchemaVersion,
            groupId,
            new WorkspaceGroupState(groupId, tabId, [tab]),
            activeLeaf,
            []);
    }
}
