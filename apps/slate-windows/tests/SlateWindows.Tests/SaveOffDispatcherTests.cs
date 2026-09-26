// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1280 (locked decision 05 §4.1): a save's synchronous core write runs
/// on a worker, never on the dispatcher. The editor state is snapshotted on
/// the dispatcher, the write runs under the tab's save chain, and the
/// outcome — state, status and exactly one D-10 announcement — is published
/// back on the dispatcher. The explicit Save, Save All and save-before-close
/// wait for it in a nested dispatcher frame, so input, focus, the status
/// and notifications keep flowing while the file and index work runs.
/// </summary>
public sealed class SaveOffDispatcherTests
{
    /// <summary>A save whose worker is parked inside the write still lets
    /// the dispatcher run queued work — here the very operation that
    /// releases it, which could never run if the write held the dispatcher
    /// — and the write ran on another thread.</summary>
    [Theory]
    [InlineData("save")]
    [InlineData("save-all")]
    [InlineData("close")]
    public void AParkedSaveWorkerKeepsTheDispatcherPumping(string action)
    {
        using var host = new Host();
        WorkspaceTabViewModel tab = host.OpenNote();
        tab.Text += "\nPumped edit.";
        int dispatcherThread = Environment.CurrentManagedThreadId;
        int writerThread = dispatcherThread;
        using var parked = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        tab.SaveWriteHookForTests = () =>
        {
            writerThread = Environment.CurrentManagedThreadId;
            parked.Set();
            release.Wait(TimeSpan.FromSeconds(20));
        };
        bool ranWhileParked = false;
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                ranWhileParked = parked.Wait(TimeSpan.FromSeconds(10)) && !release.IsSet;
                release.Set();
            }));

        Act(host, tab, action);

        Assert.True(ranWhileParked, "the dispatcher did not run queued work while the write was parked");
        Assert.NotEqual(dispatcherThread, writerThread);
        Assert.False(tab.IsDirty);
        Assert.EndsWith("Pumped edit.", File.ReadAllText(host.NotePath), StringComparison.Ordinal);
        Assert.DoesNotContain(
            host.Announced,
            item => item.Event is A11yEvent.NoteSaveBlocked or A11yEvent.NoteSaveConflict);
    }

    /// <summary>Two saves in quick succession — the second pressed while
    /// the first's write is still parked, after another edit — each publish
    /// exactly once, in order: the first confirmation lands with the first
    /// write on disk, the second with the second, and nothing is announced
    /// out of order however the nested frames unwind.</summary>
    [Fact]
    public void TwoQuickSavesPublishOnceEachInOrder()
    {
        using var host = new Host();
        WorkspaceTabViewModel tab = host.OpenNote();
        tab.Text += "\nFirst edit.";
        int writes = 0;
        using var parked = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        tab.SaveWriteHookForTests = () =>
        {
            if (Interlocked.Increment(ref writes) == 1)
            {
                parked.Set();
                release.Wait(TimeSpan.FromSeconds(20));
            }
        };
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                Assert.True(parked.Wait(TimeSpan.FromSeconds(10)));
                tab.Text += "\nSecond edit.";
                // Released only once the second save is waiting behind it.
                dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(release.Set));
                host.Workspace.SaveActiveCommand.Execute(null);
            }));

        host.Workspace.SaveActiveCommand.Execute(null);

        Assert.Equal(2, writes);
        (A11yEvent Event, int Thread, string Disk)[] saved = host.Announced
            .Where(item => item.Event is A11yEvent.NoteSaved)
            .ToArray();
        Assert.Equal(2, saved.Length);
        Assert.EndsWith("First edit.", saved[0].Disk, StringComparison.Ordinal);
        Assert.EndsWith("Second edit.", saved[1].Disk, StringComparison.Ordinal);
        Assert.All(saved, item => Assert.Equal(host.DispatcherThread, item.Thread));
        Assert.Equal(2, host.Announced.Count);
        Assert.False(tab.IsDirty);
        Assert.EndsWith("Second edit.", File.ReadAllText(host.NotePath), StringComparison.Ordinal);
    }

    /// <summary>Typing while the write runs — now possible, since the
    /// dispatcher pumps — lands on disk as the snapshot, not the newer
    /// text: the tab says it saved, stays dirty with the edit kept, and
    /// undoing back to the saved text makes it clean again.</summary>
    [Fact]
    public void AnEditDuringTheWriteKeepsTheTabDirty()
    {
        using var host = new Host();
        WorkspaceTabViewModel tab = host.OpenNote();
        tab.Text += "\nSaved edit.";
        string saved = tab.Text;
        using var parked = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        tab.SaveWriteHookForTests = () =>
        {
            parked.Set();
            release.Wait(TimeSpan.FromSeconds(20));
        };
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                Assert.True(parked.Wait(TimeSpan.FromSeconds(10)));
                tab.EditorDocument!.Insert(tab.EditorDocument.TextLength, "\nTyped meanwhile.");
                release.Set();
            }));

        Assert.True(tab.Save());

        Assert.Equal(saved, File.ReadAllText(host.NotePath));
        Assert.EndsWith("Typed meanwhile.", tab.Text, StringComparison.Ordinal);
        Assert.True(tab.IsDirty);
        Assert.Equal("Saved note0.md.", tab.Status);

        tab.EditorDocument!.UndoStack.Undo();
        Assert.Equal(saved, tab.Text);
        Assert.False(tab.IsDirty);
    }

    /// <summary>R-7 through the moved write: a note changed externally
    /// while its save's write is parked refuses with core's conflict
    /// sentence exactly once, published on the dispatcher, with the status
    /// equal to it — through each save entry point.</summary>
    [Theory]
    [InlineData("save")]
    [InlineData("save-all")]
    [InlineData("close")]
    public void AConflictDuringTheWriteSpeaksOnceOnTheDispatcher(string action)
    {
        using var host = new Host();
        WorkspaceTabViewModel tab = host.OpenNote();
        tab.Text += "\nLocal edit.";
        string local = tab.Text;
        const string external = "# Changed while saving\n";
        using var parked = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        tab.SaveWriteHookForTests = () =>
        {
            parked.Set();
            release.Wait(TimeSpan.FromSeconds(20));
        };
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                Assert.True(parked.Wait(TimeSpan.FromSeconds(10)));
                File.WriteAllText(host.NotePath, external);
                release.Set();
            }));

        Act(host, tab, action);

        var conflict = Assert.Single(host.Announced);
        var sentence = Assert.IsType<A11yEvent.NoteSaveConflict>(conflict.Event);
        Assert.Equal("note0.md", sentence.Filename);
        Assert.Equal(host.DispatcherThread, conflict.Thread);
        Assert.Equal(SlateUniffiMethods.A11yRender(sentence).Text, tab.Status);
        Assert.Equal(external, File.ReadAllText(host.NotePath));
        Assert.Equal(local, tab.Text);
        Assert.True(tab.IsDirty);
    }

    private static void Act(Host host, WorkspaceTabViewModel tab, string action)
    {
        switch (action)
        {
            case "save": host.Workspace.SaveActiveCommand.Execute(null); break;
            case "save-all": host.Workspace.SaveAll(); break;
            case "close": host.Workspace.CloseTabCommand.Execute(tab); break;
            default: throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    private sealed class Host : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(1, "save-off-dispatcher");
        private readonly VaultSession _session;

        internal Host()
        {
            DispatcherThread = Environment.CurrentManagedThreadId;
            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using var cancel = new CancelToken();
            _session.ScanInitial(cancel);
            Workspace = new WorkspaceViewModel(
                _session,
                _fixture.Root,
                () => [],
                item => Announced.Add((item, Environment.CurrentManagedThreadId, File.ReadAllText(NotePath))),
                dirtyCloseDecision: _ => WorkspaceDirtyNavigationDecision.Save,
                startInteractionBackgroundWork: false);
        }

        internal int DispatcherThread { get; }
        internal List<(A11yEvent Event, int Thread, string Disk)> Announced { get; } = [];
        internal WorkspaceViewModel Workspace { get; }
        internal string NotePath => Path.Combine(_fixture.Root, "note0.md");

        internal WorkspaceTabViewModel OpenNote()
        {
            Workspace.OpenPath("note0.md");
            WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(Workspace.ActiveGroup.ActiveTab);
            Announced.Clear();
            return tab;
        }

        public void Dispose()
        {
            Workspace.Dispose();
            _session.Dispose();
            _fixture.Dispose();
        }
    }
}
