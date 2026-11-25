// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Channels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-16 (#1791; Call 3, W9): the Files status line. "Loading
/// files…" and "Filtering files…" are transient lines: when the work that
/// wrote one ends with nothing of its own to say, the line says what it said
/// before, unless the tree wrote that line (its count, its overflow line, the
/// settings notice or a tree failure), which is stale by then and leaves the
/// line empty. A line written while the work ran wins, and a plain refresh
/// never writes a count. The facts use the shapes of step 0's probes P2a to
/// P2e, P3a and P3b.
/// </summary>
public sealed class FilesStatusLineTests
{
    private const string Loading = "Loading files…";
    private const string Filtering = "Filtering files…";
    private const string SortedZToA = "Sorted by name, Z to A.";
    private const string MalformedSettings = "{\"version\":1,\"sort\":\"bogus\"}";
    private const string MalformedReason = "Sidebar settings are malformed and are read-only.";

    /// <summary>Fact 19 (P2c, the issue's repro): New Canvas through the
    /// sidebar, its publication, then the change feed's plain refresh through
    /// the lifecycle's real listener. The line ends on the create's sentence,
    /// not on "Loading files…".</summary>
    [Fact]
    public void APlainRefreshPutsBackAMutationsLine() => RunSta(() =>
    {
        using RescanTests.Harness h = RescanTests.Harness.WithAsyncTree("files-line-canvas", ("alpha.md", "# Alpha\n"));
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        int handled = h.Lifecycle.FileChangesHandledForTests;
        string created = SlateUniffiMethods.A11yRender(
            new A11yEvent.Canvas(new CanvasA11yEvent.CanvasFileCreated("Untitled Canvas"))).Text;

        h.Sidebar.CreateCanvasCommand.Execute(null);
        h.Context.RunUntil(
            () => h.Sidebar.RootNodes.Any(node => node.Path.EndsWith(".canvas", StringComparison.Ordinal))
                && h.Lifecycle.FileChangesHandledForTests > handled
                && !h.Lifecycle.DebouncedSidebarRefreshPendingForTests
                && !h.Sidebar.IsRefreshingTree,
            "the create's publication and the feed's refresh");

        Assert.Equal(created, h.Sidebar.Status);
    });

    /// <summary>Fact 20: a line the tree wrote is stale once the tree moves,
    /// so a plain refresh leaves the line empty rather than put it back. The
    /// open's count, then a file added through Slate and the feed's refresh;
    /// and a failed refresh's "Could not load files.", then a refresh that
    /// succeeds. Both lines are read before either is asserted, so each half
    /// shows on its own.</summary>
    [Fact]
    public void ARefreshNeverPutsBackALineTheTreeWrote() => RunSta(() =>
    {
        using RescanTests.Harness h = RescanTests.Harness.WithAsyncTree("files-line-tree-wrote", ("alpha.md", "# Alpha\n"));
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        Assert.Equal("1 top-level items.", h.Sidebar.Status);

        int handled = h.Lifecycle.FileChangesHandledForTests;
        h.CreateInCore("feed.md", "# Feed\n");
        h.Context.RunUntil(
            () => h.Sidebar.RootNodes.Any(node => node.Path == "feed.md")
                && h.Lifecycle.FileChangesHandledForTests > handled
                && !h.Lifecycle.DebouncedSidebarRefreshPendingForTests
                && !h.Sidebar.IsRefreshingTree,
            "the feed's refresh");
        string afterTheFeed = h.Sidebar.Status;

        bool fail = true;
        h.TreeWorker = (work, token) => Volatile.Read(ref fail)
            ? Task.FromException(new IOException("injected tree refresh failure"))
            : Task.Run(work, token);
        h.Sidebar.Refresh();
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the failed refresh");
        Assert.Equal("Could not load files.", h.Sidebar.Status);
        Volatile.Write(ref fail, false);
        h.Sidebar.Refresh();
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the refresh after it");
        h.TreeWorker = null;
        string afterTheRetry = h.Sidebar.Status;

        Assert.Equal((string.Empty, string.Empty), (afterTheFeed, afterTheRetry));
    });

    /// <summary>The diff red team's finding 1: a tag tree's failure on a plain
    /// refresh is a line the tree wrote, so a later refresh whose tags load
    /// leaves the line empty rather than put the failure back.</summary>
    [Fact]
    public void ARefreshNeverPutsBackATagTreesFailure() => RunSta(() =>
    {
        using RescanTests.Harness h = RescanTests.Harness.WithAsyncTree("files-line-tag-failure", ("alpha.md", "# Alpha\n\n#topic\n"));
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        h.Sidebar.AfterTagTreeForTests = () => throw new VaultException.Io("injected tag tree failure");
        h.Sidebar.Refresh();
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the refresh whose tags fail");
        string failed = h.Sidebar.Status;
        Assert.False(string.IsNullOrEmpty(failed) || failed == Loading, $"premise: the tag tree's failure left the line \"{failed}\"");

        h.Sidebar.AfterTagTreeForTests = null;
        h.Sidebar.Refresh();
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the refresh whose tags load");

        Assert.Equal(string.Empty, h.Sidebar.Status);
    });

    /// <summary>The diff red team's finding 2: a folder expansion's overflow
    /// line is the tree's own, stale once the folder closes, so a plain refresh
    /// leaves the line empty rather than put it back.</summary>
    [Fact]
    [Trait("census", "moderate")]
    public void ARefreshNeverPutsBackAnExpansionsOverflowLine() => RunSta(() =>
    {
        var files = new List<(string Path, string Text)> { ("alpha.md", "# Alpha\n") };
        for (int index = 0; index < FilesSidebarViewModel.MaxMaterializedDirectoryItems + 1; index++)
        {
            files.Add(($"folder/note-{index:D5}.md", string.Empty));
        }

        using RescanTests.Harness h = RescanTests.Harness.WithAsyncTree("files-line-overflow", [.. files]);
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        FileTreeNodeViewModel folder = Assert.Single(h.Sidebar.RootNodes, node => node.Path == "folder");
        folder.IsExpanded = true;
        h.Context.Await(h.Sidebar.ChildExpansionCompletion, "the folder's expansion");
        Assert.Contains("Showing the first 5,000 items in folder", h.Sidebar.Status, StringComparison.Ordinal);

        folder.IsExpanded = false;
        h.Sidebar.Refresh();
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the plain refresh");

        Assert.Equal(string.Empty, h.Sidebar.Status);
    });

    /// <summary>Codex round 1: a folder whose children fail to load says so, and that line is
    /// the tree's own: once the folder is closed and a plain refresh succeeds, the line is
    /// empty, not the old failure.</summary>
    [Fact]
    public void ARefreshNeverPutsBackAFolderExpansionsFailure() => RunSta(() =>
    {
        using RescanTests.Harness h = RescanTests.Harness.WithAsyncTree(
            "files-line-expansion-failure",
            ("alpha.md", "# Alpha\n"),
            ("folder/inner.md", "# Inner\n"));
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        int reads = 0;
        h.TreeWorker = (work, token) => Interlocked.Increment(ref reads) == 1
            ? Task.FromException(new IOException("injected folder read failure"))
            : Task.Run(work, token);
        FileTreeNodeViewModel folder = Assert.Single(h.Sidebar.RootNodes, node => node.Path == "folder");
        folder.IsExpanded = true;
        h.Context.Await(h.Sidebar.ChildExpansionCompletion, "the folder's failed expansion");
        h.Settle();
        string failed = h.Sidebar.Status;
        Assert.False(string.IsNullOrEmpty(failed) || failed == Loading, $"premise: the failed expansion left the line \"{failed}\"");

        folder.IsExpanded = false;
        h.Sidebar.Refresh();
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the plain refresh");
        h.TreeWorker = null;

        Assert.Equal(string.Empty, h.Sidebar.Status);
    });

    /// <summary>Codex round 1: the dual pane's failed read of a folder is the tree's own line:
    /// after a healthy folder is chosen and a plain refresh succeeds, the line is empty, not
    /// the old failure.</summary>
    [Fact]
    public void ARefreshNeverPutsBackTheDualPanesFailure() => RunSta(() =>
    {
        using RescanTests.Harness h = RescanTests.Harness.WithAsyncTree(
            "files-line-dual-pane-failure",
            ("alpha.md", "# Alpha\n"),
            ("kept/inner.md", "# Inner\n"));
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        FileTreeNodeViewModel kept = Assert.Single(h.Sidebar.RootNodes, node => node.Path == "kept");
        // A folder row whose path core refuses to read, so the dual pane's read fails.
        var unreadable = new FileTreeNodeViewModel(h.Sidebar, "../outside", "outside", isDirectory: true, level: 1, hasChildren: false);
        h.Sidebar.SelectedNode = unreadable;
        h.Sidebar.IsDualPaneEnabled = true;
        h.Settle();
        string failed = h.Sidebar.Status;
        Assert.StartsWith("Could not load folder files", failed, StringComparison.Ordinal);

        h.Sidebar.SelectedNode = kept;
        h.Settle();
        h.Sidebar.Refresh();
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the plain refresh");

        Assert.Equal(string.Empty, h.Sidebar.Status);
    });

    /// <summary>Codex round 2: the dual pane's read fails while a refresh is still to
    /// publish, so its line is held for that publication; the reassertion keeps it the
    /// tree's own, and a later plain refresh leaves the line empty, not the old failure.
    /// </summary>
    [Fact]
    public void AHeldReadFailureStaysTheTreesOwn() => RunSta(() =>
    {
        using RescanTests.Harness h = RescanTests.Harness.WithAsyncTree(
            "files-line-held-read-failure",
            ("alpha.md", "# Alpha\n"),
            ("kept/inner.md", "# Inner\n"));
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        FileTreeNodeViewModel kept = Assert.Single(h.Sidebar.RootNodes, node => node.Path == "kept");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var parked = new ManualResetEventSlim(false);
        h.TreeWorker = async (work, token) =>
        {
            parked.Set();
            await release.Task.WaitAsync(token).ConfigureAwait(false);
            work();
        };
        h.Sidebar.Refresh();
        h.Context.RunUntil(() => parked.IsSet, "the refresh parking");

        var unreadable = new FileTreeNodeViewModel(h.Sidebar, "../outside", "outside", isDirectory: true, level: 1, hasChildren: false);
        h.Sidebar.SelectedNode = unreadable;
        h.Sidebar.IsDualPaneEnabled = true;
        Assert.StartsWith("Could not load folder files", h.Sidebar.Status, StringComparison.Ordinal);
        release.SetResult();
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the held refresh's publication");
        h.TreeWorker = null;
        Assert.StartsWith("Could not load folder files", h.Sidebar.Status, StringComparison.Ordinal);

        h.Sidebar.SelectedNode = kept;
        h.Settle();
        h.Sidebar.Refresh();
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the plain refresh");

        Assert.Equal(string.Empty, h.Sidebar.Status);
    });

    /// <summary>Fact 21 (P2e): a refresh cancelled with no successor (the
    /// close's path) puts back the line it replaced.</summary>
    [Fact]
    public void ACancelledRefreshPutsBackTheLine() => RunSta(() =>
    {
        using RescanTests.Harness h = SortedHarness("files-line-cancelled");
        using var parked = new ManualResetEventSlim(false);
        h.TreeWorker = async (work, token) =>
        {
            parked.Set();
            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
            work();
        };
        h.Sidebar.Refresh();
        h.Context.RunUntil(() => parked.IsSet, "the refresh parking");
        Assert.Equal(Loading, h.Sidebar.Status);

        Assert.True(h.Sidebar.CancelTreeRefresh());
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the cancel");
        h.TreeWorker = null;

        Assert.Equal(SortedZToA, h.Sidebar.Status);
    });

    /// <summary>Fact 21: the second of two refreshes in a row finds the line
    /// showing the first's "Loading files…" and keeps the first's memory, so
    /// its publication puts back the line from before the first.</summary>
    [Fact]
    public void TwoRefreshesInARowPutBackTheLineBeforeTheFirst() => RunSta(() =>
    {
        using RescanTests.Harness h = SortedHarness("files-line-two-refreshes");

        h.Sidebar.Refresh();
        h.Sidebar.Refresh();
        Assert.Equal(Loading, h.Sidebar.Status);
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the second refresh's publication");

        Assert.Equal(SortedZToA, h.Sidebar.Status);
    });

    /// <summary>Fact 22, a guard: a failure detail written between the
    /// refresh's request and its publication stays. A put-back acts only while
    /// the line still shows "Loading files…". Nothing before the refresh is
    /// asserted, so an end that ignored the line fails here and not in the
    /// setup.</summary>
    [Fact]
    public void ALineWrittenWhileTheRefreshRanWins() => RunSta(() =>
    {
        using RescanTests.Harness h = RescanTests.Harness.WithAsyncTree("files-line-written-while", ("alpha.md", "# Alpha\n"));
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var parked = new ManualResetEventSlim(false);
        h.TreeWorker = async (work, token) =>
        {
            parked.Set();
            await release.Task.WaitAsync(token).ConfigureAwait(false);
            work();
        };
        h.Sidebar.Refresh();
        h.Context.RunUntil(() => parked.IsSet, "the refresh parking");
        Assert.Equal(Loading, h.Sidebar.Status);

        // The canvas create's failure route, while the refresh is parked.
        A11yEvent failure = FilesSidebarViewModel.CanvasCreateFailure("Untitled Canvas.canvas: no free name");
        h.Sidebar.ReportFailure(failure);
        release.SetResult();
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the publication");
        h.TreeWorker = null;

        Assert.Equal(SlateUniffiMethods.A11yRender(failure).Text, h.Sidebar.Status);
    });

    /// <summary>Fact 24 (drift 5): with the settings notice up, a refresh that
    /// counts writes the count with the notice, and a plain refresh writes the
    /// notice alone.</summary>
    [Fact]
    public void APlainRefreshWithASettingsNoticeWritesNoCount() => RunSta(() =>
    {
        using RescanTests.Harness h = RescanTests.Harness.WithAsyncTree(
            "files-line-notice",
            ("alpha.md", "# Alpha\n"),
            (".slate/sidebar.json", MalformedSettings));
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
        Assert.Equal(MalformedReason, h.Sidebar.SettingsNotice);
        Assert.Equal($"1 top-level items. {MalformedReason}", h.Sidebar.Status);

        h.Sidebar.Refresh();
        h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the plain refresh");

        Assert.Equal(MalformedReason, h.Sidebar.Status);
    });

    /// <summary>Fact 25 (P3a): the close's admission cancels a filter run and
    /// is refused; the line puts back what "Filtering files…" replaced, whether
    /// the run it cancelled is the user's or a publication's automatic run that
    /// superseded the user's. The filter is not run again (the red team's M2).
    /// The tree here is synchronous, as P3a's is, so a plain publication
    /// writes no "Loading files…" of its own.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AFilterTheCloseCancelledPutsBackItsLine(bool anAutomaticRunSupersededIt)
    {
        using FixtureVault fixture = FixtureVault.Create(2, "files-line-filter-close");
        var workerCalls = Channel.CreateUnbounded<bool>();
        Task HeldFilterWorker(Action work, CancellationToken token)
        {
            Assert.True(workerCalls.Writer.TryWrite(true));
            // Held until the run is cancelled: the query never runs.
            return Task.Delay(Timeout.InfiniteTimeSpan, token);
        }

        try
        {
            using var lifecycle = new VaultLifecycleViewModel(
                pickVault: () => Task.FromResult<string?>(fixture.Root),
                enqueueUi: action => action(),
                recentVaultsStore: new RecentVaultsStore(Path.Combine(fixture.Root + "-device", "recent-vaults.json")),
                filterUiContext: new ThreadPoolContext(),
                filterWorker: HeldFilterWorker);
            await lifecycle.OpenVaultAsync(fixture.Root);
            FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
            sidebar.SortMode = SidebarSortMode.NameDescending;
            Assert.Equal(SortedZToA, sidebar.Status);

            sidebar.FilterText = "note";
            _ = await workerCalls.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            if (anAutomaticRunSupersededIt)
            {
                // A plain publication refilters: its automatic run cancels the
                // user's and takes its place.
                sidebar.Refresh();
                _ = await workerCalls.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            }

            Assert.Equal(Filtering, sidebar.Status);

            Assert.False(lifecycle.PrepareForApplicationClose());
            Assert.Equal(
                "File filter cancellation requested. Close the vault again after the current query finishes.",
                lifecycle.StatusText);
            await sidebar.FilterCompletion.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(SortedZToA, sidebar.Status);
        }
        finally
        {
            DeleteQuietly(fixture.Root + "-device");
        }
    }

    /// <summary>Fact 25 (P3b): a field set to spaces while a filter run waits
    /// ends the filter. The line goes back to what it said before the run, and
    /// nothing is spoken: a field that starts with a space is typing (the
    /// FilterText setter's own rule).</summary>
    [Fact]
    public async Task AFieldOfSpacesEndsTheFilterLineSilently()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "files-line-filter-space");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }

        var delays = Channel.CreateUnbounded<TaskCompletionSource>();
        Task Delay(CancellationToken token)
        {
            var delay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Assert.True(delays.Writer.TryWrite(delay));
            return delay.Task.WaitAsync(token);
        }

        var said = new List<A11yEvent>();
        var sidebar = new FilesSidebarViewModel(
            session,
            said.Add,
            localAppDataRoot: DeviceStateForTests.FreshFileRecentsRoot(),
            filterUiContext: new ThreadPoolContext(),
            filterDelay: Delay,
            filterWorker: (work, token) =>
            {
                token.ThrowIfCancellationRequested();
                work();
                return Task.CompletedTask;
            });
        sidebar.SortMode = SidebarSortMode.NameDescending;
        Assert.Equal(SortedZToA, sidebar.Status);
        said.Clear();

        sidebar.FilterText = "note";
        _ = await delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(Filtering, sidebar.Status);

        sidebar.FilterText = " ";
        await sidebar.FilterCompletion.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(sidebar.IsFilterActive);
        Assert.Equal(SortedZToA, sidebar.Status);
        Assert.Empty(said);
    }

    /// <summary>A harness on the asynchronous tree whose line is a mutation's
    /// own sentence (a sort's), settled: the line a transient puts back.</summary>
    private static RescanTests.Harness SortedHarness(string label)
    {
        RescanTests.Harness h = RescanTests.Harness.WithAsyncTree(
            label,
            ("alpha.md", "# Alpha\n"),
            ("beta.md", "# Beta\n"));
        try
        {
            h.Context.RunUntil(() => !h.Sidebar.IsRefreshingTree, "the open's tree");
            h.Sidebar.SortMode = SidebarSortMode.NameDescending;
            h.Context.RunUntil(
                () => !h.Sidebar.IsRefreshingTree && !h.Lifecycle.DebouncedSidebarRefreshPendingForTests,
                "the sort's refresh");
            Assert.Equal(SortedZToA, h.Sidebar.Status);
            return h;
        }
        catch
        {
            h.Dispose();
            throw;
        }
    }

    private static void DeleteQuietly(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(120), "STA test body timed out.");

    /// <summary>The filter's publication context for a headless sidebar, as
    /// P3a and P3b ran it: each post on the thread pool.</summary>
    private sealed class ThreadPoolContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) =>
            ThreadPool.QueueUserWorkItem(_ => d(state));
    }
}
