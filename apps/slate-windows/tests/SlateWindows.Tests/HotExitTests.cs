// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Threading;
using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 44 WB-29: unsaved notes survive closing, quitting and a crash on
/// Windows. Each fact runs a real vault through the real lifecycle over its own
/// store host on a temp device root (the real core store). A store call is held
/// through <see cref="HotExitStoreHost.BeforeCallForTests"/>. "A new process" is
/// the old host's simulated death then a fresh host; a restore assertion also
/// proves the new open held the vault and read it (<see cref="AssertRead"/>), so
/// a refused or inert open can never pass for an empty store.
/// </summary>
public sealed class HotExitTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    [Fact]
    public void ADirtyTabClaimsAndWritesAtOnce()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        var calls = new List<HotExitCallKind>();
        host.Store.BeforeCallForTests = call =>
        {
            lock (calls)
            {
                calls.Add(call.Kind);
            }
            return null;
        };

        tab.Text += "typed";

        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "the first edit was never kept");
        lock (calls)
        {
            Assert.Equal(HotExitCallKind.Claim, calls[0]);
            Assert.Equal(HotExitCallKind.Write, calls[1]);
        }
    }

    [Fact]
    public void AChangedSnapshotIsWrittenAfterTheDebounce()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "one";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "the first edit was never kept");

        tab.Text += "two";
        host.PumpFor(TimeSpan.FromMilliseconds(200));
        Assert.DoesNotContain("two", host.StoredText(tab), StringComparison.Ordinal);
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "the debounce never wrote");
        HotExitEntry entry = host.StoredEntry(tab)!;
        Assert.Equal("a.md", entry.Path);
        Assert.Equal(tab.SavedContentHash, entry.BaselineHash);
        Assert.Equal(tab.FileIdentity, entry.BaselineFileId);
        Assert.NotNull(entry.BaselineFileId);
    }

    [Fact]
    public void ACleanTabRetiresItsEntry()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");

        Assert.True(tab.Save());
        host.PumpUntil(() => !tab.HasPendingSaves, "the save never settled");

        host.PumpUntil(() => host.StoredEntry(tab) is null, "the clean tab kept its entry");
        Assert.Empty(host.Snapshot().Claims);
    }

    [Theory]
    [InlineData("ApplicationClose")]
    [InlineData("Switch")]
    public void AKeptCloseClosesWithoutAPrompt(string routeName)
    {
        var route = Enum.Parse<TeardownRoute>(routeName);
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        // Typed after any debounce could have run.
        tab.Text += "kept";
        string final = tab.Text;

        Assert.Equal(WorkspaceTeardown.ClosedKept, host.Close(route));

        Assert.Equal(0, host.ClosePrompts);
        Assert.Equal("# a\n", host.Disk("a.md"));
        host.Crash();
        host.Open();
        host.AssertRead();
        WorkspaceTabViewModel restored = host.TabOn("a.md");
        Assert.True(restored.IsDirty);
        Assert.Equal(final, restored.Text);
        Assert.Equal("# a\n", host.Disk("a.md"));
        Assert.Single(host.Announced, item => item is A11yEvent.UnsavedNotesRestored { Count: 1 });
    }

    [Fact]
    public void ASettleThatCannotKeepFallsBackToThePromptWithItsReason()
    {
        using var host = new HotExitHost { ClosePrompt = () => VaultCloseDecision.Cancel };
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        host.Store.BeforeCallForTests = call =>
            call.Kind == HotExitCallKind.Write ? new HotExitFault.Refused(HotExitRefusal.VaultFull) : null;
        tab.Text += "typed";

        Assert.Equal(WorkspaceTeardown.Refused, host.Close(TeardownRoute.ApplicationClose));

        Assert.Equal(1, host.ClosePrompts);
        A11yEvent.UnsavedNotesNotKept notKept =
            Assert.IsType<A11yEvent.UnsavedNotesNotKept>(
                host.Announced.Single(item => item is A11yEvent.UnsavedNotesNotKept));
        Assert.Equal(SlateUniffiMethods.HotExitRefusalReason(HotExitRefusal.VaultFull), notKept.Reason);
        Assert.Contains(notKept.Reason, host.Lifecycle.StatusText, StringComparison.Ordinal);
        Assert.Equal("# a\ntyped", tab.Text);
    }

    [Fact]
    public void CloseVaultStillAsksAndDiscardAllFencesTheStore()
    {
        using var host = new HotExitHost { ClosePrompt = () => VaultCloseDecision.Discard };
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");

        Assert.Equal(WorkspaceTeardown.ClosedChangesDiscarded, host.Close(TeardownRoute.Close));

        Assert.Equal(1, host.ClosePrompts);
        host.Crash();
        host.Open();
        host.AssertRead();
        Assert.False(host.TabOn("a.md").IsDirty);
        Assert.DoesNotContain(host.Announced, item => item is A11yEvent.UnsavedNotesRestored);
    }

    [Theory]
    [InlineData("Cancel")]
    [InlineData("CloseKeepingCopies")]
    [InlineData("TryAgain")]
    public void AnUnconfirmedRetirementOpensTheDiscardPendingPrompt(string answerName)
    {
        var answer = Enum.Parse<DiscardPendingDecision>(answerName);
        using var host = new HotExitHost();
        host.ShortenBudgets();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");
        using var gate = new ManualResetEventSlim();
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind == HotExitCallKind.Retire)
            {
                Assert.True(gate.Wait(Patience), "the retirement was never released");
            }
            return null;
        };
        Assert.True(tab.Save());
        host.PumpUntil(() => !tab.HasPendingSaves, "the save never settled");
        host.DiscardPrompt = () =>
        {
            if (answer == DiscardPendingDecision.TryAgain)
            {
                gate.Set();
            }
            return answer;
        };

        WorkspaceTeardown outcome = host.Close(TeardownRoute.ApplicationClose);

        Assert.Equal(1, host.DiscardPrompts);
        switch (answer)
        {
            case DiscardPendingDecision.Cancel:
                Assert.Equal(WorkspaceTeardown.Refused, outcome);
                gate.Set();
                break;
            case DiscardPendingDecision.TryAgain:
                Assert.Equal(WorkspaceTeardown.Closed, outcome);
                host.Crash();
                host.Open();
                host.AssertRead();
                Assert.False(host.TabOn("a.md").IsDirty);
                break;
            case DiscardPendingDecision.CloseKeepingCopies:
                Assert.Equal(WorkspaceTeardown.ClosedCopiesKept, outcome);
                host.NewProcess();
                gate.Set();
                host.Open();
                host.AssertRead();
                Assert.True(host.TabOn("a.md").IsDirty, "the copy did not come back");
                break;
        }
    }

    [Fact]
    public void CloseAndKeepTheCopiesAfterAFailedDiscard()
    {
        using var host = new HotExitHost
        {
            ClosePrompt = () => VaultCloseDecision.Discard,
            DiscardPrompt = () => DiscardPendingDecision.CloseKeepingCopies,
        };
        host.ShortenBudgets();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        string final = tab.Text;
        host.PumpUntil(() => host.StoredText(tab) == final, "never kept");
        using var gate = new ManualResetEventSlim();
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind == HotExitCallKind.Discard)
            {
                return new HotExitFault.Io();
            }
            if (call.Kind == HotExitCallKind.Retire)
            {
                Assert.True(gate.Wait(Patience), "the retirement was never released");
            }
            return null;
        };

        Assert.Equal(WorkspaceTeardown.ClosedCopiesKept, host.Close(TeardownRoute.Close));

        Assert.Equal(1, host.DiscardPrompts);
        host.NewProcess();
        gate.Set();
        host.Open();
        host.AssertRead();
        WorkspaceTabViewModel restored = host.TabOn("a.md");
        Assert.True(restored.IsDirty);
        Assert.Equal(final, restored.Text);
    }

    [Theory]
    [InlineData("ApplicationClose")]
    [InlineData("Switch")]
    public void ALateDiscardNeverReKeepsTheTextItDiscarded(string routeName)
    {
        var route = Enum.Parse<TeardownRoute>(routeName);
        using var host = new HotExitHost
        {
            ClosePrompt = () => VaultCloseDecision.Discard,
            DiscardPrompt = () => DiscardPendingDecision.Cancel,
        };
        host.ShortenBudgets();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");
        using var gate = new ManualResetEventSlim();
        // A close asks only when the settle cannot keep: the store refuses the
        // next write, so the fallback prompt's Discard All runs, and is held.
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind == HotExitCallKind.Write)
            {
                return new HotExitFault.Refused(HotExitRefusal.VaultFull);
            }
            if (call.Kind == HotExitCallKind.Discard)
            {
                Assert.True(gate.Wait(Patience), "the discard was never released");
            }
            return null;
        };
        tab.Text += "more";
        Assert.Equal(WorkspaceTeardown.Refused, host.Close(route));
        int asked = host.ClosePrompts;
        Assert.Equal(1, asked);
        Assert.Equal(1, host.DiscardPrompts);

        gate.Set();
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "the discard never resolved");
        host.PumpFor(TimeSpan.FromMilliseconds(100));

        // The next close asks, with core's discarded reason, never ClosedKept.
        host.ClosePrompt = () => VaultCloseDecision.Cancel;
        Assert.Equal(WorkspaceTeardown.Refused, host.Close(route));
        Assert.Equal(asked + 1, host.ClosePrompts);
        Assert.Contains(host.Announced, item => item is A11yEvent.UnsavedNotesNotKept notKept
            && notKept.Reason == SlateUniffiMethods.HotExitReasonDiscarded());
        host.Crash();
        host.Open();
        host.AssertRead();
        Assert.False(host.TabOn("a.md").IsDirty, "the discarded text came back");
    }

    /// <summary>A real vault and lifecycle over a private store host.</summary>
    private sealed class HotExitHost : IDisposable
    {
        private readonly string _stateDir;
        private readonly TimeSpan _settle = VaultLifecycleViewModel.HotExitSettleBudget;
        private readonly TimeSpan _discard = VaultLifecycleViewModel.HotExitDiscardBudget;
        private readonly TimeSpan _reopen = VaultLifecycleViewModel.HotExitReopenWait;
        private bool _shortened;

        internal HotExitHost()
        {
            Dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher));
            Root = Path.Combine(Path.GetTempPath(), $"slate-hot-exit-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "a.md"), "# a\n");
            File.WriteAllText(Path.Combine(Root, "b.md"), "# b\n");
            DeviceRoot = Path.Combine(Path.GetTempPath(), $"slate-hot-exit-device-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DeviceRoot);
            _stateDir = Path.Combine(Path.GetTempPath(), $"slate-hot-exit-state-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_stateDir);
            Store = HotExitStoreHost.Install(DeviceRoot, static _ => true);
            Assert.True(Store.HasStore);
        }

        internal Dispatcher Dispatcher { get; }
        internal string Root { get; }
        internal string DeviceRoot { get; }
        internal HotExitStoreHost Store { get; private set; }
        internal VaultLifecycleViewModel Lifecycle { get; private set; } = null!;
        internal HotExitVaultKey Key { get; private set; } = null!;
        internal List<A11yEvent> Announced { get; } = [];
        internal Func<VaultCloseDecision> ClosePrompt { get; set; } = () => VaultCloseDecision.Cancel;
        internal Func<DiscardPendingDecision> DiscardPrompt { get; set; } = () => DiscardPendingDecision.Cancel;
        internal int ClosePrompts { get; private set; }
        internal int DiscardPrompts { get; private set; }

        internal WorkspaceViewModel Workspace => Assert.IsType<WorkspaceViewModel>(Lifecycle.Workspace);

        /// <summary>Facts that hold a call past a budget keep the wait short.</summary>
        internal void ShortenBudgets()
        {
            _shortened = true;
            VaultLifecycleViewModel.HotExitSettleBudget = TimeSpan.FromMilliseconds(400);
            VaultLifecycleViewModel.HotExitDiscardBudget = TimeSpan.FromMilliseconds(400);
            VaultLifecycleViewModel.HotExitReopenWait = TimeSpan.FromMilliseconds(400);
        }

        internal void Open()
        {
            Announced.Clear();
            Lifecycle = new VaultLifecycleViewModel(
                pickVault: () => Task.FromResult<string?>(Root),
                enqueueUi: action => Dispatcher.BeginInvoke(action),
                recentVaultsStore: new RecentVaultsStore(Path.Combine(_stateDir, "recent-vaults.json")),
                announce: Announced.Add,
                confirmUnsavedClose: () =>
                {
                    ClosePrompts++;
                    return ClosePrompt();
                },
                confirmDiscardPending: () =>
                {
                    DiscardPrompts++;
                    return DiscardPrompt();
                })
            {
                HotExitHostForTests = Store,
            };
            Task open = Lifecycle.OpenVaultAsync(Root);
            Assert.True(PumpedDispatcher.PumpUntil(() => open.IsCompleted, TimeSpan.FromSeconds(60)), "never opened");
            open.GetAwaiter().GetResult();
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => Lifecycle.FileSidebar is { } sidebar
                        && sidebar.TreeRefreshCompletion.IsCompleted
                        && !sidebar.IsLoadingChildren
                        && !sidebar.IsExpandingLoaded,
                    TimeSpan.FromSeconds(30)),
                "the sidebar never settled");
            Key = Assert.IsType<HotExitKeyResult.Key>(Lifecycle.SessionForTests!.HotExitKey()).KeyValue;
            PumpedDispatcher.Drain();
        }

        internal WorkspaceTabViewModel OpenNote(string path)
        {
            Workspace.OpenPath(path);
            PumpedDispatcher.Drain();
            return TabOn(path);
        }

        internal WorkspaceTabViewModel TabOn(string path) =>
            Workspace.Groups.SelectMany(group => group.Tabs).First(tab => tab.Path == path);

        internal WorkspaceTeardown Close(TeardownRoute route) => Lifecycle.TryCloseForTests(route);

        /// <summary>The crash shape: disposal with no teardown route.</summary>
        internal void Crash()
        {
            Lifecycle.Dispose();
            PumpedDispatcher.Drain();
        }

        /// <summary>Calls still held never issue; a fresh host owns the store.</summary>
        internal void NewProcess()
        {
            Lifecycle.Dispose();
            PumpedDispatcher.Drain();
            Store.SimulateProcessDeathForTests();
            Store = HotExitStoreHost.Install(DeviceRoot, static _ => true);
        }

        /// <summary>The open held the vault and its read succeeded.</summary>
        internal void AssertRead()
        {
            Assert.True(Store.HoldsVaultForTests(Key), "the open does not hold the vault");
            Assert.NotNull(Workspace.HotExit);
            Assert.False(Workspace.HotExit!.IsInertForTests, "the open's read did not succeed");
        }

        internal string Disk(string path) => File.ReadAllText(Path.Combine(Root, path));

        /// <summary>The store as a fresh reader sees it, through no hold.</summary>
        internal HotExitSnapshot Snapshot()
        {
            using HotExitStore store = HotExitStore.Open(DeviceRoot);
            return store.ReadBuffers(Key);
        }

        internal HotExitEntry? StoredEntry(WorkspaceTabViewModel tab) =>
            Snapshot().Entries.FirstOrDefault(entry => entry.EntryId == tab.RecoveryEntryId);

        internal string? StoredText(WorkspaceTabViewModel tab) => StoredEntry(tab)?.Text;

        internal void PumpUntil(Func<bool> condition, string message) =>
            Assert.True(PumpedDispatcher.PumpUntil(condition, Patience), message);

        internal void PumpFor(TimeSpan span)
        {
            DateTime until = DateTime.UtcNow + span;
            _ = PumpedDispatcher.PumpUntil(() => DateTime.UtcNow >= until, span + TimeSpan.FromSeconds(5));
        }

        public void Dispose()
        {
            if (_shortened)
            {
                VaultLifecycleViewModel.HotExitSettleBudget = _settle;
                VaultLifecycleViewModel.HotExitDiscardBudget = _discard;
                VaultLifecycleViewModel.HotExitReopenWait = _reopen;
            }
            Store.BeforeCallForTests = null;
            Lifecycle?.Dispose();
            PumpedDispatcher.Drain();
            foreach (string directory in new[] { Root, DeviceRoot, _stateDir })
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // A hold or a held call may still have the file open.
                }
            }
        }
    }
}
