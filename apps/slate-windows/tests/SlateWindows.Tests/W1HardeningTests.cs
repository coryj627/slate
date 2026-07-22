// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows.Threading;
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
    public async Task DirectDisposalJoinsActiveSessionLoadingBeforeRelease()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "session-load-dispose-barrier");
        using var loadStarted = new ManualResetEventSlim();
        using var releaseLoad = new ManualResetEventSlim();
        using var disposeEntered = new ManualResetEventSlim();
        var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
            sessionLoadWorker: work => Task.Run(() =>
            {
                loadStarted.Set();
                releaseLoad.Wait();
                return work();
            }));

        Task open = lifecycle.OpenVaultAsync(fixture.Root);
        Assert.True(loadStarted.Wait(TimeSpan.FromSeconds(5)));
        Task dispose = Task.Run(() =>
        {
            disposeEntered.Set();
            lifecycle.Dispose();
        });
        Assert.True(disposeEntered.Wait(TimeSpan.FromSeconds(5)));
        Assert.False(dispose.Wait(TimeSpan.FromMilliseconds(200)));

        releaseLoad.Set();
        await dispose.WaitAsync(TimeSpan.FromSeconds(5));
        await open.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(lifecycle.FileSidebar);
        Assert.Null(lifecycle.Workspace);
    }

    [Fact]
    public async Task OffThreadDisposalRunsOnTheCapturedWpfDispatcher()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "dispatcher-dispose-affinity");
        using var ready = new ManualResetEventSlim();
        using var filterStarted = new ManualResetEventSlim();
        Dispatcher? ownerDispatcher = null;
        VaultLifecycleViewModel? lifecycle = null;
        Exception? setupException = null;
        int ownerThreadId = 0;
        int cancellationThreadId = 0;
        var ownerThread = new Thread(() =>
        {
            ownerThreadId = Environment.CurrentManagedThreadId;
            ownerDispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(ownerDispatcher));
            lifecycle = new VaultLifecycleViewModel(
                pickVault: () => Task.FromResult<string?>(fixture.Root),
                enqueueUi: action => ownerDispatcher.BeginInvoke(action),
                recentVaultsStore: new RecentVaultsStore(
                    Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
                filterUiContext: SynchronizationContext.Current,
                treeUiContext: SynchronizationContext.Current,
                filterWorker: (_, cancellationToken) =>
                {
                    cancellationToken.Register(
                        () => Volatile.Write(
                            ref cancellationThreadId,
                            Environment.CurrentManagedThreadId));
                    filterStarted.Set();
                    return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                });
            ownerDispatcher.BeginInvoke(async () =>
            {
                try
                {
                    await lifecycle.OpenVaultAsync(fixture.Root);
                    FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(
                        lifecycle.FileSidebar);
                    sidebar.FilterText = "note";
                }
                catch (Exception exception)
                {
                    setupException = exception;
                }
                finally
                {
                    ready.Set();
                }
            });
            Dispatcher.Run();
        })
        {
            IsBackground = true,
        };
        ownerThread.SetApartmentState(ApartmentState.STA);
        ownerThread.Start();

        try
        {
            Assert.True(ready.Wait(TimeSpan.FromSeconds(10)));
            Assert.Null(setupException);
            Assert.True(filterStarted.Wait(TimeSpan.FromSeconds(5)));
            VaultLifecycleViewModel activeLifecycle = Assert.IsType<VaultLifecycleViewModel>(lifecycle);

            await Task.Run(activeLifecycle.Dispose).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(ownerThreadId, Volatile.Read(ref cancellationThreadId));
        }
        finally
        {
            ownerDispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);
            Assert.True(ownerThread.Join(TimeSpan.FromSeconds(5)));
        }
    }

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
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);

        Task dispose = Task.Run(lifecycle.Dispose);
        Assert.True(SpinWait.SpinUntil(
            () => sidebar.SessionShutdownStarted,
            TimeSpan.FromSeconds(5)));
        Assert.False(dispose.IsCompleted);

        releaseProvider.Set();
        await dispose.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DirectDisposalCancelsTreeRefreshAcrossCompletionPublication()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "tree-publication-barrier");
        using var providerStarted = new ManualResetEventSlim();
        int workerInvocations = 0;
        var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
            treeUiContext: new PumpSynchronizationContext(),
            treeWorker: (work, cancellationToken) =>
            {
                if (Interlocked.Increment(ref workerInvocations) == 1)
                {
                    work();
                    return Task.CompletedTask;
                }

                providerStarted.Set();
                cancellationToken.WaitHandle.WaitOne();
                cancellationToken.ThrowIfCancellationRequested();
                work();
                return Task.CompletedTask;
            });
        await lifecycle.OpenVaultAsync(fixture.Root);
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
        await sidebar.TreeRefreshCompletion.WaitAsync(TimeSpan.FromSeconds(5));

        Task refreshCall = Task.Run(() => sidebar.Refresh());
        Assert.True(providerStarted.Wait(TimeSpan.FromSeconds(5)));
        Task dispose = Task.Run(lifecycle.Dispose);

        await dispose.WaitAsync(TimeSpan.FromSeconds(5));
        await refreshCall.WaitAsync(TimeSpan.FromSeconds(5));
        await sidebar.TreeRefreshCompletion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task TreePublicationDoesNotDependOnPumpingTheAmbientContext()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "tree-publication-context");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using (var scanCancellation = new CancelToken())
        {
            session.ScanInitial(scanCancellation);
        }
        var ambientContext = new NonPumpingSynchronizationContext();
        FilesSidebarViewModel sidebar;
        SynchronizationContext? prior = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(ambientContext);
            sidebar = new FilesSidebarViewModel(
                session,
                _ => { },
                vaultRoot: fixture.Root,
                localAppDataRoot: Path.Combine(fixture.Root, "device-state"),
                treeUiContext: new PumpSynchronizationContext());
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(prior);
        }

        await sidebar.TreeRefreshCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, ambientContext.PostCount);
    }

    [Fact]
    public async Task OwnerContextDisposeDoesNotWaitOnImportOrExpansionUiContinuations()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "sidebar-owner-context-dispose");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "folder"));
        string source = Path.GetTempFileName();
        try
        {
            using var expansionStarted = new ManualResetEventSlim();
            using var importStarted = new ManualResetEventSlim();
            using var completed = new ManualResetEventSlim();
            int delayExpansion = 0;
            Exception? threadException = null;
            var lifecycle = new VaultLifecycleViewModel(
                pickVault: () => Task.FromResult<string?>(fixture.Root),
                enqueueUi: action => action(),
                recentVaultsStore: new RecentVaultsStore(
                    Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
                treeUiContext: new PumpSynchronizationContext(),
                pickImportSources: () => Task.FromResult<IReadOnlyList<string>>([source]),
                treeWorker: (work, cancellationToken) =>
                {
                    if (Volatile.Read(ref delayExpansion) == 0)
                    {
                        return Task.Run(work, cancellationToken);
                    }

                    expansionStarted.Set();
                    return Task.Run(async () =>
                    {
                        await Task.Delay(100);
                        work();
                    });
                },
                importWorker: (work, _) => Task.Run(async () =>
                {
                    importStarted.Set();
                    await Task.Delay(100);
                    work();
                }));
            await lifecycle.OpenVaultAsync(fixture.Root);
            FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
            await sidebar.TreeRefreshCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            Volatile.Write(ref delayExpansion, 1);

            var ownerThread = new Thread(() =>
            {
                var context = new QueueingSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(context);
                try
                {
                    sidebar.ExpandLoadedCommand.Execute(null);
                    sidebar.ImportCommand.Execute(null);
                    Assert.True(expansionStarted.Wait(TimeSpan.FromSeconds(5)));
                    Assert.True(importStarted.Wait(TimeSpan.FromSeconds(5)));
                    lifecycle.Dispose();
                    Assert.True(SpinWait.SpinUntil(
                        () =>
                        {
                            context.Drain();
                            return sidebar.ExpandLoadedCompletion.IsCompleted
                                && sidebar.ImportCompletion.IsCompleted;
                        },
                        TimeSpan.FromSeconds(5)));
                }
                catch (Exception exception)
                {
                    threadException = exception;
                }
                finally
                {
                    completed.Set();
                }
            })
            {
                IsBackground = true,
            };
            ownerThread.Start();

            Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
            Assert.Null(threadException);
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public async Task CancellationAtTreeAndBulkCompletionNeverAbortsCleanup()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "tree-cancellation-ownership");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "folder"));
        using var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
            treeUiContext: new PumpSynchronizationContext());
        await lifecycle.OpenVaultAsync(fixture.Root);
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
        await sidebar.TreeRefreshCompletion.WaitAsync(TimeSpan.FromSeconds(5));

        for (int attempt = 0; attempt < 25; attempt++)
        {
            sidebar.Refresh();
            Task refresh = sidebar.TreeRefreshCompletion;
            await Task.WhenAll(Task.Run(sidebar.CancelTreeRefresh), refresh)
                .WaitAsync(TimeSpan.FromSeconds(5));

            sidebar.ExpandLoadedCommand.Execute(null);
            Task expansion = sidebar.ExpandLoadedCompletion;
            await Task.WhenAll(Task.Run(sidebar.CancelExpandLoaded), expansion)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
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

    [Theory]
    [InlineData("note1")]
    [InlineData("")]
    public async Task DirectDisposalJoinsASupersededSidebarFilter(
        string replacementQuery)
    {
        using FixtureVault fixture = FixtureVault.Create(2, "superseded-filter-dispose-barrier");
        using var firstWorkerStarted = new ManualResetEventSlim();
        using var releaseFirstWorker = new ManualResetEventSlim();
        int workerInvocations = 0;
        var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
            filterUiContext: new PumpSynchronizationContext(),
            filterWorker: (work, _) => Task.Run(() =>
            {
                if (Interlocked.Increment(ref workerInvocations) == 1)
                {
                    firstWorkerStarted.Set();
                    releaseFirstWorker.Wait();
                }

                work();
            }));
        await lifecycle.OpenVaultAsync(fixture.Root);
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
        sidebar.FilterText = "note0";
        Assert.True(firstWorkerStarted.Wait(TimeSpan.FromSeconds(5)));
        Task firstFilter = sidebar.FilterCompletion;

        sidebar.FilterText = replacementQuery;
        Task transitiveBarrier = sidebar.FilterCompletion;
        Task dispose = Task.Run(lifecycle.Dispose);

        Assert.True(SpinWait.SpinUntil(
            () => sidebar.SessionShutdownStarted,
            TimeSpan.FromSeconds(5)));
        Assert.False(dispose.IsCompleted);
        Assert.False(firstFilter.IsCompleted);

        releaseFirstWorker.Set();
        await dispose.WaitAsync(TimeSpan.FromSeconds(5));
        await transitiveBarrier.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(firstFilter.IsCompleted);
    }

    [Fact]
    public async Task DirectDisposalJoinsBulkExpansionSessionWork()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "expand-dispose-barrier");
        string folder = Path.Combine(fixture.Root, "folder");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "child.md"), string.Empty);
        using var expansionWorkerStarted = new ManualResetEventSlim();
        using var releaseExpansionWorker = new ManualResetEventSlim();
        int blockWorker = 0;
        var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
            treeUiContext: new PumpSynchronizationContext(),
            treeWorker: (work, cancellationToken) => Task.Run(() =>
            {
                if (Volatile.Read(ref blockWorker) != 0)
                {
                    expansionWorkerStarted.Set();
                    releaseExpansionWorker.Wait();
                }

                work();
            }, cancellationToken));
        await lifecycle.OpenVaultAsync(fixture.Root);
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
        await sidebar.TreeRefreshCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Volatile.Write(ref blockWorker, 1);
        sidebar.ExpandLoadedCommand.Execute(null);
        Assert.True(expansionWorkerStarted.Wait(TimeSpan.FromSeconds(5)));

        Task dispose = Task.Run(lifecycle.Dispose);
        Assert.True(SpinWait.SpinUntil(
            () => sidebar.SessionShutdownStarted,
            TimeSpan.FromSeconds(5)));
        Assert.False(dispose.IsCompleted);

        releaseExpansionWorker.Set();
        await dispose.WaitAsync(TimeSpan.FromSeconds(5));
        await sidebar.ExpandLoadedCompletion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DirectDisposalJoinsImportSessionWork()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "import-dispose-barrier");
        string source = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(source, "imported");
            using var importWorkerStarted = new ManualResetEventSlim();
            using var releaseImportWorker = new ManualResetEventSlim();
            var lifecycle = new VaultLifecycleViewModel(
                pickVault: () => Task.FromResult<string?>(fixture.Root),
                enqueueUi: action => action(),
                recentVaultsStore: new RecentVaultsStore(
                    Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
                pickImportSources: () => Task.FromResult<IReadOnlyList<string>>([source]),
                importWorker: (work, _) => Task.Run(() =>
                {
                    importWorkerStarted.Set();
                    releaseImportWorker.Wait();
                    work();
                }));
            await lifecycle.OpenVaultAsync(fixture.Root);
            FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
            sidebar.ImportCommand.Execute(null);
            Assert.True(importWorkerStarted.Wait(TimeSpan.FromSeconds(5)));
            Task import = sidebar.ImportCompletion;

            Task dispose = Task.Run(lifecycle.Dispose);
            Assert.True(SpinWait.SpinUntil(
                () => sidebar.SessionShutdownStarted,
                TimeSpan.FromSeconds(5)));
            Assert.False(dispose.IsCompleted);

            releaseImportWorker.Set();
            await dispose.WaitAsync(TimeSpan.FromSeconds(5));
            await import.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public async Task ImportIsCloseVisibleWhileTheSourcePickerIsPending()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "import-picker-close-barrier");
        var selectedSources = new TaskCompletionSource<IReadOnlyList<string>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
            pickImportSources: () => selectedSources.Task);
        await lifecycle.OpenVaultAsync(fixture.Root);
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
        sidebar.ImportCommand.Execute(null);

        Assert.True(sidebar.IsImporting);
        Assert.False(lifecycle.PrepareForApplicationClose());
        Assert.Contains("import cancellation requested", lifecycle.StatusText, StringComparison.OrdinalIgnoreCase);

        selectedSources.SetResult([]);
        await sidebar.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(sidebar.IsImporting);
    }

    [Fact]
    public async Task PickerCompletionAfterDirectDisposalCannotAdmitImportWork()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "import-picker-shutdown-admission");
        string source = Path.GetTempFileName();
        try
        {
            var selectedSources = new TaskCompletionSource<IReadOnlyList<string>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int workerInvocations = 0;
            var lifecycle = new VaultLifecycleViewModel(
                pickVault: () => Task.FromResult<string?>(fixture.Root),
                enqueueUi: action => action(),
                recentVaultsStore: new RecentVaultsStore(
                    Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
                pickImportSources: () => selectedSources.Task,
                importWorker: (work, cancellationToken) =>
                {
                    Interlocked.Increment(ref workerInvocations);
                    return Task.Run(work, cancellationToken);
                });
            await lifecycle.OpenVaultAsync(fixture.Root);
            FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
            sidebar.ImportCommand.Execute(null);
            Task import = sidebar.ImportCompletion;
            Assert.True(sidebar.IsImporting);

            await Task.Run(lifecycle.Dispose).WaitAsync(TimeSpan.FromSeconds(5));
            selectedSources.SetResult([source]);
            await import.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(0, Volatile.Read(ref workerInvocations));
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public async Task ShutdownRejectsEveryRepresentativeSidebarSessionEntry()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "sidebar-shutdown-admission");
        string folderPath = Path.Combine(fixture.Root, "folder");
        Directory.CreateDirectory(folderPath);
        File.WriteAllText(Path.Combine(folderPath, "child.md"), string.Empty);
        int treeWorkerInvocations = 0;
        int filterWorkerInvocations = 0;
        var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
            treeUiContext: new PumpSynchronizationContext(),
            filterUiContext: new PumpSynchronizationContext(),
            treeWorker: (work, cancellationToken) => Task.Run(() =>
            {
                Interlocked.Increment(ref treeWorkerInvocations);
                work();
            }, cancellationToken),
            filterWorker: (work, cancellationToken) => Task.Run(() =>
            {
                Interlocked.Increment(ref filterWorkerInvocations);
                work();
            }, cancellationToken));
        await lifecycle.OpenVaultAsync(fixture.Root);
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
        await sidebar.TreeRefreshCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        int initialTreeWorkers = Volatile.Read(ref treeWorkerInvocations);
        FileTreeNodeViewModel folder = Assert.Single(
            sidebar.RootNodes,
            node => node.IsDirectory && node.Path == "folder");
        FileTreeNodeViewModel file = Assert.Single(
            sidebar.RootNodes,
            node => !node.IsDirectory && node.Path == "note0.md");
        int initialFolderChildren = folder.Children.Count;

        lifecycle.Dispose();
        Exception? exception = Record.Exception(() =>
        {
            sidebar.FilterText = "note";
            sidebar.Refresh();
            sidebar.ShowTags = true;
            sidebar.SelectedNode = folder;
            sidebar.IsDualPaneEnabled = true;
            sidebar.LoadChildren(folder);
            sidebar.ExpandLoadedCommand.Execute(null);
            sidebar.ImportCommand.Execute(null);

            sidebar.MutationName = "after-shutdown";
            sidebar.CreateFolderCommand.Execute(null);
            sidebar.CreateNoteCommand.Execute(null);
            sidebar.CreateFolderNoteCommand.Execute(null);
            sidebar.DeleteFolderNoteCommand.Execute(null);

            sidebar.SelectedNode = file;
            sidebar.MutationName = "renamed-after-shutdown.md";
            sidebar.RenameCommand.Execute(null);
            sidebar.CopyWikilinkCommand.Execute(null);
            sidebar.DeleteCommand.Execute(null);
            file.IsBatchSelected = true;
            sidebar.TagInput = "after-shutdown";
            sidebar.AddTagCommand.Execute(null);
            sidebar.RemoveTagCommand.Execute(null);
            sidebar.MoveDestination = "folder";
            sidebar.BatchMoveCommand.Execute(null);
            sidebar.BatchTrashCommand.Execute(null);
        });

        await sidebar.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        await sidebar.ExpandLoadedCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(exception);
        Assert.Equal(initialTreeWorkers, Volatile.Read(ref treeWorkerInvocations));
        Assert.Equal(0, Volatile.Read(ref filterWorkerInvocations));
        Assert.Equal(initialFolderChildren, folder.Children.Count);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "after-shutdown")));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "after-shutdown.md")));
        Assert.True(File.Exists(Path.Combine(fixture.Root, "note0.md")));
    }

    [Fact]
    public async Task ThrowingFilterCancellationCallbackDoesNotSkipImportCancellation()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "sidebar-cancel-callback-failure");
        string source = Path.GetTempFileName();
        try
        {
            using var filterStarted = new ManualResetEventSlim();
            using var importStarted = new ManualResetEventSlim();
            var lifecycle = new VaultLifecycleViewModel(
                pickVault: () => Task.FromResult<string?>(fixture.Root),
                enqueueUi: action => action(),
                recentVaultsStore: new RecentVaultsStore(
                    Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
                filterUiContext: new PumpSynchronizationContext(),
                pickImportSources: () => Task.FromResult<IReadOnlyList<string>>([source]),
                filterWorker: (_, cancellationToken) =>
                {
                    cancellationToken.Register(
                        () => throw new InvalidOperationException("sensitive cancellation detail"));
                    filterStarted.Set();
                    return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                },
                importWorker: (_, cancellationToken) =>
                {
                    importStarted.Set();
                    return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                });
            await lifecycle.OpenVaultAsync(fixture.Root);
            FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
            sidebar.FilterText = "note";
            sidebar.ImportCommand.Execute(null);
            Assert.True(filterStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(importStarted.Wait(TimeSpan.FromSeconds(5)));

            await Task.Run(lifecycle.Dispose).WaitAsync(TimeSpan.FromSeconds(5));
            await sidebar.FilterCompletion.WaitAsync(TimeSpan.FromSeconds(5));
            await sidebar.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            File.Delete(source);
        }
    }

    private sealed class PumpSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) =>
            ThreadPool.QueueUserWorkItem(_ => callback(state));
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        private int _postCount;
        public int PostCount => Volatile.Read(ref _postCount);

        public override void Post(SendOrPostCallback callback, object? state) =>
            Interlocked.Increment(ref _postCount);
    }

    private sealed class QueueingSynchronizationContext : SynchronizationContext
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
            "_filterCancellationGate",
            "_filterCompletion",
            "Func<Action, CancellationToken, Task> _runFilterWorker",
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
            "SemaphoreSlim _treeProviderLane",
            "Task _treeRefreshCompletion",
            "Task _expandLoadedCompletion",
            "int _treeGeneration",
            "private async Task RefreshTreeAsync",
            "private async Task ExpandLoadedAsync",
            "private async Task<bool> RunAdmittedTreeWorkerAsync",
        })
        {
            Assert.DoesNotContain(ownedMember, primary, StringComparison.Ordinal);
            Assert.Contains(ownedMember, tree, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ChildExpansionStateIsOwnedByTheDedicatedPartial()
    {
        string sourceDirectory = Path.Combine(
            RepoRoot(),
            "apps",
            "slate-windows",
            "src",
            "SlateWindows");
        string primary = File.ReadAllText(
            Path.Combine(sourceDirectory, "FilesSidebarViewModel.cs"));
        string childExpansion = File.ReadAllText(
            Path.Combine(sourceDirectory, "FilesSidebarViewModel.ChildExpansion.cs"));

        foreach (string ownedMember in new[]
        {
            "object _childExpansionGate",
            "Dictionary<FileTreeNodeViewModel, ChildExpansionOperation> _childExpansions",
            "int _activeChildExpansions",
            "TaskCompletionSource? _childExpansionsIdle",
            "private async Task LoadChildrenAsync",
            "internal void CancelChildExpansions",
            "record ChildExpansionOutcome",
        })
        {
            Assert.DoesNotContain(ownedMember, primary, StringComparison.Ordinal);
            Assert.Contains(ownedMember, childExpansion, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ImportOperationStateIsOwnedByTheDedicatedPartial()
    {
        string sourceDirectory = Path.Combine(
            RepoRoot(),
            "apps",
            "slate-windows",
            "src",
            "SlateWindows");
        string primary = File.ReadAllText(
            Path.Combine(sourceDirectory, "FilesSidebarViewModel.cs"));
        string import = File.ReadAllText(
            Path.Combine(sourceDirectory, "FilesSidebarViewModel.Import.cs"));

        foreach (string ownedMember in new[]
        {
            "MaxImportEntries =",
            "MaxImportFileBytes =",
            "Func<Task<IReadOnlyList<string>>> _pickImportSources",
            "string? _vaultRoot",
            "CancellationTokenSource? _importCancellation",
            "object _importCancellationGate",
            "Task _importCompletion",
            "Func<Action, CancellationToken, Task> _runImportWorker",
            "bool _isImporting",
            "private async Task ImportAsync",
            "private ImportResult ImportSources",
            "internal static bool HasReparsePointInPath",
        })
        {
            Assert.DoesNotContain(ownedMember, primary, StringComparison.Ordinal);
            Assert.Contains(ownedMember, import, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SessionWorkAdmissionIsOwnedByTheDedicatedPartial()
    {
        string sourceDirectory = Path.Combine(
            RepoRoot(),
            "apps",
            "slate-windows",
            "src",
            "SlateWindows");
        string primary = File.ReadAllText(
            Path.Combine(sourceDirectory, "FilesSidebarViewModel.cs"));
        string sessionWork = File.ReadAllText(
            Path.Combine(sourceDirectory, "FilesSidebarViewModel.SessionWork.cs"));

        foreach (string ownedMember in new[]
        {
            "object _sessionWorkGate",
            "int _activeSessionWork",
            "bool _sessionShutdownStarted",
            "BeginSessionShutdownAndCaptureWork",
            "TryBeginSessionWork",
            "class SessionWorkLease",
            "record SidebarSessionShutdown",
        })
        {
            Assert.DoesNotContain(ownedMember, primary, StringComparison.Ordinal);
            Assert.Contains(ownedMember, sessionWork, StringComparison.Ordinal);
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
    public async Task UnexpectedFilterWorkerFailureIsReportedWithoutFaultingCompletion()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-filter-failure");
        using VaultSession session = OpenScanned(fixture.Root);
        var context = new PumpSynchronizationContext();
        var announcements = new List<A11yEvent>();
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            filterUiContext: context,
            filterWorker: (_, _) =>
                Task.FromException(new InvalidOperationException("sensitive detail")),
            announce: announcements.Add);
        announcements.Clear();

        sidebar.FilterText = "note";
        Assert.True(SpinWait.SpinUntil(
            () => context.PendingCount > 0,
            TimeSpan.FromSeconds(5)));
        context.Drain();
        await sidebar.FilterCompletion.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Could not filter files.", sidebar.Status);
        Assert.DoesNotContain("sensitive detail", sidebar.Status, StringComparison.Ordinal);
        Assert.Contains(announcements, item => item is A11yEvent.HostComposed);
        Assert.All(
            announcements,
            item => Assert.DoesNotContain(
                "sensitive detail",
                item.ToString(),
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ThrowingFilterPresentationContextDoesNotFaultCompletion()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "sidebar-filter-context-failure");
        using VaultSession session = OpenScanned(fixture.Root);
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            filterUiContext: new ThrowingSynchronizationContext());

        sidebar.FilterText = "note";
        await sidebar.FilterCompletion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ImportPickerFailureUsesGenericAccessibleCopy()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-import-picker-failure");
        using VaultSession session = OpenScanned(fixture.Root);
        var announcements = new List<A11yEvent>();
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            announce: announcements.Add,
            pickImportSources: () => Task.FromException<IReadOnlyList<string>>(
                new IOException("sensitive picker path")));
        announcements.Clear();

        sidebar.ImportCommand.Execute(null);
        await sidebar.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Could not choose import sources.", sidebar.Status);
        Assert.Contains(announcements, item => item is A11yEvent.HostComposed);
        Assert.All(
            announcements,
            item => Assert.DoesNotContain(
                "sensitive picker path",
                item.ToString(),
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportWorkerFailureUsesGenericAccessibleCopy()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-import-worker-failure");
        string source = Path.GetTempFileName();
        try
        {
            using VaultSession session = OpenScanned(fixture.Root);
            var announcements = new List<A11yEvent>();
            var sidebar = CreateSidebar(
                session,
                fixture.Root,
                announce: announcements.Add,
                pickImportSources: () => Task.FromResult<IReadOnlyList<string>>([source]),
                importWorker: (_, _) =>
                    Task.FromException(new ApplicationException("sensitive import path")));
            announcements.Clear();

            sidebar.ImportCommand.Execute(null);
            await sidebar.ImportCompletion.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal("Could not import the selected items.", sidebar.Status);
            Assert.Contains(announcements, item => item is A11yEvent.HostComposed);
            Assert.All(
                announcements,
                item => Assert.DoesNotContain(
                    "sensitive import path",
                    item.ToString(),
                    StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public async Task UnexpectedBulkExpansionFailureUsesGenericAccessibleCopy()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-expansion-worker-failure");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "folder"));
        using VaultSession session = OpenScanned(fixture.Root);
        var announcements = new List<A11yEvent>();
        var sidebar = CreateSidebar(
            session,
            fixture.Root,
            treeWorker: (_, _) =>
                Task.FromException(new ApplicationException("sensitive expansion detail")),
            announce: announcements.Add);
        announcements.Clear();

        sidebar.ExpandLoadedCommand.Execute(null);
        await sidebar.ExpandLoadedCompletion.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Could not expand loaded folders.", sidebar.Status);
        Assert.Contains(announcements, item => item is A11yEvent.HostComposed);
        Assert.All(
            announcements,
            item => Assert.DoesNotContain(
                "sensitive expansion detail",
                item.ToString(),
                StringComparison.Ordinal));
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
    public void DirectoryListing_UsesBoundedLookaheadAndReportsTheMaterializationBound()
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
        Func<Action, CancellationToken, Task>? filterWorker = null,
        IEnumerable<string>? restoredExpandedPaths = null,
        Action<A11yEvent>? announce = null,
        Func<Task<IReadOnlyList<string>>>? pickImportSources = null,
        Func<Action, CancellationToken, Task>? importWorker = null) => new(
        session,
        announce ?? (_ => { }),
        restoredExpandedPaths: restoredExpandedPaths,
        vaultRoot: root,
        localAppDataRoot: Path.Combine(root, "device-state"),
        filterUiContext: filterUiContext,
        treeUiContext: treeUiContext,
        treeWorker: treeWorker,
        filterWorker: filterWorker,
        pickImportSources: pickImportSources,
        importWorker: importWorker);

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

    private sealed class ThrowingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) =>
            throw new InvalidOperationException("presentation context unavailable");
    }
}

public sealed class W1WorkspaceOwnershipTests
{
    [Fact]
    public void PersistenceStateIsOwnedByTheDedicatedPartial()
    {
        string sourceDirectory = Path.Combine(
            RepoRoot(),
            "apps",
            "slate-windows",
            "src",
            "SlateWindows");
        string primary = File.ReadAllText(
            Path.Combine(sourceDirectory, "WorkspaceViewModel.cs"));
        string persistence = File.ReadAllText(
            Path.Combine(sourceDirectory, "WorkspaceViewModel.Persistence.cs"));

        Assert.Contains(
            "internal sealed partial class WorkspaceViewModel",
            primary,
            StringComparison.Ordinal);
        foreach (string ownedMember in new[]
        {
            "WorkspacePersistence _persistence",
            "Func<IReadOnlyList<string>> _expandedDirectoryPaths",
            "bool _restoring",
            "int _persistenceBatchDepth",
            "bool _persistencePending",
            "private (WorkspacePaneNodeViewModel Root, WorkspaceGroupViewModel Active) Restore",
            "private void RunWorkspaceMutation",
            "private void PersistCore",
            "private static WorkspaceNodeState SnapshotNode",
        })
        {
            Assert.DoesNotContain(ownedMember, primary, StringComparison.Ordinal);
            Assert.Contains(ownedMember, persistence, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LayoutStateIsOwnedByTheDedicatedPartial()
    {
        string sourceDirectory = Path.Combine(
            RepoRoot(),
            "apps",
            "slate-windows",
            "src",
            "SlateWindows");
        string primary = File.ReadAllText(
            Path.Combine(sourceDirectory, "WorkspaceViewModel.cs"));
        string layout = File.ReadAllText(
            Path.Combine(sourceDirectory, "WorkspaceViewModel.Layout.cs"));

        foreach (string ownedMember in new[]
        {
            "record struct PaneRect",
            "const int ClosedTabCapacity",
            "List<(WorkspaceItemState Item, Guid Group)> _closedTabs",
            "WorkspacePaneNodeViewModel _root",
            "WorkspaceGroupViewModel _activeGroup",
            "private bool AddSplitWithItem",
            "public bool FocusDirectionalPane",
            "private void ResizeActivePane",
            "private static bool TryFindParent",
            "private void RaiseCommandStates",
        })
        {
            Assert.DoesNotContain(ownedMember, primary, StringComparison.Ordinal);
            Assert.Contains(ownedMember, layout, StringComparison.Ordinal);
        }
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
