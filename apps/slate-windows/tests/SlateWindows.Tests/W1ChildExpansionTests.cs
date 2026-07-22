// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using SlateWindows;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class W1ChildExpansionTests
{
    [Fact]
    [Trait("census", "moderate")]
    public async Task OrdinaryExpansionReturnsBeforeBlockedProviderAndPublishesOneBoundedCollection()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "child-expansion-budget");
        string folderPath = Path.Combine(fixture.Root, "folder");
        Directory.CreateDirectory(folderPath);
        for (int index = 0; index < FilesSidebarViewModel.MaxMaterializedDirectoryItems + 1; index++)
        {
            File.WriteAllText(Path.Combine(folderPath, $"note-{index:D5}.md"), string.Empty);
        }

        using VaultSession session = OpenScanned(fixture.Root);
        var context = new PumpSynchronizationContext();
        using var childStarted = new ManualResetEventSlim();
        using var releaseChild = new ManualResetEventSlim();
        int workerInvocation = 0;
        int providerThread = 0;
        int uiThread = Environment.CurrentManagedThreadId;
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            context,
            (work, cancellationToken) => Task.Run(() =>
            {
                int invocation = Interlocked.Increment(ref workerInvocation);
                if (invocation == 2)
                {
                    providerThread = Environment.CurrentManagedThreadId;
                    childStarted.Set();
                    releaseChild.Wait();
                }

                work();
            }, cancellationToken));
        await DrainUntilComplete(sidebar.TreeRefreshCompletion, context);
        FileTreeNodeViewModel folder = Assert.Single(sidebar.RootNodes, node => node.Path == "folder");
        var originalChildren = folder.Children;
        int collectionMutations = 0;
        int childrenReplacements = 0;
        int publicationThread = 0;
        originalChildren.CollectionChanged += (_, _) => collectionMutations++;
        folder.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FileTreeNodeViewModel.Children))
            {
                childrenReplacements++;
                publicationThread = Environment.CurrentManagedThreadId;
            }
        };

        folder.IsExpanded = true;
        Task completion = sidebar.ChildExpansionCompletion;

        Assert.True(childStarted.Wait(TimeSpan.FromSeconds(5)));
        Assert.NotEqual(uiThread, providerThread);
        Assert.True(folder.IsExpanded);
        Assert.Equal(FileTreeChildLoadState.Loading, folder.ChildLoadState);
        Assert.Single(folder.Children, node => node.IsPlaceholder && node.Name == "Loading…");
        Assert.Equal(0, childrenReplacements);
        Assert.Equal(0, collectionMutations);

        releaseChild.Set();
        await DrainUntilComplete(completion, context, TimeSpan.FromSeconds(15));

        Assert.Equal(FileTreeChildLoadState.Loaded, folder.ChildLoadState);
        Assert.Equal(
            FilesSidebarViewModel.MaxMaterializedDirectoryItems,
            folder.Children.Count(node => !node.IsPlaceholder));
        Assert.Single(folder.Children, node => node.IsPlaceholder);
        Assert.Equal(1, childrenReplacements);
        Assert.Equal(uiThread, publicationThread);
        Assert.Equal(0, collectionMutations);
        Assert.Equal(2, Volatile.Read(ref workerInvocation));
        Assert.Contains("Showing the first 5,000 items", sidebar.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CollapseAndReExpandAllowOnlyTheNewestRequestToPublish()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "child-expansion-supersession");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "folder"));
        File.WriteAllText(Path.Combine(fixture.Root, "folder", "child.md"), string.Empty);
        using VaultSession session = OpenScanned(fixture.Root);
        var context = new PumpSynchronizationContext();
        using var firstStarted = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var secondStarted = new ManualResetEventSlim();
        int workerInvocation = 0;
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            context,
            (work, cancellationToken) => Task.Run(() =>
            {
                int invocation = Interlocked.Increment(ref workerInvocation);
                if (invocation == 2)
                {
                    firstStarted.Set();
                    releaseFirst.Wait();
                }
                else if (invocation == 3)
                {
                    secondStarted.Set();
                }

                work();
            }, cancellationToken));
        await DrainUntilComplete(sidebar.TreeRefreshCompletion, context);
        FileTreeNodeViewModel folder = Assert.Single(sidebar.RootNodes, node => node.Path == "folder");
        int replacements = 0;
        folder.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FileTreeNodeViewModel.Children))
            {
                replacements++;
            }
        };

        folder.IsExpanded = true;
        Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(5)));
        folder.IsExpanded = false;
        Assert.Equal(FileTreeChildLoadState.Unloaded, folder.ChildLoadState);
        folder.IsExpanded = true;
        Task completion = sidebar.ChildExpansionCompletion;
        Assert.Equal(FileTreeChildLoadState.Loading, folder.ChildLoadState);

        releaseFirst.Set();
        Assert.True(secondStarted.Wait(TimeSpan.FromSeconds(5)));
        await DrainUntilComplete(completion, context);

        Assert.Equal(3, Volatile.Read(ref workerInvocation));
        Assert.Equal(1, replacements);
        Assert.Equal(FileTreeChildLoadState.Loaded, folder.ChildLoadState);
        Assert.Single(folder.Children, node => node.Path == "folder/child.md");
    }

    [Fact]
    public async Task SharedProviderLaneSerializesSiblingExpansionAndSkipsCanceledQueuedWork()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "child-expansion-lane");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "first"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "second"));
        File.WriteAllText(Path.Combine(fixture.Root, "first", "one.md"), string.Empty);
        File.WriteAllText(Path.Combine(fixture.Root, "second", "two.md"), string.Empty);
        using VaultSession session = OpenScanned(fixture.Root);
        var context = new PumpSynchronizationContext();
        using var firstStarted = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var secondStarted = new ManualResetEventSlim();
        int workerInvocations = 0;
        int activeProviders = 0;
        int maximumProviders = 0;
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            context,
            (work, _) => Task.Run(() =>
            {
                int invocation = Interlocked.Increment(ref workerInvocations);
                int active = Interlocked.Increment(ref activeProviders);
                UpdateMaximum(ref maximumProviders, active);
                try
                {
                    if (invocation == 2)
                    {
                        firstStarted.Set();
                        releaseFirst.Wait();
                    }
                    else if (invocation == 3)
                    {
                        secondStarted.Set();
                    }

                    work();
                }
                finally
                {
                    Interlocked.Decrement(ref activeProviders);
                }
            }));
        await DrainUntilComplete(sidebar.TreeRefreshCompletion, context);
        FileTreeNodeViewModel first = Assert.Single(
            sidebar.RootNodes,
            node => node.Path == "first");
        FileTreeNodeViewModel second = Assert.Single(
            sidebar.RootNodes,
            node => node.Path == "second");

        first.IsExpanded = true;
        Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(5)));
        second.IsExpanded = true;
        Task completion = sidebar.ChildExpansionCompletion;

        Assert.Equal(1, Volatile.Read(ref maximumProviders));
        second.IsExpanded = false;
        releaseFirst.Set();
        await DrainUntilComplete(completion, context);

        Assert.Equal(2, Volatile.Read(ref workerInvocations));
        Assert.Equal(1, Volatile.Read(ref maximumProviders));
        Assert.False(secondStarted.IsSet);
        Assert.Single(first.Children, node => node.Path == "first/one.md");
        Assert.Equal(FileTreeChildLoadState.Unloaded, second.ChildLoadState);
        Assert.Single(second.Children, node => node.IsPlaceholder && node.Name == "Loading…");
    }

    [Fact]
    public async Task RefreshRejectsPublicationIntoTheDetachedExpandedNode()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "child-expansion-refresh");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "folder"));
        File.WriteAllText(Path.Combine(fixture.Root, "folder", "child.md"), string.Empty);
        using VaultSession session = OpenScanned(fixture.Root);
        var context = new PumpSynchronizationContext();
        using var childStarted = new ManualResetEventSlim();
        using var releaseChild = new ManualResetEventSlim();
        int workerInvocation = 0;
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            context,
            (work, cancellationToken) => Task.Run(() =>
            {
                if (Interlocked.Increment(ref workerInvocation) == 2)
                {
                    childStarted.Set();
                    releaseChild.Wait();
                }

                work();
            }, cancellationToken));
        await DrainUntilComplete(sidebar.TreeRefreshCompletion, context);
        FileTreeNodeViewModel detached = Assert.Single(sidebar.RootNodes, node => node.Path == "folder");

        detached.IsExpanded = true;
        Task childCompletion = sidebar.ChildExpansionCompletion;
        Assert.True(childStarted.Wait(TimeSpan.FromSeconds(5)));
        sidebar.Refresh();
        Task refreshCompletion = sidebar.TreeRefreshCompletion;
        releaseChild.Set();
        await DrainUntilComplete(Task.WhenAll(childCompletion, refreshCompletion), context);

        Assert.Equal(FileTreeChildLoadState.Failed, detached.ChildLoadState);
        Assert.Single(
            detached.Children,
            node => node.IsPlaceholder
                && node.Name == "Folder loading was canceled. Collapse and expand to retry.");
        FileTreeNodeViewModel current = Assert.Single(sidebar.RootNodes, node => node.Path == "folder");
        Assert.NotSame(detached, current);
        Assert.True(current.IsExpanded);
        Assert.Single(current.Children, node => node.Path == "folder/child.md");
    }

    [Fact]
    public async Task RetainedNodeExpandedAfterRefreshNeverEntersTheProviderLane()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "child-expansion-detached");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "folder"));
        File.WriteAllText(Path.Combine(fixture.Root, "folder", "child.md"), string.Empty);
        using VaultSession session = OpenScanned(fixture.Root);
        var context = new PumpSynchronizationContext();
        int workerInvocations = 0;
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            context,
            (work, cancellationToken) =>
            {
                Interlocked.Increment(ref workerInvocations);
                return Task.Run(work, cancellationToken);
            });
        await DrainUntilComplete(sidebar.TreeRefreshCompletion, context);
        FileTreeNodeViewModel detached = Assert.Single(
            sidebar.RootNodes,
            node => node.Path == "folder");

        sidebar.Refresh();
        await DrainUntilComplete(sidebar.TreeRefreshCompletion, context);
        Assert.NotSame(
            detached,
            Assert.Single(sidebar.RootNodes, node => node.Path == "folder"));

        detached.IsExpanded = true;

        Assert.True(sidebar.ChildExpansionCompletion.IsCompleted);
        Assert.Equal(2, Volatile.Read(ref workerInvocations));
        Assert.Equal(FileTreeChildLoadState.Unloaded, detached.ChildLoadState);
        Assert.Single(
            detached.Children,
            node => node.IsPlaceholder && node.Name == "Loading…");
    }

    [Fact]
    public async Task ExpansionStartedDuringRefreshWaitsAndRejectsTheReplacedNodeBeforeProviderWork()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "child-expansion-refresh-precedence");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "folder"));
        File.WriteAllText(Path.Combine(fixture.Root, "folder", "child.md"), string.Empty);
        using VaultSession session = OpenScanned(fixture.Root);
        var context = new PumpSynchronizationContext();
        using var refreshStarted = new ManualResetEventSlim();
        using var releaseRefresh = new ManualResetEventSlim();
        int workerInvocations = 0;
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            context,
            (work, cancellationToken) => Task.Run(() =>
            {
                if (Interlocked.Increment(ref workerInvocations) == 2)
                {
                    refreshStarted.Set();
                    releaseRefresh.Wait();
                }

                work();
            }, cancellationToken));
        await DrainUntilComplete(sidebar.TreeRefreshCompletion, context);
        FileTreeNodeViewModel detached = Assert.Single(
            sidebar.RootNodes,
            node => node.Path == "folder");

        sidebar.Refresh();
        Task refresh = sidebar.TreeRefreshCompletion;
        Assert.True(refreshStarted.Wait(TimeSpan.FromSeconds(5)));
        detached.IsExpanded = true;
        Task expansion = sidebar.ChildExpansionCompletion;

        Assert.Equal(2, Volatile.Read(ref workerInvocations));
        releaseRefresh.Set();
        await DrainUntilComplete(Task.WhenAll(refresh, expansion), context);

        Assert.Equal(2, Volatile.Read(ref workerInvocations));
        Assert.NotSame(
            detached,
            Assert.Single(sidebar.RootNodes, node => node.Path == "folder"));
    }

    [Fact]
    public async Task BulkExpansionStartedDuringRefreshUsesOnlyThePublishedReplacementTree()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "bulk-expansion-refresh-precedence");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "folder"));
        File.WriteAllText(Path.Combine(fixture.Root, "folder", "child.md"), string.Empty);
        using VaultSession session = OpenScanned(fixture.Root);
        var context = new PumpSynchronizationContext();
        using var refreshStarted = new ManualResetEventSlim();
        using var releaseRefresh = new ManualResetEventSlim();
        int workerInvocations = 0;
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            context,
            (work, cancellationToken) => Task.Run(() =>
            {
                if (Interlocked.Increment(ref workerInvocations) == 2)
                {
                    refreshStarted.Set();
                    releaseRefresh.Wait();
                }

                work();
            }, cancellationToken));
        await DrainUntilComplete(sidebar.TreeRefreshCompletion, context);
        FileTreeNodeViewModel detached = Assert.Single(
            sidebar.RootNodes,
            node => node.Path == "folder");

        sidebar.Refresh();
        Task refresh = sidebar.TreeRefreshCompletion;
        Assert.True(refreshStarted.Wait(TimeSpan.FromSeconds(5)));
        sidebar.ExpandLoadedCommand.Execute(null);
        Task expansion = sidebar.ExpandLoadedCompletion;

        Assert.Equal(2, Volatile.Read(ref workerInvocations));
        releaseRefresh.Set();
        await DrainUntilComplete(Task.WhenAll(refresh, expansion), context);

        FileTreeNodeViewModel current = Assert.Single(
            sidebar.RootNodes,
            node => node.Path == "folder");
        Assert.NotSame(detached, current);
        Assert.False(detached.IsExpanded);
        Assert.True(current.IsExpanded);
        Assert.Single(current.Children, node => node.Path == "folder/child.md");
        Assert.Equal(3, Volatile.Read(ref workerInvocations));
    }

    [Fact]
    public async Task CloseDuringRefreshCancelsDeferredOrdinaryExpansion()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "close-refresh-child-cascade");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "folder"));
        File.WriteAllText(Path.Combine(fixture.Root, "folder", "child.md"), string.Empty);
        var context = new PumpSynchronizationContext();
        using var refreshStarted = new ManualResetEventSlim();
        using var releaseRefresh = new ManualResetEventSlim();
        int workerInvocations = 0;
        using var lifecycle = CreateLifecycle(
            fixture.Root,
            context,
            (work, cancellationToken) => Task.Run(() =>
            {
                if (Interlocked.Increment(ref workerInvocations) == 2)
                {
                    refreshStarted.Set();
                    releaseRefresh.Wait();
                }

                work();
            }, cancellationToken));
        await lifecycle.OpenVaultAsync(fixture.Root);
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(
            lifecycle.FileSidebar);
        await DrainUntilComplete(sidebar.TreeRefreshCompletion, context);
        FileTreeNodeViewModel folder = Assert.Single(
            sidebar.RootNodes,
            node => node.Path == "folder");

        sidebar.Refresh();
        Task refresh = sidebar.TreeRefreshCompletion;
        Assert.True(refreshStarted.Wait(TimeSpan.FromSeconds(5)));
        folder.IsExpanded = true;
        Task expansion = sidebar.ChildExpansionCompletion;

        Assert.False(lifecycle.PrepareForApplicationClose());
        releaseRefresh.Set();
        await DrainUntilComplete(Task.WhenAll(refresh, expansion), context);

        Assert.Equal(2, Volatile.Read(ref workerInvocations));
        Assert.Equal(FileTreeChildLoadState.Failed, folder.ChildLoadState);
        Assert.Single(
            folder.Children,
            node => node.IsPlaceholder
                && node.Name == "Folder loading was canceled. Collapse and expand to retry.");
        Assert.True(lifecycle.PrepareForApplicationClose());
    }

    [Fact]
    public async Task CloseDuringRefreshCancelsDeferredBulkExpansion()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "close-refresh-bulk-cascade");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "folder"));
        File.WriteAllText(Path.Combine(fixture.Root, "folder", "child.md"), string.Empty);
        var context = new PumpSynchronizationContext();
        using var refreshStarted = new ManualResetEventSlim();
        using var releaseRefresh = new ManualResetEventSlim();
        int workerInvocations = 0;
        using var lifecycle = CreateLifecycle(
            fixture.Root,
            context,
            (work, cancellationToken) => Task.Run(() =>
            {
                if (Interlocked.Increment(ref workerInvocations) == 2)
                {
                    refreshStarted.Set();
                    releaseRefresh.Wait();
                }

                work();
            }, cancellationToken));
        await lifecycle.OpenVaultAsync(fixture.Root);
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(
            lifecycle.FileSidebar);
        await DrainUntilComplete(sidebar.TreeRefreshCompletion, context);
        FileTreeNodeViewModel folder = Assert.Single(
            sidebar.RootNodes,
            node => node.Path == "folder");

        sidebar.Refresh();
        Task refresh = sidebar.TreeRefreshCompletion;
        Assert.True(refreshStarted.Wait(TimeSpan.FromSeconds(5)));
        sidebar.ExpandLoadedCommand.Execute(null);
        Task expansion = sidebar.ExpandLoadedCompletion;

        Assert.False(lifecycle.PrepareForApplicationClose());
        releaseRefresh.Set();
        await DrainUntilComplete(Task.WhenAll(refresh, expansion), context);

        Assert.Equal(2, Volatile.Read(ref workerInvocations));
        Assert.False(folder.IsExpanded);
        Assert.False(sidebar.IsExpandingLoaded);
        Assert.True(lifecycle.PrepareForApplicationClose());
    }

    [Fact]
    public async Task ExpansionFailureIsGenericAccessibleAndRetryable()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "child-expansion-failure");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "folder"));
        File.WriteAllText(Path.Combine(fixture.Root, "folder", "child.md"), string.Empty);
        using VaultSession session = OpenScanned(fixture.Root);
        var context = new PumpSynchronizationContext();
        var announcements = new List<A11yEvent>();
        int workerInvocation = 0;
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            context,
            (work, cancellationToken) =>
            {
                int invocation = Interlocked.Increment(ref workerInvocation);
                if (invocation == 2)
                {
                    return Task.FromException(
                        new ApplicationException(@"C:\Private\medical.md authored detail"));
                }

                return Task.Run(work, cancellationToken);
            },
            announcements.Add);
        await DrainUntilComplete(sidebar.TreeRefreshCompletion, context);
        announcements.Clear();
        FileTreeNodeViewModel folder = Assert.Single(sidebar.RootNodes, node => node.Path == "folder");

        folder.IsExpanded = true;
        await DrainUntilComplete(sidebar.ChildExpansionCompletion, context);

        const string expected = "Could not load folder. Collapse and expand to retry.";
        Assert.Equal(FileTreeChildLoadState.Failed, folder.ChildLoadState);
        Assert.Single(folder.Children, node => node.IsPlaceholder && node.Name == expected);
        Assert.Equal(expected, sidebar.Status);
        A11yEvent.HostComposed failure = Assert.Single(
            announcements.OfType<A11yEvent.HostComposed>());
        Assert.Equal(expected, failure.Text);
        Assert.DoesNotContain("Private", failure.Text, StringComparison.Ordinal);

        folder.IsExpanded = false;
        folder.IsExpanded = true;
        await DrainUntilComplete(sidebar.ChildExpansionCompletion, context);

        Assert.Equal(FileTreeChildLoadState.Loaded, folder.ChildLoadState);
        Assert.Single(folder.Children, node => node.Path == "folder/child.md");
    }

    [Fact]
    public async Task OrdinaryExpansionRestoresPersistedDescendantsOnTheWorker()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "child-expansion-restored");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "A", "B", "C"));
        File.WriteAllText(Path.Combine(fixture.Root, "A", "B", "C", "note.md"), string.Empty);
        using VaultSession session = OpenScanned(fixture.Root);
        var context = new PumpSynchronizationContext();
        int uiThread = Environment.CurrentManagedThreadId;
        int childWorkerThread = 0;
        int workerInvocation = 0;
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            context,
            (work, cancellationToken) => Task.Run(() =>
            {
                if (Interlocked.Increment(ref workerInvocation) == 2)
                {
                    childWorkerThread = Environment.CurrentManagedThreadId;
                }

                work();
            }, cancellationToken),
            restoredExpandedPaths: ["A/B"]);
        await DrainUntilComplete(sidebar.TreeRefreshCompletion, context);
        FileTreeNodeViewModel a = Assert.Single(sidebar.RootNodes, node => node.Path == "A");

        a.IsExpanded = true;
        await DrainUntilComplete(sidebar.ChildExpansionCompletion, context);

        Assert.NotEqual(uiThread, childWorkerThread);
        FileTreeNodeViewModel b = Assert.Single(a.Children, node => node.Path == "A/B");
        Assert.True(b.IsExpanded);
        Assert.Single(b.Children, node => node.Path == "A/B/C");
    }

    [Fact]
    public async Task InteractiveCloseCancelsOrdinaryExpansionAndDefersUntilItDrains()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "child-expansion-close");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "folder"));
        File.WriteAllText(Path.Combine(fixture.Root, "folder", "child.md"), string.Empty);
        var context = new PumpSynchronizationContext();
        using var childStarted = new ManualResetEventSlim();
        using var releaseChild = new ManualResetEventSlim();
        int workerInvocation = 0;
        using var lifecycle = CreateLifecycle(
            fixture.Root,
            context,
            (work, cancellationToken) => Task.Run(() =>
            {
                if (Interlocked.Increment(ref workerInvocation) == 2)
                {
                    childStarted.Set();
                    releaseChild.Wait();
                }

                work();
            }, cancellationToken));
        await lifecycle.OpenVaultAsync(fixture.Root);
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
        await DrainUntilComplete(sidebar.TreeRefreshCompletion, context);
        FileTreeNodeViewModel folder = Assert.Single(sidebar.RootNodes, node => node.Path == "folder");

        folder.IsExpanded = true;
        Task completion = sidebar.ChildExpansionCompletion;
        Assert.True(childStarted.Wait(TimeSpan.FromSeconds(5)));
        Assert.False(lifecycle.PrepareForApplicationClose());

        Assert.False(releaseChild.IsSet);
        Assert.Contains("Folder expansion cancellation requested", lifecycle.StatusText);
        releaseChild.Set();
        await completion.WaitAsync(TimeSpan.FromSeconds(5));
        context.Drain();

        const string canceled = "Folder loading was canceled. Collapse and expand to retry.";
        Assert.Equal(FileTreeChildLoadState.Failed, folder.ChildLoadState);
        Assert.Single(folder.Children, node => node.IsPlaceholder && node.Name == canceled);
        folder.IsExpanded = false;
        folder.IsExpanded = true;
        await DrainUntilComplete(sidebar.ChildExpansionCompletion, context);

        Assert.Equal(FileTreeChildLoadState.Loaded, folder.ChildLoadState);
        Assert.Single(folder.Children, node => node.Path == "folder/child.md");
        Assert.True(lifecycle.PrepareForApplicationClose());
    }

    [Fact]
    public async Task DirectDisposalWaitsForAdmittedChildProviderWithoutUiPumping()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "child-expansion-dispose");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "folder"));
        File.WriteAllText(Path.Combine(fixture.Root, "folder", "child.md"), string.Empty);
        var context = new PumpSynchronizationContext();
        using var childStarted = new ManualResetEventSlim();
        using var releaseChild = new ManualResetEventSlim();
        int workerInvocation = 0;
        var lifecycle = CreateLifecycle(
            fixture.Root,
            context,
            (work, cancellationToken) => Task.Run(() =>
            {
                if (Interlocked.Increment(ref workerInvocation) == 2)
                {
                    childStarted.Set();
                    releaseChild.Wait();
                }

                work();
            }, cancellationToken));
        await lifecycle.OpenVaultAsync(fixture.Root);
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
        await DrainUntilComplete(sidebar.TreeRefreshCompletion, context);
        FileTreeNodeViewModel folder = Assert.Single(sidebar.RootNodes, node => node.Path == "folder");
        folder.IsExpanded = true;
        Assert.True(childStarted.Wait(TimeSpan.FromSeconds(5)));

        Task dispose = Task.Run(lifecycle.Dispose);
        Assert.True(SpinWait.SpinUntil(
            () => sidebar.SessionShutdownStarted,
            TimeSpan.FromSeconds(5)));
        Assert.False(dispose.IsCompleted);
        releaseChild.Set();
        await dispose.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static VaultLifecycleViewModel CreateLifecycle(
        string root,
        SynchronizationContext treeUiContext,
        Func<Action, CancellationToken, Task> treeWorker) => new(
        pickVault: () => Task.FromResult<string?>(root),
        enqueueUi: action => action(),
        recentVaultsStore: new RecentVaultsStore(
            Path.Combine(root, "device-state", "recent-vaults.json")),
        treeUiContext: treeUiContext,
        treeWorker: treeWorker);

    private static FilesSidebarViewModel CreateSidebar(
        VaultSession session,
        string root,
        SynchronizationContext treeUiContext,
        Func<Action, CancellationToken, Task> treeWorker,
        Action<A11yEvent>? announce = null,
        IEnumerable<string>? restoredExpandedPaths = null) => new(
        session,
        announce ?? (_ => { }),
        restoredExpandedPaths: restoredExpandedPaths,
        vaultRoot: root,
        localAppDataRoot: Path.Combine(root, "device-state"),
        treeUiContext: treeUiContext,
        treeWorker: treeWorker);

    private static VaultSession OpenScanned(string root)
    {
        VaultSession session = VaultSession.OpenFilesystem(root);
        using var cancellation = new CancelToken();
        session.ScanInitial(cancellation);
        return session;
    }

    private static async Task DrainUntilComplete(
        Task completion,
        PumpSynchronizationContext context,
        TimeSpan? timeout = null)
    {
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    context.Drain();
                    return completion.IsCompleted;
                },
                timeout ?? TimeSpan.FromSeconds(5)),
            "The queued UI publication did not complete within the test deadline.");
        await completion;
        context.Drain();
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        int observed;
        do
        {
            observed = Volatile.Read(ref maximum);
            if (candidate <= observed)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref maximum, candidate, observed) != observed);
    }

    private sealed class PumpSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = [];

        public override void Post(SendOrPostCallback callback, object? state) =>
            _queue.Enqueue((callback, state));

        public void Drain()
        {
            while (_queue.TryDequeue(out var work))
            {
                work.Callback(work.State);
            }
        }
    }
}
