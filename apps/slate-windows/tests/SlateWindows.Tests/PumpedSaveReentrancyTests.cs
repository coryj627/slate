// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1280 round 2 (contract 38 D-10 as amended — the pumped-wait invariant).
/// A save waits for its worker in a nested dispatcher frame, and anything the
/// dispatcher can run may run inside it. Every caller that waits that way —
/// Save, Save All, close tab, close pane, the replace gate and vault teardown —
/// must survive every mutation of the tab set or a tab's identity that can land
/// in the frame. Each cell of the matrix parks the first save's worker inside
/// the write, runs the mutation from the frame, then releases the write; the
/// theory asserts what every cell shares, and the named facts pin the exact
/// outcomes the review called out.
/// </summary>
/// <remarks>
/// The fixture is a real vault opened through the vault lifecycle, so
/// renames and deletes arrive as the production file events do and teardown
/// is the real close: pane 1 holds the dirty target (note0) and a clean
/// sibling (note1); pane 2 holds the target's same-path peer and a clean note
/// (note2). Every dirty-tab prompt answers Save, so no typed text may vanish.
/// </remarks>
public sealed class PumpedSaveReentrancyTests
{
    private static readonly string[] Sites =
        ["save", "save-all", "close-tab", "close-pane", "replace", "teardown"];

    private static readonly string[] Mutations =
    [
        "close-same", "close-sibling", "close-pane", "open-edit", "navigate",
        "duplicate", "move", "rename", "delete", "save-all", "save-peer",
        "teardown", "type", "type-peer",
    ];

    public static TheoryData<string, string> Matrix()
    {
        var data = new TheoryData<string, string>();
        foreach (string site in Sites)
        {
            foreach (string mutation in Mutations)
            {
                data.Add(site, mutation);
            }
        }
        return data;
    }

    /// <summary>Every cell of the pumped-site × mutation matrix: no
    /// exception, no typed text lost, no false conflict, no duplicated
    /// confirmation, every announcement on the dispatcher, a consistent
    /// workspace, and no save still admitted or writing afterwards.</summary>
    [Theory]
    [MemberData(nameof(Matrix))]
    public void EveryPumpedCallerSurvivesEveryMutation(string site, string mutation)
    {
        using var host = new Host();
        host.RunCell(site, mutation);
        host.AssertInvariants(site, mutation);
    }

    /// <summary>Codex round 1's critical finding: the same tab closed from
    /// inside its own save-before-close. The inner close completes; the outer
    /// finds the tab gone and stops — one TabClosed, never a stale index.</summary>
    [Fact]
    public void ACloseDuringItsOwnSavesPumpClosesOnce()
    {
        using var host = new Host();
        host.RunCell("close-tab", "close-same");
        host.AssertInvariants("close-tab", "close-same");

        Assert.Single(host.Announced, item => item.Event is A11yEvent.TabClosed);
        Assert.True(host.T.IsDisposed);
        Assert.DoesNotContain(host.T, host.LiveTabs());
        Assert.Contains("Marker-A", host.Disk("note0.md"), StringComparison.Ordinal);
    }

    /// <summary>A tab opened into the pane and typed into while the pane's
    /// close is saving: the close admits it — saves it — before disposing
    /// the pane, instead of disposing it past its dirty gate.</summary>
    [Fact]
    public void AClosePaneAdmitsATabOpenedDuringItsSave()
    {
        using var host = new Host();
        host.RunCell("close-pane", "open-edit");
        host.AssertInvariants("close-pane", "open-edit");

        Assert.DoesNotContain(host.G1, host.Workspace.Groups);
        WorkspaceTabViewModel opened = Assert.IsType<WorkspaceTabViewModel>(host.N);
        Assert.True(opened.IsDisposed);
        Assert.Contains("Marker-N", host.Disk("note4.md"), StringComparison.Ordinal);
        Assert.Contains("Marker-A", host.Disk("note0.md"), StringComparison.Ordinal);
    }

    /// <summary>Save All with a tab opened and typed into mid-way: every
    /// dirty tab is saved, each exactly once, and the answer is saved.</summary>
    [Fact]
    public void SaveAllReachesATabOpenedMidway()
    {
        using var host = new Host();
        host.RunCell("save-all", "open-edit");
        host.AssertInvariants("save-all", "open-edit");

        Assert.True(host.SiteAnswer);
        Assert.Contains("Marker-A", host.Disk("note0.md"), StringComparison.Ordinal);
        Assert.Contains("Marker-N", host.Disk("note4.md"), StringComparison.Ordinal);
        Assert.Equal(1, host.WritesTo("note0.md"));
        Assert.Equal(1, host.WritesTo("note4.md"));
        Assert.DoesNotContain(host.LiveTabs(), tab => tab.IsDirty);
    }

    /// <summary>Typing while a save with a same-path peer is writing: one
    /// confirmation, the snapshot on disk, and BOTH tabs dirty with the typed
    /// text — the peer adopts the saved baseline behind its edits instead of
    /// throwing out of the publication.</summary>
    [Fact]
    public void TypingDuringASaveWithAPeerKeepsBothDirty()
    {
        using var host = new Host();
        host.RunCell("save", "type");
        host.AssertInvariants("save", "type");

        Assert.Single(host.Announced, item => item.Event is A11yEvent.NoteSaved);
        Assert.True(host.T.IsDirty);
        Assert.True(host.P.IsDirty);
        Assert.Contains("Marker-T2", host.T.Text, StringComparison.Ordinal);
        Assert.Contains("Marker-T2", host.P.Text, StringComparison.Ordinal);
        string disk = host.Disk("note0.md");
        Assert.Contains("Marker-A", disk, StringComparison.Ordinal);
        Assert.DoesNotContain("Marker-T2", disk, StringComparison.Ordinal);
    }

    /// <summary>The file renamed or deleted under an in-flight save — after
    /// the write landed, or before it could: the old write's outcome is
    /// retired silently. A rename leaves the tab at its new path (a landed
    /// write adopted as its baseline) and its next save succeeds with no
    /// false conflict; a deletion keeps the missing-file status and the
    /// unsaved text.</summary>
    [Theory]
    [InlineData("rename", true)]
    [InlineData("rename", false)]
    [InlineData("delete", true)]
    [InlineData("delete", false)]
    public void ARenameOrDeleteRetiresTheInFlightPublication(string change, bool landed)
    {
        using var host = new Host();
        host.RunCell("save", change, parkAfterWrite: landed);
        host.AssertInvariants("save", change);

        Assert.DoesNotContain(
            host.Announced,
            item => item.Event is A11yEvent.NoteSaved
                or A11yEvent.NoteSaveConflict
                or A11yEvent.NoteSaveBlocked);
        if (change == "rename")
        {
            Assert.Equal("renamed0.md", host.T.Path);
            Assert.Equal(landed, !host.T.IsDirty);
            if (landed)
            {
                Assert.Contains("Marker-A", host.Disk("renamed0.md"), StringComparison.Ordinal);
            }

            host.Type(host.T, "Marker-B");
            host.Announced.Clear();
            host.Workspace.SaveActiveCommand.Execute(null);
            host.Settle();

            var saved = Assert.Single(host.Announced, item => item.Event is A11yEvent.NoteSaved);
            Assert.Equal("renamed0.md", Assert.IsType<A11yEvent.NoteSaved>(saved.Event).Filename);
            Assert.DoesNotContain(
                host.Announced,
                item => item.Event is A11yEvent.NoteSaveConflict or A11yEvent.NoteSaveBlocked);
            string disk = host.Disk("renamed0.md");
            Assert.Contains("Marker-A", disk, StringComparison.Ordinal);
            Assert.Contains("Marker-B", disk, StringComparison.Ordinal);
        }
        else
        {
            Assert.True(host.T.IsMissingFromDisk);
            Assert.Equal(
                "note0.md no longer exists on disk. Unsaved editor content is preserved.",
                host.T.Status);
            Assert.True(host.T.IsDirty);
            Assert.Contains("Marker-A", host.T.Text, StringComparison.Ordinal);
        }
    }

    /// <summary>Two tabs on one file, a save on each while the first is
    /// writing: the second waits for the first's publication and starts from
    /// the hash it recorded — two confirmations in order, one write, and no
    /// false "modified externally".</summary>
    [Fact]
    public void PeersSaveInOrderWithoutAFalseConflict()
    {
        using var host = new Host();
        host.RunCell("save", "save-peer");
        host.AssertInvariants("save", "save-peer");

        (A11yEvent Event, int Thread, string Disk)[] saved =
            [.. host.Announced.Where(item => item.Event is A11yEvent.NoteSaved)];
        Assert.Equal(2, saved.Length);
        Assert.All(saved, item => Assert.Contains("Marker-A", item.Disk, StringComparison.Ordinal));
        Assert.DoesNotContain(
            host.Announced,
            item => item.Event is A11yEvent.NoteSaveConflict or A11yEvent.NoteSaveBlocked);
        Assert.Equal(1, host.WritesTo("note0.md"));
        Assert.False(host.T.IsDirty);
        Assert.False(host.P.IsDirty);
    }

    /// <summary>Contract 35 A-1: a vault close while a save is writing — the
    /// user would choose Discard — settles the admitted save first. It
    /// publishes, so nothing is left to ask about: the prompt never appears,
    /// the text is on disk, the close is announced, and the session goes only
    /// after the workers are joined.</summary>
    [Fact]
    public void TeardownSettlesAnAdmittedSaveBeforeItAsks()
    {
        using var host = new Host(VaultCloseDecision.Discard);
        host.RunCell("save", "teardown");
        host.AssertInvariants("save", "teardown");

        Assert.Null(host.Lifecycle.Workspace);
        Assert.Equal(0, host.ClosePrompts);
        Assert.Single(host.Announced, item => item.Event is A11yEvent.NoteSaved);
        Assert.Single(host.Announced, item => item.Event is A11yEvent.VaultClosed);
        Assert.DoesNotContain(host.Announced, item => item.Event is A11yEvent.VaultClosedChangesDiscarded);
        Assert.Contains("Marker-A", host.Disk("note0.md"), StringComparison.Ordinal);
        Assert.True(host.Workspace.SavesForTests.IsClosed);
        Assert.Equal(0, host.Workspace.SavesForTests.LiveWorkersForTests);
    }

    /// <summary>Contract 35 A-1, without the settle: the lifecycle disposed
    /// while a save's worker is still writing (the shutdown path). Disposal
    /// joins the worker — its write lands before the session is disposed —
    /// and no worker ever reaches a disposed session.</summary>
    [Fact]
    public void DisposalJoinsASaveWorkerBeforeTheSessionGoes()
    {
        using var host = new Host();
        host.HookAll();
        bool landedBeforeDisposal = false;
        int workersAfterDisposal = -1;
        host.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                try
                {
                    Assert.True(host.WaitParked(), "no write parked");
                    // The join holds the dispatcher, so the write is released
                    // from another thread a moment into the disposal.
                    var releaser = new Thread(() =>
                    {
                        Thread.Sleep(300);
                        host.Release();
                    })
                    {
                        IsBackground = true,
                    };
                    releaser.Start();
                    host.DisposeLifecycle();
                    landedBeforeDisposal = host.Disk("note0.md").Contains("Marker-A", StringComparison.Ordinal);
                    workersAfterDisposal = host.Workspace.SavesForTests.LiveWorkersForTests;
                }
                catch (Exception exception)
                {
                    host.Faults.Add(exception);
                    host.Release();
                }
            }));

        host.Workspace.SaveActiveCommand.Execute(null);
        host.Settle();

        Assert.True(host.Faults.Count == 0, string.Join("\n---\n", host.Faults));
        Assert.True(landedBeforeDisposal, "the session was disposed before the admitted write landed");
        Assert.Equal(0, workersAfterDisposal);
        Assert.True(host.Workspace.SavesForTests.IsClosed);
        Assert.Null(host.Lifecycle.Workspace);
        // The publication found its tab disposed and said nothing.
        Assert.DoesNotContain(
            host.Announced,
            item => item.Event is A11yEvent.NoteSaved
                or A11yEvent.NoteSaveConflict
                or A11yEvent.NoteSaveBlocked);
    }

    internal sealed class Host : IDisposable
    {
        private readonly SynchronizationContext? _previousContext;
        private readonly string _stateDir;
        private readonly ManualResetEventSlim _parked = new();
        private readonly ManualResetEventSlim _release = new();
        private readonly Lock _writesGate = new();
        private readonly Dictionary<string, int> _writes = new(StringComparer.Ordinal);
        private int _parkCandidates;
        private bool _parkAfterWrite;
        private bool _lifecycleDisposed;

        internal Host(VaultCloseDecision closeDecision = VaultCloseDecision.SaveAll)
        {
            _previousContext = SynchronizationContext.Current;
            Dispatcher = Dispatcher.CurrentDispatcher;
            DispatcherThread = Environment.CurrentManagedThreadId;
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher));

            Root = Path.Combine(Path.GetTempPath(), $"slate-pumped-save-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            for (int n = 0; n < 6; n++)
            {
                File.WriteAllText(Path.Combine(Root, $"note{n}.md"), $"# Note {n}\n\nBody {n}.\n");
            }
            _stateDir = Path.Combine(Path.GetTempPath(), $"slate-pumped-save-state-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_stateDir);

            Lifecycle = new VaultLifecycleViewModel(
                pickVault: () => Task.FromResult<string?>(Root),
                enqueueUi: action => Dispatcher.BeginInvoke(action),
                recentVaultsStore: new RecentVaultsStore(Path.Combine(_stateDir, "recent-vaults.json")),
                announce: item => Announced.Add(
                    (item, Environment.CurrentManagedThreadId, DiskOrEmpty("note0.md"))),
                confirmUnsavedClose: () =>
                {
                    ClosePrompts++;
                    return closeDecision;
                },
                confirmDirtyNavigation: (_, _) => WorkspaceDirtyNavigationDecision.Save,
                confirmDirtyClose: _ => WorkspaceDirtyNavigationDecision.Save);
            Task open = Lifecycle.OpenVaultAsync(Root);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => open.IsCompleted, TimeSpan.FromSeconds(60)),
                "the vault never opened");
            open.GetAwaiter().GetResult();
            Workspace = Assert.IsType<WorkspaceViewModel>(Lifecycle.Workspace);
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => Lifecycle.FileSidebar is { } sidebar
                        && sidebar.TreeRefreshCompletion.IsCompleted
                        && !sidebar.IsLoadingChildren
                        && !sidebar.IsExpandingLoaded,
                    TimeSpan.FromSeconds(30)),
                "the sidebar never settled");

            Workspace.OpenPath("note0.md");
            T = ActiveTab();
            Workspace.OpenPath("note1.md", WorkspaceOpenTarget.NewTab);
            S = ActiveTab();
            Workspace.OpenPath("note0.md");
            Assert.Same(T, ActiveTab());
            G1 = Workspace.ActiveGroup;
            Workspace.OpenPath("note0.md", WorkspaceOpenTarget.SplitRight);
            G2 = Workspace.ActiveGroup;
            Assert.NotSame(G1, G2);
            P = ActiveTab();
            Workspace.OpenPath("note2.md", WorkspaceOpenTarget.NewTab);
            U = ActiveTab();
            Workspace.SelectGroupFromKeyboardFocus(G1);
            Assert.Same(G1, Workspace.ActiveGroup);
            Assert.Same(T, G1.ActiveTab);
            Assert.Equal(new[] { T, S }, G1.Tabs);
            Assert.Equal(new[] { P, U }, G2.Tabs);
            PumpedDispatcher.Drain();

            Type(T, "Marker-A");
            Assert.True(T.IsDirty, "the target is not dirty");
            Assert.True(P.IsDirty, "the peer did not mirror the edit");
            Announced.Clear();
        }

        internal Dispatcher Dispatcher { get; }
        internal int DispatcherThread { get; }
        internal string Root { get; }
        internal VaultLifecycleViewModel Lifecycle { get; }

        /// <summary>The workspace the fixture opened — kept after a teardown
        /// disposes it, so its save coordinator can be inspected.</summary>
        internal WorkspaceViewModel Workspace { get; }

        internal WorkspaceGroupViewModel G1 { get; }
        internal WorkspaceGroupViewModel G2 { get; }
        internal WorkspaceTabViewModel T { get; }
        internal WorkspaceTabViewModel S { get; }
        internal WorkspaceTabViewModel P { get; }
        internal WorkspaceTabViewModel U { get; }

        /// <summary>The tab the open-edit mutation opened.</summary>
        internal WorkspaceTabViewModel? N { get; private set; }

        internal List<(A11yEvent Event, int Thread, string Disk)> Announced { get; } = [];
        internal List<Exception> Faults { get; } = [];
        internal HashSet<string> Markers { get; } = new(StringComparer.Ordinal);
        internal bool MutationRanWhileParked { get; private set; }
        internal bool SiteAnswer { get; private set; }
        internal int ClosePrompts { get; private set; }

        private VaultSession Session =>
            Lifecycle.SessionForTests ?? throw new InvalidOperationException("the vault is closed");

        /// <summary>One cell: park the first write, run the mutation from the
        /// frame the site pumps, release the write, then settle.</summary>
        internal void RunCell(string site, string mutation, bool parkAfterWrite = false)
        {
            _parkAfterWrite = parkAfterWrite;
            HookAll();
            Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    try
                    {
                        if (!WaitParked())
                        {
                            throw new TimeoutException($"{site}: no write parked");
                        }
                        MutationRanWhileParked = !_release.IsSet;
                        // Released from the frame the mutation runs in, or
                        // right after it when the mutation does not pump.
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Release));
                        Mutate(mutation);
                        HookAll();
                    }
                    catch (Exception exception)
                    {
                        Faults.Add(exception);
                        Release();
                    }
                }));

            try
            {
                RunSite(site);
            }
            catch (Exception exception)
            {
                Faults.Add(exception);
            }
            finally
            {
                Release();
            }
            Settle();
        }

        internal void AssertInvariants(string site, string mutation)
        {
            string cell = $"{site} × {mutation}";
            Assert.True(Faults.Count == 0, $"{cell}:\n" + string.Join("\n---\n", Faults));
            Assert.True(MutationRanWhileParked, $"{cell}: the mutation did not run while the write was parked");
            Assert.All(Announced, item => Assert.Equal(DispatcherThread, item.Thread));

            // A deletion is a real conflict for a save that still names the
            // file; nothing else in the matrix changes the file behind a tab.
            if (mutation != "delete")
            {
                Assert.DoesNotContain(
                    Announced,
                    item => item.Event is A11yEvent.NoteSaveConflict or A11yEvent.NoteSaveBlocked);
            }
            int saveCommands = (site == "save" ? 1 : 0) + (mutation == "save-peer" ? 1 : 0);
            int confirmations = Announced.Count(item => item.Event is A11yEvent.NoteSaved);
            Assert.True(
                confirmations <= saveCommands,
                $"{cell}: {confirmations} save confirmations for {saveCommands} Save commands");

            // Every decision was Save: every typed marker is on disk or in a
            // tab that is still open.
            string disk = AllDiskText();
            WorkspaceTabViewModel[] live = LiveTabs();
            foreach (string marker in Markers)
            {
                Assert.True(
                    disk.Contains(marker, StringComparison.Ordinal)
                        || live.Any(tab => tab.Text.Contains(marker, StringComparison.Ordinal)),
                    $"{cell}: {marker} is on neither disk nor an open tab");
            }

            if (Lifecycle.Workspace is WorkspaceViewModel open)
            {
                Assert.Contains(open.ActiveGroup, open.Groups);
                foreach (WorkspaceGroupViewModel group in open.Groups)
                {
                    Assert.True(
                        group.ActiveTab is null || group.Tabs.Contains(group.ActiveTab),
                        $"{cell}: a group's active tab is not one of its tabs");
                    Assert.DoesNotContain(group.Tabs, tab => tab.IsDisposed);
                }
                Assert.True(
                    open.Groups.Count == 1 || open.Groups.All(group => group.Tabs.Count > 0),
                    $"{cell}: an empty pane was left beside others");
                Assert.True(open.SavesIdle, $"{cell}: a save is still admitted");
            }
            else
            {
                Assert.True(Workspace.SavesForTests.IsClosed, $"{cell}: the vault closed with its saves open");
            }
            Assert.Equal(0, Workspace.SavesForTests.LiveWorkersForTests);
        }

        internal bool WaitParked() => _parked.Wait(TimeSpan.FromSeconds(10));

        internal void Release() => _release.Set();

        /// <summary>Every tab's save counts its landed writes by path, and the
        /// first write anywhere parks — before the core write, or after it
        /// when the cell asks for a write that has landed.</summary>
        internal void HookAll()
        {
            foreach (WorkspaceTabViewModel tab in Lifecycle.Workspace?.Groups.SelectMany(group => group.Tabs) ?? [])
            {
                WorkspaceTabViewModel hooked = tab;
                hooked.SaveWriteHookForTests = () =>
                {
                    if (!_parkAfterWrite)
                    {
                        MaybePark();
                    }
                };
                hooked.SaveWrittenHookForTests = () =>
                {
                    lock (_writesGate)
                    {
                        _writes[hooked.Path] = _writes.GetValueOrDefault(hooked.Path) + 1;
                    }
                    if (_parkAfterWrite)
                    {
                        MaybePark();
                    }
                };
            }
        }

        internal int WritesTo(string path)
        {
            lock (_writesGate)
            {
                return _writes.GetValueOrDefault(path);
            }
        }

        internal void Type(WorkspaceTabViewModel tab, string marker)
        {
            Markers.Add(marker);
            tab.EditorDocument!.Insert(tab.EditorDocument.TextLength, $"\n{marker}.\n");
        }

        internal WorkspaceTabViewModel[] LiveTabs() =>
            [.. Lifecycle.Workspace?.Groups.SelectMany(group => group.Tabs).Where(tab => !tab.IsDisposed) ?? []];

        internal string Disk(string path) => File.ReadAllText(Path.Combine(Root, path));

        /// <summary>Pump until no save is admitted or writing, then drain what
        /// the publications queued.</summary>
        internal void Settle()
        {
            PumpedDispatcher.PumpUntil(
                () => Workspace.SavesIdle && Workspace.SavesForTests.LiveWorkersForTests == 0,
                TimeSpan.FromSeconds(30));
            for (int drain = 0; drain < 3; drain++)
            {
                PumpedDispatcher.Drain();
            }
        }

        internal void DisposeLifecycle()
        {
            _lifecycleDisposed = true;
            Lifecycle.Dispose();
        }

        public void Dispose()
        {
            Release();
            try
            {
                Settle();
            }
            finally
            {
                if (!_lifecycleDisposed)
                {
                    DisposeLifecycle();
                }
                SynchronizationContext.SetSynchronizationContext(_previousContext);
                _parked.Dispose();
                _release.Dispose();
                TryDelete(Root);
                TryDelete(_stateDir);
            }
        }

        private void RunSite(string site)
        {
            switch (site)
            {
                case "save": Workspace.SaveActiveCommand.Execute(null); break;
                case "save-all": SiteAnswer = Workspace.SaveAll(); break;
                case "close-tab": Workspace.CloseTabCommand.Execute(T); break;
                case "close-pane": Workspace.ClosePaneCommand.Execute(null); break;
                case "replace": Workspace.OpenPath("note3.md"); break;
                case "teardown": Lifecycle.CloseVault(); break;
                default: throw new ArgumentOutOfRangeException(nameof(site), site, null);
            }
        }

        private void Mutate(string mutation)
        {
            switch (mutation)
            {
                case "close-same": Workspace.CloseTabCommand.Execute(T); break;
                case "close-sibling": Workspace.CloseTabCommand.Execute(S); break;
                case "close-pane": Workspace.ClosePaneCommand.Execute(null); break;
                case "open-edit":
                    Workspace.OpenPath("note4.md", WorkspaceOpenTarget.NewTab);
                    N = ActiveTab();
                    HookAll();
                    Type(N, "Marker-N");
                    break;
                case "navigate": Workspace.OpenPath("note5.md"); break;
                case "duplicate": Workspace.DuplicateTabCommand.Execute(null); break;
                case "move": Workspace.MoveTabRightCommand.Execute(null); break;
                case "rename": _ = Session.RenameFile("note0.md", "renamed0.md"); break;
                case "delete": DeleteOnSta("note0.md"); break;
                case "save-all": _ = Workspace.SaveAll(); break;
                case "save-peer":
                    Workspace.SelectGroupFromKeyboardFocus(G2);
                    G2.ActiveTab = P;
                    Workspace.SaveActiveCommand.Execute(null);
                    break;
                case "teardown": Lifecycle.CloseVault(); break;
                case "type": Type(T, "Marker-T2"); break;
                case "type-peer": Type(P, "Marker-P"); break;
                default: throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }
        }

        /// <summary>DeleteFile routes through the system trash, whose COM
        /// init needs an STA thread (the app's UI thread is one; the xunit
        /// thread is not — the HistoryPanelTests precedent). The Deleted
        /// event still arrives through the lifecycle's listener.</summary>
        private void DeleteOnSta(string path)
        {
            VaultSession session = Session;
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    session.DeleteFile(path);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure is not null)
            {
                throw failure;
            }
        }

        private void MaybePark()
        {
            if (Interlocked.Increment(ref _parkCandidates) == 1)
            {
                _parked.Set();
                _release.Wait(TimeSpan.FromSeconds(20));
            }
        }

        private WorkspaceTabViewModel ActiveTab() =>
            Assert.IsType<WorkspaceTabViewModel>(Workspace.ActiveGroup.ActiveTab);

        private string DiskOrEmpty(string path)
        {
            try
            {
                return File.ReadAllText(Path.Combine(Root, path));
            }
            catch (IOException)
            {
                return string.Empty;
            }
        }

        private string AllDiskText() =>
            string.Join(
                "\n",
                Directory.EnumerateFiles(Root, "*.md", SearchOption.AllDirectories)
                    .Select(File.ReadAllText));

        private static void TryDelete(string directory)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
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
