// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using SlateWindows;
using SlateWindows.FileManagement;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class MoveToLoadingTests
{
    [Fact]
    public async Task BlockedDestinationReadReturnsImmediatelyAndCancelPreventsPublication()
    {
        using var rig = await Rig.Create();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        rig.Sidebar.BeforeMoveToPageForTesting = (_, _) =>
        {
            started.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
        };
        rig.Sidebar.OpenMoveTo();
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(rig.Sidebar.MoveToSheet);
        Assert.Equal(MoveToLoadState.Loading, picker.State);
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        try
        {
            picker.FilterText = "fresh";
            Assert.DoesNotContain(picker.Rows, row => row.Kind == MoveToRowKind.NewFolder);
            picker.ActivateCommand.Execute(new MoveToRowViewModel(MoveToRowKind.Folder, "dest", "dest"));
            Assert.True(File.Exists(Path.Combine(rig.Root, "a.md")));
            picker.CancelCommand.Execute(null);
            Assert.Null(rig.Sidebar.MoveToSheet);
        }
        finally { release.Set(); }
        await rig.Drain(rig.Sidebar.MoveToCompletion);
        Assert.Null(rig.Sidebar.MoveToSheet);
        Assert.DoesNotContain(rig.Announcements, item => Render(item).Contains("loaded.", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(rig.Root, "fresh")));
    }

    [Fact]
    public async Task RefusedAdmissionStartsNoWorkerAndShutdownRejectsAnOldCommand()
    {
        using var rig = await Rig.Create();
        int pages = 0;
        rig.Sidebar.BeforeMoveToPageForTesting = (_, _) => pages++;
        rig.Sidebar.MoveToOpenAdmission = () => false;
        rig.Sidebar.OpenMoveTo();
        Assert.Null(rig.Sidebar.MoveToSheet);
        Assert.Equal(0, pages);
        rig.Sidebar.MoveToOpenAdmission = () => true;
        rig.Sidebar.BeginSessionShutdownAndCaptureWork();
        rig.Sidebar.MoveToCommand.Execute(null);
        Assert.Null(rig.Sidebar.MoveToSheet);
        Assert.Equal(0, pages);
    }

    [Fact]
    public async Task FailureRetainsTheSheetAndFilterAndRetryRequiresTheCurrentOwner()
    {
        using var rig = await Rig.Create();
        rig.Sidebar.BeforeMoveToPageForTesting = (_, _) => throw new IOException("private path");
        rig.Sidebar.OpenMoveTo();
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(rig.Sidebar.MoveToSheet);
        picker.FilterText = "de";
        await rig.Drain(rig.Sidebar.MoveToCompletion);
        Assert.Same(picker, rig.Sidebar.MoveToSheet);
        Assert.Equal(MoveToLoadState.Failed, picker.State);
        Assert.True(picker.CanRetry);
        Assert.DoesNotContain("private path", picker.Status, StringComparison.Ordinal);
        rig.Sidebar.BeforeMoveToPageForTesting = null;
        picker.RetryCommand.Execute(null);
        await rig.Drain(rig.Sidebar.MoveToCompletion);
        Assert.Equal("de", picker.FilterText);
        Assert.Equal(MoveToLoadState.Ready, picker.State);
        Assert.Contains(picker.Rows, row => row.Destination == "dest");
        rig.Sidebar.MoveToOwnsModal = () => false;
        picker.ActivateCommand.Execute(picker.Rows.First());
        Assert.True(File.Exists(Path.Combine(rig.Root, "a.md")));
        Assert.Single(rig.Announcements, item => Render(item).StartsWith("Could not load destination", StringComparison.Ordinal));
        Assert.Single(rig.Announcements, item => Render(item) == "Destination folders loaded.");
    }

    [Fact]
    public async Task AQueuedOldPublicationCannotChangeTheReplacementPicker()
    {
        using var rig = await Rig.Create();
        rig.Sidebar.OpenMoveTo();
        MoveToPickerViewModel first = Assert.IsType<MoveToPickerViewModel>(rig.Sidebar.MoveToSheet);
        await rig.WaitForPost();
        first.CancelCommand.Execute(null);
        rig.Sidebar.OpenMoveTo();
        MoveToPickerViewModel second = Assert.IsType<MoveToPickerViewModel>(rig.Sidebar.MoveToSheet);
        second.FilterText = "de";
        await rig.Drain(rig.Sidebar.MoveToCompletion);
        Assert.Same(second, rig.Sidebar.MoveToSheet);
        Assert.Equal("de", second.FilterText);
        Assert.Equal(MoveToLoadState.Loading, first.State);
        first.ActivateCommand.Execute(new MoveToRowViewModel(MoveToRowKind.Folder, "dest", "dest"));
        first.CancelCommand.Execute(null);
        Assert.Same(second, rig.Sidebar.MoveToSheet);
        Assert.Single(rig.Announcements, item => Render(item) == "Destination folders loaded.");
    }

    [Fact]
    public async Task ShutdownDrainsNativeWorkWithoutPumpingQueuedPublications()
    {
        using var rig = await Rig.Create();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        rig.Sidebar.BeforeMoveToPageForTesting = (_, _) =>
        {
            started.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
        };
        rig.Sidebar.OpenMoveTo();
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        SidebarSessionShutdown shutdown = rig.Sidebar.BeginSessionShutdownAndCaptureWork();
        Assert.False(shutdown.SessionWork.IsCompleted);
        release.Set();
        await shutdown.SessionWork.WaitAsync(TimeSpan.FromSeconds(5));
        await rig.Sidebar.MoveToCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        rig.Context.Drain();
        Assert.DoesNotContain(rig.Announcements, item => Render(item) == "Destination folders loaded.");
    }

    [Fact]
    public async Task ShutdownCancelsAPostedPageWithoutWaitingForItsDispatcher()
    {
        using var rig = await Rig.Create();
        rig.Sidebar.OpenMoveTo();
        await rig.WaitForPost();
        SidebarSessionShutdown shutdown = rig.Sidebar.BeginSessionShutdownAndCaptureWork();
        await shutdown.SessionWork.WaitAsync(TimeSpan.FromSeconds(5));
        await rig.Sidebar.MoveToCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        rig.Context.Drain();
        Assert.DoesNotContain(rig.Announcements, item => Render(item) == "Destination folders loaded.");
    }

    [Fact]
    public async Task BatchExecutionUsesTheSourcesCapturedBeforeLoading()
    {
        using var rig = await Rig.Create();
        FileTreeNodeViewModel a = rig.Sidebar.RootNodes.Single(node => node.Path == "a.md");
        FileTreeNodeViewModel b = rig.Sidebar.RootNodes.Single(node => node.Path == "b.md");
        a.IsBatchSelected = true;
        b.IsBatchSelected = true;
        rig.Sidebar.OpenMoveTo();
        a.IsBatchSelected = false;
        b.IsBatchSelected = false;
        rig.Sidebar.RootNodes.Single(node => node.Path == "other.md").IsBatchSelected = true;
        await rig.Drain(rig.Sidebar.MoveToCompletion);
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(rig.Sidebar.MoveToSheet);
        picker.ActivateCommand.Execute(picker.Rows.Single(row => row.Destination == "dest"));
        await rig.Drain(rig.Sidebar.TreeRefreshCompletion);
        Assert.True(File.Exists(Path.Combine(rig.Root, "dest", "a.md")));
        Assert.True(File.Exists(Path.Combine(rig.Root, "dest", "b.md")));
        Assert.True(File.Exists(Path.Combine(rig.Root, "other.md")));
    }

    [Fact]
    public async Task DestinationWalkContinuesBeyondTheFirstThousandRowPage()
    {
        using var rig = await Rig.Create(extraFolders: 1_005);
        var rootCursors = new ConcurrentQueue<string?>();
        rig.Sidebar.BeforeMoveToPageForTesting = (parent, cursor) =>
        {
            if (parent.Length == 0) { rootCursors.Enqueue(cursor); }
        };
        rig.Sidebar.OpenMoveTo();
        await rig.Drain(rig.Sidebar.MoveToCompletion);
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(rig.Sidebar.MoveToSheet);
        Assert.Equal(MoveToLoadState.Ready, picker.State);
        Assert.Equal(1_006, picker.Rows.Count(row => row.Kind == MoveToRowKind.Folder));
        Assert.Equal(2, rootCursors.Count);
        Assert.Contains(rootCursors, cursor => cursor is not null);
        Assert.Contains(picker.Rows, row => row.Destination == "folder-1004");
    }

    [Fact]
    public void IncrementalRowsPreserveFilterAndSelectionWithoutAnnouncingEachPage()
    {
        var announcements = new List<A11yEvent>();
        var picker = new MoveToPickerViewModel([], true, "a.md", _ => { }, _ => { },
            () => { }, _ => true, announcements.Add, loading: true);
        picker.FilterText = "match";
        picker.PublishFolders(["match-b"], complete: false, truncated: false);
        picker.SelectedRow = picker.Rows.Single(row => row.Destination == "match-b");
        announcements.Clear();
        picker.PublishFolders(["match-a", "match-b", "unrelated"], complete: false, truncated: false);
        Assert.Equal("match", picker.FilterText);
        Assert.Equal("match-b", picker.SelectedRow?.Destination);
        Assert.Empty(announcements);
        Assert.False(picker.ActivateCommand.CanExecute(picker.SelectedRow));
        Assert.DoesNotContain(picker.Rows, row => row.Kind == MoveToRowKind.NewFolder);
        picker.PublishFolders(["match-a", "match-b"], complete: true, truncated: true);
        Assert.True(picker.IsReady);
        Assert.Contains("50,000", picker.Status, StringComparison.Ordinal);
        Assert.DoesNotContain(picker.Rows, row => row.Kind == MoveToRowKind.NewFolder);
    }

    private static string Render(A11yEvent item) => SlateUniffiMethods.A11yRender(item).Text;

    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> _pending = new();
        public bool HasPosts => !_pending.IsEmpty;
        public override void Post(SendOrPostCallback callback, object? state) => _pending.Enqueue(() => callback(state));
        public void Drain() { while (_pending.TryDequeue(out Action? action)) { action(); } }
    }

    private sealed class Rig : IDisposable
    {
        private readonly FixtureVault _fixture;
        private readonly VaultSession _session;
        private Rig(FixtureVault fixture, VaultSession session, QueuedContext context,
            FilesSidebarViewModel sidebar, ConcurrentQueue<A11yEvent> announcements)
        {
            _fixture = fixture;
            _session = session;
            Context = context;
            Sidebar = sidebar;
            Announcements = announcements;
        }
        public string Root => _fixture.Root;
        public QueuedContext Context { get; }
        public FilesSidebarViewModel Sidebar { get; }
        public ConcurrentQueue<A11yEvent> Announcements { get; }
        public static async Task<Rig> Create(int extraFolders = 0)
        {
            FixtureVault fixture = FixtureVault.Create(0, "move-to-loading");
            foreach (string name in new[] { "a.md", "b.md", "other.md" })
            {
                File.WriteAllText(Path.Combine(fixture.Root, name), name);
            }
            Directory.CreateDirectory(Path.Combine(fixture.Root, "dest"));
            for (int index = 0; index < extraFolders; index++)
            {
                Directory.CreateDirectory(Path.Combine(fixture.Root, $"folder-{index:D4}"));
            }
            VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);
            var context = new QueuedContext();
            var announcements = new ConcurrentQueue<A11yEvent>();
            var sidebar = new FilesSidebarViewModel(session, announcements.Enqueue, treeUiContext: context);
            var rig = new Rig(fixture, session, context, sidebar, announcements);
            await rig.Drain(sidebar.TreeRefreshCompletion);
            sidebar.SelectedNode = sidebar.RootNodes.Single(node => node.Path == "a.md");
            return rig;
        }
        public async Task WaitForPost()
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!Context.HasPosts) { deadline.Token.ThrowIfCancellationRequested(); await Task.Delay(1); }
        }
        public async Task Drain(Task completion)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (!completion.IsCompleted)
            {
                deadline.Token.ThrowIfCancellationRequested();
                Context.Drain();
                await Task.Delay(1);
            }
            Context.Drain();
            await completion;
        }
        public void Dispose()
        {
            SidebarSessionShutdown shutdown = Sidebar.BeginSessionShutdownAndCaptureWork();
            shutdown.SessionWork.GetAwaiter().GetResult();
            _session.Dispose();
            _fixture.Dispose();
        }
    }
}
