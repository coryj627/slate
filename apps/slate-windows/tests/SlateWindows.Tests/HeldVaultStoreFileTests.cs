// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using SlateWindows.Search;

namespace SlateWindows.Tests;

/// <summary>
/// #1869: another process that briefly holds a vault store's file (an
/// indexer, antivirus, a sync client) must not lose the save or make the
/// store unreadable. A reader without delete sharing makes the save's rename
/// fail with access denied (5); one that shares nothing makes the read fail
/// with a sharing violation (32). Each store tries again for a short while;
/// its <c>HeldAttemptForTests</c> seam runs at each held attempt, so a fact
/// lets the file go there instead of racing the retries' timing.
/// </summary>
public sealed class HeldVaultStoreFileTests
{
    [Theory]
    [InlineData("workspace")]
    [InlineData("search-recents")]
    [InlineData("sidebar")]
    public void ASaveLandsWhenTheProcessHoldingTheFileLetsGoInTime(string kind)
    {
        using FixtureVault fixture = FixtureVault.Create(0, $"held-lands-{kind}");
        IHeldStore store = StoreOf(kind, fixture.Root);
        store.Save("before");
        var held = new List<int>();
        using (FileStream hold = Hold(store.FilePath, FileShare.Read))
        {
            store.HeldAttempt = attempt =>
            {
                held.Add(attempt);
                hold.Dispose();
            };

            Assert.Null(store.Save("after"));
        }

        Assert.Equal([0], held);
        Assert.Equal("after", StoreOf(kind, fixture.Root).Read());
    }

    [Theory]
    [InlineData("workspace")]
    [InlineData("search-recents")]
    [InlineData("sidebar")]
    public void ASaveThatStaysHeldFailsAfterAShortWaitAndLeavesTheFile(string kind)
    {
        using FixtureVault fixture = FixtureVault.Create(0, $"held-stays-{kind}");
        IHeldStore store = StoreOf(kind, fixture.Root);
        store.Save("before");
        byte[] before = File.ReadAllBytes(store.FilePath);
        var held = new List<int>();
        store.HeldAttempt = held.Add;
        var clock = Stopwatch.StartNew();
        Exception? failure;
        using (Hold(store.FilePath, FileShare.Read))
        {
            failure = store.Save("after");
        }

        clock.Stop();
        Assert.IsAssignableFrom<IOException>(failure);
        Assert.Equal(Enumerable.Range(0, AnchoredVaultStore.BriefHeldFileBackoff.Count), held);
        // The UI thread waits at most the brief budget, plus the attempts.
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"a held save took {clock.Elapsed}");
        Assert.Equal(before, File.ReadAllBytes(store.FilePath));
    }

    /// <summary>A process that shares nothing fails the read itself. The
    /// sidebar used to call that unreadable and turn its settings read-only
    /// for the session; search recents used to read it as an empty list and
    /// replace every remembered query with the new one.</summary>
    [Theory]
    [InlineData("search-recents")]
    [InlineData("sidebar")]
    public void AReadThatMeetsANoSharingHoldWaitsForItInsteadOfLosingTheFile(string kind)
    {
        using FixtureVault fixture = FixtureVault.Create(0, $"held-read-{kind}");
        IHeldStore store = StoreOf(kind, fixture.Root);
        store.Save("before");
        var held = new List<int>();
        using (FileStream hold = Hold(store.FilePath, FileShare.None))
        {
            store.HeldAttempt = attempt =>
            {
                held.Add(attempt);
                hold.Dispose();
            };

            Assert.Null(store.Save("after"));
        }

        Assert.Equal([0], held);
        Assert.Equal("after", StoreOf(kind, fixture.Root).Read());
        Assert.Null(store.ReadOnlyReason);
        if (kind == "search-recents")
        {
            Assert.Equal(["after", "before"], new SearchRecentsStore(fixture.Root).Load());
        }
    }

    /// <summary>A no-sharing hold that outlasts the read's wait leaves the
    /// list unknown, and a hold that ends just after it would otherwise let
    /// the save replace every remembered query with the new one.</summary>
    [Fact]
    public void ARecentSearchIsNotSavedOverAListThatCouldNotBeRead()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "held-recents-unread");
        var store = new SearchRecentsStore(fixture.Root);
        _ = store.Add("one");
        _ = store.Add("two");
        string path = Path.Combine(fixture.Root, ".slate", "search-recents.json");
        byte[] before = File.ReadAllBytes(path);
        FileStream hold = Hold(path, FileShare.None);
        try
        {
            store.BeforeSaveForTests = hold.Dispose;

            IReadOnlyList<string> returned = store.Add("three");

            Assert.Equal(["three"], returned);
            Assert.IsAssignableFrom<IOException>(store.LastSaveError);
        }
        finally
        {
            hold.Dispose();
        }

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(["two", "one"], new SearchRecentsStore(fixture.Root).Load());
    }

    [Fact]
    public void TheSidebarsOpeningReadWaitsForANoSharingHoldInsteadOfGoingReadOnly()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "held-sidebar-load");
        var writer = new SidebarSettingsStore(fixture.Root);
        writer.SetOrganization(SidebarSortMode.CreatedNewest, groupByDate: true);
        string path = Path.Combine(fixture.Root, ".slate", "sidebar.json");
        var store = new SidebarSettingsStore(fixture.Root);
        var held = new List<int>();
        SidebarSettingsSnapshot loaded;
        using (FileStream hold = Hold(path, FileShare.None))
        {
            store.HeldAttemptForTests = attempt =>
            {
                held.Add(attempt);
                hold.Dispose();
            };

            loaded = store.Load();
        }

        Assert.Equal([0], held);
        Assert.Null(loaded.ReadOnlyReason);
        Assert.Equal(SidebarSortMode.CreatedNewest, loaded.SortMode);
    }

    /// <summary>A no-sharing hold that outlasts the wait fails that one
    /// update, as any write failure does, but the file isn't unreadable: the
    /// store stays writable, and the next update after the hold lands (Codex
    /// on #1869: it used to latch read-only until Retry).</summary>
    [Fact]
    public void ASidebarUpdateThatStaysHeldFailsWithoutMakingTheSettingsReadOnly()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "held-sidebar-update");
        var store = new SidebarHeldStore(fixture.Root);
        Assert.Null(store.Save("before"));
        Exception? failure;
        using (Hold(store.FilePath, FileShare.None))
        {
            failure = store.Save("during");
        }

        Assert.IsAssignableFrom<IOException>(failure);
        Assert.Null(store.ReadOnlyReason);
        Assert.Null(store.Save("after"));
        Assert.Equal("after", store.Read());
    }

    /// <summary>The opening read fails closed while the file stays held:
    /// a session that couldn't read the pins must not write over them, so
    /// the settings are read-only until they're read again (the sidebar's
    /// Retry), which then succeeds.</summary>
    [Fact]
    public void TheSidebarsOpeningReadFailsClosedWhileTheFileStaysHeldAndRecoversOnTheNextRead()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "held-sidebar-load-stays");
        new SidebarSettingsStore(fixture.Root).SetOrganization(SidebarSortMode.CreatedNewest, groupByDate: true);
        string path = Path.Combine(fixture.Root, ".slate", "sidebar.json");
        var store = new SidebarSettingsStore(fixture.Root);
        SidebarSettingsSnapshot held;
        using (Hold(path, FileShare.None))
        {
            held = store.Load();
        }

        Assert.NotNull(held.ReadOnlyReason);
        SidebarSettingsSnapshot again = store.Load();
        Assert.Null(again.ReadOnlyReason);
        Assert.Equal(SidebarSortMode.CreatedNewest, again.SortMode);
    }

    /// <summary>One budget per operation (Codex on #1869): an update whose
    /// lock and write both meet a hold waits the brief backoff once in all,
    /// not once per step.</summary>
    [Fact]
    public void AnUpdateWhoseStepsEachMeetAHoldWaitsOneBudgetInAll()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "held-sidebar-budget");
        var store = new SidebarSettingsStore(fixture.Root);
        store.SetOrganization(SidebarSortMode.CreatedNewest, groupByDate: true);
        string directory = Path.Combine(fixture.Root, ".slate");
        // Another Slate holds the lock for the first two waits; a reader
        // holds the settings without delete sharing throughout.
        FileStream lockHold = Hold(Path.Combine(directory, "sidebar.json.lock"), FileShare.None);
        using FileStream fileHold = Hold(Path.Combine(directory, "sidebar.json"), FileShare.Read);
        var held = new List<int>();
        store.HeldAttemptForTests = wait =>
        {
            held.Add(wait);
            if (wait == 1)
            {
                lockHold.Dispose();
            }
        };
        try
        {
            Assert.Throws<IOException>(() => store.SetOrganization(SidebarSortMode.NameDescending, groupByDate: false));
        }
        finally
        {
            lockHold.Dispose();
        }

        Assert.Equal(Enumerable.Range(0, AnchoredVaultStore.BriefHeldFileBackoff.Count), held);
    }

    private static FileStream Hold(string path, FileShare share) =>
        new(path, FileMode.Open, FileAccess.Read, share);

    private static IHeldStore StoreOf(string kind, string root) => kind switch
    {
        "workspace" => new WorkspaceStore(root),
        "search-recents" => new SearchRecentsHeldStore(root),
        "sidebar" => new SidebarHeldStore(root),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>One store under test: a save of a marker value that answers
    /// its failure, the marker read back, and the store's held seam.</summary>
    private interface IHeldStore
    {
        string FilePath { get; }

        Action<int>? HeldAttempt { set; }

        string? ReadOnlyReason { get; }

        Exception? Save(string marker);

        string? Read();
    }

    private sealed class WorkspaceStore(string root) : IHeldStore
    {
        private readonly WorkspacePersistence _store = new(root);

        public string FilePath { get; } = Path.Combine(root, ".slate", "workspace.json");

        public Action<int>? HeldAttempt { set => _store.HeldAttemptForTests = value; }

        public string? ReadOnlyReason => null;

        public Exception? Save(string marker) => Capture(() => _store.Save(Snapshot(marker)));

        public string? Read() => _store.Load()?.ActiveLeaf;

        private static WorkspaceSnapshot Snapshot(string activeLeaf)
        {
            Guid groupId = Guid.NewGuid();
            Guid tabId = Guid.NewGuid();
            var tab = new WorkspaceTabState(tabId, new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md"));
            return new WorkspaceSnapshot(
                WorkspacePersistence.SchemaVersion,
                groupId,
                new WorkspaceGroupState(groupId, tabId, [tab]),
                activeLeaf,
                []);
        }
    }

    private sealed class SearchRecentsHeldStore(string root) : IHeldStore
    {
        private readonly SearchRecentsStore _store = new(root);

        public string FilePath { get; } = Path.Combine(root, ".slate", "search-recents.json");

        public Action<int>? HeldAttempt { set => _store.HeldAttemptForTests = value; }

        public string? ReadOnlyReason => null;

        public Exception? Save(string marker)
        {
            _ = _store.Add(marker);
            return _store.LastSaveError;
        }

        public string? Read() => _store.Load().FirstOrDefault();
    }

    private sealed class SidebarHeldStore(string root) : IHeldStore
    {
        private readonly SidebarSettingsStore _store = new(root);

        public string FilePath { get; } = Path.Combine(root, ".slate", "sidebar.json");

        public Action<int>? HeldAttempt { set => _store.HeldAttemptForTests = value; }

        public string? ReadOnlyReason => _store.ReadOnlyReason;

        // The marker is a shortcut path, which survives a read back.
        public Exception? Save(string marker) =>
            Capture(() => _store.SetShortcuts([new SidebarShortcutState("file", $"{marker}.md")]));

        public string? Read() =>
            new SidebarSettingsStore(root).Load().Shortcuts.SingleOrDefault()?.Path is string path
                ? Path.GetFileNameWithoutExtension(path)
                : null;
    }

    private static Exception? Capture(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}
