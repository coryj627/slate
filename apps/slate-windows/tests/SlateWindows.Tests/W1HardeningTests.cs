// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using System.Xml.Linq;
using SlateWindows;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class W1HostLoggingPrivacyTests
{
    [Fact]
    public void DurableDiagnostics_OmitExceptionMessagesAndVaultPaths()
    {
        const string sentinelPath = @"C:\Vaults\Private\medical-notes.md";
        var output = new StringWriter();
        TextWriter original = Console.Error;
        try
        {
            Console.SetError(output);
            foreach (HostDiagnosticEvent diagnosticEvent in Enum.GetValues<HostDiagnosticEvent>())
            {
                HostLog.Write(
                    diagnosticEvent,
                    new IOException($"Could not access {sentinelPath}: authored detail"));
            }

            HostLog.WriteSizeLimit(
                HostDiagnosticEvent.RecentVaultsPayloadRejected,
                new FileSizeLimitExceededException(65_537, 65_536));
        }
        finally
        {
            Console.SetError(original);
        }

        string logged = output.ToString();
        Assert.DoesNotContain(sentinelPath, logged, StringComparison.Ordinal);
        Assert.DoesNotContain("authored detail", logged, StringComparison.Ordinal);
        Assert.Contains(nameof(IOException), logged, StringComparison.Ordinal);
        Assert.Contains("observedBytes=65537", logged, StringComparison.Ordinal);
        Assert.Contains("maximumBytes=65536", logged, StringComparison.Ordinal);
        foreach (HostDiagnosticEvent diagnosticEvent in Enum.GetValues<HostDiagnosticEvent>())
        {
            Assert.Contains($"SlateWindows.{diagnosticEvent}", logged, StringComparison.Ordinal);
        }
    }
}

public sealed class W1ReleaseEvidenceContractTests
{
    [Fact]
    public void TestProjectGraphDeclaresHostLogProbeBuildDependency()
    {
        XDocument project = XDocument.Load(Path.Combine(
            RepoRoot(),
            "apps",
            "slate-windows",
            "tests",
            "SlateWindows.Tests",
            "SlateWindows.Tests.csproj"));
        XNamespace xmlNamespace = project.Root?.Name.Namespace ?? XNamespace.None;
        XElement reference = Assert.Single(
            project.Descendants(xmlNamespace + "ProjectReference"),
            element => string.Equals(
                ((string?)element.Attribute("Include"))?.Replace('\\', '/'),
                "../../tools/HostLogProbe/HostLogProbe.csproj",
                StringComparison.Ordinal));

        Assert.Equal(
            "false",
            ((string?)reference.Attribute("ReferenceOutputAssembly"))?.ToLowerInvariant());
    }

    [Fact]
    public void WindowsWorkflowRetainsAccessibilityEvidenceAfterGateFailure()
    {
        string workflow = File.ReadAllText(Path.Combine(
            RepoRoot(),
            ".github",
            "workflows",
            "windows.yml"));

        Assert.Contains("SLATE_ACCESSIBILITY_EVIDENCE_DIR:", workflow, StringComparison.Ordinal);
        Assert.Contains("--logger \"trx;LogFileName=slate-windows-accessibility.trx\"", workflow, StringComparison.Ordinal);
        Assert.Contains("if: ${{ always() }}", workflow, StringComparison.Ordinal);
        Assert.Contains("slate-windows-accessibility-${{ github.sha }}", workflow, StringComparison.Ordinal);
        Assert.Contains("retention-days: 30", workflow, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Cargo.toml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

public sealed class W1BoundedFileIoTests
{
    [Fact]
    public void OpenedStreamBound_AllowsTheLimitAndRejectsLimitPlusOne()
    {
        byte[] expected = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray();
        using var exact = new MemoryStream(expected);
        Assert.Equal(expected, SafeFile.ReadAllBytesBounded(exact, expected.Length));

        using var oversized = new MemoryStream(new byte[expected.Length + 1]);
        FileSizeLimitExceededException exception = Assert.Throws<FileSizeLimitExceededException>(
            () => SafeFile.ReadAllBytesBounded(oversized, expected.Length));
        Assert.Equal(expected.Length + 1, exception.ObservedBytes);
        Assert.Equal(expected.Length, exception.MaximumBytes);
    }

    [Fact]
    public void OpenedStreamBound_RejectsGrowthBeyondTheLimit()
    {
        using var grewAfterLengthCheck = new UnderreportedLengthStream(new byte[5], 3);

        FileSizeLimitExceededException exception = Assert.Throws<FileSizeLimitExceededException>(
            () => SafeFile.ReadAllBytesBounded(grewAfterLengthCheck, 4));

        Assert.Equal(5, exception.ObservedBytes);
        Assert.Equal(4, exception.MaximumBytes);
    }

    [Fact]
    public void OpenedStreamBound_HonorsPreCancelledReads()
    {
        using var input = new MemoryStream(new byte[16]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => SafeFile.ReadAllBytesBounded(input, 16, cancellation.Token));
    }

    [Fact]
    public void BestEffortCleanup_DoesNotThrowForADirectoryTarget()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "safe-file-cleanup");
        string directory = Path.Combine(fixture.Root, "still-a-directory");
        Directory.CreateDirectory(directory);

        Exception? exception = Record.Exception(() => SafeFile.TryDelete(directory));

        Assert.Null(exception);
        Assert.True(Directory.Exists(directory));
    }

    private sealed class UnderreportedLengthStream(byte[] bytes, long reportedLength)
        : MemoryStream(bytes)
    {
        public override long Length => reportedLength;
    }
}

public sealed class W1VaultCloseBarrierTests
{
    [Fact]
    public async Task PendingTreeRefreshMustFinishBeforeTheVaultSessionCloses()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "tree-close-barrier");
        using var providerStarted = new ManualResetEventSlim();
        using var releaseProvider = new ManualResetEventSlim();
        var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
            treeUiContext: new PumpSynchronizationContext(),
            treeWorker: (work, _) => Task.Run(() =>
            {
                providerStarted.Set();
                releaseProvider.Wait();
                work();
            }));
        await lifecycle.OpenVaultAsync(fixture.Root);
        Assert.True(providerStarted.Wait(TimeSpan.FromSeconds(5)));
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);

        Assert.False(lifecycle.PrepareForApplicationClose());
        Assert.Contains("tree refresh cancellation requested", lifecycle.StatusText, StringComparison.OrdinalIgnoreCase);

        releaseProvider.Set();
        await sidebar.TreeRefreshCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(lifecycle.PrepareForApplicationClose());
        lifecycle.Dispose();
    }

    [Fact]
    public async Task DirectDisposalJoinsAPendingTreeRefreshBeforeReleasingTheSession()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "tree-dispose-barrier");
        using var providerStarted = new ManualResetEventSlim();
        using var releaseProvider = new ManualResetEventSlim();
        var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
            treeUiContext: new PumpSynchronizationContext(),
            treeWorker: (work, _) => Task.Run(() =>
            {
                providerStarted.Set();
                releaseProvider.Wait();
                work();
            }));
        await lifecycle.OpenVaultAsync(fixture.Root);
        Assert.True(providerStarted.Wait(TimeSpan.FromSeconds(5)));

        Task dispose = Task.Run(lifecycle.Dispose);
        await Task.Delay(100);
        Assert.False(dispose.IsCompleted);

        releaseProvider.Set();
        await dispose.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PendingSidebarFilterMustFinishBeforeTheVaultSessionCloses()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "filter-close-barrier");
        var filterContext = new PumpSynchronizationContext();
        var recents = new RecentVaultsStore(
            Path.Combine(fixture.Root, "device-state", "recent-vaults.json"));
        using var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: recents,
            filterUiContext: filterContext);
        await lifecycle.OpenVaultAsync(fixture.Root);
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
        sidebar.FilterText = "note";
        Assert.True(sidebar.IsFiltering);

        Assert.False(lifecycle.PrepareForApplicationClose());
        Assert.Contains("filter cancellation requested", lifecycle.StatusText, StringComparison.OrdinalIgnoreCase);

        await sidebar.FilterCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(sidebar.IsFiltering);
        Assert.True(lifecycle.PrepareForApplicationClose());
    }

    [Fact]
    public async Task DirectDisposalJoinsAPendingSidebarFilterBeforeReleasingTheSession()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "filter-dispose-barrier");
        var filterContext = new PumpSynchronizationContext();
        var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
            filterUiContext: filterContext);
        await lifecycle.OpenVaultAsync(fixture.Root);
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
        sidebar.FilterText = "note";
        Task pendingFilter = sidebar.FilterCompletion;
        Assert.False(pendingFilter.IsCompleted);

        Exception? exception = Record.Exception(lifecycle.Dispose);

        Assert.Null(exception);
        Assert.True(pendingFilter.IsCompleted);
    }

    private sealed class PumpSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) =>
            ThreadPool.QueueUserWorkItem(_ => callback(state));
    }
}

public sealed class W1SidebarHardeningTests
{
    [Fact]
    public void FilterOperationStateIsOwnedByTheDedicatedPartial()
    {
        string sourceDirectory = Path.Combine(
            RepoRoot(),
            "apps",
            "slate-windows",
            "src",
            "SlateWindows");
        string primary = File.ReadAllText(
            Path.Combine(sourceDirectory, "FilesSidebarViewModel.cs"));
        string filter = File.ReadAllText(
            Path.Combine(sourceDirectory, "FilesSidebarViewModel.Filter.cs"));

        Assert.Contains(
            "internal sealed partial class FilesSidebarViewModel",
            primary,
            StringComparison.Ordinal);
        foreach (string ownedMember in new[]
        {
            "_filterCancellation",
            "_filterCompletion",
            "_filterGeneration",
            "FilterAfterDelayAsync",
            "BuildDateWindows",
        })
        {
            Assert.DoesNotContain(ownedMember, primary, StringComparison.Ordinal);
            Assert.Contains(ownedMember, filter, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TreeOperationStateIsOwnedByTheDedicatedPartial()
    {
        string sourceDirectory = Path.Combine(
            RepoRoot(),
            "apps",
            "slate-windows",
            "src",
            "SlateWindows");
        string primary = File.ReadAllText(
            Path.Combine(sourceDirectory, "FilesSidebarViewModel.cs"));
        string tree = File.ReadAllText(
            Path.Combine(sourceDirectory, "FilesSidebarViewModel.TreeOperations.cs"));

        foreach (string ownedMember in new[]
        {
            "CancellationTokenSource? _treeRefreshCancellation",
            "CancellationTokenSource? _bulkExpandCancellation",
            "Task _treeRefreshCompletion",
            "Task _expandLoadedCompletion",
            "int _treeGeneration",
            "private async Task RefreshTreeAsync",
            "private async Task ExpandLoadedAsync",
        })
        {
            Assert.DoesNotContain(ownedMember, primary, StringComparison.Ordinal);
            Assert.Contains(ownedMember, tree, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task UnexpectedTreeWorkerFailureIsReportedWithoutFaultingRefreshCompletion()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-refresh-failure");
        using VaultSession session = OpenScanned(fixture.Root);
        var context = new PumpSynchronizationContext();
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            treeUiContext: context,
            treeWorker: (_, _) => Task.FromException(new InvalidOperationException("sensitive detail")));

        Assert.True(SpinWait.SpinUntil(
            () => context.PendingCount > 0,
            TimeSpan.FromSeconds(5)));
        context.Drain();
        await sidebar.TreeRefreshCompletion.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Could not load files.", sidebar.Status);
        Assert.DoesNotContain("sensitive detail", sidebar.Status, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("census", "moderate")]
    public async Task FiveThousandItemRefreshReturnsWithinTheUiDispatchBudget()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-refresh-budget");
        for (int index = 0; index < FilesSidebarViewModel.MaxMaterializedDirectoryItems + 1; index++)
        {
            File.WriteAllText(Path.Combine(fixture.Root, $"note-{index:D5}.md"), string.Empty);
        }

        using VaultSession session = OpenScanned(fixture.Root);
        var context = new PumpSynchronizationContext();
        using var providerStarted = new ManualResetEventSlim();
        using var releaseProvider = new ManualResetEventSlim();
        int uiThread = Environment.CurrentManagedThreadId;
        int providerThread = 0;
        var stopwatch = Stopwatch.StartNew();
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            treeUiContext: context,
            treeWorker: (work, _) => Task.Run(() =>
            {
                providerThread = Environment.CurrentManagedThreadId;
                providerStarted.Set();
                releaseProvider.Wait();
                work();
            }));
        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromMilliseconds(100),
            $"UI dispatch took {stopwatch.Elapsed.TotalMilliseconds:N1} ms.");
        Assert.True(providerStarted.Wait(TimeSpan.FromSeconds(5)));
        Assert.NotEqual(uiThread, providerThread);
        Assert.Empty(sidebar.RootNodes);
        Assert.True(sidebar.IsRefreshingTree);

        releaseProvider.Set();
        Assert.True(SpinWait.SpinUntil(
            () =>
            {
                context.Drain();
                return sidebar.TreeRefreshCompletion.IsCompleted;
            },
            TimeSpan.FromSeconds(10)));
        await sidebar.TreeRefreshCompletion;

        Assert.Equal(
            FilesSidebarViewModel.MaxMaterializedDirectoryItems,
            sidebar.RootNodes.Count(node => !node.IsPlaceholder));
        Assert.Single(sidebar.RootNodes, node => node.IsPlaceholder);
    }

    [Fact]
    public async Task QueuedStaleTreePublicationCannotReplaceTheLatestGeneration()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "sidebar-tree-generation");
        using VaultSession session = OpenScanned(fixture.Root);
        var context = new PumpSynchronizationContext();
        using var secondProviderStarted = new ManualResetEventSlim();
        using var releaseSecondProvider = new ManualResetEventSlim();
        int run = 0;
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            treeUiContext: context,
            treeWorker: (work, _) => Task.Run(() =>
            {
                if (Interlocked.Increment(ref run) == 2)
                {
                    secondProviderStarted.Set();
                    releaseSecondProvider.Wait();
                }

                work();
            }));
        Task first = sidebar.TreeRefreshCompletion;
        Assert.True(SpinWait.SpinUntil(
            () => context.PendingCount > 0,
            TimeSpan.FromSeconds(5)));

        sidebar.Refresh();
        Task second = sidebar.TreeRefreshCompletion;
        Assert.True(secondProviderStarted.Wait(TimeSpan.FromSeconds(5)));
        context.Drain();
        Assert.Empty(sidebar.RootNodes);

        releaseSecondProvider.Set();
        Assert.True(SpinWait.SpinUntil(
            () =>
            {
                context.Drain();
                return second.IsCompleted;
            },
            TimeSpan.FromSeconds(5)));
        await first;
        await second;
        Assert.Equal(2, sidebar.RootNodes.Count);
    }

    [Fact]
    public void LargeSidebarCollectionsUseRecyclingVirtualization()
    {
        XDocument xaml = XDocument.Load(Path.Combine(
            RepoRoot(),
            "apps",
            "slate-windows",
            "src",
            "SlateWindows",
            "MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        foreach (string name in new[] { "FilesTree", "FilterResultsList" })
        {
            XElement element = Assert.Single(
                xaml.Descendants(),
                candidate => (string?)candidate.Attribute(x + "Name") == name);
            Assert.Equal("True", (string?)element.Attribute("ScrollViewer.CanContentScroll"));
            Assert.Equal("True", (string?)element.Attribute("VirtualizingStackPanel.IsVirtualizing"));
            Assert.Equal("Recycling", (string?)element.Attribute("VirtualizingStackPanel.VirtualizationMode"));
        }

        XElement dualPane = Assert.Single(
            xaml.Descendants(),
            candidate => (string?)candidate.Attribute("AutomationProperties.AutomationId")
                == "SidebarDualPane");
        Assert.Equal("True", (string?)dualPane.Attribute("ScrollViewer.CanContentScroll"));
        Assert.Equal("True", (string?)dualPane.Attribute("VirtualizingStackPanel.IsVirtualizing"));
        Assert.Equal("Recycling", (string?)dualPane.Attribute("VirtualizingStackPanel.VirtualizationMode"));
    }

    [Fact]
    public void DirectoryListing_DrainsPagesAndReportsTheMaterializationBound()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-pages");
        for (int index = 0; index < FilesSidebarViewModel.MaxMaterializedDirectoryItems + 1; index++)
        {
            File.WriteAllText(Path.Combine(fixture.Root, $"note-{index:D5}.md"), string.Empty);
        }

        using VaultSession session = OpenScanned(fixture.Root);
        var sidebar = CreateSidebar(session, fixture.Root);

        Assert.Equal(
            FilesSidebarViewModel.MaxMaterializedDirectoryItems,
            sidebar.RootNodes.Count(node => !node.IsPlaceholder));
        FileTreeNodeViewModel overflow = Assert.Single(
            sidebar.RootNodes,
            node => node.IsPlaceholder);
        Assert.Contains("More than 5,000 items", overflow.Name, StringComparison.Ordinal);
        Assert.Contains("Showing the first 5,000 items", sidebar.Status, StringComparison.Ordinal);

        sidebar.IsDualPaneEnabled = true;
        Assert.Equal(
            FilesSidebarViewModel.MaxMaterializedDirectoryItems,
            sidebar.DualPaneFiles.Count(node => !node.IsPlaceholder));
        Assert.Single(sidebar.DualPaneFiles, node => node.IsPlaceholder);
    }

    [Fact]
    public void MixedDirectoryAndFinalFilePage_ReportsFilesOmittedInsideThePage()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-mixed-overflow");
        const int files = 100;
        int directories = FilesSidebarViewModel.MaxMaterializedDirectoryItems - (files / 2);
        for (int index = 0; index < directories; index++)
        {
            Directory.CreateDirectory(Path.Combine(fixture.Root, $"folder-{index:D5}"));
        }

        for (int index = 0; index < files; index++)
        {
            File.WriteAllText(Path.Combine(fixture.Root, $"note-{index:D3}.md"), string.Empty);
        }

        using VaultSession session = OpenScanned(fixture.Root);
        var sidebar = CreateSidebar(session, fixture.Root);

        Assert.Equal(
            FilesSidebarViewModel.MaxMaterializedDirectoryItems,
            sidebar.RootNodes.Count(node => !node.IsPlaceholder));
        Assert.Single(sidebar.RootNodes, node => node.IsPlaceholder);
        Assert.Equal(files / 2, sidebar.RootNodes.Count(node => !node.IsDirectory && !node.IsPlaceholder));
        Assert.Contains("Showing the first 5,000 items", sidebar.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshSnapshotsLiveExpansions_AndExpandLoadedUsesAPreMaterializedSnapshot()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-expansion");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "A", "B", "C"));
        File.WriteAllText(Path.Combine(fixture.Root, "A", "B", "C", "note.md"), "# Note\n");

        using VaultSession session = OpenScanned(fixture.Root);
        var sidebar = CreateSidebar(session, fixture.Root);
        FileTreeNodeViewModel firstA = Assert.Single(sidebar.RootNodes, node => node.Path == "A");
        firstA.IsExpanded = true;

        sidebar.Refresh();

        FileTreeNodeViewModel restoredA = Assert.Single(sidebar.RootNodes, node => node.Path == "A");
        Assert.True(restoredA.IsExpanded);
        Assert.Contains("A", sidebar.RestoredExpandedPaths);

        var freshSidebar = CreateSidebar(session, fixture.Root);
        FileTreeNodeViewModel freshA = Assert.Single(freshSidebar.RootNodes, node => node.Path == "A");
        freshSidebar.ExpandLoadedCommand.Execute(null);
        await freshSidebar.ExpandLoadedCompletion.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(freshA.IsExpanded);
        FileTreeNodeViewModel materializedB = Assert.Single(
            freshA.Children,
            node => node.Path == "A/B");
        Assert.False(materializedB.IsExpanded);
    }

    [Fact]
    public void TruncatedRestoredBranchDoesNotSkipLaterExpandedBranches()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-expanded-overflow");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "A"));
        for (int index = 0; index < FilesSidebarViewModel.MaxMaterializedDirectoryItems + 1; index++)
        {
            File.WriteAllText(
                Path.Combine(fixture.Root, "A", $"note-{index:D5}.md"),
                string.Empty);
        }

        Directory.CreateDirectory(Path.Combine(fixture.Root, "Z", "Child"));
        using VaultSession session = OpenScanned(fixture.Root);
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            restoredExpandedPaths: ["A", "Z"]);

        FileTreeNodeViewModel a = Assert.Single(sidebar.RootNodes, node => node.Path == "A");
        Assert.True(a.IsExpanded);
        Assert.Single(a.Children, node => node.IsPlaceholder);
        FileTreeNodeViewModel z = Assert.Single(sidebar.RootNodes, node => node.Path == "Z");
        Assert.True(z.IsExpanded);
        Assert.Contains(z.Children, node => node.Path == "Z/Child");
    }

    [Fact]
    public void FilterDebouncePublishesOnlyTheLatestGeneration()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "sidebar-filter-generation");
        using VaultSession session = OpenScanned(fixture.Root);
        var context = new PumpSynchronizationContext();
        SynchronizationContext? prior = SynchronizationContext.Current;
        FilesSidebarViewModel sidebar;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            sidebar = CreateSidebar(session, fixture.Root, context);
            sidebar.FilterText = "note0";
            sidebar.FilterText = "note1";
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(prior);
        }

        Assert.True(SpinWait.SpinUntil(
            () =>
            {
                context.Drain();
                return sidebar.FilterResults.Count > 0;
            },
            TimeSpan.FromSeconds(5)));
        FileTreeNodeViewModel result = Assert.Single(sidebar.FilterResults);
        Assert.Equal("note1.md", result.Path);
    }

    private static VaultSession OpenScanned(string root)
    {
        VaultSession session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    private static FilesSidebarViewModel CreateSidebar(
        VaultSession session,
        string root,
        SynchronizationContext? filterUiContext = null,
        SynchronizationContext? treeUiContext = null,
        Func<Action, CancellationToken, Task>? treeWorker = null,
        IEnumerable<string>? restoredExpandedPaths = null) => new(
        session,
        _ => { },
        restoredExpandedPaths: restoredExpandedPaths,
        vaultRoot: root,
        localAppDataRoot: Path.Combine(root, "device-state"),
        filterUiContext: filterUiContext,
        treeUiContext: treeUiContext,
        treeWorker: treeWorker);

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Cargo.toml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed class PumpSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = [];
        public int PendingCount => _queue.Count;

        public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

        public void Drain()
        {
            while (_queue.TryDequeue(out var work))
            {
                work.Callback(work.State);
            }
        }
    }
}

public sealed class W1SidebarSettingsHardeningTests
{
    [Fact]
    public void GroupedOldestSortDirectionsRoundTrip()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-settings-sort");
        var store = new SidebarSettingsStore(fixture.Root);
        store.Load();

        store.SetOrganization(SidebarSortMode.CreatedOldest, groupByDate: true);
        string settingsPath = Path.Combine(fixture.Root, ".slate", "sidebar.json");
        using (JsonDocument written = JsonDocument.Parse(File.ReadAllBytes(settingsPath)))
        {
            Assert.Equal(
                "dateBuckets",
                written.RootElement.GetProperty("grouping").GetString());
        }

        SidebarSettingsSnapshot created = new SidebarSettingsStore(fixture.Root).Load();
        Assert.Null(created.ReadOnlyReason);
        Assert.True(created.GroupByDate);
        Assert.Equal(SidebarSortMode.CreatedOldest, created.SortMode);

        store.SetOrganization(SidebarSortMode.ModifiedOldest, groupByDate: true);
        SidebarSettingsSnapshot modified = new SidebarSettingsStore(fixture.Root).Load();
        Assert.Null(modified.ReadOnlyReason);
        Assert.True(modified.GroupByDate);
        Assert.Equal(SidebarSortMode.ModifiedOldest, modified.SortMode);
    }

    [Fact]
    public void PinAndShortcutBoundsFailWithoutSilentlyDroppingState()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-settings-bounds");
        string slateDirectory = Path.Combine(fixture.Root, ".slate");
        Directory.CreateDirectory(slateDirectory);
        string settingsPath = Path.Combine(slateDirectory, "sidebar.json");
        var reserved = Enumerable.Range(0, SidebarSettingsStore.MaxShortcuts)
            .Select(index => new { kind = "future", path = $"reserved-{index}" })
            .ToArray();
        File.WriteAllText(
            settingsPath,
            JsonSerializer.Serialize(new { version = SidebarSettingsStore.SchemaVersion, shortcuts = reserved }));
        byte[] original = File.ReadAllBytes(settingsPath);
        var store = new SidebarSettingsStore(fixture.Root);
        store.Load();

        Assert.Throws<InvalidOperationException>(() => store.SetShortcuts(
            [new SidebarShortcutState("file", "visible.md")]));
        Assert.Equal(original, File.ReadAllBytes(settingsPath));

        string[] excessivePins = Enumerable.Range(0, SidebarSettingsStore.MaxPinsPerFolder + 1)
            .Select(index => $"folder/note-{index}.md")
            .ToArray();
        Assert.Throws<InvalidOperationException>(() => store.ReplacePins(excessivePins));
        Assert.Equal(original, File.ReadAllBytes(settingsPath));

        Dictionary<string, string[]> saturatedPins = Enumerable.Range(0, 10).ToDictionary(
            folder => $"folder-{folder}",
            folder => Enumerable.Range(0, SidebarSettingsStore.MaxPinsPerFolder)
                .Select(index => $"folder-{folder}/note-{index}.md")
                .ToArray());
        File.WriteAllText(
            settingsPath,
            JsonSerializer.Serialize(new
            {
                version = SidebarSettingsStore.SchemaVersion,
                pins = saturatedPins,
            }));
        byte[] saturated = File.ReadAllBytes(settingsPath);
        var saturatedStore = new SidebarSettingsStore(fixture.Root);
        Assert.Null(saturatedStore.Load().ReadOnlyReason);
        Assert.Throws<InvalidOperationException>(() => saturatedStore.SetPinsForFolder(
            "one-too-many",
            ["one-too-many/note.md"]));
        Assert.Equal(saturated, File.ReadAllBytes(settingsPath));

        Assert.Throws<InvalidOperationException>(() => saturatedStore.SetPinsForFolder(
            new string('f', SidebarSettingsStore.MaxPathLength + 1),
            []));
        Assert.Equal(saturated, File.ReadAllBytes(settingsPath));
    }
}

public sealed class W1ImportSafeguardTests
{
    [Fact]
    public void ImportEntryBudgetStopsAtExactlyTenThousand()
    {
        int visited = 0;
        for (int index = 0; index < FilesSidebarViewModel.MaxImportEntries; index++)
        {
            Assert.True(FilesSidebarViewModel.TryReserveImportEntry(ref visited));
        }

        Assert.Equal(10_000, visited);
        Assert.False(FilesSidebarViewModel.TryReserveImportEntry(ref visited));
        Assert.Equal(10_000, visited);
    }

    [Fact]
    public void ReparseCheckWalksEveryAncestorUntilItFindsOne()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "slate-import-walk"));
        string reparseAncestor = Path.Combine(root, "linked");
        string source = Path.Combine(reparseAncestor, "nested", "note.md");
        var inspected = new List<string>();

        bool found = FilesSidebarViewModel.HasReparsePointInPath(
            source,
            path =>
            {
                inspected.Add(path);
                return string.Equals(path, reparseAncestor, StringComparison.OrdinalIgnoreCase)
                    ? FileAttributes.Directory | FileAttributes.ReparsePoint
                    : FileAttributes.Normal;
            });

        Assert.True(found);
        Assert.Equal(source, inspected[0]);
        Assert.Contains(reparseAncestor, inspected, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(root, inspected, StringComparer.OrdinalIgnoreCase);
    }

}

public sealed class W1SingleInstanceHardeningTests
{
    [Fact]
    public async Task StalledConnectionTimesOutAndTheNextActivationIsAccepted()
    {
        string identity = $"slate-single-instance-deadline-{Guid.NewGuid():N}";
        using var primary = new SingleInstanceCoordinator(identity);
        using var secondary = new SingleInstanceCoordinator(identity);
        Assert.True(primary.IsPrimary);
        Assert.False(secondary.IsPrimary);
        var received = new TaskCompletionSource<string[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        primary.StartListening(arguments => received.TrySetResult(arguments));

        using var stalled = new NamedPipeClientStream(
            ".",
            primary.PipeNameForTesting,
            PipeDirection.Out,
            PipeOptions.Asynchronous);
        await stalled.ConnectAsync(5_000);
        byte[] header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, 32);
        await stalled.WriteAsync(header);
        await stalled.WriteAsync(new byte[] { (byte)'[' });
        await stalled.FlushAsync();

        await Task.Delay(SingleInstanceCoordinator.ConnectionReadTimeout + TimeSpan.FromMilliseconds(300));
        string[] expected = ["--from-test", @"C:\Vaults\Recovered"];
        Assert.True(secondary.SendActivation(expected, TimeSpan.FromSeconds(5)));
        Assert.Equal(expected, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
